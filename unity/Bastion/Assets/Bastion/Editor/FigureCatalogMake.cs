using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        const string Asset = "Assets/Bastion/Client/Resources/FigureCatalog.asset";
        const string Characters = "Assets/Synty/SidekickCharacters/Characters";

        /// <summary>Where each of 0.10.1's animation packs is in the project.</summary>
        static readonly Dictionary<string, string> Packs = new Dictionary<string, string>
        {
            ["baseLocomotion"] = "Assets/Synty/AnimationBaseLocomotion",
            ["idles"] = "Assets/Synty/AnimationIdles",
            ["bowCombat"] = "Assets/Synty/AnimationBowCombat",
            ["swordCombat"] = "Assets/Synty/AnimationSwordCombat",
            ["goblinLocomotion"] = "Assets/Synty/AnimationGoblinLocomotion",
        };

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
            // The weapons, as the map's props are found (a prefab, else the model).
            var prefabOf = MapLab.Prefabs();
            var weapons = ((JObject)anims["weapons"]).Properties().Select(p => (string)p.Value["fbx"]).Distinct().OrderBy(n => n)
                .Select(n => (n, prefab: prefabOf(n))).Where(w => w.prefab != null).ToList();
            catalog.weaponNames = weapons.Select(w => w.n).ToArray();
            catalog.weapons = weapons.Select(w => w.prefab).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[figures] {Asset}: {presets.Count} presets, {maps.Count} colour maps, {clips.Count} clips, {weapons.Count} weapons; not in the project: {string.Join(", ", missing)}");
            Debug.Log("[figures] deaths settle: " + string.Join(", ", catalog.clipIds.Select((id, k) => (id, k)).Where(p => p.id.StartsWith("death"))
                .Select(p => $"{p.id} {catalog.settles[p.k]:0.00}/{catalog.clips[p.k].length:0.00} s")));
        }
    }
}
