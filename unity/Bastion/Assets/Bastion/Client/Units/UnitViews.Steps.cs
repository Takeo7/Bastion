using System.Collections.Generic;
using System.Threading;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The steps of a walk that aren't on the flat (unitViews.ts stepOf and walk): up, along and off a building's stairs,
    /// onto a window's sill and off it, up and down a ladder, and a drop off an edge. 0.10.1's numbers are in tiles; its
    /// frame's z is Unity's −z (TileWay, U).
    /// </summary>
    public sealed partial class UnitViews
    {
        enum StepKind { Flat, Stairs, Sill, OffSill, LadderUp, LadderDown, Drop }

        /// <summary>unitViews.ts: a hop's seconds, a storey of ladder's, how high a window's sill is, how far out of its wall a vault stays crouched (tiles).</summary>
        const float HopSeconds = 0.16f, LadderSeconds = 1.2f, SillHeight = 0.75f, UnderLintel = 0.35f;
        /// <summary>
        /// The foot of a flight of one, behind its first tread, and the corner's point (STAIR_FOOT); a climber's middle behind
        /// the rungs and its chest over its feet (CLIMB_GAP, CLIMB_CHEST); a building's ladder from its tile's middle (LADDER_RUNGS). Tiles.
        /// </summary>
        const float StairFoot = 0.7f, ClimbGap = 0.2f, ClimbChest = 0.9f, LadderRungs = 0.44f;

        /// <summary>A point of 0.10.1's frame in tiles (x east, y up, z south) in Unity's, in metres.</summary>
        static Vector3 U(Vector3 tiles) => new Vector3(tiles.x * MapSpace.Tile, tiles.y * MapSpace.Tile, -tiles.z * MapSpace.Tile);

        /// <summary>Whether place p is a window (whole or broken) on its storey (windowAt).</summary>
        bool WindowAt(Pos p)
        {
            if (state == null || p.X < 0 || p.Y < 0 || p.X >= state.Width || p.Y >= state.Height) return false;
            var tiles = p.Z > 0 ? (state.Levels != null && p.Z - 1 < state.Levels.Count ? state.Levels[p.Z - 1]?.Tiles : null) : state.Tiles;
            int i = p.Y * state.Width + p.X;
            string t = tiles != null && i < tiles.Count ? tiles[i] : null;
            return t == "window" || t == "windowBroken";
        }

        /// <summary>The flight of stairs p is a tile of (flightOf): the way up it, its tiles, its lowest one and p's step on it; null if none.</summary>
        (Vec2 d, int n, Pos low, int step)? FlightOf(Pos p)
        {
            if (state == null || p.X < 0 || p.Y < 0 || p.X >= state.Width || p.Y >= state.Height) return null;
            var on = Flights.FlightAt(state, new Pos { X = p.X, Y = p.Y, Z = p.Z });
            if (on == null) return null;
            var low = on.Flight.Tiles[0];
            return (Bastion.Engine.Grid.Dirs4[on.Flight.Up], on.Flight.Tiles.Count, new Pos { X = low.X, Y = low.Y, Z = on.Flight.Z }, on.Step);
        }

        /// <summary>Where one stands on p in 0.10.1's frame (tiles): standAt.</summary>
        Vector3 StandTiles(Pos p)
        {
            var at = MapSpace.StandAt(state, p);
            return new Vector3(at.x, at.y, -at.z) / MapSpace.Tile;
        }

        /// <summary>A tile's middle on its floor in 0.10.1's frame (tileToWorld).</summary>
        Vector3 TileTiles(Pos p) => new Vector3(p.X + 0.5f, MapSpace.FloorAt(state, p) / MapSpace.Tile, p.Y + 0.5f);

        /// <summary>What the step from `from` to `to` is, and its seconds at a tile's `tile` seconds on the flat (stepOf).</summary>
        (StepKind kind, float seconds) StepOf(Pos from, Pos to, float tile)
        {
            float fromY = StandTiles(from).y + (WindowAt(from) ? SillHeight : 0);
            float rise = StandTiles(to).y - fromY, storey = MapSpace.Storey / MapSpace.Tile;
            var flight = FlightOf(to) ?? FlightOf(from);
            // A tile of a flight of several, up or down it: a little slower than on the flat.
            if (flight is { } f && f.n > 1) return (StepKind.Stairs, tile * 1.25f);
            if (flight != null)
            {
                // Round the corner from beside the flight is longer than straight up it: a little longer to walk.
                var way = TileWay(from, to);
                float length = 0;
                for (int k = 1; k < way.Count; k++) length += Mathf.Sqrt((way[k].x - way[k - 1].x) * (way[k].x - way[k - 1].x) + (way[k].z - way[k - 1].z) * (way[k].z - way[k - 1].z));
                return (StepKind.Stairs, tile * (Mathf.Abs(rise) > 0.5f ? 1.6f : 1) * Mathf.Max(1, length / 1.6f));
            }
            if (Mathf.Abs(rise) < 0.5f && WindowAt(to)) return (StepKind.Sill, HopSeconds * 1.8f);
            if (WindowAt(from)) return (StepKind.OffSill, HopSeconds * (1.2f + 0.45f * Mathf.Max(0, -rise)));
            if (rise > 0.5f) return (StepKind.LadderUp, tile + LadderSeconds * (rise / storey));
            if (rise < -0.5f && LadderDown(from, to, rise)) return (StepKind.LadderDown, tile + LadderSeconds * (-rise / storey));
            if (rise < -0.5f) return (StepKind.Drop, HopSeconds * (1.2f + 0.45f * -rise));
            return (StepKind.Flat, tile * (from.X != to.X && from.Y != to.Y ? 1.4f : 1));
        }

        /// <summary>What each step of a walk from `from` along `path` is (for the labs): its kind and seconds at a run.</summary>
        public string StepsOf(Pos from, IReadOnlyList<Pos> path)
        {
            var out_ = new List<string>();
            foreach (var to in path)
            {
                var (kind, seconds) = StepOf(from, to, 0.375f);
                out_.Add($"{to.X},{to.Y},{to.Z} {kind} {seconds:0.00}");
                from = to;
            }
            return string.Join("; ", out_);
        }

        /// <summary>
        /// How unit `id` stands (for the labs), in 0.10.1's frame (yaw 0 south, 90 east): its root's heading, its shoulders'
        /// (where the chest faces: left to right turned a quarter) and the line from its right hand to its left (the gun's).
        /// </summary>
        public string Stance(string id)
        {
            if (!views.TryGetValue(id, out var v) || v.animator == null || !v.animator.isHuman) return "-";
            var a = v.animator;
            float Yaw(Vector3 d) => Mathf.Atan2(d.x, -d.z) * Mathf.Rad2Deg;
            Vector3 P(HumanBodyBones b) => a.GetBoneTransform(b) is Transform t ? t.position : Vector3.zero;
            var shoulders = P(HumanBodyBones.RightUpperArm) - P(HumanBodyBones.LeftUpperArm);
            var hands = P(HumanBodyBones.LeftHand) - P(HumanBodyBones.RightHand);
            return $"root {Yaw(v.root.forward):0.0} | chest {Yaw(shoulders) + 90:0.0} | handR->L {Yaw(hands):0.0}";
        }

        /// <summary>Down a storey from `from` onto a ladder (not a building's stairs): climbed down, not jumped (ladderDown).</summary>
        bool LadderDown(Pos from, Pos to, float rise)
        {
            if (Mathf.Abs(rise + MapSpace.Storey / MapSpace.Tile) > 0.3f || Mathf.Abs(to.X - from.X) + Mathf.Abs(to.Y - from.Y) != 1) return false;
            return Bastion.Engine.Grid.KindAt(state, new Pos { X = to.X, Y = to.Y, Z = to.Z }) == TileKinds.Ladder && FlightOf(to) == null;
        }

        /// <summary>
        /// The way of a step onto, along or off a building's stairs (stairWay), in 0.10.1's frame, each point at the height
        /// of the treads under it: a flight of several straight along its line; one of a single tile straight up or down it,
        /// or from beside it or the corner behind round by its foot first, never across a banister.
        /// </summary>
        List<Vector3> TileWay(Pos from, Pos to)
        {
            bool toStairs = FlightOf(to) != null;
            Pos onStairs = toStairs ? to : from, other = toStairs ? from : to;
            var f = FlightOf(onStairs).Value;
            var d = f.d;
            float floor = MapSpace.FloorAt(state, f.low) / MapSpace.Tile;
            float Height(float a, int n) => MapSpace.FlightHeight(a, n) / MapSpace.Tile;
            var out_ = new List<Vector3>();
            if (f.n > 1)
            {
                float Up(Pos p) => (p.X - f.low.X) * d.X + (p.Y - f.low.Y) * d.Y + 0.5f;
                float a0 = Up(from), a1 = Up(to);
                Vector3 p0 = TileTiles(from), p1 = TileTiles(to);
                int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 0.1f));
                for (int i = 0; i <= n; i++)
                {
                    var q = Vector3.Lerp(p0, p1, (float)i / n);
                    q.y = floor + Height(a0 + (a1 - a0) * i / n, f.n);
                    out_.Add(q);
                }
                out_[0] = new Vector3(out_[0].x, StandTiles(from).y, out_[0].z);
                out_[n] = new Vector3(out_[n].x, StandTiles(to).y, out_[n].z);
                return out_;
            }
            var side = new Vector2(-d.Y, d.X);
            var mid = TileTiles(onStairs);
            int relX = other.X - onStairs.X, relY = other.Y - onStairs.Y;
            float u = relX * d.X + relY * d.Y, v = relX * side.x + relY * side.y;
            bool sameFloor = other.Z == onStairs.Z;
            // From the floor in front of the flight's high end: straight on.
            bool underTop = sameFloor && u > 0;
            Vector3 Point(float a, float b)
            {
                var p = new Vector3(mid.x + d.X * a + side.x * b, floor, mid.z + d.Y * a + side.y * b);
                if (underTop) p.y = floor + Height(0.5f, 1) * Mathf.Clamp01(1 - a);
                else if (Mathf.Abs(b) <= 0.5f + 1e-6f && a >= -0.6f) p.y = floor + Height(a + 0.5f, 1);
                return p;
            }
            var way = new List<Vector2> { new Vector2(u, v) };
            // From beside the flight or the corner behind it: round by its foot.
            if (v != 0 && sameFloor && u <= 0)
            {
                int sv = v > 0 ? 1 : -1;
                var corner = new Pos { X = onStairs.X - d.X + (int)side.x * sv, Y = onStairs.Y - d.Y + (int)side.y * sv, Z = onStairs.Z };
                bool cornerFree = u == -1 || (Bastion.Engine.Grid.CanStandOn(Bastion.Engine.Grid.KindAt(state, corner)) && Mathf.Approximately(MapSpace.FloorAt(state, corner) / MapSpace.Tile, floor) && FlightOf(corner) == null);
                string footKind = Bastion.Engine.Grid.KindAt(state, new Pos { X = onStairs.X - d.X, Y = onStairs.Y - d.Y, Z = onStairs.Z });
                if (cornerFree && Bastion.Engine.Grid.CanStandOn(footKind))
                {
                    if (u == 0) way.Add(new Vector2(-StairFoot, StairFoot * sv));
                    way.Add(new Vector2(-StairFoot, 0));
                }
            }
            way.Add(Vector2.zero);
            if (!toStairs) way.Reverse();
            out_.Add(Point(way[0].x, way[0].y));
            for (int k = 1; k < way.Count; k++)
            {
                var w0 = way[k - 1];
                var w1 = way[k];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(w0, w1) / 0.1f));
                for (int i = 1; i <= n; i++) out_.Add(Point(w0.x + (w1.x - w0.x) * i / n, w0.y + (w1.y - w0.y) * i / n));
            }
            // The landing (a storey up) and any floor off the flight: the points there on that floor.
            int end = toStairs ? 0 : out_.Count - 1;
            out_[end] = new Vector3(out_[end].x, StandTiles(other).y, out_[end].z);
            return out_;
        }

        /// <summary>Unit v's step of `kind` from `from` to `to` over `seconds` (walk's steps that aren't plain), at `gait`, a tile `tile` seconds.</summary>
        async Awaitable StepAcross(View v, string id, StepKind kind, Pos from, Pos to, float seconds, Gaits.Gait gait, float tile, float clip, CancellationToken ct)
        {
            Vector3 start = v.root.position, end = MapSpace.StandAt(state, to);
            float rise = end.y - start.y;
            Face(id, end);
            async Awaitable Tween(float time, System.Action<float> at)
            {
                for (float t = 0; t < time;)
                {
                    await Awaitable.NextFrameAsync(ct);
                    t = Mathf.Min(time, t + Time.deltaTime);
                    at(time > 0 ? t / time : 1);
                }
                if (time <= 0) at(1);
            }
            var anim = v.anim;
            switch (kind)
            {
                case StepKind.Stairs:
                {
                    // Up or down the line of its treads, the feet on them, turned to where it goes a few tenths on.
                    var way = TileWay(from, to).ConvertAll(U);
                    way[0] = start;
                    var lengths = new List<float>();
                    float total = 0;
                    for (int k = 1; k < way.Count; k++)
                    {
                        float l = Mathf.Sqrt((way[k].x - way[k - 1].x) * (way[k].x - way[k - 1].x) + (way[k].z - way[k - 1].z) * (way[k].z - way[k - 1].z));
                        lengths.Add(l);
                        total += l;
                    }
                    if (total <= 0) total = 1;
                    int leg = -1;
                    await Tween(seconds, t =>
                    {
                        float left = t * total;
                        int i = 0;
                        while (i < lengths.Count - 1 && left > lengths[i]) left -= lengths[i++];
                        v.root.position = Vector3.Lerp(way[i], way[i + 1], Mathf.Min(1, left / (lengths[i] > 0 ? lengths[i] : 1)));
                        if (i != leg && i + 1 < way.Count - 1)
                        {
                            leg = i;
                            Face(id, way[Mathf.Min(way.Count - 1, i + 3)]);
                        }
                    });
                    break;
                }
                case StepKind.Sill:
                {
                    // A crouched climb up to the sill, down low by the time the head comes under the frame's top, and so on it.
                    var sill = end + Vector3.up * (SillHeight * MapSpace.Tile);
                    anim?.Motion("idle");
                    float crouch = anim?.Crouch ?? 0;
                    await Tween(seconds, t =>
                    {
                        var at = Vector3.Lerp(start, sill, t);
                        at.y = start.y + (sill.y - start.y) * Mathf.Pow(Mathf.Min(1, t / 0.65f), 0.6f);
                        v.root.position = at;
                        if (anim != null) anim.Crouch = Mathf.Max(crouch, EaseInOut(Mathf.Min(1, t / 0.6f)));
                    });
                    if (anim != null) anim.Crouch = 1;
                    v.onSill = true;
                    break;
                }
                case StepKind.OffSill:
                {
                    // Off it, still crouched through the frame, then down to the ground beyond (or the fall, from a storey up).
                    anim?.Motion("idle");
                    float out_ = UnderLintel * MapSpace.Tile / Mathf.Max(0.5f * MapSpace.Tile, Mathf.Sqrt((end.x - start.x) * (end.x - start.x) + (end.z - start.z) * (end.z - start.z)));
                    await Tween(seconds, t =>
                    {
                        float along = Mathf.Min(1, t * 1.3f);
                        var at = Vector3.Lerp(start, end, along);
                        float down = Mathf.Clamp01((along - out_) / (1 - out_));
                        at.y = start.y + rise * down * down;
                        v.root.position = at;
                        if (anim != null) anim.Crouch = along < out_ ? 1 : 1 - 0.6f * EaseInOut(down);
                    });
                    v.onSill = false;
                    anim?.Motion(gait.motion, clip);
                    _ = Dip(v, t => Mathf.Max(0, 0.4f + Mathf.Sin(t * Mathf.PI) * 0.4f - 0.4f * t));
                    break;
                }
                case StepKind.LadderUp:
                case StepKind.LadderDown:
                    await ClimbLadder(v, id, kind == StepKind.LadderUp ? v.place : to, kind == StepKind.LadderUp ? to : v.place, kind == StepKind.LadderUp, start, end, tile, gait, clip, Tween);
                    break;
                case StepKind.Drop:
                    // A short hop out, then the fall, longer the higher it is; a crouch on landing.
                    await Tween(seconds, t =>
                    {
                        var at = Vector3.Lerp(start, end, Mathf.Min(1, t * 1.3f));
                        at.y = start.y + rise * t * t + Mathf.Sin(Mathf.Min(1, t * 2) * Mathf.PI) * 0.35f * MapSpace.Tile;
                        v.root.position = at;
                    });
                    _ = Dip(v, t => Mathf.Sin(t * Mathf.PI) * 0.8f);
                    break;
            }
        }

        static float EaseInOut(float t) => t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;

        /// <summary>A crouch over 0.2 s as `at` has it (0 to 1 through it), then none: a landing.</summary>
        static async Awaitable Dip(View v, System.Func<float, float> at)
        {
            for (float t = 0; t < 0.2f; t += Time.deltaTime)
            {
                if (v.anim == null || v.onSill) return;
                v.anim.Crouch = at(t / 0.2f);
                await Awaitable.NextFrameAsync();
            }
            if (v.anim != null && !v.onSill) v.anim.Crouch = 0;
        }

        /// <summary>
        /// How far the rungs of the ladder on tile p stand from its middle, towards where it climbs, `h` tiles up its foot
        /// (rungsOf): the siege tower's leant from its foot onto the deck's edge (BasePieces.TowerLadder*), a building's upright.
        /// </summary>
        System.Func<float, float> RungsOf(Pos p)
        {
            if (state?.Base != null && p.Z == 0)
            {
                var map = BasePieces.MapOf(state);
                int k = map != null && p.Y * state.Width + p.X < map.at.Length ? map.at[p.Y * state.Width + p.X] : -1;
                if (k >= 0 && map.spots[k].piece.Piece == "tower")
                    return h => (float)(BasePieces.TowerLadderFoot + BasePieces.TowerLadderLean * Mathf.Min(h, (float)BasePieces.TowerLadderTop));
            }
            return h => LadderRungs;
        }

        /// <summary>
        /// Up a ladder from tile `low` to `high` (`up`), or down it (climbLadder): walked to it, climbed facing the rungs with
        /// the body ClimbGap behind them all the way, and off at the top onto the floor, or at the foot to the ladder's tile.
        /// </summary>
        async Awaitable ClimbLadder(View v, string id, Pos low, Pos high, bool up, Vector3 from, Vector3 to, float tile, Gaits.Gait gait, float clip,
            System.Func<float, System.Action<float>, Awaitable> tween)
        {
            var foot0 = MapSpace.TileToWorld(state, low);
            var dir = new Vector3(high.X - low.X, 0, -(high.Y - low.Y)).normalized;
            float height = Mathf.Abs(to.y - from.y);
            var rungs = RungsOf(low);
            Vector3 At(float h) => foot0 + dir * ((rungs(h / MapSpace.Tile + ClimbChest) - ClimbGap) * MapSpace.Tile) + Vector3.up * h;
            Vector3 foot = At(0), top = At(height);
            float climbing = LadderSeconds * (height / MapSpace.Storey);
            async Awaitable Climb(float a, float b)
            {
                Climbing(v, true);
                try
                {
                    await tween(climbing, t => v.root.position = At(a + (b - a) * t));
                }
                finally
                {
                    Climbing(v, false, gait.motion, clip);
                }
            }
            if (up)
            {
                await tween(tile * 0.4f, t => v.root.position = Vector3.Lerp(from, foot, t));
                Face(id, foot + dir);
                await Climb(0, height);
                Face(id, to);
                await tween(tile * 0.6f, t => v.root.position = Vector3.Lerp(top, to, t));
                return;
            }
            // Down: to the top of it, turned round to face the rungs, down them, and off at the foot.
            await tween(tile * 0.6f, t => v.root.position = Vector3.Lerp(from, top, t));
            Face(id, top + dir);
            await Climb(height, 0);
            Face(id, to);
            await tween(tile * 0.4f, t => v.root.position = Vector3.Lerp(foot, to, t));
        }

        /// <summary>
        /// On a ladder's rungs or off them: still (the idle under the climb's pose, ClimbPose), the gun's clips let go as a
        /// search does (a rifle kept at low ready in the right hand); off, back to its walk and its hold.
        /// </summary>
        void Climbing(View v, bool on, string walk = "idle", float speed = 1)
        {
            v.climbing = on;
            if (v.anim == null) return;
            if (on)
            {
                v.anim.Motion("idle");
                if (Rifled(v) && v.animator != null && v.animator.isHuman)
                {
                    HoldAsNow(v);
                    var catalog = FigureCatalog.Load();
                    v.anim.Rest(null, 0.15f);
                    if (catalog != null) v.anim.ArmRest(catalog.Clip(RestOf(v)), 0, 0.15f);
                }
                return;
            }
            v.anim.Motion(walk, speed);
            if (Rifled(v)) Settle(v, 0.15f);
        }

        /// <summary>
        /// The climb over the clips (syntyFigures.ts climbLegs and rung): the legs stepping up in turn, the lower foot on its
        /// rung; the hands on the rungs in front, the higher on its turn (with a rifle, the left alone: the gun at low ready).
        /// </summary>
        void ClimbPose(View v)
        {
            var a = v.animator;
            if (!v.climbing || a == null || !a.isHuman) return;
            float scale = v.body != null ? v.body.lossyScale.x : 1, time = Time.time;
            var turn = v.root.rotation;
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            float s7 = Mathf.Sin(time * 7);
            var legs = new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, -1f), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 1f) };
            float rest = float.PositiveInfinity, lowest = float.PositiveInfinity;
            for (int k = 0; k < 2; k++)
            {
                var (upper, lower, foot, outward) = legs[k];
                Transform thigh = a.GetBoneTransform(upper), shin = a.GetBoneTransform(lower), ankle = a.GetBoneTransform(foot);
                if (thigh == null || shin == null || ankle == null) return;
                rest = Mathf.Min(rest, ankle.position.y);
                // (Three's turns about its x: the thigh forward of straight down, the shin a little back of it; splayed 0.05.)
                float lift = Mathf.Max(0, k == 1 ? s7 : -s7), up = 0.25f + 1.05f * lift, back = 0.05f + 0.35f * lift;
                var along = turn * new Vector3(outward * 0.05f, -Mathf.Cos(up), Mathf.Sin(up));
                thigh.rotation = Quaternion.FromToRotation(shin.position - thigh.position, along) * thigh.rotation;
                var below = turn * new Vector3(outward * 0.05f, -Mathf.Cos(back), -Mathf.Sin(back));
                shin.rotation = Quaternion.FromToRotation(ankle.position - shin.position, below) * shin.rotation;
                lowest = Mathf.Min(lowest, ankle.position.y);
            }
            // The lower foot on its rung: where the feet stood.
            hips.position += Vector3.up * (rest - lowest);
            foreach (var (side, upperArm, lowerArm, hand) in new[] { (1f, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (-1f, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
            {
                if (side < 0 && v.weapon != null)
                {
                    if (Rifled(v)) LowReady(v, 1, scale, 0);
                    continue;
                }
                var shoulder = a.GetBoneTransform(upperArm);
                if (shoulder == null) continue;
                var rung = shoulder.position + turn * new Vector3(side * 0.04f, 0.16f + 0.1f * Mathf.Sin(time * 7 + (side > 0 ? Mathf.PI : 0)), 0.3f) * scale;
                Reach(a, upperArm, lowerArm, hand, rung, turn * new Vector3(-side * 0.6f, -1, -0.3f), SoftReach * scale);
            }
        }
    }
}
