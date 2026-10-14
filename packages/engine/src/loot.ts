// Searching (docs/FASE2.md, «Saqueo»): which furniture can be searched, what
// turns up in it and what the squad brings home. The rolls happen on the host
// (Sim.search); the `searched` event carries what was found.
import { FOUND_WEAPONS, ITEMS, NIGHT, TEMPLATES } from './content';
import { decorOf } from './decor';
import { DIRS8, heightAt, inBounds, kindAt, nodeOf, tileIndex, toPos } from './grid';
import type { Rng } from './rng';
import type { DecorKind, FoundWeaponId, GameState, ItemId, Loot, Place, Pos, TileKind, Unit } from './types';

/** What kind of place a searchable piece of furniture belongs to: it sets what turns up in it. */
type Spot = 'home' | 'office' | 'shop' | 'store' | 'trunk' | 'glovebox';

/** Searchable decor (Lore's SEARCH_SPOTS): furniture, crates and both halves of a car. */
const SPOTS: Partial<Record<DecorKind, Spot>> = {
  wardrobe: 'home',
  bed: 'home',
  table: 'home',
  desk: 'office',
  cabinet: 'office',
  shelf: 'shop',
  counter: 'shop',
  crates: 'store',
  rack: 'store',
  carRear: 'trunk',
  carFront: 'glovebox',
};

export const SEARCHABLE: readonly DecorKind[] = Object.keys(SPOTS) as DecorKind[];

interface LootTable {
  /** Chances out of 100, in this order; whatever is left turns up nothing. */
  supplies: number;
  item: number;
  medicines: number;
  /** Rations: pantries, shops and stores most (docs/FASE4.md). */
  food: number;
  /** Parts: stores, car boots and offices. */
  parts: number;
  /** A good weapon (FOUND_WEAPONS): rare, in boots, stores and wardrobes. */
  weapon: number;
  /** A magazine (docs/FASE7.md), for a searcher who counts them: stores and car boots most. */
  mags: number;
  /** Accessories that turn up there (only those the difficulty opens: `level` ≤ difficulty + 2). */
  items: ItemId[];
}

const TABLES: Record<Spot, LootTable> = {
  home: { supplies: 30, item: 8, medicines: 12, food: 20, parts: 0, weapon: 2, mags: 4, items: ['firstAid', 'smokeGrenade', 'nanoVest', 'mindShield'] },
  office: { supplies: 30, item: 8, medicines: 8, food: 4, parts: 15, weapon: 0, mags: 3, items: ['firstAid', 'flashbang', 'tracerRounds', 'mindShield'] },
  shop: { supplies: 35, item: 8, medicines: 12, food: 25, parts: 2, weapon: 0, mags: 6, items: ['firstAid', 'smokeGrenade', 'fragGrenade', 'tracerRounds'] },
  store: { supplies: 30, item: 14, medicines: 4, food: 15, parts: 15, weapon: 3, mags: 10, items: ['fragGrenade', 'smokeGrenade', 'tracerRounds', 'apRounds', 'flashbang'] },
  trunk: { supplies: 30, item: 14, medicines: 8, food: 5, parts: 15, weapon: 4, mags: 10, items: ['fragGrenade', 'firstAid', 'apRounds', 'nanoVest'] },
  glovebox: { supplies: 25, item: 5, medicines: 12, food: 3, parts: 5, weapon: 1, mags: 8, items: ['firstAid', 'tracerRounds'] },
};

/** Rations and parts found at once. */
const FOOD_FIND = [2, 4] as const;
const PARTS_FIND = [1, 2] as const;

/** Which good weapon turns up, by FOUND_WEAPONS weight. */
function foundWeapon(rng: Rng): FoundWeaponId {
  const all = Object.values(FOUND_WEAPONS);
  let roll = rng.int(all.reduce((n, w) => n + w.weight, 0));
  return all.find((w) => (roll -= w.weight) < 0)!.id;
}

/** Suministros found at once: 5–14, and 2 more per difficulty step (up to 20); half as much again at night. */
function supplies(difficulty: number, rng: Rng, night: boolean): number {
  const found = rng.range(5, 14) + 2 * (difficulty - 1);
  return night ? Math.round(found * NIGHT.supplies) : found;
}

/**
 * What a search of `decor` turns up at that difficulty (1–4), or null for
 * nothing; more suministros at night. A magazine only for a searcher who
 * counts them (`mags`); for anyone else that share turns up nothing, so the
 * other rolls come out as before.
 */
export function rollLoot(decor: DecorKind, difficulty: number, rng: Rng, night = false, mags = false): Loot | null {
  const spot = SPOTS[decor];
  if (!spot) return null;
  const d = Math.max(1, Math.min(4, difficulty));
  const t = TABLES[spot];
  const roll = rng.int(100);
  if (roll < t.supplies) return { supplies: supplies(d, rng, night) };
  if (roll < t.supplies + t.item) {
    const pool = t.items.filter((id) => ITEMS[id].level <= d + 2);
    return pool.length ? { item: rng.pick(pool) } : { supplies: supplies(d, rng, night) };
  }
  let edge = t.supplies + t.item + t.medicines;
  if (roll < edge) return { medicines: 1 };
  if (roll < (edge += t.food)) return { food: rng.range(...FOOD_FIND) };
  if (roll < (edge += t.parts)) return { parts: rng.range(...PARTS_FIND) };
  if (roll < (edge += t.weapon)) return { weapon: foundWeapon(rng) };
  if (roll < (edge += t.mags)) return mags ? { mags: 1 } : null;
  return null;
}

// ------------------------------------------------------------ the furniture

/** The searchable piece of furniture on `p`, searched or not; null if there is none or it was blown away. */
export function searchSpot(s: GameState, at: Place): DecorKind | null {
  const p = toPos(at);
  const decor = p.z === 0 ? s.decor : s.levels?.[p.z - 1]?.decor;
  if (!decor || !inBounds(s, p.x, p.y)) return null;
  const kind = kindAt(s, p);
  if (kind !== 'low' && kind !== 'high') return null;
  const look = decorOf(decor[tileIndex(s, p.x, p.y)]);
  return look && SPOTS[look.kind] ? look.kind : null;
}

export function wasSearched(s: GameState, p: Place): boolean {
  return (s.searched ?? []).includes(nodeOf(s, toPos(p)));
}

/** Furniture on `p` that is still there to search (for highlighting it anywhere on the map). */
export function searchable(s: GameState, p: Pos): boolean {
  return searchSpot(s, p) !== null && !wasSearched(s, p);
}

const SOLID: ReadonlySet<TileKind> = new Set<TileKind>(['wall', 'door', 'window', 'windowBroken']);

/** Whether a soldier on `from` reaches `p` with the hands: beside it, at its height, and not round a wall's corner. */
export function reachesToSearch(s: GameState, from: Pos, at: Place): boolean {
  const p = toPos(at);
  const dx = p.x - from.x;
  const dy = p.y - from.y;
  if (Math.max(Math.abs(dx), Math.abs(dy)) !== 1 || heightAt(s, from) !== heightAt(s, p)) return false;
  if (dx === 0 || dy === 0) return true;
  const solid = (x: number, y: number) => {
    const kind = kindAt(s, { x, y, z: from.z });
    return kind !== null && SOLID.has(kind);
  };
  return !solid(from.x + dx, from.y) || !solid(from.x, from.y + dy);
}

/** Furniture `u` can search right now from where it stands (any backpack room aside). */
export function searchableTiles(s: GameState, u: Unit): Pos[] {
  return DIRS8.map((d) => ({ x: u.pos.x + d.x, y: u.pos.y + d.y, z: u.pos.z })).filter((p) => searchable(s, p) && reachesToSearch(s, u.pos, p));
}

/** Room left in the backpack. */
export function backpackRoom(u: Unit, slots: number): number {
  return slots - (u.backpack?.length ?? 0);
}

// --------------------------------------------------------------- the haul

export interface LootTotals {
  supplies: number;
  items: ItemId[];
  medicines: number;
  food: number;
  parts: number;
  weapons: FoundWeaponId[];
}

export function lootTotals(loot: readonly Loot[]): LootTotals {
  const out: LootTotals = { supplies: 0, items: [], medicines: 0, food: 0, parts: 0, weapons: [] };
  for (const l of loot) {
    out.supplies += l.supplies ?? 0;
    if (l.item) out.items.push(l.item);
    out.medicines += l.medicines ?? 0;
    out.food += l.food ?? 0;
    out.parts += l.parts ?? 0;
    if (l.weapon) out.weapons.push(l.weapon);
  }
  return out;
}

/** Whether what `u` carries comes home: it got out, or it is standing when the mission is won. A fallen soldier's is lost. */
export function bringsLootHome(s: GameState, u: Unit): boolean {
  return TEMPLATES[u.template].team === 'xcom' && (u.evacuated || (u.alive && s.outcome === 'victory'));
}

/** What the squad brings back from the mission (docs/FASE2.md): only what comes home counts. */
export function broughtBack(s: GameState): LootTotals {
  return lootTotals(s.units.filter((u) => bringsLootHome(s, u)).flatMap((u) => u.backpack ?? []));
}
