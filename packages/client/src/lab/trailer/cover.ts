import { drawLogo, logoSize, type LogoPlace } from './logo';

// The cover in Steam's sizes (docs/TRAILER.md, «Qué se entrega»): Arte's key
// art (3840×2160, src/lab/trailer/shots/portada.ts), framed for each size, with
// the logo over it in the clear sky Arte leaves at the top; and the logo alone,
// clear, for the library. Drawn by portada.html (coverLab.ts).

/** What Arte gives of the key art, in its pixels. */
export const KEYART = {
  /** Between the soldier in front and the head of the horde. */
  focus: [1500, 1550] as [number, number],
  /** The clean sky, for the logo: x0, y0, x1, y1 (to its right, the smoke of a fire, x 2440–2800). */
  sky: [400, 80, 2300, 620] as [number, number, number, number],
};

export interface Format {
  /** The file, without extension, under .dev-snapshots/trailer/portada/. */
  name: string;
  w: number;
  h: number;
  /** Which logo goes on it; none on the library's background. */
  logo: LogoPlace['parts'] | null;
  /** The logo's widest share of the width. */
  fill: number;
  /** Where the logo goes: in the sky as this size frames it, or over the middle (the small capsule, whose sky is a sliver). */
  place: 'sky' | 'middle';
  /**
   * Which piece of the key art it shows, at the scale that just covers the
   * size: `x` the art's column at the middle (the focus if left out), `y` the
   * art's row at the top (the focus in the middle if left out).
   */
  x?: number;
  y?: number;
  /** The logo alone, on nothing. */
  clear?: boolean;
}

// Steam's capsules carry the art and the game's name, nothing else: the word
// and its band. The line goes only on the key art itself (press, the site).
export const FORMATS: Format[] = [
  { name: 'portada-3840x2160', w: 3840, h: 2160, logo: 'line', fill: 0.5, place: 'sky', y: 0 },
  // A little wider than the art: what goes is a strip at the bottom, the sky stays whole.
  { name: 'cabecera-920x430', w: 920, h: 430, logo: 'word', fill: 0.62, place: 'sky', y: 0 },
  { name: 'pequena-462x174', w: 462, h: 174, logo: 'word', fill: 0.8, place: 'middle', y: 420 },
  { name: 'principal-1232x706', w: 1232, h: 706, logo: 'word', fill: 0.56, place: 'sky', y: 0 },
  // The tall ones: the whole squad on its roof and the front of the horde (x 380–2170 and 380–1820).
  { name: 'vertical-748x896', w: 748, h: 896, logo: 'word', fill: 0.86, place: 'sky', x: 1270, y: 0 },
  { name: 'biblioteca-600x900', w: 600, h: 900, logo: 'word', fill: 0.86, place: 'sky', x: 1100, y: 0 },
  // The library's background: the band from y 560 to 1800, the horizon and all that matters, no logo.
  { name: 'biblioteca-fondo-3840x1240', w: 3840, h: 1240, logo: null, fill: 0, place: 'sky', y: 560 },
  { name: 'biblioteca-logo-1280x720', w: 1280, h: 720, logo: 'word', fill: 0.86, place: 'middle', clear: true },
];

/** One format drawn on its own canvas. */
export function drawCover(format: Format, art: HTMLImageElement | null): HTMLCanvasElement {
  const { w, h } = format;
  const canvas = Object.assign(document.createElement('canvas'), { width: w, height: h });
  const ctx = canvas.getContext('2d')!;
  // Where the art's pixels land: x' = (x - x0) · k.
  let view = { k: 1, x0: 0, y0: 0 };
  if (!format.clear) {
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, w, h);
    if (art) {
      view = frame(art, format);
      ctx.imageSmoothingQuality = 'high';
      ctx.drawImage(art, -view.x0 * view.k, -view.y0 * view.k, art.width * view.k, art.height * view.k);
    }
  }
  if (!format.logo) return canvas;

  // The room for the logo: the sky as framed here, or the whole picture.
  let box = { x0: 0, y0: 0, x1: w, y1: h };
  if (format.place === 'sky' && art) {
    const [sx0, sy0, sx1, sy1] = KEYART.sky;
    box = {
      x0: Math.max(0, (sx0 - view.x0) * view.k),
      y0: Math.max(0, (sy0 - view.y0) * view.k),
      x1: Math.min(w, (sx1 - view.x0) * view.k),
      y1: Math.min(h, (sy1 - view.y0) * view.k),
    };
  }
  const unit = logoSize(ctx, 1, format.logo);
  const scale = Math.min((w * format.fill) / unit.width, ((box.x1 - box.x0) * 0.92) / unit.width, ((box.y1 - box.y0) * 0.95) / unit.height);
  const cx = (box.x0 + box.x1) / 2;
  const cy = (box.y0 + box.y1) / 2;
  if (!format.clear) {
    // A little shade behind it, so it reads over any sky.
    const reach = cy + (unit.height * scale) / 2;
    const r = (unit.width * scale) / 2 + w * 0.06;
    const shade = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
    shade.addColorStop(0, `rgba(0, 0, 0, ${format.place === 'middle' ? 0.5 : 0.3})`);
    shade.addColorStop(1, 'rgba(0, 0, 0, 0)');
    ctx.save();
    ctx.translate(cx, cy);
    ctx.scale(1, Math.max(0.3, (reach - cy + h * 0.06) / r));
    ctx.translate(-cx, -cy);
    ctx.fillStyle = shade;
    ctx.fillRect(cx - r, cy - r, r * 2, r * 2);
    ctx.restore();
  }
  drawLogo(ctx, 99, { cx, cy, scale, parts: format.logo }, false);
  return canvas;
}

/** The scale that just covers the size, and the piece of the art it shows, never past its edges. */
function frame(art: HTMLImageElement, format: Format): { k: number; x0: number; y0: number } {
  const k = Math.max(format.w / art.width, format.h / art.height);
  const vw = format.w / k;
  const vh = format.h / k;
  const x0 = Math.min(art.width - vw, Math.max(0, (format.x ?? KEYART.focus[0]) - vw / 2));
  const y0 = Math.min(art.height - vh, Math.max(0, format.y ?? KEYART.focus[1] - vh / 2));
  return { k, x0, y0 };
}
