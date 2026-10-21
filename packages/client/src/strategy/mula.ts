import * as THREE from 'three';
import { StaticBatch } from '../game/batch';
import type { BaseModelId } from '../game/syntyManifest';
import { KitBatch, kitMesh, kitModel } from './baseKit';

// La Mula (LORE: an armoured city bus): POLYGON Town's bus with steel plates
// bolted over its windows, slits to see through, plates on the roof. Its
// doors and wheels are parts of their own, so they move. The same bus waits
// in the base's hangar (bastion.ts) and at a mission's evacuation
// (game/evacBus.ts).
//
// Before it is the bus, La Lata is one of Apocalypse's vehicles with its own
// armour (playtest 18/10, #45: buildLata): a 4×4 with bars on its windows and
// spikes on its nose, a TV van with its dish still on the roof (its back doors
// swing open), an army lorry under its canvas.

/** What a scene has to have loaded (baseKit.ts loadKitModels) to build La Mula. */
export const MULA_MODELS: BaseModelId[] = ['mulaBus', 'plate', 'plateSlit', 'plateRoof'];

const UNIT = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);

export interface Mula {
  /** The bus in its model's space: its nose +z, its doors on its −x side, true size, standing on y = 0. */
  body: THREE.Group;
  /** Each door leaf, and where it is shut and open (in the body's space); one that swings on its hinge, how far (radians) when open. */
  doors: { leaf: THREE.Object3D; shut: THREE.Vector3; open: THREE.Vector3; swing?: number }[];
  /** Each wheel's hub, turned about its own axle (x). */
  wheels: THREE.Object3D[];
  wheelRadius: number;
  /** The headlights' material: a glow of 0.2 while it stands. */
  lights: THREE.MeshStandardMaterial;
  /** Each doorway's middle, in the body's space. */
  doorCentres: THREE.Vector3[];
  /**
   * Whether one gets in on its −x side only (the bus), so that it is mirrored to have its doors towards the squad
   * (evacBus.ts park); the rest (doors on both sides, or at the back) never are: a van's lettering read backwards.
   */
  oneSided: boolean;
  size: THREE.Vector3;
}

/**
 * The 4×4's doors (playtest 20/10 #7: get in, not vanish in the gap): Synty's body has them in its one shell, so they
 * are cut out of it, the glass, the bars and the armour with them: along the body (fractions of its length from the
 * back), front and rear, as its handles and mirrors place them; from the sill to the roof's edge (fractions of its
 * height); the outer share of its width either side. Each hangs on its front edge and swings out.
 */
const CAR_DOORS = [
  { from: 0.435, to: 0.587 },
  { from: 0.253, to: 0.435 },
];
const CAR_DOOR_LOW = 0.09;
const CAR_DOOR_HIGH = 0.845;
const CAR_DOOR_SIDE = 0.55;
const CAR_DOOR_SWING = 1.15;

/** La Lata's vehicles before the bus (GameState.vehicle 0 4×4, 1 van, 2 lorry): the body and its armour, and where one gets in. */
const LATA: Record<number, { body: BaseModelId; extras: BaseModelId[]; way: 'side' | 'back' }> = {
  0: { body: 'lataCar', extras: ['lataCarArmour', 'lataCarBars'], way: 'side' },
  1: { body: 'lataVan', extras: ['lataVanArmour', 'lataVanRoof', 'lataVanBars'], way: 'back' },
  2: { body: 'lataTruck', extras: ['lataTruckArmour', 'lataTruckCanopy', 'lataTruckBars'], way: 'back' },
};

/** What a scene has to have loaded to build La Lata as `vehicle` (3, or none: the bus). */
export function lataModels(vehicle = 3): BaseModelId[] {
  const v = LATA[vehicle];
  return v ? [v.body, ...v.extras] : MULA_MODELS;
}

/**
 * La Lata as `vehicle` (3, or none: La Mula), built from the kit, as La Mula is (its nose +z, its left −x, standing on
 * y = 0); null if its models haven't loaded. A van's back doors swing on their hinges (`swing`).
 */
export function buildLata(vehicle = 3): Mula | null {
  const v = LATA[vehicle];
  if (!v) return buildMula();
  const model = kitModel(v.body);
  if (!model) return null;
  const body = new THREE.Group();
  const statics = new StaticBatch(50);
  statics.begin(null);
  const still = new THREE.Matrix4();
  // Each model back where it was made to go round the body, turned nose +z (the kit has it +x).
  const placed = (id: BaseModelId, g: THREE.BufferGeometry) => {
    const shift = (kitModel(id)?.origin ?? model.origin).clone().sub(model.origin);
    const out = g.clone().translate(shift.x, shift.y, shift.z).rotateY(-Math.PI / 2);
    out.computeBoundingBox();
    return out;
  };
  const box = new THREE.Box3();
  const wheels: THREE.Object3D[] = [];
  let wheelRadius = 0.3;
  const leaves = new Map<string, THREE.Mesh[]>();
  // All of it first, for its size (the 4×4's doors are cut by it).
  const pieces: { id: BaseModelId; mesh: THREE.Mesh; geometry: THREE.BufferGeometry }[] = [];
  for (const id of [v.body, ...v.extras]) {
    for (const part of kitMesh(id, 'plain').children.slice()) {
      const mesh = part as THREE.Mesh;
      const geometry = placed(id, mesh.geometry);
      box.union(geometry.boundingBox!);
      pieces.push({ id, mesh, geometry });
    }
  }
  const cut = v.way === 'side' ? carDoors(box) : null;
  for (const { id, mesh, geometry: whole } of pieces) {
    let geometry = whole;
    {
      const bb = geometry.boundingBox!;
      if (id === v.body && /wheel/i.test(mesh.name)) {
        // Turned about its own axle.
        const centre = bb.getCenter(new THREE.Vector3());
        const hub = new THREE.Group();
        hub.position.copy(centre);
        hub.add(new THREE.Mesh(geometry.translate(-centre.x, -centre.y, -centre.z), mesh.material));
        body.add(hub);
        wheels.push(hub);
        wheelRadius = (bb.max.y - bb.min.y) / 2;
        continue;
      }
      const door = id === v.body ? /Door_([lr])/i.exec(mesh.name)?.[1]?.toLowerCase() : undefined;
      if (door) {
        const m = new THREE.Mesh(geometry, mesh.material);
        leaves.set(door, [...(leaves.get(door) ?? []), m]);
        continue;
      }
      if (cut && id === v.body) {
        // The 4×4: what of its own shell and glass lies in a door's box goes with that door.
        const split = cut.split(geometry);
        split.doors.forEach((g, k) => g && cut.leaves[k]!.add(Object.assign(new THREE.Mesh(g, mesh.material), { castShadow: true })));
        geometry = split.rest;
      }
      statics.add(geometry, mesh.material as THREE.Material, still, true, undefined, false);
    }
  }
  statics.commit();
  body.add(statics.group);
  // The back doors, each on its outer edge, opening out and round towards the side.
  const doors: Mula['doors'] = [];
  for (const meshes of leaves.values()) {
    const bb = new THREE.Box3();
    for (const m of meshes) bb.union(m.geometry.boundingBox!);
    const outer = Math.abs(bb.max.x) > Math.abs(bb.min.x) ? bb.max.x : bb.min.x;
    const hinge = new THREE.Vector3(outer, 0, (bb.min.z + bb.max.z) / 2);
    const leaf = new THREE.Group();
    leaf.position.copy(hinge);
    for (const m of meshes) {
      m.geometry.translate(-hinge.x, 0, -hinge.z);
      m.castShadow = true;
      leaf.add(m);
    }
    body.add(leaf);
    doors.push({ leaf, shut: hinge.clone(), open: hinge.clone(), swing: outer > 0 ? -1.97 : 1.97 });
  }
  if (cut) {
    for (const [k, leaf] of cut.leaves.entries()) {
      body.add(leaf);
      doors.push({ leaf, shut: leaf.position.clone(), open: leaf.position.clone(), swing: cut.swings[k]! });
    }
  }
  const size = box.getSize(new THREE.Vector3());
  // Where one gets in: by its doors on either side (the 4×4: each door's middle), or at its back.
  const doorCentres = v.way === 'side' ? [box.min.x, box.max.x].flatMap((x) => CAR_DOORS.map((d) => new THREE.Vector3(x, 0.5, box.min.z + size.z * (d.from + d.to) / 2))) : [new THREE.Vector3((box.min.x + box.max.x) / 2, 0.6, box.min.z)];
  // Headlights, off while it stands.
  const lights = new THREE.MeshStandardMaterial({ color: 0x2a2a20, emissive: 0xfff2c0, emissiveIntensity: 0.2 });
  for (const x of [-1, 1]) {
    const lamp = new THREE.Mesh(UNIT, lights);
    lamp.scale.set(0.2, 0.1, 0.04);
    lamp.position.set((box.min.x + box.max.x) / 2 + x * (size.x / 2 - 0.25), size.y * 0.3, box.max.z + 0.005);
    body.add(lamp);
  }
  return { body, doors, wheels, wheelRadius, lights, doorCentres, oneSided: false, size };
}

/**
 * The 4×4's four doors in a body of box `box` (nose +z): a leaf for each (left front, left rear, right front, right
 * rear) on its hinge, how far each swings open, and what splits a geometry into the doors' triangles and the rest
 * (a triangle goes by its middle).
 */
function carDoors(box: THREE.Box3) {
  const size = box.getSize(new THREE.Vector3());
  const midX = (box.min.x + box.max.x) / 2;
  const half = size.x / 2;
  const low = box.min.y + size.y * CAR_DOOR_LOW;
  const high = box.min.y + size.y * CAR_DOOR_HIGH;
  const doors = [-1, 1].flatMap((side) => CAR_DOORS.map((d) => ({ side, z0: box.min.z + size.z * d.from, z1: box.min.z + size.z * d.to })));
  const leaves = doors.map((d) => {
    const leaf = new THREE.Group();
    // On its front edge, at the body's side.
    leaf.position.set(midX + d.side * half, 0, d.z1);
    return leaf;
  });
  // A point behind the hinge swings out to its side: −x for the left (x < 0) side, turned the other way.
  const swings = doors.map((d) => (d.side > 0 ? -CAR_DOOR_SWING : CAR_DOOR_SWING));
  const which = (x: number, y: number, z: number) =>
    y < low || y > high || Math.abs(x - midX) < half * CAR_DOOR_SIDE ? -1 : doors.findIndex((d) => Math.sign(x - midX) === d.side && z >= d.z0 - 0.01 && z <= d.z1 + 0.01);
  const split = (g: THREE.BufferGeometry) => {
    const source = g.index ? g.toNonIndexed() : g;
    const names = Object.keys(source.attributes);
    const out = doors.map(() => names.map(() => [] as number[]));
    const rest = names.map(() => [] as number[]);
    const pos = source.getAttribute('position');
    for (let t = 0; t < pos.count; t += 3) {
      // A triangle whose three corners are in the same door's box (none across its edge).
      const k0 = which(pos.getX(t), pos.getY(t), pos.getZ(t));
      const k = k0 >= 0 && which(pos.getX(t + 1), pos.getY(t + 1), pos.getZ(t + 1)) === k0 && which(pos.getX(t + 2), pos.getY(t + 2), pos.getZ(t + 2)) === k0 ? k0 : -1;
      const into = k < 0 ? rest : out[k]!;
      names.forEach((n, a) => {
        const attr = source.getAttribute(n);
        for (let v = t; v < t + 3; v++) for (let c = 0; c < attr.itemSize; c++) into[a]!.push(attr.getComponent(v, c));
      });
    }
    const make = (data: number[][], at: THREE.Vector3 | null) => {
      if (!data[0]!.length) return null;
      const geometry = new THREE.BufferGeometry();
      names.forEach((n, a) => geometry.setAttribute(n, new THREE.Float32BufferAttribute(data[a]!, source.getAttribute(n).itemSize)));
      // A door's about its hinge.
      if (at) geometry.translate(-at.x, 0, -at.z);
      geometry.computeBoundingBox();
      return geometry;
    };
    return { doors: out.map((d, k) => make(d, leaves[k]!.position)), rest: make(rest, null) ?? new THREE.BufferGeometry() };
  };
  return { leaves, swings, split };
}

/** A door at `k` of the way open (0 shut, 1 open): slid along, or swung on its hinge. */
export function doorAt(d: Mula['doors'][number], k: number): void {
  d.leaf.position.lerpVectors(d.shut, d.open, k);
  if (d.swing) d.leaf.rotation.y = d.swing * k;
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
  return { body, doors, wheels, wheelRadius, lights, doorCentres, oneSided: true, size: model.size.clone() };
}
