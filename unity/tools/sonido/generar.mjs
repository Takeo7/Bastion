// The game's sound for Unity, generated from the 0.10.1's (docs/UNITY-SONIDO.md): the synthesised effects, the
// score and the beds rendered offline in Electron (render.ts), the recordings from Opus to WAV (ffmpeg), the
// interface's WAVs as they are, and manifiesto.json with every file's hash. Out of git: Unity's AppBuild checks the
// folder against the manifest.
//
//   node unity/tools/sonido/generar.mjs [--salida <dir>] [--comprobar]
//
// --comprobar generates again into a temporary folder and compares: the recordings and the interface byte for byte;
// what Chromium renders, sample by sample, one bit apart at most (its offline render varies that much between runs).
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '../../..');
const CLIENT = join(ROOT, 'packages/client');
const DEFAULT_OUT = join(ROOT, 'unity/Bastion/Assets/Bastion/Client/Audio/Generado');

/** The live pieces: the score where it isn't recorded (the base and the menus, the Map) and every ambience bed. */
const PIECES = [
  { kind: 'music', name: 'base', seconds: 480, seed: 11 },
  { kind: 'music', name: 'geo', seconds: 480, seed: 12 },
  { kind: 'ambience', name: 'city', seconds: 240, seed: 21 },
  { kind: 'ambience', name: 'wilds', seconds: 240, seed: 22 },
  { kind: 'ambience', name: 'facility', seconds: 240, seed: 23 },
  { kind: 'ambience', name: 'base', seconds: 240, seed: 24 },
  { kind: 'ambience', name: 'geo', seconds: 240, seed: 25 },
];

const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

function run(cmd, cmdArgs, opts = {}) {
  const r = spawnSync(cmd, cmdArgs, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, ...opts });
  if (r.status !== 0) throw new Error(`${cmd} ${cmdArgs.join(' ')}: ${(r.stderr || r.stdout || '').trim().slice(-2000)}`);
  return r.stdout;
}

/** render.ts bundled next to main.js and an index.html, in a temporary folder; Vite's import.meta.env as in a release. */
function bundle() {
  const dir = mkdtempSync(join(tmpdir(), 'bastion-sonido-'));
  run(join(ROOT, 'node_modules/.bin/esbuild'), [join(HERE, 'render.ts'), '--bundle', '--format=iife', '--loader:.css=empty', '--define:import.meta.env={"DEV":false,"PROD":true}', `--outfile=${join(dir, 'bundle.js')}`, '--log-level=error']);
  copyFileSync(join(HERE, 'main.js'), join(dir, 'main.js'));
  writeFileSync(join(dir, 'index.html'), '<!doctype html><meta charset="utf-8"><title>sonido</title><script src="bundle.js"></script>\n');
  return dir;
}

function electron(page, fn, fnArgs) {
  // packages/desktop's Electron, hoisted to the root's node_modules.
  const out = run(join(ROOT, 'node_modules/.bin/electron'), [join(page, 'main.js'), `--user-data-dir=${join(page, 'perfil')}`], {
    env: { ...process.env, TAREA: JSON.stringify({ fn, args: fnArgs }) },
  });
  const line = out.split('\n').find((l) => l.startsWith('RESULTADO '));
  if (!line) throw new Error(`Electron no dio resultado para ${fn}`);
  return JSON.parse(line.slice('RESULTADO '.length));
}

function recorded(out) {
  const src = join(CLIENT, 'public/audio/recorded');
  const index = JSON.parse(readFileSync(join(src, 'index.json'), 'utf8'));
  const list = {};
  for (const [group, takes] of Object.entries(index)) {
    list[group] = takes.map((t) => {
      const file = join('grabados', t.file.replace(/\.ogg$/, '.wav'));
      mkdirSync(dirname(join(out, file)), { recursive: true });
      // bitexact: the same WAV every time (no encoder tag, no metadata).
      run('ffmpeg', ['-v', 'error', '-y', '-i', join(src, t.file), '-map_metadata', '-1', '-fflags', '+bitexact', '-flags:a', '+bitexact', '-c:a', 'pcm_s16le', join(out, file)]);
      return { file, db: t.db ?? 0 };
    });
  }
  return list;
}

function interfaceSounds(out) {
  const src = join(CLIENT, 'public/audio/ui');
  mkdirSync(join(out, 'interfaz'), { recursive: true });
  return readdirSync(src)
    .filter((f) => f.endsWith('.wav'))
    .sort()
    .map((f) => {
      copyFileSync(join(src, f), join(out, 'interfaz', f));
      return join('interfaz', f);
    });
}

function walk(dir, base = dir) {
  return readdirSync(dir)
    .flatMap((f) => (statSync(join(dir, f)).isDirectory() ? walk(join(dir, f), base) : [relative(base, join(dir, f))]))
    .filter((f) => f.endsWith('.wav'))
    .sort();
}

function generate(out) {
  rmSync(out, { recursive: true, force: true });
  mkdirSync(out, { recursive: true });
  const started = Date.now();
  const page = bundle();
  try {
    const sintetizados = electron(page, 'renderEffects', [join(out, 'sintetizados')]);
    for (const s of Object.values(sintetizados)) {
      s.files = s.files.map((f) => join('sintetizados', f));
      if (s.loopFile) s.loopFile = join('sintetizados', s.loopFile);
    }
    const vivos = electron(page, 'renderPieces', [join(out, 'vivos'), PIECES]);
    for (const v of Object.values(vivos)) v.file = join('vivos', v.file);
    const grabados = recorded(out);
    const interfaz = interfaceSounds(out);
    const files = {};
    for (const f of walk(out)) {
      files[f] = { sha256: createHash('sha256').update(readFileSync(join(out, f))).digest('hex'), exacto: f.startsWith('grabados/') || f.startsWith('interfaz/') };
    }
    const source = spawnSync('git', ['log', '-1', '--format=%h', '--', 'packages/client/src/audio', 'packages/client/public/audio', 'unity/tools/sonido'], { cwd: ROOT, encoding: 'utf8' }).stdout.trim();
    const manifest = { formato: 1, origen: source, sintetizados, vivos, grabados, interfaz, archivos: files };
    writeFileSync(join(out, 'manifiesto.json'), JSON.stringify(manifest, null, 1) + '\n');
    const bytes = Object.keys(files).reduce((n, f) => n + statSync(join(out, f)).size, 0);
    console.log(`sonido: ${Object.keys(files).length} archivos, ${(bytes / 1e6).toFixed(0)} MB, en ${((Date.now() - started) / 1000).toFixed(0)} s → ${out}`);
    return manifest;
  } finally {
    rmSync(page, { recursive: true, force: true });
  }
}

/** 16-bit samples of a WAV written here (a 44-byte header). */
function samples(file) {
  const b = readFileSync(file);
  return new Int16Array(b.buffer, b.byteOffset + 44, (b.length - 44) / 2);
}

function compare(out) {
  if (!existsSync(join(out, 'manifiesto.json'))) throw new Error(`No hay ${out}/manifiesto.json: nada que comprobar.`);
  const old = JSON.parse(readFileSync(join(out, 'manifiesto.json'), 'utf8'));
  const tmp = mkdtempSync(join(tmpdir(), 'bastion-sonido-comprobar-'));
  try {
    const fresh = generate(tmp);
    const names = new Set([...Object.keys(old.archivos), ...Object.keys(fresh.archivos)]);
    let worst = 0;
    const bad = [];
    let apart = 0;
    for (const f of names) {
      const a = old.archivos[f];
      const b = fresh.archivos[f];
      if (!a || !b) {
        bad.push(`${f}: ${a ? 'ya no sale' : 'nuevo'}`);
        continue;
      }
      if (a.sha256 === b.sha256) continue;
      if (a.exacto) {
        bad.push(`${f}: distinto (debía ser idéntico)`);
        continue;
      }
      const x = samples(join(out, f));
      const y = samples(join(tmp, f));
      if (x.length !== y.length) {
        bad.push(`${f}: ${x.length} muestras frente a ${y.length}`);
        continue;
      }
      let most = 0;
      for (let i = 0; i < x.length; i++) most = Math.max(most, Math.abs(x[i] - y[i]));
      worst = Math.max(worst, most);
      apart++;
      if (most > 1) bad.push(`${f}: hasta ${most} LSB`);
    }
    console.log(`comprobar: ${names.size} archivos; ${apart} varían, como mucho ${worst} LSB${bad.length ? `; ${bad.length} no pasan:` : '; todo dentro de 1 LSB'}`);
    for (const b of bad.slice(0, 40)) console.log(`  ${b}`);
    if (bad.length) process.exitCode = 1;
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
}

const out = resolve(option('--salida') ?? DEFAULT_OUT);
if (flag('--comprobar')) compare(out);
else generate(out);
