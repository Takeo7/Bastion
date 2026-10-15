import { describe, expect, it } from 'vitest';
import { createCampaign, FACILITIES, missionInfo, turnDay, type CampaignState } from '../src/campaign';
import { decorOf } from '../src/decor';
import { DIRS4 } from '../src/grid';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { ALARM_NAMES } from '../src/lore';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import { createMission } from '../src/setup';
import type { Command } from '../src/protocol';
import type { GameState, Slot } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// The playtest of 2026-10-15 (docs/PRUEBA-2026-10-15.md).

const field = (w = 20, h = 20) => stateFrom(Array.from({ length: h }, () => '.'.repeat(w)));
const ctx = (slot: Slot, connected: Slot[] = [0, 1]) => ({ slot, connected, rng: new Rng(1) });
const run = (s: GameState, cmd: Command, slot: Slot, connected?: Slot[]) => {
  const r = executeCommand(s, cmd, ctx(slot, connected));
  if (!r.ok) throw new Error(r.error);
  return r.events;
};

describe('turns one soldier at a time (#9)', () => {
  function two() {
    const s = field();
    const a = addUnit(s, 'a', 'rookie', 2, 2, { owner: 0 });
    const b = addUnit(s, 'b', 'rookie', 2, 4, { owner: 0 });
    const c = addUnit(s, 'c', 'rookie', 4, 2, { owner: 1 });
    addUnit(s, 'z', 'zombie', 19, 19);
    s.command = 0;
    return { s, a, b, c };
  }

  it('the soldier in play is finished first; done, the partner plays one of theirs', () => {
    const { s, a } = two();
    run(s, { type: 'move', unit: 'a', to: { x: 3, y: 2 } }, 0);
    expect(s.activeUnit).toBe('a');
    expect(executeCommand(s, { type: 'move', unit: 'b', to: { x: 3, y: 4 } }, ctx(0))).toEqual({ ok: false, error: `Primero termina con ${a.name}, o cede el turno.` });
    run(s, { type: 'move', unit: 'a', to: { x: 4, y: 3 } }, 0);
    expect(a.ap).toBe(0);
    expect(s.activeUnit).toBeUndefined();
    expect(s.command).toBe(1);
    // The partner's soldier, then back.
    run(s, { type: 'ability', unit: 'c', ability: 'overwatch' }, 1);
    expect(s.command).toBe(0);
    run(s, { type: 'move', unit: 'b', to: { x: 3, y: 4 } }, 0);
  });

  it('handing the turn over still works, and the soldier left half-done waits for later', () => {
    const { s, a } = two();
    run(s, { type: 'move', unit: 'a', to: { x: 3, y: 2 } }, 0);
    run(s, { type: 'pass' }, 0);
    expect(s.command).toBe(1);
    expect(s.activeUnit).toBeUndefined();
    expect(a.ap).toBe(1);
  });

  it('alone, or with a partner who has nobody left to move, it is as before', () => {
    const { s } = two();
    run(s, { type: 'move', unit: 'a', to: { x: 3, y: 2 } }, 0, [0]);
    run(s, { type: 'move', unit: 'b', to: { x: 3, y: 4 } }, 0, [0]);
    const t = two();
    t.c.ap = 0;
    run(t.s, { type: 'move', unit: 'a', to: { x: 3, y: 2 } }, 0);
    run(t.s, { type: 'move', unit: 'b', to: { x: 3, y: 4 } }, 0);
  });

  it('a client replaying it ends with the host state', () => {
    const { s } = two();
    const client = cloneState(s);
    const events: GameEvent[] = [
      ...run(s, { type: 'move', unit: 'a', to: { x: 3, y: 2 } }, 0),
      ...run(s, { type: 'move', unit: 'a', to: { x: 4, y: 3 } }, 0),
      ...run(s, { type: 'ability', unit: 'c', ability: 'overwatch' }, 1),
    ];
    for (const e of events) applyEvent(client, e);
    expect(client).toEqual(s);
  });
});

describe('the bitten and the Cuarentena (#13)', () => {
  function bus(quarantine: boolean | undefined) {
    const s = field();
    s.mission = { ...s.mission, kind: 'recovery', evacZone: [{ x: 0, y: 0 }, { x: 1, y: 0 }], ...(quarantine === undefined ? {} : { quarantine }) };
    const bitten = addUnit(s, 's1', 'rookie', 0, 0, { infected: true });
    const fine = addUnit(s, 's2', 'rookie', 1, 0);
    addUnit(s, 'z', 'zombie', 19, 19);
    return { s, bitten, fine };
  }

  it('without a Cuarentena a bitten soldier does not board La Lata, nor asks to', () => {
    const { s, bitten, fine } = bus(false);
    expect(executeCommand(s, { type: 'evac', unit: 's1' }, ctx(0, [0]))).toEqual({ ok: false, error: 'Con un mordisco no se sube a La Lata sin una Cuarentena en el Bastión.' });
    run(s, { type: 'evac', unit: 's2' }, 0, [0]);
    expect(fine.evacuated).toBe(true);
    expect(bitten.lost).toBe(true);
  });

  it('with one, or out of the campaign, they board', () => {
    for (const q of [true, undefined]) {
      const { s, bitten } = bus(q);
      run(s, { type: 'evac', unit: 's1' }, 0, [0]);
      expect(bitten.evacuated).toBe(true);
    }
  });

  it('a bitten soldier loses a point of health at every turn of the squad, armour or not', () => {
    const { s, bitten, fine } = bus(true);
    bitten.armor = 2;
    const hp = bitten.hp;
    const sim = new Sim(s, new Rng(1), [0]);
    sim.runAlienTurn();
    expect(bitten.hp).toBe(hp - 1);
    expect(fine.hp).toBe(fine.maxHp);
  });

  it('the Cuarentena is built like any facility, and its levels hold the fever off a day each', () => {
    const c: CampaignState = createCampaign([0], new Rng(1));
    expect(FACILITIES.quarantine).toMatchObject({ name: 'Cuarentena', unique: true, level: 1 });
    const s = c.soldiers[0]!;
    s.infection = { since: c.day };
    const before = turnDay(c, s)!;
    Object.assign(c.base.plots.find((p) => p.terrain === 'clear' && !p.building)!, { building: 'quarantine', level: 3 });
    expect(turnDay(c, s)).toBe(before + 2);
  });
});

describe('operations say who and what (#14, #20)', () => {
  it('a rescue names who is held and a sabotage what makes the noise; the map is called as the operation', () => {
    let rescue = false;
    let sabotage = false;
    for (let seed = 1; seed <= 40 && !(rescue && sabotage); seed++) {
      const c = createCampaign([0], new Rng(seed));
      for (const o of c.offers.filter((x) => !x.story && !x.final)) {
        const info = missionInfo(c, o);
        if (o.kind === 'rescue') {
          rescue = true;
          expect(o.vip).toBeTruthy();
          expect(info.vipName).toBe(o.vip);
          const s = createMission({ map: o.map, slots: [0], rng: new Rng(1), kind: o.kind, vipName: info.vipName, campaignMission: { id: o.id, name: o.name } });
          expect(s.units.find((u) => u.captive)!.name).toBe(o.vip);
          expect(s.mapName).toBe(o.name);
        }
        if (o.kind === 'sabotage') {
          sabotage = true;
          expect(['police', 'ambulance', 'factory']).toContain(o.alarm);
          const s = createMission({ map: o.map, slots: [0], rng: new Rng(1), kind: o.kind, alarm: info.alarm, objectiveName: ALARM_NAMES[info.alarm!] });
          expect(s.mission.alarm).toBe(o.alarm);
          expect(s.units.find((u) => u.objective)!.name).toBe(ALARM_NAMES[o.alarm!]);
        }
      }
    }
    expect(rescue && sabotage).toBe(true);
  });
});

describe('the siren a sabotage silences (#14)', () => {
  it('a patrol car or an ambulance blocks the tile of its nose, facing it; a factory alarm takes one tile', () => {
    for (let seed = 1; seed <= 6; seed++) {
      const at = (alarm: 'police' | 'ambulance' | 'factory') => createMission({ map: { kind: 'generated', biome: 'facility', seed }, slots: [0], rng: new Rng(1), kind: 'sabotage', alarm });
      for (const alarm of ['police', 'ambulance'] as const) {
        const s = at(alarm);
        const spot = s.units.find((u) => u.objective)!.pos;
        const noses = s.decor!.flatMap((d, i) => (decorOf(d)?.kind === 'sirenCar' ? [{ i, facing: decorOf(d)!.facing }] : []));
        expect(noses, `${alarm} ${seed}`).toHaveLength(1);
        const { i, facing } = noses[0]!;
        expect(s.tiles[i]).toBe('high');
        expect({ x: i % s.width, y: Math.floor(i / s.width) }).toEqual({ x: spot.x + DIRS4[facing]!.x, y: spot.y + DIRS4[facing]!.y });
      }
      expect(at('factory').decor!.some((d) => decorOf(d)?.kind === 'sirenCar')).toBe(false);
    }
  });
});

describe('medicines where medicines are kept (#15)', () => {
  it('a medicine box is never out in the street', () => {
    for (const biome of ['city', 'wilds', 'facility'] as const) {
      for (let seed = 1; seed <= 6; seed++) {
        const s = createMission({ map: { kind: 'generated', biome, seed }, slots: [0], rng: new Rng(1), kind: 'recovery' });
        s.decor!.forEach((d, i) => {
          if (decorOf(d)?.kind === 'lootMedical') expect(s.ground?.[i], `${biome} ${seed}`).toBe(2);
        });
      }
    }
  });
});
