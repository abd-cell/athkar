import { Component, computed, input } from '@angular/core';

import { WidgetSurface } from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { PREVIEW_PRAYERS, shapeFor } from './widget-shapes';

/**
 * What the entry looks like on a phone.
 *
 * The console's one blind spot until now: an admin could name a widget, order
 * it, badge it and make it the default for every new reader without ever seeing
 * the thing. This draws it — in a phone frame, over a wallpaper, at the size
 * the launcher grants it (four columns by two rows).
 *
 * It is honest about three things, because each is a way an admin could be
 * misled by a prettier preview:
 *
 * 1. **The values are a sample.** The server does not know where any reader
 *    lives and never will — no coordinates ever reach it — so the times are a
 *    plausible Makkah day, labelled as such. What is faithful here is the
 *    *arrangement*, not the numbers.
 * 2. **The phone draws one layout for every entry.** The several designs a
 *    catalogue row may offer are drawn by the app, inside the app's own
 *    gallery; the home screen gets this arrangement whichever design the reader
 *    picked. An admin who thought otherwise would lower a design count expecting
 *    the home screen to change.
 * 3. **A lock-screen entry is drawn nowhere yet.** This build ships no
 *    lock-screen widget on either platform, so those entries are browsable in
 *    the app and placeable on no screen. Saying so is more use than a mockup of
 *    something that does not exist.
 */
@Component({
  selector: 'app-widget-phone-preview',
  imports: [TranslatePipe],
  template: `
    <div class="phone" [attr.aria-label]="'widget.preview.title' | translate">
      <div class="phone__screen">
        <div class="phone__clock">{{ 'widget.preview.clock' | translate }}</div>

        @if (shape(); as s) {
          <div class="widget" dir="rtl">
            @if (s.date) {
              <div class="widget__date">{{ 'widget.preview.hijri' | translate }}</div>
            }
            @if (s.headline) {
              <div class="widget__headline">{{ s.headline | translate }}</div>
            }
            @if (s.title) {
              <div class="widget__title" [class.widget__title--narrated]="s.narrated">
                {{ s.title | translate }}
              </div>
            }

            @if (s.prayerRow) {
              <div class="widget__prayers">
                @for (prayer of prayers; track prayer.name; let first = $first) {
                  <div class="widget__prayer" [class.widget__prayer--next]="first">
                    <span class="widget__prayer-name">{{ prayer.name | translate }}</span>
                    <span class="widget__prayer-time">{{ prayer.at | translate }}</span>
                  </div>
                }
              </div>
            }

            @if (s.subtitle) {
              <div class="widget__subtitle">{{ s.subtitle | translate }}</div>
            }
          </div>
        } @else {
          <div class="widget widget--absent" dir="rtl">
            {{ 'widget.preview.lockNotDrawn' | translate }}
          </div>
        }

        <div class="phone__dock">
          <span></span><span></span><span></span><span></span>
        </div>
      </div>
    </div>

    <p class="phone__note">
      {{ (shape() ? 'widget.preview.note' : 'widget.preview.lockNote') | translate }}
    </p>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .phone {
        width: 232px;
        margin-inline: auto;
        padding: 10px;
        border-radius: 26px;
        background: #1b1814;
        border: 1px solid var(--border);
      }

      /* A wallpaper, not a flat panel: the whole question an admin has is
         whether the widget reads over one. */
      .phone__screen {
        border-radius: 18px;
        padding: 12px 10px 10px;
        min-height: 300px;
        display: flex;
        flex-direction: column;
        background: linear-gradient(160deg, #33506b 0%, #1d2733 55%, #12161c 100%);
      }

      .phone__clock {
        color: rgba(255, 255, 255, 0.92);
        font-size: 11px;
        text-align: center;
        margin-bottom: 12px;
      }

      /* Four columns by two rows, which is what the provider asks the launcher
         for — so the preview cannot flatter the design with more room than it
         will get. */
      .widget {
        background: var(--surface);
        border-radius: 16px;
        padding: 12px;
        min-height: 104px;
        display: flex;
        flex-direction: column;
        justify-content: center;
        gap: 2px;
        text-align: start;
      }

      .widget--absent {
        align-items: center;
        text-align: center;
        color: var(--muted);
        font-size: 11px;
        line-height: 1.7;
      }

      .widget__date {
        font-size: 9.5px;
        color: var(--muted);
      }

      .widget__headline {
        font-size: 9.5px;
        font-weight: 600;
        color: var(--brand);
      }

      .widget__title {
        font-size: 14px;
        color: var(--ink);
        margin-top: 3px;
        overflow: hidden;
        display: -webkit-box;
        -webkit-line-clamp: 2;
        -webkit-box-orient: vertical;
      }

      /* Amiri carries anything narrated, here as in the app. */
      .widget__title--narrated {
        font-family: var(--serif);
        font-size: 15px;
        line-height: 1.7;
      }

      .widget__prayers {
        display: flex;
        margin-top: 8px;
      }

      .widget__prayer {
        flex: 1;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2px;
      }

      .widget__prayer-name {
        font-size: 8.5px;
        color: var(--muted);
      }

      .widget__prayer-time {
        font-size: 11px;
        color: var(--ink);
      }

      /* The next prayer is picked out by the app, never by the launcher — it
         has no clock it can trust against the reader's adjustments. */
      .widget__prayer--next .widget__prayer-name,
      .widget__prayer--next .widget__prayer-time {
        color: var(--brand);
        font-weight: 600;
      }

      .widget__subtitle {
        font-size: 9.5px;
        color: var(--muted);
        margin-top: 6px;
      }

      .phone__dock {
        margin-top: auto;
        padding-top: 16px;
        display: flex;
        justify-content: center;
        gap: 10px;
      }

      .phone__dock span {
        width: 22px;
        height: 22px;
        border-radius: 7px;
        background: rgba(255, 255, 255, 0.16);
      }

      .phone__note {
        margin: 10px 0 0;
        font-size: 11px;
        line-height: 1.8;
        color: var(--faint);
        text-align: center;
      }
    `,
  ],
})
export class WidgetPhonePreviewComponent {
  readonly key = input.required<string>();
  readonly surface = input.required<WidgetSurface>();

  protected readonly prayers = PREVIEW_PRAYERS;

  protected readonly shape = computed(() => shapeFor(this.key(), this.surface()));
}
