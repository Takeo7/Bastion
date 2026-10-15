import * as THREE from 'three';
import type { GameState, Loot, Place, Pos } from '@bastion/engine';
import { tileToWorld } from './coords';
import { atlasMaterial, loadModel, modelNow } from './synty';
import { foundModel } from './syntyFigures';
import { WEAPONS, type PropId, type WeaponId } from './syntyManifest';
import { syntyProp, syntyPropsLoaded } from './syntyProps';

// What is left on the floor (docs/FASE8.md §C: GameState.dropped), one thing
// a tile, to be read from the tactical camera: a weapon lies on its side; a
// medicine is its first-aid box; food a carton with a can on it; anything
// else, and several things together, a bag. Each lies off the tile's middle
// (whoever stands there doesn't hide it), turned its own way, on a thin pale
// ring that tells it from the street's litter, and goes once it is picked up.

/** A tile's things as they look: a weapon's model, or props (each its size, set on the one before if `on`). */
type Look = { weapon: WeaponId } | { props: { id: PropId; size: number; on?: boolean; turn?: number }[] };

const BAG: Look = { props: [{ id: 'dropBag', size: 1 }] };

function lookOf(things: Loot[]): Look {
  if (things.length !== 1) return BAG;
  const l = things[0]!;
  if (l.weapon) return { weapon: foundModel(l.weapon) };
  if (l.medicines) return { props: [{ id: 'lootMedicalKit', size: 0.9 }] };
  if (l.food) return { props: [{ id: 'lootFoodTop', size: 0.72 }, { id: 'lootCan', size: 1.3, on: true, turn: 0.6 }] };
  return BAG;
}

/** Stable numbers from a thing's id, 0 to 1 (FNV-1a). */
function hash(id: string, k: number): number {
  let h = 0x811c9dc5 ^ k;
  for (let i = 0; i < id.length; i++) h = Math.imul(h ^ id.charCodeAt(i), 0x01000193);
  return (h >>> 0) / 0xffffffff;
}

/** How far from the tile's middle a thing lies. */
const OFF = 0.24;

/** The ring under each: pale, a little short of white (the bloom leaves it be), drawn over the floor. */
const RING = new THREE.RingGeometry(0.26, 0.3, 40).rotateX(-Math.PI / 2);
const RING_MATERIAL = new THREE.MeshBasicMaterial({ color: 0xe9dfc4, transparent: true, opacity: 0.6, depthWrite: false, toneMapped: false });

export class DroppedView {
  readonly group = new THREE.Group();
  /** The things drawn (their ids), to see one dropped or picked up. */
  private key: string | null = null;
  private tiles: { group: THREE.Group; place: Pos }[] = [];

  /** The floor's things as the state has them now; those on a storey the cutaway hides, hidden. */
  update(s: GameState, hidden: (p: Place) => boolean): void {
    const dropped = s.dropped ?? [];
    let key = '';
    for (const d of dropped) key += `${d.id},`;
    if (key !== this.key) {
      this.key = key;
      this.build(dropped);
    }
    for (const t of this.tiles) t.group.visible = !hidden(t.place);
  }

  private build(dropped: NonNullable<GameState['dropped']>): void {
    this.group.clear();
    this.tiles = [];
    const byTile = new Map<string, { pos: Pos; id: string; things: Loot[] }>();
    for (const d of dropped) {
      const k = `${d.pos.x},${d.pos.y},${d.pos.z}`;
      const t = byTile.get(k);
      if (t) t.things.push(d.loot);
      else byTile.set(k, { pos: d.pos, id: d.id, things: [d.loot] });
    }
    let waiting = false;
    for (const t of byTile.values()) {
      const g = new THREE.Group();
      const way = hash(t.id, 1) * Math.PI * 2;
      g.position.copy(tileToWorld(t.pos)).add(new THREE.Vector3(Math.sin(way) * OFF, 0, Math.cos(way) * OFF));
      g.rotation.y = hash(t.id, 2) * Math.PI * 2;
      if (!this.dress(g, lookOf(t.things))) waiting = true;
      const ring = new THREE.Mesh(RING, RING_MATERIAL);
      ring.position.y = 0.025;
      ring.renderOrder = 1;
      g.add(ring);
      this.group.add(g);
      this.tiles.push({ group: g, place: t.pos });
    }
    // Models still loading: drawn again once they are.
    if (waiting) void Promise.all([syntyPropsLoaded(), ...this.weaponLoads(dropped)]).then(() => (this.key = null));
  }

  private weaponLoads(dropped: NonNullable<GameState['dropped']>): Promise<unknown>[] {
    return dropped.flatMap((d) => {
      if (!d.loot.weapon) return [];
      const id = foundModel(d.loot.weapon);
      return [loadModel(WEAPONS[id].pack, 'wep', id).catch(() => undefined)];
    });
  }

  /** Puts a look's models in `g`, on the floor; false if some haven't loaded yet. */
  private dress(g: THREE.Group, look: Look): boolean {
    if ('weapon' in look) {
      const source = modelNow(WEAPONS[look.weapon].pack, 'wep', look.weapon);
      if (!source) return false;
      // On its side (its own x up), its middle on the spot, resting on the floor.
      const gun = source.clone();
      gun.rotation.set(0, 0, Math.PI / 2);
      gun.scale.setScalar((WEAPONS[look.weapon] as { size?: number }).size ?? 1);
      gun.updateMatrixWorld(true);
      const box = new THREE.Box3().setFromObject(gun);
      gun.position.set(-(box.min.x + box.max.x) / 2, -box.min.y + 0.005, -(box.min.z + box.max.z) / 2);
      g.add(gun);
      return true;
    }
    let base = 0;
    let all = true;
    for (const p of look.props) {
      const parts = syntyProp(p.id);
      if (!parts) {
        all = false;
        continue;
      }
      const placed = new THREE.Group();
      placed.scale.setScalar(p.size);
      placed.rotation.y = p.turn ?? 0;
      let top = 0;
      for (const part of parts) {
        const mesh = new THREE.Mesh(part.geometry, atlasMaterial(part.atlas));
        mesh.matrixAutoUpdate = false;
        mesh.matrix.copy(part.matrix);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        placed.add(mesh);
        const box = (part.geometry.boundingBox ?? (part.geometry.computeBoundingBox(), part.geometry.boundingBox!)).clone().applyMatrix4(part.matrix);
        top = Math.max(top, box.max.y);
      }
      placed.position.y = p.on ? base : 0;
      base = (p.on ? base : 0) + top * p.size;
      g.add(placed);
    }
    return all;
  }
}
