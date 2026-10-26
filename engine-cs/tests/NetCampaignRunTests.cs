using System.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign the two test apps play by themselves (CampaignAutoRun, the app's CampaignRun), through a room with the
    /// engine's rules. Seed 2 is the one the apps are tested with (BASTION_SEMILLA=2): its opening operation is done, its
    /// debrief comes, and its second mission, the siege, is won. If the engine changes it, CampaignAutoRun finds another:
    /// MissionBot doesn't win every one (a third or so of new campaigns' opening operations, when it was written).
    /// </summary>
    public class NetCampaignRunTests
    {
        [Fact]
        public void TheTestAppsCampaignIsPlayedThrough()
        {
            var played = CampaignAutoRun.Run(2, 2);
            Assert.Null(CampaignAutoRun.Stuck);
            Assert.Equal(new[] { "El consultorio de Bielsa", "Asedio" }, played.Select((m) => m.Name));
            Assert.All(played, (m) => Assert.Equal(Outcomes.Victory, m.Outcome));
        }
    }
}
