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
    /// G23.4 Separados' R1 by seats (Producto, 20/11; .dev-snapshots/gameplay/g23/SEPARADOS.md): Separados against the Saqueo
    /// on the same maps, with 2, 4, 6 and 8 seats, alone and two, rifles and knives; the uneven splits two players can have
    /// apart; and Separados with the horde awake (an event's). The bots of today (MissionBot) for every seat. Run only with
    /// BASTION_BANCO (`BASTION_BANCO=<carpeta> dotnet test engine-cs/tests --filter RunBenchSplit`); BASTION_BANCO_SEMILLAS
    /// seeds (default 5); BASTION_BANCO_PARTE=lados, only Separados as it comes (no Saqueo, uneven splits or awake: the two
    /// sides of cuadro 95). Writes r1-separados.md and r1-separados-combates.txt there.
    /// </summary>
    public class RunBenchSplit
    {
        private static readonly int[] Seats = { 2, 4, 6, 8 };

        /// <summary>
        /// Two players' uneven squads (by owner): each takes half the seats at most with both there (Runs.SquadLimit), so the
        /// other one fewer, by having fewer alive. With 4 seats 2 and 1; with 6, 3 and 1, 3 and 2; with 8, 4 and 1 to 4 and 3.
        /// </summary>
        private static readonly (int Seats, int A, int B)[] Uneven = { (4, 2, 1), (6, 3, 1), (6, 3, 2), (8, 4, 1), (8, 4, 2), (8, 4, 3) };

        private static int[] BenchSeeds => int.TryParse(Environment.GetEnvironmentVariable("BASTION_BANCO_SEMILLAS"), out var n) ? Enumerable.Range(1, n).ToArray() : new[] { 1, 2, 3, 4, 5 };

        private sealed class Job
        {
            public string Kind = "", Site = "";
            public int Seats, Seed, A, B;
            public bool Two, Rifles, Awake;
            /// <summary>An uneven split's own run (A and B soldiers of each player); 0, the squad as it comes.</summary>
            public bool IsUneven => A > 0;
        }

        private sealed class Fight
        {
            public Job Job = new Job();
            public string Outcome = "", Reason = "";
            public bool Stuck;
            public int Turn, Fallen, Reward, Orders;
            public int[] Sizes = new int[0];
            /// <summary>At the start, each group's (or the squad's) nearest of the dead, and the dead within 10 of it.</summary>
            public List<int> Nearest = new List<int>(), Within10 = new List<int>();
            /// <summary>At the start, each group's first one's walk to the nearest of La Lata's zone (steps; -1, none).</summary>
            public List<int> Walk = new List<int>();
            /// <summary>Per group: the turn its first one got into La Lata (or went in it), and the turn it was all fallen.</summary>
            public int?[] Arrived = new int?[2], Fell = new int?[2];
            /// <summary>The group on side A, the first sweep's (Setup.Split): away from the usual start; the other, on its side (B).</summary>
            public int Far;
            public double Seconds;

            public bool Won => Outcome == Outcomes.Victory;
            public bool Wiped => Reason == EndReasons.Wiped;
            public bool OutNoReward => Outcome == Outcomes.Defeat && !Wiped;
            /// <summary>A group all fallen before any of it was in La Lata.</summary>
            public bool FellBefore(int g) => Fell[g] != null && Arrived[g] == null;
            public bool AnyFellBefore => Sizes.Length == 2 && (FellBefore(0) || FellBefore(1));
            public int? FirstFell => Enumerable.Range(0, Sizes.Length).Where(FellBefore).Select((g) => Fell[g]).Min();
        }

        private static void Arm(CampaignState c, CampaignSoldier x)
        {
            InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
            InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
            InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
        }

        /// <summary>
        /// One fight, as RunBench.Play: a campaign of `seed`, the squad upgrade for the job's seats and soldiers to fill them,
        /// rifles at difficulty 2 or knives at 1, a node of row 1 made the job's kind at its site on map seed 100 + `seed`; the
        /// bots for every seat there, folded into the run as the room does it.
        /// </summary>
        private static Fight Play(Job job)
        {
            var clock = Stopwatch.StartNew();
            var c = job.Two ? Two(job.Seed) : Solo(job.Seed);
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = Array.IndexOf(Seats, job.Seats);
            var rng0 = new Rng(job.Seed * 101);
            int Of(int owner) => c.Soldiers.Count((x) => x.Alive && x.Owner == owner);
            var want = job.IsUneven ? new[] { job.A, job.B } : null;
            while (c.Soldiers.Count((x) => x.Alive) < job.Seats || (want != null && (Of(0) < want[0] || Of(1) < want[1])))
            {
                var owner = want != null ? (Of(0) < want[0] ? 0 : 1) : c.Players[c.Soldiers.Count % c.Players.Count];
                Runs.NewSoldier(c, owner, rng0);
            }
            if (job.Rifles) foreach (var x in c.Soldiers) Arm(c, x);
            c.Squad = want != null
                ? c.Soldiers.Where((x) => x.Alive && x.Owner == 0).Take(want[0]).Concat(c.Soldiers.Where((x) => x.Alive && x.Owner == 1).Take(want[1])).Select((x) => x.Id).ToList()
                : Runs.DefaultSquad(c, c.Players);
            var run = Start(c, job.Seed);
            var n = RunMap.Choices(run)[0];
            n.Kind = job.Kind;
            n.Modifier = job.Awake ? Modifiers.Awake : null;
            n.Seed = 100 + job.Seed;
            n.Site = job.Site;
            if (job.Site == Places.Town) n.Site = Places.Of(RunMap.MapOf(n)!);
            n.Difficulty = job.Rifles ? 2 : 1;
            var choose = new ChooseNodeCommand { Node = n.Id };
            if (job.Two) Agree(c, choose);
            else Ok(c, choose);
            var rules = new EngineCampaignRules();
            var s = rules.LaunchMission(c, new Rng(job.Seed * 31))!.Value.state;
            var slots = job.Two ? Both : Zero;
            var f = new Fight { Job = job, Far = unchecked((uint)Math.Abs(n.Seed ?? 0) * 2654435761u) >> 31 == 1 ? 1 : 0 };
            var groups = s.Mission.Split?.Select((ids) => ids.Select((id) => Grid.GetUnit(s, id)!).ToList()).ToList();
            if (groups != null) f.Sizes = groups.Select((g) => g.Count).ToArray();
            var dead = s.Units.Where((u) => u.Alive && u.Team == Teams.Alien).ToList();
            int Gap(Unit a, Unit b) => Math.Max(Math.Abs(a.Pos.X - b.Pos.X), Math.Abs(a.Pos.Y - b.Pos.Y));
            foreach (var g in groups ?? new List<List<Unit>> { s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive).ToList() })
            {
                f.Nearest.Add(dead.Select((d) => g.Min((u) => Gap(u, d))).DefaultIfEmpty(99).Min());
                f.Within10.Add(dead.Count((d) => g.Any((u) => Gap(u, d) <= 10)));
                var reach = Paths.ComputeReach(s, g[0], 1_000_000, ignore: (_) => true).Cost;
                var zone = s.Mission.EvacZone.Where((z) => Grid.InBounds(s, (int)z.X, (int)z.Y)).Select((z) => Grid.TileIndex(s, (int)z.X, (int)z.Y)).Where(reach.ContainsKey).Select((i) => reach[i]).ToList();
                f.Walk.Add(zone.Count == 0 ? -1 : zone.Min() / Paths.CostOrtho);
            }
            void Watch()
            {
                if (groups == null) return;
                for (var g = 0; g < groups.Count; g++)
                {
                    if (f.Arrived[g] == null && groups[g].Any((u) => u.Evacuated)) f.Arrived[g] = s.Turn;
                    if (f.Fell[g] == null && !groups[g].Any((u) => u.Alive || u.Evacuated)) f.Fell[g] = s.Turn;
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
                    if (Rules.ExecuteCommand(s, cmd, slot, slots, rng).Ok) any = true;
                    Watch();
                }
                idle = any ? 0 : idle + 1;
            }
            f.Turn = s.Turn;
            f.Orders = orders;
            f.Stuck = s.Outcome == null;
            if (s.Outcome != null)
            {
                f.Outcome = s.Outcome;
                f.Reason = s.OutcomeReason ?? "";
                rules.ApplyMissionResult(c, s, new Rng(job.Seed));
                var report = c.LastReport!.Run!;
                f.Fallen = report.Fallen.Count;
                f.Reward = report.Reward ?? 0;
            }
            f.Seconds = clock.Elapsed.TotalSeconds;
            return f;
        }

        // ------------------------------------------------------------------ the tables

        private static string Pct(int a, int b) => b == 0 ? "–" : $"{100.0 * a / b:0} %";

        /// <summary>«mediana 6 (p10 3, p90 11; 2–14)»; «–» with none.</summary>
        private static string Spread(IEnumerable<int> xs)
        {
            var v = xs.OrderBy((x) => x).ToList();
            if (v.Count == 0) return "–";
            int At(double q) => v[Math.Min(v.Count - 1, (int)Math.Floor(q * (v.Count - 1) + 0.5))];
            return $"mediana {At(0.5)} (p10 {At(0.1)}, p90 {At(0.9)}; {v[0]}–{v[^1]})";
        }

        /// <summary>A histogram by turn: «1–2: 3 · 3–4: 5 · … · 13+: 1».</summary>
        private static string ByTurn(IEnumerable<int> xs)
        {
            var v = xs.ToList();
            if (v.Count == 0) return "–";
            var cells = new List<string>();
            for (var t = 1; t <= 11; t += 2) cells.Add($"{t}–{t + 1}: {v.Count((x) => x >= t && x <= t + 1)}");
            cells.Add($"13+: {v.Count((x) => x >= 13)}");
            return string.Join(" · ", cells);
        }

        private static string SiteName(string site) => site == Places.Town ? "Pueblo o ciudad" : Places.Names[site];

        [BenchFact]
        public void R1BySeats()
        {
            var seeds = BenchSeeds;
            var sites = RunMap.SplitSites.Where((x) => x != Places.City).ToArray();
            var sidesOnly = Environment.GetEnvironmentVariable("BASTION_BANCO_PARTE") == "lados";
            var jobs = new List<Job>();
            foreach (var kind in sidesOnly ? new[] { NodeKinds.Split } : new[] { NodeKinds.Split, NodeKinds.Normal })
                foreach (var seats in Seats)
                    foreach (var two in new[] { false, true })
                        foreach (var site in sites)
                            foreach (var rifles in new[] { true, false })
                                foreach (var seed in seeds)
                                    jobs.Add(new Job { Kind = kind, Seats = seats, Two = two, Site = site, Rifles = rifles, Seed = seed });
            foreach (var (seats, a, b) in sidesOnly ? new (int, int, int)[0] : Uneven)
                foreach (var site in sites)
                    foreach (var rifles in new[] { true, false })
                        foreach (var seed in seeds)
                            jobs.Add(new Job { Kind = NodeKinds.Split, Seats = seats, Two = true, A = a, B = b, Site = site, Rifles = rifles, Seed = seed });
            foreach (var seats in sidesOnly ? new int[0] : Seats)
                foreach (var site in sites)
                    foreach (var rifles in new[] { true, false })
                        foreach (var seed in seeds)
                            jobs.Add(new Job { Kind = NodeKinds.Split, Seats = seats, Two = true, Awake = true, Site = site, Rifles = rifles, Seed = seed });
            var clock = Stopwatch.StartNew();
            var fights = new Fight[jobs.Count];
            Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i) => fights[i] = Play(jobs[i]));

            var main = fights.Where((f) => !f.Job.IsUneven && !f.Job.Awake).ToList();
            var lines = new List<string>
            {
                $"# R1 de Separados por plazas: {fights.Length} combates en {clock.Elapsed.TotalMinutes:0.0} min ({fights.Average((f) => f.Seconds):0.0} s cada uno)",
                "",
                $"Sitios: {string.Join(", ", sites.Select(SiteName))}. Semillas de mapa {100 + seeds[0]}–{100 + seeds[^1]}, fusiles (dificultad 2) y cuchillos (dificultad 1), el bot de hoy en cada asiento. «Llegar»: el primero del grupo dentro de La Lata.",
                "",
                "El bot de Separados de Red (MissionBot, 20/11): cada grupo va a La Lata desde el turno 1 y registra solo lo que tiene al lado; no sale, ni deja salir, mientras al grupo sin nadie a bordo le quede alguien que pueda llegar. Mide los mapas, las reglas y ese bot, no cómo jugarían dos personas.",
                "",
                "**El «premio» mide también al bot:** la primera R1 (bot sin Separados) salía sin esperar al otro grupo.",
                "",
                "Desde el cuadro 95 (20/11), el mapa aparta a los muertos de las dos salidas como de la de siempre (Setup.Split).",
                "",
            };
            if (sidesOnly) lines.Insert(1, "Solo la parte de los dos lados (BASTION_BANCO_PARTE=lados): Separados tal cual, sin el Saqueo, los repartos desiguales ni el despierto.\n");
            void Table(string title, Func<Fight, bool> which, bool withSaqueo)
            {
                lines.Add($"## {title}");
                lines.Add("");
                lines.Add(withSaqueo
                    ? "| Plazas | Jugadores | Armas | Separados | premio | sin premio | cae la escuadra | ⚠ | turnos | Saqueo: ganadas | cae la escuadra | turnos |"
                    : "| Plazas | Jugadores | Armas | Separados | premio | sin premio | cae la escuadra | ⚠ | turnos |");
                lines.Add(withSaqueo ? "|---|---|---|---|---|---|---|---|---|---|---|---|" : "|---|---|---|---|---|---|---|---|---|");
                foreach (var seats in Seats)
                    foreach (var two in new[] { false, true })
                        foreach (var rifles in new[] { true, false })
                        {
                            var sp = fights.Where((f) => which(f) && f.Job.Kind == NodeKinds.Split && f.Job.Seats == seats && f.Job.Two == two && f.Job.Rifles == rifles).ToList();
                            if (sp.Count == 0) continue;
                            var row = $"| {seats} | {(two ? "dos" : "uno")} | {(rifles ? "fusiles" : "cuchillos")} | {sp.Count} | {Pct(sp.Count((f) => f.Won), sp.Count)} | {Pct(sp.Count((f) => f.OutNoReward), sp.Count)} | {Pct(sp.Count((f) => f.Wiped), sp.Count)} | {sp.Count((f) => f.Stuck)} | {Spread(sp.Where((f) => !f.Stuck).Select((f) => f.Turn))} |";
                            if (withSaqueo)
                            {
                                var sq = fights.Where((f) => f.Job.Kind == NodeKinds.Normal && f.Job.Seats == seats && f.Job.Two == two && f.Job.Rifles == rifles).ToList();
                                row += $" {Pct(sq.Count((f) => f.Won), sq.Count)} | {Pct(sq.Count((f) => f.Wiped), sq.Count)} | {Spread(sq.Where((f) => !f.Stuck).Select((f) => f.Turn))} |";
                            }
                            lines.Add(row);
                        }
                lines.Add("");
            }
            Table("Cómo acaba: Separados contra el Saqueo en los mismos mapas", (f) => !f.Job.IsUneven && !f.Job.Awake, true);

            lines.Add("## Un grupo entero cae antes de llegar a La Lata");
            lines.Add("");
            lines.Add("| Plazas | Jugadores | Armas | combates | cae un grupo antes de llegar | turno | por turno |");
            lines.Add("|---|---|---|---|---|---|---|");
            foreach (var seats in Seats)
                foreach (var two in new[] { false, true })
                    foreach (var rifles in new[] { true, false })
                    {
                        var sp = main.Where((f) => f.Job.Kind == NodeKinds.Split && f.Job.Seats == seats && f.Job.Two == two && f.Job.Rifles == rifles && f.Sizes.Length == 2).ToList();
                        var fell = sp.Where((f) => f.AnyFellBefore).ToList();
                        lines.Add($"| {seats} | {(two ? "dos" : "uno")} | {(rifles ? "fusiles" : "cuchillos")} | {sp.Count} | {fell.Count} ({Pct(fell.Count, sp.Count)}) | {Spread(fell.Select((f) => f.FirstFell!.Value))} | {ByTurn(fell.Select((f) => f.FirstFell!.Value))} |");
                    }
            lines.Add("");
            var lone = main.Where((f) => f.Job.Kind == NodeKinds.Split && f.Job.Seats == 2 && f.Sizes.Length == 2).ToList();
            lines.Add("## Con 2 plazas: el soldado solo de cada grupo");
            lines.Add("");
            foreach (var rifles in new[] { true, false })
            {
                var mine = lone.Where((f) => f.Job.Rifles == rifles).ToList();
                var arrive = mine.SelectMany((f) => Enumerable.Range(0, 2).Where((g) => f.Arrived[g] != null).Select((g) => f.Arrived[g]!.Value)).ToList();
                var fell = mine.SelectMany((f) => Enumerable.Range(0, 2).Where(f.FellBefore).Select((g) => f.Fell[g]!.Value)).ToList();
                lines.Add($"- {(rifles ? "Fusiles" : "Cuchillos")}: {mine.Count * 2} soldados solos en {mine.Count} combates. Llegan {arrive.Count} ({Spread(arrive)}); caen antes de llegar {fell.Count} ({Spread(fell)}; {ByTurn(fell)}). Premio en {Pct(mine.Count((f) => f.Won), mine.Count)}.");
            }
            lines.Add("");

            // The sides as the map has them: A, away from the usual start (the first sweep's); B, on its side, where the map put
            // the dead away from the squad.
            var paired = main.Where((f) => f.Job.Kind == NodeKinds.Split && f.Sizes.Length == 2 && f.Nearest.Count == 2).ToList();
            lines.Add("## Los dos lados");
            lines.Add("");
            lines.Add($"Lado A: el que queda lejos de la salida de siempre. Lado B: el de la salida de siempre, de donde el mapa ya apartaba a los muertos. En {paired.Count} combates (sin desiguales ni despierto):");
            lines.Add($"- Cae antes de llegar: lado A {paired.Count((f) => f.FellBefore(f.Far))} ({Pct(paired.Count((f) => f.FellBefore(f.Far)), paired.Count)}); lado B {paired.Count((f) => f.FellBefore(1 - f.Far))} ({Pct(paired.Count((f) => f.FellBefore(1 - f.Far)), paired.Count)}).");
            lines.Add($"- Muerto más cerca al empezar: lado A {Spread(paired.Select((f) => f.Nearest[f.Far]))}; lado B {Spread(paired.Select((f) => f.Nearest[1 - f.Far]))}.");
            lines.Add($"- El primer grupo (el del jugador 1) en el lado A: {paired.Count((f) => f.Far == 0)} de {paired.Count}.");
            // The walks to La Lata, each side's and the longer against the shorter (Setup.Split's SplitEven, 20/11).
            var walked = paired.Where((f) => f.Walk.Count == 2 && f.Walk.All((w) => w > 0)).ToList();
            int Ratio(Fight f) => (int)Math.Round(100.0 * f.Walk.Max() / f.Walk.Min());
            lines.Add($"- A pie hasta La Lata (pasos): lado A {Spread(paired.Select((f) => f.Walk[f.Far]))}; lado B {Spread(paired.Select((f) => f.Walk[1 - f.Far]))}; el largo contra el corto, en centésimas: {Spread(walked.Select(Ratio))}.");
            lines.Add("");
            lines.Add("| Sitio | combates | cae el lado A | cae el B | a pie: lado A | lado B | largo / corto | muerto más cerca: lado A | lado B | muertos a ≤10: A | B |");
            lines.Add("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var site in sites)
            {
                var mine = paired.Where((f) => f.Job.Site == site).ToList();
                lines.Add($"| {SiteName(site)} | {mine.Count} | {Pct(mine.Count((f) => f.FellBefore(f.Far)), mine.Count)} | {Pct(mine.Count((f) => f.FellBefore(1 - f.Far)), mine.Count)} | {Spread(mine.Select((f) => f.Walk[f.Far]))} | {Spread(mine.Select((f) => f.Walk[1 - f.Far]))} | {Spread(walked.Where((f) => f.Job.Site == site).Select(Ratio))} | {Spread(mine.Select((f) => f.Nearest[f.Far]))} | {Spread(mine.Select((f) => f.Nearest[1 - f.Far]))} | {mine.Select((f) => f.Within10[f.Far]).DefaultIfEmpty(0).Average():0.0} | {mine.Select((f) => f.Within10[1 - f.Far]).DefaultIfEmpty(0).Average():0.0} |");
            }
            lines.Add("");
            // And by the walk: the group with the longer one against the other (the ties out).
            var longer = walked.Where((f) => f.Walk[0] != f.Walk[1]).ToList();
            int Long(Fight f) => f.Walk[0] > f.Walk[1] ? 0 : 1;
            lines.Add($"Y por el camino: el grupo con el camino a pie más largo contra el otro, en {longer.Count} combates (los empates fuera): cae antes de llegar el del largo {longer.Count((f) => f.FellBefore(Long(f)))} ({Pct(longer.Count((f) => f.FellBefore(Long(f))), longer.Count)}), el del corto {longer.Count((f) => f.FellBefore(1 - Long(f)))} ({Pct(longer.Count((f) => f.FellBefore(1 - Long(f))), longer.Count)}).");
            lines.Add("");
            // And by the dead: the group whose nearest was nearer at the start (one is always nearer, by its definition).
            var sided = paired.Where((f) => f.Nearest[0] != f.Nearest[1]).ToList();
            int Hard(Fight f) => f.Nearest[0] < f.Nearest[1] ? 0 : 1;
            lines.Add($"Y por el muerto más cerca: el grupo que lo tenía más cerca al empezar contra el otro (uno siempre lo tiene más cerca), en {sided.Count} combates (los empates fuera):");
            lines.Add($"- Cae antes de llegar: el lado con muertos {sided.Count((f) => f.FellBefore(Hard(f)))} ({Pct(sided.Count((f) => f.FellBefore(Hard(f))), sided.Count)}); el otro {sided.Count((f) => f.FellBefore(1 - Hard(f)))} ({Pct(sided.Count((f) => f.FellBefore(1 - Hard(f))), sided.Count)}).");
            lines.Add($"- Muerto más cerca al empezar: el lado con muertos {Spread(sided.Select((f) => f.Nearest[Hard(f)]))}; el otro {Spread(sided.Select((f) => f.Nearest[1 - Hard(f)]))}.");
            lines.Add($"- El del jugador 1 (el primer grupo) en el lado con muertos: {sided.Count((f) => Hard(f) == 0)} de {sided.Count}.");
            var both = main.Where((f) => f.Job.Kind == NodeKinds.Split && f.Sizes.Length == 2).ToList();
            lines.Add($"- Por grupo, en {both.Count} combates: el primero cae antes de llegar {both.Count((f) => f.FellBefore(0))} ({Pct(both.Count((f) => f.FellBefore(0)), both.Count)}), el segundo {both.Count((f) => f.FellBefore(1))} ({Pct(both.Count((f) => f.FellBefore(1)), both.Count)}).");
            lines.Add("");
            lines.Add("| Sitio | combates | cae el lado con muertos | cae el otro | muerto más cerca: lado con muertos | el otro |");
            lines.Add("|---|---|---|---|---|---|");
            foreach (var site in sites)
            {
                var mine = sided.Where((f) => f.Job.Site == site).ToList();
                lines.Add($"| {SiteName(site)} | {mine.Count} | {Pct(mine.Count((f) => f.FellBefore(Hard(f))), mine.Count)} | {Pct(mine.Count((f) => f.FellBefore(1 - Hard(f))), mine.Count)} | {Spread(mine.Select((f) => f.Nearest[Hard(f)]))} | {Spread(mine.Select((f) => f.Nearest[1 - Hard(f)]))} |");
            }
            lines.Add("");

            // The uneven splits and the awake: not in the part of the two sides.
            if (!sidesOnly)
            {
            lines.Add("## Repartos desiguales a dos (por dueño)");
            lines.Add("");
            lines.Add("| Plazas | Reparto | Armas | combates | cae el grupo pequeño antes de llegar | turno | en el lado con muertos | cae el grande antes | premio | sin premio | cae la escuadra | ⚠ |");
            lines.Add("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var (seats, a, b) in Uneven)
                foreach (var rifles in new[] { true, false })
                {
                    var mine = fights.Where((f) => f.Job.IsUneven && f.Job.Seats == seats && f.Job.A == a && f.Job.B == b && f.Job.Rifles == rifles && f.Sizes.Length == 2).ToList();
                    // Slot 0's group is the first (Setup.SplitGroups): the big one.
                    var small = mine.Where((f) => f.FellBefore(1)).ToList();
                    var hardSide = mine.Where((f) => f.Nearest.Count == 2 && f.Nearest[1] < f.Nearest[0]).ToList();
                    lines.Add($"| {seats} | {a} y {b} | {(rifles ? "fusiles" : "cuchillos")} | {mine.Count} | {small.Count} ({Pct(small.Count, mine.Count)}) | {Spread(small.Select((f) => f.Fell[1]!.Value))} | {hardSide.Count((f) => f.FellBefore(1))} de {hardSide.Count} | {mine.Count((f) => f.FellBefore(0))} | {Pct(mine.Count((f) => f.Won), mine.Count)} | {Pct(mine.Count((f) => f.OutNoReward), mine.Count)} | {Pct(mine.Count((f) => f.Wiped), mine.Count)} | {mine.Count((f) => f.Stuck)} |");
                }
            lines.Add("");
            lines.Add("Para comparar, el mismo número de plazas con el reparto igual está en las tablas de arriba (a dos).");
            lines.Add("");

            Table("Separados despierto (lo de la gasolinera), a dos", (f) => f.Job.Awake, false);
            lines.Add("Un grupo entero cae antes de llegar, despierto:");
            lines.Add("");
            foreach (var seats in Seats)
                foreach (var rifles in new[] { true, false })
                {
                    var aw = fights.Where((f) => f.Job.Awake && f.Job.Seats == seats && f.Job.Rifles == rifles && f.Sizes.Length == 2).ToList();
                    var calm = main.Where((f) => f.Job.Kind == NodeKinds.Split && f.Job.Two && f.Job.Seats == seats && f.Job.Rifles == rifles && f.Sizes.Length == 2).ToList();
                    var fell = aw.Where((f) => f.AnyFellBefore).ToList();
                    lines.Add($"- {seats} plazas, {(rifles ? "fusiles" : "cuchillos")}: {fell.Count} de {aw.Count} ({Pct(fell.Count, aw.Count)}; sin despertar, {Pct(calm.Count((f) => f.AnyFellBefore), calm.Count)}); turno {Spread(fell.Select((f) => f.FirstFell!.Value))}.");
                }
            lines.Add("");

            }

            lines.Add("## Por sitio (Separados, sin desiguales ni despierto)");
            lines.Add("");
            lines.Add("| Sitio | combates | premio | sin premio | cae la escuadra | cae un grupo antes de llegar | ⚠ | muerto más cerca al empezar | muertos a ≤10 | Saqueo: más cerca | a ≤10 |");
            lines.Add("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var site in sites)
            {
                var mine = main.Where((f) => f.Job.Kind == NodeKinds.Split && f.Job.Site == site).ToList();
                var sq = main.Where((f) => f.Job.Kind == NodeKinds.Normal && f.Job.Site == site).ToList();
                lines.Add($"| {SiteName(site)} | {mine.Count} | {Pct(mine.Count((f) => f.Won), mine.Count)} | {Pct(mine.Count((f) => f.OutNoReward), mine.Count)} | {Pct(mine.Count((f) => f.Wiped), mine.Count)} | {Pct(mine.Count((f) => f.AnyFellBefore), mine.Count)} | {mine.Count((f) => f.Stuck)} | {Spread(mine.SelectMany((f) => f.Nearest))} | {mine.SelectMany((f) => f.Within10).DefaultIfEmpty(0).Average():0.0} | {Spread(sq.SelectMany((f) => f.Nearest))} | {sq.SelectMany((f) => f.Within10).DefaultIfEmpty(0).Average():0.0} |");
            }
            lines.Add("");
            lines.Add($"Sin dos grupos (un solo soldado, o sin sitio en el mapa): {fights.Count((f) => f.Job.Kind == NodeKinds.Split && f.Sizes.Length != 2)} de {fights.Count((f) => f.Job.Kind == NodeKinds.Split)}.");

            var folder = BenchFactAttribute.Folder!;
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "r1-separados.md"), lines);
            File.WriteAllLines(Path.Combine(folder, "r1-separados-combates.txt"), fights.Select((f) =>
                $"{f.Job.Kind} {f.Job.Site} {f.Job.Seats} plazas {(f.Job.Two ? "dos" : "uno")}{(f.Job.IsUneven ? $" {f.Job.A}y{f.Job.B}" : "")}{(f.Job.Awake ? " despierto" : "")} {(f.Job.Rifles ? "fusiles" : "cuchillos")} semilla {f.Job.Seed}: "
                + $"{(f.Stuck ? "ATASCADO" : $"{f.Outcome} ({f.Reason})")} t{f.Turn}, grupos {string.Join("/", f.Sizes)}, llegan t{f.Arrived[0]?.ToString() ?? "-"}/t{f.Arrived[1]?.ToString() ?? "-"}, caen t{f.Fell[0]?.ToString() ?? "-"}/t{f.Fell[1]?.ToString() ?? "-"}, "
                + $"{f.Fallen} caídos, {f.Reward} §, {f.Orders} órdenes, {f.Seconds:0.0} s"));
            Assert.DoesNotContain(fights, (f) => f.Stuck);
        }
    }
}
