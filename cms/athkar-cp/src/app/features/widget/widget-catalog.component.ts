import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslationsEditorComponent } from '../../components/translations-editor.component';
import { ApiService, errorKey } from '../../core/api/api.service';
import {
  AdminWidgetCatalogOutput,
  LanguageOutput,
  WidgetCatalogInput,
  WidgetFamily,
  WidgetSurface,
} from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { WidgetPhonePreviewComponent } from './widget-phone-preview.component';

/**
 * The widget gallery — the list a reader browses before placing anything.
 *
 * The one thing to understand before editing here is what a row *is not*: it is
 * not a design. A widget is drawn by code compiled into the app, because a
 * widget gets a fraction of a second of CPU and no network, so nothing typed on
 * this screen becomes a new layout on a phone that already shipped. What a row
 * carries is everything around the drawing — whether the entry is offered, what
 * it is called in each language, where it sits, and which badges it wears.
 *
 * `key` is therefore a contract with the app's renderer registry, not a label,
 * and that is why it cannot be edited after the entry exists: every phone that
 * has placed the widget is holding the old key, and renaming it here would not
 * rename it there — it would simply make the entry stop matching anything the
 * app can draw. The server refuses it too; this screen just does not offer it.
 *
 * Ordering is by buttons rather than drag-and-drop on purpose. The list is
 * thirty rows in two groups, a drag across a scroll boundary is the hardest
 * gesture to get right, and a misplaced drop reorders the gallery for every
 * reader with nothing on screen to say it happened.
 */
@Component({
  selector: 'app-widget-catalog',
  imports: [FormsModule, TranslatePipe, TranslationsEditorComponent, WidgetPhonePreviewComponent],
  templateUrl: './widget-catalog.component.html',
})
export class WidgetCatalogComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<AdminWidgetCatalogOutput[]>([]);
  protected readonly languages = signal<LanguageOutput[]>([]);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly reordering = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<WidgetCatalogInput | null>(null);
  protected readonly editingId = signal<number | null>(null);

  /**
   * The row whose phone preview is open, if any.
   *
   * Its own state rather than a flag on the edit dialog: an admin scanning the
   * list wants to see what an entry looks like without opening it for editing,
   * and opening an editor to look at something is how an entry gets saved by
   * accident.
   */
  protected readonly previewing = signal<AdminWidgetCatalogOutput | null>(null);

  /**
   * Which entry is the default, so the row can say so.
   *
   * Read-only here — it is set on the widget settings screen. This screen shows
   * it because "which one does a new reader get" is the question an admin has
   * while looking at the list, and having to open another screen to answer it is
   * how a default ends up pointing somewhere nobody intended.
   *
   * The call needs Admin, and this screen is open to Editors; a refusal simply
   * leaves the badge off rather than failing the screen.
   */
  protected readonly defaultKey = signal<string | null>(null);

  protected readonly home = computed(() =>
    this.rows().filter((row) => row.surface === WidgetSurface.Home),
  );

  protected readonly lock = computed(() =>
    this.rows().filter((row) => row.surface === WidgetSurface.Lock),
  );

  /**
   * The two surfaces, each with its own rows.
   *
   * A computed rather than an array literal in the template: the template is
   * re-evaluated on every change detection pass, and a literal there would
   * rebuild both groups each time for no gain.
   */
  protected readonly groups = computed(() => [
    { surface: WidgetSurface.Home, key: 'widget.surface.home', rows: this.home() },
    { surface: WidgetSurface.Lock, key: 'widget.surface.lock', rows: this.lock() },
  ]);

  protected readonly surfaces = [
    { value: WidgetSurface.Home, key: 'widget.surface.home' },
    { value: WidgetSurface.Lock, key: 'widget.surface.lock' },
  ];

  protected readonly families = [
    { value: WidgetFamily.Prayer, key: 'widget.family.prayer' },
    { value: WidgetFamily.Date, key: 'widget.family.date' },
    { value: WidgetFamily.PrayerAndDate, key: 'widget.family.prayerAndDate' },
    { value: WidgetFamily.Dhikr, key: 'widget.family.dhikr' },
    { value: WidgetFamily.Quran, key: 'widget.family.quran' },
    { value: WidgetFamily.Countdown, key: 'widget.family.countdown' },
    { value: WidgetFamily.Moon, key: 'widget.family.moon' },
    { value: WidgetFamily.Tracker, key: 'widget.family.tracker' },
  ];

  constructor() {
    this.api.languages().subscribe((response) => this.languages.set(response.data ?? []));

    this.api
      .widgetSettings()
      .subscribe((response) => this.defaultKey.set(response.data?.defaultWidgetKey ?? null));

    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.widgetCatalog().subscribe((response) => {
      this.rows.set(response.data ?? []);
      this.loading.set(false);
    });
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      key: '',
      surface: WidgetSurface.Home,
      family: WidgetFamily.Prayer,
      designCount: 1,
      defaultDesign: 0,
      isExclusive: false,
      isNew: true,
      isEnabled: true,
      sortOrder: this.home().length,
      translations: [],
    });
  }

  protected edit(row: AdminWidgetCatalogOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      key: row.key,
      surface: row.surface,
      family: row.family,
      designCount: row.designCount,
      defaultDesign: row.defaultDesign,
      isExclusive: row.isExclusive,
      isNew: row.isNew,
      isEnabled: row.isEnabled,
      sortOrder: row.sortOrder,
      // The subtitle rides in `body`, which is what the shared translations
      // editor calls its second field.
      translations: row.translations.map((t) => ({
        languageCode: t.languageCode,
        title: t.title,
        body: t.subtitle ?? '',
      })),
    });
  }

  /**
   * Validated on the press, not in a `computed`.
   *
   * Two-way binding mutates the form object in place, so a signal holding it
   * never notifies and a `computed` over it is evaluated once and never again —
   * which would leave the save button disabled however much the admin typed.
   */
  protected save(): void {
    const input = this.editing();
    if (!input || this.saving()) return;

    input.translations = input.translations.filter((t) => t.title.trim().length > 0);

    if (this.editingId() === null && input.key.trim().length === 0) {
      this.error.set('widget.catalog.keyRequired');
      return;
    }

    if (input.translations.length === 0) {
      this.error.set('widget.catalog.nameRequired');
      return;
    }

    if (input.defaultDesign >= input.designCount) {
      this.error.set('widget.catalog.defaultOutOfRange');
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    const id = this.editingId();
    const request =
      id === null
        ? this.api.createWidgetCatalogItem(input)
        : this.api.updateWidgetCatalogItem(id, input);

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

  protected remove(row: AdminWidgetCatalogOutput): void {
    if (!confirm(this.nameOf(row))) return;

    this.api.deleteWidgetCatalogItem(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  /**
   * Moves one row within its own surface.
   *
   * The reorder call is all-or-nothing on the server, so the ids sent are the
   * whole gallery in its new order rather than the pair that swapped — a
   * partial list would write positions derived from something the admin was
   * never looking at.
   */
  protected move(row: AdminWidgetCatalogOutput, delta: number): void {
    if (this.reordering()) return;

    const group = row.surface === WidgetSurface.Home ? [...this.home()] : [...this.lock()];
    const index = group.findIndex((item) => item.id === row.id);
    const target = index + delta;

    if (index < 0 || target < 0 || target >= group.length) return;

    [group[index], group[target]] = [group[target], group[index]];

    const other = row.surface === WidgetSurface.Home ? this.lock() : this.home();
    const ordered =
      row.surface === WidgetSurface.Home ? [...group, ...other] : [...other, ...group];

    this.reordering.set(true);

    this.api.reorderWidgetCatalog(ordered.map((item) => item.id)).subscribe((response) => {
      this.reordering.set(false);

      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected preview(row: AdminWidgetCatalogOutput): void {
    this.previewing.set(row);
  }

  protected closePreview(): void {
    this.previewing.set(null);
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected nameOf(row: AdminWidgetCatalogOutput): string {
    return row.translations.find((t) => t.languageCode === 'ar')?.title ?? row.key;
  }

  protected familyKey(family: WidgetFamily): string {
    return this.families.find((option) => option.value === family)?.key ?? '';
  }
}
