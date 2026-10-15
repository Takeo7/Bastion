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
  if (!['medicalCabinet', 'medicalCart', 'wheelchair', 'receptionDesk', 'waitingSeats', 'examTable', 'medicalMonitor', 'xray', 'bedsideTable', 'visitorChair', 'clinicalSink', 'sterilizer', 'ultrasound', 'waitingTable', 'waterDispenser', 'toilet', 'clinicalBin', 'consultDesk'].includes(kind)) return null;
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
    for (const y of [0.72, 0.99]) for (let n = 0; n < 5; n++) {
      const x = -0.25 + n * 0.12;
      box(n % 2 ? 0xb08c50 : 0xe1e8dc, x, y, -0.26, 0.055, 0.11, 0.025);
      box(BLUE, x, y + 0.06, -0.26, 0.055, 0.025, 0.03);
    }
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
    box(METAL, -0.10, 0.59, -0.08, 0.22, 0.025, 0.17);
    for (const x of [-0.15, -0.10]) box(RUBBER, x, 0.61, -0.08, 0.018, 0.012, 0.13);
  } else if (kind === 'receptionDesk' || kind === 'consultDesk') {
    box(WHITE, 0, kind === 'consultDesk' ? 0.28 : 0.37, 0.08, 0.85, kind === 'consultDesk' ? 0.50 : 0.72, 0.35);
    box(METAL, 0, 0.75, 0, 0.92, 0.055, 0.65);
    box(BLUE, 0, 0.53, -0.105, 0.80, 0.09, 0.025);
    box(METAL, 0.17, 0.84, 0.08, 0.045, 0.16, 0.04);
    box(RUBBER, 0.17, 0.96, 0.08, 0.29, 0.21, 0.045);
    box(0x75a3a5, 0.17, 0.96, 0.053, 0.245, 0.165, 0.012);
    box(0xf5f0dd, -0.22, 0.79, -0.05, 0.19, 0.025, 0.26);
    box(BLUE, -0.23, 0.40, 0.40, 0.30, 0.06, 0.24);
    box(RUBBER, -0.23, 0.56, 0.48, 0.30, 0.29, 0.035);
    box(RUBBER, -0.12, 0.79, 0.12, 0.13, 0.025, 0.08);
    if (kind === 'consultDesk') {
      box(BLUE, 0.19, 0.33, -0.40, 0.28, 0.05, 0.26);
      box(BLUE, 0.19, 0.49, -0.49, 0.28, 0.28, 0.035);
    }
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
    add(new THREE.CylinderGeometry(0.055, 0.055, 0.48, 10).rotateZ(Math.PI / 2), WHITE, 0, 0.48, -0.41);
    box(0xf3eedf, 0, 0.615, -0.25, 0.45, 0.012, 0.27);
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
  } else if (kind === 'bedsideTable') {
    box(WHITE, 0, 0.28, 0, 0.39, 0.48, 0.39);
    box(METAL, 0, 0.54, 0, 0.42, 0.035, 0.42);
    for (const y of [0.22, 0.40]) {
      box(0xb8c6c2, 0, y, -0.205, 0.34, 0.15, 0.025);
      box(METAL, 0, y, -0.23, 0.10, 0.02, 0.03);
    }
    add(new THREE.CylinderGeometry(0.035, 0.03, 0.09, 8), 0xc1d5d8, -0.10, 0.60, 0.05);
    box(BLUE, 0.10, 0.58, -0.06, 0.11, 0.035, 0.14);
  } else if (kind === 'visitorChair') {
    for (const x of [-0.18, 0.18]) for (const z of [-0.16, 0.16]) box(METAL, x, 0.17, z, 0.03, 0.32, 0.03);
    box(BLUE, 0, 0.34, 0, 0.42, 0.065, 0.39);
    box(BLUE, 0, 0.55, 0.18, 0.42, 0.37, 0.05);
    for (const x of [-0.22, 0.22]) box(RUBBER, x, 0.49, 0, 0.045, 0.04, 0.37);
  } else if (kind === 'clinicalSink') {
    box(WHITE, 0, 0.35, 0.08, 0.64, 0.62, 0.43);
    box(WHITE, 0, 0.69, 0.02, 0.70, 0.08, 0.53);
    box(0x728a8a, 0, 0.735, -0.02, 0.44, 0.012, 0.30);
    box(0xc8dbd8, 0, 0.74, -0.02, 0.34, 0.014, 0.22);
    box(METAL, 0, 0.82, 0.21, 0.025, 0.22, 0.025);
    box(METAL, 0, 0.92, 0.15, 0.025, 0.025, 0.15);
    box(WHITE, -0.22, 1.08, 0.28, 0.20, 0.25, 0.10);
    box(BLUE, -0.22, 1.01, 0.215, 0.08, 0.06, 0.035);
    box(0xf0ecda, 0.21, 0.80, 0.11, 0.08, 0.16, 0.07);
    box(METAL, 0.21, 0.89, 0.11, 0.08, 0.025, 0.05);
  } else if (kind === 'sterilizer') {
    box(WHITE, 0, 0.31, 0.04, 0.78, 0.58, 0.53);
    box(METAL, 0, 0.63, 0, 0.85, 0.05, 0.59);
    box(0xb9c5c2, -0.16, 0.83, 0.08, 0.41, 0.35, 0.37);
    box(RUBBER, -0.16, 0.83, -0.113, 0.29, 0.23, 0.02);
    box(0x668e8c, -0.16, 0.83, -0.126, 0.22, 0.15, 0.01);
    for (const x of [-0.27, -0.08]) box(METAL, x, 0.99, -0.12, 0.05, 0.025, 0.03);
    box(METAL, 0.24, 0.68, -0.05, 0.21, 0.03, 0.25);
    for (const x of [0.18, 0.24]) box(RUBBER, x, 0.70, -0.05, 0.015, 0.015, 0.17);
  } else if (kind === 'ultrasound') {
    for (const x of [-0.22, 0.22]) for (const z of [-0.19, 0.19]) wheel(x, 0.07, z, 0.06);
    box(WHITE, 0, 0.37, 0.05, 0.43, 0.53, 0.37);
    box(WHITE, 0, 0.69, 0, 0.60, 0.13, 0.48);
    box(METAL, 0, 0.88, 0.12, 0.06, 0.35, 0.06);
    box(WHITE, 0, 1.04, 0.07, 0.48, 0.32, 0.10);
    box(RUBBER, 0, 1.04, 0.012, 0.40, 0.25, 0.015);
    box(0x9aac9d, 0, 1.03, 0, 0.23, 0.16, 0.01);
    box(0xc7d9d5, 0, 0.77, -0.13, 0.34, 0.025, 0.15);
    const cable = new THREE.CatmullRomCurve3([new THREE.Vector3(0.24, 0.70, 0.06), new THREE.Vector3(0.37, 0.29, 0), new THREE.Vector3(0.31, 0.75, -0.13)]);
    add(new THREE.TubeGeometry(cable, 10, 0.009, 4, false), RUBBER, 0, 0, 0);
    box(WHITE, 0.31, 0.80, -0.13, 0.06, 0.13, 0.045);
  } else if (kind === 'waitingTable') {
    for (const x of [-0.20, 0.20]) for (const z of [-0.15, 0.15]) box(METAL, x, 0.18, z, 0.025, 0.32, 0.025);
    box(WHITE, 0, 0.36, 0, 0.53, 0.055, 0.42);
    for (let n = 0; n < 3; n++) box([BLUE, 0xa16c4d, 0xc2bb77][n]!, -0.15 + n * 0.14, 0.40, -0.02, 0.11, 0.015, 0.20);
    box(WHITE, 0.15, 0.43, 0.10, 0.12, 0.10, 0.10);
  } else if (kind === 'waterDispenser') {
    box(WHITE, 0, 0.47, 0, 0.36, 0.84, 0.36);
    box(RUBBER, 0, 0.62, -0.19, 0.25, 0.23, 0.025);
    box(METAL, 0, 0.52, -0.24, 0.23, 0.025, 0.14);
    for (const x of [-0.07, 0.07]) box(x < 0 ? BLUE : 0x98443b, x, 0.69, -0.23, 0.05, 0.045, 0.07);
    add(new THREE.CylinderGeometry(0.15, 0.12, 0.29, 10), 0x719fac, 0, 1.05, 0);
    box(BLUE, 0, 0.89, 0, 0.13, 0.05, 0.13);
  } else if (kind === 'toilet') {
    box(WHITE, 0, 0.24, 0, 0.28, 0.39, 0.36);
    add(new THREE.CylinderGeometry(0.21, 0.14, 0.12, 12), WHITE, 0, 0.43, -0.05);
    add(new THREE.TorusGeometry(0.15, 0.035, 6, 12).rotateX(Math.PI / 2), 0x9dada8, 0, 0.50, -0.05);
    box(WHITE, 0, 0.52, 0.22, 0.40, 0.43, 0.18);
    box(METAL, 0.10, 0.75, 0.22, 0.06, 0.015, 0.04);
  } else if (kind === 'clinicalBin') {
    box(0xcbb146, 0, 0.24, 0, 0.27, 0.42, 0.28);
    box(RUBBER, 0, 0.47, 0, 0.31, 0.045, 0.32);
    box(METAL, 0, 0.06, -0.18, 0.11, 0.025, 0.12);
    box(RUBBER, 0, 0.28, -0.145, 0.13, 0.025, 0.012);
    box(RUBBER, 0, 0.28, -0.146, 0.025, 0.13, 0.012);
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
