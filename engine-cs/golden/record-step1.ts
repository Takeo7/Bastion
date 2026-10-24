/**
 * Writes engine-cs/golden/step1/*.json: missions made by the TypeScript engine and what its step-1 modules (content,
 * grid, decor, cover, flights, path, los) answer on them, for engine-cs/tests/Step1GoldenTests.cs to repeat in C#
 * (docs/UNITY.md §3, hito 1 paso 1).
 *
 *   npx tsx engine-cs/golden/record-step1.ts
 *
 * Each mission file holds the state as JSON.stringify writes it and, sampled with a seeded Rng, the inputs and the
 * answers: lines of sight, the fog of each team, reaches and paths, steps, cover, flights, decor, lights. Pairs and
 * lists are flat arrays of numbers to keep the files small. math.json holds Math.atan2, Math.hypot and Math.round of
 * many values (Js.cs), content.json the tables of content.ts and some of its functions.
 */
import { mkdirSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import * as C from '../../packages/engine/src/content';
import { coverAgainst, coverSides } from '../../packages/engine/src/cover';
import { anchorOf, carPartner, decorCode, objectAt } from '../../packages/engine/src/decor';
import { bearsFlight, canStopAt, flightAt, flightFoot, flightLanding, hasLongFlights, restingPlace } from '../../packages/engine/src/flights';
import {
  blocksSightAt,
  DIRS8,
  heightAt,
  kindAt,
  levelCount,
  nodeOf,
  opaqueAtIndex,
  placesOn,
  posOf,
  roofSide,
  sameSpace,
  sightTop,
  storeysApart,
  surfaceAt,
} from '../../packages/engine/src/grid';
import {
  canSee,
  canSeeTile,
  detectionTo,
  facingTo,
  floorLevels,
  floorView,
  inViewCone,
  isLit,
  lightTiles,
  lineClear,
  litMask,
  peekOrigins,
  sightOrigin,
  sightTo,
  teamView,
  teamVisibility,
  visibleEnemies,
} from '../../packages/engine/src/los';
import { buildPath, computeReach, destinations, maxMoveCost, stepBetween, stepCost, stepsFrom } from '../../packages/engine/src/path';
import { Rng } from '../../packages/engine/src/rng';
import { createMission, makeUnit, type MapSource } from '../../packages/engine/src/setup';
import type { GameState, Pos, TemplateId, Unit } from '../../packages/engine/src/types';

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, 'step1');
mkdirSync(out, { recursive: true });

const bits = (xs: readonly boolean[]) => xs.map((b) => (b ? '1' : '0')).join('');
const posNode = (s: GameState, p: Pos | null | undefined) => (p ? nodeOf(s, p) : -1);

/** A random place: a tile, then one of the places on it (the ground or a storey with a slab). */
function randomPlace(s: GameState, r: Rng, near?: Pos, span = 12): Pos {
  const x = near ? Math.max(0, Math.min(s.width - 1, near.x + r.range(-span, span))) : r.int(s.width);
  const y = near ? Math.max(0, Math.min(s.height - 1, near.y + r.range(-span, span))) : r.int(s.height);
  return r.pick(placesOn(s, x, y));
}

function record(name: string, s: GameState): void {
  const r = new Rng(12345);
  const area = s.width * s.height;
  const nodes = area * levelCount(s);
  const g: Record<string, unknown> = { name };
  // The state as it is before anything below runs (teamView and the rest only read it).
  g.state = JSON.parse(JSON.stringify(s));
  const alive = s.units.filter((u) => u.alive);

  // ------------------------------------------------------------ lines of sight
  // [ax, ay, az, bx, by, bz, ha (-1 default), hb (-1 default), reach (-1 Infinity, -2 default), clear]
  const lines: number[] = [];
  for (let n = 0; n < 2000; n++) {
    const a = n % 4 === 0 ? randomPlace(s, r) : r.pick(alive).pos;
    const b = n % 3 === 0 ? randomPlace(s, r) : n % 7 === 0 ? { ...a } : randomPlace(s, r, a);
    const heights = n % 2 === 1;
    const ha = heights ? Math.round(r.next() * 3500) / 1000 : -1;
    const hb = heights ? (n % 5 === 0 ? r.next() * 3.2 : Math.round(r.next() * 3500) / 1000) : -1;
    const reach = n % 11 === 0 ? -1 : n % 13 === 0 ? r.int(3) : -2;
    const clear = lineClear(s, a, b, ha < 0 ? undefined : ha, hb < 0 ? undefined : hb, reach === -1 ? Infinity : reach === -2 ? undefined : reach);
    lines.push(a.x, a.y, a.z, b.x, b.y, b.z, ha, hb, reach, clear ? 1 : 0);
  }
  g.lines = lines;

  // ------------------------------------------------------------ the fog
  g.visXcom = bits(teamVisibility(s, 'xcom'));
  g.visAlien = bits(teamVisibility(s, 'alien'));
  g.viewXcom = bits(teamView(s, 'xcom'));
  g.viewAlien = bits(teamView(s, 'alien'));
  g.visReach = bits(teamVisibility(s, 'xcom', 9.5));
  g.floors = Array.from({ length: levelCount(s) + 1 }, (_, f) => ({ levels: floorLevels(s, f), view: bits(floorView(s, teamVisibility(s, 'xcom'), f)) }));
  g.visibleXcom = visibleEnemies(s, 'xcom').map((u) => u.id);
  g.visibleAlien = visibleEnemies(s, 'alien').map((u) => u.id);

  // ------------------------------------------------------------ who sees whom
  // By ordered pair of living units (viewer, target): canSee, inViewCone, sightOrigin's node with sightTo's range (-1 none).
  const see: string[] = [];
  const cone: string[] = [];
  const origin: number[] = [];
  const sightRange: number[] = [];
  const detection: number[] = [];
  for (const v of alive) {
    for (const t of alive) {
      see.push(canSee(s, v, t) ? '1' : '0');
      cone.push(inViewCone(v, t.pos) ? '1' : '0');
      const range = sightTo(s, v, t.pos);
      sightRange.push(range);
      detection.push(detectionTo(s, v, t.pos));
      origin.push(posNode(s, sightOrigin(s, v.pos, t.pos, range)));
    }
  }
  g.unitIds = alive.map((u) => u.id);
  g.canSee = see.join('');
  g.inViewCone = cone.join('');
  g.sightOrigin = origin;
  g.sightTo = sightRange;
  g.detectionTo = detection;
  g.peek = alive.map((u) => peekOrigins(s, u.pos).map((p) => nodeOf(s, p)));
  // A facing given (a patrol on its way): [viewer index, x, y, facing, in cone]
  const cones: number[] = [];
  for (let n = 0; n < 1500; n++) {
    const v = r.pick(alive);
    const p = randomPlace(s, r, v.pos, 6);
    const facing = n % 3 === 0 ? (r.int(8) * Math.PI) / 4 : r.next() * 7 - 0.5;
    cones.push(alive.indexOf(v), p.x, p.y, facing, inViewCone(v, p, undefined, facing) ? 1 : 0);
  }
  g.cones = cones;
  // canSeeTile: [from node, to node, range, seen]
  const tiles: number[] = [];
  for (let n = 0; n < 1000; n++) {
    const from = r.pick(alive).pos;
    const to = randomPlace(s, r, from, 16);
    const range = r.range(4, 20);
    tiles.push(nodeOf(s, from), nodeOf(s, to), range, canSeeTile(s, from, to, range) ? 1 : 0);
  }
  g.canSeeTile = tiles;
  const facings: number[] = [];
  for (let n = 0; n < 500; n++) {
    const a = randomPlace(s, r);
    const b = n % 9 === 0 ? a : randomPlace(s, r, a);
    facings.push(a.x, a.y, b.x, b.y, facingTo(a, b) ?? 99);
  }
  g.facingTo = facings;

  // ------------------------------------------------------------ night
  g.lightTiles = (s.lights ?? []).map((l) => lightTiles(s, l.tile, l.radius, C.NIGHT.lampHeight));
  g.litMask = bits(litMask(s));
  g.isLit = bits(Array.from({ length: nodes }, (_, i) => isLit(s, posOf(s, i))));

  // ------------------------------------------------------------ reaches and paths
  const xcom = alive.filter((u) => u.team === 'xcom');
  const enemies = alive.filter((u) => u.team !== 'xcom');
  const walkers: { u: Unit; maxCost: number; from?: Pos; siege?: boolean; slow?: number[] }[] = [
    ...xcom.slice(0, 3).map((u) => ({ u, maxCost: maxMoveCost(u) })),
    ...enemies.slice(0, 4).map((u) => ({ u, maxCost: maxMoveCost({ ...u, ap: 2 }) })),
    { u: xcom[0]!, maxCost: 400 },
    { u: enemies[enemies.length - 1]!, maxCost: 120 },
    { u: xcom[1]!, maxCost: 60, from: randomPlace(s, r, xcom[1]!.pos, 6) },
  ];
  const zombie = enemies.find((u) => C.TEMPLATES[u.template].undead);
  if (zombie) {
    const slow = Array.from({ length: 40 }, () => nodeOf(s, randomPlace(s, r, zombie.pos, 8)));
    walkers.push({ u: zombie, maxCost: 80, siege: true, slow });
  }
  g.reaches = walkers.map(({ u, maxCost, from, siege, slow }) => {
    const t = siege ? (JSON.parse(JSON.stringify(s)) as GameState) : s;
    if (siege) {
      t.mission.siege = { hold: 11 };
      t.slow = slow;
    }
    const w = t.units.find((o) => o.id === u.id)!;
    const reach = computeReach(t, w, maxCost, from);
    const entries: number[] = [];
    for (const [node, cost] of reach.cost) entries.push(node, cost, reach.prev.get(node) ?? -1);
    const dest = destinations(t, w, reach).flatMap((d) => [nodeOf(t, d.pos), d.cost]);
    const keys = [...reach.cost.keys()];
    const paths: number[][] = [];
    for (let n = 0; n < 30; n++) {
      const to = n % 3 === 0 ? randomPlace(t, r, w.pos, 10) : posOf(t, r.pick(keys));
      const path = buildPath(t, w, reach, to);
      paths.push([nodeOf(t, to), ...(path ? path.map((p) => nodeOf(t, p)) : [-1])]);
    }
    return { unit: u.id, maxCost, from: from ? nodeOf(s, from) : -1, siege: !!siege, slow: slow ?? [], entries, dest, paths };
  });

  // ------------------------------------------------------------ steps
  // stepsFrom: [node, dir (DIRS8), count, to, cost, …]
  const steps: number[] = [];
  for (let n = 0; n < 3000; n++) {
    const p = n % 2 ? randomPlace(s, r) : posOf(s, r.int(nodes));
    const d = r.int(8);
    const found = stepsFrom(s, p, DIRS8[d]!.x, DIRS8[d]!.y);
    steps.push(nodeOf(s, p), d, found.length, ...found.flatMap((f) => [nodeOf(s, f.to), f.cost]));
  }
  g.steps = steps;
  // stepCost on the ground: [x, y, dir, cost (-1 null)]
  const costs: number[] = [];
  for (let n = 0; n < 2000; n++) {
    const x = r.int(s.width);
    const y = r.int(s.height);
    const d = r.int(8);
    costs.push(x, y, d, stepCost(s, x, y, DIRS8[d]!.x, DIRS8[d]!.y) ?? -1);
  }
  g.stepCost = costs;
  // stepBetween: [from node, to node, cost (-1 null)]
  const between: number[] = [];
  for (let n = 0; n < 2000; n++) {
    const a = randomPlace(s, r);
    const b = randomPlace(s, r, a, 1);
    between.push(nodeOf(s, a), nodeOf(s, b), stepBetween(s, a, b) ?? -1);
  }
  g.stepBetween = between;

  // ------------------------------------------------------------ places
  g.canStop = bits(Array.from({ length: nodes }, (_, i) => canStopAt(s, posOf(s, i))));
  g.bears = bits(Array.from({ length: nodes }, (_, i) => bearsFlight(s, posOf(s, i))));
  g.cover = Array.from({ length: nodes }, (_, i) => coverSides(s, posOf(s, i)).join('')).join('');
  g.sightTop = Array.from({ length: area }, (_, i) => sightTop(s, i % s.width, Math.floor(i / s.width)));
  g.hasLongFlights = hasLongFlights(s);
  // Every ladder place: [node, step (-1 none), up, tiles, foot node, landing node, resting node]
  const flights: number[] = [];
  for (let i = 0; i < nodes; i++) {
    const p = posOf(s, i);
    if (kindAt(s, p) !== 'ladder') continue;
    const at = flightAt(s, p);
    flights.push(i, at ? at.step : -1, at ? at.flight.up : -1, at ? at.flight.tiles.length : 0, at ? nodeOf(s, flightFoot(at.flight)) : -1, at ? nodeOf(s, flightLanding(at.flight)) : -1, nodeOf(s, restingPlace(s, p)));
  }
  g.flights = flights;
  // [a node, b node, sameSpace, storeysApart]
  const spaces: number[] = [];
  for (let n = 0; n < 1500; n++) {
    const a = randomPlace(s, r);
    const b = randomPlace(s, r, a, 2);
    spaces.push(nodeOf(s, a), nodeOf(s, b), sameSpace(s, a, b) ? 1 : 0, storeysApart(s, a, b));
  }
  g.spaces = spaces;
  // [x, y, h, surface node (-1 null)]
  const surfaces: number[] = [];
  for (let n = 0; n < 1500; n++) {
    const x = r.range(-1, s.width);
    const y = r.range(-1, s.height);
    const h = r.range(-1, 4);
    surfaces.push(x, y, h, posNode(s, surfaceAt(s, x, y, h)));
  }
  g.surfaces = surfaces;
  // [x, y, h, blocksSightAt, opaqueAtIndex (in bounds), roofSide (0 null, 1 under, 2 over)]
  const sights: number[] = [];
  for (let n = 0; n < 3000; n++) {
    const x = r.int(s.width);
    const y = r.int(s.height);
    const h = n % 4 ? r.next() * 3.8 : r.int(8) / 2;
    const roof = roofSide(s, x, y, h);
    sights.push(x, y, h, blocksSightAt(s, x, y, h) ? 1 : 0, opaqueAtIndex(s, y * s.width + x, h) ? 1 : 0, roof === null ? 0 : roof === 'under' ? 1 : 2);
  }
  g.sights = sights;
  // coverAgainst: [target node, attacker x, attacker y, cover]
  const against: number[] = [];
  for (let n = 0; n < 3000; n++) {
    const t = n % 2 ? r.pick(alive).pos : randomPlace(s, r);
    const a = n % 5 === 0 ? { x: t.x, y: t.y } : { x: t.x + r.range(-15, 15), y: t.y + r.range(-15, 15) };
    against.push(nodeOf(s, t), a.x, a.y, coverAgainst(s, t, a));
  }
  g.coverAgainst = against;

  // ------------------------------------------------------------ decor
  const decors = [s.decor, ...(s.levels ?? []).map((l) => l.decor)];
  g.decor = decors.map((decor) => {
    if (!decor) return null;
    const anchors: number[] = [];
    const objects: number[] = [];
    const cars: number[] = [];
    for (let i = 0; i < decor.length; i++) {
      const a = anchorOf(decor, s.width, i);
      anchors.push(a);
      const o = objectAt(decor, s.width, i);
      if (o) objects.push(i, decorCode(o.kind, o.facing), o.anchor, o.tiles.length, ...o.tiles);
      const c = carPartner(decor, s.width, i);
      if (c >= 0) cars.push(i, c);
    }
    return { anchors, objects, cars };
  });
  // heightAt of every place, a check on nodeOf and posOf.
  g.heights = Array.from({ length: nodes }, (_, i) => heightAt(s, posOf(s, i)));

  writeFileSync(join(out, `${name}.json`), JSON.stringify(g));
  console.log(name, `${(JSON.stringify(g).length / 1024).toFixed(0)} KB`);
}

const missions: [string, MapSource, number, boolean][] = [
  ['city-1', { kind: 'generated', biome: 'city', seed: 1 }, 1, false],
  ['city-7', { kind: 'generated', biome: 'city', seed: 7 }, 7, false],
  ['wilds-1', { kind: 'generated', biome: 'wilds', seed: 1 }, 1, false],
  ['wilds-7', { kind: 'generated', biome: 'wilds', seed: 7 }, 7, false],
  ['facility-1', { kind: 'generated', biome: 'facility', seed: 1 }, 1, false],
  ['facility-7', { kind: 'generated', biome: 'facility', seed: 7 }, 7, false],
  ['torre', { kind: 'handmade', id: 'torre' }, 3, false],
  ['city-5-night', { kind: 'generated', biome: 'city', seed: 5 }, 5, true],
  ['wilds-3-night', { kind: 'generated', biome: 'wilds', seed: 3 }, 3, true],
];
for (const [name, map, seed, night] of missions) {
  record(name, createMission({ map, slots: [0, 1], rng: new Rng(seed), night }));
}

// ------------------------------------------------------------ JavaScript's Math
{
  const r = new Rng(777);
  const atan2: number[] = [];
  for (let y = -40; y <= 40; y++) for (let x = -40; x <= 40; x++) atan2.push(y, x, Math.atan2(y, x));
  for (let n = 0; n < 3000; n++) {
    const y = (r.next() - 0.5) * 10 ** r.range(-8, 8);
    const x = (r.next() - 0.5) * 10 ** r.range(-8, 8);
    atan2.push(y, x, Math.atan2(y, x));
  }
  // JSON writes -0 as 0: atan2(-0, -1) is a test of its own (Step1GoldenTests).
  for (const [y, x] of [[0, -1], [0, 0], [1, 0], [-1, 0], [1e300, 1e-300], [1e-300, -1e300], [1, 1], [-3, 1]]) atan2.push(y!, x!, Math.atan2(y!, x!));
  const hypot: number[] = [];
  for (let y = 0; y <= 100; y++) for (let x = 0; x <= 100; x++) hypot.push(x, y, Math.hypot(x, y));
  for (let n = 0; n < 3000; n++) {
    const x = (r.next() - 0.5) * 10 ** r.range(-6, 6);
    const y = (r.next() - 0.5) * 10 ** r.range(-6, 6);
    hypot.push(x, y, Math.hypot(x, y));
  }
  const round: number[] = [];
  for (const x of [0.5, 1.5, 2.5, -0.5, -1.5, -2.5, 0.49999999999999994, -0.49999999999999994, 3.7, -3.7, 1e15 + 0.5, 4503599627370497]) round.push(x, Math.round(x));
  for (let n = 0; n < 1000; n++) {
    const x = (r.next() - 0.5) * 10 ** r.range(0, 6);
    round.push(x, Math.round(x));
  }
  const coverMinDot = Math.cos((C.COVER_MAX_ANGLE_DEG * Math.PI) / 180);
  writeFileSync(join(out, 'math.json'), JSON.stringify({ atan2, hypot, round, coverMinDot }));
  console.log('math');
}

// ------------------------------------------------------------ content.ts
{
  const templates = Object.keys(C.TEMPLATES) as TemplateId[];
  const hostile = templates.flatMap((a) => templates.map((b) => (C.hostileTo({ team: C.TEMPLATES[a].team, template: a }, { team: C.TEMPLATES[b].team, template: b }) ? '1' : '0'))).join('');
  const names = [
    ...C.SOLDIER_NAMES.map((n) => n.name),
    'Kenji Watanabe', '  Ana   Pérez ', 'Luis', 'Nadie Conocido', '', 'Óscar', 'Pablo\u00a0Rey', 'Iker\tOrtega', 'Ana Ríos',
  ];
  const sexes = names.map((n) => C.sexOfName(n) ?? null);
  const ranges: Record<string, number[]> = {};
  for (const table of Object.keys(C.RANGE_TABLES) as C.RangeTable[]) ranges[table] = Array.from({ length: 70 }, (_, i) => C.rangeModifier(table, i * 0.5));
  // Units with and without their own arms, and what each attack fires.
  const units: Unit[] = [];
  const add = (u: Unit) => units.push(u);
  add(makeUnit('s1', 'assault', 'A', 0, { x: 1, y: 2 }, null));
  add(makeUnit('s2', 'sharpshooter', 'B', 1, { x: 1, y: 2 }, null));
  add(makeUnit('a1', 'zombie', 'C', null, { x: 4, y: 5 }, 'p1'));
  add(makeUnit('a2', 'lancer', 'D', null, { x: 4, y: 5 }, 'p1'));
  add({ ...makeUnit('s3', 'rookie', 'E', 0, { x: 1, y: 2 }, null), arms: { primary: 'sawnOff', mods: ['extMag'], condition: 50, melee: 'machete' }, pistolAmmo: 2, ammo: 1 });
  add({ ...makeUnit('s4', 'rookie', 'F', 0, { x: 1, y: 2 }, null), arms: { primary: null, melee: null }, ammo: 0 });
  add({ ...makeUnit('s5', 'specialist', 'G', 0, { x: 1, y: 2 }, null), arms: { primary: 'scopedRifle', condition: 0 }, pistolAmmo: 6, ammo: 3, rank: 3 });
  add({ ...makeUnit('s6', 'grenadier', 'H', 0, { x: 1, y: 2 }, null), arms: { primary: 'precisionRifle', mods: ['scope'] }, ammo: 3 });
  const attacks = Object.keys(C.ABILITIES).filter((a) => C.isAttack(a as never));
  const fires = units.map((u) => ({
    weapons: attacks.map((a) => C.attackWeapon(u, a as never).id),
    clips: attacks.map((a) => C.clipOf(u, a as never)),
    sidearm: attacks.map((a) => C.firesSidearm(u, a as never)),
    rounds: attacks.map((a) => C.roundsOf(u, a as never)),
    loaded: attacks.map((a) => C.loadedFor(u, a as never)),
    primary: Object.keys(C.ABILITIES).map((a) => C.firesPrimary(u, a as never)),
    condition: C.conditionOf(u),
    fullyLoaded: C.fullyLoaded(u),
    squadsight: C.hasSquadsight(u),
    apPerTurn: C.apPerTurn(u),
    reloaded: (() => {
      const v = JSON.parse(JSON.stringify(u)) as Unit;
      C.reloadWeapon(v);
      return [v.ammo, v.pistolAmmo ?? null];
    })(),
  }));
  writeFileSync(
    join(out, 'content.json'),
    JSON.stringify({
      tables: {
        templates: C.TEMPLATES,
        weapons: C.WEAPONS,
        abilities: C.ABILITIES,
        items: C.ITEMS,
        foundWeapons: C.FOUND_WEAPONS,
        weaponMods: C.WEAPON_MODS,
        plainWeapon: C.PLAIN_WEAPON,
        rangeTables: C.RANGE_TABLES,
        infectionBite: C.INFECTION.bite,
        soldierNames: C.SOLDIER_NAMES,
        playerColors: C.PLAYER_COLORS,
        thrown: [...C.THROWN],
        zeroMods: C.ZERO_MODS,
      },
      constants: {
        apPerTurn: C.AP_PER_TURN, thirdActionRank: C.THIRD_ACTION_RANK, spareMags: C.SPARE_MAGS, fallDamage: C.FALL_DAMAGE,
        lowCoverBonus: C.LOW_COVER_BONUS, highCoverBonus: C.HIGH_COVER_BONUS, hunkerDefense: C.HUNKER_DEFENSE, hunkerDodge: C.HUNKER_DODGE,
        reactionPenalty: C.REACTION_PENALTY, grazeDamageMult: C.GRAZE_DAMAGE_MULT, squadsightDistanceMod: C.SQUADSIGHT_DISTANCE_MOD,
        squadsightCritMod: C.SQUADSIGHT_CRIT_MOD, medkitHeal: C.MEDKIT_HEAL, stoneDamage: C.STONE_DAMAGE, heightAdvantageAim: C.HEIGHT_ADVANTAGE_AIM,
        coverMaxAngleDeg: C.COVER_MAX_ANGLE_DEG, recoveryTurns: C.RECOVERY_TURNS, hackBase: C.HACK_BASE, psiBase: C.PSI_BASE,
        disorientedAim: C.DISORIENTED_AIM, suppressedAim: C.SUPPRESSED_AIM, holoAim: C.HOLO_AIM, ruptureDamage: C.RUPTURE_DAMAGE,
        controlTurns: C.CONTROL_TURNS, restorationHeal: C.RESTORATION_HEAL, smokeDefense: C.SMOKE_DEFENSE, smokeTurns: C.SMOKE_TURNS,
        backpackSlots: C.BACKPACK_SLOTS, medicineHeal: C.MEDICINE_HEAL, pistolClip: C.PISTOL_CLIP, modSlots: C.MOD_SLOTS, modAim: C.MOD_AIM,
        extMag: C.EXT_MAG, wornBelow: C.WORN_BELOW, wornAim: C.WORN_AIM, repairPerPart: C.REPAIR_PER_PART, patchUp: C.PATCH_UP,
        soldierMobility: C.SOLDIER_MOBILITY, zombiePace: C.ZOMBIE_PACE, squadSize: C.SQUAD_SIZE,
        noise: C.NOISE, traps: C.TRAPS, siege: C.SIEGE, horde: C.HORDE, night: C.NIGHT,
        infectionBurst: C.INFECTION.burst, infectionVest: C.INFECTION.vest,
      },
      templateIds: templates,
      hostile,
      names,
      sexes,
      ranges,
      hordePoints: Array.from({ length: 40 }, (_, i) => C.hordePoints(i * 0.75)),
      wear: Object.keys(C.WEAPONS).map((w) => C.wearOf(w as C.WeaponId)),
      units,
      attacks,
      fires,
      commandsFor: [0, 1].flatMap((slot) =>
        [null, 0, 1].flatMap((owner) => [undefined, [], [0], [1], [0, 1]].map((connected) => (C.commandsFor(slot as 0 | 1, owner as 0 | 1 | null, connected as (0 | 1)[] | undefined) ? 1 : 0))),
      ),
    }),
  );
  console.log('content');
}
