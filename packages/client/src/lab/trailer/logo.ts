import { capHeight, caps, clamp01, font, hazard, image, inOutCubic, INK, lerp, outBack, outCubic, outQuart, sheet, span, TITLE, BODY, track, WHITE, YELLOW, type Glyph } from './paint';

// The logo, animated (docs/TRAILER.md, shot 8, and on its own): BASTIÓN in
// Oswald cut from the Mixta steel, the yellow-and-black band under it, the line
// under that and «Próximamente». The letters are stamped in one after another
// on the music's last accent; the acute of the Ó is a yellow chip of the band
// that drops in after them. Drawn still (any `t` past the end) it is the
// cover's logo.

export const LOGO = {
  word: 'BASTIÓN',
  line: 'Supervivencia táctica para dos',
  soon: 'Próximamente',
};

/** The word's size at 1080 lines, scale 1. */
const SIZE = 236;
const TRACKING = 0.07;

/** When each part arrives, in seconds from the logo's start. */
const STAMP = 0.06;
const STAMP_LEN = 0.24;
const ACCENT_AT = 0.78;
const GLINT_AT = 1.05;
const LINE_AT = 1.35;
const SOON_AT = 2.5;

let steel: HTMLImageElement | null = null;
let grime: HTMLImageElement | null = null;

export async function setupLogo(): Promise<void> {
  [steel, grime] = await Promise.all([image('/ui/mixta/steel.webp'), image('/ui/mixta/grime.webp')]);
}

interface Letter {
  /** The letter in steel: bevelled, outlined, textured. */
  art: HTMLCanvasElement;
  /** Its bare shape, white, for the flash as it lands. */
  mask: HTMLCanvasElement;
  glyph: Glyph;
}

interface Art {
  size: number;
  cap: number;
  pad: number;
  width: number;
  letters: Letter[];
  /** The Ó: where its chip sits, from the word's left and its baseline. */
  accent: { x: number; y: number; w: number; h: number; slant: number };
  /** The word drawn whole each frame, for the glint. */
  layer: [HTMLCanvasElement, CanvasRenderingContext2D];
}

const arts = new Map<number, Art>();

function art(ctx: CanvasRenderingContext2D, size: number): Art {
  const cached = arts.get(size);
  if (cached) return cached;
  const f = font(600, size, TITLE);
  const cap = capHeight(ctx, f);
  const pad = Math.ceil(size * 0.08);
  const { glyphs, width } = track(ctx, LOGO.word, f, size, TRACKING);
  const letters = glyphs.map((g) => letter(g, f, size, cap, pad));
  const o = glyphs.find((g) => g.ch === 'Ó')!;
  const accent = { x: o.x + o.w / 2 + size * 0.02, y: -cap - size * 0.105, w: size * 0.135, h: size * 0.145, slant: size * 0.085 };
  const result: Art = { size, cap, pad, width, letters, accent, layer: sheet(width + pad * 2, cap + pad * 2) };
  arts.set(size, result);
  return result;
}

/** One letter in steel. The Ó is drawn as an O: its acute is the chip. */
function letter(glyph: Glyph, f: string, size: number, cap: number, pad: number): Letter {
  const ch = glyph.ch === 'Ó' ? 'O' : glyph.ch;
  const w = glyph.w + pad * 2;
  const h = cap + pad * 2;
  const base = pad + cap;

  const [mask, m] = sheet(w, h);
  m.font = f;
  m.fillStyle = '#fff';
  m.fillText(ch, pad, base);

  const [out, c] = sheet(w, h);
  c.drawImage(mask, 0, 0);
  c.globalCompositeOperation = 'source-in';
  const body = c.createLinearGradient(0, pad, 0, base);
  body.addColorStop(0, '#f1f3ef');
  body.addColorStop(0.46, '#bfc5c2');
  body.addColorStop(0.5, '#8d9592');
  body.addColorStop(0.78, '#a9b0ad');
  body.addColorStop(1, '#d3d8d5');
  c.fillStyle = body;
  c.fillRect(0, 0, w, h);
  // Brushed steel and grime, lined up across the word so the grain runs on.
  c.globalCompositeOperation = 'multiply';
  for (const [img, scale, alpha, dy] of [[steel, 1.6, 0.55, 0], [grime, 2.4, 0.4, 37]] as const) {
    if (!img) continue;
    const pattern = c.createPattern(img, 'repeat')!;
    pattern.setTransform(new DOMMatrix().translate(pad - glyph.x, dy).scale(scale));
    c.globalAlpha = alpha;
    c.fillStyle = pattern;
    c.fillRect(0, 0, w, h);
  }
  c.globalAlpha = 1;
  c.globalCompositeOperation = 'destination-in';
  c.drawImage(mask, 0, 0);

  // The bevel: a lit top edge, a dark bottom one.
  const d = Math.max(2, size * 0.018);
  for (const [dy, colour] of [[d, 'rgba(255, 255, 255, 0.85)'], [-d, 'rgba(14, 16, 18, 0.55)']] as const) {
    const [edge, e] = sheet(w, h);
    e.drawImage(mask, 0, 0);
    e.globalCompositeOperation = 'destination-out';
    e.drawImage(mask, 0, dy);
    e.globalCompositeOperation = 'source-in';
    e.fillStyle = colour;
    e.fillRect(0, 0, w, h);
    c.globalCompositeOperation = 'source-over';
    c.drawImage(edge, 0, 0);
  }

  // A dark rim behind it all.
  c.globalCompositeOperation = 'destination-over';
  c.font = f;
  c.lineJoin = 'round';
  c.lineWidth = size * 0.032;
  c.strokeStyle = '#0a0b0b';
  c.strokeText(ch, pad, base);
  return { art: out, mask, glyph };
}

export interface LogoPlace {
  /** The middle of the whole logo, the acute's chip included. */
  cx: number;
  cy: number;
  /** 1 at 1080 lines. */
  scale: number;
  /** 0–1, the whole logo. */
  opacity?: number;
  /** All of it; the word, its band and the line; or the word and its band alone (the small covers). */
  parts?: 'all' | 'line' | 'word';
}

interface Layout {
  art: Art;
  fLine: string;
  fSoon: string;
  width: number;
  height: number;
  top: number;
  baseline: number;
  bandY: number;
  band: number;
  lineBase: number;
  soonBase: number;
  capSoon: number;
}

function layout(ctx: CanvasRenderingContext2D, place: LogoPlace): Layout {
  const a = art(ctx, Math.round(SIZE * place.scale));
  const { cap } = a;
  const parts = place.parts ?? 'all';
  const fLine = font(500, Math.round(cap * 0.19), TITLE);
  const fSoon = font(600, Math.round(cap * 0.13), BODY);
  const capLine = capHeight(ctx, fLine);
  const capSoon = capHeight(ctx, fSoon);
  // The chip stands above the capitals.
  const rise = -a.accent.y - cap + a.accent.h / 2;
  const gapBand = cap * 0.12;
  const band = cap * 0.115;
  const gapLine = cap * 0.22;
  const gapSoon = cap * 0.4;
  let height = rise + cap + gapBand + band;
  if (parts !== 'word') height += gapLine + capLine;
  if (parts === 'all') height += gapSoon + capSoon;
  const top = place.cy - height / 2 + rise;
  const baseline = top + cap;
  const bandY = baseline + gapBand;
  const lineBase = bandY + band + gapLine + capLine;
  const line = parts === 'word' ? 0 : track(ctx, caps(LOGO.line), fLine, Math.round(cap * 0.19), 0.34).width;
  return { art: a, fLine, fSoon, width: Math.max(a.width, line), height, top, baseline, bandY, band, lineBase, soonBase: lineBase + gapSoon + capSoon, capSoon };
}

/** How much room the logo takes, still, at `scale` (the chip and the line included). */
export function logoSize(ctx: CanvasRenderingContext2D, scale: number, parts: LogoPlace['parts'] = 'all'): { width: number; height: number } {
  const l = layout(ctx, { cx: 0, cy: 0, scale, parts });
  return { width: l.width, height: l.height };
}

/** The logo `t` seconds after it starts; still from about 3.5 s on, but for a slow push in. */
export function drawLogo(ctx: CanvasRenderingContext2D, t: number, place: LogoPlace, push = true): void {
  const s = place.scale;
  const parts = place.parts ?? 'all';
  const { art: a, fLine, fSoon, top, baseline, bandY, band, lineBase, soonBase, capSoon } = layout(ctx, place);
  const { cap, width } = a;
  const left = place.cx - width / 2;

  ctx.save();
  ctx.globalAlpha = place.opacity ?? 1;
  // The slow push in, from the letters' landing to the end.
  const k = push ? 1 + 0.028 * inOutCubic(span(t, 0.7, 9)) : 1;
  // The weight of the last letter landing, and a smaller one for the chip.
  const last = (a.letters.length - 1) * STAMP + STAMP_LEN;
  const land = ACCENT_AT + 0.3;
  const shake = jolt(t, last, s * 5) + jolt(t, land, s * 2.5);
  ctx.translate(place.cx, place.cy + shake);
  ctx.scale(k, k);
  ctx.translate(-place.cx, -place.cy);

  // The band, opening out from the middle, its stripes sliding to a stop.
  const open = outCubic(span(t, 0, 0.42));
  if (open > 0) {
    shadowed(ctx, s, 0.5);
    hazard(ctx, place.cx - (width / 2) * open, bandY, width * open, band, 90 * s * outCubic(span(t, 0, 1.6)));
    ctx.shadowColor = 'transparent';
  }

  // The letters, stamped in one after another.
  const [layer, l] = a.layer;
  l.clearRect(0, 0, layer.width, layer.height);
  a.letters.forEach((it, i) => {
    const p = span(t, i * STAMP, i * STAMP + STAMP_LEN);
    if (p <= 0) return;
    const grow = lerp(1.45, 1, outQuart(p));
    const gx = a.pad + it.glyph.x + it.glyph.w / 2;
    const gy = a.pad + cap / 2;
    l.save();
    l.globalAlpha = clamp01(p * 2.5);
    l.translate(gx, gy);
    l.scale(grow, grow);
    l.translate(-gx, -gy);
    l.drawImage(it.art, it.glyph.x, 0);
    const flash = 0.75 * (1 - outCubic(span(t, i * STAMP, i * STAMP + 0.6)));
    if (flash > 0) {
      l.globalAlpha *= flash;
      l.drawImage(it.mask, it.glyph.x, 0);
    }
    l.restore();
  });
  // The light running across the steel once it has all landed.
  const g = span(t, GLINT_AT, GLINT_AT + 0.85);
  if (g > 0 && g < 1) {
    const gx = lerp(-0.25, 1.25, inOutCubic(g)) * layer.width;
    const glint = l.createLinearGradient(gx - cap * 0.7, layer.height, gx + cap * 0.7, 0);
    glint.addColorStop(0, 'rgba(255, 255, 255, 0)');
    glint.addColorStop(0.5, 'rgba(255, 255, 255, 0.65)');
    glint.addColorStop(1, 'rgba(255, 255, 255, 0)');
    l.save();
    l.globalCompositeOperation = 'source-atop';
    l.fillStyle = glint;
    l.fillRect(0, 0, layer.width, layer.height);
    l.restore();
  }
  shadowed(ctx, s, 0.75);
  ctx.drawImage(layer, left - a.pad, top - a.pad);

  // The acute: a chip of the band dropping onto the O.
  const c = span(t, ACCENT_AT, ACCENT_AT + 0.3);
  if (c > 0) {
    const { x, y, w, h, slant } = a.accent;
    const fall = (1 - outBack(c, 2.2)) * -cap * 0.55;
    ctx.save();
    ctx.globalAlpha *= clamp01(c * 3);
    ctx.translate(left + x, baseline + y + fall);
    ctx.beginPath();
    ctx.moveTo(-w / 2 - slant / 2, h / 2);
    ctx.lineTo(w / 2 - slant / 2, h / 2);
    ctx.lineTo(w / 2 + slant / 2, -h / 2);
    ctx.lineTo(-w / 2 + slant / 2, -h / 2);
    ctx.closePath();
    ctx.fillStyle = YELLOW;
    ctx.fill();
    ctx.shadowColor = 'transparent';
    ctx.lineJoin = 'round';
    ctx.lineWidth = a.size * 0.022;
    ctx.strokeStyle = INK;
    ctx.stroke();
    ctx.restore();
  }
  ctx.shadowColor = 'transparent';

  // The line: its letters closing in from wide.
  const ln = span(t, LINE_AT, LINE_AT + 0.95);
  if (ln > 0 && parts !== 'word') {
    const size = Math.round(cap * 0.19);
    const set = track(ctx, caps(LOGO.line), fLine, size, lerp(0.6, 0.34, outCubic(ln)));
    ctx.save();
    ctx.globalAlpha *= outCubic(span(t, LINE_AT, LINE_AT + 0.6));
    shadowed(ctx, s, 0.6);
    ctx.fillStyle = YELLOW;
    for (const gl of set.glyphs) ctx.fillText(gl.ch, place.cx - set.width / 2 + gl.x, lineBase);
    ctx.restore();
  }

  // «Próximamente», between two rules growing outwards.
  const sn = span(t, SOON_AT, SOON_AT + 0.7);
  if (sn > 0 && parts === 'all') {
    const size = Math.round(cap * 0.13);
    const set = track(ctx, caps(LOGO.soon), fSoon, size, 0.55);
    const x0 = place.cx - set.width / 2;
    ctx.save();
    ctx.globalAlpha *= outCubic(sn) * 0.92;
    shadowed(ctx, s, 0.6);
    ctx.fillStyle = WHITE;
    for (const gl of set.glyphs) ctx.fillText(gl.ch, x0 + gl.x, soonBase);
    const rule = cap * 0.62 * outCubic(span(t, SOON_AT + 0.1, SOON_AT + 1.1));
    const gap = size * 1.1;
    const ry = soonBase - capSoon / 2;
    ctx.fillStyle = 'rgba(244, 242, 236, 0.45)';
    ctx.fillRect(x0 - gap - rule, ry - s, rule, 2 * s);
    ctx.fillRect(x0 + set.width + gap, ry - s, rule, 2 * s);
    ctx.restore();
  }
  ctx.restore();
}

/** The soft dark shadow everything of the logo throws. */
function shadowed(ctx: CanvasRenderingContext2D, s: number, alpha: number): void {
  ctx.shadowColor = `rgba(0, 0, 0, ${alpha})`;
  ctx.shadowBlur = 36 * s;
  ctx.shadowOffsetY = 10 * s;
}

/** A quick dying shudder from `at` on. */
function jolt(t: number, at: number, amount: number): number {
  if (t < at) return 0;
  const k = 1 - span(t, at, at + 0.32);
  return Math.sin((t - at) * 70) * amount * k * k;
}
