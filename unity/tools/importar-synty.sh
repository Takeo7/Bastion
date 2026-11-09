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
# Fantasy Menus: only the button's ground under each enemy's one gauge, its health and its eye (Ana, 7/11 #1; UI).
name=INTERFACE_Fantasy_Menus_Unity_2022_3_v1_1_1
if wanted "$@"; then
  echo "== $name (un sprite)"
  python3 -I "$tools/upkg-extract.py" "$synty/INTERFACE - Fantasy Menus/Unity/$name.unitypackage" "$project" \
    Assets/Synty/InterfaceFantasyMenus/Sprites/FantasyMenus/SPR_FantasyMenus_Menu_Button_08_Background.png || failed=1
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
# Woodland Apocalypse Map: its own folder alone (MapArt's fences, log piles, signs, lorries and containers), not the
# copy of PolygonGeneric it brings (the other packs' is already in, and its shaders are shared) nor its demo scenes.
name=POLYGON_Woodland_Apocalypse_Map_Unity_2022_3_v1_0_1
if wanted "$@"; then
  echo "== $name (su carpeta)"
  python3 -I "$tools/upkg-extract.py" --under Assets/Synty/PolygonMapsWoodlandApocalypse "$synty/POLYGON - Woodland Apocalypse Map/Unity/$name.unitypackage" "$project" || failed=1
fi
# Horror Asylum (Arturo, 9/11): its four patients, the orderly and the nurse alone, the dead of clinics and hospitals
# (Unit.Outfit patient: UnitViews.Patients), with the meshes, materials and textures they use (--refs), not the rest
# of the pack (its rooms, props and other people).
name=POLYGON_Horror_Asylum_Unity_2022_3_v1_1_2
if wanted "$@"; then
  echo "== $name (pacientes, celador y enfermera)"
  a=Assets/Synty/PolygonHorrorAsylum/Prefabs/Characters
  python3 -I "$tools/upkg-extract.py" --refs "$synty/POLYGON - Horror Asylum/Unity/$name.unitypackage" "$project" \
    $a/SM_Chr_Patient_Male_01.prefab $a/SM_Chr_Patient_Male_02.prefab $a/SM_Chr_Patient_Male_03.prefab \
    $a/SM_Chr_Patient_Female_01.prefab $a/SM_Chr_Orderly_01.prefab $a/SM_Chr_Nurse_01.prefab || failed=1
fi
# The city's buildings beyond Town, City and Office (Ana's #3, 1/11; Arturo: houses first, then shops, offices and
# industry): Shops, Gang Warfare and Palm City, each its own folder alone. Not the copy of PolygonGeneric each brings,
# nor Palm City's PNB_Core (its palms' foliage shader, with editor scripts), nor Palm City's one script (a sign's eye).
# And the Dog Pack (Gameplay, 1/11: animals about the map), its dogs, zombie dogs, fox and coyote with their clips,
# without its scripts (its demo's camera and controller).
for p in "POLYGON - Shops Pack/Unity/POLYGON_Shops_Unity_2022_3_v1_6_6.unitypackage:PolygonShops" \
  "POLYGON - Gang Warfare Pack/Unity/POLYGON_Gang_Warfare_Unity_2022_3_v1_7_2.unitypackage:PolygonGangWarfare" \
  "POLYGON - Palm City/Unity/POLYGON_Palm_City_Unity_2022_3_v1_0_3.unitypackage:PolygonPalmCity" \
  "POLYGON - Dog Pack/Unity/POLYGON_Dogs_Unity_2022_3_v1_1_1.unitypackage:PolygonDog"; do
  name=$(basename "${p%%:*}" .unitypackage)
  wanted "$@" || continue
  echo "== $name (su carpeta)"
  python3 -I "$tools/upkg-extract.py" --under "Assets/Synty/${p##*:}" "$synty/${p%%:*}" "$project" || failed=1
done
rm -rf "$project/Assets/Synty/PolygonPalmCity/Scripts" "$project/Assets/Synty/PolygonPalmCity/Scripts.meta" "$project/Assets/Synty/PolygonDog/Scripts" "$project/Assets/Synty/PolygonDog/Scripts.meta"
# SIMPLE - Forest Animals (Arturo, 1/11: its stags, does, boars and rabbits, which POLYGON hasn't; nothing else of SIMPLE,
# CLAUDE.md): under Assets/Synty with the rest (its own folder is Assets/SimpleForestAnimal), without its other animals.
name=SIMPLE_Forest_Animals_Unity_2020_3_v1_02
if wanted "$@"; then
  echo "== $name (ciervos, jabalíes y conejos)"
  python3 -I "$tools/upkg-extract.py" --move Assets/SimpleForestAnimal=Assets/Synty/SimpleForestAnimal --under Assets/SimpleForestAnimal \
    "$synty/SIMPLE - Forest Animals/Unity/$name.unitypackage" "$project" || failed=1
  for other in Bear Fox Moose Raccoon Skunk Wolf; do rm -f "$project/Assets/Synty/SimpleForestAnimal/Prefabs/$other"_* ; done
  rm -f "$project/Assets/Synty/SimpleForestAnimal/Models/"{Bear,Fox}.fbx* "$project/Assets/Synty/SimpleForestAnimal/Models/SFA_Animal_"{Bear,Fox}.controller*
fi
# SIMPLE - Cats (Ana's #11, 1/11; Arturo: its cat, the one animated there is, CLAUDE.md): under Assets/Synty too (its own
# folder is Assets/SimpleCats), its cats alone: not its props (bowls, litter trays, yarn…) nor its demo scene.
name=SIMPLE_Cats_Unity_2020_3_v1_0_1
if wanted "$@"; then
  echo "== $name (gatos)"
  python3 -I "$tools/upkg-extract.py" --move Assets/SimpleCats=Assets/Synty/SimpleCats --under Assets/SimpleCats \
    "$synty/SIMPLE - Cats/Unity/$name.unitypackage" "$project" || failed=1
  rm -rf "$project/Assets/Synty/SimpleCats/Scenes" "$project/Assets/Synty/SimpleCats/Scenes.meta"
  rm -f "$project/Assets/Synty/SimpleCats/Prefabs/SC_Prop_"* "$project/Assets/Synty/SimpleCats/Models/SM_Prop_"* "$project/Assets/Synty/SimpleCats/Materials/Ground.mat"*
fi
# The HUD's icons (UI): four INTERFACE packs, only their sprites and textures (no prefabs, scripts or samples: the HUD
# is UI Toolkit, not their uGUI). Dark Fantasy's for the life bars (Ana's #8, 29/10).
for p in "INTERFACE - Apocalypse HUD/Unity/INTERFACE_Apocalypse_HUD_Unity_2022_3_v1_1_1.unitypackage" \
  "INTERFACE - Military Combat HUD/Unity/INTERFACE_Military_Combat_HUD_Unity_2022_3_v1_1_1.unitypackage" \
  "INTERFACE - Modern Menus/Unity/INTERFACE_Modern_Menus_Unity_2022_3_v1_0_2.unitypackage" \
  "INTERFACE - Dark Fantasy HUD/Unity/INTERFACE_Dark_Fantasy_HUD_Unity_2022_3_v1_1_1.unitypackage"; do
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
