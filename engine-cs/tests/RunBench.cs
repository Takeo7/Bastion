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
            NodeKinds.Elimination, NodeKinds.Recovery, NodeKinds.Rescue, NodeKinds.Key, NodeKinds.Bitten, NodeKinds.Wounded,
        };

        /// <summary>
        /// What a site can be (RunMap.SpecialFor, Generate): a Sabotaje only where an alarm is, a Travesía never in one building,
        /// La llave only at a police station.
        /// </summary>
        private static bool Drawn(string site, string kind)
        {
            var inside = site == Places.Offices || site == Places.Shed || site == Places.Police || site == Places.Hospital;
            if (kind == NodeKinds.Crossing) return !inside;
            if (kind == NodeKinds.Sabotage) return site == Places.Plaza || site == Places.Camping || site == Places.Factory;
            if (kind == NodeKinds.Key) return site == Places.Police;
            if (kind == NodeKinds.Bitten) return site == Places.Hospital;
            return true;
        }

        /// <summary>R1's seeds: 1 to BASTION_BANCO_SEMILLAS (default Seeds), and its stages BASTION_BANCO_ETAPAS (NodeKinds ids, «normal,key»).</summary>
        private static int[] R1Seeds => int.TryParse(Environment.GetEnvironmentVariable("BASTION_BANCO_SEMILLAS"), out var n) ? Enumerable.Range(1, n).ToArray() : Seeds;

        private sealed class Fight
        {
            public string Site = "", Kind = "", Map = "", Outcome = "", Reason = "";
            public bool Rifles, Done, Stuck;
            public int Seed, Turn, Fallen, Found, Supplies, Reward, Orders, Refused;
            public bool? Unseen;
            public double Seconds;
            /// <summary>What it found at the G19 probe's prices, and La llave's armoury (RunBench.Seen).</summary>
            public Leg Leg = new Leg();
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
            var lost = false;
            var bite = new Leg();
            string? lastBack = null;
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
                    if (Looting.KeyLost(s)) lost = true;
                    if (s.Mission.Bitten == true)
                    {
                        if (bite.FoundTurn == null && s.Mission.BiteTurns != null) bite.FoundTurn = s.Turn;
                        if (bite.AntiviralTurn == null && s.Mission.Antiviral == null) bite.AntiviralTurn = s.Turn;
                        if (bite.CuredTurn == null && s.Mission.Cured == true) bite.CuredTurn = s.Turn;
                        if (Looting.AntiviralLost(s)) bite.AntiviralLost = true;
                    }
                    if (Rules.WoundedOf(s) is Unit hurt)
                    {
                        if (bite.FoundTurn == null && s.Mission.SurvivorFound == true) bite.FoundTurn = s.Turn;
                        if (hurt.CarriedBy != null)
                        {
                            bite.CarriedTurn ??= s.Turn;
                            if (lastBack != null && lastBack != hurt.CarriedBy) bite.Relays++;
                            lastBack = hurt.CarriedBy;
                        }
                        if (bite.AboardTurn == null && hurt.Aboard == true) bite.AboardTurn = s.Turn;
                        if (bite.DeadTurn == null && !hurt.Alive && hurt.CarriedBy == null && !hurt.Evacuated) bite.DeadTurn = s.Turn;
                    }
                }
                idle = any ? 0 : idle + 1;
            }
            var f = new Fight
            {
                Site = site, Kind = kind, Rifles = rifles, Seed = seed, Map = s.MapName ?? "", Turn = s.Turn,
                Done = s.Mission.ObjectiveDone, Orders = orders, Refused = refused, Stuck = s.Outcome == null, Leg = Seen(s),
            };
            f.Leg.KeyLost = lost;
            f.Leg.FoundTurn = bite.FoundTurn;
            f.Leg.AntiviralTurn = bite.AntiviralTurn;
            f.Leg.CuredTurn = bite.CuredTurn;
            f.Leg.AntiviralLost = bite.AntiviralLost;
            if (Rules.WoundedOf(s) is Unit w)
            {
                f.Leg.FoundTurn = bite.FoundTurn;
                f.Leg.CarriedTurn = bite.CarriedTurn;
                f.Leg.AboardTurn = bite.AboardTurn;
                f.Leg.Relays = bite.Relays;
                f.Leg.DeadTurn = bite.DeadTurn;
                f.Leg.WoundedDead = !w.Alive && w.CarriedBy == null && !w.Evacuated;
            }
            (f.Leg.Turned, f.Leg.Killed, f.Leg.Left) = HowItEnded(s);
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
                f.Leg.Worth = Worth(report.Found);
            }
            f.Seconds = clock.Elapsed.TotalSeconds;
            return f;
        }

        private static readonly Dictionary<string, string> KindNames = new Dictionary<string, string>
        {
            [NodeKinds.Normal] = "Saqueo", [NodeKinds.Hold] = "Asedio", [NodeKinds.Crossing] = "Travesía", [NodeKinds.Stealth] = "Sigilo*",
            [NodeKinds.Elite] = "Guarida", [NodeKinds.Sabotage] = "Sabotaje", [NodeKinds.Elimination] = "Limpieza",
            [NodeKinds.Recovery] = "Recuperación", [NodeKinds.Rescue] = "Rescate", [NodeKinds.Key] = "La llave", [NodeKinds.Bitten] = "Mordido",
            [NodeKinds.Wounded] = "El herido", [NodeKinds.Split] = "Separados", [NodeKinds.Raiders] = "Saqueadores",
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
            var stages = Environment.GetEnvironmentVariable("BASTION_BANCO_ETAPAS")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var kinds = stages != null && stages.Length > 0 ? Kinds.Where((x) => stages.Contains(x)).ToArray() : Kinds;
            var seeds = R1Seeds;
            var jobs = (from site in sites from kind in kinds where Drawn(site, kind) from rifles in new[] { true, false } from seed in seeds select (site, kind, rifles, seed)).ToList();
            var clock = Stopwatch.StartNew();
            var fights = new Fight[jobs.Count];
            Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i) => fights[i] = Play(jobs[i].site, jobs[i].kind, jobs[i].rifles, jobs[i].seed));
            var lines = new List<string> { $"# R1: {fights.Length} combates en {clock.Elapsed.TotalMinutes:0.0} min ({fights.Sum((f) => f.Seconds) / fights.Length:0.0} s cada uno)", "" };
            foreach (var rifles in new[] { true, false })
            {
                lines.Add($"## Con {(rifles ? "fusiles (dificultad 2)" : "cuchillos (dificultad 1)")}: ganadas de {seeds.Length}");
                lines.Add("");
                lines.Add("| Sitio | " + string.Join(" | ", kinds.Select((k) => KindNames[k])) + " |");
                lines.Add("|---|" + string.Concat(kinds.Select((_) => "---|")));
                foreach (var site in sites)
                {
                    var cells = kinds.Select((k) =>
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
            if (kinds.Contains(NodeKinds.Normal))
            {
                lines.Add($"## Saqueo: lo traído por combate (media de las {seeds.Length})");
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
            }
            // G23.2 Mordido (Producto, 19/11): found, the antiviral, cured, turned, won; the turns between.
            if (kinds.Contains(NodeKinds.Bitten) && sites.Contains(Places.Hospital))
            {
                lines.Add($"## Mordido en el hospital (media de las {seeds.Length}; cuenta de {Setup.BiteTurns} turnos)");
                lines.Add("");
                lines.Add("| | ganados | caídos (media) | encontrado | antiviral sacado | curado | convertido (la cuenta a 0) | muerto de otro modo | en tierra al salir La Lata | turno en que se le encuentra (media) | turnos de encontrarle a tener el antiviral (media) | antiviral perdido |");
                lines.Add("|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var rifles in new[] { true, false })
                {
                    var mine = fights.Where((f) => f.Site == Places.Hospital && f.Kind == NodeKinds.Bitten && f.Rifles == rifles).ToList();
                    if (mine.Count == 0) continue;
                    var found = mine.Where((f) => f.Leg.FoundTurn != null).ToList();
                    var both = found.Where((f) => f.Leg.AntiviralTurn != null).ToList();
                    string Avg(IEnumerable<double> v) => v.Any() ? $"{v.Average():0.0}" : "–";
                    lines.Add($"| {(rifles ? "fusiles" : "cuchillos")} | {mine.Count((f) => f.Outcome == Outcomes.Victory)} de {mine.Count} | {mine.Average((f) => f.Fallen):0.0} | {found.Count} | {mine.Count((f) => f.Leg.AntiviralTurn != null)} | {mine.Count((f) => f.Leg.CuredTurn != null)} | {mine.Count((f) => f.Leg.Turned)} | {mine.Count((f) => f.Leg.Killed)} | {mine.Count((f) => f.Leg.Left)} | {Avg(found.Select((f) => (double)f.Leg.FoundTurn!.Value))} | {Avg(both.Select((f) => (double)Math.Max(0, f.Leg.AntiviralTurn!.Value - f.Leg.FoundTurn!.Value)))} | {mine.Count((f) => f.Leg.AntiviralLost)} |");
                }
                lines.Add("");
            }
            // G23.3 El herido against the Rescate at the same sites (Producto, 19/11: the distribution, not only the mean).
            if (kinds.Contains(NodeKinds.Wounded))
            {
                string Spread(IEnumerable<int> xs)
                {
                    var v = xs.OrderBy((x) => x).ToList();
                    return v.Count == 0 ? "–" : $"mín {v[0]} · mediana {v[v.Count / 2]} · máx {v[^1]} ({v.Count})";
                }
                lines.Add($"## El herido (de {seeds.Length} por sitio y arma)");
                lines.Add("");
                lines.Add("| sitio | armas | ganados (Rescate) | lo encuentran | lo cargan | dentro de La Lata | muere | muere sin haberlo cargado nadie | caen todos | relevos (total) |");
                lines.Add("|---|---|---|---|---|---|---|---|---|---|");
                foreach (var site in sites)
                    foreach (var rifles in new[] { true, false })
                    {
                        var mine = fights.Where((f) => f.Site == site && f.Kind == NodeKinds.Wounded && f.Rifles == rifles).ToList();
                        if (mine.Count == 0) continue;
                        var rescue = fights.Where((f) => f.Site == site && f.Kind == NodeKinds.Rescue && f.Rifles == rifles).ToList();
                        lines.Add($"| {SiteName(site)} | {(rifles ? "fusiles" : "cuchillos")} | {mine.Count((f) => f.Outcome == Outcomes.Victory)}{(rescue.Count > 0 ? $" ({rescue.Count((f) => f.Outcome == Outcomes.Victory)})" : "")} | {mine.Count((f) => f.Leg.FoundTurn != null)} | {mine.Count((f) => f.Leg.CarriedTurn != null)} | {mine.Count((f) => f.Leg.AboardTurn != null)} | {mine.Count((f) => f.Leg.WoundedDead)} | {mine.Count((f) => f.Leg.WoundedDead && f.Leg.CarriedTurn == null)} | {mine.Count((f) => f.Reason == EndReasons.Wiped)} | {mine.Sum((f) => f.Leg.Relays)} |");
                    }
                lines.Add("");
                foreach (var rifles in new[] { true, false })
                {
                    var all = fights.Where((f) => f.Kind == NodeKinds.Wounded && f.Rifles == rifles).ToList();
                    if (all.Count == 0) continue;
                    var rescue = fights.Where((f) => f.Kind == NodeKinds.Rescue && f.Rifles == rifles).ToList();
                    lines.Add($"**{(rifles ? "Fusiles" : "Cuchillos")}, todos los sitios:** ganados {all.Count((f) => f.Outcome == Outcomes.Victory)} de {all.Count}{(rescue.Count > 0 ? $" (el Rescate, {rescue.Count((f) => f.Outcome == Outcomes.Victory)} de {rescue.Count})" : "")}; caídos {all.Average((f) => f.Fallen):0.0} por combate{(rescue.Count > 0 ? $" (el Rescate, {rescue.Average((f) => f.Fallen):0.0})" : "")}.");
                    lines.Add($"- Turno en que lo encuentran: {Spread(all.Where((f) => f.Leg.FoundTurn != null).Select((f) => f.Leg.FoundTurn!.Value))}.");
                    lines.Add($"- De encontrarlo a cargarlo: {Spread(all.Where((f) => f.Leg.FoundTurn != null && f.Leg.CarriedTurn != null).Select((f) => f.Leg.CarriedTurn!.Value - f.Leg.FoundTurn!.Value))} turnos.");
                    lines.Add($"- De cargarlo a tenerlo dentro de La Lata: {Spread(all.Where((f) => f.Leg.CarriedTurn != null && f.Leg.AboardTurn != null).Select((f) => f.Leg.AboardTurn!.Value - f.Leg.CarriedTurn!.Value))} turnos.");
                    lines.Add($"- Turno en que acaba: ganados {Spread(all.Where((f) => f.Outcome == Outcomes.Victory).Select((f) => f.Turn))}; perdidos {Spread(all.Where((f) => f.Outcome != Outcomes.Victory).Select((f) => f.Turn))}.");
                    lines.Add($"- Muere: {all.Count((f) => f.Leg.WoundedDead)} de {all.Count}; sin haberlo cargado nadie, {all.Count((f) => f.Leg.WoundedDead && f.Leg.CarriedTurn == null)}. De encontrarlo a morir sin cargarlo: {Spread(all.Where((f) => f.Leg.WoundedDead && f.Leg.CarriedTurn == null && f.Leg.FoundTurn != null && f.Leg.DeadTurn != null).Select((f) => f.Leg.DeadTurn!.Value - f.Leg.FoundTurn!.Value))} turnos.");
                    lines.Add($"- Relevos por combate: {Spread(all.Select((f) => f.Leg.Relays))}. Combates atascados: {all.Count((f) => f.Stuck)}.");
                    lines.Add("");
                }
            }
            // G23.1 La llave against a Saqueo at the same police stations: its armoury opened, what each found.
            if (kinds.Contains(NodeKinds.Key) && sites.Contains(Places.Police))
            {
                lines.Add($"## La llave en la comisaría (media de las {seeds.Length})");
                lines.Add("");
                lines.Add("| | ganados | caídos (media) | llave en una mochila o usada | armería abierta | muebles de la armería registrados (media) | lo hallado, § (media) | llave perdida |");
                lines.Add("|---|---|---|---|---|---|---|---|");
                foreach (var rifles in new[] { true, false })
                    foreach (var kind in new[] { NodeKinds.Key, NodeKinds.Normal }.Where((k) => kinds.Contains(k)))
                    {
                        var mine = fights.Where((f) => f.Site == Places.Police && f.Kind == kind && f.Rifles == rifles).ToList();
                        if (mine.Count == 0) continue;
                        var key = kind == NodeKinds.Key;
                        lines.Add($"| {KindNames[kind]}, {(rifles ? "fusiles" : "cuchillos")} | {mine.Count((f) => f.Outcome == Outcomes.Victory)} de {mine.Count} | {mine.Average((f) => f.Fallen):0.0} | "
                            + (key ? $"{mine.Count((f) => f.Leg.KeyAt == KeyPlaces.Pack || f.Leg.KeyAt == KeyPlaces.Used)} | {mine.Count((f) => f.Leg.ArmoryOpen)} | {mine.Average((f) => f.Leg.ArmorySearched):0.0} de {mine.Average((f) => f.Leg.ArmoryPieces):0.0} | " : "– | – | – | ")
                            + $"{mine.Average((f) => f.Leg.Worth):0} | {(key ? $"{mine.Count((f) => f.Leg.KeyLost)}" : "–")} |");
                    }
                lines.Add("");
                lines.Add("Lo hallado, a los precios de la sonda de G19 (un fusil de asalto 90 §, una escopeta 80 §, un cargador 15 §…).");
                lines.Add("");
            }
            lines.Add("## Cada combate");
            lines.Add("");
            foreach (var f in fights)
                lines.Add($"{SiteName(f.Site)} · {KindNames[f.Kind]} · {(f.Rifles ? "fusiles" : "cuchillos")} · {f.Seed} ({f.Map}): {(f.Stuck ? "SIN ACABAR" : $"{f.Outcome}/{f.Reason}")}, turno {f.Turn}, objetivo {(f.Done ? "sí" : "no")}"
                    + $", {f.Fallen} caídos, {f.Found} objetos ({f.Leg.Worth} §), {f.Supplies} §, premio {f.Reward}{(f.Unseen != null ? f.Unseen.Value ? " sin ser vistos" : " vistos" : "")}"
                    + (f.Leg.KeyAt != null ? $", llave {f.Leg.KeyAt}{(f.Leg.KeyLost ? " PERDIDA" : "")}, armería {(f.Leg.ArmoryOpen ? "abierta" : "cerrada")} {f.Leg.ArmorySearched}/{f.Leg.ArmoryPieces}" : "")
                    + (f.Kind == NodeKinds.Bitten ? $", encontrado t{f.Leg.FoundTurn?.ToString() ?? "-"}, antiviral t{f.Leg.AntiviralTurn?.ToString() ?? "-"}, curado t{f.Leg.CuredTurn?.ToString() ?? "-"}{(f.Leg.Turned ? ", CONVERTIDO" : "")}{(f.Leg.Killed ? ", MUERTO" : "")}{(f.Leg.Left ? ", EN TIERRA" : "")}{(f.Leg.AntiviralLost ? ", ANTIVIRAL PERDIDO" : "")}" : "")
                    + (f.Kind == NodeKinds.Wounded ? $", encontrado t{f.Leg.FoundTurn?.ToString() ?? "-"}, cargado t{f.Leg.CarriedTurn?.ToString() ?? "-"}, dentro t{f.Leg.AboardTurn?.ToString() ?? "-"}, {f.Leg.Relays} relevos{(f.Leg.WoundedDead ? ", MUERE" : "")}" : "") + $", {f.Orders} órdenes, {f.Refused} rechazadas, {f.Seconds:0.0} s");
            File.WriteAllLines(Path.Combine(BenchFactAttribute.Folder!, "r1.md"), lines);
            foreach (var l in lines.Take(40)) output.WriteLine(l);
            Assert.Equal(0, fights.Sum((f) => f.Refused));
            Assert.DoesNotContain(fights, (f) => f.Leg.KeyLost || f.Leg.AntiviralLost);
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
            /// <summary>What it found, at the G19 probe's prices (.dev-snapshots/gameplay/muebles/ZzProbeLoot.cs.txt).</summary>
            public int Worth;
            /// <summary>At a police station; G23.1 La llave's: where its key ended, its armoury open, its pieces searched (of all).</summary>
            public bool Police;
            public string? KeyAt;
            public bool ArmoryOpen;
            /// <summary>Its key nowhere to be had at some point of the fight (Looting.KeyLost: Producto's invariant, 19/11).</summary>
            public bool KeyLost;
            public int ArmorySearched, ArmoryPieces;
            /// <summary>
            /// G23.2 Mordido's (Producto, 19/11): the turns the bitten was found, the antiviral taken out of its piece and the
            /// bitten cured (null: never), whether they turned, and whether the antiviral was ever nowhere (Looting.AntiviralLost).
            /// </summary>
            public int? FoundTurn, AntiviralTurn, CuredTurn;
            /// <summary>
            /// G23.3 El herido's (Producto, 19/11): the turns they were found, first carried and got into La Lata (null: never),
            /// how many times they changed backs (a pass, or set down and taken up by another), and whether they died.
            /// </summary>
            public int? CarriedTurn, AboardTurn, DeadTurn;
            public int Relays;
            public bool WoundedDead;
            /// <summary>
            /// Turned: the count out (Rules.Bite). Killed: dead some other way (a bite of the dead, a fall), cured or not. Left:
            /// alive on the ground when La Lata went (LeftBehindEvent: Unit.Lost).
            /// </summary>
            public bool Turned, Killed, Left, AntiviralLost;
            /// <summary>
            /// G23.4 Separados' (Producto, 20/11): its two groups' sizes (none: a Saqueo of the stage), and per group the turn its
            /// first got into La Lata and the turn it was all fallen (null: never).
            /// </summary>
            public int[] SplitSizes = new int[0];
            public int?[] SplitArrived = new int?[2], SplitFell = new int?[2];
            public bool SplitFellBefore(int g) => SplitFell[g] != null && SplitArrived[g] == null;
            /// <summary>
            /// G23.5 Saqueadores' (Producto, 21/11: on the maps with R2): whether its camp was made (else a Saqueo of the stage), its
            /// crates searched and how many while the camp slept, the turn it woke (null: never), its guards and those down.
            /// </summary>
            public bool Camp;
            public int Crates, Unseen, Guards, GuardsDown;
            public int? WokeTurn;
        }

        private sealed class Expedition
        {
            public int Index, Soldiers, Squad, Before, After, Row;
            public string Upgrades = "", Arms = "";
            public string? Ended;
            /// <summary>La llave's on its map, and in reach from where it went out (all of them: from row 1).</summary>
            public int Keys;
            /// <summary>G23.2 Mordido's on its map, likewise; G23.3 El herido's; G23.4 Separados'; G23.5 Saqueadores'.</summary>
            public int Bitten, Wounded, Split, Raiders;
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

        /// <summary>Saqueadores: its stash's crates searched (GameState.Searched, by node as the crates).</summary>
        private static int CratesSearched(GameState s) => s.Mission.Raiders == null ? 0 : s.Mission.Raiders.Crates.Count((i) => s.Searched != null && s.Searched.Contains(i));

        private static int RowOf(string? node) => node != null && node.StartsWith("n") && int.TryParse(node.Substring(1).Split('-')[0], out var r) ? r : 0;

        /// <summary>Every § the squad has on the base: the stash's and the living soldiers' packs'.</summary>
        private static int AllSupplies(CampaignState c) => Runs.StashSupplies(c) + c.Soldiers.Where((s) => s.Alive && s.Kit != null).Sum((s) => Gears.Supplies(s.Kit!.Pack));

        private static bool Fires(Gear? g) => g != null && Content.GearWeapon(g) is FoundWeaponDef w && w.Slot != FoundWeaponSlots.Melee;

        private static readonly Dictionary<string, int> WeaponPrice = new Dictionary<string, int>
        {
            [FoundWeaponIds.Pistol] = 40, [FoundWeaponIds.AssaultRifle] = 90, [FoundWeaponIds.PumpShotgun] = 80, [FoundWeaponIds.PrecisionRifle] = 120,
            [FoundWeaponIds.Stick] = 20, [FoundWeaponIds.Pan] = 20, [FoundWeaponIds.Bat] = 25, [FoundWeaponIds.Rake] = 25, [FoundWeaponIds.Crowbar] = 30,
            [FoundWeaponIds.Machete] = 30, [FoundWeaponIds.Longsword] = 35, [FoundWeaponIds.Katana] = 40,
        };

        /// <summary>What `found` is worth at the G19 probe's prices: a § a supply, a long gun 80–120…</summary>
        private static int Worth(IEnumerable<Gear> found) => found.Sum((g) =>
            g.Kind == GearKinds.Weapon ? WeaponPrice.TryGetValue(g.Type!, out var p) ? p : 80
            : g.Kind == GearKinds.Mags || g.Kind == GearKinds.Medicine ? 15 * g.Count
            : g.Kind == GearKinds.Vest ? g.Type == VestIds.HeavyVest ? 100 : 60
            : g.Kind == GearKinds.Supplies ? g.Count
            : g.Kind == GearKinds.Item ? (g.Type == ItemIds.FirstAid ? 25 : 15) * g.Count
            : g.Kind == GearKinds.Mod ? 30 * g.Count : 0);

        /// <summary>
        /// Mordido's bitten at the end: turned by the count (Rules.Bite: the count at 0, never cured), dead some other way (a
        /// squad's then, risen as one of its fallen: Reanimated too, so that alone doesn't say it), or left on the ground as
        /// La Lata went. Not Alive says none of it alone: in La Lata (Evacuated) and left (Lost) aren't either.
        /// </summary>
        private static (bool Turned, bool Killed, bool Left) HowItEnded(GameState s)
        {
            if (s.Mission.Bitten != true) return (false, false, false);
            var b = s.Units.FirstOrDefault((u) => u.Id == s.Mission.ObjectiveUnit);
            if (b == null || b.Alive || b.Evacuated) return (false, false, false);
            if (b.Lost == true) return (false, false, true);
            var turned = s.Mission.Cured != true && s.Mission.BiteTurns == 0;
            return (turned, !turned, false);
        }

        /// <summary>The fight once over, for its leg: at a police station, and La llave's armoury.</summary>
        private static Leg Seen(GameState s)
        {
            var leg = new Leg { Police = s.Interiors?.Any((x) => x.Kind == InteriorKinds.Police) == true };
            if (s.Mission.KeyArmory != true) return leg;
            leg.KeyAt = Looting.KeyWhere(s);
            leg.ArmoryOpen = Looting.ArmoryOpen(s);
            var station = s.Interiors!.First((x) => x.Kind == InteriorKinds.Police);
            var a = station.Rooms.First((r) => r.Use == PoliceUses.Armory && r.Z == 0);
            var pieces = station.Dressing.Where((d) => d.Z == 0 && d.X >= a.X && d.Y >= a.Y && d.X < a.X + a.W && d.Y < a.Y + a.H && SearchPieces.Police.Contains(d.Kind)).ToList();
            leg.ArmoryPieces = pieces.Count;
            // Searched, its tile in GameState.searched (a piece stays in MissionState.pieces; Red, 19/11).
            leg.ArmorySearched = pieces.Count((d) => Looting.WasSearched(s, new Pos { X = d.X, Y = d.Y, Z = 0 }));
            return leg;
        }

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
            // A chapter played past a squad fallen with nobody at the base: recruits as before N8 (Tuning.Wipes.One), not the
            // game lost, so that the chapters measure what they did.
            Runs.Tuning.Set(new Runs.Tuning.Settings { Wipe = Runs.Tuning.Wipes.One });
            var clock = Stopwatch.StartNew();
            var ch = new Chapter { Seed = seed };
            Expedition? now = null;
            Leg? seen = null;
            var lost = false;
            var bite = new Leg();
            string? back = null;
            var split = new Leg();
            var raid = new Leg();
            var played = CampaignAutoRun.Run(seed, 1000,
                watch: (c) =>
                {
                    if (c.Run != null)
                    {
                        if (now == null)
                        {
                            now = new Expedition { Index = ch.Expeditions.Count + 1, Before = AllSupplies(c), Upgrades = UpgradesOf(c), Squad = c.Run.Squad.Count, Soldiers = c.Soldiers.Count((s) => s.Alive), Arms = ArmsOf(c), Keys = c.Run.Nodes.Count((n) => n.Kind == NodeKinds.Key), Bitten = c.Run.Nodes.Count((n) => n.Kind == NodeKinds.Bitten), Wounded = c.Run.Nodes.Count((n) => n.Kind == NodeKinds.Wounded), Split = c.Run.Nodes.Count((n) => n.Kind == NodeKinds.Split), Raiders = c.Run.Nodes.Count((n) => n.Kind == NodeKinds.Raiders) };
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
                    var leg = seen ?? new Leg();
                    seen = null;
                    leg.Name = m.Name;
                    leg.Matched = run != null && run.Node == m.Node;
                    leg.Kind = run?.NodeKind ?? "";
                    leg.Row = RowOf(m.Node);
                    leg.Victory = report?.Victory == true;
                    leg.Reason = report?.Reason ?? "";
                    leg.Ended = run?.Ended;
                    leg.Fallen = run?.Fallen.Count ?? 0;
                    leg.Supplies = run?.Found.Where((g) => g.Kind == GearKinds.Supplies).Sum((g) => g.Count) ?? 0;
                    leg.Reward = run?.Reward ?? 0;
                    leg.Worth = run != null ? Worth(run.Found) : 0;
                    var e = now ?? ch.Expeditions.LastOrDefault();
                    e?.Legs.Add(leg);
                    if (e != null && leg.Ended != null) e.Ended = leg.Ended;
                },
                done: (c) => (c.ChaptersDone ?? 0) >= 1 || c.Run == null && ch.Expeditions.Count >= MostExpeditions,
                steps: 60_000,
                fought: (s) =>
                {
                    seen = Seen(s);
                    seen.KeyLost = lost;
                    lost = false;
                    seen.FoundTurn = bite.FoundTurn;
                    seen.AntiviralTurn = bite.AntiviralTurn;
                    seen.CuredTurn = bite.CuredTurn;
                    seen.AntiviralLost = bite.AntiviralLost;
                    (seen.Turned, seen.Killed, seen.Left) = HowItEnded(s);
                    if (Rules.WoundedOf(s) is Unit hw)
                    {
                        seen.CarriedTurn = bite.CarriedTurn;
                        seen.AboardTurn = bite.AboardTurn;
                        seen.Relays = bite.Relays;
                        seen.DeadTurn = bite.DeadTurn;
                        seen.WoundedDead = !hw.Alive && hw.CarriedBy == null && !hw.Evacuated;
                    }
                    if (s.Mission.Split != null)
                    {
                        seen.SplitSizes = s.Mission.Split.Select((g) => g.Count).ToArray();
                        seen.SplitArrived = split.SplitArrived;
                        seen.SplitFell = split.SplitFell;
                    }
                    if (s.Mission.Raiders != null)
                    {
                        seen.Camp = true;
                        seen.Crates = CratesSearched(s);
                        seen.Unseen = raid.Unseen;
                        seen.WokeTurn = raid.WokeTurn;
                        seen.Guards = s.Units.Count((u) => u.Guard != null);
                        seen.GuardsDown = s.Units.Count((u) => u.Guard != null && !u.Alive);
                    }
                    bite = new Leg();
                    back = null;
                    split = new Leg();
                    raid = new Leg();
                },
                playing: (s) =>
                {
                    lost |= Looting.KeyLost(s);
                    if (s.Mission.Raiders != null)
                    {
                        // A crate searched with the camp still asleep after it (a search that wakes it is a seen one).
                        var awake = s.Units.Any((u) => u.Guard != null && u.Alive && !Paths.OnPatrol(s, u));
                        var crates = CratesSearched(s);
                        if (crates > raid.Crates && !awake) raid.Unseen += crates - raid.Crates;
                        raid.Crates = crates;
                        if (awake) raid.WokeTurn ??= s.Turn;
                    }
                    if (s.Mission.Split != null)
                        for (var g = 0; g < s.Mission.Split.Count; g++)
                        {
                            var group = s.Mission.Split[g].Select((id) => Grid.GetUnit(s, id)).Where((u) => u != null).Select((u) => u!).ToList();
                            if (split.SplitArrived[g] == null && group.Any((u) => u.Evacuated)) split.SplitArrived[g] = s.Turn;
                            if (split.SplitFell[g] == null && !group.Any((u) => u.Alive || u.Evacuated)) split.SplitFell[g] = s.Turn;
                        }
                    if (Rules.WoundedOf(s) is Unit hurt)
                    {
                        if (bite.FoundTurn == null && s.Mission.SurvivorFound == true) bite.FoundTurn = s.Turn;
                        if (hurt.CarriedBy != null)
                        {
                            bite.CarriedTurn ??= s.Turn;
                            if (back != null && back != hurt.CarriedBy) bite.Relays++;
                            back = hurt.CarriedBy;
                        }
                        if (bite.AboardTurn == null && hurt.Aboard == true) bite.AboardTurn = s.Turn;
                        if (bite.DeadTurn == null && !hurt.Alive && hurt.CarriedBy == null && !hurt.Evacuated) bite.DeadTurn = s.Turn;
                        return;
                    }
                    if (s.Mission.Bitten != true) return;
                    if (bite.FoundTurn == null && s.Mission.BiteTurns != null) bite.FoundTurn = s.Turn;
                    if (bite.AntiviralTurn == null && s.Mission.Antiviral == null) bite.AntiviralTurn = s.Turn;
                    if (bite.CuredTurn == null && s.Mission.Cured == true) bite.CuredTurn = s.Turn;
                    if (Looting.AntiviralLost(s)) bite.AntiviralLost = true;
                });
            ch.Missions = played.Count;
            ch.Stuck = CampaignAutoRun.Stuck;
            if (CampaignAutoRun.StuckMission != null) File.WriteAllText(Path.Combine(BenchFactAttribute.Folder!, $"atasco-{seed}.json"), CampaignAutoRun.StuckMission);
            ch.Seconds = clock.Elapsed.TotalSeconds;
            Runs.Tuning.Reset();
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
            // N9 (Producto, 22/11): by the seats the squad went out with, for the levels' numbers.
            lines.Add("## Por plazas al salir");
            lines.Add("");
            lines.Add("| Escuadra | Expediciones | Vuelven (por su cuenta, Toro o volviendo) | Caen todos | Fila alcanzada (media) | Combates (media) | Caídos (media) | § netos (media) |");
            lines.Add("|---|---|---|---|---|---|---|---|");
            foreach (var g in all.GroupBy((e) => e.Squad).OrderBy((g) => g.Key))
                lines.Add($"| {g.Key} | {g.Count()} | {g.Count((e) => e.Ended != RunEnds.Wiped)} ({100.0 * g.Count((e) => e.Ended != RunEnds.Wiped) / g.Count():0} %) | {g.Count((e) => e.Ended == RunEnds.Wiped)} | {g.Average((e) => e.Row):0.0} | {g.Average((e) => e.Legs.Count):0.0} | {g.Average((e) => e.Legs.Sum((l) => l.Fallen)):0.0} | {g.Average((e) => e.After - e.Before):0} |");
            lines.Add("");
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
            // G23.1 La llave (Producto, 19/11): how often it is on the maps and played, its armoury opened, what it gives.
            var keys = legs.Where((l) => l.Kind == NodeKinds.Key).ToList();
            var police = legs.Where((l) => l.Kind == NodeKinds.Normal && l.Police).ToList();
            var expeditions = chapters.SelectMany((c) => c.Expeditions).ToList();
            lines.Add("## La llave");
            lines.Add("");
            lines.Add($"- En los mapas: {expeditions.Sum((e) => e.Keys)} en {expeditions.Count} expediciones ({expeditions.Sum((e) => e.Keys) / (double)Math.Max(1, expeditions.Count):0.00} por mapa; {expeditions.Count((e) => e.Keys > 0) / (double)Math.Max(1, expeditions.Count):P0} de los mapas con alguna); {expeditions.Sum((e) => e.Keys) / (double)count:0.0} por capítulo.");
            lines.Add($"- Jugadas: {keys.Count} en {count} capítulos ({keys.Count / (double)count:0.00} por capítulo), {keys.Count((l) => l.Victory)} ganadas, {keys.Sum((l) => l.Fallen)} caídos, {keys.Count((l) => l.Ended == RunEnds.Wiped)} sin nadie.");
            if (keys.Count > 0)
            {
                lines.Add($"- La llave al acabar: {string.Join(", ", keys.GroupBy((l) => l.KeyAt ?? "-").OrderByDescending((g) => g.Count()).Select((g) => $"{g.Key} {g.Count()}"))} (holder: en el muerto; floor: en el suelo; pack: en una mochila; used: abrió la puerta).");
                lines.Add($"- La llave perdida en algún momento (Looting.KeyLost, orden a orden): {keys.Count((l) => l.KeyLost)} de {keys.Count}.");
                lines.Add($"- Armería abierta: {keys.Count((l) => l.ArmoryOpen)} de {keys.Count}; registrada entera: {keys.Count((l) => l.ArmoryPieces > 0 && l.ArmorySearched == l.ArmoryPieces)}; muebles registrados (media, de los abiertos): {(keys.Any((l) => l.ArmoryOpen) ? keys.Where((l) => l.ArmoryOpen).Average((l) => l.ArmorySearched) : 0):0.0} de {(keys.Count > 0 ? keys.Average((l) => l.ArmoryPieces) : 0):0.0}.");
                lines.Add("");
                lines.Add("| | combates | ganados | caídos (media) | lo hallado, § (media) | § sueltos (media) |");
                lines.Add("|---|---|---|---|---|---|");
                void Row(string name, List<Leg> ls)
                {
                    if (ls.Count > 0) lines.Add($"| {name} | {ls.Count} | {ls.Count((l) => l.Victory)} | {ls.Average((l) => l.Fallen):0.0} | {ls.Average((l) => l.Worth):0} | {ls.Average((l) => l.Supplies + l.Reward):0} |");
                }
                Row("La llave, armería abierta", keys.Where((l) => l.ArmoryOpen).ToList());
                Row("La llave, cerrada", keys.Where((l) => !l.ArmoryOpen).ToList());
                Row("La llave, todas", keys);
                Row("Saqueo en la comisaría", police);
                Row("Saqueo en la comisaría, filas 3–5", police.Where((l) => l.Row >= RunMap.KeyFrom).ToList());
                lines.Add("");
                lines.Add("Lo hallado, a los precios de la sonda de G19 (un fusil de asalto 90 §, una escopeta 80 §, un cargador 15 §…).");
            }
            // G23.2 Mordido (Producto, 19/11: on the maps; the distribution, not only the mean).
            var bitten = legs.Where((l) => l.Kind == NodeKinds.Bitten).ToList();
            lines.Add("");
            lines.Add("## Mordido");
            lines.Add("");
            lines.Add($"- En los mapas: {expeditions.Sum((e) => e.Bitten)} en {expeditions.Count} expediciones ({expeditions.Sum((e) => e.Bitten) / (double)Math.Max(1, expeditions.Count):0.00} por mapa; {expeditions.Count((e) => e.Bitten > 0) / (double)Math.Max(1, expeditions.Count):P0} de los mapas con alguno); {expeditions.Sum((e) => e.Bitten) / (double)count:0.0} por capítulo.");
            lines.Add($"- Jugados: {bitten.Count} en {count} capítulos ({bitten.Count / (double)count:0.00} por capítulo; filas {string.Join(", ", bitten.GroupBy((l) => l.Row).OrderBy((g) => g.Key).Select((g) => $"{g.Key}: {g.Count()}"))}).");
            if (bitten.Count > 0)
            {
                string Spread(IEnumerable<int> xs)
                {
                    var v = xs.OrderBy((x) => x).ToList();
                    return v.Count == 0 ? "ninguno" : $"{v.Count}: mín {v[0]}, mediana {v[v.Count / 2]}, máx {v[^1]} ({string.Join(" ", v)})";
                }
                lines.Add($"- El mordido: curado y a bordo (ganada) {bitten.Count((l) => l.Victory && l.CuredTurn != null)}; curado y no llega {bitten.Count((l) => !l.Victory && l.CuredTurn != null)}; convertido (la cuenta a 0) {bitten.Count((l) => l.Turned)}; muerto de otro modo (mordido por los muertos…) {bitten.Count((l) => l.Killed)}, {bitten.Count((l) => l.Killed && l.CuredTurn != null)} de ellos ya curados; en tierra al salir La Lata {bitten.Count((l) => l.Left)}; sin encontrarlo {bitten.Count((l) => l.FoundTurn == null)}; encontrado y vivo al acabar (sin unirse, o al caer la escuadra) {bitten.Count((l) => l.FoundTurn != null && !l.Victory && !l.Turned && !l.Killed && !l.Left)}.");
                lines.Add($"- La escuadra: cae entera {bitten.Count((l) => l.Ended == RunEnds.Wiped)}; sale sin él {bitten.Count((l) => !l.Victory && l.Ended != RunEnds.Wiped)}.");
                lines.Add($"- Ganadas {bitten.Count((l) => l.Victory)} de {bitten.Count}; caídos {bitten.Sum((l) => l.Fallen)} ({bitten.Average((l) => l.Fallen):0.0} por combate); razones: {string.Join(", ", bitten.GroupBy((l) => l.Reason).OrderByDescending((g) => g.Count()).Select((g) => $"{g.Key} {g.Count()}"))}.");
                lines.Add($"- Turno en que lo encuentran: {Spread(bitten.Where((l) => l.FoundTurn != null).Select((l) => l.FoundTurn!.Value))}.");
                lines.Add($"- Turnos desde encontrarlo hasta sacar el antiviral (con antiviral): {Spread(bitten.Where((l) => l.FoundTurn != null && l.AntiviralTurn != null).Select((l) => l.AntiviralTurn!.Value - l.FoundTurn!.Value))}.");
                lines.Add($"- Turnos desde encontrarlo hasta curarlo (curados): {Spread(bitten.Where((l) => l.FoundTurn != null && l.CuredTurn != null).Select((l) => l.CuredTurn!.Value - l.FoundTurn!.Value))}.");
                lines.Add($"- Antiviral fuera del mueble antes de encontrarlo: {bitten.Count((l) => l.AntiviralTurn != null && (l.FoundTurn == null || l.AntiviralTurn < l.FoundTurn))}; perdido en algún momento (Looting.AntiviralLost): {bitten.Count((l) => l.AntiviralLost)}.");
                lines.Add("");
                lines.Add("| Mordido | combates | ganados | caídos (media) | § sueltos (media) |");
                lines.Add("|---|---|---|---|---|");
                foreach (var g in bitten.GroupBy((l) => l.Row <= 2 ? "fila 2" : l.Row <= 4 ? "filas 3–4" : "fila 5").OrderBy((g) => g.Key))
                    lines.Add($"| {g.Key} | {g.Count()} | {g.Count((l) => l.Victory)} | {g.Average((l) => l.Fallen):0.0} | {g.Average((l) => l.Supplies + l.Reward):0} |");
                var rescue = legs.Where((l) => l.Kind == NodeKinds.Rescue).ToList();
                if (rescue.Count > 0) lines.Add($"| Rescate (para comparar) | {rescue.Count} | {rescue.Count((l) => l.Victory)} | {rescue.Average((l) => l.Fallen):0.0} | {rescue.Average((l) => l.Supplies + l.Reward):0} |");
            }
            // G23.3 El herido (Producto, 19/11: the 0.13.5; by distribution, against the Rescate in the same bench).
            var hurtLegs = legs.Where((l) => l.Kind == NodeKinds.Wounded).ToList();
            lines.Add("");
            lines.Add("## El herido");
            lines.Add("");
            lines.Add($"- En los mapas: {expeditions.Sum((e) => e.Wounded)} en {expeditions.Count} expediciones ({expeditions.Sum((e) => e.Wounded) / (double)Math.Max(1, expeditions.Count):0.00} por mapa; {expeditions.Count((e) => e.Wounded > 0) / (double)Math.Max(1, expeditions.Count):P0} de los mapas con alguno); {expeditions.Sum((e) => e.Wounded) / (double)count:0.0} por capítulo.");
            lines.Add($"- Jugados: {hurtLegs.Count} en {count} capítulos ({hurtLegs.Count / (double)count:0.00} por capítulo; filas {string.Join(", ", hurtLegs.GroupBy((l) => l.Row).OrderBy((g) => g.Key).Select((g) => $"{g.Key}: {g.Count()}"))}).");
            if (hurtLegs.Count > 0)
            {
                string Spread2(IEnumerable<int> xs)
                {
                    var v = xs.OrderBy((x) => x).ToList();
                    return v.Count == 0 ? "ninguno" : $"{v.Count}: mín {v[0]}, mediana {v[v.Count / 2]}, máx {v[^1]} ({string.Join(" ", v)})";
                }
                lines.Add($"- El herido: dentro de La Lata (ganada) {hurtLegs.Count((l) => l.Victory)}; sin encontrarlo {hurtLegs.Count((l) => l.FoundTurn == null)}; encontrado y sin cargarlo nunca {hurtLegs.Count((l) => l.FoundTurn != null && l.CarriedTurn == null)}; muere {hurtLegs.Count((l) => l.WoundedDead)} ({hurtLegs.Count((l) => l.WoundedDead && l.CarriedTurn == null)} sin haberlo cargado nadie).");
                lines.Add($"- La escuadra: cae entera {hurtLegs.Count((l) => l.Ended == RunEnds.Wiped)}; sale sin él (muerto) {hurtLegs.Count((l) => !l.Victory && l.Ended != RunEnds.Wiped)}; caídos {hurtLegs.Sum((l) => l.Fallen)} ({hurtLegs.Average((l) => l.Fallen):0.0} por combate).");
                lines.Add($"- Turno en que lo encuentran: {Spread2(hurtLegs.Where((l) => l.FoundTurn != null).Select((l) => l.FoundTurn!.Value))}.");
                lines.Add($"- De encontrarlo a cargarlo: {Spread2(hurtLegs.Where((l) => l.FoundTurn != null && l.CarriedTurn != null).Select((l) => l.CarriedTurn!.Value - l.FoundTurn!.Value))} turnos.");
                lines.Add($"- De cargarlo a tenerlo dentro: {Spread2(hurtLegs.Where((l) => l.CarriedTurn != null && l.AboardTurn != null).Select((l) => l.AboardTurn!.Value - l.CarriedTurn!.Value))} turnos.");
                lines.Add($"- Relevos: {Spread2(hurtLegs.Select((l) => l.Relays))}.");
                lines.Add("");
                lines.Add("| | combates | ganados | caídos (media) | cae la escuadra entera | § sueltos (media) |");
                lines.Add("|---|---|---|---|---|---|");
                void Row2(string name, List<Leg> ls)
                {
                    if (ls.Count > 0) lines.Add($"| {name} | {ls.Count} | {ls.Count((l) => l.Victory)} | {ls.Average((l) => l.Fallen):0.0} | {ls.Count((l) => l.Ended == RunEnds.Wiped)} | {ls.Average((l) => l.Supplies + l.Reward):0} |");
                }
                foreach (var g in hurtLegs.GroupBy((l) => l.Row <= 2 ? "fila 2" : l.Row <= 4 ? "filas 3–4" : "fila 5").OrderBy((g) => g.Key)) Row2($"El herido, {g.Key}", g.ToList());
                Row2("El herido, todos", hurtLegs);
                Row2("Mordido (para comparar)", legs.Where((l) => l.Kind == NodeKinds.Bitten).ToList());
                var rescueLegs = legs.Where((l) => l.Kind == NodeKinds.Rescue).ToList();
                foreach (var g in rescueLegs.GroupBy((l) => l.Row <= 2 ? "fila 2" : l.Row <= 4 ? "filas 3–4" : "fila 5").Where((g) => g.Key != "fila 1").OrderBy((g) => g.Key)) Row2($"Rescate, {g.Key}", g.Where((l) => l.Row >= 2).ToList());
                Row2("Rescate, todos (filas 1–5)", rescueLegs);
            }
            // G23.4 Separados (Producto, 20/11: on the maps with R2), against the Saqueo in the same bench, by row.
            var splitLegs = legs.Where((l) => l.Kind == NodeKinds.Split).ToList();
            lines.Add("");
            lines.Add("## Separados");
            lines.Add("");
            lines.Add($"- En los mapas: {expeditions.Sum((e) => e.Split)} en {expeditions.Count} expediciones ({expeditions.Sum((e) => e.Split) / (double)Math.Max(1, expeditions.Count):0.00} por mapa; {expeditions.Count((e) => e.Split > 0) / (double)Math.Max(1, expeditions.Count):P0} de los mapas con alguno); {expeditions.Sum((e) => e.Split) / (double)count:0.0} por capítulo.");
            lines.Add($"- Jugados: {splitLegs.Count} en {count} capítulos ({splitLegs.Count / (double)count:0.00} por capítulo; filas {string.Join(", ", splitLegs.GroupBy((l) => l.Row).OrderBy((g) => g.Key).Select((g) => $"{g.Key}: {g.Count()}"))}).");
            if (splitLegs.Count > 0)
            {
                var paired = splitLegs.Where((l) => l.SplitSizes.Length == 2).ToList();
                lines.Add($"- Con dos grupos: {paired.Count} (grupos {string.Join(", ", paired.GroupBy((l) => $"{l.SplitSizes[0]}+{l.SplitSizes[1]}").OrderBy((g) => g.Key).Select((g) => $"{g.Key}: {g.Count()}"))}); sin ellos (un soldado), {splitLegs.Count - paired.Count}.");
                lines.Add($"- Cae un grupo entero antes de llegar: {paired.Count((l) => l.SplitFellBefore(0) || l.SplitFellBefore(1))} de {paired.Count}.");
                lines.Add("");
                lines.Add("| | combates | premio (ganados) | sin premio | cae la escuadra entera | caídos (media) | § sueltos (media) |");
                lines.Add("|---|---|---|---|---|---|---|");
                void Row3(string name, List<Leg> ls)
                {
                    if (ls.Count > 0) lines.Add($"| {name} | {ls.Count} | {ls.Count((l) => l.Victory)} | {ls.Count((l) => !l.Victory && l.Ended != RunEnds.Wiped)} | {ls.Count((l) => l.Ended == RunEnds.Wiped)} | {ls.Average((l) => l.Fallen):0.0} | {ls.Average((l) => l.Supplies + l.Reward):0} |");
                }
                string RowsOf(Leg l) => l.Row <= 2 ? "fila 2" : l.Row <= 4 ? "filas 3–4" : "fila 5";
                foreach (var g in splitLegs.GroupBy(RowsOf).OrderBy((g) => g.Key)) Row3($"Separados, {g.Key}", g.ToList());
                Row3("Separados, todos", splitLegs);
                var saqueo = legs.Where((l) => l.Kind == NodeKinds.Normal && l.Row >= 2).ToList();
                foreach (var g in saqueo.GroupBy(RowsOf).OrderBy((g) => g.Key)) Row3($"Saqueo, {g.Key}", g.ToList());
                Row3("Saqueo, todos (filas 2–5)", saqueo);
            }
            // G23.5 Saqueadores (Producto, 21/11: on the maps with R2, the 0.13.7): its stash, unseen or not, and what it brings
            // against a Saqueo of the same rows in the same bench (is the stash worth its risk: Looting.StashCrate's loot).
            var raidLegs = legs.Where((l) => l.Kind == NodeKinds.Raiders).ToList();
            lines.Add("");
            lines.Add("## Saqueadores");
            lines.Add("");
            lines.Add($"- En los mapas: {expeditions.Sum((e) => e.Raiders)} en {expeditions.Count} expediciones ({expeditions.Sum((e) => e.Raiders) / (double)Math.Max(1, expeditions.Count):0.00} por mapa; {expeditions.Count((e) => e.Raiders > 0) / (double)Math.Max(1, expeditions.Count):P0} de los mapas con alguno); {expeditions.Sum((e) => e.Raiders) / (double)count:0.0} por capítulo.");
            lines.Add($"- Jugados: {raidLegs.Count} en {count} capítulos ({raidLegs.Count / (double)count:0.00} por capítulo; filas {string.Join(", ", raidLegs.GroupBy((l) => l.Row).OrderBy((g) => g.Key).Select((g) => $"{g.Key}: {g.Count()}"))}).");
            if (raidLegs.Count > 0)
            {
                var camps = raidLegs.Where((l) => l.Camp).ToList();
                lines.Add($"- Con campamento: {camps.Count} de {raidLegs.Count} (sin él, un Saqueo del sitio).");
                if (camps.Count > 0)
                {
                    lines.Add($"- Cajas registradas: {string.Join(", ", Enumerable.Range(0, Mapgen.StashCrates + 1).Select((k) => $"{k}: {camps.Count((l) => l.Crates == k)}"))}; sin despertar al campamento, {camps.Sum((l) => l.Unseen)} de {camps.Sum((l) => l.Crates)} cajas, y alguna en {camps.Count((l) => l.Unseen > 0)} de {camps.Count} combates.");
                    lines.Add($"- El campamento despierta: {camps.Count((l) => l.WokeTurn != null)} de {camps.Count} (turno: {string.Join(" ", camps.Where((l) => l.WokeTurn != null).Select((l) => l.WokeTurn!.Value).OrderBy((x) => x))}); guardias tumbados o muertos: {camps.Sum((l) => l.GuardsDown)} de {camps.Sum((l) => l.Guards)}.");
                }
                lines.Add($"- Ganados {raidLegs.Count((l) => l.Victory)} de {raidLegs.Count}; caídos {raidLegs.Sum((l) => l.Fallen)} ({raidLegs.Average((l) => l.Fallen):0.0} por combate); cae la escuadra entera {raidLegs.Count((l) => l.Ended == RunEnds.Wiped)}; razones: {string.Join(", ", raidLegs.GroupBy((l) => l.Reason).OrderByDescending((g) => g.Count()).Select((g) => $"{g.Key} {g.Count()}"))}.");
                lines.Add("");
                lines.Add("| | combates | ganados | caídos (media) | cae la escuadra entera | lo hallado, § (media) | § sueltos (media) |");
                lines.Add("|---|---|---|---|---|---|---|");
                void Row4(string name, List<Leg> ls)
                {
                    if (ls.Count > 0) lines.Add($"| {name} | {ls.Count} | {ls.Count((l) => l.Victory)} | {ls.Average((l) => l.Fallen):0.0} | {ls.Count((l) => l.Ended == RunEnds.Wiped)} | {ls.Average((l) => l.Worth):0} | {ls.Average((l) => l.Supplies + l.Reward):0} |");
                }
                string RowsOf4(Leg l) => l.Row <= 4 ? "filas 3–4" : "fila 5";
                foreach (var g in raidLegs.GroupBy(RowsOf4).OrderBy((g) => g.Key)) Row4($"Saqueadores, {g.Key}", g.ToList());
                Row4("Saqueadores, con las 3 cajas", raidLegs.Where((l) => l.Crates == Mapgen.StashCrates).ToList());
                Row4("Saqueadores, con 1–2", raidLegs.Where((l) => l.Crates > 0 && l.Crates < Mapgen.StashCrates).ToList());
                Row4("Saqueadores, sin ninguna", raidLegs.Where((l) => l.Crates == 0).ToList());
                Row4("Saqueadores, todos", raidLegs);
                var saqueo3 = legs.Where((l) => l.Kind == NodeKinds.Normal && l.Row >= RunMap.RaidersFrom).ToList();
                foreach (var g in saqueo3.GroupBy(RowsOf4).OrderBy((g) => g.Key)) Row4($"Saqueo, {g.Key}", g.ToList());
                Row4("Saqueo, todos (filas 3–5)", saqueo3);
                lines.Add("");
                lines.Add("Lo hallado, a los precios de la sonda de G19 (un fusil de asalto 90 §, una escopeta 80 §, un cargador o una medicina 15 §…), con lo del alijo.");
            }
            lines.Add("");
            lines.Add("## Cada expedición");
            lines.Add("");
            foreach (var ch in chapters)
                foreach (var e in ch.Expeditions)
                    lines.Add($"{ch.Seed}·{e.Index}: {e.Squad} de {e.Soldiers} vivos, armas {e.Arms}, mejoras «{e.Upgrades}», § {e.Before} → {e.After}, fila {e.Row}, acaba {e.Ended ?? "-"}: "
                        + string.Join(" / ", e.Legs.Select((l) => $"{l.Name} f{l.Row} {(l.Victory ? "gana" : "pierde")} ({l.Reason}) {l.Fallen} caídos {l.Supplies}+{l.Reward} §{(l.KeyAt != null ? $" llave {l.KeyAt}{(l.KeyLost ? " PERDIDA" : "")}, armería {(l.ArmoryOpen ? "abierta" : "cerrada")} {l.ArmorySearched}/{l.ArmoryPieces}" : "")}{(l.Kind == NodeKinds.Wounded ? $" encontrado t{l.FoundTurn?.ToString() ?? "-"}, cargado t{l.CarriedTurn?.ToString() ?? "-"}, dentro t{l.AboardTurn?.ToString() ?? "-"}, {l.Relays} relevos{(l.WoundedDead ? ", MUERE" : "")}" : "")}{(l.Kind == NodeKinds.Bitten ? $" encontrado t{l.FoundTurn?.ToString() ?? "-"}, antiviral t{l.AntiviralTurn?.ToString() ?? "-"}, curado t{l.CuredTurn?.ToString() ?? "-"}{(l.Turned ? ", CONVERTIDO" : "")}{(l.Killed ? ", MUERTO" : "")}{(l.Left ? ", EN TIERRA" : "")}{(l.AntiviralLost ? ", ANTIVIRAL PERDIDO" : "")}" : "")}{(l.SplitSizes.Length == 2 ? $" grupos {l.SplitSizes[0]}+{l.SplitSizes[1]}, llegan t{l.SplitArrived[0]?.ToString() ?? "-"}/t{l.SplitArrived[1]?.ToString() ?? "-"}, caen t{l.SplitFell[0]?.ToString() ?? "-"}/t{l.SplitFell[1]?.ToString() ?? "-"}" : "")}{(l.Kind == NodeKinds.Raiders ? (l.Camp ? $" cajas {l.Crates} ({l.Unseen} sin despertar), despierta t{l.WokeTurn?.ToString() ?? "-"}, guardias {l.GuardsDown}/{l.Guards}, hallado {l.Worth} §" : " SIN CAMPAMENTO") : "")}{(l.Matched ? "" : " ¿?")}")));
            File.WriteAllLines(Path.Combine(BenchFactAttribute.Folder!, "r2.md"), lines);
            foreach (var l in lines.Take(30)) output.WriteLine(l);
            Assert.All(chapters, (c) => Assert.Null(c.Stuck));
            Assert.DoesNotContain(keys, (l) => l.KeyLost);
            Assert.DoesNotContain(bitten, (l) => l.AntiviralLost);
        }
    }
}
