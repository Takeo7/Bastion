// The trailer's montage (docs/TRAILER.md): the takes in script order, the
// music and the game's sounds of each take, into one file with ffmpeg.
//
//   npx tsx packages/client/tools/trailer/montage.ts [--borrador] [--musica musica/maqueta-30s.wav]
//
// Reads, under .dev-snapshots/trailer/:
//   tomas/<id>.mp4   each take (receiver.ts); one still missing is black for its time
//   tomas/<id>.wav   the sounds the game made during it, laid at the take's place
//   musica/musica.wav   the music (`?musica` on trailer.html), else the latest mock-up
// and writes trailer.mp4: 1920×1080, 60 fps, H.264, AAC, at −14 LUFS (what
// YouTube and Steam play at), true peak −1 dB. The music dips a little under
// loud sounds. `--borrador` encodes fast, for looking at the cut.
// The cuts happen under UI's covers (black or the wipe): no crossfades here.

import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { LENGTH, SCRIPT } from '../../src/lab/trailer/script.ts';

const OUT = resolve(import.meta.dirname, '../../../../.dev-snapshots/trailer');
const FFMPEG = existsSync('/opt/homebrew/bin/ffmpeg') ? '/opt/homebrew/bin/ffmpeg' : 'ffmpeg';
const DRAFT = process.argv.includes('--borrador');

function arg(name: string): string | undefined {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 ? process.argv[i + 1] : undefined;
}

/** The music: as asked, else the full piece, else the newest mock-up. */
function music(): string | null {
  const asked = arg('musica');
  if (asked) return join(OUT, asked);
  if (existsSync(join(OUT, 'musica/musica.wav'))) return join(OUT, 'musica/musica.wav');
  const mocks = existsSync(join(OUT, 'musica')) ? readdirSync(join(OUT, 'musica')).filter((f) => /^maqueta.*\.wav$/.test(f)) : [];
  return mocks.length ? join(OUT, 'musica', mocks.sort().at(-1)!) : null;
}

function run(args: string[]): string {
  const r = spawnSync(FFMPEG, ['-hide_banner', '-y', ...args], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (r.status !== 0) throw new Error(`ffmpeg: ${r.stderr.split('\n').slice(-12).join('\n')}`);
  return r.stderr;
}

const inputs: string[] = [];
const video: string[] = [];
const missing: string[] = [];
const empty: string[] = [];
let n = 0;
for (const p of SCRIPT) {
  const seconds = p.to - p.from;
  // A real take, else the animatic's empty one (trailer.html?todas&vacias), else black.
  const clip = [join(OUT, 'tomas', `${p.shot}.mp4`), join(OUT, 'vacias', `${p.shot}.mp4`)].find((f) => existsSync(f));
  if (clip) {
    inputs.push('-i', clip);
    if (clip.includes('/vacias/')) empty.push(p.shot);
  } else {
    missing.push(p.shot);
    inputs.push('-f', 'lavfi', '-t', String(seconds), '-i', 'color=c=black:s=1920x1080:r=60');
  }
  // Exactly the script's length, whatever the take came out at (a short one holds its last frame); 60 fps, 1920×1080.
  video.push(`[${n}:v]fps=60,scale=1920:1080,setsar=1,format=yuv420p,tpad=stop_mode=clone:stop_duration=${seconds},trim=duration=${seconds},setpts=PTS-STARTPTS[v${n}]`);
  n++;
}
video.push(`${SCRIPT.map((_, i) => `[v${i}]`).join('')}concat=n=${SCRIPT.length}:v=1:a=0[vout]`);

// The audio: the music, and each take's sounds delayed to where it plays.
const audio: string[] = [];
const score = music();
let mus = '';
if (score && existsSync(score)) {
  inputs.push('-i', score);
  audio.push(`[${n}:a]apad,atrim=0:${LENGTH},aformat=sample_rates=48000:channel_layouts=stereo[mus]`);
  mus = '[mus]';
  n++;
}
const sfx: string[] = [];
for (const p of SCRIPT) {
  const track = join(OUT, 'tomas', `${p.shot}.wav`);
  if (!existsSync(track)) continue;
  inputs.push('-i', track);
  const ms = Math.round(p.from * 1000);
  audio.push(`[${n}:a]atrim=0:${p.to - p.from},adelay=${ms}|${ms},aformat=sample_rates=48000:channel_layouts=stereo[s${sfx.length}]`);
  sfx.push(`[s${sfx.length}]`);
  n++;
}
if (sfx.length) audio.push(`${sfx.join('')}amix=inputs=${sfx.length}:normalize=0,apad,atrim=0:${LENGTH}[sfx]`);
if (mus && sfx.length) {
  // The music steps back a little under the shots, the screams, the engine.
  audio.push('[sfx]asplit=2[sfxa][sfxb]', `${mus}[sfxb]sidechaincompress=threshold=0.04:ratio=3:attack=8:release=350[duck]`, '[duck][sfxa]amix=inputs=2:normalize=0[mix]');
} else if (mus) audio.push(`${mus}anull[mix]`);
else if (sfx.length) audio.push('[sfx]anull[mix]');
else audio.push(`anullsrc=r=48000:cl=stereo,atrim=0:${LENGTH}[mix]`);

const graph = [...video, ...audio];
const file = join(OUT, DRAFT ? 'trailer-borrador.mp4' : 'trailer.mp4');

// Loudness in two passes: measure the mix, then bring it to −14 LUFS without squashing it.
console.log('Midiendo el sonido…');
const measured = run([...inputs, '-filter_complex', [...audio, '[mix]loudnorm=I=-14:TP=-1:LRA=11:print_format=json[aout]'].join(';'), '-map', '[aout]', '-f', 'null', '-']);
const stats = JSON.parse(measured.slice(measured.lastIndexOf('{'), measured.lastIndexOf('}') + 1)) as Record<string, string>;
const norm = `loudnorm=I=-14:TP=-1:LRA=11:measured_I=${stats.input_i}:measured_TP=${stats.input_tp}:measured_LRA=${stats.input_lra}:measured_thresh=${stats.input_thresh}:offset=${stats.target_offset}:linear=true`;

console.log(`Montando ${SCRIPT.length} tomas${empty.length ? ` (de la animática, en negro con la capa de UI: ${empty.join(', ')})` : ''}${missing.length ? ` (en negro, sin grabar aún: ${missing.join(', ')})` : ''}${score ? ` con ${score.slice(OUT.length + 1)}` : ' sin música'} y ${sfx.length} pistas de sonido…`);
const x264 = DRAFT ? ['-preset', 'veryfast', '-crf', '23'] : ['-preset', 'slow', '-crf', '17'];
run([
  ...inputs,
  '-filter_complex',
  [...graph, `[mix]${norm},aresample=48000[aout]`].join(';'),
  '-map', '[vout]', '-map', '[aout]',
  '-c:v', 'libx264', ...x264, '-pix_fmt', 'yuv420p', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709',
  '-c:a', 'aac', '-b:a', '320k',
  '-t', String(LENGTH), '-movflags', '+faststart', file,
]);
console.log(`Hecho: ${file} (entrada ${stats.input_i} LUFS → −14)`);
