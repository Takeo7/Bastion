import type { Vector3 } from 'three';
import type { Arms, Tier, WeaponModId } from '@bastion/engine';
import type { GaitMotion } from './gaits';
import type { UnitRig } from './models';
import type { CampLoop, CampMove, Gesture, PropId, Stance, WeaponId } from './syntyManifest';

/** How far a feeding is (FigureRig.feed): the bite that kills, getting down to the body, at it, getting up. */
export type FeedPhase = 'bite' | 'down' | 'eat' | 'up';

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
  /** A stone tossed underhand with the free hand (docs/FASE10.md §4), 0 to 1 through it; 0 none. */
  toss?: number;
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
  /** Coming into sight: drawn in over `seconds` instead of at once (syntyFigures.ts). */
  fadeIn?(seconds: number): void;
  /** Out of the camera's way (the shot over a shoulder): dithered out, or back in. */
  veil?(on: boolean): void;
  /** Marked as the target being picked (unitViews highlight), or not: on figures that dye (syntyFigures.ts). */
  mark?(on: boolean): void;
  /** A shade, or not (unitViews shade): on figures that dye. */
  shade?(on: boolean, now?: boolean): void;
  /** How far ahead of its feet the muzzle of a long gun in its hands comes when shouldered (tiles); null for anything else. */
  aimReach?(): number | null;
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
  /** Its weapons all on it, the hands free (at ease on the clearing: clearingLife.ts); false, the one drawn in the hand again. */
  sling?(on: boolean): void;
  /** Makes a gesture once over how it stands (syntyManifest.ts GESTURES: those talking on the clearing); whether it began. */
  gesture?(g: Gesture): boolean;
  /**
   * At the camp (the clearing: clearingLife.ts, syntyManifest.ts CAMP_LOOPS): stays in `loop` instead of how it stands
   * (null: as it stands) and, standing, does the camp's things now and then (CAMP_VARIATIONS); `on` false, none of it;
   * `now`, into it without easing (after a move into it).
   */
  camp?(loop: CampLoop | null, on?: boolean, now?: boolean): void;
  /** Sits down or gets up, leans or comes off, gets into La Lata (CAMP_MOVES) once over what it does, at `speed`, holding its end; how far through, 0 to 1 (1 without its clips); null lets go. */
  campMove?(move: CampMove | null, speed?: number): number;
  /**
   * One of the dead killing and eating one of ours (22/10 #21; syntyManifest.ts FEEDERS, FEED_CLIPS): 'bite' into its
   * neck, 'down' onto its knees, 'eat' there, 'up' again, null none. The seconds 'bite', 'down' or 'up' takes (0
   * without its clips).
   */
  feed?(phase: FeedPhase | null): number;
  /** Where its chest is as posed now (a body on the ground: where it lies), into `out`. */
  chestWorld?(out: Vector3): Vector3;
  /** Where its hips and its head are as posed now (a body on the ground: which way it lies), into `hips` and `head`. */
  bodyLine?(hips: Vector3, head: Vector3): void;
  /** Holds a thing in the right hand at the camp (syntyManifest.ts CAMP_ITEMS: a metal cup, a ration bar), its weapons slung, `upright` kept level (a cup); null, nothing. */
  holdItem?(id: PropId | null, upright?: boolean): void;
  /** Waits with a rifle in the `k`-th of its ways (syntyManifest.ts RIFLE_RESTS, round): side by side, each another (the lineup). */
  restAs?(k: number): void;
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
