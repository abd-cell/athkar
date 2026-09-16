import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationsEditorComponent } from '../../components/translations-editor.component';
import { ApiService, errorKey } from '../../core/api/api.service';
import { AdminFaqOutput, FaqCategory, FaqInput, LanguageOutput } from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

@Component({
  selector: 'app-faq',
  imports: [FormsModule, TranslatePipe, TranslationsEditorComponent],
  templateUrl: './faq.component.html',
})
export class FaqComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<AdminFaqOutput[]>([]);
  protected readonly languages = signal<LanguageOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<FaqInput | null>(null);
  protected readonly editingId = signal<number | null>(null);

  protected readonly categories = [
    { value: FaqCategory.General, key: 'faq.category.general' },
    { value: FaqCategory.Adhkar, key: 'faq.category.adhkar' },
    { value: FaqCategory.PrayerTimes, key: 'faq.category.prayerTimes' },
    { value: FaqCategory.Notifications, key: 'faq.category.notifications' },
    { value: FaqCategory.Qibla, key: 'faq.category.qibla' },
    { value: FaqCategory.Quran, key: 'faq.category.quran' },
    { value: FaqCategory.Privacy, key: 'faq.category.privacy' },
  ];

  constructor() {
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.faq({ pageSize: 100 }).subscribe((response) => {
      this.rows.set(response.data?.data ?? []);
      this.loading.set(false);
    });
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      category: FaqCategory.General,
      sortOrder: this.rows().length,
      isPublished: true,
      translations: [],
    });
  }

  protected edit(row: AdminFaqOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      category: row.category,
      sortOrder: row.sortOrder,
      isPublished: row.isPublished,
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
    const request = id === null ? this.api.createFaq(input) : this.api.updateFaq(id, input);

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

  protected remove(row: AdminFaqOutput): void {
    if (!confirm(this.questionOf(row))) return;

    this.api.deleteFaq(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected questionOf(row: AdminFaqOutput): string {
    return row.translations.find((t) => t.languageCode === 'ar')?.title ?? '';
  }

  protected categoryKey(category: FaqCategory): string {
    return this.categories.find((option) => option.value === category)?.key ?? '';
  }
}
