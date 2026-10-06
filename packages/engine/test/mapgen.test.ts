import { describe, expect, it } from 'vitest';
import { canStandOn } from '../src/grid';
import { generateLayout } from '../src/mapgen';
import { computeReach } from '../src/path';
import { Rng } from '../src/rng';
import { createMission, podPlan } from '../src/setup';
import type { Biome, MissionKind, Vec2 } from '../src/types';

const BIOMES: Biome[] = ['city', 'wilds', 'facility'];
const KINDS: MissionKind[] = ['elimination', 'recovery', 'sabotage', 'hack', 'rescue'];

describe('procedural maps', () => {
  it('are deterministic for a seed', () => {
    const a = generateLayout({ biome: 'city', kind: 'recovery', podSizes: [2, 2, 3], seed: 42 });
    const b = generateLayout({ biome: 'city', kind: 'recovery', podSizes: [2, 2, 3], seed: 42 });
    expect(a).toEqual(b);
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
          const near = (p: Vec2) => [p, { x: p.x + 1, y: p.y }, { x: p.x - 1, y: p.y }, { x: p.x, y: p.y + 1 }, { x: p.x, y: p.y - 1 }, { x: p.x + 1, y: p.y + 1 }, { x: p.x - 1, y: p.y - 1 }, { x: p.x + 1, y: p.y - 1 }, { x: p.x - 1, y: p.y + 1 }]
            .some((q) => reach.cost.has(q.y * s.width + q.x) || (q.x === scout.pos.x && q.y === scout.pos.y));
          for (const u of s.units) expect(near(u.pos), `${label}: ${u.id} at ${u.pos.x},${u.pos.y}`).toBe(true);
          for (const z of s.mission.evacZone) {
            expect(canStandOn(s.tiles[z.y * s.width + z.x]!), label).toBe(true);
            expect(near(z), `${label}: evac`).toBe(true);
          }
          if (kind !== 'elimination') expect(s.mission.evacZone.length, label).toBe(9);
          if (kind === 'hack') expect(near(s.mission.terminal!), `${label}: terminal`).toBe(true);
          const aliens = s.units.filter((u) => u.team === 'alien' && !u.objective);
          expect(aliens.length, label).toBe(plan.flat().length);
          for (const a of aliens) {
            const d = Math.min(...soldiers.map((x) => Math.hypot(x.pos.x - a.pos.x, x.pos.y - a.pos.y)));
            expect(d, `${label}: ${a.id} too close`).toBeGreaterThanOrEqual(10);
          }
        }
      }
    }
  });

  it('city maps have rooftops and openings; every pod gets a patrol route', () => {
    let roofs = 0;
    let openings = 0;
    for (let seed = 1; seed <= 10; seed++) {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed }, slots: [0], rng: new Rng(seed), kind: 'elimination' });
      roofs += s.elev.filter((e) => e > 0).length;
      openings += s.tiles.filter((t) => t === 'door' || t === 'window').length;
      for (const pod of s.pods) expect(pod.patrol.length).toBeGreaterThan(0);
    }
    expect(roofs).toBeGreaterThan(0);
    expect(openings).toBeGreaterThan(0);
  });
});
