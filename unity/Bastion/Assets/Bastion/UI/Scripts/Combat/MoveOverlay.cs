using System;
using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>A tile the picked soldier can end its move on (game.ts reach): one for the dash, one where the squad would be noticed.</summary>
    public sealed class MoveTile
    {
        public Pos Pos;
        public bool Dash;
        public bool Detected;
    }

    /// <summary>
    /// The marks of a move on the map (overlays.ts, UI's, Gameplay 23/10), in the look Arturo chose (tileLooks.ts, 18/10:
    /// «Hueso y óxido», made clean): bone for what one action reaches, rust for the dash, no grid and no wash. Inside
    /// each side on the range's edge a strip bright at the edge and fading inwards, and on it a thin line with its
    /// corners rounded (Ana 22/10 #52), over a dark stroke; never round a thing in the way (Ana 21/10 #18). The way to
    /// the tile under the pointer, a ring on that tile, and over it a shield on each side with cover. A red frame where
    /// the squad would be noticed. Round the picked one's feet a white ring in three arcs that turn; round the
    /// partner's pick, a ring in their colour that breathes. Planning without the command, all of it fainter and blue.
    /// Lines keep their width in pixels (Bastion/Marks), and they and the shields are drawn over walls, as the web's.
    /// </summary>
    public sealed class MoveOverlay : MonoBehaviour
    {
        static readonly Color32 WalkFill = Hex(0xcdb486);
        static readonly Color32 DashFill = Hex(0x9a4a2c);
        static readonly Color32 WalkEdge = Hex(0xf3ecdc);
        static readonly Color32 DashEdge = Hex(0xc4704a);
        static readonly Color32 WalkPath = Hex(0xf6f0e2);
        static readonly Color32 DashPath = Hex(0xe2a684);
        static readonly Color32 PlanTint = Hex(0x7fb2d4);
        /// <summary>The brightest the ground goes under a line (overlays.ts SHADE).</summary>
        static readonly Color32 Shade = Hex(0x56606a);
        /// <summary>Being seen, one red for all of it (Ana, 21/10 #46).</summary>
        static readonly Color32 SightRed = Hex(0xf0503c);

        /// <summary>The pointer's ring on a tile one action away, on one for the dash (TileLook.ringColors).</summary>
        public static readonly Color32 WalkRing = WalkPath;
        public static readonly Color32 DashRing = DashPath;
        /// <summary>The pointer's ring on a tile where the squad would be noticed, and on an enemy.</summary>
        public static readonly Color32 NoticedRing = Hex(0xff4a4a);
        public static readonly Color32 EnemyRing = Hex(0xff5a5a);

        const float T = Board.Tile;
        const float FillAlpha = 0.45f;
        const float EdgeAlpha = 0.75f;
        const float EdgePx = 1.8f;
        const float EdgeShadePx = 1.8f + 2.2f;
        const float PathPx = 2f;
        const float PathShadePx = 2f + 4f;
        /// <summary>How far into the tile a strip fades (overlays.ts FADE_DEPTH).</summary>
        const float FadeDepth = 0.55f * T;
        /// <summary>How far back from a corner of the range its border turns (overlays.ts CORNER_R).</summary>
        const float CornerR = 0.24f * T;

        public static Color32 Hex(int rgb, byte alpha = 0xff) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);

        static Color32 Alpha(Color32 c, float a) => new Color32(c.r, c.g, c.b, (byte)Mathf.RoundToInt(255 * a));

        /// <summary>The camera the shields face; the main one if none.</summary>
        public Camera View;

        readonly MeshBuilder build = new MeshBuilder();
        readonly MeshBuilder under = new MeshBuilder();
        MeshFilter strips;
        MeshFilter frames;
        MeshFilter rangeShade;
        MeshFilter edges;
        MeshFilter wayShade;
        MeshFilter way;
        MeshFilter hover;
        Material hoverMaterial;
        Transform picked;
        Transform partner;
        Material partnerMaterial;
        Transform partnerHover;
        Material partnerHoverMaterial;
        readonly Transform[] shields = new Transform[4];
        readonly Material[] shieldMaterials = new Material[4];
        Texture2D lowShield;
        Texture2D highShield;
        MeshFilter eyes;

        void Awake()
        {
            strips = Layer("Alcance (franjas)", 3100, false, false);
            frames = Layer("Alcance (te verían)", 3101, false, false);
            eyes = Layer("Alcance (ojos)", 3101, false, false);
            eyes.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = EyeTexture();
            rangeShade = Layer("Alcance (sombra)", 3103, true, true);
            edges = Layer("Alcance (bordes)", 3104, true, false);
            wayShade = Layer("Camino (sombra)", 3103, true, true);
            way = Layer("Camino", 3105, true, false);

            // The pointer's ring (TileLook.ring: 0.33–0.41 of a tile), tinted by its material.
            hover = Layer("Puntero", 3102, false, false);
            build.Clear();
            build.Arc(Vector3.zero, 0.33f * T, 0.41f * T, 0, Mathf.PI * 2, 36, Hex(0xffffff, 0xe6));
            hover.sharedMesh = build.Build(null);
            hoverMaterial = hover.GetComponent<MeshRenderer>().sharedMaterial;
            hover.gameObject.SetActive(false);

            // The picked one's: three arcs (0.5–0.58 of a tile), a gap between each, over a thin dark edge that keeps them apart from the ground.
            var ring = Layer("Elegido", 3102, false, false);
            build.Clear();
            build.Arc(Vector3.zero, 0.47f * T, 0.61f * T, 0, Mathf.PI * 2, 48, Hex(0, 0x59));
            for (var i = 0; i < 3; i++)
            {
                var a = i * Mathf.PI * 2 / 3;
                build.Arc(Vector3.up * 0.004f, 0.5f * T, 0.58f * T, a, a + Mathf.PI * 2 / 3 - 0.35f, 8, Hex(0xffffff, 0xf2));
            }
            ring.sharedMesh = build.Build(null);
            picked = ring.transform;
            picked.gameObject.SetActive(false);

            var other = Layer("Elegido del compañero", 3102, false, false);
            build.Clear();
            build.Arc(Vector3.zero, 0.5f * T, 0.58f * T, 0, Mathf.PI * 2, 40, Hex(0xffffff, 0xbf));
            other.sharedMesh = build.Build(null);
            partner = other.transform;
            partnerMaterial = other.GetComponent<MeshRenderer>().sharedMaterial;
            partner.gameObject.SetActive(false);

            // The partner's pointer (overlays.ts partnerHover: 0.22–0.3 of a tile), in their colour.
            var theirs = Layer("Puntero del compañero", 3102, false, false);
            build.Clear();
            build.Arc(Vector3.zero, 0.22f * T, 0.3f * T, 0, Mathf.PI * 2, 32, Hex(0xffffff, 0xcc));
            theirs.sharedMesh = build.Build(null);
            partnerHover = theirs.transform;
            partnerHoverMaterial = theirs.GetComponent<MeshRenderer>().sharedMaterial;
            partnerHover.gameObject.SetActive(false);

            // A shield on each side (0.62 of a tile), facing the camera, over everything.
            lowShield = ShieldTexture(1);
            highShield = ShieldTexture(2);
            build.Clear();
            var h = 0.31f * T;
            build.Quad(new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0), Hex(0xffffff));
            var quad = build.Build(null);
            for (var i = 0; i < 4; i++)
            {
                var s = Layer("Cobertura " + i, 3110, true, false);
                s.sharedMesh = quad;
                shields[i] = s.transform;
                shieldMaterials[i] = s.GetComponent<MeshRenderer>().sharedMaterial;
                s.gameObject.SetActive(false);
            }
        }

        MeshFilter Layer(string name, int queue, bool overAll, bool shade)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = MeshBuilder.Material(queue, overAll, shade);
            return go.AddComponent<MeshFilter>();
        }

        void Update()
        {
            // The picked one's ring turns (18/10 #28: «en movimiento»); the partner's breathes.
            var time = Time.time;
            picked.localRotation = Quaternion.Euler(0, time * 0.9f * Mathf.Rad2Deg, 0);
            partner.localScale = Vector3.one * (1 - Mathf.Sin(time * 5) * 0.06f);
        }

        void LateUpdate()
        {
            var cam = View != null ? View : Camera.main;
            if (cam == null) return;
            foreach (var s in shields) s.rotation = cam.transform.rotation;
        }

        public void Clear()
        {
            ShowRange(null, null);
            ShowPath(null, null, null);
            ShowHover(null, null, default);
            ShowCover(null, null);
            ShowPicked(null);
            ShowPartner(null, Color.white);
        }

        /// <summary>
        /// The tiles the picked one can end its move on, on the floor shown (game.ts refreshInteractive): the strips and
        /// lines round each band, a red frame where it would be noticed. `planning`: without the command, fainter and
        /// in the planning blue. `soft(x, y, z)`: a thing in the way the border leaves alone.
        /// </summary>
        public void ShowRange(GameState s, IList<MoveTile> tiles, bool planning = false, Func<int, int, int, bool> soft = null)
        {
            build.Clear();
            under.Clear();
            var lines = new MeshBuilder();
            var red = new MeshBuilder();
            var eye = new MeshBuilder();
            if (s != null && tiles != null)
            {
                var range = new Dictionary<int, MoveTile>();
                foreach (var t in tiles) range[t.Pos.X * 1024 + t.Pos.Y] = t;
                var walkSegs = new List<Vector3[]>();
                var dashSegs = new List<Vector3[]>();
                var fill = FillAlpha * (planning ? 0.5f : 1);
                foreach (var t in tiles)
                {
                    var p = t.Pos;
                    var floor = Board.TileToWorld(s, p).y;
                    for (var side = 0; side < 4; side++)
                    {
                        var d = Grid.Dirs4[side];
                        range.TryGetValue((p.X + d.X) * 1024 + p.Y + d.Y, out var n);
                        var outside = t.Dash ? n == null : n == null || n.Dash;
                        if (!outside) continue;
                        // Not round a thing in the way: no line on the side that faces it.
                        if (n == null && soft != null && soft(p.X + d.X, p.Y + d.Y, p.Z)) continue;
                        Side(p.X, p.Y, side, floor + 0.06f * T, out var a, out var b);
                        (t.Dash ? dashSegs : walkSegs).Add(new[] { a, b });
                        Strip(p.X, p.Y, side, floor + 0.03f * T, planning ? PlanTint : t.Dash ? DashFill : WalkFill, fill);
                    }
                    if (t.Detected)
                    {
                        // A red frame, and an eye in it (patterns besides colour: UX-REVISION-2 #49).
                        var c = Board.TileToWorld(s, p, 0.03f * T);
                        Frame(red, c);
                        var e = 0.17f * T;
                        eye.Quad(c + new Vector3(-e, 0, -e), c + new Vector3(e, 0, -e), c + new Vector3(e, 0, e), c + new Vector3(-e, 0, e), Alpha(SightRed, 0.95f));
                    }
                }
                var edge = EdgeAlpha * (planning ? 0.55f : 1);
                foreach (var seg in RoundCorners(walkSegs, CornerR)) Stroke(lines, seg[0], seg[1], EdgePx, EdgeShadePx, Alpha(planning ? PlanTint : WalkEdge, edge));
                foreach (var seg in RoundCorners(dashSegs, CornerR)) Stroke(lines, seg[0], seg[1], EdgePx, EdgeShadePx, Alpha(planning ? PlanTint : DashEdge, edge));
            }
            strips.sharedMesh = build.Build(strips.sharedMesh);
            rangeShade.sharedMesh = under.Build(rangeShade.sharedMesh);
            edges.sharedMesh = lines.Build(edges.sharedMesh);
            frames.sharedMesh = red.Build(frames.sharedMesh);
            eyes.sharedMesh = eye.Build(eyes.sharedMesh);
        }

        /// <summary>A line and, in the `under` builder, the dark stroke beneath it.</summary>
        void Stroke(MeshBuilder into, Vector3 a, Vector3 b, float px, float shadePx, Color32 color)
        {
            into.Line(a, b, px, color);
            under.Line(a, b, shadePx, Shade);
        }

        /// <summary>A tile's side (DIRS4: 0 north, 1 east, 2 south, 3 west), its ends going round the tile clockwise.</summary>
        static void Side(int x, int y, int side, float h, out Vector3 from, out Vector3 to)
        {
            var nw = Board.Corner(x, y, h);
            var ne = nw + new Vector3(T, 0, 0);
            var se = nw + new Vector3(T, 0, -T);
            var sw = nw + new Vector3(0, 0, -T);
            switch (side)
            {
                case 0: from = nw; to = ne; break;
                case 1: from = ne; to = se; break;
                case 2: from = se; to = sw; break;
                default: from = sw; to = nw; break;
            }
        }

        /// <summary>A strip inside a tile's side, the fill's colour at the edge and gone FadeDepth in (overlays.ts fadeTexture: 1, 0.45 at a third, 0).</summary>
        void Strip(int x, int y, int side, float h, Color32 color, float alpha)
        {
            Side(x, y, side, h, out var a, out var b);
            var middle = Board.Corner(x, y, h) + new Vector3(T / 2, 0, -T / 2);
            var mid = (a + b) / 2;
            var inward = (middle - mid).normalized;
            var k = 0.35f * FadeDepth;
            var c0 = Alpha(color, alpha);
            var c1 = Alpha(color, alpha * 0.45f);
            var c2 = Alpha(color, 0);
            build.Quad(a, b, b + inward * k, a + inward * k, c0, c0, c1, c1);
            build.Quad(a + inward * k, b + inward * k, b + inward * FadeDepth, a + inward * FadeDepth, c1, c1, c2, c2);
        }

        /// <summary>A square frame on a tile (overlays.ts detect: a four-sided ring 0.46–0.63 turned 45°).</summary>
        static void Frame(MeshBuilder into, Vector3 c)
        {
            var i = 0.46f * T / Mathf.Sqrt(2);
            var o = 0.63f * T / Mathf.Sqrt(2);
            var color = Alpha(SightRed, 0.95f);
            into.Quad(c + new Vector3(-o, 0, o), c + new Vector3(o, 0, o), c + new Vector3(i, 0, i), c + new Vector3(-i, 0, i), color);
            into.Quad(c + new Vector3(o, 0, o), c + new Vector3(o, 0, -o), c + new Vector3(i, 0, -i), c + new Vector3(i, 0, i), color);
            into.Quad(c + new Vector3(o, 0, -o), c + new Vector3(-o, 0, -o), c + new Vector3(-i, 0, -i), c + new Vector3(i, 0, -i), color);
            into.Quad(c + new Vector3(-o, 0, -o), c + new Vector3(-o, 0, o), c + new Vector3(-i, 0, i), c + new Vector3(-i, 0, -i), color);
        }

        /// <summary>
        /// A border's segments with their right-angled corners rounded (overlays.ts roundCorners): where exactly two meet
        /// square, each stops `r` short of the corner and a curve joins them (a quadratic one, its control the corner,
        /// in four steps). Straight joins, loose ends and crossings stay as they are.
        /// </summary>
        static List<Vector3[]> RoundCorners(List<Vector3[]> segs, float r)
        {
            var at = new Dictionary<Vector3Int, List<(int seg, int end)>>();
            Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * 1000), Mathf.RoundToInt(v.y * 1000), Mathf.RoundToInt(v.z * 1000));
            for (var i = 0; i < segs.Count; i++)
                for (var end = 0; end < 2; end++)
                {
                    var k = Key(segs[i][end]);
                    if (!at.TryGetValue(k, out var list)) at[k] = list = new List<(int, int)>();
                    list.Add((i, end));
                }
            var trimmed = segs.ConvertAll(sg => new[] { sg[0], sg[1] });
            var result = new List<Vector3[]>();
            foreach (var list in at.Values)
            {
                if (list.Count != 2) continue;
                var (sa, ea) = list[0];
                var (sb, eb) = list[1];
                var v = segs[sa][ea];
                var da = segs[sa][1 - ea] - v;
                var db = segs[sb][1 - eb] - v;
                da.y = db.y = 0;
                var la = da.magnitude;
                var lb = db.magnitude;
                // Square corners only (a straight join stays), and room on both for the bend.
                if (la == 0 || lb == 0 || Mathf.Abs(Vector3.Dot(da, db)) > 1e-4f || la < r * 2 || lb < r * 2) continue;
                var p1 = v + da / la * r;
                var p2 = v + db / lb * r;
                trimmed[sa][ea] = p1;
                trimmed[sb][eb] = p2;
                var prev = p1;
                for (var k = 1; k <= 4; k++)
                {
                    var t = k / 4f;
                    var q = (1 - t) * (1 - t) * p1 + 2 * (1 - t) * t * v + t * t * p2;
                    result.Add(new[] { prev, q });
                    prev = q;
                }
            }
            result.AddRange(trimmed);
            return result;
        }

        /// <summary>
        /// The way from where the soldier stands (`from`) along `path` (BuildPath's places), in the walk's colour or the
        /// dash's (or planning's blue), a little over the floor; null clears it.
        /// </summary>
        public void ShowPath(GameState s, Pos from, IList<Pos> path, bool dash = false, bool planning = false)
        {
            build.Clear();
            under.Clear();
            if (s != null && from != null && path != null && path.Count > 0)
            {
                var color = Alpha(planning ? PlanTint : dash ? DashPath : WalkPath, 0.95f);
                var prev = Board.TileToWorld(s, from, 0.08f * T);
                foreach (var p in path)
                {
                    var at = Board.TileToWorld(s, p, 0.08f * T);
                    build.Line(prev, at, PathPx, color);
                    under.Line(prev, at, PathShadePx, Shade);
                    prev = at;
                }
            }
            way.sharedMesh = build.Build(way.sharedMesh);
            wayShade.sharedMesh = under.Build(wayShade.sharedMesh);
        }

        /// <summary>The pointer's ring on `tile` in `color` (WalkRing, DashRing, NoticedRing, EnemyRing); null: none.</summary>
        public void ShowHover(GameState s, Pos tile, Color32 color)
        {
            hover.gameObject.SetActive(s != null && tile != null);
            if (s == null || tile == null) return;
            hover.transform.localPosition = Board.TileToWorld(s, tile, 0.04f * T);
            hoverMaterial.color = color;
        }

        /// <summary>Over `tile`, a shield on each side with cover (Cover.CoverSides: 1 low, half full; 2 high, full); null clears them.</summary>
        public void ShowCover(GameState s, Pos tile)
        {
            var sides = s != null && tile != null ? Cover.CoverSides(s, tile) : null;
            for (var i = 0; i < 4; i++)
            {
                var level = sides != null && i < sides.Length ? sides[i] : 0;
                shields[i].gameObject.SetActive(level > 0);
                if (level <= 0) continue;
                var d = Grid.Dirs4[i];
                shields[i].localPosition = Board.TileToWorld(s, tile, 1.25f * T) + new Vector3(d.X, 0, -d.Y) * (0.45f * T);
                shieldMaterials[i].mainTexture = level >= 2 ? highShield : lowShield;
            }
        }

        /// <summary>The ring round the picked soldier, at its feet (null: none).</summary>
        public void ShowPicked(Vector3? feet)
        {
            picked.gameObject.SetActive(feet != null);
            if (feet != null) picked.localPosition = feet.Value + Vector3.up * (0.035f * T);
        }

        /// <summary>The ring round the soldier the partner has picked, in their colour (null: none).</summary>
        public void ShowPartner(Vector3? feet, Color color)
        {
            partner.gameObject.SetActive(feet != null);
            if (feet == null) return;
            partner.localPosition = feet.Value + Vector3.up * (0.033f * T);
            partnerMaterial.color = color;
        }

        /// <summary>The tile under the partner's pointer (their presence), a small ring in their colour; null, none.</summary>
        public void ShowPartnerHover(GameState s, Pos tile, Color color)
        {
            partnerHover.gameObject.SetActive(s != null && tile != null);
            if (s == null || tile == null) return;
            partnerHover.localPosition = Board.TileToWorld(s, tile, 0.045f * T);
            partnerHoverMaterial.color = color;
        }

        /// <summary>
        /// The shield (overlays.ts shieldTexture, 64 px): a dark shield, filled in cyan from halfway down for low cover
        /// and whole for high, edged in pale cyan.
        /// </summary>
        static Texture2D ShieldTexture(int level)
        {
            const int size = 64;
            // Its outline, top-down in the canvas's pixels: a point at the top, the sides, two curves to the tip.
            var shape = new List<Vector2> { new Vector2(32, 4), new Vector2(56, 13), new Vector2(53, 38) };
            for (var k = 1; k <= 8; k++) shape.Add(Bezier(new Vector2(53, 38), new Vector2(46, 54), new Vector2(32, 60), k / 8f));
            for (var k = 1; k <= 8; k++) shape.Add(Bezier(new Vector2(32, 60), new Vector2(18, 54), new Vector2(11, 38), k / 8f));
            shape.Add(new Vector2(8, 13));
            var dark = new Color(10 / 255f, 20 / 255f, 30 / 255f, 0.75f);
            var fill = (Color)Hex(0x54d1ff);
            var stroke = (Color)Hex(0xbff0ff);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (var py = 0; py < size; py++)
            for (var px = 0; px < size; px++)
            {
                // Four samples a pixel, for soft edges.
                var sum = Color.clear;
                for (var sy = 0; sy < 2; sy++)
                for (var sx = 0; sx < 2; sx++)
                {
                    var p = new Vector2(px + 0.25f + sx * 0.5f, py + 0.25f + sy * 0.5f);
                    var inside = Inside(shape, p);
                    var edge = Distance(shape, p) <= 2f;
                    Color c;
                    if (edge) c = stroke;
                    else if (inside) c = p.y >= (level >= 2 ? 0 : 34) ? fill : dark;
                    else c = Color.clear;
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
                sum /= 4;
                var a = sum.a;
                // The canvas's rows go down, the texture's up.
                pixels[(size - 1 - py) * size + px] = a > 0 ? new Color(sum.r / a, sum.g / a, sum.b / a, a) : Color.clear;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>An eye (styles.css's mask, 16 × 10 in a 64 square), white: tinted by the quad's colour.</summary>
        static Texture2D EyeTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (var py = 0; py < size; py++)
            for (var px = 0; px < size; px++)
            {
                var a = 0f;
                for (var sy = 0; sy < 2; sy++)
                for (var sx = 0; sx < 2; sx++)
                {
                    // In the drawing's 16 × 10 units, centred.
                    var x = (px + 0.25f + sx * 0.5f) / size * 16;
                    var y = (py + 0.25f + sy * 0.5f) / size * 16 - 3;
                    var dx = (x - 8) / 8;
                    var dy = (y - 5) / 5;
                    var almond = Mathf.Abs(dy) <= Mathf.Sqrt(Mathf.Max(0, 1 - dx * dx)) * (1 - 0.15f * dx * dx);
                    var d = Mathf.Sqrt((x - 8) * (x - 8) + (y - 5) * (y - 5));
                    if (almond && (d > 3.2f || d <= 1.6f)) a += 0.25f;
                }
                pixels[py * size + px] = new Color(1, 1, 1, a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;

        static bool Inside(List<Vector2> poly, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i];
                var b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        static float Distance(List<Vector2> poly, Vector2 p)
        {
            var best = float.MaxValue;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[j];
                var ab = poly[i] - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }
    }
}
