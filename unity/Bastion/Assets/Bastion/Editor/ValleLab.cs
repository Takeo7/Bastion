using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bastion.Client.Map;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The stage map's ground (Arturo, 23/11, cuadros 126 and 129: «el valle de verdad, que se vea»): the valley seen from
    /// straight above, without perspective, to be put under the map's network by UI. Two kinds of picture, at the same
    /// pixels a tile, put together outside (.dev-snapshots/arte/unity/runs/valle/montar.sh):
    ///   Plano: the base in its pinar at the foot of the picture, the wood going on over the whole valley (its glades from a
    ///   noise, as the base's), a river down its west side, the road and its tracks; Wide × Tall tiles, the places left clear.
    ///   Sitio: one generated map alone (BASTION_GENERAR, as MapLab's), BASTION_VALLE_LADO its side in tiles, with Margin round it.
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.ValleLab.Plano -shotCamera "Shot camera" -shotSize 2160x1680
    /// BASTION_NIEVE=1 in winter (the base's own); BASTION_VALLE=x,z,half the camera elsewhere (tiles), a test.
    /// </summary>
    public static class ValleLab
    {
        /// <summary>The picture, in tiles; where the base's middle is in it (from its north-west corner, y south).</summary>
        public const int Wide = 270, Tall = 210, BaseX = 135, BaseY = 185;
        /// <summary>Round a place's map, what its own picture carries (tiles).</summary>
        public const int Margin = 4;
        /// <summary>The places: their maps' north-west corners and sides, in the picture's tiles (montar.sh puts them there).</summary>
        static readonly (int x, int y, int side)[] Places = { (30, 22, 72), (24, 112, 48), (186, 10, 64), (198, 116, 72) };
        /// <summary>
        /// The river, the road and the tracks off it, through these (the picture's tiles). None runs level across the
        /// picture: under the map's rows it would read as one more of its ways (UI, 23/11).
        /// </summary>
        static readonly Vector2[] River = { new Vector2(20, -8), new Vector2(9, 40), new Vector2(18, 92), new Vector2(7, 140), new Vector2(16, 218) };
        static readonly Vector2[] Road = { new Vector2(142, -8), new Vector2(122, 50), new Vector2(150, 100), new Vector2(184, 90), new Vector2(214, 64) };
        static readonly Vector2[][] Tracks =
        {
            new[] { new Vector2(124, 54), new Vector2(112, 60), new Vector2(98, 58) },
            new[] { new Vector2(148, 98), new Vector2(110, 108), new Vector2(66, 124) },
            new[] { new Vector2(152, 101), new Vector2(180, 114), new Vector2(206, 128) },
        };
        const float RiverWide = 8, RoadWide = 3.2f, TrackWide = 1.8f;

        /// <summary>
        /// The valley as numbers, for the drawn map (cuadro 131: the map on paper; .dev-snapshots/arte/unity/runs/valle/papel/
        /// dibujo.swift traces it): the picture's size, the river's, the road's and the tracks' lines, where the wood stands
        /// tile by tile, and each place with its buildings' tiles (what its walls close). To BASTION_VALLE_DATOS, as text.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.ValleLab.Datos -quit
        /// </summary>
        public static void Datos()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"size {Wide} {Tall} base {BaseX} {BaseY}");
            string Line(IEnumerable<Vector2> line) => string.Join(" ", line.Select(p => FormattableString.Invariant($"{p.x:0.00},{p.y:0.00}")));
            var river = Smooth(River);
            var road = Smooth(Road);
            var tracks = Tracks.Select(Smooth).ToList();
            text.AppendLine("river " + Line(river));
            text.AppendLine("road " + Line(road));
            foreach (var t in tracks) text.AppendLine("track " + Line(t));
            float Near(List<Vector2> line, Vector2 p) => line.Min(q => (q - p).magnitude);
            for (int py = 0; py < Tall; py++)
            {
                var row = new char[Wide];
                for (int px = 0; px < Wide; px++)
                {
                    var p = new Vector2(px, py);
                    // (The base's own wood: all of it but its clearing.)
                    bool home = Mathf.Abs(px - BaseX) < 28 + BaseWoods.Reach - 8 && Mathf.Abs(py - BaseY) < 28 + BaseWoods.Reach - 8;
                    bool wooded = home ? (p - new Vector2(BaseX, BaseY)).magnitude > 13 && !(Mathf.Abs(px - BaseX) < 2 && py > BaseY) : Wooded(px, py);
                    if (wooded && (Near(river, p) < RiverWide / 2 + 2.5f || Near(road, p) < RoadWide / 2 + 2 || tracks.Any(t => Near(t, p) < TrackWide / 2 + 1.5f))) wooded = false;
                    row[px] = wooded ? '1' : '0';
                }
                text.AppendLine("w " + new string(row));
            }
            var specs = new[] { ("wilds", 5, (string)null), ("wilds", 3, "camping"), ("city", 7, null), ("city", 4, "industrial") };
            for (int k = 0; k < Places.Length; k++)
            {
                var state = MapLab.Generated(specs[k].Item1, specs[k].Item2, "scavenge", specs[k].Item3);
                int w = state.Width, h = state.Height;
                text.AppendLine($"place {Places[k].x} {Places[k].y} {w} {h} {specs[k].Item1} {specs[k].Item3}");
                // Its buildings: what has a storey's slab or a roof over it (a level's tile), or a pitched roof.
                var open = new bool[w * h];
                for (int i = 0; i < w * h; i++)
                {
                    int x = i % w, y = i / w;
                    bool roofed = state.Levels != null && state.Levels.Any(l => i < l.Tiles.Count && l.Tiles[i] != null)
                        || state.Pitched != null && state.Pitched.Any(r => x >= r.X && x < r.X + r.W && y >= r.Y && y < r.Y + r.H);
                    open[i] = !roofed;
                }
                for (int y = 0; y < h; y++) text.AppendLine("b " + new string(Enumerable.Range(0, w).Select(x => open[y * w + x] ? '0' : '1').ToArray()));
            }
            string path = Environment.GetEnvironmentVariable("BASTION_VALLE_DATOS") ?? "valle.txt";
            System.IO.File.WriteAllText(path, text.ToString());
            Debug.Log($"[valle] data to {path}");
        }

        /// <summary>The wood stands on that tile of the picture: stands and glades of every size, their edges bent so none runs straight; none on a place.</summary>
        static bool Wooded(int px, int py)
        {
            float ragged = 5 * Mathf.PerlinNoise(px / 7f + 90.5f, py / 7f + 13.2f);
            if (Places.Any(s => px >= s.x - ragged && px < s.x + s.side + ragged && py >= s.y - ragged && py < s.y + s.side + ragged)) return false;
            float wx = px + 14 * (Mathf.PerlinNoise(px / 23f + 31.7f, py / 23f + 4.1f) - 0.5f), wy = py + 14 * (Mathf.PerlinNoise(px / 23f + 7.3f, py / 23f + 52.9f) - 0.5f);
            float n = 0.55f * Mathf.PerlinNoise(wx / 44f + 3.3f, wy / 44f + 9.1f) + 0.3f * Mathf.PerlinNoise(wx / 17f + 60.2f, wy / 17f + 18.4f) + 0.15f * Mathf.PerlinNoise(wx / 6f + 11.5f, wy / 6f + 77.7f) + 0.06f;
            return n >= 0.46f;
        }

        public static void Sitio()
        {
            MapLab.Day();
            int side = int.Parse(Environment.GetEnvironmentVariable("BASTION_VALLE_LADO") ?? "64");
            Down(side / 2f, side / 2f, side / 2f + Margin);
        }

        public static void Plano()
        {
            SettlementLab.Clearing();
            var v = Environment.GetEnvironmentVariable("BASTION_VALLE")?.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            int c = 28;
            // The picture's tiles to the base's: its middle (c, c) at (BaseX, BaseY).
            Vector2 ToBase(Vector2 p) => new Vector2(p.x - BaseX + c, p.y - BaseY + c);
            if (v != null) Down(v[0], v[1], v[2]);
            else Down(Wide / 2f - BaseX + c, Tall / 2f - BaseY + c, Tall / 2f);
            // The base's ground, on over the whole valley (one quad, drawn by where it is: Ground.shader).
            var ground = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).FirstOrDefault(f => f.name == "Suelo");
            if (ground != null)
            {
                float far = 400 * MapSpace.Tile;
                var mesh = new Mesh { name = "Suelo del valle" };
                mesh.vertices = new[] { new Vector3(-far, 0, far), new Vector3(far, 0, far), new Vector3(far, 0, -far), new Vector3(-far, 0, -far) };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
                ground.sharedMesh = mesh;
            }
            var root = new GameObject("Valle").transform;
            var river = Smooth(River.Select(ToBase).ToArray());
            var road = Smooth(Road.Select(ToBase).ToArray());
            var tracks = Tracks.Select(t => Smooth(t.Select(ToBase).ToArray())).ToList();
            var shape = Resources.Load<Material>("FrameShape");
            var water = Resources.Load<Material>("FrameWater");
            Ribbon(root, "Orilla", river, RiverWide + 2.4f, 0.02f, Hex(0x6b5a44), shape);
            Ribbon(root, "Río", river, RiverWide, 0.04f, Hex(0x46605a), water != null ? water : shape);
            foreach (var t in tracks) Ribbon(root, "Pista", t, TrackWide, 0.03f, Hex(0x8a6c4e), shape);
            Ribbon(root, "Arcén", road, RoadWide + 0.9f, 0.05f, Hex(0x7c7466), shape);
            Ribbon(root, "Carretera", road, RoadWide, 0.06f, Hex(0x4f4c49), shape);
            // The wood: a pine a tile where the noise says, none where the base's own stands, on a place, the water or a way.
            float Near(List<Vector2> line, Vector2 p) => line.Min(q => (q - p).magnitude);
            // (The pines themselves, from those the base has standing just past its edge: MapBuilder.Beyond's near ones.)
            var wood = GameObject.Find("Bosque");
            var kinds = wood == null ? new List<(Mesh mesh, int sub, Material material, Matrix4x4 local)>[0]
                : wood.transform.Cast<Transform>().Where(t => !t.name.StartsWith("Pinar ")).Take(8).Select(Parts).Where(k => k.Count > 0).ToArray();
            if (kinds.Length == 0) { Debug.Log("[valle] no pines to copy"); return; }
            var lots = new Dictionary<(int, int, Material), List<CombineInstance>>();
            var rng = new System.Random(11);
            int pines = 0;
            int reach = BaseWoods.Reach - 2;
            for (int py = -6; py < Tall + 6; py++)
                for (int px = -6; px < Wide + 6; px++)
                {
                    float jx = (float)rng.NextDouble(), jy = (float)rng.NextDouble(), turn = (float)rng.NextDouble() * 360, size = 0.85f + 0.4f * (float)rng.NextDouble();
                    int kind = rng.Next(kinds.Length);
                    bool thin = rng.NextDouble() < 0.5;
                    var b = ToBase(new Vector2(px, py));
                    float ragged = 5 * Mathf.PerlinNoise(px / 7f + 90.5f, py / 7f + 13.2f);
                    // (Into the base's own wood's rim, so that its end isn't a straight line.)
                    float rim = reach - 3 - 2 * ragged;
                    if (b.x > -rim && b.x < 2 * c + rim && b.y > -rim && b.y < 2 * c + rim) continue;
                    if (Places.Any(s => px >= s.x - ragged && px < s.x + s.side + ragged && py >= s.y - ragged && py < s.y + s.side + ragged)) continue;
                    // (Stands and glades of every size, their edges bent so none runs straight.)
                    float wx = px + 14 * (Mathf.PerlinNoise(px / 23f + 31.7f, py / 23f + 4.1f) - 0.5f), wy = py + 14 * (Mathf.PerlinNoise(px / 23f + 7.3f, py / 23f + 52.9f) - 0.5f);
                    float n = 0.55f * Mathf.PerlinNoise(wx / 44f + 3.3f, wy / 44f + 9.1f) + 0.3f * Mathf.PerlinNoise(wx / 17f + 60.2f, wy / 17f + 18.4f) + 0.15f * Mathf.PerlinNoise(wx / 6f + 11.5f, wy / 6f + 77.7f) + 0.06f;
                    // (Thinner at a stand's edge.)
                    if (n < 0.46f || n < 0.52f && thin) continue;
                    if (Near(river, b) < RiverWide / 2 + 2.5f || Near(road, b) < RoadWide / 2 + 2 || tracks.Any(t => Near(t, b) < TrackWide / 2 + 1.5f)) continue;
                    var at = Matrix4x4.TRS(MapSpace.World(b.x + jx - 0.5f, b.y + jy - 0.5f), Quaternion.Euler(0, turn, 0), Vector3.one * size);
                    foreach (var (mesh, sub, material, local) in kinds[kind])
                    {
                        var key = (px / 32, py / 32, material);
                        if (!lots.TryGetValue(key, out var lot)) lots[key] = lot = new List<CombineInstance>();
                        lot.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = at * local });
                    }
                    pines++;
                }
            foreach (var kv in lots)
            {
                var mesh = new Mesh { name = "Pinar del valle", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var o = new GameObject(mesh.name);
                o.transform.SetParent(root, false);
                o.AddComponent<MeshFilter>().sharedMesh = mesh;
                o.AddComponent<MeshRenderer>().sharedMaterial = kv.Key.Item3;
            }
            Debug.Log($"[valle] {pines} pines in {lots.Count} meshes");
        }

        /// <summary>The shot camera straight down over (x, y) tiles, `half` tiles to the picture's top; no haze (from that high it would be all there is).</summary>
        static void Down(float x, float y, float half)
        {
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(k => k.name == "Shot camera")
                ?? new GameObject("Shot camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = half * MapSpace.Tile;
            camera.transform.position = new Vector3(x * MapSpace.Tile, 120, -y * MapSpace.Tile);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.nearClipPlane = 1;
            camera.farClipPlane = 400;
            RenderSettings.fog = false;
        }

        /// <summary>A placed thing's meshes, each with its material and where it is from the thing's foot.</summary>
        static List<(Mesh mesh, int sub, Material material, Matrix4x4 local)> Parts(Transform thing)
        {
            var parts = new List<(Mesh, int, Material, Matrix4x4)>();
            var from = Matrix4x4.Translate(-thing.position);
            foreach (var r in thing.GetComponentsInChildren<MeshRenderer>())
            {
                var f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null || r.sharedMaterials.Length == 0) continue;
                for (int k = 0; k < f.sharedMesh.subMeshCount; k++) parts.Add((f.sharedMesh, k, r.sharedMaterials[Mathf.Min(k, r.sharedMaterials.Length - 1)], from * r.transform.localToWorldMatrix));
            }
            return parts;
        }

        /// <summary>A strip `wide` tiles wide along `line` (the base's tiles), `up` metres over the ground, of one colour.</summary>
        static void Ribbon(Transform root, string name, List<Vector2> line, float wide, float up, Color colour, Material material)
        {
            var points = new List<Vector3>();
            var triangles = new List<int>();
            for (int k = 0; k < line.Count; k++)
            {
                var along = (line[Mathf.Min(k + 1, line.Count - 1)] - line[Mathf.Max(k - 1, 0)]).normalized;
                var side = new Vector2(-along.y, along.x) * wide / 2;
                foreach (var p in new[] { line[k] - side, line[k] + side }) points.Add(new Vector3(p.x * MapSpace.Tile, up, -p.y * MapSpace.Tile));
                if (k == 0) continue;
                int i = 2 * k;
                triangles.AddRange(new[] { i - 2, i, i - 1, i - 1, i, i + 1, i - 2, i - 1, i, i - 1, i + 1, i });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(points);
            mesh.SetNormals(points.Select(_ => Vector3.up).ToList());
            mesh.SetColors(points.Select(_ => colour.linear).ToList());
            mesh.SetTriangles(triangles, 0);
            var o = new GameObject(name);
            o.transform.SetParent(root, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A curve through `through` (Catmull-Rom), a point every tile or so.</summary>
        static List<Vector2> Smooth(Vector2[] through)
        {
            var line = new List<Vector2>();
            for (int k = 0; k + 1 < through.Length; k++)
            {
                Vector2 a = through[Mathf.Max(k - 1, 0)], b = through[k], c = through[k + 1], d = through[Mathf.Min(k + 2, through.Length - 1)];
                int steps = Mathf.Max(2, Mathf.CeilToInt((c - b).magnitude));
                for (int i = 0; i < steps; i++)
                {
                    float t = i / (float)steps;
                    line.Add(0.5f * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (3 * b - a - 3 * c + d) * t * t * t));
                }
            }
            line.Add(through[through.Length - 1]);
            return line;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
