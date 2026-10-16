import { DIRS4 } from './grid';
import { Rng } from './rng';
import type { Decor, Level, MedicalDetail, MedicalSite, TileKind, Vec2 } from './types';

/** Separate scenery roll: preserves the approved bodies, furniture and combat RNG. */
export function medicalAbandonment(site: MedicalSite, width: number, tiles: TileKind[], decor: Decor[], levels: Level[], seed: number, reserved: Vec2[], existing: MedicalDetail[]): MedicalDetail[] {
  const rng = new Rng((seed ^ 0x424c4f4f ^ (site.x * 8191 + site.y * 131)) >>> 0);
  const details: MedicalDetail[] = [];
  const layer = (z: number) => z ? levels[z - 1]! : { tiles, decor };
  const blocked = new Set(reserved.map((p) => `${p.x},${p.y},0`));
  const bodies = existing.filter((d) => ['corpseCivilian', 'corpseStaff', 'coveredCorpse', 'coveredBed'].includes(d.kind));
  const occupied = new Set(bodies.map((p) => `${p.x},${p.y},${p.z}`));
  const intensity = rng.range(2, site.kind === 'clinic' ? 4 : 6);
  const add = (kind: MedicalDetail['kind'], p: { x: number; y: number; z: number }, facing = rng.int(4)) =>
    details.push({ kind, ...p, facing, height: 0, variant: rng.int(8) });
  const shuffled = <T>(xs: T[]): T[] => {
    for (let i = xs.length - 1; i > 0; i--) { const j = rng.int(i + 1); [xs[i], xs[j]] = [xs[j]!, xs[i]!]; }
    return xs;
  };
  for (const body of bodies) {
    add('bloodPool', { x: body.x, y: body.y, z: body.z }, body.facing);
    const room = site.rooms?.find((r) => r.z === body.z && body.x >= r.x && body.x < r.x + r.w && body.y >= r.y && body.y < r.y + r.h);
    if (!room) continue;
    const neighbours = DIRS4.map((d, facing) => ({ x: body.x + d.x, y: body.y + d.y, z: body.z, facing }))
      .filter((p) => p.x >= room.x && p.x < room.x + room.w && p.y >= room.y && p.y < room.y + room.h && layer(p.z).tiles[p.y * width + p.x] === 'floor' && !blocked.has(`${p.x},${p.y},${p.z}`));
    const trail = shuffled(neighbours)[0];
    if (trail) add('bloodDrops', { x: trail.x, y: trail.y, z: trail.z }, trail.facing);
  }
  for (const room of site.rooms ?? []) {
    if (room.use === 'stairs') continue;
    const floor: { x: number; y: number; z: number }[] = [];
    for (let y = room.y; y < room.y + room.h; y++) for (let x = room.x; x < room.x + room.w; x++) {
      if (layer(room.z).tiles[y * width + x] === 'floor' && !blocked.has(`${x},${y},${room.z}`)) floor.push({ x, y, z: room.z });
    }
    const walls = floor.flatMap((p) => DIRS4.flatMap((d, facing) => layer(p.z).tiles[(p.y - d.y) * width + p.x - d.x] === 'wall' ? [{ ...p, facing }] : []));
    // Grime gathers at room edges. Flat litter can cross a route, bulky objects cannot.
    const edges = shuffled(floor.filter((p) => p.x === room.x || p.y === room.y || p.x === room.x + room.w - 1 || p.y === room.y + room.h - 1));
    for (const p of edges.slice(0, Math.min(3, intensity))) add('floorGrime', p);
    const clear = shuffled(floor.filter((p) => !occupied.has(`${p.x},${p.y},${p.z}`)
      && !DIRS4.some((d) => ['door', 'ladder'].includes(layer(p.z).tiles[(p.y + d.y) * width + p.x + d.x] ?? ''))));
    for (const [i, p] of clear.slice(0, intensity).entries()) {
      if (i === 0) add('scatteredPapers', p);
      else if (i === 1) add(room.use === 'ward' ? 'rumpledLinen' : 'spilledSupplies', p);
      else if (i === 2 && edges.some((e) => e.x === p.x && e.y === p.y)) add(room.use === 'waiting' || room.use === 'reception' ? 'overturnedChair' : 'fallenIV', p);
      else add(rng.pick(['scatteredPapers', 'medicalWaste', 'spilledSupplies'] as const), p);
    }
    const nearby = bodies.filter((d) => d.z === room.z && d.x >= room.x && d.x < room.x + room.w && d.y >= room.y && d.y < room.y + room.h);
    const wall = shuffled(walls).sort((a, b) => {
      const distance = (p: Vec2) => Math.min(...nearby.map((d) => Math.abs(d.x - p.x) + Math.abs(d.y - p.y)));
      return nearby.length ? distance(a) - distance(b) : 0;
    }).find((p) => !existing.some((d) => ['wallCross', 'safeSign', 'cautionSign'].includes(d.kind) && d.x === p.x && d.y === p.y && d.z === p.z && d.facing === p.facing));
    if (wall && (nearby.length || rng.next() < 0.25)) add('wallBlood', { x: wall.x, y: wall.y, z: wall.z }, wall.facing);
  }
  return details;
}
