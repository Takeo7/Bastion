import { describe, expect, it } from 'vitest';
import { BOARD, BOARD_CELLS, boardNeighbors, boardRole, CORE, PERK_ABILITY, SECTOR_LINES } from '../src/board';
import { abilitiesOf } from '../src/abilities';
import { apPerTurn } from '../src/content';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import {
  barCost,
  barsCost,
  barsOf,
  cellState,
  fighterStats,
  learn,
  learnedFrom,
  missionLines,
  NO_GROWTH,
  NO_SKILLS,
  pointsLeft,
  randomBoard,
  recruitSkills,
  SKILL_IDS,
  unlockBlocker,
  totalBars,
  veterancyRank,
  VETERANCY,
  veteranSkills,
  veteranWill,
  type Skills,
} from '../src/skills';
import { addUnit, stateFrom } from './helpers';

// Fase 10, barras de experiencia y tablero hexagonal (docs/FASE10.md).

/** Lines with these bars filled, and nothing into the next. */
const bars = (filled: Partial<Record<keyof Skills, number>>): Skills => {
  const out = { ...NO_SKILLS };
  for (const [id, n] of Object.entries(filled) as [keyof Skills, number][]) out[id] = barsCost(n);
  return out;
};

describe('the bars', () => {
  it('take 10, 15, 20… and start again when full', () => {
    expect([0, 1, 2, 3].map(barCost)).toEqual([10, 15, 20, 25]);
    expect([0, 1, 2, 3].map(barsCost)).toEqual([0, 10, 25, 45]);
    expect(barsOf(0)).toEqual({ filled: 0, into: 0, need: 10 });
    expect(barsOf(9)).toEqual({ filled: 0, into: 9, need: 10 });
    expect(barsOf(10)).toEqual({ filled: 1, into: 0, need: 15 });
    expect(barsOf(32)).toEqual({ filled: 2, into: 7, need: 20 });
  });

  it('deeds turn into experience, line by line, and a bonus adds its share rounded up', () => {
    expect(learnedFrom({ shot: 3, hit: 2, gunKill: 1, swing: 1, blow: 1, throw: 2, throwHit: 1, healOther: 1, healSelf: 1, search: 2, fix: 1, hunker: 2, run: 3 })).toEqual({
      shooting: 6,
      melee: 2,
      throwing: 3,
      medicine: 3,
      searching: 7,
      fitness: 5,
    });
    expect(learnedFrom({ shot: 9, hit: 4 }, 0.2)).toMatchObject({ shooting: 16, melee: 0 });
    expect(learnedFrom(undefined)).toEqual(NO_SKILLS);
  });

  it('a bar that fills adds 5 to 10 % to its line and a point; two at once, two of each', () => {
    const r = learn({ ...NO_SKILLS, shooting: 8 }, NO_GROWTH, { shot: 10, hit: 9, gunKill: 3, hunker: 2 }, new Rng(4));
    // 8 + 22 = 30: the first bar (10) and the second (25).
    expect(r.skills).toEqual({ ...NO_SKILLS, shooting: 30, fitness: 2 });
    expect(r.gained).toEqual({ ...NO_SKILLS, shooting: 22, fitness: 2 });
    expect(r.bars).toHaveLength(1);
    expect(r.bars[0]).toMatchObject({ skill: 'shooting', filled: 2 });
    expect(r.bars[0]!.growth).toHaveLength(2);
    for (const g of r.bars[0]!.growth) expect(g).toBeGreaterThanOrEqual(5), expect(g).toBeLessThanOrEqual(10);
    expect(r.growth.shooting).toBe(r.bars[0]!.growth[0]! + r.bars[0]!.growth[1]!);
    expect(r.growth.fitness).toBe(0);
    expect(r.points).toBe(2);
  });

  it('what the lines add: a share of the base, rounded when it is used', () => {
    const st = fighterStats(NO_SKILLS, { shooting: 10, melee: 20, throwing: 10, medicine: 13, searching: 10, fitness: 23 }, []);
    // 60 → 66 and 72; a throw of 10 → 11; the medkit's 4 → 5 (4.52); 55 → 61 (60.5); health 5 → 6 (6.15).
    expect(st.mods).toMatchObject({ gunAim: 6, meleeAim: 12, grenadeRange: 1, heal: 1, hack: 6 });
    expect(st.maxHp).toBe(6);
    expect(fighterStats(NO_SKILLS, NO_GROWTH, []).maxHp).toBe(5);
  });
});

describe('veterancy (Arturo, 16/10)', () => {
  it('a rank for every 5 points spent on the board, with its names', () => {
    expect(totalBars(bars({ shooting: 3, melee: 2, fitness: 1 }))).toBe(6);
    expect([0, 4, 5, 9, 10, 14, 15, 19, 20, 40].map((v) => VETERANCY[veterancyRank(v)]!.name)).toEqual([
      'Novato',
      'Novato',
      'Fogueado',
      'Fogueado',
      'Curtido',
      'Curtido',
      'Veterano',
      'Veterano',
      'Superviviente',
      'Superviviente',
    ]);
  });

  it('each rank gives 10 of will, and a Veterano the third action; bars not spent count for nothing', () => {
    expect([0, 1, 4].map(veteranWill)).toEqual([40, 50, 80]);
    const skills = bars({ shooting: 5, melee: 5, fitness: 5, medicine: 5 });
    expect(fighterStats(skills, NO_GROWTH, []).rank).toBe(0);
    expect(fighterStats(skills, NO_GROWTH, []).will).toBe(40);
    const board = randomBoard(skills, new Rng(2));
    expect(board).toHaveLength(20);
    expect(fighterStats(skills, NO_GROWTH, board.slice(0, 14))).toMatchObject({ rank: 2, will: 60 });
    expect(fighterStats(skills, NO_GROWTH, board.slice(0, 15))).toMatchObject({ rank: 3, will: 70 });
    expect(apPerTurn({ team: 'xcom', rank: 3 })).toBe(3);
  });
});

describe('newcomers', () => {
  it('a recruit has a bar in one or two lines, not always the same, with its growth', () => {
    const seen = new Set<string>();
    for (let seed = 1; seed <= 30; seed++) {
      const { skills, growth } = recruitSkills(new Rng(seed));
      const full = SKILL_IDS.filter((id) => barsOf(skills[id]).filled === 1);
      expect(full.length).toBeGreaterThanOrEqual(1);
      expect(full.length).toBeLessThanOrEqual(2);
      expect(totalBars(skills)).toBe(full.length);
      for (const id of SKILL_IDS) expect(growth[id] > 0).toBe(full.includes(id));
      seen.add(full.join());
    }
    expect(seen.size).toBeGreaterThan(4);
  });

  it('a skirmish veteran has two to five bars in every line and every point spent', () => {
    for (let seed = 1; seed <= 10; seed++) {
      const { skills, board } = veteranSkills(new Rng(seed));
      for (const id of SKILL_IDS) expect(barsOf(skills[id]).filled).toBeGreaterThanOrEqual(2);
      expect(board).toHaveLength(totalBars(skills));
      expect(pointsLeft(skills, board)).toBe(0);
      // Bought one by one, each beside the ones before.
      board.forEach((id, i) => expect(unlockBlocker(skills, board.slice(0, i), id), id).toBeNull());
    }
  });
});

describe('the hex board (docs/FASE10.md §6)', () => {
  const hexDistance = (q: number, r: number) => (Math.abs(q) + Math.abs(r) + Math.abs(q + r)) / 2;

  it('169 cells: the core and seven rings, six sectors of 28', () => {
    expect(BOARD).toHaveLength(169);
    expect(new Set(BOARD.map((c) => c.id)).size).toBe(169);
    expect(new Set(BOARD.map((c) => `${c.q},${c.r}`)).size).toBe(169);
    expect(new Set(BOARD.map((c) => c.name)).size).toBe(169);
    for (let k = 1; k <= 7; k++) expect(BOARD.filter((c) => c.ring === k)).toHaveLength(6 * k);
    for (let i = 0; i < 6; i++) {
      const sector = BOARD.filter((c) => c.sector === i);
      expect(sector).toHaveLength(28);
      for (const c of sector) expect(c.line).toBe(SECTOR_LINES[i]);
    }
    for (const c of BOARD) expect(hexDistance(c.q, c.r), c.id).toBe(c.ring);
  });

  it('the core touches one cell of every sector; every cell has its neighbours on the board', () => {
    expect(boardNeighbors(CORE).map((id) => BOARD_CELLS[id]!.sector).sort()).toEqual([0, 1, 2, 3, 4, 5]);
    for (const c of BOARD) expect(boardNeighbors(c.id).length, c.id).toBe(c.ring === 7 ? (boardNeighbors(c.id).length === 3 ? 3 : 4) : 6);
  });

  it('every active gives its ability, and every skill says what it does', () => {
    for (const c of BOARD) {
      expect(c.effect.length, c.id).toBeGreaterThan(5);
      if (c.kind === 'active') expect(PERK_ABILITY[c.perk!], c.id).toBe(c.ability);
    }
  });

  it('a skill is bought beside one already had, with a point, and nothing else', () => {
    const first = BOARD.find((c) => c.ring === 1 && c.line === 'shooting')!;
    const second = BOARD.find((c) => c.ring === 2 && c.line === 'shooting' && boardNeighbors(c.id).includes(first.id))!;
    const far = BOARD.find((c) => c.ring === 3 && c.line === 'shooting')!;
    const one = bars({ melee: 1 });
    expect(cellState([], first.id)).toBe('open');
    expect(cellState([], far.id)).toBe('locked');
    expect(cellState([], CORE)).toBe('unlocked');
    expect(unlockBlocker(one, [], first.id)).toBeNull();
    expect(unlockBlocker(one, [], far.id)).toMatch(/al lado/);
    // A point from any line opens any cell beside: no bars of its own line asked (Arturo, 16/10).
    expect(unlockBlocker(bars({ melee: 2 }), [first.id], second.id)).toBeNull();
    expect(unlockBlocker(one, [first.id], second.id)).toMatch(/No le quedan puntos/);
    expect(unlockBlocker(one, [first.id], first.id)).toBe('Ya la tiene.');
    expect(unlockBlocker(one, [], CORE)).toBe('Ya la tiene.');
    expect(unlockBlocker(one, [], 'nothing')).toBe('Esa casilla no existe.');
    expect(pointsLeft(bars({ shooting: 2, fitness: 1 }), [first.id])).toBe(2);
  });

  it('what the cells give: numbers, health, armour, rules, abilities, and spare uses only of what is carried', () => {
    const st = fighterStats(NO_SKILLS, NO_GROWTH, ['steadyHand', 'stamina', 'vest', 'implacable', 'aimedShot', 'bandolier', 'bigKit', 'restoration']);
    expect(st.mods.gunAim).toBe(3);
    expect(st.maxHp).toBe(6);
    expect(st.armor).toBe(1);
    expect(st.perks).toEqual(['implacable', 'aimedShot', 'restoration']);
    expect(st.charges).toEqual({ restoration: 1 });
    const carrying = fighterStats(NO_SKILLS, NO_GROWTH, ['bandolier', 'goodArm', 'demolition'], ['fragGrenade', 'fragGrenade']);
    // Two accessories of two grenades, one more each with Buen brazo, and one more with Bandolera.
    expect(carrying.charges.grenade).toBe(4 + 2 + 1);
    expect(carrying.mods).toMatchObject({ grenadeRange: 3, grenadeRadius: 1, grenadeDamage: 1 });
    expect(fighterStats(NO_SKILLS, NO_GROWTH, ['bladeMaster'], [], { primary: null, melee: null }).mods.meleeDamage).toBe(0);
    expect(fighterStats(NO_SKILLS, NO_GROWTH, ['bladeMaster'], [], { primary: null, melee: 'machete' }).mods).toMatchObject({ meleeDamage: 2, meleeAim: 15 });
  });

  it('the role: the line with most cells bought, a tie to the one bought last', () => {
    expect(boardRole([])).toBeNull();
    expect(boardRole(['steadyHand', 'grip', 'lightTrigger'])).toBe('shooting');
    expect(boardRole(['steadyHand', 'grip'])).toBe('melee');
  });

  it('a random board spends every point, each cell beside the ones before', () => {
    const skills = bars({ shooting: 3, fitness: 2 });
    const board = randomBoard(skills, new Rng(7));
    expect(board).toHaveLength(5);
    board.forEach((id, i) => expect(unlockBlocker(skills, board.slice(0, i), id), id).toBeNull());
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

  it('every shot and swing, more if it lands, more if it kills', () => {
    const { s, a } = field();
    for (const e of [
      shot('s1', 'z1', 'shoot', 'hit'),
      shot('s1', 'z1', 'shoot', 'miss'),
      shot('s1', 'z1', 'shoot', 'graze'),
      { t: 'damaged', unit: 'z1', amount: 9, mitigated: 0, shred: 0 },
      { t: 'died', unit: 'z1', by: 's1' },
      shot('s1', 'z2', 'slash', 'miss'),
      shot('s1', 'z2', 'slash', 'crit'),
      { t: 'died', unit: 'z2', by: 's1' },
      shot('s1', 's2', 'shoot', 'hit'),
    ] as GameEvent[]) applyEvent(s, e);
    expect(a.deeds).toEqual({ shot: 3, hit: 2, gunKill: 1, swing: 2, blow: 1, bladeKill: 1 });
  });

  it('a throw teaches, and once more if it reaches an enemy; a kill with it is no gun kill', () => {
    const { s, a } = field();
    for (const e of [
      { t: 'explosive', unit: 's1', ability: 'grenade', target: { x: 6, y: 1, z: 0 } },
      { t: 'damaged', unit: 'z1', amount: 3, mitigated: 0, shred: 0 },
      { t: 'damaged', unit: 'z2', amount: 3, mitigated: 0, shred: 0 },
      { t: 'died', unit: 'z2', by: 's1' },
      { t: 'explosive', unit: 's1', ability: 'flashbang', target: { x: 6, y: 1, z: 0 } },
      { t: 'disoriented', unit: 'z1' },
      { t: 'explosive', unit: 's1', ability: 'smoke', target: { x: 1, y: 1, z: 0 } },
    ] as GameEvent[]) applyEvent(s, e);
    expect(a.deeds).toEqual({ throw: 3, throwHit: 2 });
  });

  it('cures, searches, a fixed radio, a silenced alarm, hunkering and running', () => {
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
      { t: 'hunker', unit: 's2' },
      { t: 'moved', unit: 's2', path: [{ x: 3, y: 1, z: 0 }], ap: 1 },
      { t: 'moved', unit: 's2', path: [{ x: 9, y: 1, z: 0 }], ap: 2 },
    ] as GameEvent[]) applyEvent(s, e);
    expect(a.deeds).toEqual({ healOther: 2, healSelf: 1, shot: 1, hit: 1, fix: 1 });
    expect(b.deeds).toEqual({ search: 2, fix: 1, hunker: 1, run: 1 });
  });

  it('only the squad learns, and a client counts the same as the host', () => {
    const { s, z } = field();
    const client = cloneState(s);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'grenade', tile: { x: 6, y: 1 } }, { slot: 0, connected: [0, 1], rng: new Rng(3) });
    if (!r.ok) throw new Error(r.error);
    for (const e of r.events) applyEvent(client, e);
    expect(client).toEqual(s);
    expect(s.units.find((u) => u.id === 's1')!.deeds).toEqual({ throw: 1, throwHit: 1 });
    expect(z.deeds).toBeUndefined();
  });
});

describe('a bar fills out there (Arturo, 16/10)', () => {
  const ctx = (seed = 1) => ({ slot: 0 as const, connected: [0 as const], rng: new Rng(seed) });

  it('after the action that fills it, its stat goes up at once, the same on every client', () => {
    const s = stateFrom(Array.from({ length: 12 }, () => '.'.repeat(12)));
    const u = addUnit(s, 's1', 'rookie', 1, 1, { owner: 0, arms: { primary: 'assaultRifle', melee: null }, lines: missionLines({ ...NO_SKILLS, shooting: 8 }, NO_GROWTH) });
    addUnit(s, 's2', 'rookie', 1, 3, { owner: 0 });
    addUnit(s, 'z1', 'zombie', 6, 1, { hp: 99, maxHp: 99 });
    u.mods.aim = 300;
    const aim = u.mods.gunAim;
    const client = cloneState(s);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'shoot', target: 'z1' }, ctx());
    if (!r.ok) throw new Error(r.error);
    // A shot and a hit: 8 + 2 = 10, the first bar. It comes after the whole shot.
    const filled = r.events.findIndex((e) => e.t === 'barFilled');
    expect(filled).toBeGreaterThan(r.events.findIndex((e) => e.t === 'damaged'));
    const bar = r.events[filled] as Extract<GameEvent, { t: 'barFilled' }>;
    expect(bar).toMatchObject({ unit: 's1', skill: 'shooting', filled: 1 });
    expect(bar.growth).toBeGreaterThanOrEqual(5);
    expect(u.mods.gunAim - aim).toBe(Math.round(60 * (1 + bar.growth / 100)) - 60);
    expect(u.lines!.rolls).toEqual({ shooting: [bar.growth] });
    for (const e of r.events) applyEvent(client, e);
    expect(client).toEqual(s);
    // Home again: the bar keeps what it rolled out there, and isn't rolled twice.
    const home = learn({ ...NO_SKILLS, shooting: 8 }, NO_GROWTH, u.deeds, new Rng(99), 0, u.lines);
    expect(home.bars).toEqual([{ skill: 'shooting', filled: 1, growth: [bar.growth] }]);
    expect(home.growth.shooting).toBe(bar.growth);
  });

  it('Forma física: health grows at once, the new points full', () => {
    const s = stateFrom(['....']);
    const u = addUnit(s, 's1', 'rookie', 0, 0, { owner: 0, hp: 3, maxHp: 5, lines: missionLines(NO_SKILLS, { ...NO_GROWTH, fitness: 15 }) });
    // 5 grown 15 % is 6 (5.75); 25 % more is 6 (6.25); 35 %, 7 (6.75).
    applyEvent(s, { t: 'barFilled', unit: 's1', skill: 'fitness', filled: 1, growth: 10 });
    expect([u.maxHp, u.hp]).toEqual([5, 3]);
    applyEvent(s, { t: 'barFilled', unit: 's1', skill: 'fitness', filled: 2, growth: 10 });
    expect([u.maxHp, u.hp]).toEqual([6, 4]);
  });
});

describe('the new actions (docs/FASE10.md §4)', () => {
  const ctx = (seed = 1) => ({ slot: 0 as const, connected: [0 as const], rng: new Rng(seed) });
  const field = () => stateFrom(Array.from({ length: 16 }, () => '.'.repeat(16)));
  const armed = { arms: { primary: 'assaultRifle' as const, melee: null } };

  it('a stone is always at hand, and the blade can keep watch', () => {
    const s = field();
    const u = addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, ...armed });
    expect(abilitiesOf(u)).toEqual(expect.arrayContaining(['stone', 'overwatchMelee']));
    const list = abilitiesOf(u);
    expect(list.indexOf('overwatchMelee')).toBe(list.indexOf('overwatch') + 1);
    expect(abilitiesOf(addUnit(s, 'z1', 'zombie', 1, 1))).not.toContain('stone');
  });

  it('the stone: heard where it lands, 1 of damage to whoever is there, and the turn goes on', () => {
    const s = field();
    const u = addUnit(s, 's1', 'rookie', 2, 2, { owner: 0, ...armed });
    const z = addUnit(s, 'z1', 'zombie', 8, 2, { hp: 5, maxHp: 5 });
    const far = addUnit(s, 'z2', 'zombie', 14, 14);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'stone', tile: { x: 8, y: 2 } }, ctx());
    if (!r.ok) throw new Error(r.error);
    expect(r.events.find((e) => e.t === 'stoneThrown')).toMatchObject({ unit: 's1', hit: 'z1' });
    expect(z.hp).toBe(4);
    expect(u.ap).toBe(1);
    const noise = r.events.find((e) => e.t === 'noise') as { tile: { x: number; y: number }; radius: number } | undefined;
    expect(noise).toMatchObject({ tile: { x: 8, y: 2 }, radius: 8 });
    expect(far.alive).toBe(true);
    expect(u.deeds).toEqual({ throw: 1, throwHit: 1 });
  });

  it('a stone on the floor only sounds; Señuelo makes it heard further, Canto afilado hurts more', () => {
    const s = field();
    const st = fighterStats(NO_SKILLS, NO_GROWTH, ['decoy', 'sharpStone']);
    const u = addUnit(s, 's1', 'rookie', 2, 2, { owner: 0, ...armed, mods: st.mods });
    addUnit(s, 'z1', 'zombie', 14, 14);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'stone', tile: { x: 6, y: 6 } }, ctx());
    if (!r.ok) throw new Error(r.error);
    expect(r.events.find((e) => e.t === 'stoneThrown')).toMatchObject({ hit: null });
    expect(r.events.find((e) => e.t === 'noise')).toMatchObject({ radius: 12 });
    expect(u.deeds).toEqual({ throw: 1 });
    const t = field();
    addUnit(t, 's1', 'rookie', 2, 2, { owner: 0, ...armed, mods: st.mods });
    const z = addUnit(t, 'z1', 'zombie', 4, 2, { hp: 5, maxHp: 5 });
    expect(executeCommand(t, { type: 'ability', unit: 's1', ability: 'stone', tile: { x: 4, y: 2 } }, ctx()).ok).toBe(true);
    expect(z.hp).toBe(3);
  });

  const walk = (x: number, from: number, to: number) => Array.from({ length: from - to }, (_, i) => ({ x, y: from - 1 - i, z: 0 }));

  it('melee overwatch strikes the first enemy that comes alongside, not the one walking past in sight, and stops', () => {
    const s = field();
    const u = addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, ...armed, ap: 2 });
    // Someone with actions left, so the turn doesn't pass.
    addUnit(s, 's2', 'rookie', 1, 1, { owner: 0, ...armed });
    addUnit(s, 'z9', 'zombie', 15, 15);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'overwatchMelee' }, ctx());
    if (!r.ok) throw new Error(r.error);
    expect(u).toMatchObject({ overwatch: true, watchMelee: true, ap: 0 });
    const passer = addUnit(s, 'z0', 'zombie', 9, 12, { hp: 50, maxHp: 50 });
    const z = addUnit(s, 'z1', 'zombie', 5, 9, { hp: 50, maxHp: 50 });
    const z2 = addUnit(s, 'z2', 'zombie', 4, 9, { hp: 50, maxHp: 50 });
    const sim = new Sim(s, new Rng(1), [0]);
    sim.moveAlong(passer, walk(9, 12, 8), 1);
    expect(sim.events.some((e) => e.t === 'shot')).toBe(false);
    sim.moveAlong(z, walk(5, 9, 6), 1);
    const strikes = sim.events.filter((e) => e.t === 'shot');
    expect(strikes).toHaveLength(1);
    expect(strikes[0]).toMatchObject({ unit: 's1', target: 'z1', ability: 'slash', reaction: true });
    expect(u.overwatch).toBe(false);
    sim.moveAlong(z2, walk(4, 9, 6), 1);
    expect(sim.events.filter((e) => e.t === 'shot')).toHaveLength(1);
    expect(u.deeds?.swing).toBe(1);
  });

  it('Muro de acero: every enemy that comes alongside, once each', () => {
    const s = field();
    const u = addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, ...armed, ap: 2, perks: ['bladeWall'] });
    addUnit(s, 's2', 'rookie', 1, 1, { owner: 0, ...armed });
    addUnit(s, 'z9', 'zombie', 15, 15);
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'overwatchMelee' }, ctx()).ok).toBe(true);
    const z = addUnit(s, 'z1', 'zombie', 5, 9, { hp: 50, maxHp: 50 });
    const z2 = addUnit(s, 'z2', 'zombie', 4, 9, { hp: 50, maxHp: 50 });
    const sim = new Sim(s, new Rng(1), [0]);
    sim.moveAlong(z, walk(5, 9, 6), 1);
    sim.moveAlong(z2, walk(4, 9, 6), 1);
    expect(sim.events.filter((e) => e.t === 'shot').map((e) => (e as { target: string }).target)).toEqual(['z1', 'z2']);
    expect(u.overwatch).toBe(true);
  });

  it('Segundo aliento: a blade kill heals 1', () => {
    const s = field();
    const u = addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, ...armed, perks: ['secondWind'], hp: 2, maxHp: 6, mods: { ...fighterStats(NO_SKILLS, NO_GROWTH, []).mods, meleeAim: 200 } });
    addUnit(s, 'z1', 'zombie', 6, 5, { hp: 1, maxHp: 1 });
    addUnit(s, 'z9', 'zombie', 15, 15);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'slash', target: 'z1' }, ctx());
    if (!r.ok) throw new Error(r.error);
    expect(u.hp).toBe(3);
  });

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
  });

  it('Espabilar needs no medkit, only a partner alongside', () => {
    const s = field();
    addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, perks: ['revivalProtocol'] });
    const dazed = addUnit(s, 's2', 'rookie', 6, 5, { owner: 0, stunned: true, disoriented: 1 });
    const far = addUnit(s, 's3', 'rookie', 9, 5, { owner: 0, panicked: true });
    addUnit(s, 'z1', 'zombie', 11, 11);
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'revival', target: 's3' }, ctx()).ok).toBe(false);
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'revival', target: 's2' }, ctx()).ok).toBe(true);
    expect(dazed).toMatchObject({ stunned: false, disoriented: 0 });
    expect(far.panicked).toBe(true);
  });

  it('first aid heals more with what Curar has grown and with Vendaje', () => {
    const s = field();
    const st = fighterStats(NO_SKILLS, { ...NO_GROWTH, medicine: 26 }, ['bandage', 'firstAid', 'bigKit'], ['firstAid']);
    // Two uses of the Botiquín, and one more with Botiquín grande.
    expect(st.charges.firstAid).toBe(3);
    addUnit(s, 's1', 'rookie', 5, 5, { owner: 0, gear: ['firstAid'], charges: st.charges, mods: st.mods });
    const hurt = addUnit(s, 's2', 'rookie', 6, 5, { owner: 0, hp: 1, maxHp: 12 });
    addUnit(s, 'z1', 'zombie', 15, 15);
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'firstAid', target: 's2' }, ctx());
    if (!r.ok) throw new Error(r.error);
    // 4 grown 26 % is 5 (5.04), and +1 for Vendaje; and Primeros auxilios shelters whoever it heals.
    expect(hurt.hp).toBe(1 + 5 + 1);
    expect(hurt.aided).toBe(10);
  });

  it('Mantenimiento: the weapon wears half', () => {
    const s = field();
    const plain = addUnit(s, 's1', 'rookie', 2, 2, { owner: 0, arms: { primary: 'assaultRifle', melee: null, condition: 100 } });
    const careful = addUnit(s, 's2', 'rookie', 2, 4, { owner: 0, arms: { primary: 'assaultRifle', melee: null, condition: 100 }, mods: { ...plain.mods, care: 1 } });
    for (const u of [plain, careful]) applyEvent(s, { t: 'shot', unit: u.id, target: 'zz', ability: 'shoot', from: u.pos, result: 'miss', chance: 50, roll: 90, reaction: false });
    expect(100 - careful.arms!.condition!).toBe((100 - plain.arms!.condition!) / 2);
  });
});
