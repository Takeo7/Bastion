using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A diagonal walked as one line (E2 of Arturo's notes of 15/11: «al caminar en diagonal, los personajes avanzan en
    /// zigzag»). Since 7/11 nobody steps diagonally (Paths.ComputeReach: the four directions), so a path to a tile off the
    /// grid's lines is a staircase, and Walk turned at every tile of it (its corners rounded within WalkRound). Now a run of
    /// plain steps goes straight from where the figure is to as far along it as it can, by the middles of the tiles and of
    /// the edges between them, where nothing is in between: its body (BodyRadius) only over the walk's own tiles and the
    /// open ones round them (Open: floor of the same storey and height, nothing standing on it, nobody on it nor walking
    /// through it, no fire, no wire), clear of the tall things by the kerbs (PoleClear: StreetDressing's street lights,
    /// poles, stop signs, hydrants and mailboxes), and no tile's middle it leaves out further than StrayMost from it. So
    /// never through a wall's corner, a door's or a window's frame, cover, a car, a prop or another unit; never across a
    /// storey or a stair (those steps are StepAcross's, apart). Its last half tile along its last step, so it ends facing
    /// the way the engine has it (the move's Facing). In the seconds the staircase took (Gameplay: each action seen whole,
    /// at the same pace), its clip as much slower as its way is shorter, so its feet keep to the ground.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>
        /// Tiles: how far round its middle the body takes room; how far from the line a tile's middle it leaves out may be
        /// (a 45° staircase's are 0.71 off: a straight line; a two-to-one's come to 0.9, and it goes in shorter lines); how
        /// far from a tall thing by the kerb its middle keeps; and how often the line is looked along.
        /// </summary>
        const float BodyRadius = 0.25f, StrayMost = 0.75f, PoleClear = 0.3f, LookEvery = 0.1f;
        /// <summary>BASTION_ZIGZAG=1: the staircases walked as before (the labs' «before»).</summary>
        public static bool StraightOff = Environment.GetEnvironmentVariable("BASTION_ZIGZAG") == "1";
        /// <summary>BASTION_ANDAR=1: a line [andar] for each run straightened (the labs and the real scene).</summary>
        static readonly bool StraightLog = Environment.GetEnvironmentVariable("BASTION_ANDAR") == "1";

        /// <summary>
        /// The walks under way (Walk): each walker's tiles still to go and when it set off. A group's walkers all set off in
        /// the same frame and are straightened in the next: none goes through another's way (its tiles are not open).
        /// </summary>
        readonly Dictionary<string, (HashSet<int> nodes, float at)> walkingOn = new Dictionary<string, (HashSet<int>, float)>();
        /// <summary>A walk's tiles are another's way for this long at most (one cut short by a new state, never let go).</summary>
        const float WalkingMost = 60;

        /// <summary>The tall things by the kerbs (tiles: x east, y south, the tiles' middles whole), and the map they are of.</summary>
        List<Vector2> poles;
        string polesMap;
        int polesWide, polesHigh;
        static readonly HashSet<string> Tall = new HashSet<string> { "streetLight", "powerPole", "stopSign", DecorKinds.Hydrant, DecorKinds.Mailbox };

        /// <summary>`id` walking `path` from `from`: those tiles taken for the others' straightening till it is there.</summary>
        void Claim(string id, Pos from, IReadOnlyList<Pos> path)
        {
            if (state == null) return;
            var nodes = new HashSet<int>();
            if (from != null) nodes.Add(Bastion.Engine.Grid.NodeOf(state, from));
            foreach (var p in path) nodes.Add(Bastion.Engine.Grid.NodeOf(state, p));
            walkingOn[id] = (nodes, Time.time);
        }

        /// <summary>Places nobody straightens through but `id`: where units stand or lie, the figures stand, others walk; fire and wire.</summary>
        HashSet<int> Taken(string id)
        {
            var taken = new HashSet<int>();
            foreach (var u in state.Units)
                if (u.Id != id && !u.Evacuated) taken.Add(Bastion.Engine.Grid.NodeOf(state, u.Pos));
            foreach (var other in views.Values)
                if (other.id != id && other.place != null) taken.Add(Bastion.Engine.Grid.NodeOf(state, other.place));
            foreach (var kv in walkingOn)
                if (kv.Key != id && Time.time - kv.Value.at < WalkingMost) taken.UnionWith(kv.Value.nodes);
            if (Burning.Nodes(state) is HashSet<int> fire) taken.UnionWith(fire);
            if (state.Slow != null) taken.UnionWith(state.Slow);
            return taken;
        }

        /// <summary>Whether (x, y) on storey z is floor at `floor`'s height (m) with nothing on it and nobody (`taken`): open to a straightened walk.</summary>
        bool Open(int x, int y, int z, float floor, HashSet<int> taken)
        {
            if (x < 0 || y < 0 || x >= state.Width || y >= state.Height) return false;
            var p = new Pos { X = x, Y = y, Z = z };
            if (Bastion.Engine.Grid.KindAt(state, p) != TileKinds.Floor || Mathf.Abs(MapSpace.FloorAt(state, p) - floor) > 0.05f) return false;
            int i = y * state.Width + x;
            var decor = z == 0 ? state.Decor : state.Levels != null && z - 1 < state.Levels.Count ? state.Levels[z - 1]?.Decor : null;
            if (decor != null && i < decor.Count && decor[i] != 0) return false;
            return !taken.Contains(Bastion.Engine.Grid.NodeOf(state, p));
        }

        /// <summary>
        /// The tall things by the kerbs of the map now (StreetDressing, as GroundCover places them), worked out once a map,
        /// as the figures are first synced on it (Sync: 10–20 ms, at the map's own making, not at a first walk).
        /// </summary>
        List<Vector2> Poles()
        {
            if (poles != null && polesMap == state.MapId && polesWide == state.Width && polesHigh == state.Height) return poles;
            poles = new List<Vector2>();
            (polesMap, polesWide, polesHigh) = (state.MapId, state.Width, state.Height);
            try
            {
                foreach (var d in StreetDressing.Dress(state, new GroundMap(state), GroundCover.Density))
                    if (Tall.Contains(d.Id)) poles.Add(new Vector2(d.Matrix.m03 - 0.5f, d.Matrix.m23 - 0.5f));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[andar] the street's tall things: " + e.Message);
            }
            return poles;
        }

        /// <summary>A point of the world in tiles (x east, y south, the tiles' middles whole), and back at height `y`.</summary>
        static Vector2 TilesOf(Vector3 at) => new Vector2(at.x / MapSpace.Tile - 0.5f, -at.z / MapSpace.Tile - 0.5f);
        static Vector3 WorldOf(Vector2 at, float y) => new Vector3((at.x + 0.5f) * MapSpace.Tile, y, -(at.y + 0.5f) * MapSpace.Tile);

        static float OffLine(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
            return (a + ab * t - p).magnitude;
        }

        /// <summary>
        /// The way of the run of plain steps path[k..m) from where `v` stands (`from` its tile), straightened as above, with
        /// a mark for each of its steps (where the way comes nearest that tile's middle, in order; the last at its end); null
        /// to walk it as ever: under two steps, a step passing another on it (its swerve), or nothing to straighten.
        /// </summary>
        Way Straighten(View v, Pos from, IReadOnlyList<Pos> path, int k, int m, IReadOnlyList<bool> passes)
        {
            // (Nor one caged: a step or two in its cell, its end pressed to its bars, UnitViews.Bars.)
            if (StraightOff || state == null || from == null || m - k < 2 || v.caged) return null;
            for (int n = k; n < m; n++)
                if (passes != null && n < passes.Count && passes[n]) return null;
            var clock = StraightLog ? System.Diagnostics.Stopwatch.StartNew() : null;
            float floor = MapSpace.FloorAt(state, from);
            // Where it is, then each step's edge middle and its tile's middle (`middle`: the step's, −1 an edge's).
            var q = new List<Vector2> { TilesOf(v.root.position) };
            var middle = new List<int> { -1 };
            for (int n = k; n < m; n++)
            {
                var a = n == k ? from : path[n - 1];
                q.Add(new Vector2((a.X + path[n].X) * 0.5f, (a.Y + path[n].Y) * 0.5f));
                middle.Add(-1);
                q.Add(new Vector2(path[n].X, path[n].Y));
                middle.Add(n);
            }
            int last = q.Count - 1, end = last - 1;
            // Its own tiles and the open ones round them, on a grid over them all and a tile more.
            int x0 = Mathf.FloorToInt(q.Min(p => p.x)) - 2, y0 = Mathf.FloorToInt(q.Min(p => p.y)) - 2;
            int wide = Mathf.CeilToInt(q.Max(p => p.x)) + 3 - x0, high = Mathf.CeilToInt(q.Max(p => p.y)) + 3 - y0;
            var allowed = new bool[wide * high];
            var looked = new bool[wide * high];
            int Cell(int x, int y) => x < x0 || y < y0 || x >= x0 + wide || y >= y0 + high ? -1 : (y - y0) * wide + (x - x0);
            var own = new List<Pos> { from };
            for (int n = k; n < m; n++) own.Add(path[n]);
            foreach (var p in own)
            {
                int c = Cell(p.X, p.Y);
                if (c >= 0) allowed[c] = looked[c] = true;
            }
            var taken = Taken(v.id);
            foreach (var p in own)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int c = Cell(p.X + dx, p.Y + dy);
                        if (c < 0 || looked[c]) continue;
                        looked[c] = true;
                        allowed[c] = Open(p.X + dx, p.Y + dy, from.Z, floor, taken);
                    }
            float minX = x0, maxX = x0 + wide, minY = y0, maxY = y0 + high;
            var tall = Poles().Where(p => p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY).ToList();
            const float Reach = 0.5f + BodyRadius;
            bool Clear(int i, int j)
            {
                Vector2 a = q[i], b = q[j];
                for (int t = i + 1; t < j; t++)
                    if (middle[t] >= 0 && OffLine(q[t], a, b) > StrayMost) return false;
                foreach (var p in tall)
                    if (OffLine(p, a, b) < PoleClear) return false;
                int count = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / LookEvery));
                for (int s = 0; s <= count; s++)
                {
                    var p = Vector2.Lerp(a, b, (float)s / count);
                    // (The tiles its body can reach from there: two across at most each way.)
                    for (int y = Mathf.CeilToInt(p.y - Reach); y <= Mathf.FloorToInt(p.y + Reach); y++)
                        for (int x = Mathf.CeilToInt(p.x - Reach); x <= Mathf.FloorToInt(p.x + Reach); x++)
                        {
                            float ox = Mathf.Max(Mathf.Abs(p.x - x) - 0.5f, 0), oy = Mathf.Max(Mathf.Abs(p.y - y) - 0.5f, 0);
                            if (ox * ox + oy * oy >= BodyRadius * BodyRadius - 1e-6f) continue;
                            int c = Cell(x, y);
                            if (c < 0 || !allowed[c]) return false;
                        }
                }
                return true;
            }
            // From each point kept, the furthest it can go straight to; never past the last edge's middle, so its last half
            // tile is along its last step.
            var keep = new List<int> { 0 };
            for (int a = 0; a < last;)
            {
                int best = a + 1;
                for (int b = a + 2; b <= (a < end ? end : last); b++)
                    if (Clear(a, b)) best = b;
                keep.Add(best);
                a = best;
            }
            if (keep.Count == q.Count) return null;
            // Points in a line with those either side of them left out.
            var line = new List<int> { keep[0] };
            for (int i = 1; i < keep.Count - 1; i++)
            {
                Vector2 d1 = q[keep[i]] - q[line[line.Count - 1]], d2 = q[keep[i + 1]] - q[keep[i]];
                if (Mathf.Abs(d1.x * d2.y - d1.y * d2.x) < 1e-4f && Vector2.Dot(d1, d2) > 0) continue;
                line.Add(keep[i]);
            }
            line.Add(keep[keep.Count - 1]);
            var way = RoundedWay(line.Select(i => WorldOf(q[i], i == 0 ? v.root.position.y : floor)).ToList());
            // Each step's tile reached where the way comes nearest its middle, in order; the last at the way's end.
            var marks = new List<float>();
            int after = 0;
            for (int n = k; n < m; n++)
            {
                if (n == m - 1)
                {
                    marks.Add(way.length);
                    break;
                }
                var c = new Vector2(path[n].X, path[n].Y);
                float nearest = float.MaxValue, along = way.along[after];
                int at = after;
                for (int j = after; j + 1 < way.samples.Count; j++)
                {
                    Vector2 a = TilesOf(way.samples[j]), b = TilesOf(way.samples[j + 1]);
                    var ab = b - a;
                    float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / ab.sqrMagnitude) : 0;
                    float d = (a + ab * t - c).magnitude;
                    if (d < nearest - 1e-5f)
                    {
                        nearest = d;
                        at = j;
                        along = way.along[j] + t * (way.along[j + 1] - way.along[j]);
                    }
                }
                after = at;
                marks.Add(Mathf.Max(along, marks.Count > 0 ? marks[marks.Count - 1] : 0));
            }
            way.marks = marks;
            if (clock != null)
                Debug.Log(System.FormattableString.Invariant($"[andar] {v.id} steps {k}–{m - 1} from ({from.X},{from.Y}): {line.Count} points of {q.Count}, {way.length / MapSpace.Tile:0.00} tiles; {clock.Elapsed.TotalMilliseconds:0.00} ms"));
            return way;
        }
    }
}
