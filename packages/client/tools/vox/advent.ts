// ADVENT, hand-made: trooper, officer and lancer share a body (the soldiers'
// skeleton; lancers on the slim build) and differ in pads, visor, crest,
// cape and weapon. Dark bodysuit, oxblood armour, a smooth egg-shaped helmet
// with one glowing visor slit and a crest. The plate colour and the visor are
// repainted per type (SLOT.secondary, SLOT.visor).
//
// Tags: the build ("spine:normal"), the type ("shoulderL:officer"), the gun.

import { SLOT } from '../../src/game/vox.ts';
import { ADVENT_GUNS, SOLDIER_BUILDS, type BuildShape, type SoldierBuild } from '../../src/game/voxBodies.ts';
import { Kit, Part } from './sculpt.ts';

const S = SLOT;
const SUIT = 16;
const SUIT_DK = 17;
const ARMOUR = 18;
const ARMOUR_LT = 19;
const ARMOUR_DK = 20;
const BOOT = 21;
const SOLE = 22;
const METAL = 23;
const METAL_DK = 24;
const GOLD = 25;
const GOLD_DK = 26;
const CAPE = 27;
const CAPE_DK = 28;

const DEFAULT = new Set(['normal', 'trooper', 'rifle']);
type Add = (bone: string, tags: string[], part: (p: Part) => void, mirror?: string) => Part;

export function adventKit(): Kit {
  const kit = new Kit().colours({
    [S.secondary]: 0x7a1f2b,
    [S.secondaryLight]: 0x9a3040,
    [S.secondaryDark]: 0x561520,
    [S.visor]: [0xff4a4a, 'glow'],
    [SUIT]: 0x241f22,
    [SUIT_DK]: 0x161315,
    [ARMOUR]: 0x4d363c,
    [ARMOUR_LT]: 0x664a52,
    [ARMOUR_DK]: 0x34252a,
    [BOOT]: 0x1a161a,
    [SOLE]: 0x0e0c0e,
    [METAL]: 0x3a3f45,
    [METAL_DK]: 0x23272b,
    [GOLD]: 0xc9a13a,
    [GOLD_DK]: 0x8a6a22,
    [CAPE]: 0x5a1622,
    [CAPE_DK]: 0x3e0f18,
  });
  const add: Add = (bone, tags, part, mirror) => {
    const name = tags.length ? `${bone}:${tags.join('.')}` : bone;
    const p = new Part(name).hide(!tags.every((t) => DEFAULT.has(t)));
    part(p);
    kit.add(p);
    if (mirror) kit.add(p.mirrored(tags.length ? `${mirror}:${tags.join('.')}` : mirror));
    return p;
  };
  for (const build of ['normal', 'slim'] as SoldierBuild[]) body(add, build, SOLDIER_BUILDS[build]);
  types(add);
  add('neck', [], (p) => p.box(-1, 22, -1, 1, 22, 0, SUIT_DK));
  helmet(add);
  guns(add);
  return kit;
}

function body(add: Add, build: SoldierBuild, b: BuildShape): void {
  const t = b.torso;
  const [l0, l1] = [b.legX, b.legX + b.legW - 1];
  const [a0, a1] = [b.armX, b.armX + b.armW - 1];

  add('ankleL', [build], (p) => p.box(l0, 0, -1, l1, 0, 2, SOLE).box(l0, 1, -1, l1, 1, 2, BOOT).box(l0, 1, 2, l1, 1, 2, ARMOUR_DK), 'ankleR');
  add(
    'kneeL',
    [build],
    (p) => {
      p.box(l0, 2, -1, l1, 3, 1, BOOT).box(l0, 4, -1, l1, 6, 1, SUIT);
      // Shin guard in front, lit on its top edge.
      p.box(l0, 3, 1, l1, 5, 1, ARMOUR);
    },
    'kneeR',
  );
  add('hipL', [build], (p) => p.box(l0, 7, -1, l1, 12, 1, SUIT).box(l0, 9, 1, l1, 11, 1, ARMOUR).box(l1, 9, -1, l1, 10, 0, ARMOUR_DK), 'hipR');
  add('pivot', [build], (p) =>
    p
      .box(-t, 13, -2, t, 13, 1, SUIT)
      .box(0, 12, -1, 0, 12, 1, SUIT_DK)
      .box(-t, 14, -2, t, 14, 1, METAL_DK)
      .box(-1, 12, 2, 1, 13, 2, ARMOUR_DK)
      .set(0, 14, 2, S.secondary),
  );
  add('spine', [build], (p) => {
    p.box(-t, 15, -2, t, 16, 1, SUIT);
    // Armoured chest and back, a plate across the upper chest in the type's colour.
    p.box(-t, 17, -2, t, 20, 1, ARMOUR).box(-t, 17, -2, t, 17, 1, ARMOUR_DK);
    p.box(-t, 16, 2, t, 20, 2, ARMOUR).box(-t, 16, 2, t, 16, 2, ARMOUR_DK);
    p.box(-(t - 1), 18, 2, t - 1, 20, 2, S.secondary).box(-(t - 1), 20, 2, t - 1, 20, 2, S.secondaryLight);
    p.box(0, 16, 2, 0, 17, 2, METAL_DK);
    // Ribbed belly and a collar.
    p.box(-(t - 1), 15, 2, t - 1, 15, 2, SUIT_DK).box(-(t - 1), 16, 2, t - 1, 16, 2, ARMOUR_DK);
    p.box(-t, 21, -2, t, 21, 1, ARMOUR_DK).box(-1, 21, -1, 1, 21, 0, SUIT_DK);
    p.box(-(t - 1), 16, -3, t - 1, 20, -3, ARMOUR).box(-(t - 1), 20, -3, t - 1, 20, -3, ARMOUR_LT);
  });
  add(
    'shoulderL',
    [build],
    (p) => p.box(a0, 16, -1, a1, 20, 0, SUIT).box(a1, 17, -1, a1, 19, 0, ARMOUR),
    'shoulderR',
  );
  add(
    'elbowL',
    [build],
    (p) => p.box(a0, 14, -1, a1, 15, 0, SUIT).box(a0, 12, -1, a1, 13, 0, ARMOUR).box(a0, 13, -1, a1, 13, 0, ARMOUR_LT).box(a0, 10, -1, a1, 11, 0, BOOT),
    'elbowR',
  );
}

function types(add: Add): void {
  // Troopers: plate pads and knee pads in the type's colour.
  add('shoulderL', ['trooper'], (p) => p.box(4, 20, -1, 6, 21, 1, S.secondary).box(4, 21, -1, 6, 21, 1, S.secondaryLight).box(6, 20, -1, 6, 20, 1, S.secondaryDark), 'shoulderR');
  add('kneeL', ['trooper'], (p) => p.box(1, 6, 2, 3, 7, 2, S.secondary).box(1, 7, 2, 3, 7, 2, S.secondaryLight), 'kneeR');

  // Officers: gold pads, knee pads, a gold crest and a cape.
  add(
    'shoulderL',
    ['officer'],
    (p) => p.box(4, 20, -1, 6, 21, 1, GOLD).box(4, 21, -2, 7, 21, 2, GOLD).box(7, 21, -2, 7, 21, 2, GOLD_DK).box(4, 20, -1, 6, 20, 1, GOLD_DK),
    'shoulderR',
  );
  add('kneeL', ['officer'], (p) => p.box(1, 6, 2, 3, 7, 2, S.secondary).box(1, 7, 2, 3, 7, 2, S.secondaryLight), 'kneeR');
  add('spine', ['officer'], (p) => {
    p.box(-3, 12, -4, 3, 21, -4, CAPE);
    for (let x = -3; x <= 3; x += 2) p.box(x, 12, -4, x, 19, -4, CAPE_DK);
    p.box(-3, 21, -3, 3, 21, -3, GOLD_DK);
  });

  // Lancers: no pads, a glowing band on each forearm.
  add('elbowL', ['lancer'], (p) => p.box(3, 13, -1, 4, 13, 0, S.visor), 'elbowR');
}

function helmet(add: Add): void {
  let top = 0;
  add('head', [], (p) => {
    // An egg-shaped dome, longer to the back.
    p.ellipsoid(0, 25.2, -0.6, 3.1, 3.3, 3.7, ARMOUR);
    p.paintWhere((_x, y) => y >= 27, ARMOUR_LT);
    p.paintWhere((_x, y) => y <= 23, ARMOUR_DK);
    const front = (x: number, y: number) => {
      let z = 8;
      while (z > -8 && !p.has(x, y, z)) z--;
      return z;
    };
    // One glowing slit across the eyes, a mouth plate under it.
    for (let x = -2; x <= 2; x++) p.set(x, 25, front(x, 25), S.visor);
    for (let x = -1; x <= 1; x++) p.set(x, 23, front(x, 23), METAL_DK);
    // A crest from the brow to the back of the skull.
    for (const [, y] of p.cells.values()) top = Math.max(top, y);
    for (let z = -3; z <= 1; z++) p.set(0, top + 1, z, S.secondary);
    p.set(0, top + 1, 2, S.secondaryLight);
  });
  // The officer's crest is gold and taller.
  add('head', ['officer'], (p) => p.box(0, top + 1, -3, 0, top + 1, 2, GOLD).box(0, top + 2, -3, 0, top + 2, 1, GOLD).box(0, top + 3, -2, 0, top + 3, 0, GOLD).set(0, top + 1, 2, GOLD_DK));
}

function guns(add: Add): void {
  const x = ADVENT_GUNS.rifle.pivot[0];
  add('gun', ['rifle'], (p) =>
    p
      .box(x, 18, 3, x, 20, 3, METAL_DK)
      .box(x, 19, 4, x, 20, 4, METAL)
      .box(x, 17, 4, x, 18, 4, METAL_DK)
      .box(x, 19, 5, x, 20, 9, METAL)
      // A glowing strip along the top, in the visor's colour.
      .box(x, 21, 5, x, 21, 8, S.visor)
      .box(x, 17, 6, x, 18, 6, METAL_DK)
      .set(x, 18, 7, METAL_DK)
      .box(x, 20, 10, x, 20, 11, METAL)
      .set(x, 19, 11, METAL_DK),
  );
  const [bx, by, bz] = ADVENT_GUNS.baton.pivot;
  const y = Math.floor(by);
  add('gun', ['baton'], (p) => {
    p.box(bx, y, bz - 2, bx, y, bz + 9, METAL_DK).box(bx, y, bz - 1, bx, y, bz + 1, BOOT);
    // The glowing tip.
    const tz = bz + 10;
    p.set(bx, y, tz, S.visor).set(bx, y + 1, tz, S.visor).set(bx, y - 1, tz, S.visor).set(bx + 1, y, tz, S.visor).set(bx - 1, y, tz, S.visor).set(bx, y, tz + 1, S.visor);
  });
}
