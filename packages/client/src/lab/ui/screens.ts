import { ICONS } from './icons';

// Mock-ups of the campaign screens, with the same contents as the real ones
// (strategy/screen.ts, ui/lobby.ts) and sample data. Every screen is built from
// the same few pieces (panels, list rows, detail, bar, buttons…) so each theme
// restyles all of them at once.

export type ScreenId =
  | 'combate'
  | 'menu'
  | 'bastion'
  | 'geoesfera'
  | 'investigacion'
  | 'ingenieria'
  | 'cuartel'
  | 'ascenso'
  | 'instalaciones'
  | 'hangar'
  | 'informe'
  | 'acontecimiento';

export interface ScreenDef {
  id: ScreenId;
  name: string;
  /** Top bar and bottom tabs: both, only the top bar, or none. */
  chrome: 'full' | 'top' | 'none';
  /** Bottom tab lit on this screen. */
  tab?: string;
}

export const SCREENS: ScreenDef[] = [
  { id: 'combate', name: 'Combate', chrome: 'none' },
  { id: 'menu', name: 'Menú', chrome: 'none' },
  { id: 'bastion', name: 'Bastión', chrome: 'full', tab: 'Bastión' },
  { id: 'geoesfera', name: 'Geoesfera', chrome: 'full', tab: 'Geoesfera' },
  { id: 'investigacion', name: 'Investigación', chrome: 'full', tab: 'Investigación' },
  { id: 'ingenieria', name: 'Ingeniería', chrome: 'full', tab: 'Ingeniería' },
  { id: 'cuartel', name: 'Cuartel', chrome: 'full', tab: 'Cuartel' },
  { id: 'ascenso', name: 'Ascenso', chrome: 'full', tab: 'Cuartel' },
  { id: 'instalaciones', name: 'Instalaciones', chrome: 'full', tab: 'Instalaciones' },
  { id: 'hangar', name: 'Hangar', chrome: 'top' },
  { id: 'informe', name: 'Informe', chrome: 'top' },
  { id: 'acontecimiento', name: 'Acontecimiento', chrome: 'full', tab: 'Bastión' },
];

interface RowOpts {
  sel?: boolean;
  dim?: boolean;
  glyph?: string;
  side?: string;
  sideCls?: string;
  owner?: 'p1' | 'p2';
}

const row = (title: string, sub: string, o: RowOpts = {}) =>
  `<li class="${[o.sel && 'sel', o.dim && 'dim', o.owner].filter(Boolean).join(' ')}">${o.glyph ? `<span class="g">${o.glyph}</span>` : ''}<div><b>${title}</b>${sub ? `<small>${sub}</small>` : ''}</div>${o.side !== undefined ? `<em class="${o.sideCls ?? ''}">${o.side}</em>` : ''}</li>`;

const bar = (pct: number) => `<div class="s-bar" style="--v: ${pct}%"><i></i></div>`;
const pips = (n: number, of: number, cls: string) => Array.from({ length: of }, (_, i) => `<i class="${cls}${i < n ? ' on' : ''}"></i>`).join('');

/** Top bar and bottom tabs, shared by the base screens. */
export const CHROME = `
  <div class="s-top">
    <div class="s-brand"><b>BASTIÓN</b><span>Día 12</span></div>
    <div class="s-res">
      <span>${ICONS.supplies}<small>Suministros</small><b>150</b></span>
      <span>${ICONS.intel}<small>Inteligencia</small><b>40</b></span>
      <span>${ICONS.alloys}<small>Aleaciones</small><b>10</b></span>
    </div>
    <div class="s-doom"><small>Proyecto Ascensión</small><span>${pips(4, 12, 'doom')}</span></div>
    <div class="s-players"><span class="pl p1"><i></i>Takeo · tú</span><span class="pl p2"><i></i>Compañero · Cuartel</span></div>
    <button class="s-menu">Menú</button>
  </div>
  <nav class="s-tabs">
    ${['Bastión', 'Geoesfera', 'Investigación', 'Ingeniería', 'Cuartel', 'Instalaciones'].map((t) => `<button data-tab="${t}">${t}${t === 'Cuartel' ? '<i class="dot" title="Compañero está aquí"></i>' : ''}</button>`).join('')}
  </nav>`;

const proposal = (what: string) => `
  <div class="s-proposal"><span><b class="who p2">Compañero</b> propone ${what}.</span><button class="s-cta">Aceptar</button><button class="s-btn danger">Rechazar</button></div>`;

const MENU = `
  <div class="m-shade"></div>
  <div class="m-column">
    <div class="m-title"><h1>BASTIÓN</h1><p>Táctica por turnos cooperativa</p></div>
    <button class="m-item primary on">Continuar campaña<small>Día 12 · Proyecto 4/12</small></button>
    <button class="m-item">Nueva campaña<small>La base compartida, de principio a fin</small></button>
    <button class="m-item">Escaramuza<small>Una misión suelta, sin consecuencias</small></button>
  </div>
  <section class="s-panel m-squad">
    <h3>Comandantes</h3>
    <ul class="s-list">
      ${row('Takeo', 'Tú · anfitrión', { side: 'Listo', sideCls: 'ok', owner: 'p1' })}
      ${row('Compañero', 'Conectado desde su casa', { side: 'Listo', sideCls: 'ok', owner: 'p2' })}
    </ul>
  </section>`;

const BASTION = `
  ${proposal('investigar <b>Armas magnéticas</b>')}
  <section class="s-panel s-ticker">
    <h3>Estado</h3>
    <ul class="s-list">
      ${row('Autopsia de MEC', 'Investigación · 2/4 días')}
      ${row('Enfermería', 'Obras · faltan 3 días')}
      ${row('2 operaciones', 'Suministros del Consejo en 18 días')}
    </ul>
    <p class="s-muted">Pulsa una sala para entrar.</p>
  </section>`;

const GEOESFERA = `
  <section class="s-panel s-left">
    <h3>Operaciones</h3>
    <ul class="s-list">
      ${row('Operación Vigilia Callada', 'Eliminación · Europa Occidental', { sel: true, glyph: '✕', side: '6 d' })}
      ${row('Operación Sombra Callada', 'Sabotaje · Europa Occidental', { glyph: 'ϟ', side: '2 d', sideCls: 'late' })}
      ${row('Rescate en Lisboa', 'Rescate VIP · Europa Occidental', { glyph: '✚', side: '9 d' })}
    </ul>
    <h3>Amenazas alienígenas</h3>
    <ul class="s-list">${row('Oriente Medio', 'Instalación · sin contacto', { glyph: '▲' })}</ul>
    <h3>Red de la resistencia</h3>
    <p>2 de 3 regiones contactadas. Pulsa una región del globo para ver sus datos.</p>
    <aside class="s-note">¿Vamos a por el sabotaje primero? — C.</aside>
  </section>
  <section class="s-panel s-right s-detail">
    <span class="kicker">Operación</span>
    <h2>Operación Vigilia Callada</h2>
    <p class="where">Europa Occidental · Ciudad</p>
    <p>Un grupo alienígena opera en la zona. Eliminad a todos los hostiles.</p>
    <dl>
      <dt>Tipo</dt><dd>Eliminación</dd>
      <dt>Dificultad</dt><dd>★★☆ Moderada</dd>
      <dt>Caduca</dt><dd>En 6 días</dd>
      <dt>Recompensa</dt><dd>60 suministros · 9 inteligencia · 6 aleaciones</dd>
    </dl>
    <button class="s-cta">Proponer desplegar</button>
    <span class="s-stamp">Clasificado</span>
  </section>
  <div class="s-scan"><small>Día 12</small><button>${ICONS.scan}<b>Escanear</b><span>Avanza el tiempo hasta que pase algo</span></button></div>`;

const INVESTIGACION = `
  <section class="s-panel s-left">
    <h3>Laboratorio</h3>
    <p class="s-muted">Un proyecto a la vez · ritmo ×1,5</p>
    <span class="s-group">En curso</span>
    <ul class="s-list">${row('Autopsia de MEC', '', { side: '2 d' })}</ul>
    <span class="s-group">Disponibles</span>
    <ul class="s-list">
      ${row('Armas magnéticas', '', { sel: true, side: '6 d' })}
      ${row('Autopsia de lancero', '', { side: '2 d' })}
      ${row('Cifrado alienígena', '', { side: '5 d' })}
    </ul>
    <span class="s-group">Bloqueadas</span>
    <ul class="s-list">
      ${row('Armadura de placas', '', { dim: true, side: '6 d' })}
      ${row('Granadas de plasma', '', { dim: true, side: '4 d' })}
      ${row('Origen de la señal', '', { dim: true, side: '8 d' })}
    </ul>
    <span class="s-group">Completadas</span>
    <ul class="s-list">${row('Autopsia de xenoide', '', { dim: true, side: '✓', sideCls: 'ok' })}</ul>
  </section>
  <section class="s-panel s-right s-detail">
    <span class="kicker">Proyecto</span>
    <h2>Armas magnéticas</h2>
    <p>Permite fabricar armas magnéticas (+2 de daño).</p>
    ${bar(0)}
    <p class="s-muted">0 de 8 días de trabajo</p>
    <span class="s-group">Desbloquea</span>
    <p>· Mejora: Armas magnéticas</p>
    <p class="s-muted">Coste: 20 inteligencia</p>
    <button class="s-cta">Proponer investigar</button>
    <aside class="s-note">Esta primero, que los MEC aguantan mucho — C.</aside>
  </section>`;

const INGENIERIA = `
  <section class="s-panel s-left">
    <h3>Ingeniería</h3>
    <div class="s-seg"><button>Mejoras</button><button class="on">Equipo</button></div>
    <ul class="s-list">
      ${row('Granada extra', 'Utilidad', { side: '2/2' })}
      ${row('Granada de humo', 'Utilidad', { sel: true, side: '0/0' })}
      ${row('Botiquín', 'Utilidad', { side: '1/1' })}
      ${row('Granada aturdidora', 'Utilidad', { dim: true, side: '0/0' })}
      ${row('Escudo mental', 'Utilidad', { dim: true, side: '0/0' })}
      ${row('Munición trazadora', 'Munición', { side: '1/1' })}
      ${row('Munición perforante', 'Munición', { dim: true, side: '0/0' })}
    </ul>
    <p class="s-muted">El taller abarata un 25 % los suministros.</p>
  </section>
  <section class="s-panel s-right s-detail">
    <span class="kicker">Equipo · Utilidad</span>
    <h2>Granada de humo</h2>
    <p>Humo durante 2 turnos: −20 a la puntería contra quien esté dentro.</p>
    <p class="s-muted">En el almacén: 0 libres de 0. Se equipa en el Cuartel; si el soldado cae, se pierde.</p>
    <p class="s-muted">Coste: 19 suministros · no necesita votación</p>
    <button class="s-cta">Fabricar</button>
  </section>`;

const CUARTEL = `
  <section class="s-panel s-left">
    <h3>Tus soldados</h3>
    <ul class="s-list">
      ${row('FRT · Marta Soler', 'Sargento · ¡ascenso!', { sel: true, side: 'Listo', sideCls: 'ok', owner: 'p1' })}
      ${row('GRN · Clara Bosch', 'Cabo', { side: 'Herida 4 d', sideCls: 'warn', owner: 'p1' })}
      ${row('ASL · Marcos Vela', 'Soldado', { side: 'Listo', sideCls: 'ok', owner: 'p1' })}
      ${row('ESP · Nora Campos', 'Novato', { side: 'Listo', sideCls: 'ok', owner: 'p1' })}
    </ul>
    <span class="s-group">Reclutar · 30 suministros (4/6)</span>
    <div class="s-seg"><button>ASL</button><button>GRN</button><button>FRT</button><button>ESP</button></div>
    <h3>Soldados de Compañero</h3>
    <ul class="s-list">
      ${row('ASL · Irene Galán', 'Cabo', { side: 'Listo', sideCls: 'ok', owner: 'p2' })}
      ${row('FRT · Sara Mena', 'Soldado', { side: 'Listo', sideCls: 'ok', owner: 'p2' })}
      ${row('ESP · Julia Font', 'Cabo', { dim: true, side: 'Caída', sideCls: 'late', owner: 'p2' })}
    </ul>
  </section>
  <section class="s-panel s-right s-detail">
    <span class="kicker">Francotiradora · Tuya</span>
    <h2>Marta Soler</h2>
    <p class="where">Sargento · Rifle de precisión</p>
    ${bar(36)}
    <p class="s-muted">11/14 XP para Teniente</p>
    <div class="s-stats">
      <div><small>Salud</small><b>5</b></div>
      <div><small>Puntería</small><b>80</b></div>
      <div><small>Voluntad</small><b>55</b></div>
      <div><small>Misiones</small><b>5</b></div>
      <div><small>Bajas</small><b>7</b></div>
      <div><small>Estado</small><b class="ok">Lista</b></div>
    </div>
    <span class="s-group">Equipo</span>
    <button class="s-select"><span>Utilidad</span><b>Granada de humo</b></button>
    <button class="s-select"><span>Munición</span><b>Munición trazadora</b></button>
    <span class="s-group">Habilidades</span>
    <p>Precisión letal · Vigía lejano</p>
    <button class="s-cta pulse">¡Ascenso! Elegir habilidad</button>
    <p class="s-muted">Cada rango da +5 de puntería.</p>
    <aside class="s-note">Dale Ojo letal, que la necesito en el tejado — C.</aside>
  </section>`;

const PERKS: [string, [string, string, string][], 'chosen' | 'pick' | 'locked'][] = [
  ['Soldado', [['Precisión letal', '+10 de crítico.', 'chosen'], ['Pistolero', '+15 de puntería con la pistola.', '']], 'chosen'],
  ['Cabo', [['Vigía lejano', 'La vigilancia dispara a todo lo que vea la escuadra.', 'chosen'], ['Desenfunde', 'Disparar la pistola ya no termina el turno.', '']], 'chosen'],
  ['Sargento', [['Ojo letal ◆', 'Disparo con −25 de puntería y +50 % de daño (enfriamiento 3).', ''], ['Manos rápidas ◆', 'Acción gratuita: un disparo de pistola (enfriamiento 4).', '']], 'pick'],
  ['Teniente', [['Muerte desde arriba', 'Matar desde más altura devuelve una acción (una vez por turno).', ''], ['Ojo de halcón', '+10 de puntería.', '']], 'locked'],
  ['Capitán', [['Cara a cara ◆', 'Un disparo de pistola a cada enemigo a la vista (enfriamiento 4).', ''], ['Zona letal ◆', 'Vigilancia que dispara a cada enemigo que se mueva (enfriamiento 4).', '']], 'locked'],
];

const ASCENSO = `
  <div class="s-backdrop"></div>
  <section class="s-panel s-modal wide">
    <span class="kicker">Francotiradora · Sargento</span>
    <h2>Marta Soler</h2>
    <p class="where">Elige una habilidad para cada ascenso pendiente.</p>
    <div class="s-perks">
      ${PERKS.map(
        ([rank, cells, state]) =>
          `<span class="rank ${state}">${rank}</span>${cells
            .map(([name, text, c]) => `<button class="perk ${c || (state === 'pick' ? 'pickable' : state === 'locked' ? 'locked' : 'skipped')}"><b>${name}</b><small>${text}</small></button>`)
            .join('')}`,
      ).join('')}
    </div>
    <p class="s-muted">Las habilidades con ◆ son activas (enfriamiento o usos limitados).</p>
    <div class="s-actions"><button class="s-btn">Cerrar</button></div>
  </section>`;

const INSTALACIONES = `
  <section class="s-panel s-left">
    <h3>Instalaciones</h3>
    <p>Pulsa una sala del Bastión para excavarla o construir en ella. Las obras se hacen de una en una.</p>
    <span class="s-group">En obras</span>
    <p>Enfermería · faltan 3 días</p>
    ${bar(50)}
    <span class="s-group">Efectos activos</span>
    <p>· Investigación ×1,5</p>
    <p>· Fabricación −25 % de suministros</p>
    <p>· Regiones contactables: 3</p>
  </section>
  <section class="s-panel s-right s-detail">
    <span class="kicker">Sala 2-1</span>
    <h2>Sala vacía</h2>
    <p class="s-warn">Hay otra obra en marcha: espera a que termine.</p>
    <ul class="s-list build">
      <li><div><b>Laboratorio</b><small>La investigación avanza un 50 % más rápido.</small><small class="cost">90 suministros · 8 días</small></div><button class="s-btn" disabled>Proponer construir</button></li>
      <li><div><b>Centro de entrenamiento</b><small>Cada jugador puede llevar un soldado más.</small><small class="cost">110 suministros · 10 aleaciones · 10 días</small></div><button class="s-btn" disabled>Proponer construir</button></li>
      <li class="dim"><div><b>Taller</b><small>Fabricar cuesta un 25 % menos de suministros.</small><small class="why">Ya tenéis uno</small></div></li>
      <li class="dim"><div><b>Centro de comunicaciones</b><small>+2 regiones contactables.</small><small class="why">Requiere investigar Cifrado alienígena</small></div></li>
    </ul>
  </section>`;

/** Lineup cards: filled in by uilab.ts under each pad of the hangar. */
const HANGAR = `
  <section class="s-panel s-ctop s-detail">
    <span class="kicker">Selección de escuadra</span>
    <h1>Operación Vigilia Callada</h1>
    <p class="where">Eliminación · Europa Occidental · Dificultad Moderada</p>
    <p>Un grupo alienígena opera en la zona. Eliminad a todos los hostiles.</p>
  </section>
  <section class="s-panel s-left compact">
    <h3>Tus soldados · 2/3</h3>
    <ul class="s-list">
      ${row('FRT · Marta Soler', 'Sargento · Granada de humo', { sel: true, side: 'En escuadra', owner: 'p1' })}
      ${row('GRN · Clara Bosch', 'Cabo · Botiquín', { sel: true, side: 'En escuadra', owner: 'p1' })}
      ${row('ASL · Marcos Vela', 'Soldado', { owner: 'p1' })}
      ${row('ESP · Nora Campos', 'Novato', { dim: true, side: '3 d', sideCls: 'warn', owner: 'p1' })}
    </ul>
    <p class="s-muted">Pulsa un soldado para meterlo o sacarlo. El equipo se cambia en el Cuartel.</p>
  </section>
  <div class="s-cards" data-cards="hangar">
    <div class="s-card p1"><b>Marta Soler</b><small>Francotiradora · Sargento</small><small>Tuya</small><button class="s-btn">Quitar</button></div>
    <div class="s-card p1"><b>Clara Bosch</b><small>Granadera · Cabo</small><small>Tuya</small><button class="s-btn">Quitar</button></div>
    <div class="s-card p2"><b>Irene Galán</b><small>Asalto · Cabo</small><small>De Compañero</small></div>
    <div class="s-card p2"><b>Julia Font</b><small>Especialista · Cabo</small><small>De Compañero</small></div>
    <div class="s-card empty"><b>Libre</b><small>Elige a la izquierda</small></div>
    <div class="s-card empty"><b>Libre</b><small>—</small></div>
  </div>
  <div class="s-launch">
    <button class="s-cta big">Lanzar misión</button>
    <small><i class="who p2">Compañero</i> espera para despegar · 4/6 soldados</small>
    <div class="s-actions"><button class="s-btn">Volver al Bastión</button><button class="s-btn danger">Cancelar misión</button></div>
  </div>`;

const INFORME = `
  <section class="s-panel s-ctop s-detail victory">
    <span class="kicker good">Informe de misión</span>
    <h1>Misión cumplida</h1>
    <p class="where">Operación Vigilia Callada</p>
    <p>Recompensa: 60 suministros · 9 inteligencia · 6 aleaciones</p>
    <p class="s-good">Cuerpos para la autopsia: Lancero</p>
  </section>
  <div class="s-cards" data-cards="informe">
    <div class="s-card p1 ok"><b>Marta Soler</b><small class="ok">Ilesa</small><small>2 bajas · +3 XP</small><small class="promo">▲ Sargento</small></div>
    <div class="s-card p1 wounded"><b>Clara Bosch</b><small class="warn">Herida · 4 días</small><small>1 baja · +2 XP</small></div>
    <div class="s-card p2 ok"><b>Irene Galán</b><small class="ok">Ilesa</small><small>3 bajas · +3 XP</small></div>
    <div class="s-card p2 dead"><b>Julia Font</b><small class="late">Caída en combate</small><small>0 bajas</small></div>
  </div>
  <div class="s-launch"><button class="s-cta big">Continuar</button></div>`;

// The vote bar stays above the backdrop: the modal points to it.
const ACONTECIMIENTO = `
  <div class="s-backdrop"></div>
  <section class="s-panel s-modal">
    <span class="kicker">Acontecimiento</span>
    <h2>Contrabandistas</h2>
    <p>Unos contrabandistas ofrecen aleaciones robadas a un convoy alienígena.</p>
    <div class="s-options">
      <button class="voted"><b>Comprarlas</b><small>−40 suministros, +15 aleaciones</small><i class="who p2">Compañero</i></button>
      <button><b>Rechazar la oferta</b><small>Sin efecto</small></button>
    </div>
    <p class="s-warn">Compañero propone comprarlas: acepta o rechaza arriba.</p>
    <p class="s-muted">El tiempo no avanza hasta que decidáis.</p>
  </section>
  ${proposal('<b>comprarlas</b>')}`;

export const SCREEN_HTML: Partial<Record<ScreenId, string>> = {
  menu: MENU,
  bastion: BASTION,
  geoesfera: GEOESFERA,
  investigacion: INVESTIGACION,
  ingenieria: INGENIERIA,
  cuartel: CUARTEL,
  ascenso: `${CUARTEL}${ASCENSO}`,
  instalaciones: INSTALACIONES,
  hangar: HANGAR,
  informe: INFORME,
  acontecimiento: `${BASTION.replace(proposal('investigar <b>Armas magnéticas</b>'), '')}${ACONTECIMIENTO}`,
};
