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
  if (!['medicalCabinet', 'medicalCart', 'wheelchair', 'receptionDesk', 'waitingSeats', 'examTable', 'medicalMonitor', 'xray'].includes(kind)) return null;
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
  } else if (kind === 'receptionDesk') {
    box(WHITE, 0, 0.37, 0.08, 0.85, 0.72, 0.35);
    box(METAL, 0, 0.75, 0, 0.92, 0.055, 0.65);
    box(BLUE, 0, 0.53, -0.105, 0.80, 0.09, 0.025);
    box(METAL, 0.17, 0.84, 0.08, 0.045, 0.16, 0.04);
    box(RUBBER, 0.17, 0.96, 0.08, 0.29, 0.21, 0.045);
    box(0x75a3a5, 0.17, 0.96, 0.053, 0.245, 0.165, 0.012);
    box(0xf5f0dd, -0.22, 0.79, -0.05, 0.19, 0.025, 0.26);
    box(BLUE, -0.23, 0.40, 0.40, 0.30, 0.06, 0.24);
  } else if (kind === 'waitingSeats') {
    box(METAL, 0, 0.22, 0, 0.83, 0.04, 0.06);
    for (const x of [-0.30, 0, 0.30]) {
      box(BLUE, x, 0.31, 0, 0.27, 0.06, 0.30);
      box(BLUE, x, 0.49, 0.14, 0.27, 0.32, 0.04);
    }
    for (const x of [-0.32, 0.32]) {
      box(METAL, x, 0.13, 0.05, 0.04, 0.23, 0.04);
      box(METAL, x, 0.03, 0.05, 0.13, 0.04, 0.30);
    }
  } else if (kind === 'examTable') {
    for (const x of [-0.21, 0.21]) for (const z of [-0.30, 0.30]) box(METAL, x, 0.28, z, 0.045, 0.50, 0.045);
    box(WHITE, 0, 0.42, 0, 0.51, 0.20, 0.77);
    box(BLUE, 0, 0.56, -0.13, 0.55, 0.09, 0.54);
    box(BLUE, 0, 0.64, 0.27, 0.55, 0.12, 0.25);
    box(0xf3eedf, 0, 0.71, 0.27, 0.45, 0.025, 0.19);
    box(METAL, 0.32, 0.25, -0.23, 0.16, 0.03, 0.17);
  } else if (kind === 'medicalMonitor') {
    for (const x of [-0.19, 0.19]) for (const z of [-0.16, 0.16]) wheel(x, 0.055, z, 0.05);
    box(METAL, 0, 0.40, 0, 0.05, 0.73, 0.05);
    box(WHITE, 0, 0.62, 0, 0.50, 0.09, 0.35);
    box(WHITE, 0, 0.84, 0.04, 0.43, 0.34, 0.16);
    box(RUBBER, 0, 0.85, -0.048, 0.35, 0.25, 0.016);
    for (let n = 0; n < 8; n++) box(0x69ae91, -0.14 + n * 0.04, 0.85 + (n % 3 === 0 ? 0.045 : -0.015), -0.061, 0.045, 0.012, 0.01);
    box(BLUE, 0.18, 0.63, -0.08, 0.05, 0.02, 0.04);
  } else if (kind === 'xray') {
    box(METAL, 0, 0.055, 0.12, 0.68, 0.09, 0.55);
    box(WHITE, 0, 0.77, 0.28, 0.12, 1.40, 0.12);
    box(WHITE, 0, 0.85, 0.15, 0.62, 0.78, 0.14);
    box(0x65787b, 0, 0.85, 0.067, 0.49, 0.62, 0.015);
    box(0xb0c3c0, 0, 0.84, 0.052, 0.04, 0.35, 0.012);
    for (const y of [0.74, 0.83, 0.92]) box(0xb0c3c0, 0, y, 0.05, 0.25, 0.018, 0.015);
    box(METAL, 0, 1.44, 0.03, 0.08, 0.06, 0.52);
    box(WHITE, 0, 1.29, -0.21, 0.25, 0.25, 0.24);
    box(RUBBER, 0, 1.14, -0.21, 0.13, 0.06, 0.13);
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
