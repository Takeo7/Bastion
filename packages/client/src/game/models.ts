import * as THREE from 'three';
import type { Appearance, TemplateId, Tier } from '@bastion/engine';
import type { FigurePose, FigureRig, Gear } from './figures';
import { preloadSynty, syntyFigure, type Siren } from './syntyFigures';

// The rig every unit's model answers to, for the unit views to move it:
//
//   root    position on the ground (moved by the unit view)
//   └ yaw   facing; +Z is forward
//     ├ pivot   hips: lean, crouch, fall and hit reactions rotate/translate this
//     │ ├ body
//     │ ├ flash      red overlay shown when hit
//     │ └ gunPivot   shoulder: aim (rotation.x) and recoil (position.z)
//     │   └ muzzle   where tracers and flashes start
//     └ drone   specialist's support drone, hovering over the shoulder

export interface UnitRig {
  root: THREE.Group;
  yaw: THREE.Group;
  pivot: THREE.Group;
  gunPivot: THREE.Group;
  muzzle: THREE.Object3D;
  flash: THREE.Mesh;
  drone: THREE.Object3D | null;
  /** Hip height; the pivot rests here when standing. */
  hip: number;
  /** Height of the chest, for aiming and damage numbers. */
  chest: number;
  /** Top of the head, where the flag floats (when the model knows it). */
  top?: number;
  /** The rig moves its own weapon for a reload, instead of the unit view's twirl. */
  ownsReload?: boolean;
}

// Synty models load in the background from the start, so the first battle finds them ready.
preloadSynty();

/**
 * The model a unit is shown with: its Synty character (syntyFigures.ts).
 * `seed` keeps the same survivor for the same soldier (their campaign id). A
 * template the manifest has no character for yet shows as a recruit, so
 * nothing is ever missing from the field. `siren` is what a sabotage's
 * objective is (its alarm, where a car's nose points).
 */
export function buildUnit(template: TemplateId, accent: string, appearance?: Appearance, gear?: Gear, seed?: string, sex?: 'f' | 'm', siren?: Siren): FigureRig {
  let synty = syntyFigure(template, accent, appearance, gear, seed, sex, siren);
  if (!synty) {
    console.warn(`No Synty character for «${template}»: add it to syntyManifest.ts`);
    synty = syntyFigure('rookie', accent, appearance, gear, seed, sex)!;
  }
  synty.applyPose(IDLE_POSE, 0);
  return synty;
}

/** Weapon and armour tier as the figures want them (1–3; anything else is tier 1). */
export function gearOf(weaponTier?: number, armorTier?: number): Gear {
  const tier = (n?: number) => Math.min(3, Math.max(1, Math.round(n ?? 1))) as Tier;
  return { weapon: tier(weaponTier), armor: tier(armorTier) };
}

export function isFigure(rig: UnitRig): rig is FigureRig {
  return 'applyPose' in rig;
}

/** A neutral standing pose for figures, to spread overrides onto. */
export const IDLE_POSE: FigurePose = { crouch: 0, aim: 0, run: 0, recoil: 0, hit: 0, flash: 0, fall: 0, lean: 0, hop: 0, yaw: 0, phase: 0 };
