import { PLAYER_COLORS, type TemplateId } from '@bastion/engine';
import * as THREE from 'three';
import type { FigurePose, FigureRig } from '../../game/figures';
import { Fx } from '../../game/fx';
import { buildUnit, IDLE_POSE, isFigure } from '../../game/models';
import { Renderer } from '../../game/renderer';
import { SONG, SongFx, SongMarks } from '../../game/songFx';
import { wait } from '../../game/tween';

// Effects lab (/efectos.html): every combat effect (fx.ts) and every trade of
// the Oficio (songFx.ts) on a survivor and three zombies, in the game's light.
// The buttons play one; `#molotov` plays it on load, `__fx.play('bengala')`
// from a script and `__fx.sheet(name, [[effect, ms], …])` saves a contact sheet.

// A preview that isn't on screen gets no animation frames, counts as hidden (tweens skip to their end)
// and has its timers throttled. `?virtual` runs the page on a clock of its own instead: time only moves
// when `sheet` (or `__fx.advance(ms)`) moves it, a frame at a time, so every capture lands on its moment.
const virtual = new URLSearchParams(location.search).has('virtual');
let now = performance.now();
let frames: FrameRequestCallback[] = [];
if (virtual) {
  performance.now = () => now;
  window.requestAnimationFrame = (cb) => frames.push(cb);
  Object.defineProperty(document, 'hidden', { get: () => false });
}

/** A macrotask that hidden pages don't throttle, so promise chains settle between frames. */
const settle = () =>
  new Promise<void>((resolve) => {
    const c = new MessageChannel();
    c.port1.onmessage = () => resolve();
    c.port2.postMessage(0);
  });

/** Moves the virtual clock on by `ms`, frame by frame. */
async function advance(ms: number): Promise<void> {
  for (let t = 0; t < ms; t += 1000 / 60) {
    now += 1000 / 60;
    const due = frames;
    frames = [];
    for (const cb of due) cb(now);
    await settle();
  }
}

const container = document.getElementById('fx')!;
const renderer = new Renderer(container);
const scene = renderer.scene;
const fx = new Fx();
const song = new SongFx();
const marks = new SongMarks();
scene.add(fx.group, song.group, marks.group);

const ground = new THREE.Mesh(new THREE.PlaneGeometry(40, 30).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x77705f, roughness: 0.95 }));
ground.position.set(10, 0, 10);
ground.receiveShadow = true;
scene.add(ground);
renderer.fitMap(20, 20);

interface Actor {
  rig: FigureRig;
  pose: FigurePose;
}

const actors: Actor[] = [];
function actor(template: TemplateId, x: number, z: number, yaw: number, accent: string, seed: string): THREE.Vector3 {
  const rig = buildUnit(template, accent, template === 'rookie' ? { primary: '#56605a', secondary: accent, visor: '#9fe3ff', head: 'bare', build: 'normal', character: 'hunter' } : undefined, undefined, seed);
  if (isFigure(rig)) {
    rig.root.position.set(x, 0, z);
    scene.add(rig.root);
    actors.push({ rig, pose: { ...IDLE_POSE, yaw, spin: 0, search: 0 } });
  }
  return new THREE.Vector3(x, 0, z);
}

const me = actor('rookie', 8, 10.5, Math.PI / 2, PLAYER_COLORS[0]!, 'lab');
const ally = actor('rookie', 7.4, 12.2, Math.PI / 2, PLAYER_COLORS[1]!, 'lab2');
const them = [actor('zombie', 11.6, 9.2, -Math.PI / 2, '#ff4d4d', 'z1'), actor('zombie', 12.2, 11, -Math.PI / 2, '#ff4d4d', 'z2'), actor('bloater', 11.6, 12.8, -Math.PI / 2, '#ff4d4d', 'z3')];
const chest = (p: THREE.Vector3) => p.clone().setY(0.85);
const hands = chest(me).add(new THREE.Vector3(0.3, 0.25, 0));

const EFFECTS: Record<string, () => Promise<unknown> | void> = {
  disparo: async () => {
    for (let i = 0; i < 3; i++) {
      const from = hands.clone().add(new THREE.Vector3(0.3, 0, 0));
      fx.muzzleFlash(from, 0x9fe8ff);
      void fx.bullet(from, chest(them[1]!), 0x9fe8ff).then(() => fx.sparks(chest(them[1]!), 0xff7a5a, 8));
      await wait(0.11);
    }
  },
  mordisco: () => fx.bite(chest(me), them[0]!),
  granada: () => fx.explosion(them[1]!, 1.5),
  hinchado: () => {
    void fx.explosion(them[2]!, 1.5);
    fx.burst(them[2]!, 1.5);
  },
  escombros: () => fx.debris(new THREE.Vector3(10, 0.6, 10), 0x8f6c45, 8),
  polvo: () => fx.dust(me, 9),
  botiquín: () => fx.heal(chest(ally)),
  bengala: () => song.bolt(hands, chest(them[0]!)),
  molotov: () => song.fireball(hands, them[1]!, 2.5),
  bidón: () => song.storm(them.map(chest)),
  flecha: () => song.arrow(hands, chest(them[1]!)),
  lluvia: () => song.arrows(hands, them.map(chest)),
  petardos: async () => {
    const at = new THREE.Vector3(13.2, 0, 10.8);
    await song.dome(at, 2);
    marks.setSilences([{ at, radius: 2 }]);
  },
  vendaje: () => song.heal(ally),
  desfibrilador: () => song.revive(ally),
  sedante: () => song.cleanse(ally),
  rastreo: () => song.mark(chest(them[0]!)),
  escudo: () => song.aura(me, 1.5, [me, ally], SONG.cyan),
  arenga: () => song.aura(me, 6, [me, ally], SONG.green),
  aguantad: () => {
    song.aura(me, 4, [me], SONG.gold);
    marks.setOaths([me]);
  },
  saltar: async () => {
    await song.vanish(me);
    await wait(0.3);
    await song.appear(me);
  },
  grito: async () => {
    const mouth = chest(them[0]!).add(new THREE.Vector3(0, 0.45, 0));
    fx.scream(mouth);
    await fx.psiBeam(mouth, chest(me));
  },
  levantarse: async () => {
    const corpse = new THREE.Vector3(10, 0, 12.4);
    await fx.psiBeam(chest(them[0]!).add(new THREE.Vector3(0, 0.45, 0)), corpse.clone().setY(0.5));
    fx.evacBeam(corpse);
  },
  limpiar: () => {
    marks.setSilences([]);
    marks.setOaths([]);
    fx.clear();
    song.clear();
  },
};

const bar = document.getElementById('fx-buttons')!;
for (const name of Object.keys(EFFECTS)) {
  const b = document.createElement('button');
  b.textContent = name;
  b.onclick = () => void EFFECTS[name]!();
  bar.append(b);
}

renderer.camera.position.set(10.2, 6.5, 18.5);
renderer.camera.lookAt(10.2, 0.6, 11);

let clock = 0;
renderer.onFrame((dt) => {
  clock += dt;
  for (const a of actors) a.rig.applyPose(a.pose, clock);
  fx.tick(dt);
  song.tick(dt);
});

function play(name: string): Promise<unknown> | void {
  return EFFECTS[name]?.();
}

/**
 * A contact sheet for review: each effect `ms` after it starts, four to a
 * row, sent to the dev server's /__snap (saved as .dev-snapshots/<name>.jpg).
 */
async function sheet(name: string, shots: [string, number][]): Promise<void> {
  const c = renderer.gl.domElement;
  const [w, cols] = [560, 4];
  const h = Math.round((w * c.height) / c.width);
  const out = document.createElement('canvas');
  out.width = w * cols;
  out.height = h * Math.ceil(shots.length / cols);
  const g = out.getContext('2d')!;
  g.font = '18px sans-serif';
  const pause = (ms: number) => (virtual ? advance(ms) : new Promise((r) => setTimeout(r, ms)));
  for (const [i, [effect, ms]] of shots.entries()) {
    void play('limpiar');
    await pause(virtual ? 100 : 2500);
    void play(effect);
    await pause(ms);
    renderer.render(0);
    const [x, y] = [(i % cols) * w, Math.floor(i / cols) * h];
    g.drawImage(c, 0, 0, c.width, c.height, x, y, w, h);
    g.fillStyle = '#fff';
    g.fillText(effect, x + 6, y + 20);
  }
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: out.toDataURL('image/jpeg', 0.85) });
}

(window as unknown as { __fx: unknown }).__fx = { play, sheet, advance, renderer, fx, song, marks };
const first = decodeURIComponent(location.hash.slice(1));
if (first) void wait(2).then(() => play(first));
