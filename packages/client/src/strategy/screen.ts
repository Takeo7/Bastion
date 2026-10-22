import './strategy.css';
import {
  ARMOR_COLORS,
  ARMOR_TIERS,
  armorTier,
  baseArmorTier,
  bastionLevel,
  bastionLevelA,
  bastionLevelName,
  bastionLevelThe,
  BIOME_NAMES,
  BITE_TAG,
  BUILD_IDS,
  BUILDS,
  canAfford,
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
  FELL_COST,
  nextLevel,
  repairCost,
  rearmCost,
  dismantleRefund,
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
  beds,
  bedsFree,
  garageCut,
  healRate,
  listeningDelay,
  rangeAim,
  EXPEDIENTE,
  facilityCount,
  memorialWill,
  fill,
  GLOSSARY,
  HEAD_IDS,
  HEADS,
  HOME_REGION,
  ITEMS,
  itemTag,
  itemsFree,
  itemUnlocked,
  levelUnlocks,
  upgradeLevel,
  soldierCap,
  MISSION_NAMES,
  NAME_MAX,
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
  activeOffer,
  postBlocker,
  randomAppearance,
  REGION_LORE,
  regionIncome,
  REGIONS,
  Rng,
  shopPrice,
  soldierArms,
  soldierReady,
  canSpendPoints,
  PREMISE,
  proposalClock,
  floodedZoneNote,
  contactBlocker,
  underMarea,
  NIGHT_TEXT,
  OUTFIT_NAMES,
  LIVING_TIP,
  craftBlocker,
  rationsPerMission,
  gardenPerMission,
  convoyIncome,
  convoyIn,
  FOUND_WEAPONS,
  meleeWeaponOf,
  spareMags,
  type CraftId,
  weaponLabel,
  MEDICATE_COST,
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
  weaponTier,
  type Appearance,
  type ArmorPart,
  ARMOR_PART_IDS,
  partColor,
  type BaseView,
  BASE_SIEGE_TEXT,
  siegeHeard,
  mustWait,
  PIECE_STATE,
  BUILD_TEXT,
  siegeWarning,
  siegeWarningShort,
  inMissions,
  rationsHelp,
  VEHICLES,
  VEHICLE_SEATS,
  vehicleOf,
  LEVEL_TEXT,
  needsLevelLine,
  nestNoContactTip,
  rewardMultiplier,
  wintry,
  outingsMark,
  timeHelp,
  SIEGE_TEXT,
  DAYS_PER_MISSION,
  type CampaignCommand,
  type CampaignSoldier,
  type CraftDef,
  type CampaignState,
  type Vec2,
  type ItemId,
  type ItemSlot,
  type MissionKind,
  type MissionOffer,
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
import { marked } from '../ui/marks';
import { canQuit, QUIT_BUTTON, quitGame, quitQuestion } from '../ui/quit';
import { graphicsSettings, soundSettings } from '../ui/settingsPanel';
import { graphics } from '../settings';
import { GeoscapeSet, shownOffers } from './geoscape';
import { ClearingSet, footprint, type ArmoryStock, type BaseCursor, type ClearingArt, type ClearingTool } from './clearing';
import { ClearingArtView, type LineupEntry } from './clearingArt';
import { fellBlockerFor, LINE_PIECES, lineBlockerFor, lineLine, palette, palettePieces, pieceCard, placeBlockerFor, placeLine } from './buildMode';
import { makePieceIcons } from './pieceIcons';
import { ARMOR_LEVELS, craftLevel, FreshMarks, markNew, type FreshView } from './fresh';
import { Box3, Vector3 } from 'three';
import { Stage, type Shot } from './stage';
import { sound } from '../audio/bank';
import { uiSound } from '../audio/ui';
import { carriedProfile, kindOf, loadoutEl, modPanel, placeOf, profileOf, weaponCare, weaponDiff, weaponIcon, weaponMarks, weaponPick, weaponStats, type WeaponPickContext } from './arms';
import { soldierPortrait } from './portraits';
import { learnedTags, roleDot, skillsBlock, skillsOf, treePanel, veterancyBadge } from './skillSheet';
import { deadlineEl } from '../ui/deadline';
import { outfitOf, outfitsFor, preloadWardrobe, soldierSex } from '../game/wardrobe';

export interface StrategyHandlers {
  send(cmd: CampaignCommand): void;
  onView(view: BaseView): void;
  onLeave(): void;
  /** The piece in this player's hand on the clearing, for the partner (null: none). */
  onCursor?(c: BaseCursor | null): void;
}

/** The packs' icon (public/ui/synty/icons) for what a level brings, in its alert's row (#31); the family's otherwise. */
const TECH_ICONS: Partial<Record<string, string>> = {
  magWeapons: 'crafting',
  plateArmor: 'armor',
  plasmaGrenades: 'explosives',
  encryption: 'mh-comms',
  plasmaWeapons: 'mh-accuracy',
  nanoMedkit: 'mh-syringe',
  predatorArmor: 'helmet',
  signal: 'sound',
};
const ITEM_ICONS: Partial<Record<string, string>> = {
  fragGrenade: 'explosives',
  smokeGrenade: 'mh-smoke',
  firstAid: 'medkit',
  flashbang: 'mh-flashbang',
  tracerRounds: 'mh-bullets',
  apRounds: 'ammo',
  nanoVest: 'armor',
  mindShield: 'mh-headphones',
};
const PIECE_ICONS: Partial<Record<string, string>> = {
  radio: 'mh-comms',
  quarters: 'mh-tent',
  infirmary: 'heal',
  workshop: 'crafting',
  garden: 'mh-plants',
  quarantine: 'biohazard',
  memorial: 'r-camposanto',
  range: 'target',
  training: 'mh-dumbbell',
  simulation: 'map-pin',
  garage: 'mh-repair',
  echoes: 'screwdriver',
  floodlight: 'lightning',
  wire: 'mh-pliers',
  mine: 'mh-dynamite',
  turret: 'mh-machinegun',
};
const KIND_ICONS = { building: 'home', defence: 'mh-shield', trap: 'mh-targeted' } as const;

/** Personalizar's colours with the clothes not dyed (Ajustes · «Ropa con el color del jugador», off by default). */
const TINT_OFF = 'Para teñir la ropa, activa «Ropa con el color del jugador» en Ajustes.';


interface Alert {
  tone: 'info' | 'good' | 'bad';
  kicker: string;
  title: string;
  /** Typed paragraphs before `text` (a page of the Diario). */
  body?: string[];
  text: string;
  /** Handwritten in the margin. */
  note?: string;
  /** A decision the alert asks for (a rumour to listen to), beside «Entendido»; never a «Ver…» (playtest 18/10 #27: the agenda takes you there). */
  action?: { label: string; go: () => void };
  /** Higher first: a moment pops up only its most important alert. */
  rank?: number;
  /** Where it happens: the globe turns there when it pops up (rumours). */
  region?: RegionId;
  /** The Bastion level whose things it shows, as a row of icons (Ana, 20/10 #31: no list of lines). */
  brought?: number;
}

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

/** When Bielsa's next convoy comes, in words: «tras la próxima misión», «dentro de 6 misiones» (#38). */
function convoyWhen(c: CampaignState): string {
  return inMissions(Math.max(1, convoyIn(c)));
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
  // No «Campaña» (Arturo, 20/10: «no nos está aportando nada»): the Bastion goes up from its card in Construir.
  // «Mapa», no longer «El valle» (Ana, 20/10 #28).
  { view: 'geoscape', label: 'Mapa' },
  { view: 'engineering', label: 'Armería' },
  // «Soldados», no longer «Cuartel» (Arturo, 18/10): the place is the caravan now; the tab is who you have.
  { view: 'barracks', label: 'Soldados' },
  // The base is built on the clearing now (docs/FASE12.md §11.7): the tab is the place, «Asentamiento» (Arturo, 20/10;
  // «Construir» is the palette's action, BUILD_TEXT.build). B goes there.
  { view: 'facilities', label: 'Asentamiento' },
];

/** «la Marea» → «La Marea». */
function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
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
  private readonly geo: GeoscapeSet;
  /** The base in the clearing (docs/FASE12.md §11.7): Campaña and Construir look at it. */
  private readonly clearing: ClearingSet;
  /** Construir: the piece picked on the clearing, and «Desmontar» pressed once. */
  private piece: string | null = null;
  private dismantling = false;
  private partnerCursorTimer = 0;
  /** The clearing is drawn under snow (still winter in the campaign: wintry); null until the first state. */
  private clearingSnow: boolean | null = null;
  /** The siege's defender in hand, to place on the clearing (#58). */
  private postPick: string | null = null;
  private clearingArt: ClearingArt | null = null;
  /** Back from a mission: La Lata comes in up the track the first time the clearing is shown. */
  private arriving = false;
  private readonly top = h('div.strat-top');
  /** The strip's names on hover (playtest 18/10 #56: «que diga qué recurso es»): at once, under what the pointer is on. */
  private readonly topTip = h('div.top-tip', { 'aria-hidden': 'true' });
  /** The top strip's tips that are more than their data-tip's sentences (withTip). */
  private readonly tipBodies = new WeakMap<HTMLElement, () => Node[]>();
  private readonly proposalEl = h('div.strat-proposal.x-panel');
  private readonly viewEl = h('div.strat-view');
  private readonly nav = h('div.strat-nav');
  private readonly modal = h('div.strat-modal');
  private readonly toastEl = h('div.strat-toast');
  private state: CampaignState | null = null;
  private players: PlayerInfo[] = [];
  /** Soldados when the base opens and after a mission (Arturo, 20/10). */
  private view: BaseView = 'barracks';
  private partnerView: BaseView | null = null;
  private seenReport = loadSeenReport();
  /** The pop-up waiting to be read: one per moment (back at the base, or after a scan). */
  private alerts: Alert[] = [];
  /** The story's radio scenes (strategy/transmissions.ts); made in the constructor, which knows this player's seat. */
  private readonly radio: Transmissions;
  private toastTimer = 0;
  private anchorFrame = 0;
  private scroll = new Map<string, number>();
  // Selections inside the screens.
  private region: RegionId | null = null;
  private offer: string | null = null;
  private engTab: 'gear' | 'items' = 'items';
  private engPick: string | null = null;
  private soldier: string | null = null;
  /** The barracks customizer's draft; saved only when the player confirms. */
  private custom: { soldier: string; name: string; nickname: string; appearance: Appearance } | null = null;
  /** The Árbol de habilidades is open on the soldier's sheet (skillSheet.ts keeps its draft). */
  private promoting = false;
  /** Open equipment picker ("soldierId:slot"). */
  private picking: string | null = null;
  /** The alert on screen last time, so turning the globe to it happens once. */
  private shownAlert: Alert | null = null;
  /** "Volver al menú" asked: it takes both players, so it is confirmed first. */
  private leaving = false;
  /** «Salir de Bastión» asked from the menu (desktop app only, ui/quit.ts): its question in the same window. */
  private quitting = false;
  /** Settings open over the base: only for this player, the partner stays where they are. */
  private settingsOpen = false;
  /** The menu (playtest 19/10 #7): one button and Esc open it; the same list as in combat. */
  private menuOpen = false;
  /** What the levels bought have brought and this player hasn't looked at yet: its «Nuevo» (#13, fresh.ts). */
  private readonly freshMarks = new FreshMarks();
  /** News keys already seen (navNews), for the campaign `seenNewsFor`. */
  private readonly seenNews = new Set<string>();
  private seenNewsFor: string | null = null;

  constructor(container: HTMLElement, private readonly mySlot: Slot, private readonly handlers: StrategyHandlers) {
    this.radio = new Transmissions(() => ({ saludo: greeting(this.commanderNames()), yo: greeting([this.playerName(mySlot)]) }), mySlot);
    this.geo = new GeoscapeSet((region) => {
      this.region = region;
      this.offer = (this.state ? shownOffers(this.state) : []).find((o) => o.region === region)?.id ?? null;
      this.geo.focus(region);
      this.render();
    });
    this.clearing = new ClearingSet({
      blocker: (piece, x, y, rot) => (this.state ? placeBlockerFor(this.state, piece, x, y, rot, this.voting()) : 'Un momento.'),
      placeHint: (piece, blocked) => (this.state ? placeLine(this.state, piece, this.voting(), blocked) : ''),
      place: (piece, x, y, rot) => {
        this.send({ type: 'place', piece, x, y, rot });
        // Walls and traps go down one after another; a building (most are only one) or what goes to a vote (#55: all,
        // with the other one here, and one at a time), once.
        if (PIECES[piece].kind === 'building' || PIECES[piece].unique || this.voting()) this.dropTool();
      },
      lineable: (piece) => LINE_PIECES.has(piece),
      lineBlocker: (piece, cells, rot) => (this.state ? lineBlockerFor(this.state, piece, cells, rot, this.voting()) : 'Un momento.'),
      lineHint: (piece, cells, blocked) => (this.state ? lineLine(this.state, piece, cells, this.voting(), blocked) : ''),
      placeLine: (piece, cells, rot) => {
        this.send({ type: 'placeLine', piece, cells, rot });
        // Proposed: the hand is let go, as for a piece.
        if (this.voting()) this.dropTool();
      },
      fell: (tiles) => {
        this.send({ type: 'fell', tiles });
        if (this.voting()) this.dropTool();
      },
      fellBlocker: (trees) => (this.state ? fellBlockerFor(this.state, trees, this.voting()) : null),
      select: (id) => this.pickPiece(id),
      cancel: () => this.dropTool(),
      cursor: (c) => this.handlers.onCursor?.(c),
      postBlocker: (soldier, x, y) => (this.state ? postBlocker(this.state, this.mySlot, soldier, { x, y }) : 'Un momento.'),
      postHint: (soldier, blocked) => {
        const s = this.state?.soldiers.find((x) => x.id === soldier);
        const name = s ? displayName(s.name, s.nickname) : '';
        return blocked ? `${name} · ${blocked}` : name;
      },
      post: (soldier, x, y) => this.post(soldier, { x, y }),
      pickPost: (soldier) => this.pickPost(soldier),
    });
    this.stage.add('geo', this.geo);
    this.stage.add('clearing', this.clearing);
    this.root.append(this.stage.element, this.viewEl, this.top, this.topTip, this.proposalEl, this.nav, this.modal, this.radio.element, this.toastEl);
    this.top.addEventListener('mouseover', (e) => this.showTopTip(e.target as HTMLElement));
    this.top.addEventListener('mouseleave', () => this.topTip.classList.remove('show'));
    container.append(this.root);
    void this.stage.show('clearing', this.clearing.shot(), true);
    handlers.onView(this.view);
    window.addEventListener('keydown', this.onKey);
  }

  private readonly onKey = (e: KeyboardEvent) => {
    if (e.defaultPrevented || !this.state || this.root.classList.contains('launching')) return;
    if ((e.target as HTMLElement | null)?.closest?.('input, textarea')) return;
    // B: in and out of Construir.
    if (e.code === 'KeyB' && !e.ctrlKey && !e.metaKey && !this.modal.classList.contains('show') && (this.view === 'barracks' || this.view === 'facilities')) {
      void this.go(this.view === 'facilities' ? 'barracks' : 'facilities');
      return;
    }
    if (e.key !== 'Escape') return;
    e.preventDefault();
    this.back();
  };

  /** Esc: closes what is open, lets go of what is picked, and otherwise opens the menu (playtest 19/10 #7). */
  private back(): void {
    if (this.picking || this.custom || this.promoting || this.leaving || this.quitting || this.settingsOpen) {
      this.picking = null;
      this.custom = null;
      this.promoting = false;
      this.leaving = false;
      this.quitting = false;
      this.settingsOpen = false;
      this.render();
      return;
    }
    if (this.menuOpen) {
      this.menuOpen = false;
      this.render();
      return;
    }
    // Alerts and events are answered with their own buttons.
    if (this.modal.classList.contains('show')) return;
    // Construir: the piece in hand first, then the one picked.
    if (this.view === 'facilities' && this.clearing.currentTool.kind !== 'look') return this.dropTool();
    if (this.view === 'facilities' && this.piece) return this.pickPiece(null);
    // The siege's squad (#58): the defender in hand first.
    if (this.view === 'squad' && this.postPick) return this.pickPost(null);
    this.menuOpen = true;
    this.render();
  }

  // ------------------------------------------------------------ public

  /**
   * `before`: what the base looked like when the squad took off. A screen made
   * anew after a mission compares with it, so the mission's news (levels, new
   * operations, the clock) still pop up, after the debrief, for both commanders.
   */
  update(c: CampaignState, before?: CampaignState | null): void {
    const first = !this.state;
    const prev = this.state ?? before ?? null;
    // A screen made anew after a mission (main.ts passes the base as it took off): La Lata is on its way back.
    if (before && !this.state) this.arriving = true;
    this.state = c;
    this.loadSeenNews(c);
    this.freshMarks.load(c.uid ?? '', bastionLevel(c));
    // News arriving on the screen being looked at is seen at once.
    this.seeNews(this.view);
    // The partner's proposal waits on its note (with «Ver»): it never takes this player away from what they are doing.
    const proposed = !!c.proposal && c.proposal.by !== this.mySlot && prev?.proposal?.text !== c.proposal.text;
    if (proposed) this.blink('.strat-proposal');
    // One's own proposal that costs: said at once that nothing is paid until the yes (playtest 16/10 #5).
    if (c.proposal && c.proposal.by === this.mySlot && prev?.proposal?.text !== c.proposal.text && proposalCost(c, c.proposal.action)) {
      this.toast(`Propuesto a ${this.partner()?.name ?? 'el otro jugador'}: se paga cuando diga que sí.`);
    }
    if (this.clearingSnow !== wintry(c)) {
      this.clearingSnow = wintry(c);
      this.clearing.setArt((this.clearingArt = new ClearingArtView(this.clearingSnow)));
    }
    this.clearingArt?.setVehicle?.(vehicleOf(c));
    this.clearingArt?.setArmory?.(this.armoryStock(c));
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
    if (!c.activeMission && !this.reportPending(c)) this.lineup([]);
    if (this.reportPending(c) && this.view !== 'debrief') void this.go('debrief');
    // A screen made anew opens on Soldados (Arturo, 20/10), on its own shot.
    else if (first && this.view === 'barracks') void this.go('barracks');
    else this.render();
    // Back without a debrief to read: the clearing is already on screen.
    if (this.arriving && !this.reportPending(c) && this.setFor(this.view) === 'clearing') this.arrive();
  }

  /** La Lata comes back up the track and stops by the caravan (clearingArt.ts), with the camera on it. */
  private arrive(): void {
    this.arriving = false;
    this.clearing.focusCaravan(true);
    void this.clearingArt?.arrive?.();
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
      this.clearing.hold(false);
      this.clearingArt?.setShowcase?.(null, '');
      this.clearing.focusCaravan(true);
      await this.stage.show('clearing', this.clearing.shot());
      await new Promise((r) => setTimeout(r, 1600));
      this.stage.blackout();
      await new Promise((r) => setTimeout(r, 450));
      return;
    }
    if (!art) {
      // No clearing drawn yet (no state): straight to black.
      this.stage.blackout();
      await new Promise((r) => setTimeout(r, 450));
      return;
    }
    this.clearing.setBuilding(false);
    this.clearing.leave();
    this.clearingArt?.setShowcase?.(null, '');
    // From the squad by La Lata (#44), on that shot: they run to the doors and get on; else the caravan from above.
    if (art.lineupShot) {
      this.clearing.hold(true);
      await this.stage.show('clearing', art.lineupShot());
    } else {
      this.clearing.hold(false);
      this.clearing.focusCaravan(true);
      await this.stage.show('clearing', this.clearing.shot());
    }
    // Never held up for long whatever happens to the drawing.
    await Promise.race([art.depart?.() ?? Promise.resolve(), new Promise((r) => setTimeout(r, 9000))]);
    this.stage.blackout();
    await new Promise((r) => setTimeout(r, 450));
  }

  destroy(): void {
    window.removeEventListener('keydown', this.onKey);
    window.clearTimeout(this.partnerCursorTimer);
    cancelAnimationFrame(this.anchorFrame);
    // A transmission open at take-off held the sound down at 40 % for the whole mission.
    this.radio.panel.dispose();
    this.clearing.dispose();
    this.stage.dispose();
    this.root.remove();
  }

  // -------------------------------------------------------- navigation

  private async go(asked: BaseView): Promise<void> {
    // The old «Bastión» and Campaña are gone (Arturo, 20/10): whatever still asks for them lands on Soldados.
    const view = asked === 'hub' || asked === 'research' ? 'barracks' : asked;
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
    // The siege's squad is placed on the clearing (#58): its grid and edge band show, as when building.
    const posting = view === 'squad' && this.siegeNow();
    this.postPick = null;
    if (!posting) this.clearing.setPosts([]);
    this.clearing.setBuilding(view === 'facilities' || posting);
    // The soldiers out about the clearing at their ease in the Asentamiento (Arte's clearingLife.ts, 22/10 #1).
    this.clearingArt?.setLife?.(view === 'facilities' ? this.lifeEntries() : []);
    // Soldados and the Armería are fixed shots of the clearing (Arte's): the rig lets go of the camera,
    // and takes it back only once the stage has eased it home from one.
    const fixed = this.fixedShot(view);
    if (fixed) this.clearing.hold(true);
    if (view !== 'barracks') this.clearingArt?.setShowcase?.(null, '');
    if (this.setFor(view) !== 'clearing') this.clearing.leave();
    else if (this.arriving && !fixed) {
      // Back from a mission, the first time on the clearing (after the debrief): La Lata comes in.
      this.render();
      await this.stage.show('clearing', this.shotFor(view));
      this.clearing.hold(false);
      this.arrive();
      return;
    }
    this.render();
    await this.stage.show(this.setFor(view), this.shotFor(view));
    if (!fixed && this.view === view) this.clearing.hold(false);
    // Back from a mission onto Soldados (a fixed shot, Arturo 20/10): La Lata is back by the caravan all the same.
    if (this.arriving && fixed && view !== 'debrief' && this.setFor(view) === 'clearing') this.arrive();
  }

  /** The 3D set each screen happens in: the valley, or the clearing (Soldados, the Armería and the squad too, by the caravan). */
  private setFor(view: BaseView): 'geo' | 'clearing' {
    return view === 'geoscape' ? 'geo' : 'clearing';
  }

  /** Soldados, the Armería, the squad and the debrief: fixed shots of the clearing, when its drawing has them (Arte, 18/10 #44). */
  private fixedShot(view: BaseView): boolean {
    const art = this.clearingArt;
    return (view === 'barracks' && !!art?.soldierShot) || (view === 'engineering' && !!art?.armoryShot) || (((view === 'squad' && !this.siegeNow()) || view === 'debrief') && !!art?.lineupShot);
  }

  /**
   * The row's shot kept in what the panels leave free (playtest 19/10 #2: on a laptop the windows went over
   * the soldiers), as Stage.fitted does: its box from the first place to the last, from the feet to over the
   * heads, and before them the room their cards take.
   */
  private framedLineup(): Shot | undefined {
    const art = this.clearingArt;
    const shot = art?.lineupShot?.();
    const n = this.lineupCount;
    if (!shot || !art?.padPosition || !n) return shot;
    const toward = shot.pos.clone().sub(shot.target).setY(0).normalize();
    const side = new Vector3(-toward.z, 0, toward.x);
    const fit = new Box3();
    // Each place's own width: over the heads, and on the ground before them for the cards. These points, not
    // the box round them, are what is fitted (a row at a slant has a box far bigger than itself, framed small).
    const points: Vector3[] = [];
    for (let i = 0; i < n; i++) {
      const p = art.padPosition(i, n);
      for (const s of [-0.7, 0.7]) points.push(p.clone().addScaledVector(side, s).setY(2.3), p.clone().addScaledVector(side, s).addScaledVector(toward, 1.8));
    }
    for (const p of points) fit.expandByPoint(p);
    this.framedKey = this.lineupKey(n);
    // In the middle of the screen, as Ana drew it (20/10 #16): the list of soldiers on the left doesn't push it over.
    return { ...shot, fit, fitPoints: points, centred: true };
  }

  /** Where the row stood when it was last framed (lineupKey). */
  private framedKey = '';

  /** Where a row of `n` stands now, its ends: it moves once La Lata has come in and parked (clearingArt placeLineup). */
  private lineupKey(n: number): string {
    const at = this.clearingArt?.padPosition;
    if (!at || !n) return '';
    const [a, b] = [at.call(this.clearingArt, 0, n), at.call(this.clearingArt, n - 1, n)];
    return [a.x, a.z, b.x, b.z].map((v) => v.toFixed(2)).join(',');
  }

  /**
   * The row framed again where it stands now (0.8.4: framed on arriving, before La Lata had parked and the row
   * moved beside it, it stood off to the right, half out of the screen); the cards' frame loop asks.
   */
  private reframeLineup(): void {
    if ((this.view !== 'squad' && this.view !== 'debrief') || this.siegeNow()) return;
    if (this.lineupKey(this.lineupCount) === this.framedKey) return;
    void this.stage.show('clearing', this.shotFor(this.view));
  }

  /** How many places the lineup has now (lineup()), for its box. */
  private lineupCount = 0;

  /** The squad standing (its screen, the debrief): by La Lata on the clearing. */
  private lineup(entries: (LineupEntry | null)[]): void {
    this.lineupCount = entries.length;
    // Each sees their own half on the left, by their name (playtest 18/10 #51): the second player's row in a mirror.
    const c = this.state;
    const mirror = !!c && c.players.length > 1 && c.players[1] === this.mySlot;
    // A commander alone: one row, with no step in its middle between two halves (19/10, #2).
    this.clearingArt?.setLineup?.(entries, { mirror, halves: !!c && c.players.length > 1 });
  }

  /** Who is out about the clearing in the Asentamiento: the soldiers on their feet, both commanders' (a handful of them, but the squad by La Lata: Arte's). */
  private lifeEntries(): LineupEntry[] {
    const c = this.state;
    if (!c) return [];
    return c.soldiers
      .filter((s) => s.alive && s.woundedDays <= 0)
      .map((s) => ({ template: SOLDIER, color: this.playerColor(s.owner), appearance: s.appearance, gear: this.gearFor(c, s), seed: s.id }));
  }

  /** The soldier looked at, standing on the clearing: Soldados, and its customizer. */
  private showcase(...args: Parameters<NonNullable<ClearingArt['setShowcase']>>): void {
    this.clearingArt?.setShowcase?.(...args);
  }

  private shotFor(view: BaseView): Shot {
    switch (view) {
      case 'geoscape':
        return this.geo.shot();
      case 'facilities':
        return this.clearing.shot();
      case 'engineering': {
        // Arte's bench and rack (Ana, 20/10 #29), in the middle of the screen between the list and the sheet.
        const shot = this.clearingArt?.armoryShot?.();
        return shot ? { ...shot, centred: true } : this.clearing.shot();
      }
      case 'barracks':
        return this.clearingArt?.soldierShot?.() ?? this.clearing.shot();
      case 'squad':
        // The siege's squad is the clearing from above, round the caravan (#58).
        if (this.siegeNow()) {
          this.clearing.focusCaravan(true);
          return this.clearing.shot();
        }
        return this.framedLineup() ?? this.clearing.shot();
      case 'debrief':
        // Framed as the Escuadra's (#26: it came out to the right).
        return this.framedLineup() ?? this.clearing.shot();
      default:
        return this.clearing.shot();
    }
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

  /** The other one is here: everything on the base goes to a vote, one at a time (Arturo, 18/10 #55; campaign.ts propose). */
  private voting(): boolean {
    return (this.state?.players.length ?? 0) > 1 && !!this.partner()?.connected;
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
  /**
   * Why spending the common stores waits, with the other one here (Ana, 21/10 #31: recruiting and buying are proposed,
   * as building): a proposal is pending, this player's (who must answer) or the other's; '' otherwise.
   */
  private pendingWhy(c: CampaignState): string {
    if (!this.voting() || !c.proposal) return '';
    return c.proposal.by === this.mySlot ? `Esperando a que ${this.partner()!.name} conteste.` : 'Hay una propuesta pendiente: contesta arriba.';
  }

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
          h('small.x-muted', {}, `Esperando a ${partner?.name ?? 'el otro jugador'}`),
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

  /**
   * What the Armería's bench and rack show (Arte's, Ana 20/10 #29): what is there, not what the soldiers carry: the
   * weapons nobody holds, the accessories nobody has on, the improvements loose.
   */
  private armoryStock(c: CampaignState): ArmoryStock {
    const items: Partial<Record<ItemId, number>> = {};
    for (const id of Object.keys(ITEMS) as ItemId[]) {
      const free = itemsFree(c, id);
      if (free > 0) items[id] = free;
    }
    return { weapons: (c.foundWeapons ?? []).filter((w) => !w.holder).map((w) => w.kind), items, mods: c.weaponMods ?? {} };
  }

  /** Tier diamonds, coloured like the guns: steel, magnetic, plasma. */
  private tierMarks(tier: number): HTMLElement {
    return h(`span.tier.t${tier}`, { title: `Nivel ${tier}` }, ...Array.from({ length: tier }, () => h('i')));
  }

  /** Filled and empty marks for "2 of 3" counts (contacts, squad slots). */
  private meter(value: number, max: number, title: string): HTMLElement {
    return h('span.x-meter', { title }, ...Array.from({ length: max }, (_, i) => h(`i${i < value ? '.on' : ''}`)), h('b', {}, `${value}/${max}`));
  }

  /**
   * What the Bastion is now, by name (#13: never «nivel N»; Lore's names), and the missions flown towards what it
   * can become (Arturo, 18/10 #40: levels are bought, there is no experience).
   */
  private levelBadge(c: CampaignState): HTMLElement {
    const level = bastionLevel(c);
    const next = nextLevel(c);
    return h(
      'span.strat-level',
      { 'data-tip-name': bastionLevelName(level), 'data-tip': next ? `Para ${bastionLevelThe(next.level)}: ${next.cost} § y ${next.missions} misiones hechas (llevas ${next.flown}).\n${LEVEL_TEXT.about}` : LEVEL_TEXT.top },
      h('b', {}, bastionLevelName(level).toUpperCase()),
      next ? this.bar(Math.min(next.flown, next.missions), next.missions) : null,
    );
  }

  /**
   * What a Bastion level brought, as a row of icons (Ana, 20/10 #31: wherever what an upgrade brings is shown):
   * its techniques, belt items, pieces and buildings' upgrades, each its own icon or its family's, in the family's
   * colour; «Name: what it is for» on hover. In the alert on going up.
   */
  private broughtIcons(level: number): HTMLElement[] {
    const icon = (file: string, family: string, name: string, detail: string) =>
      h(`span.unlock-icon.${family}`, { title: `${name}: ${detail}`, 'aria-label': name, style: { '--unlock-icon': `url('/ui/synty/icons/${file}.webp')` } });
    const pieces = (Object.keys(PIECES) as (keyof typeof PIECES)[]).filter((id) => id !== 'caravan');
    return [
      ...Object.values(TECHS)
        .filter((t) => t.level === level)
        .map((t) => icon(TECH_ICONS[t.id] ?? 'mh-xp', 'xp', t.name, t.description)),
      ...Object.values(ITEMS)
        .filter((i) => i.level === level)
        .map((i) => icon(ITEM_ICONS[i.id] ?? 'mh-crate', 'flag', i.name, i.description)),
      ...pieces.filter((id) => PIECES[id].level === level).map((id) => icon(PIECE_ICONS[id] ?? KIND_ICONS[PIECES[id].kind], 'pick', PIECE_TEXT[id].name, PIECE_TEXT[id].line)),
      // A building going up a level («Barracón, nivel 2»): its own icon, what that level does on hover.
      ...pieces.flatMap((id) =>
        PIECES[id].kind === 'building'
          ? [2, 3]
              .filter((to) => upgradeLevel(PIECES[id].level, to) === level)
              .map((to) => icon(PIECE_ICONS[id] ?? KIND_ICONS.building, 'pick', `${PIECE_TEXT[id].name}, nivel ${to}`, PIECE_TEXT[id].levels?.[to - 1] ?? PIECE_TEXT[id].line))
          : [],
      ),
    ];
  }

  /** broughtIcons under its heading, or nothing for a level that brought nothing. */
  private broughtRow(level: number, heading: string): HTMLElement[] {
    const icons = this.broughtIcons(level);
    return icons.length ? [h('div.x-group', {}, heading), h('div.unlock-icons', {}, ...icons)] : [];
  }

  /** "Armas magnéticas · Sala de simulación": what a Bastion level unlocks. */
  private unlocksText(level: number): string {
    const list = levelUnlocks(level);
    return list.length ? `Desbloquea: ${list.join(' · ')}.` : 'Sin desbloqueos nuevos.';
  }

  /** Remembers each panel's scroll across re-renders. */
  private panel(key: string, side: 'left' | 'right' | 'left.compact' | 'right.compact', ...children: (Node | null | false | undefined)[]): HTMLElement {
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
    // Only the words (Ana, 20/10 #15: no «KESSRA», no «El claro» over them); Lore's key words in bold (marked).
    this.alerts.unshift({
      tone: 'info',
      kicker: '',
      title: '',
      body: PREMISE.lines,
      text: '',
    });
  }

  /** XCOM-style pop-ups for what happened while time passed. */
  private collectAlerts(prev: CampaignState, c: CampaignState): void {
    const fresh: Alert[] = [];
    // Each level, a page of the doctor's diary on going up: its line, what the Bastion is now, what it brought. After a
    // mission it waits until the debrief is closed.
    for (let level = bastionLevel(prev) + 1; level <= bastionLevel(c); level++) {
      const page = EXPEDIENTE[level - 1]!;
      fresh.push({
        tone: 'good',
        // No page number: it was the level's (#13).
        kicker: 'DIARIO',
        rank: 90,
        title: page.title,
        // One line of the page (playtest: «mucho texto»).
        body: [page.summary],
        text: `El Bastión es ${bastionLevelA(level)}.`,
        brought: level,
        note: page.note,
      });
    }
    // Of what the campaign logged as a pop-up, only the siege (Ana, 21/10 #26, #28: the windows after a mission kept
    // her from Soldados; missions, rumours, Nidos, the convoy and errands are on the Mapa, read when she goes there).
    // The events that ask for a decision have their own window (renderModal). New entries sit at the front.
    const last = prev.log[0];
    const seen = last ? c.log.findIndex((e) => e.day === last.day && e.text === last.text) : c.log.length;
    for (const e of c.log.slice(0, seen === -1 ? c.log.length : seen).reverse()) {
      if (e.alert?.kicker === 'Asedio') fresh.push(this.logAlert(c, e.tone, e.alert, e.text));
    }
    // The clock as the days pass.
    if (c.day > prev.day) sound('ui.tick');
    this.flushAlerts(fresh);
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
   * Every new alert as a window, one after another, the most important first, after any already waiting: with
   * Novedades gone with Campaña (Arturo, 20/10), a Nido or a convoy left out was something not known.
   */
  private flushAlerts(fresh: Alert[]): void {
    if (!fresh.length) return;
    this.geo.setScanning(false);
    this.alerts.push(...[...fresh].sort((a, b) => (b.rank ?? 0) - (a.rank ?? 0)));
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
    // La Lata as it is now (#45): the valley's icon is its own (valley.css).
    this.root.dataset.vehicle = String(vehicleOf(c));
    this.renderTop(c);
    this.renderProposal(c);
    this.renderNav(c);
    clear(this.viewEl);
    switch (this.view) {
      case 'geoscape':
        this.renderGeoscape(c);
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
      // Before the first outing, nothing beside the name (playtest 19/10 #4: not «Aún sin salir»).
      h('div.strat-brand', { 'data-tip-name': 'Salidas', 'data-tip': timeHelp(bastionLevel(c)) }, h('b', {}, 'BASTIÓN'), c.missionsFlown ? h('span', {}, outingsMark(c.missionsFlown)) : null),
      this.siegeEl(c),
      h(
        'div.strat-res',
        {},
        // The day, always in sight (Ana and Arturo, 22/10 #48): a mission is a day, and so is a day waited.
        h('span.strat-day', { 'data-tip-name': `Día ${c.day}`, 'data-tip': timeHelp(bastionLevel(c)) }, h('i', { 'aria-hidden': 'true' }), h('b', {}, `DÍA ${c.day}`)),
        // The names and lines are the glossary's (Lore), short for the medicines.
        this.withTip(h('span.strat-credits', { 'data-tip-name': 'Suministros', 'data-tip': GLOSSARY.Suministros }, h('i', {}, '§'), h('b', {}, String(c.credits)), this.owedEl(c, 'credits')), () => [
          // What Bielsa's convoy brings and when, first (#38); the glossary's line under it.
          h('div.tip-figures', {}, h('span', {}, h('em', {}, `+${convoyIncome(c)}`), ` con el convoy, ${convoyWhen(c)}`)),
          h('p.tip-rule.first', {}, GLOSSARY.Suministros),
        ]),
        h('span.strat-credits.medicines', { 'data-tip-name': 'Medicinas', 'data-tip': GLOSSARY.Medicinas }, h('i', {}, '✚'), h('b', {}, String(c.medicines ?? 0))),
        // Food and parts (docs/FASE4.md): the base eats every day; the Taller makes weapons and armour with parts.
        // What a mission eats is in Lore's why on hover (playtest 19/10 #6: «importante, pero no ahí»). Red when
        // what is left, with the Huerto's, does not cover the next mission.
        this.withTip(
          h(
            `span.strat-credits.food${c.hungry || (c.food ?? 0) + gardenPerMission(c) < rationsPerMission(c) ? '.x-bad' : ''}`,
            { 'data-tip-name': 'Raciones', 'data-tip': rationsHelp() },
            h('i', { 'aria-hidden': 'true' }),
            h('b', {}, String(c.food ?? 0)),
          ),
          () => this.rationsTip(c),
        ),
        h('span.strat-credits.parts', { 'data-tip-name': 'Piezas', 'data-tip': GLOSSARY.Piezas }, h('i', { 'aria-hidden': 'true' }), h('b', {}, String(c.parts ?? 0)), this.owedEl(c, 'parts')),
        this.levelBadge(c),
      ),
      h(
        'div.strat-doom',
        { 'data-tip-name': capitalize(doomName(bastionLevel(c))), 'data-tip': `${doomStatus(bastionLevel(c), c.doom)}. ${bastionLevel(c) >= PARTITURA_LEVEL ? GLOSSARY.Marea : 'Si se llena, la campaña está perdida.'} Quemar Nidos la hace retroceder.` },
        h('small', {}, doomName(bastionLevel(c)).toUpperCase()),
        pips,
        h('b', {}, bastionLevel(c) >= PARTITURA_LEVEL ? `${Math.max(0, DOOM_MAX - c.doom)} km` : `${c.doom}/${DOOM_MAX}`),
      ),
      // No players here (playtest 19/10 #10): where the other one is shows on the tabs, and in the Escuadra. Nor
      // Novedades (Ana, 20/10 #30: nothing more on the top strip): they are in Campaña.
      // One button (playtest 19/10 #7): Continuar, Ajustes, the main menu and leaving the game are in it.
      h('button.x-btn.strat-menu', { onclick: () => ((this.menuOpen = true), this.render()), title: 'Menú (Esc)' }, 'Menú'),
    );
  }

  /**
   * The next siege (docs/FASE12.md §11.5): when, and with the Sala de radio how
   * many and by where, in the strip; red the mission before, and while it is on.
   */
  private siegeEl(c: CampaignState): HTMLElement | null {
    if (!c.siege) return null;
    const now = c.offers.find((o) => o.siege);
    if (now) return h('div.strat-siege.now', { 'data-tip-name': SIEGE_TEXT.name, 'data-tip': BASE_SIEGE_TEXT.defenders }, `${SIEGE_TEXT.name.toUpperCase()}${now.night ? ` · ${NIGHT_TEXT.label.toUpperCase()}` : ''}`);
    const heard = siegeHeard(c);
    const full = siegeWarning(heard.in, heard.count !== undefined ? { count: heard.count, sides: heard.sides ?? [] } : undefined);
    return h(`div.strat-siege${heard.in <= 1 ? '.near' : ''}`, { 'data-tip-name': SIEGE_TEXT.name, 'data-tip': full }, siegeWarningShort(heard.in));
  }

  /** The name (and what it is for) of what the pointer is on in the top strip, under it and inside the window. */
  private showTopTip(target: HTMLElement): void {
    const el = target.closest<HTMLElement>('[data-tip-name]');
    if (!el || !this.top.contains(el)) {
      this.topTip.classList.remove('show');
      return;
    }
    clear(this.topTip);
    // A sentence a line (Ana, 21/10 #33: «no un bloque»); some say their figures first, big (tipBodies).
    const body = this.tipBodies.get(el)?.() ?? sentences(el.dataset.tip ?? '').map((line) => h('p', {}, ...figures(line)));
    append(this.topTip, h('b', {}, el.dataset.tipName ?? ''), ...body);
    this.topTip.classList.add('show');
    const r = el.getBoundingClientRect();
    const host = this.root.getBoundingClientRect();
    const w = this.topTip.offsetWidth;
    this.topTip.style.left = `${Math.max(8, Math.min(host.width - w - 8, r.left + r.width / 2 - w / 2 - host.left))}px`;
    this.topTip.style.top = `${r.bottom - host.top + 8}px`;
  }

  /** `el`'s tip in the top strip drawn by `body` when shown (showTopTip), not from its data-tip. */
  private withTip(el: HTMLElement, body: () => (Node | null)[]): HTMLElement {
    this.tipBodies.set(el, () => body().filter((n): n is Node => !!n));
    return el;
  }

  /**
   * The rations' tip (#33): what a mission eats and what the Huertos grow, big; how many missions what there is lasts;
   * then the rule and the hunger, a line each, the hunger in amber.
   */
  private rationsTip(c: CampaignState): (Node | null)[] {
    const eat = rationsPerMission(c);
    const grow = gardenPerMission(c);
    const net = eat - grow;
    const lasts = net > 0 ? Math.floor((c.food ?? 0) / net) : null;
    return [
      c.hungry ? h('p.tip-bad', {}, 'Hay hambre.') : null,
      h(
        'div.tip-figures',
        {},
        h('span', {}, h('em', {}, `−${eat}`), ' por misión'),
        grow ? h('span.tip-good', {}, h('em', {}, `+${grow}`), ' de los Huertos') : null,
      ),
      lasts === null ? h('p.tip-good', {}, 'Los Huertos dan lo que se come.') : h(`p${lasts < 2 ? '.tip-bad' : ''}`, {}, lasts ? `Hay para ${lasts} ${lasts === 1 ? 'misión' : 'misiones'}.` : 'No hay para la próxima misión.'),
      ...sentences(rationsHelp()).map((line, i) => h(`p.tip-rule${i ? '' : '.first'}${/hambre/i.test(line) ? '.tip-warn' : ''}`, {}, ...figures(line))),
    ];
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
      case 'placeLine': {
        const def = PIECES[action.piece];
        const n = action.cells.length;
        const [first] = action.cells;
        return {
          rows: [['Suministros', costText(def.cost.supplies * n)], ...(def.cost.parts ? [['Piezas', `${def.cost.parts * n}`] as [string, string]] : []), ['Qué hace', `${n} × ${PIECE_TEXT[action.piece].name.toLowerCase()}. ${PIECE_TEXT[action.piece].line}`]],
          see: first
            ? () => {
                if (this.view !== 'facilities') void this.go('facilities');
                this.clearing.focusTile(first);
              }
            : null,
        };
      }
      case 'fell': {
        const [first] = action.tiles;
        return {
          rows: [['Suministros', costText(FELL_COST * action.tiles.length)], ['Qué hace', action.tiles.length === 1 ? 'Talar un árbol.' : `Talar ${action.tiles.length} árboles.`]],
          see: first
            ? () => {
                if (this.view !== 'facilities') void this.go('facilities');
                this.clearing.focusTile(first);
              }
            : null,
        };
      }
      // Mending, setting again and taking down go to a vote too (18/10 #55).
      case 'mend':
      case 'rearm':
      case 'dismantle': {
        const p = c.bastion.pieces.find((x) => x.id === action.piece);
        if (!p) return { rows: [], see: null };
        const rows: [string, string][] =
          action.type === 'mend'
            ? [['Suministros', costText(repairCost(p).supplies)], ...(repairCost(p).parts ? [['Piezas', `${repairCost(p).parts}`] as [string, string]] : []), ['Qué hace', `Repara ${PIECE_TEXT[p.piece].the}.`]]
            : action.type === 'rearm'
              ? [['Piezas', `${rearmCost(p)}`], ['Qué hace', `Repone ${PIECE_TEXT[p.piece].the}.`]]
              : [['Qué hace', `Desmonta ${PIECE_TEXT[p.piece].the}${dismantleRefund(p) ? `: devuelve ${costText(dismantleRefund(p))}` : ''}.`]];
        return {
          rows,
          see: () => {
            if (this.view !== 'facilities') void this.go('facilities');
            this.clearing.focusPiece(p.id);
          },
        };
      }
      case 'levelUp': {
        const next = nextLevel(c);
        return { rows: next ? [['Suministros', costText(next.cost)], ['Qué hace', `El Bastión pasa a ser ${bastionLevelA(next.level)}.`]] : [], see: null };
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
      // Spending the common supplies is proposed too (Ana, 21/10 #31): a recruit, a purchase in the Armería.
      case 'recruit':
        return { rows: [['Suministros', costText(recruitCost(c))], ['Qué hace', `Un recluta para ${this.playerName(action.for)}.`]], see: () => void this.go('barracks') };
      case 'buy':
        return { rows: [['Suministros', costText(shopPrice(c, action.item))], ['Qué hace', `Comprar: ${ITEMS[action.item].name}.`]], see: () => void this.go('engineering') };
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
    const title = mine ? `Pendiente: se paga cuando ${this.partner()?.name ?? 'el otro jugador'} diga que sí` : `Pendiente: se paga si aceptas la propuesta de ${this.playerName(c.proposal.by)}`;
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
        h('div.proposal-text', {}, kicker, h('span', {}, `Has propuesto ${p.text}. Esperando a ${this.partner()?.name ?? 'el otro jugador'}…`), cost, detail, proposalTimer(true)),
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
    // Only one's own: a soldier's skills are its owner's alone, there or not (playtest 18/10 #42).
    const promotions = c.soldiers.filter((s) => s.owner === this.mySlot && this.hasPoints(s));
    if (promotions.length) pending.set('barracks', { n: `▲${promotions.length}`, urgent: false, what: promotions.map((s) => `${s.name}: talento por elegir`) });
    const fresh = this.freshViews(c);
    for (const item of items) {
      const here = partner?.connected && this.partnerView === item.view;
      const todo = pending.get(item.view);
      const isNew = fresh.has(item.view as FreshView);
      this.nav.append(
        h(
          `button${this.view === item.view ? '.on' : ''}${item.view === 'squad' ? '.urgent' : ''}`,
          { 'data-view': item.view, onclick: () => void this.go(item.view), title: todo ? todo.what.join(' · ') : '', 'aria-label': item.label },
          item.label,
          todo ? h(`span.nav-count${todo.urgent ? '.urgent' : ''}`, {}, todo.n) : null,
          // Something a level brought, not looked at yet (#13): until the pointer goes over it there.
          isNew && !todo ? h('span.new-tag', { title: 'Hay cosas nuevas' }, 'Nuevo') : null,
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
          ...shownOffers(c).map((o) => ({ key: `offer:${o.id}`, label: `Nueva misión: ${o.name}`, urgent: !!(o.story || o.final || o.facility) })),
          ...c.rumors.map((r) => ({ key: `rumor:${r.id}`, label: `Rumor en ${REGIONS[r.region].name}`, urgent: false })),
        ],
      ],
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

  /** Whether what `key` names came with a level bought and this player hasn't looked at it yet (#13). */
  private readonly isNew = (key: string): boolean => !!this.state && this.freshMarks.isFresh(key, bastionLevel(this.state));

  /** The pointer went over it: its «Nuevo» goes, and the tab's with it once nothing new is left there. */
  private readonly seenNew = (key: string): void => {
    if (this.freshMarks.see(key) && this.state) this.renderNav(this.state);
  };

  /** Where this player has something new that a level brought: Construir, the Armería (a building's upgrade, only if one can take it). */
  private freshViews(c: CampaignState): Set<FreshView> {
    const out = new Set<FreshView>();
    for (const t of this.freshMarks.fresh(bastionLevel(c))) {
      if (t.raise && !c.bastion.pieces.some((p) => p.piece === t.raise!.piece && (p.level ?? 1) === t.raise!.to - 1)) continue;
      out.add(t.view);
    }
    return out;
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

  // ---------------------------------------------------------- geoscape

  private renderGeoscape(c: CampaignState): void {
    this.geo.update(c, this.region);
    // During a siege, only the siege (#50): the rest come back after it.
    const offers = [...shownOffers(c)].sort(
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
      // «Misiones», not «Operaciones» (Ana, 21/10 #25: it wasn't understood). The day is on the top strip (#48).
      h('div.x-head', {}, 'Misiones'),
      list,
      // Or a day at the Bastión, without going out (Ana and Arturo, 22/10 #48), under what there is to go to.
      this.waitDayEl(c),
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
                  r.contacted ? this.chip('Asaltable', 'bad') : this.chip('Sin contacto', '', nestNoContactTip(r.id, contactCost(c))),
                ),
              ),
            ),
          ]
        : []),
      ...this.questRows(c),
      ...this.rumorRows(c),
      h('div.x-head', { title: 'Pulsa una zona del mapa para ver sus datos' }, 'Red de enclaves'),
      this.meter(network, contactCapacity(c), 'Zonas contactadas / máximo (los centros de comunicaciones dan más)'),
    );
    this.viewEl.append(left);

    const offer = offers.find((o) => o.id === this.offer);
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
          `‹ Misiones · ${shownOffers(c).length}`,
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
          const offer = shownOffers(c).find((o) => o.quest === q.id);
          // Its step waits out a siege (#50), hidden like every other operation.
          const where = offer ? REGIONS[offer.region].name : c.offers.some((o) => o.quest === q.id) ? 'Tras el asedio' : 'Esperando vuestra decisión';
          const whose = q.owner !== null && c.players.length > 1 ? ` · de ${this.playerName(q.owner)}` : '';
          return h(
            `button.x-row${offer && this.offer === offer.id ? '.selected' : ''}`,
            { style: { '--row-accent': '#2f9e6a' }, onclick: () => (offer ? this.focusOffer(offer.id) : undefined) },
            h('div', {}, h('b', {}, def.title), h('small', {}, `Paso ${q.step + 1} de ${def.steps.length} · ${where}${whose}`)),
            h('span.x-side', {}, offer?.expiresDay != null ? aheadChip(c, offer.expiresDay) : ''),
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
    // A better vehicle (#45): what La Lata becomes, and how many it takes then.
    if (r.vehicle !== undefined) rewards.push(`La Lata pasa a ser ${VEHICLES[r.vehicle]?.a ?? ''} (${VEHICLE_SEATS[r.vehicle] ?? '?'} plazas)`);
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
        h('p.x-text.briefing-text', {}, ...marked(offerBriefing(c, o))),
        offerSignature(c, o) ? h('p.x-note', {}, offerSignature(c, o)) : null,
        h(
          'dl.briefing',
          {},
          h('dt', {}, 'Tipo'),
          h('dd', {}, SIEGE_TEXT.name),
          h('dt', {}, 'Dificultad'),
          h('dd', {}, h('span.stars', {}, '★'.repeat(o.difficulty)), ` ${DIFFICULTY[o.difficulty]}`),
        ),
        chosen ? h('button.x-btn.primary', { onclick: () => void this.go('squad') }, 'Ir a la escuadra') : this.deployButton(c, o),
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
                : 'MISIÓN',
      ),
      h('h2.x-title', {}, o.name),
      h('p.x-sub', {}, `${REGIONS[o.region].name} · ${biome}${o.night ? ` · ${NIGHT_TEXT.label}` : ''}${quest ? ` · paso ${quest.step + 1} de ${QUESTS[quest.id].steps.length}` : ''}`),
      h('p.x-text.briefing-text', {}, ...marked(offerBriefing(c, o))),
      offerSignature(c, o) ? h('p.x-note', {}, offerSignature(c, o)) : null,
      // The living hold it (18/10 #47): the dead go for them too.
      o.living ? h('p.x-text.x-warn', {}, LIVING_TIP) : null,
      h(
        'dl.briefing',
        {},
        h('dt', {}, 'Tipo'),
        h('dd', {}, MISSION_NAMES[o.kind]),
        h('dt', {}, 'Dificultad'),
        h('dd', {}, h('span.stars', {}, '★'.repeat(o.difficulty)), ` ${DIFFICULTY[o.difficulty]}`),
        h('dt', {}, 'Recompensa'),
        h('dd', {}, rewards.filter(Boolean).join(' · ') || 'La victoria'),
      ),
      // Called off here, not from the Escuadra (playtest 19/10 #3: there it read as the same as going back).
      chosen
        ? h(
            'div.alert-actions',
            {},
            h('button.x-btn.primary', { onclick: () => void this.go('squad') }, 'Ir a la escuadra'),
            h('button.x-btn.danger', { onclick: () => this.send({ type: 'cancelMission' }) }, 'Cancelar misión'),
          )
        : this.deployButton(c, o),
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
      [contacted >= capacity, 'Construid o mejorad la Sala de radio para más contactos'],
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
      r.facility ? h('p.x-text.x-bad', {}, `Nido: hace avanzar ${doomSubject(bastionLevel(c))} cada ${missionsText(missionsFor(14))}.${r.contacted ? '' : ` ${nestNoContactTip(r.id, contactCost(c))}`}`) : null,
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
      // Only the tiers the Bastion's level allows (#13: what isn't had yet isn't shown).
      const level = bastionLevel(c);
      const tiers = ([
        ['a1', ARMOR_TIERS[0]!.name, baseArmorTier(c) >= 1],
        ['a2', ARMOR_TIERS[1]!.name, baseArmorTier(c) >= 2],
        ['a3', ARMOR_TIERS[2]!.name, baseArmorTier(c) >= 3],
      ] as [string, string, boolean][]).filter(([key, , made]) => made || ARMOR_LEVELS[key]! <= level);
      const crafts = Object.values(CRAFTS).filter((d) => (d.kind === 'arms' || d.kind === 'ammo' || d.kind === 'mod') && craftLevel(d) <= level);
      // A pick left on something no longer shown: the first that is.
      const shown = (k: string) => (k.startsWith('a') ? tiers.some(([t]) => t === k) : k.startsWith('m:') ? crafts.some((d) => `m:${d.id}` === k) : true);
      if (this.engPick && !shown(this.engPick)) this.engPick = null;
      this.engPick ??= (c.foundWeapons ?? [])[0] ? `f:${c.foundWeapons![0]!.id}` : (tiers.find(([, , made]) => !made)?.[0] ?? 'a1');
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
      for (const [key, name, unlocked] of tiers) {
        const tier = Number(key[1]);
        const row = h(
          `button.x-row.tier-row${this.engPick === key ? '.selected' : ''}${unlocked ? '' : '.dim'}`,
          { onclick: () => ((this.engPick = key), this.render()) },
          this.tierMarks(tier),
          h('b', {}, name),
          unlocked ? h('span.x-side', {}, '✓') : (this.craftChip(c, key) ?? this.chip('En el Taller', '', 'Se hace en el Taller')),
        );
        rows.push(markNew(row, !unlocked && this.isNew(`armor:${key}`), () => this.seenNew(`armor:${key}`)));
      }
      // The Taller's weapons, munition and improvements (docs/FASE8.md §A, FASE9.md §5): munition three times, the rest again and again.
      rows.push(h('div.x-group', {}, 'Hacer en el Taller'));
      for (const def of crafts) {
        const work = c.base.works.find((w) => w.project === def.id);
        const done = def.kind === 'ammo' && c.crafted?.includes(def.id);
        const row = h(
          `button.x-row${this.engPick === `m:${def.id}` ? '.selected' : ''}${done || !craftBlocker(c, def.id) || work ? '' : '.dim'}`,
          { onclick: () => ((this.engPick = `m:${def.id}`), this.render()) },
          h('b', {}, weaponIcon(def.kind === 'mod' ? 'mod' : def.kind === 'ammo' || !def.makes ? 'ammo' : kindOf(def.makes)), def.name),
          h('span.x-side', {}, done ? '✓' : work ? this.chip(`Taller · ${missionsText(missionsFor(work.daysLeft))}`, 'good') : def.makes ? h('small.x-place', {}, placeOf(def.makes)) : ''),
        );
        rows.push(markNew(row, this.isNew(`craft:${def.id}`), () => this.seenNew(`craft:${def.id}`)));
      }
    } else {
      // Only what goes on the belt (docs/FASE9.md §4): the special rounds are the weapons' improvements now.
      // Only what the Bastion's level allows (#13).
      const belt = Object.values(ITEMS).filter((i) => i.slot === 'utility' && itemUnlocked(c, i.id));
      if (this.engPick && !belt.some((i) => i.id === this.engPick)) this.engPick = null;
      for (const item of belt) {
        this.engPick ??= item.id;
        const row = h(
          `button.x-row${this.engPick === item.id ? '.selected' : ''}`,
          { onclick: () => ((this.engPick = item.id), this.render()) },
          h('div', {}, h('b', {}, item.name), h('small', {}, this.chip(costText(shopPrice(c, item.id))))),
          h('span.x-side', { title: 'Se equipan en la ficha de cada soldado' }, this.stockText(c, item.id)),
        );
        rows.push(markNew(row, this.isNew(`item:${item.id}`), () => this.seenNew(`item:${item.id}`)));
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
          this.chip(this.stockText(c, item.id) || 'No tienes ninguno', '', 'Se equipa en la ficha del soldado; si cae, se pierde'),
          unlocked ? this.chip(costText(cost)) : this.chip(needsLevelLine(item.level), 'warn'),
        ),
        // With the other one here a purchase is proposed (Ana, 21/10 #31): one at a time, as the rest.
        unlocked ? h('button.x-btn.primary', { disabled: !canAfford(c, cost) || !!this.pendingWhy(c), onclick: () => this.send({ type: 'buy', item: item.id }) }, 'Comprar') : null,
        unlocked ? this.whyEl(this.pendingWhy(c) || this.shortfall(cost)) : null,
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
      h('div.x-group', {}, mine.some((s) => s.owner !== this.mySlot) ? `Tus soldados y los de ${this.partner()?.name ?? 'el otro jugador'}` : 'Tus soldados'),
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
    // A line of the debrief's list (renderDebrief).
    if (!parts.length) return h('li.debrief-damage', {}, 'En la base: todo en pie.');
    return h('li.debrief-damage.warn', {}, `En la base: ${parts.join(', ')}. `, h('button.x-btn.small', { onclick: () => void this.go('facilities') }, BUILD_TEXT.build));
  }

  /**
   * Nobody can go out (mustWait, docs/FASE12.md §11.6): only the hurt left, and nothing to recruit
   * with. Time passes with the missions, so here it passes by waiting, both agreeing as before.
   */
  /**
   * «Esperar un día» (Ana and Arturo, 22/10 #48): a day goes by at the Bastión as on a mission (rations, wounds, the
   * Taller, the Marea), once both say so; alone, at once. Pressed, it waits for the other, and says so, as «Lanzar
   * misión»; the other's yes is said on it. Not with a siege coming, nor while an event waits for its answer.
   */
  private waitDayEl(c: CampaignState): HTMLElement {
    const ready = c.advanceReady[this.mySlot];
    const partner = this.partner();
    const together = !!partner?.connected && c.players.length > 1;
    const partnerReady = together && !ready && c.advanceReady[partner!.slot];
    const why = activeOffer(c)?.siege ? 'Viene el asedio: no se puede esperar.' : c.event ? 'Antes, responded al acontecimiento.' : '';
    return h(
      'div.wait-day',
      {},
      h(
        `button.x-btn${ready ? '.ready' : partnerReady ? '.primary' : ''}`,
        { disabled: !!why && !ready, title: `Un día sin salir: se come, se curan las heridas y avanzan el Taller y ${doomSubject(bastionLevel(c))}.`, onclick: () => this.send({ type: 'advance', ready: !ready }) },
        ready ? 'Esperando…' : 'Esperar un día',
      ),
      why ? h('small.x-muted', {}, why) : ready && together ? h('small.x-muted', {}, `Falta ${partner!.name}`) : partnerReady ? h('small.wait-asked', {}, `${partner!.name} quiere esperar un día`) : null,
    );
  }

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
    // The other player's only while they are away, to prepare them (Ana, 21/10 #30: each sees their own).
    const theirs = c.soldiers.filter((s) => s.owner !== this.mySlot && listed(s) && this.commands(s.owner)).sort((a, b) => Number(b.alive) - Number(a.alive) || b.board.length - a.board.length || totalBars(b.skills) - totalBars(a.skills));
    if (!this.soldier || ![...mine, ...theirs].some((s) => s.id === this.soldier)) this.soldier = mine.find((s) => s.alive)?.id ?? mine[0]?.id ?? theirs[0]?.id ?? null;
    // The soldiers by their faces, in a grid (Ana, 22/10 #49: «la cara de los soldados en cuadrícula»): the bust, the
    // name they go by, the rank; in a corner, a talent to choose (▲) or what keeps them in (wounds, fever, fallen).
    const row = (s: CampaignSoldier) => {
      const points = s.owner === this.mySlot && this.hasPoints(s);
      const state = s.alive && !s.infection && !s.woundedDays ? null : this.healthTag(c, s, true);
      const tile = h(
        `button.soldier-tile${this.soldier === s.id ? '.selected' : ''}${s.alive ? '' : '.dim'}`,
        {
          style: { '--accent': this.playerColor(s.owner) },
          title: displayName(s.name, s.nickname),
          onclick: () => {
            this.soldier = s.id;
            this.promoting = false;
            this.picking = null;
            this.custom = null;
            this.render();
          },
        },
        h('span.st-face', { 'aria-hidden': 'true' }),
        h('b.st-name', {}, s.nickname || s.name.split(' ')[0]!),
        veterancyBadge(s.board, s.appearance?.sex === 'f'),
        points ? h('span.st-points', { title: 'Talento por elegir' }, '▲') : null,
        state,
      );
      this.bustOn(tile, s);
      return tile;
    };
    const count = mine.filter((s) => s.alive).length;
    const cap = soldierCap(c);
    const price = recruitCost(c);
    const blockedRecruit = this.why([count >= cap, `Ya tienes tus ${cap} soldados: es el máximo por jugador`], [bedsFree(c) <= 0, 'No quedan camas: para reclutar más, construid un Barracón.'], [true, this.pendingWhy(c)], [true, this.shortfall(price)]);
    // Recruiting, on a card of its own: what it is, what it costs, what it needs, and one big button.
    const recruit = h(
      'div.recruit-card',
      { title: `Como mucho ${cap} soldados por jugador` },
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
        h('div.soldier-grid', {}, ...mine.map(row)),
        recruit,
        theirs.length ? h('div.x-head', {}, `De ${this.playerName(theirs[0]!.owner)}`, this.commands(theirs[0]!.owner) ? h('small', {}, ' · no está: los preparas tú') : null) : null,
        h('div.soldier-grid', {}, ...theirs.map(row)),
      ),
    );
    const s = c.soldiers.find((x) => x.id === this.soldier);
    const draft = this.custom && this.custom.soldier === s?.id ? this.custom : null;
    // A fallen one: their grave where they would stand (playtest 21/10 #13).
    this.showcase(s?.alive ? SOLDIER : null, s ? this.playerColor(s.owner) : '#fff', draft?.appearance ?? s?.appearance, s ? this.gearFor(c, s) : undefined, s?.id, !!s && !s.alive);
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
              {
                onclick: () => {
                  // Every piece of the wardrobe on its way (5.7 MB, Arte): then each look over a choice takes a few ms.
                  preloadWardrobe();
                  this.custom = { soldier: s.id, name: s.name, nickname: s.nickname ?? '', appearance: { ...(s.appearance ?? defaultAppearance(s.owner, undefined, undefined, s.id)) } };
                  this.render();
                },
              },
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
        // The board only for its own commander, the other here or not (Arturo, 18/10 #42).
        ...skillsBlock(skillsOf(s), s.owner === this.mySlot && s.alive, () => ((this.promoting = true), this.render())),
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
        if (this.custom === d) this.showcase(SOLDIER, this.playerColor(s.owner), d.appearance, this.gearFor(this.c(), s), s.id);
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
    const worn = outfitOf(look.character);
    const preview = (appearance: Appearance) => this.custom === d && this.showcase(SOLDIER, this.playerColor(s.owner), appearance, this.gearFor(this.c(), s), s.id);
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
    // The colours only show with the clothes dyed (Ajustes, off by default: Arturo, 21/10); without, they wait there.
    const tinted = graphics().squadTint;
    const copy: HTMLButtonElement = h(
      'button.x-btn.small',
      {
        disabled: !!blocked || !others.length || !tinted,
        title: tinted ? 'Guarda a este soldado y pinta a todos tus soldados con estos colores; cada uno conserva su nombre, su cabeza y su complexión.' : TINT_OFF,
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
      // The clothes (Sidekick, Arte's wardrobe.ts): among those of the soldier's sex; unset, the ones their id picks,
      // the same in every screen. Over one, the soldier on the pedestal wears it, and back to theirs on leaving.
      h(
        'div.x-field',
        {},
        h('small', {}, 'Ropa'),
        h(
          'div.x-chips',
          {},
          h(`button${worn ? '' : '.on'}`, { title: 'Una al azar, y no cambia.', onclick: () => set({ character: undefined }) }, 'Al azar'),
          ...outfitsFor(soldierSex(look, s.id)).map((id) =>
            h(
              `button${worn === id ? '.on' : ''}`,
              { onclick: () => set({ character: id }), onpointerenter: () => preview({ ...look, character: id }), onpointerleave: () => preview(look) },
              OUTFIT_NAMES[id] ?? id,
            ),
          ),
        ),
      ),
      chips('Cabeza', HEAD_IDS, HEADS, look.head, (head) => set({ head })),
      chips('Complexión', BUILD_IDS, BUILDS, look.build, (build) => set({ build })),
      h('div.x-head', {}, 'Colores'),
      tinted ? null : h('p.x-muted.tint-off', {}, TINT_OFF),
      h(
        `div.x-parts${tinted ? '' : '.off'}`,
        { inert: !tinted },
        armour,
        helmetRow,
        pieceRow('torso', 'Torso'),
        pieceRow('arms', 'Brazos'),
        pieceRow('legs', 'Piernas'),
        // What the trim colours on these figures (Arte): the hood, beret or cap while it has no colour of its own. No
        // visor row: Synty's figures draw none.
        row('Detalles', look.secondary, ARMOR_COLORS, (secondary) => ({ secondary }), note('capucha, boina y gorra')),
        mark,
      ),
      h(
        'div.x-actions',
        {},
        h('button.x-btn.small', { onclick: () => ((d.appearance = randomAppearance(new Rng(Date.now() >>> 0))), this.render()) }, 'Aleatorio'),
        h(
          'button.x-btn.small',
          // Its own look by its id (the engine's, Gameplay 21/10): not everyone back in a helmet.
          { title: 'Vuelve al aspecto de serie de este soldado', onclick: () => ((d.appearance = defaultAppearance(s.owner, undefined, s.appearance?.sex, s.id)), this.render()) },
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
      editable: s.owner === this.mySlot && s.alive,
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
    const tool = this.clearing.currentTool;
    // The palette's pictures, made once (pieceIcons.ts); the palette drawn again when they are.
    makePieceIcons(palettePieces(bastionLevel(c)), () => this.view === 'facilities' && this.render());
    const panels = palette(
      { c, tool, partner: this.voting() ? this.partner()!.name : null, me: this.mySlot, isNew: this.isNew },
      {
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
        seen: this.seenNew,
      },
    );
    this.viewEl.append(this.stack('build-left', 'left', ...panels));
    // On the right, the Bastion in figures (#38), and under it the card of a piece picked on the clearing.
    const p = this.piece ? c.bastion.pieces.find((x) => x.id === this.piece) : undefined;
    const card = p
      ? pieceCard(
          { c, piece: p, dismantling: this.dismantling, partner: this.voting() ? this.partner()!.name : null, isNew: this.isNew },
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
            seen: this.seenNew,
          },
        )
      : null;
    this.viewEl.append(this.stack('build-right', 'right', this.bastionStats(c), card));
  }

  /**
   * Panels one over the other in a side's column, each its own steel sheet (Ana, 21/10 #40); null ones left out.
   * The column scrolls as one, and keeps where it was (as panel()).
   */
  private stack(key: string, side: 'left' | 'right', ...panels: (Node[] | null)[]): HTMLElement {
    const wrap = h(`div.strat-${side}.panel-stack`, { onscroll: (e: Event) => this.scroll.set(key, (e.target as HTMLElement).scrollTop) });
    for (const children of panels) {
      if (!children) continue;
      const sheet = h('div.x-panel');
      append(sheet, ...children);
      wrap.append(sheet);
    }
    const top = this.scroll.get(key) ?? 0;
    queueMicrotask(() => (wrap.scrollTop = top));
    return wrap;
  }

  /**
   * The Bastion in figures (Ana, 21/10 #38: «tienes 6/8 camas…, todo en el mismo panel», not a list of
   * «Efectos»): tiles of a number and what it is, the beds and the rations first, then what each kind of
   * building adds, only those there are. The contacts are the Mapa's (#37).
   */
  private bastionStats(c: CampaignState): Node[] {
    const stats: { icon: string; value: string; label: string; tone?: 'good' | 'warn' | 'bad' }[] = [];
    const alive = c.soldiers.filter((s) => s.alive).length;
    stats.push({ icon: 'beds', value: `${alive}/${beds(c)}`, label: 'Camas', tone: alive >= beds(c) ? 'warn' : undefined });
    const eat = rationsPerMission(c);
    const grow = gardenPerMission(c);
    stats.push({ icon: 'food', value: `−${eat}`, label: 'Raciones por misión', tone: (c.food ?? 0) + grow < eat ? 'bad' : undefined });
    if (grow) stats.push({ icon: 'garden', value: `+${grow}`, label: 'De los Huertos', tone: 'good' });
    stats.push({ icon: 'supplies', value: `+${convoyIncome(c)}`, label: `Convoy ${convoyWhen(c)}` });
    if (facilityCount(c, 'infirmary')) stats.push({ icon: 'heal', value: `×${healRate(c)}`, label: 'Curación' });
    if (garageCut(c).wounds) stats.push({ icon: 'garage', value: `−${garageCut(c).wounds}`, label: 'Días de baja' });
    if (rangeAim(c)) stats.push({ icon: 'aim', value: `+${rangeAim(c)}`, label: 'Puntería' });
    if (facilityCount(c, 'workshop')) stats.push({ icon: 'shop', value: `−${Math.round((1 - shopPrice(c, 'smokeGrenade') / ITEMS.smokeGrenade.cost) * 100)} %`, label: 'Accesorios' });
    if (facilityCount(c, 'simulation')) stats.push({ icon: 'supplies', value: `+${Math.round((rewardMultiplier(c) - 1) * 100)} %`, label: 'Suministros de misión' });
    if (facilityCount(c, 'training')) stats.push({ icon: 'squad', value: `${squadMax(c)}`, label: 'Plazas en la escuadra' });
    if (memorialWill(c)) stats.push({ icon: 'will', value: `+${memorialWill(c)}`, label: 'Voluntad' });
    if (listeningDelay(c)) stats.push({ icon: 'radio', value: `+${listeningDelay(c)}`, label: `Días: ${doomSubject(bastionLevel(c))}` });
    return [
      h('div.x-head', {}, 'Bastión'),
      h(
        'div.bastion-stats',
        {},
        ...stats.map((st) => h(`div.bstat${st.tone ? `.${st.tone}` : ''}`, { 'data-icon': st.icon }, h('i', { 'aria-hidden': 'true' }), h('b', {}, st.value), h('small', {}, st.label))),
      ),
    ];
  }

  /** Construir: the piece in hand (or felling) is let go; the siege's defender in hand too (#58). */
  private dropTool(): void {
    this.postPick = null;
    this.clearing.setTool({ kind: 'look' });
    this.render();
  }

  /** The siege is the base's mission now, and its squad is placed on the clearing (Arturo, 18/10 #58). */
  private siegeNow(): boolean {
    const c = this.state;
    return !!c && !!activeOffer(c)?.siege && !!this.clearingArt;
  }

  /** One of this player's defenders taken up (null: let go), to place on the clearing (#58). */
  private pickPost(soldier: string | null): void {
    this.postPick = soldier;
    this.render();
  }

  /** `soldier` starts the siege at `at` (#58); the next of this player's with no post is taken up, if any. */
  private post(soldier: string, at: Vec2): void {
    this.send({ type: 'post', soldier, at });
    const c = this.state;
    if (!c) return;
    const posts = c.siegePosts ?? {};
    const mine = c.squad.filter((id) => c.soldiers.find((s) => s.id === id)?.owner === this.mySlot);
    const i = mine.indexOf(soldier);
    this.pickPost([...mine.slice(i + 1), ...mine.slice(0, Math.max(0, i))].find((id) => !posts[id]) ?? null);
  }

  /**
   * The siege's defenders (Arturo, 18/10 #58): this player's to place on the clearing, one at a time (picked
   * here or on the clearing, then a click on a tile); the other's as they stand, in their colour. Whoever has
   * no post starts by the Bastión, as before.
   */
  private postsPanel(c: CampaignState, squad: CampaignSoldier[]): HTMLElement {
    const posts = c.siegePosts ?? {};
    const mine = squad.filter((s) => s.owner === this.mySlot);
    const theirs = squad.filter((s) => s.owner !== this.mySlot);
    if (this.postPick && !mine.some((s) => s.id === this.postPick)) this.postPick = null;
    const want: ClearingTool = this.postPick ? { kind: 'post', soldier: this.postPick } : { kind: 'look' };
    const tool = this.clearing.currentTool;
    if (tool.kind !== want.kind || (tool.kind === 'post' && want.kind === 'post' && tool.soldier !== want.soldier)) this.clearing.setTool(want);
    this.clearing.setPosts(
      squad
        .filter((s) => posts[s.id])
        .map((s) => ({
          id: s.id,
          name: displayName(s.name, s.nickname),
          color: this.playerColor(s.owner),
          x: posts[s.id]!.x,
          y: posts[s.id]!.y,
          mine: s.owner === this.mySlot,
          entry: { template: SOLDIER, color: this.playerColor(s.owner), appearance: s.appearance, gear: this.gearFor(c, s), seed: s.id },
        })),
    );
    const row = (s: CampaignSoldier) => {
      const own = s.owner === this.mySlot;
      return h(
        `button.x-row.soldier${this.postPick === s.id ? '.selected' : ''}`,
        { style: { '--row-accent': this.playerColor(s.owner) }, disabled: !own, onclick: () => this.pickPost(this.postPick === s.id ? null : s.id) },
        roleDot(s.board),
        h('b', {}, displayName(s.name, s.nickname)),
        veterancyBadge(s.board, s.appearance?.sex === 'f'),
        posts[s.id] ? this.chip('Puesto', 'good', 'Empieza el asedio donde está en el claro') : this.chip('Junto al Bastión', '', 'Sin puesto: empieza junto al Bastión'),
      );
    };
    const picked = mine.find((s) => s.id === this.postPick);
    return this.panel(
      'squad-left',
      'left.compact',
      h('div.x-head', { title: BASE_SIEGE_TEXT.defenders }, `Tus defensores · ${mine.length}`),
      h('p.x-muted.post-help', {}, picked ? `${displayName(picked.name, picked.nickname)}: clic para elegir su puesto, Esc para soltar.` : 'Elige un soldado y haz clic en el claro donde empezará el asedio. Si no tiene puesto, empieza junto al Bastión.'),
      h('div.x-list', {}, ...mine.map(row)),
      picked && posts[picked.id] ? h('button.x-btn.small.post-home', { onclick: () => this.send({ type: 'post', soldier: picked.id, at: null }) }, 'Que empiece junto al Bastión') : null,
      theirs.length ? h('div.x-head', {}, `De ${this.playerName(theirs[0]!.owner)} · ${theirs.length}`) : null,
      theirs.length ? h('div.x-list', {}, ...theirs.map(row)) : null,
    );
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
      this.viewEl.append(h('div.strat-center-top', {}, h('div.x-panel', {}, h('p.x-text', {}, 'No hay ninguna misión elegida.'), h('button.x-btn', { onclick: () => void this.go('geoscape') }, 'Ir al mapa'))));
      this.lineup([]);
      return;
    }
    // The siege (docs/FASE12.md §11.5): every soldier fit to fight defends, however many; nobody picks, nobody leaves.
    const siege = !!offer.siege;
    const squad = c.squad.map((id) => c.soldiers.find((s) => s.id === id)).filter((s): s is CampaignSoldier => !!s);
    const max = siege ? squad.length : squadMax(c);
    const limit = squadLimit(c, this.connected());
    const pads = siege ? squad : squadPads(c).map((id) => squad.find((s) => s.id === id) ?? null);
    // The siege's on the clearing, each where they will start (#58), not in a row.
    const posting = siege && this.siegeNow();
    this.lineup(posting ? [] : pads.map((s) => (s ? { template: SOLDIER, color: this.playerColor(s.owner), appearance: s.appearance, gear: this.gearFor(c, s), seed: s.id } : null)));
    const myCount = squad.filter((s) => s.owner === this.mySlot).length;

    const roster = c.soldiers.filter((s) => s.owner === this.mySlot && s.alive);
    // The other player's, to see but not to pick: each puts in and takes out only their own (playtest 18/10 #43).
    const lent = c.soldiers.filter((s) => s.owner !== this.mySlot && s.alive);
    const pickRow = (s: CampaignSoldier) => {
      const on = c.squad.includes(s.id);
      const ready = soldierReady(s);
      const gear = [s.loadout.utility, s.loadout.ammo].filter((g): g is ItemId => !!g).map((g) => ITEMS[g].name);
      return h(
        `button.x-row.soldier${on ? '.selected' : ''}${ready ? '' : '.dim'}`,
        // In a siege who defends is the healthy, all of them: nothing to pick.
        { style: { '--row-accent': this.playerColor(s.owner) }, disabled: siege || s.owner !== this.mySlot || (!ready && !on), onclick: () => this.send({ type: 'squad', soldier: s.id, on: !on }) },
        roleDot(s.board),
        h('b', { title: gear.join(' · ') }, displayName(s.name, s.nickname)),
        veterancyBadge(s.board, s.appearance?.sex === 'f'),
        on ? this.chip('✓', 'good', 'En la escuadra') : ready ? h('span') : s.infection ? this.chip('Fiebre', 'bad', 'Mordido: necesita el tratamiento antes de salir') : this.chip(missionsText(missionsFor(s.woundedDays)), 'warn', 'Herido'),
      );
    };
    if (posting) this.viewEl.append(this.postsPanel(c, squad));
    else {
      this.viewEl.append(
        this.panel(
          'squad-left',
          'left.compact',
          h(
            'div.x-head',
            { title: siege ? BASE_SIEGE_TEXT.defenders : 'Pulsa un soldado para meterlo o sacarlo. Los del otro jugador los elige él; los huecos que no llene se quedan vacíos. El equipo se cambia en Soldados.' },
            siege ? `Tus soldados · ${myCount}` : `Tus soldados · ${myCount}/${limit}`,
          ),
          h('div.x-list', {}, ...roster.map(pickRow)),
        ),
      );
      // The other player's on the right (Ana, 21/10 #15), as each sees their own on their left in the row.
      if (lent.length)
        this.viewEl.append(
          this.panel(
            'squad-right',
            'right.compact',
            h('div.x-head', { title: 'Los elige su jugador' }, `De ${this.playerName(lent[0]!.owner)} · ${squad.length - myCount}`),
            h('div.x-list', {}, ...lent.map(pickRow)),
          ),
        );
    }

    const ready = c.launchReady[this.mySlot];
    const partner = this.partner();
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
              h('b', {}, this.playerName(slot)),
              h('small', {}, c.launchReady[slot] ? 'A PUNTO' : 'ELIGIENDO'),
            ),
          ),
        )
      : null;
    // Down under the row, centred (Ana, 20/10 #16, as her drawing): who is ready over one panel, the mission on its
    // left (its name and what it is for, whole: no kicker, no «Botín · Brena · ★», no picture of the vehicle) and
    // the buttons on its right (playtest 19/10 #3; on a laptop the mission's window at the top hid the soldiers, #2).
    // Who is ready is on the plates over the panel (Ana, 21/10 #16: no «Arturo espera para salir» here).
    const status = siege ? BASE_SIEGE_TEXT.defenders : '';
    this.viewEl.append(
      h(
        'div.squad-bottom',
        {},
        readyBoard,
        h(
          'div.x-panel.squad-mission',
          {},
          h(
            'div.squad-text',
            {},
            h('h1', {}, offer.name, offer.night ? h('small', {}, NIGHT_TEXT.label) : null),
            // What the mission is for, in view before choosing the squad (playtest 13/10: it was only a tooltip).
            h('p.x-text.briefing-text', {}, ...marked(offerBriefing(c, offer))),
            offer.living ? h('p.x-text.x-warn', {}, LIVING_TIP) : null,
          ),
          h(
            'div.squad-actions',
            {},
            h(
              `button.x-btn.big.squad-launch${ready ? '.ready' : ''}${partnerReady ? '.waiting' : ''}`,
              { disabled: !c.squad.length, onclick: () => this.send({ type: 'launch', ready: !ready }) },
              // «Lanzar misión», and «A punto» once pressed (Ana, 21/10 #16: no «Salir con Arturo»; Lore: no gender, as on
              // the ready board); pressed again, back.
              ready ? 'A PUNTO' : siege ? 'DEFENDER EL BASTIÓN' : 'LANZAR MISIÓN',
            ),
            status ? h('small.x-muted', {}, status) : null,
            // Not «Cancelar misión» here (#3: it read as the same as going back); it is on the mission's card on the map.
            // The first mission is the only way in: no going back to the base before it (Ana, 22/10).
            c.story.done === 0 && offer.story === 1 ? null : h('button.x-btn', { onclick: () => void this.go('barracks') }, 'Volver al Bastión'),
          ),
        ),
      ),
    );

    if (posting) return;
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
            // Its rank in words, no role's dot before it (Ana, 20/10 #17).
            h('small', {}, veterancyName(s)),
            // Low cards (playtest 19/10 #8): whose is its colour and its side; «Quitar», a small x in the corner.
            s.owner === this.mySlot && !siege
              ? h('button.card-x', { title: 'Quitar de la escuadra', 'aria-label': `Quitar a ${displayName(s.name, s.nickname)}`, onclick: () => this.send({ type: 'squad', soldier: s.id, on: false }) }, '×')
              : null,
          )
        : recruitable
          ? this.recruitCard(c, cost)
          : h(
              'div.lineup-card.empty',
              { title: spare ? 'Elige un soldado a la izquierda' : '' },
              h('b', {}, 'Libre'),
              room && this.padSide(c, i) === this.mySlot ? h('small', {}, 'Para reclutar más soldados, construid un Barracón') : null,
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
    const pending = this.pendingWhy(c);
    return h(
      'div.lineup-card.empty.recruit',
      {},
      h('b', {}, 'Libre'),
      h(
        'button.x-btn',
        {
          disabled: !afford || !!pending,
          title: pending || (afford ? 'Un recluta nuevo, directo a la escuadra.' : this.shortfall(cost)),
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
        this.reframeLineup();
        // In the room right of the soldiers' list, half of them a row.
        const perRow = Math.ceil(count / 2);
        const width = cards.parentElement?.clientWidth ?? window.innerWidth;
        const from = (this.viewEl.querySelector<HTMLElement>('.strat-left')?.getBoundingClientRect().right ?? 0) + 12;
        const room = Math.max(perRow * 86, width - from - 12);
        const cw = Math.max(76, Math.min(124, room / perRow - 10));
        restyle(cards, '--card-w', `${cw}px`);
        restyle(cards, 'width', `${perRow * (cw + 10)}px`);
        restyle(cards, 'left', `${Math.max(0, from + (room - perRow * (cw + 10)) / 2)}px`);
        // Just above the controls, counted from where the cards' box starts.
        const controls = this.viewEl.querySelector<HTMLElement>('.squad-bottom, .strat-center-bottom');
        const origin = cards.offsetParent?.getBoundingClientRect().top ?? 0;
        const floor = controls ? controls.getBoundingClientRect().top : window.innerHeight - 170;
        restyle(cards, 'top', `${Math.max(0, floor - origin - cards.offsetHeight - 14)}px`);
        this.anchorFrame = requestAnimationFrame(place);
      };
      place();
      return;
    }
    // Where the `i`th of `count` stands by La Lata, for the cards under them.
    const padAt = this.clearingArt?.padPosition?.bind(this.clearingArt);
    if (!padAt) return;
    const step = () => {
      this.reframeLineup();
      const pads = Array.from({ length: count }, (_, i) => this.stage.project(padAt(i, count)));
      const spacing = count > 1 ? Math.min(...pads.slice(1).map((p, i) => Math.abs(p.x - pads[i]!.x))) : 140;
      restyle(cards, '--card-w', `${Math.max(84, Math.min(150, spacing - 8))}px`);
      // One row with a shared height, above the launch controls even after a resize or UI zoom.
      const children = Array.from(cards.children) as HTMLElement[];
      const height = Math.max(0, ...children.map((el) => el.offsetHeight));
      const controls = this.viewEl.querySelector<HTMLElement>('.squad-bottom, .strat-center-bottom');
      const ceiling = controls ? controls.getBoundingClientRect().top - cards.getBoundingClientRect().top - height - 18 : Infinity;
      // Each under its own soldier's feet (playtest 19/10 #2: with the row seen at a slant, one height for all
      // put the near ones' cards over their bodies), and all above the foot of the screen.
      for (const el of children) {
        const p = pads[Number(el.dataset.pad)];
        if (!p) continue;
        restyle(el, 'left', `${p.x}px`);
        restyle(el, 'top', `${Math.min(p.y + 12, ceiling)}px`);
      }
      this.anchorFrame = requestAnimationFrame(step);
    };
    step();
  }

  // ------------------------------------------------------------ debrief

  private renderDebrief(c: CampaignState): void {
    const report = c.lastReport;
    if (!report) {
      void this.go('barracks');
      return;
    }
    const entries: LineupEntry[] = report.squad.map((e) => {
      const s = c.soldiers.find((x) => x.id === e.id);
      // Someone left behind (docs/PRUEBA-2026-10-14.md P1) didn't come back either: a memorial, as the fallen.
      return { template: SOLDIER, color: this.playerColor(e.owner), status: e.status === 'lost' ? 'dead' : e.status, appearance: s?.appearance, gear: s ? this.gearFor(c, s) : undefined, seed: e.id };
    });
    this.lineup(entries);
    // What came back, in pictures of one size (Ana, 22/10 #40: «iconos en vez de texto»), the mission's own thing
    // bigger; then what changed for the base, as chips; the details in each one's title.
    const haul = this.haulTiles(report);
    const events = [
      report.levelTo > report.levelFrom ? this.chip(`¡${bastionLevelName(report.levelTo)}!`, 'good', Array.from({ length: report.levelTo - report.levelFrom }, (_, i) => this.unlocksText(report.levelFrom + i + 1)).join(' ')) : null,
      report.doomChange ? this.chip(report.doomChange > 0 ? `Reloj +${report.doomChange}` : `Reloj ${report.doomChange}`, report.doomChange > 0 ? 'bad' : 'good', doomChangeText(report.levelTo, report.doomChange)) : null,
    ].filter((x): x is HTMLElement => !!x);
    const story = report.victory && report.story ? STORY[report.story - 1]!.after : '';
    const close = () => {
      this.seenReport = this.reportKey(c);
      try {
        localStorage.setItem(REPORT_KEY, this.seenReport);
      } catch {
        /* storage unavailable: the debrief may show again after a reload */
      }
      this.lineup([]);
      void this.go('barracks');
    };
    // Laid out as the Escuadra (Ana, 20/10 #26): the row centred, and under it one panel. Within it, one column (22/10
    // #40): the result's plate on a line of its own, the mission's name under it; the haul; the few lines that say
    // what pictures can't; and at the foot the story's page and «Continuar» on the right, as «Entendido» elsewhere.
    // The bars filled are on each soldier's card, not here.
    const line = (tone: 'good' | 'warn' | 'bad' | '', ...content: (string | HTMLElement)[]) => h(`li.line${tone ? `.${tone}` : ''}`, {}, ...content);
    const lines = [
      // The base fell (report.fallen): what is lost, in red like the other warnings of the debrief.
      report.fallen ? line('bad', h('b', {}, `${BASE_SIEGE_TEXT.fallen}. `), BASE_SIEGE_TEXT.fallenLine) : null,
      this.damageLine(report.damage),
      report.night ? line('', NIGHT_TEXT.report) : null,
    ].filter((x): x is HTMLElement => !!x);
    this.viewEl.append(
      h(
        'div.squad-bottom.debrief-bottom',
        {},
        h(
          `div.x-panel.squad-mission.debrief-card.briefing-head.${report.victory ? 'victory' : 'defeat'}`,
          {},
          h(
            'div.debrief-main',
            {},
            h('h1.debrief-result', {}, report.victory ? 'MISIÓN CUMPLIDA' : 'MISIÓN FRACASADA'),
            h('h2.debrief-name', {}, report.missionName),
            haul.length || events.length ? h('div.debrief-haul', {}, ...haul, events.length ? h('div.debrief-events', {}, ...events) : null) : null,
            lines.length ? h('ul.debrief-lines', {}, ...lines) : null,
            h(
              'div.debrief-foot',
              {},
              story ? h('details.debrief-story', {}, h('summary', {}, 'Leer el parte'), h('p.x-text', {}, story)) : h('span'),
              h('button.x-btn.big.primary', { onclick: close }, 'CONTINUAR'),
            ),
          ),
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
  }

  /**
   * What a mission brought back, as tiles of one size (Ana, 22/10 #40): the supplies (reward, bodies and haul in one),
   * rations, medicines, parts, each weapon found by its silhouette, each accessory, a recruit, La Lata's new vehicle;
   * first and bigger, the thing the mission was for (a story's: «la insulina»). A figure under each, or nothing;
   * the names in their titles.
   */
  private haulTiles(report: NonNullable<CampaignState['lastReport']>): HTMLElement[] {
    const reward = report.reward ?? {};
    const loot = report.loot;
    const tile = (kind: string, figure: string, title: string, extra = '') =>
      // The figure's line always there, empty without one, so every tile is as tall (Gameplay, 22/10).
      h(`div.haul${extra}`, { 'data-haul': kind, title }, h('i.haul-pic', { 'aria-hidden': 'true' }), h('b', {}, figure || '\u00a0'));
    const out: HTMLElement[] = [];
    const goal = report.victory && report.story ? STORY[report.story - 1]?.item : undefined;
    // Its name as the tag over whoever carries it in the field (Lore's itemTag: «INSULINA»).
    if (goal) out.push(h('div.haul.goal', { 'data-haul': goal === 'la insulina' ? 'medicine' : 'objective', title: capitalize(goal) }, h('i.haul-pic', { 'aria-hidden': 'true' }), h('b', {}, itemTag(goal))));
    const credits = (reward.credits ?? 0) + report.salvage + (loot?.supplies ?? 0);
    const creditsWhy = [reward.credits ? `Recompensa: ${reward.credits}` : '', report.bodies ? `${report.bodies} cuerpos registrados: ${report.salvage}` : '', loot?.supplies ? `Botín: ${loot.supplies}` : ''].filter(Boolean).join(' · ');
    if (credits) out.push(tile('supplies', `+${credits}`, `Suministros · ${creditsWhy}`));
    const food = (reward.food ?? 0) + (loot?.food ?? 0);
    if (food) out.push(tile('food', `+${food}`, 'Raciones'));
    const medicines = (reward.medicines ?? 0) + (loot?.medicines ?? 0);
    if (medicines) out.push(tile('medicines', `+${medicines}`, 'Medicinas'));
    const parts = (reward.parts ?? 0) + (loot?.parts ?? 0) + (report.scrap ?? 0);
    if (parts) out.push(tile('parts', `+${parts}`, 'Piezas'));
    for (const w of loot?.weapons ?? []) out.push(tile(`w-${kindOf(w)}`, '', FOUND_WEAPONS[w].name, FOUND_WEAPONS[w].better ? '.better' : ''));
    const items = new Map<ItemId, number>();
    for (const item of loot?.items ?? []) items.set(item, (items.get(item) ?? 0) + 1);
    for (const [item, n] of items) out.push(tile(item, n > 1 ? `×${n}` : '', ITEMS[item].name));
    if (reward.recruit) out.push(tile('recruit', '+1', 'Un recluta'));
    if (reward.vehicle) out.push(tile('vehicle', '', capitalize(VEHICLES[reward.vehicle]?.the ?? 'La Lata')));
    return out;
  }

  // -------------------------------------------------------------- modal

  /** One thing at a time: campaign over, then a pending event, then alerts, then the promotion screen. */
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
      // Asked only with the other one here: it takes them back to the menu too.
      content = h(
        'div.x-panel',
        {},
        h('div.alert-kicker', {}, 'MENÚ DE INICIO'),
        h('h2.x-title', {}, '¿Volver al menú de inicio?'),
        h('p.x-text', {}, `La campaña queda guardada. ${this.partner()?.name ?? 'El otro jugador'} vuelve también al menú.`),
        h(
          'div.alert-actions',
          {},
          h('button.x-btn.primary', { onclick: () => ((this.leaving = false), (this.menuOpen = false), this.handlers.onLeave()) }, 'Volver al menú'),
          h('button.x-btn', { onclick: () => ((this.leaving = false), this.render()) }, 'Seguir aquí'),
        ),
      );
    } else if (this.menuOpen && !c.outcome) {
      // The same list as in combat (hud.ts renderMenu), one entry a line, all alike (playtest 19/10 #7).
      const partnerHere = c.players.length > 1 && !!this.partner()?.connected;
      content = h(
        'div.game-menu-card.panel',
        {},
        h('h2', {}, 'Menú'),
        h('button.menu-entry', { onclick: () => ((this.menuOpen = false), this.render()) }, 'Continuar'),
        h('button.menu-entry', { onclick: () => ((this.settingsOpen = true), this.render()) }, 'Ajustes'),
        h(
          'button.menu-entry',
          { onclick: () => (partnerHere ? ((this.leaving = true), this.render()) : ((this.menuOpen = false), this.handlers.onLeave())), title: 'La campaña queda guardada' },
          'Volver al menú de inicio',
        ),
        // Closing the game, in the desktop app only (playtest 17/10).
        canQuit() ? h('button.menu-entry', { onclick: () => ((this.quitting = true), this.render()) }, QUIT_BUTTON) : null,
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
                h('p.x-warn', {}, `Esperando a ${this.partner()?.name ?? 'el otro jugador'}…`),
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
        alert.kicker ? h('div.alert-kicker', { className: `alert-kicker ${alert.tone === 'info' ? '' : alert.tone}` }, alert.kicker) : null,
        alert.title ? h('h2.x-title', {}, alert.title) : null,
        ...(alert.body ?? []).map((p) => h('p.x-text', {}, ...marked(p))),
        h(`p.${alert.body ? 'x-muted' : 'x-text'}`, {}, alert.text),
        ...(alert.brought ? this.broughtRow(alert.brought, 'Nuevo') : []),
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
          h(`button.x-btn${alert.action ? '' : '.primary'}`, { onclick: close }, 'Entendido'),
        ),
      );
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

/** A style written only when it changes: the lineup's cards are placed every frame, and each write laid the page out again for the next read. */
function restyle(el: HTMLElement, prop: string, value: string): void {
  if (el.style.getPropertyValue(prop) !== value) el.style.setProperty(prop, value);
}

/** A line with its figures picked out («2 ✚», «−10», «25 %»), for a tip's eye to land on them (#33). */
function figures(line: string): (string | Node)[] {
  return line.split(/([−+]?\d+(?:[.,]\d+)?(?:\s?(?:✚|§|%))?)/).map((part, i) => (i % 2 ? h('em.tip-num', {}, part) : part)).filter((part) => part !== '');
}

/** A text cut into its sentences, for a tip that reads a line each (#33). */
function sentences(text: string): string[] {
  return text.split(/(?<=[.!?])\s+(?=[A-ZÁÉÍÓÚÑ¡¿−+0-9«])/).filter(Boolean);
}
