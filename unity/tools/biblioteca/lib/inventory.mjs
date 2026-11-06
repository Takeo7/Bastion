// The packs on the external disk (docs/BIBLIOTECA.md §3): one folder each, with the manifest.json Synty's downloads left
// (the versions offered and their sizes), the icon in Extras and the source zips in SourceFiles. Kept in the cache
// (assets-src/biblioteca/inventario.json), so that with the disk away the page still lists them.
import { existsSync, readdirSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';

const LINE = /^(POLYGON MINI|POLYGON|SIDEKICK|SIMPLE|ANIMATION|INTERFACE) - (.+)$/;

function list(dir, test) {
  return existsSync(dir) ? readdirSync(dir).filter((f) => !f.startsWith('.') && test.test(f)) : [];
}

/** A version as Synty writes it in the manifest (Unity_2022_3 | v1_19_4), as 1.19.4. */
function version(texts) {
  const m = /\bv(\d+(?:_\d+)*)/.exec(texts.join(' '));
  return m ? m[1].replace(/_/g, '.') : null;
}

function readPack(synty, folder) {
  const dir = join(synty, folder);
  const [, line, title] = LINE.exec(folder);
  let offered = [];
  try { offered = JSON.parse(readFileSync(join(dir, 'manifest.json'), 'utf8')).offered ?? []; } catch { /* a pack with no manifest */ }
  const unity = offered.find((o) => o.kind === 'Unity');
  const sources = offered.filter((o) => o.kind === 'SourceFiles');
  const icons = list(join(dir, 'Extras'), /icon.*\.png$/i);
  return {
    id: folder,
    line,
    title,
    version: unity ? version(unity.texts) : null,
    unitySize: unity?.size_bytes_est ?? null,
    sourceSize: sources.reduce((s, o) => s + (o.size_bytes_est ?? 0), 0) || null,
    icon: icons.length ? `Extras/${icons[0]}` : null,
    unityPackages: list(join(dir, 'Unity'), /\.unitypackage$/i),
    sourceZips: list(join(dir, 'SourceFiles'), /\.zip$/i),
    // (The INTERFACE packs' images come apart, in Extras: INTERFACE_…_Source_Sprites_v3.zip.)
    spriteZips: list(join(dir, 'Extras'), /sprite.*\.zip$/i),
  };
}

/** The packs, read from the disk if it is there (and saved), else the last ones saved. */
export function readInventory(synty, cacheDir) {
  const saved = join(cacheDir, 'inventario.json');
  const mounted = existsSync(synty) && readdirSync(synty).some((f) => LINE.test(f));
  if (mounted) {
    const packs = readdirSync(synty).filter((f) => LINE.test(f)).sort().map((f) => readPack(synty, f));
    mkdirSync(dirname(saved), { recursive: true });
    writeFileSync(saved, JSON.stringify({ read: new Date().toISOString(), packs }, null, 1));
    return { mounted, read: null, packs };
  }
  if (existsSync(saved)) {
    const { read, packs } = JSON.parse(readFileSync(saved, 'utf8'));
    return { mounted, read, packs };
  }
  return { mounted, read: null, packs: [] };
}
