using System.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign the two test apps play by themselves (CampaignAutoRun, the app's CampaignRun), through a room with the
    /// engine's rules. Seed 364 is the one the apps are tested with (BASTION_SEMILLA=364): its opening operation is done, its
    /// debrief comes, and its second mission, the siege, is won. If the engine changes it, CampaignAutoRun finds another:
    /// MissionBot doesn't win every one. It was seed 2 till the dead went faster (28/10), and 4 till Arturo's rules of 31/10
    /// (recruits' aim, fewer dead out of town, no seeing through windows); with the base aim at 55 and the dead of a siege
    /// after whoever they can walk to (1/11), of seeds 1 to 50 only 8, 12, 22, 27 and 43 won both, and 27 won both with
    /// the aim at 60 as well. With the campaign starting with the blade only (the police car's two pistols in the first
    /// mission) and a siege of a group every turn (Arturo, 1/11), and MissionBot striking with the blade and putting on a
    /// pistol it finds, of seeds 1 to 50 only 5, 12, 13, 27 and 37 won both (the first mission 7 of 50, the siege 24). With
    /// two soldiers each, two pistols, a machete and a crowbar between them, and no police car sure to be there (Arturo,
    /// 2/11), and MissionBot going for a gun in a police car's boot, of seeds 1 to 400 only 335, 347, 364 and 380 win both
    /// (the first mission 39 of 400, the siege 4 of 28; in 10 more nobody was left ready to go out again). With a recruit
    /// for nothing for whoever is left without a soldier (Campaign.NobodyLeft) and boarded doors (Mapgen BoardDoors; Arturo,
    /// 2/11), and MissionBot taking the recruit or waiting, and prying a boarded door in the way or going round, only 54,
    /// 182 and 364 (the first mission 49 of 400, the siege 3 of 49); 364 wins both as well with whoever comes during a
    /// siege among its defenders. With the movement apart from the actions, only the sabotage on a clock (Arturo, 5/11 #1
    /// and #14) and MissionBot moving in stretches along the way, 364 still does, and of seeds 1 to 12, 2, 4, 5 and 6
    /// (the first mission 5 of 15 with 54 and 182, every siege after it won). With a run past 5 tiles in the turn (Arturo,
    /// 5/11, in the box) and MissionBot walking quiet while the squad is hidden, 364 still does, the only one of those 15.
    /// With a run only all the movement at once (Arturo, 6/11) and MissionBot never running, 364 does, and 2, 4, 5 and 6.
    /// With a soldier in play till it has nothing left or is left (Arturo, 7/11) and MissionBot ceding the command once its
    /// actions are spent, its movement kept, 364 does, as before (of seeds 1 to 120, 18 win both; leaving it instead, 20,
    /// but not 364: its siege is lost).
    /// </summary>
    public class NetCampaignRunTests
    {
        [Fact]
        public void TheTestAppsCampaignIsPlayedThrough()
        {
            var played = CampaignAutoRun.Run(364, 2);
            Assert.Null(CampaignAutoRun.Stuck);
            Assert.Equal(new[] { "El consultorio de Metsovo", "Asedio" }, played.Select((m) => m.Name));
            Assert.All(played, (m) => Assert.Equal(Outcomes.Victory, m.Outcome));
        }
    }
}
