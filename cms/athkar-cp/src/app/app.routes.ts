import { Routes } from '@angular/router';

import { LangWrapperComponent } from './components/lang-wrapper.component';
import { appAuthGuard, languageGuard, loggedInGuard, superAdminGuard } from './core/guard/guards';

/**
 * Language-first routes: `/:languageCode/...`, guarded by `languageGuard`.
 *
 * Every screen is lazy (`loadComponent`), so the login page does not carry the
 * whole console with it — which is the only page most visits ever reach.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'ar' },
  {
    path: ':languageCode',
    component: LangWrapperComponent,
    canActivate: [languageGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'login',
        canActivate: [loggedInGuard],
        loadComponent: () =>
          import('./features/auth/login.component').then((m) => m.LoginComponent),
      },
      {
        path: '',
        canActivate: [appAuthGuard],
        loadComponent: () =>
          import('./features/layout/main-layout.component').then((m) => m.MainLayoutComponent),
        children: [
          {
            path: 'dashboard',
            loadComponent: () =>
              import('./features/dashboard/dashboard.component').then(
                (m) => m.DashboardComponent,
              ),
          },
          {
            path: 'categories',
            loadComponent: () =>
              import('./features/content/categories.component').then(
                (m) => m.CategoriesComponent,
              ),
          },
          {
            path: 'radio',
            loadComponent: () =>
              import('./features/content/radio.component').then((m) => m.RadioComponent),
          },
          {
            path: 'recitations',
            loadComponent: () =>
              import('./features/content/recitations.component').then(
                (m) => m.RecitationsComponent,
              ),
          },
          {
            path: 'adhkar',
            loadComponent: () =>
              import('./features/content/adhkar.component').then((m) => m.AdhkarComponent),
          },
          {
            path: 'reminders',
            loadComponent: () =>
              import('./features/reminders/reminders.component').then(
                (m) => m.RemindersComponent,
              ),
          },
          {
            path: 'broadcasts',
            loadComponent: () =>
              import('./features/broadcasts/broadcasts.component').then(
                (m) => m.BroadcastsComponent,
              ),
          },
          {
            path: 'push',
            loadComponent: () =>
              import('./features/push/push.component').then((m) => m.PushComponent),
          },
          {
            path: 'widget',
            loadComponent: () =>
              import('./features/widget/widget.component').then((m) => m.WidgetComponent),
          },
          {
            path: 'widget-catalog',
            loadComponent: () =>
              import('./features/widget/widget-catalog.component').then(
                (m) => m.WidgetCatalogComponent,
              ),
          },
          {
            path: 'languages',
            loadComponent: () =>
              import('./features/languages/languages.component').then(
                (m) => m.LanguagesComponent,
              ),
          },
          {
            path: 'quran',
            loadComponent: () =>
              import('./features/quran/quran.component').then((m) => m.QuranComponent),
          },
          {
            path: 'faq',
            loadComponent: () =>
              import('./features/support/faq.component').then((m) => m.FaqComponent),
          },
          {
            path: 'feedback',
            loadComponent: () =>
              import('./features/support/feedback.component').then((m) => m.FeedbackComponent),
          },
          {
            path: 'settings',
            loadComponent: () =>
              import('./features/settings/settings.component').then((m) => m.SettingsComponent),
          },
          {
            path: 'staff',
            canActivate: [superAdminGuard],
            loadComponent: () =>
              import('./features/staff/staff.component').then((m) => m.StaffComponent),
          },
          {
            path: 'sessions',
            loadComponent: () =>
              import('./features/sessions/sessions.component').then((m) => m.SessionsComponent),
          },
          {
            path: 'audit',
            loadComponent: () =>
              import('./features/audit/audit.component').then((m) => m.AuditComponent),
          },
          {
            path: 'logs',
            loadComponent: () =>
              import('./features/audit/logs.component').then((m) => m.LogsComponent),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: 'ar' },
];
