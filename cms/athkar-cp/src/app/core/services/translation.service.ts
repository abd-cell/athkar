import { Injectable, computed, inject } from '@angular/core';

import { ar } from '../../i18n/ar';
import { en } from '../../i18n/en';
import { GlobalService } from './global.service';

/** The languages the CMS itself is written in. Not the app's language list. */
export const AppLanguages = ['ar', 'en'];

/**
 * CMS copy. Flat `Record<string, string>` maps, read through the `translate`
 * pipe.
 *
 * Every key must exist in **both** files — a missing one falls back to Arabic
 * and then to the key itself, which is visible but not useful.
 */
@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly global = inject(GlobalService);

  private readonly strings = computed<Record<string, string>>(() =>
    this.global.languageCode() === 'en' ? en : ar,
  );

  translate(key: string, args: Record<string, string | number> = {}): string {
    let text = this.strings()[key] ?? ar[key] ?? key;

    for (const [name, value] of Object.entries(args)) {
      text = text.replaceAll(`{${name}}`, String(value));
    }

    return text;
  }
}
