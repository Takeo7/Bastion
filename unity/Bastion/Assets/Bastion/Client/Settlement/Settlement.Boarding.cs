using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bastion.Client.Map;
using Bastion.Client.Units;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The squad leaving (0.10.1's clearingArt.ts depart and board): the row, its guns slung, runs to La Lata's doors, one
    /// after another, and climbs in (each doorway's doors opening as its first one gets there and shutting behind its
    /// last; up a step, Mixamo's stairs, higher at a van's or a lorry's back, on in out of sight; into the 4×4 each by a door
    /// of its own, sitting down sideways and pulling it shut: boardCar), the camera down lower and in on the door most take;
    /// then La Lata goes, the camera back out to the row's shot turning after it only a little.
    /// </summary>
    public sealed partial class Settlement
    {
        const float BoardSpeed = 2.9f, BoardEase = 0.35f, BoardStagger = 0.12f, BoardTurn = 7;
        /// <summary>
        /// Up a step (Mixamo's «Ascending Stairs», StepSpan of it, in the clip's own seconds): the speed it plays at and how
        /// long that takes, how high its hips go up (tiles: the rest of the floor's height the body's), how long the walk on
        /// in takes and how far in it goes, out of sight. 0.10.1 read its 60 fps file as 30 and had it at half its pace
        /// (CAMP_SPANS and STEP_* twice these seconds); here it plays at its own, at 0.10.1's speed: twice as fast as there
        /// (Arturo, 25/10).
        /// </summary>
        const float StepSpeed = 1.3f, StepTime = 0.8f / StepSpeed, StepRise = 0.17f, StepWalk = 0.45f, StepIn = 0.5f;
        const float BoardLane = 0.3f, VanLeaf = 0.22f, TailgateOver = 0.15f, DoorGap = 0.15f, DoorOpen = 0.35f;
        const float Climb = StepTime + StepWalk;
        static readonly float BusFollow = 18 * Mathf.Deg2Rad;
        /// <summary>syntyAnims.ts BLEND: how long a camp move takes to come in or go.</summary>
        const float MoveBlend = 0.35f;
        static readonly (float from, float to) StepSpan = (0.15f, 0.95f), EnterSpan = (1.1f, 4.2f);
        /// <summary>
        /// Into the 4×4 (boardCar; «Entering Car», EnterSpan of it, measured on our bodies: SettlementLab.Into): how far out
        /// from the car's side its ground stands (where the hand meets the handle) and ahead of the doorway's middle, where
        /// the hips are from it as the clip begins (back along the car, in); its seconds and the speed it plays at; when the
        /// hand opens the door and shuts it; how long after the one before each starts in, and after the one in front on its
        /// side (its door out of the way). The clip's seconds as it is, at its own pace (0.10.1's half these, at half its
        /// pace, as StepSpan: twice as fast as there, Arturo 25/10), and the wait for the door with it.
        /// </summary>
        const float EnterSide = 0.62f, EnterAlong = -0.12f, EnterFromAlong = -0.18f, EnterFromIn = 0.36f;
        const float EnterSeconds = 3.1f, EnterSpeed = 1.25f, EnterAfter = 0.5f, EnterClear = 1.05f;
        static readonly (float from, float to) EnterOpen = (-0.175f, 0.125f), EnterShut = (2.65f, 2.95f);
        /// <summary>A 4×4's door leaf, its share of the car's length; how far it swings open (radians), and how much wider it is pulled.</summary>
        const float EnterLeaf = 0.17f, EnterSwing = 1.15f, EnterWide = 1.3f;
        /// <summary>How far the door is kept off the hand's middle (tiles: the fist's half), and how far in from the car's widest the hand is, the door shut on it.</summary>
        const float EnterFist = 0.2f, EnterIn = -0.12f;

        /// <summary>Whether the squad is getting in or La Lata going (Leave): the row and the camera are its meanwhile.</summary>
        public bool Leaving { get; private set; }

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
                var from = Camera != null ? (Vector3?)Camera.transform.position : null;
                if (standers.Count > 0 && Lata.Shown)
                {
                    float began = Time.time;
                    await Board(ct);
                    Debug.Log($"[settlement] {standers.Count} aboard La Lata ({Lata.Vehicle}) in {Time.time - began:0.00} s");
                }
                if (lineupRoot != null)
                    foreach (Transform t in lineupRoot)
                        if (t.name == "sitio") t.gameObject.SetActive(false);
                // Given again (the debrief, the next mission's), the row is made again.
                lineupKey = "";
                away = true;
                var going = Lata.DepartAsync(busWay, !busRoute, ct);
                await Follow(from, ct);
                await going;
            }
            finally
            {
                Leaving = false;
            }
        }

        /// <summary>
        /// The camera while La Lata goes: back out to where it was before the boarding, looking after La Lata only a little
        /// (BUS_FOLLOW: it drives on out of the frame, not the camera after it into the pines).
        /// </summary>
        async Awaitable Follow(Vector3? back, CancellationToken ct)
        {
            var camera = Camera;
            if (camera == null || Lata == null) return;
            var start = Lata.transform.position;
            var flat = new Vector3(start.x - camera.transform.position.x, 0, start.z - camera.transform.position.z);
            float reach = Mathf.Tan(BusFollow) * flat.magnitude;
            var look = camera.transform.position + camera.transform.forward * 4 * MapSpace.Tile;
            while (Lata != null && Lata.Moving && !ct.IsCancellationRequested)
            {
                var along = Lata.transform.position - start;
                along = Vector3.ClampMagnitude(along, reach);
                var to = start + along;
                to.y = 1.0f * MapSpace.Tile;
                look = Vector3.Lerp(look, to, 0.04f);
                if (back != null) camera.transform.position = Vector3.Lerp(camera.transform.position, back.Value, 0.03f);
                camera.transform.LookAt(look);
                await Awaitable.NextFrameAsync(ct);
            }
        }

        sealed class Boarder
        {
            public Stander s;
            public List<Vector3> run;
            public List<float> lengths;
            public float total, running, delay, atFoot;
            public Vector3 foot, doorway, inside;
            public int way, lane;
            public int phase;
        }

        /// <summary>The row in (clearingArt.ts board), in 0.10.1's tiles; done once all are aboard.</summary>
        async Awaitable Board(CancellationToken ct)
        {
            var doors = Lata.Doorways();
            if (doors.Count == 0) return;
            var standing = standers.Where(s => s.root != null && s.root.gameObject.activeInHierarchy).ToList();
            foreach (var s in standing) Sling(s);
            if (Lata.Vehicle == 0)
            {
                await BoardCar(standing, doors, ct);
                return;
            }
            var centre = Lata.At;
            var dir = new Vector3(Mathf.Sin(Lata.Heading), 0, Mathf.Cos(Lata.Heading));
            var across = new Vector3(dir.z, 0, -dir.x);
            float half = Lata.Length / 2, width = Lata.Width;
            var taken = new Dictionary<int, int>();
            var plans = new List<Boarder>();
            for (int i = 0; i < standing.Count; i++)
            {
                var s = standing[i];
                var from = Tiles(s.root.position);
                from.y = 0;
                var door = doors.OrderBy(d => Vector3.Distance(d, from)).First();
                var offset = door - centre;
                // A door on its side (the bus's, the 4×4's) or at its back (the van's, the lorry's).
                bool backDoor = Mathf.Abs(Vector3.Dot(offset, dir)) > half - 0.35f;
                var normal = backDoor ? dir * (Mathf.Sign(Vector3.Dot(offset, dir)) is float a && a != 0 ? a : -1) : across * (Mathf.Sign(Vector3.Dot(offset, across)) is float b && b != 0 ? b : 1);
                // A double doorway's two leaves are one way in: one at a time through it (two at a back one, side by side).
                int way = doors.FindIndex(d => Vector3.Distance(d, door) < 0.7f);
                int nth = taken.TryGetValue(way, out int n) ? n : 0;
                taken[way] = nth + 1;
                int lanes = backDoor && Lata.Vehicle == 2 ? 2 : 1;
                int lane = nth % lanes;
                var side2 = new Vector3(normal.z, 0, -normal.x);
                var aside = lanes > 1 ? side2 * (lane == 1 ? BoardLane : -BoardLane) : backDoor ? side2 * VanLeaf : Vector3.zero;
                var foot = door + normal * 0.55f + aside;
                var doorway = door - normal * 0.1f + aside;
                var inside = door - normal * (0.1f + StepIn) + aside;
                // Round its corner first, from behind it to a side door, or from its side to its back.
                var path = new List<Vector3> { from };
                bool behind = Vector3.Dot(from - centre, dir) < -half;
                float sideOf = Mathf.Sign(Vector3.Dot(from - centre, across)) is float c && c != 0 ? c : 1;
                if (behind && !backDoor) path.Add(centre - dir * (half + 0.4f) + normal * (width / 2 + 0.6f));
                if (!behind && backDoor) path.Add(centre - dir * (half + 0.4f) + across * (sideOf * (width / 2 + 0.6f)));
                path.Add(foot);
                var lengths = new List<float>();
                for (int k = 1; k < path.Count; k++) lengths.Add(Vector3.Distance(path[k], path[k - 1]));
                float total = lengths.Sum();
                float running = total / BoardSpeed + BoardEase;
                plans.Add(new Boarder { s = s, run = path, lengths = lengths, total = total, running = running, delay = i * BoardStagger, atFoot = running + i * BoardStagger, foot = foot, doorway = doorway, inside = inside, way = way, lane = lane });
            }
            if (plans.Count == 0) return;
            // One at a time at each door, the nearest first: each held back at its place until the one before has climbed in.
            for (int d = 0; d < doors.Count; d++)
                for (int lane = 0; lane < 2; lane++)
                {
                    float free = float.NegativeInfinity;
                    foreach (var p in plans.Where(q => q.way == d && q.lane == lane).OrderBy(q => q.atFoot))
                    {
                        float wait = Mathf.Max(0, free - p.atFoot);
                        p.delay += wait;
                        p.atFoot += wait;
                        free = p.atFoot + StepTime + DoorGap;
                    }
                }
            // Each doorway's doors: opening as its first gets to its foot, shutting once its last is in.
            var ways = Enumerable.Range(0, doors.Count).Select(d =>
            {
                var at = plans.Where(p => p.way == d).ToList();
                if (at.Count == 0) return ((float open, float shut)?)null;
                return (Mathf.Max(0, at.Min(p => p.atFoot) - DoorOpen), at.Max(p => p.atFoot + Climb) + 0.08f);
            }).ToList();
            // How high it is in: the 4×4's sill, the van's floor, the lorry's bed (and over its tailgate), the bus's step.
            float rise = new[] { 0.22f, 0.45f, 0.62f, 0.3f }[Mathf.Clamp(Lata.Vehicle, 0, 3)];
            float over = Lata.Vehicle == 2 ? TailgateOver : 0;
            float seconds = Mathf.Max(plans.Max(p => p.delay + p.running + Climb), ways.Max(w => w != null ? w.Value.shut + DoorOpen : 0)) + 0.05f;
            // The camera on the doorway most take.
            int busiest = Enumerable.Range(0, doors.Count).OrderByDescending(d => plans.Count(p => p.way == d)).First();
            var onDoor = doors[busiest];
            var shot = BoardShot(onDoor);
            moved = MoveSeconds;
            float now = 0;
            while (now < seconds && !ct.IsCancellationRequested)
            {
                now = Mathf.Min(seconds, now + Time.deltaTime);
                shot?.Invoke(now, seconds);
                for (int d = 0; d < ways.Count; d++)
                {
                    if (ways[d] == null) continue;
                    var (open, shut) = ways[d].Value;
                    float k = now < shut ? Mathf.Clamp01((now - open) / DoorOpen) : 1 - Mathf.Clamp01((now - shut) / DoorOpen);
                    Lata.SetWay(d, Mathf.SmoothStep(0, 1, k));
                }
                foreach (var p in plans) Step(p, now, rise, over);
                await Awaitable.NextFrameAsync(ct);
            }
            foreach (var p in plans) if (p.s.root != null) p.s.root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The camera while they get in (playtest 21/10 #3, Ana: a shot of its own): from the row's shot down lower and a
        /// little in, turned to between the row and `door` (the whole row still in it), then a slow push in on the door while
        /// they climb aboard; at `now` of `seconds`. None without a camera.
        /// </summary>
        System.Action<float, float> BoardShot(Vector3 door)
        {
            var camera = Camera;
            if (camera == null) return null;
            var onDoor = new Vector3(door.x, 1.0f, door.z);
            var fromPos = Tiles(camera.transform.position);
            var fromLook = goal.target;
            var intoPos = Vector3.Lerp(fromPos, new Vector3(onDoor.x, fromPos.y, onDoor.z), 0.15f);
            intoPos.y = Mathf.Lerp(fromPos.y, 1.7f, 0.6f);
            var intoLook = Vector3.Lerp(fromLook, onDoor, 0.6f);
            intoLook.y = 1.0f;
            return (now, seconds) =>
            {
                if (camera == null) return;
                float k = Smoother(Mathf.Min(1, now / 1.4f));
                float push = Mathf.Max(0, now - 1.4f) / Mathf.Max(0.5f, seconds - 1.4f);
                var pos = Vector3.Lerp(Vector3.Lerp(fromPos, intoPos, k), onDoor, push * 0.1f * k);
                var look = Vector3.Lerp(Vector3.Lerp(fromLook, intoLook, k), onDoor, Mathf.SmoothStep(0, 1, push) * 0.6f);
                camera.transform.position = World(pos);
                camera.transform.LookAt(World(look));
            };
        }

        static float Smoother(float t) => t * t * t * (t * (t * 6 - 15) + 10);

        /// <summary>`yaw` (0.10.1's, radians) turned towards `goal` as fast as they turn (BOARD_TURN), the short way round.</summary>
        static float TurnToward(float yaw, float goal) =>
            Mathf.MoveTowardsAngle(yaw * Mathf.Rad2Deg, goal * Mathf.Rad2Deg, BoardTurn * Mathf.Rad2Deg * Time.deltaTime) * Mathf.Deg2Rad;

        sealed class CarBoarder
        {
            public Stander s;
            public List<Vector3> path;
            public List<float> lengths;
            public float total, running, delay, start;
            public Vector3 origin, foot, inward, outward;
            public Transform hand;
            public string move;
            public int door, side;
            public bool front, started, inside;
        }

        /// <summary>
        /// Into the 4×4 (clearingArt.ts boardCar; 22/10 #12, Arturo: «se ponen a caballo y se meten de frente»; each by a door
        /// of its own, the driver first): each runs to beside its door, a hand to the handle, opens it, sits down sideways,
        /// swings the legs in and shuts it (Mixamo's «Entering Car», a left door's; mirrored for the right). The doors by the
        /// shortest runs all told; the driver's starts in first, each other a beat after the one before. Done once all are in.
        /// </summary>
        async Awaitable BoardCar(List<Stander> standing, List<Vector3> doors, CancellationToken ct)
        {
            var catalog = FigureCatalog.Load();
            var centre = Lata.At;
            var dir = new Vector3(Mathf.Sin(Lata.Heading), 0, Mathf.Cos(Lata.Heading));
            var across = new Vector3(dir.z, 0, -dir.x);
            float half = Lata.Length / 2, width = Lata.Width;
            var froms = standing.Select(s => Vector3.Scale(Tiles(s.root.position), new Vector3(1, 0, 1))).ToList();
            var seat = BestDoors(froms, doors);
            var plans = new List<CarBoarder>();
            for (int i = 0; i < standing.Count; i++)
            {
                var door = doors[seat[i]];
                // Its side (+1 its left), out from it, and in.
                int side = Vector3.Dot(door - centre, across) < 0 ? -1 : 1;
                var outward = across * side;
                // The clip's ground (its start, facing the car, its front to the left on its left side): out from the car's side
                // where the hand meets the handle, by the doorway's middle; where it stands as the clip begins.
                var origin = door + outward * EnterSide + dir * EnterAlong;
                var foot = origin + dir * EnterFromAlong - outward * EnterFromIn;
                // Round the car's nearer end to a door on its far side.
                var from = froms[i];
                var path = new List<Vector3> { from };
                if (Vector3.Dot(from - centre, outward) < 0)
                {
                    float end = Vector3.Dot(from - centre, dir) > 0 ? 1 : -1;
                    Vector3 Corner(int sg) => centre + dir * (end * (half + 0.45f)) + across * (sg * (width / 2 + 0.55f));
                    path.Add(Corner(-side));
                    path.Add(Corner(side));
                }
                path.Add(foot);
                var lengths = new List<float>();
                for (int k = 1; k < path.Count; k++) lengths.Add(Vector3.Distance(path[k], path[k - 1]));
                float total = lengths.Sum();
                // (The hand that pulls the door to: the left, or the right in the mirrored clip.)
                var hand = standing[i].animator != null && standing[i].animator.isHuman ? standing[i].animator.GetBoneTransform(side > 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand) : null;
                plans.Add(new CarBoarder
                {
                    s = standing[i], path = path, lengths = lengths, total = total, running = total / BoardSpeed + BoardEase, delay = i * BoardStagger,
                    origin = origin, foot = foot, inward = -outward, outward = outward, hand = hand, move = side > 0 ? "enterCar" : "enterCarR",
                    door = seat[i], side = side, front = Vector3.Dot(door - centre, dir) > 0,
                });
            }
            if (plans.Count == 0) return;
            // The driver's in first; each other, once there, a beat after the one before (any there first waits at its door,
            // facing it): those at the front doors, then those at the rear, each once the one in front on its side is in (its
            // door wide open stood across their way in: 23/10 #2).
            var driver = plans.FirstOrDefault(p => p.door == 0) ?? plans[0];
            float next = driver.delay + driver.running;
            var order = plans.Where(q => q != driver).OrderByDescending(q => q.front ? 1 : 0).ThenBy(q => q.delay + q.running).ToList();
            foreach (var p in new[] { driver }.Concat(order))
            {
                var ahead = p.front ? null : plans.FirstOrDefault(q => q.front && q.side == p.side && (q == driver || order.IndexOf(q) < order.IndexOf(p)));
                p.start = Mathf.Max(p.delay + p.running, next, ahead != null ? ahead.start + EnterClear : 0);
                next = p.start + EnterAfter;
            }
            float seconds = plans.Max(p => p.start + EnterSeconds / EnterSpeed) + 0.25f;
            var shot = BoardShot(doors[driver.door]);
            moved = MoveSeconds;
            float now = 0;
            while (now < seconds && !ct.IsCancellationRequested)
            {
                now = Mathf.Min(seconds, now + Time.deltaTime);
                shot?.Invoke(now, seconds);
                foreach (var p in plans) StepCar(p, now, doors, catalog);
                await Awaitable.NextFrameAsync(ct);
            }
            foreach (var p in plans) if (p.s.root != null) p.s.root.gameObject.SetActive(false);
        }

        /// <summary>One getting into the 4×4 at `now`: running to its door, waiting there, or in through it (its door by its hand).</summary>
        void StepCar(CarBoarder p, float now, List<Vector3> doors, FigureCatalog catalog)
        {
            var s = p.s;
            if (s.root == null) return;
            float c = (now - p.start) * EnterSpeed;
            // Its door opened and shut by its hand, as the clip has it; pulled to no further than its hand is in (the clip's
            // car is wider: shut on time, the arm stuck out through the panel). Gone in once the door is shut on it.
            float open = c < EnterShut.from ? Mathf.Clamp01((c - EnterOpen.from) / (EnterOpen.to - EnterOpen.from)) : 1 - Mathf.Clamp01((c - EnterShut.from) / (EnterShut.to - EnterShut.from));
            open = Mathf.SmoothStep(0, 1, open) * EnterWide;
            if (p.hand != null && c > EnterOpen.to && !p.inside)
            {
                float reach = Vector3.Dot(Tiles(p.hand.position) - doors[p.door], p.outward);
                open = Mathf.Max(open, Mathf.Asin(Mathf.Clamp01((reach + EnterFist) / (Lata.Length * EnterLeaf))) / EnterSwing);
                if (c >= EnterShut.to && reach < EnterIn) p.inside = true;
            }
            if (p.inside || c >= EnterSeconds) open = 0;
            Lata.SetWay(p.door, open, false);
            bool shown = !p.inside && c < EnterSeconds;
            if (s.root.gameObject.activeSelf != shown) s.root.gameObject.SetActive(shown);
            float since = now - p.delay;
            if (since <= 0 || !shown) return;
            float inward = Mathf.Atan2(p.inward.x, p.inward.z);
            if (c >= 0)
            {
                // Into the car: the clip moves the body, its ground stays (the hips' start offset eased off as it comes in).
                if (!p.started)
                {
                    p.started = true;
                    s.anim?.PlayWholePart(catalog?.Clip(p.move), EnterSpan.from, EnterSpan.to, MoveBlend, false, EnterSpeed);
                }
                float share = Mathf.Min(1, (now - p.start) / MoveBlend);
                s.root.position = World(Vector3.Lerp(p.foot, p.origin, share));
                s.yaw = TurnToward(s.yaw, inward);
                return;
            }
            if (since >= p.running)
            {
                // There first: waiting at its door, facing it.
                s.anim?.Motion("idle");
                s.root.position = World(p.foot);
                s.yaw = TurnToward(s.yaw, inward);
                return;
            }
            s.anim?.Motion("run");
            s.root.position = World(Along(p.path, p.lengths, p.total, p.running, since, out var a, out var b));
            s.yaw = TurnToward(s.yaw, Mathf.Atan2(b.x - a.x, b.z - a.z));
        }

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
            // The gun's middle along it, and its half thickness (in the figure's units).
            var box = new Bounds();
            bool any = false;
            foreach (var f in gun.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null) continue;
                var m = gun.worldToLocalMatrix * f.transform.localToWorldMatrix;
                foreach (var corner in Corners(f.sharedMesh.bounds))
                {
                    var q = m.MultiplyPoint3x4(corner);
                    if (!any) box = new Bounds(q, Vector3.zero);
                    else box.Encapsulate(q);
                    any = true;
                }
            }
            float scale = gun.lossyScale.x / Mathf.Max(figure.lossyScale.x, 1e-5f);
            float middle = box.center.z * scale, thick = Mathf.Min(box.size.x, box.size.y) / 2 * scale;
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

        /// <summary>One of the row at `now` of the boarding: running to its door's foot (picking up speed, slowing to it), up the step, on in.</summary>
        void Step(Boarder p, float now, float rise, float over)
        {
            var s = p.s;
            if (s.root == null) return;
            float since = now - p.delay;
            bool shown = since < p.running + Climb;
            if (s.root.gameObject.activeSelf != shown) s.root.gameObject.SetActive(shown);
            if (since <= 0 || !shown) return;
            if (since >= p.running)
            {
                // At the door, turned to it (22/10 #12: not stooped, «a caballo»): up the step, its feet as the clip has them
                // (Mixamo's stairs, a step of it) and the rest of the floor's height the body's, then on in, walking.
                float c = since - p.running;
                s.yaw = Mathf.Atan2(p.inside.x - p.foot.x, p.inside.z - p.foot.z);
                Vector3 at;
                if (c < StepTime)
                {
                    if (p.phase < 2)
                    {
                        p.phase = 2;
                        s.anim?.Motion("idle");
                        s.anim?.PlayWholePart(FigureCatalog.Load()?.Clip("stepUp"), StepSpan.from, StepSpan.to, MoveBlend, false, StepSpeed);
                    }
                    // Up first, then in (over a lorry's tailgate, a little higher as it goes over).
                    float k = c / StepTime;
                    at = Vector3.Lerp(p.foot, p.doorway, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.3f, 1, k)));
                    at.y = (rise - StepRise) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 0.6f, k)) + over * Mathf.Sin(Mathf.PI * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.3f, 1, k)));
                }
                else
                {
                    if (p.phase < 3)
                    {
                        p.phase = 3;
                        s.anim?.StopWhole(MoveBlend);
                        s.anim?.Motion("crouchWalk");
                    }
                    // (Its feet back on the floor as the step's clip eases out: the floor's whole height the body's again.)
                    // Stooped under its roof, on in (upright, the head came out through a van's).
                    float w = Mathf.Min(1, (c - StepTime) / StepWalk);
                    at = Vector3.Lerp(p.doorway, p.inside, w);
                    at.y = rise - StepRise * Mathf.Max(0, 1 - (c - StepTime) / MoveBlend);
                }
                s.root.position = World(at);
                return;
            }
            if (p.phase < 1)
            {
                p.phase = 1;
                s.anim?.Motion("run", 1);
            }
            // Picking up speed at the start, slowing at the door (a constant one between); turning to where it goes as it
            // goes, not all at once at each corner.
            s.root.position = World(Along(p.run, p.lengths, p.total, p.running, since, out var from, out var to));
            s.yaw = TurnToward(s.yaw, Mathf.Atan2(to.x - from.x, to.z - from.z));
        }
    }
}
