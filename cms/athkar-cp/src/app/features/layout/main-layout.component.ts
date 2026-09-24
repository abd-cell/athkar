import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { ApiService } from '../../core/api/api.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { GlobalService } from '../../core/services/global.service';

interface NavItem {
  readonly path: string;
  readonly key: string;
  /** Hidden from an editor. */
  readonly adminOnly?: boolean;
  readonly superAdminOnly?: boolean;
}

interface NavGroup {
  readonly key: string;
  readonly items: readonly NavItem[];
}

/**
 * The console's frame: a sidebar of grouped links, a header, and the outlet.
 *
 * The nav is filtered by role rather than merely disabled — an editor who can
 * see a "Staff" link they cannot open learns nothing useful from it, and the
 * route guard refuses them anyway.
 */
@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
})
export class MainLayoutComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  protected readonly global = inject(GlobalService);

  private static readonly GROUPS: readonly NavGroup[] = [
    {
      key: 'nav.dashboard',
      items: [{ path: 'dashboard', key: 'nav.dashboard' }],
    },
    {
      key: 'nav.content',
      items: [
        { path: 'categories', key: 'nav.categories' },
        { path: 'adhkar', key: 'nav.adhkar' },
        { path: 'radio', key: 'nav.radio' },
        { path: 'recitations', key: 'nav.recitations' },
        { path: 'languages', key: 'nav.languages', adminOnly: true },
        { path: 'quran', key: 'nav.quran', adminOnly: true },
      ],
    },
    {
      key: 'nav.reminders',
      items: [
        { path: 'reminders', key: 'nav.reminders', adminOnly: true },
        { path: 'broadcasts', key: 'nav.broadcasts', adminOnly: true },
        { path: 'push', key: 'nav.push', adminOnly: true },
        { path: 'widget', key: 'nav.widget', adminOnly: true },
        // No adminOnly: naming a widget in a new language is editorial work,
        // the same as naming a chapter, and routing it through an administrator
        // only means the wording waits.
        { path: 'widget-catalog', key: 'nav.widgetCatalog' },
      ],
    },
    {
      key: 'nav.support',
      items: [
        { path: 'faq', key: 'nav.faq' },
        { path: 'feedback', key: 'nav.feedback' },
      ],
    },
    {
      key: 'nav.settings',
      items: [
        { path: 'settings', key: 'nav.settings', adminOnly: true },
        { path: 'staff', key: 'nav.staff', superAdminOnly: true },
        // Next to staff rather than next to the push manager: the installs half
        // is about notifications, but the other half ends somebody's access,
        // and that belongs where the accounts are.
        { path: 'sessions', key: 'nav.sessions', adminOnly: true },
        { path: 'audit', key: 'nav.audit', adminOnly: true },
        { path: 'logs', key: 'nav.logs', adminOnly: true },
      ],
    },
  ];

  protected readonly groups = computed(() =>
    MainLayoutComponent.GROUPS.map((group) => ({
      key: group.key,
      items: group.items.filter(
        (item) =>
          (!item.adminOnly || this.global.isAtLeastAdmin()) &&
          (!item.superAdminOnly || this.global.isSuperAdmin()),
      ),
    })).filter((group) => group.items.length > 0),
  );

  protected readonly language = this.global.languageCode;

  protected switchLanguage(): void {
    const next = this.global.languageCode() === 'ar' ? 'en' : 'ar';
    const rest = this.router.url.split('/').slice(2).join('/');

    this.global.setLanguage(next);
    void this.router.navigateByUrl(`/${next}/${rest}`);
  }

  protected signOut(): void {
    // The call is fired but not waited on: the session row is revoked
    // server-side either way, and a reader of this console should not be held
    // on a spinner while a network they may not have answers.
    this.api.logout().subscribe();

    this.global.signOut();
    void this.router.navigate(['/', this.global.languageCode(), 'login']);
  }
}
