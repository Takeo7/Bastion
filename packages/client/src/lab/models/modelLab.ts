import { PLAYER_COLORS, SCENES, SPEAKERS, type Appearance, type SpeakerId, type TemplateId } from '@bastion/engine';
import * as THREE from 'three';
import type { FigurePose, FigureRig } from '../../game/figures';
import { buildUnit, IDLE_POSE, isFigure } from '../../game/models';
import { Renderer } from '../../game/renderer';
import { SURVIVOR_CHOICES } from '../../game/syntyManifest';
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
  frame.className = 'radio-portrait voxel';
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

(window as unknown as { __models: unknown }).__models = { pose: setPose, renderer, rigs };
