import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { LineSegments2 } from 'three/addons/lines/LineSegments2.js';
import { LineSegmentsGeometry } from 'three/addons/lines/LineSegmentsGeometry.js';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { AWARE_RANGE, DIRS4, VIEW_CONE, type CoverLevel, type Place, type Vec2 } from '@bastion/engine';
import { tileToWorld } from './coords';
import { graphics } from '../settings';
import { tileLook, type TileLook } from './tileLooks';
import { wait } from './tween';
import eyeUrl from '../assets/expediente/eye.png';
import hatchUrl from '../assets/expediente/hatch.png';

/**
 * How the move range is drawn (playtest 2026-10-13 #13: «nada de cuadrícula
 * fija, solo líneas suaves y las casillas al pasar el ratón»), as XCOM does:
 * a border round what one action reaches (solid blue) and round the dash
 * (dashed yellow), and the tiles' squares only round the pointer. `outline`
 * is just that; `soft` adds a faint, gapless wash inside the borders.
 */
export type RangeStyle = 'outline' | 'soft';

/** How far round the pointer the tiles' squares show, fading out. */
const NEAR_RADIUS = 2.5;

export interface MoveTile {
  /** With its level (docs/FASE6.md), when it has one. */
  pos: Vec2 & { z?: number };
  dash: boolean;
  /** Ending here would get the concealed squad noticed. */
  detected: boolean;
}

const COLOR_MOVE = new THREE.Color(0x3aa0ff);
const COLOR_DASH = new THREE.Color(0xffc93a);
/** The dark casing under a path that has one («Clásico»). */
const CASING = new THREE.Color(0x081018);
const IDENTITY = new THREE.Quaternion();
/** The brightest the ground goes under a shade (shadeMaterial). */
const SHADE = new THREE.Color(0x56606a);
const COLOR_DETECT = new THREE.Color(0xff4a4a);
/** The detection light at the enemy: its colour and how much of it there (it fades to nothing at the edge). Very faint (#30). */
const DETECT_GLOW = new THREE.Color(0xff3a26);
const DETECT_ALPHA = 0.22;
const COLOR_BLAST = new THREE.Color(0xff8a2a);
/** A blast that can't be thrown there. */
const COLOR_OFF = new THREE.Color(0x8a939c);
const COLOR_EVAC = new THREE.Color(0x4dff9a);

/**
 * Furniture to search (docs/FASE2.md, «Registrar»): `open` can be searched,
 * `pick` is the one Registrar would search now, `done` was searched already.
 */
export type SpotKind = 'open' | 'pick' | 'done';

const SPOT_COLORS: Record<SpotKind, number> = { open: 0xffd25a, pick: 0xffffff, done: 0x9aa3ab };
/** The badge floats just over the furniture: higher, from the tilted camera it read as over the soldier beside it. */
const SPOT_HEIGHT = 1.3;

/** The badge over a piece of furniture: a magnifier while it can be searched, a tick once it has been. */
function spotTexture(done: boolean): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d')!;
  const ink = done ? '#b7c0c7' : '#ffd25a';
  g.beginPath();
  g.arc(32, 32, 27, 0, Math.PI * 2);
  g.fillStyle = 'rgba(10,20,30,0.82)';
  g.fill();
  g.lineWidth = 4;
  g.strokeStyle = ink;
  g.stroke();
  g.lineCap = 'round';
  g.lineJoin = 'round';
  g.lineWidth = 5.5;
  g.beginPath();
  if (done) {
    g.moveTo(20, 33);
    g.lineTo(29, 42);
    g.lineTo(45, 23);
  } else {
    g.arc(28, 28, 10, 0, Math.PI * 2);
    g.moveTo(35.5, 35.5);
    g.lineTo(45, 45);
  }
  g.stroke();
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

/** Empties a group of throwaway meshes, lines and sprites (shared geometries and textures stay). */
function clearGroup(group: THREE.Group, shared: ReadonlySet<THREE.BufferGeometry> = new Set()): void {
  for (const child of [...group.children]) {
    group.remove(child);
    if (child instanceof THREE.Mesh || child instanceof Line2 || child instanceof THREE.Sprite) {
      if (!(child instanceof THREE.Sprite) && !shared.has(child.geometry)) child.geometry.dispose();
      (child.material as THREE.Material).dispose();
    }
  }
}

/** Draws a shield icon on a canvas: full (2) or half (1) cover. */
function shieldTexture(level: 1 | 2): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d')!;
  const shape = () => {
    g.beginPath();
    g.moveTo(32, 4);
    g.lineTo(56, 13);
    g.lineTo(53, 38);
    g.quadraticCurveTo(46, 54, 32, 60);
    g.quadraticCurveTo(18, 54, 11, 38);
    g.lineTo(8, 13);
    g.closePath();
  };
  shape();
  g.fillStyle = 'rgba(10,20,30,0.75)';
  g.fill();
  g.save();
  shape();
  g.clip();
  g.fillStyle = '#54d1ff';
  g.fillRect(0, level === 2 ? 0 : 34, 64, 64);
  g.restore();
  shape();
  g.lineWidth = 4;
  g.strokeStyle = '#bff0ff';
  g.stroke();
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

function flatRing(inner: number, outer: number, color: number, opacity: number): THREE.Mesh {
  const mesh = new THREE.Mesh(
    new THREE.RingGeometry(inner, outer, 36).rotateX(-Math.PI / 2),
    new THREE.MeshBasicMaterial({ color, transparent: true, opacity, depthWrite: false }),
  );
  mesh.visible = false;
  return mesh;
}

/**
 * A mark for the middle of a tile («Puntos»): a white disc (or ring, `hollow`,
 * for the dash) on a dark rim, so it reads on snow; the instance's colour
 * tints the white and leaves the rim. Its white reaches DOT_CORE of the half.
 */
function dotTexture(hollow: boolean): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d')!;
  g.fillStyle = 'rgba(8,12,16,0.55)';
  g.beginPath();
  g.arc(32, 32, 30, 0, Math.PI * 2);
  g.fill();
  g.fillStyle = '#ffffff';
  g.beginPath();
  g.arc(32, 32, 32 * DOT_CORE, 0, Math.PI * 2);
  if (hollow) g.arc(32, 32, 32 * DOT_CORE * 0.5, 0, Math.PI * 2, true);
  g.fill();
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}
const DOT_CORE = 0.6;

/** A strip's fade («Degradado»): opaque on its top row, gone at the bottom. */
function fadeTexture(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 4;
  c.height = 64;
  const g = c.getContext('2d')!;
  const grad = g.createLinearGradient(0, 0, 0, 64);
  grad.addColorStop(0, 'rgba(255,255,255,1)');
  grad.addColorStop(0.35, 'rgba(255,255,255,0.45)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 4, 64);
  return new THREE.CanvasTexture(c);
}
/** How far into the tile a «Degradado» strip goes. */
const FADE_DEPTH = 0.55;

/**
 * A line colour as light (tileLooks.ts, playtest 18/10 #14): `hex` pushed past
 * 1 by `k`, so Arte's bloom takes it up at medium and high effects.
 */
function glow(hex: THREE.ColorRepresentation, k: number): THREE.Color {
  return new THREE.Color(hex).multiplyScalar(k);
}

/** The soft halo under a light line: the same shape, wider, faint and additive (a chalk line's dust: plain). */
function haloMaterial(color: THREE.ColorRepresentation, dashed = false, width = 5, alpha = 0.12): LineMaterial {
  return new LineMaterial({ color: new THREE.Color(color), linewidth: width, transparent: true, opacity: alpha, blending: THREE.AdditiveBlending, depthTest: false, depthWrite: false, dashed, dashSize: 0.24, gapSize: 0.18 });
}

/**
 * A dark stroke under a line (TileLook.shade). It blends by the minimum: the
 * ground goes no brighter than SHADE there, so it shows on snow and sand and
 * not at all on dark ground, and where its pieces overlap (the joints) it
 * doesn't darken twice.
 */
function shadeMaterial(dashed = false): LineMaterial {
  const m = new LineMaterial({ color: SHADE, linewidth: 5, transparent: true, depthTest: false, depthWrite: false, dashed, dashSize: 0.24, gapSize: 0.18 });
  shadeBlend(m);
  return m;
}

function shadeBlend(m: THREE.Material): void {
  m.blending = THREE.CustomBlending;
  m.blendEquation = THREE.MinEquation;
}

/** A hand's wobble for a chalk stroke: a few hundredths of a tile, the same for the same point. */
function wobble(x: number, z: number, axis: number): number {
  const h = Math.sin(x * 12.9898 + z * 78.233 + axis * 37.719) * 43758.5453;
  return (h - Math.floor(h) - 0.5) * 0.09;
}

function fatLine(color: number, width: number): Line2 {
  const line = new Line2(
    new LineGeometry(),
    new LineMaterial({ color, linewidth: width, transparent: true, opacity: 0.95, depthTest: false }),
  );
  line.renderOrder = 10;
  line.visible = false;
  return line;
}

/**
 * Gives a fat line a new shape. Disposing first matters: three.js caps the
 * segments it draws at the count of the first shape it uploaded (a longer
 * path came out cut short), and frees the old buffers only on dispose.
 */
function reshape(line: Line2, points: number[]): void {
  const geometry = line.geometry as LineGeometry;
  geometry.dispose();
  geometry.setPositions(points);
}

/**
 * The column of light over an objective (playtest 17/10 #48: «la línea verde
 * que indica hacia dónde ir»): a line of a fixed width on screen, so it reads
 * from far off, solid green with a dark edge (an additive glow vanished over
 * the snow), from over the heads (it hides nothing of the fighting below) up
 * to GUIDE_HEIGHT. Local to its group, which stands on the tile; `from`
 * raises its foot (an objective upstairs).
 */
const GUIDE_HEIGHT = 40;
function guideBeam(color: number, from = 0): THREE.Group {
  const g = new THREE.Group();
  const points = [0, from + 1.6, 0, 0, from + GUIDE_HEIGHT, 0];
  const casing = new Line2(new LineGeometry(), new LineMaterial({ color: 0x0b1a12, linewidth: 7, transparent: true, opacity: 0.5, depthWrite: false }));
  const core = new Line2(new LineGeometry(), new LineMaterial({ color, linewidth: 3.5, transparent: true, opacity: 0.95, depthWrite: false }));
  for (const line of [casing, core]) line.geometry.setPositions(points);
  casing.renderOrder = 7;
  core.renderOrder = 8;
  g.add(casing, core);
  return g;
}

/**
 * The edge of what an enemy sees (Arturo, 17/10: the dead see in a cone, not
 * all round): its VIEW_CONE ahead out to `range`, and the AWARE_RANGE circle
 * all round, behind it too, as one closed line, a keyhole. Without a facing
 * (the living, an old save) the whole circle. World points round `c`.
 */
function keyhole(c: THREE.Vector3, range: number, facing: number | undefined): number[] {
  const out: number[] = [];
  const at = (a: number, r: number) => out.push(c.x + Math.cos(a) * r, c.y, c.z + Math.sin(a) * r);
  if (facing === undefined || range <= AWARE_RANGE) {
    for (let i = 0; i <= 96; i++) at((i / 96) * Math.PI * 2, Math.max(range, AWARE_RANGE));
    return out;
  }
  const half = (VIEW_CONE / 2) * (Math.PI / 180);
  // The cone's far arc, then round behind it at arm's length, and back to where it began.
  for (let i = 0; i <= 48; i++) at(facing - half + (i / 48) * 2 * half, range);
  at(facing + half, AWARE_RANGE);
  for (let i = 1; i <= 48; i++) at(facing + half + (i / 48) * (2 * Math.PI - 2 * half), AWARE_RANGE);
  at(facing - half, range);
  return out;
}

/**
 * Planning without the command (playtest 17/10 #49): the move's preview in
 * the player's colour, dimmed (18/10 #4: it was grey, and didn't show), so it
 * shows it isn't one's turn to order, like the plan's own line.
 */
const PLAN_DIM = 0.3;
/** How long a signal's tag stays over the spot, in seconds (fx.ping's beam is 3.5). */
const PING_TAG_S = 4;

export class Overlays {
  readonly group = new THREE.Group();
  /** Without the command: the range's borders, the path and the pointer's ring go the plan's colour (#49). */
  private planning = false;
  /** The plan's colour: the player's, dimmed (setPlanColor). */
  private readonly planTint = new THREE.Color(0x9aa0a4);
  private readonly tiles: THREE.InstancedMesh;
  /** The range's borders: one action (solid) and the dash (dashed), at the tiles' own heights. */
  private readonly walkEdge = new LineSegments2(
    new LineSegmentsGeometry(),
    new LineMaterial({ color: 0x6cc4ff, linewidth: 2.5, transparent: true, opacity: 0.95, depthTest: false }),
  );
  private readonly dashEdge = new LineSegments2(
    new LineSegmentsGeometry(),
    new LineMaterial({ color: 0xffc93a, linewidth: 2.5, dashed: true, dashSize: 0.32, gapSize: 0.2, transparent: true, opacity: 0.95, depthTest: false }),
  );
  /** Their halos (a light look's glow, a chalk look's dust), on the same geometry (drawBorders shapes both). */
  private readonly walkHalo = new LineSegments2(this.walkEdge.geometry, haloMaterial(0x6cc4ff));
  private readonly dashHalo = new LineSegments2(this.dashEdge.geometry, haloMaterial(0xffc93a, true));
  /** A soft dark stroke under them (TileLook.shade), for snow and sand. */
  private readonly walkShade = new LineSegments2(this.walkEdge.geometry, shadeMaterial());
  private readonly dashShade = new LineSegments2(this.dashEdge.geometry, shadeMaterial(true));
  /** How the preview looks (tileLooks.ts: the five proposals of playtest 18/10 #14, and «Clásico»). */
  private look: TileLook = tileLook();
  /** A mark in the middle of each tile in reach («Puntos»): one action's, filled, and the dash's, hollow. */
  private readonly dots: [THREE.InstancedMesh, THREE.InstancedMesh];
  /** «Degradado»: a strip inside each side on the range's edge, bright at the edge and fading inwards. */
  private readonly fade: THREE.InstancedMesh;
  /** A dotted path («Puntos»): marks along it like the tiles'. */
  private readonly pathDots: THREE.InstancedMesh;
  /** What was last shown, to draw it again in another look. */
  private lastRange: { tiles: MoveTile[]; planning: boolean } = { tiles: [], planning: false };
  private lastPath: { from: Vec2 | null; path: Vec2[] | null; dash: boolean } = { from: null, path: null, dash: false };
  private lastPlans: { from: Vec2; path: Vec2[]; color: string }[] = [];
  private lastFocus: Vec2 | null = null;
  /** A night mission: the lines glow less (Arte, 18/10: the threshold of the night's bloom doubled them), and a flat wash adds light instead of paint. */
  private night = false;
  /** The tiles' squares round the pointer, brighter the nearer (additive: a fade to nothing). */
  private readonly near: THREE.InstancedMesh;
  /** The range last shown, by tile (x * 1024 + y), for the squares round the pointer. */
  private range = new Map<number, MoveTile>();
  rangeStyle: RangeStyle = 'outline';
  /** Red frames on the tiles where the squad would be noticed, over the cost colour (which still shows). */
  private readonly detect: THREE.InstancedMesh;
  /** Patterns besides colour (UX-REVISION-2 #49): hatching on dash tiles, an eye where the squad would be seen. */
  private readonly hatch: THREE.InstancedMesh;
  private readonly eyes: THREE.InstancedMesh;
  private readonly blast: THREE.InstancedMesh;
  /** Walls a frag grenade will breach (docs/FASE7.md §4). */
  private readonly breach: THREE.InstancedMesh;
  private readonly path = fatLine(0xeafaff, 4);
  /** Under the path, on its geometry: a dark casing so it reads over the tiles («Clásico»; a softer one, TileLook.shade), and a halo of light. */
  private readonly pathCasing = new Line2(this.path.geometry, new LineMaterial({ color: CASING, linewidth: 8, transparent: true, opacity: 0.6, depthTest: false }));
  private readonly pathHalo = new Line2(this.path.geometry, haloMaterial(0xffffff, false, 8, 0.28));
  private readonly aim = fatLine(0xff5a5a, 3);
  /** How far a grenade reaches from the thrower. */
  private readonly rangeRing = fatLine(0xffb070, 2);
  private readonly hover = flatRing(0.3, 0.42, 0xffffff, 0.9);
  private readonly targetRing = flatRing(0.5, 0.62, 0xff4a4a, 0.95);
  private readonly blastCenter = flatRing(0.2, 0.32, 0xff8a2a, 1);
  private readonly partnerHover = flatRing(0.22, 0.3, 0xffffff, 0.8);
  private readonly evac: THREE.InstancedMesh;
  private readonly item = new THREE.Group();
  private readonly shields: THREE.Sprite[] = [];
  /** Objective markers (terminal, relay, VIP) and reinforcement flares, rebuilt on demand. */
  private readonly beacons = new THREE.Group();
  private readonly flares = new THREE.Group();
  /** A siege's doors and barricades that have taken blows: a bar over each (setBreakables). */
  private readonly breakBars = new THREE.Group();
  /** Where a fallen soldier's rucksack lies (playtest 18/10 #49): a tag over it (setFallenPacks). */
  private readonly packTags = new THREE.Group();
  private readonly smoke = new THREE.Group();
  /** Moves planned without the command (UX-REVISION-2 #35): a dashed line in the planner's colour. */
  private readonly plans = new THREE.Group();
  /** While concealed: how close each enemy in sight lets a soldier get unseen (its cone and arm's length), as a faint red light. */
  private readonly detection = new THREE.Group();
  private readonly detectionMaterial = new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, depthWrite: false, side: THREE.DoubleSide });
  /**
   * One enemy's field of view (Arturo, 17/10), under the pointer or aimed at:
   * the tiles it sees, in its cone and its line of sight, washed faintly; the
   * ones it would notice a hidden soldier on, more; and the keyhole's edge.
   */
  private readonly seen: THREE.InstancedMesh;
  private readonly noticed: THREE.InstancedMesh;
  private readonly sightEdge = fatLine(0xff7a5a, 2);
  private readonly shieldTextures = { 1: shieldTexture(1), 2: shieldTexture(2) };
  /** The move range's layers and their full opacity: planning shows them fainter. */
  private rangeLayers: [THREE.InstancedMesh, number][] = [];
  /** Furniture marked for Registrar (armed, or under the pointer): a frame round the tile and a badge over it. */
  private readonly spots = new THREE.Group();
  private readonly spotTextures = { open: spotTexture(false), done: spotTexture(true) };
  /** A four-segment ring turned 45°: a square frame a little wider than the tile, round the furniture. */
  private readonly spotFrame = new THREE.RingGeometry(0.62, 0.74, 4).rotateZ(Math.PI / 4).rotateX(-Math.PI / 2);
  /** Noise spreading from where it was made (docs/FASE2.md): a ring and a faint disc, gone once spread. */
  private readonly noises = new THREE.Group();
  private readonly noiseCircle = new THREE.CircleGeometry(1, 64).rotateX(-Math.PI / 2);
  /** One short line on a tile: what a move or a throw sounds like, what a piece of furniture is. */
  private readonly note = new CSS2DObject(document.createElement('div'));

  constructor(capacity: number) {
    const tileGeometry = new THREE.PlaneGeometry(0.9, 0.9).rotateX(-Math.PI / 2);
    // The wash fills whole tiles, edge to edge: no gaps, so no grid.
    this.tiles = new THREE.InstancedMesh(
      new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.1, depthWrite: false }),
      capacity,
    );
    this.near = new THREE.InstancedMesh(
      new THREE.RingGeometry(0.62, 0.68, 4).rotateZ(Math.PI / 4).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false, depthTest: false }),
      64,
    );
    this.near.count = 0;
    this.near.position.y = 0.03;
    this.near.renderOrder = 9;
    this.near.frustumCulled = false;
    this.walkEdge.renderOrder = this.dashEdge.renderOrder = 9;
    // The glow first, then the shade over it (an additive glow on snow would wash it out), then the line.
    this.walkHalo.renderOrder = this.dashHalo.renderOrder = 7;
    this.walkShade.renderOrder = this.dashShade.renderOrder = 8;
    for (const edge of [this.walkEdge, this.dashEdge, this.walkHalo, this.dashHalo, this.walkShade, this.dashShade]) {
      edge.frustumCulled = false;
      edge.visible = false;
    }
    this.tiles.count = 0;
    this.tiles.position.y = 0.02;
    // A four-segment ring is a square frame; turned 45° its sides follow the tile's.
    this.detect = new THREE.InstancedMesh(
      new THREE.RingGeometry(0.46, 0.63, 4).rotateZ(Math.PI / 4).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xff2a2a, transparent: true, opacity: 0.95, depthWrite: false }),
      capacity,
    );
    this.detect.count = 0;
    this.detect.position.y = 0.025;
    const loader = new THREE.TextureLoader();
    const hatchTexture = loader.load(hatchUrl);
    hatchTexture.wrapS = hatchTexture.wrapT = THREE.RepeatWrapping;
    this.hatch = new THREE.InstancedMesh(
      tileGeometry,
      new THREE.MeshBasicMaterial({ map: hatchTexture, color: 0x2a2318, transparent: true, opacity: 0.32, depthWrite: false }),
      capacity,
    );
    this.hatch.count = 0;
    this.hatch.position.y = 0.022;
    this.eyes = new THREE.InstancedMesh(
      new THREE.PlaneGeometry(0.34, 0.34).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ map: loader.load(eyeUrl), color: 0xff4a4a, transparent: true, opacity: 0.95, depthWrite: false }),
      capacity,
    );
    this.eyes.count = 0;
    this.eyes.position.y = 0.03;
    this.seen = new THREE.InstancedMesh(
      new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xff5a3a, transparent: true, opacity: 0.14, depthWrite: false }),
      capacity,
    );
    this.noticed = new THREE.InstancedMesh(
      new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xff3a2a, transparent: true, opacity: 0.2, depthWrite: false }),
      capacity,
    );
    this.seen.count = this.noticed.count = 0;
    this.seen.position.y = 0.015;
    this.noticed.position.y = 0.016;
    (this.sightEdge.material as LineMaterial).opacity = 0.75;
    this.blast = new THREE.InstancedMesh(
      tileGeometry,
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.4, depthWrite: false }),
      capacity,
    );
    this.blast.count = 0;
    this.blast.position.y = 0.03;
    // A bright square frame halfway up the wall, drawn over it (no depth test), so the wall that goes reads through itself.
    this.breach = new THREE.InstancedMesh(
      new THREE.RingGeometry(0.3, 0.4, 4).rotateZ(Math.PI / 4).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffd08a, transparent: true, opacity: 0.95, depthWrite: false, depthTest: false }),
      64,
    );
    this.breach.count = 0;
    this.breach.renderOrder = 9;

    this.evac = new THREE.InstancedMesh(
      new THREE.PlaneGeometry(0.96, 0.96).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: COLOR_EVAC, transparent: true, opacity: 0.22, depthWrite: false }),
      64,
    );
    this.evac.count = 0;
    this.evac.position.y = 0.015;

    const dotMaterial = (hollow: boolean) => new THREE.MeshBasicMaterial({ map: dotTexture(hollow), transparent: true, depthWrite: false });
    const unit = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
    this.dots = [new THREE.InstancedMesh(unit, dotMaterial(false), capacity), new THREE.InstancedMesh(unit, dotMaterial(true), capacity)];
    this.pathDots = new THREE.InstancedMesh(unit, dotMaterial(false), 256);
    this.pathDots.renderOrder = 10;
    // The strip lies along the tile's north side (-z), its fade's top row on the edge; turned for the others.
    this.fade = new THREE.InstancedMesh(
      new THREE.PlaneGeometry(1, FADE_DEPTH).rotateX(-Math.PI / 2).translate(0, 0, -0.5 + FADE_DEPTH / 2),
      new THREE.MeshBasicMaterial({ map: fadeTexture(), transparent: true, depthWrite: false }),
      capacity,
    );
    for (const mesh of [...this.dots, this.pathDots, this.fade]) mesh.count = 0;

    // Objective: a glowing data core with a beacon of light above it.
    const core = new THREE.Mesh(
      new THREE.BoxGeometry(0.34, 0.34, 0.34),
      new THREE.MeshStandardMaterial({ color: 0x1a3a2a, emissive: 0x4dff9a, emissiveIntensity: 1.2, metalness: 0.4, roughness: 0.3 }),
    );
    core.position.y = 0.45;
    core.castShadow = true;
    const beacon = guideBeam(0x4dff9a);
    const glow = new THREE.PointLight(0x4dff9a, 4, 4, 2);
    glow.position.y = 0.6;
    this.item.add(core, beacon, glow);
    this.item.visible = false;

    for (let i = 0; i < 4; i++) {
      const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.shieldTextures[2], depthTest: false, transparent: true }));
      s.scale.setScalar(0.62);
      s.renderOrder = 11;
      s.visible = false;
      this.shields.push(s);
    }

    // three.js works out an instanced mesh's bounds once, from wherever its
    // instances were then (nowhere, for an empty one), and culls it by them
    // from then on: a grenade area or a move range elsewhere on the map would
    // vanish whenever that first spot left the screen. They are cheap to draw.
    for (const mesh of [this.tiles, this.detect, this.hatch, this.eyes, this.blast, this.breach, this.evac, this.seen, this.noticed, ...this.dots, this.pathDots, this.fade]) mesh.frustumCulled = false;
    this.rangeLayers = [this.tiles, this.hatch, this.detect, this.eyes].map((mesh) => [mesh, (mesh.material as THREE.MeshBasicMaterial).opacity]);

    // The path's halo under it, so it reads over the ground and the range alike.
    this.pathHalo.renderOrder = 8;
    this.pathCasing.renderOrder = 9;
    this.pathCasing.visible = this.pathHalo.visible = false;
    this.rangeRing.renderOrder = 9;
    this.note.element.className = 'tile-note';
    this.note.visible = false;
    this.group.add(this.beacons, this.flares, this.breakBars, this.smoke, this.plans, this.detection, this.evac, this.item, this.tiles, this.hatch, this.detect, this.eyes, this.blast, this.breach, this.pathCasing, this.pathHalo, this.path, this.aim, this.rangeRing, this.hover, this.targetRing, this.blastCenter, this.partnerHover, ...this.shields);
    this.group.add(this.walkShade, this.dashShade, this.walkHalo, this.dashHalo, this.walkEdge, this.dashEdge, this.near, this.seen, this.noticed, this.sightEdge);
    this.group.add(this.spots, this.noises, this.note, this.fade, ...this.dots, this.pathDots, this.packTags);
    this.applyLook();
  }

  /** A night mission or not (game.ts, on each map): the look is drawn again for it. */
  setNight(on: boolean): void {
    if (this.night === on) return;
    this.night = on;
    this.setLook(this.look);
  }

  /**
   * How far past 1 a line goes (TileLook's glow): by night, less (the edges
   * half as much, the path a third: Arte, 18/10); with low effects, never past
   * 1, as there is no tone mapping then and it would clip to a garish colour.
   */
  private glowOf(k: number, path = false): number {
    if (graphics().effects === 'low') return 1;
    return this.night ? 1 + (k - 1) * (path ? 0.35 : 0.5) : k;
  }

  /** Another look for the preview (tileLooks.ts; F8 in the battle): what is on show is drawn again in it. */
  setLook(look: TileLook): void {
    this.look = look;
    this.applyLook();
    this.showMoveRange(this.lastRange.tiles, this.lastRange.planning);
    this.showPath(this.lastPath.from, this.lastPath.path, this.lastPath.dash);
    this.setRangeFocus(this.lastFocus);
    this.plans.userData.key = undefined;
    this.setPlans(this.lastPlans);
  }

  /** The look's widths, dashes, halos and ring on the lines and meshes that keep them. */
  private applyLook(): void {
    this.tintPlanning();
    const { edges, path, ring } = this.look;
    if (edges) {
      for (const edge of [this.walkEdge, this.dashEdge]) edge.material.linewidth = edges.width;
      this.dashEdge.material.dashed = this.dashHalo.material.dashed = this.dashShade.material.dashed = edges.dashDashed;
      for (const shade of [this.walkShade, this.dashShade]) shade.material.linewidth = edges.width + (edges.shadeWidth ?? 4);
      // Their dashes over the line's own.
      for (const under of [this.dashHalo, this.dashShade]) {
        under.material.dashSize = this.dashEdge.material.dashSize;
        under.material.gapSize = this.dashEdge.material.gapSize;
      }
      // Chalk's shadow is dark and plain, under it; light's halo, additive.
      for (const halo of [this.walkHalo, this.dashHalo]) {
        halo.material.blending = edges.chalk ? THREE.NormalBlending : THREE.AdditiveBlending;
        halo.material.linewidth = edges.chalk ? edges.width + 3.5 : 5;
        halo.material.opacity = edges.chalk ? 0.35 : 0.12;
      }
    }
    const line = this.path.material as LineMaterial;
    line.linewidth = path.width;
    line.dashed = path.chalk;
    line.dashSize = 0.2;
    line.gapSize = 0.14;
    const casing = this.pathCasing.material as LineMaterial;
    casing.linewidth = path.casing ? 8 : path.width + 4;
    casing.color.copy(path.casing ? CASING : SHADE);
    if (path.casing) casing.blending = THREE.NormalBlending;
    else shadeBlend(casing);
    // Under a chalk path, dashed as it is.
    casing.dashed = path.chalk;
    casing.dashSize = line.dashSize;
    casing.gapSize = line.gapSize;
    // A flat wash is the look's fill; otherwise the faint one of `soft`. By night it adds light to the ground
    // instead of flattening it like paint (Arte, 18/10), as the lamps' pools do: the colour, scaled down, at full opacity.
    const flat = this.look.fill?.kind === 'flat';
    const lit = flat && this.night;
    const wash = this.tiles.material as THREE.MeshBasicMaterial;
    wash.blending = lit ? THREE.AdditiveBlending : THREE.NormalBlending;
    this.rangeLayers[0] = [this.tiles, lit ? 1 : flat ? this.look.fill!.alpha : 0.1];
    this.hover.geometry.dispose();
    this.hover.geometry = new THREE.RingGeometry(ring[0], ring[1], 36).rotateX(-Math.PI / 2);
  }

  /** Animates the objective beacon and the evac zone pulse. */
  tick(time: number): void {
    if (this.item.visible) {
      const core = this.item.children[0]!;
      core.rotation.y = time * 1.2;
      core.position.y = 0.45 + Math.sin(time * 2.4) * 0.06;
    }
    (this.evac.material as THREE.MeshBasicMaterial).opacity = 0.18 + Math.sin(time * 2.2) * 0.06;
    // The columns breathe a little (their core line; guideBeam).
    for (const b of this.beacons.children) {
      const core = b.children[0]?.children[1] as Line2 | undefined;
      if (core) (core.material as LineMaterial).opacity = 0.85 + Math.sin(time * 2 + b.position.x) * 0.1;
    }
    for (const puff of this.smoke.children) puff.scale.setScalar(1 + Math.sin(time * 0.8 + puff.userData.phase) * 0.08);
    this.flares.children.forEach((f, i) => {
      const k = (time * 0.9 + i * 0.3) % 1;
      const ring = f.children[1] as THREE.Mesh;
      ring.scale.setScalar(0.6 + k * 1.6);
      (ring.material as THREE.MeshBasicMaterial).opacity = 0.8 * (1 - k);
      const smoke = f.children[0] as THREE.Mesh;
      smoke.rotation.y = time * 0.6;
      (smoke.material as THREE.MeshBasicMaterial).opacity = 0.3 + Math.sin(time * 5 + i) * 0.08;
    });
    for (const badge of this.spots.children) if (badge.userData.pick) badge.scale.setScalar(0.6 + Math.sin(time * 5) * 0.06);
    this.tickNoises(time);
  }

  /** Each noise spreads fast and slows to its radius as it fades; bigger noises take longer. */
  private tickNoises(time: number): void {
    for (const ring of [...this.noises.children]) {
      const d = ring.userData as { radius: number; start: number | null; delay: number; duration: number; peak: number };
      // The clock starts at the first frame after the noise.
      d.start ??= time + d.delay;
      const k = (time - d.start) / d.duration;
      if (k < 0) continue;
      const material = (ring as THREE.Mesh).material as THREE.Material;
      if (k >= 1) {
        this.noises.remove(ring);
        if (ring instanceof Line2) ring.geometry.dispose();
        material.dispose();
        continue;
      }
      const r = Math.max(0.05, d.radius * (1 - (1 - k) ** 3));
      ring.scale.set(r, 1, r);
      material.opacity = d.peak * (k < 0.15 ? k / 0.15 : (1 - k) / 0.85);
    }
  }

  /**
   * How far a noise was heard (docs/FASE2.md): a ring spreading to its radius
   * from where it was made, over a faint disc; a loud one sends a second ring
   * after the first and lasts longer. Nothing moves the camera.
   */
  noise(tile: Vec2, radius: number, color: number): void {
    if (radius <= 0) return;
    const at = tileToWorld(tile, 0.08);
    const duration = 0.8 + radius * 0.08;
    const add = (obj: THREE.Object3D, delay: number, peak: number) => {
      obj.position.copy(at);
      obj.scale.setScalar(0.05);
      obj.userData = { radius, start: null, delay, duration, peak };
      this.noises.add(obj);
    };
    const disc = new THREE.Mesh(this.noiseCircle, new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthTest: false, depthWrite: false }));
    disc.renderOrder = 7;
    add(disc, 0, 0.045);
    const points: number[] = [];
    for (let i = 0; i <= 72; i++) points.push(Math.cos((i / 72) * Math.PI * 2), 0, Math.sin((i / 72) * Math.PI * 2));
    for (let i = 0; i < (radius >= 10 ? 2 : 1); i++) {
      const ring = new Line2(new LineGeometry(), new LineMaterial({ color, linewidth: i ? 1.5 : 2.5, transparent: true, opacity: 0, depthTest: false }));
      ring.geometry.setPositions(points);
      ring.renderOrder = 8;
      add(ring, i * 0.35, i ? 0.45 : 0.8);
    }
  }

  /**
   * Furniture marked for Registrar: a frame round each tile and a badge over
   * it (the one it would search, pulsing). `badges` false: frames only (doors).
   */
  setSpots(list: { pos: Vec2; kind: SpotKind }[], badges = true): void {
    const key = JSON.stringify(list) + badges;
    if (this.spots.userData.key === key) return;
    this.spots.userData.key = key;
    clearGroup(this.spots, new Set([this.spotFrame]));
    for (const s of list) {
      const frame = new THREE.Mesh(
        this.spotFrame,
        new THREE.MeshBasicMaterial({ color: SPOT_COLORS[s.kind], transparent: true, opacity: s.kind === 'done' ? 0.55 : 0.95, depthTest: false, depthWrite: false }),
      );
      frame.position.copy(tileToWorld(s.pos, 0.04));
      frame.renderOrder = 12;
      const badge = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.spotTextures[s.kind === 'done' ? 'done' : 'open'], depthTest: false, transparent: true }));
      badge.position.copy(tileToWorld(s.pos, SPOT_HEIGHT));
      badge.scale.setScalar(s.kind === 'pick' ? 0.6 : 0.44);
      badge.renderOrder = 13;
      badge.userData.pick = s.kind === 'pick';
      this.spots.add(frame);
      if (badges) this.spots.add(badge);
      else badge.material.dispose();
    }
  }

  /** One short line on `tile` («Ruido 3: corre», «El armario · ya registrado»), or none. */
  setNote(tile: Vec2 | null, text = '', height = 0.15): void {
    this.note.visible = !!tile && !!text;
    if (!tile || !text) return;
    if (this.note.element.textContent !== text) this.note.element.textContent = text;
    this.note.position.copy(tileToWorld(tile, height));
  }

  /** What a search turned up, rising over the furniture («+12 §», «Un botiquín», «Nada»). */
  find(tile: Vec2, text: string, kind: 'loot' | 'empty' | 'rare' = 'loot'): void {
    const el = document.createElement('div');
    el.className = `float-text find${kind === 'loot' ? '' : ` ${kind}`}`;
    el.textContent = text;
    const label = new CSS2DObject(el);
    label.position.copy(tileToWorld(tile, 1.2));
    this.group.add(label);
    // A found weapon is rare: it stays up longer, to be read.
    void wait(kind === 'rare' ? 3.4 : 2.2).then(() => {
      this.group.remove(label);
      el.remove();
    });
  }

  /** A signal's tag over the spot (game.ts onPing, playtest 18/10 #7): whose, in their colour, for PING_TAG_S. */
  pingTag(tile: Place, who: string, color: string): void {
    const el = document.createElement('div');
    el.className = 'ping-tag';
    el.style.setProperty('--accent', color);
    el.textContent = who;
    const label = new CSS2DObject(el);
    label.position.copy(tileToWorld(tile, 2.9));
    this.group.add(label);
    void wait(PING_TAG_S).then(() => {
      this.group.remove(label);
      el.remove();
    });
  }

  /** Light beams over the mission objectives. */
  setBeacons(list: { pos: { x: number; y: number }; color: number; height?: number }[]): void {
    const key = JSON.stringify(list);
    if (this.beacons.userData.key === key) return;
    this.beacons.userData.key = key;
    for (const child of [...this.beacons.children]) {
      this.beacons.remove(child);
      child.traverse((o) => {
        if (o instanceof THREE.Mesh || o instanceof Line2) {
          o.geometry.dispose();
          (o.material as THREE.Material).dispose();
        }
      });
    }
    for (const b of list) {
      const g = new THREE.Group();
      const beam = guideBeam(b.color, b.height ?? 0);
      const ring = flatRing(0.48, 0.6, b.color, 0.75);
      ring.visible = true;
      ring.position.y = 0.05;
      g.add(beam, ring);
      g.position.copy(tileToWorld(b.pos));
      this.beacons.add(g);
    }
  }

  /** Grey clouds over every smoke grenade still in effect. */
  setSmoke(clouds: { pos: { x: number; y: number }; radius: number }[]): void {
    const key = JSON.stringify(clouds);
    if (this.smoke.userData.key === key) return;
    this.smoke.userData.key = key;
    for (const child of [...this.smoke.children]) this.smoke.remove(child);
    for (const cloud of clouds) {
      const center = tileToWorld(cloud.pos);
      for (let i = 0; i < 9; i++) {
        const a = (i / 9) * Math.PI * 2;
        const r = i === 0 ? 0 : cloud.radius * (0.45 + ((i * 37) % 10) / 20);
        const puff = new THREE.Mesh(
          new THREE.SphereGeometry(0.9 + (i % 3) * 0.25, 10, 8),
          new THREE.MeshBasicMaterial({ color: 0xb8c2cc, transparent: true, opacity: 0.28, depthWrite: false }),
        );
        puff.position.copy(center).add(new THREE.Vector3(Math.cos(a) * r, 0.8 + (i % 2) * 0.5, Math.sin(a) * r));
        puff.userData.phase = i;
        this.smoke.add(puff);
      }
    }
  }

  /** Red smoke flares where reinforcements are about to land. */
  setFlares(list: { x: number; y: number }[]): void {
    const key = JSON.stringify(list);
    if (this.flares.userData.key === key) return;
    this.flares.userData.key = key;
    for (const child of [...this.flares.children]) this.flares.remove(child);
    for (const p of list) {
      const g = new THREE.Group();
      const smoke = new THREE.Mesh(
        new THREE.CylinderGeometry(0.35, 0.9, 5, 12, 1, true).translate(0, 2.5, 0),
        new THREE.MeshBasicMaterial({ color: 0xff3a2a, transparent: true, opacity: 0.32, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide }),
      );
      const ring = flatRing(0.8, 1, 0xff4a3a, 0.8);
      ring.visible = true;
      ring.position.y = 0.06;
      g.add(smoke, ring);
      g.position.copy(tileToWorld(p));
      this.flares.add(g);
    }
  }

  /**
   * A siege's doors and barricades that have taken blows (docs/FASE12.md
   * §10): a short bar over each with what it still holds, red near the end.
   * Whole ones show nothing; what it holds whole is under the cursor (game.ts).
   */
  setBreakables(list: { pos: Place; kind: 'door' | 'barricade'; left: number; full: number }[]): void {
    const hurt = list.filter((b) => b.left < b.full);
    const key = JSON.stringify(hurt);
    if (this.breakBars.userData.key === key) return;
    this.breakBars.userData.key = key;
    for (const child of [...this.breakBars.children]) {
      this.breakBars.remove(child);
      (child as CSS2DObject).element.remove();
    }
    for (const b of hurt) {
      const el = document.createElement('div');
      el.className = `break-bar${b.left * 3 <= b.full ? ' low' : ''}`;
      const track = document.createElement('span');
      const fill = document.createElement('i');
      fill.style.width = `${Math.round((b.left / b.full) * 100)}%`;
      track.append(fill);
      const count = document.createElement('b');
      count.textContent = String(b.left);
      el.append(track, count);
      const label = new CSS2DObject(el);
      label.position.copy(tileToWorld(b.pos, b.kind === 'door' ? 2.4 : 1.3));
      this.breakBars.add(label);
    }
  }

  /** The fallen soldiers' rucksacks on the floor: «MOCHILA DE CHIARA» over each (#49). */
  setFallenPacks(list: { pos: Place; label: string }[]): void {
    const key = JSON.stringify(list);
    if (this.packTags.userData.key === key) return;
    this.packTags.userData.key = key;
    for (const child of [...this.packTags.children]) {
      this.packTags.remove(child);
      (child as CSS2DObject).element.remove();
    }
    for (const p of list) {
      const el = document.createElement('div');
      el.className = 'pack-tag';
      el.textContent = p.label;
      const tag = new CSS2DObject(el);
      tag.position.copy(tileToWorld(p.pos, 0.6));
      this.packTags.add(tag);
    }
  }

  showEvacZone(tiles: Vec2[]): void {
    const m = new THREE.Matrix4();
    tiles.slice(0, this.evac.instanceMatrix.count).forEach((t, i) => {
      m.setPosition(tileToWorld(t));
      this.evac.setMatrixAt(i, m);
    });
    this.evac.count = Math.min(tiles.length, this.evac.instanceMatrix.count);
    this.evac.instanceMatrix.needsUpdate = true;
  }

  /** Objective lying on the ground, or null while carried / secured. */
  setItem(pos: Vec2 | null): void {
    this.item.visible = !!pos;
    if (pos) this.item.position.copy(tileToWorld(pos));
  }

  /**
   * The move range (RangeStyle): its borders, solid blue round what one action
   * reaches and dashed yellow round the dash (line and pattern, not colour
   * alone: UX-REVISION-2 #49), a faint wash inside with `soft`, and a red
   * frame with an eye on a tile that would get the squad noticed. The tiles'
   * own squares only show round the pointer (setRangeFocus).
   * `planning`: without the command the range is only for planning, so it is drawn fainter.
   */
  showMoveRange(tiles: MoveTile[], planning = false): void {
    this.lastRange = { tiles, planning };
    for (const [mesh, opacity] of this.rangeLayers) (mesh.material as THREE.MeshBasicMaterial).opacity = planning ? opacity * 0.5 : opacity;
    const opacity = this.look.edges?.opacity ?? 0.95;
    for (const edge of [this.walkEdge, this.dashEdge]) edge.material.opacity = planning ? opacity * 0.55 : opacity;
    this.planning = planning;
    const { edges, fill, dots } = this.look;
    if (edges) {
      this.walkEdge.material.color.copy(glow(planning ? this.planTint : edges.walk, this.glowOf(edges.glow)));
      this.dashEdge.material.color.copy(glow(planning ? this.planTint : edges.dash, this.glowOf(edges.glow)));
      for (const [halo, hex] of [[this.walkHalo, edges.walk], [this.dashHalo, edges.dash]] as const) halo.material.color.set(edges.chalk ? 0x0c1014 : planning ? this.planTint : hex);
    }
    this.range = new Map(tiles.map((t) => [t.pos.x * 1024 + t.pos.y, t]));
    const m = new THREE.Matrix4();
    const c = new THREE.Color();
    const tint = (walk: number, dash: number, t: MoveTile, k = 1) => (planning ? c.copy(this.planTint) : c.set(t.dash ? dash : walk)).multiplyScalar(k);
    const flat = fill?.kind === 'flat' ? fill : null;
    const dotSize = dots ? (dots.radius * 2) / DOT_CORE : 0;
    const marks = [0, 0];
    let frames = 0;
    let washed = 0;
    for (const t of tiles.slice(0, this.tiles.instanceMatrix.count)) {
      const at = tileToWorld(t.pos);
      m.makeTranslation(at.x, at.y, at.z);
      if (flat || this.rangeStyle === 'soft') {
        this.tiles.setMatrixAt(washed, m);
        this.tiles.setColorAt(washed++, flat ? tint(flat.walk, flat.dash, t, this.night ? (t.dash ? 0.12 : 0.15) : 1) : c.copy(t.dash ? COLOR_DASH : COLOR_MOVE));
      }
      if (dots) {
        const mesh = this.dots[t.dash ? 1 : 0];
        const i = marks[t.dash ? 1 : 0]++;
        mesh.setMatrixAt(i, m.compose(at.setY(at.y + 0.03), IDENTITY, new THREE.Vector3(dotSize, 1, dotSize)));
        mesh.setColorAt(i, tint(dots.walk, dots.dash, t, this.glowOf(this.look.path.glow)));
      }
      if (t.detected) {
        m.setPosition(tileToWorld(t.pos));
        this.detect.setMatrixAt(frames, m);
        this.eyes.setMatrixAt(frames++, m);
      }
    }
    this.tiles.count = washed;
    this.tiles.instanceMatrix.needsUpdate = true;
    if (this.tiles.instanceColor) this.tiles.instanceColor.needsUpdate = true;
    this.dots.forEach((mesh, i) => {
      mesh.count = marks[i]!;
      (mesh.material as THREE.MeshBasicMaterial).opacity = planning ? 0.5 : 0.95;
      mesh.instanceMatrix.needsUpdate = true;
      if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
    });
    this.hatch.count = 0;
    this.detect.count = frames;
    this.detect.instanceMatrix.needsUpdate = true;
    this.eyes.count = frames;
    this.eyes.instanceMatrix.needsUpdate = true;
    this.drawBorders(tiles);
  }

  /**
   * The range's borders, edge by edge: a walk tile's side that faces a dash
   * tile or nothing is on the blue line; a dash tile's side that faces
   * nothing, on the yellow. Each sits at its own tile's height (rooftops).
   */
  private drawBorders(tiles: MoveTile[]): void {
    const walk: number[] = [];
    const dash: number[] = [];
    const { edges, fill } = this.look;
    const fade = fill?.kind === 'fade' ? fill : null;
    const m = new THREE.Matrix4();
    const q = new THREE.Quaternion();
    const c = new THREE.Color();
    const up = new THREE.Vector3(0, 1, 0);
    const one = new THREE.Vector3(1, 1, 1);
    let strips = 0;
    // Each side: the neighbour across it, its ends, and how a «Degradado» strip (drawn on the north side) turns to it.
    const sides = [
      [1, 0, 1, 0, 1, 1, -Math.PI / 2],
      [-1, 0, 0, 0, 0, 1, Math.PI / 2],
      [0, 1, 0, 1, 1, 1, Math.PI],
      [0, -1, 0, 0, 1, 0, 0],
    ] as const;
    for (const t of tiles) {
      const y = tileToWorld(t.pos, 0.06).y;
      for (const [dx, dy, ax, az, bx, bz, turn] of sides) {
        const n = this.range.get((t.pos.x + dx) * 1024 + t.pos.y + dy);
        const out = t.dash ? !n : !n || n.dash;
        if (!out) continue;
        const into = t.dash ? dash : walk;
        const [x0, z0, x1, z1] = [t.pos.x + ax, t.pos.y + az, t.pos.x + bx, t.pos.y + bz];
        if (edges?.chalk) {
          // Two strokes by hand, each wobbling at the corners and halfway along.
          for (const pass of [0, 1]) {
            const pts = [[x0, z0], [(x0 + x1) / 2, (z0 + z1) / 2], [x1, z1]].map(([x, z]) => [x! + wobble(x!, z!, pass * 2), y, z! + wobble(x!, z!, pass * 2 + 1)]);
            into.push(...pts[0]!, ...pts[1]!, ...pts[1]!, ...pts[2]!);
          }
        } else into.push(x0, y, z0, x1, y, z1);
        if (fade && strips < this.fade.instanceMatrix.count) {
          m.compose(tileToWorld(t.pos, 0.03), q.setFromAxisAngle(up, turn), one);
          this.fade.setMatrixAt(strips, m);
          this.fade.setColorAt(strips++, this.planning ? c.copy(this.planTint) : c.set(t.dash ? fade.dash : fade.walk));
        }
      }
    }
    this.fade.count = strips;
    (this.fade.material as THREE.MeshBasicMaterial).opacity = fade ? fade.alpha * (this.planning ? 0.5 : 1) : 0;
    this.fade.instanceMatrix.needsUpdate = true;
    if (this.fade.instanceColor) this.fade.instanceColor.needsUpdate = true;
    for (const [edge, halo, shade, points] of [[this.walkEdge, this.walkHalo, this.walkShade, walk], [this.dashEdge, this.dashHalo, this.dashShade, dash]] as const) {
      edge.visible = !!edges && points.length > 0;
      halo.visible = edge.visible && edges!.halo;
      // A chalk stroke's halo is already its shadow.
      shade.visible = edge.visible && this.look.shade && !edges!.chalk;
      edge.geometry.dispose();
      if (!edge.visible) continue;
      edge.geometry.setPositions(points);
      edge.computeLineDistances();
    }
  }

  /** The tiles' squares round the pointer (`tile`), fading with the distance; none off the range. */
  setRangeFocus(tile: Vec2 | null): void {
    this.lastFocus = tile;
    const m = new THREE.Matrix4();
    const c = new THREE.Color();
    let n = 0;
    if (tile && this.look.near) {
      const r = Math.ceil(NEAR_RADIUS);
      for (let dy = -r; dy <= r; dy++) {
        for (let dx = -r; dx <= r; dx++) {
          const d = Math.hypot(dx, dy);
          const t = d <= NEAR_RADIUS ? this.range.get((tile.x + dx) * 1024 + tile.y + dy) : undefined;
          if (!t || n >= this.near.instanceMatrix.count) continue;
          m.setPosition(tileToWorld(t.pos));
          this.near.setMatrixAt(n, m);
          // Additive: a darker colour is a fainter square.
          this.near.setColorAt(n++, c.copy(t.dash ? COLOR_DASH : COLOR_MOVE).multiplyScalar(0.85 * (1 - d / (NEAR_RADIUS + 0.5))));
        }
      }
    }
    this.near.count = n;
    this.near.instanceMatrix.needsUpdate = true;
    if (this.near.instanceColor) this.near.instanceColor.needsUpdate = true;
  }

  /** The plan's colour as CSS (game.ts drawPlans: one's own plans while the partner holds the command). */
  get planHex(): string {
    return `#${this.planTint.getHexString()}`;
  }

  /** The player's colour (PLAYER_COLORS), for the preview while planning: dimmed towards the night's ink (or the look's own, applyLook). */
  setPlanColor(color: string): void {
    this.playerColor = color;
    this.tintPlanning();
  }

  private playerColor = '#9aa0a4';

  /** The planning colour: the look's (one for everyone: 18/10 #31), else the player's dimmed. */
  private tintPlanning(): void {
    if (this.look.planning !== undefined) this.planTint.set(this.look.planning);
    else this.planTint.set(this.playerColor).lerp(new THREE.Color(0x1c2024), PLAN_DIM);
  }

  /** Planned moves, each a dashed line from the soldier to a ring on its destination. */
  setPlans(list: { from: Vec2; path: Vec2[]; color: string }[]): void {
    this.lastPlans = list;
    const key = JSON.stringify(list);
    if (this.plans.userData.key === key) return;
    this.plans.userData.key = key;
    for (const child of [...this.plans.children]) {
      this.plans.remove(child);
      if (child instanceof Line2 || child instanceof THREE.Mesh) {
        child.geometry.dispose();
        (child.material as THREE.Material).dispose();
      }
    }
    const { plan: style, path } = this.look;
    for (const plan of list) {
      // In the look's line (#14): with a halo, a light's thin core over it.
      const line = new Line2(
        new LineGeometry(),
        new LineMaterial({ color: glow(plan.color, this.glowOf(path.glow, true)), linewidth: style.width, dashed: !style.solid, dashSize: style.dashSize, gapSize: style.gapSize, transparent: true, opacity: 0.9, depthTest: false }),
      );
      line.geometry.setPositions([plan.from, ...plan.path].flatMap((p) => tileToWorld(p, 0.1).toArray()));
      line.computeLineDistances();
      line.renderOrder = 10;
      const ring = flatRing(style.ring[0], style.ring[1], 0xffffff, 0.9);
      (ring.material as THREE.MeshBasicMaterial).color.set(plan.color);
      ring.position.copy(tileToWorld(plan.path[plan.path.length - 1]!, 0.05));
      ring.visible = true;
      if (style.halo) {
        const halo = new Line2(line.geometry, haloMaterial(plan.color));
        halo.renderOrder = 9;
        this.plans.add(halo);
      }
      this.plans.add(line, ring);
    }
  }

  /**
   * Detection radii (playtest: «no he entendido por qué me han visto»): a
   * concealed soldier ending a move inside one, in the enemy's line of sight,
   * is noticed. The red frames on the move range say which tiles exactly.
   */
  setDetection(list: { pos: Place; radius: number; facing?: number }[]): void {
    const key = JSON.stringify(list);
    if (this.detection.userData.key === key) return;
    this.detection.userData.key = key;
    for (const child of [...this.detection.children] as THREE.Mesh[]) {
      this.detection.remove(child);
      child.geometry.dispose();
    }
    // A faint red light on the ground (playtest 18/10 #30: no dashed lines): brightest at the enemy and
    // gone by the keyhole's edge, a fan of triangles from it with the colour fading out to the rim.
    for (const d of list) {
      const c = tileToWorld(d.pos, 0.05);
      const rim = keyhole(c, d.radius, d.facing);
      const pos = [c.x, c.y, c.z, ...rim];
      // RGBA: the red at the enemy, gone (alpha 0) at the rim; a tint, not added light, so it shows on snow too.
      const col = [DETECT_GLOW.r, DETECT_GLOW.g, DETECT_GLOW.b, DETECT_ALPHA];
      for (let i = 0; i < rim.length / 3; i++) col.push(DETECT_GLOW.r, DETECT_GLOW.g, DETECT_GLOW.b, 0);
      const index: number[] = [];
      for (let i = 1; i < rim.length / 3; i++) index.push(0, i, i + 1);
      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
      geometry.setAttribute('color', new THREE.Float32BufferAttribute(col, 4));
      geometry.setIndex(index);
      const fan = new THREE.Mesh(geometry, this.detectionMaterial);
      fan.renderOrder = 3;
      this.detection.add(fan);
    }
  }

  /**
   * One enemy's field of view, or null: `seen`, the tiles it sees (its cone
   * and line of sight, los.ts); `noticed`, those of them it would notice a
   * hidden soldier on; the keyhole's edge out to `range`.
   */
  setVision(v: { pos: Place; facing?: number; range: number; seen: Place[]; noticed: Place[] } | null): void {
    const key = v ? `${v.pos.x},${v.pos.y},${v.pos.z ?? 0},${v.facing ?? '-'},${v.range},${v.seen.length},${v.noticed.length}` : '';
    if (this.seen.userData.key === key) return;
    this.seen.userData.key = key;
    const m = new THREE.Matrix4();
    for (const [mesh, tiles] of [[this.seen, v?.seen ?? []], [this.noticed, v?.noticed ?? []]] as const) {
      const n = Math.min(tiles.length, mesh.instanceMatrix.count);
      for (let i = 0; i < n; i++) {
        m.setPosition(tileToWorld(tiles[i]!));
        mesh.setMatrixAt(i, m);
      }
      mesh.count = n;
      mesh.instanceMatrix.needsUpdate = true;
    }
    // The tiles it sees say it; no edge line any more (18/10 #30).
    this.sightEdge.visible = false;
  }

  showPath(from: Vec2 | null, path: Vec2[] | null, dash = false): void {
    this.lastPath = { from, path, dash };
    this.pathDots.count = 0;
    if (!from || !path?.length) {
      this.path.visible = this.pathCasing.visible = this.pathHalo.visible = false;
      return;
    }
    const style = this.look.path;
    const tone = this.planning ? this.planTint : new THREE.Color(dash ? style.dash : style.color);
    const at = [from, ...path].map((p) => tileToWorld(p, 0.08));
    if (style.dotted) {
      // Marks every third of a tile, like the range's, from the first step on.
      const m = new THREE.Matrix4();
      const size = new THREE.Vector3(0.15, 1, 0.15);
      const p = new THREE.Vector3();
      const color = tone.clone().multiplyScalar(this.glowOf(style.glow, true));
      for (let i = 1; i < at.length; i++) {
        const steps = Math.max(1, Math.round(at[i - 1]!.distanceTo(at[i]!) * 3));
        for (let k = 1; k <= steps && this.pathDots.count < this.pathDots.instanceMatrix.count; k++) {
          this.pathDots.setMatrixAt(this.pathDots.count, m.compose(p.lerpVectors(at[i - 1]!, at[i]!, k / steps), IDENTITY, size));
          this.pathDots.setColorAt(this.pathDots.count++, color);
        }
      }
      this.pathDots.instanceMatrix.needsUpdate = true;
      if (this.pathDots.instanceColor) this.pathDots.instanceColor.needsUpdate = true;
      this.path.visible = this.pathCasing.visible = this.pathHalo.visible = false;
      return;
    }
    // Chalk: by hand, wobbling at each tile and between them; the ends stay put.
    const pts = style.chalk
      ? at.flatMap((v, i) => {
          const mid = i ? v.clone().lerp(at[i - 1]!, 0.5) : null;
          const shake = (w: THREE.Vector3, end: boolean) => (end ? w : w.clone().add(new THREE.Vector3(wobble(w.x, w.z, 4), 0, wobble(w.x, w.z, 5))));
          return [...(mid ? [shake(mid, false)] : []), shake(v, i === 0 || i === at.length - 1)];
        }).flatMap((v) => v.toArray())
      : at.flatMap((v) => v.toArray());
    reshape(this.path, pts);
    if (style.chalk) this.path.computeLineDistances();
    (this.path.material as LineMaterial).color.copy(glow(tone, this.glowOf(style.glow, true)));
    (this.pathHalo.material as LineMaterial).color.copy(tone);
    this.path.visible = true;
    this.pathCasing.visible = style.casing || this.look.shade;
    this.pathHalo.visible = style.halo;
  }

  /** The circle a thrower can reach, or none. */
  showRange(center: Vec2 | null, radius = 0): void {
    this.rangeRing.visible = !!center && radius > 0;
    if (!center || radius <= 0) return;
    const c = tileToWorld(center, 0.06);
    const pts: number[] = [];
    for (let i = 0; i <= 72; i++) {
      const a = (i / 72) * Math.PI * 2;
      pts.push(c.x + Math.cos(a) * radius, c.y, c.z + Math.sin(a) * radius);
    }
    reshape(this.rangeRing, pts);
  }

  /** The pointer's ring colour on a tile of the range (game.ts): the look's, one action away or for the dash. */
  ringColor(dash: boolean): number {
    const [walk, run] = this.look.ringColors ?? [0x9fe8ff, 0xffc93a];
    return dash ? run : walk;
  }

  /** The pointer's ring, in the look's size (applyLook). */
  setHover(tile: Vec2 | null, color = 0xffffff): void {
    this.hover.visible = !!tile;
    if (tile) {
      this.hover.position.copy(tileToWorld(tile, 0.04));
      (this.hover.material as THREE.MeshBasicMaterial).color.set(this.planning && color === 0xffffff ? this.planTint : color);
    }
  }

  /** Shield icons on the sides of `tile` that offer cover. */
  showCover(tile: Vec2 | null, sides: CoverLevel[] = []): void {
    this.shields.forEach((s, i) => {
      const level = sides[i] ?? 0;
      s.visible = !!tile && level > 0;
      if (!tile || level === 0) return;
      const d = DIRS4[i]!;
      s.material.map = this.shieldTextures[level as 1 | 2];
      s.position.copy(tileToWorld(tile, 1.25)).add(new THREE.Vector3(d.x * 0.45, 0, d.y * 0.45));
    });
  }

  /** Line of fire and target ring; green for friendly targets. */
  showAim(from: Vec2 | null, to: Vec2 | null, friendly = false): void {
    this.targetRing.visible = !!to;
    this.aim.visible = !!from && !!to;
    if (!from || !to) return;
    (this.targetRing.material as THREE.MeshBasicMaterial).color.set(friendly ? 0x4dff9a : 0xff4a4a);
    (this.aim.material as LineMaterial).color.set(friendly ? 0x4dff9a : 0xff5a5a);
    this.targetRing.position.copy(tileToWorld(to, 0.05));
    reshape(this.aim, [...tileToWorld(from, 1.1).toArray(), ...tileToWorld(to, 1.1).toArray()]);
  }

  /**
   * Grenade preview; `danger` tiles (own soldiers inside the blast) are drawn
   * red. A throw that can't be made still shows its area, greyed out.
   * `walls`: those it would breach (docs/FASE7.md §4), framed.
   */
  showBlast(center: Vec2 | null, tiles: Vec2[], valid: boolean, danger: Vec2[] = [], walls: Vec2[] = []): void {
    this.blastCenter.visible = !!center;
    if (!center) {
      this.blast.count = 0;
      this.breach.count = 0;
      return;
    }
    const w = new THREE.Matrix4();
    walls.slice(0, 64).forEach((t, i) => {
      w.setPosition(tileToWorld(t, 1));
      this.breach.setMatrixAt(i, w);
    });
    this.breach.count = Math.min(64, walls.length);
    this.breach.instanceMatrix.needsUpdate = true;
    (this.breach.material as THREE.MeshBasicMaterial).opacity = valid ? 0.95 : 0.4;
    this.blastCenter.position.copy(tileToWorld(center, 0.05));
    (this.blastCenter.material as THREE.MeshBasicMaterial).color.set(valid ? COLOR_BLAST : COLOR_DETECT);
    const m = new THREE.Matrix4();
    tiles.forEach((t, i) => {
      m.setPosition(tileToWorld(t));
      this.blast.setMatrixAt(i, m);
      this.blast.setColorAt(i, danger.some((d) => d.x === t.x && d.y === t.y) ? COLOR_DETECT : valid ? COLOR_BLAST : COLOR_OFF);
    });
    this.blast.count = tiles.length;
    this.blast.instanceMatrix.needsUpdate = true;
    if (this.blast.instanceColor) this.blast.instanceColor.needsUpdate = true;
  }

  setPartnerHover(tile: Vec2 | null, color: string): void {
    this.partnerHover.visible = !!tile;
    if (!tile) return;
    this.partnerHover.position.copy(tileToWorld(tile, 0.045));
    (this.partnerHover.material as THREE.MeshBasicMaterial).color.set(color);
  }

  clearAll(): void {
    this.showMoveRange([]);
    this.showPath(null, null);
    this.setHover(null);
    this.showCover(null);
    this.showAim(null, null);
    this.showBlast(null, [], false);
    this.showRange(null);
    this.setSpots([]);
    this.setNote(null);
    this.setRangeFocus(null);
  }
}
