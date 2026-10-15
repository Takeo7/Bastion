import * as THREE from 'three';
import type { MedicalDetail } from '@bastion/engine';

type Part = { geometry: THREE.BufferGeometry; matrix: THREE.Matrix4; color: THREE.Color };
const KINDS = new Set(['bloodPool', 'bloodDrops', 'wallBlood', 'floorGrime', 'scatteredPapers', 'medicalWaste', 'overturnedChair', 'fallenIV', 'spilledSupplies', 'rumpledLinen']);
const cache = new Map<string, Part[]>();
const METAL = 0x7e8a85, WHITE = 0xcac6b3, DARK = 0x383e38;

/** Own, cached scenery. Flat stains receive lighting but cast no shadows. */
export function medicalDebris(kind: MedicalDetail['kind'], variant: number): Part[] | null {
  if (!KINDS.has(kind)) return null;
  const key = `${kind}:${variant & 7}`;
  const ready = cache.get(key);
  if (ready) return ready;
  const parts: Part[] = [];
  const rand = (n: number) => { const t = Math.sin((variant + 1) * 127.1 + n * 311.7) * 43758.5453; return t - Math.floor(t); };
  const add = (geometry: THREE.BufferGeometry, color: number, x: number, y: number, z: number, rx = 0, ry = 0, rz = 0) =>
    parts.push({ geometry, color: new THREE.Color(color), matrix: new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromEuler(new THREE.Euler(rx, ry, rz)), new THREE.Vector3(1, 1, 1)) });
  const box = (c: number, x: number, y: number, z: number, w: number, h: number, d: number, ry = 0) => add(new THREE.BoxGeometry(w, h, d), c, x, y, z, 0, ry);
  const stain = (rx: number, rz: number, salt = 0) => {
    const vertices = [0, 0, 0], indices: number[] = [], n = 15;
    for (let k = 0; k < n; k++) { const a = k / n * Math.PI * 2, r = 0.68 + rand(k + salt) * 0.32; vertices.push(Math.cos(a) * rx * r, 0, Math.sin(a) * rz * r); }
    for (let k = 0; k < n; k++) indices.push(0, (k + 1) % n + 1, k + 1);
    const g = new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
    g.setIndex(indices); g.computeVertexNormals(); return g;
  };
  const drop = (c: number, x: number, z: number, rx: number, rz: number, salt: number, y = 0.014) => add(stain(rx, rz, salt), c, x, y, z);
  if (kind === 'bloodPool') {
    drop(0x604035, 0.04, 0.04, 0.38, 0.29, 3);
    drop(0x651c20, 0.06, 0.03, 0.32, 0.235, 3, 0.016);
    drop(0x401719, 0.06, 0.02, 0.22, 0.16, 3, 0.018);
    for (let n = 0; n < 8; n++) drop(n % 2 ? 0x72272a : 0x58302b, (rand(n + 31) - 0.5) * 0.80, (rand(n + 50) - 0.5) * 0.82, 0.012 + rand(n + 70) * 0.035, 0.01 + rand(n + 90) * 0.025, n);
  } else if (kind === 'bloodDrops') {
    for (let n = 0; n < 4; n++) {
      const x = n % 2 ? 0.10 : -0.10, z = -0.32 + n * 0.20;
      drop(0x68312c, x, z, 0.05, 0.085, n);
      drop(0x602522, x, z + 0.11, 0.037, 0.035, n + 9);
    }
    for (let n = 0; n < 6; n++) drop(0x752a2b, (rand(n + 5) - 0.5) * 0.7, (rand(n + 15) - 0.5) * 0.7, 0.015, 0.019, n + 22);
  } else if (kind === 'wallBlood') {
    add(stain(0.28, 0.20, 5), 0x68252a, -0.04, 0.96, 0.785, -Math.PI / 2);
    add(stain(0.16, 0.12, 5), 0x4f1c20, -0.02, 0.96, 0.780, -Math.PI / 2);
    for (let n = 0; n < 9; n++) {
      const x = (rand(n + 11) - 0.5) * 0.72, y = 0.64 + rand(n + 32) * 0.72;
      add(stain(0.014 + rand(n + 55) * 0.025, 0.02, n), 0x742c2d, x, y, 0.778, -Math.PI / 2);
    }
    for (let n = 0; n < 4; n++) {
      const h = 0.10 + rand(n + 99) * 0.31;
      box(0x69302b, -0.20 + n * 0.10, 0.78 - h / 2, 0.778, 0.014, h, 0.003);
    }
  } else if (kind === 'floorGrime') {
    drop(0x827b61, 0.02, -0.04, 0.44, 0.28, 7, 0.006);
    drop(0x5d5c48, -0.02, -0.04, 0.30, 0.14, 7, 0.008);
    for (let n = 0; n < 4; n++) drop(0x686954, (rand(n + 33) - 0.5) * 0.20, -0.15 + n * 0.09, 0.28, 0.018, n + 34, 0.010);
    for (let n = 0; n < 8; n++) drop(0x77735e, (rand(n + 3) - 0.5) * 0.8, (rand(n + 13) - 0.5) * 0.8, 0.025, 0.035, n, 0.010);
  } else if (kind === 'scatteredPapers') {
    for (let n = 0; n < 6; n++) {
      const x = (rand(n + 3) - 0.5) * 0.65, z = (rand(n + 13) - 0.5) * 0.64, yaw = rand(n + 42) * 2;
      const at = new THREE.Matrix4().makeTranslation(x, 0.018 + n * 0.002, z).multiply(new THREE.Matrix4().makeRotationY(yaw));
      const start = parts.length;
      box(n === 0 ? 0x8b684e : WHITE, 0, 0, 0, 0.16, 0.004, 0.22);
      for (let line = 0; line < 4; line++) box(0x8c9080, 0, 0.003, -0.05 + line * 0.028, 0.105, 0.002, 0.005);
      box(0x7d5049, -0.04, 0.004, -0.08, 0.05, 0.002, 0.015);
      for (const p of parts.slice(start)) p.matrix.premultiply(at);
    }
  } else if (kind === 'spilledSupplies' || kind === 'medicalWaste') {
    box(0xa59876, -0.12, 0.07, 0.03, 0.24, 0.12, 0.22, 0.3);
    box(DARK, -0.12, 0.134, 0.03, 0.19, 0.004, 0.17, 0.3);
    box(WHITE, 0.13, 0.025, 0.21, 0.23, 0.02, 0.20, -0.4);
    box(0x885a4e, 0.12, 0.038, 0.20, 0.08, 0.006, 0.05, -0.4);
    for (let n = 0; n < 4; n++) {
      const x = (rand(n + 11) - 0.5) * 0.70, z = -0.11 - rand(n + 21) * 0.21;
      if (n % 2) {
        add(new THREE.CylinderGeometry(0.026, 0.026, 0.11, 7), n === 1 ? 0x9d7846 : 0xaab6a5, x, 0.035, z, Math.PI / 2, rand(n) * 2);
        box(0x597778, x + 0.05, 0.025, z, 0.03, 0.025, 0.035);
      } else add(new THREE.TorusGeometry(0.043, 0.017, 5, 9).rotateX(Math.PI / 2), WHITE, x, 0.024, z);
    }
    if (kind === 'medicalWaste') {
      add(new THREE.IcosahedronGeometry(0.12, 0).scale(1.3, 0.6, 1), 0x777f68, 0.20, 0.08, -0.02);
      box(0x969a83, 0.20, 0.16, -0.02, 0.035, 0.07, 0.035);
    }
  } else if (kind === 'rumpledLinen') {
    for (let n = 0; n < 5; n++) add(new THREE.IcosahedronGeometry(0.16, 0).scale(1.4, 0.25, 0.85), n % 2 ? 0xabae9c : 0xc6c5b1, -0.20 + n * 0.09, 0.04 + (n % 2) * 0.02, (rand(n) - 0.5) * 0.15, 0, n * 0.4);
    drop(0x7e5148, 0.06, 0.02, 0.08, 0.04, 3, 0.088);
  } else if (kind === 'overturnedChair') {
    for (const x of [-0.17, 0.17]) for (const z of [-0.15, 0.15]) box(METAL, x, 0.17, z, 0.025, 0.32, 0.025);
    box(0x526c68, 0, 0.34, 0, 0.41, 0.055, 0.38);
    box(0x526c68, 0, 0.54, 0.17, 0.41, 0.36, 0.045);
    const at = new THREE.Matrix4().makeTranslation(-0.30, 0.23, 0).multiply(new THREE.Matrix4().makeRotationZ(-Math.PI / 2));
    for (const p of parts) p.matrix.premultiply(at);
  } else if (kind === 'fallenIV') {
    add(new THREE.CylinderGeometry(0.011, 0.011, 1.02, 6), METAL, 0, 0.09, 0, 0, 0, Math.PI / 2);
    box(METAL, 0.40, 0.09, 0, 0.035, 0.035, 0.30);
    for (const z of [-0.15, 0.15]) box(DARK, 0.40, 0.06, z, 0.08, 0.06, 0.04);
    box(METAL, -0.44, 0.10, 0.04, 0.025, 0.025, 0.14);
    box(0xb5c6b8, -0.30, 0.055, 0.15, 0.19, 0.06, 0.11);
    box(WHITE, -0.30, 0.09, 0.15, 0.09, 0.005, 0.06);
    const tube = new THREE.CatmullRomCurve3([new THREE.Vector3(-0.20, 0.035, 0.15), new THREE.Vector3(-0.10, 0.03, 0.30), new THREE.Vector3(0.12, 0.025, 0.24)]);
    add(new THREE.TubeGeometry(tube, 8, 0.006, 4, false), 0xbac1af, 0, 0, 0);
    for (const p of parts) p.matrix.premultiply(new THREE.Matrix4().makeRotationY(Math.PI / 4));
  }
  cache.set(key, parts);
  return parts;
}
