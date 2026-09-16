import { Component, computed, effect, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { LanguageOutput, TranslationInput } from '../core/api/models';
import { TranslatePipe } from '../core/pipes/translate.pipe';

/**
 * One tab per language, for anything translatable.
 *
 * Every translatable thing in this system has the same shape — a title, an
 * optional body, and two fields only a dhikr uses — so one editor serves
 * chapters, adhkar, reminders, broadcasts and help topics rather than five
 * near-identical forms.
 *
 * The tab strip marks which languages are filled in, which is the question an
 * editor actually has while working: not "what does this say in Turkish" but
 * "is the Turkish done".
 */
@Component({
  selector: 'app-translations-editor',
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="tabs">
      @for (language of languages(); track language.code) {
        <button
          type="button"
          class="chip"
          [class.chip--on]="active() === language.code"
          (click)="active.set(language.code)"
        >
          {{ language.nativeName }}
          @if (isFilled(language.code)) {
            <span class="dot"></span>
          }
        </button>
      }
    </div>

    @if (current(); as translation) {
      <div [attr.dir]="isRtl(translation.languageCode) ? 'rtl' : 'ltr'">
        <div class="field">
          <label>{{ titleLabel() | translate }}</label>
          @if (multilineTitle()) {
            <textarea rows="3" [(ngModel)]="translation.title" [name]="'t-' + translation.languageCode"></textarea>
          } @else {
            <input type="text" [(ngModel)]="translation.title" [name]="'t-' + translation.languageCode" />
          }
        </div>

        @if (bodyLabel(); as label) {
          <div class="field">
            <label>{{ label | translate }}</label>
            <textarea rows="4" [(ngModel)]="translation.body" [name]="'b-' + translation.languageCode"></textarea>
          </div>
        }

        @if (secondaryLabel(); as label) {
          <div class="field">
            <label>{{ label | translate }}</label>
            <input type="text" [(ngModel)]="translation.secondary" [name]="'s-' + translation.languageCode" />
          </div>
        }

        @if (virtueLabel(); as label) {
          <div class="field">
            <label>{{ label | translate }}</label>
            <textarea rows="3" [(ngModel)]="translation.virtue" [name]="'v-' + translation.languageCode"></textarea>
            @if (virtueHint(); as hint) {
              <span class="field__hint">{{ hint | translate }}</span>
            }
          </div>
        }
      </div>
    }
  `,
  styles: [
    `
      .tabs {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
        margin-bottom: 14px;
      }

      .dot {
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background: currentColor;
        opacity: 0.7;
      }
    `,
  ],
})
export class TranslationsEditorComponent {
  readonly languages = input.required<LanguageOutput[]>();

  /**
   * The working set, mutated in place.
   *
   * A `model` rather than an output event: the parent holds the array it is
   * about to POST, and round-tripping every keystroke through an event would
   * buy nothing but ceremony.
   */
  readonly translations = model.required<TranslationInput[]>();

  readonly titleLabel = input('categories.name');
  readonly bodyLabel = input<string | null>(null);
  readonly secondaryLabel = input<string | null>(null);
  readonly virtueLabel = input<string | null>(null);
  readonly virtueHint = input<string | null>(null);
  readonly multilineTitle = input(false);

  readonly active = model<string>('ar');

  constructor() {
    // Created lazily, so opening a tab is enough to start writing in it and an
    // untouched language never reaches the payload.
    //
    // This has to be an effect rather than part of [current]: a `computed` may
    // not write to a signal (NG0600), and when it throws, the `@if` around the
    // fields never opens — which is every "new" form in the CMS rendering with
    // no text inputs at all, since those start from an empty array.
    effect(() => {
      const code = this.active();

      if (!this.translations().some((t) => t.languageCode === code)) {
        this.translations.update((list) => [...list, { languageCode: code, title: '', body: '' }]);
      }
    });
  }

  protected readonly current = computed(() =>
    this.translations().find((t) => t.languageCode === this.active()),
  );

  protected isFilled(code: string): boolean {
    const translation = this.translations().find((t) => t.languageCode === code);
    return !!translation && translation.title.trim().length > 0;
  }

  protected isRtl(code: string): boolean {
    return this.languages().find((language) => language.code === code)?.isRtl ?? false;
  }
}
