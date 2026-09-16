import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationsEditorComponent } from '../../components/translations-editor.component';
import { ApiService, errorKey } from '../../core/api/api.service';
import {
  Audience,
  BroadcastInput,
  BroadcastOutput,
  BroadcastStatus,
  LanguageOutput,
} from '../../core/api/models';
import { AppDatePipe } from '../../core/pipes/app-date.pipe';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * One-off messages to the installed base.
 *
 * Two things worth noticing in the result columns: **Send schedules rather than
 * sends** — the worker does the fan-out, so pressing it returns at once even for
 * a million devices — and **Skipped is not Failed**. A message where most
 * readers simply have notifications off is a successful message, and the counts
 * say so rather than reporting a disaster.
 */
@Component({
  selector: 'app-broadcasts',
  imports: [FormsModule, AppDatePipe, TranslatePipe, TranslationsEditorComponent],
  templateUrl: './broadcasts.component.html',
})
export class BroadcastsComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<BroadcastOutput[]>([]);
  protected readonly languages = signal<LanguageOutput[]>([]);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<BroadcastInput | null>(null);
  protected readonly editingId = signal<number | null>(null);

  protected readonly Status = BroadcastStatus;

  protected readonly audiences = [
    { value: Audience.All, key: 'reminders.audience.all' },
    { value: Audience.Language, key: 'reminders.audience.language' },
    { value: Audience.Device, key: 'reminders.audience.device' },
  ];

  private static readonly STATUS_KEYS: Record<BroadcastStatus, string> = {
    [BroadcastStatus.Draft]: 'broadcasts.status.draft',
    [BroadcastStatus.Scheduled]: 'broadcasts.status.scheduled',
    [BroadcastStatus.Sending]: 'broadcasts.status.sending',
    [BroadcastStatus.Sent]: 'broadcasts.status.sent',
    [BroadcastStatus.Failed]: 'broadcasts.status.failed',
    [BroadcastStatus.Cancelled]: 'broadcasts.status.cancelled',
  };

  constructor() {
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.broadcasts({ pageSize: 50 }).subscribe((response) => {
      this.rows.set(response.data?.data ?? []);
      this.loading.set(false);
    });
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      audience: Audience.All,
      targetLanguageCode: null,
      targetPlatform: null,
      targetDeviceKey: null,
      scheduledAtUtc: null,
      route: null,
      translations: [],
    });
  }

  protected edit(row: BroadcastOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      audience: row.audience,
      targetLanguageCode: row.targetLanguageCode,
      targetPlatform: row.targetPlatform,
      targetDeviceKey: row.targetDeviceKey,
      // `datetime-local` wants a naive local string, so the trailing zone is
      // trimmed; the server re-reads it as UTC.
      scheduledAtUtc: row.scheduledAtUtc ? row.scheduledAtUtc.slice(0, 16) : null,
      route: row.route,
      translations: row.translations.map((t) => ({ ...t })),
    });
  }

  protected save(): void {
    const input = this.editing();
    if (!input || this.saving()) return;

    input.translations = input.translations.filter((t) => t.title.trim().length > 0);

    this.saving.set(true);
    this.error.set(null);

    const id = this.editingId();
    const request =
      id === null ? this.api.createBroadcast(input) : this.api.updateBroadcast(id, input);

    request.subscribe((response) => {
      this.saving.set(false);

      if (!response.success) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      this.editing.set(null);
      this.load();
    });
  }

  protected send(row: BroadcastOutput): void {
    this.api.sendBroadcast(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected cancelSending(row: BroadcastOutput): void {
    this.api.cancelBroadcast(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected remove(row: BroadcastOutput): void {
    if (!confirm(this.titleOf(row))) return;

    this.api.deleteBroadcast(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected titleOf(row: BroadcastOutput): string {
    return row.translations[0]?.title ?? '';
  }

  protected statusKey(status: BroadcastStatus): string {
    return BroadcastsComponent.STATUS_KEYS[status];
  }

  protected isEditable(row: BroadcastOutput): boolean {
    return (
      row.status === BroadcastStatus.Draft ||
      row.status === BroadcastStatus.Scheduled ||
      row.status === BroadcastStatus.Cancelled
    );
  }
}
