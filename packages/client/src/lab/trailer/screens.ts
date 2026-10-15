import type { Cue } from './cards';
import { clamp01, H, image, inOutCubic, lerp, outCubic, span, W } from './paint';

// Shot 6, «pantallas» (docs/TRAILER.md): the Bastión's own screens, captured
// from the real game at 1920×1080 (tools/trailer/capturas.mjs), one after
// another as panels over dark steel. Each slides in from the right while the
// one before steps back into the dark, and the camera leans slowly into what
// matters on it.

/** The captures, if they have been made (.dev-snapshots/trailer/capturas/, and at twice the pixels in 2x/, which the panels prefer). */
const CAPTURES = import.meta.glob<string>(['../../../../../.dev-snapshots/trailer/capturas/*.png', '../../../../../.dev-snapshots/trailer/capturas/2x/*.png'], { query: '?url', import: 'default', eager: true });

interface Panel {
  /** The capture's name. */
  name: string;
  /** Seconds into the shot it comes in. */
  at: number;
  /** How close the camera leans, from the screen whole (1) to its end. */
  zoom: number;
  /** What it leans towards, in the capture's pixels at 1920×1080. */
  focus: [number, number];
}

const PANELS: Panel[] = [
  // The tree, whole, and in close to its trunks.
  { name: 'arbol-entero', at: 0, zoom: 1.75, focus: [1112, 590] },
  // The Armería, and in close to its list: every weapon and who carries it.
  { name: 'armeria', at: 2.7, zoom: 1.7, focus: [330, 720] },
  // The mission: the squad on the road, two of the dead coming, and what the backpack's medicines can do.
  { name: 'mochila-menu', at: 5.4, zoom: 1.3, focus: [735, 620] },
];

/** The panel's size on the frame: the screen whole, with the dark around it. */
const PW = 1600;
const PH = (PW * H) / W;
const ENTER = 0.75;

const images = new Map<string, HTMLImageElement>();
let steel: HTMLImageElement | null = null;

export async function setupScreens(): Promise<void> {
  const url = (name: string) => CAPTURES[`../../../../../.dev-snapshots/trailer/capturas/2x/${name}.png`] ?? CAPTURES[`../../../../../.dev-snapshots/trailer/capturas/${name}.png`];
  const loads = PANELS.map(async (p) => {
    const src = url(p.name);
    if (src) images.set(p.name, await image(src));
  });
  await Promise.all([...loads, image('/ui/mixta/steel.webp').then((img) => (steel = img))]);
}

/** The screens from `from` to `to` (the trailer's seconds), opaque: they are the whole picture. */
export function screens(from: number, to: number): Cue {
  return (ctx, time) => {
    if (time < from || time >= to) return;
    const t = time - from;
    ground(ctx, t);
    PANELS.forEach((p, i) => {
      if (t < p.at) return;
      const next = PANELS[i + 1];
      // Gone once the next has fully come in.
      if (next && t > next.at + ENTER + 0.4) return;
      const come = outCubic(span(t, p.at, p.at + ENTER));
      const back = next ? inOutCubic(span(t, next.at, next.at + ENTER + 0.3)) : 0;
      const x = W / 2 + lerp(W * 0.62, 0, come) - back * W * 0.2;
      const scale = lerp(0.95, 1, come) * lerp(1, 0.88, back);
      // Opaque from the first frame: it slides in from off the frame over the one before, never through it.
      panel(ctx, p, x, H / 2 + 6, scale, back, t - p.at);
    });
  };
}

/** Dark brushed steel, lifted a little in the middle. */
function ground(ctx: CanvasRenderingContext2D, t: number): void {
  ctx.fillStyle = '#0d0f10';
  ctx.fillRect(0, 0, W, H);
  if (steel) {
    const pattern = ctx.createPattern(steel, 'repeat')!;
    // The grain drifts a little, so the ground is not dead still.
    pattern.setTransform(new DOMMatrix().translate(-t * 6, 0).scale(2.2));
    ctx.save();
    ctx.globalAlpha = 0.06;
    ctx.fillStyle = pattern;
    ctx.fillRect(0, 0, W, H);
    ctx.restore();
  }
  const lift = ctx.createRadialGradient(W / 2, H / 2, 0, W / 2, H / 2, W * 0.62);
  lift.addColorStop(0, 'rgba(60, 66, 70, 0.35)');
  lift.addColorStop(1, 'rgba(0, 0, 0, 0.6)');
  ctx.fillStyle = lift;
  ctx.fillRect(0, 0, W, H);
}

/** One screen as a panel: framed, shadowed, the picture inside leaning towards its focus; `dim` as it steps back. */
function panel(ctx: CanvasRenderingContext2D, p: Panel, cx: number, cy: number, scale: number, dim: number, age: number): void {
  const w = PW * scale;
  const h = PH * scale;
  const x = cx - w / 2;
  const y = cy - h / 2;
  ctx.save();
  ctx.shadowColor = 'rgba(0, 0, 0, 0.7)';
  ctx.shadowBlur = 60;
  ctx.shadowOffsetY = 18;
  ctx.fillStyle = '#050606';
  ctx.fillRect(x - 6, y - 6, w + 12, h + 12);
  ctx.shadowColor = 'transparent';
  const img = images.get(p.name);
  ctx.save();
  ctx.beginPath();
  ctx.rect(x, y, w, h);
  ctx.clip();
  if (img) {
    // Whole as it comes in, then leaning in on its focus, never past the capture's edges.
    const lean = inOutCubic(span(age, 0.5, 3.3));
    const k = lerp(1, p.zoom, lean);
    const unit = img.width / W;
    const sw = img.width / k;
    const sh = img.height / k;
    const fx = lerp(img.width / 2, p.focus[0] * unit, lean);
    const fy = lerp(img.height / 2, p.focus[1] * unit, lean);
    const sx = Math.min(img.width - sw, Math.max(0, fx - sw / 2));
    const sy = Math.min(img.height - sh, Math.max(0, fy - sh / 2));
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(img, sx, sy, sw, sh, x, y, w, h);
  } else {
    ctx.fillStyle = '#1b1e20';
    ctx.fillRect(x, y, w, h);
    ctx.fillStyle = '#6b7275';
    ctx.font = '500 28px Oswald';
    ctx.textAlign = 'center';
    ctx.fillText(`Falta la captura «${p.name}» (tools/trailer/capturas.mjs)`, cx, cy);
  }
  if (dim > 0) {
    ctx.fillStyle = `rgba(0, 0, 0, ${0.6 * dim})`;
    ctx.fillRect(x, y, w, h);
  }
  ctx.restore();
  // A fine light rim, as on the game's plates.
  ctx.strokeStyle = 'rgba(255, 255, 255, 0.14)';
  ctx.lineWidth = 1.5;
  ctx.strokeRect(x + 0.75, y + 0.75, w - 1.5, h - 1.5);
  ctx.restore();
}
