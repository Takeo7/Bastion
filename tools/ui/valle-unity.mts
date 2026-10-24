// The valley map's data for Unity (docs/UNITY-ESTRATEGIA.md, step 7), written from the 0.10.1's own code so that the
// Unity map is the same to the pixel: the Marea's field (geoscape.ts mareaField: valleyGeography.ts mareaReach over
// 240×160 cells, as half floats) and the geography (the zones, the roads traced along the painting, the landmarks,
// Atalaya's works, the Bastion), in the map's units of the 0.10.1 (x east, z south).
//
//   cd packages/client && npx tsx ../../tools/ui/valle-unity.mts <out folder>
//
// Writes marea.bytes (little-endian half floats, row by row from the north), valley.json; and copies the painting.
import { copyFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import * as THREE from 'three';
import { DOOM_MAX, LANDMARKS, MAREA_SOURCE, REGIONS, ROADS, type RegionId } from '../../packages/engine/src/index.ts';
import { ATALAYA_WORKS, BASTION, D, FIELD, HD, HW, mareaReach, mareaRoads, PX, roadLine, threshold, VALLEY_LANDMARKS, W, ZONES } from '../../packages/client/src/strategy/valleyGeography.ts';

const out = process.argv[2];
if (!out) throw new Error('valle-unity.mts <out folder>');
mkdirSync(out, { recursive: true });

const roads = mareaRoads();
const data = new Uint16Array(FIELD.w * FIELD.d);
for (let j = 0; j < FIELD.d; j++)
  for (let i = 0; i < FIELD.w; i++) {
    const v = mareaReach(-HW + ((i + 0.5) / FIELD.w) * W, -HD + ((j + 0.5) / FIELD.d) * D, roads);
    data[j * FIELD.w + i] = THREE.DataUtils.toHalfFloat(Math.min(v, 40));
  }
writeFileSync(join(out, 'marea.bytes'), Buffer.from(data.buffer));

const r2 = (n: number) => Math.round(n * 1000) / 1000;
const valley = {
  size: { w: r2(W), d: r2(D), px: PX },
  field: FIELD,
  doomMax: DOOM_MAX,
  mareaSource: MAREA_SOURCE,
  atalayaWorks: { x: r2(ATALAYA_WORKS.x), z: r2(ATALAYA_WORKS.z), rx: ATALAYA_WORKS.rx, rz: ATALAYA_WORKS.rz },
  bastion: { x: r2(BASTION.x), z: r2(BASTION.z) },
  zones: (Object.keys(ZONES) as RegionId[]).map((id) => ({ id, name: REGIONS[id].name, x: r2(ZONES[id].x), z: r2(ZONES[id].z), threshold: threshold(id), roads: REGIONS[id].roads })),
  roads: ROADS.map(([a, b]) => ({ a, b, points: roadLine(a, b).flatMap((p) => [r2(p.x), r2(p.z)]) })),
  landmarks: VALLEY_LANDMARKS.map((l) => ({ key: l.key, name: LANDMARKS[l.key], x: r2(l.at[0]), z: r2(l.at[1]) })),
};
writeFileSync(join(out, 'valley.json'), JSON.stringify(valley));
copyFileSync(new URL('../../packages/client/public/maps/valle/mapa.jpg', import.meta.url), join(out, 'mapa.jpg'));
console.log('ok', FIELD.w, '×', FIELD.d, 'field;', valley.zones.length, 'zones,', valley.roads.length, 'roads,', valley.landmarks.length, 'landmarks');
