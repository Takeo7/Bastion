import { createReadStream, existsSync, mkdirSync, statSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { networkInterfaces } from 'node:os';
import { extname, join, normalize, resolve } from 'node:path';
import { WebSocketServer, type WebSocket } from 'ws';
import { SaveStore } from './persistence';
import type { HostAddress } from '@bastion/engine';
import { Room } from './room';

function arg(name: string): string | undefined {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 ? process.argv[i + 1] : undefined;
}

/** `--port 3000` wins over the PORT env var (dev tooling sometimes sets PORT for the web client). */
const PORT = Number(arg('port') ?? process.env.PORT ?? 3000);
const ROOT = resolve(import.meta.dirname, '../../..');
const CLIENT_DIST = join(ROOT, 'packages/client/dist');
const SAVE_PATH = resolve(arg('save') ?? process.env.SAVE_PATH ?? join(ROOT, 'data/save.json'));

const MIME: Record<string, string> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.json': 'application/json',
};

/** Debug only (`--snapshots <dir>`): the client's `__bastion.snap()` posts frames here. */
const SNAPSHOT_DIR = arg('snapshots');

const http = createServer((req, res) => {
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
  if (!existsSync(CLIENT_DIST)) {
    res.writeHead(503, { 'content-type': 'text/plain; charset=utf-8' });
    res.end('El cliente no está compilado. Ejecuta "npm run build" o usa "npm run dev".');
    return;
  }
  const url = new URL(req.url ?? '/', 'http://localhost');
  const requested = normalize(join(CLIENT_DIST, decodeURIComponent(url.pathname)));
  let file = requested.startsWith(CLIENT_DIST) ? requested : CLIENT_DIST;
  if (!existsSync(file) || statSync(file).isDirectory()) file = join(CLIENT_DIST, 'index.html');
  res.writeHead(200, { 'content-type': MIME[extname(file)] ?? 'application/octet-stream' });
  createReadStream(file).pipe(res);
});

/** IPv4 addresses a partner can reach: same network, or Tailscale (100.64.0.0/10). */
function hostAddresses(): HostAddress[] {
  const out: HostAddress[] = [];
  for (const addrs of Object.values(networkInterfaces())) {
    for (const a of addrs ?? []) {
      if (a.family !== 'IPv4' || a.internal) continue;
      const [first, second] = a.address.split('.').map(Number);
      out.push({ address: a.address, kind: first === 100 && second! >= 64 && second! < 128 ? 'tailscale' : 'lan' });
    }
  }
  return out;
}

const room = new Room(new SaveStore(SAVE_PATH), hostAddresses());
const wss = new WebSocketServer({ server: http, path: '/ws', maxPayload: 64 * 1024 });
const alive = new WeakSet<WebSocket>();

wss.on('connection', (socket) => {
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

http.listen(PORT, () => {
  console.log(`\n  Bastión — servidor listo en el puerto ${PORT}`);
  console.log(`    Tú:              http://localhost:${PORT}`);
  for (const h of hostAddresses()) console.log(`    Tu compañero:    http://${h.address}:${PORT}${h.kind === 'tailscale' ? '  (Tailscale)' : ''}`);
  console.log('');
});
