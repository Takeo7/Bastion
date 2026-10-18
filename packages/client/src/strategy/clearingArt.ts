import * as THREE from 'three';
import { createMission, pieceAt, PIECES, Rng, treeAt, type BasePiece, type BaseState, type GameState, type PieceId } from '@bastion/engine';
import { drawPiece, pieceMap, type PieceArt } from '../game/basePieces';
import { EvacBus } from '../game/evacBus';
import { MapView } from '../game/mapView';
import { dayMood, runMapJobs } from '../game/renderer';
import { graphics } from '../settings';
import { syntyProp, syntyPropsLoaded } from '../game/syntyProps';
import type { PropId } from '../game/syntyManifest';
import type { ClearingArt } from './clearing';
import { skyTexture, type StageMood } from './stage';

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
  /** La Lata, parked on a track by the caravan (parkBus); and how far it goes to be out of the clearing. */
  private readonly bus = new EvacBus();
  private busWay = 14;
  /** Frames left with the shadow map drawn whatever (the first ones, while the props come in). */
  private warm = 30;

  constructor(snow = false) {
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
    this.group.add(this.map.group, this.bus.root, this.sun, this.sun.target, this.sky, this.fill, this.fill.target);
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
    const s = clearingState(base);
    if (!this.built) {
      this.map.build(s);
      this.built = true;
      this.aim(base);
    } else this.map.rebuild(s);
    // It is ours: all of it in sight (§11.1).
    this.map.setVisibility(new Array<boolean>(s.width * s.height).fill(true));
    this.parkBus(base);
  }

  /**
   * La Lata by the caravan, nose out along a track (base.ts onTrack), where
   * nothing is built and no pine stands under it: the nearest such spot up
   * each track from the caravan, a lane to either side if the middle is
   * taken; its doors to the caravan. Nowhere clear, it is away (hidden).
   */
  private parkBus(base: BaseState): void {
    const caravan = base.pieces.find((p) => p.piece === 'caravan');
    const [cw, ch] = caravan ? (caravan.rot % 2 ? [PIECES.caravan.size[1], PIECES.caravan.size[0]] : PIECES.caravan.size) : [0, 0];
    const mid = caravan ? { x: caravan.x + cw / 2, z: caravan.y + ch / 2 } : { x: base.width / 2, z: base.height / 2 };
    const length = this.bus.length;
    const clear = (at: THREE.Vector3, dir: { x: number; z: number }) => {
      for (let k = -length / 2; k <= length / 2; k += 0.5) {
        for (const side of [-0.8, 0, 0.8]) {
          const x = Math.floor(at.x + dir.x * k + dir.z * side);
          const y = Math.floor(at.z + dir.z * k + dir.x * side);
          if (x < 0 || y < 0 || x >= base.width || y >= base.height || pieceAt(base, x, y) || treeAt(base, x, y)) return false;
        }
      }
      return true;
    };
    let best: { at: THREE.Vector3; dir: { x: number; z: number }; way: number; cost: number } | null = null;
    for (const dir of [{ x: 1, z: 0 }, { x: -1, z: 0 }, { x: 0, z: 1 }, { x: 0, z: -1 }]) {
      const half = (dir.x ? cw : ch) / 2;
      for (let reach = half + 1.5; reach <= half + 9; reach += 0.5) {
        for (const lane of [0, -1.5, 1.5]) {
          const along = reach + length / 2;
          const at = new THREE.Vector3(base.width / 2 + dir.x * along + dir.z * lane, 0, base.height / 2 + dir.z * along + dir.x * lane);
          // Nearest the caravan, the middle of the track first.
          const cost = reach + Math.abs(lane) * 1.5;
          if ((best && cost >= best.cost) || !clear(at, dir)) continue;
          const way = (dir.x ? base.width : base.height) / 2 - along + length + 2;
          best = { at, dir, way, cost };
        }
      }
    }
    if (!best) {
      this.bus.root.visible = false;
      return;
    }
    this.busWay = best.way;
    this.bus.park(best.at, Math.atan2(best.dir.x, best.dir.z), new THREE.Vector3(mid.x, 0, mid.z));
  }

  /** La Lata away down its track, out of the clearing (the squad off on a mission); resolves once it is gone. */
  depart(): Promise<void> {
    return this.bus.depart(this.busWay);
  }

  /** La Lata back up its track and round to stand by the caravan again; resolves once it stands. */
  arrive(): Promise<void> {
    return this.bus.arrive(this.busWay);
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

  tick(dt: number): void {
    this.map.tick(dt);
  }

  beforeRender(gl: THREE.WebGLRenderer): void {
    if (runMapJobs(gl) || this.warm > 0) gl.shadowMap.needsUpdate = true;
    if (this.warm > 0) this.warm--;
  }
}

/** The clearing as a battlefield with nobody on it: the map its siege would be fought on. */
function clearingState(base: BaseState): GameState {
  const s = createMission({ map: { kind: 'base', base }, slots: [0], rng: new Rng(1), difficulty: 1 });
  s.units = [];
  delete s.traps;
  if (s.base) s.base.felled = base.felled ?? [];
  return s;
}

const ghostMaterial = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.8 });

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
