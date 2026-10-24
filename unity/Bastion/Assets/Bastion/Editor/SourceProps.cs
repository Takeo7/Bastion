using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// MapArt's models that are only in their pack's source files (assets-src), not in its Unity package: each copied into
    /// Assets/Synty/Extra (off git, as the rest of Synty) and imported with its pack's own material, so MapLab.Prefabs and
    /// the map's catalog find it by its name. Then MapCatalogMake.Run.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.SourceProps.Run -quit
    /// </summary>
    public static class SourceProps
    {
        const string Folder = "Assets/Synty/Extra";

        /// <summary>The model, its source FBX (from the repository's root) and its pack's material.</summary>
        static readonly (string model, string source, string material)[] Models =
        {
            // The loose rubbish of a town (ground.ts scatterCover's `trash`, UI's GroundCover).
            ("SM_Prop_Trash_02", "assets-src/synty/POLYGON_Apocalypse_SourceFiles_v4/FBX/SM_Prop_Trash_02.fbx", "PolygonApocalypse_01_A"),
        };

        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Synty", "Extra");
            var materialOf = MapLab.Materials();
            foreach (var (model, source, material) in Models)
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
                var own = materialOf(material);
                // Every material the FBX names, the pack's one.
                foreach (var m in AssetDatabase.LoadAllAssetsAtPath(to).OfType<Material>())
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), own);
                importer.SaveAndReimport();
                Debug.Log($"[source props] {to} with {(own != null ? own.name : "no material")}");
            }
        }
    }
}
