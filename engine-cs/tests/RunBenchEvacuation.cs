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
    /// G23.6 Evacuación's R1 by seats (EVACUACION.md, «Cómo se mide»; Producto, 21/11: «por qué no llegan»): Evacuación against
    /// the Saqueo on the same maps, 2, 4, 6 and 8 seats, alone, its four sites, rifles at difficulty 2, Red's bot. Fight by
    /// fight, each civilian: when found, when aboard, how it died and by whom, how far from La Lata it was left, its panics
    /// and what each cost (the walk to La Lata before and after that step); and when the horde came. Run only with
    /// BASTION_BANCO (`BASTION_BANCO=<carpeta> dotnet test engine-cs/tests --filter RunBenchEvacuation`); BASTION_BANCO_SEMILLAS
    /// seeds (5); BASTION_BANCO_NOMBRE the files' name (r1-evacuacion). Writes its .md and its combates.txt there.
    /// </summary>
    public class RunBenchEvacuation
    {
        private static readonly int[] Seats = { 2, 4, 6, 8 };

        private static int[] BenchSeeds => int.TryParse(Environment.GetEnvironmentVariable("BASTION_BANCO_SEMILLAS"), out var n) ? Enumerable.Range(1, n).ToArray() : new[] { 1, 2, 3, 4, 5 };

        private sealed class Job
        {
            public string Kind = "", Site = "";
            public int Seats, Seed;
        }

        private sealed class Civ
        {
            public int? Found, Aboard, Died, Hid;
            public string? Killer;
            public int Refound;
            /// <summary>Its walk to La Lata in tiles when first found, and at the end (alive, not aboard; null: aboard or dead).</summary>
            public int? WalkFound, WalkLeft;
            /// <summary>Each panic: its kind and the walk to La Lata before and after that step (after: when it hid, when found again).</summary>
            public readonly List<(string Kind, int Before, int After, int Turns)> Panics = new List<(string, int, int, int)>();
        }

        private sealed class Fight
        {
            public Job Job = new Job();
            /// <summary>The site as drawn: a town's map is a town or a city by its seed (Places.Of).</summary>
            public string Site = "";
            public string Outcome = "", Reason = "";
            public int Turn, Fallen, FallenToHorde, Aboard;
            public int? Horde, FirstFell;
            public bool Stuck;
            public readonly List<Civ> Civs = new List<Civ>();
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

        /// <summary>`u`'s walk to La Lata's zone, in tiles, through anyone (null: none).</summary>
        private static int? WalkLeft(GameState s, Unit u)
        {
            var cost = Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost;
            var best = s.Mission.EvacZone.Select((v) => cost.TryGetValue(Grid.NodeOf(s, new Pos { X = v.X, Y = v.Y, Z = 0 }), out var c) ? c : double.MaxValue).DefaultIfEmpty(double.MaxValue).Min();
            return best == double.MaxValue ? null : (int?)Math.Round(best / Paths.CostOrtho);
        }

        private static Fight Play(Job job)
        {
            var clock = Stopwatch.StartNew();
            var c = Solo(job.Seed);
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = Array.IndexOf(Seats, job.Seats);
            var rng0 = new Rng(job.Seed * 101);
            while (c.Soldiers.Count((x) => x.Alive) < job.Seats) Runs.NewSoldier(c, 0, rng0);
            foreach (var x in c.Soldiers) Arm(c, x);
            c.Squad = Runs.DefaultSquad(c, c.Players);
            var run = Start(c, job.Seed);
            var n = RunMap.Choices(run)[0];
            n.Kind = job.Kind;
            n.Modifier = null;
            n.Seed = 100 + job.Seed;
            n.Site = job.Site;
            if (job.Site == Places.Town) n.Site = Places.Of(RunMap.MapOf(n)!);
            n.Difficulty = 2;
            Ok(c, new ChooseNodeCommand { Node = n.Id });
            var rules = new EngineCampaignRules();
            var s = rules.LaunchMission(c, new Rng(job.Seed * 31))!.Value.state;
            var f = new Fight { Job = job, Site = n.Site! };
            var civs = (s.Mission.Civilians ?? new List<CivilianSpot>()).ToDictionary((x) => x.Unit, (_) => new Civ());
            foreach (var kv in civs) f.Civs.Add(kv.Value);
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).Select((u) => u.Id).ToHashSet();
            var horde = new HashSet<string>();
            var hidAt = new Dictionary<string, (int Turn, int Before)>();
            var rng = new Rng(job.Seed * 7919 + 13);
            int orders = 0, idle = 0;
            while (s.Outcome == null && orders < 3000 && idle < 3)
            {
                var cmd = MissionBot.Next(s, 0, Zero);
                if (cmd == null)
                {
                    idle++;
                    continue;
                }
                orders++;
                // Before the turn ends (the civilians' steps), each found one's walk to La Lata.
                var before = cmd is EndTurnCommand ? civs.Keys.Select((id) => Grid.GetUnit(s, id)).Where((u) => u != null && u.Alive && u.Aboard != true).ToDictionary((u) => u!.Id, (u) => WalkLeft(s, u!)) : null;
                var r = Rules.ExecuteCommand(s, cmd, 0, Zero, rng);
                idle = r.Ok ? 0 : idle + 1;
                foreach (var e in r.Events ?? new List<GameEvent>())
                {
                    switch (e)
                    {
                        case SpawnedEvent sp:
                            foreach (var u in sp.Units) horde.Add(u.Id);
                            f.Horde ??= s.Turn;
                            break;
                        case CivilianFoundEvent cf when civs.TryGetValue(cf.Unit, out var cv):
                            if (cv.Found == null)
                            {
                                cv.Found = s.Turn;
                                var u = Grid.GetUnit(s, cf.Unit);
                                if (u != null) cv.WalkFound = WalkLeft(s, u);
                            }
                            else cv.Refound++;
                            if (hidAt.TryGetValue(cf.Unit, out var hid))
                            {
                                var u = Grid.GetUnit(s, cf.Unit);
                                var at = u != null ? WalkLeft(s, u) ?? hid.Before : hid.Before;
                                cv.Panics.Add((PanicKinds.Hide, hid.Before, at, s.Turn - hid.Turn));
                                hidAt.Remove(cf.Unit);
                            }
                            break;
                        case CivilianHidEvent ch when civs.ContainsKey(ch.Unit):
                            civs[ch.Unit].Hid ??= s.Turn;
                            hidAt[ch.Unit] = (s.Turn, before != null && before.TryGetValue(ch.Unit, out var b) && b != null ? b.Value : 0);
                            break;
                        case CivilianPanickedEvent cp when civs.TryGetValue(cp.Unit, out var pv) && cp.Kind != PanicKinds.Hide:
                        {
                            var u = Grid.GetUnit(s, cp.Unit);
                            var b0 = before != null && before.TryGetValue(cp.Unit, out var bb) && bb != null ? bb.Value : 0;
                            pv.Panics.Add((cp.Kind, b0, u != null ? WalkLeft(s, u) ?? b0 : b0, 1));
                            break;
                        }
                        case BoardedEvent bo when civs.TryGetValue(bo.Unit, out var bv):
                            bv.Aboard ??= s.Turn;
                            break;
                        case DiedEvent d:
                            if (civs.TryGetValue(d.Unit, out var dv))
                            {
                                dv.Died ??= s.Turn;
                                var by = d.By != null ? Grid.GetUnit(s, d.By) : null;
                                dv.Killer = by == null ? "?" : horde.Contains(by.Id) ? "horda" : Content.Templates[by.Template].Name;
                            }
                            else if (squad.Contains(d.Unit))
                            {
                                f.FirstFell ??= s.Turn;
                                if (d.By != null && horde.Contains(d.By)) f.FallenToHorde++;
                            }
                            break;
                    }
                }
            }
            // A hide still not found again at the end: its cost so far.
            foreach (var kv in hidAt) civs[kv.Key].Panics.Add((PanicKinds.Hide, kv.Value.Before, kv.Value.Before, s.Turn - kv.Value.Turn));
            f.Turn = s.Turn;
            f.Stuck = s.Outcome == null;
            f.Outcome = s.Outcome ?? "stuck";
            f.Reason = s.OutcomeReason ?? "";
            f.Aboard = s.Mission.Civilians != null ? Rules.CiviliansAboard(s) : 0;
            foreach (var kv in civs)
            {
                var u = Grid.GetUnit(s, kv.Key);
                if (u != null && u.Alive && u.Aboard != true && kv.Value.Found != null) kv.Value.WalkLeft = WalkLeft(s, u);
            }
            if (s.Outcome != null)
            {
                rules.ApplyMissionResult(c, s, new Rng(job.Seed));
                f.Fallen = c.LastReport!.Run!.Fallen.Count;
            }
            f.Seconds = clock.Elapsed.TotalSeconds;
            return f;
        }

        private static string P(int a, int b) => b == 0 ? "–" : $"{100.0 * a / b:0} %";

        private static string Spread(IEnumerable<int> xs)
        {
            var v = xs.OrderBy((x) => x).ToList();
            return v.Count == 0 ? "–" : $"{v[v.Count / 2]} ({v[0]}–{v[^1]})";
        }

        [Fact]
        public void R1()
        {
            var dir = Environment.GetEnvironmentVariable("BASTION_BANCO");
            if (string.IsNullOrEmpty(dir)) return;
            // A town's map is a town or a city by its seed (Places.Of): the city is drawn as a town, and told apart as it comes out.
            var sites = RunMap.EvacuationSites.Where((x) => x != Places.City && (Environment.GetEnvironmentVariable("BASTION_BANCO_SITIO") is not string only || x == only)).ToList();
            var named = new[] { Places.City, Places.Town, Places.Roadside, Places.Plaza };
            var jobs = new List<Job>();
            foreach (var kind in new[] { NodeKinds.Evacuation, NodeKinds.Normal })
                foreach (var seats in Seats)
                    foreach (var site in sites)
                        foreach (var seed in BenchSeeds)
                            jobs.Add(new Job { Kind = kind, Seats = seats, Site = site, Seed = seed });
            var clock = Stopwatch.StartNew();
            var fights = new Fight[jobs.Count];
            Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i) => fights[i] = Play(jobs[i]));
            var name = Environment.GetEnvironmentVariable("BASTION_BANCO_NOMBRE") ?? "r1-evacuacion";
            var evac = fights.Where((f) => f.Job.Kind == NodeKinds.Evacuation).ToList();
            var saqueo = fights.Where((f) => f.Job.Kind == NodeKinds.Normal).ToList();
            var lines = new List<string>
            {
                $"# R1 de Evacuación con el bot de Red: {fights.Length} combates en {clock.Elapsed.TotalMinutes:0.0} min",
                "",
                $"Sitios: {string.Join(", ", named.Select((x) => Places.Names[x]))} (ciudad o pueblo, por la semilla). Semillas de mapa {100 + BenchSeeds[0]}–{100 + BenchSeeds[^1]}. A uno, fusiles, dificultad 2. «Premio»: dos civiles o más a bordo. «Salen»: la escuadra llega a La Lata (con o sin premio).",
                "",
                "## Por plazas",
                "",
                "| Plazas | n | premio | a bordo: 0 / 1 / 2+ | salen | cae la escuadra | caídos (media) | civiles: encontrados / a bordo / muertos (media) | turnos | Saqueo: salen / cae / caídos |",
                "|---|---|---|---|---|---|---|---|---|---|",
            };
            void ByRows(Func<Fight, string> key, IEnumerable<string> keys, string title)
            {
                foreach (var k in keys)
                {
                    var r = evac.Where((f) => key(f) == k).ToList();
                    var q = saqueo.Where((f) => key(f) == k).ToList();
                    if (r.Count == 0) continue;
                    lines.Add($"| {k} | {r.Count} | {P(r.Count((f) => f.Aboard >= 2), r.Count)} | {P(r.Count((f) => f.Aboard == 0), r.Count)} / {P(r.Count((f) => f.Aboard == 1), r.Count)} / {P(r.Count((f) => f.Aboard >= 2), r.Count)} | {P(r.Count((f) => !f.Wiped && !f.Stuck), r.Count)} | {P(r.Count((f) => f.Wiped), r.Count)} | {r.Average((f) => (double)f.Fallen):0.0} | {r.Average((f) => (double)f.Civs.Count((x) => x.Found != null)):0.0} / {r.Average((f) => (double)f.Aboard):0.0} / {r.Average((f) => (double)f.Civs.Count((x) => x.Died != null)):0.0} | {Spread(r.Select((f) => f.Turn))} | {P(q.Count((f) => !f.Wiped && !f.Stuck), q.Count)} / {P(q.Count((f) => f.Wiped), q.Count)} / {(q.Count > 0 ? q.Average((f) => (double)f.Fallen) : 0):0.0} |");
                }
            }
            ByRows((f) => $"{f.Job.Seats}", Seats.Select((x) => $"{x}"), "plazas");
            lines.Add("");
            lines.Add("## Por sitio (todas las plazas)");
            lines.Add("");
            lines.Add("| Sitio | n | premio | a bordo: 0 / 1 / 2+ | salen | cae la escuadra | caídos (media) | civiles: encontrados / a bordo / muertos (media) | turnos | Saqueo: salen / cae / caídos |");
            lines.Add("|---|---|---|---|---|---|---|---|---|---|");
            ByRows((f) => Places.Names[f.Site], named.Select((x) => Places.Names[x]), "sitio");
            lines.Add("");
            var all = evac.SelectMany((f) => f.Civs.Select((x) => (F: f, C: x))).ToList();
            lines.Add("## Los civiles, por sitio");
            lines.Add("");
            lines.Add("| Sitio | civiles | encontrados | turno del primer hallazgo | a La Lata al encontrarlo (casillas) | a bordo | turno a bordo | de encontrado a bordo (turnos) | muertos (de los encontrados) | quedan andando al acabar: cuántos, casillas que les faltan |");
            lines.Add("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var site in named)
            {
                var cs = all.Where((x) => x.F.Site == site).Select((x) => x.C).ToList();
                if (cs.Count == 0) continue;
                var found = cs.Where((x) => x.Found != null).ToList();
                var left = found.Where((x) => x.WalkLeft != null).ToList();
                lines.Add($"| {Places.Names[site]} | {cs.Count} | {P(found.Count, cs.Count)} | {Spread(found.Select((x) => x.Found!.Value))} | {Spread(found.Where((x) => x.WalkFound != null).Select((x) => x.WalkFound!.Value))} | {P(cs.Count((x) => x.Aboard != null), cs.Count)} | {Spread(cs.Where((x) => x.Aboard != null).Select((x) => x.Aboard!.Value))} | {Spread(cs.Where((x) => x.Aboard != null && x.Found != null).Select((x) => x.Aboard!.Value - x.Found!.Value))} | {P(found.Count((x) => x.Died != null), found.Count)} | {left.Count}, {Spread(left.Select((x) => x.WalkLeft!.Value))} |");
            }
            lines.Add("");
            var dead = all.Where((x) => x.C.Died != null).ToList();
            lines.Add($"**Quién mata a los civiles** ({dead.Count}): {string.Join(", ", dead.GroupBy((x) => x.C.Killer ?? "?").OrderByDescending((g) => g.Count()).Select((g) => $"{g.Key} {g.Count()}"))}.");
            lines.Add("");
            var panics = all.SelectMany((x) => x.C.Panics).ToList();
            lines.Add($"## El pánico ({panics.Count} en {all.Count((x) => x.C.Panics.Count > 0)} civiles de {all.Count((x) => x.C.Found != null)} encontrados)");
            lines.Add("");
            lines.Add("Lo que cuesta cada uno: casillas de más que le quedan hasta La Lata tras ese paso, contra lo que habría andado (5), y turnos.");
            lines.Add("");
            lines.Add("| Pánico | veces | casillas perdidas (mediana, rango) | turnos perdidos (mediana, rango) |");
            lines.Add("|---|---|---|---|");
            foreach (var g in panics.GroupBy((p) => p.Kind).OrderBy((g) => g.Key))
                lines.Add($"| {g.Key} | {g.Count()} | {Spread(g.Select((p) => p.After - p.Before + (int)Content.CivilianMobility))} | {Spread(g.Select((p) => p.Kind == PanicKinds.Hide ? p.Turns : (p.After - p.Before + (int)Content.CivilianMobility + 4) / 5))} |");
            lines.Add("");
            lines.Add($"Pánicos por civil encontrado: {Spread(all.Where((x) => x.C.Found != null).Select((x) => x.C.Panics.Count))}. Escondidos de nuevo y vueltos a encontrar: {all.Sum((x) => x.C.Refound)}; escondidos que no se vuelven a encontrar: {all.Count((x) => x.C.Hid != null && x.C.Panics.Any((p) => p.Kind == PanicKinds.Hide && p.After == p.Before))}.");
            lines.Add("");
            lines.Add("## La horda y la escuadra");
            lines.Add("");
            lines.Add("| Plazas | la horda llega (turno) | primer caído (turno) | caídos | de ellos, por la horda |");
            lines.Add("|---|---|---|---|---|");
            foreach (var seats in Seats)
            {
                var r = evac.Where((f) => f.Job.Seats == seats).ToList();
                lines.Add($"| {seats} | {Spread(r.Where((f) => f.Horde != null).Select((f) => f.Horde!.Value))} ({r.Count((f) => f.Horde != null)} de {r.Count}) | {Spread(r.Where((f) => f.FirstFell != null).Select((f) => f.FirstFell!.Value))} | {r.Sum((f) => f.Fallen)} | {r.Sum((f) => f.FallenToHorde)} |");
            }
            File.WriteAllLines(Path.Combine(dir, name + ".md"), lines);
            File.WriteAllLines(Path.Combine(dir, name + "-combates.txt"), evac.Select((f) =>
                $"{Places.Names[f.Site]} {f.Job.Seats} pl. sem. {f.Job.Seed}: {f.Outcome}/{f.Reason} t{f.Turn}, a bordo {f.Aboard}, caídos {f.Fallen} (horda {f.FallenToHorde}), horda t{f.Horde?.ToString() ?? "-"} · "
                + string.Join(" · ", f.Civs.Select((x, i) => $"c{i + 1}: {(x.Found != null ? $"enc t{x.Found} ({x.WalkFound}c)" : "sin encontrar")}{(x.Aboard != null ? $", a bordo t{x.Aboard}" : "")}{(x.Died != null ? $", muere t{x.Died} ({x.Killer})" : "")}{(x.WalkLeft != null ? $", le faltan {x.WalkLeft}c" : "")}{(x.Panics.Count > 0 ? $", pánicos {string.Join("/", x.Panics.Select((p) => $"{p.Kind}{p.After - p.Before:+0;-0;0}"))}" : "")}"))));
            Assert.DoesNotContain(fights, (f) => f.Stuck);
        }
    }
}
