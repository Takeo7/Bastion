using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bastion.Client.Effects;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// Reinforcements and the horde arriving (arrivals.ts): the dead stagger in from the edge of the map, raiders run in, a
    /// pickup's headlights flaring at the edge behind the first of them. Each comes in along a real way from the nearest
    /// tile of the edge it can walk in by (the rules' own, Paths.ComputeReach), round where the others arriving with it
    /// will stand if it can; those going deepest set off first and the rest after them, none through another
    /// (WalkPlan.PlanHasted). Whoever has no such way in (far from any edge, or walled off from it) gets up out of a cloud
    /// of dust where it is. UI says who, by which way and when; the figures and the effects are Arte's (UnitViews.Arrive,
    /// Rise, Fx.Dust, Headlights).
    /// </summary>
    public static class Arrivals
    {
        /// <summary>Tiles in from an edge that a unit still walks in from it (its way's cost, a straight one).</summary>
        const int EdgeWalk = 7;
        /// <summary>Seconds per tile: the dead stagger, the living run (as a move).</summary>
        const float DeadPace = 0.42f;
        const float LivingPace = 0.16f;
        /// <summary>No arrival walk lasts longer than this (waiting for those before it aside), so the turn keeps moving.</summary>
        const float Longest = 2.2f;
        /// <summary>A diagonal step takes this much longer than a straight one.</summary>
        const float Diagonal = 1.4f;

        /// <summary>One arrival's way in: from the edge (just outside its tile) through each tile to its own.</summary>
        sealed class WayIn
        {
            public Unit U;
            public bool Zombie;
            /// <summary>The tiles, the edge's first, the unit's own last.</summary>
            public List<Pos> Tiles;
            /// <summary>Out of the map from the edge tile (tiles: x east, y south).</summary>
            public Vector2Int Out;
        }

        /// <summary>Plays the arrival of `arrived` (units the squad sees, not yet in the state); done when they all stand on their tiles.</summary>
        public static async Awaitable Play(UnitViews units, GameState s, IReadOnlyList<Unit> arrived, Func<float, Awaitable> wait, CancellationToken ct)
        {
            if (units == null || arrived.Count == 0) return;
            var fx = Fx.Current;
            var engine = false;
            var ways = new List<WayIn>();
            var risers = new List<Unit>();
            // Each way in round where the others come to stand (the state hasn't got them yet); through them only if there
            // is no other (the plan has it go by them first, or to one side).
            var crowd = Events.CloneState(s);
            crowd.Units.AddRange(arrived);
            foreach (var u in arrived)
            {
                var way = Way(crowd, u) ?? Way(s, u);
                if (way != null) ways.Add(way);
                else risers.Add(u);
            }
            // The deepest in first, so that nobody is left standing in the way of those still to come by them.
            ways = ways.OrderByDescending(w => w.Tiles.Count).ToList();
            // At `haste` times their pace (quicker if they would take too long all together: WalkPlan.PlanHasted).
            IReadOnlyList<WalkPlan.Walker> WalkersAt(float haste) => ways.Select(w =>
            {
                var pace = (w.Zombie ? DeadPace : LivingPace) * Mathf.Min(1, Longest / Natural(w)) / haste;
                var steps = new List<float> { 0.5f * pace };
                for (var k = 1; k < w.Tiles.Count; k++) steps.Add(pace * (IsDiagonal(w.Tiles[k - 1], w.Tiles[k]) ? Diagonal : 1));
                // Just outside its edge tile: one key for all those coming in by it (they don't hold each other up there).
                var outside = -1 - Grid.NodeOf(s, w.Tiles[0]);
                var corners = new List<(int, int)?> { null };
                for (var k = 0; k < w.Tiles.Count; k++)
                {
                    var t = w.Tiles[k];
                    var next = k + 1 < w.Tiles.Count ? w.Tiles[k + 1] : null;
                    corners.Add(next == null || next.X == t.X || next.Y == t.Y ? ((int, int)?)null
                        : (Grid.NodeOf(s, new Pos { X = next.X, Y = t.Y, Z = t.Z }), Grid.NodeOf(s, new Pos { X = t.X, Y = next.Y, Z = t.Z })));
                }
                return new WalkPlan.Walker
                {
                    Tiles = new[] { outside }.Concat(w.Tiles.Select(t => Grid.NodeOf(s, t))).ToArray(),
                    Steps = steps.ToArray(),
                    Corners = corners.Take(w.Tiles.Count + 1).ToArray(),
                };
            }).ToList();
            Debug.Log($"[llegada] {ways.Count} entran andando ({string.Join(", ", ways.Select(w => $"{w.U.Id} {w.Tiles.Count} casillas"))}), {risers.Count} se levantan");
            var plans = WalkPlan.PlanHasted(WalkersAt);
            var walkers = WalkersAt(plans.Length > 0 ? plans[0].Haste : 1);

            async Awaitable Walk(int i)
            {
                var w = ways[i];
                var plan = plans[i];
                await wait(plan.Waits.Length > 0 ? plan.Waits[0] : 0);
                var edge = MapSpace.StandAt(s, w.Tiles[0]);
                var out_ = new Vector3(w.Out.x, 0, -w.Out.y);
                var from = edge + out_ * (0.5f * MapSpace.Tile);
                fx?.Dust(from);
                if (!w.Zombie && !engine)
                {
                    engine = true;
                    fx?.Headlights(from + out_ * (1.5f * MapSpace.Tile), -out_);
                }
                var points = new List<Vector3> { from };
                points.AddRange(w.Tiles.Select(t => MapSpace.StandAt(s, t)));
                var waits = new List<float> { 0 };
                waits.AddRange(plan.Waits.Skip(1));
                await units.Arrive(w.U, points, walkers[i].Steps, waits, plan.Passes, ct);
            }

            async Awaitable Rise(int i)
            {
                var u = risers[i];
                await wait(i * 0.2f);
                fx?.Dust(MapSpace.StandAt(s, u.Pos), 9);
                await units.Rise(u, ct);
            }

            var all = Enumerable.Range(0, ways.Count).Select(Walk).Concat(Enumerable.Range(0, risers.Count).Select(Rise)).ToList();
            foreach (var a in all) await a;
        }

        static bool IsDiagonal(Pos a, Pos b) => a.X != b.X && a.Y != b.Y;

        /// <summary>The seconds a way in takes at its pace, uncapped.</summary>
        static float Natural(WayIn w)
        {
            var tiles = 0.5f;
            for (var k = 1; k < w.Tiles.Count; k++) tiles += IsDiagonal(w.Tiles[k - 1], w.Tiles[k]) ? Diagonal : 1;
            return tiles * (w.Zombie ? DeadPace : LivingPace);
        }

        /// <summary>
        /// Unit `u`'s way in from the edge of the map: the cheapest way (the rules' own) from a tile of the edge on the ground
        /// to its tile, EdgeWalk at most, every step of it one the rules allow that way round; null if there is none (it
        /// gets up where it is).
        /// </summary>
        static WayIn Way(GameState s, Unit u)
        {
            var at = new Pos { X = u.Pos.X, Y = u.Pos.Y, Z = u.Pos.Z };
            if (at.Z != 0) return null;
            var reach = Paths.ComputeReach(s, u, EdgeWalk * Paths.CostOrtho, at, o => o.Id == u.Id);
            var best = -1;
            var cost = int.MaxValue;
            foreach (var pair in reach.Cost)
            {
                var p = Grid.PosOf(s, pair.Key);
                if (p.Z != 0 || pair.Value >= cost || (p.X != 0 && p.Y != 0 && p.X != s.Width - 1 && p.Y != s.Height - 1)) continue;
                best = pair.Key;
                cost = pair.Value;
            }
            if (best < 0) return null;
            // From the edge back to its tile, then each step checked the way it is walked (a drop one way is a climb the other).
            var tiles = new List<Pos>();
            for (int? n = best; n != null; n = reach.Prev.TryGetValue(n.Value, out var prev) ? prev : (int?)null) tiles.Add(Grid.PosOf(s, n.Value));
            for (var k = 1; k < tiles.Count; k++)
                if (Paths.StepBetween(s, tiles[k - 1], tiles[k]) == null) return null;
            var edge = tiles[0];
            var outs = new List<Vector2Int>();
            if (edge.X == 0) outs.Add(new Vector2Int(-1, 0));
            if (edge.X == s.Width - 1) outs.Add(new Vector2Int(1, 0));
            if (edge.Y == 0) outs.Add(new Vector2Int(0, -1));
            if (edge.Y == s.Height - 1) outs.Add(new Vector2Int(0, 1));
            // At a corner, out the side facing away from where it heads.
            var next = tiles.Count > 1 ? tiles[1] : null;
            var out_ = next != null ? outs.Aggregate((a, b) => a.x * (next.X - edge.X) + a.y * (next.Y - edge.Y) <= b.x * (next.X - edge.X) + b.y * (next.Y - edge.Y) ? a : b) : outs[0];
            return new WayIn { U = u, Zombie = Gaits.MovesDead(u.Template), Tiles = tiles, Out = out_ };
        }
    }
}
