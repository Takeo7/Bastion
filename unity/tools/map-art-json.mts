// The map's art for Unity (docs/UNITY-ARTE.md): 0.10.1's props (syntyManifest.ts PROPS: each one's Synty model and how
// it is fitted to its tile), prop sets (PROP_SETS) and what an object over several tiles is fitted to (FOOTPRINT_FIT), to Assets/Bastion/Client/Resources/MapArt.json. Names and
// numbers only, nothing of Synty's.   cd unity/tools && npx tsx map-art-json.mts
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
const m = await import(join(root, 'packages/client/src/game/syntyManifest.ts'));
const keep = ['pack', 'fbx', 'fit', 'stretch', 'thick', 'yaw', 'shift', 'origin', 'deck', 'sink', 'tall'];
const props: Record<string, Record<string, unknown>> = {};
for (const [id, a] of Object.entries(m.PROPS as Record<string, Record<string, unknown>>)) {
  props[id] = Object.fromEntries(keep.filter((k) => a[k] !== undefined).map((k) => [k, a[k]]));
}
const out = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/MapArt.json');
writeFileSync(out, JSON.stringify({ props, sets: m.PROP_SETS, footprints: m.FOOTPRINT_FIT }, null, 1) + '\n');
console.log(Object.keys(props).length, 'props,', Object.keys(m.PROP_SETS).length, 'sets ->', out.slice(root.length + 1));
