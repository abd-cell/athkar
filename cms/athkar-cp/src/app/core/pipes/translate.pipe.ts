import { Pipe, PipeTransform, inject } from '@angular/core';

import { TranslationService } from '../services/translation.service';

/**
 * `{{ 'content.title' | translate }}`.
 *
 * Impure, because the language is a signal that changes at runtime and a pure
 * pipe would keep showing the language the page was first rendered in.
 */
@Pipe({ name: 'translate', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly translations = inject(TranslationService);

  transform(key: string, args: Record<string, string | number> = {}): string {
    return this.translations.translate(key, args);
  }
}
