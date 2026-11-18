using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using Xunit.Abstractions;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The node map of a run (H1, Campaign/RunMap.cs; H0-DISENO §4, PLAN-RUNS §3.1), over many seeds: its shape, links
    /// that never cross, every node reached and leading on, what each row may hold, a Trade in reach, the modifiers, the
    /// sites and the difficulty; and the same seed, the same map.
    /// </summary>
    public class RunMapTests
    {
        private const int Seeds = 600;

        private readonly ITestOutputHelper output;

        public RunMapTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static IEnumerable<(int Seed, List<RunNode> Map)> Maps(int chapter = 1)
        {
            for (var seed = 0; seed < Seeds; seed++) yield return (seed, RunMap.Generate(chapter, seed * 7919 - 1000));
        }

        private static RunNode Get(List<RunNode> map, string id) => map.First((n) => n.Id == id);

        [Fact]
        public void SevenRowsOfTheirShape()
        {
            foreach (var (seed, map) in Maps())
            {
                for (var row = 1; row <= RunMap.Rows; row++)
                {
                    var nodes = map.Where((n) => n.Row == row).ToList();
                    if (row <= RunMap.NodeRows) Assert.InRange(nodes.Count, 3, 4);
                    else Assert.Single(nodes);
                    Assert.Equal(Enumerable.Range(0, nodes.Count), nodes.Select((n) => n.Col));
                    Assert.All(nodes, (n) => Assert.Equal($"n{row}-{n.Col}", n.Id));
                }
                Assert.Equal(NodeKinds.Refuge, map.Single((n) => n.Row == RunMap.RefugeRow).Kind);
                Assert.Equal(NodeKinds.Boss, map.Single((n) => n.Row == RunMap.BossRow).Kind);
                Assert.All(map.Where((n) => n.Row == 1), (n) => Assert.Equal(NodeKinds.Normal, n.Kind));
            }
        }

        [Fact]
        public void LinksGoOneRowOnAndNeverCross()
        {
            foreach (var (seed, map) in Maps())
            {
                foreach (var n in map)
                {
                    if (n.Row == RunMap.BossRow)
                    {
                        Assert.Empty(n.Next);
                        continue;
                    }
                    Assert.InRange(n.Next.Count, 1, 2);
                    Assert.Equal(n.Next.Count, n.Next.Distinct().Count());
                    Assert.All(n.Next, (id) => Assert.Equal(n.Row + 1, Get(map, id).Row));
                    // In column order.
                    Assert.Equal(n.Next.Select((id) => Get(map, id).Col).OrderBy((x) => x), n.Next.Select((id) => Get(map, id).Col));
                }
                for (var row = 1; row < RunMap.Rows; row++)
                {
                    var nodes = map.Where((n) => n.Row == row).OrderBy((n) => n.Col).ToList();
                    for (var i = 0; i + 1 < nodes.Count; i++)
                    {
                        var left = nodes[i].Next.Max((id) => Get(map, id).Col);
                        var right = nodes[i + 1].Next.Min((id) => Get(map, id).Col);
                        Assert.True(left <= right, $"seed {seed}: row {row} crosses");
                    }
                }
                // Every node past row 1 is reached.
                Assert.All(map.Where((n) => n.Row > 1), (n) => Assert.Contains(map, (p) => p.Next.Contains(n.Id)));
                // Row 5 all to the Refuge, the Refuge to the boss.
                var refuge = map.Single((n) => n.Row == RunMap.RefugeRow);
                Assert.All(map.Where((n) => n.Row == RunMap.NodeRows), (n) => Assert.Equal(new[] { refuge.Id }, n.Next));
            }
        }

        [Fact]
        public void SomeNodesLeadToTwo()
        {
            var forks = Maps().Sum((m) => m.Map.Count((n) => n.Next.Count == 2));
            Assert.True(forks > Seeds * 3, $"{forks} forks in {Seeds} maps");
        }

        [Fact]
        public void WhatTheRowsHold()
        {
            var allowed = new[] { NodeKinds.Normal }.Concat(RunMap.Specials).Concat(NodeKinds.Stops).ToList();
            var counts = new Dictionary<string, int>();
            var middle = 0;
            foreach (var (seed, map) in Maps())
            {
                foreach (var n in map.Where((x) => x.Row >= 2 && x.Row <= RunMap.NodeRows))
                {
                    Assert.Contains(n.Kind, allowed);
                    counts[n.Kind] = (counts.TryGetValue(n.Kind, out var k) ? k : 0) + 1;
                    middle++;
                    // Never two stops in a row on a way, the Refuge of row 6 too.
                    if (NodeKinds.IsStop(n.Kind)) Assert.All(n.Next, (id) => Assert.False(NodeKinds.IsStop(Get(map, id).Kind), $"seed {seed}: {n.Id}"));
                }
                Assert.DoesNotContain(map, (n) => n.Row == RunMap.NodeRows && NodeKinds.IsStop(n.Kind));
            }
            double Share(params string[] kinds) => kinds.Sum((k) => counts.TryGetValue(k, out var n) ? n : 0) / (double)middle;
            output.WriteLine($"Filas 2–5: normal {Share(NodeKinds.Normal):P1}, especiales {Share(RunMap.Specials.ToArray()):P1}, paradas {Share(NodeKinds.Stops.ToArray()):P1} ({string.Join(", ", NodeKinds.Stops.Select((k) => $"{k} {Share(k):P1}"))})");
            // The Rescates kept in reach (N3) are normal fights made Rescates.
            Assert.InRange(Share(NodeKinds.Normal), 0.30, 0.65);
            Assert.InRange(Share(RunMap.Specials.ToArray()), 0.20, 0.45);
            // (Stops more since G22's Refuges, some 3 a map: cuadro 76.)
            Assert.InRange(Share(NodeKinds.Stops.ToArray()), 0.15, 0.45);
            Assert.All(NodeKinds.Stops, (k) => Assert.True(counts.ContainsKey(k), k));
            Assert.All(RunMap.Specials, (k) => Assert.True(counts.ContainsKey(k), k));
            // H5's four too.
            Assert.All(RunMap.NewSpecials, (k) => Assert.True(counts.ContainsKey(k), k));
        }

        [Fact]
        public void ATradeInReachFromEveryStart()
        {
            // Since G22 (cuadro 72: «el Trueque cede»), from most starts: the Refuges are placed first, and two stops are
            // never in a row (.dev-snapshots/gameplay/g21-g22/G22-VOLVER.md: 83 %); one on every map.
            int starts = 0, with = 0, none = 0;
            foreach (var (seed, map) in Maps())
            {
                // (One on every map but where none fits: Refuges round every place left for it, some 1 map in 75.)
                if (!map.Any((n) => n.Kind == NodeKinds.Trade)) none++;
                foreach (var start in map.Where((n) => n.Row == 1))
                {
                    var seen = new HashSet<string>();
                    var next = new List<string> { start.Id };
                    while (next.Count > 0)
                    {
                        next = next.SelectMany((id) => Get(map, id).Next).Where(seen.Add).ToList();
                    }
                    starts++;
                    if (seen.Any((id) => Get(map, id).Kind == NodeKinds.Trade)) with++;
                }
            }
            Assert.InRange(with / (double)starts, 0.75, 1);
            Assert.InRange(none, 0, Seeds / 50);
        }

        [Fact]
        public void ARescueInReachFromEveryStartAndNoSurvivor()
        {
            // N3 (17/11): recruits come only from a Rescate, so one is in reach from every start in rows 2–4; the
            // Superviviente stop is gone.
            foreach (var (seed, map) in Maps())
            {
                Assert.DoesNotContain(map, (n) => n.Kind == Campaign.OldSurvivorStop);
                foreach (var start in map.Where((n) => n.Row == 1))
                {
                    var seen = new HashSet<string>();
                    var next = new List<string> { start.Id };
                    while (next.Count > 0)
                    {
                        next = next.SelectMany((id) => Get(map, id).Next).Where(seen.Add).ToList();
                    }
                    Assert.True(seen.Any((id) => Get(map, id).Kind == NodeKinds.Rescue && Get(map, id).Row <= RunMap.RescueLastRow), $"seed {seed}: no Rescate from {start.Id}");
                }
            }
        }

        [Fact]
        public void ModifiersSitesAndDifficulty()
        {
            var fights = 0;
            var modified = 0;
            var roadside = 0;
            foreach (var (seed, map) in Maps(2))
            {
                foreach (var n in map)
                {
                    Assert.Equal((n.Row <= 2 ? 1 : n.Row <= 4 ? 2 : 3) + 1, n.Difficulty);
                    if (NodeKinds.IsStop(n.Kind))
                    {
                        Assert.Null(n.Modifier);
                        Assert.Null(n.Site);
                        Assert.Null(n.Seed);
                        continue;
                    }
                    Assert.NotNull(n.Seed);
                    Assert.Contains(n.Site, Places.Names.Keys);
                    // The site is what the map will be.
                    Assert.Equal(n.Site, Places.Of(RunMap.MapOf(n)!));
                    // A sabotage in a factory, or on the plaza that stands for it (H9); the boss, in a factory.
                    if (n.Kind == NodeKinds.Boss) Assert.Equal(Places.Factory, n.Site);
                    if (n.Kind == NodeKinds.Sabotage) Assert.Contains(n.Site, new[] { Places.Factory, Places.Plaza, Places.Camping });
                    // No service area in the first two rows; later, yes.
                    if (n.Row <= RunMap.EarlyRows) Assert.NotEqual(Places.Roadside, n.Site);
                    else if (n.Site == Places.Roadside) roadside++;
                    if (n.Row == 1 || n.Kind == NodeKinds.Boss) Assert.Null(n.Modifier);
                    else
                    {
                        fights++;
                        if (n.Modifier != null)
                        {
                            modified++;
                            Assert.Contains(n.Modifier, Modifiers.All);
                        }
                    }
                }
            }
            Assert.InRange(modified / (double)fights, 0.22, 0.38);
            Assert.True(roadside > 0);
            Assert.Equal(1, RunMap.DifficultyOf(2, 1));
            Assert.Equal(3, RunMap.DifficultyOf(7, 1));
            Assert.Equal(5, RunMap.DifficultyOf(7, 3));
        }

        [Fact]
        public void TheSameSeedTheSameMap()
        {
            Assert.Equal(Json.Write(RunMap.Generate(1, 12345)), Json.Write(RunMap.Generate(1, 12345)));
            Assert.NotEqual(Json.Write(RunMap.Generate(1, 12345)), Json.Write(RunMap.Generate(1, 12346)));
            // The places all turn up; the hospital only with its flag (H7, RunHospitalTests).
            var sites = Maps().SelectMany((m) => m.Map).Where((n) => n.Site != null).Select((n) => n.Site!).Distinct().ToList();
            Assert.Equal(Places.Names.Keys.Where((x) => x != Places.Hospital || RunMap.HospitalSites).OrderBy((x) => x), sites.OrderBy((x) => x));
        }

        [Fact]
        public void TheMapAFightIsPlayedOn()
        {
            var n = new RunNode { Kind = NodeKinds.Normal, Seed = 77, Site = Places.Estate };
            var map = RunMap.MapOf(n)!;
            Assert.Equal((Biomes.City, Sites.Industrial, 77), (map.Biome!, map.Site!, map.Seed!.Value));
            n.Site = Places.Roadside;
            Assert.Equal((Biomes.Wilds, Sites.Roadside), (RunMap.MapOf(n)!.Biome!, RunMap.MapOf(n)!.Site!));
            n.Site = Places.Factory;
            Assert.Equal(Biomes.Facility, RunMap.MapOf(n)!.Biome);
            Assert.Null(RunMap.MapOf(new RunNode { Kind = NodeKinds.Trade }));
        }
    }
}
