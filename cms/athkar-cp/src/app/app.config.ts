import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideClientHydration } from '@angular/platform-browser';
import { provideRouter, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { refreshInterceptor } from './core/interceptors/refresh.interceptor';
import { AppConfigService } from './core/services/app-config.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideClientHydration(),
    provideHttpClient(
      withFetch(),
      // Order matters on the way back: responses unwind in reverse, so
      // `refreshInterceptor` sees a 401 before `errorInterceptor` ends the
      // session. See the note in refresh.interceptor.ts.
      withInterceptors([authInterceptor, errorInterceptor, refreshInterceptor]),
    ),
    // The brand is painted before the first screen, so nothing flashes the
    // default palette on its way to the configured one.
    provideAppInitializer(() => inject(AppConfigService).load()),
  ],
};
