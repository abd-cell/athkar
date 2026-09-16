import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiService } from '../../core/api/api.service';
import {
  AdminWidgetCatalogOutput,
  BaseResponse,
  LanguageOutput,
  WidgetFamily,
  WidgetSettingsOutput,
  WidgetSurface,
} from '../../core/api/models';
import { IS_SERVER } from '../../core/services/platform';
import { WidgetCatalogComponent } from './widget-catalog.component';

/**
 * The gallery manager.
 *
 * Two rules are worth a test here because both fail *quietly*, on a screen an
 * admin looks at once a month:
 *
 * - **Reorder sends the whole gallery, not the pair that swapped.** The server
 *   refuses a partial list, so a bug here does not corrupt anything — it simply
 *   makes the arrows stop working, with a generic error nobody connects to the
 *   cause.
 * - **A key cannot be edited after the entry exists.** Every phone that placed
 *   the widget holds the old key; renaming it here would make the entry stop
 *   matching anything the app can draw, and the gallery would go quiet rather
 *   than break.
 */
describe('WidgetCatalogComponent', () => {
  function entry(
    id: number,
    key: string,
    surface = WidgetSurface.Home,
  ): AdminWidgetCatalogOutput {
    return {
      id,
      key,
      surface,
      family: WidgetFamily.Prayer,
      designCount: 2,
      defaultDesign: 0,
      isExclusive: false,
      isNew: false,
      isEnabled: true,
      sortOrder: id,
      translations: [{ languageCode: 'ar', title: key, subtitle: null }],
    };
  }

  const rows = [
    entry(1, 'today_prayers'),
    entry(2, 'prayer_times'),
    entry(3, 'lock_day', WidgetSurface.Lock),
  ];

  const reorder = vi.fn((_ids: number[]) => of({ success: true } as BaseResponse));

  const api = {
    languages: () =>
      of({ success: true, data: [] as LanguageOutput[] } as BaseResponse<LanguageOutput[]>),
    widgetCatalog: () =>
      of({ success: true, data: rows } as BaseResponse<AdminWidgetCatalogOutput[]>),
    // The screen reads the default so a row can be badged with it. The call
    // needs Admin and this screen is open to Editors, so the component has to
    // cope with a refusal — hence a plain failed envelope here rather than a
    // throw.
    widgetSettings: () =>
      of({ success: true, data: { defaultWidgetKey: 'today_prayers' } } as unknown as
        BaseResponse<WidgetSettingsOutput>),
    reorderWidgetCatalog: (ids: number[]) => reorder(ids) as Observable<BaseResponse>,
  };

  beforeEach(() => {
    reorder.mockClear();

    // As the render server: the translate pipe reaches GlobalService, which
    // reads localStorage on construction, and nothing here depends on it.
    TestBed.configureTestingModule({
      providers: [
        { provide: IS_SERVER, useValue: true },
        { provide: ApiService, useValue: api },
      ],
    });
  });

  function render() {
    const fixture = TestBed.createComponent(WidgetCatalogComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('marks the row a new reader would get, where the admin is looking at the list', () => {
    const component = render().componentInstance as unknown as {
      defaultKey: () => string | null;
    };

    expect(component.defaultKey()).toBe('today_prayers');
  });

  it('separates the two surfaces, because a lock widget is not a home widget', () => {
    const component = render().componentInstance as unknown as {
      home: () => AdminWidgetCatalogOutput[];
      lock: () => AdminWidgetCatalogOutput[];
    };

    expect(component.home().map((row) => row.key)).toEqual(['today_prayers', 'prayer_times']);
    expect(component.lock().map((row) => row.key)).toEqual(['lock_day']);
  });

  it('sends the whole gallery when one row moves, not just the pair that swapped', () => {
    const fixture = render();
    const component = fixture.componentInstance as unknown as {
      move: (row: AdminWidgetCatalogOutput, delta: number) => void;
    };

    component.move(rows[1], -1);

    // The server applies every id or none; a partial list would write positions
    // derived from a list the admin was never looking at.
    expect(reorder).toHaveBeenCalledWith([2, 1, 3]);
  });

  it('refuses to move a row past the end of its own surface', () => {
    const component = render().componentInstance as unknown as {
      move: (row: AdminWidgetCatalogOutput, delta: number) => void;
    };

    component.move(rows[0], -1);
    component.move(rows[2], 1);

    expect(reorder).not.toHaveBeenCalled();
  });

  it('locks the key on an existing entry and leaves it writable on a new one', async () => {
    const fixture = render();
    const component = fixture.componentInstance as unknown as {
      edit: (row: AdminWidgetCatalogOutput) => void;
      create: () => void;
    };

    component.edit(rows[0]);
    fixture.detectChanges();
    // NgModel applies a disabled binding through setDisabledState, which lands
    // after the pass that created the control rather than during it.
    await fixture.whenStable();

    const locked = fixture.nativeElement.querySelector(
      'input[name="key"]',
    ) as HTMLInputElement;
    expect(locked.disabled).toBe(true);

    component.create();
    fixture.detectChanges();
    await fixture.whenStable();

    const fresh = fixture.nativeElement.querySelector('input[name="key"]') as HTMLInputElement;
    expect(fresh.disabled).toBe(false);
  });

  it('rejects a default design that sits outside the designs offered', () => {
    // Validated on the press rather than in a `computed`: two-way binding
    // mutates the form object in place, so a signal holding it never notifies
    // and a computed over it would be evaluated once and never again.
    const fixture = render();
    const component = fixture.componentInstance as unknown as {
      create: () => void;
      save: () => void;
      editing: () => { key: string; designCount: number; defaultDesign: number;
        translations: { languageCode: string; title: string }[] } | null;
      error: () => string | null;
    };

    component.create();

    const form = component.editing()!;
    form.key = 'prayer_times';
    form.translations = [{ languageCode: 'ar', title: 'مواقيت الصلاة' }];
    form.designCount = 2;
    form.defaultDesign = 5;

    component.save();

    expect(component.error()).toBe('widget.catalog.defaultOutOfRange');
  });
});
