import { describe, expect, it } from 'vitest';
import { abilityBlocker, abilityTargets, psiChance } from '../src/abilities';
import { previewShot } from '../src/aim';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import type { Command } from '../src/protocol';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import type { AbilityId, GameState, PerkId, Slot } from '../src/types';
import { addUnit, stateFrom } from './helpers';

const ctx = (seed = 1, slot: Slot = 0) => ({ slot, connected: [0, 1] as Slot[], rng: new Rng(seed) });
const field = (w = 20, h = 20) => stateFrom(Array.from({ length: h }, () => '.'.repeat(w)));

function run(s: GameState, cmd: Command, seed = 1): GameEvent[] {
  const r = executeCommand(s, cmd, ctx(seed));
  if (!r.ok) throw new Error(r.error);
  return r.events;
}

const use = (unit: string, ability: AbilityId, extra: { target?: string; tile?: { x: number; y: number } } = {}): Command => ({ type: 'ability', unit, ability, ...extra });

/** A soldier with perks, plus a partner soldier that keeps the XCOM turn going. */
function squad(s: GameState, id: string, template: Parameters<typeof addUnit>[2], x: number, y: number, perks: PerkId[] = [], extra = {}) {
  if (!s.units.some((u) => u.id === 'keeper')) addUnit(s, 'keeper', 'specialist', 0, s.height - 1, { owner: 1 });
  return addUnit(s, id, template, x, y, { perks, ...extra });
}

describe('cooldowns and free actions', () => {
  it('Run and Gun is free, gives an action and then cools down', () => {
    const s = field();
    const u = squad(s, 's1', 'assault', 2, 2, ['runAndGun']);
    addUnit(s, 'a1', 'trooper', 19, 2);
    run(s, use('s1', 'runAndGun'));
    expect(u.ap).toBe(3);
    expect(u.cooldowns.runAndGun).toBe(4);
    expect(abilityBlocker(s, u, 'runAndGun')).toMatch(/Disponible en 4/);
    applyEvent(s, { t: 'turnBegan', team: 'xcom', turn: 2 });
    expect(u.cooldowns.runAndGun).toBe(3);
  });

  it('Lightning Hands fires the pistol without spending an action; Quickdraw keeps the turn going', () => {
    const s = field();
    const u = squad(s, 's1', 'sharpshooter', 2, 2, ['lightningHands', 'quickdraw']);
    addUnit(s, 'a1', 'trooper', 6, 2, { hp: 99, maxHp: 99 });
    run(s, use('s1', 'lightningHands', { target: 'a1' }));
    expect(u.ap).toBe(2);
    run(s, use('s1', 'pistol', { target: 'a1' }));
    expect(u.ap).toBe(1);
  });

  it('Salvo: a grenade no longer ends the turn', () => {
    const s = field();
    const u = squad(s, 's1', 'grenadier', 2, 2, ['salvo']);
    addUnit(s, 'a1', 'trooper', 10, 2);
    run(s, use('s1', 'launch', { tile: { x: 10, y: 2 } }));
    expect(u.ap).toBe(1);
  });
});

describe('attack variants', () => {
  it('Rapid Fire shoots twice with −15 aim; Chain Shot only repeats on a hit', () => {
    const s = field();
    squad(s, 's1', 'assault', 2, 2, ['rapidFire']);
    addUnit(s, 'a1', 'trooper', 6, 2, { hp: 99, maxHp: 99 });
    const shooter = s.units.find((u) => u.id === 's1')!;
    const target = s.units.find((u) => u.id === 'a1')!;
    expect(previewShot(s, shooter, target, { ability: 'rapidFire' })!.mods).toContainEqual({ label: 'Fuego rápido', value: -15 });
    const events = run(s, use('s1', 'rapidFire', { target: 'a1' }));
    expect(events.filter((e) => e.t === 'shot')).toHaveLength(2);
    expect(shooter.ammo).toBe(2);

    for (let seed = 1; seed < 20; seed++) {
      const t = field();
      squad(t, 's1', 'grenadier', 2, 2, ['chainShot']);
      addUnit(t, 'a1', 'trooper', 6, 2, { hp: 99, maxHp: 99 });
      const shots = run(t, use('s1', 'chainShot', { target: 'a1' }), seed).filter((e) => e.t === 'shot') as Extract<GameEvent, { t: 'shot' }>[];
      expect(shots.length).toBe(shots[0]!.result === 'miss' ? 1 : 2);
    }
  });

  it('Rupture leaves the target taking +3 from every hit; Holo-targeting marks it for allies', () => {
    const s = field();
    const g = squad(s, 's1', 'grenadier', 2, 2, ['rupture', 'holoTargeting']);
    const a = addUnit(s, 'a1', 'trooper', 3, 2, { hp: 99, maxHp: 99 });
    for (let seed = 1; seed < 30 && !a.ruptured; seed++) {
      g.ap = 2;
      delete g.cooldowns.rupture;
      g.ammo = 3;
      run(s, use('s1', 'rupture', { target: 'a1' }), seed);
    }
    expect(a.ruptured).toBe(3);
    expect(a.marked).toBe(15);
    const ally = addUnit(s, 's2', 'specialist', 2, 4);
    expect(previewShot(s, ally, a)!.mods).toContainEqual({ label: 'Holo-objetivo', value: 15 });
    const sim = new Sim(s, new Rng(1), [0]);
    const before = a.hp;
    sim.damage(a, 1, 0);
    expect(before - a.hp).toBe(4);
  });

  it('Face Off fires the pistol at every enemy in sight', () => {
    const s = field();
    squad(s, 's1', 'sharpshooter', 2, 2, ['faceoff']);
    for (const [i, x] of [5, 8, 11].entries()) addUnit(s, `a${i}`, 'trooper', x, 2, { hp: 99, maxHp: 99 });
    const events = run(s, use('s1', 'faceoff'));
    expect(events.filter((e) => e.t === 'shot')).toHaveLength(3);
  });
});

describe('reactions', () => {
  it('Kill Zone keeps firing, once per enemy; plain overwatch fires once', () => {
    const s = field();
    s.activeTeam = 'alien';
    const sniper = addUnit(s, 's1', 'sharpshooter', 2, 10, { overwatch: true, killZone: true, ammo: 3 });
    const a = addUnit(s, 'a1', 'trooper', 15, 2, { hp: 99, maxHp: 99 });
    const b = addUnit(s, 'a2', 'trooper', 15, 18, { hp: 99, maxHp: 99 });
    const sim = new Sim(s, new Rng(1), [0]);
    sim.moveAlong(a, [{ x: 14, y: 2 }, { x: 13, y: 2 }], 1);
    sim.moveAlong(b, [{ x: 14, y: 18 }, { x: 13, y: 18 }], 1);
    expect(sim.events.filter((e) => e.t === 'shot')).toHaveLength(2);
    expect(sniper.overwatch).toBe(true);
  });

  it('Shadowstep never draws reaction fire', () => {
    const s = field();
    squad(s, 's1', 'assault', 0, 10, ['shadowstep']);
    addUnit(s, 'a1', 'trooper', 10, 0, { overwatch: true });
    const events = run(s, { type: 'move', unit: 's1', to: { x: 6, y: 10 } });
    expect(events.some((e) => e.t === 'shot')).toBe(false);
  });

  it('a suppressed enemy that moves gets shot, and the suppression ends', () => {
    const s = field();
    const g = squad(s, 's1', 'grenadier', 2, 2, ['suppression']);
    const a = addUnit(s, 'a1', 'trooper', 8, 2, { hp: 99, maxHp: 99 });
    run(s, use('s1', 'suppression', { target: 'a1' }));
    expect(a.suppressedBy).toBe('s1');
    expect(previewShot(s, a, g)!.mods).toContainEqual({ label: 'Suprimido', value: -50 });
    s.activeTeam = 'alien';
    const sim = new Sim(s, new Rng(1), [0]);
    sim.moveAlong(a, [{ x: 9, y: 2 }], 1);
    expect(sim.events.some((e) => e.t === 'shot' && e.reaction && e.unit === 's1')).toBe(true);
    expect(a.suppressedBy).toBeNull();
  });

  it('Covering Fire shoots at an enemy who attacks', () => {
    const s = field();
    addUnit(s, 's1', 'specialist', 2, 2, { perks: ['coveringFire'], overwatch: true });
    const a = addUnit(s, 'a1', 'trooper', 8, 2, { hp: 99, maxHp: 99, ap: 2 });
    s.activeTeam = 'alien';
    const sim = new Sim(s, new Rng(1), [0]);
    sim.perform(a, 'shoot', { target: s.units[0]! });
    const shots = sim.events.filter((e) => e.t === 'shot') as Extract<GameEvent, { t: 'shot' }>[];
    expect(shots[0]).toMatchObject({ unit: 's1', reaction: true });
  });
});

describe('support', () => {
  it('Combat Protocol never misses, ignores armour and hits machines harder', () => {
    const s = field();
    squad(s, 's1', 'specialist', 2, 2, ['combatProtocol'], { charges: { combatProtocol: 2, medkit: 2, grenade: 1 } });
    const mec = addUnit(s, 'a1', 'mec', 8, 2);
    run(s, use('s1', 'combatProtocol', { target: 'a1' }));
    expect(mec.hp).toBe(7 - 4);
  });

  it('Threat Assessment: Aid Protocol also puts the ally on overwatch', () => {
    const s = field();
    squad(s, 's1', 'specialist', 2, 2, ['threatAssessment']);
    const ally = addUnit(s, 's2', 'sharpshooter', 4, 2, { ap: 0 });
    addUnit(s, 'a1', 'trooper', 19, 19);
    run(s, use('s1', 'aid', { target: 's2' }));
    expect(ally.aided).toBeGreaterThan(0);
    expect(ally.overwatch).toBe(true);
  });

  it('Restoration heals and cleanses everyone nearby', () => {
    const s = field();
    squad(s, 's1', 'specialist', 2, 2, ['restoration']);
    const ally = addUnit(s, 's2', 'assault', 4, 2, { hp: 2, disoriented: 1, panicked: true });
    addUnit(s, 'a1', 'trooper', 19, 19);
    run(s, use('s1', 'restoration'));
    expect(ally.hp).toBe(4);
    expect(ally.disoriented).toBe(0);
    expect(ally.panicked).toBe(false);
  });

  it('Capacitor Discharge hurts everyone in the area and leaves cover standing', () => {
    const s = field();
    squad(s, 's1', 'specialist', 2, 2, ['capacitorDischarge']);
    const a = addUnit(s, 'a1', 'trooper', 8, 2);
    s.tiles[2 * 20 + 9] = 'low';
    run(s, use('s1', 'discharge', { tile: { x: 8, y: 2 } }));
    expect(a.alive).toBe(false);
    expect(s.tiles[2 * 20 + 9]).toBe('low');
  });
});

describe('kills that pay back', () => {
  it('Implacable: a killing blade strike gives an action back, once per turn', () => {
    const s = field();
    const u = squad(s, 's1', 'assault', 2, 2, ['implacable']);
    addUnit(s, 'a1', 'trooper', 3, 2, { hp: 1, maxHp: 1 });
    addUnit(s, 'a2', 'trooper', 19, 19);
    for (let seed = 1; seed < 20 && !u.refunded; seed++) {
      const r = executeCommand(s, use('s1', 'slash', { target: 'a1' }), ctx(seed));
      if (!r.ok) break;
    }
    expect(u.refunded).toBe(true);
    expect(u.ap).toBe(1);
  });

  it('Death From Above: a kill from higher ground gives an action back', () => {
    const rows = Array.from({ length: 10 }, () => '.'.repeat(10));
    const heights = Array.from({ length: 10 }, (_, y) => (y < 3 ? '1111000000' : '0000000000'));
    const s = stateFrom(rows, heights);
    const u = squad(s, 's1', 'sharpshooter', 1, 1, ['deathFromAbove']);
    addUnit(s, 'a1', 'trooper', 7, 7, { hp: 1, maxHp: 1 });
    addUnit(s, 'a2', 'trooper', 9, 0);
    for (let seed = 1; seed < 20 && !u.refunded; seed++) {
      u.ap = 2;
      u.ammo = 3;
      const target = s.units.find((x) => x.id === 'a1')!;
      if (!target.alive) break;
      executeCommand(s, use('s1', 'shoot', { target: 'a1' }), ctx(seed));
    }
    expect(u.refunded).toBe(true);
  });
});

describe('psionics', () => {
  function sectoidField() {
    const s = field();
    s.activeTeam = 'alien';
    const soldier = addUnit(s, 's1', 'assault', 2, 2, { will: 40 });
    addUnit(s, 's2', 'specialist', 2, 6);
    const sectoid = addUnit(s, 'a1', 'sectoid', 9, 2, { podId: 'p1', ap: 2 });
    s.pods.push({ id: 'p1', active: true, unitIds: ['a1'], patrol: [], patrolIndex: 0 });
    return { s, soldier, sectoid };
  }

  it('will resists mindspin; machines are immune', () => {
    const { s, soldier } = sectoidField();
    expect(psiChance(soldier)).toBe(70);
    expect(psiChance({ ...soldier, will: 60 })).toBe(50);
    expect(psiChance(addUnit(s, 'a9', 'mec', 0, 0))).toBe(0);
  });

  it('a mind-controlled soldier fights for the aliens until the sectoid dies', () => {
    let done = false;
    for (let seed = 1; seed < 200 && !done; seed++) {
      const { s, soldier, sectoid } = sectoidField();
      const sim = new Sim(s, new Rng(seed), [0]);
      sim.perform(sectoid, 'mindspin', { target: soldier });
      const psi = sim.events.find((e) => e.t === 'psi') as Extract<GameEvent, { t: 'psi' }>;
      if (psi.effect !== 'controlled') continue;
      expect(soldier.team).toBe('alien');
      expect(soldier.controlledBy).toBe('a1');
      sim.damage(sectoid, 99, 0);
      expect(soldier.team).toBe('xcom');
      expect(soldier.controlledBy).toBeNull();
      done = true;
    }
    expect(done).toBe(true);
  });

  it('panic costs the next turn; disorientation lowers aim and forbids explosives', () => {
    const { s, soldier } = sectoidField();
    applyEvent(s, { t: 'psi', unit: 'a1', target: 's1', success: true, effect: 'panicked', chance: 70 });
    applyEvent(s, { t: 'turnBegan', team: 'xcom', turn: 2 });
    expect(soldier.ap).toBe(0);
    expect(soldier.hunkered).toBe(true);
    applyEvent(s, { t: 'psi', unit: 'a1', target: 's2', success: true, effect: 'disoriented', chance: 70 });
    const spec = s.units.find((u) => u.id === 's2')!;
    spec.ap = 2;
    expect(abilityBlocker(s, spec, 'grenade')).toMatch(/Desorientado/);
    expect(previewShot(s, spec, s.units.find((u) => u.id === 'a1')!)!.mods).toContainEqual({ label: 'Desorientado', value: -20 });
  });

  it('sectoids raise corpses as zombies that join their pod and claw at soldiers', () => {
    const { s, sectoid } = sectoidField();
    const corpse = addUnit(s, 'a5', 'trooper', 7, 4, { alive: false, hp: 0 });
    expect(abilityTargets(s, sectoid, 'reanimate')).toContain(corpse);
    const host = cloneState(s);
    const sim = new Sim(host, new Rng(1), [0]);
    sim.perform(host.units.find((u) => u.id === 'a1')!, 'reanimate', { target: host.units.find((u) => u.id === 'a5')! });
    const zombie = host.units.find((u) => u.template === 'zombie')!;
    expect(zombie.alive).toBe(true);
    expect(host.pods[0]!.unitIds).toContain(zombie.id);
    expect(host.units.find((u) => u.id === 'a5')!.reanimated).toBe(true);
    for (const e of sim.events) applyEvent(s, e);
    expect(s).toEqual(host);
    // Next alien turn, the zombie shambles over and attacks.
    zombie.pos = { x: 3, y: 3 };
    const turn = new Sim(host, new Rng(2), [0]);
    turn.runAlienTurn();
    expect(turn.events.some((e) => e.t === 'shot' && e.unit === zombie.id && e.ability === 'claw')).toBe(true);
  });
});

describe('equipment in the field', () => {
  it('smoke makes everyone inside harder to hit and clears after two turns', () => {
    const s = field();
    squad(s, 's1', 'assault', 2, 2, [], { gear: ['smokeGrenade'], charges: { grenade: 1, smoke: 1 } });
    const a = addUnit(s, 'a1', 'trooper', 10, 2);
    const ally = addUnit(s, 's2', 'specialist', 4, 4);
    run(s, use('s1', 'smoke', { tile: { x: 4, y: 4 } }));
    expect(previewShot(s, a, ally)!.mods).toContainEqual({ label: 'Humo', value: -20 });
    applyEvent(s, { t: 'turnBegan', team: 'xcom', turn: 2 });
    expect(s.smoke).toHaveLength(1);
    applyEvent(s, { t: 'turnBegan', team: 'xcom', turn: 3 });
    expect(s.smoke).toHaveLength(0);
  });

  it('a flashbang disorients the enemies caught in it, without damage', () => {
    const s = field();
    squad(s, 's1', 'specialist', 2, 2, [], { gear: ['flashbang'], charges: { grenade: 1, medkit: 2, flashbang: 1 } });
    const a = addUnit(s, 'a1', 'trooper', 9, 2);
    const mec = addUnit(s, 'a2', 'mec', 9, 3);
    run(s, use('s1', 'flashbang', { tile: { x: 9, y: 2 } }));
    expect(a.disoriented).toBe(1);
    expect(a.hp).toBe(a.maxHp);
    expect(mec.disoriented).toBe(0);
  });

  it('a first-aid kit heals an adjacent ally; armour-piercing rounds ignore armour', () => {
    const s = field();
    squad(s, 's1', 'assault', 2, 2, [], { gear: ['firstAid'], charges: { grenade: 1, firstAid: 1 } });
    const hurt = addUnit(s, 's2', 'sharpshooter', 3, 2, { hp: 1 });
    addUnit(s, 'a1', 'trooper', 19, 19);
    run(s, use('s1', 'firstAid', { target: 's2' }));
    expect(hurt.hp).toBe(5);

    const t = field();
    const gunner = addUnit(t, 'g1', 'specialist', 2, 2, { mods: { ...addUnit(t, 'tmp', 'specialist', 0, 0).mods, pierce: 1 } });
    t.units = t.units.filter((u) => u.id !== 'tmp');
    const mec = addUnit(t, 'a1', 'mec', 4, 2);
    const sim = new Sim(t, new Rng(1), [0]);
    sim.damage(mec, 3, 0, gunner, gunner.mods.pierce);
    expect(mec.hp).toBe(7 - 3);
  });
});
