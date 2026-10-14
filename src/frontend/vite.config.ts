import { fileURLToPath, URL } from 'node:url';

import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// TASK-078 (CTL-12): the SPA's response headers. The deployed host serves the production build with exactly these
// (endpoint-hardening.md §3.2); `vite preview` serves it with them too, which is what the header scan runs against. The bundle
// loads only its own scripts, styles and fonts and calls only its own origin's /api, so the policy allows only 'self'.
// The dev server sets none: Vite's hot reload needs inline scripts.
const SPA_SECURITY_HEADERS = {
  'Content-Security-Policy':
    "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; " +
    "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'",
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'Strict-Transport-Security': 'max-age=31536000; includeSubDomains',
  'Referrer-Policy': 'no-referrer',
  'Permissions-Policy':
    'accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()',
  'Cross-Origin-Opener-Policy': 'same-origin',
  'Cross-Origin-Resource-Policy': 'same-origin',
  'Cross-Origin-Embedder-Policy': 'require-corp',
};

// T-1 (ADR-003 §4.1): only VITE_-prefixed variables reach the bundle; no DB_ variable is ever exposed.
export default defineConfig({
  plugins: [react()],
  envPrefix: 'VITE_',
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    strictPort: true,
    // The SPA calls the API same-origin at /api/v1 (TASK-032 D-2). In development Vite forwards it to the API:
    // localhost:5080 from the host, http://api:8080 inside the compose network (PMPLATFORM_API_ORIGIN).
    proxy: {
      '/api': process.env.PMPLATFORM_API_ORIGIN ?? 'http://localhost:5080',
    },
  },
  preview: {
    headers: SPA_SECURITY_HEADERS,
  },
  build: {
    sourcemap: true,
  },
  // Unit tests only (TASK-015). e2e/ is Playwright's and is never collected here.
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['src/test/setup.ts'],
    restoreMocks: true,
  },
});
