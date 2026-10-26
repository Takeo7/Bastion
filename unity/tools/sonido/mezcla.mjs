// Unity's sound mix against 0.10.1's (docs/UNITY-SONIDO.md §5): each script of guiones/ (sounds played, stopped,
// looped, ducked, the volumes moved…) rendered by engine.ts in Chromium, offline (mezcla.ts), and by Mixer.cs
// (mezcla/, dotnet), with the same sound files and the same random numbers; then compared, half a second at a time.
//
//   node unity/tools/sonido/mezcla.mjs [--sonido <dir>] [guion.json…]
//
// A script passes if nowhere does the difference come within 60 dB of the sound (they agree to 80–120).
import { spawnSync } from 'node:child_process';
import { copyFileSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '../../..');
const LIMIT = -60;

const args = process.argv.slice(2);
const at = args.indexOf('--sonido');
const sound = resolve(at >= 0 ? args.splice(at, 2)[1] : join(ROOT, 'unity/Bastion/Assets/Bastion/Client/Audio/Resources/Generado'));
const scripts = args.length ? args.map((a) => resolve(a)) : readdirSync(join(HERE, 'guiones')).filter((f) => f.endsWith('.json')).sort().map((f) => join(HERE, 'guiones', f));

function run(cmd, cmdArgs, opts = {}) {
  const r = spawnSync(cmd, cmdArgs, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, ...opts });
  if (r.status !== 0) throw new Error(`${cmd} ${cmdArgs.join(' ')}: ${(r.stderr || r.stdout || '').trim().slice(-2000)}`);
  return r.stdout;
}

const dir = mkdtempSync(join(tmpdir(), 'bastion-mezcla-'));
run(join(ROOT, 'node_modules/.bin/esbuild'), [join(HERE, 'mezcla.ts'), '--bundle', '--format=iife', '--loader:.css=empty', '--define:import.meta.env={"DEV":false,"PROD":true}', `--outfile=${join(dir, 'bundle.js')}`, '--log-level=error']);
copyFileSync(join(HERE, 'main.js'), join(dir, 'main.js'));
writeFileSync(join(dir, 'index.html'), '<!doctype html><meta charset="utf-8"><title>mezcla</title><script src="bundle.js"></script>\n');
run('dotnet', ['build', join(HERE, 'mezcla/Mezcla.csproj'), '-c', 'Release', '-nologo', '-v', 'q', `-o`, join(dir, 'cs')]);

const read = (file) => {
  const b = readFileSync(file);
  return new Float32Array(b.buffer, b.byteOffset, b.byteLength / 4);
};
const db = (x) => (x > 0 ? 10 * Math.log10(x) : -Infinity);

let failed = 0;
for (const script of scripts) {
  const name = basename(script, '.json');
  const g = JSON.parse(readFileSync(script, 'utf8'));
  const chromium = join(dir, `${name}.chromium.f32`);
  const unity = join(dir, `${name}.unity.f32`);
  const out = run(join(ROOT, 'node_modules/.bin/electron'), [join(dir, 'main.js'), `--user-data-dir=${join(dir, 'perfil')}`], {
    env: { ...process.env, TAREA: JSON.stringify({ fn: 'renderMix', args: [g, sound, chromium] }) },
  });
  if (!out.includes('RESULTADO ')) throw new Error(`Electron no dio resultado para ${name}`);
  run('dotnet', [join(dir, 'cs/Mezcla.dll'), script, sound, unity]);
  const a = read(chromium);
  const b = read(unity);
  const win = Math.floor(g.rate * 0.5);
  let worst = -Infinity;
  let where = 0;
  for (let s = 0; s * 2 < a.length; s += win) {
    for (let c = 0; c < 2; c++) {
      let sig = 0;
      let dif = 0;
      const e = Math.min(a.length / 2, s + win);
      for (let i = s; i < e; i++) {
        const x = a[i * 2 + c];
        sig += x * x;
        dif += (x - b[i * 2 + c]) ** 2;
      }
      // Below −100 dB the sound is gone: what's left there is rounding.
      if (db(sig / (e - s)) < -100) continue;
      const rel = db(dif) - db(sig);
      if (rel > worst) {
        worst = rel;
        where = s / g.rate;
      }
    }
  }
  const ok = worst <= LIMIT;
  if (!ok) failed++;
  console.log(`${ok ? 'bien' : 'MAL '}  ${name}: la diferencia, a ${(-worst).toFixed(0)} dB del sonido como poco (a los ${where.toFixed(1)} s)`);
}
process.exitCode = failed ? 1 : 0;
