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

        // -------------------------------------------------------------- Separados

        /// <summary>A Separados group's start from La Lata on foot, against the usual start's: SplitNear to SplitFar of it.</summary>
        private const double SplitNear = 0.8;
        private const double SplitFar = 1.3;
        /// <summary>
        /// A Separados group's start this far at least (tiles) from any of the dead, the first that has room: as the usual
        /// start (a Saqueo's nearest dead 12 tiles away at the least, 18–21 the median; Gameplay's probe, 20/11), down to the
        /// Travesía's (CrossingClear). At 5 only, as the contract had it, each group began with 2 or 3 of the dead within 10.
        /// </summary>
        private static readonly IReadOnlyList<int> SplitClear = new[] { 12, 10, 8, CrossingClear };
        /// <summary>
        /// Before a start nearer the dead, one nearer or farther from La Lata (SplitNearWide to SplitFarWide of the usual
        /// start): a town's dead stand all round its ring at 12 tiles.
        /// </summary>
        private const double SplitNearWide = 0.6;
        private const double SplitFarWide = 1.6;
        /// <summary>A group's soldiers within this of its start (tiles), and as clear of the dead as it less this.</summary>
        private const int SplitHuddle = 4;
        private const int SplitHuddleClear = 3;
        /// <summary>La Lata between the two starts: their walk to each other at least this share of their two walks to it.</summary>
        private const double SplitBetween = 0.8;
        /// <summary>
        /// The two walks to La Lata alike: the longer this many times the shorter at the most (Producto, 20/11: four of a
        /// factory's maps had its first start 56–72 steps away, its second 24–52, and the first fell twice as often). On 600
        /// of the maps (Gameplay's probe, 20/11): at 1.3 or 1.2, 99 % of them; at 1.1 too, but the walks some 4 steps shorter.
        /// </summary>
        private const double SplitEven = 1.2;

        /// <summary>
        /// G23.4 Separados (Arturo, cuadros 71 and 94; .dev-snapshots/gameplay/g23/SEPARADOS.md): each soldier's group, 0 or 1,
        /// by the squad's order (`owners`, each one's slot): by owner with two players who both have soldiers in it (slot 0's
        /// first); else the squad halved, the odd one in the first. Null with fewer than two: no groups.
        /// </summary>
        public static List<int>? SplitGroups(IReadOnlyList<int> owners)
        {
            if (owners.Count < 2) return null;
            var slots = owners.Distinct().OrderBy((o) => o).ToList();
            if (slots.Count > 1) return owners.Select((o) => o == slots[0] ? 0 : 1).ToList();
            var first = (owners.Count + 1) / 2;
            return owners.Select((o, i) => i < first ? 0 : 1).ToList();
        }

        /// <summary>
        /// Separados: the squad in two groups (SplitGroups, MissionState.Split) on either side of La Lata, once its zone is
        /// set (Runs.SeatZone): two tiles out of doors as far from La Lata on foot as the usual start (SplitNear to SplitFar of
        /// it), clear of the dead (SplitClear) and of fire, as far apart on foot as they can be (two sweeps, from the one
        /// farthest from the usual start), their walks to La Lata alike (SplitEven) before anything else; each group on the
        /// free tiles nearest its own, looking towards La Lata. On foot as the squad walks (Paths: hills and all; the ground
        /// alone left a country's start on its hill cut off; and one drops off a hill but never climbs it, so a walk to La
        /// Lata is the start's, not La Lata's to it). Nothing with one soldier, no La Lata or no room: a Saqueo of the stage,
        /// its reward by getting out. The sweep leaves its second start on the usual start's side, clear of the dead (the map
        /// put them away from it): the first group fell before getting to La Lata 149 times in 400 there, the second 59 (R1,
        /// 20/11). Which group goes where is `swap`'s (the map's seed), so it is not always the first player's. Then the dead
        /// kept off both (DeadOffTheGroups; `seed`, where to).
        /// </summary>
        public static void Split(GameState s, bool swap = false, int seed = 0)
        {
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive && u.CampaignId != null).ToList();
            var groups = SplitGroups(squad.Select((u) => u.Owner ?? 0).ToList());
            var bus = s.Mission.EvacZone.Select((z) => new Pos { X = (int)z.X, Y = (int)z.Y, Z = 0 }).FirstOrDefault((p) => Grid.InBounds(s, p.X, p.Y));
            if (groups == null || bus == null) return;
            var area = s.Width * s.Height;
            var walker = squad[0];
            var home = walker.Pos;
            Dictionary<int, int> From(Pos p)
            {
                walker.Pos = p;
                var cost = Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true).Cost.Where((kv) => kv.Key < area).ToDictionary((kv) => kv.Key, (kv) => kv.Value);
                walker.Pos = home;
                cost[Grid.TileIndex(s, p.X, p.Y)] = 0;
                return cost;
            }
            Pos At(int i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };
            var fromBus = From(bus);
            var usual = squad.Where((u) => u.Pos.Z == 0 && fromBus.ContainsKey(Grid.TileIndex(s, u.Pos.X, u.Pos.Y))).Select((u) => fromBus[Grid.TileIndex(s, u.Pos.X, u.Pos.Y)]).DefaultIfEmpty(0).Min();
            if (usual <= 0) return;
            var dead = s.Units.Where((u) => u.Alive && u.Team == Teams.Alien).Select((u) => u.Pos).ToList();
            var ours = new HashSet<Unit>(squad);
            // Each tile's nearest of the dead, as the crow flies (Chebyshev).
            int Clear(Pos p) => dead.Select((d) => Math.Max(Math.Abs(d.X - p.X), Math.Abs(d.Y - p.Y))).DefaultIfEmpty(int.MaxValue).Min();
            var fromHome = From(home);
            var froms = new Dictionary<int, Dictionary<int, int>>();
            Dictionary<int, int> FromTile(int i) => froms.TryGetValue(i, out var f) ? f : froms[i] = From(At(i));
            // La Lata's zone, and a start's walk to the nearest of it (`from`, the start's): as its group walks in.
            var zone = s.Mission.EvacZone.Select((z) => Grid.TileIndex(s, (int)z.X, (int)z.Y)).Where(fromBus.ContainsKey).Distinct().ToList();
            int Walk(Dictionary<int, int> from) => zone.Where(from.ContainsKey).Select((z) => from[z]).DefaultIfEmpty(int.MaxValue).Min();
            // Each tile's from the nearest of it: the same but on the hills, which one drops off and never climbs.
            var toZone = new Dictionary<int, int>();
            foreach (var z in zone)
                foreach (var kv in FromTile(z))
                    if (!toZone.TryGetValue(kv.Key, out var c) || kv.Value < c) toZone[kv.Key] = kv.Value;
            bool Alike(int w, int v) => w < int.MaxValue && v < int.MaxValue && Math.Max(w, v) <= SplitEven * Math.Min(w, v);
            (List<Queue<int>> Spots, double Apart)? Place(int clear, double near, double far, bool even)
            {
                bool Free(int i, int least)
                {
                    var p = At(i);
                    return fromBus.ContainsKey(i) && !Abilities.InEvacZone(s, p) && !Burning.At(s, p) && (s.Ground == null || i >= s.Ground.Count || s.Ground[i] != 2)
                        && (s.Storeys == null || i >= s.Storeys.Count || s.Storeys[i] == 0) && Flights.CanStopAt(s, p) && (Grid.UnitAt(s, p) is not Unit o || ours.Contains(o))
                        && Clear(p) >= least;
                }
                var band = fromBus.Keys.Where((i) => fromBus[i] >= near * usual && fromBus[i] <= far * usual && Free(i, clear)).OrderBy((i) => i).ToList();
                int Farthest(Dictionary<int, int> from, int like = -1) => band.Where((i) => from.ContainsKey(i) && (like < 0 || (toZone.TryGetValue(i, out var w) && Alike(w, like)))).OrderByDescending((i) => from[i]).ThenBy((i) => i).DefaultIfEmpty(-1).First();
                (List<Queue<int>> Spots, double Apart)? Pair(int a, int b)
                {
                    if (a < 0 || b < 0 || a == b) return null;
                    var taken = new HashSet<int>();
                    // By group: each one's tiles.
                    var spots = new List<Queue<int>> { new Queue<int>(), new Queue<int>() };
                    foreach (var (start, group) in new[] { (a, swap ? 1 : 0), (b, swap ? 0 : 1) })
                    {
                        var walk = FromTile(start);
                        var count = groups.Count((g) => g == group);
                        var least = Math.Max(CrossingClear, clear - SplitHuddleClear);
                        var mine = walk.Keys.Where((i) => Grid.Distance(At(i), At(start)) <= SplitHuddle && Free(i, least) && !taken.Contains(i)).OrderBy((i) => walk[i]).ThenBy((i) => i).Take(count).ToList();
                        if (mine.Count < count) return null;
                        taken.UnionWith(mine);
                        spots[group] = new Queue<int>(mine);
                    }
                    // How far apart on foot against their two walks to La Lata: 1 with La Lata on the way between them. With
                    // `even`, the walks to it as they walk them (and alike); else from it, as it was.
                    if (!even) return (spots, FromTile(a)[b] / (double)(fromBus[a] + fromBus[b]));
                    var busAt = Grid.TileIndex(s, bus.X, bus.Y);
                    if (!Alike(Walk(FromTile(a)), Walk(FromTile(b))) || !FromTile(a).TryGetValue(b, out var ab) || !FromTile(a).TryGetValue(busAt, out var ga) || !FromTile(b).TryGetValue(busAt, out var gb)) return null;
                    return (spots, ab / (double)(ga + gb));
                }
                var first = Farthest(fromHome);
                if (first < 0) return null;
                var second = Farthest(FromTile(first));
                if (second < 0 || second == first) return null;
                // The first start, the farthest from the second.
                if (!even) return Pair(Farthest(FromTile(second)), second);
                // With `even`, one whose walk to La Lata is like the second's (by the walk from it, then by its own); else the
                // first as it comes and a second like it.
                var placed = Pair(Farthest(FromTile(second), Walk(FromTile(second))), second);
                if (placed != null && placed.Value.Apart >= SplitBetween) return placed;
                first = Farthest(FromTile(second));
                return first < 0 ? null : Pair(first, Farthest(FromTile(first), Walk(FromTile(first))));
            }
            // The clearest that leaves La Lata between them (SplitBetween: a city's at 12 tiles had both on one side, 27 steps
            // apart and 42 and 49 from it), at the usual distance from it or else wider, with their walks to it alike
            // (SplitEven) before anything; with none, as unlike as they come; with none still, the one farthest apart, the
            // clearer first.
            (List<Queue<int>> Spots, double Apart)? best = null;
            foreach (var even in new[] { true, false })
            {
                foreach (var (clear, near, far) in SplitClear.SelectMany((c) => new[] { (c, SplitNear, SplitFar), (c, SplitNearWide, SplitFarWide) }))
                {
                    var placed = Place(clear, near, far, even);
                    if (placed == null) continue;
                    if (placed.Value.Apart >= SplitBetween)
                    {
                        best = placed;
                        break;
                    }
                    if (!even && (best == null || placed.Value.Apart > best.Value.Apart)) best = placed;
                }
                if (best != null) break;
            }
            if (best == null) return;
            var spots = best.Value.Spots;
            for (var k = 0; k < squad.Count; k++)
            {
                squad[k].Pos = At(spots[groups[k]].Dequeue());
                squad[k].Facing = Los.FacingTo(squad[k].Pos, new Vec2 { X = bus.X, Y = bus.Y }) ?? squad[k].Facing;
            }
            s.Mission.Split = new[] { 0, 1 }.Select((g) => squad.Where((u, k) => groups[k] == g).Select((u) => u.Id).ToList()).ToList();
            DeadOffTheGroups(s, new[] { 0, 1 }.Select((g) => squad.Where((u, k) => groups[k] == g).ToList()).ToList(), fromBus, new Rng(unchecked((uint)seed ^ 0x5911u)));
        }

        /// <summary>The dead's groups (with a round) this far from a Separados group's middle at least, as from the usual start (Mapgen's PodDistance).</summary>
        private const int SplitPodGap = 16;
        /// <summary>Nor their rounds' stops this near it (Mapgen: 12 from the usual start).</summary>
        private const int SplitRound = 12;

        /// <summary>
        /// Separados (Arturo, cuadro 95: «Igualar los lados»): the dead kept off both groups as the map keeps them off the
        /// usual start, which is no one's now: a group of theirs with its first tile (its round's, where the map put it) nearer
        /// a group's middle than a third of the map's side (SplitPodGap at the least, as Mapgen's podGap) goes to a spot that
        /// is not, drawn as the map draws them (`rng`), as spread from the other groups as there is room for (Mapgen's
        /// podSpread, then 6, 3); the stops of their rounds nearer than SplitRound are left out; a lone one (LurkerSpots)
        /// nearer any soldier than LurkerClear goes to a tile drawn among those that are not, inside if it was inside. The R1
        /// of 20/11 had a group fall before getting to La Lata 36 times in 100 on the side with the dead, 12 on the other; with
        /// them moved to the nearest spot they stood at its edge, the nearest at 14 tiles on that side and 21 on the other,
        /// and fell 23 and 9. Only those on the ground; nothing else moves.
        /// </summary>
        private static void DeadOffTheGroups(GameState s, List<List<Unit>> groups, Dictionary<int, int> fromBus, Rng rng, int? podGap = null, int[]? corridor = null, int corridorGap = 0, int corridorClear = 0)
        {
            var side = Math.Max(s.Width, s.Height);
            var gap = podGap ?? Math.Max(SplitPodGap, (int)Js.Round(side / 3.0));
            var spreads = new[] { Math.Max(11, (int)Js.Round(side / 4.5)), 6, 3, 0 };
            var middles = groups.Select((g) => (X: g.Average((u) => u.Pos.X), Y: g.Average((u) => u.Pos.Y))).ToList();
            var ours = groups.SelectMany((g) => g).ToList();
            Pos At(int i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };
            double FromMiddles(Pos p) => middles.Min((m) => Js.Hypot(m.X - p.X, m.Y - p.Y));
            double FromOurs(Pos p) => ours.Min((u) => Js.Hypot(u.Pos.X - p.X, u.Pos.Y - p.Y));
            // G23.5: a way to keep clear too (`corridor`: tiles from it, by tile), its groups CorridorGap off it, the lone and the
            // stops of rounds corridorClear.
            int FromCorridor(Pos p) => corridor == null ? int.MaxValue : corridor[p.Y * s.Width + p.X];
            bool Inside(int i) => s.Ground != null && i < s.Ground.Count && s.Ground[i] == 2;
            // Where one of the dead can wait: on the ground the squad walks (from La Lata), out of its zone, off fire and
            // flights, nobody there.
            bool Free(int i)
            {
                var p = At(i);
                return fromBus.ContainsKey(i) && Grid.CanStandOn(Grid.KindAt(s, p)) && !(s.Ground != null && i < s.Ground.Count && s.Ground[i] == 4)
                    && !Abilities.InEvacZone(s, p) && !Burning.At(s, p) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null;
            }
            var tiles = fromBus.Keys.OrderBy((i) => i).ToList();
            List<int> Shuffled(List<int> xs)
            {
                var out_ = new List<int>(xs);
                for (var i = out_.Count - 1; i > 0; i--)
                {
                    var j = rng.Int(i + 1);
                    (out_[i], out_[j]) = (out_[j], out_[i]);
                }
                return out_;
            }
            var dead = s.Units.Where((u) => u.Alive && u.Team == Teams.Alien).ToList();
            foreach (var pod in s.Pods)
            {
                var members = pod.UnitIds.Select((id) => Grid.GetUnit(s, id)).Where((u) => u != null && u.Alive).Select((u) => u!).ToList();
                // The dead's only (G23.5: a camp's living keep their posts).
                if (members.Count == 0 || members.Any((u) => u.Pos.Z != 0 || !Content.Templates[u.Template].Undead)) continue;
                if (pod.Patrol.Count > 0)
                {
                    if (FromMiddles(pod.Patrol[0]) < gap || FromCorridor(pod.Patrol[0]) < corridorGap)
                    {
                        var anchors = s.Pods.Where((o) => o != pod && o.Patrol.Count > 0).Select((o) => o.Patrol[0]).ToList();
                        var was = members.Select((u) => u.Pos).ToList();
                        // Off their own tiles while their new ones are looked for.
                        foreach (var u in members) u.Pos = new Pos { X = -1, Y = -1, Z = 0 };
                        var room = Shuffled(tiles.Where((i) => Free(i) && FromMiddles(At(i)) >= gap && FromCorridor(At(i)) >= corridorGap).ToList());
                        List<int>? placed = null;
                        foreach (var spread in spreads)
                        {
                            foreach (var a in room.Where((i) => anchors.All((p) => Js.Hypot(p.X - At(i).X, p.Y - At(i).Y) >= spread)))
                            {
                                // Round it as Mapgen's Cluster: by the eight ways, within 3 of it, on its level.
                                var lvl = Js.At(s.Elev, a, 0);
                                var near = new List<int>();
                                var seen = new HashSet<int> { a };
                                var queue = new List<int> { a };
                                for (var head = 0; head < queue.Count && near.Count < members.Count; head++)
                                {
                                    var i = queue[head];
                                    if (Free(i) && Js.At(s.Elev, i, 0) == lvl) near.Add(i);
                                    foreach (var d in Grid.Dirs8)
                                    {
                                        int x = i % s.Width + d.X, y = i / s.Width + d.Y;
                                        if (!Grid.InBounds(s, x, y)) continue;
                                        var j = Grid.TileIndex(s, x, y);
                                        if (seen.Contains(j) || Js.Hypot(x - a % s.Width, y - a / s.Width) > 3) continue;
                                        seen.Add(j);
                                        if (Grid.IsWalkable(Grid.KindAt(s, At(j)))) queue.Add(j);
                                    }
                                }
                                if (near.Count < members.Count) continue;
                                placed = near;
                                break;
                            }
                            if (placed != null) break;
                        }
                        if (placed == null)
                        {
                            // No room: where they were.
                            for (var k = 0; k < members.Count; k++) members[k].Pos = was[k];
                            continue;
                        }
                        for (var k = 0; k < members.Count; k++) members[k].Pos = At(placed[k]);
                        pod.Patrol[0] = At(placed[0]);
                    }
                }
                else
                {
                    foreach (var u in members)
                    {
                        if (FromOurs(u.Pos) >= LurkerClear && FromCorridor(u.Pos) >= corridorClear) continue;
                        var was = Grid.TileIndex(s, u.Pos.X, u.Pos.Y);
                        bool Fits(int i)
                        {
                            var p = At(i);
                            return Free(i) && FromOurs(p) >= LurkerClear && FromCorridor(p) >= corridorClear && s.Mission.EvacZone.All((z) => Js.Hypot(z.X - p.X, z.Y - p.Y) >= LurkerBus)
                                && dead.All((o) => o == u || Js.Hypot(o.Pos.X - p.X, o.Pos.Y - p.Y) >= (o.PodId != null && s.Pods.Any((q) => q.Id == o.PodId && q.Patrol.Count > 0) ? LurkerPodGap : LurkerGap));
                        }
                        var fits = tiles.Where(Fits).ToList();
                        var alike = fits.Where((i) => Inside(i) == Inside(was)).ToList();
                        if (fits.Count > 0) u.Pos = At(rng.Pick(alike.Count > 0 ? alike : fits));
                    }
                }
            }
            // No round of theirs through a group's start.
            foreach (var pod in s.Pods)
            {
                if (pod.Patrol.Count < 2 || pod.UnitIds.Any((id) => Grid.GetUnit(s, id) is Unit u && !Content.Templates[u.Template].Undead)) continue;
                var kept = new List<Pos> { pod.Patrol[0] };
                // G23.5: nor across the way in and out of a camp, between its stops (the straight line, every tile of it).
                bool Across(Pos a, Pos b)
                {
                    if (corridor == null) return false;
                    var n = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
                    for (var k = 0; k <= n; k++)
                    {
                        var p = new Pos { X = (int)Js.Round(a.X + (b.X - a.X) * (double)k / Math.Max(1, n)), Y = (int)Js.Round(a.Y + (b.Y - a.Y) * (double)k / Math.Max(1, n)), Z = 0 };
                        if (Grid.InBounds(s, p.X, p.Y) && FromCorridor(p) < corridorClear) return true;
                    }
                    return false;
                }
                foreach (var p in pod.Patrol.Skip(1).Where((p) => FromMiddles(p) >= SplitRound && FromCorridor(p) >= corridorClear))
                    if (!Across(kept[kept.Count - 1], p)) kept.Add(p);
                pod.Patrol = kept;
                pod.PatrolIndex = 0;
            }
        }

        // ---------------------------------------------------------------- Saqueadores

        /// <summary>
        /// G23.5 Saqueadores (SAQUEADORES.md; how many waits for Arturo's cuadro): the living keeping the camp by the stage's
        /// difficulty, the Cabecilla first, then who sits by the fire; the first Saqueador walks the round.
        /// </summary>
        /// <summary>
        /// A run's survivor (MissionState.Survivor) with no pack, as one saved before cuadro 140 (M7): a recruit's, empty, as
        /// wide as the runs' and as tall as the squad's least (the upgrades'), and the knife. Nothing if they have one.
        /// </summary>
        public static void SurvivorPack(GameState s)
        {
            var vip = !string.IsNullOrEmpty(s.Mission.Survivor) ? Grid.GetUnit(s, s.Mission.Survivor!) : null;
            if (vip == null || vip.Pack != null || !(vip.Alive || vip.Aboard == true)) return;
            var rows = s.Units.Where((u) => u.CampaignId != null && u.Pack != null).Select((u) => u.Pack!.H).DefaultIfEmpty(Runs.PackH).Min();
            vip.Pack = new GearGrid { W = Runs.PackW, H = rows };
            vip.Kit ??= Gears.ClonePlaces(new Kit { Melee = new Gear { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Knife } });
        }

        /// <summary>
        /// A run's fight with gear that has no id (a survivor's knife of a save of the 0.14.3's first days: Red, 23/11, it fell on
        /// the floor and couldn't be taken up through the room): each numbered from the fight's own (RunFight.NextGear), on the
        /// units' places and packs and on the floor.
        /// </summary>
        public static void NumberGear(GameState s)
        {
            if (s.Run == null) return;
            void Number(Gear? g)
            {
                if (g != null && string.IsNullOrEmpty(g.Id)) g.Id = $"g{++s.Run.NextGear}";
            }
            foreach (var u in s.Units)
            {
                if (u.Kit != null) foreach (var g in new[] { u.Kit.Primary, u.Kit.Melee, u.Kit.Pistol, u.Kit.Vest }.Concat(u.Kit.Belt)) Number(g);
                if (u.Pack != null) foreach (var p in u.Pack.Items) Number(p.Gear);
            }
            foreach (var d in s.FloorGear ?? new List<DroppedGear>()) Number(d.Gear);
        }

        public static List<string> RaidersBand(int difficulty) => difficulty >= 3
            ? new List<string> { TemplateIds.Officer, TemplateIds.Trooper, TemplateIds.Lancer, TemplateIds.Trooper, TemplateIds.Trooper }
            : new List<string> { TemplateIds.Officer, TemplateIds.Trooper, TemplateIds.Lancer, TemplateIds.Trooper };

        /// <summary>The dead's groups at the start no nearer the camp's middle than this (Mapgen keeps them this far from where the squad comes in).</summary>
        public const int CampDeadGap = 20;

        /// <summary>
        /// G23.5 Saqueadores: the camp of the living the map was made with (GenOptions.Camp: the camping site's room of
        /// CampUses.Raiders and its marks), once La Lata's zone is set. Its bounds and crates in MissionState.Raiders, the
        /// crates among the pieces searched one by one (MissionState.Pieces, SearchPieces.StashCrate). Its guards
        /// (RaidersBand), each a group of its own, unaware (Unit.Guard): the first Saqueador on the round, two tiles out round
        /// the fence (its four corners where they can be stood on); the rest on the chairs by the fire looking as they do
        /// (GuardPosts.Fire), and with more of them than chairs, by the crates looking out (GuardPosts.Stash). Then the dead
        /// kept off the camp and off the squad's start alike (DeadOffTheGroups, CampDeadGap; `rng`, where to). Nothing without
        /// a camp on the map: a Saqueo of the stage.
        /// </summary>
        public static void PlaceCamp(GameState s, int difficulty, Rng rng)
        {
            var site = (s.Interiors ?? new List<InteriorSite>()).FirstOrDefault((x) => x.Kind == InteriorKinds.Camping && x.Rooms.Any((r) => r.Use == CampUses.Raiders));
            var room = site?.Rooms.First((r) => r.Use == CampUses.Raiders);
            if (site == null || room == null) return;
            bool In(InteriorDetail d) => d.Z == 0 && d.X >= room.X && d.Y >= room.Y && d.X < room.X + room.W && d.Y < room.Y + room.H;
            var crates = site.Dressing.Where((d) => d.Kind == CampDetailKinds.StashCrate && In(d)).Select((d) => Grid.TileIndex(s, d.X, d.Y)).OrderBy((i) => i).ToList();
            var chairs = site.Dressing.Where((d) => d.Kind == CampDetailKinds.CampChair && In(d)).OrderBy((d) => d.Y).ThenBy((d) => d.X).ToList();
            s.Mission.Raiders = new RaidersCamp { X = room.X, Y = room.Y, W = room.W, H = room.H, Crates = crates };
            s.Mission.Pieces = (s.Mission.Pieces ?? new List<int>()).Concat(crates).Distinct().OrderBy((i) => i).ToList();
            var middle = new Vec2 { X = room.X + room.W / 2, Y = room.Y + room.H / 2 };
            var next = s.Units.Select((u) => u.Id.StartsWith("a") && int.TryParse(u.Id.Substring(1), out var n) ? n : 0).DefaultIfEmpty(0).Max();
            bool Free(Pos p) => Grid.InBounds(s, p.X, p.Y) && Grid.CanStandOn(Grid.KindAt(s, p)) && Grid.UnitAt(s, p) == null && !Abilities.InEvacZone(s, p);
            double Facing(Pos at, int side) => Js.Atan2(Grid.Dirs4[side].Y, Grid.Dirs4[side].X);
            var guards = new List<Unit>();
            void Post(string template, Pos at, double facing, string post, List<Pos> round)
            {
                var id = $"a{++next}";
                var pod = new Pod { Id = $"rc{guards.Count + 1}", Active = false, UnitIds = new List<string> { id }, Patrol = round, PatrolIndex = 0 };
                var u = MakeUnit(id, template, Content.Templates[template].Name, null, at, pod.Id);
                u.Facing = facing;
                u.Guard = post;
                s.Units.Add(u);
                s.Pods.Add(pod);
                guards.Add(u);
            }
            var band = RaidersBand(difficulty);
            var walker = band.IndexOf(TemplateIds.Trooper);
            // One by the stash always (Producto, 20/11: the round and the stash's guard keep watch, the fire's look at it):
            // the last Saqueador after the round's, else the Cazador.
            var keeper = Enumerable.Range(0, band.Count).Where((k) => k != walker && band[k] == TemplateIds.Trooper).DefaultIfEmpty(band.IndexOf(TemplateIds.Lancer)).Last();
            // The round: two tiles out round the fence, its corners where one can stand, from the one nearest the middle's north-west.
            var corners = new[] { (room.X - 2, room.Y - 2), (room.X + room.W + 1, room.Y - 2), (room.X + room.W + 1, room.Y + room.H + 1), (room.X - 2, room.Y + room.H + 1) }
                .Select((c) => new Pos { X = c.Item1, Y = c.Item2, Z = 0 }).Where(Free).ToList();
            if (walker >= 0 && corners.Count >= 2)
            {
                var start = corners[0];
                Post(band[walker], start, Facing(start, 1), GuardPosts.Round, corners.Skip(1).Concat(new[] { start }).Select(Grid.ToPos).ToList());
            }
            var seated = 0;
            var stash = crates.Select((i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 }).ToList();
            // The stash's guard first (its post by a crate), then the rest to the chairs, by the stash too without one.
            foreach (var k in Enumerable.Range(0, band.Count).OrderBy((k) => k == keeper ? 0 : 1).ThenBy((k) => k))
            {
                if (k == walker && guards.Count > 0 && guards[0].Guard == GuardPosts.Round) continue;
                if (k != keeper && seated < chairs.Count)
                {
                    var chair = chairs[seated++];
                    var at = new Pos { X = chair.X, Y = chair.Y, Z = 0 };
                    if (Free(at))
                    {
                        Post(band[k], at, Facing(at, chair.Facing), GuardPosts.Fire, new List<Pos> { Grid.ToPos(at) });
                        continue;
                    }
                }
                // By the crate farthest from where the squad starts (La Lata is near it: the way in and out keeps off its eyes;
                // Red's R15, 20/11), on the free tile beside it farthest from the middle, looking out.
                var start = s.Units.FirstOrDefault((x) => x.Team == Teams.Xcom && x.CampaignId != null)?.Pos ?? new Pos();
                var by = stash.OrderByDescending((c) => Js.Hypot(c.X - start.X, c.Y - start.Y)).ThenBy((c) => c.Y).ThenBy((c) => c.X)
                    .Select((c) => Grid.Dirs8.Select((d) => new Pos { X = c.X + d.X, Y = c.Y + d.Y, Z = 0 })
                        .Where((p) => Free(p) && p.X > room.X && p.Y > room.Y && p.X < room.X + room.W - 1 && p.Y < room.Y + room.H - 1)
                        .OrderByDescending((p) => Math.Abs(p.X - middle.X) + Math.Abs(p.Y - middle.Y)).ThenBy((p) => p.Y).ThenBy((p) => p.X).FirstOrDefault())
                    .FirstOrDefault((p) => p != null);
                if (by == null) continue;
                Post(band[k], by, Los.FacingTo(new Pos { X = middle.X, Y = middle.Y, Z = 0 }, by) ?? 0, GuardPosts.Stash, new List<Pos> { Grid.ToPos(by) });
            }
            // The dead off the camp, as off where the squad comes in: off both (one moved off the camp went next to the squad's
            // start, R1 of 20/11).
            var bus = s.Mission.EvacZone.Select((z) => new Pos { X = (int)z.X, Y = (int)z.Y, Z = 0 }).FirstOrDefault((p) => Grid.InBounds(s, p.X, p.Y));
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive && u.CampaignId != null).ToList();
            var soldier = squad.FirstOrDefault();
            if (bus == null || soldier == null || guards.Count == 0) return;
            var home = soldier.Pos;
            soldier.Pos = bus;
            var area = s.Width * s.Height;
            var fromBus = Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Where((kv) => kv.Key < area).ToDictionary((kv) => kv.Key, (kv) => kv.Value);
            soldier.Pos = home;
            // The way in and out kept clear of the dead too (Producto, 20/11, after Red's R15: at the service area the sleeping
            // dead cut the hidden way, by their eyes and their bites): from the squad's start to the camp's inside, and from
            // there to La Lata, on foot (CampWay).
            // First off the camp and the start, as before; then off the way, where there is room (the camping is small).
            DeadOffTheGroups(s, new List<List<Unit>> { guards, squad }, fromBus, rng, CampDeadGap);
            DeadOffTheGroups(s, new List<List<Unit>> { guards, squad }, fromBus, rng, CampDeadGap, CampWay(s, soldier, room, bus), CampWayGap, CampWayClear);
        }

        /// <summary>G23.5: the dead's groups at the start no nearer the way in and out of the camp (CampWay) than this; the lone and the rounds' stops, CampWayClear.</summary>
        public const int CampWayGap = 12, CampWayClear = 8;

        /// <summary>
        /// G23.5: how far each tile is from the way in and out of the camp (Chebyshev, in tiles): the walk from where `walker`
        /// stands to the nearest floor of the camp's inside (`room` but its fence), and from there to La Lata's (`bus`) zone,
        /// nobody in the way. Null with no way.
        /// </summary>
        private static int[]? CampWay(GameState s, Unit walker, InteriorRoom room, Pos bus)
        {
            var home = walker.Pos;
            var inside = new List<Pos>();
            for (var y = room.Y + 1; y < room.Y + room.H - 1; y++)
                for (var x = room.X + 1; x < room.X + room.W - 1; x++)
                    if (Grid.CanStandOn(Grid.KindAt(s, new Pos { X = x, Y = y, Z = 0 }))) inside.Add(new Pos { X = x, Y = y, Z = 0 });
            var there = Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true);
            var into = inside.Where((p) => there.Cost.ContainsKey(Grid.NodeOf(s, p))).OrderBy((p) => there.Cost[Grid.NodeOf(s, p)]).ThenBy((p) => p.Y).ThenBy((p) => p.X).FirstOrDefault();
            if (into == null) return null;
            var way = Paths.BuildPath(s, walker, there, into) ?? new List<Pos>();
            walker.Pos = into;
            var back = Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true);
            walker.Pos = home;
            var zone = s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).Where((p) => back.Cost.ContainsKey(Grid.NodeOf(s, p))).OrderBy((p) => back.Cost[Grid.NodeOf(s, p)]).FirstOrDefault();
            if (zone != null) way.AddRange(Paths.BuildPath(s, walker, back, zone) ?? new List<Pos>());
            way.Add(home);
            // From every tile of it outwards, by the eight ways.
            var far = Enumerable.Repeat(int.MaxValue, s.Width * s.Height).ToArray();
            var queue = new List<int>();
            foreach (var p in way.Where((p) => Grid.InBounds(s, p.X, p.Y)))
            {
                var i = p.Y * s.Width + p.X;
                if (far[i] == 0) continue;
                far[i] = 0;
                queue.Add(i);
            }
            for (var head = 0; head < queue.Count; head++)
            {
                var i = queue[head];
                foreach (var d in Grid.Dirs8)
                {
                    int x = i % s.Width + d.X, y = i / s.Width + d.Y;
                    if (!Grid.InBounds(s, x, y)) continue;
                    var j = y * s.Width + x;
                    if (far[j] <= far[i] + 1) continue;
                    far[j] = far[i] + 1;
                    queue.Add(j);
                }
            }
            return far;
        }

        /// <summary>
        /// G23.5: a camp's guard posted (fire or stash: its group's one stop) and unaware, back to its post once nothing is
        /// heard, and there looking as its post does (Ai.KeepPost). Its facing at the post: its chair's, or out from the
        /// camp's middle by the stash.
        /// </summary>
        public static double? PostFacing(GameState s, Unit u, Pos post)
        {
            var camp = s.Mission.Raiders;
            if (camp == null) return null;
            if (u.Guard == GuardPosts.Fire)
            {
                foreach (var site in s.Interiors ?? new List<InteriorSite>())
                {
                    if (site.Kind != InteriorKinds.Camping) continue;
                    foreach (var d in site.Dressing)
                        if (d.Kind == CampDetailKinds.CampChair && d.X == post.X && d.Y == post.Y && d.Z == post.Z) return Js.Atan2(Grid.Dirs4[d.Facing].Y, Grid.Dirs4[d.Facing].X);
                }
            }
            return Los.FacingTo(new Pos { X = camp.X + camp.W / 2, Y = camp.Y + camp.H / 2, Z = 0 }, post);
        }

        // ---------------------------------------------------------------- the survivor (N7)

        /// <summary>N7 (Arturo, 22/11): the dead's groups this far from the survivor at the start, and from the way to them and back.</summary>
        public const int SurvivorDeadGap = 12, SurvivorWayGap = 10, SurvivorWayClear = 6;
        /// <summary>How far a survivor on a door or in a pass is moved, at most, to a tile of its own.</summary>
        public const int SurvivorShift = 8;

        /// <summary>
        /// N7 (Arturo, 22/11: «había aparecido en la misma casilla que una puerta, y al lado tenía una fila de por lo menos 15
        /// zombies… según se unía a tu grupo te veían todos»): a Rescate's survivor (also Mordido's and El herido's), after
        /// SeatZone. Never on a door, a window nor a pass (a tile shut on two sides: Rules.InPass): to the nearest free floor
        /// tile of its level that is none of them, SurvivorShift at most, its circle moved with it; and the dead's groups off
        /// them and off the squad, then off the way from the squad to them and from them to La Lata (as Saqueadores'
        /// camp: DeadOffTheGroups), drawn by their own seed. A Rescate with Cuervos keeps its living where they are.
        /// </summary>
        public static void SurvivorClear(GameState s, Rng rng)
        {
            var vip = s.Mission.Survivor != null ? Grid.GetUnit(s, s.Mission.Survivor) : null;
            if (vip == null || !vip.Alive) return;
            if (Rules.BadSpot(s, vip.Pos))
            {
                // Somewhere of its own building (the one it stands in, or by its door the ones beside it), out of the pits, that
                // the squad walks to (Producto's review of 23/11: not out in the street, nor in a room with no way in).
                var walker = s.Units.FirstOrDefault((u) => u.Team == Teams.Xcom && u.Alive && !string.IsNullOrEmpty(u.CampaignId));
                var walk = walker != null ? new HashSet<int>(Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true).Cost.Keys) : null;
                var own = Buildings.Of(s, vip.Pos);
                var homes = own >= 0 ? new HashSet<int> { own } : new HashSet<int>(Grid.Dirs4.Select((d) => Buildings.Of(s, new Pos { X = vip.Pos.X + d.X, Y = vip.Pos.Y + d.Y, Z = vip.Pos.Z })).Where((b) => b >= 0));
                bool Fits(Pos p) => (walk == null || walk.Contains(Grid.NodeOf(s, p))) && (homes.Count == 0 ? Buildings.Of(s, p) < 0 : homes.Contains(Buildings.Of(s, p)));
                var to = Nearest(s, vip.Pos, (p) => !Rules.BadSpot(s, p) && Grid.KindAt(s, p) == TileKinds.Floor && Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p) && Fits(p))
                    ?? Nearest(s, vip.Pos, (p) => !Rules.DoorSpot(s, p) && Grid.KindAt(s, p) == TileKinds.Floor && Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p) && Fits(p));
                if (to != null)
                {
                    var area = s.Mission.SearchArea;
                    if (area != null) area.Center = new Vec2 { X = Math.Max(0, Math.Min(s.Width - 1, area.Center.X + to.X - vip.Pos.X)), Y = Math.Max(0, Math.Min(s.Height - 1, area.Center.Y + to.Y - vip.Pos.Y)) };
                    vip.Pos = to;
                }
            }
            var bus = s.Mission.EvacZone.Select((z) => new Pos { X = (int)z.X, Y = (int)z.Y, Z = 0 }).FirstOrDefault((p) => Grid.InBounds(s, p.X, p.Y));
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive && u.CampaignId != null).ToList();
            var soldier = squad.FirstOrDefault();
            if (bus == null || soldier == null) return;
            var home = soldier.Pos;
            soldier.Pos = bus;
            var area_ = s.Width * s.Height;
            var fromBus = Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Where((kv) => kv.Key < area_).ToDictionary((kv) => kv.Key, (kv) => kv.Value);
            soldier.Pos = home;
            var groups = new List<List<Unit>> { new List<Unit> { vip }, squad };
            DeadOffTheGroups(s, groups, fromBus, rng, SurvivorDeadGap);
            DeadOffTheGroups(s, groups, fromBus, rng, SurvivorDeadGap, WayThrough(s, soldier, vip.Pos, bus), SurvivorWayGap, SurvivorWayClear);
        }

        /// <summary>
        /// The nearest place to `from` on its level, walked to (the steps the rules allow, the four ways, never into a pit),
        /// SurvivorShift steps at most, that `ok` (Producto's review of 23/11: by the eight ways over the grid it cut wall
        /// corners and went through doors).
        /// </summary>
        private static Pos? Nearest(GameState s, Pos from, Func<Pos, bool> ok)
        {
            var pits = s.Mission.Pits != null ? new HashSet<int>(s.Mission.Pits) : null;
            var seen = new HashSet<int> { Grid.NodeOf(s, from) };
            var queue = new List<(Pos P, int D)> { (from, 0) };
            for (var head = 0; head < queue.Count; head++)
            {
                var (p, d) = queue[head];
                if (d > 0 && ok(p)) return p;
                if (d >= SurvivorShift) continue;
                foreach (var dir in Grid.Dirs4)
                    foreach (var step in Paths.StepsFrom(s, p, dir.X, dir.Y))
                    {
                        var q = step.To;
                        var n = Grid.NodeOf(s, q);
                        if (q.Z != from.Z || (pits != null && pits.Contains(n)) || !seen.Add(n)) continue;
                        queue.Add((q, d + 1));
                    }
            }
            return null;
        }

        /// <summary>
        /// A way's distance field, by tile (as CampWay): on foot from the squad's start (`walker`'s place) to `to`, and from there
        /// to La Lata's nearest; every tile's distance to it by the eight ways. Null when `to` can't be walked to.
        /// </summary>
        private static int[]? WayThrough(GameState s, Unit walker, Pos to, Pos bus)
        {
            var home = walker.Pos;
            var there = Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true);
            // Beside them, where they can't be stood on.
            var into = new[] { to }.Concat(Grid.Dirs8.Select((d) => new Pos { X = to.X + d.X, Y = to.Y + d.Y, Z = to.Z }))
                .Where((p) => Grid.InBounds(s, p.X, p.Y) && there.Cost.ContainsKey(Grid.NodeOf(s, p))).OrderBy((p) => there.Cost[Grid.NodeOf(s, p)]).FirstOrDefault();
            if (into == null) return null;
            var way = Paths.BuildPath(s, walker, there, into) ?? new List<Pos>();
            walker.Pos = into;
            var back = Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true);
            walker.Pos = home;
            var zone = s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).Where((p) => back.Cost.ContainsKey(Grid.NodeOf(s, p))).OrderBy((p) => back.Cost[Grid.NodeOf(s, p)]).FirstOrDefault();
            if (zone != null) way.AddRange(Paths.BuildPath(s, walker, back, zone) ?? new List<Pos>());
            way.Add(home);
            way.Add(to);
            var far = Enumerable.Repeat(int.MaxValue, s.Width * s.Height).ToArray();
            var queue = new List<int>();
            foreach (var p in way.Where((p) => Grid.InBounds(s, p.X, p.Y)))
            {
                var i = p.Y * s.Width + p.X;
                if (far[i] == 0) continue;
                far[i] = 0;
                queue.Add(i);
            }
            for (var head = 0; head < queue.Count; head++)
            {
                var i = queue[head];
                foreach (var d in Grid.Dirs8)
                {
                    int x = i % s.Width + d.X, y = i / s.Width + d.Y;
                    if (!Grid.InBounds(s, x, y)) continue;
                    var j = y * s.Width + x;
                    if (far[j] <= far[i] + 1) continue;
                    far[j] = far[i] + 1;
                    queue.Add(j);
                }
            }
            return far;
        }

        // ---------------------------------------------------------------- Evacuación

        /// <summary>G23.6 Evacuación's civilians (cuadro 71: four), each in a building of its own of this many ground tiles at least.</summary>
        public const int Civilians = 4, CivilianFewest = 3, CivilianHouse = 9;
        /// <summary>
        /// A civilian's building this far from La Lata on foot, in tiles (EVACUACION.md: none at its door, none a chapter off). 15–60
        /// till Gameplay's R1 of 21/11 with Red's bot: nearer, the reward by seats 0/13/27/43 % went up (banco/evacuacion).
        /// </summary>
        public const int CivilianNear = 8, CivilianFar = 35, CivilianFarthest = 60;
        /// <summary>The dead's groups at least this far from each civilian's tile at the start, as off the squad's (DeadOffTheGroups; R1 of 21/11).</summary>
        public const int CivilianDeadGap = 10;
        /// <summary>
        /// Evacuación's horde (the trickle and the waves) only from this turn (R1 of 21/11: it came at turn 3 and killed 58 % of
        /// the civilians that died, and most of the squad that fell with 4 seats or more).
        /// </summary>
        public const int CivilianHordeFrom = 8;
        /// <summary>A civilian's tile at least this far from where the squad starts (not found from the start).</summary>
        public const int CivilianFromSquad = 12;
        /// <summary>The tiles of a building drawn to hide a civilian on, each tried for its walk to La Lata, before giving the building up.</summary>
        public const int CivilianTries = 4;

        /// <summary>
        /// G23.6 Evacuación (EVACUACION.md, cuadro 100): Civilians civilians, once La Lata's zone is set, each hidden in a
        /// building of its own (CivilianHouse ground tiles or more) whose floor walks to La Lata's zone in CivilianNear to
        /// CivilianFar tiles, CivilianFromSquad or more from the squad; the buildings as spread out as can be (each next the
        /// farthest from those taken, the first drawn), on a free floor tile of each drawn on `rng`. Nobody's, captive till
        /// found (CivilianFoundEvent), going by themselves (Automaton.Civilian), their names a soldier's; each with its search
        /// circle (a Rescate's survivor's) and its seed (`seed`'s and theirs: their panics). With fewer than CivilianFewest
        /// such buildings, none: a Saqueo of the stage.
        /// </summary>
        public static void PlaceCivilians(GameState s, Rng rng, int seed)
        {
            var bus = s.Mission.EvacZone.Select((z) => new Pos { X = (int)z.X, Y = (int)z.Y, Z = 0 }).FirstOrDefault((p) => Grid.InBounds(s, p.X, p.Y));
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive && u.CampaignId != null).ToList();
            if (bus == null || squad.Count == 0) return;
            var walker = squad[0];
            var home = walker.Pos;
            var area = s.Width * s.Height;
            var zone = s.Mission.EvacZone.Select((z) => Grid.TileIndex(s, (int)z.X, (int)z.Y)).ToHashSet();
            bool Free(int i) => s.Ground != null && i < s.Ground.Count && s.Ground[i] == 2 && Grid.KindAt(s, new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 }) == TileKinds.Floor
                && Grid.UnitAt(s, new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 }) == null && !zone.Contains(i);
            // Each building's floor that walks to La Lata in its band, as the civilian would walk from there: a tile of it
            // drawn, up to CivilianTries of them.
            // A tile's walk to La Lata, in tiles (null: none).
            int? WalkOf(int i)
            {
                walker.Pos = new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };
                var reach = Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true).Cost;
                walker.Pos = home;
                var walk = zone.Where((z) => reach.ContainsKey(z)).Select((z) => reach[z]).DefaultIfEmpty(int.MaxValue).Min();
                return walk == int.MaxValue ? null : (int?)(walk / Paths.CostOrtho);
            }
            var all = new List<(List<int> Floor, double X, double Y, int Walk)>();
            foreach (var tiles in Buildings.GroundTiles(s).Where((t) => t.Count >= CivilianHouse))
            {
                var floor = tiles.Where(Free).Where((i) => squad.All((u) => Math.Max(Math.Abs(u.Pos.X - i % s.Width), Math.Abs(u.Pos.Y - i / s.Width)) >= CivilianFromSquad)).OrderBy((i) => i).ToList();
                var tried = new List<int>();
                for (var k = 0; k < CivilianTries && tried.Count < floor.Count; k++)
                {
                    var i = floor.Where((x) => !tried.Contains(x)).ElementAt(rng.Int(floor.Count - tried.Count));
                    tried.Add(i);
                    var walk = WalkOf(i);
                    if (walk == null || walk < CivilianNear || walk > CivilianFarthest) continue;
                    all.Add((new List<int> { i }, floor.Average((x) => x % s.Width), floor.Average((x) => x / s.Width), walk.Value));
                    break;
                }
            }
            // Near (CivilianFar, R1 of 21/11) where the map has Civilians buildings so near; else as far as before (a service area).
            var near = all.Where((h) => h.Walk <= CivilianFar).ToList();
            var houses = (near.Count >= Civilians ? near : all).Select((h) => (h.Floor, h.X, h.Y)).ToList();
            if (houses.Count < CivilianFewest) return;
            var taken = new List<(List<int> Floor, double X, double Y)> { houses[rng.Int(houses.Count)] };
            while (taken.Count < Civilians && taken.Count < houses.Count)
                taken.Add(houses.Where((h) => !taken.Contains(h)).OrderByDescending((h) => taken.Min((t) => Js.Hypot(t.X - h.X, t.Y - h.Y))).First());
            var names = Content.SoldierNames.ToList();
            var used = s.Units.Select((u) => u.Name).ToHashSet();
            var spots = new List<CivilianSpot>();
            for (var k = 0; k < taken.Count; k++)
            {
                var at = taken[k].Floor[rng.Int(taken[k].Floor.Count)];
                var pos = new Pos { X = at % s.Width, Y = at / s.Width, Z = 0 };
                var free = names.Where((n) => !used.Contains(n.Name)).ToList();
                var who = free.Count > 0 ? free[rng.Int(free.Count)] : names[rng.Int(names.Count)];
                used.Add(who.Name);
                var u = MakeUnit($"c{k + 1}", TemplateIds.Civilian, who.Name, null, pos, null);
                u.Captive = true;
                u.Ap = 0;
                u.Moves = 0;
                u.Automaton = new Automaton { Piece = Automaton.Civilian };
                u.Facing = rng.Next() * 2 * Math.PI - Math.PI;
                // No appearance: their street clothes as they are, untinted (Producto, 20/11), Arte's to draw by the template.
                s.Units.Add(u);
                spots.Add(new CivilianSpot { Unit = u.Id, Area = Rules.CircleAround(s, pos, rng), Seed = unchecked(seed * 31 + k * 7919) });
            }
            s.Mission.Civilians = spots;
            // The dead's groups off each civilian and off the squad (R1 of 21/11), by the rules of the usual start, drawn by their own seed.
            walker.Pos = bus;
            var fromBus = Paths.ComputeReach(s, walker, 1_000_000, ignore: (_) => true).Cost.Where((kv) => kv.Key < area).ToDictionary((kv) => kv.Key, (kv) => kv.Value);
            walker.Pos = home;
            var groups = s.Units.Where(Rules.IsCivilian).Select((u) => new List<Unit> { u }).ToList();
            groups.Add(squad);
            DeadOffTheGroups(s, groups, fromBus, new Rng(unchecked((uint)seed ^ 0xc1ea)), CivilianDeadGap);
        }

        // ---------------------------------------------------------------- Guarida

        /// <summary>A Guarida's box from La Lata, on foot (Networking, R2: opened at the far end, the squad never got back): 15 to 25.</summary>
        public static readonly IReadOnlyList<int> LairWalk = new[] { 15, 25 };

        /// <summary>
        /// Where a Guarida's box goes (H5): a tile of floor inside a building (a room), with floor all round it, so that it
        /// can be opened from any side; anywhere on the ground with none indoors. At mid distance from La Lata (LairWalk on
        /// foot, the nearer its middle the better: Gameplay, 17/11); with no La Lata, the farthest from the squad's start, as
        /// it was. Null with nowhere at all.
        /// </summary>
        private static Vec2? LairSpot(MissionLayout layout)
        {
            var w = layout.Width;
            var steps = StepsFrom(layout, layout.Spawns.Select((p) => p.Y * w + p.X));
            var evac = layout.EvacZone.Count > 0 ? StepsFrom(layout, layout.EvacZone.Select((z) => z.Y * w + z.X).Where((i) => i >= 0 && i < layout.Tiles.Count)) : null;
            var mid = (LairWalk[0] + LairWalk[1]) / 2.0;
            double Score(int i)
            {
                if (evac == null) return -steps[i];
                var n = evac[i];
                if (n < 0) return 1e6;
                var off = n < LairWalk[0] ? LairWalk[0] - n : n > LairWalk[1] ? n - LairWalk[1] : 0;
                return off * 100 + Math.Abs(n - mid);
            }
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
                    if (best < 0 || Score(i) < Score(best)) best = i;
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

        // ------------------------------------------------------ the hospital only inside

        /// <summary>
        /// The dead of a map that is one building (H7; Gameplay, 16/11: about a third on each storey, in rooms, «patients in
        /// beds fit the place»): its groups and its lone dead, shared out over its storeys, the biggest first to the storey
        /// with fewest so far; up there each group in a room of its own, not the corridor (a storey's biggest), a ward first
        /// (MedicalUses.Ward), beside its beds (a bed is furniture: nobody stands on it) and then round them, its round that
        /// room. On the ground floor, where they were; a Guarida's guard by its box. As many as before.
        /// </summary>
        private static void UpstairsDead(MissionLayout layout, List<Unit> units, List<Pod> pods, Rng rng)
        {
            var storeys = layout.Storeys;
            var levels = layout.Levels;
            if (storeys == null || levels == null) return;
            var w = layout.Width;
            var area = w * layout.Height;
            if (storeys.Count != area) return;
            var floors = 1;
            while (floors <= levels.Count && storeys.Any((k) => k > floors)) floors++;
            if (floors < 2) return;
            var groups = pods
                .Select((p, k) => (Pod: p, K: k, Units: p.UnitIds.Select((id) => units.First((u) => u.Id == id)).ToList()))
                .Where((g) => g.Pod.Id != "pg" && g.Units.Count > 0 && g.Units.All((u) => Content.Templates[u.Template].Undead))
                .OrderByDescending((g) => g.Units.Count)
                .ThenBy((g) => g.K)
                .ToList();
            // Each storey's rooms: its floor joined side by side, not through doors, flights nor ladders.
            var rooms = new Dictionary<int, List<List<int>>>();
            var wards = new Dictionary<int, HashSet<int>>();
            var byBed = new Dictionary<int, HashSet<int>>();
            var taken = new HashSet<int>(units.Select((u) => u.Pos.Z * area + u.Pos.Y * w + u.Pos.X));
            var used = new HashSet<(int, int)>();
            for (var z = 1; z < floors; z++)
            {
                var level = levels[z - 1];
                if (level.Tiles.Count != area) continue;
                bool Floor(int i) => storeys[i] > z && level.Tiles[i] == TileKinds.Floor && !Flights.LongFlightTile(level.Tiles, level.Decor, w, i);
                var of = Enumerable.Repeat(-1, area).ToArray();
                var list = new List<List<int>>();
                for (var start = 0; start < area; start++)
                {
                    if (of[start] >= 0 || !Floor(start)) continue;
                    var tiles = new List<int> { start };
                    of[start] = list.Count;
                    for (var k = 0; k < tiles.Count; k++)
                    {
                        int x = tiles[k] % w, y = tiles[k] / w;
                        foreach (var d in Grid.Dirs4)
                        {
                            int nx = x + d.X, ny = y + d.Y;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= layout.Height) continue;
                            var j = ny * w + nx;
                            if (of[j] >= 0 || !Floor(j)) continue;
                            of[j] = list.Count;
                            tiles.Add(j);
                        }
                    }
                    list.Add(tiles);
                }
                // Not the corridor.
                if (list.Count > 1) list.Remove(list.OrderByDescending((r) => r.Count).ThenBy((r) => r[0]).First());
                rooms[z] = list;
                var sites = layout.Medical ?? new List<MedicalSite>();
                wards[z] = new HashSet<int>(sites.SelectMany((m) => m.Rooms ?? new List<MedicalRoom>()).Where((r) => r.Z == z && r.Use == MedicalUses.Ward).SelectMany((r) => Enumerable.Range(r.Y, r.H).SelectMany((y) => Enumerable.Range(r.X, r.W).Select((x) => y * w + x))));
                var beds = sites.SelectMany((m) => m.Dressing ?? new List<MedicalDetail>()).Where((d) => d.Z == z && (d.Kind == MedicalDetailKinds.CoveredBed || d.Kind == MedicalDetailKinds.BareBed)).Select((d) => (d.X, d.Y)).ToList();
                byBed[z] = new HashSet<int>(beds.SelectMany((b) => Grid.Dirs4.Select((d) => (b.X + d.X, b.Y + d.Y))).Where((p) => p.Item1 >= 0 && p.Item2 >= 0 && p.Item1 < w && p.Item2 < layout.Height).Select((p) => p.Item2 * w + p.Item1));
            }
            var count = new int[floors];
            foreach (var g in groups)
            {
                var placed = false;
                foreach (var z in Enumerable.Range(0, floors).OrderBy((k) => count[k]).ThenBy((k) => k))
                {
                    if (z == 0)
                    {
                        placed = true;
                        count[0] += g.Units.Count;
                        break;
                    }
                    if (!rooms.TryGetValue(z, out var list)) continue;
                    List<int> Free(List<int> room) => room.Where((i) => !taken.Contains(z * area + i)).ToList();
                    var fit = list.Select((r, k) => (R: r, K: k)).Where((r) => Free(r.R).Count >= g.Units.Count).ToList();
                    if (fit.Count == 0) continue;
                    // A room nobody has yet, a ward, first.
                    var best = fit.Where((r) => !used.Contains((z, r.K))).ToList();
                    if (best.Count == 0) best = fit;
                    var ward = best.Where((r) => r.R.Any(wards[z].Contains)).ToList();
                    if (ward.Count > 0) best = ward;
                    var (room, index) = best[rng.Int(best.Count)];
                    used.Add((z, index));
                    var free = Free(room);
                    // Beside its beds first, then round the first of them.
                    var first = free.Where(byBed[z].Contains).OrderBy((i) => i).ToList();
                    var anchor = first.Count > 0 ? first[0] : free.OrderBy((i) => i).First();
                    var spots = first.Concat(free.Where((i) => !byBed[z].Contains(i)).OrderBy((i) => Math.Max(Math.Abs(i % w - anchor % w), Math.Abs(i / w - anchor / w))).ThenBy((i) => i)).Take(g.Units.Count).ToList();
                    for (var k = 0; k < g.Units.Count; k++)
                    {
                        var u = g.Units[k];
                        taken.Remove(u.Pos.Z * area + u.Pos.Y * w + u.Pos.X);
                        u.Pos = new Pos { X = spots[k] % w, Y = spots[k] / w, Z = z };
                        taken.Add(z * area + spots[k]);
                    }
                    g.Pod.Patrol = new List<Pos> { new Pos { X = anchor % w, Y = anchor / w, Z = z } };
                    g.Pod.PatrolIndex = 0;
                    count[z] += g.Units.Count;
                    placed = true;
                    break;
                }
                if (!placed) count[0] += g.Units.Count;
            }
        }

        /// <summary>
        /// The caged (H9; Arturo, 17/11: «los encerrados van hacia las verjas»): one of the dead in about half a police
        /// station's cells, one at least, in every node (a Limpieza too: they come to the bars and are struck through them).
        /// Each a group of its own, awake from the start, on the first floor tile of its cell. The lone dead and the waves
        /// keep out of the cells still.
        /// </summary>
        private static void CagedDead(MissionLayout layout, List<Unit> units, List<Pod> pods, ref int alienIndex, Rng rng)
        {
            var cells = (layout.Interiors ?? new List<InteriorSite>()).SelectMany((x) => x.Rooms).Where((r) => r.Use == PoliceUses.Cells).ToList();
            if (cells.Count == 0) return;
            var held = cells.Where((_) => rng.Int(2) == 0).ToList();
            if (held.Count == 0) held.Add(cells[rng.Int(cells.Count)]);
            var w = layout.Width;
            var k = 0;
            foreach (var r in held)
            {
                var spot = Enumerable.Range(r.Y, r.H).SelectMany((y) => Enumerable.Range(r.X, r.W).Select((x) => new Pos { X = x, Y = y, Z = r.Z }))
                    .FirstOrDefault((p) => layout.Tiles[p.Y * w + p.X] == TileKinds.Floor && !units.Any((u) => Grid.SamePos(u.Pos, p)));
                if (spot == null) continue;
                var id = $"a{++alienIndex}";
                var pod = new Pod { Id = $"c{++k}", Active = true, UnitIds = new List<string> { id }, Patrol = new List<Pos>(), PatrolIndex = 0 };
                var u = MakeUnit(id, TemplateIds.Zombie, Content.Templates[TemplateIds.Zombie].Name, null, spot, pod.Id);
                u.Caged = true;
                units.Add(u);
                pods.Add(pod);
            }
        }

        /// <summary>A Guarida's guard (H5), by the stage's map seed: an Acorazado or a Chillón.</summary>
        public static string GuardOf(int seed)
        {
            return Math.Abs(seed) % 2 == 0 ? TemplateIds.Mec : TemplateIds.Sectoid;
        }

        /// <summary>
        /// G23.1 La llave (Arturo, 18/11, cuadros 71, 73 y 79; .dev-snapshots/gameplay/g23/LLAVE.md): the police station's
        /// armoury locked (MissionState.Locked: its door), all its furniture full (MissionState.Pieces), nobody in it; its key
        /// (MissionState.Key, a gear of the fight's) on one of the dead in uniform the squad can get to and not caged
        /// (MissionState.KeyHolder), a common one put in a policeman's uniform where none is. After OnlyWithinReach.
        /// </summary>
        public static void KeyArmory(GameState s, Rng rng)
        {
            var station = (s.Interiors ?? new List<InteriorSite>()).FirstOrDefault((x) => x.Kind == InteriorKinds.Police);
            var armory = station?.Rooms.FirstOrDefault((r) => r.Use == PoliceUses.Armory && r.Z == 0);
            if (station == null || armory == null || s.Run == null) return;
            s.Mission.KeyArmory = true;
            bool In(int x, int y) => x >= armory.X && y >= armory.Y && x < armory.X + armory.W && y < armory.Y + armory.H;
            // Its door (in its walls), shut and locked.
            var locked = new List<int>();
            for (var y = armory.Y - 1; y <= armory.Y + armory.H; y++)
            {
                for (var x = armory.X - 1; x <= armory.X + armory.W; x++)
                {
                    if (In(x, y) || !Grid.InBounds(s, x, y)) continue;
                    var i = Grid.TileIndex(s, x, y);
                    if (s.Tiles[i] != TileKinds.Door && s.Tiles[i] != TileKinds.DoorOpen) continue;
                    s.Tiles[i] = TileKinds.Door;
                    locked.Add(Grid.NodeOf(s, new Pos { X = x, Y = y, Z = 0 }));
                }
            }
            if (locked.Count == 0) return;
            s.Mission.Locked = locked;
            // All its furniture full.
            var pieces = s.Mission.Pieces ??= new List<int>();
            foreach (var d in station.Dressing.Where((d) => d.Z == 0 && In(d.X, d.Y) && SearchPieces.Police.Contains(d.Kind)))
            {
                var at = new Pos { X = d.X, Y = d.Y, Z = 0 };
                var node = Grid.NodeOf(s, at);
                if (SearchPieces.Standing(s, at, d.Kind) && !pieces.Contains(node)) pieces.Add(node);
            }
            // Nobody in it: the dead there out to the nearest free tiles of the station.
            var soldier = s.Units.FirstOrDefault((u) => u.Team == Teams.Xcom && u.Alive);
            var free = Enumerable.Range(0, s.Width * s.Height).Select((i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 })
                .Where((p) => !In(p.X, p.Y) && Grid.IsWalkable(Grid.KindAt(s, p)) && Flights.CanStopAt(s, p) && InteriorSite.UseAt(s, p) != null && InteriorSite.UseAt(s, p) != PoliceUses.Cells)
                .OrderBy((p) => Math.Abs(p.X - (armory.X + armory.W / 2)) + Math.Abs(p.Y - (armory.Y + armory.H / 2))).ThenBy((p) => Grid.NodeOf(s, p)).ToList();
            foreach (var u in s.Units.Where((u) => u.Alive && u.Team == Teams.Alien && u.Pos.Z == 0 && In(u.Pos.X, u.Pos.Y)).ToList())
            {
                var to = free.FirstOrDefault((p) => Grid.UnitAt(s, p) == null);
                if (to != null) u.Pos = to;
            }
            // Its key, on one of the dead in uniform the squad gets to.
            if (soldier == null) return;
            var reach = new HashSet<int>(Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Keys);
            bool Can(Unit u) => u.Alive && u.Team == Teams.Alien && !u.Captive && u.Template == TemplateIds.Zombie && reach.Contains(Grid.NodeOf(s, u.Pos))
                && InteriorSite.UseAt(s, u.Pos) != PoliceUses.Cells;
            var uniformed = s.Units.Where((u) => Can(u) && (u.Outfit == Outfits.Police || u.Outfit == Outfits.Military)).ToList();
            if (uniformed.Count == 0)
            {
                var common = s.Units.Where(Can).ToList();
                if (common.Count == 0) return;
                var dressed = rng.Pick(common);
                dressed.Outfit = Outfits.Police;
                uniformed.Add(dressed);
            }
            var holder = rng.Pick(uniformed);
            s.Mission.KeyHolder = holder.Id;
            var maker = new RunLoot.Maker(s.Run.NextGear);
            s.Mission.Key = maker.Make(GearKinds.Key, null)[0];
            s.Run.NextGear = maker.Next;
        }

        /// <summary>
        /// G23.3 El herido (Arturo, cuadros 71, 73 y 92; .dev-snapshots/gameplay/g23/HERIDO.md): the Rescate's survivor can't
        /// walk (MissionState.Wounded, Unit.Lame): no actions nor movement ever; carried to La Lata. Where they wait is the
        /// Rescate's own, on the ground, the squad's to walk to (OnlyWithinReach).
        /// </summary>
        public static void Wounded(GameState s)
        {
            if (s.Run == null || string.IsNullOrEmpty(s.Mission.ObjectiveUnit)) return;
            var w = Grid.GetUnit(s, s.Mission.ObjectiveUnit!);
            if (w == null) return;
            s.Mission.Wounded = true;
            w.Lame = true;
            w.Ap = 0;
            w.Moves = 0;
        }

        /// <summary>G23.2 Mordido (Arturo, cuadro 87: 10; cuadro 89, after R1 with the bots: «En cualquiera, con 15 turnos»): the squad's turns the bitten has from their finding.</summary>
        public const int BiteTurns = 15;

        /// <summary>
        /// G23.2 Mordido (Arturo, 19/11, cuadros 73 y 87; .dev-snapshots/gameplay/g23/MORDIDO.md): the Rescate's survivor
        /// bitten (MissionState.Bitten); the antiviral (MissionState.Antiviral, a gear of the fight's) in one medicine cabinet
        /// or shelf of the map (AntiviralAt), unmarked: one with something in it (MissionState.Pieces) if there is one that
        /// may hold it, else one made so. One that may: standing, searched from a tile the squad walks to, and in no room
        /// without a door (Producto, 19/11). After OnlyWithinReach.
        /// </summary>
        public static void Bitten(GameState s, Rng rng)
        {
            if (s.Run == null || string.IsNullOrEmpty(s.Mission.ObjectiveUnit)) return;
            var soldier = s.Units.FirstOrDefault((u) => u.Team == Teams.Xcom && u.Alive && u.CampaignId != null);
            if (soldier == null) return;
            var reach = new HashSet<int>(Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Keys);
            var rooms = (s.Medical ?? new List<MedicalSite>()).SelectMany((m) => m.Rooms ?? new List<MedicalRoom>()).ToList();
            bool Inside(MedicalRoom r, int x, int y) => x >= r.X && y >= r.Y && x < r.X + r.W && y < r.Y + r.H;
            // The room it stands in has a door in its walls (none: not in a room).
            bool Doored(Pos p)
            {
                var room = rooms.FirstOrDefault((r) => r.Z == p.Z && Inside(r, p.X, p.Y));
                if (room == null) return true;
                for (var y = room.Y - 1; y <= room.Y + room.H; y++)
                    for (var x = room.X - 1; x <= room.X + room.W; x++)
                    {
                        if (Inside(room, x, y) || !Grid.InBounds(s, x, y)) continue;
                        var kind = Grid.KindAt(s, new Pos { X = x, Y = y, Z = p.Z });
                        if (kind == TileKinds.Door || kind == TileKinds.DoorOpen) return true;
                    }
                return false;
            }
            var out_ = new HashSet<int>(s.Mission.OutOfReach ?? new List<int>());
            var can = new List<Pos>();
            for (var z = 0; z < Grid.LevelCount(s); z++)
                for (var y = 0; y < s.Height; y++)
                    for (var x = 0; x < s.Width; x++)
                    {
                        var p = new Pos { X = x, Y = y, Z = z };
                        var kind = Looting.PieceAt(s, p);
                        if (kind != SearchPieces.MedicalCabinet && kind != SearchPieces.MedicalShelf) continue;
                        if (!SearchPieces.Standing(s, p, kind) || out_.Contains(Grid.NodeOf(s, p)) || !Doored(p)) continue;
                        if (!Grid.Dirs8.Any((d) => new Pos { X = x + d.X, Y = y + d.Y, Z = z } is Pos q && Grid.InBounds(s, q.X, q.Y) && reach.Contains(Grid.NodeOf(s, q)) && Looting.ReachesToSearch(s, q, p))) continue;
                        can.Add(p);
                    }
            if (can.Count == 0) return;
            var pieces = s.Mission.Pieces ??= new List<int>();
            var full = can.Where((p) => pieces.Contains(Grid.NodeOf(s, p))).ToList();
            var at = rng.Pick(full.Count > 0 ? full : can);
            var node = Grid.NodeOf(s, at);
            if (!pieces.Contains(node)) pieces.Add(node);
            s.Mission.Bitten = true;
            s.Mission.AntiviralAt = node;
            var maker = new RunLoot.Maker(s.Run.NextGear);
            s.Mission.Antiviral = maker.Make(GearKinds.Antiviral, null)[0];
            s.Run.NextGear = maker.Next;
        }

        /// <summary>
        /// A run's fight (Red, R3 17/11): what there is to search that no soldier gets beside on foot from the squad is out of
        /// reach (MissionState.OutOfReach: never a spot to search), and no such car is a police car. A town's outline shuts up
        /// the ground nobody can get to (Mapgen's SealPockets) but not what stands on it, so a lot's cars were left whole in its
        /// wall. Not mended there: the step recordings' maps would change.
        /// </summary>
        /// <summary>
        /// Cuadro 122 (no abandoning a fight, and nobody left loses the game): the places a soldier could get to and never
        /// back to La Lata from (Paths.NoWayOut: a hollow below a hill's edge, walled in by cover), made pits nobody walks
        /// into (MissionState.Pits). The squad's starts never one (WayOutTests).
        /// </summary>
        public static void NoPits(GameState s)
        {
            var pits = Paths.NoWayOut(s);
            foreach (var u in s.Units.Where((o) => o.Team == Teams.Xcom && o.Alive)) pits.Remove(Grid.NodeOf(s, u.Pos));
            s.Mission.Pits = pits.Count > 0 ? pits.OrderBy((n) => n).ToList() : null;
        }

        /// <summary>
        /// Producto's review of 23/11: nobody but the squad in a pit once the fight is set out (one of the dead there could never
        /// be reached with a blade, and a Limpieza never won): to the nearest free place the squad can walk to, its own level
        /// first; a survivor's circle goes with them.
        /// </summary>
        public static void OutOfPits(GameState s)
        {
            if (s.Mission.Pits == null || s.Mission.Pits.Count == 0) return;
            var pits = new HashSet<int>(s.Mission.Pits);
            var soldier = s.Units.FirstOrDefault((u) => u.Team == Teams.Xcom && u.Alive && !string.IsNullOrEmpty(u.CampaignId));
            if (soldier == null) return;
            var walk = Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Keys.ToList();
            foreach (var u in s.Units.Where((o) => o.Alive && string.IsNullOrEmpty(o.CampaignId) && pits.Contains(Grid.NodeOf(s, o.Pos))).ToList())
            {
                Pos? best = null;
                var far = double.MaxValue;
                foreach (var n in walk)
                {
                    var p = Grid.PosOf(s, n);
                    if (!Flights.CanStopAt(s, p) || Grid.UnitAt(s, p) != null) continue;
                    var d = Grid.Distance(u.Pos, p) + 100 * Math.Abs(p.Z - u.Pos.Z);
                    if (d >= far) continue;
                    best = p;
                    far = d;
                }
                if (best == null) continue;
                var area = s.Mission.SearchArea;
                if (area != null && u.Id == s.Mission.Survivor) area.Center = new Vec2 { X = Math.Max(0, Math.Min(s.Width - 1, area.Center.X + best.X - u.Pos.X)), Y = Math.Max(0, Math.Min(s.Height - 1, area.Center.Y + best.Y - u.Pos.Y)) };
                u.Pos = best;
            }
        }

        public static void OnlyWithinReach(GameState s)
        {
            var soldier = s.Units.FirstOrDefault((u) => u.Team == Teams.Xcom && u.Alive && !u.Evacuated);
            if (soldier == null) return;
            var reach = new HashSet<int>(Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Keys);
            var area = s.Width * s.Height;
            var out_ = new List<int>();
            for (var z = 0; z <= (s.Levels?.Count ?? 0); z++)
            {
                for (var i = 0; i < area; i++)
                {
                    var p = new Pos { X = i % s.Width, Y = i / s.Width, Z = z };
                    if (Looting.SearchSpot(s, p) == null) continue;
                    var beside = Grid.Dirs8.Any((d) =>
                    {
                        var from = new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = z };
                        return Grid.InBounds(s, from.X, from.Y) && reach.Contains(Grid.NodeOf(s, from)) && Looting.ReachesToSearch(s, from, p);
                    });
                    if (!beside) out_.Add(Grid.NodeOf(s, p));
                }
            }
            if (out_.Count == 0) return;
            s.Mission.OutOfReach = out_;
            var police = s.Mission.PoliceCar ?? new List<int>();
            var kept = new List<int>();
            for (var k = 0; k + 1 < police.Count; k += 2)
            {
                if (out_.Contains(police[k + 1])) continue;
                kept.Add(police[k]);
                kept.Add(police[k + 1]);
            }
            s.Mission.PoliceCar = kept.Count > 0 ? kept : null;
        }
    }
}
