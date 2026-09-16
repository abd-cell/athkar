import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { WidgetSurface } from '../../core/api/models';
import { IS_SERVER } from '../../core/services/platform';
import { WidgetPhonePreviewComponent } from './widget-phone-preview.component';
import { shapeFor } from './widget-shapes';

/**
 * The console's answer to "what will a reader actually see".
 *
 * The shape table is a hand-kept mirror of `WidgetBridge._build` in the app, so
 * the failure worth guarding is drift: a key the server seeds that this console
 * has no shape for would be previewed as a dhikr, quietly, and an admin would
 * make a decision on a picture of the wrong widget.
 */
@Component({
  imports: [WidgetPhonePreviewComponent],
  template: `<app-widget-phone-preview [key]="key()" [surface]="surface()" />`,
})
class HostComponent {
  readonly key = signal('prayer_times');
  readonly surface = signal(WidgetSurface.Home);
}

describe('WidgetPhonePreviewComponent', () => {
  beforeEach(() => {
    // As the render server: the translate pipe reaches GlobalService, which
    // reads localStorage on construction.
    TestBed.configureTestingModule({
      providers: [{ provide: IS_SERVER, useValue: true }],
    });
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('draws the five-column row only for the entries that are about the timetable', () => {
    const fixture = render();

    expect(fixture.nativeElement.querySelectorAll('.widget__prayer').length).toBe(5);

    fixture.componentInstance.key.set('assorted_adhkar');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.widget__prayers')).toBeNull();
  });

  it('sets narrated text in the serif face, as the phone does', () => {
    const fixture = render();

    fixture.componentInstance.key.set('quran_verse');
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelector('.widget__title--narrated'),
    ).not.toBeNull();
  });

  it('says a lock-screen entry is drawn nowhere rather than mocking one up', () => {
    // This build ships no lock-screen widget on either platform. A pretty
    // preview here would tell an admin the entry is placeable when it is not.
    const fixture = render();

    fixture.componentInstance.key.set('lock_next_prayer');
    fixture.componentInstance.surface.set(WidgetSurface.Lock);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.widget--absent')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.widget__prayers')).toBeNull();
  });

  it('follows the key as the admin changes it', () => {
    const fixture = render();
    const title = () =>
      (fixture.nativeElement.querySelector('.widget__title') as HTMLElement).textContent?.trim();

    const before = title();

    fixture.componentInstance.key.set('date_only');
    fixture.detectChanges();

    expect(title()).not.toBe(before);
  });
});

describe('shapeFor', () => {
  // The keys the server seeds. A new one added to the catalogue without a shape
  // here would silently be previewed as a dhikr.
  const seededHome = [
    'today_prayers',
    'prayer_times',
    'prayer_track',
    'date_only',
    'prayer_calendar',
    'assorted_adhkar',
    'quran_verse',
    'occasion_countdown',
    'night_thirds',
    'comprehensive',
    'custom_pinned',
  ];

  it('gives every seeded home entry a shape of its own', () => {
    const shapes = seededHome.map((key) => shapeFor(key, WidgetSurface.Home));

    for (const shape of shapes) {
      expect(shape).not.toBeNull();
    }

    // Not all identical: if they were, this table would have stopped tracking
    // the app and nobody would notice, because every preview would still draw.
    const distinct = new Set(shapes.map((shape) => JSON.stringify(shape)));
    expect(distinct.size).toBeGreaterThan(5);
  });

  it('gives a lock entry no shape at all', () => {
    expect(shapeFor('lock_next_prayer', WidgetSurface.Lock)).toBeNull();
  });

  it('falls an unknown key to the dhikr shape, exactly as the app does', () => {
    const unknown = shapeFor('widget_from_a_later_release', WidgetSurface.Home);

    expect(unknown).toEqual(shapeFor('assorted_adhkar', WidgetSurface.Home));
  });

  it('only the timetable entries carry the prayer row', () => {
    const withRow = seededHome.filter(
      (key) => shapeFor(key, WidgetSurface.Home)?.prayerRow,
    );

    // Mirrors `WidgetKeys.withPrayerRow` in the app.
    expect(withRow.sort()).toEqual(
      ['comprehensive', 'prayer_calendar', 'prayer_times', 'today_prayers'].sort(),
    );
  });
});
