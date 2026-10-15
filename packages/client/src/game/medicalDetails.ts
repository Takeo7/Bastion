import * as THREE from 'three';
import type { MedicalDetail } from '@bastion/engine';
import type { Cutaway } from './buildings';
import { extendShader } from './fogOfWar';

/** Called after floorFadePatch: wall fixtures fade with the wall supporting them. */
export function medicalWallPatch(material: THREE.Material, cut: Cutaway): void {
  extendShader(material, 'medical-wall-fixture', (shader) => {
    Object.assign(shader.uniforms, cut.uniforms);
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nattribute vec2 aMedicalWall;\nvarying float vMedicalFixture;')
      .replace('#include <project_vertex>', `#include <project_vertex>
        vMedicalFixture = 0.0;
        vec4 fixtureState = cutState(aFloor.x);
        if (abs(aFloor.y - cutFloor(fixtureState)) < 0.5) {
          float facing = dot(aMedicalWall, aMedicalWall) < 0.01 ? 1.0 : smoothstep(-0.15, 0.25, -dot(aMedicalWall, cutView));
          vMedicalFixture = max(fixtureState.g * facing, fixtureState.b);
        }`);
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nvarying float vMedicalFixture;')
      .replace('#include <clipping_planes_fragment>', '#include <clipping_planes_fragment>\nif (vMedicalFixture > 0.25) discard;');
  });
}

// Own POLYGON-style scenery for models not present in the converted asset
// collection. Corpses themselves reuse Synty characters, baked once offline.
type Part = { geometry: THREE.BufferGeometry; matrix: THREE.Matrix4; color: THREE.Color };
const cache = new Map<string, Part[]>();
const METAL = 0x83918e, RUST = 0x746554, WHITE = 0xd6d4c6, DARK = 0x343a37;

export function medicalDetails(kind: MedicalDetail['kind']): Part[] {
  const ready = cache.get(kind);
  if (ready) return ready;
  const parts: Part[] = [];
  const add = (geometry: THREE.BufferGeometry, color: number, x: number, y: number, z: number, rz = 0) =>
    parts.push({ geometry, color: new THREE.Color(color), matrix: new THREE.Matrix4().makeTranslation(x, y, z).multiply(new THREE.Matrix4().makeRotationZ(rz)) });
  const box = (c: number, x: number, y: number, z: number, w: number, h: number, d: number) => add(new THREE.BoxGeometry(w, h, d), c, x, y, z);
  const sheet = (base: number) => {
    // A single faceted drape: raised at the head, shoulders, knees and feet,
    // falling over each side rather than a row of disconnected white boxes.
    const sections = [[-0.49, 0.09, 0.12], [-0.35, 0.18, 0.14], [-0.19, 0.17, 0.23], [0.07, 0.12, 0.21], [0.28, 0.14, 0.15], [0.48, 0.05, 0.12]];
    const vertices: number[] = [], index: number[] = [];
    for (const [z, h, w] of sections) for (const [x, y] of [[-0.29, -0.07], [-w!, h! * 0.45], [0, h], [w, h! * 0.45], [0.29, -0.07]]) vertices.push(x!, base + y!, z!);
    for (let j = 0; j < sections.length - 1; j++) for (let i = 0; i < 4; i++) {
      const a = j * 5 + i, b = a + 5; index.push(a, b, a + 1, a + 1, b, b + 1);
    }
    const g = new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
    g.setIndex(index); g.computeVertexNormals(); add(g.toNonIndexed(), WHITE, 0, 0, 0);
    // Faint old stains, kept small enough for the body silhouette to lead.
    box(0x775550, 0.02, base + 0.157, -0.20, 0.075, 0.006, 0.055);
  };
  if (kind === 'bareBed' || kind === 'coveredBed') {
    for (const x of [-0.25, 0.25]) for (const z of [-0.46, 0.46]) {
      box(RUST, x, 0.37, z, 0.035, 0.66, 0.035);
      add(new THREE.CylinderGeometry(0.055, 0.055, 0.04, 8), DARK, x, 0.06, z, Math.PI / 2);
    }
    for (const x of [-0.25, 0.25]) box(RUST, x, 0.39, 0, 0.035, 0.06, 0.96);
    for (let n = 0; n < 8; n++) box(METAL, 0, 0.39, -0.43 + n * 0.123, 0.48, 0.025, 0.045);
    for (const z of [-0.46, 0.46]) for (const y of [0.54, 0.66]) box(RUST, 0, y, z, 0.53, 0.03, 0.03);
    if (kind === 'coveredBed') { box(0x8c9b8d, 0, 0.43, 0, 0.48, 0.075, 0.94); sheet(0.48); }
  } else if (kind === 'coveredCorpse') sheet(0.075);
  else if (kind === 'privacyScreen') {
    // Folded along one side of a bed, entirely inside its existing tile.
    for (const z of [-0.42, 0.06, 0.38]) {
      box(METAL, 0.43, 0.61, z, 0.022, 1.16, 0.022);
      box(METAL, 0.40, 0.04, z, 0.15, 0.025, 0.08);
    }
    box(METAL, 0.43, 1.20, -0.02, 0.025, 0.025, 0.84);
    for (let n = 0; n < 12; n++) {
      const z = -0.39 + n * 0.065;
      box(n % 2 ? 0xc0c9bc : 0x9daea6, 0.43 + (n % 2) * 0.025, 0.75, z, 0.025, 0.79, 0.065);
      add(new THREE.TorusGeometry(0.023, 0.006, 4, 6).rotateY(Math.PI / 2), METAL, 0.43, 1.18, z);
    }
  } else if (kind === 'medicalSupplies') {
    for (const [x, z, c] of [[-0.14, 0.04, 0xa88c55], [0.02, 0.10, 0xe0e0d2], [0.15, 0.03, 0xb0b48c]]) {
      add(new THREE.CylinderGeometry(0.035, 0.035, 0.105, 7), c!, x!, 0.057, z!);
      box(0x576a68, x!, 0.116, z!, 0.055, 0.025, 0.055);
      box(WHITE, x!, 0.060, z! - 0.034, 0.04, 0.04, 0.008);
    }
    box(0xe1d9bf, -0.07, 0.02, -0.10, 0.16, 0.035, 0.10);
    box(0x98493f, -0.07, 0.04, -0.10, 0.07, 0.008, 0.022);
    for (const x of [0.08, 0.16]) add(new THREE.CylinderGeometry(0.032, 0.032, 0.06, 8).rotateX(Math.PI / 2), WHITE, x, 0.04, -0.10);
  } else if (kind === 'luggage') {
    box(0x655646, 0.30, 0.19, -0.14, 0.22, 0.34, 0.30);
    box(0x403a32, 0.30, 0.20, -0.30, 0.17, 0.24, 0.025);
    box(METAL, 0.30, 0.39, -0.14, 0.10, 0.07, 0.025);
    box(DARK, 0.30, 0.42, -0.14, 0.10, 0.025, 0.035);
    for (const z of [-0.24, -0.05]) box(DARK, 0.30, 0.025, z, 0.18, 0.035, 0.04);
  } else if (kind === 'crutches') {
    for (const x of [0.24, 0.35]) {
      for (const dx of [-0.025, 0.025]) add(new THREE.CylinderGeometry(0.009, 0.009, 0.83, 5), METAL, x + dx, 0.46, -0.29, -0.12);
      box(0x8c785a, x + 0.04, 0.88, -0.29, 0.14, 0.045, 0.055);
      box(DARK, x, 0.045, -0.29, 0.035, 0.06, 0.035);
      box(0x8c785a, x, 0.57, -0.29, 0.09, 0.035, 0.035);
    }
  } else if (kind === 'books') {
    for (let n = 0; n < 3; n++) {
      box([0x706e46, 0x83564d, 0x4c6a70][n]!, -0.10 + n * 0.04, 0.015 + n * 0.033, 0.04, 0.22, 0.025, 0.16);
      box(0xd8d0b7, -0.10 + n * 0.04, 0.017 + n * 0.033, 0.04, 0.20, 0.016, 0.14);
    }
  } else if (kind === 'wallCross') {
    box(0x69503d, 0, 1.40, 0.80, 0.045, 0.33, 0.035);
    box(0x69503d, 0, 1.47, 0.80, 0.20, 0.045, 0.035);
  } else if (kind === 'cautionSign' || kind === 'safeSign') {
    const safe = kind === 'safeSign';
    box(DARK, 0, 1.33, 0.80, 0.65, 0.36, 0.035);
    box(safe ? 0x4a7060 : 0xc6ad50, 0, 1.33, 0.776, 0.61, 0.32, 0.015);
    const glyphs: Record<string, string[]> = {
      A:['01110','10001','10001','11111','10001','10001','10001'],
      T:['11111','00100','00100','00100','00100','00100','00100'],
      E:['11111','10000','10000','11110','10000','10000','11111'],
      N:['10001','11001','11001','10101','10011','10011','10001'],
      C:['01111','10000','10000','10000','10000','10000','01111'],
      I:['111','010','010','010','010','010','111'],
      O:['01110','10001','10001','10001','10001','10001','01110'],
      Z:['11111','00001','00010','00100','01000','10000','11111'],
      S:['01111','10000','10000','01110','00001','00001','11110'],
      G:['01111','10000','10000','10111','10001','10001','01110'],
      U:['10001','10001','10001','10001','10001','10001','01110'],
      R:['11110','10001','10001','11110','10100','10010','10001'],
    };
    const text = (value: string, y: number, size: number) => {
      const total = [...value].reduce((n, c) => n + (glyphs[c]![0]!.length + 1) * size, -size);
      let left = -total / 2;
      for (const c of value) {
        glyphs[c]!.forEach((row, j) => [...row].forEach((v, i) => { if (v === '1') box(safe ? WHITE : DARK, -(left + i * size), y - j * size, 0.764, size * 0.9, size * 0.9, 0.008); }));
        left += (glyphs[c]![0]!.length + 1) * size;
      }
    };
    if (safe) { text('ZONA', 1.43, 0.016); text('SEGURA', 1.29, 0.014); }
    else { text('ATENCION', 1.35, 0.011); box(DARK, 0, 1.23, 0.76, 0.45, 0.018, 0.008); }
  }
  cache.set(kind, parts);
  return parts;
}
