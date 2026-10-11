// The zombie, hand-made: a raised corpse in the rags of a uniform, grey-green
// skin showing through, a scrap of armour on one shoulder, violet eyes, a
// hanging jaw and long pale claws. The soldiers' skeleton; the game hunches it
// and drags its feet.

import { Kit, Part } from './sculpt.ts';

const SKIN = 16;
const SKIN_DK = 17;
const RAG = 18;
const RAG_DK = 19;
const REMNANT = 20;
const BOOT = 21;
const CLAW = 22;
const EYE = 23;
const MOUTH = 24;
const BONE = 25;

export function zombieKit(): Kit {
  const kit = new Kit().colours({
    [SKIN]: 0x7c8a72,
    [SKIN_DK]: 0x5e6b56,
    [RAG]: 0x3d4239,
    [RAG_DK]: 0x2b2f28,
    [REMNANT]: 0x56605a,
    [BOOT]: 0x26231f,
    [CLAW]: 0xcfd2c4,
    [EYE]: [0xd29aff, 'glow'],
    [MOUTH]: 0x1e1814,
    [BONE]: 0xb9b8a6,
  });

  // Legs: the left still booted and in rags, the right bare and torn.
  kit.add(new Part('ankleL').box(1, 0, -1, 3, 1, 2, BOOT), new Part('ankleR').box(-3, 0, -1, -1, 0, 1, SKIN_DK).box(-3, 0, 2, -1, 0, 2, CLAW));
  kit.add(
    new Part('kneeL').box(1, 2, -1, 3, 3, 1, BOOT).box(1, 4, -1, 3, 6, 1, RAG).box(1, 6, 2, 3, 7, 2, REMNANT).set(2, 5, 1, RAG_DK),
    new Part('kneeR').box(-3, 1, -1, -1, 6, 1, SKIN).box(-3, 5, -1, -1, 6, 1, RAG_DK).set(-2, 3, 1, SKIN_DK).set(-1, 2, -1, BONE),
  );
  const thigh = new Part('hipL').box(1, 7, -1, 3, 12, 1, RAG).set(3, 9, 0, RAG_DK).set(1, 8, 1, RAG_DK);
  kit.add(thigh, thigh.mirrored('hipR').set(-2, 8, 1, SKIN).set(-3, 8, 0, SKIN));

  kit.part('pivot').box(-3, 13, -2, 3, 14, 1, RAG).box(-3, 14, -2, 3, 14, 1, RAG_DK).box(0, 12, -1, 0, 12, 1, RAG_DK).set(2, 13, 2, REMNANT);
  const spine = kit.part('spine');
  // A torn shirt: the ribs show through a hole, a scrap of armour on the right shoulder.
  spine.box(-3, 15, -2, 3, 21, 1, RAG).box(-3, 21, -2, 3, 21, 1, RAG_DK);
  spine.box(-1, 16, 2, 2, 19, 2, SKIN).box(-1, 17, 2, 2, 17, 2, SKIN_DK).box(-1, 19, 2, 2, 19, 2, SKIN_DK);
  spine.box(-3, 15, 2, -2, 18, 2, RAG).set(-2, 20, 2, RAG_DK).set(3, 16, 2, RAG_DK);
  spine.box(-4, 20, -2, -2, 21, 1, REMNANT).set(-4, 19, 0, REMNANT);
  spine.box(-1, 16, -3, 1, 18, -3, RAG_DK);
  kit.part('neck').box(-1, 22, -1, 1, 22, 0, SKIN_DK);

  // Arms: a ragged sleeve on the left, bare on the right, long claws on both.
  kit.add(
    new Part('shoulderL').box(4, 16, -1, 5, 20, 0, RAG).box(4, 16, -1, 5, 16, 0, RAG_DK),
    new Part('shoulderR').box(-5, 16, -1, -4, 20, 0, SKIN).set(-5, 18, 0, SKIN_DK),
  );
  const fore = new Part('elbowL').box(4, 12, -1, 5, 15, 0, SKIN).set(4, 14, 0, SKIN_DK).box(4, 11, -1, 5, 11, 0, SKIN_DK);
  for (const z of [-1, 0]) fore.box(4, 8, z, 4, 10, z, CLAW);
  fore.box(5, 9, -1, 5, 10, -1, CLAW);
  kit.add(fore, fore.mirrored('elbowR'));

  // A bald head, sunken glowing eyes, the jaw hanging open.
  kit
    .part('head')
    .box(-2, 23, -2, 2, 27, 2, SKIN)
    .box(-2, 27, -2, 2, 27, 1, SKIN_DK)
    .set(-1, 25, 2, EYE)
    .set(1, 25, 2, EYE)
    .box(-1, 23, 2, 1, 24, 2, MOUTH)
    .box(-1, 22, 1, 1, 22, 2, SKIN_DK)
    .set(2, 26, 1, SKIN_DK)
    .set(-2, 24, -2, SKIN_DK);
  return kit;
}
