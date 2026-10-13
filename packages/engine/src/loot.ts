// Searching (docs/FASE2.md, «Saqueo»): which furniture can be searched, what
// turns up in it and what the squad brings home. The rolls happen on the host
// (Sim.search); the `searched` event carries what was found.
import { ITEMS, TEMPLATES } from './content';
import { decorOf } from './decor';
import { DIRS8, elevAt, inBounds, tileAt, tileIndex } from './grid';
import type { Rng } from './rng';
import type { DecorKind, GameState, ItemId, Loot, TileKind, Unit, Vec2 } from './types';

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
  /** Chances out of 100; whatever is left turns up nothing. */
  supplies: number;
  item: number;
  medicines: number;
  /** Accessories that turn up there (only those the difficulty opens: `level` ≤ difficulty + 2). */
  items: ItemId[];
}

const TABLES: Record<Spot, LootTable> = {
  home: { supplies: 40, item: 10, medicines: 15, items: ['firstAid', 'smokeGrenade', 'nanoVest', 'mindShield'] },
  office: { supplies: 40, item: 10, medicines: 10, items: ['firstAid', 'flashbang', 'tracerRounds', 'mindShield'] },
  shop: { supplies: 50, item: 10, medicines: 15, items: ['firstAid', 'smokeGrenade', 'fragGrenade', 'tracerRounds'] },
  store: { supplies: 45, item: 20, medicines: 5, items: ['fragGrenade', 'smokeGrenade', 'tracerRounds', 'apRounds', 'flashbang'] },
  trunk: { supplies: 40, item: 20, medicines: 10, items: ['fragGrenade', 'firstAid', 'apRounds', 'nanoVest'] },
  glovebox: { supplies: 30, item: 5, medicines: 15, items: ['firstAid', 'tracerRounds'] },
};

/** Suministros found at once: 5–14, and 2 more per difficulty step (up to 20). */
function supplies(difficulty: number, rng: Rng): number {
  return rng.range(5, 14) + 2 * (difficulty - 1);
}

/** What a search of `decor` turns up at that difficulty (1–4), or null for nothing. */
export function rollLoot(decor: DecorKind, difficulty: number, rng: Rng): Loot | null {
  const spot = SPOTS[decor];
  if (!spot) return null;
  const d = Math.max(1, Math.min(4, difficulty));
  const t = TABLES[spot];
  const roll = rng.int(100);
  if (roll < t.supplies) return { supplies: supplies(d, rng) };
  if (roll < t.supplies + t.item) {
    const pool = t.items.filter((id) => ITEMS[id].level <= d + 2);
    return pool.length ? { item: rng.pick(pool) } : { supplies: supplies(d, rng) };
  }
  if (roll < t.supplies + t.item + t.medicines) return { medicines: 1 };
  return null;
}

// ------------------------------------------------------------ the furniture

/** The searchable piece of furniture on `p`, searched or not; null if there is none or it was blown away. */
export function searchSpot(s: GameState, p: Vec2): DecorKind | null {
  if (!s.decor || !inBounds(s, p.x, p.y)) return null;
  const kind = tileAt(s, p.x, p.y);
  if (kind !== 'low' && kind !== 'high') return null;
  const look = decorOf(s.decor[tileIndex(s, p.x, p.y)]);
  return look && SPOTS[look.kind] ? look.kind : null;
}

export function wasSearched(s: GameState, p: Vec2): boolean {
  return (s.searched ?? []).includes(tileIndex(s, p.x, p.y));
}

/** Furniture on `p` that is still there to search (for highlighting it anywhere on the map). */
export function searchable(s: GameState, p: Vec2): boolean {
  return searchSpot(s, p) !== null && !wasSearched(s, p);
}

const SOLID: ReadonlySet<TileKind> = new Set<TileKind>(['wall', 'door', 'window', 'windowBroken']);

/** Whether a soldier on `from` reaches `p` with the hands: beside it, on its level, and not round a wall's corner. */
export function reachesToSearch(s: GameState, from: Vec2, p: Vec2): boolean {
  const dx = p.x - from.x;
  const dy = p.y - from.y;
  if (Math.max(Math.abs(dx), Math.abs(dy)) !== 1 || elevAt(s, from.x, from.y) !== elevAt(s, p.x, p.y)) return false;
  if (dx === 0 || dy === 0) return true;
  return !SOLID.has(tileAt(s, from.x + dx, from.y)) || !SOLID.has(tileAt(s, from.x, from.y + dy));
}

/** Furniture `u` can search right now from where it stands (any backpack room aside). */
export function searchableTiles(s: GameState, u: Unit): Vec2[] {
  return DIRS8.map((d) => ({ x: u.pos.x + d.x, y: u.pos.y + d.y })).filter((p) => searchable(s, p) && reachesToSearch(s, u.pos, p));
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
}

export function lootTotals(loot: readonly Loot[]): LootTotals {
  const out: LootTotals = { supplies: 0, items: [], medicines: 0 };
  for (const l of loot) {
    out.supplies += l.supplies ?? 0;
    if (l.item) out.items.push(l.item);
    out.medicines += l.medicines ?? 0;
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
