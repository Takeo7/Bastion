using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.UI.Hud;
using UnityEditor;
using UnityEngine;

namespace Bastion.UI.EditorTools
{
    /// <summary>
    /// The HUD's icon table (Resources/HudIcons.asset), its sprites found by file name in the INTERFACE packs Arte imports
    /// (Apocalypse HUD, Military Combat HUD, Modern Menus: docs/UNITY-ARTE.md §1). As the 0.10.1 had them
    /// (synty-icons.css, synty-sprites.py); its renders of items (Icons pack, after the first milestone) stand in by the
    /// nearest of these, and the doors (our own SVGs then) have none yet. Run again after an import.
    ///
    ///   unity/tools/unity.sh -executeMethod Bastion.UI.EditorTools.HudIcons.Make
    /// </summary>
    public static class HudIcons
    {
        const string TablePath = "Assets/Bastion/UI/Resources/HudIcons.asset";

        // The packs' file names in Unity (without .png) by the HUD's ids. Apocalypse HUD's are named apart from its
        // source files (ICON_Apocalpyse_…_01, Synty's own spelling), the others as in the source.
        const string Rifle = "ICON_SM_Wep_AssaultRifle_01_Clean";
        const string Pistol = "ICON_SM_Wep_Pistol_01_Clean";
        const string Sniper = "ICON_SM_Wep_SniperRifle_01_Clean";
        const string Shotgun = "ICON_SM_Wep_Shotgun_01_Military_Clean";
        const string MachineGun = "ICON_SM_Wep_MachineGun_Bandit_01_Military_Clean";
        const string Knife = "ICON_SM_Wep_Knife_01_Military_Clean";
        // The weapons found in the field (Ana's notes, Arturo 29/10): the frying pan, the bat, the crowbar, the chainsaw. The
        // bat's and the crowbar's flat sprites lie across and read as a line on a key: ours (Textures/Icons) are the packs'
        // slanted pictures (ICON_SM_Wep_Bat_Wood_01, ICON_SM_Item_Crowbar_01_Military) made white, cut to their edges.
        const string Pan = "ICON_SM_Wep_Pan_01_BattleRoyale_Clean";
        const string Bat = "w-bate";
        const string Crowbar = "w-palanca";
        const string Chainsaw = "ICON_SM_Wep_ChainSaw_01_Clean";
        const string Revolver = "ICON_SM_Wep_Pistol_Revolver_01_BattleRoyale_Clean";
        const string Launcher = "ICON_SM_Wep_Grenade_Launcher_01_Clean";
        const string Target = "ICON_Apocalpyse_Map_Target_01";
        const string Ammo = "ICON_Apocalpyse_Map_Ammo_01";
        const string Skull = "ICON_Apocalpyse_Map_Skull_01";
        const string Armor = "ICON_Apocalpyse_Status_Armor_01";
        const string Health = "ICON_Apocalpyse_Status_Health_01";
        const string Backpack = "ICON_Apocalpyse_Inventory_Backpack_01";
        const string Explosives = "ICON_Apocalpyse_Inventory_Explosives_01";
        const string Healing = "ICON_Apocalpyse_Inventory_Healing_01";
        const string Helmet = "ICON_Apocalpyse_Inventory_Helmets_01";
        // Esconderse (Ana's #1 of 1/11): a hood, Dark Fantasy HUD's «stealthy»; its dotted figure («invisible») is thin on a
        // key and says unseen, not seen from half as far.
        const string Hood = "ICON_DarkFantasy_Status_Stealthy_01_Clean";
        // Golpe silencioso (Gameplay, 16/11): Dark Fantasy HUD's dagger, a stab, apart from the Tajo's blades and Codazo's knife.
        const string Dagger = "ICON_DarkFantasy_Inventory_Daggers_01_Clean";
        const string Reload = "ICON_ModernMenus_Reload_01_Clean";
        const string FastForward = "ICON_ModernMenus_FastForward_01_Clean";
        const string Settings = "ICON_ModernMenus_Settings_01_Clean";
        const string Confirm = "ICON_ModernMenus_Confirm_01_Clean";
        // A blade with some body for the bar (the machete's sprite read as a thin line, Gameplay 23/10).
        const string Blades = "ICON_ModernMenus_Combat_01_Clean";
        const string Blade = "ICON_ModernMenus_Combat_02_Clean";
        const string Smoke = "ICON_MilitaryCombat_Inventory_GrenadeSmoke_01_Clean";
        const string Flashbang = "ICON_MilitaryCombat_Inventory_GrenadeFlashbang_01_Clean";
        const string Bullets = "ICON_MilitaryCombat_Inventory_Ammo_Bullets_02_Clean";
        const string Repair = "ICON_MilitaryCombat_Inventory_Repair_01_Clean";
        const string Heart = "ICON_MilitaryCombat_Status_Health_01_Clean";
        const string ShieldBroken = "ICON_MilitaryCombat_Status_DefenseBroken_01_Clean";
        const string Shocked = "ICON_MilitaryCombat_Status_Shocked_01_Clean";
        const string SpeedUp = "ICON_MilitaryCombat_Status_SpeedUp_01_Clean";
        const string Targeted = "ICON_MilitaryCombat_Status_Targeted_01_Clean";
        const string Critical = "ICON_MilitaryCombat_Status_Critical_02_Clean";
        const string Dead = "ICON_MilitaryCombat_Status_Dead_01_Clean";
        const string Poisoned = "ICON_MilitaryCombat_Status_Poisoned_01_Clean";
        const string Time = "ICON_MilitaryCombat_Status_Time_01_Clean";
        const string Flag = "ICON_MilitaryCombat_Map_Flag_01_Clean";
        const string Vehicle = "ICON_MilitaryCombat_Map_Vehicle_01_Clean";
        const string Accuracy = "ICON_MilitaryCombat_Stat_Accuracy_01_Clean";
        const string Speed = "ICON_MilitaryCombat_Stat_Speed_02_Clean";
        // Our own (Textures/Icons): the 0.10.1's stone, a Synty rock's silhouette (synty-sprites.py r-roca), for Lanzar and
        // Piedra; it had Explosives, the grenade's, and Lanzar read as a grenade (Ana, 28/10).
        const string Rock = "r-roca";
        // El herido (U26, after the R10): a crutch, ours, white and solid as the packs' (none of theirs reads «can't walk»:
        // their wounds are skulls, a claw's slashes or drops of blood). Drawn by u26/muleta.swift.
        const string Crutch = "n-muleta";
        const string OwnIcons = "Assets/Bastion/UI/Textures/Icons";

        static readonly (string Id, string File)[] Icons =
        {
            // The bar's keys (synty-icons.css data-ability), and Disparar's by its gun (data-weapon).
            ("shoot", Rifle), ("weapon:rifle", Rifle), ("weapon:pistol", Pistol), ("weapon:shotgun", Shotgun),
            ("weapon:sniper", Sniper), ("weapon:cannon", MachineGun), ("pistol", Pistol),
            // The bolt-action rifle's own (it fires as the assault rifle: Arturo, 18/11 T14), Apocalypse HUD's.
            ("weapon:huntingRifle", "ICON_SM_Wep_HuntingRifle_Clean_01_Clean"),
            // Arco y ballesta (U29; Arturo, cuadro 108; Producto, 21/11): Apocalypse's crossbow drawn thicker (u29/lab/iconos.swift
            // gruesa … 8: its line was gone at the bar's 28 px), Dark Fantasy's bow and its one arrow; on the soldier's panel
            // (14 px) a bold arrow of ours (… flecha … 30), where Dark Fantasy's was a «/» by the separators.
            ("weapon:crossbow", "w-ballesta"), ("weapon:bow", "ICON_DarkFantasy_Inventory_Bows_01_Clean"),
            ("arrows", "ICON_DarkFantasy_Inventory_Arrows_02_Clean"), ("stat:arrows", "i-flecha-ficha"),
            ("weapon:pan", Pan), ("weapon:bat", Bat), ("weapon:crowbar", Crowbar), ("weapon:chainsaw", Chainsaw),
            ("slash", Blades), ("takedown", Dagger), ("overwatchMelee", Blade), ("stone", Rock), ("overwatch", Target),
            ("hunker", Helmet), ("hide", Hood), ("pry", Crowbar), ("reload", Reload), ("grenade", Explosives), ("missiles", Explosives),
            ("launch", Launcher), ("smoke", Smoke), ("flashbang", Flashbang), ("firstAid", Healing),
            ("evac", Vehicle), ("evacAccept", Vehicle), ("evacRefuse", Time), ("abandonRefuse", Time),
            // Bajar de La Lata (T24): Dark Fantasy HUD's arrow down, apart from Evacuar's vehicle.
            ("alight", "ICON_DarkFantasy_Status_Down_01_Clean"),
            // «Ceder el mando» on the bar, with only soldiers inside La Lata left (T24, two players): the wait's clock.
            ("passCommand", Time),
            // The states as icons on narrow busts (Arturo, in the cuadro, 18/11: SquadList.StateIcons), one shape each that
            // tells apart small: the bar's where it has one (a ring, a dagger, a helmet, a hood, the vehicle), else the HUD
            // packs' status icons (four arrows, a bolt, an eye struck out, chevrons down, a spiral, a burst, a tangle).
            ("state:overwatch", Target), ("state:killZone", "ICON_DarkFantasy_Status_Targeted_02_Clean"), ("state:watchMelee", Blade),
            ("state:hunker", Helmet), ("state:hide", Hood), ("state:stun", Shocked), ("state:disoriented", "ICON_DarkFantasy_Status_Blinded_01_Clean"),
            ("state:down", "ICON_MilitaryCombat_Status_Down_01_Clean"), ("state:controlled", "ICON_DarkFantasy_Status_Cursed_01_Clean"),
            ("state:panic", "ICON_DarkFantasy_Status_Critical_01_Clean"), ("state:captive", "ICON_DarkFantasy_Status_Entangled_01_Clean"),
            ("state:aboard", Vehicle), ("state:carry", Backpack),
            ("abandon", Flag), ("abandonAccept", Flag), ("hack", Settings), ("search", Backpack), ("searchRoom", Backpack),
            // Registrar by what is searched (Arturo, 18/11 T12): a car's boot (Apocalypse's car, not La Lata's), a body, a box.
            ("search:car", "ICON_Apocalpyse_Map_Vehicle_01"), ("search:body", "ICON_DarkFantasy_Status_Dead_01_Clean"), ("search:box", "ICON_ModernMenus_Crate_01_Clean"),
            ("runAndGun", FastForward), ("rapidFire", Ammo), ("aimedShot", Sniper), ("suppression", MachineGun),
            ("chainShot", Bullets), ("rupture", ShieldBroken), ("elbowRoom", Knife), ("patchUp", Repair),
            ("lightningHands", SpeedUp), ("faceoff", Revolver), ("killZone", Targeted), ("revival", Health),
            ("restoration", Heart), ("claw", Skull), ("mindspin", Critical), ("reanimate", Dead), ("burst", Poisoned),
            ("stunLance", Shocked),
            // Hablar, to a run's survivor (N2, 17/11): a speech bubble, Modern Menus' as the menu's.
            ("talk", "ICON_ModernMenus_Chat_01_Clean"),
            // The soldier's figures (UnitPanel: StatView.Kind).
            ("stat:hp", Heart), ("stat:ap", Speed), ("stat:ammo", Bullets), ("stat:sidearm", Bullets),
            ("stat:aim", Accuracy), ("stat:melee", Blades), ("stat:grenade", Explosives), ("stat:armor", Armor),
            ("stat:worn", Armor), ("stat:weapon", Rifle), ("stat:dodge", SpeedUp),
            // ✓, which Oswald lacks (the arrows are drawn: Triangle).
            ("ui:check", Confirm),
            // The base's top strip (mixta.css --mi-*: synty-sprites.py's mh-crate, mh-medkit, mh-repair, mh-hourglass,
            // mh-up, biohazard, mainmenu; the rations by Apocalypse's hunger).
            ("store:credits", "ICON_MilitaryCombat_Map_Treasure_01_Clean"), ("store:medicines", "ICON_MilitaryCombat_Inventory_Healing_02_Clean"),
            ("store:food", "ICON_Apocalpyse_Status_Hunger_01"), ("store:parts", Repair), ("base:day", Time),
            ("base:level", "ICON_MilitaryCombat_Status_Up_01_Clean"), ("base:doom", "ICON_Apocalpyse_Map_Biohazard_01"),
            ("base:menu", "ICON_ModernMenus_MainMenu_01_Clean"),
            // The Bastion's figures in the Asentamiento (strategy.css .bstat: mh-tent, mh-plants, mh-mind; the rest are
            // the strip's, the tabs' and the markers' own).
            ("base:beds", "ICON_MilitaryCombat_Map_Home_02_Clean"), ("base:garden", "ICON_MilitaryCombat_Map_Plants_01_Clean"),
            ("base:will", "ICON_MilitaryCombat_Stat_Mind_01_Clean"),
            // A soldier's rank (mixta.css .rank: synty-sprites.py's mh/rank-1 to 7, the Military Combat HUD's chevrons).
            ("rank:1", "SPR_MilitaryCombat_Badge_Chevron_05_Bronze"), ("rank:2", "SPR_MilitaryCombat_Badge_Chevron_06_Bronze"),
            ("rank:3", "SPR_MilitaryCombat_Badge_Chevron_07_Bronze"), ("rank:4", "SPR_MilitaryCombat_Badge_Chevron_01_Silver"),
            ("rank:5", "SPR_MilitaryCombat_Badge_Chevron_02_Silver"), ("rank:6", "SPR_MilitaryCombat_Badge_Chevron_03_Gold"),
            ("rank:7", "SPR_MilitaryCombat_Badge_Chevron_04_Gold"),
            // The base's tabs (mixta-base.css --ni: mh-pin, mh-shop, helmet, crafting; the squad's La Lata by a vehicle).
            ("tab:geoscape", "ICON_MilitaryCombat_Map_Pin_01_Clean"), ("tab:engineering", "ICON_MilitaryCombat_Map_ShopWeapons_01_Clean"),
            ("tab:barracks", Helmet), ("tab:facilities", "ICON_Apocalpyse_Inventory_Crafting_01"), ("tab:squad", Vehicle),
            // The valley's markers (valley.css: map-objective, map-skull, map-unknown, map-home, map-vehicle) and the
            // operations' pins with their kind inside (mixta-base.css .mission-icon: map-pin; skull, mh-crate, fire,
            // mh-comms, heal, backpack).
            ("map:zone", "ICON_MilitaryCombat_Map_Objective_01_Clean"), ("map:nest", "ICON_MilitaryCombat_Map_Skull_01_Clean"),
            ("map:rumor", "ICON_MilitaryCombat_Map_Unknown_01_Clean"), ("map:home", "ICON_MilitaryCombat_Map_Home_01_Clean"),
            ("map:lata", Vehicle), ("map:pin", "ICON_MilitaryCombat_Map_Pin_01_Clean"),
            ("kind:elimination", Skull), ("kind:recovery", "ICON_MilitaryCombat_Map_Treasure_01_Clean"), ("kind:sabotage", "ICON_Apocalpyse_Map_Fire_01"),
            ("kind:hack", "ICON_MilitaryCombat_Inventory_Comms_01_Clean"), ("kind:rescue", "ICON_Apocalpyse_Map_Healing_01"), ("kind:scavenge", Backpack),
            // The runs' stages on the map (16/11; today's objectives keep their kind:): Asedio a shield, Travesía boots, the
            // Guarida's best box a chest, the boss a monster; the stops, a shelter, a merchant, a quest's mark (the Superviviente's
            // went with it, N3); and the
            // horde awake, an exclamation mark (night and the weather are our line drawings).
            ("node:hold", "ICON_DarkFantasy_Inventory_Shields_01_Clean"), ("node:crossing", "ICON_DarkFantasy_Inventory_Boots_01_Clean"),
            ("node:elite", "ICON_ModernMenus_Chest_01_Clean"), ("node:boss", "ICON_DarkFantasy_Map_Monster_01_Clean"),
            ("node:refuge", "ICON_DarkFantasy_Map_Home_01_Clean"), ("node:trade", "ICON_DarkFantasy_Map_Merchant_01_Clean"),
            ("node:event", "ICON_Apocalpyse_Map_Quest_01"),
            ("node:awake", "ICON_ModernMenus_ExclamationMark_01_Clean"),
            // La llave (U22): the same family's key, its stage on the map, and the key itself in a pack, on the floor (its
            // mark) and in the log's float.
            ("node:key", "ICON_DarkFantasy_Map_Key_01_Clean"), ("key", "ICON_DarkFantasy_Map_Key_01_Clean"),
            // Mordido (U23): Apocalypse's biohazard, its stage on the map and the count over the bitten (no other icon of the
            // game is it: the 0.10.1's doom went with its campaign); the antiviral, Dark Fantasy's flask, apart from the
            // medicines' cross, till Arte's picture.
            ("node:bitten", "ICON_Apocalpyse_Map_Biohazard_01"), ("bite", "ICON_Apocalpyse_Map_Biohazard_01"),
            ("antiviral", "ICON_DarkFantasy_Map_Potions_01_Clean"),
            // El herido (U26): the crutch, its stage on the map and the arrow at the screen's edge to them on the floor (not
            // Rescate's cross nor Mordido's biohazard). Every stage needs its icon: NodeIconsCheck says which lack one.
            ("node:wounded", Crutch),
            // Separados (U27): two of Military Combat's busts apart (u27/separados.swift; Arturo, cuadro 96, over Dark
            // Fantasy's flag and two arrows meeting). Also the other group's arrow at the screen's edge.
            ("node:split", "n-separados-dos"),
            // Saqueadores (U28): Modern Menus' skull and crossbones (Arturo, cuadro 104, over Military Combat's rifle twice
            // crossed and a tent of ours, which the Camping's site badge already is). Also over the stash's crates still shut.
            ("node:raiders", "ICON_ModernMenus_Death_05_Clean"),
            // Evacuación (G23.6): PROVISIONAL, Military Combat's helicopter, only so that NodeIconsCheck is quiet while its
            // engine goes in, off the maps (Producto, 21/11); its own goes to Arturo's cuadro with the stage's UI.
            ("node:evacuation", "ICON_MilitaryCombat_Map_Helicopter_01_Clean"),
            // Las cargas: PROVISIONAL, Military Combat's explosive (Producto, 21/11), as Evacuación's; its own to the cuadro.
            ("node:charges", "ICON_MilitaryCombat_Inventory_Explosive_01_Clean"),
            // The runs' base tabs: Almacén a backpack, Mejoras the workshop's tools (Salir is the Escuadra's La Lata).
            ("tab:stash", Backpack), ("tab:upgrades", "ICON_Apocalpyse_Inventory_Crafting_01"),
        };

        public static void Make()
        {
            var files = new Dictionary<string, Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite ICON_", new[] { "Assets/Synty" }).Concat(AssetDatabase.FindAssets("t:Sprite SPR_MilitaryCombat_Badge", new[] { "Assets/Synty" })))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (files.ContainsKey(name)) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) files[name] = sprite;
            }
            // Ours, as sprites.
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { OwnIcons }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) files[Path.GetFileNameWithoutExtension(path)] = sprite;
            }
            var table = AssetDatabase.LoadAssetAtPath<HudIconTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<HudIconTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }
            table.Entries.Clear();
            var missing = new List<string>();
            foreach (var (id, file) in Icons)
            {
                files.TryGetValue(file, out var sprite);
                if (sprite == null) missing.Add($"{id} ({file})");
                table.Entries.Add(new HudIconTable.Entry { Id = id, Sprite = sprite });
            }
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"HUD icons: {Icons.Length - missing.Count} of {Icons.Length}" + (missing.Count > 0 ? "; missing: " + string.Join(", ", missing) : ""));
        }
    }
}
