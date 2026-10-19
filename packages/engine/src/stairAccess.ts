import { decorCode, decorOf } from './decor';
import { longFlightAt } from './flights';
import { DIRS4, kindAt, nodeOf, posOf, samePos } from './grid';
import { roomAt } from './loot';
import { stepBetween, stepsFrom } from './path';
import type { Decor, GameState, Pos, TileKind } from './types';

/** Terrain reachability, without units temporarily blocking a passage. */
function reachable(s: GameState, start: Pos): Set<number> {
  const seen = new Set([nodeOf(s, start)]);
  const queue = [start];
  while (queue.length) {
    const p = queue.pop()!;
    for (const d of DIRS4) for (const { to } of stepsFrom(s, p, d.x, d.y)) {
      const n = nodeOf(s, to);
      if (seen.has(n)) continue;
      seen.add(n);
      queue.push(to);
    }
  }
  return seen;
}

/**
 * Old generated missions preferred stairs against a wall. Move those flights
 * within their existing room when there is safe floor on both storeys. Never
 * move a unit, remove furniture or an objective, or cut off a reachable floor.
 * If no safe position fits, keep the saved terrain intact. Flights of several
 * tiles (docs/MAPAS.md, phase 2) are laid out right by the generator: left
 * alone.
 */
export function repairStairAccess(s: GameState): void {
  if (!s.mapId.startsWith('gen-') || !s.levels || !s.ground || !s.storeys) return;
  const searched = (s.roomsSearched ?? []).map((id) => ({ id, room: roomAt(s, posOf(s, id)) }));
  let changed = false;
  for (let z = 0; z < s.levels.length; z++) {
    const tiles: (TileKind | null)[] = z ? s.levels[z - 1]!.tiles : s.tiles;
    const decor: Decor[] | undefined = z ? s.levels[z - 1]!.decor : s.decor;
    if (!decor) continue;
    // Snapshot: a newly moved flight must not be processed a second time.
    const flights: { i: number; up: number }[] = tiles.flatMap((kind, i) => {
      const look = decorOf(decor[i]);
      if (kind !== 'ladder' || look?.kind !== 'stairs' || longFlightAt(s, { x: i % s.width, y: Math.floor(i / s.width), z })) return [];
      return [{ i, up: look.facing }];
    });
    for (const { i, up } of flights) {
      const at = { x: i % s.width, y: Math.floor(i / s.width), z };
      const clear = (state: GameState, p: Pos) => DIRS4.every((d) => [z, z + 1].every((k) => ['floor', 'low', 'high'].includes(kindAt(state, { x: p.x + d.x, y: p.y + d.y, z: k }) ?? '')));
      if (clear(s, at) || s.levels[z]!.tiles[i] !== null) continue;
      const before = reachable(s, at);
      const lower: (TileKind | null)[] = [...tiles];
      const lowerDecor: Decor[] = [...decor];
      const upper = [...s.levels[z]!.tiles];
      const upperDecor = s.levels[z]!.decor;
      lower[i] = 'floor';
      lowerDecor[i] = 0;
      upper[i] = 'floor';
      const trial: GameState = {
        ...s,
        tiles: z ? s.tiles : lower as GameState['tiles'],
        decor: z ? s.decor : lowerDecor,
        levels: s.levels.map((l, k) => k === z ? { ...l, tiles: upper } : k === z - 1 ? { ...l, tiles: lower, decor: lowerDecor } : l),
      };
      // Above the ground, roomAt needs the ceiling temporarily restored too.
      const room = roomAt(trial, at);
      if (!room) continue;
      const occupied = (p: Pos) => s.units.some((u) => samePos(u.pos, p)) || (s.dropped ?? []).some((d) => samePos(d.pos, p)) ||
        !!s.mission.item?.pos && samePos(s.mission.item.pos, p) || !!s.mission.terminal && samePos(s.mission.terminal, p) ||
        s.mission.reinforcements.some((r) => r.pos && samePos(r.pos, p)) || s.mission.evacZone.some((q) => samePos({ ...q, z: 0 }, p));
      const options: Pos[] = room.tiles.filter((p) => {
        const j = p.y * s.width + p.x;
        return j !== i && lower[j] === 'floor' && !lowerDecor[j] && upper[j] === 'floor' && !upperDecor?.[j] && !occupied({ ...p, z: z + 1 }) && clear(trial, p);
      }).sort((a, b) => Math.abs(a.x - at.x) + Math.abs(a.y - at.y) - Math.abs(b.x - at.x) - Math.abs(b.y - at.y));
      let moved = false;
      for (const p of options) {
        const j = p.y * s.width + p.x;
        for (const facing of [up, ...DIRS4.map((_, k) => k).filter((k) => k !== up)]) {
          const d = DIRS4[facing]!;
          const foot = { x: p.x - d.x, y: p.y - d.y, z };
          const landing = { x: p.x + d.x, y: p.y + d.y, z: z + 1 };
          if (kindAt(trial, foot) !== 'floor' || kindAt(trial, landing) !== 'floor') continue;
          lower[j] = 'ladder';
          lowerDecor[j] = decorCode('stairs', facing);
          upper[j] = null;
          const after = reachable(trial, at);
          const lost = [...before].some((n) => kindAt(trial, posOf(s, n)) !== null && !after.has(n));
          if (!lost && stepBetween(trial, p, landing) !== null && stepBetween(trial, landing, p) !== null && after.has(nodeOf(s, landing))) {
            tiles[i] = 'floor';
            decor[i] = 0;
            tiles[j] = 'ladder';
            decor[j] = lowerDecor[j]!;
            s.levels[z]!.tiles[i] = 'floor';
            s.levels[z]!.tiles[j] = null;
            moved = changed = true;
            break;
          }
          lower[j] = 'floor';
          lowerDecor[j] = 0;
          upper[j] = 'floor';
        }
        if (moved) break;
      }
    }
  }
  // Moving a stairwell may change the lowest tile index used as a room id.
  if (changed && s.roomsSearched) s.roomsSearched = [...new Set(searched.map(({ id, room }) => {
    for (const p of room?.tiles ?? []) {
      const now = roomAt(s, p);
      if (now) return now.id;
    }
    return id;
  }))];
}
