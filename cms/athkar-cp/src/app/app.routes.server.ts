import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * Server-rendered, never prerendered.
 *
 * Every route carries the `:languageCode` parameter, and prerendering a
 * parameterised route would mean enumerating the languages at build time — in a
 * console whose whole point is that an admin adds a language without a release.
 */
export const serverRoutes: ServerRoute[] = [
  {
    path: '**',
    renderMode: RenderMode.Server,
  },
];
