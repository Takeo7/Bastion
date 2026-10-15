import { black, BODY, capHeight, caps, clamp01, font, H, hazard, inCubic, inOutCubic, INK, lerp, outCubic, outQuart, span, TITLE, track, W, WHITE, words, YELLOW, type Word } from './paint';

// The trailer's words and cuts (docs/TRAILER.md): three kinds of card and the
// transitions. Each is a cue, drawn on every frame with the trailer's time and
// nothing else, and each keeps out of the frame before and after its seconds.
//   frase:  a line of the story, in Barlow, low on the left beside a yellow tab;
//           its words rise into place one by one.
//   title:  the title between the story and the combat, on black, in Oswald,
//           stamped on the music's hit between two strips of the band.
//   stamp:  the montage's short calls, in Oswald on a dark plate with its key
//           words in yellow; the plate wipes in and out.

export type Cue = (ctx: CanvasRenderingContext2D, time: number) => void;

const FRASE_SIZE = 60;
const FRASE_LINE = 1.24;
const FRASE_X = 156;
const FRASE_BOTTOM = H - 176;
/** Where the first line's capitals start when the words go high, out of the way of what happens low. */
const FRASE_TOP = 150;

/**
 * A line of the story from `from` to `to`, split into `lines` as given. A
 * `beat` holds back every line after the first, for a pause in the sentence.
 * `high`: at the top left instead of the bottom left.
 */
export function frase(lines: string[], from: number, to: number, beat = 0, high = false): Cue {
  let set: { words: (Word & { line: number; order: number })[]; widest: number; cap: number } | null = null;
  const f = font(600, FRASE_SIZE, BODY);
  return (ctx, time) => {
    if (time < from || time > to) return;
    if (!set) {
      let order = 0;
      const all = lines.flatMap((text, line) => words(ctx, text, f, FRASE_SIZE, 0.005).words.map((w) => ({ ...w, line, order: order++ })));
      set = { words: all, widest: Math.max(...lines.map((text) => words(ctx, text, f, FRASE_SIZE, 0.005).width)), cap: capHeight(ctx, f) };
    }
    const lineH = FRASE_SIZE * FRASE_LINE;
    const last = high ? FRASE_TOP + set.cap + (lines.length - 1) * lineH : FRASE_BOTTOM;
    const baseOf = (line: number) => last - (lines.length - 1 - line) * lineH;
    const top = baseOf(0) - set.cap - FRASE_SIZE * 0.18;
    const bottom = last + FRASE_SIZE * 0.26;
    const gone = inCubic(span(time, to - 0.42, to));
    const shown = outCubic(span(time, from, from + 0.5)) * (1 - gone);

    // A soft darkening behind the words, so they read over any image.
    ctx.save();
    ctx.globalAlpha = shown;
    const midY = (top + bottom) / 2;
    ctx.translate(FRASE_X - 40, midY);
    ctx.scale(1, (bottom - top + 220) / (set.widest + 520));
    const scrim = ctx.createRadialGradient(set.widest * 0.38, 0, 0, set.widest * 0.38, 0, set.widest * 0.5 + 260);
    scrim.addColorStop(0, 'rgba(0, 0, 0, 0.5)');
    scrim.addColorStop(0.6, 'rgba(0, 0, 0, 0.3)');
    scrim.addColorStop(1, 'rgba(0, 0, 0, 0)');
    ctx.fillStyle = scrim;
    ctx.fillRect(-400, -(set.widest + 520), set.widest + 1200, (set.widest + 520) * 2);
    ctx.restore();

    // The tab grows down first, and goes up last.
    const grow = outCubic(span(time, from, from + 0.38));
    const shrink = inOutCubic(span(time, to - 0.36, to - 0.02));
    const tabTop = lerp(top, bottom, shrink);
    const tabBottom = lerp(top, bottom, grow);
    if (tabBottom > tabTop) {
      ctx.fillStyle = YELLOW;
      ctx.fillRect(FRASE_X - 36, tabTop, 8, tabBottom - tabTop);
    }

    ctx.font = f;
    ctx.fillStyle = WHITE;
    ctx.shadowColor = 'rgba(0, 0, 0, 0.75)';
    ctx.shadowBlur = 18;
    ctx.shadowOffsetY = 3;
    for (const w of set.words) {
      const start = from + 0.2 + w.order * 0.065 + (w.line > 0 ? beat : 0);
      const q = span(time, start, start + 0.55);
      if (q <= 0) continue;
      const base = baseOf(w.line);
      ctx.save();
      // Each line rises out of a slot of its own.
      ctx.beginPath();
      ctx.rect(FRASE_X - 10, base - set.cap - FRASE_SIZE * 0.4, set.widest + 40, set.cap + FRASE_SIZE * 0.72);
      ctx.clip();
      ctx.globalAlpha = outCubic(q) * (1 - gone);
      const rise = (1 - outQuart(q)) * FRASE_SIZE * 0.95 - gone * FRASE_SIZE * 0.3;
      ctx.fillText(w.text, FRASE_X + w.x, base + rise);
      ctx.restore();
    }
  };
}

const TITLE_SIZE = 132;

/** The title on black: the first line on the hit at `from`, the rest at `second`, until `to`. */
export function title(lines: [string, string], from: number, second: number, to: number): Cue {
  const f = font(600, TITLE_SIZE, TITLE);
  return (ctx, time) => {
    if (time < from || time >= to) return;
    const t = time - from;
    // Black, a little lifted in the middle, flashing on each hit.
    const bg = ctx.createRadialGradient(W / 2, H / 2, 0, W / 2, H / 2, W * 0.6);
    bg.addColorStop(0, '#16191b');
    bg.addColorStop(1, '#000');
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, W, H);
    const flash = 0.16 * (1 - outCubic(span(time, from, from + 0.35))) + 0.1 * (1 - outCubic(span(time, second, second + 0.3)));
    if (flash > 0) {
      ctx.fillStyle = `rgba(242, 240, 232, ${flash})`;
      ctx.fillRect(0, 0, W, H);
    }

    const cap = capHeight(ctx, f);
    const sets = lines.map((text) => track(ctx, caps(text), f, TITLE_SIZE, 0.04));
    const widest = Math.max(...sets.map((s) => s.width));
    const gap = TITLE_SIZE * 0.34;
    const strip = 18;
    const room = 40;
    const height = strip + room + cap + gap + cap + room + strip;
    const top = (H - height) / 2;
    const bases = [top + strip + room + cap, top + strip + room + cap + gap + cap];

    // A slow push in, all the way to the cut.
    const k = 1 + 0.035 * (t / (to - from));
    ctx.save();
    ctx.translate(W / 2, H / 2);
    ctx.scale(k, k);
    ctx.translate(-W / 2, -H / 2);

    const x0 = (W - widest) / 2;
    const a = outQuart(span(time, from, from + 0.3));
    const b = outQuart(span(time, second, second + 0.3));
    hazard(ctx, x0, top, widest * a, strip, 120 * outCubic(span(time, from, from + 2)));
    hazard(ctx, x0 + widest * (1 - b), top + height - strip, widest * b, strip, -120 * outCubic(span(time, second, second + 2)));

    ctx.font = f;
    ctx.shadowColor = 'rgba(0, 0, 0, 0.6)';
    ctx.shadowBlur = 30;
    ctx.shadowOffsetY = 8;
    sets.forEach((set, i) => {
      const at = i === 0 ? from : second;
      const p = span(time, at, at + 0.22);
      if (p <= 0) return;
      const grow = lerp(1.22, 1, outQuart(p));
      const cx = W / 2;
      const cy = bases[i] - cap / 2;
      ctx.save();
      ctx.globalAlpha = clamp01(p * 4);
      ctx.translate(cx, cy);
      ctx.scale(grow, grow);
      ctx.translate(-cx, -cy);
      ctx.fillStyle = i === 0 ? WHITE : YELLOW;
      const left = (W - set.width) / 2;
      for (const g of set.glyphs) ctx.fillText(g.ch, left + g.x, bases[i]);
      ctx.restore();
    });
    ctx.restore();
  };
}

const STAMP_SIZE = 50;
const STAMP_X = 112;
const STAMP_BOTTOM = H - 142;

/** A call of the montage, its key words between asterisks: «Cada disparo *se oye.*». */
export function stamp(text: string, from: number, to: number): Cue {
  const f = font(600, STAMP_SIZE, TITLE);
  // Which letters are the key ones.
  const marks: boolean[] = [];
  let on = false;
  for (const ch of Array.from(text)) {
    if (ch === '*') on = !on;
    else marks.push(on);
  }
  const plain = caps(text.replace(/\*/g, ''));
  return (ctx, time) => {
    if (time < from || time > to) return;
    const set = track(ctx, plain, f, STAMP_SIZE, 0.05);
    const cap = capHeight(ctx, f);
    const tab = 9;
    const padX = 30;
    const padY = 22;
    const w = tab + padX + set.width + padX;
    const h = cap + padY * 2;
    const x = STAMP_X;
    const y = STAMP_BOTTOM - h;

    // The plate wipes in from the left and out to the right.
    const open = outQuart(span(time, from, from + 0.24));
    const close = inCubic(span(time, to - 0.22, to));
    const left = x + w * close;
    const right = x + w * open;
    if (right <= left) return;
    ctx.save();
    ctx.beginPath();
    ctx.rect(left, y - 4, right - left, h + 8);
    ctx.clip();
    ctx.shadowColor = 'rgba(0, 0, 0, 0.55)';
    ctx.shadowBlur = 24;
    ctx.shadowOffsetY = 6;
    ctx.fillStyle = 'rgba(13, 15, 16, 0.88)';
    ctx.fillRect(x, y, w, h);
    ctx.shadowColor = 'transparent';
    ctx.fillStyle = 'rgba(255, 255, 255, 0.07)';
    ctx.fillRect(x, y, w, 1);
    ctx.fillStyle = YELLOW;
    ctx.fillRect(x, y, tab, h);

    ctx.font = f;
    const base = y + padY + cap;
    set.glyphs.forEach((g, i) => {
      const start = from + 0.1 + i * 0.012;
      const q = span(time, start, start + 0.26);
      if (q <= 0) return;
      ctx.globalAlpha = outCubic(q);
      ctx.fillStyle = marks[i] ? YELLOW : WHITE;
      ctx.fillText(g.ch, x + tab + padX + g.x - (1 - outCubic(q)) * 22, base);
    });
    ctx.restore();
  };
}

/** From black into the image, between `from` and `to`. */
export function fadeIn(from: number, to: number): Cue {
  return (ctx, time) => {
    if (time < from || time > to) return;
    black(ctx, 1 - inOutCubic(span(time, from, to)));
  };
}

/** From the image into black, between `from` and `to`, and black until `until`. */
export function fadeOut(from: number, to: number, until = to): Cue {
  return (ctx, time) => {
    if (time < from || time >= until) return;
    black(ctx, inOutCubic(span(time, from, to)));
  };
}

/** Black, sharp, from `from` until `until`. */
export function cut(from: number, until: number): Cue {
  return (ctx, time) => {
    if (time >= from && time < until) black(ctx, 1);
  };
}

/**
 * The band sweeping across, left to right: from `from` it covers the image
 * behind its leading edge, the cut happens at `mid` under the black, and by
 * `to` its trailing edge has uncovered the next.
 */
export function sweep(from: number, mid: number, to: number): Cue {
  const band = 84;
  const lean = 260;
  return (ctx, time) => {
    if (time < from || time >= to) return;
    // The edges' x at the bottom of the frame; at the top they are `lean` further on.
    const lead = time < mid ? lerp(-lean - band, W + band, inOutCubic(span(time, from, mid))) : W + band;
    const trail = time < mid ? -lean - band : lerp(-lean - band, W + band, inOutCubic(span(time, mid, to)));
    ctx.save();
    ctx.beginPath();
    ctx.moveTo(trail, H);
    ctx.lineTo(trail + lean, 0);
    ctx.lineTo(lead + lean, 0);
    ctx.lineTo(lead, H);
    ctx.closePath();
    ctx.fillStyle = '#000';
    ctx.fill();
    // The band rides the moving edge: ahead while covering, behind while uncovering.
    const edge = time < mid ? lead - band : trail;
    ctx.beginPath();
    ctx.moveTo(edge, H);
    ctx.lineTo(edge + lean, 0);
    ctx.lineTo(edge + lean + band, 0);
    ctx.lineTo(edge + band, H);
    ctx.closePath();
    ctx.clip();
    hazard(ctx, edge - 10, 0, lean + band + 20, H, 0, 96, false);
    ctx.restore();
    ctx.save();
    ctx.strokeStyle = INK;
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(edge, H);
    ctx.lineTo(edge + lean, 0);
    ctx.moveTo(edge + band, H);
    ctx.lineTo(edge + lean + band, 0);
    ctx.stroke();
    ctx.restore();
  };
}
