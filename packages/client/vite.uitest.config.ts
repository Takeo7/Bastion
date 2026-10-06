import { defineConfig, mergeConfig } from 'vite';
import base from './vite.config';

/**
 * The game client wired to the throwaway test server (port 3300) instead of
 * the real one, for screenshots and UI reviews that must not touch the
 * players' saved campaign: `npx vite -c vite.uitest.config.ts`.
 */
export default mergeConfig(
  base,
  defineConfig({
    server: {
      port: 5190,
      strictPort: true,
      proxy: { '/ws': { target: 'ws://localhost:3300', ws: true } },
    },
  }),
);
