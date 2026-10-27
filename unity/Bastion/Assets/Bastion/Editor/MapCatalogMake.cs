using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Makes Resources/MapCatalog.asset (Bastion.Client.Map.MapCatalog): every model MapArt.json's props name and the
    /// materials the map repaints with, looked up in Assets/Synty as the labs do; and the models whose meshes the map
    /// merges, imported readable. Run it again when the props change:
    /// unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapCatalogMake.Run -quit.
    /// </summary>
    public static class MapCatalogMake
    {
        const string Asset = "Assets/Bastion/Client/Resources/MapCatalog.asset";

        public static void Run()
        {
            // The bodies of the houses' and clinics' dressing, baked lying first (CorpseBake), so they are among the props.
            CorpseBake.Run();
            var prefabOf = MapLab.Prefabs();
            var materialOf = MapLab.Materials();
            var missing = new List<string>();
            Readable(prefabOf);

            // The props' models, and La Lata's (its vehicles and La Mula's plates).
            var modelNames = MapArt.Models().Concat(LaLata.Models).Append(SnowCover.Mound).Distinct().OrderBy(n => n).ToList();
            var models = modelNames.Select(n => prefabOf(n)).ToList();
            // The sky's cloud rings, by their own ids (UI's Sky).
            foreach (var (id, key) in SkyModels)
            {
                modelNames.Add(id);
                models.Add(prefabOf(key));
            }
            var materialNames = MapBuilder.MaterialsUsed.Distinct().OrderBy(n => n).ToList();
            var materials = materialNames.Select(n => materialOf(n)).ToList();
            for (int k = 0; k < modelNames.Count; k++) if (models[k] == null) missing.Add(modelNames[k]);
            for (int k = 0; k < materialNames.Count; k++) if (materials[k] == null) missing.Add(materialNames[k]);

            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(Asset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MapCatalog>();
                AssetDatabase.CreateAsset(catalog, Asset);
            }
            // Only what was found: a name without its model would only be a gap the builder reports anyway.
            var found = Enumerable.Range(0, modelNames.Count).Where(k => models[k] != null).ToList();
            catalog.modelNames = found.Select(k => modelNames[k]).ToArray();
            catalog.models = found.Select(k => models[k]).ToArray();
            catalog.modelTurns = found.Select(k => MapLab.UnityTurnOf(AssetDatabase.GetAssetPath(models[k]))).ToArray();
            var foundMaterials = Enumerable.Range(0, materialNames.Count).Where(k => materials[k] != null).ToList();
            catalog.materialNames = foundMaterials.Select(k => materialNames[k]).ToArray();
            catalog.materials = foundMaterials.Select(k => materials[k]).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[catalog] {Asset}: {catalog.models.Length} models, {catalog.materials.Length} materials" + (missing.Count > 0 ? "; missing: " + string.Join(", ", missing) : ""));
        }

        /// <summary>
        /// syntyManifest.ts SKY_MODELS: Generic's ring, and Alpine Mountain's thinner one (Extra: its FBX from the pack's
        /// source files, as SnowCover's mound).
        /// </summary>
        static readonly (string id, string key)[] SkyModels =
        {
            ("cloudRing", "natureGeneric:Assets/Synty/PolygonGeneric/Models/SM_Gen_Env_Cloud_Ring_01"),
            ("cloudRingThin", "alpine:SM_Env_Cloud_Ring_01"),
        };

        /// <summary>
        /// The props whose meshes the map merges as it is made (Mesh.CombineMeshes, which play mode and a build only do
        /// with meshes they can read: the rest it leaves out, with «Cannot combine mesh that does not allow access»): the
        /// pines of the base's wood past the edge (MapBuilder.Beyond), and what PropParts merges (UI's GroundCover,
        /// FrameArt, PumpCanopies and StreetDressing: every prop id they name). UI 26/10: the ground's pebbles, papers, tufts and junk
        /// were missing in play mode.
        /// </summary>
        static IEnumerable<string> Merged => BaseWoods.BasePines.Append(BaseWoods.FarPine).Concat(PropPartsIds).Distinct();

        /// <summary>The prop ids GroundCover, FrameArt, PumpCanopies and StreetDressing name (26/10): a new one there goes here too.</summary>
        static readonly string[] PropPartsIds =
        {
            // GroundCover.
            "bike", "cardboard", "cardboard2", "cardboard4", "drain", "flowers", "grassBlob", "holdall", "leaves", "luggage", "luggageOpen",
            "luggageRetro", "manhole", "papers", "pebbles", "pumpIsland", "streetLight", "tire", "trash", "trashBag", "trolley",
            "trolleyBroken", "tuft", "tuftLow", "tuftWide",
            // FrameArt.
            "barricade1", "boulder", "brickStack", "bus", "bush", "carRusted", "carWreck", "cementBags", "cliff", "cliff2", "concreteFence",
            "container", "containerBlue", "containerOrange", "debrisPile", "debrisPile2", "floodlight", "guardTower", "log", "milBarrierTall",
            "milCrate", "palletStack", "pineLarge", "pineLarge2", "pipes", "portaloo", "razorWire", "rock", "rubble1", "rubble2", "ruinPile",
            "sandbags", "sheetFence", "sheetFence2", "tirePile", "trashPile", "tree", "treeDead", "treePine", "van",
            // PumpCanopies.
            "pumpCanopy", "pumpCanopyBurnt",
            // StreetDressing.
            "hydrant", "mailbox", "pallet", "powerLine", "powerPole", "stopSign", "trashBag2",
        };

        /// <summary>
        /// The models of Merged imported readable (their FBX's importer), those that weren't; and the 4×4's, whose doors
        /// La Lata cuts out of its shell (LaLata.CutDoors).
        /// </summary>
        static void Readable(System.Func<string, GameObject> prefabOf)
        {
            var sources = new HashSet<string>();
            var models = Merged.Select(id => MapArt.Prop(id)).Select(art => art?.fbx != null ? prefabOf(MapArt.ModelKey(art)) : null).Append(prefabOf(LaLata.Bodies[0]));
            foreach (var model in models)
            {
                if (model == null) continue;
                foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null && !f.sharedMesh.isReadable) sources.Add(AssetDatabase.GetAssetPath(f.sharedMesh));
            }
            foreach (string path in sources)
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.isReadable) continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            Debug.Log($"[catalog] readable for merging: {(sources.Count > 0 ? string.Join(", ", sources.Select(Path.GetFileName)) : "all were already")}");
        }

        /// <summary>
        /// Every recorded state of a biome (BASTION_BIOME, city unless said) in engine-cs/golden/step*/ (one a file, or a file's
        /// cases) built with the
        /// game's catalog, and what each asks for that the catalog lacks: the maps the game would draw with gaps.
        /// unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapCatalogMake.Check -quit
        /// </summary>
        public static void Check()
        {
            string biome = System.Environment.GetEnvironmentVariable("BASTION_BIOME") ?? "city";
            var catalog = MapCatalog.Load() ?? throw new FileNotFoundException(Asset);
            string golden = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../engine-cs/golden"));
            var gaps = new SortedSet<string>();
            int maps = 0;
            // A state of its own (step1, step2), or many, each a case's (step3: {cases: [{name, state}]}).
            var states = new List<(string name, Newtonsoft.Json.Linq.JToken state)>();
            foreach (string file in Directory.GetDirectories(golden, "step*").OrderBy(d => d).SelectMany(d => Directory.GetFiles(d, "*.json").OrderBy(f => f)))
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));
                string where = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file)) + "/" + System.IO.Path.GetFileNameWithoutExtension(file);
                if (json["state"] != null) states.Add((where, json["state"]));
                if (json["cases"] is Newtonsoft.Json.Linq.JArray cases)
                    foreach (var c in cases)
                        if (c["state"] != null) states.Add(($"{where}/{(string)c["name"]}", c["state"]));
            }
            foreach (var (name, json) in states)
            {
                var state = Bastion.Net.Json.Read<GameState>(json.ToString(Newtonsoft.Json.Formatting.None));
                if (state.Biome != biome) continue;
                var root = new GameObject("Comprobación").transform;
                var builder = new MapBuilder(state, root, catalog.PrefabOf(), catalog.MaterialOf());
                builder.Build();
                Object.DestroyImmediate(root.gameObject);
                maps++;
                Debug.Log($"[catalog] {name} ({state.MapId}, {state.Site ?? "-"}): {builder.Placed} props" + (builder.Missing.Count > 0 ? "; missing: " + string.Join(", ", builder.Missing.OrderBy(m => m)) : ""));
                gaps.UnionWith(builder.Missing);
            }
            Debug.Log($"[catalog] {maps} {biome} maps; missing in all: " + (gaps.Count > 0 ? string.Join(", ", gaps) : "nothing"));
        }
    }
}
