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
// (Unity's own too, Ana 28/10 p4: «no todos están destrozados»: the street's cars not all wrecks, Synty's whole ones of
// City, Town and Apocalypse among them, fitted as the wrecks are; `yaw` as each pack's models face. MapBuilder lifts the
// boot of a wreck alone when it is searched.)
const WHOLE_CARS: [id: string, pack: string, fbx: string, yaw: number][] = [
  ['carSedan', 'city', 'SM_Veh_Car_Sedan_01', 0],
  ['carSmall', 'city', 'SM_Veh_Car_Small_01', 0],
  ['carMedium', 'city', 'SM_Veh_Car_Medium_01', 0],
  ['carMuscle', 'city', 'SM_Veh_Car_Muscle_01', 0],
  ['carTaxi', 'city', 'SM_Veh_Car_Taxi_01', 0],
  ['carVan', 'city', 'SM_Veh_Car_Van_01', 0],
  ['carConvertible', 'town', 'SM_Veh_Convertable_01', 0],
  ['carPickup', 'town', 'SM_Veh_Pickup_01', 0],
  ['carUte', 'apocalypse', 'SM_Veh_Ute_01', 1],
  ['carHatch', 'apocalypse', 'SM_Veh_Car_Medium_01', 1],
];
// (Unity's own too, Gameplay 28/10: the sidewalks' bollards, the engine's decor 100, where a street tree in a pot stood:
// City's row of four posts, two of them knocked askew, along the kerb, its long side fitted to the tile and a little over.)
props.bollard = { pack: 'city', fbx: 'SM_Prop_SidewalkPoles_01', fit: 1.2, stretch: true, thick: 0.9 };
// (Unity's own too, Arturo 29/10 on Ana's #6: the Bastion's first stage, Apocalypse's camper armoured with planks, its
// roof fenced in, instead of the towed caravan; on the caravan's footprint, as long as it.)
props.caravan = { ...props.caravan, fbx: 'SM_Veh_Camper_01', fit: 4.6 };
const sets: Record<string, string[]> = { ...m.PROP_SETS };
const footprints: Record<string, unknown> = { ...m.FOOTPRINT_FIT };
for (const [id, pack, fbx, yaw] of WHOLE_CARS) {
  props[id] = { pack, fbx, fit: 1.95, ...(yaw ? { yaw } : {}) };
  footprints[id] = footprints.carWreck;
}
// Half of them wrecks, half whole (Gameplay 28/10: the 4 wrecks five times over, the 10 whole ones twice).
const wrecks = sets.carFront!;
sets.carFront = [...[1, 2, 3, 4, 5].flatMap(() => wrecks), ...[1, 2].flatMap(() => WHOLE_CARS.map(([id]) => id))];
const out = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/MapArt.json');
writeFileSync(out, JSON.stringify({ props, sets, footprints }, null, 1) + '\n');
console.log(Object.keys(props).length, 'props,', Object.keys(m.PROP_SETS).length, 'sets ->', out.slice(root.length + 1));
