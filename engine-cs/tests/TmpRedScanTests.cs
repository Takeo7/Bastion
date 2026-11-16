using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Bastion.Engine.Tests
{
    public class TmpRedScanTests
    {
        private readonly ITestOutputHelper output;
        public TmpRedScanTests(ITestOutputHelper output) { this.output = output; }

        [Fact]
        public void Scan()
        {
            for (var seed = 1; seed <= 16; seed++)
            {
                var played = CampaignAutoRun.Run(seed, 3);
                output.WriteLine($"semilla {seed}: {string.Join(" · ", played.Select((m) => $"{m.Name} {m.Outcome}/{m.Reason} t{m.Turns}"))}{(CampaignAutoRun.Stuck != null ? $" · ATASCADA {CampaignAutoRun.Stuck}" : "")}");
            }
        }
    }
}
