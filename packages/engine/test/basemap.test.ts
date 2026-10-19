import { describe, expect, it } from 'vitest';
import {
  BASE_EDGE,
  BASE_MAP,
  besidePieces,
  bastionFootprint,
  bastionLot,
  dismantleBlocker,
  dismantleRefund,
  fellBlocker,
  fellTree,
  freshBase,
  PIECES,
  pieceAt,
  pieceCells,
  placeBlocker,
  placePiece,
  rearmCost,
  repairCost,
  treeAt,
  type BaseState,
} from '../src/base';
import { decorOf } from '../src/decor';
import type { GameEvent } from '../src/events';
import { Rng } from '../src/rng';
import { NIGHT, TRAPS } from '../src/content';
import { previewShot } from '../src/aim';
import { computeReach } from '../src/path';
import { executeCommand, Sim } from '../src/rules';
import { createMission, makeUnit, siegeWaves } from '../src/setup';
import type { GameState, Slot, Unit } from '../src/types';

// Fase 12, the new base (docs/FASE12.md §11): a clearing in the pines with a caravan, and what is built on it.

/** A base with no trees, for the rules of building. */
const bare = (): BaseState => ({ width: BASE_MAP, height: BASE_MAP, trees: [], pieces: [], nextPiece: 1 });

describe('the clearing', () => {
  it('starts with the caravan in the middle of a clearing, a track out to every side', () => {
    const base = freshBase(new Rng(1));
    expect(base.width).toBe(BASE_MAP);
    expect(base.pieces.map((p) => p.piece)).toEqual(['caravan']);
    const caravan = base.pieces[0]!;
    expect(caravan.hp).toBe(PIECES.caravan.hp);
    expect(Math.abs(caravan.x + 2 - BASE_MAP / 2)).toBeLessThanOrEqual(1);
    // Round the caravan, clear ground.
    for (let y = caravan.y - 4; y <= caravan.y + 5; y++) for (let x = caravan.x - 3; x <= caravan.x + 6; x++) expect(treeAt(base, x, y)).toBe(false);
    // And a way in from every side.
    const mid = Math.floor((BASE_MAP - 1) / 2);
    for (let i = 0; i < BASE_MAP; i++) {
      expect(treeAt(base, i, mid)).toBe(false);
      expect(treeAt(base, mid, i)).toBe(false);
    }
  });

  it('is its own every time: sometimes thicker pines, sometimes more glades', () => {
    const shares = [1, 2, 3, 4, 5, 6, 7, 8].map((seed) => freshBase(new Rng(seed)).trees.length / (BASE_MAP * BASE_MAP));
    expect(new Set(shares).size).toBe(8);
    expect(Math.min(...shares)).toBeGreaterThan(0.15);
    expect(Math.max(...shares)).toBeLessThan(0.7);
    expect(Math.max(...shares) - Math.min(...shares)).toBeGreaterThan(0.05);
    // The same seed, the same ground.
    expect(freshBase(new Rng(3))).toEqual(freshBase(new Rng(3)));
  });
});

describe('the Bastion\'s lot (18/10 #40)', () => {
  it('is 6 × 6 round the caravan, nothing else is built on it, and the building on it grows with the level', () => {
    const base = freshBase(new Rng(1));
    const caravan = base.pieces[0]!;
    expect(bastionLot(base)).toEqual({ x: caravan.x - 1, y: caravan.y - 2, w: 6, h: 6 });
    expect(placeBlocker(base, 'sandbags', caravan.x - 1, caravan.y - 2, 0)).toBe('Ahí no: ese sitio es para el Bastión, que crece con cada nivel.');
    expect(placeBlocker(base, 'sandbags', caravan.x - 2, caravan.y - 2, 0)).toBeNull();
    // Each stage, two levels: the caravan where it is, the mobile home, the barracón, the house.
    const box = (level: number) => {
      const cells = bastionFootprint({ ...base, level });
      const xs = cells.map((c) => c.x);
      const ys = cells.map((c) => c.y);
      return [Math.min(...xs) - caravan.x, Math.min(...ys) - caravan.y, Math.max(...xs) - Math.min(...xs) + 1, Math.max(...ys) - Math.min(...ys) + 1];
    };
    expect([1, 2, 3, 5, 7, 8].map(box)).toEqual([
      [0, 0, 4, 2],
      [0, 0, 4, 2],
      [-1, -1, 6, 3],
      [-1, -1, 5, 4],
      [-1, -2, 6, 5],
      [-1, -2, 6, 5],
    ]);
    // In a siege, the building is solid and holds the caravan's strength; the squad stands outside it.
    const s = createMission({ map: { kind: 'base', base: { ...base, level: 7 } }, slots: [0], rng: new Rng(2) });
    for (const t of bastionFootprint({ ...base, level: 7 })) {
      expect(s.tiles[t.y * s.width + t.x]).toBe('wall');
      expect(s.breakables![t.y * s.width + t.x]).toBe(PIECES.caravan.hp);
    }
    for (const u of s.units.filter((x) => x.team === 'xcom')) expect(s.tiles[u.pos.y * s.width + u.pos.x]).toBe('floor');
  });

  it('beside the lot is beside the caravan', () => {
    const base = freshBase(new Rng(1));
    const lot = bastionLot(base)!;
    const bags = placePiece(base, 'sandbags', lot.x - 1, lot.y + 2, 0);
    expect(besidePieces(base, bags)).toEqual([base.pieces[0]]);
    expect(besidePieces(base, base.pieces[0]!)).toEqual([bags]);
  });
});

describe('building', () => {
  it('turns a piece about its own box', () => {
    const at = (rot: number) => pieceCells('caravan', 10, 10, rot).map((c) => `${c.x},${c.y}`).sort();
    expect(at(0)).toEqual(['10,10', '10,11', '11,10', '11,11', '12,10', '12,11', '13,10', '13,11']);
    // A quarter turn: two across, four deep.
    expect(at(1)).toEqual(['10,10', '10,11', '10,12', '10,13', '11,10', '11,11', '11,12', '11,13']);
    // The tower's ladder goes in front, wherever it faces.
    const ladder = (rot: number) => pieceCells('tower', 10, 10, rot).find((c) => c.tile === 'ladder')!;
    expect([ladder(0).x, ladder(0).y]).toEqual([10, 12]);
    expect([ladder(1).x, ladder(1).y]).toEqual([10, 10]);
    expect([ladder(2).x, ladder(2).y]).toEqual([11, 10]);
    expect([ladder(3).x, ladder(3).y]).toEqual([12, 11]);
    expect(pieceCells('tower', 10, 10, 0).filter((c) => c.elev === 1)).toHaveLength(4);
  });

  it('only on cleared, free ground off the edge, and the unique only once', () => {
    const base = bare();
    expect(placeBlocker(base, 'palisade', 1, 10, 0)).toBe('Ahí no: es el borde del claro.');
    expect(placeBlocker(base, 'palisade', BASE_EDGE, 10, 0)).toBeNull();
    expect(placeBlocker(base, 'quarters', BASE_MAP - BASE_EDGE - 3, 10, 0)).toBe('Ahí no: es el borde del claro.');
    base.trees = [10 * BASE_MAP + 12];
    expect(placeBlocker(base, 'quarters', 10, 10, 0)).toBe('Hay árboles: tala antes.');
    expect(placeBlocker(base, 'quarters', 10, 11, 0)).toBeNull();
    placePiece(base, 'quarters', 10, 11, 0);
    expect(placeBlocker(base, 'sandbags', 13, 13, 0)).toBe('Ahí ya hay algo.');
    // More than one barracón, one radio.
    expect(placeBlocker(base, 'quarters', 20, 20, 0)).toBeNull();
    placePiece(base, 'radio', 20, 5, 0);
    expect(placeBlocker(base, 'radio', 5, 20, 0)).toBe('Ya está en la base.');
  });

  it('a new piece is whole: a building at level 1, a trap set', () => {
    const base = bare();
    const hut = placePiece(base, 'infirmary', 5, 5, 1);
    expect(hut).toMatchObject({ id: 'b1', piece: 'infirmary', rot: 1, hp: PIECES.infirmary.hp, level: 1 });
    expect(placePiece(base, 'mine', 3, 3, 0)).toMatchObject({ id: 'b2', charges: 1 });
    expect(placePiece(base, 'turret', 3, 4, 0).charges).toBe(PIECES.turret.charges);
    expect(pieceAt(base, 6, 8)).toBe(hut);
    expect(pieceAt(base, 8, 6)).toBeUndefined();
  });

  it('felling a tree clears its tile (what it costs, the campaign charges)', () => {
    const base = bare();
    base.trees = [10 * BASE_MAP + 10, 10 * BASE_MAP + 11];
    expect(fellBlocker(base, 12, 10)).toBe('Ahí no hay ningún árbol.');
    expect(fellBlocker(base, 11, 10)).toBeNull();
    expect(fellTree(base, 11, 10)).toBe(1);
    expect(treeAt(base, 11, 10)).toBe(false);
    expect(treeAt(base, 10, 10)).toBe(true);
    expect(fellTree(base, 11, 10)).toBe(0);
    // A stump stays where it was felled, till something is built on it.
    expect(base.felled).toEqual([10 * BASE_MAP + 11]);
    placePiece(base, 'sandbags', 11, 10, 0);
    expect(base.felled).toEqual([]);
  });

  it('mending costs what was lost; a spent trap is set again with piezas; dismantling gives half back, never the caravan', () => {
    const base = bare();
    const wall = placePiece(base, 'wall', 5, 5, 0);
    expect(repairCost(wall)).toEqual({ supplies: 0, parts: 0 });
    wall.hp = PIECES.wall.hp / 2;
    expect(repairCost(wall)).toEqual({ supplies: Math.ceil(PIECES.wall.cost.supplies / 2), parts: 1 });
    const mine = placePiece(base, 'mine', 6, 5, 0);
    expect(rearmCost(mine)).toBe(0);
    mine.charges = 0;
    expect(rearmCost(mine)).toBe(1);
    const gate = placePiece(base, 'gate', 7, 5, 0);
    expect(dismantleRefund(gate)).toBe(5);
    const caravan = placePiece(base, 'caravan', 20, 20, 0);
    expect(dismantleBlocker(caravan)).toBe('La caravana no se desmonta.');
    expect(dismantleBlocker(gate)).toBeNull();
  });

  it('«junto a» is side by side on the map', () => {
    const base = bare();
    const infirmary = placePiece(base, 'infirmary', 10, 10, 0);
    const quarters = placePiece(base, 'quarters', 14, 10, 0);
    const far = placePiece(base, 'workshop', 10, 20, 0);
    expect(besidePieces(base, infirmary)).toEqual([quarters]);
    expect(besidePieces(base, far)).toEqual([]);
  });
});

describe('the siege of the base', () => {
  /** A base with a ring of palisades round the caravan, a gate in it, a tower and some sandbags. */
  function walled(seed = 1): BaseState {
    const base = freshBase(new Rng(seed));
    const caravan = base.pieces[0]!;
    const [x0, y0, x1, y1] = [caravan.x - 3, caravan.y - 3, caravan.x + 6, caravan.y + 4];
    for (let x = x0; x <= x1; x++) {
      for (const y of [y0, y1]) placePiece(base, x === caravan.x + 1 && y === y0 ? 'gate' : 'palisade', x, y, 0);
    }
    for (let y = y0 + 1; y < y1; y++) for (const x of [x0, x1]) placePiece(base, 'palisade', x, y, 0);
    placePiece(base, 'sandbags', caravan.x, caravan.y + 3, 0);
    placePiece(base, 'tower', x0 + 1, y0 + 1, 0);
    return base;
  }

  it('is a siege on the base as it stands: pines, pieces on their tiles, what each holds, the squad by the caravan', () => {
    const base = walled();
    const s = createMission({ map: { kind: 'base', base }, slots: [0], rng: new Rng(2) });
    expect(s.mapId).toBe('base');
    expect(s.mission.kind).toBe('elimination');
    expect(s.mission.siege).toEqual({ hold: expect.any(Number) });
    const at = (x: number, y: number) => y * s.width + x;
    const tree = base.trees[0]!;
    expect(s.tiles[tree]).toBe('high');
    expect(decorOf(s.decor?.[tree])?.kind).toBe('tree');
    const palisade = base.pieces.find((p) => p.piece === 'palisade')!;
    expect(s.tiles[at(palisade.x, palisade.y)]).toBe('wall');
    expect(s.breakables![at(palisade.x, palisade.y)]).toBe(PIECES.palisade.hp);
    const gate = base.pieces.find((p) => p.piece === 'gate')!;
    expect(s.tiles[at(gate.x, gate.y)]).toBe('door');
    // The tower's deck, a storey up, with its ladder; the deck holds nothing to break.
    const tower = base.pieces.find((p) => p.piece === 'tower')!;
    expect(s.elev[at(tower.x, tower.y)]).toBe(1);
    expect(s.tiles[at(tower.x, tower.y + 2)]).toBe('ladder');
    expect(s.breakables![at(tower.x, tower.y)]).toBeUndefined();
    // The tracks, trodden earth; the rest, open ground.
    const mid = Math.floor((s.width - 1) / 2);
    expect(s.ground?.[at(mid, 3)]).toBe(3);
    expect(s.ground?.[at(3, 3)]).toBe(0);
    // The base goes with it, for the client to draw and the campaign to take back; its stumps too.
    expect(s.base?.pieces).toEqual(base.pieces);
    expect(s.base?.felled).toBeUndefined();
    fellTree(base, tree % base.width, Math.floor(tree / base.width));
    expect(createMission({ map: { kind: 'base', base }, slots: [0], rng: new Rng(2) }).base?.felled).toEqual([tree]);
    expect(s.base?.pieces[0]).not.toBe(base.pieces[0]);
    // The squad round the caravan, inside the ring.
    const caravan = base.pieces[0]!;
    for (const u of s.units.filter((x) => x.team === 'xcom')) expect(Math.hypot(u.pos.x - caravan.x - 2, u.pos.y - caravan.y - 1)).toBeLessThan(4);
  });

  it('the dead break the palisade or the gate to get at the soldiers', () => {
    const s = createMission({ map: { kind: 'base', base: walled(3) }, slots: [0], rng: new Rng(4), difficulty: 4 });
    const ctx = { slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(5) };
    const events: GameEvent[] = [];
    for (let turn = 0; turn < 8 && !s.outcome; turn++) {
      const r = executeCommand(s, { type: 'endTurn', ready: true }, ctx);
      if (r.ok) events.push(...r.events);
    }
    const blows = events.filter((e): e is Extract<GameEvent, { t: 'smashed' }> => e.t === 'smashed');
    expect(blows.length).toBeGreaterThan(0);
    expect(blows.every((e) => ['wall', 'door', 'floor', 'low'].includes(s.tiles[e.tile.y * s.width + e.tile.x]!))).toBe(true);
  });

  it('a campaign siege brings as many as its clock says, and someone by every side it heard', () => {
    for (const [count, sides] of [[14, [1, 3]], [20, [0, 1, 2]], [31, [2]], [10, [0, 3]]] as const) {
      for (let seed = 1; seed <= 20; seed++) {
        const waves = siegeWaves(2, new Rng(seed), { count, sides: [...sides] });
        expect(waves.flatMap((w) => w.units)).toHaveLength(count);
        expect(new Set(waves.map((w) => w.side))).toEqual(new Set(sides));
        // More each time, and the five specials as ever.
        const sizes = [1, 3, 5, 7, 9].map((t) => waves.filter((w) => w.turn === t).reduce((n, w) => n + w.units.length, 0));
        expect(sizes.every((n, i) => i === 0 || n >= sizes[i - 1]!)).toBe(true);
        expect(waves.flatMap((w) => w.units).filter((u) => u !== 'zombie').sort()).toEqual(['bloater', 'mec', 'sectoid', 'xenoid', 'xenoid']);
      }
    }
  });

  it('a wave never comes in where the pines close it off from the base', () => {
    // 18/10 test: a wave landed in a glade by the east edge with pines all round, and stood there.
    let landed = 0;
    for (let seed = 1; seed <= 6; seed++) {
      const base = walled(seed);
      const s = createMission({ map: { kind: 'base', base }, siegePlan: { count: 30, sides: [0, 1, 2, 3] }, slots: [0], rng: new Rng(seed) });
      // The squad stays put, by the caravan, behind its ring.
      const at = new Map<string, number>();
      const ctx = { slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(seed) };
      for (let turn = 0; turn < 10 && !s.outcome; turn++) {
        const r = executeCommand(s, { type: 'endTurn', ready: true }, ctx);
        if (r.ok) for (const e of r.events) if (e.t === 'spawned') for (const u of e.units) at.set(u.id, u.pos.y * s.width + u.pos.x);
      }
      // Joined to the caravan by ground or by what can be broken; the pines never are.
      const caravan = base.pieces[0]!;
      const open = new Set([caravan.y * s.width + caravan.x - 1]);
      const queue = [...open];
      while (queue.length) {
        const i = queue.pop()!;
        for (const j of [i - 1, i + 1, i - s.width, i + s.width]) {
          if (j < 0 || j >= s.tiles.length || open.has(j) || Math.abs((j % s.width) - (i % s.width)) > 1) continue;
          if (s.tiles[j] === 'high' && s.breakables?.[j] === undefined) continue;
          open.add(j);
          queue.push(j);
        }
      }
      for (const i of at.values()) expect(open.has(i)).toBe(true);
      landed += at.size;
    }
    expect(landed).toBeGreaterThan(50);
    // Sixty enemy turns of hordes of thirty a side, each of the dead finding its way round the rest (19/10): time for it.
  }, 20_000);
});

describe('the defences that act on their own', () => {
  /** A bare base: a caravan, and the defences given, all round (20, 20). */
  function armed(build: (base: BaseState) => void): GameState {
    const base = bare();
    placePiece(base, 'caravan', 18, 19, 0);
    build(base);
    return createMission({ map: { kind: 'base', base }, slots: [0], rng: new Rng(2) });
  }
  const zombieAt = (s: GameState, id: string, x: number, y: number): Unit => {
    const u = makeUnit(id, 'zombie', 'Zombi', null, { x, y }, 'pz');
    s.units.push(u);
    s.pods.push({ id: 'pz', active: true, unitIds: [id], patrol: [], patrolIndex: 0 });
    return s.units.at(-1)!;
  };

  it('they are set from the base: turrets as units nobody commands, traps, barbed wire, floodlights', () => {
    const s = armed((base) => {
      placePiece(base, 'turret', 25, 20, 0);
      placePiece(base, 'mine', 26, 20, 0);
      const spent = placePiece(base, 'snare', 27, 20, 0);
      spent.charges = 0;
      placePiece(base, 'wire', 28, 20, 0);
      placePiece(base, 'floodlight', 29, 20, 0);
    });
    const turret = s.units.find((u) => u.template === 'turret')!;
    expect(turret).toMatchObject({ team: 'xcom', owner: null, pos: { x: 25, y: 20 }, ammo: PIECES.turret.charges, automaton: { piece: 'b2' } });
    expect(s.traps).toEqual([{ piece: 'b3', kind: 'mine', node: 20 * s.width + 26, charges: 1 }]);
    expect(s.slow).toEqual([20 * s.width + 28]);
    expect(s.lights?.map((l) => [l.tile.x, l.tile.y])).toEqual([[29, 20]]);
    // Nobody gives it orders.
    const ctx = { slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(1) };
    expect(executeCommand(s, { type: 'ability', unit: turret.id, ability: 'overwatch' }, ctx)).toEqual({ ok: false, error: 'Va sola: nadie le da órdenes.' });
  });

  it('by night, a floodlight lights twice as far as a street light, and nobody but the waves comes', () => {
    // Arturo, 18/10: some sieges are by night, and the floodlight is what lets the squad see far in them.
    const base = bare();
    placePiece(base, 'caravan', 18, 19, 0);
    placePiece(base, 'floodlight', 29, 20, 0);
    const s = createMission({ map: { kind: 'base', base }, slots: [0], rng: new Rng(2), night: true });
    expect(s.mission.night).toBe(true);
    const [light] = s.lights!;
    expect(light!.radius).toBe(NIGHT.floodlight);
    expect(NIGHT.floodlight).toBeGreaterThan(NIGHT.streetLight);
    expect(light!.tiles).toContain(20 * s.width + 29 - NIGHT.floodlight);
    expect(s.units.filter((u) => u.team !== 'xcom')).toEqual([]);
  });

  it('a mine blows up whoever steps on it; spikes hurt and stop them; a snare holds them', () => {
    const s = armed((base) => {
      placePiece(base, 'mine', 24, 12, 0);
      placePiece(base, 'spikes', 24, 14, 0);
      placePiece(base, 'snare', 24, 16, 0);
    });
    const run = new Sim(s, new Rng(1), [0]);
    const a = zombieAt(s, 'z1', 23, 12);
    run.moveAlong(a, [{ x: 24, y: 12, z: 0 }, { x: 25, y: 12, z: 0 }], 1);
    expect(a.alive).toBe(false);
    expect(s.traps!.find((t) => t.kind === 'mine')!.charges).toBe(0);
    const b = zombieAt(s, 'z2', 23, 14);
    b.hp = b.maxHp = 9;
    run.moveAlong(b, [{ x: 24, y: 14, z: 0 }, { x: 25, y: 14, z: 0 }], 1);
    expect([b.pos.x, b.hp]).toEqual([24, 9 - TRAPS.spikes]);
    expect(s.traps!.find((t) => t.kind === 'spikes')!.charges).toBe(2);
    const c = zombieAt(s, 'z3', 23, 16);
    run.moveAlong(c, [{ x: 24, y: 16, z: 0 }, { x: 25, y: 16, z: 0 }], 1);
    expect([c.pos.x, c.stunned]).toEqual([24, true]);
    // Spent, a trap does nothing.
    const d = zombieAt(s, 'z4', 23, 12);
    run.moveAlong(d, [{ x: 24, y: 12, z: 0 }, { x: 25, y: 12, z: 0 }], 1);
    expect([d.pos.x, d.alive]).toEqual([25, true]);
  });

  it('barbed wire costs more to cross', () => {
    const s = armed((base) => placePiece(base, 'wire', 24, 12, 0));
    const z = zombieAt(s, 'z1', 23, 12);
    const reach = computeReach(s, z, 100);
    expect(reach.cost.get(12 * s.width + 24)).toBe(2 + TRAPS.wire);
  });

  it('a turret keeps watch through the enemy turn and shoots who comes in sight, but holds no base on its own', () => {
    const s = armed((base) => placePiece(base, 'turret', 22, 12, 0));
    const turret = s.units.find((u) => u.template === 'turret')!;
    zombieAt(s, 'z1', 22, 4);
    const run = new Sim(s, new Rng(3), [0]);
    run.runAlienTurn();
    const shots = run.events.filter((e): e is Extract<GameEvent, { t: 'shot' }> => e.t === 'shot' && e.unit === turret.id);
    expect(shots.length).toBeGreaterThan(0);
    expect(turret.ammo).toBe(PIECES.turret.charges! - shots.length);
    // Made to fire at what comes in: no loss of aim on a reaction shot.
    const z = s.units.find((u) => u.id === 'z1')!;
    const watch = previewShot(s, turret, z, { reaction: true })!;
    expect(watch.mods.some((m) => m.label === 'Fuego de reacción')).toBe(false);
    expect(watch.hit).toBe(previewShot(s, turret, z)!.hit);
    // With every soldier down, a turret still standing doesn't hold the base.
    const t = armed((base) => placePiece(base, 'turret', 22, 12, 0));
    for (const u of t.units.filter((x) => x.team === 'xcom' && !x.automaton)) u.alive = false;
    new Sim(t, new Rng(1), [0]).checkOutcome();
    expect([t.outcome, t.outcomeReason]).toEqual(['defeat', 'wiped']);
  });
});
