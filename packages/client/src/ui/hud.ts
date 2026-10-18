import {
  PIECES,
  ABANDON_TEXT,
  BELT_TEXT,
  beltable,
  SETTINGS_TEXT,
  BITE_TAG,
  NIGHT_TEXT,
  biteNote,
  clipOf,
  displayName,
  weaponName,
  FOUND_WEAPONS,
  ITEMS,
  magsCount,
  MISSION_NAMES,
  PISTOL_CLIP,
  TEMPLATES,
  VETERANCY,
  apPerTurn,
  conditionOf,
  evacWaiting,
  wearable,
  WEAPONS,
  type AbilityId,
  type FoundWeaponId,
  type ItemId,
  type MissionKind,
  type Modifier,
  type Outcome,
  type PlayerInfo,
  type SkirmishMap,
  type Slot,
  type Unit,
} from '@bastion/engine';
import { sound } from '../audio/bank';
import { uiSound } from '../audio/ui';
import type { NetStatus } from '../net';
import { append, clear, h } from './dom';
import { abilityIcon, arrowIcon } from './icons';
import { wearMarks } from '../strategy/arms';
import { canQuit, QUIT_BUTTON, quitGame, quitQuestion } from './quit';
import { graphicsSettings, soundSettings } from './settingsPanel';
import { typed } from './typed';

/**
 * What the action bar holds: the soldier's abilities, and the evacuation's own
 * (playtest 15/10 #10): «Evacuar» from La Lata, and the partner's answer to it;
 * and the same for abandoning the mission (16/10): the wait, and the answer.
 */
export type BarActionId = AbilityId | 'evac' | 'evacAccept' | 'evacRefuse' | 'abandon' | 'abandonAccept' | 'abandonRefuse' | MenuId;

/** The bar's actions with a menu (docs/FASE10.md §4): Lanzar, Vigilancia, Investigar. */
export type MenuId = 'menu:throw' | 'menu:watch' | 'menu:investigate';
export const isMenu = (id: BarActionId): id is MenuId => id.startsWith('menu:');

export interface AbilityView {
  id: BarActionId;
  label: string;
  hotkey: string;
  enabled: boolean;
  /** Why it can't be used now. */
  reason?: string;
  /** The hover card: name, cost («1 acción», «Acaba el turno»…), limits and what it does. */
  name: string;
  cost: string;
  /** Cooldown and uses left («Disponible en 2 turnos · Usos: 1»). */
  limits?: string;
  /** What it takes off, for what does damage («Daño 4–6 · crítico +2»). */
  damage?: string;
  /** How far it is heard («ruido 12», «sin ruido»), after the cost (docs/FASE2.md). */
  noise?: string;
  description: string;
  active?: boolean;
  count?: number;
  /** Turns left before it can be used again. */
  cooldown?: number;
  /** Costs no action. */
  free?: boolean;
  /** Nothing left to use (no ammo, no charges): stamped, not explained. */
  depleted?: boolean;
  /** Its drawing when it isn't its id's (a menu shows its family's). */
  icon?: AbilityId;
  /** A menu's choices: with two or more, pressing it opens them above it; with one, it is that one. */
  options?: AbilityView[];
}

/**
 * Whose turn the screen shows: mine (I give the orders), the partner's (they
 * hold the command) or the enemy's. It colours the frame and the turn label.
 */
export type TurnOwner = { kind: 'mine'; color: string } | { kind: 'partner'; name: string; color: string } | { kind: 'enemy' };

/** Who holds the command (el mando) while playing with a partner. */
export interface CommandView {
  mine: boolean;
  holder: string;
  color: string;
  /** The holder can hand it over (partner connected and still playing). */
  canPass: boolean;
}

/** What the aimed ability takes off that target: ranges after its armour, and what armour does. */
export interface DamageView {
  hit: string;
  crit?: string;
  graze?: string;
  notes: string[];
  /** «Lo mata», «Puede matarlo», «Un crítico lo mata». */
  kill?: string;
}

export type TargetView =
  | {
      kind: 'attack';
      title: string;
      /** The confirm button: the ability alone ("Disparar"); the title also names the weapon. */
      action: string;
      /** Planning without the command: the button is off and says who holds it ("MANDO: ANA"). */
      locked?: string;
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
      damage: DamageView;
      /** «Ruido 12»: how far the shot is heard. */
      noise?: string;
    }
  | { kind: 'support'; title: string; action: string; locked?: string; targetName: string; effect: string; damage?: DamageView; index: number; total: number; noise?: string }
  /**
   * A self ability armed by its first press: what it costs and does, until the
   * second press or Enter. Registrar arms the same way, on the furniture it
   * would search (`hint` then says how to pick another).
   */
  | { kind: 'self'; title: string; action: string; locked?: string; cost: string; limits?: string; effect: string; detail?: string; noise?: string; hint?: string };

/** The Saqueo's horde (docs/FASE2.md): how near the next wave is, 0–1, and that in words. */
export interface HordeView {
  fill: number;
  title: string;
}

export interface MissionView {
  kind: MissionKind;
  /** In place of the kind's name (a siege: «Asedio», docs/FASE12.md). */
  label?: string;
  objective: string;
  /** The objective in full, when the strip shows a short version. */
  objectiveTip?: string;
  turnsLeft: number | null;
  /** The turns left are to hold out (a siege): the count runs towards the win, so it is never urgent. */
  hold?: boolean;
  /** A siege's next wave: short, in full for its tooltip, and whether it comes in this turn. */
  wave?: { text: string; tip: string; urgent: boolean } | null;
  /** Urgent notice under the objective (reinforcements on the way). */
  alert: string | null;
  /** Name of the area (map). */
  area: string;
  /** Saqueo only: the horde's clock. */
  horde?: HordeView | null;
}

/** One thing in a backpack (docs/FASE2.md, «Mochila»): what it is, for the slot's icon and tooltip. */
export type PackSlot =
  | { kind: 'supplies'; amount: number }
  | { kind: 'item'; item: ItemId }
  | { kind: 'medicines'; amount: number }
  /** docs/FASE4.md: rations, parts and a good weapon found (FOUND_WEAPONS). */
  | { kind: 'food'; amount: number }
  | { kind: 'parts'; amount: number }
  | { kind: 'weapon'; weapon: FoundWeaponId };

/** The soldier's backpack on the sheet, and what the whole squad carries. */
export interface PackView {
  slots: PackSlot[];
  size: number;
  squad: { supplies: number; items: number; medicines: number; food: number; parts: number; weapons: number; text: string };
  /** One of the player's own, with orders to give (docs/FASE8.md §C): the slots take a click. */
  live?: boolean;
  /** What each slot holds, in words (lore.ts findLabel), for its menu. */
  labels?: string[];
  /** The same as a phrase (lore.ts lootName: «una medicina»), for «Dejar …» and «Coger …». */
  names?: string[];
  /** Partners right next to it: who it can hand a thing to, or treat. */
  near?: { id: string; name: string }[];
  /** Things on its tile (GameState.dropped), to pick up. */
  floor?: { id: string; label: string; name: string; kind: PackSlot['kind'] }[];
  /** What it has on (Unit.worn): one vest at a time (rules.ts wearable). */
  worn?: ItemId[];
  /** What it carries (Unit.gear): what only gives, once (rules.ts beltable). */
  gear?: ItemId[];
}

/** An order on a thing carried or lying on the floor: the engine's `item` command (docs/FASE8.md §C). */
export interface ItemOrder {
  action: 'drop' | 'take' | 'give' | 'use' | 'equip';
  index?: number;
  id?: string;
  to?: string;
}

/** The backpack's menu open: a slot's orders, or what to leave to take a thing from the floor. */
type PackMenu = { unit: string; slot: number } | { unit: string; swap: string } | null;

/** The symbol of an accessory found while searching, from the sheet's own set (styles.css). */
const ITEM_ICONS: Record<ItemId, string> = {
  fragGrenade: 'grenade',
  smokeGrenade: 'grenade',
  flashbang: 'grenade',
  firstAid: 'medkit',
  nanoVest: 'armor',
  mindShield: 'will',
  apRounds: 'ammo',
  tracerRounds: 'ammo',
};

const KINDS: MissionKind[] = ['elimination', 'recovery', 'sabotage', 'hack', 'rescue', 'scavenge'];
const MAPS: SkirmishMap[] = ['city', 'wilds', 'facility'];
const FOLLOW_TURN_KEY = 'bastion.followTurn';

/** The camera switch as last left (on unless switched off). */
function readFollowTurn(): boolean {
  try {
    return localStorage.getItem(FOLLOW_TURN_KEY) !== '0';
  } catch {
    return true;
  }
}

const randomKind = () => KINDS[Math.floor(Math.random() * KINDS.length)]!;
const randomMap = () => MAPS[Math.floor(Math.random() * MAPS.length)]!;

export interface HudHandlers {
  onAbility(id: BarActionId): void;
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
  /**
   * Abandoning as it stands (16/10): the partner who has to say yes (null
   * alone, or with the partner gone: it ends at once), and whether this
   * player's own call is waiting for it.
   */
  abandonState(): { partner: string | null; waiting: boolean };
  onPassCommand(): void;
  /** A menu of the bar opened or closed: draw the bar again. */
  onMenu(): void;
  /** The camera to what the partner holding the command is doing. */
  onWatchPartner(): void;
  /** Toggles the sound; returns whether it is now on. */
  onToggleSound(): boolean;
  soundOn(): boolean;
  /** One floor up (1) or down (-1) (docs/FASE6.md). */
  onFloor(dir: 1 | -1): void;
  /** An order on the backpack or the floor (docs/FASE8.md §C). */
  onItem(order: ItemOrder): void;
  /** Leave slot `drop` on the floor and pick up `take` from it: the swap when the backpack is full. */
  onSwap(take: string, drop: number): void;
  /** «Señalar» (playtest 18/10 #7): the next click on the map signals that spot to the partner; again, it's put away. */
  onPingArm(): void;
  /** «Hoja» (playtest 18/10 #23): the soldier's sheet, only to look at. */
  onSheet(unit: string): void;
}

/** A soldier's sheet in the battle (playtest 18/10 #23): who, the figures, what it carries and its skills; only to look at. */
export interface SoldierSheetView {
  name: string;
  /** Its veterancy (or what it is, if not a soldier) and, a partner's, whose. */
  rank: string;
  whose: { name: string; color: string } | null;
  /** Label, value and, when it helps, the long version. */
  stats: [string, string, string?][];
  carries: [string, string][];
  /** The board's cells it has (in a skirmish, where there is no board to show). */
  skills: string[];
}

const CONTROLS: [string, string][] = [
  ['Clic', 'Seleccionar y mover'],
  ['Tab', 'Siguiente soldado u objetivo'],
  ['1–9', 'Elegir habilidad (otra vez para usarla)'],
  ['Espacio · Intro', 'Usar la habilidad elegida; sin ninguna, disparar'],
  ['C', 'Ceder el mando'],
  ['Retroceso', 'Terminar turno'],
  ['G · Alt+clic · clic con la rueda', SETTINGS_TEXT.pingControls],
  ['H · clic en su nombre', 'Hoja del soldado: tablero, estadísticas y lo que lleva'],
  ['Q / E', 'Girar la cámara'],
  ['Rueda', 'Acercar o alejar: de lejos se ven los tejados; de cerca, la planta del soldado'],
  ['RePág / AvPág · Mayús + rueda', 'Subir o bajar de planta'],
  ['WASD', 'Mover la cámara'],
  ['F', 'Centrar en el soldado'],
  ['O', 'Centrar en el objetivo'],
  ['Clic derecho', 'Ficha del enemigo'],
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

/** The damage under the odds: «DAÑO 3–5 · crít. 5–7», what armour does, whether it kills. */
function damageBlock(d: DamageView): HTMLElement {
  return h(
    'div.shot-damage',
    {},
    h('div.dmg-line', {}, h('span.dmg-label', {}, 'DAÑO'), h('b.dmg', {}, d.hit), d.crit ? h('span.dmg-crit', {}, `crít. ${d.crit}`) : null),
    ...d.notes.map((n) => h('div.dmg-note', {}, n)),
    d.kill ? h('div.dmg-kill', {}, d.kill) : null,
  );
}

/** Room kept between a panel and the screen's edge. */
const EDGE = 8;

/** Log lines kept, and shown while folded. */
const LOG_KEEP = 30;
const LOG_FOLDED = 2;

/** DOM overlay for everything that is not in the 3D scene. */
export class Hud {
  readonly root = h('div.hud');
  /** What the mission's carrier carries, as its tag (lore.ts itemTag: «MEDICINAS»); set by the game per mission. */
  itemTag = 'CARGA';
  /** Round the screen, in the colour of whoever plays now (playtest: «que se note de quién es el turno»). */
  private readonly frame = h('div.turn-frame');
  private readonly turn = h('div.turn');
  /** Which floor is shown, on maps with storeys (docs/FASE6.md): «Planta 1 de 3», with arrows. */
  private readonly floor = h('div.floor-ind.hidden');
  private floorKey = '';
  private readonly mission = h('div.mission');
  private readonly players = h('div.players');
  /** There is a partner in the room, connected or not: abandoning ends the mission for both. */
  private paired = false;
  /** The partner, while connected: whom leaving the game would leave without it, if this window hosts it (ui/quit.ts). */
  private partnerOnline: string | null = null;
  private readonly squad = h('div.squad.panel');
  private readonly unitPanel = h('div.unit-panel.panel');
  private readonly abilities = h('div.abilities');
  private readonly target = h('div.shot-panel.panel');
  private readonly endTurn = h('button.end-turn');
  private readonly passBtn = h('button.pass-command', { title: 'Tu compañero da las órdenes hasta que te lo devuelva (C)' });
  /** With the partner holding the command: the camera to what they are doing (playtest 16/10 #8). */
  private readonly watchBtn = h('button.pass-command.watch-partner', { title: 'Lleva la cámara a lo que está haciendo tu compañero (F)' });
  /** The camera to the soldier whose turn it is, or not (playtest 17/10 #47): a switch beside «Ver», kept per browser. */
  private followTurn = readFollowTurn();
  private readonly followBtn = h('button.pass-command.follow-turn', { type: 'button', title: 'Al cambiar el turno o el mando, la cámara va al soldado al que le toca (si no acabas de moverla tú)' });
  private readonly watchWho = h('span');
  private readonly logEl = h('div.log');
  private readonly logLines: HTMLElement[] = [];
  private logOpen = false;
  private readonly menu = h('div.game-menu');
  private readonly bannerEl = h('div.banner');
  private readonly toastEl = h('div.toast');
  /** A key moment the partner caused, with «Ver (F)» (UX-REVISION-2 #36). */
  private readonly noticeEl = h('div.toast.partner-notice');
  /** Something found with the backpack full, left on the floor: «Cambiar» to choose what stays (docs/FASE8.md §C). */
  private readonly packOfferEl = h('div.toast.pack-offer');
  private packOfferTimer = 0;
  private packMenu: PackMenu = null;
  /** The soldier on the sheet, and the backpack last drawn, so an open menu survives the panel's redraws. */
  private shownUnit: string | null = null;
  private readonly outcome = h('div.outcome');
  private readonly modal = h('div.modal');
  private readonly connection = h('div.connection');
  /** What a bite does in this mission, on the MORDISCO tags (game.ts setBiteNote). */
  private biteNote = biteNote(true);
  private bannerTimer = 0;
  private toastTimer = 0;

  private readonly pingBtn = h('button.menu-button.ping-button', { type: 'button', title: `${SETTINGS_TEXT.pingTip} ${SETTINGS_TEXT.pingKeys}` }, SETTINGS_TEXT.ping);

  constructor(container: HTMLElement, private readonly handlers: HudHandlers) {
    this.endTurn.addEventListener('click', () => handlers.onEndTurn());
    this.passBtn.addEventListener('click', () => handlers.onPassCommand());
    this.passBtn.append(h('span', {}, 'CEDER EL MANDO'), h('small', {}, 'C'));
    this.watchBtn.addEventListener('click', () => handlers.onWatchPartner());
    // A menu of the bar closes with a press anywhere else.
    document.addEventListener('pointerdown', (e) => {
      if (this.barMenu && !(e.target as Element | null)?.closest?.('.ability-slot')) this.closeAbilityMenu();
    });
    this.watchBtn.append(this.watchWho, h('small', {}, 'F'));
    this.followBtn.addEventListener('click', () => {
      this.followTurn = !this.followTurn;
      try {
        localStorage.setItem(FOLLOW_TURN_KEY, this.followTurn ? '1' : '0');
      } catch {
        /* storage unavailable: it holds for this game */
      }
      this.drawFollow();
    });
    this.drawFollow();
    const menuButton = h('button.menu-button', { onclick: () => this.toggleMenu(), title: 'Menú (Esc)' }, 'Menú');
    // Signalling a spot, in view (playtest 18/10 #7: Arturo didn't know it was there, and a trackpad has no middle click).
    this.pingBtn.addEventListener('click', () => handlers.onPingArm());
    this.root.append(
      this.frame,
      h('div.top', {}, this.turn, this.floor, this.mission, this.players),
      this.squad,
      this.unitPanel,
      this.abilities,
      this.target,
      h('div.turn-actions', {}, this.followBtn, this.passBtn, this.watchBtn, this.endTurn),
      h('div.side', {}, h('div.side-row', {}, this.pingBtn, menuButton), this.logEl),
      this.bannerEl,
      this.toastEl,
      this.noticeEl,
      this.packOfferEl,
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

  /**
   * Whose turn it is, three ways at once: the label on the strip («TU TURNO»,
   * «TURNO DE ANA», «TURNO ENEMIGO») on a tab of that player's colour, a frame
   * round the screen in it (dashed for the partner's), and the ability cards
   * resting when the orders aren't ours. `owner` null: the mission is over.
   */
  /**
   * `night`: a night mission (docs/ALTURA-Y-NOCHE.md): a tag («De noche»)
   * beside the concealment, whose tooltip says what the dark changes.
   */
  /**
   * `inPlay`: the soldier in play while turns go one soldier at a time
   * (playtest 15/10 #9), by the label: whoever holds the turn finishes it first.
   */
  setTurn(turn: number, owner: TurnOwner | null, concealed: boolean, night: { label: string; rules: string } | null = null, inPlay: string | null = null): void {
    const key = `${turn}|${JSON.stringify(owner)}|${concealed}|${night?.label}|${inPlay}`;
    if (key === this.turnKey) return;
    this.turnKey = key;
    const kind = owner?.kind ?? 'over';
    for (const k of ['mine', 'partner', 'enemy'] as const) this.root.classList.toggle(`turn-${k}`, kind === k);
    // --turn colours the frame, the tab and the slip over the cards.
    this.root.style.setProperty('--turn', owner && owner.kind !== 'enemy' ? owner.color : 'var(--stamp)');
    this.frame.className = `turn-frame ${kind}`;
    const label = !owner ? 'FIN DE LA MISIÓN' : owner.kind === 'mine' ? 'TU TURNO' : owner.kind === 'partner' ? `TURNO DE ${owner.name.toUpperCase()}` : 'TURNO ENEMIGO';
    // Why the cards rest: the bar says it over itself.
    if (owner?.kind === 'partner') this.abilities.dataset.lock = `Turno de ${owner.name}: puedes apuntar y planear`;
    else if (owner?.kind === 'enemy') this.abilities.dataset.lock = 'Turno enemigo';
    else delete this.abilities.dataset.lock;
    clear(this.turn);
    append(
      this.turn,
      // The number goes in the tooltip: the strip has no room for it next to a partner's name.
      h(`span.turn-label.${kind}`, { title: `Turno ${turn}` }, type(label)),
      inPlay ? h('span.in-play', { title: 'Un soldado cada vez: este termina sus acciones antes que otro, o se cede el turno' }, `EN JUEGO · ${inPlay.toUpperCase()}`) : null,
      h(`span.conceal.${concealed ? 'on' : 'off'}`, {}, concealed ? 'OCULTOS' : 'DETECTADOS'),
      // The same stamp as the concealment, so every theme dresses it alike; `.night` gives its colour.
      night ? h('span.conceal.night', { title: night.rules }, night.label.toUpperCase()) : null,
    );
  }

  /**
   * The active floor (docs/FASE6.md): «Planta baja» or «Planta 2 de 3», with
   * an arrow up and one down; hidden on maps without storeys (`null`).
   */
  setFloor(view: { level: number; max: number } | null): void {
    const key = view ? `${view.level}/${view.max}` : '';
    if (key === this.floorKey) return;
    this.floorKey = key;
    this.floor.classList.toggle('hidden', !view);
    clear(this.floor);
    if (!view) return;
    const label = view.level === 0 ? 'Planta baja' : `Planta ${view.level} de ${view.max}`;
    append(
      this.floor,
      h('button.floor-down', { disabled: view.level === 0, title: 'Bajar de planta (AvPág)', 'aria-label': 'Bajar de planta', onclick: () => this.handlers.onFloor(-1) }, '▼'),
      h('span.floor-label', { title: 'RePág / AvPág, o Mayús y la rueda' }, label.toUpperCase()),
      h('button.floor-up', { disabled: view.level >= view.max, title: 'Subir de planta (RePág)', 'aria-label': 'Subir de planta', onclick: () => this.handlers.onFloor(1) }, '▲'),
    );
  }

  setMission(m: MissionView): void {
    const key = JSON.stringify(m);
    if (key === this.missionKey) return;
    this.missionKey = key;
    clear(this.mission);
    append(
      this.mission,
      h('span.mission-kind', { title: m.area }, type((m.label ?? MISSION_NAMES[m.kind]).toUpperCase())),
      h('span.mission-goal', { title: m.objectiveTip ?? m.objective }, type(m.objective)),
      m.turnsLeft !== null
        ? h(`span.timer${m.turnsLeft <= 3 && !m.hold ? '.urgent' : ''}`, { title: m.hold ? m.objectiveTip : undefined }, `${m.turnsLeft} ${m.turnsLeft === 1 ? 'TURNO' : 'TURNOS'}`)
        : null,
      m.wave ? h(`span.timer.wave${m.wave.urgent ? '.urgent' : ''}`, { title: m.wave.tip }, m.wave.text) : null,
      m.horde ? this.hordeGauge(m.horde) : null,
      m.alert ? h('span.timer.urgent.alert', {}, m.alert) : null,
    );
  }

  /** «Señalar» armed: the next click on the map signals it (lit while it waits). */
  setPingArmed(on: boolean): void {
    this.pingBtn.classList.toggle('on', on);
  }

  /** The horde's clock in the strip: a short bar that fills towards the next wave (full: it comes this turn). */
  private hordeGauge(horde: HordeView): HTMLElement {
    const level = horde.fill >= 1 ? '.full' : horde.fill >= 0.7 ? '.near' : '';
    return h(
      `span.horde${level}`,
      { title: horde.title, 'aria-label': horde.title },
      h('span.horde-label', {}, 'HORDA'),
      h('span.horde-bar', {}, h('i', { style: { width: `${Math.round(Math.min(1, horde.fill) * 100)}%` } })),
    );
  }

  setPlayers(players: PlayerInfo[], ready: [boolean, boolean], mySlot: Slot, busy: Set<Slot>, commander: Slot | null = null): void {
    clear(this.players);
    this.paired = players.some((p) => p.slot !== mySlot);
    this.partnerOnline = players.find((p) => p.slot !== mySlot && p.connected)?.name ?? null;
    // Alone, the turn state is the end-turn button's business: a status here read as "you are waiting".
    const alone = !players.some((p) => p.slot !== mySlot && p.connected);
    for (const p of players) {
      const holds = commander === p.slot;
      // Who holds the command is the turn label's business now («TURNO DE ANA»): here only the underline.
      // The window minimized or the laptop asleep: back soon, their turn waits for them.
      const status = !p.connected ? 'Desconectado' : p.away ? 'Ausente' : alone || holds ? '' : ready[p.slot] ? 'Listo' : busy.has(p.slot) ? 'Con acciones' : 'Sin acciones';
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

  /** What a bite does in this mission (playtest 15/10 #13), for the MORDISCO tags' tooltip. */
  setBiteNote(text: string): void {
    this.biteNote = text;
  }

  /** `inPlay`: the soldier in play (playtest 15/10 #9), marked on its card. */
  /** `commands`: whether this player gives the unit orders (its own, and the partner's while the partner is gone). */
  setSquad(
    units: Unit[],
    mySlot: Slot,
    selected: string | null,
    colors: Record<number, string>,
    carrier: string | null,
    inPlay: string | null = null,
    commands: (u: Unit) => boolean = (u) => u.team === 'xcom' && (u.owner === mySlot || u.owner === null),
  ): void {
    clear(this.squad);
    // One's own first, then the partner's commanded while they're gone, then the rest.
    const rank = (u: Unit) => (!commands(u) ? 2 : u.owner === mySlot || u.owner === null ? 0 : 1);
    const sorted = [...units].sort((a, b) => rank(a) - rank(b));
    for (const u of sorted) {
      const mine = commands(u);
      const theirs = u.owner !== null && u.owner !== mySlot;
      const state = u.evacuated ? '.evacuated' : u.alive ? '' : '.dead';
      const card = h(
        `div.squad-card${u.id === selected ? '.selected' : ''}${state}${mine ? '.mine' : ''}${u.id === inPlay ? '.in-play' : ''}`,
        {
          style: { '--accent': u.owner === null ? '#e8edf2' : colors[u.owner] },
          onclick: () => mine && u.alive && this.handlers.onSelectUnit(u.id),
          // A siege's turret: in the list for its health and rounds, nobody's to pick (rules.ts).
          title: u.automaton ? 'Va sola: nadie le da órdenes.' : !theirs ? 'Seleccionar' : mine ? 'Soldado de tu compañero: lo mandas tú mientras no está' : 'Soldado de tu compañero',
        },
        // Fase 9: soldiers have no class any more; only who isn't one (the VIP) says what it is.
        h('div.card-name', {}, u.template === 'rookie' ? null : h('b.cls', {}, TEMPLATES[u.template].short), u.template === 'rookie' ? null : ' ', u.nickname ? `«${u.nickname}»` : u.name),
        h(
          'div.card-row',
          {},
          h('span.hp', {}, ...Array.from({ length: u.maxHp }, (_, i) => h(`i${i < u.hp ? '.on' : ''}`))),
          // A turret has no actions of its own: its rounds instead, against what a full load holds.
          u.automaton
            ? h('span.ap.rounds', { title: `Munición: ${u.ammo}/${PIECES.turret.charges ?? u.ammo}` }, ...Array.from({ length: Math.max(PIECES.turret.charges ?? 0, u.ammo) }, (_, i) => h(`i${i < u.ammo ? '.on' : ''}`)))
            : h('span.ap', {}, ...Array.from({ length: Math.max(apPerTurn(u), u.ap) }, (_, i) => h(`i${i < u.ap ? '.on' : ''}`))),
          u.id === inPlay ? h('em.in-play', { title: 'En juego: termina sus acciones antes que otro' }, 'EN JUEGO') : null,
          u.controlledBy ? h('em.psi', {}, 'CONTROLADO') : null,
          u.captive ? h('em.data', {}, 'CAUTIVO') : null,
          u.disoriented ? h('em.psi', {}, 'DESORIENTADO') : null,
          u.panicked ? h('em.psi', {}, 'PÁNICO') : null,
          // Bitten (docs/FASE3.md): if it falls it rises, and back at the base it turns unless treated.
          u.infected && u.alive ? h('em.bite', { title: this.biteNote }, BITE_TAG) : null,
          u.evacuated ? h('em.evac', {}, 'EVACUADO') : null,
          carrier === u.id ? h('em.data', {}, this.itemTag) : null,
          u.overwatch ? h('em.ow', {}, u.killZone ? 'ZONA LETAL' : u.watchMelee ? 'HOJA EN ALTO' : 'VIGILANCIA') : null,
          u.hunkered ? h('em.hk', {}, 'CUBRIÉNDOSE') : null,
          u.stunned ? h('em.stun', {}, 'ATURDIDO') : null,
        ),
      );
      this.squad.append(card);
    }
  }

  /**
   * The soldier sheet: symbols, pips and numbers, readable at a glance (full
   * words in the tooltips). `pack`: the backpack, when the map has anything to
   * search or the soldier carries something.
   */
  setSelected(u: Unit | null, pack: PackView | null = null, whose: { name: string; color: string } | null = null): void {
    clear(this.unitPanel);
    this.unitPanel.classList.toggle('hidden', !u);
    if (!u) return;
    const t = TEMPLATES[u.template];
    // A soldier's own arms (docs/FASE8.md §A): what is in the hand (the pistol if nothing), the pistol beside it, the knife or machete.
    const arms = u.arms;
    const weapon = u.weaponName ?? (arms ? (arms.primary ? FOUND_WEAPONS[arms.primary].name : WEAPONS.pistol.name) : weaponName(t.weapon, u.weaponTier ?? 1));
    const extras = (arms ? [arms.primary ? WEAPONS.pistol.name : '', arms.melee ? FOUND_WEAPONS[arms.melee].name : WEAPONS.knife.name] : [t.secondary && WEAPONS[t.secondary].name, t.meleeWeapon && WEAPONS[t.meleeWeapon].name])
      .filter(Boolean)
      .join(' · ');
    const clip = clipOf(u, 'shoot');
    const sidearm = !!arms?.primary;
    // A soldier's veterancy (Unit.rank, skills.ts), in the feminine for a woman.
    const soldier = u.team === 'xcom' && u.template === 'rookie';
    const step = VETERANCY[u.rank];
    const rank = soldier && step ? (u.appearance?.sex === 'f' ? step.female : step.name) : '';
    const pips = (filled: number, total: number) => Array.from({ length: total }, (_, i) => h(`i${i < filled ? '.on' : ''}`));
    const stat = (kind: string, title: string, ...content: (Node | string)[]) => h(`span.stat.${kind}`, { title }, h('span.stat-icon'), ...content);
    append(
      this.unitPanel,
      h(
        // A soldier's head opens its whole sheet, to look at (playtest 18/10 #23); H does too.
        `div.unit-head${soldier ? '.opens' : ''}`,
        soldier ? { title: `${rank} · La hoja del soldado: tablero, estadísticas y lo que lleva (H)`, onclick: () => this.handlers.onSheet(u.id) } : { title: t.name },
        soldier ? h('span.rank', { title: rank }, ...Array.from({ length: u.rank }, () => h('i'))) : null,
        h('span.unit-name', {}, type(displayName(u.name, u.nickname))),
        // The partner's soldier says whose, in the partner's colour (Arturo, 17/10), where the veterancy
        // goes: its chevrons and the sheet's tooltip still say it, and the name keeps its room.
        whose
          ? h('em.whose', { style: { '--accent': whose.color }, title: `Soldado de ${whose.name}${rank ? ` · ${rank}` : ''}` }, `DE ${whose.name.toUpperCase()}`)
          : h('span.unit-class', {}, soldier ? rank : t.short),
        // Bitten (playtest 15/10 #13): what it does to them, on the sheet as on the list.
        u.infected && u.alive ? h('em.bite', { title: this.biteNote }, BITE_TAG) : null,
      ),
      h(
        'div.unit-stats',
        {},
        stat('hp', `Salud ${u.hp}/${u.maxHp}`, h('span.pips', {}, ...pips(u.hp, u.maxHp))),
        stat('ap', `Acciones ${u.ap}/${apPerTurn(u)}`, h('span.pips', {}, ...pips(u.ap, Math.max(apPerTurn(u), u.ap)))),
        // The spare magazines after the clip (docs/FASE7.md): «+3», red at none; the enemies don't count theirs.
        clip > 0
          ? stat(
              'ammo',
              `${weapon}: ${u.ammo}/${clip}${u.mags === undefined ? '' : u.mags ? ` · ${magsCount(u.mags)} de repuesto` : ' · sin cargadores de repuesto'}`,
              h('span.pips', {}, ...pips(u.ammo, clip)),
              ...(u.mags === undefined ? [] : [h(`b.mags${u.mags ? '' : '.none'}`, {}, `+${u.mags}`)]),
            )
          : null,
        // The pistol carried beside a main gun counts its own rounds (PISTOL_CLIP); the spares serve both.
        sidearm ? stat('ammo.sidearm', `${WEAPONS.pistol.name}: ${u.pistolAmmo ?? 0}/${PISTOL_CLIP}`, h('span.pips', {}, ...pips(u.pistolAmmo ?? 0, PISTOL_CLIP))) : null,
        // With firearms (Tiro, Fase 9): the shots' aim; the blade's has its own line in the breakdown.
        stat('aim', `Puntería con armas de fuego ${t.aim + u.mods.aim + u.mods.gunAim}`, h('b', {}, String(t.aim + u.mods.aim + u.mods.gunAim))),
        // And with the blade, for a soldier (playtest 17/10).
        soldier ? stat('melee', `Puntería cuerpo a cuerpo ${t.aim + u.mods.aim + u.mods.meleeAim}`, h('b', {}, String(t.aim + u.mods.aim + u.mods.meleeAim))) : null,
        u.charges.grenade !== undefined ? stat('grenade', `Granadas: ${u.charges.grenade}`, h('b', {}, `×${u.charges.grenade}`)) : null,
        u.charges.medkit !== undefined ? stat('medkit', `Botiquín: ${u.charges.medkit}`, h('b', {}, `×${u.charges.medkit}`)) : null,
        u.armor ? stat('armor', `Blindaje ${u.armor}`, h('b', {}, String(u.armor))) : null,
        // A vest found and put on out there (playtest 16/10): its health is already in the pips.
        ...(u.worn ?? []).map((item) => stat('worn', `Lleva puesto: ${ITEMS[item].name} (${ITEMS[item].description.replace(/\.$/, '')})`, h('b', {}, `+${ITEMS[item].hp ?? 0}`))),
      ),
      h(
        'div.unit-weapon',
        { title: [weapon, extras].filter(Boolean).join(' · ') },
        h('span.stat-icon'),
        h('span.unit-weapon-name', {}, weapon),
        // Its state and improvements (docs/FASE9.md §5), where it carries a weapon of the Armería; no weapon tiers any more.
        arms?.primary ? wearMarks(conditionOf(u), arms.mods) : null,
      ),
      pack ? this.backpack(u.id, pack) : null,
    );
  }

  /** Opens the backpack's swap for `id` on the floor (the notice's «Cambiar»): what to leave to take it. */
  openSwap(unit: string, id: string): void {
    this.packMenu = { unit, swap: id };
    this.shownUnit = unit;
  }

  /**
   * Something found with the backpack full stays on the floor (docs/FASE8.md
   * §C): a notice says so, with «Cambiar» to choose what stays.
   */
  packOffer(text: string, swap: () => void): void {
    clear(this.packOfferEl);
    this.packOfferEl.classList.add('show');
    append(
      this.packOfferEl,
      h('span', {}, text),
      h(
        'button.x-btn.small',
        {
          onclick: () => {
            this.packOfferEl.classList.remove('show');
            swap();
          },
        },
        'Cambiar',
      ),
    );
    window.clearTimeout(this.packOfferTimer);
    this.packOfferTimer = window.setTimeout(() => this.packOfferEl.classList.remove('show'), 7000);
  }

  /**
   * The backpack (docs/FASE2.md): one slot per thing carried, § for
   * suministros, the accessory's symbol, a pill for medicines; then what the
   * whole squad carries. It only counts if it gets home.
   */
  private backpack(unit: string, pack: PackView): HTMLElement {
    // A menu open on another soldier closes when this one is shown.
    if (this.shownUnit !== unit) this.packMenu = null;
    this.shownUnit = unit;
    const menu = this.packMenu;
    const slot = (s: PackSlot | undefined, i: number) => {
      const el = slotEl(s);
      if (!s || !pack.live) return el;
      // Its own soldier's: a click opens what can be done with it.
      const open = !!menu && 'slot' in menu && menu.slot === i;
      return h(
        `button.pack-slot-btn${open ? '.open' : ''}`,
        {
          type: 'button',
          title: `${pack.labels?.[i] ?? ''}: soltar${s.kind === 'weapon' ? ', equipar' : ''}${s.kind === 'item' && wearable(s.item) ? ', ponerse' : ''}${s.kind === 'medicines' ? ', usar' : ''}${pack.near?.length ? ', dar' : ''}`,
          onclick: () => {
            this.packMenu = open ? null : { unit, slot: i };
            this.redrawPack(unit, pack);
          },
        },
        el,
      );
    };
    const slotEl = (s: PackSlot | undefined) => {
      if (!s) return h('span.pack-slot.empty', { title: 'Hueco libre' });
      if (s.kind === 'supplies') return h('span.pack-slot.supplies', { title: `Suministros: +${s.amount} §` }, h('b', {}, `${s.amount}§`));
      if (s.kind === 'medicines') return h('span.pack-slot.medicines', { title: s.amount === 1 ? 'Medicinas' : `Medicinas ×${s.amount}` }, h('span.stat-icon'));
      // The number and a drawn icon, never a glyph: ⚙ comes out as an emoji on Windows.
      if (s.kind === 'food') return h('span.pack-slot.food', { title: `Raciones: +${s.amount}` }, h('b', {}, String(s.amount)), h('span.ico.food'));
      if (s.kind === 'parts') return h('span.pack-slot.parts', { title: `Piezas: +${s.amount}` }, h('b', {}, String(s.amount)), h('span.ico.parts'));
      if (s.kind === 'weapon') return h('span.pack-slot.weapon', { title: `${FOUND_WEAPONS[s.weapon].name}: a la Armería, para un soldado` }, h('span.stat-icon'));
      return h(`span.pack-slot.item.i-${ITEM_ICONS[s.item]}`, { title: `${ITEMS[s.item].name}: ${ITEMS[s.item].description}` }, h('span.stat-icon'));
    };
    const q = pack.squad;
    // Supplies carry their sign («§») in the number; rations and parts an icon after it; the rest an icon before.
    const after = ['food', 'parts'];
    const total = (kind: string, n: number, text: string) =>
      n ? h(`span.pack-total.${kind}`, {}, kind === 'supplies' || after.includes(kind) ? null : h('span.stat-icon'), h('b', {}, text), after.includes(kind) ? h(`span.ico.${kind}`) : null) : null;
    const full = pack.slots.length >= pack.size;
    // What lies on its tile: a click picks it up, or, with the backpack full, asks what to leave for it.
    const floor = pack.live && pack.floor?.length
      ? h(
          'div.pack-floor',
          {},
          h('small', {}, 'EN EL SUELO'),
          ...pack.floor.map((f) =>
            h(
              `button.pack-floor-item.${f.kind}`,
              {
                type: 'button',
                title: full ? `${f.label}: la mochila está llena; elige qué dejar` : `${f.label}: recogerlo (gratis)`,
                onclick: () => {
                  if (!full) {
                    this.packMenu = null;
                    this.handlers.onItem({ action: 'take', id: f.id });
                    return;
                  }
                  this.packMenu = { unit, swap: f.id };
                  this.redrawPack(unit, pack);
                },
              },
              h('b', {}, f.label),
              h('small', {}, full ? 'Cambiar' : 'Coger'),
            ),
          ),
        )
      : null;
    return h(
      'div.unit-pack',
      { title: `Mochila ${pack.slots.length}/${pack.size}. Cuenta si vuelve: lo que lleva un caído se pierde.` },
      this.packMenuEl(unit, pack),
      floor,
      h('span.pack-slots', {}, ...Array.from({ length: pack.size }, (_, i) => slot(pack.slots[i], i))),
      h(
        'span.pack-squad',
        { title: q.text },
        h('small', {}, 'ESCUADRA'),
        q.supplies || q.items || q.medicines || q.food || q.parts || q.weapons
          ? h(
              'span.pack-totals',
              {},
              total('supplies', q.supplies, `${q.supplies}§`),
              total('food', q.food, String(q.food)),
              total('parts', q.parts, String(q.parts)),
              total('weapon', q.weapons, `×${q.weapons}`),
              total('item', q.items, `×${q.items}`),
              total('medicines', q.medicines, `×${q.medicines}`),
            )
          : h('span.pack-none', {}, '—'),
      ),
    );
  }

  /** The backpack drawn again in place, for its menu (the rest of the sheet stays). */
  private redrawPack(unit: string, pack: PackView): void {
    const old = this.unitPanel.querySelector('.unit-pack');
    if (old) old.replaceWith(this.backpack(unit, pack));
  }

  /**
   * The open menu, over the backpack: for a slot, what can be done with it
   * (soltar, free; equipar, usar, dar, an action each); for a thing on the
   * floor with the backpack full, which slot to leave for it.
   */
  private packMenuEl(unit: string, pack: PackView): HTMLElement | null {
    const menu = this.packMenu;
    if (!menu || menu.unit !== unit || !pack.live) return null;
    const close = () => {
      this.packMenu = null;
      this.redrawPack(unit, pack);
    };
    const order = (o: ItemOrder) => {
      this.packMenu = null;
      this.handlers.onItem(o);
    };
    // An order that can't be given now says why in place of its cost.
    const act = (label: string, cost: string, fn: () => void, kind = '', off: string | null = null) =>
      h(`button.pack-act${kind}`, { type: 'button', onclick: fn, disabled: !!off, title: off ?? undefined }, h('span', {}, label), h('small', {}, off ?? cost));
    const items: HTMLElement[] = [];
    let title = '';
    if ('slot' in menu) {
      const s = pack.slots[menu.slot];
      if (!s) return null;
      const i = menu.slot;
      title = pack.labels?.[i] ?? '';
      if (s.kind === 'weapon') items.push(act('Equipar', '1 acción', () => order({ action: 'equip', index: i }), '.main'));
      // A vest is put on there and then (playtest 16/10), one at a time (rules.ts).
      if (s.kind === 'item' && wearable(s.item)) items.push(act('Ponerse', '1 acción', () => order({ action: 'equip', index: i }), '.main', pack.worn?.length ? 'Ya lleva un chaleco puesto.' : null));
      // On the belt, to use there and then (playtest 18/10 #5): grenades, smoke, the stun one, the medkit, ear plugs; what only gives, once.
      if (s.kind === 'item' && beltable(s.item)) {
        const worn = !ITEMS[s.item].charges && !!pack.gear?.includes(s.item);
        items.push(act(BELT_TEXT.button, '1 acción', () => order({ action: 'equip', index: i }), '.main', worn ? 'Ya lleva eso puesto.' : null));
      }
      if (s.kind === 'medicines') {
        items.push(act('Usar', '1 acción', () => order({ action: 'use', index: i }), '.main'));
        for (const n of pack.near ?? []) items.push(act(`Curar a ${n.name}`, '1 acción', () => order({ action: 'use', index: i, to: n.id })));
      }
      for (const n of pack.near ?? []) items.push(act(`Dar a ${n.name}`, '1 acción', () => order({ action: 'give', index: i, to: n.id })));
      items.push(act('Soltar', 'gratis', () => order({ action: 'drop', index: i })));
    } else {
      const f = pack.floor?.find((x) => x.id === menu.swap);
      if (!f) return null;
      title = `Coger ${f.name}: ¿qué dejas?`;
      pack.slots.forEach((_, i) =>
        items.push(
          act(`Dejar ${pack.names?.[i] ?? pack.labels?.[i] ?? ''}`, 'gratis', () => {
            this.packMenu = null;
            this.handlers.onSwap(menu.swap, i);
          }),
        ),
      );
    }
    return h('div.pack-menu', { role: 'menu' }, h('div.pack-menu-head', {}, h('b', {}, title), h('button.pack-menu-x', { type: 'button', title: 'Cerrar', onclick: close }, '✕')), ...items);
  }

  /** The menu open on the bar (Lanzar, Vigilancia, Investigar), and its choices as last drawn. */
  private barMenu: MenuId | null = null;
  private menuOptions: AbilityView[] = [];

  setAbilities(list: AbilityView[]): void {
    clear(this.abilities);
    // A menu whose choices are gone (another soldier, the turn over) closes.
    const menu = list.find((a) => a.id === this.barMenu);
    if (!menu || (menu.options?.length ?? 0) < 2 || !menu.enabled) this.barMenu = null;
    // No explanations on the cards: they live in the card shown on hover, and an empty one gets a stamp.
    for (const a of list) {
      const card = this.abilityButton(a, () => {
        const options = a.options;
        if (!options) return this.handlers.onAbility(a.id);
        if (options.length === 1) return this.handlers.onAbility(options[0]!.id);
        this.toggleBarMenu(a.id as MenuId);
      });
      if (a.id !== this.barMenu) {
        this.abilities.append(card);
        continue;
      }
      // Its choices above it, numbered from 1 (the keys pick them while it is open).
      this.menuOptions = a.options!;
      card.classList.add('menu-open');
      const choices = h(
        'div.ability-menu',
        { role: 'menu', 'aria-label': a.name },
        ...a.options!.map((o, i) =>
          this.abilityButton({ ...o, hotkey: String(i + 1) }, () => {
            this.barMenu = null;
            this.handlers.onAbility(o.id);
          }),
        ),
      );
      this.abilities.append(h('span.ability-slot', {}, card, choices));
    }
  }

  private abilityButton(a: AbilityView, onclick: () => void): HTMLElement {
    return h(
      `button.ability${a.active ? '.active' : ''}${a.free ? '.free' : ''}${a.depleted ? '.depleted' : ''}${a.options && a.options.length > 1 ? '.has-menu' : ''}`,
      {
        disabled: !a.enabled,
        'data-ability': a.icon ?? a.id,
        'aria-label': `${a.name}. ${a.cost}. ${a.description}${a.reason && !a.enabled ? ` ${a.reason}` : ''}`,
        'aria-haspopup': a.options && a.options.length > 1 ? 'menu' : null,
        onclick,
      },
      h('span.key', {}, a.hotkey),
      abilityIcon(a.icon ?? (a.id as AbilityId)),
      h('span.label', {}, a.label),
      a.count !== undefined ? h('span.count', {}, `×${a.count}`) : null,
      a.cooldown ? h('span.cd', {}, String(a.cooldown)) : null,
      a.depleted ? h('span.stamp', { 'aria-label': 'Agotado' }) : null,
      this.abilityCard(a),
    );
  }

  /** Whether a menu of the bar is open: the number keys pick its choices then. */
  get abilityMenuOpen(): boolean {
    return this.barMenu !== null;
  }

  /** Opens `id`'s menu above its button, or closes it if it was open; the screen redraws the bar. */
  toggleBarMenu(id: MenuId): void {
    this.barMenu = this.barMenu === id ? null : id;
    this.handlers.onMenu();
  }

  closeAbilityMenu(): boolean {
    if (!this.barMenu) return false;
    this.barMenu = null;
    this.handlers.onMenu();
    return true;
  }

  /** The open menu's `n`-th choice (from 1), as a click on it. */
  pickMenu(n: number): void {
    const o = this.menuOptions[n - 1];
    if (!o) return;
    this.barMenu = null;
    this.handlers.onAbility(o.id);
  }

  /** What hovering a card tells before anything is pressed: name, cost, limits, what it does and why not now. */
  private abilityCard(a: AbilityView): HTMLElement {
    return h(
      'span.ability-card',
      { 'aria-hidden': 'true' },
      h('span.ac-head', {}, h('b', {}, a.name), a.hotkey ? h('span.ac-key', {}, a.hotkey) : null),
      h('span.ac-cost', {}, [a.cost, a.limits, a.noise].filter(Boolean).join(' · ')),
      a.damage ? h('span.ac-dmg', {}, a.damage) : null,
      h('span.ac-desc', {}, a.description),
      a.reason ? h('span.ac-why', {}, a.reason) : null,
    );
  }

  setTarget(view: TargetView | null): void {
    this.fillTarget(view);
    this.placeTarget(view?.kind === 'self');
  }

  private fillTarget(view: TargetView | null): void {
    clear(this.target);
    this.target.classList.toggle('hidden', !view);
    this.target.classList.toggle('support', view?.kind === 'support');
    this.target.classList.toggle('self', view?.kind === 'self');
    if (!view) return;
    if (view.kind === 'self') {
      append(
        this.target,
        h('div.shot-title', {}, type(view.title)),
        h('div.self-cost', {}, [view.cost, view.limits, view.noise].filter(Boolean).join(' · ')),
        h('div.self-desc', {}, view.effect),
        view.detail ? h('div.effect', {}, view.detail) : null,
        h('div.self-hint', {}, view.locked ? 'Sin el mando solo puedes consultarla' : view.hint ?? 'Otra vez, Espacio o Intro: usar · Esc: cancelar'),
        h(
          'div.shot-actions',
          {},
          view.locked
            ? h('button.fire', { disabled: true, title: 'Tiene el mando tu compañero' }, view.locked)
            : h('button.fire', { onclick: () => this.handlers.onConfirmTarget(), title: 'Espacio o Intro' }, view.action.toUpperCase()),
          h('button.cancel', { onclick: () => this.handlers.onCancel(), title: 'Esc' }, 'Cancelar'),
        ),
      );
      return;
    }
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
      view.locked
        ? h('button.fire', { disabled: true, title: 'Sin el mando solo se puede apuntar: el porcentaje sirve para planear' }, view.locked)
        : h('button.fire', { onclick: () => this.handlers.onConfirmTarget(), title: 'Espacio o Intro' }, view.action.toUpperCase()),
      h('button.cancel', { onclick: () => this.handlers.onCancel(), title: 'Esc' }, 'Cancelar'),
    );
    const noise = view.noise ? h('div.shot-noise', { title: 'Todo enemigo a esa distancia lo oye y va a mirar' }, view.noise) : null;
    if (view.kind === 'support') {
      append(this.target, h('div.shot-title', {}, type(view.title)), head, view.damage ? damageBlock(view.damage) : null, h('div.effect', {}, view.effect), noise, actions);
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
        // --p: the odds, 0–100, for the theme to draw as an arc round the dial.
        h('div.big', { style: { '--p': String(view.hit) } }, `${view.hit}%`, h('small', {}, 'IMPACTO')),
        h('div.big.crit', {}, `${view.crit}%`, h('small', {}, 'CRÍTICO')),
      ),
      damageBlock(view.damage),
      h('div.mods', {}, ...view.mods.map(row)),
      view.critMods.length ? h('div.mods.crit', {}, ...view.critMods.map(row)) : null,
      view.graze ? h('div.graze', {}, `Rozadura posible: ${view.graze}%${view.damage.graze ? ` (daño ${view.damage.graze})` : ''}`) : null,
      view.note ? h('div.effect', {}, view.note) : null,
      noise,
      actions,
    );
  }

  /**
   * Where the target panel goes. Aiming, it waits on the right; a self ability
   * armed by its first press (Vigilancia) shows its confirmation right over the
   * card that was pressed (playtest: «se pone muy lejos del botón»), centred on
   * it but never past the screen's edge.
   */
  private placeTarget(armed: boolean): void {
    const card = armed ? this.abilities.querySelector<HTMLElement>('.ability.active') : null;
    this.target.classList.toggle('anchored', !!card);
    if (!card) {
      this.target.style.left = '';
      if (this.target.previousElementSibling !== this.abilities) this.abilities.after(this.target);
      return;
    }
    if (this.target.parentElement !== this.abilities) this.abilities.append(this.target);
    // Offsets are in the bar's own units; the screen's edges are not (the interface size zooms the bar).
    const bar = this.abilities.getBoundingClientRect();
    const scale = bar.width / (this.abilities.offsetWidth || 1) || 1;
    const width = this.target.offsetWidth;
    const left = card.offsetLeft + card.offsetWidth / 2 - width / 2;
    const min = (EDGE - bar.left) / scale;
    const max = (window.innerWidth - EDGE - bar.left) / scale - width;
    this.target.style.left = `${Math.round(Math.max(min, Math.min(max, left)))}px`;
  }

  /** The menu, a modal or the mission's briefing is up: the keys are theirs. */
  get covered(): boolean {
    return this.menuOpen || this.modal.classList.contains('show') || this.introOpen;
  }

  /** Closes the mission's briefing, when it is up; `quiet`, without its click. */
  private introClose: ((quiet?: boolean) => void) | null = null;

  get introOpen(): boolean {
    return this.introClose !== null;
  }

  closeIntro(quiet = false): void {
    this.introClose?.(quiet);
  }

  /**
   * `waiting`: the partner has the command (`waitingFor` names them).
   * `confirm`: pressed once with soldiers still able to act (`waitingFor` says how many).
   */
  setEndTurn(mode: 'act' | 'ready' | 'waiting' | 'disabled' | 'confirm' | 'enemy', waitingFor: string | null): void {
    this.endTurn.disabled = mode === 'disabled' || mode === 'waiting' || mode === 'enemy';
    this.endTurn.classList.toggle('is-ready', mode === 'ready');
    this.endTurn.classList.toggle('is-waiting', mode === 'waiting');
    this.endTurn.classList.toggle('is-confirm', mode === 'confirm');
    clear(this.endTurn);
    if (mode === 'confirm') {
      this.endTurn.append(h('span', {}, '¿TERMINAR?'), h('small', {}, waitingFor ?? 'Pulsa otra vez'));
    } else if (mode === 'ready') {
      this.endTurn.append(h('span', {}, 'LISTO ✓'), h('small', {}, waitingFor ? `Esperando a ${waitingFor}` : 'Pulsa para cancelar'));
    } else if (mode === 'waiting') {
      this.endTurn.append(h('span', {}, `MANDO: ${(waitingFor ?? '').toUpperCase()}`), h('small', {}, 'Puedes planear: clic en una casilla'));
    } else if (mode === 'enemy') {
      // The enemy turn plays (docs/UX-RITMO.md): the space bar held shows it in «Rápida».
      this.endTurn.append(h('span', {}, 'TURNO ENEMIGO'), h('small', {}, 'Espacio: más rápido'));
    } else {
      this.endTurn.append(h('span', {}, 'TERMINAR TURNO'), h('small', {}, 'Retroceso'));
    }
  }

  /** Whether the camera goes to the soldier whose turn it is (game.ts turnCamera). */
  get followsTurn(): boolean {
    return this.followTurn;
  }

  private drawFollow(): void {
    clear(this.followBtn);
    this.followBtn.setAttribute('aria-pressed', String(this.followTurn));
    this.followBtn.classList.toggle('on', this.followTurn);
    this.followBtn.append(h('span', {}, 'Cámara al turno'), h('small', {}, this.followTurn ? 'Sí' : 'No'));
  }

  /** The "pass the command" button: only for the holder, while playing with a partner. */
  setCommand(view: CommandView | null): void {
    this.passBtn.classList.toggle('hidden', !view?.mine);
    this.passBtn.disabled = !view?.canPass;
    this.watchBtn.classList.toggle('hidden', !view || view.mine);
    this.watchWho.textContent = view && !view.mine ? `VER A ${view.holder.toUpperCase()}` : '';
    this.root.classList.toggle('has-command', !!view?.mine);
    this.root.classList.toggle('waiting-command', !!view && !view.mine);
  }

  /** `detail`: a line under the stamp saying why (who saw the squad…); it stays longer, to be read. */
  banner(text: string, kind: 'xcom' | 'alien' | 'warn' = 'xcom', detail?: string): void {
    // The banner is a rubber stamp coming down.
    uiSound('stamp', { gain: 0.55 });
    clear(this.bannerEl);
    append(this.bannerEl, text, detail ? h('small.banner-detail', {}, detail) : null);
    this.bannerEl.className = `banner show ${kind}`;
    window.clearTimeout(this.bannerTimer);
    this.bannerTimer = window.setTimeout(() => this.bannerEl.classList.remove('show'), detail ? 3600 : 1500);
  }

  /**
   * Arrival briefing: the operation, the area (`lines`: map, night, turns),
   * the objective in its own line and, when there is one, a line of the
   * operation's briefing; then it fades away. A longer card stays longer.
   * It stays until the player closes it (playtest 14/10 #3: it went before
   * it could be read): «Empezar», a click anywhere, Enter, Space or Esc
   * (game.ts). Resolves when it is closed.
   */
  intro(kicker: string, title: string, lines: string[], objective?: string, briefing?: string): Promise<void> {
    this.introClose?.();
    const close = () => this.introClose?.();
    // A click anywhere closes it; nothing under it is played meanwhile.
    const catcher = h('div.intro-catcher', { onpointerdown: close });
    const el = h(
      'div.mission-intro.open',
      { onclick: close },
      kicker ? h('small', {}, kicker) : null,
      h('h1', {}, type(title)),
      lines.length ? h('p.intro-meta', {}, lines.join(' · ')) : null,
      objective ? h('p.intro-objective', {}, h('b', {}, 'Objetivo: '), objective) : null,
      briefing ? h('p.intro-briefing', {}, briefing) : null,
      // A finger has no Intro key (docs/FASE11.md §3).
      h('div.intro-foot', {}, h('button.fire.intro-go', { type: 'button' }, 'EMPEZAR'), h('small', {}, 'clic o Intro')),
    );
    this.root.append(catcher, el);
    return new Promise((resolve) => {
      this.introClose = (quiet = false) => {
        this.introClose = null;
        if (!quiet) uiSound('confirm');
        catcher.remove();
        el.classList.add('out');
        window.setTimeout(() => el.remove(), 800);
        resolve();
      };
    });
  }

  /**
   * The loading veil (UX-REVISION-3 #1): while the battle's models come in
   * and its first frames are drawn, the operation's name over a running bar
   * instead of a frozen black screen. The bar moves on the compositor, so it
   * keeps going while the page is busy. Returns what lifts it.
   */
  veil(kicker: string, title: string, place: string): () => void {
    const el = h(
      'div.load-veil',
      { role: 'status' },
      kicker ? h('small', {}, kicker) : null,
      h('h1', {}, title),
      h('span.load-bar', { 'aria-hidden': 'true' }, h('i')),
      place ? h('p', {}, place) : null,
    );
    this.root.append(el);
    return () => {
      el.classList.add('out');
      window.setTimeout(() => el.remove(), 450);
    };
  }

  /** Shows (or, with null, hides) the partner's key moment: its text in their colour and «Ver (F)». */
  notice(text: string | null, color = '', onSee?: () => void): void {
    clear(this.noticeEl);
    this.noticeEl.classList.toggle('show', !!text);
    if (!text) return;
    sound('radio.static', { gain: 0.6 });
    this.noticeEl.style.setProperty('--accent', color);
    // The partner's colour goes on the tape (styles.css): as text on paper, orange and light blue read poorly.
    append(this.noticeEl, h('span', {}, text), h('button.x-btn.small', { onclick: () => onSee?.() }, 'Ver (F)'));
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
    if (open !== this.menu.classList.contains('show')) uiSound('paper');
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
        this.abandonEntry(),
        // Closing the game, in the desktop app only (playtest 17/10).
        canQuit() ? h('button.menu-entry', { onclick: () => (this.toggleMenu(false), this.confirmQuit()) }, QUIT_BUTTON) : null,
      );
    } else if (page === 'controls') {
      append(
        card,
        h('h2', {}, 'Controles'),
        h('dl.controls', {}, ...CONTROLS.flatMap(([key, what]) => [h('dt', {}, key), h('dd', {}, what)])),
        // How the light works at night (Lore's, #50), here too for whenever it is wanted.
        h('h3.controls-night', {}, NIGHT_TEXT.label),
        h('ul.night-rules', {}, ...NIGHT_TEXT.rules.map((line) => h('li', {}, line))),
        back,
      );
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
      append(card, h('h2', {}, 'Ajustes'), sound, soundSettings(() => this.renderMenu('settings')), graphicsSettings(() => this.renderMenu('settings')), back);
    }
    this.menu.append(card);
  }

  get menuOpen(): boolean {
    return this.menu.classList.contains('show');
  }

  /** The stamp, one short line (`line`) and the way on: nothing more (playtest: «devuelve mucho texto»). */
  /** `siege`: its «again» is another siege on the same base, not a new map. */
  showOutcome(outcome: Outcome | null, line = '', campaign = false, siege = false): void {
    // The result is stamped on the report as it appears (not again on a reconnect).
    if (outcome && !this.outcome.classList.contains('show')) uiSound('stamp', { delay: 0.25 });
    if (!outcome) this.outcome.classList.remove('leaving');
    clear(this.outcome);
    this.outcome.classList.toggle('show', !!outcome);
    if (!outcome) return;
    this.outcome.append(
      h(
        `div.outcome-card.${outcome}`,
        {},
        h('h1', {}, outcome === 'victory' ? 'MISIÓN CUMPLIDA' : 'MISIÓN FRACASADA'),
        // In a campaign the numbers are in the hangar's debrief, one screen later: here only the result.
        line ? h('p.outcome-line', {}, line) : null,
        campaign
          ? h('div.outcome-actions', {}, h('button.fire', { onclick: () => this.returnToBase() }, 'VOLVER A LA BASE'))
          : h(
              'div.outcome-actions',
              {},
              h('button.fire', { onclick: () => this.handlers.onNewMission() }, siege ? 'OTRO ASEDIO' : 'OTRA IGUAL (MAPA NUEVO)'),
              h('button.secondary', { onclick: () => this.handlers.onNewMission(randomKind(), randomMap()) }, 'MISIÓN AL AZAR'),
              h('button.secondary', { onclick: () => this.handlers.onBackToMenu(), title: 'Los dos volvéis al menú principal' }, 'VOLVER AL MENÚ'),
            ),
      ),
    );
    // Drawn again on the way back (a reconnection): it still says so.
    if (campaign && this.outcome.classList.contains('leaving')) this.returning();
  }

  /**
   * «Volver a la base» (playtest, Ana: «tarda mucho, no sé si se espera a
   * Arturo»): the server takes both players at once, then building the base
   * holds the screen a moment. The report says so straight away, and the order
   * only goes once that is on screen: sent at once, the build could freeze the
   * screen before it is painted.
   */
  private returnToBase(): void {
    if (this.outcome.classList.contains('leaving')) return;
    this.returning();
    // After the next paint; a hidden page paints nothing, so a timer sends it anyway.
    let sent = false;
    const send = () => {
      if (sent) return;
      sent = true;
      this.handlers.onReturnToBase();
    };
    requestAnimationFrame(() => window.setTimeout(send, 0));
    window.setTimeout(send, 150);
  }

  /** The report on its way to the Bastión: the button off and saying so, the screen dimmed. */
  returning(): void {
    this.outcome.classList.add('leaving');
    const button = this.outcome.querySelector<HTMLButtonElement>('.outcome-actions button.fire');
    if (!button) return;
    button.disabled = true;
    button.textContent = 'VOLVIENDO AL BASTIÓN…';
  }

  /** The menu's «Abandonar la misión»: off while this player's call waits for the partner. */
  private abandonEntry(): HTMLElement {
    const { partner, waiting } = this.handlers.abandonState();
    if (waiting) return h('button.menu-entry.danger', { disabled: true, title: 'Contesta en su barra' }, evacWaiting(partner ?? 'tu compañero'));
    return h(
      'button.menu-entry.danger',
      { onclick: () => (this.toggleMenu(false), this.confirmAbandon()), title: partner ? `Se lo pregunta a ${partner}` : this.paired ? 'Termina la misión para los dos' : 'Termina la misión' },
      ABANDON_TEXT.button,
    );
  }

  /** What abandoning costs, before it is asked (with the partner there, it waits for a yes on their bar). */
  private confirmAbandon(): void {
    uiSound('paper');
    clear(this.modal);
    this.modal.classList.add('show');
    const close = () => this.modal.classList.remove('show');
    const { partner } = this.handlers.abandonState();
    this.modal.append(
      h(
        'div.modal-card.panel',
        {},
        h('h2', {}, `¿${ABANDON_TEXT.button}?`),
        h('p', {}, ABANDON_TEXT.stakes),
        partner ? h('p', {}, `${partner} tiene que aceptarlo.`) : null,
        h(
          'div.modal-actions',
          {},
          h('button.danger', { onclick: () => (close(), this.handlers.onAbandon()) }, ABANDON_TEXT.accept.toUpperCase()),
          h('button.fire', { onclick: close }, ABANDON_TEXT.refuse.toUpperCase()),
        ),
      ),
    );
  }

  /**
   * The first night (playtest 17/10 #50: «la luz no está muy explicada»): a
   * card under the turn bar with how the light works, until it is closed. Its
   * lines stay in the «De noche» tag and the controls.
   */
  nightCard(title: string, lines: readonly string[]): void {
    this.root.querySelector('.night-card')?.remove();
    const card = h(
      'div.night-card.panel',
      { role: 'note' },
      h('b', {}, title),
      h('ul', {}, ...lines.map((line) => h('li', {}, line))),
      h('button.fire', { onclick: () => card.remove() }, 'ENTENDIDO'),
    );
    this.root.append(card);
  }

  /** Before closing the game: it is saved; hosting it, the partner is left without it (ui/quit.ts). */
  private confirmQuit(): void {
    uiSound('paper');
    clear(this.modal);
    this.modal.classList.add('show');
    const close = () => this.modal.classList.remove('show');
    const q = quitQuestion(this.partnerOnline, true);
    this.modal.append(
      h(
        'div.modal-card.panel',
        {},
        h('h2', {}, q.title),
        h('p', {}, q.text),
        q.warning ? h('p.quit-warning', {}, q.warning) : null,
        h(
          'div.modal-actions',
          {},
          h('button.danger', { onclick: () => (close(), quitGame()) }, q.yes.toUpperCase()),
          h('button.fire', { onclick: close }, q.no.toUpperCase()),
        ),
      ),
    );
  }

  /** The Bastion's file on an enemy (right click): what it is, what it does, how to beat it. */
  enemyFile(name: string, stats: string, file: { what: string; does: string; tip: string }): void {
    clear(this.modal);
    this.modal.classList.add('show');
    const close = () => this.modal.classList.remove('show');
    this.modal.append(
      h(
        'div.modal-card.panel.enemy-file',
        {},
        h('small.enemy-file-kicker', {}, 'FICHA DE ATALAYA'),
        h('h2', {}, name),
        h('p.enemy-file-stats', {}, stats),
        h('p', {}, file.what),
        h('p', {}, file.does),
        h('p.enemy-file-tip', {}, file.tip),
        h('div.modal-actions', {}, h('button.fire', { onclick: close }, 'CERRAR')),
      ),
    );
  }

  /**
   * A soldier's sheet over the battle (playtest 18/10 #23): its figures, what
   * it carries and its skills on the left, and its board (strategy's
   * treePanel, not editable) beside them in a campaign; nothing to spend, no
   * command passed.
   */
  soldierSheet(view: SoldierSheetView, board: HTMLElement | null): void {
    clear(this.modal);
    this.modal.classList.add('show');
    const close = () => this.modal.classList.remove('show');
    const rows = (list: [string, string, string?][]) => h('dl.sheet-rows', {}, ...list.flatMap(([k, v, tip]) => [h('dt', { title: tip ?? '' }, k), h('dd', { title: tip ?? '' }, v)]));
    this.modal.append(
      h(
        `div.modal-card.panel.soldier-sheet${board ? '.with-board' : ''}`,
        {},
        h(
          'div.sheet-side',
          {},
          h('small.enemy-file-kicker', {}, 'HOJA DEL SOLDADO'),
          h('h2', {}, view.name),
          h('p.sheet-rank', {}, view.rank, view.whose ? h('em.whose', { style: { '--accent': view.whose.color } }, ` DE ${view.whose.name.toUpperCase()}`) : null),
          rows(view.stats),
          h('h3', {}, 'Lleva'),
          rows(view.carries),
          view.skills.length && !board ? h('h3', {}, 'Habilidades') : null,
          view.skills.length && !board ? h('ul.sheet-skills', {}, ...view.skills.map((name) => h('li', {}, name))) : null,
          board ? null : h('div.modal-actions', {}, h('button.fire', { onclick: close }, 'CERRAR')),
        ),
        board,
      ),
    );
  }

  /** Closes the window over the battle (a sheet, an enemy's file); whether one was up (Esc closes it first). */
  closeModal(): boolean {
    const open = this.modal.classList.contains('show');
    this.modal.classList.remove('show');
    return open;
  }

  setConnection(status: NetStatus): void {
    this.connection.textContent = status === 'open' ? '' : status === 'connecting' ? 'Conectando…' : 'Conexión perdida · reconectando…';
    this.connection.classList.toggle('show', status !== 'open');
  }

  destroy(): void {
    this.root.remove();
  }
}
