// Recorded sounds (docs/SONIDO.md): CC0 only, each from its own page. The
// originals go to assets-src/sonido/ (never into the repository, like Synty's);
// cut and converted, to packages/client/public/audio/recorded/, with the index
// the game reads (client audio/recorded.ts).
//
//   node packages/client/tools/sound/convert.mjs           cuts and converts what is in assets-src/sonido/
//   node packages/client/tools/sound/convert.mjs --bajar   first fetches the originals still missing
//
// Short sounds: the separate takes in each file (cut where it falls silent),
// mono, each brought to about the same loudness, faded at the ends. Beds: whole
// and stereo, quieter; only their level changes, so one made to loop still does.
// Needs ffmpeg.
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../../..');
const SRC = join(ROOT, 'assets-src', 'sonido');
const OUT = join(ROOT, 'packages', 'client', 'public', 'audio', 'recorded');

/** A Freesound sound's high-quality preview (an account is needed for the original file). */
const fs = (id, user) => `https://cdn.freesound.org/previews/${Math.floor(id / 1000)}/${id}_${user}-hq.ogg`;
const oga = (file) => `https://opengameart.org/sites/default/files/${file}`;

/**
 * Each original: where from, the group its takes go to, and how many takes at most. Groups (audio/recorded.ts):
 * the dead's growl, alert, attack, bite and eating; our soldiers' pain and scream, by sex; the sting on contact;
 * and the beds the music lays under combat (music.ts).
 */
const SOURCES = [
  { name: 'fs-323819', url: fs(323819, 4366671), group: 'zombie.growl', takes: 2 },
  { name: 'fs-555417', url: fs(555417, 8247784), group: 'zombie.growl', takes: 1 },
  { name: 'fs-204820', url: fs(204820, 1900515), group: 'zombie.growl', takes: 2 },
  { name: 'fs-769200', url: fs(769200, 16391898), group: 'zombie.growl', takes: 6 },
  { name: 'fs-612699', url: fs(612699, 11113354), group: 'zombie.growl', takes: 2 },
  { name: 'fs-435651', url: fs(435651, 7399650), group: 'zombie.alert', takes: 1 },
  { name: 'fs-179345', url: fs(179345, 544580), group: 'zombie.alert', takes: 1 },
  { name: 'fs-703317', url: fs(703317, 10334845), group: 'zombie.alert', takes: 1 },
  { name: 'fs-42201', url: fs(42201, 424069), group: 'zombie.alert', takes: 2 },
  { name: 'fs-213509', url: fs(213509, 3997324), group: 'zombie.attack', takes: 3 },
  { name: 'fs-155973', url: fs(155973, 2449149), group: 'zombie.bite', takes: 4 },
  { name: 'fs-445987', url: fs(445987, 9159316), group: 'zombie.bite', takes: 1 },
  { name: 'fs-467701', url: fs(467701, 8272463), group: 'zombie.bite', takes: 1 },
  { name: 'fs-448089', url: fs(448089, 9159316), group: 'zombie.eat', takes: 2 },
  { name: 'fs-236331', url: fs(236331, 985466), group: 'zombie.eat', takes: 2 },
  { name: 'fs-221544', url: fs(221544, 4096172), group: 'soldier.scream.m', takes: 1 },
  { name: 'fs-220819', url: fs(220819, 3559284), group: 'soldier.scream.m', takes: 1 },
  { name: 'fs-813310', url: fs(813310, 71257), group: 'soldier.scream.m', takes: 1 },
  { name: 'fs-416839', url: fs(416839, 8247784), group: 'soldier.pain.m', takes: 1 },
  { name: 'fs-416838', url: fs(416838, 8247784), group: 'soldier.pain.m', takes: 1 },
  { name: 'fs-219659', url: fs(219659, 4092389), group: 'soldier.scream.f', takes: 1 },
  { name: 'fs-618193', url: fs(618193, 13349052), group: 'soldier.scream.f', takes: 1 },
  { name: 'fs-673306', url: fs(673306, 13324371), group: 'soldier.scream.f', takes: 2 },
  { name: 'fs-845164', url: fs(845164, 11030713), group: 'soldier.scream.f', takes: 1 },
  { name: 'fs-504615', url: fs(504615, 7704891), group: 'melee.blade', takes: 1 },
  { name: 'fs-35213', url: fs(35213, 307822), group: 'melee.blade', takes: 1 },
  { name: 'fs-521030', url: fs(521030, 11490791), group: 'melee.blade', takes: 3 },
  { name: 'fs-539535', url: fs(539535, 12023220), group: 'melee.blade', takes: 3 },
  { name: 'fs-513332', url: fs(513332, 8644110), group: 'sting', takes: 1, db: -3 },
  { name: 'fs-725225', url: fs(725225, 985232), group: 'sting', takes: 1, db: -3 },
  { name: 'oga-wastelands', url: oga('Juhani%20Junkala%20-%20Post%20Apocalyptic%20Wastelands%20%5BLoop%20Ready%5D.ogg'), group: 'bed.ambience', bed: 'loop' },
  { name: 'oga-emptycity', url: oga('EmptyCity.ogg'), group: 'bed.ambience', bed: 'loop' },
  { name: 'fs-785818', url: fs(785818, 16335323), group: 'bed.ambience', bed: 'fade' },
  { name: 'fs-686762', url: fs(686762, 3983735), group: 'bed.ambience', bed: 'fade', max: 150 },
  { name: 'oga-bleedingout', url: oga('bleeding_out2_2.ogg'), group: 'bed.tension', bed: 'fade' },
  { name: 'fs-826597', url: fs(826597, 17484852), group: 'bed.tension', bed: 'fade' },
  { name: 'fs-871722', url: fs(871722, 413687), group: 'bed.tension', bed: 'fade' },
];

/** How long a take may last, by group (seconds): shorter pieces are clicks and breaths, longer ones are cut. */
const SPAN = {
  'zombie.growl': [0.6, 3.5],
  'zombie.alert': [0.5, 3],
  'zombie.attack': [0.4, 2.5],
  'zombie.bite': [0.15, 2],
  'zombie.eat': [1, 4],
  'soldier.scream.m': [0.4, 2.5],
  'soldier.scream.f': [0.4, 2.5],
  'soldier.pain.m': [0.25, 1.5],
  'melee.blade': [0.12, 1],
  sting: [1, 6],
};

/** Short takes' loudness (mean, dB) and the most their peaks may reach; beds sit lower. */
const TAKE_MEAN = -20;
const BED_MEAN = -28;
const PEAK = -1.5;

const run = (args) => execFileSync('ffmpeg', ['-hide_banner', '-nostats', ...args], { stdio: ['ignore', 'pipe', 'pipe'] });

function duration(file) {
  return Number(execFileSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', file], { encoding: 'utf8' }).trim());
}

/** Mean and peak volume (dB) of `file` from `start`, `length` seconds long. */
function levels(file, start = 0, length) {
  const args = ['-ss', String(start), ...(length ? ['-t', String(length)] : []), '-i', file, '-af', 'volumedetect'];
  let text;
  try {
    text = execFileSync('ffmpeg', ['-hide_banner', '-nostats', ...args, '-f', 'null', '-'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  } catch (e) {
    text = String(e.stderr ?? '');
  }
  return { mean: Number(/mean_volume: (-?[\d.]+)/.exec(text)?.[1] ?? -20), peak: Number(/max_volume: (-?[\d.]+)/.exec(text)?.[1] ?? 0) };
}

/** Where `file` makes a sound: the spans between its silences, longest first. */
function takes(file) {
  const total = duration(file);
  let text = '';
  try {
    text = execFileSync('ffmpeg', ['-hide_banner', '-nostats', '-i', file, '-af', 'silencedetect=noise=-38dB:d=0.18', '-f', 'null', '-'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  } catch (e) {
    text = String(e.stderr ?? '');
  }
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
    if (s.bed) {
      const total = Math.min(duration(file), s.max ?? Infinity);
      const { mean, peak } = levels(file, 0, total);
      const gain = Math.min(BED_MEAN - mean, PEAK - peak);
      const fades = s.bed === 'fade' ? `,afade=t=in:d=2,afade=t=out:st=${(total - 2).toFixed(2)}:d=2` : '';
      const out = join(dir, `${s.name}.ogg`);
      run(['-y', '-t', String(total), '-i', file, '-af', `volume=${gain.toFixed(2)}dB${fades}`, '-ac', '2', '-ar', '44100', '-c:a', 'libvorbis', '-q:a', '3', out]);
      (index[s.group] ??= []).push({ file: `${s.group.replace(/\./g, '-')}/${s.name}.ogg`, db: s.db ?? 0 });
      console.log(`${s.group} ← ${s.name} (fondo, ${total.toFixed(0)} s)`);
      continue;
    }
    const [min, max] = SPAN[s.group] ?? [0.3, 3];
    const spans = takes(file)
      .map(([a, b]) => [a, Math.min(b, a + max)])
      .filter(([a, b]) => b - a >= min)
      .sort((p, q) => q[1] - q[0] - (p[1] - p[0]))
      .slice(0, s.takes ?? 1);
    spans.forEach(([a, b], i) => {
      const length = b - a;
      const { mean, peak } = levels(file, a, length);
      const gain = Math.min(TAKE_MEAN - mean, PEAK - peak);
      const out = join(dir, `${s.name}-${i + 1}.ogg`);
      const fade = Math.min(0.08, length / 4);
      run(['-y', '-ss', a.toFixed(3), '-t', length.toFixed(3), '-i', file, '-af', `volume=${gain.toFixed(2)}dB,afade=t=in:d=0.01,afade=t=out:st=${(length - fade).toFixed(3)}:d=${fade.toFixed(3)}`, '-ac', '1', '-ar', '44100', '-c:a', 'libvorbis', '-q:a', '4', out]);
      (index[s.group] ??= []).push({ file: `${s.group.replace(/\./g, '-')}/${s.name}-${i + 1}.ogg`, db: s.db ?? 0 });
    });
    console.log(`${s.group} ← ${s.name} (${spans.length} ${spans.length === 1 ? 'toma' : 'tomas'})`);
  }
  writeFileSync(join(OUT, 'index.json'), `${JSON.stringify(index, null, 2)}\n`);
  console.log(`Sonidos grabados en ${OUT}`);
}

if (process.argv.includes('--bajar')) fetchMissing();
convert();
