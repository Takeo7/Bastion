import type { Arms, Tier, WeaponModId } from '@bastion/engine';
import type { GaitMotion } from './gaits';
import type { UnitRig } from './models';
import type { Stance, WeaponId } from './syntyManifest';

// What a unit's figure answers to (syntyFigures.ts makes them, the unit views
// pose them every frame): the pose values and the equipment it shows. The
// articulated figures built from primitives that used to live here (the XCOM
// soldiers, ADVENT, the sectoid, the MEC…) are gone: every unit is a Synty
// character now.

/** Pose values, the same ones the game's unit views animate. */
export interface FigurePose {
  crouch: number;
  aim: number;
  run: number;
  recoil: number;
  hit: number;
  flash: number;
  fall: number;
  lean: number;
  hop: number;
  yaw: number;
  phase: number;
  /** Reload, 0 to 1 (rigs that move their own weapon for it, ownsReload). */
  spin?: number;
  /** Rummaging through furniture, 0 to 1 (the unit view also crouches the figure for it). */
  search?: number;
  /** Up a ladder, 0 to 1: hands and feet on the rungs (rigs that can; the others just walk up). */
  climb?: number;
  /** A melee stroke, −1 wound up to +1 followed through: the living cut across, the dead grab and bite. */
  swing?: number;
  /** How far into its hit reaction (react), 0 to 1; 0 none. */
  react?: number;
}

/** The blow that kills a unit, for how it falls (syntyManifest.ts DEATHS). */
export interface Blow {
  /** The way it comes from in the unit's frame (0 ahead, π/2 from its left), or null if unknown. */
  from: number | null;
  /** A critical: a shot to the head. */
  head: boolean;
  /** At arm's length (a blade, a bite). */
  melee: boolean;
  /** Picks among the falls that fit it, the same for the same unit. */
  seed: string;
  /** Whether the body has room to fall `reach` tiles that way (an angle in its frame): no wall or high cover on the way. */
  room?: (way: number, reach: number) => boolean;
}

export interface FigureRig extends UnitRig {
  applyPose(p: FigurePose, time: number): void;
  /** What its moves play, and how fast (gaits.ts), on figures that play clips. */
  setGait?(motion: GaitMotion, speed: number): void;
  /**
   * How it dies, on figures that play clips: from then on its pose's `fall`
   * (0 to 1) is how far into the death it is. Returns the seconds the fall
   * takes; null if it has no clips (yet), and the fall is the stiff topple.
   */
  die?(blow: Blow): number | null;
  /** On its feet again after die (Último verso, a body risen as a zombie), once `fall` is back to 0. */
  rise?(): void;
  /**
   * How it takes a hit of `strength` (1 a hit, more a critical, less a graze),
   * on figures that play clips: from then on its pose's `react` is how far into
   * it. Returns the seconds it takes; null without clips (the procedural hit only).
   */
  react?(blow: Blow, strength: number): number | null;
  /** How it stands waiting (syntyManifest.ts STANCES), null the plain idle; on figures that play clips. */
  setStance?(stance: Stance | null): void;
  /** Hidden by the buildings' cutaway (the unit views set it): what it draws away from its own meshes (the crowd) hides too. */
  cutAway?: boolean;
  /** Which of its weapons is in the hand (Armament): the main one, the pistol from its holster, the blade from the belt. */
  draw?(which: Carried): void;
  /** Other weapons on it (the engine's weaponEquipped), the main one in the hand. */
  arm?(a: Armament): void;
}

/** Where a unit carries each of its weapons: the main one, the pistol in its holster, the blade at the belt. */
export type Carried = 'weapon' | 'sidearm' | 'belt';

/**
 * The weapons a unit carries, as models (syntyFigures.ts armamentOf): the
 * main one in the hand; beside it, the pistol in its holster on the right
 * thigh, drawn for a pistol shot; and at the left hip the knife or the
 * machete, drawn for a stroke. A campaign soldier's main gun also shows what
 * is mounted on it and how worn it is (docs/FASE9.md §5: Arms.mods, condition).
 */
export interface Armament extends Partial<Record<Carried, WeaponId | null>> {
  weapon: WeaponId | null;
  mods?: readonly WeaponModId[];
  /** 0 to 100; absent, 100. */
  condition?: number;
}

/** Equipment tiers a soldier carries (1 when absent): the weapon's and the armour's; and a campaign soldier's own weapons (docs/FASE8.md §A). */
export interface Gear {
  weapon?: Tier;
  armor?: Tier;
  arms?: Arms | null;
}
