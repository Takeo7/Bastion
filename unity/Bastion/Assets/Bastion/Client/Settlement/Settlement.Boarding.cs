using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bastion.Client.Map;
using Bastion.Client.Units;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The squad leaving (0.10.1's clearingArt.ts depart and board, without its climbing in: Arturo, 31/10, his #1): the
    /// row, its guns slung, runs to La Lata's doors, one after another, and each fades out beside its doorway as it gets
    /// there, as one boarding it in a battle does (each doorway's doors opening as its first comes and shutting behind its
    /// last; into the 4×4 each by a door of its own); then La Lata goes, the camera turning after it as it does.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>
        /// How fast they run (tiles/s), ease in and out of it (s), set off one after another (s), and turn while they run
        /// (rad/s: 0.10.1's 7 turned 40° between two frames at 10 fps, A-11; under 25° now).
        /// </summary>
        const float BoardSpeed = 2.9f, BoardEase = 0.35f, BoardStagger = 0.12f, BoardTurn = 4.3f;
        /// <summary>
        /// Where they fade at a doorway (tiles): out from it; how long its doors take to open or shut (seconds).
        /// </summary>
        const float BoardOut = 0.55f, DoorOpen = 0.35f;
        /// <summary>
        /// One behind another at a doorway (the 3D review's A-11, 10/11: the second and third stood half a metre to a metre
        /// either side of it, against the bus's side, and faded there; those of a doorway ran glued together): each further
        /// one BoardQueue tiles further out and BoardSide to one side or the other; a rear doorway (the van's, the lorry's)
        /// two such lines BoardLanes apart. Each waits, then steps up a place in StepSeconds once the one before has left it
        /// (from the doorway's own place, once BoardFaded of its fade is past), and fades at the doorway. Those of a doorway
        /// set off BoardQueueDelay apart besides the row's stagger; no doorway takes more than its share of the row. In the
        /// last BoardEase of its run each turns to its doorway, no faster than BoardTurnEnd (rad/s), walking.
        /// </summary>
        const float BoardQueue = 0.45f, BoardSide = 0.12f, BoardLanes = 0.3f, StepSeconds = 0.3f, BoardFaded = 0.6f, BoardQueueDelay = 0.3f, BoardTurnEnd = 4;
        /// <summary>
        /// The camera while La Lata goes (Arturo, 31/10: a short turn that watches it go, 2 to 3 s): turned after it no further
        /// than this from where it looked (it drives on out of the frame, not the camera after it into the pines: BUS_FOLLOW,
        /// 18° in 0.10.1), and how quickly it catches up (1 − e^(−dt × this)).
        /// </summary>
        static readonly float BusFollow = 30 * Mathf.Deg2Rad;
        const float FollowEase = 3;

        /// <summary>Whether the squad is getting in or La Lata going (Leave): the row and the camera are its meanwhile.</summary>
        public bool Leaving { get; private set; }

        /// <summary>
        /// (A lab's: SettlementLab.Escuadra, BASTION_MEDIR=giro.) Each one's turn, place and fade every frame of the boarding,
        /// and how far from its doorway's middle, along La Lata, it starts fading (the 3D review's A-11, 10/11).
        /// </summary>
        public static bool BoardMeasured;

        /// <summary>
        /// The squad in the row gets into La Lata and it drives off (screen.ts on launching: clearingArt depart); done once it
        /// is gone (at once if there is nobody and nothing to go). The row then stands again only when it is given again.
        /// </summary>
        public async Awaitable Leave(CancellationToken ct = default)
        {
            if (Lata == null || Leaving) return;
            Leaving = true;
            try
            {
                if (standers.Count > 0 && Lata.Shown)
                {
                    float began = Time.time;
                    await Board(ct);
                    Debug.Log($"[settlement] {standers.Count} aboard La Lata ({Lata.Vehicle}) in {Time.time - began:0.00} s");
                }
                // Given again (the debrief, the next mission's), the row is made again.
                lineupKey = "";
                away = true;
                float off = Time.time;
                var going = Lata.DepartAsync(busWay, !busRoute, ct);
                await Follow(ct);
                await going;
                Debug.Log($"[settlement] La Lata gone in {Time.time - off:0.00} s");
            }
            finally
            {
                Leaving = false;
            }
        }

        /// <summary>
        /// The camera while La Lata goes, as long as it takes (Arturo, 31/10, his #1): where it stands, turned after La Lata
        /// as it goes, from where it looked, by as much as La Lata has gone round it (no further than BusFollow); the pines it
        /// turns onto before the row's place out of the way, as the row's shot has them.
        /// </summary>
        async Awaitable Follow(CancellationToken ct)
        {
            var camera = Camera;
            if (camera == null || Lata == null) return;
            var eye = camera.transform.position;
            var ahead = camera.transform.forward;
            var up = Vector3.up * MapSpace.Tile;
            var first = (Lata.transform.position + up - eye).normalized;
            var look = ahead;
            while (Lata != null && Lata.Moving && !ct.IsCancellationRequested)
            {
                var now = (Lata.transform.position + up - eye).normalized;
                var goal = Vector3.RotateTowards(ahead, Quaternion.FromToRotation(first, now) * ahead, BusFollow, 0);
                look = Vector3.Slerp(look, goal, 1 - Mathf.Exp(-FollowEase * Time.deltaTime));
                camera.transform.rotation = Quaternion.LookRotation(look);
                ClearBefore(camera);
                await Awaitable.NextFrameAsync(ct);
            }
        }

        sealed class Boarder
        {
            public Stander s;
            public List<Vector3> run;
            public List<float> lengths;
            public float total, running, delay;
            public Vector3 foot, door;
            public int way, index;
            public bool started, there;
            /// <summary>
            /// Its places in its line at the doorway, the doorway's own first (slots[0]); when it steps up from each to the next
            /// (steps[j]: from slots[j + 1] to slots[j]), and when it starts fading at the doorway.
            /// </summary>
            public List<Vector3> slots;
            public float[] steps;
            public float fades;
            public string motion;
            /// <summary>Its doorway's middle (its leaves' together) and the way along La Lata there (BoardMeasured).</summary>
            public Vector3 doorway, side;
            public float fade = 1;
            /// <summary>Where it starts from, the way out from its doorway, and whether that is at La Lata's back.</summary>
            public Vector3 from, normal;
            public bool back;
        }

        /// <summary>
        /// The row in (clearingArt.ts board, without the climbing in), in 0.10.1's tiles: each runs to beside its doorway,
        /// round La Lata's corner first if it is on another side, turns to it and fades out over UnitViews.BoardSeconds.
        /// Done once all are gone.
        /// </summary>
        async Awaitable Board(CancellationToken ct)
        {
            var doors = Lata.Doorways();
            if (doors.Count == 0) return;
            var standing = standers.Where(s => s.root != null && s.root.gameObject.activeInHierarchy).ToList();
            if (standing.Count == 0) return;
            foreach (var s in standing) Sling(s);
            var centre = Lata.At;
            var dir = new Vector3(Mathf.Sin(Lata.Heading), 0, Mathf.Cos(Lata.Heading));
            var across = new Vector3(dir.z, 0, -dir.x);
            float half = Lata.Length / 2, width = Lata.Width;
            bool car = Lata.Vehicle == 0;
            var froms = standing.Select(s => Vector3.Scale(Tiles(s.root.position), new Vector3(1, 0, 1))).ToList();
            // Into the 4×4 each by a door of its own, by the shortest runs all told; else by the doorway nearest with room (a
            // double doorway's two leaves one way in; none more than its share: A-11), the nearest of the row first.
            int WayOf(int d) => car ? d : doors.FindIndex(e => Vector3.Distance(e, doors[d]) < 0.7f);
            List<int> seat;
            if (car) seat = BestDoors(froms, doors);
            else
            {
                int share = Mathf.CeilToInt(standing.Count / (float)Enumerable.Range(0, doors.Count).Select(WayOf).Distinct().Count());
                var load = new Dictionary<int, int>();
                seat = new List<int>(new int[standing.Count]);
                foreach (int i in Enumerable.Range(0, standing.Count).OrderBy(i => doors.Min(d => Vector3.Distance(d, froms[i]))))
                {
                    int pick = Enumerable.Range(0, doors.Count).Where(d => (load.TryGetValue(WayOf(d), out int l) ? l : 0) < share).OrderBy(d => Vector3.Distance(doors[d], froms[i])).First();
                    load[WayOf(pick)] = (load.TryGetValue(WayOf(pick), out int had) ? had : 0) + 1;
                    seat[i] = pick;
                }
            }
            // Each one's place in its doorway's line: the nearest to it first.
            var rank = new int[standing.Count];
            foreach (var g in Enumerable.Range(0, standing.Count).GroupBy(i => WayOf(seat[i])))
            {
                int r = 0;
                foreach (int i in g.OrderBy(i => Vector3.Distance(froms[i], doors[seat[i]]))) rank[i] = r++;
            }
            var plans = new List<Boarder>();
            for (int i = 0; i < standing.Count; i++)
            {
                var door = doors[seat[i]];
                int way = WayOf(seat[i]);
                int nth = rank[i];
                var offset = door - centre;
                // A door on its side (the bus's, the 4×4's) or at its back (the van's, the lorry's), and out from it.
                bool backDoor = Mathf.Abs(Vector3.Dot(offset, dir)) > half - 0.35f;
                var normal = backDoor ? dir * (Vector3.Dot(offset, dir) < 0 ? -1 : 1) : across * (Vector3.Dot(offset, across) < 0 ? -1 : 1);
                var along = new Vector3(normal.z, 0, -normal.x);
                // Its line (a rear doorway's two, side by side) and its place in it: the doorway's own, then each one out
                // behind it, a little to one side or the other.
                int lanes = backDoor ? 2 : 1, lane = nth % lanes, place = nth / lanes;
                float laneAt = lanes == 1 ? 0 : (lane == 0 ? -BoardLanes : BoardLanes);
                var slots = Enumerable.Range(0, place + 1).Select(q => door + normal * (BoardOut + BoardQueue * q) + along * (laneAt + (q == 0 ? 0 : (q % 2 == 1 ? 1 : -1) * BoardSide))).ToList();
                var leaves = doors.Where(d => Vector3.Distance(d, door) < 0.7f).ToList();
                plans.Add(new Boarder { s = standing[i], delay = i * BoardStagger + place * BoardQueueDelay, door = door, way = way * 2 + lane, index = i, doorway = leaves.Aggregate(Vector3.zero, (a, b) => a + b) / leaves.Count, side = along, slots = slots, from = froms[i], normal = normal, back = backDoor });
            }
            // The run from where one stands to `foot` by its doorway: round La Lata's corner first if it is on another side.
            List<Vector3> Route(Boarder b, Vector3 foot)
            {
                var from = b.from;
                var path = new List<Vector3> { from };
                float endOf = Vector3.Dot(from - centre, dir) < 0 ? -1 : 1, sideOf = Vector3.Dot(from - centre, across) < 0 ? -1 : 1;
                Vector3 Corner(float end, float side) => centre + dir * (end * (half + 0.45f)) + across * (side * (width / 2 + 0.6f));
                if (b.back)
                {
                    // From beside it, not out past its back: round that corner.
                    float end = Vector3.Dot(b.normal, dir);
                    if (Vector3.Dot(from - centre, dir) * end < half) path.Add(Corner(end, sideOf));
                }
                else if (Vector3.Dot(from - centre, b.normal) < width / 2)
                {
                    // Not out on its door's side: round its nearer end (from out past it, its one corner on that side).
                    float side = Vector3.Dot(b.normal, across);
                    if (Mathf.Abs(Vector3.Dot(from - centre, dir)) <= half) path.Add(Corner(endOf, -side));
                    path.Add(Corner(endOf, side));
                }
                path.Add(foot);
                return path;
            }
            // Each line in turn (A-11): one runs to the nearest place of its line that is free by the time it gets there,
            // steps up into each next once the one before has left it (the doorway's own once that one is BoardFaded
            // faded), and fades at the doorway.
            foreach (var line in plans.GroupBy(p => p.way))
            {
                Boarder ahead = null;
                foreach (var p in line.OrderBy(p => p.slots.Count))
                {
                    // When the one ahead leaves place j (null ahead: never there).
                    float Left(int j) => ahead == null ? 0 : j == 0 ? ahead.fades + BoardFaded * UnitViews.BoardSeconds : j - 1 < ahead.steps.Length ? ahead.steps[j - 1] : 0;
                    for (int start = 0; start < p.slots.Count; start++)
                    {
                        var path = Route(p, p.slots[start]);
                        var lengths = new List<float>();
                        for (int k = 1; k < path.Count; k++) lengths.Add(Vector3.Distance(path[k], path[k - 1]));
                        float total = lengths.Sum(), running = total / BoardSpeed + BoardEase;
                        // (Not before it could have stepped in: running straight in, it came on the one ahead still fading.)
                        if (start < p.slots.Count - 1 && p.delay + running < Left(start) + StepSeconds) continue;
                        (p.run, p.lengths, p.total, p.running, p.foot) = (path, lengths, total, running, p.slots[start]);
                        p.steps = new float[start];
                        break;
                    }
                    float ready = p.delay + p.running;
                    for (int j = p.steps.Length - 1; j >= 0; j--)
                    {
                        p.steps[j] = Mathf.Max(ready, Left(j));
                        ready = p.steps[j] + StepSeconds;
                    }
                    p.fades = ready;
                    ahead = p;
                }
            }
            // Each doorway's doors: opening as its first gets there, shutting once its last is gone (the last ones as La Lata
            // goes: LaLata.Depart shuts them first).
            var ways = plans.GroupBy(p => p.way / 2).Select(g => (way: g.Key, open: Mathf.Max(0, g.Min(p => p.delay + p.running) - DoorOpen), shut: g.Max(p => p.fades) + UnitViews.BoardSeconds)).ToList();
            float seconds = plans.Max(p => p.fades) + UnitViews.BoardSeconds + 0.05f;
            moved = MoveSeconds;
            float now = 0;
            while (now < seconds && !ct.IsCancellationRequested)
            {
                now = Mathf.Min(seconds, now + Time.deltaTime);
                foreach (var (way, open, shut) in ways)
                {
                    float k = now < shut ? Mathf.Clamp01((now - open) / DoorOpen) : 1 - Mathf.Clamp01((now - shut) / DoorOpen);
                    Lata.SetWay(way, Mathf.SmoothStep(0, 1, k), !car);
                }
                foreach (var p in plans) Step(p, now);
                if (BoardMeasured)
                    foreach (var p in plans)
                        if (p.s.root != null && p.s.root.gameObject.activeSelf && now > p.delay)
                        {
                            var at = Tiles(p.s.root.position);
                            Debug.Log(System.FormattableString.Invariant($"[subir] {now:0.000} {p.index} yaw {p.s.yaw * Mathf.Rad2Deg:0.0} at {at.x:0.000},{at.z:0.000} fade {p.fade:0.00}"));
                        }
                await Awaitable.NextFrameAsync(ct);
            }
            foreach (var p in plans) if (p.s.root != null) p.s.root.gameObject.SetActive(false);
        }

        /// <summary>
        /// One of the row at `now` of the boarding: running to its place in its doorway's line (picking up speed, turning to
        /// where it goes as it goes, and in the run's last BoardEase to its doorway, walking: A-11), waiting there turned to
        /// it, stepping up the line, then fading out at the doorway.
        /// </summary>
        void Step(Boarder p, float now)
        {
            var s = p.s;
            if (s.root == null) return;
            float since = now - p.delay;
            if (since <= 0) return;
            float doorYaw(Vector3 from) => Mathf.Atan2(p.door.x - from.x, p.door.z - from.z);
            if (since < p.running)
            {
                if (!p.started)
                {
                    p.started = true;
                    Moving(p, "run", 1);
                }
                s.root.position = World(Along(p.run, p.lengths, p.total, p.running, since, out var from, out var to));
                // Its last stretch: turned to the doorway, slowing to a walk.
                if (since > p.running - BoardEase)
                {
                    Moving(p, "walk");
                    s.yaw = TurnToward(s.yaw, doorYaw(p.foot), BoardTurnEnd);
                }
                else s.yaw = TurnToward(s.yaw, Mathf.Atan2(to.x - from.x, to.z - from.z));
                return;
            }
            // Up the line: waiting in a place, or stepping on to the next.
            int at = p.steps.Length;
            for (int j = p.steps.Length - 1; j >= 0; j--)
            {
                if (now < p.steps[j]) break;
                if (now < p.steps[j] + StepSeconds)
                {
                    Moving(p, "walk");
                    s.root.position = World(Vector3.Lerp(p.slots[j + 1], p.slots[j], Mathf.SmoothStep(0, 1, (now - p.steps[j]) / StepSeconds)));
                    s.yaw = TurnToward(s.yaw, doorYaw(p.slots[j]), BoardTurnEnd);
                    return;
                }
                at = j;
            }
            if (now < p.fades)
            {
                Moving(p, "idle");
                s.root.position = World(p.slots[at]);
                s.yaw = TurnToward(s.yaw, doorYaw(p.slots[at]), BoardTurnEnd);
                return;
            }
            if (!p.there)
            {
                p.there = true;
                Moving(p, "idle");
                s.root.position = World(p.slots[0]);
                if (BoardMeasured) Debug.Log(System.FormattableString.Invariant($"[subir] {p.index} fades {Vector3.Dot(p.slots[0] - p.doorway, p.side):0.00} along from its doorway's middle, {Vector3.Distance(Vector3.Scale(p.slots[0] - p.doorway, new Vector3(1, 0, 1)), Vector3.zero):0.00} from it"));
            }
            s.yaw = TurnToward(s.yaw, doorYaw(p.slots[0]), BoardTurnEnd);
            p.fade = 1 - (now - p.fades) / UnitViews.BoardSeconds;
            FadeOut(s, p.fade);
        }

        /// <summary>Its clip `motion` (at `speed`), unless it is on it already.</summary>
        static void Moving(Boarder p, string motion, float speed = 1)
        {
            if (p.motion == motion) return;
            p.motion = motion;
            p.s.anim?.Motion(motion, speed);
        }

        /// <summary>
        /// One of the row with `k` of it still drawn (1 all, 0 none), dithered as a figure fading in a battle (UnitViews:
        /// Bastion/FigureFade in its body's materials' place, from the colour map its block gives it, casting no shadow);
        /// what it carries gone from halfway; at none, out of sight. For good: once aboard, the row is made again (Leave).
        /// </summary>
        static void FadeOut(Stander s, float k)
        {
            if (s.root == null) return;
            fadeBlock ??= new MaterialPropertyBlock();
            if (s.fading == null)
            {
                s.fading = s.root.GetComponentsInChildren<SkinnedMeshRenderer>().ToList();
                foreach (var r in s.fading)
                {
                    r.sharedMaterials = r.sharedMaterials.Select(UnitViews.FadeCopy).ToArray();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.GetPropertyBlock(fadeBlock);
                    if (fadeBlock.HasTexture("_ColorMap")) fadeBlock.SetTexture("_BaseMap", fadeBlock.GetTexture("_ColorMap"));
                    r.SetPropertyBlock(fadeBlock);
                }
            }
            foreach (var r in s.fading)
            {
                if (r == null) continue;
                r.GetPropertyBlock(fadeBlock);
                fadeBlock.SetFloat("_Fade", Mathf.Clamp01(k));
                r.SetPropertyBlock(fadeBlock);
            }
            foreach (var r in s.root.GetComponentsInChildren<MeshRenderer>()) r.enabled = k >= 0.5f;
            if (k <= 0) s.root.gameObject.SetActive(false);
        }

        static MaterialPropertyBlock fadeBlock;

        /// <summary>`yaw` (0.10.1's, radians) turned towards `goal` as fast as they turn (BOARD_TURN; `speed` rad/s if said), the short way round.</summary>
        static float TurnToward(float yaw, float goal, float speed = BoardTurn) =>
            Mathf.MoveTowardsAngle(yaw * Mathf.Rad2Deg, goal * Mathf.Rad2Deg, speed * Mathf.Rad2Deg * Time.deltaTime) * Mathf.Deg2Rad;

        /// <summary>
        /// Where a run of `total` tiles along `path` is `since` seconds into its `running`: picking up speed at the start,
        /// slowing at the end (BOARD_EASE either end, its top speed kept between); and the leg it is on (from `a` to `b`).
        /// </summary>
        static Vector3 Along(List<Vector3> path, List<float> lengths, float total, float running, float since, out Vector3 a, out Vector3 b)
        {
            float ease = Mathf.Min(BoardEase, running / 2);
            float top = total / Mathf.Max(0.01f, running - ease);
            float end = running - since;
            float left = since < ease ? 0.5f * top * since * since / ease : end < ease ? total - 0.5f * top * end * end / ease : 0.5f * top * ease + top * (since - ease);
            a = path[0];
            b = path[path.Count - 1];
            for (int k = 0; k < lengths.Count; k++)
            {
                a = path[k];
                b = path[k + 1];
                if (left <= lengths[k] || k == lengths.Count - 1) return Vector3.Lerp(a, b, Mathf.Min(1, left / (lengths[k] > 0 ? lengths[k] : 1)));
                left -= lengths[k];
            }
            return b;
        }

        /// <summary>For each of those at `from`, a door of `doors` of its own, by the shortest runs all told (any more than doors share the nearest).</summary>
        static List<int> BestDoors(List<Vector3> from, List<Vector3> doors)
        {
            int n = Mathf.Min(from.Count, doors.Count);
            var best = new List<int>();
            float bestCost = float.PositiveInfinity;
            var taken = new List<int>();
            void Pick(int k, float cost)
            {
                if (cost >= bestCost) return;
                if (k == n)
                {
                    bestCost = cost;
                    best = taken.ToList();
                    return;
                }
                for (int d = 0; d < doors.Count; d++)
                {
                    if (taken.Contains(d)) continue;
                    taken.Add(d);
                    Pick(k + 1, cost + Vector3.Distance(from[k], doors[d]));
                    taken.RemoveAt(taken.Count - 1);
                }
            }
            Pick(0, 0);
            for (int k = n; k < from.Count; k++)
            {
                int nearest = 0;
                for (int j = 1; j < doors.Count; j++) if (Vector3.Distance(doors[j], from[k]) < Vector3.Distance(doors[nearest], from[k])) nearest = j;
                best.Add(nearest);
            }
            return best;
        }

        /// <summary>
        /// The gun slung on the back to get in (syntyFigures.ts sling, hangStows): on the chest's bone, its middle behind it
        /// and a little over it, from the left hip up to the right shoulder blade, its top out from the back, as far out as
        /// it clears the body (outOfBody: here the body's hindmost skin along it, as it stands); and moving as one unarmed
        /// (the run, the walk and the standing of Synty's), nothing held.
        /// </summary>
        void Sling(Stander s)
        {
            if (s.slung || s.animator == null || !s.animator.isHuman) return;
            s.slung = true;
            var catalog = FigureCatalog.Load();
            if (s.anim != null && catalog != null)
            {
                s.anim.Rest(null);
                foreach (string m in new[] { "idle", "walk", "run", "crouchWalk", "crouchIdle" }) s.anim.SetMotion(m, catalog.Clip(Anims.Motion(s.build, m, false)));
                // (A wounded one up off the crouch to run: clearingArt.ts, none running.)
                s.anim.Crouch = 0;
            }
            var gun = s.gun;
            // The pistol, its only gun, in its holster down the right thigh (syntyFigures.ts hold: pistolOnly), not on the back.
            if (s.pistol)
            {
                s.anim?.ArmRest(null, 0, 0.2f);
                if (gun != null && UnitViews.Holster(s.animator, s.animator.transform) is (Transform thigh, Vector3 there, Quaternion holstered))
                {
                    s.slingBone = thigh;
                    s.slingAt = there;
                    s.slingTurn = holstered;
                    KeepSlung(s);
                }
                return;
            }
            var bone = s.animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? s.animator.GetBoneTransform(HumanBodyBones.Chest);
            if (gun == null || bone == null) return;
            var figure = s.animator.transform;
            var chest = figure.InverseTransformPoint(bone.position);
            // (0.10.1's −x, the right, is +x here.)
            var along = new Vector3(0.45f, 0.9f, 0).normalized;
            var turn = Quaternion.LookRotation(along, Vector3.back);
            // The gun's middle along it, and its half thickness (in the figure's units): its body's, not its magazine's or
            // its bipod's (the 3D review's A-31, 10/11: by the whole gun's, a machine gun hung a hand off the back).
            var box = new Bounds();
            var own = new Bounds();
            bool any = false, owned = false;
            foreach (var f in gun.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null) continue;
                bool loose = f.name.Contains("Magazine") || f.name.Contains("Bipod");
                var m = gun.worldToLocalMatrix * f.transform.localToWorldMatrix;
                foreach (var corner in Corners(f.sharedMesh.bounds))
                {
                    var q = m.MultiplyPoint3x4(corner);
                    if (!any) box = new Bounds(q, Vector3.zero);
                    else box.Encapsulate(q);
                    any = true;
                    if (loose) continue;
                    if (!owned) own = new Bounds(q, Vector3.zero);
                    else own.Encapsulate(q);
                    owned = true;
                }
            }
            if (!owned) own = box;
            float scale = gun.lossyScale.x / Mathf.Max(figure.lossyScale.x, 1e-5f);
            float middle = box.center.z * scale, thick = Mathf.Min(own.size.x, own.size.y) / 2 * scale;
            float length = box.size.z * scale;
            var at = new Vector3(0, chest.y + 0.02f, chest.z - 0.11f);
            // Out from the back's skin wherever along it the gun lies (the hindmost of the body near its line).
            float back = at.z;
            var baked = new Mesh();
            foreach (var smr in s.animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || smr.sharedMesh == null) continue;
                smr.BakeMesh(baked, true);
                var toFigure = figure.worldToLocalMatrix * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in baked.vertices)
                {
                    var p = toFigure.MultiplyPoint3x4(v);
                    var off = p - at;
                    float t = Vector3.Dot(new Vector3(off.x, off.y, 0), along);
                    if (Mathf.Abs(t) > length / 2) continue;
                    var across = new Vector3(off.x, off.y, 0) - along * t;
                    if (across.magnitude > thick * 1.2f || p.z > chest.z) continue;
                    back = Mathf.Min(back, p.z);
                }
            }
            if (Application.isPlaying) Destroy(baked);
            else DestroyImmediate(baked);
            at.z = Mathf.Min(at.z, back - thick - 0.004f);
            var worldTurn = figure.rotation * turn;
            var worldAt = figure.TransformPoint(at) - worldTurn * new Vector3(0, 0, middle) * figure.lossyScale.x;
            s.slingAt = bone.InverseTransformPoint(worldAt);
            s.slingTurn = Quaternion.Inverse(bone.rotation) * worldTurn;
            s.slingBone = bone;
            KeepSlung(s);
            // How far the gun's body stands off the back's hindmost skin along its line, as slung (A-31, 4).
            Debug.Log(System.FormattableString.Invariant($"[sling] {s.root.name} gun {gun.name} ({string.Join(",", gun.GetComponentsInChildren<MeshFilter>().Select(f => f.name))}) thick {thick:0.000} (whole {Mathf.Min(box.size.x, box.size.y) / 2 * scale:0.000}) back {back:0.000} at {at.z:0.000} gap {back - (at.z + thick):0.000}"));
        }

        /// <summary>The slung gun where it rides on its bone as the body is now.</summary>
        static void KeepSlung(Stander s)
        {
            if (s.gun == null || s.slingBone == null) return;
            s.gun.SetPositionAndRotation(s.slingBone.TransformPoint(s.slingAt), s.slingBone.rotation * s.slingTurn);
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int k = 0; k < 8; k++) yield return b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
        }
    }
}
