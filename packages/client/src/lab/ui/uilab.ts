import '@fontsource/rajdhani/latin-500.css';
import '@fontsource/rajdhani/latin-600.css';
import '@fontsource/rajdhani/latin-700.css';
import './uilab.css';
import './themes.css';
import './screens.css';
import './themes-base.css';
import { createCampaign, Rng, type CampaignState } from '@bastion/engine';
import * as THREE from 'three';
import type { CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js';
import { BastionSet, type LineupEntry } from '../../strategy/bastion';
import { GeoscapeSet } from '../../strategy/geoscape';
import { Stage, type Shot, type StageSet } from '../../strategy/stage';
import { STYLES } from '../catalog';
import { CAMERA_PRESETS, LabView, preloadFonts } from '../view';
import { ICONS } from './icons';
import { CHROME, SCREEN_HTML, SCREENS, type ScreenId } from './screens';

// UI art lab (M5): the tactical HUD and every campaign screen, with sample
// data, in several interface art directions. Combat sits over the lab's live
// scene; the base screens over the game's own 3D Bastion and globe.

interface UiTheme {
  id: string;
  name: string;
  idea: string;
  pairs: string;
  pros: string[];
  cons: string[];
}

// Only the themes that fit the chosen art (vóxel and low-poly) remain.
const THEMES: UiTheme[] = [
  {
    id: 'actual',
    name: 'Actual',
    idea: 'La interfaz de hoy: paneles oscuros con borde cian, solo texto en los botones.',
    pairs: 'Referencia para comparar',
    pros: ['Ya existe y funciona', 'Sobria, no tapa la escena'],
    cons: [
      'Botones sin iconos; los motivos de “no disponible” solo en tooltips nativos',
      'Dos juegos de estilos (combate y base) que no casan',
      'Genérica: no dice nada del estilo de arte',
    ],
  },
  {
    id: 'holo',
    name: 'Holotáctico',
    idea: 'Evolución del actual: esquinas cortadas, iconos, indicador circular de probabilidad y un solo sistema de fichas.',
    pairs: 'Vóxel · Low-poly',
    pros: ['El paso más corto: unifica combate y base', 'Casa con el cian y los brillos del low-poly', 'Iconos y estados claros'],
    cons: ['Seguro pero poco distintivo', 'Se parece a muchos juegos del género'],
  },
  {
    id: 'expediente',
    name: 'Expediente',
    idea: 'Carpeta clasificada: papel, máquina de escribir, sellos de goma, clips y notas a boli de tu compañero.',
    pairs: 'Vóxel · Low-poly',
    pros: ['Perfecto para la base y los informes de misión', 'Muy cooperativo: notas escritas a mano entre jugadores', 'Distinto a todo lo que hay en el género'],
    cons: ['En combate es menos inmediato que un HUD clásico', 'Papel sobre escenas sci-fi: la ficción tiene que justificarlo (la resistencia tira de papel)'],
  },
];

/** The art styles chosen for the game: the only backgrounds the lab offers. */
const ARTS = ['voxel', 'polygon'] as const;
const ART_NAMES: Record<(typeof ARTS)[number], string> = {
  voxel: 'Vóxel',
  polygon: 'Low-poly',
};

const params = new URLSearchParams(location.search);
let theme = THEMES.find((t) => t.id === params.get('tema')) ?? THEMES[2]!;
let art: (typeof ARTS)[number] = ARTS.find((a) => a === params.get('fondo')) ?? 'voxel';
/** Old links used `?pantalla=base` for the geoscape. */
const toScreen = (id: string | null): ScreenId => (id === 'base' ? 'geoesfera' : (SCREENS.find((x) => x.id === id)?.id ?? 'combate'));
let screen: ScreenId = toScreen(params.get('pantalla'));
let showBanner = true;

const esc = (s: string) => s.replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' })[c]!);
const pips = (n: number, of: number, cls = 'pip') => Array.from({ length: of }, (_, i) => `<i class="${cls}${i < n ? ' on' : ''}"></i>`).join('');

// --------------------------------------------------------------- mock UI

const HUD = `
  <div class="h-top">
    <div class="h-turn"><b>TURNO 3</b><span>ESCUADRA</span><em class="chip ok">OCULTOS</em></div>
    <div class="h-objective"><span class="k">ELIMINACIÓN</span> Eliminad a todos los hostiles <span class="timer">4 turnos</span></div>
    <div class="h-players">
      <span class="pl p1"><i></i>Takeo · actuando</span>
      <span class="pl p2"><i></i>Compañero · listo</span>
    </div>
  </div>
  <ul class="h-squad">
    ${[
      ['FRT', 'Marta Soler', 4, 5, 2, 'p1', ''],
      ['ASL', 'Irene Galán', 5, 5, 1, 'p2', 'AGAZAPADA'],
      ['GRN', 'Clara Bosch', 5, 5, 2, 'p1', ''],
      ['ESP', 'Julia Font', 3, 5, 0, 'p2', 'VIGILANCIA'],
    ]
      .map(
        ([cls, name, hp, max, ap, owner, st], i) => `
      <li class="${owner}${i === 0 ? ' sel' : ''}">
        <span class="cls">${cls}</span><span class="nm">${name}</span>
        <span class="hp">${pips(hp as number, max as number)}</span>
        <span class="ap">${pips(ap as number, 2, 'ap')}</span>
        ${st ? `<em class="chip">${st}</em>` : ''}
      </li>`,
      )
      .join('')}
  </ul>
  <div class="h-unit">
    <div class="portrait">${ICONS.shoot}</div>
    <div class="who"><b>Marta Soler</b><span>Cabo · Francotiradora · Rifle de precisión</span></div>
    <dl>
      <div><dt>Salud</dt><dd>4/5</dd></div><div><dt>Acciones</dt><dd>2/2</dd></div><div><dt>Munición</dt><dd>3/4</dd></div><div><dt>Puntería</dt><dd>72</dd></div>
    </dl>
  </div>
  <div class="h-abilities">
    ${[
      ['shoot', 'Disparar', '1', 'active', ''],
      ['overwatch', 'Vigilancia', '2', '', ''],
      ['pistol', 'Pistola', '3', 'free', ''],
      ['grenade', 'Granada', '4', 'off', 'Sin granadas'],
      ['hunker', 'Agazaparse', '5', '', ''],
      ['reload', 'Recargar', '6', '', ''],
    ]
      .map(
        ([icon, label, key, state, reason]) => `
      <button class="ab ${state}" ${state === 'off' ? 'aria-disabled="true"' : ''}>
        <span class="key">${key}</span>${ICONS[icon as keyof typeof ICONS]}<span class="lb">${label}</span>
        ${reason ? `<span class="why">${reason}</span>` : ''}
      </button>`,
      )
      .join('')}
    <div class="ab-tip"><b>Disparar</b> · 1 acción, termina el turno · 4–6 de daño</div>
  </div>
  <div class="h-target">
    <div class="t-head"><button class="cyc">‹</button><span class="t-name">${ICONS.alien}Soldado alienígena</span><span class="t-count">1/3</span><button class="cyc">›</button></div>
    <div class="t-odds">
      <div class="hit" style="--p: 72"><span class="num">72<small>%</small></span><span class="lbl">Impacto</span></div>
      <div class="crit"><span class="num">18<small>%</small></span><span class="lbl">Crítico</span></div>
    </div>
    <ul class="t-mods">
      <li><span>Puntería</span><b class="up">+65</b></li>
      <li><span>Altura</span><b class="up">+10</b></li>
      <li><span>Distancia corta</span><b class="up">+17</b></li>
      <li><span>Cobertura baja</span><b class="down">−20</b></li>
    </ul>
    <div class="t-actions"><button class="go">Disparar <kbd>Espacio</kbd></button><button class="cancel">Cancelar <kbd>Esc</kbd></button></div>
    <span class="note">¡Ojo! Está flanqueando a Irene</span>
  </div>
  <div class="h-end"><button><b>TERMINAR TURNO</b><span>Retroceso</span></button></div>
  <ol class="h-log">
    <li class="x">Marta Soler entra en vigilancia</li>
    <li class="a">Un soldado alienígena se mueve a cobertura</li>
    <li class="w">¡Contacto! Grupo de 3 hostiles</li>
  </ol>
  <div class="h-banner"><span>¡CONTACTO!</span></div>
  <div class="h-flag f-sharp"><span class="fn">FRT · Marta Soler</span><span class="fr">${ICONS.coverFull}${pips(4, 5)}<em class="chip">VIG</em></span></div>
  <div class="h-flag f-alien alien"><span class="fn">Soldado alienígena</span><span class="fr">${ICONS.coverHalf}${pips(3, 4)}</span></div>
  <div class="h-float">−4</div>`;

// ----------------------------------------------------------------- page

const root = document.getElementById('ui-lab')!;
root.innerHTML = `
  <header class="uil-bar">
    <div class="uil-title"><span>Bastión · M5</span><b>Interfaz</b></div>
    <nav class="uil-themes"></nav>
    <div class="uil-tools">
      <span class="uil-label">Fondo</span>${ARTS.map((a) => `<button data-art="${a}">${ART_NAMES[a]}</button>`).join('')}
      <button id="uil-banner" aria-pressed="true">Aviso</button>
    </div>
    <nav class="uil-screens">${SCREENS.map((x) => `<button data-screen="${x.id}">${x.name}</button>`).join('')}</nav>
    <p class="uil-info"></p>
  </header>
  <main class="uil-stage">
    <div class="uil-scene"></div>
    <div class="uil-base"></div>
    <div class="ui hud">${HUD}</div>
    <div class="ui strat">${CHROME}${SCREENS.filter((x) => SCREEN_HTML[x.id])
      .map((x) => `<div class="scr" data-scr="${x.id}" hidden>${SCREEN_HTML[x.id]}</div>`)
      .join('')}</div>
  </main>`;

const stage = root.querySelector<HTMLElement>('.uil-stage')!;
const sceneEl = root.querySelector<HTMLElement>('.uil-scene')!;
const baseEl = root.querySelector<HTMLElement>('.uil-base')!;
const info = root.querySelector<HTMLElement>('.uil-info')!;
const hudEl = root.querySelector<HTMLElement>('.hud')!;
const flagSharp = root.querySelector<HTMLElement>('.f-sharp')!;
const flagAlien = root.querySelector<HTMLElement>('.f-alien')!;
const floatEl = root.querySelector<HTMLElement>('.h-float')!;
void preloadFonts();

const view = new LabView(sceneEl, { maxPixelRatio: 1.5 });
const eye = new THREE.Vector3(...CAMERA_PRESETS.tactica.pos);
const target = new THREE.Vector3(...CAMERA_PRESETS.tactica.target);

function setArt(id: (typeof ARTS)[number]): void {
  art = id;
  view.setPose(eye, target);
  view.build(STYLES.find((s) => s.id === id)!);
  render();
}

// ------------------------------------------------------- base backdrop

const P1 = '#3fa9ff';
const P2 = '#ffa630';
const SQUAD: LineupEntry[] = [
  { template: 'sharpshooter', color: P1 },
  { template: 'grenadier', color: P1 },
  { template: 'assault', color: P2 },
  { template: 'specialist', color: P2 },
];

/** Day 12 of a two-player campaign: simulation room and workshop built, the infirmary half done. */
function sampleCampaign(): CampaignState {
  const c = createCampaign([0, 1], new Rng(7));
  c.day = 12;
  c.doom = 4;
  const slot = (id: string) => c.base.slots.find((x) => x.id === id)!;
  slot('r0c0').facility = 'simulation';
  slot('r0c1').facility = 'workshop';
  slot('r1c0').excavated = true;
  c.base.construction = {
    slot: 'r0c2',
    kind: 'build',
    facility: 'infirmary',
    daysLeft: 3,
    total: 6,
  };
  for (const r of c.regions) {
    if (r.id === 'na') r.contacted = true;
    if (r.id === 'me') r.facility = { nextDoomDay: 20 };
  }
  return c;
}

let base: { stage: Stage; bastion: BastionSet; geo: GeoscapeSet } | null = null;

/** The game's own strategy sets, built the first time a base screen opens. */
function ensureBase(): NonNullable<typeof base> {
  if (base) return base;
  const st = new Stage();
  const bastion = new BastionSet(() => {});
  const geo = new GeoscapeSet(() => {});
  st.add('bastion', bastion);
  st.add('geo', geo);
  baseEl.append(st.element);
  const c = sampleCampaign();
  bastion.setBase(c);
  bastion.setPartner('barracks', P2, 'Compañero');
  geo.update(c, 'weu');
  geo.flyTo('weu');
  base = { stage: st, bastion, geo };
  return base;
}

function showBase(id: ScreenId): void {
  const { stage: st, bastion, geo } = ensureBase();
  bastion.setLabels(id === 'bastion' || id === 'acontecimiento' || id === 'instalaciones');
  bastion.select(id === 'instalaciones' ? 'r1c0' : null);
  bastion.setShowcase(id === 'cuartel' || id === 'ascenso' ? 'sharpshooter' : null, P1);
  const fates = ['ok', 'wounded', 'ok', 'dead'] as const;
  bastion.setLineup(id === 'hangar' ? [...SQUAD, null, null] : id === 'informe' ? SQUAD.map((e, i) => ({ ...e, status: fates[i] })) : []);
  let set = 'bastion';
  let shot: Shot;
  switch (id) {
    case 'menu': {
      // Same framing as the main menu: the globe pushed to the right.
      const g = geo.shot();
      const shift = new THREE.Vector3(-4.2, 0, 0);
      shot = {
        ...g,
        pos: g.pos
          .clone()
          .add(shift)
          .add(new THREE.Vector3(0, 0, 1)),
        target: g.target.clone().add(shift),
      };
      set = 'geo';
      break;
    }
    case 'geoesfera':
      geo.focus('weu');
      shot = geo.shot();
      set = 'geo';
      break;
    case 'investigacion':
      shot = bastion.room('research');
      break;
    case 'ingenieria':
      shot = bastion.room('engineering');
      break;
    case 'cuartel':
    case 'ascenso':
      shot = bastion.armory();
      break;
    case 'instalaciones':
      shot = bastion.facilities();
      break;
    case 'hangar':
    case 'informe':
      shot = bastion.hangar();
      break;
    default:
      shot = bastion.overview();
  }
  void st.show(set, shot, true);
}

/** Lineup cards stand under each soldier on the launch pad, as in the game. */
function anchorCards(): void {
  const wrap = root.querySelector<HTMLElement>(`.scr[data-scr="${screen}"] .s-cards`);
  if (!base || !wrap) return;
  const cards = Array.from(wrap.children) as HTMLElement[];
  const pads = cards.map((_, i) => base!.stage.project(base!.bastion.padPosition(i, cards.length)));
  const spacing = pads.length > 1 ? Math.abs(pads[1]!.x - pads[0]!.x) : 140;
  wrap.style.setProperty('--card-w', `${Math.max(84, Math.min(150, spacing - 8))}px`);
  cards.forEach((el, i) => {
    el.style.left = `${pads[i]!.x}px`;
    el.style.top = `${pads[i]!.y + 12}px`;
  });
}

/**
 * Draws the base stage right now, after `ticks` animation steps. The stage
 * draws on requestAnimationFrame, which stops while the browser pane is
 * hidden; this lets captures work anyway.
 */
function drawBaseNow(ticks: number): void {
  if (!base) return;
  const s = base.stage as unknown as {
    resize(): void;
    gl: THREE.WebGLRenderer;
    camera: THREE.PerspectiveCamera;
    labels: Map<string, CSS2DRenderer>;
    sets: Map<string, StageSet>;
    active: string;
    post: {
      composer: { render(dt?: number): void };
      scene: { scene: THREE.Scene };
      ao: { scene: THREE.Scene; enabled: boolean };
    };
  };
  s.resize();
  const set = s.sets.get(s.active);
  if (!set) return;
  for (let i = 0; i < ticks; i++) set.tick(1 / 30, 10 + i / 30);
  // Same path as the stage's loop: the voxel post chain on the active set.
  s.post.scene.scene = set.scene;
  s.post.ao.scene = set.scene;
  s.post.ao.enabled = !!set.ambientOcclusion;
  s.post.composer.render(0);
  s.labels.get(s.active)?.render(set.scene, s.camera);
  anchorCards();
}

// ---------------------------------------------------------------- render

function render(): void {
  const def = SCREENS.find((x) => x.id === screen)!;
  stage.dataset.theme = theme.id;
  stage.dataset.screen = screen;
  stage.dataset.chrome = def.chrome;
  hudEl.classList.toggle('with-banner', showBanner);
  root.querySelectorAll<HTMLElement>('.scr').forEach((el) => (el.hidden = el.dataset.scr !== screen));
  root.querySelectorAll<HTMLButtonElement>('.s-tabs button').forEach((b) => b.classList.toggle('on', b.dataset.tab === def.tab));
  root.querySelectorAll<HTMLButtonElement>('.uil-themes button').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.id === theme.id)));
  root.querySelectorAll<HTMLButtonElement>('[data-screen]').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.screen === screen)));
  root.querySelector('#uil-banner')!.setAttribute('aria-pressed', String(showBanner));
  // The art style and the banner only apply to the combat scene.
  root.querySelectorAll<HTMLButtonElement>('[data-art]').forEach((b) => {
    b.setAttribute('aria-pressed', String(b.dataset.art === art));
    b.disabled = screen !== 'combate';
  });
  root.querySelector<HTMLButtonElement>('#uil-banner')!.disabled = screen !== 'combate';
  if (screen !== 'combate') showBase(screen);
  info.innerHTML = `<b>${esc(theme.name)}</b> ${esc(theme.idea)} <span class="pairs">Combina con: ${esc(theme.pairs)}</span>
    <span class="pc"><span class="good">+ ${theme.pros.map(esc).join(' · ')}</span><span class="bad">− ${theme.cons.map(esc).join(' · ')}</span></span>`;
}

function setTheme(t: UiTheme): void {
  theme = t;
  render();
}

for (const t of THEMES) {
  const b = document.createElement('button');
  b.dataset.id = t.id;
  b.textContent = t.name;
  b.onclick = () => setTheme(t);
  root.querySelector('.uil-themes')!.append(b);
}
root.querySelectorAll<HTMLButtonElement>('[data-screen]').forEach(
  (b) =>
    (b.onclick = () => {
      screen = toScreen(b.dataset.screen ?? null);
      render();
    }),
);
root.querySelector<HTMLButtonElement>('#uil-banner')!.onclick = () => {
  showBanner = !showBanner;
  render();
};
root.querySelectorAll<HTMLButtonElement>('[data-art]').forEach((b) => (b.onclick = () => setArt(b.dataset.art as (typeof ARTS)[number])));

/** Unit flags and the damage number follow the units on screen. */
function anchor(el: HTMLElement, p: [number, number, number]): void {
  const s = view.project(p);
  el.style.transform = `translate(${s.x}px, ${s.y}px)`;
}

let last = performance.now();
function frame(now: number): void {
  const dt = Math.min(0.1, (now - last) / 1000);
  last = now;
  if (screen === 'combate') {
    view.setPose(eye, target);
    view.advance(dt);
    view.draw();
    anchor(flagSharp, [4.5, 2.05, 6.5]);
    anchor(flagAlien, [13.5, 2.1, 5.5]);
    anchor(floatEl, [13.3, 2.6, 6.4]);
  } else {
    anchorCards();
  }
  requestAnimationFrame(frame);
}

setArt(art);
requestAnimationFrame(frame);

Object.assign(window, {
  __ui: {
    themes: THEMES.map((t) => t.id),
    screens: SCREENS.map((x) => x.id),
    theme: (id: string, background?: (typeof ARTS)[number]) => {
      const t = THEMES.find((x) => x.id === id);
      if (!t) return;
      setTheme(t);
      if (background && background !== art) setArt(background);
    },
    screen: (id: string) => {
      screen = toScreen(id);
      render();
    },
    /** The base stage and its sets, for inspecting the 3D scene. */
    get base() {
      return base;
    },
    /** Base screens: settle the 3D set and draw it now (for captures). */
    still(ticks = 60) {
      drawBaseNow(ticks);
    },
    view,
    async freeze(at = 1.5) {
      view.restart();
      view.paused = false;
      for (let t = 0; t < at; t += 1 / 30) await view.step(1, 1 / 30);
      view.paused = true;
      view.draw();
      anchor(flagSharp, [4.5, 2.05, 6.5]);
      anchor(flagAlien, [13.5, 2.1, 5.5]);
      anchor(floatEl, [13.3, 2.6, 6.4]);
    },
  },
});
