import * as THREE from 'three';
import { bastionFootprint, bastionLot, bastionStage, cellsOf, createMission, onBastionLot, pieceAt, PIECES, Rng, treeAt, type Appearance, type BasePiece, type BaseState, type GameState, type PieceId, type TemplateId } from '@bastion/engine';
import { drawPiece, pieceMap, type PieceArt } from '../game/basePieces';
import { EvacBus } from '../game/evacBus';
import { MapView } from '../game/mapView';
import { dayMood, runMapJobs } from '../game/renderer';
import { graphics } from '../settings';
import { syntyProp, syntyPropsLoaded } from '../game/syntyProps';
import type { AtlasId, PropId } from '../game/syntyManifest';
import type { Gear } from '../game/figures';
import { buildUnit, IDLE_POSE, isFigure, type UnitRig } from '../game/models';
import { STOREY } from '../game/coords';
import { atlasTexture, loadModel } from '../game/synty';
import { tween } from '../game/tween';
import type { ClearingArt } from './clearing';
import { skyTexture, type Shot, type StageMood } from './stage';

/** A soldier standing by La Lata (the squad, the debrief) or at its post for a siege. */
export interface LineupEntry {
  template: TemplateId;
  color: string;
  appearance?: Appearance;
  gear?: Gear;
  /** Debrief pose: wounded soldiers slump, the fallen leave a memorial. */
  status?: 'ok' | 'wounded' | 'dead';
  /** Picks the same Synty survivor everywhere for this soldier (their campaign id). */
  seed?: string;
}

// The base's clearing drawn (docs/FASE12.md §11.7) for UI's ClearingSet: the
// battle's own map (MapView) over the clearing as its siege would have it
// (baseLayout, through createMission), so the base looks on its screen as it
// does when the dead come; under the battle's daylight, or the snow's. The
// pieces are basePieces.ts'. A change (a pine felled, a piece put, a level
// up) redraws only the chunks it touches.

export class ClearingArtView implements ClearingArt {
  readonly group = new THREE.Group();
  readonly mood: StageMood;
  private readonly map = new MapView();
  private readonly sun = new THREE.DirectionalLight();
  private readonly sky = new THREE.HemisphereLight();
  private readonly fill = new THREE.DirectionalLight();
  private readonly look: ReturnType<typeof dayMood>;
  private built = false;
  private readonly snow: boolean;
  /** La Lata, parked on a track by the caravan (parkBus), as the campaign has it (setVehicle); and how far it goes to be out of the clearing. */
  private bus = new EvacBus();
  private busWay = 14;
  /** Its way out along its track (and back in by it) is clear to the clearing's edge; if not, it fades out and in where it stands. */
  private busRoute = true;
  /** La Lata is out on a mission (gone down its track until it comes back); and while it drives, the vehicle a change asked for, for after. */
  private away = false;
  private driving: Promise<void> | null = null;
  private nextVehicle: number | null = null;
  /**
   * The Soldados' and the Armería's screens (the caravan is both, FASE12 §11): the soldier looked at, standing by
   * the caravan (by the barracks once there is one), turning slowly; the Armería's corner at the caravan's end, a
   * bench and a rack of rifles under a tarp. Where each stands and which way its camera looks from (campPlaces).
   */
  private readonly showcase = new THREE.Group();
  private showcaseRig: UnitRig | null = null;
  private showcaseKey = '';
  private readonly armoryCorner = new THREE.Group();
  private soldierAt = { x: 0, z: 0, out: { x: 0, z: 1 } };
  private armoryAt = { x: 0, z: 0, out: { x: 1, z: 0 } };
  /** Which way each shot's camera stands from what it looks at (on the ground plan), and how far: the clearest (campPlaces). */
  private soldierView = { dir: { x: 0, z: 1 }, dist: 4 };
  private armoryView = { dir: { x: 0.92, z: 0.4 }, dist: 3.5 };
  /** Frames left with the shadow map drawn whatever (the first ones, while the props come in). */
  private warm = 30;
  /**
   * The squad by La Lata (playtest 18/10, #44: choosing it and the debrief, on the clearing): in a row along the
   * bus's side or across the track at its tail, whichever its camera sees most clearly (placeLineup); one
   * commander's half and then the other's, in two rows past eight. The fallen leave a cross with a candle.
   */
  private readonly lineup = new THREE.Group();
  private lineupRigs: UnitRig[] = [];
  private lineupKey = '';
  /** What it was built from: its places, and whether it is mirrored (each player's own on their left: playtest 18/10 #51). */
  private lineupEntries: (LineupEntry | null)[] = [];
  private mirror = false;
  /** The row in two halves, a step apart (the two commanders' soldiers), or one (a commander alone). */
  private halves = true;
  private place: LineupPlace | null = null;
  private base: BaseState | null = null;
  /** How high each tile's floor stands (a watchtower's deck), from the clearing as its siege has it. */
  private elev: readonly number[] = [];
  /**
   * The siege's posts (playtest 18/10, #58: each player sets their soldiers about the clearing): a figure standing on
   * each, looking out to the nearest edge, where the dead come in; kept from one change to the next by who it is.
   */
  private readonly posts = new THREE.Group();
  private postRigs = new Map<string, UnitRig>();
  private postsKey = '';

  constructor(snow = false) {
    this.snow = snow;
    const m = (this.look = dayMood(snow));
    this.mood = { exposure: m.exposure, bloom: m.bloom, grade: m.grade };
    this.sun.color.set(m.sun);
    this.sun.intensity = m.sunIntensity;
    this.sun.castShadow = true;
    const size = graphics().effects === 'high' ? 4096 : 2048;
    this.sun.shadow.mapSize.set(size, size);
    this.sun.shadow.bias = -0.0004;
    this.sun.shadow.normalBias = 0.02;
    this.sky.color.set(m.skyLight);
    this.sky.groundColor.set(m.groundLight);
    this.sky.intensity = m.skyIntensity;
    this.fill.color.set(m.fill);
    this.fill.intensity = m.fillIntensity;
    this.group.add(this.map.group, this.bus.root, this.showcase, this.armoryCorner, this.lineup, this.posts, this.sun, this.sun.target, this.sky, this.fill, this.fill.target);
  }

  /** The sky and the haze of the battle on `scene` (the set's): its lights are this group's. */
  dress(scene: THREE.Scene): void {
    scene.background = skyTexture(this.look.sky);
    scene.fog = new THREE.FogExp2(this.look.haze, this.look.hazeDensity);
  }

  /** The camera the clearing is seen with, for what the map does by it (its lamps, what fades). */
  watch(camera: THREE.Camera): void {
    this.map.watch(camera, () => ({ selected: null, hover: null, target: null, level: null }));
  }

  setBase(base: BaseState): void {
    const s = clearingState(base, this.snow);
    this.elev = s.elev;
    if (!this.built) {
      this.map.build(s);
      this.built = true;
      this.aim(base);
    } else this.map.rebuild(s);
    // It is ours: all of it in sight (§11.1).
    this.map.setVisibility(new Array<boolean>(s.width * s.height).fill(true));
    this.base = base;
    this.parkBus(base);
    this.campPlaces(base);
    // The squad stands again where it now can.
    this.place = null;
    this.relineup();
  }

  /**
   * Where the soldier looked at stands (out from the barracks' front if there
   * is one, else the caravan's long side) and the Armería's corner (at the
   * caravan's end): the first side of each with nothing built and no pine
   * there; and the corner's things, those whose tile is free.
   */
  private campPlaces(base: BaseState): void {
    const caravan = base.pieces.find((p) => p.piece === 'caravan');
    const quarters = base.pieces.find((p) => p.piece === 'quarters' && p.hp > 0);
    // Off the Bastion's lot (its building and its yard) as off anything built.
    const free = (x: number, z: number) => {
      const tx = Math.floor(x);
      const tz = Math.floor(z);
      return tx >= 0 && tz >= 0 && tx < base.width && tz < base.height && !pieceAt(base, tx, tz) && !treeAt(base, tx, tz) && !onBastionLot(base, tx, tz);
    };
    /** A piece's middle and its sides (out, and how far its edge is), its own front (+y as built, turned) first; the caravan's, the Bastion's footprint. */
    const sides = (p: BasePiece) => {
      const cells = p.piece === 'caravan' ? bastionFootprint(base) : cellsOf(p);
      const xs = cells.map((c) => c.x);
      const ys = cells.map((c) => c.y);
      const mid = { x: (Math.min(...xs) + Math.max(...xs) + 1) / 2, z: (Math.min(...ys) + Math.max(...ys) + 1) / 2 };
      const half = { x: (Math.max(...xs) - Math.min(...xs) + 1) / 2, z: (Math.max(...ys) - Math.min(...ys) + 1) / 2 };
      const front = [{ x: 0, z: 1 }, { x: -1, z: 0 }, { x: 0, z: -1 }, { x: 1, z: 0 }][p.rot % 4]!;
      const all = [front, { x: front.z, z: -front.x }, { x: -front.z, z: front.x }, { x: -front.x, z: -front.z }];
      return { mid, half, all };
    };
    const footprint = new Set(bastionFootprint(base).map((c) => c.y * base.width + c.x));
    /** What stands tall enough on a tile to hide what is behind it: the Bastion, a building (not a low one), a wall, a palisade, a gate, the tower, a floodlight, a pine. */
    const tall = (x: number, z: number) => {
      const tx = Math.floor(x);
      const tz = Math.floor(z);
      if (tx < 0 || tz < 0 || tx >= base.width || tz >= base.height) return false;
      if (treeAt(base, tx, tz) || footprint.has(tz * base.width + tx)) return true;
      const p = pieceAt(base, tx, tz);
      if (!p || p.hp <= 0) return false;
      const def = PIECES[p.piece];
      return def.kind === 'building' ? def.tile !== 'low' : TALL.has(p.piece);
    };
    /**
     * The clearest way for a camera to look at (x, z) from `out` (on the ground plan): turned up to 70° either way and
     * near or far, the fewest tall tiles between them (and none under the camera), the least turned.
     */
    const view = (x: number, z: number, out: { x: number; z: number }, far: number) => {
      let best = { dir: out, dist: far, cost: Infinity };
      for (const a of [0, 0.4, -0.4, 0.8, -0.8, 1.2, -1.2]) {
        const dir = { x: out.x * Math.cos(a) - out.z * Math.sin(a), z: out.x * Math.sin(a) + out.z * Math.cos(a) };
        for (const dist of [far, far * 0.75]) {
          let cost = Math.abs(a) + (dist < far ? 0.5 : 0);
          if (tall(x + dir.x * dist, z + dir.z * dist)) cost += 50;
          for (let t = 0.7; t < dist; t += 0.25) if (tall(x + dir.x * t, z + dir.z * t)) cost += 10;
          if (cost < best.cost) best = { dir, dist, cost };
        }
      }
      return best;
    };
    const home = quarters ?? caravan;
    if (home) {
      const { mid, half, all } = sides(home);
      let best: { x: number; z: number; out: { x: number; z: number }; dir: { x: number; z: number }; dist: number; cost: number } | null = null;
      all.forEach((out, k) => {
        // Out from its side, past its lot's yard if need be.
        let reach = (out.x ? half.x : half.z) + 1.4;
        while (reach < (out.x ? half.x : half.z) + 5 && !free(mid.x + out.x * reach, mid.z + out.z * reach)) reach += 0.5;
        const x = mid.x + out.x * reach;
        const z = mid.z + out.z * reach;
        if (!free(x, z)) return;
        // Its front first, if nothing hides the soldier there.
        const v = view(x, z, out, 4);
        const cost = v.cost + k * 0.6;
        if (!best || cost < best.cost) best = { x, z, out, dir: v.dir, dist: v.dist, cost };
      });
      if (best) {
        const b = best as { x: number; z: number; out: { x: number; z: number }; dir: { x: number; z: number }; dist: number };
        this.soldierAt = { x: b.x, z: b.z, out: b.out };
        this.soldierView = { dir: b.dir, dist: b.dist };
      }
    }
    this.showcase.position.set(this.soldierAt.x, 0, this.soldierAt.z);
    this.armoryCorner.clear();
    if (!caravan) return;
    const { mid, half } = sides(caravan);
    // The end of the caravan: along its length.
    const along = half.x >= half.z ? { x: 1, z: 0 } : { x: 0, z: 1 };
    // The caravan's end the camera sees most clearly, from out and to one side or the other of it.
    let best: { x: number; z: number; out: { x: number; z: number }; dir: { x: number; z: number }; dist: number; cost: number } | null = null;
    for (const end of [1, -1]) {
      const out = { x: along.x * end, z: along.z * end };
      let reach = (along.x ? half.x : half.z) + 1.1;
      while (reach < (along.x ? half.x : half.z) + 4 && !free(mid.x + out.x * reach, mid.z + out.z * reach)) reach += 0.5;
      const x = mid.x + out.x * reach;
      const z = mid.z + out.z * reach;
      if (!free(x, z)) continue;
      for (const lean of [1, -1]) {
        const side = { x: -out.z * lean, z: out.x * lean };
        const length = Math.hypot(3.2, 1.4);
        const v = view(x - out.x * 0.2, z - out.z * 0.2, { x: (out.x * 3.2 + side.x * 1.4) / length, z: (out.z * 3.2 + side.z * 1.4) / length }, 3.5);
        if (!best || v.cost < best.cost) best = { x, z, out, dir: v.dir, dist: v.dist, cost: v.cost };
      }
    }
    if (!best) return;
    const b = best as { x: number; z: number; out: { x: number; z: number }; dir: { x: number; z: number }; dist: number };
    this.armoryAt = { x: b.x, z: b.z, out: b.out };
    this.armoryView = { dir: b.dir, dist: b.dist };
    this.fillArmory(base, free);
  }

  /** The Armería's corner at armoryAt, against the caravan's end: the bench with its toolbox, a rack of rifles, ammunition and supplies. */
  private fillArmory(base: BaseState, free: (x: number, z: number) => boolean): void {
    const { x, z, out } = this.armoryAt;
    const side = { x: -out.z, z: out.x };
    const yaw = Math.atan2(out.x, out.z);
    let lacking = false;
    const put = (id: PropId, u: number, v: number, turn = 0, k = 1, y = 0) => {
      const px = x + out.x * v + side.x * u;
      const pz = z + out.z * v + side.z * u;
      if (!free(px, pz)) return;
      const m = new THREE.Matrix4().compose(new THREE.Vector3(px, y, pz), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), yaw + turn), new THREE.Vector3(k, k, k));
      if (!propMeshes(id, m, this.armoryCorner)) lacking = true;
    };
    put('bWorkbench', -0.5, -0.45, Math.PI, 0.8);
    put('bToolbox', -0.15, 0.05, 0.4, 1);
    put('gunRack', 0.6, -0.5, Math.PI, 0.75);
    put('lootAmmo', 0.85, 0.35, 0.3, 1);
    put('lootAmmo', 1.15, 0.55, 1.2, 0.9);
    put('bSupplyCrate', -1.0, 0.45, 0.2, 0.75);
    // The rifles in the rack, standing, a little apart.
    const rack = { x: x + out.x * -0.5 + side.x * 0.6, z: z + out.z * -0.5 + side.z * 0.6 };
    if (free(rack.x, rack.z)) {
      (['rifle', 'shotgun', 'huntingRifle'] as const).forEach((id, k) => {
        void loadModel('apocalypse', 'wep', id).then((gun) => {
          const g = gun.clone();
          const t = (k - 1) * 0.15;
          g.position.set(rack.x + side.x * t + out.x * 0.02, 0.12, rack.z + side.z * t + out.z * 0.02);
          g.rotation.set(-Math.PI / 2 + 0.12, yaw + Math.PI, 0, 'YXZ');
          g.traverse((o) => {
            if (o instanceof THREE.Mesh) o.castShadow = true;
          });
          this.armoryCorner.add(g);
        }).catch(() => undefined);
      });
    }
    if (lacking) void syntyPropsLoaded().then(() => {
      this.armoryCorner.clear();
      this.fillArmory(base, free);
    });
  }

  /** The soldier looked at in Soldados (as the old Cuartel's pedestal): built again only if it changed; null, none. */
  setShowcase(template: TemplateId | null, color: string, appearance?: Appearance, gear?: Gear, seed?: string): void {
    const key = `${template}:${color}:${JSON.stringify(appearance ?? null)}:${JSON.stringify(gear ?? null)}:${seed ?? ''}`;
    if (key === this.showcaseKey) return;
    this.showcaseKey = key;
    const turned = this.showcaseRig?.root.rotation.y ?? Math.atan2(this.soldierAt.out.x, this.soldierAt.out.z);
    this.showcase.clear();
    this.showcaseRig = null;
    if (!template) return;
    const rig = buildUnit(template, color, appearance, gear, seed);
    rig.root.rotation.y = turned;
    this.showcase.add(rig.root);
    this.showcaseRig = rig;
  }

  /** Soldados: the soldier by the caravan (or the barracks), seen from out in the clearing, the pines behind it. */
  soldierShot(): Shot {
    const { x, z } = this.soldierAt;
    const { dir, dist } = this.soldierView;
    const target = new THREE.Vector3(x, 1.0, z);
    return { pos: target.clone().add(new THREE.Vector3(dir.x * dist, 1.5, dir.z * dist)), target, fov: 38 };
  }

  /** The Armería: its corner at the caravan's end, from out and to the side of it. */
  armoryShot(): Shot {
    const { x, z, out } = this.armoryAt;
    const { dir, dist } = this.armoryView;
    const target = new THREE.Vector3(x + out.x * -0.2, 0.6, z + out.z * -0.2);
    return { pos: target.clone().add(new THREE.Vector3(dir.x * dist, 1.5, dir.z * dist)), target, fov: 40 };
  }

  /**
   * La Lata by the caravan, nose out along a track (base.ts onTrack), where
   * nothing is built and no pine stands under it: the nearest such spot up
   * each track from the caravan, a lane to either side if the middle is
   * taken; its doors to the caravan. Best where its way out along the track
   * to the clearing's edge is clear too (it leaves and comes back by it:
   * busRoute); if no spot has one, it fades out and in where it stands.
   * Nowhere clear, it is away (hidden).
   */
  private parkBus(base: BaseState): void {
    // From the Bastion's lot out (never on it: playtest 18/10, #40).
    const lot = bastionLot(base);
    const [cw, ch] = lot ? [lot.w, lot.h] : [0, 0];
    const mid = lot ? { x: lot.x + lot.w / 2, z: lot.y + lot.h / 2 } : { x: base.width / 2, z: base.height / 2 };
    const length = this.bus.length;
    const halfWidth = this.bus.width / 2;
    const blocked = (x: number, y: number) => pieceAt(base, x, y) || treeAt(base, x, y) || onBastionLot(base, x, y);
    /**
     * Nothing under it from `from` to `to` tiles along `dir` from `at` (its middle), its width across; `out`:
     * past the clearing's edge the way goes on (its way out), else it is no place to stand.
     */
    const clear = (at: THREE.Vector3, dir: { x: number; z: number }, from: number, to: number, out = false) => {
      for (let k = from; k <= to + 1e-6; k += 0.5) {
        for (const side of [-halfWidth, 0, halfWidth]) {
          const x = Math.floor(at.x + dir.x * k + dir.z * side);
          const y = Math.floor(at.z + dir.z * k + dir.x * side);
          if (x < 0 || y < 0 || x >= base.width || y >= base.height) return out;
          if (blocked(x, y)) return false;
        }
      }
      return true;
    };
    let best: { at: THREE.Vector3; dir: { x: number; z: number }; way: number; route: boolean; cost: number } | null = null;
    for (const dir of [{ x: 1, z: 0 }, { x: -1, z: 0 }, { x: 0, z: 1 }, { x: 0, z: -1 }]) {
      const half = (dir.x ? cw : ch) / 2;
      for (let reach = half + 0.5; reach <= half + 9; reach += 0.5) {
        for (const lane of [0, -1.5, 1.5]) {
          const along = reach + length / 2;
          const at = new THREE.Vector3(base.width / 2 + dir.x * along + dir.z * lane, 0, base.height / 2 + dir.z * along + dir.x * lane);
          // Nearest the caravan, the middle of the track first, and above all with its way out clear.
          let cost = reach + Math.abs(lane) * 1.5;
          if ((best && cost >= best.cost) || !clear(at, dir, -length / 2, length / 2)) continue;
          const way = (dir.x ? base.width : base.height) / 2 - along + length + 2;
          const route = clear(at, dir, length / 2, way + length / 2, true);
          if (!route) cost += 40;
          if (best && cost >= best.cost) continue;
          best = { at, dir, way, route, cost };
        }
      }
    }
    if (!best) {
      this.bus.root.visible = false;
      return;
    }
    this.busWay = best.way;
    this.busRoute = best.route;
    this.bus.park(best.at, Math.atan2(best.dir.x, best.dir.z), new THREE.Vector3(mid.x, 0, mid.z), !this.away);
  }

  /** La Lata going or coming (`run`); a vehicle asked for meanwhile, once it is done. */
  private async drive(run: () => Promise<void>): Promise<void> {
    if (this.driving) await this.driving;
    const going = run();
    this.driving = going;
    try {
      await going;
    } finally {
      this.driving = null;
      const next = this.nextVehicle;
      this.nextVehicle = null;
      if (next !== null) this.setVehicle(next);
    }
  }

  /** La Lata as the campaign has it (vehicleOf: 0 4×4, 1 van, 2 lorry, 3 the bus), parked again once it is built. */
  setVehicle(vehicle: number): void {
    // Not in the middle of its going or coming: after it.
    if (this.driving) {
      this.nextVehicle = vehicle;
      return;
    }
    if (vehicle === this.bus.vehicle) return;
    this.bus.root.removeFromParent();
    const bus = (this.bus = new EvacBus(vehicle));
    this.group.add(bus.root);
    if (this.base) this.parkBus(this.base);
    void bus.loaded().then(() => {
      if (bus !== this.bus || !this.base) return;
      this.parkBus(this.base);
      this.place = null;
      this.relineup();
    });
  }

  /** The lineup as it was, stood again where it now can (the clearing or La Lata changed). */
  private relineup(): void {
    if (!this.lineupKey) return;
    this.lineupKey = '';
    this.setLineup(this.lineupEntries, { mirror: this.mirror, halves: this.halves });
  }

  /**
   * The squad standing by La Lata (null: an empty place); built again only if it changed, [] clears it. In order,
   * left to right as its camera sees them, the first ceil(n / 2) and then the rest; `mirror`, right to left (the
   * second player's screen: each sees their own on their left, by their name, playtest 18/10 #51); `halves` false, one
   * row with no step in its middle (a commander alone: 19/10, #2).
   */
  setLineup(entries: (LineupEntry | null)[], opts: { mirror?: boolean; halves?: boolean } = {}): void {
    const mirror = !!opts.mirror;
    const halves = opts.halves ?? true;
    const key = entries.length ? JSON.stringify([mirror, halves, entries]) : '';
    if (key === this.lineupKey) return;
    this.lineupKey = key;
    this.lineupEntries = entries;
    if (mirror !== this.mirror || halves !== this.halves) this.place = null;
    this.mirror = mirror;
    this.halves = halves;
    for (const o of this.lineup.children) if (o.userData.pad) (o as THREE.Mesh).geometry.dispose();
    this.lineup.clear();
    this.lineupRigs = [];
    this.warm = Math.max(this.warm, 4);
    if (!entries.length) return;
    const place = this.placeLineup(entries.length);
    entries.forEach((entry, i) => {
      const at = place.spots[i]!;
      const facing = Math.atan2(place.cam.x - at.x, place.cam.z - at.z);
      const pad = new THREE.Mesh(new THREE.RingGeometry(0.36, 0.42, 28).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: entry ? new THREE.Color(entry.color) : 0x3a4652, transparent: true, opacity: 0.75, depthWrite: false }));
      pad.position.set(at.x, 0.03, at.z);
      pad.userData.pad = true;
      this.lineup.add(pad);
      if (!entry) return;
      if (entry.status === 'dead') {
        // A cross where they would stand, as tall as a soldier's chest, a candle at its foot.
        const cross = () => {
          const parts = syntyProp('bCross');
          if (!parts) return false;
          const top = parts.reduce((y, part) => Math.max(y, (part.geometry.boundingBox ?? (part.geometry.computeBoundingBox(), part.geometry.boundingBox!)).clone().applyMatrix4(part.matrix).max.y), 0) || 1;
          const k = 0.95 / top;
          return propMeshes('bCross', new THREE.Matrix4().compose(new THREE.Vector3(at.x, 0, at.z), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), facing), new THREE.Vector3(k, k, k)), this.lineup);
        };
        if (!cross()) void syntyPropsLoaded().then(() => this.lineupKey === key && cross());
        const candle = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.12, 0.07).translate(0, 0.06, 0), CANDLE);
        candle.position.set(at.x + Math.sin(facing) * 0.25, 0, at.z + Math.cos(facing) * 0.25);
        this.lineup.add(candle);
        return;
      }
      const rig = buildUnit(entry.template, entry.color, entry.appearance, entry.gear, entry.seed);
      rig.root.position.set(at.x, 0, at.z);
      rig.root.traverse((o) => {
        if (o instanceof THREE.Mesh) o.castShadow = true;
      });
      // Pose state, applied every frame in tick().
      Object.assign(rig.root.userData, { yaw: facing + (i % 3 - 1) * 0.12, phase: i * 0.7, wounded: entry.status === 'wounded', running: 0 });
      this.lineupRigs.push(rig);
      this.lineup.add(rig.root);
    });
  }

  /** The siege's posts: a soldier standing on each tile (its middle), out to the nearest edge; [] clears them. */
  setPosts(posts: { entry: LineupEntry; x: number; y: number }[]): void {
    const key = JSON.stringify(posts);
    if (key === this.postsKey) return;
    this.postsKey = key;
    this.warm = Math.max(this.warm, 4);
    const width = this.base?.width ?? 40;
    const height = this.base?.height ?? 40;
    const kept = new Map<string, UnitRig>();
    posts.forEach((post, i) => {
      const who = JSON.stringify(post.entry);
      const rig = this.postRigs.get(who) ?? buildUnit(post.entry.template, post.entry.color, post.entry.appearance, post.entry.gear, post.entry.seed);
      this.postRigs.delete(who);
      kept.set(who, rig);
      const x = post.x + 0.5;
      const z = post.y + 0.5;
      // Out to the nearest edge: north, east, south or west.
      const gaps = [z, width - x, height - z, x];
      const out = gaps.indexOf(Math.min(...gaps));
      const yaw = [Math.PI, Math.PI / 2, 0, -Math.PI / 2][out]!;
      rig.root.position.set(x, (this.elev[post.y * width + post.x] ?? 0) * STOREY, z);
      rig.root.traverse((o) => {
        if (o instanceof THREE.Mesh) o.castShadow = true;
      });
      Object.assign(rig.root.userData, { yaw: yaw + ((i % 3) - 1) * 0.15, phase: i * 0.9 });
      this.posts.add(rig.root);
    });
    // Those no longer posted go.
    for (const rig of this.postRigs.values()) this.posts.remove(rig.root);
    this.postRigs = kept;
  }

  /** Where soldier `i` of a lineup of `count` stands (for the cards under them). */
  padPosition(i: number, count: number): THREE.Vector3 {
    const at = this.placeLineup(count).spots[i] ?? { x: 0, z: 0 };
    return new THREE.Vector3(at.x, 0, at.z);
  }

  /** The squad's row by La Lata, from out in the clearing. */
  lineupShot(): Shot {
    const place = this.placeLineup(Math.max(1, this.lineupKey ? this.lineupEntries.length : 4));
    return { pos: place.cam.clone(), target: place.target.clone(), fov: 38 };
  }

  /**
   * Where a lineup of `count` stands, and its camera: along either side of La Lata (its doors turned to them) or
   * across the track behind it; each seen from straight out to 50° aside, near enough to fill the frame or a little
   * further. The one where the fewest pines and buildings come between the camera and the row, and nothing stands
   * under the row, the camera least turned. Without La Lata, in front of the caravan.
   */
  private placeLineup(count: number): LineupPlace {
    if (this.place && this.place.count === count && this.place.mirror === this.mirror && this.place.halves === this.halves) return this.place;
    const base = this.base;
    const root = this.bus.root;
    const parked = root.visible || !base;
    const heading = parked ? root.rotation.y : 0;
    const dir = { x: Math.sin(heading), z: Math.cos(heading) };
    const side = { x: dir.z, z: -dir.x };
    // Without La Lata, in front of the Bastion's lot (its yard and building are no place to stand).
    const lot = parked ? null : bastionLot(base);
    const at = parked ? { x: root.position.x, z: root.position.z } : lot ? { x: lot.x + lot.w / 2, z: lot.y + lot.h + 1.5 } : { x: base.width / 2, z: base.height / 2 + 5 };
    const length = parked ? this.bus.length : 0;
    const reach = parked ? this.bus.width / 2 + 0.95 : 0;
    // The Bastion grown out of the caravan stands on its whole footprint (pieceAt only knows the caravan's tiles).
    const footprint = new Set(base && bastionStage(base) > 0 ? bastionFootprint(base).map((c) => c.y * base.width + c.x) : []);
    const height = (x: number, z: number, crowns = true) => {
      if (!base) return 0;
      const tx = Math.floor(x);
      const tz = Math.floor(z);
      if (tx < 0 || tz < 0 || tx >= base.width || tz >= base.height) return 0;
      if (treeAt(base, tx, tz)) return 4.5;
      // A pine's crown spreads over the tiles round its trunk, from a storey or so up (it hid the frame's edge); one
      // stands under it.
      if (crowns) for (const [dx, dz] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]] as const) {
        const nx = tx + dx;
        const nz = tz + dz;
        if (nx >= 0 && nz >= 0 && nx < base.width && nz < base.height && treeAt(base, nx, nz)) return 2.5;
      }
      if (footprint.has(tz * base.width + tx)) return 3;
      const p = pieceAt(base, tx, tz);
      if (!p || p.hp <= 0) return 0;
      const def = PIECES[p.piece];
      return def.kind === 'building' ? (def.tile === 'low' ? 0.8 : 3) : TALL.has(p.piece) ? 2.2 : 0.6;
    };
    const rows = count > LINEUP_ROW ? 2 : 1;
    const front = rows === 2 ? Math.ceil(count / 2) : count;
    const gap = this.halves && count > 1 ? LINEUP_GAP : 0;
    const halfWidth = (rows === 1 ? ((count - 1) / 2) * LINEUP_STEP + gap / 2 : ((front - 1) / 2) * LINEUP_STEP + LINEUP_STEP / 2) + 0.7;
    // Along either side (also moved up towards the nose, away from the Bastion's lot behind it: a long row went past
    // the tail onto its yard), or across the track at its tail.
    const candidates: { centre: { x: number; z: number }; toward: { x: number; z: number }; k: number; shift: number }[] = [];
    for (const shift of [0, 0.6, 1.2]) {
      candidates.push(
        { centre: { x: at.x + side.x * reach + dir.x * shift, z: at.z + side.z * reach + dir.z * shift }, toward: side, k: 0, shift },
        { centre: { x: at.x - side.x * reach + dir.x * shift, z: at.z - side.z * reach + dir.z * shift }, toward: { x: -side.x, z: -side.z }, k: 1, shift },
      );
    }
    candidates.push({ centre: { x: at.x - dir.x * (length / 2 + 1.3), z: at.z - dir.z * (length / 2 + 1.3) }, toward: { x: -dir.x, z: -dir.z }, k: 2, shift: 0 });
    let best: LineupPlace | null = null;
    let bestCost = Infinity;
    candidates.forEach((c) => {
      const k = c.k;
      // Left to right as the camera sees them (right to left, mirrored); the second row a step behind, between the first's.
      const right = this.mirror ? { x: -c.toward.z, z: c.toward.x } : { x: c.toward.z, z: -c.toward.x };
      const spots: { x: number; z: number }[] = [];
      for (let i = 0; i < count; i++) {
        let u: number;
        let v = 0;
        if (rows === 1) u = (i - (count - 1) / 2) * LINEUP_STEP + (i < Math.ceil(count / 2) ? -gap / 2 : gap / 2);
        else {
          const back = i >= front ? 1 : 0;
          const n = back ? count - front : front;
          u = ((back ? i - front : i) - (n - 1) / 2) * LINEUP_STEP + back * (LINEUP_STEP / 2);
          v = -back * 0.95;
        }
        spots.push({ x: c.centre.x + right.x * u + c.toward.x * v, z: c.centre.z + right.z * u + c.toward.z * v });
      }
      // Nobody stands on anything built, a pine, the Bastion or its yard (its lot), nor off the clearing.
      let standing = (k === 2 ? 0.5 : 0) + c.shift * 2;
      for (const p of spots) if (height(p.x, p.z, false) > 0 || (base && (onBastionLot(base, Math.floor(p.x), Math.floor(p.z)) || p.x < 0 || p.z < 0 || p.x >= base.width || p.z >= base.height))) standing += 20;
      const near = Math.max(5.5, halfWidth / 0.42);
      const middle = { x: c.centre.x - c.toward.x * (rows - 1) * 0.45, z: c.centre.z - c.toward.z * (rows - 1) * 0.45 };
      for (const a of [0, 0.3, -0.3, 0.6, -0.6, 0.9, -0.9]) {
        const d = { x: c.toward.x * Math.cos(a) - c.toward.z * Math.sin(a), z: c.toward.x * Math.sin(a) + c.toward.z * Math.cos(a) };
        for (const [dist, lift] of [near, near + 1.5, near + 3].flatMap((d) => [1, 1.5, 2].map((k) => [d, k] as const))) {
          // From higher up, over the pines, rather than turned (a little steeper is better than a row seen askew).
          const y = (1.8 + dist * 0.3) * lift;
          const cam = { x: middle.x + d.x * dist, z: middle.z + d.z * dist };
          // Seen from straight out unless the way is blocked (19/10, #2: turned, the row ran off across the frame).
          let cost = standing + Math.abs(a) * 25 + (dist - near) * 0.4 + (lift - 1) * 3;
          if (height(cam.x, cam.z) > y - 0.6) cost += 60;
          // What comes between the camera and the row's middle, and (half as much: at the frame's edge) its ends.
          const out = (k: number) => ({ x: middle.x + right.x * halfWidth * k, z: middle.z + right.z * halfWidth * k });
          for (const [q, weight] of [[middle, 6], [out(1), 3], [out(-1), 3]] as const) {
            for (let t = 0.8; t < dist; t += 0.4) {
              const f = t / dist;
              if (height(q.x + (cam.x - q.x) * f, q.z + (cam.z - q.z) * f) > 0.9 + (y - 0.9) * f) cost += weight;
            }
          }
          if (cost < bestCost) {
            bestCost = cost;
            best = { count, mirror: this.mirror, halves: this.halves, spots, toward: c.toward, cam: new THREE.Vector3(cam.x, y, cam.z), target: new THREE.Vector3(middle.x, 0.5, middle.z) };
          }
        }
      }
    });
    const place = best as unknown as LineupPlace;
    // La Lata's doors to the squad.
    if (parked && root.visible) this.bus.park(root.position.clone(), heading, new THREE.Vector3(place.spots[0]!.x, 0, place.spots[0]!.z).lerp(new THREE.Vector3(place.spots[count - 1]!.x, 0, place.spots[count - 1]!.z), 0.5));
    return (this.place = place);
  }

  /** The squad in the row runs to La Lata's doors, one after another, and climbs in; resolves once all are aboard. */
  private async board(): Promise<void> {
    await this.bus.loaded();
    const rigs = this.lineupRigs.filter((r) => r.root.visible);
    if (!rigs.length) return;
    const root = this.bus.root;
    const centre = root.position.clone();
    const dir = new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y));
    const across = new THREE.Vector3(dir.z, 0, -dir.x);
    const doors = this.bus.doorways();
    const half = this.bus.length / 2;
    const plans = rigs.map((rig, i) => {
      const from = rig.root.position.clone();
      const door = doors.reduce((a, b) => (a.distanceTo(from) <= b.distanceTo(from) ? a : b));
      const offset = door.clone().sub(centre);
      // A door on its side (the bus's, the 4×4's) or at its back (the van's, the lorry's): at its very end, the back (a
      // side's door near the end counted as a back one, and the runners went round the far corner to it, through the
      // rest of the row).
      const back = Math.abs(offset.dot(dir)) > half - 0.35;
      const normal = back ? dir.clone().multiplyScalar(Math.sign(offset.dot(dir)) || -1) : across.clone().multiplyScalar(Math.sign(offset.dot(across)) || 1);
      const foot = door.clone().addScaledVector(normal, 0.55);
      // In through the doorway itself: up to its plane, not into the body (19/10, #11: they ran into it and through it).
      const inside = door.clone().addScaledVector(normal, 0.08);
      // Round its corner first, from behind it to a side door, or from its side to its back.
      const path = [from];
      const behind = from.clone().sub(centre).dot(dir) < -half;
      const side = Math.sign(from.clone().sub(centre).dot(across)) || 1;
      if (behind && !back) path.push(centre.clone().addScaledVector(dir, -half - 0.4).addScaledVector(normal, this.bus.width / 2 + 0.6));
      if (!behind && back) path.push(centre.clone().addScaledVector(dir, -half - 0.4).addScaledVector(across, side * (this.bus.width / 2 + 0.6)));
      path.push(foot, inside);
      // The run to the door's foot; then the climb in, from the foot to the doorway.
      const run = path.slice(0, -1);
      const lengths = run.slice(1).map((p, k) => p.distanceTo(run[k]!));
      const total = lengths.reduce((a, b) => a + b, 0);
      // When it would get to the door's foot, setting off at once.
      const atFoot = total / BOARD_SPEED;
      // A double doorway's two leaves are one way in: one at a time through it.
      const way = doors.findIndex((d) => d.distanceTo(door) < 0.7);
      return { rig, run, lengths, total, foot, inside, door: way, atFoot, delay: 0, running: total / BOARD_SPEED };
    });
    // One at a time at each door, the nearest first (out of the way of those behind, who run past where it stood):
    // each held back at its place in the row until the one before it has climbed in (they all made for the same
    // spot at once, a seventh of a second apart, and climbed in through one another).
    for (let d = 0; d < doors.length; d++) {
      let free = -Infinity;
      for (const p of plans.filter((q) => q.door === d).sort((a, b) => a.atFoot - b.atFoot)) {
        p.delay = Math.max(0, free - p.atFoot);
        p.atFoot += p.delay;
        free = p.atFoot + CLIMB + DOOR_GAP;
      }
    }
    const seconds = Math.max(...plans.map((p) => p.delay + p.running + CLIMB)) + 0.05;
    await tween(seconds, (t) => {
      const now = t * seconds;
      // Their shadows run with them.
      this.warm = Math.max(this.warm, 1);
      for (const p of plans) {
        const since = now - p.delay;
        const d = p.rig.root.userData;
        const climbing = since >= p.running;
        d.running = since > 0 && !climbing ? 1 : 0;
        p.rig.root.visible = since < p.running + CLIMB;
        if (since <= 0) continue;
        if (climbing) {
          // At the door, turned to it, up and in: a little lower as it goes, gone at the doorway.
          const c = Math.min(1, (since - p.running) / CLIMB);
          p.rig.root.position.lerpVectors(p.foot, p.inside, c * c);
          p.rig.root.position.y = -0.18 * c;
          d.yaw = Math.atan2(p.inside.x - p.foot.x, p.inside.z - p.foot.z);
          continue;
        }
        let left = (since / p.running) * p.total;
        for (let s = 0; s < p.lengths.length; s++) {
          const a = p.run[s]!;
          const b = p.run[s + 1]!;
          if (left <= p.lengths[s]! || s === p.lengths.length - 1) {
            p.rig.root.position.lerpVectors(a, b, Math.min(1, left / (p.lengths[s]! || 1)));
            d.yaw = Math.atan2(b.x - a.x, b.z - a.z);
            break;
          }
          left -= p.lengths[s]!;
        }
      }
    });
  }

  /** The squad aboard (if it is lined up), then La Lata away down its track, out of the clearing; resolves once it is gone. */
  depart(): Promise<void> {
    return this.drive(async () => {
      if (this.lineupRigs.length && this.bus.root.visible) await this.board();
      for (const o of this.lineup.children) if (o.userData.pad) o.visible = false;
      await this.bus.depart(this.busWay, !this.busRoute);
      this.away = true;
    });
  }

  /** La Lata backed in along its track to stand by the caravan again (faded in, its track built over); resolves once it stands. */
  arrive(): Promise<void> {
    return this.drive(async () => {
      this.away = false;
      await this.bus.arrive(this.busWay, !this.busRoute);
    });
  }

  /** The sun and its shadow camera over the whole clearing, from where the battle has it. */
  private aim(base: BaseState): void {
    const cx = base.width / 2;
    const cz = base.height / 2;
    const from = this.look.sunFrom;
    this.sun.position.set(cx + from[0], from[1], cz + from[2]);
    this.sun.target.position.set(cx, 0, cz);
    const r = Math.hypot(base.width, base.height) / 2 + 2;
    Object.assign(this.sun.shadow.camera, { left: -r, right: r, top: r, bottom: -r, near: 1, far: Math.hypot(...from) + r * 2 });
    this.sun.shadow.camera.updateProjectionMatrix();
    this.fill.position.set(cx - from[0], from[1] * 0.6, cz - from[2]);
    this.fill.target.position.set(cx, 0, cz);
  }

  pieceModel(piece: PieceId, rot: number, level = 1): THREE.Object3D {
    return pieceModel(piece, rot, level);
  }

  tick(dt: number, time = performance.now() / 1000): void {
    this.map.tick(dt);
    // The soldier looked at turns slowly, at rest, as on the old pedestal.
    if (this.showcaseRig) {
      this.showcaseRig.root.rotation.y += dt * 0.35;
      if (isFigure(this.showcaseRig)) this.showcaseRig.applyPose(IDLE_POSE, time);
    }
    // At their posts, at rest.
    for (const rig of this.postRigs.values()) if (isFigure(rig)) rig.applyPose({ ...IDLE_POSE, yaw: rig.root.userData.yaw, phase: rig.root.userData.phase }, time);
    // The squad breathes in line, the wounded slumped, and runs to board.
    for (const rig of this.lineupRigs) {
      const d = rig.root.userData;
      if (isFigure(rig)) rig.applyPose({ ...IDLE_POSE, yaw: d.yaw, phase: d.phase, run: d.running, crouch: d.wounded && !d.running ? 0.4 : 0 }, time);
    }
  }

  beforeRender(gl: THREE.WebGLRenderer): void {
    if (runMapJobs(gl) || this.warm > 0) gl.shadowMap.needsUpdate = true;
    if (this.warm > 0) this.warm--;
  }
}

/** The clearing as a battlefield with nobody on it: the map its siege would be fought on (under snow in winter). */
function clearingState(base: BaseState, snow: boolean): GameState {
  const s = createMission({ map: { kind: 'base', base }, slots: [0], rng: new Rng(1), difficulty: 1, ...(snow ? { snow: true } : {}) });
  s.units = [];
  delete s.traps;
  if (s.base) s.base.felled = base.felled ?? [];
  return s;
}

const ghostMaterial = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.8 });

/** A lineup: where each stands, which way they face (to its camera), and the camera. */
interface LineupPlace {
  count: number;
  mirror: boolean;
  halves: boolean;
  spots: { x: number; z: number }[];
  toward: { x: number; z: number };
  cam: THREE.Vector3;
  target: THREE.Vector3;
}

/** A lineup's spacing (tiles), the gap between the two commanders' halves, and how many stand in one row. */
const LINEUP_STEP = 0.95;
const LINEUP_GAP = 0.5;
const LINEUP_ROW = 8;
/** How fast the squad runs to board La Lata (tiles a second). */
const BOARD_SPEED = 3.4;
/** How long climbing in takes, from the door's foot (seconds), and how long after one the next gets to that door. */
const CLIMB = 0.45;
const DOOR_GAP = 0.15;
const CANDLE = new THREE.MeshStandardMaterial({ color: 0x2a1a00, emissive: 0xffb050, emissiveIntensity: 2.2 });

/** The pieces that stand tall enough to hide a soldier from a camera a storey up (but the buildings, by their tiles). */
const TALL = new Set<PieceId>(['palisade', 'wall', 'gate', 'tower', 'floodlight']);

/** Each pack's atlas as a material, for props put on their own (the Armería's corner). */
const atlasMaterials = new Map<AtlasId, THREE.MeshStandardMaterial>();

/** A prop's meshes at `m` into `into`, on its pack's atlas; false if its model is still loading. */
function propMeshes(id: PropId, m: THREE.Matrix4, into: THREE.Group): boolean {
  const parts = syntyProp(id);
  if (!parts) return false;
  for (const part of parts) {
    let material = atlasMaterials.get(part.atlas);
    if (!material) atlasMaterials.set(part.atlas, (material = new THREE.MeshStandardMaterial({ map: atlasTexture(part.atlas), roughness: 0.85 })));
    const mesh = new THREE.Mesh(part.geometry, material);
    mesh.matrixAutoUpdate = false;
    mesh.matrix.copy(m).multiply(part.matrix);
    mesh.castShadow = mesh.receiveShadow = true;
    into.add(mesh);
  }
  return true;
}

/**
 * A piece alone (for the one about to be built): its models as basePieces
 * draws them, turned `rot`, centred on its footprint at the origin. Its
 * materials are for the caller to change; if its models are still loading,
 * they come in later with the material in its `userData.material` (else the
 * first mesh's).
 */
function pieceModel(id: PieceId, rot: number, level: number): THREE.Object3D {
  const out = new THREE.Group();
  let missing = false;
  const fill = () => {
    missing = false;
    out.clear();
    const material = (out.userData.material as THREE.Material | undefined) ?? firstMaterial(out) ?? ghostMaterial;
    out.add(pieceGroup(id, rot, level, material, () => (missing = true)));
  };
  fill();
  if (missing) void syntyPropsLoaded().then(() => {
    const material = (out.userData.material as THREE.Material | undefined) ?? firstMaterial(out);
    if (material) out.userData.material = material;
    fill();
  });
  return out;
}

function firstMaterial(o: THREE.Object3D): THREE.Material | undefined {
  let found: THREE.Material | undefined;
  o.traverse((c) => {
    if (!found && c instanceof THREE.Mesh) found = c.material as THREE.Material;
  });
  return found;
}

/** A piece's models round its footprint's middle, in `material`; `lacking` if some are still loading. */
function pieceGroup(id: PieceId, rot: number, level: number, material: THREE.Material, lacking: () => void): THREE.Object3D {
  const def = PIECES[id];
  const [w, h] = rot % 2 ? [def.size[1], def.size[0]] : def.size;
  const width = w + 2;
  const height = h + 2;
  const piece: BasePiece = { id: 'ghost', piece: id, x: 1, y: 1, rot, hp: def.hp, ...(def.kind === 'building' ? { level } : {}), ...(def.charges ? { charges: def.charges } : {}) };
  const s = { width, height, tiles: new Array(width * height).fill('floor'), elev: new Array(width * height).fill(0), units: [], base: { pieces: [piece] }, mission: {} } as unknown as GameState;
  const map = pieceMap(s)!;
  const spot = map.spots[0]!;
  const inner = new THREE.Group();
  inner.position.set(-spot.x, 0, -spot.z);
  const box = (pid: PropId) => {
    const out = new THREE.Box3();
    for (const part of syntyProp(pid) ?? []) out.union((part.geometry.boundingBox ?? (part.geometry.computeBoundingBox(), part.geometry.boundingBox!)).clone().applyMatrix4(part.matrix));
    return out;
  };
  const art: PieceArt = {
    put(pid, m) {
      const parts = syntyProp(pid);
      if (!parts) lacking();
      for (const part of parts ?? []) {
        const mesh = new THREE.Mesh(part.geometry, material);
        mesh.matrixAutoUpdate = false;
        mesh.matrix.copy(m).multiply(part.matrix);
        inner.add(mesh);
      }
    },
    top: (pid) => box(pid).max.y || 0.3,
    size: (pid) => (box(pid).isEmpty() ? new THREE.Vector3(1, 1, 1) : box(pid).getSize(new THREE.Vector3())),
  };
  // Sandbags are the battle map's own (mapView, as a siege's barricade's); alone, a wall of them along its line.
  if (id === 'sandbags') art.put('sandbags', new THREE.Matrix4().compose(new THREE.Vector3(spot.x, 0, spot.z), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), rot % 2 ? 0 : Math.PI / 2), new THREE.Vector3(1, 1, 1)));
  else drawPiece(s, map, spot, 0, art);
  return inner;
}
