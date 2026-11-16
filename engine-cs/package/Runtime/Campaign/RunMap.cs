#nullable enable
// A chapter's node map (H1: .dev-snapshots/gameplay/runs/H0-DISENO.md §4, PLAN-RUNS.md §3.1; new in C#), made from the
// run's seed: 7 rows, rows 1–5 of 3 or 4 nodes, row 6 a Refuge every node of row 5 leads to, row 7 the boss. Each node
// leads to 1 or 2 of the next row and no two links cross; every node is reached and leads on. What can be seen of it,
// and the map a fight is played on.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class RunMap
    {
        public const int Rows = 7;
        /// <summary>Rows of nodes to choose from: 1 to 5.</summary>
        public const int NodeRows = 5;
        public const int RefugeRow = 6;
        public const int BossRow = 7;
        /// <summary>Rows seen past the one the squad is at, before the «sight» upgrade.</summary>
        public const int Sight = 2;

        /// <summary>
        /// The specials drawn on the maps: today's objectives, and since H5 the four new ones (NewSpecials: Asedio,
        /// Travesía, Sigilo, Guarida).
        /// </summary>
        public static readonly IReadOnlyList<string> Specials = new[]
        {
            NodeKinds.Elimination, NodeKinds.Recovery, NodeKinds.Sabotage, NodeKinds.Hack, NodeKinds.Rescue,
            NodeKinds.Hold, NodeKinds.Crossing, NodeKinds.Stealth, NodeKinds.Elite,
        };
        /// <summary>H5's four, on the maps since their rules are there (they are in Specials too).</summary>
        public static readonly IReadOnlyList<string> NewSpecials = new[] { NodeKinds.Hold, NodeKinds.Crossing, NodeKinds.Stealth, NodeKinds.Elite };

        /// <summary>What a node of rows 2–5 is, in percent: normal 45, special 25, stops 30 (Refuge 10, Trade 8, Event 7, Survivor 5).</summary>
        private static readonly IReadOnlyList<(string Kind, int Weight)> Mix = new[]
        {
            (NodeKinds.Normal, 45),
            ("special", 25),
            (NodeKinds.Refuge, 10),
            (NodeKinds.Trade, 8),
            (NodeKinds.Event, 7),
            (NodeKinds.Survivor, 5),
        };

        /// <summary>Rows with no service area: the first fights, made with knives only.</summary>
        public const int EarlyRows = 2;

        /// <summary>
        /// The hospital only inside on the maps (H7, Places.Hospital): off till Gameplay says (Arte draws its camera and its
        /// walls). On, a town's or a city's fight (not a sabotage nor the boss) is one, one in HospitalOdds by its map's seed.
        /// </summary>
        public const bool HospitalSites = false;
        public const int HospitalOdds = 5;

        /// <summary>Fights from row 2 with a modifier, in percent.</summary>
        public const int ModifierChance = 30;

        /// <summary>
        /// The places a fight is drawn in, and how often: a town or a city (by the map's seed, as Places.Of tells them),
        /// an industrial estate, a factory, a service area, the countryside. A sabotage and the boss, in a factory.
        /// </summary>
        private static readonly IReadOnlyList<(string Site, int Weight)> SiteMix = new[]
        {
            (Places.Town, 5),
            (Places.Estate, 2),
            (Places.Factory, 1),
            (Places.Roadside, 1),
            (Places.Country, 2),
        };

        // ---------------------------------------------------------------- the map

        /// <summary>The map of a run of `chapter`, all of it, from its seed.</summary>
        public static List<RunNode> Generate(int chapter, int seed) => Generate(chapter, seed, HospitalSites);

        /// <summary>Generate, with the hospital only inside or without it (`hospitals`: HospitalSites); the rest the same.</summary>
        public static List<RunNode> Generate(int chapter, int seed, bool hospitals)
        {
            var rng = new Rng(seed);
            var rows = new List<List<RunNode>>();
            for (var row = 1; row <= Rows; row++)
            {
                var count = row <= NodeRows ? 3 + rng.Int(2) : 1;
                var kind = row == BossRow ? NodeKinds.Boss : row == RefugeRow ? NodeKinds.Refuge : NodeKinds.Normal;
                rows.Add(Enumerable.Range(0, count).Select((col) => new RunNode { Id = $"n{row}-{col}", Row = row, Col = col, Kind = kind, Difficulty = DifficultyOf(row, chapter) }).ToList());
            }
            for (var r = 0; r < Rows - 1; r++)
            {
                foreach (var (i, j) in Links(rng, rows[r].Count, rows[r + 1].Count)) rows[r][i].Next.Add(rows[r + 1][j].Id);
            }
            var all = rows.SelectMany((x) => x).ToList();
            // Rows 2–5: never two stops in a row on a way, the Refuge of row 6 too (so row 5 is all fights).
            for (var r = 1; r < NodeRows; r++)
            {
                foreach (var n in rows[r])
                {
                    var after = all.Where((p) => p.Next.Contains(n.Id)).Any((p) => NodeKinds.IsStop(p.Kind));
                    n.Kind = Roll(rng, stops: !after && n.Row < NodeRows);
                }
            }
            TradeInReach(rng, rows, all);
            foreach (var n in all.Where((x) => IsFight(x.Kind)))
            {
                if (n.Row >= 2 && n.Kind != NodeKinds.Boss && rng.Int(100) < ModifierChance) n.Modifier = rng.Pick(Modifiers.All);
                n.Seed = (int)Math.Floor(rng.Next() * 2147483648.0);
                n.Site = n.Kind == NodeKinds.Sabotage || n.Kind == NodeKinds.Boss ? Places.Factory : Weighted(rng, SiteMix);
                // No service area in rows 1–2 (Red, 16/11: a long walk to La Lata with knives only): the countryside, drawn
                // as it was so the rest of the map stays the same.
                if (n.Site == Places.Roadside && n.Row <= EarlyRows) n.Site = Places.Country;
                // A town or a city is the map's seed's (Places.OldTownSeed): the site says the one it will be.
                if (n.Site == Places.Town) n.Site = Places.Of(MapOf(n)!);
                // The hospital only inside (H7), by the map's seed: nothing else drawn changes.
                if (hospitals && (n.Site == Places.Town || n.Site == Places.City) && Math.Abs(n.Seed.Value) % HospitalOdds == 0) n.Site = Places.Hospital;
            }
            return all;
        }

        /// <summary>Rows 1–2: 1, 3–4: 2, 5 on: 3; +1 per chapter after the first.</summary>
        public static int DifficultyOf(int row, int chapter)
        {
            return (row <= 2 ? 1 : row <= 4 ? 2 : 3) + Math.Max(0, chapter - 1);
        }

        /// <summary>A node with a fight (not a stop): today's and H5's objectives, the normal ones and the boss.</summary>
        public static bool IsFight(string kind) => !NodeKinds.IsStop(kind);

        /// <summary>
        /// The links between a row of `a` nodes and the next of `b`, as (i, j): a walk from the first pair to the last that
        /// moves on in one row, the other or both, so no two cross and every node has one. A node leads to 2 at most.
        /// </summary>
        private static List<(int I, int J)> Links(Rng rng, int a, int b)
        {
            var links = new List<(int I, int J)> { (0, 0) };
            int i = 0, j = 0, from = 1;
            // Whether from (i, j), with `d` links already from i, the walk can still reach b - 1 without a third link.
            bool Room(int ni, int nj, int d) => (2 - d) + (a - 1 - ni) * 2 >= b - 1 - nj;
            while (i < a - 1 || j < b - 1)
            {
                var moves = new List<(int Move, int Weight)>();
                if (i < a - 1 && j < b - 1 && Room(i + 1, j + 1, 1)) moves.Add((0, 2));
                if (j < b - 1 && from < 2 && Room(i, j + 1, from + 1)) moves.Add((1, 2));
                if (i < a - 1 && Room(i + 1, j, 1)) moves.Add((2, 1));
                var move = Weighted(rng, moves);
                if (move == 0) (i, j, from) = (i + 1, j + 1, 1);
                else if (move == 1) (j, from) = (j + 1, from + 1);
                else (i, from) = (i + 1, 1);
                links.Add((i, j));
            }
            return links;
        }

        /// <summary>What a node of rows 2–5 is (Mix); without `stops`, a fight, normal or special as often as the mix has them.</summary>
        private static string Roll(Rng rng, bool stops)
        {
            var kind = stops ? Weighted(rng, Mix) : Weighted(rng, Mix.Where((m) => !NodeKinds.IsStop(m.Kind)).ToList());
            return kind == "special" ? rng.Pick(Specials) : kind;
        }

        private static T Weighted<T>(Rng rng, IReadOnlyList<(T Value, int Weight)> table)
        {
            var roll = rng.Int(table.Sum((x) => x.Weight));
            foreach (var (value, weight) in table)
            {
                if (roll < weight) return value;
                roll -= weight;
            }
            return table[table.Count - 1].Value;
        }

        /// <summary>The nodes a node leads to, all the way.</summary>
        private static List<RunNode> Ahead(RunNode n, List<RunNode> all)
        {
            var seen = new List<RunNode>();
            var next = new List<RunNode> { n };
            while (next.Count > 0)
            {
                next = next.SelectMany((x) => x.Next).Distinct().Select((id) => all.First((x) => x.Id == id)).Where((x) => !seen.Contains(x)).ToList();
                seen.AddRange(next);
            }
            return seen;
        }

        /// <summary>
        /// A Trade in reach from wherever the run starts: while a row-1 node has none ahead, a fight of rows 2–4 whose
        /// neighbours aren't stops becomes one, the one ahead of the most of those starts; failing that, a stop ahead of
        /// one becomes a Trade.
        /// </summary>
        private static void TradeInReach(Rng rng, List<List<RunNode>> rows, List<RunNode> all)
        {
            var ahead = rows[0].ToDictionary((n) => n, (n) => Ahead(n, all).Where((x) => x.Row < NodeRows).ToList());
            bool Free(RunNode x) => IsFight(x.Kind) && x.Next.All((id) => IsFight(all.First((y) => y.Id == id).Kind)) && all.Where((p) => p.Next.Contains(x.Id)).All((p) => IsFight(p.Kind));
            while (true)
            {
                var missing = rows[0].Where((n) => !ahead[n].Any((x) => x.Kind == NodeKinds.Trade)).ToList();
                if (missing.Count == 0) return;
                var reach = missing.SelectMany((n) => ahead[n]).Distinct().Where(Free).Select((x) => (Node: x, Starts: missing.Count((n) => ahead[n].Contains(x)))).ToList();
                if (reach.Count > 0)
                {
                    var most = reach.Max((x) => x.Starts);
                    rng.Pick(reach.Where((x) => x.Starts == most).Select((x) => x.Node).ToList()).Kind = NodeKinds.Trade;
                    continue;
                }
                var stops = ahead[missing[0]].Where((x) => NodeKinds.IsStop(x.Kind)).ToList();
                if (stops.Count == 0) return;
                rng.Pick(stops).Kind = NodeKinds.Trade;
            }
        }

        // ---------------------------------------------------------- the run on it

        public static RunNode? Node(RunState run, string? id)
        {
            return id == null ? null : run.Nodes.FirstOrDefault((n) => n.Id == id);
        }

        /// <summary>The nodes the squad may go to next: row 1 at the start, then those the node it is at leads to.</summary>
        public static List<RunNode> Choices(RunState run)
        {
            var at = Node(run, run.At);
            return at == null ? run.Nodes.Where((n) => n.Row == 1).ToList() : at.Next.Select((id) => Node(run, id)).Where((n) => n != null).Select((n) => n!).ToList();
        }

        /// <summary>The last row in sight: two past the one the squad is at (0 before the first), and one more a «sight» level.</summary>
        public static int VisibleRows(CampaignState c)
        {
            var run = c.Run;
            if (run == null) return 0;
            return (Node(run, run.At)?.Row ?? 0) + Sight + Runs.Level(c, UpgradeIds.Sight);
        }

        /// <summary>Whether both players see what a node is: the engine says, so both see the same. One not seen is drawn dark.</summary>
        public static bool Visible(CampaignState c, RunNode n)
        {
            return n.Row <= VisibleRows(c);
        }

        /// <summary>The map a fight is played on: its site and its seed (null for a stop).</summary>
        public static MapSource? MapOf(RunNode n)
        {
            if (!IsFight(n.Kind) || n.Seed == null) return null;
            var seed = n.Seed.Value;
            switch (n.Site)
            {
                case Places.Estate:
                    return MapSource.Generated(Biomes.City, seed, Sites.Industrial);
                case Places.Factory:
                    return MapSource.Generated(Biomes.Facility, seed);
                case Places.Roadside:
                    return MapSource.Generated(Biomes.Wilds, seed, Sites.Roadside);
                case Places.Hospital:
                    return MapSource.Generated(Biomes.City, seed, Sites.Hospital);
                case Places.Country:
                    return MapSource.Generated(Biomes.Wilds, seed);
                default:
                    return MapSource.Generated(Biomes.City, seed);
            }
        }
    }
}
