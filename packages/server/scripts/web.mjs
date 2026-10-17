// Builds the game for a website (docs/FASE11.md §6, docs/WEB.md), into packages/server/dist:
//   bastion/client/      the game, built by Vite (never the players' packages/client/dist)
//   bastion/server.mjs   packages/server with the engine and ws in one file, as a website's server (BASTION_WEB)
//   bastion/LEEME.md     the guide (docs/WEB.md)
//   Bastion-web-<version>.tar.gz   all of it, to copy to the server
//
//   npm run web:dist
import { build as bundle } from 'esbuild';
import { execFileSync } from 'node:child_process';
import { copyFileSync, mkdirSync, readFileSync, renameSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { build as vite } from 'vite';

const server = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const packages = resolve(server, '..');
const dist = join(server, 'dist');
const out = join(dist, 'bastion');
// Built aside and swapped in at the end: a failed build leaves the last good one in place.
const fresh = join(dist, 'bastion-nuevo');
// The game's version is the app's (packages/desktop).
const { version } = JSON.parse(readFileSync(join(packages, 'desktop', 'package.json'), 'utf8'));

rmSync(fresh, { recursive: true, force: true });
mkdirSync(fresh, { recursive: true });

const client = join(packages, 'client');
await vite({ root: client, configFile: join(client, 'vite.config.ts'), logLevel: 'warn', build: { outDir: join(fresh, 'client'), emptyOutDir: true } });

await bundle({
  entryPoints: [join(server, 'src', 'index.ts')],
  outfile: join(fresh, 'server.mjs'),
  bundle: true,
  platform: 'node',
  format: 'esm',
  target: 'node22',
  // A website's server (index.ts WEB), before anything in it runs; ws inside it still calls require.
  banner: { js: "process.env.BASTION_WEB ??= '1'; import { createRequire } from 'node:module'; const require = createRequire(import.meta.url);" },
  // ws's optional native speed-ups: not installed, and ws does without them.
  external: ['bufferutil', 'utf-8-validate'],
  logLevel: 'warning',
});
copyFileSync(join(packages, '..', 'docs', 'WEB.md'), join(fresh, 'LEEME.md'));

rmSync(out, { recursive: true, force: true });
renameSync(fresh, out);
const tarball = `Bastion-web-${version}.tar.gz`;
rmSync(join(dist, tarball), { force: true });
// macOS's tar leaves its extended attributes out of it, so Linux's has nothing to complain about.
execFileSync('tar', ['-czf', tarball, ...(process.platform === 'darwin' ? ['--no-xattrs', '--no-mac-metadata'] : []), 'bastion'], { cwd: dist, env: { ...process.env, COPYFILE_DISABLE: '1' } });
console.log(`Bastión (web): ${out}\n  y para copiar al servidor: ${join(dist, tarball)}`);
