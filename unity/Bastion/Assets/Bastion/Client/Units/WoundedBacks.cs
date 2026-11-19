#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// El herido's check (UnitViews.Wounded), the editor's: every rucksack of the wardrobe (Sidekick's AttachmentBack
    /// parts, «_24ABAC_» in Wardrobe.json) and the bones its corners are skinned to. Who carries hides it by shrinking
    /// «backAttach», so one skinned to another bone would stay drawn. «[espaldas]» a line each. Here and not in Editor:
    /// Sidekick's database is this assembly's.
    /// unity/tools/unity.sh -executeMethod Bastion.Client.Units.WoundedBacks.Run -quit
    /// </summary>
    public static class WoundedBacks
    {
        public static void Run()
        {
            var text = Resources.Load<TextAsset>("Wardrobe");
            var names = System.Text.RegularExpressions.Regex.Matches(text.text, "\"([A-Za-z0-9_]*_24ABAC_[A-Za-z0-9_]*)\"").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
            var database = new DatabaseManager();
            database.GetDbConnection();
            foreach (string name in names)
            {
                var part = SidekickPart.SearchForByName(database, name)?.GetPartModel();
                var r = part != null ? part.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (r == null || r.sharedMesh == null)
                {
                    Debug.Log($"[espaldas] {name}: no model");
                    continue;
                }
                var share = new Dictionary<string, float>();
                foreach (var w in r.sharedMesh.boneWeights)
                    foreach (var (bone, weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                        if (weight > 0 && bone < r.bones.Length && r.bones[bone] != null) share[r.bones[bone].name] = share.TryGetValue(r.bones[bone].name, out float had) ? had + weight : weight;
                float all = share.Values.Sum();
                // (Hidden if every bone of it is backAttach or hangs under it.)
                var loose = r.bones.Where(b => b != null && share.ContainsKey(b.name) && !b.GetComponentsInParent<Transform>(true).Any(t => t.name == "backAttach")).Select(b => b.name).Distinct().ToList();
                Debug.Log($"[espaldas] {name}: " + (loose.Count == 0 ? "all under backAttach" : "NOT under backAttach: " + string.Join(", ", loose)));
                Debug.Log($"[espaldas] {name}: " + string.Join(", ", share.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / all * 100:0.#} %")));
            }
        }
    }
}
#endif
