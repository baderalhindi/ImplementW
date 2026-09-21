import { fileURLToPath, URL } from 'node:url';

import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

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
  },
  build: {
    sourcemap: true,
  },
});
