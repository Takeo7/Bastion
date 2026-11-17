using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// N3 (17/11): with the Superviviente stop gone, recruits come only from a Rescate. How many a chapter's map holds, and
    /// how many a squad can take on its way: on the best way for them, and walking it at random; how many have Cuervos.
    /// Only with BASTION_MEDIR_RESCATES, the folder it writes to; a measure, not a test. The figures of before the
    /// Rescate kept in reach are in .dev-snapshots/gameplay/n3/rescates.txt.
    /// </summary>
    public class RescueCountMeasure
    {
        private const int Maps = 5000;

        [Fact]
        public void RescuesOnAChaptersWay()
        {
            var dir = Environment.GetEnvironmentVariable("BASTION_MEDIR_RESCATES");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            var report = new StringBuilder();
            report.AppendLine($"Capítulo 1, {Maps} mapas (semillas 1–{Maps}):");
            foreach (var kind in new[] { NodeKinds.Rescue, NodeKinds.Event, NodeKinds.Refuge, NodeKinds.Trade })
            {
                var onMap = 0;
                var best = new int[8];
                double walked = 0;
                var reached = 0;
                for (var seed = 1; seed <= Maps; seed++)
                {
                    var nodes = RunMap.Generate(1, seed);
                    var byId = nodes.ToDictionary((n) => n.Id);
                    onMap += nodes.Count((n) => n.Kind == kind);
                    // The most of `kind` on one way from row 1 to the boss, and the chance a random walk meets it.
                    var most = new Dictionary<string, int>();
                    var expect = new Dictionary<string, double>();
                    foreach (var n in nodes.OrderByDescending((n) => n.Row))
                    {
                        var own = n.Kind == kind ? 1 : 0;
                        most[n.Id] = own + (n.Next.Count == 0 ? 0 : n.Next.Max((x) => most[x]));
                        expect[n.Id] = own + (n.Next.Count == 0 ? 0 : n.Next.Average((x) => expect[x]));
                    }
                    var starts = nodes.Where((n) => n.Row == 1).ToList();
                    var m = starts.Max((n) => most[n.Id]);
                    best[Math.Min(7, m)]++;
                    if (m > 0) reached++;
                    walked += starts.Average((n) => expect[n.Id]);
                }
                var shares = string.Join(", ", Enumerable.Range(0, 4).Select((k) => $"{k}{(k == 3 ? "+" : "")}: {100.0 * (k == 3 ? best.Skip(3).Sum() : best[k]) / Maps:0}%"));
                report.AppendLine($"{kind}: {(double)onMap / Maps:0.00} por mapa · en el mejor camino {shares} · al azar {walked / Maps:0.00} por capítulo · alguno a mano en el {100.0 * reached / Maps:0}%");
            }
            // With Cuervos (RunMap.GuardedRescue): how many of the Rescates, and how often the one in reach has them.
            int all = 0, guarded = 0, alone = 0;
            for (var seed = 1; seed <= Maps; seed++)
            {
                var nodes = RunMap.Generate(1, seed);
                all += nodes.Count((n) => n.Kind == NodeKinds.Rescue);
                guarded += nodes.Count((n) => n.Kind == NodeKinds.Rescue && n.Guarded == true);
                // From every start, one without Cuervos in reach.
                var starts = nodes.Where((n) => n.Row == 1).ToList();
                bool Free(RunNode start)
                {
                    var seen = new HashSet<string>();
                    var next = new List<string> { start.Id };
                    while (next.Count > 0) next = next.SelectMany((id) => nodes.First((x) => x.Id == id).Next).Where(seen.Add).ToList();
                    return seen.Any((id) => nodes.First((x) => x.Id == id) is RunNode x && x.Kind == NodeKinds.Rescue && x.Guarded != true);
                }
                if (starts.All(Free)) alone++;
            }
            report.AppendLine($"Con Cuervos: {guarded} de {all} Rescates ({100.0 * guarded / all:0}%), ninguno en las filas 1–{RunMap.GuardedFrom - 1}; uno sin Cuervos a mano desde cualquier salida en el {100.0 * alone / Maps:0}% de los capítulos");
            File.WriteAllText(Path.Combine(dir, "rescates.txt"), report.ToString());
        }
    }
}
