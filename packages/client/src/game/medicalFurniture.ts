import * as THREE from 'three';
import type { DecorKind } from '@bastion/engine';

// Own low-poly furniture for the items not available as converted Synty
// models. Every object fits its single cover tile; parts are merged with
// the map and use its floor cutaway, fog, shadows and contact occlusion.
interface Part {
  geometry: THREE.BufferGeometry;
  matrix: THREE.Matrix4;
  color: THREE.Color;
}
const cache = new Map<DecorKind, Part[]>();
const METAL = 0x8b9c9c, WHITE = 0xdedfd5, RUBBER = 0x353d40, BLUE = 0x416b78;

export function medicalFurniture(kind: DecorKind): Part[] | null {
  if (kind !== 'medicalCabinet' && kind !== 'medicalCart' && kind !== 'wheelchair') return null;
  const ready = cache.get(kind);
  if (ready) return ready;
  const parts: Part[] = [];
  const add = (g: THREE.BufferGeometry, color: number, x: number, y: number, z: number, rz = 0) => {
    parts.push({ geometry: g, color: new THREE.Color(color), matrix: new THREE.Matrix4().makeTranslation(x, y, z).multiply(new THREE.Matrix4().makeRotationZ(rz)) });
  };
  const box = (c: number, x: number, y: number, z: number, w: number, h: number, d: number) => add(new THREE.BoxGeometry(w, h, d), c, x, y, z);
  const wheel = (x: number, y: number, z: number, radius: number) => {
    add(new THREE.CylinderGeometry(radius, radius, 0.05, 10), RUBBER, x, y, z, Math.PI / 2);
    add(new THREE.CylinderGeometry(radius * 0.68, radius * 0.68, 0.055, 10), METAL, x, y, z, Math.PI / 2);
  };
  if (kind === 'medicalCabinet') {
    box(METAL, 0, 0.09, 0, 0.65, 0.14, 0.42);
    box(WHITE, 0, 0.72, 0, 0.66, 1.18, 0.44);
    for (const x of [-0.16, 0.16]) {
      box(0xb5c8c5, x, 0.91, -0.229, 0.27, 0.65, 0.018);
      box(METAL, x + (x < 0 ? 0.09 : -0.09), 0.85, -0.25, 0.018, 0.15, 0.025);
      for (const y of [0.69, 0.96, 1.18]) box(WHITE, x, y, -0.245, 0.27, 0.02, 0.02);
      box(0xc7cac1, x, 0.35, -0.233, 0.29, 0.29, 0.025);
    }
    box(0x91453c, 0, 1.23, -0.245, 0.13, 0.045, 0.02);
    box(0x91453c, 0, 1.23, -0.25, 0.045, 0.13, 0.02);
  } else if (kind === 'medicalCart') {
    for (const x of [-0.25, 0.25]) for (const z of [-0.18, 0.18]) {
      wheel(x, 0.07, z, 0.065);
      box(METAL, x, 0.36, z, 0.025, 0.56, 0.025);
    }
    for (const y of [0.20, 0.56]) {
      box(WHITE, 0, y, 0, 0.57, 0.035, 0.42);
      box(METAL, 0, y + 0.035, 0.20, 0.57, 0.055, 0.02);
    }
    box(WHITE, 0, 0.43, 0, 0.48, 0.18, 0.34);
    box(METAL, 0, 0.43, -0.18, 0.15, 0.025, 0.02);
    box(BLUE, -0.13, 0.24, 0, 0.18, 0.06, 0.26);
    for (const x of [0.12, 0.20]) {
      add(new THREE.CylinderGeometry(0.025, 0.025, 0.10, 6), 0xe1d5b6, x, 0.63, 0.10);
      add(new THREE.CylinderGeometry(0.027, 0.027, 0.025, 6), BLUE, x, 0.69, 0.10);
    }
    box(METAL, 0.31, 0.64, 0, 0.025, 0.025, 0.40);
  } else {
    for (const x of [-0.28, 0.28]) {
      wheel(x, 0.26, 0.12, 0.25);
      wheel(x, 0.08, -0.30, 0.075);
      box(METAL, x, 0.28, -0.09, 0.025, 0.025, 0.48);
      box(METAL, x, 0.46, 0.20, 0.025, 0.63, 0.025);
      box(RUBBER, x, 0.57, -0.03, 0.05, 0.035, 0.32);
      box(METAL, x * 0.7, 0.19, -0.35, 0.15, 0.03, 0.15);
    }
    box(BLUE, 0, 0.36, 0, 0.45, 0.055, 0.42);
    box(BLUE, 0, 0.56, 0.20, 0.45, 0.40, 0.055);
    box(METAL, 0, 0.27, 0.15, 0.56, 0.025, 0.025);
  }
  cache.set(kind, parts);
  return parts;
}
