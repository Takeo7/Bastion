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
        /// <summary>
        /// The player's own slot (UI's MySlot), for whose it is to play (Following): null, the selected soldier's owner's,
        /// which is wrong with another's soldier selected or with none.
        /// </summary>
        public int? slot;
        public bool roofs;
        public string closeAttacker, closeTarget;
        /// <summary>
        /// Who acts in the event being played, and on whom (UI's EventPlayer.Acting, the 3D review's E-08, 10/11): while
        /// it lasts, the storey the actor stands on is the one its building shows, over the selected soldier's and the one
        /// picked by hand, so that each action is seen whole.
        /// </summary>
        public string acting, actingTarget;
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
        /// <summary>The state's Seq when the squad's sight was worked out: the view is changed in place, event by event.</summary>
        int seenSeq;
        /// <summary>A frame waited for UnitViews' sight of the state as it is now (SightOf), rather than working it out again.</summary>
        bool waited;
        int[] seenInside = Array.Empty<int>();
        /// <summary>
        /// The buildings someone of the squad sees into from beside a window now (N1, B: the engine's Buildings.SidePeeks):
        /// their roof off and the walls facing the camera down, as for the selected soldier by a wall, so that those rows
        /// are seen from the game's camera; the rest of their inside stays the fog's, not found.
        /// </summary>
        int[] peeked = Array.Empty<int>();
        /// <summary>On a map that is one building (H9): the nodes found (Buildings.FoundTiles), and the buildings with any; null and none on any other.</summary>
        HashSet<int> found;
        bool[] known = Array.Empty<bool>();
        bool[] visible;
        /// <summary>
        /// Things on a storey of a building (furniture, a sign, what stands on a roof), turned off while it hides them; a
        /// sign with the way it faces out (0.10.1's frame; zero for the rest).
        /// </summary>
        readonly List<(GameObject thing, int building, int level, bool sign, Vector2 outward)> tracked = new List<(GameObject, int, int, bool, Vector2)>();
        /// <summary>What hangs on a wall (a clinic's cross, sign or blood: medicalDetails.ts medicalWallPatch), with the way its wall looks out.</summary>
        readonly List<(GameObject thing, int building, int level, Vector2 outward)> fixtures = new List<(GameObject, int, int, Vector2)>();
        /// <summary>Something tracked since the last Upload: it is shown or hidden at once, not at the cut's next change.</summary>
        bool fresh;
        /// <summary>The way the camera looks, on the ground (0.10.1's frame: x east, y south; _CutView).</summary>
        Vector2 lookFlat;
        /// <summary>lookFlat as the things were last shown or hidden by it (Things).</summary>
        Vector2 thingsLook;
        /// <summary>
        /// The one who last acted while it wasn't the player's to play (the partner's with the command, the enemy's turn):
        /// its building goes on showing its storey between its actions, and goes back to the selected soldier's only once
        /// the command is the player's again (Arturo, 28/11, cuadro 189: «Seguirle, pero sin parpadeo»; with one of ours on
        /// each storey, the building went to the partner's storey and back at each action of theirs). Null, nobody.
        /// </summary>
        public string Following { get; private set; }
        /// <summary>The storey in view when Following was last set: one picked by hand since lets go of it.</summary>
        int? followLevel;

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
            Following = null;
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
            found = null;
            known = new bool[size];
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

        /// <summary>
        /// `thing` stands on storey `level` of building `building` (a sign: it goes while its building is cut on its storey;
        /// one facing `outward`, 0.10.1's frame, also while its roof is off and its back is to the camera: the 3D review's
        /// E-14, 10/11, a supermarket's «MARKET» read back to front from inside).
        /// </summary>
        public void Track(GameObject thing, int building, int level, bool sign = false, Vector2 outward = default)
        {
            if (thing == null || building < 0 || building >= n) return;
            tracked.Add((thing, building, level, sign, outward));
            fresh = true;
        }

        /// <summary>`thing` lies on place p (a molotov's fire on an upper floor or a roof, N4): tracked as what stands on that storey of the building there, if any.</summary>
        public void TrackAt(GameObject thing, Pos p)
        {
            if (plan == null || p == null || p.Z <= 0) return;
            Track(thing, plan.At(new Vec2 { X = p.X, Y = p.Y }), p.Z);
        }

        /// <summary>
        /// `thing` hangs on a wall of storey `level` of building `building` (medicalWallPatch): it goes with the wall when it
        /// drops, an inner wall's whenever its building's walls drop, an outer one's (`outward` the way that wall looks out
        /// of the building, 0.10.1's frame) while it looks towards the camera, or when every wall drops.
        /// </summary>
        public void TrackFixture(GameObject thing, int building, int level, Vector2 outward)
        {
            if (thing == null || building < 0 || building >= n) return;
            fixtures.Add((thing, building, level, outward));
            fresh = true;
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
            // Whose it is to play: the player's own (the squad's turn, and the command theirs or nobody's), or another's.
            // (By the state on screen, changed event by event: the one the orders go by is ahead of it, and would let go of the
            // one followed before their last actions are seen.)
            int? me = focus.slot ?? selectedUnit?.Owner;
            bool mine = s.ActiveTeam == "xcom" && (s.Command == null || me == null || s.Command == me);
            if (focus.acting != null)
            {
                Following = mine ? null : focus.acting;
                followLevel = focus.level;
            }
            else if (Following != null && (mine || focus.level != followLevel || !s.Units.Any(u => u.Id == Following && u.Alive))) Following = null;
            // (Between the actions of the one followed, as while it acts: no target, and the selected soldier's own gone by.)
            if (focus.acting == null && Following != null)
            {
                focus.acting = Following;
                focus.actingTarget = null;
            }
            // The storey the player looks at: the active one, else the selected soldier's.
            int view = focus.level ?? selectedUnit?.Pos.Z ?? 0;
            // Chosen by hand (not the selected soldier's own): it wins over where the soldier stands.
            bool chosen = focus.level != null && focus.level != (selectedUnit?.Pos.Z ?? 0);
            // (UnitViews.Sync works it out after each event the player applies: a frame's wait for it saves doing it twice.)
            var shared = s != seenFor || s.Seq != seenSeq ? UnitViews.Current?.SightOf(s) : null;
            if ((s != seenFor || s.Seq != seenSeq) && shared == null && !waited && UnitViews.Current != null) waited = true;
            else if (s != seenFor || s.Seq != seenSeq)
            {
                waited = false;
                seenFor = s;
                seenSeq = s.Seq;
                visible = shared ?? Los.TeamVisibility(s, "xcom");
                var seen = new HashSet<int>();
                int area = s.Width * s.Height;
                // (On a map that is one building, H9, only what is found: its roof seen from the strip, or a room not found
                // seen into through a loading bay's opening, took the shed's roof off over an inside all dark.)
                found = Bastion.Engine.Buildings.ByRoom(s) ? Bastion.Engine.Buildings.FoundTiles(s) : null;
                for (int k = 0; k < visible.Length; k++)
                    if (visible[k] && (found == null || found.Contains(k))) seen.Add(plan.At(new Vec2 { X = k % area % s.Width, Y = k % area / s.Width }));
                seen.Remove(-1);
                seenInside = seen.ToArray();
                peeked = Bastion.Engine.Buildings.SidePeeks(s).Select(i => plan.At(new Vec2 { X = i % s.Width, Y = i / s.Width })).Where(b => b >= 0).Distinct().ToArray();
                known = new bool[n];
                if (found != null)
                    foreach (int k in found)
                        if (k % area < plan.Owner.Length && plan.Owner[k % area] >= 0 && plan.Owner[k % area] < n) known[plan.Owner[k % area]] = true;
            }
            bool close = focus.Close;
            bool roofs = focus.roofs || close;
            // A map that is one building, found room by room (H7, the hospital).
            bool inside = Bastion.Engine.Buildings.ByRoom(s);
            if (!roofs) foreach (int b in seenInside) Mark(b, 0);
            if (!roofs)
                foreach (int b in peeked)
                {
                    Mark(b, 0);
                    Mark(b, 1);
                    Aim(b, 0, false);
                }
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
            // (On a map that is one building, H9, not over what is not found yet: the pointer over the shed from the strip
            // took its front down onto an inside all dark. Over what is found, or out of doors, as ever.)
            if (focus.hover != null && !roofs && (found == null || plan.At(new Vec2 { X = focus.hover.X, Y = focus.hover.Y }) < 0 || found.Contains((focus.hover.Z * s.Height + focus.hover.Y) * s.Width + focus.hover.X)))
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
                bool acting = focus.acting != null;
                bool keen = close ? u.Id == focus.closeAttacker || u.Id == focus.closeTarget
                    : acting ? u.Id == focus.acting || u.Id == focus.actingTarget : u.Id == focus.selected || u.Id == focus.target;
                if (close && !keen) continue;
                var (tx, ty, point) = Locate(u);
                if (!close) Within(tx, ty, u.Pos.Z, acting ? u.Id == focus.acting : u.Id == focus.selected && !chosen);
                Behind(point, plan.At(new Vec2 { X = tx, Y = ty }), keen);
                if (u.Id != focus.selected || close || acting) continue;
                // The selected soldier by a building's wall (about to go in, or in cover against it). Not on a map that is one
                // building (H7) from out of doors: the hospital's whole front, three storeys, went down onto its unfound
                // inside, all dark, as a soldier walked along it; in the way, Behind does. In its doorway (a door is no
                // tile inside for At, but the building's: Owner) it is this that opens it.
                if (inside && (u.Pos.X < 0 || u.Pos.Y < 0 || u.Pos.X >= s.Width || u.Pos.Y >= s.Height || plan.Owner[u.Pos.Y * s.Width + u.Pos.X] < 0)) continue;
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
                        // (A building of a map that is one building, H9: only once something of it is found.)
                        if (found != null && (b.index >= known.Length || !known[b.index])) continue;
                        float dx = Mathf.Max(Mathf.Max(b.x0 - gx, 0), gx - b.x1 - 1);
                        float dz = Mathf.Max(Mathf.Max(b.y0 - gz, 0), gz - b.y1 - 1);
                        if (dx * dx + dz * dz < ZoomRadius * ZoomRadius) Mark(b.index, 0);
                    }
                }
            }
            // A storey chosen by hand: every building with storeys above it opens down to it. On a map that is one building
            // (H7), its walls facing the camera down too, as with someone in it: else, looking at a storey nobody stood on,
            // the hospital's near outer wall, a storey high, hid the near rooms.
            if (chosen && !roofs)
                foreach (var b in plan.Buildings)
                    if (b.leveled && b.storeys > view)
                    {
                        Mark(b.index, 0);
                        if (inside) Mark(b.index, 1);
                    }
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
            if (changed || fresh) Upload();
            // (The camera turned: what shows or hides by the way it looks.)
            else if (Vector2.Distance(lookFlat, thingsLook) > 0.02f) Things();
        }

        /// <summary>The fades into the cut map (smoothstepped), the storey each shows in its alpha; what they hide off.</summary>
        void Upload()
        {
            fresh = false;
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
            Things();
        }

        /// <summary>What the cut hides shown or hidden, as it is now and as the camera looks.</summary>
        void Things()
        {
            thingsLook = lookFlat;
            for (int k = tracked.Count - 1; k >= 0; k--)
            {
                var (thing, b, level, sign, outward) = tracked[k];
                if (thing == null)
                {
                    tracked.RemoveAt(k);
                    continue;
                }
                bool gone = level > shown[b] ? now[b * 3] > 0.5f : sign && level == shown[b] && Mathf.Max(now[b * 3 + 1], now[b * 3 + 2]) > 0.5f;
                // A sign seen from behind, its roof off (E-14): two thresholds, so that one side on doesn't flicker.
                if (!gone && sign && level == shown[b] && outward.sqrMagnitude > 0.01f && now[b * 3] > 0.5f)
                    gone = Vector2.Dot(outward, lookFlat) > (thing.activeSelf ? 0.25f : 0.15f);
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
