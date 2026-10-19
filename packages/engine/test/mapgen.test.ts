import { describe, expect, it } from 'vitest';
import { canStandOn, DIRS4, DIRS8, isWalkable } from '../src/grid';
import { anchorOf, decorOf, FOOTPRINT, objectTiles } from '../src/decor';
import { GEN_SIZE, generateLayout, type MissionLayout } from '../src/mapgen';
import { roomAt } from '../src/loot';
import { computeReach, stepCost } from '../src/path';
import { Rng } from '../src/rng';
import { createMission, garrison, MISSION_TURNS, missionTurns, podPlan } from '../src/setup';
import type { Biome, GameState, MissionKind, TileKind, Vec2 } from '../src/types';

const BIOMES: Biome[] = ['city', 'wilds', 'facility'];
const KINDS: MissionKind[] = ['elimination', 'recovery', 'sabotage', 'hack', 'rescue'];
/** `ground` codes (GroundKind). */
const ROAD = 1;
const INDOOR = 2;

const layout = (biome: Biome, kind: MissionKind, seed: number) => generateLayout({ biome, kind, podSizes: [2, 3, 3, 2], seed: seed * 104729 });

/** Walkable tiles reachable from `start` with the movement rules. */
function reachable(l: MissionLayout, start: Vec2): Set<number> {
  const s = { width: l.width, height: l.height, tiles: l.tiles, elev: l.elev } as unknown as GameState;
  const seen = new Set([start.y * l.width + start.x]);
  const queue = [start];
  while (queue.length) {
    const p = queue.pop()!;
    for (const d of DIRS8) {
      if (stepCost(s, p.x, p.y, d.x, d.y) === null) continue;
      const q = { x: p.x + d.x, y: p.y + d.y };
      const i = q.y * l.width + q.x;
      if (seen.has(i)) continue;
      seen.add(i);
      queue.push(q);
    }
  }
  return seen;
}

/** Bounding boxes of the 4-connected areas of tiles that pass `inside`, with their tile count. */
function areas(l: MissionLayout, inside: (i: number) => boolean): { w: number; h: number; n: number }[] {
  const seen = new Set<number>();
  const out: { w: number; h: number; n: number }[] = [];
  for (let i = 0; i < l.tiles.length; i++) {
    if (seen.has(i) || !inside(i)) continue;
    const queue = [i];
    seen.add(i);
    let [x0, y0, x1, y1, n] = [Infinity, Infinity, -1, -1, 0];
    while (queue.length) {
      const j = queue.pop()!;
      const x = j % l.width;
      const y = (j - x) / l.width;
      [x0, y0, x1, y1, n] = [Math.min(x0, x), Math.min(y0, y), Math.max(x1, x), Math.max(y1, y), n + 1];
      for (const d of DIRS4) {
        const k = (y + d.y) * l.width + x + d.x;
        if (x + d.x < 0 || y + d.y < 0 || x + d.x >= l.width || y + d.y >= l.height || seen.has(k) || !inside(k)) continue;
        seen.add(k);
        queue.push(k);
      }
    }
    out.push({ w: x1 - x0 + 1, h: y1 - y0 + 1, n });
  }
  return out;
}

const centroid = (ps: Vec2[]): Vec2 => ({ x: ps.reduce((s, p) => s + p.x, 0) / ps.length, y: ps.reduce((s, p) => s + p.y, 0) / ps.length });
const dist = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y);

describe('procedural maps', () => {
  it('puts the opening medicines on reachable clinic room floor, beyond its doorways', () => {
    for (let seed = 0; seed < 80; seed++) {
      const s = createMission({
        map: { kind: 'generated', biome: 'city', seed: seed * 104729 },
        slots: seed % 2 ? [0] : [0, 1], rng: new Rng(seed), kind: 'recovery', difficulty: 1,
        campaignMission: { id: 'opening', name: 'El consultorio de Brena', story: 1 },
      });
      const label = `opening/${seed}`;
      expect(s.mission.kind, label).toBe('recovery');
      const p = s.mission.item!.pos!;
      const i = p.y * s.width + p.x;
      expect(s.tiles[i], label).toBe('floor');
      expect(s.ground![i], label).toBe(INDOOR);
      // A doorstep is tagged INDOOR too: all eight surrounding tiles must
      // still belong to the building, and none may be an outside entrance.
      for (const d of DIRS8) expect(s.ground![(p.y + d.y) * s.width + p.x + d.x], label).toBe(INDOOR);
      const room = roomAt(s, p);
      expect(room, label).not.toBeNull();
      const furniture = new Set(room!.tiles.flatMap((q) => DIRS4.map((d) => decorOf(s.decor![(q.y + d.y) * s.width + q.x + d.x])?.kind)));
      expect(furniture.has('examTable'), label).toBe(true);
      expect(furniture.has('medicalShelf'), label).toBe(true);
      const scout = s.units.find((u) => u.team === 'xcom')!;
      const reach = computeReach({ ...s, units: [scout] }, scout, 100_000);
      expect(reach.cost.has(i), label).toBe(true);
    }
  }, 30_000);

  it('keeps clinic furniture out of other missions', () => {
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 3 }, slots: [0], rng: new Rng(3), kind: 'recovery' });
    expect(s.decor!.some((d) => ['hospitalBed', 'medicalShelf'].includes(decorOf(d)?.kind ?? ''))).toBe(false);
  });

  it('are deterministic for a seed', () => {
    const a = generateLayout({ biome: 'city', kind: 'recovery', podSizes: [2, 2, 3], seed: 42 });
    const b = generateLayout({ biome: 'city', kind: 'recovery', podSizes: [2, 2, 3], seed: 42 });
    expect(a).toEqual(b);
  });

  it('are GEN_SIZE square, with streets or tracks on the ground layer', () => {
    for (const biome of BIOMES) {
      const l = layout(biome, 'elimination', 3);
      expect(l.width).toBe(GEN_SIZE);
      expect(l.height).toBe(GEN_SIZE);
      for (const layer of [l.tiles, l.elev, l.ground!]) expect(layer.length).toBe(GEN_SIZE * GEN_SIZE);
      expect(l.ground!.filter((g) => g === ROAD).length, biome).toBeGreaterThan(40);
    }
  });

  it('every biome and mission kind places the squad, pods and objective where the squad can reach them', () => {
    for (const biome of BIOMES) {
      for (const kind of KINDS) {
        for (let seed = 1; seed <= 8; seed++) {
          const plan = podPlan(1 + (seed % 4));
          const s = createMission({ map: { kind: 'generated', biome, seed: seed * 7919 }, slots: [0, 1], rng: new Rng(seed), kind, pods: plan });
          const label = `${biome}/${kind}/${seed}`;
          expect(s.mission.kind, label).toBe(kind);
          const soldiers = s.units.filter((u) => u.team === 'xcom' && !u.captive);
          expect(soldiers.length, label).toBe(6);
          // A scout alone on the map (no enemies blocking) reaches everything that matters.
          const scout = { ...soldiers[0]! };
          const alone = { ...s, units: [scout] };
          const reach = computeReach(alone, scout, 100_000);
          const near = (p: Vec2) => [p, ...DIRS8.map((d) => ({ x: p.x + d.x, y: p.y + d.y }))].some((q) => reach.cost.has(q.y * s.width + q.x) || (q.x === scout.pos.x && q.y === scout.pos.y));
          for (const u of s.units) expect(near(u.pos), `${label}: ${u.id} at ${u.pos.x},${u.pos.y}`).toBe(true);
          for (const z of s.mission.evacZone) {
            expect(canStandOn(s.tiles[z.y * s.width + z.x]!), label).toBe(true);
            expect(near(z), `${label}: evac`).toBe(true);
          }
          if (kind !== 'elimination') expect(s.mission.evacZone.length, label).toBe(9);
          if (kind === 'hack') expect(near(s.mission.terminal!), `${label}: terminal`).toBe(true);
          const aliens = s.units.filter((u) => u.team === 'alien' && !u.objective);
          // The plan, and the living enemy's whole strength from the start (playtest 2026-10-13).
          expect(aliens.length, label).toBe(plan.flat().length + (garrison(kind, 2)?.length ?? 0));
          for (const a of aliens) {
            const d = Math.min(...soldiers.map((x) => Math.hypot(x.pos.x - a.pos.x, x.pos.y - a.pos.y)));
            expect(d, `${label}: ${a.id} too close`).toBeGreaterThanOrEqual(12);
          }
        }
      }
    }
  }, 60_000);

  it('leave no walkable pocket out of reach (rooftops, rooms, the heart of a grove)', () => {
    for (const biome of BIOMES) {
      for (let seed = 1; seed <= 20; seed++) {
        const l = layout(biome, seed % 2 ? 'recovery' : 'hack', seed);
        const reach = reachable(l, l.spawns[0]!);
        const lost = l.tiles.map((t, i) => (isWalkable(t) && !reach.has(i) ? i : -1)).filter((i) => i >= 0);
        expect(lost, `${biome}/${seed}`).toEqual([]);
      }
    }
  });

  it('build walled buildings of believable sizes, with rooms at least 3×3 off corridors 2 wide', () => {
    const openings = new Set<TileKind>(['wall', 'door', 'doorOpen', 'window', 'windowBroken']);
    for (const biome of BIOMES) {
      let rooms = 0;
      for (let seed = 1; seed <= 12; seed++) {
        const l = layout(biome, 'recovery', seed);
        // A building is a full rectangle from 7 tiles (cabins, houses) to 16 (halls), walls included.
        for (const b of areas(l, (i) => l.ground![i] === INDOOR)) {
          expect(b.n, `${biome}/${seed}: ragged building`).toBe(b.w * b.h);
          expect(Math.min(b.w, b.h), `${biome}/${seed}`).toBeGreaterThanOrEqual(7);
          expect(Math.max(b.w, b.h), `${biome}/${seed}`).toBeLessThanOrEqual(16);
        }
        for (const r of areas(l, (i) => l.ground![i] === INDOOR && !openings.has(l.tiles[i]!))) {
          const corridor = Math.min(r.w, r.h) === 2 && Math.max(r.w, r.h) >= 6;
          if (!corridor) expect(Math.min(r.w, r.h), `${biome}/${seed}: room ${r.w}×${r.h}`).toBeGreaterThanOrEqual(3);
          rooms++;
        }
      }
      expect(rooms, biome).toBeGreaterThan(12);
    }
  });

  it('start the squad on the ground in a corner, with the objective in the far half and the evac on a side', () => {
    for (const biome of BIOMES) {
      for (const kind of ['recovery', 'sabotage', 'hack', 'rescue'] as MissionKind[]) {
        for (let seed = 1; seed <= 6; seed++) {
          const l = layout(biome, kind, seed);
          const label = `${biome}/${kind}/${seed}`;
          expect(l.spawns.length, label).toBe(8);
          const corner = { x: l.spawns[0]!.x < GEN_SIZE / 2 ? 0 : GEN_SIZE - 1, y: l.spawns[0]!.y < GEN_SIZE / 2 ? 0 : GEN_SIZE - 1 };
          for (const p of l.spawns) {
            expect(dist(p, corner), label).toBeLessThan(6);
            expect(l.elev[p.y * GEN_SIZE + p.x], label).toBe(0);
          }
          const squad = centroid(l.spawns);
          const objective = l.item ?? l.objectiveSpot ?? l.terminal;
          expect(objective, label).not.toBeNull();
          expect(dist(objective!, squad), label).toBeGreaterThan(GEN_SIZE * 0.55);
          // The evac sits near one of the two side corners, far from the squad.
          const evac = centroid(l.evacZone);
          expect(dist(evac, squad), label).toBeGreaterThan(GEN_SIZE * 0.5);
          expect(l.evacZone.every((z) => l.ground![z.y * GEN_SIZE + z.x] !== INDOOR && l.elev[z.y * GEN_SIZE + z.x] === 0), label).toBe(true);
        }
      }
    }
  });

  it('put the data and the VIP indoors, and a hack console with room around it', () => {
    for (const biome of BIOMES) {
      for (let seed = 1; seed <= 8; seed++) {
        for (const kind of ['recovery', 'rescue'] as MissionKind[]) {
          const l = layout(biome, kind, seed);
          const spot = (kind === 'recovery' ? l.item : l.objectiveSpot)!;
          expect(l.ground![spot.y * GEN_SIZE + spot.x], `${biome}/${kind}/${seed}`).toBe(INDOOR);
        }
        const l = layout(biome, 'hack', seed);
        const t = l.terminal!;
        expect(l.tiles[t.y * GEN_SIZE + t.x]).toBe('console');
        const free = DIRS8.filter((d) => canStandOn(l.tiles[(t.y + d.y) * GEN_SIZE + t.x + d.x] ?? 'wall')).length;
        expect(free, `${biome}/${seed}`).toBeGreaterThanOrEqual(3);
      }
    }
  });

  it('spread the pods away from the squad, each with a patrol route', () => {
    for (const biome of BIOMES) {
      for (let seed = 1; seed <= 8; seed++) {
        const l = layout(biome, 'elimination', seed);
        const squad = centroid(l.spawns);
        expect(l.pods.length).toBe(4);
        for (const pod of l.pods) {
          expect(pod.tiles.length).toBeGreaterThan(0);
          expect(pod.patrol.length).toBeGreaterThan(1);
          for (const p of pod.tiles) expect(dist(p, squad), `${biome}/${seed}`).toBeGreaterThanOrEqual(13);
        }
        const anchors = l.pods.map((p) => p.patrol[0]!);
        const closest = Math.min(...anchors.flatMap((a, i) => anchors.slice(i + 1).map((b) => dist(a, b))));
        expect(closest, `${biome}/${seed}`).toBeGreaterThanOrEqual(6);
      }
    }
  });

  it('city maps have rooftops and openings; wilds have hills', () => {
    let roofs = 0;
    let openings = 0;
    let hills = 0;
    for (let seed = 1; seed <= 10; seed++) {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed }, slots: [0], rng: new Rng(seed), kind: 'elimination' });
      roofs += s.elev.filter((e) => e > 0).length;
      openings += s.tiles.filter((t) => t === 'door' || t === 'window').length;
      // Every pod walks a round, but the living posted upstairs (docs/FASE6-PLAN.md), who stay put.
      for (const pod of s.pods) if (!s.units.some((u) => u.podId === pod.id && u.pos.z > 0)) expect(pod.patrol.length).toBeGreaterThan(0);
      hills += layout('wilds', 'elimination', seed).elev.filter((e) => e > 0).length;
    }
    expect(roofs).toBeGreaterThan(0);
    expect(openings).toBeGreaterThan(0);
    expect(hills).toBeGreaterThan(0);
  });

  it('say what their cover is meant to be, only on cover, with cars whole', () => {
    for (const biome of BIOMES) {
      const kinds = new Set<string>();
      for (let seed = 1; seed <= 6; seed++) {
        const l = layout(biome, seed % 2 ? 'recovery' : 'elimination', seed);
        const label = `${biome}/${seed}`;
        expect(l.decor!.length).toBe(GEN_SIZE * GEN_SIZE);
        expect(generateLayout({ biome, kind: seed % 2 ? 'recovery' : 'elimination', podSizes: [2, 3, 3, 2], seed: seed * 104729 }).decor, label).toEqual(l.decor);
        l.decor!.forEach((code, i) => {
          const look = decorOf(code);
          if (!look) return;
          kinds.add(look.kind);
          // A building's stairs are ladder tiles (docs/FASE6-PLAN.md), a flight's lower ones its parts (docs/MAPAS.md,
          // phase 2); everything else dressed is cover.
          const flight = look.kind === 'stairs' || (look.kind === 'part' && decorOf(l.decor![anchorOf(l.decor!, l.width, i)])?.kind === 'stairs');
          expect(flight ? ['ladder'] : ['low', 'high'], `${label}: ${look.kind} on ${l.tiles[i]}`).toContain(l.tiles[i]);
          // A car whole, over its three tiles (docs/MAPAS.md, phase 1); every other tile of an object leads to its anchor.
          if (look.kind === 'carFront') expect(objectTiles(l.decor!, l.width, i).length, `${label}: a car in pieces`).toBe(FOOTPRINT.car[1]);
          if (look.kind === 'part') expect(anchorOf(l.decor!, l.width, i), `${label}: a part of nothing`).toBeGreaterThanOrEqual(0);
        });
        // Everything on the ground is dressed; only rooftop parapets are left to the client.
        const plain = l.tiles.filter((t, i) => (t === 'low' || t === 'high') && !l.decor![i] && l.elev[i] === 0).length;
        expect(plain, label).toBe(0);
      }
      const expected = { city: ['carFront', 'streetTree', 'shelf', 'desk', 'fountain'], wilds: ['fence', 'gatePost', 'hay', 'tree', 'bush', 'rock'], facility: ['shelf', 'machine', 'container', 'pallets', 'drums', 'truck', 'silo', 'carFront'] }[biome];
      for (const k of expected) expect(kinds, `${biome}: ${k}`).toContain(k);
    }
    // The mission keeps the hints; hand-made maps have none.
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 3 }, slots: [0], rng: new Rng(3), kind: 'elimination' });
    expect(s.decor!.some((code) => decorOf(code)?.kind === 'carFront')).toBe(true);
    expect(createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(3) }).decor).toBeUndefined();
  });

  it('a facility is entered from the apron half the time; the other half the squad lands inside its fence', () => {
    let outside = 0;
    for (let seed = 1; seed <= 24; seed++) {
      const m = layout('facility', 'sabotage', seed);
      // The corner the squad starts in, and the wall line four to seven tiles away from it.
      const cx = m.spawns.every((p) => p.x < GEN_SIZE / 2) ? 0 : GEN_SIZE - 1;
      const cy = m.spawns.every((p) => p.y < GEN_SIZE / 2) ? 0 : GEN_SIZE - 1;
      const wallAt = (d: number) => {
        const x = Math.abs(cx - d);
        const y = Math.abs(cy - d);
        let n = 0;
        for (let k = 0; k < GEN_SIZE; k++) n += (m.tiles[k * GEN_SIZE + x] === 'wall' ? 1 : 0) + (m.tiles[y * GEN_SIZE + k] === 'wall' ? 1 : 0);
        return n;
      };
      if ([4, 5, 6, 7].some((d) => wallAt(d) >= 60)) outside++;
    }
    expect(outside).toBeGreaterThan(4);
    expect(outside).toBeLessThan(20);
  });

  it('generate quickly', () => {
    const start = Date.now();
    for (const biome of BIOMES) for (let seed = 1; seed <= 10; seed++) layout(biome, 'hack', seed);
    expect((Date.now() - start) / 30).toBeLessThan(60);
  });
});

describe('the scenarios (playtest 2026-10-14, #25)', () => {
  const kindAt = (l: MissionLayout, i: number) => decorOf(l.decor![i])?.kind;

  it('the service area and the industrial estate: everything reachable, every objective placed', () => {
    for (const [biome, site] of [['wilds', 'roadside'], ['city', 'industrial']] as const) {
      for (const kind of KINDS) {
        for (let seed = 1; seed <= 5; seed++) {
          const label = `${site}/${kind}/${seed}`;
          const s = createMission({ map: { kind: 'generated', biome, seed: seed * 7919, site }, slots: [0, 1], rng: new Rng(seed), kind, pods: podPlan(2) });
          expect(s.site, label).toBe(site);
          const scout = s.units.find((u) => u.team === 'xcom')!;
          const reach = computeReach({ ...s, units: [scout] }, scout, 100_000);
          const near = (p: Vec2) => [p, ...DIRS8.map((d) => ({ x: p.x + d.x, y: p.y + d.y }))].some((q) => reach.cost.has(q.y * s.width + q.x) || (q.x === scout.pos.x && q.y === scout.pos.y));
          for (const u of s.units) expect(near(u.pos), `${label}: ${u.id}`).toBe(true);
          for (const z of s.mission.evacZone) expect(near(z), `${label}: evac`).toBe(true);
          if (kind === 'recovery') expect(s.mission.item, label).not.toBeNull();
        }
      }
    }
  });

  it('they are made of what such places have, each object a whole block of its own', () => {
    const counts = (l: MissionLayout) => {
      const n: Record<string, number> = {};
      for (let i = 0; i < l.tiles.length; i++) {
        const k = kindAt(l, i);
        if (k) n[k] = (n[k] ?? 0) + 1;
      }
      return n;
    };
    for (let seed = 1; seed <= 6; seed++) {
      const road = generateLayout({ biome: 'wilds', kind: 'recovery', podSizes: [3, 3], seed, site: 'roadside' });
      const n = counts(road);
      expect(road.site).toBe('roadside');
      for (const k of ['truck', 'waterTower', 'sign', 'tank', 'counter']) expect(n[k] ?? 0, `roadside ${seed}: ${k}`).toBeGreaterThan(0);
      // A lorry is 2 × 6 where it fits, else 2 × 4 (docs/MAPAS.md, phase 1), a water tower 2 × 2: blocks that never touch another of their kind.
      for (const a of areas(road, (i) => kindAt(road, i) === 'truck')) expect([8, 12], `roadside ${seed}: truck`).toContain(a.n);
      for (const a of areas(road, (i) => kindAt(road, i) === 'waterTower')) expect(a.n, `roadside ${seed}: waterTower`).toBe(4);
      const estate = generateLayout({ biome: 'city', kind: 'recovery', podSizes: [3, 3], seed, site: 'industrial' });
      const m = counts(estate);
      expect(m.container ?? 0, `industrial ${seed}`).toBeGreaterThanOrEqual(10);
      for (const a of areas(estate, (i) => kindAt(estate, i) === 'container')) expect([a.w, a.h].sort(), `industrial ${seed}`).toEqual([2, 5]);
      // No homes on an industrial estate.
      expect(m.bed ?? 0).toBe(0);
    }
    // A site only where it belongs, and a map without one is the biome's usual.
    expect(generateLayout({ biome: 'facility', kind: 'recovery', podSizes: [3], seed: 1, site: 'roadside' }).site).toBeUndefined();
    expect(generateLayout({ biome: 'city', kind: 'recovery', podSizes: [3], seed: 1 })).toEqual(generateLayout({ biome: 'city', kind: 'recovery', podSizes: [3], seed: 1, site: 'roadside' }));
  });
});

describe('mission timers', () => {
  it('keep the values tuned on 36-tile maps and add turns on bigger ones, more when the objective comes back', () => {
    expect(missionTurns('hack', 36)).toBe(MISSION_TURNS.hack);
    expect(missionTurns('rescue', 30)).toBe(MISSION_TURNS.rescue);
    expect(missionTurns('elimination', 48)).toBeNull();
    expect(missionTurns('hack', 48)! - MISSION_TURNS.hack!).toBe(2);
    expect(missionTurns('sabotage', 48)! - MISSION_TURNS.sabotage!).toBe(2);
    expect(missionTurns('recovery', 48)! - MISSION_TURNS.recovery!).toBe(3);
    expect(missionTurns('rescue', 48)! - MISSION_TURNS.rescue!).toBe(3);
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 5 }, slots: [0], rng: new Rng(5), kind: 'recovery' });
    expect(s.mission.turnsLeft).toBe(missionTurns('recovery', GEN_SIZE));
    const plaza = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(5), kind: 'recovery' });
    expect(plaza.mission.turnsLeft).toBe(MISSION_TURNS.recovery);
  });
});
