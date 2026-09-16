import { Pipe, PipeTransform, inject } from '@angular/core';

import { GlobalService } from '../services/global.service';

/**
 * `{{ row.createdAt | appDate }}`.
 *
 * Angular's own `date` pipe formats against `LOCALE_ID`, which is fixed at
 * `en-US` unless the app registers locale data at bootstrap — so an Arabic desk
 * was reading «9/12/26, 10:34 AM» in a right-to-left table. `LOCALE_ID` is
 * resolved once per injector and the language here is a signal that changes
 * without a reload, so this reads the signal instead and formats through
 * `Intl`, which every target browser already carries.
 *
 * Impure for the same reason as `TranslatePipe`: the language can change under
 * a rendered page.
 */
@Pipe({ name: 'appDate', pure: false })
export class AppDatePipe implements PipeTransform {
  private readonly global = inject(GlobalService);

  transform(
    value: string | number | Date | null | undefined,
    mode: 'short' | 'date' = 'short',
  ): string {
    if (value === null || value === undefined || value === '') return '—';

    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '—';

    // Arabic-Indic digits are the app's convention; `ar` alone would give a
    // Hijri-flavoured order on some engines, so the calendar is pinned.
    const locale = this.global.languageCode() === 'en' ? 'en-GB' : 'ar-EG-u-ca-gregory';

    return new Intl.DateTimeFormat(locale, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      ...(mode === 'short' ? { hour: '2-digit', minute: '2-digit' } : {}),
    }).format(date);
  }
}
