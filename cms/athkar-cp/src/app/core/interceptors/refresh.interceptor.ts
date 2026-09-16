import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { SessionRefreshService } from '../services/session-refresh.service';
import { SKIP_AUTH_HANDLING } from './auth-context';

/**
 * Renews an expired access token and replays the call once.
 *
 * **Interceptor order matters on the way back.** They are registered as
 * `[auth, error, refresh]`, and responses unwind in reverse — so this one sees
 * a 401 *before* `errorInterceptor` does and can renew before the session is
 * ended. Swapping the two is how a working refresh token still logs everybody
 * out.
 */
export const refreshInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(SKIP_AUTH_HANDLING)) return next(request);

  const sessions = inject(SessionRefreshService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return sessions.refresh().pipe(
        switchMap((token) =>
          token
            ? next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
            : throwError(() => error),
        ),
      );
    }),
  );
};
