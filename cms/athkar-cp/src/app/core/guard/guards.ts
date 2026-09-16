import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AppLanguages } from '../services/translation.service';
import { GlobalService } from '../services/global.service';

/**
 * Every route is language-first (`/:languageCode/...`). This rejects a code the
 * CMS has no copy for, so a typo in a URL lands on a real page rather than a
 * half-translated one.
 */
export const languageGuard: CanActivateFn = (route) => {
  const code = route.paramMap.get('languageCode');
  const router = inject(Router);

  if (code && AppLanguages.includes(code)) {
    inject(GlobalService).setLanguage(code);
    return true;
  }

  return router.createUrlTree(['/ar']);
};

/**
 * All three auth guards return `true` off the browser and let the browser
 * decide.
 *
 * `localStorage` is unreadable during SSR, so a guard that answered honestly
 * there would bounce every deep link to the login page before hydration — the
 * user then watches that flash past on the way to the page they asked for, and
 * a direct link to a deep page becomes a 302 to the login form.
 *
 * The test is `!isBrowser` rather than `isPlatformServer`, because only one of
 * those two is a value worth depending on.
 */
export const appAuthGuard: CanActivateFn = () => {
  const global = inject(GlobalService);
  if (!global.isBrowser) return true;
  if (global.isSignedIn()) return true;

  return inject(Router).createUrlTree(['/', global.languageCode(), 'login']);
};

/** Keeps a signed-in user off the login page. */
export const loggedInGuard: CanActivateFn = () => {
  const global = inject(GlobalService);
  if (!global.isBrowser) return true;
  if (!global.isSignedIn()) return true;

  return inject(Router).createUrlTree(['/', global.languageCode(), 'dashboard']);
};

/** Staff management, and nothing else, is super-admin only. */
export const superAdminGuard: CanActivateFn = () => {
  const global = inject(GlobalService);
  if (!global.isBrowser) return true;
  if (global.isSuperAdmin()) return true;

  return inject(Router).createUrlTree(['/', global.languageCode(), 'dashboard']);
};
