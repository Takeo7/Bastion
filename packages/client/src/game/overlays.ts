import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { DIRS4, type CoverLevel, type Vec2 } from '@bastion/engine';
import { tileToWorld } from './coords';

export interface MoveTile {
  pos: Vec2;
  dash: boolean;
  /** Ending here would get the concealed squad noticed. */
  detected: boolean;
}

const COLOR_MOVE = new THREE.Color(0x3aa0ff);
const COLOR_DASH = new THREE.Color(0xffc93a);
const COLOR_DETECT = new THREE.Color(0xff4a4a);
const COLOR_BLAST = new THREE.Color(0xff8a2a);
const COLOR_EVAC = new THREE.Color(0x4dff9a);

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

export class Overlays {
  readonly group = new THREE.Group();
  private readonly tiles: THREE.InstancedMesh;
  private readonly blast: THREE.InstancedMesh;
  private readonly path = fatLine(0xd9f4ff, 4);
  private readonly aim = fatLine(0xff5a5a, 3);
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
  private readonly shieldTextures = { 1: shieldTexture(1), 2: shieldTexture(2) };

  constructor(capacity: number) {
    const tileGeometry = new THREE.PlaneGeometry(0.9, 0.9).rotateX(-Math.PI / 2);
    this.tiles = new THREE.InstancedMesh(
      tileGeometry,
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.32, depthWrite: false }),
      capacity,
    );
    this.tiles.count = 0;
    this.tiles.position.y = 0.02;
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

    this.group.add(this.beacons, this.flares, this.smoke, this.evac, this.item, this.tiles, this.blast, this.path, this.aim, this.hover, this.targetRing, this.blastCenter, this.partnerHover, ...this.shields);
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

  showMoveRange(tiles: MoveTile[]): void {
    const m = new THREE.Matrix4();
    tiles.slice(0, this.tiles.instanceMatrix.count).forEach((t, i) => {
      m.setPosition(tileToWorld(t.pos));
      this.tiles.setMatrixAt(i, m);
      this.tiles.setColorAt(i, t.detected ? COLOR_DETECT : t.dash ? COLOR_DASH : COLOR_MOVE);
    });
    this.tiles.count = tiles.length;
    this.tiles.instanceMatrix.needsUpdate = true;
    if (this.tiles.instanceColor) this.tiles.instanceColor.needsUpdate = true;
  }

  showPath(from: Vec2 | null, path: Vec2[] | null, dash = false): void {
    if (!from || !path?.length) {
      this.path.visible = false;
      return;
    }
    const pts = [from, ...path].flatMap((p) => tileToWorld(p, 0.08).toArray());
    (this.path.geometry as LineGeometry).setPositions(pts);
    this.path.computeLineDistances();
    (this.path.material as LineMaterial).color.set(dash ? 0xffe08a : 0xd9f4ff);
    this.path.visible = true;
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
    (this.aim.geometry as LineGeometry).setPositions([...tileToWorld(from, 1.1).toArray(), ...tileToWorld(to, 1.1).toArray()]);
    this.aim.computeLineDistances();
  }

  /** Grenade preview; `danger` tiles (own soldiers inside the blast) are drawn red. */
  showBlast(center: Vec2 | null, tiles: Vec2[], valid: boolean, danger: Vec2[] = []): void {
    this.blastCenter.visible = !!center;
    if (!center) {
      this.blast.count = 0;
      return;
    }
    this.blastCenter.position.copy(tileToWorld(center, 0.05));
    (this.blastCenter.material as THREE.MeshBasicMaterial).color.set(valid ? COLOR_BLAST : COLOR_DETECT);
    const m = new THREE.Matrix4();
    const shown = valid ? tiles : [];
    shown.forEach((t, i) => {
      m.setPosition(tileToWorld(t));
      this.blast.setMatrixAt(i, m);
      this.blast.setColorAt(i, danger.some((d) => d.x === t.x && d.y === t.y) ? COLOR_DETECT : COLOR_BLAST);
    });
    this.blast.count = shown.length;
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
  }
}
