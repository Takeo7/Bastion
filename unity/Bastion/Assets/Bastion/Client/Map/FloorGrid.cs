using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The tiles marked on the floor (Arturo's cuadro 154 of 25/11: «un grid en el suelo para ver las casillas»; UI's G and
    /// its option in Ajustes set Shown): a thin line along every edge of the tiles one can walk, on the storey in view
    /// (the level the camera looks down on at each tile, as the fog's: Los.FloorLevels), one mesh, unlit, a hair over the
    /// floor, under the marks (the reach, the paths, the rings) and under the fog, which dims it where nothing is seen.
    /// Each line a pale middle between two dark edges, so that it reads on the dark road, at night and on the snow.
    /// Made again when the map changes (a wall down, a door open) or the storey in view does.
    /// </summary>
    public static class FloorGrid
    {
        // (BASTION_REJILLA=1: on from the start, for the labs' pictures; the game's own is UI's preference.)
        static bool shown = System.Environment.GetEnvironmentVariable("BASTION_REJILLA") == "1";

        /// <summary>Whether the grid is on: on the map that stands, and on those built after.</summary>
        public static bool Shown
        {
            get => shown;
            set
            {
                shown = value;
                if (grid != null) grid.SetActive(shown);
            }
        }

        /// <summary>How wide a line is (m), how far over the floor (m: over the soot and the embers, 0.02 and 0.03), and how strong.</summary>
        const float Width = 0.085f, Lift = 0.04f;
        static readonly Color Tone = new Color(1, 1, 1, 0.36f), NightTone = new Color(0.75f, 0.8f, 0.95f, 0.17f),
            SnowTone = new Color(0.07f, 0.11f, 0.26f, 0.6f);
        /// <summary>Drawn before the marks (3000 on) and after the soot (2990).</summary>
        const int Queue = 2994;

        static GameObject grid;
        static Mesh mesh;
        static Material material;
        static GameState drawn;
        static int drawnLevel = -1;
        static MapView drawnOn;

        /// <summary>MapView's, every frame: the grid kept to the map that stands and the storey in view, while it is on.</summary>
        internal static void Keep(MapView view)
        {
            if (!shown || view == null || view.State == null)
            {
                if (grid != null && grid.activeSelf && !shown) grid.SetActive(false);
                return;
            }
            if (grid != null && drawnOn == view && ReferenceEquals(drawn, view.State) && drawnLevel == view.Level)
            {
                if (!grid.activeSelf) grid.SetActive(true);
                return;
            }
            Draw(view);
        }

        static void Draw(MapView view)
        {
            var s = view.State;
            if (material == null)
            {
                material = KitMaterials.New(KitMaterials.SeeThrough, "Rejilla del suelo");
                if (material == null) return;
                material.SetColor("_BaseColor", Tone);
                material.mainTexture = Profile();
                material.renderQueue = Queue;
            }
            if (grid == null || drawnOn != view)
            {
                if (grid != null) Object.Destroy(grid);
                grid = new GameObject("Rejilla");
                grid.transform.SetParent(view.transform, false);
                mesh = new Mesh { name = "Rejilla", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                grid.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = grid.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // (Unlit, it glowed in a night's dark: fainter there.)
            // (And dark on the snow, by day and by night: pale on pale it was hardly seen, Pruebas' pass of the 0.14.6.)
            bool snow = view.GetComponentInChildren<SnowCover>() != null || s.Weather == Weathers.Snowfall || s.Weather == Weathers.Blizzard;
            material.SetColor("_BaseColor", snow ? SnowTone : s.Mission?.Night == true ? NightTone : Tone);
            (drawn, drawnLevel, drawnOn) = (s, view.Level, view);
            int floor = view.Level >= view.Levels ? 0 : view.Level;
            var levels = Los.FloorLevels(s, floor);
            int w = s.Width, h = s.Height;
            // Each tile's height where it is walked (null: not walked on the storey in view).
            var height = new float?[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Pos { X = x, Y = y, Z = levels[y * w + x] };
                    if (Bastion.Engine.Grid.IsWalkable(Bastion.Engine.Grid.KindAt(s, p))) height[y * w + x] = MapSpace.StandAt(s, p).y;
                }
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            // A tile's corner in the world: MapSpace's own, by two of its tiles' middles.
            var origin = MapSpace.TileToWorld(s, new Pos { X = 0, Y = 0, Z = 0 });
            var east = MapSpace.TileToWorld(s, new Pos { X = 1, Y = 0, Z = 0 }) - origin;
            var south = MapSpace.TileToWorld(s, new Pos { X = 0, Y = 1, Z = 0 }) - origin;
            Vector3 Corner(int x, int y, float at) { var c = origin + east * (x - 0.5f) + south * (y - 0.5f); c.y = at + Lift; return c; }
            void Line(Vector3 a, Vector3 b)
            {
                var across = Vector3.Cross(Vector3.up, (b - a).normalized) * (Width / 2);
                int i = vertices.Count;
                vertices.Add(a - across); vertices.Add(a + across); vertices.Add(b + across); vertices.Add(b - across);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            float? At(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? null : height[y * w + x];
            for (int y = 0; y <= h; y++)
                for (int x = 0; x <= w; x++)
                {
                    float? here = At(x, y), west = At(x - 1, y), north = At(x, y - 1);
                    // The edge between this tile and the one to its west, and the one to its north: once each, where either is walked.
                    if (here != null || west != null)
                    {
                        float at = Mathf.Max(here ?? float.MinValue, west ?? float.MinValue);
                        Line(Corner(x, y, at), Corner(x, y + 1, at));
                    }
                    if (here != null || north != null)
                    {
                        float at = Mathf.Max(here ?? float.MinValue, north ?? float.MinValue);
                        Line(Corner(x, y, at), Corner(x + 1, y, at));
                    }
                }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            grid.SetActive(true);
            if (System.Environment.GetEnvironmentVariable("BASTION_REJILLA") == "1") Debug.Log($"[rejilla] {vertices.Count} vertices, {triangles.Count / 3} triangles, one mesh, storey {floor}");
        }

        /// <summary>A line's profile across it (alpha and tone): dark, pale, dark, soft at its edges.</summary>
        static Texture2D Profile()
        {
            const int n = 16;
            var t = new Texture2D(n, 1, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Perfil de la rejilla" };
            var pixels = new Color32[n];
            for (int x = 0; x < n; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / n - 0.5f) * 2;          // 0 its middle, 1 its edge
                bool pale = u < 0.38f;
                float a = pale ? 0.62f : Mathf.Clamp01((1 - u) / 0.3f) * 0.75f;
                byte v = (byte)(pale ? 235 : 14);
                pixels[x] = new Color32(v, v, pale ? (byte)225 : (byte)26, (byte)(255 * a));
            }
            t.SetPixels32(pixels);
            t.Apply(true);
            return t;
        }
    }
}
