import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationsEditorComponent } from '../../components/translations-editor.component';
import { ApiService, errorKey } from '../../core/api/api.service';
import {
  AdhkarImportAction,
  AdhkarImportOutput,
  TakhrijSyncOutput,
  TakhrijSyncRow,
  TakhrijSyncStatus,
  AdminCategoryOutput,
  CategoryInput,
  CategoryRhythm,
  CategorySection,
  LanguageOutput,
  PrayerAnchor,
} from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

@Component({
  selector: 'app-categories',
  imports: [FormsModule, TranslatePipe, TranslationsEditorComponent],
  templateUrl: './categories.component.html',
})
export class CategoriesComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<AdminCategoryOutput[]>([]);
  protected readonly languages = signal<LanguageOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** The row being edited, or null when the dialog is closed. */
  protected readonly editing = signal<CategoryInput | null>(null);
  protected readonly editingId = signal<number | null>(null);
  protected readonly saving = signal(false);

  protected readonly rhythms = [
    { value: CategoryRhythm.None, key: 'categories.rhythm.none' },
    { value: CategoryRhythm.Daily, key: 'categories.rhythm.daily' },
    { value: CategoryRhythm.Monthly, key: 'categories.rhythm.monthly' },
  ];

  /// The three sections of the reader's index, plus the unfiled state. Offered
  /// in the order they are drawn, so the list reads like the screen it makes.
  protected readonly sections = [
    { value: CategorySection.Adhkar, key: 'categories.section.adhkar' },
    { value: CategorySection.Duas, key: 'categories.section.duas' },
    { value: CategorySection.Virtues, key: 'categories.section.virtues' },
    { value: CategorySection.None, key: 'categories.section.none' },
  ];

  protected readonly anchors = [
    { value: PrayerAnchor.None, key: 'anchor.none' },
    { value: PrayerAnchor.Fajr, key: 'anchor.fajr' },
    { value: PrayerAnchor.Sunrise, key: 'anchor.sunrise' },
    { value: PrayerAnchor.Dhuhr, key: 'anchor.dhuhr' },
    { value: PrayerAnchor.Asr, key: 'anchor.asr' },
    { value: PrayerAnchor.Maghrib, key: 'anchor.maghrib' },
    { value: PrayerAnchor.Isha, key: 'anchor.isha' },
    { value: PrayerAnchor.Bedtime, key: 'anchor.bedtime' },
  ];

  // ── importing حصن المسلم ──
  //
  // Two clicks, like every other bulk action here: the check reports, and only
  // a report with something in it offers the button that writes. The import can
  // create a hundred and more أبواب, which is not a thing to discover afterwards.
  protected readonly importReport = signal<AdhkarImportOutput | null>(null);
  protected readonly importing = signal(false);
  protected readonly importError = signal<string | null>(null);
  protected readonly importAction = AdhkarImportAction;

  // ── attributing those drafts from the book's own footnotes ──
  //
  // The same two clicks, and for a sharper reason: this writes the one field
  // publishing is gated on. What it does not do is publish — the report says so
  // too, because a button that filled a takhrij and pushed it to readers would
  // be this project's promise undone by a convenience.
  protected readonly takhrijReport = signal<TakhrijSyncOutput | null>(null);
  protected readonly takhrijRunning = signal(false);
  protected readonly takhrijError = signal<string | null>(null);
  protected readonly takhrijStatus = TakhrijSyncStatus;

  /**
   * The rows the editor has ticked. Nothing is ticked by default: the check
   * proposes an attribution for each dhikr and the editor decides, one line at a
   * time, which of them they accept — which is the whole point of doing this in
   * two steps rather than one.
   */
  protected readonly takhrijChosen = signal<ReadonlySet<number>>(new Set());

  constructor() {
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));
    this.load();
  }

  protected previewTakhrij(): void {
    if (this.takhrijRunning()) return;

    this.takhrijRunning.set(true);
    this.takhrijError.set(null);
    this.takhrijReport.set(null);

    this.api.previewTakhrijSync().subscribe((response) => {
      this.takhrijRunning.set(false);

      if (!response.success) {
        this.takhrijError.set(errorKey(response.errorCode));
        return;
      }

      this.takhrijChosen.set(new Set());
      this.takhrijReport.set(response.data ?? null);
    });
  }

  protected applyTakhrij(): void {
    const chosen = [...this.takhrijChosen()];
    if (chosen.length === 0 || this.takhrijRunning()) return;

    this.takhrijRunning.set(true);
    this.takhrijError.set(null);

    this.api.applyTakhrijSync(chosen).subscribe((response) => {
      this.takhrijRunning.set(false);

      if (!response.success) {
        this.takhrijError.set(errorKey(response.errorCode));
        return;
      }

      this.takhrijChosen.set(new Set());
      this.takhrijReport.set(response.data ?? null);
    });
  }

  /** Something to write. A report that matched nothing is not worth a click. */
  protected hasPendingTakhrij(): boolean {
    const report = this.takhrijReport();
    return !!report && !report.applied && report.matched > 0;
  }

  /** The rows an editor may take: the ones the footnotes actually attribute. */
  protected takhrijOffered(report: TakhrijSyncOutput): TakhrijSyncRow[] {
    return report.rows.filter(
      (row) =>
        row.status === TakhrijSyncStatus.Filled || row.status === TakhrijSyncStatus.BookOnly,
    );
  }

  protected isChosen(row: TakhrijSyncRow): boolean {
    return this.takhrijChosen().has(row.dhikrId);
  }

  protected toggleChosen(row: TakhrijSyncRow): void {
    const next = new Set(this.takhrijChosen());
    if (!next.delete(row.dhikrId)) next.add(row.dhikrId);
    this.takhrijChosen.set(next);
  }

  /** Tick everything on offer, or clear the lot. */
  protected toggleAllChosen(report: TakhrijSyncOutput): void {
    const offered = this.takhrijOffered(report);
    const all = offered.length > 0 && offered.every((row) => this.isChosen(row));
    this.takhrijChosen.set(all ? new Set() : new Set(offered.map((row) => row.dhikrId)));
  }

  protected allChosen(report: TakhrijSyncOutput): boolean {
    const offered = this.takhrijOffered(report);
    return offered.length > 0 && offered.every((row) => this.isChosen(row));
  }

  protected chosenCount(): number {
    return this.takhrijChosen().size;
  }

  /** The rows worth listing: what would be filled, and what nobody can fill. */
  protected takhrijRows(report: TakhrijSyncOutput): TakhrijSyncRow[] {
    return report.rows.filter((row) => row.status !== TakhrijSyncStatus.AlreadyAttributed);
  }

  protected previewImport(): void {
    if (this.importing()) return;

    this.importing.set(true);
    this.importError.set(null);
    this.importReport.set(null);

    this.api.previewAdhkarImport().subscribe((response) => {
      this.importing.set(false);

      if (!response.success) {
        this.importError.set(errorKey(response.errorCode));
        return;
      }

      this.importReport.set(response.data ?? null);
    });
  }

  protected applyImport(): void {
    if (!this.hasPendingImport() || this.importing()) return;

    this.importing.set(true);
    this.importError.set(null);

    this.api.applyAdhkarImport().subscribe((response) => {
      this.importing.set(false);

      if (!response.success) {
        this.importError.set(errorKey(response.errorCode));
        return;
      }

      this.importReport.set(response.data ?? null);
      // The chapter list is what this screen shows, and it just grew.
      this.load();
    });
  }

  /** Anything to write. A report of nothing but skips is not something to act on. */
  protected hasPendingImport(): boolean {
    const report = this.importReport();
    return !!report && !report.applied && report.chaptersAdded + report.adhkarAdded > 0;
  }

  /** The chapters worth listing — the unchanged ones are not reported at all. */
  protected importRows(): AdhkarImportOutput['chapters'] {
    return this.importReport()?.chapters ?? [];
  }

  /**
   * Every chapter, not the first hundred.
   *
   * `PageInput` caps a page at 100, and this screen has no paging control —
   * which was invisible while there were fifteen chapters and became a wall the
   * moment the حصن المسلم import brought in 129 more. A management list of this
   * size is cheap to render whole; what is not acceptable is an editor being
   * unable to reach a chapter at all.
   */
  protected load(page = 1, gathered: AdminCategoryOutput[] = []): void {
    if (page === 1) this.loading.set(true);

    this.api.categories({ pageNumber: page, pageSize: 100 }).subscribe((response) => {
      const batch = response.data?.data ?? [];
      const rows = [...gathered, ...batch];

      // A short page is the last one. Guarded on the batch rather than on a
      // total, because a total the server does not send reads as zero.
      if (batch.length === 100) {
        this.load(page + 1, rows);
        return;
      }

      this.rows.set(rows);
      this.loading.set(false);
    });
  }

  /// The section's own slug, for the table cell. Read from the option list
  /// rather than indexed by value: `CategorySection.None` is 0 and an array
  /// lookup would put the unfiled state first in a list ordered for the screen.
  protected sectionKey(section: CategorySection): string {
    const found = this.sections.find((option) => option.value === section);
    return (found?.key ?? 'categories.section.none').split('.').pop()!;
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      key: '',
      icon: null,
      // Put at the end rather than at zero, so a new chapter does not silently
      // jump to the top of a reader's list.
      sortOrder: this.rows().length,
      rhythm: CategoryRhythm.None,
      anchor: PrayerAnchor.None,
      section: CategorySection.Adhkar,
      isPublished: false,
      translations: [],
    });
  }

  protected edit(row: AdminCategoryOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      key: row.key,
      icon: row.icon,
      sortOrder: row.sortOrder,
      rhythm: row.rhythm,
      anchor: row.anchor,
      section: row.section,
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
      ? this.api.createCategory(input)
      : this.api.updateCategory(id, input);

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

  protected remove(row: AdminCategoryOutput): void {
    if (!confirm(row.key)) return;

    this.api.deleteCategory(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected nameOf(row: AdminCategoryOutput): string {
    return row.translations.find((t) => t.languageCode === 'ar')?.title ?? row.key;
  }
}
