// The figures' clips for Unity (docs/UNITY-ARTE.md): 0.10.1's animations (syntyManifest.ts ANIMATIONS: each one's pack
// and file), which clip plays each part for each build (MOTIONS, DEAD_MOTIONS, IDLE_VARIATIONS, STANCES, GESTURES,
// DEATHS, HITS, ACTIONS, ARMED_MOTIONS, RIFLE_RESTS, FEED_CLIPS) and the weapons (WEAPONS, UNIT_WEAPONS), to
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
  // The guns and blades (WEAPONS: each one's model, where the support hand holds it, its muzzle) and the one each
  // template carries by weapon tier (UNIT_WEAPONS).
  weapons: Object.fromEntries(Object.entries(m.WEAPONS as Record<string, Record<string, unknown>>).map(([id, w]) => [id, Object.fromEntries(Object.entries(w).filter(([k]) => k !== 'pack'))])),
  unitWeapons: m.UNIT_WEAPONS,
};
writeFileSync(out, JSON.stringify(anims, null, 1) + '\n');
console.log(Object.keys(clips).length, 'clips ->', out.slice(root.length + 1));
