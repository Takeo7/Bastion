// The recordings made for Unity since the 0.10.1 (docs/SONIDO.md, «Desde la 0.10.1»): CC0 only, each from its own
// page, downloaded with Arturo's yes (29/10). The originals go to assets-src/sonido/, never into the repository; cut
// and converted, to unity/tools/sonido/grabados/ with its index.json, which generar.mjs adds to the 0.10.1's recordings
// (packages/ is frozen). The same cutting as packages/client/tools/sound/convert.mjs.
//
//   node unity/tools/sonido/grabados.mjs           cuts and converts what is in assets-src/sonido/
//   node unity/tools/sonido/grabados.mjs --bajar   first fetches the originals still missing
//
// Short sounds: the separate takes in each file (cut where it falls silent), or the given `cuts` of one that never
// does (a chainsaw); mono, each brought to about the same loudness, faded at the ends. Opus in Ogg, as convert.mjs.
import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '../../..');
const SRC = join(ROOT, 'assets-src', 'sonido');
const OUT = join(HERE, 'grabados');

/** A Freesound sound's high-quality preview (an account is needed for the original file). */
const fs = (id, user) => `https://cdn.freesound.org/previews/${Math.floor(id / 1000)}/${id}_${user}-hq.ogg`;

/**
 * Each original: where from, the group its takes go to, and how many takes at most (or which `cuts`, [start, length]
 * in seconds); `max`, a take's length at most for this one. The Hinchado's own voice, steps, death and burst (Ana, 20 p1); the new blunt weapons' blows over the
 * body's thud, and the chainsaw revving and cutting (Ana, 21 p2).
 */
const SOURCES = [
  { name: 'fs-830859', url: fs(830859, 195590), group: 'bloater.voice', takes: 3 },
  { name: 'fs-653581', url: fs(653581, 14083205), group: 'bloater.voice', takes: 4 },
  { name: 'fs-653515', url: fs(653515, 2194110), group: 'bloater.death', takes: 3 },
  { name: 'fs-676531', url: fs(676531, 14725713), group: 'bloater.step', takes: 6 },
  // (fs-513309, downloaded with these, stays out: its takes were mostly hiss, 7 kHz bright at −32 dB.)
  { name: 'fs-344904', url: fs(344904, 4082837), group: 'bloater.burst', takes: 1 },
  { name: 'fs-504629', url: fs(504629, 8171657), group: 'bloater.burst', takes: 1 },
  { name: 'fs-396345', url: fs(396345, 7057194), group: 'melee.blunt', takes: 6 },
  { name: 'fs-529567', url: fs(529567, 1380975), group: 'melee.pan', takes: 1 },
  { name: 'fs-401706', url: fs(401706, 6188540), group: 'melee.pan', takes: 1 },
  // An aluminium bat's ping rings 460 ms at 1.9 kHz (a bell, Ana's #53 of 22/10): only its first 0.2 s, under the thud.
  { name: 'fs-684607', url: fs(684607, 14228776), group: 'melee.bat', takes: 1, max: 0.2 },
  { name: 'fs-443243', url: fs(443243, 4277096), group: 'melee.crowbar', takes: 3, max: 0.4 },
  { name: 'fs-453259', url: fs(453259, 612689), group: 'melee.chainsaw.rev', cuts: [[7, 3]] },
  // An ambulance's siren at its start: the cuts are from where it cuts at full revs, well after.
  { name: 'fs-185579', url: fs(185579, 230160), group: 'melee.chainsaw.cut', cuts: [[46, 2.5], [58, 2.5], [68, 2.5]] },
  { name: 'fs-810392', url: fs(810392, 2040107), group: 'melee.chainsaw.cut', cuts: [[4, 2.5], [108, 2.5]] },
];

/** How long a take may last, by group (seconds): shorter pieces are clicks and breaths, longer ones are cut. */
const SPAN = {
  'bloater.voice': [0.6, 3],
  'bloater.death': [0.8, 3],
  'bloater.step': [0.12, 0.6],
  'bloater.burst': [0.3, 2.5],
  'melee.blunt': [0.15, 0.6],
  'melee.pan': [0.2, 0.8],
  'melee.bat': [0.2, 0.8],
  'melee.crowbar': [0.12, 0.8],
};

/** Short takes' loudness (mean, dB) and the most their peaks may reach (Opus lifts them a little). */
const TAKE_MEAN = -20;
const PEAK = -3;

const run = (args) => execFileSync('ffmpeg', ['-hide_banner', '-nostats', ...args], { stdio: ['ignore', 'pipe', 'pipe'] });
/** What an analysis pass reports: ffmpeg writes it to stderr, whether or not it fails. */
const analyse = (args) => spawnSync('ffmpeg', ['-hide_banner', '-nostats', ...args, '-f', 'null', '-'], { encoding: 'utf8' }).stderr ?? '';

function duration(file) {
  return Number(execFileSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', file], { encoding: 'utf8' }).trim());
}

/** Mean and peak volume (dB) of `file` from `start`, `length` seconds long. */
function levels(file, start = 0, length) {
  const text = analyse(['-ss', String(start), ...(length ? ['-t', String(length)] : []), '-i', file, '-af', 'volumedetect']);
  return { mean: Number(/mean_volume: (-?[\d.]+)/.exec(text)?.[1] ?? -20), peak: Number(/max_volume: (-?[\d.]+)/.exec(text)?.[1] ?? 0) };
}

/** Where `file` makes a sound: the spans between its silences. */
function takes(file) {
  const total = duration(file);
  const text = analyse(['-i', file, '-af', 'silencedetect=noise=-38dB:d=0.18']);
  const starts = [...text.matchAll(/silence_start: (-?[\d.]+)/g)].map((m) => Math.max(0, Number(m[1])));
  const ends = [...text.matchAll(/silence_end: ([\d.]+)/g)].map((m) => Number(m[1]));
  const spans = [];
  let from = 0;
  for (let i = 0; i < starts.length; i++) {
    if (starts[i] > from) spans.push([from, starts[i]]);
    from = ends[i] ?? total;
  }
  if (from < total) spans.push([from, total]);
  return spans;
}

function fetchMissing() {
  mkdirSync(SRC, { recursive: true });
  for (const s of SOURCES) {
    const file = join(SRC, `${s.name}.ogg`);
    if (existsSync(file)) continue;
    console.log(`bajando ${s.name} · ${s.url}`);
    execFileSync('curl', ['-fsSL', '--retry', '2', '-o', file, s.url], { stdio: 'inherit' });
  }
}

function convert() {
  rmSync(OUT, { recursive: true, force: true });
  mkdirSync(OUT, { recursive: true });
  const index = {};
  for (const s of SOURCES) {
    const file = join(SRC, `${s.name}.ogg`);
    if (!existsSync(file)) {
      console.log(`falta ${s.name} (con --bajar se baja)`);
      continue;
    }
    const dir = join(OUT, s.group.replace(/\./g, '-'));
    mkdirSync(dir, { recursive: true });
    const [min, longest] = SPAN[s.group] ?? [0.3, 3];
    const max = s.max ?? longest;
    // Cuts are given whole (a sound that never falls silent); takes, the longest first, as convert.mjs.
    const spans = s.cuts
      ? s.cuts.map(([a, length]) => [a, a + length])
      : takes(file)
          .map(([a, b]) => [a, Math.min(b, a + max)])
          .filter(([a, b]) => b - a >= min)
          .sort((p, q) => q[1] - q[0] - (p[1] - p[0]))
          .slice(0, s.takes ?? 1);
    spans.forEach(([a, b], i) => {
      const length = b - a;
      const { mean, peak } = levels(file, a, length);
      const gain = Math.min(TAKE_MEAN - mean, PEAK - peak);
      const out = join(dir, `${s.name}-${i + 1}.ogg`);
      const fade = s.cuts ? Math.min(0.25, length / 4) : Math.min(0.08, length / 4);
      const fadeIn = s.cuts ? 0.04 : 0.01;
      run(['-y', '-ss', a.toFixed(3), '-t', length.toFixed(3), '-i', file, '-af', `volume=${gain.toFixed(2)}dB,afade=t=in:d=${fadeIn},afade=t=out:st=${(length - fade).toFixed(3)}:d=${fade.toFixed(3)}`, '-ac', '1', '-ar', '48000', '-c:a', 'libopus', '-b:a', '64k', out]);
      (index[s.group] ??= []).push({ file: `${s.group.replace(/\./g, '-')}/${s.name}-${i + 1}.ogg`, db: s.db ?? 0 });
    });
    console.log(`${s.group} ← ${s.name} (${spans.length} ${spans.length === 1 ? 'toma' : 'tomas'})`);
  }
  writeFileSync(join(OUT, 'index.json'), `${JSON.stringify(index, null, 2)}\n`);
  console.log(`Sonidos grabados en ${OUT}`);
}

if (process.argv.includes('--bajar')) fetchMissing();
convert();
