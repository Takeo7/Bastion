// The relay, hand-made: the objective of sabotage missions. An octagonal
// plinth on three struts, a tapering mast with collars, a dish, a glowing
// core on top and a ring that spins around the mast. Its parts hang on the
// pelvis (plinth), the spine (mast) and a "ring" that the game turns.

import { RELAY } from '../../src/game/voxBodies.ts';
import { Kit } from './sculpt.ts';

const DARK = 16;
const STEEL = 17;
const STEEL_LT = 18;
const GLOW = 19;
const GLOW_DK = 20;

export function relayKit(): Kit {
  const kit = new Kit().colours({
    [DARK]: 0x2a1e26,
    [STEEL]: 0x3a2a34,
    [STEEL_LT]: 0x52404b,
    [GLOW]: [0xff8aa0, 'glow'],
    [GLOW_DK]: [0xff6a8a, 'glow'],
  });

  // Plinth: an octagon two steps high, on three struts.
  const plinth = kit.part('pivot');
  for (let x = -6; x <= 6; x++) for (let z = -6; z <= 6; z++) if (Math.abs(x) + Math.abs(z) <= 9) plinth.box(x, 0, z, x, 2, z, DARK);
  for (let x = -4; x <= 4; x++) for (let z = -4; z <= 4; z++) if (Math.abs(x) + Math.abs(z) <= 6) plinth.set(x, 3, z, STEEL);
  for (let k = 0; k < 3; k++) {
    const a = (k / 3) * Math.PI * 2 + 0.4;
    plinth.line(Math.round(Math.cos(a) * 6), 0, Math.round(Math.sin(a) * 6), Math.round(Math.cos(a) * 9), 0, Math.round(Math.sin(a) * 9), STEEL);
  }

  // Mast: thick at the base, thin above, with dark collars; a dish to one side; the core on top.
  const spine = kit.part('spine');
  spine.box(-2, 4, -2, 2, 12, 2, STEEL).box(-1, 13, -1, 1, 30, 1, STEEL);
  for (const y of [8, 15, 21, 27]) spine.box(-2, y, -2, 2, y, 2, DARK);
  spine.box(-2, 12, -2, 2, 12, 2, STEEL_LT);
  spine.ellipsoid(3, 23.5, 2, 3, 0.8, 3, DARK).box(2, 23, 1, 2, 24, 1, STEEL);
  const c = RELAY.core;
  for (let y = c - 4; y <= c + 4; y++) {
    const r = 2.6 - Math.abs(y - c) * 0.6;
    for (let x = -2; x <= 2; x++) for (let z = -2; z <= 2; z++) if (Math.abs(x) + Math.abs(z) <= r) spine.set(x, y, z, GLOW);
  }
  spine.set(0, c + 5, 0, GLOW);

  // The ring, centred on the mast so it can spin.
  const ring = kit.part('ring');
  for (let x = -7; x <= 7; x++)
    for (let z = -7; z <= 7; z++) {
      const d = Math.hypot(x, z);
      if (d > 5.4 && d < 6.6) ring.set(x, RELAY.ring, z, GLOW_DK);
    }
  return kit;
}
