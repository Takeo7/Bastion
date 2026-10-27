// The map's art for Unity (docs/UNITY-ARTE.md): 0.10.1's props (syntyManifest.ts PROPS: each one's Synty model and how
// it is fitted to its tile), prop sets (PROP_SETS) and what an object over several tiles is fitted to (FOOTPRINT_FIT), to Assets/Bastion/Client/Resources/MapArt.json. Names and
// numbers only, nothing of Synty's.   cd unity/tools && npx tsx map-art-json.mts
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
const m = await import(join(root, 'packages/client/src/game/syntyManifest.ts'));
const keep = ['pack', 'fbx', 'fit', 'stretch', 'thick', 'yaw', 'shift', 'origin', 'deck', 'sink', 'tall', 'atlas'];
const props: Record<string, Record<string, unknown>> = {};
for (const [id, a] of Object.entries(m.PROPS as Record<string, Record<string, unknown>>)) {
  props[id] = Object.fromEntries(keep.filter((k) => a[k] !== undefined).map((k) => [k, a[k]]));
  // A model with faces taken out (`hole` alone: the shelves emptied as they were stripped) is a model of its own, cut
  // by SourceProps into Assets/Synty/Extra under this name (the guard tower, cut with `keep` too, is SourceProps' own).
  if (a.hole && !a.keep) props[id] = { ...props[id], fbx: `${a.fbx}_${id}`, cut: { source: a.fbx, hole: a.hole } };
}
// (Unity's own: a wardrobe searched, Town's with its doors and drawers apart, placed as the shut one is; 0.10.1 drew
// it as the kit piece wardrobeOpen, mapView.ts openWardrobe. MapBuilder.Ransack swings them open.)
props.wardrobeOpen = { ...props.wardrobe, fbx: 'SM_Prop_Wardrobe_01' };
const out = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/MapArt.json');
writeFileSync(out, JSON.stringify({ props, sets: m.PROP_SETS, footprints: m.FOOTPRINT_FIT }, null, 1) + '\n');
console.log(Object.keys(props).length, 'props,', Object.keys(m.PROP_SETS).length, 'sets ->', out.slice(root.length + 1));
