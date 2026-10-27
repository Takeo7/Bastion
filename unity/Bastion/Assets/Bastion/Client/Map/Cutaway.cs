using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// What the player is looking at (buildings.ts Focus), from UI's controller: the selected soldier, the target, the
    /// cursor's place (its storey in Z), the active storey, the roofs on (the camera drawn back), the shot over a shoulder.
    /// </summary>
    public struct CutFocus
    {
        public string selected;
        public string target;
        public Pos hover;
        public int? level;
        public bool roofs;
        public string closeAttacker, closeTarget;
        /// <summary>The camera over a shoulder (rig.closeUp), aiming too, whoever is in the shot: game.ts passes `close` then even with neither.</summary>
        public bool close;

        public bool Close => close || closeAttacker != null || closeTarget != null;
    }

    /// <summary>
    /// The cutaway of every building (0.10.1's buildings.ts Cutaway): which open up this frame and every fade moved
    /// towards it, in a texel each of the global _CutMap (r roof gone, g walls facing the camera cut, b every wall cut, a
    /// the storey in view: what stands above it fades with r, its walls drop with g and b), read by Bastion/Building.
    /// The same rules in 0.10.1's tiles: the cursor's building and storey first, then everyone's (ours, and the enemies in
    /// sight) with what stands between the camera and them, the selected soldier by a wall, the roofs round where the
    /// camera looks when it is close, a storey chosen by hand. What is on a storey hidden so (figures, furniture, a sign)
    /// is turned off (Hides, the props registered with Track).
    /// </summary>
    public sealed class Cutaway : MonoBehaviour, IShotStep
    {
        public static Cutaway Current { get; private set; }

        /// <summary>The storey in view, as a cut map's alpha (this much a storey).</summary>
        const int FloorCode = 64;
        /// <summary>Seconds a building keeps its cutaway after the reason for it goes, and a fade takes.</summary>
        const float Hold = 0.6f, Fade = 0.3f;
        /// <summary>Closer than this (tiles from the camera to the ground it looks at), roofs near that point fade.</summary>
        const float ZoomNear = 15, ZoomRadius = 8;

        /// <summary>What the player is looking at (UI's controller sets it).</summary>
        public CutFocus Focus;

        static readonly int CutMap = Shader.PropertyToID("_CutMap");
        static readonly int CutCount = Shader.PropertyToID("_CutCount");
        static readonly int CutView = Shader.PropertyToID("_CutView");

        BuildingPlan plan;
        int n;
        float[] now = Array.Empty<float>(), hold = Array.Empty<float>();
        byte[] marks = Array.Empty<byte>(), locked = Array.Empty<byte>(), shown = Array.Empty<byte>();
        int[] want = Array.Empty<int>();
        Bounds[] boxes = Array.Empty<Bounds>();
        Texture2D map;
        Color32[] data = Array.Empty<Color32>();
        GameState seenFor;
        int[] seenInside = Array.Empty<int>();
        bool[] visible;
        /// <summary>Things on a storey of a building (furniture, a sign, what stands on a roof), turned off while it hides them.</summary>
        readonly List<(GameObject thing, int building, int level, bool sign)> tracked = new List<(GameObject, int, int, bool)>();
        /// <summary>What hangs on a wall (a clinic's cross, sign or blood: medicalDetails.ts medicalWallPatch), with the way its wall looks out.</summary>
        readonly List<(GameObject thing, int building, int level, Vector2 outward)> fixtures = new List<(GameObject, int, int, Vector2)>();
        /// <summary>The way the camera looks, on the ground (0.10.1's frame: x east, y south; _CutView).</summary>
        Vector2 lookFlat;

        void OnEnable() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Shader.SetGlobalFloat(CutCount, 0);
            if (map != null) DestroyImmediate(map);
        }

        /// <summary>A new map's buildings (measured once their roofs are built: Building.top), nothing cut.</summary>
        public void Reset(BuildingPlan buildings)
        {
            Current = this;
            plan = buildings;
            n = plan?.Buildings.Count ?? 0;
            int size = Mathf.Max(1, n);
            now = new float[size * 3];
            hold = new float[size * 3];
            marks = new byte[size * 3];
            want = new int[size];
            locked = new byte[size];
            shown = new byte[size];
            data = new Color32[size];
            tracked.Clear();
            fixtures.Clear();
            seenFor = null;
            boxes = plan == null ? Array.Empty<Bounds>() : plan.Buildings.Select(b =>
            {
                var box = new Bounds();
                box.SetMinMax(new Vector3(b.x0 + 0.4f, b.@base, b.y0 + 0.4f), new Vector3(b.x1 + 0.6f, b.top, b.y1 + 0.6f));
                return box;
            }).ToArray();
            if (map != null) DestroyImmediate(map);
            map = new Texture2D(size, 1, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Corte" };
            Upload();
        }

        /// <summary>`thing` stands on storey `level` of building `building` (a sign: it goes while its building is cut on its storey).</summary>
        public void Track(GameObject thing, int building, int level, bool sign = false)
        {
            if (thing != null && building >= 0 && building < n) tracked.Add((thing, building, level, sign));
        }

        /// <summary>
        /// `thing` hangs on a wall of storey `level` of building `building` (medicalWallPatch): it goes with the wall when it
        /// drops, an inner wall's whenever its building's walls drop, an outer one's (`outward` the way that wall looks out
        /// of the building, 0.10.1's frame) while it looks towards the camera, or when every wall drops.
        /// </summary>
        public void TrackFixture(GameObject thing, int building, int level, Vector2 outward)
        {
            if (thing != null && building >= 0 && building < n) fixtures.Add((thing, building, level, outward));
        }

        /// <summary>
        /// Whether place p is hidden by the cutaway: on a storey above the one its building shows, while that building is
        /// cut (what stands there, a unit included, isn't to be drawn).
        /// </summary>
        public bool Hides(Pos p)
        {
            if (plan == null || p == null) return false;
            int b = plan.At(new Vec2 { X = p.X, Y = p.Y });
            return b >= 0 && p.Z > shown[b] && now[b * 3] > 0.5f;
        }

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        public void ShotStep(float dt) => Step(dt);

        /// <summary>0.10.1's world (tiles: x east, y up, z south) from Unity's.</summary>
        static Vector3 Tiles(Vector3 world) => new Vector3(world.x / MapSpace.Tile, world.y / MapSpace.Tile, -world.z / MapSpace.Tile);

        void Step(float dt)
        {
            if (n == 0 || plan == null) return;
            var s = UnitViews.Current?.State ?? plan.S;
            var camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            Array.Clear(marks, 0, marks.Length);
            for (int k = 0; k < n; k++) want[k] = -1;
            Array.Clear(locked, 0, locked.Length);
            void Mark(int b, int what)
            {
                if (b >= 0) marks[b * 3 + what] = 1;
            }
            // Building b is opened down to storey `level` (the highest anyone in it stands on; `keen`, the focus's, wins).
            void Aim(int b, int level, bool keen)
            {
                if (b < 0 || locked[b] != 0) return;
                if (keen)
                {
                    want[b] = level;
                    locked[b] = 1;
                }
                else want[b] = Math.Max(want[b], level);
            }
            var focus = Focus;
            var selectedUnit = focus.selected != null ? s.Units.FirstOrDefault(u => u.Id == focus.selected) : null;
            // The storey the player looks at: the active one, else the selected soldier's.
            int view = focus.level ?? selectedUnit?.Pos.Z ?? 0;
            // Chosen by hand (not the selected soldier's own): it wins over where the soldier stands.
            bool chosen = focus.level != null && focus.level != (selectedUnit?.Pos.Z ?? 0);
            if (s != seenFor)
            {
                seenFor = s;
                visible = Los.TeamVisibility(s, "xcom");
                var seen = new HashSet<int>();
                int area = s.Width * s.Height;
                for (int k = 0; k < visible.Length; k++)
                    if (visible[k]) seen.Add(plan.At(new Vec2 { X = k % area % s.Width, Y = k % area / s.Width }));
                seen.Remove(-1);
                seenInside = seen.ToArray();
            }
            bool close = focus.Close;
            bool roofs = focus.roofs || close;
            if (!roofs) foreach (int b in seenInside) Mark(b, 0);
            // Opens building b for something inside it on storey `level`: what stands above off, the walls facing the camera down.
            void Within(int x, int y, int level, bool keen = false)
            {
                int b = plan.At(new Vec2 { X = x, Y = y });
                // Nobody goes into a block of the outline: it opens only when it stands in the way (behind).
                if (b >= 0 && plan.Buildings[b].frame) return;
                Mark(b, 0);
                Mark(b, 1);
                Aim(b, level, keen);
            }
            Vector3 eye = Vector3.zero, look = Vector3.forward;
            if (camera != null)
            {
                eye = Tiles(camera.transform.position);
                var forward = camera.transform.forward;
                look = new Vector3(forward.x, forward.y, -forward.z);
                float flat = Mathf.Sqrt(look.x * look.x + look.z * look.z);
                if (flat < 1e-6f) flat = 1;
                Shader.SetGlobalVector(CutView, new Vector4(look.x / flat, look.z / flat, 0, 0));
                lookFlat = new Vector2(look.x / flat, look.z / flat);
            }
            // Every building between the camera and the point `at` (not the one `own` it is in) loses its roof; with
            // `all`, all its walls drop too.
            void Behind(Vector3 at, int own, bool all)
            {
                if (camera == null) return;
                var dir = at - eye;
                float far = dir.magnitude;
                if (far < 1e-6f) return;
                dir /= far;
                for (int b = 0; b < n; b++)
                {
                    if (b == own || (close && boxes[b].Contains(eye))) continue;
                    if (Hit(boxes[b], eye, dir, out float t) && t < far - 0.3f)
                    {
                        Mark(b, 0);
                        if (all) Mark(b, 2);
                    }
                }
            }
            // Where a unit is now (mid-walk too): its tile and its chest.
            (int x, int y, Vector3 point) Locate(Unit u)
            {
                var views = UnitViews.Current;
                Vector3 point;
                if (views != null && views.Has(u.Id)) point = Tiles(views.Feet(u.Id));
                else point = new Vector3(u.Pos.X + 0.5f, MapSpace.FloorAt(s, u.Pos) / MapSpace.Tile, u.Pos.Y + 0.5f);
                int tx = Mathf.FloorToInt(point.x), ty = Mathf.FloorToInt(point.z);
                point.y += 0.9f;
                return (tx, ty, point);
            }
            // The cursor first: the storey under it is the one the player looks at in that building.
            if (focus.hover != null && !roofs)
            {
                var p = focus.hover;
                Within(p.X, p.Y, p.Z, true);
                Behind(new Vector3(p.X + 0.5f, MapSpace.FloorAt(s, p) / MapSpace.Tile + 0.2f, p.Y + 0.5f), plan.At(new Vec2 { X = p.X, Y = p.Y }), true);
            }
            foreach (var u in s.Units)
            {
                if (!u.Alive) continue;
                bool ours = u.Team == "xcom";
                int node = (u.Pos.Z * s.Height + u.Pos.Y) * s.Width + u.Pos.X;
                if (!ours && !(visible != null && node >= 0 && node < visible.Length && visible[node])) continue;
                bool keen = close ? u.Id == focus.closeAttacker || u.Id == focus.closeTarget : u.Id == focus.selected || u.Id == focus.target;
                if (close && !keen) continue;
                var (tx, ty, point) = Locate(u);
                if (!close) Within(tx, ty, u.Pos.Z, u.Id == focus.selected && !chosen);
                Behind(point, plan.At(new Vec2 { X = tx, Y = ty }), keen);
                if (u.Id != focus.selected || close) continue;
                // The selected soldier by a building's wall (about to go in, or in cover against it).
                foreach (var b in plan.Buildings)
                {
                    if (b.frame) continue;
                    int dx = Math.Max(Math.Max(b.x0 - tx, 0), tx - b.x1);
                    int dy = Math.Max(Math.Max(b.y0 - ty, 0), ty - b.y1);
                    if (dx + dy <= 1)
                    {
                        Mark(b.index, 0);
                        Mark(b.index, 1);
                        Aim(b.index, u.Pos.Z, false);
                    }
                }
            }
            // Zoomed in: the roofs round where the camera looks.
            if (camera != null && !roofs && look.y < -0.05f)
            {
                float t = -eye.y / look.y;
                if (t < ZoomNear)
                {
                    float gx = eye.x + look.x * t, gz = eye.z + look.z * t;
                    foreach (var b in plan.Buildings)
                    {
                        float dx = Mathf.Max(Mathf.Max(b.x0 - gx, 0), gx - b.x1 - 1);
                        float dz = Mathf.Max(Mathf.Max(b.y0 - gz, 0), gz - b.y1 - 1);
                        if (dx * dx + dz * dz < ZoomRadius * ZoomRadius) Mark(b.index, 0);
                    }
                }
            }
            // A storey chosen by hand: every building with storeys above it opens down to it.
            if (chosen && !roofs) foreach (var b in plan.Buildings) if (b.leveled && b.storeys > view) Mark(b.index, 0);
            // The storey each opened building shows: the cursor's or the selected soldier's in theirs; else the one chosen
            // by hand; else the highest anyone in it stands on; else the one in view. A building whose storeys aren't real
            // shows its ground floor. Kept while it fades back.
            bool changed = false;
            foreach (var b in plan.Buildings)
            {
                int k = b.index;
                if (marks[k * 3] == 0 && marks[k * 3 + 1] == 0 && marks[k * 3 + 2] == 0) continue;
                int asked = locked[k] != 0 || (!chosen && want[k] >= 0) ? want[k] : view;
                int floor = b.leveled ? Math.Max(0, Math.Min(b.storeys, asked)) : 0;
                if (floor != shown[k])
                {
                    shown[k] = (byte)floor;
                    changed = true;
                }
            }
            // Walls opened right through also lose their roof; held a moment, then faded.
            float stepBy = dt / Fade;
            for (int k = 0; k < n * 3; k++)
            {
                hold[k] = marks[k] != 0 ? Hold : Mathf.Max(0, hold[k] - dt);
                float goal = hold[k] > 0 ? 1 : 0;
                float v = now[k];
                float next = v < goal ? Mathf.Min(goal, v + stepBy) : Mathf.Max(goal, v - stepBy);
                if (next != v)
                {
                    now[k] = next;
                    changed = true;
                }
            }
            if (changed) Upload();
        }

        /// <summary>The fades into the cut map (smoothstepped), the storey each shows in its alpha; what they hide off.</summary>
        void Upload()
        {
            for (int b = 0; b < n; b++)
            {
                byte Eased(float v) => (byte)Mathf.RoundToInt(v * v * (3 - 2 * v) * 255);
                data[b] = new Color32(Eased(now[b * 3]), Eased(now[b * 3 + 1]), Eased(now[b * 3 + 2]), (byte)(shown[b] * FloorCode));
            }
            if (map == null) return;
            map.SetPixels32(data);
            map.Apply(false);
            Shader.SetGlobalTexture(CutMap, map);
            Shader.SetGlobalFloat(CutCount, Mathf.Max(1, n));
            for (int k = tracked.Count - 1; k >= 0; k--)
            {
                var (thing, b, level, sign) = tracked[k];
                if (thing == null)
                {
                    tracked.RemoveAt(k);
                    continue;
                }
                bool gone = level > shown[b] ? now[b * 3] > 0.5f : sign && level == shown[b] && Mathf.Max(now[b * 3 + 1], now[b * 3 + 2]) > 0.5f;
                if (thing.activeSelf == gone) thing.SetActive(!gone);
            }
            for (int k = fixtures.Count - 1; k >= 0; k--)
            {
                var (thing, b, level, outward) = fixtures[k];
                if (thing == null)
                {
                    fixtures.RemoveAt(k);
                    continue;
                }
                float Eased(float v) => v * v * (3 - 2 * v);
                // medicalWallPatch: an inner wall's whenever the walls facing the camera drop; an outer one's as its wall turns to it.
                float facing = outward.sqrMagnitude < 0.01f ? 1 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.15f, 0.25f, -Vector2.Dot(outward, lookFlat)));
                bool gone = level > shown[b] ? now[b * 3] > 0.5f : level == shown[b] && Mathf.Max(Eased(now[b * 3 + 1]) * facing, Eased(now[b * 3 + 2])) > 0.25f;
                if (thing.activeSelf == gone) thing.SetActive(!gone);
            }
        }

        /// <summary>three.js's Ray.intersectBox: how far along the ray it meets the box (from inside, where it leaves it).</summary>
        static bool Hit(Bounds box, Vector3 origin, Vector3 dir, out float t)
        {
            t = 0;
            float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity;
            for (int a = 0; a < 3; a++)
            {
                float inv = 1 / dir[a];
                float t0 = (box.min[a] - origin[a]) * inv, t1 = (box.max[a] - origin[a]) * inv;
                if (inv < 0) (t0, t1) = (t1, t0);
                if (t0 > tmin) tmin = t0;
                if (t1 < tmax) tmax = t1;
                if (tmin > tmax) return false;
            }
            if (tmax < 0) return false;
            t = tmin >= 0 ? tmin : tmax;
            return true;
        }
    }
}
