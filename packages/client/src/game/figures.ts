import type { Tier } from '@bastion/engine';
import type { UnitRig } from './models';

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
}

export interface FigureRig extends UnitRig {
  applyPose(p: FigurePose, time: number): void;
}

/** Equipment tiers a soldier carries (1 when absent): the weapon's and the armour's. */
export interface Gear {
  weapon?: Tier;
  armor?: Tier;
}
