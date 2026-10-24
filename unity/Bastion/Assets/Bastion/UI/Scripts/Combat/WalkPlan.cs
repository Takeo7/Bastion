using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// Walks played at the same time without anyone walking through anyone (walkPlan.ts: the enemy turn's dead walking in
    /// one run, EventPlayer.PlayGroup). The engine plans such walks one after another, each from where the ones before
    /// ended up, but they are played at once, at different paces: a runner overtook a walker by walking through it, two
    /// crossed in a doorway. This keeps a timeline of who holds which tile when and holds a walker back (at the start, or
    /// before a tile) while the tile it is about to enter is still held by another walker, or will be entered by one
    /// planned before it. No deadlocks and no long turn: each wait is capped, the group as a whole may take only a little
    /// longer than played all at once (Slack: it walks quicker to keep to that, if it must, PlanHasted), and a standstill
    /// lets one through. Where one has to go by another all the same, the step is marked (Passes): it swerves round it.
    /// </summary>
    public static class WalkPlan
    {
        /// <summary>One walker: the tiles it goes through (a key unique per tile and storey), the one it starts on first.</summary>
        public sealed class Walker
        {
            public int[] Tiles;
            /// <summary>Seconds each step takes, Tiles[k] to Tiles[k + 1].</summary>
            public float[] Steps;
            /// <summary>A diagonal step's two tiles beside (two walkers crossing through the same corner), null for the others.</summary>
            public (int, int)?[] Corners;
        }

        /// <summary>A walker's plan: the seconds it stands before each step, the tiles it passes someone on, how quick it goes.</summary>
        public sealed class Plan
        {
            public float[] Waits;
            /// <summary>By tile index (Tiles[k]): another stands there or comes through as it does.</summary>
            public bool[] Passes;
            /// <summary>How many times its pace it walks at, the whole group alike (PlanHasted; 1, at it).</summary>
            public float Haste;
        }

        /// <summary>How far into a step (0 to 1) the tile it leaves is free again: the walker's body is past the border by then.</summary>
        const float Release = 0.6f;
        /// <summary>A walker going ahead of one planned before it must be off the shared tile this long before the other gets there.</summary>
        const float AheadMargin = 0.15f;
        /// <summary>The longest a walker waits for one planned before it to go by first (not its last tile: there it waits for good).</summary>
        const float YieldCap = 1.0f;
        /// <summary>Stepping onto where it stops, a walker lets one planned after it go through first if that one is due within StopSoon, waiting StopCap at most.</summary>
        const float StopSoon = 1.2f;
        const float StopCap = 0.8f;
        /// <summary>The group may take this much longer than played all at once (seconds; a quarter of that if more).</summary>
        const float Slack = 0.8f;
        /// <summary>One out of time keeps the last Reserve seconds of its time for one stepping into the tile it goes to; past them it goes by.</summary>
        const float Reserve = 0.4f;
        /// <summary>A group that won't keep to its time otherwise walks quicker, this many times its pace at most.</summary>
        const float MaxHaste = 1.4f;
        /// <summary>The plan gives up (one walk after another) past this many decisions.</summary>
        const int MaxEvents = 20000;
        const float Eps = 1e-6f;

        sealed class Live
        {
            public int K;
            public bool Moving;
            public float Start;
            public float End;
            public float ReleaseAt;
            public float Ready;
            public float Waited;
            public float Budget;
            public bool Forced;
            public float[] Waits;
            public bool[] Passes;
        }

        /// <summary>Each walker's plan, in their planning order (walkers[0] planned first: it goes first wherever they meet).</summary>
        public static Plan[] PlanWalks(IReadOnlyList<Walker> walkers) => Settle(walkers, DeadlineOf(walkers), 1);

        /// <summary>
        /// PlanWalks for a group that may walk quicker to keep to its time: `walkers(haste)` gives its walkers at `haste`
        /// times their pace. At it if that keeps to the time; else as much quicker as it takes, MaxHaste at most, nobody
        /// going by anybody; past that, those out of time go by others.
        /// </summary>
        public static Plan[] PlanHasted(Func<float, IReadOnlyList<Walker>> walkers)
        {
            var these = walkers(1);
            var deadline = DeadlineOf(these);
            var haste = 1f;
            // A few tries, each as much quicker as it fell short: some steps (a sill, a ladder) quicken less than the rest.
            for (var tries = 0; tries < 4; tries++)
            {
                var plans = Fit(these, deadline, null, haste);
                if (plans == null) break;
                var length = PlanLength(these, plans.Select(p => p.Waits).ToList());
                if (length <= deadline + Eps) return plans;
                if (haste >= MaxHaste) break;
                haste = Math.Min(MaxHaste, haste * (length / deadline) * 1.01f);
                these = walkers(haste);
            }
            return Settle(these, deadline, haste);
        }

        /// <summary>The group's time: its longest walk, and a little longer (Slack, or a quarter of it).</summary>
        static float DeadlineOf(IReadOnlyList<Walker> walkers)
        {
            var longest = walkers.Count == 0 ? 0 : walkers.Max(w => w.Steps.Sum());
            return longest + Math.Max(Slack, longest * 0.25f);
        }

        /// <summary>The plans by `deadline`: as freely as they go, else again with Reserve kept; if they can't be fitted, one after another.</summary>
        static Plan[] Settle(IReadOnlyList<Walker> walkers, float deadline, float haste)
        {
            var free = Fit(walkers, deadline, null, haste);
            var plans = free != null && PlanLength(walkers, free.Select(p => p.Waits).ToList()) <= deadline + Eps ? free : Fit(walkers, deadline, Reserve, haste) ?? free;
            if (plans != null) return plans;
            // In planning order.
            var at = 0f;
            return walkers.Select(w =>
            {
                var waits = new float[w.Steps.Length];
                if (waits.Length > 0) waits[0] = at;
                at += w.Steps.Sum();
                return new Plan { Waits = waits, Passes = new bool[w.Tiles.Length], Haste = haste };
            }).ToArray();
        }

        /// <summary>
        /// The plans by `deadline`, or null if they can't be fitted. Out of time, a walker waits for another stepping into or
        /// out of the tile it goes to, as long as that takes (`reserve` null); or, for one stepping into it, for as long as
        /// `reserve` lets it still get there by the deadline, and goes by the rest.
        /// </summary>
        static Plan[] Fit(IReadOnlyList<Walker> walkers, float deadline, float? reserve, float haste)
        {
            var n = walkers.Count;
            var live = walkers.Select(w => new Live
            {
                Budget = deadline - w.Steps.Sum() - (reserve ?? 0),
                Waits = new float[w.Steps.Length],
                Passes = new bool[w.Tiles.Length],
            }).ToArray();
            int Last(int w) => walkers[w].Tiles.Length - 1;
            bool Done(int w) => !live[w].Moving && live[w].K >= Last(w);
            // Whether walker o holds tile `key` at time t: where it stands, where it goes, and where it leaves until it is clear of it.
            bool Holds(int o, int key, float t)
            {
                var l = live[o];
                var tiles = walkers[o].Tiles;
                if (tiles[l.K] == key) return true;
                return l.Moving && t < l.ReleaseAt && l.K > 0 && tiles[l.K - 1] == key;
            }
            // Walker o crossing the corner between tiles a and b at time t (a diagonal step from one to the other).
            bool Crossing(int o, int a, int b, float t)
            {
                var l = live[o];
                if (!l.Moving || t >= l.ReleaseAt || l.K == 0) return false;
                var tiles = walkers[o].Tiles;
                var from = tiles[l.K - 1];
                var to = tiles[l.K];
                return (from == a && to == b) || (from == b && to == a);
            }
            // When walker o would first hold its tile idx (the start of its step into it), at the earliest.
            float Reaches(int o, int idx, float t)
            {
                var l = live[o];
                var at = l.Moving ? l.End : t;
                for (var k = l.K; k < idx - 1; k++) at += walkers[o].Steps[k];
                return at;
            }
            (int, int)? CornerOf(int w) => walkers[w].Corners != null && live[w].K < walkers[w].Corners.Length ? walkers[w].Corners[live[w].K] : null;
            // Someone in the way of walker w's next step at t (it would go by them): the tile is held, or a crossing.
            bool InWay(int w, float t)
            {
                var key = walkers[w].Tiles[live[w].K + 1];
                var corner = CornerOf(w);
                for (var o = 0; o < n; o++)
                    if (o != w && (Holds(o, key, t) || (corner != null && Crossing(o, corner.Value.Item1, corner.Value.Item2, t)))) return true;
                return false;
            }
            // Why walker w can't take its next step at t: hold (the tile is held) or yield (another goes through it first),
            // with whom, and until when at most (a capped wait); null if it can go.
            (bool hold, int by, float? until)? Blocked(int w, float t)
            {
                var l = live[w];
                var walker = walkers[w];
                var key = walker.Tiles[l.K + 1];
                var corner = CornerOf(w);
                for (var o = 0; o < n; o++)
                {
                    if (o == w) continue;
                    // One stopped for good on its way: the engine walks it through (allies let each other by), at once.
                    if (Holds(o, key, t) && Done(o)) continue;
                    if (Holds(o, key, t) || (corner != null && Crossing(o, corner.Value.Item1, corner.Value.Item2, t))) return (true, o, null);
                }
                var end = l.K + 1 == Last(w);
                // Onto where it stops: one planned after it that goes through there soon goes first, for a while at most.
                if (end && t - l.Ready < StopCap - 1e-9f)
                    for (var o = w + 1; o < n; o++)
                    {
                        if (Done(o)) continue;
                        var tiles = walkers[o].Tiles;
                        for (var idx = live[o].K + 1; idx < tiles.Length - 1; idx++)
                            if (tiles[idx] == key && Reaches(o, idx, t) - t <= StopSoon) return (false, o, l.Ready + StopCap);
                    }
                // Where one planned before it is still to go through: after it (the engine's order), or well ahead of it.
                for (var o = 0; o < w; o++)
                {
                    if (Done(o)) continue;
                    var tiles = walkers[o].Tiles;
                    for (var idx = live[o].K + 1; idx < tiles.Length; idx++)
                    {
                        if (tiles[idx] != key) continue;
                        // Where the earlier one stops: better gone through before it gets there (it waits for this one, if need be).
                        if (idx == tiles.Length - 1) break;
                        if (end) return (false, o, null);
                        var clear = t + walker.Steps[l.K] + Release * (l.K + 1 < walker.Steps.Length ? walker.Steps[l.K + 1] : 0);
                        if (clear + AheadMargin <= Reaches(o, idx, t) || t - l.Ready >= YieldCap - 1e-9f) break;
                        return (false, o, l.Ready + YieldCap);
                    }
                }
                return null;
            }
            // For one out of time: only another moving into or out of the tile it goes to holds it back (with a reserve, into it, while that lasts).
            (bool hold, int by, float? until)? Passing(int w, float t)
            {
                var l = live[w];
                var key = walkers[w].Tiles[l.K + 1];
                var corner = CornerOf(w);
                for (var o = 0; o < n; o++)
                {
                    var other = live[o];
                    if (o == w || !other.Moving) continue;
                    var across = corner != null && Crossing(o, corner.Value.Item1, corner.Value.Item2, t);
                    if (reserve == null)
                    {
                        if (Holds(o, key, t) || across) return (true, o, null);
                    }
                    else if ((walkers[o].Tiles[other.K] == key || across) && l.Waited + (other.End - l.Ready) <= l.Budget + reserve.Value + 1e-9f) return (true, o, null);
                }
                return null;
            }
            void Go(int w, float t)
            {
                var l = live[w];
                var seconds = walkers[w].Steps[l.K];
                // Going by someone all the same (forced, or one stopped on its way): it swerves round them.
                if (InWay(w, t)) l.Passes[l.K + 1] = true;
                l.Waits[l.K] = t - l.Ready;
                l.Waited += t - l.Ready;
                l.K++;
                l.Moving = true;
                l.Start = t;
                l.End = t + seconds;
                l.ReleaseAt = t + seconds * Release;
                l.Forced = false;
            }

            var time = 0f;
            for (var events = 0; events < MaxEvents; events++)
            {
                // Arrivals first: who got to its tile stands there, ready for the next.
                foreach (var l in live)
                    if (l.Moving && l.End <= time + 1e-9f)
                    {
                        l.Moving = false;
                        l.Ready = l.End;
                    }
                if (Enumerable.Range(0, n).All(Done)) return live.Select(l => new Plan { Waits = l.Waits, Passes = l.Passes, Haste = haste }).ToArray();
                // Then, in planning order, whoever can set off does; whoever has stood all it may, goes all the same.
                var stuck = new List<(int w, bool hold, int by, float? until)>();
                for (var w = 0; w < n; w++)
                {
                    var l = live[w];
                    if (l.Moving || Done(w)) continue;
                    // Out of time it goes by whoever stands in its way, but not into a tile another is stepping into or out of.
                    var late = l.Waited + (time - l.Ready) >= l.Budget - 1e-9f;
                    var why = l.Forced ? null : late ? Passing(w, time) : Blocked(w, time);
                    if (why == null) Go(w, time);
                    else stuck.Add((w, why.Value.hold, why.Value.by, why.Value.until));
                }
                // The next moment anything changes: a step ends or leaves its tile behind, a wait runs out.
                var next = float.PositiveInfinity;
                foreach (var l in live)
                {
                    if (!l.Moving) continue;
                    if (l.ReleaseAt > time + 1e-9f) next = Math.Min(next, l.ReleaseAt);
                    next = Math.Min(next, l.End);
                }
                foreach (var s in stuck)
                    if (s.until != null && s.until.Value > time) next = Math.Min(next, s.until.Value);
                if (!float.IsPositiveInfinity(next))
                {
                    // (Or sooner, when one of those waiting has stood all it may.)
                    foreach (var s in stuck)
                    {
                        var outAt = live[s.w].Ready + live[s.w].Budget - live[s.w].Waited;
                        if (outAt > time + 1e-9f) next = Math.Min(next, outAt);
                    }
                    time = Math.Max(time, next);
                    continue;
                }
                // A standstill: nobody moves and nobody can. One goes through: who only gives way, the last planned; else who
                // waits on one that has stopped for good, else the first planned.
                if (stuck.Count == 0) break;
                var yielding = stuck.Where(s => !s.hold).ToList();
                var onStill = stuck.Where(s => s.hold && Done(s.by)).ToList();
                var pick = yielding.Count > 0 ? yielding[yielding.Count - 1] : onStill.Count > 0 ? onStill[0] : stuck[0];
                live[pick.w].Forced = true;
            }
            return null;
        }

        /// <summary>When the last of them gets there, with these waits (for measuring a plan against playing them all at once).</summary>
        public static float PlanLength(IReadOnlyList<Walker> walkers, IReadOnlyList<float[]> waits)
        {
            var longest = 0f;
            for (var i = 0; i < walkers.Count; i++)
            {
                var total = walkers[i].Steps.Sum() + (i < waits.Count ? waits[i].Sum() : 0);
                longest = Math.Max(longest, total);
            }
            return longest;
        }
    }
}
