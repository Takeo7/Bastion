import { describe, expect, it } from 'vitest';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { Rng } from '../src/rng';
import { executeCommand } from '../src/rules';
import {
  fighterStats,
  learn,
  learnedFrom,
  NO_SKILLS,
  nextLevelXp,
  pendingPicks,
  picksBlocker,
  recruitSkills,
  SKILL_IDS,
  SKILL_TALENTS,
  SKILL_XP,
  skillLevel,
  skillMods,
  TALENT_PAIRS,
  veteranAp,
  veteranHp,
  veterancy,
  veterancyRank,
  VETERANCY,
  veteranSkills,
  veteranWill,
  type Skills,
} from '../src/skills';
import { addUnit, stateFrom } from './helpers';

// Fase 9, «Se aprende haciendo» (docs/FASE9.md).

const at = (levels: Partial<Record<keyof Skills, number>>): Skills => {
  const out = { ...NO_SKILLS };
  for (const [id, level] of Object.entries(levels) as [keyof Skills, number][]) out[id] = level ? SKILL_XP[level - 1]! : 0;
  return out;
};

describe('skills rise with use', () => {
  it('a level asks for 3, 8, 15, 24 and 35 of experience', () => {
    expect([0, 9, 10, 24, 25, 49, 50, 84, 85, 129, 130, 999].map(skillLevel)).toEqual([0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5]);
    expect(nextLevelXp(30)).toBe(50);
    expect(nextLevelXp(140)).toBeNull();
  });

  it('deeds turn into experience, all of it (no cap: Arturo, 16/10), and a bonus adds its share rounded up', () => {
    expect(learnedFrom({ hit: 2, gunKill: 1, healOther: 1, healSelf: 1, search: 2, fix: 1, blast: 1 })).toEqual({ shooting: 3, melee: 0, medicine: 3, craft: 5 });
    expect(learnedFrom({ hit: 9, gunKill: 4 }).shooting).toBe(13);
    expect(learnedFrom({ hit: 9, gunKill: 4, search: 1 }, 0.2)).toMatchObject({ shooting: 16, craft: 2, melee: 0 });
    expect(learnedFrom(undefined)).toEqual(NO_SKILLS);
  });

  it('learning says which levels went up', () => {
    const r = learn({ ...NO_SKILLS, shooting: 22 }, { hit: 2, gunKill: 1, blow: 1 });
    expect(r.skills).toEqual({ ...NO_SKILLS, shooting: 25, melee: 1 });
    expect(r.levels).toEqual([{ skill: 'shooting', from: 1, to: 2 }]);
  });

  it('what the levels give, grenades and the medkit only to whoever carries them', () => {
    const top = at({ shooting: 5, melee: 5, medicine: 5, craft: 5 });
    expect(skillMods(top).mods).toMatchObject({ gunAim: 25, gunCrit: 10, meleeAim: 40, meleeDamage: 2, heal: 2, hack: 50, grenadeDamage: 1 });
    expect(skillMods(top).charges).toEqual({});
    expect(skillMods(top, { grenades: true, medkit: true }).charges).toEqual({ grenade: 1, firstAid: 1 });
    expect(skillMods(at({ shooting: 2, melee: 2 })).mods).toMatchObject({ gunCrit: 0, meleeDamage: 0 });
  });
});

describe('talents: one of a pair at levels 2 and 4', () => {
  it('every skill has two pairs of talents of its own, sixteen in all', () => {
    const ids = SKILL_IDS.flatMap((id) => TALENT_PAIRS[id].flat());
    expect(new Set(ids).size).toBe(16);
    for (const id of ids) expect(SKILL_TALENTS[id], id).toBeDefined();
  });

  it('a level opens its pair until one is picked', () => {
    const skills = at({ shooting: 4, medicine: 2 });
    expect(pendingPicks(skills, []).map((p) => `${p.skill}${p.level}`)).toEqual(['shooting2', 'shooting4', 'medicine2']);
    expect(pendingPicks(skills, ['rapidFire', 'faceoff', 'fieldMedic'])).toEqual([]);
  });

  it('what «Guardar» sends: talents of the levels reached, one of each pair', () => {
    const skills = at({ shooting: 2 });
    expect(picksBlocker(skills, ['aimedShot'])).toBeNull();
    expect(picksBlocker(skills, ['aimedShot', 'rapidFire'])).toMatch(/Solo uno de cada pareja/);
    expect(picksBlocker(skills, ['killZone'])).toBe('Nadie pasa pide Tiro 4.');
    expect(picksBlocker(skills, ['drill'])).toBe('Ese talento no existe.');
  });
});

describe('veterancy', () => {
  it('is the sum of the four levels, with its names', () => {
    expect(veterancy(at({ shooting: 3, melee: 2, craft: 1 }))).toBe(6);
    expect([0, 2, 3, 5, 6, 9, 10, 13, 14, 20].map((v) => VETERANCY[veterancyRank(v)]!.name)).toEqual(['Novato', 'Novato', 'Fogueado', 'Fogueado', 'Curtido', 'Curtido', 'Veterano', 'Veterano', 'Superviviente', 'Superviviente']);
  });

  it('gives health every 4 points, will every point and a third action from Veterano', () => {
    expect([0, 3, 4, 20].map(veteranHp)).toEqual([5, 5, 6, 10]);
    expect([0, 10].map(veteranWill)).toEqual([40, 70]);
    expect([9, 10].map(veteranAp)).toEqual([2, 3]);
  });
});

describe('newcomers', () => {
  it('a recruit has level 1 in one or two skills, not always the same', () => {
    const seen = new Set<string>();
    for (let seed = 1; seed <= 30; seed++) {
      const s = recruitSkills(new Rng(seed));
      const ones = SKILL_IDS.filter((id) => s[id] === SKILL_XP[0]);
      expect(ones.length).toBeGreaterThanOrEqual(1);
      expect(ones.length).toBeLessThanOrEqual(2);
      expect(veterancy(s)).toBe(ones.length);
      seen.add(ones.join());
    }
    expect(seen.size).toBeGreaterThan(4);
  });

  it('a skirmish veteran has every skill at 2 to 4 and a talent of every pair it opened', () => {
    for (let seed = 1; seed <= 10; seed++) {
      const { skills, picks } = veteranSkills(new Rng(seed));
      for (const id of SKILL_IDS) expect(skillLevel(skills[id])).toBeGreaterThanOrEqual(2);
      expect(pendingPicks(skills, picks)).toEqual([]);
      expect(picksBlocker(skills, picks)).toBeNull();
    }
  });
});

describe('what a soldier does in a mission is counted by the events', () => {
  function field() {
    const s = stateFrom(Array.from({ length: 12 }, () => '.'.repeat(12)));
    const a = addUnit(s, 's1', 'rookie', 1, 1, { owner: 0 });
    const b = addUnit(s, 's2', 'rookie', 2, 1, { owner: 1 });
    const z = addUnit(s, 'z1', 'zombie', 6, 1);
    const z2 = addUnit(s, 'z2', 'zombie', 7, 1);
    return { s, a, b, z, z2 };
  }
  const shot = (unit: string, target: string, ability: 'shoot' | 'slash', result: 'miss' | 'hit' | 'graze' | 'crit'): GameEvent => ({ t: 'shot', unit, target, ability, from: { x: 1, y: 1, z: 0 }, result, chance: 50, roll: 10, reaction: false });

  it('hits and kills, by the weapon that dealt them', () => {
    const { s, a } = field();
    for (const e of [
      shot('s1', 'z1', 'shoot', 'hit'),
      shot('s1', 'z1', 'shoot', 'miss'),
      shot('s1', 'z1', 'shoot', 'graze'),
      { t: 'damaged', unit: 'z1', amount: 9, mitigated: 0, shred: 0 },
      { t: 'died', unit: 'z1', by: 's1' },
      shot('s1', 'z2', 'slash', 'crit'),
      { t: 'died', unit: 'z2', by: 's1' },
    ] as GameEvent[]) applyEvent(s, e);
    expect(a.deeds).toEqual({ hit: 2, gunKill: 1, blow: 1, bladeKill: 1 });
  });

  it('an explosive teaches once, if it reaches an enemy; a kill with it is no gun kill', () => {
    const { s, a } = field();
    for (const e of [
      { t: 'explosive', unit: 's1', ability: 'grenade', target: { x: 6, y: 1, z: 0 } },
      { t: 'damaged', unit: 'z1', amount: 3, mitigated: 0, shred: 0 },
      { t: 'damaged', unit: 'z2', amount: 3, mitigated: 0, shred: 0 },
      { t: 'died', unit: 'z2', by: 's1' },
    ] as GameEvent[]) applyEvent(s, e);
    expect(a.deeds).toEqual({ blast: 1 });
  });

  it('cures, searches, a fixed radio and a silenced alarm', () => {
    const { s, a, b } = field();
    const relay = addUnit(s, 'r1', 'relay', 9, 9, { objective: true });
    s.mission = { ...s.mission, kind: 'sabotage', objectiveUnit: relay.id };
    for (const e of [
      { t: 'healed', unit: 's1', target: 's2', amount: 3 },
      { t: 'healed', unit: 's1', target: 's1', amount: 2 },
      { t: 'cured', unit: 's2', by: 's1' },
      { t: 'searched', unit: 's2', tile: { x: 2, y: 2, z: 0 }, loot: null },
      { t: 'roomSearched', unit: 's2', room: 1, loot: null },
      { t: 'hacked', unit: 's2', success: true, chance: 60, remote: false },
      shot('s1', 'r1', 'shoot', 'hit'),
      { t: 'died', unit: 'r1', by: 's1' },
    ] as GameEvent[]) applyEvent(s, e);
    expect(a.deeds).toEqual({ healOther: 2, healSelf: 1, hit: 1, fix: 1 });
    expect(b.deeds).toEqual({ search: 2, fix: 1 });
  });

  it('only the squad learns, and a client counts the same as the host', () => {
    const { s, z } = field();
    const client = cloneState(s);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'grenade', tile: { x: 6, y: 1 } }, { slot: 0, connected: [0, 1], rng: new Rng(3) });
    if (!r.ok) throw new Error(r.error);
    for (const e of r.events) applyEvent(client, e);
    expect(client).toEqual(s);
    expect(s.units.find((u) => u.id === 's1')!.deeds).toEqual({ blast: 1 });
    expect(z.deeds).toBeUndefined();
  });
});

describe('the talents in the fight (docs/FASE9.md §2)', () => {
  const ctx = (seed = 1) => ({ slot: 0 as const, connected: [0 as const], rng: new Rng(seed) });
  function field() {
    const s = stateFrom(Array.from({ length: 12 }, () => '.'.repeat(12)));
    return s;
  }

  it('Hacerse sitio: a Slash at every enemy alongside, none at the one further off', () => {
    const s = field();
    const u = addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, perks: ['elbowRoom'], arms: { primary: null, melee: 'machete' } });
    addUnit(s, 'z1', 'zombie', 4, 5, { hp: 99, maxHp: 99 });
    addUnit(s, 'z2', 'zombie', 6, 6, { hp: 99, maxHp: 99 });
    addUnit(s, 'z3', 'zombie', 9, 9, { hp: 99, maxHp: 99 });
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'elbowRoom' }, ctx());
    if (!r.ok) throw new Error(r.error);
    expect(r.events.filter((e) => e.t === 'shot').map((e) => (e as { target: string }).target).sort()).toEqual(['z1', 'z2']);
    expect(u.cooldowns.elbowRoom).toBeGreaterThan(0);
    const alone = field();
    addUnit(alone, 's1', 'rookie', 5, 5, { owner: 0, perks: ['elbowRoom'] });
    addUnit(alone, 'z3', 'zombie', 9, 9);
    expect(executeCommand(alone, { type: 'ability', unit: 's1', ability: 'elbowRoom' }, ctx())).toEqual({ ok: false, error: 'No tienes ningún enemigo al lado.' });
  });

  it('Espabilar needs no medkit, only a partner alongside', () => {
    const s = field();
    addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, perks: ['revivalProtocol'] });
    const dazed = addUnit(s, 's2', 'rookie', 6, 5, { owner: 0, stunned: true, disoriented: 1 });
    const far = addUnit(s, 's3', 'rookie', 9, 5, { owner: 0, panicked: true });
    addUnit(s, 'z1', 'zombie', 11, 11);
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'revival', target: 's3' }, ctx()).ok).toBe(false);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'revival', target: 's2' }, ctx());
    expect(r.ok).toBe(true);
    expect(dazed).toMatchObject({ stunned: false, disoriented: 0 });
    expect(far.panicked).toBe(true);
  });

  it('Sin hacer ruido: searching makes no noise; Buen brazo and Demolición reach further', () => {
    const quiet = { ...stateFrom(['....']).units[0], mods: { ...fighterStats(NO_SKILLS, ['quiet']).mods } };
    expect(quiet.mods.quiet).toBe(1);
    const thrower = fighterStats(at({ craft: 4 }), ['goodArm', 'demolition'], ['fragGrenade', 'fragGrenade']);
    // Two accessories of two grenades, one more each with Buen brazo, and one more with Maña 2.
    expect(thrower.charges.grenade).toBe(4 + 2 + 1);
    expect(thrower.mods).toMatchObject({ grenadeRange: 3, grenadeRadius: 1, grenadeDamage: 2 });
  });

  it('Mano de machete counts only with the machete, and the medkit heals more with Medicina', () => {
    expect(fighterStats(NO_SKILLS, ['bladeMaster'], [], { primary: null, melee: null }).mods.meleeDamage).toBe(0);
    expect(fighterStats(NO_SKILLS, ['bladeMaster'], [], { primary: null, melee: 'machete' }).mods).toMatchObject({ meleeDamage: 2, meleeAim: 15 });
    const s = field();
    addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, gear: ['firstAid'], charges: { firstAid: 2 }, mods: { ...fighterStats(at({ medicine: 4 }), ['fieldMedic']).mods } });
    const hurt = addUnit(s, 's2', 'rookie', 6, 5, { owner: 0, hp: 1, maxHp: 12 });
    addUnit(s, 'z1', 'zombie', 11, 11);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'firstAid', target: 's2' }, ctx());
    if (!r.ok) throw new Error(r.error);
    // 4, +2 for Medicina 4 and +1 for Buen pulso.
    expect(hurt.hp).toBe(1 + 4 + 2 + 1);
  });
});
