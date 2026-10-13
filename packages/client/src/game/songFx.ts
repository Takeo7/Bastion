import * as THREE from 'three';
import { Fx, fxShape } from './fx';
import { fxTexture, modelNow } from './synty';
import { tween, wait } from './tween';

// The Oficio's effects (docs/LORE.md): trades and tools, not magic. A flare
// thrown at a zombie, a molotov, a petrol drum going up, crossbow bolts,
// firecrackers to draw the dead away, bandages, a sedative, a defibrillator.
// Built from the same Synty pieces as the rest of the effects (fx.ts):
// sparks, faceted smoke, flame shapes, flare light, puffs of gauze. Each
// effect resolves when its moment lands (a bolt hitting, the bottle
// breaking) so game.ts can show the damage after it. The names keep the old
// abilities' ids (bolt, fireball, silence…), which the engine still uses.

/** Tints game.ts passes for the auras: cyan the riot shield, green a rally or a medic, gold «¡Aguantad!». */
export const SONG = {
  green: new THREE.Color(0x8fe39a),
  cyan: new THREE.Color(0x9cc8e6),
  white: new THREE.Color(0xf2f0ea),
  gold: new THREE.Color(0xffc46a),
  fire: new THREE.Color(0xff8a3a),
} as const;

const FLARE = 0xff3a2a;
const SMOKE = 0x8f8a84;
const SOOT = 0x34312e;

const rand = (a: number, b: number) => a + Math.random() * (b - a);

/**
 * A molotov with its rag alight: Apocalypse's bottle (syntyManifest.ts,
 * FX_MODELS) and a flame at the neck; a plain bottle until it has loaded.
 * Also the interface's icon (lab/renders).
 */
export function molotov(): THREE.Group {
  const g = new THREE.Group();
  const synty = modelNow('apocalypse', 'fx', 'molotov');
  let top = 0.19;
  if (synty) {
    const bottle = synty.clone();
    bottle.traverse((o) => (o.userData.keep = true));
    g.add(bottle);
    top = new THREE.Box3().setFromObject(bottle).max.y - 0.005;
  } else {
    const glass = new THREE.MeshStandardMaterial({ color: 0x3f6a3a, flatShading: true, roughness: 0.6 });
    const bottle = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.045, 0.14, 7).translate(0, 0.07, 0), glass);
    const neck = new THREE.Mesh(new THREE.CylinderGeometry(0.016, 0.03, 0.07, 7).translate(0, 0.17, 0), glass);
    g.add(bottle, neck);
  }
  const rag = new THREE.Mesh(fxShape('flame'), new THREE.MeshBasicMaterial({ color: new THREE.Color(0xffa030).multiplyScalar(1.6) }));
  rag.position.y = top + 0.04;
  rag.scale.setScalar(0.1);
  rag.userData.shared = true;
  g.add(rag);
  return g;
}

/** Something thrown: a lit flare stick or a molotov. */
function thrown(kind: 'flare' | 'molotov'): THREE.Group {
  if (kind === 'molotov') return molotov();
  const g = new THREE.Group();
  g.add(new THREE.Mesh(new THREE.CylinderGeometry(0.022, 0.022, 0.2, 6), new THREE.MeshStandardMaterial({ color: 0xb3261e, flatShading: true, roughness: 0.6 })));
  return g;
}

/** Frees what `thrown` made (the Particle FX shapes are shared and stay, the Synty bottle's model and atlas too). */
function discard(g: THREE.Object3D): void {
  g.traverse((o) => {
    if (!(o instanceof THREE.Mesh) || o.userData.keep) return;
    if (!o.userData.shared) o.geometry.dispose();
    (o.material as THREE.Material).dispose();
  });
}

export class SongFx {
  readonly group = new THREE.Group();
  private readonly fx = new Fx();
  private readonly scratch = new THREE.Vector3();

  constructor() {
    this.group.add(this.fx.group);
    modelNow('apocalypse', 'fx', 'molotov');
  }

  /** Advances the effects; call every frame. */
  tick(dt: number): void {
    this.fx.tick(dt);
  }

  /** Takes every effect away at once. */
  clear(): void {
    this.fx.clear();
  }

  /** Sparks from a point (in a colour of SONG, or the fire's). */
  burst(at: THREE.Vector3, count = 16, _speed = 3, color: THREE.Color = SONG.fire): void {
    this.fx.sparks(at, color.getHex(), count);
  }

  /** A thing flying from `from` to `to` along an arc of height `lift`; `trail` runs along the way. */
  private async fly(from: THREE.Vector3, to: THREE.Vector3, o: { speed: number; lift: number; core: THREE.Object3D; spin: boolean; trail: (at: THREE.Vector3) => void }): Promise<void> {
    const time = Math.max(0.15, from.distanceTo(to) / o.speed);
    this.group.add(o.core);
    let shed = 0;
    const last = from.clone();
    await tween(time, (k) => {
      const p = this.scratch.lerpVectors(from, to, k);
      p.y += Math.sin(k * Math.PI) * o.lift;
      if (o.spin) {
        o.core.rotation.x += 0.35;
        o.core.rotation.z += 0.2;
      } else if (p.distanceToSquared(last) > 1e-6) {
        o.core.lookAt(p.clone().add(p.clone().sub(last)));
      }
      last.copy(o.core.position);
      o.core.position.copy(p);
      if (k - shed > 0.04) {
        shed = k;
        o.trail(p);
      }
    });
    this.group.remove(o.core);
  }

  // ------------------------------------------------------------ the trades

  /** Bengala: a lit flare thrown at the target; it lights them up red and burns on the ground. Resolves when it hits. */
  async bolt(from: THREE.Vector3, to: THREE.Vector3): Promise<void> {
    const core = thrown('flare');
    let smoke = false;
    await this.fly(from, to, {
      speed: 14,
      lift: 0.7,
      core,
      spin: true,
      trail: (at) => {
        this.fx.trail(at, FLARE, 0.22, 0.25);
        this.fx.embers(at, 1, 0.03, 0xff6a4a);
        if ((smoke = !smoke)) this.fx.puffs(at, SMOKE, 1, { size: 0.07, spread: 0, rise: 0.3, out: 0.05, life: 0.7, grow: 1.6 });
      },
    });
    discard(core);
    this.fx.flash(to, FLARE, 0.9, 12, 0.3);
    this.fx.sparks(to, 0xff7a5a, 14);
    this.fx.puffs(to, SMOKE, 3, { size: 0.24, spread: 0.2, rise: 0.6, out: 0.3, life: 1.5 });
    this.fx.flare(to.clone().setY(0), FLARE, 2.6);
  }

  /** Cóctel molotov: a bottle with a burning rag lobbed onto the spot; glass, a sheet of fire, black smoke and a scorch. Resolves when it breaks. */
  async fireball(from: THREE.Vector3, to: THREE.Vector3, radius = 2.5): Promise<void> {
    const core = thrown('molotov');
    await this.fly(from, to, {
      speed: 11,
      lift: 1.8,
      core,
      spin: true,
      trail: (at) => {
        this.fx.embers(at, 1, 0.04);
        this.fx.puffs(at, SOOT, 1, { size: 0.1, spread: 0, rise: 0.25, out: 0.05, life: 0.7, grow: 1.5 });
      },
    });
    discard(core);
    const ground = to.clone().setY(0);
    this.fx.chips(ground.clone().setY(0.25), 0xa8d0a0, 9, 3);
    this.fx.flash(ground.clone().setY(0.5), 0xffa040, radius * 0.9, 30, 0.35);
    this.fx.flames(ground, 18, radius * 0.75, { size: 0.6, life: 1.3 });
    void wait(0.4).then(() => this.fx.flames(ground, 12, radius * 0.6, { size: 0.5, life: 1.1 }));
    void wait(0.2).then(() => this.fx.puffs(ground.clone().setY(0.3), SOOT, 10, { size: 0.55, spread: radius * 0.55, rise: 1.1, out: 0.5, life: 2.6, grow: 1.5 }));
    this.fx.decal(ground, 'groundCrack', 0x1a1410, radius * 1.6, 8, 0.85);
  }

  /** Flecha perforante: a crossbow bolt, fast and straight, a faint streak behind it. Resolves when it hits. */
  async arrow(from: THREE.Vector3, to: THREE.Vector3): Promise<void> {
    const core = new THREE.Group();
    const shaft = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.012, 0.42, 5).rotateX(Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x5a4632, flatShading: true }));
    const head = new THREE.Mesh(new THREE.ConeGeometry(0.022, 0.07, 4).rotateX(Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x9aa0a6, metalness: 0.6, roughness: 0.4, flatShading: true }));
    head.position.z = 0.24;
    const vanes = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.004, 0.09), new THREE.MeshStandardMaterial({ color: 0xe8e4da }));
    vanes.position.z = -0.17;
    const vanes2 = vanes.clone();
    vanes2.rotation.z = Math.PI / 2;
    core.add(shaft, head, vanes, vanes2);
    core.position.copy(from);
    core.lookAt(to);
    await this.fly(from, to, { speed: 26, lift: 0.05, core, spin: false, trail: (at) => this.fx.trail(at, 0xf2f0ea, 0.08, 0.2) });
    discard(core);
  }

  /** Lluvia de flechas: a bolt at each target, one after another. */
  async arrows(from: THREE.Vector3, targets: THREE.Vector3[]): Promise<void> {
    await Promise.all(targets.map((t, i) => wait(i * 0.15).then(() => this.arrow(from.clone().add(new THREE.Vector3(0, 0.3 * i, 0)), t))));
  }

  /** Saltar por la ventana, Encaramarse, first half: the push-off, dust and splinters. */
  async vanish(at: THREE.Vector3): Promise<void> {
    this.fx.dust(at, 6);
    this.fx.chips(at.clone().setY(at.y + 0.6), 0x8a7a5a, 5, 2.5);
    await wait(0.25);
  }

  /** Second half: the landing, a ring of dust. Resolves as they touch down. */
  async appear(at: THREE.Vector3): Promise<void> {
    await wait(0.1);
    this.fx.dust(at, 8);
    this.fx.decal(at, 'shockRing', 0xcbbba0, 0.6, 0.5, 0.5, 2.5);
  }

  /** Petardos: a string of firecrackers going off round the spot, sparks, flashes, paper and smoke. Resolves once it is going. */
  async dome(center: THREE.Vector3, radius: number): Promise<void> {
    for (let i = 0; i < 12; i++) {
      void wait(i * 0.07 + rand(0, 0.04)).then(() => {
        const a = Math.random() * Math.PI * 2;
        const d = Math.sqrt(Math.random()) * radius * 0.8;
        const p = center.clone().add(new THREE.Vector3(Math.cos(a) * d, 0.15, Math.sin(a) * d));
        this.fx.flash(p, i % 2 ? 0xffe08a : 0xff6a40, 0.5, i % 3 === 0 ? 8 : 0, 0.08);
        this.fx.sparks(p, 0xffc070, 5);
        this.fx.chips(p, i % 2 ? 0xc0282a : 0xd8b04a, 2, 2);
        this.fx.puffs(p, 0xbdb6ac, 1, { size: 0.24, spread: 0.05, rise: 0.6, out: 0.2, life: 1.4 });
      });
    }
    await wait(0.45);
  }

  /** El bidón: the fire reaching each target, one after another. Resolves after the last. */
  async storm(targets: THREE.Vector3[]): Promise<void> {
    for (const t of targets) {
      const ground = t.clone().setY(0);
      this.fx.flash(t, 0xffa040, 1.4, 25, 0.3);
      this.fx.flames(ground, 10, 0.45, { size: 0.7, life: 0.9 });
      this.fx.puffs(ground.clone().setY(0.5), SOOT, 4, { size: 0.45, spread: 0.3, rise: 1, out: 0.3, life: 2 });
      this.fx.decal(ground, 'groundCrack', 0x1a1410, 1.2, 6, 0.8);
      await wait(0.18);
    }
  }

  /** A ring spreading over the ground to `radius` (the area an order or a tool reaches). */
  ring(center: THREE.Vector3, radius: number, color: THREE.Color = SONG.green, life = 0.8): void {
    const ring = this.fx.decal(center, 'shockRing', color.getHex(), 0.5, life, 0.75, radius * 4 - 1);
    (ring.material as THREE.MeshBasicMaterial).blending = THREE.AdditiveBlending;
  }

  /** Escudo antidisturbios, Arenga, ¡Aguantad!: a ring from whoever calls it, and a lift in each ally. */
  aura(center: THREE.Vector3, radius: number, allies: THREE.Vector3[], color: THREE.Color = SONG.green): void {
    this.ring(center, radius, color, 1.1);
    for (const a of allies) this.rise(a, color, 12);
  }

  /** A few small lights and a scuff of dust round someone (any trade put to use). */
  rise(at: THREE.Vector3, color: THREE.Color = SONG.green, count = 18): void {
    this.fx.motes(at.clone().setY(at.y + 0.6), color.getHex(), Math.ceil(count / 2));
    this.fx.dust(at, 2);
  }

  /** Vendaje rápido: puffs of gauze, strips fluttering off, a first-aid cross. */
  heal(at: THREE.Vector3): void {
    const chest = at.clone().setY(at.y + 0.8);
    this.fx.puffs(chest, 0xf2efe8, 6, { size: 0.2, spread: 0.3, rise: 0.7, out: 0.4, life: 1 });
    this.fx.chips(chest, 0xf4f1ea, 4, 1.6);
    this.fx.motes(chest, SONG.green.getHex(), 6);
    this.fx.cross(at.clone().setY(at.y + 0.2));
  }

  /** Desfibrilador: two jolts into the fallen, then they come round. Resolves when they stand. */
  async revive(at: THREE.Vector3): Promise<void> {
    const chest = at.clone().setY(at.y + 0.35);
    this.fx.zap(chest);
    await wait(0.35);
    this.fx.zap(chest);
    await wait(0.3);
    this.fx.cross(at.clone().setY(at.y + 0.3));
    this.fx.motes(chest, SONG.green.getHex(), 8);
  }

  /** Sedante: a pale breath and a few lights settling on the ally, then calm. Resolves at the calm. */
  async cleanse(at: THREE.Vector3): Promise<void> {
    const chest = at.clone().setY(at.y + 1);
    this.fx.puffs(chest, 0xd8ecf4, 4, { size: 0.2, spread: 0.2, rise: 0.3, out: 0.3, life: 0.8 });
    this.fx.motes(chest, 0x9fd8ff, 8);
    await wait(0.35);
    this.fx.flash(chest, 0xbfe8ff, 0.5, 4, 0.15);
  }

  /** Rastreo: a sight closing on the target. */
  mark(at: THREE.Vector3): void {
    this.fx.reticle(at, SONG.gold.getHex());
  }

  /** A machete's swing. */
  blade(at: THREE.Vector3, _facing: THREE.Vector3): void {
    this.fx.slash(at, 0xe8f6ff);
  }
}

/**
 * What stays on the field after a trade: where the firecrackers went off (a
 * faint ring, spent paper and a haze of smoke) and «¡Aguantad!» (a gold ring
 * round whoever holds the line). Rebuilt from the state whenever it changes.
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

  /** Petardos zones: centre on the ground (world) and radius in tiles. */
  setSilences(list: { at: THREE.Vector3; radius: number }[]): void {
    const key = list.map((z) => `${z.at.x},${z.at.z},${z.radius}`).join('|');
    if (key === this.zoneKey) return;
    this.zoneKey = key;
    clearMarks(this.zones);
    for (const z of list) this.zones.add(groundRing(z.at, z.radius, 0xd8584a, 0.4), spentCrackers(z.at, z.radius));
  }

  /** ¡Aguantad!: a gold ring, radius 4, round each who holds the line. */
  setOaths(list: THREE.Vector3[], radius = 4): void {
    const key = list.map((p) => `${p.x},${p.z}`).join('|');
    if (key === this.oathKey) return;
    this.oathKey = key;
    clearMarks(this.oaths);
    for (const p of list) this.oaths.add(groundRing(p, radius, SONG.gold.getHex(), 0.4));
  }
}

/** Empties a marks group, disposing what it made (the Particle FX shapes stay). */
function clearMarks(g: THREE.Group): void {
  g.traverse((o) => {
    if (o instanceof THREE.Mesh) (o.material as THREE.Material).dispose();
  });
  g.clear();
}

const ringPlane = new THREE.PlaneGeometry(2, 2).rotateX(-Math.PI / 2);

/** A ring lying on the ground at `radius`, turning slowly. */
function groundRing(center: THREE.Vector3, radius: number, color: number, opacity: number): THREE.Mesh {
  const material = new THREE.MeshBasicMaterial({ map: fxTexture('shockRing'), color, transparent: true, opacity, depthWrite: false, blending: THREE.AdditiveBlending });
  const ring = new THREE.Mesh(ringPlane, material);
  ring.position.copy(center).setY(center.y + 0.04);
  ring.scale.setScalar(radius);
  ring.onBeforeRender = () => (ring.rotation.y = (performance.now() / 1000) * 0.15);
  return ring;
}

/** Where firecrackers went off: red and gold paper on the ground, and a low haze breathing over it. */
function spentCrackers(center: THREE.Vector3, radius: number): THREE.Group {
  const g = new THREE.Group();
  g.position.copy(center);
  const paper = [0xc0282a, 0xd8b04a].map((color) => new THREE.MeshStandardMaterial({ color, flatShading: true }));
  for (let i = 0; i < 14; i++) {
    const a = Math.random() * Math.PI * 2;
    const d = Math.sqrt(Math.random()) * radius * 0.85;
    const bit = new THREE.Mesh(fxShape('woodChip'), paper[i % 2]!);
    bit.position.set(Math.cos(a) * d, 0.03, Math.sin(a) * d);
    bit.rotation.set(Math.PI / 2, rand(0, 6), 0);
    bit.scale.setScalar(rand(0.08, 0.13));
    g.add(bit);
  }
  const haze = new THREE.MeshStandardMaterial({ color: 0xc8c2b8, flatShading: true, transparent: true, opacity: 0.45, depthWrite: false });
  for (let i = 0; i < 5; i++) {
    const a = (i / 5) * Math.PI * 2 + rand(0, 0.8);
    const d = rand(0.2, 0.6) * radius;
    const puff = new THREE.Mesh(fxShape('smokePuff'), haze);
    puff.position.set(Math.cos(a) * d, 0.25, Math.sin(a) * d);
    const size = rand(0.6, 0.9);
    const phase = rand(0, 6);
    puff.scale.set(size * 1.4, size * 0.6, size * 1.4);
    puff.onBeforeRender = () => {
      const t = performance.now() / 1000;
      puff.scale.y = size * (0.55 + 0.1 * Math.sin(t * 0.8 + phase));
      puff.rotation.y = t * 0.05 + phase;
    };
    g.add(puff);
  }
  return g;
}
