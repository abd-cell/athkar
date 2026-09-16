import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { GlobalService } from '../services/global.service';
import { SKIP_AUTH_HANDLING } from './auth-context';

/** Attaches the bearer token. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(SKIP_AUTH_HANDLING)) return next(request);

  const token = inject(GlobalService).accessToken();
  if (!token) return next(request);

  return next(
    request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }),
  );
};
