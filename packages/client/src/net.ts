import { HOST_CLOSED, hostClosedLine, type CampaignMessage, type ClientMessage, type HostAddress, type PlayerInfo, type ServerMessage, type Slot } from '@bastion/engine';
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

/** WebSocket with automatic reconnection and seat token persistence. */
export class Net {
  private ws: WebSocket | null = null;
  private name = '';
  private retry = 0;
  private stopped = false;
  /** Whether a connection ever opened: if not, the host hears about it (a browser turning the certificate down). */
  private opened = false;
  /** Who is in the room and which seat is ours, for the partner's name when the host closes the game. */
  private players: PlayerInfo[] = [];
  private slot: Slot | null = null;
  private readonly messageHandlers = new Set<(m: ServerMessage) => void>();
  private readonly statusHandlers = new Set<(s: NetStatus) => void>();

  constructor(private readonly url: string) {}

  connect(name: string): void {
    this.name = name;
    this.stopped = false;
    this.open();
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

  private open(): void {
    this.emitStatus('connecting');
    const ws = new WebSocket(this.url);
    this.ws = ws;
    ws.onopen = () => {
      this.retry = 0;
      this.opened = true;
      this.emitStatus('open');
      const token = storage()?.getItem(TOKEN_KEY) ?? undefined;
      this.send({ t: 'hello', name: this.name, token });
    };
    ws.onmessage = (ev) => {
      const msg = JSON.parse(String(ev.data)) as ServerMessage;
      if (msg.t === 'welcome') {
        storage()?.setItem(TOKEN_KEY, msg.token);
        this.slot = msg.slot;
      }
      if (msg.t === 'room') this.players = msg.players;
      if (msg.t === 'closing') {
        // The host closed the game (server index.ts shutdown): there is nobody to reconnect to. The host's own
        // window (on this machine, by localhost) is closing with it, or its server was stopped there: no note.
        this.stopped = true;
        if (!LOOPBACK.has(location.hostname)) hostClosed(this.players.find((p) => p.slot !== this.slot)?.name);
      }
      for (const fn of this.messageHandlers) fn(msg);
    };
    ws.onclose = (ev) => {
      if (this.ws !== ws) return;
      this.emitStatus('closed');
      if (!this.opened && this.retry === 2) post(`el WebSocket no abre (${this.url}, código ${ev.code})`);
      // 4000: this seat was opened in another window — do not fight over it.
      if (this.stopped || ev.code === 4000) return;
      const delay = Math.min(5000, 500 * 2 ** this.retry++);
      setTimeout(() => this.open(), delay);
    };
  }

  private emitStatus(s: NetStatus): void {
    for (const fn of this.statusHandlers) fn(s);
  }
}

/** This machine, as a page's address names it. */
const LOOPBACK = new Set(['localhost', '127.0.0.1', '[::1]']);

/**
 * The host closed the game: in the desktop app, back to its start screen saying
 * so (where the game shows up again once reopened); in a browser, the note over
 * whatever was on screen and a way back in. Never «Conectando…» for ever.
 */
function hostClosed(host?: string): void {
  const app = desktop();
  // Lore's words (engine lore.ts).
  if (app) return app.toLauncher(`${hostClosedLine(host)} ${HOST_CLOSED.back}`);
  document.body.append(
    h(
      'div.menu-modal',
      { style: { position: 'fixed', zIndex: '1000' } },
      h(
        'div.x-panel',
        {},
        h('h2.x-title', {}, hostClosedLine(host)),
        h('p.x-text', {}, HOST_CLOSED.back),
        h('div.alert-actions', {}, h('button.x-btn.primary', { onclick: () => location.reload() }, HOST_CLOSED.retry)),
      ),
    ),
  );
}

export function serverUrl(): string {
  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  return `${proto}://${location.host}/ws`;
}
