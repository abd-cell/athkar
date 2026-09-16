import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { IS_SERVER } from './core/services/platform';

const serverConfig: ApplicationConfig = {
  providers: [
    // The one place this is true. Guards and storage read it to know that no
    // session is visible here, and that redirecting on that basis would turn
    // every deep link into a bounce to the login form.
    { provide: IS_SERVER, useValue: true },
    provideServerRendering(withRoutes(serverRoutes))
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
