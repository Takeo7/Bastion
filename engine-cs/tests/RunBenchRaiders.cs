#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G23.5 Saqueadores' R1 by seats (SAQUEADORES.md, «Cómo se mide»): Saqueadores against the Saqueo on the same maps, with
    /// 2, 4, 6 and 8 seats, alone and two, its three sites; rifles at difficulty 2 (four guards) and 3 (five), knives at 2.
    /// The bots of today (MissionBot) for every seat: they don't know the camp, they go for the nearest enemy and search
    /// what they pass, so this measures the fight with the living, not robbing them unseen. Run only with BASTION_BANCO
    /// (`BASTION_BANCO=<carpeta> dotnet test engine-cs/tests --filter RunBenchRaiders`); BASTION_BANCO_SEMILLAS seeds (5).
    /// BASTION_BANCO_PARTE=saqueo: the bots told it is a Saqueo (RunState's node kind, theirs only: MissionBot.RunSaqueo),
    /// so they search and leave as there, the camp on their way or not; without it they never leave while one of the
    /// living is in reach (the first R1, 20/11: all fell). Writes r1-saqueadores(-saqueo).md and its combates.txt there.
    /// </summary>
    public class RunBenchRaiders
    {
        private static readonly int[] Seats = { 2, 4, 6, 8 };

        private static bool AsSaqueo => Environment.GetEnvironmentVariable("BASTION_BANCO_PARTE") == "saqueo";

        private static int[] BenchSeeds => int.TryParse(Environment.GetEnvironmentVariable("BASTION_BANCO_SEMILLAS"), out var n) ? Enumerable.Range(1, n).ToArray() : new[] { 1, 2, 3, 4, 5 };

        /// <summary>The kits and stages measured: rifles at difficulty 2 and 3, knives at 2.</summary>
        private static readonly (bool Rifles, int Difficulty, string Name)[] Kits = { (true, 2, "fusiles, dif. 2"), (true, 3, "fusiles, dif. 3"), (false, 2, "cuchillos, dif. 2") };

        private sealed class Job
        {
            public string Kind = "", Site = "";
            public int Seats, Seed, Kit;
            public bool Two;
        }

        private sealed class Fight
        {
            public Job Job = new Job();
            public string Outcome = "", Reason = "";
            public bool Stuck, Camp;
            public int Turn, Fallen, Guards, GuardsBySquad, GuardsByDead, Crates, Waves;
            public int? Seen, FirstFell;
            public double Seconds;

            public bool Won => Outcome == Outcomes.Victory;
            public bool Wiped => Reason == EndReasons.Wiped;
        }

        private static void Arm(CampaignState c, CampaignSoldier x)
        {
            InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
            InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
            InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
        }

        private static Fight Play(Job job)
        {
            var clock = Stopwatch.StartNew();
            var kit = Kits[job.Kit];
            var c = job.Two ? Two(job.Seed) : Solo(job.Seed);
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = Array.IndexOf(Seats, job.Seats);
            var rng0 = new Rng(job.Seed * 101);
            while (c.Soldiers.Count((x) => x.Alive) < job.Seats) Runs.NewSoldier(c, c.Players[c.Soldiers.Count % c.Players.Count], rng0);
            if (kit.Rifles) foreach (var x in c.Soldiers) Arm(c, x);
            c.Squad = Runs.DefaultSquad(c, c.Players);
            var run = Start(c, job.Seed);
            var n = RunMap.Choices(run)[0];
            n.Kind = job.Kind;
            n.Modifier = null;
            n.Seed = 100 + job.Seed;
            n.Site = job.Site;
            n.Difficulty = kit.Difficulty;
            var choose = new ChooseNodeCommand { Node = n.Id };
            if (job.Two) Agree(c, choose);
            else Ok(c, choose);
            var rules = new EngineCampaignRules();
            var s = rules.LaunchMission(c, new Rng(job.Seed * 31))!.Value.state;
            // The bench's shim (BASTION_BANCO_PARTE=saqueo): the bots play it as a Saqueo; the rules don't read it.
            if (AsSaqueo && s.Run != null) s.Run.NodeKind = NodeKinds.Normal;
            var slots = job.Two ? Both : Zero;
            var f = new Fight { Job = job, Camp = s.Mission.Raiders != null };
            var guards = s.Units.Where((u) => u.Guard != null).Select((u) => u.Id).ToHashSet();
            f.Guards = guards.Count;
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();
            void Watch(CommandResult r)
            {
                if (f.Seen == null && !s.Concealed) f.Seen = s.Turn;
                if (f.FirstFell == null && squad.Any((u) => !u.Alive && !u.Evacuated)) f.FirstFell = s.Turn;
                foreach (var d in (r.Events ?? new List<GameEvent>()).OfType<DiedEvent>())
                {
                    if (!guards.Contains(d.Unit)) continue;
                    var by = d.By != null ? Grid.GetUnit(s, d.By) : null;
                    if (by?.Team == Teams.Xcom) f.GuardsBySquad++;
                    else if (by != null && Content.Templates[by.Template].Undead) f.GuardsByDead++;
                }
            }
            var rng = new Rng(job.Seed * 7919 + 13);
            int orders = 0, idle = 0;
            while (s.Outcome == null && orders < 3000 && idle < 3)
            {
                var any = false;
                foreach (var slot in slots)
                {
                    var cmd = MissionBot.Next(s, slot, slots);
                    if (cmd == null) continue;
                    orders++;
                    var r = Rules.ExecuteCommand(s, cmd, slot, slots, rng);
                    if (r.Ok) any = true;
                    Watch(r);
                }
                idle = any ? 0 : idle + 1;
            }
            f.Turn = s.Turn;
            f.Stuck = s.Outcome == null;
            f.Crates = s.Mission.Raiders?.Crates.Count((i) => !Looting.IsSearchable(s, new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 })) ?? 0;
            f.Waves = s.Mission.Reinforcements.Count((r) => r.Horde == true && r.Trickle != true);
            if (s.Outcome != null)
            {
                f.Outcome = s.Outcome;
                f.Reason = s.OutcomeReason ?? "";
                rules.ApplyMissionResult(c, s, new Rng(job.Seed));
                f.Fallen = c.LastReport!.Run!.Fallen.Count;
            }
            f.Seconds = clock.Elapsed.TotalSeconds;
            return f;
        }

        private static string Pct(int a, int b) => b == 0 ? "–" : $"{100.0 * a / b:0} %";

        private static string Spread(IEnumerable<int> xs)
        {
            var v = xs.OrderBy((x) => x).ToList();
            if (v.Count == 0) return "–";
            int At(double q) => v[Math.Min(v.Count - 1, (int)Math.Floor(q * (v.Count - 1) + 0.5))];
            return $"{At(0.5)} ({At(0.1)}–{At(0.9)})";
        }

        [BenchFact]
        public void R1BySeats()
        {
            var seeds = BenchSeeds;
            var jobs = new List<Job>();
            foreach (var kind in new[] { NodeKinds.Raiders, NodeKinds.Normal })
                foreach (var seats in Seats)
                    foreach (var two in new[] { false, true })
                        foreach (var site in RunMap.RaidersSites)
                            for (var k = 0; k < Kits.Length; k++)
                                foreach (var seed in seeds)
                                    jobs.Add(new Job { Kind = kind, Seats = seats, Two = two, Site = site, Kit = k, Seed = seed });
            var clock = Stopwatch.StartNew();
            var fights = new Fight[jobs.Count];
            Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i) => fights[i] = Play(jobs[i]));
            var lines = new List<string>
            {
                $"# R1 de Saqueadores por plazas: {fights.Length} combates en {clock.Elapsed.TotalMinutes:0.0} min ({fights.Average((f) => f.Seconds):0.0} s cada uno)",
                "",
                $"Sitios: {string.Join(", ", RunMap.RaidersSites.Select((x) => Places.Names[x]))}. Semillas de mapa {100 + seeds[0]}–{100 + seeds[^1]}. El bot de hoy en cada asiento: no conoce el campamento; va al enemigo más cercano y registra lo que pilla de camino. **Mide pelear con los vivos, no robarles sin que te vean.**",
                "",
                "Guardias: 4 con dificultad 2, 5 con dificultad 3. «Salen»: la escuadra llega a La Lata y se va (victoria de un Saqueo). Para comparar, el Saqueo en los mismos mapas, con el mismo equipo.",
                "",
            };
            foreach (var (rifles, difficulty, name) in Kits)
            {
                var k = Array.FindIndex(Kits, (x) => x.Name == name);
                lines.Add($"## {name}");
                lines.Add("");
                lines.Add("| Plazas | Jugadores | Saqueadores | salen | cae la escuadra | ⚠ | turnos | soldados caídos | os ven en el turno | vivos muertos: escuadra / muertos | cajas (de 3) | oleadas | Saqueo: salen | cae la escuadra | turnos | oleadas |");
                lines.Add("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var seats in Seats)
                    foreach (var two in new[] { false, true })
                    {
                        var r = fights.Where((f) => f.Job.Kind == NodeKinds.Raiders && f.Job.Kit == k && f.Job.Seats == seats && f.Job.Two == two).ToList();
                        var q = fights.Where((f) => f.Job.Kind == NodeKinds.Normal && f.Job.Kit == k && f.Job.Seats == seats && f.Job.Two == two).ToList();
                        lines.Add($"| {seats} | {(two ? "dos" : "uno")} | {r.Count} | {Pct(r.Count((f) => f.Won), r.Count)} | {Pct(r.Count((f) => f.Wiped), r.Count)} | {r.Count((f) => f.Stuck)} | {Spread(r.Where((f) => !f.Stuck).Select((f) => f.Turn))} | {r.Average((f) => (double)f.Fallen):0.0} | {Spread(r.Where((f) => f.Seen != null).Select((f) => f.Seen!.Value))} | {r.Average((f) => (double)f.GuardsBySquad):0.0} / {r.Average((f) => (double)f.GuardsByDead):0.0} de {r.Average((f) => (double)f.Guards):0.0} | {r.Average((f) => (double)f.Crates):0.0} | {r.Average((f) => (double)f.Waves):0.0} | {Pct(q.Count((f) => f.Won), q.Count)} | {Pct(q.Count((f) => f.Wiped), q.Count)} | {Spread(q.Where((f) => !f.Stuck).Select((f) => f.Turn))} | {q.Average((f) => (double)f.Waves):0.0} |");
                    }
                lines.Add("");
                lines.Add("Por sitio (todas las plazas):");
                lines.Add("");
                foreach (var site in RunMap.RaidersSites)
                {
                    var r = fights.Where((f) => f.Job.Kind == NodeKinds.Raiders && f.Job.Kit == k && f.Job.Site == site).ToList();
                    var q = fights.Where((f) => f.Job.Kind == NodeKinds.Normal && f.Job.Kit == k && f.Job.Site == site).ToList();
                    lines.Add($"- {Places.Names[site]}: Saqueadores salen {Pct(r.Count((f) => f.Won), r.Count)}, cae la escuadra {Pct(r.Count((f) => f.Wiped), r.Count)}, sin campamento {r.Count((f) => !f.Camp)}; Saqueo salen {Pct(q.Count((f) => f.Won), q.Count)}, cae la escuadra {Pct(q.Count((f) => f.Wiped), q.Count)}.");
                }
                lines.Add("");
            }
            var dir = Environment.GetEnvironmentVariable("BASTION_BANCO")!;
            var file = AsSaqueo ? "r1-saqueadores-saqueo" : "r1-saqueadores";
            if (AsSaqueo) lines.Insert(1, "\n**Con el apaño del banco:** el bot juega Saqueadores como un Saqueo (registra y se va, como en el Saqueo); el campamento está en el mapa, en su camino o no.\n");
            File.WriteAllLines(Path.Combine(dir, file + ".md"), lines);
            File.WriteAllLines(Path.Combine(dir, file + "-combates.txt"), fights.Select((f) =>
                $"{f.Job.Kind}\t{f.Job.Site}\t{f.Job.Seats}\t{(f.Job.Two ? 2 : 1)}\t{Kits[f.Job.Kit].Name}\t{f.Job.Seed}\t{f.Outcome}\t{f.Reason}\t{f.Turn}\t{f.Fallen}\t{f.Seen}\t{f.Guards}\t{f.GuardsBySquad}\t{f.GuardsByDead}\t{f.Crates}\t{f.Waves}\t{(f.Stuck ? "atascado" : "")}"));
        }
    }
}
