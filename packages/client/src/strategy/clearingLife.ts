import { bastionFootprint, cellsOf, pieceAt, treeAt, type BaseState, type TemplateId } from '@bastion/engine';
import * as THREE from 'three';
import { clipSpeed } from '../game/gaits';
import { buildUnit, IDLE_POSE, isFigure, type UnitRig } from '../game/models';
import { whenCampClips } from '../game/syntyAnims';
import { CAMP_ITEMS, type CampLoop, type CampMove, type Gesture, type PropId, type Stance } from '../game/syntyManifest';
import type { LineupEntry } from './clearingArt';

// The clearing with life (Arturo, 22/10 #1): the soldiers at the base out about it while the Asentamiento is open,
// at their ease, their guns slung. Some stand about (arms folded, hands on hips, a hand at the chin, or the plain idle
// with its looks round); two stand talking face to face, now one and now the other saying something with a gesture
// while the other nods; one walks from one place to another between the buildings and stays a while at each. They
// keep off the pieces, the pines, the Bastion, La Lata and whatever else the clearing says (ClearingArtView), and walk
// off what is being placed (keepClear). A handful at most: they are the battle's figures (LIFE_MAX), and the camera
// of the Asentamiento sees them from above.
// And the camp (22/10, Gameplay): round the fire, sat on its camp chairs or on the ground, one knelt warming the
// hands; one leant on La Lata; the one walking about goes and sits on a free chair or leans on La Lata a while; those
// standing about now and then stretch, yawn, look at the time or at the sky (the camp's clips: CAMP_LOOPS,
// CAMP_VARIATIONS). Sitting down and getting up, leaning and coming off, are clips of their own (CAMP_MOVES), so
// nobody jumps from one to the other.

/** How many at most, and how far from the clearing's middle they keep (tiles). */
const LIFE_MAX = 6;
const LIFE_REACH = 9;
/** A walk's pace (tiles a second), and how long one stays at either end of its walk (seconds, from and to); sat, longer. */
const WALK = 1.15;
const STAY: [number, number] = [4, 9];
const SAT: [number, number] = [9, 16];
/** How far apart two talking stand (tiles), and the seconds between one's gestures and the other's. */
const TALK_APART = 1.05;
const TALK_EVERY: [number, number] = [2.2, 4.5];
/** How fast one turns to where it goes (radians a second), and how near it is turned enough to sit down. */
const TURN = 5;
const TURNED = 0.12;
/** Seconds one sat on the ground takes to get up before walking off (no clip for it: eased). */
const RISE = 0.8;
/** What the ones standing about stand in, and what the talkers do. */
const STANDS: (Stance | null)[] = ['armsFolded', 'handsOnHips', null, 'thoughtful', 'armsFolded', null];
const SAYS: Gesture[] = ['point', 'shrug', 'calm', 'chin', 'point'];
/**
 * Round a fire: how far a camp chair's sitter sits ahead of it (its hips over the seat, tiles), and how far ahead of
 * that it stands to sit down and gets up to (Mixamo's sitting down and getting up move the hips that far back and
 * forth: measured on our bodies, 22/10); how far out on the ground the others sit and the one knelt warming the
 * hands kneels, how far apart they keep, and the sitters' ways (on a chair, two near each other talk; on the
 * ground; knelt).
 */
const SEAT_AHEAD = 0.2;
const SIT_SHIFT = 0.32;
const FIRE_RING = 1.05;
const FIRE_KNEEL = 0.8;
const FIRE_APART = 0.9;
/** How far round each fire the rest keep off (those round it sit within it): nobody walks through them. */
const FIRE_CIRCLE = 1.7;
/** How near the Camposanto's tiles a fire's circle may come (tiles): nobody sits warming the hands among the graves (Gameplay, 22/10). */
const GRAVES_OFF = 0.6;
/** How far from a fire's middle one stands at least to sit down by it (its stones and flames). */
const FIRE_CLEAR = 0.6;
const ON_CHAIR: CampLoop[] = ['sitChair', 'sitChairStill', 'sitBreathe', 'sitCrossed'];
const ON_GROUND: CampLoop[] = ['sitFloor', 'sitReclined', 'sitFloor'];
/**
 * Those standing about with no way of standing of their own drink from a metal cup or eat a ration bar now and then
 * (CAMP_ITEMS): the seconds between, how long they keep at it, and between one sip or bite and the next.
 */
const HABIT_EVERY: [number, number] = [10, 24];
const HABIT_FOR: [number, number] = [8, 14];
const HABIT_SIP: [number, number] = [2.5, 5];
const HABIT_MOVES = { drink: { into: 'drinkIn', out: 'drinkOut', once: 'sip' }, eat: { into: 'eatIn', out: 'eatOut', once: 'bite' } } as const satisfies Record<keyof typeof CAMP_ITEMS, Record<string, CampMove>>;
/** Leant on La Lata: a shoulder to it (the back would put the slung gun through it), the left or the right. */
const LEANS: Record<1 | -1, { loop: CampLoop; into: CampMove; out: CampMove }> = {
  1: { loop: 'leanLeft', into: 'leanLeftIn', out: 'leanLeftOut' },
  [-1]: { loop: 'leanRight', into: 'leanRightIn', out: 'leanRightOut' },
};

type Vec = { x: number; z: number };

/**
 * The clearing's camp (ClearingArtView: basePieces.ts's fires and camp chairs, La Lata's long sides): its fires, the
 * chairs by them facing them, what else stands about them (each prop's box, its inverse matrix, for none to sit in
 * it), and the places along a wall one can lean at (facing along it, the wall `side` 1 to the left, −1 the right).
 */
export interface Camp {
  fires: Vec[];
  seats: (Vec & { yaw: number })[];
  things: { inverse: THREE.Matrix4; min: THREE.Vector3; max: THREE.Vector3 }[];
  walls: (Vec & { yaw: number; side: 1 | -1 })[];
}

/**
 * A place at the camp: what one does there, where it stays and facing which way, where it stands to get into it and
 * gets up to, the clips into and out of it (none: eased), what it holds there and the clip it plays now and then.
 */
interface Spot {
  loop: CampLoop;
  at: Vec;
  yaw: number;
  stand: Vec;
  into: CampMove | null;
  out: CampMove | null;
  item?: PropId;
  once?: CampMove;
}

interface Idler {
  rig: UnitRig;
  /** Its entry as given (to keep the same figure when the list comes again). */
  key: string;
  kind: 'stand' | 'talk' | 'walk' | 'fire' | 'lean';
  /** Where it is, which way it faces, and where it goes (null: it stays). */
  at: Vec;
  yaw: number;
  goal: Vec | null;
  /** Where it faces once there (a talker, its partner; null, as it came). */
  face: Vec | null;
  stance: Stance | null;
  phase: number;
  /** Its place at the camp (sat, knelt, leant: one round the fire, the one leant, the walker's end there). */
  spot: Spot | null;
  /** In its place's loop now; on its way into it (turning to sit); the clip into or out of it playing. */
  settled: boolean;
  toSit: boolean;
  act: { move: CampMove; into: boolean; once?: boolean } | null;
  /** Standing about, what it does now and then (a cup, a ration bar), the seconds until then, and until the next sip or bite. */
  habit: keyof typeof CAMP_ITEMS | null;
  habitWait: number;
  sipWait: number;
  /** Seconds left getting up without a clip, and what it does once up. */
  rise: number;
  then: (() => void) | null;
  /** A walker's two ends (the second its place at the camp, if it has one), the one it goes to now, and how long it stays before it sets off again. */
  ends: [Vec, Vec] | null;
  end: number;
  stay: number;
  /** A talker's partner. */
  partner: Idler | null;
}

/** A small seeded random: the same people in the same places each time the clearing is drawn again. */
function hashOf(text: string): number {
  let h = 2166136261;
  for (let i = 0; i < text.length; i++) h = Math.imul(h ^ text.charCodeAt(i), 16777619);
  return h >>> 0;
}
const random = (seed: number) => () => {
  seed = (Math.imul(seed ^ (seed >>> 15), 2246822519) + 0x9e3779b9) >>> 0;
  return seed / 4294967296;
};

export class ClearingLife {
  readonly group = new THREE.Group();
  private idlers: Idler[] = [];
  private key = '';
  private base: BaseState | null = null;
  /** The tiles kept clear (what is being placed), as tile indices. */
  private clear = new Set<number>();
  /** The tiles the clearing says are taken (La Lata, the squad's row, the Armería's corner, the soldier in Soldados). */
  private taken: (x: number, z: number) => boolean = () => false;
  /** The Bastion's footprint grown from the caravan (tile indices), and the Camposanto's. */
  private foot = new Set<number>();
  private graves: { x: number; y: number }[] = [];
  private camp: Camp = { fires: [], seats: [], things: [], walls: [] };
  private campKey = '';

  /** The camp as the clearing now draws it (ClearingArtView); the soldiers are placed again if its fires, chairs or walls moved. */
  setCamp(camp: Camp): void {
    const round = (p: Vec) => `${p.x.toFixed(1)},${p.z.toFixed(1)}`;
    const campKey = [...camp.fires, ...camp.seats, ...camp.walls].map(round).join(' ') + `|${camp.things.length}`;
    this.camp = camp;
    if (campKey === this.campKey) return;
    this.campKey = campKey;
    const key = this.key;
    this.key = '';
    if (key) this.set(JSON.parse(key) as LineupEntry[]);
  }

  /** The clearing as it stands, and what else on it is taken (ClearingArtView: La Lata, the corners of its shots). */
  setBase(base: BaseState, taken: (x: number, z: number) => boolean): void {
    this.base = base;
    this.taken = taken;
    this.foot = new Set(bastionFootprint(base).map((c) => c.y * base.width + c.x));
    this.graves = base.pieces.filter((p) => p.piece === 'memorial').flatMap((p) => cellsOf(p));
    // Those now standing on something go elsewhere.
    for (const d of this.idlers) if (!this.stays(d, 0)) this.moveOff(d);
  }

  /** The soldiers out about the clearing (an empty list clears them); the same list again does nothing. */
  set(entries: LineupEntry[]): void {
    const list = entries.slice(0, LIFE_MAX);
    const key = JSON.stringify(list);
    if (key === this.key) return;
    this.key = key;
    const old = new Map(this.idlers.map((d) => [d.key, d.rig]));
    this.idlers = [];
    this.group.clear();
    if (!this.base || !list.length) return;
    // (The camp's clips, apart from the battle's: in by the time they have sat down.)
    for (const build of ['masc', 'femn'] as const) void whenCampClips(build).catch(() => undefined);
    const r = random(hashOf(key));
    const kinds = this.kindsFor(list.length);
    list.forEach((entry, i) => {
      const who = JSON.stringify(entry);
      const rig = old.get(who) ?? buildUnit(entry.template as TemplateId, entry.color, entry.appearance, entry.gear, entry.seed);
      if (isFigure(rig)) {
        rig.sling?.(true);
        rig.setGait?.('walk', clipSpeed(entry.template as TemplateId, { motion: 'walk', seconds: 1 / WALK }));
        rig.campMove?.(null);
      }
      rig.root.traverse((o) => {
        if (o instanceof THREE.Mesh) o.castShadow = true;
      });
      const kind = kinds[i]!;
      const stance = kind === 'stand' ? STANDS[i % STANDS.length]! : kind === 'talk' ? (i === 0 ? null : 'armsFolded') : null;
      this.idlers.push({ rig, key: who, kind, at: { x: 0, z: 0 }, yaw: r() * Math.PI * 2, goal: null, face: null, stance, phase: r() * 10, spot: null, settled: false, toSit: false, act: null, habit: null, habitWait: 0, sipWait: 0, rise: 0, then: null, ends: null, end: 0, stay: 0, partner: null });
      this.group.add(rig.root);
    });
    const talkers = this.idlers.filter((d) => d.kind === 'talk');
    if (talkers.length === 2) {
      talkers[0]!.partner = talkers[1]!;
      talkers[1]!.partner = talkers[0]!;
    }
    this.seatRound(r);
    // Those standing about as they are drink or eat now and then, by turns.
    let habits = 0;
    for (const d of this.idlers) {
      if (d.kind !== 'stand' || d.stance) continue;
      d.habit = habits++ % 2 ? 'eat' : 'drink';
      d.habitWait = between(r, HABIT_EVERY) * 0.5;
    }
    for (const d of this.idlers) {
      // Those already in their places are sat there from the first frame, not sitting down.
      if (d.settled && isFigure(d.rig)) d.rig.camp?.(d.spot!.loop, true, true);
      else this.place(d, r, true);
    }
  }

  /**
   * Who does what, for `n` out: round the fire (one; two from three on; three from five on), one leant on La Lata from
   * two on, one walking from three on, a pair talking from six, the rest standing about. With no fire nor wall, as
   * before: a pair talking from three on, one walking from two on.
   */
  private kindsFor(n: number): Idler['kind'][] {
    const camp = this.camp;
    if (!this.circleFire() && !camp.walls.length) return Array.from({ length: n }, (_, i) => (n >= 3 && i < 2 ? 'talk' : n >= 2 && i === (n >= 3 ? 2 : 1) ? 'walk' : 'stand'));
    const out: Idler['kind'][] = [];
    const add = (kind: Idler['kind'], count: number) => {
      for (let k = 0; k < count && out.length < n; k++) out.push(kind);
    };
    add('fire', this.circleFire() ? (n >= 5 ? 3 : n >= 3 ? 2 : 1) : 0);
    add('lean', camp.walls.length && n >= 2 ? 1 : 0);
    add('walk', n >= 3 ? 1 : 0);
    if (n - out.length >= 2 && n >= 6) add('talk', 2);
    add('stand', n);
    return out;
  }

  /**
   * Those round the fire (the first one's): on its chairs first, then on the ground about it facing it, one of them
   * knelt nearest; and the one leant on La Lata. Each already there, in its place. Who can't be put there stands about.
   */
  private seatRound(r: () => number): void {
    const camp = this.camp;
    const fire = this.circleFire();
    const sitters = this.idlers.filter((d) => d.kind === 'fire');
    // (The chairs themselves too: one on the ground behind a chair was in the back of who sat on it.)
    const taken: Vec[] = camp.seats.map((s) => ({ x: s.x, z: s.z }));
    const settle = (d: Idler, spot: Spot) => {
      d.spot = spot;
      d.at = { ...spot.at };
      d.yaw = spot.yaw;
      d.stance = null;
      d.settled = true;
      taken.push(spot.at);
    };
    let k = 0;
    if (fire) {
      for (const c of camp.seats.filter((s) => Math.hypot(s.x - fire.x, s.z - fire.z) < 2.5)) {
        const d = sitters[k];
        if (!d) break;
        // (Two on chairs: the second talks to the first.)
        settle(d, this.chairSpot(c, k === 1 ? 'sitTalk' : ON_CHAIR[Math.floor(r() * ON_CHAIR.length)]!));
        k++;
      }
      // The rest on the ground round it, the last left over knelt nearer, warming the hands.
      const start = r() * Math.PI * 2;
      for (; k < sitters.length; k++) {
        const d = sitters[k]!;
        const kneel = k === sitters.length - 1 && k > 0;
        const ring = kneel ? FIRE_KNEEL : FIRE_RING;
        for (let step = 0; step < 12; step++) {
          const a = start + step * (Math.PI / 6) + k * 0.9;
          const at = { x: fire.x + Math.sin(a) * ring, z: fire.z + Math.cos(a) * ring };
          if (!this.groundFree(at, taken)) continue;
          settle(d, { loop: kneel ? 'warmHands' : ON_GROUND[k % ON_GROUND.length]!, at, yaw: Math.atan2(fire.x - at.x, fire.z - at.z), stand: at, into: kneel ? 'kneelDown' : null, out: kneel ? 'kneelUp' : null });
          break;
        }
      }
    }
    for (const d of this.idlers.filter((o) => o.kind === 'lean')) {
      const wall = camp.walls.find((w) => this.groundFree(w, taken));
      if (wall) settle(d, this.wallSpot(wall));
    }
    for (const d of this.idlers) if ((d.kind === 'fire' || d.kind === 'lean') && !d.settled) d.kind = 'stand';
  }

  /** The fire to sit round: the first whose circle keeps off the Camposanto (the yard's, unless it was built beside it). */
  private circleFire(): Vec | null {
    const reach = FIRE_RING + 0.5 + GRAVES_OFF;
    return this.camp.fires.find((f) => !this.graves.some((c) => nearTile(f, c, reach))) ?? null;
  }

  /**
   * Sitting on camp chair `c` in `loop`: its hips over the seat, standing a step ahead of it to sit down and getting up
   * there; facing the fire it is by (the chair is a stool: any way round; the Barracón's is not turned to its fire), or
   * a little aside where that would have it stand in the fire to sit down.
   */
  private chairSpot(c: Vec & { yaw: number }, loop: CampLoop): Spot {
    const fire = this.camp.fires.find((f) => Math.hypot(f.x - c.x, f.z - c.z) < 2.5);
    const spotAt = (yaw: number): Spot => {
      const ahead = { x: Math.sin(yaw), z: Math.cos(yaw) };
      const at = { x: c.x + ahead.x * SEAT_AHEAD, z: c.z + ahead.z * SEAT_AHEAD };
      return { loop, at, yaw, stand: { x: at.x + ahead.x * SIT_SHIFT, z: at.z + ahead.z * SIT_SHIFT }, into: 'sitDown', out: 'standUp' };
    };
    if (!fire) return spotAt(c.yaw);
    const toward = Math.atan2(fire.x - c.x, fire.z - c.z);
    const clear = (s: Spot) => Math.hypot(s.stand.x - fire.x, s.stand.z - fire.z) > FIRE_CLEAR;
    for (const aside of [0, 0.6, -0.6, 1.1, -1.1, Math.PI / 2, -Math.PI / 2]) {
      const s = spotAt(toward + aside);
      if (clear(s)) return s;
    }
    return spotAt(toward + Math.PI / 2);
  }

  /** Leaning at `w` (a shoulder to the wall), into it and off it with its clips. */
  private wallSpot(w: Vec & { yaw: number; side: 1 | -1 }): Spot {
    const lean = LEANS[w.side];
    return { loop: lean.loop, at: { x: w.x, z: w.z }, yaw: w.yaw, stand: { x: w.x, z: w.z }, into: lean.into, out: lean.out };
  }

  /** A place at the camp nobody has: a chair by a fire, else a place along La Lata. */
  private freeSpot(r: () => number): Spot | null {
    const used = this.idlers.flatMap((d) => (d.spot ? [d.spot.at] : []));
    const free = (p: Vec) => !used.some((u) => Math.hypot(u.x - p.x, u.z - p.z) < FIRE_APART);
    // (Not one by the Camposanto's graves.)
    const chairs = this.camp.seats.filter((c) => free(this.chairSpot(c, 'sitChair').at) && !this.graves.some((g) => nearTile(c, g, GRAVES_OFF + 0.8)));
    if (chairs.length) return this.chairSpot(chairs[Math.floor(r() * chairs.length)]!, ON_CHAIR[Math.floor(r() * ON_CHAIR.length)]!);
    const walls = this.camp.walls.filter((w) => free(w) && this.groundFree(w, []));
    return walls.length ? this.wallSpot(walls[Math.floor(r() * walls.length)]!) : null;
  }

  /** Whether one can sit at `at` by the fire: nothing of the camp there (a prop's box with a margin), no pine, none of `taken` near. */
  private groundFree(at: Vec, taken: Vec[]): boolean {
    const base = this.base;
    if (!base) return false;
    const x = Math.floor(at.x);
    const z = Math.floor(at.z);
    if (x < 1 || z < 1 || x >= base.width - 1 || z >= base.height - 1) return false;
    if (treeAt(base, x, z) || this.clear.has(z * base.width + x)) return false;
    if (this.graves.some((c) => nearTile(at, c, GRAVES_OFF))) return false;
    if (taken.some((t) => Math.hypot(t.x - at.x, t.z - at.z) < FIRE_APART)) return false;
    const p = new THREE.Vector3();
    for (const t of this.camp.things) {
      p.set(at.x, 0.3, at.z).applyMatrix4(t.inverse);
      // (About a body's half width round it, in the prop's own measure.)
      const m = 0.28 * t.inverse.getMaxScaleOnAxis();
      if (p.x > t.min.x - m && p.x < t.max.x + m && p.z > t.min.z - m && p.z < t.max.z + m && p.y > t.min.y - m && p.y < t.max.y + m) return false;
    }
    return true;
  }

  /** What is being placed (its tiles; null nothing): whoever is on it, or about to walk through it, goes elsewhere. */
  keepClear(cells: { x: number; y: number }[] | null): void {
    const w = this.base?.width ?? 0;
    const next = new Set((cells ?? []).map((c) => c.y * w + c.x));
    if (next.size === this.clear.size && [...next].every((i) => this.clear.has(i))) return;
    this.clear = next;
    if (!next.size) return;
    for (const d of this.idlers) if (!this.stays(d, 1)) this.moveOff(d);
  }

  /** Whether `d` can stay where it is and go where it is going (`margin` tiles round free): one at the camp, what is placed off it. */
  private stays(d: Idler, margin: number): boolean {
    const w = this.base?.width ?? 0;
    const unplaced = (p: Vec) => !this.clear.has(Math.floor(p.z) * w + Math.floor(p.x));
    if (d.settled || d.act || d.toSit) return unplaced(d.at) && (!d.spot || unplaced(d.spot.at));
    // A walker's end at the camp is in the yard or by La Lata (off the ground the rest keep to): only what is placed counts there.
    const ok = (p: Vec) => (d.spot && p === d.ends?.[1] ? unplaced(p) : this.ok(p, margin));
    return ok(d.at) && (!d.goal || ok(d.goal)) && (!d.ends || d.ends.every((e) => ok(e) && this.way(d.at, e, d.spot)));
  }

  /** Every frame: they walk, turn, sit down and get up, stay a while and talk. */
  tick(dt: number, time: number): void {
    for (const d of this.idlers) {
      const rig = d.rig;
      let moving = 0;
      if (d.act) {
        // Into its place or out of it: the clip to its end, standing where it stands for it.
        const done = isFigure(rig) ? (rig.campMove?.(d.act.move) ?? 1) : 1;
        if (done >= 1) this.endAct(d);
      } else if (d.rise > 0) {
        d.rise -= dt;
        if (d.rise <= 0) this.risen(d);
      } else if (d.goal) {
        const dx = d.goal.x - d.at.x;
        const dz = d.goal.z - d.at.z;
        const far = Math.hypot(dx, dz);
        const step = WALK * dt;
        if (far <= step) {
          d.at = d.goal;
          d.goal = null;
          d.stay = this.stayFor(d);
          // At its place at the camp: it turns to sit down or lean there.
          if (d.kind === 'walk' && d.spot && d.end === 1) d.toSit = true;
        } else {
          d.at = { x: d.at.x + (dx / far) * step, z: d.at.z + (dz / far) * step };
          d.yaw = turnToward(d.yaw, Math.atan2(dx, dz), TURN * dt);
          moving = 1;
        }
      } else if (d.toSit && d.spot) {
        d.yaw = turnToward(d.yaw, d.spot.yaw, TURN * dt * 0.6);
        if (Math.abs(Math.atan2(Math.sin(d.spot.yaw - d.yaw), Math.cos(d.spot.yaw - d.yaw))) < TURNED) {
          d.toSit = false;
          this.startAct(d, true);
        }
      } else {
        if (d.face) d.yaw = turnToward(d.yaw, Math.atan2(d.face.x - d.at.x, d.face.z - d.at.z), TURN * dt * 0.6);
        // A walker stays a while (sat or leant, if it went to the camp), then gets up and sets off for its other end.
        if (d.kind === 'walk' && d.ends) {
          d.stay -= dt;
          if (d.stay <= 0) {
            const go = () => {
              d.end = 1 - d.end;
              d.goal = d.ends![d.end]!;
            };
            if (d.settled) this.getUp(d, go);
            else go();
          }
        }
        // Standing about: now and then it gets out its cup or its ration bar, has a sip or a bite every so often, and puts it away.
        if (d.kind === 'stand' && d.habit) this.habitTick(d, dt);
        // Two talking: one says something with a gesture, the other nods; then the other way round.
        if (d.kind === 'talk' && d.partner && !d.partner.goal && isFigure(rig)) {
          d.stay -= dt;
          if (d.stay <= 0 && d.partner.stay > 0.6) {
            const says = SAYS[Math.floor((time * 7 + d.phase) % SAYS.length)]!;
            if (rig.gesture?.(says)) {
              const other = d.partner.rig;
              if (isFigure(other)) other.gesture?.('nod');
              d.stay = TALK_EVERY[0] + Math.random() * (TALK_EVERY[1] - TALK_EVERY[0]);
              d.partner.stay = Math.max(d.partner.stay, 1.2);
            }
          }
        }
      }
      rig.root.position.set(d.at.x, 0, d.at.z);
      if (isFigure(rig)) {
        rig.setStance?.(moving || d.spot ? null : d.stance);
        // At the camp: sat, knelt or leant once in its place; standing about, the camp's things now and then (not two talking).
        rig.camp?.(moving || !d.settled ? null : d.spot!.loop, d.kind !== 'talk');
        rig.holdItem?.(d.spot?.item && (d.settled || d.act) ? d.spot.item : null, d.spot?.loop === 'drink');
        rig.applyPose({ ...IDLE_POSE, yaw: d.yaw, phase: d.phase, run: moving }, time);
      } else rig.root.rotation.y = d.yaw;
    }
  }

  /** A habit's turn (drinking, eating): out with it when its wait is over, a sip or a bite now and then, away again after a while. */
  private habitTick(d: Idler, dt: number): void {
    const habit = d.habit!;
    if (!d.settled) {
      d.habitWait -= dt;
      if (d.habitWait > 0 || d.goal || d.spot) return;
      const moves = HABIT_MOVES[habit];
      d.spot = { loop: habit, at: { ...d.at }, yaw: d.yaw, stand: { ...d.at }, into: moves.into, out: moves.out, item: CAMP_ITEMS[habit], once: moves.once };
      d.stay = between(Math.random, HABIT_FOR);
      d.sipWait = between(Math.random, HABIT_SIP);
      this.startAct(d, true);
      return;
    }
    if (!d.spot?.item) return;
    d.stay -= dt;
    d.sipWait -= dt;
    if (d.stay <= 0) {
      this.getUp(d, () => {
        d.spot = null;
        d.habitWait = between(Math.random, HABIT_EVERY);
      });
    } else if (d.sipWait <= 0 && d.spot.once && isFigure(d.rig)) {
      d.act = { move: d.spot.once, into: true, once: true };
      d.sipWait = between(Math.random, HABIT_SIP);
    }
  }

  /** Into its place (`into`) or out of it: its clip if it has one, else eased (the loop eases in, or it rises a moment). */
  private startAct(d: Idler, into: boolean): void {
    const spot = d.spot!;
    const move = into ? spot.into : spot.out;
    if (move && isFigure(d.rig) && d.rig.campMove) {
      d.act = { move, into };
      return;
    }
    if (into) {
      d.at = { ...spot.at };
      d.yaw = spot.yaw;
      d.settled = true;
    } else {
      d.settled = false;
      d.rise = RISE;
    }
  }

  /** Its clip into its place or out of it played: there, in the loop at once (where the clip left the body), or up where it gets up to. */
  private endAct(d: Idler): void {
    const rig = d.rig;
    const spot = d.spot!;
    const { into, once } = d.act!;
    d.act = null;
    // A sip or a bite over what it holds: back to holding it.
    if (once) {
      if (isFigure(rig)) rig.campMove?.(null);
      return;
    }
    if (isFigure(rig)) {
      rig.campMove?.(null);
      rig.camp?.(into ? spot.loop : null, true, true);
    }
    if (into) {
      d.at = { ...spot.at };
      d.settled = true;
    } else {
      d.at = { ...spot.stand };
      d.settled = false;
      this.risen(d);
    }
  }

  /** Up from its place: what it was to do then. */
  private risen(d: Idler): void {
    const then = d.then;
    d.then = null;
    then?.();
  }

  /** Gets up from its place (its clip, or eased), then `then`. */
  private getUp(d: Idler, then: () => void): void {
    d.then = then;
    this.startAct(d, false);
  }

  /** Leaves the clearing: nobody about. */
  clearAll(): void {
    this.set([]);
  }

  private stayFor(d: Idler): number {
    if (d.kind === 'walk') return d.spot && d.end === 1 ? SAT[0] + Math.random() * (SAT[1] - SAT[0]) : STAY[0] + Math.random() * (STAY[1] - STAY[0]);
    return d.kind === 'talk' ? 1 + Math.random() * 2 : 0;
  }

  /** Where `d` goes: a free place (two side by side for a pair, an end at either side of a walk); `now`, already there. */
  private place(d: Idler, r: () => number, now: boolean): void {
    if (d.kind === 'talk' && d.partner) {
      const lead = this.idlers.indexOf(d) < this.idlers.indexOf(d.partner);
      if (!lead) return;
      const spot = this.spot(r, (p) => {
        const a = r() * Math.PI * 2;
        const q = { x: p.x + Math.sin(a) * TALK_APART, z: p.z + Math.cos(a) * TALK_APART };
        return this.ok(q) ? q : null;
      });
      if (!spot) return;
      const [p, q] = spot;
      for (const [who, there, other] of [[d, p, q], [d.partner, q, p]] as const) {
        who.face = other;
        if (now) {
          who.at = there;
          who.yaw = Math.atan2(other.x - there.x, other.z - there.z);
        } else who.goal = there;
        who.stay = this.stayFor(who);
      }
      return;
    }
    if (d.kind === 'walk') {
      // A free place at the camp (a chair by the fire, La Lata) and another a good few tiles off, the way free between.
      const camp = this.freeSpot(r);
      const toCamp = camp && this.spot(r, (p) => (Math.hypot(camp.stand.x - p.x, camp.stand.z - p.z) > 4 && this.way(p, camp.stand, camp) ? camp.stand : null));
      const spot =
        toCamp ??
        this.spot(r, (p) => {
          for (let k = 0; k < 12; k++) {
            const q = this.free(r);
            if (q && Math.hypot(q.x - p.x, q.z - p.z) > 4 && this.way(p, q)) return q;
          }
          return null;
        });
      if (!spot) return;
      d.spot = camp && spot[1] === camp.stand ? camp : null;
      d.ends = spot;
      d.end = 0;
      if (now) d.at = spot[0];
      else d.goal = spot[0];
      d.stay = r() * STAY[1];
      return;
    }
    const spot = this.free(r);
    if (!spot) return;
    if (now) d.at = spot;
    else d.goal = spot;
  }

  /** A free place and another found from it (`then`), tried a few times. */
  private spot(r: () => number, then: (p: Vec) => Vec | null): [Vec, Vec] | null {
    for (let k = 0; k < 24; k++) {
      const p = this.free(r);
      const q = p && then(p);
      if (p && q) return [p, q];
    }
    return null;
  }

  /** A free place about the clearing's middle, away from the others. */
  private free(r: () => number): Vec | null {
    const base = this.base;
    if (!base) return null;
    const cx = base.width / 2;
    const cz = base.height / 2;
    for (let k = 0; k < 40; k++) {
      const a = r() * Math.PI * 2;
      const d = Math.sqrt(r()) * LIFE_REACH;
      const p = { x: Math.floor(cx + Math.sin(a) * d) + 0.5 + (r() - 0.5) * 0.4, z: Math.floor(cz + Math.cos(a) * d) + 0.5 + (r() - 0.5) * 0.4 };
      if (!this.ok(p, 1)) continue;
      if (this.idlers.some((o) => Math.hypot(o.at.x - p.x, o.at.z - p.z) < 1.6 || (o.goal && Math.hypot(o.goal.x - p.x, o.goal.z - p.z) < 1.6))) continue;
      return p;
    }
    return null;
  }

  /** Whether one can stand at `p` (`margin` tiles round it free too: not against a wall or a pine), off the fires' circles. */
  private ok(p: Vec, margin = 0): boolean {
    const base = this.base;
    if (!base) return false;
    if (this.camp.fires.some((f) => Math.hypot(p.x - f.x, p.z - f.z) < FIRE_CIRCLE)) return false;
    const foot = this.foot;
    for (let dz = -margin; dz <= margin; dz++) {
      for (let dx = -margin; dx <= margin; dx++) {
        // (A whole tile round: the caravan and the tents stand out past their tiles.)
        const x = Math.floor(p.x + dx);
        const z = Math.floor(p.z + dz);
        if (x < 1 || z < 1 || x >= base.width - 1 || z >= base.height - 1) return false;
        const i = z * base.width + x;
        if (treeAt(base, x, z) || pieceAt(base, x, z) || foot.has(i) || this.clear.has(i)) return false;
        if (this.taken(x + 0.5, z + 0.5)) return false;
      }
    }
    return true;
  }

  /**
   * Whether the way from `a` to `b` is free all along; to a place at the camp (`to`), the last of it (in the yard, by
   * La Lata) clear of the camp's things instead.
   */
  private way(a: Vec, b: Vec, to: Spot | null = null): boolean {
    const n = Math.ceil(Math.hypot(b.x - a.x, b.z - a.z) * 2);
    for (let k = 1; k < n; k++) {
      const p = { x: a.x + ((b.x - a.x) * k) / n, z: a.z + ((b.z - a.z) * k) / n };
      if (this.ok(p)) continue;
      if (to && Math.hypot(p.x - b.x, p.z - b.z) < 3 && this.groundFree(p, this.idlers.flatMap((d) => (d.settled ? [d.at] : [])))) continue;
      return false;
    }
    return true;
  }

  /** Off what is being placed, or what now stands where it was: somewhere else, walking there (getting up first). */
  private moveOff(d: Idler): void {
    const r = random(hashOf(d.key) ^ Math.floor(performance.now()));
    if (d.settled || d.act) {
      if (d.act && !d.act.into) return;
      const go = () => {
        if (d.kind === 'fire' || d.kind === 'lean') d.kind = 'stand';
        d.spot = null;
        d.ends = null;
        this.place(d, r, false);
      };
      if (d.act && !d.act.once) {
        // Halfway into it: out of it again at once.
        d.act = null;
        d.settled = false;
        if (isFigure(d.rig)) d.rig.campMove?.(null);
        go();
        return;
      }
      // (A sip or a bite cut short: it puts the thing away as ever.)
      if (d.act) {
        d.act = null;
        if (isFigure(d.rig)) d.rig.campMove?.(null);
      }
      this.getUp(d, go);
      return;
    }
    d.toSit = false;
    if (d.kind === 'walk') {
      d.ends = null;
      d.spot = null;
      this.place(d, r, false);
      return;
    }
    if (d.kind === 'talk' && d.partner) {
      const lead = this.idlers.indexOf(d) < this.idlers.indexOf(d.partner) ? d : d.partner;
      lead.partner!.goal = null;
      lead.goal = null;
      this.place(lead, r, false);
      return;
    }
    this.place(d, r, false);
  }
}

/** A number between `range`'s two, by `r`. */
const between = (r: () => number, range: [number, number]) => range[0] + r() * (range[1] - range[0]);

/** Whether `p` is within `reach` tiles of tile `c` (of its square, not its middle). */
function nearTile(p: Vec, c: { x: number; y: number }, reach: number): boolean {
  const dx = Math.max(c.x - p.x, 0, p.x - (c.x + 1));
  const dz = Math.max(c.y - p.z, 0, p.z - (c.y + 1));
  return Math.hypot(dx, dz) < reach;
}

/** `yaw` turned towards `goal` by at most `step` (radians), the short way round. */
function turnToward(yaw: number, goal: number, step: number): number {
  const d = Math.atan2(Math.sin(goal - yaw), Math.cos(goal - yaw));
  return yaw + THREE.MathUtils.clamp(d, -step, step);
}
