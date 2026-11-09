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
};
