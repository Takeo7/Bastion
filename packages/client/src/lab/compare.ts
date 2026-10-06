import '@fontsource/rajdhani/latin-500.css';
import '@fontsource/rajdhani/latin-600.css';
import '@fontsource/rajdhani/latin-700.css';
import './compare.css';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { FAMILIES, STYLES } from './catalog';
import type { ArtStyle, Family } from './stylekit';
import { CAMERA_PRESETS, LabView, preloadFonts, type PresetId } from './view';
import { openVoteStore, type Person, type Ratings, type VoteStore } from './votes';

// Style comparison: two or four live views with the same camera and the same
// moment of the skirmish, a curtain to wipe between two styles, and votes.

type Layout = 'dos' | 'cortina' | 'cuatro';
const LAYOUTS: { id: Layout; label: string; panes: number }[] = [
  { id: 'dos', label: 'Dos', panes: 2 },
  { id: 'cortina', label: 'Cortina', panes: 2 },
  { id: 'cuatro', label: 'Cuatro', panes: 4 },
];
const SLOTS = ['A', 'B', 'C', 'D'];
const FAMILY_SHORT: Record<Family, string> = {
  voxel: 'Vóxel',
  tinta: 'Tinta',
  comic: 'Cómic',
  sorpresa: 'Sorpresa',
  doggy: 'Retro sucio',
  aparcado: 'Aparcado',
};
const PREFS_KEY = 'bastion.cmp.prefs';
const BAR_SPACE = 52;
const styleById = new Map(STYLES.map((s) => [s.id, s]));

interface Prefs {
  layout: Layout;
  styles: string[];
  curtain: number;
  votesOpen: boolean;
}

function loadPrefs(): Prefs {
  const base: Prefs = { layout: 'dos', styles: ['voxel99', 'tebeo', 'vhs', 'linternas'], curtain: 50, votesOpen: true };
  try {
    const saved = JSON.parse(localStorage.getItem(PREFS_KEY) ?? '{}') as Partial<Prefs>;
    const styles = Array.isArray(saved.styles) ? saved.styles.filter((id) => styleById.has(id)) : [];
    return {
      layout: LAYOUTS.some((l) => l.id === saved.layout) ? saved.layout! : base.layout,
      styles: [...styles, ...base.styles].slice(0, 4),
      curtain: typeof saved.curtain === 'number' ? Math.min(95, Math.max(5, saved.curtain)) : base.curtain,
      votesOpen: saved.votesOpen ?? base.votesOpen,
    };
  } catch {
    return base;
  }
}

const prefs = loadPrefs();
function savePrefs(): void {
  try {
    localStorage.setItem(PREFS_KEY, JSON.stringify(prefs));
  } catch {
    /* preferences just are not remembered */
  }
}

const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);

// --------------------------------------------------------------- DOM

const root = document.getElementById('cmp')!;
root.innerHTML = `
  <header class="bar">
    <div class="brand">
      <span class="kicker">Bastión · M5</span>
      <h1>Comparador de estilos</h1>
    </div>
    <div class="tools">
      <div class="seg" role="group" aria-label="Disposición" data-group="layout"></div>
      <div class="seg" role="group" aria-label="Cámara" data-group="camera"></div>
      <div class="seg" role="group" aria-label="Tiempo">
        <button type="button" id="act-pause">Pausa</button>
        <button type="button" id="act-moment" title="Congela todas las vistas en el disparo con la explosión">Momento clave</button>
        <button type="button" id="act-ui" aria-pressed="false" title="Alcance de movimiento, ruta y cobertura">Interfaz táctica</button>
      </div>
      <button type="button" class="votes-toggle" id="act-votes" aria-pressed="true">Votos <span class="count"></span></button>
      <span class="fps" aria-hidden="true"></span>
    </div>
  </header>
  <main class="app">
    <section class="stage" data-layout="dos">
      <div class="panes"></div>
      <div class="orbit" title="Arrastra para girar la cámara de todas las vistas; rueda para acercar"></div>
      <div class="bars"></div>
      <div class="curtain" hidden role="slider" aria-label="Posición de la cortina" tabindex="0"><span></span></div>
    </section>
    <aside class="votes" aria-label="Votos">
      <header>
        <h2>Votos</h2>
        <p class="scope">Conectando…</p>
      </header>
      <ol class="ranking"></ol>
    </aside>
  </main>`;

const appEl = root.querySelector<HTMLElement>('.app')!;
const stageEl = root.querySelector<HTMLElement>('.stage')!;
const panesEl = root.querySelector<HTMLElement>('.panes')!;
const barsEl = root.querySelector<HTMLElement>('.bars')!;
const orbitEl = root.querySelector<HTMLElement>('.orbit')!;
const curtainEl = root.querySelector<HTMLElement>('.curtain')!;
const rankingEl = root.querySelector<HTMLOListElement>('.ranking')!;
const scopeEl = root.querySelector<HTMLElement>('.scope')!;
const countEl = root.querySelector<HTMLElement>('.count')!;
const fpsEl = root.querySelector<HTMLElement>('.fps')!;
void preloadFonts();

// ------------------------------------------------------------- panes

interface Pane {
  el: HTMLDivElement;
  bar: HTMLDivElement;
  select: HTMLSelectElement;
  stars: HTMLElement;
  avg: HTMLElement;
  view: LabView | null;
  style: ArtStyle;
}

function styleOptions(): string {
  return FAMILIES.map(
    (f) =>
      `<optgroup label="${esc(f.label)}">${STYLES.filter((s) => s.family === f.id)
        .map((s) => `<option value="${s.id}">${esc(s.name)}</option>`)
        .join('')}</optgroup>`,
  ).join('');
}

function starButtons(label: string): string {
  return [1, 2, 3, 4, 5].map((n) => `<button type="button" data-score="${n}" aria-label="${label} ${n} de 5">★</button>`).join('');
}

let focused = 0;
const panes: Pane[] = SLOTS.map((slot, i) => {
  const el = document.createElement('div');
  el.className = 'pane';
  el.dataset.i = String(i);
  panesEl.append(el);
  const bar = document.createElement('div');
  bar.className = 'pane-bar';
  bar.dataset.i = String(i);
  bar.innerHTML = `
    <span class="slot">${slot}</span>
    <select id="pane-style-${i}" aria-label="Estilo de la vista ${slot}">${styleOptions()}</select>
    <span class="stars" role="group" aria-label="Tu puntuación">${starButtons('Puntuar con')}</span>
    <span class="avg"></span>`;
  barsEl.append(bar);
  const select = bar.querySelector('select')!;
  const style = styleById.get(prefs.styles[i]!) ?? STYLES[i]!;
  select.value = style.id;
  const pane: Pane = { el, bar, select, stars: bar.querySelector('.stars')!, avg: bar.querySelector('.avg')!, view: null, style };
  select.onchange = () => setPaneStyle(i, select.value);
  bar.addEventListener('pointerdown', () => focusPane(i));
  pane.stars.onclick = (e) => {
    const score = Number((e.target as HTMLElement).dataset.score);
    if (score) rate(pane.style.id, score);
  };
  return pane;
});

const layoutPanes = () => LAYOUTS.find((l) => l.id === prefs.layout)!.panes;
const activePanes = () => panes.slice(0, layoutPanes());

function ensureView(p: Pane): LabView {
  if (!p.view) {
    p.view = new LabView(p.el, { maxPixelRatio: prefs.layout === 'cuatro' ? 1.25 : 1.5, insets: () => ({ left: 0, bottom: BAR_SPACE }) });
    p.view.setUi(showUi);
    p.view.build(p.style);
  }
  return p.view;
}

/** Every visible view restarts the skirmish together, so they show the same instant. */
function restartAll(): void {
  for (const p of activePanes()) {
    const v = ensureView(p);
    v.paused = false;
    v.restart();
  }
  setPausedLabel(false);
}

function setPaneStyle(i: number, id: string): void {
  const p = panes[i]!;
  const style = styleById.get(id);
  if (!style) return;
  p.style = style;
  p.select.value = id;
  prefs.styles[i] = id;
  savePrefs();
  if (p.view) p.view.build(style);
  else ensureView(p);
  restartAll();
  focusPane(i);
  renderVotes();
}

function focusPane(i: number): void {
  focused = i;
  panes.forEach((p, n) => p.el.classList.toggle('focused', n === i && layoutPanes() > 1));
}

function setLayout(layout: Layout): void {
  prefs.layout = layout;
  savePrefs();
  stageEl.dataset.layout = layout;
  const count = layoutPanes();
  panes.forEach((p, i) => {
    p.el.hidden = i >= count;
    p.bar.hidden = i >= count;
  });
  curtainEl.hidden = layout !== 'cortina';
  applyCurtain();
  root.querySelectorAll<HTMLButtonElement>('[data-group="layout"] button').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.id === layout)));
  if (focused >= count) focused = 0;
  focusPane(focused);
  restartAll();
}

// ------------------------------------------------------------ curtain

function applyCurtain(): void {
  const x = prefs.curtain;
  curtainEl.style.left = `${x}%`;
  curtainEl.setAttribute('aria-valuenow', String(Math.round(x)));
  panes[1]!.el.style.clipPath = prefs.layout === 'cortina' ? `inset(0 0 0 ${x}%)` : '';
}

curtainEl.addEventListener('pointerdown', (e) => {
  curtainEl.setPointerCapture(e.pointerId);
  const move = (ev: PointerEvent) => {
    const r = stageEl.getBoundingClientRect();
    prefs.curtain = Math.min(95, Math.max(5, ((ev.clientX - r.left) / r.width) * 100));
    applyCurtain();
  };
  const up = () => {
    curtainEl.removeEventListener('pointermove', move);
    curtainEl.removeEventListener('pointerup', up);
    savePrefs();
  };
  curtainEl.addEventListener('pointermove', move);
  curtainEl.addEventListener('pointerup', up);
});
curtainEl.addEventListener('keydown', (e) => {
  const step = e.key === 'ArrowLeft' ? -5 : e.key === 'ArrowRight' ? 5 : 0;
  if (!step) return;
  prefs.curtain = Math.min(95, Math.max(5, prefs.curtain + step));
  applyCurtain();
  savePrefs();
  e.preventDefault();
});

// ------------------------------------------------------------- camera

const eye = new THREE.PerspectiveCamera(40, 1, 0.1, 400);
const controls = new OrbitControls(eye, orbitEl);
controls.enableDamping = true;
controls.maxPolarAngle = THREE.MathUtils.degToRad(86);
controls.minDistance = 3;
controls.maxDistance = 60;

function setPreset(id: PresetId): void {
  const p = CAMERA_PRESETS[id];
  eye.position.set(p.pos[0], p.pos[1], p.pos[2]);
  controls.target.set(p.target[0], p.target[1], p.target[2]);
  controls.update();
}

// --------------------------------------------------------------- time

let showUi = false;

function setPausedLabel(paused: boolean): void {
  root.querySelector('#act-pause')!.textContent = paused ? 'Reanudar' : 'Pausa';
}

root.querySelector<HTMLButtonElement>('#act-pause')!.onclick = () => {
  const paused = !activePanes().every((p) => p.view?.paused);
  for (const p of activePanes()) if (p.view) p.view.paused = paused;
  setPausedLabel(paused);
};
root.querySelector<HTMLButtonElement>('#act-moment')!.onclick = async () => {
  await Promise.all(activePanes().map((p) => ensureView(p).keyMoment()));
  setPausedLabel(true);
};
root.querySelector<HTMLButtonElement>('#act-ui')!.onclick = (e) => {
  showUi = !showUi;
  (e.currentTarget as HTMLElement).setAttribute('aria-pressed', String(showUi));
  for (const p of panes) p.view?.setUi(showUi);
};
root.querySelector<HTMLButtonElement>('#act-votes')!.onclick = (e) => {
  prefs.votesOpen = !prefs.votesOpen;
  savePrefs();
  (e.currentTarget as HTMLElement).setAttribute('aria-pressed', String(prefs.votesOpen));
  appEl.classList.toggle('no-votes', !prefs.votesOpen);
};

for (const l of LAYOUTS) {
  const b = document.createElement('button');
  b.type = 'button';
  b.dataset.id = l.id;
  b.textContent = l.label;
  b.onclick = () => setLayout(l.id);
  root.querySelector('[data-group="layout"]')!.append(b);
}
for (const [id, p] of Object.entries(CAMERA_PRESETS) as [PresetId, (typeof CAMERA_PRESETS)[PresetId]][]) {
  const b = document.createElement('button');
  b.type = 'button';
  b.textContent = p.label;
  b.onclick = () => setPreset(id);
  root.querySelector('[data-group="camera"]')!.append(b);
}

let last = performance.now();
let fpsAvg = 60;
function frame(now: number): void {
  const dt = Math.min(0.1, (now - last) / 1000);
  last = now;
  controls.update();
  for (const p of activePanes()) {
    if (!p.view) continue;
    p.view.setPose(eye.position, controls.target);
    p.view.advance(dt);
    p.view.draw();
  }
  fpsAvg += (1 / Math.max(dt, 0.001) - fpsAvg) * 0.05;
  fpsEl.textContent = `${Math.round(fpsAvg)} fps`;
  requestAnimationFrame(frame);
}

// -------------------------------------------------------------- votes

let store: VoteStore | null = null;
let people = new Map<string, Ratings>();
let names: Record<string, Person> = {};
const rows = new Map<string, HTMLLIElement>();
const noteTimers = new Map<string, number>();

const mine = (): Ratings => (store ? (people.get(store.me) ?? {}) : {});

function rate(styleId: string, score: number): void {
  if (!store?.canWrite) return;
  const r: Ratings = { ...mine() };
  if (r[styleId]?.score === score) delete r[styleId];
  else r[styleId] = { score, note: r[styleId]?.note ?? '' };
  people.set(store.me, r);
  store.save(r);
  renderVotes();
}

function setNote(styleId: string, note: string): void {
  if (!store?.canWrite) return;
  const r: Ratings = { ...mine() };
  const current = r[styleId];
  if (!current || current.note === note) return;
  r[styleId] = { ...current, note };
  people.set(store.me, r);
  store.save(r);
}

function stats(styleId: string): { avg: number; count: number } {
  let sum = 0;
  let count = 0;
  for (const ratings of people.values()) {
    const r = ratings[styleId];
    if (r) {
      sum += r.score;
      count++;
    }
  }
  return { avg: count ? sum / count : 0, count };
}

const fmt = (n: number) => n.toLocaleString('es-ES', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

function paintStars(el: HTMLElement, score: number): void {
  el.querySelectorAll<HTMLButtonElement>('button').forEach((b) => {
    const on = Number(b.dataset.score) <= score;
    b.classList.toggle('on', on);
    b.setAttribute('aria-pressed', String(Number(b.dataset.score) === score));
    b.disabled = !store?.canWrite;
  });
}

function makeRow(style: ArtStyle): HTMLLIElement {
  const li = document.createElement('li');
  li.className = 'row';
  li.dataset.id = style.id;
  li.innerHTML = `
    <div class="row-head">
      <button type="button" class="name" title="Mostrar en la vista seleccionada">${esc(style.name)}</button>
      <span class="fam">${esc(FAMILY_SHORT[style.family])}</span>
      <span class="avg"></span>
    </div>
    <p class="tag">${esc(style.tagline)}</p>
    <div class="row-vote">
      <span class="stars" role="group" aria-label="Tu puntuación para ${esc(style.name)}">${starButtons('Puntuar con')}</span>
      <input class="note" id="note-${style.id}" maxlength="280" autocomplete="off" aria-label="Tu nota sobre ${esc(style.name)}" />
    </div>
    <ul class="others"></ul>`;
  li.querySelector<HTMLButtonElement>('.name')!.onclick = () => setPaneStyle(focused, style.id);
  li.querySelector<HTMLElement>('.stars')!.onclick = (e) => {
    const score = Number((e.target as HTMLElement).dataset.score);
    if (score) rate(style.id, score);
  };
  const note = li.querySelector<HTMLInputElement>('.note')!;
  note.oninput = () => {
    clearTimeout(noteTimers.get(style.id));
    noteTimers.set(style.id, window.setTimeout(() => setNote(style.id, note.value.trim()), 800));
  };
  note.onchange = () => setNote(style.id, note.value.trim());
  note.onblur = () => renderVotes();
  return li;
}

function renderVotes(): void {
  if (!store) return;
  const me = mine();
  const order = [...STYLES].sort((a, b) => {
    const sa = stats(a.id);
    const sb = stats(b.id);
    return sb.avg - sa.avg || sb.count - sa.count || STYLES.indexOf(a) - STYLES.indexOf(b);
  });
  const editing = document.activeElement instanceof HTMLInputElement && rankingEl.contains(document.activeElement);
  for (const style of order) {
    let li = rows.get(style.id);
    if (!li) rows.set(style.id, (li = makeRow(style)));
    const { avg, count } = stats(style.id);
    li.querySelector('.avg')!.innerHTML = count ? `<b>${fmt(avg)}</b><small>${count === 1 ? '1 voto' : `${count} votos`}</small>` : '<small>Sin votos</small>';
    paintStars(li.querySelector('.stars')!, me[style.id]?.score ?? 0);
    const note = li.querySelector<HTMLInputElement>('.note')!;
    note.disabled = !store.canWrite || !me[style.id];
    note.placeholder = me[style.id] ? 'Tu nota (opcional)' : 'Puntúa para añadir una nota';
    if (document.activeElement !== note) note.value = me[style.id]?.note ?? '';
    const others = [...people.entries()].filter(([id, r]) => id !== store!.me && r[style.id]);
    const list = li.querySelector('.others')!;
    list.replaceChildren(
      ...others.map(([id, r]) => {
        const item = document.createElement('li');
        const dot = document.createElement('i');
        dot.style.background = names[id]?.color ?? '#8aa1b0';
        const who = document.createElement('b');
        who.textContent = `${names[id]?.name || 'Alguien'} · ${r[style.id]!.score}★`;
        item.append(dot, who);
        if (r[style.id]!.note) {
          const text = document.createElement('span');
          text.textContent = r[style.id]!.note;
          item.append(text);
        }
        return item;
      }),
    );
    // Reordering moves elements; leave the order alone while someone is typing.
    if (!editing) rankingEl.append(li);
  }
  for (const p of panes) {
    paintStars(p.stars, me[p.style.id]?.score ?? 0);
    const { avg, count } = stats(p.style.id);
    p.avg.textContent = count ? `${fmt(avg)} · ${count}` : '';
    p.avg.title = count ? `Media ${fmt(avg)} de ${count} ${count === 1 ? 'voto' : 'votos'}` : '';
  }
  const voters = [...people.values()].filter((r) => Object.keys(r).length).length;
  countEl.textContent = voters ? String(voters) : '';
  scopeEl.textContent = !store.shared
    ? 'Se guardan solo en este navegador. En la página publicada se comparten con quien la abra.'
    : store.canWrite
      ? 'Compartidos: cada persona que abre esta página puntúa por su cuenta y ve los votos de los demás.'
      : 'Tienes acceso de solo lectura: ves los votos de los demás, pero no puedes votar.';
}

async function connectVotes(): Promise<void> {
  store = await openVoteStore();
  store.onChange(async (next) => {
    people = new Map(next);
    renderVotes();
    const ids = [...people.keys()];
    if (ids.length && store) {
      names = await store.people(ids);
      renderVotes();
    }
  });
}

// --------------------------------------------------------------- boot

appEl.classList.toggle('no-votes', !prefs.votesOpen);
root.querySelector('#act-votes')!.setAttribute('aria-pressed', String(prefs.votesOpen));
setPreset('tactica');
setLayout(prefs.layout);
requestAnimationFrame(frame);
void connectVotes();

// Dev handle for captures (the lab page has the full tooling).
Object.assign(window, {
  __cmp: {
    layout: setLayout,
    style: setPaneStyle,
    preset: setPreset,
    moment: () => Promise.all(activePanes().map((p) => ensureView(p).keyMoment())),
    views: () => panes.map((p) => p.view),
    get panes() {
      return activePanes().map((p) => ({ style: p.style.id, time: p.view?.clock.time ?? 0, paused: p.view?.paused ?? false }));
    },
  },
});
