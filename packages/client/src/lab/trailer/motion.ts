import '@fontsource/barlow/latin-600.css';
import '@fontsource/oswald/latin-500.css';
import '@fontsource/oswald/latin-600.css';
import { cut, fadeIn, fadeOut, frase, stamp, sweep, title, type Cue } from './cards';
import { drawLogo, setupLogo } from './logo';
import { H, span, W } from './paint';
import { screens, setupScreens } from './screens';
import { placement } from './script';
import type { MotionLayer } from './types';

// The trailer's motion graphics (docs/TRAILER.md): the layer the page (main.ts,
// Red's) captures. Each frame it copies the shot's image and draws the cues
// over it, all from the trailer's time, so a card or a sweep runs on across a
// cut and lands on the music's beats wherever the shots are placed
// (script.ts). Every cut happens under black or the band.

/** With `&alfa`, the shots of the layer alone (the logo) leave their ground clear. */
const CLEAR = new URLSearchParams(location.search).has('alfa');

/** The trailer's time `t` seconds into `shot`. */
function at(shot: string, t = 0): number {
  return (placement(shot)?.from ?? Number.NaN) + t;
}

// The words are Lore's (docs/TRAILER.md, «Guion»). The beats are Red's music,
// at 120 BPM: the hit at 0:20, the bars every 2 s from 0:24, the last accent
// at 1:06.
const CUES: Cue[] = [
  // 1–3: the story, the night's black opening on the street.
  fadeIn(at('calle'), at('calle', 1.6)),
  frase(['Hace dos inviernos,', 'la Pandemia vació el valle.'], at('calle', 1.3), at('calle', 5.7)),
  // The horde comes out of the haze at once; from 5 s the first of it reaches the pavement over the words.
  frase(['Los muertos siguen andando.'], at('horda', 0.8), at('horda', 4.9)),
  // Over the walls from above, on the second heartbeat; the hangar and La Lata after it are left clear.
  frase(['Atalaya aguanta.'], at('atalaya', 1.0), at('atalaya', 3.9)),
  // The half second of silence before the hit.
  cut(19.5, at('titulo')),
  // 4: the hit and the title.
  title(['Táctica por turnos.', 'Para dos.'], at('titulo'), at('titulo', 1.0), at('titulo', 4)),
  // 5: one call per third of the montage, each on a bar.
  stamp('Cada disparo *se oye.*', 26, 29.6),
  stamp('*Registrad* lo que podáis.', 32, 35.8),
  stamp('Volved *vivos.*', 38, 41.6),
  // Through black into the campaign, where the music opens up.
  fadeOut(43.55, 44, 44),
  fadeIn(44, 44.6),
  // High: below, the Marea swallows Corran and Galen (1.5–2.5 s).
  frase(['Construid.', 'Decidid los dos.'], at('valle', 0.6), at('valle', 3.8), 0.5, true),
  // 6: the Bastión's screens, the band in and out of them.
  screens(at('pantallas'), at('evacuacion')),
  sweep(at('pantallas', -0.45), at('pantallas'), at('pantallas', 0.45)),
  // 7: the band again into the climax, and the rule of the evacuation over whoever stays.
  sweep(at('evacuacion', -0.45), at('evacuacion'), at('evacuacion', 0.45)),
  // «se pierde.» comes as the one left out shows, against the wall (7 s).
  frase(['Quien se queda fuera,', 'se pierde.'], at('evacuacion', 5.9), at('evacuacion', 9.7), 1.1),
  // The music's dry cut before the logo's accent: black, like the silence before the title.
  cut(65.75, at('logo')),
  // 8: the logo, stamped on the last accent, holding while it rings, out to black at the end.
  logo(at('logo'), at('logo', 9)),
];

/** The logo shot: on a dark ground lifted in the middle (none with `&alfa`), fading out at the very end. */
function logo(from: number, to: number): Cue {
  return (ctx, time) => {
    if (time < from || time > to) return;
    const end = 1 - span(time, to - 0.75, to);
    if (!CLEAR) {
      const ground = ctx.createRadialGradient(W / 2, H * 0.47, 0, W / 2, H * 0.47, W * 0.55);
      ground.addColorStop(0, '#191c1e');
      ground.addColorStop(0.55, '#0a0c0d');
      ground.addColorStop(1, '#000');
      ctx.fillStyle = ground;
      ctx.fillRect(0, 0, W, H);
    }
    drawLogo(ctx, time - from, { cx: W / 2, cy: H * 0.47, scale: 1, opacity: CLEAR ? end : 1 });
    if (!CLEAR && end < 1) {
      ctx.fillStyle = `rgba(0, 0, 0, ${1 - end})`;
      ctx.fillRect(0, 0, W, H);
    }
  };
}

export function motionLayer(): MotionLayer {
  const canvas = Object.assign(document.createElement('canvas'), { width: W, height: H });
  // Drawn by the GPU: the Bastión's screens are scaled down from 3840×2160 every frame.
  const ctx = canvas.getContext('2d')!;
  return {
    canvas,
    async setup() {
      await Promise.all([document.fonts.load('600 56px Barlow'), document.fonts.load('500 40px Oswald'), document.fonts.load('600 132px Oswald'), setupLogo(), setupScreens()]);
    },
    draw(shot, t, scene) {
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      ctx.globalAlpha = 1;
      ctx.globalCompositeOperation = 'source-over';
      ctx.clearRect(0, 0, W, H);
      if (scene) ctx.drawImage(scene, 0, 0, W, H);
      else if (!CLEAR) {
        ctx.fillStyle = '#000';
        ctx.fillRect(0, 0, W, H);
      }
      // A shot outside the script (a test of Red's or Arte's) gets the image alone.
      const time = at(shot, t);
      if (!Number.isFinite(time)) return;
      for (const cue of CUES) {
        ctx.save();
        cue(ctx, time);
        ctx.restore();
      }
    },
  };
}
