import { PLAYER_COLORS, type Appearance, type TemplateId } from '@bastion/engine';
import * as THREE from 'three';
import { buildFigure, type FigurePose, type FigureRig, type Gear } from '../../game/figures';
import { IDLE_POSE } from '../../game/models';
import { Renderer } from '../../game/renderer';
import { voxelizeUnit, voxelSurface } from '../../game/voxel';
import { sound } from '../../audio/bank';
import { SONG, SongFx, singerMark } from '../../game/songFx';
import { voxFigure } from '../../game/voxFigures';

// Model lab (/modelos.html): the hand-made voxel figures next to the ones
// converted from primitives, in the game's light, in several poses: in each
// pair today's model on the left, hand-made on the right. `#todos`, `#par0`,
// `#lado0`, `#atras0`, `#tactico`, `#lejos` pick the camera; `__models`
// lets tools take stills.

const container = document.getElementById('models')!;
const renderer = new Renderer(container);
const scene = renderer.scene;


interface Sample {
  template: TemplateId;
  accent: string;
  look?: Appearance;
  gear?: Gear;
  pose: Partial<FigurePose>;
}

const custom = (primary: string, secondary: string, visor: string, head: Appearance['head'], build: Appearance['build']): Appearance => ({ primary, secondary, visor, head, build });
const P1 = PLAYER_COLORS[0];
const P2 = PLAYER_COLORS[1];

const SAMPLES: Sample[] = [
  // The four classes as they come (engine defaults).
  { template: 'specialist', accent: P1, pose: {} },
  { template: 'assault', accent: P1, pose: { aim: 1 } },
  { template: 'grenadier', accent: P1, pose: {} },
  { template: 'sharpshooter', accent: P1, pose: { aim: 1 } },
  // Customized: heads, builds, armour and weapon tiers.
  { template: 'specialist', accent: P2, look: custom('#2f3a44', '#ffa630', '#ffd27a', 'bare', 'slim'), gear: { armor: 2 }, pose: {} },
  { template: 'assault', accent: P1, look: custom('#c9ccd1', '#8f2f2f', '#ff6a5a', 'mask', 'heavy'), gear: { armor: 3, weapon: 3 }, pose: { aim: 1 } },
  { template: 'grenadier', accent: P2, look: custom('#6b7d5a', '#c9a13a', '#7dffb0', 'cap', 'normal'), gear: { armor: 2, weapon: 2 }, pose: { crouch: 1, aim: 0.3 } },
  { template: 'sharpshooter', accent: P1, look: custom('#1d2124', '#5a3f7a', '#d79fff', 'beret', 'slim'), gear: { armor: 3, weapon: 2 }, pose: { run: 1 } },
  { template: 'sectoid', accent: '#ff4a4a', pose: {} },
  { template: 'sectoid', accent: '#ff4a4a', pose: { aim: 1 } },
  // Aliens.
  { template: 'trooper', accent: '#ff4a4a', pose: { aim: 1 } },
  { template: 'officer', accent: '#ff4a4a', pose: {} },
  { template: 'lancer', accent: '#ff4a4a', pose: { run: 1 } },
  { template: 'xenoid', accent: '#ff4a4a', pose: { aim: 1 } },
  { template: 'mec', accent: '#ff4a4a', pose: {} },
  { template: 'zombie', accent: '#ff4a4a', pose: { run: 1 } },
  { template: 'relay', accent: '#ff4a4a', pose: {} },
  { template: 'vip', accent: P1, pose: { run: 1 } },
];

/** Each sample is a pair: today's model on the left, hand-made on the right. */
const SPACING = 2.4;
const ground = new THREE.Mesh(
  new THREE.BoxGeometry(SAMPLES.length * SPACING + 6, 0.2, 10),
  voxelSurface(new THREE.MeshStandardMaterial({ color: 0x8c918a, roughness: 0.9 })),
);
ground.position.set(((SAMPLES.length - 1) * SPACING) / 2, -0.1, 0.5);
ground.receiveShadow = true;
scene.add(ground);
renderer.fitMap(SAMPLES.length * SPACING + 4, 8);
const rigs: { rig: FigureRig; pose: FigurePose; old: boolean }[] = [];
SAMPLES.forEach((s, i) => {
  const pose = { ...IDLE_POSE, phase: i, ...s.pose };
  const old = buildFigure(s.template, s.accent, s.look, s.gear);
  voxelizeUnit(old);
  old.root.position.set(i * SPACING - 0.5, 0, 0);
  scene.add(old.root);
  rigs.push({ rig: old, pose, old: true });
  const hand = voxFigure(s.template, s.accent, s.look, s.gear);
  if (hand) {
    hand.root.position.set(i * SPACING + 0.5, 0, 0);
    scene.add(hand.root);
    rigs.push({ rig: hand, pose, old: false });
  }
});

const centre = new THREE.Vector3(((SAMPLES.length - 1) * SPACING) / 2, 0.9, 0);
const pair = (i: number) => new THREE.Vector3(i * SPACING, 0.85, 0);
const VIEWS: Record<string, () => void> = {
  todos: () => look(centre.clone().add(new THREE.Vector3(1.5, 2.6, 13)), centre, 34),
  espalda: () => look(centre.clone().add(new THREE.Vector3(-1.5, 2.6, -13)), centre, 34),
  // The game's camera: 52° down, 45° turned; at a close zoom and at the default one.
  tactico: () => tactical(11),
  lejos: () => tactical(24),
};
// The hand-made ones alone, soldiers then aliens (`#desfile0`, `#desfile1`).
const parade = (from: number, to: number) => {
  const mid = new THREE.Vector3(((from + to) / 2) * SPACING + 0.5, 0.95, 0);
  const width = (to - from + 1) * SPACING;
  // Far enough that the whole row fits the window's width.
  const fov = 24;
  const half = Math.atan(Math.tan(THREE.MathUtils.degToRad(fov / 2)) * renderer.camera.aspect);
  const d = (width / 2 / Math.tan(half)) * 1.02;
  look(mid.clone().add(new THREE.Vector3(0, d * 0.12, d)), mid, fov);
};
VIEWS.desfile0 = () => parade(0, 7);
VIEWS.desfile1 = () => parade(8, SAMPLES.length - 1);

// One pair up close: from the front-right, from the side and from behind.
SAMPLES.forEach((_, i) => {
  VIEWS[`par${i}`] = () => look(pair(i).add(new THREE.Vector3(0.9, 0.5, 3.4)), pair(i), 34);
  VIEWS[`lado${i}`] = () => look(pair(i).add(new THREE.Vector3(-3.6, 0.4, 0.4)), pair(i), 34);
  VIEWS[`atras${i}`] = () => look(pair(i).add(new THREE.Vector3(-0.9, 0.6, -3.4)), pair(i), 34);
  // Further back, for the big ones (MEC, relay).
  VIEWS[`grande${i}`] = () => look(pair(i).add(new THREE.Vector3(1.6, 1.6, 6.2)), pair(i).add(new THREE.Vector3(0, 0.5, 0)), 34);
});

function look(pos: THREE.Vector3, target: THREE.Vector3, fov: number): void {
  const cam = renderer.camera;
  cam.fov = fov;
  cam.position.copy(pos);
  cam.lookAt(target);
  cam.updateProjectionMatrix();
}

function tactical(distance: number): void {
  const pitch = THREE.MathUtils.degToRad(52);
  const yaw = Math.PI / 4;
  const h = Math.cos(pitch) * distance;
  const target = centre.clone().setY(0);
  look(target.clone().add(new THREE.Vector3(Math.sin(yaw) * h, Math.sin(pitch) * distance, Math.cos(yaw) * h)), target, 40);
}

let view = location.hash.slice(1) || 'todos';
(VIEWS[view] ?? VIEWS.todos!)();
window.addEventListener('hashchange', () => {
  view = location.hash.slice(1);
  (VIEWS[view] ?? VIEWS.todos!)();
});

// ---------------------------------------------------------------- songs
// A small stage for the Canto de Velmar's effects: a singer (with the
// singer's mark) and three troopers in front, away from the pairs.
const songFx = new SongFx();
scene.add(songFx.group);
renderer.onFrame((dt) => songFx.tick(dt));
const STAGE = new THREE.Vector3(-8, 0, 0);
const singer = voxFigure('specialist', P1)!;
singer.root.position.copy(STAGE);
singer.yaw.rotation.y = Math.PI / 2;
singer.applyPose({ ...IDLE_POSE, aim: 1 }, 0);
const singerGlow = singerMark();
singerGlow.position.set(0, 1.78, 0);
singer.yaw.add(singerGlow);
scene.add(singer.root);
const foes = [new THREE.Vector3(-3, 0, -1.6), new THREE.Vector3(-2, 0, 0), new THREE.Vector3(-3, 0, 1.6)].map((p) => {
  const r = voxFigure('trooper', '#ff4a4a')!;
  r.root.position.copy(p);
  r.yaw.rotation.y = -Math.PI / 2;
  r.applyPose(IDLE_POSE, 0);
  scene.add(r.root);
  return p.clone().setY(1.1);
});
const stageGround = new THREE.Mesh(new THREE.BoxGeometry(10, 0.2, 8), voxelSurface(new THREE.MeshStandardMaterial({ color: 0x8c918a, roughness: 0.9 })));
stageGround.position.set(-5, -0.1, 0);
stageGround.receiveShadow = true;
scene.add(stageGround);
const hands = STAGE.clone().add(new THREE.Vector3(0.4, 1.2, 0));
VIEWS.canto = () => look(new THREE.Vector3(-5, 4.2, 7.5), new THREE.Vector3(-5, 0.9, 0), 40);

/** Each song with its sound, as game.ts will play it. */
const SONGS: Record<string, () => Promise<void> | void> = {
  bolt: () => (sound('song.bolt'), songFx.bolt(hands, foes[1]!)),
  fireball: () => (sound('song.fire'), songFx.fireball(hands, foes[1]!.clone().setY(0.1), 2.5)),
  arrow: () => (sound('song.arrow'), songFx.arrow(hands, foes[0]!)),
  rain: () => (sound('song.arrow'), songFx.arrows(hands, foes)),
  vanish: () => (sound('song.vanish'), songFx.vanish(STAGE)),
  appear: () => (sound('song.appear'), songFx.appear(STAGE.clone().add(new THREE.Vector3(1.5, 0, 1.5)))),
  dome: () => (sound('song.dome'), songFx.dome(foes[1]!.clone().setY(0), 2)),
  storm: () => {
    sound('song.storm');
    foes.forEach((_, i) => sound('song.thunder', { delay: i * 0.18 }));
    return songFx.storm(foes);
  },
  hymn: () => (sound('song.aura'), songFx.aura(STAGE, 3, [STAGE.clone().add(new THREE.Vector3(0, 0, 1.4))], SONG.green)),
  aegis: () => (sound('song.aura'), songFx.aura(STAGE, 1.6, [STAGE], SONG.cyan)),
  heal: () => (sound('song.heal'), songFx.heal(STAGE)),
  revive: () => (sound('song.revive'), songFx.revive(STAGE.clone().add(new THREE.Vector3(1.5, 0, -1.5)))),
  cleanse: () => (sound('song.cleanse'), songFx.cleanse(STAGE)),
  mark: () => (sound('song.mark'), songFx.mark(foes[2]!)),
  blade: () => (sound('song.blade'), songFx.blade(foes[1]!, new THREE.Vector3(1, 0, 0))),
};

// Studio light from the camera's side, to judge the sculpt (the game's sun
// comes from behind the default camera, so the faces you see are in shade).
const studio = new THREE.DirectionalLight(0xfff2e0, 0);
studio.position.set(6, 8, 14);
scene.add(studio);

let frozen: number | null = null;
renderer.onFrame((_dt, time) => {
  const t = frozen ?? time;
  for (const { rig, pose } of rigs) rig.applyPose(pose, t);
});

Object.assign(window, {
  __models: {
    /** Plays a song's effect and sound; with `still`, freezes the scene `at` seconds in for a capture. */
    async song(name: string, at?: number) {
      void SONGS[name]?.();
      if (at !== undefined) {
        const steps = Math.round(at * 60);
        for (let i = 0; i < steps; i++) songFx.tick(1 / 60);
      }
    },
    songs: Object.keys(SONGS),
    /** Freezes the animation at `time` and shows a view. */
    still(name: string, time = 0.35, light = false) {
      frozen = time;
      studio.intensity = light ? 2.2 : 0;
      for (const r of rigs) r.rig.root.visible = !(r.old && name.startsWith('desfile'));
      (VIEWS[name] ?? VIEWS.todos!)();
    },
  },
});
