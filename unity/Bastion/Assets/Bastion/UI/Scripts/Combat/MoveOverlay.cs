using System;
using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>A tile the picked soldier can end its move on (game.ts reach): one for the dash, one where the squad would be noticed, one on fire.</summary>
    public sealed class MoveTile
    {
        public Pos Pos;
        public bool Dash;
        /// <summary>Only got to running: past Content.QuietTiles of the turn's movement, in one stretch or several (Paths.IsRun; Arturo, 5/11), and heard.</summary>
        public bool Run;
        public bool Detected;
        public bool Fire;
    }

    /// <summary>
    /// The marks of a move on the map (overlays.ts, UI's, Gameplay 23/10), in the look Arturo chose (tileLooks.ts, 18/10:
    /// «Hueso y óxido», made clean), now sky blue for all the movement left, one band (Arturo, 5/11 #13, #14: no dash; the
    /// rust kept for a recording of before), no grid and no wash. Inside
    /// the range's edge one glow, bright at the edge and fading inwards, round the corners too (Glow; Arturo 5/11 #16),
    /// and on it a thin line with its corners rounded (Ana 22/10 #52), in its colour alone (#16: no dark stroke), unbroken:
    /// it goes round the outside of a thing in the way at the edge, never stops at it nor rings one inside (Ana 21/10
    /// #18, Arturo 01/11 #4: Outline). The way to
    /// the tile under the pointer, a ring on that tile, and over it a shield on each side with cover. Where the squad
    /// would be noticed, an eye in a soft red, no frame (Arturo, 31/10 #14); where the ground burns, a flame in orange
    /// (Arturo, 01/11: it burns whoever steps on it). Round the picked one's feet a white ring in
    /// three arcs that turn; round the partner's pick, a ring in their colour that breathes. Planning without the command, all of it fainter and grey (blue is the reach's now).
    /// Lines keep their width in pixels (Bastion/Marks). The shields are drawn over walls, as the web's; the lines over
    /// everything but the figures, soldiers and the dead (Arturo, 31/10 #3: not cars, crates or walls), behind the legs
    /// standing over them (Ana, Arturo 31/10 #6: Pull).
    /// Over a shoulder all but the rings go (Cinematic: #3).
    /// </summary>
    public sealed class MoveOverlay : MonoBehaviour
    {
        static readonly Color32 WalkFill = Hex(0x3aa8e6);
        static readonly Color32 DashFill = Hex(0x9a4a2c);
        static readonly Color32 WalkEdge = Hex(0x7fd4fa);
        static readonly Color32 DashEdge = Hex(0xc4704a);
        static readonly Color32 WalkPath = Hex(0xa6e2fc);
        static readonly Color32 DashPath = Hex(0xe2a684);
        static readonly Color32 PlanTint = Hex(0xb4bec6);
        /// <summary>The brightest the ground goes under a line (overlays.ts SHADE).</summary>
        static readonly Color32 Shade = Hex(0x56606a);
        /// <summary>The eye on a tile where the squad would be noticed: a soft red (Arturo, 31/10 #14; it was 0xf0503c with a frame).</summary>
        static readonly Color32 SoftSight = Hex(0xe58a80);
        /// <summary>The flame on a tile of the reach on fire.</summary>
        static readonly Color32 FireMark = Hex(0xff8a3c);

        /// <summary>The pointer's ring planning without the command (on a reachable tile only: Ana, Arturo 01/11 #1).</summary>
        public static readonly Color32 PlanRing = PlanTint;
        /// <summary>The pointer's ring on a tile one action away, on one for the dash (TileLook.ringColors).</summary>
        public static readonly Color32 WalkRing = WalkPath;
        public static readonly Color32 DashRing = DashPath;
        /// <summary>The pointer's ring on a tile where the squad would be noticed.</summary>
        public static readonly Color32 NoticedRing = Hex(0xff4a4a);
        /// <summary>The lit ring of a soldier pointed at from the list (#11), as its flag's arrow (Flags.uss .uflag__pointer).</summary>
        static readonly Color32 PointedRing = Hex(0xffe27a);

        const float T = Board.Tile;
        const float FillAlpha = 0.45f;
        const float EdgeAlpha = 0.75f;
        const float EdgePx = 1.8f;
        const float EdgeShadePx = 1.8f + 2.2f;
        const float PathPx = 2f;
        const float PathShadePx = 2f + 4f;
        /// <summary>How far in from the border its glow fades (overlays.ts FADE_DEPTH).</summary>
        const float FadeDepth = 0.55f * T;
        /// <summary>How far back from a corner of the range its border turns (overlays.ts CORNER_R).</summary>
        const float CornerR = 0.24f * T;
        /// <summary>
        /// How much nearer the camera the lines are tested against a figure's depth (Bastion/Marks _Pull, metres): short of
        /// a body standing over them (a line a tile's edge behind the feet is a metre behind the legs).
        /// </summary>
        const float Pull = 0.4f;

        public static Color32 Hex(int rgb, byte alpha = 0xff) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);

        static Color32 Alpha(Color32 c, float a) => new Color32(c.r, c.g, c.b, (byte)Mathf.RoundToInt(255 * a));

        /// <summary>The camera the shields face; the main one if none.</summary>
        public Camera View;

        /// <summary>What a move shows (the reach, the way, the pointers, the shields): all of it off over a shoulder.</summary>
        Transform zone;
        readonly MeshBuilder build = new MeshBuilder();
        readonly MeshBuilder under = new MeshBuilder();
        MeshFilter strips;
        MeshFilter rangeShade;
        MeshFilter edges;
        MeshFilter wayShade;
        MeshFilter way;
        MeshFilter hover;
        Material hoverMaterial;
        Transform picked;
        Transform partner;
        Material partnerMaterial;
        /// <summary>The soldier whose name is under the pointer in the list: a bigger ring, lit, that beats (Arturo, 31/10 #11).</summary>
        Transform pointed;
        Transform partnerHover;
        Material partnerHoverMaterial;
        /// <summary>The rings under the enemies in sight (ShowEnemies), as many as there have been at once.</summary>
        readonly List<Transform> enemyRings = new List<Transform>();
        Mesh enemyRing;
        readonly Transform[] shields = new Transform[4];
        readonly Material[] shieldMaterials = new Material[4];
        Texture2D lowShield;
        Texture2D highShield;
        MeshFilter eyes;
        MeshFilter flames;

        void Awake()
        {
            zone = new GameObject("Zona").transform;
            zone.SetParent(transform, false);
            strips = Layer("Alcance (franjas)", 3100, false, false, zone);
            eyes = Layer("Alcance (ojos)", 3101, false, false, zone);
            eyes.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = EyeTexture();
            flames = Layer("Alcance (fuego)", 3101, false, false, zone);
            flames.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = FlameTexture();
            rangeShade = Layer("Alcance (sombra)", 3103, false, true, zone, Pull, true);
            edges = Layer("Alcance (bordes)", 3104, false, false, zone, Pull, true);
            wayShade = Layer("Camino (sombra)", 3103, false, true, zone, Pull, true);
            way = Layer("Camino", 3105, false, false, zone, Pull, true);

            // The pointer's ring (TileLook.ring: 0.33–0.41 of a tile), tinted by its material.
            hover = Layer("Puntero", 3102, false, false, zone);
            build.Clear();
            build.Arc(Vector3.zero, 0.33f * T, 0.41f * T, 0, Mathf.PI * 2, 36, Hex(0xffffff, 0xe6));
            hover.sharedMesh = build.Build(null);
            hoverMaterial = hover.GetComponent<MeshRenderer>().sharedMaterial;
            hover.gameObject.SetActive(false);

            // The picked one's: Apocalypse's thin ring, white (0.54 of a tile; Ana, Arturo 29/10: much thinner).
            var ring = Layer("Elegido", 3102, false, false);
            ring.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = RingTexture();
            build.Clear();
            Ring(build, Vector3.zero, 0.54f * T, Hex(0xffffff, 0xf2));
            ring.sharedMesh = build.Build(null);
            picked = ring.transform;
            picked.gameObject.SetActive(false);

            var other = Layer("Elegido del compañero", 3102, false, false);
            other.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = RingTexture();
            build.Clear();
            Ring(build, Vector3.zero, 0.54f * T, Hex(0xffffff, 0xbf));
            other.sharedMesh = build.Build(null);
            partner = other.transform;
            partnerMaterial = other.GetComponent<MeshRenderer>().sharedMaterial;
            partner.gameObject.SetActive(false);

            // The soldier pointed at from the list: the same ring, larger and in a warm light (#11).
            var lit = Layer("Señalado", 3102, false, false);
            lit.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = RingTexture();
            build.Clear();
            Ring(build, Vector3.zero, 0.62f * T, PointedRing);
            Ring(build, Vector3.zero, 0.52f * T, PointedRing);
            lit.sharedMesh = build.Build(null);
            pointed = lit.transform;
            pointed.gameObject.SetActive(false);

            // The partner's pointer (overlays.ts partnerHover: 0.22–0.3 of a tile), in their colour.
            var theirs = Layer("Puntero del compañero", 3102, false, false, zone);
            build.Clear();
            build.Arc(Vector3.zero, 0.22f * T, 0.3f * T, 0, Mathf.PI * 2, 32, Hex(0xffffff, 0xcc));
            theirs.sharedMesh = build.Build(null);
            partnerHover = theirs.transform;
            partnerHoverMaterial = theirs.GetComponent<MeshRenderer>().sharedMaterial;
            partnerHover.gameObject.SetActive(false);

            // Under each enemy in sight: the same thin ring, 0.42 of a tile, in ALIEN_COLOR at 0.95 (unitViews.ts drew a band 0.33–0.46);
            // the red line alone, without the dark threads, that looked a grey edge by it (Ana, Arturo 01/11 #7).
            build.Clear();
            Ring(build, Vector3.zero, 0.42f * T, Hex(0xff4d4d, 0xf2), false);
            enemyRing = build.Build(null);

            // A shield on each side (0.62 of a tile), facing the camera, over everything.
            lowShield = ShieldTexture(1);
            highShield = ShieldTexture(2);
            build.Clear();
            var h = 0.31f * T;
            build.Quad(new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0), Hex(0xffffff));
            var quad = build.Build(null);
            for (var i = 0; i < 4; i++)
            {
                var s = Layer("Cobertura " + i, 3110, true, false, zone);
                s.sharedMesh = quad;
                shields[i] = s.transform;
                shieldMaterials[i] = s.GetComponent<MeshRenderer>().sharedMaterial;
                s.gameObject.SetActive(false);
            }
        }

        MeshFilter Layer(string name, int queue, bool overAll, bool shade, Transform parent = null, float pull = 0, bool figures = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = MeshBuilder.Material(queue, overAll, shade, pull, figures);
            return go.AddComponent<MeshFilter>();
        }

        /// <summary>
        /// Over a shoulder (the aim's, an attack's): as a cutscene, none of a move's marks, this player's or the partner's
        /// (Ana, Arturo 31/10 #3); back as it ends. The rings under the units stay.
        /// </summary>
        public void Cinematic(bool on)
        {
            if (zone != null && zone.gameObject.activeSelf == on) zone.gameObject.SetActive(!on);
        }

        void Update()
        {
            // The picked one's ring turns (18/10 #28: «en movimiento»); the partner's breathes.
            var time = Time.time;
            picked.localRotation = Quaternion.Euler(0, time * 0.9f * Mathf.Rad2Deg, 0);
            partner.localScale = Vector3.one * (1 - Mathf.Sin(time * 5) * 0.06f);
            pointed.localScale = Vector3.one * (1 + Mathf.Sin(time * 7) * 0.08f);
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
            ShowEnemies(null);
        }

        /// <summary>
        /// The tiles the picked one can end its move on, on the floor shown (game.ts refreshInteractive): the strips and
        /// lines round each band, a red frame where it would be noticed. `planning`: without the command, fainter and
        /// in the planning blue. `soft(x, y, z)`: a thing in the way the border goes round the outside of (Outline).
        /// </summary>
        public void ShowRange(GameState s, IList<MoveTile> tiles, bool planning = false, Func<int, int, int, bool> soft = null)
        {
            build.Clear();
            under.Clear();
            var lines = new MeshBuilder();
            var eye = new MeshBuilder();
            var flame = new MeshBuilder();
            if (s != null && tiles != null)
            {
                var walkSegs = new List<Vector3[]>();
                var dashSegs = new List<Vector3[]>();
                var walkFloors = new Dictionary<int, float>();
                var dashFloors = new Dictionary<int, float>();
                var fill = FillAlpha * (planning ? 0.5f : 1);
                var bands = Outline(tiles, soft);
                foreach (var (p, dash) in bands.Values)
                {
                    var floor = Board.TileToWorld(s, p).y;
                    (dash ? dashFloors : walkFloors)[Key(p.X, p.Y)] = floor;
                    for (var side = 0; side < 4; side++)
                    {
                        var d = Grid.Dirs4[side];
                        var has = bands.TryGetValue(Key(p.X + d.X, p.Y + d.Y), out var n);
                        var outside = dash ? !has : !has || n.dash;
                        if (!outside) continue;
                        Side(p.X, p.Y, side, floor + 0.06f * T, out var a, out var b);
                        (dash ? dashSegs : walkSegs).Add(new[] { a, b });
                    }
                }
                foreach (var t in tiles)
                {
                    var p = t.Pos;
                    // Both on one tile, side by side.
                    var apart = t.Detected && t.Fire ? new Vector3(0.14f * T, 0, 0) : Vector3.zero;
                    if (t.Detected)
                    {
                        // An eye (a pattern besides colour: UX-REVISION-2 #49), in a soft red and with no frame round it (#14).
                        var c = Board.TileToWorld(s, p, 0.03f * T) - apart;
                        var e = 0.17f * T;
                        eye.Quad(c + new Vector3(-e, 0, -e), c + new Vector3(e, 0, -e), c + new Vector3(e, 0, e), c + new Vector3(-e, 0, e), Alpha(SoftSight, 0.85f));
                    }
                    if (t.Fire)
                    {
                        var c = Board.TileToWorld(s, p, 0.03f * T) + apart;
                        var e = 0.17f * T;
                        flame.Quad(c + new Vector3(-e, 0, -e), c + new Vector3(e, 0, -e), c + new Vector3(e, 0, e), c + new Vector3(-e, 0, e), Alpha(FireMark, 0.9f));
                    }
                }
                // Each band's line alone in its colour, no dark stroke under it (Arturo, 5/11 #16), and inside it one glow.
                var edge = EdgeAlpha * (planning ? 0.55f : 1);
                var walkBends = new List<Bend>();
                var dashBends = new List<Bend>();
                var walkLine = RoundCorners(walkSegs, CornerR, walkBends);
                var dashLine = RoundCorners(dashSegs, CornerR, dashBends);
                foreach (var seg in walkLine) lines.Line(seg[0], seg[1], EdgePx, Alpha(planning ? PlanTint : WalkEdge, edge));
                foreach (var seg in dashLine) lines.Line(seg[0], seg[1], EdgePx, Alpha(planning ? PlanTint : DashEdge, edge));
                Glow(build, walkFloors, walkLine, walkBends, planning ? PlanTint : WalkFill, fill);
                Glow(build, dashFloors, dashLine, dashBends, planning ? PlanTint : DashFill, fill);
            }
            strips.sharedMesh = build.Build(strips.sharedMesh);
            rangeShade.sharedMesh = under.Build(rangeShade.sharedMesh);
            edges.sharedMesh = lines.Build(edges.sharedMesh);
            eyes.sharedMesh = eye.Build(eyes.sharedMesh);
            flames.sharedMesh = flame.Build(flames.sharedMesh);
        }

        static int Key(int x, int y) => x * 1024 + y;

        /// <summary>
        /// What the reach's border goes round, each tile in its band (dash or not): the reach; the things in the way beside
        /// it (`soft`: a car, a crate, a console, someone standing), in the walk's band if they touch it; and any hole left
        /// inside of nothing but such things. So the line goes on round the outside of a thing at its edge (Arturo, 01/11
        /// #4: it broke off at each one, 30 to 80 sides on a city's reach) and never rings one inside (Ana, 21/10 #18);
        /// walls and holes with a wall in them it goes round as before.
        /// </summary>
        static Dictionary<int, (Pos pos, bool dash)> Outline(IList<MoveTile> tiles, Func<int, int, int, bool> soft)
        {
            var bands = new Dictionary<int, (Pos pos, bool dash)>();
            foreach (var t in tiles) bands[Key(t.Pos.X, t.Pos.Y)] = (t.Pos, t.Dash);
            if (soft == null || tiles.Count == 0) return bands;
            var reach = new HashSet<int>(bands.Keys);
            var z = tiles[0].Pos.Z;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var t in tiles)
            {
                x0 = Mathf.Min(x0, t.Pos.X);
                y0 = Mathf.Min(y0, t.Pos.Y);
                x1 = Mathf.Max(x1, t.Pos.X);
                y1 = Mathf.Max(y1, t.Pos.Y);
                for (var side = 0; side < 4; side++)
                {
                    var d = Grid.Dirs4[side];
                    int x = t.Pos.X + d.X, y = t.Pos.Y + d.Y;
                    var k = Key(x, y);
                    if (reach.Contains(k)) continue;
                    // A thing taken in already: in the walk's band once any of the walk touches it.
                    if (bands.ContainsKey(k))
                    {
                        if (!t.Dash) bands[k] = (bands[k].pos, false);
                        continue;
                    }
                    if (soft(x, y, z)) bands[k] = (new Pos { X = x, Y = y, Z = z }, t.Dash);
                }
            }
            // The holes: what the rim of the box round it all (two tiles out: the things beside are one) doesn't reach.
            x0 -= 2;
            y0 -= 2;
            x1 += 2;
            y1 += 2;
            var outside = new HashSet<int>();
            var queue = new Queue<(int x, int y)>();
            for (var x = x0; x <= x1; x++)
            {
                queue.Enqueue((x, y0));
                queue.Enqueue((x, y1));
            }
            for (var y = y0; y <= y1; y++)
            {
                queue.Enqueue((x0, y));
                queue.Enqueue((x1, y));
            }
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x < x0 || x > x1 || y < y0 || y > y1 || bands.ContainsKey(Key(x, y)) || !outside.Add(Key(x, y))) continue;
                foreach (var d in Grid.Dirs4) queue.Enqueue((x + d.X, y + d.Y));
            }
            var hole = new List<(int x, int y)>();
            var seen = new HashSet<int>();
            for (var x = x0; x <= x1; x++)
                for (var y = y0; y <= y1; y++)
                {
                    if (bands.ContainsKey(Key(x, y)) || outside.Contains(Key(x, y)) || seen.Contains(Key(x, y))) continue;
                    hole.Clear();
                    var things = true;
                    var walk = false;
                    queue.Enqueue((x, y));
                    while (queue.Count > 0)
                    {
                        var (a, b) = queue.Dequeue();
                        var k = Key(a, b);
                        if (bands.TryGetValue(k, out var by))
                        {
                            walk |= !by.dash;
                            continue;
                        }
                        if (outside.Contains(k) || !seen.Add(k)) continue;
                        hole.Add((a, b));
                        things &= soft(a, b, z);
                        foreach (var d in Grid.Dirs4) queue.Enqueue((a + d.X, b + d.Y));
                    }
                    if (!things) continue;
                    foreach (var (a, b) in hole) bands[Key(a, b)] = (new Pos { X = a, Y = b, Z = z }, !walk);
                }
            return bands;
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

        static Texture2D ringTexture;

        /// <summary>
        /// The units' ring: Synty INTERFACE Apocalypse HUD's SPR_HUD_Apocalypse_Ring_Large_02_Clean (Ana's #2, Arturo 29/10), a
        /// thin white line round 0.887 of the picture's half, tinted by the mark's colour.
        /// </summary>
        internal static Texture2D RingTexture()
        {
            if (ringTexture != null) return ringTexture;
            ringTexture = Resources.Load<Texture2D>("Combat/Aro");
            if (ringTexture != null) ringTexture.wrapMode = TextureWrapMode.Clamp;
            return ringTexture;
        }

        /// <summary>
        /// A flat ring of `radius` round `c` in `color` (RingTexture's picture, sized so its line falls there), with two dark
        /// threads just outside and two just inside it that keep it apart from snow and light floors (`threads`: the
        /// enemies' go without them).
        /// </summary>
        internal static void Ring(MeshBuilder into, Vector3 c, float radius, Color32 color, bool threads = true)
        {
            void Square(float r, Vector3 at, Color32 tint)
            {
                var h = r / 0.887f;
                into.Quad(at + new Vector3(-h, 0, -h), at + new Vector3(h, 0, -h), at + new Vector3(h, 0, h), at + new Vector3(-h, 0, h), tint);
            }
            var dark = Hex(0, (byte)(color.a * 0.6f));
            if (threads)
                foreach (var k in new[] { 1.06f, 1.035f, 0.965f, 0.94f }) Square(radius * k, c, dark);
            Square(radius, c + Vector3.up * 0.002f, color);
        }

        /// <summary>A corner RoundCorners bent: where it was, the two ways its sides go from it, and how far back they turn.</summary>
        struct Bend
        {
            public Vector2 At;
            public Vector2 A;
            public Vector2 B;
            public float R;
        }

        /// <summary>How many steps a tile is cut into each way for the glow (Glow): enough for the fade to look smooth.</summary>
        const int GlowSteps = 8;

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>The tile a point of the ground (x, z) lies on, as Key's.</summary>
        static int TileKey(Vector2 p) => Key(Mathf.FloorToInt(p.x / T), Mathf.FloorToInt(-p.y / T));

        /// <summary>
        /// The glow inside a band's border (Arturo, 5/11 #16: it went tile by tile, square at the corners and broken where
        /// the border turned in): one glow along all of it, strongest at the line and gone FadeDepth in (overlays.ts's fade:
        /// 0.45 at a third), round its rounded corners too. Each tile near the line is cut into GlowSteps × GlowSteps, each
        /// point lit by how far it is from the line as drawn (`line`, RoundCorners'), if it is on the line's inside: a tile of
        /// the band (`floors`, by Key, with its floor's height) but past a bend's curve at a corner that sticks out, or within
        /// one at a corner that turns in (`bends`).
        /// </summary>
        static void Glow(MeshBuilder into, Dictionary<int, float> floors, List<Vector3[]> line, List<Bend> bends, Color32 color, float alpha)
        {
            if (floors.Count == 0 || line.Count == 0) return;
            // Each tile with the pieces of the line near enough to light it.
            var near = new Dictionary<int, List<int>>();
            for (var i = 0; i < line.Count; i++)
            {
                var a = Flat(line[i][0]);
                var b = Flat(line[i][1]);
                int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - FadeDepth) / T), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + FadeDepth) / T);
                int y0 = Mathf.FloorToInt((-Mathf.Max(a.y, b.y) - FadeDepth) / T), y1 = Mathf.FloorToInt((-Mathf.Min(a.y, b.y) + FadeDepth) / T);
                for (var y = y0; y <= y1; y++)
                    for (var x = x0; x <= x1; x++)
                    {
                        var k = Key(x, y);
                        if (!near.TryGetValue(k, out var list)) near[k] = list = new List<int>();
                        list.Add(i);
                    }
            }
            // The bends by the tile their curve's corner lies in; a corner turning in lies on a tile out of the band, lit
            // at the height of a tile of the band beside it.
            var bent = new Dictionary<int, List<Bend>>();
            var heights = new Dictionary<int, float>(floors);
            foreach (var bend in bends)
            {
                var k = TileKey(bend.At + (bend.A + bend.B) * (bend.R / 2));
                if (!bent.TryGetValue(k, out var list)) bent[k] = list = new List<Bend>();
                list.Add(bend);
                if (heights.ContainsKey(k)) continue;
                int tx = Mathf.FloorToInt((bend.At + (bend.A + bend.B) * (bend.R / 2)).x / T), ty = Mathf.FloorToInt(-(bend.At + (bend.A + bend.B) * (bend.R / 2)).y / T);
                foreach (var d in Grid.Dirs4)
                    if (floors.TryGetValue(Key(tx + d.X, ty + d.Y), out var h))
                    {
                        heights[k] = h;
                        break;
                    }
            }
            var light = new float[GlowSteps + 1, GlowSteps + 1];
            var at = new Vector3[GlowSteps + 1, GlowSteps + 1];
            foreach (var pair in heights)
            {
                if (!near.TryGetValue(pair.Key, out var pieces)) continue;
                var inBand = floors.ContainsKey(pair.Key);
                bent.TryGetValue(pair.Key, out var curves);
                if (!inBand && curves == null) continue;
                int tx = pair.Key / 1024, ty = pair.Key % 1024;
                // (Key packs x * 1024 + y; the reach never reaches a negative tile.)
                var lit = false;
                for (var j = 0; j <= GlowSteps; j++)
                    for (var i = 0; i <= GlowSteps; i++)
                    {
                        var p = new Vector2(T * (tx + (float)i / GlowSteps), -T * (ty + (float)j / GlowSteps));
                        at[i, j] = new Vector3(p.x, pair.Value + 0.03f * T, p.y);
                        var inside = inBand;
                        if (curves != null)
                            foreach (var c in curves)
                            {
                                // Within the square a bend cut off (or took in) and on its corner's side of the curve
                                // (a quadratic one with its control on the corner: √u + √w = 1).
                                var u = Vector2.Dot(p - c.At, c.A) / c.R;
                                var w = Vector2.Dot(p - c.At, c.B) / c.R;
                                if (u >= 0 && w >= 0 && u <= 1 && w <= 1 && Mathf.Sqrt(u) + Mathf.Sqrt(w) < 1) inside = !inside;
                            }
                        var v = 0f;
                        if (inside)
                        {
                            var d = float.MaxValue;
                            foreach (var n in pieces) d = Mathf.Min(d, DistanceTo(p, Flat(line[n][0]), Flat(line[n][1])));
                            var t = 1 - d / FadeDepth;
                            if (t > 0) v = t * t;
                        }
                        light[i, j] = v;
                        lit |= v > 0;
                    }
                if (!lit) continue;
                for (var j = 0; j < GlowSteps; j++)
                    for (var i = 0; i < GlowSteps; i++)
                    {
                        float a = light[i, j], b = light[i + 1, j], c = light[i + 1, j + 1], d = light[i, j + 1];
                        if (a <= 0 && b <= 0 && c <= 0 && d <= 0) continue;
                        into.Quad(at[i, j], at[i + 1, j], at[i + 1, j + 1], at[i, j + 1], Alpha(color, alpha * a), Alpha(color, alpha * b), Alpha(color, alpha * c), Alpha(color, alpha * d));
                    }
            }
        }

        static float DistanceTo(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var len = ab.sqrMagnitude;
            var t = len > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// An area of tiles bordered as the reach is: one glow fading in from its edge (Glow), and the edge with its corners
        /// rounded over a dark stroke, all into `into` in `color` (an enemy's field of view: Ana's #14, Arturo 29/10).
        /// </summary>
        internal static void Border(MeshBuilder into, GameState s, IList<Pos> tiles, Color32 color, float fill, float edge)
        {
            var floors = new Dictionary<int, float>();
            foreach (var t in tiles) floors[Key(t.X, t.Y)] = Board.TileToWorld(s, t).y;
            var segs = new List<Vector3[]>();
            foreach (var p in tiles)
            {
                var floor = floors[Key(p.X, p.Y)];
                for (var side = 0; side < 4; side++)
                {
                    var d = Grid.Dirs4[side];
                    if (floors.ContainsKey(Key(p.X + d.X, p.Y + d.Y))) continue;
                    Side(p.X, p.Y, side, floor + 0.06f * T, out var a, out var b);
                    segs.Add(new[] { a, b });
                }
            }
            var bends = new List<Bend>();
            var rounded = RoundCorners(segs, CornerR, bends);
            // The glow as the reach's, one along all the border (#16); the line over its dark stroke, as before.
            Glow(into, floors, rounded, bends, color, fill);
            foreach (var seg in rounded) into.Line(seg[0], seg[1], EdgeShadePx, Shade);
            foreach (var seg in rounded) into.Line(seg[0], seg[1], EdgePx, Alpha(color, edge));
        }

        /// <summary>
        /// A border's segments with their right-angled corners rounded (overlays.ts roundCorners): where exactly two meet
        /// square, each stops `r` short of the corner and a curve joins them (a quadratic one, its control the corner,
        /// in four steps). Straight joins, loose ends and crossings stay as they are.
        /// </summary>
        static List<Vector3[]> RoundCorners(List<Vector3[]> segs, float r, List<Bend> bends = null)
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
                bends?.Add(new Bend { At = Flat(v), A = Flat(da / la), B = Flat(db / lb), R = r });
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
                // Up a flight of stairs it rides the treads, not the floor under them.
                var prev = Board.WayPoint(s, from, 0.08f * T);
                foreach (var p in path)
                {
                    var at = Board.WayPoint(s, p, 0.08f * T);
                    build.Line(prev, at, PathPx, color);
                    under.Line(prev, at, PathShadePx, Shade);
                    prev = at;
                }
            }
            way.sharedMesh = build.Build(way.sharedMesh);
            wayShade.sharedMesh = under.Build(wayShade.sharedMesh);
        }

        /// <summary>The pointer's ring on `tile` in `color` (PlanRing, WalkRing, DashRing, NoticedRing); null: none.</summary>
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

        /// <summary>The lit ring round the soldier pointed at from the list, at its feet (null: none; Arturo, 31/10 #11).</summary>
        public void ShowPointed(Vector3? feet)
        {
            pointed.gameObject.SetActive(feet != null);
            if (feet != null) pointed.localPosition = feet.Value + Vector3.up * (0.04f * T);
        }

        /// <summary>The ring round the picked soldier, at its feet (null: none).</summary>
        public void ShowPicked(Vector3? feet)
        {
            picked.gameObject.SetActive(feet != null);
            if (feet != null) picked.localPosition = feet.Value + Vector3.up * (0.035f * T);
        }

        /// <summary>
        /// A red ring under each enemy in sight, at its feet as it walks (unitViews.ts: the squad's own go without, 18/10
        /// #28; null or none, none).
        /// </summary>
        public void ShowEnemies(IList<Vector3> feet)
        {
            var n = feet?.Count ?? 0;
            while (enemyRings.Count < n)
            {
                var ring = Layer("Enemigo", 3102, false, false);
                ring.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = RingTexture();
                ring.sharedMesh = enemyRing;
                enemyRings.Add(ring.transform);
            }
            for (var i = 0; i < enemyRings.Count; i++)
            {
                var on = i < n;
                if (enemyRings[i].gameObject.activeSelf != on) enemyRings[i].gameObject.SetActive(on);
                if (on) enemyRings[i].localPosition = feet[i] + Vector3.up * (0.03f * T);
            }
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

        /// <summary>
        /// A flame, as the eye is drawn (supersampled, white for its tint): a drop with its tip up, leaning a little, and a
        /// smaller one cut out of its foot.
        /// </summary>
        internal static Texture2D FlameTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            // In 16 × 16 units, y up: a round foot (cx, foot, r) narrowing to a tip at `tip`.
            bool Drop(float x, float y, float cx, float foot, float r, float tip, float lean)
            {
                if ((x - cx) * (x - cx) + (y - foot) * (y - foot) <= r * r) return true;
                if (y < foot || y > tip) return false;
                var k = (y - foot) / (tip - foot);
                var half = r * Mathf.Pow(1 - k, 1.4f);
                return Mathf.Abs(x - (cx + lean * k * k)) <= half;
            }
            for (var py = 0; py < size; py++)
            for (var px = 0; px < size; px++)
            {
                var a = 0f;
                for (var sy = 0; sy < 2; sy++)
                for (var sx = 0; sx < 2; sx++)
                {
                    var x = (px + 0.25f + sx * 0.5f) / size * 16;
                    var y = (py + 0.25f + sy * 0.5f) / size * 16;
                    if (Drop(x, y, 8, 5.6f, 4.6f, 15.2f, 1.2f) && !Drop(x, y, 8.2f, 4.4f, 2.1f, 9.6f, 0.6f)) a += 0.25f;
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
