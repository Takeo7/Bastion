import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { NIGHT } from './night';
import { fxTexture, loadModel } from './synty';
import { FX_MODELS, type FxModelId, type FxTextureId } from './syntyManifest';
import { tween, wait } from './tween';

// Transient effects, in the look of Synty's Particle FX (syntyManifest.ts,
// FX_MODELS and FX_TEXTURES): smoke, dust and gas are faceted puffs that
// swell and shrink away, blood and gore are flat polygon drops and low-poly
// chunks, flashes are sparkle stars, fire is a lick of flame shapes. Light
// itself (flashes, tracers, sparks) stays as soft additive glows.

/** Soft radial glow used for flashes, sparks and tracers. */
function glowTexture(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d')!;
  const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grad.addColorStop(0, 'rgba(255,255,255,1)');
  grad.addColorStop(0.35, 'rgba(255,255,255,0.6)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 64, 64);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

// ------------------------------------------------------------------ shapes

/**
 * Particle FX's meshes, a unit across and centred, positions only (faceted
 * materials shade each face flat). A low-poly stand-in serves until each
 * loads; the shapes load with the first Fx.
 */
const SHAPES = new Map<FxModelId, THREE.BufferGeometry>();
const STAND_IN = new THREE.IcosahedronGeometry(0.5, 0);
let shapesLoading = false;

/** A Particle FX shape (shared: don't dispose it). */
export function fxShape(id: FxModelId): THREE.BufferGeometry {
  loadShapes();
  return SHAPES.get(id) ?? STAND_IN;
}

const shape = fxShape;

/** The model's first mesh as plain float positions, centred and scaled to a unit across. */
function unitShape(model: THREE.Object3D): THREE.BufferGeometry | null {
  let mesh: THREE.Mesh | null = null;
  model.traverse((o) => {
    if (!mesh && o instanceof THREE.Mesh) mesh = o;
  });
  if (!mesh) return null;
  const m = mesh as THREE.Mesh;
  model.updateMatrixWorld(true);
  const src = m.geometry.getAttribute('position');
  const out = new Float32Array(src.count * 3);
  const v = new THREE.Vector3();
  for (let i = 0; i < src.count; i++) v.fromBufferAttribute(src, i).applyMatrix4(m.matrixWorld).toArray(out, i * 3);
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(out, 3));
  if (m.geometry.index) g.setIndex(Array.from(m.geometry.index.array));
  g.computeBoundingBox();
  const box = g.boundingBox!;
  const size = box.getSize(v);
  const centre = box.getCenter(new THREE.Vector3());
  g.translate(-centre.x, -centre.y, -centre.z);
  g.scale(1 / Math.max(size.x, size.y, size.z), 1 / Math.max(size.x, size.y, size.z), 1 / Math.max(size.x, size.y, size.z));
  g.computeVertexNormals();
  g.computeBoundingSphere();
  return g;
}

function loadShapes(): void {
  if (shapesLoading) return;
  shapesLoading = true;
  for (const [id, f] of Object.entries(FX_MODELS) as [FxModelId, (typeof FX_MODELS)[FxModelId]][]) {
    void loadModel(f.pack, 'fx', id)
      .then((model) => {
        const g = unitShape(model);
        if (g) SHAPES.set(id, g);
      })
      .catch(() => undefined);
  }
}

// --------------------------------------------------------------- materials

const solids = new Map<number, THREE.MeshStandardMaterial>();
const hots = new Map<number, THREE.MeshBasicMaterial>();

/** Faceted and lit (smoke, dust, gore, chips): one per colour, shared. */
function solid(color: number): THREE.MeshStandardMaterial {
  let m = solids.get(color);
  if (!m) solids.set(color, (m = new THREE.MeshStandardMaterial({ color, flatShading: true, roughness: 0.95 })));
  return m;
}

/** Unlit and bright enough to bloom (flames, embers): one per colour, shared. */
function hot(color: number, k = 1.6): THREE.MeshBasicMaterial {
  const key = color * 10 + Math.round(k * 4);
  let m = hots.get(key);
  if (!m) hots.set(key, (m = new THREE.MeshBasicMaterial({ color: new THREE.Color(color).multiplyScalar(k) })));
  return m;
}

const GORE: FxModelId[] = ['goreChunk1', 'goreChunk2', 'goreChunk3'];
const GORE_COLORS = [0x5a1410, 0x7a1e14, 0x3e0c08];
const BLOOD = [0x6a0c08, 0x8a120c, 0x4a0805];
/** The Chillón's shriek: a pale, dusty white, not a colour of magic. */
const SHRIEK = 0xf0e4d4;

interface Particle {
  obj: THREE.Object3D;
  /** Its own material, faded and disposed with it; null: a shared one, and it shrinks away instead. */
  material: (THREE.Material & { opacity: number }) | null;
  velocity: THREE.Vector3;
  spin: THREE.Vector3;
  gravity: number;
  /** How fast it slows down (smoke, dust), per second. */
  drag: number;
  age: number;
  life: number;
  grow: number;
  base: THREE.Vector3;
  opacity: number;
}

const streakGeometry = new THREE.CylinderGeometry(0.018, 0.018, 0.9, 5, 1, true).rotateX(Math.PI / 2);
const heavyStreakGeometry = new THREE.CylinderGeometry(0.04, 0.04, 1.6, 6, 1, true).rotateX(Math.PI / 2);
const slashGeometry = new THREE.RingGeometry(0.45, 0.6, 24, 1, 0, Math.PI * 0.85);
const bubbleGeometry = new THREE.SphereGeometry(0.85, 20, 14);
const missileGeometry = new THREE.CylinderGeometry(0.04, 0.05, 0.32, 6).rotateX(Math.PI / 2);
const beamGeometry = new THREE.CylinderGeometry(0.5, 0.5, 14, 20, 1, true);
const pingRingGeometry = new THREE.RingGeometry(0.42, 0.55, 32).rotateX(-Math.PI / 2);
/** How tall a ping's beam stands (tiles): from the ground past the top of any camera's view. */
const PING_HEIGHT = 84;
/** Faceted, like Synty's blasts. */
const blastGeometry = new THREE.IcosahedronGeometry(1, 1);
const shellGeometry = new THREE.SphereGeometry(0.09, 10, 8);
/** A thrown stone (docs/FASE10.md §4), ours: a faceted lump, a little flat. */
const stoneGeometry = new THREE.IcosahedronGeometry(0.06, 0).scale(1, 0.72, 0.88);
const stoneMaterial = new THREE.MeshStandardMaterial({ color: 0x77736b, roughness: 1, flatShading: true });
const debrisGeometry = new THREE.BoxGeometry(0.12, 0.12, 0.12);
const groundGeometry = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
const screamRingGeometry = new THREE.TorusGeometry(1, 0.04, 4, 40);

const rand = (a: number, b: number) => a + Math.random() * (b - a);

/**
 * How many lights the effects have, always in the scene, dark while unused (playtest 21/10 #5): a light added for a
 * shot's flash changed how many lights every lit material counts, so each drew with another program the first time
 * something was lit; on Windows (ANGLE over D3D11) every one of them was finished there and then, at the first shot,
 * for seconds. A flash and an impact at once, or a flare and a flash; one more takes the dimmest.
 */
const LAMPS = 2;

/** One of the effects' lights while an effect has it: `set` its brightness, `done` it gives it back (both do nothing once another took it). */
interface Lamp {
  set(intensity: number): void;
  done(): void;
}

/** Transient visual effects. Particles advance in `tick`, driven by the render loop. */
export class Fx {
  readonly group = new THREE.Group();
  private readonly glow = glowTexture();
  private particles: Particle[] = [];
  /** samples(): one of each kind of thing drawn, kept for the battle. */
  private kinds: THREE.Group | null = null;
  /** How big and bright the glow of a shot's impact is (1 in battle; the trailer's close shots, lab/trailer/sala.ts, take less: bloomed up close it swallowed the body). */
  impactGlow = 1;
  /** The effects' lights (LAMPS), and which use of each is the current one. */
  private readonly lamps = Array.from({ length: LAMPS }, () => new THREE.PointLight(0xffffff, 0, 6, 2));
  private readonly lampUse = new Array<number>(LAMPS).fill(0);

  constructor() {
    loadShapes();
    this.group.add(...this.lamps);
  }

  /** A light at `at` for an effect: a dark one, or the dimmest if all are lit. */
  private lamp(at: THREE.Vector3, color: number, intensity: number, distance: number): Lamp {
    let k = this.lamps.findIndex((l) => l.intensity === 0);
    if (k < 0) k = this.lamps.reduce((best, l, i) => (l.intensity < this.lamps[best]!.intensity ? i : best), 0);
    const light = this.lamps[k]!;
    const use = ++this.lampUse[k]!;
    light.color.set(color);
    light.distance = distance;
    light.position.copy(at);
    light.intensity = intensity;
    const mine = () => this.lampUse[k] === use;
    return {
      set: (i) => {
        if (mine()) light.intensity = Math.max(0, i);
      },
      done: () => {
        if (mine()) light.intensity = 0;
      },
    };
  }

  tick(dt: number): void {
    const list = this.particles;
    if (!list.length) return;
    // Compacted in place: no new array a frame.
    let kept = 0;
    for (let i = 0; i < list.length; i++) {
      const p = list[i]!;
      p.age += dt;
      const t = p.age / p.life;
      if (t >= 1) {
        this.group.remove(p.obj);
        p.material?.dispose();
        continue;
      }
      list[kept++] = p;
      p.velocity.y -= p.gravity * dt;
      if (p.drag) p.velocity.multiplyScalar(Math.exp(-p.drag * dt));
      p.obj.position.addScaledVector(p.velocity, dt);
      if (p.gravity && p.obj.position.y < 0.06) {
        p.obj.position.y = 0.06;
        p.velocity.y *= -0.35;
        p.velocity.x *= 0.55;
        p.velocity.z *= 0.55;
      }
      p.obj.rotation.x += p.spin.x * dt;
      p.obj.rotation.y += p.spin.y * dt;
      p.obj.rotation.z += p.spin.z * dt;
      const size = 1 + p.grow * t;
      if (p.material) {
        p.obj.scale.copy(p.base).multiplyScalar(size);
        p.material.opacity = p.opacity * (1 - t * t);
      } else {
        // Pops in, then shrinks away (no fading: Synty's shapes are opaque).
        p.obj.scale.copy(p.base).multiplyScalar(size * Math.min(1, 0.3 + t * 7) * (1 - t * t * t));
      }
    }
    list.length = kept;
  }

  /**
   * One of each kind of thing the effects draw (a glow, a streak, a ring, a decal, a flame, a puff, a shell, a piece
   * fading), for the renderer to compile behind the loading veil (Renderer.warm; game.ts): made at their first use,
   * they compiled on the spot, at the first shot (playtest 20/10 #8). Kept, never drawn: while one material uses a
   * program, three.js keeps it, and the effects' own are made and disposed shot by shot.
   */
  samples(): THREE.Group {
    if (this.kinds) return this.kinds;
    const kinds = new THREE.Group();
    const mesh = (m: THREE.Material) => kinds.add(new THREE.Mesh(debrisGeometry, m));
    kinds.add(this.glowSprite(0xffffff, 1), this.sprite('sparkle', 0xffffff, 1, 1, true));
    mesh(new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 1, blending: THREE.AdditiveBlending, depthWrite: false }));
    mesh(new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 1, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide }));
    mesh(new THREE.MeshBasicMaterial({ map: fxTexture('sparkle'), transparent: true, opacity: 1, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2 }));
    mesh(new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.9, depthWrite: false }));
    mesh(hot(0xffb030, 1.3));
    mesh(solid(0x8a8a86));
    mesh(stoneMaterial);
    mesh(new THREE.MeshStandardMaterial({ color: 0x9aa3ad, emissive: 0xff7a2a, emissiveIntensity: 0.6 }));
    mesh(new THREE.MeshStandardMaterial({ color: 0x3b4a3a }));
    mesh(new THREE.MeshStandardMaterial({ color: 0xffffff, transparent: true, opacity: 1 }));
    return (this.kinds = kinds);
  }

  /** Takes every effect away at once, lights and things in flight included (the lights themselves stay, dark). */
  clear(): void {
    for (const p of this.particles) p.material?.dispose();
    this.particles = [];
    this.group.clear();
    for (let k = 0; k < LAMPS; k++) {
      this.lampUse[k]!++;
      this.lamps[k]!.intensity = 0;
    }
    this.group.add(...this.lamps);
  }

  private spawn(
    obj: THREE.Object3D,
    material: Particle['material'],
    opts: Partial<Omit<Particle, 'obj' | 'material' | 'base'>> & { baseScale?: number },
  ): void {
    const p: Particle = {
      obj,
      material,
      velocity: opts.velocity ?? new THREE.Vector3(),
      spin: opts.spin ?? new THREE.Vector3(),
      gravity: opts.gravity ?? 0,
      drag: opts.drag ?? 0,
      age: 0,
      life: opts.life ?? 0.5,
      grow: opts.grow ?? 0,
      base: opts.baseScale !== undefined ? new THREE.Vector3().setScalar(opts.baseScale) : obj.scale.clone(),
      opacity: opts.opacity ?? 1,
    };
    // Its size at birth (it would otherwise show at full size until the next tick).
    obj.scale.copy(p.base).multiplyScalar(material ? 1 : 0.3);
    this.particles.push(p);
    this.group.add(obj);
  }

  private glowSprite(color: number, scale: number, opacity = 1): THREE.Sprite {
    const material = new THREE.SpriteMaterial({ map: this.glow, color, transparent: true, opacity, blending: THREE.AdditiveBlending, depthWrite: false });
    const sprite = new THREE.Sprite(material);
    sprite.scale.setScalar(scale);
    return sprite;
  }

  /** One of Particle FX's sprites, tinted; `additive` for light. */
  private sprite(tex: FxTextureId, color: number, scale: number, opacity = 1, additive = false): THREE.Sprite {
    const material = new THREE.SpriteMaterial({
      map: fxTexture(tex),
      color,
      transparent: true,
      opacity,
      depthWrite: false,
      rotation: Math.random() * Math.PI * 2,
      blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending,
    });
    const s = new THREE.Sprite(material);
    s.scale.setScalar(scale);
    return s;
  }

  // ---------------------------------------------------------- the toolkit

  /**
   * Faceted puffs (smoke, dust, gas, a bandage's lint): opaque and lit, they
   * swell and shrink away. `spread` scatters them round `at`, `rise` lifts
   * them, `out` pushes them away from the middle.
   */
  puffs(at: THREE.Vector3, color: number, count: number, o: { size?: number; spread?: number; rise?: number; out?: number; life?: number; grow?: number } = {}): void {
    const size = o.size ?? 0.4;
    for (let i = 0; i < count; i++) {
      const a = Math.random() * Math.PI * 2;
      const d = Math.random() * (o.spread ?? 0.3);
      const puff = new THREE.Mesh(shape('smokePuff'), solid(color));
      puff.position.copy(at).add(new THREE.Vector3(Math.cos(a) * d, rand(0, 0.15), Math.sin(a) * d));
      puff.rotation.set(rand(0, 6), rand(0, 6), rand(0, 6));
      const out = o.out ?? 0.3;
      this.spawn(puff, null, {
        velocity: new THREE.Vector3(Math.cos(a) * out * rand(0.5, 1), (o.rise ?? 0.5) * rand(0.6, 1.2), Math.sin(a) * out * rand(0.5, 1)),
        spin: new THREE.Vector3(rand(-0.6, 0.6), rand(-0.6, 0.6), 0),
        drag: 1.2,
        life: (o.life ?? 1.2) * rand(0.75, 1.25),
        grow: o.grow ?? 0.8,
        baseScale: size * rand(0.7, 1.3),
      });
    }
  }

  /** Licks of flame rising from round `at` (within `radius`), with a few embers. */
  flames(at: THREE.Vector3, count: number, radius = 0.3, o: { size?: number; life?: number } = {}): void {
    // Yellower than fire looks: past white the tone mapping turns a saturated orange pink.
    const colors = [0xffb030, 0xffd050, 0xff9a20, 0xffe080];
    for (let i = 0; i < count; i++) {
      const a = Math.random() * Math.PI * 2;
      const d = Math.sqrt(Math.random()) * radius;
      const flame = new THREE.Mesh(shape('flame'), hot(colors[i % colors.length]!, 1.3));
      const size = (o.size ?? 0.45) * rand(0.6, 1.2);
      flame.position.copy(at).add(new THREE.Vector3(Math.cos(a) * d, size * 0.4, Math.sin(a) * d));
      flame.rotation.y = rand(0, 6);
      this.spawn(flame, null, {
        velocity: new THREE.Vector3(0, rand(0.3, 0.8), 0),
        spin: new THREE.Vector3(0, rand(-2, 2), 0),
        life: (o.life ?? 0.7) * rand(0.6, 1.3),
        grow: 0.3,
        baseScale: size,
      });
    }
    this.embers(at, Math.ceil(count / 2), radius);
  }

  /** Embers (or a flare's sparks, in its colour) drifting up off a fire. */
  embers(at: THREE.Vector3, count: number, radius = 0.3, color?: number): void {
    for (let i = 0; i < count; i++) {
      const s = this.glowSprite(color ?? (i % 2 ? 0xffa040 : 0xff6a20), rand(0.06, 0.11));
      s.position.copy(at).add(new THREE.Vector3(rand(-radius, radius), rand(0.1, 0.5), rand(-radius, radius)));
      this.spawn(s, s.material, { velocity: new THREE.Vector3(rand(-0.3, 0.3), rand(0.8, 1.6), rand(-0.3, 0.3)), life: rand(0.6, 1.2) });
    }
  }

  /** A sparkle star and its light: a gun's mouth, a bang, a flare catching. */
  flash(at: THREE.Vector3, color: number, size = 0.8, light = 9, life = 0.09): void {
    const star = this.sprite('sparkle', color, size, 1, true);
    star.position.copy(at);
    this.spawn(star, star.material, { life, grow: 0.5 });
    const core = this.glowSprite(0xffffff, size * 0.45);
    core.position.copy(at);
    this.spawn(core, core.material, { life: life * 0.7 });
    if (!light) return;
    const lamp = this.lamp(at, color, light, 6);
    void tween(Math.max(0.08, life), (t) => lamp.set(light * (1 - t))).then(lamp.done);
  }

  /** A burning flare lying at `at`: a flickering glow and its light, for `life` seconds. */
  flare(at: THREE.Vector3, color: number, life = 2.5): void {
    const glow = this.glowSprite(color, 0.7, 0.9);
    glow.position.copy(at).setY(at.y + 0.12);
    this.spawn(glow, glow.material, { life, opacity: 0.9 });
    const lamp = this.lamp(at.clone().setY(at.y + 0.35), color, 6, 5);
    void tween(life, (t) => lamp.set(6 * (1 - t * t) * (0.8 + Math.random() * 0.4))).then(lamp.done);
  }

  /** An electric jolt at `at`: two crossed bolts, a cold flash and sparks (the defibrillator). */
  zap(at: THREE.Vector3, color = 0xbfe8ff): void {
    for (const turn of [0.5, -0.4]) {
      const bolt = this.sprite('lightning', color, 1, 1, true);
      bolt.material.rotation = turn;
      bolt.scale.set(0.22, 1.1, 1);
      bolt.position.copy(at);
      this.spawn(bolt, bolt.material, { life: 0.16 });
    }
    this.flash(at, color, 0.45, 4, 0.1);
    this.sparks(at, color, 8);
  }

  /** A sight closing on a target (at its chest), and a ring tightening on the ground under it. */
  reticle(at: THREE.Vector3, color: number): void {
    const sight = this.sprite('shockRing', color, 1.4, 0.9, true);
    sight.position.copy(at);
    this.spawn(sight, sight.material, { life: 0.7, grow: -0.65, opacity: 0.9 });
    this.decal(at.clone().setY(0), 'shockRing', color, 1.6, 1.2, 0.8, -0.55);
  }

  /** A faint light left hanging in the air (a bolt's or a flare's trail). */
  trail(at: THREE.Vector3, color: number, size = 0.14, life = 0.3): void {
    const s = this.glowSprite(color, size, 0.6);
    s.position.copy(at);
    this.spawn(s, s.material, { life, opacity: 0.6 });
  }

  /** A sprite lying on the ground (a crack, a ring, a splash), fading over `life`. */
  decal(at: THREE.Vector3, tex: FxTextureId, color: number, size: number, life: number, opacity = 0.85, grow = 0): THREE.Mesh {
    const material = new THREE.MeshBasicMaterial({ map: fxTexture(tex), color, transparent: true, opacity, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2 });
    const mesh = new THREE.Mesh(groundGeometry, material);
    mesh.position.copy(at).setY(0.02 + Math.random() * 0.01);
    mesh.rotation.y = Math.random() * Math.PI * 2;
    mesh.scale.setScalar(size);
    this.spawn(mesh, material, { life, opacity, grow });
    return mesh;
  }

  /** Low-poly gore flying out and bouncing. */
  gore(at: THREE.Vector3, count: number, speed = 4): void {
    for (let i = 0; i < count; i++) {
      const chunk = new THREE.Mesh(shape(GORE[i % GORE.length]!), solid(GORE_COLORS[i % GORE_COLORS.length]!));
      chunk.position.copy(at);
      const dir = new THREE.Vector3(rand(-1, 1), rand(0.5, 1.4), rand(-1, 1)).normalize();
      this.spawn(chunk, null, {
        velocity: dir.multiplyScalar(speed * rand(0.5, 1)),
        spin: new THREE.Vector3(rand(-10, 10), rand(-10, 10), 0),
        gravity: 12,
        life: rand(1.6, 2.6),
        baseScale: rand(0.07, 0.15),
      });
    }
  }

  /** Splinters (wood, glass, paper) flying out. */
  chips(at: THREE.Vector3, color: number, count: number, speed = 3): void {
    for (let i = 0; i < count; i++) {
      const chip = new THREE.Mesh(shape('woodChip'), solid(color));
      chip.position.copy(at);
      const dir = new THREE.Vector3(rand(-1, 1), rand(0.4, 1.2), rand(-1, 1)).normalize();
      this.spawn(chip, null, {
        velocity: dir.multiplyScalar(speed * rand(0.5, 1)),
        spin: new THREE.Vector3(rand(-14, 14), rand(-14, 14), rand(-14, 14)),
        gravity: 11,
        life: rand(0.9, 1.5),
        baseScale: rand(0.09, 0.16),
      });
    }
  }

  /** Small lights rising round a body (an aura, a heal). */
  motes(at: THREE.Vector3, color: number, count: number): void {
    for (let i = 0; i < count; i++) {
      const s = this.glowSprite(color, rand(0.1, 0.16));
      s.position.copy(at).add(new THREE.Vector3(rand(-0.35, 0.35), rand(-0.4, 0.5), rand(-0.35, 0.35)));
      this.spawn(s, s.material, { velocity: new THREE.Vector3(0, rand(0.7, 1.4), 0), life: rand(0.8, 1.3) });
    }
  }

  /** A first-aid cross rising over someone and fading. */
  cross(at: THREE.Vector3, color = 0x6dff9a): void {
    const material = new THREE.MeshBasicMaterial({ color: new THREE.Color(color).multiplyScalar(1.4), transparent: true, opacity: 0.95, depthWrite: false });
    const mesh = new THREE.Mesh(shape('medCross'), material);
    mesh.position.copy(at).setY(at.y + 0.7);
    this.spawn(mesh, material, { velocity: new THREE.Vector3(0, 0.55, 0), spin: new THREE.Vector3(0, 2.2, 0), life: 1.2, baseScale: 0.26, opacity: 0.95 });
  }

  // -------------------------------------------------------------- weapons

  /** A star at the muzzle, its light, and a wisp of smoke off the barrel. */
  muzzleFlash(at: THREE.Vector3, color: number): void {
    this.flash(at, color, 0.75);
    this.puffs(at, 0x8a8a86, 1, { size: 0.12, spread: 0.02, rise: 0.4, out: 0.1, life: 0.6, grow: 1.2 });
  }

  /** A bright streak travelling from `from` to `to`; `heavy` for sniper rounds. */
  async bullet(from: THREE.Vector3, to: THREE.Vector3, color: number, speed = 60, heavy = false): Promise<void> {
    const material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 1, blending: THREE.AdditiveBlending, depthWrite: false });
    const streak = new THREE.Mesh(heavy ? heavyStreakGeometry : streakGeometry, material);
    streak.position.copy(from);
    streak.lookAt(to);
    this.group.add(streak);
    const dist = from.distanceTo(to);
    await tween(Math.max(0.05, dist / speed), (t) => streak.position.lerpVectors(from, to, t));
    this.group.remove(streak);
    material.dispose();
  }

  sparks(at: THREE.Vector3, color: number, count = 9): void {
    for (let i = 0; i < count; i++) {
      const s = this.glowSprite(color, 0.12 + Math.random() * 0.08);
      s.position.copy(at);
      const dir = new THREE.Vector3(Math.random() - 0.5, Math.random() * 0.8, Math.random() - 0.5).normalize();
      this.spawn(s, s.material, { velocity: dir.multiplyScalar(2.5 + Math.random() * 3), gravity: 9, life: 0.3 + Math.random() * 0.25 });
    }
    if (this.impactGlow <= 0) return;
    const puff = this.glowSprite(color, 0.5 * this.impactGlow, 0.8 * this.impactGlow);
    puff.position.copy(at);
    this.spawn(puff, puff.material, { life: 0.15, grow: 1 });
  }

  /** Blade arc flashing across the target. */
  slash(at: THREE.Vector3, color: number): void {
    const material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 1, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const arc = new THREE.Mesh(slashGeometry, material);
    arc.position.copy(at);
    arc.rotation.set(Math.random() * 0.6 - 0.3, Math.random() * Math.PI, Math.random() * Math.PI);
    this.spawn(arc, material, { life: 0.28, grow: 0.6 });
    this.sparks(at, color, 10);
  }

  /** A medkit: green motes rising and a first-aid cross. */
  heal(at: THREE.Vector3): void {
    this.motes(at, 0x6dff9a, 12);
    this.cross(at);
  }

  /** Brief protective bubble (Aid Protocol). */
  shield(at: THREE.Vector3): void {
    const material = new THREE.MeshBasicMaterial({ color: 0x54d1ff, transparent: true, opacity: 0.35, blending: THREE.AdditiveBlending, depthWrite: false });
    const bubble = new THREE.Mesh(bubbleGeometry, material);
    bubble.position.copy(at);
    this.spawn(bubble, material, { life: 1.1, grow: 0.25, opacity: 0.35 });
  }

  /** A volley of small missiles converging on `targets`; resolves when the last one lands. */
  async missiles(from: THREE.Vector3, targets: THREE.Vector3[]): Promise<void> {
    const flights = targets.map(async (to, i) => {
      await wait(i * 0.09);
      const material = new THREE.MeshStandardMaterial({ color: 0x9aa3ad, emissive: 0xff7a2a, emissiveIntensity: 0.6 });
      const missile = new THREE.Mesh(missileGeometry, material);
      this.group.add(missile);
      const height = 2.5 + from.distanceTo(to) * 0.2;
      let last = from.clone();
      await tween(0.7, (t) => {
        missile.position.lerpVectors(from, to, t);
        missile.position.y += Math.sin(t * Math.PI) * height;
        missile.lookAt(missile.position.clone().add(missile.position.clone().sub(last)));
        if (missile.position.distanceTo(last) > 0.4) this.puffs(missile.position, 0x8a8a86, 1, { size: 0.22, spread: 0, rise: 0.2, out: 0.05, life: 0.8, grow: 1.4 });
        last = missile.position.clone();
      });
      this.group.remove(missile);
      material.dispose();
    });
    await Promise.all(flights);
  }

  /**
   * Someone leaving the map, or a body getting up off the ground: dust
   * kicked up round the spot and a little grit (the name is the old
   * evacuation beam's; game.ts calls it for both).
   */
  evacBeam(at: THREE.Vector3): void {
    const ground = at.clone().setY(0);
    this.dust(ground, 8);
    this.chips(ground.clone().setY(0.2), 0x6e6152, 5, 2);
    this.decal(ground, 'shockRing', 0xb8a88e, 0.5, 0.6, 0.45, 2.2);
  }

  /** Player marker: a coloured beam and pulsing rings for a few seconds. */
  ping(at: THREE.Vector3, color: string, enemy: boolean): void {
    const c = new THREE.Color(enemy ? '#ff4a4a' : color);
    // Up past the top of the screen and seen through whatever stands in front (0.10.0, Arturo: it stopped at a roof,
    // hidden behind walls), the haze none of it.
    const beamMat = new THREE.MeshBasicMaterial({ color: c, transparent: true, opacity: 0.5, blending: THREE.AdditiveBlending, depthWrite: false, depthTest: false, fog: false, side: THREE.DoubleSide });
    const beam = new THREE.Mesh(beamGeometry, beamMat);
    beam.scale.set(0.12, PING_HEIGHT / 14, 0.12);
    beam.position.copy(at).setY(at.y + PING_HEIGHT / 2);
    beam.renderOrder = 20;
    this.spawn(beam, beamMat, { life: 3.5, opacity: 0.5 });
    for (let i = 0; i < 3; i++) {
      void wait(i * 0.45).then(() => {
        const ringMat = new THREE.MeshBasicMaterial({ color: c, transparent: true, opacity: 0.9, depthWrite: false });
        const ring = new THREE.Mesh(pingRingGeometry, ringMat);
        ring.position.copy(at).setY(at.y + 0.06);
        this.spawn(ring, ringMat, { life: 1.1, grow: 1.6, opacity: 0.9 });
      });
    }
  }

  // ----------------------------------------------------------- explosives

  async grenadeArc(from: THREE.Vector3, to: THREE.Vector3): Promise<void> {
    const shell = new THREE.Mesh(shellGeometry, new THREE.MeshStandardMaterial({ color: 0x3b4a3a }));
    shell.castShadow = true;
    this.group.add(shell);
    const height = 1.5 + from.distanceTo(to) * 0.3;
    await tween(0.55 + from.distanceTo(to) * 0.03, (t) => {
      shell.position.lerpVectors(from, to, t);
      shell.position.y += Math.sin(t * Math.PI) * height;
      shell.rotation.x += 0.4;
    });
    this.group.remove(shell);
  }

  /**
   * A stone thrown (docs/FASE10.md §4): a low, quick arc from the hand to
   * `to`, a dull knock where it lands (a puff of dust, a short hop) and it
   * lies there a moment; no blast, no smoke. `struck`: `to` is someone's
   * chest, and from there it drops at their feet. Resolves as it lands or
   * strikes.
   */
  async stone(from: THREE.Vector3, to: THREE.Vector3, struck = false): Promise<void> {
    const rock = new THREE.Mesh(stoneGeometry, stoneMaterial);
    rock.castShadow = true;
    this.group.add(rock);
    const d = from.distanceTo(to);
    const height = (struck ? 0.25 : 0.5) + d * 0.12;
    await tween(0.3 + d * 0.025, (t) => {
      rock.position.lerpVectors(from, to, t);
      rock.position.y += Math.sin(t * Math.PI) * height;
      rock.rotation.x += 0.5;
      rock.rotation.z += 0.3;
    });
    const ground = struck ? to.clone().setY(0) : to.clone();
    const away = new THREE.Vector3(to.x - from.x, 0, to.z - from.z).setLength(struck ? 0.2 : 0.18);
    if (!struck) this.dust(ground, 2);
    // Off the body down to the feet, or a short hop on from where it hit the ground.
    const start = rock.position.clone();
    void tween(struck ? 0.26 : 0.16, (t) => {
      rock.position.lerpVectors(start, ground.clone().add(away), t);
      rock.position.y = struck ? THREE.MathUtils.lerp(start.y, ground.y + 0.04, t * t) : ground.y + 0.04 + Math.sin(t * Math.PI) * 0.1;
      rock.rotation.x += 0.2;
    }).then(() => {
      if (struck) this.dust(rock.position, 1);
      return wait(1.6);
    }).then(() => this.group.remove(rock));
  }

  /** Fireball with shockwave, debris and smoke. */
  explosion(at: THREE.Vector3, radius: number): Promise<void> {
    const fireMat = new THREE.MeshBasicMaterial({ color: 0xffb050, transparent: true, opacity: 0.95, blending: THREE.AdditiveBlending, depthWrite: false });
    const fire = new THREE.Mesh(blastGeometry, fireMat);
    fire.position.copy(at).setY(at.y + 0.45);
    fire.rotation.set(rand(0, 6), rand(0, 6), 0);
    const light = this.lamp(at.clone().setY(at.y + 1.35), 0xff8a30, 45, radius * 6);
    this.group.add(fire);
    const shock = this.decal(at, 'shockRing', 0xffd9a0, radius * 0.6, 0.6, 0.8, 4.5);
    (shock.material as THREE.MeshBasicMaterial).blending = THREE.AdditiveBlending;
    this.flash(at.clone().setY(at.y + 0.6), 0xffe0a0, radius * 1.2, 0, 0.18);

    for (let i = 0; i < 14; i++) this.debrisPiece(at, i % 3 === 0 ? 0x5a4a3a : 0x2c2f33, 5 + Math.random() * 4);
    // The scorch left behind, and smoke rolling up out of the fire.
    this.decal(at, 'groundCrack', 0x1a1714, radius * 1.3, 7, 0.9);
    this.flames(at, 8, radius * 0.4, { size: 0.6, life: 0.6 });
    void wait(0.15).then(() => this.puffs(at, 0x3b3936, 8, { size: 0.55, spread: radius * 0.5, rise: 1, out: 0.6, life: 2.2, grow: 1.4 }));

    return tween(0.6, (t) => {
      fire.scale.setScalar(0.3 + radius * Math.sqrt(t));
      fireMat.opacity = 0.95 * (1 - t);
      fireMat.color.setHSL(0.08 - t * 0.06, 1, 0.6 - t * 0.3);
      light.set(45 * (1 - t));
    }).then(() => {
      this.group.remove(fire);
      light.done();
      fireMat.dispose();
    });
  }

  /** Chunks flying out of a destroyed cover block. */
  debris(at: THREE.Vector3, color: number, count = 8): void {
    for (let i = 0; i < count; i++) this.debrisPiece(at, color, 3 + Math.random() * 2);
    this.puffs(at, 0x8f8676, 3, { size: 0.35, spread: 0.3, rise: 0.4, out: 0.5, life: 1.2 });
  }

  /**
   * A blow on a siege's door or barricade (the 'smashed' event, docs/FASE12.md
   * §10), `at` where it lands: on a gate's sheet metal, sparks, flakes of rust
   * and paint and the dust it shakes off; on sandbags, earth bursting out and
   * grit. `gives`: the blow that brings it down, a good deal more. Shown at
   * once, whatever is in sight: the caller says whether the tile is seen.
   */
  smash(at: THREE.Vector3, kind: 'door' | 'barricade', gives = false): void {
    const k = gives ? 2.5 : 1;
    if (kind === 'door') {
      this.sparks(at, 0xffc46b, Math.round(6 * k));
      this.chips(at, 0x6f6a5e, Math.round(4 * k), 2.6);
      this.puffs(at.clone().setY(at.y - 0.5), 0x8f8a80, Math.round(2 * k), { size: 0.3, spread: 0.35, rise: 0.25, out: 0.45, life: 1.1 });
      if (gives) this.debris(at.clone().setY(at.y - 0.8), 0x5a5e62, 6);
    } else {
      this.puffs(at, 0x9a8462, Math.round(3 * k), { size: 0.34, spread: 0.3, rise: 0.35, out: 0.6, life: 1.3, grow: 1.3 });
      this.chips(at, 0xb39a6c, Math.round(6 * k), 2.2);
      if (gives) this.dust(at.clone().setY(at.y - 0.5), 10);
    }
  }

  private debrisPiece(at: THREE.Vector3, color: number, speed: number): void {
    const material = new THREE.MeshStandardMaterial({ color, transparent: true, opacity: 1 });
    const piece = new THREE.Mesh(debrisGeometry, material);
    piece.castShadow = true;
    piece.position.copy(at).setY(Math.max(0.3, at.y));
    const dir = new THREE.Vector3(Math.random() - 0.5, 0.6 + Math.random() * 0.9, Math.random() - 0.5).normalize();
    this.spawn(piece, material, {
      velocity: dir.multiplyScalar(speed),
      spin: new THREE.Vector3(Math.random() * 12, Math.random() * 12, 0),
      gravity: 14,
      life: 1.4 + Math.random() * 0.6,
      baseScale: 0.6 + Math.random() * 1.2,
    });
  }

  // ------------------------------------------------------------- the dead

  /**
   * The Hinchado bursting (on top of the blast): a sickly green cloud that
   * rolls out and hangs a while, gore flying and dark splashes left round
   * where it stood.
   */
  burst(at: THREE.Vector3, radius: number): void {
    const cloud = at.clone().setY(at.y + 0.3);
    this.puffs(cloud, 0x7f9a3c, 9, { size: 0.7, spread: radius * 0.35, rise: 0.25, out: radius * 0.7, life: 3.2, grow: 1.6 });
    this.puffs(cloud, 0xa3b452, 5, { size: 0.5, spread: radius * 0.25, rise: 0.35, out: radius * 0.5, life: 2.6, grow: 1.4 });
    this.gore(at.clone().setY(at.y + 0.8), 14, 5);
    for (let i = 0; i < 7; i++) {
      const a = Math.random() * Math.PI * 2;
      const d = 0.2 + Math.random() * radius * 0.9;
      this.decal(at.clone().add(new THREE.Vector3(Math.cos(a) * d, 0, Math.sin(a) * d)), 'polyDisc', BLOOD[i % BLOOD.length]!, rand(0.4, 0.9), 6 + Math.random() * 3, 0.9);
    }
  }

  /** The Chillón's scream: rings of pressure rolling out from its head. */
  scream(head: THREE.Vector3): void {
    for (let i = 0; i < 3; i++) {
      void wait(i * 0.13).then(() => {
        const material = new THREE.MeshBasicMaterial({ color: SHRIEK, transparent: true, opacity: 0.5, blending: THREE.AdditiveBlending, depthWrite: false });
        const ring = new THREE.Mesh(screamRingGeometry, material);
        ring.position.copy(head);
        ring.rotation.x = Math.PI / 2 + (Math.random() - 0.5) * 0.4;
        this.spawn(ring, material, { life: 0.75, grow: 7, opacity: 0.6, baseScale: 0.25 });
      });
    }
  }

  /** A bite or a claw that gets in: blood spurting away from the attacker (`from`), a drop or two on the ground. */
  bite(at: THREE.Vector3, from: THREE.Vector3): void {
    const away = at.clone().sub(from).setY(0).normalize();
    for (let i = 0; i < 12; i++) {
      const drop = this.sprite('polyDisc', BLOOD[i % BLOOD.length]!, rand(0.07, 0.14), 0.95);
      drop.position.copy(at);
      const dir = away.clone().multiplyScalar(0.8 + Math.random()).add(new THREE.Vector3((Math.random() - 0.5) * 0.9, 0.5 + Math.random() * 0.9, (Math.random() - 0.5) * 0.9));
      this.spawn(drop, drop.material, { velocity: dir.multiplyScalar(1.8 + Math.random() * 1.6), gravity: 9, life: 0.45 + Math.random() * 0.3, opacity: 0.95 });
    }
    this.gore(at, 2, 2.5);
    const ground = at.clone().addScaledVector(away, 0.5).setY(0);
    this.decal(ground, 'polyDisc', BLOOD[1]!, rand(0.25, 0.4), 5, 0.85);
  }

  // ------------------------------------------------------------- arrivals

  /** Dust kicked up by feet or wheels at the ground point `at`. */
  dust(at: THREE.Vector3, amount = 6): void {
    this.puffs(at.clone().setY(at.y + 0.1), 0x9a8a72, amount, { size: 0.32, spread: 0.45, rise: 0.35, out: 0.4, life: 1.3, grow: 1.4 });
  }

  /** A vehicle's headlights at the map's edge (`at`, looking along `dir`), coming on, holding, then gone. */
  headlights(at: THREE.Vector3, dir: THREE.Vector3): void {
    const side = new THREE.Vector3(-dir.z, 0, dir.x).multiplyScalar(0.45);
    for (const s of [1, -1]) {
      const lamp = this.glowSprite(0xfff2c8, 0.9, 0.95);
      lamp.position.copy(at).addScaledVector(side, s).setY(at.y + 0.55);
      this.spawn(lamp, lamp.material, { life: 1.8, opacity: 0.95 });
    }
    // One of the effects' lights, a few metres ahead and low (a spot of its own would change every material's lights).
    const light = this.lamp(at.clone().addScaledVector(dir, 3.5).setY(at.y + 0.7), 0xfff0c0, 0, 10);
    // By night less: in the dark it lit the edge white (Arturo 17/10: «la luz deslumbra mucho de noche»).
    const peak = NIGHT.value > 0.5 ? 9 : 18;
    void tween(1.8, (t) => light.set(peak * Math.sin(Math.min(1, t * 1.4) * Math.PI))).then(light.done);
    this.dust(at, 8);
  }

  /**
   * The Chillón's shriek reaching someone (a soldier it rattles, a corpse it
   * raises): ripples of sound rolling from its mouth to them, and a jolt of
   * pressure where they land. Resolves when the wave arrives. (The name is
   * the old psionic beam's; game.ts still calls it that.)
   */
  async psiBeam(from: THREE.Vector3, to: THREE.Vector3, color = SHRIEK): Promise<void> {
    const travel = 0.45;
    const waves = Array.from({ length: 5 }, (_, i) =>
      wait(i * 0.07).then(() => {
        const material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.4, blending: THREE.AdditiveBlending, depthWrite: false });
        const ring = new THREE.Mesh(screamRingGeometry, material);
        ring.position.copy(from);
        ring.lookAt(to);
        ring.scale.setScalar(0.12);
        this.group.add(ring);
        return tween(travel, (t) => {
          ring.position.lerpVectors(from, to, t);
          ring.scale.setScalar(0.12 + t * 0.45);
          material.opacity = 0.4 * (1 - t * 0.7);
        }).then(() => {
          this.group.remove(ring);
          material.dispose();
        });
      }),
    );
    await wait(travel);
    const jolt = this.sprite('shockRing', color, 0.4, 0.8, true);
    jolt.position.copy(to);
    this.spawn(jolt, jolt.material, { life: 0.35, grow: 3, opacity: 0.8 });
    this.dust(to.clone().setY(0), 3);
    await Promise.all(waves);
  }

  // ----------------------------------------------------------------- text

  /**
   * Rising text above a world point ("−4", "FALLO", "VIGILANCIA"...). A
   * `callout` («¡APARECE UN IMPRESO!») is a slip that stays a while, to be read.
   */
  floatText(at: THREE.Vector3, text: string, kind: 'damage' | 'crit' | 'miss' | 'info' | 'warn' | 'alert' | 'heal' | 'callout' = 'info'): void {
    const el = document.createElement('div');
    el.className = `float-text ${kind}`;
    el.textContent = text;
    const label = new CSS2DObject(el);
    label.position.copy(at).setY(at.y + 1.4);
    this.group.add(label);
    void wait(kind === 'callout' ? 2.7 : 1.6).then(() => {
      this.group.remove(label);
      el.remove();
    });
  }

  /**
   * A bar filled in the field (playtest 16/10 #10, UI): over the soldier, its
   * line's name and what it added, and a small bar filling up, in the line's
   * colour (`cls`: hexBoard.css's .sk-*).
   */
  floatBar(at: THREE.Vector3, text: string, cls: string): void {
    const el = document.createElement('div');
    el.className = `float-bar ${cls}`;
    const label = document.createElement('b');
    label.textContent = text;
    el.append(label, document.createElement('i'));
    const obj = new CSS2DObject(el);
    obj.position.copy(at).setY(at.y + 1.7);
    this.group.add(obj);
    void wait(2.9).then(() => {
      this.group.remove(obj);
      el.remove();
    });
  }
}
