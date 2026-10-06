import './strategy.css';
import {
  ARMOR_COLORS,
  ARMOR_TIERS,
  armorTier,
  baseArmorTier,
  baseWeaponTier,
  bastionLevel,
  BIOME_NAMES,
  BUILD_IDS,
  buildable,
  BUILDS,
  canAfford,
  contactCapacity,
  contactCost,
  costText,
  defaultAppearance,
  displayName,
  DOOM_MAX,
  doomChangeText,
  doomName,
  doomSubject,
  ENDING_TITLES,
  epilogue,
  eventDef,
  eventOptionAvailable,
  excavationCost,
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
  PERKS,
  QUESTS,
  perksFor,
  randomAppearance,
  RANK_AIM,
  RANKS,
  RECRUIT_COST,
  REGION_LORE,
  regionIncome,
  REGIONS,
  Rng,
  shopPrice,
  slotName,
  soldierReady,
  soldierWeaponName,
  squadLimit,
  squadMax,
  squadMember,
  STORY,
  TECHS,
  TEMPLATES,
  TIER_RANK,
  TRAITS,
  VISOR_COLORS,
  WEAPON_TIER_DAMAGE,
  weaponTier,
  type Appearance,
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
  type PerkTier,
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
import { BastionSet, LAUNCH_SECONDS, type FixedRoom, type LineupEntry } from './bastion';
import { GeoscapeSet } from './geoscape';
import { Stage, type Shot } from './stage';

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
}

/** The Novedades tray keeps this many alerts at most. */
const MAX_NEWS = 20;

const REPORT_KEY = storageKey('seenReport');

const KIND_ICON: Record<MissionKind, string> = { elimination: '✕', recovery: '◆', sabotage: 'ϟ', hack: '⌘', rescue: '✚' };
const DIFFICULTY = ['', 'Moderada', 'Difícil', 'Muy difícil', 'Extrema'];
const CLASSES: ClassId[] = ['assault', 'grenadier', 'sharpshooter', 'specialist'];
const TIERS: PerkTier[] = [1, 2, 3, 4, 5];

const NAV: { view: BaseView; label: string }[] = [
  { view: 'hub', label: 'Bastión' },
  { view: 'geoscape', label: 'Geoesfera' },
  { view: 'research', label: 'Progreso' },
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
  research: 'Progreso',
  engineering: 'Armería',
  barracks: 'Cuartel',
  facilities: 'Instalaciones',
  squad: 'Hangar',
  debrief: 'Informe',
};

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
  private readonly radio = new Transmissions(() => ({ saludo: greeting(this.commanderNames()) }));
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
  private slot: string | null = null;
  private promoting = false;
  /** Open equipment picker ("soldierId:slot"). */
  private picking: string | null = null;
  /** "Volver al menú" asked: it takes both players, so it is confirmed first. */
  private leaving = false;

  constructor(container: HTMLElement, private readonly mySlot: Slot, private readonly handlers: StrategyHandlers) {
    this.bastion = new BastionSet((hot) => this.pickRoom(hot));
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
  }

  // ------------------------------------------------------------ public

  update(c: CampaignState): void {
    const prev = this.state;
    this.state = c;
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
    cancelAnimationFrame(this.anchorFrame);
    this.stage.dispose();
    this.root.remove();
  }

  // -------------------------------------------------------- navigation

  private async go(view: BaseView): Promise<void> {
    this.view = view;
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
        return this.slot ? this.bastion.room(this.slot) : this.bastion.facilities();
      default:
        return this.bastion.overview();
    }
  }

  /** Clicking a room of the Bastion opens its screen. */
  private pickRoom(hot: string): void {
    if (hot.startsWith('slot:')) {
      this.slot = hot.slice(5);
      if (this.view === 'facilities') {
        this.render();
        void this.stage.show('bastion', this.shotFor('facilities'));
      } else {
        void this.go('facilities');
      }
      return;
    }
    const target: Record<string, BaseView> = { command: 'geoscape', research: 'research', engineering: 'engineering', barracks: 'barracks' };
    if (hot === 'hangar') void this.go(this.state?.activeMission ? 'squad' : 'geoscape');
    else if (target[hot]) void this.go(target[hot]!);
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

  /** "Faltan 20 créditos", or '' when the cost is affordable. */
  private shortfall(cost: number): string {
    const missing = cost - this.c().credits;
    return missing > 0 ? `Faltan ${missing} créditos` : '';
  }

  private offerById(id: string | null): MissionOffer | undefined {
    return id ? this.state?.offers.find((o) => o.id === id) : undefined;
  }

  private reportKey(c: CampaignState): string {
    const r = c.lastReport;
    return r ? `${r.day ?? ''}:${r.missionName}:${r.victory}` : '';
  }

  private reportPending(c: CampaignState): boolean {
    return !!c.lastReport && !c.activeMission && this.reportKey(c) !== this.seenReport;
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
    const newReport = c.lastReport !== prev.lastReport && this.reportKey(c) !== this.reportKey(prev);
    const fresh: Alert[] = [];
    // Each level adds a page to the Expediente. After a mission it waits until the debrief is closed.
    for (let level = bastionLevel(prev) + 1; level <= bastionLevel(c); level++) {
      const page = EXPEDIENTE[level - 1]!;
      fresh.push({
        tone: 'good',
        kicker: `EXPEDIENTE CORO · HOJA ${level}`,
        rank: 90,
        title: page.title,
        body: page.paragraphs,
        text: `Bastión nivel ${level}. ${this.unlocksText(level)}`,
        note: page.note,
        action: { label: 'Ver el progreso', go: () => void this.go('research') },
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
    if (c.day > prev.day && !newReport) this.timeAlerts(prev, c, fresh);
    this.flushAlerts(fresh);
  }

  /** What changed while time passed: new operations, works, Órganos, the monthly letter, the doom track. */
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
    const work = prev.base.construction;
    if (work && c.base.construction?.slot !== work.slot) {
      fresh.push({
        tone: 'good',
        kicker: 'OBRAS TERMINADAS',
        rank: 20,
        title: work.kind === 'excavate' ? 'Sala excavada' : FACILITIES[work.facility!].name,
        text: work.kind === 'excavate' ? `${slotName(c.base.slots.find((s) => s.id === work.slot)!)} está lista para construir.` : FACILITIES[work.facility!].description,
        action: {
          label: 'Ver instalaciones',
          go: () => {
            this.slot = work.slot;
            void this.go('facilities');
          },
        },
      });
    }
    for (const r of c.regions.filter((x) => x.facility && !prev.regions.find((p) => p.id === x.id)?.facility)) {
      fresh.push({
        tone: 'bad',
        kicker: 'ÓRGANO',
        rank: 65,
        title: REGIONS[r.id].name,
        text: r.contacted
          ? `El Coro ha levantado un Órgano. Hará avanzar ${doomSubject(bastionLevel(c))} hasta que lo destruyáis.`
          : 'El Coro ha levantado un Órgano. Contactad con la región para poder asaltarlo.',
        action: { label: 'Ver en la geoesfera', go: () => this.focusRegion(r.id) },
      });
    }
    if (c.nextSupplyDay !== prev.nextSupplyDay) {
      fresh.push({
        tone: 'good',
        kicker: 'FIN DE MES',
        rank: 10,
        title: 'Carta del Consejo',
        text: `La consejera Halvorsen envía ${c.credits - prev.credits} créditos. Cada región contactada aumenta el pago.`,
      });
    }
    if (c.doom > prev.doom) {
      const level = bastionLevel(c);
      const subject = doomSubject(level);
      fresh.push({
        tone: 'bad',
        kicker: doomName(level).toUpperCase(),
        rank: 50,
        title: `${subject[0]!.toUpperCase()}${subject.slice(1)} avanza: ${c.doom}/${DOOM_MAX}${level >= PARTITURA_LEVEL ? ' compases' : ''}`,
        text: `Si llega a ${DOOM_MAX}, la campaña está perdida. Asaltar Órganos la hace retroceder.`,
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
      action: rumor
        ? {
            label: `${this.decide('escuchar')} · ${rumor.daysLeft} d`,
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
  }

  private focusRegion(id: RegionId): void {
    this.region = id;
    this.offer = null;
    this.geo.focus(id);
    void this.go('geoscape');
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
    this.bastion.select(this.view === 'facilities' ? this.slot : null);
    this.bastion.setLabels(this.view === 'hub' || (this.view === 'facilities' && !this.slot));
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
      h('div.strat-brand', {}, h('b', {}, 'BASTIÓN'), h('span', {}, `DÍA ${c.day}`)),
      h(
        'div.strat-res',
        {},
        h('span.strat-credits', { title: 'Créditos: construir, comprar accesorios, reclutar y contactar regiones' }, h('i', {}, '₡'), h('b', {}, String(c.credits))),
        this.levelBadge(c),
      ),
      h(
        'div.strat-doom',
        { title: `${bastionLevel(c) >= PARTITURA_LEVEL ? GLOSSARY.Partitura : `${doomName(bastionLevel(c))}: si se llena, la campaña está perdida.`} Asaltar Órganos la hace retroceder.` },
        h('small', {}, doomName(bastionLevel(c)).toUpperCase()),
        pips,
        h('b', {}, `${c.doom}/${DOOM_MAX}`),
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
      h('button.x-btn', { onclick: () => ((this.leaving = true), this.render()), title: 'Volver al menú principal (la campaña queda guardada)' }, 'Menú'),
    );
  }

  private renderProposal(c: CampaignState): void {
    clear(this.proposalEl);
    const p = c.proposal;
    this.proposalEl.classList.toggle('show', !!p);
    if (!p) return;
    if (p.by === this.mySlot) {
      append(
        this.proposalEl,
        h('span', {}, `Has propuesto ${p.text}. Esperando a ${this.partner()?.name ?? 'tu compañero'}…`),
        h('button.x-btn', { onclick: () => this.send({ type: 'withdraw' }) }, 'Retirar'),
      );
    } else {
      append(
        this.proposalEl,
        h('span', {}, h('b', { style: { color: this.playerColor(p.by) } }, this.playerName(p.by)), ` propone ${p.text}.`),
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
    // What is pending on each screen, as a number on its tab (red if something is urgent).
    const pending = new Map<BaseView, { n: number; urgent: boolean; what: string[] }>();
    for (const step of nextSteps(c, this.mySlot)) {
      if (step.view === 'hub') continue;
      const e = pending.get(step.view) ?? { n: 0, urgent: false, what: [] };
      e.n += step.count ?? 1;
      e.urgent ||= step.tone === 'urgent';
      e.what.push(step.label);
      pending.set(step.view, e);
    }
    for (const item of items) {
      const here = partner?.connected && this.partnerView === item.view;
      const todo = pending.get(item.view);
      this.nav.append(
        h(
          `button${this.view === item.view ? '.on' : ''}${item.view === 'squad' ? '.urgent' : ''}`,
          { onclick: () => void this.go(item.view), title: todo ? todo.what.join(' · ') : '' },
          item.label,
          todo ? h(`span.nav-count${todo.urgent ? '.urgent' : ''}`, {}, String(todo.n)) : null,
          here ? h('span.partner-dot', { style: { background: partner!.color }, title: `${partner!.name} está aquí` }) : null,
        ),
      );
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
    if (go.slot) this.slot = go.slot;
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
    const work = c.base.construction;
    if (work) {
      rows.push(
        h(
          'button.x-row.agenda',
          { onclick: () => ((this.slot = work.slot), void this.go('facilities')), title: 'Obras en marcha' },
          h('span.agenda-icon.pick'),
          h('b', {}, work.kind === 'excavate' ? 'Excavación' : FACILITIES[work.facility!].name),
          this.chip(`${work.daysLeft} d`),
        ),
      );
    }
    rows.push(
      h(
        'div.x-row.agenda',
        { title: 'Créditos del Consejo: más por cada región contactada' },
        h('span.agenda-icon.clock'),
        h('b', {}, 'Pago del Consejo'),
        this.chip(`${c.nextSupplyDay - c.day} d`),
      ),
    );
    const ticker = h('div.strat-ticker');
    ticker.append(h('div.x-panel', {}, nowEl, h('div.x-head', { title: 'Pulsa una sala del Bastión para entrar' }, 'Agenda'), h('div.x-list', {}, ...rows)));
    this.viewEl.append(ticker);
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
            { title: `${o.facility ? 'Órgano' : o.story ? `Historia · ${MISSION_NAMES[o.kind]}` : o.quest ? `Encargo · ${MISSION_NAMES[o.kind]}` : MISSION_NAMES[o.kind]} · ${REGIONS[o.region].name}` },
            h('b', {}, o.name),
            h('small', {}, REGIONS[o.region].name),
          ),
          h(
            `span.x-side.days${days !== null && days <= 2 ? '.x-bad' : ''}`,
            { title: days === null ? 'No caduca' : `Caduca en ${Math.max(0, days)} días` },
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
            h('div.x-head', { title: GLOSSARY.Órgano }, 'Órganos'),
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
      h('div.x-head', { title: 'Pulsa una región del globo para ver sus datos' }, 'Red del Consejo'),
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
        h('div.scan-day', {}, `DÍA ${c.day}`),
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
                    : 'Avanza el tiempo'),
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
                ? h('small.x-good', {}, `Escuchando · ${r.daysLeft} d`)
                : h(
                    'button.x-btn.small',
                    { disabled: !!blocked, title: blocked || `Hacen falta ${r.daysLeft} días de escaneo`, onclick: () => this.send({ type: 'investigate', id: r.id }) },
                    this.decide('escuchar'),
                  ),
            ),
            h('span.x-side', {}, listening ? '' : `${Math.max(0, r.expiresDay - c.day)} d`),
          );
        }),
      ),
    ];
  }

  private briefing(c: CampaignState, o: MissionOffer): (HTMLElement | null)[] {
    const r = o.reward;
    const rewards = [
      r.credits ? `${r.credits} créditos` : '',
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
      h('p.x-muted', { title: 'Créditos que aporta a cada carta mensual del Consejo una vez contactada' }, `${REGION_LORE[id].ally} · +${regionIncome(c, id)} ₡ al mes`),
      this.regionQuest(c, id),
      r.facility ? h('p.x-text.x-bad', {}, `Órgano: hace avanzar ${doomSubject(bastionLevel(c))} cada 14 días.${r.contacted ? '' : ' Contactad para asaltarlo.'}`) : null,
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
      ...(['gear', 'items'] as const).map((t) =>
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
            h('span.x-side', { title: 'Libres / en el almacén' }, `${itemsFree(c, item.id)}/${c.inventory[item.id] ?? 0}`),
          ),
        );
      }
    }
    const note = this.engTab === 'items' && facilityCount(c, 'workshop') ? 'Taller: −25 %' : '';
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
          this.chip(`${itemsFree(c, item.id)}/${c.inventory[item.id] ?? 0} libres`, '', 'Se equipa en el Cuartel; si el soldado cae, se pierde'),
          unlocked ? this.chip(costText(cost), '', 'No necesita votación') : this.chip(`Nivel ${item.level} del Bastión`, 'warn'),
        ),
        unlocked ? h('button.x-btn.primary', { disabled: !canAfford(c, cost), onclick: () => this.send({ type: 'buy', item: item.id }) }, 'Comprar') : null,
        unlocked ? this.whyEl(this.shortfall(cost)) : null,
      ),
    );
  }

  /** A weapon or armour tier: what it gives, who may carry it, and each class's version. */
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
    const mine = c.soldiers.filter((s) => s.owner === this.mySlot).sort((a, b) => Number(b.alive) - Number(a.alive) || b.rank - a.rank);
    const theirs = c.soldiers.filter((s) => s.owner !== this.mySlot).sort((a, b) => Number(b.alive) - Number(a.alive) || b.rank - a.rank);
    if (!this.soldier || !c.soldiers.some((s) => s.id === this.soldier)) this.soldier = mine.find((s) => s.alive)?.id ?? c.soldiers[0]?.id ?? null;
    const row = (s: CampaignSoldier) =>
      h(
        `button.x-row.soldier${this.soldier === s.id ? '.selected' : ''}${s.alive ? '' : '.dim'}`,
        {
          style: { '--row-accent': this.soldier === s.id ? 'var(--x-accent)' : this.playerColor(s.owner) },
          onclick: () => {
            this.soldier = s.id;
            this.promoting = false;
            this.picking = null;
            this.custom = null;
            this.render();
          },
        },
        h('span.cls', { title: TEMPLATES[s.cls].name }, TEMPLATES[s.cls].short),
        h('b', { title: RANKS[s.rank]!.name }, displayName(s.name, s.nickname)),
        h('span.rank', { title: RANKS[s.rank]!.name }, ...Array.from({ length: s.rank }, () => h('i'))),
        s.pendingTiers.length && s.alive
          ? this.chip('▲', 'good', 'Ascenso pendiente: elige habilidad')
          : h(`span.x-side${!s.alive ? '.x-bad' : s.woundedDays ? '.x-warn' : ''}`, {}, !s.alive ? 'Caído' : s.woundedDays ? `${s.woundedDays} d` : 'Listo'),
      );
    const count = mine.filter((s) => s.alive).length;
    const cap = soldierCap(c);
    const blockedRecruit = this.why([count >= cap, 'El cuartel está lleno'], [true, this.shortfall(RECRUIT_COST)]);
    const recruit = h(
      'div',
      {},
      h('div.x-group', { title: `Cada recluta cuesta ${RECRUIT_COST} créditos; como mucho ${cap} soldados por jugador` }, 'Reclutar ', this.chip(costText(RECRUIT_COST)), ' ', this.chip(`${count}/${cap}`)),
      h(
        'div.x-tabs',
        {},
        ...CLASSES.map((cls) => h('button', { disabled: !!blockedRecruit, onclick: () => this.send({ type: 'recruit', cls }) }, TEMPLATES[cls].short)),
      ),
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
    this.bastion.setShowcase(s?.alive ? s.cls : null, s ? this.playerColor(s.owner) : '#fff', draft?.appearance ?? s?.appearance, s ? this.gearFor(c, s) : undefined);
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
        h('div.alert-kicker', {}, `${TEMPLATES[s.cls].name.toUpperCase()} · ${isMine ? 'TUYO' : `DE ${this.playerName(s.owner).toUpperCase()}`}`),
        h('h2.x-title', { title: `Misiones: ${s.missions} · Bajas: ${s.kills}` }, displayName(s.name, s.nickname)),
        h('p.x-sub', {}, `${RANKS[s.rank]!.name} · ${soldierWeaponName(s.cls, weaponTier(c, s))}`),
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
          this.stat('aim', String(TEMPLATES[s.cls].aim + stats.mods.aim), `Puntería (cada rango da +${RANK_AIM})`),
          this.stat('will', String(stats.will), 'Voluntad'),
          h(`b.x-state${!s.alive ? '.x-bad' : s.woundedDays ? '.x-warn' : '.x-good'}`, {}, !s.alive ? 'Caído' : s.woundedDays ? `Herido · ${s.woundedDays} d` : 'Listo'),
        ),
        s.traits?.length ? h('div.x-group', {}, 'Rasgos') : null,
        s.traits?.length ? h('p.x-chips', {}, ...s.traits.map((t) => this.chip(TRAITS[t].name, '', TRAITS[t].description))) : null,
        h('div.x-group', {}, 'Equipo'),
        ...(['weapon', 'armor'] as const).map((kind) => {
          const tier = kind === 'weapon' ? weaponTier(c, s) : armorTier(c, s);
          const name = kind === 'weapon' ? soldierWeaponName(s.cls, tier) : ARMOR_TIERS[tier - 1]!.name;
          const blocker = s.alive ? nextTierBlocker(c, s, kind) : null;
          const tech = tier < 3 ? (kind === 'weapon' ? [TECHS.magWeapons, TECHS.plasmaWeapons] : [TECHS.plateArmor, TECHS.predatorArmor])[tier - 1]! : null;
          const short = tech ? `→ Nv ${tech.level} · ${RANKS[TIER_RANK[tier as 1 | 2]]!.name}` : '';
          return h('div.x-gear', {}, this.tierMarks(tier), h('span', {}, name), blocker && short ? this.chip(short, '', blocker) : null);
        }),
        loadout('utility', 'Utilidad'),
        loadout('ammo', 'Munición'),
        h('div.x-group', {}, 'Habilidades'),
        s.perks.length
          ? h('p.x-chips', {}, ...s.perks.map((p) => this.chip(PERKS[p].name, '', PERKS[p].description)))
          : h('p.x-muted', {}, `Llegan al ascender a ${RANKS[1]!.name}.`),
        h(
          `button.x-btn${s.pendingTiers.length && isMine && s.alive ? '.primary.waiting' : ''}`,
          { onclick: () => ((this.promoting = true), this.render()) },
          s.pendingTiers.length && isMine && s.alive ? '¡Ascenso! Elegir habilidad' : 'Árbol de habilidades',
        ),
      ),
    );
  }

  /** XCOM-style soldier customizer: name, nickname, colours, head and build, previewed on the pedestal. */
  private customizer(s: CampaignSoldier, d: NonNullable<StrategyScreen['custom']>): (HTMLElement | null)[] {
    const set = (patch: Partial<Appearance>) => {
      d.appearance = { ...d.appearance, ...patch };
      this.render();
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
    const swatches = (key: 'primary' | 'secondary' | 'visor', label: string, colors: readonly string[]) => {
      const current = d.appearance[key].toLowerCase();
      return h(
        'div.x-field',
        {},
        h('small', {}, label),
        h(
          'div.x-swatches',
          {},
          ...colors.map((color) =>
            h(`button.x-swatch${color.toLowerCase() === current ? '.on' : ''}`, { style: { '--swatch': color }, title: color, onclick: () => set({ [key]: color }) }),
          ),
          // Any other colour, through the system picker (applied when it closes).
          h('input.x-swatch-any', { type: 'color', value: current, title: 'Otro color', onchange: (e: Event) => set({ [key]: (e.target as HTMLInputElement).value }) }),
        ),
      );
    };
    const chips = <K extends string>(label: string, ids: readonly K[], names: Record<K, { name: string }>, current: K, pick: (id: K) => void) =>
      h(
        'div.x-field',
        {},
        h('small', {}, label),
        h('div.x-chips', {}, ...ids.map((id) => h(`button${id === current ? '.on' : ''}`, { onclick: () => pick(id) }, names[id].name))),
      );
    const blocked = this.why([!d.name.trim(), 'Ponle un nombre']);
    return [
      h('div.alert-kicker', {}, `PERSONALIZAR · ${TEMPLATES[s.cls].name.toUpperCase()}`),
      h('h2.x-title', {}, displayName(d.name.trim() || s.name, d.nickname.trim())),
      text('name', 'Nombre', NAME_MAX, s.name),
      text('nickname', 'Apodo', NICKNAME_MAX, 'Sin apodo'),
      swatches('primary', 'Armadura', ARMOR_COLORS),
      swatches('secondary', 'Detalles', ARMOR_COLORS),
      swatches('visor', 'Visor', VISOR_COLORS),
      chips('Cabeza', HEAD_IDS, HEADS, d.appearance.head, (head) => set({ head })),
      chips('Complexión', BUILD_IDS, BUILDS, d.appearance.build, (build) => set({ build })),
      h(
        'div.x-actions',
        {},
        h('button.x-btn.small', { onclick: () => set(randomAppearance(new Rng(Date.now() >>> 0))) }, 'Aleatorio'),
        h('button.x-btn.small', { onclick: () => set(defaultAppearance(s.cls, s.owner)) }, 'Original'),
      ),
      h(
        'div.x-actions',
        {},
        h(
          'button.x-btn.primary',
          {
            disabled: !!blocked,
            onclick: () => {
              this.send({ type: 'customize', soldier: s.id, name: d.name.trim(), nickname: d.nickname.trim(), appearance: d.appearance });
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

  /** XCOM-style promotion screen: ranks down, the two choices across. */
  private promotionPanel(s: CampaignSoldier): HTMLElement {
    const isMine = s.owner === this.mySlot && s.alive;
    const grid = h('div.perk-grid');
    for (const tier of TIERS) {
      const rank = RANKS.findIndex((r) => r.perkTier === tier);
      const pending = s.pendingTiers.includes(tier);
      grid.append(h('div.rank', {}, RANKS[rank]!.name));
      for (const perk of perksFor(s.cls, tier)) {
        const chosen = s.perks.includes(perk.id);
        const pickable = pending && isMine;
        grid.append(
          h(
            `button.perk-cell${chosen ? '.chosen' : ''}${pickable ? '.pickable' : ''}${s.rank < rank ? '.locked' : ''}`,
            { disabled: !pickable, onclick: () => this.send({ type: 'promote', soldier: s.id, perk: perk.id }) },
            h('b', {}, `${perk.name}${perk.ability ? ' ◆' : ''}`),
            h('small', {}, perk.description),
          ),
        );
      }
    }
    return h(
      'div.x-panel.wide',
      {},
      h('div.alert-kicker', {}, `${TEMPLATES[s.cls].name.toUpperCase()} · ${RANKS[s.rank]!.name.toUpperCase()}`),
      h('h2.x-title', {}, displayName(s.name, s.nickname)),
      h('p.x-sub', {}, isMine && s.pendingTiers.length ? 'Elige una habilidad para cada ascenso pendiente.' : 'Las habilidades con ◆ son activas (enfriamiento o usos limitados).'),
      grid,
      h('div.alert-actions', {}, h('button.x-btn', { onclick: () => ((this.promoting = false), this.render()) }, 'Cerrar')),
    );
  }

  // -------------------------------------------------------- facilities

  private renderFacilities(c: CampaignState): void {
    const lines: [string, string][] = [];
    if (facilityCount(c, 'simulation')) lines.push(['xp', 'XP de misión +25 %']);
    if (facilityCount(c, 'workshop')) lines.push(['flag', 'Accesorios −25 %']);
    if (facilityCount(c, 'infirmary')) lines.push(['hp', 'Heridas ×2 de rápido']);
    if (facilityCount(c, 'training')) lines.push(['flag', `Escuadra de ${squadMax(c)}`]);
    lines.push(['will', `Contactos: ${contactCapacity(c)} regiones`]);
    const work = c.base.construction;
    // Every room that can be acted on, with its state and its action: the same as clicking it in 3D.
    const pick = (id: string) => {
      this.slot = id;
      this.render();
      void this.stage.show('bastion', this.shotFor('facilities'));
    };
    const rooms = c.base.slots
      .filter((sl) => sl.excavated || sl.row === 0 || c.base.slots.some((up) => up.col === sl.col && up.row === sl.row - 1 && up.excavated))
      .sort((a, b) => a.row - b.row || a.col - b.col)
      .map((sl) => {
        const busy = work?.slot === sl.id;
        const state = busy
          ? `${work!.kind === 'excavate' ? 'Excavando' : FACILITIES[work!.facility!].name}`
          : !sl.excavated
            ? 'Roca'
            : sl.facility
              ? FACILITIES[sl.facility].name
              : 'Vacía';
        const tag = busy
          ? this.chip(`${work!.daysLeft} d`)
          : !sl.excavated
            ? this.chip(`Excavar · ${costText(excavationCost(sl.row).cost)}`, '', `${excavationCost(sl.row).days} días`)
            : sl.facility
              ? h('span.x-side', {}, '✓')
              : this.chip('Construir', 'good');
        return h(
          `button.x-row.room${this.slot === sl.id ? '.selected' : ''}${sl.excavated || busy ? '' : '.dim'}`,
          { onclick: () => pick(sl.id) },
          h('span.cls', {}, `${sl.row + 1}-${sl.col + 1}`),
          h('b', {}, state),
          tag,
        );
      });
    this.viewEl.append(
      this.panel(
        'fac-left',
        'left',
        h('div.x-head', { title: 'Pulsa una sala aquí o en el Bastión para excavarla o construir en ella. Las obras se hacen de una en una.' }, 'Salas'),
        h('div.x-list', {}, ...rooms),
        h('div.x-head', {}, 'Efectos'),
        ...lines.map(([icon, text]) => h('div.x-effect', {}, h(`span.agenda-icon.${icon}`), h('span', {}, text))),
      ),
    );
    const slot = c.base.slots.find((s) => s.id === this.slot);
    if (!slot) return;
    const busy = work?.slot === slot.id;
    const content: (HTMLElement | null)[] = [h('div.alert-kicker', {}, `SALA ${slot.row + 1}-${slot.col + 1}`)];
    if (busy && work) {
      content.push(h('h2.x-title', {}, work.kind === 'excavate' ? 'Excavando' : FACILITIES[work.facility!].name), this.bar(work.total - work.daysLeft, work.total), h('p.x-muted', {}, `Faltan ${work.daysLeft} días`));
    } else if (!slot.excavated) {
      const { cost, days } = excavationCost(slot.row);
      const above = c.base.slots.find((s) => s.col === slot.col && s.row === slot.row - 1);
      const blocked = this.why(
        [!!above && !above.excavated, 'Primero hay que excavar la sala de encima'],
        [!!work, 'Ya hay una obra en marcha'],
        [!!c.proposal, 'Hay una propuesta pendiente'],
        [!canAfford(c, cost), this.shortfall(cost)],
      );
      content.push(
        h('h2.x-title', {}, 'Roca'),
        h('p.x-chips', {}, this.chip(costText(cost)), this.chip(`${days} días`)),
        h('button.x-btn.primary', { disabled: !!blocked, onclick: () => this.send({ type: 'excavate', slot: slot.id }) }, this.decide('excavar')),
        this.whyEl(blocked),
      );
    } else if (slot.facility) {
      const def = FACILITIES[slot.facility];
      content.push(h('h2.x-title', {}, def.name), h('p.x-good', {}, 'En servicio'), h('p.x-text', {}, def.description));
    } else {
      content.push(h('h2.x-title', {}, 'Sala vacía'), work ? h('p.x-why', {}, 'Hay otra obra en marcha') : null);
      for (const def of Object.values(FACILITIES)) {
        const blocker = buildable(c, slot, def.id);
        // The ongoing work is already explained above the list.
        const why = blocker ? '' : this.why([!!c.proposal, 'Hay una propuesta pendiente'], [!canAfford(c, def.cost), this.shortfall(def.cost)]);
        content.push(
          h(
            `div.x-row.build${blocker ? '.dim' : ''}`,
            { title: def.description },
            h(
              'div',
              {},
              h('b', {}, def.name),
              h('small', {}, def.description),
              blocker ? h('small.x-warn', {}, blocker) : h('p.x-chips', {}, this.chip(costText(def.cost)), this.chip(`${def.days} d`)),
              why && !work ? h('small.x-why', {}, why) : null,
            ),
            blocker
              ? null
              : h('button.x-btn', { disabled: !!work || !!why, onclick: () => this.send({ type: 'build', slot: slot.id, facility: def.id }) }, this.decide('construir')),
          ),
        );
      }
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
    this.bastion.setLineup(Array.from({ length: max }, (_, i) => (squad[i] ? { template: squad[i]!.cls, color: this.playerColor(squad[i]!.owner), appearance: squad[i]!.appearance, gear: this.gearFor(c, squad[i]!) } : null)));
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
        h('div.x-head', { title: 'Pulsa un soldado para meterlo o sacarlo. El equipo se cambia en el Cuartel.' }, `Tus soldados · ${myCount}/${limit}`),
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
              h('span.cls', { title: TEMPLATES[s.cls].name }, TEMPLATES[s.cls].short),
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
    this.viewEl.append(
      h(
        'div.strat-center-bottom',
        { style: { bottom: '24px' } },
        h(
          `button.x-btn.big${ready ? '.ready' : ''}${partnerReady ? '.waiting' : ''}`,
          { disabled: !c.squad.length, onclick: () => this.send({ type: 'launch', ready: !ready }) },
          ready ? 'LISTOS PARA DESPEGAR' : 'LANZAR MISIÓN',
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
    const entries = Array.from({ length: max }, (_, i) => squad[i] ?? null);
    entries.forEach((s, i) => {
      const card = s
        ? h(
            'div.lineup-card',
            { style: { '--accent': this.playerColor(s.owner) } },
            h('b', {}, displayName(s.name, s.nickname)),
            h('small', {}, `${TEMPLATES[s.cls].name} · ${RANKS[s.rank]!.name}`),
            h('small', {}, s.owner === this.mySlot ? 'Tuyo' : `De ${this.playerName(s.owner)}`),
            s.owner === this.mySlot ? h('button.x-btn', { onclick: () => this.send({ type: 'squad', soldier: s.id, on: false }) }, 'Quitar') : null,
          )
        : h('div.lineup-card.empty', { title: myCount < limit ? 'Elige un soldado a la izquierda' : '' }, h('b', {}, 'Libre'));
      card.dataset.pad = String(i);
      cards.append(card);
    });
    this.viewEl.append(cards);
    this.anchorCards(cards, max);
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

  private renderDebrief(c: CampaignState): void {
    const report = c.lastReport;
    if (!report) {
      void this.go('hub');
      return;
    }
    const entries: LineupEntry[] = report.squad.map((e) => {
      const s = c.soldiers.find((x) => x.id === e.id);
      return { template: e.cls, color: this.playerColor(e.owner), status: e.status, appearance: s?.appearance, gear: s ? this.gearFor(c, s) : undefined };
    });
    this.bastion.setLineup(entries);
    const r = report.reward;
    const rewards = r?.credits ? `${r.credits} créditos` : '';
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
          report.victory && report.story ? h('p.x-text', {}, STORY[report.story - 1]!.after) : null,
          rewards ? h('p.x-text', {}, `Recompensa: ${rewards}`) : null,
          report.doomChange ? h(`p.x-text.${report.doomChange > 0 ? 'x-bad' : 'x-good'}`, {}, doomChangeText(report.levelTo, report.doomChange)) : null,
          report.bodies ? h('p.x-text.x-good', {}, `La morgue del Consejo compra ${report.bodies} cuerpos · ${report.salvage} créditos`) : null,
          report.xp ? h('p.x-text', {}, `Experiencia del Bastión: +${report.xp}`) : null,
          report.levelTo > report.levelFrom
            ? h('p.x-text.x-good', {}, `¡El Bastión sube al nivel ${report.levelTo}! ${Array.from({ length: report.levelTo - report.levelFrom }, (_, i) => this.unlocksText(report.levelFrom + i + 1)).join(' ')}`)
            : null,
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
        e.promotedTo !== null ? h('small.promo', {}, `▲ ${RANKS[e.promotedTo]!.name.toUpperCase()}`) : null,
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
    if (this.leaving && !c.outcome) {
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
      if (s) content = this.promotionPanel(s);
    }
    this.modal.classList.toggle('show', !!content);
    if (content) this.modal.append(content);
  }
}
