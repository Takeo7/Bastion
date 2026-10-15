// The trailer's receiver (docs/TRAILER.md): serves a fixed build of
// trailer.html and takes what the page sends while it records, with nothing
// but node:http and ffmpeg.
//
//   npx tsx packages/client/tools/trailer/receiver.ts [--build] [--port 3392]
//
//   --build   builds trailer.html first (tools/trailer/vite.config.ts) into
//             .dev-snapshots/trailer/web: a vite reload, when another session
//             saves a file, can then never cut a take in half.
//
// Then open http://localhost:3392/trailer.html?toma=<id> (or ?todas). The
// page posts:
//   /__trailer/start?name=tomas/x&w&h&fps[&alfa]   ffmpeg starts: tomas/x.mp4 (H.264), or .webm with transparency
//   /__trailer/end?name=tomas/x                     the file is closed
//   /__trailer/file?name=tomas/x.wav                a file, as it comes
//   /__trailer/log                                  a line for this terminal
// and sends the frames, raw RGBA, over a WebSocket (/__trailer/frames?name=tomas/x),
// each answered once it is in ffmpeg. Not by POST: Chromium uploads 8 MB from a
// typed array at ~23 MB/s, and as Blobs it fills its blob storage (~2 GB) and
// then refuses every request until it empties.
// Everything lands in .dev-snapshots/trailer/, outside the repository.

import { spawn, spawnSync, type ChildProcess } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream, existsSync, mkdirSync, statSync, writeFileSync } from 'node:fs';
import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import type { Duplex } from 'node:stream';
import { dirname, extname, join, normalize, resolve } from 'node:path';

const CLIENT = resolve(import.meta.dirname, '../..');
const OUT = resolve(CLIENT, '../../.dev-snapshots/trailer');
const WEB = join(OUT, 'web');
const FFMPEG = existsSync('/opt/homebrew/bin/ffmpeg') ? '/opt/homebrew/bin/ffmpeg' : 'ffmpeg';

function arg(name: string): string | undefined {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 ? process.argv[i + 1] : undefined;
}
const PORT = Number(arg('port') ?? 3392);

if (process.argv.includes('--build')) {
  console.log('Compilando trailer.html…');
  const built = spawnSync('npx', ['vite', 'build', '--config', 'tools/trailer/vite.config.ts', '--logLevel', 'warn'], { cwd: CLIENT, stdio: 'inherit' });
  if (built.status !== 0) process.exit(built.status ?? 1);
}

const MIME: Record<string, string> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.webp': 'image/webp',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.json': 'application/json',
  '.glb': 'model/gltf-binary',
  '.ktx2': 'image/ktx2',
  '.wav': 'audio/wav',
  '.ogg': 'audio/ogg',
};

/** A path under .dev-snapshots/trailer/, never outside it. */
function inside(name: string | null): string {
  const path = normalize(join(OUT, name ?? ''));
  if (!name || !path.startsWith(OUT + '/')) throw new Error(`nombre no válido: ${name}`);
  return path;
}

function body(req: IncomingMessage): Promise<Buffer> {
  return new Promise((resolve, reject) => {
    const chunks: Buffer[] = [];
    req.on('data', (c: Buffer) => chunks.push(c));
    req.on('end', () => resolve(Buffer.concat(chunks)));
    req.on('error', reject);
  });
}

const mb = (bytes: number) => `${(bytes / 1048576).toFixed(1)} MB`;

interface Encoding {
  ffmpeg: ChildProcess;
  file: string;
  frameBytes: number;
  frames: number;
  done: Promise<number>;
  log: string;
}
const encodings = new Map<string, Encoding>();

function start(name: string, w: number, h: number, fps: number, alpha: boolean): void {
  // The page was reloaded mid-take: that one is dropped and this one starts over.
  const old = encodings.get(name);
  if (old) {
    old.ffmpeg.kill('SIGKILL');
    encodings.delete(name);
    console.log(`  ${name}: se descarta la grabación que quedó a medias (${old.frames} fotogramas)`);
  }
  const file = inside(`${name}.${alpha ? 'webm' : 'mp4'}`);
  mkdirSync(dirname(file), { recursive: true });
  const input = ['-f', 'rawvideo', '-pix_fmt', 'rgba', '-s', `${w}x${h}`, '-r', String(fps), '-i', 'pipe:0'];
  // Takes are kept nearly lossless (CRF 12): the montage encodes them once more. BT.709, as HD video is.
  const output = alpha
    ? ['-c:v', 'libvpx-vp9', '-pix_fmt', 'yuva420p', '-b:v', '0', '-crf', '18', '-row-mt', '1']
    : ['-vf', 'scale=out_color_matrix=bt709:out_range=tv', '-c:v', 'libx264', '-preset', 'medium', '-crf', '12', '-pix_fmt', 'yuv420p', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709', '-movflags', '+faststart'];
  const ffmpeg = spawn(FFMPEG, ['-hide_banner', '-loglevel', 'error', '-y', ...input, ...output, file], { stdio: ['pipe', 'ignore', 'pipe'] });
  const enc: Encoding = { ffmpeg, file, frameBytes: w * h * 4, frames: 0, log: '', done: new Promise((r) => ffmpeg.on('close', (code) => r(code ?? -1))) };
  ffmpeg.stderr!.on('data', (d: Buffer) => (enc.log += d.toString()));
  encodings.set(name, enc);
  console.log(`▶ ${name}: ${w}×${h} a ${fps} fps → ${file.slice(OUT.length + 1)}`);
}

async function frame(name: string, n: number, data: Buffer): Promise<void> {
  const enc = encodings.get(name);
  if (!enc) throw new Error(`${name} no se está grabando`);
  if (n !== enc.frames) throw new Error(`${name}: llega el fotograma ${n} y tocaba el ${enc.frames}`);
  if (data.length !== enc.frameBytes) throw new Error(`${name}: el fotograma ${n} trae ${data.length} bytes y son ${enc.frameBytes}`);
  enc.frames++;
  // Backpressure: the page waits for this answer before sending the next frame but one.
  if (!enc.ffmpeg.stdin!.write(data)) await new Promise((r) => enc.ffmpeg.stdin!.once('drain', r));
  if (enc.frames % 60 === 0) process.stdout.write(`  ${name}: ${enc.frames} fotogramas (${(enc.frames / 60).toFixed(0)} s)\n`);
}

async function end(name: string): Promise<string> {
  const enc = encodings.get(name);
  if (!enc) throw new Error(`${name} no se está grabando`);
  encodings.delete(name);
  enc.ffmpeg.stdin!.end();
  const code = await enc.done;
  if (code !== 0) throw new Error(`ffmpeg acabó con ${code}: ${enc.log.trim()}`);
  const said = `${enc.file.slice(OUT.length + 1)} · ${enc.frames} fotogramas · ${mb(statSync(enc.file).size)}`;
  console.log(`■ ${said}`);
  return said;
}

async function trailer(req: IncomingMessage, res: ServerResponse, url: URL): Promise<void> {
  const q = url.searchParams;
  const name = q.get('name');
  switch (url.pathname) {
    case '/__trailer/start':
      inside(name);
      start(name!, Number(q.get('w')), Number(q.get('h')), Number(q.get('fps')), q.has('alfa'));
      return void res.end('ok');
    case '/__trailer/end':
      return void res.end(await end(name!));
    case '/__trailer/file': {
      const file = inside(name);
      const data = await body(req);
      mkdirSync(dirname(file), { recursive: true });
      writeFileSync(file, data);
      console.log(`■ ${name} · ${mb(data.length)}`);
      return void res.end('ok');
    }
    case '/__trailer/log':
      console.log(`· ${(await body(req)).toString()}`);
      return void res.end('ok');
  }
  res.writeHead(404);
  res.end();
}

function serve(res: ServerResponse, url: URL): void {
  const file = normalize(join(WEB, decodeURIComponent(url.pathname)));
  if (!file.startsWith(WEB) || !existsSync(file) || statSync(file).isDirectory()) {
    res.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' });
    res.end(existsSync(WEB) ? `No existe: ${url.pathname}` : 'Sin compilar: arranca el receptor con --build.');
    return;
  }
  res.writeHead(200, { 'content-type': MIME[extname(file)] ?? 'application/octet-stream', 'cache-control': 'no-cache' });
  createReadStream(file).pipe(res);
}

// ------------------------------------------------------------- WebSocket
// Just what the page needs (RFC 6455): its binary messages in, short text answers out.

/** Sends a short text message (unmasked, as a server does). */
function sendText(socket: Duplex, text: string): void {
  const data = Buffer.from(text);
  socket.write(Buffer.concat([Buffer.from([0x81, data.length]), data]));
}

/** Takes the frames of one clip: each binary message is a frame, answered with its number once ffmpeg has it. */
function frames(socket: Duplex, name: string): void {
  let chunks: Buffer[] = [];
  let have = 0;
  let parts: Buffer[] = [];
  let n = 0;
  let queue = Promise.resolve();
  const take = (bytes: number): Buffer => {
    const all = chunks.length === 1 ? chunks[0]! : Buffer.concat(chunks, have);
    chunks = all.length > bytes ? [all.subarray(bytes)] : [];
    have -= bytes;
    return all.subarray(0, bytes);
  };
  const peek = (bytes: number): Buffer => (chunks.length === 1 ? chunks[0]! : (chunks = [Buffer.concat(chunks, have)])[0]!).subarray(0, bytes);
  socket.on('data', (d: Buffer) => {
    chunks.push(d);
    have += d.length;
    for (;;) {
      if (have < 2) return;
      const h = peek(Math.min(have, 14));
      const fin = (h[0]! & 0x80) !== 0;
      const op = h[0]! & 0x0f;
      let len = h[1]! & 0x7f;
      let at = 2;
      if (len === 126) {
        if (have < 4) return;
        len = h.readUInt16BE(2);
        at = 4;
      } else if (len === 127) {
        if (have < 10) return;
        len = Number(h.readBigUInt64BE(2));
        at = 10;
      }
      if (have < at + 4 + len) return;
      const mask = Buffer.from(h.subarray(at, at + 4));
      take(at + 4);
      const payload = Buffer.from(take(len));
      for (let i = 0; i < payload.length; i++) payload[i]! ^= mask[i & 3]!;
      if (op === 0x8) return void socket.end(Buffer.from([0x88, 0]));
      if (op === 0x9) {
        socket.write(Buffer.concat([Buffer.from([0x8a, payload.length]), payload]));
        continue;
      }
      parts.push(payload);
      if (!fin) continue;
      const message = parts.length === 1 ? parts[0]! : Buffer.concat(parts);
      parts = [];
      const k = n++;
      queue = queue.then(
        () => frame(name, k, message).then(() => sendText(socket, String(k))),
        () => {},
      );
      queue.catch((e: Error) => {
        console.log(`✗ ${e.message}`);
        sendText(socket, `error ${e.message}`.slice(0, 120));
      });
    }
  });
  socket.on('error', () => {});
}

const server = createServer((req, res) => {
  const url = new URL(req.url ?? '/', 'http://x');
  // The page may also be served by someone's vite (&receptor=http://localhost:3392).
  res.setHeader('access-control-allow-origin', '*');
  if (req.method === 'OPTIONS') {
    res.writeHead(204, { 'access-control-allow-methods': 'POST, GET', 'access-control-allow-headers': 'content-type', 'access-control-max-age': '86400' });
    return void res.end();
  }
  if (req.method === 'POST' && url.pathname.startsWith('/__trailer/')) {
    trailer(req, res, url).catch((e: Error) => {
      console.log(`✗ ${e.message}`);
      res.writeHead(500, { 'content-type': 'text/plain; charset=utf-8' });
      res.end(e.message);
    });
    return;
  }
  // The game's telemetry (perf.ts) has nowhere to go here.
  if (req.method === 'POST') return void req.resume().on('end', () => res.end('ok'));
  serve(res, url);
});

server.on('upgrade', (req: IncomingMessage, socket: Duplex) => {
  const url = new URL(req.url ?? '/', 'http://x');
  const name = url.searchParams.get('name');
  const key = req.headers['sec-websocket-key'];
  if (url.pathname !== '/__trailer/frames' || !name || !key || !encodings.has(name)) return void socket.destroy();
  const accept = createHash('sha1').update(`${key}258EAFA5-E914-47DA-95CA-C5AB0DC85B11`).digest('base64');
  socket.write(`HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ${accept}\r\n\r\n`);
  frames(socket, name);
});

server.listen(PORT, () => {
  console.log(`\n  Receptor del tráiler en http://localhost:${PORT}/trailer.html`);
  console.log(`    ?toma=<id>  graba una toma · ?todas  todas las del guion · ?toma=<id>&ver  la enseña`);
  console.log(`    Sale en ${OUT}\n`);
});
