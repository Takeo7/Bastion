// The trailer's combat shots (docs/TRAILER.md, takes 5 and 7), for Arte's trailer.html: for each, a mission
// state, the orders that play the action ({ at, slot, cmd }, run in that order through executeCommand with
// one Rng(seed) and `connected`, as the server would) and where the action is, for the camera. Every shot is
// played here first with the real rules, trying seeds until the action comes out as the shot needs it (the
// blow kills, the wall breaks, the bus leaves one behind…), and only then written.
//
// npx tsx packages/client/tools/trailer/tomas.ts [partidas folder] [tokenAna] [tokenArturo]
// Reads <folder>/base.json (escaparate.ts) for the squad, writes <folder>/tomas/<id>.json, the key art's state
// (tomas/portada.json) and the store page's combat captures as server saves (<folder>/capturas/<id>.json).
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  applyEvent,
  buildSquad,
  canSee,
  canStandOn,
  cloneState,
  createMission,
  decorCode,
  executeCommand,
  inEvacZone,
  makeUnit,
  roomAt,
  Rng,
  type CampaignState,
  type Command,
  type GameEvent,
  type GameState,
  type MapSource,
  type Pos,
  type Slot,
  type TemplateId,
  type Unit,
} from '../../../engine/src/index';

const folder = process.argv[2] ?? '.dev-snapshots/trailer/partidas';
const campaign = (JSON.parse(readFileSync(join(folder, 'base.json'), 'utf8')) as { campaign: CampaignState }).campaign;

interface Order {
  /** Seconds from the start of the shot. */
  at: number;
  slot: Slot;
  cmd: Command;
}
interface Shot {
  id: string;
  /** What it shows, for whoever reads the file. */
  what: string;
  state: GameState;
  orders: Order[];
  /** Seats connected while it plays (ctx.connected). */
  connected: Slot[];
  /** Where the action is, for the camera. */
  focus: { x: number; y: number; level: number };
  /** Whether the events of a run show what the shot needs. */
  check: (events: GameEvent[], s: GameState) => boolean;
}

// ------------------------------------------------------------------ helpers

const W = (s: GameState) => s.width;
const idx = (s: GameState, p: { x: number; y: number }) => p.y * W(s) + p.x;
const inside = (s: GameState, p: { x: number; y: number }) => p.x >= 0 && p.y >= 0 && p.x < s.width && p.y < s.height;
const kind = (s: GameState, p: { x: number; y: number }) => (inside(s, p) ? s.tiles[idx(s, p)]! : null);
const indoor = (s: GameState, p: { x: number; y: number }) => s.ground?.[idx(s, p)] === 2;
const occupied = (s: GameState, p: Pos) => s.units.some((u) => u.alive && u.pos.x === p.x && u.pos.y === p.y && u.pos.z === p.z);
const freeAt = (s: GameState, p: Pos) => inside(s, p) && p.z === 0 && canStandOn(kind(s, p)) && kind(s, p) !== 'door' && kind(s, p) !== 'window' && !occupied(s, p);
const P = (x: number, y: number): Pos => ({ x, y, z: 0 });
const DIRS = [P(0, -1), P(1, 0), P(0, 1), P(-1, 0)];
const add = (a: Pos, b: Pos, k = 1): Pos => ({ x: a.x + b.x * k, y: a.y + b.y * k, z: 0 });
const all = (s: GameState) => Array.from({ length: s.width * s.height }, (_, i) => P(i % s.width, Math.floor(i / s.width)));
/** Places for an action: at least 7 tiles from every edge (the camera sees around them), the middle of the map first. */
const tiles = (s: GameState) =>
  all(s)
    .filter((p) => p.x >= 7 && p.y >= 7 && p.x < s.width - 7 && p.y < s.height - 7)
    .sort((a, b) => Math.hypot(a.x - s.width / 2, a.y - s.height / 2) - Math.hypot(b.x - s.width / 2, b.y - s.height / 2));

/** A mission on `map` with the showcase campaign's squad (owned by `slot`), and only one dead far away, asleep, so it goes on. */
function base(map: MapSource, opts: { night?: boolean; kind?: 'elimination' | 'recovery'; squad?: number; slot?: Slot } = {}): GameState {
  const s = createMission({ map, slots: [0, 1], rng: new Rng(1), kind: opts.kind ?? 'elimination', squad: buildSquad(campaign), pods: [['zombie']], difficulty: 2, night: opts.night });
  const keep = opts.squad ?? 4;
  const squad = s.units.filter((u) => u.team === 'xcom').slice(0, keep);
  for (const u of squad) u.owner = opts.slot ?? 0;
  const sleeper = s.units.find((u) => u.team === 'alien')!;
  s.units = [...squad, sleeper];
  s.pods = s.pods.filter((p) => p.unitIds.includes(sleeper.id));
  s.mission.reinforcements = [];
  s.concealed = false;
  s.command = opts.slot ?? 0;
  return s;
}

/** Moves the sleeper to the far corner from `from`, out of everything. */
function farSleeper(s: GameState, from: Pos): void {
  const z = s.units.find((u) => u.team === 'alien' && s.pods.some((p) => p.unitIds.includes(u.id)))!;
  const far = all(s).filter((p) => freeAt(s, p)).sort((a, b) => Math.hypot(b.x - from.x, b.y - from.y) - Math.hypot(a.x - from.x, a.y - from.y))[0]!;
  z.pos = far;
}

let nextId = 1;
/** Adds the dead (or the living) of `template` at `at`, in one pod, active or asleep. */
function enemies(s: GameState, template: TemplateId, at: Pos[], active: boolean, extra: Partial<Unit> = {}): Unit[] {
  const pod = `tp${nextId}`;
  const out = at.map((p) => ({ ...makeUnit(`t${nextId++}`, template, '', null, p, pod), ...extra }) as Unit);
  for (const u of out) u.name = template === 'zombie' ? 'Zombi' : template === 'bloater' ? 'Hinchado' : template === 'sectoid' ? 'Chillón' : template;
  s.units.push(...out);
  s.pods.push({ id: pod, active, unitIds: out.map((u) => u.id), patrol: [], patrolIndex: 0 });
  return out;
}

/** The squad around `at`: the first on it, the rest on free tiles near it (never on `avoid`). */
function placeSquad(s: GameState, at: Pos, avoid: Pos[] = []): Unit[] {
  const squad = s.units.filter((u) => u.team === 'xcom');
  const taken = (p: Pos) => avoid.some((a) => a.x === p.x && a.y === p.y);
  squad[0]!.pos = at;
  const around = tiles(s)
    .filter((p) => freeAt(s, p) && !taken(p) && indoor(s, p) === indoor(s, at) && Math.hypot(p.x - at.x, p.y - at.y) <= 3)
    .sort((a, b) => Math.hypot(a.x - at.x, a.y - at.y) - Math.hypot(b.x - at.x, b.y - at.y));
  squad.slice(1).forEach((u, i) => (u.pos = around[i + 1] ?? around[i] ?? at));
  return squad;
}

const of = <T extends GameEvent['t']>(events: GameEvent[], t: T) => events.filter((e): e is Extract<GameEvent, { t: T }> => e.t === t);

/** Plays a shot's orders with `seed`; its events, or null if an order was refused. */
function play(shot: Shot, seed: number): { events: GameEvent[]; s: GameState } | null {
  const s = cloneState(shot.state);
  const rng = new Rng(seed);
  const events: GameEvent[] = [];
  for (const o of shot.orders) {
    const r = executeCommand(s, o.cmd, { slot: o.slot, connected: shot.connected, rng });
    if (!r.ok) return null;
    events.push(...r.events);
  }
  // A client replaying the events has to end where the host does.
  const replay = cloneState(shot.state);
  for (const e of events) applyEvent(replay, e);
  return { events, s };
}

// -------------------------------------------------------------------- shots

const CITY: MapSource = { kind: 'generated', biome: 'city', seed: 7 };

/** Overwatch on a corner: the dead come round it in the enemy turn and the reaction shot goes. */
function overwatch(): Shot {
  const s = base(CITY);
  // An open street: a soldier with 10 free tiles in a row to one side.
  const spot = tiles(s).find((p) => freeAt(s, p) && !indoor(s, p) && DIRS.some((d) => [1, 2, 3, 4, 5, 6, 7, 8, 9, 10].every((k) => freeAt(s, add(p, d, k)) && !indoor(s, add(p, d, k)))))!;
  const dir = DIRS.find((d) => [1, 2, 3, 4, 5, 6, 7, 8, 9, 10].every((k) => freeAt(s, add(spot, d, k)) && !indoor(s, add(spot, d, k))))!;
  const squad = placeSquad(s, spot);
  for (const u of squad) u.overwatch = true;
  enemies(s, 'zombie', [add(spot, dir, 9), add(add(spot, dir, 10), DIRS[(DIRS.indexOf(dir) + 1) % 4]!)], true);
  farSleeper(s, spot);
  return {
    id: 'vigilancia',
    what: 'La escuadra en vigilancia; en el turno de los muertos, dos se acercan por la calle y salta el disparo de reacción.',
    state: s,
    orders: [{ at: 0.2, slot: 0, cmd: { type: 'endTurn', ready: true } }],
    connected: [0, 1],
    focus: { ...add(spot, dir, 6), level: 0 },
    check: (ev) => of(ev, 'shot').some((e) => e.reaction && (e.result === 'hit' || e.result === 'crit')) && of(ev, 'died').length > 0,
  };
}

/** The machete: one blow at the dead beside the soldier, and it falls. */
function machete(): Shot {
  const s = base(CITY);
  const spot = tiles(s).find((p) => freeAt(s, p) && !indoor(s, p) && freeAt(s, add(p, DIRS[1]!)) && freeAt(s, add(p, DIRS[1]!, 2)))!;
  const squad = placeSquad(s, spot, [add(spot, DIRS[1]!)]);
  const blade = squad[0]!;
  blade.arms = { ...(blade.arms ?? { primary: null }), melee: 'machete', primary: blade.arms?.primary ?? null };
  const [z] = enemies(s, 'zombie', [add(spot, DIRS[1]!)], true, { hp: 3 });
  farSleeper(s, spot);
  return {
    id: 'machete',
    what: 'Un soldado con el machete: el Tajo al zombi de al lado, que cae muerto.',
    state: s,
    orders: [{ at: 0.2, slot: 0, cmd: { type: 'ability', unit: blade.id, ability: 'slash', target: z!.id } }],
    connected: [0, 1],
    focus: { ...spot, level: 0 },
    check: (ev) => of(ev, 'died').some((e) => e.unit === z!.id),
  };
}

/** A wall with a room behind it and the street in front: [wall, outside, inside]. */
function wallSpots(s: GameState): [Pos, Pos, Pos][] {
  const out: [Pos, Pos, Pos][] = [];
  for (const w of tiles(s)) {
    if (kind(s, w) !== 'wall') continue;
    for (const d of DIRS) {
      const o = add(w, d);
      const i = add(w, d, -1);
      if (freeAt(s, o) && !indoor(s, o) && freeAt(s, i) && indoor(s, i) && freeAt(s, add(w, d, 4)) && !indoor(s, add(w, d, 4)) && freeAt(s, add(i, d, -1))) out.push([w, o, i]);
    }
  }
  return out;
}

/** The grenade: thrown at a wall, it breaks it, and the dead behind it get the blast. */
function grenade(): Shot {
  const s = base(CITY);
  const spots = wallSpots(s);
  for (const [w, o, i] of spots) {
    const d = { x: o.x - w.x, y: o.y - w.y, z: 0 };
    const thrower = add(w, d, 5);
    if (!freeAt(s, thrower) || indoor(s, thrower)) continue;
    const t = cloneState(s);
    const squad = placeSquad(t, thrower);
    const z = enemies(t, 'zombie', [i, add(i, d, -1)], false);
    farSleeper(t, thrower);
    return {
      id: 'granada',
      what: 'Una granada contra la pared: la pared se rompe y los dos zombis de detrás se llevan la explosión.',
      state: t,
      orders: [{ at: 0.2, slot: 0, cmd: { type: 'ability', unit: squad[0]!.id, ability: 'grenade', tile: { x: o.x, y: o.y } } }],
      connected: [0, 1],
      focus: { ...w, level: 0 },
      check: (ev) => of(ev, 'tileChanged').some((e) => e.decor === 'breach') && of(ev, 'damaged').some((e) => z.some((u) => u.id === e.unit)),
    };
  }
  throw new Error('granada: no wall');
}

/** The window: a soldier inside jumps out through it. */
function windowJump(): Shot {
  const s = base(CITY);
  for (const wn of tiles(s)) {
    if (kind(s, wn) !== 'window') continue;
    for (const d of DIRS) {
      const i = add(wn, d, -1);
      const o = add(wn, d, 2);
      if (!(freeAt(s, i) && indoor(s, i) && freeAt(s, add(wn, d)) && !indoor(s, add(wn, d)) && freeAt(s, o) && !indoor(s, o))) continue;
      const t = cloneState(s);
      const squad = placeSquad(t, i);
      farSleeper(t, i);
      return {
        id: 'ventana',
        what: 'Un soldado salta por la ventana, de dentro afuera.',
        state: t,
        orders: [{ at: 0.2, slot: 0, cmd: { type: 'move', unit: squad[0]!.id, to: { x: o.x, y: o.y } } }],
        connected: [0, 1],
        focus: { ...wn, level: 0 },
        check: (ev) => of(ev, 'moved').some((e) => e.path.some((p) => p.x === wn.x && p.y === wn.y)),
      };
    }
  }
  throw new Error('ventana: no window');
}

/** The door: a soldier opens it, and the dead are right behind. */
function door(): Shot {
  const s = base(CITY);
  for (const dr of tiles(s)) {
    if (kind(s, dr) !== 'door') continue;
    for (const d of DIRS) {
      const o = add(dr, d);
      const i = add(dr, d, -1);
      if (!(freeAt(s, o) && !indoor(s, o) && freeAt(s, i) && indoor(s, i) && freeAt(s, add(i, d, -1)))) continue;
      const t = cloneState(s);
      const squad = placeSquad(t, o, [dr]);
      enemies(t, 'zombie', [i, add(i, d, -1)], false);
      farSleeper(t, o);
      return {
        id: 'puerta',
        what: 'Un soldado abre la puerta y detrás hay dos zombis.',
        state: t,
        orders: [{ at: 0.2, slot: 0, cmd: { type: 'ability', unit: squad[0]!.id, ability: 'openDoor', tile: { x: dr.x, y: dr.y } } }],
        connected: [0, 1],
        focus: { ...dr, level: 0 },
        check: (ev) => of(ev, 'tileChanged').some((e) => e.pos.x === dr.x && e.pos.y === dr.y && e.kind === 'doorOpen'),
      };
    }
  }
  throw new Error('puerta: no door');
}

/** Searching: the room first, then the weapons box beside the soldier. */
function search(): Shot {
  const s = base(CITY);
  for (const p of tiles(s)) {
    if (!freeAt(s, p) || !indoor(s, p) || !roomAt(s, p)) continue;
    const box = DIRS.map((d) => add(p, d)).find((q) => freeAt(s, q) && indoor(s, q) && DIRS.filter((d) => freeAt(s, add(q, d))).length >= 2);
    if (!box) continue;
    const t = cloneState(s);
    t.tiles[idx(t, box)] = 'low';
    t.decor ??= new Array(t.width * t.height).fill(0);
    t.decor[idx(t, box)] = decorCode('lootWeapons');
    if (!roomAt(t, p)) continue;
    const squad = placeSquad(t, p, [box]);
    for (const u of squad.slice(1)) u.pos = { ...squad[0]!.pos };
    t.units = t.units.filter((u) => u.team !== 'xcom' || u === squad[0]);
    farSleeper(t, p);
    return {
      id: 'registrar',
      what: 'Un soldado registra la habitación y después abre la caja de armas que tiene al lado.',
      state: t,
      orders: [
        { at: 0.2, slot: 0, cmd: { type: 'ability', unit: squad[0]!.id, ability: 'searchRoom' } },
        { at: 1.6, slot: 0, cmd: { type: 'ability', unit: squad[0]!.id, ability: 'search', tile: { x: box.x, y: box.y } } },
      ],
      connected: [0, 1],
      focus: { ...p, level: 0 },
      check: (ev) => of(ev, 'roomSearched').length === 1 && of(ev, 'searched').some((e) => !!e.loot?.weapon),
    };
  }
  throw new Error('registrar: no room');
}

/** The Chillón and the Hinchado: a shot bursts the Hinchado, and in the dead's turn the Chillón screams. */
function screamer(): Shot {
  const s = base(CITY);
  for (const p of tiles(s)) {
    if (!freeAt(s, p) || indoor(s, p)) continue;
    const dir = DIRS.find((d) => [1, 2, 3, 4, 5, 6, 7, 8, 9].every((k) => freeAt(s, add(p, d, k)) && !indoor(s, add(p, d, k))));
    if (!dir) continue;
    const t = cloneState(s);
    const squad = placeSquad(t, p);
    const [bloater] = enemies(t, 'bloater', [add(p, dir, 6)], true, { hp: 1 });
    const [sectoid] = enemies(t, 'sectoid', [add(p, dir, 9)], true);
    if (!squad.some((u) => canSee(t, u, bloater!)) || !squad.some((u) => canSee(t, u, sectoid!))) continue;
    farSleeper(t, p);
    return {
      id: 'chillon',
      what: 'Un disparo revienta al Hinchado; en el turno de los muertos, el Chillón grita.',
      state: t,
      orders: [
        { at: 0.2, slot: 0, cmd: { type: 'ability', unit: squad[0]!.id, ability: 'shoot', target: bloater!.id } },
        { at: 2.2, slot: 0, cmd: { type: 'endTurn', ready: true } },
      ],
      connected: [0, 1],
      focus: { ...add(p, dir, 7), level: 0 },
      check: (ev) => of(ev, 'died').some((e) => e.unit === bloater!.id) && of(ev, 'psi').some((e) => e.unit === sectoid!.id),
    };
  }
  throw new Error('chillon: no street');
}

/** A walk: on `map`, one soldier crosses `steps` tiles of open ground, near a street light at night. */
function walk(id: string, what: string, map: MapSource, night: boolean, steps: number, nearLight = false): Shot {
  const s = base(map, { night });
  const lamps = s.lights?.filter((l) => l.turns === undefined).map((l) => l.tile) ?? [];
  for (const p of tiles(s)) {
    if (!freeAt(s, p) || indoor(s, p)) continue;
    if (nearLight && !lamps.some((l) => Math.hypot(l.x - p.x, l.y - p.y) <= 3)) continue;
    const dir = DIRS.find((d) => Array.from({ length: steps }, (_, k) => add(p, d, k + 1)).every((q) => freeAt(s, q) && !indoor(s, q)));
    if (!dir) continue;
    const t = cloneState(s);
    const squad = placeSquad(t, add(p, dir, -0));
    const to = add(p, dir, Math.min(steps, 7));
    farSleeper(t, p);
    return {
      id,
      what,
      state: t,
      orders: [{ at: 0.2, slot: 0, cmd: { type: 'move', unit: squad[0]!.id, to: { x: to.x, y: to.y } } }],
      connected: [0, 1],
      focus: { ...add(p, dir, Math.floor(steps / 2)), level: 0 },
      check: (ev) => of(ev, 'moved').length > 0,
    };
  }
  throw new Error(`${id}: no ground`);
}

/**
 * The evacuation (take 7): one group of six of the dead comes walking from the town's side in their turn
 * (both players end theirs), only walking: they start far enough from everyone to spend the turn on their
 * feet, and end two to four tiles from the nearest soldier. In the next turn a soldier walks into the zone,
 * one player asks to leave, the partner says yes, La Lata goes and one is left behind with them close.
 */
function evacuation(): Shot {
  const s0 = base({ kind: 'generated', biome: 'city', seed: 11 }, { kind: 'recovery', squad: 4 });
  const zone = s0.mission.evacZone.map((z) => ({ ...z, z: 0 }));
  if (!zone.length || !s0.mission.evacBus) throw new Error('evacuacion: no bus');
  const inZone = zone.filter((z) => freeAt(s0, z));
  const centre = { x: zone.reduce((a, z) => a + z.x, 0) / zone.length, y: zone.reduce((a, z) => a + z.y, 0) / zone.length };
  const from = (p: { x: number; y: number }, q: { x: number; y: number }) => Math.hypot(p.x - q.x, p.y - q.y);
  // Towards the town: the way from the zone to the middle of the map.
  const inward = { x: s0.width / 2 - centre.x, y: s0.height / 2 - centre.y };
  const townward = (p: Pos) => ((p.x - centre.x) * inward.x + (p.y - centre.y) * inward.y) / (Math.hypot(inward.x, inward.y) * from(p, centre) || 1);
  const ring = (s: GameState, lo: number, hi: number) =>
    all(s).filter((p) => freeAt(s, p) && !indoor(s, p) && !inEvacZone(s, p) && from(p, centre) >= lo && from(p, centre) <= hi);
  for (const left of ring(s0, 5, 7).filter((p) => townward(p) > 0.4)) {
    for (const lead of ring(s0, 11, 16).filter((p) => townward(p) > 0.5)) {
      const s = cloneState(s0);
      const squad = s.units.filter((u) => u.team === 'xcom');
      squad[0]!.pos = inZone[0]!;
      squad[0]!.owner = 0;
      squad[1]!.pos = inZone[1]!;
      squad[1]!.owner = 1;
      squad[2]!.pos = { x: -1, y: -1, z: 0 };
      squad[3]!.pos = left;
      squad[3]!.owner = 1;
      // Six round the lead, at least ten tiles from every soldier: a whole turn on their feet.
      const soldiers = [inZone[0]!, inZone[1]!, left];
      const group = all(s)
        .filter((p) => freeAt(s, p) && !indoor(s, p) && from(p, lead) <= 2.3 && soldiers.every((q) => from(p, q) >= 10))
        .slice(0, 6);
      if (group.length < 6) continue;
      const dead = enemies(s, 'zombie', group, true);
      farSleeper(s, centre as Pos);
      // Their turn, played once, with the seed the shot keeps.
      const after = cloneState(s);
      const rng = new Rng(1);
      const events: GameEvent[] = [];
      for (const slot of [0, 1] as Slot[]) {
        const r = executeCommand(after, { type: 'endTurn', ready: true }, { slot, connected: [0, 1], rng });
        if (r.ok) events.push(...r.events);
      }
      const ids = new Set(dead.map((u) => u.id));
      if (events.some((e) => (e.t === 'shot' || e.t === 'abilityUsed') && ids.has(e.unit))) continue;
      const living = after.units.filter((u) => u.team === 'xcom' && u.alive && u.pos.x >= 0);
      const near = after.units.filter((u) => ids.has(u.id)).map((u) => Math.min(...living.map((q) => from(u.pos, q.pos))));
      if (Math.min(...near) < 2 || Math.min(...near) > 4) continue;
      // The walker belongs to whoever holds the command now, and walks into the zone in one move.
      const holder = (after.command ?? 0) as Slot;
      squad[2]!.owner = holder;
      const walker = ring(after, 2, 5).find((p) => {
        const t = cloneState(after);
        const w = t.units.find((u) => u.id === squad[2]!.id)!;
        w.pos = p;
        w.owner = holder;
        return executeCommand(t, { type: 'move', unit: w.id, to: { x: inZone[2]!.x, y: inZone[2]!.y } }, { slot: holder, connected: [0, 1], rng: new Rng(1) }).ok;
      });
      if (!walker) continue;
      squad[2]!.pos = walker;
      return {
        id: 'evacuacion',
        what: 'Un grupo de seis muertos viene andando desde la ciudad (fin de turno de los dos) y se queda a 2–4 casillas; en el turno siguiente uno entra en la zona, el slot 0 pide evacuar, el slot 1 acepta, La Lata se va y uno se queda fuera con ellos cerca.',
        state: s,
        orders: [
          { at: 0.2, slot: 0, cmd: { type: 'endTurn', ready: true } },
          { at: 0.3, slot: 1, cmd: { type: 'endTurn', ready: true } },
          { at: 4.0, slot: holder, cmd: { type: 'move', unit: squad[2]!.id, to: { x: inZone[2]!.x, y: inZone[2]!.y } } },
          { at: 6.0, slot: 0, cmd: { type: 'evac', unit: squad[0]!.id } },
          { at: 7.5, slot: 1, cmd: { type: 'evacAnswer', accept: true } },
        ],
        connected: [0, 1],
        focus: { x: Math.round(centre.x), y: Math.round(centre.y), level: 0 },
        check: (ev) =>
          of(ev, 'evacDeparted').length === 1 &&
          of(ev, 'leftBehind').some((e) => e.unit === squad[3]!.id) &&
          !ev.some((e) => (e.t === 'shot' || e.t === 'abilityUsed') && ids.has(e.unit)),
      };
    }
  }
  throw new Error('evacuacion: no place where the dead only walk');
}

/** A dead (or other) unit of `template` standing on `p`, only to ask who sees that place. */
const probe = (p: Pos): Unit => ({ ...makeUnit('probe', 'zombie', '', null, p, null) }) as Unit;
const seenBy = (s: GameState, squad: Unit[], p: Pos) => squad.some((u) => canSee(s, u, probe(p)));

/**
 * The key art (docs/TRAILER.md): the squad of four on the edge of a flat roof, looking down a street where a
 * horde of 30–40 of the dead comes at them, and La Lata waiting by its zone, further off to one side. No
 * orders: Arte paints it at dusk with the soldiers aiming.
 */
function cover(): Shot {
  for (let seed = 1; seed <= 40; seed++) {
    const s = base({ kind: 'generated', biome: 'city', seed }, { kind: 'recovery', squad: 4 });
    const bus = s.mission.evacBus;
    if (!s.levels?.length || !bus) continue;
    const busAt = { x: bus.tiles.reduce((a, t) => a + t.x, 0) / bus.tiles.length, y: bus.tiles.reduce((a, t) => a + t.y, 0) / bus.tiles.length };
    for (let k = 1; k <= s.levels.length; k++) {
      // Roof floor: a slab with open sky above, a parapet (low) on the side of the street.
      const roofAt = (p: Pos) => kind(s, p) !== null && s.levels![k - 1]!.tiles[idx(s, p)] === 'floor' && (s.levels![k]?.tiles[idx(s, p)] ?? null) === null;
      for (const p of all(s)) {
        if (!roofAt(p)) continue;
        for (const d of DIRS) {
          const side = { x: -d.y, y: d.x, z: 0 };
          const edge = [0, 1, 2, 3].map((j) => add(p, side, j));
          if (!edge.every((q) => inside(s, q) && roofAt(q) && s.levels![k - 1]!.tiles[idx(s, add(q, d))] === 'low')) continue;
          // The street below, in front: open ground from 3 to 14 tiles out, five wide.
          const street = [];
          for (let out = 3; out <= 14; out++) for (let j = -1; j <= 4; j++) street.push(add(add(p, d, out), side, j));
          const free = street.filter((q) => freeAt(s, q) && !indoor(s, q));
          if (free.length < 34) continue;
          if (Math.hypot(busAt.x - p.x, busAt.y - p.y) > 26) continue;
          const t = cloneState(s);
          const squad = t.units.filter((u) => u.team === 'xcom');
          squad.forEach((u, j) => (u.pos = { x: edge[j]!.x, y: edge[j]!.y, z: k }));
          enemies(t, 'zombie', free.slice(0, 36), true);
          farSleeper(t, p);
          const front = add(p, d, 8);
          return {
            id: 'portada',
            what: `La escuadra en el borde de un tejado (planta ${k}), mirando a la calle; en ella, una horda de 36 que viene de frente; La Lata en su zona, a un lado.`,
            state: t,
            orders: [],
            connected: [0, 1],
            focus: { x: front.x, y: front.y, level: 0 },
            check: () => true,
          };
        }
      }
    }
  }
  throw new Error('portada: no roof over a street near the bus');
}

/** A capture's game (the store page's combat shots): squad of four on a free 2 × 2 by `near`, `n` of the dead in sight from `lo` to `hi` tiles. */
function scene(
  id: string,
  what: string,
  map: MapSource,
  opts: { night?: boolean; kind?: 'elimination' | 'recovery'; n: number; lo: number; hi: number; near?: (s: GameState, p: Pos) => boolean; anywhere?: boolean },
): Shot {
  const s = base(map, { night: opts.night, kind: opts.kind, squad: 4 });
  // `anywhere`: by the edges too (the evacuation zone is by a corner).
  for (const p of opts.anywhere ? all(s) : tiles(s)) {
    const block = [p, add(p, DIRS[1]!), add(p, DIRS[2]!), add(add(p, DIRS[1]!), DIRS[2]!)];
    if (!block.every((q) => freeAt(s, q) && !indoor(s, q))) continue;
    if (opts.near && !opts.near(s, p)) continue;
    const t = cloneState(s);
    const squad = t.units.filter((u) => u.team === 'xcom');
    squad.forEach((u, j) => (u.pos = block[j]!));
    const ring = all(t)
      .filter((q) => freeAt(t, q) && !indoor(t, q) && Math.hypot(q.x - p.x, q.y - p.y) >= opts.lo && Math.hypot(q.x - p.x, q.y - p.y) <= opts.hi && seenBy(t, squad, q))
      .sort((a, b) => Math.atan2(a.y - p.y, a.x - p.x) - Math.atan2(b.y - p.y, b.x - p.x));
    if (ring.length < opts.n) continue;
    // Gathered on one side, not all round: the nearest angular run of `n`.
    const pick = ring.slice(0, opts.n);
    enemies(t, 'zombie', pick, true);
    farSleeper(t, p);
    const mid = { x: Math.round((p.x + pick[Math.floor(pick.length / 2)]!.x) / 2), y: Math.round((p.y + pick[Math.floor(pick.length / 2)]!.y) / 2) };
    return { id, what, state: t, orders: [], connected: [0, 1], focus: { ...mid, level: 0 }, check: () => true };
  }
  throw new Error(`${id}: no place`);
}

/** As the server saves a campaign mission (persistence.ts), with the showcase campaign and Arte's seats. */
function saveOf(shot: Shot, tokens: [string, string]): unknown {
  const c = JSON.parse(JSON.stringify(campaign)) as CampaignState;
  const offer = c.offers.find((o) => !o.story && !o.final) ?? c.offers[0]!;
  c.activeMission = offer.id;
  const s = cloneState(shot.state);
  s.campaignMission = { id: offer.id, name: s.mapName };
  return {
    version: 6,
    seats: [
      { slot: 0, name: 'Ana', token: tokens[0] },
      { slot: 1, name: 'Arturo', token: tokens[1] },
    ],
    phase: 'mission',
    mode: 'campaign',
    state: s,
    campaign: c,
    resultApplied: false,
    rng: new Rng(1).state,
    missionKind: s.mission.kind,
    skirmishMap: 'city',
    skirmishVeterans: false,
  };
}

const lamps = (s: GameState) => s.lights?.filter((l) => l.turns === undefined).map((l) => l.tile) ?? [];
const captures: (() => Shot)[] = [
  () => scene('combate', 'Combate en la ciudad, de día: cuatro zombis a 6–9 casillas.', CITY, { n: 4, lo: 6, hi: 9 }),
  () => scene('horda', 'La horda: 24 zombis llegando por la calle.', CITY, { n: 24, lo: 6, hi: 15 }),
  () => scene('noche', 'De noche, junto a una farola: tres zombis a la vista.', CITY, { night: true, n: 3, lo: 6, hi: 9, near: (s, p) => lamps(s).some((l) => Math.hypot(l.x - p.x, l.y - p.y) <= 3) }),
  () => scene('area', 'El área de servicio: la escuadra en la explanada, cuatro zombis a la vista.', { kind: 'generated', biome: 'wilds', seed: 5, site: 'roadside' }, { n: 4, lo: 7, hi: 11, near: (s, p) => s.ground?.[idx(s, p)] === 3 }),
  () => scene('poligono', 'El polígono: la escuadra entre los contenedores, cuatro zombis a la vista.', { kind: 'generated', biome: 'city', seed: 4, site: 'industrial' }, { n: 4, lo: 7, hi: 11, near: (s, p) => s.ground?.[idx(s, p)] === 3 }),
  () => {
    const s = scene('evacuacion', 'La evacuación: la escuadra junto a La Lata y la horda llegando.', { kind: 'generated', biome: 'city', seed: 11 }, { kind: 'recovery', n: 16, lo: 5, hi: 12, anywhere: true, near: (t, p) => t.mission.evacZone.some((z) => Math.hypot(z.x - p.x, z.y - p.y) <= 2) });
    return s;
  },
];

// --------------------------------------------------------------------- run

const builders: (() => Shot)[] = [
  overwatch,
  machete,
  grenade,
  windowJump,
  door,
  search,
  screamer,
  () => walk('noche', 'De noche: un soldado avanza por una calle con farolas.', { kind: 'generated', biome: 'city', seed: 7 }, true, 8, true),
  () => walk('area', 'El área de servicio del bosque, de día: un soldado cruza la explanada.', { kind: 'generated', biome: 'wilds', seed: 5, site: 'roadside' }, false, 8),
  () => walk('poligono', 'El polígono industrial: un soldado avanza entre los contenedores.', { kind: 'generated', biome: 'city', seed: 4, site: 'industrial' }, false, 8),
  evacuation,
];

const out = join(folder, 'tomas');
mkdirSync(out, { recursive: true });
for (const build of builders) {
  let shot: Shot;
  try {
    shot = build();
  } catch (err) {
    console.log(`× ${(err as Error).message}`);
    continue;
  }
  let found: number | null = null;
  for (let seed = 1; seed <= 400 && found === null; seed++) {
    const run = play(shot, seed);
    if (run && shot.check(run.events, run.s)) found = seed;
  }
  if (found === null) {
    const first = play(shot, 1);
    const refused = (() => {
      const t = cloneState(shot.state);
      const rng = new Rng(1);
      for (const o of shot.orders) {
        const r = executeCommand(t, o.cmd, { slot: o.slot, connected: shot.connected, rng });
        if (!r.ok) return `${o.cmd.type}: ${r.error}`;
      }
      return '';
    })();
    console.log(`× ${shot.id}: no seed gives it (${first ? first.events.map((e) => e.t).join(' ') : refused})`);
    continue;
  }
  const { check: _check, ...file } = shot;
  writeFileSync(join(out, `${shot.id}.json`), JSON.stringify({ ...file, seed: found }));
  console.log(`✓ ${shot.id}: seed ${found}, focus ${shot.focus.x},${shot.focus.y}`);
}

// The key art's state, as a shot without orders.
try {
  const shot = cover();
  const { check: _c, ...file } = shot;
  writeFileSync(join(out, 'portada.json'), JSON.stringify({ ...file, seed: 1 }));
  console.log(`✓ portada: focus ${shot.focus.x},${shot.focus.y}`);
} catch (err) {
  console.log(`× ${(err as Error).message}`);
}

// The store page's combat captures: games Arte loads in its server (3330), seats Ana and Arturo.
const tokens: [string, string] = [process.argv[3] ?? '4a7c7852-2c60-4a11-b7f8-b297cda98340', process.argv[4] ?? '6ce6529a-76be-4bea-9bb9-b17edeba8079'];
const savesDir = join(folder, 'capturas');
mkdirSync(savesDir, { recursive: true });
for (const build of captures) {
  try {
    const shot = build();
    writeFileSync(join(savesDir, `${shot.id}.json`), JSON.stringify(saveOf(shot, tokens)));
    console.log(`✓ capturas/${shot.id}: focus ${shot.focus.x},${shot.focus.y}`);
  } catch (err) {
    console.log(`× ${(err as Error).message}`);
  }
}
