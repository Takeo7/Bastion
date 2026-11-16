// Recipes made for Unity, in sounds.ts's way (its Synth, rendered and evened out as bank.ts does): packages/ stays
// frozen at the 0.10.1, so a sound that has to fit Unity's figures goes here, under a name of its own. render.ts renders
// them with the 0.10.1's; CombatSound picks them, and falls back to the 0.10.1's where the folder doesn't have them yet.
import type { SoundDef } from '../../../packages/client/src/audio/sounds';
import { hit, type Synth } from '../../../packages/client/src/audio/synth';

export const UNITY_SOUNDS: Record<string, SoundDef> = {
  // The rifle's reload (Mixamo's «Reload 3» over 1.3 s since the review of 9/11; Arte's timings from its start): sounds.ts
  // reload's clicks, its magazine out where the left hand takes it (0.15 s, not 0.36), a pouch as the hand goes down with
  // it (0.30), the new one in at 0.72 and the hand on the gun at 0.80. The pistol's («Reloading 2»: out at 0.42, in at
  // 0.72, the slide at 0.78) keeps the 0.10.1's.
  'reload.rifle': {
    bus: 'sfx',
    dur: 1,
    level: -4,
    build: (s: Synth) => {
      const click = (at: number, f: number, l: number) => {
        s.to(s.amp(s.filter(s.noise('white'), 'bandpass', s.vary(f), 4), hit(at, 0.001, 0.03, l)));
        s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', 420, 2), hit(at, 0.002, 0.05, l * 0.6)));
      };
      click(0, 2600, 0.6);
      s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 1800, 1.5), [[0.03, 0], [0.08, 0.15], [0.14, 0.001]]));
      click(0.15, 1900, 1);
      s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 1500, 1.5), [[0.28, 0], [0.34, 0.1], [0.46, 0.001]]));
      click(0.72, 3200, 0.7);
      click(0.8, 2400, 0.8);
    },
  },
  // The silent takedown (Arturo, 16/11; the 0.12.26): from behind, on one of the dead that doesn't see it coming, with a
  // blade or a blunt weapon (Arte's blow: the knife into the nape, the throat cut, the blow on the nape). A dull blow,
  // short and low, with no whoosh of a swing and no ring: the body's thump and the blow muffled in flesh, nothing over
  // 1 kHz to speak of, and well under a melee hit (melee.sword), so that it reads as done without a sound.
  'melee.takedown': {
    bus: 'sfx',
    dur: 0.35,
    level: -8,
    variants: 3,
    reverb: 0.03,
    build: (s: Synth) => {
      s.to(s.amp(s.osc('sine', [[0, s.vary(85)], [0.11, 42]], 0, 0.25), hit(0, 0.004, 0.13, 1)));
      s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', s.vary(600), 0.7), hit(0.002, 0.003, 0.08, 0.9)));
      // The knock into flesh, a few hundred Hz: a laptop's or a phone's speakers give little under 200 Hz.
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(320), 1.4), hit(0.004, 0.003, 0.07, 1.8)));
      s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 300), hit(0.03, 0.02, 0.12, 0.25)));
    },
  },
  // El Toro, the boss of chapter 1 (H6, Gameplay 16/11; .dev-snapshots/gameplay/runs/H6-TORO.md): a Brute of 30 health that
  // roars once on seeing the squad, charges in a line and pounds with both fists. Its roar is the Hinchado's recorded voice
  // played deeper (CombatSound.Roar) over this growl of its own, a throat bigger than any of the dead's; without the
  // recording, this alone.
  'boss.roar': {
    bus: 'sfx',
    dur: 1.9,
    level: 1,
    variants: 2,
    reverb: 0.35,
    build: (s: Synth) => {
      const rise: [number, number][] = [[0, 0], [0.2, 1], [1.15, 0.75], [1.8, 0.001]];
      const pitch: [number, number][] = [[0, s.vary(68)], [0.3, s.vary(92)], [1.2, 78], [1.8, 52]];
      s.to(s.amp(s.filter(s.shape(s.osc('sawtooth', pitch), 4), 'lowpass', 850, 0.9), rise));
      s.to(s.amp(s.filter(s.osc('sawtooth', pitch, 0, 1.9, 30), 'lowpass', 600, 0.7), rise), 0.6);
      // The rasp of the breath through it, opening and closing.
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', [[0, 450], [0.35, 950], [1.7, 380]], 1.3), rise), 0.7);
      s.to(s.amp(s.osc('sine', [[0, 46], [1.8, 34]]), rise), 0.5);
    },
  },
  // Its feet, walking or charging (CombatSound.Step for a «brute»): heavy, a body of 1.25 times a man's (Arte) on the ground.
  'boss.step': {
    bus: 'sfx',
    dur: 0.45,
    level: -4,
    variants: 4,
    reverb: 0.1,
    build: (s: Synth) => {
      s.to(s.amp(s.osc('sine', [[0, s.vary(70)], [0.16, 38]]), hit(0, 0.004, 0.2, 0.55)));
      s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 420, 0.7), hit(0, 0.003, 0.12, 0.5)));
      // The knock and the grit under the foot: what a laptop's or a phone's speakers give of it (little under 200 Hz).
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(300), 1.3), hit(0.002, 0.003, 0.07, 1.4)));
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(1700), 1), hit(0.005, 0.002, 0.05, 0.25)));
    },
  },
  // The charge's end (CombatSound.ChargeHit): its whole weight into a soldier, who goes down. The loudest of its sounds, a
  // body blow and the kit on the soldier rattling.
  'boss.impact': {
    bus: 'sfx',
    dur: 0.75,
    level: 2,
    variants: 2,
    reverb: 0.2,
    build: (s: Synth) => {
      s.to(s.amp(s.osc('sine', [[0, s.vary(92)], [0.25, 34]]), hit(0, 0.003, 0.35, 0.6)));
      s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 650, 0.7), hit(0, 0.003, 0.2, 0.6)));
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(320), 1.4), hit(0.002, 0.003, 0.12, 2.2)));
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(550), 1.2), hit(0.003, 0.003, 0.09, 1)));
      s.to(s.amp(s.filter(s.noise('white'), 'bandpass', 2200, 2), hit(0.004, 0.002, 0.06, 0.35)));
      s.to(s.filter(s.grains(0.01, 0.16, 8, 0.004, false), 'bandpass', 1400, 0.8), 0.35);
    },
  },
  // Mazazo (CombatSound.Pound, the «pound» attack): both fists down together, a pair of blows a hair apart, heavier than
  // any weapon's knock. The swing before it, if any, is Melee's.
  'boss.pound': {
    bus: 'sfx',
    dur: 0.55,
    level: 1,
    variants: 3,
    reverb: 0.15,
    build: (s: Synth) => {
      s.to(s.amp(s.osc('sine', [[0, s.vary(82)], [0.18, 40]]), hit(0, 0.003, 0.22, 0.6)));
      s.to(s.amp(s.osc('sine', [[0.025, s.vary(76)], [0.2, 40]], 0.025), hit(0.025, 0.003, 0.2, 0.45)));
      s.to(s.amp(s.filter(s.noise('brown'), 'lowpass', 520, 0.7), hit(0, 0.003, 0.14, 0.6)));
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(300), 1.4), hit(0.002, 0.003, 0.1, 2.4)));
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(500), 1.2), hit(0.027, 0.003, 0.08, 0.8)));
    },
  },
  // A soldier knocked down by the charge (CombatSound.KnockedDown): the body on the ground, then its limbs and kit after it.
  'body.down': {
    bus: 'sfx',
    dur: 0.6,
    level: -3,
    variants: 3,
    reverb: 0.1,
    build: (s: Synth) => {
      s.to(s.amp(s.osc('sine', [[0, s.vary(75)], [0.12, 45]]), hit(0, 0.004, 0.14, 0.5)));
      s.to(s.amp(s.osc('sine', [[0.09, s.vary(62)], [0.2, 42]], 0.09), hit(0.09, 0.004, 0.1, 0.3)));
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(350), 1.2), hit(0, 0.004, 0.08, 1)));
      s.to(s.amp(s.filter(s.noise('pink'), 'bandpass', s.vary(420), 1.2), hit(0.09, 0.004, 0.06, 0.6)));
      s.to(s.amp(s.filter(s.noise('pink'), 'lowpass', 1200), hit(0, 0.01, 0.2, 0.3)));
      s.to(s.filter(s.grains(0, 0.25, 10, 0.006, false), 'bandpass', 2500, 0.8), 0.35);
    },
  },
};
