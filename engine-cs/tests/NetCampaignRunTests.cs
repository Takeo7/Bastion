using System.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign the two test apps play by themselves (CampaignAutoRun, the app's CampaignRun), through a room with the
    /// engine's rules. The seed is the one the apps are tested with (BASTION_SEMILLA, below): its opening operation is done, its
    /// debrief comes, and its second operation, chosen on the base, is won. If the engine changes it, CampaignAutoRun finds
    /// another: MissionBot doesn't win every one. It was seed 2 till the dead went faster (28/10), and 4 till Arturo's rules of 31/10
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
    /// but not 364: its siege is lost). With no sieges (Arturo, 7/11, Campaign.SiegesOn) the second mission is the easiest
    /// operation, chosen once the wounded are fit (HeardGame.BaseOrder); of seeds 1 to 200, 47 win the first and only 31,
    /// 58, 87, 98 and 157 both, 364 not: 157, which wins both without the wait as well. With the windows in columns and the
    /// walls between rooms kept off them (playtest note 17, Arturo, 7/11: the maps change), 157 loses its second mission;
    /// of seeds 1 to 200, 44 win the first and only 47 and 91 both: 47. With a pod that heard something going to look
    /// though its first member is boxed in, and climbing in through windows to a noise inside (Arturo's notes of 14/11),
    /// 47 loses its first mission (of 10 seeds, 3 win the first, as 4 did before: MissionBot is no worse off); 71 wins
    /// both, its second mission another operation: 71. With 3 tiles more while the squad is hidden and the dead not
    /// noticing whoever comes up behind them then (Arturo, 16/11), 71 loses its first mission; of seeds 1 to 80, 14 win
    /// the first and only 23 and 53 both: 53 (BASTION_SEMILLA=53 for the apps). With the runs (Arturo, 16/11), its first two
    /// fights: the first row is always a Saqueo, and the squad sets out with four knives between two players; MissionBot
    /// searches on the way and goes for La Lata (a services area, La Lata 50 tiles off, is seldom won so). Of seeds 1 to 56,
    /// 19, 21, 28, 38, 47 and 49 won both: 19 (BASTION_SEMILLA=19 for the apps). With the hospital only inside on the map
    /// (Gameplay, 16/11), 19's first fight is a Saqueo in the hospital, won too. With 2 seats (N4) and the dead biting whoever
    /// walks past them (T20, 18/11), 19 loses its second; of seeds 1 to 80, 29 win both: 7. With La Lata's zone cut to its
    /// seats (T21), 7 loses its second; 30 won both before it and after. Since Red's apps start from a save (R5, 18/11), what
    /// a seed wins is no longer asked, each rule changing it: only that two fights are played through, none stuck, each
    /// ended.
    /// </summary>
    public class NetCampaignRunTests
    {
        [Fact]
        public void TheTestAppsCampaignIsPlayedThrough()
        {
            var played = CampaignAutoRun.Run(30, 2);
            Assert.Null(CampaignAutoRun.Stuck);
            Assert.Equal(2, played.Count);
            Assert.All(played, (m) => Assert.NotNull(m.Outcome));
        }
    }
}
