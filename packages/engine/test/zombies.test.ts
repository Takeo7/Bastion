import { describe, expect, it } from 'vitest';
import { abilityBlocker, canTargetTile, noiseOf } from '../src/abilities';
import { applyMissionResult, buildSquad, CAMPAIGN_VERSION, createCampaign, executeCampaignCommand, medicineCures, migrateCampaign, type CampaignState } from '../src/campaign';
import { HORDE, hordePoints, NOISE, TEMPLATES } from '../src/content';
import { decorCode, decorOf } from '../src/decor';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { broughtBack, rollLoot, roomAt, searchable, searchableTiles, SEARCHABLE } from '../src/loot';
import { lootSummary } from '../src/lore';
import { canSee } from '../src/los';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import { createMission, hordeWave, MISSION_NAMES } from '../src/setup';
import type { GameState, Loot, Pod, Slot, Unit } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// Phase 2 of the zombies (docs/FASE2.md): noise, the dead, searching and the Saqueo.

const field = (w = 20, h = 20) => stateFrom(Array.from({ length: h }, () => '.'.repeat(w)));
const sim = (s: GameState, seed = 1) => new Sim(s, new Rng(seed), [0]);
const ctx = (seed = 1) => ({ slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(seed) });

function pod(s: GameState, id: string, active: boolean, units: Unit[]): Pod {
  const p: Pod = { id, active, unitIds: units.map((u) => u.id), patrol: [], patrolIndex: 0 };
  for (const u of units) u.podId = id;
  s.pods.push(p);
  return p;
}

const of = <T extends GameEvent['t']>(events: GameEvent[], t: T) => events.filter((e): e is Extract<GameEvent, { t: T }> => e.t === t);

describe('noise', () => {
  it('every weapon and action is heard as far as the table says', () => {
    const s = field();
    const kit = (template: Parameters<typeof addUnit>[2]) => addUnit(s, `s${s.units.length}`, template, 0, 0);
    expect(noiseOf(s, kit('sharpshooter'), 'pistol')).toBe(8);
    expect(noiseOf(s, kit('assault'), 'shoot')).toBe(10);
    expect(noiseOf(s, kit('rookie'), 'shoot')).toBe(12);
    expect(noiseOf(s, kit('grenadier'), 'shoot')).toBe(14);
    expect(noiseOf(s, kit('sharpshooter'), 'shoot')).toBe(16);
    expect(noiseOf(s, kit('rookie'), 'grenade')).toBe(18);
    expect(noiseOf(s, kit('rookie'), 'search')).toBe(2);
    expect(noiseOf(s, kit('assault'), 'slash')).toBe(1);
    expect(noiseOf(s, kit('sharpshooter'), 'spectralArrow')).toBe(1);
    expect(noiseOf(s, kit('rookie'), 'overwatch')).toBe(0);
    expect(noiseOf(s, addUnit(s, 'a1', 'sectoid', 0, 0), 'mindspin')).toBe(16);
    expect(NOISE).toEqual({ door: 1, breach: 6, dash: 3, alarm: 16 });
  });

  it('those in earshot that see nobody go to look; the busy, the deaf and the maker’s own pod do not', () => {
    const s = field(30, 30);
    const soldier = addUnit(s, 's1', 'rookie', 2, 2);
    const busy = addUnit(s, 'a1', 'zombie', 2, 8);
    const idle = addUnit(s, 'a2', 'zombie', 12, 2);
    const deaf = addUnit(s, 'a3', 'zombie', 2, 20);
    pod(s, 'p1', true, [busy]);
    pod(s, 'p2', false, [idle]);
    pod(s, 'p3', false, [deaf]);
    const run = sim(s);
    run.makeNoise(soldier.pos, 12, soldier);
    expect(of(run.events, 'noise')).toEqual([{ t: 'noise', tile: { x: 2, y: 2, z: 0 }, radius: 12, by: 's1', heard: ['a2'] }]);
    expect(idle.heard).toEqual({ x: 2, y: 2, z: 0 });
    expect(busy.heard).toBeUndefined();
    expect(deaf.heard).toBeUndefined();
    expect(s.pods.find((p) => p.id === 'p2')!.active).toBe(false);
    // The squad's noise winds up the horde's clock.
    expect(s.mission.noise).toBe(hordePoints(12));
    // A zombie's own pod knows what the noise was.
    const mate = addUnit(s, 'a4', 'zombie', 13, 2);
    s.pods.find((p) => p.id === 'p2')!.unitIds.push('a4');
    mate.podId = 'p2';
    const again = sim(s);
    again.makeNoise(idle.pos, 6, idle);
    expect(of(again.events, 'noise')[0]!.heard).toEqual([]);
    expect(s.mission.noise).toBe(hordePoints(12));
  });

  it('noise never gives the squad away: a pod drawn by a dash wakes only when it sees someone', () => {
    // A wall between the street and the yard, open at the far end.
    const s = stateFrom(['....................', '....................', '###################.', '....................']);
    s.concealed = true;
    const runner = addUnit(s, 's1', 'rookie', 0, 1);
    // A partner still to act: the squad's turn goes on.
    addUnit(s, 's2', 'rookie', 0, 0);
    const z1 = addUnit(s, 'a1', 'zombie', 13, 3);
    const z2 = addUnit(s, 'a2', 'zombie', 12, 3);
    pod(s, 'p1', false, [z1, z2]);
    const events = executeCommand(s, { type: 'move', unit: 's1', to: { x: 13, y: 1 } }, ctx());
    expect(events.ok).toBe(true);
    if (!events.ok) return;
    expect(runner.ap).toBe(0);
    expect(of(events.events, 'noise')).toEqual([{ t: 'noise', tile: { x: 13, y: 1, z: 0 }, radius: NOISE.dash, by: 's1', heard: ['a1', 'a2'] }]);
    expect(s.concealed).toBe(true);
    expect(s.pods[0]!.active).toBe(false);
    // On their turn they come round the end of the wall, see the runner and only then wake up.
    const turn = sim(s, 2);
    turn.runAlienTurn();
    expect(z1.pos.x).toBeGreaterThan(13);
    const woke = of(turn.events, 'podActivated')[0];
    expect(woke).toMatchObject({ pod: 'p1', cause: 'patrol' });
    expect(of(turn.events, 'concealmentBroken')[0]).toMatchObject({ reason: 'patrol' });
  });

  it('opening a door is quiet; going through a window is heard', () => {
    const s = stateFrom(['..........', '....+=....', '..........']);
    addUnit(s, 's1', 'rookie', 4, 0);
    addUnit(s, 's2', 'rookie', 5, 0);
    const z = addUnit(s, 'a1', 'zombie', 9, 2);
    pod(s, 'p1', false, [z]);
    const r = executeCommand(s, { type: 'move', unit: 's1', to: { x: 4, y: 2 } }, ctx());
    expect(r.ok && of(r.events, 'noise')).toEqual([{ t: 'noise', tile: { x: 4, y: 1, z: 0 }, radius: NOISE.door, by: 's1', heard: [] }]);
    const w = executeCommand(s, { type: 'move', unit: 's2', to: { x: 5, y: 2 } }, ctx());
    expect(w.ok && of(w.events, 'noise')[0]).toMatchObject({ tile: { x: 5, y: 1 }, radius: NOISE.breach });
  });

  it('a pod that finds nobody where it heard something forgets it and stays unaware', () => {
    const s = stateFrom(['....#.........', '....#.........', '....#.........']);
    s.concealed = true;
    addUnit(s, 's1', 'rookie', 0, 1);
    const z1 = addUnit(s, 'a1', 'zombie', 13, 1, { heard: { x: 6, y: 1, z: 0 } });
    const z2 = addUnit(s, 'a2', 'zombie', 13, 2, { heard: { x: 6, y: 1, z: 0 } });
    pod(s, 'p1', false, [z1, z2]);
    const events: GameEvent[] = [];
    for (let i = 0; i < 4 && z1.heard; i++) {
      const turn = sim(s, i);
      turn.runAlienTurn();
      events.push(...turn.events);
    }
    expect(z1.pos.x).toBeLessThanOrEqual(7);
    expect(of(events, 'forgot').map((e) => e.unit).sort()).toEqual(['a1', 'a2']);
    expect(z1.heard).toBeNull();
    expect(s.pods[0]!.active).toBe(false);
    expect(s.concealed).toBe(true);
  });
});

describe('the dead', () => {
  it('the zombie templates fight with their bodies: no guns, no cover', () => {
    for (const id of ['zombie', 'xenoid', 'mec', 'sectoid', 'bloater'] as const) {
      const t = TEMPLATES[id];
      expect(t.undead).toBe(true);
      expect(t.abilities).not.toContain('shoot');
      expect(t.abilities).not.toContain('overwatch');
    }
    expect(TEMPLATES.sectoid.abilities).toEqual(['mindspin', 'reanimate']);
    expect(TEMPLATES.xenoid.mobility).toBeGreaterThan(TEMPLATES.zombie.mobility);
    expect(TEMPLATES.mec.armor).toBe(1);
    expect(TEMPLATES.bloater).toMatchObject({ name: 'Hinchado', short: 'HIN', abilities: ['burst'] });
    expect(TEMPLATES.trooper.undead).toBe(false);
  });

  it('a zombie bites the nearest soldier in reach, and otherwise lurches toward the nearest it sees', () => {
    const s = field(30, 30);
    s.activeTeam = 'alien';
    const near = addUnit(s, 's1', 'rookie', 5, 5, { hp: 50, maxHp: 50 });
    addUnit(s, 's2', 'rookie', 5, 12, { hp: 50, maxHp: 50 });
    const biter = addUnit(s, 'a1', 'zombie', 8, 5);
    const walker = addUnit(s, 'a2', 'zombie', 18, 5);
    pod(s, 'p1', true, [biter, walker]);
    const turn = sim(s);
    turn.runAlienTurn();
    expect(of(turn.events, 'shot').filter((e) => e.unit === 'a1')).toEqual([expect.objectContaining({ ability: 'claw', target: 's1' })]);
    // Ten tiles off: two actions of walking (4 tiles each), straight at it, no shot.
    expect(of(turn.events, 'shot').some((e) => e.unit === 'a2')).toBe(false);
    expect(Math.hypot(walker.pos.x - near.pos.x, walker.pos.y - near.pos.y)).toBeLessThanOrEqual(13 - 8 + 0.5);
  });

  it('whoever goes for the squad acts first; then the dead with nobody in reach walk, all together (playtest 2026-10-16)', () => {
    const s = field(40, 30);
    s.activeTeam = 'alien';
    addUnit(s, 's1', 'rookie', 5, 5, { hp: 50, maxHp: 50 });
    const biter = addUnit(s, 'a1', 'zombie', 8, 5);
    const far1 = addUnit(s, 'a2', 'zombie', 30, 5);
    const far2 = addUnit(s, 'a3', 'zombie', 31, 6);
    pod(s, 'p1', true, [biter, far1, far2]);
    const turn = sim(s);
    turn.runAlienTurn();
    const acts = turn.events.filter((e): e is Extract<GameEvent, { t: 'moved' | 'shot' }> => e.t === 'moved' || e.t === 'shot').map((e) => e.unit);
    // Everything of the one that bites comes before the far ones' walks.
    const firstWalker = acts.findIndex((id) => id !== 'a1');
    expect(firstWalker).toBeGreaterThan(0);
    expect(acts.slice(0, firstWalker).every((id) => id === 'a1')).toBe(true);
    expect(new Set(acts.slice(firstWalker))).toEqual(new Set(['a2', 'a3']));
    expect(of(turn.events, 'shot').filter((e) => e.unit === 'a1')).toEqual([expect.objectContaining({ ability: 'claw', target: 's1' })]);
  });

  it('a zombie bites only whoever it sees itself, not whoever another of the dead sees (Arturo, 16/10)', () => {
    // Another house: a wall between the soldier and the zombie, with a way round it within one action.
    const s = stateFrom(['....................', '....................', '....................', '....................', '###.................', '....................', '....................']);
    s.activeTeam = 'alien';
    addUnit(s, 's1', 'rookie', 1, 2, { hp: 50, maxHp: 50 });
    const watcher = addUnit(s, 'a1', 'zombie', 9, 2);
    // It could reach the soldier round the wall in one action (four steps), but doesn't see it.
    const behind = addUnit(s, 'a2', 'zombie', 2, 5, { heard: { x: 12, y: 6, z: 0 } });
    pod(s, 'p1', true, [watcher, behind]);
    expect(canSee(s, watcher, s.units[0]!)).toBe(true);
    expect(canSee(s, behind, s.units[0]!)).toBe(false);
    const turn = sim(s);
    turn.runAlienTurn();
    expect(of(turn.events, 'shot').some((e) => e.unit === 'a2')).toBe(false);
  });

  it('the dead see in a cone ahead of them, and whoever is right beside them (Arturo, 17/10)', () => {
    const s = field(20, 20);
    const soldier = addUnit(s, 's1', 'rookie', 10, 10);
    const ahead = addUnit(s, 'a1', 'zombie', 4, 10, { facing: 0 });
    const behind = addUnit(s, 'a2', 'zombie', 16, 10, { facing: 0 });
    const side = addUnit(s, 'a3', 'zombie', 10, 4, { facing: 0 });
    const beside = addUnit(s, 'a4', 'zombie', 11, 10, { facing: 0 });
    expect(canSee(s, ahead, soldier)).toBe(true);
    expect(canSee(s, behind, soldier)).toBe(false);
    // Straight down from it is 90° off where it faces: outside the 120° cone.
    expect(canSee(s, side, soldier)).toBe(false);
    expect(canSee(s, beside, soldier)).toBe(true);
    // The living look all round.
    expect(canSee(s, addUnit(s, 'h1', 'trooper', 16, 11, { facing: 0 }), soldier)).toBe(true);
  });

  it('the dead turn to what they hear, and look the way they walk or strike', () => {
    const s = field(20, 20);
    const z = addUnit(s, 'a1', 'zombie', 10, 10, { facing: 0 });
    applyEvent(s, { t: 'noise', tile: { x: 10, y: 2, z: 0 }, radius: 12, heard: ['a1'] });
    expect(z.facing).toBeCloseTo(-Math.PI / 2);
    applyEvent(s, { t: 'moved', unit: 'a1', path: [{ x: 9, y: 10, z: 0 }, { x: 8, y: 11, z: 0 }], ap: 1 });
    expect(z.facing).toBeCloseTo((3 * Math.PI) / 4);
    addUnit(s, 's1', 'rookie', 9, 11);
    applyEvent(s, { t: 'shot', unit: 'a1', target: 's1', ability: 'claw', from: z.pos, result: 'miss', chance: 50, roll: 90, reaction: false });
    expect(z.facing).toBeCloseTo(0);
    // Out of a mission's making, every one of the dead looks some way.
    const m = createMission({ map: { kind: 'generated', biome: 'city', seed: 3 }, slots: [0], rng: new Rng(3) });
    expect(m.units.filter((u) => TEMPLATES[u.template].undead).every((u) => u.facing !== undefined)).toBe(true);
  });

  it('a pod that faces away from a soldier walking past does not notice him', () => {
    const run = (facing: number) => {
      const s = field(20, 20);
      const z = addUnit(s, 'a1', 'zombie', 10, 10, { facing });
      pod(s, 'p1', false, [z]);
      const soldier = addUnit(s, 's1', 'rookie', 2, 12, { owner: 0 });
      const sim = new Sim(s, new Rng(1), [0]);
      sim.moveAlong(soldier, [{ x: 3, y: 12, z: 0 }, { x: 4, y: 12, z: 0 }, { x: 5, y: 12, z: 0 }], 1);
      return s.pods[0]!.active;
    };
    expect(run(Math.PI)).toBe(true);
    expect(run(0)).toBe(false);
  });

  it('seeing nobody, a zombie goes to the last noise it heard; with nothing heard, it wanders', () => {
    const s = stateFrom(['...............', '.#############.', '...............']);
    s.activeTeam = 'alien';
    addUnit(s, 's1', 'rookie', 0, 0);
    const z = addUnit(s, 'a1', 'zombie', 14, 2, { heard: { x: 2, y: 2, z: 0 } });
    pod(s, 'p1', true, [z]);
    sim(s).runAlienTurn();
    expect(z.pos).toEqual({ x: 6, y: 2, z: 0 });
  });

  it('the living keep their ways, but seeing nobody they too go where they heard something', () => {
    const street = () => {
      const s = stateFrom(['.....#..............', '.....#..............', '.....#..............']);
      s.activeTeam = 'alien';
      addUnit(s, 's1', 'rookie', 0, 2);
      return s;
    };
    // Without a noise, a Saqueador hunts the squad as ever...
    const s = street();
    const hunter = addUnit(s, 'a1', 'trooper', 10, 2);
    pod(s, 'p1', true, [hunter]);
    sim(s).runAlienTurn();
    expect(hunter.pos.x).toBeLessThan(10);
    // ...with one, and nobody in sight, it goes to look.
    const t = street();
    const raider = addUnit(t, 'a1', 'trooper', 10, 2, { heard: { x: 19, y: 0, z: 0 } });
    pod(t, 'p1', true, [raider]);
    sim(t).runAlienTurn();
    expect(raider.pos.x).toBeGreaterThan(10);
  });

  it('a Corredor covers twice the ground of a zombie: nine tiles off, a step and a bite; the zombie only gets there', () => {
    const turn = (template: 'xenoid' | 'zombie') => {
      const s = stateFrom(['................................']);
      s.activeTeam = 'alien';
      addUnit(s, 's1', 'rookie', 0, 0, { hp: 50, maxHp: 50 });
      pod(s, 'p1', true, [addUnit(s, 'a1', template, 9, 0)]);
      const t = sim(s);
      t.runAlienTurn();
      return of(t.events, 'shot');
    };
    expect(turn('xenoid')).toEqual([expect.objectContaining({ unit: 'a1', ability: 'claw', target: 's1' })]);
    expect(turn('zombie')).toEqual([]);
  });

  it('the dead have two actions (playtest 16/10 #21): two bites, or a step and a bite; a bite needs the soldier beside it', () => {
    const s = field(12, 7);
    s.activeTeam = 'alien';
    addUnit(s, 's1', 'rookie', 2, 3, { hp: 50, maxHp: 50 });
    const beside = addUnit(s, 'a1', 'zombie', 3, 3);
    const near = addUnit(s, 'a2', 'zombie', 6, 5);
    pod(s, 'p1', true, [beside, near]);
    const t = sim(s);
    t.runAlienTurn();
    const bites = of(t.events, 'shot');
    expect(bites.filter((e) => e.unit === 'a1')).toHaveLength(2);
    expect(bites.filter((e) => e.unit === 'a2')).toHaveLength(1);
    expect(TEMPLATES.zombie).toMatchObject({ hp: 3, mobility: TEMPLATES.rookie.mobility * 0.6 });
  });

  it('a horde of twenty plays its turn quickly', () => {
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 11 }, slots: [0], rng: new Rng(1), kind: 'scavenge', difficulty: 4 });
    const soldiers = s.units.filter((u) => u.team === 'xcom');
    const horde: Unit[] = [];
    for (let y = 0; y < s.height && horde.length < 20; y += 2) {
      for (let x = 0; x < s.width && horde.length < 20; x += 3) {
        if (s.tiles[y * s.width + x] !== 'floor' || s.units.some((u) => u.pos.x === x && u.pos.y === y)) continue;
        if (soldiers.some((o) => Math.hypot(o.pos.x - x, o.pos.y - y) < 10)) continue;
        horde.push(addUnit(s, `h${horde.length}`, horde.length % 5 === 0 ? 'xenoid' : 'zombie', x, y, { heard: { ...soldiers[0]!.pos } }));
      }
    }
    pod(s, 'ph', true, horde);
    s.concealed = false;
    const t0 = Date.now();
    for (let i = 0; i < 3 && !s.outcome; i++) {
      const turn = sim(s, i);
      turn.runAlienTurn();
      for (const u of s.units) if (u.team === 'xcom') u.ap = 2;
    }
    expect(Date.now() - t0).toBeLessThan(3000);
  });
});

describe('the Hinchado', () => {
  it('bursts when it dies: 3 to everyone beside it, scenery untouched', () => {
    const s = field();
    const shooter = addUnit(s, 's1', 'rookie', 2, 10);
    const bloater = addUnit(s, 'a1', 'bloater', 8, 10, { hp: 1 });
    const beside = addUnit(s, 'a2', 'zombie', 9, 11);
    const soldier = addUnit(s, 's2', 'rookie', 7, 10, { hp: 10, maxHp: 10 });
    const apart = addUnit(s, 'a3', 'zombie', 10, 10);
    s.tiles[9 * 20 + 8] = 'low';
    pod(s, 'p1', true, [bloater, beside, apart]);
    const run = sim(s);
    run.damage(bloater, 5, 0, shooter);
    expect(bloater.alive).toBe(false);
    expect(of(run.events, 'explosive')).toEqual([{ t: 'explosive', unit: 'a1', ability: 'burst', target: { x: 8, y: 10, z: 0 } }]);
    expect(soldier.hp).toBe(7);
    expect(beside.hp).toBe(TEMPLATES.zombie.hp - 3);
    expect(apart.hp).toBe(TEMPLATES.zombie.hp);
    expect(s.tiles[9 * 20 + 8]).toBe('low');
    expect(of(run.events, 'noise')[0]).toMatchObject({ tile: { x: 8, y: 10 }, radius: 6 });
  });

  it('one burst sets off the Hinchado beside it', () => {
    const s = field();
    const a = addUnit(s, 'a1', 'bloater', 8, 10, { hp: 1 });
    const b = addUnit(s, 'a2', 'bloater', 9, 10, { hp: 3 });
    addUnit(s, 's1', 'rookie', 0, 0);
    const run = sim(s);
    run.damage(a, 1, 0);
    expect(b.alive).toBe(false);
    expect(of(run.events, 'explosive').map((e) => e.unit)).toEqual(['a1', 'a2']);
  });

  it('walks up to a soldier and bursts beside it', () => {
    const s = field();
    s.activeTeam = 'alien';
    const soldier = addUnit(s, 's1', 'rookie', 5, 5, { hp: 10, maxHp: 10 });
    const bloater = addUnit(s, 'a1', 'bloater', 9, 5);
    pod(s, 'p1', true, [bloater]);
    const turn = sim(s);
    turn.runAlienTurn();
    expect(bloater.alive).toBe(false);
    expect(of(turn.events, 'abilityUsed')).toEqual([expect.objectContaining({ unit: 'a1', ability: 'burst' })]);
    expect(soldier.hp).toBe(7);
  });
});

describe('the Chillón', () => {
  it('screams at a soldier it sees, and the zombies out of sight come to the scream without waking', () => {
    const s = stateFrom([
      '..............................',
      '..............................',
      '..............................',
      '#######################.......',
      '..............................',
    ]);
    s.activeTeam = 'alien';
    const soldier = addUnit(s, 's1', 'rookie', 2, 0, { will: 0 });
    const chillon = addUnit(s, 'a1', 'sectoid', 12, 2);
    pod(s, 'p1', true, [chillon]);
    const z = addUnit(s, 'a2', 'zombie', 24, 4);
    pod(s, 'p2', false, [z]);
    const turn = sim(s);
    turn.runAlienTurn();
    expect(of(turn.events, 'psi')[0]).toMatchObject({ unit: 'a1', target: 's1' });
    expect(of(turn.events, 'psi')[0]!.effect).not.toBe('controlled');
    expect(of(turn.events, 'noise')[0]).toMatchObject({ tile: { x: 12, y: 2 }, radius: 16, heard: ['a2'] });
    // The zombie went to look the same turn.
    expect(Math.hypot(z.pos.x - 12, z.pos.y - 2)).toBeLessThan(Math.hypot(24 - 12, 4 - 2));
    expect(soldier.team).toBe('xcom');
  });

  it('does not bite: it keeps its distance from those it sees', () => {
    const s = field(30, 10);
    s.activeTeam = 'alien';
    addUnit(s, 's1', 'rookie', 5, 5, { will: 0 });
    const chillon = addUnit(s, 'a1', 'sectoid', 7, 5, { cooldowns: { mindspin: 2, reanimate: 2 } });
    pod(s, 'p1', true, [chillon]);
    const turn = sim(s);
    turn.runAlienTurn();
    expect(of(turn.events, 'shot')).toEqual([]);
    expect(chillon.pos.x - 5).toBeGreaterThanOrEqual(5);
  });
});

describe('searching', () => {
  /** A medicine box at (3,1) against a wall line, a car outside (docs/FASE8.md §B). */
  function house() {
    const s = stateFrom(['........', '...H....', '..##....', '........', '.....h..']);
    s.decor = new Array(s.width * s.height).fill(0);
    s.decor[1 * 8 + 3] = decorCode('lootMedical');
    s.decor[4 * 8 + 5] = decorCode('carRear');
    return s;
  }

  it('one action beside a loot box: always what it is marked for, and it makes a little noise', () => {
    const s = house();
    const u = addUnit(s, 's1', 'rookie', 2, 1);
    const z = addUnit(s, 'a1', 'zombie', 4, 2);
    pod(s, 'p1', false, [z]);
    expect(searchableTiles(s, u)).toEqual([{ x: 3, y: 1, z: 0 }]);
    expect(canTargetTile(s, u, 'search', { x: 3, y: 1, z: 0 })).toBe(true);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'search', tile: { x: 3, y: 1 } }, ctx(3));
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    const found = of(r.events, 'searched')[0]!;
    expect(found).toMatchObject({ unit: 's1', tile: { x: 3, y: 1 } });
    expect(found.loot?.medicines).toBeGreaterThanOrEqual(1);
    expect(u.backpack).toEqual([found.loot]);
    expect(of(r.events, 'noise')[0]).toMatchObject({ tile: { x: 3, y: 1 }, radius: 2, heard: ['a1'] });
    expect(u.ap).toBe(1);
    expect(s.searched).toEqual([1 * 8 + 3]);
    expect(searchable(s, { x: 3, y: 1, z: 0 })).toBe(false);
    expect(abilityBlocker(s, u, 'search')).toBe('Ya está abierta.');
  });

  it('a full backpack leaves the find on the floor; the box at hand, no wall in between; furniture and cars are no longer searched', () => {
    const s = house();
    const u = addUnit(s, 's1', 'rookie', 2, 1, { backpack: [{ supplies: 5 }, { medicines: 1 }, { item: 'firstAid' }] });
    expect(abilityBlocker(s, u, 'search')).toBeNull();
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'search', tile: { x: 3, y: 1 } }, ctx(3));
    expect(r.ok).toBe(true);
    expect(u.backpack).toHaveLength(3);
    expect(s.dropped).toEqual([{ id: 'l1', pos: { x: 2, y: 1, z: 0 }, loot: expect.objectContaining({ medicines: expect.any(Number) }) }]);
    s.searched = [];
    u.pos = { x: 0, y: 0, z: 0 };
    expect(abilityBlocker(s, u, 'search')).toBe('No hay ninguna caja al lado.');
    // Diagonally round the corner of a wall: out of reach.
    u.pos = { x: 4, y: 2, z: 0 };
    expect(searchableTiles(s, u)).toEqual([{ x: 3, y: 1, z: 0 }]);
    s.tiles[2 * 8 + 4] = 'floor';
    s.tiles[1 * 8 + 4] = 'wall';
    s.tiles[2 * 8 + 3] = 'wall';
    expect(searchableTiles(s, u)).toEqual([]);
    // A car's boot is not searched any more.
    u.pos = { x: 4, y: 4, z: 0 };
    expect(searchableTiles(s, u)).toEqual([]);
    expect(SEARCHABLE).toEqual(expect.arrayContaining(['wardrobe', 'table', 'desk', 'shelf', 'counter', 'cabinet', 'bed', 'crates', 'rack']));
  });

  it('every map has its special boxes: up to 3, each of a different kind, away from the start (docs/FASE8.md §B)', () => {
    const kinds = ['lootWeapons', 'lootMedical', 'lootAmmo', 'lootFood'] as const;
    for (const biome of ['city', 'wilds', 'facility'] as const) {
      for (let seed = 1; seed <= 4; seed++) {
        const s = createMission({ map: { kind: 'generated', biome, seed }, slots: [0], rng: new Rng(1), kind: 'scavenge' });
        const boxes = s.decor!.flatMap((d, i) => {
          const k = decorOf(d)?.kind;
          return k && (kinds as readonly string[]).includes(k) ? [{ k, x: i % s.width, y: Math.floor(i / s.width) }] : [];
        });
        expect(boxes.length, `${biome} ${seed}`).toBeGreaterThanOrEqual(1);
        expect(boxes.length).toBeLessThanOrEqual(3);
        expect(new Set(boxes.map((b) => b.k)).size).toBe(boxes.length);
        const squad = s.units.filter((u) => u.team === 'xcom');
        for (const b of boxes) expect(Math.min(...squad.map((u) => Math.hypot(b.x - u.pos.x, b.y - u.pos.y)))).toBeGreaterThanOrEqual(10);
      }
    }
  });

  it('a room is searched once for the whole squad, from inside it; its furniture says what kind of place it is', () => {
    // Two rooms (an office with a desk, and a home with a bed) parted by a door, the street outside.
    const s = stateFrom(['..........', '.########.', '.#.H#..h#.', '.#..+...#.', '.########.', '..........']);
    s.decor = new Array(s.width * s.height).fill(0);
    s.decor[2 * 10 + 3] = decorCode('desk');
    s.decor[2 * 10 + 7] = decorCode('bed');
    s.ground = new Array(s.width * s.height).fill(0);
    for (const [x, y] of [[2, 2], [2, 3], [3, 3], [5, 2], [6, 2], [5, 3], [6, 3], [7, 3]]) s.ground[y! * 10 + x!] = 2;
    const office = roomAt(s, { x: 2, y: 2, z: 0 })!;
    const home = roomAt(s, { x: 6, y: 3, z: 0 })!;
    expect(office.spot).toBe('office');
    expect(home.spot).toBe('home');
    expect(office.id).not.toBe(home.id);
    expect(roomAt(s, { x: 4, y: 3, z: 0 })).toBeNull();
    expect(roomAt(s, { x: 0, y: 0, z: 0 })).toBeNull();
    const a = addUnit(s, 's1', 'rookie', 3, 3);
    const b = addUnit(s, 's2', 'rookie', 2, 2);
    expect(abilityBlocker(s, a, 'searchRoom')).toBeNull();
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'searchRoom' }, ctx(5));
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(of(r.events, 'roomSearched')[0]).toMatchObject({ unit: 's1', room: office.id });
    expect(s.roomsSearched).toEqual([office.id]);
    expect(abilityBlocker(s, b, 'searchRoom')).toBe('Esta habitación ya está registrada.');
    b.pos = { x: 6, y: 3, z: 0 };
    expect(abilityBlocker(s, b, 'searchRoom')).toBeNull();
    b.pos = { x: 0, y: 0, z: 0 };
    expect(abilityBlocker(s, b, 'searchRoom')).toBe('Solo dentro de una habitación.');
  });

  it('the loot comes from a seeded table: suministros (5–20), an accessory, medicines or nothing', () => {
    const roll = (seed: number, difficulty = 2) => rollLoot('carRear', difficulty, new Rng(seed));
    for (let seed = 1; seed < 20; seed++) expect(roll(seed)).toEqual(roll(seed));
    const kinds = new Set<string>();
    for (let seed = 1; seed < 400; seed++) {
      for (const difficulty of [1, 4]) {
        const loot = roll(seed, difficulty);
        kinds.add(loot ? Object.keys(loot)[0]! : 'nothing');
        if (loot?.supplies) {
          expect(loot.supplies).toBeGreaterThanOrEqual(5);
          expect(loot.supplies).toBeLessThanOrEqual(20);
        }
        // Only what the difficulty opens: no armour-piercing rounds in an easy town.
        if (difficulty === 1) expect(loot?.item).not.toBe('apRounds');
      }
    }
    // Since phase 4, rations, parts and now and then a good weapon too (docs/FASE4.md); since Fase 9, an improvement for one.
    expect([...kinds].sort()).toEqual(['food', 'item', 'medicines', 'mod', 'nothing', 'parts', 'supplies', 'weapon']);
    expect(rollLoot('tree', 2, new Rng(1))).toBeNull();
  });
});

describe('the Saqueo', () => {
  it('a town with plenty to search, an evac and no timer; the dead scattered about, the horde to come', () => {
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 5 }, slots: [0, 1], rng: new Rng(1), kind: 'scavenge', difficulty: 2 });
    expect(MISSION_NAMES.scavenge).toBe('Saqueo');
    expect(s.mission).toMatchObject({ kind: 'scavenge', turnsLeft: null, item: null, difficulty: 2, reinforcements: [] });
    expect(s.mission.evacZone.length).toBeGreaterThan(0);
    expect(s.mission.horde).toEqual(hordeWave(2));
    expect(s.units.filter((u) => u.team === 'alien').every((u) => TEMPLATES[u.template].undead)).toBe(true);
    // Rooms to search (docs/FASE8.md §B), and a box or so.
    const rooms = new Set<number>();
    for (let i = 0; i < s.tiles.length; i++) {
      const room = roomAt(s, { x: i % s.width, y: Math.floor(i / s.width), z: 0 });
      if (room) rooms.add(room.id);
    }
    expect(rooms.size).toBeGreaterThan(20);
  });

  it('is won by getting somebody out; what they carry counts, a fallen soldier’s is lost', () => {
    const s = field();
    s.mission.kind = 'scavenge';
    s.mission.evacZone = [{ x: 0, y: 0 }];
    const out = addUnit(s, 's1', 'rookie', 0, 0, { backpack: [{ supplies: 12 }, { item: 'firstAid' }] });
    const fallen = addUnit(s, 's2', 'rookie', 5, 5, { backpack: [{ supplies: 20 }, { medicines: 1 }] });
    addUnit(s, 'a1', 'zombie', 15, 15);
    const run = sim(s);
    // The bus leaves with s1 in it; s2, out in the street, is lost with what it carries (docs/PRUEBA-2026-10-14.md P1).
    run.callEvac(0, out);
    expect(s.outcome).toBe('victory');
    expect(fallen.lost).toBe(true);
    expect(broughtBack(s)).toMatchObject({ supplies: 12, items: ['firstAid'], medicines: 0 });

    // Everyone down: lost, and nothing comes home.
    const t = field();
    t.mission.kind = 'scavenge';
    t.mission.evacZone = [{ x: 0, y: 0 }];
    const last = addUnit(t, 's1', 'rookie', 5, 5, { backpack: [{ supplies: 9 }] });
    addUnit(t, 'a1', 'zombie', 15, 15);
    const lose = sim(t);
    lose.damage(last, 99, 0);
    lose.checkOutcome();
    expect(t.outcome).toBe('defeat');
    expect(broughtBack(t)).toEqual({ supplies: 0, items: [], medicines: 0, food: 0, parts: 0, weapons: [] });
  });

  it('the standing squad brings its loot home from any mission it wins', () => {
    const s = field();
    const u = addUnit(s, 's1', 'rookie', 0, 0, { backpack: [{ supplies: 7 }, { medicines: 1 }] });
    const a = addUnit(s, 'a1', 'zombie', 3, 3, { hp: 1 });
    const win = sim(s);
    win.damage(a, 5, 0, u);
    win.checkOutcome();
    expect(s.outcome).toBe('victory');
    expect(broughtBack(s)).toMatchObject({ supplies: 7, items: [], medicines: 1 });
  });

  it('the horde comes in by the edges every few turns, sooner the more noise, on the prowl', () => {
    const setup = () => {
      const s = field(40, 40);
      s.mission.kind = 'scavenge';
      s.mission.evacZone = [{ x: 39, y: 39 }];
      s.mission.horde = ['zombie', 'zombie'];
      s.concealed = true;
      addUnit(s, 's1', 'rookie', 20, 20);
      return s;
    };
    const turns = (s: GameState, n: number) => {
      const events: GameEvent[] = [];
      for (let i = 0; i < n; i++) {
        const turn = sim(s, i + 1);
        turn.runAlienTurn();
        events.push(...turn.events);
      }
      return events;
    };
    // Quiet: the first wave is announced once the turns alone fill the clock (the fifth), to land a turn later.
    const quiet = setup();
    const first = HORDE.wave / HORDE.perTurn;
    // (The few that come every turn, playtest 2026-10-15 #11, apart.)
    const waves = (events: GameEvent[]) => of(events, 'reinforcementsIncoming').filter((e) => !e.r.trickle);
    const announced = waves(turns(quiet, first));
    expect(announced.map((e) => e.r.turn)).toEqual([first + 1]);
    const r = announced[0]!.r;
    expect(r.horde).toBe(true);
    // No exact spot is ever announced (playtest 2026-10-13).
    expect(r.pos).toBeNull();
    // It lands unaware by an edge, far from the squad, heading for it: the squad stays hidden until they see it.
    const landing = turns(quiet, 1);
    const spawned = of(landing, 'spawned').find((e) => e.units.length === r.units.length)!;
    for (const u of spawned.units) {
      expect(u.pos.x < 2 || u.pos.y < 2 || u.pos.x >= 38 || u.pos.y >= 38).toBe(true);
      expect(Math.hypot(u.pos.x - 20, u.pos.y - 20)).toBeGreaterThanOrEqual(12);
    }
    expect(spawned.pod.active).toBe(false);
    expect(spawned.units.every((u) => u.heard && u.heard.x === 20 && u.heard.y === 20)).toBe(true);
    // (The few that came before it, every turn, may have found the squad by now: the wave itself doesn't.)
    expect(of(landing, 'concealmentBroken').filter((e) => spawned.units.some((u) => u.id === e.by))).toEqual([]);
    // Noisy: a firefight's worth of noise brings it on the first turn.
    const loud = setup();
    loud.mission.noise = HORDE.wave - HORDE.perTurn;
    expect(waves(turns(loud, 1)).map((e) => e.r.turn)).toEqual([2]);
  });

  it('a few of the dead come in every turn on any mission left by bus, twice as many while a sabotage alarm sounds (playtest 2026-10-15, #11, #14)', () => {
    const trickles = (kind: 'scavenge' | 'sabotage' | 'elimination', difficulty: number) => {
      const s = field(40, 40);
      s.mission.evacZone = [{ x: 39, y: 39 }];
      s.mission.horde = ['zombie', 'zombie'];
      addUnit(s, 's1', 'rookie', 20, 20);
      s.mission.kind = kind;
      s.mission.difficulty = difficulty;
      if (kind === 'elimination') delete s.mission.horde;
      const t = sim(s, 1);
      t.runAlienTurn();
      return of(t.events, 'reinforcementsIncoming').filter((e) => e.r.trickle).map((e) => e.r.units.length);
    };
    expect(trickles('scavenge', 1)).toEqual([HORDE.trickle[1]]);
    expect(trickles('scavenge', 4)).toEqual([HORDE.trickle[4]]);
    expect(trickles('sabotage', 2)).toEqual([2 * HORDE.trickle[2]]);
    // A Limpieza ends when they're all down: nobody keeps coming.
    expect(trickles('elimination', 3)).toEqual([]);
  });
});

describe('loot in the campaign', () => {
  function flown(kind: 'scavenge' | 'elimination') {
    const c = createCampaign([0, 1], new Rng(2));
    const offer = c.offers[0]!;
    offer.kind = kind;
    const r = executeCampaignCommand(c, { type: 'chooseMission', id: offer.id }, { slot: 0, connected: [0], rng: new Rng(7) });
    if (!r.ok) throw new Error(r.error);
    const mission = createMission({ map: offer.map, slots: [0, 1], rng: new Rng(3), kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    return { c, offer, mission };
  }

  it('suministros go to the credits, accessories to the inventory, medicines to their counter; the report says it', () => {
    const { c, offer, mission } = flown('scavenge');
    const [a, b, d] = mission.units.filter((u) => u.team === 'xcom');
    const carried: Loot[] = [{ supplies: 12 }, { item: 'smokeGrenade' }, { medicines: 1 }];
    Object.assign(a!, { alive: false, evacuated: true, backpack: carried });
    Object.assign(b!, { alive: false, hp: 0, backpack: [{ supplies: 20 }] });
    Object.assign(d!, { alive: false, evacuated: true, backpack: [{ medicines: 1 }] });
    for (const u of mission.units) if (u.team === 'xcom' && u.alive) Object.assign(u, { alive: false, evacuated: true });
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const credits = c.credits;
    const smoke = c.inventory.smokeGrenade ?? 0;
    // No enclave convoy while the squad flies back, to count only the loot (and the reward's).
    c.nextMedicineDay = 999;
    const medicines = c.medicines ?? 0;
    const report = applyMissionResult(c, mission, new Rng(4));
    expect(report.loot).toEqual({ supplies: 12, items: ['smokeGrenade'], medicines: 2, food: 0, parts: 0, weapons: [], line: lootSummary(12, ['smokeGrenade'], 2) });
    expect(report.loot!.line).toMatch(/12/);
    expect(c.medicines).toBe(medicines + 2 + (offer.reward.medicines ?? 0));
    expect(c.inventory.smokeGrenade).toBe(smoke + 1);
    // The reward and the loot; no bodies from a Saqueo: the squad left by the evac.
    expect(report.bodies).toBe(0);
    expect(c.credits).toBeGreaterThanOrEqual(credits + 12 + (offer.reward.credits ?? 0));
  });

  it('a lost mission brings home only what was evacuated', () => {
    const { c, mission } = flown('elimination');
    const [a, b] = mission.units.filter((u) => u.team === 'xcom');
    Object.assign(a!, { alive: false, evacuated: true, backpack: [{ supplies: 8 }] });
    Object.assign(b!, { backpack: [{ supplies: 30 }] });
    mission.outcome = 'defeat';
    mission.outcomeReason = 'abandoned';
    const report = applyMissionResult(c, mission, new Rng(4));
    expect(report.loot).toMatchObject({ supplies: 8, items: [], medicines: 0 });
  });

  it('Saqueos turn up among the offers, in towns and full of the dead; older campaigns keep working', () => {
    const offers = [];
    for (let seed = 1; seed < 30; seed++) {
      const c = createCampaign([0], new Rng(seed));
      for (let i = 0; i < 6; i++) executeCampaignCommand(c, { type: 'advance', ready: true }, { slot: 0, connected: [0], rng: new Rng(seed * 10 + i) });
      offers.push(...c.offers.filter((o) => o.kind === 'scavenge'));
    }
    expect(offers.length).toBeGreaterThan(0);
    for (const o of offers) {
      expect(o.map).toMatchObject({ kind: 'generated', biome: 'city' });
      expect(o.pods.flat().every((t) => TEMPLATES[t].undead)).toBe(true);
    }
    const old = createCampaign([0], new Rng(1)) as CampaignState & { medicines?: number };
    delete old.medicines;
    const c = migrateCampaign(old);
    expect(c.version).toBe(CAMPAIGN_VERSION);
    expect(c.medicines ?? 0).toBe(0);
  });
});

describe('the backpack (docs/FASE8.md §C)', () => {
  const item = (s: GameState, cmd: Omit<Extract<Parameters<typeof executeCommand>[1], { type: 'item' }>, 'type'>) => executeCommand(s, { type: 'item', ...cmd }, ctx());
  // A zombie asleep far away, so the mission goes on.
  const field = () => {
    const s = stateFrom(Array.from({ length: 20 }, () => '.'.repeat(20)));
    pod(s, 'p1', false, [addUnit(s, 'a1', 'zombie', 19, 19)]);
    return s;
  };

  it('dropping and taking are free, even with no actions left; what is dropped stays on the tile for anyone', () => {
    const s = field();
    const a = addUnit(s, 's1', 'rookie', 5, 5, { ap: 0, backpack: [{ food: 3 }, { medicines: 1 }] });
    const b = addUnit(s, 's2', 'rookie', 9, 9, { backpack: [{ supplies: 4 }, { parts: 2 }, { food: 1 }] });
    expect(item(s, { unit: 's1', action: 'drop', index: 0 }).ok).toBe(true);
    expect(a.backpack).toEqual([{ medicines: 1 }]);
    expect(s.dropped).toEqual([{ id: 'l1', pos: { x: 5, y: 5, z: 0 }, loot: { food: 3 } }]);
    expect(item(s, { unit: 's2', action: 'take', id: 'l1' })).toEqual({ ok: false, error: 'Aquí no hay nada que coger.' });
    b.pos = { x: 5, y: 5, z: 0 };
    expect(item(s, { unit: 's2', action: 'take', id: 'l1' })).toEqual({ ok: false, error: 'La mochila está llena: suelta algo antes.' });
    expect(item(s, { unit: 's1', action: 'take', id: 'l1' }).ok).toBe(true);
    expect(a.backpack).toEqual([{ medicines: 1 }, { food: 3 }]);
    expect(s.dropped).toEqual([]);
    expect(a.ap).toBe(0);
    // The Chapistería's one more slot.
    b.packSlots = 4;
    a.backpack!.pop();
    s.dropped = [{ id: 'l9', pos: { x: 5, y: 5, z: 0 }, loot: { supplies: 9 } }];
    expect(item(s, { unit: 's2', action: 'take', id: 'l9' }).ok).toBe(true);
    expect(b.backpack).toHaveLength(4);
  });

  it('giving takes an action and a partner next to you with room', () => {
    const s = field();
    const a = addUnit(s, 's1', 'rookie', 5, 5, { backpack: [{ parts: 2 }] });
    const b = addUnit(s, 's2', 'rookie', 7, 5, { backpack: [{ supplies: 4 }, { parts: 2 }, { food: 1 }] });
    expect(item(s, { unit: 's1', action: 'give', index: 0, to: 's2' })).toEqual({ ok: false, error: 'Tiene que estar a tu lado.' });
    b.pos = { x: 6, y: 6, z: 0 };
    expect(item(s, { unit: 's1', action: 'give', index: 0, to: 's2' })).toEqual({ ok: false, error: 'No le cabe: lleva la mochila llena.' });
    b.backpack!.pop();
    const ap = a.ap;
    expect(item(s, { unit: 's1', action: 'give', index: 0, to: 's2' }).ok).toBe(true);
    expect(a.backpack).toEqual([]);
    expect(b.backpack).toEqual([{ supplies: 4 }, { parts: 2 }, { parts: 2 }]);
    expect(a.ap).toBe(ap - 1);
    expect(item(s, { unit: 's1', action: 'give', index: 0, to: 's2' })).toEqual({ ok: false, error: 'No lleva eso en la mochila.' });
  });

  it('a medicine heals 3 on you or a partner next to you, for an action; the contagion only stops once unlocked', () => {
    const s = field();
    const a = addUnit(s, 's1', 'rookie', 5, 5, { backpack: [{ medicines: 2 }, { food: 1 }] });
    const b = addUnit(s, 's2', 'rookie', 6, 5, { hp: 1, infected: true });
    expect(item(s, { unit: 's1', action: 'use', index: 1 })).toEqual({ ok: false, error: 'Solo se usan las medicinas.' });
    expect(item(s, { unit: 's1', action: 'use', index: 0 })).toEqual({ ok: false, error: 'No estás herido.' });
    const r = item(s, { unit: 's1', action: 'use', index: 0, to: 's2' });
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(b.hp).toBe(4);
    expect(b.infected).toBe(true);
    expect(of(r.events, 'cured')).toEqual([]);
    expect(a.backpack).toEqual([{ medicines: 1 }, { food: 1 }]);
    // Healthy but bitten: with the Enfermería at level 2, the medicine is worth it again, and it stops the bite.
    b.hp = b.maxHp;
    expect(item(s, { unit: 's1', action: 'use', index: 0, to: 's2' })).toEqual({ ok: false, error: 'No está herido.' });
    s.mission = { ...s.mission!, medicineCures: true };
    expect(item(s, { unit: 's1', action: 'use', index: 0, to: 's2' }).ok).toBe(true);
    expect(b.infected).toBe(false);
    expect(a.backpack).toEqual([{ food: 1 }]);
  });

  it('the support talent stops the bite for its own soldier only', () => {
    const s = field();
    addUnit(s, 's1', 'rookie', 5, 5, { perks: ['lightHands'], backpack: [{ medicines: 1 }] });
    const b = addUnit(s, 's2', 'rookie', 6, 5, { hp: 2, infected: true });
    expect(item(s, { unit: 's1', action: 'use', index: 0, to: 's2' }).ok).toBe(true);
    expect(b.infected).toBe(false);
  });

  it('the Chapistería gives one more slot, and the Enfermería at level 2 makes medicines stop the bite', () => {
    const c = createCampaign([0], new Rng(1));
    expect(buildSquad(c).every((m) => m.packSlots === undefined)).toBe(true);
    expect(medicineCures(c)).toBe(false);
    const free = c.base.plots.filter((p) => p.terrain === 'clear' && !p.building);
    Object.assign(free[0]!, { building: 'garage', level: 1 });
    Object.assign(free[1]!, { building: 'infirmary', level: 2 });
    expect(buildSquad(c).every((m) => m.packSlots === 4)).toBe(true);
    expect(medicineCures(c)).toBe(true);
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 3 }, slots: [0], rng: new Rng(1), squad: buildSquad(c), medicineCures: medicineCures(c) });
    expect(s.mission!.medicineCures).toBe(true);
    expect(s.units.filter((u) => u.team === 'xcom').every((u) => u.packSlots === 4)).toBe(true);
  });

  it('a client replaying the backpack ends with the host state', () => {
    const s = field();
    addUnit(s, 's1', 'rookie', 5, 5, { backpack: [{ medicines: 2 }, { food: 3 }] });
    addUnit(s, 's2', 'rookie', 6, 5, { hp: 2, infected: true, perks: ['lightHands'] });
    const client = cloneState(s);
    const orders = [
      { unit: 's1', action: 'drop', index: 1 },
      { unit: 's1', action: 'take', id: 'l1' },
      { unit: 's1', action: 'give', index: 1, to: 's2' },
      { unit: 's2', action: 'drop', index: 0 },
      { unit: 's1', action: 'use', index: 0, to: 's2' },
    ] as const;
    for (const o of orders) {
      const r = item(s, o);
      expect(r.ok).toBe(true);
      if (r.ok) for (const e of r.events) applyEvent(client, e);
    }
    expect(client).toEqual(s);
  });
});

describe('replication of the new events', () => {
  it('clients replaying noise, searches and bursts end with the host state', () => {
    const s = field();
    s.decor = new Array(400).fill(0);
    s.decor[5 * 20 + 6] = decorCode('lootFood');
    s.tiles[5 * 20 + 6] = 'low';
    addUnit(s, 's1', 'rookie', 5, 5);
    const z = addUnit(s, 'a1', 'zombie', 15, 5);
    const b = addUnit(s, 'a2', 'bloater', 6, 6, { hp: 1 });
    pod(s, 'p1', false, [z, b]);
    const client = cloneState(s);
    const host = s;
    const r1 = executeCommand(host, { type: 'ability', unit: 's1', ability: 'search', tile: { x: 6, y: 5 } }, ctx(9));
    const r2 = executeCommand(host, { type: 'ability', unit: 's1', ability: 'shoot', target: 'a2' }, ctx(9));
    for (const r of [r1, r2]) if (r.ok) for (const e of r.events) applyEvent(client, e);
    expect(client).toEqual(host);
  });
});
