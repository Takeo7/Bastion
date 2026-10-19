// Walks played at the same time without anyone walking through anyone (the
// enemy turn's dead walking in one run, game.ts playGroup; the horde coming in
// by an edge, arrivals.ts). The engine plans such walks one after another,
// each from where the ones before ended up (rules.ts runAlienTurn), but they
// are played at once, at different paces: a runner overtook a walker by
// walking through it, two crossed in a doorway. This keeps a timeline of who
// holds which tile when and holds a walker back (at the start, or before a
// tile) while the tile it is about to enter is still held by another walker,
// or will be entered by one planned before it. No deadlocks and no long turn:
// each wait is capped, the group as a whole may take only a little longer
// than played all at once (SLACK), and a standstill lets one through. Where
// one has to go by another all the same (the engine walked it through an
// ally), the step is marked: the walker swerves round the other (unitViews).
// Plain numbers only (no three): a group is planned once, before it sets off.

/** One walker: the tiles it goes through (any key unique per tile and storey), the one it starts on first. */
export interface Walker {
  tiles: readonly number[];
  /** Seconds each step takes, tiles[k] to tiles[k + 1]. */
  steps: readonly number[];
  /** A diagonal step's two tiles beside (two walkers crossing through the same corner), null for the others. */
  corners?: readonly (readonly [number, number] | null)[];
}

/** A walker's plan: the seconds it stands before each step, and the tiles it passes someone on (it swerves). */
export interface WalkPlan {
  waits: number[];
  /** By tile index (tiles[k]): another stands there or comes through as it does. */
  passes: boolean[];
}

/** How far into a step (0 to 1) the tile it leaves is free again: the walker's body is past the tiles' border by then. */
const RELEASE = 0.6;
/** A walker going ahead of one planned before it must be off the shared tile this long before the other gets there. */
const AHEAD_MARGIN = 0.15;
/** The longest a walker waits for one planned before it to go by first (not its last tile: there it waits for good). */
const YIELD_CAP = 1.0;
/**
 * Stepping onto where it stops, a walker lets one planned after it go through there first if that one is due
 * within STOP_SOON, waiting STOP_CAP at most: else that one has to go by it standing.
 */
const STOP_SOON = 1.2;
const STOP_CAP = 0.8;
/**
 * The group may take this much longer than played all at once (seconds; a quarter of that if more): each
 * walker waits no more than lets it get there by then (docs/UX-RITMO.md: the dead walk together for pace).
 */
const SLACK = 0.8;
/** The plan gives up (one walk after another) past this many decisions. */
const MAX_EVENTS = 20000;

interface Live {
  /** The tile it stands on, or is moving into. */
  k: number;
  moving: boolean;
  start: number;
  end: number;
  release: number;
  /** When it got to where it waits now (its last step's end, or 0). */
  ready: number;
  /** Seconds it has stood so far, and may stand in all. */
  waited: number;
  budget: number;
  /** Let through once regardless (a standstill broken, its time used up). */
  forced: boolean;
  waits: number[];
  passes: boolean[];
}

/**
 * Each walker's plan, in their planning order (`walkers[0]` planned first: it
 * goes first wherever they meet). One that can't be fitted plays after the
 * walk before it (its first wait, the time until then).
 */
export function planWalks(walkers: readonly Walker[]): WalkPlan[] {
  const n = walkers.length;
  const totals = walkers.map((w) => w.steps.reduce((a, b) => a + b, 0));
  const longest = Math.max(0, ...totals);
  const deadline = longest + Math.max(SLACK, longest * 0.25);
  const live: Live[] = walkers.map((w, i) => ({
    k: 0, moving: false, start: 0, end: 0, release: 0, ready: 0, waited: 0, budget: deadline - totals[i]!, forced: false,
    waits: w.steps.map(() => 0), passes: w.tiles.map(() => false),
  }));
  const last = (w: number) => walkers[w]!.tiles.length - 1;
  const done = (w: number) => !live[w]!.moving && live[w]!.k >= last(w);
  /** Whether walker `o` holds tile `key` at time `t`: where it stands, where it goes, and where it leaves until it is clear of it. */
  const holds = (o: number, key: number, t: number) => {
    const l = live[o]!;
    const tiles = walkers[o]!.tiles;
    if (tiles[l.k] === key) return true;
    return l.moving && t < l.release && tiles[l.k - 1] === key;
  };
  /** Walker `o` crossing the corner between tiles a and b at time t (a diagonal step from one to the other). */
  const crossing = (o: number, a: number, b: number, t: number) => {
    const l = live[o]!;
    if (!l.moving || t >= l.release) return false;
    const tiles = walkers[o]!.tiles;
    const from = tiles[l.k - 1];
    const to = tiles[l.k];
    return (from === a && to === b) || (from === b && to === a);
  };
  /** When walker `o` would first hold its tile `idx` (the start of its step into it), at the earliest. */
  const reaches = (o: number, idx: number, t: number) => {
    const l = live[o]!;
    let at = l.moving ? l.end : t;
    for (let k = l.k; k < idx - 1; k++) at += walkers[o]!.steps[k]!;
    return at;
  };
  /** Someone in the way of walker `w`'s next step at `t` (it would go by them): the tile is held, or a crossing. */
  const inWay = (w: number, t: number) => {
    const l = live[w]!;
    const walker = walkers[w]!;
    const key = walker.tiles[l.k + 1]!;
    const corner = walker.corners?.[l.k] ?? null;
    for (let o = 0; o < n; o++) if (o !== w && (holds(o, key, t) || (corner && crossing(o, corner[0], corner[1], t)))) return true;
    return false;
  };
  /**
   * Why walker `w` can't take its next step at `t`: 'hold' (the tile is held), 'yield' (another goes through
   * it first), with whom, and until when at most (a capped wait); null if it can go.
   */
  const blocked = (w: number, t: number): { why: 'hold' | 'yield'; by: number; until?: number } | null => {
    const l = live[w]!;
    const walker = walkers[w]!;
    const key = walker.tiles[l.k + 1]!;
    const corner = walker.corners?.[l.k] ?? null;
    for (let o = 0; o < n; o++) {
      if (o === w) continue;
      // One stopped for good on its way: the engine walks it through (allies let each other by), at once.
      if (holds(o, key, t) && done(o)) continue;
      if (holds(o, key, t) || (corner && crossing(o, corner[0], corner[1], t))) return { why: 'hold', by: o };
    }
    const end = l.k + 1 === last(w);
    // Onto where it stops: one planned after it that goes through there soon goes first, for a while at most.
    if (end && t - l.ready < STOP_CAP - 1e-9) {
      for (let o = w + 1; o < n; o++) {
        if (done(o)) continue;
        const tiles = walkers[o]!.tiles;
        for (let idx = live[o]!.k + 1; idx < tiles.length - 1; idx++) {
          if (tiles[idx] === key && reaches(o, idx, t) - t <= STOP_SOON) return { why: 'yield', by: o, until: l.ready + STOP_CAP };
        }
      }
    }
    // Where one planned before it is still to go through: after it (the engine's order), or well ahead of it.
    for (let o = 0; o < w; o++) {
      if (done(o)) continue;
      const tiles = walkers[o]!.tiles;
      for (let idx = live[o]!.k + 1; idx < tiles.length; idx++) {
        if (tiles[idx] !== key) continue;
        // Where the earlier one stops: better gone through before it gets there (it waits for this one, if need be).
        if (idx === tiles.length - 1) break;
        if (end) return { why: 'yield', by: o };
        const clear = t + walker.steps[l.k]! + RELEASE * (walker.steps[l.k + 1] ?? 0);
        if (clear + AHEAD_MARGIN <= reaches(o, idx, t) || t - l.ready >= YIELD_CAP - 1e-9) break;
        return { why: 'yield', by: o, until: l.ready + YIELD_CAP };
      }
    }
    return null;
  };
  /** For one out of time: only another moving into or out of the tile it goes to holds it back. */
  const passing = (w: number, t: number): { why: 'hold'; by: number } | null => {
    const l = live[w]!;
    const walker = walkers[w]!;
    const key = walker.tiles[l.k + 1]!;
    const corner = walker.corners?.[l.k] ?? null;
    for (let o = 0; o < n; o++) {
      if (o !== w && live[o]!.moving && (holds(o, key, t) || (corner && crossing(o, corner[0], corner[1], t)))) return { why: 'hold', by: o };
    }
    return null;
  };
  const go = (w: number, t: number) => {
    const l = live[w]!;
    const seconds = walkers[w]!.steps[l.k]!;
    // Going by someone all the same (forced, or one stopped on its way): it swerves round them.
    if (inWay(w, t)) l.passes[l.k + 1] = true;
    l.waits[l.k] = t - l.ready;
    l.waited += t - l.ready;
    l.k++;
    l.moving = true;
    l.start = t;
    l.end = t + seconds;
    l.release = t + seconds * RELEASE;
    l.forced = false;
  };

  let t = 0;
  for (let events = 0; events < MAX_EVENTS; events++) {
    // Arrivals first: who got to its tile stands there, ready for the next.
    for (const l of live) {
      if (l.moving && l.end <= t + 1e-9) {
        l.moving = false;
        l.ready = l.end;
      }
    }
    if (live.every((_, w) => done(w))) return live.map((l) => ({ waits: l.waits, passes: l.passes }));
    // Then, in planning order, whoever can set off does; whoever has stood all it may, goes all the same.
    const stuck: { w: number; why: 'hold' | 'yield'; by: number; until?: number }[] = [];
    for (let w = 0; w < n; w++) {
      const l = live[w]!;
      if (l.moving || done(w)) continue;
      // Out of time it goes by whoever stands in its way, but not into a tile another is stepping into or out of.
      const late = l.waited + (t - l.ready) >= l.budget - 1e-9;
      const why = l.forced ? null : late ? passing(w, t) : blocked(w, t);
      if (!why) go(w, t);
      else stuck.push({ w, ...why });
    }
    // The next moment anything changes: a step ends or leaves its tile behind, a wait runs out.
    let next = Infinity;
    for (const l of live) {
      if (!l.moving) continue;
      if (l.release > t + 1e-9) next = Math.min(next, l.release);
      next = Math.min(next, l.end);
    }
    for (const s of stuck) if (s.until !== undefined && s.until > t) next = Math.min(next, s.until);
    if (next < Infinity) {
      // (Or sooner, when one of those waiting has stood all it may.)
      for (const s of stuck) {
        const out = live[s.w]!.ready + live[s.w]!.budget - live[s.w]!.waited;
        if (out > t + 1e-9) next = Math.min(next, out);
      }
      t = Math.max(t, next);
      continue;
    }
    // A standstill: nobody moves and nobody can. One goes through: who only gives way, the last planned; else who
    // waits on one that has stopped for good (the engine walked it through), else the first planned.
    if (!stuck.length) break;
    const yielding = stuck.filter((s) => s.why === 'yield');
    const onStill = stuck.filter((s) => s.why === 'hold' && done(s.by));
    const pick = yielding.length ? yielding[yielding.length - 1]! : onStill.length ? onStill[0]! : stuck[0]!;
    live[pick.w]!.forced = true;
  }
  // Not fitted: one after another, in planning order.
  let at = 0;
  return walkers.map((w, i) => {
    const waits = w.steps.map(() => 0);
    if (waits.length) waits[0] = at;
    at += totals[i]!;
    return { waits, passes: w.tiles.map(() => false) };
  });
}

/** When the last of them gets there, with these waits (for measuring a plan against playing them all at once). */
export function planLength(walkers: readonly Walker[], waits: readonly (readonly number[])[]): number {
  let longest = 0;
  walkers.forEach((w, i) => {
    const total = w.steps.reduce((a, b) => a + b, 0) + (waits[i] ?? []).reduce((a, b) => a + b, 0);
    longest = Math.max(longest, total);
  });
  return longest;
}
