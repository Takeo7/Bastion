// The MEC, hand-made: a walking gun platform. Oxblood hull over a dark frame,
// reverse-jointed legs (drawn straight; the game bends them), a missile pod on
// the left shoulder with four glowing tubes, a fist on the left arm and an
// arm-cannon for a right arm. Joints in voxBodies.ts (MEC).

import { MEC } from '../../src/game/voxBodies.ts';
import { Kit, Part } from './sculpt.ts';

const HULL = 16;
const HULL_LT = 17;
const HULL_DK = 18;
const FRAME = 19;
const FRAME_DK = 20;
const METAL = 21;
const GLOW = 22;
const PISTON = 23;

export function mecKit(): Kit {
  const kit = new Kit().colours({
    [HULL]: 0x5a2a30,
    [HULL_LT]: 0x76383f,
    [HULL_DK]: 0x401d22,
    [FRAME]: 0x3a3d42,
    [FRAME_DK]: 0x26282c,
    [METAL]: 0x2a2e33,
    [GLOW]: [0xff4a4a, 'glow'],
    [PISTON]: 0x8d949b,
  });

  // Legs: big feet, a frame shin with a piston, an armoured knee, a frame thigh.
  const foot = new Part('ankleL').box(3, 0, -2, 5, 1, 3, METAL).box(3, 0, 3, 5, 0, 4, FRAME_DK).box(3, 0, -3, 5, 0, -3, FRAME_DK);
  const shin = new Part('kneeL')
    .box(3, 2, -1, 5, 7, 1, FRAME)
    .box(4, 3, -2, 4, 7, -2, PISTON)
    .box(2, 8, -2, 6, 10, 2, HULL)
    .box(2, 10, -2, 6, 10, 2, HULL_LT)
    .box(2, 8, -2, 6, 8, 2, HULL_DK);
  const thigh = new Part('hipL').box(3, 11, -1, 5, 15, 1, FRAME).box(4, 11, 2, 4, 14, 2, PISTON).box(3, 15, -1, 5, 15, 1, FRAME_DK);
  kit.add(foot, foot.mirrored('ankleR'), shin, shin.mirrored('kneeR'), thigh, thigh.mirrored('hipR'));

  kit.part('pivot').box(-5, 16, -3, 5, 18, 2, FRAME).box(-5, 16, -3, 5, 16, 2, FRAME_DK).box(-2, 17, 3, 2, 18, 3, HULL_DK);

  const spine = kit.part('spine');
  // Waist frame, then the hull: lit on top, darker underneath, a frame plate in front.
  spine.box(-4, 19, -3, 4, 21, 2, FRAME);
  spine.box(-6, 22, -4, 6, 30, 4, HULL).box(-6, 30, -4, 6, 30, 4, HULL_LT).box(-6, 22, -4, 6, 22, 4, HULL_DK);
  for (const x of [-6, 6]) for (const z of [-4, 4]) spine.box(x, 22, z, x, 30, z, HULL_DK);
  spine.box(-4, 23, 5, 4, 28, 5, FRAME).box(-3, 24, 5, 3, 27, 5, FRAME_DK).box(-2, 25, 6, 2, 26, 6, METAL);
  // Vents on the back.
  for (const y of [24, 26, 28]) spine.box(-4, y, -5, 4, y, -5, FRAME_DK);
  // Missile pod over the left shoulder, four glowing tubes.
  spine.box(5, 31, -3, 10, 34, 3, FRAME).box(5, 34, -3, 10, 34, 3, FRAME_DK);
  for (const [x, y] of [
    [6, 32],
    [9, 32],
    [6, 33],
    [9, 33],
  ])
    spine.set(x, y, 4, GLOW);

  kit.part('neck').box(-2, 31, -2, 2, 31, 2, FRAME_DK);
  // A low wide head with one red slit and an antenna.
  kit
    .part('head')
    .box(-3, 32, -3, 3, 34, 3, FRAME)
    .box(-3, 34, -3, 3, 34, 3, HULL)
    .box(-2, 33, 4, 2, 33, 4, GLOW)
    .box(-3, 32, 4, 3, 32, 4, FRAME_DK)
    .box(2, 35, -2, 2, 37, -2, PISTON);

  // Left arm: frame and a big fist.
  kit.part('shoulderL').box(7, 22, -1, 8, 27, 1, FRAME).box(7, 26, -2, 9, 29, 2, HULL).box(7, 29, -2, 9, 29, 2, HULL_LT);
  kit.part('elbowL').box(7, 18, -1, 8, 21, 1, FRAME).box(6, 14, -2, 9, 17, 2, METAL).box(6, 17, -2, 9, 17, 2, FRAME);

  // The right arm is the gun: a housing at the shoulder and a long barrel.
  const [gx, gy] = MEC.gun.pivot;
  const g = kit.part('gun:cannon');
  g.box(gx - 2, gy - 2.5, -3, gx + 1, gy + 1.5, 3, HULL).box(gx - 2, gy + 1.5, -3, gx + 1, gy + 1.5, 3, HULL_LT);
  g.box(gx - 1, gy - 2.5, 4, gx + 1, gy - 0.5, 12, METAL).box(gx - 1, gy - 0.5, 5, gx + 1, gy - 0.5, 8, FRAME);
  g.box(gx - 1, gy - 2.5, 13, gx + 1, gy - 0.5, 13, FRAME_DK).set(gx, gy - 1.5, 13, GLOW);
  return kit;
}
