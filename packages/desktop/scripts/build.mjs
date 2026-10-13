// Builds what the desktop app runs, into packages/desktop/dist/app:
//   client/            the game, built by Vite (never the players' packages/client/dist)
//   server/server.mjs  packages/server with the engine and ws in one file, for Electron's Node
//   main.cjs, preload.cjs, launcher.html   the app itself
// electron-builder then packages it (electron-builder.json).
import { build as bundle } from 'esbuild';
import { copyFileSync, mkdirSync, renameSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { build as vite } from 'vite';

const desktop = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const packages = resolve(desktop, '..');
const app = join(desktop, 'dist', 'app');
// Built aside and swapped in at the end: a failed build leaves the last good one in place.
const out = join(desktop, 'dist', 'app-nuevo');

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

const client = join(packages, 'client');
await vite({ root: client, configFile: join(client, 'vite.config.ts'), logLevel: 'warn', build: { outDir: join(out, 'client'), emptyOutDir: true } });

// The server is ESM (import.meta.dirname); ws inside it still calls require.
await bundle({
  entryPoints: [join(packages, 'server', 'src', 'index.ts')],
  outfile: join(out, 'server', 'server.mjs'),
  bundle: true,
  platform: 'node',
  format: 'esm',
  target: 'node22',
  banner: { js: "import { createRequire } from 'node:module'; const require = createRequire(import.meta.url);" },
  // ws's optional native speed-ups: not installed, and ws does without them.
  external: ['bufferutil', 'utf-8-validate'],
  logLevel: 'warning',
});

for (const name of ['main', 'preload']) {
  await bundle({
    entryPoints: [join(desktop, 'src', `${name}.ts`)],
    outfile: join(out, `${name}.cjs`),
    bundle: true,
    platform: 'node',
    format: 'cjs',
    target: 'node22',
    external: ['electron'],
    logLevel: 'warning',
  });
}
copyFileSync(join(desktop, 'src', 'launcher.html'), join(out, 'launcher.html'));
rmSync(app, { recursive: true, force: true });
renameSync(out, app);
console.log(`Bastión (escritorio): compilado en ${app}`);
