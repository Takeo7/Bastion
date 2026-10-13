import * as THREE from 'three';
import { SPEAKERS, type Appearance, type SpeakerId, type TemplateId } from '@bastion/engine';
import type { FigureRig } from '../game/figures';
import { IDLE_POSE } from '../game/models';
import { SyntyFigure, syntyFigure, voiceFigure } from '../game/syntyFigures';
import type { CharacterId } from '../game/syntyManifest';

// Radio portraits: each speaker is a Synty character (syntyManifest.ts, «the
// radio's voices») standing in their idle pose, framed head and shoulders in
// a soft studio light, rendered once to an offscreen canvas and kept as an
// image. Soldiers get theirs the same way (soldierPortrait). The «Voz sin
// nombre» on the Congregación's frequency shows no face: a veiled figure
// against the light. Models load in the background, so portraitSrc hands out
// a silhouette first and swaps it when the bust is ready.

const SIZE = 160;

interface Voice {
  character: CharacterId;
  /** Helmet, torso, arms and legs colours (null: as Synty painted it). */
  dyes?: (string | null)[];
  skin?: string;
  /** Turned a little so it isn't a passport photo. */
  turn?: number;
  /** Lit only from behind: a shape and no face. */
  backlit?: boolean;
}

const VOICES: Record<SpeakerId, Voice> = {
  // Inés Albarrán, «Morse»: sixty, grey hair, her radio headphones, a wine cardigan.
  morse: { character: 'voiceMorse', dyes: [null, '#7a2e3a', '#7a2e3a', null] },
  // Teodoro Vidal, «el Relojero»: bald, white moustache, his good jacket.
  teo: { character: 'voiceTeo' },
  // Dra. Amara Nwosu: dark curls, glasses, a white coat.
  nwosu: { character: 'voiceNwosu', dyes: [null, '#e8e6df', '#e8e6df', null], skin: '#6b4330' },
  // Simón Ferreira: a Cuervos deserter in a field jacket; he shows his scarred side.
  simon: { character: 'voiceSimon', turn: -0.48 },
  // Celia Arranz, «la Voz»: a radio host's headset, ivory and composed.
  voz: { character: 'voiceVoz', dyes: [null, '#e9e4d8', '#e9e4d8', null] },
  // Ruth Halvorsen: platinum hair, a charcoal suit.
  halvorsen: { character: 'voiceHalvorsen', dyes: [null, '#2b2f35', '#2b2f35', null] },
  // The nameless voice of the Congregación: a veil against a green glow, nobody you could name.
  coro: { character: 'nun', turn: 0.35, backlit: true },
};

const done = new Map<string, string>();
const pending = new Map<string, Promise<string>>();
/** One render at a time on the one offscreen renderer. */
let queue: Promise<unknown> = Promise.resolve();
let renderer: THREE.WebGLRenderer | null = null;
let idle = 0;

/** Until the bust is ready: a head-and-shoulders shadow (the key keeps each one's source apart). */
function silhouette(key: string): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160"><!--${key}--><path fill="rgba(16,20,24,0.3)" d="M80 34a24 27 0 1 1 0 54a24 27 0 1 1 0-54zM30 160c0-42 22-60 50-60s50 18 50 60z"/></svg>`;
  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
}

function gl(): THREE.WebGLRenderer {
  window.clearTimeout(idle);
  if (!renderer) {
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    renderer.setPixelRatio(1);
    renderer.setSize(SIZE, SIZE, false);
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.1;
    renderer.setClearColor(0x000000, 0);
  }
  return renderer;
}

/** The offscreen renderer gives its context back once nobody has asked for a portrait in a while. */
function release(): void {
  window.clearTimeout(idle);
  idle = window.setTimeout(() => {
    renderer?.dispose();
    renderer?.forceContextLoss();
    renderer = null;
  }, 3000);
}

/** Studio light: a warm key from the front left, a cool rim from behind, a soft sky. `backlit`: the rim alone, green. */
function studio(subject: THREE.Object3D, backlit = false): THREE.Scene {
  const scene = new THREE.Scene();
  scene.add(subject);
  if (backlit) {
    scene.add(new THREE.HemisphereLight(0x203028, 0x050807, 0.35));
    for (const x of [1.3, -1.3]) {
      const rim = new THREE.DirectionalLight(0x7dffb0, 3.2);
      rim.position.set(x, 0.8, -1);
      scene.add(rim);
    }
    return scene;
  }
  scene.add(new THREE.HemisphereLight(0xd8e4ff, 0x4a3a30, 1.3));
  const key = new THREE.DirectionalLight(0xffe6c8, 2.4);
  key.position.set(-1, 1.2, 1.5);
  const rim = new THREE.DirectionalLight(0xa8c8ff, 1.4);
  rim.position.set(1.2, 0.6, -1);
  scene.add(key, rim);
  return scene;
}

function shoot(scene: THREE.Scene, target: THREE.Vector3, distance: number): string {
  const r = gl();
  const camera = new THREE.PerspectiveCamera(20, 1, 0.05, 10);
  camera.layers.enableAll();
  camera.position.copy(target).add(new THREE.Vector3(0, 0.03, distance));
  camera.lookAt(target);
  r.render(scene, camera);
  return r.domElement.toDataURL('image/png');
}

/** Every atlas the figure wears has its image. */
function painted(rig: FigureRig): boolean {
  let ok = true;
  rig.root.traverse((o) => {
    const map = o instanceof THREE.Mesh ? (o.material as THREE.MeshStandardMaterial).map : null;
    if (map && map.image == null) ok = false;
  });
  return ok;
}

/** Resolves once the figure is dressed, has its clips and its atlas (or gives up and shoots what there is). */
async function dressed(rig: FigureRig): Promise<void> {
  for (let i = 0; i < 300; i++) {
    if (!(rig instanceof SyntyFigure) || (rig.ready && painted(rig))) return;
    await new Promise((r) => setTimeout(r, 50));
  }
}

/** A character's head and shoulders, in their idle pose, turned a little. */
async function bust(rig: FigureRig, turn: number, backlit = false): Promise<string> {
  await dressed(rig);
  // The clips blend in over a few frames.
  for (let t = 0.1; t <= 1.2; t += 0.1) rig.applyPose(IDLE_POSE, t);
  rig.root.rotation.y = turn;
  rig.root.updateMatrixWorld(true);
  const head = rig.root.getObjectByName('Head');
  const target = head ? head.getWorldPosition(new THREE.Vector3()) : new THREE.Vector3(0, (rig.top ?? 1.2) - 0.15, 0);
  target.y += 0.02;
  const url = shoot(studio(rig.root, backlit), target, 1.45);
  // The figure's own material goes; models, atlases and shared materials (the plain one too) stay with the game.
  rig.root.traverse((o) => {
    if (!(o instanceof THREE.Mesh)) return;
    const m = o.material as THREE.Material;
    if (m.userData.dyes && !m.userData.shared) m.dispose();
  });
  return url;
}

/**
 * Renders a portrait: `prepare` starts its loading at once and returns the
 * shot, which waits its turn after the others. Caches the image and swaps it
 * into any image still showing its silhouette.
 */
function render(key: string, prepare: () => () => Promise<string> | string): Promise<string> {
  let job = pending.get(key);
  if (job) return job;
  let shot: () => Promise<string> | string;
  try {
    shot = prepare();
  } catch (e) {
    return Promise.reject(e);
  }
  job = queue.then(shot).then((url) => {
    done.set(key, url);
    pending.delete(key);
    const shadow = silhouette(key);
    for (const img of Array.from(document.images)) if (img.src === shadow) img.src = url;
    return url;
  });
  job.catch(() => pending.delete(key)).finally(release);
  queue = job.catch(() => undefined);
  pending.set(key, job);
  return job;
}

function speakerShot(speaker: SpeakerId): () => Promise<string> | string {
  const v = VOICES[speaker];
  const rig = voiceFigure(v.character, v.dyes ?? null, v.skin);
  return () => bust(rig, v.turn ?? -0.3, v.backlit);
}

/** The speaker's portrait, when it is ready. */
export function whenPortrait(speaker: SpeakerId): Promise<string> {
  return done.has(speaker) ? Promise.resolve(done.get(speaker)!) : render(speaker, () => speakerShot(speaker));
}

/** The speaker's portrait as an image source: a silhouette until it is rendered, then the bust (cached). */
export function portraitSrc(speaker: SpeakerId): string {
  const url = done.get(speaker);
  if (url) return url;
  void whenPortrait(speaker).catch(() => undefined);
  return silhouette(speaker);
}

/** A soldier's portrait, dressed as in battle (the barracks' colours, character and helmet). */
export function soldierPortrait(template: TemplateId, accent: string, appearance: Appearance | undefined, seed: string): Promise<string> {
  const key = `soldier|${template}|${accent}|${seed}|${JSON.stringify(appearance ?? null)}`;
  const url = done.get(key);
  if (url) return Promise.resolve(url);
  return render(key, () => {
    const rig = syntyFigure(template, accent, appearance, undefined, seed);
    if (!rig) throw new Error(`No Synty character for «${template}»`);
    return () => bust(rig, -0.3);
  });
}

/** Renders every speaker's portrait in the background, so the first transmission has its faces. */
export function preloadPortraits(): void {
  for (const speaker of Object.keys(SPEAKERS) as SpeakerId[]) void whenPortrait(speaker).catch(() => undefined);
}

// A few seconds after the game opens, while the menus are quiet.
window.setTimeout(preloadPortraits, 4000);
