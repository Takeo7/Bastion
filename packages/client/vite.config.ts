import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { defineConfig, type Plugin } from 'vite';

/**
 * Dev-only: POST a data URL to /__snap?name=x and it is saved as
 * .dev-snapshots/x.jpg — lets tooling inspect frames from a hidden tab.
 */
function devSnapshots(): Plugin {
  const dir = resolve(import.meta.dirname, '../../.dev-snapshots');
  return {
    name: 'dev-snapshots',
    apply: 'serve',
    configureServer(server) {
      server.middlewares.use('/__snap', (req, res) => {
        let body = '';
        req.on('data', (chunk) => (body += chunk));
        req.on('end', () => {
          const name = new URL(req.url ?? '', 'http://x').searchParams.get('name')?.replace(/[^\w-]/g, '') || 'snap';
          mkdirSync(dir, { recursive: true });
          writeFileSync(resolve(dir, `${name}.jpg`), Buffer.from(body.replace(/^data:image\/\w+;base64,/, ''), 'base64'));
          res.end('ok');
        });
      });
    },
  };
}

export default defineConfig({
  plugins: [devSnapshots()],
  server: {
    host: true,
    port: 5173,
    strictPort: true,
    proxy: {
      '/ws': { target: 'ws://localhost:3000', ws: true },
    },
  },
  build: {
    target: 'es2022',
    chunkSizeWarningLimit: 2000,
  },
});
