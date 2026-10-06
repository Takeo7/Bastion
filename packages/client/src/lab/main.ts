import '@fontsource/rajdhani/latin-500.css';
import '@fontsource/rajdhani/latin-600.css';
import '@fontsource/rajdhani/latin-700.css';
import './lab.css';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { FAMILIES, STYLES } from './catalog';
import { DEPTH, WIDTH } from './layout';
import type { ArtStyle } from './stylekit';
import { CAMERA_PRESETS, LabView, preloadFonts, type PresetId } from './view';

// Style lab (M5): the same street corner rendered in several art directions.
// Open /estilos.html on the dev server; /comparar.html shows several at once.

const STORE_KEY = 'bastion.lab.style';

// --------------------------------------------------------------- DOM

const root = document.getElementById('lab')!;
root.innerHTML = `
  <div class="view"></div>
  <aside class="panel side">
    <header>
      <div class="kicker">Bastión · M5</div>
      <h1>Laboratorio de estilos</h1>
      <p class="hint">Misma escena, distinto arte. Arrastra para girar, rueda para acercar. <a href="comparar.html">Comparar estilos →</a></p>
    </header>
    <nav class="styles"></nav>
    <section class="info"></section>
  </aside>
  <div class="panel toolbar">
    <div class="group cams"></div>
    <div class="group">
      <button data-act="pause" title="Espacio">Pausa</button>
      <button data-act="moment" title="M">Momento clave</button>
      <button data-act="ui" title="U">Interfaz táctica</button>
    </div>
    <div class="fps"></div>
  </div>`;
const viewEl = root.querySelector<HTMLDivElement>('.view')!;
const navEl = root.querySelector<HTMLElement>('.styles')!;
const infoEl = root.querySelector<HTMLElement>('.info')!;
const camsEl = root.querySelector<HTMLElement>('.cams')!;
const fpsEl = root.querySelector<HTMLElement>('.fps')!;
const sideEl = root.querySelector<HTMLElement>('.side')!;
const toolbarEl = root.querySelector<HTMLElement>('.toolbar')!;
void preloadFonts();

// ------------------------------------------------------------ view

const view = new LabView(viewEl, {
  insets: () => {
    if (root.classList.contains('bare')) return { left: 0, bottom: 0 };
    return { left: sideEl.getBoundingClientRect().right, bottom: viewEl.clientHeight - toolbarEl.getBoundingClientRect().top };
  },
});
// The orbit camera is shared state: the view copies its pose every frame.
const eye = new THREE.PerspectiveCamera(40, 1, 0.1, 400);
const controls = new OrbitControls(eye, view.canvas);
controls.enableDamping = true;
controls.maxPolarAngle = THREE.MathUtils.degToRad(86);
controls.minDistance = 3;
controls.maxDistance = 60;
let style: ArtStyle = STYLES[0]!;

let last = performance.now();
let fpsAvg = 60;
view.renderer.setAnimationLoop(() => {
  const now = performance.now();
  const dt = Math.min(0.1, (now - last) / 1000);
  last = now;
  controls.update();
  view.setPose(eye.position, controls.target);
  view.advance(dt);
  view.draw();
  fpsAvg += (1 / Math.max(dt, 0.001) - fpsAvg) * 0.05;
  fpsEl.textContent = `${Math.round(fpsAvg)} fps`;
});

// --------------------------------------------------------------- UI

function setPreset(id: PresetId): void {
  const p = CAMERA_PRESETS[id];
  eye.position.set(p.pos[0], p.pos[1], p.pos[2]);
  controls.target.set(p.target[0], p.target[1], p.target[2]);
  controls.update();
  view.setPose(eye.position, controls.target);
}

function setPaused(p: boolean): void {
  view.paused = p;
  root.querySelector('[data-act="pause"]')!.textContent = p ? 'Reanudar' : 'Pausa';
}

function selectStyle(i: number): void {
  const next = STYLES[(i + STYLES.length) % STYLES.length]!;
  try {
    localStorage.setItem(STORE_KEY, next.id);
  } catch {
    /* storage unavailable: the choice just is not remembered */
  }
  // Styles with a fixed camera (isometric) force it; leaving them restores the tactical view.
  if (next.preset) setPreset(next.preset);
  else if (style.preset && style !== next) setPreset('tactica');
  style = next;
  view.build(next);
  setPaused(false);
  navEl.querySelectorAll('button').forEach((b) => b.classList.toggle('on', b.dataset.id === next.id));
  renderInfo();
}

async function keyMoment(): Promise<void> {
  await view.keyMoment();
  setPaused(view.paused);
}

const esc = (s: string) => s.replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' })[c]!);

function renderInfo(): void {
  const s = style;
  infoEl.innerHTML = `
    <h2>${esc(s.name)}</h2>
    <p class="tagline">${esc(s.tagline)}</p>
    <div class="cost cost-${s.cost.replace(' ', '-').toLowerCase()}">Coste de arte: <b>${s.cost}</b></div>
    <h3>Referencias</h3>
    <p class="refs">${s.refs.map(esc).join(' · ')}</p>
    <h3>Cómo se harían los modelos</h3>
    <p>${esc(s.assets)}</p>
    <h3>A favor</h3>
    <ul class="pros">${s.pros.map((p) => `<li>${esc(p)}</li>`).join('')}</ul>
    <h3>En contra</h3>
    <ul class="cons">${s.cons.map((p) => `<li>${esc(p)}</li>`).join('')}</ul>`;
}

for (const family of FAMILIES) {
  const group = document.createElement('details');
  group.open = family.open;
  group.innerHTML = `<summary>${esc(family.label)}</summary><div class="list"></div>`;
  const list = group.querySelector('.list')!;
  STYLES.forEach((s, i) => {
    if (s.family !== family.id) return;
    const b = document.createElement('button');
    b.dataset.id = s.id;
    b.innerHTML = `<kbd>${i < 9 ? i + 1 : i === 9 ? 0 : ''}</kbd><span>${esc(s.name)}</span>`;
    b.onclick = () => selectStyle(i);
    list.append(b);
  });
  navEl.append(group);
}

for (const [id, p] of Object.entries(CAMERA_PRESETS) as [PresetId, (typeof CAMERA_PRESETS)[PresetId]][]) {
  const b = document.createElement('button');
  b.textContent = p.label;
  b.onclick = () => setPreset(id);
  camsEl.append(b);
}

function toggleUi(): void {
  view.setUi(!view.showUi);
  root.querySelector('[data-act="ui"]')!.classList.toggle('on', view.showUi);
}

root.querySelector<HTMLButtonElement>('[data-act="pause"]')!.onclick = () => setPaused(!view.paused);
root.querySelector<HTMLButtonElement>('[data-act="moment"]')!.onclick = () => void keyMoment();
root.querySelector<HTMLButtonElement>('[data-act="ui"]')!.onclick = toggleUi;

window.addEventListener('keydown', (e) => {
  const i = STYLES.indexOf(style);
  if (e.key >= '1' && e.key <= '9') selectStyle(Number(e.key) - 1);
  else if (e.key === '0') selectStyle(9);
  else if (e.key === 'ArrowRight') selectStyle(i + 1);
  else if (e.key === 'ArrowLeft') selectStyle(i - 1);
  else if (e.code === 'Space') setPaused(!view.paused);
  else if (e.key === 'm' || e.key === 'M') void keyMoment();
  else if (e.key === 'u' || e.key === 'U') toggleUi();
  else if (e.key === 'h' || e.key === 'H') root.classList.toggle('bare');
  else return;
  e.preventDefault();
});

let initial = 0;
try {
  initial = Math.max(0, STYLES.findIndex((s) => s.id === localStorage.getItem(STORE_KEY)));
} catch {
  /* default style */
}
setPreset('tactica');
selectStyle(initial);

// ---------------------------------------------------------- tooling

// Captures from a hidden tab: step the clock by hand and post frames to the
// dev server's /__snap endpoint (saved under .dev-snapshots/).
Object.assign(window, {
  __lab: {
    styles: STYLES.map((s) => s.id),
    set: (id: string) => selectStyle(STYLES.findIndex((s) => s.id === id)),
    preset: setPreset,
    camera(pos: [number, number, number], target: [number, number, number]) {
      eye.position.set(...pos);
      controls.target.set(...target);
      controls.update();
      view.setPose(eye.position, controls.target);
    },
    moment: keyMoment,
    actor: (id: string) => view.actorState(id),
    pose: (id: string, patch: Record<string, number>) => view.posePatch(id, patch),
    step: (n = 30, dt = 1 / 30) => view.step(n, dt),
    ui: (on: boolean) => on !== view.showUi && toggleUi(),
    async snap(name: string, width = 1280) {
      view.draw();
      const src = view.canvas;
      const c = document.createElement('canvas');
      c.width = width;
      c.height = Math.round((width * src.height) / src.width);
      const g = c.getContext('2d')!;
      g.imageSmoothingEnabled = !style.pixelScale || !!style.pixelSmooth;
      g.drawImage(src, 0, 0, c.width, c.height);
      g.imageSmoothingEnabled = true;
      g.drawImage(view.hudCanvas, 0, 0, c.width, c.height);
      await fetch(`/__snap?name=${name}`, { method: 'POST', body: c.toDataURL('image/jpeg', 0.88) });
      return name;
    },
    get state() {
      return { style: style.id, time: view.clock.time, paused: view.paused, size: [WIDTH, DEPTH] };
    },
  },
});
