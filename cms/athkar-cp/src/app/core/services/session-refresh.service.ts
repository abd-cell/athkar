import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, of, shareReplay, tap } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environment';
import { AuthOutput, BaseResponse } from '../api/models';
import { SKIP_AUTH_HANDLING } from '../interceptors/auth-context';
import { GlobalService } from './global.service';

/**
 * Trades a refresh token for a new pair.
 *
 * The in-flight observable is shared, because refresh tokens **rotate**: two
 * requests failing at once would otherwise send the same token twice, and the
 * second call would be rejected against a token the first had already retired.
 */
@Injectable({ providedIn: 'root' })
export class SessionRefreshService {
  private readonly http = inject(HttpClient);
  private readonly global = inject(GlobalService);

  private inFlight: Observable<string | null> | null = null;

  refresh(): Observable<string | null> {
    if (this.inFlight) return this.inFlight;

    const refreshToken = this.global.refreshToken();
    if (!refreshToken) return of(null);

    this.inFlight = this.http
      .post<BaseResponse<AuthOutput>>(
        `${environment.apiBaseUrl}auth/refresh`,
        { refreshToken },
        { context: new HttpContext().set(SKIP_AUTH_HANDLING, true) },
      )
      .pipe(
        map((response) => {
          if (!response.success || !response.data) return null;

          this.global.signIn(response.data);
          return response.data.accessToken;
        }),
        catchError(() => of(null)),
        tap(() => {
          this.inFlight = null;
        }),
        shareReplay(1),
      );

    return this.inFlight;
  }
}
