import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService, errorKey } from '../../core/api/api.service';
import {
  DeviceAdminOutput,
  DeviceInboxOutput,
  DeviceReach,
  DevicePlatform,
  SessionOutput,
} from '../../core/api/models';
import { AppDatePipe } from '../../core/pipes/app-date.pipe';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * Everything signed in to this system, which is two unrelated things.
 *
 * **Installs** are anonymous copies of the app. They hold no account and
 * authenticate nothing — the key on the row is one the app minted for itself,
 * and `X-Device-Key` says which install is calling, never that it may act (see
 * `docs/BUSINESS_LOGIC.md` §2). What makes them worth a screen is the FCM
 * token: the push manager can say "seven installs hold no token", and until now
 * that sentence could not be turned into seven rows anybody could act on.
 *
 * **Staff sessions** are the real ones — a signed token plus a row that every
 * request re-checks, which is what makes signing out immediate. They are here
 * because a signed token is otherwise valid until it expires, so "an editor
 * left" and "somebody is signed in from a machine nobody recognises" were both
 * invisible.
 *
 * They share a screen and nothing else, so they are two tabs rather than one
 * merged list. Merging them would suggest an install is a kind of login.
 */
@Component({
  selector: 'app-sessions',
  imports: [FormsModule, AppDatePipe, TranslatePipe],
  templateUrl: './sessions.component.html',
})
export class SessionsComponent {
  private readonly api = inject(ApiService);

  protected readonly Reach = DeviceReach;
  protected readonly Platform = DevicePlatform;

  protected readonly tab = signal<'installs' | 'staff'>('installs');

  // ────────────────────────────── installs ───────────────────────────────

  protected readonly devices = signal<DeviceAdminOutput[]>([]);
  protected readonly deviceTotal = signal(0);
  protected readonly loadingDevices = signal(true);
  protected readonly deviceSearch = signal('');
  protected readonly reachFilter = signal<DeviceReach | null>(null);

  /**
   * Tokens an admin has asked to see, by install id.
   *
   * Held only for this page view. Each one cost an audited read on the server,
   * so the screen does not throw it away on a filter change — but it is never
   * written anywhere, and a refresh starts over.
   */
  protected readonly revealed = signal<Record<number, string>>({});
  protected readonly revealing = signal<number | null>(null);

  protected readonly reachFilters = [
    { value: null, key: 'sessions.reach.all' },
    { value: DeviceReach.Reachable, key: 'push.reachable' },
    { value: DeviceReach.Tokenless, key: 'push.tokenless' },
    { value: DeviceReach.Muted, key: 'push.muted' },
    { value: DeviceReach.Silent, key: 'push.stale' },
  ];

  // ─────────────────────────── one install's inbox ────────────────────────

  protected readonly inboxOf = signal<DeviceAdminOutput | null>(null);
  protected readonly inbox = signal<DeviceInboxOutput[]>([]);
  protected readonly loadingInbox = signal(false);

  // ─────────────────────────── staff sessions ─────────────────────────────

  protected readonly sessions = signal<SessionOutput[]>([]);
  protected readonly loadingSessions = signal(false);
  protected readonly includeExpired = signal(false);

  protected readonly error = signal<string | null>(null);
  protected readonly note = signal<string | null>(null);

  /** Live staff sessions, for the heading — expired ones are not somebody signed in. */
  protected readonly liveSessions = computed(
    () => this.sessions().filter((row) => !row.isExpired).length,
  );

  constructor() {
    this.loadDevices();
  }

  protected show(tab: 'installs' | 'staff'): void {
    this.tab.set(tab);
    this.error.set(null);
    this.note.set(null);

    if (tab === 'staff' && this.sessions().length === 0) this.loadSessions();
  }

  // ────────────────────────────── installs ───────────────────────────────

  protected loadDevices(): void {
    this.loadingDevices.set(true);

    const reach = this.reachFilter();

    // The four states are three columns on the server, so the filter is
    // expressed the way the server stores it rather than as a state name. The
    // mapping is the sender's ladder, in the sender's order — the same one
    // `DeviceAdminService.Reach` walks.
    this.api
      .devices({
        pageSize: 100,
        search: this.deviceSearch().trim() || undefined,
        ...(reach === DeviceReach.Tokenless ? { hasPushToken: false } : {}),
        ...(reach === DeviceReach.Muted ? { hasPushToken: true, notificationsEnabled: false } : {}),
        ...(reach === DeviceReach.Silent
          ? { hasPushToken: true, notificationsEnabled: true, isActive: false }
          : {}),
        ...(reach === DeviceReach.Reachable
          ? { hasPushToken: true, notificationsEnabled: true, isActive: true }
          : {}),
      })
      .subscribe((response) => {
        this.devices.set(response.data?.data ?? []);
        this.deviceTotal.set(response.data?.totalRows ?? 0);
        this.loadingDevices.set(false);
      });
  }

  protected filterReach(value: DeviceReach | null): void {
    this.reachFilter.set(value);
    this.loadDevices();
  }

  /**
   * Asks the server for one install's whole token.
   *
   * Deliberately one at a time and never in bulk: the token is a capability —
   * with the server's credentials, whoever holds it can raise a notification on
   * that phone — and the server writes every read to the audit trail. A column
   * of a hundred of them would be a hundred such reads for one glance.
   */
  protected reveal(row: DeviceAdminOutput): void {
    if (this.revealing() !== null || this.revealed()[row.id]) return;

    this.revealing.set(row.id);
    this.error.set(null);

    this.api.revealPushToken(row.id).subscribe((response) => {
      this.revealing.set(null);

      if (!response.success || !response.data) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      this.revealed.update((held) => ({ ...held, [row.id]: response.data! }));
    });
  }

  protected copy(text: string): void {
    // Best-effort: a console over plain HTTP on a LAN has no clipboard API, and
    // failing silently there is better than an error about a browser policy
    // nobody can act on. The token is on screen either way.
    navigator.clipboard?.writeText(text).then(
      () => this.note.set('sessions.copied'),
      () => undefined,
    );
  }

  protected openInbox(row: DeviceAdminOutput): void {
    this.inboxOf.set(row);
    this.inbox.set([]);
    this.loadingInbox.set(true);

    this.api.deviceInbox(row.id, { pageSize: 50 }).subscribe((response) => {
      this.inbox.set(response.data?.data ?? []);
      this.loadingInbox.set(false);
    });
  }

  protected closeInbox(): void {
    this.inboxOf.set(null);
  }

  // ─────────────────────────── staff sessions ─────────────────────────────

  protected loadSessions(): void {
    this.loadingSessions.set(true);

    this.api
      .sessions({ pageSize: 100, includeExpired: this.includeExpired() })
      .subscribe((response) => {
        this.sessions.set(response.data?.data ?? []);
        this.loadingSessions.set(false);
      });
  }

  protected toggleExpired(): void {
    this.includeExpired.update((on) => !on);
    this.loadSessions();
  }

  protected revoke(row: SessionOutput): void {
    this.error.set(null);
    this.note.set(null);

    this.api.revokeSession(row.id).subscribe((response) => {
      if (!response.success) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      // Ending your own session leaves this page holding a token the server no
      // longer knows. Said plainly rather than left to the next request, which
      // would simply bounce the admin to the login form with no explanation.
      this.note.set(row.isCurrent ? 'sessions.revokedSelf' : 'sessions.revoked');
      this.loadSessions();
    });
  }

  protected revokeAll(row: SessionOutput): void {
    this.error.set(null);
    this.note.set(null);

    this.api.revokeAllSessionsFor(row.userId).subscribe((response) => {
      if (!response.success) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      this.note.set('sessions.revoked');
      this.loadSessions();
    });
  }

  // ──────────────────────────────── shared ────────────────────────────────

  protected platformName(platform: DevicePlatform): string {
    switch (platform) {
      case DevicePlatform.Android:
        return 'Android';
      case DevicePlatform.Ios:
        return 'iOS';
      case DevicePlatform.Web:
        return 'Web';
      default:
        return '—';
    }
  }

  protected reachKey(reach: DeviceReach): string {
    switch (reach) {
      case DeviceReach.Reachable:
        return 'push.reachable';
      case DeviceReach.Muted:
        return 'push.muted';
      case DeviceReach.Silent:
        return 'push.stale';
      default:
        return 'push.tokenless';
    }
  }

  /**
   * A user agent, shortened to the part anybody reads.
   *
   * The whole string is a paragraph of version numbers, and the question being
   * asked of this column is only ever "is that the machine I think it is".
   */
  protected shortAgent(agent: string | null): string {
    if (!agent) return '—';

    const browser = /(Firefox|Edg|Chrome|Safari)\/[\d.]+/.exec(agent)?.[0] ?? '';
    const system = /\(([^)]+)\)/.exec(agent)?.[1]?.split(';')[0] ?? '';

    return [system, browser.replace('Edg/', 'Edge/')].filter(Boolean).join(' · ') || agent;
  }
}
