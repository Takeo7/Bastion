import { PLAYER_COLORS, SCENES, SPEAKERS, type Appearance, type SpeakerId, type TemplateId } from '@bastion/engine';
import * as THREE from 'three';
import type { FigurePose, FigureRig } from '../../game/figures';
import { buildUnit, IDLE_POSE, isFigure } from '../../game/models';
import { Renderer } from '../../game/renderer';
import { atlasMaterial } from '../../game/synty';
import { PROPS, SURVIVOR_CHOICES, type PropId } from '../../game/syntyManifest';
import { syntyProp, syntyPropsLoaded } from '../../game/syntyProps';
import { portraitSrc, soldierPortrait } from '../../strategy/portraits';
import '../../strategy/transmissions.css';

// Model lab (/modelos.html): every Synty character the game uses, in the
// game's light, all in one pose. Front row: the survivors a soldier can be
// (barracks «Personaje»), in squad colours; back row: the Cuervos, the dead
// and who is rescued. The hash picks the pose (#idle, #aim, #run, #crouch,
// #search, #hit, #dead) and `__models.pose('aim')` changes it; the clips and
// procedural layers play live. Above them, the radio portraits
// (strategy/portraits.ts) at twice their panel size, and two soldiers';
// `?radio` also plays the first transmission in the game's own panel.
// `__models.props(name, ids)` saves a sheet of map props to /__snap, each
// on a grid of tiles as the map places it (fit, yaw), an arrow to the map's
// north (−z, a facing of 0), seen from the south-east above.

const container = document.getElementById('models')!;
const renderer = new Renderer(container);
const scene = renderer.scene;

const POSES: Record<string, Partial<FigurePose>> = {
  idle: {},
  aim: { aim: 1 },
  run: { run: 1 },
  crouch: { crouch: 1 },
  search: { crouch: 0.6, search: 1 },
  hit: { hit: 0.6, flash: 0.6 },
  dead: { fall: 1 },
};

const P1 = PLAYER_COLORS[0];
const ENEMIES: TemplateId[] = ['trooper', 'officer', 'lancer', 'zombie', 'zombie', 'xenoid', 'mec', 'sectoid', 'bloater', 'vip', 'relay'];

const ground = new THREE.Mesh(new THREE.PlaneGeometry(60, 30).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x77705f, roughness: 0.95 }));
ground.position.set(10, 0, 8);
ground.receiveShadow = true;
scene.add(ground);
renderer.fitMap(20, 16);

const rigs: FigureRig[] = [];
const look = (character: string): Appearance => ({ primary: '#56605a', secondary: P1, visor: '#9fe3ff', head: 'bare', build: 'normal', character });

SURVIVOR_CHOICES.forEach((c, i) => {
  const rig = buildUnit('rookie', P1, look(c.id), undefined, c.id);
  if (!isFigure(rig)) return;
  rig.root.position.set(2 + i * 1.05, 0, 10);
  scene.add(rig.root);
  rigs.push(rig);
});
ENEMIES.forEach((t, i) => {
  const rig = buildUnit(t, '#ff4d4d', undefined, undefined, `${t}${i}`);
  if (!isFigure(rig)) return;
  rig.root.position.set(3 + i * 1.4, 0, 7);
  scene.add(rig.root);
  rigs.push(rig);
});

// ---------------------------------------------------------------- portraits

const strip = document.getElementById('portraits')!;

function card(title: string, speaker: string, src: string): HTMLImageElement {
  const frame = document.createElement('div');
  frame.className = 'radio-portrait bust';
  frame.dataset.speaker = speaker;
  frame.style.cssText = 'width: 112px; height: 112px';
  const img = document.createElement('img');
  img.src = src;
  frame.append(img);
  const label = document.createElement('div');
  label.textContent = title;
  const cell = document.createElement('div');
  cell.append(frame, label);
  strip.append(cell);
  return img;
}

for (const speaker of Object.keys(SPEAKERS) as SpeakerId[]) card(SPEAKERS[speaker].name, speaker, portraitSrc(speaker));
for (const [i, c] of (['hunter', 'mechanic'] as const).entries()) {
  const accent = PLAYER_COLORS[i]!;
  const img = card(`Soldado (${c})`, 'soldier', '');
  void soldierPortrait('rookie', accent, { ...look(c), secondary: accent }, c).then((url) => (img.src = url));
}

if (new URLSearchParams(location.search).has('radio')) {
  void Promise.all([import('../../styles.css'), import('../../strategy/transmissions')]).then(([, { RadioPanel }]) => {
    const panel = new RadioPanel();
    document.body.append(panel.el);
    const scene = SCENES[0]!;
    panel.play(scene, scene.lines, { saludo: 'Arturo, Ana' });
  });
}

renderer.camera.position.set(10, 4.5, 17.5);
renderer.camera.lookAt(10, 0.9, 8.5);

let pose: FigurePose = { ...IDLE_POSE, spin: 0, search: 0 };

function setPose(name: string): void {
  pose = { ...IDLE_POSE, spin: 0, search: 0, ...(POSES[name] ?? {}) };
}

setPose(location.hash.slice(1) || 'idle');
window.addEventListener('hashchange', () => setPose(location.hash.slice(1)));

let clock = 0;
renderer.onFrame((dt) => {
  clock += dt;
  for (const rig of rigs) rig.applyPose(pose, clock);
});

/** A sheet of map props (ids from PROPS, all if none), `columns` across, to /__snap as `name`. */
async function props(name: string, ids: PropId[] = Object.keys(PROPS) as PropId[], columns = 4): Promise<string> {
  for (const id of ids) syntyProp(id);
  await syntyPropsLoaded();
  for (const r of rigs) r.root.visible = false;
  ground.visible = false;
  const cell = 320;
  const out = document.createElement('canvas');
  out.width = cell * columns;
  out.height = cell * Math.ceil(ids.length / columns);
  const g = out.getContext('2d')!;
  g.font = '15px sans-serif';
  const src = renderer.gl.domElement;
  const camera = renderer.camera;
  const lines = new THREE.LineBasicMaterial({ color: 0xd8d0b8 });
  const north = new THREE.MeshBasicMaterial({ color: 0xff5040 });
  for (const [k, id] of ids.entries()) {
    const holder = new THREE.Group();
    let size = 1;
    for (const part of syntyProp(id) ?? []) {
      const mesh = new THREE.Mesh(part.geometry, atlasMaterial(part.atlas));
      mesh.matrixAutoUpdate = false;
      mesh.matrix.copy(part.matrix);
      mesh.castShadow = true;
      holder.add(mesh);
    }
    holder.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(holder);
    size = Math.max(1, box.max.x - box.min.x, box.max.z - box.min.z, box.max.y - box.min.y);
    // Tiles under it, and the way north.
    const n = Math.ceil(size / 2) + 1;
    const grid: number[] = [];
    for (let t = -n; t <= n; t++) grid.push(t, 0.01, -n, t, 0.01, n, -n, 0.01, t, n, 0.01, t);
    const tiles = new THREE.LineSegments(new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(grid, 3)), lines);
    const arrow = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.02, n).translate(0, 0.02, -n / 2), north);
    holder.add(tiles, arrow);
    scene.add(holder);
    const target = new THREE.Vector3(0, (box.max.y - box.min.y) * 0.35, 0);
    camera.position.copy(target).add(new THREE.Vector3(0.7, 0.85, 1).normalize().multiplyScalar(size * 2.4 + 1.5));
    camera.lookAt(target);
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
    const col = k % columns;
    const row = Math.floor(k / columns);
    g.drawImage(src, (src.width - side) / 2, (src.height - side) / 2, side, side, col * cell, row * cell, cell, cell);
    g.fillStyle = '#fff';
    g.fillText(`${id} · ${(box.max.x - box.min.x).toFixed(1)}×${(box.max.z - box.min.z).toFixed(1)}×${(box.max.y - box.min.y).toFixed(1)}`, col * cell + 6, row * cell + 18);
    scene.remove(holder);
  }
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: out.toDataURL('image/jpeg', 0.88) });
  return name;
}

(window as unknown as { __models: unknown }).__models = { pose: setPose, renderer, rigs, props };
