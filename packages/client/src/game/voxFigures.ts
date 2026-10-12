import * as THREE from 'three';
import type { Appearance, TemplateId } from '@bastion/engine';
import adventVox from '../assets/voxel/advent.vox?inline';
import mecVox from '../assets/voxel/mec.vox?inline';
import relayVox from '../assets/voxel/relay.vox?inline';
import sectoidVox from '../assets/voxel/sectoid.vox?inline';
import soldierVox from '../assets/voxel/soldier.vox?inline';
import vipVox from '../assets/voxel/vip.vox?inline';
import xenoidVox from '../assets/voxel/xenoid.vox?inline';
import zombieVox from '../assets/voxel/zombie.vox?inline';
import { Figure, flashOn, soldierPalette, type Body, type FigureRig, type Gear } from './figures';
import { readVox, SLOT } from './vox';
import {
  ADVENT_BUILD,
  ADVENT_GUNS,
  CLASS_GUN,
  DRONE_AT,
  MEC,
  RELAY,
  SECTOID,
  SOLDIER,
  SOLDIER_BUILDS,
  SOLDIER_GUNS,
  VIP_HANDS,
  XENOID,
  ZOMBIE_REACH,
  hipX,
  shoulderX,
  type GunSpec,
  type SoldierBuild,
} from './voxBodies';
import { GAME_VOXEL, voxelField, type Colour } from './voxel';

// Hand-made voxel figures: the models come from MagicaVoxel kits
// (assets/voxel/*.vox, drawn by tools/vox), one model per bone, and ride the
// same skeleton as figures.ts, so poses and animations do not change. The
// joints are in voxBodies.ts, shared with the tools that draw the kits.
//
// A kit part named "bone" is always worn; "bone:tag1.tag2" only when the
// figure asks for all its tags (its build, armour tier, class, head, gun).
// The parts a figure wears on one bone are merged in kit order, later ones
// painting over earlier ones, and meshed once. A figure the kits cannot
// dress returns null, and figures.ts builds it as before.

const U = GAME_VOXEL;
const v = (n: number) => n * U;

// ------------------------------------------------------------------ kits

interface KitPart {
  bone: string;
  tags: string[];
  /** Colour index by packed cell. */
  cells: Map<number, number>;
}

interface Kit {
  /** In kit order: later parts paint over earlier ones. */
  parts: { name: string; part: KitPart }[];
  palette: number[];
  emit: Set<number>;
}

const pack = (x: number, y: number, z: number) => ((x + 512) << 20) | ((y + 512) << 10) | (z + 512);
const unpack = (k: number): [number, number, number] => [(k >> 20) - 512, ((k >> 10) & 1023) - 512, (k & 1023) - 512];

function decode(dataUrl: string): Kit {
  const bin = atob(dataUrl.slice(dataUrl.indexOf(',') + 1));
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  const vox = readVox(bytes);
  const parts = vox.parts.map((p) => {
    const [bone, tags] = p.name.split(':') as [string, string | undefined];
    const cells = new Map<number, number>();
    for (let i = 0; i < p.cells.length; i += 4) cells.set(pack(p.cells[i]!, p.cells[i + 1]!, p.cells[i + 2]!), p.cells[i + 3]!);
    return { name: p.name, part: { bone, tags: tags ? tags.split('.') : [], cells } };
  });
  return { parts, palette: vox.palette, emit: vox.emit };
}

const SOURCES: Record<string, string> = {
  soldier: soldierVox,
  sectoid: sectoidVox,
  advent: adventVox,
  xenoid: xenoidVox,
  mec: mecVox,
  zombie: zombieVox,
  relay: relayVox,
  vip: vipVox,
};
const kits = new Map<string, Kit>();

function kit(name: string): Kit {
  let k = kits.get(name);
  if (!k) kits.set(name, (k = decode(SOURCES[name]!)));
  return k;
}

/** Whether the kit has a part with exactly these tags (to tell if a variant is drawn). */
function has(kitName: string, name: string): boolean {
  return kit(kitName).parts.some((p) => p.name === name);
}

// --------------------------------------------------------------- colours

/** A glowing colour as the figures' materials make it: the hue deeper, then pushed past white for the bloom. */
function glowColour(c: THREE.Color): Colour {
  const hsl = c.getHSL({ h: 0, s: 0, l: 0 }, THREE.SRGBColorSpace);
  const deep = new THREE.Color().setHSL(hsl.h, hsl.s * 0.64, hsl.l * 0.57, THREE.SRGBColorSpace);
  return { r: deep.r * 1.8, g: deep.g * 1.8, b: deep.b * 1.8, glow: true };
}

const plain = (c: THREE.Color): Colour => ({ r: c.r, g: c.g, b: c.b, glow: false });

/** Repaints: colour index → the colour to use instead, and whether it glows. */
type Repaint = Map<number, { colour: THREE.Color; glow: boolean }>;

/** Gun colours by weapon tier: conventional steel, magnetic blue steel with cyan coils, plasma white with a violet chamber. */
const GUN_TIERS = {
  1: { gun: 0x2e3438, dark: 0x1d2124, glow: [0x3a4046, false] },
  2: { gun: 0x4a5866, dark: 0x22282e, glow: [0x9fe8ff, true] },
  3: { gun: 0xc9ccd1, dark: 0x4a4f56, glow: [0xe2c8ff, true] },
} as const;

function soldierRepaint(accent: string, look: Appearance | undefined, gear: Gear | undefined): Repaint {
  const primary = new THREE.Color(look ? look.primary : new THREE.Color(accent).lerp(new THREE.Color(0x56605a), 0.55));
  const secondary = new THREE.Color(look?.secondary ?? accent);
  const white = new THREE.Color(1, 1, 1);
  const black = new THREE.Color(0, 0, 0);
  const tier = GUN_TIERS[gear?.weapon ?? 1];
  const r: Repaint = new Map();
  const set = (index: number, colour: THREE.ColorRepresentation, glow = false) => r.set(index, { colour: new THREE.Color(colour), glow });
  set(SLOT.primary, primary);
  set(SLOT.primaryLight, primary.clone().lerp(white, 0.22));
  set(SLOT.primaryDark, primary.clone().lerp(black, 0.38));
  set(SLOT.secondary, secondary);
  set(SLOT.secondaryLight, secondary.clone().lerp(white, 0.3));
  set(SLOT.secondaryDark, secondary.clone().lerp(black, 0.4));
  set(SLOT.visor, look?.visor ?? 0x9fe3ff, true);
  // The owner's mark glows, so whose soldier it is shows even under customised armour (playtest).
  set(SLOT.team, accent, true);
  set(SLOT.gun, tier.gun);
  set(SLOT.gunDark, tier.dark);
  set(SLOT.gunGlow, tier.glow[0], tier.glow[1]);
  // Hood and cape: the plates' colour dyed dark, as figures.ts does.
  set(SLOT.cloth, secondary.clone().multiplyScalar(0.45));
  set(SLOT.clothDark, secondary.clone().multiplyScalar(0.3));
  return r;
}

// ----------------------------------------------------------------- meshes

/** Built bone models by kit, worn parts and colours; each figure gets clones (shared geometry). */
const built = new Map<string, THREE.Group>();

function boneModel(kitName: string, parts: KitPart[], names: string, repaint: Repaint, repaintKey: string): THREE.Group {
  const key = `${kitName}|${names}|${repaintKey}`;
  let model = built.get(key);
  if (!model) {
    const k = kit(kitName);
    const cells = new Map<number, number>();
    for (const p of parts) for (const [c, index] of p.cells) cells.set(c, index);
    const min: [number, number, number] = [Infinity, Infinity, Infinity];
    const max: [number, number, number] = [-Infinity, -Infinity, -Infinity];
    for (const c of cells.keys()) {
      const xyz = unpack(c);
      for (let i = 0; i < 3; i++) {
        min[i] = Math.min(min[i]!, xyz[i]!);
        max[i] = Math.max(max[i]!, xyz[i]!);
      }
    }
    const colours = new Map<number, Colour>();
    const colourOf = (index: number): Colour => {
      let c = colours.get(index);
      if (!c) {
        const over = repaint.get(index);
        const base = over?.colour ?? new THREE.Color(k.palette[index] ?? 0xff00ff);
        c = (over ? over.glow : k.emit.has(index)) ? glowColour(base) : plain(base);
        colours.set(index, c);
      }
      return c;
    };
    model = voxelField(
      min,
      max,
      (x, y, z) => {
        const index = cells.get(pack(x, y, z));
        return index ? colourOf(index) : null;
      },
      { size: U, jitter: 0.06 },
    );
    built.set(key, model);
  }
  return model.clone();
}

/** Bone objects of a figure by the names kit parts use, plus any extra ones (the relay's ring). */
function bones(f: Figure, extra: Record<string, THREE.Object3D>): Record<string, THREE.Object3D> {
  return { pivot: f.pivot, spine: f.spine, neck: f.neck, head: f.head, gun: f.gunPivot, ...f.joints, ...(f.drone ? { drone: f.drone } : {}), ...extra };
}

/**
 * Dresses figure `f` (in its bind pose: no joint rotated) with the kit's
 * parts whose tags are all in `tags`. Each bone gets one model, placed so
 * that at rest it sits where the kit drew it.
 */
function dress(f: Figure, kitName: string, tags: Set<string>, repaint: Repaint, repaintKey: string, extra: Record<string, THREE.Object3D> = {}): void {
  const map = bones(f, extra);
  const byBone = new Map<string, { names: string[]; parts: KitPart[] }>();
  for (const { name, part } of kit(kitName).parts) {
    if (!map[part.bone] || !part.tags.every((t) => tags.has(t))) continue;
    let entry = byBone.get(part.bone);
    if (!entry) byBone.set(part.bone, (entry = { names: [], parts: [] }));
    entry.names.push(name);
    entry.parts.push(part);
  }
  // Kits draw the legs straight; reverse-jointed figures (MEC) are built already bent, so straighten them to measure.
  const b = f.body;
  const pivotY = f.pivot.position.y;
  f.pivot.position.y = b.hipDrop + b.thigh + b.shin + b.foot;
  f.root.updateMatrixWorld(true);
  const rest = new THREE.Vector3();
  for (const [bone, { names, parts }] of byBone) {
    const target = map[bone]!;
    target.getWorldPosition(rest);
    const model = boneModel(kitName, parts, names.join(','), repaint, repaintKey);
    // x and z cells are centred on whole voxels; voxelField starts them there.
    model.position.set(-rest.x - U / 2, -rest.y, -rest.z - U / 2);
    target.add(model);
  }
  f.pivot.position.y = pivotY;
}

/** Hangs a gun on the figure: pivot from the spine, hands on its grips, shots from its muzzle. */
function arm(f: Figure, gun: GunSpec, spineY: number): void {
  const [px, py, pz] = gun.pivot;
  const g = (p: readonly number[]): [number, number, number] => [v(p[0]!), v(p[1]!), v(p[2]!)];
  f.weapon([v(px), v(py - spineY), v(pz)], () => undefined, v(gun.muzzle[2]), gun.gripR ? g(gun.gripR) : null, gun.gripL ? g(gun.gripL) : null);
  f.muzzle.position.set(v(gun.muzzle[0]), v(gun.muzzle[1]), v(gun.muzzle[2]));
}

// ---------------------------------------------------------------- soldiers

type SoldierClass = keyof typeof CLASS_GUN;
const SPINE_Y = SOLDIER.foot + SOLDIER.shin + SOLDIER.thigh + SOLDIER.hipDrop + SOLDIER.spineY;

function soldierBody(build: SoldierBuild): Body {
  const b = SOLDIER_BUILDS[build];
  return {
    hipDrop: v(SOLDIER.hipDrop),
    hipX: v(hipX(b)),
    thigh: v(SOLDIER.thigh),
    shin: v(SOLDIER.shin),
    foot: v(SOLDIER.foot),
    spineY: v(SOLDIER.spineY),
    shoulderX: v(shoulderX(b)),
    shoulderY: v(SOLDIER.shoulderY),
    neckY: v(SOLDIER.neckY),
    upper: v(SOLDIER.upper),
    fore: v(SOLDIER.fore),
    thick: 1,
    hunch: 0,
    digitigrade: false,
    boxy: false,
    scale: 1,
  };
}

function soldier(cls: SoldierClass, accent: string, look: Appearance | undefined, gear: Gear | undefined): FigureRig | null {
  // Default looks as the engine gives them: sharpshooters hooded, grenadiers heavy.
  const head = look?.head ?? (cls === 'sharpshooter' ? 'hood' : 'helmet');
  const build = look?.build ?? (cls === 'grenadier' ? 'heavy' : 'normal');
  const gun = CLASS_GUN[cls];
  if (!has('soldier', `head:${head}`) || !has('soldier', `gun:${gun}`) || !has('soldier', `spine:${build}`)) return null;
  const armour = gear?.armor ?? 1;
  const tags = new Set<string>([build, cls, head, gun]);
  if (armour >= 2) tags.add('a2');
  if (armour >= 3) tags.add('a3');

  const f = new Figure(soldierBody(build), soldierPalette(accent, look));
  f.head.position.y = v(SOLDIER.head);
  flashOn(f, 0.24, 0.3, v(4));
  f.chest = f.standingHeight(0, 0) + v(SOLDIER.spineY + 4);
  arm(f, SOLDIER_GUNS[gun], SPINE_Y);
  if (cls === 'specialist') {
    const drone = new THREE.Group();
    drone.position.set(v(DRONE_AT[0]), v(DRONE_AT[1]), v(DRONE_AT[2]));
    f.yaw.add(drone);
    f.drone = drone;
  }
  const key = [accent, look?.primary, look?.secondary, look?.visor, gear?.weapon ?? 1].join('|');
  dress(f, 'soldier', tags, soldierRepaint(accent, look, gear), key);
  return f.rig();
}

// ---------------------------------------------------------------- aliens

const SECTOID_BODY: Body = {
  hipDrop: v(SECTOID.hipDrop),
  hipX: v(SECTOID.hipX),
  thigh: v(SECTOID.thigh),
  shin: v(SECTOID.shin),
  foot: v(SECTOID.foot),
  spineY: v(SECTOID.spineY),
  shoulderX: v(SECTOID.shoulderX),
  shoulderY: v(SECTOID.shoulderY),
  neckY: v(SECTOID.neckY),
  upper: v(SECTOID.upper),
  fore: v(SECTOID.fore),
  thick: 1,
  hunch: 0.1,
  digitigrade: false,
  boxy: false,
  scale: 1,
};

/** Small grey psionic: thin limbs, long arms, a forward hunch; when it casts, the free hand goes to the temple. */
function sectoid(): FigureRig {
  const f = new Figure(SECTOID_BODY, soldierPalette('#56605a'));
  f.head.position.y = v(SECTOID.head);
  flashOn(f, 0.17, 0.2, v(2.5));
  f.chest = f.standingHeight(0, 0) + v(SECTOID.spineY + 2.5);
  arm(f, SECTOID.gun, SECTOID.foot + SECTOID.shin + SECTOID.thigh + SECTOID.hipDrop + SECTOID.spineY);
  f.leftAim = new THREE.Vector3(v(3.6), v(10), v(1));
  dress(f, 'sectoid', new Set(['pistol']), new Map(), '');
  return f.rig();
}

/** Plate colour and visor of each ADVENT type: red troopers, officers with an orange visor, lancers in blue. */
const ADVENT_LOOK = {
  trooper: { plate: 0x7a1f2b, visor: 0xff6a6a },
  officer: { plate: 0x7a1f2b, visor: 0xffb04a },
  lancer: { plate: 0x5a1622, visor: 0x6ad8ff },
} as const;

function advent(type: keyof typeof ADVENT_LOOK): FigureRig {
  const build = ADVENT_BUILD[type];
  const gun = type === 'lancer' ? 'baton' : 'rifle';
  const f = new Figure(soldierBody(build), soldierPalette('#56605a'));
  f.head.position.y = v(SOLDIER.head);
  flashOn(f, 0.24, 0.3, v(4));
  f.chest = f.standingHeight(0, 0) + v(SOLDIER.spineY + 4);
  arm(f, ADVENT_GUNS[gun], SPINE_Y);
  const look = ADVENT_LOOK[type];
  const plate = new THREE.Color(look.plate);
  const repaint: Repaint = new Map([
    [SLOT.secondary, { colour: plate, glow: false }],
    [SLOT.secondaryLight, { colour: plate.clone().lerp(new THREE.Color(1, 1, 1), 0.2), glow: false }],
    [SLOT.secondaryDark, { colour: plate.clone().lerp(new THREE.Color(0, 0, 0), 0.35), glow: false }],
    [SLOT.visor, { colour: new THREE.Color(look.visor), glow: true }],
  ]);
  dress(f, 'advent', new Set([build, type, gun]), repaint, type);
  return f.rig();
}

/** A body from one of the joint tables of voxBodies.ts. */
function jointsBody(j: { foot: number; shin: number; thigh: number; hipDrop: number; hipX: number; spineY: number; shoulderX: number; shoulderY: number; neckY: number; upper: number; fore: number }, extra: Partial<Body> = {}): Body {
  return {
    hipDrop: v(j.hipDrop),
    hipX: v(j.hipX),
    thigh: v(j.thigh),
    shin: v(j.shin),
    foot: v(j.foot),
    spineY: v(j.spineY),
    shoulderX: v(j.shoulderX),
    shoulderY: v(j.shoulderY),
    neckY: v(j.neckY),
    upper: v(j.upper),
    fore: v(j.fore),
    thick: 1,
    hunch: 0,
    digitigrade: false,
    boxy: false,
    scale: 1,
    ...extra,
  };
}

const spineOf = (j: { foot: number; shin: number; thigh: number; hipDrop: number; spineY: number }) => j.foot + j.shin + j.thigh + j.hipDrop + j.spineY;

/** A thin grey with a long skull and a hunch, an emitter in the right hand. */
function xenoid(): FigureRig {
  const f = new Figure(jointsBody(XENOID, { hunch: 0.18 }), soldierPalette('#56605a'));
  f.head.position.y = v(XENOID.head);
  flashOn(f, 0.2, 0.3, v(3.5));
  f.chest = f.standingHeight(0, 0) + v(XENOID.spineY + 3.5);
  arm(f, XENOID.gun, spineOf(XENOID));
  dress(f, 'xenoid', new Set(['emitter']), new Map(), '');
  return f.rig();
}

/** The MEC: reverse-jointed legs, a missile pod, and a gun for a right arm. */
function mec(): FigureRig {
  const f = new Figure(jointsBody(MEC, { digitigrade: true, boxy: true }), soldierPalette('#56605a'));
  f.head.position.y = v(MEC.head);
  flashOn(f, 0.45, 0.5, v(7));
  f.chest = f.standingHeight(0, 0) + v(MEC.spineY + 7);
  arm(f, MEC.gun, spineOf(MEC));
  f.freeR = false;
  f.joints.shoulderR.visible = false;
  dress(f, 'mec', new Set(['cannon']), new Map(), '');
  return f.rig();
}

/** A raised corpse: hunched, dragging its feet, claws forward. */
function zombie(): FigureRig {
  const f = new Figure({ ...soldierBody('normal'), hunch: 0.38 }, soldierPalette('#56605a'));
  f.shamble = 1;
  f.head.position.y = v(SOLDIER.head);
  flashOn(f, 0.22, 0.5, v(4));
  f.chest = f.standingHeight(0, 0) + v(SOLDIER.spineY + 4);
  arm(f, ZOMBIE_REACH, SPINE_Y);
  dress(f, 'zombie', new Set(), new Map(), '');
  return f.rig();
}

/** The objective pylon: no legs, no arms, a ring spinning around the mast. */
function relay(): FigureRig {
  const f = new Figure(
    { ...jointsBody({ foot: 0, shin: 0, thigh: 0, hipDrop: 0, hipX: 0, spineY: RELAY.spineY, shoulderX: 0, shoulderY: 0, neckY: 30, upper: 0, fore: 0 }) },
    soldierPalette('#56605a'),
  );
  f.still = true;
  f.freeR = false;
  flashOn(f, 0.24, 1.0, 0.85);
  f.chest = 1.3;
  f.weapon([0, v(RELAY.core + 0.5 - RELAY.spineY), 0], () => undefined, 0, null, null);
  const ring = new THREE.Group();
  ring.position.y = v(RELAY.ring + 0.5 - RELAY.spineY);
  f.spine.add(ring);
  f.spinners.push({ obj: ring, speed: 1.4 });
  dress(f, 'relay', new Set(), new Map(), '', { ring });
  return f.rig();
}

/** The resistance contact: unarmed, arms swinging as it runs, a scarf in the squad's colour. */
function vip(accent: string): FigureRig {
  const f = new Figure(soldierBody('normal'), soldierPalette(accent));
  f.swingArms = true;
  f.head.position.y = v(SOLDIER.head);
  flashOn(f, 0.21, 0.5, v(4));
  f.chest = f.standingHeight(0, 0) + v(SOLDIER.spineY + 4);
  arm(f, VIP_HANDS, SPINE_Y);
  dress(f, 'vip', new Set(), new Map([[SLOT.team, { colour: new THREE.Color(accent), glow: false }]]), accent);
  return f.rig();
}

/** A hand-made figure for this unit, or null when the kits cannot dress it yet. */
export function voxFigure(template: TemplateId, accent: string, look?: Appearance, gear?: Gear): FigureRig | null {
  switch (template) {
    case 'rookie':
    case 'assault':
    case 'grenadier':
    case 'sharpshooter':
    case 'specialist':
      return soldier(template, accent, look, gear);
    case 'sectoid':
      return sectoid();
    case 'trooper':
    case 'officer':
    case 'lancer':
      return advent(template);
    case 'xenoid':
      return xenoid();
    case 'mec':
      return mec();
    case 'zombie':
      return zombie();
    case 'relay':
      return relay();
    case 'vip':
      return vip(accent);
    default:
      return null;
  }
}
