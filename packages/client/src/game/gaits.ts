import type { TemplateId } from '@bastion/engine';
import { CHARACTER_SCALE, SYNTY_SCALE } from './syntyManifest';

// How each kind of unit moves (docs/ARTE.md, «Velocidades»; Arturo, 2026-10-14:
// «todos corren demasiado rápido y no se ve bien la animación de andar ni
// correr»). A move of one action (the blue) takes the gait `one`, a move of
// two (the yellow) `two`: the clip it plays and the seconds a tile takes,
// GAITS below, the one table to tune them in (Gameplay decides the seconds).
// The clip plays at the speed that keeps its feet on the ground (clipSpeed,
// from what a second of each clip covers on our characters: CLIP_SPEED); the
// factor each gait comes to is in docs/ARTE.md. The battle's speed
// (tween.ts timeRate) scales it all.

/** The clips a gait plays (MOTION_ORDER's moving ones). */
export type GaitMotion = 'walk' | 'run' | 'sprint' | 'crouchWalk';

/**
 * Metres a second each clip's feet cover at its own speed on our characters,
 * the living's (Base Locomotion) and the dead's (Goblin Locomotion: DEAD_MOTIONS):
 * the planted foot's speed back along the ground, measured in the reference
 * lab (its median over the frames it is down, the figure posed in place).
 * Synty's «…_RootMotion» versions say walk 1.45, run 2.59 and sprint 7.24 m/s
 * (Goblin: 1.68, 3.62, 7.24); the dead's vary ±8 % from body to body.
 */
const CLIP_SPEED: Record<'living' | 'dead', Record<GaitMotion, number>> = {
  living: { walk: 1.46, run: 2.4, sprint: 6.9, crouchWalk: 1.48 },
  dead: { walk: 1.78, run: 2.95, sprint: 5.6, crouchWalk: 1.48 },
};

/** Tiles a character's metre covers: the converted metre (SYNTY_SCALE), the characters drawn CHARACTER_SCALE larger. */
const TILES_PER_METRE = SYNTY_SCALE * CHARACTER_SCALE;

export interface Gait {
  motion: GaitMotion;
  /** Seconds a tile takes (a diagonal one, √2 as long). */
  seconds: number;
}

/**
 * The extra a body adds to its gait over the clip: the common dead drag
 * themselves, hunched, the armoured ones tread heavily, the bloated one
 * wobbles, the runner goes bent forward.
 */
export type GaitStyle = 'drag' | 'heavy' | 'wobble' | 'hunched' | null;

interface Gaits {
  one: Gait;
  two: Gait;
  style: GaitStyle;
}

const g = (motion: GaitMotion, seconds: number): Gait => ({ motion, seconds });

/** The living (the squad, the Cuervos, the VIP): a run for a move (8 tiles: 3 s), a sprint for a dash (16: 3.8 s). */
const LIVING: Gaits = { one: g('run', 0.375), two: g('sprint', 0.24), style: null };

/** The dead (Gameplay, 2026-10-14: slower than the living, a dash quicker than a move). */
const GAITS: Partial<Record<TemplateId, Gaits>> = {
  // The slowest, dragging a foot (4 tiles: 4.1 s), but a turn of many of them doesn't drag on.
  zombie: { one: g('walk', 1.02), two: g('walk', 0.77), style: 'drag' },
  xenoid: { one: g('run', 0.52), two: g('sprint', 0.2), style: 'hunched' },
  mec: { one: g('walk', 0.97), two: g('walk', 0.71), style: 'heavy' },
  sectoid: { one: g('walk', 0.92), two: g('walk', 0.66), style: null },
  bloater: { one: g('walk', 1.08), two: g('walk', 0.8), style: 'wobble' },
};

/** Moving crouched (hunkered, held): the crouched walk. */
export const CROUCHED: Gait = g('crouchWalk', 0.71);

/** How template `t` moves. */
export function gaitsOf(t: TemplateId): Gaits {
  return GAITS[t] ?? LIVING;
}

/** The speed template `t`'s clip for gait `g` plays at so its feet keep pace with a tile every `g.seconds`. */
export function clipSpeed(t: TemplateId, g: Gait): number {
  return 1 / (TILES_PER_METRE * CLIP_SPEED[GAITS[t] ? 'dead' : 'living'][g.motion] * g.seconds);
}
