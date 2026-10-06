import {
  displayName,
  weaponName,
  MISSION_NAMES,
  RANKS,
  TEMPLATES,
  WEAPONS,
  type AbilityId,
  type MissionKind,
  type Modifier,
  type Outcome,
  type PlayerInfo,
  type SkirmishMap,
  type Slot,
  type Team,
  type Unit,
} from '@bastion/engine';
import type { NetStatus } from '../net';
import { append, clear, h } from './dom';
import { abilityIcon, arrowIcon } from './icons';
import { graphicsSettings } from './settingsPanel';
import { typed } from './typed';

export interface AbilityView {
  id: AbilityId;
  label: string;
  hotkey: string;
  enabled: boolean;
  reason?: string;
  active?: boolean;
  count?: number;
  /** Turns left before it can be used again. */
  cooldown?: number;
  /** Costs no action. */
  free?: boolean;
  /** Nothing left to use (no ammo, no charges): stamped, not explained. */
  depleted?: boolean;
}

/** Who holds the command (el mando) while playing with a partner. */
export interface CommandView {
  mine: boolean;
  holder: string;
  color: string;
  /** The holder can hand it over (partner connected and still playing). */
  canPass: boolean;
}

export type TargetView =
  | {
      kind: 'attack';
      title: string;
      /** The confirm button: the ability alone ("Disparar"); the title also names the weapon. */
      action: string;
      targetName: string;
      hit: number;
      crit: number;
      graze: number;
      mods: Modifier[];
      critMods: Modifier[];
      index: number;
      total: number;
      /** What the ability changes besides the odds ("+50 % de daño"). */
      note?: string;
    }
  | { kind: 'support'; title: string; action: string; targetName: string; effect: string; index: number; total: number };

export interface MissionView {
  kind: MissionKind;
  objective: string;
  turnsLeft: number | null;
  /** Urgent notice under the objective (reinforcements on the way). */
  alert: string | null;
  /** Name of the area (map). */
  area: string;
}

const KINDS: MissionKind[] = ['elimination', 'recovery', 'sabotage', 'hack', 'rescue'];
const MAPS: SkirmishMap[] = ['city', 'wilds', 'facility'];
const randomKind = () => KINDS[Math.floor(Math.random() * KINDS.length)]!;
const randomMap = () => MAPS[Math.floor(Math.random() * MAPS.length)]!;

export interface HudHandlers {
  onAbility(id: AbilityId): void;
  onEndTurn(): void;
  onSelectUnit(id: string): void;
  onConfirmTarget(): void;
  onCancel(): void;
  onCycleTarget(dir: 1 | -1): void;
  /** Skirmish replay: same settings when omitted, on a fresh map. */
  onNewMission(kind?: MissionKind, map?: SkirmishMap): void;
  onReturnToBase(): void;
  /** Skirmish over: both players back to the main menu. */
  onBackToMenu(): void;
  onAbandon(): void;
  onPassCommand(): void;
  /** Toggles the sound; returns whether it is now on. */
  onToggleSound(): boolean;
  soundOn(): boolean;
}

const CONTROLS: [string, string][] = [
  ['Clic', 'Seleccionar y mover'],
  ['Tab', 'Siguiente soldado u objetivo'],
  ['Espacio', 'Disparar'],
  ['1–9', 'Habilidades'],
  ['C', 'Ceder el mando'],
  ['Retroceso', 'Terminar turno'],
  ['G · clic central', 'Marcar'],
  ['Q / E', 'Girar la cámara'],
  ['WASD', 'Mover la cámara'],
  ['F', 'Centrar en el soldado'],
  ['M', 'Sonido'],
  ['Esc', 'Cancelar o abrir este menú'],
];

/** Typewritten text is built once per string and cloned after (the HUD redraws often). */
const typedCache = new Map<string, DocumentFragment>();
function type(text: string): Node {
  let frag = typedCache.get(text);
  if (!frag) typedCache.set(text, (frag = typed(text)));
  return frag.cloneNode(true);
}

/** Log lines kept, and shown while folded. */
const LOG_KEEP = 30;
const LOG_FOLDED = 2;

/** DOM overlay for everything that is not in the 3D scene. */
export class Hud {
  readonly root = h('div.hud');
  private readonly turn = h('div.turn');
  private readonly mission = h('div.mission');
  private readonly players = h('div.players');
  private readonly squad = h('div.squad.panel');
  private readonly unitPanel = h('div.unit-panel.panel');
  private readonly abilities = h('div.abilities');
  private readonly target = h('div.shot-panel.panel');
  private readonly endTurn = h('button.end-turn');
  private readonly passBtn = h('button.pass-command', { title: 'Tu compañero da las órdenes hasta que te lo devuelva (C)' });
  private readonly logEl = h('div.log');
  private readonly logLines: HTMLElement[] = [];
  private logOpen = false;
  private readonly menu = h('div.game-menu');
  private readonly bannerEl = h('div.banner');
  private readonly toastEl = h('div.toast');
  private readonly outcome = h('div.outcome');
  private readonly modal = h('div.modal');
  private readonly connection = h('div.connection');
  private bannerTimer = 0;
  private toastTimer = 0;

  constructor(container: HTMLElement, private readonly handlers: HudHandlers) {
    this.endTurn.addEventListener('click', () => handlers.onEndTurn());
    this.passBtn.addEventListener('click', () => handlers.onPassCommand());
    this.passBtn.append(h('span', {}, 'CEDER EL MANDO'), h('small', {}, 'C'));
    const menuButton = h('button.menu-button', { onclick: () => this.toggleMenu(), title: 'Menú (Esc)' }, 'Menú');
    this.root.append(
      h('div.top', {}, this.turn, this.mission, this.players),
      this.squad,
      this.unitPanel,
      this.abilities,
      this.target,
      h('div.turn-actions', {}, this.passBtn, this.endTurn),
      h('div.side', {}, menuButton, this.logEl),
      this.bannerEl,
      this.toastEl,
      this.outcome,
      this.menu,
      this.modal,
      this.connection,
    );
    this.renderLog();
    container.append(this.root);
    this.setTarget(null);
  }

  private turnKey = '';
  private missionKey = '';

  setTurn(turn: number, team: Team, concealed: boolean): void {
    const key = `${turn}|${team}|${concealed}`;
    if (key === this.turnKey) return;
    this.turnKey = key;
    clear(this.turn);
    this.turn.append(
      h(`span.turn-label.${team}`, {}, type(team === 'xcom' ? `TURNO ${turn} · ESCUADRA` : 'TURNO ENEMIGO')),
      h(`span.conceal.${concealed ? 'on' : 'off'}`, {}, concealed ? 'OCULTOS' : 'DETECTADOS'),
    );
  }

  setMission(m: MissionView): void {
    const key = JSON.stringify(m);
    if (key === this.missionKey) return;
    this.missionKey = key;
    clear(this.mission);
    append(
      this.mission,
      h('span.mission-kind', { title: m.area }, type(MISSION_NAMES[m.kind].toUpperCase())),
      h('span.mission-goal', {}, type(m.objective)),
      m.turnsLeft !== null ? h(`span.timer${m.turnsLeft <= 3 ? '.urgent' : ''}`, {}, `${m.turnsLeft} ${m.turnsLeft === 1 ? 'TURNO' : 'TURNOS'}`) : null,
      m.alert ? h('span.timer.urgent.alert', {}, m.alert) : null,
    );
  }

  setPlayers(players: PlayerInfo[], ready: [boolean, boolean], mySlot: Slot, busy: Set<Slot>, commander: Slot | null = null): void {
    clear(this.players);
    // Alone, the turn state is the end-turn button's business: a status here read as "you are waiting".
    const alone = !players.some((p) => p.slot !== mySlot && p.connected);
    for (const p of players) {
      const holds = commander === p.slot;
      const status = !p.connected ? 'Desconectado' : alone ? '' : holds ? 'Mando' : ready[p.slot] ? 'Listo' : busy.has(p.slot) ? 'Con acciones' : 'Sin acciones';
      this.players.append(
        h(
          `div.player${p.slot === mySlot ? '.me' : ''}${ready[p.slot] ? '.ready' : ''}${p.connected ? '' : '.offline'}${holds ? '.command' : ''}`,
          { style: { '--accent': p.color } },
          h('span.dot'),
          h('span.name', {}, p.name),
          status ? h('span.status', {}, status) : null,
        ),
      );
    }
  }

  setSquad(units: Unit[], mySlot: Slot, selected: string | null, colors: Record<number, string>, carrier: string | null): void {
    clear(this.squad);
    const sorted = [...units].sort((a, b) => Number(b.owner === mySlot) - Number(a.owner === mySlot));
    for (const u of sorted) {
      const mine = (u.owner === mySlot || u.owner === null) && u.team === 'xcom';
      const state = u.evacuated ? '.evacuated' : u.alive ? '' : '.dead';
      const card = h(
        `div.squad-card${u.id === selected ? '.selected' : ''}${state}${mine ? '.mine' : ''}`,
        {
          style: { '--accent': u.owner === null ? '#e8edf2' : colors[u.owner] },
          onclick: () => mine && u.alive && this.handlers.onSelectUnit(u.id),
          title: mine ? 'Seleccionar' : 'Soldado de tu compañero',
        },
        h('div.card-name', {}, h('b.cls', {}, TEMPLATES[u.template].short), ' ', u.nickname ? `«${u.nickname}»` : u.name),
        h(
          'div.card-row',
          {},
          h('span.hp', {}, ...Array.from({ length: u.maxHp }, (_, i) => h(`i${i < u.hp ? '.on' : ''}`))),
          h('span.ap', {}, ...Array.from({ length: Math.max(2, u.ap) }, (_, i) => h(`i${i < u.ap ? '.on' : ''}`))),
          u.controlledBy ? h('em.psi', {}, 'CONTROLADO') : null,
          u.captive ? h('em.data', {}, 'CAUTIVO') : null,
          u.disoriented ? h('em.psi', {}, 'DESORIENTADO') : null,
          u.panicked ? h('em.psi', {}, 'PÁNICO') : null,
          u.evacuated ? h('em.evac', {}, 'EVACUADO') : null,
          carrier === u.id ? h('em.data', {}, 'DATOS') : null,
          u.overwatch ? h('em.ow', {}, u.killZone ? 'ZONA LETAL' : 'VIGILANCIA') : null,
          u.hunkered ? h('em.hk', {}, 'AGAZAPADO') : null,
          u.stunned ? h('em.stun', {}, 'ATURDIDO') : null,
        ),
      );
      this.squad.append(card);
    }
  }

  /** The soldier sheet: symbols, pips and numbers, readable at a glance (full words in the tooltips). */
  setSelected(u: Unit | null): void {
    clear(this.unitPanel);
    this.unitPanel.classList.toggle('hidden', !u);
    if (!u) return;
    const t = TEMPLATES[u.template];
    const w = WEAPONS[t.weapon];
    const weapon = weaponName(t.weapon, u.weaponTier ?? 1);
    const extras = [t.secondary && WEAPONS[t.secondary].name, t.meleeWeapon && WEAPONS[t.meleeWeapon].name].filter(Boolean).join(' · ');
    const rank = u.team === 'xcom' ? RANKS[u.rank]?.name ?? '' : '';
    const pips = (filled: number, total: number) => Array.from({ length: total }, (_, i) => h(`i${i < filled ? '.on' : ''}`));
    const stat = (kind: string, title: string, ...content: (Node | string)[]) => h(`span.stat.${kind}`, { title }, h('span.stat-icon'), ...content);
    append(
      this.unitPanel,
      h(
        'div.unit-head',
        { title: [rank, t.name].filter(Boolean).join(' · ') },
        u.team === 'xcom' ? h('span.rank', { title: rank }, ...Array.from({ length: u.rank }, () => h('i'))) : null,
        h('span.unit-name', {}, type(displayName(u.name, u.nickname))),
        h('span.unit-class', {}, t.short),
      ),
      h(
        'div.unit-stats',
        {},
        stat('hp', `Salud ${u.hp}/${u.maxHp}`, h('span.pips', {}, ...pips(u.hp, u.maxHp))),
        stat('ap', `Acciones ${u.ap}/2`, h('span.pips', {}, ...pips(u.ap, Math.max(2, u.ap)))),
        w.clip > 0 ? stat('ammo', `Munición ${u.ammo}/${w.clip}`, h('span.pips', {}, ...pips(u.ammo, w.clip))) : null,
        stat('aim', `Puntería ${t.aim + u.mods.aim}`, h('b', {}, String(t.aim + u.mods.aim))),
        u.charges.grenade !== undefined ? stat('grenade', `Granadas: ${u.charges.grenade}`, h('b', {}, `×${u.charges.grenade}`)) : null,
        u.charges.medkit !== undefined ? stat('medkit', `Botiquín: ${u.charges.medkit}`, h('b', {}, `×${u.charges.medkit}`)) : null,
        u.armor ? stat('armor', `Blindaje ${u.armor}`, h('b', {}, String(u.armor))) : null,
      ),
      h(
        'div.unit-weapon',
        { title: [weapon, extras].filter(Boolean).join(' · ') },
        h('span.stat-icon'),
        h(`span.tier.t${u.weaponTier ?? 1}`, {}, ...Array.from({ length: u.weaponTier ?? 1 }, () => h('i'))),
      ),
    );
  }

  setAbilities(list: AbilityView[]): void {
    clear(this.abilities);
    // No explanations on the cards: the reason lives in the tooltip, and an empty one gets a stamp.
    for (const a of list) {
      this.abilities.append(
        h(
          `button.ability${a.active ? '.active' : ''}${a.free ? '.free' : ''}${a.depleted ? '.depleted' : ''}`,
          {
            disabled: !a.enabled,
            'data-tip': a.reason ?? a.label,
            onclick: () => this.handlers.onAbility(a.id),
          },
          h('span.key', {}, a.hotkey),
          abilityIcon(a.id),
          h('span.label', {}, a.label),
          a.count !== undefined ? h('span.count', {}, `×${a.count}`) : null,
          a.cooldown ? h('span.cd', { title: `Disponible en ${a.cooldown} turnos` }, String(a.cooldown)) : null,
          a.depleted ? h('span.stamp', { 'aria-label': 'Agotado' }) : null,
        ),
      );
    }
  }

  setTarget(view: TargetView | null): void {
    clear(this.target);
    this.target.classList.toggle('hidden', !view);
    this.target.classList.toggle('support', view?.kind === 'support');
    if (!view) return;
    const head = h(
      'div.shot-head',
      {},
      h('button.cycle.prev', { onclick: () => this.handlers.onCycleTarget(-1), title: 'Objetivo anterior (Mayús+Tab)' }, arrowIcon('left')),
      h('div.shot-target', {}, view.targetName, h('small', {}, ` ${view.index + 1}/${view.total}`)),
      h('button.cycle.next', { onclick: () => this.handlers.onCycleTarget(1), title: 'Siguiente objetivo (Tab)' }, arrowIcon('right')),
    );
    const actions = h(
      'div.shot-actions',
      {},
      h('button.fire', { onclick: () => this.handlers.onConfirmTarget(), title: 'Espacio' }, view.action.toUpperCase()),
      h('button.cancel', { onclick: () => this.handlers.onCancel(), title: 'Esc' }, 'Cancelar'),
    );
    if (view.kind === 'support') {
      append(this.target, h('div.shot-title', {}, type(view.title)), head, h('div.effect', {}, view.effect), actions);
      return;
    }
    const row = (m: Modifier) => h('div.mod', {}, h('span', {}, m.label), h(`span.${m.value >= 0 ? 'pos' : 'neg'}`, {}, `${m.value > 0 ? '+' : ''}${m.value}`));
    append(
      this.target,
      h('div.shot-title', {}, type(view.title)),
      head,
      h(
        'div.shot-numbers',
        {},
        h('div.big', {}, `${view.hit}%`, h('small', {}, 'IMPACTO')),
        h('div.big.crit', {}, `${view.crit}%`, h('small', {}, 'CRÍTICO')),
      ),
      h('div.mods', {}, ...view.mods.map(row)),
      view.critMods.length ? h('div.mods.crit', {}, ...view.critMods.map(row)) : null,
      view.graze ? h('div.graze', {}, `Rozadura posible: ${view.graze}%`) : null,
      view.note ? h('div.effect', {}, view.note) : null,
      actions,
    );
  }

  /**
   * `waiting`: the partner has the command (`waitingFor` names them).
   * `confirm`: pressed once with soldiers still able to act (`waitingFor` says how many).
   */
  setEndTurn(mode: 'act' | 'ready' | 'waiting' | 'disabled' | 'confirm', waitingFor: string | null): void {
    this.endTurn.disabled = mode === 'disabled' || mode === 'waiting';
    this.endTurn.classList.toggle('is-ready', mode === 'ready');
    this.endTurn.classList.toggle('is-waiting', mode === 'waiting');
    this.endTurn.classList.toggle('is-confirm', mode === 'confirm');
    clear(this.endTurn);
    if (mode === 'confirm') {
      this.endTurn.append(h('span', {}, '¿TERMINAR?'), h('small', {}, waitingFor ?? 'Pulsa otra vez'));
    } else if (mode === 'ready') {
      this.endTurn.append(h('span', {}, 'LISTO ✓'), h('small', {}, waitingFor ? `Esperando a ${waitingFor}` : 'Pulsa para cancelar'));
    } else if (mode === 'waiting') {
      this.endTurn.append(h('span', {}, `MANDO: ${(waitingFor ?? '').toUpperCase()}`), h('small', {}, 'Espera a que te lo ceda'));
    } else {
      this.endTurn.append(h('span', {}, 'TERMINAR TURNO'), h('small', {}, 'Retroceso'));
    }
  }

  /** The "pass the command" button: only for the holder, while playing with a partner. */
  setCommand(view: CommandView | null): void {
    this.passBtn.classList.toggle('hidden', !view?.mine);
    this.passBtn.disabled = !view?.canPass;
    this.root.classList.toggle('has-command', !!view?.mine);
    this.root.classList.toggle('waiting-command', !!view && !view.mine);
  }

  banner(text: string, kind: 'xcom' | 'alien' | 'warn' = 'xcom'): void {
    this.bannerEl.textContent = text;
    this.bannerEl.className = `banner show ${kind}`;
    window.clearTimeout(this.bannerTimer);
    this.bannerTimer = window.setTimeout(() => this.bannerEl.classList.remove('show'), 1500);
  }

  /** Arrival briefing: operation, area and objective, then it fades away. */
  intro(kicker: string, title: string, lines: string[]): void {
    const el = h('div.mission-intro', {}, h('small', {}, kicker), h('h1', {}, type(title)), ...lines.map((l) => h('p', {}, l)));
    this.root.append(el);
    window.setTimeout(() => el.classList.add('out'), 4200);
    window.setTimeout(() => el.remove(), 5000);
  }

  toast(text: string): void {
    this.toastEl.textContent = text;
    this.toastEl.classList.add('show');
    window.clearTimeout(this.toastTimer);
    this.toastTimer = window.setTimeout(() => this.toastEl.classList.remove('show'), 2600);
  }

  /** Newest first; folded it shows the last two, unfolded the rest. */
  log(text: string, color?: string): void {
    this.logLines.unshift(h('div.log-line', { style: color ? { borderLeftColor: color } : {} }, text));
    this.logLines.length = Math.min(this.logLines.length, LOG_KEEP);
    this.renderLog();
  }

  private renderLog(): void {
    clear(this.logEl);
    this.logEl.classList.toggle('open', this.logOpen);
    this.logEl.classList.toggle('empty', !this.logLines.length);
    append(this.logEl, ...(this.logOpen ? this.logLines : this.logLines.slice(0, LOG_FOLDED)));
    const more = this.logLines.length - LOG_FOLDED;
    if (more > 0 || this.logOpen) {
      this.logEl.append(
        h(
          'button.log-toggle',
          { onclick: () => ((this.logOpen = !this.logOpen), this.renderLog()) },
          this.logOpen ? '▴ Plegar' : `▾ ${more} más`,
        ),
      );
    }
  }

  // --------------------------------------------------------------- menu

  /** Esc menu: controls, settings and abandoning the mission. */
  toggleMenu(open = !this.menu.classList.contains('show')): void {
    this.menu.classList.toggle('show', open);
    if (open) this.renderMenu('main');
  }

  private renderMenu(page: 'main' | 'controls' | 'settings'): void {
    clear(this.menu);
    const back = h('button.menu-back', { onclick: () => this.renderMenu('main') }, '‹ Volver');
    const card = h('div.game-menu-card.panel');
    if (page === 'main') {
      append(
        card,
        h('h2', {}, 'Menú'),
        h('button.menu-entry', { onclick: () => this.toggleMenu(false) }, 'Continuar'),
        h('button.menu-entry', { onclick: () => this.renderMenu('controls') }, 'Controles'),
        h('button.menu-entry', { onclick: () => this.renderMenu('settings') }, 'Ajustes'),
        h('button.menu-entry.danger', { onclick: () => (this.toggleMenu(false), this.confirmAbandon()), title: 'Termina la misión para los dos' }, 'Abandonar misión'),
      );
    } else if (page === 'controls') {
      append(card, h('h2', {}, 'Controles'), h('dl.controls', {}, ...CONTROLS.flatMap(([key, what]) => [h('dt', {}, key), h('dd', {}, what)])), back);
    } else {
      const sound = h(
        'button.menu-entry.toggle',
        {
          onclick: () => {
            this.handlers.onToggleSound();
            this.renderMenu('settings');
          },
        },
        `Sonido: ${this.handlers.soundOn() ? 'activado' : 'desactivado'}`,
      );
      append(card, h('h2', {}, 'Ajustes'), sound, graphicsSettings(() => this.renderMenu('settings')), back);
    }
    this.menu.append(card);
  }

  get menuOpen(): boolean {
    return this.menu.classList.contains('show');
  }

  showOutcome(outcome: Outcome | null, title = '', summary = '', campaign = false): void {
    clear(this.outcome);
    this.outcome.classList.toggle('show', !!outcome);
    if (!outcome) return;
    this.outcome.append(
      h(
        `div.outcome-card.${outcome}`,
        {},
        h('h1', {}, outcome === 'victory' ? 'MISIÓN CUMPLIDA' : 'MISIÓN FRACASADA'),
        title ? h('h2', {}, title) : '',
        // In a campaign the numbers are in the hangar's debrief, one screen later: here only the result.
        campaign ? '' : h('p', {}, summary),
        campaign
          ? h('div.outcome-actions', {}, h('button.fire', { onclick: () => this.handlers.onReturnToBase() }, 'VOLVER A LA BASE'))
          : h(
              'div.outcome-actions',
              {},
              h('button.fire', { onclick: () => this.handlers.onNewMission() }, 'OTRA IGUAL (MAPA NUEVO)'),
              h('button.secondary', { onclick: () => this.handlers.onNewMission(randomKind(), randomMap()) }, 'MISIÓN AL AZAR'),
              h('button.secondary', { onclick: () => this.handlers.onBackToMenu(), title: 'Los dos volvéis al menú principal' }, 'VOLVER AL MENÚ'),
            ),
      ),
    );
  }

  private confirmAbandon(): void {
    clear(this.modal);
    this.modal.classList.add('show');
    const close = () => this.modal.classList.remove('show');
    this.modal.append(
      h(
        'div.modal-card.panel',
        {},
        h('h2', {}, '¿Abandonar la misión?'),
        h('p', {}, 'La misión termina para los dos jugadores y cuenta como fracasada.'),
        h(
          'div.modal-actions',
          {},
          h('button.danger', { onclick: () => (close(), this.handlers.onAbandon()) }, 'ABANDONAR'),
          h('button.fire', { onclick: close }, 'SEGUIR JUGANDO'),
        ),
      ),
    );
  }

  setConnection(status: NetStatus): void {
    this.connection.textContent = status === 'open' ? '' : status === 'connecting' ? 'Conectando…' : 'Conexión perdida · reconectando…';
    this.connection.classList.toggle('show', status !== 'open');
  }

  destroy(): void {
    this.root.remove();
  }
}
