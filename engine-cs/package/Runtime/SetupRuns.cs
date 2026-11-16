#nullable enable
// The runs' stages in Setup (H5 and H6; new in C#): what CreateMission does for an Asedio (La Lata late, waves every two
// turns), a Travesía (the squad at the far end from La Lata, the horde behind it), a Guarida (the reward's box in a room,
// its guard round it) and a boss (the unit to kill). The Sigilo is a recovery with a flag (MissionState.Stealth).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Setup
    {
        /// <summary>An Asedio's La Lata: on this turn (Lore.StageLine's holdTurns).</summary>
        public const int HoldTurns = 8;
        /// <summary>A Guarida's guard and its group: the guard first.</summary>
        public static readonly IReadOnlyList<string> GuardPod = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid };
        /// <summary>The squad's start this far at least (tiles) from any group of the dead, on a Travesía.</summary>
        private const int CrossingClear = 5;

        // ----------------------------------------------------------------- Asedio

        /// <summary>
        /// An Asedio's waves (H5): one every two turns till La Lata comes, each bigger, by every edge in groups of
        /// SiegeGroup at most; a Corredor with the second, a Hinchado with the third, an Acorazado and a Corredor with the
        /// fourth.
        /// </summary>
        public static List<Reinforcement> HoldWaves(int difficulty, int until, int first)
        {
            var d = Math.Max(1, Math.Min(4, difficulty));
            var out_ = new List<Reinforcement>();
            var side = 0;
            for (var k = 1; 2 * k <= until; k++)
            {
                var units = Enumerable.Repeat(TemplateIds.Zombie, 2 + d + 2 * k).ToList();
                if (k == 2) units.Add(TemplateIds.Xenoid);
                if (k == 3) units.Add(TemplateIds.Bloater);
                if (k >= 4) units.AddRange(new[] { TemplateIds.Mec, TemplateIds.Xenoid });
                for (var i = 0; i < units.Count; i += SiegeGroup)
                {
                    out_.Add(new Reinforcement { Id = $"r{first + out_.Count}", Turn = 2 * k, Pos = null, Units = units.Skip(i).Take(SiegeGroup).ToList(), Landed = false, Horde = true, Side = side++ % 4 });
                }
            }
            return out_;
        }

        // --------------------------------------------------------------- Travesía

        /// <summary>A layout tile one can stand on at the ground, inside what is played.</summary>
        private static bool GroundTile(MissionLayout layout, int i)
        {
            return Grid.IsWalkable(layout.Tiles[i]) && Js.At(layout.Elev, i, 0) == 0 && !(layout.Ground != null && i < layout.Ground.Count && layout.Ground[i] == 4);
        }

        /// <summary>Steps by the ground (four ways) from `from` to every tile it reaches; -1 where it doesn't.</summary>
        private static int[] StepsFrom(MissionLayout layout, IEnumerable<int> from)
        {
            var steps = Enumerable.Repeat(-1, layout.Width * layout.Height).ToArray();
            var queue = new Queue<int>();
            foreach (var i in from)
            {
                if (steps[i] >= 0) continue;
                steps[i] = 0;
                queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                int x = i % layout.Width, y = i / layout.Width;
                foreach (var d in Grid.Dirs4)
                {
                    int nx = x + d.X, ny = y + d.Y;
                    if (nx < 0 || ny < 0 || nx >= layout.Width || ny >= layout.Height) continue;
                    var j = ny * layout.Width + nx;
                    if (steps[j] >= 0 || !GroundTile(layout, j)) continue;
                    steps[j] = steps[i] + 1;
                    queue.Enqueue(j);
                }
            }
            return steps;
        }

        /// <summary>
        /// A Travesía (H5): the squad starts at the far end from La Lata, on the ground one can walk from there to it the
        /// farthest from its zone as the crow flies, the spawns round it, clear of the dead. As it was if the map has no La
        /// Lata, if its start is that far already, or if there is no room there.
        /// </summary>
        private static void FarSpawns(MissionLayout layout)
        {
            if (layout.EvacZone.Count == 0 || layout.Spawns.Count == 0) return;
            var w = layout.Width;
            var steps = StepsFrom(layout, layout.EvacZone.Select((z) => z.Y * w + z.X).Where((i) => i >= 0 && i < layout.Tiles.Count));
            int Gap(int x, int y) => layout.EvacZone.Min((z) => Math.Max(Math.Abs(z.X - x), Math.Abs(z.Y - y)));
            var dead = layout.Pods.SelectMany((p) => p.Tiles).ToList();
            bool Clear(int i) => !dead.Any((p) => Math.Max(Math.Abs(p.X - i % w), Math.Abs(p.Y - i / w)) < CrossingClear);
            var far = -1;
            for (var i = 0; i < steps.Length; i++) if (steps[i] > 0 && Clear(i) && (far < 0 || Gap(i % w, i / w) > Gap(far % w, far / w))) far = i;
            if (far < 0 || Gap(far % w, far / w) <= layout.Spawns.Min((p) => Gap(p.X, p.Y))) return;
            var near = StepsFrom(layout, new[] { far });
            var spots = Enumerable.Range(0, near.Length).Where((i) => near[i] >= 0 && Clear(i) && steps[i] > 0).OrderBy((i) => near[i]).ThenBy((i) => i).Take(layout.Spawns.Count).ToList();
            if (spots.Count < layout.Spawns.Count) return;
            layout.Spawns = spots.Select((i) => new Vec2 { X = i % w, Y = i / w }).ToList();
        }

        /// <summary>The edge nearest the squad's start (Sim.ChooseDropZone's sides: 0 top, 1 right, 2 bottom, 3 left).</summary>
        private static int StartSide(MissionLayout layout)
        {
            var x = layout.Spawns.Average((p) => p.X);
            var y = layout.Spawns.Average((p) => p.Y);
            var gaps = new[] { y, layout.Width - 1 - x, layout.Height - 1 - y, x };
            return Array.IndexOf(gaps, gaps.Min());
        }

        // ---------------------------------------------------------------- Guarida

        /// <summary>
        /// Where a Guarida's box goes (H5): a tile of floor inside a building, the farthest from the squad's start, with
        /// floor all round it, so that it can be opened from any side; anywhere on the ground with none indoors. Null with
        /// nowhere at all.
        /// </summary>
        private static Vec2? LairSpot(MissionLayout layout)
        {
            var w = layout.Width;
            var steps = StepsFrom(layout, layout.Spawns.Select((p) => p.Y * w + p.X));
            var taken = new HashSet<int>(layout.Pods.SelectMany((p) => p.Tiles).Select((p) => p.Y * w + p.X).Concat(layout.EvacZone.Select((p) => p.Y * w + p.X)));
            bool Open(int i) => Grid.Dirs4.All((d) =>
            {
                int x = i % w + d.X, y = i / w + d.Y;
                return x >= 0 && y >= 0 && x < w && y < layout.Height && GroundTile(layout, y * w + x);
            });
            int? Best(bool indoors)
            {
                var best = -1;
                for (var i = 0; i < steps.Length; i++)
                {
                    if (steps[i] <= 0 || taken.Contains(i) || layout.Tiles[i] != TileKinds.Floor || !Open(i)) continue;
                    if (indoors && !(layout.Ground != null && i < layout.Ground.Count && layout.Ground[i] == 2)) continue;
                    if (best < 0 || steps[i] > steps[best]) best = i;
                }
                return best >= 0 ? best : (int?)null;
            }
            var at = Best(true) ?? Best(false);
            return at != null ? new Vec2 { X = at.Value % w, Y = at.Value / w } : null;
        }

        /// <summary>
        /// A Guarida (H5): the reward's box (a weapons crate on the map) on its spot, and its guard and group on the floor
        /// nearest it, a group of their own, unaware. The box's tile in MissionState.lair.
        /// </summary>
        private static Pos? PlaceLair(MissionLayout layout, string guard, List<Unit> units, List<Pod> pods, ref int alienIndex)
        {
            var spot = LairSpot(layout);
            if (spot == null) return null;
            var w = layout.Width;
            var box = spot.Y * w + spot.X;
            layout.Tiles[box] = TileKinds.Low;
            if (layout.Decor != null && box < layout.Decor.Count) layout.Decor[box] = Decor.DecorCode(DecorKinds.LootWeapons);
            var near = StepsFrom(layout, Grid.Dirs4.Select((d) => (spot.Y + d.Y) * w + spot.X + d.X).Where((i) => i >= 0 && i < layout.Tiles.Count && GroundTile(layout, i)));
            var used = new HashSet<int>(units.Select((u) => u.Pos.Y * w + u.Pos.X).Concat(layout.Spawns.Select((p) => p.Y * w + p.X)));
            var members = new[] { guard }.Concat(GuardPod).ToList();
            var tiles = Enumerable.Range(0, near.Length).Where((i) => near[i] >= 0 && !used.Contains(i)).OrderBy((i) => near[i]).ThenBy((i) => i).Take(members.Count).ToList();
            var pod = new Pod { Id = "pg", Active = false, UnitIds = new List<string>(), Patrol = new List<Pos>(), PatrolIndex = 0 };
            for (var k = 0; k < tiles.Count; k++)
            {
                var id = $"a{++alienIndex}";
                var u = MakeUnit(id, members[k], Content.Templates[members[k]].Name, null, new Pos { X = tiles[k] % w, Y = tiles[k] / w, Z = 0 }, pod.Id);
                // Facing the box it keeps.
                u.Facing = Los.FacingTo(u.Pos, spot) ?? u.Facing;
                units.Add(u);
                pod.UnitIds.Add(id);
            }
            if (pod.UnitIds.Count > 0) pods.Add(pod);
            return new Pos { X = spot.X, Y = spot.Y, Z = 0 };
        }

        /// <summary>A Guarida's guard (H5), by the stage's map seed: an Acorazado or a Chillón.</summary>
        public static string GuardOf(int seed)
        {
            return Math.Abs(seed) % 2 == 0 ? TemplateIds.Mec : TemplateIds.Sectoid;
        }
    }
}
