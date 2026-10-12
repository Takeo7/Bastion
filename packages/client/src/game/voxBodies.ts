// Joints of the hand-made voxel kits, shared by the tools that sculpt them
// (tools/vox) and by the game that builds their skeleton (voxFigures.ts), so
// the two never drift apart. All in voxels (1/16 of a tile).
//
// Cells: x and z centred on whole numbers (cell 0 spans −0.5…0.5), y from the
// ground (cell 0 spans 0…1). A cell's centre is (x, y + 0.5, z).
//
// No three.js here: the tools run in Node.

export type SoldierBuild = 'slim' | 'normal' | 'heavy';
export type SoldierGun = 'rifle' | 'shotgun' | 'cannon' | 'sniper';

/** Heights shared by every build: ankles 2, knees 7, hips 13, pelvis 14, spine 15, shoulders 21, neck 22, head 23, elbows 16. */
export const SOLDIER = {
  foot: 2,
  shin: 5,
  thigh: 6,
  hipDrop: 1,
  spineY: 1,
  shoulderY: 6,
  neckY: 7,
  head: 1,
  upper: 5,
  fore: 4,
} as const;

/** What changes with the build: how wide the torso is and where arms and legs hang. */
export interface BuildShape {
  /** The torso spans x −torso…torso. */
  torso: number;
  /** First arm cell (left side) and the arm's width. */
  armX: number;
  armW: number;
  /** First leg cell (left side) and the leg's width. */
  legX: number;
  legW: number;
  pads: 'none' | 'small' | 'big';
}

export const SOLDIER_BUILDS: Record<SoldierBuild, BuildShape> = {
  slim: { torso: 2, armX: 3, armW: 2, legX: 1, legW: 2, pads: 'none' },
  normal: { torso: 3, armX: 4, armW: 2, legX: 1, legW: 3, pads: 'small' },
  heavy: { torso: 3, armX: 4, armW: 3, legX: 1, legW: 3, pads: 'big' },
};

/** A joint's distance from the axis: the centre of the limb's cells. */
export const shoulderX = (b: BuildShape) => b.armX + (b.armW - 1) / 2;
export const hipX = (b: BuildShape) => b.legX + (b.legW - 1) / 2;

/**
 * The guns, level in front of the chest in the bind pose. `pivot` (figure
 * voxels) is where the stock meets the shoulder, the gun turns about it; the
 * rest is relative to it: where the hands hold it and where shots leave.
 */
export interface GunSpec {
  pivot: [number, number, number];
  muzzle: [number, number, number];
  gripR: [number, number, number] | null;
  gripL: [number, number, number] | null;
}

export const SOLDIER_GUNS: Record<SoldierGun, GunSpec> = {
  rifle: { pivot: [-1, 20, 2], muzzle: [0, 0.5, 9.5], gripR: [0, -2, 2], gripL: [0, -1.5, 5] },
  shotgun: { pivot: [-1, 20, 2], muzzle: [0, 0.5, 7.5], gripR: [0, -2, 2], gripL: [0, -1.5, 4.5] },
  cannon: { pivot: [-1.5, 19.5, 2], muzzle: [0, 0.5, 10.5], gripR: [-0.5, -2.5, 2], gripL: [0, -2, 4] },
  sniper: { pivot: [-1, 20, 2], muzzle: [0, 0.5, 13.5], gripR: [0, -2, 3], gripL: [0, -1.5, 5] },
};

/** A rookie carries the rifle and no class gear yet. */
export const CLASS_GUN: Record<'assault' | 'grenadier' | 'sharpshooter' | 'specialist' | 'rookie', SoldierGun> = {
  rookie: 'rifle',
  assault: 'shotgun',
  grenadier: 'cannon',
  sharpshooter: 'sniper',
  specialist: 'rifle',
};

/** ADVENT wear the soldiers' skeleton: troopers and officers the normal build, lancers the slim one. */
export const ADVENT_BUILD = { trooper: 'normal', officer: 'normal', lancer: 'slim' } as const;

export const ADVENT_GUNS: Record<'rifle' | 'baton', GunSpec> = {
  rifle: SOLDIER_GUNS.rifle,
  // The stun lance, held in the right hand only, its glowing tip forward.
  baton: { pivot: [-2, 20.5, 3], muzzle: [0, 0, 10.5], gripR: [0, 0, 0], gripL: null },
};

/** The specialist's drone hovers here (figure voxels), and turns about it. */
export const DRONE_AT: [number, number, number] = [8, 30, -2];

/** Sectoid: ankles 1, knees 5, hips (±1.5, 10), pelvis 11, spine 12, shoulders (±3, 16), elbows 12, neck 17, head 18. */
export const SECTOID = {
  foot: 1,
  shin: 4,
  thigh: 5,
  hipDrop: 1,
  hipX: 1.5,
  spineY: 1,
  shoulderX: 3,
  shoulderY: 4,
  neckY: 5,
  head: 1,
  upper: 4,
  fore: 4,
  gun: { pivot: [-2, 14, 2], muzzle: [0, 0.5, 4.5], gripR: [0, -1, 0], gripL: null } as GunSpec,
} as const;

/** Xenoid: a thin grey with a long skull, long arms and a hunch. Ankles 1, knees 6, hips (±1.5, 11), pelvis 12, spine 13, shoulders (±3, 18), neck 19, head 21. */
export const XENOID = {
  foot: 1,
  shin: 5,
  thigh: 5,
  hipDrop: 1,
  hipX: 1.5,
  spineY: 1,
  shoulderX: 3,
  shoulderY: 5,
  neckY: 6,
  head: 2,
  upper: 5,
  fore: 5,
  gun: { pivot: [-2, 17.5, 3], muzzle: [0, 0, 4], gripR: [0, -1, 0], gripL: null } as GunSpec,
} as const;

/**
 * MEC: drawn with its reverse-jointed legs straight (the bind pose); the game
 * bends them. Ankles 2, knees 9, hips (±4, 16), pelvis 17, spine 19,
 * shoulders (±7.5, 28), neck 31, head 32. The right arm is the gun.
 */
export const MEC = {
  foot: 2,
  shin: 7,
  thigh: 7,
  hipDrop: 1,
  hipX: 4,
  spineY: 2,
  shoulderX: 7.5,
  shoulderY: 9,
  neckY: 12,
  head: 1,
  upper: 6,
  fore: 5,
  gun: { pivot: [-8, 27.5, 0], muzzle: [0, -1, 13.5], gripR: [0, 0, 0], gripL: null } as GunSpec,
} as const;

/** Zombie: the soldiers' skeleton; no gun, the hands reach forward to claw. */
export const ZOMBIE_REACH: GunSpec = { pivot: [0, 20.5, 3], muzzle: [0, 0, 8], gripR: [-2, -0.5, 6], gripL: [2, -0.5, 6] };

/** VIP: the soldiers' skeleton, unarmed. */
export const VIP_HANDS: GunSpec = { pivot: [0, 20, 2], muzzle: [0, 0, 2], gripR: null, gripL: null };

/** Relay: no legs; the mast stands on the pelvis at the ground, the ring spins about the axis at `ring` (figure voxels). */
export const RELAY = { spineY: 4.5, ring: 25, core: 33 } as const;
