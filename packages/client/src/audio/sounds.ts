import type { Bus } from './engine';
import { hit, type Env, type Synth } from './synth';

// Every sound effect of the game, made in code (no recordings): a recipe per
// sound, rendered once into a few variants (bank.ts) so repeats never sound
// identical. Levels are relative, in dB; the bank evens out loudness first.
//
// Families: weapons (by gun and tier, and alien guns), impacts, explosives,
// steps and actions, deaths, combat stingers, the interface of the case file
// (typewriter keys, carriage bell, rubber stamp, paper, pen), the Oficio's
// trades and tools, the radio and the strategy layer.

export interface SoundDef {
  bus: Bus;
  /** Length of the rendered buffer, in seconds. */
  dur: number;
  build: (s: Synth) => void;
  variants?: number;
  /** Relative level in dB after the bank evens loudness out. */
  level?: number;
  /** How much goes to the shared reverb. */
  reverb?: number;
  /**
   * A looping sound (the sabotage's siren): its period in seconds. The recipe
   * runs LOOP_SEAM longer, and bank.ts loopBuffer fades that tail into the
   * head, so the loop never clicks where it joins.
   */
  loop?: number;
}

/** Seconds a looping recipe runs past its period, to fade across the seam. */
export const LOOP_SEAM = 0.4;

const def = (bus: Bus, dur: number, build: (s: Synth) => void, o: Omit<SoundDef, 'bus' | 'dur' | 'build'> = {}): SoundDef => ({ bus, dur, build, ...o });

/** A looping recipe's envelope: `points` (within one `period`) over and over, to `length` (the recipe's, seam included) and a step past it. */
function repeat(period: number, points: Env, length: number): Env {
  const out: Env = [];
  for (let k = 0; k * period <= length; k++) for (const [t, v] of points) out.push([k * period + t, v]);
  return out;
}

// ---------------------------------------------------------------- weapons

interface GunVoice {
  /** Supersonic crack level. */
  crack: number;
  /** Centre of the body noise, its width (Q) and how long it lasts. */
  body: number;
  bodyQ: number;
  bodyDecay: number;
  /** Low thump start pitch and length. */
  thump: number;
  thumpDecay: number;
  /** Outdoor tail: length and tone. */
  tail: number;
  tailTone: number;
  drive: number;
  /** Echo off the buildings around. */
  echo?: number;
}

const GUNS: Record<'rifle' | 'shotgun' | 'sniper' | 'cannon' | 'pistol', GunVoice> = {
  rifle: { crack: 0.7, body: 1700, bodyQ: 0.9, bodyDecay: 0.09, thump: 150, thumpDecay: 0.08, tail: 0.5, tailTone: 1400, drive: 2.2, echo: 0.16 },
  shotgun: { crack: 0.5, body: 800, bodyQ: 0.6, bodyDecay: 0.16, thump: 115, thumpDecay: 0.15, tail: 0.75, tailTone: 1100, drive: 3, echo: 0.2 },
  sniper: { crack: 1.1, body: 1400, bodyQ: 0.8, bodyDecay: 0.11, thump: 165, thumpDecay: 0.12, tail: 1.3, tailTone: 1600, drive: 2.6, echo: 0.27 },
  cannon: { crack: 0.6, body: 950, bodyQ: 0.8, bodyDecay: 0.12, thump: 120, thumpDecay: 0.12, tail: 0.6, tailTone: 1200, drive: 2.8, echo: 0.18 },
  pistol: { crack: 0.5, body: 2200, bodyQ: 1.1, bodyDecay: 0.06, thump: 210, thumpDecay: 0.06, tail: 0.35, tailTone: 1700, drive: 1.8 },
};

/** A powder shot: crack, body, low thump and the tail rolling off the streets. */
function ballistic(s: Synth, g: GunVoice, level = 1): void {
  const mix = s.ctx.createGain();
  s.amp(s.filter(s.noise('white'), 'highpass', s.vary(2600), 0.7), hit(0, 0.0006, 0.028, g.crack)).connect(mix);
  s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(g.body), g.bodyQ), hit(0, 0.001, s.vary(g.bodyDecay), 1)).connect(mix);
  const thump = s.osc('sine', [
    [0, s.vary(g.thump)],
    [g.thumpDecay, g.thump * 0.35],
  ]);
  s.amp(thump, hit(0, 0.002, g.thumpDecay, 1.3)).connect(mix);
  const tail = s.filter(s.noise('pink'), 'lowpass', [
    [0, g.tailTone],
    [g.tail, g.tailTone * 0.3],
  ]);
  s.amp(tail, [
    [0, 0],
    [0.012, 0.32],
    [g.tail, 0.0002],
  ]).connect(mix);
  let out: AudioNode = s.shape(mix, g.drive);
  if (g.echo) out = s.echo(out, s.vary(g.echo, 0.15), 0.25, 0.3, 1600);
  s.to(out, level);
}

/** Magnetic rounds: an electric snap falling fast, and the rail ringing for a moment. */
function magnetic(s: Synth, level = 1, low = 260): void {
  const zap = s.filter(
    s.osc('sawtooth', [
      [0, s.vary(3400)],
      [0.07, low],
    ]),
    'bandpass',
    1700,
    1.4,
  );
  s.to(s.amp(zap, hit(0, 0.001, 0.09, level)));
  const buzz = s.filter(s.osc('square', s.vary(118)), 'lowpass', 900);
  s.to(s.amp(buzz, hit(0, 0.002, 0.07, level * 0.35)));
  s.to(s.metal(0.002, [2150, 3420, 5310].map((f) => s.vary(f, 0.04)), 0.22, level * 0.22));
}

/** Plasma: a falling, wobbling bolt with a hiss and a soft low whoomp. */
function plasma(s: Synth, level = 1, top = 1150, bottom = 150): void {
  const bolt = s.osc('sine', [
    [0, s.vary(top)],
    [0.2, bottom],
  ]);
  const mod = s.osc('sine', s.vary(70));
  const depth = s.ctx.createGain();
  depth.gain.value = 260;
  mod.connect(depth).connect(bolt.frequency);
  s.to(s.amp(s.shape(bolt, 1.5), hit(0, 0.002, 0.22, level)));
  s.to(s.amp(s.filter(s.noise('white'), 'highpass', 4000), hit(0, 0.003, 0.2, level * 0.35)));
  const whoomp = s.osc('sine', [
    [0, 95],
    [0.22, 40],
  ]);
  s.to(s.amp(whoomp, hit(0, 0.005, 0.22, level * 0.9)));
}

function soldierGun(gun: keyof typeof GUNS, tier: 1 | 2 | 3): SoundDef {
  const g = GUNS[gun];
  const big = gun === 'sniper' || gun === 'shotgun';
  return def(
    'sfx',
    g.tail + 0.5,
    (s) => {
      if (tier === 1) ballistic(s, g);
      else if (tier === 2) {
        ballistic(s, { ...g, crack: g.crack * 0.4, body: g.body * 1.3 }, 0.65);
        magnetic(s, big ? 1.1 : 0.9);
      } else {
        ballistic(s, { ...g, crack: 0.15 }, 0.3);
        plasma(s, big ? 1.1 : 0.9, gun === 'sniper' ? 1500 : 1150, gun === 'shotgun' ? 110 : 150);
      }
      // The shotgun racks a new shell after each shot.
      if (gun === 'shotgun') {
        for (const at of [0.38, 0.5]) s.to(s.amp(s.filter(s.noise(), 'bandpass', s.vary(2400), 3), hit(at, 0.001, 0.03, 0.3)));
      }
    },
    { level: gun === 'sniper' ? 1 : gun === 'pistol' ? -3 : 0, reverb: gun === 'sniper' ? 0.35 : 0.18 },
  );
}

/** ADVENT guns: magnetic, harsher and lower than ours, with a gritty buzz. */
function adventGun(level = 1): (s: Synth) => void {
  return (s) => {
    ballistic(s, { ...GUNS.rifle, crack: 0.25, body: 1200, drive: 3.4 }, 0.55 * level);
    magnetic(s, level, 180);
    const grit = s.filter(s.osc('sawtooth', s.vary(76)), 'lowpass', 1400);
    s.to(s.amp(s.shape(grit, 4), hit(0, 0.002, 0.1, 0.5 * level)));
  };
}

// ---------------------------------------------------------------- the Oficio

// The Oficio's trades and tools (engine content.ts): machete, flares,
// firecrackers, petrol, crossbow, bandages, syringes and the defibrillator.
// The ids keep the old «song.*» names, so the battle (game.ts) plays them as it did.

/** `input` pulsing in level `rate` times a second (a spinning bottle, a sputtering flame, a shock's buzz). */
function pulse(s: Synth, input: AudioNode, rate: number, depth: number, at = 0, dur = s.dur - at): GainNode {
  const g = s.ctx.createGain();
  g.gain.value = 1 - depth;
  const lfo = s.osc('sine', rate, at, dur);
  const d = s.ctx.createGain();
  d.gain.value = depth;
  lfo.connect(d).connect(g.gain);
  input.connect(g);
  return g;
}

/** Something burning: the low roar of the flames and their crackle. */
function flames(s: Synth, at: number, dur: number, level = 1): void {
  const roar = s.filter(s.noise('brown', at, dur), 'lowpass', s.vary(700), 0.7);
  s.to(s.amp(pulse(s, roar, s.vary(5), 0.3, at, dur), [[at, 0], [at + 0.15, level], [at + dur * 0.5, level * 0.6], [at + dur, 0.0005]]));
  s.to(s.filter(s.grains(at, at + dur, Math.round(dur * 45), 0.004), 'bandpass', s.vary(2600), 0.8), level * 0.9);
}

/** Petrol catching: a deep whoomp that opens up. */
function whoomp(s: Synth, at: number, level = 1): void {
  const body = s.filter(s.noise('brown', at, 1.2), 'lowpass', [[at, 180], [at + 0.25, 1400], [at + 1, 500]], 0.8);
  s.to(s.shape(s.amp(body, [[at, 0], [at + 0.07, 1.4 * level], [at + 1.1, 0.0005]]), 1.8));
  s.to(s.amp(s.osc('sine', [[at, 70], [at + 0.5, 32]]), hit(at, 0.03, 0.5, level)));
}

/** A bottle breaking: the snap, a dull knock and the shards scattering. */
function smash(s: Synth, at: number, level = 1): void {
  s.to(s.amp(s.filter(s.noise('white', at, 0.1), 'highpass', 2500), hit(at, 0.0005, 0.03, level)));
  s.to(s.amp(s.osc('sine', [[at, 160], [at + 0.08, 70]]), hit(at, 0.002, 0.08, level * 0.6)));
  for (const f of [3100, 4600, 6200, 8100]) s.to(s.filter(s.grains(at + 0.005, at + 0.4, 9, 0.018), 'bandpass', s.vary(f, 0.1), 10), level * 2.2);
}

/** A firecracker: a sharp crack with a little body. */
function banger(s: Synth, at: number, level: number, out: AudioNode): void {
  s.shape(s.amp(s.filter(s.noise('white', at, 0.08), 'highpass', s.vary(1800)), hit(at, 0.0004, s.vary(0.025), level)), 3).connect(out);
  s.amp(s.filter(s.noise('pink', at, 0.1), 'bandpass', s.vary(700), 1.2), hit(at, 0.001, 0.05, level * 0.5)).connect(out);
}

// ---------------------------------------------------------------- the dead's throats

/**
 * A human throat torn ragged (the Chillón): the voice and its growl an octave
 * down, the pitch jittering as it strains, through the formants of a scream
 * opening from «a» towards «e», breath rasping through, all distorted.
 * `rise` is the pitch and `level` the loudness: envelopes in seconds from the sound's start.
 */
function scream(s: Synth, at: number, rise: [number, number][], level: [number, number][]): void {
  const voice = s.osc('sawtooth', rise, at);
  const fry = s.osc('sawtooth', rise.map(([t, f]): [number, number] => [t, f / 2]), at);
  const jitter = s.ctx.createGain();
  jitter.gain.value = s.vary(110);
  s.filter(s.noise('brown', at), 'lowpass', 40).connect(jitter);
  jitter.connect(voice.detune);
  jitter.connect(fry.detune);
  const throat = s.ctx.createGain();
  voice.connect(throat);
  s.amp(fry, 0.5).connect(throat);
  const sum = s.ctx.createGain();
  s.amp(s.filter(throat, 'bandpass', [[at, 780], [at + 0.4, 950]], 4), 1).connect(sum);
  s.amp(s.filter(throat, 'bandpass', [[at, 1250], [at + 0.4, 1900]], 5), 0.7).connect(sum);
  s.amp(s.filter(throat, 'bandpass', s.vary(2900), 6), 0.45).connect(sum);
  s.amp(s.filter(s.noise('white', at), 'bandpass', s.vary(3200), 1.2), 0.35).connect(sum);
  const torn = s.filter(s.shape(pulse(s, sum, s.vary(33), 0.45, at), 5), 'lowpass', 6000);
  s.to(s.amp(torn, level));
}

/** A wet gurgle: breath bubbling through a throat full of something, the voice under it sinking. */
function gurgle(s: Synth, at: number, dur: number, level = 1): void {
  const wet = s.filter(s.grains(at, at + dur, Math.round(dur * 60), 0.012, false), 'bandpass', s.vary(450), 1.2);
  s.to(s.amp(wet, [[at, 0], [at + 0.04, level * 4], [at + dur * 0.7, level * 3], [at + dur, 0.001]]));
  const voice = s.filter(s.osc('sawtooth', [[at, s.vary(120)], [at + dur, 70]], at, dur), 'bandpass', 500, 3);
  s.to(s.amp(pulse(s, voice, s.vary(17), 0.8, at, dur), [[at, 0], [at + 0.04, level * 1.2], [at + dur * 0.7, level * 0.9], [at + dur, 0.001]]));
}

const OFICIO: Record<string, SoundDef> = {
  // The cue of the Oficio's moves (Saltar por la ventana, Encaramarse, Lluvia de flechas): kit shifting, a buckle.
  'song.voice': def('sfx', 0.6, (s) => {
    s.to(s.amp(s.filter(s.grains(0, 0.4, 26, 0.025, false), 'bandpass', s.vary(1400), 0.8), [[0, 0], [0.05, 1.2], [0.45, 0.001]]));
    s.to(s.metal(s.r(0.12, 0.2), [s.vary(2600), s.vary(3900), s.vary(5200)], 0.08, 0.3));
  }, { level: -5 }),
  // Bengala: the striker scraped, the flare catching with a pop, and its hiss as it flies, sputtering.
  'song.bolt': def('sfx', 1.3, (s) => {
    s.to(s.filter(s.grains(0, 0.09, 22, 0.006, false), 'bandpass', s.vary(3200), 1.2), 1.2);
    s.to(s.amp(s.osc('sine', [[0.1, 340], [0.16, 120]]), hit(0.1, 0.002, 0.07, 0.6)));
    s.to(s.amp(s.filter(s.noise('white', 0.1, 0.1), 'highpass', 900), hit(0.1, 0.001, 0.04, 0.7)));
    const hiss = s.filter(s.noise('white', 0.1), 'bandpass', [[0.1, 2800], [1.2, 4200]], 0.9);
    s.to(s.amp(pulse(s, hiss, s.vary(23), 0.3, 0.1), [[0.1, 0], [0.16, 0.9], [0.6, 0.6], [1.25, 0.001]]));
  }, { reverb: 0.2 }),
  // Cóctel molotov: the bottle spinning through the air, smashing, and the petrol going up in flames.
  'song.fire': def('sfx', 2.6, (s) => {
    const spin = s.filter(s.noise('pink', 0, 0.75), 'bandpass', [[0, 600], [0.7, 1100]], 1.5);
    s.to(s.amp(pulse(s, spin, s.vary(9), 0.5, 0, 0.75), [[0, 0], [0.15, 0.5], [0.65, 0.4], [0.72, 0.001]]));
    smash(s, 0.7);
    whoomp(s, 0.74);
    flames(s, 0.8, 1.7, 0.6);
  }, { level: 2, reverb: 0.35 }),
  // Flecha perforante, Lluvia de flechas: the crossbow's trigger, the string slapping home and the bolt hissing off.
  'song.arrow': def('sfx', 0.7, (s) => {
    s.to(s.amp(s.filter(s.noise('white', 0, 0.05), 'bandpass', s.vary(3000), 4), hit(0, 0.0005, 0.012, 0.6)));
    const string = s.filter(s.osc('sawtooth', [[0.012, s.vary(190)], [0.2, 120]], 0.012, 0.3), 'lowpass', [[0.012, 2400], [0.2, 500]]);
    s.to(s.amp(string, hit(0.012, 0.001, 0.18, 0.9)));
    s.to(s.amp(s.filter(s.noise('pink', 0.012, 0.1), 'bandpass', s.vary(900), 1.5), hit(0.012, 0.001, 0.05, 1)));
    s.to(s.amp(s.filter(s.noise('white', 0.02, 0.4), 'bandpass', [[0.02, 2600], [0.4, 1200]], 2), [[0.02, 0], [0.06, 0.35], [0.4, 0.001]]));
  }, { level: -1, reverb: 0.15 }),
  // Saltar por la ventana, Encaramarse: boots pushing off and the body going over...
  'song.vanish': def('sfx', 0.6, (s) => {
    s.to(s.filter(s.grains(0, 0.08, 12, 0.012, false), 'bandpass', s.vary(1100), 0.9), 1.2);
    s.to(s.amp(s.osc('sine', [[0, 110], [0.06, 60]]), hit(0, 0.002, 0.07, 0.6)));
    s.to(s.amp(s.filter(s.noise('pink', 0.05), 'bandpass', [[0.05, 500], [0.45, 1400]], 1), [[0.05, 0], [0.2, 0.5], [0.5, 0.001]]));
    s.to(s.amp(s.filter(s.grains(0.06, 0.4, 14, 0.02, false), 'bandpass', 1600, 0.7), [[0.06, 0], [0.1, 0.8], [0.45, 0.001]]));
  }, { level: -3 }),
  // ...and landing: a heavy thud, grit under the boots and the kit rattling.
  'song.appear': def('sfx', 0.8, (s) => {
    s.to(s.amp(s.osc('sine', [[0, 95], [0.15, 42]]), hit(0, 0.003, 0.18, 1)));
    s.to(s.amp(s.filter(s.noise('pink', 0, 0.3), 'lowpass', 900), hit(0, 0.002, 0.08, 0.9)));
    s.to(s.filter(s.grains(0.01, 0.25, 30, 0.006), 'bandpass', s.vary(2400), 0.9), 1);
    for (const at of [s.r(0.06, 0.12), s.r(0.16, 0.26)]) s.to(s.metal(at, [s.vary(2200), s.vary(3300)], 0.06, 0.25));
  }, { level: -2 }),
  // Petardos: the fuse fizzing, then the string of crackers going off and echoing down the street.
  'song.dome': def('sfx', 2.8, (s) => {
    s.to(s.amp(s.filter(s.noise('white', 0, 0.6), 'bandpass', s.vary(4200), 1.5), [[0, 0], [0.05, 0.3], [0.5, 0.25], [0.55, 0.001]]));
    s.to(s.filter(s.grains(0, 0.55, 30, 0.003, false), 'highpass', 3000), 0.5);
    // Close together at first, then the last few straggling.
    const bangs = s.ctx.createGain();
    let at = 0.55;
    for (let i = 0; i < 16; i++) {
      banger(s, at, s.r(0.6, 1), bangs);
      at += i < 11 ? s.r(0.04, 0.11) : s.r(0.1, 0.22);
    }
    s.to(s.echo(bangs, 0.19, 0.3, 0.35, 2200));
  }, { level: 2, reverb: 0.35 }),
  // El bidón: a kick against a steel drum, petrol sloshing, then the drum going up in a ball of fire...
  'song.storm': def('sfx', 2.4, (s) => {
    s.to(s.metal(0, [s.vary(165), s.vary(262), s.vary(388), s.vary(611)], 0.9, 0.7));
    s.to(s.amp(s.filter(s.noise('pink', 0, 0.1), 'lowpass', 600), hit(0, 0.002, 0.05, 0.8)));
    s.to(s.amp(pulse(s, s.filter(s.noise('brown', 0.05, 0.4), 'lowpass', 500), 7, 0.6, 0.05, 0.4), [[0.05, 0], [0.12, 0.5], [0.4, 0.001]]));
    whoomp(s, 0.28, 1.2);
    flames(s, 0.35, 1.9, 0.8);
  }, { level: 2, reverb: 0.4 }),
  // ...and the fire reaching each enemy: a gust of flame and its crackle.
  'song.thunder': def('sfx', 1.4, (s) => {
    whoomp(s, 0, 0.9);
    flames(s, 0.05, 1.2, 0.5);
  }, { reverb: 0.3 }),
  // Escudo antidisturbios, Arenga, ¡Aguantad!: two blows of a baton on a riot shield, to rally the others.
  'song.aura': def('sfx', 1.1, (s) => {
    for (const [at, l] of [
      [0, 1],
      [0.3, 0.85],
    ] as const) {
      // The shield's hollow plastic boom and its rim rattling.
      s.to(s.amp(s.filter(s.noise('pink', at, 0.3), 'bandpass', s.vary(420), 2.5), hit(at, 0.001, 0.14, l)));
      s.to(s.amp(s.osc('sine', [[at, s.vary(150)], [at + 0.12, 80]]), hit(at, 0.002, 0.14, l * 0.8)));
      s.to(s.metal(at, [s.vary(1150), s.vary(1780)], 0.07, l * 0.25));
      s.to(s.filter(s.grains(at + 0.01, at + 0.12, 6, 0.006), 'bandpass', 2600, 1), l * 0.5);
    }
  }, { level: -1, reverb: 0.3 }),
  // Vendaje rápido (and the Desfibrilador with no one down): a strip torn off the roll, the gauze wound round, the tape pressed down.
  'song.heal': def('sfx', 1.1, (s) => {
    const tear = s.filter(s.grains(0, 0.28, 90, 0.004, false), 'bandpass', [[0, 1800], [0.28, 3400]], 0.9);
    s.to(s.amp(tear, [[0, 0], [0.02, 1.4], [0.24, 1], [0.3, 0.001]]));
    s.to(s.amp(s.filter(s.noise('white', 0, 0.3), 'bandpass', 2600, 1.2), [[0, 0], [0.03, 0.3], [0.28, 0.001]]));
    s.to(s.amp(s.filter(s.noise('pink', 0.35, 0.5), 'bandpass', [[0.35, 900], [0.8, 1400]], 1), [[0.35, 0], [0.45, 0.35], [0.85, 0.001]]));
    s.to(s.amp(s.filter(s.noise('pink', 0.9, 0.1), 'lowpass', 1200), hit(0.9, 0.002, 0.05, 0.5)));
  }, { level: -3 }),
  // Desfibrilador: the charge whining up, the ready beeps, the shock (a thump, a crack, a buzz) and the monitor beeping again.
  'song.revive': def('sfx', 2.6, (s) => {
    s.to(s.amp(s.osc('sine', [[0, 700], [0.75, 3600]]), [[0, 0], [0.1, 0.25], [0.75, 0.3], [0.8, 0.001]]));
    s.to(s.amp(s.osc('sine', [[0, 1400], [0.75, 7200]]), [[0, 0], [0.1, 0.06], [0.75, 0.07], [0.8, 0.001]]));
    for (const at of [0.8, 0.92]) s.to(s.amp(s.osc('square', 1760, at, 0.07), hit(at, 0.003, 0.06, 0.18)));
    const at = 1.1;
    s.to(s.amp(s.osc('sine', [[at, 110], [at + 0.2, 45]]), hit(at, 0.002, 0.22, 1.2)));
    s.to(s.shape(s.amp(s.filter(s.noise('white', at, 0.15), 'highpass', 1500), hit(at, 0.0005, 0.05, 1)), 3));
    s.to(s.amp(pulse(s, s.filter(s.noise('white', at, 0.2), 'bandpass', 3000, 1.2), 60, 1, at, 0.2), hit(at, 0.002, 0.15, 0.5)));
    for (const beat of [1.75, 2.3]) s.to(s.amp(s.osc('sine', 1046, beat, 0.15), hit(beat, 0.005, 0.12, 0.25)));
  }, { level: 1, reverb: 0.25 }),
  // Sedante: the syringe's cap pulled off, a squirt to clear it and the plunger pressed home.
  'song.cleanse': def('sfx', 0.8, (s) => {
    s.to(s.amp(s.filter(s.noise('white', 0, 0.05), 'bandpass', s.vary(2400), 3), hit(0, 0.0005, 0.02, 0.8)));
    s.to(s.amp(s.osc('sine', [[0, 900], [0.03, 500]]), hit(0, 0.001, 0.03, 0.3)));
    s.to(s.amp(s.filter(s.noise('white', 0.14, 0.12), 'highpass', 5000), [[0.14, 0], [0.16, 0.4], [0.24, 0.001]]));
    s.to(s.amp(s.filter(s.noise('white', 0.4, 0.05), 'bandpass', s.vary(3400), 5), hit(0.4, 0.0005, 0.015, 0.7)));
    s.to(s.amp(s.filter(s.noise('pink', 0.42, 0.25), 'bandpass', 1200, 2), [[0.42, 0], [0.45, 0.2], [0.65, 0.001]]));
  }, { level: -5 }),
  // Rastreo: down on one knee, the scope's dial clicked round to the range, a breath held.
  'song.mark': def('sfx', 0.9, (s) => {
    s.to(s.amp(s.filter(s.grains(0, 0.2, 12, 0.025, false), 'bandpass', 1200, 0.7), [[0, 0], [0.04, 0.9], [0.25, 0.001]]));
    for (let i = 0; i < 3; i++) {
      const at = 0.3 + i * 0.09;
      s.to(s.amp(s.filter(s.noise('white', at, 0.03), 'bandpass', s.vary(4200, 0.04), 6), hit(at, 0.0004, 0.01, 0.8)));
      s.to(s.metal(at, [s.vary(5300, 0.03)], 0.02, 0.2));
    }
    s.to(s.amp(s.filter(s.noise('pink', 0.6, 0.3), 'bandpass', 800, 0.8), [[0.6, 0], [0.7, 0.12], [0.88, 0.001]]));
  }, { level: -6 }),
  // Machetazo: a heavy machete swung through the air and biting home. Nothing plays it yet: the Tajo has its own sound.
  'song.blade': def('sfx', 0.7, (s) => {
    s.to(s.amp(s.filter(s.noise('white', 0, 0.25), 'bandpass', [[0, 600], [0.16, 2400]], 1.8), [[0, 0], [0.13, 0.8], [0.17, 0.001]]));
    const at = 0.17;
    s.to(s.amp(s.osc('sine', [[at, 140], [at + 0.1, 60]]), hit(at, 0.002, 0.1, 1)));
    s.to(s.amp(s.filter(s.noise('pink', at, 0.15), 'bandpass', s.vary(700), 1.2), hit(at, 0.001, 0.07, 1)));
    s.to(s.metal(at, [s.vary(2100), s.vary(3200), s.vary(4700)], 0.18, 0.25));
  }, { reverb: 0.15 }),
};

// ---------------------------------------------------------------- the list

export const SOUNDS: Record<string, SoundDef> = {
  ...OFICIO,
  ...Object.fromEntries(
    (Object.keys(GUNS) as (keyof typeof GUNS)[]).flatMap((gun) => ([1, 2, 3] as const).map((tier) => [`shot.${gun}.${tier}`, soldierGun(gun, tier)])),
  ),
  'shot.advent': def('sfx', 0.9, adventGun(), { reverb: 0.15 }),
  'shot.mec': def(
    'sfx',
    1,
    (s) => {
      adventGun(1.2)(s);
      ballistic(s, { ...GUNS.cannon, crack: 0.2 }, 0.7);
      s.to(s.metal(0.01, [620, 1130, 1790], 0.3, 0.25));
    },
    { level: 1, reverb: 0.2 },
  ),
  // The sectoid's pistol: a psionic bolt rising then falling, with a glassy shimmer.
  'shot.sectoid': def(
    'sfx',
    0.6,
    (s) => {
      const o = s.osc('triangle', [
        [0, s.vary(600)],
        [0.03, 1300],
        [0.3, 240],
      ]);
      const vib = s.osc('sine', 32);
      const d = s.ctx.createGain();
      d.gain.value = 60;
      vib.connect(d).connect(o.frequency);
      s.to(s.amp(o, hit(0, 0.004, 0.3, 0.9)));
      s.to(s.metal(0.005, [3100, 4250, 5600], 0.35, 0.2));
      plasma(s, 0.4, 900, 200);
    },
    { reverb: 0.3 },
  ),
  // The xenoid's emitter: a ring-modulated beam sweeping down.
  'shot.xenoid': def(
    'sfx',
    0.7,
    (s) => {
      const beam = s.filter(s.noise('white'), 'bandpass', [
        [0, 4200],
        [0.28, 700],
      ], 4);
      const carrier = s.osc('sawtooth', s.vary(220));
      const ring = s.ctx.createGain();
      ring.gain.value = 0;
      const mod = s.osc('sine', 47);
      mod.connect(ring.gain);
      carrier.connect(ring);
      s.to(s.amp(beam, hit(0, 0.004, 0.3, 1)));
      s.to(s.amp(s.filter(ring, 'lowpass', 1800), hit(0, 0.004, 0.3, 0.6)));
    },
    { reverb: 0.25 },
  ),

  // ------------------------------------------------------------ impacts
  'impact.flesh': def('sfx', 0.3, (s) => {
    s.to(
      s.amp(
        s.osc('sine', [
          [0, s.vary(95)],
          [0.12, 50],
        ]),
        hit(0, 0.003, 0.12, 1),
      ),
    );
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 900), hit(0, 0.002, 0.08, 0.7)));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(650), 2), hit(0.01, 0.004, 0.06, 0.4)));
  }),
  'impact.armor': def('sfx', 0.5, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 1800), hit(0, 0.0005, 0.02, 0.8)));
    s.to(s.metal(0, [1350, 2240, 3170, 4520].map((f) => s.vary(f, 0.06)), 0.3, 0.7));
    s.to(s.amp(s.osc('sine', [[0, 140], [0.1, 70]]), hit(0, 0.002, 0.1, 0.7)));
  }),
  'impact.robot': def(
    'sfx',
    0.7,
    (s) => {
      s.to(s.amp(s.filter(s.noise('white'), 'highpass', 1200), hit(0, 0.0005, 0.03, 0.8)));
      s.to(s.metal(0, [480, 910, 1390, 2270].map((f) => s.vary(f, 0.06)), 0.45, 0.8));
      s.to(s.amp(s.filter(s.grains(0, 0.25, 12, 0.008), 'bandpass', 5200, 1.5), hit(0, 0.001, 0.3, 0.6)));
    },
    { reverb: 0.15 },
  ),
  // A miss: the round whizzes past, or ricochets off something.
  'impact.miss': def(
    'sfx',
    0.4,
    (s) => {
      if (s.rnd() < 0.5) {
        s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 3600], [0.18, 1300]], 6), hit(0, 0.05, 0.13, 0.9)));
      } else {
        const o = s.osc('sine', [
          [0, s.vary(2900)],
          [0.28, 900],
        ]);
        s.to(s.amp(o, hit(0.01, 0.004, 0.26, 0.45)));
        s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2500), hit(0, 0.001, 0.025, 0.6)));
      }
    },
    { variants: 4, level: -4 },
  ),
  'impact.cover': def('sfx', 0.35, (s) => {
    s.to(s.filter(s.grains(0, 0.12, 9, 0.012), 'bandpass', s.vary(1500), 0.8));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 500), hit(0, 0.002, 0.1, 0.8)));
  }, { level: -3 }),
  // On a critical hit, over the impact: a harder blow than a plain hit, flesh and bone (playtest 18/10 #22:
  // the old bright sting rang like a bell). A deep, heavy thump with some grit to it, a wet slap, the dry crack
  // of a bone and a few splinters after it. Nothing that rings or sings.
  'impact.crit': def('sfx', 0.5, (s) => {
    const thump = s.osc('sine', [[0, s.vary(85)], [0.16, 38]], 0, 0.3);
    s.to(s.shape(s.amp(thump, hit(0, 0.003, 0.2, 1.2)), 1.6));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 450), hit(0, 0.002, 0.14, 1)));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(1100), 1.3), hit(0.004, 0.002, 0.07, 0.75)));
    s.to(s.amp(s.filter(s.noise('white', 0.012, 0.05), 'bandpass', s.vary(2600), 2.5), hit(0.012, 0.0004, 0.018, 0.9)));
    s.to(s.filter(s.grains(0.02, 0.09, 5, 0.004, false), 'bandpass', s.vary(3200), 1.5), 0.5);
  }, { variants: 4, reverb: 0.08 }),
  // The same on a machine (the alarm box, the Acorazado's plates): metal bent in, a dull crunch, no ring.
  'impact.crit.metal': def('sfx', 0.45, (s) => {
    s.to(s.shape(s.amp(s.osc('sine', [[0, s.vary(110)], [0.12, 50]], 0, 0.25), hit(0, 0.002, 0.15, 1)), 1.5));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 700), hit(0, 0.001, 0.12, 0.9)));
    s.to(s.filter(s.grains(0, 0.12, 14, 0.006, false), 'bandpass', s.vary(1400), 0.8), 0.8);
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2500), hit(0, 0.0005, 0.03, 0.6)));
  }, { variants: 3, reverb: 0.1 }),
  'impact.glass': def(
    'sfx',
    0.7,
    (s) => {
      s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2000), hit(0, 0.0005, 0.03, 0.8)));
      for (const f of [3300, 4700, 6100, 7900]) s.to(s.filter(s.grains(0.01, 0.45, 8, 0.02), 'bandpass', s.vary(f, 0.1), 12), 2.5);
    },
    { reverb: 0.15 },
  ),

  // ------------------------------------------------------------ explosives
  explosion: def(
    'sfx',
    2.6,
    (s) => {
      s.to(s.amp(s.filter(s.noise('white'), 'highpass', 1500), hit(0, 0.001, 0.05, 0.8)));
      const boom = s.filter(s.noise('brown'), 'lowpass', [
        [0, 900],
        [1.4, 140],
      ]);
      s.to(s.shape(s.amp(boom, hit(0, 0.006, 1.8, 1.6)), 2.5));
      const sub = s.osc('sine', [
        [0, 72],
        [0.9, 26],
      ]);
      s.to(s.amp(sub, hit(0, 0.008, 1.1, 1.3)));
      // Debris pattering down.
      s.to(s.filter(s.grains(0.15, 1.6, 40, 0.01), 'bandpass', 2600, 0.9), 0.9);
    },
    { level: 3, reverb: 0.4, variants: 3 },
  ),
  // The flashbang: a sharp bang and the ears ringing after it.
  'explosion.flash': def(
    'sfx',
    1.8,
    (s) => {
      s.to(s.shape(s.amp(s.filter(s.noise('white'), 'highpass', 700), hit(0, 0.001, 0.08, 1.4)), 3));
      s.to(s.amp(s.osc('sine', [[0, 110], [0.2, 45]]), hit(0, 0.003, 0.2, 1)));
      s.to(s.amp(s.osc('sine', 3800), [[0.05, 0], [0.12, 0.12], [1.7, 0.0001]]));
    },
    { level: 1, reverb: 0.3 },
  ),
  smoke: def('sfx', 1.8, (s) => {
    s.to(s.amp(s.osc('sine', [[0, 160], [0.08, 60]]), hit(0, 0.002, 0.08, 0.6)));
    s.to(
      s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 2400], [1.6, 4800]], 0.8), [
        [0.04, 0],
        [0.25, 0.6],
        [1.7, 0.001],
      ]),
    );
  }),
  // A rocket leaving: a rising whoosh over the motor's roar.
  missile: def('sfx', 1, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 500], [0.6, 2800]], 1.5), [[0, 0], [0.08, 0.8], [0.9, 0.001]]));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 1500), [[0, 0], [0.05, 0.9], [0.8, 0.001]]));
  }),
  // The stone (docs/FASE10.md §4): thrown to make the dead look somewhere else, so where it lands must carry.
  // As it leaves the hand: a short swish, lighter than a grenade's.
  'stone.throw': def('sfx', 0.3, (s) => {
    const air = s.filter(s.noise('white'), 'bandpass', [[0, s.vary(900)], [0.12, s.vary(2600)], [0.26, 1500]], 1.4);
    s.to(s.amp(air, [[0, 0], [0.06, 0.55], [0.26, 0.001]]));
  }, { level: -7 }),
  // On hard ground (street, factory floor): a dry knock, two bounces and a little grit.
  'stone.land': def('sfx', 0.55, (s) => {
    for (const [at, l] of [[0, 1], [s.vary(0.12), 0.45], [s.vary(0.21), 0.2]] as const) {
      const knock = s.osc('sine', [[at, s.vary(260)], [at + 0.05, s.vary(160)]], at, 0.12);
      s.to(s.amp(knock, hit(at, 0.001, 0.06, l * 0.8)));
      const click = s.filter(s.noise('white', at, 0.06), 'highpass', 1800, 0.7);
      s.to(s.amp(click, hit(at, 0.0005, 0.025, l)));
    }
    s.to(s.filter(s.grains(0.02, 0.32, 9, 0.006), 'highpass', 2500, 0.7), 0.25);
  }, { variants: 4, reverb: 0.25 }),
  // On earth (out of town): a thud and a spray of soil.
  'stone.thud': def('sfx', 0.4, (s) => {
    const body = s.osc('sine', [[0, s.vary(130)], [0.08, s.vary(70)]], 0, 0.25);
    s.to(s.amp(body, hit(0, 0.002, 0.14, 0.9)));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 700), hit(0, 0.002, 0.1, 0.8)));
    s.to(s.filter(s.grains(0.01, 0.2, 7, 0.01), 'bandpass', 1400, 0.8), 0.2);
  }, { variants: 4, reverb: 0.2 }),
  // On whoever it strikes: blunt, with no ring to it.
  'stone.hit': def('sfx', 0.3, (s) => {
    const body = s.osc('sine', [[0, s.vary(160)], [0.06, s.vary(95)]], 0, 0.2);
    s.to(s.amp(body, hit(0, 0.001, 0.16, 1)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 1100), hit(0, 0.001, 0.1, 0.7)));
  }, { variants: 3, reverb: 0.1 }),
  // A siege (docs/FASE12.md §10): one of the dead beating on what keeps it out. On a door: a dull blow on the
  // boards, the panel booming low, and the door rattling in its frame and at the latch.
  'smash.door': def('sfx', 0.6, (s) => {
    const body = s.osc('sine', [[0, s.vary(115)], [0.12, s.vary(62)]], 0, 0.35);
    s.to(s.amp(body, hit(0, 0.002, 0.22, 1)));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(420), 1.6), hit(0, 0.001, 0.09, 0.9)));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 600), hit(0, 0.002, 0.16, 0.8)));
    s.to(s.filter(s.grains(0.03, 0.3, 8, 0.008), 'bandpass', s.vary(1700), 1.1), 0.35);
    s.to(s.metal(0.02, [s.vary(1850), s.vary(2900)], 0.07, 0.12));
  }, { variants: 4, reverb: 0.2 }),
  // On a barricade: boards and sheet metal, the sheets ringing hollow and clattering against the boards.
  'smash.barricade': def('sfx', 0.7, (s) => {
    s.to(s.amp(s.osc('sine', [[0, s.vary(140)], [0.1, s.vary(75)]], 0, 0.3), hit(0, 0.002, 0.16, 0.8)));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(650), 1.2), hit(0, 0.001, 0.07, 0.8)));
    s.to(s.metal(0, [310, 520, 785, 1130, 1690].map((f) => s.vary(f, 0.07)), 0.45, 0.55));
    s.to(s.filter(s.grains(0.02, 0.4, 12, 0.01), 'bandpass', s.vary(2300), 0.9), 0.4);
  }, { variants: 4, reverb: 0.2 }),
  // A door giving way: the wood splitting with a crack and a tearing that sinks, splinters, and the door
  // slapping down flat.
  'smash.give.door': def('sfx', 1.2, (s) => {
    s.to(s.shape(s.amp(s.filter(s.noise('white'), 'highpass', 1400), hit(0, 0.0005, 0.05, 1)), 2));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', [[0, 1600], [0.35, 500]], 2.2), [[0, 0], [0.02, 0.7], [0.38, 0.001]]));
    s.to(s.filter(s.grains(0.01, 0.45, 26, 0.006), 'highpass', 2200, 0.7), 0.5);
    const down = s.vary(0.42, 0.1);
    s.to(s.amp(s.osc('sine', [[down, 95], [down + 0.15, 45]], down, 0.4), hit(down, 0.002, 0.25, 1.1)));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 900), hit(down, 0.001, 0.2, 0.9)));
    s.to(s.filter(s.grains(down + 0.03, down + 0.6, 14, 0.012), 'bandpass', 1200, 0.8), 0.3);
  }, { variants: 3, reverb: 0.3, level: 1 }),
  // A barricade giving way: the same crack, and its sheets and boards tumbling over a second.
  'smash.give.barricade': def('sfx', 1.3, (s) => {
    s.to(s.shape(s.amp(s.filter(s.noise('white'), 'highpass', 1400), hit(0, 0.0005, 0.05, 1)), 2));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', [[0, 1400], [0.3, 450]], 2), [[0, 0], [0.02, 0.6], [0.32, 0.001]]));
    for (const at of [s.r(0.12, 0.2), s.r(0.3, 0.45), s.r(0.55, 0.75)]) {
      s.to(s.metal(at, [s.vary(380), s.vary(610), s.vary(940), s.vary(1450)], 0.3, 0.4));
      s.to(s.amp(s.osc('sine', [[at, s.vary(170)], [at + 0.06, s.vary(90)]], at, 0.15), hit(at, 0.001, 0.08, 0.6)));
    }
    s.to(s.filter(s.grains(0.05, 1.1, 30, 0.01), 'bandpass', 1800, 0.8), 0.45);
  }, { variants: 3, reverb: 0.3, level: 1 }),
  'grenade.throw': def('sfx', 0.45, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 700], [0.25, 1300]], 1.2), [[0, 0], [0.12, 0.6], [0.35, 0.001]]));
    s.to(s.metal(0, [3800, 5100], 0.06, 0.15));
  }),
  // The grenade landing: two clinks on the ground.
  'grenade.land': def('sfx', 0.5, (s) => {
    for (const [at, l] of [
      [0, 0.6],
      [0.17, 0.35],
    ] as const) {
      s.to(s.metal(at, [2300, 3650, 4900].map((f) => s.vary(f, 0.05)), 0.12, l));
      s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 800), hit(at, 0.001, 0.04, l)));
    }
  }, { level: -4 }),
  // The grenadier's launcher: a hollow pop.
  launch: def('sfx', 0.5, (s) => {
    s.to(s.amp(s.osc('sine', [[0, 260], [0.08, 90]]), hit(0, 0.002, 0.1, 1)));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', 600, 1.2), hit(0, 0.002, 0.12, 0.8)));
  }),

  // ------------------------------------------------------------ moving and acting
  step: def(
    'sfx',
    0.15,
    (s) => {
      s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', s.vary(650)), hit(0, 0.004, 0.06, 1)));
      s.to(s.filter(s.grains(0.005, 0.05, 4, 0.006), 'bandpass', s.vary(2600), 1.2), 0.5);
    },
    { variants: 6, level: -14 },
  ),
  'step.mec': def(
    'sfx',
    0.6,
    (s) => {
      s.to(s.amp(s.osc('sine', [[0, 70], [0.2, 34]]), hit(0, 0.004, 0.22, 1)));
      s.to(s.metal(0.01, [520, 870, 1330], 0.18, 0.3));
      s.to(s.amp(s.filter(s.osc('sawtooth', [[0, 380], [0.25, 620]]), 'bandpass', 900, 2), [[0, 0], [0.06, 0.12], [0.3, 0.001]]));
    },
    { variants: 4, level: -6 },
  ),
  'step.drag': def('sfx', 0.35, (s) => s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 500), [[0, 0], [0.05, 0.8], [0.3, 0.001]])), { variants: 4, level: -12 }),
  // The dead coming in from the edge of the map: a long throaty moan, far off. A buzzing
  // voice through two vowel formants that slide from «aa» to «oo», a wobble in the pitch,
  // rasp on top, and the distance taking the top end off.
  'zombie.moan': def('sfx', 1.7, (s) => {
    const low = s.vary(80, 0.15);
    const voice = s.osc('sawtooth', [[0, low], [0.5, low * s.r(1.1, 1.25)], [1.45, low * 0.7]]);
    const wobble = s.ctx.createGain();
    wobble.gain.value = s.r(25, 45);
    s.osc('sine', s.vary(4.5, 0.25)).connect(wobble).connect(voice.detune);
    const sum = s.ctx.createGain();
    s.amp(s.filter(voice, 'bandpass', [[0, s.vary(620)], [1.2, 430]], 4), 1).connect(sum);
    s.amp(s.filter(voice, 'bandpass', [[0, s.vary(1150)], [1.2, 780]], 5), 0.5).connect(sum);
    s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(1300), 1.2), 0.25).connect(sum);
    const far = s.filter(s.shape(sum, 2), 'lowpass', s.vary(1700));
    s.to(s.amp(far, [[0, 0], [0.28, 0.9], [1.0, 0.7], [1.6, 0.001]]));
  }, { variants: 4, level: -4, reverb: 0.35 }),
  // Raiders arriving: a pickup coming in fast, braking hard on loose ground (a squeal and the
  // tyres crunching), idling for a moment and a door slammed.
  'truck.engine': def('sfx', 2.7, (s) => {
    // Firing rate of the engine: up as it comes in, down as it brakes, then idle.
    const rpm: [number, number][] = [[0, s.vary(48)], [0.9, s.vary(66)], [1.15, 70], [1.75, 34], [2.7, 30]];
    const engine = s.ctx.createGain();
    s.osc('sawtooth', rpm).connect(engine);
    const half = s.osc('square', rpm.map(([t, f]) => [t, f / 2] as [number, number]));
    s.amp(half, 0.5).connect(engine);
    const rough = s.ctx.createGain();
    rough.gain.value = 0.6;
    s.filter(s.noise('brown'), 'lowpass', 90).connect(rough.gain);
    engine.connect(rough);
    const body = s.filter(s.shape(rough, 2.5), 'lowpass', [[0, 700], [1.15, 900], [1.75, 380], [2.7, 320]], 1.5);
    s.to(s.amp(body, [[0, 0], [0.4, 0.45], [1.15, 0.55], [1.75, 0.3], [2.2, 0.2], [2.65, 0.001]]));
    // The brakes: a squeal wavering, and gravel under the tyres.
    const squeal = s.osc('sine', [[1.2, s.vary(2300)], [1.75, 2050]]);
    const trem = s.ctx.createGain();
    trem.gain.value = 40;
    s.osc('sine', 9).connect(trem).connect(squeal.detune);
    s.to(s.amp(squeal, [[1.2, 0], [1.3, 0.25], [1.65, 0.2], [1.8, 0.001]]));
    s.to(s.filter(s.grains(1.15, 1.85, 90, 0.01), 'bandpass', 1600, 0.7), 1.1);
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 900), [[1.15, 0], [1.25, 0.6], [1.8, 0.001]]));
    // A door slammed.
    s.to(s.amp(s.osc('sine', [[2.3, 120], [2.4, 60]]), hit(2.3, 0.002, 0.1, 0.9)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 1800), hit(2.3, 0.001, 0.05, 0.8)));
    s.to(s.metal(2.3, [s.vary(900), s.vary(1450)], 0.06, 0.25));
  }, { variants: 2, level: -2 }),
  // Registrar: a drawer (or a lid, a boot) dragged open and knocking against its stop, then
  // hands rummaging through what is inside and a couple of small things clinking.
  search: def('sfx', 0.7, (s) => {
    const slide = s.filter(s.noise('pink'), 'bandpass', [[0, s.vary(380)], [0.2, s.vary(950)]], 2.5);
    s.to(s.amp(slide, [[0, 0], [0.04, 0.55], [0.18, 0.45], [0.22, 0.001]]));
    s.to(s.filter(s.grains(0.01, 0.2, 14, 0.008, false), 'bandpass', 1300, 1.2), 0.35);
    s.to(s.amp(s.osc('sine', [[0.21, s.vary(150)], [0.3, 70]]), hit(0.21, 0.002, 0.08, 0.45)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 1400), hit(0.21, 0.001, 0.04, 0.35)));
    const rustle = s.filter(s.grains(0.25, 0.68, 70, 0.016, false), 'bandpass', s.vary(2200), 0.8);
    s.to(s.amp(rustle, [[0.25, 0], [0.3, 1.4], [0.55, 1], [0.68, 0.001]]));
    for (const at of [s.r(0.33, 0.42), s.r(0.48, 0.58)]) s.to(s.metal(at, [s.vary(2900), s.vary(4300), s.vary(6100)], 0.05, 0.25));
  }, { variants: 4, level: -6 }),
  // Magazine out, magazine in, bolt.
  reload: def('sfx', 0.95, (s) => {
    const click = (at: number, f: number, l: number) => {
      s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(f), 4), hit(at, 0.001, 0.03, l)));
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', 420, 2), hit(at, 0.002, 0.05, l * 0.6)));
    };
    click(0, 2600, 0.6);
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 1800, 1.5), [[0.03, 0], [0.08, 0.15], [0.16, 0.001]]));
    click(0.36, 1900, 1);
    click(0.66, 3200, 0.7);
    click(0.74, 2400, 0.8);
  }, { level: -4 }),
  // Overwatch: the safety off, the bolt ready and the rifle brought up.
  overwatch: def('ui', 0.4, (s) => {
    for (const at of [0, 0.08]) s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(2900), 4), hit(at, 0.001, 0.03, 0.7)));
    s.to(s.metal(0.16, [s.vary(1900), s.vary(3100)], 0.03, 0.4));
    s.to(s.amp(s.filter(s.grains(0.18, 0.36, 8, 0.02, false), 'bandpass', 1300, 0.7), [[0.18, 0], [0.22, 1], [0.36, 0.001]]), 0.4);
  }),
  // Hunkering down: cloth and kit shifting.
  hunker: def('sfx', 0.45, (s) => {
    s.to(s.amp(s.filter(s.grains(0, 0.35, 18, 0.03, false), 'bandpass', 1300, 0.7), [[0, 0], [0.05, 1], [0.4, 0.001]]));
  }, { level: -6 }),
  'melee.swing': def('sfx', 0.35, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 2200], [0.2, 800]], 2), [[0, 0], [0.09, 0.9], [0.25, 0.001]]));
  }),
  // The sword: a slash and the blade ringing.
  'melee.sword': def('sfx', 0.8, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2200), hit(0, 0.002, 0.05, 0.9)));
    s.to(s.metal(0.005, [2800, 4100, 5700].map((f) => s.vary(f, 0.03)), 0.55, 0.45));
    s.to(s.amp(s.osc('sine', [[0, 120], [0.1, 60]]), hit(0, 0.003, 0.1, 0.6)));
  }, { reverb: 0.15 }),
  // Electric: a buzzing zap (stun lance, the drone's shock).
  'melee.stun': def('sfx', 0.5, (s) => {
    const z = s.filter(s.noise('white'), 'bandpass', 3000, 1.2);
    const chop = s.ctx.createGain();
    chop.gain.value = 0;
    s.osc('square', 60).connect(chop.gain);
    z.connect(chop);
    s.to(s.amp(chop, hit(0, 0.002, 0.35, 1)));
    s.to(s.amp(s.shape(s.osc('sawtooth', 120), 3), hit(0, 0.002, 0.3, 0.4)));
  }),
  'melee.claw': def('sfx', 0.4, (s) => {
    s.to(s.filter(s.grains(0, 0.09, 3, 0.03, false), 'bandpass', 1500, 1.4), 1.2);
    s.to(s.amp(s.osc('sine', [[0, 100], [0.1, 50]]), hit(0.03, 0.003, 0.1, 0.8)));
  }),
  door: def('sfx', 0.6, (s) => {
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', 800, 1), [[0, 0], [0.1, 0.5], [0.4, 0.001]]));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 400), hit(0.42, 0.003, 0.1, 0.8)));
  }, { level: -4 }),
  // The specialist's drone taking off: a buzzing hum.
  drone: def('sfx', 0.8, (s) => {
    const buzz = s.filter(s.osc('sawtooth', [[0, 160], [0.3, 210]]), 'bandpass', 900, 1.5);
    const trem = s.ctx.createGain();
    trem.gain.value = 0.6;
    const lfo = s.osc('sine', 28);
    const depth = s.ctx.createGain();
    depth.gain.value = 0.4;
    lfo.connect(depth).connect(trem.gain);
    buzz.connect(trem);
    s.to(s.amp(trem, [[0, 0], [0.12, 0.7], [0.75, 0.001]]));
  }, { level: -6 }),
  // Someone patched up (a medkit, the medic's drone): a pouch torn open, a hiss of spray and a pat on the
  // dressing. No chime (playtest 18/10 #22: the old one was a power-up's arpeggio).
  heal: def('sfx', 0.9, (s) => {
    const velcro = s.filter(s.grains(0, 0.17, 40, 0.004, false), 'bandpass', s.vary(2500), 0.8);
    s.to(s.amp(velcro, [[0, 0], [0.03, 1], [0.15, 0.7], [0.18, 0.001]]));
    const spray = s.filter(s.noise('white'), 'bandpass', s.vary(5200), 0.7);
    s.to(s.amp(spray, [[0.24, 0], [0.27, 0.3], [0.52, 0.25], [0.58, 0.001]]));
    s.to(s.amp(s.osc('sine', [[0.66, s.vary(180)], [0.72, 100]], 0.66, 0.12), hit(0.66, 0.002, 0.06, 0.4)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 900), hit(0.66, 0.001, 0.05, 0.4)));
  }, { variants: 3, reverb: 0.15, level: -3 }),
  // Covering someone (+20 defence): a plate or a riot shield braced with a dull thunk, and its strap pulled tight.
  shield: def('sfx', 0.6, (s) => {
    s.to(s.amp(s.osc('sine', [[0, s.vary(140)], [0.1, 70]], 0, 0.2), hit(0, 0.002, 0.12, 0.8)));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 600), hit(0, 0.001, 0.1, 0.8)));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(900), 1.4), hit(0, 0.001, 0.05, 0.6)));
    s.to(s.metal(0, [430, 1010, 1730].map((f) => s.vary(f, 0.06)), 0.04, 0.12));
    s.to(s.amp(s.filter(s.grains(0.14, 0.34, 14, 0.006, false), 'bandpass', 1600, 0.8), [[0.14, 0], [0.2, 1], [0.34, 0.001]]), 0.6);
  }, { variants: 3, reverb: 0.15, level: -3 }),
  // Working on the radio (the hack mission, «Arreglad la radio»): a screwdriver's clicks, static spitting
  // and a tuning sweep through the noise. No computer chirps.
  hack: def('ui', 0.8, (s) => {
    for (const at of [0, s.r(0.06, 0.1), s.r(0.15, 0.2)]) s.to(s.metal(at, [s.vary(2400), s.vary(3700)], 0.02, 0.35));
    const crackle = s.filter(s.grains(0.18, 0.75, 26, 0.012, false), 'bandpass', s.vary(1800), 0.7);
    s.to(s.amp(crackle, [[0.18, 0], [0.25, 1], [0.7, 0.6], [0.78, 0.001]]), 0.6);
    const tune = s.filter(s.noise('white'), 'bandpass', [[0.3, s.vary(700)], [0.72, s.vary(2300)]], 9);
    s.to(s.amp(tune, [[0.3, 0], [0.4, 0.35], [0.7, 0.3], [0.78, 0.001]]));
  }, { variants: 3 }),
  // The radio back to life: the switch clacking on, a burst of static settling, and the set humming.
  'hack.ok': def('ui', 0.9, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2000), hit(0, 0.0005, 0.015, 0.8)));
    s.to(s.amp(s.osc('sine', [[0, 300], [0.04, 150]], 0, 0.08), hit(0, 0.001, 0.04, 0.5)));
    const fizz = s.filter(s.noise('white'), 'bandpass', 2000, 0.6);
    s.to(s.amp(fizz, [[0.05, 0], [0.08, 0.6], [0.45, 0.15], [0.85, 0.001]]));
    const hum = s.filter(s.osc('sawtooth', 100, 0.1, 0.8), 'lowpass', 320);
    s.to(s.amp(hum, [[0.1, 0], [0.25, 0.15], [0.85, 0.001]]));
  }),
  // The alarm: a two-tone klaxon.
  alarm: def('sfx', 1.6, (s) => {
    for (let i = 0; i < 3; i++) {
      const at = i * 0.5;
      const o = s.osc('sawtooth', [[at, 520], [at + 0.24, 520], [at + 0.25, 390], [at + 0.49, 390]], at, 0.5);
      s.to(s.amp(s.filter(o, 'lowpass', 1800), [[at, 0], [at + 0.02, 0.4], [at + 0.48, 0.4], [at + 0.5, 0]]));
    }
  }, { reverb: 0.3 }),
  // The sabotage's siren (playtest 15/10 #14), one per kind of map (sfx.ts siren), each a seamless loop
  // that game.ts places where it stands. The sweeps repeat past the period so the seam can be faded.
  // A police car in town: the wail, up fast and down slow, through a horn speaker.
  'siren.police': def('sfx', 4.4 + LOOP_SEAM, (s) => {
    const sweep = repeat(4.4, [[0, 640], [1.5, 1480]], s.dur);
    const voice = s.ctx.createGain();
    s.osc('sawtooth', sweep).connect(voice);
    s.amp(s.osc('square', sweep, 0, undefined, 4), 0.35).connect(voice);
    const horn = s.filter(s.filter(voice, 'highpass', 420, 0.7), 'peaking', 1900, 1.1, 6);
    s.to(s.amp(s.filter(s.shape(horn, 1.8), 'lowpass', 4300, 0.8), 0.5));
  }, { variants: 1, loop: 4.4, reverb: 0.35 }),
  // An ambulance out of town: two tones, a fourth apart, half a second each.
  'siren.ambulance': def('sfx', 4.4 + LOOP_SEAM, (s) => {
    const tones = repeat(1.1, [[0, 466], [0.548, 466], [0.55, 622], [1.098, 622]], s.dur);
    const voice = s.ctx.createGain();
    s.osc('square', tones).connect(voice);
    s.amp(s.osc('sawtooth', tones, 0, undefined, -5), 0.4).connect(voice);
    const horn = s.filter(s.filter(voice, 'highpass', 320, 0.7), 'peaking', 1500, 1.2, 5);
    s.to(s.amp(s.filter(s.shape(horn, 1.4), 'lowpass', 2800, 0.8), 0.5));
  }, { variants: 1, loop: 4.4, reverb: 0.35 }),
  // A factory's motor siren: two rotors a third apart winding up, holding, winding down, with the wind of the blades.
  'siren.alarm': def('sfx', 7 + LOOP_SEAM, (s) => {
    const rotor = repeat(7, [[0, 180], [2.6, 720], [4, 700]], s.dur);
    const voice = s.ctx.createGain();
    s.osc('sawtooth', rotor).connect(voice);
    s.amp(s.osc('sawtooth', rotor.map(([t, f]) => [t, f * 1.26] as [number, number])), 0.7).connect(voice);
    const blades = s.filter(s.noise('pink'), 'bandpass', rotor.map(([t, f]) => [t, f * 3] as [number, number]), 2);
    s.amp(blades, 0.5).connect(voice);
    const body = s.filter(s.filter(s.shape(voice, 1.6), 'bandpass', 950, 0.6), 'lowpass', 3200, 0.7);
    s.to(s.amp(body, repeat(7, [[0, 0.5], [2.6, 1], [4, 1]], s.dur)));
  }, { variants: 1, loop: 7, reverb: 0.5 }),
  // The Chillón's shriek (its Grito, and the dead it raises; the id is the old psionics'): one
  // human throat torn ragged, rising to a scream that cracks and falls, rasping and distorted.
  psi: def('sfx', 1.8, (s) => {
    const base = s.vary(240, 0.1);
    scream(s, 0, [[0, base], [0.35, base * 2.6], [0.9, base * 2.8], [1.25, base * 2.2], [1.55, base * 1.3]], [[0, 0], [0.08, 0.6], [0.35, 1], [1.2, 0.85], [1.65, 0.001]]);
  }, { variants: 4, reverb: 0.45 }),
  // A beam of light lifting someone out (evacuation, printed reinforcements arriving).
  beam: def('sfx', 1.4, (s) => {
    for (const k of [1, 1.5, 2.01]) {
      s.to(s.amp(s.osc('sine', [[0, 600 * k], [0.9, 1800 * k]]), [[0, 0], [0.2, 0.18], [1.3, 0.001]]));
    }
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 5000), [[0, 0], [0.3, 0.2], [1.2, 0.001]]));
  }, { reverb: 0.5 }),
  // Printing pods falling from the airship.
  pods: def('sfx', 1.6, (s) => {
    s.to(s.amp(s.osc('sine', [[0, 2100], [0.8, 420]]), [[0, 0], [0.1, 0.3], [0.8, 0.2], [0.85, 0]]));
    s.to(s.amp(s.osc('sine', [[0.85, 90], [1.1, 40]]), hit(0.85, 0.004, 0.3, 1)));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 3000), [[0.9, 0], [1.0, 0.4], [1.5, 0.001]]));
  }, { reverb: 0.35 }),
  // A red flare popping, and a distant horn from the airship.
  flare: def('sfx', 2.2, (s) => {
    s.to(s.amp(s.osc('sine', [[0, 300], [0.06, 120]]), hit(0, 0.002, 0.08, 0.7)));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 2500, 0.8), [[0.02, 0], [0.1, 0.4], [0.9, 0.001]]));
    for (const f of [98, 147]) s.to(s.amp(s.filter(s.osc('sawtooth', f), 'lowpass', 600), [[0.5, 0], [0.9, 0.25], [2.1, 0.001]]));
  }, { reverb: 0.5 }),
  // The cargo taken (a Recovery): grabbed, stuffed into a pack with a zip, and its weight settling.
  data: def('ui', 0.75, (s) => {
    const grab = s.filter(s.grains(0, 0.24, 22, 0.012, false), 'bandpass', 1400, 0.8);
    s.to(s.amp(grab, [[0, 0], [0.04, 1], [0.24, 0.001]]), 0.6);
    const zip = s.filter(s.grains(0.26, 0.5, 34, 0.003, false), 'highpass', 2500, 0.7);
    s.to(s.amp(zip, [[0.26, 0], [0.3, 0.8], [0.48, 1], [0.5, 0.001]]), 0.5);
    s.to(s.amp(s.osc('sine', [[0.56, s.vary(150)], [0.63, 80]], 0.56, 0.12), hit(0.56, 0.002, 0.08, 0.6)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 900), hit(0.56, 0.001, 0.06, 0.5)));
  }, { variants: 3 }),
  // Something found while searching: the thing lifted out (a small knock), slipped into the pack (a cloth
  // swish) and settling with a rattle. No notes (playtest 18/10 #22).
  found: def('sfx', 0.6, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(1800), 3), hit(0, 0.001, 0.03, 0.5)));
    s.to(s.amp(s.osc('sine', [[0, s.vary(230)], [0.06, 120]]), hit(0, 0.002, 0.06, 0.5)));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', [[0.06, 1200], [0.28, 3000]], 1.2), [[0.06, 0], [0.12, 0.5], [0.3, 0.001]]));
    s.to(s.amp(s.osc('sine', [[0.32, s.vary(260)], [0.38, 140]], 0.32, 0.1), hit(0.32, 0.002, 0.05, 0.4)));
    s.to(s.filter(s.grains(0.33, 0.48, 6, 0.006, false), 'bandpass', s.vary(2600), 1), 0.35);
  }, { variants: 3, level: -8 }),
  // The VIP joins the squad: a walkie-talkie keyed («lo tenemos») and gear shifting as they fall in.
  rescue: def('ui', 0.7, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2200), hit(0, 0.0005, 0.012, 0.6)));
    const squelch = s.filter(s.noise('white'), 'bandpass', s.vary(1700), 1.2);
    s.to(s.amp(squelch, [[0.01, 0], [0.02, 0.45], [0.13, 0.35], [0.15, 0]]));
    const gear = s.filter(s.grains(0.2, 0.55, 12, 0.02, false), 'bandpass', 1200, 0.7);
    s.to(s.amp(gear, [[0.2, 0], [0.25, 1], [0.55, 0.001]]), 0.5);
    s.to(s.amp(s.osc('sine', [[0.24, 130], [0.3, 70]], 0.24, 0.12), hit(0.24, 0.002, 0.07, 0.5)));
  }, { variants: 3, reverb: 0.15 }),

  // ------------------------------------------------------------ deaths
  'death.human': def('sfx', 0.9, (s) => {
    s.to(s.amp(s.osc('sine', [[0.05, 75], [0.35, 40]]), hit(0.05, 0.004, 0.3, 1)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 700), hit(0.05, 0.003, 0.18, 0.8)));
    s.to(s.metal(0.08, [2400, 3600], 0.08, 0.2));
    s.to(s.metal(0.16, [2900, 4300], 0.06, 0.15));
  }),
  // ADVENT are printed: they come apart in a crackle of glitches.
  'death.advent': def('sfx', 1, (s) => {
    death(s);
    let held = 0;
    const glitch = s.samples(0.6, (t, i) => {
      if (i % Math.floor(40 + t * 300) === 0) held = s.rnd() * 2 - 1;
      return held * Math.pow(1 - t / 0.6, 2) * 0.5;
    }, 0.05);
    s.to(s.filter(glitch, 'bandpass', 1800, 0.7));
  }),
  // Machines power down.
  'death.robot': def('sfx', 1.4, (s) => {
    s.to(s.amp(s.filter(s.osc('sawtooth', [[0, 420], [0.9, 38]]), 'lowpass', 1500), [[0, 0], [0.02, 0.5], [1, 0.001]]));
    s.to(s.filter(s.grains(0, 0.5, 14, 0.006), 'bandpass', 4500, 1), 0.7);
    s.to(s.metal(0.95, [380, 710, 1150], 0.4, 0.6));
    s.to(s.amp(s.osc('sine', [[0.95, 80], [1.2, 40]]), hit(0.95, 0.003, 0.3, 1)));
  }, { reverb: 0.2 }),
  // The Chillón dying (the id is the old sectoid's): a last shriek choked off into a gurgle, and the body down.
  'death.sectoid': def('sfx', 1.4, (s) => {
    const base = s.vary(250, 0.1);
    scream(s, 0, [[0, base], [0.2, base * 2.4], [0.4, base * 2.6], [0.46, base * 1.2]], [[0, 0], [0.05, 0.7], [0.3, 1], [0.42, 0.9], [0.46, 0.0005]]);
    gurgle(s, 0.45, 0.35, 1.4);
    death(s, 0.75);
  }, { variants: 3, reverb: 0.35 }),
  // The Corredor dying: a quick wet gurgle as it goes down mid-stride, knees then body.
  'death.runner': def('sfx', 1, (s) => {
    gurgle(s, 0, 0.4, 1.8);
    s.to(s.amp(s.osc('sine', [[0.15, 90], [0.3, 50]]), hit(0.15, 0.003, 0.15, 0.6)));
    s.to(s.amp(s.filter(s.noise('pink', 0.15, 0.2), 'lowpass', 900), hit(0.15, 0.002, 0.08, 0.5)));
    death(s, 0.32);
  }, { variants: 3 }),
  'death.zombie': def('sfx', 1, (s) => {
    const voice = s.osc('sawtooth', [[0, 95], [0.7, 70]]);
    const f1 = s.filter(voice, 'bandpass', 520, 5);
    const f2 = s.filter(voice, 'bandpass', 1300, 6);
    s.to(s.amp(f1, [[0, 0], [0.1, 0.8], [0.7, 0.001]]));
    s.to(s.amp(f2, [[0, 0], [0.1, 0.4], [0.6, 0.001]]));
    death(s, 0.65);
  }),

  // ------------------------------------------------------------ combat stingers
  // ¡Contacto!: a brass-like stab with a dissonant edge and a drum hit.
  contact: def('sfx', 2, (s) => {
    for (const f of [73.4, 110, 146.8, 155.6]) {
      const o = s.filter(s.osc('sawtooth', f, 0, 1.8, s.r(-8, 8)), 'lowpass', [[0, 2400], [1.5, 500]]);
      s.to(s.amp(o, [[0, 0], [0.02, 0.35], [1.6, 0.001]]));
    }
    s.to(s.amp(s.osc('sine', [[0, 85], [0.4, 55]]), hit(0, 0.003, 0.7, 1.2)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 1200), hit(0, 0.002, 0.25, 0.7)));
    const screech = s.osc('sawtooth', 1175);
    const trem = s.ctx.createGain();
    trem.gain.value = 0;
    s.osc('sine', 11).connect(trem.gain);
    screech.connect(trem);
    s.to(s.amp(s.filter(trem, 'bandpass', 2400, 2), [[0.05, 0], [0.3, 0.08], [1.4, 0.001]]));
  }, { reverb: 0.45, level: 2 }),
  // Concealment broken: a rising rush ending on a low hit.
  reveal: def('sfx', 1.4, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 300], [0.65, 3200]], 1.5), [[0, 0], [0.6, 0.7], [0.7, 0.001]]));
    s.to(s.amp(s.osc('sine', [[0.65, 90], [1.1, 45]]), hit(0.65, 0.003, 0.6, 1.2)));
    for (const f of [146.8, 207.7]) s.to(s.amp(s.filter(s.osc('sawtooth', f, 0.65), 'lowpass', 1200), hit(0.65, 0.01, 0.6, 0.25)));
  }, { reverb: 0.4 }),
  // Our turn: a walkie-talkie keyed, a breath of static and the key let go. Theirs: a low swell with a metal scrape.
  'turn.xcom': def('ui', 0.45, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2200), hit(0, 0.0005, 0.012, 0.6)));
    const squelch = s.filter(s.noise('white'), 'bandpass', s.vary(1600), 1.1);
    s.to(s.amp(squelch, [[0.01, 0], [0.02, 0.35], [0.2, 0.25], [0.22, 0]]));
    s.to(s.amp(s.filter(s.noise('white', 0.22, 0.05), 'bandpass', 2600, 1.5), hit(0.22, 0.0005, 0.02, 0.5)));
  }, { variants: 3 }),
  'turn.alien': def('sfx', 2, (s) => {
    for (const f of [55, 58.3, 82.4]) s.to(s.amp(s.filter(s.osc('sawtooth', f), 'lowpass', [[0, 200], [0.8, 700], [1.9, 200]]), [[0, 0], [0.5, 0.35], [1.9, 0.001]]));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 800], [1, 2400]], 6), [[0.2, 0], [0.7, 0.15], [1.4, 0.001]]));
  }, { reverb: 0.4 }),
  // The command passed between partners: a radio's click and a short, soft breath of static.
  command: def('ui', 0.2, (s) => {
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', 2400, 1.5), hit(0, 0.001, 0.015, 0.5)));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(1500), 0.9), [[0.02, 0], [0.03, 0.12], [0.09, 0.08], [0.11, 0]]));
  }, { variants: 3, level: -3 }),
  // Señalar (playtest 18/10 #7; #33: the radio squelch it borrowed from the command was «mega desagradable»): a
  // pin pushed into the map, soft and short, so it is heard without getting in the way however often it is used.
  mark: def('ui', 0.15, (s) => {
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(1900), 0.9), hit(0, 0.0008, 0.012, 0.6)));
    s.to(s.amp(s.osc('sine', [[0, s.vary(430)], [0.04, 260]], 0, 0.08), hit(0, 0.002, 0.04, 0.5)));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 1100), hit(0.002, 0.002, 0.03, 0.4)));
  }, { variants: 3, level: -5 }),
  // The mission won: a low chord swelling in like a held breath let out, over a soft drum. No fanfare or bell
  // (playtest 18/10 #22).
  victory: def('music', 3.5, (s) => {
    for (const f of [73.4, 146.8, 185, 220]) {
      const o = s.filter(s.osc('sawtooth', f, 0, 3.4, s.r(-7, 7)), 'lowpass', [[0, 400], [1.2, 1400], [3.3, 500]]);
      s.to(s.amp(o, [[0, 0], [0.9, 0.22], [2.2, 0.16], [3.4, 0.001]]));
    }
    s.to(s.amp(s.osc('sine', [[0, 70], [0.6, 45]]), hit(0, 0.01, 0.8, 0.9)));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 300), hit(0, 0.01, 0.6, 0.5)));
  }, { reverb: 0.5, level: 2 }),
  // The mission lost: a dark, close chord rising out of the low end and fading, a far boom under it, wind.
  // No falling notes (playtest 18/10 #22: they sounded like an arcade's game over).
  defeat: def('music', 3.8, (s) => {
    for (const f of [73.4, 77.8, 110]) {
      const o = s.filter(s.osc('sawtooth', f, 0, 3.7, s.r(-6, 6)), 'lowpass', [[0, 250], [1.4, 700], [3.6, 200]]);
      s.to(s.amp(o, [[0, 0], [1, 0.25], [3.7, 0.001]]));
    }
    s.to(s.amp(s.osc('sine', [[0.1, 55], [1.2, 30]]), hit(0.1, 0.02, 1.4, 0.9)));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', [[0, 400], [3, 900]], 0.8), [[0, 0], [1.2, 0.12], [3.7, 0.001]]));
  }, { reverb: 0.5, level: 2 }),

  // ------------------------------------------------------------ the case file (interface)
  // A typewriter key: the type bar striking, the key's click, a wooden thock.
  'ui.key': def(
    'ui',
    0.12,
    (s) => {
      s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(3500, 0.15), 3), hit(0, 0.0005, 0.02, 1)));
      s.to(s.amp(s.osc('sine', [[0, s.vary(190)], [0.04, 120]]), hit(0, 0.001, 0.04, 0.7)));
      s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(1300), 4), hit(0.008, 0.0008, 0.035, 0.6)));
    },
    { variants: 6, level: -2 },
  ),
  'ui.space': def('ui', 0.15, (s) => {
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', 900, 2), hit(0, 0.001, 0.05, 1)));
    s.to(s.amp(s.osc('sine', [[0, 140], [0.06, 90]]), hit(0, 0.001, 0.06, 0.9)));
  }),
  // The carriage bell.
  'ui.bell': def('ui', 1.2, (s) => s.to(s.metal(0, [2350, 6490, 12700].map((f) => s.vary(f, 0.01)), 1.1, 0.6)), { variants: 2 }),
  // Carriage return: the ratchet running back, then the bell (confirming, launching).
  'ui.return': def('ui', 1.1, (s) => {
    for (let i = 0; i < 9; i++) {
      const at = 0.03 * i + 0.002 * i * i;
      s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(2600, 0.1), 5), hit(at, 0.0005, 0.015, 0.6)));
    }
    s.to(s.metal(0.36, [2350, 6490], 0.8, 0.45));
    s.to(s.amp(s.osc('sine', [[0.34, 160], [0.4, 100]]), hit(0.34, 0.001, 0.06, 0.7)));
  }),
  // Paper: a sheet pulled out or put down.
  'ui.paper': def('ui', 0.4, (s) => {
    s.to(s.amp(s.filter(s.grains(0, 0.28, 26, 0.012, false), 'bandpass', s.vary(2600), 0.7), [[0, 0], [0.05, 1], [0.32, 0.001]]));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', 900, 0.7), [[0, 0], [0.06, 0.3], [0.3, 0.001]]));
  }, { variants: 4, level: -4 }),
  // A page turned (switching screens).
  'ui.page': def('ui', 0.35, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 1500], [0.18, 4200]], 1), [[0, 0], [0.1, 0.7], [0.25, 0.001]]));
    s.to(s.filter(s.grains(0.02, 0.2, 8, 0.01, false), 'highpass', 3000), 0.5);
  }, { variants: 4, level: -5 }),
  // A rubber stamp: thump, slap and the desk knocking.
  'ui.stamp': def('ui', 0.4, (s) => {
    s.to(s.amp(s.osc('sine', [[0, 105], [0.1, 55]]), hit(0, 0.002, 0.12, 1.2)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 2200), hit(0, 0.001, 0.045, 0.9)));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 700, 5), hit(0.004, 0.001, 0.07, 0.6)));
  }, { variants: 3, level: 2 }),
  // A pen scratching a note.
  'ui.pen': def('ui', 0.45, (s) => {
    const scratch = s.filter(s.noise('white'), 'highpass', 3200);
    const am = s.ctx.createGain();
    am.gain.value = 0;
    s.samples(0.45, (t) => Math.abs(Math.sin(t * Math.PI * 2 * 9)) * (0.6 + 0.4 * Math.sin(t * 31))).connect(am.gain);
    scratch.connect(am);
    s.to(s.amp(am, [[0, 0], [0.05, 0.6], [0.38, 0.4], [0.44, 0.001]]));
  }, { variants: 3, level: -6 }),
  // Not allowed: a dull double knock.
  'ui.error': def('ui', 0.3, (s) => {
    for (const at of [0, 0.09]) s.to(s.amp(s.filter(s.osc('triangle', 220, at, 0.08), 'lowpass', 600), hit(at, 0.002, 0.07, 0.8)));
  }),
  // The clock while days pass.
  'ui.tick': def('ui', 0.1, (s) => s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(4000, 0.2), 6), hit(0, 0.0005, 0.012, 0.8))), { variants: 4, level: -8 }),

  // The skill board (strategy/hexBoard.ts; playtest 16/10, «sutil pero satisfactorio»), through audio/ui.ts.
  // A cell bought, as it bounces: a metal latch clicking home, a little weight under it and a short glint.
  // Neither bell nor fanfare: several may be bought one after another.
  'cell.buy': def('ui', 0.45, (s) => {
    s.to(s.metal(0, [2900, 4100, 5600].map((f) => s.vary(f, 0.04)), 0.035, 0.6));
    s.to(s.metal(0.028, [2300, 3500].map((f) => s.vary(f, 0.04)), 0.03, 0.4));
    s.to(s.amp(s.osc('sine', [[0, s.vary(520, 0.03)], [0.05, 380]], 0, 0.1), hit(0, 0.001, 0.05, 0.45)));
    s.to(s.filter(s.metal(0.03, [6200, 7400, 8800].map((f) => s.vary(f, 0.03)), 0.22, 1), 'highpass', 4000, 0.7), 0.2);
  }, { variants: 4, reverb: 0.15 }),
  // Over a cell (unlocked or chosen): a tick at the edge of hearing; the board is swept fast, so it barely varies.
  'cell.hover': def('ui', 0.05, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 3000, 0.7), hit(0, 0.0005, 0.008, 0.6)));
    s.to(s.amp(s.osc('sine', s.vary(1800, 0.02), 0, 0.03), hit(0, 0.001, 0.012, 0.4)));
  }, { variants: 3, level: -18 }),
  // While a cell is held down to buy it (one second, hexBoard HOLD_MS): a soft tone rising an octave as the
  // stroke goes round, gone just as the purchase clicks. Cut off (audio/ui.ts) if let go sooner.
  'cell.hold': def('ui', 1.05, (s) => {
    const rise: Env = [[0, 420], [1, 840]];
    const tone = s.ctx.createGain();
    s.osc('sine', rise).connect(tone);
    s.amp(s.osc('triangle', rise.map(([t, f]) => [t, f * 2] as [number, number])), 0.12).connect(tone);
    s.to(s.amp(tone, [[0, 0], [0.15, 0.25], [0.95, 0.5], [1, 0]]));
    s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', [[0, 2000], [1, 6000]], 2), [[0, 0], [0.9, 0.12], [1, 0]]));
  }, { variants: 1, level: -10 }),

  // ------------------------------------------------------------ the holo interface
  // The other interface (Holotáctico, ui/theme.ts): glass and light instead of
  // paper, so electronic blips instead of typewriter keys.
  'holo.click': def('ui', 0.08, (s) => {
    s.to(s.amp(s.osc('sine', s.vary(1850, 0.06)), hit(0, 0.002, 0.04, 0.6)));
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 6000), hit(0, 0.0005, 0.008, 0.25)));
  }, { variants: 4, level: -4 }),
  'holo.confirm': def('ui', 0.4, (s) => {
    for (const [at, f] of [
      [0, 1200],
      [0.07, 1800],
    ] as const)
      s.to(s.amp(s.filter(s.osc('triangle', f, at, 0.12), 'lowpass', 4000), hit(at, 0.003, 0.1, 0.5)));
    s.to(s.metal(0.07, [3600, 5400], 0.25, 0.12));
  }),
  'holo.page': def('ui', 0.25, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 1200], [0.14, 5200]], 2), [[0, 0], [0.06, 0.5], [0.18, 0.001]]));
    s.to(s.amp(s.osc('sine', 2400, 0.12, 0.06), hit(0.12, 0.002, 0.05, 0.25)));
  }, { variants: 3, level: -4 }),
  'holo.open': def('ui', 0.5, (s) => {
    s.to(s.amp(s.filter(s.osc('sawtooth', 220), 'lowpass', [[0, 300], [0.25, 2400]], 2), [[0, 0], [0.15, 0.3], [0.4, 0.001]]));
    s.to(s.metal(0.15, [2640, 3960], 0.3, 0.15));
  }, { variants: 2, level: -4 }),
  'holo.error': def('ui', 0.3, (s) => {
    s.to(s.amp(s.filter(s.osc('square', [[0, 320], [0.2, 190]]), 'lowpass', 1400), [[0, 0], [0.01, 0.4], [0.22, 0.001]]));
  }),
  'holo.alert': def('ui', 0.5, (s) => {
    for (let i = 0; i < 3; i++) s.to(s.amp(s.filter(s.osc('square', 1400, i * 0.09, 0.06), 'lowpass', 3500), hit(i * 0.09, 0.002, 0.05, 0.3)));
    s.to(s.amp(s.osc('sine', [[0, 120], [0.2, 70]]), hit(0, 0.004, 0.2, 0.6)));
  }, { level: 1 }),

  // ------------------------------------------------------------ radio
  'radio.static': def('radio', 0.4, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 1800, 1), [[0, 0], [0.01, 0.8], [0.3, 0.001]]));
    s.to(s.filter(s.grains(0, 0.3, 14, 0.004), 'highpass', 2500), 0.8);
  }, { variants: 4 }),
  'radio.key': def('radio', 0.05, (s) => s.to(s.amp(s.filter(s.noise('white'), 'highpass', 3200), hit(0, 0.0005, 0.015, 1))), { variants: 4, level: -10 }),
  'radio.close': def('radio', 0.3, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 2800, 4), hit(0, 0.0005, 0.02, 1)));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 1800, 1), [[0.01, 0], [0.02, 0.4], [0.2, 0.001]]));
  }),
  // Under the Congregación's voice: bells tolling somewhere in the sierra, through a set that drifts off the station.
  'radio.bells': def('radio', 2.8, (s) => {
    const bells = s.ctx.createGain();
    for (const at of [0, 1.3]) {
      const f = s.vary(233, 0.02);
      // A church bell's partials: hum, prime, minor third, fifth, octave and above.
      s.metal(at, [0.5, 1, 1.19, 1.5, 2, 2.52, 3].map((k) => f * k), 1.5, 0.7).connect(bells);
    }
    const band = s.filter(s.filter(bells, 'highpass', 350), 'lowpass', 2600);
    s.to(pulse(s, band, s.vary(0.7), 0.35));
    s.to(s.amp(s.osc('sine', [[0, 1830], [2.6, 1790]]), [[0, 0], [0.4, 0.02], [2.4, 0.015], [2.7, 0]]));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 2000, 0.8), [[0, 0], [0.2, 0.05], [2.5, 0.04], [2.75, 0]]));
  }, { reverb: 0.4 }),

  // ------------------------------------------------------------ strategy
  // The globe's scanner: a radar ping.
  'geo.ping': def('ui', 1.6, (s) => s.to(s.metal(0, [1250, 2510], 1.4, 0.5)), { reverb: 0.5 }),
  // A facility built or terrain cleared: hammering and a weld.
  build: def('sfx', 1.4, (s) => {
    for (const at of [0, 0.32, 0.64]) {
      s.to(s.metal(at, [820, 1530, 2690].map((f) => s.vary(f, 0.05)), 0.25, 0.5));
      s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 900), hit(at, 0.001, 0.05, 0.6)));
    }
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 5200, 3), [[0.9, 0], [0.95, 0.3], [1.35, 0.001]]));
  }, { level: -3 }),
};

/** A body hitting the ground, with kit rattling: shared by the deaths. */
function death(s: Synth, at = 0.05): void {
  s.to(s.amp(s.osc('sine', [[at, 75], [at + 0.3, 40]]), hit(at, 0.004, 0.3, 1)));
  s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 700), hit(at, 0.003, 0.18, 0.8)));
}
