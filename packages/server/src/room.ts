import { randomUUID } from 'node:crypto';
import { basename } from 'node:path';
import {
  answerAbandon,
  callAbandon,
  activeOffer,
  offerMap,
  siegePlan,
  addPlayer,
  applyMissionResult,
  bastionLevel,
  buildSquad,
  campaignHackBonus,
  campaignMessageSchema,
  clientMessageSchema,
  createCampaign,
  openingMission,
  createMission,
  defaultSquad,
  executeCampaignCommand,
  executeCommand,
  hasAdjacency,
  ALARM_NAMES,
  hasQuarantine,
  medicineCures,
  missionInfo,
  offerBriefing,
  PLAYER_COLORS,
  Rng,
  syncPads,
  syncTurn,
  type BaseView,
  type CampaignMessage,
  type CampaignState,
  type ClientMessage,
  type DeadlineKind,
  type GameState,
  type HostAddress,
  type MapSource,
  type MissionKind,
  type PlayerInfo,
  type RoomPhase,
  type ServerMessage,
  type SkirmishMap,
  type Slot,
} from '@bastion/engine';
import type { WebSocket } from 'ws';
import type { SaveStore } from './persistence';

interface Seat {
  slot: Slot;
  name: string;
  token: string;
  socket: WebSocket | null;
  /** When the socket dropped; null while connected. */
  droppedAt: number | null;
  /** The base screen this player is on, so a reconnecting partner sees it. */
  view?: BaseView;
  /** Their game is out of sight (the `away` message): a drop then waits AWAY_GRACE_MS. */
  away?: boolean;
}

export type RoomMode = 'skirmish' | 'campaign';

/** A dropped player keeps blocking the end of turn this long, so a reload or Wi-Fi blip never costs them their turn. */
const RECONNECT_GRACE_MS = 30_000;
/** The same for a player whose game was out of sight when it dropped (a laptop put to sleep): back, the turn and the command are still theirs. */
const AWAY_GRACE_MS = 5 * 60_000;
/** How long a joint decision waits for the partner's answer before it goes ahead (playtest 2026-10-14, #28: in case they left; 16/10: 90 s was too long). */
const ANSWER_WAIT_MS = 30_000;

type AnyMessage = ClientMessage | CampaignMessage;

const sameName = (a: string, b: string): boolean => a.trim().toLocaleLowerCase('es') === b.trim().toLocaleLowerCase('es');

/**
 * A single co-op session for two seats. The room owns the authoritative state:
 * the campaign (base) and the tactical mission in progress. Clients only send
 * intents; tactical results stream as events, the campaign is sent whole.
 */
export class Room {
  private seats: [Seat | null, Seat | null] = [null, null];
  /** The waits on joint decisions now running (ANSWER_WAIT_MS): what each waits on, and when it ends. */
  private waits: Record<DeadlineKind, { key: string; at: number; since: number; timer: ReturnType<typeof setTimeout> } | null> = { proposal: null, evac: null, abandon: null };
  private phase: RoomPhase = 'lobby';
  private mode: RoomMode = 'skirmish';
  private state: GameState | null = null;
  private campaign: CampaignState | null = null;
  /** Whether the finished campaign mission has already been folded into the campaign. */
  private resultApplied = false;
  private rng = new Rng(Date.now() >>> 0);
  private missionKind: MissionKind = 'elimination';
  private skirmishMap: SkirmishMap = 'city';
  private skirmishVeterans = true;
  private skirmishNight = false;

  constructor(
    private readonly store: SaveStore,
    private readonly hosts: HostAddress[] = [],
    /** The game's https port (index.ts), offered to the partner; null when there is none. */
    private readonly httpsPort: number | null = null,
  ) {
    const saved = store.load();
    if (!saved) return;
    this.seats = [0, 1].map((i) => {
      const s = saved.seats[i];
      return s ? { ...s, socket: null, droppedAt: null } : null;
    }) as [Seat | null, Seat | null];
    this.phase = saved.phase;
    this.mode = saved.mode;
    this.state = saved.state;
    this.campaign = saved.campaign;
    this.resultApplied = saved.resultApplied;
    this.rng = new Rng(saved.rng);
    this.missionKind = saved.missionKind;
    this.skirmishMap = saved.skirmishMap;
    this.skirmishVeterans = saved.skirmishVeterans ?? true;
    this.skirmishNight = saved.skirmishNight ?? false;
    console.log(`[room] Partida restaurada (${this.phase}${this.campaign ? `, campaña día ${this.campaign.day}` : ''}).`);
  }

  // ---------------------------------------------------------- connections

  connect(socket: WebSocket): void {
    let seat: Seat | null = null;

    socket.on('message', (raw) => {
      let msg: AnyMessage;
      try {
        const json: unknown = JSON.parse(String(raw));
        const parsed = clientMessageSchema.safeParse(json);
        const campaign = parsed.success ? null : campaignMessageSchema.safeParse(json);
        if (parsed.success) msg = parsed.data;
        else if (campaign?.success) msg = campaign.data;
        else return this.send(socket, { t: 'error', message: 'Mensaje no válido.' });
      } catch {
        return this.send(socket, { t: 'error', message: 'Mensaje no válido.' });
      }

      if (msg.t === 'hello') {
        seat = this.join(socket, msg.name, msg.token);
        return;
      }
      if (!seat || seat.socket !== socket) return this.send(socket, { t: 'error', message: 'Identifícate primero.' });
      this.handle(seat, msg);
    });

    socket.on('close', () => {
      if (!seat || seat.socket !== socket) return;
      seat.socket = null;
      seat.droppedAt = Date.now();
      console.log(`[room] ${seat.name} se ha desconectado.`);
      this.broadcastRoom();
      setTimeout(() => this.resyncTurn(), (seat.away ? AWAY_GRACE_MS : RECONNECT_GRACE_MS) + 100);
    });
  }

  private join(socket: WebSocket, name: string, token?: string): Seat | null {
    // By the seat's token; from a browser or an app without it (another computer, the app after Chrome, a
    // campaign carried over from another host), by the player's name, so each finds their own seat and soldiers.
    let seat = this.seats.find((s) => s && token && s.token === token) ?? this.seats.find((s) => s && sameName(s.name, name)) ?? null;
    if (!seat) {
      const free = this.seats.findIndex((s) => s === null);
      if (free >= 0) {
        seat = { slot: free as Slot, name, token: randomUUID(), socket: null, droppedAt: null };
        this.seats[free] = seat;
      } else {
        // Both seats taken: allow taking over a seat whose player is offline.
        seat = this.seats.find((s) => s && !s.socket) ?? null;
        if (!seat) {
          this.send(socket, { t: 'error', message: 'La partida ya tiene dos jugadores.' });
          socket.close();
          return null;
        }
        seat.token = randomUUID();
      }
    }

    if (seat.socket && seat.socket !== socket) seat.socket.close(4000, 'Sesión abierta en otra ventana');
    seat.socket = socket;
    seat.droppedAt = null;
    seat.away = false;
    seat.name = name;
    console.log(`[room] ${name} ocupa el puesto ${seat.slot + 1}.`);

    this.send(socket, { t: 'welcome', token: seat.token, slot: seat.slot, hosts: this.hosts, httpsPort: this.httpsPort ?? undefined });
    // A partner joining a campaign started without them gets soldiers of their own.
    if (this.phase === 'base') this.bringPlayersIn();
    this.broadcastRoom();
    if (this.campaign && this.phase !== 'lobby') this.send(socket, { t: 'campaign', state: this.campaign });
    for (const other of this.seats) if (other && other !== seat && other.view) this.send(socket, { t: 'baseView', slot: other.slot, view: other.view });
    if (this.phase === 'mission' && this.state) this.send(socket, { t: 'snapshot', state: this.state });
    this.persist();
    this.resyncTurn();
    // Back in: how long the open decisions have left.
    for (const kind of ['proposal', 'evac'] as const) {
      const wait = this.waits[kind];
      if (wait) this.send(socket, { t: 'deadline', kind, ms: Math.max(0, wait.at - Date.now()) });
    }
    return seat;
  }

  // ------------------------------------------------------------- messages

  private handle(seat: Seat, msg: Exclude<AnyMessage, { t: 'hello' }>): void {
    switch (msg.t) {
      // ---- lobby and skirmish
      case 'start':
        if (this.phase === 'lobby') {
          if (msg.veterans !== undefined) this.skirmishVeterans = msg.veterans;
          if (msg.night !== undefined) this.skirmishNight = msg.night;
          this.startSkirmish(msg.mission, msg.map);
        }
        break;
      case 'newMission':
        if (this.phase !== 'mission' || this.mode !== 'skirmish' || !this.state?.outcome) return;
        if (msg.night !== undefined) this.skirmishNight = msg.night;
        this.startSkirmish(msg.mission, msg.map);
        break;

      // ---- campaign
      case 'newCampaign':
        if (this.phase !== 'lobby') return;
        this.campaign = createCampaign(this.connectedSlots(), this.rng);
        // It opens with the first operation chosen and its squad in the hangar (playtest 2026-10-13).
        openingMission(this.campaign);
        console.log(`[room] Nueva campaña con ${this.campaign.players.length} jugador(es).`);
        this.enterBase();
        break;
      case 'continueCampaign':
        if (this.phase === 'lobby' && this.campaign) this.enterBase();
        break;
      case 'leaveCampaign':
      case 'backToMenu': {
        const skirmishOver = this.phase === 'mission' && this.mode === 'skirmish' && !!this.state?.outcome;
        if (this.phase !== 'base' && !skirmishOver) return;
        if (skirmishOver) this.state = null;
        this.phase = 'lobby';
        this.broadcastRoom();
        this.persist();
        break;
      }
      case 'ccmd': {
        if (this.phase !== 'base' || !this.campaign) return;
        const result = executeCampaignCommand(this.campaign, msg.cmd, { slot: seat.slot, connected: this.connectedSlots(), rng: this.rng });
        if (!result.ok) {
          this.send(seat.socket!, { t: 'rejected', message: result.error });
          return;
        }
        if (result.launch) this.launchCampaignMission();
        else this.broadcast({ t: 'campaign', state: this.campaign });
        this.persist();
        this.checkWaits();
        break;
      }
      case 'returnToBase':
        if (this.phase !== 'mission' || this.mode !== 'campaign' || !this.state?.outcome) return;
        this.state = null;
        this.enterBase();
        break;
      case 'baseView':
        seat.view = msg.view;
        this.toPartner(seat, { t: 'baseView', slot: seat.slot, view: msg.view });
        break;
      case 'baseCursor':
        // What the partner has in hand on the base, passed on as it comes (docs/FASE12.md §11.7).
        if (this.phase === 'base') this.toPartner(seat, { t: 'baseCursor', slot: seat.slot, piece: msg.piece, x: msg.x, y: msg.y, rot: msg.rot });
        break;
      case 'away': {
        // The partner hears of a change; whoever asked always gets an answer, its proof the socket is alive (net.ts).
        const changed = !!seat.away !== msg.away;
        seat.away = msg.away;
        if (changed) this.broadcastRoom();
        else this.send(seat.socket!, this.roomMessage());
        break;
      }

      // ---- tactical
      case 'abandon': {
        // Asked of the partner first, if here (playtest 16/10); alone, it ends now.
        if (this.phase !== 'mission' || !this.state) return;
        const events = callAbandon(this.state, seat.slot, this.activeSlots());
        if (!events.length) return;
        console.log(`[room] ${seat.name} ${this.state.outcome ? 'abandona la misión' : 'pide abandonar la misión'}.`);
        this.broadcast({ t: 'events', events, seq: this.state.seq, by: seat.slot });
        this.afterTactical();
        this.checkWaits();
        break;
      }
      case 'abandonAnswer': {
        if (this.phase !== 'mission' || !this.state) return;
        const events = answerAbandon(this.state, seat.slot, msg.accept);
        if (!events.length) return;
        console.log(`[room] ${seat.name} ${msg.accept ? 'acepta abandonar la misión' : 'no quiere abandonar la misión'}.`);
        this.broadcast({ t: 'events', events, seq: this.state.seq, by: seat.slot });
        this.afterTactical();
        this.checkWaits();
        break;
      }
      case 'ping':
        this.broadcast({ t: 'ping', slot: seat.slot, tile: msg.tile, kind: msg.kind });
        break;
      case 'sync':
        if (this.phase === 'mission' && this.state) this.send(seat.socket!, { t: 'snapshot', state: this.state });
        break;
      case 'cmd': {
        if (this.phase !== 'mission' || !this.state) return;
        const result = executeCommand(this.state, msg.cmd, { slot: seat.slot, connected: this.activeSlots(), rng: this.rng });
        if (!result.ok) {
          this.send(seat.socket!, { t: 'rejected', message: result.error });
          return;
        }
        this.broadcast({ t: 'events', events: result.events, seq: this.state.seq, by: seat.slot });
        this.afterTactical();
        this.checkWaits();
        break;
      }
      case 'presence':
        this.toPartner(seat, { t: 'presence', slot: seat.slot, unit: msg.unit, tile: msg.tile });
        break;
      case 'plan': {
        // Only with one's own soldiers, and only in a mission.
        const unit = this.phase === 'mission' ? this.state?.units.find((u) => u.id === msg.unit) : undefined;
        if (!unit || unit.team !== 'xcom' || (unit.owner !== null && unit.owner !== seat.slot)) return;
        this.toPartner(seat, { t: 'plan', slot: seat.slot, unit: msg.unit, tile: msg.tile });
        break;
      }
    }
  }

  private startSkirmish(kind: MissionKind = this.missionKind, map: SkirmishMap = this.skirmishMap): void {
    const slots = this.connectedSlots();
    if (!slots.length) return;
    // A Saqueo is always in town: houses to search (the lobby only offers the city for it).
    if (kind === 'scavenge' && map !== 'industrial') map = 'city';
    this.missionKind = kind;
    this.skirmishMap = map;
    this.mode = 'skirmish';
    const seed = this.rng.int(2 ** 31);
    const source: MapSource =
      map === 'plaza' || map === 'torre' || map === 'bastion'
        ? { kind: 'handmade', id: map }
        : map === 'roadside'
          ? { kind: 'generated', biome: 'wilds', seed, site: 'roadside' }
          : map === 'industrial'
            ? { kind: 'generated', biome: 'city', seed, site: 'industrial' }
            : { kind: 'generated', biome: map, seed };
    this.state = createMission({ map: source, slots, rng: this.rng, kind, veterans: this.skirmishVeterans, night: this.skirmishNight });
    this.phase = 'mission';
    console.log(`[room] Escaramuza (${kind}${this.skirmishNight ? ', de noche' : ''}, ${this.state.mapName}) con ${slots.length} jugador(es).`);
    this.broadcastRoom();
    this.broadcast({ t: 'snapshot', state: this.state });
    this.persist();
  }

  /** Every connected player takes part in the campaign (one who arrives late gets soldiers). */
  private bringPlayersIn(): void {
    const c = this.campaign;
    if (!c) return;
    let changed = false;
    for (const slot of this.connectedSlots()) if (addPlayer(c, slot, this.rng)) changed = true;
    if (!changed) return;
    // A squad being picked is redone with both commanders' soldiers.
    if (c.activeMission) {
      c.squad = defaultSquad(c, this.connectedSlots());
      syncPads(c);
      c.launchReady = [false, false];
    }
    this.broadcast({ t: 'campaign', state: c });
    this.persist();
  }

  private enterBase(): void {
    this.bringPlayersIn();
    this.phase = 'base';
    this.mode = 'campaign';
    this.broadcastRoom();
    if (this.campaign) this.broadcast({ t: 'campaign', state: this.campaign });
    this.persist();
  }

  private launchCampaignMission(): void {
    const c = this.campaign!;
    const offer = activeOffer(c);
    if (!offer) return;
    const info = missionInfo(c, offer);
    const siege = siegePlan(c, offer);
    this.state = createMission({
      // A siege is fought on the base as it stands now (docs/FASE12.md §11.5).
      map: offerMap(c, offer),
      ...(siege ? { siegePlan: siege } : {}),
      slots: c.players,
      rng: this.rng,
      kind: offer.kind,
      night: offer.night,
      living: offer.living,
      squad: buildSquad(c),
      pods: offer.pods,
      difficulty: offer.difficulty,
      hackBonus: campaignHackBonus(c),
      campaignMission: { id: offer.id, name: offer.name, ...(offer.story ? { story: offer.story } : {}), ...(info.kicker ? { kicker: info.kicker } : {}), ...(info.item ? { item: info.item } : {}), briefing: offerBriefing(c, offer) },
      vipName: info.vipName,
      ...(info.alarm ? { alarm: info.alarm, objectiveName: ALARM_NAMES[info.alarm] } : {}),
      leader: info.leader,
      itemOutdoors: info.itemOutdoors,
      medicineCures: medicineCures(c),
      quarantine: hasQuarantine(c),
      snow: info.snow,
    });
    // The Taller de oficios wall to wall with the listening post: the Oficio makes less noise.
    if (hasAdjacency(c, 'echoes')) this.state.quietSongs = true;
    this.resultApplied = false;
    this.phase = 'mission';
    console.log(`[room] Misión de campaña: ${offer.name}.`);
    this.broadcastRoom();
    this.broadcast({ t: 'campaign', state: c });
    this.broadcast({ t: 'snapshot', state: this.state });
  }

  /** After any tactical change: fold a finished campaign mission into the campaign once. */
  private afterTactical(): void {
    if (this.mode === 'campaign' && this.campaign && this.state?.outcome && !this.resultApplied) {
      applyMissionResult(this.campaign, this.state, this.rng);
      this.resultApplied = true;
      this.broadcast({ t: 'campaign', state: this.campaign });
    }
    this.persist();
  }

  /** A disconnect may be the last thing blocking the end of the XCOM turn. */
  private resyncTurn(): void {
    if (this.phase !== 'mission' || !this.state || !this.connectedSlots().length) return;
    const events = syncTurn(this.state, { connected: this.activeSlots(), rng: this.rng });
    if (events.length) {
      this.broadcast({ t: 'events', events, seq: this.state.seq, by: null });
      this.afterTactical();
    }
    this.checkWaits();
  }

  /**
   * Starts, keeps or drops the waits on joint decisions (playtest 2026-10-14,
   * #28): a base proposal, a call to evacuate. A new one starts its
   * ANSWER_WAIT_MS and both clients hear how long it has; when it is answered
   * or gone, they hear it is over.
   */
  private checkWaits(): void {
    const proposal = this.phase === 'base' ? this.campaign?.proposal : null;
    this.watch('proposal', proposal ? JSON.stringify(proposal) : null);
    const call = this.phase === 'mission' && this.state && !this.state.outcome ? this.state.evacCall : undefined;
    this.watch('evac', call && this.state ? `${this.state.turn}:${call.by}:${call.unit}` : null);
    const leave = this.phase === 'mission' && this.state && !this.state.outcome ? this.state.abandonCall : undefined;
    this.watch('abandon', leave ? `by:${leave.by}` : null);
  }

  private watch(kind: DeadlineKind, key: string | null): void {
    const wait = this.waits[kind];
    if (wait?.key === key) return;
    if (wait) {
      clearTimeout(wait.timer);
      this.waits[kind] = null;
      if (!key) this.broadcast({ t: 'deadline', kind, ms: null });
    }
    if (!key) return;
    const timer = setTimeout(() => this.answerForPartner(kind, key), ANSWER_WAIT_MS);
    this.waits[kind] = { key, at: Date.now() + ANSWER_WAIT_MS, since: Date.now(), timer };
    this.broadcast({ t: 'deadline', kind, ms: ANSWER_WAIT_MS });
  }

  /** Who is to answer a question of `kind`: the one who didn't ask. */
  private answerer(kind: DeadlineKind): Slot | null {
    const by = kind === 'proposal' ? this.campaign?.proposal?.by : kind === 'evac' ? this.state?.evacCall?.by : this.state?.abandonCall?.by;
    return by === undefined ? null : by === 0 ? 1 : 0;
  }

  /** The wait ran out: the decision goes ahead, as if the partner had said yes. */
  private answerForPartner(kind: DeadlineKind, key: string): void {
    const wait = this.waits[kind];
    if (wait?.key !== key) return;
    // Whoever is to answer has the window minimized or the laptop asleep: the question waits for them, up to AWAY_GRACE_MS.
    const answerer = this.answerer(kind);
    const away = answerer !== null && this.seats[answerer]?.away;
    if (away && Date.now() - wait.since < AWAY_GRACE_MS) {
      const left = Math.max(1000, wait.since + AWAY_GRACE_MS - Date.now());
      wait.at = Date.now() + left;
      wait.timer = setTimeout(() => this.answerForPartner(kind, key), left);
      this.broadcast({ t: 'deadline', kind, ms: left });
      return;
    }
    this.waits[kind] = null;
    if (kind === 'proposal' && this.campaign?.proposal && this.phase === 'base') {
      const partner: Slot = this.campaign.proposal.by === 0 ? 1 : 0;
      const result = executeCampaignCommand(this.campaign, { type: 'vote', accept: true }, { slot: partner, connected: [0, 1], rng: this.rng });
      console.log(`[room] Propuesta sin respuesta: sigue adelante${result.ok ? '' : ` (${result.error})`}.`);
      if (result.ok && result.launch) this.launchCampaignMission();
      else this.broadcast({ t: 'campaign', state: this.campaign });
      this.persist();
    }
    if (kind === 'evac' && this.state?.evacCall && this.phase === 'mission') {
      const partner: Slot = this.state.evacCall.by === 0 ? 1 : 0;
      const result = executeCommand(this.state, { type: 'evacAnswer', accept: true }, { slot: partner, connected: this.activeSlots(), rng: this.rng });
      console.log(`[room] Evacuación sin respuesta: sale el autobús${result.ok ? '' : ` (${result.error})`}.`);
      if (result.ok) {
        this.broadcast({ t: 'events', events: result.events, seq: this.state.seq, by: null });
        this.afterTactical();
      }
    }
    if (kind === 'abandon' && this.state?.abandonCall && this.phase === 'mission') {
      const partner: Slot = this.state.abandonCall.by === 0 ? 1 : 0;
      const events = answerAbandon(this.state, partner, true);
      console.log('[room] Abandono sin respuesta: se abandona la misión.');
      if (events.length) {
        this.broadcast({ t: 'events', events, seq: this.state.seq, by: null });
        this.afterTactical();
      }
    }
    this.broadcast({ t: 'deadline', kind, ms: null });
    this.checkWaits();
  }

  // -------------------------------------------------------------- helpers

  private connectedSlots(): Slot[] {
    return this.seats.filter((s): s is Seat => !!s?.socket).map((s) => s.slot);
  }

  /** Seats that still count for the end of turn: connected, or dropped less than the grace period ago. */
  private activeSlots(): Slot[] {
    const now = Date.now();
    return this.seats
      .filter((s): s is Seat => !!s && (!!s.socket || (s.droppedAt !== null && now - s.droppedAt < (s.away ? AWAY_GRACE_MS : RECONNECT_GRACE_MS))))
      .map((s) => s.slot);
  }

  /** The server is shutting down (index.ts): the game is saved, then everyone hears it, so the partner stops waiting. */
  closing(): void {
    this.persist();
    this.broadcast({ t: 'closing' });
  }

  /** Who is playing and where, for the app's announcement on the local network (index.ts). */
  summary(): { players: string[]; phase: RoomPhase } {
    return { players: this.players().filter((p) => p.connected).map((p) => p.name), phase: this.phase };
  }

  private players(): PlayerInfo[] {
    return this.seats
      .filter((s): s is Seat => s !== null)
      .map((s) => ({ slot: s.slot, name: s.name, color: PLAYER_COLORS[s.slot], connected: !!s.socket, ...(s.away ? { away: true } : {}) }));
  }

  private broadcastRoom(): void {
    this.broadcast(this.roomMessage());
  }

  private roomMessage(): ServerMessage {
    const savedCampaign = this.campaign && !this.campaign.outcome ? { day: this.campaign.day, missions: this.campaign.missionsFlown ?? 0, doom: this.campaign.doom, level: bastionLevel(this.campaign) } : null;
    // A campaign from before the 0.3 kept aside (docs/FASE9.md §7): its file's name, for the lobby to say so.
    const setAside = this.store.setAside ? basename(this.store.setAside) : null;
    return { t: 'room', players: this.players(), phase: this.phase, savedCampaign, ...(setAside ? { setAside } : {}) };
  }

  private broadcast(msg: ServerMessage): void {
    const data = JSON.stringify(msg);
    for (const s of this.seats) if (s?.socket) s.socket.send(data);
  }

  private toPartner(seat: Seat, msg: ServerMessage): void {
    for (const other of this.seats) if (other && other !== seat && other.socket) this.send(other.socket, msg);
  }

  private send(socket: WebSocket, msg: ServerMessage): void {
    socket.send(JSON.stringify(msg));
  }

  private persist(): void {
    this.store.save({
      seats: this.seats.map((s) => (s ? { slot: s.slot, name: s.name, token: s.token } : null)),
      phase: this.phase,
      mode: this.mode,
      state: this.state,
      campaign: this.campaign,
      resultApplied: this.resultApplied,
      rng: this.rng.state,
      missionKind: this.missionKind,
      skirmishMap: this.skirmishMap,
      skirmishVeterans: this.skirmishVeterans,
      skirmishNight: this.skirmishNight,
    });
  }
}
