using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Cuadro 122 (Arturo, 22/11): no abandoning a fight, and the game lost with nobody left (N8). So every fight of a run has
    /// a way to end when it goes badly: La Lata, a Limpieza's too (out on it, a retreat), and a way to it from wherever a
    /// soldier can get to, jumping too (no yard to drop into and stay in). The fights as the runs' maps draw them
    /// (RunMap.Generate), and Evacuación and Las cargas on their sites though they are parked.
    /// </summary>
    public class WayOutTests
    {
        private readonly ITestOutputHelper output;

        public WayOutTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>What a fight's ways out are: La Lata's places, and the places a soldier gets to that don't get back to it.</summary>
        public sealed class Exit
        {
            public string Kind = "", Site = "", Map = "";
            public int Seed, Bus, Reached, Pits;
            /// <summary>Places a soldier can stop on, reached from the squad's start, with no way from them to La Lata.</summary>
            public List<Pos> Traps = new List<Pos>();
            /// <summary>Soldiers whose start has no way to La Lata.</summary>
            public int Cut;
            public bool Clock;
            public bool Ok => Bus > 0 && Traps.Count == 0 && Cut == 0;
        }

        /// <summary>
        /// The places a soldier reaches from `from` (any movement, others standing aside, jumping off storeys), and which of
        /// them get back to La Lata: a walk out from the start, and one back from La Lata along the same steps turned round.
        /// </summary>
        public static Exit WaysOut(GameState s)
        {
            var e = new Exit { Map = s.MapName ?? "", Bus = s.Mission.EvacZone.Count, Clock = s.Mission.TurnsLeft != null || s.Mission.HoldUntil != null, Pits = s.Mission.Pits?.Count ?? 0 };
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive && !Rules.IsCivilian(u) && u.Lame != true).ToList();
            // Out from the starts: every step the rules allow a soldier (Paths.StepsFrom, the 4 ways ComputeReach walks).
            var reached = new HashSet<int>();
            var back = new Dictionary<int, List<int>>();
            var queue = new Queue<int>();
            foreach (var u in squad)
            {
                var n = Grid.NodeOf(s, u.Pos);
                if (reached.Add(n)) queue.Enqueue(n);
            }
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                var p = Grid.PosOf(s, n);
                foreach (var d in Grid.Dirs4)
                    foreach (var step in Paths.StepsFrom(s, p, d.X, d.Y))
                    {
                        var m = Grid.NodeOf(s, step.To);
                        // The pits (MissionState.Pits, Setup.NoPits): nobody walks into them.
                        if (s.Mission.Pits != null && s.Mission.Pits.Contains(m)) continue;
                        if (!back.TryGetValue(m, out var from)) back[m] = from = new List<int>();
                        from.Add(n);
                        if (reached.Add(m)) queue.Enqueue(m);
                    }
            }
            e.Reached = reached.Count;
            // Back from La Lata's places along the steps turned round.
            var home = new HashSet<int>();
            foreach (var z in s.Mission.EvacZone)
            {
                var n = Grid.NodeOf(s, new Pos { X = z.X, Y = z.Y, Z = 0 });
                if (reached.Contains(n) && home.Add(n)) queue.Enqueue(n);
            }
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                if (!back.TryGetValue(n, out var from)) continue;
                foreach (var m in from) if (home.Add(m)) queue.Enqueue(m);
            }
            e.Traps = reached.Where((n) => !home.Contains(n)).Select((n) => Grid.PosOf(s, n)).Where((p) => Flights.CanStopAt(s, p)).ToList();
            e.Cut = squad.Count((u) => !home.Contains(Grid.NodeOf(s, u.Pos)));
            return e;
        }

        /// <summary>A fight like `like` (its kind, site, seed, difficulty, modifier, guarded) launched as the room does it.</summary>
        public static GameState Launch(RunNode like, int seed)
        {
            var c = Two(seed);
            var run = Start(c, seed);
            var n = RunMap.Choices(run)[0];
            n.Kind = like.Kind;
            n.Modifier = like.Modifier;
            n.Added = null;
            n.Seed = like.Seed;
            n.Site = like.Site;
            n.Difficulty = like.Difficulty;
            n.Guarded = like.Guarded;
            Agree(c, new ChooseNodeCommand { Node = n.Id });
            return new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
        }

        /// <summary>
        /// The fights to look at: those of the runs' maps of chapters 1–3 on `maps` seeds (each kind, site and seed once), and
        /// Evacuación and Las cargas, parked, `parked` seeds on each of their sites.
        /// </summary>
        public static List<RunNode> Fights(int maps, int parked)
        {
            var seen = new HashSet<(string, string?, int?)>();
            var out_ = new List<RunNode>();
            for (var chapter = 1; chapter <= 3; chapter++)
                for (var seed = 1; seed <= maps; seed++)
                    foreach (var n in RunMap.Generate(chapter, seed).Where((x) => RunMap.IsFight(x.Kind)))
                        if (seen.Add((n.Kind, n.Site, n.Seed))) out_.Add(n);
            for (var seed = 1; seed <= parked; seed++)
            {
                foreach (var site in RunMap.EvacuationSites) out_.Add(new RunNode { Kind = NodeKinds.Evacuation, Site = site, Seed = 7000 + seed, Difficulty = 1 + seed % 3 });
                foreach (var site in RunMap.ChargesSites) out_.Add(new RunNode { Kind = NodeKinds.Charges, Site = site, Seed = 8000 + seed, Difficulty = 1 + seed % 3 });
            }
            return out_;
        }

        /// <summary>Where a fight of `kind` can be (RunMap: Generate, SpecialFor, the sites of each): every place but where it never is.</summary>
        private static bool Fits(string kind, string place)
        {
            var inside = place == Places.Offices || place == Places.Shed || place == Places.Police || place == Places.Hospital;
            switch (kind)
            {
                case NodeKinds.Sabotage: return place == Places.Plaza || place == Places.Camping || place == Places.Factory;
                case NodeKinds.Key: return place == Places.Police;
                case NodeKinds.Bitten: return place == Places.Hospital;
                case NodeKinds.Boss: return place == Places.Factory;
                case NodeKinds.Crossing: return !inside;
                case NodeKinds.Split: return RunMap.SplitSites.Contains(place);
                case NodeKinds.Raiders: return RunMap.RaidersSites.Contains(place);
                case NodeKinds.Evacuation: return RunMap.EvacuationSites.Contains(place);
                case NodeKinds.Charges: return RunMap.ChargesSites.Contains(place);
                default: return true;
            }
        }

        private static readonly string[] AllPlaces =
        {
            Places.City, Places.Town, Places.Estate, Places.Factory, Places.Roadside, Places.Country, Places.Hospital, Places.Offices,
            Places.Shed, Places.Police, Places.Plaza, Places.Camping,
        };

        /// <summary>Every fight kind forced on every place it fits, on `seeds` seeds (`one`: one place a kind, by turns).</summary>
        public static List<RunNode> Forced(int seeds, bool one = false)
        {
            var out_ = new List<RunNode>();
            var k = 0;
            foreach (var kind in NodeKinds.All.Where(RunMap.IsFight))
            {
                var places = AllPlaces.Where((p) => Fits(kind, p)).ToList();
                if (one) places = new List<string> { places[k++ % places.Count] };
                for (var seed = 1; seed <= seeds; seed++)
                    foreach (var place in places)
                        out_.Add(new RunNode { Kind = kind, Site = place, Seed = 9000 + seed * 97, Difficulty = 1 + seed % 3 });
            }
            return out_;
        }

        private static Exit Look(RunNode n)
        {
            var s = Launch(n, 1);
            var e = WaysOut(s);
            e.Kind = n.Kind;
            e.Site = n.Site ?? "";
            e.Seed = n.Seed ?? 0;
            return e;
        }

        [Fact]
        public void EveryFightHasLaLataAndAWayToItFromAnywhere()
        {
            // A few maps of each chapter, and the parked ones on one seed: the whole sweep is the bench below.
            var bad = Fights(2, 1).Concat(Forced(1, true)).Select(Look).Where((e) => !e.Ok).ToList();
            foreach (var e in bad) output.WriteLine($"{e.Kind} {e.Site} {e.Seed} ({e.Map}): La Lata {e.Bus}, sin salida {e.Traps.Count} {string.Join(" ", e.Traps.Take(6).Select((p) => $"{p.X},{p.Y},{p.Z}"))}, cortados {e.Cut}");
            Assert.Empty(bad);
        }

        [Fact]
        public void ALimpiezaOfARunHasLaLataAndLeavingIsARetreat()
        {
            var (c, s) = LaunchAt(NodeKinds.Elimination);
            Assert.Equal(MissionKinds.Elimination, s.Mission.Kind);
            Assert.NotEmpty(s.Mission.EvacZone);
            // No clock: La Lata waits.
            Assert.Null(s.Mission.TurnsLeft);
            Assert.True(WaysOut(s).Ok);
            // Out on it: a retreat, everyone aboard alive, the run going on.
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            var zone = s.Mission.EvacZone.Select((z) => new Pos { X = (int)z.X, Y = (int)z.Y, Z = 0 }).ToList();
            for (var i = 0; i < squad.Count; i++) squad[i].Pos = zone[i];
            Step2Helpers.NewSim(s).DepartEvac();
            Assert.Equal((Outcomes.Defeat, EndReasons.Abandoned), (s.Outcome, s.OutcomeReason));
            new EngineCampaignRules().ApplyMissionResult(c, s, new Rng(9));
            Assert.All(squad, (u) => Assert.True(c.Soldiers.First((x) => x.Id == u.CampaignId).Alive));
            Assert.Equal(RunPhases.Choose, c.Run!.Phase);
        }

        [Fact]
        public void ARunsFightIsNeverAbandoned()
        {
            // «Quitamos el abandonar combate» (Arturo, cuadro 122): neither a call nor at once, alone or with the partner.
            var (_, s) = LaunchAt(NodeKinds.Normal);
            Assert.Empty(Rules.CallAbandon(s, 0, new[] { 0 }));
            Assert.Empty(Rules.CallAbandon(s, 0, Both));
            Assert.Empty(Rules.AbandonMission(s));
            Assert.Null(s.Outcome);
            Assert.Null(s.AbandonCall);
        }

        /// <summary>The sweep (cuadro 122): every fight of chapters 1–3 on BASTION_BANCO_SEMILLAS maps (default 20), the parked on as many.</summary>
        [BenchFact]
        public void TheWholeSweep()
        {
            var maps = int.TryParse(Environment.GetEnvironmentVariable("BASTION_BANCO_SEMILLAS"), out var n) ? n : 20;
            var all = Fights(maps, maps).Concat(Forced(3)).Select(Look).ToList();
            var lines = new List<string>
            {
                $"# Salida de cada combate (cuadro 122): {all.Count} combates: los de {maps} mapas de expedición por capítulo (1–3), Evacuación y Las cargas en sus sitios ({maps} semillas), y cada etapa forzada en cada sitio donde puede salir (3 semillas)",
                "",
                "| Etapa | Combates | Con La Lata | Con reloj | Algún sitio sin camino a La Lata | Inicio sin camino | Con hondonadas cerradas (casillas) |",
                "|---|---|---|---|---|---|---|",
            };
            foreach (var g in all.GroupBy((e) => e.Kind).OrderBy((g) => g.Key))
                lines.Add($"| {g.Key} | {g.Count()} | {g.Count((e) => e.Bus > 0)} | {g.Count((e) => e.Clock)} | {g.Count((e) => e.Traps.Count > 0)} | {g.Count((e) => e.Cut > 0)} | {g.Count((e) => e.Pits > 0)} ({g.Sum((e) => e.Pits)}) |");
            lines.Add("");
            foreach (var e in all.Where((e) => !e.Ok))
                lines.Add($"- {e.Kind} · {e.Site} · {e.Seed} ({e.Map}): La Lata {e.Bus}, sin camino {e.Traps.Count} ({string.Join(" ", e.Traps.Take(8).Select((p) => $"{p.X},{p.Y},{p.Z}"))}), inicios cortados {e.Cut}");
            File.WriteAllLines(Path.Combine(BenchFactAttribute.Folder!, "salida.md"), lines);
            foreach (var l in lines) output.WriteLine(l);
        }
    }
}
