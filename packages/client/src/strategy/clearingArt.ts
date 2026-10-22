import * as THREE from 'three';
import { bastionFootprint, bastionLot, bastionStage, cellsOf, createMission, onBastionLot, pieceAt, PIECES, Rng, treeAt, type Appearance, type BasePiece, type BaseState, type GameState, type ItemId, type PieceId, type TemplateId, type WeaponModId } from '@bastion/engine';
import { drawPiece, pieceMap, pieceWear, type PieceArt } from '../game/basePieces';
import { EvacBus } from '../game/evacBus';
import { fxShape } from '../game/fx';
import { MapView } from '../game/mapView';
import { dayMood, runMapJobs } from '../game/renderer';
import { graphics } from '../settings';
import { syntyProp, syntyPropsLoaded } from '../game/syntyProps';
import { WEAPON_PARTS, WEAPONS, type AtlasId, type CampMove, type PropId, type WeaponId, type WeaponPartId } from '../game/syntyManifest';
import { foundModel } from '../game/syntyFigures';
import type { Gear } from '../game/figures';
import { buildUnit, IDLE_POSE, isFigure, type UnitRig } from '../game/models';
import { ClearingLife, type Camp } from './clearingLife';
import { STOREY } from '../game/coords';
import { atlasTexture, loadModel } from '../game/synty';
import { tween } from '../game/tween';
import { whenCampClips } from '../game/syntyAnims';
import type { ArmoryStock, ClearingArt } from './clearing';
import { Sky } from '../game/sky';
import { type Shot, type StageMood } from './stage';

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
  /** The stage's camera (watch): the boarding moves it itself, a shot of its own (playtest 21/10 #3). */
  private camera: THREE.PerspectiveCamera | null = null;
  /** Where the boarding's camera started (the row's shot): it goes back out there as La Lata leaves. */
  private boardFrom: THREE.Vector3 | null = null;
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
  /** Where La Lata stands parked (parkBus): its middle, its heading, half its length and width; null nowhere. */
  private busBox: { x: number; z: number; dx: number; dz: number; half: number; halfWidth: number } | null = null;
  /** What the Armería holds (setArmory), and what it was filled with last. */
  private stock: ArmoryStock = { weapons: [], items: {}, mods: {} };
  private stocked = '';
  /** What fillArmory was last called with, to fill it again when the stock changes. */
  private armoryArgs: { base: BaseState; free: (x: number, z: number) => boolean } | null = null;
  /** Frames left with the shadow map drawn whatever (the first ones, while the props come in). */
  private warm = 30;
  /**
   * The squad by La Lata (playtest 18/10, #44: choosing it and the debrief, on the clearing): in a row along the
   * bus's side or across the track at its tail, whichever its camera sees most clearly (placeLineup); one
   * commander's half and then the other's, in two rows past eight. The fallen leave a cross with a candle.
   */
  private readonly lineup = new THREE.Group();
  private lineupRigs: UnitRig[] = [];
  /**
   * The lineup's figures by who each is (their entry), kept from one lineup to the next: built again, the whole row
   * hopped back into its idle at every change of the campaign (pressing «Lanzar misión»: playtest 21/10 #2).
   */
  private lineupByWho = new Map<string, UnitRig>();
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
  /** The sky round the clearing (sky.ts): the valley's mountains. */
  private readonly skyDome = new Sky('country');
  private postRigs = new Map<string, UnitRig>();
  private postsKey = '';
  /** The soldiers out about the clearing while the Asentamiento is open (playtest 22/10 #1: clearingLife.ts). */
  private readonly life = new ClearingLife();
  /** The fires lit (basePieces.ts's fire pits are cold stones): a few licks of flame over each. */
  private readonly flames = new CampFlames();
  private lifeEntries: LineupEntry[] = [];

  constructor(snow = false) {
    this.snow = snow;
    const m = (this.look = dayMood(snow));
    this.mood = { exposure: m.exposure, bloom: m.bloom, grade: m.grade };
    // Nothing is played beyond the woods: past the edge the pinar goes on, lit, thinning out (mapView: playtest 21/10
    // #27), into the haze under the mountains, not the dark.
    this.map.litBeyond(true);
    this.map.fadeBeyond(8, 30);
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
    this.group.add(this.map.group, this.bus.root, this.showcase, this.armoryCorner, this.lineup, this.posts, this.life.group, this.flames.group, this.sun, this.sun.target, this.sky, this.fill, this.fill.target);
  }

  /** The soldiers out about the clearing at their ease (the Asentamiento's; an empty list clears them). */
  setLife(entries: LineupEntry[]): void {
    this.lifeEntries = entries;
    this.relife();
  }

  /** Those about the clearing but the squad standing by La Lata (the same soldier was in both). */
  private relife(): void {
    const inRow = new Set(this.lineupEntries.map((e) => e?.seed).filter((s) => s));
    this.life.set(this.lifeEntries.filter((e) => !e.seed || !inRow.has(e.seed)));
    this.warm = Math.max(this.warm, 4);
  }

  /** What is being placed on the clearing (its tiles; null nothing): those about it walk off it. */
  keepClear(cells: { x: number; y: number }[] | null): void {
    this.life.keepClear(cells);
  }

  /** Whether those about the clearing keep off (x, z): La Lata with a step round it, the Armería's corner, Soldados' place. */
  private lifeTaken(x: number, z: number): boolean {
    const root = this.bus.root;
    if (root.visible && !this.away) {
      const dx = x - root.position.x;
      const dz = z - root.position.z;
      const along = dx * Math.sin(root.rotation.y) + dz * Math.cos(root.rotation.y);
      const across = dx * Math.cos(root.rotation.y) - dz * Math.sin(root.rotation.y);
      if (Math.abs(along) < this.bus.length / 2 + 1 && Math.abs(across) < this.bus.width / 2 + 1) return true;
    }
    return Math.hypot(x - this.armoryAt.x, z - this.armoryAt.z) < 2 || Math.hypot(x - this.soldierAt.x, z - this.soldierAt.z) < 1.5;
  }

  /** The sky and the haze of the battle on `scene` (the set's): its lights are this group's; the mountains round the valley. */
  dress(scene: THREE.Scene): void {
    scene.background = new THREE.Color(this.look.haze);
    scene.fog = new THREE.FogExp2(this.look.haze, this.look.hazeDensity);
    this.skyDome.setMood(this.look, { night: false, snow: this.snow });
    scene.add(this.skyDome.group);
  }

  /** The camera the clearing is seen with, for what the map does by it (its lamps, what fades). */
  watch(camera: THREE.Camera): void {
    if (camera instanceof THREE.PerspectiveCamera) this.camera = camera;
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
    // The squad stands again where it now can (and the Armería's corner keeps out of its way: campPlaces).
    this.place = null;
    this.campPlaces(base);
    this.relineup();
    this.life.setBase(base, (x, z) => this.lifeTaken(x, z));
    const camp = () => {
      const c = this.campOf(s);
      this.life.setCamp(c);
      this.flames.set(c.fires);
    };
    camp();
    // Again once its props are in: what stands about the fires is measured by their models.
    void syntyPropsLoaded().then(() => {
      if (this.base === base) camp();
    });
  }

  /**
   * The clearing's camp for those about it (clearingLife.ts Camp): the fires and camp chairs basePieces.ts draws
   * (its pieces drawn again, only noted: where each prop stands), and places along La Lata's long sides to lean at,
   * off the squad's row.
   */
  private campOf(s: GameState): Camp {
    const camp: Camp = { fires: [], seats: [], things: [], walls: [] };
    const map = pieceMap(s);
    if (map) {
      const at = new THREE.Vector3();
      const turn = new THREE.Quaternion();
      const scale = new THREE.Vector3();
      const yaw = new THREE.Euler();
      const art: PieceArt = {
        put(id, m) {
          m.decompose(at, turn, scale);
          if (id === 'bFirePit') camp.fires.push({ x: at.x, z: at.z });
          else if (id === 'bCampChair') camp.seats.push({ x: at.x, z: at.z, yaw: yaw.setFromQuaternion(turn, 'YXZ').y });
          else {
            const box = propBox(id);
            if (!box.isEmpty()) camp.things.push({ inverse: m.clone().invert(), min: box.min, max: box.max });
          }
        },
        top: (id) => propBox(id).max.y || 0.3,
        size: (id) => (propBox(id).isEmpty() ? new THREE.Vector3(1, 1, 1) : propBox(id).getSize(new THREE.Vector3())),
      };
      for (const spot of map.spots) drawPiece(s, map, spot, pieceWear(s, spot), art);
    }
    // La Lata's long sides, a shoulder to it, a quarter of its length from its middle either way.
    const root = this.bus.root;
    if (root.visible && !this.away) {
      const dir = { x: Math.sin(root.rotation.y), z: Math.cos(root.rotation.y) };
      const across = { x: dir.z, z: -dir.x };
      const row = this.place?.spots ?? [];
      for (const side of [1, -1] as const) {
        for (const along of LEAN_ALONG) {
          const k = along * this.bus.length;
          const out = side * (this.bus.width / 2 + LEAN_OFF);
          const p = { x: root.position.x + dir.x * k + across.x * out, z: root.position.z + dir.z * k + across.z * out };
          if (row.some((q) => Math.hypot(q.x - p.x, q.z - p.z) < 1.4)) continue;
          // Facing along it with it on the left (side 1, `across` to its left) or the right.
          const face = Math.atan2(dir.x, dir.z) + (along > 0 ? 0 : Math.PI);
          const left = { x: Math.cos(face), z: -Math.sin(face) };
          const wallSide = left.x * across.x * side + left.z * across.z * side < 0 ? 1 : -1;
          camp.walls.push({ ...p, yaw: face, side: wallSide });
        }
      }
    }
    return camp;
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
    /** On La Lata parked, `margin` round it (20/10 #29: the Armería's camera was inside the 4×4, the bench behind it). */
    const bus = this.busBox;
    const inBus = (x: number, z: number, margin: number) => {
      if (!bus) return false;
      const ox = x - bus.x;
      const oz = z - bus.z;
      return Math.abs(ox * bus.dx + oz * bus.dz) <= bus.half + margin && Math.abs(ox * bus.dz - oz * bus.dx) <= bus.halfWidth + margin;
    };
    // Off the Bastion's lot (its building and its yard) as off anything built, and off La Lata.
    const free = (x: number, z: number) => {
      const tx = Math.floor(x);
      const tz = Math.floor(z);
      return tx >= 0 && tz >= 0 && tx < base.width && tz < base.height && !pieceAt(base, tx, tz) && !treeAt(base, tx, tz) && !onBastionLot(base, tx, tz) && !inBus(x, z, 0.7);
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
      if (inBus(x, z, 0.3)) return true;
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
    // The Armería's corner: at an end of the caravan (along its length), else by one of its long sides, to either hand
    // (CORNER_SHIFTS) or further out; as near the caravan as the whole corner has room (CORNER_SPOTS); of those, the one
    // the camera sees most clearly (playtest 0.8.4: a Huerto over the end left it nowhere, and its camera, never
    // moved, on the Huerto; after a reload, in the pines at the map's corner).
    const along = half.x >= half.z ? { x: 1, z: 0 } : { x: 0, z: 1 };
    const ways = [along, { x: -along.x, z: -along.z }, { x: along.z, z: along.x }, { x: -along.z, z: -along.x }];
    // Clear of Soldados' soldier too, and of the way from its camera to it (playtest 0.8.4: the bench in front of it).
    const soldier = this.soldierAt;
    const shot = this.soldierView;
    const inShot = (px: number, pz: number) => {
      const reach = shot.dist + 1;
      const t = THREE.MathUtils.clamp((px - soldier.x) * shot.dir.x + (pz - soldier.z) * shot.dir.z, 0, reach);
      return Math.hypot(px - soldier.x - shot.dir.x * t, pz - soldier.z - shot.dir.z * t) <= SOLDIER_ROOM;
    };
    // And of the squad's row by La Lata and what its camera sees of it (UI, 0.8.4: the bench at the frame's edge), as
    // many as it may be.
    const row = this.placeLineup(Math.max(ROW_GUESS, this.lineupKey ? this.lineupEntries.length : 0));
    const cam = { x: row.cam.x, z: row.cam.z };
    const ends = [row.spots[0]!, row.spots[Math.min(row.spots.length - 1, row.count > LINEUP_ROW ? Math.ceil(row.count / 2) - 1 : row.count - 1)]!];
    // Its frame: from its camera out past either end, and on behind the row (what stands there is its background).
    const beyond = ends.map((e) => {
      const d = Math.hypot(e.x - cam.x, e.z - cam.z) || 1;
      return { x: e.x + ((e.x - cam.x) / d) * ROW_BEHIND, z: e.z + ((e.z - cam.z) / d) * ROW_BEHIND };
    });
    const inRow = (px: number, pz: number) =>
      row.spots.some((q) => Math.hypot(px - q.x, pz - q.z) <= SOLDIER_ROOM) ||
      nearTriangle(px, pz, cam, beyond[0]!, beyond[1]!, ROW_ROOM);
    const room = (x: number, z: number, out: { x: number; z: number }) =>
      CORNER_SPOTS.every(([u, v]) => {
        const [px, pz] = [x + out.x * v - out.z * u, z + out.z * v + out.x * u];
        return free(px, pz) && !inShot(px, pz) && !inRow(px, pz);
      });
    let best: { x: number; z: number; out: { x: number; z: number }; dir: { x: number; z: number }; dist: number; cost: number } | null = null;
    // Room for the whole corner; with none anywhere, for its bench at least (the camera never left where it was).
    for (const fits of [room, (x: number, z: number) => free(x, z)]) {
      ways.forEach((out, k) => {
        const edge = out.x ? half.x : half.z;
        for (const shift of CORNER_SHIFTS) {
          for (let reach = edge + 1.1; reach <= edge + CORNER_REACH; reach += 0.5) {
            const x = mid.x + out.x * reach - out.z * shift;
            const z = mid.z + out.z * reach + out.x * shift;
            if (!fits(x, z, out)) continue;
            for (const lean of [1, -1]) {
              const side = { x: -out.z * lean, z: out.x * lean };
              const length = Math.hypot(3.2, 1.4);
              // As far as the shot may go back to keep the corner whole (its fit).
              const v = view(x - out.x * 0.2, z - out.z * 0.2, { x: (out.x * 3.2 + side.x * 1.4) / length, z: (out.z * 3.2 + side.z * 1.4) / length }, 5);
              // Its own camera not among the squad, nor the squad before it.
              const camera = { x: x + v.dir.x * v.dist, z: z + v.dir.z * v.dist };
              const crowded = row.spots.some((q) => {
                const t = THREE.MathUtils.clamp((q.x - x) * v.dir.x + (q.z - z) * v.dir.z, 0, v.dist);
                return Math.hypot(q.x - x - v.dir.x * t, q.z - z - v.dir.z * t) < 0.9 || Math.hypot(q.x - camera.x, q.z - camera.z) < 1.2;
              });
              const cost = v.cost + (crowded ? 30 : 0) + (k < 2 ? 0 : 1.5) + Math.abs(shift) * 0.3 + (reach - edge - 1.1) * 0.2;
              if (!best || cost < best.cost) best = { x, z, out, dir: v.dir, dist: v.dist, cost };
            }
            break;
          }
        }
      });
      if (best) break;
    }
    if (!best) return;
    const b = best as { x: number; z: number; out: { x: number; z: number }; dir: { x: number; z: number }; dist: number };
    this.armoryAt = { x: b.x, z: b.z, out: b.out };
    this.armoryView = { dir: b.dir, dist: b.dist };
    this.fillArmory(base, free);
  }

  /**
   * The Armería's corner at armoryAt, by the caravan, at its end if there is room (playtest 20/10 #29, Ana: a table and boxes with what
   * there is, near empty at first): the bench, and on it what is bought and found (setArmory: the grenades, smoke and
   * flash ones, the first-aid kits, boxes of the special rounds, the improvements not yet mounted); the rack with the
   * weapons found, the rest in a crate open beside it once the rack is full; an ammunition crate, its lid off.
   */
  private fillArmory(base: BaseState, free: (x: number, z: number) => boolean): void {
    this.armoryArgs = { base, free };
    this.stocked = JSON.stringify(this.stock);
    this.armoryCorner.clear();
    const { x, z, out } = this.armoryAt;
    const side = { x: -out.z, z: out.x };
    const yaw = Math.atan2(out.x, out.z);
    let lacking = false;
    const at = (u: number, v: number, y: number, turn: number, k: number) =>
      new THREE.Matrix4().compose(new THREE.Vector3(x + out.x * v + side.x * u, y, z + out.z * v + side.z * u), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), yaw + turn), new THREE.Vector3(k, k, k));
    const put = (id: PropId, u: number, v: number, turn = 0, k = 1, y = 0) => {
      if (!free(x + out.x * v + side.x * u, z + out.z * v + side.z * u)) return;
      if (!propMeshes(id, at(u, v, y, turn, k), this.armoryCorner)) lacking = true;
    };
    // (On the corner's own tile, out from the caravan's: a step nearer it is the caravan's.)
    // Its long side across the camera's view, its board to the caravan.
    put('bWorkbench', 0, 0, -Math.PI / 2, 0.8);
    put('bToolbox', 0.6, 0.45, 0.4, 0.9);
    put('gunRack', -1.0, -0.05, Math.PI, 0.75);
    put('armAmmoCrate', 1.0, 0.3, 0.25, 0.55);
    put('armAmmoCrateLid', 1.3, 0.65, 1.1, 0.55);
    // On the bench: in rows along its top, each thing as many times as there are (a few at most).
    const top = BENCH_TOP * 0.8;
    const items = this.stock.items;
    const onBench: PropId[] = [
      ...Array<PropId>(Math.min(4, items.fragGrenade ?? 0)).fill('armGrenade'),
      ...Array<PropId>(Math.min(3, items.smokeGrenade ?? 0)).fill('armSmoke'),
      ...Array<PropId>(Math.min(3, items.flashbang ?? 0)).fill('armFlashbang'),
      ...Array<PropId>(Math.min(2, items.firstAid ?? 0)).fill('clinicalKit'),
      ...Array<PropId>(Math.min(2, items.apRounds ?? 0)).fill('armAmmo556'),
      ...Array<PropId>(Math.min(2, items.tracerRounds ?? 0)).fill('armAmmo762'),
    ];
    onBench.forEach((id, k) => {
      const row = k % 2;
      const col = Math.floor(k / 2);
      put(id, -0.45 + col * 0.13 + row * 0.06, -0.06 + row * 0.14, (k * 1.7) % 1.2 - 0.6, 1, top);
    });
    // The improvements not yet mounted: a sight, a silencer, lying at the bench's end.
    const parts: WeaponPartId[] = [...((this.stock.mods.scope ?? 0) > 0 ? (['scope2'] as const) : []), ...((this.stock.mods.silencer ?? 0) > 0 ? (['silencer2'] as const) : [])];
    parts.forEach((part, k) => {
      void loadModel(WEAPON_PARTS[part].pack, 'wep', part).then((model) => {
        const g = model.clone();
        g.applyMatrix4(at(0.4 - k * 0.14, 0.05, top + 0.02, Math.PI / 2, 1));
        this.armoryCorner.add(g);
      }).catch(() => undefined);
    });
    // The weapons found: in the rack, standing, a little apart; the rest laid in a crate open by it.
    const found = this.stock.weapons.map((kind) => foundModel(kind));
    const gun = (id: WeaponId, place: (g: THREE.Object3D) => void) =>
      void loadModel(WEAPONS[id].pack, 'wep', id).then((model) => {
        const g = model.clone();
        place(g);
        g.traverse((o) => {
          if (o instanceof THREE.Mesh) o.castShadow = true;
        });
        this.armoryCorner.add(g);
      }).catch(() => undefined);
    const rack = { x: x + out.x * -0.05 + side.x * -1.0, z: z + out.z * -0.05 + side.z * -1.0 };
    if (free(rack.x, rack.z)) {
      found.slice(0, 3).forEach((id, k) => gun(id, (g) => {
        const t = (k - 1) * 0.15;
        g.position.set(rack.x + side.x * t + out.x * 0.02, 0.12, rack.z + side.z * t + out.z * 0.02);
        g.rotation.set(-Math.PI / 2 + 0.12, yaw + Math.PI, 0, 'YXZ');
      }));
    }
    if (found.length > 3) {
      put('lootWeapons', -1.1, 0.75, 0.2, 0.8);
      put('lootWeaponsLid', -1.55, 0.85, 1.2, 0.8);
      found.slice(3, 6).forEach((id, k) => gun(id, (g) => g.applyMatrix4(at(-1.1 + (k - 1) * 0.12, 0.75, 0.22, 0.2 + Math.PI / 2, 1))));
    }
    if (lacking) void syntyPropsLoaded().then(() => this.fillArmory(base, free));
  }

  /** What the Armería holds (campaign.ts: the weapons found, the accessories bought, the improvements loose): its corner filled again if it changed. */
  setArmory(stock: ArmoryStock): void {
    this.stock = stock;
    if (JSON.stringify(stock) === this.stocked || !this.armoryArgs) return;
    this.fillArmory(this.armoryArgs.base, this.armoryArgs.free);
  }

  /**
   * The soldier looked at in Soldados (as the old Cuartel's pedestal): built again only if it changed; null, none;
   * `fallen`, their cross and candle where they would stand (playtest 21/10 #13), as in the debrief's row.
   */
  setShowcase(template: TemplateId | null, color: string, appearance?: Appearance, gear?: Gear, seed?: string, fallen = false): void {
    const key = `${fallen ? 'grave' : template}:${color}:${JSON.stringify(appearance ?? null)}:${JSON.stringify(gear ?? null)}:${seed ?? ''}`;
    if (key === this.showcaseKey) return;
    this.showcaseKey = key;
    this.showcase.clear();
    this.showcaseRig = null;
    if (fallen) {
      const { dir } = this.soldierView;
      grave(this.showcase, { x: 0, z: 0 }, Math.atan2(dir.x, dir.z), () => this.showcaseKey === key);
      return;
    }
    if (!template) return;
    const rig = buildUnit(template, color, appearance, gear, seed);
    rig.root.rotation.y = this.showcaseYaw(performance.now() / 1000);
    this.showcase.add(rig.root);
    this.showcaseRig = rig;
  }

  /**
   * Where the soldier in Soldados faces: its camera, turning a little to either side and back (playtest 21/10: turning
   * round and round, it stood with its back to Personalizar half the time).
   */
  private showcaseYaw(time: number): number {
    const { dir } = this.soldierView;
    return Math.atan2(dir.x, dir.z) + Math.sin(time * 0.4) * 0.45;
  }

  /** Soldados: the soldier by the caravan (or the barracks), seen from out in the clearing, the pines behind it. */
  soldierShot(): Shot {
    const { x, z } = this.soldierAt;
    const { dir, dist } = this.soldierView;
    const target = new THREE.Vector3(x, 1.0, z);
    return { pos: target.clone().add(new THREE.Vector3(dir.x * dist, 1.5, dir.z * dist)), target, fov: 38 };
  }

  /**
   * The Armería: its corner by the caravan (campPlaces), from in front of the bench and a little to the side, close (20/10
   * #29: it showed the caravan and La Lata); `fit`, the corner's box, for the screen to keep it whole and centred.
   */
  armoryShot(): Shot {
    const { x, z, out } = this.armoryAt;
    const { dir } = this.armoryView;
    const target = new THREE.Vector3(x + out.x * 0.2, 0.55, z + out.z * 0.2);
    // Along the clearest way to it (campPlaces: no pine nor La Lata in between), mostly from in front.
    const ahead = new THREE.Vector3(out.x * 0.5 + dir.x * 0.5, 0, out.z * 0.5 + dir.z * 0.5).normalize();
    // What stands by the bench (not a lid thrown further off): the box the screen keeps whole.
    const fit = new THREE.Box3();
    for (const o of this.armoryCorner.children) {
      const b = new THREE.Box3().setFromObject(o);
      if (!b.isEmpty() && Math.hypot(b.getCenter(cv).x - x, cv.z - z) < 1.4) fit.union(b);
    }
    if (fit.isEmpty()) fit.setFromCenterAndSize(target, new THREE.Vector3(2.4, 1.2, 2.4));
    return { pos: target.clone().addScaledVector(ahead, 2.6).add(new THREE.Vector3(0, 1.3, 0)), target, fov: 40, fit };
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
      this.busBox = null;
      return;
    }
    this.busBox = { x: best.at.x, z: best.at.z, dx: best.dir.x, dz: best.dir.z, half: length / 2, halfWidth };
    this.busWay = best.way;
    this.busRoute = best.route;
    // Its doors shut, as it stands waiting (who gets in opens them: board).
    this.bus.park(best.at, Math.atan2(best.dir.x, best.dir.z), new THREE.Vector3(mid.x, 0, mid.z), !this.away, 0, false);
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
    if (this.base) {
      this.parkBus(this.base);
      this.campPlaces(this.base);
    }
    void bus.loaded().then(() => {
      if (bus !== this.bus || !this.base) return;
      this.parkBus(this.base);
      // Where it stands now the Armería's corner and Soldados' soldier keep out of its way.
      this.campPlaces(this.base);
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
    if (this.lifeEntries.length) this.relife();
    if (mirror !== this.mirror || halves !== this.halves) this.place = null;
    this.mirror = mirror;
    this.halves = halves;
    for (const o of this.lineup.children) if (o.userData.pad) (o as THREE.Mesh).geometry.dispose();
    this.lineup.clear();
    this.lineupRigs = [];
    this.warm = Math.max(this.warm, 4);
    const before = this.lineupByWho;
    this.lineupByWho = new Map();
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
        grave(this.lineup, at, facing, () => this.lineupKey === key);
        return;
      }
      const who = JSON.stringify(entry);
      const kept = before.get(who);
      const rig = kept ?? buildUnit(entry.template, entry.color, entry.appearance, entry.gear, entry.seed);
      this.lineupByWho.set(who, rig);
      // (Side by side, each waits another way: 22/10 #7.)
      if (isFigure(rig)) rig.restAs?.(i);
      rig.root.position.set(at.x, 0, at.z);
      if (!kept)
        rig.root.traverse((o) => {
          if (o instanceof THREE.Mesh) o.castShadow = true;
        });
      // Pose state, applied every frame in tick(); one already standing keeps its breath and its stride.
      Object.assign(rig.root.userData, { yaw: facing + (i % 3 - 1) * 0.12, wounded: entry.status === 'wounded' }, kept ? {} : { phase: i * 0.7, running: 0 });
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
    if (parked && root.visible) this.bus.park(root.position.clone(), heading, new THREE.Vector3(place.spots[0]!.x, 0, place.spots[0]!.z).lerp(new THREE.Vector3(place.spots[count - 1]!.x, 0, place.spots[count - 1]!.z), 0.5), true, 0, false);
    return (this.place = place);
  }

  /**
   * The squad in the row runs to La Lata's doors, one after another, and climbs in (playtest 20/10 #7: not vanishing
   * in the gap): each doorway's doors open as its first one gets there, each climbs up into it and on in (higher at a
   * van's or a lorry's back, over its tailgate), and its doors shut behind the last; resolves once all are aboard.
   */
  private async board(): Promise<void> {
    await this.bus.loaded();
    const rigs = this.lineupRigs.filter((r) => r.root.visible);
    if (!rigs.length) return;
    // (The clips of getting in are the camp's: loaded with the Asentamiento's life, but not for a build none of it has.)
    await Promise.all([whenCampClips('masc'), whenCampClips('femn')]).catch(() => undefined);
    if (this.bus.vehicle === 0) return this.boardCar(rigs);
    const root = this.bus.root;
    const centre = root.position.clone();
    const dir = new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y));
    const across = new THREE.Vector3(dir.z, 0, -dir.x);
    const doors = this.bus.doorways();
    const half = this.bus.length / 2;
    // (How many have taken each doorway so far: at a back one, two go up side by side, one each side of its middle.)
    const taken = new Map<number, number>();
    const plans = rigs.map((rig, i) => {
      const from = rig.root.position.clone();
      const door = doors.reduce((a, b) => (a.distanceTo(from) <= b.distanceTo(from) ? a : b));
      const offset = door.clone().sub(centre);
      // A door on its side (the bus's, the 4×4's) or at its back (the van's, the lorry's): at its very end, the back (a
      // side's door near the end counted as a back one, and the runners went round the far corner to it, through the
      // rest of the row).
      const back = Math.abs(offset.dot(dir)) > half - 0.35;
      const normal = back ? dir.clone().multiplyScalar(Math.sign(offset.dot(dir)) || -1) : across.clone().multiplyScalar(Math.sign(offset.dot(across)) || 1);
      // A double doorway's two leaves are one way in: one at a time through it (two at a back one, side by side).
      const way = doors.findIndex((d) => d.distanceTo(door) < 0.7);
      const nth = taken.get(way) ?? 0;
      taken.set(way, nth + 1);
      // (A lorry's tailgate is as wide as its back; a van's right leaf has its armour plate over it: by its left one.)
      const lanes = back && this.bus.vehicle === 2 ? 2 : 1;
      const lane = nth % lanes;
      const across2 = new THREE.Vector3(normal.z, 0, -normal.x);
      const aside = lanes > 1 ? across2.multiplyScalar(lane ? BOARD_LANE : -BOARD_LANE) : back ? across2.multiplyScalar(VAN_LEAF) : new THREE.Vector3();
      const foot = door.clone().addScaledVector(normal, 0.55).add(aside);
      // Up the step into the doorway, then on in past its plane, out of sight (19/10, #11: not through the body).
      const doorway = door.clone().addScaledVector(normal, -0.1).add(aside);
      const inside = door.clone().addScaledVector(normal, -0.1 - STEP_IN).add(aside);
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
      // A jog that picks up and slows to the door (BOARD_EASE either end: its top speed kept), setting off a beat
      // after the one before it (not all at once, on a word).
      const running = total / BOARD_SPEED + BOARD_EASE;
      const atFoot = running + i * BOARD_STAGGER;
      return { rig, run, lengths, total, foot, doorway, inside, door: way, lane, atFoot, delay: i * BOARD_STAGGER, running, stepped: false, walked: false };
    });
    // One at a time at each door, the nearest first (out of the way of those behind, who run past where it stood):
    // each held back at its place in the row until the one before it has climbed in (they all made for the same
    // spot at once, a seventh of a second apart, and climbed in through one another).
    for (let d = 0; d < doors.length; d++) {
      for (const lane of [0, 1]) {
        let free = -Infinity;
        for (const p of plans.filter((q) => q.door === d && q.lane === lane).sort((a, b) => a.atFoot - b.atFoot)) {
          const wait = Math.max(0, free - p.atFoot);
          p.delay += wait;
          p.atFoot += wait;
          free = p.atFoot + STEP_TIME + DOOR_GAP;
        }
      }
    }
    // Each doorway's doors: opening as its first gets to its foot, shutting once its last is in.
    const leaves = this.bus.leavesByDoorway();
    const ways = doors.map((_, d) => {
      const at = plans.filter((p) => p.door === d);
      if (!at.length) return null;
      const first = Math.min(...at.map((p) => p.atFoot));
      const last = Math.max(...at.map((p) => p.atFoot + CLIMB));
      // A double doorway's leaves are listed by its two halves.
      const which = doors.flatMap((w, j) => (w.distanceTo(doors[d]!) < 0.7 ? leaves[j] ?? [] : []));
      return { which, open: Math.max(0, first - DOOR_OPEN), shut: last + 0.08 };
    });
    // How high it is in: the 4×4's sill, the van's floor, the lorry's bed (and over its tailgate), the bus's step.
    const rise = [0.22, 0.45, 0.62, 0.3][this.bus.vehicle] ?? 0.3;
    const over = this.bus.vehicle === 2 ? TAILGATE_OVER : 0;
    const seconds = Math.max(...plans.map((p) => p.delay + p.running + CLIMB), ...ways.map((w) => (w ? w.shut + DOOR_OPEN : 0))) + 0.05;
    for (const p of plans) if (isFigure(p.rig)) p.rig.sling?.(true);
    // The camera (playtest 21/10 #3, Ana: a shot of its own): from the row's shot down lower and a little in, turned
    // to between the row and the doorway most of them take (the whole row still in it, the door at its middle: in
    // close on the door, those at the far end ran in from out of the frame), then a slow push in on the door while
    // they climb aboard.
    const cam = this.camera;
    const busiest = doors.map((_, d) => plans.filter((p) => p.door === d).length).reduce((best, n, d, all) => (n > all[best]! ? d : best), 0);
    const door = doors[busiest]!;
    const from = cam ? { pos: cam.position.clone(), look: this.lineupShot().target.clone() } : null;
    const into = from
      ? (() => {
          const pos = from.pos.clone().lerp(door.clone().setY(from.pos.y), 0.15);
          pos.y = THREE.MathUtils.lerp(from.pos.y, 1.7, 0.6);
          return { pos, look: from.look.clone().lerp(door, 0.6).setY(1.0) };
        })()
      : null;
    const look = new THREE.Vector3();
    const onDoor = door.clone().setY(1.0);
    this.boardFrom = from?.pos ?? null;
    await tween(seconds, (t) => {
      const now = t * seconds;
      if (cam && from && into) {
        const k = THREE.MathUtils.smootherstep(Math.min(1, now / 1.4), 0, 1);
        const push = Math.max(0, now - 1.4) / Math.max(0.5, seconds - 1.4);
        cam.position.lerpVectors(from.pos, into.pos, k).lerp(onDoor, push * 0.1 * k);
        cam.lookAt(look.lerpVectors(from.look, into.look, k).lerp(onDoor, THREE.MathUtils.smoothstep(push, 0, 1) * 0.6));
      }
      for (const w of ways) {
        if (!w) continue;
        const k = now < w.shut ? THREE.MathUtils.clamp((now - w.open) / DOOR_OPEN, 0, 1) : 1 - THREE.MathUtils.clamp((now - w.shut) / DOOR_OPEN, 0, 1);
        this.bus.setDoors(w.which, THREE.MathUtils.smoothstep(k, 0, 1));
      }
      // Their shadows run with them.
      this.warm = Math.max(this.warm, 1);
      for (const p of plans) {
        const since = now - p.delay;
        const d = p.rig.root.userData;
        const climbing = since >= p.running;
        d.running = since > 0 && !climbing ? 1 : 0;
        p.rig.root.visible = since < p.running + CLIMB;
        if (since <= 0) continue;
        d.climbing = climbing ? 1 : 0;
        if (climbing) {
          // At the door, turned to it (22/10 #12: not stooped, «a caballo»): up the step, its feet as the clip has
          // them (Mixamo's stairs, a step of it) and the rest of the floor's height the body's, then on in, walking.
          const c = since - p.running;
          d.climbing = 0;
          d.yaw = Math.atan2(p.inside.x - p.foot.x, p.inside.z - p.foot.z);
          if (c < STEP_TIME) {
            if (!p.stepped && isFigure(p.rig)) {
              p.stepped = true;
              p.rig.camp?.(null, true, true);
              p.rig.campMove?.('stepUp', STEP_SPEED);
            }
            // Up first, then in (over a lorry's tailgate, a little higher as it goes over).
            const k = c / STEP_TIME;
            p.rig.root.position.lerpVectors(p.foot, p.doorway, THREE.MathUtils.smoothstep(k, 0.3, 1));
            p.rig.root.position.y = (rise - STEP_RISE) * THREE.MathUtils.smoothstep(k, 0, 0.6) + over * Math.sin(Math.PI * THREE.MathUtils.smoothstep(k, 0.3, 1));
            d.running = 0;
            continue;
          }
          if (!p.walked && isFigure(p.rig)) {
            p.walked = true;
            p.rig.campMove?.(null);
          }
          // (Its feet back on the floor as the step's clip eases out: the floor's whole height the body's again.)
          // Stooped under its roof, on in (upright, the head came out through a van's).
          const w = Math.min(1, (c - STEP_TIME) / STEP_WALK);
          p.rig.root.position.lerpVectors(p.doorway, p.inside, w);
          p.rig.root.position.y = rise - STEP_RISE * Math.max(0, 1 - (c - STEP_TIME) / 0.35);
          d.running = 0.4;
          d.climbing = 1;
          continue;
        }
        // Picking up speed at the start, slowing at the door (a constant one between); the stride's weight with it.
        const ease = Math.min(BOARD_EASE, p.running / 2);
        const top = p.total / Math.max(0.01, p.running - ease);
        const end = p.running - since;
        let left = since < ease ? (0.5 * top * since * since) / ease : end < ease ? p.total - (0.5 * top * end * end) / ease : 0.5 * top * ease + top * (since - ease);
        d.running = Math.min(1, 0.35 + 0.65 * Math.min(since, end, ease) / ease);
        for (let s = 0; s < p.lengths.length; s++) {
          const a = p.run[s]!;
          const b = p.run[s + 1]!;
          if (left <= p.lengths[s]! || s === p.lengths.length - 1) {
            p.rig.root.position.lerpVectors(a, b, Math.min(1, left / (p.lengths[s]! || 1)));
            // Turning to where it goes as it goes, not all at once at each corner.
            d.yaw = turnToward(d.yaw, Math.atan2(b.x - a.x, b.z - a.z), BOARD_TURN / 60);
            break;
          }
          left -= p.lengths[s]!;
        }
      }
    });
    // In, out of sight: as they were, for when they are next lined up.
    for (const p of plans) {
      if (!isFigure(p.rig)) continue;
      p.rig.campMove?.(null);
      p.rig.camp?.(null, false);
      p.rig.sling?.(false);
    }
  }

  /**
   * Into the 4×4 (22/10 #12, Arturo: «se ponen a caballo y se meten de frente»; each by a door of their own, the driver
   * first): each runs to beside its door, the gun slung, a hand to the handle, opens it, sits down sideways, swings the
   * legs in and shuts it (Mixamo's «Entering Car», a left door's; mirrored for the right). The doors by the shortest runs
   * all told; the driver's starts in first, each other a beat after the one before. Resolves once all are in.
   */
  private async boardCar(rigs: UnitRig[]): Promise<void> {
    const root = this.bus.root;
    const centre = root.position.clone();
    const dir = new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y));
    const across = new THREE.Vector3(dir.z, 0, -dir.x);
    const half = this.bus.length / 2;
    const doors = this.bus.doorways();
    const leaves = this.bus.leavesByDoorway();
    const seat = bestDoors(rigs.map((r) => r.root.position), doors);
    const plans = rigs.map((rig, i) => {
      const door = doors[seat[i]!]!;
      // Its side (+1 its left), out from it, and in.
      const side = Math.sign(door.clone().sub(centre).dot(across)) || 1;
      const out = across.clone().multiplyScalar(side);
      const inward = out.clone().negate();
      // The clip's ground (its start, facing the car, its front to the left on its left side): the car's side where the
      // hand meets the handle, the seat in its doorway's middle; where it stands as the clip begins.
      const origin = door.clone().addScaledVector(out, ENTER_SIDE).addScaledVector(dir, ENTER_ALONG);
      const foot = origin.clone().addScaledVector(dir, ENTER_FROM[0]).addScaledVector(inward, ENTER_FROM[1]);
      // Round the car's nearer end to a door on its far side.
      const from = rig.root.position.clone();
      const path = [from];
      if (from.clone().sub(centre).dot(out) < 0) {
        const end = Math.sign(from.clone().sub(centre).dot(dir)) || -1;
        const corner = (s: number) => centre.clone().addScaledVector(dir, end * (half + 0.45)).addScaledVector(across, s * (this.bus.width / 2 + 0.55));
        path.push(corner(-side), corner(side));
      }
      path.push(foot);
      const lengths = path.slice(1).map((p, k) => p.distanceTo(path[k]!));
      const total = lengths.reduce((a, b) => a + b, 0);
      // (The hand that pulls the door to: the left, or the right in the mirrored clip.)
      const hand = rig.root.getObjectByName(side > 0 ? 'Hand_L' : 'Hand_R') ?? null;
      return { rig, path, lengths, total, origin, foot, inward, out, hand, move: (side > 0 ? 'enterCar' : 'enterCarR') as CampMove, door: seat[i]!, delay: i * BOARD_STAGGER, running: total / BOARD_SPEED + BOARD_EASE, start: 0, started: false, inside: false };
    });
    // The driver's in first; each other, once there, a beat after the one before (any that got to its door first waits
    // there for it, facing it).
    const driver = plans.find((p) => p.door === 0) ?? plans[0]!;
    let next = driver.delay + driver.running;
    for (const p of [driver, ...plans.filter((q) => q !== driver).sort((a, b) => a.delay + a.running - (b.delay + b.running))]) {
      p.start = Math.max(p.delay + p.running, next);
      next = p.start + ENTER_AFTER;
    }
    const length = ENTER_SECONDS / ENTER_SPEED;
    const clip = (p: (typeof plans)[number], at: number) => (at - p.start) * ENTER_SPEED;
    const seconds = Math.max(...plans.map((p) => p.start + length)) + 0.25;
    // The camera as for the others (board): down lower and in, turned to between the row and the driver's door.
    const cam = this.camera;
    const onDoor = doors[driver.door]!.clone().setY(1.0);
    const from = cam ? { pos: cam.position.clone(), look: this.lineupShot().target.clone() } : null;
    const into = from ? { pos: from.pos.clone().lerp(onDoor.clone().setY(from.pos.y), 0.15).setY(THREE.MathUtils.lerp(from.pos.y, 1.7, 0.6)), look: from.look.clone().lerp(onDoor, 0.6).setY(1.0) } : null;
    const look = new THREE.Vector3();
    this.boardFrom = from?.pos ?? null;
    for (const p of plans) if (isFigure(p.rig)) p.rig.sling?.(true);
    await tween(seconds, (t) => {
      const now = t * seconds;
      if (cam && from && into) {
        const k = THREE.MathUtils.smootherstep(Math.min(1, now / 1.4), 0, 1);
        const push = Math.max(0, now - 1.4) / Math.max(0.5, seconds - 1.4);
        cam.position.lerpVectors(from.pos, into.pos, k).lerp(onDoor, push * 0.1 * k);
        cam.lookAt(look.lerpVectors(from.look, into.look, k).lerp(onDoor, THREE.MathUtils.smoothstep(push, 0, 1) * 0.6));
      }
      this.warm = Math.max(this.warm, 1);
      for (const p of plans) {
        const d = p.rig.root.userData;
        const c = clip(p, now);
        // Its door opened and shut by its hand, as the clip has it; pulled to no further than its hand is in (the clip's
        // car is wider: shut on time, the arm stuck out through the panel). Gone in once the door is shut on it.
        let open = c < ENTER_SHUT[0] ? THREE.MathUtils.clamp((c - ENTER_OPEN[0]) / (ENTER_OPEN[1] - ENTER_OPEN[0]), 0, 1) : 1 - THREE.MathUtils.clamp((c - ENTER_SHUT[0]) / (ENTER_SHUT[1] - ENTER_SHUT[0]), 0, 1);
        open = THREE.MathUtils.smoothstep(open, 0, 1);
        if (p.hand && c > ENTER_OPEN[1] && !p.inside) {
          const reach = p.hand.getWorldPosition(handAt).sub(doors[p.door]!).dot(p.out);
          open = Math.max(open, Math.asin(THREE.MathUtils.clamp((reach + ENTER_FIST) / (this.bus.length * ENTER_LEAF), 0, 1)) / ENTER_SWING);
          if (c >= ENTER_SHUT[1] && reach < ENTER_IN) p.inside = true;
        }
        if (p.inside || c >= ENTER_SECONDS) open = 0;
        this.bus.setDoors(leaves[p.door] ?? [], open);
        p.rig.root.visible = !p.inside && c < ENTER_SECONDS;
        const since = now - p.delay;
        if (since <= 0) continue;
        if (c >= 0) {
          // Into the car: the clip moves the body, its ground stays (the hips' start offset eased off as it comes in).
          if (!p.started && isFigure(p.rig)) {
            p.started = true;
            p.rig.camp?.(null, true, true);
            p.rig.campMove?.(p.move, ENTER_SPEED);
          }
          const share = Math.min(0.97, (now - p.start) / ENTER_BLEND);
          p.rig.root.position.lerpVectors(p.foot, p.origin, share);
          d.running = d.climbing = 0;
          d.yaw = turnToward(d.yaw, Math.atan2(p.inward.x, p.inward.z), BOARD_TURN / 60);
          continue;
        }
        const running = since < p.running;
        d.running = running ? 1 : 0;
        if (!running) {
          // There first: waiting at its door, facing it.
          p.rig.root.position.copy(p.foot);
          d.yaw = turnToward(d.yaw, Math.atan2(p.inward.x, p.inward.z), BOARD_TURN / 60);
          continue;
        }
        const ease = Math.min(BOARD_EASE, p.running / 2);
        const top = p.total / Math.max(0.01, p.running - ease);
        const end = p.running - since;
        let left = since < ease ? (0.5 * top * since * since) / ease : end < ease ? p.total - (0.5 * top * end * end) / ease : 0.5 * top * ease + top * (since - ease);
        d.running = Math.min(1, 0.35 + 0.65 * Math.min(since, end, ease) / ease);
        for (let s = 0; s < p.lengths.length; s++) {
          const a = p.path[s]!;
          const b = p.path[s + 1]!;
          if (left <= p.lengths[s]! || s === p.lengths.length - 1) {
            p.rig.root.position.lerpVectors(a, b, Math.min(1, left / (p.lengths[s]! || 1)));
            d.yaw = turnToward(d.yaw, Math.atan2(b.x - a.x, b.z - a.z), BOARD_TURN / 60);
            break;
          }
          left -= p.lengths[s]!;
        }
      }
    });
    // In, out of sight: as they were, for when they are next lined up.
    for (const p of plans) {
      if (!isFigure(p.rig)) continue;
      p.rig.campMove?.(null);
      p.rig.camp?.(null, false);
      p.rig.sling?.(false);
    }
  }

  /** The squad aboard (if it is lined up), then La Lata away down its track, out of the clearing; resolves once it is gone. */
  depart(): Promise<void> {
    return this.drive(async () => {
      if (this.lineupRigs.length && this.bus.root.visible) await this.board();
      for (const o of this.lineup.children) if (o.userData.pad) o.visible = false;
      // The camera back out to the row's shot as La Lata goes, turning after it only a little (BUS_FOLLOW): it drives
      // on out of the frame (the camera turned after it all the way into the pines, 40° and more, and ended on them).
      const cam = this.camera;
      const back = this.boardFrom;
      const start = this.bus.root.position.clone();
      const reach = cam ? Math.tan(BUS_FOLLOW) * Math.hypot(start.x - cam.position.x, start.z - cam.position.z) : 0;
      let going = !!cam;
      const look = cam ? cam.position.clone().add(cam.getWorldDirection(new THREE.Vector3()).multiplyScalar(4)) : null;
      const follow = () => {
        if (!going || !cam || !look) return;
        const along = this.bus.root.position.clone().sub(start);
        along.setLength(Math.min(along.length(), reach));
        look.lerp(along.add(start).setY(1.0), 0.04);
        if (back) cam.position.lerp(back, 0.03);
        cam.lookAt(look);
        requestAnimationFrame(follow);
      };
      requestAnimationFrame(follow);
      await this.bus.depart(this.busWay, !this.busRoute);
      going = false;
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
    // The soldier looked at, at rest, facing its camera and turning a little either way.
    if (this.showcaseRig) {
      this.showcaseRig.root.rotation.y = this.showcaseYaw(time);
      if (isFigure(this.showcaseRig)) this.showcaseRig.applyPose(IDLE_POSE, time);
    }
    // About the clearing at their ease, by the fire.
    this.life.tick(dt, time);
    this.flames.tick(time);
    // At their posts, at rest.
    for (const rig of this.postRigs.values()) if (isFigure(rig)) rig.applyPose({ ...IDLE_POSE, yaw: rig.root.userData.yaw, phase: rig.root.userData.phase }, time);
    // The squad breathes in line, the wounded slumped, and runs to board.
    for (const rig of this.lineupRigs) {
      const d = rig.root.userData;
      if (isFigure(rig)) rig.applyPose({ ...IDLE_POSE, yaw: d.yaw, phase: d.phase, run: d.running, crouch: d.climbing ? 0.45 : d.wounded && !d.running ? 0.4 : 0 }, time);
    }
  }

  beforeRender(gl: THREE.WebGLRenderer): void {
    if (runMapJobs(gl) || this.warm > 0) gl.shadowMap.needsUpdate = true;
    if (this.warm > 0) this.warm--;
  }
}

/** How far from La Lata's side one leaning on it stands (its shoulder to the armour plates, tiles), and where along it (of its length from the middle: between the wheels). */
const LEAN_OFF = 0.46;
const LEAN_ALONG = [-0.1, 0.1];

/** A fire's flames: how many licks, how tall at most and how far out from its middle (tiles), their colours (yellower than fire looks: past white the tone mapping turns a saturated orange pink), and how bright (to bloom). */
const FLAME_LICKS = 6;
const FLAME_TALL = 0.42;
const FLAME_OUT = 0.13;
const FLAME_COLORS = [0xffb030, 0xffd050, 0xff9a20, 0xffe080];
const FLAME_GLOW = 1.3;

/** The fires lit: over each fire pit a few licks of flame, each flickering on its own, turning slowly; no light of their own (a light more is a shader more). */
class CampFlames {
  readonly group = new THREE.Group();
  private licks: { mesh: THREE.Mesh; size: number; phase: number; rate: number }[] = [];
  private key = '';
  private readonly materials = FLAME_COLORS.map((c) => new THREE.MeshBasicMaterial({ color: new THREE.Color(c).multiplyScalar(FLAME_GLOW) }));

  set(fires: { x: number; z: number }[]): void {
    const key = fires.map((f) => `${f.x.toFixed(2)},${f.z.toFixed(2)}`).join(' ');
    if (key === this.key) return;
    this.key = key;
    this.group.clear();
    this.licks = [];
    fires.forEach((f, n) => {
      for (let k = 0; k < FLAME_LICKS; k++) {
        const mesh = new THREE.Mesh(fxShape('flame'), this.materials[k % this.materials.length]!);
        const a = (k / FLAME_LICKS) * Math.PI * 2 + n;
        const out = k === 0 ? 0 : FLAME_OUT * (0.6 + 0.4 * ((k * 37) % 5) / 4);
        mesh.position.set(f.x + Math.cos(a) * out, 0.08, f.z + Math.sin(a) * out);
        this.group.add(mesh);
        this.licks.push({ mesh, size: FLAME_TALL * (k === 0 ? 1 : 0.55 + 0.1 * (k % 3)), phase: k * 1.7 + n * 3.1, rate: 5 + (k % 4) * 1.3 });
      }
    });
  }

  tick(time: number): void {
    // (The shape loads with the effects: a stand-in until then.)
    const shape = this.licks.length ? fxShape('flame') : null;
    for (const l of this.licks) {
      if (l.mesh.geometry !== shape) l.mesh.geometry = shape!;
      const t = time * l.rate + l.phase;
      const flick = 0.75 + 0.25 * Math.sin(t) * Math.sin(t * 0.37 + 1.1);
      const s = l.size * flick;
      l.mesh.scale.set(s * 0.55, s, s * 0.55);
      l.mesh.position.y = 0.08 + s * 0.38;
      l.mesh.rotation.y = time * 0.6 + l.phase;
    }
  }
}

/** A prop's box in its own frame (empty while it loads). */
function propBox(id: PropId): THREE.Box3 {
  const out = new THREE.Box3();
  for (const part of syntyProp(id) ?? []) out.union((part.geometry.boundingBox ?? (part.geometry.computeBoundingBox(), part.geometry.boundingBox!)).clone().applyMatrix4(part.matrix));
  return out;
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
/**
 * How fast the squad jogs to board La Lata (tiles a second), the seconds it takes to get going and to slow at the door,
 * how long after the one before each sets off, and how fast each turns (radians a second).
 */
const BOARD_SPEED = 2.9;
const BOARD_EASE = 0.35;
const BOARD_STAGGER = 0.12;
const BOARD_TURN = 7;
/** How far the camera turns after La Lata as it leaves (radians, from where it stood). */
const BUS_FOLLOW = THREE.MathUtils.degToRad(18);

/** `yaw` turned towards `goal` by at most `step` (radians), the short way round. */
function turnToward(yaw: number, goal: number, step: number): number {
  const d = Math.atan2(Math.sin(goal - yaw), Math.cos(goal - yaw));
  return yaw + THREE.MathUtils.clamp(d, -step, step);
}
/**
 * Into the 4×4 (boardCar; Mixamo's «Entering Car», CAMP_SPANS' part of it, measured on our bodies): how far out from
 * the car's side its ground stands (where the hand meets the handle) and ahead of the doorway's middle (where the hips
 * sit down), where the hips are from it as the clip begins (back along the car, in), its seconds and the speed it
 * plays at; when the hand opens the door and shuts it (the clip's seconds); how long it takes to come in, and how long
 * after the one before each starts in.
 */
const ENTER_SIDE = 0.62;
const ENTER_ALONG = 0.09;
const ENTER_FROM: [number, number] = [-0.18, 0.36];
const ENTER_SECONDS = 6.2;
const ENTER_SPEED = 1.25;
const ENTER_OPEN: [number, number] = [0.1, 1.2];
const ENTER_SHUT: [number, number] = [5.3, 5.9];
const ENTER_BLEND = 0.35;
const ENTER_AFTER = 0.5;
/** A 4×4's door leaf, its share of the car's length, and how far it swings open (radians: mula.ts CAR_DOOR_SWING). */
const ENTER_LEAF = 0.17;
const ENTER_SWING = 1.15;
/**
 * How far the door is kept off the hand's middle (tiles: the fist's half, and the panel's inside the car's widest, its
 * bars and armour), and how far in from that widest the hand is in, the door shut on it.
 */
const ENTER_FIST = 0.2;
const ENTER_IN = -0.12;
const handAt = new THREE.Vector3();

/** For each of those at `from`, a door of `doors` of its own, by the shortest runs all told (any more than doors share the nearest). */
function bestDoors(from: THREE.Vector3[], doors: THREE.Vector3[]): number[] {
  const n = Math.min(from.length, doors.length);
  let best: number[] = [];
  let bestCost = Infinity;
  const pick = (k: number, taken: number[], cost: number) => {
    if (cost >= bestCost) return;
    if (k === n) {
      bestCost = cost;
      best = [...taken];
      return;
    }
    for (let d = 0; d < doors.length; d++) {
      if (taken.includes(d)) continue;
      taken.push(d);
      pick(k + 1, taken, cost + from[k]!.distanceTo(doors[d]!));
      taken.pop();
    }
  };
  pick(0, [], 0);
  for (let k = n; k < from.length; k++) best.push(doors.reduce((a, b, j) => (b.distanceTo(from[k]!) < doors[a]!.distanceTo(from[k]!) ? j : a), 0));
  return best;
}

/**
 * Up into a van's or a lorry's back or the bus's door (board; Mixamo's «Ascending Stairs», a step of it: CAMP_SPANS):
 * the speed it plays at and how long that takes, how high its hips go up (tiles: the rest of the floor's height the
 * body's), and how long the walk on in takes and how far in it goes, out of sight; how far apart two going up side by
 * side at a back doorway are from its middle.
 */
const STEP_SPEED = 1.3;
const STEP_TIME = 1.6 / STEP_SPEED;
const STEP_RISE = 0.17;
const STEP_WALK = 0.45;
const STEP_IN = 0.5;
const BOARD_LANE = 0.3;
/** A van's back: how far its left leaf's middle is from the doorway's (its right one has an armour plate over it). */
const VAN_LEAF = 0.22;
/** How much higher one goes over a lorry's tailgate than its bed (tiles). */
const TAILGATE_OVER = 0.15;
/** How long climbing in takes, from the door's foot (seconds), and how long after one the next gets to that door. */
const CLIMB = STEP_TIME + STEP_WALK;
const DOOR_GAP = 0.15;
/** How long La Lata's doors take to open or shut as the squad gets in (s). */
const DOOR_OPEN = 0.35;
const CANDLE = new THREE.MeshStandardMaterial({ color: 0x2a1a00, emissive: 0xffb050, emissiveIntensity: 2.2 });

/** The pieces that stand tall enough to hide a soldier from a camera a storey up (but the buildings, by their tiles). */
const TALL = new Set<PieceId>(['palisade', 'wall', 'gate', 'tower', 'floodlight']);

/** Each pack's atlas as a material, for props put on their own (the Armería's corner). */
const atlasMaterials = new Map<AtlasId, THREE.MeshStandardMaterial>();

/** A prop's meshes at `m` into `into`, on its pack's atlas; false if its model is still loading. */
/** Scratch for the Armería's shot. */
const cv = new THREE.Vector3();

/** The Armería's bench's work surface above the ground, at the model's own size (measured: a shelf on its board is at 0.97, the board goes on up to 1.35). */
const BENCH_TOP = 0.58;
/**
 * Where the Armería's corner puts things (fillArmory: across the caravan's end u, out from it v, tiles): the bench's
 * middle and ends, the toolbox, the rack, the ammunition crate and its lid, the crate of weapons and its lid; all of
 * them free and clear of Soldados' soldier and its shot, or the corner goes elsewhere (campPlaces), out to this much
 * past the caravan's edge.
 */
const CORNER_SPOTS: readonly [number, number][] = [[0, 0], [0.8, 0], [-0.8, 0], [0.6, 0.45], [-1, -0.05], [1, 0.3], [1.3, 0.65], [-1.1, 0.75], [-1.55, 0.85]];
const CORNER_REACH = 8;
/** And this far to either hand along the side it is out from (m), the nearer first. */
const CORNER_SHIFTS = [0, 1.5, -1.5, 3, -3];
/** How far the corner's things keep from Soldados' soldier and from the way its camera looks at it (m). */
const SOLDIER_ROOM = 1.2;
/** And from what the squad's row's camera sees (m), for a row of this many at least (the Escuadra's usual), and behind it. */
const ROW_ROOM = 1.5;
const ROW_GUESS = 6;
/** How far behind the row its camera's frame is kept clear (m). */
const ROW_BEHIND = 4;

/** Whether (x, z) is inside triangle a b c (on the ground plan) or within `margin` of it. */
function nearTriangle(x: number, z: number, a: { x: number; z: number }, b: { x: number; z: number }, c: { x: number; z: number }, margin: number): boolean {
  const cross = (p: { x: number; z: number }, q: { x: number; z: number }) => (q.x - p.x) * (z - p.z) - (q.z - p.z) * (x - p.x);
  const [d1, d2, d3] = [cross(a, b), cross(b, c), cross(c, a)];
  if (!((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0))) return true;
  const edge = (p: { x: number; z: number }, q: { x: number; z: number }) => {
    const len = Math.hypot(q.x - p.x, q.z - p.z) || 1;
    const t = THREE.MathUtils.clamp(((x - p.x) * (q.x - p.x) + (z - p.z) * (q.z - p.z)) / (len * len), 0, 1);
    return Math.hypot(x - p.x - (q.x - p.x) * t, z - p.z - (q.z - p.z) * t);
  };
  return Math.min(edge(a, b), edge(b, c), edge(c, a)) <= margin;
}

/** A cross where a fallen soldier would stand (`at`, in `into`), as tall as a soldier's chest, a candle at its foot; `still`: whether it is still wanted once the props load. */
function grave(into: THREE.Group, at: { x: number; z: number }, facing: number, still: () => boolean): void {
  const cross = () => {
    const parts = syntyProp('bCross');
    if (!parts) return false;
    const top = parts.reduce((y, part) => Math.max(y, (part.geometry.boundingBox ?? (part.geometry.computeBoundingBox(), part.geometry.boundingBox!)).clone().applyMatrix4(part.matrix).max.y), 0) || 1;
    const k = 0.95 / top;
    // Synty's cross has its arms along z: turned a quarter, it shows them to `facing` (not edge on, a bare post).
    return propMeshes('bCross', new THREE.Matrix4().compose(new THREE.Vector3(at.x, 0, at.z), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), facing + Math.PI / 2), new THREE.Vector3(k, k, k)), into);
  };
  if (!cross()) void syntyPropsLoaded().then(() => still() && cross());
  const candle = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.12, 0.07).translate(0, 0.06, 0), CANDLE);
  candle.position.set(at.x + Math.sin(facing) * 0.25, 0, at.z + Math.cos(facing) * 0.25);
  into.add(candle);
}

/** A pack's atlas as a material of its own, shared. */
function atlasLook(atlas: AtlasId): THREE.MeshStandardMaterial {
  let material = atlasMaterials.get(atlas);
  if (!material) atlasMaterials.set(atlas, (material = new THREE.MeshStandardMaterial({ map: atlasTexture(atlas), roughness: 0.85 })));
  return material;
}

function propMeshes(id: PropId, m: THREE.Matrix4, into: THREE.Group): boolean {
  const parts = syntyProp(id);
  if (!parts) return false;
  for (const part of parts) {
    const mesh = new THREE.Mesh(part.geometry, atlasLook(part.atlas));
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

/**
 * A piece in its own colours, turned to the front, centred at the origin, once its models have loaded (UI: Construir's
 * pictures, pieceIcons.ts; Ana, 21/10 #35).
 */
export async function pieceLook(id: PieceId, level = 1): Promise<THREE.Object3D> {
  let missing = false;
  const out = pieceGroup(id, 0, level, null, () => (missing = true));
  if (!missing) return out;
  await syntyPropsLoaded();
  return pieceGroup(id, 0, level, null, () => undefined);
}

/** A piece's models round its footprint's middle, in `material` (null: each on its own atlas); `lacking` if some are still loading. */
function pieceGroup(id: PieceId, rot: number, level: number, material: THREE.Material | null, lacking: () => void): THREE.Object3D {
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
        const mesh = new THREE.Mesh(part.geometry, material ?? atlasLook(part.atlas));
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
