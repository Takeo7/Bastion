import { describe, expect, it } from 'vitest';
import { abilitiesOf, abilityBlocker, abilityTargets, allyTargets, canTargetTile, meleeTargets } from '../src/abilities';
import { ABILITIES, isAttack, perksFor } from '../src/content';
import { previewShot } from '../src/aim';
import { cloneState, applyEvent, type GameEvent } from '../src/events';
import { MAPS, parseMap } from '../src/maps';
import { computeReach } from '../src/path';
import type { Command } from '../src/protocol';
import { Rng } from '../src/rng';
import { abandonMission, executeCommand, Sim } from '../src/rules';
import { createMission } from '../src/setup';
import type { AbilityId, ClassId, GameState, Slot, TemplateId } from '../src/types';
import { addUnit, stateFrom } from './helpers';

const ctx = (slot: Slot = 0, connected: Slot[] = [0], seed = 1) => ({ slot, connected, rng: new Rng(seed) });

function run(s: GameState, cmd: Command, c = ctx()): GameEvent[] {
  const r = executeCommand(s, cmd, c);
  if (!r.ok) throw new Error(r.error);
  return r.events;
}

const ability = (unit: string, id: AbilityId, extra: { target?: string; tile?: { x: number; y: number } } = {}): Command => ({
  type: 'ability',
  unit,
  ability: id,
  ...extra,
});

const field = (w = 20, h = 20) => stateFrom(Array.from({ length: h }, () => '.'.repeat(w)));

describe('actions', () => {
  it('a short move costs 1 AP, a dash costs 2', () => {
    const s = field();
    const u = addUnit(s, 's1', 'specialist', 0, 0);
    addUnit(s, 's2', 'specialist', 19, 19);
    addUnit(s, 'a1', 'trooper', 19, 0);
    run(s, { type: 'move', unit: 's1', to: { x: 5, y: 0 } });
    expect(u.ap).toBe(1);
    const t = field();
    const v = addUnit(t, 's1', 'specialist', 0, 0);
    addUnit(t, 's2', 'specialist', 19, 19);
    addUnit(t, 'a1', 'trooper', 19, 0);
    run(t, { type: 'move', unit: 's1', to: { x: 12, y: 0 } });
    expect(v.ap).toBe(0);
  });

  it('rejects orders for the partner soldiers', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 0, 0, { owner: 1 });
    addUnit(s, 'a1', 'trooper', 19, 19);
    const r = executeCommand(s, { type: 'move', unit: 's1', to: { x: 1, y: 0 } }, ctx(0, [0, 1]));
    expect(r.ok).toBe(false);
  });

  it('enemy overwatch interrupts a move and splits it into segments', () => {
    const s = field();
    const u = addUnit(s, 's1', 'specialist', 0, 10);
    addUnit(s, 's2', 'specialist', 0, 19);
    addUnit(s, 'a1', 'trooper', 10, 0, { overwatch: true });
    const events = run(s, { type: 'move', unit: 's1', to: { x: 6, y: 10 } });
    const firstMove = events.findIndex((e) => e.t === 'moved');
    const shot = events.findIndex((e) => e.t === 'shot');
    expect(firstMove).toBeGreaterThanOrEqual(0);
    expect(shot).toBeGreaterThan(firstMove);
    expect(events[shot]).toMatchObject({ reaction: true, unit: 'a1' });
    if (u.alive) expect(u.pos).toEqual({ x: 6, y: 10 });
  });

  it('grenades damage everyone in the blast and destroy cover', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 0, 0);
    const a = addUnit(s, 'a1', 'trooper', 5, 5);
    addUnit(s, 'a2', 'trooper', 19, 19);
    s.tiles[5 * 20 + 6] = 'low';
    s.tiles[4 * 20 + 5] = 'high';
    run(s, ability('s1', 'grenade', { tile: { x: 5, y: 5 } }));
    expect(a.hp).toBeLessThan(3);
    expect(s.tiles[5 * 20 + 6]).toBe('floor');
    expect(s.tiles[4 * 20 + 5]).toBe('low');
  });

  it('the grenade launcher reaches further than a thrown grenade', () => {
    const s = field();
    const g = addUnit(s, 's1', 'grenadier', 0, 0);
    const sp = addUnit(s, 's2', 'specialist', 0, 1);
    addUnit(s, 'a1', 'trooper', 19, 19);
    expect(canTargetTile(s, sp, 'grenade', { x: 12, y: 0 })).toBe(false);
    expect(canTargetTile(s, g, 'launch', { x: 12, y: 0 })).toBe(true);
  });
});

describe('classes', () => {
  it('each class brings its own kit', () => {
    const kit = (t: Parameters<typeof addUnit>[2]) => abilitiesOf(addUnit(field(), 'x', t, 0, 0));
    expect(kit('assault')).toEqual(expect.arrayContaining(['slash', 'shoot']));
    expect(kit('grenadier')).toContain('launch');
    expect(kit('sharpshooter')).toEqual(expect.arrayContaining(['pistol', 'shoot']));
    expect(kit('specialist')).toEqual(expect.arrayContaining(['medkit', 'aid']));
  });

  it('the sniper rifle needs both actions, the pistol only one', () => {
    const s = field();
    const sniper = addUnit(s, 's1', 'sharpshooter', 0, 0, { ap: 1 });
    addUnit(s, 'a1', 'trooper', 0, 8);
    expect(abilityBlocker(s, sniper, 'shoot')).toMatch(/2 acciones/);
    expect(abilityBlocker(s, sniper, 'pistol')).toBeNull();
  });

  it('squadsight lets the sniper hit what an ally sees, with a distance penalty', () => {
    const s = stateFrom(Array.from({ length: 30 }, () => '.'.repeat(5)));
    const sniper = addUnit(s, 's1', 'sharpshooter', 2, 0);
    addUnit(s, 's2', 'specialist', 2, 15);
    const far = addUnit(s, 'a1', 'trooper', 2, 25);
    const p = previewShot(s, sniper, far)!;
    expect(p.squadsight).toBe(true);
    expect(p.mods.find((m) => m.label === 'Visión de escuadra')?.value).toBe(-2 * (25 - 18));
    expect(previewShot(s, sniper, far, { ability: 'pistol' })).toBeNull();
  });

  it('slash runs to the enemy and ignores cover', () => {
    const s = field();
    const ranger = addUnit(s, 's1', 'assault', 0, 5);
    const a = addUnit(s, 'a1', 'trooper', 5, 5, { hp: 20, maxHp: 20 });
    s.tiles[5 * 20 + 6] = 'high';
    const options = meleeTargets(s, ranger, 'slash');
    expect(options.map((o) => o.target.id)).toContain('a1');
    const p = previewShot(s, ranger, a, { ability: 'slash', from: options[0]!.tile })!;
    expect(p.cover).toBe(0);
    expect(p.mods.find((m) => m.label === 'Arma cuerpo a cuerpo')?.value).toBe(20);
    const events = run(s, ability('s1', 'slash', { target: 'a1' }));
    expect(events.some((e) => e.t === 'moved')).toBe(true);
    expect(events.some((e) => e.t === 'shot' && e.ability === 'slash')).toBe(true);
    expect(Math.max(Math.abs(ranger.pos.x - a.pos.x), Math.abs(ranger.pos.y - a.pos.y))).toBe(1);
  });

  it('the medkit heals and spends a charge; aid protocol raises defense', () => {
    const s = field();
    const medic = addUnit(s, 's1', 'specialist', 0, 0);
    const hurt = addUnit(s, 's2', 'assault', 3, 3, { hp: 1 });
    addUnit(s, 'a1', 'trooper', 10, 3);
    expect(allyTargets(s, medic, 'medkit')).toContain(hurt);
    run(s, ability('s1', 'medkit', { target: 's2' }));
    expect(hurt.hp).toBe(5);
    expect(medic.charges.medkit).toBe(1);
    expect(medic.ap).toBe(1);
    run(s, ability('s1', 'aid', { target: 's2' }));
    expect(hurt.aided).toBe(20);
    const shot = previewShot(s, s.units.find((u) => u.id === 'a1')!, hurt)!;
    expect(shot.mods.find((m) => m.label === 'Protocolo de ayuda')?.value).toBe(-20);
  });
});

describe('enemies', () => {
  it('armour absorbs damage and explosives shred it', () => {
    const s = field();
    const mec = addUnit(s, 'a1', 'mec', 5, 5);
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(mec, 4, 0);
    expect(mec.hp).toBe(7 - 3);
    sim.damage(mec, 3, 1);
    expect(mec.armor).toBe(0);
    sim.damage(mec, 1, 0);
    expect(mec.hp).toBe(7 - 3 - 2 - 1);
  });

  it('a stun lance hit leaves the soldier with a single action next turn', () => {
    const s = field();
    const soldier = addUnit(s, 's1', 'specialist', 5, 5, { hp: 50, maxHp: 50 });
    const lancer = addUnit(s, 'a1', 'lancer', 7, 5);
    let stunned = false;
    for (let seed = 1; seed < 20 && !stunned; seed++) {
      const sim = new Sim(s, new Rng(seed), [0]);
      sim.meleeAttack(lancer, meleeTargets(s, lancer, 'stunLance')[0]!, 'stunLance');
      stunned = soldier.stunned;
    }
    expect(stunned).toBe(true);
    applyEvent(s, { t: 'turnBegan', team: 'xcom', turn: 2 });
    expect(soldier.ap).toBe(1);
  });

  it('aliens throw grenades at clustered soldiers', () => {
    const s = field();
    for (const [i, p] of [[8, 10], [9, 10], [8, 11]].entries()) addUnit(s, `s${i}`, 'specialist', p[0]!, p[1]!);
    const trooper = addUnit(s, 'a1', 'trooper', 8, 2, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: true, unitIds: ['a1'], patrol: [], patrolIndex: 0 });
    s.activeTeam = 'alien';
    const sim = new Sim(s, new Rng(3), [0]);
    sim.runAlienTurn();
    expect(sim.events.some((e) => e.t === 'explosive' && e.unit === trooper.id)).toBe(true);
  });
});

describe('concealment and pods', () => {
  function ambushSetup() {
    const s = field(30, 20);
    s.concealed = true;
    addUnit(s, 's1', 'specialist', 0, 10);
    addUnit(s, 's2', 'specialist', 0, 12);
    addUnit(s, 'a1', 'trooper', 20, 10, { podId: 'p1' });
    addUnit(s, 'a2', 'trooper', 20, 12, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: false, unitIds: ['a1', 'a2'], patrol: [], patrolIndex: 0 });
    return s;
  }

  it('concealed soldiers are only noticed inside the detection radius', () => {
    const s = ambushSetup();
    run(s, { type: 'move', unit: 's1', to: { x: 6, y: 10 } });
    expect(s.concealed).toBe(true);
    expect(s.pods[0]!.active).toBe(false);
    run(s, { type: 'move', unit: 's2', to: { x: 13, y: 12 } });
    expect(s.concealed).toBe(false);
    expect(s.pods[0]!.active).toBe(true);
  });

  it('shooting from concealment breaks it and the pod scampers', () => {
    const s = ambushSetup();
    run(s, { type: 'move', unit: 's1', to: { x: 6, y: 10 } });
    const events = run(s, ability('s1', 'shoot', { target: 'a1' }));
    const types = events.map((e) => e.t);
    expect(types).toContain('concealmentBroken');
    expect(types).toContain('podActivated');
  });
});

describe('recovery missions', () => {
  function recovery() {
    const s = field();
    s.mission = { kind: 'recovery', turnsLeft: 3, item: { pos: { x: 4, y: 0 }, carrier: null, evacuated: false }, evacZone: [{ x: 0, y: 5 }], terminal: null, objectiveUnit: null, objectiveDone: false, hackBonus: 0, reinforcements: [] };
    const a = addUnit(s, 's1', 'specialist', 0, 0);
    const b = addUnit(s, 's2', 'specialist', 1, 0);
    addUnit(s, 'a1', 'trooper', 19, 19);
    return { s, a, b };
  }

  it('walking over the objective picks it up; a carrier who falls drops it', () => {
    const { s, a } = recovery();
    run(s, { type: 'move', unit: 's1', to: { x: 6, y: 0 } });
    expect(s.mission.item?.carrier).toBe('s1');
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(a, 99, 0);
    expect(s.mission.item).toMatchObject({ carrier: null, pos: { x: 6, y: 0 } });
  });

  it('evacuating with the objective wins once nobody is left on the field', () => {
    const { s } = recovery();
    run(s, { type: 'move', unit: 's1', to: { x: 4, y: 0 } });
    run(s, { type: 'move', unit: 's1', to: { x: 0, y: 5 } });
    expect(s.mission.item?.carrier).toBe('s1');
    s.units[0]!.ap = 1;
    run(s, ability('s1', 'evac'));
    expect(s.mission.item?.evacuated).toBe(true);
    expect(s.outcome).toBeNull();
    run(s, { type: 'move', unit: 's2', to: { x: 0, y: 5 } });
    run(s, ability('s2', 'evac'));
    expect(s.outcome).toBe('victory');
  });

  it('the mission fails when the timer runs out', () => {
    const { s } = recovery();
    for (let i = 0; i < 4 && !s.outcome; i++) run(s, { type: 'endTurn', ready: true });
    expect(s.outcome).toBe('defeat');
    expect(s.outcomeReason).toBe('timer');
  });

  it('either player can abandon the mission', () => {
    const { s } = recovery();
    const events = abandonMission(s);
    expect(events).toHaveLength(1);
    expect(s.outcomeReason).toBe('abandoned');
  });
});

describe('turn flow', () => {
  it('the XCOM turn ends when every connected player is ready', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 0, 0, { owner: 0 });
    addUnit(s, 's2', 'specialist', 1, 0, { owner: 1 });
    addUnit(s, 'a1', 'trooper', 19, 19);
    const both: Slot[] = [0, 1];
    run(s, { type: 'endTurn', ready: true }, ctx(0, both));
    expect(s.activeTeam).toBe('xcom');
    const events = run(s, { type: 'endTurn', ready: true }, ctx(1, both));
    expect(events.filter((e) => e.t === 'turnBegan').map((e) => (e as { team: string }).team)).toEqual(['alien', 'xcom']);
    expect(s.turn).toBe(2);
  });

  it('a disconnected partner does not block the end of turn', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 0, 0, { owner: 0 });
    addUnit(s, 's2', 'specialist', 1, 0, { owner: 1 });
    addUnit(s, 'a1', 'trooper', 19, 19);
    run(s, { type: 'endTurn', ready: true }, ctx(0, [0]));
    expect(s.turn).toBe(2);
  });

  it('only the player with the command gives orders, until they pass it', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 0, 0, { owner: 0 });
    addUnit(s, 's2', 'specialist', 1, 0, { owner: 1 });
    addUnit(s, 'a1', 'trooper', 19, 19);
    s.command = 0;
    const both: Slot[] = [0, 1];
    expect(executeCommand(s, { type: 'move', unit: 's2', to: { x: 1, y: 2 } }, ctx(1, both))).toMatchObject({ ok: false, error: expect.stringMatching(/mando/) });
    expect(executeCommand(s, { type: 'pass' }, ctx(1, both)).ok).toBe(false);
    expect(run(s, { type: 'pass' }, ctx(0, both))).toEqual([{ t: 'commandPassed', to: 1 }]);
    run(s, { type: 'move', unit: 's2', to: { x: 1, y: 2 } }, ctx(1, both));
    expect(s.units.find((u) => u.id === 's2')!.pos).toEqual({ x: 1, y: 2 });
    // Alone, there's nobody to pass it to.
    expect(executeCommand(s, { type: 'pass' }, ctx(1, [1])).ok).toBe(false);
    // Nor to a partner with nothing left to do.
    s.units.find((u) => u.id === 's1')!.ap = 0;
    expect(executeCommand(s, { type: 'pass' }, ctx(1, both))).toMatchObject({ ok: false, error: expect.stringMatching(/acciones/) });
  });

  it('ending the turn hands the command over, and the other player opens the next turn', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 0, 0, { owner: 0 });
    addUnit(s, 's2', 'specialist', 1, 0, { owner: 1 });
    addUnit(s, 'a1', 'trooper', 19, 19);
    s.command = 0;
    const both: Slot[] = [0, 1];
    run(s, { type: 'endTurn', ready: true }, ctx(0, both));
    expect(s).toMatchObject({ activeTeam: 'xcom', command: 1 });
    // Done players can't be handed the command back.
    expect(executeCommand(s, { type: 'pass' }, ctx(1, both)).ok).toBe(false);
    run(s, { type: 'endTurn', ready: true }, ctx(1, both));
    expect(s).toMatchObject({ turn: 2, activeTeam: 'xcom', command: 1 });
  });

  it('a holder out of actions passes on their own; a holder who left blocks nobody', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 0, 0, { owner: 0, ap: 1 });
    addUnit(s, 's2', 'specialist', 1, 0, { owner: 1 });
    addUnit(s, 'a1', 'trooper', 19, 19);
    s.command = 0;
    const both: Slot[] = [0, 1];
    run(s, { type: 'move', unit: 's1', to: { x: 0, y: 2 } }, ctx(0, both));
    expect(s.command).toBe(1);
    // Player 1 drops out: player 0 can act and takes the command.
    s.units.find((u) => u.id === 's1')!.ap = 2;
    run(s, { type: 'move', unit: 's1', to: { x: 0, y: 3 } }, ctx(0, [0]));
    expect(s.command).toBe(0);
  });

  it('killing the last alien wins an elimination mission', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 0, 0);
    const a = addUnit(s, 'a1', 'trooper', 5, 5);
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(a, 99, 0);
    sim.checkOutcome();
    expect(s.outcome).toBe('victory');
    expect(executeCommand(s, { type: 'endTurn', ready: true }, ctx()).ok).toBe(false);
  });
});

describe('maps', () => {
  it('every map parses and every pod and the objective are reachable from the spawn', () => {
    for (const def of Object.values(MAPS)) {
      const map = parseMap(def);
      expect(map.spawns.length).toBeGreaterThanOrEqual(6);
      const s = createMission({ map: { kind: 'handmade', id: def.id }, slots: [0, 1], rng: new Rng(3), kind: 'recovery' });
      const scout = { ...s.units[0]!, team: 'xcom' as const };
      s.units = [scout];
      const reach = computeReach(s, scout, 10_000);
      const targets = [...Object.values(map.podSpawns).flat(), ...(map.item ? [map.item] : []), ...map.evacZone];
      for (const t of targets) expect(reach.cost.has(t.y * s.width + t.x), `${t.x},${t.y}`).toBe(true);
    }
  });
});

describe('alien AI', () => {
  it('scampering aliens do not end up next to a soldier', () => {
    for (let seed = 1; seed <= 6; seed++) {
      const s = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(seed) });
      const sim = new Sim(s, new Rng(seed), [0]);
      const spots = [{ x: 12, y: 15 }, { x: 13, y: 15 }, { x: 14, y: 16 }, { x: 11, y: 16 }, { x: 15, y: 15 }, { x: 10, y: 15 }];
      s.units.filter((u) => u.team === 'xcom').forEach((u, i) => (u.pos = spots[i]!));
      s.concealed = false;
      sim.activatePods(s.pods.map((p) => p.id));
      for (const a of s.units.filter((u) => u.team === 'alien' && u.alive && u.template !== 'lancer')) {
        const nearest = Math.min(...s.units.filter((u) => u.team === 'xcom' && u.alive).map((x) => Math.hypot(x.pos.x - a.pos.x, x.pos.y - a.pos.y)));
        expect(nearest, `seed ${seed}: ${a.id} at ${a.pos.x},${a.pos.y}`).toBeGreaterThanOrEqual(2);
      }
    }
  });
});

describe('replication', () => {
  /** A crude bot that tries every ability it has, with valid-looking targets. */
  function botCommand(s: GameState, slot: Slot, rng: Rng): Command {
    const mine = s.units.filter((u) => u.alive && u.team === 'xcom' && u.owner === slot && u.ap > 0);
    if (!mine.length || rng.int(10) === 0) return { type: 'endTurn', ready: true };
    if (rng.int(12) === 0) return { type: 'pass' };
    const u = rng.pick(mine);
    const aliens = s.units.filter((a) => a.alive && a.team === 'alien');
    if (rng.int(3) === 0 && aliens.length) {
      const id = rng.pick(abilitiesOf(u));
      const def = ABILITIES[id];
      switch (def.target) {
        case 'enemy':
        case 'ally':
        case 'corpse': {
          const t = abilityTargets(s, u, id);
          return ability(u.id, id, { target: (t.length ? rng.pick(t) : rng.pick(aliens)).id });
        }
        case 'melee': {
          const t = isAttack(id) ? meleeTargets(s, u, id) : [];
          return ability(u.id, id, { target: (t.length ? rng.pick(t).target : rng.pick(aliens)).id });
        }
        case 'tile':
          return ability(u.id, id, { tile: rng.pick(aliens).pos });
        default:
          return ability(u.id, id);
      }
    }
    const goal =
      (s.mission.item?.carrier === u.id ? s.mission.evacZone[0] : s.mission.item?.pos) ?? (aliens.length ? rng.pick(aliens).pos : u.pos);
    const dx = Math.sign(goal.x - u.pos.x) * rng.range(1, 6);
    const dy = Math.sign(goal.y - u.pos.y) * rng.range(1, 6);
    return { type: 'move', unit: u.id, to: { x: u.pos.x + dx, y: u.pos.y + dy } };
  }

  /** Gives every soldier a random pick of their class's perks (all tiers). */
  function veterans(s: GameState, rng: Rng): void {
    for (const u of s.units) {
      if (u.team !== 'xcom' || u.template === 'vip') continue;
      for (const tier of [1, 2, 3, 4, 5] as const) {
        const options = perksFor(u.template as ClassId, tier);
        const perk = rng.pick(options);
        u.perks.push(perk.id);
        for (const [c, n] of Object.entries(perk.charges ?? {})) u.charges[c as keyof typeof u.charges] = (u.charges[c as keyof typeof u.charges] ?? 0) + n;
      }
    }
  }

  it('clients replaying the event stream end with exactly the host state', () => {
    const kinds = ['elimination', 'recovery', 'sabotage', 'hack', 'rescue'] as const;
    const biomes = ['city', 'wilds', 'facility'] as const;
    for (let seed = 1; seed <= 15; seed++) {
      const rng = new Rng(seed);
      const map = seed <= 5 ? { kind: 'handmade' as const, id: 'plaza' } : { kind: 'generated' as const, biome: biomes[seed % 3]!, seed };
      const pods: TemplateId[][] = [['sectoid', 'trooper'], ['lancer', 'sectoid'], ['mec', 'officer'], ['xenoid', 'zombie']];
      const host = createMission({ map, slots: [0, 1], rng, kind: kinds[seed % kinds.length]!, difficulty: 1 + (seed % 4), pods: seed % 2 ? pods : undefined });
      if (seed % 3 !== 0) veterans(host, rng);
      const client = cloneState(host);
      for (let i = 0; i < 700 && !host.outcome; i++) {
        // Mostly whoever holds the command, sometimes the other (whose orders are refused).
        const slot = (rng.int(5) === 0 ? i % 2 : host.command ?? i % 2) as Slot;
        const r = executeCommand(host, botCommand(host, slot, rng), { slot, connected: [0, 1], rng });
        if (r.ok) for (const e of r.events) applyEvent(client, e);
      }
      expect(client).toEqual(host);
    }
  });
});
