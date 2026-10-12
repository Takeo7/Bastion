import './strategy.css';
import {
  ARMOR_COLORS,
  ARMOR_TIERS,
  ATTR_IDS,
  ATTR_POINTS_PER_RANK,
  ATTR_THRESHOLDS,
  ATTR_MAX,
  attrBlocker,
  attrCap,
  ATTRS,
  armorTier,
  baseArmorTier,
  baseWeaponTier,
  bastionLevel,
  BIOME_NAMES,
  BRANCHES,
  BUILD_IDS,
  buildable,
  BUILDS,
  canAfford,
  CLASS_TOP,
  CENTER,
  CENTER_NAME,
  AXES,
  axesOf,
  baseAttrs,
  echoesLevel,
  RECRUIT_DESCRIPTION,
  soldierTemplate,
  songRowLevel,
  wouldSpecialize,
  attrPointsFree,
  dormant,
  KIT_CLASS,
  KIT_NAMES,
  leaningClass,
  learnTalent,
  SPECIALIZE_RANK_NAME,
  talentPointsFree,
  contactCapacity,
  contactCost,
  costText,
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
  missionDays,
  NAME_MAX,
  nextSteps,
  nextTierBlocker,
  NICKNAME_MAX,
  offerBriefing,
  offerSignature,
  PARTITURA_LEVEL,
  QUESTS,
  pointsInAxis,
  pointsInBranch,
  pointsToSpend,
  randomAppearance,
  RANKS,
  retrainCost,
  ROW_GATES,
  RECRUIT_COST,
  REGION_LORE,
  regionIncome,
  REGIONS,
  Rng,
  shopPrice,
  plotName,
  soldierReady,
  soldierWeaponName,
  squadLimit,
  squadPads,
  squadPlace,
  recruitCost,
  squadMax,
  squadMember,
  STORY,
  TALENT_POINTS_PER_RANK,
  talentBlocker,
  talentIds,
  talentsOf,
  TALENTS,
  TECHS,
  TEMPLATES,
  TIER_RANK,
  TRAITS,
  VETERAN_AIM,
  VISOR_COLORS,
  WEAPON_TIER_DAMAGE,
  WEAPONS,
  weaponTier,
  type Appearance,
  type ArmorPart,
  ARMOR_PART_IDS,
  partColor,
  type BaseView,
  type CampaignCommand,
  type CampaignSoldier,
  type CampaignState,
  type ClassId,
  type ItemId,
  type ItemSlot,
  type MissionKind,
  type MissionOffer,
  type NextStep,
  type AttrId,
  type Build,
  type PerkId,
  type TalentDef,
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
import { graphicsSettings, soundSettings, themeSettings } from '../ui/settingsPanel';
import { BastionSet, LAUNCH_SECONDS, type FixedRoom, type LineupEntry } from './bastion';
import { GeoscapeSet } from './geoscape';
import { Stage, type Shot } from './stage';
import { sound } from '../audio/bank';
import { uiSound } from '../audio/ui';
import { plotIcon } from './plotIcons';

export interface StrategyHandlers {
  send(cmd: CampaignCommand): void;
  onView(view: BaseView): void;
  onLeave(): void;
}

interface Alert {
  tone: 'info' | 'good' | 'bad';
  kicker: string;
  title: string;
  /** Typed paragraphs before `text` (an Expediente page). */
  body?: string[];
  text: string;
  /** Handwritten in the margin. */
  note?: string;
  action?: { label: string; go: () => void };
  /** Higher first: a moment pops up only its most important alert. */
  rank?: number;
  /** Where it happens: the globe turns there when it pops up (rumours). */
  region?: RegionId;
}

/** The Novedades tray keeps this many alerts at most. */
const MAX_NEWS = 20;

const REPORT_KEY = storageKey('seenReport');
/** The rule of time, wherever the day is shown. */
const TIME_HELP = 'El tiempo solo pasa al ESCANEAR (hasta que ocurre algo) y al volar una misión (3–4 días). Comprar, reclutar, entrenar o proponer no gastan días; las obras, las curas y las escuchas avanzan mientras pasan.';

/** What each browser has already seen of every campaign's news (the tabs' numbers). */
const NEWS_KEY = storageKey('seenNews');

const KIND_ICON: Record<MissionKind, string> = { elimination: '✕', recovery: '◆', sabotage: 'ϟ', hack: '⌘', rescue: '✚' };
const DIFFICULTY = ['', 'Moderada', 'Difícil', 'Muy difícil', 'Extrema'];
const CLASSES: ClassId[] = ['assault', 'grenadier', 'sharpshooter', 'specialist'];

const NAV: { view: BaseView; label: string }[] = [
  { view: 'hub', label: 'Bastión' },
  { view: 'geoscape', label: 'Geoesfera' },
  { view: 'research', label: 'Expediente' },
  { view: 'engineering', label: 'Armería' },
  { view: 'barracks', label: 'Cuartel' },
  { view: 'facilities', label: 'Instalaciones' },
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
  hub: 'Bastión',
  geoscape: 'Geoesfera',
  research: 'Expediente',
  engineering: 'Armería',
  barracks: 'Cuartel',
  facilities: 'Instalaciones',
  squad: 'Hangar',
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
  ['Soldados', ['quarters', 'infirmary', 'training', 'range', 'memorial']],
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
  private readonly top = h('div.strat-top');
  private readonly proposalEl = h('div.strat-proposal.x-panel');
  private readonly viewEl = h('div.strat-view');
  private readonly nav = h('div.strat-nav');
  private readonly modal = h('div.strat-modal');
  private readonly toastEl = h('div.strat-toast');
  private state: CampaignState | null = null;
  private players: PlayerInfo[] = [];
  private view: BaseView = 'hub';
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
  private promoting = false;
  /** Soldier whose Reentrenar button waits for a second click. */
  private retrainArmed: string | null = null;
  /** Axis of the Rosa open in the Hoja de servicio (null = the soldier's main one). */
  private sheetAxis: ClassId | null = null;
  /** The Hoja de servicio shows the whole Rosa instead of what can be learnt (or was). */
  private wholeRosa = false;
  /** A promotion being laid out on the sheet; nothing is sent until «Guardar». */
  private plan: { soldier: string; attrs: AttrId[]; talents: PerkId[] } | null = null;
  /** The sheet just saved: it shows its stamp for a moment. */
  private savedStamp: { soldier: string; until: number } | null = null;
  /** Open equipment picker ("soldierId:slot"). */
  private picking: string | null = null;
  /** The alert on screen last time, so turning the globe to it happens once. */
  private shownAlert: Alert | null = null;
  /** "Volver al menú" asked: it takes both players, so it is confirmed first. */
  private leaving = false;
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
    this.stage.add('bastion', this.bastion);
    this.stage.add('geo', this.geo);
    this.root.append(this.stage.element, this.viewEl, this.top, this.proposalEl, this.nav, this.modal, this.radio.element, this.toastEl);
    container.append(this.root);
    void this.stage.show('bastion', this.bastion.overview(), true);
    handlers.onView(this.view);
    window.addEventListener('keydown', this.onKey);
  }

  private readonly onKey = (e: KeyboardEvent) => {
    if (e.key !== 'Escape' || e.defaultPrevented || !this.state || this.root.classList.contains('launching')) return;
    if ((e.target as HTMLElement | null)?.closest?.('input, textarea')) return;
    e.preventDefault();
    this.back();
  };

  /** Esc: closes what is open, lets go of what is picked, and otherwise goes back to the Bastión. */
  private back(): void {
    if (this.picking || this.custom || this.promoting || this.leaving || this.newsOpen || this.settingsOpen) {
      this.picking = null;
      this.custom = null;
      this.promoting = false;
      this.plan = null;
      this.wholeRosa = false;
      this.leaving = false;
      this.newsOpen = false;
      this.settingsOpen = false;
      this.render();
      return;
    }
    // Alerts and events are answered with their own buttons.
    if (this.modal.classList.contains('show')) return;
    if (this.view === 'facilities' && this.plot) this.pickPlot(null);
    else if (this.view !== 'hub' && this.view !== 'debrief') void this.go('hub');
  }

  // ------------------------------------------------------------ public

  /**
   * `before`: what the base looked like when the squad took off. A screen made
   * anew after a mission compares with it, so the mission's news (levels, new
   * operations, the clock) still pop up, after the debrief, for both commanders.
   */
  update(c: CampaignState, before?: CampaignState | null): void {
    const prev = this.state ?? before ?? null;
    this.state = c;
    this.loadSeenNews(c);
    // News arriving on the screen being looked at is seen at once.
    this.seeNews(this.view);
    // The partner's proposal waits on its note (with «Ver»): it never takes this player away from what they are doing.
    const proposed = !!c.proposal && c.proposal.by !== this.mySlot && prev?.proposal?.text !== c.proposal.text;
    if (proposed) this.blink('.strat-proposal');
    this.bastion.setBase(c);
    this.geo.flyTo(this.offerById(c.activeMission)?.region ?? HOME_REGION);
    if (prev) this.collectAlerts(prev, c);
    // A mission chosen by either player sends both to the hangar; a finished one to the debrief.
    if (!prev?.activeMission && c.activeMission) void this.go('squad');
    else if (prev?.activeMission && !c.activeMission && this.view === 'squad') void this.go('geoscape');
    if (!c.activeMission && !this.reportPending(c)) this.bastion.setLineup([]);
    if (this.reportPending(c) && this.view !== 'debrief') void this.go('debrief');
    else this.render();
  }

  setPlayers(players: PlayerInfo[]): void {
    this.players = players;
    this.render();
  }

  setPartnerView(view: BaseView): void {
    this.partnerView = view;
    this.render();
  }

  toast(text: string): void {
    this.toastEl.textContent = text;
    this.toastEl.classList.add('show');
    window.clearTimeout(this.toastTimer);
    this.toastTimer = window.setTimeout(() => this.toastEl.classList.remove('show'), 2600);
  }

  /** Take-off: the dropship leaves the hangar before the mission loads. */
  async launch(): Promise<void> {
    if (this.view !== 'squad') await this.go('squad');
    clear(this.modal);
    this.modal.classList.remove('show');
    this.root.classList.add('launching');
    this.root.append(h('div.launch-banner', {}, 'DESPEGUE'));
    void this.stage.show('bastion', this.bastion.takeoff());
    const gone = this.bastion.launch();
    window.setTimeout(() => this.stage.blackout(), (LAUNCH_SECONDS - 0.5) * 1000);
    await gone;
  }

  destroy(): void {
    window.removeEventListener('keydown', this.onKey);
    cancelAnimationFrame(this.anchorFrame);
    this.stage.dispose();
    this.root.remove();
  }

  // -------------------------------------------------------- navigation

  private async go(view: BaseView): Promise<void> {
    this.view = view;
    this.seeNews(view);
    this.promoting = false;
    this.picking = null;
    this.custom = null;
    this.handlers.onView(view);
    this.render();
    await this.stage.show(view === 'geoscape' ? 'geo' : 'bastion', this.shotFor(view));
  }

  private shotFor(view: BaseView): Shot {
    switch (view) {
      case 'geoscape':
        return this.geo.shot();
      case 'research':
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

  private playerName(slot: Slot): string {
    return this.players.find((p) => p.slot === slot)?.name ?? `Jugador ${slot + 1}`;
  }

  private playerColor(slot: Slot): string {
    return this.players.find((p) => p.slot === slot)?.color ?? '#888';
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
    return gearOf(weaponTier(c, s), armorTier(c, s));
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

  /** A soldier's class: its symbol and short code, the full name on hover. */
  private clsTag(cls: ClassId | null): HTMLElement {
    const t = soldierTemplate(cls);
    return h(`span.cls.cls-${t}`, { title: cls ? TEMPLATES[t].name : RECRUIT_DESCRIPTION }, h('i'), TEMPLATES[t].short);
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

  /** "Armas magnéticas · Sala de simulación": what a Bastion level unlocks. */
  private unlocksText(level: number): string {
    const list = levelUnlocks(level, FACILITIES);
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

  /** XCOM-style pop-ups for what happened while time passed. */
  private collectAlerts(prev: CampaignState, c: CampaignState): void {
    const fresh: Alert[] = [];
    // Each level adds a page to the Expediente. After a mission it waits until the debrief is closed.
    for (let level = bastionLevel(prev) + 1; level <= bastionLevel(c); level++) {
      const page = EXPEDIENTE[level - 1]!;
      fresh.push({
        tone: 'good',
        kicker: `EXPEDIENTE · HOJA ${level}`,
        rank: 90,
        title: page.title,
        // One line of the page here; the whole page waits in the Expediente (playtest: «mucho texto»).
        body: [page.summary],
        text: `Bastión nivel ${level}. ${this.unlocksText(level)}`,
        note: page.note,
        action: { label: 'Ver el expediente', go: () => void this.go('research') },
      });
    }
    // Story chapters and the final mission are announced whenever they open: after a level, a mission or an event.
    for (const o of c.offers.filter((x) => (x.story || x.final) && !prev.offers.some((p) => p.id === x.id))) {
      fresh.push({
        tone: o.final ? 'bad' : 'good',
        kicker: o.final ? 'DIAPASÓN LOCALIZADO' : `MISIÓN DE HISTORIA · HOJA ${o.story}`,
        rank: o.final ? 85 : 80,
        title: o.name,
        text: `${o.final ? 'Misión final' : MISSION_NAMES[o.kind]} en ${REGIONS[o.region].name}. ${offerBriefing(c, o)}`,
        note: offerSignature(c, o),
        action: { label: 'Ver en la geoesfera', go: () => this.focusOffer(o.id) },
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
        kicker: o.facility ? 'OBJETIVO: ÓRGANO' : 'NUEVA OPERACIÓN',
        rank: o.facility ? 65 : 40,
        title: o.name,
        text: `${MISSION_NAMES[o.kind]} en ${REGIONS[o.region].name}. ${offerBriefing(c, o)}`,
        action: { label: 'Ver en la geoesfera', go: () => this.focusOffer(o.id) },
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
        action: { label: 'Ver en la geoesfera', go: () => this.focusRegion(r.id) },
      });
    }
    if (c.nextSupplyDay !== prev.nextSupplyDay) {
      fresh.push({
        tone: 'good',
        kicker: 'FIN DE MES',
        rank: 10,
        title: 'Llega el convoy de Altomonte',
        text: `Ruth Halvorsen manda desde Altomonte ${c.credits - prev.credits} suministros. Cada zona contactada aumenta el envío.`,
      });
    }
    if (c.doom > prev.doom) {
      const level = bastionLevel(c);
      fresh.push({
        tone: 'bad',
        kicker: doomName(level).toUpperCase(),
        rank: 50,
        title: doomStatus(level, c.doom),
        text: `Si llega a Atalaya, la campaña está perdida. Quemar Nidos la hace retroceder.`,
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
            label: `${this.decide('escuchar')} · ${rumor.daysLeft} días`,
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
    this.bastion.setLabels(this.view === 'hub' || (this.view === 'facilities' && !this.plot));
    switch (this.view) {
      case 'hub':
        this.renderHub(c);
        break;
      case 'geoscape':
        this.renderGeoscape(c);
        break;
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
      h('div.strat-brand', { title: TIME_HELP }, h('b', {}, 'BASTIÓN'), h('span', {}, `DÍA ${c.day}`)),
      h(
        'div.strat-res',
        {},
        h('span.strat-credits', { title: 'Suministros: construir, comprar accesorios, reclutar y contactar zonas' }, h('i', {}, '§'), h('b', {}, String(c.credits))),
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
          const where = p.slot === this.mySlot ? 'tú' : !p.connected ? 'desconectado' : this.partnerView ? VIEW_NAMES[this.partnerView] : '';
          const ready = c.advanceReady[p.slot] || c.launchReady[p.slot];
          return h(
            `div.strat-player${p.connected ? '' : '.offline'}`,
            { style: { '--accent': p.color } },
            h('span.dot'),
            h('b', {}, p.name),
            h('small', {}, ready ? 'listo' : where),
          );
        }),
      ),
      this.news.length
        ? h('button.x-btn.strat-news', { onclick: () => ((this.newsOpen = !this.newsOpen), this.render()), title: 'Lo que ha pasado y no salió en una ventana' }, 'Novedades ', this.chip(String(this.news.length), 'warn'))
        : null,
      h('button.x-btn', { onclick: () => ((this.settingsOpen = true), this.render()), title: 'Gráficos, sonido y estilo, sin salir de la campaña' }, 'Ajustes'),
      h('button.x-btn', { onclick: () => ((this.leaving = true), this.render()), title: 'Volver al menú principal (la campaña queda guardada)' }, 'Menú'),
    );
  }

  /** What a proposal costs, takes and does, and where to see it: whoever decides should know as much as whoever proposed. */
  /**
   * What a proposal costs and brings, one kind per line in the same order
   * (Tiempo, Suministros, Qué hace, Extra), and how to see what it is about.
   */
  private proposalDetail(c: CampaignState, action: NonNullable<CampaignState['proposal']>['action']): { rows: [string, string][]; see: (() => void) | null } {
    switch (action.type) {
      case 'chooseMission': {
        const o = this.offerById(action.id);
        if (!o) return { rows: [], see: null };
        const reward = [o.reward.credits ? `${o.reward.credits} §` : '', o.reward.recruit ? 'un recluta' : ''].filter(Boolean).join(' y ');
        return {
          rows: [
            ['Tiempo', `${missionDays(c).join('–')} días fuera: el tiempo pasa`],
            ['Qué hace', `${MISSION_NAMES[o.kind]} en ${REGIONS[o.region].name}`],
            ['Extra', `Dificultad ${'★'.repeat(o.difficulty)}${reward ? ` · recompensa: ${reward}` : ''}`],
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
            ['Qué hace', `Despeja ${TERRAINS[plot.terrain as keyof typeof TERRAINS]?.name.toLowerCase() ?? 'el terreno'} en ${plotCode(plot)}`],
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
            ['Qué hace', `Contacto con ${REGIONS[action.region].name}`],
            ['Extra', `+${regionIncome(c, action.region)} § al mes`],
          ],
          see: () => this.focusRegion(action.region),
        };
      case 'investigate': {
        const rumor = c.rumors.find((r) => r.id === action.id);
        return rumor
          ? { rows: [['Tiempo', `${rumor.daysLeft} días escuchando; no hace pasar el tiempo`], ['Qué hace', `Investigar el rumor de ${REGIONS[rumor.region].name}`]], see: () => this.focusRegion(rumor.region) }
          : { rows: [], see: null };
      }
      case 'event': {
        const def = eventDef(c);
        return { rows: def ? [['Qué hace', def.options[action.option].effect]] : [], see: null };
      }
    }
  }

  private renderProposal(c: CampaignState): void {
    clear(this.proposalEl);
    const p = c.proposal;
    this.proposalEl.classList.toggle('show', !!p);
    if (!p) return;
    const { rows, see } = this.proposalDetail(c, p.action);
    const detail = rows.length ? h('dl.proposal-detail', {}, ...rows.flatMap(([label, text]) => [h('dt', {}, label), h('dd', {}, text)])) : null;
    const seeBtn = see ? h('button.x-btn.small', { onclick: see }, 'Ver') : null;
    if (p.by === this.mySlot) {
      append(
        this.proposalEl,
        h('div.proposal-text', {}, h('span', {}, `Has propuesto ${p.text}. Esperando a ${this.partner()?.name ?? 'tu compañero'}…`), detail),
        h('button.x-btn', { onclick: () => this.send({ type: 'withdraw' }) }, 'Retirar'),
      );
    } else {
      append(
        this.proposalEl,
        h('div.proposal-text', {}, h('span', {}, h('b', { style: { color: this.playerColor(p.by) } }, this.playerName(p.by)), ` propone ${p.text}.`), detail),
        seeBtn,
        h('button.x-btn.primary', { onclick: () => this.send({ type: 'vote', accept: true }) }, 'Aceptar'),
        h('button.x-btn.danger', { onclick: () => this.send({ type: 'vote', accept: false }) }, 'Rechazar'),
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
    const promotions = c.soldiers.filter((s) => s.owner === this.mySlot && this.hasPoints(s));
    if (promotions.length) pending.set('barracks', { n: `▲${promotions.length}`, urgent: false, what: promotions.map((s) => `${s.name}: ascenso por repartir`) });
    for (const item of items) {
      const here = partner?.connected && this.partnerView === item.view;
      const todo = pending.get(item.view);
      this.nav.append(
        h(
          `button${this.view === item.view ? '.on' : ''}${item.view === 'squad' ? '.urgent' : ''}`,
          { onclick: () => void this.go(item.view), title: todo ? todo.what.join(' · ') : '' },
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
      ['research', [{ key: `level:${bastionLevel(c)}`, label: `Nueva hoja del Expediente: nivel ${bastionLevel(c)}`, urgent: false }]],
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
    if (step.view !== this.view) void this.go(step.view);
  }

  /**
   * The Bastion at a glance: "Ahora", the one next thing to do, then what else
   * is pending (each line a shortcut), works under way and the Council's payment.
   */
  private renderHub(c: CampaignState): void {
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
    // Steps on the hub itself (a proposal, an event) are already on screen: they only inform.
    const here = (step: NextStep) => step.view === 'hub';
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
    const ticker = h('div.strat-ticker');
    ticker.append(h('div.x-panel', {}, nowEl, rows.length ? h('div.x-head', { title: 'Pulsa una sala del Bastión para entrar' }, 'Agenda') : null, h('div.x-list', {}, ...rows), ...this.calendar(c)));
    this.viewEl.append(ticker);
  }

  /**
   * Calendario: what will happen on which day, so «quedan 3 días» has a date
   * (playtest: in XCOM too, nobody knew what day it was).
   */
  private calendar(c: CampaignState): HTMLElement[] {
    const dates: { day: number; icon: string; text: string; go?: () => void }[] = [];
    for (const work of c.base.works) {
      const what = work.kind === 'clear' ? 'Terreno despejado' : work.kind === 'upgrade' ? `${FACILITIES[work.facility!].name}, nivel ${(plotById(c, work.plot)?.level ?? 1) + 1}` : `${FACILITIES[work.facility!].name} terminado`;
      dates.push({ day: c.day + work.daysLeft, icon: 'pick', text: what, go: () => this.pickPlot(work.plot, false) });
    }
    for (const s of c.soldiers.filter((x) => x.alive && x.woundedDays > 0 && x.owner === this.mySlot)) dates.push({ day: c.day + s.woundedDays, icon: 'hp', text: `${s.name} vuelve al servicio` });
    const rumor = c.rumors.find((r) => r.id === c.listening);
    if (rumor) dates.push({ day: c.day + rumor.daysLeft, icon: 'will', text: `Se sabrá qué pasa en ${REGIONS[rumor.region].name}` });
    for (const o of c.offers.filter((x) => x.expiresDay !== null)) dates.push({ day: o.expiresDay!, icon: 'flag', text: `Caduca «${o.name}»`, go: () => this.focusOffer(o.id) });
    dates.push({ day: c.nextSupplyDay, icon: 'clock', text: 'Convoy de Altomonte' });
    dates.push({ day: c.nextDoomDay, icon: 'clock', text: `Avanza ${doomSubject(bastionLevel(c))}` });
    dates.sort((a, b) => a.day - b.day);
    const when = (day: number) => (day <= c.day ? 'hoy' : day === c.day + 1 ? 'mañana' : `en ${day - c.day} d`);
    return [
      h('div.x-head', { title: TIME_HELP }, `Calendario · hoy, día ${c.day}`),
      h(
        'div.x-list.calendar',
        {},
        ...dates.slice(0, 7).map((d) =>
          h(
            `${d.go ? 'button' : 'div'}.x-row.agenda.calendar-row`,
            d.go ? { onclick: d.go } : {},
            h(`span.agenda-icon.${d.icon}`),
            h('b', {}, h('span.calendar-day', {}, `Día ${d.day}`), ` ${d.text}`),
            this.chip(when(d.day)),
          ),
        ),
      ),
      h('p.calendar-note', {}, 'Los días solo pasan al ESCANEAR y en las misiones. Obras, curas y escuchas avanzan con ellos.'),
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
      const days = o.expiresDay === null ? null : o.expiresDay - c.day;
      list.append(
        h(
          `button.x-row.mission${this.offer === o.id ? '.selected' : ''}`,
          { onclick: () => this.focusOffer(o.id) },
          h('span.mission-icon', { style: { '--icon': o.final ? '#ff5a8a' : o.facility ? '#ff4a5a' : o.story ? '#4a7dff' : o.quest ? '#2f9e6a' : '#ffb030' } }, h('span', {}, KIND_ICON[o.kind])),
          h(
            'div',
            { title: `${o.facility ? 'Nido' : o.story ? `Historia · ${MISSION_NAMES[o.kind]}` : o.quest ? `Encargo · ${MISSION_NAMES[o.kind]}` : MISSION_NAMES[o.kind]} · ${REGIONS[o.region].name}` },
            h('b', {}, o.name),
            h('small', {}, REGIONS[o.region].name),
          ),
          h(
            `span.x-side.days${days !== null && days <= 2 ? '.x-bad' : ''}`,
            { title: days === null ? 'No caduca' : `Caduca el día ${o.expiresDay} (${days <= 0 ? 'hoy' : `en ${days} ${days === 1 ? 'día' : 'días'}`})` },
            days === null ? '' : days <= 0 ? 'hoy' : `${days} d`,
          ),
        ),
      );
    }
    if (!offers.length) list.append(h('p.x-muted', {}, 'Ninguna a la vista: escanead.'));
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
                  r.contacted ? this.chip('Asaltable', 'bad') : this.chip('Sin contacto', '', 'Contactad con la región para poder asaltarlo'),
                ),
              ),
            ),
          ]
        : []),
      ...this.questRows(c),
      ...this.rumorRows(c),
      h('div.x-head', { title: 'Pulsa una región del globo para ver sus datos' }, 'Red de enclaves'),
      this.meter(network, contactCapacity(c), 'Regiones contactadas / máximo (los centros de comunicaciones dan más)'),
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

    // Scan: advancing time is the Bastion's dropship scanning for activity.
    const ready = c.advanceReady[this.mySlot];
    const partner = this.partner();
    const waiting = partner?.connected && c.players.length > 1 && !c.advanceReady[partner.slot];
    const partnerReady = partner?.connected && !ready && c.advanceReady[partner.slot];
    const blocked = c.activeMission ? 'Hay una misión elegida: id al hangar' : c.event ? 'Antes decidid qué hacer con el acontecimiento' : '';
    // The main action here is choosing an operation; scanning lets them expire. With a
    // briefing open, deploying is the only action on screen.
    if (offer && !ready) return;
    const expiring = c.offers.find((o) => o.expiresDay !== null && o.expiresDay - c.day <= 1);
    const secondary = c.offers.length > 0 && !ready && !partnerReady;
    this.viewEl.append(
      h(
        'div.strat-center-bottom',
        {},
        h('div.scan-day', { title: TIME_HELP }, `HOY: DÍA ${c.day}`),
        h(
          `button.x-btn.scan-btn${secondary ? '.secondary' : '.big'}${ready ? '.ready' : ''}${partnerReady ? '.waiting' : ''}`,
          {
            disabled: !!blocked,
            onclick: () => {
              if (!ready) this.geo.setScanning(true);
              this.send({ type: 'advance', ready: !ready });
            },
          },
          ready ? 'ESCANEANDO…' : 'ESCANEAR',
          h(
            'small',
            {},
            blocked ||
              (ready && waiting
                ? `Esperando a ${partner!.name}`
                : partnerReady
                  ? `${partner!.name} quiere escanear`
                  : expiring
                    ? `«${expiring.name}» caduca ${expiring.expiresDay! - c.day <= 0 ? 'hoy' : 'mañana'}`
                    : 'Hace pasar los días hasta que ocurra algo'),
          ),
        ),
      ),
    );
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
            h('span.x-side', {}, offer?.expiresDay != null ? `${Math.max(0, offer.expiresDay - c.day)} d` : '—'),
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
                ? h('small.x-good', {}, `Escuchando · faltan ${r.daysLeft} días`)
                : h(
                    'button.x-btn.small',
                    { disabled: !!blocked, title: blocked || `Hacen falta ${r.daysLeft} días de escaneo`, onclick: () => this.send({ type: 'investigate', id: r.id }) },
                    `${this.decide('escuchar')} · ${r.daysLeft} días`,
                  ),
            ),
            // The clock is always "caduca en", as for the operations.
            h('span.x-side.days', { title: `Caduca en ${Math.max(0, r.expiresDay - c.day)} días` }, listening ? '' : `${Math.max(0, r.expiresDay - c.day)} d`),
          );
        }),
      ),
    ];
  }

  private briefing(c: CampaignState, o: MissionOffer): (HTMLElement | null)[] {
    const r = o.reward;
    const rewards = [
      r.credits ? `${r.credits} suministros` : '',
      r.recruit ? `un recluta (${TEMPLATES[r.recruit].name})` : '',
      r.doom ? `${doomSubject(bastionLevel(c))} retrocede ${r.doom}` : '',
      o.story ? (STORY[o.story - 1]!.prize ?? '') : '',
    ];
    const quest = o.quest ? c.quests.find((q) => q.id === o.quest) : undefined;
    if (quest) rewards.push(`Al final del encargo: ${fill(QUESTS[quest.id].prize, quest.names)}`);
    const chosen = c.activeMission === o.id;
    const biome = o.map.kind === 'generated' ? BIOME_NAMES[o.map.biome] : 'Distrito Comercial';
    return [
      h(
        'div.alert-kicker',
        {
          className: `alert-kicker${o.facility || o.final ? ' bad' : o.story || o.quest ? ' good' : ''}`,
          title: quest ? 'Si se pierde o caduca, el encargo se pierde con sus consecuencias' : '',
        },
        o.final
          ? 'MISIÓN FINAL'
          : o.facility
            ? 'ÓRGANO'
            : o.story
              ? `MISIÓN DE HISTORIA · HOJA ${o.story}`
              : o.quest
                ? `ENCARGO · ${QUESTS[o.quest].title.toUpperCase()}`
                : 'OPERACIÓN',
      ),
      h('h2.x-title', {}, o.name),
      h('p.x-sub', {}, `${REGIONS[o.region].name} · ${biome}${quest ? ` · paso ${quest.step + 1} de ${QUESTS[quest.id].steps.length}` : ''}`),
      h('p.x-text', {}, offerBriefing(c, o)),
      h('p.x-note', {}, offerSignature(c, o)),
      h(
        'dl.briefing',
        {},
        h('dt', {}, 'Tipo'),
        h('dd', {}, MISSION_NAMES[o.kind]),
        h('dt', {}, 'Dificultad'),
        h('dd', {}, h('span.stars', {}, '★'.repeat(o.difficulty)), ` ${DIFFICULTY[o.difficulty]}`),
        h('dt', {}, 'Caduca'),
        h('dd', {}, o.expiresDay === null ? 'No caduca' : `${Math.max(0, o.expiresDay - c.day)} días`),
        h('dt', {}, 'Recompensa'),
        h('dd', {}, rewards.filter(Boolean).join(' · ') || 'La victoria'),
      ),
      chosen ? h('button.x-btn.primary', { onclick: () => void this.go('squad') }, 'Ir al hangar') : this.deployButton(c, o),
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
          ? 'escuchad los rumores de la región para encontrarlo'
          : 'contactad con la región para que os lo cuenten';
    return h('p.x-muted', {}, `Encargo de la región: ${done || c.quests.some((q) => q.id === quest) ? `«${def.title}», ` : ''}${state}.`);
  }

  private regionInfo(c: CampaignState, id: RegionId): (HTMLElement | null)[] {
    const def = REGIONS[id];
    const r = c.regions.find((x) => x.id === id)!;
    const contacted = c.regions.filter((x) => x.contacted).length;
    const capacity = contactCapacity(c);
    const cost = contactCost(c);
    const blockedContact = this.why(
      [contacted >= capacity, 'Construid un centro de comunicaciones para más contactos'],
      [!!c.proposal, 'Hay una propuesta pendiente'],
      [!canAfford(c, cost), this.shortfall(cost)],
    );
    return [
      h('div.alert-kicker', {}, r.contacted ? 'REGIÓN CONTACTADA' : 'REGIÓN SIN CONTACTO'),
      h('h2.x-title', {}, def.name),
      h('p.x-text', {}, REGION_LORE[id].situation),
      h('p.x-muted', { title: 'Suministros que manda con cada convoy mensual una vez contactada' }, `${REGION_LORE[id].ally} · +${regionIncome(c, id)} § al mes`),
      this.regionQuest(c, id),
      r.facility ? h('p.x-text.x-bad', {}, `Nido: hace avanzar ${doomSubject(bastionLevel(c))} cada 14 días.${r.contacted ? '' : ' Contactad para quemarlo.'}`) : null,
      r.contacted
        ? null
        : h(
            'button.x-btn.primary',
            { disabled: !!blockedContact, onclick: () => this.send({ type: 'contact', region: id }) },
            `${this.decide('contactar')} · ${costText(cost)}`,
          ),
      r.contacted ? null : this.whyEl(blockedContact),
      r.contacted ? null : this.meter(contacted, capacity, 'Regiones contactadas / máximo'),
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
      const unlocks = levelUnlocks(n, FACILITIES);
      return h(
        `button.x-row.level${this.levelPick === n ? '.selected' : ''}${reached ? '.reached' : '.dim'}`,
        { onclick: () => ((this.levelPick = n), this.render()), title: unlocks.join(' · ') || 'El Bastión entra en servicio' },
        h('span.level-num', {}, String(n)),
        h('b', {}, reached ? EXPEDIENTE[i]!.title : unlocks.length ? `${unlocks.length} ${unlocks.length === 1 ? 'novedad' : 'novedades'}` : 'Sin novedades'),
        h('span.x-side', {}, reached ? '✓' : `${xp} XP`),
      );
    });
    const xpHelp = 'Cada misión da experiencia: +2 por ir, +3 y su dificultad si se gana, y +1 por cada baja. La sala de simulación da un 25 % más.';
    this.viewEl.append(
      this.panel(
        'progress-left',
        'left',
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
    const facilities = Object.values(FACILITIES).filter((f) => f.level === n);
    const page = EXPEDIENTE[n - 1]!;
    const line = (kind: string, name: string, detail: string, tag: HTMLElement | null = null) =>
      h('div.x-row.unlock', { title: detail }, h(`span.agenda-icon.${kind}`), h('div', {}, h('b', {}, name), h('small', {}, detail), tag));
    this.viewEl.append(
      this.panel(
        'progress-right',
        'right',
        h('div.alert-kicker', { className: `alert-kicker${reached ? ' good' : ''}` }, reached ? `ALCANZADO · EXPEDIENTE, HOJA ${n}` : `FALTAN ${LEVEL_XP[n - 1]! - c.xp} XP`),
        h('h2.x-title', {}, reached ? page.title : `Nivel ${n}`),
        ...(reached ? [...page.paragraphs.map((p) => h('p.x-text.page', {}, p)), h('p.x-note', {}, page.note)] : []),
        techs.length || items.length || facilities.length ? h('div.x-group', {}, reached ? 'Desbloqueado' : 'Desbloquea') : null,
        ...techs.map((t) => line('xp', t.name, t.description)),
        ...items.map((i) => line('flag', i.name, i.description, this.chip(costText(i.cost), '', 'En la Armería'))),
        ...facilities.map((f) => line('pick', f.name, f.description, this.chip('Instalación'))),
        !techs.length && !items.length && !facilities.length ? h('p.x-muted', {}, 'Lo básico: armas convencionales, kevlar, granadas, humo y botiquines.') : null,
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
      const tiers: [string, string, number, boolean][] = [
        ['w1', 'Armas convencionales', 1, true],
        ['w2', TECHS.magWeapons.name, TECHS.magWeapons.level, baseWeaponTier(c) >= 2],
        ['w3', TECHS.plasmaWeapons.name, TECHS.plasmaWeapons.level, baseWeaponTier(c) >= 3],
        ['a1', ARMOR_TIERS[0]!.name, 1, true],
        ['a2', ARMOR_TIERS[1]!.name, TECHS.plateArmor.level, baseArmorTier(c) >= 2],
        ['a3', ARMOR_TIERS[2]!.name, TECHS.predatorArmor.level, baseArmorTier(c) >= 3],
      ];
      this.engPick ??= 'w2';
      for (const [key, name, lvl, unlocked] of tiers) {
        const tier = Number(key[1]);
        if (key === 'a1') rows.push(h('div.x-group', {}, 'Armadura'));
        if (key === 'w1') rows.push(h('div.x-group', {}, 'Armas'));
        rows.push(
          h(
            `button.x-row.tier-row${this.engPick === key ? '.selected' : ''}${unlocked ? '' : '.dim'}`,
            { onclick: () => ((this.engPick = key), this.render()) },
            this.tierMarks(tier),
            h('b', {}, name),
            unlocked
              ? h('span.x-side', {}, '✓')
              : this.chip(`Nv ${lvl}${tier > 1 ? ` · ${RANKS[TIER_RANK[tier - 1]]!.name}` : ''}`, '', `Nivel ${lvl} del Bastión; cada soldado desde ${RANKS[TIER_RANK[tier - 1]]!.name}`),
          ),
        );
      }
    } else {
      for (const item of Object.values(ITEMS)) {
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
              h(
                'small',
                {},
                item.slot === 'utility' ? 'Utilidad ' : 'Munición ',
                unlocked ? this.chip(costText(shopPrice(c, item.id))) : this.chip(`Nv ${item.level}`, '', `Se desbloquea con el nivel ${item.level} del Bastión`),
              ),
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
      this.viewEl.append(this.panel('eng-right', 'right', ...this.tierPanel(c, this.engPick)));
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
        h('div.alert-kicker', {}, item.slot === 'utility' ? 'ACCESORIO · UTILIDAD' : 'ACCESORIO · MUNICIÓN'),
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

  /** A weapon or armour tier: what it gives, who may carry it, and each class's version. */
  /** «daño 3–5» for the template's weapon, with the soldier's damage bonuses (tier, talents). */
  private weaponDamage(template: TemplateId, bonus: number): string {
    const w = WEAPONS[TEMPLATES[template].weapon];
    const lo = w.damage + bonus - w.spread;
    const hi = w.damage + bonus + w.spread + (w.plusOne ? 1 : 0);
    return lo === hi ? `daño ${lo}` : `daño ${lo}–${hi}`;
  }

  private tierPanel(c: CampaignState, key: string): (HTMLElement | null)[] {
    const weapon = key[0] === 'w';
    const tier = Number(key[1]) as 1 | 2 | 3;
    const tech = weapon ? [null, TECHS.magWeapons, TECHS.plasmaWeapons][tier - 1] : [null, TECHS.plateArmor, TECHS.predatorArmor][tier - 1];
    const unlocked = (weapon ? baseWeaponTier(c) : baseArmorTier(c)) >= tier;
    const mine = c.soldiers.filter((s) => s.owner === this.mySlot && s.alive);
    const carrying = mine.filter((s) => (weapon ? weaponTier(c, s) : armorTier(c, s)) === tier);
    const armour = ARMOR_TIERS[tier - 1]!;
    const effect = weapon
      ? tier === 1
        ? 'El arma de cada clase.'
        : `+${WEAPON_TIER_DAMAGE[tier - 1]} de daño con el arma principal.`
      : tier === 1
        ? 'La protección de serie.'
        : `+${armour.hp} de salud${armour.armor ? ` y +${armour.armor} de blindaje` : ''}.`;
    return [
      h('div.alert-kicker', { className: `alert-kicker${unlocked ? ' good' : ''}` }, `${weapon ? 'ARMAS' : 'ARMADURA'} · NIVEL ${tier}`),
      h('h2.x-title', {}, this.tierMarks(tier), ' ', weapon ? tech?.name ?? 'Armas convencionales' : armour.name),
      h('p.x-text', {}, effect),
      h(
        'p.x-chips',
        { title: 'No se compra: cada soldado la lleva en cuanto cumple los requisitos' },
        tier === 1 ? this.chip('Todos', 'good') : this.chip(`Desde ${RANKS[TIER_RANK[tier - 1]]!.name}`),
        tier === 1 || unlocked ? null : this.chip(`Nivel ${tech!.level} del Bastión`, 'warn'),
      ),
      weapon ? h('div.x-group', {}, 'Por clase') : null,
      weapon ? h('dl.x-pairs', {}, ...CLASSES.flatMap((cls) => [h('dt', {}, TEMPLATES[cls].short), h('dd', {}, soldierWeaponName(cls, tier))])) : null,
      h('div.x-group', {}, 'Tus soldados con este nivel'),
      carrying.length ? h('p.x-text', {}, carrying.map((s) => displayName(s.name, s.nickname)).join(' · ')) : h('p.x-muted', {}, '—'),
    ];
  }

  // ---------------------------------------------------------- barracks

  private renderBarracks(c: CampaignState): void {
    // With a Camposanto the fallen rest there, out of this list.
    const listed = (s: CampaignSoldier) => s.alive || !facilityCount(c, 'memorial');
    const mine = c.soldiers.filter((s) => s.owner === this.mySlot && listed(s)).sort((a, b) => Number(b.alive) - Number(a.alive) || b.rank - a.rank);
    const theirs = c.soldiers.filter((s) => s.owner !== this.mySlot && listed(s)).sort((a, b) => Number(b.alive) - Number(a.alive) || b.rank - a.rank);
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
        this.clsTag(s.cls),
        h('b', { title: RANKS[s.rank]!.name }, displayName(s.name, s.nickname)),
        h('span.rank', { title: RANKS[s.rank]!.name }, ...Array.from({ length: s.rank }, () => h('i'))),
        this.hasPoints(s)
          ? this.chip('▲', 'good', 'Ascenso: puntos de aptitud y de talento por repartir')
          : h(`span.x-side${!s.alive ? '.x-bad' : s.woundedDays ? '.x-warn' : ''}`, {}, !s.alive ? 'Caído' : s.woundedDays ? `${s.woundedDays} d` : 'Listo'),
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
      h('p.x-text', {}, `Un recluta nuevo, sin clase: lleva fusil y aprende lo que le enseñéis. Llega al momento, sin gastar días.`),
      h(
        'p.x-chips',
        {},
        this.chip(costText(price)),
        this.chip(`Tus soldados ${count}/${cap}`),
        this.chip(`Camas libres ${Math.max(0, bedsFree(c))}`, bedsFree(c) <= 0 ? 'warn' : '', 'Cada soldado necesita cama: el búnker tiene 8 y cada barracón suma más'),
      ),
      h('button.x-btn.primary.recruit-btn', { disabled: !!blockedRecruit, title: RECRUIT_DESCRIPTION, onclick: () => this.send({ type: 'recruit' }) }, `Reclutar · ${costText(price)}`),
      this.whyEl(blockedRecruit),
    );
    this.viewEl.append(
      this.panel(
        'barracks-left',
        'left',
        h('div.x-head', {}, 'Tus soldados'),
        h('div.x-list', {}, ...mine.map(row)),
        recruit,
        theirs.length ? h('div.x-head', {}, `De ${this.playerName(theirs[0]!.owner)}`) : null,
        h('div.x-list', {}, ...theirs.map(row)),
      ),
    );
    const s = c.soldiers.find((x) => x.id === this.soldier);
    const draft = this.custom && this.custom.soldier === s?.id ? this.custom : null;
    this.bastion.setShowcase(s?.alive ? soldierTemplate(s.cls) : null, s ? this.playerColor(s.owner) : '#fff', draft?.appearance ?? s?.appearance, s ? this.gearFor(c, s) : undefined);
    if (!s) return;
    if (draft) {
      this.viewEl.append(this.panel('barracks-right', 'right', ...this.customizer(s, draft)));
      return;
    }
    const isMine = s.owner === this.mySlot;
    const stats = squadMember(c, s);
    const next = RANKS[s.rank + 1];
    // Equipment is a form field that opens its list of choices.
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
      const choices = Object.values(ITEMS)
        .filter((i) => i.slot === slot)
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
        h('div.alert-kicker', {}, `${TEMPLATES[soldierTemplate(s.cls)].name.toUpperCase()} · ${isMine ? 'TUYO' : `DE ${this.playerName(s.owner).toUpperCase()}`}`),
        h('h2.x-title', { title: `Misiones: ${s.missions} · Bajas: ${s.kills}` }, displayName(s.name, s.nickname)),
        h(
          'p.x-sub',
          { title: s.cls ? '' : RECRUIT_DESCRIPTION },
          `${RANKS[s.rank]!.name} · ${soldierWeaponName(soldierTemplate(s.cls), weaponTier(c, s))}`,
          !s.cls && AXES.some((a) => pointsInAxis(s.talents, a) > 0) ? ` · en ${SPECIALIZE_RANK_NAME}, ${TEMPLATES[leaningClass(s)].name}` : '',
        ),
        isMine && s.alive
          ? h(
              'button.x-btn.small',
              { onclick: () => ((this.custom = { soldier: s.id, name: s.name, nickname: s.nickname ?? '', appearance: { ...(s.appearance ?? defaultAppearance(s.cls, s.owner)) } }), this.render()) },
              'Personalizar',
            )
          : null,
        next ? this.bar(s.xp, next.xp) : null,
        h('p.x-muted', {}, next ? `${s.xp}/${next.xp} XP → ${next.name}` : 'Rango máximo'),
        h(
          'div.x-statline',
          {},
          this.stat('hp', String(stats.maxHp), 'Salud'),
          this.stat('aim', String(TEMPLATES[soldierTemplate(s.cls)].aim + stats.mods.aim), `Puntería: 50 + 3 por punto de Pulso + ${VETERAN_AIM} por ascenso`),
          this.stat('will', String(stats.will), 'Voluntad'),
          h(`b.x-state${!s.alive ? '.x-bad' : s.woundedDays ? '.x-warn' : '.x-good'}`, {}, !s.alive ? 'Caído' : s.woundedDays ? `Herido · ${s.woundedDays} d` : 'Listo'),
        ),
        s.traits?.length ? h('div.x-group', {}, 'Rasgos') : null,
        s.traits?.length ? h('p.x-chips', {}, ...s.traits.map((t) => this.chip(TRAITS[t].name, '', TRAITS[t].description))) : null,
        // What they carry, and what is chosen here and what isn't (playtest: «no se ve qué armas llevan ni dónde se elige»).
        h('div.x-group', { title: 'No se eligen: van con la clase y mejoran solas con el nivel del Bastión y el rango' }, 'Arma y armadura'),
        ...(['weapon', 'armor'] as const).map((kind) => {
          const tier = kind === 'weapon' ? weaponTier(c, s) : armorTier(c, s);
          const name = kind === 'weapon' ? soldierWeaponName(soldierTemplate(s.cls), tier) : ARMOR_TIERS[tier - 1]!.name;
          const blocker = s.alive ? nextTierBlocker(c, s, kind) : null;
          const tech = tier < 3 ? (kind === 'weapon' ? [TECHS.magWeapons, TECHS.plasmaWeapons] : [TECHS.plateArmor, TECHS.predatorArmor])[tier - 1]! : null;
          const short = tech ? `→ Nv ${tech.level} · ${RANKS[TIER_RANK[tier as 1 | 2]]!.name}` : '';
          const stat = kind === 'weapon' ? this.weaponDamage(soldierTemplate(s.cls), stats.mods.damage) : `${stats.maxHp} de salud${stats.armor ? ` · ${stats.armor} de blindaje` : ''}`;
          return h('div.x-gear', {}, this.tierMarks(tier), h('span', {}, name, h('small', {}, ` · ${stat}`)), blocker && short ? this.chip(short, '', blocker) : null);
        }),
        h('p.x-muted.gear-note', {}, s.cls ? `Van con la clase de ${TEMPLATES[s.cls].name}; mejoran con el nivel del Bastión y el rango.` : `Un recluta lleva fusil; el arma de su clase llega en ${SPECIALIZE_RANK_NAME}.`),
        h('div.x-group', { title: 'Se compran en la Armería; si el soldado cae, se pierden' }, 'Accesorios · se eligen aquí'),
        loadout('utility', 'Utilidad'),
        loadout('ammo', 'Munición'),
        h('div.x-group', {}, 'Aptitudes'),
        h('p.x-chips', {}, ...ATTR_IDS.map((id) => this.chip(`${ATTRS[id].short} ${s.attrs[id]}`, '', `${ATTRS[id].name}: ${ATTRS[id].effect}`))),
        h('div.x-group', {}, 'Talentos'),
        talentIds(s.talents).length
          ? h(
              'p.x-chips',
              {},
              ...talentIds(s.talents).map((id) => {
                const d = TALENTS[id];
                return this.chip(d.max > 1 ? `${d.name} ${s.talents[id]}/${d.max}` : d.name, '', d.description);
              }),
            )
          : h('p.x-muted', {}, `Cada ascenso da ${TALENT_POINTS_PER_RANK} puntos de talento, desde ${RANKS[1]!.name}.`),
        h(
          `button.x-btn${this.hasPoints(s) && isMine ? '.primary.waiting' : ''}`,
          { onclick: () => ((this.promoting = true), (this.retrainArmed = null), this.render()) },
          this.hasPoints(s) && isMine ? '¡Ascenso! Repartir puntos' : 'Hoja de servicio',
        ),
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
        if (this.custom === d) this.bastion.setShowcase(soldierTemplate(s.cls), this.playerColor(s.owner), d.appearance, this.gearFor(this.c(), s));
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
      h('div.alert-kicker', {}, `PERSONALIZAR · ${TEMPLATES[soldierTemplate(s.cls)].name.toUpperCase()}`),
      h('h2.x-title', {}, displayName(d.name.trim() || s.name, d.nickname.trim())),
      text('name', 'Nombre', NAME_MAX, s.name),
      text('nickname', 'Apodo', NICKNAME_MAX, 'Sin apodo'),
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
          { title: 'Vuelve a los colores de tu jugador y al aspecto de su clase', onclick: () => ((d.appearance = defaultAppearance(s.cls, s.owner)), this.render()) },
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
  private hasPoints(s: CampaignSoldier): boolean {
    const free = pointsToSpend(s);
    return s.alive && free.attrs + free.talents > 0;
  }

  /**
   * The promotion being prepared: points laid on the sheet, sent only with
   * «Guardar», step by step as the server checks them, so nothing is lost and
   * nothing is spent by a stray click.
   */
  private planned(s: CampaignSoldier, echoes: number): { b: Build; attrs: AttrId[]; talents: PerkId[] } {
    const plan = this.plan?.soldier === s.id ? this.plan : null;
    const b: Build = { cls: s.cls, rank: s.rank, attrs: { ...s.attrs }, talents: { ...s.talents } };
    const attrs: AttrId[] = [];
    const talents: PerkId[] = [];
    // Checked again in order: taking one back can leave a later one without what it asked for.
    for (const id of plan?.attrs ?? []) {
      if (attrBlocker(b, id)) continue;
      b.attrs[id]++;
      attrs.push(id);
    }
    for (const id of plan?.talents ?? []) {
      if (talentBlocker(b, id, echoes)) continue;
      learnTalent(b, id);
      talents.push(id);
    }
    return { b, attrs, talents };
  }

  private savePlan(s: CampaignSoldier, attrs: AttrId[], talents: PerkId[]): void {
    for (const attr of attrs) this.send({ type: 'train', soldier: s.id, attr });
    for (const talent of talents) this.send({ type: 'talent', soldier: s.id, talent });
    this.plan = null;
    this.savedStamp = { soldier: s.id, until: performance.now() + 2600 };
    uiSound('stamp');
    const what = [...new Set(attrs)].map((id) => `${ATTRS[id].name} +${attrs.filter((x) => x === id).length}`).concat([...new Set(talents)].map((id) => TALENTS[id].name));
    this.toast(`Guardado en la hoja de ${s.name}: ${what.join(', ')}.`);
    window.setTimeout(() => this.render(), 2700);
  }

  /**
   * Hoja de servicio. With points to spend it is the promotion: aptitudes with
   * + and −, and only the talents that can be learnt now, laid out as a draft
   * and saved at once. Otherwise it reads as the soldier's file, and the whole
   * Rosa is one click away.
   */
  private promotionPanel(c: CampaignState, s: CampaignSoldier): HTMLElement {
    const mine = s.owner === this.mySlot && s.alive;
    const echoes = echoesLevel(c);
    const { b, attrs: planAttrs, talents: planTalents } = this.planned(s, echoes);
    const free = { attrs: attrPointsFree(b), talents: talentPointsFree(b) };
    const drafted = planAttrs.length + planTalents.length;
    const promoting = mine && (free.attrs + free.talents > 0 || drafted > 0);
    const setPlan = (attrs: AttrId[], talents: PerkId[]) => {
      this.plan = { soldier: s.id, attrs, talents };
      this.render();
    };
    const without = <T,>(list: T[], item: T) => {
      const i = list.lastIndexOf(item);
      return list.filter((_, j) => j !== i);
    };

    // Aptitudes: the value, ten boxes (inked = base, pen = spent, pencil = in this draft) and what the next mark brings.
    const base = baseAttrs(b.cls);
    const apts = ATTR_IDS.map((id: AttrId) => {
      const def = ATTRS[id];
      const saved = s.attrs[id];
      const value = b.attrs[id];
      const cap = attrCap(b.cls, id);
      const added = value - saved;
      const blocked = attrBlocker(b, id);
      const next = ATTR_THRESHOLDS.findIndex((n) => value < n);
      const boxes = Array.from({ length: 10 }, (_, i) =>
        h(`i${i < base[id] ? '.base' : i < saved ? '.add' : i < value ? '.plan' : i < cap ? '' : '.cap'}${i === 6 || i === 9 ? '.mark' : ''}`),
      );
      return h(
        `div.apt${added ? '.drafted' : ''}`,
        { title: ATTR_THRESHOLDS.map((n, i) => `${n}: ${def.thresholds[i]}`).join('\n') },
        h('div.apt-head', {}, h('b', {}, def.name), h('small.apt-effect', {}, def.effect), h('span.apt-val', {}, String(value)), added ? h('small.apt-added', {}, `+${added}`) : null),
        h(
          'div.apt-row',
          {},
          h('span.apt-boxes', { title: b.cls ? `Tope ${cap}` : `Tope ${cap}: al especializarse, las dos de su clase llegan a 10` }, ...boxes),
          promoting ? h('button.apt-minus', { disabled: !added, title: 'Quitar un punto', onclick: () => setPlan(without(planAttrs, id), planTalents) }, '−') : null,
          promoting ? h('button.apt-plus', { disabled: !!blocked, title: blocked ?? `Subir ${def.name}`, onclick: () => setPlan([...planAttrs, id], planTalents) }, '+') : null,
        ),
        // The cap in words, where it stops short of the next mark (playtest: «no puedo subirle la puntería… ¿es un bug?»).
        value >= cap && cap < ATTR_MAX
          ? h(
              'small.apt-next.capped',
              {},
              b.cls
                ? `Tope ${cap}: no es aptitud de ${TEMPLATES[b.cls].name}. Las suyas, ${CLASS_TOP[b.cls].map((x) => ATTRS[x].name).join(' y ')}, llegan a ${ATTR_MAX}.`
                : `Tope ${cap} de un recluta: al especializarse, las dos aptitudes de su clase llegan a ${ATTR_MAX}.`,
            )
          : next >= 0
            ? h('small.apt-next', {}, `A ${ATTR_THRESHOLDS[next]}: ${def.thresholds[next]}`)
            : h('small.apt-next.got', {}, 'Todo lo de esta aptitud'),
      );
    });

    // A talent card; in a draft a click adds a level and its ✕ takes one back.
    const card = (d: TalentDef, where = false) => {
      const level = b.talents[d.id] ?? 0;
      const fresh = level - (s.talents[d.id] ?? 0);
      const blocked = talentBlocker(b, d.id, echoes);
      const open = promoting && !blocked;
      const sleeping = level > 0 && dormant(b, d.id);
      const becomes = wouldSpecialize(b, d.id);
      const req = d.req ? h(`span.talent-req${b.attrs[d.req[0]] < d.req[1] ? '.miss' : ''}`, { title: `Pide ${ATTRS[d.req[0]].name} ${d.req[1]}` }, `${ATTRS[d.req[0]].short} ${d.req[1]}`) : null;
      return h(
        `button.talent${level ? '.taken' : ''}${fresh ? '.drafted' : ''}${open ? '.open' : ''}${!level && blocked && !/No quedan puntos/.test(blocked) ? '.shut' : ''}${d.coop ? '.coop' : ''}${d.song ? '.song' : ''}`,
        {
          disabled: !open,
          title: blocked && level < d.max && mine ? blocked : d.coop ? 'Llave doble: solo funciona junto a soldados de tu compañero.' : '',
          onclick: () => setPlan(planAttrs, [...planTalents, d.id]),
        },
        where && d.axis ? h('small.talent-where', {}, `${BRANCHES[d.axis][d.branch].name} · fila ${d.row + 1}`) : null,
        h('b', {}, d.name, d.ability ? ' ◆' : '', d.coop ? ' ⚷⚷' : ''),
        h('span.talent-levels', {}, ...Array.from({ length: d.max }, (_, i) => h(`i${i < level - fresh ? '.on' : i < level ? '.plan' : ''}`)), h('small', {}, `${level}/${d.max}`), req),
        h('small.talent-desc', {}, d.description),
        d.kit && b.cls !== KIT_CLASS[d.kit] ? h('small.talent-becomes', {}, `${sleeping ? 'Dormido' : 'Duerme'} hasta llevar ${KIT_NAMES[d.kit]}`) : null,
        becomes && open ? h('small.talent-becomes', {}, `Te especializa como ${TEMPLATES[becomes].name}`) : null,
        fresh
          ? h(
              'span.talent-undo',
              {
                title: 'Quitar',
                onclick: (e: Event) => {
                  e.stopPropagation();
                  setPlan(planAttrs, without(planTalents, d.id));
                },
              },
              '✕',
            )
          : null,
      );
    };

    // Where the soldier is heading: the class, or the axis a recruit leans to.
    const leaning = b.cls ? null : leaningClass(b);
    const { second } = axesOf(b);
    const path = b.cls
      ? `${TEMPLATES[b.cls].name}${second ? ` · segundo eje: ${TEMPLATES[second].name}` : ''}`
      : `Recluta · en ${SPECIALIZE_RANK_NAME} se especializa en el eje en que más haya aprendido${AXES.some((a) => pointsInAxis(b.talents, a) > 0) ? `: ahora, ${TEMPLATES[leaning!].name}` : ''}`;

    // Talents: in a draft, only what can be learnt now (and what this draft took): the centre, then one axis at a time.
    const shown = (d: TalentDef) => (promoting ? !talentBlocker(b, d.id, echoes) || (b.talents[d.id] ?? 0) > (s.talents[d.id] ?? 0) : (b.talents[d.id] ?? 0) > 0);
    const order = [b.cls ?? leaning!, ...AXES.filter((a) => a !== (b.cls ?? leaning))];
    const counts = new Map(order.map((a) => [a, talentsOf(a).filter(shown).length]));
    const started = AXES.some((a) => pointsInAxis(b.talents, a) > 0);
    const axis = this.sheetAxis ?? (b.cls ?? (started ? leaning : null) ?? order.find((a) => counts.get(a)) ?? order[0]!);
    const centre = CENTER.filter(shown);
    const axisTabs = h(
      'div.rosa-axes',
      {},
      ...order.map((a) =>
        h(
          `button.x-btn.small${a === axis ? '.on' : ''}`,
          { onclick: () => ((this.sheetAxis = a), this.render()), title: a === b.cls ? 'Su clase' : a === leaning && started ? 'Hacia donde se inclina' : '' },
          this.clsTag(a),
          ` ${TEMPLATES[a].name}`,
          h('small', {}, ` · ${counts.get(a)}`),
        ),
      ),
    );
    const axisCards = talentsOf(axis).filter(shown);
    const picker = h(
      'div.talent-picker',
      {},
      centre.length ? h('div.x-group', {}, CENTER_NAME) : null,
      centre.length ? h('div.talent-pick', {}, ...centre.map((d) => card(d))) : null,
      axisTabs,
      axisCards.length
        ? h('div.talent-pick', {}, ...axisCards.map((d) => card(d, true)))
        : h('p.x-muted', {}, promoting ? 'Nada que aprender en este eje ahora: hacen falta más puntos en una rama o una aptitud más alta.' : 'Nada aprendido en este eje.'),
    );
    const rosa = this.wholeRosa ? this.rosaGrid(b, axis, card, echoes) : null;
    const talentsPart = promoting
      ? h(
          'div.service-tree',
          {},
          h('div.x-group', {}, '2 · Talentos', this.chip(free.talents ? `Quedan ${free.talents}` : 'Repartidos', free.talents ? 'good' : '')),
          this.wholeRosa ? rosa : picker,
        )
      : h(
          'div.service-tree',
          {},
          h('div.x-group', {}, 'Talentos'),
          this.wholeRosa ? rosa : talentIds(b.talents).length ? picker : h('p.x-muted', {}, `Cada ascenso da ${TALENT_POINTS_PER_RANK} puntos de talento, desde ${RANKS[1]!.name}.`),
        );

    const cost = retrainCost(c);
    const spent = attrPointsFree(s) < ATTR_POINTS_PER_RANK * s.rank || talentPointsFree(s) < TALENT_POINTS_PER_RANK * s.rank;
    const armed = this.retrainArmed === s.id;
    const retrain =
      mine && spent && !drafted
        ? h(
            `button.x-btn.small${armed ? '.danger' : ''}`,
            {
              disabled: !canAfford(c, cost),
              title: canAfford(c, cost) ? 'Devuelve todos los puntos de aptitud y de talento, y deja al soldado sin clase' : this.shortfall(cost),
              onclick: () => {
                if (!armed) {
                  this.retrainArmed = s.id;
                  this.render();
                  return;
                }
                this.retrainArmed = null;
                this.send({ type: 'retrain', soldier: s.id });
              },
            },
            armed ? `Confirmar: devolver todo por ${costText(cost)}` : `Reentrenar · ${costText(cost)}`,
          )
        : null;
    const close = () => {
      this.promoting = false;
      this.retrainArmed = null;
      this.sheetAxis = null;
      this.wholeRosa = false;
      this.plan = null;
      this.render();
    };
    const stamped = this.savedStamp?.soldier === s.id && this.savedStamp.until > performance.now();
    return h(
      `div.x-panel.wide.service${promoting ? '.promoting' : ''}`,
      {},
      stamped ? h('div.saved-stamp', {}, 'GUARDADO') : null,
      h('div.alert-kicker', {}, promoting ? `ASCENSO · ${RANKS[s.rank]!.name.toUpperCase()}` : `HOJA DE SERVICIO · ${RANKS[s.rank]!.name.toUpperCase()}`),
      h('h2.x-title', {}, displayName(s.name, s.nickname)),
      h('p.x-sub', {}, path),
      promoting
        ? h(
            'p.service-help',
            {},
            free.attrs + free.talents
              ? `Reparte ${free.attrs ? `${free.attrs} de aptitud` : ''}${free.attrs && free.talents ? ' y ' : ''}${free.talents ? `${free.talents} de talento` : ''}. Nada cuenta hasta que pulses «Guardar»; lo que no gastes queda para luego.`
              : 'Todo repartido. Pulsa «Guardar» para apuntarlo en la hoja.',
          )
        : null,
      h(
        'div.service-sheet',
        {},
        h('div.service-apts', {}, h('div.x-group', {}, promoting ? '1 · Aptitudes' : 'Aptitudes', promoting ? this.chip(free.attrs ? `Quedan ${free.attrs}` : 'Repartidas', free.attrs ? 'good' : '') : null), ...apts, retrain),
        talentsPart,
      ),
      h(
        'div.alert-actions',
        {},
        promoting ? h('button.x-btn.primary', { disabled: !drafted, onclick: () => this.savePlan(s, planAttrs, planTalents) }, drafted ? `Guardar (${drafted})` : 'Guardar') : null,
        promoting && drafted ? h('button.x-btn', { onclick: () => setPlan([], []) }, 'Deshacer todo') : null,
        h('button.x-btn', { onclick: () => ((this.wholeRosa = !this.wholeRosa), this.render()) }, this.wholeRosa ? (promoting ? 'Solo lo que puedo aprender' : 'Solo lo aprendido') : 'Ver la Rosa entera'),
        h('button.x-btn', { onclick: close }, drafted ? 'Cerrar sin guardar' : 'Cerrar'),
      ),
    );
  }

  /** The whole Rosa of one axis, row by row with its gates: for those who want to plan far ahead. */
  private rosaGrid(b: Build, axis: ClassId, card: (d: TalentDef) => HTMLElement, echoes: number): HTMLElement {
    const { second } = axesOf(b);
    const tabs = h(
      'div.rosa-axes',
      {},
      ...AXES.map((a) => {
        const role = a === b.cls ? 'principal' : a === second ? 'segundo eje' : '';
        const points = pointsInAxis(b.talents, a);
        return h(
          `button.x-btn.small${a === axis ? '.on' : ''}`,
          { onclick: () => ((this.sheetAxis = a), this.render()) },
          this.clsTag(a),
          ` ${TEMPLATES[a].name}`,
          points ? h('small', {}, ` · ${points}`) : null,
          role ? h('small', {}, ` · ${role}`) : null,
        );
      }),
    );
    const grid = h('div.talent-grid.four');
    grid.append(h('div'));
    BRANCHES[axis].forEach((br, i) =>
      grid.append(
        h(
          `div.talent-branch${br.song ? '.song' : ''}`,
          {},
          h('b', {}, br.name),
          h('small', {}, br.song ? (echoes ? `Taller de oficios · nivel ${echoes}` : 'Hace falta el Taller de oficios') : br.focus),
          h('span', {}, `${pointsInBranch(b.talents, axis, i)} puntos`),
        ),
      ),
    );
    ROW_GATES.forEach((gate, row) => {
      grid.append(h('div.talent-gate', { title: `Esta fila pide ${gate} puntos en la rama, en las filas de arriba (en el Oficio, uno por fila)` }, h('b', {}, String(gate)), h('small', {}, 'puntos')));
      for (const branch of [0, 1, 2, 3]) {
        const cards = talentsOf(axis).filter((d) => d.branch === branch && d.row === row);
        const songLocked = branch === 3 && songRowLevel(row) > echoes;
        grid.append(h(`div.talent-cell${songLocked ? '.locked' : ''}`, { title: songLocked ? `Taller de oficios · nivel ${songRowLevel(row)}` : '' }, ...cards.map((d) => card(d))));
      }
    });
    return h('div.rosa-whole', {}, h('div.x-group', {}, CENTER_NAME), h('div.talent-center', {}, ...CENTER.map((d) => card(d))), tabs, grid);
  }

  // -------------------------------------------------------- facilities

  private renderFacilities(c: CampaignState): void {
    const lines: [string, string][] = [];
    const alive = c.soldiers.filter((s) => s.alive).length;
    lines.push(['flag', `Camas: ${alive}/${beds(c)}`]);
    lines.push(['pick', `Cuadrillas: ${c.base.works.length}/${workCrews(c)} en obra`]);
    if (facilityCount(c, 'simulation')) lines.push(['xp', `XP de misión +${Math.round((xpMultiplier(c) - 1) * 100)} %`]);
    if (facilityCount(c, 'workshop')) lines.push(['flag', `Accesorios −${Math.round((1 - shopPrice(c, 'smokeGrenade') / ITEMS.smokeGrenade.cost) * 100)} %`]);
    if (facilityCount(c, 'infirmary')) lines.push(['hp', `Heridas ×${healRate(c)} de rápido`]);
    if (facilityCount(c, 'training')) lines.push(['flag', `Escuadra de ${squadMax(c)}`]);
    if (rangeAim(c)) lines.push(['xp', `Puntería +${rangeAim(c)}`]);
    if (garageCut(c).days) lines.push(['flag', `Misiones −${garageCut(c).days} d${garageCut(c).wounds ? ` · heridas −${garageCut(c).wounds} d` : ''}`]);
    if (listeningDelay(c)) lines.push(['will', `Reloj enemigo +${listeningDelay(c)} d`]);
    if (facilityLevel(c, 'crew')) lines.push(['flag', `Obras −${[25, 40, 50][facilityLevel(c, 'crew') - 1]} %`]);
    lines.push(['will', `Contactos: ${contactCapacity(c)} regiones`]);
    const bonuses = adjacencyBonuses(c);
    const works = c.base.works;
    const workOn = (id: string) => works.find((w) => w.plot === id);
    // Every crew busy: nothing new starts until one finishes.
    const crewsFull = works.length >= workCrews(c);
    const crewsText = `Todas las cuadrillas están ocupadas (${workCrews(c)})`;
    // Every plot that can be acted on, with its state and its action: the same as clicking it in 3D.
    // The compound as it is seen from above: a 7 × 7 grid, each plot with the mark of what stands on it.
    const cells = c.base.plots.map((p) => {
      const work = workOn(p.id);
      const fixed = !!p.building && p.building in FIXED_NAMES_SET;
      const level = p.building && p.building in FACILITIES ? (p.level ?? 1) : 0;
      const kind = work ? 'work' : (p.building ?? (p.terrain === 'clear' ? null : p.terrain));
      const state = work ? '.work' : fixed ? '.fixed' : p.building ? '.built' : p.terrain === 'clear' ? (neighbours(c, p).some((q) => q.building) ? '.free' : '.free.far') : `.terrain${clearable(c, p) ? '.far' : ''}`;
      return h(
        `button.plot-cell${state}${this.plot === p.id ? '.selected' : ''}${this.plotHovered === p.id ? '.hovered' : ''}${!work && p.building && !upgradable(c, p) ? '.can-up' : ''}`,
        {
          title: `${plotCode(p)} · ${this.plotLine(c, p.id)}`,
          'aria-label': `${plotCode(p)}: ${this.plotLine(c, p.id)}`,
          'data-plot': p.id,
          onclick: () => this.pickPlot(p.id),
          onmouseenter: () => this.bastion.hover(`plot:${p.id}`),
          onmouseleave: () => this.bastion.hover(null),
        },
        kind ? plotIcon(kind) : null,
        level > 1 ? h('small.plot-level', {}, String(level)) : null,
        work ? h('small.plot-days', {}, `${work.daysLeft} d`) : null,
      );
    });
    const pointed = this.plotHovered ?? this.plot;
    this.viewEl.append(
      this.panel(
        'fac-left',
        'left',
        h(
          'div.x-head',
          { title: 'Pulsa una parcela aquí o en el Bastión. Se construye pegado a otro edificio y se despeja junto a terreno ya despejado.' },
          'Parcelas',
          this.plot ? h('button.x-btn.small.plot-drop', { title: 'Soltar la parcela (Esc)', onclick: () => this.pickPlot(null) }, 'Soltar ✕') : null,
        ),
        h('div.plot-grid', {}, ...cells),
        h('p.plot-hover', {}, ...this.plotHoverLine(c, pointed)),
        h('div.x-head', {}, 'Efectos'),

        ...lines.map(([icon, text]) => h('div.x-effect', {}, h(`span.agenda-icon.${icon}`), h('span', {}, text))),
        ...bonuses.map((b) =>
          h('div.x-effect.bonus', { title: b.def.text }, h('span.agenda-icon.flag'), h('span', {}, `${FACILITIES[b.def.facility].name} + ${buildingName(b.def.next)}`)),
        ),
      ),
    );
    const plot = this.plot ? plotById(c, this.plot) : undefined;
    if (!plot) return;
    const work = workOn(plot.id);
    const content: (HTMLElement | null)[] = [h('div.alert-kicker', {}, `PARCELA ${plotCode(plot)}`)];
    if (work) {
      content.push(
        h('h2.x-title', {}, work.kind === 'clear' ? 'Despejando el terreno' : work.kind === 'upgrade' ? `${FACILITIES[work.facility!].name} → nivel ${(plot.level ?? 1) + 1}` : FACILITIES[work.facility!].name),
        this.bar(work.total - work.daysLeft, work.total),
        h('p.x-muted', {}, `Faltan ${work.daysLeft} días`),
      );
    } else if (plot.terrain !== 'clear') {
      const terrain = TERRAINS[plot.terrain];
      const { cost, salvage } = clearCost(plot);
      const days = workDays(c, clearCost(plot).days);
      const blocked = this.why(
        [!!clearable(c, plot), clearable(c, plot) ?? ''],
        [crewsFull, crewsText],
        [!!c.proposal, 'Hay una propuesta pendiente'],
        [!canAfford(c, cost), this.shortfall(cost)],
      );
      content.push(
        h('h2.x-title', {}, terrain.name),
        h('p.x-text', {}, terrain.description),
        h('p.x-chips', {}, this.chip(costText(cost)), this.chip(`${days} días`), salvage ? this.chip(`+${salvage} §`, 'good') : null),
        h('button.x-btn.primary', { disabled: !!blocked, onclick: () => this.send({ type: 'clear', plot: plot.id }) }, this.decide('despejar')),
        this.whyEl(blocked),
      );
    } else if (plot.building && plot.building in FIXED_NAMES_SET) {
      content.push(h('h2.x-title', {}, buildingName(plot.building)), h('p.x-muted', {}, 'Del búnker original: no se mueve.'));
    } else if (plot.building) {
      const def = FACILITIES[plot.building as keyof typeof FACILITIES];
      const adj = ADJACENCY.find((a) => a.facility === def.id);
      const active = bonuses.find((b) => b.plot === plot.id);
      const level = plot.level ?? 1;
      const now = level === 1 ? def.description : def.upgrades[level - 2]!;
      content.push(
        h('h2.x-title', {}, `${def.name} · nivel ${level}`),
        h('p.x-good', {}, 'En servicio'),
        h('p.x-text', {}, now),
        adj ? h(active ? 'p.x-good' : 'p.x-muted', {}, `${active ? `Pared con pared con ${withArticle(adj.next)}` : `Mejor ${nextTo(adj.next)}`}: ${adj.text}`) : null,
      );
      if (def.id === 'memorial') {
        // The names, as Lore asked: rank, missions and the day they fell.
        const fallen = c.soldiers.filter((s) => !s.alive).sort((a, b) => (b.fellDay ?? 0) - (a.fellDay ?? 0));
        content.push(
          h('div.x-head', {}, `Caídos · ${fallen.length}`),
          fallen.length
            ? h(
                'div.x-list.fallen',
                {},
                ...fallen.map((s) =>
                  h(
                    'div.x-row.fallen-row',
                    { style: { '--row-accent': this.playerColor(s.owner) } },
                    h('b', {}, displayName(s.name, s.nickname)),
                    h('small', {}, `${RANKS[s.rank]!.name} · ${s.missions} ${s.missions === 1 ? 'misión' : 'misiones'}${s.fellDay !== undefined ? ` · cayó el día ${s.fellDay}` : ''}`),
                  ),
                ),
              )
            : h('p.x-muted', {}, 'Nadie todavía. Que siga así.'),
        );
      }
      if (level < 3) {
        // The next level: what it brings, its price and why it can't be done yet.
        const next = upgradeCost(def.id, level + 1);
        const blocker = upgradable(c, plot);
        const why = this.why(
          [!!blocker, blocker ?? ''],
          [crewsFull, crewsText],
          [!!c.proposal, 'Hay una propuesta pendiente'],
          [!canAfford(c, next.cost), this.shortfall(next.cost)],
        );
        content.push(
          h('div.x-head', {}, `Nivel ${level + 1}`),
          h('p.x-text', {}, def.upgrades[level - 1]!),
          h('p.x-chips', {}, this.chip(costText(next.cost)), this.chip(`${workDays(c, next.days)} días`), this.chip(`Bastión nivel ${next.level}`)),
          h('button.x-btn.primary', { disabled: !!why, onclick: () => this.send({ type: 'upgrade', plot: plot.id }) }, this.decide('mejorar')),
          this.whyEl(why),
        );
      } else {
        content.push(h('p.x-muted', {}, 'Al máximo.'));
      }
    } else {
      content.push(h('h2.x-title', {}, 'Terreno libre'), crewsFull ? h('p.x-why', {}, crewsText) : null);
      // By what they are for, each with its mark: the effect in one line, the rest in the card's title.
      const locked: HTMLElement[] = [];
      for (const [group, ids] of BUILD_GROUPS) {
        const cards: HTMLElement[] = [];
        for (const id of ids) {
          const def = FACILITIES[id];
          const blocker = buildable(c, plot, def.id);
          if (blocker) {
            locked.push(h('div.build-card.locked', { title: `${def.description}\n\n${blocker}` }, plotIcon(def.id), h('b', {}, def.name), h('small.x-warn', {}, blocker)));
            continue;
          }
          const bonus = adjacencyAt(c, plot, def.id);
          const why = this.why([!!c.proposal, 'Hay una propuesta pendiente'], [!canAfford(c, def.cost), this.shortfall(def.cost)]);
          cards.push(
            h(
              `div.build-card${bonus ? '.bonus' : ''}`,
              { title: `${def.description}${bonus ? `\n\n${cap(nextTo(bonus.next))}: ${bonus.text}` : ''}` },
              plotIcon(def.id),
              h('b', {}, def.name),
              h('small', {}, def.description.split('.')[0] + '.'),
              h(
                'span.build-meta',
                {},
                this.chip(`${def.cost} §`),
                this.chip(`${workDays(c, def.days)} d de obra`, '', 'Días de obra: avanzan cuando pasa el tiempo'),
                bonus ? this.chip(`+ ${cap(nextTo(bonus.next))}`, 'good', bonus.text) : null,
              ),
              h(
                'button.x-btn.small',
                { disabled: crewsFull || !!why, title: why, onclick: () => this.send({ type: 'build', plot: plot.id, facility: def.id }) },
                this.decide('construir'),
              ),
            ),
          );
        }
        if (cards.length) content.push(h('div.x-group', {}, group), h('div.build-grid', {}, ...cards));
      }
      if (locked.length)
        content.push(
          h(
            'details.build-locked',
            {},
            h('summary', {}, `Aún no se pueden construir aquí (${locked.length})`),
            h('div.build-grid', {}, ...locked),
          ),
        );
    }
    this.viewEl.append(this.panel('fac-right', 'right', ...content));
  }

  // -------------------------------------------------------------- squad

  private renderSquad(c: CampaignState): void {
    const offer = this.offerById(c.activeMission);
    if (!offer) {
      this.viewEl.append(h('div.strat-center-top', {}, h('div.x-panel', {}, h('p.x-text', {}, 'No hay ninguna misión elegida.'), h('button.x-btn', { onclick: () => void this.go('geoscape') }, 'Ir a la geoesfera'))));
      this.bastion.setLineup([]);
      return;
    }
    const max = squadMax(c);
    const limit = squadLimit(c, this.connected());
    const squad = c.squad.map((id) => c.soldiers.find((s) => s.id === id)).filter((s): s is CampaignSoldier => !!s);
    const pads = squadPads(c).map((id) => squad.find((s) => s.id === id) ?? null);
    this.bastion.setLineup(pads.map((s) => (s ? { template: soldierTemplate(s.cls), color: this.playerColor(s.owner), appearance: s.appearance, gear: this.gearFor(c, s) } : null)));
    const myCount = squad.filter((s) => s.owner === this.mySlot).length;

    this.viewEl.append(
      h(
        'div.strat-center-top',
        {},
        h(
          'div.x-panel.briefing-head',
          {},
          h('div.alert-kicker', {}, 'SELECCIÓN DE ESCUADRA'),
          h('h1', { title: offerBriefing(c, offer) }, offer.name),
          h('p.x-sub', { title: offerBriefing(c, offer) }, `${MISSION_NAMES[offer.kind]} · ${REGIONS[offer.region].name} · ${'★'.repeat(offer.difficulty)}`),
        ),
      ),
    );
    const roster = c.soldiers.filter((s) => s.owner === this.mySlot && s.alive);
    this.viewEl.append(
      this.panel(
        'squad-left',
        'left.compact',
        h(
          'div.x-head',
          { title: 'Pulsa un soldado para meterlo o sacarlo. Si tu compañero no llena su parte, puedes ocupar sus huecos; los recupera cuando tenga a quién llevar. El equipo se cambia en el Cuartel.' },
          myCount > limit ? `Tus soldados · ${myCount} (${myCount - limit} en huecos de ${this.partner()?.name ?? 'tu compañero'})` : `Tus soldados · ${myCount}/${limit}`,
        ),
        h(
          'div.x-list',
          {},
          ...roster.map((s) => {
            const on = c.squad.includes(s.id);
            const ready = soldierReady(s);
            const gear = [s.loadout.utility, s.loadout.ammo].filter((g): g is ItemId => !!g).map((g) => ITEMS[g].name);
            return h(
              `button.x-row.soldier${on ? '.selected' : ''}${ready ? '' : '.dim'}`,
              { disabled: !ready && !on, onclick: () => this.send({ type: 'squad', soldier: s.id, on: !on }) },
              this.clsTag(s.cls),
              h('b', { title: [RANKS[s.rank]!.name, ...gear].join(' · ') }, displayName(s.name, s.nickname)),
              h('span.rank', { title: RANKS[s.rank]!.name }, ...Array.from({ length: s.rank }, () => h('i'))),
              on ? this.chip('✓', 'good', 'En la escuadra') : ready ? h('span') : this.chip(`${s.woundedDays} d`, 'warn', 'Herido'),
            );
          }),
        ),
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
            h(
              `span.ready-mark${c.launchReady[slot] ? '.on' : ''}`,
              { style: { '--accent': this.playerColor(slot) } },
              h('b', {}, slot === this.mySlot ? 'Tú' : this.playerName(slot)),
              c.launchReady[slot] ? ' · LISTO ✓' : ' · eligiendo…',
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
          ready ? 'LISTOS PARA DESPEGAR' : partnerReady ? `DESPEGAR: ${partner!.name.toUpperCase()} YA ESTÁ LISTO` : 'LANZAR MISIÓN',
        ),
        h('small.x-muted', {}, ready && waiting ? `Esperando a ${partner!.name}` : partnerReady ? `${partner!.name} espera para despegar` : `${squad.length}/${max} soldados`),
        h(
          'div.alert-actions',
          {},
          h('button.x-btn', { onclick: () => void this.go('hub') }, 'Volver al Bastión'),
          h('button.x-btn.danger', { onclick: () => this.send({ type: 'cancelMission' }) }, 'Cancelar misión'),
        ),
      ),
    );

    const cards = h('div.lineup-cards');
    // A free place this player could take but has nobody ready to put in: recruit straight into it (#39).
    const spare = roster.some((s) => soldierReady(s) && !c.squad.includes(s.id));
    const room = !spare && roster.length < soldierCap(c) && !squadPlace(c, this.mySlot, this.connected()).error;
    const recruitable = room && bedsFree(c) > 0;
    const cost = recruitCost(c);
    pads.forEach((s, i) => {
      const card = s
        ? h(
            `div.lineup-card${together && c.launchReady[s.owner] ? '.ready' : ''}`,
            { style: { '--accent': this.playerColor(s.owner) } },
            h('b', {}, displayName(s.name, s.nickname)),
            h('small', {}, `${TEMPLATES[soldierTemplate(s.cls)].name} · ${RANKS[s.rank]!.name}`),
            c.players.length > 1 ? h('small', {}, s.owner === this.mySlot ? 'Tuyo' : `De ${this.playerName(s.owner)}`) : null,
            s.owner === this.mySlot ? h('button.x-btn', { onclick: () => this.send({ type: 'squad', soldier: s.id, on: false }) }, 'Quitar') : null,
          )
        : recruitable
          ? this.recruitCard(c, cost)
          : h(
              'div.lineup-card.empty',
              { title: spare ? 'Elige un soldado a la izquierda' : '' },
              h('b', {}, 'Libre'),
              room && this.padSide(c, i) === this.mySlot ? h('small', {}, 'Para reclutar más soldados, construid un barracón') : null,
            );
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
          title: afford ? `Un recluta nuevo, directo a la escuadra. ${RECRUIT_DESCRIPTION}` : this.shortfall(cost),
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
    const step = () => {
      const pads = Array.from({ length: count }, (_, i) => this.stage.project(this.bastion.padPosition(i, count)));
      const spacing = count > 1 ? Math.abs(pads[1]!.x - pads[0]!.x) : 140;
      cards.style.setProperty('--card-w', `${Math.max(84, Math.min(150, spacing - 8))}px`);
      for (const el of Array.from(cards.children) as HTMLElement[]) {
        const p = pads[Number(el.dataset.pad)];
        if (!p) continue;
        el.style.left = `${p.x}px`;
        el.style.top = `${p.y + 12}px`;
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
        ? `${displayName(wounded[0]!.name, c.soldiers.find((x) => x.id === wounded[0]!.id)?.nickname)} no podrá desplegar hasta el día ${c.day + last}.`
        : `${wounded.length} heridos: no podrán desplegar hasta el día ${c.day + last} como mucho.`;
    return h('p.x-text.x-warn', { title: 'La enfermería cura las heridas el doble de rápido' }, text);
  }

  private renderDebrief(c: CampaignState): void {
    const report = c.lastReport;
    if (!report) {
      void this.go('hub');
      return;
    }
    const entries: LineupEntry[] = report.squad.map((e) => {
      const s = c.soldiers.find((x) => x.id === e.id);
      return { template: soldierTemplate(e.cls), color: this.playerColor(e.owner), status: e.status, appearance: s?.appearance, gear: s ? this.gearFor(c, s) : undefined };
    });
    this.bastion.setLineup(entries);
    // The outcome in a line of figures; the details are in each chip's title, the story's page one click away.
    const credits = (report.reward?.credits ?? 0) + report.salvage;
    const creditsWhy = [report.reward?.credits ? `Recompensa: ${report.reward.credits}` : '', report.bodies ? `${report.bodies} cuerpos registrados: ${report.salvage}` : ''].filter(Boolean).join(' · ');
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
          story ? h('details.debrief-story', {}, h('summary', {}, 'Leer el parte'), h('p.x-text', {}, story)) : null,
        ),
      ),
    );
    const cards = h('div.lineup-cards');
    report.squad.forEach((e, i) => {
      const status = e.status === 'dead' ? 'CAÍDO EN COMBATE' : e.status === 'wounded' ? `HERIDO · ${e.woundedDays} días` : 'ILESO';
      const card = h(
        `div.lineup-card.${e.status}`,
        { style: { '--accent': this.playerColor(e.owner) } },
        h('b', {}, displayName(e.name, c.soldiers.find((x) => x.id === e.id)?.nickname)),
        h(`small${e.status === 'dead' ? '.x-bad' : e.status === 'wounded' ? '.x-warn' : ''}`, {}, status),
        h('small', {}, `${e.kills} baja${e.kills === 1 ? '' : 's'}${e.status === 'dead' ? '' : ` · +${e.xpGained} XP`}`),
        e.promotedTo !== null ? h('small.promo', {}, `▲ ${RANKS[e.promotedTo]!.name.toUpperCase()}${e.specializedAs ? ` · ${TEMPLATES[e.specializedAs].name.toUpperCase()}` : ''}`) : null,
      );
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
              void this.go('hub');
            },
          },
          'CONTINUAR',
        ),
        // What comes next, the same for both commanders, so nobody wonders what the other was told.
        (() => {
          const now = nextSteps(c, this.mySlot)[0];
          return now ? h('small.debrief-next', {}, h('b', {}, now.tone === 'urgent' ? 'Lo más urgente: ' : 'Después: '), now.label) : null;
        })(),
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
        themeSettings(redraw),
        soundSettings(redraw),
        graphicsSettings(redraw),
        h('div.alert-actions', {}, h('button.x-btn.primary', { onclick: () => ((this.settingsOpen = false), this.render()) }, 'Cerrar')),
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
          c.event === 'finale' ? 'DOBLE LLAVE · DECISIÓN FINAL' : c.event === 'quest' && c.questEvent ? `ENCARGO · ${QUESTS[c.questEvent].title.toUpperCase()}` : 'ACONTECIMIENTO',
        ),
        h('h2.x-title', {}, def.title),
        h('p.x-text', {}, def.text),
        h(
          'div.event-options',
          {},
          ...([0, 1] as const).map((i) =>
            h(
              'button.event-option',
              { disabled: !!c.proposal || !eventOptionAvailable(c, i), onclick: () => this.send({ type: 'answerEvent', option: i }) },
              h('b', {}, def.options[i].label),
              h('small', {}, def.options[i].effect),
              eventOptionAvailable(c, i) ? null : h('small.x-why', {}, 'Ahora no está a vuestro alcance'),
            ),
          ),
        ),
        proposal ? h('p.x-warn', {}, proposal.by === this.mySlot ? `Esperando a ${this.partner()?.name ?? 'tu compañero'}…` : `${this.playerName(proposal.by)} propone ${proposal.text}: acepta o rechaza arriba.`) : h('p.x-muted', {}, 'El tiempo no avanza hasta que decidáis.'),
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
      if (s) content = this.promotionPanel(c, s);
    }
    // A sheet of paper when a notice or a panel opens.
    if (content && !this.modal.classList.contains('show')) uiSound('paper');
    this.modal.classList.toggle('show', !!content);
    if (content) this.modal.append(content);
  }
}
