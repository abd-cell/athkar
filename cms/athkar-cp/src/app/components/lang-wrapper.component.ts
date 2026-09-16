import { Component, computed, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { GlobalService } from '../core/services/global.service';

/**
 * Sits above every language-scoped route and owns the document's direction.
 *
 * Setting `dir` here rather than on `<html>` in `index.html` means a language
 * switch flips the whole console without a reload — which matters because the
 * console is bilingual and its authors switch between the two constantly.
 */
@Component({
  selector: 'app-lang-wrapper',
  imports: [RouterOutlet],
  template: `
    <div [attr.dir]="direction()" [attr.lang]="language()" class="lang-root">
      <router-outlet />
    </div>
  `,
  styles: [
    `
      .lang-root {
        min-height: 100vh;
      }
    `,
  ],
})
export class LangWrapperComponent {
  private readonly global = inject(GlobalService);

  protected readonly language = this.global.languageCode;
  protected readonly direction = computed(() => (this.global.isRtl() ? 'rtl' : 'ltr'));
}
