// The buildings' kit pieces for Unity (docs/UNITY-ARTE.md): 0.10.1's syntyManifest.ts PIECES (each one's pack and FBX,
// the box of it kept, how it sits in a tile: centre, run, out, unit, storey, base, its door leaf) with the source FBX
// each comes from (assets-src/synty, as convert.ts findFbx), to Assets/Bastion/Client/Resources/Pieces.json. Names and
// numbers only, nothing of Synty's. pieces-cut.py cuts them with Blender from this.
//   cd unity/tools && npx tsx pieces-json.mts
import { existsSync, readdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { basename, dirname, join, relative } from 'node:path';
const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
const m = await import(join(root, 'packages/client/src/game/syntyManifest.ts'));
const sources = join(root, 'assets-src/synty');

const index = new Map<string, Map<string, string>>();
function findFbx(pack: string, name: string): string | null {
  const dir = join(sources, m.PACKS[pack].dir);
  if (name.includes('/')) {
    const path = join(dir, `${name}.fbx`);
    return existsSync(path) ? path : null;
  }
  let found = index.get(pack);
  if (!found) {
    found = new Map();
    const walk = (d: string) => {
      for (const entry of readdirSync(d, { withFileTypes: true })) {
        const path = join(d, entry.name);
        if (entry.isDirectory()) walk(path);
        else if (/\.fbx$/i.test(entry.name)) {
          const key = basename(entry.name, '.fbx');
          // As convert.ts: Unreal's and the collision meshes' copies only if there is no other.
          const worse = /unreal|unsupported|[\\/]collision[\\/]/i.test(path);
          if (!found!.has(key) || !worse) found!.set(key, path);
        }
      }
    };
    if (existsSync(dir)) walk(dir);
    index.set(pack, found);
  }
  return found.get(name) ?? null;
}

const pieces: Record<string, Record<string, unknown>> = {};
const missing: string[] = [];
for (const [id, p] of Object.entries(m.PIECES as Record<string, Record<string, unknown>>)) {
  const src = findFbx(p.pack as string, p.fbx as string);
  if (!src) missing.push(`${id} (${p.pack}/${p.fbx})`);
  pieces[id] = { ...p, src: src ? relative(root, src) : null };
}
const out = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/Pieces.json');
writeFileSync(out, JSON.stringify({ kitScale: m.KIT_SCALE, pieces }, null, 1) + '\n');
console.log(Object.keys(pieces).length, 'pieces ->', relative(root, out), missing.length ? `; no source: ${missing.join(', ')}` : '');
