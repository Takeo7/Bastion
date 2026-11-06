// RETIRED (5/11, docs/BIBLIOTECA.md §5): Assets/Bastion/Client/Resources/Anims.json is the source, changed by hand; this script stays as history. To compare, run a copy that writes elsewhere, never over the list.
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
// (Unity's own, Arturo 1/11: soldiers with no gun at all wait with their blade held low, Sword Combat's one-handed
// sword's, not the pistol's; they walk and run in Synty's own, the blade in the hand: UnitViews.StanceOf.)
clips.bladeIdle = { pack: 'swordCombat', fbx: 'Animations/Polygon/Idle/Base/A_Idle_Base_Sword' };
// (Unity's own, Ana's #1 of 1/11: hiding, down onto a knee with the rifle or the pistol in hand: UnitViews.Hide.)
clips.rifleKneelDown = { pack: 'mixamo', fbx: 'Rifle Stand To Kneel' };
clips.pistolKneelDown = { pack: 'mixamo', fbx: 'Pistol Stand To Kneel' };
const out = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/Anims.json');
const anims = {
  clips,
  motionOrder: ['idle', 'crouchIdle', 'walk', 'run', 'crouchWalk', 'sprint'],
  motions: m.MOTIONS,
  deadMotions: m.DEAD_MOTIONS,
  idleVariations: m.IDLE_VARIATIONS,
  stanceOrder: m.STANCE_ORDER,
  stances: Object.fromEntries(Object.entries(m.STANCES as Record<string, Record<string, string>>).map(([build, s]) => [build, { ...s, blade: 'bladeIdle' }])),
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
// (Unity's own, Ana's B5 and Arturo, 2/11: four more found, in the same slot. The katana from Apocalypse, its fist at its
// guard and its blade along +z as the machete's; Gang Warfare's straight sword (its sheath left off: UnitViews.Arms), its
// grip at its origin and its blade up; Farm's rake hung from the top of its handle, held a third of the way down and its
// other hand lower, struck from above; Apocalypse's stick, held by an end. The long ones across the back.)
Object.assign(anims.weapons, {
  katana: { fbx: 'apocalypse:SM_Wep_Katana_01', melee: true, size: 1.3, slung: true },
  longsword: { fbx: 'gang:SM_Wep_Sword_02', melee: true, size: 1.15, grip: [0, 0, 0], along: [0, 1, 0], slung: true },
  rake: { fbx: 'farm:SM_Prop_Tool_Rake_01', melee: true, size: 1, grip: [0, -0.35, 0], along: [0, -1, 0], two: [0, -0.75, 0], slung: true, stroke: 'chop' },
  stick: { fbx: 'apocalypse:SM_Item_Stick_01', melee: true, size: 1.4, grip: [0, -0.22, 0], along: [0, 1, 0] },
});
writeFileSync(out, JSON.stringify(anims, null, 1) + '\n');
console.log(Object.keys(clips).length, 'clips ->', out.slice(root.length + 1));
