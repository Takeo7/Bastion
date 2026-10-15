"""
Copies the Synty interface sprites the interface uses («Mixta», ui/themes/)
from the original packs in assets-src/synty (not in the repository: the licence
forbids redistributing them) to packages/client/public/ui/synty/, downscaled
and as WebP. Only what is listed here is copied; run it again after editing.

  python3 tools/ui/synty-sprites.py [name …]   (needs Pillow)

With names (the keys below: «mh/rank-1», «biohazard», «mh-pin»…), only those are
written: the rest stay byte for byte as they are, whatever Pillow's version.
"""
import os
import sys
from PIL import Image, ImageFilter

ONLY = set(sys.argv[1:])


def want(name: str) -> bool:
    return not ONLY or name in ONLY

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
APOC = os.path.join(ROOT, 'assets-src/synty/INTERFACE_ApocalypseHUD_SourceFiles/Sprites')
MM = os.path.join(ROOT, 'assets-src/synty/INTERFACE_Modern_Menus_SourceFiles_v1/Sprites')
MH = os.path.join(ROOT, 'assets-src/synty/INTERFACE_MilitaryCombatHUD_SourceFiles/Sprites')
OUT = os.path.join(ROOT, 'packages/client/public/ui/synty')

# out name: (source, longest side in px)
SPRITES = {
    # INTERFACE Military Combat HUD: the rank badges (ui/themes/mixta.css), bronze to gold.
    'mh/rank-1': (f'{MH}/Badges/SPR_MilitaryCombat_Badge_Chevron_05_Bronze.png', 64),
    'mh/rank-2': (f'{MH}/Badges/SPR_MilitaryCombat_Badge_Chevron_06_Bronze.png', 64),
    'mh/rank-3': (f'{MH}/Badges/SPR_MilitaryCombat_Badge_Chevron_07_Bronze.png', 64),
    'mh/rank-4': (f'{MH}/Badges/SPR_MilitaryCombat_Badge_Chevron_01_Silver.png', 64),
    'mh/rank-5': (f'{MH}/Badges/SPR_MilitaryCombat_Badge_Chevron_02_Silver.png', 64),
    'mh/rank-6': (f'{MH}/Badges/SPR_MilitaryCombat_Badge_Chevron_03_Gold.png', 64),
    'mh/rank-7': (f'{MH}/Badges/SPR_MilitaryCombat_Badge_Chevron_04_Gold.png', 64),
}

# Military Combat HUD pieces turned to neutral steel (their teal glints would fight the hazard yellow).
STEEL = {
    'mh/dial': (f'{MH}/MilitaryCombat/SPR_MilitaryCombat_Dial_Medium_03.png', 256),
    # The panels' frames (playtest 15/10 #5), cut in nine (border-image): a plain bevel, and one with bolts and a handle.
    'mh/frame-a': (f'{MH}/MilitaryCombat/SPR_MilitaryCombat_Frame_Medium_01.png', 256),
    'mh/frame-b': (f'{MH}/MilitaryCombat/SPR_MilitaryCombat_Frame_Medium_05.png', 256),
}

# Flat white silhouettes, used as masks (coloured by the theme). out name: source.
ICONS = {
    'rifle': f'{APOC}/Icons_Weapons/ICON_SM_Wep_AssaultRifle_01_Clean.png',
    'pistol': f'{APOC}/Icons_Weapons/ICON_SM_Wep_Pistol_01_Clean.png',
    'sniper': f'{APOC}/Icons_Weapons/ICON_SM_Wep_SniperRifle_01_Clean.png',
    'launcher': f'{APOC}/Icons_Weapons/ICON_SM_Wep_Grenade_Launcher_01_Clean.png',
    'crossbow': f'{APOC}/Icons_Weapons/ICON_SM_Wep_CrossBow_01_Clean.png',
    'bow': f'{APOC}/Icons_Weapons/ICON_SM_Prop_Bow_01_Clean.png',
    'flaregun': f'{APOC}/Icons_Weapons/ICON_SM_Wep_FlareGun_01_Clean.png',
    'target': f'{APOC}/Icons_Map/ICON_Map_Target.png',
    'ammo': f'{APOC}/Icons_Map/ICON_Map_Ammo.png',
    'fire': f'{APOC}/Icons_Map/ICON_Map_Fire.png',
    'heal': f'{APOC}/Icons_Map/ICON_Map_Healing.png',
    'lightning': f'{APOC}/Icons_Map/ICON_Map_Lightning.png',
    'petrol': f'{APOC}/Icons_Map/ICON_Map_Petrol.png',
    'skull': f'{APOC}/Icons_Map/ICON_Map_Skull.png',
    'biohazard': f'{APOC}/Icons_Map/ICON_Map_Biohazard.png',
    'home': f'{APOC}/Icons_Map/ICON_Map_Home.png',
    'armor': f'{APOC}/Icons_Status/ICON_Status_Armor.png',
    'health': f'{APOC}/Icons_Status/ICON_Status_Health01.png',
    'bleeding': f'{APOC}/Icons_Status/ICON_Status_Bleeding.png',
    'backpack': f'{APOC}/Icons_Inventory/ICON_Inventory_Backpack.png',
    'crafting': f'{APOC}/Icons_Inventory/ICON_Inventory_Crafting.png',
    'explosives': f'{APOC}/Icons_Inventory/ICON_Inventory_Explosives.png',
    'medkit': f'{APOC}/Icons_Inventory/ICON_Inventory_Healing.png',
    'helmet': f'{APOC}/Icons_Inventory/ICON_Inventory_Helmets.png',
    'herbs': f'{APOC}/Icons_Inventory/ICON_Inventory_Herbs.png',
    'notes': f'{APOC}/Icons_Inventory/ICON_Inventory_Notes.png',
    'reload': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Reload_01_Clean.png',
    'fastforward': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_FastForward_01_Clean.png',
    'screwdriver': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Settings_01_Clean.png',
    'settings': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Settings_02_Clean.png',
    'sound': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Sound_01_On_Clean.png',
    'mainmenu': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_MainMenu_01_Clean.png',
    'notify': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Notification_01_On_Clean.png',
}

# INTERFACE Military Combat HUD: flat icons for what the other two packs lack, cut to their
# outline (and the long machete turned on its diagonal so it fills a square). The pack's 3D
# item renders (binoculars, syringe, dynamite) are used by their outline only.
MH_ICONS = {
    'mh-machete': (f'{MH}/Icons_Weapons/ICON_SM_Wep_Machete_01_Military_Clean.png', 38, 9),
    'mh-machinegun': (f'{MH}/Icons_Weapons/ICON_SM_Wep_MachineGun_Bandit_01_Military_Clean.png', 0),
    'mh-revolver': (f'{MH}/Icons_Weapons/ICON_SM_Wep_Pistol_Revolver_01_BattleRoyale_Clean.png', 0),
    'mh-drone': (f'{MH}/Icons_Special/ICON_MilitaryCombat_Special_Drone_02_Clean.png', 0),
    'mh-drone-heal': (f'{MH}/Icons_Special/ICON_MilitaryCombat_Special_DroneHeal_01_Clean.png', 0),
    'mh-drone-bomb': (f'{MH}/Icons_Special/ICON_MilitaryCombat_Special_DroneBomb_01_Clean.png', 0),
    'mh-accuracy': (f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Accuracy_01_Clean.png', 0),
    'mh-pulse': (f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Health_02_Clean.png', 0),
    'mh-mind': (f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Mind_01_Clean.png', 0),
    'mh-speed': (f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Speed_02_Clean.png', 0),
    'mh-runner': (f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Speed_01_Clean.png', 0),
    'mh-heart': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Health_01_Clean.png', 0),
    'mh-shield': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Defense_01_Clean.png', 0),
    'mh-shield-up': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_DefenseUp_01_Clean.png', 0),
    'mh-shield-broken': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_DefenseBroken_01_Clean.png', 0),
    'mh-shocked': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Shocked_01_Clean.png', 0),
    'mh-speedup': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_SpeedUp_01_Clean.png', 0),
    'mh-targeted': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Targeted_01_Clean.png', 0),
    'mh-up': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Up_01_Clean.png', 0),
    'mh-critical': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Critical_02_Clean.png', 0),
    'mh-dead': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Dead_01_Clean.png', 0),
    'mh-poisoned': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Poisoned_01_Clean.png', 0),
    'mh-smoke': (f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_GrenadeSmoke_01_Clean.png', 0),
    'mh-flashbang': (f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_GrenadeFlashbang_01_Clean.png', 0),
    'mh-bullets': (f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Ammo_Bullets_02_Clean.png', 0),
    'mh-repair': (f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Repair_01_Clean.png', 0),
    'mh-flag': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Flag_01_Clean.png', 0),
    'mh-binoculars': (f'{MH}/Icons_Resources/ICON_SM_Item_Binoculars_01_Military.png', 0),
    # The compound's plots (plotIcons.ts, data-plot).
    'mh-tent': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Home_02_Clean.png', 0),
    'mh-dumbbell': (f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Strength_01_Clean.png', 0),
    'mh-chip': (f'{MH}/Icons_Special/ICON_MilitaryCombat_Special_Microchip_01_Clean.png', 0),
    'mh-plants': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Plants_01_Clean.png', 0),
    'mh-comms': (f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Comms_01_Clean.png', 0),
    'mh-shop': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_ShopWeapons_01_Clean.png', 0),
    'mh-headphones': (f'{MH}/Icons_Resources/ICON_SM_Item_Earmuffs_01_Military.png', 0),
    'mh-pliers': (f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Crafting_01_Clean.png', 0),
    'mh-syringe': (f'{MH}/Icons_Resources/ICON_SM_Prop_Needle_01_BattleRoyale.png', 0),
    'mh-dynamite': (f'{MH}/Icons_Resources/ICON_SM_Chr_Attach_Bomb_Dynamite_01_Military.png', 0),
    # «Mixta»: the top bar's resources, the tabs and the skill tree's lock.
    'mh-crate': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Treasure_01_Clean.png', 0),
    'mh-medkit': (f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Healing_02_Clean.png', 0),
    'mh-xp': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_XP_01_Clean.png', 0),
    'mh-hourglass': (f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Time_01_Clean.png', 0),
    'mh-lock': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Lock_01_Clean.png', 0),
    'mh-pin': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Pin_01_Clean.png', 0),
    'mh-notes': (f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Notes_01_Clean.png', 0),
    # The map's markers and the partner's presence (playtest 15/10 #3, #5): one family for the valley and the bases.
    'map-pin': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Pin_01_Clean.png', 0),
    'map-player': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Player_01_Clean.png', 0),
    'map-home': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Home_01_Clean.png', 0),
    'map-vehicle': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Vehicle_01_Clean.png', 0),
    'map-skull': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Skull_01_Clean.png', 0),
    'map-unknown': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Unknown_01_Clean.png', 0),
    'map-quest': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Quest_01_Clean.png', 0),
    'map-objective': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Objective_01_Clean.png', 0),
    'map-danger': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Danger_01_Clean.png', 0),
    'map-target': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Target_01_Clean.png', 0),
    'map-treasure': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Treasure_01_Clean.png', 0),
    'map-message': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Message_01_Clean.png', 0),
    'map-lightning': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Lightning_01_Clean.png', 0),
    'map-healing': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Healing_01_Clean.png', 0),
    'map-star': (f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Star_01_Clean.png', 0),
}


def save(src: str, out: str, size: int) -> None:
    im = Image.open(src).convert('RGBA')
    im.thumbnail((size, size), Image.LANCZOS)
    path = os.path.join(OUT, out + '.webp')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path, 'WEBP', quality=90, method=6)


# Hazard tape, painted: the pack's white stripes over a dark ground, in the theme's colours.
TAPES = {'hud/hazard-yellow': ((242, 194, 48), (28, 22, 8)), 'hud/hazard-red': ((226, 74, 51), (24, 10, 8))}


def tape(out: str, stripe: tuple, ground: tuple) -> None:
    mask = Image.open(f'{APOC}/HUD/SPR_Background_HazardStripes.png').convert('RGBA').getchannel('A').resize((128, 128), Image.LANCZOS)
    im = Image.new('RGBA', mask.size, ground + (255,))
    im.paste(Image.new('RGBA', mask.size, stripe + (255,)), (0, 0), mask)
    path = os.path.join(OUT, out + '.webp')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path, 'WEBP', quality=90, method=6)


for out, (src, size) in SPRITES.items():
    if want(out):
        save(src, out, size)


def steel(src: str, out: str, size: int) -> None:
    """The piece in greys with a hint of warmth: its shading kept, its colour gone."""
    im = Image.open(src).convert('RGBA')
    im.thumbnail((size, size), Image.LANCZOS)
    grey = im.convert('L')
    tinted = Image.merge('RGB', (grey.point(lambda v: min(255, int(v * 1.04))), grey, grey.point(lambda v: int(v * 0.95))))
    tinted.putalpha(im.getchannel('A'))
    path = os.path.join(OUT, out + '.webp')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tinted.save(path, 'WEBP', quality=90, method=6)


for out, (src, size) in STEEL.items():
    if want(out):
        steel(src, out, size)
for out, (stripe, ground) in TAPES.items():
    if want(out):
        tape(out, stripe, ground)
for out, src in ICONS.items():
    if want(out):
        save(src, 'icons/' + out, 128)


def outline(src: str, out: str, turn: int, bold: int = 0, size: int = 128) -> None:
    """The icon's shape in white (the CSS uses it as a mask), turned, thickened and cut to its edges."""
    im = Image.open(src).convert('RGBA')
    if turn:
        im = im.rotate(turn, Image.BICUBIC, expand=True)
    alpha = im.getchannel('A')
    if bold:
        alpha = alpha.filter(ImageFilter.MaxFilter(bold))
    alpha = alpha.crop(alpha.getbbox())
    alpha.thumbnail((size, size), Image.LANCZOS)
    white = Image.new('RGBA', alpha.size, (255, 255, 255, 0))
    white.putalpha(alpha)
    path = os.path.join(OUT, out + '.webp')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    white.save(path, 'WEBP', quality=90, method=6)


for out, (src, turn, *bold) in MH_ICONS.items():
    if want(out):
        outline(src, 'icons/' + out, turn, *bold)


# Arte's renders of Synty models (public/ui/synty/renders, made by Arte-Personajes): their
# masks cut to their edges like the rest. La Mula's flat mask reads as a bar, so its windows
# and tyres (the dark and glass-blue pixels of the colour render) are cut out of it.
RENDERS = os.path.join(OUT, 'renders')
for name in ('camposanto', 'roca', 'ruinas', 'racion'):
    if want(f'r-{name}'):
        outline(f'{RENDERS}/{name}-mask.webp', f'icons/r-{name}', 0)


def windows_cut(src: str, out: str, size: int = 128) -> None:
    im = Image.open(src).convert('RGBA')
    alpha = im.getchannel('A')
    px, ap = im.load(), alpha.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a and (r + g + b < 170 or (g > r + 25 and b > r + 25)):
                ap[x, y] = 0
    alpha = alpha.crop(alpha.getbbox())
    alpha.thumbnail((size, size), Image.LANCZOS)
    white = Image.new('RGBA', alpha.size, (255, 255, 255, 0))
    white.putalpha(alpha)
    white.save(os.path.join(OUT, out + '.webp'), 'WEBP', quality=90, method=6)


if want('r-mula'):
    windows_cut(f'{RENDERS}/mula-side.webp', 'icons/r-mula')
total = sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk(OUT) for f in fs)
print(f'{len(SPRITES) + len(STEEL) + len(ICONS) + len(MH_ICONS)} sprites, {total // 1024} KB in {OUT}')
