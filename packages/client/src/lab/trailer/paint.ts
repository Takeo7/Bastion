// What the trailer's motion graphics share (docs/TRAILER.md): the frame, the
// colours and fonts of the Mixta theme, easing, tracked type and the
// yellow-and-black band. Everything is drawn in the captured 2D canvas and is a
// pure function of the time, so Red's virtual clock can step it frame by frame.

export const W = 1920;
export const H = 1080;

/** The theme's accent and the dark of its hazard band (public/ui/synty/hud/hazard-yellow.webp). */
export const YELLOW = '#f2c230';
export const INK = '#1d190d';
export const WHITE = '#f4f2ec';

/** The game's two families (ui/theme.ts), and no other. */
export const TITLE = 'Oswald';
export const BODY = 'Barlow';

export const clamp01 = (x: number): number => (x < 0 ? 0 : x > 1 ? 1 : x);
/** How far `t` is through [a, b], from 0 to 1. */
export const span = (t: number, a: number, b: number): number => clamp01((t - a) / (b - a));
export const lerp = (a: number, b: number, k: number): number => a + (b - a) * k;

export const outCubic = (x: number): number => 1 - (1 - x) ** 3;
export const outQuart = (x: number): number => 1 - (1 - x) ** 4;
export const inCubic = (x: number): number => x * x * x;
export const inOutCubic = (x: number): number => (x < 0.5 ? 4 * x * x * x : 1 - (-2 * x + 2) ** 3 / 2);
export const outBack = (x: number, s = 1.70158): number => 1 + (s + 1) * (x - 1) ** 3 + s * (x - 1) ** 2;

export const font = (weight: number, size: number, family: string): string => `${weight} ${size}px ${family}`;

/** Spanish capitals, accents kept. */
export const caps = (text: string): string => text.toLocaleUpperCase('es');

export interface Glyph {
  ch: string;
  /** From the start of the line, kerning kept. */
  x: number;
  w: number;
}

/** A line set letter by letter with tracking (in em), each letter where the font's own kerning puts it. */
export function track(ctx: CanvasRenderingContext2D, text: string, f: string, size: number, tracking: number): { glyphs: Glyph[]; width: number } {
  ctx.font = f;
  const chars = Array.from(text);
  const gap = tracking * size;
  const glyphs = chars.map((ch, i) => ({ ch, x: ctx.measureText(chars.slice(0, i).join('')).width + i * gap, w: ctx.measureText(ch).width }));
  const last = glyphs[glyphs.length - 1];
  return { glyphs, width: last ? last.x + last.w : 0 };
}

export interface Word {
  text: string;
  x: number;
  w: number;
}

/** A line's words, each where it falls in the line set whole. */
export function words(ctx: CanvasRenderingContext2D, text: string, f: string, size: number, tracking = 0): { words: Word[]; width: number } {
  const { glyphs, width } = track(ctx, text, f, size, tracking);
  const out: Word[] = [];
  let start = -1;
  for (let i = 0; i <= glyphs.length; i++) {
    const space = i === glyphs.length || glyphs[i].ch === ' ';
    if (!space && start < 0) start = i;
    if (space && start >= 0) {
      const a = glyphs[start];
      const b = glyphs[i - 1];
      out.push({ text: glyphs.slice(start, i).map((g) => g.ch).join(''), x: a.x, w: b.x + b.w - a.x });
      start = -1;
    }
  }
  return { words: out, width };
}

/** How tall the capitals of a font stand, measured. */
export function capHeight(ctx: CanvasRenderingContext2D, f: string): number {
  ctx.font = f;
  return ctx.measureText('H').actualBoundingBoxAscent;
}

/**
 * The yellow-and-black band of the cover and the menu: dark stripes leaning
 * like the texture's (\), `shift` sliding them along; `worn`, a strip of it,
 * with its edges caught by the light and a thin dark rim.
 */
export function hazard(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, shift = 0, period = h * 2.2, worn = true): void {
  if (w <= 0 || h <= 0) return;
  ctx.save();
  ctx.beginPath();
  ctx.rect(x, y, w, h);
  ctx.clip();
  ctx.fillStyle = YELLOW;
  ctx.fillRect(x, y, w, h);
  ctx.fillStyle = INK;
  ctx.beginPath();
  const offset = ((shift % period) + period) % period;
  for (let sx = x - h - period + offset; sx < x + w; sx += period) {
    ctx.moveTo(sx, y);
    ctx.lineTo(sx + period / 2, y);
    ctx.lineTo(sx + period / 2 + h, y + h);
    ctx.lineTo(sx + h, y + h);
  }
  ctx.fill();
  if (!worn) {
    ctx.restore();
    return;
  }
  // Worn paint: the light catches the top edge, the bottom one sinks.
  ctx.fillStyle = 'rgba(255, 255, 255, 0.18)';
  ctx.fillRect(x, y, w, Math.max(1, h * 0.08));
  ctx.fillStyle = 'rgba(0, 0, 0, 0.35)';
  ctx.fillRect(x, y + h - Math.max(1, h * 0.1), w, Math.max(1, h * 0.1));
  ctx.restore();
  ctx.strokeStyle = 'rgba(8, 7, 3, 0.9)';
  ctx.lineWidth = Math.max(1.5, h * 0.07);
  ctx.strokeRect(x, y, w, h);
}

/** An image, loaded before the first frame (by its load event: `decode()` waits for a page that is shown). */
export function image(src: string): Promise<HTMLImageElement> {
  return new Promise((done, fail) => {
    const img = new Image();
    img.onload = () => done(img);
    img.onerror = () => fail(new Error(`No se carga ${src}`));
    img.src = src;
  });
}

/** An offscreen canvas. */
export function sheet(w: number, h: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement('canvas');
  c.width = Math.max(1, Math.ceil(w));
  c.height = Math.max(1, Math.ceil(h));
  return [c, c.getContext('2d')!];
}

/** Black over everything, `k` from 0 (none) to 1 (all). */
export function black(ctx: CanvasRenderingContext2D, k: number): void {
  if (k <= 0) return;
  ctx.save();
  ctx.globalAlpha = clamp01(k);
  ctx.fillStyle = '#000';
  ctx.fillRect(0, 0, W, H);
  ctx.restore();
}
