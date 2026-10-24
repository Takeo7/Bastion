using System.Collections.Generic;
using System.IO;
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

        static readonly (string Id, string File)[] Icons =
        {
            // The bar's keys (synty-icons.css data-ability), and Disparar's by its gun (data-weapon).
            ("shoot", Rifle), ("weapon:rifle", Rifle), ("weapon:pistol", Pistol), ("weapon:shotgun", Shotgun),
            ("weapon:sniper", Sniper), ("weapon:cannon", MachineGun), ("pistol", Pistol),
            ("slash", Blades), ("overwatchMelee", Blade), ("stone", Explosives), ("overwatch", Target),
            ("hunker", Helmet), ("reload", Reload), ("grenade", Explosives), ("missiles", Explosives),
            ("launch", Launcher), ("smoke", Smoke), ("flashbang", Flashbang), ("firstAid", Healing),
            ("evac", Vehicle), ("evacAccept", Vehicle), ("evacRefuse", Time), ("abandonRefuse", Time),
            ("abandon", Flag), ("abandonAccept", Flag), ("hack", Settings), ("search", Backpack), ("searchRoom", Backpack),
            ("runAndGun", FastForward), ("rapidFire", Ammo), ("aimedShot", Sniper), ("suppression", MachineGun),
            ("chainShot", Bullets), ("rupture", ShieldBroken), ("elbowRoom", Knife), ("patchUp", Repair),
            ("lightningHands", SpeedUp), ("faceoff", Revolver), ("killZone", Targeted), ("revival", Health),
            ("restoration", Heart), ("claw", Skull), ("mindspin", Critical), ("reanimate", Dead), ("burst", Poisoned),
            ("stunLance", Shocked),
            // The soldier's figures (UnitPanel: StatView.Kind).
            ("stat:hp", Heart), ("stat:ap", Speed), ("stat:ammo", Bullets), ("stat:sidearm", Bullets),
            ("stat:aim", Accuracy), ("stat:melee", Blades), ("stat:grenade", Explosives), ("stat:armor", Armor),
            ("stat:worn", Armor), ("stat:weapon", Rifle),
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
        };

        public static void Make()
        {
            var files = new Dictionary<string, Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite ICON_", new[] { "Assets/Synty" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (files.ContainsKey(name)) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) files[name] = sprite;
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
