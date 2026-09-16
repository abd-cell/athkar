import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService, errorKey } from '../../core/api/api.service';
import {
  QuranPackageEditInput,
  QuranPackageOutput,
  QuranScript,
  QuranSyncAction,
  QuranSyncOutput,
} from '../../core/api/models';
import { TranslationService } from '../../core/services/translation.service';
import { AppDatePipe } from '../../core/pipes/app-date.pipe';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * Uploading and publishing the mushaf.
 *
 * The whole text is one prepared SQLite file rather than a table of verses —
 * see `docs/BUSINESS_LOGIC.md` §7 for why, and for the schema the file is
 * expected to have. Uploading and publishing are separate steps so a large file
 * is transferred and verified before a million installs are told about it.
 */
@Component({
  selector: 'app-quran',
  imports: [FormsModule, AppDatePipe, TranslatePipe],
  templateUrl: './quran.component.html',
})
export class QuranComponent {
  private readonly api = inject(ApiService);
  private readonly translations = inject(TranslationService);

  /** The delete confirmation, read from i18n — `confirm()` cannot take a pipe. */
  private get confirmDeleteText(): string {
    return this.translations.translate('quran.confirmDelete');
  }

  protected readonly rows = signal<QuranPackageOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly scripts = [
    { value: QuranScript.Uthmani, key: 'quran.script.uthmani' },
    { value: QuranScript.IndoPak, key: 'quran.script.indoPak' },
    { value: QuranScript.Naskh, key: 'quran.script.naskh' },
  ];

  // ── The Qur'anic adhkar, read back from the canonical source ──
  //
  // Separate from the package upload above and deliberately two clicks: the
  // preview reads the source and reports, and only then is there a button that
  // rewrites anything. Nowhere else in this console does content change without
  // somebody typing it, so it does not happen here on one click either.
  protected readonly sync = signal<QuranSyncOutput | null>(null);
  protected readonly syncing = signal(false);
  protected readonly syncError = signal<string | null>(null);
  protected readonly syncAction = QuranSyncAction;

  // ── Correcting a package after it is uploaded ──
  //
  // Only what a package *says about itself*. The version, the bytes, the size
  // and the checksum are how an installed copy identifies itself, and an
  // install that already downloaded version 3 has no way to be told that
  // version 3 now means something else — so the server refuses to change them
  // and this form does not offer them.
  protected readonly editing = signal<QuranPackageOutput | null>(null);
  protected readonly saving = signal(false);

  protected edit: QuranPackageEditInput = {
    name: '',
    script: QuranScript.Uthmani,
    hasWaqfAnnotations: false,
    releaseNotes: '',
  };

  /**
   * The mushaf this upload is a version of.
   *
   * Typed rather than picked, because the first upload of a mushaf is the one
   * that names it — but the existing slugs are listed beside the field so the
   * common case is copying one, not inventing a second spelling of it.
   */
  protected edition = '';
  protected name = '';
  protected version = 1;
  protected script = QuranScript.Uthmani;
  protected hasWaqfAnnotations = false;
  protected releaseNotes = '';
  protected expectedSha256 = '';
  protected file: File | null = null;

  constructor() {
    this.load();
  }

  /** The editions already on the shelf, so an admin copies a slug rather than inventing one. */
  protected readonly editions = computed(() =>
    [...new Set(this.rows().map((row) => row.edition))].sort(),
  );

  protected load(): void {
    this.loading.set(true);

    this.api.quranPackages().subscribe((response) => {
      const rows = response.data ?? [];
      this.rows.set(rows);
      this.bumpVersion();
      this.loading.set(false);
    });
  }

  /**
   * The next free number **within the edition being uploaded**.
   *
   * Scoped, because versions are a sequence per mushaf: a second mushaf whose
   * first upload was numbered 8 — because Hafs had reached 7 — would look to
   * every reader like seven versions they had missed.
   */
  protected bumpVersion(): void {
    const slug = this.edition.trim().toLowerCase();
    const mine = this.rows().filter((row) => row.edition === slug);
    this.version = Math.max(0, ...mine.map((row) => row.version)) + 1;
  }

  protected useEdition(slug: string): void {
    this.edition = slug;
    // The name travels with the edition: re-typing it on every new version is
    // how one mushaf ends up under two names in the reader's list.
    this.name = this.rows().find((row) => row.edition === slug)?.name ?? this.name;
    this.bumpVersion();
  }

  protected onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file = input.files?.[0] ?? null;
  }

  protected upload(): void {
    if (!this.file || this.uploading()) return;

    const form = new FormData();
    form.append('file', this.file);
    form.append('Edition', this.edition.trim().toLowerCase());
    form.append('Name', this.name.trim());
    form.append('Version', String(this.version));
    form.append('Script', String(this.script));
    form.append('HasWaqfAnnotations', String(this.hasWaqfAnnotations));
    form.append('ReleaseNotes', this.releaseNotes);
    if (this.expectedSha256.trim()) {
      form.append('ExpectedSha256', this.expectedSha256.trim());
    }

    this.uploading.set(true);
    this.error.set(null);

    this.api.uploadQuranPackage(form).subscribe((response) => {
      this.uploading.set(false);

      if (!response.success) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      this.file = null;
      this.releaseNotes = '';
      this.expectedSha256 = '';
      this.load();
    });
  }

  protected publish(row: QuranPackageOutput): void {
    this.api.publishQuranPackage(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  /**
   * Withdraws the published mushaf, leaving nothing published.
   *
   * Without this, publishing was a one-way door: a package could only be
   * replaced by another, and a published one cannot be deleted — so a mushaf
   * found to have a defect had no way out at all.
   */
  protected unpublish(row: QuranPackageOutput): void {
    this.error.set(null);

    this.api.unpublishQuranPackage(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  /**
   * Points every device that names no edition at this mushaf.
   *
   * The reach is why it is its own button rather than a side-effect of
   * publishing: it redirects every fresh install, and the server writes an audit
   * line for it.
   */
  protected setDefault(row: QuranPackageOutput): void {
    this.error.set(null);

    this.api.setDefaultQuranPackage(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected startEdit(row: QuranPackageOutput): void {
    this.edit = {
      name: row.name,
      script: row.script,
      hasWaqfAnnotations: row.hasWaqfAnnotations,
      releaseNotes: row.releaseNotes ?? '',
    };

    this.error.set(null);
    this.editing.set(row);
  }

  protected cancelEdit(): void {
    this.editing.set(null);
  }

  protected saveEdit(): void {
    const row = this.editing();
    if (!row || this.saving()) return;

    this.saving.set(true);
    this.error.set(null);

    this.api.editQuranPackage(row.id, this.edit).subscribe((response) => {
      this.saving.set(false);

      if (!response.success) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      this.editing.set(null);
      this.load();
    });
  }

  /**
   * Downloads what is actually stored, published or not.
   *
   * Fetched rather than opened in a tab: the endpoint needs the bearer token,
   * and a new tab would arrive without one. The object URL is revoked straight
   * after the click — a 60MB mushaf held by a forgotten URL is 60MB of memory
   * for as long as the console stays open.
   */
  protected download(row: QuranPackageOutput): void {
    this.error.set(null);

    this.api.downloadQuranPackage(row.id).subscribe((blob) => {
      if (!blob) {
        this.error.set('error.generic');
        return;
      }

      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');

      link.href = url;
      link.download = row.fileName;
      link.click();

      URL.revokeObjectURL(url);
    });
  }

  protected remove(row: QuranPackageOutput): void {
    // Named, and with the consequence stated. `confirm(row.fileName)` asked the
    // question by showing a filename and nothing else, which is not a question.
    const question = `${row.fileName}\n\n${this.confirmDeleteText}`;
    if (!confirm(question)) return;

    this.api.deleteQuranPackage(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected previewSync(): void {
    if (this.syncing()) return;

    this.syncing.set(true);
    this.syncError.set(null);
    this.sync.set(null);

    this.api.previewQuranAthkarSync().subscribe((response) => {
      this.syncing.set(false);

      if (!response.success) {
        this.syncError.set(errorKey(response.errorCode));
        return;
      }

      this.sync.set(response.data ?? null);
    });
  }

  protected applySync(): void {
    const pending = this.sync();
    if (!pending || this.syncing()) return;

    this.syncing.set(true);
    this.syncError.set(null);

    this.api.applyQuranAthkarSync().subscribe((response) => {
      this.syncing.set(false);

      if (!response.success) {
        this.syncError.set(errorKey(response.errorCode));
        return;
      }

      this.sync.set(response.data ?? null);
    });
  }

  /** Anything to apply. A report of nothing but skips is not something to act on. */
  protected hasPendingSync(): boolean {
    const pending = this.sync();
    return !!pending && !pending.applied && pending.added + pending.changed > 0;
  }

  protected megabytes(bytes: number): string {
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected scriptKey(script: QuranScript): string {
    return this.scripts.find((option) => option.value === script)?.key ?? '';
  }
}
