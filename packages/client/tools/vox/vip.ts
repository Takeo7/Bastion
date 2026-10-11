// The VIP, hand-made: a resistance contact in a long coat, a scarf in the
// squad's colour (SLOT.team), glasses, and a briefcase in the right hand.
// The soldiers' skeleton, unarmed.

import { SLOT } from '../../src/game/vox.ts';
import { Kit, Part } from './sculpt.ts';

const COAT = 16;
const COAT_DK = 17;
const TROUSERS = 18;
const SHOE = 19;
const SKIN = 20;
const SKIN_DK = 21;
const HAIR = 22;
const GLASSES = 23;
const SHIRT = 24;
const CASE = 25;
const CASE_DK = 26;

export function vipKit(): Kit {
  const kit = new Kit().colours({
    [SLOT.team]: 0x3fa9ff,
    [COAT]: 0x343a46,
    [COAT_DK]: 0x262b34,
    [TROUSERS]: 0x2a2e36,
    [SHOE]: 0x3a2a20,
    [SKIN]: 0xc89f84,
    [SKIN_DK]: 0xa88066,
    [HAIR]: 0x4a3a30,
    [GLASSES]: 0x1d2124,
    [SHIRT]: 0xd8d8d0,
    [CASE]: 0x5a4030,
    [CASE_DK]: 0x3a2a1e,
  });

  const shoe = new Part('ankleL').box(1, 0, -1, 3, 1, 2, SHOE).box(1, 0, -1, 3, 0, 2, CASE_DK);
  const shin = new Part('kneeL').box(1, 2, -1, 3, 6, 1, TROUSERS);
  // The coat's tails hang from the hips and swing with the legs.
  const thigh = new Part('hipL').box(1, 7, -1, 3, 12, 1, TROUSERS).box(1, 7, -2, 3, 12, -2, COAT).box(3, 7, -1, 3, 12, 2, COAT).box(1, 7, 2, 2, 12, 2, COAT).box(1, 7, -2, 3, 7, 2, COAT_DK);
  kit.add(shoe, shoe.mirrored('ankleR'), shin, shin.mirrored('kneeR'), thigh, thigh.mirrored('hipR'));

  kit.part('pivot').box(-3, 13, -2, 3, 14, 2, COAT).box(0, 12, -1, 0, 12, 1, TROUSERS);
  const spine = kit.part('spine');
  // Coat buttoned over a white shirt, lapels darker, the scarf round the neck.
  spine.box(-3, 15, -2, 3, 21, 2, COAT);
  spine.box(0, 17, 2, 0, 20, 2, SHIRT).box(-1, 19, 2, -1, 20, 2, COAT_DK).box(1, 19, 2, 1, 20, 2, COAT_DK);
  spine.box(-2, 21, -2, 2, 21, 1, SLOT.team).box(1, 17, 3, 1, 20, 3, SLOT.team);
  kit.part('neck').box(-1, 22, -1, 1, 22, 0, SKIN_DK);

  const upper = new Part('shoulderL').box(4, 16, -1, 5, 20, 0, COAT).box(4, 20, -1, 5, 20, 0, COAT_DK);
  const fore = new Part('elbowL').box(4, 12, -1, 5, 15, 0, COAT).box(4, 12, -1, 5, 12, 0, SHIRT).box(4, 10, -1, 5, 11, 0, SKIN);
  // The briefcase hangs from the right hand.
  const foreR = fore.mirrored('elbowR').box(-5, 5, -2, -4, 9, 2, CASE).box(-5, 9, -1, -4, 9, 1, CASE_DK).box(-5, 7, -2, -4, 7, 2, CASE_DK);
  kit.add(upper, upper.mirrored('shoulderR'), fore, foreR);

  kit
    .part('head')
    .box(-2, 23, -2, 2, 26, 2, SKIN)
    .box(-2, 27, -2, 2, 27, 1, HAIR)
    .box(-2, 24, -2, 2, 26, -2, HAIR)
    .box(-2, 26, -1, -2, 26, 1, HAIR)
    .box(2, 26, -1, 2, 26, 1, HAIR)
    // Glasses, a nose, ears.
    .box(-2, 25, 3, 2, 25, 3, GLASSES)
    .set(0, 24, 3, SKIN_DK)
    .set(0, 23, 2, SKIN_DK)
    .set(-3, 24, 0, SKIN)
    .set(3, 24, 0, SKIN);
  return kit;
}
