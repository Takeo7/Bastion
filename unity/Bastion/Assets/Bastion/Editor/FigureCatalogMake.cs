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
        /// <summary>A voice's prefab, by its name, in the packs' Characters folders.</summary>
        static GameObject VoicePrefab(string name)
        {
            foreach (string folder in new[] { "Assets/Synty/PolygonOffice/Prefabs/Characters", "Assets/Synty/PolygonCity/Prefabs/Characters", "Assets/Synty/PolygonApocalypse/Prefabs/Characters" })
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

        public static void Run()
        {
            var presets = new SortedDictionary<string, GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Characters }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                presets[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
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
            // POLYGON's characters for the radio's voices (Bastion.Client.Voices), and the headset they wear.
            var characters = Voices.Prefabs.Select(n => (n, prefab: VoicePrefab(n))).Where(c => c.prefab != null).ToList();
            foreach (string n in Voices.Prefabs.Where(n => VoicePrefab(n) == null)) missing.Add(n);
            catalog.characterNames = characters.Select(c => c.n).ToArray();
            catalog.characters = characters.Select(c => c.prefab).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[figures] {Asset}: {presets.Count} presets, {maps.Count} colour maps, {clips.Count} clips, {weapons.Count} weapons, {characters.Count} characters; not in the project: {string.Join(", ", missing)}");
            Debug.Log("[figures] deaths settle: " + string.Join(", ", catalog.clipIds.Select((id, k) => (id, k)).Where(p => p.id.StartsWith("death"))
                .Select(p => $"{p.id} {catalog.settles[p.k]:0.00}/{catalog.clips[p.k].length:0.00} s")));
        }
    }
}
