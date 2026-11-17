using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bastion.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The chapter played whole by the bots under each variant of the knobs (Runs.Tuning; Producto, 17/11, G3-NUMEROS.md):
    /// 20 campaigns of 15 expeditions at most for each, as RunBench.R2TheWholeChapter plays them (CampaignAutoRun: two
    /// players through a real room, HeardGame on the base and the map, MissionBot in the fights). Run only with BASTION_BANCO
    /// set to the folder its table goes to (`BASTION_BANCO=/tmp/banco dotnet test engine-cs/tests --filter RunBenchVariants`).
    /// </summary>
    public class RunBenchVariants
    {
        private readonly ITestOutputHelper output;

        public RunBenchVariants(ITestOutputHelper output)
        {
            this.output = output;
        }

        private const int Campaigns = 20;
        private const int MostExpeditions = 15;

        /// <summary>Producto's first prices (variant e).</summary>
        private static readonly Dictionary<string, int> Prices = new Dictionary<string, int>
        {
            [UpgradeIds.Squad] = 120,
            [UpgradeIds.Mend] = 80,
            [UpgradeIds.Pack] = 60,
            [UpgradeIds.Stash] = 50,
            [UpgradeIds.Sight] = 60,
            [UpgradeIds.Haggle] = 80,
        };

        private static readonly (string Name, Runs.Tuning.Settings? Settings)[] Variants =
        {
            ("a) Hoy", null),
            ("b) Base llena (cuchillo)", new Runs.Tuning.Settings { Wipe = Runs.Tuning.Wipes.Fill }),
            ("c) Base llena (cuchillo y pistola)", new Runs.Tuning.Settings { Wipe = Runs.Tuning.Wipes.FillPistol }),
            ("d) Se recupera la mitad de los § y las armas de fuego", new Runs.Tuning.Settings { SalvageSupplies = 0.5, SalvageFirearms = true }),
            ("e) La de Producto: c, d, ×3 §, precios bajos", new Runs.Tuning.Settings { Wipe = Runs.Tuning.Wipes.FillPistol, SalvageSupplies = 0.5, SalvageFirearms = true, SuppliesScale = 3, UpgradePrices = Prices }),
        };

        private sealed class Chapter
        {
            public int Seed;
            public int Expeditions;
            public int? Hundred, Toro;
            public int Fights, Won;
            public string? Stuck;
            /// <summary>Whether the knobs were seen from inside the room (each flow of work its own).</summary>
            public bool Tuned;
            /// <summary>What the squads that reached El Toro carried: their weapons' types, each squad once.</summary>
            public readonly List<string> AtToro = new List<string>();
        }

        private static bool Fires(Gear? g) => g != null && Content.GearWeapon(g) is FoundWeaponDef w && w.Slot != FoundWeaponSlots.Melee;

        /// <summary>One chapter as RunBench.PlayChapter plays it, from a new campaign with the room's Rng at `seed`.</summary>
        private static Chapter PlayChapter(int seed)
        {
            var ch = new Chapter { Seed = seed };
            var going = false;
            var atToro = false;
            CampaignAutoRun.Run(seed, 1000,
                watch: (c) =>
                {
                    ch.Tuned |= Runs.Tuning.IsSet;
                    if (c.Run != null)
                    {
                        if (!going)
                        {
                            going = true;
                            atToro = false;
                            ch.Expeditions++;
                        }
                        // Reaching El Toro: what the squad carries, once.
                        if (!atToro && RunMap.Node(c.Run, c.Run.At)?.Kind == NodeKinds.Boss)
                        {
                            atToro = true;
                            foreach (var s in c.Run.Squad.Select((id) => c.Soldiers.FirstOrDefault((x) => x.Id == id)).Where((s) => s?.Kit != null))
                            {
                                var kit = s!.Kit!;
                                ch.AtToro.Add(Fires(kit.Primary) ? kit.Primary!.Type! : Fires(kit.Pistol) ? kit.Pistol!.Type! : FoundWeaponIds.Knife);
                            }
                        }
                        return;
                    }
                    going = false;
                    if (ch.Hundred == null && Runs.StashSupplies(c) >= 100) ch.Hundred = ch.Expeditions;
                    if (ch.Toro == null && (c.ChaptersDone ?? 0) >= 1) ch.Toro = ch.Expeditions;
                },
                ended: (m, c) =>
                {
                    ch.Fights++;
                    if (c.LastReport?.Victory == true) ch.Won++;
                },
                done: (c) => (c.ChaptersDone ?? 0) >= 1 || c.Run == null && ch.Expeditions >= MostExpeditions,
                steps: 60_000);
            ch.Stuck = CampaignAutoRun.Stuck;
            return ch;
        }

        private static string Median(IEnumerable<int> xs)
        {
            var v = xs.OrderBy((x) => x).ToList();
            if (v.Count == 0) return "—";
            var m = v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2.0;
            return m.ToString("0.#");
        }

        [BenchFact]
        public void TheVariants()
        {
            var clock = Stopwatch.StartNew();
            var count = int.TryParse(Environment.GetEnvironmentVariable("BASTION_BANCO_SEMILLAS"), out var n) ? n : Campaigns;
            var lines = new List<string>
            {
                $"| Variante | Expediciones hasta 100 § en el almacén (mediana) | Campañas que no llegan | Matan al Toro (de {count}) | En la expedición | Armas al llegar al Toro | Combates ganados por expedición |",
                "|---|---|---|---|---|---|---|",
            };
            var stuck = new List<string>();
            foreach (var (name, settings) in Variants)
            {
                var chapters = new Chapter[count];
                Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i) =>
                {
                    Runs.Tuning.Set(settings);
                    try
                    {
                        chapters[i] = PlayChapter(i + 1);
                    }
                    finally
                    {
                        Runs.Tuning.Reset();
                    }
                });
                var hundred = chapters.Where((c) => c.Hundred != null).Select((c) => c.Hundred!.Value).ToList();
                var toro = chapters.Where((c) => c.Toro != null).Select((c) => c.Toro!.Value).ToList();
                var arms = chapters.SelectMany((c) => c.AtToro).GroupBy((a) => a).OrderByDescending((g) => g.Count()).ThenBy((g) => g.Key, StringComparer.Ordinal).Select((g) => $"{g.Key} {g.Count()}");
                var squads = chapters.Count((c) => c.AtToro.Count > 0);
                var expeditions = chapters.Sum((c) => c.Expeditions);
                // The knobs reached the room's work, or today's played.
                Assert.All(chapters, (c) => Assert.Equal(settings != null, c.Tuned));
                lines.Add($"| {name} | {Median(hundred)} | {count - hundred.Count} | {toro.Count} | {(toro.Count > 0 ? $"mediana {Median(toro)} ({string.Join(", ", toro.OrderBy((x) => x))})" : "—")} | {(squads > 0 ? $"{squads} {(squads == 1 ? "escuadra" : "escuadras")}: {string.Join(", ", arms)}" : "nadie llega")} | {(expeditions > 0 ? (double)chapters.Sum((c) => c.Won) / expeditions : 0):0.0} |");
                stuck.AddRange(chapters.Where((c) => c.Stuck != null).Select((c) => $"{name}, semilla {c.Seed}: {c.Stuck}"));
            }
            lines.Add("");
            lines.Add($"{count} campañas por variante, {MostExpeditions} expediciones como mucho, en {clock.Elapsed.TotalMinutes:0} min. Armas: la larga si la lleva, si no la pistola, si no el cuchillo; una vez por soldado de cada escuadra que llega.");
            if (stuck.Count > 0)
            {
                lines.Add("");
                lines.Add("Atascadas: " + string.Join("; ", stuck));
            }
            File.WriteAllLines(Path.Combine(BenchFactAttribute.Folder!, "variantes.md"), lines);
            foreach (var l in lines) output.WriteLine(l);
            Assert.False(Runs.Tuning.IsSet);
        }
    }
}
