import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Observable } from 'rxjs';

import { ApiService, errorKey } from '../../core/api/api.service';
import {
  Audience,
  BroadcastInput,
  DevicePlatform,
  LanguageOutput,
  BaseResponse,
  PushDispatchOutput,
  PushOverviewOutput,
  PushRunOutput,
  PushStatus,
} from '../../core/api/models';
import { AppDatePipe } from '../../core/pipes/app-date.pipe';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * The push manager.
 *
 * It answers one question that nothing else in the CMS could: a broadcast
 * reports "4 targeted, 0 delivered" and an admin has no way to tell which of
 * four unrelated things happened — nobody holds a token, the readers muted
 * notifications, the sender is not running, or Firebase is rejecting what we
 * send. Each has a different fix and three of them are invisible from the
 * broadcast row.
 *
 * It also sends. That is a shortcut, not a second implementation: the send
 * posts to one endpoint that composes a broadcast and hands it to the same
 * service the broadcasts screen uses, so a message sent from here lands in the
 * same history with the same counters and the same audit trail — and shows up
 * in the outcomes on this very page.
 *
 * What stays on the broadcasts screen is *scheduling*. A message with a time on
 * it can still be edited or withdrawn before it goes, and that needs somewhere
 * to read it back from; a send-now has no such window.
 */
@Component({
  selector: 'app-push',
  imports: [FormsModule, AppDatePipe, TranslatePipe],
  templateUrl: './push.component.html',
})
export class PushComponent {
  private readonly api = inject(ApiService);

  protected readonly overview = signal<PushOverviewOutput | null>(null);
  protected readonly loading = signal(true);
  protected readonly windowDays = signal(7);

  protected readonly windows = [1, 7, 30];

  // ─────────────────────────────── sending ───────────────────────────────

  protected readonly languages = signal<LanguageOutput[]>([]);
  protected readonly composing = signal(false);
  protected readonly sending = signal(false);
  protected readonly sent = signal<number | null>(null);
  protected readonly sendError = signal<string | null>(null);

  protected readonly Audience = Audience;
  protected readonly Status = PushStatus;

  // ── the delivery log ──
  //
  // The overview's twenty-five recent failures answer "is something wrong".
  // This answers what comes next — which sends, to what kind of install, and
  // what Firebase actually said — and it is where a failed send is retried or a
  // queued one withdrawn.
  protected readonly log = signal<PushDispatchOutput[]>([]);
  protected readonly logTotal = signal(0);
  protected readonly loadingLog = signal(false);
  protected readonly logStatus = signal<PushStatus | null>(null);
  protected readonly logError = signal<string | null>(null);

  protected readonly running = signal(false);
  protected readonly ranSummary = signal<PushRunOutput | null>(null);

  protected readonly logFilters = [
    { value: null, key: 'push.log.all' },
    { value: PushStatus.Pending, key: 'push.pending' },
    { value: PushStatus.Sent, key: 'push.sent' },
    { value: PushStatus.Failed, key: 'push.failed' },
    { value: PushStatus.Skipped, key: 'push.skipped' },
    { value: PushStatus.TokenExpired, key: 'push.tokenExpired' },
  ];

  protected readonly audiences = [
    { value: Audience.All, key: 'reminders.audience.all' },
    { value: Audience.Language, key: 'reminders.audience.language' },
    { value: Audience.Platform, key: 'reminders.audience.platform' },
    { value: Audience.Device, key: 'push.audience.device' },
  ];

  protected readonly platforms = [
    { value: DevicePlatform.Android, label: 'Android' },
    { value: DevicePlatform.Ios, label: 'iOS' },
    { value: DevicePlatform.Web, label: 'Web' },
  ];

  /**
   * Starts on a single device rather than on everyone.
   *
   * The default is the blast radius: an admin who opens this to check that push
   * works at all, and taps send without reading the audience, should reach one
   * phone — their own — and not every install.
   */
  protected readonly draft = signal<BroadcastInput>(this.emptyDraft());

  private emptyDraft(): BroadcastInput {
    return {
      audience: Audience.Device,
      targetLanguageCode: null,
      targetPlatform: null,
      targetDeviceKey: null,
      scheduledAtUtc: null,
      route: null,
      translations: [{ languageCode: 'ar', title: '', body: '' }],
    };
  }

  /**
   * The delivery rate as a whole percent.
   *
   * Rounded here rather than in a template expression so the "no attempts yet"
   * case has somewhere to live: a rate of 0 out of 0 is not a bad rate, it is
   * the absence of one, and showing «٠٪» would read as a total failure.
   */
  protected readonly deliveryPercent = computed(() => {
    const outcomes = this.overview()?.outcomes;
    if (!outcomes) return null;

    const attempted = outcomes.sent + outcomes.failed + outcomes.tokenExpired;
    return attempted === 0 ? null : Math.round(outcomes.deliveryRate * 100);
  });

  /** Reach as a share of installs — what a broadcast can hope to land on. */
  protected readonly reachPercent = computed(() => {
    const reach = this.overview()?.reach;
    if (!reach || reach.totalDevices === 0) return null;

    return Math.round((reach.reachable / reach.totalDevices) * 100);
  });

  /**
   * The one condition that means the pipeline is stuck rather than idle.
   *
   * Pending rows whose moment has passed can only mean the sender worker is not
   * running: everything else it does — skipping a muted device, retiring a dead
   * token — closes the row.
   */
  protected readonly isStalled = computed(
    () => (this.overview()?.queue.overdueDispatches ?? 0) > 0,
  );

  constructor() {
    this.load();
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));
  }

  protected compose(): void {
    this.draft.set(this.emptyDraft());
    this.sent.set(null);
    this.sendError.set(null);
    this.composing.set(true);
  }

  protected cancel(): void {
    this.composing.set(false);
  }

  /**
   * What the send needs, checked at submit rather than as a `computed`.
   *
   * `ngModel` mutates the draft object in place, which never notifies the
   * signal holding it — a computed gate would be evaluated once against the
   * empty draft and stay false, leaving the button disabled forever however
   * much the admin typed. Reading the object when the button is pressed is what
   * the rest of the CMS does, and it is the only thing that sees the typing.
   */
  private missingField(draft: BroadcastInput): string | null {
    const first = draft.translations[0];

    if (!first?.title?.trim()) return 'push.needsTitle';
    if (!first?.body?.trim()) return 'push.needsBody';

    if (draft.audience === Audience.Device && !draft.targetDeviceKey?.trim()) {
      return 'push.needsDeviceKey';
    }
    if (draft.audience === Audience.Language && !draft.targetLanguageCode) {
      return 'push.needsLanguage';
    }
    if (draft.audience === Audience.Platform && draft.targetPlatform == null) {
      return 'push.needsPlatform';
    }

    return null;
  }

  protected send(): void {
    if (this.sending()) return;

    const draft = this.draft();

    const missing = this.missingField(draft);
    if (missing) {
      this.sendError.set(missing);
      return;
    }

    this.sending.set(true);
    this.sendError.set(null);

    this.api.pushSend(draft).subscribe((response) => {
      this.sending.set(false);

      if (!response.success || !response.data) {
        this.sendError.set(errorKey(response.errorCode));
        return;
      }

      this.sent.set(response.data.id);
      this.composing.set(false);

      // The point of sending from this screen: the outcome appears on it. The
      // worker takes a moment, so this reload is the first of two an admin
      // will want — the second is theirs to ask for.
      this.load();
    });
  }

  protected load(): void {
    this.loading.set(true);

    this.api.pushOverview(this.windowDays()).subscribe((response) => {
      this.overview.set(response.data ?? null);
      this.loading.set(false);
    });

    this.loadLog();
  }

  // ──────────────────────────── the delivery log ───────────────────────────

  protected loadLog(): void {
    this.loadingLog.set(true);

    const status = this.logStatus();

    this.api
      .dispatches({ pageSize: 50, ...(status === null ? {} : { status }) })
      .subscribe((response) => {
        this.log.set(response.data?.data ?? []);
        this.logTotal.set(response.data?.totalRows ?? 0);
        this.loadingLog.set(false);
      });
  }

  protected filterLog(status: PushStatus | null): void {
    this.logStatus.set(status);
    this.loadLog();
  }

  protected retry(row: PushDispatchOutput): void {
    this.act(this.api.retryDispatch(row.id));
  }

  protected cancelDispatch(row: PushDispatchOutput): void {
    this.act(this.api.cancelDispatch(row.id));
  }

  /** The two row actions differ only in which call they make. */
  private act(call: Observable<BaseResponse<PushDispatchOutput>>): void {
    this.logError.set(null);

    call.subscribe((response) => {
      if (!response.success) {
        this.logError.set(errorKey(response.errorCode));
        return;
      }

      // Both the log and the counters above it moved, so both are re-read.
      this.load();
    });
  }

  /**
   * Runs the pipeline now instead of waiting for the workers.
   *
   * This is what makes the "overdue" warning actionable: the screen could
   * already say the sender looked stopped, and an admin could do nothing about
   * it. Safe to press twice — it is the same dispatcher the workers call, and
   * every pass is idempotent.
   */
  protected runNow(): void {
    if (this.running()) return;

    this.running.set(true);
    this.logError.set(null);
    this.ranSummary.set(null);

    this.api.runPushNow().subscribe((response) => {
      this.running.set(false);

      if (!response.success || !response.data) {
        this.logError.set(errorKey(response.errorCode));
        return;
      }

      this.ranSummary.set(response.data);
      this.load();
    });
  }

  protected statusKey(status: PushStatus): string {
    switch (status) {
      case PushStatus.Sent:
        return 'push.sent';
      case PushStatus.Failed:
        return 'push.failed';
      case PushStatus.Skipped:
        return 'push.skipped';
      case PushStatus.TokenExpired:
        return 'push.tokenExpired';
      default:
        return 'push.pending';
    }
  }

  protected setWindow(days: number): void {
    if (days === this.windowDays()) return;

    this.windowDays.set(days);
    this.load();
  }

  protected entries(map: Record<string, number>): { key: string; value: number }[] {
    return Object.entries(map)
      .map(([key, value]) => ({ key, value }))
      .sort((a, b) => b.value - a.value);
  }
}
