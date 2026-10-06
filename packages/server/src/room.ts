import { randomUUID } from 'node:crypto';
import {
  abandonMission,
  addPlayer,
  activeOffer,
  applyMissionResult,
  buildSquad,
  campaignHackBonus,
  campaignMessageSchema,
  clientMessageSchema,
  createCampaign,
  createMission,
  defaultSquad,
  executeCampaignCommand,
  executeCommand,
  missionInfo,
  PLAYER_COLORS,
  Rng,
  syncTurn,
  type CampaignMessage,
  type CampaignState,
  type HostAddress,
  type BaseView,
  type ClientMessage,
  type GameState,
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
}

export type RoomMode = 'skirmish' | 'campaign';

/** A dropped player keeps blocking the end of turn this long, so a reload or Wi-Fi blip never costs them their turn. */
const RECONNECT_GRACE_MS = 30_000;

type AnyMessage = ClientMessage | CampaignMessage;

/**
 * A single co-op session for two seats. The room owns the authoritative state:
 * the campaign (base) and the tactical mission in progress. Clients only send
 * intents; tactical results stream as events, the campaign is sent whole.
 */
export class Room {
  private seats: [Seat | null, Seat | null] = [null, null];
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

  constructor(
    private readonly store: SaveStore,
    private readonly hosts: HostAddress[] = [],
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
      setTimeout(() => this.resyncTurn(), RECONNECT_GRACE_MS + 100);
    });
  }

  private join(socket: WebSocket, name: string, token?: string): Seat | null {
    let seat = this.seats.find((s) => s && token && s.token === token) ?? null;
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
    seat.name = name;
    console.log(`[room] ${name} ocupa el puesto ${seat.slot + 1}.`);

    this.send(socket, { t: 'welcome', token: seat.token, slot: seat.slot, hosts: this.hosts });
    // A partner joining a campaign started without them gets soldiers of their own.
    if (this.phase === 'base') this.bringPlayersIn();
    this.broadcastRoom();
    if (this.campaign && this.phase !== 'lobby') this.send(socket, { t: 'campaign', state: this.campaign });
    for (const other of this.seats) if (other && other !== seat && other.view) this.send(socket, { t: 'baseView', slot: other.slot, view: other.view });
    if (this.phase === 'mission' && this.state) this.send(socket, { t: 'snapshot', state: this.state });
    this.persist();
    this.resyncTurn();
    return seat;
  }

  // ------------------------------------------------------------- messages

  private handle(seat: Seat, msg: Exclude<AnyMessage, { t: 'hello' }>): void {
    switch (msg.t) {
      // ---- lobby and skirmish
      case 'start':
        if (this.phase === 'lobby') {
          if (msg.veterans !== undefined) this.skirmishVeterans = msg.veterans;
          this.startSkirmish(msg.mission, msg.map);
        }
        break;
      case 'newMission':
        if (this.phase === 'mission' && this.mode === 'skirmish' && this.state?.outcome) this.startSkirmish(msg.mission, msg.map);
        break;

      // ---- campaign
      case 'newCampaign':
        if (this.phase !== 'lobby') return;
        this.campaign = createCampaign(this.connectedSlots(), this.rng);
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

      // ---- tactical
      case 'abandon': {
        if (this.phase !== 'mission' || !this.state) return;
        const events = abandonMission(this.state);
        if (!events.length) return;
        console.log(`[room] ${seat.name} abandona la misión.`);
        this.broadcast({ t: 'events', events, seq: this.state.seq, by: seat.slot });
        this.afterTactical();
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
        break;
      }
      case 'presence':
        this.toPartner(seat, { t: 'presence', slot: seat.slot, unit: msg.unit, tile: msg.tile });
        break;
    }
  }

  private startSkirmish(kind: MissionKind = this.missionKind, map: SkirmishMap = this.skirmishMap): void {
    const slots = this.connectedSlots();
    if (!slots.length) return;
    this.missionKind = kind;
    this.skirmishMap = map;
    this.mode = 'skirmish';
    const source: MapSource = map === 'plaza' ? { kind: 'handmade', id: 'plaza' } : { kind: 'generated', biome: map, seed: this.rng.int(2 ** 31) };
    this.state = createMission({ map: source, slots, rng: this.rng, kind, veterans: this.skirmishVeterans });
    this.phase = 'mission';
    console.log(`[room] Escaramuza (${kind}, ${this.state.mapName}) con ${slots.length} jugador(es).`);
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
    this.state = createMission({
      map: offer.map,
      slots: c.players,
      rng: this.rng,
      kind: offer.kind,
      squad: buildSquad(c),
      pods: offer.pods,
      difficulty: offer.difficulty,
      hackBonus: campaignHackBonus(c),
      campaignMission: { id: offer.id, name: offer.name, ...(offer.story ? { story: offer.story } : {}), ...(info.kicker ? { kicker: info.kicker } : {}), ...(info.item ? { item: info.item } : {}) },
      vipName: info.vipName,
      leader: info.leader,
    });
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
  }

  // -------------------------------------------------------------- helpers

  private connectedSlots(): Slot[] {
    return this.seats.filter((s): s is Seat => !!s?.socket).map((s) => s.slot);
  }

  /** Seats that still count for the end of turn: connected, or dropped less than the grace period ago. */
  private activeSlots(): Slot[] {
    const now = Date.now();
    return this.seats
      .filter((s): s is Seat => !!s && (!!s.socket || (s.droppedAt !== null && now - s.droppedAt < RECONNECT_GRACE_MS)))
      .map((s) => s.slot);
  }

  private players(): PlayerInfo[] {
    return this.seats
      .filter((s): s is Seat => s !== null)
      .map((s) => ({ slot: s.slot, name: s.name, color: PLAYER_COLORS[s.slot], connected: !!s.socket }));
  }

  private broadcastRoom(): void {
    const savedCampaign = this.campaign && !this.campaign.outcome ? { day: this.campaign.day, doom: this.campaign.doom } : null;
    this.broadcast({ t: 'room', players: this.players(), phase: this.phase, savedCampaign });
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
    });
  }
}
