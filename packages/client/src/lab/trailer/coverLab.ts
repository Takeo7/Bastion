import '@fontsource/barlow/latin-600.css';
import '@fontsource/oswald/latin-500.css';
import '@fontsource/oswald/latin-600.css';
import { drawCover, FORMATS } from './cover';
import { setupLogo } from './logo';
import { image } from './paint';

// portada.html: the cover in every size of Steam's, over Arte's key art
// (.dev-snapshots/trailer/portada/keyart.png), shown side by side.
//   ?arte=<url>   another image instead of the key art (a stand-in, to try)
//   ?guardar      also saves each one as .dev-snapshots/trailer/portada/<name>.png

const params = new URLSearchParams(location.search);
// The key art, if Arte has left it there yet.
const ART = Object.values(import.meta.glob<string>('../../../../../.dev-snapshots/trailer/portada/keyart.png', { query: '?url', import: 'default', eager: true }))[0];

/** To the trailer's receiver when it serves this page, else to the UX/UI vite's /__uiux-snap. */
async function save(name: string, canvas: HTMLCanvasElement): Promise<string> {
  const blob = await new Promise<Blob>((done) => canvas.toBlob((b) => done(b!), 'image/png'));
  const res = await fetch(`/__trailer/file?name=portada/${name}.png`, { method: 'POST', body: blob }).catch(() => null);
  if (res?.ok) return `portada/${name}.png`;
  return (await fetch(`/__uiux-snap?name=trailer/portada/${name}.png`, { method: 'POST', body: blob })).text();
}

async function main(): Promise<void> {
  await Promise.all([document.fonts.load('600 56px Barlow'), document.fonts.load('500 40px Oswald'), document.fonts.load('600 132px Oswald'), setupLogo()]);
  const src = params.get('arte') ?? ART;
  const art = src ? await image(src) : null;
  const note = document.createElement('p');
  note.textContent = art ? `Imagen: ${src}` : 'Sin key art todavía (.dev-snapshots/trailer/portada/keyart.png): solo el logo.';
  document.body.append(note);
  const saved: string[] = [];
  for (const format of FORMATS) {
    const canvas = drawCover(format, art);
    const box = document.createElement('figure');
    box.style.cssText = 'display: inline-block; margin: 0 16px 16px 0; vertical-align: top';
    canvas.style.cssText = `display: block; width: ${Math.min(format.w, 480)}px; background: repeating-conic-gradient(#333 0 25%, #444 0 50%) 0 0 / 16px 16px`;
    const caption = document.createElement('figcaption');
    caption.textContent = `${format.name}${format.clear ? ' (transparente)' : ''}`;
    box.append(canvas, caption);
    document.body.append(box);
    if (params.has('guardar')) saved.push(await save(format.name, canvas));
  }
  Object.assign(window, { __cover: { saved } });
}

void main();
