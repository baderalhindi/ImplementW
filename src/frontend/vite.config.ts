import { fileURLToPath, URL } from 'node:url';

import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

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
