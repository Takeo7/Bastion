#nullable enable
// A chapter's node map (H1: .dev-snapshots/gameplay/runs/H0-DISENO.md §4, PLAN-RUNS.md §3.1; new in C#), made from the
// run's seed: 7 rows, rows 1–5 of 3 or 4 nodes, row 6 a Refuge every node of row 5 leads to, row 7 the boss. Each node
// leads to 1 or 2 of the next row and no two links cross; every node is reached and leads on. A Refuge in reach every
// two rows (G22). What can be seen of it, and the map a fight is played on.
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
        /// Travesía, Sigilo, Guarida). Without «Arreglar la radio» since T2 (Arturo, 18/11: «se quita la etapa», and all of
        /// fixing with it): its share goes to the others. La llave since G23.1, from row KeyFrom only, always at a police
        /// station. Mordido since G23.2 (Arturo, cuadro 90: «Como está, y lo pruebo yo»), rows 2–5, always at the hospital.
        /// </summary>
        public static readonly IReadOnlyList<string> Specials = new[]
        {
            NodeKinds.Elimination, NodeKinds.Recovery, NodeKinds.Sabotage, NodeKinds.Rescue,
            NodeKinds.Hold, NodeKinds.Crossing, NodeKinds.Stealth, NodeKinds.Elite, NodeKinds.Key, NodeKinds.Bitten,
        };

        /// <summary>G23.1 La llave from this row (Producto, 19/11: its armoury full arms a squad at once; rows 1–2 are knives').</summary>
        public const int KeyFrom = 3;
        /// <summary>H5's four, on the maps since their rules are there (they are in Specials too).</summary>
        public static readonly IReadOnlyList<string> NewSpecials = new[] { NodeKinds.Hold, NodeKinds.Crossing, NodeKinds.Stealth, NodeKinds.Elite };

        /// <summary>
        /// What a node of rows 2–5 is, in percent: normal 60, special 25, stops 15 (Trade 8, Event 7). The Superviviente's 5
        /// went to the normal ones (N3, 17/11); the Refuge's 10 too, since the Refuges are placed where they are wanted
        /// (G22: RefugesInReach).
        /// </summary>
        private static readonly IReadOnlyList<(string Kind, int Weight)> Mix = new[]
        {
            (NodeKinds.Normal, 60),
            ("special", 25),
            (NodeKinds.Trade, 8),
            (NodeKinds.Event, 7),
        };

        /// <summary>
        /// G22 (Arturo, 18/11, cuadros 72 y 76: «por lo menos un refugio cada 2 líneas, de momento»; «algo intermedio»): a
        /// Refuge in row RefugeFirst (one, past the row-1 node with the most ways on), and one in rows RefugeFirst + 1 to
        /// RefugeSecond past every node of row RefugeFirst, so in reach from wherever the run went by then; the last, row 6's
        /// (RefugeRow). Some 3 a map (.dev-snapshots/gameplay/g21-g22/G22-VOLVER.md).
        /// </summary>
        public const int RefugeFirst = 2;
        public const int RefugeSecond = 4;

        /// <summary>
        /// G22's Refuges at most (Producto, 19/11: Arturo «no quería un mapa de paradas»; a map had three side by side in row
        /// 4 and five before row 6's): in one row, and in rows 2–5 together. Row 6's apart. RefugesCovering keeps them in
        /// reach as before (.dev-snapshots/gameplay/g23/REFUGIOS.md).
        /// </summary>
        public const int RefugesARow = 2;
        public const int RefugesBefore = 4;

        /// <summary>
        /// The rows a Rescate is kept in reach in from every start (RescueInReach; N3, 17/11: recruits come only from a
        /// Rescate, and drawn as a special there was none to reach in two chapters of three).
        /// </summary>
        public const int RescueLastRow = 4;

        /// <summary>
        /// Rescates with Cuervos (N2, Arturo 17/11: «a veces»; Producto: few and not at the start): from this row, one in
        /// GuardedOdds by the map's seed. The rest, and those an event marks, the survivor hidden alone.
        /// </summary>
        public const int GuardedFrom = 3;
        public const int GuardedOdds = 3;

        /// <summary>A survivor loose on a fight's map (N2, Arturo 17/11): one in LooseOdds, by the map's seed.</summary>
        public const int LooseOdds = 6;

        /// <summary>A number of the map's seed for a draw of its own (the hospital's is the seed itself, HospitalOdds).</summary>
        /// <summary>A day sky's share in 100 (cuadro 83, Producto 19/11): clear, overcast, and the rest dusk.</summary>
        public const int SkyClear = 50, SkyOvercast = 30;

        /// <summary>
        /// The day sky of `n`'s fight (Skies; cuadro 83: look only, not on the node's sheet), by its map's seed: none at
        /// night (the night rules), overcast in a storm, else SkyClear / SkyOvercast / the rest dusk. The same node, the same
        /// sky: the stage's road after it (UI's Settlement.Halt) takes it from here. Null for a stop.
        /// </summary>
        public static string? SkyOf(RunNode n)
        {
            if (!IsFight(n.Kind) || n.Seed == null) return null;
            if (n.Modifier == Modifiers.Night || n.Added == Modifiers.Night) return null;
            if (n.Modifier == Modifiers.Storm || n.Added == Modifiers.Storm) return Skies.Overcast;
            var d = SeedDraw(n.Seed.Value, 0x5c1e5) % 100;
            return d < SkyClear ? Skies.Clear : d < SkyClear + SkyOvercast ? Skies.Overcast : Skies.Dusk;
        }

        private static uint SeedDraw(int seed, uint salt) => unchecked(((uint)Math.Abs(seed) ^ salt) * 2654435761u) >> 16;

        /// <summary>Whether a Rescate drawn on the map has Cuervos (RunNode.Guarded): from GuardedFrom, one in GuardedOdds.</summary>
        public static bool GuardedRescue(RunNode n) => n.Kind == NodeKinds.Rescue && n.Row >= GuardedFrom && n.Seed != null && SeedDraw(n.Seed.Value, 0xc0e5) % GuardedOdds == 0;

        /// <summary>
        /// Whether a fight's map has a survivor loose on it (N2): one in LooseOdds of the fights that aren't a Rescate (whose
        /// survivor is sure) nor the boss, by the map's seed; no mark on the map, nor where to look.
        /// </summary>
        public static bool LooseSurvivor(RunNode n) => IsFight(n.Kind) && n.Kind != NodeKinds.Rescue && n.Kind != NodeKinds.Bitten && n.Kind != NodeKinds.Wounded && n.Kind != NodeKinds.Boss && n.Seed != null && SeedDraw(n.Seed.Value, 0x10e5e) % LooseOdds == 0;

        /// <summary>Rows with no service area: the first fights, made with knives only.</summary>
        public const int EarlyRows = 2;

        /// <summary>
        /// The hospital only inside on the maps (H7, Places.Hospital): on since Arte's camera and walls and UI's pass (16/11).
        /// A town's or a city's fight (not a sabotage, a Travesía nor the boss) is one, one in HospitalOdds by its map's seed.
        /// </summary>
        public const bool HospitalSites = true;
        public const int HospitalOdds = 5;

        /// <summary>
        /// The special maps (H7, H9: H9-INTERIORES.md), all together one fight in HospitalOdds (Arturo, 17/11), by the map's
        /// seed, each where a place like it was: the offices, the hospital, the police station and the plaza for a town or a
        /// city, the shed for an estate or a factory, the camping for the country, in turn by the seed. Never the boss; a
        /// Sabotaje and a Travesía only on one out of doors (the plaza, the camping; Gameplay, 16/11: a Travesía's far end
        /// makes no sense in one building). Null: as drawn.
        /// </summary>
        public static string? SpecialFor(RunNode n)
        {
            if (n.Seed == null || n.Kind == NodeKinds.Boss) return null;
            // Mordido's, always (G23.2: the one map with medicine cabinets and shelves enough).
            if (n.Kind == NodeKinds.Bitten) return Places.Hospital;
            var seed = Math.Abs(n.Seed.Value);
            if (seed % HospitalOdds != 0) return null;
            var outdoors = n.Kind == NodeKinds.Sabotage || n.Kind == NodeKinds.Crossing;
            var town = n.Site == Places.Town || n.Site == Places.City;
            // A sabotage (its place always a factory) on the plaza; a Travesía in a town or a city on the plaza too.
            var like = n.Kind == NodeKinds.Sabotage ? new[] { Places.Plaza, Places.Camping }
                : outdoors ? (town ? new[] { Places.Plaza } : n.Site == Places.Country ? new[] { Places.Camping } : new string[0])
                : town ? new[] { Places.Offices, Places.Hospital, Places.Police, Places.Plaza }
                : n.Site == Places.Estate || n.Site == Places.Factory ? new[] { Places.Shed }
                : n.Site == Places.Country ? new[] { Places.Camping }
                : new string[0];
            return like.Length > 0 ? like[seed / HospitalOdds % like.Length] : null;
        }

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
                    n.Kind = Roll(rng, stops: !after && n.Row < NodeRows, n.Row);
                }
                // The rows of a window rolled, the rest not yet: its Refuges (what comes after rolls round them). Row 2's,
                // one, past the row-1 node with the most ways on; then one in rows 3–4 past every node of row 2.
                if (r == RefugeFirst - 1)
                    foreach (var s in rows[0].OrderByDescending((n) => n.Next.Count))
                    {
                        if (rows[RefugeFirst - 1].Any((x) => x.Kind == NodeKinds.Refuge)) break;
                        RefugesInReach(rng, all, new List<RunNode> { s }, RefugeFirst, RefugeFirst);
                    }
                if (r == RefugeSecond - 1) RefugesCovering(rng, all, rows[RefugeFirst - 1].ToList(), RefugeFirst + 1, RefugeSecond);
            }
            TradeInReach(rng, rows, all);
            OneTrade(rng, rows, all);
            RescueInReach(rng, rows, all);
            foreach (var n in all.Where((x) => IsFight(x.Kind)))
            {
                if (n.Row >= 2 && n.Kind != NodeKinds.Boss && rng.Int(100) < ModifierChance) n.Modifier = rng.Pick(Modifiers.All);
                n.Seed = (int)Math.Floor(rng.Next() * 2147483648.0);
                n.Site = n.Kind == NodeKinds.Sabotage || n.Kind == NodeKinds.Boss ? Places.Factory : n.Kind == NodeKinds.Key ? Places.Police
                    // Mordido (G23.2): a town's or a city's, the hospital (SpecialFor) with its flag.
                    : n.Kind == NodeKinds.Bitten ? Places.Town : Weighted(rng, SiteMix);
                // No service area in rows 1–2 (Red, 16/11: a long walk to La Lata with knives only): the countryside, drawn
                // as it was so the rest of the map stays the same.
                if (n.Site == Places.Roadside && n.Row <= EarlyRows) n.Site = Places.Country;
                // A town or a city is the map's seed's (Places.OldTownSeed): the site says the one it will be.
                if (n.Site == Places.Town) n.Site = Places.Of(MapOf(n)!);
                // The special maps (H7, H9), by the map's seed: nothing else drawn changes.
                if (hospitals) n.Site = SpecialFor(n) ?? n.Site;
                if (GuardedRescue(n)) n.Guarded = true;
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
        private static string Roll(Rng rng, bool stops, int row)
        {
            var kind = stops ? Weighted(rng, Mix) : Weighted(rng, Mix.Where((m) => !NodeKinds.IsStop(m.Kind)).ToList());
            return kind == "special" ? rng.Pick(row >= KeyFrom ? Specials : Specials.Where((k) => k != NodeKinds.Key).ToList()) : kind;
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
        /// one becomes a Trade (a Refuge only if G22's stay in reach without it: RefugesHold).
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
                // (A Refuge only where every way keeps one in reach without it: G22's come first.)
                var stops = ahead[missing[0]].Where((x) => NodeKinds.IsStop(x.Kind) && (x.Kind != NodeKinds.Refuge || RefugesHold(all, rows, x))).ToList();
                if (stops.Count == 0) return;
                rng.Pick(stops).Kind = NodeKinds.Trade;
            }
        }

        /// <summary>
        /// A Trade on every map at least (G22: «el Trueque cede», in reach from most starts only): with none, a normal fight
        /// of rows 2–4 with no stop next to it becomes one, else a special one so (never a Rescate), else an event, else a
        /// Refuge G22's can spare.
        /// </summary>
        private static void OneTrade(Rng rng, List<List<RunNode>> rows, List<RunNode> all)
        {
            if (all.Any((n) => n.Kind == NodeKinds.Trade)) return;
            bool Clear(RunNode x) => x.Next.All((id) => IsFight(all.First((y) => y.Id == id).Kind)) && all.Where((p) => p.Next.Contains(x.Id)).All((p) => IsFight(p.Kind));
            var middle = all.Where((n) => n.Row >= 2 && n.Row < NodeRows).ToList();
            var tiers = new Func<RunNode, bool>[]
            {
                (n) => n.Kind == NodeKinds.Normal && Clear(n),
                (n) => IsFight(n.Kind) && n.Kind != NodeKinds.Rescue && n.Kind != NodeKinds.Boss && Clear(n),
                (n) => n.Kind == NodeKinds.Event,
                (n) => n.Kind == NodeKinds.Refuge && RefugesHold(all, rows, n),
            };
            foreach (var tier in tiers)
            {
                var pick = middle.Where(tier).ToList();
                if (pick.Count == 0) continue;
                rng.Pick(pick).Kind = NodeKinds.Trade;
                return;
            }
        }

        /// <summary>
        /// G22: while a node of `from` has no Refuge ahead in rows `lo` to `hi`, one of those ahead of the most of them
        /// becomes one: a normal fight with no stop before or after it, else a special one so (never a Rescate), else an
        /// event, else a trade (a stop for a stop: never two stops in a row on a way). Row 2's; within the caps.
        /// </summary>
        private static void RefugesInReach(Rng rng, List<RunNode> all, List<RunNode> from, int lo, int hi)
        {
            var ahead = from.ToDictionary((n) => n, (n) => Ahead(n, all).Where((x) => x.Row >= lo && x.Row <= hi).ToList());
            bool Clear(RunNode x) => x.Next.All((id) => IsFight(all.First((y) => y.Id == id).Kind)) && all.Where((p) => p.Next.Contains(x.Id)).All((p) => IsFight(p.Kind));
            var tiers = new Func<RunNode, bool>[]
            {
                (x) => x.Kind == NodeKinds.Normal && Clear(x),
                (x) => IsFight(x.Kind) && x.Kind != NodeKinds.Normal && x.Kind != NodeKinds.Rescue && x.Kind != NodeKinds.Boss && Clear(x),
                (x) => x.Kind == NodeKinds.Event,
                (x) => x.Kind == NodeKinds.Trade,
            };
            // Within the caps: a row with RefugesARow has no room for more; RefugesBefore in rows 2–5, none more anywhere.
            bool Room(RunNode x) => all.Count((y) => y.Kind == NodeKinds.Refuge && y.Row == x.Row) < RefugesARow;
            while (true)
            {
                var missing = from.Where((n) => !ahead[n].Any((x) => x.Kind == NodeKinds.Refuge)).ToList();
                if (missing.Count == 0) return;
                if (all.Count((y) => y.Kind == NodeKinds.Refuge && y.Row >= RefugeFirst && y.Row < RefugeRow) >= RefugesBefore) return;
                var near = missing.SelectMany((n) => ahead[n]).Distinct().Where(Room).ToList();
                var reach = new List<(RunNode Node, int From)>();
                foreach (var tier in tiers)
                {
                    reach = near.Where(tier).Select((x) => (Node: x, From: missing.Count((n) => ahead[n].Contains(x)))).ToList();
                    if (reach.Count > 0) break;
                }
                if (reach.Count == 0) return;
                var most = reach.Max((x) => x.From);
                rng.Pick(reach.Where((x) => x.From == most).Select((x) => x.Node).ToList()).Kind = NodeKinds.Refuge;
            }
        }

        /// <summary>
        /// G22 within the caps (RefugesARow, RefugesBefore), rows 3–4's: of the nodes in rows `lo` to `hi` ahead of `from`
        /// that may be one (RefugesInReach's tiers), the set that leaves the most of `from` with a Refuge ahead there, with
        /// the fewest Refuges and the most common nodes made one; never two of them on one way. Every set is tried (8 nodes
        /// at most). One by one (RefugesInReach), the caps left 93,9 % of row 2 covered; so, 97,4 % (97,0 % before them).
        /// </summary>
        private static void RefugesCovering(Rng rng, List<RunNode> all, List<RunNode> from, int lo, int hi)
        {
            var ahead = from.ToDictionary((n) => n, (n) => Ahead(n, all).Where((x) => x.Row >= lo && x.Row <= hi).ToList());
            bool Clear(RunNode x) => x.Next.All((id) => IsFight(all.First((y) => y.Id == id).Kind)) && all.Where((p) => p.Next.Contains(x.Id)).All((p) => IsFight(p.Kind));
            int Tier(RunNode x) => x.Kind == NodeKinds.Normal && Clear(x) ? 0
                : IsFight(x.Kind) && x.Kind != NodeKinds.Normal && x.Kind != NodeKinds.Rescue && x.Kind != NodeKinds.Boss && Clear(x) ? 1
                : x.Kind == NodeKinds.Event ? 2 : x.Kind == NodeKinds.Trade ? 3 : -1;
            var can = ahead.Values.SelectMany((x) => x).Distinct().Where((x) => Tier(x) >= 0).OrderBy((x) => x.Row).ThenBy((x) => x.Col).ToList();
            var before = all.Count((y) => y.Kind == NodeKinds.Refuge && y.Row >= RefugeFirst && y.Row < RefugeRow);
            var best = new List<List<RunNode>>();
            (int Covered, int Count, int Tiers) top = (-1, 0, 0);
            for (var mask = 0; mask < 1 << can.Count; mask++)
            {
                var set = can.Where((_, i) => (mask & (1 << i)) != 0).ToList();
                if (before + set.Count > RefugesBefore) continue;
                if (set.GroupBy((x) => x.Row).Any((g) => g.Count() + all.Count((y) => y.Kind == NodeKinds.Refuge && y.Row == g.Key) > RefugesARow)) continue;
                if (set.Any((x) => set.Any((y) => x.Next.Contains(y.Id)))) continue;
                var key = (Covered: from.Count((n) => ahead[n].Any((x) => x.Kind == NodeKinds.Refuge || set.Contains(x))), Count: set.Count, Tiers: set.Sum(Tier));
                var better = key.Covered > top.Covered || key.Covered == top.Covered && (key.Count < top.Count || key.Count == top.Count && key.Tiers < top.Tiers);
                var same = key.Covered == top.Covered && key.Count == top.Count && key.Tiers == top.Tiers;
                if (better)
                {
                    top = key;
                    best.Clear();
                }
                if (better || same) best.Add(set);
            }
            if (best.Count == 0) return;
            foreach (var x in rng.Pick(best)) x.Kind = NodeKinds.Refuge;
        }

        /// <summary>Whether G22's Refuges hold still `without` one of them: row RefugeFirst's, and one past every node of it.</summary>
        private static bool RefugesHold(List<RunNode> all, List<List<RunNode>> rows, RunNode without)
        {
            bool Has(RunNode n, int lo, int hi) => Ahead(n, all).Any((x) => x != without && x.Kind == NodeKinds.Refuge && x.Row >= lo && x.Row <= hi);
            var first = rows[RefugeFirst - 1];
            return first.Any((x) => x != without && x.Kind == NodeKinds.Refuge) && first.All((n) => Has(n, RefugeFirst + 1, RefugeSecond));
        }

        /// <summary>
        /// A Rescate in reach from wherever the run starts (N3): while a row-1 node has none ahead in rows 2–RescueLastRow,
        /// a normal fight there becomes one, the one ahead of the most of those starts; with none, another special does. A
        /// stop (the Trade with them) stays.
        /// </summary>
        private static void RescueInReach(Rng rng, List<List<RunNode>> rows, List<RunNode> all)
        {
            var ahead = rows[0].ToDictionary((n) => n, (n) => Ahead(n, all).Where((x) => x.Row <= RescueLastRow).ToList());
            while (true)
            {
                var missing = rows[0].Where((n) => !ahead[n].Any((x) => x.Kind == NodeKinds.Rescue)).ToList();
                if (missing.Count == 0) return;
                var near = missing.SelectMany((n) => ahead[n]).Distinct().ToList();
                var fights = near.Where((x) => x.Kind == NodeKinds.Normal).ToList();
                if (fights.Count == 0) fights = near.Where((x) => IsFight(x.Kind)).ToList();
                var reach = fights.Select((x) => (Node: x, Starts: missing.Count((n) => ahead[n].Contains(x)))).ToList();
                if (reach.Count == 0) return;
                var most = reach.Max((x) => x.Starts);
                rng.Pick(reach.Where((x) => x.Starts == most).Select((x) => x.Node).ToList()).Kind = NodeKinds.Rescue;
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

        /// <summary>
        /// Whether both players see what a node is: the engine says, so both see the same. One not seen is drawn dark; the
        /// boss too, till it is in sight (Arturo, 17/11: not always seen).
        /// </summary>
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
                case Places.Offices:
                    return MapSource.Generated(Biomes.City, seed, Sites.Offices);
                case Places.Shed:
                    return MapSource.Generated(Biomes.Facility, seed, Sites.Shed);
                case Places.Police:
                    return MapSource.Generated(Biomes.City, seed, Sites.Police);
                case Places.Plaza:
                    return MapSource.Generated(Biomes.City, seed, Sites.Plaza);
                case Places.Camping:
                    return MapSource.Generated(Biomes.Wilds, seed, Sites.Camping);
                case Places.Country:
                    return MapSource.Generated(Biomes.Wilds, seed);
                default:
                    return MapSource.Generated(Biomes.City, seed);
            }
        }
    }
}
