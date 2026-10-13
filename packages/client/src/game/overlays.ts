import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { LineSegments2 } from 'three/addons/lines/LineSegments2.js';
import { LineSegmentsGeometry } from 'three/addons/lines/LineSegmentsGeometry.js';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { DIRS4, type CoverLevel, type Vec2 } from '@bastion/engine';
import { tileToWorld } from './coords';
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
const COLOR_DETECT = new THREE.Color(0xff4a4a);
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

export class Overlays {
  readonly group = new THREE.Group();
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
  private readonly path = fatLine(0xeafaff, 4);
  private readonly pathCasing = new Line2(
    this.path.geometry,
    new LineMaterial({ color: 0x081018, linewidth: 8, transparent: true, opacity: 0.6, depthTest: false }),
  );
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
  private readonly smoke = new THREE.Group();
  /** Moves planned without the command (UX-REVISION-2 #35): a dashed line in the planner's colour. */
  private readonly plans = new THREE.Group();
  /** While concealed: how close each enemy in sight lets a soldier get unseen (a dashed red circle). */
  private readonly detection = new THREE.Group();
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
    this.walkEdge.frustumCulled = this.dashEdge.frustumCulled = false;
    this.walkEdge.visible = this.dashEdge.visible = false;
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
    this.blast = new THREE.InstancedMesh(
      tileGeometry,
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.4, depthWrite: false }),
      capacity,
    );
    this.blast.count = 0;
    this.blast.position.y = 0.03;

    this.evac = new THREE.InstancedMesh(
      new THREE.PlaneGeometry(0.96, 0.96).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: COLOR_EVAC, transparent: true, opacity: 0.22, depthWrite: false }),
      64,
    );
    this.evac.count = 0;
    this.evac.position.y = 0.015;

    // Objective: a glowing data core with a beacon of light above it.
    const core = new THREE.Mesh(
      new THREE.BoxGeometry(0.34, 0.34, 0.34),
      new THREE.MeshStandardMaterial({ color: 0x1a3a2a, emissive: 0x4dff9a, emissiveIntensity: 1.2, metalness: 0.4, roughness: 0.3 }),
    );
    core.position.y = 0.45;
    core.castShadow = true;
    const beacon = new THREE.Mesh(
      new THREE.CylinderGeometry(0.08, 0.08, 6, 10, 1, true),
      new THREE.MeshBasicMaterial({ color: 0x4dff9a, transparent: true, opacity: 0.35, blending: THREE.AdditiveBlending, depthWrite: false }),
    );
    beacon.position.y = 3;
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
    for (const mesh of [this.tiles, this.detect, this.hatch, this.eyes, this.blast, this.evac]) mesh.frustumCulled = false;
    this.rangeLayers = [this.tiles, this.hatch, this.detect, this.eyes].map((mesh) => [mesh, (mesh.material as THREE.MeshBasicMaterial).opacity]);

    // The path gets a dark casing, so it reads over the blue and the yellow tiles alike.
    this.pathCasing.renderOrder = 9;
    this.pathCasing.visible = false;
    this.rangeRing.renderOrder = 9;
    this.note.element.className = 'tile-note';
    this.note.visible = false;
    this.group.add(this.beacons, this.flares, this.smoke, this.plans, this.detection, this.evac, this.item, this.tiles, this.hatch, this.detect, this.eyes, this.blast, this.pathCasing, this.path, this.aim, this.rangeRing, this.hover, this.targetRing, this.blastCenter, this.partnerHover, ...this.shields);
    this.group.add(this.walkEdge, this.dashEdge, this.near);
    this.group.add(this.spots, this.noises, this.note);
  }

  /** Animates the objective beacon and the evac zone pulse. */
  tick(time: number): void {
    if (this.item.visible) {
      const core = this.item.children[0]!;
      core.rotation.y = time * 1.2;
      core.position.y = 0.45 + Math.sin(time * 2.4) * 0.06;
    }
    (this.evac.material as THREE.MeshBasicMaterial).opacity = 0.18 + Math.sin(time * 2.2) * 0.06;
    for (const b of this.beacons.children) {
      const beam = b.children[0] as THREE.Mesh;
      (beam.material as THREE.MeshBasicMaterial).opacity = 0.22 + Math.sin(time * 2 + b.position.x) * 0.08;
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

  /** Light beams over the mission objectives. */
  setBeacons(list: { pos: { x: number; y: number }; color: number; height?: number }[]): void {
    const key = JSON.stringify(list);
    if (this.beacons.userData.key === key) return;
    this.beacons.userData.key = key;
    for (const child of [...this.beacons.children]) this.beacons.remove(child);
    for (const b of list) {
      const g = new THREE.Group();
      const beam = new THREE.Mesh(
        new THREE.CylinderGeometry(0.1, 0.1, 7, 10, 1, true),
        new THREE.MeshBasicMaterial({ color: b.color, transparent: true, opacity: 0.3, blending: THREE.AdditiveBlending, depthWrite: false }),
      );
      beam.position.y = 3.5 + (b.height ?? 0);
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
    for (const [mesh, opacity] of this.rangeLayers) (mesh.material as THREE.MeshBasicMaterial).opacity = planning ? opacity * 0.5 : opacity;
    for (const edge of [this.walkEdge, this.dashEdge]) edge.material.opacity = planning ? 0.5 : 0.95;
    this.range = new Map(tiles.map((t) => [t.pos.x * 1024 + t.pos.y, t]));
    const m = new THREE.Matrix4();
    let frames = 0;
    let washed = 0;
    for (const t of tiles.slice(0, this.tiles.instanceMatrix.count)) {
      m.setPosition(tileToWorld(t.pos));
      if (this.rangeStyle === 'soft') {
        this.tiles.setMatrixAt(washed, m);
        this.tiles.setColorAt(washed++, t.dash ? COLOR_DASH : COLOR_MOVE);
      }
      if (t.detected) {
        this.detect.setMatrixAt(frames, m);
        this.eyes.setMatrixAt(frames++, m);
      }
    }
    this.tiles.count = washed;
    this.tiles.instanceMatrix.needsUpdate = true;
    if (this.tiles.instanceColor) this.tiles.instanceColor.needsUpdate = true;
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
    const sides = [
      [1, 0, 1, 0, 1, 1],
      [-1, 0, 0, 0, 0, 1],
      [0, 1, 0, 1, 1, 1],
      [0, -1, 0, 0, 1, 0],
    ] as const;
    for (const t of tiles) {
      const y = tileToWorld(t.pos, 0.06).y;
      for (const [dx, dy, ax, az, bx, bz] of sides) {
        const n = this.range.get((t.pos.x + dx) * 1024 + t.pos.y + dy);
        const out = t.dash ? !n : !n || n.dash;
        if (!out) continue;
        (t.dash ? dash : walk).push(t.pos.x + ax, y, t.pos.y + az, t.pos.x + bx, y, t.pos.y + bz);
      }
    }
    for (const [edge, points] of [[this.walkEdge, walk], [this.dashEdge, dash]] as const) {
      edge.visible = points.length > 0;
      edge.geometry.dispose();
      if (!points.length) continue;
      edge.geometry.setPositions(points);
      edge.computeLineDistances();
    }
  }

  /** The tiles' squares round the pointer (`tile`), fading with the distance; none off the range. */
  setRangeFocus(tile: Vec2 | null): void {
    const m = new THREE.Matrix4();
    const c = new THREE.Color();
    let n = 0;
    if (tile) {
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

  /** Planned moves, each a dashed line from the soldier to a ring on its destination. */
  setPlans(list: { from: Vec2; path: Vec2[]; color: string }[]): void {
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
    for (const plan of list) {
      const line = new Line2(
        new LineGeometry(),
        new LineMaterial({ color: plan.color, linewidth: 3, dashed: true, dashSize: 0.28, gapSize: 0.18, transparent: true, opacity: 0.9, depthTest: false }),
      );
      line.geometry.setPositions([plan.from, ...plan.path].flatMap((p) => tileToWorld(p, 0.1).toArray()));
      line.computeLineDistances();
      line.renderOrder = 10;
      const ring = flatRing(0.3, 0.44, 0xffffff, 0.9);
      (ring.material as THREE.MeshBasicMaterial).color.set(plan.color);
      ring.position.copy(tileToWorld(plan.path[plan.path.length - 1]!, 0.05));
      ring.visible = true;
      this.plans.add(line, ring);
    }
  }

  /**
   * Detection radii (playtest: «no he entendido por qué me han visto»): a
   * concealed soldier ending a move inside one, in the enemy's line of sight,
   * is noticed. The red frames on the move range say which tiles exactly.
   */
  setDetection(list: { pos: Vec2; radius: number }[]): void {
    const key = JSON.stringify(list);
    if (this.detection.userData.key === key) return;
    this.detection.userData.key = key;
    for (const child of [...this.detection.children] as Line2[]) {
      this.detection.remove(child);
      child.geometry.dispose();
      child.material.dispose();
    }
    for (const d of list) {
      const c = tileToWorld(d.pos, 0.07);
      const points: number[] = [];
      for (let i = 0; i <= 96; i++) {
        const a = (i / 96) * Math.PI * 2;
        points.push(c.x + Math.cos(a) * d.radius, c.y, c.z + Math.sin(a) * d.radius);
      }
      const ring = new Line2(
        new LineGeometry(),
        new LineMaterial({ color: 0xff4a4a, linewidth: 2, dashed: true, dashSize: 0.4, gapSize: 0.3, transparent: true, opacity: 0.6, depthTest: false }),
      );
      ring.geometry.setPositions(points);
      ring.computeLineDistances();
      ring.renderOrder = 8;
      this.detection.add(ring);
    }
  }

  showPath(from: Vec2 | null, path: Vec2[] | null, dash = false): void {
    if (!from || !path?.length) {
      this.path.visible = this.pathCasing.visible = false;
      return;
    }
    const pts = [from, ...path].flatMap((p) => tileToWorld(p, 0.08).toArray());
    reshape(this.path, pts);
    (this.path.material as LineMaterial).color.set(dash ? 0xffd34d : 0xeafaff);
    this.path.visible = this.pathCasing.visible = true;
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

  setHover(tile: Vec2 | null, color = 0xffffff): void {
    this.hover.visible = !!tile;
    if (tile) {
      this.hover.position.copy(tileToWorld(tile, 0.04));
      (this.hover.material as THREE.MeshBasicMaterial).color.set(color);
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
   */
  showBlast(center: Vec2 | null, tiles: Vec2[], valid: boolean, danger: Vec2[] = []): void {
    this.blastCenter.visible = !!center;
    if (!center) {
      this.blast.count = 0;
      return;
    }
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
