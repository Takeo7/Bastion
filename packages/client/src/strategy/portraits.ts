import * as THREE from 'three';
import type { SpeakerId } from '@bastion/engine';
import { voxelize, type VoxelSource } from '../game/voxel';

// Voxel busts for the radio transmissions: each speaker is modelled from
// primitives (head, hair, face, shoulders and what makes them recognisable),
// voxelized finer than the battle units so a face reads, rendered once to an
// offscreen canvas and kept as an image. The Coro is not a person: a column
// of glowing tesserae.

const SIZE = 160;
/** Finer than combat (1/16 of a tile): the head is about eleven voxels across. */
const VOXEL = 0.018;

const cache = new Map<SpeakerId, string>();
let renderer: THREE.WebGLRenderer | null = null;

function material(color: number, glow = false): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({ color, emissive: glow ? color : 0x000000, emissiveIntensity: glow ? 1.4 : 0 });
}

/** A bust under construction: parts placed in bust space (head centre near y = 0.06, facing +z). */
class Bust {
  readonly group = new THREE.Group();

  add(geometry: THREE.BufferGeometry, color: number, at: [number, number, number], o: { rot?: [number, number, number]; scale?: [number, number, number]; glow?: boolean } = {}): this {
    const mesh = new THREE.Mesh(geometry, material(color, o.glow));
    mesh.position.set(...at);
    if (o.rot) mesh.rotation.set(...o.rot);
    if (o.scale) mesh.scale.set(...o.scale);
    this.group.add(mesh);
    return this;
  }

  box(w: number, h: number, d: number, color: number, at: [number, number, number], o: { rot?: [number, number, number]; glow?: boolean } = {}): this {
    return this.add(new THREE.BoxGeometry(w, h, d), color, at, o);
  }

  ball(r: number, color: number, at: [number, number, number], scale?: [number, number, number], glow = false): this {
    return this.add(new THREE.SphereGeometry(r, 18, 14), color, at, { scale, glow });
  }

  /** Head, neck, shoulders and the face every person shares. */
  person(skin: number, clothes: number, o: { eyes?: number; brows?: number; lips?: number; build?: number } = {}): this {
    const build = o.build ?? 1;
    this.add(new THREE.CylinderGeometry(0.17 * build, 0.23 * build, 0.24, 18), clothes, [0, -0.24, 0], { scale: [1, 1, 0.55] });
    this.add(new THREE.CylinderGeometry(0.042, 0.046, 0.1, 12), skin, [0, -0.07, 0]);
    this.ball(0.1, skin, [0, 0.06, 0], [0.9, 1.08, 0.98]);
    this.box(0.02, 0.04, 0.03, skin, [0.09, 0.055, 0]).box(0.02, 0.04, 0.03, skin, [-0.09, 0.055, 0]);
    const eyes = o.eyes ?? 0x1d1a18;
    this.box(0.022, 0.016, 0.02, eyes, [0.033, 0.07, 0.09]).box(0.022, 0.016, 0.02, eyes, [-0.033, 0.07, 0.09]);
    if (o.brows !== undefined) this.box(0.03, 0.01, 0.02, o.brows, [0.034, 0.093, 0.09]).box(0.03, 0.01, 0.02, o.brows, [-0.034, 0.093, 0.09]);
    this.box(0.018, 0.034, 0.03, skin, [0, 0.045, 0.097]);
    this.box(0.044, 0.012, 0.02, o.lips ?? new THREE.Color(skin).multiplyScalar(0.7).getHex(), [0, 0.006, 0.088]);
    return this;
  }
}

function morse(): Bust {
  // Inés Albarrán: sixty, grey hair in a bun, radio headphones, wine cardigan over a cream blouse.
  const b = new Bust().person(0xd8a88c, 0x7a2e3a, { brows: 0x8a8a88 });
  b.ball(0.105, 0xb8b6b0, [0, 0.108, -0.025], [0.96, 0.72, 0.98]).ball(0.045, 0xb8b6b0, [0, 0.12, -0.1]);
  b.box(0.21, 0.022, 0.034, 0x2a2e33, [0, 0.175, 0]);
  for (const x of [0.1, -0.1]) b.add(new THREE.CylinderGeometry(0.042, 0.042, 0.034, 14), 0x2a2e33, [x, 0.065, 0], { rot: [0, 0, Math.PI / 2] });
  b.box(0.012, 0.012, 0.1, 0x2a2e33, [0.085, 0.02, 0.05], { rot: [0, -0.45, 0] }).ball(0.014, 0x111315, [0.06, 0.01, 0.095]);
  b.box(0.08, 0.09, 0.03, 0xe8dcc2, [0, -0.15, 0.09]);
  for (const y of [-0.2, -0.26]) b.box(0.012, 0.012, 0.012, 0xd8c690, [0.022, y, 0.11]);
  return b;
}

function teo(): Bust {
  // Teodoro Vidal, «el Relojero»: bald crown, white sides and moustache, a loupe on his forehead, leather apron.
  const b = new Bust().person(0xd7a98a, 0x5b6b7c, { brows: 0xe8e6e0 });
  b.ball(0.102, 0xe8e6e0, [0, 0.04, -0.045], [0.97, 0.62, 0.85]);
  b.box(0.064, 0.016, 0.022, 0xe8e6e0, [0, 0.024, 0.094]);
  // Head strap and the loupe pushed up, its lens catching the light.
  b.box(0.2, 0.014, 0.205, 0x3a2a20, [0, 0.128, 0]);
  b.add(new THREE.CylinderGeometry(0.027, 0.022, 0.04, 12), 0x16181a, [0.03, 0.135, 0.105], { rot: [Math.PI / 2, 0, 0] });
  b.box(0.026, 0.026, 0.01, 0x9fe3ff, [0.03, 0.135, 0.128], { glow: true });
  b.box(0.24, 0.22, 0.03, 0x6b4a2e, [0, -0.25, 0.11]);
  for (const x of [0.07, -0.07]) b.box(0.025, 0.12, 0.025, 0x6b4a2e, [x, -0.12, 0.08]);
  return b;
}

function nwosu(): Bust {
  // Dra. Amara Nwosu: short natural hair, thin glasses, a stained lab coat over a dark shirt.
  const b = new Bust().person(0x6b4330, 0xe8e6df, { brows: 0x161210 });
  b.ball(0.118, 0x161210, [0, 0.1, -0.015], [1, 0.92, 1]);
  for (const x of [0.034, -0.034]) {
    b.box(0.044, 0.032, 0.008, 0x2a2420, [x, 0.07, 0.1]);
    b.box(0.032, 0.02, 0.008, 0xbfd8e8, [x, 0.07, 0.104]);
  }
  b.box(0.026, 0.008, 0.008, 0x2a2420, [0, 0.074, 0.104]);
  b.box(0.07, 0.1, 0.03, 0x1f3a40, [0, -0.15, 0.09]);
  for (const x of [0.045, -0.045]) b.box(0.04, 0.12, 0.03, 0xf2f0ea, [x, -0.17, 0.1], { rot: [0, 0, x > 0 ? -0.35 : 0.35] });
  b.box(0.05, 0.03, 0.02, 0x8a8a4a, [-0.08, -0.26, 0.115]).box(0.022, 0.04, 0.02, 0x7a6a40, [-0.06, -0.24, 0.118]);
  return b;
}

function simon(): Bust {
  // Simón Ferreira: short dark hair, stubble, the scar behind his ear where the «nota» was, a ragged Armonía uniform.
  const b = new Bust().person(0xc89a78, 0xb9bcb8, { brows: 0x2a2018 });
  b.ball(0.104, 0x2a2018, [0, 0.122, -0.025], [0.96, 0.6, 0.97]);
  b.box(0.12, 0.05, 0.02, 0x8a6e5a, [0, 0.0, 0.082]);
  b.box(0.01, 0.045, 0.012, 0xb03a3a, [0.096, 0.07, -0.035], { rot: [0.3, 0, 0] }).box(0.012, 0.012, 0.012, 0xc85050, [0.1, 0.04, -0.03]);
  // Armonía collar, torn, and a faded insignia on the shoulder.
  b.box(0.14, 0.035, 0.12, 0x9a9e9a, [0, -0.11, 0.01]);
  b.box(0.03, 0.03, 0.02, 0x4a4e4a, [-0.05, -0.12, 0.075]);
  b.box(0.05, 0.04, 0.02, 0x3f7f7a, [0.14, -0.17, 0.06], { rot: [0, 0.6, 0] });
  b.box(0.06, 0.03, 0.02, 0x7a7c78, [-0.09, -0.29, 0.11]);
  return b;
}

function voz(): Bust {
  // Celia Arranz, «la Voz»: an impeccable silver bob, red lips, ivory jacket with Armonía teal, the implant behind her ear.
  const b = new Bust().person(0xe2b49a, 0xe9e4d8, { brows: 0x8a7a6a, lips: 0xb02a3a });
  b.ball(0.108, 0xd8d0c0, [0, 0.105, -0.032], [1, 0.85, 1]);
  b.box(0.15, 0.03, 0.04, 0xd8d0c0, [0.012, 0.135, 0.07], { rot: [0, 0, -0.18] });
  for (const x of [0.09, -0.09]) b.box(0.03, 0.12, 0.1, 0xd8d0c0, [x, 0.03, -0.025]);
  b.box(0.012, 0.012, 0.012, 0x6ff7f0, [0.1, 0.045, -0.045], { glow: true });
  b.box(0.13, 0.04, 0.13, 0x2a8f8a, [0, -0.11, 0.0]);
  b.box(0.03, 0.03, 0.015, 0xd8b04a, [-0.08, -0.19, 0.1], { glow: true });
  return b;
}

function halvorsen(): Bust {
  // Ruth Halvorsen: pale, platinum hair pulled into a hard bun, straight brows, a charcoal coat with a high collar.
  const b = new Bust().person(0xf0cdb8, 0x2b2f35, { brows: 0x9a9890, eyes: 0x2a3a4a });
  b.ball(0.104, 0xe8e2d0, [0, 0.112, -0.03], [0.96, 0.7, 0.95]).ball(0.04, 0xe8e2d0, [0, 0.09, -0.11]);
  b.add(new THREE.CylinderGeometry(0.07, 0.08, 0.08, 14), 0x2b2f35, [0, -0.085, 0]);
  for (const y of [-0.2, -0.27]) b.box(0.014, 0.014, 0.014, 0x8a8f96, [0.035, y, 0.11]);
  return b;
}

function coro(): Bust {
  // Not a person: a column of tesserae, unevenly lit, as if something were being assembled.
  const b = new Bust();
  let seed = 7;
  const rand = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
  for (let row = 0; row < 11; row++) {
    const y = -0.32 + row * 0.05;
    const n = 1 + Math.floor(rand() * 3);
    for (let i = 0; i < n; i++) {
      const x = (rand() - 0.5) * 0.12;
      const z = (rand() - 0.5) * 0.08;
      const hue = rand() < 0.75 ? 0x7dffb0 : 0x6ff7f0;
      const tone = new THREE.Color(hue).multiplyScalar(0.35 + rand() * 0.65).getHex();
      b.box(0.04, 0.04, 0.04, tone, [x, y, z], { glow: rand() < 0.7 });
    }
  }
  return b;
}

const BUSTS: Record<SpeakerId, () => Bust> = { morse, teo, nwosu, simon, voz, halvorsen, coro };
/** Each bust turned a little so it isn't a passport photo (Simón shows his scarred side). */
const TURN: Partial<Record<SpeakerId, number>> = { simon: -0.48, coro: 0.4 };

function render(speaker: SpeakerId): string {
  renderer ??= new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
  renderer.setPixelRatio(1);
  renderer.setSize(SIZE, SIZE, false);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.1;
  renderer.setClearColor(0x000000, 0);

  const bust = BUSTS[speaker]();
  bust.group.updateMatrixWorld(true);
  const sources: VoxelSource[] = [];
  bust.group.traverse((o) => {
    if (o instanceof THREE.Mesh) sources.push({ mesh: o, matrix: o.matrixWorld.clone() });
  });
  const model = voxelize(sources, { size: VOXEL, jitter: 0.08 });
  model.rotation.y = TURN[speaker] ?? -0.3;

  const scene = new THREE.Scene();
  scene.add(model);
  scene.add(new THREE.HemisphereLight(0xd8e4ff, 0x4a3a30, 1.1));
  const key = new THREE.DirectionalLight(0xffe6c8, 2.3);
  key.position.set(-1, 1.2, 1.5);
  const rim = new THREE.DirectionalLight(0xa8c8ff, 1.3);
  rim.position.set(1.2, 0.6, -1);
  scene.add(key, rim);
  const camera = new THREE.PerspectiveCamera(20, 1, 0.1, 10);
  camera.position.set(0, 0.03, 1.18);
  camera.lookAt(0, -0.01, 0);
  renderer.render(scene, camera);
  const url = renderer.domElement.toDataURL('image/png');

  model.traverse((o) => {
    if (o instanceof THREE.Mesh) {
      o.geometry.dispose();
      (o.material as THREE.Material).dispose();
    }
  });
  bust.group.traverse((o) => {
    if (o instanceof THREE.Mesh) {
      o.geometry.dispose();
      (o.material as THREE.Material).dispose();
    }
  });
  return url;
}

/** The speaker's voxel bust as an image source (rendered on first use, then cached). */
export function portraitSrc(speaker: SpeakerId): string {
  let url = cache.get(speaker);
  if (!url) {
    url = render(speaker);
    cache.set(speaker, url);
  }
  return url;
}
