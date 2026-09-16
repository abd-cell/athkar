import { Injectable, computed, inject, signal } from '@angular/core';

import { AuthOutput, Roles, StaffOutput } from '../api/models';
import { IS_SERVER } from './platform';

/**
 * The signed-in session, and the chosen language.
 *
 * Signals, backed by `localStorage`. Every read of storage is guarded by
 * [isPlatformBrowser]: this runs under SSR too, where `localStorage` does not
 * exist and touching it throws during render.
 */
@Injectable({ providedIn: 'root' })
export class GlobalService {
  /**
   * Whether storage is reachable, and whether a session can be seen at all.
   *
   * From [IS_SERVER] rather than from the platform id — see that token for why
   * the platform id could not be trusted here.
   */
  readonly isBrowser = !inject(IS_SERVER);

  private static readonly ACCESS = 'athkar.access';
  private static readonly REFRESH = 'athkar.refresh';
  private static readonly USER = 'athkar.user';
  private static readonly LANGUAGE = 'athkar.language';

  readonly accessToken = signal<string | null>(this.read(GlobalService.ACCESS));
  readonly refreshToken = signal<string | null>(this.read(GlobalService.REFRESH));
  readonly user = signal<StaffOutput | null>(this.readUser());
  readonly languageCode = signal<string>(this.read(GlobalService.LANGUAGE) ?? 'ar');

  readonly isSignedIn = computed(() => this.accessToken() !== null);
  readonly isRtl = computed(() => this.languageCode() === 'ar');

  /** Roles are a ladder, so permission questions are comparisons. */
  readonly isAtLeastAdmin = computed(() =>
    (this.user()?.roles ?? []).some((role) => role >= Roles.Admin),
  );

  readonly isSuperAdmin = computed(() =>
    (this.user()?.roles ?? []).includes(Roles.SuperAdmin),
  );

  signIn(auth: AuthOutput): void {
    this.accessToken.set(auth.accessToken);
    this.refreshToken.set(auth.refreshToken);
    this.user.set(auth.user);

    this.write(GlobalService.ACCESS, auth.accessToken);
    this.write(GlobalService.REFRESH, auth.refreshToken);
    this.write(GlobalService.USER, JSON.stringify(auth.user));
  }

  signOut(): void {
    this.accessToken.set(null);
    this.refreshToken.set(null);
    this.user.set(null);

    this.remove(GlobalService.ACCESS);
    this.remove(GlobalService.REFRESH);
    this.remove(GlobalService.USER);
  }

  setLanguage(code: string): void {
    this.languageCode.set(code);
    this.write(GlobalService.LANGUAGE, code);
  }

  private read(key: string): string | null {
    return this.isBrowser ? localStorage.getItem(key) : null;
  }

  private readUser(): StaffOutput | null {
    const raw = this.read(GlobalService.USER);
    if (!raw) return null;

    try {
      return JSON.parse(raw) as StaffOutput;
    } catch {
      return null;
    }
  }

  private write(key: string, value: string): void {
    if (this.isBrowser) localStorage.setItem(key, value);
  }

  private remove(key: string): void {
    if (this.isBrowser) localStorage.removeItem(key);
  }
}
