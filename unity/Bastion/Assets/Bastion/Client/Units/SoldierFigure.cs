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
            // (Asked for: kept as the others, till the scene goes.)
            unasked.Remove(key);
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
            var wanted = dresses.ToList();
            // What was made in the background before and no one asked for, let go now unless wanted again (Forget).
            var keys = new HashSet<string>(wanted.Select(d => KeyOf(d, separate)));
            foreach (string key in unasked.Keys.Where(k => !keys.Contains(k)).ToList()) Forget(key);
            foreach (var d in wanted)
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

        /// <summary>
        /// The figures made in the background that no one has asked for yet (Make), with what each made of its own: its
        /// meshes, its material and its palette (Owned). Personalizar made all the outfits again at each head or build,
        /// 4.7 MB a figure, kept till the scene went: 110 MB a change, 870 MB after eight (the review of 10/11, row 4;
        /// CombatLab.MemoriaPersonalizar). Those not wanted again are let go at the next Prepare, those left as the scene
        /// goes then (its own unload keeps what a field holds).
        /// </summary>
        static readonly Dictionary<string, UnityEngine.Object[]> unasked = new Dictionary<string, UnityEngine.Object[]>();
        /// <summary>What the figure Building gave last made of its own (Owned), for the Runner as it takes it.</summary>
        static UnityEngine.Object[] builtOwn;

        /// <summary>A figure no one has asked for let go, and what it made of its own: none of it was copied.</summary>
        static void Forget(string key)
        {
            if (unasked.TryGetValue(key, out var own))
                foreach (var o in own)
                    if (o != null) DestroyNow(o);
            unasked.Remove(key);
            if (made.TryGetValue(key, out var model) && model != null) DestroyNow(model);
            made.Remove(key);
        }

        /// <summary>
        /// What `model` made of its own: its meshes, materials and their colour maps, made as it was (a negative instance
        /// id), not the parts' (`parts`) nor any other asset's.
        /// </summary>
        static UnityEngine.Object[] Owned(GameObject model, IEnumerable<Mesh> parts)
        {
            var shared = new HashSet<Mesh>(parts);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var meshes = renderers.OfType<SkinnedMeshRenderer>().Select(r => r.sharedMesh).Concat(model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh))
                .Where(m => m != null && !shared.Contains(m)).Cast<UnityEngine.Object>();
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).ToList();
            var maps = materials.Where(m => m.HasTexture("_ColorMap")).Select(m => m.GetTexture("_ColorMap")).Where(t => t != null).Cast<UnityEngine.Object>();
            return meshes.Concat(materials).Concat(maps).Where(o => o.GetInstanceID() < 0).Distinct().ToArray();
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        /// <summary>A step of the background's next figure a frame.</summary>
        sealed class Runner : MonoBehaviour
        {
            void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneUnloaded += Gone;

            void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= Gone;

            /// <summary>The scene gone with the figures (the shelf is its): those no one asked for, let go with what they made.</summary>
            static void Gone(UnityEngine.SceneManagement.Scene scene)
            {
                foreach (string key in unasked.Keys.ToList()) Forget(key);
            }

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
                        if (job.steps.Current != null)
                        {
                            made[job.key] = job.steps.Current;
                            unasked[job.key] = builtOwn;
                            builtOwn = null;
                        }
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
            // (What on the head the wardrobe hasn't, as the engine's newer heads before their pieces, left off: bare-headed.)
            var pieces = dress.Pieces().Select(p => (p.piece, art: p.piece == "headwear" ? Wardrobe.Find(p.piece, p.id) : Wardrobe.Piece(p.piece, p.id))).Where(p => p.art != null).ToList();
            var parts = new Dictionary<string, SkinnedMeshRenderer>();
            // (Under the gas mask, the face's nose MaskNose.)
            bool masked = dress.headwear == "mask";
            foreach (string name in pieces.SelectMany(p => masked && p.piece == "face" ? p.art.parts.Select(n => n.Contains("_35NOSE_") ? MaskNose : n) : p.art.parts).Distinct())
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
            // How far a rucksack (a part on the back, Sidekick's AttachmentBack) stands out behind the upper chest, at rest, where
            // the slung gun lies across it (Stows: from the upper chest, 2 cm up, along (0.45, 0.9); within PackReach of that
            // line): the gun goes over it (SoldierMarks.pack). Its farthest point (a flask, a rolled mat) would leave the gun in
            // the air.
            float pack = 0;
            var line = new Vector2(0.45f, 0.9f).normalized;
            foreach (var kv in parts)
            {
                if (!kv.Key.Contains("_24ABAC_") || kv.Value.sharedMesh == null) continue;
                var chest = kv.Value.bones.FirstOrDefault(b => b != null && b.name == "spine_03");
                if (chest == null) continue;
                var at = chest.position + Vector3.up * 0.02f;
                foreach (var p in kv.Value.sharedMesh.vertices)
                {
                    var w = kv.Value.transform.TransformPoint(p);
                    float off = Mathf.Abs((w.x - at.x) * line.y - (w.y - at.y) * line.x);
                    if (off <= PackReach) pack = Mathf.Max(pack, PackBehind * (chest.position.z - w.z));
                }
            }
            yield return null;
            // The hair under a hat fitted to it (FitUnder), a torso's own hood flattened under a hood on the head (Flatten):
            // those parts' corners moved, in a copy of their meshes as Split's UVs. Worked out once a session for each pair.
            var reshaped = new Dictionary<string, (Vector3[] corners, Vector3[] normals)>();
            var hatParts = pieces.Where(p => p.piece == "headwear").SelectMany(p => p.art.parts).Where(parts.ContainsKey).ToList();
            if (dress.headwear != null && Fitted.Contains(dress.headwear) && hatParts.Count > 0)
                foreach (string name in pieces.Where(p => p.piece == "hair").SelectMany(p => p.art.parts).Where(parts.ContainsKey).ToList())
                {
                    string key = string.Join("+", hatParts) + "|" + name;
                    if (!fitted.TryGetValue(key, out var corners))
                    {
                        fitted[key] = corners = FitUnder(parts[name], hatParts.Select(n => parts[n]));
                        yield return null;
                    }
                    if (corners != null) reshaped[name] = (corners, null);
                }
            if (dress.headwear != null && Hoods.Contains(dress.headwear))
                foreach (string name in parts.Keys.Where(HoodedTorsos.Contains).ToList())
                {
                    if (!flattened.TryGetValue(name, out var flat))
                    {
                        flattened[name] = flat = Flatten(parts[name]);
                        yield return null;
                    }
                    if (flat.corners != null) reshaped[name] = flat;
                }
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
            // (A part split or reshaped, from a copy of its model with its mesh's UVs or corners moved: the kit's own stay as
            // they are.)
            var temporary = new List<UnityEngine.Object>();
            var used = parts.Select(kv =>
            {
                bool moved = split.TryGetValue(kv.Key, out var uvs), shaped = reshaped.TryGetValue(kv.Key, out var shape);
                if (!moved && !shaped) return kv.Value;
                var holder = UnityEngine.Object.Instantiate(kv.Value.transform.root.gameObject);
                holder.hideFlags = HideFlags.HideAndDontSave;
                var r = holder.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x => x.name == kv.Value.name);
                temporary.Add(holder);
                if (r == null) return kv.Value;
                var mesh = UnityEngine.Object.Instantiate(kv.Value.sharedMesh);
                mesh.name = kv.Value.sharedMesh.name;
                if (moved) mesh.uv = uvs;
                if (shaped)
                {
                    mesh.vertices = shape.corners;
                    if (shape.normals != null) mesh.normals = shape.normals;
                    mesh.RecalculateBounds();
                }
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
            // Sidekick's police elbow pads (31AEBL, 32AEBR) lie on the upper arm's end but ride the forearm's elbowAttach: as the
            // arm bent they swung 15–20 cm off the elbow (the 3D review of 10/11, F-09). Hung from the upper arm, they stay on
            // it; where they are at rest doesn't change.
            var bones = model.GetComponentsInChildren<Transform>(true);
            foreach (string side in new[] { "_l", "_r" })
            {
                var pad = bones.FirstOrDefault(t => t.name == "elbowAttach" + side);
                var upper = bones.FirstOrDefault(t => t.name == "upperarm" + side);
                if (pad != null && upper != null) pad.SetParent(upper, true);
            }
            ShutJaw(model);
            OwnColours(model);
            var kept = model.AddComponent<SoldierMarks>();
            kept.cells = marks.cells;
            kept.palette = cells;
            kept.bandLeft = marks.left;
            kept.bandRight = marks.right;
            kept.pack = pack;
            kept.holster = parts.Keys.Any(HolsterParts.Contains);
            if (shelf == null)
            {
                shelf = new GameObject("Soldados hechos").transform;
                shelf.gameObject.hideFlags = HideFlags.HideInHierarchy;
            }
            model.transform.SetParent(shelf, false);
            model.SetActive(false);
            builtOwn = Owned(model, parts.Values.Select(r => r.sharedMesh));
            yield return model;
            builtOwn = null;
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

        /// <summary>
        /// The trousers with a holster and its pistol on the right thigh (the five of police, survivor3's and survivor8's:
        /// seen in their parts' photos, Biblioteca): ours isn't drawn there (SoldierMarks.holster; the 3D review's F-08, 10/11,
        /// Arturo's A). The parts stay: they are the trousers.
        /// </summary>
        static readonly HashSet<string> HolsterParts = new HashSet<string> { "SK_MDRN_POLC_03_17HIPS_HU01", "SK_APOC_SURV_05_17HIPS_HU01" };

        /// <summary>
        /// Under the gas mask, the nose Synty gives it (Apocalypse Survivor 03's preset; face6's): the noses of face2, face3
        /// and face5 showed through the mask (the 3D review of 10/11, F-13). Under it, it isn't seen.
        /// </summary>
        const string MaskNose = "SK_HUMN_BASE_07_35NOSE_HU01";

        /// <summary>
        /// The headwear the hair under it (Dress.HairUnder: the police's, Synty's shortest) is fitted to as the figure is made
        /// (FitUnder): it went through them at the back and the crown (the 3D review of 10/11, F-03; Arturo's B, the hair
        /// that shows below them kept).
        /// </summary>
        static readonly HashSet<string> Fitted = new HashSet<string> { "helmet", "earflapHat", "bandana", "cap", "plainCap" };

        /// <summary>Each hair part's corners fitted under each hat's parts («hat parts|hair part»; null, none moved).</summary>
        static readonly Dictionary<string, Vector3[]> fitted = new Dictionary<string, Vector3[]>();

        /// <summary>
        /// FitUnder's measures (the parts' own units, metres as bound): the point the hat is seen from, over the head's bone;
        /// how far inside the hat the hair is left; its cells of directions round that point, and the points of each of the
        /// hat's triangles (36).
        /// </summary>
        const float FitAbove = 0.06f, FitGap = 0.004f;
        const int FitRound = 64, FitUp = 32, FitSamples = 7;

        /// <summary>
        /// `hair`'s corners drawn in under `hats` (in its mesh's space; null if none had to move): from 6 cm over the head's
        /// bone, the nearest of the hats' surface in each direction (64 × 32 cells, 36 points of each triangle); a corner
        /// further out than that less 4 mm, in a direction a hat covers (its cell; the nearest of it and the 8 round it),
        /// moved in along its ray to there. What hangs below them (no cell of theirs) is left as it is.
        /// </summary>
        static Vector3[] FitUnder(SkinnedMeshRenderer hair, IEnumerable<SkinnedMeshRenderer> hats)
        {
            var mesh = hair.sharedMesh;
            var head = hair.bones.FirstOrDefault(b => b != null && b.name == "head");
            if (mesh == null || head == null) return null;
            var into = hair.transform.worldToLocalMatrix;
            var centre = into.MultiplyPoint3x4(head.position + Vector3.up * FitAbove);
            var nearest = new float[FitRound * FitUp];
            for (int k = 0; k < nearest.Length; k++) nearest[k] = float.MaxValue;
            int Cell(Vector3 p, out float r)
            {
                var d = p - centre;
                r = d.magnitude;
                float round = Mathf.Atan2(d.x, d.z), up = Mathf.Asin(Mathf.Clamp(d.y / Mathf.Max(r, 1e-6f), -1, 1));
                int i = (int)((round + Mathf.PI) / (2 * Mathf.PI) * FitRound) % FitRound;
                int j = Mathf.Clamp((int)((up + Mathf.PI / 2) / Mathf.PI * FitUp), 0, FitUp - 1);
                return j * FitRound + i;
            }
            foreach (var hat in hats)
            {
                if (hat.sharedMesh == null) continue;
                var to = into * hat.transform.localToWorldMatrix;
                var corners = hat.sharedMesh.vertices;
                var triangles = hat.sharedMesh.triangles;
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 a = to.MultiplyPoint3x4(corners[triangles[t]]), b = to.MultiplyPoint3x4(corners[triangles[t + 1]]), c = to.MultiplyPoint3x4(corners[triangles[t + 2]]);
                    for (int u = 0; u <= FitSamples; u++)
                        for (int w = 0; u + w <= FitSamples; w++)
                        {
                            float s = u / (float)FitSamples, q = w / (float)FitSamples;
                            int cell = Cell(a + (b - a) * s + (c - a) * q, out float r);
                            if (r < nearest[cell]) nearest[cell] = r;
                        }
                }
            }
            var fit = mesh.vertices;
            bool any = false;
            for (int v = 0; v < fit.Length; v++)
            {
                int cell = Cell(fit[v], out float r);
                if (nearest[cell] == float.MaxValue || r < 1e-6f) continue;
                int i = cell % FitRound, j = cell / FitRound;
                float inside = float.MaxValue;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                        if (j + dj >= 0 && j + dj < FitUp) inside = Mathf.Min(inside, nearest[(j + dj) * FitRound + (i + di + FitRound) % FitRound]);
                float reach = inside - FitGap;
                if (r <= reach) continue;
                fit[v] = centre + (fit[v] - centre) * (Mathf.Max(reach, 0) / r);
                any = true;
            }
            return any ? fit : null;
        }

        /// <summary>
        /// The torsos with a hood of their own down the back (Sidekick sews it into the torso, with no back under it:
        /// survivor2's, survivor4's and civilian3's, survivor6's and survivor11's, survivor7's, civilian6's), and the heads
        /// with a hood (Hoods): with one of those on, the torso's showed hanging below its rim (the 3D review of 10/11, F-05;
        /// Arturo's A, flattened as the figure is made).
        /// </summary>
        static readonly HashSet<string> HoodedTorsos = new HashSet<string>
        {
            "SK_APOC_SURV_06_10TORS_HU01", "SK_APOC_SURV_04_10TORS_HU01", "SK_APOC_SURV_03_10TORS_HU01", "SK_MDRN_CIVL_07_10TORS_HU01", "SK_MDRN_CIVL_15_10TORS_HU01",
        };
        static readonly HashSet<string> Hoods = new HashSet<string> { "hood", "scarfHood", "twoToneCap" };

        /// <summary>Each hooded torso's corners and normals with its hood flattened (null if none had to move).</summary>
        static readonly Dictionary<string, (Vector3[] corners, Vector3[] normals)> flattened = new Dictionary<string, (Vector3[], Vector3[])>();

        /// <summary>
        /// Flatten's measures (the torso's mesh as bound: metres, y up, facing +z; CombatLab.Piezas): the band its back is
        /// taken from, under every hood; from where up a hood is flattened, and how wide (whole to HoodMiddle, none from
        /// HoodWide); how far behind the back it is left, and how deep a hood is squeezed into that; from where up the back
        /// bends in to the neck, and how fast (m per m²; Survivor 01's torso, without a hood, bends so).
        /// </summary>
        const float BackLow = 1.20f, BackHigh = 1.28f, HoodFrom = 1.30f, HoodMiddle = 0.10f, HoodWide = 0.16f, HoodGap = 0.01f, HoodSqueeze = 0.10f,
            NeckFrom = 1.40f, NeckBend = 4.7f;

        /// <summary>
        /// `torso`'s corners (its mesh's own space) with the hood down its back flattened onto it: its back is the furthest
        /// back of the torso between 1.20 and 1.28 m, by columns 2 cm wide; above 1.30 m and within 16 cm of the middle,
        /// what stands further back than 1 cm behind that (bent in towards the neck from 1.40 m) is squeezed into the next
        /// centimetre, in its order, so the hood lies on the back as cloth; above the head's hood's rim, under it. Their
        /// normals turned as much to the back's (BackFacing): left as the hood's, its shading drew it still. Nulls if
        /// nothing had to move.
        /// </summary>
        static (Vector3[] corners, Vector3[] normals) Flatten(SkinnedMeshRenderer torso)
        {
            var mesh = torso.sharedMesh;
            if (mesh == null) return (null, null);
            var corners = mesh.vertices;
            var normals = mesh.normals;
            var back = new Dictionary<int, float>();
            foreach (var p in corners)
            {
                if (p.y < BackLow || p.y > BackHigh || p.z >= 0) continue;
                int column = Mathf.RoundToInt(p.x / 0.02f);
                back[column] = Mathf.Max(back.TryGetValue(column, out float b) ? b : 0, -p.z);
            }
            if (back.Count == 0) return (null, null);
            float Back(float x)
            {
                int column = Mathf.RoundToInt(x / 0.02f);
                return back.TryGetValue(column, out float b) ? b : back[back.Keys.OrderBy(k => Mathf.Abs(k - column)).First()];
            }
            bool any = false;
            for (int v = 0; v < corners.Length; v++)
            {
                var p = corners[v];
                float side = Mathf.Abs(p.x), deep = -p.z;
                if (p.y < HoodFrom || p.z >= 0 || side >= HoodWide) continue;
                float bend = Mathf.Max(0, p.y - NeckFrom);
                float to = Back(p.x) + HoodGap - NeckBend * bend * bend;
                if (deep <= to) continue;
                float squeezed = Mathf.Min(1, (deep - to) / HoodSqueeze);
                float flat = to + HoodGap * squeezed;
                float share = side <= HoodMiddle ? 1 : (HoodWide - side) / (HoodWide - HoodMiddle);
                corners[v].z = -(deep + (flat - deep) * share);
                if (v < normals.Length) normals[v] = Vector3.Lerp(normals[v], BackFacing, share * squeezed).normalized;
                any = true;
            }
            return any ? (corners, normals) : (null, null);
        }

        /// <summary>The upper back's normal, the way a flattened hood is turned: back and a little up (its mesh's space).</summary>
        static readonly Vector3 BackFacing = new Vector3(0, 0.3f, -1).normalized;

        /// <summary>
        /// Each avatar Sidekick gives its figures (its base model's: the same for all) without the jaw (the 3D review of 10/11,
        /// F-17): Mixamo's clips, the gun's and the pistol's rests, aims and reloads, hold «Jaw Close» at 0, the mouth open on
        /// the head's purple hollow (the faces have no teeth); Synty's hold it at 1.03 (CombatLab.Mandibula). Without it no clip
        /// moves the jaw: it stays as bound, the mouth shut. Null, one that couldn't be made (the figure keeps its own).
        /// </summary>
        static readonly Dictionary<Avatar, Avatar> jawless = new Dictionary<Avatar, Avatar>();

        /// <summary>The figure's avatar the same without its jaw (jawless), its animator bound again.</summary>
        static void ShutJaw(GameObject model)
        {
            var animator = model.GetComponent<Animator>();
            var avatar = animator != null ? animator.avatar : null;
            if (avatar == null || !avatar.isHuman) return;
            if (!jawless.TryGetValue(avatar, out var shut))
            {
                var description = avatar.humanDescription;
                if (description.human != null && description.human.Any(h => h.humanName == "Jaw"))
                {
                    description.human = description.human.Where(h => h.humanName != "Jaw").ToArray();
                    // (Built on this figure under the root's name the avatar knows: its bones are the base model's.)
                    string name = model.name;
                    if (description.skeleton != null && description.skeleton.Length > 0) model.name = description.skeleton[0].name;
                    shut = AvatarBuilder.BuildHumanAvatar(model, description);
                    model.name = name;
                    if (shut != null && shut.isValid && shut.isHuman) shut.name = avatar.name + " (jawless)";
                    else
                    {
                        Debug.LogWarning($"[figures] {avatar.name} without its jaw: not made, the jaw left to the clips");
                        shut = null;
                    }
                }
                jawless[avatar] = shut;
            }
            if (shut == null) return;
            animator.avatar = shut;
            animator.Rebind();
        }

        /// <summary>Which way is behind a Sidekick part at rest, along its z (its figures face +z).</summary>
        const float PackBehind = 1;

        /// <summary>How far to either side of the slung gun's line a rucksack is measured (model units): the gun's width and some.</summary>
        const float PackReach = 0.08f;

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
