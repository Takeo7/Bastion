import { X509Certificate } from 'node:crypto';
import { createSocket } from 'node:dgram';
import { appendFileSync, createReadStream, existsSync, mkdirSync, statSync, writeFileSync } from 'node:fs';
import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import { createServer as createHttpsServer } from 'node:https';
import type { Socket } from 'node:net';
import type { TLSSocket } from 'node:tls';
import { hostname, networkInterfaces } from 'node:os';
import { dirname, extname, join, normalize, resolve } from 'node:path';
import { WebSocketServer, type WebSocket } from 'ws';
import { announce } from './lan';
import { SaveStore } from './persistence';
import type { HostAddress } from '@bastion/engine';
import { Room } from './room';
import { certificate, type Tls } from './tls';

function arg(name: string): string | undefined {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 ? process.argv[i + 1] : undefined;
}

/** `--port 3000` wins over the PORT env var (dev tooling sometimes sets PORT for the web client). */
const PORT = Number(arg('port') ?? process.env.PORT ?? 3000);
/** The same game over https (Safari needs it, see tls.ts); `--https-port 0` turns it off. */
const HTTPS_PORT = Number(arg('https-port') ?? PORT + 1);
const ROOT = resolve(import.meta.dirname, '../../..');
/** `--client <dir>`: a client built elsewhere (a test build that leaves the players' one alone). */
const CLIENT_DIST = resolve(arg('client') ?? join(ROOT, 'packages/client/dist'));
const SAVE_PATH = resolve(arg('save') ?? process.env.SAVE_PATH ?? join(ROOT, 'data/save.json'));
/** Who connects, how, and what their browser reports: kept next to the save to diagnose a partner who cannot play. */
const NET_LOG = join(dirname(SAVE_PATH), 'red.log');

const MIME: Record<string, string> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.json': 'application/json',
  '.webp': 'image/webp',
  '.glb': 'model/gltf-binary',
  '.wav': 'audio/wav',
};

/** Debug only (`--snapshots <dir>`): the client's `__bastion.snap()` posts frames here. */
const SNAPSHOT_DIR = arg('snapshots');

function netLog(line: string): void {
  try {
    appendFileSync(NET_LOG, `${new Date().toISOString()} ${line}\n`);
  } catch {
    /* the log helps, it never stops the game */
  }
}

function peer(socket: Socket): string {
  return socket.remoteAddress?.replace(/^::ffff:/, '') ?? '?';
}

/** The host's own browser: it stays on http, which is fast on localhost. */
const LOOPBACK = new Set(['localhost', '127.0.0.1', '[::1]']);

/**
 * A page asked for on the http port from another computer goes over to https:
 * Safari runs plain http pages from the network some fifty times slower
 * (tls.ts), and the old address is easy to type out of habit. `?http` stays,
 * in case https gives trouble.
 */
function toHttps(req: IncomingMessage, res: ServerResponse): boolean {
  if (!https || req.method !== 'GET' || !req.headers.accept?.includes('text/html')) return false;
  const host = req.headers.host?.replace(/:\d+$/, '');
  const url = new URL(req.url ?? '/', 'http://x');
  if (!host || LOOPBACK.has(host) || url.searchParams.has('http')) return false;
  netLog(`http → https ${peer(req.socket)} (${host})`);
  res.writeHead(302, { location: `https://${host}:${HTTPS_PORT}${url.pathname}${url.search}` });
  res.end();
  return true;
}

function handle(req: IncomingMessage, res: ServerResponse): void {
  // The partner's browser reports its errors and hangs here (client report.ts): the host sees them.
  if (req.method === 'POST' && req.url === '/__report') {
    let body = '';
    req.on('data', (chunk) => {
      if (body.length < 2000) body += chunk;
    });
    req.on('end', () => {
      const text = body.slice(0, 2000);
      // How each browser draws (client perf.ts) comes tagged with the player's name, `[rend ana] …`; the address stands in for a missing one.
      const line = /^\[rend[ \]]/.test(text) ? text.replace(/^\[rend\]/, `[rend ${peer(req.socket)}]`) : `[navegador ${peer(req.socket)}] ${text}`;
      console.log(line);
      netLog(line);
      res.end('ok');
    });
    return;
  }
  if (SNAPSHOT_DIR && req.method === 'POST' && req.url?.startsWith('/__snap')) {
    let body = '';
    req.on('data', (chunk) => (body += chunk));
    req.on('end', () => {
      const name = new URL(req.url!, 'http://x').searchParams.get('name')?.replace(/[^\w-]/g, '') || 'snap';
      mkdirSync(SNAPSHOT_DIR, { recursive: true });
      writeFileSync(join(SNAPSHOT_DIR, `${name}.jpg`), Buffer.from(body.replace(/^data:image\/\w+;base64,/, ''), 'base64'));
      res.end('ok');
    });
    return;
  }
  // The https certificate, for the partner to trust it in their keychain: then
  // Safari takes the page as secure, with no warning and its JavaScript compiler on.
  if (tls && req.url === '/certificado') {
    res.writeHead(200, { 'content-type': 'application/x-x509-ca-cert', 'content-disposition': 'attachment; filename="Bastion.cer"' });
    res.end(new X509Certificate(tls.cert).raw);
    return;
  }
  if (!existsSync(CLIENT_DIST)) {
    res.writeHead(503, { 'content-type': 'text/plain; charset=utf-8' });
    res.end('El cliente no está compilado. Ejecuta "npm run build" o usa "npm run dev".');
    return;
  }
  const url = new URL(req.url ?? '/', 'http://localhost');
  const requested = normalize(join(CLIENT_DIST, decodeURIComponent(url.pathname)));
  let file = requested.startsWith(CLIENT_DIST) ? requested : CLIENT_DIST;
  if (!existsSync(file) || statSync(file).isDirectory()) {
    // A missing model or script is an error, not the page: a loader fed the page's HTML fails in odd ways.
    if (extname(url.pathname)) {
      res.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' });
      res.end(`No existe: ${url.pathname}`);
      return;
    }
    file = join(CLIENT_DIST, 'index.html');
  }
  // Vite names its bundles after their content, so they never change; the rest
  // (the page, the models) is checked again each time and answered 304 when it is the same.
  const modified = statSync(file).mtime;
  modified.setMilliseconds(0);
  const fresh = url.pathname.startsWith('/assets/');
  const since = Date.parse(req.headers['if-modified-since'] ?? '');
  if (!fresh && since >= modified.getTime()) {
    res.writeHead(304);
    res.end();
    return;
  }
  res.writeHead(200, {
    'content-type': MIME[extname(file)] ?? 'application/octet-stream',
    'cache-control': fresh ? 'public, max-age=31536000, immutable' : 'no-cache',
    'last-modified': modified.toUTCString(),
  });
  createReadStream(file).pipe(res);
}

/**
 * Virtual adapters (virtual machines, containers, WSL): no partner is on the
 * other side. Windows names them in words; macOS and Linux by prefix. On macOS,
 * bridge100 and up are made for virtual machines and containers (OrbStack,
 * Docker); bridge0 is the Thunderbolt bridge, a real cable, and stays. So does
 * utun, which is Tailscale.
 */
const VIRTUAL = /vEthernet|Hyper-V|WSL|VirtualBox|VMware|^bridge1\d\d|^vmnet|^vboxnet|^docker|^veth|^br-|^virbr/i;

/**
 * The address this machine goes out from (its default route), the one a
 * partner on the same network most likely reaches. A UDP socket that only
 * connects sends nothing: the system just picks the route. Null without one.
 */
function outbound(): Promise<string | null> {
  return new Promise((resolve) => {
    const socket = createSocket('udp4');
    let over = false;
    const done = (address: string | null) => {
      if (over) return;
      over = true;
      socket.close();
      resolve(address);
    };
    socket.once('error', () => done(null));
    socket.once('connect', () => done(socket.address().address));
    // TEST-NET-1 (RFC 5737): nobody there, but routed like any outside address.
    socket.connect(9, '192.0.2.1');
  });
}

/** IPv4 addresses a partner can reach: same network, or Tailscale (100.64.0.0/10). The default route's goes first. */
function hostAddresses(main: string | null): HostAddress[] {
  const out: HostAddress[] = [];
  for (const [name, addrs] of Object.entries(networkInterfaces())) {
    if (VIRTUAL.test(name)) continue;
    for (const a of addrs ?? []) {
      if (a.family !== 'IPv4' || a.internal) continue;
      const [first, second] = a.address.split('.').map(Number);
      out.push({ address: a.address, kind: first === 100 && second! >= 64 && second! < 128 ? 'tailscale' : 'lan' });
    }
  }
  return out.sort((a, b) => Number(b.address === main) - Number(a.address === main));
}

const hosts = hostAddresses(await outbound());
const tls: Tls | null = HTTPS_PORT ? certificate(join(dirname(SAVE_PATH), 'tls'), hosts.map((h) => h.address)) : null;
const http = createServer((req, res) => toHttps(req, res) || handle(req, res));
const https = tls ? createHttpsServer(tls, handle) : null;
// A browser that turns the certificate down (the warning page, or Safari refusing it for the WebSocket) ends
// here. By then its TLS socket has forgotten the address: it is noted when the TCP connection arrives.
const tcpPeers = new WeakMap<Socket, string>();
https?.on('connection', (raw: Socket) => tcpPeers.set(raw, peer(raw)));
https?.on('tlsClientError', (e: Error & { code?: string }, socket) => {
  const raw = (socket as Socket & { _parent?: Socket })._parent;
  netLog(`certificado rechazado por ${(raw && tcpPeers.get(raw)) ?? '?'} (${e.code ?? e.message.trim()})`);
});

const room = ((): Room => {
  try {
    return new Room(new SaveStore(SAVE_PATH, netLog), hosts, https ? HTTPS_PORT : null);
  } catch (e) {
    // A campaign too old for this release that could not be kept aside (persistence.ts): stop, rather than start and write over it.
    console.log(`\n  ${(e as Error).message}\n`);
    netLog(`no arranca: ${(e as Error).message}`);
    process.exit(1);
  }
})();
// One WebSocket server for both: the game is the same over http and https.
// Compressed (permessage-deflate, which browsers undo on their own): a mission's
// snapshot goes from ~105 KB to ~7 KB. Level 1 is nearly as small as the default
// and cheaper; the small messages of each order go as they are.
const wss = new WebSocketServer({ noServer: true, maxPayload: 64 * 1024, perMessageDeflate: { zlibDeflateOptions: { level: 1 }, threshold: 1024 } });
const alive = new WeakSet<WebSocket>();

for (const server of [http, https].filter((s): s is Server => !!s)) {
  server.on('upgrade', (req, socket, head) => {
    if (new URL(req.url ?? '/', 'http://x').pathname !== '/ws') return socket.destroy();
    wss.handleUpgrade(req, socket, head, (ws) => wss.emit('connection', ws, req));
  });
}

wss.on('connection', (socket, req) => {
  const who = `${peer(req.socket)} ${(req.socket as TLSSocket).encrypted ? 'wss' : 'ws'}`;
  // A browser that declines compression still plays: the log says so, in case it is slow to load a mission.
  netLog(`conecta ${who}${socket.extensions.includes('permessage-deflate') ? '' : ' (sin comprimir)'}`);
  socket.on('close', (code) => netLog(`desconecta ${who} (${code})`));
  alive.add(socket);
  socket.on('pong', () => alive.add(socket));
  room.connect(socket);
});

// Drop connections that stopped answering (closed laptop, lost Wi-Fi...).
setInterval(() => {
  for (const socket of wss.clients) {
    if (!alive.has(socket)) {
      socket.terminate();
      continue;
    }
    alive.delete(socket);
    socket.ping();
  }
}, 15_000);

/** `--anunciar`: the desktop app's server tells the local network there is a game here (lan.ts). */
const ANNOUNCE = process.argv.includes('--anunciar');

http.listen(PORT, () => {
  netLog(`servidor arrancado: http ${PORT}${https ? `, https ${HTTPS_PORT}` : ''} · ${hosts.map((h) => h.address).join(', ')}${ANNOUNCE ? ' · anunciado en la red' : ''}`);
  if (ANNOUNCE) announce(PORT, hostname().replace(/\.local$/, ''), () => room.summary());
  console.log(`\n  Bastión — servidor listo en el puerto ${PORT}`);
  console.log(`    Tú:              http://localhost:${PORT}`);
  for (const h of hosts) console.log(`    Tu compañero:    ${https ? `https://${h.address}:${HTTPS_PORT}` : `http://${h.address}:${PORT}`}${h.kind === 'tailscale' ? '  (Tailscale)' : ''}`);
  if (https) console.log('    (la primera vez su navegador avisa del certificado: es el de este ordenador, que lo acepte)');
  else if (HTTPS_PORT) console.log('    Sin https (no hay openssl en este ordenador): con Chrome, Edge o la app va bien; en Safari, tu compañero irá muy lento.');
  console.log('');
});
https?.on('error', (e) => console.log(`  No se pudo abrir https en el puerto ${HTTPS_PORT}: ${e.message}`));
// The game's own port taken (another Bastión open?): say so plainly instead of a stack trace.
http.on('error', (e: Error & { code?: string }) => {
  console.log(`\n  No se pudo abrir el puerto ${PORT}${e.code === 'EADDRINUSE' ? ': ya lo usa otro programa (¿otro Bastión abierto?)' : `: ${e.message}`}\n`);
  process.exit(1);
});
https?.listen(HTTPS_PORT);
