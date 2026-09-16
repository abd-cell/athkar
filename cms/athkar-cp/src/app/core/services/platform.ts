import { InjectionToken } from '@angular/core';

/**
 * True only when the app is rendering on the server.
 *
 * An explicit token rather than `isPlatformServer(PLATFORM_ID)`. The platform
 * id is supposed to answer this, and under SSR here it does not: the browser
 * branch is taken on the server, which silently turned every deep link into a
 * 302 to the login form. This value is set in exactly two places — `false` by
 * its own factory, `true` in `app.config.server.ts` — so it cannot disagree
 * with reality.
 *
 * Anything that touches `localStorage`, or that would redirect based on a
 * session it cannot see, asks this first.
 */
export const IS_SERVER = new InjectionToken<boolean>('IS_SERVER', {
  providedIn: 'root',
  factory: () => false,
});
