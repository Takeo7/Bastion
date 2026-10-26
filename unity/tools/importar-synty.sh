#!/bin/bash
# The Synty packs the game uses, into unity/Bastion/Assets/Synty (docs/UNITY-ARTE.md §1), from the external disk:
# each pack's files with their .meta (upkg-extract.py), Alpine's two ground textures alone, the demo scenes and
# samples left out, and then Unity to import them (its log in Logs/import-synty.log). Assets/Synty stays off GitHub:
# this is run on each machine.
#   unity/tools/importar-synty.sh [only the packs named, by file name without .unitypackage…]
set -u
tools=$(cd "$(dirname "$0")" && pwd)
project="$tools/../Bastion"
synty=${SYNTY:-/Volumes/4T_White/50_INBOX/Synty}
sidekicks=${SIDEKICKS:-$tools/../../assets-src/sidekicks-tool/1.2.8}
packs=(
  "POLYGON - Apocalypse Pack/Unity/POLYGON_Apocalypse_Unity_2022_3_v1_19_4.unitypackage"
  "POLYGON - Town Pack/Unity/POLYGON_Town_Unity_2022_3_v1_9_2.unitypackage"
  "POLYGON - City Pack/Unity/POLYGON_City_Unity_2022_3_v1_12_5.unitypackage"
  "POLYGON - Office Pack/Unity/POLYGON_Office_Unity_2022_3_v1_9_4.unitypackage"
  "POLYGON - Military Pack/Unity/POLYGON_Military_Unity_2022_3_v1_10_4.unitypackage"
  "POLYGON - Construction Pack/Unity/POLYGON_Construction_Unity_2022_3_v1_5_2.unitypackage"
  "POLYGON - Nature Pack/Unity/POLYGON_Nature_Unity_2022_3_v1_2_1.unitypackage"
  "POLYGON - Military Warehouse Map/Unity/POLYGON_Military_Warehouse_Map_Unity_2022_3_v1_0_2.unitypackage"
  "POLYGON - City Zombies Pack/Unity/POLYGON_City_Zombies_Unity_2022_3_v1_7_2.unitypackage"
  "POLYGON - Boss Zombies/Unity/POLYGON_BossZombies_Unity_2022_3_v1_4_1.unitypackage"
  "POLYGON - Particle FX Pack/Unity/POLYGON_Particle_FX_Unity_2022_3_v1_4_1.unitypackage"
  "POLYGON - Farm Pack/Unity/POLYGON_Farm_Unity_2022_3_v1_7_3.unitypackage"
  "SIDEKICK - Starter/Unity/SIDEKICK_Starter_Unity_2021_3_v1_0_4.unitypackage"
  "SIDEKICK - Apocalypse Survivors/Unity/SIDEKICK_Apocalypse_Survivors_Unity_2021_3_v1_0_4.unitypackage"
  "SIDEKICK - Apocalypse Zombies/Unity/SIDEKICK_Apocalypse_Zombies_Unity_2021_3_v1_0_3.unitypackage"
  "SIDEKICK - Apocalypse Outlaws/Unity/SIDEKICK_Apocalypse_Outlaws_Unity_2021_3_v1_0_6.unitypackage"
  "SIDEKICK - Modern Civilians/Unity/SIDEKICK_Modern_Civilians_Unity_2021_3_v1_0_2.unitypackage"
  "SIDEKICK - Modern Police/Unity/SIDEKICK_Modern_Police_Unity_2021_3_v1_0_0.unitypackage"
  "$sidekicks/Sidekicks.unitypackage"
  "$sidekicks/SidekicksDatabase.unitypackage"
  "ANIMATION - Base Locomotion/Unity/ANIMATION_Base_Locomotion_Unity_2021_1_v1_1_3.unitypackage"
  "ANIMATION - Idles/Unity/ANIMATION_Idles_Unity_2021_3_v1_1_2.unitypackage"
  "ANIMATION - Bow Combat/Unity/ANIMATION_Bow_Combat_Unity_2022_3_v1_0_0.unitypackage"
  "ANIMATION - Sword Combat/Unity/ANIMATION_Sword_Combat_Unity_2021_1_v1_2_0.unitypackage"
  "ANIMATION - Goblin Locomotion/Unity/ANIMATION_Goblin_Locomotion_Unity_2022_3_v1_0_1.unitypackage"
)
wanted() { [ $# -eq 0 ] || printf '%s\n' "$@" | grep -qx "$name"; }
failed=0
# Out of the package files by hand, not with Unity's -importPackage: Unity refuses to start in batchmode while any
# script fails to compile (the engine half ported, 23/10), and the pack then never goes in. Unity imports them below.
for p in "${packs[@]}"; do
  case "$p" in /*) path=$p ;; *) path=$synty/$p ;; esac
  name=$(basename "$path" .unitypackage)
  wanted "$@" || continue
  [ -f "$path" ] || { echo "== $name: no está ($path)"; failed=1; continue; }
  echo "== $name"
  python3 -I "$tools/upkg-extract.py" "$path" "$project" || failed=1
done
# Alpine Mountain: only the ground's grass and dirt (300 MB of pack for them).
name=POLYGON_NatureBiomes_AlpineMountain_Unity_2022_3_v1_5_5
if wanted "$@"; then
  echo "== $name (dos texturas)"
  python3 -I "$tools/upkg-extract.py" "$synty/POLYGON - Alpine Mountain - Nature Biome/Unity/$name.unitypackage" "$project" \
    Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain/Textures/Grass_Texture_01.png \
    Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain/Textures/Dirt_Texture_01.png || failed=1
fi
# Emotes and Taunts: only the two gestures the camp's talkers make (clearingLife.ts SAYS: a shrug, calming down), a
# woman's and a man's; the rest of the pack brings older copies of Sidekick's and Generic's shaders.
name=ANIMATION_Emotes_And_Taunts_Unity_2022_3_v1_0_0
if wanted "$@"; then
  echo "== $name (dos gestos)"
  e=Assets/Synty/AnimationEmotesAndTaunts/Animations/Polygon
  python3 -I "$tools/upkg-extract.py" "$synty/ANIMATION - Emotes and Taunts/Unity/$name.unitypackage" "$project" \
    $e/Masculine/Sad/A_POLY_EMOT_Sad_Shrug_Masc.fbx $e/Feminine/Sad/A_POLY_EMOT_Sad_Shrug_Femn.fbx \
    $e/Masculine/Reproach/A_POLY_EMOT_Reproach_CalmDown_Masc.fbx $e/Feminine/Reproach/A_POLY_EMOT_Reproach_CalmDown_Femn.fbx || failed=1
fi
# The HUD's icons (UI): three INTERFACE packs, only their sprites and textures (no prefabs, scripts or samples: the HUD
# is UI Toolkit, not their uGUI).
for p in "INTERFACE - Apocalypse HUD/Unity/INTERFACE_Apocalypse_HUD_Unity_2022_3_v1_1_1.unitypackage" \
  "INTERFACE - Military Combat HUD/Unity/INTERFACE_Military_Combat_HUD_Unity_2022_3_v1_1_1.unitypackage" \
  "INTERFACE - Modern Menus/Unity/INTERFACE_Modern_Menus_Unity_2022_3_v1_0_2.unitypackage"; do
  name=$(basename "$p" .unitypackage)
  wanted "$@" || continue
  echo "== $name (sprites y texturas)"
  python3 -I "$tools/upkg-extract.py" --only Sprites,Textures "$synty/$p" "$project" || failed=1
done
# Demo scenes and samples: not ours, and the samples want packages we don't use (left out above; these from before).
find "$project/Assets/Synty" -mindepth 2 -maxdepth 2 -type d \( -name Scenes -o -name Samples \) | while read -r d; do
  echo "fuera: ${d#$project/}"
  rm -rf "$d" "$d.meta"
done
# Unity imports what came in (if the scripts don't compile, it does so the next time they do).
echo "== Unity"
LOG="$project/Logs/import-synty.log" "$tools/unity.sh" || true
exit $failed
