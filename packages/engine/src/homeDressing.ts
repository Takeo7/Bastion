import { decorOf } from './decor';
import { DIRS4 } from './grid';
import { Rng } from './rng';
import type { Decor, HomeRoom, HomeSite, Level, MedicalDetail, Pos, TileKind, Vec2 } from './types';

/**
 * What lies about in a house (playtest 2026-10-18, #15: «cadáveres, sangre,
 * cosas desordenadas y muebles caídos»), the more the worse off it is (its
 * wear): the dead where they fell, their blood and its trail, papers and
 * books strewn about, clothes off the beds, a chair over, bags left packed
 * by the door, grime along the walls and blood up them. The medical centres'
 * details (types.ts MedicalDetail), drawn the same way. On a seed of its own:
 * rooms, objectives and combat rolls are unchanged; never on a doorway's or a
 * ladder's approach, nor where somebody stands. What stands up (a chair over,
 * the bags) takes its tile, as cover (Arturo, 19/10: «que ocupen»), where
 * `strew` lets it (furnishing.ts Furnisher.strew: it closes no way); where it
 * doesn't, papers lie there instead. Without `strew`, nothing changes the map.
 */
export function dressHome(site: HomeSite, width: number, tiles: TileKind[], decor: Decor[], levels: Level[], seed: number, reserved: Vec2[] = [], strew?: (p: Pos) => boolean): MedicalDetail[] {
  const rng = new Rng((seed ^ 0x484f4d45 ^ (site.x * 40503 + site.y * 977)) >>> 0);
  const details: MedicalDetail[] = [];
  const used = new Set<string>();
  const layer = (z: number) => (z ? levels[z - 1] : { tiles, decor });
  const key = (x: number, y: number, z: number) => `${x},${y},${z}`;
  const blocked = new Set(reserved.map((p) => key(p.x, p.y, 0)));
  const add = (kind: MedicalDetail['kind'], p: { x: number; y: number; z: number }, facing = rng.int(4), height = 0) => {
    details.push({ kind, x: p.x, y: p.y, z: p.z, facing, height, variant: rng.int(8) });
    used.add(key(p.x, p.y, p.z));
  };
  const shuffled = <T>(xs: T[]): T[] => {
    for (let i = xs.length - 1; i > 0; i--) {
      const j = rng.int(i + 1);
      [xs[i], xs[j]] = [xs[j]!, xs[i]!];
    }
    return xs;
  };
  const tileAt = (x: number, y: number, z: number) => layer(z)?.tiles[y * width + x] ?? null;
  const cells = (r: HomeRoom) => {
    const ps: { x: number; y: number; z: number }[] = [];
    for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) ps.push({ x, y, z: r.z });
    return ps;
  };
  /** A room's open floor, clear of the ways through its doors and up its ladders. */
  const floorOf = (r: HomeRoom) =>
    cells(r).filter(
      (p) =>
        tileAt(p.x, p.y, p.z) === 'floor' &&
        !blocked.has(key(p.x, p.y, p.z)) &&
        !DIRS4.some((d) => ['door', 'doorOpen', 'ladder'].includes(tileAt(p.x + d.x, p.y + d.y, p.z) ?? '')),
    );
  const wear = site.wear;
  const rooms = site.rooms.filter((r) => layer(r.z));
  // The dead: none or one in a kept house, up to three in a wrecked one; some on the beds.
  const bodies = wear < 0.35 ? (rng.next() < 0.35 ? 1 : 0) : wear < 0.65 ? rng.range(0, 2) : rng.range(1, 3);
  const all = shuffled(rooms.flatMap(floorOf));
  let placed = 0;
  for (const p of all) {
    if (placed >= bodies) break;
    if (used.has(key(p.x, p.y, p.z))) continue;
    add('corpseCivilian', p);
    add('bloodPool', p);
    // The trail it left, a tile off.
    const trail = shuffled(DIRS4.map((d, facing) => ({ x: p.x + d.x, y: p.y + d.y, z: p.z, facing })).filter((q) => tileAt(q.x, q.y, q.z) === 'floor' && !blocked.has(key(q.x, q.y, q.z))))[0];
    if (trail && wear > 0.3) add('bloodDrops', trail, trail.facing);
    placed++;
  }
  for (const r of rooms) {
    const floor = shuffled(floorOf(r).filter((p) => !used.has(key(p.x, p.y, p.z))));
    const ps = cells(r);
    const ds = layer(r.z)?.decor;
    // By what stands in it: clothes off a bed, books off a shelf or a desk.
    for (const p of ps) {
      const look = decorOf(ds?.[p.y * width + p.x]);
      if (!look) continue;
      if (look.kind === 'bed' && rng.next() < 0.35 + wear * 0.5) add('rumpledLinen', p, look.facing);
      if ((look.kind === 'shelf' || look.kind === 'desk') && rng.next() < wear) {
        const near = floor.find((q) => !used.has(key(q.x, q.y, q.z)) && Math.abs(q.x - p.x) + Math.abs(q.y - p.y) === 1);
        if (near) add('books', near);
      }
    }
    // Strewn about, the more the worse off it is.
    const mess = Math.round(wear * (r.use === 'bath' || r.use === 'hall' ? 1 : 3) + rng.next() * 0.8);
    for (const p of floor.slice(0, mess)) {
      if (used.has(key(p.x, p.y, p.z))) continue;
      const pick = rng.next();
      const kind = pick < 0.45 ? 'scatteredPapers' : pick < 0.7 && (r.use === 'kitchen' || r.use === 'living' || r.use === 'study') ? 'overturnedChair' : pick < 0.85 && r.use === 'bedroom' ? 'luggage' : 'scatteredPapers';
      add(kind !== 'scatteredPapers' && strew && !strew(p) ? 'scatteredPapers' : kind, p);
    }
    // Grime along the walls; blood up one in a wrecked house.
    const edges = floor.filter((p) => p.x === r.x || p.y === r.y || p.x === r.x + r.w - 1 || p.y === r.y + r.h - 1);
    for (const p of edges.slice(0, Math.round(wear * 3))) add('floorGrime', p);
    if (wear > 0.6 && rng.next() < wear - 0.3) {
      const walls = floor.flatMap((p) => DIRS4.flatMap((d, facing) => (tileAt(p.x - d.x, p.y - d.y, p.z) === 'wall' ? [{ ...p, facing }] : [])));
      const wall = shuffled(walls)[0];
      if (wall) add('wallBlood', wall, wall.facing);
    }
  }
  return details;
}
