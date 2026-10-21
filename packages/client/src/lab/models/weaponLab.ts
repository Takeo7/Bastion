import { PLAYER_COLORS, type Appearance, type Arms, type BuildId, type TemplateId, type Tier } from '@bastion/engine';
import * as THREE from 'three';
import type { Carried, FigurePose, FigureRig } from '../../game/figures';
import { buildUnit, IDLE_POSE, isFigure } from '../../game/models';
import { Renderer } from '../../game/renderer';
import { LOW_READY, SYNTY_HANDS, SYNTY_LOD, SyntyFigure } from '../../game/syntyFigures';
import type { ActionId } from '../../game/syntyManifest';

// Weapons lab (/armas.html): how every weapon sits in the hands of every kind
// of body, pose by pose, in close-up (bodies: the wardrobe's, 'police1:f:heavy'). `__weapons.sheet(name, pose, bodies)`
// saves a contact sheet (rows: weapons; columns: bodies) to the dev server's
// /__snap; poses: idle, aim, run, crouch, crouchAim, reload, throw, hit, climb,
// dead, windup, strike; views: right, left, front, back. `__weapons.ARMS_CASES`
// are a campaign soldier's own (docs/FASE8.md §A), with what each has drawn;
// `__weapons.MOD_CASES`, what is mounted on the gun and its wear (FASE9.md §5).
// `__weapons.HANDS.fingers` / `.elbows` off: the hands flat and the arms as
// the IK leaves them, to compare.

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

/** A body's look: a character of the manifest, or one of the wardrobe's outfits with a sex and a build ('police1:f:heavy'). */
const look = (body: string): Appearance => {
  const [character, sex, build] = body.split(':') as [string, 'f' | 'm' | undefined, BuildId | undefined];
  return { primary: '#56605a', secondary: PLAYER_COLORS[0], visor: '#9fe3ff', head: 'bare', build: build ?? 'normal', ...(sex ? { sex } : {}), character };
};

/** A body with a weapon, posed for `seconds` at 30 fps so the clips have blended in. */
async function posed(template: TemplateId, tier: Tier, body: string | null, pose: Partial<FigurePose>, arms?: Arms, draw?: Carried): Promise<FigureRig> {
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
  bodies: string[] = ['survivor2:m:normal', 'survivor1:f:normal', 'police1:m:heavy', 'civilian2:f:heavy', 'survivor4:f:slim', 'outlaw1:m:slim'],
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

/**
 * A Mixamo action (ACTIONS, docs/MIXAMO.md) on `bodies` at `times` (seconds in): a row a time, a column a body, the
 * whole body from `viewName`; `raw`, the clip alone (no procedural layers), else as the game poses it over them.
 */
async function act(name: string, id: ActionId, bodies: string[], times: number[], viewName = 'front', distance = 3.4, raw = true, weapon: WeaponCase = WEAPON_CASES[0]!): Promise<string> {
  const cell = 300;
  const out = document.createElement('canvas');
  out.width = cell * bodies.length;
  out.height = cell * times.length;
  const g = out.getContext('2d')!;
  g.font = '14px sans-serif';
  const src = renderer.gl.domElement;
  const camera = renderer.camera;
  const view = (VIEWS[viewName] ?? VIEWS.front!).clone().normalize();
  for (const [col, body] of bodies.entries()) {
    const rig = await posed(weapon.template, weapon.tier, body, {}, weapon.arms, weapon.draw);
    scene.add(rig.root);
    const p: FigurePose = { ...IDLE_POSE, spin: 0, search: 0 };
    for (const [row, t] of times.entries()) {
      // Into the clip, a few frames to settle the mixer at that time.
      (rig as SyntyFigure).action = { id, time: t, share: 1, upper: false, raw };
      for (let k = 0; k < 12; k++) rig.applyPose(p, 2 + row * 0.5 + k / 30);
      rig.root.updateMatrixWorld(true);
      const target = new THREE.Vector3(0, 0.95, 0);
      camera.position.copy(target).addScaledVector(view, distance);
      camera.lookAt(target);
      renderer.gl.setPixelRatio(1);
      renderer.gl.setSize(900, 900, false);
      const { composer } = (renderer as unknown as { post: { composer: { setPixelRatio(r: number): void; setSize(w: number, h: number): void } } }).post;
      composer.setPixelRatio(1);
      composer.setSize(900, 900);
      camera.fov = 40;
      camera.aspect = 1;
      camera.updateProjectionMatrix();
      renderer.render(0);
      const side = Math.min(src.width, src.height);
      g.drawImage(src, (src.width - side) / 2, (src.height - side) / 2, side, side, col * cell, row * cell, cell, cell);
      g.fillStyle = '#fff';
      g.fillText(`${id} ${t.toFixed(2)} s · ${body}`, col * cell + 6, row * cell + 16);
    }
    (rig as SyntyFigure).action = null;
    scene.remove(rig.root);
  }
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: out.toDataURL('image/jpeg', 0.88) });
  return name;
}

/** A stretch of a film (film): `seconds` long, the pose at each moment of it (`t` from 0 to 1), and its caption. */
interface Scene {
  label: string;
  seconds: number;
  pose: (t: number) => Partial<FigurePose>;
}

const ramp = (t: number, a: number, b: number) => THREE.MathUtils.clamp((t - a) / (b - a), 0, 1);
const ease = (t: number) => 0.5 - 0.5 * Math.cos(Math.PI * t);
/** A blade's stroke `s` seconds in, as unitViews lunge poses it (from 0.15 s, so it starts at rest). */
function lunge(s: number): Partial<FigurePose> {
  const t = s - 0.15;
  if (t < 0) return {};
  if (t < 0.2) return { lean: -0.25 * (t / 0.2), swing: -ease(t / 0.2) };
  if (t < 0.34) return { lean: -0.25 + 0.75 * ((t - 0.2) / 0.14), swing: -1 + 2 * ease((t - 0.2) / 0.14) };
  if (t < 0.69) return { lean: 0.5 * (1 - (t - 0.34) / 0.35), swing: 1 - ease((t - 0.34) / 0.35) };
  return {};
}

/** The game's timings (unitViews: throwMotion, tossStone, the shot's recoil), for film. */
export const FILM_SCENES: Record<string, Scene[]> = {
  rifle: [
    { label: 'Fusil en reposo', seconds: 2, pose: () => ({}) },
    { label: 'Apunta', seconds: 1.2, pose: (t) => ({ aim: ramp(t, 0, 0.35) }) },
    { label: 'Dispara', seconds: 1.5, pose: (t) => ({ aim: 1, recoil: Math.max(0, 1 - ((t * 1.5) % 0.4) / 0.15) * (t < 0.8 ? 1 : 0) }) },
    { label: 'Baja el arma', seconds: 1, pose: (t) => ({ aim: 1 - ramp(t, 0, 0.4) }) },
  ],
  reload: [{ label: 'Recarga', seconds: 2.6, pose: (t) => ({ spin: t < 0.95 ? t / 0.95 : 0 }) }],
  turn: [
    { label: 'Gira 90° en el sitio', seconds: 1.2, pose: (t) => ({ yaw: (Math.PI / 2) * ramp(t, 0.1, 0.6) }) },
    { label: 'Y vuelve', seconds: 1.2, pose: (t) => ({ yaw: (Math.PI / 2) * (1 - ramp(t, 0.1, 0.6)) }) },
  ],
  crouch: [
    { label: 'Se agacha', seconds: 1.2, pose: (t) => ({ crouch: ramp(t, 0, 0.3) }) },
    { label: 'Agachado, apunta', seconds: 1.5, pose: (t) => ({ crouch: 1, aim: ramp(t, 0, 0.3) }) },
    { label: 'Se levanta', seconds: 1, pose: (t) => ({ crouch: 1 - ramp(t, 0.1, 0.5), aim: 1 - ramp(t, 0, 0.3) }) },
  ],
  grenade: [
    { label: 'Granada', seconds: 1.6, pose: (t) => {
      const s = t * 1.6;
      return { lean: s < 0.35 ? -0.35 * (0.5 - 0.5 * Math.cos((Math.PI * s) / 0.35)) : s < 0.49 ? -0.35 + 0.65 * ((s - 0.35) / 0.14) : s < 0.74 ? 0.3 * (1 - (s - 0.49) / 0.25) : 0 };
    } },
  ],
  stone: [
    { label: 'Piedra', seconds: 1.4, pose: (t) => {
      const s = t * 1.4;
      return { toss: s < 0.12 ? 0 : s < 0.36 ? 0.6 * (0.5 - 0.5 * Math.cos((Math.PI * (s - 0.12)) / 0.24)) : s < 0.58 ? 0.6 + 0.4 * ((s - 0.36) / 0.22) : 0 };
    } },
  ],
  // unitViews lunge: wound up 0.2 s, the stroke 0.14 s, back 0.35 s.
  knife: [{ label: 'Cuchillo', seconds: 1.3, pose: (t) => lunge(t * 1.3) }],
  machete: [{ label: 'Machete', seconds: 1.3, pose: (t) => lunge(t * 1.3) }],
  pistol: [
    { label: 'Pistola a dos manos', seconds: 1.6, pose: () => ({}) },
    { label: 'Apunta', seconds: 1, pose: (t) => ({ aim: ramp(t, 0, 0.35) }) },
    { label: 'Dispara', seconds: 1.2, pose: (t) => ({ aim: 1, recoil: Math.max(0, 1 - ((t * 1.2) % 0.4) / 0.15) * (t < 0.8 ? 1 : 0) }) },
    { label: 'Baja el arma', seconds: 0.8, pose: (t) => ({ aim: 1 - ramp(t, 0, 0.4) }) },
  ],
};

/**
 * The frames of a film of `scenes` (FILM_SCENES names, one after another) at 30 fps, `from` frame on, `count` of
 * them (the caller asks in batches): JPEG data URLs, captioned. One body, the rifle, or the pistol for 'pistol'.
 */
const filmRigs = new Map<string, FigureRig>();
async function film(names: string[], body: string, from: number, count: number, size = 720): Promise<string[]> {
  const scenes = names.flatMap((n) => FILM_SCENES[n] ?? []);
  const total = Math.round(scenes.reduce((n, s) => n + s.seconds, 0) * 30);
  const pistol = names.includes('pistol');
  const blade = names.includes('knife') ? 'knife' : names.includes('machete') ? 'machete' : '';
  const key = `${body}|${pistol}|${blade}`;
  let rig = filmRigs.get(key);
  if (!rig) {
    const c = pistol ? ARMS_CASES[0]! : blade === 'knife' ? ARMS_CASES[2]! : blade === 'machete' ? ARMS_CASES[3]! : WEAPON_CASES[0]!;
    rig = await posed(c.template, c.tier, body, {}, c.arms, c.draw);
    filmRigs.set(key, rig);
  }
  for (const r of filmRigs.values()) r.root.removeFromParent();
  scene.add(rig.root);
  const out: string[] = [];
  const src = renderer.gl.domElement;
  const camera = renderer.camera;
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  const g = canvas.getContext('2d')!;
  g.font = '22px sans-serif';
  renderer.gl.setPixelRatio(1);
  renderer.gl.setSize(size, size, false);
  const { composer } = (renderer as unknown as { post: { composer: { setPixelRatio(r: number): void; setSize(w: number, h: number): void } } }).post;
  composer.setPixelRatio(1);
  composer.setSize(size, size);
  camera.fov = 34;
  camera.aspect = 1;
  camera.updateProjectionMatrix();
  for (let f = from; f < Math.min(total, from + count); f++) {
    let at = f / 30;
    let k = 0;
    while (k < scenes.length - 1 && at >= scenes[k]!.seconds) at -= scenes[k++]!.seconds;
    const sc = scenes[k]!;
    const p: FigurePose = { ...IDLE_POSE, spin: 0, search: 0, yaw: 0, ...sc.pose(Math.min(1, at / sc.seconds)) };
    rig.applyPose(p, 10 + f / 30);
    rig.root.updateMatrixWorld(true);
    const target = new THREE.Vector3(0, 0.9, 0);
    camera.position.copy(target).add(new THREE.Vector3(1.9, 0.55, 2.6));
    camera.lookAt(target);
    renderer.render(1 / 30);
    g.drawImage(src, 0, 0, size, size);
    g.fillStyle = 'rgba(0,0,0,0.45)';
    g.fillRect(0, size - 44, size, 44);
    g.fillStyle = '#fff';
    g.fillText(sc.label, 16, size - 14);
    out.push(canvas.toDataURL('image/jpeg', 0.85));
  }
  return out;
}

(window as unknown as { __weapons: unknown }).__weapons = { sheet, act, film, FILM_SCENES, posed, renderer, POSES, WEAPON_CASES, ARMS_CASES, MOD_CASES, HANDS: SYNTY_HANDS, LOW_READY };
