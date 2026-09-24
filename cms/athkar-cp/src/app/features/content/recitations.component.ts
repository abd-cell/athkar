import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationsEditorComponent } from '../../components/translations-editor.component';
import { ApiService, errorKey } from '../../core/api/api.service';
import {
  AdminRecitationOutput,
  AdminReciterOutput,
  LanguageOutput,
  RecitationSyncOutput,
  RecitationSyncRow,
  RecitationSyncStatus,
  ReciterInput,
} from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

type Filter = 'all' | 'published' | 'drafts';

/**
 * The reciters the app offers under «الاستماع».
 *
 * The catalogue comes from the publisher in one sync; this screen is where an
 * editor decides which of two hundred reciters, and which of their recordings,
 * a reader actually meets. Everything a sync brings in arrives as a draft, and
 * the screen says so before the button is pressed as well as after — the same
 * contract as the takhrij sync.
 *
 * The publisher's credit is shown above everything else on purpose: the audio
 * is theirs, it plays from their servers, and that is both what the app tells
 * the reader and what an editor should keep in mind when publishing.
 */
@Component({
  selector: 'app-recitations',
  imports: [FormsModule, TranslatePipe, TranslationsEditorComponent],
  templateUrl: './recitations.component.html',
})
export class RecitationsComponent {
  private readonly api = inject(ApiService);

  protected readonly Status = RecitationSyncStatus;

  protected readonly rows = signal<AdminReciterOutput[]>([]);
  protected readonly total = signal(0);
  protected readonly languages = signal<LanguageOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly filter = signal<Filter>('published');
  protected search = '';
  protected readonly page = signal(1);
  protected readonly pageSize = 50;

  /** The reciter open in the dialog. Held whole so the recordings table can refresh in place. */
  protected readonly editing = signal<AdminReciterOutput | null>(null);
  protected form: ReciterInput | null = null;
  protected readonly saving = signal(false);
  protected readonly dialogError = signal<string | null>(null);

  // ── sync ──
  protected readonly syncRunning = signal(false);
  protected readonly syncError = signal<string | null>(null);
  protected readonly report = signal<RecitationSyncOutput | null>(null);
  protected readonly chosen = signal<ReadonlySet<number>>(new Set());
  protected readonly syncFilter = signal<'pending' | 'all'>('pending');

  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));

  constructor() {
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    const filter = this.filter();

    this.api
      .reciters({
        pageNumber: this.page(),
        pageSize: this.pageSize,
        search: this.search.trim() || undefined,
        ...(filter === 'all' ? {} : { isPublished: filter === 'published' }),
      })
      .subscribe((response) => {
        this.rows.set(response.data?.data ?? []);
        this.total.set(response.data?.totalRows ?? 0);
        this.loading.set(false);
      });
  }

  protected setFilter(filter: Filter): void {
    this.filter.set(filter);
    this.page.set(1);
    this.load();
  }

  protected goTo(page: number): void {
    this.page.set(Math.min(Math.max(1, page), this.pages()));
    this.load();
  }

  protected publishedCount(row: AdminReciterOutput): number {
    return row.recitations.filter((r) => r.isPublished).length;
  }

  protected timedCount(row: AdminReciterOutput): number {
    return row.recitations.filter((r) => r.hasTiming).length;
  }

  // ── editing ──

  protected edit(row: AdminReciterOutput): void {
    this.dialogError.set(null);
    this.editing.set(row);
    this.form = {
      imageUrl: row.imageUrl,
      isFeatured: row.isFeatured,
      sortOrder: row.sortOrder,
      isPublished: row.isPublished,
      translations: row.translations.map((t) => ({ ...t })),
    };
  }

  protected save(): void {
    const row = this.editing();
    const form = this.form;
    if (!row || !form || this.saving()) return;

    form.translations = form.translations.filter((t) => t.title.trim().length > 0);

    // Said here rather than left to the server's generic validation message:
    // the fix is one click away, in the table just below.
    if (form.isPublished && this.publishedCount(row) === 0) {
      this.dialogError.set('recitations.needsRecording');
      return;
    }

    this.saving.set(true);
    this.dialogError.set(null);

    this.api.updateReciter(row.id, form).subscribe((response) => {
      this.saving.set(false);
      if (!response.success) {
        this.dialogError.set(errorKey(response.errorCode));
        return;
      }
      this.editing.set(null);
      this.form = null;
      this.load();
    });
  }

  /**
   * Publishing a recording is saved at once rather than with the dialog: it is
   * the switch an editor flips while listening through a reciter's recordings,
   * and a toggle that waits for "save" is a toggle somebody forgets.
   */
  protected toggleRecording(recording: AdminRecitationOutput): void {
    const row = this.editing();
    if (!row) return;

    this.dialogError.set(null);

    this.api
      .updateRecitation(row.id, recording.id, {
        sortOrder: recording.sortOrder,
        isPublished: !recording.isPublished,
        translations: recording.translations.map((t) => ({ ...t })),
      })
      .subscribe((response) => {
        if (!response.success || !response.data) {
          this.dialogError.set(errorKey(response.errorCode));
          return;
        }
        this.editing.set(response.data);
        // Withdrawing the last recording withdraws the reciter server-side;
        // the dialog's own switch must follow, or saving would put it back.
        if (this.form) this.form.isPublished = response.data.isPublished;
        this.load();
      });
  }

  protected remove(row: AdminReciterOutput): void {
    if (!confirm(row.name)) return;

    this.api.deleteReciter(row.id).subscribe((response) => {
      if (response.success) {
        this.editing.set(null);
        this.load();
      } else {
        this.error.set(errorKey(response.errorCode));
      }
    });
  }

  protected cancel(): void {
    this.editing.set(null);
    this.form = null;
    this.dialogError.set(null);
  }

  // ── sync ──

  protected previewSync(): void {
    this.syncRunning.set(true);
    this.syncError.set(null);

    this.api.previewRecitationSync().subscribe((response) => {
      this.syncRunning.set(false);
      if (!response.success || !response.data) {
        this.syncError.set(errorKey(response.errorCode));
        this.report.set(null);
        return;
      }
      this.report.set(response.data);
      // Nothing is pre-chosen: two hundred reciters is a list to read, not to
      // accept wholesale by missing a checkbox.
      this.chosen.set(new Set());
    });
  }

  protected applySync(): void {
    const ids = [...this.chosen()];
    if (ids.length === 0 || this.syncRunning()) return;

    this.syncRunning.set(true);
    this.syncError.set(null);

    this.api.applyRecitationSync(ids).subscribe((response) => {
      this.syncRunning.set(false);
      if (!response.success || !response.data) {
        this.syncError.set(errorKey(response.errorCode));
        return;
      }
      this.report.set(response.data);
      this.chosen.set(new Set());
      // Drafts are where they land, so that is the list worth showing next.
      this.filter.set('drafts');
      this.page.set(1);
      this.load();
    });
  }

  protected isOffered(row: RecitationSyncRow): boolean {
    return row.status === RecitationSyncStatus.New || row.status === RecitationSyncStatus.Changed;
  }

  protected visibleRows(report: RecitationSyncOutput): RecitationSyncRow[] {
    return this.syncFilter() === 'all'
      ? report.rows
      : report.rows.filter((r) => r.status !== RecitationSyncStatus.Unchanged);
  }

  protected toggleChosen(row: RecitationSyncRow): void {
    const next = new Set(this.chosen());
    if (next.has(row.externalId)) next.delete(row.externalId);
    else next.add(row.externalId);
    this.chosen.set(next);
  }

  protected allChosen(report: RecitationSyncOutput): boolean {
    const offered = report.rows.filter((r) => this.isOffered(r));
    return offered.length > 0 && offered.every((r) => this.chosen().has(r.externalId));
  }

  protected toggleAll(report: RecitationSyncOutput): void {
    const offered = report.rows.filter((r) => this.isOffered(r)).map((r) => r.externalId);
    this.chosen.set(this.allChosen(report) ? new Set() : new Set(offered));
  }

  protected statusKey(status: RecitationSyncStatus): string {
    switch (status) {
      case RecitationSyncStatus.New:
        return 'recitations.sync.status.new';
      case RecitationSyncStatus.Changed:
        return 'recitations.sync.status.changed';
      case RecitationSyncStatus.Gone:
        return 'recitations.sync.status.gone';
      default:
        return 'recitations.sync.status.unchanged';
    }
  }

  protected changeKey(code: string): string {
    return `recitations.sync.change.${code}`;
  }
}
