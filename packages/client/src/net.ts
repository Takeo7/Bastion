import type { CampaignMessage, ClientMessage, HostAddress, ServerMessage } from '@bastion/engine';

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

export function setHosts(list: HostAddress[]): void {
  hosts = list;
}

export interface InviteLink {
  label: string;
  url: string;
}

/**
 * Where the partner opens the game: this page's port (dev or built) on the
 * host's network addresses. `localhost` only works on this machine, so it is
 * never offered; if the page was opened through a network address, that one.
 */
export function inviteLinks(): InviteLink[] {
  const port = location.port ? `:${location.port}` : '';
  const url = (address: string) => `${location.protocol}//${address}${port}/`;
  const links = hosts.map((h) => ({ label: h.kind === 'tailscale' ? 'Por internet (Tailscale)' : 'En tu misma red', url: url(h.address) }));
  const local = ['localhost', '127.0.0.1', '[::1]'].includes(location.hostname);
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
      this.emitStatus('open');
      const token = storage()?.getItem(TOKEN_KEY) ?? undefined;
      this.send({ t: 'hello', name: this.name, token });
    };
    ws.onmessage = (ev) => {
      const msg = JSON.parse(String(ev.data)) as ServerMessage;
      if (msg.t === 'welcome') storage()?.setItem(TOKEN_KEY, msg.token);
      for (const fn of this.messageHandlers) fn(msg);
    };
    ws.onclose = (ev) => {
      if (this.ws !== ws) return;
      this.emitStatus('closed');
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

export function serverUrl(): string {
  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  return `${proto}://${location.host}/ws`;
}
