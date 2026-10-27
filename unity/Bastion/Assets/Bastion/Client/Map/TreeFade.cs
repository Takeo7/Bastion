using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The trees' crowns fade where they would hide what the player looks at (treeFade.ts and mapView.ts fadeCrowns; UI's,
    /// Arte's review of 27/10: on the country and road maps the soldiers went under them), as a building's roof does: over
    /// the selected soldier, the target, the tile under the cursor, or between any of them and the camera; the trunk stays.
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

        /// <summary>How often the map is looked over again for its trees (a rebuild, one felled), seconds.</summary>
        const float Rescan = 2f;

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
        }

        readonly List<Tree> trees = new List<Tree>();
        MaterialPropertyBlock block;
        static readonly Dictionary<Material, Material> crowns = new Dictionary<Material, Material>();
        static readonly int FadeId = Shader.PropertyToID("_Fade"), TrunkTopId = Shader.PropertyToID("_TrunkTop");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), BaseColorId = Shader.PropertyToID("_BaseColor"), CutoffId = Shader.PropertyToID("_Cutoff");
        static Shader shader;
        float scanned = -Rescan;

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
            if (Time.unscaledTime - scanned >= Rescan || trees.Exists(t => t.Holder == null)) Scan();
            if (trees.Count == 0) return;
            var points = Looked();
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
            foreach (var tree in trees)
            {
                var fade = 0f;
                foreach (var f in points)
                {
                    var from = new Vector2(f.x, f.y);
                    var t = Mathf.Clamp01(Vector2.Dot(tree.At - from, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    var d = Vector2.Distance(tree.At, from + ab * t);
                    fade = Mathf.Max(fade, 1 - Smooth(Near, Far, d));
                }
                fade *= Most;
                if (Mathf.Abs(fade - tree.Fade) < 0.005f && (fade > 0) == (tree.Fade > 0)) continue;
                Show(tree, fade);
            }
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

        /// <summary>The points looked at (tiles, and their height in tiles): the selected soldier, the target where their bodies stand, the tile under the cursor.</summary>
        List<Vector3> Looked()
        {
            var points = new List<Vector3>(3);
            var cutaway = Cutaway.Current;
            var units = UnitViews.Current;
            if (cutaway == null) return points;
            var focus = cutaway.Focus;
            foreach (var id in new[] { focus.selected, focus.target })
            {
                if (id == null || units == null || !units.Has(id)) continue;
                var feet = units.Feet(id);
                points.Add(new Vector3(feet.x / MapSpace.Tile, -feet.z / MapSpace.Tile, feet.y / MapSpace.Tile));
            }
            if (focus.hover != null) points.Add(new Vector3(focus.hover.X + 0.5f, focus.hover.Y + 0.5f, focus.hover.Z * MapSpace.Storey / MapSpace.Tile));
            return points;
        }

        /// <summary>The map's trees found again (a rebuild makes them anew), each as it stands now.</summary>
        void Scan()
        {
            scanned = Time.unscaledTime;
            foreach (var tree in trees)
                if (tree.Holder != null && tree.Fade > 0) Show(tree, 0);
            trees.Clear();
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
            var texture = own.HasProperty("_Main_Texture") ? own.GetTexture("_Main_Texture") : own.mainTexture;
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
