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
// else, and several things together, a bag. What a fallen soldier carried
// (`from`, playtest 18/10 #49) is their rucksack, tipped over, with their
// main weapon beside it, one for each of them on a tile, until nothing of
// theirs is left. Each lies off the tile's middle (whoever stands there
// doesn't hide it), turned its own way, on a thin pale ring that tells it
// from the street's litter, and goes once it is picked up.

/**
 * A tile's things as they look: a weapon's model, props (each its size, set on the one before if `on`), or a
 * fallen soldier's rucksack and their weapon.
 */
type Look = { weapon: WeaponId } | { props: { id: PropId; size: number; on?: boolean; turn?: number }[] } | { pack: true; weapon?: WeaponId };

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
    // By tile, and on it each fallen soldier's things apart.
    const byTile = new Map<string, { pos: Pos; id: string; things: Loot[]; from?: string }>();
    for (const d of dropped) {
      const k = `${d.pos.x},${d.pos.y},${d.pos.z}${d.from ? `:${d.from}` : ''}`;
      const t = byTile.get(k);
      if (t) t.things.push(d.loot);
      else byTile.set(k, { pos: d.pos, id: d.from ?? d.id, things: [d.loot], ...(d.from ? { from: d.from } : {}) });
    }
    let waiting = false;
    for (const t of byTile.values()) {
      const g = new THREE.Group();
      const way = hash(t.id, 1) * Math.PI * 2;
      g.position.copy(tileToWorld(t.pos)).add(new THREE.Vector3(Math.sin(way) * OFF, 0, Math.cos(way) * OFF));
      g.rotation.y = hash(t.id, 2) * Math.PI * 2;
      const weapon = t.things.find((l) => l.weapon)?.weapon;
      const look: Look = t.from ? { pack: true, ...(weapon ? { weapon: foundModel(weapon) } : {}) } : lookOf(t.things);
      if (!this.dress(g, look)) waiting = true;
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

  /** A weapon on its side (its own x up), its middle `aside` along the tile's x from the spot, resting on the floor; false if not loaded. */
  private laid(g: THREE.Group, weapon: WeaponId, aside = 0, turn = 0): boolean {
    const source = modelNow(WEAPONS[weapon].pack, 'wep', weapon);
    if (!source) return false;
    const gun = source.clone();
    gun.rotation.set(0, turn, Math.PI / 2, 'YXZ');
    gun.scale.setScalar((WEAPONS[weapon] as { size?: number }).size ?? 1);
    gun.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(gun);
    gun.position.set(aside - (box.min.x + box.max.x) / 2, -box.min.y + 0.005, -(box.min.z + box.max.z) / 2);
    g.add(gun);
    return true;
  }

  /** Puts a look's models in `g`, on the floor; false if some haven't loaded yet. */
  private dress(g: THREE.Group, look: Look): boolean {
    if ('pack' in look) {
      // The rucksack tipped onto its back, a little askew; the weapon beside it.
      const parts = syntyProp('fallenPack');
      const gun = look.weapon ? this.laid(g, look.weapon, 0.17, 0.25) : true;
      if (!parts) return false;
      const pack = new THREE.Group();
      for (const part of parts) {
        const mesh = new THREE.Mesh(part.geometry, atlasMaterial(part.atlas));
        mesh.matrixAutoUpdate = false;
        mesh.matrix.copy(part.matrix);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        pack.add(mesh);
      }
      pack.rotation.set(-1.35, 0.5, 0.12, 'YXZ');
      pack.updateMatrixWorld(true);
      const box = new THREE.Box3().setFromObject(pack);
      pack.position.set(-0.12 - (box.min.x + box.max.x) / 2, -box.min.y, -(box.min.z + box.max.z) / 2);
      g.add(pack);
      return gun;
    }
    if ('weapon' in look) return this.laid(g, look.weapon);
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
