using System;
using System.Collections.Generic;
using System.Linq;
using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A soldier's figure from their Dress (0.10.1's wardrobe.ts modelOf): the pieces' parts, out of Sidekick's database,
    /// put together by the Sidekicks tool's runtime API into one mesh on one rig, in a palette of their own
    /// (SoldierPalette). One per dress is kept (a copy each after the first). Joined in one mesh (the battle's, the busts':
    /// one draw call) it takes 30–200 ms, Sidekick's joining most of it: made as a mission loads, never in the middle of a
    /// turn. Its parts left apart (`separate`: Soldados' soldier, Personalizar's previews) about 25 ms, and made in the
    /// background a step a frame (Prepare) it is there when it is asked for.
    /// </summary>
    public static class SoldierFigure
    {
        static DatabaseManager database;
        static readonly Dictionary<string, GameObject> made = new Dictionary<string, GameObject>();
        static Transform shelf;

        static string KeyOf(Dress dress, bool separate) => separate ? dress.Key + "|apart" : dress.Key;

        /// <summary>
        /// A new figure in that dress (inactive copies are kept on a shelf of their own), its parts joined in one mesh or
        /// `separate`; null if a part is missing.
        /// </summary>
        public static GameObject Make(Dress dress, string name, bool separate = false)
        {
            string key = KeyOf(dress, separate);
            if (!made.TryGetValue(key, out var model) || model == null)
            {
                queued.Remove(key);
                if (job.key == key) job = default;
                made[key] = model = Build(dress, separate);
            }
            if (model == null) return null;
            var figure = UnityEngine.Object.Instantiate(model);
            figure.name = name;
            figure.SetActive(true);
            return figure;
        }

        /// <summary>Whether that dress is made already (a figure of it is a copy, at once).</summary>
        public static bool Ready(Dress dress, bool separate = false) => made.TryGetValue(KeyOf(dress, separate), out var m) && m != null;

        /// <summary>
        /// These dresses made in the background while the game runs, in their order, a step a frame (the parts, the
        /// palette, the figure); what was waiting from before is let go. In edit mode, nothing.
        /// </summary>
        public static void Prepare(IEnumerable<Dress> dresses, bool separate = false)
        {
            queued.Clear();
            pending.Clear();
            foreach (var d in dresses)
            {
                string key = KeyOf(d, separate);
                if (Ready(d, separate) || pending.ContainsKey(key)) continue;
                pending[key] = (d, separate);
                queued.Add(key);
            }
            if (!Application.isPlaying || queued.Count == 0 || runner != null) return;
            var o = new GameObject("Soldados por hacer") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(o);
            runner = o.AddComponent<Runner>();
        }

        /// <summary>How many dresses wait to be made in the background, and how long its last step took (ms).</summary>
        public static int Waiting => queued.Count + (job.steps != null ? 1 : 0);
        public static float LastStepMs { get; private set; }

        static readonly List<string> queued = new List<string>();
        static readonly Dictionary<string, (Dress dress, bool separate)> pending = new Dictionary<string, (Dress, bool)>();
        static (string key, IEnumerator<GameObject> steps) job;
        static Runner runner;

        /// <summary>A step of the background's next figure a frame.</summary>
        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (job.steps == null)
                {
                    while (queued.Count > 0 && job.steps == null)
                    {
                        string key = queued[0];
                        queued.RemoveAt(0);
                        if (!pending.TryGetValue(key, out var p) || made.TryGetValue(key, out var m) && m != null) continue;
                        job = (key, Building(p.dress, p.separate));
                    }
                    if (job.steps == null) return;
                }
                var clock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    if (job.steps.MoveNext())
                    {
                        if (job.steps.Current != null) made[job.key] = job.steps.Current;
                        else
                        {
                            LastStepMs = (float)clock.Elapsed.TotalMilliseconds;
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[figures] {job.key}: {e.Message}");
                }
                LastStepMs = (float)clock.Elapsed.TotalMilliseconds;
                pending.Remove(job.key);
                job = default;
            }
        }

        static GameObject Build(Dress dress, bool separate)
        {
            GameObject model = null;
            var steps = Building(dress, separate);
            while (steps.MoveNext()) if (steps.Current != null) model = steps.Current;
            return model;
        }

        /// <summary>The figure made in steps, null between them: each part out of the database, the palette, the figure.</summary>
        static IEnumerator<GameObject> Building(Dress dress, bool separate)
        {
            var catalog = FigureCatalog.Load();
            if (catalog == null)
            {
                Debug.LogError("[figures] Resources/FigureCatalog.asset is missing: unity/tools/unity.sh -executeMethod Bastion.EditorTools.FigureCatalogMake.Run -quit");
                yield break;
            }
            if (database == null)
            {
                database = new DatabaseManager();
                database.GetDbConnection();
            }
            var budget = System.Diagnostics.Stopwatch.StartNew();
            var pieces = dress.Pieces().Select(p => (p.piece, art: Wardrobe.Piece(p.piece, p.id))).Where(p => p.art != null).ToList();
            var parts = new Dictionary<string, SkinnedMeshRenderer>();
            foreach (string name in pieces.SelectMany(p => p.art.parts).Distinct())
            {
                var part = SidekickPart.SearchForByName(database, name)?.GetPartModel();
                var renderer = part != null ? part.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (renderer == null)
                {
                    Debug.LogWarning($"[figures] no Sidekick part {name}");
                    continue;
                }
                parts[name] = renderer;
                // Parts for 3 ms a step (a query and a model each: well under one ms, more the first time).
                if (budget.Elapsed.TotalMilliseconds >= 3)
                {
                    yield return null;
                    budget.Restart();
                }
            }
            yield return null;
            var outfit = pieces[0].art;
            var hat = pieces.Where(p => p.piece == "headwear").Select(p => p.art).FirstOrDefault();
            List<Mesh> Meshes(WardrobePiece piece) => piece?.parts.Where(parts.ContainsKey).Select(n => parts[n].sharedMesh).ToList();
            var palette = SoldierPalette.Compose(outfit, Meshes(outfit), hat, Meshes(hat), dress.skin, dress.hairColor, catalog.ColourMap, out var cells);
            // What its paint needs (SquadTint): the pieces each cell is worn on (a cell worn on two split), the owner's band
            // round each arm.
            var marks = Marks(pieces.Select(p => (p.piece, p.art)).ToList(), parts, dress);
            var split = Split(parts, marks.of, marks.count, marks.cells, cells, catalog.cellKinds);
            if (split.Count > 0) palette = SoldierPalette.Texture(cells);

            yield return null;
            var baseModel = Resources.Load<GameObject>("Meshes/SK_BaseModel");
            var material = new Material(Resources.Load<Material>("Materials/M_BaseMaterial"));
            material.SetTexture("_ColorMap", palette);
            var runtime = new SidekickRuntime(baseModel, material, null, database)
            {
                BodyTypeBlendValue = dress.sex == "f" ? 100 : -100,
                BodySizeSkinnyBlendValue = dress.build == "slim" ? 100 : 0,
                // wardrobe.ts BUILD_WEIGHTS: a heavy build at 0.4 of Sidekick's full.
                BodySizeHeavyBlendValue = dress.build == "heavy" ? 40 : 0,
                MusclesBlendValue = -100,
            };
            // (A part split, from a copy of its model with its mesh's UVs moved: the kit's own stay as they are.)
            var temporary = new List<UnityEngine.Object>();
            var used = parts.Select(kv =>
            {
                if (!split.TryGetValue(kv.Key, out var uvs)) return kv.Value;
                var holder = UnityEngine.Object.Instantiate(kv.Value.transform.root.gameObject);
                holder.hideFlags = HideFlags.HideAndDontSave;
                var r = holder.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x => x.name == kv.Value.name);
                temporary.Add(holder);
                if (r == null) return kv.Value;
                var mesh = UnityEngine.Object.Instantiate(kv.Value.sharedMesh);
                mesh.name = kv.Value.sharedMesh.name;
                mesh.uv = uvs;
                r.sharedMesh = mesh;
                temporary.Add(mesh);
                return r;
            }).ToList();
            var model = runtime.CreateCharacter("Soldado", used, !separate, true);
            foreach (var o in temporary)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(o);
                else UnityEngine.Object.DestroyImmediate(o);
            }
            OwnColours(model);
            var kept = model.AddComponent<SoldierMarks>();
            kept.cells = marks.cells;
            kept.palette = cells;
            kept.bandLeft = marks.left;
            kept.bandRight = marks.right;
            if (shelf == null)
            {
                shelf = new GameObject("Soldados hechos").transform;
                shelf.gameObject.hideFlags = HideFlags.HideInHierarchy;
            }
            model.transform.SetParent(shelf, false);
            model.SetActive(false);
            yield return model;
        }

        /// <summary>
        /// Which pieces of the body each palette cell is worn on (convert.py write_parts, PART: by each vertex's main bone,
        /// the torso the spine's and the clavicles', the arms the upper and lower arms', the legs the pelvis', thighs' and
        /// calves'; the headgear whole), and the band's radius round each upper arm: how far its sleeve's vertices from 0.18
        /// to 0.5 of the way to the elbow stand from the bone (in the bone's own units).
        /// </summary>
        static (int[] cells, float left, float right, int[,] count, Dictionary<string, int[]> of) Marks(List<(string piece, WardrobePiece art)> pieces, Dictionary<string, SkinnedMeshRenderer> parts, Dress dress)
        {
            // (And the piece each corner of the clothes is worn on, −1 none: Split.)
            var of = new Dictionary<string, int[]>();
            // How many corners of each piece each cell is under: a cell goes with the piece most of its own are on (the
            // trousers' cell under the jacket's hem stays the trousers').
            var count = new int[256, 5];
            var rounds = new Dictionary<string, List<float>> { ["l"] = new List<float>(), ["r"] = new List<float>() };
            // (Whatever is on the head takes its colour, the mask too, as 0.10.1's converter marked every headgear.)
            bool painted = dress.headwear != null;
            foreach (var (piece, art) in pieces)
            {
                if (piece != "outfit" && !(piece == "headwear" && painted)) continue;
                foreach (string name in art.parts)
                {
                    if (!parts.TryGetValue(name, out var r) || r.sharedMesh == null) continue;
                    var mesh = r.sharedMesh;
                    var uv = mesh.uv;
                    var weights = mesh.boneWeights;
                    var vertices = mesh.vertices;
                    var bones = r.bones;
                    var mine = of[name] = Enumerable.Repeat(-1, vertices.Length).ToArray();
                    for (int v = 0; v < vertices.Length && v < uv.Length; v++)
                    {
                        int part = 1;
                        string bone = null;
                        Transform main = null;
                        if (piece == "outfit")
                        {
                            // Its bone, or the one of the game's skeleton it folds into (convert.py clean_skeleton: a pad's
                            // shoulderAttach_l the clavicle's, a twist bone its limb's).
                            var own = v < weights.Length && weights[v].boneIndex0 < bones.Length ? bones[weights[v].boneIndex0] : null;
                            main = own;
                            while (main != null && !Kept(main.name)) main = main.parent;
                            // (What rides on Sidekick's root goes with the hips.)
                            bone = main != null ? main.name.ToLowerInvariant() : own != null ? "pelvis" : "";
                            part = PartOf(bone);
                        }
                        mine[v] = part;
                        count[SoldierPalette.CellOf(uv[v]), part]++;
                        if (part != 3 || main == null || !bone.StartsWith("upperarm")) continue;
                        // Along the upper arm to the elbow, and how far out from it.
                        var upper = main;
                        var lower = upper.Cast<Transform>().FirstOrDefault(c => c.name.ToLowerInvariant().StartsWith("lowerarm"));
                        if (lower == null) continue;
                        var p = upper.InverseTransformPoint(r.transform.TransformPoint(vertices[v]));
                        var axis = lower.localPosition;
                        float t = Vector3.Dot(p, axis) / Mathf.Max(axis.sqrMagnitude, 1e-8f);
                        if (t <= 0.18f || t >= 0.5f) continue;
                        rounds[upper.name.ToLowerInvariant().EndsWith("_l") ? "l" : "r"].Add((p - axis * t).magnitude);
                    }
                }
            }
            var cells = new int[256];
            for (int c = 0; c < 256; c++)
            {
                int most = 0;
                for (int part = 1; part < 5; part++) if (count[c, part] > count[c, most]) most = part;
                if (most > 0) cells[c] = 1 << most;
            }
            float Round(List<float> d)
            {
                if (d.Count == 0) return 0;
                d.Sort();
                return d[(int)(d.Count * 0.8f)];
            }
            return (cells, Round(rounds["l"]), Round(rounds["r"]), count, of);
        }

        /// <summary>
        /// The cells worn on more than one piece split (0.10.1 dyed each corner by its own bone: convert.py write_parts; c3's
        /// shoulder pads share two cells with its hips, and by the cell went the trousers' colour): the corners of a piece a
        /// cell isn't mostly on moved onto a free cell of the same kind (`kinds`, FigureCatalog.cellKinds), in its colour,
        /// marked that piece's (`cells`, `colours` changed so). The parts whose meshes change, by name, and their UVs.
        /// </summary>
        static bool told;

        static Dictionary<string, Vector2[]> Split(Dictionary<string, SkinnedMeshRenderer> parts, Dictionary<string, int[]> of, int[,] count, int[] cells, Color32[] colours, int[] kinds)
        {
            var moved = new Dictionary<string, Vector2[]>();
            if (kinds == null || kinds.Length < 256 || colours == null || colours.Length < 256) return moved;
            var used = new bool[256];
            foreach (var r in parts.Values)
                if (r.sharedMesh != null)
                    foreach (var uv in r.sharedMesh.uv) used[SoldierPalette.CellOf(uv)] = true;
            var most = new int[256];
            for (int c = 0; c < 256; c++)
                for (int part = 1; part < 5; part++) if (count[c, part] > count[c, most[c]]) most[c] = part;
            var free = new Dictionary<(int cell, int part), int>();
            foreach (var (name, piece) in of)
            {
                if (!parts.TryGetValue(name, out var r) || r.sharedMesh == null) continue;
                var uvs = r.sharedMesh.uv;
                // Each corner's move (its cell and piece), kept only where every face it is on moves with it whole (one
                // corner of a face moved and the others not, the face ran through every cell between).
                var group = new int[uvs.Length];
                for (int v = 0; v < uvs.Length; v++)
                {
                    int p = v < piece.Length ? piece[v] : -1, c = SoldierPalette.CellOf(uvs[v]);
                    group[v] = p < 0 || p == most[c] || SoldierPalette.IsBody(c) ? -1 : c * 8 + p;
                }
                var whole = group.Select(g => g >= 0).ToArray();
                var mesh = r.sharedMesh;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var tris = mesh.GetTriangles(sub);
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        int a = tris[t], b = tris[t + 1], d = tris[t + 2];
                        if (group[a] >= 0 && group[a] == group[b] && group[a] == group[d]) continue;
                        whole[a] = whole[b] = whole[d] = false;
                    }
                }
                bool any = false;
                for (int v = 0; v < uvs.Length && v < piece.Length; v++)
                {
                    if (!whole[v]) continue;
                    int p = piece[v], c = SoldierPalette.CellOf(uvs[v]);
                    if (!free.TryGetValue((c, p), out int f))
                    {
                        f = -1;
                        for (int k = 0; k < 256 && f < 0; k++) if (!used[k] && !SoldierPalette.IsBody(k) && kinds[k] == kinds[c]) f = k;
                        if (f >= 0)
                        {
                            used[f] = true;
                            colours[f] = colours[c];
                            cells[f] = p > 0 ? 1 << p : 0;
                        }
                        free[(c, p)] = f;
                    }
                    if (f < 0) continue;
                    // (Where in the cell it was, there in the other: Sidekick's folds' shade is a half of each.)
                    uvs[v] += new Vector2((f % 16 - c % 16) / 16f, -(f / 16 - c / 16) / 16f);
                    any = true;
                }
                if (any) moved[name] = uvs;
            }
            int split = free.Values.Count(f => f >= 0), left = free.Values.Count(f => f < 0);
            if (left > 0 && !told)
            {
                told = true;
                Debug.Log($"[figures] {left} of {split + left} shared cells left whole (no free cell of their kind): by the cell, as the most of their corners");
            }
            return moved;
        }

        /// <summary>Whether a Sidekick bone is one of the game's skeleton (convert.py BONES), not one folded into its parent.</summary>
        static bool Kept(string bone) => kept.IsMatch(bone);

        static readonly System.Text.RegularExpressions.Regex kept = new System.Text.RegularExpressions.Regex(
            @"^(pelvis|hips|spine_0[123]|neck_01|neck|head|clavicle_[lr]|upperarm_[lr]|shoulder_[lr]|lowerarm_[lr]|elbow_[lr]|hand_[lr]|thigh_[lr]|upperleg_[lr]|calf_[lr]|lowerleg_[lr]|foot_[lr]|ankle_[lr]|ball_[lr]|(thumb|index|middle|indexfinger|finger)_0\d_[lr])$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>convert.py PART by a Sidekick bone: 2 the torso, 3 the arms, 4 the legs; 0 the rest (head, hands, feet).</summary>
        static int PartOf(string bone)
        {
            if (bone.StartsWith("spine") || bone.StartsWith("clavicle")) return 2;
            if (bone.StartsWith("upperarm") || bone.StartsWith("lowerarm")) return 3;
            if (bone.StartsWith("pelvis") || bone.StartsWith("thigh") || bone.StartsWith("calf")) return 4;
            return 0;
        }

        /// <summary>
        /// Each renderer's colour map on itself (a MaterialPropertyBlock): with URP's SRP Batcher two Sidekick characters
        /// drawn one after another came out both in one's colours (docs/UNITY-ARTE.md §8).
        /// </summary>
        public static void OwnColours(GameObject figure)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var map = renderer.sharedMaterial != null && renderer.sharedMaterial.HasTexture("_ColorMap") ? renderer.sharedMaterial.GetTexture("_ColorMap") : null;
                if (map == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetTexture("_ColorMap", map);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
