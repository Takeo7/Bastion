import '../../styles.css';
import '../../strategy/strategy.css';
import { ambience, type Bed } from '../../audio/ambience';
import { buffers, render, sound } from '../../audio/bank';
import { audio } from '../../audio/engine';
import { music, type Piece } from '../../audio/music';
import { morse } from '../../audio/radio';
import { SOUNDS } from '../../audio/sounds';
import { installUiSounds } from '../../audio/ui';
import { h } from '../../ui/dom';
import { soundSettings } from '../../ui/settingsPanel';

// Sound lab (/sonido.html): every sound of the game on a button, grouped by
// family, with its spectrogram (time left to right, pitch bottom to top), the
// music pieces, the ambience beds, a short combat scene to judge the mix, and
// the volumes. For listening and for saying what to change.

const LABELS: Record<string, string> = {
  'shot.rifle': 'Fusil (especialista)',
  'shot.shotgun': 'Escopeta (asalto)',
  'shot.sniper': 'Fusil de precisión (francotirador)',
  'shot.cannon': 'Ametralladora (granadero)',
  'shot.pistol': 'Pistola',
  'shot.advent': 'Fusil de ADVENT',
  'shot.mec': 'Cañón del Acorazado',
  'shot.sectoid': 'Pistola del Saqueador',
  'shot.xenoid': 'Grito del Chillón',
  'impact.flesh': 'Impacto en el cuerpo',
  'impact.armor': 'Impacto en el blindaje',
  'impact.robot': 'Impacto en una máquina',
  'impact.miss': 'Fallo (silbido o rebote)',
  'impact.cover': 'Fallo contra la cobertura',
  'impact.crit': 'Crítico (encima del impacto)',
  'impact.glass': 'Ventana rota',
  explosion: 'Explosión',
  'explosion.flash': 'Granada cegadora',
  smoke: 'Humo',
  missile: 'Misil',
  'grenade.throw': 'Lanzar granada',
  'grenade.land': 'Granada al caer',
  launch: 'Lanzagranadas',
  step: 'Pasos',
  'step.mec': 'Pasos del Acorazado',
  'step.drag': 'Pasos del zombi',
  'zombie.moan': 'Gemido a lo lejos',
  'truck.engine': 'Camioneta llegando',
  search: 'Registrar',
  reload: 'Recargar',
  overwatch: 'Vigilancia',
  hunker: 'Agazaparse',
  'melee.swing': 'Golpe al aire',
  'melee.sword': 'Machete',
  'melee.stun': 'Descarga eléctrica',
  'melee.claw': 'Garras',
  door: 'Puerta',
  drone: 'Dron',
  heal: 'Curación',
  shield: 'Escudo',
  hack: 'Hackeo',
  'hack.ok': 'Equipo arreglado',
  alarm: 'Alarma',
  'siren.police': 'Alarma del sabotaje: coche de policía (en bucle, una vuelta)',
  'siren.ambulance': 'Alarma del sabotaje: ambulancia (en bucle, una vuelta)',
  'siren.alarm': 'Alarma del sabotaje: sirena de motor de la fábrica (en bucle, una vuelta)',
  psi: 'Grito del Chillón (también al levantar a un muerto)',
  beam: 'Evacuación (haz)',
  pods: 'Cápsulas de impresión',
  flare: 'Bengala y dirigible',
  data: 'Datos recuperados',
  found: 'Hallazgo al registrar',
  rescue: 'VIP rescatado',
  'death.human': 'Muerte (soldado)',
  'death.advent': 'Muerte (ADVENT)',
  'death.robot': 'Muerte (máquina)',
  'death.sectoid': 'Muerte (Chillón)',
  'death.runner': 'Muerte (Corredor)',
  'death.zombie': 'Muerte (zombi)',
  contact: '¡Contacto!',
  reveal: 'Ocultación rota',
  'turn.xcom': 'Turno de la escuadra',
  'turn.alien': 'Turno enemigo',
  command: 'Pasar el mando / marca',
  victory: 'Misión cumplida',
  defeat: 'Misión fracasada',
  'ui.key': 'Tecla (todos los botones)',
  'ui.space': 'Barra espaciadora',
  'ui.bell': 'Campanilla (Intro)',
  'ui.return': 'Retorno de carro (confirmar)',
  'ui.paper': 'Papel (abrir un panel)',
  'ui.page': 'Página (cambiar de pestaña)',
  'ui.stamp': 'Sello (rótulos, resultado)',
  'ui.pen': 'Pluma',
  'ui.error': 'No se puede',
  'ui.tick': 'Reloj (pasan los días)',
  'radio.static': 'Estática (abrir canal)',
  'radio.key': 'Tecla de radio',
  'radio.close': 'Cerrar canal',
  'radio.bells': 'Campanas (la frecuencia de la Congregación)',
  'geo.ping': 'Radar del valle',
  'song.voice': 'Saltar, Encaramarse, Lluvia de flechas (el equipo)',
  'song.bolt': 'Bengala',
  'song.fire': 'Cóctel molotov',
  'song.arrow': 'Ballesta (Flecha perforante, cada flecha de la Lluvia)',
  'song.vanish': 'Saltar, Encaramarse: el impulso',
  'song.appear': 'Saltar, Encaramarse: la caída',
  'song.dome': 'Petardos',
  'song.storm': 'El bidón: estalla',
  'song.thunder': 'El bidón: el fuego en cada enemigo',
  'song.aura': 'Escudo antidisturbios, Arenga, ¡Aguantad!',
  'song.heal': 'Vendaje rápido',
  'song.revive': 'Desfibrilador',
  'song.cleanse': 'Sedante',
  'song.mark': 'Rastreo',
  'song.blade': 'Machetazo (aún sin usar)',
  'holo.click': 'Holotáctico: botón',
  'holo.confirm': 'Holotáctico: confirmar',
  'holo.page': 'Holotáctico: pestaña',
  'holo.open': 'Holotáctico: abrir panel',
  'holo.error': 'Holotáctico: no se puede',
  'holo.alert': 'Holotáctico: rótulo',
  build: 'Obras',
};

const FAMILIES: [string, (n: string) => boolean][] = [
  ['Armas', (n) => n.startsWith('shot.')],
  ['Impactos', (n) => n.startsWith('impact.')],
  ['Explosivos', (n) => ['explosion', 'explosion.flash', 'smoke', 'missile', 'grenade.throw', 'grenade.land', 'launch'].includes(n)],
  ['Pasos y acciones', (n) => /^(step|reload|overwatch|hunker|melee|door|drone|heal|shield|hack|alarm|psi|beam|pods|flare|data|rescue)/.test(n)],
  ['Muertes', (n) => n.startsWith('death.')],
  ['Momentos del combate', (n) => ['contact', 'reveal', 'turn.xcom', 'turn.alien', 'command', 'victory', 'defeat'].includes(n)],
  ['El Oficio', (n) => n.startsWith('song.')],
  ['Interfaz (el Diario)', (n) => n.startsWith('ui.')],
  ['Interfaz (Holotáctico)', (n) => n.startsWith('holo.')],
  ['Radio', (n) => n.startsWith('radio.')],
  ['Estrategia', (n) => ['geo.ping', 'build'].includes(n)],
];

function label(name: string): string {
  const tier = /^shot\.\w+\.(\d)$/.exec(name);
  if (tier) return `${LABELS[name.replace(/\.\d$/, '')]} · nivel ${tier[1]}`;
  return LABELS[name] ?? name;
}

// ---------------------------------------------------------------- spectrogram

function fft(re: Float32Array, im: Float32Array): void {
  const n = re.length;
  for (let i = 1, j = 0; i < n; i++) {
    let bit = n >> 1;
    for (; j & bit; bit >>= 1) j ^= bit;
    j ^= bit;
    if (i < j) {
      [re[i], re[j]] = [re[j]!, re[i]!];
      [im[i], im[j]] = [im[j]!, im[i]!];
    }
  }
  for (let len = 2; len <= n; len <<= 1) {
    const ang = (-2 * Math.PI) / len;
    for (let i = 0; i < n; i += len) {
      for (let k = 0; k < len / 2; k++) {
        const wr = Math.cos(ang * k);
        const wi = Math.sin(ang * k);
        const ur = re[i + k]!;
        const ui = im[i + k]!;
        const vr = re[i + k + len / 2]! * wr - im[i + k + len / 2]! * wi;
        const vi = re[i + k + len / 2]! * wi + im[i + k + len / 2]! * wr;
        re[i + k] = ur + vr;
        im[i + k] = ui + vi;
        re[i + k + len / 2] = ur - vr;
        im[i + k + len / 2] = ui - vi;
      }
    }
  }
}

/** Time left to right, frequency bottom to top on a log scale (50 Hz–16 kHz), loudness as ink. */
function spectrogram(buf: AudioBuffer, canvas: HTMLCanvasElement): void {
  const d = buf.getChannelData(0);
  const N = 512;
  const W = canvas.width;
  const H = canvas.height;
  const ctx = canvas.getContext('2d')!;
  const img = ctx.createImageData(W, H);
  const re = new Float32Array(N);
  const im = new Float32Array(N);
  for (let x = 0; x < W; x++) {
    const start = Math.floor((x / W) * Math.max(1, d.length - N));
    for (let i = 0; i < N; i++) {
      re[i] = (d[start + i] ?? 0) * (0.5 - 0.5 * Math.cos((2 * Math.PI * i) / N));
      im[i] = 0;
    }
    fft(re, im);
    for (let y = 0; y < H; y++) {
      const f = 50 * Math.pow(16000 / 50, 1 - y / H);
      const bin = Math.min(N / 2 - 1, Math.round((f / buf.sampleRate) * N));
      const mag = Math.hypot(re[bin]!, im[bin]!);
      const db = 20 * Math.log10(mag + 1e-6);
      const v = Math.max(0, Math.min(1, (db + 50) / 50));
      const o = (y * W + x) * 4;
      img.data[o] = 40 + (1 - v) * 200;
      img.data[o + 1] = 34 + (1 - v) * 196;
      img.data[o + 2] = 24 + (1 - v) * 174;
      img.data[o + 3] = 255;
    }
  }
  ctx.putImageData(img, 0, 0);
}

// ---------------------------------------------------------------- page

const root = document.getElementById('sound-lab')!;
const css = document.createElement('style');
css.textContent = `
  html, body { height: auto; overflow: auto; }
  #sound-lab { max-width: 1180px; margin: 0 auto; padding: 20px 16px 60px; font-family: var(--font); color: var(--ink); }
  .lab-head { display: flex; justify-content: space-between; align-items: end; gap: 16px; flex-wrap: wrap; margin-bottom: 14px; }
  .lab-head { color: var(--paper-light); }
  .lab-head h1 { margin: 0; font-family: var(--font-display); font-weight: 400; letter-spacing: 0.08em; }
  .lab-head p { margin: 4px 0 0; max-width: 62ch; }
  .lab-cols { display: grid; grid-template-columns: minmax(0, 1fr) 320px; gap: 16px; align-items: start; }
  @media (max-width: 900px) { .lab-cols { grid-template-columns: 1fr; } }
  .lab-panel { background: var(--paper-grain), var(--paper); box-shadow: 1px 2px 6px rgba(0,0,0,.35); padding: 10px 12px 12px; margin-bottom: 14px; }
  .lab-panel h2 { margin: 0 0 8px; font: 400 15px var(--font-display); letter-spacing: 0.12em; text-transform: uppercase; }
  .sound-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(176px, 1fr)); gap: 8px; }
  .sound-btn { display: grid; gap: 3px; padding: 5px; text-align: left; background: var(--paper-light); border: 1px solid var(--ink-soft); border-radius: 2px; font: 12px var(--font); color: var(--ink); }
  .sound-btn:hover { background: #fff; }
  .sound-btn canvas { width: 100%; height: 40px; display: block; }
  .sound-btn small { color: var(--ink-soft); }
  .row { display: flex; flex-wrap: wrap; gap: 6px; }
  .row .x-btn.on { background: var(--ink); color: var(--paper-light); }
`;
document.head.append(css);

installUiSounds();

const head = h(
  'div.lab-head',
  {},
  h(
    'div',
    {},
    h('h1', {}, 'Bastión · Sonido'),
    h(
      'p',
      {},
      'Todo el sonido del juego está hecho en código, sin grabaciones. Pulsa cada botón para oírlo; cada vez suena una variante distinta. Debajo de cada nombre, su espectrograma: el tiempo de izquierda a derecha, los agudos arriba.',
    ),
  ),
);

const grids = FAMILIES.map(([title, test]) => {
  const names = Object.keys(SOUNDS).filter(test);
  const grid = h('div.sound-grid');
  for (const name of names) {
    const canvas = h('canvas', { width: 200, height: 40 }) as HTMLCanvasElement;
    const btn = h('button.sound-btn', { onclick: () => sound(name), title: name }, h('span', {}, label(name)), canvas, h('small', {}, `${(SOUNDS[name]!.dur).toFixed(1)} s · ${SOUNDS[name]!.variants ?? 3} variantes`));
    grid.append(btn);
    void render(name).then(() => {
      const b = buffers(name);
      if (b?.[0]) spectrogram(b[0], canvas);
    });
  }
  return h('div.lab-panel', {}, h('h2', {}, title), grid);
});

const PIECES: [Piece | null, string][] = [
  ['base', 'Bastión y menús'],
  ['geo', 'Globo'],
  ['calm', 'Combate oculto'],
  ['tense', 'Combate tras el contacto'],
  ['alien', 'Turno enemigo'],
  [null, 'Silencio'],
];
const BEDS: [Bed | null, string][] = [
  ['city', 'Ciudad'],
  ['wilds', 'Afueras'],
  ['facility', 'Imprenta'],
  ['base', 'Bastión'],
  ['geo', 'Globo'],
  [null, 'Nada'],
];

function choices<T>(list: [T, string][], play: (v: T) => void): HTMLElement {
  const row = h('div.row');
  for (const [v, text] of list) {
    const b = h(
      'button.x-btn',
      {
        onclick: () => {
          play(v);
          for (const c of Array.from(row.children)) c.classList.remove('on');
          b.classList.add('on');
        },
      },
      text,
    );
    row.append(b);
  }
  return row;
}

/** A few seconds of a firefight, to judge the mix: steps, reload, contact, bursts, a grenade. */
function scene(): void {
  const at = (t: number, fn: () => void) => window.setTimeout(fn, t * 1000);
  music.play('calm', 0.5);
  ambience.play('city', 0.5);
  for (let i = 0; i < 6; i++) at(0.3 + i * 0.28, () => sound('step', { pan: -0.3 }));
  at(2.2, () => sound('reload', { pan: -0.3 }));
  at(3.4, () => {
    sound('contact');
    audio.duck(0.6, 0.8, 2);
    music.play('tense', 2);
  });
  for (let i = 0; i < 3; i++) at(5 + i * 0.11, () => sound('shot.rifle.1', { pan: -0.4 }));
  at(5.4, () => sound('impact.flesh', { pan: 0.4 }));
  for (let i = 0; i < 3; i++) at(6.6 + i * 0.12, () => sound('shot.advent', { pan: 0.5 }));
  at(7, () => sound('impact.miss', { pan: -0.3 }));
  at(8.2, () => sound('grenade.throw', { pan: -0.3 }));
  at(9, () => sound('grenade.land', { pan: 0.4 }));
  at(9.2, () => {
    sound('explosion', { pan: 0.4 });
    audio.duck(0.55, 0.6, 1.8);
  });
  at(10.2, () => sound('death.advent', { pan: 0.4 }));
  at(11.8, () => sound('ui.stamp'));
}

const side = h(
  'div',
  {},
  h('div.lab-panel', {}, h('h2', {}, 'Escena de prueba'), h('p', {}, 'Unos segundos de combate con música y ambiente, para juzgar la mezcla.'), h('button.x-btn.primary', { onclick: scene }, 'Reproducir')),
  h('div.lab-panel', {}, h('h2', {}, 'Música'), choices(PIECES, (p) => music.play(p, 1.5))),
  h('div.lab-panel', {}, h('h2', {}, 'Ambiente'), choices(BEDS, (b) => ambience.play(b, 1.5))),
  h('div.lab-panel', {}, h('h2', {}, 'Morse'), h('button.x-btn', { onclick: () => morse() }, 'Unas letras'), ' ', h('button.x-btn', { onclick: () => morse('SOS') }, 'SOS')),
);

const volumes = h('div.lab-panel', {}, h('h2', {}, 'Volumen'));
const redraw = () => {
  volumes.replaceChildren(h('h2', {}, 'Volumen'), soundSettings(redraw));
};
redraw();
side.append(volumes);

root.append(head, h('div.lab-cols', {}, h('div', {}, ...grids), side));

Object.assign(window, { __sound: { sound, music, ambience, render, buffers, SOUNDS } });
