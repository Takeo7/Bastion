import {
  fallenPackAction,
  fallenPackMark,
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
import { marked } from './marks';
import { deadlineEl, deadlineOf } from './deadline';
import { turnClockOf } from '../turnClock';

/**
 * What the action bar holds: the soldier's abilities, and the evacuation's own
 * (playtest 15/10 #10): «Evacuar» from La Lata, and the partner's answer to it;
 * and the same for abandoning the mission (16/10): the wait, and the answer.
 */
export type BarActionId = AbilityId | 'evac' | 'evacAccept' | 'evacRefuse' | 'abandon' | 'abandonAccept' | 'abandonRefuse' | MenuId;

/** A call of the partner's waiting for this player's answer (hud.showCall): to leave in La Lata, or to abandon. */
export interface CallView {
  kind: 'evac' | 'abandon';
  kicker: string;
  title: string;
  text: string;
  /** Under the time left: what happens when it runs out. */
  clockNote: string;
  accept: { label: string; act: () => void };
  refuse: { label: string; act: () => void };
}

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
  /** What to do first, on the button itself in place of its name, for a reason that wants seeing without the card (#24). */
  note?: string;
  /** The hover card: name, cost («1 acción», «Acaba el turno»…), limits and what it does. */
  name: string;
  cost: string;
  /** Cooldown and uses left («Disponible en 2 turnos · Usos: 1»). */
  limits?: string;
  /** What it takes off, for what does damage («Daño 4–6 · crítico +2»). */
  damage?: string;
  /** Its figures, each a name and its value, as the card lists them alike (#18): actions, reach, damage, noise… */
  stats?: [string, string][];
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
  /** Disparar's gun (Arturo, 23/10 #7): its picture on the key (synty-icons.css data-weapon). */
  weapon?: string;
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
  /** «Lo mata», «Un crítico lo mata». */
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
  /** `from`: the fallen soldier it was carried by (playtest 18/10 #49), by name. */
  floor?: { id: string; label: string; name: string; kind: PackSlot['kind']; from?: string }[];
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
  /** Back to the main menu (playtest 19/10 #7): who would go too, and whether it is a campaign's (left paused). */
  leaveState(): { partner: string | null; campaign: boolean };
  onPassCommand(): void;
  /** A menu of the bar opened or closed: draw the bar again. */
  onMenu(): void;
  /** The camera to what the partner holding the command is doing. */
  onWatchPartner(): void;
  /** The camera to the soldier whose turn it is, picked (playtest 19/10 #15). */
  onGoToSoldier(): void;
  /** Toggles the sound; returns whether it is now on. */
  onToggleSound(): boolean;
  soundOn(): boolean;
  /** One floor up (1) or down (-1) (docs/FASE6.md). */
  onFloor(dir: 1 | -1): void;
  /** An order on the backpack or the floor (docs/FASE8.md §C). */
  onItem(order: ItemOrder): void;
  /** Take these things from the floor one after another, while they fit (a fallen soldier's rucksack: #49). */
  onTakeAll?(ids: string[]): void;
  /** Leave slot `drop` on the floor and pick up `take` from it: the swap when the backpack is full. */
  onSwap(take: string, drop: number): void;
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
  ['1–9', 'Usar una habilidad; si necesita objetivo, elegirlo y pulsar otra vez'],
  ['Espacio · Intro', 'Usar la habilidad elegida; sin ninguna, disparar'],
  ['C', 'Ceder el mando'],
  ['Retroceso', 'Terminar turno'],
  // Signalling (playtest 19/10 #14): Mayús+clic, with no button; G and the wheel's click still do.
  ['Mayús + clic · G · clic con la rueda', SETTINGS_TEXT.pingControls],
  ['H · clic en su nombre', 'Hoja del soldado: tablero, estadísticas y lo que lleva'],
  ['Q / E', 'Girar la cámara'],
  ['Rueda', 'Acercar o alejar'],
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
  /**
   * The objective's card on the right, under «Menú» (playtest 18/10 #52: «siempre en pantalla
   * sin estorbar»): the arrival card shrinks into it (dock) and it stays the whole mission, with the mission's
   * name, its objective as it goes and what counts (turns, waves, the horde). Hidden while that card is open.
   */
  /** The objective's card on the right, where the arrival card shrinks to; a click opens that card again (Ana, 20/10 #32). */
  private readonly goalDock = h('div.goal-dock.panel', { onclick: () => this.reopenIntro(), title: 'Ver la misión' }, this.mission);
  /** What the arrival card said, to open it again from the objective's card. */
  private introArgs: Parameters<Hud['intro']> | null = null;
  /** Real time (docs/TIEMPO-REAL.md): the big «PAUSA» while the clock is stopped. */
  private readonly rtPauseEl = h('div.rt-pause.hidden', { role: 'status' }, h('b', {}, 'PAUSA'), h('small', {}, 'Espacio para seguir'));
  /** Real time: the slow motion while someone picks an ability's target (rtSlow), and what to do if it is this player. */
  private readonly rtSlowEl = h('div.rt-pause.rt-slow.hidden', { role: 'status' }, h('b', {}, 'CÁMARA LENTA'), h('small'));
  private rtKey = '';
  /** What the target panel last showed (setTarget); null before the first, which must hide it (Ana, 20/10 #34: an empty frame stood on the screen). */
  private targetKey: string | null = null;
  /** The right-hand column (Menú, the objective, the log): the aim's panel keeps below it (#20: «se solapan»). */
  private sideEl!: HTMLElement;
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
  private readonly passBtn = h('button.pass-command', { title: 'El otro jugador da las órdenes hasta que te lo devuelva (C)' });
  /** With the partner holding the command: the camera to what they are doing (playtest 16/10 #8). */
  private readonly watchBtn = h('button.pass-command.watch-partner', { title: 'Lleva la cámara a lo que está haciendo el otro jugador (F)' });
  /** The camera to the soldier whose turn it is, or not (playtest 17/10 #47): a switch beside «Ver», kept per browser. */
  private followTurn = readFollowTurn();
  private readonly followBtn = h('button.pass-command.follow-turn', { type: 'button', title: SETTINGS_TEXT.cameraFollowAbout });
  /** To the soldier whose turn it is (playtest 19/10 #15: it had gone), while this player gives the orders. */
  private readonly goBtn = h('button.pass-command.go-soldier.hidden', { type: 'button', title: 'Lleva la cámara al soldado al que le toca y lo elige (F)' });
  private readonly goWho = h('span');
  private readonly watchWho = h('span');
  private readonly logEl = h('div.log');
  private readonly logLines: HTMLElement[] = [];
  private logOpen = false;
  private readonly menu = h('div.game-menu');
  private readonly bannerEl = h('div.banner', { onclick: () => this.releaseBanner() });
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
  /** The partner's call waiting for this player's answer, in the middle (callCard), and what it was drawn for. */
  private callEl: HTMLElement | null = null;
  private callKey = '';


  constructor(container: HTMLElement, private readonly handlers: HudHandlers) {
    this.endTurn.addEventListener('click', () => handlers.onEndTurn());
    this.passBtn.addEventListener('click', () => handlers.onPassCommand());
    this.passBtn.append(h('span', {}, 'CEDER EL MANDO'), h('small', {}, 'C'));
    this.watchBtn.addEventListener('click', () => handlers.onWatchPartner());
    // A menu of the bar closes with a press anywhere else.
    document.addEventListener('pointerdown', this.onPressAnywhere);
    this.watchBtn.append(this.watchWho, h('small', {}, 'F'));
    this.goBtn.addEventListener('click', () => handlers.onGoToSoldier());
    this.goBtn.append(this.goWho, h('small', {}, 'F'));
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
    this.root.append(
      this.frame,
      h('div.top', {}, this.turn, this.floor, this.players),
      this.squad,
      this.unitPanel,
      this.abilities,
      this.target,
      h('div.turn-actions', {}, this.followBtn, this.goBtn, this.passBtn, this.watchBtn, this.endClock, this.endTurn),
      (this.sideEl = h('div.side', {}, h('div.side-row', {}, menuButton), this.goalDock, this.logEl)),
      this.bannerEl,
      this.rtPauseEl,
      this.rtSlowEl,
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
   * Real time (docs/TIEMPO-REAL.md), in place of the turn (null: by turns): the mission's clock on the strip (its
   * figures by tickRealtime), whether it is stopped, and the big «PAUSA»; what belongs to turns is put away (styles.css).
   */
  setRealtime(view: { paused: boolean; slow: boolean; armed: 'enemy' | 'ally' | 'tile' | null; partner: string | null; concealed: boolean; night: { label: string; rules: string } | null } | null): void {
    this.root.classList.toggle('realtime', !!view);
    this.rtPauseEl.classList.toggle('hidden', !view?.paused);
    // Slow (not stopped): the notice, and with an ability armed here, how to aim it or let it go.
    this.rtSlowEl.classList.toggle('hidden', !view?.slow || view.paused);
    // With an ability armed here, the keys say what they do now (Space uses it, also with the clock stopped).
    const armed =
      view?.armed === 'enemy'
        ? 'Tab: otro blanco · Espacio o Intro: usar · Esc: cancelar'
        : view?.armed === 'ally'
          ? 'Tab: otro soldado · Espacio o Intro: usar · Esc: cancelar'
          : view?.armed === 'tile'
            ? 'Elige una casilla · Esc: cancelar'
            : null;
    this.rtPauseEl.lastElementChild!.textContent = armed ?? 'Espacio para seguir';
    this.rtSlowEl.lastElementChild!.textContent = armed ?? `${view?.partner ?? 'El otro jugador'} elige a quién`;
    if (!view) return;
    const key = JSON.stringify(view);
    if (key === this.rtKey) return;
    this.rtKey = key;
    this.turnKey = '';
    for (const k of ['mine', 'partner', 'enemy'] as const) this.root.classList.toggle(`turn-${k}`, false);
    this.frame.className = 'turn-frame';
    clear(this.turn);
    append(
      this.turn,
      h(`span.turn-label.rt-clock${view.paused ? '.paused' : view.slow ? '.slow' : ''}`, { title: 'Tiempo de misión' }, '00:00'),
      h(`span.conceal.${view.concealed ? 'on' : 'off'}`, {}, view.concealed ? 'OCULTOS' : 'DETECTADOS'),
      view.night ? h('span.conceal.night', { title: view.night.rules }, view.night.label.toUpperCase()) : null,
    );
  }

  /** Every frame in real time: the clock's figures (mm:ss) and the bars of the squad's aims and reloads. */
  tickRealtime(clock: number): void {
    const label = this.turn.querySelector<HTMLElement>('.rt-clock');
    const secs = Math.floor(clock / 1000);
    const text = `${String(Math.floor(secs / 60)).padStart(2, '0')}:${String(secs % 60).padStart(2, '0')}`;
    if (label && label.textContent !== text) label.textContent = text;
    for (const bar of this.squad.querySelectorAll<HTMLElement>('.rt-bar')) {
      const from = Number(bar.dataset.from);
      const until = Number(bar.dataset.until);
      const k = until > from ? Math.min(1, Math.max(0, (clock - from) / (until - from))) : 1;
      (bar.firstElementChild as HTMLElement | null)?.style.setProperty('width', `${Math.round(k * 100)}%`);
    }
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

  /**
   * The turn clocks, over «Terminar turno» (Arturo, 23/10: not under the names, and to be read at a glance): this
   * player's a thick bar in their colour, the partner's a thin one under it (tickClocks).
   */
  private readonly endClock = h('div.end-clock', { 'aria-hidden': 'true' }, h('span.turn-clock.mine', {}, h('i')), h('span.turn-clock.theirs', {}, h('i')));
  private clockSlots: { mine: Slot; theirs: Slot | null } = { mine: 0, theirs: null };

  setPlayers(players: PlayerInfo[], ready: [boolean, boolean], mySlot: Slot, busy: Set<Slot>, commander: Slot | null = null): void {
    clear(this.players);
    const partner = players.find((p) => p.slot !== mySlot);
    this.clockSlots = { mine: mySlot, theirs: partner?.slot ?? null };
    const [mineEl, theirsEl] = [...this.endClock.children] as HTMLElement[];
    mineEl!.style.setProperty('--accent', players.find((p) => p.slot === mySlot)?.color ?? 'var(--t-accent)');
    if (partner) theirsEl!.style.setProperty('--accent', partner.color);
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
    this.tickClocks();
  }

  /**
   * The turn clocks, every frame (game.ts frame): how much of each player's time is left, as the bar's width; striped
   * and moving while it runs, still while the screen shows what happened; amber when little is left, red at the end,
   * breathing slowly. Gone outside the squad's turn.
   */
  tickClocks(): void {
    const [mineEl, theirsEl] = [...this.endClock.children] as HTMLElement[];
    let any = false;
    for (const [slot, el] of [[this.clockSlots.mine, mineEl!], [this.clockSlots.theirs, theirsEl!]] as const) {
      const c = slot === null ? null : turnClockOf(slot);
      el.classList.toggle('none', !c);
      if (c) any = true;
      if (!c) continue;
      (el.firstElementChild as HTMLElement).style.width = `${(Math.max(0, Math.min(1, c.left)) * 100).toFixed(1)}%`;
      el.classList.toggle('running', c.running);
      el.classList.toggle('low', c.left < 0.25);
      el.classList.toggle('out', c.left < 0.1);
    }
    this.endClock.classList.toggle('none', !any);
  }

  /**
   * Whether something the game put up covers the field to be read (the arrival card, «TU TURNO» held, the partner's
   * question, a card of how something works): the turn clock doesn't run meanwhile (game.ts reportIdle). A sheet the
   * player opened doesn't count.
   */
  get covering(): boolean {
    return !!this.introClose || !!this.callEl || this.bannerEl.classList.contains('held') || (this.modal.classList.contains('show') && !this.modal.querySelector('.soldier-sheet, .enemy-file'));
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
    /** Real time: what each one is doing, in place of its actions, with the bar of an aim or a reload. */
    rt?: (u: Unit) => { text: string; title?: string; from?: number; until?: number } | null,
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
          title: u.automaton ? 'Va sola: nadie le da órdenes.' : !theirs ? 'Seleccionar' : mine ? 'Soldado del otro jugador: lo mandas tú mientras no está' : 'Soldado del otro jugador',
        },
        // Fase 9: soldiers have no class any more; only who isn't one (the VIP) says what it is.
        h('div.card-name', {}, u.template === 'rookie' ? null : h('b.cls', {}, TEMPLATES[u.template].short), u.template === 'rookie' ? null : ' ', u.nickname ? `«${u.nickname}»` : u.name),
        h(
          'div.card-row',
          {},
          h('span.hp', {}, ...Array.from({ length: u.maxHp }, (_, i) => h(`i${i < u.hp ? '.on' : ''}`))),
          // In real time, what it is doing (no actions to count), and how far along an aim or a reload is.
          // A turret has no actions of its own: its rounds instead, against what a full load holds.
          rt
            ? this.rtDoingEl(rt(u))
            : u.automaton
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

  /** A card's line in real time: what it does, and (an aim, a reload) a bar that fills to when it ends (tickRealtime). */
  private rtDoingEl(doing: { text: string; title?: string; from?: number; until?: number } | null): HTMLElement | null {
    if (!doing) return null;
    return h(
      'span.rt-doing',
      { title: doing.title },
      h('span', {}, doing.text),
      doing.until !== undefined ? h('i.rt-bar', { 'data-from': String(doing.from ?? 0), 'data-until': String(doing.until) }, h('b')) : null,
    );
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
          // A fallen soldier's rucksack (#49): its name, and one order that takes what fits.
          ...[...new Set(pack.floor.map((f) => f.from).filter((x): x is string => !!x))].map((who) => {
            const ids = pack.floor!.filter((f) => f.from === who).map((f) => f.id);
            const room = pack.size - pack.slots.length;
            return h(
              'div.pack-fallen',
              {},
              h('b', {}, fallenPackMark(who)),
              h(
                'button.x-btn.small',
                {
                  type: 'button',
                  disabled: room <= 0,
                  title: room <= 0 ? 'La mochila está llena: deja algo antes' : `Coge lo que quepa: ${Math.min(room, ids.length)} de ${ids.length}`,
                  onclick: () => {
                    this.packMenu = null;
                    if (this.handlers.onTakeAll) this.handlers.onTakeAll(ids);
                    else this.handlers.onItem({ action: 'take', id: ids[0]! });
                  },
                },
                fallenPackAction(who),
              ),
            );
          }),
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
      `button.ability${a.active ? '.active' : ''}${a.free ? '.free' : ''}${a.depleted ? '.depleted' : ''}${a.options && a.options.length > 1 ? '.has-menu' : ''}${a.note && !a.enabled ? '.noted' : ''}`,
      {
        disabled: !a.enabled,
        'data-ability': a.icon ?? a.id,
        'data-weapon': a.weapon ?? null,
        'aria-label': `${a.name}. ${a.cost}. ${a.description}${a.reason && !a.enabled ? ` ${a.reason}` : ''}`,
        'aria-haspopup': a.options && a.options.length > 1 ? 'menu' : null,
        onclick,
      },
      // The icon alone (Ana, 22/10 #35): its name and its key are on the card on hover; the number keys still work.
      abilityIcon(a.icon ?? (a.id as AbilityId)),
      // What to do first (20/10 #24: «Sin balas: recarga»), on a tag over the key, read without the card.
      a.note && !a.enabled ? h('span.label', {}, a.note) : null,
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
      // Its figures, all alike (playtest 19/10 #18): a name and its value a line, in two aligned columns.
      a.stats?.length
        ? h('span.ac-stats', {}, ...a.stats.flatMap(([k, v]) => [h('span.ac-k', {}, k), h('span.ac-v', {}, v)]))
        : [a.cost, a.limits, a.noise].some(Boolean)
          ? h('span.ac-cost', {}, [a.cost, a.limits, a.noise].filter(Boolean).join(' · '))
          : null,
      !a.stats?.length && a.damage ? h('span.ac-dmg', {}, a.damage) : null,
      h('span.ac-desc', {}, a.description),
      a.reason ? h('span.ac-why', {}, a.reason) : null,
    );
  }

  setTarget(view: TargetView | null): void {
    // Real time asks for it several times a second: built again only when it says something new (a click on its
    // button, between press and release, was lost to a rebuild).
    const key = view ? JSON.stringify(view) : '';
    if (key !== this.targetKey) {
      this.targetKey = key;
      this.fillTarget(view);
    }
    this.placeTarget(view?.kind === 'self');
    this.keepClear();
  }

  /**
   * The aim's panel, on the right, keeps clear of the column over it (playtest 19/10 #20: the objective's card
   * and an open log went under it): centred as before when there is room, else just below the column.
   */
  private keepClear(): void {
    const panel = this.target;
    panel.style.top = '';
    if (panel.classList.contains('hidden') || panel.classList.contains('anchored') || !this.sideEl) return;
    const side = this.sideEl.getBoundingClientRect();
    const r = panel.getBoundingClientRect();
    if (!r.height || r.top >= side.bottom + EDGE) return;
    // Its offsets are in its own pixels, which the interface size zooms.
    const zoom = r.width / (panel.offsetWidth || r.width) || 1;
    panel.style.top = `calc(50% + ${Math.ceil((side.bottom + EDGE - r.top) / zoom)}px)`;
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
            ? h('button.fire', { disabled: true, title: 'Tiene el mando el otro jugador' }, view.locked)
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
    // A switch, on or off (#15: «activado/desactivado, no Sí/No»): its name, and the track with its knob.
    this.followBtn.append(h('span', {}, SETTINGS_TEXT.cameraFollow), h('i.switch', { 'aria-hidden': 'true' }, h('b')));
    this.followBtn.title = `${SETTINGS_TEXT.cameraFollowAbout} ${this.followTurn ? 'Activada.' : 'Desactivada.'}`;
  }

  /** «Ir a Mateo» (#15): the soldier whose turn it is, while this player gives the orders; null hides it. */
  setGoTo(name: string | null): void {
    this.goBtn.classList.toggle('hidden', !name);
    this.goWho.textContent = name ? `IR A ${name.split(' ')[0]!.toUpperCase()}` : '';
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

  /**
   * `detail`: a line under the stamp saying why (who saw the squad…); it stays longer, to be read. `held`: it stays
   * until it is clicked, or Intro or Esc pressed (Ana, 22/10 #50: «TU TURNO» went before it was seen); meanwhile it
   * covers the field (covering: the turn clock waits).
   */
  banner(text: string, kind: 'xcom' | 'alien' | 'warn' = 'xcom', detail?: string, held = false): void {
    // The banner is a rubber stamp coming down.
    uiSound('stamp', { gain: 0.55 });
    clear(this.bannerEl);
    append(this.bannerEl, text, detail ? h('small.banner-detail', {}, detail) : null, held ? h('small.banner-go', {}, 'Clic para seguir') : null);
    this.bannerEl.className = `banner show ${kind}${held ? ' held' : ''}`;
    window.clearTimeout(this.bannerTimer);
    if (!held) this.bannerTimer = window.setTimeout(() => this.bannerEl.classList.remove('show'), detail ? 3600 : 1500);
  }

  /**
   * A held banner (banner's `held`) let go: by its click, or Intro or Esc; `quiet`, by doing anything else in the
   * game (game.ts, Arturo 23/10 #8), without its click. Whether there was one.
   */
  releaseBanner(quiet = false): boolean {
    if (!this.bannerEl.classList.contains('held') || !this.bannerEl.classList.contains('show')) return false;
    this.bannerEl.classList.remove('show', 'held');
    if (!quiet) uiSound('confirm');
    return true;
  }

  /**
   * Arrival briefing: the operation, the area (`lines`: map, night, turns),
   * the objective in its own line and, when there is one, a line of the
   * operation's briefing; then it fades away. A longer card stays longer.
   * It stays until the player closes it (playtest 14/10 #3: it went before
   * it could be read): «Empezar», a click anywhere, Enter, Space or Esc
   * (game.ts). Resolves when it is closed.
   */
  intro(kicker: string, title: string, objective?: string, turns?: string, briefing?: string): Promise<void> {
    this.introClose?.();
    this.introArgs = [kicker, title, objective, turns, briefing];
    this.goalDock.classList.add('waiting');
    const close = () => this.introClose?.();
    // Only «Entendido» (or Enter, Space) closes it (Ana, 20/10 #32: a click by the way lost it); nothing under it is
    // played meanwhile.
    const catcher = h('div.intro-catcher');
    const el = h(
      'div.mission-intro.open',
      {},
      kicker ? h('small', {}, kicker) : null,
      // A long name smaller, in two lines (Ana, 21/10 #43).
      h(`h1${title.length > 24 ? '.long' : ''}`, {}, type(title)),
      // The objective, the turns under it in the accent, then the briefing whole (playtest 19/10 #12).
      objective ? h('p.intro-objective', {}, h('b', {}, 'Objetivo: '), objective) : null,
      turns ? h('p.intro-turns', {}, turns) : null,
      // Lore's marks (#28): the key words in white bold, the objective in the accent.
      briefing ? h('p.intro-briefing', {}, ...marked(briefing)) : null,
      // Only what closes it (playtest 18/10 #27; Ana, 20/10 #32), the same each time it is opened.
      h('div.intro-foot', {}, h('button.fire.intro-go', { type: 'button', onclick: close }, 'ENTENDIDO')),
    );
    this.root.append(catcher, el);
    return new Promise((resolve) => {
      this.introClose = (quiet = false) => {
        this.introClose = null;
        if (!quiet) uiSound('confirm');
        catcher.remove();
        this.dock(el);
        resolve();
      };
    });
  }

  /** The arrival card again, from the objective's card it shrank into (#32). */
  private reopenIntro(): void {
    if (this.introOpen || !this.introArgs || this.goalDock.classList.contains('waiting')) return;
    uiSound('paper');
    void this.intro(...this.introArgs);
  }

  /**
   * The arrival card, closed, shrinks into the objective's card on the right (#52), which shows as it gets
   * there. Its centre goes to the card's centre: scaled round itself (transform-origin on it, styles.css).
   */
  private dock(card: HTMLElement): void {
    const dock = this.goalDock;
    const from = card.getBoundingClientRect();
    const to = dock.getBoundingClientRect();
    const show = () => dock.classList.remove('waiting');
    if (!to.width || !from.width || matchMedia('(prefers-reduced-motion: reduce)').matches) {
      card.classList.add('out');
      show();
      window.setTimeout(() => card.remove(), 800);
      return;
    }
    // Moves are in the card's own pixels, which the interface size zooms (--ui-scale).
    const zoom = from.width / (card.offsetWidth || from.width) || 1;
    const dx = (to.left + to.width / 2 - (from.left + from.width / 2)) / zoom;
    const dy = (to.top + to.height / 2 - (from.top + from.height / 2)) / zoom;
    card.classList.add('docking');
    card.style.translate = `${Math.round(dx)}px ${Math.round(dy)}px`;
    card.style.scale = String(Math.max(0.15, to.width / from.width));
    window.setTimeout(show, 380);
    window.setTimeout(() => card.remove(), 700);
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

  /**
   * The partner asks to leave or to abandon (Ana, 22/10 #39: it went through unseen after 30 s): for whoever must
   * answer, a card in the middle of the screen with what is asked, the time left large, and the two answers. Null
   * takes it away. The bar keeps its own buttons too.
   */
  showCall(call: CallView | null): void {
    // Drawn again when the server's clock arrives (it may come after the call).
    const key = call ? `${call.kind}:${call.title}:${deadlineOf(call.kind)?.at ?? ''}` : '';
    if (key === this.callKey) return;
    const sounded = !!this.callEl;
    this.callKey = key;
    this.callEl?.remove();
    this.callEl = null;
    if (!call) return;
    const clock = deadlineEl(call.kind, (left) => left);
    this.callEl = h(
      `div.call-card.${call.kind}`,
      { role: 'alertdialog', 'aria-label': call.title },
      call.kicker ? h('div.call-kicker', {}, call.kicker) : null,
      h('h2.call-title', {}, call.title),
      h('p.call-text', {}, call.text),
      clock ? h('div.call-clock', {}, clock, h('small', {}, call.clockNote)) : null,
      h(
        'div.call-actions',
        {},
        h('button.x-btn.call-refuse', { type: 'button', onclick: call.refuse.act }, call.refuse.label),
        h('button.fire.call-accept', { type: 'button', onclick: call.accept.act }, call.accept.label),
      ),
    );
    this.root.append(this.callEl);
    if (!sounded) uiSound('bell');
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
    this.keepClear();
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
    const back = (to: 'main' | 'settings' = 'main') => h('button.menu-back', { onclick: () => this.renderMenu(to) }, '‹ Volver');
    const card = h('div.game-menu-card.panel');
    if (page === 'main') {
      // The same list as the base's (playtest 19/10 #7), one entry a line, all alike; the controls are in Ajustes.
      append(
        card,
        h('h2', {}, 'Menú'),
        h('button.menu-entry', { onclick: () => this.toggleMenu(false) }, 'Continuar'),
        h('button.menu-entry', { onclick: () => this.renderMenu('settings') }, 'Ajustes'),
        // The main menu (#7; the server lets one go mid-mission: a campaign's stays paused). Not once it is over:
        // the report has its own way out.
        this.outcome.classList.contains('show') ? null : h('button.menu-entry', { onclick: () => (this.toggleMenu(false), this.confirmLeave()) }, 'Volver al menú de inicio'),
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
        back('settings'),
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
      append(
        card,
        h('h2', {}, 'Ajustes'),
        sound,
        soundSettings(() => this.renderMenu('settings')),
        graphicsSettings(() => this.renderMenu('settings')),
        h('button.menu-entry', { onclick: () => this.renderMenu('controls') }, 'Controles'),
        back(),
      );
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
    if (waiting) return h('button.menu-entry.danger', { disabled: true, title: 'Contesta en su barra' }, evacWaiting(partner ?? 'el otro jugador'));
    return h(
      'button.menu-entry.danger',
      { onclick: () => (this.toggleMenu(false), this.confirmAbandon()), title: partner ? `Se lo pregunta a ${partner}` : this.paired ? 'Termina la misión para los dos' : 'Termina la misión' },
      ABANDON_TEXT.button,
    );
  }

  /** What abandoning costs, before it is asked (with the partner there, it waits for a yes on their bar). */
  /** Back to the main menu: asked with the other one here, who goes too; otherwise at once. */
  private confirmLeave(): void {
    const { partner, campaign } = this.handlers.leaveState();
    if (!partner) {
      this.handlers.onBackToMenu();
      return;
    }
    uiSound('paper');
    clear(this.modal);
    this.modal.classList.add('show');
    const close = () => this.modal.classList.remove('show');
    this.modal.append(
      h(
        'div.modal-card.panel',
        {},
        h('h2', {}, '¿Volver al menú de inicio?'),
        h('p', {}, campaign ? 'La misión queda guardada tal como está; «Continuar campaña» vuelve a ella.' : 'La escaramuza se acaba.'),
        h('p', {}, `${partner} vuelve también al menú.`),
        h(
          'div.modal-actions',
          {},
          h('button.fire', { onclick: () => (close(), this.handlers.onBackToMenu()) }, 'VOLVER AL MENÚ'),
          h('button.secondary', { onclick: close }, 'SEGUIR AQUÍ'),
        ),
      ),
    );
  }

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

  private readonly onPressAnywhere = (e: PointerEvent): void => {
    if (this.barMenu && !(e.target as Element | null)?.closest?.('.ability-slot')) this.closeAbilityMenu();
  };

  destroy(): void {
    // The listener held the HUD, and through its handlers the whole game, after each mission.
    document.removeEventListener('pointerdown', this.onPressAnywhere);
    this.root.remove();
  }
}
