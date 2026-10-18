// Searching (docs/FASE2.md, «Saqueo»): which furniture can be searched, what
// turns up in it and what the squad brings home. The rolls happen on the host
// (Sim.search); the `searched` event carries what was found.
import { BACKPACK_SLOTS, FOUND_WEAPONS, ITEMS, NIGHT, TEMPLATES, WEAPON_MODS } from './content';
import { decorOf } from './decor';
import { DIRS4, DIRS8, heightAt, inBounds, isWalkable, kindAt, nodeOf, slabAt, tileIndex, toPos } from './grid';
import type { Rng } from './rng';
import type { ChargeId, DecorKind, FoundWeaponId, GameState, ItemId, Loot, Place, Pos, TileKind, Unit, WeaponModId } from './types';

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
  /** An improvement for a weapon (docs/FASE9.md §5): stores and car boots most. */
  mod: number;
  /** A magazine (docs/FASE7.md), for a searcher who counts them: stores and car boots most. */
  mags: number;
  /** Accessories that turn up there (only those the difficulty opens: `level` ≤ difficulty + 2). */
  items: ItemId[];
}

const TABLES: Record<Spot, LootTable> = {
  // Special rounds aren't found any more: the weapons' mods took their place (docs/FASE9.md §5).
  home: { supplies: 30, item: 8, medicines: 3, food: 20, parts: 0, weapon: 2, mod: 0, mags: 4, items: ['firstAid', 'smokeGrenade', 'nanoVest', 'mindShield'] },
  office: { supplies: 30, item: 8, medicines: 2, food: 4, parts: 15, weapon: 0, mod: 1, mags: 3, items: ['firstAid', 'flashbang', 'mindShield'] },
  shop: { supplies: 35, item: 8, medicines: 12, food: 25, parts: 2, weapon: 0, mod: 1, mags: 6, items: ['firstAid', 'smokeGrenade', 'fragGrenade'] },
  store: { supplies: 30, item: 14, medicines: 2, food: 15, parts: 15, weapon: 3, mod: 4, mags: 10, items: ['fragGrenade', 'smokeGrenade', 'flashbang'] },
  trunk: { supplies: 30, item: 14, medicines: 8, food: 5, parts: 15, weapon: 4, mod: 3, mags: 10, items: ['fragGrenade', 'firstAid', 'nanoVest'] },
  glovebox: { supplies: 25, item: 5, medicines: 12, food: 3, parts: 5, weapon: 1, mod: 1, mags: 8, items: ['firstAid'] },
};

/** Rations and parts found at once. */
const FOOD_FIND = [2, 4] as const;
const PARTS_FIND = [1, 2] as const;

/**
 * Which weapon turns up, by FOUND_WEAPONS weight, as it is found (docs/FASE9.md
 * §5): somewhat worn, and a good one with its improvement mounted.
 */
function foundWeapon(rng: Rng): Loot {
  const all = Object.values(FOUND_WEAPONS);
  let roll = rng.int(all.reduce((n, w) => n + w.weight, 0));
  const def = all.find((w) => (roll -= w.weight) < 0)!;
  return { weapon: def.id, condition: rng.range(FOUND_CONDITION[0], FOUND_CONDITION[1]), ...(def.mod ? { mods: [def.mod] } : {}) };
}

/** The condition a weapon is found in. */
const FOUND_CONDITION = [40, 90] as const;

/** Which improvement turns up. */
function foundMod(rng: Rng): Loot {
  return { mod: rng.pick(Object.keys(WEAPON_MODS) as WeaponModId[]) };
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
  return spot ? rollSpot(spot, difficulty, rng, night, mags) : null;
}

/** What searching a place of kind `spot` turns up: rollLoot by the kind of place, not the piece of furniture. */
function rollSpot(spot: Spot, difficulty: number, rng: Rng, night: boolean, mags: boolean): Loot | null {
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
  if (roll < (edge += t.weapon)) return foundWeapon(rng);
  if (roll < (edge += t.mod)) return foundMod(rng);
  if (roll < (edge += t.mags)) return mags ? { mags: 1 } : null;
  return null;
}

// ------------------------------------------------------------ the furniture

// ------------------------------------------------------- the loot boxes

/** The special boxes (docs/FASE8.md §B): each always holds what it is marked for. */
export const LOOT_BOXES: readonly DecorKind[] = ['lootWeapons', 'lootMedical', 'lootAmmo', 'lootFood'];

/**
 * What opening a box of `kind` turns up: a good weapon, one or two
 * medicines, two magazines (for a searcher who counts them; nothing for
 * anyone else), or three to five rations.
 */
export function rollBox(kind: DecorKind, rng: Rng, mags: boolean): Loot | null {
  switch (kind) {
    case 'lootWeapons':
      // A weapon, or now and then an improvement for one (docs/FASE9.md §5).
      return rng.int(10) < 3 ? foundMod(rng) : foundWeapon(rng);
    case 'lootMedical':
      return { medicines: rng.range(1, 2) };
    case 'lootAmmo':
      return mags ? { mags: 2 } : null;
    case 'lootFood':
      return { food: rng.range(3, 5) };
    default:
      return null;
  }
}

/**
 * The loot box on `p`, opened or not; null if there is none or it was blown
 * away. Furniture and cars are no longer searched one by one (docs/FASE8.md
 * §B): a room is, as a whole (roomAt).
 */
export function searchSpot(s: GameState, at: Place): DecorKind | null {
  const p = toPos(at);
  const decor = p.z === 0 ? s.decor : s.levels?.[p.z - 1]?.decor;
  if (!decor || !inBounds(s, p.x, p.y)) return null;
  const kind = kindAt(s, p);
  if (kind !== 'low' && kind !== 'high') return null;
  const look = decorOf(decor[tileIndex(s, p.x, p.y)]);
  return look && LOOT_BOXES.includes(look.kind) ? look.kind : null;
}

// ------------------------------------------------------------- the rooms

/** The ground's mark for the inside of a building (mapgen.ts INDOOR). */
const INDOOR_GROUND = 2;

/** A room (docs/FASE8.md §B): its floor tiles on one level, the kind of place its furniture makes it, and its id (its first node). */
export interface Room {
  id: number;
  tiles: Pos[];
  spot: Spot;
}

/** Whether place (x, y) on level `z` is inside a building: an indoor tile on the ground, or a storey with a floor over it. */
function interior(s: GameState, x: number, y: number, z: number): boolean {
  return z === 0 ? (s.ground?.[tileIndex(s, x, y)] ?? 0) === INDOOR_GROUND : slabAt(s, x, y, z + 1);
}

/** Floor a room is made of: walkable, but not a doorway nor a window (they part one room from the next). */
function roomFloor(kind: TileKind | null): boolean {
  return isWalkable(kind) && kind !== 'door' && kind !== 'doorOpen' && kind !== 'window' && kind !== 'windowBroken';
}

/**
 * The room place `at` stands in (docs/FASE8.md §B): the floor joined to it on
 * its level, inside the building, without crossing a doorway or a window.
 * Its kind of place is the one most of its furniture makes it (a home, an
 * office, a shop, a store), a home when it has none. Null outdoors, on a
 * roof, in a doorway, or on a hand-made map (they mark no inside).
 */
export function roomAt(s: GameState, at: Place): Room | null {
  const start = toPos(at);
  if (!inBounds(s, start.x, start.y) || !roomFloor(kindAt(s, start)) || !interior(s, start.x, start.y, start.z)) return null;
  const z = start.z;
  const decor = z === 0 ? s.decor : s.levels?.[z - 1]?.decor;
  const seen = new Set<number>([tileIndex(s, start.x, start.y)]);
  const tiles: Pos[] = [];
  const furniture = new Map<number, Spot>();
  const queue: Pos[] = [start];
  while (queue.length) {
    const p = queue.shift()!;
    tiles.push(p);
    for (const d of DIRS4) {
      const x = p.x + d.x;
      const y = p.y + d.y;
      if (!inBounds(s, x, y)) continue;
      const i = tileIndex(s, x, y);
      const q = { x, y, z };
      const kind = kindAt(s, q);
      if ((kind === 'low' || kind === 'high') && decor) {
        const look = decorOf(decor[i]);
        const spot = look && SPOTS[look.kind];
        if (spot) furniture.set(i, spot);
      }
      if (seen.has(i) || !roomFloor(kind) || !interior(s, x, y, z)) continue;
      seen.add(i);
      queue.push(q);
    }
  }
  const counts = new Map<Spot, number>();
  for (const spot of furniture.values()) counts.set(spot, (counts.get(spot) ?? 0) + 1);
  const spot = [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))[0]?.[0] ?? 'home';
  const id = Math.min(...tiles.map((p) => nodeOf(s, p)));
  return { id, tiles, spot };
}

/** Whether the squad has already searched `room` (docs/FASE8.md §B: once for everyone). */
export function roomSearched(s: GameState, room: Pick<Room, 'id'>): boolean {
  return (s.roomsSearched ?? []).includes(room.id);
}

/** What searching `room` turns up at that difficulty: the furniture's tables, rolled once for the room. */
export function rollRoom(room: Room, difficulty: number, rng: Rng, night = false, mags = false): Loot | null {
  return rollSpot(room.spot, difficulty, rng, night, mags);
}

/** Room in a soldier's backpack: BACKPACK_SLOTS, one more with the Chapistería (docs/FASE8.md §C). */
export function packSlots(u: Pick<Unit, 'packSlots'>): number {
  return u.packSlots ?? BACKPACK_SLOTS;
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
  /** Weapons found: new to the Armería. */
  weapons: FoundWeaponId[];
  /** The same, as found: their condition and mods (docs/FASE9.md §5). */
  arms?: CarriedWeapon[];
  /** The Armería's own weapons coming back in a backpack (Loot.weaponId), swapped for a find (docs/FASE8.md §C). */
  returned?: (CarriedWeapon & { id: string })[];
  /** Loose improvements found, for the Armería (docs/FASE9.md §5). */
  mods?: WeaponModId[];
}

/** A weapon with its condition and mods, as it travels in a backpack. */
export interface CarriedWeapon {
  kind: FoundWeaponId;
  condition?: number;
  mods?: WeaponModId[];
}

export function lootTotals(loot: readonly Loot[]): LootTotals {
  const out: LootTotals = { supplies: 0, items: [], medicines: 0, food: 0, parts: 0, weapons: [] };
  for (const l of loot) {
    out.supplies += l.supplies ?? 0;
    if (l.item) out.items.push(l.item);
    out.medicines += l.medicines ?? 0;
    out.food += l.food ?? 0;
    out.parts += l.parts ?? 0;
    const carried: CarriedWeapon | null = l.weapon ? { kind: l.weapon, ...(l.condition !== undefined ? { condition: l.condition } : {}), ...(l.mods?.length ? { mods: [...l.mods] } : {}) } : null;
    if (carried && l.weaponId) (out.returned ??= []).push({ id: l.weaponId, ...carried });
    else if (carried) {
      out.weapons.push(carried.kind);
      (out.arms ??= []).push(carried);
    }
    if (l.mod) (out.mods ??= []).push(l.mod);
  }
  return out;
}

/** Whether what `u` carries comes home: it got out, or it is standing when the mission is won. A fallen soldier's is lost. */
export function bringsLootHome(s: GameState, u: Unit): boolean {
  return TEMPLATES[u.template].team === 'xcom' && (u.evacuated || (u.alive && s.outcome === 'victory'));
}

/** What the squad brings back from the mission (docs/FASE2.md): only what comes home counts. */
export function broughtBack(s: GameState): LootTotals {
  // A vest put on out there comes home on the soldier, as if in the backpack; a belt thing too, if not dipped into.
  return lootTotals(s.units.filter((u) => bringsLootHome(s, u)).flatMap((u) => [...(u.backpack ?? []), ...(u.worn ?? []).map((item) => ({ item })), ...beltedBack(u)]));
}

/**
 * The belt things found and put on out there that come home (playtest 18/10
 * #5): those whose uses are all still there, counting the soldier's own as
 * spent first (they are the Armería's, and it fills them again).
 */
export function beltedBack(u: Unit): Loot[] {
  const left: Partial<Record<ChargeId, number>> = { ...u.charges };
  const back: Loot[] = [];
  for (const item of [...(u.belted ?? [])].reverse()) {
    const uses = Object.entries(ITEMS[item].charges ?? {}) as [ChargeId, number][];
    if (!uses.every(([charge, n]) => (left[charge] ?? 0) >= n)) continue;
    for (const [charge, n] of uses) left[charge] = (left[charge] ?? 0) - n;
    back.push({ item });
  }
  return back;
}
