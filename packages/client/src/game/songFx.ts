import * as THREE from 'three';
import { tween, wait } from './tween';

// The look of the Canto de Velmar (docs/LORE.md): every song is made of light
// tiles, voxel cubes glowing in the Coro's green and cyan (the "teselas" that
// sing), so magic reads as the enemy's language and not as fire from a gun.
// Each effect resolves when its moment lands (a bolt hitting, a dome closing)
// so game.ts can show damage after it. The singers' mark, a few cubes
// orbiting the head, is here too.

export const SONG = {
  green: new THREE.Color(0x6dffb0),
  cyan: new THREE.Color(0x6ad8ff),
  white: new THREE.Color(0xe8fff6),
  gold: new THREE.Color(0xffd27a),
  fire: new THREE.Color(0xff9a3a),
} as const;

const cube = new THREE.BoxGeometry(1, 1, 1);

interface Tile {
  mesh: THREE.Mesh;
  vel: THREE.Vector3;
  spin: THREE.Vector3;
  age: number;
  life: number;
  size: number;
  /** Size at the end of its life, as a fraction of `size`. */
  shrink: number;
  gravity: number;
  /** Pulled towards this point instead of flying (tiles gathering into a body). */
  home: THREE.Vector3 | null;
}

/** A glowing material: colours past white bloom in the post-processing. */
function glow(color: THREE.Color, k = 2.2, opacity = 1, xray = false): THREE.MeshBasicMaterial {
  return new THREE.MeshBasicMaterial({
    color: color.clone().multiplyScalar(k),
    transparent: true,
    opacity,
    depthWrite: false,
    depthTest: !xray,
    blending: THREE.AdditiveBlending,
  });
}

const rand = (a: number, b: number) => a + Math.random() * (b - a);
const pick = <T>(xs: readonly T[]) => xs[Math.floor(Math.random() * xs.length)]!;

export class SongFx {
  readonly group = new THREE.Group();
  private tiles: Tile[] = [];
  private readonly scratch = new THREE.Vector3();

  /** Advances the tiles; call every frame. */
  tick(dt: number): void {
    this.tiles = this.tiles.filter((t) => {
      t.age += dt;
      const k = t.age / t.life;
      if (k >= 1) {
        this.group.remove(t.mesh);
        (t.mesh.material as THREE.Material).dispose();
        return false;
      }
      if (t.home) {
        // Gathering: ease towards home, faster as it gets near the end.
        t.mesh.position.lerp(t.home, Math.min(1, dt * (3 + k * 10)));
      } else {
        t.vel.y -= t.gravity * dt;
        t.mesh.position.addScaledVector(t.vel, dt);
        t.vel.multiplyScalar(Math.exp(-dt * 1.5));
      }
      t.mesh.rotation.x += t.spin.x * dt;
      t.mesh.rotation.y += t.spin.y * dt;
      t.mesh.scale.setScalar(t.size * (1 - (1 - t.shrink) * k));
      (t.mesh.material as THREE.MeshBasicMaterial).opacity = 1 - k * k;
      return true;
    });
  }

  /** One light tile at `at`, flying with `vel`. */
  private tile(at: THREE.Vector3, vel: THREE.Vector3, o: { color?: THREE.Color; size?: number; life?: number; gravity?: number; shrink?: number; home?: THREE.Vector3; k?: number; xray?: boolean } = {}): THREE.Mesh {
    const mesh = new THREE.Mesh(cube, glow(o.color ?? pick([SONG.green, SONG.cyan]), o.k ?? 2.2, 1, o.xray));
    mesh.position.copy(at);
    mesh.rotation.set(rand(0, 3), rand(0, 3), 0);
    const size = o.size ?? rand(0.05, 0.11);
    mesh.scale.setScalar(size);
    this.group.add(mesh);
    this.tiles.push({
      mesh,
      vel: vel.clone(),
      spin: new THREE.Vector3(rand(-4, 4), rand(-4, 4), 0),
      age: 0,
      life: o.life ?? rand(0.5, 0.9),
      size,
      shrink: o.shrink ?? 0.2,
      gravity: o.gravity ?? 0,
      home: o.home ?? null,
    });
    return mesh;
  }

  /** A burst of tiles from a point. */
  burst(at: THREE.Vector3, count = 16, speed = 3, color?: THREE.Color): void {
    for (let i = 0; i < count; i++) {
      const dir = new THREE.Vector3(rand(-1, 1), rand(-0.3, 1), rand(-1, 1)).normalize().multiplyScalar(speed * rand(0.4, 1));
      this.tile(at, dir, { color, gravity: 3 });
    }
  }

  /** A thing flying from `from` to `to` (along an arc of height `lift`), shedding tiles. */
  private async fly(from: THREE.Vector3, to: THREE.Vector3, o: { speed: number; lift: number; core: THREE.Mesh; trail: () => void }): Promise<void> {
    const dist = from.distanceTo(to);
    const time = Math.max(0.15, dist / o.speed);
    this.group.add(o.core);
    let shed = 0;
    await tween(time, (k) => {
      const p = this.scratch.lerpVectors(from, to, k);
      p.y += Math.sin(k * Math.PI) * o.lift;
      o.core.position.copy(p);
      o.core.rotation.x += 0.3;
      o.core.rotation.y += 0.2;
      if (k - shed > 0.04) {
        shed = k;
        o.trail();
      }
    });
    this.group.remove(o.core);
  }

  // ------------------------------------------------------------ the songs

  /** Saeta: a bolt of light that never misses. Resolves when it hits. */
  async bolt(from: THREE.Vector3, to: THREE.Vector3): Promise<void> {
    const core = new THREE.Mesh(cube, glow(SONG.white, 3));
    core.scale.setScalar(0.2);
    await this.fly(from, to, {
      speed: 16,
      lift: 0.4,
      core,
      trail: () => this.tile(core.position, new THREE.Vector3(rand(-0.4, 0.4), rand(-0.2, 0.4), rand(-0.4, 0.4)), { color: SONG.cyan, life: 0.4 }),
    });
    this.burst(to, 18, 3.5);
  }

  /** Bola de fuego: a ball of burning tiles lobbed onto the spot, then a blast of fire and song. Resolves at the blast. */
  async fireball(from: THREE.Vector3, to: THREE.Vector3, radius = 2.5): Promise<void> {
    const core = new THREE.Mesh(cube, glow(SONG.fire, 2.6));
    core.scale.setScalar(0.35);
    await this.fly(from, to, {
      speed: 11,
      lift: 1.8,
      core,
      trail: () => {
        for (let i = 0; i < 2; i++) this.tile(core.position, new THREE.Vector3(rand(-0.6, 0.6), rand(0, 0.8), rand(-0.6, 0.6)), { color: pick([SONG.fire, SONG.gold, SONG.green]), life: 0.5 });
      },
    });
    // The blast: fire tiles out to the radius, a ring of the Coro's green on the ground.
    // Fewer, dimmer tiles than a bolt's: overlapping additive tiles burn out to white under the bloom.
    for (let i = 0; i < 46; i++) {
      const dir = new THREE.Vector3(rand(-1, 1), rand(0, 1.2), rand(-1, 1)).normalize().multiplyScalar(radius * rand(1, 2.4));
      this.tile(to, dir, { color: pick([SONG.fire, SONG.fire, SONG.gold, SONG.green]), size: rand(0.08, 0.18), life: rand(0.6, 1.1), gravity: 4, k: 1.3 });
    }
    this.ring(to, radius, SONG.green, 0.9);
  }

  /** Flecha espectral: a pale arrow that goes through cover (drawn through walls). Resolves when it hits. */
  async arrow(from: THREE.Vector3, to: THREE.Vector3): Promise<void> {
    const core = new THREE.Mesh(cube, glow(SONG.cyan, 2.6, 1, true));
    core.scale.set(0.06, 0.06, 0.7);
    core.lookAt(to);
    const look = to.clone();
    await this.fly(from, to, {
      speed: 22,
      lift: 0,
      core,
      trail: () => this.tile(core.position, new THREE.Vector3(), { color: SONG.cyan, life: 0.35, size: 0.06, xray: true }),
    });
    core.lookAt(look);
    this.burst(to, 10, 2.5, SONG.cyan);
  }

  /** Lluvia de luz: an arrow at each target, one after another. */
  async arrows(from: THREE.Vector3, targets: THREE.Vector3[]): Promise<void> {
    await Promise.all(targets.map((t, i) => wait(i * 0.15).then(() => this.arrow(from.clone().add(new THREE.Vector3(0, 0.3 * i, 0)), t))));
  }

  /** Salto de eco / Paso de sombra, first half: the body comes apart in tiles that drift up. */
  async vanish(at: THREE.Vector3): Promise<void> {
    for (let i = 0; i < 46; i++) {
      const p = at.clone().add(new THREE.Vector3(rand(-0.3, 0.3), rand(0.05, 1.7), rand(-0.3, 0.3)));
      this.tile(p, new THREE.Vector3(rand(-0.5, 0.5), rand(0.6, 1.8), rand(-0.5, 0.5)), { life: rand(0.5, 0.9) });
    }
    await wait(0.25);
  }

  /** Second half: tiles gather from around into a body standing there. Resolves when it is whole. */
  async appear(at: THREE.Vector3): Promise<void> {
    for (let i = 0; i < 46; i++) {
      const home = at.clone().add(new THREE.Vector3(rand(-0.25, 0.25), rand(0.05, 1.7), rand(-0.25, 0.25)));
      const p = home.clone().add(new THREE.Vector3(rand(-1.2, 1.2), rand(-0.2, 1.4), rand(-1.2, 1.2)));
      this.tile(p, new THREE.Vector3(), { home, life: 0.55, shrink: 0.6 });
    }
    await wait(0.45);
    this.ring(at, 0.8, SONG.cyan, 0.5);
  }

  /** Silencio: a dome of tiles closing over the zone, then fading. Resolves when it has closed. */
  async dome(center: THREE.Vector3, radius: number): Promise<void> {
    const shell = new THREE.Group();
    shell.position.copy(center);
    const mat = glow(SONG.cyan, 1.4, 0.8);
    for (let lat = 1; lat <= 6; lat++) {
      const phi = (lat / 7) * (Math.PI / 2);
      const ringR = Math.cos(phi);
      const count = Math.max(6, Math.round(28 * ringR));
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + lat * 0.4;
        const m = new THREE.Mesh(cube, mat);
        m.position.set(Math.cos(theta) * ringR, Math.sin(phi), Math.sin(theta) * ringR);
        m.scale.setScalar(0.07);
        shell.add(m);
      }
    }
    this.group.add(shell);
    this.ring(center, radius, SONG.cyan, 1.6);
    await tween(0.45, (k) => shell.scale.setScalar(radius * (0.2 + 0.8 * Math.sin((k * Math.PI) / 2))));
    void tween(1.2, (k) => {
      mat.opacity = 0.8 * (1 - k);
      shell.rotation.y = k * 0.6;
    }).then(() => {
      this.group.remove(shell);
      mat.dispose();
    });
  }

  /** Tormenta de Velmar: lightning out of a sky of tiles onto each target, one after another. Resolves after the last. */
  async storm(targets: THREE.Vector3[]): Promise<void> {
    for (const t of targets) {
      const top = t.clone().add(new THREE.Vector3(rand(-0.5, 0.5), 7, rand(-0.5, 0.5)));
      for (let i = 0; i < 10; i++) this.tile(top.clone().add(new THREE.Vector3(rand(-1, 1), rand(-0.3, 0.3), rand(-1, 1))), new THREE.Vector3(rand(-0.3, 0.3), 0, rand(-0.3, 0.3)), { color: SONG.cyan, life: 0.7, size: 0.16 });
      this.lightning(top, t);
      this.burst(t, 16, 4, SONG.white);
      await wait(0.18);
    }
  }

  /** A jagged bolt of tiles from `a` down to `b`, gone in a flash. */
  private lightning(a: THREE.Vector3, b: THREE.Vector3): void {
    const steps = 14;
    let prev = a.clone();
    for (let i = 1; i <= steps; i++) {
      const k = i / steps;
      const p = a.clone().lerp(b, k);
      if (i < steps) p.add(new THREE.Vector3(rand(-0.35, 0.35), 0, rand(-0.35, 0.35)));
      const seg = prev.distanceTo(p);
      const n = Math.max(1, Math.round(seg / 0.12));
      for (let j = 0; j < n; j++) this.tile(prev.clone().lerp(p, j / n), new THREE.Vector3(), { color: SONG.white, life: 0.28, size: 0.09, shrink: 1, k: 3 });
      prev = p;
    }
  }

  /** A ring of tiles on the ground spreading to `radius`. */
  ring(center: THREE.Vector3, radius: number, color: THREE.Color = SONG.green, life = 0.8): void {
    const count = Math.round(18 + radius * 10);
    for (let i = 0; i < count; i++) {
      const a = (i / count) * Math.PI * 2;
      const dir = new THREE.Vector3(Math.cos(a), 0, Math.sin(a));
      this.tile(center.clone().setY(center.y + 0.08).addScaledVector(dir, 0.3), dir.multiplyScalar(radius * 1.6), { color, life, size: 0.09, shrink: 0.5 });
    }
  }

  /** Auras (Himno, Égida, Juramento, Canto de calma): a ring from the singer and light rising off each ally. */
  aura(center: THREE.Vector3, radius: number, allies: THREE.Vector3[], color: THREE.Color = SONG.green): void {
    this.ring(center, radius, color, 1.1);
    for (const a of allies) this.rise(a, color, 14);
  }

  /** Tiles rising around a body, in a slow spiral (healing, auras on an ally). */
  rise(at: THREE.Vector3, color: THREE.Color = SONG.green, count = 18): void {
    for (let i = 0; i < count; i++) {
      const a = (i / count) * Math.PI * 4;
      const p = at.clone().add(new THREE.Vector3(Math.cos(a) * 0.45, rand(0, 0.4), Math.sin(a) * 0.45));
      this.tile(p, new THREE.Vector3(-Math.sin(a) * 0.4, rand(1, 2), Math.cos(a) * 0.4), { color, life: rand(0.8, 1.3), size: rand(0.05, 0.09) });
    }
  }

  /** Imposición de manos, Manos de luz: light gathering into the wounded. */
  heal(at: THREE.Vector3): void {
    this.rise(at, SONG.green, 20);
    this.burst(at.clone().add(new THREE.Vector3(0, 1, 0)), 8, 1, SONG.white);
  }

  /** Último verso: a column of light out of the sky and tiles gathering into the fallen. Resolves when they stand. */
  async revive(at: THREE.Vector3): Promise<void> {
    const top = at.clone().add(new THREE.Vector3(0, 8, 0));
    for (let i = 0; i < 40; i++) this.tile(top.clone().lerp(at, i / 40).add(new THREE.Vector3(rand(-0.2, 0.2), 0, rand(-0.2, 0.2))), new THREE.Vector3(0, -1, 0), { color: SONG.white, life: 0.9, size: 0.1 });
    await this.appear(at);
    this.rise(at, SONG.gold, 20);
  }

  /** Contracanto: a ring of tiles closing in on the ally and a bright pop. Resolves at the pop. */
  async cleanse(at: THREE.Vector3): Promise<void> {
    const c = at.clone().add(new THREE.Vector3(0, 1, 0));
    for (let i = 0; i < 36; i++) {
      const a = (i / 36) * Math.PI * 2;
      this.tile(c.clone().add(new THREE.Vector3(Math.cos(a) * 1.4, rand(-0.5, 0.5), Math.sin(a) * 1.4)), new THREE.Vector3(), { home: c, life: 0.6, color: pick([SONG.cyan, SONG.white]), size: rand(0.08, 0.13), k: 2.8 });
    }
    await wait(0.4);
    this.burst(c, 14, 3, SONG.white);
  }

  /** Marca del cazador: a sight of tiles turning over the target's head. */
  mark(at: THREE.Vector3): void {
    const c = at.clone().add(new THREE.Vector3(0, 0.9, 0));
    for (let i = 0; i < 16; i++) {
      const a = (i / 16) * Math.PI * 2;
      const p = c.clone().add(new THREE.Vector3(Math.cos(a) * 0.4, 0, Math.sin(a) * 0.4));
      this.tile(p, new THREE.Vector3(-Math.sin(a), 0, Math.cos(a)).multiplyScalar(1.2), { color: SONG.gold, life: 1.2, size: 0.07, shrink: 0.6 });
    }
    for (const [dx, dz] of [
      [0.6, 0],
      [-0.6, 0],
      [0, 0.6],
      [0, -0.6],
    ] as const)
      this.tile(c.clone().add(new THREE.Vector3(dx, 0, dz)), new THREE.Vector3(-dx, 0, -dz).multiplyScalar(0.6), { color: SONG.gold, life: 1, size: 0.1 });
  }

  /** Hoja cantada: the slash of a singing blade, an arc of tiles. */
  blade(at: THREE.Vector3, facing: THREE.Vector3): void {
    const side = new THREE.Vector3(-facing.z, 0, facing.x).normalize();
    for (let i = 0; i < 18; i++) {
      const k = i / 17 - 0.5;
      const p = at.clone().addScaledVector(side, k * 1.1).add(new THREE.Vector3(0, -k * 0.8, 0));
      this.tile(p, facing.clone().multiplyScalar(1.5).addScaledVector(side, k), { color: SONG.cyan, life: 0.45, size: 0.08 });
    }
  }
}

/**
 * What stays on the field after a song: the Silencio zones (a faint dome of
 * tiles over a glowing ring) and the Juramento's aura (a gold ring around
 * whoever keeps the oath). Rebuilt from the state whenever it changes.
 */
export class SongMarks {
  readonly group = new THREE.Group();
  private readonly zones = new THREE.Group();
  private readonly oaths = new THREE.Group();
  private zoneKey = '';
  private oathKey = '';

  constructor() {
    this.group.add(this.zones, this.oaths);
  }

  /** Silencio zones: centre on the ground (world) and radius in tiles. */
  setSilences(list: { at: THREE.Vector3; radius: number }[]): void {
    const key = list.map((z) => `${z.at.x},${z.at.z},${z.radius}`).join('|');
    if (key === this.zoneKey) return;
    this.zoneKey = key;
    this.zones.clear();
    for (const z of list) this.zones.add(tileRing(z.at, z.radius, SONG.cyan, 0.5, 2), domeOfTiles(z.at, z.radius));
  }

  /** Juramento: a ring of gold tiles, radius 4, around each oath keeper. */
  setOaths(list: THREE.Vector3[], radius = 4): void {
    const key = list.map((p) => `${p.x},${p.z}`).join('|');
    if (key === this.oathKey) return;
    this.oathKey = key;
    this.oaths.clear();
    for (const p of list) this.oaths.add(tileRing(p, radius, SONG.gold, 0.45, 1));
  }
}

/** A ring of small glowing tiles lying on the ground, turning slowly. */
function tileRing(center: THREE.Vector3, radius: number, color: THREE.Color, opacity: number, k: number): THREE.Group {
  const g = new THREE.Group();
  g.position.copy(center).setY(center.y + 0.06);
  const mat = glow(color, k, opacity);
  const count = Math.round(radius * 14);
  for (let i = 0; i < count; i++) {
    const a = (i / count) * Math.PI * 2;
    const m = new THREE.Mesh(cube, mat);
    m.position.set(Math.cos(a) * radius, 0, Math.sin(a) * radius);
    m.scale.set(0.12, 0.04, 0.12);
    g.add(m);
  }
  const first = g.children[0] as THREE.Mesh | undefined;
  if (first) first.onBeforeRender = () => (g.rotation.y = (performance.now() / 1000) * 0.15);
  return g;
}

/** A faint dome of tiles (the Silencio zone, standing). */
function domeOfTiles(center: THREE.Vector3, radius: number): THREE.Group {
  const g = new THREE.Group();
  g.position.copy(center);
  const mat = glow(SONG.cyan, 0.9, 0.35);
  for (let lat = 1; lat <= 5; lat++) {
    const phi = (lat / 6) * (Math.PI / 2);
    const r = Math.cos(phi) * radius;
    const count = Math.max(5, Math.round(r * 7));
    for (let i = 0; i < count; i++) {
      const theta = (i / count) * Math.PI * 2 + lat * 0.5;
      const m = new THREE.Mesh(cube, mat);
      m.position.set(Math.cos(theta) * r, Math.sin(phi) * radius * 0.8, Math.sin(theta) * r);
      m.scale.setScalar(0.06);
      g.add(m);
    }
  }
  return g;
}

/**
 * The mark of a soldier who can sing: three small light tiles orbiting the
 * head, faint, so the squad shows who carries the Canto. Returns the group to
 * hang on the head; it turns by itself.
 */
export function singerMark(): THREE.Group {
  const g = new THREE.Group();
  g.name = 'singer-mark';
  const mat = glow(SONG.green, 1.6, 0.85);
  for (let i = 0; i < 3; i++) {
    const m = new THREE.Mesh(cube, mat);
    m.scale.setScalar(0.06);
    const a = (i / 3) * Math.PI * 2;
    m.position.set(Math.cos(a) * 0.32, 0.12 + (i % 2) * 0.08, Math.sin(a) * 0.32);
    m.onBeforeRender = () => {
      const t = performance.now() / 1000;
      g.rotation.y = t * 0.9;
      m.rotation.set(t * 2 + i, t * 1.3, 0);
    };
    g.add(m);
  }
  return g;
}
