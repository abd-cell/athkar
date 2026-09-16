import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { LanguageOutput, TranslationInput } from '../core/api/models';
import { IS_SERVER } from '../core/services/platform';
import { TranslationsEditorComponent } from './translations-editor.component';

/**
 * The editor every "new" form in the CMS is built on.
 *
 * It is tested from an **empty** translations array because that is the state
 * no other check reaches: the build is clean, every list screen renders, and
 * editing existing content works — only creating something new starts here.
 * When lazily adding the first language was done inside a `computed`, Angular
 * refused the write (NG0600), the `@if` guarding the fields never opened, and
 * the composer rendered with no text inputs at all. Nothing failed loudly.
 */
@Component({
  imports: [TranslationsEditorComponent],
  template: `
    <app-translations-editor
      [languages]="languages"
      [(translations)]="translations"
      titleLabel="broadcasts.headline"
      bodyLabel="feedback.message"
    />
  `,
})
class HostComponent {
  readonly languages: LanguageOutput[] = [
    { code: 'ar', nativeName: 'العربية', englishName: 'Arabic', isRtl: true } as LanguageOutput,
    { code: 'en', nativeName: 'English', englishName: 'English', isRtl: false } as LanguageOutput,
  ];

  readonly translations = signal<TranslationInput[]>([]);
}

describe('TranslationsEditorComponent', () => {
  beforeEach(() => {
    // The editor pulls in TranslatePipe, and through it GlobalService, which
    // reads `localStorage` on construction. Nothing here depends on stored
    // state, so it is simplest to run these as the render server does.
    TestBed.configureTestingModule({
      providers: [{ provide: IS_SERVER, useValue: true }],
    });
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders writable fields when it starts with nothing', () => {
    const fixture = render();
    const element: HTMLElement = fixture.nativeElement;

    expect(element.querySelector('input[type="text"]')).not.toBeNull();
    expect(element.querySelector('textarea')).not.toBeNull();
  });

  it('seeds an entry for the default language so the first keystroke lands somewhere', () => {
    const fixture = render();

    expect(fixture.componentInstance.translations()).toEqual([
      { languageCode: 'ar', title: '', body: '' },
    ]);
  });

  it('adds an entry only for a language whose tab is opened', () => {
    const fixture = render();
    const tabs = fixture.nativeElement.querySelectorAll(
      'button.chip',
    ) as NodeListOf<HTMLButtonElement>;

    // Untouched languages stay out of the payload, which is what keeps a save
    // from writing empty translations for every language in the table.
    expect(fixture.componentInstance.translations().map((t) => t.languageCode)).toEqual(['ar']);

    tabs[1].click();
    fixture.detectChanges();

    expect(fixture.componentInstance.translations().map((t) => t.languageCode)).toEqual([
      'ar',
      'en',
    ]);
  });

  it('marks a language as filled once its title is written', () => {
    const fixture = render();

    fixture.componentInstance.translations.update((list) =>
      list.map((t) => ({ ...t, title: 'عنوان' })),
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button.chip .dot')).not.toBeNull();
  });
});
