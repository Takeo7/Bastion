import { describe, expect, it } from 'vitest';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { abilitiesOf } from '../src/abilities';
import { buildSquad, createCampaign } from '../src/campaign';
import { ABILITIES, apPerTurn, TEMPLATES, THIRD_ACTION_RANK } from '../src/content';
import { createMission, garrison, hordeWave, makeUnit, missionPods } from '../src/setup';
import { STORY } from '../src/lore';
import { QUESTS, type QuestMission } from '../src/quests';
import { Rng } from '../src/rng';
import { computeReach } from '../src/path';
import { executeCommand, Sim } from '../src/rules';
import type { Command } from '../src/protocol';
import type { GameState, MissionKind, TemplateId } from '../src/types';
import { addUnit, stateFrom } from './helpers';

const ctx = (seed = 1) => ({ slot: 0 as const, connected: [0 as const], rng: new Rng(seed) });

function run(s: GameState, cmd: Command, seed = 1): GameEvent[] {
  const r = executeCommand(s, cmd, ctx(seed));
  if (!r.ok) throw new Error(r.error);
  return r.events;
}

function field(kind: MissionKind, rows = Array.from({ length: 20 }, () => '.'.repeat(20))): GameState {
  const s = stateFrom(rows);
  s.mission = { ...s.mission, kind, turnsLeft: 5, evacZone: [{ x: 0, y: 19 }, { x: 1, y: 19 }] };
  return s;
}

describe('sabotage', () => {
  it('destroying the relay secures the objective and stops the timer; the mission is won back on the bus', () => {
    const s = field('sabotage');
    addUnit(s, 's1', 'sharpshooter', 0, 0);
    const relay = { ...makeUnit('relay', 'relay', 'Retransmisor', null, { x: 6, y: 0 }, null), objective: true };
    s.units.push(relay);
    s.mission.objectiveUnit = 'relay';
    const alien = addUnit(s, 'a1', 'trooper', 19, 19);
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(relay, 99, 0);
    sim.checkOutcome();
    expect(s.mission.objectiveDone).toBe(true);
    expect(s.mission.turnsLeft).toBeNull();
    expect(s.outcome).toBeNull();
    sim.damage(alien, 99, 0);
    sim.checkOutcome();
    // Clearing the map is not enough (docs/PRUEBA-2026-10-14.md P1): it is won getting out.
    expect(s.outcome).toBeNull();
    s.mission.evacZone = [{ x: 0, y: 0 }];
    run(s, { type: 'evac', unit: 's1' });
    expect(s.outcome).toBe('victory');
  });

  it('the relay never counts as a hostile for an elimination check', () => {
    const s = field('elimination');
    addUnit(s, 's1', 'sharpshooter', 0, 0);
    s.units.push({ ...makeUnit('relay', 'relay', 'R', null, { x: 6, y: 0 }, null), objective: true });
    const sim = new Sim(s, new Rng(1), [0]);
    sim.checkOutcome();
    expect(s.outcome).toBe('victory');
  });
});

describe('hack', () => {
  function hackField() {
    const rows = Array.from({ length: 20 }, () => '.'.repeat(20));
    rows[5] = '.....C' + '.'.repeat(14);
    const s = field('hack', rows);
    s.mission.terminal = { x: 5, y: 5, z: 0 };
    s.concealed = true;
    return s;
  }

  it('only next to the terminal, or within the drone range for a specialist', () => {
    const s = hackField();
    addUnit(s, 's1', 'assault', 0, 0);
    addUnit(s, 's2', 'specialist', 5, 12);
    addUnit(s, 'a1', 'trooper', 19, 0);
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'hack' }, ctx()).ok).toBe(false);
    expect(executeCommand(s, { type: 'ability', unit: 's2', ability: 'hack' }, ctx()).ok).toBe(true);
  });

  it('success secures the objective; failure raises the alarm, heard far, and a horde comes in by an edge', () => {
    let failed: GameState | null = null;
    for (let seed = 1; seed < 40; seed++) {
      const s = hackField();
      addUnit(s, 's1', 'assault', 4, 4);
      addUnit(s, 's2', 'assault', 0, 19); // still has actions: the XCOM turn goes on
      addUnit(s, 'a1', 'trooper', 19, 0);
      const events = run(s, { type: 'ability', unit: 's1', ability: 'hack' }, seed);
      const hack = events.find((e) => e.t === 'hacked') as Extract<GameEvent, { t: 'hacked' }>;
      if (hack.success) {
        expect(s.mission.objectiveDone).toBe(true);
      } else {
        const coming = events.find((e) => e.t === 'reinforcementsIncoming') as Extract<GameEvent, { t: 'reinforcementsIncoming' }>;
        expect(coming.r).toMatchObject({ horde: true, pos: null });
        expect(coming.r.units.every((t) => t !== 'trooper' && t !== 'officer' && t !== 'lancer')).toBe(true);
        expect(events.find((e) => e.t === 'noise')).toMatchObject({ tile: s.mission.terminal!, radius: 16 });
        expect(s.concealed).toBe(false);
        failed = s;
      }
    }
    expect(failed).not.toBeNull();
    // The horde lands at the end of the enemy turn, by an edge, on the prowl.
    const s = failed!;
    const before = s.units.length;
    new Sim(s, new Rng(2), [0]).runAlienTurn();
    expect(s.units.length).toBeGreaterThan(before);
    expect(s.mission.reinforcements.every((r) => r.landed)).toBe(true);
    expect(s.pods.find((p) => p.id.startsWith('pr'))?.active).toBe(false);
    for (const u of s.units.filter((x) => x.podId?.startsWith('pr'))) expect(u.pos.x < 2 || u.pos.y < 2 || u.pos.x >= s.width - 2 || u.pos.y >= s.height - 2).toBe(true);
  });
});

describe('rescue', () => {
  function rescueField() {
    const s = field('rescue');
    s.units.push({ ...makeUnit('vip', 'vip', 'VIP', null, { x: 8, y: 8 }, null), captive: true, ap: 0 });
    s.mission.objectiveUnit = 'vip';
    addUnit(s, 's1', 'assault', 0, 8);
    addUnit(s, 'a1', 'trooper', 19, 0);
    return s;
  }

  it('the VIP joins whoever reaches them, then must be extracted', () => {
    const s = rescueField();
    run(s, { type: 'move', unit: 's1', to: { x: 7, y: 8 } });
    const vip = s.units.find((u) => u.id === 'vip')!;
    expect(vip.captive).toBe(false);
    expect(vip.owner).toBe(0);
    // Both on the bus; it leaves, and the VIP is out (docs/PRUEBA-2026-10-14.md P1).
    s.mission.evacZone = [{ x: 0, y: 19 }, { x: 1, y: 19 }];
    vip.pos = { x: 0, y: 19, z: 0 };
    s.units.find((u) => u.id === 's1')!.pos = { x: 1, y: 19, z: 0 };
    run(s, { type: 'evac', unit: 'vip' });
    expect(s.mission.objectiveDone).toBe(true);
    expect(s.outcome).toBe('victory');
  });

  it('losing the VIP fails the mission', () => {
    const s = rescueField();
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(s.units.find((u) => u.id === 'vip')!, 99, 0);
    sim.checkOutcome();
    expect(s.outcome).toBe('defeat');
    expect(s.outcomeReason).toBe('objectiveLost');
  });

  it('a captive VIP is ignored by the aliens and gives no vision', () => {
    const s = rescueField();
    s.units.find((u) => u.id === 's1')!.pos = { x: 0, y: 0, z: 0 };
    const sim = new Sim(s, new Rng(1), [0]);
    expect(sim.podSpotsSquad(s.units.find((u) => u.id === 'a1')!, { x: 19, y: 19, z: 0 })).toBe(false);
  });
});

describe('patrols and reinforcements', () => {
  it('a patrolling pod walks its route and stops when it spots the squad', () => {
    const s = field('elimination', Array.from({ length: 30 }, () => '.'.repeat(30)));
    s.concealed = false;
    addUnit(s, 's1', 'specialist', 2, 28);
    const a = addUnit(s, 'a1', 'trooper', 28, 2, { podId: 'p1' });
    addUnit(s, 'a2', 'trooper', 28, 3, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: false, unitIds: ['a1', 'a2'], patrol: [{ x: 28, y: 2, z: 0 }, { x: 4, y: 26, z: 0 }], patrolIndex: 0 });
    const client = cloneState(s);
    const sim = new Sim(s, new Rng(1), [0]);
    let moved = false;
    for (let turn = 0; turn < 8 && !s.pods[0]!.active; turn++) {
      const before = { ...a.pos };
      sim.runAlienTurn();
      if (a.pos.x !== before.x || a.pos.y !== before.y) moved = true;
    }
    expect(moved).toBe(true);
    expect(s.pods[0]!.active).toBe(true);
    for (const e of sim.events) applyEvent(client, e);
    expect(client).toEqual(s);
  });

  it('a pod on patrol goes in by the door, never through a window', () => {
    const s = field('elimination', [
      '........................',
      '........................',
      '....#####...............',
      '....#...#...............',
      '....=...+...............',
      '....#...#...............',
      '....#####...............',
      ...Array.from({ length: 17 }, () => '.'.repeat(24)),
    ]);
    s.concealed = true;
    addUnit(s, 's1', 'specialist', 23, 23);
    const a = addUnit(s, 'a1', 'trooper', 1, 4, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: false, unitIds: ['a1'], patrol: [{ x: 6, y: 4, z: 0 }], patrolIndex: 0 });
    const glass = 4 * 24 + 4;
    expect(computeReach(s, a, 100).cost.has(glass)).toBe(false);
    const sim = new Sim(s, new Rng(1), [0]);
    for (let turn = 0; turn < 4; turn++) sim.runAlienTurn();
    expect(s.tiles[glass]).toBe('window');
    expect(s.tiles[4 * 24 + 8]).toBe('doorOpen');
    expect(sim.events.some((e) => e.t === 'tileChanged' && e.kind === 'windowBroken')).toBe(false);
    // Once the pod is in the fight, the window is a way in again.
    s.pods[0]!.active = true;
    expect(computeReach(s, a, 100).cost.has(glass)).toBe(true);
  });

  // Playtest 2026-10-13: nobody drops in out of nowhere.
  it('every enemy is on the map from the start; only hordes of the dead come later, by an edge; the living only where the mission is theirs', () => {
    const kinds: MissionKind[] = ['elimination', 'recovery', 'sabotage', 'hack', 'rescue', 'scavenge'];
    /** The living on the map (the Sirena is a machine, not one of them). */
    const livingOn = (s: GameState) => s.units.filter((u) => u.team === 'alien' && !TEMPLATES[u.template].undead && !TEMPLATES[u.template].robotic).length;
    for (const kind of kinds) {
      for (const difficulty of [1, 2, 3, 4]) {
        const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 11 + difficulty }, slots: [0], rng: new Rng(difficulty), kind, difficulty });
        const label = `${kind} ${difficulty}`;
        // docs/ZOMBIS-PRIMERO.md: a mission of the dead has none of the living; one of the living, at least their garrison.
        expect(livingOn(s), label).toBe(0);
        if (kind !== 'scavenge') {
          const held = createMission({ map: { kind: 'generated', biome: 'city', seed: 11 + difficulty }, slots: [0], rng: new Rng(difficulty), kind, difficulty, living: true });
          expect(livingOn(held), label).toBeGreaterThanOrEqual(Math.max(2, garrison(held.mission.kind, difficulty, true)?.length ?? 0));
        }
        for (const r of s.mission.reinforcements) {
          expect(r, label).toMatchObject({ horde: true, pos: null, landed: false });
          expect(r.units.every((t) => TEMPLATES[t].undead), label).toBe(true);
        }
        expect(s.mission.horde?.every((t) => TEMPLATES[t].undead) ?? true, label).toBe(true);
        // Sabotage and hack keep their pressure as two hordes; the others bring nobody.
        expect(s.mission.reinforcements.map((r) => r.turn), label).toEqual(s.mission.kind === 'sabotage' || s.mission.kind === 'hack' ? [4, 8] : []);
        // The strength those drops had is a garrison on the map from the start, of the dead here.
        const extra = garrison(s.mission.kind, difficulty);
        const dead = s.units.filter((u) => u.team === 'alien' && TEMPLATES[u.template].undead).length;
        expect(dead, label).toBeGreaterThanOrEqual(extra?.length ?? 0);
      }
    }
    expect(garrison('scavenge', 4)).toBeNull();
    expect(garrison('elimination', 2)).toBeNull();
    expect(garrison('sabotage', 1)).toEqual(['zombie', 'zombie', 'zombie']);
    expect(garrison('sabotage', 1, true)).toEqual(['trooper', 'trooper']);
    expect(hordeWave(9)).toEqual(hordeWave(4));
  }, 60_000);

  it('a horde due lands at the end of its enemy turn, by an edge and away from the squad, on the prowl', () => {
    const s = field('sabotage', Array.from({ length: 30 }, () => '.'.repeat(30)));
    s.concealed = true;
    addUnit(s, 's1', 'specialist', 15, 15);
    addUnit(s, 'a1', 'trooper', 29, 29);
    s.mission.turnsLeft = 20;
    s.mission.reinforcements = [{ id: 'r1', turn: 2, pos: null, units: hordeWave(3), landed: false, horde: true }];
    const sim = new Sim(s, new Rng(4), [0]);
    sim.runAlienTurn(); // turn 1: nothing yet, and no flare
    expect(sim.events.some((e) => e.t === 'reinforcementsIncoming' || e.t === 'spawned')).toBe(false);
    expect(s.mission.reinforcements[0]!.pos).toBeNull();
    s.turn = 2;
    sim.runAlienTurn(); // turn 2: in by an edge
    expect(s.mission.reinforcements[0]!.landed).toBe(true);
    const horde = s.units.filter((u) => u.id.startsWith('r1'));
    expect(horde.map((u) => u.template)).toEqual(hordeWave(3));
    for (const u of horde) {
      expect(u.pos.x < 2 || u.pos.y < 2 || u.pos.x >= 28 || u.pos.y >= 28).toBe(true);
      expect(Math.hypot(u.pos.x - 15, u.pos.y - 15)).toBeGreaterThanOrEqual(12);
      expect(u.heard).toEqual({ x: 15, y: 15, z: 0 });
    }
    // Unaware: they come looking, and the squad stays hidden.
    expect(s.pods.find((p) => p.id === 'pr1')!.active).toBe(false);
    expect(s.concealed).toBe(true);
  });

  it('scheduled reinforcements from older saves are still announced a turn early and land alert', () => {
    const s = field('recovery', Array.from({ length: 30 }, () => '.'.repeat(30)));
    s.concealed = false;
    addUnit(s, 's1', 'specialist', 15, 15);
    addUnit(s, 'a1', 'trooper', 29, 29);
    s.mission.turnsLeft = 20;
    s.mission.reinforcements = [{ id: 'r1', turn: 2, pos: null, units: ['trooper', 'trooper'], landed: false }];
    const sim = new Sim(s, new Rng(4), [0]);
    sim.runAlienTurn(); // turn 1: flare for turn 2
    expect(s.mission.reinforcements[0]!.pos).not.toBeNull();
    expect(s.mission.reinforcements[0]!.landed).toBe(false);
    sim.runAlienTurn(); // turn 2: drop
    expect(s.mission.reinforcements[0]!.landed).toBe(true);
    const dropped = s.units.filter((u) => u.id.startsWith('r1'));
    expect(dropped).toHaveLength(2);
    for (const u of dropped) {
      const d = Math.hypot(u.pos.x - 15, u.pos.y - 15);
      expect(d).toBeGreaterThanOrEqual(3);
    }
  });
});

describe('skirmish soldiers (docs/FASE9.md §6)', () => {
  it('each one a weapon and gear at random; veterans, every pair of talents they opened picked; the same seed, the same squad', () => {
    const kinds = new Set<string>();
    for (let seed = 1; seed <= 20; seed++) {
      const veterans = (rng: Rng) => createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0, 1], rng, veterans: true }).units.filter((u) => u.team === 'xcom');
      const squad = veterans(new Rng(seed));
      expect(veterans(new Rng(seed)).map((u) => [u.perks, u.arms])).toEqual(squad.map((u) => [u.perks, u.arms]));
      for (const u of squad) {
        kinds.add(u.arms!.primary!);
        expect(u.template).toBe('rookie');
        expect(u.gear).toContain('fragGrenade');
        expect(abilitiesOf(u)).toContain('grenade');
        // Skills of 2 to 4: a talent of each level 2, at least, from Fogueado on.
        expect(u.perks.length).toBeGreaterThanOrEqual(4);
        expect(u.rank).toBeGreaterThanOrEqual(2);
      }
    }
    expect(kinds).toEqual(new Set(['assaultRifle', 'pumpShotgun', 'precisionRifle', 'machineGun']));
    // Rookies in a skirmish have no talents yet.
    const rookies = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(1) }).units.filter((u) => u.team === 'xcom');
    expect(rookies.every((u) => !u.perks.length && u.rank === 0)).toBe(true);
  }, 60_000);

  it('the campaign is untouched: its soldiers bring exactly what they learnt', () => {
    const c = createCampaign([0], new Rng(3));
    c.squad = c.soldiers.slice(0, 6).map((s) => s.id);
    const squad = buildSquad(c);
    expect(squad).toHaveLength(6);
    for (let seed = 1; seed <= 10; seed++) {
      const mission = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(seed), squad, veterans: true });
      expect(mission.units.filter((u) => u.team === 'xcom').map((u) => u.perks)).toEqual(squad.map((m) => m.perks));
    }
  });
});

describe('the dead first (docs/ZOMBIS-PRIMERO.md)', () => {
  const living = (pods: TemplateId[][]) => pods.flat().filter((t) => !TEMPLATES[t].undead).length;

  it('the groups of an ordinary mission are all the dead; a mission of the living has two of their bands among them', () => {
    for (const difficulty of [1, 2, 3, 4]) {
      for (const kind of ['elimination', 'recovery', 'sabotage', 'hack', 'rescue', 'scavenge'] as const) {
        expect(living(missionPods(difficulty, kind)), `${kind} ${difficulty}`).toBe(0);
      }
      const held = missionPods(difficulty, 'rescue', true);
      expect(held.filter((p) => p.every((t) => !TEMPLATES[t].undead))).toHaveLength(2);
      expect(held.filter((p) => p.every((t) => TEMPLATES[t].undead))).toHaveLength(2);
    }
  });

  it('the living hold the story chapters and quest steps that are theirs, and nothing else', () => {
    expect(STORY.filter((c) => c.living).map((c) => c.title)).toEqual(['La sirena', 'Los inmunes', 'La Voz']);
    const steps = Object.values(QUESTS).flatMap((q) => q.steps.filter((s): s is QuestMission => s.type === 'mission'));
    const held = steps.filter((s) => s.living || (s.leader && !TEMPLATES[s.leader.template].undead)).map((s) => s.name);
    expect(held).toEqual(
      expect.arrayContaining(['La lista de prisioneros', 'Sacar a {relative}', 'Vera Lindqvist', 'Otto Brandt', 'Ruta 50', 'Cacería en la Ruta 50', 'Manos', 'Las notas de Mei']),
    );
    expect(held).toHaveLength(8);
  });
});

describe('ranks (docs/PRUEBA-2026-10-14.md P5)', () => {
  it('a soldier from Comandante on starts each turn with three actions; anyone else, two', () => {
    const s = field('elimination');
    const veteran = addUnit(s, 's1', 'assault', 2, 2, { rank: THIRD_ACTION_RANK });
    const captain = addUnit(s, 's2', 'assault', 4, 2, { rank: THIRD_ACTION_RANK - 1 });
    applyEvent(s, { t: 'turnBegan', team: 'xcom', turn: 2 });
    expect(veteran.ap).toBe(3);
    expect(captain.ap).toBe(2);
    expect(apPerTurn({ team: 'alien', rank: 7 })).toBe(2);
  });
});
