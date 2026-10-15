import { resolve } from 'node:path';
import { defineConfig, mergeConfig } from 'vite';
import base from '../../vite.config.ts';

// The trailer page alone, built into .dev-snapshots/trailer/web for the
// receiver (receiver.ts --build) to serve: a fixed copy, which a vite reload
// can never interrupt mid-take.
const client = resolve(import.meta.dirname, '../..');

export default mergeConfig(
  base,
  defineConfig({
    root: client,
    build: {
      outDir: resolve(client, '../../.dev-snapshots/trailer/web'),
      emptyOutDir: true,
      rollupOptions: { input: resolve(client, 'trailer.html') },
    },
  }),
);
