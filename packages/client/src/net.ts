import { HOST_CLOSED, hostClosedLine, QUIT_TEXT, REJOIN_LINE, type CampaignMessage, type ClientMessage, type HostAddress, type PlayerInfo, type ServerMessage, type Slot } from '@bastion/engine';
import { desktop } from './desktop';
import { post } from './report';
import { h } from './ui/dom';

export type NetStatus = 'connecting' | 'open' | 'closed';

/**
 * `?perfil=b` keeps a separate identity per profile, so two tabs of the same
 * browser can play as two different players (handy for local testing).
 */
export function storageKey(base: string): string {
  const profile = new URLSearchParams(location.search).get('perfil');
  return profile ? `bastion.${base}.${profile}` : `bastion.${base}`;
}

const TOKEN_KEY = storageKey('token');

/** The host's network addresses, from the server's welcome. */
let hosts: HostAddress[] = [];
/** Where the server also serves the game over https (server/src/tls.ts). */
let httpsPort: number | undefined;

export function setHosts(list: HostAddress[], https?: number): void {
  hosts = list;
  httpsPort = https;
}

export interface InviteLink {
  label: string;
  url: string;
  /** Folded away in the menu: another address on the local network, less likely the one. */
  other?: boolean;
}

/**
 * Where the partner opens the game: the server's https port when it has one
 * (Safari is fifty times slower over plain http), otherwise this page's port
 * (dev or built), on the host's network addresses. `localhost` only works on
 * this machine, so it is never offered; if the page was opened through a
 * network address, that one. The server lists its default route's address
 * first: of the local network ones, only that one goes up front.
 */
export function inviteLinks(): InviteLink[] {
  const secure = httpsPort !== undefined && !import.meta.env.DEV;
  const port = secure ? `:${httpsPort}` : location.port ? `:${location.port}` : '';
  const url = (address: string) => `${secure ? 'https:' : location.protocol}//${address}${port}/`;
  let lan = 0;
  const links: InviteLink[] = hosts.map((h) =>
    h.kind === 'tailscale' ? { label: 'Por internet (Tailscale)', url: url(h.address) } : { label: 'En tu misma red', url: url(h.address), other: lan++ > 0 },
  );
  const local = LOOPBACK.has(location.hostname);
  if (!local && !hosts.some((h) => h.address === location.hostname)) links.unshift({ label: 'Esta dirección', url: url(location.hostname) });
  return links;
}

function storage(): Storage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

/** Back to the window, how long the old socket has to show it is alive before a new one is opened. */
const PROBE_MS = 2500;

/**
 * WebSocket with automatic reconnection and seat token persistence. While
 * the window is out of sight (minimized, behind another, the laptop asleep)
 * its socket may die without a word: the server hears it went away (so the
 * turn and the command wait for it, room.ts), and back here the socket is
 * tried at once and replaced if it does not answer.
 */
export class Net {
  private ws: WebSocket | null = null;
  private name = '';
  private retry = 0;
  private retryTimer = 0;
  private stopped = false;
  /** When the page went to sleep (0 while awake), and when the server was last heard. */
  private hiddenAt = 0;
  private heardAt = 0;
  /** «Volviendo a la partida…» while the way back in is under way. */
  private rejoinNote: HTMLElement | null = null;
  /** Whether a connection ever opened: if not, the host hears about it (a browser turning the certificate down). */
  private opened = false;
  /** Whether this socket got its seat (welcome): an error before it is the hello refused. */
  private welcomed = false;
  /** Who is in the room and which seat is ours, for the partner's name when the host closes the game. */
  private players: PlayerInfo[] = [];
  private slot: Slot | null = null;
  private readonly messageHandlers = new Set<(m: ServerMessage) => void>();
  private readonly statusHandlers = new Set<(s: NetStatus) => void>();

  constructor(private readonly url: string) {
    document.addEventListener('visibilitychange', () => (document.hidden ? this.asleep() : this.awake()));
    // Back from the browser's page cache, or the network back: the same check.
    window.addEventListener('pageshow', (e) => e.persisted && this.awake());
    window.addEventListener('online', () => this.awake());
  }

  /** Asks for a seat as `name`: on the socket already open if there is one (a name refused, another tried), else on a new one. */
  connect(name: string): void {
    this.name = name;
    this.stopped = false;
    if (this.ws?.readyState === WebSocket.OPEN && !this.welcomed) this.send({ t: 'hello', name, token: storage()?.getItem(TOKEN_KEY) ?? undefined });
    else this.open();
  }

  send(msg: ClientMessage | CampaignMessage): void {
    if (this.ws?.readyState === WebSocket.OPEN) this.ws.send(JSON.stringify(msg));
  }

  onMessage(fn: (m: ServerMessage) => void): void {
    this.messageHandlers.add(fn);
  }

  onStatus(fn: (s: NetStatus) => void): void {
    this.statusHandlers.add(fn);
  }

  private asleep(): void {
    this.hiddenAt = Date.now();
    this.send({ t: 'away', away: true });
  }

  /** Awake again: the server hears it, and must answer (with `room`) soon, or the socket died asleep. */
  private awake(): void {
    const slept = this.hiddenAt ? Date.now() - this.hiddenAt : 0;
    this.hiddenAt = 0;
    if (!this.name || this.stopped || this.ws?.readyState === WebSocket.CONNECTING) return;
    if (this.ws?.readyState !== WebSocket.OPEN) return this.reconnectNow(slept > 0);
    const asked = Date.now();
    this.send({ t: 'away', away: false });
    window.setTimeout(() => {
      if (this.heardAt < asked && !this.stopped && !document.hidden) this.reconnectNow(true);
    }, PROBE_MS);
  }

  /** A new socket now, not after the backoff; the old one, if it ever speaks again, is not listened to. */
  private reconnectNow(rejoining: boolean): void {
    window.clearTimeout(this.retryTimer);
    this.retry = 0;
    if (rejoining) this.showRejoin(true);
    this.open();
  }

  private showRejoin(on: boolean): void {
    if (!on) {
      this.rejoinNote?.remove();
      this.rejoinNote = null;
    } else if (!this.rejoinNote) {
      this.rejoinNote = h('div.connection.show.rejoin-note', { role: 'status', style: { position: 'fixed', zIndex: '60' } }, REJOIN_LINE);
      document.body.append(this.rejoinNote);
    }
  }

  private open(): void {
    this.emitStatus('connecting');
    const ws = new WebSocket(this.url);
    this.ws = ws;
    this.welcomed = false;
    ws.onopen = () => {
      this.retry = 0;
      this.opened = true;
      this.emitStatus('open');
      const token = storage()?.getItem(TOKEN_KEY) ?? undefined;
      this.send({ t: 'hello', name: this.name, token });
    };
    ws.onmessage = (ev) => {
      // An old socket that woke up after it was replaced: its news are stale.
      if (this.ws !== ws) return;
      this.heardAt = Date.now();
      const msg = JSON.parse(String(ev.data)) as ServerMessage;
      if (msg.t === 'welcome') {
        storage()?.setItem(TOKEN_KEY, msg.token);
        this.slot = msg.slot;
        this.welcomed = true;
        this.showRejoin(false);
      }
      // The hello refused (a name the other player has, a full game: room.ts join): coming back on its own would
      // only be refused again. The lobby asks for another name (ui/lobby.ts error), and connect tries it.
      if (msg.t === 'error' && !this.welcomed) this.stopped = true;
      if (msg.t === 'room') this.players = msg.players;
      if (msg.t === 'closing') {
        // The host closed the game (server index.ts shutdown): there is nobody to reconnect to. The host's own
        // window is closing with it, or its server was stopped there: no note. The app knows which window hosts;
        // a browser takes this machine's address for the host's.
        this.stopped = true;
        this.showRejoin(false);
        const app = desktop();
        if (!(app ? app.hosting : LOOPBACK.has(location.hostname))) hostClosed(this.players.find((p) => p.slot !== this.slot)?.name);
      }
      for (const fn of this.messageHandlers) fn(msg);
    };
    ws.onclose = (ev) => {
      if (this.ws !== ws) return;
      this.emitStatus('closed');
      if (!this.opened && this.retry === 2) post(`el WebSocket no abre (${this.url}, código ${ev.code})`);
      // 4000: this seat was opened in another window — do not fight over it.
      if (this.stopped || ev.code === 4000) return this.showRejoin(false);
      const delay = Math.min(5000, 500 * 2 ** this.retry++);
      this.retryTimer = window.setTimeout(() => this.open(), delay);
    };
  }

  private emitStatus(s: NetStatus): void {
    for (const fn of this.statusHandlers) fn(s);
  }
}

/** This machine, as a page's address names it. */
const LOOPBACK = new Set(['localhost', '127.0.0.1', '[::1]']);

/**
 * The host closed the game: a screen over whatever was up, saying so, and the
 * ways on. Back in, once the host opens it again (until then it says it is not
 * open yet); in the app, its start screen, or out of it. Never «Conectando…»
 * for ever, nor the start screen in full screen with no way out (playtest 21/10
 * #29): there, Esc takes it out of full screen.
 */
function hostClosed(host?: string): void {
  const app = desktop();
  const note = h('p.x-text', {}, HOST_CLOSED.back);
  const retry: HTMLButtonElement = h(
    'button.x-btn.primary',
    {
      onclick: async () => {
        retry.disabled = true;
        try {
          if ((await fetch('/', { cache: 'no-store' })).ok) return location.reload();
        } catch {
          /* not open yet */
        }
        note.textContent = HOST_CLOSED.notYet;
        retry.disabled = false;
      },
    },
    HOST_CLOSED.retry,
  );
  const screen = h(
    'div.menu-modal.host-closed',
    { style: { position: 'fixed', zIndex: '1000' } },
    h(
      'div.x-panel',
      {},
      h('h2.x-title', {}, hostClosedLine(host)),
      note,
      h(
        'div.alert-actions',
        {},
        retry,
        app ? h('button.x-btn', { onclick: () => app.toLauncher() }, HOST_CLOSED.home) : null,
        app ? h('button.x-btn', { onclick: () => app.quit() }, QUIT_TEXT.button) : null,
      ),
    ),
  );
  if (app) {
    document.addEventListener(
      'keydown',
      (e) => {
        if (e.key !== 'Escape' || !screen.isConnected) return;
        // The game's own menu is behind it, and has nothing to do now.
        e.stopPropagation();
        app.leaveFullscreen();
      },
      { capture: true },
    );
  }
  document.body.append(screen);
}

export function serverUrl(): string {
  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  return `${proto}://${location.host}/ws`;
}
