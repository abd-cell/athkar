import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService, errorKey } from '../../core/api/api.service';
import { LanguageInput, LanguageOutput } from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * Languages, and the interface-copy overlay that hangs off each.
 *
 * The overlay editor is a two-column list of key and value rather than a form:
 * it is a translator's working surface, and what they need is to see every key
 * at once and fill in the blanks.
 */
@Component({
  selector: 'app-languages',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './languages.component.html',
})
export class LanguagesComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<LanguageOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<LanguageInput | null>(null);
  protected readonly editingId = signal<number | null>(null);

  /** The overlay being edited, as a list so it can be rendered in order. */
  protected readonly strings = signal<{ key: string; value: string }[] | null>(null);
  protected readonly stringsFor = signal<string | null>(null);
  protected newKey = '';

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.languages().subscribe((response) => {
      this.rows.set(response.data ?? []);
      this.loading.set(false);
    });
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      code: '',
      nativeName: '',
      englishName: '',
      isRtl: false,
      isEnabled: true,
      sortOrder: this.rows().length,
    });
  }

  protected edit(row: LanguageOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      code: row.code,
      nativeName: row.nativeName,
      englishName: row.englishName,
      isRtl: row.isRtl,
      isEnabled: row.isEnabled,
      sortOrder: row.sortOrder,
    });
  }

  protected save(): void {
    const input = this.editing();
    if (!input || this.saving()) return;

    this.saving.set(true);
    this.error.set(null);

    const id = this.editingId();
    const request =
      id === null ? this.api.createLanguage(input) : this.api.updateLanguage(id, input);

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

  protected setDefault(row: LanguageOutput): void {
    this.api.setDefaultLanguage(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected remove(row: LanguageOutput): void {
    if (!confirm(row.code)) return;

    this.api.deleteLanguage(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected openStrings(row: LanguageOutput): void {
    this.stringsFor.set(row.code);
    this.strings.set(null);

    this.api.uiStrings(row.code).subscribe((response) => {
      const map = response.data?.strings ?? {};
      this.strings.set(
        Object.entries(map)
          .sort(([a], [b]) => a.localeCompare(b))
          .map(([key, value]) => ({ key, value })),
      );
    });
  }

  protected addString(): void {
    const key = this.newKey.trim();
    if (!key) return;

    this.strings.update((list) => [...(list ?? []), { key, value: '' }]);
    this.newKey = '';
  }

  protected removeString(key: string): void {
    this.strings.update((list) => (list ?? []).filter((entry) => entry.key !== key));
  }

  protected saveStrings(): void {
    const code = this.stringsFor();
    const list = this.strings();
    if (!code || !list) return;

    this.saving.set(true);

    // Sent as a complete set. A key absent from this payload is removed, which
    // is how a client is told to fall back to its built-in copy rather than
    // keeping an override nobody wanted.
    const map = Object.fromEntries(list.map((entry) => [entry.key, entry.value]));

    this.api.replaceUiStrings(code, map).subscribe((response) => {
      this.saving.set(false);

      if (!response.success) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      this.stringsFor.set(null);
      this.load();
    });
  }

  protected closeStrings(): void {
    this.stringsFor.set(null);
    this.strings.set(null);
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }
}
