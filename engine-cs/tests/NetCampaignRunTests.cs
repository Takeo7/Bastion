using System.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign the two test apps play by themselves (CampaignAutoRun, the app's CampaignRun), through a room with the
    /// engine's rules. Seed 4 is the one the apps are tested with (BASTION_SEMILLA=4): its opening operation is done, its
    /// debrief comes, and its second mission, the siege, is won. If the engine changes it, CampaignAutoRun finds another:
    /// MissionBot doesn't win every one. It was seed 2 till the dead went faster (28/10); since then, of seeds 1 to 24,
    /// only 4, 12, 13 and 14 win both.
    /// </summary>
    public class NetCampaignRunTests
    {
        [Fact]
        public void TheTestAppsCampaignIsPlayedThrough()
        {
            var played = CampaignAutoRun.Run(4, 2);
            Assert.Null(CampaignAutoRun.Stuck);
            Assert.Equal(new[] { "El consultorio de Bielsa", "Asedio" }, played.Select((m) => m.Name));
            Assert.All(played, (m) => Assert.Equal(Outcomes.Victory, m.Outcome));
        }
    }
}
