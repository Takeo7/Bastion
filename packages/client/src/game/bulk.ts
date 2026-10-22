import * as THREE from 'three';

// A body's bulk (playtest 20/10 #1: «no quiero que se atraviesen cosas, nunca»): each part of a character as its own
// mesh fills it at rest, its coat and its kit with it, for the figure (syntyFigures.ts) to keep its arms and what it
// carries out of. The torso by the bone each piece of it moves with (the hips, the three of the spine), each thigh and
// each shin; each measured slice by slice along its axis, how far out it goes each way round. What a vertex belongs
// to is its main bone (its skin weights); the arms are measured too, their thickness round their bones.

/** How many ways round a part is measured, and how far apart its slices are (model space). */
const SECTORS = 16;
const SLICE = 0.03;
/** A vertex is a part's when this much of its weight is that part's bone; the torso's only when nearly all of it is the torso's (an armpit's, shared with the arm, stands out at the side in the rest's T). */
const OWN = 0.5;
const TORSO_PURE = 0.9;

/** One part of a body at rest: its bone, its axis and the radii of its slices round it. */
export interface Girth {
  bone: number;
  /** Where its first slice starts, and the axis it is measured along (model space, rest). */
  origin: THREE.Vector3;
  axis: THREE.Vector3;
  /** Across the axis: the angle round it is measured from u towards v. */
  u: THREE.Vector3;
  v: THREE.Vector3;
  slices: number;
  /** Slice by slice, sector by sector: how far out the part goes. */
  radii: Float32Array;
  /** A sphere round all of it, to pass it by at once. */
  centre: THREE.Vector3;
  reach: number;
}

/** A body's parts, and how thick its arms are (left, right): the upper arm, the forearm, the hand. */
export interface Bulk {
  parts: Girth[];
  upper: [number, number];
  fore: [number, number];
  /** How far out the sleeves go at most round their bones (left, right), for a gun to keep clear of (22/10: through them). */
  upperOut: [number, number];
  foreOut: [number, number];
  /** And so bin by bin along them (ARM_BINS, from the joint to the next: a coat's elbow bulges out twice as far). */
  upperAlong: [number[], number[]];
  foreAlong: [number[], number[]];
  hand: number;
  /** How far ahead the torso goes (its foremost point at rest, model space z, between the hips and the chest): a belly, a vest's pouches. */
  front: number;
}

/** The bones a bulk is measured by (the figure's indices). */
export interface BulkBones {
  hips: number;
  spine: number[];
  thighs: [number, number];
  shins: [number, number];
  ankles: [number, number];
  clavicles: [number, number];
  shoulders: [number, number];
  elbows: [number, number];
  hands: [number, number];
}

const TAU = Math.PI * 2;

function girthOf(bone: number, points: THREE.Vector3[], from: THREE.Vector3, axis: THREE.Vector3): Girth | null {
  if (points.length < 8) return null;
  const u = new THREE.Vector3(1, 0, 0);
  if (Math.abs(u.dot(axis)) > 0.9) u.set(0, 0, 1);
  u.addScaledVector(axis, -u.dot(axis)).normalize();
  const v = new THREE.Vector3().crossVectors(axis, u);
  const w = new THREE.Vector3();
  let lo = Infinity;
  let hi = -Infinity;
  for (const p of points) {
    const t = w.copy(p).sub(from).dot(axis);
    lo = Math.min(lo, t);
    hi = Math.max(hi, t);
  }
  const slices = Math.max(1, Math.ceil((hi - lo) / SLICE));
  const origin = from.clone().addScaledVector(axis, lo);
  const radii = new Float32Array(slices * SECTORS);
  for (const p of points) {
    w.copy(p).sub(origin);
    const s = Math.min(slices - 1, Math.floor(w.dot(axis) / SLICE));
    const x = w.dot(u);
    const y = w.dot(v);
    const k = Math.floor((((Math.atan2(y, x) / TAU) % 1) + 1) % 1 * SECTORS) % SECTORS;
    const i = s * SECTORS + k;
    radii[i] = Math.max(radii[i]!, Math.hypot(x, y));
  }
  // A sector nothing fell in (a thin slice) takes the larger of its neighbours; an empty slice, the one before.
  for (let s = 0; s < slices; s++) {
    const row = radii.subarray(s * SECTORS, (s + 1) * SECTORS);
    if (!row.some((r) => r > 0) && s > 0) row.set(radii.subarray((s - 1) * SECTORS, s * SECTORS));
    const filled = Float32Array.from(row);
    for (let k = 0; k < SECTORS; k++) {
      if (row[k]! > 0) continue;
      filled[k] = Math.max(row[(k + SECTORS - 1) % SECTORS]!, row[(k + 1) % SECTORS]!);
    }
    row.set(filled);
  }
  const centre = origin.clone().addScaledVector(axis, (slices * SLICE) / 2);
  let reach = 0;
  for (const p of points) reach = Math.max(reach, p.distanceTo(centre));
  return { bone, origin, axis: axis.clone(), u, v, slices, radii, centre, reach };
}

/** How deep a ball of `radius` at `q` (the part's bone's rest frame, model space) is in part g: ≤ 0 clear of it. */
export function depthIn(g: Girth, q: THREE.Vector3, radius: number): number {
  const ox = q.x - g.origin.x;
  const oy = q.y - g.origin.y;
  const oz = q.z - g.origin.z;
  const t = ox * g.axis.x + oy * g.axis.y + oz * g.axis.z;
  const length = g.slices * SLICE;
  if (t < -radius || t > length + radius) return -1;
  const x = ox * g.u.x + oy * g.u.y + oz * g.u.z;
  const y = ox * g.v.x + oy * g.v.y + oz * g.v.z;
  const rho = Math.hypot(x, y);
  // Between the two slices and the two sectors either side.
  const s = THREE.MathUtils.clamp(t / SLICE - 0.5, 0, g.slices - 1);
  const s0 = Math.floor(s);
  const s1 = Math.min(g.slices - 1, s0 + 1);
  const fs = s - s0;
  const a = ((((Math.atan2(y, x) / TAU) % 1) + 1) % 1) * SECTORS - 0.5;
  const k0 = (Math.floor(a) + SECTORS) % SECTORS;
  const k1 = (k0 + 1) % SECTORS;
  const fk = a - Math.floor(a);
  const r = g.radii;
  const at = (sl: number) => r[sl * SECTORS + k0]! * (1 - fk) + r[sl * SECTORS + k1]! * fk;
  const R = at(s0) * (1 - fs) + at(s1) * fs;
  // Past an end the ball only reaches in as far as it overlaps it.
  const over = t < 0 ? -t : t > length ? t - length : 0;
  return R + Math.sqrt(Math.max(0, radius * radius - over * over)) - rho;
}

/** How far a point is from the segment a–b; null by its ends (a joint's flesh is the joint's, not the limb's girth). */
function fromSegment(p: THREE.Vector3, a: THREE.Vector3, b: THREE.Vector3): number | null {
  const ab = b.clone().sub(a);
  const t = p.clone().sub(a).dot(ab) / ab.lengthSq();
  if (t < 0.25 || t > 0.95) return null;
  return p.distanceTo(a.clone().addScaledVector(ab, t));
}

/** How many bins an arm's part is measured in along it (Bulk.upperAlong). */
export const ARM_BINS = 5;

/** Where along a part from a to b a point is (0 to 1), and how far from its line; null off its ends. */
function alongSegment(p: THREE.Vector3, a: THREE.Vector3, b: THREE.Vector3): { t: number; r: number } | null {
  const ab = b.clone().sub(a);
  const t = p.clone().sub(a).dot(ab) / ab.lengthSq();
  if (t < 0 || t > 1) return null;
  return { t, r: p.distanceTo(a.clone().addScaledVector(ab, t)) };
}

/** The radii of an arm's part bin by bin along it, at most (97 %), each at least the whole part's. */
function radiiAlong(list: { t: number; r: number }[], least: number): number[] {
  return Array.from({ length: ARM_BINS }, (_, k) => {
    const bin = list.filter((q) => Math.min(ARM_BINS - 1, Math.floor(q.t * ARM_BINS)) === k).map((q) => q.r);
    return bin.length >= 6 ? Math.max(least, share(bin, 0.97, least)) : least;
  });
}

/** A value most of a list stays under (its k-th part, sorted). */
function share(list: number[], k: number, fallback: number): number {
  if (!list.length) return fallback;
  list.sort((a, b) => a - b);
  return list[Math.min(list.length - 1, Math.floor(list.length * k))]!;
}

/**
 * A character's bulk from its model at rest (the skeleton as the model stands, as restOf reads it): `p` the joints there, `bones` the
 * indices it is measured by, `index` the figure's index of a bone by name. Its skinned body mesh only (the hair and a
 * hat are not in the arms' way).
 */
export function bulkOf(model: THREE.Object3D, p: THREE.Vector3[], bones: BulkBones, index: (name: string) => number | undefined): Bulk | null {
  const torsoBones = [bones.hips, ...bones.spine];
  const pieces = new Map<number, THREE.Vector3[]>();
  const upper: [number[], number[]] = [[], []];
  const fore: [number[], number[]] = [[], []];
  const upperAt: [{ t: number; r: number }[], { t: number; r: number }[]] = [[], []];
  const foreAt: [{ t: number; r: number }[], { t: number; r: number }[]] = [[], []];
  let found = false;
  let front = -Infinity;
  model.traverse((o) => {
    if (!(o instanceof THREE.SkinnedMesh) || o.name !== 'body') return;
    found = true;
    const pos = o.geometry.getAttribute('position');
    const si = o.geometry.getAttribute('skinIndex');
    const sw = o.geometry.getAttribute('skinWeight');
    const own = o.skeleton.bones.map((b) => index(b.name) ?? -1);
    for (let i = 0; i < pos.count; i++) {
      let best = -1;
      let weight = 0;
      let torsoW = 0;
      for (let j = 0; j < 4; j++) {
        const w = sw.getComponent(i, j);
        const b = own[si.getComponent(i, j)] ?? -1;
        if (torsoBones.includes(b)) torsoW += w;
        if (w > weight) {
          weight = w;
          best = b;
        }
      }
      if (best < 0 || weight < OWN) continue;
      // Where it is with the skeleton at rest (its geometry is kept as it was bound, the arms down: not the rest's T).
      const v = o.getVertexPosition(i, new THREE.Vector3()).applyMatrix4(o.matrixWorld);
      const side = bones.shoulders.indexOf(best);
      const u = side >= 0 ? fromSegment(v, p[best]!, p[bones.elbows[side]!]!) : null;
      if (u !== null) upper[side]!.push(u);
      const ua = side >= 0 ? alongSegment(v, p[best]!, p[bones.elbows[side]!]!) : null;
      if (ua) upperAt[side]!.push(ua);
      const elbow = bones.elbows.indexOf(best);
      const f = elbow >= 0 ? fromSegment(v, p[best]!, p[bones.hands[elbow]!]!) : null;
      if (f !== null) fore[elbow]!.push(f);
      const fa = elbow >= 0 ? alongSegment(v, p[best]!, p[bones.hands[elbow]!]!) : null;
      if (fa) foreAt[elbow]!.push(fa);
      if (torsoBones.includes(best) && torsoW >= TORSO_PURE && v.y > p[bones.hips]!.y && v.y < p[bones.spine[bones.spine.length - 1]!]!.y) front = Math.max(front, v.z);
      if ((torsoBones.includes(best) && torsoW >= TORSO_PURE) || bones.thighs.includes(best) || bones.shins.includes(best)) {
        let list = pieces.get(best);
        if (!list) pieces.set(best, (list = []));
        list.push(v);
      }
    }
  });
  if (!found) return null;
  const parts: Girth[] = [];
  const up = new THREE.Vector3(0, 1, 0);
  for (const b of torsoBones) {
    const g = girthOf(b, pieces.get(b) ?? [], p[b]!, up);
    if (g) parts.push(g);
  }
  for (let side = 0; side < 2; side++) {
    for (const [from, to] of [
      [bones.thighs[side]!, bones.shins[side]!],
      [bones.shins[side]!, bones.ankles[side]!],
    ] as const) {
      const g = girthOf(from, pieces.get(from) ?? [], p[from]!, p[to]!.clone().sub(p[from]!).normalize());
      if (g) parts.push(g);
    }
  }
  return {
    parts,
    upper: [share(upper[0], 0.7, 0.045), share(upper[1], 0.7, 0.045)],
    fore: [share(fore[0], 0.7, 0.04), share(fore[1], 0.7, 0.04)],
    upperOut: [share(upper[0], 0.97, 0.055), share(upper[1], 0.97, 0.055)],
    foreOut: [share(fore[0], 0.97, 0.05), share(fore[1], 0.97, 0.05)],
    upperAlong: [radiiAlong(upperAt[0], share(upper[0], 0.97, 0.055)), radiiAlong(upperAt[1], share(upper[1], 0.97, 0.055))],
    foreAlong: [radiiAlong(foreAt[0], share(fore[0], 0.97, 0.05)), radiiAlong(foreAt[1], share(fore[1], 0.97, 0.05))],
    hand: 0.03,
    front: Number.isFinite(front) ? front : p[bones.hips]!.z + 0.12,
  };
}

/** The cloth round the shoulders made to go with the arms (easeShoulders): off in the weapons lab, to compare. */
export const EASE_SHOULDERS = { on: true };
/** Round each shoulder joint: how far the cloth is made to go with the arm, and the most of its weight that goes; only out at the side (the outer share of the way from the spine to the shoulder), not the collar. */
const SHOULDER_REACH = 0.14;
const SHOULDER_SHARE = 0.8;
const SHOULDER_SIDE = 0.45;

/**
 * The cloth round each shoulder made to go with its arm (playtest 20/10 #1): a vest's armhole, a jacket's or a hood's
 * shoulder is weighted all to the chest in many of the models, stays where it is as the arm comes down from the
 * model's T, and the arm goes through its edge. Each vertex of the torso near a shoulder joint at rest gives that
 * shoulder a share of its weight, more the nearer (all of SHOULDER_SHARE in its inner half). Once a model (its
 * geometry is its clones' too), before its bulk is measured.
 */
export function easeShoulders(model: THREE.Object3D, p: THREE.Vector3[], bones: BulkBones, index: (name: string) => number | undefined): number {
  const torso = [bones.hips, ...bones.spine, ...bones.clavicles];
  let eased = 0;
  if (!EASE_SHOULDERS.on) return 0;
  model.traverse((o) => {
    if (!(o instanceof THREE.SkinnedMesh) || o.name !== 'body') return;
    const pos = o.geometry.getAttribute('position');
    const si = o.geometry.getAttribute('skinIndex');
    const sw = o.geometry.getAttribute('skinWeight');
    const own = o.skeleton.bones.map((b) => index(b.name) ?? -1);
    const shoulders = bones.shoulders.map((b) => own.indexOf(b));
    if (shoulders.some((k) => k < 0)) return;
    const v = new THREE.Vector3();
    const idx = [0, 0, 0, 0];
    const wts = [0, 0, 0, 0];
    for (let i = 0; i < pos.count; i++) {
      // Only what is the torso's: its main bone one of the torso's.
      let best = -1;
      let weight = 0;
      for (let j = 0; j < 4; j++) {
        idx[j] = si.getComponent(i, j);
        wts[j] = sw.getComponent(i, j);
        if (wts[j]! > weight) {
          weight = wts[j]!;
          best = own[idx[j]!] ?? -1;
        }
      }
      if (!torso.includes(best)) continue;
      o.getVertexPosition(i, v).applyMatrix4(o.matrixWorld);
      let changed = false;
      for (let side = 0; side < 2; side++) {
        const joint = p[bones.shoulders[side]!]!;
        const d = v.distanceTo(joint);
        if (d >= SHOULDER_REACH) continue;
        // Out at its side of the chest only: the collar and the middle stay the chest's.
        const spine = p[bones.spine[bones.spine.length - 1]!]!;
        if ((v.x - spine.x) / (joint.x - spine.x) < SHOULDER_SIDE) continue;
        const share = SHOULDER_SHARE * THREE.MathUtils.smoothstep(SHOULDER_REACH - d, 0, SHOULDER_REACH / 2);
        if (share <= 0) continue;
        changed = true;
        // The torso's weights give it up in proportion; the shoulder takes it in its own slot, or the lightest's.
        const shoulder = shoulders[side]!;
        let slot = idx.findIndex((k, j) => k === shoulder && wts[j]! > 0);
        let given = 0;
        for (let j = 0; j < 4; j++) {
          if (!torso.includes(own[idx[j]!] ?? -1) || !wts[j]) continue;
          given += wts[j]! * share;
          wts[j] = wts[j]! * (1 - share);
        }
        if (slot < 0) {
          slot = wts.indexOf(Math.min(...wts));
          given += wts[slot]!;
          idx[slot] = shoulder;
          wts[slot] = 0;
        }
        wts[slot] = wts[slot]! + given;
      }
      if (!changed) continue;
      si.setXYZW(i, idx[0]!, idx[1]!, idx[2]!, idx[3]!);
      sw.setXYZW(i, wts[0]!, wts[1]!, wts[2]!, wts[3]!);
      eased++;
    }
    si.needsUpdate = true;
    sw.needsUpdate = true;
  });
  return eased;
}
