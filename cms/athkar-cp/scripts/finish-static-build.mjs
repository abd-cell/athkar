// Finishes what `ng build` leaves half-done for a non-SSR, static-hosting
// deployment (IIS, nginx, a CDN — anywhere without a Node process running
// server.mjs).
//
// The build always emits a server/ bundle (see app.routes.server.ts — every
// route is RenderMode.Server by project design, so `--output-mode static`
// is not available here) and names the client-only entry
// `browser/index.csr.html` rather than `index.html`, because that file is
// meant as the Node server's own CSR fallback, not a static host's default
// document. A plain static host never runs that server, so:
//
//   1. server/ is dropped — it is dead weight without the Node process.
//   2. index.csr.html becomes index.html, so a static host's default-document
//      lookup finds something (see public/web.config for the other half of
//      this: SPA-fallback routing for deep links).
//
// Run after `ng build`, not instead of it — see package.json's
// "build:static" script.
import { rename, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';

const dist = path.resolve(import.meta.dirname, '..', 'dist', 'athkar-cp');
const browser = path.join(dist, 'browser');
const csrIndex = path.join(browser, 'index.csr.html');
const index = path.join(browser, 'index.html');
const server = path.join(dist, 'server');

if (!existsSync(csrIndex)) {
  console.error(`Expected ${csrIndex} — did the build actually run first, or did its output layout change?`);
  process.exit(1);
}

await rename(csrIndex, index);
console.log(`renamed ${path.relative(dist, csrIndex)} -> ${path.relative(dist, index)}`);

if (existsSync(server)) {
  await rm(server, { recursive: true, force: true });
  console.log('removed server/ (not needed without the Node SSR process)');
}
