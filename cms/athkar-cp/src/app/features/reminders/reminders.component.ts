import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationsEditorComponent } from '../../components/translations-editor.component';
import { ApiService, errorKey } from '../../core/api/api.service';
import {
  AdminCategoryOutput,
  Audience,
  DevicePlatform,
  LanguageOutput,
  PrayerAnchor,
  ReminderDelivery,
  ReminderInput,
  ReminderKind,
  ReminderOutput,
  WeekDays,
} from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { TranslationService } from '../../core/services/translation.service';

/**
 * Reminder campaigns.
 *
 * The screen exists to make one rule obvious rather than surprising: an
 * **anchored reminder is always scheduled on the device**, because the server
 * has no coordinates and cannot know when Maghrib is where the reader is. The
 * delivery control locks itself and explains why, instead of letting an admin
 * build a campaign that would silently never fire.
 */
@Component({
  selector: 'app-reminders',
  imports: [FormsModule, TranslatePipe, TranslationsEditorComponent],
  templateUrl: './reminders.component.html',
})
export class RemindersComponent {
  private readonly api = inject(ApiService);
  private readonly translations = inject(TranslationService);

  protected readonly rows = signal<ReminderOutput[]>([]);
  protected readonly categories = signal<AdminCategoryOutput[]>([]);
  protected readonly languages = signal<LanguageOutput[]>([]);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<ReminderInput | null>(null);
  protected readonly editingId = signal<number | null>(null);

  protected readonly Kind = ReminderKind;
  protected readonly Delivery = ReminderDelivery;

  protected readonly anchors = [
    { value: PrayerAnchor.Fajr, key: 'anchor.fajr' },
    { value: PrayerAnchor.Sunrise, key: 'anchor.sunrise' },
    { value: PrayerAnchor.Dhuhr, key: 'anchor.dhuhr' },
    { value: PrayerAnchor.Asr, key: 'anchor.asr' },
    { value: PrayerAnchor.Maghrib, key: 'anchor.maghrib' },
    { value: PrayerAnchor.Isha, key: 'anchor.isha' },
    { value: PrayerAnchor.Bedtime, key: 'anchor.bedtime' },
    { value: PrayerAnchor.IslamicMidnight, key: 'anchor.islamicMidnight' },
    { value: PrayerAnchor.LastThirdOfNight, key: 'anchor.lastThirdOfNight' },
  ];

  protected readonly audiences = [
    { value: Audience.All, key: 'reminders.audience.all' },
    { value: Audience.Language, key: 'reminders.audience.language' },
    { value: Audience.Platform, key: 'reminders.audience.platform' },
  ];

  protected readonly platforms = [
    { value: DevicePlatform.Android, label: 'Android' },
    { value: DevicePlatform.Ios, label: 'iOS' },
    { value: DevicePlatform.Web, label: 'Web' },
  ];

  /** So the template can compare against the enum without importing it itself. */
  protected readonly Audience = Audience;

  /** The seven bits, in the order a week is read. */
  protected readonly weekdays = [
    { bit: WeekDays.Sunday, key: 'days.sunday' },
    { bit: WeekDays.Monday, key: 'days.monday' },
    { bit: WeekDays.Tuesday, key: 'days.tuesday' },
    { bit: WeekDays.Wednesday, key: 'days.wednesday' },
    { bit: WeekDays.Thursday, key: 'days.thursday' },
    { bit: WeekDays.Friday, key: 'days.friday' },
    { bit: WeekDays.Saturday, key: 'days.saturday' },
  ];

  /**
   * True when the kind forces device-local delivery.
   *
   * A plain method, not a `computed` — the Kind select two-way-binds
   * `form.kind` onto the object `editing()` already holds, mutating it in
   * place without notifying the signal. A `computed` here would keep
   * whatever value it had when the dialog opened: a new reminder (opens
   * PrayerAnchored) could never unlock Delivery after switching to Fixed
   * time, and an edited FixedTime reminder switched to PrayerAnchored would
   * leave Delivery wrongly enabled.
   */
  protected deliveryLocked(): boolean {
    return this.editing()?.kind === ReminderKind.PrayerAnchored;
  }

  constructor() {
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));
    this.api.allCategories().subscribe((rows) => this.categories.set(rows));

    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.reminders({ pageSize: 100 }).subscribe((response) => {
      this.rows.set(response.data?.data ?? []);
      this.loading.set(false);
    });
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      key: '',
      categoryId: null,
      kind: ReminderKind.PrayerAnchored,
      delivery: ReminderDelivery.DeviceLocal,
      localTime: null,
      anchor: PrayerAnchor.Sunrise,
      offsetMinutes: 30,
      days: WeekDays.All,
      audience: Audience.All,
      targetLanguageCode: null,
      targetPlatform: null,
      isEnabled: true,
      isUserAdjustable: true,
      translations: [],
    });
  }

  protected edit(row: ReminderOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      key: row.key,
      categoryId: row.categoryId,
      kind: row.kind,
      delivery: row.delivery,
      localTime: row.localTime,
      anchor: row.anchor,
      offsetMinutes: row.offsetMinutes,
      days: row.days,
      audience: row.audience,
      targetLanguageCode: row.targetLanguageCode,
      targetPlatform: row.targetPlatform,
      isEnabled: row.isEnabled,
      isUserAdjustable: row.isUserAdjustable,
      translations: row.translations.map((t) => ({ ...t })),
    });
  }

  /**
   * Keeps the form honest as the kind changes: an anchored campaign loses its
   * clock time and is forced device-local, a fixed-time one gains a default
   * time so it is never saved without one.
   */
  protected onKindChange(): void {
    const form = this.editing();
    if (!form) return;

    if (form.kind === ReminderKind.PrayerAnchored) {
      form.delivery = ReminderDelivery.DeviceLocal;
      form.localTime = null;
      if (form.anchor === PrayerAnchor.None) form.anchor = PrayerAnchor.Sunrise;
    } else {
      form.anchor = PrayerAnchor.None;
      form.offsetMinutes = 0;
      form.localTime ??= '21:30';
    }
  }

  protected toggleDay(bit: number): void {
    const form = this.editing();
    if (!form) return;

    form.days = (form.days & bit) !== 0 ? form.days & ~bit : form.days | bit;
  }

  protected hasDay(days: number, bit: number): boolean {
    return (days & bit) !== 0;
  }

  protected save(): void {
    const input = this.editing();
    if (!input || this.saving()) return;

    if (input.audience === Audience.Language && !input.targetLanguageCode) {
      this.error.set('push.needsLanguage');
      return;
    }
    if (input.audience === Audience.Platform && input.targetPlatform == null) {
      this.error.set('push.needsPlatform');
      return;
    }

    input.translations = input.translations.filter((t) => t.title.trim().length > 0);

    this.saving.set(true);
    this.error.set(null);

    const id = this.editingId();
    const request =
      id === null ? this.api.createReminder(input) : this.api.updateReminder(id, input);

    request.subscribe((response) => {
      this.saving.set(false);

      if (!response.success) {
        this.error.set(
          response.errorCode === 501 ? 'reminders.noDays' : errorKey(response.errorCode),
        );
        return;
      }

      this.editing.set(null);
      this.load();
    });
  }

  protected remove(row: ReminderOutput): void {
    if (!confirm(row.key)) return;

    this.api.deleteReminder(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected titleOf(row: ReminderOutput): string {
    return row.translations.find((t) => t.languageCode === 'ar')?.title ?? row.key;
  }

  protected scheduleOf(row: ReminderOutput): string {
    if (row.kind === ReminderKind.FixedTime) return row.localTime ?? '';

    // The key is what the select binds to; the table has to show the reader's
    // own word for it, or the column reads «anchor.asr +30».
    const key = this.anchors.find((option) => option.value === row.anchor)?.key;
    const anchor = key ? this.translations.translate(key) : '';
    const sign = row.offsetMinutes >= 0 ? '+' : '−';

    return `${anchor} ${sign}${Math.abs(row.offsetMinutes)} ${this.translations.translate('reminders.minutesShort')}`;
  }
}
