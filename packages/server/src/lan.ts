import { createSocket } from 'node:dgram';
import { networkInterfaces } from 'node:os';

// Finding a game on the local network without typing addresses. The desktop
// app's server says every two seconds, by UDP broadcast, that there is a game
// here and on which port (announce); the app's start screen listens and lists
// what it hears (listen). Tailscale carries no broadcasts: there the address
// is still typed. The web flow (npm start) announces nothing.

/** The UDP port the announcements go to. */
export const LAN_PORT = 41390;
/** A game not heard for this long has gone. */
const GONE_MS = 7000;

/** What a host says. */
interface Beacon {
  /** Always 'bastion': anything else arriving on the port is ignored. */
  game: 'bastion';
  /** The game's http port. */
  port: number;
  /** The host: the first player in the room, or the computer's name. */
  name: string;
  /** Who is connected. */
  players: string[];
  /** lobby, base or mission. */
  phase: string;
}

/** A game heard on the network, and the address it came from. */
export interface LanGame {
  name: string;
  /** Where to join it: address:port. */
  address: string;
  players: string[];
  phase: string;
}

/** This machine's IPv4 addresses. */
function ownAddresses(): Set<string> {
  const out = new Set(['127.0.0.1']);
  for (const addrs of Object.values(networkInterfaces())) for (const a of addrs ?? []) if (a.family === 'IPv4') out.add(a.address);
  return out;
}

/** Broadcast addresses of this machine's networks (not loopback, nor point-to-point links such as Tailscale's). */
function broadcastAddresses(): string[] {
  const out = new Set<string>();
  for (const addrs of Object.values(networkInterfaces())) {
    for (const a of addrs ?? []) {
      if (a.family !== 'IPv4' || a.internal || a.netmask === '255.255.255.255') continue;
      const mask = a.netmask.split('.').map(Number);
      out.add(a.address.split('.').map((b, i) => Number(b) | (~mask[i]! & 255)).join('.'));
    }
  }
  return [...out];
}

/** Announces the game on `port` every two seconds; `about` says who is in it. Returns the stop. */
export function announce(port: number, computer: string, about: () => { players: string[]; phase: string }): () => void {
  const socket = createSocket({ type: 'udp4', reuseAddr: true });
  // The announcement is a help: it never stops the game.
  socket.on('error', () => {});
  socket.bind(() => socket.setBroadcast(true));
  const tick = () => {
    const { players, phase } = about();
    const beacon: Beacon = { game: 'bastion', port, name: players[0] ?? computer, players, phase };
    const data = Buffer.from(JSON.stringify(beacon));
    // This machine too: a second copy of the app on it hears the game (tests, two players on one computer).
    for (const to of [...broadcastAddresses(), '127.0.0.1']) socket.send(data, LAN_PORT, to, () => {});
  };
  const timer = setInterval(tick, 2000);
  const first = setTimeout(tick, 300);
  return () => {
    clearInterval(timer);
    clearTimeout(first);
    socket.close();
  };
}

const text = (v: unknown, max: number): string => (typeof v === 'string' ? v.slice(0, max) : '');

/**
 * Listens for announcements: `onChange` gets the games heard in the last few
 * seconds whenever that list changes. `skip(port)` leaves out a game of this
 * machine (the app's own). Returns the stop.
 */
export function listen(onChange: (games: LanGame[]) => void, skip?: (port: number) => boolean): () => void {
  const games = new Map<string, LanGame & { seen: number }>();
  const own = ownAddresses();
  let last = '';
  const emit = () => {
    const list = [...games.values()].map(({ seen: _, ...g }) => g).sort((a, b) => a.name.localeCompare(b.name));
    const now = JSON.stringify(list);
    if (now === last) return;
    last = now;
    onChange(list);
  };
  const socket = createSocket({ type: 'udp4', reuseAddr: true });
  socket.on('error', () => {});
  socket.on('message', (msg, from) => {
    let b: Partial<Beacon>;
    try {
      b = JSON.parse(String(msg)) as Partial<Beacon>;
    } catch {
      return;
    }
    if (b.game !== 'bastion' || typeof b.port !== 'number' || !Number.isInteger(b.port) || b.port <= 0 || b.port > 65535) return;
    // A game of this machine arrives twice (broadcast and loopback): one entry, with its network address if there is one.
    const local = own.has(from.address);
    if (local && skip?.(b.port)) return;
    const key = local ? `local:${b.port}` : `${from.address}:${b.port}`;
    const before = games.get(key);
    games.set(key, {
      name: text(b.name, 32) || from.address,
      address: from.address === '127.0.0.1' && before ? before.address : `${from.address}:${b.port}`,
      players: Array.isArray(b.players) ? b.players.slice(0, 2).map((p) => text(p, 32)) : [],
      phase: text(b.phase, 16),
      seen: Date.now(),
    });
    emit();
  });
  socket.bind(LAN_PORT);
  const prune = setInterval(() => {
    for (const [key, g] of games) if (Date.now() - g.seen > GONE_MS) games.delete(key);
    emit();
  }, 1000);
  return () => {
    clearInterval(prune);
    socket.close();
  };
}
