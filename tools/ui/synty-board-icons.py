"""
The hex board's icons (Fase 10; Arturo, 16/10: Synty's interface icons, not
drawn ones): one Synty INTERFACE sprite per cell, from the original packs in
assets-src/synty (not in the repository: the licence forbids redistributing
them), as two atlases in packages/client/public/ui/synty/board/ (grey.webp for
an unlocked cell, dark.webp for a selected one; 13 × 13 cells of 128 px, in
the order of ICONS) and the core's in the accent (core.webp). The cells' index
in the atlas is in strategy/boardIcons.ts, written here too.

  python3 tools/ui/synty-board-icons.py   (needs Pillow)

Each sprite becomes a silhouette (its alpha; a badge's soft glow is cut at
190); long thin ones (rifles, blades) are turned 40° to lie across the hexagon.
"""
import math
import os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
APOC = os.path.join(ROOT, 'assets-src/synty/INTERFACE_ApocalypseHUD_SourceFiles/Sprites')
MM = os.path.join(ROOT, 'assets-src/synty/INTERFACE_Modern_Menus_SourceFiles_v1/Sprites')
MH = os.path.join(ROOT, 'assets-src/synty/INTERFACE_MilitaryCombatHUD_SourceFiles/Sprites')
OUT = os.path.join(ROOT, 'packages/client/public/ui/synty/board')
TS = os.path.join(ROOT, 'packages/client/src/strategy/boardIconIndex.ts')

# cell id (packages/engine/src/board.ts): its sprite. The order is the atlas's.
ICONS = {
    'core': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Wolfshead_Silver.png',
    'steadyHand': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Accuracy_01_Clean.png',
    'lightTrigger': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Critical_01_Clean.png',
    'sidearm': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Pistols_01_Silver.png',
    'aimedShot': f'{APOC}/Icons_Weapons/ICON_SM_Wep_SniperRifle_01_Clean.png',
    'closeIn': f'{APOC}/Icons_Weapons/ICON_SM_Wep_Shotgun_01_Apoc_Clean.png',
    'fineTrigger': f'{APOC}/Icons_Weapons/ICON_SM_Wep_Pistol_Metal_01_Clean.png',
    'rapidFire': f'{APOC}/Icons_Map/ICON_Map_Ammo.png',
    'flanker': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Arrow_01_Clean.png',
    'rooftop': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Home_02_Clean.png',
    'coldBlood': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Cold_01_Clean.png',
    'chainShot': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Ammo_Bullets_02_Clean.png',
    'hollowPoint': f'{APOC}/Icons_Ammo/ICON_SM_Wep_Pistol_Ammo_01.png',
    'bigBore': f'{APOC}/Icons_Weapons/ICON_SM_Wep_Pistol_Heavy_01_Clean.png',
    'sentryCrit': f'{MH}/Icons_WeaponMods/ICON_SM_Wep_Mod_Scope_04_Military.png',
    'deathFromAbove': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Wings_04_Silver.png',
    'lightningHands': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_SpeedUp_01_Clean.png',
    'apRounds': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Ammo_Sniper_01_Clean.png',
    'shredder': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_DefenseDown_01_Clean.png',
    'serenity': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Confirm_01_Clean.png',
    'coveringFire': f'{MH}/Icons_Weapons/ICON_SM_Wep_MachineGun_USA_01_Military_Clean.png',
    'noEscape': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Cancel_02_Clean.png',
    'killZone': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Targeted_01_Clean.png',
    'faceoff': f'{APOC}/Icons_Weapons/ICON_SM_Wep_Pistol_Revolver_01_Clean.png',
    'rupture': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_DefenseBroken_01_Clean.png',
    'suppression': f'{MH}/Icons_Weapons/ICON_SM_Wep_MachineGun_Bandit_01_Military_Clean.png',
    'marker': f'{APOC}/HUD/SPR_HUD_Worldspace_Objective.png',
    'heavyHand': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_AttackUp_01_Clean.png',
    'eagleEye': f'{MH}/Icons_WeaponMods/ICON_SM_Wep_Mod_Scope_11_Military.png',
    'grip': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Fist_02_Silver.png',
    'edge': f'{MH}/Icons_Weapons/ICON_SM_Wep_Knife_01_Military_Clean.png',
    'sidestep': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Speed_03_Clean.png',
    'elbowRoom': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Machetes_01_Silver.png',
    'bladeCrit': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Critical_02_Clean.png',
    'closeRanks': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_CoatofArms_01_Silver.png',
    'implacable': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Death_03_Clean.png',
    'heavyBlow': f'{APOC}/Icons_Weapons/ICON_SM_Wep_Hammer_01_Clean.png',
    'parry': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Down_01_Clean.png',
    'grit': f'{APOC}/Apocalypse/SPR_Apocalypse_Greeble_KnuckleDusters01.png',
    'bladeMaster': f'{MH}/Icons_Weapons/ICON_SM_Wep_Knife_Kukri_01_Military_Clean.png',
    'earBlow': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Stars_01_Silver.png',
    'butcher': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Hunger_01_Clean.png',
    'bladeWall': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Cancel_01_Clean.png',
    'backstab': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Knife_02_Silver.png',
    'steelNerves': f'{MH}/Icons_Resources/ICON_SM_Chr_Attach_Earmuffs_01_BattleRoyale.png',
    'cleanCut': f'{MH}/Icons_Weapons/ICON_SM_Wep_Sword_01_Military_Clean.png',
    'waist': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Arrow_Back_01_Clean.png',
    'onGuard': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Sword_01_Silver.png',
    'hardHead': f'{APOC}/Icons_Armor/ICON_SM_Chr_Attach_FootballHelmet_01.png',
    'sweep': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Sword_02_Silver.png',
    'secondWind': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Thirst_01_Clean.png',
    'cleaver': f'{APOC}/Icons_Weapons/ICON_SM_Prop_Tool_Axe_01_Clean.png',
    'slippery': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Wet_01_Clean.png',
    'duelist': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Combat_01_Clean.png',
    'rampart': f'{APOC}/Icons_Armor/ICON_SM_PickUp_Armor_01.png',
    'bladeDance': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Shields_01_Clean.png',
    'killerInstinct': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_SkullandCrossbones_01_Silver.png',
    'looseArm': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Message_02_Clean.png',
    'sharpStone': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Currency_Gem_02_Clean.png',
    'curtain': f'{APOC}/Icons_Explosives/ICON_SM_Wep_Grenade_Smoke_01.png',
    'salvo': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Play_02_Clean.png',
    'bigCharge': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Explosive_02_Clean.png',
    'decoy': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Notification_01_On_Clean.png',
    'goodArm': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Strength_03_Clean.png',
    'shrapnel': f'{APOC}/Icons_Explosives/ICON_SM_Wep_NailBomb_01.png',
    'dazzle': f'{MH}/Icons_Resources/ICON_SM_Chr_Attach_Torch_01_Military.png',
    'overhand': f'{APOC}/HUD/SPR_HUD_Arrow_ExternalLink.png',
    'safeBlast': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Danger_01_Clean.png',
    'blastWave': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Star_04_Silver.png',
    'bandolier': f'{APOC}/Icons_Ammo/ICON_SM_Chr_Attach_ShellStrip_Rifle_01.png',
    'boulder': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Minerals_01_Clean.png',
    'demolition': f'{MH}/Icons_Resources/ICON_SM_Chr_Attach_Bomb_Dynamite_01_Military.png',
    'thrower': f'{APOC}/HUD/SPR_HUD_Reticle_Bracket_GrenadeLauncher.png',
    'smokeEyes': f'{MH}/Icons_Resources/ICON_SM_Chr_Attach_Glasses_01_Military.png',
    'doubleCharge': f'{MH}/Icons_Resources/ICON_SM_Chr_Attach_Bomb_Dynamite_02_Military.png',
    'crater': f'{MH}/Icons_Special/ICON_MilitaryCombat_Special_BombStrike_01_Clean.png',
    'fog': f'{MH}/HUD/SPR_HUD_MilitaryCombat_Icon_SeaLevel_01_Clean.png',
    'stoning': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Currency_Gem_01_Clean.png',
    'blaster': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Explosive_01_Clean.png',
    'shockwave': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Connection_01_Clean.png',
    'cartridge': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Grenade_01_Clean.png',
    'flares': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Ammo_Flare_01_Clean.png',
    'lure': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Sound_03_Clean.png',
    'ironArm': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Strength_02_Clean.png',
    'sling': f'{APOC}/Icons_Ammo/ICON_SM_Prop_Arrow_01.png',
    'bandage': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Plus_01_Clean.png',
    'calmVoice': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Chat_01_Clean.png',
    'revival': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Health_02_Clean.png',
    'bridge': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Comms_01_Clean.png',
    'bigKit': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Healing_02_Clean.png',
    'firstAid': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_DefenseUp_01_Clean.png',
    'lightHands': f'{APOC}/Icons_Map/ICON_Map_Biohazard.png',
    'suture': f'{MH}/Icons_Resources/ICON_SM_Prop_Needle_01_BattleRoyale.png',
    'bedside': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Player_01_Clean.png',
    'toughSkin': f'{APOC}/Icons_Resources/ICON_SM_Prop_Animal_Pelt_01.png',
    'restoration': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Health_01_Clean.png',
    'surgeon': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Healing_01_Clean.png',
    'lullaby': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Music_On_01_Clean.png',
    'spares': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Items_01_Clean.png',
    'triage': f'{APOC}/Icons_Status/ICON_Status_Health03.png',
    'rounds': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Time02_Clean.png',
    'fieldMedic': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Healing_01_Clean.png',
    'ironBlood': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Bleeding_01_Clean.png',
    'cheer': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Fist_01_Silver.png',
    'courage': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Medal_01_Clean.png',
    'kitBag': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Crate_01_Clean.png',
    'miracles': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Luck_01_Clean.png',
    'presence': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Crown_01_Clean.png',
    'vigor': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Leaf_01_Silver.png',
    'serenityMind': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Spirit_01_Clean.png',
    'warKit': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Crate_03_Clean.png',
    'fieldHospital': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Home_02_Clean.png',
    'goodHand': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Currency_Gem_05_Clean.png',
    'tinker': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Settings_05_Clean.png',
    'lookout': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Camera_02_Clean.png',
    'quiet': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Sound_01_Off_Clean.png',
    'patchUp': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Repair_01_Clean.png',
    'deft': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Key_01_Clean.png',
    'crossfire': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Rifles_01_Silver.png',
    'care': f'{APOC}/Icons_Resources/ICON_SM_Prop_SprayBottle_01.png',
    'sideBySide': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Stars_02_Silver.png',
    'parapet': f'{APOC}/Icons_Resources/ICON_SM_Prop_Fence_Wood_Straight_01.png',
    'picklock': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Lock_01_Clean.png',
    'morePatch': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Crafting_01_Clean.png',
    'binoculars': f'{MH}/Icons_Resources/ICON_SM_Item_Binoculars_01_Military.png',
    'coolHead': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Mind_01_Clean.png',
    'shoulder': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Flag_02_Silver.png',
    'pointOut': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Objective_01_Clean.png',
    'engineer': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Settings_04_Clean.png',
    'magnifier': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Settings_Picture_01_Clean.png',
    'weakSpot': f'{MH}/HUD/SPR_HUD_MilitaryCombat_Reticle_Sniper_Crosshair_01.png',
    'temperance': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Headphones_01_Clean.png',
    'lynx': f'{MH}/Icons_WeaponMods/ICON_SM_Wep_Mod_Scope_09_Military.png',
    'bodge': f'{MH}/Icons_Resources/ICON_SM_Item_Crowbar_01_Military.png',
    'mastermind': f'{APOC}/Apocalypse/SPR_Apocalypse_Greeble_Cog03.png',
    'inseparable': f'{MH}/Icons_Resources/ICON_SM_Item_WalkieTalkie_01_Military.png',
    'watchman': f'{MH}/Icons_Special/ICON_MilitaryCombat_Special_Drone_01_Clean.png',
    'anatomy': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Crossbones_01_Silver.png',
    'watchtower': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Helicopter_01_Clean.png',
    'shadow': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Settings_Picture_02_Clean.png',
    'ingenuity': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Dial_08_Silver.png',
    'stamina': f'{MH}/Icons_Inventory/ICON_MilitaryCombat_Inventory_Abilities_01_Clean.png',
    'crouch': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Defense_01_Clean.png',
    'quickReflex': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Lightning_02_Clean.png',
    'sprinter': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Speed_02_Clean.png',
    'lungs': f'{APOC}/Icons_Armor/ICON_SM_Chr_Attach_GasMask_01.png',
    'dodgeFire': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_LightningBolts_01_Silver.png',
    'runAndGun': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_FastForward_01_Clean.png',
    'sturdy': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Strength_01_Clean.png',
    'temper': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Medal_04_Clean.png',
    'fixedPost': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Pin_01_Clean.png',
    'shadowstep': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Unknown_01_Clean.png',
    'leathery': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Shark_01_Silver.png',
    'eel': f'{APOC}/Icons_Resources/ICON_SM_Item_Meat_Fish_05.png',
    'vest': f'{APOC}/Icons_Inventory/ICON_Inventory_Armor.png',
    'dugIn': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Save_02_Clean.png',
    'marathon': f'{APOC}/Icons_Resources/ICON_SM_Item_Bottle_01.png',
    'ironBody': f'{MH}/Icons_Map/ICON_MilitaryCombat_Map_Tank_01_Clean.png',
    'poise': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Notification_01_Off_Clean.png',
    'feline': f'{MH}/Icons_Resources/ICON_SM_Prop_Parachute_01_BattleRoyale.png',
    'ironWill': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Wreath_01_Silver.png',
    'agile': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Arrow_02_Clean.png',
    'titan': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Encumbered_01_Clean.png',
    'longRunner': f'{MH}/Icons_Stats/ICON_MilitaryCombat_Stat_Speed_01_Clean.png',
    'rock': f'{MH}/Badges/SPR_MilitaryCombat_Badge_Element_Anchor_01_Silver.png',
    'untouchable': f'{APOC}/Icons_Resources/ICON_SM_Item_Feather_01.png',
    'bulwark': f'{MH}/Icons_Status/ICON_MilitaryCombat_Status_Armour_01_Clean.png',
    'lastStand': f'{MM}/Icons_ModernMenus_Flat/ICON_ModernMenus_Flag_01_Clean.png',
    'tireless': f'{APOC}/Icons_Map/ICON_Map_Fire.png',
}

SIZE, COLS = 128, 13
GREY, DARK, ACCENT = (0x8E, 0x94, 0x8F), (0x0D, 0x0F, 0x10), (0xF2, 0xC2, 0x30)


def silhouette(path: str, rotate: float = 0) -> Image.Image:
    a = Image.open(path).convert('RGBA').getchannel('A')
    h = a.histogram()
    if sum(h[1:200]) > 0.3 * sum(h[200:]):  # a soft glow round a badge: keep the solid core
        a = a.point(lambda v: 0 if v < 190 else min(255, (v - 190) * 4))
    if rotate:
        a = a.rotate(rotate, Image.BICUBIC, expand=True)
    box = a.getbbox()
    return a.crop(box) if box else a


def glyph(path: str) -> Image.Image:
    a = silhouette(path)
    if a.width > 2.1 * a.height or a.height > 2.1 * a.width:
        a = silhouette(path, 40 if a.width > a.height else -40)
    a = a.copy()
    a.thumbnail((SIZE - 12, SIZE - 12), Image.LANCZOS)
    return a


def tinted(a: Image.Image, rgb: tuple) -> Image.Image:
    t = Image.new('RGBA', a.size, rgb + (0,))
    t.putalpha(a)
    return t


def main() -> None:
    ids = list(ICONS)
    rows = math.ceil(len(ids) / COLS)
    atlases = {k: Image.new('RGBA', (COLS * SIZE, rows * SIZE), (0, 0, 0, 0)) for k in ('grey', 'dark')}
    for i, cid in enumerate(ids):
        a = glyph(ICONS[cid])
        at = ((i % COLS) * SIZE + (SIZE - a.width) // 2, (i // COLS) * SIZE + (SIZE - a.height) // 2)
        atlases['grey'].alpha_composite(tinted(a, GREY), at)
        atlases['dark'].alpha_composite(tinted(a, DARK), at)
    os.makedirs(OUT, exist_ok=True)
    for k, im in atlases.items():
        im.save(os.path.join(OUT, f'{k}.webp'), 'WEBP', quality=92, method=6)
    a = glyph(ICONS['core'])
    core = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    core.alpha_composite(tinted(a, ACCENT), ((SIZE - a.width) // 2, (SIZE - a.height) // 2))
    core.save(os.path.join(OUT, 'core.webp'), 'WEBP', quality=92, method=6)
    body = ''.join(f'  {cid}: {i},\n' for i, cid in enumerate(ids))
    with open(TS, 'w') as f:
        f.write(
            "// Written by tools/ui/synty-board-icons.py: each board cell's place in the\n"
            f'// icon atlases (public/ui/synty/board/), {COLS} to a row, {SIZE} px each.\n'
            "import type { CellId } from '@bastion/engine';\n\n"
            f'export const ATLAS_COLS = {COLS};\n'
            f'export const ATLAS_ROWS = {rows};\n'
            f'export const ATLAS_CELL = {SIZE};\n\n'
            f'export const BOARD_ICON_INDEX: Record<CellId, number> = {{\n{body}}};\n'
        )
    print('board icons:', len(ids), [os.path.getsize(os.path.join(OUT, f'{k}.webp')) for k in ('grey', 'dark', 'core')])


if __name__ == '__main__':
    main()
