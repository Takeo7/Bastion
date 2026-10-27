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
        /// The model, its source FBX (from the repository's root), its pack's material, if that pack's Unity package isn't in
        /// the project its atlas (from the root), and how 0.10.1's converter cut it, if it did (syntyManifest.ts `keep`,
        /// `hole`; unity/tools/props-cut.py, with Blender).
        /// </summary>
        static readonly (string model, string source, string material, string atlas, string cut)[] Models = new[]
        {
            // The loose rubbish of a town (ground.ts scatterCover's `trash`, UI's GroundCover).
            ("SM_Prop_Trash_02", $"{Apocalypse}/SM_Prop_Trash_02.fbx", "PolygonApocalypse_01_A", (string)null, (string)null),
            // The base's bench and wood pile (basePieces.ts), from Alpine Mountain.
            ("SM_Prop_Bench_01", $"{Alpine}/FBX/Props/SM_Prop_Bench_01.fbx", "PolygonNatureBiomes_Alpine_Mat_01", AlpineAtlas, null),
            ("SM_Prop_Wood_Pile_01", $"{Alpine}/FBX/Props/SM_Prop_Wood_Pile_01.fbx", "PolygonNatureBiomes_Alpine_Mat_01", AlpineAtlas, null),
            // The snowbanks of a winter's map (snow.ts drifts; their material is the snow's: SnowFeatureMake).
            ("SM_Env_Snow_Mound_01", $"{Alpine}/FBX/Environment/SM_Env_Snow_Mound_01.fbx", "PolygonNatureBiomes_Alpine_Mat_01", AlpineAtlas, null),
            // The watchtower of the siege and the base (basePieces.ts tower): a deck on legs with its rail, its hut and its
            // stairs taken off (the map's ladder climbs to it), as the manifest's guardTower.
            ("SM_Bld_GuardTower_02", "assets-src/synty/POLYGON_Military_SourceFiles/Fbx/SM_Bld_GuardTower_02.fbx", "PolygonMilitary_01_A", null,
                "{\"keep\": [[-2.2, -2.2, -0.1], [2.2, 2.06, 4.6]], \"hole\": [[[-1.07, -1.07, 3.62], [1.07, 1.07, 6]]]}"),
        }
        // What Unity's Apocalypse has inside another model (the house's glass, the trap's jaws, the gun's parts on its tripod,
        // the mast's top, a gate's leaf, a mobile home's glass and skirt), and the models it is inside, each alone as in the
        // source: 0.10.1 draws them apart (basePieces.ts: the jaws open or shut, the gun turned on its swivel). In Extra
        // they come before the package's of the same name (MapLab.Prefabs).
        .Concat(FromApocalypse("SM_Bld_House_03", "SM_Bld_House_03_Glass_01:glass", "SM_Bld_Trailer_01", "SM_Bld_Trailer_Glass_01:glass",
            "SM_Bld_Trailer_Skirt_01", "SM_Bld_Trailer_Damaged_01", "SM_Bld_Trailer_Damaged_Glass_01", "SM_Wep_AAGun_01", "SM_Wep_AAGun_Body_01",
            "SM_Wep_AAGun_Mag_01", "SM_Wep_AAGun_Swivel_01", "SM_Prop_Bear_Trap_01", "SM_Prop_Bear_Trap_Jaw_01", "SM_Prop_Bear_Trap_Jaw_02",
            "SM_Bld_RadioTower_01", "SM_Bld_RadioTower_Top_01", "SM_Prop_Wall_Quarantine_GateDoor_01", "SM_Prop_CarBattery_Table_01"))
        // And what Unity's Apocalypse makes otherwise, or hasn't: the grave stick comes on a mound of earth there (the
        // memorial's crosses on graves), the toolbox is another one, and the planked gate of the palisade is only Farm's
        // lattice gate, of the same name.
        .Concat(FromApocalypse("SM_Prop_GraveStick_01", "SM_Prop_ToolBox_01", "SM_Prop_Fence_Wood_Gate_01"))
        // The Armería's crates open, their lids beside them (clearingArt.ts fillArmory): Unity's prefabs have them shut,
        // and no lid of their own.
        .Concat(FromApocalypse("SM_Item_Ammo_Crate_01", "SM_Item_Ammo_Crate_Lid_01"))
        // Its green first-aid kit, as 0.10.1 drew it from the source (Unity's is grey); and its bench, its pegboard solid
        // as there (Plain).
        .Concat(FromApocalypse("SM_Prop_MedicalBox_02", "SM_Prop_Workbench_01"))
        // (And the camp's metal cup, a canteen's, which Unity's Military has only on the canteen: clearingLife.ts CAMP_ITEMS.)
        // What a search leaves (mapView.ts ransack, lootBox): a dresser's drawer pulled out and dropped, a crate's lid, the ammunition
        // crate's, inside their models in Unity's packages.
        .Concat(FromApocalypse("SM_Prop_Dresser_03_Drawer_01", "SM_Prop_Crate_Open_Lid_01"))
        .Concat(new[] { "SM_Prop_Crate_Wood_01", "SM_Prop_Crate_Wood_Lid_01", "SM_Item_Canteen_Cup_01", "SM_Prop_Crate_Ammo_Lid_01" }.Select(m => (m, $"assets-src/synty/POLYGON_Military_SourceFiles/Fbx/{m}.fbx", "PolygonMilitary_01_A", (string)null, (string)null)))
        // The supermarket's shelves as stocked as they are (syntyManifest.ts gondolaGaps…wallShelfEmpty: the goods taken out
        // in sections, `hole`), each a model of its own under the name MapArt.json gives it (its `cut`).
        .Concat(Cuts())
        .ToArray();

        /// <summary>A pack whose models MapArt.json cuts: its source FBX folder (from the root) and its material; null, none.</summary>
        static (string folder, string material)? CutPack(string pack) => pack == "town" ? ("assets-src/synty/POLYGON_Town_SourceFiles_v7/FBX", "PolygonTown_01_A") : null;

        /// <summary>MapArt.json's props with a `cut` (map-art-json.mts: `hole` alone), as models to make.</summary>
        static IEnumerable<(string model, string source, string material, string atlas, string cut)> Cuts()
        {
            var art = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/MapArt.json"));
            foreach (var p in ((Newtonsoft.Json.Linq.JObject)art["props"]).Properties())
            {
                if (!(p.Value["cut"] is Newtonsoft.Json.Linq.JObject cut)) continue;
                string pack = (string)p.Value["pack"];
                // (Called as Models is made: nothing static after it is there yet.)
                var from = CutPack(pack);
                if (from == null)
                {
                    Debug.LogWarning($"[source props] {p.Name}: no source folder for {pack}");
                    continue;
                }
                yield return ((string)p.Value["fbx"], $"{from.Value.folder}/{(string)cut["source"]}.fbx", from.Value.material, null,
                    new Newtonsoft.Json.Linq.JObject { ["hole"] = cut["hole"] }.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
        /// <summary>
        /// The slots painted from a texture of their own (a wire's alpha) that 0.10.1 drew in one plain colour, the atlas's
        /// at a spot of it (syntyManifest.ts `plain`, Blender's UV; convert.py plain_slots): the workbench's pegboard.
        /// </summary>
        static readonly Dictionary<string, (string slot, float u, float v)> Plain = new Dictionary<string, (string, float, float)>
        {
            ["SM_Prop_Workbench_01"] = ("Polygon_Apocalypse_Wire_01", 0.93f, 0.06f),
        };

        const string Apocalypse = "assets-src/synty/POLYGON_Apocalypse_SourceFiles_v4/FBX";

        /// <summary>Apocalypse's source models with the pack's own material, or its glass (`name:glass`; MaterialList_PolygonApocalypse).</summary>
        static IEnumerable<(string model, string source, string material, string atlas, string cut)> FromApocalypse(params string[] models) =>
            models.Select(m => m.Split(':')).Select(m => (m[0], $"{Apocalypse}/{m[0]}.fbx", m.Length > 1 ? "PolygonApocalypse_01_Glass" : "PolygonApocalypse_01_A", (string)null, (string)null));
        const string Alpine = "assets-src/synty/POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3";
        const string AlpineAtlas = Alpine + "/Textures/PolygonNatureBiomesS2_Alpine_Texture_01.png";

        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Synty", "Extra");
            var materialOf = MapLab.Materials();
            // BASTION_SOLO=prefix: only the models whose names start so (the others as they are).
            string only = System.Environment.GetEnvironmentVariable("BASTION_SOLO");
            foreach (var (model, source, material, atlas, cut) in Models)
            {
                if (only != null && !model.StartsWith(only)) continue;
                string from = Path.Combine(root, source), to = $"{Folder}/{model}.fbx";
                if (!File.Exists(from))
                {
                    Debug.LogWarning($"[source props] {source}: not there");
                    continue;
                }
                if (cut != null)
                {
                    if (!Cut(root, from, to, cut)) continue;
                }
                else File.Copy(from, to, true);
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
                var importer = (ModelImporter)AssetImporter.GetAtPath(to);
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                var own = atlas != null ? Made(material, Path.Combine(root, atlas), materialOf) : materialOf(material);
                // Every material the FBX names, the pack's one (a plain slot its plain colour).
                Plain.TryGetValue(model, out var plain);
                // (What it names: its own materials, or those remapped already on an import before.)
                var names = AssetDatabase.LoadAllAssetsAtPath(to).OfType<Material>().Select(m => m.name)
                    .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name)).Distinct().ToList();
                foreach (string name in names)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), plain.slot != null && name == plain.slot ? PlainOf(model, own, plain.u, plain.v) ?? own : own);
                importer.SaveAndReimport();
                Debug.Log($"[source props] {to} with {(own != null ? own.name : "no material")}");
            }
        }

        /// <summary>The source model cut by Blender (unity/tools/props-cut.py) into `to`; whether it was.</summary>
        static bool Cut(string root, string from, string to, string cut)
        {
            string blender = System.Environment.GetEnvironmentVariable("BLENDER") ?? (File.Exists("/opt/homebrew/bin/blender") ? "/opt/homebrew/bin/blender" : "blender");
            string script = Path.Combine(root, "unity/tools/props-cut.py");
            // The cut in a file of its own: no quotes to get through the command line.
            string spec = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(to) + "-cut.json");
            File.WriteAllText(spec, cut);
            var run = new System.Diagnostics.ProcessStartInfo(blender, $"--background --factory-startup --python \"{script}\" -- \"{from}\" \"{Path.GetFullPath(to)}\" \"{spec}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            try
            {
                using var p = System.Diagnostics.Process.Start(run);
                string said = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                string line = said.Split('\n').FirstOrDefault(l => l.StartsWith("[props-cut]"));
                if (p.ExitCode != 0 || line == null || !File.Exists(to))
                {
                    Debug.LogWarning($"[source props] {from}: Blender didn't cut it ({p.ExitCode}): {said.Substring(System.Math.Max(0, said.Length - 600))}");
                    return false;
                }
                Debug.Log("[source props] " + line);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[source props] {from}: no Blender ({blender}): {e.Message}");
                return false;
            }
        }

        /// <summary>`like` in one colour: its atlas's at (u, v), on a texture of 4 × 4 of it, made in Extra (once).</summary>
        static Material PlainOf(string model, Material like, float u, float v)
        {
            if (like == null) return null;
            string path = $"{Folder}/{model}_Plain.mat";
            var made = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (made != null) return made;
            var atlas = like.GetTexture("_Albedo_Map") as Texture2D;
            if (atlas == null) return null;
            // Read through a render texture (Synty's atlases aren't readable).
            var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(atlas, rt);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
            copy.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            var colour = copy.GetPixel(Mathf.Clamp(Mathf.FloorToInt(u * atlas.width), 0, atlas.width - 1), Mathf.Clamp(Mathf.FloorToInt(v * atlas.height), 0, atlas.height - 1));
            Object.DestroyImmediate(copy);
            var swatch = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            swatch.SetPixels(Enumerable.Repeat(colour, 16).ToArray());
            swatch.Apply();
            string texture = $"{Folder}/{model}_Plain.png";
            File.WriteAllBytes(texture, swatch.EncodeToPNG());
            Object.DestroyImmediate(swatch);
            AssetDatabase.ImportAsset(texture, ImportAssetOptions.ForceUpdate);
            made = new Material(like) { name = $"{model}_Plain" };
            made.SetTexture("_Albedo_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            made.SetTexture("_Emission_Map", null);
            AssetDatabase.CreateAsset(made, path);
            Debug.Log($"[source props] {model}'s {path}: #{ColorUtility.ToHtmlStringRGB(colour)}");
            return made;
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
