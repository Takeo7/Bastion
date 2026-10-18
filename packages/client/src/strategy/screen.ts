import './strategy.css';
import {
  ARMOR_COLORS,
  ARMOR_TIERS,
  armorTier,
  baseArmorTier,
  bastionLevel,
  BIOME_NAMES,
  BITE_TAG,
  BUILD_IDS,
  buildable,
  BUILDS,
  canAfford,
  clearAim,
  contactAim,
  craftAim,
  CRAFTS,
  missionInfo,
  operationAim,
  rumorAim,
  totalBars,
  veterancyName,
  proposalCost,
  contactCapacity,
  contactCost,
  costText,
  PIECE_TEXT,
  PIECES,
  raiseCost,
  defaultAppearance,
  displayName,
  DOOM_MAX,
  doomChangeText,
  doomName,
  doomStatus,
  doomSubject,
  ENDING_TITLES,
  epilogue,
  eventDef,
  eventOptionAvailable,
  ADJACENCY,
  beds,
  bedsFree,
  facilityLevel,
  garageCut,
  healRate,
  listeningDelay,
  rangeAim,
  upgradable,
  upgradeCost,
  workDays,
  workCrews,
  xpMultiplier,
  clearCost,
  clearable,
  neighbours,
  adjacencyAt,
  adjacencyBonuses,
  buildingName,
  plotById,
  plotRing,
  TERRAINS,
  type BasePlot,
  type BuildingId,
  type FacilityId,
  EXPEDIENTE,
  FACILITIES,
  facilityCount,
  fill,
  GLOSSARY,
  HEAD_IDS,
  HEADS,
  HOME_REGION,
  ITEMS,
  itemsFree,
  itemUnlocked,
  LEVEL_XP,
  levelUnlocks,
  MAX_LEVEL,
  soldierCap,
  MISSION_NAMES,
  NAME_MAX,
  nextSteps,
  nextTierBlocker,
  NICKNAME_MAX,
  offerBriefing,
  offerSignature,
  PARTITURA_LEVEL,
  PLAYER_COLORS,
  BASE_AIM,
  MOD_SLOTS,
  WEAPON_MODS,
  weaponProfile,
  type WeaponModId,
  QUESTS,
  randomAppearance,
  RECRUIT_COST,
  REGION_LORE,
  regionIncome,
  REGIONS,
  Rng,
  shopPrice,
  soldierArms,
  plotName,
  soldierReady,
  canSpendPoints,
  PREMISE,
  proposalClock,
  floodedZoneNote,
  contactBlocker,
  underMarea,
  NIGHT_TEXT,
  craftBlocker,
  dailyRations,
  gardenYield,
  FOUND_WEAPONS,
  meleeWeaponOf,
  spareMags,
  type CraftId,
  weaponLabel,
  MEDICATE_COST,
  MEDICATE_DAYS,
  treatCost,
  TURNED_CAUSE,
  turnDay,
  commandsFor,
  squadLimit,
  squadPads,
  squadPlace,
  recruitCost,
  squadMax,
  squadMember,
  STORY,
  TECHS,
  TEMPLATES,
  TRAITS,
  VISOR_COLORS,
  weaponTier,
  type Appearance,
  type ArmorPart,
  ARMOR_PART_IDS,
  partColor,
  type BaseView,
  BASE_SIEGE_TEXT,
  VOTE_ABOVE,
  siegeHeard,
  mustWait,
  PIECE_STATE,
  BUILD_TEXT,
  siegeWarning,
  siegeWarningShort,
  inMissions,
  wintry,
  outingsMark,
  timeHelp,
  SIEGE_TEXT,
  DAYS_PER_MISSION,
  type PieceKind,
  type CampaignCommand,
  type CampaignSoldier,
  type CraftDef,
  type CampaignState,
  type ItemId,
  type ItemSlot,
  type MissionKind,
  type MissionOffer,
  type NextStep,
  type TemplateId,
  type PlayerInfo,
  type RegionId,
  type Slot,
} from '@bastion/engine';
import { greeting, Transmissions } from './transmissions';
import type { Gear } from '../game/figures';
import { gearOf } from '../game/models';
import { storageKey } from '../net';
import { append, clear, h } from '../ui/dom';
import { arrowIcon } from '../ui/icons';
import { canQuit, QUIT_BUTTON, quitGame, quitQuestion } from '../ui/quit';
import { graphicsSettings, soundSettings } from '../ui/settingsPanel';
import { BastionSet, LAUNCH_SECONDS, type FixedRoom, type LineupEntry } from './bastion';
import { GeoscapeSet } from './geoscape';
import { ClearingSet, footprint, type BaseCursor } from './clearing';
import { ClearingArtView } from './clearingArt';
import { palette, pieceCard, placeBlockerFor, placeLine } from './buildMode';
import { Stage, type Shot } from './stage';
import { sound } from '../audio/bank';
import { uiSound } from '../audio/ui';
import { plotIcon } from './plotIcons';
import { carriedProfile, kindOf, loadoutEl, modPanel, placeOf, profileOf, weaponCare, weaponDiff, weaponIcon, weaponMarks, weaponPick, weaponStats, type WeaponPickContext } from './arms';
import { soldierPortrait } from './portraits';
import { barLines, learnedTags, roleDot, skillsBlock, skillsOf, treePanel, veterancyBadge } from './skillSheet';
import { deadlineEl } from '../ui/deadline';
import { SURVIVOR_CHOICES } from '../game/syntyManifest';

export interface StrategyHandlers {
  send(cmd: CampaignCommand): void;
  onView(view: BaseView): void;
  onLeave(): void;
  /** The piece in this player's hand on the clearing, for the partner (null: none). */
  onCursor?(c: BaseCursor | null): void;
}

interface Alert {
  tone: 'info' | 'good' | 'bad';
  kicker: string;
  title: string;
  /** Typed paragraphs before `text` (a page of the Diario). */
  body?: string[];
  text: string;
  /** Handwritten in the margin. */
  note?: string;
  action?: { label: string; go: () => void };
  /** Higher first: a moment pops up only its most important alert. */
  rank?: number;
  /** Where it happens: the globe turns there when it pops up (rumours). */
  region?: RegionId;
  /** What a level unlocks, one per line with its icon (UI, playtest 14/10 #20: it came all in one line). */
  unlocks?: Unlock[];
}

/** One thing a Bastion level unlocks: its icon (agenda-icon kind), its name and a line on it. */
interface Unlock {
  kind: string;
  name: string;
  detail?: string;
}

/** The Novedades tray keeps this many alerts at most. */
const MAX_NEWS = 20;

const REPORT_KEY = storageKey('seenReport');
/** The rule of time, wherever the day is shown. */
/** Days counted in missions (docs/FASE12.md §11.6): each one takes DAYS_PER_MISSION days inside, and any day left is one more. */
function missionsFor(days: number): number {
  return Math.max(1, Math.ceil(days / DAYS_PER_MISSION));
}

/** «1 misión», «3 misiones». */
function missionsText(n: number): string {
  return n === 1 ? '1 misión' : `${n} misiones`;
}

/** How far off `day` is, in missions, for a chip: «ahora», «próxima», «en 3». */
function aheadChip(c: CampaignState, day: number): string {
  const left = day - c.day;
  if (left <= 0) return 'ahora';
  const n = missionsFor(left);
  return n === 1 ? 'próxima' : `en ${n}`;
}

/** The same in words: «tras la próxima misión», «dentro de 3 misiones» (Lore's inMissions). */
function aheadText(c: CampaignState, day: number): string {
  const left = day - c.day;
  return left <= 0 ? 'ya' : inMissions(missionsFor(left));
}

/** What each browser has already seen of every campaign's news (the tabs' numbers). */
const NEWS_KEY = storageKey('seenNews');
/** Campaigns whose premise card was shown in this browser (playtest 13/10: «¿quién soy?»). */
const PREMISE_KEY = storageKey('seenPremise');

const KIND_ICON: Record<MissionKind, string> = { elimination: '✕', recovery: '◆', sabotage: 'ϟ', hack: '⌘', rescue: '✚', scavenge: '⌂' };
const DIFFICULTY = ['', 'Moderada', 'Difícil', 'Muy difícil', 'Extrema'];
/** Fase 9: every soldier fights as the same template (skills.ts fighterStats); what tells them apart is their look. */
const SOLDIER: TemplateId = 'rookie';

const NAV: { view: BaseView; label: string }[] = [
  // «Bastión» and «Diario» are one tab, «Campaña» (Arturo, 16/10 #33): what to do now, the calendar, the levels.
  { view: 'research', label: 'Campaña' },
  { view: 'geoscape', label: 'El valle' },
  { view: 'engineering', label: 'Armería' },
  { view: 'barracks', label: 'Cuartel' },
  // The base is built on the clearing now (docs/FASE12.md §11.7): the tab is where it is built (B).
  { view: 'facilities', label: 'Construir' },
];

/** Room of the Bastion each screen happens in (for the partner marker and the camera). */
const ROOM_OF: Partial<Record<BaseView, FixedRoom | 'hangar'>> = {
  geoscape: 'command',
  research: 'research',
  engineering: 'engineering',
  barracks: 'barracks',
  squad: 'hangar',
  debrief: 'hangar',
};

const VIEW_NAMES: Record<BaseView, string> = {
  hub: 'Campaña',
  geoscape: 'El valle',
  research: 'Campaña',
  engineering: 'Armería',
  barracks: 'Cuartel',
  facilities: 'Construir',
  squad: 'Cochera',
  debrief: 'Informe',
};

/** "la parcela C2" → "La parcela C2". */
function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** The bunker's own buildings: shown in 3D, not in the list of plots. */
const FIXED_NAMES_SET: Record<string, true> = { command: true, barracks: true, research: true, engineering: true };

/** Whether `facility` stands wall to wall with its partner building. */
function hasBonus(c: CampaignState, facility: FacilityId): boolean {
  return adjacencyBonuses(c).some((b) => b.def.facility === facility);
}

/** «el Cuartel», «la Armería»: a building's name with its article. */
function withArticle(id: BuildingId): string {
  const name = buildingName(id);
  return /^(Armería|Enfermería|Sala|Estación|Brigada|Cámara)\b/.test(name) ? `la ${name}` : `el ${name}`;
}

/** «junto al Cuartel», «junto a la Armería». */
function nextTo(id: BuildingId): string {
  return `junto ${withArticle(id).replace(/^el /, 'al ').replace(/^la /, 'a la ')}`;
}

const cap = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

/** The facilities by what they are for, in the build list (playtest: «mucha lista seguida, mucho que leer»). */
const BUILD_GROUPS: [string, FacilityId[]][] = [
  ['Soldados', ['quarters', 'infirmary', 'garden', 'training', 'range', 'memorial']],
  ['Equipo y obras', ['workshop', 'garage', 'crew']],
  ['Información', ['relay', 'listening', 'simulation']],
  ['El Oficio', ['echoes']],
];

/** "C2": the plot's column letter and row number, as on a map. */
function plotCode(p: BasePlot): string {
  return `${'ABCDEFG'[p.x] ?? '?'}${p.y + 1}`;
}

function loadSeenReport(): string {
  try {
    return localStorage.getItem(REPORT_KEY) ?? '';
  } catch {
    return '';
  }
}

/**
 * The clock on the open proposal (PRUEBA-2026-10-14 #28): unanswered, the
 * server takes it as the partner's yes, so whoever stays can play on. `mine`:
 * this player proposed it.
 */
function proposalTimer(mine: boolean): HTMLElement | null {
  return deadlineEl('proposal', (left) => proposalClock(left, mine));
}

/**
 * The strategy layer, XCOM 2-style: the Bastion is the hub, every room opens a
 * screen (geoscape, research, engineering, barracks, facilities), missions go
 * through squad select in the hangar, a take-off and a debrief. Both players
 * move around freely; each sees where the other one is.
 */
export class StrategyScreen {
  private readonly root = h('div.strat');
  private readonly stage = new Stage();
  private readonly bastion: BastionSet;
  private readonly geo: GeoscapeSet;
  /** The base in the clearing (docs/FASE12.md §11.7): Campaña and Construir look at it. */
  private readonly clearing: ClearingSet;
  /** Construir: the palette's tab, the piece picked on the clearing, and «Desmontar» pressed once. */
  private buildKind: PieceKind = 'building';
  private piece: string | null = null;
  private dismantling = false;
  private partnerCursorTimer = 0;
  /** The clearing is drawn under snow (still winter in the campaign: wintry); null until the first state. */
  private clearingSnow: boolean | null = null;
  private clearingArt: ClearingArtView | null = null;
  /** Back from a mission: La Lata comes in up the track the first time the clearing is shown. */
  private arriving = false;
  private readonly top = h('div.strat-top');
  private readonly proposalEl = h('div.strat-proposal.x-panel');
  private readonly viewEl = h('div.strat-view');
  private readonly nav = h('div.strat-nav');
  private readonly modal = h('div.strat-modal');
  private readonly toastEl = h('div.strat-toast');
  private state: CampaignState | null = null;
  private players: PlayerInfo[] = [];
  private view: BaseView = 'research';
  private partnerView: BaseView | null = null;
  private seenReport = loadSeenReport();
  /** The pop-up waiting to be read: one per moment (back at the base, or after a scan). */
  private alerts: Alert[] = [];
  /** Everything else that happened, in the Novedades tray, newest first. */
  private news: Alert[] = [];
  private newsOpen = false;
  /** The story's radio scenes (strategy/transmissions.ts); made in the constructor, which knows this player's seat. */
  private readonly radio: Transmissions;
  private toastTimer = 0;
  private anchorFrame = 0;
  private scroll = new Map<string, number>();
  // Selections inside the screens.
  private region: RegionId | null = null;
  private offer: string | null = null;
  private levelPick: number | null = null;
  private engTab: 'gear' | 'items' = 'items';
  private engPick: string | null = null;
  private soldier: string | null = null;
  /** The barracks customizer's draft; saved only when the player confirms. */
  private custom: { soldier: string; name: string; nickname: string; appearance: Appearance } | null = null;
  /** The plot picked in the facilities view ("x,y"). */
  private plot: string | null = null;
  /** The plot under the pointer, in 3D or in the grid. */
  private plotHovered: string | null = null;
  /** The Árbol de habilidades is open on the soldier's sheet (skillSheet.ts keeps its draft). */
  private promoting = false;
  /** Open equipment picker ("soldierId:slot"). */
  private picking: string | null = null;
  /** The alert on screen last time, so turning the globe to it happens once. */
  private shownAlert: Alert | null = null;
  /** "Volver al menú" asked: it takes both players, so it is confirmed first. */
  private leaving = false;
  /** «Salir del juego» asked from the menu (desktop app only, ui/quit.ts): its question in the same window. */
  private quitting = false;
  /** Settings open over the base: only for this player, the partner stays where they are. */
  private settingsOpen = false;
  /** News keys already seen (navNews), for the campaign `seenNewsFor`. */
  private readonly seenNews = new Set<string>();
  private seenNewsFor: string | null = null;

  constructor(container: HTMLElement, private readonly mySlot: Slot, private readonly handlers: StrategyHandlers) {
    this.radio = new Transmissions(() => ({ saludo: greeting(this.commanderNames()), yo: greeting([this.playerName(mySlot)]) }), mySlot);
    this.bastion = new BastionSet((hot) => this.pickRoom(hot));
    this.bastion.onHover = (hot) => this.onPlotHover(hot);
    this.geo = new GeoscapeSet((region) => {
      this.region = region;
      this.offer = this.state?.offers.find((o) => o.region === region)?.id ?? null;
      this.geo.focus(region);
      this.render();
    });
    this.clearing = new ClearingSet({
      blocker: (piece, x, y, rot) => (this.state ? placeBlockerFor(this.state, piece, x, y, rot) : 'Un momento.'),
      placeHint: (piece, blocked) => (this.state ? placeLine(this.state, piece, !!this.partner()?.connected, blocked) : ''),
      place: (piece, x, y, rot) => {
        this.send({ type: 'place', piece, x, y, rot });
        // Walls and traps go down one after another; a building (most are only one) or what goes to a vote, once.
        if (PIECES[piece].kind === 'building' || PIECES[piece].unique || (this.partner()?.connected && PIECES[piece].cost.supplies > VOTE_ABOVE)) this.dropTool();
      },
      fell: (tiles) => this.send({ type: 'fell', tiles }),
      select: (id) => this.pickPiece(id),
      cancel: () => this.dropTool(),
      cursor: (c) => this.handlers.onCursor?.(c),
    });
    this.stage.add('bastion', this.bastion);
    this.stage.add('geo', this.geo);
    this.stage.add('clearing', this.clearing);
    this.root.append(this.stage.element, this.viewEl, this.top, this.proposalEl, this.nav, this.modal, this.radio.element, this.toastEl);
    container.append(this.root);
    void this.stage.show('clearing', this.clearing.shot(), true);
    handlers.onView(this.view);
    window.addEventListener('keydown', this.onKey);
  }

  private readonly onKey = (e: KeyboardEvent) => {
    if (e.defaultPrevented || !this.state || this.root.classList.contains('launching')) return;
    if ((e.target as HTMLElement | null)?.closest?.('input, textarea')) return;
    // B: in and out of Construir, from the clearing.
    if (e.code === 'KeyB' && !e.ctrlKey && !e.metaKey && !this.modal.classList.contains('show') && (this.view === 'research' || this.view === 'facilities')) {
      void this.go(this.view === 'facilities' ? 'research' : 'facilities');
      return;
    }
    if (e.key !== 'Escape') return;
    e.preventDefault();
    this.back();
  };

  /** Esc: closes what is open, lets go of what is picked, and otherwise goes back to the Bastión. */
  private back(): void {
    if (this.picking || this.custom || this.promoting || this.leaving || this.quitting || this.newsOpen || this.settingsOpen) {
      this.picking = null;
      this.custom = null;
      this.promoting = false;
      this.leaving = false;
      this.quitting = false;
      this.newsOpen = false;
      this.settingsOpen = false;
      this.render();
      return;
    }
    // Alerts and events are answered with their own buttons.
    if (this.modal.classList.contains('show')) return;
    // Construir: the piece in hand first, then the one picked.
    if (this.view === 'facilities' && this.clearing.currentTool.kind !== 'look') return this.dropTool();
    if (this.view === 'facilities' && this.piece) return this.pickPiece(null);
    else if (this.view !== 'research' && this.view !== 'debrief') void this.go('research');
  }

  // ------------------------------------------------------------ public

  /**
   * `before`: what the base looked like when the squad took off. A screen made
   * anew after a mission compares with it, so the mission's news (levels, new
   * operations, the clock) still pop up, after the debrief, for both commanders.
   */
  update(c: CampaignState, before?: CampaignState | null): void {
    const prev = this.state ?? before ?? null;
    // A screen made anew after a mission (main.ts passes the base as it took off): La Lata is on its way back.
    if (before && !this.state) this.arriving = true;
    this.state = c;
    this.loadSeenNews(c);
    // News arriving on the screen being looked at is seen at once.
    this.seeNews(this.view);
    // The partner's proposal waits on its note (with «Ver»): it never takes this player away from what they are doing.
    const proposed = !!c.proposal && c.proposal.by !== this.mySlot && prev?.proposal?.text !== c.proposal.text;
    if (proposed) this.blink('.strat-proposal');
    // One's own proposal that costs: said at once that nothing is paid until the yes (playtest 16/10 #5).
    if (c.proposal && c.proposal.by === this.mySlot && prev?.proposal?.text !== c.proposal.text && proposalCost(c, c.proposal.action)) {
      this.toast(`Propuesto a ${this.partner()?.name ?? 'tu compañero'}: se paga cuando diga que sí.`);
    }
    this.bastion.setBase(c);
    if (this.clearingSnow !== wintry(c)) {
      this.clearingSnow = wintry(c);
      this.clearing.setArt((this.clearingArt = new ClearingArtView(this.clearingSnow)));
    }
    this.clearing.setBase(c.bastion);
    const placing = c.proposal?.action.type === 'place' ? c.proposal.action : null;
    this.clearing.setProposal(placing ? { piece: placing.piece, x: placing.x, y: placing.y, rot: placing.rot } : null);
    if (this.piece && !c.bastion.pieces.some((p) => p.id === this.piece)) this.piece = null;
    this.geo.flyTo(this.offerById(c.activeMission)?.region ?? HOME_REGION);
    if (prev) this.collectAlerts(prev, c);
    this.showPremise(c);
    // A mission chosen by either player sends both to the hangar; a finished one to the debrief.
    if (!prev?.activeMission && c.activeMission) void this.go('squad');
    else if (prev?.activeMission && !c.activeMission && this.view === 'squad') void this.go('geoscape');
    if (!c.activeMission && !this.reportPending(c)) this.bastion.setLineup([]);
    if (this.reportPending(c) && this.view !== 'debrief') void this.go('debrief');
    else this.render();
    // Back without a debrief to read: the clearing is already on screen.
    if (this.arriving && !this.reportPending(c) && this.setFor(this.view) === 'clearing') this.arrive();
  }

  /** La Lata comes back up the track and stops by the caravan (clearingArt.ts), with the camera on it. */
  private arrive(): void {
    this.arriving = false;
    this.clearing.focusCaravan(true);
    void this.clearingArt?.arrive();
  }

  setPlayers(players: PlayerInfo[]): void {
    this.players = players;
    this.render();
  }

  /** Drawn again as it is: the clock on an open question changed (ui/deadline.ts, PRUEBA-2026-10-14 #28). */
  refresh(): void {
    this.render();
  }

  setPartnerView(view: BaseView): void {
    this.partnerView = view;
    // Off Construir, nothing in the partner's hand.
    if (view !== 'facilities') this.setPartnerCursor(null);
    this.render();
  }

  /** The partner's piece in hand on the clearing (main.ts, 'baseCursor'); it goes if nothing comes for a while. */
  setPartnerCursor(m: { slot: Slot; piece: BaseCursor['piece'] | null; x: number; y: number; rot: number } | null): void {
    window.clearTimeout(this.partnerCursorTimer);
    const p = m && this.players.find((x) => x.slot === m.slot);
    if (!m?.piece || !p) {
      this.clearing.setPartnerCursor(null);
      return;
    }
    this.clearing.setPartnerCursor({ piece: m.piece, x: m.x, y: m.y, rot: m.rot, color: p.color, name: p.name });
    this.partnerCursorTimer = window.setTimeout(() => this.clearing.setPartnerCursor(null), 6000);
  }

  toast(text: string): void {
    this.toastEl.textContent = text;
    this.toastEl.classList.add('show');
    window.clearTimeout(this.toastTimer);
    this.toastTimer = window.setTimeout(() => this.toastEl.classList.remove('show'), 2600);
  }

  /**
   * Take-off: La Lata leaves the clearing by its track (clearingArt.ts, docs/FASE12.md
   * §11.7) before the mission loads; the screen goes black once it is gone.
   */
  async launch(): Promise<void> {
    clear(this.modal);
    this.modal.classList.remove('show');
    this.root.classList.add('launching');
    this.root.append(h('div.launch-banner', {}, 'EN MARCHA'));
    const art = this.clearingArt;
    // A siege: nobody leaves, they are here (docs/FASE12.md §11.5). The clearing, its line, and into the fight.
    if (this.offerById(this.state?.activeMission ?? null)?.siege && art) {
      this.root.querySelector('.launch-banner')!.textContent = BASE_SIEGE_TEXT.here.replace(/\.$/, '').toUpperCase();
      this.clearing.setBuilding(false);
      this.clearing.leave();
      this.clearing.focusCaravan(true);
      await this.stage.show('clearing', this.clearing.shot());
      await new Promise((r) => setTimeout(r, 1600));
      this.stage.blackout();
      await new Promise((r) => setTimeout(r, 450));
      return;
    }
    if (!art) {
      // No clearing yet (no state): the hangar's take-off of before.
      void this.stage.show('bastion', this.bastion.takeoff());
      const gone = this.bastion.launch();
      window.setTimeout(() => this.stage.blackout(), (LAUNCH_SECONDS - 0.5) * 1000);
      await gone;
      return;
    }
    this.clearing.setBuilding(false);
    this.clearing.leave();
    this.clearing.focusCaravan(true);
    await this.stage.show('clearing', this.clearing.shot());
    // Never held up for long whatever happens to the drawing.
    await Promise.race([art.depart(), new Promise((r) => setTimeout(r, 9000))]);
    this.stage.blackout();
    await new Promise((r) => setTimeout(r, 450));
  }

  destroy(): void {
    window.removeEventListener('keydown', this.onKey);
    window.clearTimeout(this.partnerCursorTimer);
    cancelAnimationFrame(this.anchorFrame);
    this.clearing.dispose();
    this.stage.dispose();
    this.root.remove();
  }

  // -------------------------------------------------------- navigation

  private async go(asked: BaseView): Promise<void> {
    // The old «Bastión» is Campaña now (#33): whatever still asks for it lands there.
    const view = asked === 'hub' ? 'research' : asked;
    this.view = view;
    this.seeNews(view);
    this.promoting = false;
    this.picking = null;
    this.custom = null;
    this.handlers.onView(view);
    if (view !== 'facilities') {
      this.clearing.setTool({ kind: 'look' });
      this.piece = null;
    }
    this.clearing.setBuilding(view === 'facilities');
    if (this.setFor(view) !== 'clearing') this.clearing.leave();
    else if (this.arriving) {
      // Back from a mission, the first time on the clearing (after the debrief): La Lata comes in.
      this.render();
      await this.stage.show('clearing', this.shotFor(view));
      this.arrive();
      return;
    }
    this.render();
    await this.stage.show(this.setFor(view), this.shotFor(view));
  }

  /** The 3D set each screen happens in: the valley, the clearing, or the rooms that are screens (the Cuartel's, the Armería's, the hangar). */
  private setFor(view: BaseView): 'geo' | 'clearing' | 'bastion' {
    return view === 'geoscape' ? 'geo' : view === 'research' || view === 'hub' || view === 'facilities' ? 'clearing' : 'bastion';
  }

  private shotFor(view: BaseView): Shot {
    switch (view) {
      case 'geoscape':
        return this.geo.shot();
      case 'research':
      case 'hub':
      case 'facilities':
        return this.clearing.shot();
      // Campaña looks at the whole Bastión, as the old «Bastión» did.
      case 'engineering':
        return this.bastion.room(view);
      case 'barracks':
        return this.bastion.armory();
      case 'squad':
      case 'debrief':
        return this.bastion.hangar();
      case 'facilities':
        return this.plot ? this.bastion.room(this.plot) : this.bastion.facilities();
      default:
        return this.bastion.overview();
    }
  }

  /** Clicking a room of the Bastion opens its screen. */
  private pickRoom(hot: string): void {
    if (hot.startsWith('plot:')) {
      this.pickPlot(hot.slice(5));
      return;
    }
    const target: Record<string, BaseView> = { command: 'geoscape', research: 'research', engineering: 'engineering', barracks: 'barracks' };
    if (hot === 'hangar') void this.go(this.state?.activeMission ? 'squad' : 'geoscape');
    else if (target[hot]) void this.go(target[hot]!);
  }

  /**
   * Picks a plot of the facilities view, here or in 3D. Picking it again lets
   * it go (`toggle`); «Ver» buttons pick without letting go, and make it blink.
   */
  private pickPlot(id: string | null, toggle = true): void {
    this.plot = toggle && id === this.plot ? null : id;
    if (this.view !== 'facilities') {
      void this.go('facilities');
    } else {
      this.render();
      void this.stage.show('bastion', this.shotFor('facilities'));
    }
    if (!toggle && id) this.blink(`[data-plot="${id}"], .strat-right .x-title`);
  }

  /** The pointer on a plot, in 3D or in the grid: both light the same one, without redrawing the panel. */
  private onPlotHover(hot: string | null): void {
    this.plotHovered = hot?.startsWith('plot:') ? hot.slice(5) : null;
    if (this.view !== 'facilities' || !this.state) return;
    for (const el of this.viewEl.querySelectorAll<HTMLElement>('.plot-cell')) el.classList.toggle('hovered', el.dataset.plot === this.plotHovered);
    const line = this.viewEl.querySelector('.plot-hover');
    if (line) {
      clear(line);
      append(line, ...this.plotHoverLine(this.state, this.plotHovered ?? this.plot));
    }
  }

  private plotHoverLine(c: CampaignState, id: string | null): (HTMLElement | string)[] {
    const p = id ? plotById(c, id) : undefined;
    if (!p) return ['Pasa por encima de una parcela para ver qué hay; púlsala para elegirla.'];
    return [h('b', {}, plotCode(p)), ` · ${this.plotLine(c, p.id)}`, id === this.plot && !this.plotHovered ? ' · pulsa otra vez o Esc para soltarla' : ''];
  }

  /** What stands on a plot and what can be done with it, in one line. */
  private plotLine(c: CampaignState, id: string): string {
    const p = plotById(c, id)!;
    const work = c.base.works.find((w) => w.plot === p.id);
    const level = p.building && p.building in FACILITIES ? (p.level ?? 1) : 0;
    if (work) {
      const what =
        work.kind === 'clear'
          ? `Despejando: ${TERRAINS[p.terrain as keyof typeof TERRAINS]?.name.toLowerCase() ?? ''}`
          : work.kind === 'upgrade'
            ? `${FACILITIES[work.facility!].name} → nivel ${level + 1}`
            : `En obras: ${FACILITIES[work.facility!].name}`;
      return `${what} · faltan ${work.daysLeft} días`;
    }
    if (p.building && p.building in FIXED_NAMES_SET) return `${buildingName(p.building)} · del búnker original`;
    if (p.building) {
      const pair = adjacencyBonuses(c).some((b) => b.plot === p.id);
      return `${buildingName(p.building)} · nivel ${level}${!upgradable(c, p) ? ' · se puede mejorar' : pair ? ' · con su pareja al lado' : ''}`;
    }
    if (p.terrain !== 'clear') return `${TERRAINS[p.terrain].name} · ${clearable(c, p) ? 'aún no se puede despejar: despeja antes a su lado' : `despejar cuesta ${clearCost(p).cost} §`}`;
    return neighbours(c, p).some((q) => q.building) ? 'Libre · se puede construir' : 'Libre · sin acceso: construye antes a su lado';
  }

  /** Makes what a «Ver» points at blink, so it is found even when it is already on screen. */
  private blink(selector: string): void {
    window.setTimeout(() => {
      for (const el of this.root.querySelectorAll<HTMLElement>(selector)) {
        el.classList.remove('blink');
        void el.offsetWidth;
        el.classList.add('blink');
      }
    }, 60);
  }

  // ------------------------------------------------------------ helpers

  private c(): CampaignState {
    return this.state!;
  }

  private send(cmd: CampaignCommand): void {
    this.handlers.send(cmd);
  }

  private partner(): PlayerInfo | undefined {
    return this.players.find((p) => p.slot !== this.mySlot);
  }

  private connected(): Slot[] {
    return this.players.filter((p) => p.connected).map((p) => p.slot);
  }

  /**
   * Whether this player gives orders to `owner`'s soldiers (campaign.ts commandsFor): its own, and the
   * partner's while the partner isn't connected (Arturo, 17/10: «si juego solo, debería poder usarlo solo»).
   */
  private commands(owner: Slot): boolean {
    return commandsFor(this.mySlot, owner, this.connected());
  }

  private playerName(slot: Slot): string {
    return this.players.find((p) => p.slot === slot)?.name ?? `Jugador ${slot + 1}`;
  }

  private playerColor(slot: Slot): string {
    return this.players.find((p) => p.slot === slot)?.color ?? '#888';
  }

  /** «de Ana», in her colour: a partner's soldier in a list of those one commands while she's gone. */
  private whoseTag(owner: Slot): HTMLElement {
    return h('small.x-whose', { style: { '--accent': this.playerColor(owner) } }, ` · de ${this.playerName(owner)}`);
  }

  /** The soldier's own bust on a lineup card ([data-bust]), once drawn (portraits.ts); until then, the empty frame. */
  private bustOn(card: HTMLElement, s: Pick<CampaignSoldier, 'id' | 'owner'> & { appearance?: Appearance }): void {
    void soldierPortrait(SOLDIER, PLAYER_COLORS[s.owner], s.appearance, s.id)
      .then((url) => card.style.setProperty('--portrait', `url('${url}')`))
      .catch(() => undefined);
  }

  /** Shared decisions are proposals only when the partner is around. */
  private decide(verb: string): string {
    const partnerHere = this.c().players.length > 1 && !!this.partner()?.connected;
    return partnerHere ? `Proponer ${verb}` : verb;
  }

  /** First reason that applies, shown in pen under a disabled button. */
  private why(...checks: [boolean, string][]): string {
    return checks.find(([on]) => on)?.[1] ?? '';
  }

  private whyEl(text: string): HTMLElement | null {
    return text ? h('p.x-why', {}, text) : null;
  }

  /** The weapon and armour tier a soldier has earned, for their model. */
  private gearFor(c: CampaignState, s: CampaignSoldier): Gear {
    return { ...gearOf(weaponTier(c, s), armorTier(c, s)), arms: soldierArms(c, s) };
  }

  /** "Faltan 20 suministros", or '' when the cost is affordable. */
  private shortfall(cost: number): string {
    const missing = cost - this.c().credits;
    return missing > 0 ? `Faltan ${missing} suministros` : '';
  }

  private offerById(id: string | null): MissionOffer | undefined {
    return id ? this.state?.offers.find((o) => o.id === id) : undefined;
  }

  private reportKey(c: CampaignState): string {
    const r = c.lastReport;
    return r ? `${r.day ?? ''}:${r.missionName}:${r.victory}` : '';
  }

  /**
   * The debrief is shown to whoever took part: a commander who joins after the
   * mission lands in the base instead (UX-REVISION-2 #37), where Lore's arrival
   * scene sums the campaign up.
   */
  private reportPending(c: CampaignState): boolean {
    const r = c.lastReport;
    return !!r && !c.activeMission && this.reportKey(c) !== this.seenReport && r.squad.some((s) => s.owner === this.mySlot);
  }

  private bar(value: number, max: number): HTMLElement {
    const pct = Math.min(100, Math.round((value / Math.max(1, max)) * 100));
    return h('div.x-bar', {}, h('i', { style: { width: `${pct}%` } }));
  }

  /** Opens a soldier's sheet in the Cuartel. */
  private openSoldier(id: string): void {
    this.soldier = id;
    void this.go('barracks');
  }

  /**
   * How a soldier is: fallen, with a fever from a bite (and the days left
   * before turning), wounded, or ready. `short` for the Cuartel's list.
   */
  private healthTag(c: CampaignState, s: CampaignSoldier, short: boolean): HTMLElement {
    const tag = (tone: string, text: string, title = '') => h(`${short ? 'span.x-side' : 'b.x-state'}${tone}`, title ? { title } : {}, text);
    if (!s.alive) return tag('.x-bad', s.fellCause === 'turned' ? TURNED_CAUSE : 'Caído');
    if (s.infection) {
      const day = turnDay(c, s)!;
      return tag('.x-bad', short ? `Fiebre · ${aheadChip(c, day)}` : `Mordisco · se convierte ${aheadText(c, day)}`, 'Un mordisco: tiene fiebre. Sin el tratamiento, se convierte.');
    }
    if (s.woundedDays) return tag('.x-warn', short ? missionsText(missionsFor(s.woundedDays)) : `Heridas · ${missionsText(missionsFor(s.woundedDays))}`);
    return tag(short ? '' : '.x-good', 'Listo');
  }

  /**
   * Medicines on a soldier's sheet (docs/FASE3.md): treating a bite takes both
   * commanders, like launching; healing a wound faster is one click.
   */
  private careActions(c: CampaignState, s: CampaignSoldier): HTMLElement[] {
    if (!s.alive) return [];
    const have = c.medicines ?? 0;
    const out: HTMLElement[] = [];
    if (s.infection) {
      const cost = treatCost(c);
      const proposal = c.treatment;
      const partner = this.partner();
      const shared = !!partner?.connected;
      const mine = proposal?.soldier === s.id && proposal.ready[this.mySlot];
      const theirs = proposal?.soldier === s.id && !!partner && proposal.ready[partner.slot];
      const other = proposal && proposal.soldier !== s.id ? c.soldiers.find((x) => x.id === proposal.soldier) : undefined;
      const short = have < cost;
      let row: HTMLElement;
      if (theirs && !mine) {
        row = h(
          'div.alert-actions',
          {},
          h('button.x-btn.primary.waiting', { disabled: short, onclick: () => this.send({ type: 'treat', soldier: s.id, ready: true }) }, `Confirmar el tratamiento (${cost} ✚)`),
          h('button.x-btn.danger', { onclick: () => this.send({ type: 'treat', soldier: s.id, ready: false }) }, 'Rechazar'),
        );
      } else if (mine) {
        row = h(
          'div.alert-actions',
          {},
          h('button.x-btn.ready', { onclick: () => this.send({ type: 'treat', soldier: s.id, ready: false }) }, 'Retirar la propuesta'),
          h('small.x-muted', {}, `Esperando a ${partner?.name ?? 'tu compañero'}`),
        );
      } else {
        const why = short ? `Hacen falta ${cost} ✚ y tenéis ${have}.` : other ? `Ya hay un tratamiento propuesto para ${other.name}: confirmadlo o rechazadlo antes.` : '';
        row = h(
          'button.x-btn.primary',
          { disabled: short || !!other, title: why, onclick: () => this.send({ type: 'treat', soldier: s.id, ready: true }) },
          shared ? `Proponer el tratamiento (${cost} ✚)` : `Tratar (${cost} ✚)`,
        );
      }
      out.push(
        h('div.x-group', {}, 'Mordido'),
        h('p.x-muted', {}, shared ? `Las medicinas no llegan para todos: el tratamiento lo decidís los dos. Tenéis ${have} ✚.` : `El tratamiento le quita la fiebre. Tenéis ${have} ✚.`),
        row,
      );
    } else if (s.woundedDays > 0) {
      out.push(
        h(
          'button.x-btn',
          {
            disabled: have < MEDICATE_COST,
            title: have < MEDICATE_COST ? 'No quedan medicinas.' : `Gasta ${MEDICATE_COST} ✚ de las ${have} que tenéis.`,
            onclick: () => this.send({ type: 'medicate', soldier: s.id }),
          },
          `Curar (${MEDICATE_COST} ✚): vuelve antes`,
        ),
      );
    }
    return out;
  }

  /** A small typed tag for costs, days and requirements; the long version goes in the title. */
  private chip(text: string, tone: '' | 'good' | 'bad' | 'warn' = '', title = ''): HTMLElement {
    return h(`span.x-chip${tone ? `.${tone}` : ''}`, title ? { title } : {}, text);
  }

  /** A stat with its symbol, in the same colour as in combat (health green, aim ink…). */
  private stat(kind: string, value: string, title: string): HTMLElement {
    return h(`span.stat.${kind}`, { title }, h('span.stat-icon'), h('b', {}, value));
  }

  /** "Tienes 2 (1 libre)", or nothing when there are none. */
  private stockText(c: CampaignState, item: ItemId): string {
    const have = c.inventory[item] ?? 0;
    const free = itemsFree(c, item);
    if (!have) return '';
    return free === have ? `Tienes ${have}` : `Tienes ${have} (${free} ${free === 1 ? 'libre' : 'libres'})`;
  }

  /** Tier diamonds, coloured like the guns: steel, magnetic, plasma. */
  private tierMarks(tier: number): HTMLElement {
    return h(`span.tier.t${tier}`, { title: `Nivel ${tier}` }, ...Array.from({ length: tier }, () => h('i')));
  }

  /** Filled and empty marks for "2 of 3" counts (contacts, squad slots). */
  private meter(value: number, max: number, title: string): HTMLElement {
    return h('span.x-meter', { title }, ...Array.from({ length: max }, (_, i) => h(`i${i < value ? '.on' : ''}`)), h('b', {}, `${value}/${max}`));
  }

  /** The Bastion level with its experience bar, for the top strip. */
  private levelBadge(c: CampaignState): HTMLElement {
    const level = bastionLevel(c);
    const from = LEVEL_XP[level - 1]!;
    const to = LEVEL_XP[level];
    return h(
      'span.strat-level',
      { title: to === undefined ? 'Nivel máximo del Bastión' : `${c.xp - from}/${to - from} de experiencia para el nivel ${level + 1}` },
      h('small', {}, 'NIVEL'),
      h('b', {}, String(level)),
      to === undefined ? null : this.bar(c.xp - from, to - from),
    );
  }

  /** What a Bastion level unlocks, one by one: techs, accessories, facilities and their upgrades (levelUnlocks' order). */
  private unlocksOf(level: number): Unlock[] {
    const known = new Set<string>();
    const out: Unlock[] = [];
    const add = (kind: string, name: string, detail?: string) => (known.add(name), out.push({ kind, name, detail }));
    for (const t of Object.values(TECHS).filter((x) => x.level === level)) add('xp', t.name, t.description);
    for (const i of Object.values(ITEMS).filter((x) => x.level === level)) add('flag', i.name, i.description);
    for (const f of Object.values(FACILITIES).filter((x) => x.level === level)) add('pick', f.name, f.description);
    // The rest are facilities going up a level («Barracón, nivel 2»).
    for (const name of levelUnlocks(level).filter((n) => !known.has(n))) add('pick', name);
    return out;
  }

  /** "Armas magnéticas · Sala de simulación": what a Bastion level unlocks. */
  private unlocksText(level: number): string {
    const list = levelUnlocks(level);
    return list.length ? `Desbloquea: ${list.join(' · ')}.` : 'Sin desbloqueos nuevos.';
  }

  /** Remembers each panel's scroll across re-renders. */
  private panel(key: string, side: 'left' | 'right' | 'left.compact', ...children: (Node | null | false | undefined)[]): HTMLElement {
    const wrap = h(`div.strat-${side}`, { onscroll: (e: Event) => this.scroll.set(key, (e.target as HTMLElement).scrollTop) });
    const inner = h('div.x-panel');
    append(inner, ...children);
    wrap.append(inner);
    const top = this.scroll.get(key) ?? 0;
    queueMicrotask(() => (wrap.scrollTop = top));
    return wrap;
  }

  // ------------------------------------------------------------- alerts

  /**
   * Who the players are, once per campaign and browser, before the first
   * mission (the campaign opens with it already chosen: openingMission).
   */
  private showPremise(c: CampaignState): void {
    if ((c.missionsFlown ?? 0) > 0 || c.day > 1) return;
    let seen: string[] = [];
    try {
      seen = JSON.parse(localStorage.getItem(PREMISE_KEY) ?? '[]') as string[];
    } catch {
      /* private mode: shown again next time */
    }
    const uid = c.uid ?? '';
    if (seen.includes(uid)) return;
    try {
      localStorage.setItem(PREMISE_KEY, JSON.stringify([...seen, uid].slice(-20)));
    } catch {
      /* private mode */
    }
    this.alerts.unshift({
      tone: 'info',
      kicker: 'KESSRA',
      title: PREMISE.title,
      body: PREMISE.lines,
      text: '',
      action: c.activeMission ? { label: 'A la cochera', go: () => void this.go('squad') } : undefined,
    });
  }

  /** XCOM-style pop-ups for what happened while time passed. */
  private collectAlerts(prev: CampaignState, c: CampaignState): void {
    const fresh: Alert[] = [];
    // Each level adds a page to the Diario. After a mission it waits until the debrief is closed.
    for (let level = bastionLevel(prev) + 1; level <= bastionLevel(c); level++) {
      const page = EXPEDIENTE[level - 1]!;
      fresh.push({
        tone: 'good',
        kicker: `DIARIO · HOJA ${level}`,
        rank: 90,
        title: page.title,
        // One line of the page here; the whole page waits in the Diario (playtest: «mucho texto»).
        body: [page.summary],
        text: `Bastión nivel ${level}.`,
        unlocks: this.unlocksOf(level),
        note: page.note,
      });
    }
    // Story chapters and the final mission are announced whenever they open: after a level, a mission or an event.
    // Chapter 1 is already presented in squad select, immediately after Atalaya.
    for (const o of c.offers.filter((x) => (x.story || x.final) && x.story !== 1 && !prev.offers.some((p) => p.id === x.id))) {
      fresh.push({
        tone: o.final ? 'bad' : 'good',
        kicker: o.final ? 'DIAPASÓN LOCALIZADO' : `MISIÓN DE HISTORIA · HOJA ${o.story}`,
        rank: o.final ? 85 : 80,
        title: o.name,
        text: `${o.final ? 'Misión final' : MISSION_NAMES[o.kind]} en ${REGIONS[o.region].name}. ${offerBriefing(c, o)}`,
        note: offerSignature(c, o),
        action: { label: 'Ver en el valle', go: () => this.focusOffer(o.id) },
      });
    }
    // What the campaign logged as a pop-up: rumours, side quests and how they end. New entries sit at the front.
    const last = prev.log[0];
    const seen = last ? c.log.findIndex((e) => e.day === last.day && e.text === last.text) : c.log.length;
    for (const e of c.log.slice(0, seen === -1 ? c.log.length : seen).reverse()) {
      if (e.alert) fresh.push(this.logAlert(c, e.tone, e.alert, e.text));
    }
    // The clock as the days pass.
    if (c.day > prev.day) sound('ui.tick');
    // Missions take days too: what happened meanwhile pops up after the debrief.
    if (c.day > prev.day) this.timeAlerts(prev, c, fresh);
    this.flushAlerts(fresh);
  }

  /** What changed while time passed: new operations, works, Nidos, the monthly letter, the doom track. */
  private timeAlerts(prev: CampaignState, c: CampaignState, fresh: Alert[]): void {
    for (const o of c.offers.filter((x) => !x.story && !x.final && !x.quest && !prev.offers.some((p) => p.id === x.id))) {
      fresh.push({
        tone: o.facility ? 'bad' : 'info',
        kicker: o.facility ? 'OBJETIVO: NIDO' : 'NUEVA OPERACIÓN',
        rank: o.facility ? 65 : 40,
        title: o.name,
        text: `${MISSION_NAMES[o.kind]} en ${REGIONS[o.region].name}. ${offerBriefing(c, o)}`,
        action: { label: 'Ver en el valle', go: () => this.focusOffer(o.id) },
      });
    }
    // Works that were going on and are no longer: finished.
    for (const work of prev.base.works.filter((w) => !c.base.works.some((x) => x.plot === w.plot && x.kind === w.kind))) {
      const plot = plotById(c, work.plot)!;
      fresh.push({
        tone: 'good',
        kicker: 'OBRAS TERMINADAS',
        rank: 20,
        title: work.kind === 'clear' ? 'Terreno despejado' : work.kind === 'upgrade' ? `${FACILITIES[work.facility!].name} · nivel ${plot.level ?? 2}` : FACILITIES[work.facility!].name,
        text:
          work.kind === 'clear'
            ? `${capitalize(plotName(plot))} está lista para construir.`
            : work.kind === 'upgrade'
              ? FACILITIES[work.facility!].upgrades[(plot.level ?? 2) - 2]!
              : FACILITIES[work.facility!].description,
        action: { label: 'Ver instalaciones', go: () => this.pickPlot(work.plot, false) },
      });
    }
    for (const r of c.regions.filter((x) => x.facility && !prev.regions.find((p) => p.id === x.id)?.facility)) {
      fresh.push({
        tone: 'bad',
        kicker: 'NIDO',
        rank: 65,
        title: REGIONS[r.id].name,
        text: r.contacted
          ? `Los zombis se han juntado en un Nido. Hará avanzar ${doomSubject(bastionLevel(c))} hasta que lo queméis.`
          : 'Los zombis se han juntado en un Nido. Contactad con la zona para poder quemarlo.',
        action: { label: 'Ver en el valle', go: () => this.focusRegion(r.id) },
      });
    }
    if (c.nextSupplyDay !== prev.nextSupplyDay) {
      fresh.push({
        tone: 'good',
        kicker: 'FIN DE MES',
        rank: 10,
        title: 'Llega el convoy de Brena',
        text: `Ruth Halvorsen manda desde Brena ${c.credits - prev.credits} suministros. Cada zona contactada aumenta el envío.`,
      });
    }
    if (c.doom > prev.doom) {
      const level = bastionLevel(c);
      fresh.push({
        tone: 'bad',
        kicker: doomName(level).toUpperCase(),
        rank: 50,
        title: doomStatus(level, c.doom),
        text: `Si llega al Bastión, la campaña está perdida. Quemar Nidos la hace retroceder.`,
      });
    }
  }

  /** A pop-up the campaign logged: rumours (which can be listened to from it) and side quests. */
  private logAlert(c: CampaignState, tone: Alert['tone'], alert: { kicker: string; title: string; rumor?: string }, text: string): Alert {
    const rumor = alert.rumor ? c.rumors.find((r) => r.id === alert.rumor) : undefined;
    return {
      tone,
      kicker: alert.kicker,
      title: alert.title,
      text,
      rank: alert.kicker.startsWith('ENCARGO CUMPLIDO') || alert.kicker.startsWith('ENCARGO CERRADO') ? 70 : alert.kicker.startsWith('ENCARGO') ? 60 : 30,
      region: rumor?.region,
      action: rumor
        ? {
            label: `${this.decide('escuchar')} · ${missionsText(missionsFor(rumor.daysLeft))}`,
            go: () => {
              const now = this.state;
              if (now?.rumors.some((r) => r.id === rumor.id) && now.listening !== rumor.id) this.send({ type: 'investigate', id: rumor.id });
            },
          }
        : undefined,
    };
  }

  /**
   * One window per moment: the most important new alert pops up (unless one is
   * already waiting), and everything else goes to the Novedades tray.
   */
  private flushAlerts(fresh: Alert[]): void {
    if (!fresh.length) return;
    this.geo.setScanning(false);
    const sorted = [...fresh].sort((a, b) => (b.rank ?? 0) - (a.rank ?? 0));
    const [top, ...rest] = sorted;
    if (!this.alerts.length) this.alerts.push(top!);
    else rest.unshift(top!);
    this.news = [...rest, ...this.news].slice(0, MAX_NEWS);
  }

  private focusOffer(id: string): void {
    const o = this.offerById(id);
    if (!o) return;
    this.offer = id;
    this.region = o.region;
    this.geo.focus(o.region);
    void this.go('geoscape');
    this.blink('.strat-left .x-row.selected, .strat-right .x-title');
  }

  private focusRegion(id: RegionId): void {
    this.region = id;
    this.offer = null;
    this.geo.focus(id);
    void this.go('geoscape');
    this.blink('.strat-right .x-title');
  }

  // ------------------------------------------------------------- render

  private render(): void {
    const c = this.state;
    if (!c) return;
    // Re-rendering rebuilds the DOM: keep the caret in a text field the player is typing in.
    const typing = document.activeElement instanceof HTMLInputElement && this.root.contains(document.activeElement) ? document.activeElement : null;
    const keep = typing?.dataset.key ? { key: typing.dataset.key, start: typing.selectionStart, end: typing.selectionEnd } : null;
    // The radio waits for the debrief to be closed.
    this.radio.update(c, this.view === 'debrief' || this.reportPending(c));
    this.renderScreen(c);
    if (keep) {
      const field = this.root.querySelector<HTMLInputElement>(`input[data-key="${keep.key}"]`);
      field?.focus();
      if (field && keep.start !== null) field.setSelectionRange(keep.start, keep.end);
    }
  }

  private renderScreen(c: CampaignState): void {
    cancelAnimationFrame(this.anchorFrame);
    this.root.dataset.view = this.view;
    this.renderTop(c);
    this.renderProposal(c);
    this.renderNav(c);
    clear(this.viewEl);
    const room = this.partnerView ? ROOM_OF[this.partnerView] : undefined;
    const partner = this.partner();
    this.bastion.setPartner(partner?.connected && room ? room : null, partner?.color ?? '#fff', partner?.name ?? '');
    this.bastion.select(this.view === 'facilities' ? this.plot : null);
    this.bastion.setLabels(this.view === 'research' || (this.view === 'facilities' && !this.plot));
    switch (this.view) {
      case 'geoscape':
        this.renderGeoscape(c);
        break;
      case 'hub':
      case 'research':
        this.renderResearch(c);
        break;
      case 'engineering':
        this.renderEngineering(c);
        break;
      case 'barracks':
        this.renderBarracks(c);
        break;
      case 'facilities':
        this.renderFacilities(c);
        break;
      case 'squad':
        this.renderSquad(c);
        break;
      case 'debrief':
        this.renderDebrief(c);
        break;
    }
    this.renderModal(c);
  }

  /** The commanders' names, in seat order, for the radio's greeting. */
  private commanderNames(): string[] {
    const c = this.state;
    return (c?.players ?? []).map((slot) => this.players.find((p) => p.slot === slot)?.name ?? '').filter(Boolean);
  }

  private renderTop(c: CampaignState): void {
    clear(this.top);
    const pips = h('div.pips');
    for (let i = 0; i < DOOM_MAX; i++) pips.append(h(`i${i < c.doom ? '.on' : ''}`));
    append(
      this.top,
      h('div.strat-brand', { title: timeHelp(bastionLevel(c)) }, h('b', {}, 'BASTIÓN'), h('span', {}, outingsMark(c.missionsFlown ?? 0))),
      this.siegeEl(c),
      h(
        'div.strat-res',
        {},
        h('span.strat-credits', { title: 'Suministros: construir, comprar accesorios, reclutar y contactar zonas' }, h('i', {}, '§'), h('b', {}, String(c.credits)), this.owedEl(c, 'credits')),
        h('span.strat-credits.medicines', { title: 'Medicinas: se traen de los saqueos. Curan heridas y tratan a los mordidos.' }, h('i', {}, '✚'), h('b', {}, String(c.medicines ?? 0))),
        // Food and parts (docs/FASE4.md): the base eats every day; the Taller makes weapons and armour with parts.
        // Red too when what is left, with the Huerto's, does not cover the next meal.
        h(
          `span.strat-credits.food${c.hungry || (c.food ?? 0) + gardenYield(c) < dailyRations(c) ? '.x-bad' : ''}`,
          {
            title: c.hungry
              ? 'Raciones: no quedan. El Bastión pasa hambre: −10 de voluntad en las misiones y las heridas curan más despacio.'
              : (c.food ?? 0) + gardenYield(c) < dailyRations(c)
                ? `Raciones: el Bastión come ${dailyRations(c)} al día y no llegan para mañana. Sin ellas, pasa hambre.`
                : `Raciones: el Bastión come ${dailyRations(c)} al día`,
          },
          h('i', { 'aria-hidden': 'true' }),
          h('b', {}, String(c.food ?? 0)),
        ),
        h('span.strat-credits.parts', { title: 'Piezas: el Taller hace con ellas armas y armaduras mejores' }, h('i', { 'aria-hidden': 'true' }), h('b', {}, String(c.parts ?? 0)), this.owedEl(c, 'parts')),
        this.levelBadge(c),
      ),
      h(
        'div.strat-doom',
        { title: `${doomStatus(bastionLevel(c), c.doom)}. ${bastionLevel(c) >= PARTITURA_LEVEL ? GLOSSARY.Marea : 'Si se llena, la campaña está perdida.'} Quemar Nidos la hace retroceder.` },
        h('small', {}, doomName(bastionLevel(c)).toUpperCase()),
        pips,
        h('b', {}, bastionLevel(c) >= PARTITURA_LEVEL ? `${Math.max(0, DOOM_MAX - c.doom)} km` : `${c.doom}/${DOOM_MAX}`),
      ),
      h(
        'div.strat-players',
        {},
        ...this.players.map((p) => {
          const where = p.slot === this.mySlot ? 'tú' : !p.connected ? 'desconectado' : p.away ? 'ausente' : this.partnerView ? VIEW_NAMES[this.partnerView] : '';
          const ready = c.advanceReady[p.slot] || c.launchReady[p.slot];
          return h(
            `div.strat-player${p.connected ? '' : '.offline'}`,
            { style: { '--accent': p.color } },
            h('span.dot'),
            h('b', {}, p.name),
            // Without gender (Lore, 18/10): the player may be Ana.
            h('small', {}, ready ? 'a punto' : where),
          );
        }),
      ),
      this.news.length
        ? h('button.x-btn.strat-news', { onclick: () => ((this.newsOpen = !this.newsOpen), this.render()), title: 'Lo que ha pasado y no salió en una ventana' }, 'Novedades ', this.chip(String(this.news.length), 'warn'))
        : null,
      h('button.x-btn', { onclick: () => ((this.settingsOpen = true), this.render()), title: 'Gráficos y sonido, sin salir de la campaña' }, 'Ajustes'),
      h('button.x-btn', { onclick: () => ((this.leaving = true), this.render()), title: 'Volver al menú principal (la campaña queda guardada)' }, 'Menú'),
    );
  }

  /**
   * The next siege (docs/FASE12.md §11.5): when, and with the Sala de radio how
   * many and by where, in the strip; red the mission before, and while it is on.
   */
  private siegeEl(c: CampaignState): HTMLElement | null {
    if (!c.siege) return null;
    const now = c.offers.find((o) => o.siege);
    if (now) return h('div.strat-siege.now', { title: BASE_SIEGE_TEXT.defenders }, `${SIEGE_TEXT.name.toUpperCase()}${now.night ? ` · ${NIGHT_TEXT.label.toUpperCase()}` : ''}`);
    const heard = siegeHeard(c);
    const full = siegeWarning(heard.in, heard.count !== undefined ? { count: heard.count, sides: heard.sides ?? [] } : undefined);
    return h(`div.strat-siege${heard.in <= 1 ? '.near' : ''}`, { title: full }, siegeWarningShort(heard.in));
  }

  /** What a proposal costs, takes and does, and where to see it: whoever decides should know as much as whoever proposed. */
  /**
   * What a proposal costs and brings, one kind per line in the same order
   * (Tiempo, Suministros, Qué hace or Objetivo, Extra), and how to see what it is about.
   */
  private proposalDetail(c: CampaignState, action: NonNullable<CampaignState['proposal']>['action']): { rows: [string, string][]; see: (() => void) | null } {
    switch (action.type) {
      // The base (docs/FASE12.md §11): what the piece costs and does; the new base's screen will show where.
      case 'place': {
        const def = PIECES[action.piece];
        return {
          rows: [['Suministros', costText(def.cost.supplies)], ...(def.cost.parts ? [['Piezas', `${def.cost.parts}`] as [string, string]] : []), ['Qué hace', PIECE_TEXT[action.piece].line]],
          // Where it would go: on the clearing, in blue (ClearingSet.setProposal).
          see: () => {
            const [w, h] = footprint(action.piece, action.rot);
            if (this.view !== 'facilities') void this.go('facilities');
            this.clearing.focusTile({ x: action.x + (w - 1) / 2, y: action.y + (h - 1) / 2 });
          },
        };
      }
      case 'raise': {
        const p = c.bastion.pieces.find((x) => x.id === action.piece);
        const next = p && raiseCost(p);
        if (!p || !next || typeof next === 'string') return { rows: [], see: null };
        return { rows: [['Suministros', costText(next.cost)], ['Qué hace', PIECE_TEXT[p.piece].levels?.[p.level ?? 1] ?? PIECE_TEXT[p.piece].line]], see: () => {
            if (this.view !== 'facilities') void this.go('facilities');
            this.clearing.focusPiece(p.id);
          },
        };
      }
      case 'chooseMission': {
        const o = this.offerById(action.id);
        if (!o) return { rows: [], see: null };
        const reward = [o.reward.credits ? `${o.reward.credits} §` : '', o.reward.recruit ? 'un recluta' : ''].filter(Boolean).join(' y ');
        const info = missionInfo(c, o);
        return {
          rows: [
            // What there is to do there, in Lore's words (playtest 15/10 #4: «Qué hace» did not say what it was).
            ['Objetivo', operationAim(o.kind, { item: info.item, vip: info.vipName, alarm: info.alarm, facility: !!o.facility, final: o.final })],
            ['Extra', `${MISSION_NAMES[o.kind]} en ${REGIONS[o.region].name} · dificultad ${'★'.repeat(o.difficulty)}${reward ? ` · recompensa: ${reward}` : ''}`],
          ],
          see: () => this.focusOffer(o.id),
        };
      }
      case 'clear': {
        const plot = plotById(c, action.plot);
        if (!plot) return { rows: [], see: null };
        const { cost, days, salvage } = clearCost(plot);
        return {
          rows: [
            ['Tiempo', `${workDays(c, days)} días de obra; no hace pasar el tiempo`],
            ['Suministros', costText(cost)],
            ['Qué hace', clearAim(plot.terrain as 'rocks' | 'forest' | 'ruins', plotCode(plot))],
            ...(salvage ? [['Extra', `+${salvage} § de chatarra`] as [string, string]] : []),
          ],
          see: () => this.pickPlot(plot.id, false),
        };
      }
      case 'build': {
        const def = FACILITIES[action.facility];
        const plot = plotById(c, action.plot);
        const bonus = plot ? adjacencyAt(c, plot, def.id) : null;
        return {
          rows: [
            ['Tiempo', `${workDays(c, def.days)} días de obra; no hace pasar el tiempo`],
            ['Suministros', costText(def.cost)],
            ['Qué hace', def.description],
            ...(bonus ? [['Extra', `${cap(nextTo(bonus.next))}: ${bonus.text}`] as [string, string]] : []),
          ],
          see: () => this.pickPlot(action.plot, false),
        };
      }
      case 'upgrade': {
        const plot = plotById(c, action.plot);
        if (!plot?.building || !(plot.building in FACILITIES)) return { rows: [], see: null };
        const id = plot.building as FacilityId;
        const to = (plot.level ?? 1) + 1;
        const next = upgradeCost(id, to);
        return {
          rows: [
            ['Tiempo', `${workDays(c, next.days)} días de obra; no hace pasar el tiempo`],
            ['Suministros', costText(next.cost)],
            ['Qué hace', `Nivel ${to}: ${FACILITIES[id].upgrades[to - 2]}`],
          ],
          see: () => this.pickPlot(plot.id, false),
        };
      }
      case 'contact':
        return {
          rows: [
            ['Tiempo', 'Al momento'],
            ['Suministros', costText(contactCost(c))],
            ['Qué hace', contactAim(action.region, regionIncome(c, action.region))],
          ],
          see: () => this.focusRegion(action.region),
        };
      case 'investigate': {
        const rumor = c.rumors.find((r) => r.id === action.id);
        return rumor
          ? { rows: [['Tiempo', `${rumor.daysLeft} días escuchando; no hace pasar el tiempo`], ['Qué hace', rumorAim(REGIONS[rumor.region].name)]], see: () => this.focusRegion(rumor.region) }
          : { rows: [], see: null };
      }
      case 'event': {
        const def = eventDef(c);
        return { rows: def ? [['Qué hace', def.options[action.option].effect]] : [], see: null };
      }
      case 'craft': {
        // The Taller's work (docs/FASE4.md): placeholder rows until the Taller's screen shows it.
        const def = CRAFTS[action.project];
        return { rows: [['Tiempo', `Sale ${inMissions(missionsFor(def.days))}`], ['Suministros', `${costText(def.cost)} y ${def.parts} piezas`], ['Qué hace', craftAim(def.kind, def.the)]], see: null };
      }
    }
  }

  /** What a pending proposal will cost, in supplies and parts: it is paid when the partner says yes (playtest 16/10 #5). */
  private owed(c: CampaignState): { credits: number; parts: number } {
    const a = c.proposal?.action;
    if (!a) return { credits: 0, parts: 0 };
    return { credits: proposalCost(c, a), parts: a.type === 'craft' ? CRAFTS[a.project]?.parts ?? 0 : 0 };
  }

  /** «−80» after a store's number while a proposal would spend it, with who has to say yes. */
  private owedEl(c: CampaignState, store: 'credits' | 'parts'): HTMLElement | null {
    const n = this.owed(c)[store];
    if (!n || !c.proposal) return null;
    const mine = c.proposal.by === this.mySlot;
    const title = mine ? `Pendiente: se paga cuando ${this.partner()?.name ?? 'tu compañero'} diga que sí` : `Pendiente: se paga si aceptas la propuesta de ${this.playerName(c.proposal.by)}`;
    return h('small.strat-owed', { title }, `−${n}`);
  }

  private renderProposal(c: CampaignState): void {
    clear(this.proposalEl);
    const p = c.proposal;
    this.proposalEl.classList.toggle('show', !!p);
    if (!p) return;
    const { rows, see } = this.proposalDetail(c, p.action);
    const detail = rows.length ? h('dl.proposal-detail', {}, ...rows.flatMap(([label, text]) => [h('dt', {}, label), h('dd', {}, text)])) : null;
    const seeBtn = see ? h('button.x-btn.small', { onclick: see }, 'Ver') : null;
    // A plate like the alerts' (playtest 14/10, #22): who proposed on its edge, the sentence, the data, the answer at its foot.
    const mine = p.by === this.mySlot;
    this.proposalEl.classList.toggle('mine', mine);
    const kicker = h('div.proposal-kicker', {}, mine ? 'Tu propuesta' : 'Propuesta');
    // What it costs is paid on the yes, not on the proposal (playtest 16/10 #5: «el dinero no baja al pagar»).
    const owed = this.owed(c);
    const price = [owed.credits ? `${owed.credits} §` : '', owed.parts ? `${owed.parts} ${owed.parts === 1 ? 'pieza' : 'piezas'}` : ''].filter(Boolean).join(' y ');
    const cost = price ? h('small.proposal-cost', {}, mine ? `Se paga al aceptar: ${price}.` : `Si aceptas, se paga: ${price}.`) : null;
    if (mine) {
      append(
        this.proposalEl,
        h('div.proposal-text', {}, kicker, h('span', {}, `Has propuesto ${p.text}. Esperando a ${this.partner()?.name ?? 'tu compañero'}…`), cost, detail, proposalTimer(true)),
        h('div.proposal-actions', {}, h('button.x-btn', { onclick: () => this.send({ type: 'withdraw' }) }, 'Retirar')),
      );
    } else {
      append(
        this.proposalEl,
        h('div.proposal-text', {}, kicker, h('span', {}, h('b', { style: { color: this.playerColor(p.by) } }, this.playerName(p.by)), ` propone ${p.text}.`), cost, detail, proposalTimer(false)),
        h(
          'div.proposal-actions',
          {},
          seeBtn,
          h('button.x-btn.primary', { onclick: () => this.send({ type: 'vote', accept: true }) }, 'Aceptar'),
          h('button.x-btn.danger', { onclick: () => this.send({ type: 'vote', accept: false }) }, 'Rechazar'),
        ),
      );
    }
  }

  private renderNav(c: CampaignState): void {
    clear(this.nav);
    this.nav.classList.toggle('hidden', this.view === 'squad' || this.view === 'debrief');
    const partner = this.partner();
    const items = [...NAV];
    if (c.activeMission) items.push({ view: 'squad', label: 'Escuadra' });
    // A tab's number is only for news this player hasn't looked at yet, and goes once the screen is visited
    // (playtest: a permanent «1» for a missing recruit or a possible upgrade read as a bug). Promotions are the
    // exception: they wait for this player's hand, so they stay, marked ▲.
    const news = this.navNews(c);
    const pending = new Map<BaseView, { n: string; urgent: boolean; what: string[] }>();
    for (const [view, items] of news) {
      const fresh = items.filter((i) => !this.seenNews.has(i.key));
      if (fresh.length) pending.set(view, { n: String(fresh.length), urgent: fresh.some((i) => i.urgent), what: fresh.map((i) => i.label) });
    }
    const promotions = c.soldiers.filter((s) => this.commands(s.owner) && this.hasPoints(s));
    if (promotions.length) pending.set('barracks', { n: `▲${promotions.length}`, urgent: false, what: promotions.map((s) => `${s.name}: talento por elegir`) });
    for (const item of items) {
      const here = partner?.connected && this.partnerView === item.view;
      const todo = pending.get(item.view);
      this.nav.append(
        h(
          `button${this.view === item.view ? '.on' : ''}${item.view === 'squad' ? '.urgent' : ''}`,
          { 'data-view': item.view, onclick: () => void this.go(item.view), title: todo ? todo.what.join(' · ') : '', 'aria-label': item.label },
          item.label,
          todo ? h(`span.nav-count${todo.urgent ? '.urgent' : ''}`, {}, todo.n) : null,
          here ? h('span.partner-dot', { style: { '--accent': partner!.color }, title: `${partner!.name} está aquí` }, partner!.name.slice(0, 1).toUpperCase()) : null,
        ),
      );
    }
  }

  /** What is new on each screen, with a stable key so that seeing it once is enough. */
  private navNews(c: CampaignState): Map<BaseView, { key: string; label: string; urgent: boolean }[]> {
    return new Map([
      [
        'geoscape',
        [
          ...c.offers.map((o) => ({ key: `offer:${o.id}`, label: `Nueva operación: ${o.name}`, urgent: !!(o.story || o.final || o.facility) })),
          ...c.rumors.map((r) => ({ key: `rumor:${r.id}`, label: `Rumor en ${REGIONS[r.region].name}`, urgent: false })),
        ],
      ],
      [
        'facilities',
        c.base.plots
          .filter((p) => p.building && !(p.building in FIXED_NAMES_SET))
          .map((p) => ({ key: `built:${p.id}:${p.building}:${p.level ?? 1}`, label: `Obra terminada: ${buildingName(p.building!)}${(p.level ?? 1) > 1 ? ` · nivel ${p.level}` : ''}`, urgent: false })),
      ],
      ['research', [{ key: `level:${bastionLevel(c)}`, label: `Nueva hoja del Diario: nivel ${bastionLevel(c)}`, urgent: false }]],
    ]);
  }

  /** Marks the news of `view` as seen in this browser (per campaign), so its number goes. */
  private seeNews(view: BaseView): void {
    const c = this.state;
    if (!c) return;
    const items = this.navNews(c).get(view);
    if (!items?.some((i) => !this.seenNews.has(i.key))) return;
    for (const i of items) this.seenNews.add(i.key);
    this.saveSeenNews(c);
  }

  private loadSeenNews(c: CampaignState): void {
    const uid = c.uid ?? '';
    if (this.seenNewsFor === uid) return;
    this.seenNewsFor = uid;
    this.seenNews.clear();
    let stored: string[] | null = null;
    try {
      stored = JSON.parse(localStorage.getItem(NEWS_KEY) ?? 'null')?.[uid] ?? null;
    } catch {
      /* storage unavailable: news counts start from now */
    }
    // A campaign seen for the first time in this browser starts with nothing new: only what happens from now on counts.
    if (stored) for (const k of stored) this.seenNews.add(k);
    else for (const items of this.navNews(c).values()) for (const i of items) this.seenNews.add(i.key);
    this.saveSeenNews(c);
  }

  private saveSeenNews(c: CampaignState): void {
    try {
      const all = JSON.parse(localStorage.getItem(NEWS_KEY) ?? '{}') as Record<string, string[]>;
      // Keys of things long gone are dropped so the list doesn't grow forever.
      const live = new Set([...this.navNews(c).values()].flat().map((i) => i.key));
      all[c.uid ?? ''] = [...this.seenNews].filter((k) => live.has(k));
      localStorage.setItem(NEWS_KEY, JSON.stringify(all));
    } catch {
      /* storage unavailable: the counts reset on reload */
    }
  }

  // ---------------------------------------------------------------- hub

  /** The Bastion at a glance: only what is waiting, each line a shortcut to its screen. */
  /** Where a pending step takes you: its operation, soldier, room or region, else its screen. */
  private follow(step: NextStep): void {
    const go = step.go ?? {};
    if (go.offer) return this.focusOffer(go.offer);
    if (go.region) return this.focusRegion(go.region);
    if (go.soldier) {
      this.soldier = go.soldier;
      void this.go('barracks');
      return;
    }
    if (go.plot) return this.pickPlot(go.plot, false);
    if (go.piece) {
      if (this.view !== 'facilities') void this.go('facilities');
      this.clearing.focusPiece(go.piece);
      return;
    }
    if (step.view !== this.view) void this.go(step.view);
  }

  /**
   * The campaign at a glance, at the head of Campaña (the old «Bastión»,
   * Arturo 16/10 #33): "Ahora", the one next thing to do, then what else is
   * pending (each line a shortcut) and the calendar.
   */
  private overview(c: CampaignState): (HTMLElement | null)[] {
    const steps = nextSteps(c, this.mySlot);
    const icon: Record<BaseView, string> = {
      hub: 'clock',
      geoscape: 'flag',
      research: 'xp',
      engineering: 'flag',
      barracks: 'xp',
      facilities: 'pick',
      squad: 'flag',
      debrief: 'flag',
    };
    const [now, ...rest] = steps;
    // Steps on Campaña itself (a proposal, an event) are already on screen: they only inform.
    const here = (step: NextStep) => step.view === 'hub' || step.view === 'research';
    const nowEl = now
      ? h(
          `button.strat-now${now.tone === 'urgent' ? '.urgent' : ''}`,
          { disabled: here(now), onclick: () => this.follow(now) },
          h('small', {}, 'AHORA'),
          h('b', {}, now.label),
          here(now) ? null : arrowIcon('right'),
        )
      : null;
    const rows = rest.slice(0, 4).map((step) =>
      h(
        `${here(step) ? 'div' : 'button'}.x-row.agenda${step.tone === 'urgent' ? '.urgent' : ''}`,
        here(step) ? {} : { onclick: () => this.follow(step) },
        h(`span.agenda-icon.${icon[step.view]}`),
        h('b', {}, step.label),
        step.count && step.count > 1 ? this.chip(String(step.count)) : h('span'),
      ),
    );
    return [nowEl, rows.length ? h('div.x-head', { title: 'Pulsa una sala del Bastión para entrar' }, 'Agenda') : null, rows.length ? h('div.x-list', {}, ...rows) : null, ...this.calendar(c)];
  }

  /**
   * Calendario: what will happen and after how many missions (docs/FASE12.md
   * §11.6: time passes with each one; the days stay inside).
   */
  private calendar(c: CampaignState): HTMLElement[] {
    const dates: { day: number; icon: string; text: string; go?: () => void }[] = [];
    // Only the Taller's works are left (the base is built at once, on the clearing).
    for (const work of c.base.works.filter((w) => w.kind === 'craft' && w.project)) dates.push({ day: c.day + work.daysLeft, icon: 'pick', text: `${CRAFTS[work.project!].name}, del Taller` });
    for (const s of c.soldiers.filter((x) => x.alive && x.woundedDays > 0 && x.owner === this.mySlot)) dates.push({ day: c.day + s.woundedDays, icon: 'hp', text: `${s.name} vuelve al servicio` });
    // The bitten, both players': whoever has medicines can save them (docs/FASE3.md).
    for (const s of c.soldiers.filter((x) => x.alive && x.infection)) dates.push({ day: turnDay(c, s)!, icon: 'hp', text: `${s.name} se convierte si no recibe el tratamiento`, go: () => this.openSoldier(s.id) });
    const rumor = c.rumors.find((r) => r.id === c.listening);
    if (rumor) dates.push({ day: c.day + rumor.daysLeft, icon: 'will', text: `Se sabrá qué pasa en ${REGIONS[rumor.region].name}` });
    for (const o of c.offers.filter((x) => x.expiresDay !== null)) dates.push({ day: o.expiresDay!, icon: 'flag', text: `Caduca «${o.name}»`, go: () => this.focusOffer(o.id) });
    dates.push({ day: c.nextSupplyDay, icon: 'clock', text: 'Convoy de Brena' });
    dates.push({ day: c.nextDoomDay, icon: 'clock', text: `Avanza ${doomSubject(bastionLevel(c))}` });
    dates.sort((a, b) => a.day - b.day);
    return [
      h('div.x-head', { title: timeHelp(bastionLevel(c)) }, 'Calendario'),
      h(
        'div.x-list.calendar',
        {},
        ...dates.slice(0, 7).map((d) =>
          h(
            `${d.go ? 'button' : 'div'}.x-row.agenda.calendar-row`,
            d.go ? { onclick: d.go } : {},
            h(`span.agenda-icon.${d.icon}`),
            h('b', {}, d.text),
            this.chip(aheadChip(c, d.day), '', aheadText(c, d.day)),
          ),
        ),
      ),
      h('p.calendar-note', {}, 'El tiempo pasa con cada misión: las curas, las escuchas y los encargos avanzan con ellas.'),
    ];
  }

  // ---------------------------------------------------------- geoscape

  private renderGeoscape(c: CampaignState): void {
    this.geo.update(c, this.region);
    const offers = [...c.offers].sort(
      (a, b) =>
        Number(b.final) - Number(a.final) ||
        Number(!!b.facility) - Number(!!a.facility) ||
        Number(!!b.story) - Number(!!a.story) ||
        Number(!!b.quest) - Number(!!a.quest) ||
        (a.expiresDay ?? 999) - (b.expiresDay ?? 999),
    );
    const list = h('div.x-list');
    for (const o of offers) {
      const days = o.expiresDay === null ? null : missionsFor(o.expiresDay - c.day);
      list.append(
        h(
          `button.x-row.mission${this.offer === o.id ? '.selected' : ''}`,
          { onclick: () => this.focusOffer(o.id) },
          // data-kind: the theme draws the kind's icon from the packs over the glyph (ui/themes/mixta-base.css).
          h('span.mission-icon', { 'data-kind': o.kind, style: { '--icon': o.final || o.siege ? '#ff5a8a' : o.facility ? '#ff4a5a' : o.story ? '#4a7dff' : o.quest ? '#2f9e6a' : '#ffb030' } }, h('span', {}, KIND_ICON[o.kind])),
          h(
            'div',
            { title: o.siege ? `${SIEGE_TEXT.name} · El Bastión` : `${o.facility ? 'Nido' : o.story ? `Historia · ${MISSION_NAMES[o.kind]}` : o.quest ? `Encargo · ${MISSION_NAMES[o.kind]}` : MISSION_NAMES[o.kind]} · ${REGIONS[o.region].name}` },
            h('b', {}, o.name),
            h('small', {}, `${o.siege ? 'El Bastión' : REGIONS[o.region].name}${o.night ? ` · ${NIGHT_TEXT.label}` : ''}`),
          ),
          h(
            `span.x-side.days${days !== null && days <= 1 ? '.x-bad' : ''}`,
            { title: o.expiresDay === null ? 'No caduca' : `Caduca ${aheadText(c, o.expiresDay)}` },
            o.expiresDay === null ? '' : aheadChip(c, o.expiresDay),
          ),
        ),
      );
    }
    if (!offers.length) list.append(h('p.x-muted', {}, 'Ninguna a la vista.'));
    const threats = c.regions.filter((r) => r.facility);
    const network = c.regions.filter((r) => r.contacted).length;
    // Sections only appear when they have something in them.
    const left = this.panel(
      'geo-left',
      'left',
      h('div.x-head', {}, 'Operaciones'),
      list,
      ...(threats.length
        ? [
            h('div.x-head', { title: GLOSSARY.Nido }, 'Nidos'),
            h(
              'div.x-list',
              {},
              ...threats.map((r) =>
                h(
                  `button.x-row${this.region === r.id && !this.offer ? '.selected' : ''}`,
                  { style: { '--row-accent': '#ff4a5a' }, onclick: () => this.focusRegion(r.id) },
                  h('div', {}, h('b', {}, REGIONS[r.id].name)),
                  r.contacted ? this.chip('Asaltable', 'bad') : this.chip('Sin contacto', '', 'Contactad con la zona para poder asaltarlo'),
                ),
              ),
            ),
          ]
        : []),
      ...this.questRows(c),
      ...this.rumorRows(c),
      h('div.x-head', { title: 'Pulsa una zona del valle para ver sus datos' }, 'Red de enclaves'),
      this.meter(network, contactCapacity(c), 'Zonas contactadas / máximo (los centros de comunicaciones dan más)'),
    );
    this.viewEl.append(left);

    const offer = this.offerById(this.offer);
    if (offer) this.viewEl.append(this.panel('geo-right', 'right', ...this.briefing(c, offer)));
    else if (this.region) this.viewEl.append(this.panel('geo-right', 'right', ...this.regionInfo(c, this.region)));
    if (offer || this.region) {
      left.classList.add('has-detail');
      this.viewEl.append(
        h(
          'button.x-btn.strat-fold',
          {
            onclick: () => {
              this.offer = null;
              this.region = null;
              this.render();
            },
          },
          `‹ Operaciones · ${c.offers.length}`,
        ),
      );
    }

    // Time passes with the missions (docs/FASE12.md §11.6): «Esperar noticias» is gone.
  }

  /** Side quests under way, with the step they are at. */
  private questRows(c: CampaignState): HTMLElement[] {
    if (!c.quests.length) return [];
    return [
      h('div.x-head', { title: GLOSSARY.Encargo }, 'Encargos'),
      h(
        'div.x-list',
        {},
        ...c.quests.map((q) => {
          const def = QUESTS[q.id];
          const offer = c.offers.find((o) => o.quest === q.id);
          const where = offer ? REGIONS[offer.region].name : 'Esperando vuestra decisión';
          const whose = q.owner !== null && c.players.length > 1 ? ` · de ${this.playerName(q.owner)}` : '';
          return h(
            `button.x-row${offer && this.offer === offer.id ? '.selected' : ''}`,
            { style: { '--row-accent': '#2f9e6a' }, onclick: () => (offer ? this.focusOffer(offer.id) : undefined) },
            h('div', {}, h('b', {}, def.title), h('small', {}, `Paso ${q.step + 1} de ${def.steps.length} · ${where}${whose}`)),
            h('span.x-side', {}, offer?.expiresDay != null ? aheadChip(c, offer.expiresDay) : '—'),
          );
        }),
      ),
    ];
  }

  /** Rumours on the geoscape: listening to one for a few days tells what it was. */
  private rumorRows(c: CampaignState): HTMLElement[] {
    if (!c.rumors.length) return [];
    return [
      h('div.x-head', { title: `${GLOSSARY.Rumor} ${c.listening ? 'Solo se escucha uno a la vez.' : 'Se enfría si nadie lo escucha.'}` }, 'Rumores'),
      h(
        'div.x-list',
        {},
        ...c.rumors.map((r) => {
          const listening = c.listening === r.id;
          const blocked = this.why([!!c.proposal, 'Hay una propuesta pendiente']);
          return h(
            `div.x-row${this.region === r.id && !this.offer ? '.selected' : ''}`,
            {},
            h(
              'div',
              {},
              h('b', {}, REGIONS[r.region].name),
              h('small', {}, `Se habla de ${r.text}.`),
              listening
                ? h('small.x-good', {}, `Escuchando · ${missionsText(missionsFor(r.daysLeft))}`)
                : h(
                    'button.x-btn.small',
                    { disabled: !!blocked, title: blocked || `Se sabrá ${inMissions(missionsFor(r.daysLeft))}`, onclick: () => this.send({ type: 'investigate', id: r.id }) },
                    `${this.decide('escuchar')} · ${missionsText(missionsFor(r.daysLeft))}`,
                  ),
            ),
            // The clock is always "caduca en", as for the operations.
            h('span.x-side.days', { title: `Caduca ${aheadText(c, r.expiresDay)}` }, listening ? '' : aheadChip(c, r.expiresDay)),
          );
        }),
      ),
    ];
  }

  private briefing(c: CampaignState, o: MissionOffer): (HTMLElement | null)[] {
    const r = o.reward;
    const rewards = [
      r.credits ? `${r.credits} suministros` : '',
      r.recruit ? 'un recluta' : '',
      r.doom ? `${doomSubject(bastionLevel(c))} retrocede ${r.doom}` : '',
      o.story ? (STORY[o.story - 1]!.prize ?? '') : '',
    ];
    const quest = o.quest ? c.quests.find((q) => q.id === o.quest) : undefined;
    if (quest) rewards.push(`Al final del encargo: ${fill(QUESTS[quest.id].prize, quest.names)}`);
    const chosen = c.activeMission === o.id;
    const biome = o.map.kind === 'generated' ? BIOME_NAMES[o.map.biome] : 'Distrito Comercial';
    // The siege (docs/FASE12.md §11.5) is fought at home: no region's ground, nothing that expires, no reward but holding.
    if (o.siege) {
      return [
        h('div.alert-kicker', { className: 'alert-kicker bad' }, SIEGE_TEXT.name.toUpperCase()),
        h('h2.x-title', {}, o.name),
        h('p.x-sub', {}, o.night ? `El Bastión · ${NIGHT_TEXT.label}` : 'El Bastión'),
        h('p.x-text', {}, offerBriefing(c, o)),
        offerSignature(c, o) ? h('p.x-note', {}, offerSignature(c, o)) : null,
        h(
          'dl.briefing',
          {},
          h('dt', {}, 'Tipo'),
          h('dd', {}, SIEGE_TEXT.name),
          h('dt', {}, 'Dificultad'),
          h('dd', {}, h('span.stars', {}, '★'.repeat(o.difficulty)), ` ${DIFFICULTY[o.difficulty]}`),
        ),
        chosen ? h('button.x-btn.primary', { onclick: () => void this.go('squad') }, 'Ir a la cochera') : this.deployButton(c, o),
      ];
    }
    return [
      o.story === 1 ? null : h(
        'div.alert-kicker',
        {
          className: `alert-kicker${o.facility || o.final ? ' bad' : o.story || o.quest ? ' good' : ''}`,
          title: quest ? 'Si se pierde o caduca, el encargo se pierde con sus consecuencias' : '',
        },
        o.final
          ? 'MISIÓN FINAL'
          : o.facility
            ? 'NIDO'
            : o.story
              ? `MISIÓN DE HISTORIA · HOJA ${o.story}`
              : o.quest
                ? `ENCARGO · ${QUESTS[o.quest].title.toUpperCase()}`
                : 'OPERACIÓN',
      ),
      h('h2.x-title', {}, o.name),
      h('p.x-sub', {}, `${REGIONS[o.region].name} · ${biome}${o.night ? ` · ${NIGHT_TEXT.label}` : ''}${quest ? ` · paso ${quest.step + 1} de ${QUESTS[quest.id].steps.length}` : ''}`),
      h('p.x-text', {}, offerBriefing(c, o)),
      offerSignature(c, o) ? h('p.x-note', {}, offerSignature(c, o)) : null,
      h(
        'dl.briefing',
        {},
        h('dt', {}, 'Tipo'),
        h('dd', {}, MISSION_NAMES[o.kind]),
        h('dt', {}, 'Dificultad'),
        h('dd', {}, h('span.stars', {}, '★'.repeat(o.difficulty)), ` ${DIFFICULTY[o.difficulty]}`),
        h('dt', {}, 'Caduca'),
        h('dd', {}, o.expiresDay === null ? 'No caduca' : aheadText(c, o.expiresDay)),
        h('dt', {}, 'Recompensa'),
        h('dd', {}, rewards.filter(Boolean).join(' · ') || 'La victoria'),
      ),
      chosen ? h('button.x-btn.primary', { onclick: () => void this.go('squad') }, 'Ir a la cochera') : this.deployButton(c, o),
    ];
  }

  private deployButton(c: CampaignState, o: MissionOffer): HTMLElement {
    const blocked = this.why([!!c.activeMission, 'Ya hay otra misión elegida'], [!!c.proposal, 'Hay una propuesta pendiente']);
    return h(
      'div',
      {},
      h('button.x-btn.primary', { disabled: !!blocked, onclick: () => this.send({ type: 'chooseMission', id: o.id }) }, this.decide('desplegar')),
      this.whyEl(blocked),
    );
  }

  /** Where the region's own side quest stands, as a hint to listen to its rumours. */
  private regionQuest(c: CampaignState, id: RegionId): HTMLElement | null {
    const quest = (Object.keys(QUESTS) as (keyof typeof QUESTS)[]).find((q) => QUESTS[q].rumor && QUESTS[q].region === id);
    if (!quest) return null;
    const def = QUESTS[quest];
    const done = c.questLog[quest];
    const contacted = !!c.regions.find((r) => r.id === id)?.contacted;
    const state = c.quests.some((q) => q.id === quest)
      ? 'en curso'
      : done
        ? (def.outcomes[done.outcome] ?? def.outcomes.failure).tone === 'good'
          ? 'cumplido'
          : 'cerrado'
        : contacted
          ? 'escuchad los rumores de la zona para encontrarlo'
          : 'contactad con la zona para que os lo cuenten';
    return h('p.x-muted', {}, `Encargo de la zona: ${done || c.quests.some((q) => q.id === quest) ? `«${def.title}», ` : ''}${state}.`);
  }

  private regionInfo(c: CampaignState, id: RegionId): (HTMLElement | null)[] {
    const def = REGIONS[id];
    const r = c.regions.find((x) => x.id === id)!;
    const contacted = c.regions.filter((x) => x.contacted).length;
    const capacity = contactCapacity(c);
    const cost = contactCost(c);
    const flooded = underMarea(c, id);
    const blockedContact = this.why(
      [!r.contacted && !!contactBlocker(c, id), contactBlocker(c, id) ?? ''],
      [contacted >= capacity, 'Construid un centro de comunicaciones para más contactos'],
      [!!c.proposal, 'Hay una propuesta pendiente'],
      [!canAfford(c, cost), this.shortfall(cost)],
    );
    return [
      h(`div.alert-kicker${flooded ? '.bad' : ''}`, {}, flooded ? doomName(bastionLevel(c)).toUpperCase() : r.contacted ? 'ZONA CONTACTADA' : 'ZONA SIN CONTACTO'),
      h('h2.x-title', {}, def.name),
      h('p.x-text', {}, REGION_LORE[id].situation),
      flooded ? h('p.x-text.x-bad', {}, floodedZoneNote(bastionLevel(c))) : null,
      h('p.x-muted', { title: 'Suministros que manda con cada convoy mensual una vez contactada' }, `${REGION_LORE[id].ally} · +${regionIncome(c, id)} § al mes`),
      this.regionQuest(c, id),
      r.facility ? h('p.x-text.x-bad', {}, `Nido: hace avanzar ${doomSubject(bastionLevel(c))} cada ${missionsText(missionsFor(14))}.${r.contacted ? '' : ' Contactad para quemarlo.'}`) : null,
      r.contacted
        ? null
        : h(
            'button.x-btn.primary',
            { disabled: !!blockedContact, onclick: () => this.send({ type: 'contact', region: id }) },
            `${this.decide('contactar')} · ${costText(cost)}`,
          ),
      r.contacted ? null : this.whyEl(blockedContact),
      r.contacted ? null : this.meter(contacted, capacity, 'Zonas contactadas / máximo'),
    ];
  }

  // ---------------------------------------------------------- progress

  /** The old laboratory: the Bastion's level track and what each level unlocks. */
  private renderResearch(c: CampaignState): void {
    const level = bastionLevel(c);
    this.levelPick ??= Math.min(level + 1, MAX_LEVEL);
    const next = LEVEL_XP[level];
    // One line per level: number, page title once reached, how many things it opens.
    const rows = LEVEL_XP.map((xp, i) => {
      const n = i + 1;
      const reached = n <= level;
      const unlocks = levelUnlocks(n);
      return h(
        `button.x-row.level${this.levelPick === n ? '.selected' : ''}${reached ? '.reached' : '.dim'}`,
        { onclick: () => ((this.levelPick = n), this.render()), title: unlocks.join(' · ') || 'El Bastión entra en servicio' },
        h('span.level-num', {}, String(n)),
        h('b', {}, reached ? EXPEDIENTE[i]!.title : unlocks.length ? `${unlocks.length} ${unlocks.length === 1 ? 'novedad' : 'novedades'}` : 'Sin novedades'),
        h('span.x-side', {}, reached ? '✓' : `${xp} XP`),
      );
    });
    const xpHelp = 'Cada misión da experiencia: +2 por ir, +3 y su dificultad si se gana, y +1 por cada baja. La Sala de mapas da un 25 % más.';
    this.viewEl.append(
      this.panel(
        'progress-left',
        'left',
        ...this.overview(c),
        h('div.x-head', { title: xpHelp }, `Bastión · nivel ${level}`),
        next === undefined ? h('p.x-muted', {}, 'Nivel máximo.') : this.bar(c.xp - LEVEL_XP[level - 1]!, next - LEVEL_XP[level - 1]!),
        next === undefined ? null : h('p.x-muted', { title: xpHelp }, `${c.xp} XP · faltan ${next - c.xp} para el nivel ${level + 1}`),
        h('div.x-list', {}, ...rows),
        ...this.radioArchive(c),
      ),
    );
    const n = this.levelPick;
    const reached = n <= level;
    const techs = Object.values(TECHS).filter((t) => t.level === n);
    const items = Object.values(ITEMS).filter((i) => i.level === n);
    // The base's pieces it opens (docs/FASE12.md §11; progression.ts BASE_UNLOCKS).
    const pieces = (Object.keys(PIECES) as (keyof typeof PIECES)[]).filter((id) => PIECES[id].level === n && id !== 'caravan');
    const KIND_LABEL = { building: 'Edificio', defence: 'Defensa', trap: 'Automática' } as const;
    const page = EXPEDIENTE[n - 1]!;
    const line = (kind: string, name: string, detail: string, tag: HTMLElement | null = null) =>
      h('div.x-row.unlock', { title: detail }, h(`span.agenda-icon.${kind}`), h('div', {}, h('b', {}, name), h('small', {}, detail), tag));
    this.viewEl.append(
      this.panel(
        'progress-right',
        'right',
        h('div.alert-kicker', { className: `alert-kicker${reached ? ' good' : ''}` }, reached ? `ALCANZADO · DIARIO, HOJA ${n}` : `FALTAN ${LEVEL_XP[n - 1]! - c.xp} XP`),
        h('h2.x-title', {}, reached ? page.title : `Nivel ${n}`),
        ...(reached ? [...page.paragraphs.map((p) => h('p.x-text.page', {}, p)), h('p.x-note', {}, page.note)] : []),
        techs.length || items.length || pieces.length ? h('div.x-group', {}, reached ? 'Desbloqueado' : 'Desbloquea') : null,
        ...techs.map((t) => line('xp', t.name, t.description)),
        ...items.map((i) => line('flag', i.name, i.description, this.chip(costText(i.cost), '', 'En la Armería'))),
        ...pieces.map((id) => line('pick', PIECE_TEXT[id].name, PIECE_TEXT[id].line, this.chip(KIND_LABEL[PIECES[id].kind], '', 'Se construye en el claro'))),
        !techs.length && !items.length && !pieces.length ? h('p.x-muted', {}, 'Lo básico: armas convencionales, kevlar, granadas, humo y botiquines.') : null,
      ),
    );
  }

  /** Every transmission heard so far, to play again; the ones still waiting their moment are marked. */
  private radioArchive(c: CampaignState): HTMLElement[] {
    const entries = this.radio.archive(c);
    if (!entries.length) return [];
    return [
      h('div.x-head', { title: 'Las conversaciones por radio del Bastión. Pulsa una para volver a oírla.' }, 'Archivo de transmisiones'),
      h(
        'div.x-list',
        {},
        ...entries.map(({ scene, fresh }) =>
          h(
            'button.x-row',
            { title: scene.channel, onclick: () => this.radio.replay(c, scene.id, () => this.render()) },
            h('div', {}, h('b', {}, scene.title)),
            fresh ? this.chip('Nueva', 'good', 'Todavía no ha sonado') : h('span.x-side', {}, '▸'),
          ),
        ),
      ),
    ];
  }

  // ------------------------------------------------------------ armoury

  /** The old engineering bay: weapon and armour tiers, and the accessory shop. */
  private renderEngineering(c: CampaignState): void {
    const tabs = h(
      'div.x-tabs',
      {},
      ...(['items', 'gear'] as const).map((t) =>
        h(`button${this.engTab === t ? '.on' : ''}`, { onclick: () => ((this.engTab = t), (this.engPick = null), this.render()) }, t === 'gear' ? 'Armamento' : 'Accesorios'),
      ),
    );
    const rows: HTMLElement[] = [];
    if (this.engTab === 'gear') {
      // No weapon tiers any more (docs/FASE9.md §5): the armour's, made in the Taller, for everyone.
      const tiers: [string, string, number, boolean][] = [
        ['a1', ARMOR_TIERS[0]!.name, 1, true],
        ['a2', ARMOR_TIERS[1]!.name, TECHS.plateArmor.level, baseArmorTier(c) >= 2],
        ['a3', ARMOR_TIERS[2]!.name, TECHS.predatorArmor.level, baseArmorTier(c) >= 3],
      ];
      this.engPick ??= (c.foundWeapons ?? [])[0] ? `f:${c.foundWeapons![0]!.id}` : 'a2';
      // The Armería's weapons first (docs/FASE8.md §A, FASE9.md §5): each for one soldier; its state and improvements beside its name.
      const found = c.foundWeapons ?? [];
      if (found.length) rows.push(h('div.x-group', {}, 'En la Armería'));
      for (const w of found) {
        const holder = w.holder ? c.soldiers.find((x) => x.id === w.holder) : undefined;
        rows.push(
          h(
            `button.x-row${this.engPick === `f:${w.id}` ? '.selected' : ''}`,
            { onclick: () => ((this.engPick = `f:${w.id}`), this.render()) },
            h('b', {}, weaponIcon(kindOf(w.kind)), FOUND_WEAPONS[w.kind].name, weaponMarks(w)),
            h('span.x-side', {}, h('small.x-place', {}, placeOf(w.kind)), holder ? displayName(holder.name, holder.nickname) : 'En la Armería'),
          ),
        );
      }
      // The improvements loose on the rack, to put on a firearm.
      const loose = (Object.keys(WEAPON_MODS) as WeaponModId[]).filter((m) => (c.weaponMods?.[m] ?? 0) > 0);
      if (loose.length) rows.push(h('div.x-group', {}, 'Mejoras sueltas'));
      for (const m of loose)
        rows.push(
          h(
            `button.x-row${this.engPick === `x:${m}` ? '.selected' : ''}`,
            { onclick: () => ((this.engPick = `x:${m}`), this.render()) },
            h('b', {}, weaponIcon('mod'), WEAPON_MODS[m].name),
            h('span.x-side', {}, `× ${c.weaponMods![m]}`),
          ),
        );
      rows.push(h('div.x-group', {}, 'Armadura'));
      for (const [key, name, lvl, unlocked] of tiers) {
        const tier = Number(key[1]);
        rows.push(
          h(
            `button.x-row.tier-row${this.engPick === key ? '.selected' : ''}${unlocked ? '' : '.dim'}`,
            { onclick: () => ((this.engPick = key), this.render()) },
            this.tierMarks(tier),
            h('b', {}, name),
            unlocked ? h('span.x-side', {}, '✓') : this.craftChip(c, key) ?? this.chip(`Nv ${lvl}`, '', `Nivel ${lvl} del Bastión, y hacerla en el Taller`),
          ),
        );
      }
      // The Taller's weapons, munition and improvements (docs/FASE8.md §A, FASE9.md §5): munition three times, the rest again and again.
      rows.push(h('div.x-group', {}, 'Hacer en el Taller'));
      for (const def of Object.values(CRAFTS).filter((d) => d.kind === 'arms' || d.kind === 'ammo' || d.kind === 'mod')) {
        const work = c.base.works.find((w) => w.project === def.id);
        const done = def.kind === 'ammo' && c.crafted?.includes(def.id);
        rows.push(
          h(
            `button.x-row${this.engPick === `m:${def.id}` ? '.selected' : ''}${done || !craftBlocker(c, def.id) || work ? '' : '.dim'}`,
            { onclick: () => ((this.engPick = `m:${def.id}`), this.render()) },
            h('b', {}, weaponIcon(def.kind === 'mod' ? 'mod' : def.kind === 'ammo' || !def.makes ? 'ammo' : kindOf(def.makes)), def.name),
            h('span.x-side', {}, done ? '✓' : work ? this.chip(`Taller · ${missionsText(missionsFor(work.daysLeft))}`, 'good') : def.makes ? h('small.x-place', {}, placeOf(def.makes)) : ''),
          ),
        );
      }
    } else {
      // Only what goes on the belt (docs/FASE9.md §4): the special rounds are the weapons' improvements now.
      for (const item of Object.values(ITEMS).filter((i) => i.slot === 'utility')) {
        const unlocked = itemUnlocked(c, item.id);
        this.engPick ??= item.id;
        rows.push(
          h(
            `button.x-row${this.engPick === item.id ? '.selected' : ''}${unlocked ? '' : '.dim'}`,
            { onclick: () => ((this.engPick = item.id), this.render()) },
            h(
              'div',
              {},
              h('b', {}, item.name),
              h('small', {}, unlocked ? this.chip(costText(shopPrice(c, item.id))) : this.chip(`Nv ${item.level}`, '', `Se desbloquea con el nivel ${item.level} del Bastión`)),
            ),
            h('span.x-side', { title: 'Se equipan en el Cuartel' }, this.stockText(c, item.id)),
          ),
        );
      }
    }
    const note = this.engTab === 'items' && facilityCount(c, 'workshop') ? `Taller: −${Math.round((1 - shopPrice(c, 'smokeGrenade') / ITEMS.smokeGrenade.cost) * 100)} %` : '';
    this.viewEl.append(this.panel('eng-left', 'left', h('div.x-head', {}, 'Armería'), tabs, h('div.x-list', {}, ...rows), note ? h('p.x-muted', {}, note) : null));
    if (!this.engPick) return;
    if (this.engTab === 'gear') {
      const panel = this.engPick.startsWith('f:')
        ? this.foundPanel(c, this.engPick.slice(2))
        : this.engPick.startsWith('x:')
          ? modPanel(c, this.mySlot, this.engPick.slice(2) as WeaponModId, (cmd) => this.send(cmd), (s) => displayName(s.name, s.nickname), this.connected())
          : this.engPick.startsWith('m:')
            ? this.makePanel(c, this.engPick.slice(2) as CraftId)
            : this.tierPanel(c, this.engPick);
      // .item-art: the theme's picture of it, in the panel's corner (ui/themes/mixta-base.css).
      const pick = this.engPick;
      const found = pick.startsWith('f:') ? (c.foundWeapons ?? []).find((x) => x.id === pick.slice(2)) : undefined;
      const made = pick.startsWith('m:') ? CRAFTS[pick.slice(2) as CraftId] : undefined;
      // The weapon's own drawing (strategy/arms.ts): found or made by its kind, munition its bullets.
      const artKind = found ? `w-${kindOf(found.kind)}` : made ? (made.makes ? `w-${kindOf(made.makes)}` : made.kind === 'mod' ? 'weapon' : 'ammo') : pick.startsWith('a') ? 'armor' : 'weapon';
      const art = h(`div.item-art${found && FOUND_WEAPONS[found.kind].better ? '.better' : ''}`, { 'data-item': artKind, 'aria-hidden': 'true' });
      this.viewEl.append(this.panel('eng-right', 'right', art, ...panel));
      return;
    }
    const item = ITEMS[this.engPick as ItemId];
    if (!item) return;
    const unlocked = itemUnlocked(c, item.id);
    const cost = shopPrice(c, item.id);
    this.viewEl.append(
      this.panel(
        'eng-right',
        'right',
        h('div.item-art', { 'data-item': item.id, 'aria-hidden': 'true' }),
        h('div.alert-kicker', {}, 'ACCESORIO · CINTURÓN'),
        h('h2.x-title', {}, item.name),
        h('p.x-text', {}, item.description),
        h(
          'p.x-chips',
          {},
          this.chip(this.stockText(c, item.id) || 'No tienes ninguno', '', 'Se equipa en el Cuartel; si el soldado cae, se pierde'),
          unlocked ? this.chip(costText(cost), '', 'No necesita votación') : this.chip(`Nivel ${item.level} del Bastión`, 'warn'),
        ),
        unlocked ? h('button.x-btn.primary', { disabled: !canAfford(c, cost), onclick: () => this.send({ type: 'buy', item: item.id }) }, 'Comprar') : null,
        unlocked ? this.whyEl(this.shortfall(cost)) : null,
      ),
    );
  }

  /** The Taller's project that makes a tier (docs/FASE4.md). */
  private craftFor(kind: 'weapon' | 'armor', tier: number): CraftDef | undefined {
    return Object.values(CRAFTS).find((d) => d.kind === kind && d.tier === tier);
  }

  /** On the Armería's list: the days left while the Taller makes it, or nothing. */
  private craftChip(c: CampaignState, key: string): HTMLElement | null {
    const def = this.craftFor(key[0] === 'w' ? 'weapon' : 'armor', Number(key[1]));
    const work = def && c.base.works.find((w) => w.project === def.id);
    return work ? this.chip(`Taller · ${missionsText(missionsFor(work.daysLeft))}`, 'good', `El Taller está en ello: sale ${inMissions(missionsFor(work.daysLeft))}.`) : null;
  }

  /** Make a tier in the Taller: a shared decision, like building, then a work for a crew. */
  private craftRow(c: CampaignState, kind: 'weapon' | 'armor', tier: number): HTMLElement | null {
    const def = this.craftFor(kind, tier);
    if (!def) return null;
    const work = c.base.works.find((w) => w.project === def.id);
    if (work) return h('p.x-text', {}, `El Taller está en ello: sale ${inMissions(missionsFor(work.daysLeft))}.`);
    const blocker = craftBlocker(c, def.id);
    return h(
      'div',
      {},
      h('div.x-group', {}, 'Hacerla en el Taller'),
      h('p.x-chips', {}, this.chip(costText(def.cost)), this.chip(`${def.parts} piezas`, '', `Tenéis ${c.parts ?? 0}`), this.chip(missionsText(missionsFor(def.days)), '', `Sale ${inMissions(missionsFor(def.days))}`)),
      h('button.x-btn.primary', { disabled: !!blocker, title: blocker ?? '', onclick: () => this.send({ type: 'craft', project: def.id }) }, `Hacer ${def.name.toLowerCase()}`),
      blocker ? h('p.x-muted', {}, blocker) : null,
    );
  }

  /** A weapon or the munition the Taller can make (docs/FASE8.md §A). */
  private makePanel(c: CampaignState, id: CraftId): (HTMLElement | null)[] {
    const def = CRAFTS[id];
    if (!def) return [];
    const work = c.base.works.find((w) => w.project === def.id);
    const done = def.kind === 'ammo' && c.crafted?.includes(def.id);
    const blocker = craftBlocker(c, def.id);
    const text =
      def.kind === 'ammo'
        ? `Un cargador de repuesto más para cada soldado que salga de misión. Ahora cada uno sale con ${spareMags(c)} de repuesto.`
        : def.kind === 'mod' && def.makesMod
          ? `${WEAPON_MODS[def.makesMod].line} Una para la Armería: va en cualquier arma de fuego, ${MOD_SLOTS} mejoras como mucho en cada una. Se puede hacer tantas veces como haga falta.`
          : `Una para la Armería, para el soldado que la lleve. Se puede hacer tantas veces como haga falta.`;
    return [
      h('div.alert-kicker', {}, def.kind === 'ammo' ? 'TALLER · MUNICIÓN' : def.kind === 'mod' ? 'TALLER · MEJORA' : 'TALLER · ARMA'),
      h('h2.x-title', {}, def.name),
      h('p.x-text', {}, text),
      // What the weapon it makes does, as it comes out (Arturo, 17/10: «para saber qué elegir»).
      def.kind === 'arms' && def.makes ? weaponStats(weaponProfile(def.makes)) : null,
      done
        ? h('p.x-muted', {}, 'Ya está hecha.')
        : work
          ? h('p.x-text', {}, `El Taller está en ello: sale ${inMissions(missionsFor(work.daysLeft))}.`)
          : h(
              'div',
              {},
              h('p.x-chips', {}, this.chip(costText(def.cost)), this.chip(`${def.parts} piezas`, '', `Tenéis ${c.parts ?? 0}`), this.chip(missionsText(missionsFor(def.days)), '', `Sale ${inMissions(missionsFor(def.days))}`)),
              h('button.x-btn.primary', { disabled: !!blocker, title: blocker ?? '', onclick: () => this.send({ type: 'craft', project: def.id }) }, `Hacer ${def.the}`),
              blocker ? h('p.x-muted', {}, blocker) : null,
            ),
    ];
  }

  /** A weapon of the Armería: who can carry it (any of your soldiers) and who does. */
  private foundPanel(c: CampaignState, id: string): (HTMLElement | null)[] {
    const w = (c.foundWeapons ?? []).find((x) => x.id === id);
    if (!w) return [h('p.x-muted', {}, 'Esa arma ya no está.')];
    const def = FOUND_WEAPONS[w.kind];
    const holder = w.holder ? c.soldiers.find((x) => x.id === w.holder) : undefined;
    // One's own, then the partner's while they're gone (their colour and name say whose).
    const mine = c.soldiers.filter((s) => s.alive && this.commands(s.owner)).sort((a, b) => Number(a.owner !== this.mySlot) - Number(b.owner !== this.mySlot));
    const melee = def.slot === 'melee';
    const carries = (s: CampaignState['soldiers'][number]) => (melee ? (meleeWeaponOf(c, s) ? 'Machete' : 'Cuchillo') : weaponLabel(c, s));
    return [
      h('div.alert-kicker', {}, `${def.better ? 'ARMA BUENA' : melee ? 'CUERPO A CUERPO' : 'ARMA'} · ${placeOf(w.kind).toUpperCase()}`),
      h('h2.x-title', {}, def.name),
      h(
        'p.x-text',
        {},
        melee
          ? 'Para un soldado, en lugar del cuchillo. Si cae, se pierde.'
          : `Para un soldado: la lleva en la mano y la pistola al cinto.${def.better && def.mod ? ` Vino con ${WEAPON_MODS[def.mod].name.toLowerCase()}.` : ''} Si cae, se pierde.`,
      ),
      // What it does, as it is (Arturo, 17/10: «para saber qué elegir»); against each soldier's own, below.
      weaponStats(profileOf(w)),
      // Fase 9: its state, «Arreglar» and its improvements (strategy/arms.ts).
      ...weaponCare(c, this.mySlot, w, (cmd) => this.send(cmd), this.connected()),
      h('div.x-group', {}, 'La lleva'),
      holder
        ? h(
            'div.x-gear',
            {},
            h('span', {}, displayName(holder.name, holder.nickname)),
            this.commands(holder.owner) ? h('button.x-btn.small', { onclick: () => this.send({ type: 'assignWeapon', weapon: w.id, soldier: null }) }, 'Quitársela') : null,
          )
        : h('p.x-muted', {}, 'Nadie: está en la Armería.'),
      h('div.x-group', {}, mine.some((s) => s.owner !== this.mySlot) ? `Tus soldados y los de ${this.partner()?.name ?? 'tu compañero'}` : 'Tus soldados'),
      mine.length
        ? h(
            'div.x-list',
            {},
            ...mine.map((s) =>
              h(
                'div.x-gear',
                {},
                h(
                  'span',
                  {},
                  displayName(s.name, s.nickname),
                  s.owner === this.mySlot ? null : this.whoseTag(s.owner),
                  h('small', {}, ` · ${melee ? 'al cinto' : 'en la mano'}: ${carries(s)}`),
                  // Against what it carries now: what would be better in green, what worse in red.
                  s.id === w.holder ? null : weaponDiff(profileOf(w), carriedProfile(c, s, melee)),
                ),
                s.id === w.holder ? this.chip('La lleva', 'good') : h('button.x-btn.small', { onclick: () => this.send({ type: 'assignWeapon', weapon: w.id, soldier: s.id }) }, 'Dársela'),
              ),
            ),
          )
        : h('p.x-muted', {}, 'No tienes soldados que puedan llevarla.'),
    ];
  }

  /** An armour tier (docs/FASE9.md §5): what it gives; once the Taller has made it, everyone wears it. */
  private tierPanel(c: CampaignState, key: string): (HTMLElement | null)[] {
    const tier = Number(key[1]) as 1 | 2 | 3;
    const unlocked = baseArmorTier(c) >= tier;
    const armour = ARMOR_TIERS[tier - 1]!;
    const effect = tier === 1 ? 'La protección de serie.' : `+${armour.hp} de salud${armour.armor ? ` y +${armour.armor} de blindaje` : ''}.`;
    return [
      h('div.alert-kicker', { className: `alert-kicker${unlocked ? ' good' : ''}` }, `ARMADURA · NIVEL ${tier}`),
      h('h2.x-title', {}, this.tierMarks(tier), ' ', armour.name),
      h('p.x-text', {}, effect),
      h('p.x-chips', { title: 'No se compra: en cuanto el Taller la hace, la llevan todos' }, unlocked ? this.chip('La llevan todos', 'good') : this.chip('Para todos, cuando el Taller la haga')),
      tier === 1 || unlocked ? null : this.craftRow(c, 'armor', tier),
    ];
  }

  // ---------------------------------------------------------- barracks

  /**
   * A siege held (report.damage): what the base was left with, to mend and set again, and the way to Construir.
   * Only when it held: a fallen base is lost anyway.
   */
  private damageLine(damage: { broken: number; hurt: number; spent: number } | undefined): HTMLElement | null {
    if (!damage) return null;
    const parts = [
      damage.broken ? `${damage.broken} ${damage.broken === 1 ? 'rota' : 'rotas'}` : '',
      damage.hurt ? `${damage.hurt} ${damage.hurt === 1 ? 'tocada' : 'tocadas'}` : '',
      damage.spent ? `${damage.spent} ${PIECE_STATE.spent}` : '',
    ].filter(Boolean);
    if (!parts.length) return h('p.debrief-damage', {}, 'En la base: todo en pie.');
    return h('p.debrief-damage.x-warn', {}, `En la base: ${parts.join(', ')}. `, h('button.x-btn.small', { onclick: () => void this.go('facilities') }, BUILD_TEXT.build));
  }

  /**
   * Nobody can go out (mustWait, docs/FASE12.md §11.6): only the hurt left, and nothing to recruit
   * with. Time passes with the missions, so here it passes by waiting, both agreeing as before.
   */
  private waitEl(c: CampaignState): HTMLElement | null {
    if (!mustWait(c)) return null;
    const ready = c.advanceReady[this.mySlot];
    const partner = this.partner();
    const together = !!partner?.connected && c.players.length > 1;
    const partnerReady = together && !ready && c.advanceReady[partner!.slot];
    return h(
      'div.strat-center-bottom',
      {},
      h(
        `button.x-btn.big${ready ? '.ready' : ''}${partnerReady ? '.waiting' : ''}`,
        { onclick: () => this.send({ type: 'advance', ready: !ready }) },
        ready ? 'ESPERANDO…' : partnerReady ? `ESPERAR CON ${partner!.name.toUpperCase()}` : 'ESPERAR A QUE SE CUREN',
      ),
      h(
        'small.x-muted',
        {},
        ready && together
          ? `Esperando a ${partner!.name}`
          : partnerReady
            ? `${partner!.name} quiere esperar a que se curen`
            : 'Nadie puede salir: pasan los días hasta que alguien se cure o haya suministros para reclutar.',
      ),
    );
  }

  private renderBarracks(c: CampaignState): void {
    const waiting = this.waitEl(c);
    if (waiting) this.viewEl.append(waiting);
    // With a Camposanto the fallen rest there, out of this list.
    const listed = (s: CampaignSoldier) => s.alive || !facilityCount(c, 'memorial');
    const mine = c.soldiers.filter((s) => s.owner === this.mySlot && listed(s)).sort((a, b) => Number(b.alive) - Number(a.alive) || b.board.length - a.board.length || totalBars(b.skills) - totalBars(a.skills));
    const theirs = c.soldiers.filter((s) => s.owner !== this.mySlot && listed(s)).sort((a, b) => Number(b.alive) - Number(a.alive) || b.board.length - a.board.length || totalBars(b.skills) - totalBars(a.skills));
    if (!this.soldier || !c.soldiers.some((s) => s.id === this.soldier)) this.soldier = mine.find((s) => s.alive)?.id ?? c.soldiers[0]?.id ?? null;
    const row = (s: CampaignSoldier) =>
      h(
        `button.x-row.soldier${this.soldier === s.id ? '.selected' : ''}${s.alive ? '' : '.dim'}`,
        {
          style: { '--row-accent': this.playerColor(s.owner) },
          onclick: () => {
            this.soldier = s.id;
            this.promoting = false;
            this.picking = null;
            this.custom = null;
            this.render();
          },
        },
        roleDot(s.board),
        h('b', {}, displayName(s.name, s.nickname)),
        veterancyBadge(s.board, s.appearance?.sex === 'f'),
        this.hasPoints(s)
          ? this.chip('▲', 'good', 'Talento por elegir')
          : this.healthTag(c, s, true),
      );
    const count = mine.filter((s) => s.alive).length;
    const cap = soldierCap(c);
    const price = recruitCost(c);
    const blockedRecruit = this.why([count >= cap, 'El cuartel está lleno'], [bedsFree(c) <= 0, 'Para reclutar más soldados, construid un barracón: no quedan camas.'], [true, this.shortfall(price)]);
    // Recruiting, on a card of its own: what it is, what it costs, what it needs, and one big button.
    const recruit = h(
      'div.recruit-card',
      { title: `Como mucho ${cap} soldados por jugador${price < RECRUIT_COST ? '. Más barato: el centro de entrenamiento está junto al cuartel' : ''}` },
      h('div.x-group', {}, 'Reclutar un soldado'),
      h('p.x-text', {}, `Un recluta nuevo: sale con cuchillo y pistola y con algo de práctica en una o dos habilidades. Llega al momento.`),
      h(
        'p.x-chips',
        {},
        this.chip(costText(price)),
        this.chip(`Tus soldados ${count}/${cap}`),
        this.chip(`Camas libres ${Math.max(0, bedsFree(c))}`, bedsFree(c) <= 0 ? 'warn' : '', 'Cada soldado necesita cama: el búnker tiene 8 y cada barracón suma más'),
      ),
      h('button.x-btn.primary.recruit-btn', { disabled: !!blockedRecruit, onclick: () => this.send({ type: 'recruit' }) }, `Reclutar · ${costText(price)}`),
      this.whyEl(blockedRecruit),
    );
    this.viewEl.append(
      this.panel(
        'barracks-left',
        'left',
        h('div.x-head', {}, 'Tus soldados'),
        h('div.x-list', {}, ...mine.map(row)),
        recruit,
        theirs.length ? h('div.x-head', {}, `De ${this.playerName(theirs[0]!.owner)}`, this.commands(theirs[0]!.owner) ? h('small', {}, ' · no está: los preparas tú') : null) : null,
        h('div.x-list', {}, ...theirs.map(row)),
      ),
    );
    const s = c.soldiers.find((x) => x.id === this.soldier);
    const draft = this.custom && this.custom.soldier === s?.id ? this.custom : null;
    this.bastion.setShowcase(s?.alive ? SOLDIER : null, s ? this.playerColor(s.owner) : '#fff', draft?.appearance ?? s?.appearance, s ? this.gearFor(c, s) : undefined, s?.id);
    if (!s) return;
    if (draft) {
      this.viewEl.append(this.panel('barracks-right', 'right', ...this.customizer(s, draft)));
      return;
    }
    // Own, or the partner's while they're gone: either way, this player prepares them (commands).
    const own = s.owner === this.mySlot;
    const isMine = this.commands(s.owner);
    const stats = squadMember(c, s);
    // Equipment is a form field that opens its list of choices.
    const weaponPicking = (place: 'hand' | 'belt'): WeaponPickContext => {
      const key = `${s.id}:${place}`;
      return {
        c,
        s,
        mySlot: this.mySlot,
        connected: this.connected(),
        open: this.picking === key,
        toggle: () => {
          this.picking = this.picking === key ? null : key;
          this.render();
        },
        send: (cmd) => this.send(cmd),
        ownerName: (slot) => this.playerName(slot),
      };
    };
    const loadout = (slot: ItemSlot, title: string) => {
      const current = s.loadout[slot];
      if (!isMine || !s.alive) return h('p.x-text', {}, `${title}: ${current ? ITEMS[current].name : '—'}`);
      const key = `${s.id}:${slot}`;
      const open = this.picking === key;
      const pick = (item: ItemId | null) => {
        this.picking = null;
        if (item !== current) this.send({ type: 'equip', soldier: s.id, slot, item });
        this.render();
      };
      // The belt (docs/FASE9.md §4): either place takes any accessory; the special rounds don't go on it any more.
      const choices = Object.values(ITEMS)
        .filter((i) => i.slot === 'utility')
        .map((item) => ({ item, free: itemsFree(c, item.id) + (current === item.id ? 1 : 0) }))
        .filter(({ item, free }) => free > 0 || current === item.id);
      return h(
        'div.x-pick',
        {},
        h(
          'button.x-pick-field',
          {
            onclick: () => {
              this.picking = open ? null : key;
              this.render();
            },
          },
          h('small', {}, title),
          h('b', {}, current ? ITEMS[current].name : 'Nada'),
        ),
        open
          ? h(
              'div.x-pick-list',
              {},
              h(`button${current ? '' : '.on'}`, { onclick: () => pick(null) }, 'Nada'),
              ...choices.map(({ item, free }) =>
                h(`button${current === item.id ? '.on' : ''}`, { onclick: () => pick(item.id) }, item.name, current === item.id ? null : h('small', {}, `${free} libres`)),
              ),
            )
          : null,
      );
    };
    this.viewEl.append(
      this.panel(
        'barracks-right',
        'right',
        h('div.alert-kicker', {}, `${veterancyName(s).toUpperCase()} · ${own ? 'TUYO' : `DE ${this.playerName(s.owner).toUpperCase()}`}`),
        h('h2.x-title', { title: `Misiones: ${s.missions} · Bajas: ${s.kills}` }, displayName(s.name, s.nickname)),
        h('p.x-sub', {}, weaponLabel(c, s)),
        !own && isMine && s.alive ? h('p.x-muted', {}, `${this.playerName(s.owner)} no está: lo preparas tú hasta que vuelva.`) : null,
        isMine && s.alive
          ? h(
              'button.x-btn.small',
              { onclick: () => ((this.custom = { soldier: s.id, name: s.name, nickname: s.nickname ?? '', appearance: { ...(s.appearance ?? defaultAppearance(s.owner)) } }), this.render()) },
              'Personalizar',
            )
          : null,
        h(
          'div.x-statline',
          {},
          this.stat('hp', String(stats.maxHp), 'Salud'),
          // Both aims, the soldier's own without the weapon (its card says its part): the base and what the bars and the board add (playtest 17/10).
          this.stat('aim', String(TEMPLATES[SOLDIER].aim + stats.mods.aim + stats.mods.gunAim), `Puntería con armas de fuego: ${BASE_AIM} de base, más lo que suman las barras de Disparo y el tablero`),
          this.stat('melee', String(TEMPLATES[SOLDIER].aim + stats.mods.aim + stats.mods.meleeAim), `Puntería cuerpo a cuerpo: ${BASE_AIM} de base, más lo que suman las barras de Cuerpo a cuerpo y el tablero`),
          this.stat('will', String(stats.will), 'Voluntad'),
          this.healthTag(c, s, false),
        ),
        ...this.careActions(c, s),
        s.traits?.length ? h('div.x-group', {}, 'Rasgos') : null,
        s.traits?.length ? h('p.x-chips', {}, ...s.traits.map((t) => this.chip(TRAITS[t].name, '', TRAITS[t].description))) : null,
        // What they carry, and what is chosen here and what isn't (playtest: «no se ve qué armas llevan ni dónde se elige»).
        h('div.x-group', { title: 'Las armas salen de la Armería: allí se arreglan y se les ponen mejoras' }, isMine && s.alive ? 'Arma y armadura · se eligen aquí' : 'Arma y armadura'),
        // One's own, as pickers like the belt's (Arturo, 17/10); the partner's, as they are.
        ...(isMine && s.alive ? (['hand', 'belt'] as const).map((place) => weaponPick(place, weaponPicking(place))) : [loadoutEl(c, s)]),
        (() => {
          // The armour the Taller has made is for everyone (docs/FASE9.md §5); what the next one waits for, on its chip.
          const tier = armorTier(c, s);
          const blocker = s.alive ? nextTierBlocker(c, s, 'armor') : null;
          const next = ARMOR_TIERS[tier];
          const stat = `${stats.maxHp} de salud${stats.armor ? ` · ${stats.armor} de blindaje` : ''}`;
          return h('div.x-gear', {}, this.tierMarks(tier), h('span', {}, ARMOR_TIERS[tier - 1]!.name, h('small', {}, ` · ${stat}`)), blocker && next ? this.chip(`→ ${next.name}`, '', blocker) : null);
        })(),
        h('div.x-group', { title: 'Dos huecos para cualquier accesorio. Se compran en la Armería; si el soldado cae, se pierden' }, 'Cinturón · se elige aquí'),
        loadout('utility', 'Hueco 1'),
        loadout('ammo', 'Hueco 2'),
        // Fase 9: the skills, the veterancy and the talents (skillSheet.ts).
        ...skillsBlock(skillsOf(s), isMine && s.alive, () => ((this.promoting = true), this.render())),
      ),
    );
  }

  /**
   * XCOM-style soldier customizer: name, nickname, head, build and a colour
   * per armour piece, previewed on the pedestal. Pieces without a colour of
   * their own follow the armour (engine partColor); the owner's mark is not
   * on the list: it always glows in their colour.
   */
  private customizer(s: CampaignSoldier, d: NonNullable<StrategyScreen['custom']>): (HTMLElement | null)[] {
    const pieces = ARMOR_PART_IDS;
    type Piece = ArmorPart;
    // A patch may clear a piece (undefined): it follows the armour again.
    const patched = (patch: Partial<Appearance>): Appearance => {
      const next: Appearance = { ...d.appearance, ...patch };
      for (const piece of pieces) if (next[piece] === undefined) delete next[piece];
      return next;
    };
    const set = (patch: Partial<Appearance>) => {
      d.appearance = patched(patch);
      this.render();
    };
    // While the system colour picker is open only the pedestal follows, a few times a second; the sheet redraws when it closes.
    let previewing = 0;
    const drag = (patch: Partial<Appearance>) => {
      d.appearance = patched(patch);
      previewing ||= window.setTimeout(() => {
        previewing = 0;
        if (this.custom === d) this.bastion.setShowcase(SOLDIER, this.playerColor(s.owner), d.appearance, this.gearFor(this.c(), s), s.id);
      }, 90);
    };
    const text = (key: 'name' | 'nickname', label: string, max: number, placeholder: string) =>
      h(
        'label.x-field',
        {},
        h('small', {}, label),
        h('input.x-input', {
          value: d[key],
          maxLength: max,
          placeholder,
          'data-key': `custom-${key}`,
          oninput: (e: Event) => {
            d[key] = (e.target as HTMLInputElement).value;
            this.render();
          },
        }),
      );
    const chips = <K extends string>(label: string, ids: readonly K[], names: Record<K, { name: string }>, current: K, pick: (id: K) => void) =>
      h(
        'div.x-field',
        {},
        h('small', {}, label),
        h('div.x-chips', {}, ...ids.map((id) => h(`button${id === current ? '.on' : ''}`, { onclick: () => pick(id) }, names[id].name))),
      );
    const note = (text: string) => h('span.x-part-note', {}, text);
    /** One line of the list: the piece and its colour, the swatches, and the system picker for any other colour. */
    const row = (label: string, current: string, colors: readonly string[], patch: (color: string) => Partial<Appearance>, aside: HTMLElement | null) =>
      h(
        'div.x-part',
        {},
        h('div.x-part-head', {}, h('i.x-part-chip', { style: { '--swatch': current } }), h('small', {}, label), aside),
        h(
          'div.x-swatches',
          {},
          ...colors.map((color) =>
            h(`button.x-swatch${color.toLowerCase() === current.toLowerCase() ? '.on' : ''}`, { style: { '--swatch': color }, title: color, onclick: () => set(patch(color)) }),
          ),
          h('input.x-swatch-any', {
            type: 'color',
            value: current.toLowerCase(),
            title: 'Otro color',
            oninput: (e: Event) => drag(patch((e.target as HTMLInputElement).value)),
            onchange: (e: Event) => set(patch((e.target as HTMLInputElement).value)),
          }),
        ),
      );
    const look = d.appearance;
    // What the headgear is painted with until it has its own colour: the armour (helmet), the trim (beret, cap, hood) or nothing.
    const hat = HEADS[look.head].paint;
    const follows = (piece: Piece) => partColor({ ...look, [piece]: undefined }, piece);
    const colorOf = (piece: Piece) => look[piece] ?? follows(piece);
    const pieceRow = (piece: Piece, label: string) => {
      const leader = piece === 'helmet' && hat === 'trim' ? 'los detalles' : 'la armadura';
      const own = look[piece] !== undefined && look[piece].toLowerCase() !== follows(piece).toLowerCase();
      const aside = own
        ? h('button.x-part-reset', { title: `Que vuelva a ir como ${leader}`, onclick: () => set({ [piece]: undefined }) }, `↺ como ${leader}`)
        : note(`como ${leader}`);
      return row(label, colorOf(piece), ARMOR_COLORS, (color) => ({ [piece]: color }), aside);
    };
    const helmetRow = hat
      ? pieceRow('helmet', hat === 'armour' ? 'Casco' : HEADS[look.head].name)
      : h('div.x-part.off', {}, h('div.x-part-head', {}, h('i.x-part-chip'), h('small', {}, 'Casco'), note(`${HEADS[look.head].name}: nada que pintar`)));
    // The whole armour at once: every piece that follows it takes the new colour (a beret, cap or hood keeps its own).
    const armour = row(
      'Toda la armadura',
      look.primary,
      ARMOR_COLORS,
      (primary) => ({ primary, torso: undefined, arms: undefined, legs: undefined, ...(hat === 'trim' ? {} : { helmet: undefined }) }),
      note('todas las piezas'),
    );
    const owner = this.playerColor(s.owner);
    const mark = h(
      'div.x-part.locked',
      { title: 'La marca brilla siempre en el color de su jugador, para que se sepa de quién es cada soldado.' },
      h('div.x-part-head', {}, h('i.x-part-chip.glow', { style: { '--swatch': owner } }), h('small', {}, 'Marca de jugador'), note('siempre en tu color')),
    );
    const blocked = this.why([!d.name.trim(), 'Ponle un nombre']);
    const save = () => this.send({ type: 'customize', soldier: s.id, name: d.name.trim(), nickname: d.nickname.trim(), appearance: d.appearance });
    // The same colours on every soldier of this player, each keeping its name, head and build; the headgear as it looks here.
    const others = this.c().soldiers.filter((x) => x.owner === s.owner && x.alive && x.id !== s.id);
    const parts: Partial<Appearance> = {};
    for (const piece of pieces) {
      const color = piece === 'helmet' && hat ? colorOf(piece) : look[piece];
      if (color !== undefined) parts[piece] = color;
    }
    const copy: HTMLButtonElement = h(
      'button.x-btn.small',
      {
        disabled: !!blocked || !others.length,
        title: 'Guarda a este soldado y pinta a todos tus soldados con estos colores; cada uno conserva su nombre, su cabeza y su complexión.',
        onclick: () => {
          // A second click confirms: it repaints the whole squad.
          if (!copy.classList.contains('armed')) {
            copy.classList.add('armed');
            copy.textContent = `¿Pintar a tus ${others.length + 1} soldados? Pulsa otra vez`;
            return;
          }
          save();
          for (const x of others) {
            const appearance: Appearance = { primary: look.primary, secondary: look.secondary, visor: look.visor, head: x.appearance.head, build: x.appearance.build, ...parts };
            this.send({ type: 'customize', soldier: x.id, name: x.name, nickname: x.nickname ?? '', appearance });
          }
          this.custom = null;
          this.render();
        },
      },
      'Copiar a todos mis soldados',
    );
    return [
      h('div.alert-kicker', {}, `PERSONALIZAR · ${veterancyName(s).toUpperCase()}`),
      h('h2.x-title', {}, displayName(d.name.trim() || s.name, d.nickname.trim())),
      text('name', 'Nombre', NAME_MAX, s.name),
      text('nickname', 'Apodo', NICKNAME_MAX, 'Sin apodo'),
      // Which survivor the soldier is (Synty); unset, the one their id picks, the same in every screen.
      h(
        'div.x-field',
        {},
        h('small', {}, 'Personaje'),
        h(
          'div.x-chips',
          {},
          h(`button${look.character ? '' : '.on'}`, { title: 'El que le toque, siempre el mismo', onclick: () => set({ character: undefined }) }, 'Al azar'),
          ...SURVIVOR_CHOICES.map((choice) => h(`button${look.character === choice.id ? '.on' : ''}`, { onclick: () => set({ character: choice.id }) }, choice.name)),
        ),
      ),
      chips('Cabeza', HEAD_IDS, HEADS, look.head, (head) => set({ head })),
      chips('Complexión', BUILD_IDS, BUILDS, look.build, (build) => set({ build })),
      h('div.x-head', {}, 'Colores'),
      h(
        'div.x-parts',
        {},
        armour,
        helmetRow,
        pieceRow('torso', 'Torso'),
        pieceRow('arms', 'Brazos'),
        pieceRow('legs', 'Piernas'),
        row('Detalles', look.secondary, ARMOR_COLORS, (secondary) => ({ secondary }), note('rodilleras, ribetes, capa')),
        row('Visor', look.visor, VISOR_COLORS, (visor) => ({ visor }), note('y luces del traje')),
        mark,
      ),
      h(
        'div.x-actions',
        {},
        h('button.x-btn.small', { onclick: () => ((d.appearance = randomAppearance(new Rng(Date.now() >>> 0))), this.render()) }, 'Aleatorio'),
        h(
          'button.x-btn.small',
          { title: 'Vuelve a los colores de tu jugador y al aspecto de serie', onclick: () => ((d.appearance = defaultAppearance(s.owner)), this.render()) },
          'Restablecer',
        ),
        copy,
      ),
      h(
        'div.x-actions',
        {},
        h(
          'button.x-btn.primary',
          {
            disabled: !!blocked,
            onclick: () => {
              save();
              this.custom = null;
              this.render();
            },
          },
          'Guardar',
        ),
        h('button.x-btn', { onclick: () => ((this.custom = null), this.render()) }, 'Cancelar'),
      ),
      this.whyEl(blocked),
    ];
  }

  /** Alive and with aptitude or talent points waiting. */
  /** Points the soldier can still put somewhere (playtest 13/10: capped points kept «¡Ascenso!» up for good). */
  private hasPoints(s: CampaignSoldier): boolean {
    return s.alive && !!this.state && canSpendPoints(this.state, s);
  }

  /**
   * The Árbol de habilidades (skillSheet.ts): the tree, and its «Guardar» for
   * the viewer's own soldier, which sends the whole list of talents (`picks`).
   */
  private promotionPanel(s: CampaignSoldier): HTMLElement {
    return treePanel({
      s: skillsOf(s),
      editable: this.commands(s.owner) && s.alive,
      portrait: soldierPortrait(SOLDIER, PLAYER_COLORS[s.owner], s.appearance, s.id),
      owner: this.playerName(s.owner),
      onUnlock: (cell) => this.send({ type: 'unlock', soldier: s.id, cell }),
      onClose: () => ((this.promoting = false), this.render()),
    });
  }

  // -------------------------------------------------------- facilities

  /**
   * Construir (docs/FASE12.md §11.7): the palette on the left, with what the
   * buildings give; the card of the piece picked on the clearing on the right.
   * Where it goes and felling are on the clearing itself (clearing.ts).
   */
  private renderFacilities(c: CampaignState): void {
    const lines: [string, string][] = [];
    const alive = c.soldiers.filter((s) => s.alive).length;
    lines.push(['flag', `Camas: ${alive}/${beds(c)}`]);
    if (facilityCount(c, 'simulation')) lines.push(['xp', `XP de misión +${Math.round((xpMultiplier(c) - 1) * 100)} %`]);
    if (facilityCount(c, 'workshop')) lines.push(['flag', `Accesorios −${Math.round((1 - shopPrice(c, 'smokeGrenade') / ITEMS.smokeGrenade.cost) * 100)} %`]);
    if (facilityCount(c, 'infirmary')) lines.push(['hp', `Heridas ×${healRate(c)} de rápido`]);
    if (facilityCount(c, 'training')) lines.push(['flag', `Escuadra de ${squadMax(c)}`]);
    if (rangeAim(c)) lines.push(['xp', `Puntería +${rangeAim(c)}`]);
    if (garageCut(c).wounds) lines.push(['hp', 'Las heridas curan antes (Chapistería)']);
    if (listeningDelay(c)) {
      const doom = doomSubject(bastionLevel(c));
      lines.push(['will', `${doom.charAt(0).toUpperCase()}${doom.slice(1)} avanza más despacio (Sala de radio)`]);
    }
    lines.push(['will', `Contactos: ${contactCapacity(c)} zonas`]);
    const tool = this.clearing.currentTool;
    this.viewEl.append(
      this.panel(
        'build-left',
        'left',
        ...palette(
          { c, kind: this.buildKind, tool, partnerHere: !!this.partner()?.connected },
          {
            kind: (k) => {
              this.buildKind = k;
              this.render();
            },
            take: (piece) => {
              this.piece = null;
              this.dismantling = false;
              this.clearing.setTool({ kind: 'place', piece, rot: tool.kind === 'place' ? tool.rot : 0 });
              this.render();
            },
            fell: () => {
              this.piece = null;
              this.clearing.setTool({ kind: 'fell' });
              this.render();
            },
            drop: () => this.dropTool(),
          },
        ),
        h('div.x-head', {}, 'Efectos'),
        ...lines.map(([icon, text]) => h('div.x-effect', {}, h(`span.agenda-icon.${icon}`), h('span', {}, text))),
      ),
    );
    const p = this.piece ? c.bastion.pieces.find((x) => x.id === this.piece) : undefined;
    if (!p) return;
    this.viewEl.append(
      this.panel(
        'build-right',
        'right',
        ...pieceCard(
          { c, piece: p, dismantling: this.dismantling, partnerHere: !!this.partner()?.connected },
          {
            send: (cmd) => {
              this.dismantling = false;
              this.send(cmd);
            },
            dismantle: (ask) => {
              this.dismantling = ask;
              this.render();
            },
            close: () => this.pickPiece(null),
          },
        ),
      ),
    );
  }

  /** Construir: the piece in hand (or felling) is let go. */
  private dropTool(): void {
    this.clearing.setTool({ kind: 'look' });
    this.render();
  }

  /** A piece of the clearing picked (null: let go): its card in Construir. */
  private pickPiece(id: string | null): void {
    this.piece = id;
    this.dismantling = false;
    this.clearing.select(id, false);
    if (id && this.view !== 'facilities') void this.go('facilities');
    else this.render();
  }

  // -------------------------------------------------------------- squad

  private renderSquad(c: CampaignState): void {
    const offer = this.offerById(c.activeMission);
    if (!offer) {
      this.viewEl.append(h('div.strat-center-top', {}, h('div.x-panel', {}, h('p.x-text', {}, 'No hay ninguna misión elegida.'), h('button.x-btn', { onclick: () => void this.go('geoscape') }, 'Ir al valle'))));
      this.bastion.setLineup([]);
      return;
    }
    // The siege (docs/FASE12.md §11.5): every soldier fit to fight defends, however many; nobody picks, nobody leaves.
    const siege = !!offer.siege;
    const squad = c.squad.map((id) => c.soldiers.find((s) => s.id === id)).filter((s): s is CampaignSoldier => !!s);
    const max = siege ? squad.length : squadMax(c);
    const limit = squadLimit(c, this.connected());
    const pads = siege ? squad : squadPads(c).map((id) => squad.find((s) => s.id === id) ?? null);
    this.bastion.setLineup(pads.map((s) => (s ? { template: SOLDIER, color: this.playerColor(s.owner), appearance: s.appearance, gear: this.gearFor(c, s), seed: s.id } : null)));
    const myCount = squad.filter((s) => s.owner === this.mySlot).length;

    this.viewEl.append(
      h(
        'div.strat-center-top',
        {},
        h(
          'div.x-panel.briefing-head',
          {},
          h('div.alert-kicker', siege ? { className: 'alert-kicker bad' } : {}, siege ? SIEGE_TEXT.name.toUpperCase() : 'SELECCIÓN DE ESCUADRA'),
          h('h1', {}, offer.name),
          h(
            'p.x-sub',
            {},
            `${siege ? 'El Bastión' : `${MISSION_NAMES[offer.kind]} · ${REGIONS[offer.region].name}`}${offer.night ? ` · ${NIGHT_TEXT.label}` : ''} · ${'★'.repeat(offer.difficulty)}`,
          ),
          // What the mission is for, in view before choosing the squad (playtest 13/10: it was only a tooltip).
          h('p.x-text.briefing-goal', {}, offerBriefing(c, offer)),
        ),
      ),
    );
    const roster = c.soldiers.filter((s) => s.owner === this.mySlot && s.alive);
    // The partner's too while they're gone (commandsFor): this player fills the squad with both.
    const lent = c.soldiers.filter((s) => s.owner !== this.mySlot && s.alive && this.commands(s.owner));
    const pickRow = (s: CampaignSoldier) => {
      const on = c.squad.includes(s.id);
      const ready = soldierReady(s);
      const gear = [s.loadout.utility, s.loadout.ammo].filter((g): g is ItemId => !!g).map((g) => ITEMS[g].name);
      return h(
        `button.x-row.soldier${on ? '.selected' : ''}${ready ? '' : '.dim'}`,
        // In a siege who defends is the healthy, all of them: nothing to pick.
        { style: { '--row-accent': this.playerColor(s.owner) }, disabled: siege || (!ready && !on), onclick: () => this.send({ type: 'squad', soldier: s.id, on: !on }) },
        roleDot(s.board),
        h('b', { title: gear.join(' · ') }, displayName(s.name, s.nickname)),
        veterancyBadge(s.board, s.appearance?.sex === 'f'),
        on ? this.chip('✓', 'good', 'En la escuadra') : ready ? h('span') : s.infection ? this.chip('Fiebre', 'bad', 'Mordido: necesita el tratamiento antes de salir') : this.chip(missionsText(missionsFor(s.woundedDays)), 'warn', 'Herido'),
      );
    };
    this.viewEl.append(
      this.panel(
        'squad-left',
        'left.compact',
        h(
          'div.x-head',
          { title: siege ? BASE_SIEGE_TEXT.defenders : 'Pulsa un soldado para meterlo o sacarlo. Si tu compañero no llena su parte, puedes ocupar sus huecos; los recupera cuando tenga a quién llevar. El equipo se cambia en el Cuartel.' },
          siege
            ? `Tus soldados · ${myCount}`
            : lent.length
            ? `Tus soldados · ${myCount}`
            : myCount > limit
              ? `Tus soldados · ${myCount} (${myCount - limit} en huecos de ${this.partner()?.name ?? 'tu compañero'})`
              : `Tus soldados · ${myCount}/${limit}`,
        ),
        h('div.x-list', {}, ...roster.map(pickRow)),
        lent.length ? h('div.x-head', {}, `De ${this.playerName(lent[0]!.owner)} · ${squad.length - myCount}`, siege ? null : h('small', {}, ' · no está: los eliges tú')) : null,
        lent.length ? h('div.x-list', {}, ...lent.map(pickRow)) : null,
      ),
    );

    const ready = c.launchReady[this.mySlot];
    const partner = this.partner();
    const waiting = partner?.connected && c.players.length > 1 && !c.launchReady[partner.slot];
    const partnerReady = partner?.connected && !ready && c.launchReady[partner.slot];
    // Who has said «listo», in each one's colour: the partner's yes is seen at a glance.
    const together = c.players.length > 1 && partner?.connected;
    const readyBoard = together
      ? h(
          'div.ready-board',
          {},
          ...[this.mySlot, partner!.slot].map((slot) =>
            // A plate each (playtest 15/10 #5): who, and a light that says whether they are ready.
            h(
              `span.ready-mark${c.launchReady[slot] ? '.on' : ''}`,
              { style: { '--accent': this.playerColor(slot) }, title: c.launchReady[slot] ? (siege ? 'A punto para defender' : 'A punto para salir') : 'Aún elige la escuadra' },
              h('i.ready-lamp'),
              h('b', {}, slot === this.mySlot ? `${this.playerName(slot)} (tú)` : this.playerName(slot)),
              h('small', {}, c.launchReady[slot] ? 'A PUNTO' : 'ELIGIENDO'),
            ),
          ),
        )
      : null;
    this.viewEl.append(
      h(
        'div.strat-center-bottom',
        { style: { bottom: '24px' } },
        readyBoard,
        h(
          `button.x-btn.big${ready ? '.ready' : ''}${partnerReady ? '.waiting' : ''}`,
          { disabled: !c.squad.length, onclick: () => this.send({ type: 'launch', ready: !ready }) },
          siege
            ? ready
              ? 'LISTOS PARA DEFENDER'
              : partnerReady
                ? `DEFENDER CON ${partner!.name.toUpperCase()}`
                : 'DEFENDER EL BASTIÓN'
            : ready
              ? 'LISTOS PARA SALIR'
              : partnerReady
                ? `SALIR CON ${partner!.name.toUpperCase()}`
                : 'LANZAR MISIÓN',
        ),
        h(
          'small.x-muted',
          {},
          ready && waiting ? `Esperando a ${partner!.name}` : partnerReady ? `${partner!.name} espera para salir` : siege ? BASE_SIEGE_TEXT.defenders : `${squad.length}/${max} soldados`,
        ),
        h(
          'div.alert-actions',
          {},
          h('button.x-btn', { onclick: () => void this.go('research') }, 'Volver al Bastión'),
          // A siege can't be called off.
          siege ? null : h('button.x-btn.danger', { onclick: () => this.send({ type: 'cancelMission' }) }, 'Cancelar misión'),
        ),
      ),
    );

    const cards = h('div.lineup-cards');
    // A free place this player could take but has nobody ready to put in: recruit straight into it (#39).
    const spare = roster.some((s) => soldierReady(s) && !c.squad.includes(s.id));
    const room = !siege && !spare && roster.length < soldierCap(c) && !squadPlace(c, this.mySlot, this.connected()).error;
    const recruitable = room && bedsFree(c) > 0;
    const cost = recruitCost(c);
    pads.forEach((s, i) => {
      const card = s
        ? h(
            `div.lineup-card${together && c.launchReady[s.owner] ? '.ready' : ''}`,
            // data-bust: the theme puts the soldier's own bust on the card (ui/themes/mixta-base.css), once drawn.
            { style: { '--accent': this.playerColor(s.owner) }, 'data-bust': '' },
            h('b', {}, displayName(s.name, s.nickname)),
            h('small', {}, roleDot(s.board), veterancyName(s)),
            c.players.length > 1 ? h('small', {}, s.owner === this.mySlot ? 'Tuyo' : `De ${this.playerName(s.owner)}`) : null,
            this.commands(s.owner) && !siege ? h('button.x-btn', { onclick: () => this.send({ type: 'squad', soldier: s.id, on: false }) }, 'Quitar') : null,
          )
        : recruitable
          ? this.recruitCard(c, cost)
          : h(
              'div.lineup-card.empty',
              { title: spare ? 'Elige un soldado a la izquierda' : '' },
              h('b', {}, 'Libre'),
              room && this.padSide(c, i) === this.mySlot ? h('small', {}, 'Para reclutar más soldados, construid un barracón') : null,
            );
      if (s) this.bustOn(card, s);
      card.dataset.pad = String(i);
      cards.append(card);
    });
    this.viewEl.append(cards);
    this.anchorCards(cards, max);
  }

  /** A free hangar place: «Reclutar» brings a recruit (no class yet) straight into the squad. */
  private recruitCard(c: CampaignState, cost: number): HTMLElement {
    const afford = canAfford(c, cost);
    return h(
      'div.lineup-card.empty.recruit',
      {},
      h('b', {}, 'Libre'),
      h(
        'button.x-btn',
        {
          disabled: !afford,
          title: afford ? 'Un recluta nuevo, directo a la escuadra.' : this.shortfall(cost),
          onclick: () => this.send({ type: 'recruit', squad: true }),
        },
        // The price never splits from its sign on a narrow photo.
        `Reclutar · ${cost}\u00a0§`,
      ),
    );
  }

  /** Whose half of the hangar a pad is in. */
  private padSide(c: CampaignState, pad: number): Slot {
    return c.players.length < 2 || pad < Math.ceil(squadMax(c) / 2) ? c.players[0]! : c.players[1]!;
  }

  /** Keeps the lineup cards under each soldier while the camera moves. */
  private anchorCards(cards: HTMLElement, count: number): void {
    // More than a row of the hangar holds (a siege's defenders, up to 12: Gameplay, 18/10): no longer one
    // under each figure, but two rows centred over the controls, as wide as the screen lets them.
    if (count > 8) {
      cards.classList.add('rows');
      const place = () => {
        // In the room right of the soldiers' list, half of them a row.
        const perRow = Math.ceil(count / 2);
        const width = cards.parentElement?.clientWidth ?? window.innerWidth;
        const from = (this.viewEl.querySelector<HTMLElement>('.strat-left')?.getBoundingClientRect().right ?? 0) + 12;
        const room = Math.max(perRow * 86, width - from - 12);
        const cw = Math.max(76, Math.min(124, room / perRow - 10));
        cards.style.setProperty('--card-w', `${cw}px`);
        cards.style.width = `${perRow * (cw + 10)}px`;
        cards.style.left = `${Math.max(0, from + (room - perRow * (cw + 10)) / 2)}px`;
        // Just above the controls, counted from where the cards' box starts.
        const controls = this.viewEl.querySelector<HTMLElement>('.strat-center-bottom');
        const origin = cards.offsetParent?.getBoundingClientRect().top ?? 0;
        const floor = controls ? controls.getBoundingClientRect().top : window.innerHeight - 170;
        cards.style.top = `${Math.max(0, floor - origin - cards.offsetHeight - 14)}px`;
        this.anchorFrame = requestAnimationFrame(place);
      };
      place();
      return;
    }
    const step = () => {
      const pads = Array.from({ length: count }, (_, i) => this.stage.project(this.bastion.padPosition(i, count)));
      const spacing = count > 1 ? Math.min(...pads.slice(1).map((p, i) => Math.abs(p.x - pads[i]!.x))) : 140;
      cards.style.setProperty('--card-w', `${Math.max(84, Math.min(150, spacing - 8))}px`);
      // One row with a shared height, above the launch controls even after a resize or UI zoom.
      const children = Array.from(cards.children) as HTMLElement[];
      const height = Math.max(0, ...children.map((el) => el.offsetHeight));
      const controls = this.viewEl.querySelector<HTMLElement>('.strat-center-bottom');
      const ceiling = controls ? controls.getBoundingClientRect().top - cards.getBoundingClientRect().top - height - 18 : Infinity;
      const top = Math.min(Math.min(...pads.map((p) => p.y)) + 12, ceiling);
      for (const el of children) {
        const p = pads[Number(el.dataset.pad)];
        if (!p) continue;
        el.style.left = `${p.x}px`;
        el.style.top = `${top}px`;
      }
      this.anchorFrame = requestAnimationFrame(step);
    };
    step();
  }

  // ------------------------------------------------------------ debrief

  /** What the wounds mean: who can't deploy, and until when. */
  private woundedLine(c: CampaignState, squad: NonNullable<CampaignState['lastReport']>['squad']): HTMLElement | null {
    const wounded = squad.filter((e) => e.status === 'wounded');
    if (!wounded.length) return null;
    const last = Math.max(...wounded.map((e) => e.woundedDays));
    const text =
      wounded.length === 1
        ? `${displayName(wounded[0]!.name, c.soldiers.find((x) => x.id === wounded[0]!.id)?.nickname)} no podrá desplegar en ${missionsText(missionsFor(last))}.`
        : `${wounded.length} heridos: no podrán desplegar en ${missionsText(missionsFor(last))} como mucho.`;
    return h('p.x-text.x-warn', { title: 'La enfermería cura las heridas el doble de rápido' }, text);
  }

  private renderDebrief(c: CampaignState): void {
    const report = c.lastReport;
    if (!report) {
      void this.go('research');
      return;
    }
    const entries: LineupEntry[] = report.squad.map((e) => {
      const s = c.soldiers.find((x) => x.id === e.id);
      // Someone left behind (docs/PRUEBA-2026-10-14.md P1) didn't come back either: a memorial, as the fallen.
      return { template: SOLDIER, color: this.playerColor(e.owner), status: e.status === 'lost' ? 'dead' : e.status, appearance: s?.appearance, gear: s ? this.gearFor(c, s) : undefined, seed: e.id };
    });
    this.bastion.setLineup(entries);
    // The outcome in a line of figures; the details are in each chip's title, the story's page one click away.
    const credits = (report.reward?.credits ?? 0) + report.salvage + (report.loot?.supplies ?? 0);
    const creditsWhy = [
      report.reward?.credits ? `Recompensa: ${report.reward.credits}` : '',
      report.bodies ? `${report.bodies} cuerpos registrados: ${report.salvage}` : '',
      report.loot?.supplies ? `Botín: ${report.loot.supplies}` : '',
    ]
      .filter(Boolean)
      .join(' · ');
    const wounded = report.squad.filter((e) => e.status === 'wounded');
    const fallen = report.squad.filter((e) => e.status === 'dead').length;
    const tally = h(
      'p.x-chips.debrief-tally',
      {},
      credits ? this.chip(`+${credits} §`, 'good', creditsWhy) : null,
      report.xp ? this.chip(`Bastión +${report.xp} XP`, '', 'Experiencia del Bastión: sube de nivel y desbloquea') : null,
      report.levelTo > report.levelFrom ? this.chip(`¡Nivel ${report.levelTo}!`, 'good', Array.from({ length: report.levelTo - report.levelFrom }, (_, i) => this.unlocksText(report.levelFrom + i + 1)).join(' ')) : null,
      report.doomChange ? this.chip(report.doomChange > 0 ? `Reloj +${report.doomChange}` : `Reloj ${report.doomChange}`, report.doomChange > 0 ? 'bad' : 'good', doomChangeText(report.levelTo, report.doomChange)) : null,
      fallen ? this.chip(`${fallen} ${fallen === 1 ? 'caído' : 'caídos'}`, 'bad') : null,
      wounded.length ? this.chip(`${wounded.length} ${wounded.length === 1 ? 'herido' : 'heridos'}`, 'warn', this.woundedLine(c, report.squad)?.textContent ?? '') : null,
    );
    const story = report.victory && report.story ? STORY[report.story - 1]!.after : '';
    this.viewEl.append(
      h(
        'div.strat-center-top',
        {},
        h(
          `div.x-panel.briefing-head.${report.victory ? 'victory' : 'defeat'}`,
          {},
          h('div.alert-kicker', { className: `alert-kicker ${report.victory ? 'good' : 'bad'}` }, 'INFORME DE MISIÓN'),
          h('h1', {}, report.victory ? 'MISIÓN CUMPLIDA' : 'MISIÓN FRACASADA'),
          h('p.x-sub', {}, report.missionName),
          tally,
          this.damageLine(report.damage),
          report.loot?.line ? h('p.debrief-loot', {}, report.loot.line) : null,
          ...barLines(report.squad).map((line) => h('p.debrief-loot', {}, line)),
          report.night ? h('p.debrief-loot', {}, NIGHT_TEXT.report) : null,
          story ? h('details.debrief-story', {}, h('summary', {}, 'Leer el parte'), h('p.x-text', {}, story)) : null,
        ),
      ),
    );
    const cards = h('div.lineup-cards');
    report.squad.forEach((e, i) => {
      const bitten = report.infected?.includes(e.id) ? ` · ${BITE_TAG}` : '';
      const status = (e.status === 'dead' ? 'CAÍDO EN COMBATE' : e.status === 'wounded' ? `HERIDAS · ${missionsText(missionsFor(e.woundedDays)).toUpperCase()}` : 'ILESO') + bitten;
      const card = h(
        `div.lineup-card.${e.status}`,
        { style: { '--accent': this.playerColor(e.owner) }, 'data-bust': '' },
        h('b', {}, displayName(e.name, c.soldiers.find((x) => x.id === e.id)?.nickname)),
        h(`small${e.status === 'dead' ? '.x-bad' : e.status === 'wounded' ? '.x-warn' : ''}`, {}, status),
        h('small', {}, `${e.kills} baja${e.kills === 1 ? '' : 's'}`),
        // Fase 9: what it learnt and the levels it went up (skillSheet.ts); only the living bring it home.
        ...(e.status === 'dead' || e.status === 'lost' ? [] : learnedTags(e)),
      );
      this.bustOn(card, c.soldiers.find((x) => x.id === e.id) ?? e);
      card.dataset.pad = String(i);
      cards.append(card);
    });
    this.viewEl.append(cards);
    this.anchorCards(cards, Math.max(report.squad.length, 1));
    this.viewEl.append(
      h(
        'div.strat-center-bottom',
        { style: { bottom: '24px' } },
        h(
          'button.x-btn.big.primary',
          {
            onclick: () => {
              this.seenReport = this.reportKey(c);
              try {
                localStorage.setItem(REPORT_KEY, this.seenReport);
              } catch {
                /* storage unavailable: the debrief may show again after a reload */
              }
              this.bastion.setLineup([]);
              void this.go('research');
            },
          },
          'CONTINUAR',
        ),
      ),
    );
  }

  // -------------------------------------------------------------- modal

  /** One thing at a time: campaign over, then a pending event, then alerts, then the promotion screen. */
  /** The Novedades tray: what happened without a window of its own. */
  private newsPanel(): HTMLElement {
    const close = () => ((this.newsOpen = false), this.render());
    return h(
      'div.x-panel.wide',
      {},
      h('div.alert-kicker', {}, 'NOVEDADES'),
      h(
        'div.x-list.news',
        {},
        ...this.news.map((n, i) =>
          h(
            'div.x-row',
            { title: n.text },
            h('div', {}, h('small', { className: n.tone === 'info' ? '' : `x-${n.tone}` }, n.kicker), h('b', {}, n.title), h('small', {}, n.text)),
            n.action
              ? h(
                  'button.x-btn.small',
                  {
                    onclick: () => {
                      this.news.splice(i, 1);
                      this.newsOpen = false;
                      n.action!.go();
                      this.render();
                    },
                  },
                  n.action.label,
                )
              : null,
          ),
        ),
      ),
      h(
        'div.alert-actions',
        {},
        h('button.x-btn', { onclick: () => ((this.news = []), close()) }, 'Vaciar'),
        h('button.x-btn.primary', { onclick: close }, 'Cerrar'),
      ),
    );
  }

  private renderModal(c: CampaignState): void {
    clear(this.modal);
    let content: HTMLElement | null = null;
    if (this.settingsOpen) {
      const redraw = () => this.render();
      content = h(
        'div.x-panel.wide.base-settings',
        {},
        h('div.alert-kicker', {}, 'AJUSTES'),
        h('h2.x-title', {}, 'Ajustes'),
        h('p.x-muted', {}, this.partner()?.connected ? `Solo para ti: ${this.partner()!.name} sigue a lo suyo.` : 'Se guardan en este navegador.'),
        soundSettings(redraw),
        graphicsSettings(redraw),
        h('div.alert-actions', {}, h('button.x-btn.primary', { onclick: () => ((this.settingsOpen = false), this.render()) }, 'Cerrar')),
      );
    } else if (this.quitting) {
      const partner = c.players.length > 1 && this.partner()?.connected ? this.partner()!.name : null;
      const q = quitQuestion(partner, false);
      content = h(
        'div.x-panel',
        {},
        h('div.alert-kicker', {}, QUIT_BUTTON.toUpperCase()),
        h('h2.x-title', {}, q.title),
        h('p.x-text', {}, q.text),
        q.warning ? h('p.x-text.quit-warning', {}, q.warning) : null,
        h(
          'div.alert-actions',
          {},
          h('button.x-btn.danger', { onclick: () => quitGame() }, q.yes),
          h('button.x-btn', { onclick: () => ((this.quitting = false), this.render()) }, q.no),
        ),
      );
    } else if (this.leaving && !c.outcome) {
      const partnerHere = c.players.length > 1 && !!this.partner()?.connected;
      content = h(
        'div.x-panel',
        {},
        h('div.alert-kicker', {}, 'MENÚ PRINCIPAL'),
        h('h2.x-title', {}, '¿Volver al menú?'),
        h('p.x-text', {}, partnerHere ? `La campaña queda guardada. ${this.partner()!.name} vuelve también al menú.` : 'La campaña queda guardada.'),
        h(
          'div.alert-actions',
          {},
          h('button.x-btn.primary', { onclick: () => ((this.leaving = false), this.handlers.onLeave()) }, 'Volver al menú'),
          h('button.x-btn', { onclick: () => ((this.leaving = false), (this.settingsOpen = true), this.render()), title: 'Sin salir de la campaña' }, 'Solo los ajustes'),
          // Closing the game, in the desktop app only (playtest 17/10).
          canQuit() ? h('button.x-btn', { onclick: () => ((this.leaving = false), (this.quitting = true), this.render()) }, QUIT_BUTTON) : null,
          h('button.x-btn', { onclick: () => ((this.leaving = false), this.render()) }, 'Seguir aquí'),
        ),
      );
    } else if (c.outcome) {
      content = h(
        'div.x-panel',
        {},
        h('div.alert-kicker', { className: `alert-kicker ${c.outcome === 'victory' ? 'good' : 'bad'}` }, 'FIN DE LA CAMPAÑA'),
        h('h2.x-title', {}, c.outcome === 'victory' ? (c.ending ? ENDING_TITLES[c.ending] : 'Campaña ganada') : 'Campaña perdida'),
        h('p.x-text', {}, epilogue(c.outcome, c.ending, c.doom >= DOOM_MAX)),
        h('div.alert-actions', {}, h('button.x-btn.primary', { onclick: () => this.handlers.onLeave() }, 'Volver al menú')),
      );
    } else if (c.event && eventDef(c) && this.view !== 'debrief') {
      const def = eventDef(c)!;
      const proposal = c.proposal?.action.type === 'event' ? c.proposal : null;
      content = h(
        'div.x-panel',
        {},
        h(
          'div.alert-kicker',
          {},
          c.event === 'finale' ? 'DECISIÓN FINAL' : c.event === 'quest' && c.questEvent ? `ENCARGO · ${QUESTS[c.questEvent].title.toUpperCase()}` : 'ACONTECIMIENTO',
        ),
        h('h2.x-title', {}, def.title),
        h('p.x-text', {}, def.text),
        h(
          'div.event-options',
          {},
          ...([0, 1] as const).map((i) =>
            h(
              // The option on the table stands out (playtest 2026-10-14: the partner's choice was hidden).
              `button.event-option${proposal?.action.type === 'event' && proposal.action.option === i ? '.proposed' : ''}`,
              { disabled: !!c.proposal || !eventOptionAvailable(c, i), onclick: () => this.send({ type: 'answerEvent', option: i }) },
              h('b', {}, def.options[i].label),
              h('small', {}, def.options[i].effect),
              eventOptionAvailable(c, i) ? null : h('small.x-why', {}, 'Ahora no está a vuestro alcance'),
            ),
          ),
        ),
        // The answer goes right here: the proposal bar above sits behind this window (playtest 2026-10-14).
        proposal
          ? proposal.by === this.mySlot
            ? h(
                'div.event-answer',
                {},
                h('p.x-warn', {}, `Esperando a ${this.partner()?.name ?? 'tu compañero'}…`),
                h('button.x-btn', { onclick: () => this.send({ type: 'withdraw' }) }, 'Retirar'),
                proposalTimer(true),
              )
            : h(
                'div.event-answer',
                {},
                h('p.x-warn', {}, `${this.playerName(proposal.by)} propone ${proposal.text}.`),
                h('button.x-btn.primary', { onclick: () => this.send({ type: 'vote', accept: true }) }, 'Aceptar'),
                h('button.x-btn.danger', { onclick: () => this.send({ type: 'vote', accept: false }) }, 'Rechazar'),
                proposalTimer(false),
              )
          : h('p.x-muted', {}, 'El tiempo no avanza hasta que decidáis.'),
      );
    } else if (this.alerts.length && this.view !== 'debrief') {
      const alert = this.alerts[0]!;
      // Like XCOM 2: a rumour's pop-up takes you to the globe, turned to where it happens.
      if (alert.region && this.shownAlert !== alert) {
        const region = alert.region;
        queueMicrotask(() => this.focusRegion(region));
      }
      this.shownAlert = alert;
      const close = () => {
        this.alerts.shift();
        this.render();
      };
      content = h(
        'div.x-panel',
        {},
        h('div.alert-kicker', { className: `alert-kicker ${alert.tone === 'info' ? '' : alert.tone}` }, alert.kicker),
        h('h2.x-title', {}, alert.title),
        ...(alert.body ?? []).map((p) => h('p.x-text', {}, p)),
        h(`p.${alert.body ? 'x-muted' : 'x-text'}`, {}, alert.text),
        alert.unlocks?.length
          ? h(
              'div.alert-unlocks',
              {},
              h('div.x-group', {}, 'Desbloquea'),
              ...alert.unlocks.map((u) => h('div.x-row.unlock', { title: u.detail ?? '' }, h(`span.agenda-icon.${u.kind}`), h('div', {}, h('b', {}, u.name), u.detail ? h('small', {}, u.detail) : null))),
            )
          : null,
        alert.note ? h('p.x-note', {}, alert.note) : null,
        h(
          'div.alert-actions',
          {},
          alert.action
            ? h(
                'button.x-btn.primary',
                {
                  onclick: () => {
                    this.alerts.shift();
                    alert.action!.go();
                  },
                },
                alert.action.label,
              )
            : null,
          h('button.x-btn', { onclick: close }, 'Entendido'),
        ),
      );
    } else if (this.newsOpen && this.news.length) {
      content = this.newsPanel();
    } else if (this.promoting && this.view === 'barracks') {
      const s = c.soldiers.find((x) => x.id === this.soldier);
      if (s) content = this.promotionPanel(s);
    }
    // A sheet of paper when a notice or a panel opens.
    if (content && !this.modal.classList.contains('show')) uiSound('paper');
    this.modal.classList.toggle('show', !!content);
    if (content) this.modal.append(content);
  }
}
