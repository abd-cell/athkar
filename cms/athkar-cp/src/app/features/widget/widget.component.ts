import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService, errorKey } from '../../core/api/api.service';
import {
  AdminCategoryOutput,
  AdminWidgetCatalogOutput,
  WidgetKind,
  WidgetSettingsInput,
  WidgetSurface,
  WidgetTheme,
} from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * Full admin control over the home-screen widget.
 *
 * The widget is the one surface a reader sees **without opening the app**, so
 * it is also the one whose content nobody can correct in the moment. That is
 * why these settings exist at all: an admin who withdraws a disputed dhikr
 * needs it gone from the home screen too, and the master switch plus the pinned
 * chapter are how that reaches a phone.
 *
 * What this screen does *not* set is the text — the widget draws from the
 * catalogue already on the device so it keeps working offline. These are the
 * rules for choosing, not the content.
 */
@Component({
  selector: 'app-widget',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './widget.component.html',
})
export class WidgetComponent {
  private readonly api = inject(ApiService);

  protected readonly form = signal<WidgetSettingsInput | null>(null);
  protected readonly categories = signal<AdminCategoryOutput[]>([]);

  /**
   * The gallery, for the default-widget selector.
   *
   * Only the enabled home-screen entries: a lock-screen widget cannot be a
   * home-screen default, and offering a hidden one would let an admin choose a
   * default the server then refuses.
   */
  protected readonly offerable = signal<AdminWidgetCatalogOutput[]>([]);
  protected readonly version = signal(0);

  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly Kind = WidgetKind;
  protected readonly Theme = WidgetTheme;

  protected readonly themes = [
    { value: WidgetTheme.System, key: 'widget.theme.system' },
    { value: WidgetTheme.Light, key: 'widget.theme.light' },
    { value: WidgetTheme.Dark, key: 'widget.theme.dark' },
    { value: WidgetTheme.Transparent, key: 'widget.theme.transparent' },
  ];

  constructor() {
    this.api
      .categories({ pageSize: 100 })
      .subscribe((response) => this.categories.set(response.data?.data ?? []));

    this.api.widgetCatalog().subscribe((response) =>
      this.offerable.set(
        (response.data ?? []).filter(
          (item) => item.isEnabled && item.surface === WidgetSurface.Home,
        ),
      ),
    );

    this.api.widgetSettings().subscribe((response) => {
      const data = response.data;
      if (!data) return;

      this.version.set(data.version);

      const { version: _version, ...input } = data;
      this.form.set(input);
    });
  }

  protected save(): void {
    const input = this.form();
    if (!input || this.saving()) return;

    this.saving.set(true);
    this.saved.set(false);
    this.error.set(null);

    this.api.updateWidgetSettings(input).subscribe((response) => {
      this.saving.set(false);

      if (!response.success || !response.data) {
        // 650 is the one refusal this screen can provoke: widgets on, but every
        // kind withdrawn, which would leave the reader an empty picker.
        this.error.set(
          response.errorCode === 650 ? 'widget.noKindAllowed' : errorKey(response.errorCode),
        );
        return;
      }

      this.version.set(response.data.version);
      this.saved.set(true);
    });
  }

  protected categoryName(category: AdminCategoryOutput): string {
    return category.translations.find((t) => t.languageCode === 'ar')?.title ?? category.key;
  }

  protected widgetName(item: AdminWidgetCatalogOutput): string {
    return item.translations.find((t) => t.languageCode === 'ar')?.title ?? item.key;
  }
}
