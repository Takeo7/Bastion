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

// (Unity's own, Ana's #3 of 1/11, Arturo: houses of Town's pieces, not all alike): the kit's other windows, doors and
// open shutters, each house picking its own, and the roller garage door in its wall, four tiles wide, without the
// track inside it (WallLayout.HouseLook).
const HOUSE_PIECES: Record<string, Record<string, unknown>> = {
  houseWindow2: { pack: 'town', fbx: 'SM_Bld_House_Window_02', centre: [1.25, -0.1], run: 'x', out: -1 },
  houseWindow3: { pack: 'town', fbx: 'SM_Bld_House_Window_03', centre: [1.25, -0.1], run: 'x', out: -1 },
  houseWindow4: { pack: 'town', fbx: 'SM_Bld_House_Window_04', centre: [1.25, -0.1], run: 'x', out: -1 },
  houseWindow5: { pack: 'town', fbx: 'SM_Bld_House_Window_05', centre: [3.75, -0.1], run: 'x', out: -1, drop: ['Glass'] },
  houseShutters: { pack: 'town', fbx: 'SM_Bld_House_Window_Shutters_Open_01', centre: [1.25, -0.1], run: 'x', out: -1 },
  houseDoor2: { pack: 'town', fbx: 'SM_Bld_House_Door_02', centre: [1.25, -0.1], run: 'x', out: -1, parts: true, leaf: 'SM_Bld_House_Door' },
  houseDoor3: { pack: 'town', fbx: 'SM_Bld_House_Door_03', centre: [1.25, -0.1], run: 'x', out: -1, parts: true, leaf: 'SM_Bld_House_Door' },
  houseGarage: { pack: 'town', fbx: 'SM_Bld_House_ExteriorWall_GarageDoor_01', centre: [2.5, -0.1], run: 'x', out: -1, keep: [[-50, -0.3, -0.02], [50, 0.05, 50]] },
};
const pieces: Record<string, Record<string, unknown>> = {};
const missing: string[] = [];
for (const [id, p] of Object.entries({ ...(m.PIECES as Record<string, Record<string, unknown>>), ...HOUSE_PIECES })) {
  const src = findFbx(p.pack as string, p.fbx as string);
  if (!src) missing.push(`${id} (${p.pack}/${p.fbx})`);
  pieces[id] = { ...p, src: src ? relative(root, src) : null };
}
const out = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/Pieces.json');
writeFileSync(out, JSON.stringify({ kitScale: m.KIT_SCALE, pieces }, null, 1) + '\n');
console.log(Object.keys(pieces).length, 'pieces ->', relative(root, out), missing.length ? `; no source: ${missing.join(', ')}` : '');
