// The xenoid, hand-made: a tall thin grey with a long skull, big green eyes,
// three-fingered hands, a dark harness and a small emitter in its right hand.
// Joints in voxBodies.ts (XENOID).

import { XENOID } from '../../src/game/voxBodies.ts';
import { Kit, Part } from './sculpt.ts';

const SKIN_LT = 16;
const SKIN = 17;
const SKIN_DK = 18;
const PLATE = 19;
const PLATE_LT = 20;
const EYE = 21;
const EYE_GLOW = 22;
const GLOW = 23;
const CLAW = 24;

export function xenoidKit(): Kit {
  const kit = new Kit().colours({
    [SKIN_LT]: 0xa2a9b3,
    [SKIN]: 0x8a9099,
    [SKIN_DK]: 0x6d737d,
    [PLATE]: 0x2a2e33,
    [PLATE_LT]: 0x3d434a,
    [EYE]: 0x0f1a12,
    [EYE_GLOW]: [0x7dff9a, 'glow'],
    [GLOW]: [0x9dffb0, 'glow'],
    [CLAW]: 0x50555d,
  });

  // Legs: thin, two voxels wide, long feet.
  const foot = new Part('ankleL').box(1, 0, -1, 2, 0, 2, SKIN_DK);
  const shin = new Part('kneeL').box(1, 1, -1, 2, 5, 0, SKIN).box(1, 1, -1, 2, 1, 0, SKIN_DK).box(1, 5, 1, 2, 5, 1, SKIN_LT);
  const thigh = new Part('hipL').box(1, 6, -1, 2, 10, 0, SKIN).box(2, 6, -1, 2, 10, -1, SKIN_DK);
  kit.add(foot, foot.mirrored('ankleR'), shin, shin.mirrored('kneeR'), thigh, thigh.mirrored('hipR'));

  kit.part('pivot').box(-2, 11, -1, 2, 12, 0, SKIN_DK).box(-2, 12, -1, 2, 12, 0, PLATE);
  kit
    .part('spine')
    // A narrow waist and a long chest, a diagonal harness plate.
    .box(-1, 13, -1, 1, 14, 0, SKIN_DK)
    .box(-2, 15, -1, 2, 18, 1, SKIN)
    .box(-2, 15, -1, 2, 15, 1, SKIN_DK)
    .box(-1, 16, 1, 1, 17, 1, SKIN_LT)
    .line(-2, 15, 2, 2, 18, 2, PLATE)
    .line(-2, 16, 2, 1, 18, 2, PLATE)
    .box(-2, 18, -1, 2, 18, 1, PLATE)
    .box(-2, 18, -1, -2, 18, 1, PLATE_LT)
    .box(-1, 15, -2, 1, 17, -2, PLATE);
  kit.part('neck').box(0, 19, -1, 0, 20, 0, SKIN_DK);

  // Long arms with three-fingered hands.
  const upper = new Part('shoulderL').box(3, 13, 0, 3, 17, 0, SKIN).box(3, 17, -1, 3, 18, 0, PLATE);
  const fore = new Part('elbowL')
    .box(3, 9, 0, 3, 12, 0, SKIN)
    .box(3, 8, -1, 3, 8, 1, SKIN_DK)
    .box(3, 6, -1, 3, 7, -1, CLAW)
    .box(3, 6, 1, 3, 7, 1, CLAW)
    .box(3, 5, 0, 3, 7, 0, CLAW);
  kit.add(upper, upper.mirrored('shoulderR'), fore, fore.mirrored('elbowR'));

  // A long skull swept back, a small chin, big slanted eyes.
  const head = kit.part('head').ellipsoid(0, 24.4, -1.2, 3.2, 3.6, 4.4, SKIN).ellipsoid(0, 22.2, 0.8, 2.2, 1.8, 2.2, SKIN).smooth();
  head.paintWhere((_x, y) => y >= 27, SKIN_LT);
  head.paintWhere((_x, y, z) => y <= 21 || z <= -5, SKIN_DK);
  const front = (x: number, y: number) => {
    let z = 8;
    while (z > -8 && !head.has(x, y, z)) z--;
    return z;
  };
  for (const s of [1, -1]) {
    for (const [x, y] of [
      [1, 23],
      [2, 23],
      [2, 24],
    ] as const)
      head.set(s * x, y, front(s * x, y), EYE);
    head.set(s * 2, 23, front(s * 2, 23), EYE_GLOW);
  }
  head.set(0, 21, front(0, 21), SKIN_DK);

  // A small emitter in the right hand, its strip glowing.
  const [x, y, z] = XENOID.gun.pivot;
  const r = Math.floor(y);
  kit
    .part('gun:emitter')
    .box(x, r, z, x, r, z + 3, PLATE)
    .box(x, r + 1, z + 1, x, r + 1, z + 2, GLOW)
    .set(x, r - 1, z, PLATE_LT)
    .set(x, r, z + 4, GLOW);
  return kit;
}
