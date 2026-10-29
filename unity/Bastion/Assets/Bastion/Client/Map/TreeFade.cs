using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The trees' crowns fade where they would hide what the player looks at (treeFade.ts and mapView.ts fadeCrowns; UI's,
    /// Arte's review of 27/10: on the country and road maps the soldiers went under them), as a building's roof does: over
    /// the selected soldier, the target, the tile under the cursor, or between any of them and the camera; the trunk stays.
    /// Over the enemies in sight too, which the 0.10.1 didn't do (Gameplay, 28/10: the siege's dead, coming in by the paths
    /// between the pines, went unseen under the crowns until they reached the clearing; on the country maps as well).
    /// Each tree (CROWNS, the map's props by their id) gets as much fade as the nearest of those lines brings, 0.88 at most:
    /// while it has some, its materials are Bastion/Crown's (its own texture, dithered above the trunk's top); with none,
    /// its own again, in the wind.
    /// </summary>
    public sealed class TreeFade : MonoBehaviour
    {
        /// <summary>The props with a crown (mapView.ts CROWNS).</summary>
        public static readonly HashSet<string> Crowns = new HashSet<string> { "streetTree", "tree", "treePine", "treeDead", "pineLarge", "pineLarge2", "pineDead" };

        /// <summary>How high a trunk is, in tiles, above which a tree fades: over a soldier's head (treeFade.ts TRUNK).</summary>
        public const float Trunk = 1.5f;

        /// <summary>How far from a line looked along a tree starts and ends fading (tiles), and its most.</summary>
        const float Near = 1.4f, Far = 2.8f, Most = 0.88f;

        /// <summary>
        /// How soon the map is looked over again for its trees once it has changed (a rebuild, one felled: what hangs under
        /// it counts other than it did), seconds; and how often, besides, a tree gone is looked for.
        /// </summary>
        const float Rescan = 0.5f, Lost = 2f;

        /// <summary>The side of the squares the trees are kept in (tiles), so a line looks only at those near it.</summary>
        const float Cell = 4;

        sealed class Tree
        {
            public GameObject Holder;
            public Renderer[] Renderers;
            public Material[][] Own;
            public Material[][] Faded;
            /// <summary>Where it stands (tiles: x east, y south) and its trunk's top (world y).</summary>
            public Vector2 At;
            public float TrunkTop;
            public float Fade;
            /// <summary>The frame some line last came near it (Fade's count), and how much fade the lines bring it then.</summary>
            public int Stamp;
            public float Goal;
        }

        readonly List<Tree> trees = new List<Tree>();
        /// <summary>The trees by their square (Cell), those faded now, those some line comes near this frame.</summary>
        readonly Dictionary<long, List<Tree>> cells = new Dictionary<long, List<Tree>>();
        readonly List<Tree> faded = new List<Tree>(), near = new List<Tree>();
        readonly List<Vector3> points = new List<Vector3>();
        int stamp, hierarchy = -1;
        float lookedForLost;
        MaterialPropertyBlock block;
        static readonly Dictionary<Material, Material> crowns = new Dictionary<Material, Material>();
        static readonly int FadeId = Shader.PropertyToID("_Fade"), TrunkTopId = Shader.PropertyToID("_TrunkTop");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), BaseColorId = Shader.PropertyToID("_BaseColor"), CutoffId = Shader.PropertyToID("_Cutoff");
        static Shader shader;
        float scanned = -Lost;

        /// <summary>The fade on the trees under `root` (the map's), made once a map.</summary>
        public static TreeFade Attach(Transform root)
        {
            var holder = new GameObject("Copas que se apartan");
            holder.transform.SetParent(root, false);
            return holder.AddComponent<TreeFade>();
        }

        void Awake() => block = new MaterialPropertyBlock();

        void LateUpdate()
        {
            if (Measure) clock.Start();
            Fade();
            if (Measure) Report();
        }

        void Fade()
        {
            if (Changed()) Scan();
            if (trees.Count == 0) return;
            Looked();
            var camera = Camera.main;
            var toCam = new Vector2(0, 1);
            var reach = 5f;
            if (points.Count > 0 && camera != null)
            {
                // From the first point looked at towards the camera, on the ground (treeFade.ts aimTreeFade).
                var eye = camera.transform.position;
                var first = points[0];
                var flat = new Vector2(eye.x / MapSpace.Tile - first.x, -eye.z / MapSpace.Tile - first.y);
                if (flat.magnitude > 1e-3f)
                {
                    toCam = flat.normalized;
                    // A crown some 6 tiles up hides the ground this far towards the camera.
                    var tan = (eye.y / MapSpace.Tile - first.z) / flat.magnitude;
                    reach = Mathf.Min(8, 6 / Mathf.Max(tan, 0.4f));
                }
            }
            var ab = toCam * reach;
            var length = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            // Each line looks at the trees of the squares within Far of it; each tree keeps the most fade a line brings it.
            stamp++;
            near.Clear();
            foreach (var f in points)
            {
                var from = new Vector2(f.x, f.y);
                var to = from + ab;
                int x0 = Mathf.FloorToInt((Mathf.Min(from.x, to.x) - Far) / Cell), x1 = Mathf.FloorToInt((Mathf.Max(from.x, to.x) + Far) / Cell);
                int y0 = Mathf.FloorToInt((Mathf.Min(from.y, to.y) - Far) / Cell), y1 = Mathf.FloorToInt((Mathf.Max(from.y, to.y) + Far) / Cell);
                for (var cx = x0; cx <= x1; cx++)
                    for (var cy = y0; cy <= y1; cy++)
                    {
                        if (!cells.TryGetValue(Key(cx, cy), out var list)) continue;
                        foreach (var tree in list)
                        {
                            var t = Mathf.Clamp01(Vector2.Dot(tree.At - from, ab) / length);
                            var d = Vector2.Distance(tree.At, from + ab * t);
                            if (d >= Far) continue;
                            if (tree.Stamp != stamp)
                            {
                                tree.Stamp = stamp;
                                tree.Goal = 0;
                                near.Add(tree);
                            }
                            tree.Goal = Mathf.Max(tree.Goal, 1 - Smooth(Near, Far, d));
                        }
                    }
            }
            // Those faded that no line comes near now come back.
            foreach (var tree in faded)
                if (tree.Stamp != stamp)
                {
                    tree.Stamp = stamp;
                    tree.Goal = 0;
                    near.Add(tree);
                }
            faded.Clear();
            foreach (var tree in near)
            {
                var fade = tree.Goal * Most;
                if (!(Mathf.Abs(fade - tree.Fade) < 0.005f && (fade > 0) == (tree.Fade > 0))) Show(tree, fade);
                if (tree.Fade > 0) faded.Add(tree);
            }
        }

        static long Key(int cx, int cy) => ((long)cx << 32) | (uint)cy;

        /// <summary>
        /// Whether the trees are to be looked for again: what hangs under the map counts other than it did, and the last
        /// look is Rescan old; or, every Lost seconds, one of them is gone.
        /// </summary>
        bool Changed()
        {
            var root = transform.parent;
            if (root == null) return false;
            var count = root.hierarchyCount;
            if (count != hierarchy && Time.unscaledTime - scanned >= Rescan)
            {
                hierarchy = count;
                return true;
            }
            if (Time.unscaledTime - lookedForLost < Lost) return false;
            lookedForLost = Time.unscaledTime;
            return trees.Exists(t => t.Holder == null);
        }

        /// <summary>A tree at `fade`: on Bastion/Crown while it has some, its own materials again with none.</summary>
        void Show(Tree tree, float fade)
        {
            var was = tree.Fade > 0;
            tree.Fade = fade;
            for (int k = 0; k < tree.Renderers.Length; k++)
            {
                var r = tree.Renderers[k];
                if (r == null) continue;
                if (fade <= 0)
                {
                    if (was)
                    {
                        r.sharedMaterials = tree.Own[k];
                        r.SetPropertyBlock(null);
                    }
                    continue;
                }
                if (!was) r.sharedMaterials = tree.Faded[k];
                r.GetPropertyBlock(block);
                block.SetFloat(FadeId, fade);
                block.SetFloat(TrunkTopId, tree.TrunkTop);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// The points looked at (tiles, and their height in tiles): the selected soldier, the target where their bodies
        /// stand, the tile under the cursor; then each living enemy in sight (or heard: a shade), where its figure is.
        /// </summary>
        void Looked()
        {
            points.Clear();
            var cutaway = Cutaway.Current;
            var units = UnitViews.Current;
            string target = null;
            if (cutaway != null)
            {
                var focus = cutaway.Focus;
                target = focus.target;
                Add(units, focus.selected);
                Add(units, focus.target);
                if (focus.hover != null) points.Add(new Vector3(focus.hover.X + 0.5f, focus.hover.Y + 0.5f, focus.hover.Z * MapSpace.Storey / MapSpace.Tile));
            }
            var state = units != null ? units.State : null;
            if (state == null) return;
            foreach (var u in state.Units)
                if (u.Alive && !u.Evacuated && u.Team != Teams.Xcom && u.Id != target && units.Shown(u.Id)) Add(units, u.Id);
        }

        void Add(UnitViews units, string id)
        {
            if (id == null || units == null || !units.Has(id)) return;
            var feet = units.Feet(id);
            points.Add(new Vector3(feet.x / MapSpace.Tile, -feet.z / MapSpace.Tile, feet.y / MapSpace.Tile));
        }

        // ------------------------------------------------------------ its cost

        /// <summary>BASTION_COPAS_MEDIR=1: what this costs a frame, said every 30 frames (with how many points and trees, and its worst frame).</summary>
        static readonly bool Measure = System.Environment.GetEnvironmentVariable("BASTION_COPAS_MEDIR") == "1";
        readonly Stopwatch clock = new Stopwatch();
        int frames, mostPoints, scans;
        double worst, before;

        void Report()
        {
            clock.Stop();
            frames++;
            worst = System.Math.Max(worst, clock.Elapsed.TotalMilliseconds - before);
            before = clock.Elapsed.TotalMilliseconds;
            mostPoints = Mathf.Max(mostPoints, points.Count);
            if (frames >= 30) Said();
        }

        void Said()
        {
            if (frames == 0) return;
            Debug.Log(System.FormattableString.Invariant($"[copas] {clock.Elapsed.TotalMilliseconds / frames:0.000} ms por fotograma en {frames} (el peor {worst:0.000}, {scans} búsquedas); hasta {mostPoints} puntos, {trees.Count} árboles, {faded.Count} apartados ahora"));
            clock.Reset();
            frames = 0;
            scans = 0;
            mostPoints = 0;
            worst = before = 0;
        }

        void OnDisable()
        {
            if (Measure) Said();
        }

        /// <summary>The map's trees found again (a rebuild makes them anew), each as it stands now.</summary>
        void Scan()
        {
            scanned = Time.unscaledTime;
            if (Measure) scans++;
            foreach (var tree in trees)
                if (tree.Holder != null && tree.Fade > 0) Show(tree, 0);
            trees.Clear();
            cells.Clear();
            faded.Clear();
            var root = transform.parent;
            if (root == null) return;
            if (shader == null) shader = Resources.Load<Shader>("Crown");
            if (shader == null)
            {
                Debug.LogError("TreeFade: Resources/Crown.shader missing");
                enabled = false;
                return;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (!Crowns.Contains(t.name)) continue;
                var renderers = t.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var tree = new Tree
                {
                    Holder = t.gameObject,
                    Renderers = renderers,
                    Own = new Material[renderers.Length][],
                    Faded = new Material[renderers.Length][],
                    At = new Vector2(t.position.x / MapSpace.Tile, -t.position.z / MapSpace.Tile),
                    TrunkTop = t.position.y + Trunk * MapSpace.Tile,
                };
                for (int k = 0; k < renderers.Length; k++)
                {
                    tree.Own[k] = renderers[k].sharedMaterials;
                    tree.Faded[k] = System.Array.ConvertAll(tree.Own[k], CrownOf);
                }
                trees.Add(tree);
                var key = Key(Mathf.FloorToInt(tree.At.x / Cell), Mathf.FloorToInt(tree.At.y / Cell));
                if (!cells.TryGetValue(key, out var cell)) cells[key] = cell = new List<Tree>();
                cell.Add(tree);
            }
            if (trees.Count != found) Debug.Log($"[copas] {trees.Count} árboles con copa en el mapa");
            found = trees.Count;
        }

        int found = -1;

        /// <summary>A tree's material on Bastion/Crown (made once): its texture, its tint, cut out as it is.</summary>
        static Material CrownOf(Material own)
        {
            if (own == null) return null;
            if (crowns.TryGetValue(own, out var made) && made != null) return made;
            made = new Material(shader) { name = own.name + " (copa)" };
            // (Nature's and Apocalypse's trees: _Main_Texture; Synty/Generic_Basic, the city's street tree: _Albedo_Map.)
            var texture = own.HasProperty("_Main_Texture") ? own.GetTexture("_Main_Texture") : own.HasProperty("_Albedo_Map") ? own.GetTexture("_Albedo_Map") : own.mainTexture;
            made.SetTexture(BaseMapId, texture);
            var tint = own.HasProperty("_Color_Tint") ? own.GetColor("_Color_Tint") : own.HasProperty("_BaseColor") ? own.GetColor("_BaseColor") : Color.white;
            tint.a = 1;
            made.SetColor(BaseColorId, tint);
            // Nature's trees cut their leaves out by their Mask Clip Value; the others as their own material does.
            var clip = own.HasProperty("_Mask_Clip_Value") ? own.GetFloat("_Mask_Clip_Value") : own.IsKeywordEnabled("_ALPHATEST_ON") && own.HasProperty("_Cutoff") ? own.GetFloat("_Cutoff") : -1;
            if (clip >= 0)
            {
                made.SetFloat(CutoffId, clip);
                made.EnableKeyword("_ALPHATEST_ON");
            }
            else made.DisableKeyword("_ALPHATEST_ON");
            crowns[own] = made;
            return made;
        }

        static float Smooth(float a, float b, float x)
        {
            var t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
