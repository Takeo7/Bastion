// The XCOM soldier, hand-made: one model per bone of the figure skeleton,
// with the joints of game/voxBodies.ts. About 28 voxels tall.
//
// The kit holds every variant, each tagged after the colon: the body of each
// build ("spine:normal"), armour that goes over it ("spine:normal.a2", tier 3
// adds "a3" on top), each class's gear ("spine:normal.specialist"), the heads
// ("head:hood") and the guns ("gun:sniper"). The game wears a part when it
// has all its tags, and merges the parts of each bone in kit order, so later
// ones paint over earlier ones. In MagicaVoxel only the default specialist
// shows; the rest are there, hidden.

import { FATIGUES, SLOT } from '../../src/game/vox.ts';
import { DRONE_AT, SOLDIER_BUILDS, SOLDIER_GUNS, type BuildShape, type SoldierBuild } from '../../src/game/voxBodies.ts';
import { Kit, Part } from './sculpt.ts';

const S = SLOT;
// Fixed colours (indices 16+); the game dyes the fatigues on legs painted their own colour.
const SUIT = FATIGUES.suit;
const SUIT_DK = FATIGUES.suitDark;
const BOOT = 18;
const SOLE = 19;
const SKIN = 21;
const SKIN_DK = 22;
const WEB = 23;
const POUCH = 24;
const STEEL = 25;
const DRONE = 26;
const DRONE_DK = 27;
const GLOW = 28;
const PACK = 29;
const HAIR = 30;
const EYE = 31;
const GOLD = 32;
const MASK = 33;
const BLADE = 34;
const NADE = 35;
const NADE_TOP = 36;
const HILT = 37;

/** What MagicaVoxel shows when the kit opens: the default specialist. */
const DEFAULT = new Set(['normal', 'specialist', 'helmet', 'rifle']);

type Add = (bone: string, tags: string[], part: (p: Part) => void, mirror?: string) => Part;

export function soldierKit(): Kit {
  const kit = new Kit().colours({
    // Placeholders for the repainted slots: the blue player's default look.
    [S.primary]: 0x5b7182,
    [S.primaryLight]: 0x7d93a3,
    [S.primaryDark]: 0x3e4f5c,
    [S.secondary]: 0x3fa9ff,
    [S.secondaryLight]: 0x86c8ff,
    [S.secondaryDark]: 0x2a78bd,
    [S.visor]: [0x9fe3ff, 'glow'],
    [S.team]: 0x3fa9ff,
    [S.gun]: 0x2e3438,
    [S.gunDark]: 0x1d2124,
    [S.gunGlow]: 0x3a4046,
    [S.cloth]: 0x1c4c73,
    [S.clothDark]: 0x14354f,
    [SUIT]: 0x4d5549,
    [SUIT_DK]: 0x3a4038,
    [BOOT]: 0x2d2b28,
    [SOLE]: 0x1a1918,
    [SKIN]: 0xc99a7a,
    [SKIN_DK]: 0xa57758,
    [WEB]: 0x7a6d4c,
    [POUCH]: 0x8a7d5a,
    [STEEL]: 0x9aa3a8,
    [DRONE]: 0xc4ced6,
    [DRONE_DK]: 0x5f6a72,
    [GLOW]: [0x7dffb0, 'glow'],
    [PACK]: 0x454b40,
    [HAIR]: 0x4e2f1c,
    [EYE]: 0x1d1a18,
    [GOLD]: 0xc9a13a,
    [MASK]: 0x2a2e30,
    [BLADE]: 0xcfd8dc,
    [NADE]: 0x4f5d3c,
    [NADE_TOP]: 0x2a2e26,
    [HILT]: 0x3a2c22,
  });
  const add: Add = (bone, tags, part, mirror) => {
    const name = tags.length ? `${bone}:${tags.join('.')}` : bone;
    const p = new Part(name).hide(!tags.every((t) => DEFAULT.has(t)));
    part(p);
    kit.add(p);
    if (mirror) kit.add(p.mirrored(tags.length ? `${mirror}:${tags.join('.')}` : mirror));
    return p;
  };

  for (const build of Object.keys(SOLDIER_BUILDS) as SoldierBuild[]) {
    const b = SOLDIER_BUILDS[build];
    body(add, build, b);
    armour(add, build, b);
    gear(add, build, b);
  }
  add('neck', [], (p) => p.box(-1, 22, -1, 1, 22, 0, SUIT_DK));
  heads(add);
  guns(add);
  drone(add);
  return kit;
}

// ------------------------------------------------------------------ body

function body(add: Add, build: SoldierBuild, b: BuildShape): void {
  const t = b.torso;
  const [l0, l1] = [b.legX, b.legX + b.legW - 1];
  const [a0, a1] = [b.armX, b.armX + b.armW - 1];
  const [az0, az1] = b.armW === 3 ? [-1, 1] : [-1, 0];
  const mid = b.legW === 3 ? l0 + 1 : null;

  add('ankleL', [build], (p) => p.box(l0, 0, -1, l1, 0, 2, SOLE).box(l0, 1, -1, l1, 1, 2, BOOT), 'ankleR');
  add(
    'kneeL',
    [build],
    (p) => {
      // Boot shaft with laces on top, fatigues, and a knee pad standing out over the joint.
      p.box(l0, 2, -1, l1, 3, 1, BOOT).box(l0, 3, 1, l1, 3, 1, SOLE);
      p.box(l0, 4, -1, l1, 6, 1, SUIT).box(l0, 4, -1, l1, 4, 1, SUIT_DK);
      p.box(l0, 6, 2, l1, 7, 2, S.secondary).box(l0, 7, 2, l1, 7, 2, S.secondaryLight);
      if (mid !== null) p.set(mid, 6, 2, S.secondaryDark);
    },
    'kneeR',
  );
  add('hipL', [build], (p) => p.box(l0, 7, -1, l1, 12, 1, SUIT).box(l1, 9, -1, l1, 10, 0, SUIT_DK));
  // The right thigh carries a pouch on a strap.
  add('hipR', [build], (p) =>
    p
      .box(-l1, 7, -1, -l0, 12, 1, SUIT)
      .box(-l1, 9, -1, -l1, 10, 0, SUIT_DK)
      .box(-l1 - 1, 8, -1, -l1 - 1, 10, 0, POUCH)
      .box(-l1 - 1, 10, -1, -l1 - 1, 10, 0, WEB),
  );

  add('pivot', [build], (p) =>
    p
      .box(-t, 13, -2, t, 13, 1, SUIT)
      .box(0, 12, -1, 0, 12, 1, SUIT_DK)
      // Belt with a buckle, magazine pouches in front and a pack behind.
      .box(-t, 14, -2, t, 14, 1, WEB)
      .box(-t, 13, 2, -t + 1, 14, 2, POUCH)
      .box(t - 1, 13, 2, t, 14, 2, POUCH)
      .set(0, 14, 2, STEEL)
      .box(-1, 13, -3, 1, 14, -3, POUCH),
  );

  add('spine', [build], (p) => {
    // Belly, vest darker along its lower edge, chest plate lit along its top.
    p.box(-t, 15, -2, t, 16, 1, SUIT);
    p.box(-t, 17, -2, t, 20, 1, S.primary).box(-t, 17, -2, t, 17, 1, S.primaryDark);
    p.box(-t, 16, 2, t, 20, 2, S.primary).box(-t, 20, 2, t, 20, 2, S.primaryLight).box(-t, 16, 2, t, 16, 2, S.primaryDark);
    if (build === 'heavy') {
      // A back plate and plates over the ribs.
      p.box(-t, 16, -3, t, 20, -3, S.primary).box(-t, 20, -3, t, 20, -3, S.primaryLight);
      p.box(-t, 17, -2, -t, 19, 1, S.primaryDark).box(t, 17, -2, t, 19, 1, S.primaryDark);
    }
    // Harness: two straps down the front, over the shoulders and down the back.
    for (const x of [-(t - 1), t - 1]) {
      p.box(x, 15, 2, x, 19, 2, WEB);
      p.box(x, 15, -3, x, 20, -3, WEB);
    }
    // Pouches on the straps and a light on the chest in the plates' colour.
    p.set(-t, 15, 2, POUCH).set(t, 15, 2, POUCH).set(0, 19, 3, S.secondary);
    // Collar.
    p.box(-t, 21, -2, t, 21, 1, S.primaryDark).box(-1, 21, -1, 1, 21, 0, SUIT_DK);
    for (const x of [-(t - 1), t - 1]) p.box(x, 21, -2, x, 21, 1, WEB);
  });

  add(
    'shoulderL',
    [build],
    (p) => {
      p.box(a0, 16, az0, a1, 20, az1, SUIT);
      if (b.pads === 'small') {
        // A pad over the joint, its outer face in the owner's colour.
        p.box(a0, 20, -1, a1 + 1, 21, 1, S.primary).box(a0, 21, -1, a1 + 1, 21, 1, S.primaryLight);
        p.box(a1 + 1, 20, -1, a1 + 1, 20, 1, S.team).box(a1 + 1, 21, -1, a1 + 1, 21, 1, S.secondaryLight);
      } else if (b.pads === 'big') {
        p.box(a0, 19, -2, a1 + 1, 22, 2, S.primary).box(a0, 22, -2, a1 + 1, 22, 2, S.primaryLight);
        p.box(a1 + 1, 20, -2, a1 + 1, 21, 2, S.team).box(a1 + 1, 22, -2, a1 + 1, 22, 2, S.secondaryLight);
        p.box(a0, 19, -2, a1 + 1, 19, 2, S.primaryDark);
      } else {
        // Light armour has no pads: the owner's colour goes on an armband.
        p.box(a0, 18, az0, a1, 18, az1, S.team);
      }
    },
    'shoulderR',
  );
  add(
    'elbowL',
    [build],
    (p) => {
      p.box(a0, 14, az0, a1, 15, az1, SUIT);
      // Bracer, then the glove.
      p.box(a0, 12, az0, a1, 13, az1, S.primary).box(a0, 13, az0, a1, 13, az1, S.primaryLight);
      p.box(a0, 10, az0, a1, 11, az1, BOOT);
    },
    'elbowR',
  );
}

// ------------------------------------------------------------------ armour

/** Tier 2 plates the kevlar; tier 3 (powered) adds seams that glow in the visor's colour and heavier pads. */
function armour(add: Add, build: SoldierBuild, b: BuildShape): void {
  const t = b.torso;
  const [l0, l1] = [b.legX, b.legX + b.legW - 1];
  const [a0, a1] = [b.armX, b.armX + b.armW - 1];
  const [az0, az1] = b.armW === 3 ? [-1, 1] : [-1, 0];
  const mid = b.legW === 3 ? l0 + 1 : null;
  const a2 = [build, 'a2'];
  const a3 = [build, 'a3'];

  // Tier 2: plates on the thighs, shins and upper arms, and a gorget.
  add('hipL', a2, (p) => p.box(l0, 9, 1, l1, 11, 1, S.primary).box(l0, 11, 1, l1, 11, 1, S.primaryLight).box(l1, 9, -1, l1, 11, 0, S.primary), 'hipR');
  add('kneeL', a2, (p) => p.box(l0, 4, 1, l1, 5, 1, S.primary).box(l0, 5, 1, l1, 5, 1, S.primaryLight), 'kneeR');
  add(
    'shoulderL',
    a2,
    (p) => p.box(a1, 17, az0, a1, 19, az1, S.primary).box(a1, 19, az0, a1, 19, az1, S.primaryLight).box(a0, 17, az1, a1, 19, az1, S.primary),
    'shoulderR',
  );
  add('spine', a2, (p) => p.box(-(t - 1), 21, 2, t - 1, 21, 2, S.primaryDark).box(-t + 1, 15, 2, t - 1, 15, 2, S.primaryDark));

  // Tier 3: glowing seams on the chest, knees and forearms, a power pack and heavier pads.
  add('spine', a3, (p) => {
    p.box(-1, 17, 2, -1, 19, 2, S.visor).box(1, 17, 2, 1, 19, 2, S.visor);
    p.box(-2, 15, -4, 2, 18, -4, SUIT_DK).set(-1, 17, -5, S.visor).set(1, 17, -5, S.visor);
  });
  if (mid !== null) add('kneeL', a3, (p) => p.set(mid, 6, 2, S.visor), 'kneeR');
  else add('kneeL', a3, (p) => p.box(l0, 6, 2, l1, 6, 2, S.visor), 'kneeR');
  add('elbowL', a3, (p) => p.box(a1, 12, az1, a1, 13, az1, S.visor), 'elbowR');
  if (b.pads !== 'none') {
    const top = b.pads === 'big' ? 23 : 22;
    add('shoulderL', a3, (p) => p.box(a0, top, -1, a1 + 1, top, 1, S.primaryLight).box(a1 + 1, top - 1, -1, a1 + 1, top - 1, 1, S.visor), 'shoulderR');
  } else add('shoulderL', a3, (p) => p.box(a0, 20, az0, a1, 21, az1, S.primary).box(a0, 21, az0, a1, 21, az1, S.primaryLight), 'shoulderR');
}

// ------------------------------------------------------------------ class gear

function gear(add: Add, build: SoldierBuild, b: BuildShape): void {
  const t = b.torso;
  const l1 = b.legX + b.legW - 1;

  // Specialist: radio pack with its antenna.
  add('spine', [build, 'specialist'], (p) =>
    p
      .box(-2, 15, -5, 2, 20, -4, PACK)
      .box(-1, 16, -6, 1, 18, -6, POUCH)
      .box(-2, 20, -5, 2, 20, -5, WEB)
      .box(2, 21, -4, 2, 26, -4, STEEL)
      .set(2, 27, -4, GLOW),
  );

  // Assault: a sword across the back, hilt over the right shoulder.
  add('spine', [build, 'assault'], (p) => {
    p.line(3, 13, -4, -2, 20, -4, BLADE);
    p.set(-3, 20, -4, HILT).set(-1, 21, -4, HILT).set(-2, 21, -4, HILT);
    p.line(-3, 22, -4, -4, 24, -4, HILT);
    p.box(-1, 16, -5, 0, 17, -5, POUCH);
  });

  // Grenadier: a heavy pack in the armour's colour, and grenades on the belt.
  add('spine', [build, 'grenadier'], (p) =>
    p
      .box(-2, 15, -5, 2, 20, -4, S.primary)
      .box(-2, 20, -5, 2, 20, -4, S.primaryLight)
      .box(-2, 15, -5, 2, 15, -4, S.primaryDark)
      .box(-1, 17, -6, 1, 18, -6, POUCH),
  );
  add('pivot', [build, 'grenadier'], (p) => {
    for (const x of [-t, -t + 1, t - 1, t]) p.set(x, 13, 2, NADE).set(x, 14, 2, NADE_TOP);
  });

  // Sharpshooter: a cape down the back and a pistol on the right thigh.
  add('spine', [build, 'sharpshooter'], (p) => {
    p.box(-t, 13, -4, t, 21, -4, S.cloth);
    for (let x = -t; x <= t; x += 2) p.box(x, 13, -4, x, 19, -4, S.clothDark);
    p.box(-t, 13, -4, t, 13, -4, S.clothDark);
  });
  add('hipR', [build, 'sharpshooter'], (p) => p.box(-l1 - 1, 8, -1, -l1 - 1, 10, 0, MASK).set(-l1 - 1, 11, -1, S.gunDark).set(-l1 - 1, 11, 0, S.gunDark));
}

// ------------------------------------------------------------------ heads

/** A bare skull with a face: eyes, nose, ears. */
function skull(p: Part): Part {
  p.box(-2, 23, -2, 2, 26, 2, SKIN);
  p.set(-1, 25, 2, EYE).set(1, 25, 2, EYE).set(0, 24, 3, SKIN_DK).set(0, 23, 2, SKIN_DK);
  p.box(-2, 23, -2, 2, 23, -2, SKIN_DK);
  return p.set(-3, 24, 0, SKIN).set(3, 24, 0, SKIN);
}

function heads(add: Add): void {
  add('head', ['helmet'], (p) => {
    // Face and chin under the visor.
    p.box(-2, 23, -2, 2, 24, 1, SKIN).box(-1, 23, 1, 1, 23, 1, SKIN_DK);
    // Visor across the eyes, wrapping round to the ears.
    p.box(-2, 24, 2, 2, 24, 2, S.visor).set(-3, 24, 1, S.visor).set(3, 24, 1, S.visor);
    // Shell: a rim, a dome rounded at the corners, ear guards and a neck guard.
    p.box(-3, 25, -3, 3, 25, 2, S.primaryDark).box(-3, 26, -3, 3, 26, 2, S.primary);
    for (const x of [-3, 3]) for (const z of [-3, 2]) p.del(x, 26, z);
    p.box(-2, 27, -2, 2, 27, 1, S.primaryLight);
    p.box(-3, 23, -2, -3, 24, 0, S.primary).box(3, 23, -2, 3, 24, 0, S.primary);
    p.set(-3, 24, -1, S.secondary).set(3, 24, -1, S.secondary);
    p.box(-2, 23, -3, 2, 24, -3, S.primaryDark);
    // The owner's stripe over the top, the first thing seen from the tactical camera.
    p.box(0, 27, -2, 0, 27, 1, S.team).set(0, 26, -3, S.team);
  });

  add('head', ['bare'], (p) => {
    skull(p);
    // Short hair, and a radio earpiece with a light in the visor's colour.
    p.box(-2, 27, -2, 2, 27, 1, HAIR).paint(-2, 24, -2, 2, 26, -2, HAIR).paint(-2, 26, -2, -2, 26, 0, HAIR).paint(2, 26, -2, 2, 26, 0, HAIR);
    p.set(3, 24, 0, MASK).set(3, 25, 0, S.visor);
  });

  add('head', ['beret'], (p) => {
    skull(p);
    p.paint(-2, 24, -2, 2, 26, -2, HAIR);
    // Beret pulled down to the right, a badge at the front left, the owner's colour on the button.
    p.box(-3, 27, -2, 2, 27, 2, S.secondary).box(-2, 28, -1, 1, 28, 1, S.secondary).box(-3, 27, -2, -3, 27, 2, S.secondaryDark);
    p.set(1, 27, 3, GOLD).set(0, 29, 0, S.team);
  });

  add('head', ['cap'], (p) => {
    skull(p);
    p.paint(-2, 24, -2, 2, 26, -2, HAIR);
    // Cap with a brim, goggles pushed up on the forehead.
    p.box(-2, 27, -2, 2, 27, 2, S.secondary).box(-1, 28, -1, 1, 28, 1, S.secondary).box(-2, 27, 3, 2, 27, 3, S.secondaryDark);
    p.paint(-2, 26, -2, 2, 26, 2, MASK).set(-1, 26, 3, S.visor).set(1, 26, 3, S.visor).set(0, 28, 0, S.team);
  });

  add('head', ['mask'], (p) => {
    // A dark hood-mask: lenses in the visor's colour and a filter.
    p.box(-2, 23, -2, 2, 26, 2, MASK).box(-2, 27, -2, 2, 27, 1, MASK);
    p.set(-1, 25, 3, S.visor).set(1, 25, 3, S.visor).set(0, 23, 3, STEEL).set(0, 24, 3, MASK);
    p.set(-3, 24, 0, MASK).set(3, 24, 0, MASK).set(0, 27, 0, S.team);
  });

  add('head', ['hood'], (p) => {
    // A hood in dark cloth, open on the face, a visor band across the eyes.
    p.box(-3, 23, -3, 3, 27, 2, S.cloth);
    for (const x of [-3, 3]) for (const z of [-3, 2]) p.del(x, 27, z);
    p.box(-2, 23, 2, 2, 24, 2, S.clothDark).box(-1, 23, 2, 1, 24, 2, SKIN).set(0, 23, 2, SKIN_DK);
    p.box(-2, 25, 2, 2, 25, 2, S.visor);
    p.box(-2, 26, 2, 2, 26, 2, S.clothDark);
    p.set(0, 28, -2, S.cloth).set(0, 27, -3, S.clothDark).set(0, 28, 0, S.team);
  });
  // The hood falls over the shoulders.
  add('spine', ['hood'], (p) => p.box(-2, 22, -3, 2, 22, -2, S.cloth).box(-3, 21, -3, 3, 21, -3, S.clothDark));
}

// ------------------------------------------------------------------ guns

/** The guns lie level along +z, the stock against the shoulder at their pivot (voxBodies.ts). */
function guns(add: Add): void {
  const x = SOLDIER_GUNS.rifle.pivot[0];
  add('gun', ['rifle'], (p) =>
    p
      .box(x, 18, 3, x, 20, 3, S.gunDark)
      .box(x, 19, 4, x, 20, 4, S.gunDark)
      .box(x, 17, 4, x, 18, 4, S.gunDark)
      .box(x, 19, 5, x, 20, 6, S.gun)
      .box(x, 21, 5, x, 21, 6, S.gunDark)
      .box(x, 17, 6, x, 18, 6, S.gunDark)
      .box(x, 19, 7, x, 20, 9, S.gun)
      .box(x, 21, 7, x, 21, 8, S.gunGlow)
      .set(x, 18, 7, S.gunDark)
      .box(x, 20, 10, x, 20, 11, S.gunDark)
      // The tier shows at the muzzle too: dark steel, cyan coil or violet emitter.
      .set(x, 19, 9, S.gunGlow)
      .set(x, 20, 11, S.gunGlow),
  );

  add('gun', ['shotgun'], (p) =>
    p
      .box(x, 18, 3, x, 20, 3, S.gunDark)
      .box(x, 19, 4, x, 20, 4, S.gunDark)
      .box(x, 17, 4, x, 18, 4, S.gunDark)
      .box(x, 19, 5, x, 20, 7, S.gun)
      .box(x, 21, 5, x, 21, 6, S.gunGlow)
      // Pump under the tube magazine, a short heavy barrel.
      .box(x, 18, 6, x, 18, 7, S.gunDark)
      .set(x, 19, 8, S.gun)
      .box(x, 20, 8, x, 20, 9, S.gunDark)
      .set(x, 20, 9, S.gunGlow),
  );

  add('gun', ['sniper'], (p) =>
    p
      .box(x, 17, 3, x, 20, 3, S.gunDark)
      .box(x, 19, 4, x, 20, 5, S.gunDark)
      .box(x, 17, 5, x, 18, 5, S.gunDark)
      .box(x, 19, 6, x, 20, 10, S.gun)
      // Scope on top, its lens in the tier's glow.
      .box(x, 21, 5, x, 21, 8, S.gunDark)
      .set(x, 21, 8, S.gunGlow)
      .set(x, 18, 7, S.gunDark)
      // Long barrel with a folded bipod and a muzzle brake.
      .box(x, 20, 11, x, 20, 15, S.gunDark)
      .box(x, 19, 12, x, 19, 13, S.gunDark)
      .set(x, 21, 10, S.gunGlow)
      .set(x, 19, 15, S.gunDark)
      .set(x, 20, 15, S.gunGlow),
  );

  add('gun', ['cannon'], (p) =>
    p
      // Two voxels wide: body, rail, grip, front handle, drum and a wide barrel.
      .box(-2, 18, 3, -1, 20, 9, S.gun)
      .box(-2, 21, 5, -1, 21, 6, S.gunDark)
      .box(-2, 16, 4, -2, 17, 4, S.gunDark)
      .box(-2, 17, 6, -1, 17, 6, S.gunDark)
      .box(-2, 16, 7, -1, 17, 8, S.primary)
      .box(-2, 18, 3, -1, 18, 3, S.gunDark)
      .box(-2, 19, 10, -1, 20, 12, S.gunDark)
      .box(-2, 21, 8, -1, 21, 9, S.gunGlow)
      .box(-2, 19, 12, -1, 20, 12, S.gunGlow),
  );
}

function drone(add: Add): void {
  const [cx, cy, cz] = DRONE_AT;
  add('drone', [], (p) => {
    p.box(cx - 1, cy, cz - 1, cx + 1, cy + 1, cz + 1, DRONE);
    p.set(cx, cy + 2, cz, DRONE).set(cx, cy - 1, cz, DRONE_DK).set(cx, cy, cz + 1, DRONE_DK);
    // A glowing ring around it, as wide as a pad.
    for (let dx = -2; dx <= 2; dx++)
      for (let dz = -2; dz <= 2; dz++) if (Math.max(Math.abs(dx), Math.abs(dz)) === 2 && Math.abs(dx) + Math.abs(dz) < 4) p.set(cx + dx, cy, cz + dz, GLOW);
  });
}
