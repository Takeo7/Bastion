using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Units;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Makes Resources/FigureCatalog.asset (Bastion.Client.Units.FigureCatalog): every Sidekick preset's prefab and colour
    /// map under Assets/Synty/SidekickCharacters/Characters, and every clip Anims.json names that is in the project (Synty's
    /// animation packs; Mixamo's, by the game's id, in Assets/Mixamo). Run it again after importing packs or clips:
    /// unity/tools/unity.sh -executeMethod Bastion.EditorTools.FigureCatalogMake.Run -quit.
    /// </summary>
    public static class FigureCatalogMake
    {
        const string UndeadFile = "Assets/Bastion/Client/Resources/Undead.json";
        /// <summary>Where the living made dead are baked: with Synty, out of git (made again here after an import).</summary>
        const string UndeadFolder = "Assets/Synty/Extra/Undead";

        /// <summary>
        /// `mesh`'s blend shapes as `r` weighs them (the preset's build) folded into its vertices, and gone: the figure keeps
        /// its build, not Sidekick's every shape (17 MB a body with them, the game sets none).
        /// </summary>
        static void Flatten(SkinnedMeshRenderer r, Mesh mesh)
        {
            int shapes = mesh.blendShapeCount;
            if (shapes == 0) return;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var dv = new Vector3[vertices.Length];
            var dn = new Vector3[vertices.Length];
            var dt = new Vector3[vertices.Length];
            for (int s = 0; s < shapes; s++)
            {
                int frame = mesh.GetBlendShapeFrameCount(s) - 1;
                if (frame < 0) continue;
                float w = r.GetBlendShapeWeight(s) / Mathf.Max(1e-4f, mesh.GetBlendShapeFrameWeight(s, frame));
                if (Mathf.Abs(w) < 1e-5f) continue;
                mesh.GetBlendShapeFrameVertices(s, frame, dv, dn, dt);
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] += dv[i] * w;
                    if (normals.Length == vertices.Length) normals[i] += dn[i] * w;
                    if (tangents.Length == vertices.Length) tangents[i] += (Vector4)(dt[i] * w);
                }
            }
            mesh.ClearBlendShapes();
            mesh.vertices = vertices;
            if (normals.Length == vertices.Length) mesh.normals = normals.Select(n => n.normalized).ToArray();
            if (tangents.Length == vertices.Length) mesh.tangents = tangents;
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Each of UndeadFigure.Bodies made as the game would and saved: its mesh, its palette, its material and the prefab,
        /// by its id; what was there before made again.
        /// </summary>
        static Dictionary<string, GameObject> UndeadBake()
        {
            var made = new Dictionary<string, GameObject>();
            if (!AssetDatabase.IsValidFolder(UndeadFolder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(UndeadFolder), Path.GetFileName(UndeadFolder));
            foreach (var (id, _, _) in UndeadFigure.Bodies)
            {
                var model = UndeadFigure.Fresh(id);
                if (model == null) continue;
                model.transform.SetParent(null, false);
                model.SetActive(true);
                model.name = id;
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = r.sharedMesh;
                    if (mesh != null && !AssetDatabase.Contains(mesh))
                    {
                        mesh.name = id;
                        Flatten(r, mesh);
                        AssetDatabase.CreateAsset(mesh, $"{UndeadFolder}/{id}.mesh.asset");
                    }
                    var material = r.sharedMaterial;
                    if (material == null || AssetDatabase.Contains(material)) continue;
                    var palette = material.GetTexture("_ColorMap") as Texture2D;
                    if (palette != null && !AssetDatabase.Contains(palette)) AssetDatabase.CreateAsset(palette, $"{UndeadFolder}/{id}.palette.asset");
                    material.name = id;
                    AssetDatabase.CreateAsset(material, $"{UndeadFolder}/{id}.mat");
                }
                made[id] = PrefabUtility.SaveAsPrefabAsset(model, $"{UndeadFolder}/{id}.prefab");
                UnityEngine.Object.DestroyImmediate(model);
            }
            Debug.Log($"[figures] the living made dead baked in {UndeadFolder}: {made.Count} of {UndeadFigure.Bodies.Length}");
            return made;
        }

        /// <summary>
        /// Resources/Undead.json: the living presets the living made dead are put together from (UndeadFigure.Bodies), each
        /// its parts by type and its build, read off its .sk file (0.10.1's convert.ts presetParts, its sliders): a .sk is
        /// no asset the game can load.
        /// </summary>
        static void Undead()
        {
            var all = new JObject();
            foreach (string preset in UndeadFigure.Bodies.Select(b => b.preset).Distinct())
            {
                string sk = Directory.GetFiles(Characters, preset + ".sk", SearchOption.AllDirectories).FirstOrDefault();
                if (sk == null)
                {
                    Debug.LogWarning($"[figures] no {preset}.sk under {Characters}");
                    continue;
                }
                var parts = new JArray();
                string name = null;
                float type = 0, size = 0, muscle = 0;
                foreach (string raw in File.ReadLines(sk))
                {
                    string line = raw.Trim();
                    float Value() => float.Parse(line.Substring(line.IndexOf(':') + 1).Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    if (line.StartsWith("- Name: ")) name = line.Substring(8).Trim();
                    else if (line.StartsWith("PartType: ") && name != null)
                    {
                        parts.Add(new JArray(name, line.Substring(10).Trim()));
                        name = null;
                    }
                    else if (line.StartsWith("BodyTypeValue:")) type = Value();
                    else if (line.StartsWith("BodySizeValue:")) size = Value();
                    else if (line.StartsWith("MuscleValue:")) muscle = Value();
                }
                all[preset] = new JObject { ["parts"] = parts, ["type"] = type, ["size"] = size, ["muscle"] = muscle };
            }
            File.WriteAllText(UndeadFile, all.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");
            AssetDatabase.ImportAsset(UndeadFile);
            Debug.Log($"[figures] {UndeadFile}: {all.Count} presets");
        }

        /// <summary>A POLYGON character's prefab (a voice's, one of the special dead's), by its name, in the packs' Characters folders.</summary>
        static GameObject VoicePrefab(string name)
        {
            foreach (string folder in new[] { "Assets/Synty/PolygonOffice/Prefabs/Characters", "Assets/Synty/PolygonCity/Prefabs/Characters", "Assets/Synty/PolygonApocalypse/Prefabs/Characters",
                "Assets/Synty/PolygonCityZombies/Prefabs/Characters", "Assets/Synty/PolygonBossZombies/Prefabs" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.prefab");
                if (prefab != null) return prefab;
            }
            return null;
        }

        const string Asset = "Assets/Bastion/Client/Resources/FigureCatalog.asset";
        const string Characters = "Assets/Synty/SidekickCharacters/Characters";

        /// <summary>Where each of 0.10.1's animation packs is in the project.</summary>
        static readonly Dictionary<string, string> Packs = new Dictionary<string, string>
        {
            ["baseLocomotion"] = "Assets/Synty/AnimationBaseLocomotion",
            ["idles"] = "Assets/Synty/AnimationIdles",
            // (Only the camp's two gestures: unity/tools/importar-synty.sh.)
            ["emotes"] = "Assets/Synty/AnimationEmotesAndTaunts",
            ["bowCombat"] = "Assets/Synty/AnimationBowCombat",
            ["swordCombat"] = "Assets/Synty/AnimationSwordCombat",
            ["goblinLocomotion"] = "Assets/Synty/AnimationGoblinLocomotion",
        };

        /// <summary>
        /// Each palette cell's kind (FigureCatalog.cellKinds): the same number for cells Sidekick's material maps (its
        /// metal, gloss, reflection, opacity, folds' shade and skin masks, read from their files) make alike texel for texel.
        /// </summary>
        static int[] CellKinds()
        {
            // (By their files: M_BaseMaterial's shader doesn't declare them all.)
            var maps = new[] { "T_MetallicMap", "T_SmoothnessMap", "T_OpacityMap", "T_ReflectionMap", "Mask_Dark", "T_SkinMask_Map" }
                .Select(n => AssetDatabase.FindAssets(n + " t:Texture2D", new[] { "Assets/Synty/SidekickCharacters" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == n))
                .Where(f => f != null)
                .Select(f =>
                {
                    var copy = new Texture2D(2, 2);
                    copy.LoadImage(File.ReadAllBytes(f));
                    return copy;
                })
                .ToList();
            var kinds = new int[256];
            var seen = new Dictionary<string, int>();
            for (int c = 0; c < 256; c++)
            {
                int row = c / 16, column = c % 16;
                var key = new System.Text.StringBuilder();
                foreach (var m in maps)
                {
                    int n = Mathf.Max(1, m.width / 16);
                    for (int dy = 0; dy < n; dy++)
                        for (int dx = 0; dx < n; dx++)
                            key.Append(((Color32)m.GetPixel(column * n + dx, m.height - 1 - (row * n + dy))).r).Append(',');
                    key.Append('|');
                }
                if (!seen.TryGetValue(key.ToString(), out int k)) seen[key.ToString()] = k = seen.Count;
                kinds[c] = k;
            }
            foreach (var m in maps) UnityEngine.Object.DestroyImmediate(m);
            Debug.Log($"[figures] the palette's cells: {seen.Count} kinds from {maps.Count} maps");
            return kinds;
        }

        /// <summary>
        /// When a clip's body stops moving (syntyAnims.ts settleOf): the last moment the humanoid body's position moves more
        /// than 0.3 % of its height in a sample (a thirtieth of a second), and a beat to lie still.
        /// </summary>
        static float SettleOf(AnimationClip clip)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            AnimationCurve Curve(string name) => bindings.Where(b => b.propertyName == name).Select(b => AnimationUtility.GetEditorCurve(clip, b)).FirstOrDefault();
            AnimationCurve x = Curve("RootT.x"), y = Curve("RootT.y"), z = Curve("RootT.z");
            if (x == null || y == null || z == null) return clip.length;
            Vector3 At(float t) => new Vector3(x.Evaluate(t), y.Evaluate(t), z.Evaluate(t));
            float still = Mathf.Abs(At(0).y) * 0.003f;
            const float step = 1 / 30f;
            float last = 0;
            for (float t = step; t <= clip.length; t += step)
                if ((At(t) - At(t - step)).magnitude > still) last = t;
            return Mathf.Min(clip.length, last + 0.12f);
        }

        /// <summary>
        /// The Sidekick parts no wardrobe piece names (Wardrobe.json) out of Resources, where every one went into the app
        /// (Gameplay 26/10, Red's Build Report: 814 parts, 176 MB), to UndeadFigure.Spare, outside git like the rest of Synty;
        /// and back if the wardrobe names one again. Moved by AssetDatabase, so their GUIDs stay, and with them the presets'
        /// links and the bake's (UndeadFigure finds them there in the editor). Run does it last; after importing Synty again
        /// all are back in Resources until it runs. AppBuild checks that the wardrobe's are there.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.FigureCatalogMake.Trim -quit
        /// </summary>
        public static void Trim()
        {
            var named = new HashSet<string>(JObject.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/Wardrobe.json"))["pieces"]
                .SelectMany(p => p["parts"].Select(n => (string)n))) { "SK_BaseModel" };
            int moved = 0, back = 0;
            long bytes = 0;
            foreach (var (from, to, away) in new[] { (UndeadFigure.Meshes, UndeadFigure.Spare, true), (UndeadFigure.Spare, UndeadFigure.Meshes, false) })
            {
                if (!AssetDatabase.IsValidFolder(from.TrimEnd('/'))) continue;
                foreach (string path in AssetDatabase.FindAssets("t:Model", new[] { from.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList())
                {
                    if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) || named.Contains(Path.GetFileNameWithoutExtension(path)) == away) continue;
                    string target = to + path.Substring(from.Length);
                    Folder(Path.GetDirectoryName(target).Replace('\\', '/'));
                    string error = AssetDatabase.MoveAsset(path, target);
                    if (error.Length > 0)
                    {
                        Debug.LogWarning($"[figures] {path} not moved: {error}");
                        continue;
                    }
                    if (away)
                    {
                        moved++;
                        bytes += new FileInfo(target).Length;
                    }
                    else back++;
                }
            }
            var left = AssetDatabase.FindAssets("t:Model", new[] { UndeadFigure.Meshes.TrimEnd('/') }).Length;
            Debug.Log($"[figures] Sidekick's parts: {moved} out of Resources ({bytes / 1e6:0} MB of FBX), {back} back; {left} in Resources ({named.Count} named by the wardrobe, the base model among them)");
        }

        /// <summary>A folder of the project, and those it is in, made if they aren't there.</summary>
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        public static void Run()
        {
            // Only the presets the game draws (UnitViews.PresetsUsed; Gameplay 26/10, Red's Build Report: the others went into
            // the app for nothing, 62 MB of living police, survivors and civilians). The living made dead are baked below.
            var used = new HashSet<string>(UnitViews.PresetsUsed);
            var presets = new SortedDictionary<string, GameObject>();
            int left = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Characters }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid), name = Path.GetFileNameWithoutExtension(path);
                if (used.Contains(name)) presets[name] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                else left++;
            }
            var maps = new SortedDictionary<string, Texture2D>();
            foreach (string guid in AssetDatabase.FindAssets("ColorMap t:Texture2D", new[] { Characters }))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (!name.StartsWith("T_") || !name.EndsWith("ColorMap")) continue;
                maps[name.Substring(2, name.Length - 2 - "ColorMap".Length)] = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            }

            var anims = JObject.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/Anims.json"));
            var clips = new SortedDictionary<string, AnimationClip>();
            var missing = new List<string>();
            foreach (var entry in (JObject)anims["clips"])
            {
                string pack = (string)entry.Value["pack"];
                string path = pack == "mixamo" ? $"Assets/Mixamo/{entry.Key}.fbx" : Packs.TryGetValue(pack, out var folder) ? $"{folder}/{(string)entry.Value["fbx"]}.fbx" : null;
                var clip = path != null ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__")) : null;
                if (clip != null) clips[entry.Key] = clip;
                else missing.Add(entry.Key);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<FigureCatalog>(Asset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FigureCatalog>();
                AssetDatabase.CreateAsset(catalog, Asset);
            }
            catalog.presetNames = presets.Keys.ToArray();
            catalog.presets = presets.Values.ToArray();
            catalog.colourMapNames = maps.Keys.ToArray();
            catalog.colourMaps = maps.Values.ToArray();
            catalog.clipIds = clips.Keys.ToArray();
            catalog.clips = clips.Values.ToArray();
            catalog.settles = catalog.clips.Select(SettleOf).ToArray();
            catalog.cellKinds = CellKinds();
            // The weapons, as the map's props are found (a prefab, else the model).
            var prefabOf = MapLab.Prefabs();
            // (And the Armería's loose improvements, Settlement.Scope and Silencer.)
            var weapons = ((JObject)anims["weapons"]).Properties().Select(p => (string)p.Value["fbx"]).Concat(new[] { Settlement.Scope, Settlement.Silencer }).Distinct().OrderBy(n => n)
                .Select(n => (n, prefab: prefabOf(n))).Where(w => w.prefab != null).ToList();
            catalog.weaponNames = weapons.Select(w => w.n).ToArray();
            catalog.weapons = weapons.Select(w => w.prefab).ToArray();
            // POLYGON's characters for the radio's voices (Bastion.Client.Voices), and the headset they wear; and the special
            // dead's (UnitViews.DeadPrefabs).
            var names = Voices.Prefabs.Concat(Bastion.Client.Units.UnitViews.DeadPrefabs).Distinct().ToList();
            var characters = names.Select(n => (n, prefab: VoicePrefab(n))).Where(c => c.prefab != null).ToList();
            foreach (string n in names.Where(n => VoicePrefab(n) == null)) missing.Add(n);
            catalog.characterNames = characters.Select(c => c.n).ToArray();
            catalog.characters = characters.Select(c => c.prefab).ToArray();
            Undead();
            // The living made dead, baked (UndeadFigure: put together at run time they take 75–290 ms each), as presets.
            EditorUtility.SetDirty(catalog);
            var baked = UndeadBake();
            if (baked.Count > 0)
            {
                foreach (var kv in baked) presets[kv.Key] = kv.Value;
                catalog.presetNames = presets.Keys.ToArray();
                catalog.presets = presets.Values.ToArray();
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            missing.AddRange(used.Where(n => !presets.ContainsKey(n)));
            Debug.Log($"[figures] {Asset}: {presets.Count} presets ({left} left out: no one draws them), {maps.Count} colour maps, {clips.Count} clips, {weapons.Count} weapons, {characters.Count} characters; not in the project: {string.Join(", ", missing)}");
            Trim();
            Debug.Log("[figures] deaths settle: " + string.Join(", ", catalog.clipIds.Select((id, k) => (id, k)).Where(p => p.id.StartsWith("death"))
                .Select(p => $"{p.id} {catalog.settles[p.k]:0.00}/{catalog.clips[p.k].length:0.00} s")));
        }
    }
}
