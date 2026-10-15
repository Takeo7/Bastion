import { decorOf } from './decor';
import { DIRS4 } from './grid';
import { Rng } from './rng';
import type { Decor, Level, MedicalDetail, MedicalRoom, MedicalSite, TileKind, Vec2 } from './types';

/** Cosmetic only. A separate seed leaves rooms, cover, objectives and combat rolls unchanged. */
export function dressMedical(site: MedicalSite, width: number, tiles: TileKind[], decor: Decor[], levels: Level[], seed: number, reserved: Vec2[] = []): MedicalDetail[] {
  const rng = new Rng((seed ^ 0x4d454449 ^ (site.x * 65537 + site.y)) >>> 0);
  const details: MedicalDetail[] = [], used = new Set<string>();
  const layer = (z: number) => z ? levels[z - 1]! : { tiles, decor };
  const key = (x: number, y: number, z: number) => `${x},${y},${z}`;
  const reservedFloor = new Set(reserved.map((p) => key(p.x, p.y, 0)));
  const add = (kind: MedicalDetail['kind'], x: number, y: number, z: number, facing = rng.int(4), height = 0) => {
    details.push({ kind, x, y, z, facing, height });
    used.add(key(x, y, z));
  };
  const shuffled = <T>(xs: T[]): T[] => {
    for (let i = xs.length - 1; i > 0; i--) { const j = rng.int(i + 1); [xs[i], xs[j]] = [xs[j]!, xs[i]!]; }
    return xs;
  };
  const cells = (r: MedicalRoom) => {
    const ps: { x: number; y: number; z: number }[] = [];
    for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) ps.push({ x, y, z: r.z });
    return ps;
  };
  const rooms = (site.rooms ?? []).filter((r) => r.use !== 'stairs');
  const beds = shuffled(rooms.flatMap(cells).filter((p) => decorOf(layer(p.z).decor?.[p.y * width + p.x])?.kind === 'hospitalBed'));
  const bodyTarget = rng.pick(site.kind === 'hospital' ? [1, 2, 3, 5, 5, 10, 10] : [1, 1, 2, 3, 5, 10]);
  // Some of the dead stay in beds. Others have fallen in the rooms; no body is a unit or obstacle.
  const bedBodies = Math.min(Math.floor(bodyTarget / 2), beds.length, site.kind === 'hospital' ? 3 : 1);
  beds.forEach((p, i) => {
    const facing = decorOf(layer(p.z).decor?.[p.y * width + p.x])!.facing;
    if (i < bedBodies) add('coveredBed', p.x, p.y, p.z, facing);
    else if (rng.next() < 0.24) add('bareBed', p.x, p.y, p.z, facing);
  });
  const floor = shuffled(rooms.flatMap(cells).filter((p) => layer(p.z).tiles[p.y * width + p.x] === 'floor'
    && !reservedFloor.has(key(p.x, p.y, p.z))
    // Keep thresholds and stair approaches visually clear as well as traversable.
    && !DIRS4.some((d) => ['door', 'ladder'].includes(layer(p.z).tiles[(p.y + d.y) * width + p.x + d.x] ?? ''))));
  let bodies = bedBodies;
  for (const p of floor) {
    if (bodies >= bodyTarget) break;
    if (used.has(key(p.x, p.y, p.z))) continue;
    add(rng.pick(['corpseCivilian', 'corpseStaff', 'coveredCorpse'] as const), p.x, p.y, p.z);
    bodies++;
  }
  // Even the smallest centre gets a body if its only floor is beside a doorway.
  if (!bodies) {
    const p = rooms.flatMap(cells).find((p) => layer(p.z).tiles[p.y * width + p.x] === 'floor' && !reservedFloor.has(key(p.x, p.y, p.z)));
    if (p) add('coveredCorpse', p.x, p.y, p.z);
  }
  for (const r of rooms) {
    const ps = cells(r), ds = layer(r.z).decor;
    const bed = ps.find((p) => decorOf(ds?.[p.y * width + p.x])?.kind === 'hospitalBed');
    const exam = ps.find((p) => ['examTable', 'gurney'].includes(decorOf(ds?.[p.y * width + p.x])?.kind ?? ''));
    // A folded screen fits inside the existing bed's tile: it never takes another cover cell.
    const screen = bed ?? exam;
    if (screen && (bed || rng.next() < 0.8)) add('privacyScreen', screen.x, screen.y, r.z, decorOf(ds?.[screen.y * width + screen.x])!.facing);
    for (const p of ps) {
      const look = decorOf(ds?.[p.y * width + p.x]);
      if (!look) continue;
      if (look.kind === 'bedsideTable') {
        add('medicalSupplies', p.x, p.y, r.z, look.facing, 0.53);
        if (rng.next() < 0.7) add('luggage', p.x, p.y, r.z, look.facing);
      } else if (look.kind === 'medicalCart') add('medicalSupplies', p.x, p.y, r.z, look.facing, 0.60);
      else if (look.kind === 'waitingTable' || look.kind === 'consultDesk') add('books', p.x, p.y, r.z, look.facing, look.kind === 'waitingTable' ? 0.40 : 0.79);
      else if (look.kind === 'medicalCabinet' && rng.next() < 0.65) add('crutches', p.x, p.y, r.z, look.facing);
    }
    const walls = ps.flatMap((p) => DIRS4.flatMap((d, facing) => layer(r.z).tiles[(p.y - d.y) * width + p.x - d.x] === 'wall' ? [{ ...p, facing }] : []));
    const wall = shuffled(walls)[0];
    if (wall) add(r.use === 'ward' ? 'wallCross' : r.use === 'waiting' || r.use === 'reception' || r.use === 'nurses' ? 'safeSign' : 'cautionSign', wall.x, wall.y, r.z, wall.facing);
  }
  return details;
}
