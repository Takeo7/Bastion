import type { Bus } from './engine';
import { hit, type Synth } from './synth';

// Every sound effect of the game, made in code (no recordings): a recipe per
// sound, rendered once into a few variants (bank.ts) so repeats never sound
// identical. Levels are relative, in dB; the bank evens out loudness first.
//
// Families: weapons (by gun and tier, and alien guns), impacts, explosives,
// steps and actions, deaths, combat stingers, the interface of the case file
// (typewriter keys, carriage bell, rubber stamp, paper, pen), the radio and
// the strategy layer.

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
}

const def = (bus: Bus, dur: number, build: (s: Synth) => void, o: Omit<SoundDef, 'bus' | 'dur' | 'build'> = {}): SoundDef => ({ bus, dur, build, ...o });

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

// ---------------------------------------------------------------- the Lengua (songs)

/** Formants of sung vowels (Hz): what makes a buzz sound like a voice. */
const VOWELS = {
  a: [730, 1090, 2440],
  o: [570, 840, 2410],
  u: [300, 870, 2240],
  e: [530, 1840, 2480],
  i: [270, 2290, 3010],
} as const;
type Vowel = keyof typeof VOWELS;

/**
 * Voices singing the Lengua, the Coro's language: one per note, each a buzz
 * through the formants of a vowel (sliding from one vowel to another),
 * wavering, with breath. Always several at once: the Coro sings in plural.
 */
function chant(s: Synth, notes: number[], vowel: Vowel | [Vowel, Vowel], at: number, dur: number, level = 1, attack = 0.12): void {
  const [from, to] = typeof vowel === 'string' ? [vowel, vowel] : vowel;
  const end = Math.min(s.dur - 0.01, at + dur + 0.35);
  for (const f of notes) {
    const src = s.osc('sawtooth', f, at, end - at, s.r(-9, 9));
    const vib = s.osc('sine', s.r(5, 6.2), at, end - at);
    const depth = s.ctx.createGain();
    depth.gain.value = f * 0.012;
    vib.connect(depth).connect(src.frequency);
    const sum = s.ctx.createGain();
    VOWELS[from].forEach((fa, k) => {
      const fb = VOWELS[to][k]!;
      const band = s.filter(src, 'bandpass', [
        [at, fa],
        [at + dur, fb],
      ], 9);
      const g = s.ctx.createGain();
      g.gain.value = [1, 0.55, 0.28][k]!;
      band.connect(g).connect(sum);
    });
    const l = (level * 1.8) / notes.length;
    s.to(s.amp(sum, [[at, 0], [at + attack, l], [at + dur, l * 0.8], [end, 0.0005]]));
  }
  s.to(s.amp(s.filter(s.noise('pink', at, end - at), 'bandpass', 1600, 0.7), [[at, 0], [at + attack, level * 0.12], [end, 0.0005]]));
}

const NOTE = { D3: 146.8, A3: 220, D4: 293.7, E4: 329.6, F4: 349.2, Fs4: 370, G4: 392, A4: 440, C5: 523.3, D5: 587.3, E5: 659.3, A5: 880 };

const SONGS: Record<string, SoundDef> = {
  // The cue of any song: a short phrase of voices.
  'song.voice': def('sfx', 1.6, (s) => chant(s, [NOTE.D4, NOTE.F4, NOTE.A4, NOTE.E5], ['a', 'o'], 0, 1, 0.9), { reverb: 0.5 }),
  // Saeta: a rising chime, a whoosh, a bright "i".
  'song.bolt': def('sfx', 1, (s) => {
    chant(s, [NOTE.A4, NOTE.E5], 'i', 0, 0.25, 0.6, 0.04);
    s.to(s.amp(s.osc('sine', [[0, 900], [0.25, 2600]]), hit(0, 0.01, 0.35, 0.35)));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 1200], [0.3, 4200]], 1.5), [[0, 0], [0.12, 0.5], [0.45, 0.001]]));
    s.to(s.metal(0.28, [2350, 3520, 5280], 0.4, 0.25));
  }, { reverb: 0.4 }),
  // Bola de fuego: a low "o" and the roar of the fire, then the blast.
  'song.fire': def('sfx', 2.4, (s) => {
    chant(s, [NOTE.D3, NOTE.A3, NOTE.D4], 'o', 0, 0.7, 0.8);
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', [[0, 300], [0.7, 1400]]), [[0, 0], [0.6, 0.9], [0.75, 0.2], [2.2, 0.001]]));
    s.to(s.shape(s.amp(s.filter(s.noise('brown'), 'lowpass', [[0.7, 900], [2, 150]]), hit(0.7, 0.006, 1.4, 1.6)), 2.5));
    s.to(s.amp(s.osc('sine', [[0.7, 70], [1.5, 28]]), hit(0.7, 0.008, 0.9, 1.2)));
  }, { reverb: 0.4, level: 2 }),
  // Flecha espectral: a whistle and a quick "e".
  'song.arrow': def('sfx', 0.8, (s) => {
    chant(s, [NOTE.E5, NOTE.A5], 'e', 0, 0.18, 0.5, 0.03);
    s.to(s.amp(s.osc('sine', [[0, 1900], [0.35, 3100]]), [[0, 0], [0.04, 0.25], [0.4, 0.001]]));
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 5000), [[0, 0], [0.05, 0.3], [0.35, 0.001]]));
  }, { reverb: 0.35 }),
  // Teleport: going (a shimmer rising away) and coming (a shimmer falling into place with a chord).
  'song.vanish': def('sfx', 0.9, (s) => {
    for (const k of [1, 1.5, 2.02]) s.to(s.amp(s.osc('sine', [[0, 500 * k], [0.6, 1600 * k]]), [[0, 0], [0.05, 0.2], [0.6, 0.001]]));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 800], [0.6, 6000]], 2), [[0, 0], [0.1, 0.4], [0.6, 0.001]]));
  }, { reverb: 0.4 }),
  'song.appear': def('sfx', 1.2, (s) => {
    for (const k of [1, 1.5, 2.02]) s.to(s.amp(s.osc('sine', [[0, 1600 * k], [0.4, 500 * k]]), [[0, 0], [0.05, 0.2], [0.45, 0.001]]));
    chant(s, [NOTE.D4, NOTE.A4], 'a', 0.35, 0.4, 0.6, 0.05);
  }, { reverb: 0.4 }),
  // Silencio: a muffled pressure and the voices sinking.
  'song.dome': def('sfx', 2, (s) => {
    chant(s, [NOTE.D4, NOTE.F4, NOTE.A4], ['a', 'u'], 0, 1.1, 0.8);
    s.to(s.amp(s.osc('sine', [[0, 120], [0.5, 50]]), hit(0.05, 0.02, 0.8, 1)));
    s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', [[0, 3000], [1, 200]]), [[0, 0], [0.1, 0.5], [1.4, 0.001]]));
  }, { reverb: 0.5 }),
  // Tormenta: a high chord over a thunderclap; 'song.thunder' for each strike.
  'song.storm': def('sfx', 2, (s) => chant(s, [NOTE.A4, NOTE.C5, NOTE.E5, NOTE.A5], ['e', 'a'], 0, 1.2, 0.9), { reverb: 0.5 }),
  'song.thunder': def('sfx', 1.6, (s) => {
    s.to(s.shape(s.amp(s.filter(s.noise('white'), 'highpass', 900), hit(0, 0.001, 0.12, 1.4)), 3));
    s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', [[0, 800], [1.4, 120]]), hit(0.02, 0.01, 1.4, 1.3)));
  }, { reverb: 0.5, level: 1 }),
  // Himno, Égida, Juramento, Canto de calma: a major chord swelling, with a shimmer.
  'song.aura': def('sfx', 2.2, (s) => {
    chant(s, [NOTE.D4, NOTE.Fs4, NOTE.A4, NOTE.D5], ['o', 'a'], 0, 1.3, 0.9, 0.4);
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 6000), [[0, 0], [0.5, 0.12], [1.6, 0.001]]));
  }, { reverb: 0.55 }),
  // Healing: a chime and a soft "a".
  'song.heal': def('sfx', 1.6, (s) => {
    [NOTE.D5, NOTE.Fs4 * 2, NOTE.A4 * 2].forEach((f, i) => s.to(s.metal(i * 0.08, [f, f * 2.01], 0.9, 0.3)));
    chant(s, [NOTE.D4, NOTE.A4], 'a', 0.05, 0.8, 0.6, 0.2);
  }, { reverb: 0.5 }),
  // Último verso: a great swell of voices and a bell.
  'song.revive': def('sfx', 3.2, (s) => {
    chant(s, [NOTE.D3, NOTE.A3, NOTE.D4, NOTE.Fs4, NOTE.A4], ['u', 'a'], 0, 2, 1, 0.8);
    s.to(s.metal(1.2, [587, 1174, 1762], 1.8, 0.4));
  }, { reverb: 0.6, level: 1 }),
  // Contracanto: a quick rising "i" and a pop.
  'song.cleanse': def('sfx', 1.1, (s) => {
    chant(s, [NOTE.A4, NOTE.D5], ['u', 'i'], 0, 0.4, 0.7, 0.05);
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2500), hit(0.42, 0.002, 0.08, 0.6)));
    s.to(s.metal(0.42, [1760, 2640], 0.4, 0.3));
  }, { reverb: 0.4 }),
  // Marca del cazador: a golden ping and a whisper.
  'song.mark': def('sfx', 1.2, (s) => {
    s.to(s.metal(0, [1568, 2352, 3920], 0.9, 0.4));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 2400, 3), [[0, 0], [0.15, 0.2], [0.6, 0.001]]));
  }, { reverb: 0.4 }),
  // Hoja cantada: the slash rings with an "e".
  'song.blade': def('sfx', 0.9, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 2200), hit(0, 0.002, 0.05, 0.8)));
    s.to(s.metal(0.005, [2800, 4100], 0.5, 0.3));
    chant(s, [NOTE.E5, NOTE.A5], 'e', 0.01, 0.25, 0.5, 0.03);
  }, { reverb: 0.35 }),
};

// ---------------------------------------------------------------- the list

export const SOUNDS: Record<string, SoundDef> = {
  ...SONGS,
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
  // On a critical hit, a sharper crack and a bright sting on top of the impact.
  'impact.crit': def(
    'sfx',
    0.8,
    (s) => {
      s.to(s.amp(s.filter(s.noise('white'), 'highpass', 3000), hit(0, 0.0005, 0.04, 1)));
      s.to(s.metal(0.01, [1760, 2640, 3520], 0.6, 0.35));
      s.to(s.amp(s.osc('sine', [[0, 70], [0.25, 35]]), hit(0, 0.004, 0.25, 1)));
    },
    { reverb: 0.2 },
  ),
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
  // Overwatch: the bolt ready and a short beep from the visor.
  overwatch: def('ui', 0.5, (s) => {
    for (const at of [0, 0.08]) s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(2900), 4), hit(at, 0.001, 0.03, 0.7)));
    s.to(s.amp(s.osc('sine', 1400), hit(0.18, 0.004, 0.09, 0.3)));
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
  heal: def('sfx', 1.2, (s) => {
    [1047, 1319, 1568].forEach((f, i) => s.to(s.metal(i * 0.07, [f, f * 2.01], 0.9, 0.4)));
    s.to(s.amp(s.filter(s.noise('white'), 'highpass', 6000), [[0, 0], [0.1, 0.15], [0.9, 0.001]]));
  }, { reverb: 0.3, level: -3 }),
  shield: def('sfx', 1, (s) => {
    s.to(s.amp(s.filter(s.osc('sawtooth', 110), 'lowpass', [[0, 300], [0.4, 2200], [0.9, 600]]), [[0, 0], [0.3, 0.5], [0.95, 0.001]]));
    s.to(s.metal(0.3, [880, 1320], 0.6, 0.25));
  }, { reverb: 0.25, level: -3 }),
  // Hacking: data chirps.
  hack: def('ui', 0.8, (s) => {
    for (let i = 0; i < 9; i++) {
      const at = i * 0.07;
      s.to(s.amp(s.osc('square', s.r(600, 2400), at, 0.05), hit(at, 0.002, 0.04, 0.25)));
    }
  }),
  'hack.ok': def('ui', 0.8, (s) => {
    [1047, 1319, 1568].forEach((f, i) => s.to(s.amp(s.filter(s.osc('square', f, i * 0.11, 0.2), 'lowpass', 3000), hit(i * 0.11, 0.004, 0.16, 0.35))));
  }),
  // The alarm: a two-tone klaxon.
  alarm: def('sfx', 1.6, (s) => {
    for (let i = 0; i < 3; i++) {
      const at = i * 0.5;
      const o = s.osc('sawtooth', [[at, 520], [at + 0.24, 520], [at + 0.25, 390], [at + 0.49, 390]], at, 0.5);
      s.to(s.amp(s.filter(o, 'lowpass', 1800), [[at, 0], [at + 0.02, 0.4], [at + 0.48, 0.4], [at + 0.5, 0]]));
    }
  }, { reverb: 0.3 }),
  // Psionics: detuned voices wavering, swelling up.
  psi: def('sfx', 1.6, (s) => {
    for (const f of [440, 466, 622]) {
      const o = s.osc('sine', s.vary(f, 0.01));
      const vib = s.osc('sine', s.r(5, 7));
      const d = s.ctx.createGain();
      d.gain.value = 14;
      vib.connect(d).connect(o.frequency);
      s.to(s.amp(o, [[0, 0], [0.45, 0.35], [1.5, 0.001]]));
    }
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 600], [0.5, 3000]], 2), [[0, 0], [0.45, 0.4], [0.6, 0.001]]));
  }, { reverb: 0.5 }),
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
  // Data recovered: chirps climbing.
  data: def('ui', 0.6, (s) => {
    for (let i = 0; i < 6; i++) s.to(s.amp(s.osc('sine', 800 * Math.pow(1.25, i)), hit(i * 0.06, 0.003, 0.05, 0.3)));
  }),
  rescue: def('ui', 1.2, (s) => {
    for (const [at, f] of [
      [0, 784],
      [0.16, 1175],
    ] as const)
      s.to(s.metal(at, [f, f * 2.02], 0.9, 0.4));
  }, { reverb: 0.3 }),

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
  // The sectoid's psionic wail.
  'death.sectoid': def('sfx', 1.1, (s) => {
    const o = s.osc('sine', [[0, 950], [0.7, 210]]);
    const vib = s.osc('sine', 9);
    const d = s.ctx.createGain();
    d.gain.value = 40;
    vib.connect(d).connect(o.frequency);
    s.to(s.amp(o, [[0, 0], [0.05, 0.5], [0.8, 0.001]]));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 1500, 1), [[0, 0], [0.05, 0.25], [0.6, 0.001]]));
    death(s, 0.6);
  }, { reverb: 0.4 }),
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
  // Our turn: two radio chirps. Theirs: a low swell with a metal scrape.
  'turn.xcom': def('ui', 0.6, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 1800, 1), hit(0, 0.002, 0.08, 0.3)));
    for (const [at, f] of [
      [0.06, 880],
      [0.16, 1320],
    ] as const)
      s.to(s.amp(s.osc('sine', f), hit(at, 0.004, 0.08, 0.35)));
  }),
  'turn.alien': def('sfx', 2, (s) => {
    for (const f of [55, 58.3, 82.4]) s.to(s.amp(s.filter(s.osc('sawtooth', f), 'lowpass', [[0, 200], [0.8, 700], [1.9, 200]]), [[0, 0], [0.5, 0.35], [1.9, 0.001]]));
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', [[0, 800], [1, 2400]], 6), [[0.2, 0], [0.7, 0.15], [1.4, 0.001]]));
  }, { reverb: 0.4 }),
  // The command passed between partners: a click and a chirp.
  command: def('ui', 0.4, (s) => {
    s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 2800, 4), hit(0, 0.001, 0.02, 0.6)));
    s.to(s.amp(s.osc('sine', 1180), hit(0.05, 0.003, 0.1, 0.3)));
  }),
  victory: def('music', 3.5, (s) => {
    [[146.8, 0], [185, 0.12], [220, 0.24], [293.7, 0.36]].forEach(([f, at]) => {
      const o = s.filter(s.osc('sawtooth', f!, at!, 3, s.r(-6, 6)), 'lowpass', [[at!, 900], [at! + 0.6, 2600], [3.2, 800]]);
      s.to(s.amp(o, [[at!, 0], [at! + 0.08, 0.25], [3.3, 0.001]]));
    });
    s.to(s.metal(0.36, [1175, 1762, 2350], 2.2, 0.3));
  }, { reverb: 0.5, level: 2 }),
  defeat: def('music', 3.8, (s) => {
    [[293.7, 0], [261.6, 0.6], [233.1, 1.2], [220, 1.8]].forEach(([f, at]) => {
      const o = s.filter(s.osc('triangle', f!, at!, 1.9), 'lowpass', 1400);
      s.to(s.amp(o, [[at!, 0], [at! + 0.05, 0.35], [at! + 1.8, 0.001]]));
    });
    for (const f of [73.4, 87.3]) s.to(s.amp(s.filter(s.osc('sawtooth', f), 'lowpass', 400), [[0, 0], [0.8, 0.3], [3.6, 0.001]]));
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
