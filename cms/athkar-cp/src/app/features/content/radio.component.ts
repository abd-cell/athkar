import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationsEditorComponent } from '../../components/translations-editor.component';
import { ApiService, errorKey } from '../../core/api/api.service';
import {
  AdminRadioStationOutput,
  LanguageOutput,
  RadioStationInput,
} from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * The audio stations the app offers under «إذاعات صوتية».
 *
 * The list is short by nature and the screen is deliberately plain: a station
 * is a name, a broadcaster and a URL. The one thing worth being careful about
 * is the URL, which is why the row shows it rather than hiding it behind an
 * edit — a stream that has gone dead is diagnosed by looking at it.
 */
@Component({
  selector: 'app-radio',
  imports: [FormsModule, TranslatePipe, TranslationsEditorComponent],
  templateUrl: './radio.component.html',
})
export class RadioComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<AdminRadioStationOutput[]>([]);
  protected readonly languages = signal<LanguageOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** The row being edited, or null when the dialog is closed. */
  protected readonly editing = signal<RadioStationInput | null>(null);
  protected readonly editingId = signal<number | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.radioStations({ pageNumber: 1, pageSize: 100 }).subscribe((response) => {
      this.rows.set(response.data?.data ?? []);
      this.loading.set(false);
    });
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      key: '',
      streamUrl: '',
      logoUrl: null,
      // At the end, so a new station does not jump ahead of the one readers
      // already know.
      sortOrder: this.rows().length,
      isPublished: false,
      translations: [],
    });
  }

  protected edit(row: AdminRadioStationOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      key: row.key,
      streamUrl: row.streamUrl,
      logoUrl: row.logoUrl,
      sortOrder: row.sortOrder,
      isPublished: row.isPublished,
      // Cloned, so cancelling an edit really does cancel it.
      translations: row.translations.map((t) => ({ ...t })),
    });
  }

  protected save(): void {
    const input = this.editing();
    if (!input || this.saving()) return;

    // Blank tabs are created as the editor is browsed; they are not content.
    input.translations = input.translations.filter((t) => t.title.trim().length > 0);

    this.saving.set(true);
    this.error.set(null);

    const id = this.editingId();
    const request = id === null
      ? this.api.createRadioStation(input)
      : this.api.updateRadioStation(id, input);

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

  protected remove(row: AdminRadioStationOutput): void {
    if (!confirm(row.key)) return;

    this.api.deleteRadioStation(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected nameOf(row: AdminRadioStationOutput): string {
    return row.translations.find((t) => t.languageCode === 'ar')?.title ?? row.key;
  }
}
