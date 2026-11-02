using System.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign the two test apps play by themselves (CampaignAutoRun, the app's CampaignRun), through a room with the
    /// engine's rules. Seed 27 is the one the apps are tested with (BASTION_SEMILLA=27): its opening operation is done, its
    /// debrief comes, and its second mission, the siege, is won. If the engine changes it, CampaignAutoRun finds another:
    /// MissionBot doesn't win every one. It was seed 2 till the dead went faster (28/10), and 4 till Arturo's rules of 31/10
    /// (recruits' aim, fewer dead out of town, no seeing through windows); with the base aim at 55 and the dead of a siege
    /// after whoever they can walk to (1/11), of seeds 1 to 50 only 8, 12, 22, 27 and 43 won both, and 27 won both with
    /// the aim at 60 as well. With the campaign starting with the blade only (the police car's two pistols in the first
    /// mission) and a siege of a group every turn (Arturo, 1/11), and MissionBot striking with the blade and putting on a
    /// pistol it finds, of seeds 1 to 50 only 5, 12, 13, 27 and 37 win both (the first mission 7 of 50, the siege 24).
    /// </summary>
    public class NetCampaignRunTests
    {
        [Fact]
        public void TheTestAppsCampaignIsPlayedThrough()
        {
            var played = CampaignAutoRun.Run(27, 2);
            Assert.Null(CampaignAutoRun.Stuck);
            Assert.Equal(new[] { "El consultorio de Metsovo", "Asedio" }, played.Select((m) => m.Name));
            Assert.All(played, (m) => Assert.Equal(Outcomes.Victory, m.Outcome));
        }
    }
}
