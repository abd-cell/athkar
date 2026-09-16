import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationsEditorComponent } from '../../components/translations-editor.component';
import { ApiService, errorKey } from '../../core/api/api.service';
import {
  AdminCategoryOutput,
  AdminDhikrOutput,
  DhikrInput,
  HadithGrade,
  LanguageOutput,
} from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * The corpus.
 *
 * The one thing this screen is designed around: **the source is not an optional
 * extra field at the bottom of the form.** It sits beside the text, the grid
 * marks every row that lacks one, and the publish button is disabled until it
 * is filled in — because the project's whole claim rests on it.
 */
@Component({
  selector: 'app-adhkar',
  imports: [FormsModule, TranslatePipe, TranslationsEditorComponent],
  templateUrl: './adhkar.component.html',
})
export class AdhkarComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<AdminDhikrOutput[]>([]);
  protected readonly categories = signal<AdminCategoryOutput[]>([]);
  protected readonly languages = signal<LanguageOutput[]>([]);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  protected readonly categoryFilter = signal<number | null>(null);
  protected search = '';

  protected readonly editing = signal<DhikrInput | null>(null);
  protected readonly editingId = signal<number | null>(null);

  protected readonly grades = [
    { value: HadithGrade.Sahih, key: 'adhkar.grade.sahih' },
    { value: HadithGrade.Hasan, key: 'adhkar.grade.hasan' },
    { value: HadithGrade.SahihLighayrihi, key: 'adhkar.grade.sahihLighayrihi' },
    { value: HadithGrade.HasanLighayrihi, key: 'adhkar.grade.hasanLighayrihi' },
    { value: HadithGrade.QuranVerse, key: 'adhkar.grade.quranVerse' },
    { value: HadithGrade.MuttafaqAlayh, key: 'adhkar.grade.muttafaqAlayh' },
  ];

  /** True while the open form still lacks what publishing requires. */
  protected readonly missingSource = computed(() => {
    const form = this.editing();
    if (!form) return false;

    return !form.sourceBook?.trim() || !form.sourceReference?.trim();
  });

  constructor() {
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));
    this.api
      .categories({ pageSize: 100 })
      .subscribe((response) => this.categories.set(response.data?.data ?? []));

    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api
      .adhkar(this.categoryFilter(), { pageSize: 100, search: this.search })
      .subscribe((response) => {
        this.rows.set(response.data?.data ?? []);
        this.loading.set(false);
      });
  }

  protected filterBy(categoryId: number | null): void {
    this.categoryFilter.set(categoryId);
    this.load();
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      categoryId: this.categoryFilter() ?? this.categories()[0]?.id ?? 0,
      sortOrder: this.rows().length,
      arabicText: '',
      repeatCount: 1,
      sourceBook: null,
      sourceReference: null,
      grade: null,
      gradedBy: null,
      isPublished: false,
      translations: [],
    });
  }

  protected edit(row: AdminDhikrOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      categoryId: row.categoryId,
      sortOrder: row.sortOrder,
      arabicText: row.arabicText,
      repeatCount: row.repeatCount,
      sourceBook: row.sourceBook,
      sourceReference: row.sourceReference,
      grade: row.grade,
      gradedBy: row.gradedBy,
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
    const request = id === null ? this.api.createDhikr(input) : this.api.updateDhikr(id, input);

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

  protected togglePublished(row: AdminDhikrOutput): void {
    this.api.setDhikrPublished(row.id, !row.isPublished).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected remove(row: AdminDhikrOutput): void {
    if (!confirm(row.arabicText.slice(0, 40))) return;

    this.api.deleteDhikr(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected categoryName(id: number): string {
    const category = this.categories().find((c) => c.id === id);
    return category?.translations.find((t) => t.languageCode === 'ar')?.title ?? category?.key ?? '';
  }

  protected gradeKey(grade: HadithGrade | null): string {
    return this.grades.find((option) => option.value === grade)?.key ?? 'common.none';
  }
}
