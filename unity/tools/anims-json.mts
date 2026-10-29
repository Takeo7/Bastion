// The figures' clips for Unity (docs/UNITY-ARTE.md): 0.10.1's animations (syntyManifest.ts ANIMATIONS: each one's pack
// and file), which clip plays each part for each build (MOTIONS, DEAD_MOTIONS, IDLE_VARIATIONS, STANCES, GESTURES,
// DEATHS, HITS, ACTIONS, ARMED_MOTIONS, RIFLE_RESTS, FEED_CLIPS; the camp's: CAMP_LOOPS, CAMP_VARIATIONS, CAMP_MOVES,
// CAMP_PARTS, CAMP_SPANS, CAMP_FREE, CAMP_ITEMS) and the weapons (WEAPONS, UNIT_WEAPONS), to
// Assets/Bastion/Client/Resources/Anims.json. Names and numbers only, nothing of Synty's or Mixamo's.
//   cd unity/tools && npx tsx anims-json.mts
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
const m = await import(join(root, 'packages/client/src/game/syntyManifest.ts'));
const keep = ['pack', 'fbx', 'mirror', 'inPlace'];
const clips: Record<string, Record<string, unknown>> = {};
for (const [id, a] of Object.entries(m.ANIMATIONS as Record<string, Record<string, unknown>>)) {
  clips[id] = Object.fromEntries(keep.filter((k) => a[k] !== undefined).map((k) => [k, a[k]]));
}
// (Unity's own, Gameplay 29/10 on Ana's #10: a blow from above for the pan and the crowbar, Mixamo's axe brought down.)
clips.macheteChop = { pack: 'mixamo', fbx: 'Standing Melee Attack Downward' };
const out = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/Anims.json');
const anims = {
  clips,
  motionOrder: ['idle', 'crouchIdle', 'walk', 'run', 'crouchWalk', 'sprint'],
  motions: m.MOTIONS,
  deadMotions: m.DEAD_MOTIONS,
  idleVariations: m.IDLE_VARIATIONS,
  stanceOrder: m.STANCE_ORDER,
  stances: m.STANCES,
  gestureOrder: m.GESTURE_ORDER,
  gestures: m.GESTURES,
  deaths: m.DEATHS,
  hits: m.HITS,
  actions: m.ACTIONS,
  armedMotions: m.ARMED_MOTIONS,
  rifleRests: m.RIFLE_RESTS,
  feedClips: m.FEED_CLIPS,
  // The camp (the Asentamiento's soldiers about the clearing: clearingLife.ts): what each stays in, does now and then,
  // the moves into and out of those and into La Lata (a clip's id, or a part of one: `${id}In`, `${id}Out`), the parts
  // and spans cut from the clips, the moves whose hips go free, and what is held drinking or eating.
  campLoops: m.CAMP_LOOPS,
  campVariations: m.CAMP_VARIATIONS,
  campMoves: m.CAMP_MOVES,
  campParts: m.CAMP_PARTS,
  campSpans: m.CAMP_SPANS,
  campFree: [...m.CAMP_FREE],
  campItems: m.CAMP_ITEMS,
  // The guns and blades (WEAPONS: each one's model, where the support hand holds it, its muzzle) and the one each
  // template carries by weapon tier (UNIT_WEAPONS).
  weapons: Object.fromEntries(Object.entries(m.WEAPONS as Record<string, Record<string, unknown>>).map(([id, w]) => [id, Object.fromEntries(Object.entries(w).filter(([k]) => k !== 'pack'))])),
  unitWeapons: m.UNIT_WEAPONS,
};
// (Unity's own, Ana's #10, Arturo 29/10: four blades more, in the belt's slot as the machete; Apocalypse's models, at
// the size the machete's has against them. `grip` where the fist closes, `along` the way it runs from there, in the
// model's own frame: the pan by the end of its handle, the pan itself ahead; the bat a hand over its knob; the crowbar
// by its straight end, the claw ahead; the chainsaw by its back handle, its other hand on the one over its body
// (`two`), and across the back, too big for the belt. The pan and the crowbar strike from above (`stroke`, macheteChop), the
// bat and the chainsaw across as the machete.)
Object.assign(anims.weapons, {
  pan: { fbx: 'apocalypse:SM_Prop_Pan_01', melee: true, size: 0.75, grip: [-0.4, 0.07, 0], along: [1, 0, 0], stroke: 'chop' },
  bat: { fbx: 'SM_Wep_Bat_Wood_01', melee: true, size: 0.8, grip: [0, 0, 0.1] },
  crowbar: { fbx: 'SM_Wep_Crowbar_01', melee: true, size: 0.65, grip: [0, 0, -0.4], stroke: 'chop' },
  chainsaw: { fbx: 'SM_Wep_ChainSaw_01', melee: true, size: 0.75, grip: [-0.06, 0.02, 0], two: [-0.06, 0.15, 0.28], slung: true },
});
writeFileSync(out, JSON.stringify(anims, null, 1) + '\n');
console.log(Object.keys(clips).length, 'clips ->', out.slice(root.length + 1));
