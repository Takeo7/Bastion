using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The marks of what is armed (overlays.ts showAim, showBlast, showRange, setSpots; UI's): the target's ring, red for an
    /// enemy, green for an ally, beating with a red light under it once an enemy is picked to shoot (Arturo, 23/10 #2: no
    /// line then), a line to an ally; a throw's area (orange, red on the squad's own, grey where it can't go), its centre
    /// and the walls it breaks; how far a throw reaches; the furniture or doors within the hand's reach. And a noise
    /// spreading from where it sounded (overlays.ts noise), which goes by itself.
    /// </summary>
    public sealed class AimOverlay : MonoBehaviour
    {
        static readonly Color32 Enemy = MoveOverlay.Hex(0xff4a4a);
        static readonly Color32 EnemyLine = MoveOverlay.Hex(0xff5a5a);
        static readonly Color32 Friend = MoveOverlay.Hex(0x4dff9a);
        static readonly Color32 Blast = MoveOverlay.Hex(0xff8a2a);
        static readonly Color32 Off = MoveOverlay.Hex(0x8a939c);
        static readonly Color32 Breach = MoveOverlay.Hex(0xffd08a);
        static readonly Color32 Reach = MoveOverlay.Hex(0xffb070);
        /// <summary>Furniture or a door within reach, and the one the hand would act on (overlays.ts SPOT_COLORS).</summary>
        static readonly Color32 SpotOpen = MoveOverlay.Hex(0xffd25a);
        static readonly Color32 SpotPick = MoveOverlay.Hex(0xffffff);

        const float T = Board.Tile;

        readonly MeshBuilder build = new MeshBuilder();
        MeshFilter ring;
        Material ringMaterial;
        MeshFilter glow;
        Material glowMaterial;
        MeshFilter line;
        MeshFilter area;
        MeshFilter centreRing;
        MeshFilter walls;
        MeshFilter range;
        MeshFilter spots;
        MeshFilter vision;
        bool marked;

        /// <summary>A noise's disc or ring spreading: its radius (m), when it starts, how long, how bright at most.</summary>
        sealed class Spread
        {
            public Transform Shape;
            public Material Material;
            public float Radius;
            public float? Start;
            public float Delay;
            public float Duration;
            public float Peak;
        }

        readonly List<Spread> noises = new List<Spread>();

        /// <summary>A ping's beam or ring (fx.ts ping): when it was made, how long it lasts, how much it grows, how bright.</summary>
        sealed class Pulse
        {
            public Transform Shape;
            public Material Material;
            public float Born;
            public float Life;
            public float Grow;
            public float Peak;
        }

        readonly List<Pulse> pulses = new List<Pulse>();
        Mesh beam;
        Mesh pingRing;
        MeshFilter plans;
        Mesh disc;
        Mesh thick;
        Mesh thin;

        void Awake()
        {
            // The target's ring (0.5–0.62 of a tile), tinted by its material; the light under a picked one, a disc that fades out.
            ring = Layer("Blanco", 3106, false);
            build.Clear();
            build.Arc(Vector3.zero, 0.5f * T, 0.62f * T, 0, Mathf.PI * 2, 40, MoveOverlay.Hex(0xffffff));
            ring.sharedMesh = build.Build(null);
            ringMaterial = ring.GetComponent<MeshRenderer>().sharedMaterial;
            ring.gameObject.SetActive(false);

            glow = Layer("Blanco (luz)", 3099, false);
            build.Clear();
            for (var k = 0; k < 40; k++)
            {
                var a0 = k * Mathf.PI * 2 / 40;
                var a1 = (k + 1) * Mathf.PI * 2 / 40;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * (0.75f * T);
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * (0.75f * T);
                // White in the middle, half at 55 % of the way out, gone at the edge (overlays.ts glowTexture).
                var mid = new Color32(0xff, 0xff, 0xff, 0x73);
                var none = new Color32(0xff, 0xff, 0xff, 0);
                build.Quad(Vector3.zero, Vector3.zero, d1 * 0.55f, d0 * 0.55f, MoveOverlay.Hex(0xffffff), MoveOverlay.Hex(0xffffff), mid, mid);
                build.Quad(d0 * 0.55f, d1 * 0.55f, d1, d0, mid, mid, none, none);
            }
            glow.sharedMesh = build.Build(null);
            glowMaterial = glow.GetComponent<MeshRenderer>().sharedMaterial;
            glowMaterial.color = MoveOverlay.Hex(0xff4a3a);
            glow.gameObject.SetActive(false);

            line = Layer("Línea al blanco", 3107, true);
            area = Layer("Área", 3100, false);
            centreRing = Layer("Área (centro)", 3106, false);
            walls = Layer("Área (paredes)", 3108, true);
            range = Layer("Alcance de lanzar", 3104, true);
            spots = Layer("A mano", 3108, true);
            vision = Layer("Lo que ve", 3096, false);
            plans = Layer("Planes", 3109, true);
        }

        MeshFilter Layer(string name, int queue, bool overAll)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = MeshBuilder.Material(queue, overAll);
            return go.AddComponent<MeshFilter>();
        }

        /// <summary>
        /// A noise (docs/FASE2.md) of `radius` tiles at `tile`: a faint disc and a ring (two for a loud one) spreading fast
        /// and slowing to its radius as they fade; a bigger noise takes longer.
        /// </summary>
        public void Noise(GameState s, Pos tile, float radius, Color32 color)
        {
            if (radius <= 0) return;
            if (disc == null)
            {
                build.Clear();
                for (var i = 0; i < 48; i++)
                {
                    var a0 = i * Mathf.PI * 2 / 48;
                    var a1 = (i + 1) * Mathf.PI * 2 / 48;
                    build.Quad(Vector3.zero, Vector3.zero, new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)), new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), MoveOverlay.Hex(0xffffff));
                }
                disc = build.Build(null);
                thick = Circle(2.5f);
                thin = Circle(1.5f);
            }
            var at = Board.TileToWorld(s, tile, 0.08f * T);
            var duration = 0.8f + radius * 0.08f;
            void Add(Mesh mesh, int queue, float delay, float peak)
            {
                var go = new GameObject("Ruido");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = at;
                go.transform.localScale = Vector3.one * 0.05f;
                var material = MeshBuilder.Material(queue, true);
                material.color = new Color32(color.r, color.g, color.b, 0);
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                noises.Add(new Spread { Shape = go.transform, Material = material, Radius = radius * T, Delay = delay, Duration = duration, Peak = peak });
            }
            Add(disc, 3098, 0, 0.045f);
            for (var i = 0; i < (radius >= 10 ? 2 : 1); i++) Add(i > 0 ? thin : thick, 3099, i * 0.35f, i > 0 ? 0.45f : 0.8f);
        }

        Mesh Circle(float px)
        {
            build.Clear();
            for (var i = 0; i < 72; i++)
            {
                var a0 = i * Mathf.PI * 2 / 72;
                var a1 = (i + 1) * Mathf.PI * 2 / 72;
                build.Line(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)), px, MoveOverlay.Hex(0xffffff));
            }
            return build.Build(null);
        }

        /// <summary>
        /// A ping (fx.ts ping; playtest 18/10 #7): a beam of light going up past the top of the screen, seen through
        /// whatever stands in front, and three rings spreading one after another at its foot; red on an enemy.
        /// </summary>
        public void Ping(Vector3 at, Color32 colour)
        {
            if (beam == null)
            {
                build.Clear();
                var r = 0.06f * T;
                for (var i = 0; i < 12; i++)
                {
                    var a0 = i * Mathf.PI * 2 / 12;
                    var a1 = (i + 1) * Mathf.PI * 2 / 12;
                    var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r;
                    var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                    build.Quad(d0, d1, d1 + Vector3.up * (84 * T), d0 + Vector3.up * (84 * T), MoveOverlay.Hex(0xffffff));
                }
                beam = build.Build(null);
                build.Clear();
                build.Arc(Vector3.zero, 0.42f * T, 0.55f * T, 0, Mathf.PI * 2, 32, MoveOverlay.Hex(0xffffff));
                pingRing = build.Build(null);
            }
            Pulse Make(Mesh mesh, Vector3 where, float delay, float life, float grow, float peak, bool overAll)
            {
                var go = new GameObject("Señal");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = where;
                var material = MeshBuilder.Material(3111, overAll);
                material.color = new Color32(colour.r, colour.g, colour.b, 0);
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var p = new Pulse { Shape = go.transform, Material = material, Born = Time.time + delay, Life = life, Grow = grow, Peak = peak };
                pulses.Add(p);
                return p;
            }
            Make(beam, at, 0, 3.5f, 0, 0.5f, true);
            for (var i = 0; i < 3; i++) Make(pingRing, at + Vector3.up * (0.06f * T), i * 0.45f, 1.1f, 1.6f, 0.9f, false);
        }

        /// <summary>
        /// Moves planned without the command (overlays.ts setPlans; UX-REVISION-2 #35): a thin line along the path each would
        /// take now and a ring on where it ends, in the planner's colour; one that no longer gets there, its ring alone.
        /// </summary>
        public void ShowPlans(GameState s, IList<(Pos from, IList<Pos> path, Color32 colour)> list)
        {
            build.Clear();
            if (s != null && list != null)
                foreach (var (from, path, colour) in list)
                {
                    var c = new Color32(colour.r, colour.g, colour.b, 0xe6);
                    var prev = Board.TileToWorld(s, from, 0.1f * T);
                    foreach (var p in path)
                    {
                        var at = Board.TileToWorld(s, p, 0.1f * T);
                        build.Line(prev, at, 1.8f, c);
                        prev = at;
                    }
                    var end = path.Count > 0 ? path[path.Count - 1] : from;
                    build.Arc(Board.TileToWorld(s, end, 0.05f * T), 0.33f * T, 0.41f * T, 0, Mathf.PI * 2, 36, c);
                }
            plans.sharedMesh = build.Build(plans.sharedMesh);
        }

        void Update()
        {
            var now = Time.time;
            for (var i = pulses.Count - 1; i >= 0; i--)
            {
                var p = pulses[i];
                var t = (now - p.Born) / p.Life;
                if (t < 0) continue;
                if (t >= 1)
                {
                    Destroy(p.Shape.gameObject);
                    Destroy(p.Material);
                    pulses.RemoveAt(i);
                    continue;
                }
                if (p.Grow > 0) p.Shape.localScale = Vector3.one * (1 + p.Grow * t);
                var tint = p.Material.color;
                tint.a = p.Peak * (1 - t);
                p.Material.color = tint;
            }
            // Each noise spreads fast and slows to its radius as it fades.
            var time = Time.time;
            for (var i = noises.Count - 1; i >= 0; i--)
            {
                var n = noises[i];
                n.Start ??= time + n.Delay;
                var t = (time - n.Start.Value) / n.Duration;
                if (t < 0) continue;
                if (t >= 1)
                {
                    Destroy(n.Shape.gameObject);
                    Destroy(n.Material);
                    noises.RemoveAt(i);
                    continue;
                }
                var r = Mathf.Max(0.05f, n.Radius * (1 - Mathf.Pow(1 - t, 3)));
                n.Shape.localScale = new Vector3(r, 1, r);
                var tint = n.Material.color;
                tint.a = n.Peak * (t < 0.15f ? t / 0.15f : (1 - t) / 0.85f);
                n.Material.color = tint;
            }

            // A picked target's ring beats slowly, and its light with it (#2).
            var k = 0.5f + 0.5f * Mathf.Sin(Time.time * 4);
            var c = ringMaterial.color;
            c.a = marked ? 0.75f + 0.25f * k : 0.95f;
            ringMaterial.color = c;
            var g = glowMaterial.color;
            g.a = 0.35f + 0.3f * k;
            glowMaterial.color = g;
        }

        public void Clear()
        {
            ShowAim(null, null, null);
            ShowBlast(null, null, null, false, null, null);
            ShowRange(null, null, 0);
            ShowSpots(null, null, null);
        }

        /// <summary>
        /// The target's ring on `to`; `marked`, an enemy picked to shoot or strike: beating, a light under it, no line. Else
        /// a line from `from` at chest height, green to an ally (`friendly`).
        /// </summary>
        public void ShowAim(GameState s, Pos from, Pos to, bool friendly = false, bool marked = false)
        {
            this.marked = marked;
            ring.gameObject.SetActive(s != null && to != null);
            glow.gameObject.SetActive(s != null && to != null && marked);
            build.Clear();
            if (s != null && to != null)
            {
                ring.transform.localPosition = Board.TileToWorld(s, to, 0.05f * T);
                ring.transform.localScale = Vector3.one * (marked ? 1.15f : 1);
                ringMaterial.color = friendly ? Friend : Enemy;
                glow.transform.localPosition = Board.TileToWorld(s, to, 0.04f * T);
                if (from != null && !marked) build.Line(Board.TileToWorld(s, from, 1.1f * T), Board.TileToWorld(s, to, 1.1f * T), 3, friendly ? Friend : EnemyLine);
            }
            line.sharedMesh = build.Build(line.sharedMesh);
        }

        /// <summary>
        /// A throw's area round `centre`: its tiles orange (red where one of the squad stands: `danger`), all grey if it can't
        /// be thrown there (`valid`); a ring on the centre; a frame halfway up each wall it breaks (docs/FASE7.md §4).
        /// </summary>
        public void ShowBlast(GameState s, Pos centre, IList<Pos> tiles, bool valid, IList<Pos> danger, IList<Pos> breached)
        {
            build.Clear();
            var mid = new MeshBuilder();
            var frames = new MeshBuilder();
            if (s != null && centre != null)
            {
                var inset = 0.05f * T;
                foreach (var t in tiles)
                {
                    var hot = danger != null && Contains(danger, t);
                    var c = hot ? Enemy : valid ? Blast : Off;
                    var a = Board.Corner(t.X, t.Y, Board.TileToWorld(s, t).y + 0.03f * T);
                    build.Flat(a.x + inset, a.z - inset, a.x + T - inset, a.z - T + inset, a.y, new Color32(c.r, c.g, c.b, 0x66));
                }
                mid.Arc(Board.TileToWorld(s, centre, 0.05f * T), 0.2f * T, 0.32f * T, 0, Mathf.PI * 2, 32, valid ? Blast : Enemy);
                foreach (var w in breached ?? new List<Pos>())
                    Square(frames, Board.TileToWorld(s, w, 1f * T), 0.3f * T, 0.4f * T, new Color32(Breach.r, Breach.g, Breach.b, (byte)(valid ? 0xf2 : 0x66)));
            }
            area.sharedMesh = build.Build(area.sharedMesh);
            centreRing.sharedMesh = mid.Build(centreRing.sharedMesh);
            walls.sharedMesh = frames.Build(walls.sharedMesh);
        }

        /// <summary>How far a throw reaches from the thrower: a thin circle on the ground (tiles of `radius`); none at 0.</summary>
        public void ShowRange(GameState s, Pos from, double radius)
        {
            build.Clear();
            if (s != null && from != null && radius > 0)
            {
                var c = Board.TileToWorld(s, from, 0.06f * T);
                var r = (float)radius * T;
                for (var i = 0; i < 72; i++)
                {
                    var a0 = i * Mathf.PI * 2 / 72;
                    var a1 = (i + 1) * Mathf.PI * 2 / 72;
                    build.Line(c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r, 2, Reach);
                }
            }
            range.sharedMesh = build.Build(range.sharedMesh);
        }

        /// <summary>
        /// One enemy's field of view (overlays.ts setVision; Arturo, 17/10): the tiles it sees in its cone and its line of
        /// sight washed faintly in sight's red, those where it would notice a hidden soldier more (no edge line: 18/10 #30).
        /// Null, none. Apart from what is armed (Clear leaves it).
        /// </summary>
        public void ShowVision(GameState s, IList<Pos> seen, IList<Pos> noticed)
        {
            build.Clear();
            if (s != null && seen != null)
            {
                var red = MoveOverlay.Hex(0xf0503c);
                void Wash(IList<Pos> tiles, byte alpha, float lift)
                {
                    foreach (var t in tiles)
                    {
                        var a = Board.Corner(t.X, t.Y, Board.TileToWorld(s, t).y + lift * T);
                        build.Flat(a.x, a.z, a.x + T, a.z - T, a.y, new Color32(red.r, red.g, red.b, alpha));
                    }
                }
                Wash(seen, 0x29, 0.015f);
                Wash(noticed, 0x38, 0.016f);
            }
            vision.sharedMesh = build.Build(vision.sharedMesh);
        }

        /// <summary>The furniture or doors the hand reaches: a square frame on each, white on the one it would act on (`pick`).</summary>
        public void ShowSpots(GameState s, IList<Pos> list, Pos pick)
        {
            build.Clear();
            if (s != null && list != null)
                foreach (var p in list)
                {
                    var on = pick != null && p.X == pick.X && p.Y == pick.Y && p.Z == pick.Z;
                    Square(build, Board.TileToWorld(s, p, 0.04f * T), 0.62f * T, 0.74f * T, on ? SpotPick : new Color32(SpotOpen.r, SpotOpen.g, SpotOpen.b, 0xf2));
                }
            spots.sharedMesh = build.Build(spots.sharedMesh);
        }

        /// <summary>A flat square frame round `c` (a four-sided ring turned 45°, radii `inner`–`outer` to its corners).</summary>
        static void Square(MeshBuilder into, Vector3 c, float inner, float outer, Color32 color)
        {
            var i = inner / Mathf.Sqrt(2);
            var o = outer / Mathf.Sqrt(2);
            into.Quad(c + new Vector3(-o, 0, o), c + new Vector3(o, 0, o), c + new Vector3(i, 0, i), c + new Vector3(-i, 0, i), color);
            into.Quad(c + new Vector3(o, 0, o), c + new Vector3(o, 0, -o), c + new Vector3(i, 0, -i), c + new Vector3(i, 0, i), color);
            into.Quad(c + new Vector3(o, 0, -o), c + new Vector3(-o, 0, -o), c + new Vector3(-i, 0, -i), c + new Vector3(i, 0, -i), color);
            into.Quad(c + new Vector3(-o, 0, -o), c + new Vector3(-o, 0, o), c + new Vector3(-i, 0, i), c + new Vector3(-i, 0, -i), color);
        }

        static bool Contains(IList<Pos> list, Pos p)
        {
            foreach (var q in list)
                if (q.X == p.X && q.Y == p.Y) return true;
            return false;
        }
    }
}
