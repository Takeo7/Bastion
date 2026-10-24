using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// MapArt's models that are only in their pack's source files (assets-src), not in its Unity package: each copied into
    /// Assets/Synty/Extra (off git, as the rest of Synty) and imported with its pack's own material, so MapLab.Prefabs and
    /// the map's catalog find it by its name; a pack with no Unity package in the project has its material made there, as
    /// one of Apocalypse's on the pack's atlas. Then MapCatalogMake.Run.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.SourceProps.Run -quit
    /// </summary>
    public static class SourceProps
    {
        const string Folder = "Assets/Synty/Extra";

        /// <summary>
        /// The model, its source FBX (from the repository's root), its pack's material and, if that pack's Unity package
        /// isn't in the project, its atlas (from the root).
        /// </summary>
        static readonly (string model, string source, string material, string atlas)[] Models = new[]
        {
            // The loose rubbish of a town (ground.ts scatterCover's `trash`, UI's GroundCover).
            ("SM_Prop_Trash_02", $"{Apocalypse}/SM_Prop_Trash_02.fbx", "PolygonApocalypse_01_A", (string)null),
            // The base's bench and wood pile (basePieces.ts), from Alpine Mountain.
            ("SM_Prop_Bench_01", $"{Alpine}/FBX/Props/SM_Prop_Bench_01.fbx", "PolygonNatureBiomes_Alpine_Mat_01", AlpineAtlas),
            ("SM_Prop_Wood_Pile_01", $"{Alpine}/FBX/Props/SM_Prop_Wood_Pile_01.fbx", "PolygonNatureBiomes_Alpine_Mat_01", AlpineAtlas),
        }
        // What Unity's Apocalypse has inside another model (the house's glass, the trap's jaws, the gun's parts on its tripod,
        // the mast's top, a gate's leaf, a mobile home's glass and skirt), and the models it is inside, each alone as in the
        // source: 0.10.1 draws them apart (basePieces.ts: the jaws open or shut, the gun turned on its swivel). In Extra
        // they come before the package's of the same name (MapLab.Prefabs).
        .Concat(FromApocalypse("SM_Bld_House_03", "SM_Bld_House_03_Glass_01:glass", "SM_Bld_Trailer_01", "SM_Bld_Trailer_Glass_01:glass",
            "SM_Bld_Trailer_Skirt_01", "SM_Bld_Trailer_Damaged_01", "SM_Bld_Trailer_Damaged_Glass_01", "SM_Wep_AAGun_01", "SM_Wep_AAGun_Body_01",
            "SM_Wep_AAGun_Mag_01", "SM_Wep_AAGun_Swivel_01", "SM_Prop_Bear_Trap_01", "SM_Prop_Bear_Trap_Jaw_01", "SM_Prop_Bear_Trap_Jaw_02",
            "SM_Bld_RadioTower_01", "SM_Bld_RadioTower_Top_01", "SM_Prop_Wall_Quarantine_GateDoor_01", "SM_Prop_CarBattery_Table_01"))
        .ToArray();
        const string Apocalypse = "assets-src/synty/POLYGON_Apocalypse_SourceFiles_v4/FBX";

        /// <summary>Apocalypse's source models with the pack's own material, or its glass (`name:glass`; MaterialList_PolygonApocalypse).</summary>
        static IEnumerable<(string model, string source, string material, string atlas)> FromApocalypse(params string[] models) =>
            models.Select(m => m.Split(':')).Select(m => (m[0], $"{Apocalypse}/{m[0]}.fbx", m.Length > 1 ? "PolygonApocalypse_01_Glass" : "PolygonApocalypse_01_A", (string)null));
        const string Alpine = "assets-src/synty/POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3";
        const string AlpineAtlas = Alpine + "/Textures/PolygonNatureBiomesS2_Alpine_Texture_01.png";

        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Synty", "Extra");
            var materialOf = MapLab.Materials();
            foreach (var (model, source, material, atlas) in Models)
            {
                string from = Path.Combine(root, source), to = $"{Folder}/{model}.fbx";
                if (!File.Exists(from))
                {
                    Debug.LogWarning($"[source props] {source}: not there");
                    continue;
                }
                File.Copy(from, to, true);
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
                var importer = (ModelImporter)AssetImporter.GetAtPath(to);
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                var own = atlas != null ? Made(material, Path.Combine(root, atlas), materialOf) : materialOf(material);
                // Every material the FBX names, the pack's one.
                foreach (var m in AssetDatabase.LoadAllAssetsAtPath(to).OfType<Material>())
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), own);
                importer.SaveAndReimport();
                Debug.Log($"[source props] {to} with {(own != null ? own.name : "no material")}");
            }
        }

        /// <summary>A pack's material made in Extra (once): Apocalypse's, on the pack's atlas, with no normals and no glow of its.</summary>
        static Material Made(string name, string atlas, System.Func<string, Material> materialOf)
        {
            string path = $"{Folder}/{name}.mat";
            var made = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (made != null) return made;
            string texture = $"{Folder}/{Path.GetFileName(atlas)}";
            if (!File.Exists(atlas))
            {
                Debug.LogWarning($"[source props] {atlas}: not there");
                return null;
            }
            File.Copy(atlas, texture, true);
            AssetDatabase.ImportAsset(texture, ImportAssetOptions.ForceUpdate);
            var like = materialOf("PolygonApocalypse_01_A");
            if (like == null) return null;
            made = new Material(like) { name = name };
            made.SetTexture("_Albedo_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            made.SetTexture("_Emission_Map", null);
            made.SetTexture("_Normal_Map", null);
            made.SetFloat("_Normal_Amount", 0);
            AssetDatabase.CreateAsset(made, path);
            return made;
        }
    }
}
