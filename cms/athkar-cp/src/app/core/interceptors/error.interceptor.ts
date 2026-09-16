import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { GlobalService } from '../services/global.service';
import { IS_SERVER } from '../services/platform';
import { SKIP_AUTH_HANDLING } from './auth-context';

/**
 * The end of the line for an unrecoverable auth failure: by the time this sees
 * a 401, `refreshInterceptor` has already tried and failed to renew.
 *
 * **It does nothing during server rendering**, and that is load-bearing. The
 * server holds no token, so every screen's own fetches 401 there as a matter of
 * course; ending a session and navigating to the login page on that basis
 * turned every deep link into a 302 to the login form — the page the user asked
 * for never rendered at all. On the server the failure is simply left to the
 * component, which renders its empty state and fetches again after hydration.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(SKIP_AUTH_HANDLING)) return next(request);

  const global = inject(GlobalService);
  const router = inject(Router);
  const isServer = inject(IS_SERVER);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        !isServer &&
        error instanceof HttpErrorResponse &&
        (error.status === 401 || error.status === 403)
      ) {
        global.signOut();
        void router.navigate(['/', global.languageCode(), 'login']);
      }

      return throwError(() => error);
    }),
  );
};
