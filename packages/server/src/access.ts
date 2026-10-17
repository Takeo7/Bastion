import { createHmac, timingSafeEqual } from 'node:crypto';
import type { IncomingMessage, ServerResponse } from 'node:http';

// The game's password (docs/WEB.md, docs/FASE11.md §6). On a website anyone
// may come across the address, so with one set nothing of the game is served
// to a browser that has not given it: not the campaign's WebSocket, and not the
// models, which are Synty's and only the two players may see. What a browser
// needs to ask for it is open: the page, its code and fonts, the web app's
// manifest and icon. Given once, the browser keeps it as a cookie for a year
// (the client's password screen, ui/password.ts).

/** The cookie: a keyed hash of the password, so changing the password locks every browser out again. */
const COOKIE = 'bastion_acceso';
const YEAR_S = 365 * 24 * 3600;
/** Wrong passwords allowed per minute, from everyone together; then nobody is let in to try until it passes. */
const TRIES_PER_MINUTE = 10;
const MINUTE_MS = 60_000;

/**
 * Open without the password: the page and what draws the password screen.
 * Vite's bundles (code, styles, our fonts), our own steel and grime textures,
 * the manifest and icons (a phone fetches those without cookies). Never the
 * models, the maps, the sounds or Synty's interface art.
 */
function isPublic(path: string): boolean {
  if (path === '/' || path === '/index.html' || path === '/manifest.webmanifest' || path === '/__access') return true;
  if (path.startsWith('/assets/') || path.startsWith('/ui/mixta/') || path.startsWith('/icono-')) return true;
  // A route of the page (no extension) gets the page; the server's own (/__report…) are not routes.
  return !path.startsWith('/__') && !/\.\w+$/.test(path);
}

export class Access {
  private readonly key: Buffer | null;
  private wrong: number[] = [];

  constructor(
    password: string | null,
    private readonly log: (line: string) => void,
  ) {
    this.key = password ? token(password) : null;
  }

  /** Whether there is a password at all. */
  get locked(): boolean {
    return this.key !== null;
  }

  /** Whether this request may have the game: no password, or the cookie that says it was given. */
  allows(req: IncomingMessage): boolean {
    if (!this.key) return true;
    const given = cookie(req.headers.cookie, COOKIE);
    if (!given) return false;
    const mine = this.key.toString('base64url');
    return given.length === mine.length && timingSafeEqual(Buffer.from(given), Buffer.from(mine));
  }

  /**
   * Answers the request when it is this module's: `/__access` (the client asks
   * whether it may come in, and gives the password), or anything else the
   * password keeps. False when the request goes on to be served as usual.
   */
  handle(req: IncomingMessage, res: ServerResponse, who: string): boolean {
    const path = new URL(req.url ?? '/', 'http://x').pathname;
    if (path === '/__access') {
      if (req.method === 'POST') this.enter(req, res, who);
      else {
        res.writeHead(this.allows(req) ? 204 : 401, { 'cache-control': 'no-store' });
        res.end();
      }
      return true;
    }
    if (this.allows(req) || isPublic(path)) return false;
    res.writeHead(401, { 'content-type': 'text/plain; charset=utf-8', 'cache-control': 'no-store' });
    res.end('Hace falta la contraseña de la partida.');
    return true;
  }

  /** The password screen sends `{ password }`: right, the cookie (204); wrong, 401; too many wrong lately, 429. */
  private enter(req: IncomingMessage, res: ServerResponse, who: string): void {
    let body = '';
    req.on('data', (chunk) => {
      if (body.length < 1000) body += chunk;
    });
    req.on('end', () => {
      const now = Date.now();
      this.wrong = this.wrong.filter((t) => now - t < MINUTE_MS);
      if (this.wrong.length >= TRIES_PER_MINUTE) {
        res.writeHead(429, { 'retry-after': String(Math.ceil((this.wrong[0]! + MINUTE_MS - now) / 1000)), 'cache-control': 'no-store' });
        res.end();
        return;
      }
      let password = '';
      try {
        const sent = (JSON.parse(body) as { password?: unknown }).password;
        if (typeof sent === 'string') password = sent;
      } catch {
        /* nothing usable: a wrong password */
      }
      if (!this.key || !password || !timingSafeEqual(token(password), this.key)) {
        this.wrong.push(now);
        this.log(`contraseña equivocada desde ${who}`);
        res.writeHead(401, { 'cache-control': 'no-store' });
        res.end();
        return;
      }
      this.log(`contraseña correcta desde ${who}`);
      // Secure when the browser came by https: straight to us, or through the website's proxy.
      const secure = (req.socket as { encrypted?: boolean }).encrypted || req.headers['x-forwarded-proto'] === 'https';
      res.writeHead(204, {
        'set-cookie': `${COOKIE}=${this.key.toString('base64url')}; Path=/; Max-Age=${YEAR_S}; HttpOnly; SameSite=Lax${secure ? '; Secure' : ''}`,
        'cache-control': 'no-store',
      });
      res.end();
    });
  }
}

function token(password: string): Buffer {
  return createHmac('sha256', password).update('bastion-acceso').digest();
}

function cookie(header: string | undefined, name: string): string | null {
  for (const part of header?.split(';') ?? []) {
    const [k, ...v] = part.trim().split('=');
    if (k === name) return v.join('=');
  }
  return null;
}
