import { PLAYER_COLORS, type Appearance, type Arms, type TemplateId, type Tier } from '@bastion/engine';
import * as THREE from 'three';
import type { Carried, FigurePose, FigureRig } from '../../game/figures';
import { buildUnit, IDLE_POSE, isFigure } from '../../game/models';
import { Renderer } from '../../game/renderer';
import { SYNTY_LOD, SyntyFigure } from '../../game/syntyFigures';
import type { CharacterId } from '../../game/syntyManifest';

// Weapons lab (/armas.html): how every weapon sits in the hands of every kind
// of body, pose by pose, in close-up. `__weapons.sheet(name, pose, bodies)`
// saves a contact sheet (rows: weapons; columns: bodies) to the dev server's
// /__snap; poses: idle, aim, run, crouch, crouchAim, reload, throw, hit, climb,
// dead, windup, strike; views: right, left, front, back. `__weapons.ARMS_CASES`
// are a campaign soldier's own (docs/FASE8.md §A), with what each has drawn;
// `__weapons.MOD_CASES`, what is mounted on the gun and its wear (FASE9.md §5).

const container = document.getElementById('weapons')!;
const renderer = new Renderer(container);
const scene = renderer.scene;
SYNTY_LOD.on = false;

const ground = new THREE.Mesh(new THREE.PlaneGeometry(20, 20).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x77705f, roughness: 0.95 }));
ground.receiveShadow = true;
scene.add(ground);

/** A weapon as a unit carries it: its template and tier; a campaign soldier's own arms, and which it has in the hand. */
interface WeaponCase {
  label: string;
  template: TemplateId;
  tier: Tier;
  arms?: Arms;
  draw?: Carried;
}

export const WEAPON_CASES: WeaponCase[] = [
  { label: 'rifle', template: 'rookie', tier: 1 },
  { label: 'rifle2', template: 'rookie', tier: 2 },
  { label: 'rifle3', template: 'rookie', tier: 3 },
  { label: 'shotgun', template: 'assault', tier: 1 },
  { label: 'sniper', template: 'sharpshooter', tier: 1 },
  { label: 'sniper2', template: 'sharpshooter', tier: 2 },
  { label: 'machineGun', template: 'grenadier', tier: 1 },
  { label: 'huntingRifle', template: 'trooper', tier: 1 },
  { label: 'smg', template: 'officer', tier: 1 },
  { label: 'baton', template: 'lancer', tier: 1 },
];

const own = (primary: Arms['primary'], melee: Arms['melee'] = null): Arms => ({ primary, melee });

export const ARMS_CASES: WeaponCase[] = [
  { label: 'pistola sola', template: 'rookie', tier: 1, arms: own(null) },
  { label: 'fusil + pistola', template: 'rookie', tier: 1, arms: own('assaultRifle'), draw: 'sidearm' },
  { label: 'cuchillo', template: 'rookie', tier: 1, arms: own('assaultRifle'), draw: 'belt' },
  { label: 'machete', template: 'rookie', tier: 1, arms: own('pumpShotgun', 'machete'), draw: 'belt' },
  { label: 'colgadas', template: 'rookie', tier: 1, arms: own('precisionRifle', 'machete'), draw: 'sidearm' },
  { label: 'cerrojo', template: 'rookie', tier: 1, arms: own('huntingRifle') },
  { label: 'ametralladora ligera', template: 'rookie', tier: 1, arms: own('lightMachineGun') },
];

/** The improvements and the wear on a soldier's gun (docs/FASE9.md §5): `__weapons.sheet(name, pose, bodies, __weapons.MOD_CASES)`. */
export const MOD_CASES: WeaponCase[] = [
  { label: 'fusil · mira y silenciador', template: 'rookie', tier: 1, arms: { ...own('assaultRifle'), mods: ['scope', 'silencer'] } },
  { label: 'precisión · mira', template: 'rookie', tier: 1, arms: { ...own('precisionRifle'), mods: ['scope'] } },
  { label: 'con mira (encontrado)', template: 'rookie', tier: 1, arms: { ...own('scopedRifle'), mods: ['scope', 'silencer'] } },
  { label: 'escopeta · punto rojo', template: 'rookie', tier: 1, arms: { ...own('pumpShotgun'), mods: ['scope'] } },
  { label: 'ametralladora · silenciador', template: 'rookie', tier: 1, arms: { ...own('lightMachineGun'), mods: ['silencer', 'extMag'] } },
  { label: 'fusil gastado', template: 'rookie', tier: 1, arms: { ...own('assaultRifle'), condition: 20 } },
  { label: 'fusil roto', template: 'rookie', tier: 1, arms: { ...own('assaultRifle'), condition: 0 } },
];

const POSES: Record<string, Partial<FigurePose>> = {
  idle: {},
  aim: { aim: 1 },
  run: { run: 1 },
  crouch: { crouch: 1 },
  crouchAim: { crouch: 1, aim: 1 },
  reload: { spin: 0.5 },
  throw: { lean: -0.3 },
  hit: { hit: 0.6 },
  climb: { climb: 1 },
  dead: { fall: 1 },
  windup: { aim: 1, swing: -1, lean: -0.25 },
  strike: { aim: 1, swing: 1, lean: 0.5 },
};

const look = (character: CharacterId): Appearance => ({ primary: '#56605a', secondary: PLAYER_COLORS[0], visor: '#9fe3ff', head: 'bare', build: 'normal', character });

/** A body with a weapon, posed for `seconds` at 30 fps so the clips have blended in. */
async function posed(template: TemplateId, tier: Tier, body: CharacterId | null, pose: Partial<FigurePose>, arms?: Arms, draw?: Carried): Promise<FigureRig> {
  const squad = !['trooper', 'officer', 'lancer'].includes(template);
  const rig = buildUnit(template, PLAYER_COLORS[0], squad && body ? look(body) : undefined, { weapon: tier, arms }, `${template}${body}`);
  if (!isFigure(rig)) throw new Error(`${template} is not a figure`);
  for (let i = 0; i < 200 && rig instanceof SyntyFigure && !rig.ready; i++) await new Promise((r) => setTimeout(r, 30));
  if (draw) rig.draw?.(draw);
  const p: FigurePose = { ...IDLE_POSE, spin: 0, search: 0, ...pose };
  for (let t = 0; t < 1.5; t += 1 / 30) rig.applyPose(p, t);
  return rig;
}

/** Where the close-ups look from (the body faces +z): its right side, its left side, the front. */
const VIEWS: Record<string, THREE.Vector3> = {
  right: new THREE.Vector3(-1, 0.25, 0.6),
  left: new THREE.Vector3(1, 0.25, 0.6),
  front: new THREE.Vector3(0.25, 0.2, 1),
  back: new THREE.Vector3(-0.35, 0.3, -1),
};

/**
 * Saves a sheet: a row per weapon, a column per body, each a close-up of the
 * hands and the gun (`view`: right, left or front; `distance` from the chest).
 */
async function sheet(
  name: string,
  poseName = 'idle',
  bodies: CharacterId[] = ['soldierM', 'soldierF', 'hunter', 'mechanic', 'riotCop', 'coolF'],
  cases = WEAPON_CASES,
  viewName = 'right',
  distance = 2.3,
): Promise<string> {
  const cell = 300;
  const out = document.createElement('canvas');
  out.width = cell * bodies.length;
  out.height = cell * cases.length;
  const g = out.getContext('2d')!;
  g.font = '14px sans-serif';
  const pose = POSES[poseName] ?? {};
  const src = renderer.gl.domElement;
  const camera = renderer.camera;
  const view = (VIEWS[viewName] ?? VIEWS.right!).clone().normalize();
  for (const [row, c] of cases.entries()) {
    for (const [col, body] of bodies.entries()) {
      const rig = await posed(c.template, c.tier, body, pose, c.arms, c.draw);
      scene.add(rig.root);
      // On the hands: between the two, a little up towards the shoulders.
      rig.root.updateMatrixWorld(true);
      const hand = (n: string) => rig.root.getObjectByName(n)?.getWorldPosition(new THREE.Vector3()) ?? new THREE.Vector3(0, rig.chest ?? 0.85, 0.2);
      const target = hand('Hand_L').add(hand('Hand_R')).multiplyScalar(0.5).add(new THREE.Vector3(0, 0.08, 0));
      camera.position.copy(target).addScaledVector(view, distance);
      camera.lookAt(target);
      // A square view of its own each time, whatever the page's (a hidden preview has none, and the renderer follows it).
      renderer.gl.setPixelRatio(1);
      renderer.gl.setSize(900, 900, false);
      const { composer } = (renderer as unknown as { post: { composer: { setPixelRatio(r: number): void; setSize(w: number, h: number): void } } }).post;
      composer.setPixelRatio(1);
      composer.setSize(900, 900);
      camera.fov = 30;
      camera.aspect = 1;
      camera.updateProjectionMatrix();
      renderer.render(0);
      const side = Math.min(src.width, src.height);
      g.drawImage(src, (src.width - side) / 2, (src.height - side) / 2, side, side, col * cell, row * cell, cell, cell);
      g.fillStyle = '#fff';
      g.fillText(`${c.label} · ${body}`, col * cell + 6, row * cell + 16);
      scene.remove(rig.root);
    }
  }
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: out.toDataURL('image/jpeg', 0.88) });
  return name;
}

(window as unknown as { __weapons: unknown }).__weapons = { sheet, posed, renderer, POSES, WEAPON_CASES, ARMS_CASES, MOD_CASES };
