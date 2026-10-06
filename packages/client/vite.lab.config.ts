import { resolve } from 'node:path';
import { defineConfig } from 'vite';

/**
 * Static build of the style comparison page, kept apart from the game's
 * build: `npx vite build -c vite.lab.config.ts --outDir <dir>`. Relative
 * paths so the files can be hosted anywhere (e.g. as a published page).
 */
export default defineConfig({
  root: import.meta.dirname,
  base: './',
  build: {
    target: 'es2022',
    emptyOutDir: true,
    chunkSizeWarningLimit: 4000,
    rollupOptions: {
      input: { comparar: resolve(import.meta.dirname, 'comparar.html') },
    },
  },
});
