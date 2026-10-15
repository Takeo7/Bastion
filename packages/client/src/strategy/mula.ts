import * as THREE from 'three';
import { StaticBatch } from '../game/batch';
import type { BaseModelId } from '../game/syntyManifest';
import { KitBatch, kitMesh, kitModel } from './baseKit';

// La Mula (LORE: an armoured city bus): POLYGON Town's bus with steel plates
// bolted over its windows, slits to see through, plates on the roof. Its
// doors and wheels are parts of their own, so they move. The same bus waits
// in the base's hangar (bastion.ts) and at a mission's evacuation
// (game/evacBus.ts).

/** What a scene has to have loaded (baseKit.ts loadKitModels) to build La Mula. */
export const MULA_MODELS: BaseModelId[] = ['mulaBus', 'plate', 'plateSlit', 'plateRoof'];

const UNIT = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);

export interface Mula {
  /** The bus in its model's space: its nose +z, its doors on its −x side, true size, standing on y = 0. */
  body: THREE.Group;
  /** Each door leaf, and where it is shut and open (in the body's space). */
  doors: { leaf: THREE.Object3D; shut: THREE.Vector3; open: THREE.Vector3 }[];
  /** Each wheel's hub, turned about its own axle (x). */
  wheels: THREE.Object3D[];
  wheelRadius: number;
  /** The headlights' material: a glow of 0.2 while it stands. */
  lights: THREE.MeshStandardMaterial;
  /** Each doorway's middle, in the body's space. */
  doorCentres: THREE.Vector3[];
  size: THREE.Vector3;
}

/** La Mula, built from the kit (null if its models haven't loaded). */
export function buildMula(): Mula | null {
  const model = kitModel('mulaBus');
  if (!model) return null;
  const body = new THREE.Group();
  const doors: Mula['doors'] = [];
  const wheels: THREE.Object3D[] = [];
  let wheelRadius = 0.35;
  const doorCentres: THREE.Vector3[] = [];
  // The body, its glass and its plates stand still on it: merged, they move with it.
  const statics = new StaticBatch(50);
  statics.begin(null);
  const still = new THREE.Matrix4();
  for (const part of kitMesh('mulaBus', 'plain').children.slice()) {
    const mesh = part as THREE.Mesh;
    mesh.geometry.computeBoundingBox();
    const bb = mesh.geometry.boundingBox!;
    const centre = bb.getCenter(new THREE.Vector3());
    if (/wheel/i.test(mesh.name)) {
      // Turned about its own axle.
      const hub = new THREE.Group();
      hub.position.copy(centre);
      const spun = mesh.clone();
      spun.geometry = mesh.geometry.clone().translate(-centre.x, -centre.y, -centre.z);
      hub.add(spun);
      body.add(hub);
      wheels.push(hub);
      wheelRadius = (bb.max.y - bb.min.y) / 2;
    } else if (/door/i.test(mesh.name)) {
      const leaf = new THREE.Group();
      leaf.add(mesh);
      body.add(leaf);
      const width = bb.max.z - bb.min.z;
      // A leaf folds inside, along the bus away from its doorway's middle.
      const along = Number(/Door_0(\d)/.exec(mesh.name)?.[1] ?? 1) % 2 ? 1 : -1;
      doors.push({ leaf, shut: new THREE.Vector3(), open: new THREE.Vector3(0.14, 0, along * width * 0.85) });
      if (!/glass/i.test(mesh.name)) doorCentres.push(centre);
    } else statics.add(mesh.geometry, mesh.material as THREE.Material, still, true, undefined, false);
  }
  // (A leaf's glass is a mesh of its own: its name gives it the same offset, so both move alike.)
  // Steel plates over the windows, both sides, leaving the doorways open.
  const glass = model.parts.find((p) => /^SM_Veh_Bus_Glass/i.test(p.name));
  const band = new THREE.Box3();
  glass?.geometry.computeBoundingBox();
  if (glass?.geometry.boundingBox) band.copy(glass.geometry.boundingBox);
  else band.set(new THREE.Vector3(-1.1, 1.0, -3.6), new THREE.Vector3(1.1, 1.8, 3.4));
  const plates = new KitBatch();
  const windowY = (band.min.y + band.max.y) / 2 + 0.05;
  const plateH = (kitModel('plateSlit')?.size.y ?? 0.45) * 1.15;
  const sy = Math.max(1, (band.max.y - band.min.y) / plateH);
  const halfW = model.size.x / 2 + 0.02;
  const inDoorway = (z: number) => doorCentres.some((c) => Math.abs(c.z - z) < 0.75);
  for (let z = band.min.z + 0.35; z < band.max.z - 0.2; z += 0.68) {
    for (const side of [-1, 1]) {
      if (side < 0 && inDoorway(z)) continue;
      plates.put(Math.round(z * 3) % 3 ? 'plate' : 'plateSlit', { x: side * halfW, y: windowY - 0.25 * sy, z, s: 1.15, sy, finish: 'plain' });
    }
  }
  // The windscreen half covered, a slit to drive by; plates on the roof.
  const nose = model.size.z / 2 + 0.03;
  for (const x of [-0.55, 0.55]) plates.put('plateSlit', { x, y: windowY - 0.35, z: nose - 0.15, yaw: Math.PI / 2, s: 1.3, finish: 'plain' });
  for (let z = -2.6; z <= 2.6; z += 1.3) plates.put('plateRoof', { x: (Math.round(z) % 2) * 0.2, y: model.size.y + 0.01, z, yaw: 0.05 * z, s: 1.1 });
  plates.addTo(statics);
  statics.commit();
  body.add(statics.group);
  // Headlights, off while it stands.
  const lights = new THREE.MeshStandardMaterial({ color: 0x2a2a20, emissive: 0xfff2c0, emissiveIntensity: 0.2 });
  for (const x of [-0.75, 0.75]) {
    const lamp = new THREE.Mesh(UNIT, lights);
    lamp.scale.set(0.28, 0.14, 0.05);
    lamp.position.set(x, 0.55, nose + 0.01);
    body.add(lamp);
  }
  for (const d of doors) d.shut.copy(d.leaf.position);
  return { body, doors, wheels, wheelRadius, lights, doorCentres, size: model.size.clone() };
}
