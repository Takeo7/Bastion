import '@fontsource/barlow/latin-700.css';
import { paintValley, tileExists, tileView, VALLEY_TILES, type View } from '../strategy/valleyArt';

// Valley map lab (/valle-mapas.html): the valley's ground as the game bakes it
// (strategy/valleyArt.ts; Arturo chose «el Ilustrado» of the three directions
// on 15/10). `__mapas.view(cx, cz, k)` paints a part of it (k pixels a unit;
// 48 the whole valley on a 1440-wide screen); `__mapas.bake()` paints every
// tile of every level (VALLEY_TILES: the sharpest only over the valley) and
// sends each to the dev server's /__snap as `valle-<level>-<i>-<j>` (WebP
// inside the .jpg), to be copied to public/maps/valle/<level>-<i>-<j>.webp.

const canvas = document.createElement('canvas');
canvas.style.cssText = 'display: block; max-width: 100vw; max-height: 100vh; margin: auto';
document.body.append(canvas);

function view(cx = 0, cz = 0.75, k = 48, w = 1440, h = 900): void {
  paintValley(canvas, { cx, cz, k, w, h } satisfies View);
}

async function bake(levels = VALLEY_TILES.levels.map((_, l) => l)): Promise<string[]> {
  const sent: string[] = [];
  for (const level of levels) {
    const n = VALLEY_TILES.levels[level]!;
    for (let j = 0; j < n; j++)
      for (let i = 0; i < n; i++) {
        if (!tileExists(level, i, j)) continue;
        paintValley(canvas, tileView(level, i, j));
        const name = `valle-${level}-${i}-${j}`;
        await fetch(`/__snap?name=${name}`, { method: 'POST', body: canvas.toDataURL('image/webp', level >= 2 ? 0.82 : 0.88) });
        sent.push(name);
      }
  }
  return sent;
}

void (async () => {
  await document.fonts.load('700 20px Barlow');
  view();
  (window as unknown as { __mapas: unknown }).__mapas = { view, bake };
})();
