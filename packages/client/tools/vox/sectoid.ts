// The sectoid, hand-made: a small grey psionic with a huge skull, black
// slanted eyes with a violet glint, thin limbs, a harness with a psionic gem
// and a plasma sidearm. About 26 voxels tall, the skull nine wide.
//
//   joints (voxels): ankles y 1 · knees y 5 · hips (±1.5, 10) · pelvis y 11
//   spine y 12 · shoulders (±3, 16) · elbows y 12 · neck y 17 · head y 18

import { SECTOID } from '../../src/game/voxBodies.ts';
import { Kit, Part } from './sculpt.ts';

const SKIN_LT = 16;
const SKIN = 17;
const SKIN_DK = 18;
const SKIN_DKR = 19;
const HARNESS = 20;
const HARNESS_LT = 21;
const GEM = 22;
const EYE = 23;
const EYE_GLOW = 24;
const GUN = 25;
const GUN_GLOW = 26;
const GUN_DK = 27;

export function sectoidKit(): Kit {
  const kit = new Kit().colours({
    [SKIN_LT]: 0xbcc1ca,
    [SKIN]: 0xa3a8b2,
    [SKIN_DK]: 0x878c97,
    [SKIN_DKR]: 0x6c717c,
    [HARNESS]: 0x3c2c4c,
    [HARNESS_LT]: 0x5a4570,
    [GEM]: [0xd29aff, 'glow'],
    [EYE]: 0x16131d,
    [EYE_GLOW]: [0xc48aff, 'glow'],
    [GUN]: 0x4a3f5a,
    [GUN_GLOW]: [0xd29aff, 'glow'],
    [GUN_DK]: 0x2a2433,
  });

  // ---------------------------------------------------------------- legs
  const foot = new Part('ankleL').box(1, 0, -1, 2, 0, 1, SKIN_DK).set(1, 0, 2, SKIN_DKR).set(2, 0, 2, SKIN_DKR);
  const shin = new Part('kneeL')
    .box(1, 1, -1, 2, 4, 0, SKIN)
    .box(1, 1, -1, 2, 1, 0, SKIN_DK)
    // Knobbly knee standing out in front.
    .box(1, 4, 1, 2, 4, 1, SKIN_LT);
  const thigh = new Part('hipL').box(1, 5, -1, 2, 9, 0, SKIN).box(2, 5, -1, 2, 9, -1, SKIN_DK);
  kit.add(foot, foot.mirrored('ankleR'), shin, shin.mirrored('kneeR'), thigh, thigh.mirrored('hipR'));

  // ---------------------------------------------------------------- body
  kit
    .part('pivot')
    .box(-2, 10, -1, 2, 10, 0, SKIN_DK)
    .box(-2, 11, -1, 2, 11, 0, HARNESS)
    .set(0, 11, 1, HARNESS_LT);
  kit
    .part('spine')
    // Narrow waist, a rib cage, a collar of harness with plates on the shoulders.
    .box(-1, 12, -1, 1, 12, 0, SKIN_DK)
    .box(-2, 13, -1, 2, 15, 1, SKIN)
    .box(-2, 13, -1, 2, 13, 1, SKIN_DK)
    .box(-1, 14, 1, 1, 14, 1, SKIN_LT)
    .box(-2, 16, -1, 2, 16, 1, HARNESS)
    .box(-2, 16, -1, -2, 16, 1, HARNESS_LT)
    .box(2, 16, -1, 2, 16, 1, HARNESS_LT)
    .box(0, 13, 2, 0, 15, 2, HARNESS)
    .set(0, 14, 2, GEM)
    .box(-1, 13, -2, 1, 15, -2, HARNESS);
  kit.part('neck').box(0, 17, -1, 0, 17, 0, SKIN_DK);

  // ---------------------------------------------------------------- arms
  const upper = new Part('shoulderL').box(3, 12, 0, 3, 15, 0, SKIN).set(3, 15, -1, SKIN_DK).set(3, 16, 0, HARNESS_LT);
  const fore = new Part('elbowL')
    .box(3, 8, 0, 3, 11, 0, SKIN)
    .set(3, 11, -1, SKIN_DK)
    // Hand and two long fingers.
    .set(3, 7, 0, SKIN_DK)
    .box(3, 5, -1, 3, 6, -1, SKIN_DKR)
    .box(3, 5, 1, 3, 6, 1, SKIN_DKR);
  kit.add(upper, upper.mirrored('shoulderR'), fore, fore.mirrored('elbowR'));

  // ---------------------------------------------------------------- head
  // A swollen cranium over a small face, ending in a narrow chin.
  const head = kit.part('head').ellipsoid(0, 22.4, -0.6, 4.5, 3.7, 4.5, SKIN).ellipsoid(0, 20.2, 0.9, 2.7, 2.4, 2.8, SKIN).smooth();
  head.box(0, 18, 1, 0, 18, 2, SKIN_DK);
  // Shading: light on top, darker underneath and behind.
  head.paintWhere((_x, y) => y >= 25, SKIN_LT);
  head.paintWhere((_x, y, z) => y <= 19 || z <= -4, SKIN_DK);
  // Eyes: big black almonds slanting up and out, each with a violet glint.
  const front = (x: number, y: number) => {
    let z = 8;
    while (z > -8 && !head.has(x, y, z)) z--;
    return z;
  };
  for (const s of [1, -1]) {
    for (const [x, y] of [
      [1, 20],
      [2, 20],
      [2, 21],
      [3, 21],
      [1, 21],
    ] as const)
      head.set(s * x, y, front(s * x, y), EYE);
    head.set(s * 2, 21, front(s * 2, 21), EYE_GLOW);
  }
  head.set(0, 19, front(0, 19), SKIN_DKR);

  // ---------------------------------------------------------------- pistol
  // Level in front of the chest in the bind pose, from its pivot (voxBodies.ts).
  const [x, y, z] = SECTOID.gun.pivot;
  kit
    .part('gun:pistol')
    .box(x, y - 2, z, x, y - 1, z, GUN_DK)
    .box(x, y, z, x, y, z + 3, GUN)
    .box(x, y + 1, z + 1, x, y + 1, z + 2, GUN_GLOW)
    .set(x, y, z + 4, GUN_DK)
    .set(x, y - 1, z + 2, GUN_DK);
  return kit;
}
