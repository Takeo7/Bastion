#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bastion.Net;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Red's long benches of the bots on the runs (H8; Producto, 17/11): run only with BASTION_BANCO set to the folder
    /// their tables go to (`BASTION_BANCO=/tmp/banco dotnet test engine-cs/tests --filter RunBench`). Without it, skipped.
    /// </summary>
    public sealed class BenchFactAttribute : FactAttribute
    {
        public const string Variable = "BASTION_BANCO";

        public static string? Folder => Environment.GetEnvironmentVariable(Variable);

        public BenchFactAttribute()
        {
            if (string.IsNullOrEmpty(Folder)) Skip = $"Solo con {Variable}=<carpeta>: banco largo de los bots (Red).";
        }
    }

    public class RunBench
    {
        private readonly ITestOutputHelper output;

        public RunBench(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6 };

        /// <summary>The sites R1 measures: the new maps (H7, H9), then the ones they stand in for, to compare.</summary>
        private static readonly string[] Sites =
        {
            Places.Offices, Places.Shed, Places.Police, Places.Plaza, Places.Camping, Places.Hospital,
            Places.Town, Places.Factory, Places.Country,
        };

        private static readonly string[] Kinds =
        {
            NodeKinds.Normal, NodeKinds.Hold, NodeKinds.Crossing, NodeKinds.Stealth, NodeKinds.Elite, NodeKinds.Sabotage,
            NodeKinds.Elimination, NodeKinds.Recovery, NodeKinds.Rescue,
        };

        /// <summary>What a site can be (RunMap.SpecialFor, Generate): a Sabotaje only where an alarm is, a Travesía never in one building.</summary>
        private static bool Drawn(string site, string kind)
        {
            var inside = site == Places.Offices || site == Places.Shed || site == Places.Police || site == Places.Hospital;
            if (kind == NodeKinds.Crossing) return !inside;
            if (kind == NodeKinds.Sabotage) return site == Places.Plaza || site == Places.Camping || site == Places.Factory;
            return true;
        }

        private sealed class Fight
        {
            public string Site = "", Kind = "", Map = "", Outcome = "", Reason = "";
            public bool Rifles, Done, Stuck;
            public int Seed, Turn, Fallen, Found, Supplies, Reward, Orders, Refused;
            public bool? Unseen;
            public double Seconds;
        }

        /// <summary>
        /// One fight: two players (a campaign of `seed`), their squad with rifles, a pistol, a vest and magazines or the knife
        /// only, at a node of row 1 made `kind` at `site` on map seed 100 + `seed`, with no modifier; difficulty 1 with knives
        /// (rows 1–2, where they go out with them), 2 with rifles. Played by MissionBot for both seats, as the apps do, and
        /// folded into the run as the room does it.
        /// </summary>
        private static Fight Play(string site, string kind, bool rifles, int seed)
        {
            var clock = Stopwatch.StartNew();
            var c = Two(seed);
            if (rifles)
                foreach (var x in c.Soldiers)
                {
                    InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                    InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                    InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
                    InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
                }
            var run = Start(c, seed);
            var n = RunMap.Choices(run)[0];
            n.Kind = kind;
            n.Modifier = null;
            n.Seed = 100 + seed;
            n.Site = site;
            // A town or a city is its map seed's (RunMap.Generate).
            if (site == Places.Town) n.Site = Places.Of(RunMap.MapOf(n)!);
            n.Difficulty = rifles ? 2 : 1;
            Agree(c, new ChooseNodeCommand { Node = n.Id });
            var rules = new EngineCampaignRules();
            var s = rules.LaunchMission(c, new Rng(seed * 31))!.Value.state;
            var rng = new Rng(seed * 7919 + 13);
            int orders = 0, refused = 0, idle = 0;
            while (s.Outcome == null && orders < 3000 && idle < 3)
            {
                var any = false;
                foreach (var slot in Both)
                {
                    var cmd = MissionBot.Next(s, slot, Both);
                    if (cmd == null) continue;
                    orders++;
                    if (Rules.ExecuteCommand(s, cmd, slot, Both, rng).Ok) any = true;
                    else refused++;
                }
                idle = any ? 0 : idle + 1;
            }
            var f = new Fight
            {
                Site = site, Kind = kind, Rifles = rifles, Seed = seed, Map = s.MapName ?? "", Turn = s.Turn,
                Done = s.Mission.ObjectiveDone, Orders = orders, Refused = refused, Stuck = s.Outcome == null,
            };
            if (s.Outcome != null)
            {
                f.Outcome = s.Outcome;
                f.Reason = s.OutcomeReason ?? "";
                rules.ApplyMissionResult(c, s, new Rng(seed));
                var report = c.LastReport!.Run!;
                f.Fallen = report.Fallen.Count;
                f.Found = report.Found.Count;
                f.Supplies = report.Found.Where((g) => g.Kind == GearKinds.Supplies).Sum((g) => g.Count);
                f.Reward = report.Reward ?? 0;
                f.Unseen = report.Unseen;
            }
            f.Seconds = clock.Elapsed.TotalSeconds;
            return f;
        }

        private static readonly Dictionary<string, string> KindNames = new Dictionary<string, string>
        {
            [NodeKinds.Normal] = "Saqueo", [NodeKinds.Hold] = "Asedio", [NodeKinds.Crossing] = "Travesía", [NodeKinds.Stealth] = "Sigilo*",
            [NodeKinds.Elite] = "Guarida", [NodeKinds.Sabotage] = "Sabotaje", [NodeKinds.Elimination] = "Limpieza",
            [NodeKinds.Recovery] = "Recuperación", [NodeKinds.Rescue] = "Rescate",
        };

        private static string SiteName(string site) => site == Places.Town ? "Pueblo o ciudad" : Places.Names[site];

        /// <summary>
        /// R1: every stage a site can be, on the new maps and the three they stand in for, with rifles and with knives. Only
        /// some sites with BASTION_BANCO_SITIOS (Places ids, «plaza,police»).
        /// </summary>
        [BenchFact]
        public void R1TheNewMaps()
        {
            var only = Environment.GetEnvironmentVariable("BASTION_BANCO_SITIOS")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var sites = only != null && only.Length > 0 ? Sites.Where((x) => only.Contains(x)).ToArray() : Sites;
            var jobs = (from site in sites from kind in Kinds where Drawn(site, kind) from rifles in new[] { true, false } from seed in Seeds select (site, kind, rifles, seed)).ToList();
            var clock = Stopwatch.StartNew();
            var fights = new Fight[jobs.Count];
            Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i) => fights[i] = Play(jobs[i].site, jobs[i].kind, jobs[i].rifles, jobs[i].seed));
            var lines = new List<string> { $"# R1: {fights.Length} combates en {clock.Elapsed.TotalMinutes:0.0} min ({fights.Sum((f) => f.Seconds) / fights.Length:0.0} s cada uno)", "" };
            foreach (var rifles in new[] { true, false })
            {
                lines.Add($"## Con {(rifles ? "fusiles (dificultad 2)" : "cuchillos (dificultad 1)")}: ganadas de {Seeds.Length}");
                lines.Add("");
                lines.Add("| Sitio | " + string.Join(" | ", Kinds.Select((k) => KindNames[k])) + " |");
                lines.Add("|---|" + string.Concat(Kinds.Select((_) => "---|")));
                foreach (var site in sites)
                {
                    var cells = Kinds.Select((k) =>
                    {
                        if (!Drawn(site, k)) return "–";
                        var mine = fights.Where((f) => f.Site == site && f.Kind == k && f.Rifles == rifles).ToList();
                        var won = mine.Count((f) => f.Outcome == Outcomes.Victory);
                        var job = k == NodeKinds.Elite ? $" ({mine.Count((f) => f.Done)})" : "";
                        var stuck = mine.Count((f) => f.Stuck);
                        return $"{won}{job}" + (stuck > 0 ? $" ⚠{stuck}" : "");
                    });
                    lines.Add($"| {SiteName(site)} | " + string.Join(" | ", cells) + " |");
                }
                lines.Add("");
            }
            lines.Add("Guarida: entre paréntesis, la caja abierta. ⚠: combates que el bot no acabó. *Sigilo: el bot lo juega como una Recuperación, sin esconderse: no es una medida del equilibrio del Sigilo.");
            lines.Add("");
            lines.Add("## Saqueo: lo traído por combate (media de las 6)");
            lines.Add("");
            lines.Add("| Sitio | Fusiles: § | objetos | caídos | Cuchillos: § | objetos | caídos |");
            lines.Add("|---|---|---|---|---|---|---|");
            foreach (var site in sites)
            {
                string Of(bool rifles)
                {
                    var mine = fights.Where((f) => f.Site == site && f.Kind == NodeKinds.Normal && f.Rifles == rifles).ToList();
                    return $"{mine.Average((f) => f.Supplies):0} | {mine.Average((f) => f.Found):0.0} | {mine.Average((f) => f.Fallen):0.0}";
                }
                lines.Add($"| {SiteName(site)} | {Of(true)} | {Of(false)} |");
            }
            lines.Add("");
            lines.Add("## Cada combate");
            lines.Add("");
            foreach (var f in fights)
                lines.Add($"{SiteName(f.Site)} · {KindNames[f.Kind]} · {(f.Rifles ? "fusiles" : "cuchillos")} · {f.Seed} ({f.Map}): {(f.Stuck ? "SIN ACABAR" : $"{f.Outcome}/{f.Reason}")}, turno {f.Turn}, objetivo {(f.Done ? "sí" : "no")}"
                    + $", {f.Fallen} caídos, {f.Found} objetos, {f.Supplies} §, premio {f.Reward}{(f.Unseen != null ? f.Unseen.Value ? " sin ser vistos" : " vistos" : "")}, {f.Orders} órdenes, {f.Refused} rechazadas, {f.Seconds:0.0} s");
            File.WriteAllLines(Path.Combine(BenchFactAttribute.Folder!, "r1.md"), lines);
            foreach (var l in lines.Take(40)) output.WriteLine(l);
            Assert.Equal(0, fights.Sum((f) => f.Refused));
        }

        // ------------------------------------------------------------------ R2

        /// <summary>An expedition the squad ended on the map, going back (HeardGame.Homeward).</summary>
        private const string Home = "home";

        /// <summary>Most expeditions a chapter is played for: El Toro not dead by then, it says so.</summary>
        private const int MostExpeditions = 15;

        /// <summary>A fight of an expedition, as its report tells it.</summary>
        private sealed class Leg
        {
            public string Name = "", Kind = "", Reason = "";
            public string? Ended;
            public int Row, Fallen, Supplies, Reward;
            public bool Victory, Matched;
        }

        private sealed class Expedition
        {
            public int Index, Soldiers, Squad, Before, After, Row;
            public string Upgrades = "", Arms = "";
            public string? Ended;
            public readonly List<Leg> Legs = new List<Leg>();
        }

        private sealed class Chapter
        {
            public int Seed;
            public readonly List<Expedition> Expeditions = new List<Expedition>();
            public int? Hundred, Toro;
            public string? Stuck;
            public double Seconds;
            public int Missions;
        }

        private static int RowOf(string? node) => node != null && node.StartsWith("n") && int.TryParse(node.Substring(1).Split('-')[0], out var r) ? r : 0;

        /// <summary>Every § the squad has on the base: the stash's and the living soldiers' packs'.</summary>
        private static int AllSupplies(CampaignState c) => Runs.StashSupplies(c) + c.Soldiers.Where((s) => s.Alive && s.Kit != null).Sum((s) => Gears.Supplies(s.Kit!.Pack));

        private static bool Fires(Gear? g) => g != null && Content.GearWeapon(g) is FoundWeaponDef w && w.Slot != FoundWeaponSlots.Melee;

        /// <summary>The squad going out, by what it fires: «2/1/1», with a long gun / a pistol only / neither.</summary>
        private static string ArmsOf(CampaignState c)
        {
            var squad = c.Run!.Squad.Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)).Where((s) => s?.Kit != null).Select((s) => s!.Kit!).ToList();
            var longs = squad.Count((k) => Fires(k.Primary));
            var pistols = squad.Count((k) => !Fires(k.Primary) && Fires(k.Pistol));
            return $"{longs}/{pistols}/{squad.Count - longs - pistols}";
        }

        private static string UpgradesOf(CampaignState c) => c.Upgrades == null ? "" : string.Join(" ", c.Upgrades.Where((u) => u.Value > 0).OrderBy((u) => u.Key, StringComparer.Ordinal).Select((u) => $"{u.Key}{u.Value}"));

        /// <summary>
        /// One chapter as the test apps play it (CampaignAutoRun: two players through a real room, HeardGame on the base and
        /// the map, MissionBot in the fights), from a new campaign with the room's Rng at `seed`, expedition after expedition
        /// till El Toro is dead (CampaignState.chaptersDone) or MostExpeditions are over.
        /// </summary>
        private static Chapter PlayChapter(int seed)
        {
            var clock = Stopwatch.StartNew();
            var ch = new Chapter { Seed = seed };
            Expedition? now = null;
            var played = CampaignAutoRun.Run(seed, 1000,
                watch: (c) =>
                {
                    if (c.Run != null)
                    {
                        if (now == null)
                        {
                            now = new Expedition { Index = ch.Expeditions.Count + 1, Before = AllSupplies(c), Upgrades = UpgradesOf(c), Squad = c.Run.Squad.Count, Soldiers = c.Soldiers.Count((s) => s.Alive), Arms = ArmsOf(c) };
                            ch.Expeditions.Add(now);
                        }
                        now.Row = Math.Max(now.Row, RowOf(c.Run.At));
                        return;
                    }
                    if (now != null)
                    {
                        // Back by choice, unless the fight being folded now says it ended it (ended, below, in this same step).
                        now.After = AllSupplies(c);
                        now.Ended = Home;
                        now = null;
                    }
                    if (ch.Hundred == null && AllSupplies(c) >= 100) ch.Hundred = ch.Expeditions.Count;
                    if (ch.Toro == null && (c.ChaptersDone ?? 0) >= 1) ch.Toro = ch.Expeditions.Count;
                },
                ended: (m, c) =>
                {
                    var report = c.LastReport;
                    var run = report?.Run;
                    var leg = new Leg
                    {
                        Name = m.Name, Matched = run != null && run.Node == m.Node, Kind = run?.NodeKind ?? "", Row = RowOf(m.Node),
                        Victory = report?.Victory == true, Reason = report?.Reason ?? "", Ended = run?.Ended,
                        Fallen = run?.Fallen.Count ?? 0, Supplies = run?.Found.Where((g) => g.Kind == GearKinds.Supplies).Sum((g) => g.Count) ?? 0,
                        Reward = run?.Reward ?? 0,
                    };
                    var e = now ?? ch.Expeditions.LastOrDefault();
                    e?.Legs.Add(leg);
                    if (e != null && leg.Ended != null) e.Ended = leg.Ended;
                },
                done: (c) => (c.ChaptersDone ?? 0) >= 1 || c.Run == null && ch.Expeditions.Count >= MostExpeditions,
                steps: 60_000);
            ch.Missions = played.Count;
            ch.Stuck = CampaignAutoRun.Stuck;
            if (CampaignAutoRun.StuckMission != null) File.WriteAllText(Path.Combine(BenchFactAttribute.Folder!, $"atasco-{seed}.json"), CampaignAutoRun.StuckMission);
            ch.Seconds = clock.Elapsed.TotalSeconds;
            return ch;
        }

        /// <summary>R2: the chapter whole with the bots, from the base to El Toro, on BASTION_BANCO_SEMILLAS seeds (default 20).</summary>
        [BenchFact]
        public void R2TheWholeChapter()
        {
            var count = int.TryParse(Environment.GetEnvironmentVariable("BASTION_BANCO_SEMILLAS"), out var n) ? n : 20;
            var clock = Stopwatch.StartNew();
            var chapters = new Chapter[count];
            Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i) => chapters[i] = PlayChapter(i + 1));
            string Exp(int? x) => x == null ? $"no en {MostExpeditions}" : $"{x}";
            var lines = new List<string>
            {
                $"# R2: {count} capítulos, {chapters.Sum((c) => c.Missions)} combates, en {clock.Elapsed.TotalMinutes:0.0} min",
                "",
                "## Por capítulo (semilla)",
                "",
                "| Semilla | Expediciones | Hasta 100 § | Hasta el Toro | Primera con armas largas para todos | Mejoras (en la expedición) | Combates | Atascada |",
                "|---|---|---|---|---|---|---|---|",
            };
            foreach (var ch in chapters)
            {
                var bought = new List<string>();
                for (var i = 1; i < ch.Expeditions.Count; i++)
                {
                    var was = ch.Expeditions[i - 1].Upgrades.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var u in ch.Expeditions[i].Upgrades.Split(' ', StringSplitOptions.RemoveEmptyEntries).Except(was)) bought.Add($"{u} ({i + 1})");
                }
                var armed = ch.Expeditions.FirstOrDefault((e) => e.Arms.Split('/')[0] == $"{e.Squad}" && e.Squad > 0);
                lines.Add($"| {ch.Seed} | {ch.Expeditions.Count} | {Exp(ch.Hundred)} | {Exp(ch.Toro)} | {(armed != null ? $"{armed.Index}" : "nunca")} | {string.Join(", ", bought)} | {ch.Missions} | {ch.Stuck ?? ""} |");
            }
            lines.Add("");
            lines.Add("Hasta 100 § y hasta el Toro: cuántas expediciones acabadas. Las mejoras, con la expedición que salió ya con ellas. Armas: con arma larga / solo pistola / solo cuchillo.");
            lines.Add("");
            var all = chapters.SelectMany((c) => c.Expeditions).Where((e) => e.Ended != null).ToList();
            lines.Add("## Por expedición (todas las semillas)");
            lines.Add("");
            lines.Add("| Expedición | Cuántas | § netos (media) | Fila alcanzada (media) | Vuelven por su cuenta | Matan al Toro | Vuelven sin matarlo | Caen todos | Combates (media) | Caídos (media) |");
            lines.Add("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var g in all.GroupBy((e) => Math.Min(e.Index, 6)).OrderBy((g) => g.Key))
                lines.Add($"| {(g.Key < 6 ? $"{g.Key}" : "6 o más")} | {g.Count()} | {g.Average((e) => e.After - e.Before):0} | {g.Average((e) => e.Row):0.0} | {g.Count((e) => e.Ended == Home)} | {g.Count((e) => e.Ended == RunEnds.Boss)} | {g.Count((e) => e.Ended == RunEnds.Returned)} | {g.Count((e) => e.Ended == RunEnds.Wiped)} | {g.Average((e) => e.Legs.Count):0.0} | {g.Average((e) => e.Legs.Sum((l) => l.Fallen)):0.0} |");
            lines.Add("");
            var legs = all.SelectMany((e) => e.Legs).ToList();
            lines.Add("## Dónde se pierde");
            lines.Add("");
            lines.Add("| Fila | Combates | Ganados | Caídos | Expediciones que acaban ahí sin nadie |");
            lines.Add("|---|---|---|---|---|");
            foreach (var g in legs.GroupBy((l) => l.Row).OrderBy((g) => g.Key))
                lines.Add($"| {g.Key} | {g.Count()} | {g.Count((l) => l.Victory)} | {g.Sum((l) => l.Fallen)} | {g.Count((l) => l.Ended == RunEnds.Wiped)} |");
            lines.Add("");
            lines.Add("*Sigilo: jugado sin esconderse, como una Recuperación; no mide su equilibrio.");
            lines.Add("");
            lines.Add("| Etapa | Combates | Ganados | Caídos | Sin nadie | § hallados (media) |");
            lines.Add("|---|---|---|---|---|---|");
            foreach (var g in legs.GroupBy((l) => l.Kind).OrderByDescending((g) => g.Count()))
                lines.Add($"| {(KindNames.TryGetValue(g.Key, out var k) ? k : g.Key == NodeKinds.Boss ? "El Toro" : g.Key)} | {g.Count()} | {g.Count((l) => l.Victory)} | {g.Sum((l) => l.Fallen)} | {g.Count((l) => l.Ended == RunEnds.Wiped)} | {g.Average((l) => l.Supplies + l.Reward):0} |");
            lines.Add("");
            lines.Add("## Cada expedición");
            lines.Add("");
            foreach (var ch in chapters)
                foreach (var e in ch.Expeditions)
                    lines.Add($"{ch.Seed}·{e.Index}: {e.Squad} de {e.Soldiers} vivos, armas {e.Arms}, mejoras «{e.Upgrades}», § {e.Before} → {e.After}, fila {e.Row}, acaba {e.Ended ?? "-"}: "
                        + string.Join(" / ", e.Legs.Select((l) => $"{l.Name} f{l.Row} {(l.Victory ? "gana" : "pierde")} ({l.Reason}) {l.Fallen} caídos {l.Supplies}+{l.Reward} §{(l.Matched ? "" : " ¿?")}")));
            File.WriteAllLines(Path.Combine(BenchFactAttribute.Folder!, "r2.md"), lines);
            foreach (var l in lines.Take(30)) output.WriteLine(l);
            Assert.All(chapters, (c) => Assert.Null(c.Stuck));
        }
    }
}
