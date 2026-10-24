using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/base.test.ts: the base in the campaign. Every test but one builds a campaign (createCampaign,
    /// the file's `solo` and `fly`, executeCampaignCommand, and helpers.ts `raise` and `freeSpot`, which build on one), so
    /// only «customization › the default look is the armour soldiers had before customization» (appearance.ts alone) is
    /// here. Not ported, all needing campaign.ts:
    /// of «the base on the map (docs/FASE12.md §11)», «a new campaign starts with only the caravan in a clearing…»,
    /// «everything on the base goes to a vote with the other here…», «a proposal that no longer goes through when
    /// accepted…», «a row dragged is built at once, all or none…», «felling costs and gives nothing…», «a building works by
    /// its facility, wherever it stands…», «the caravan sleeps the first eight; a barracón 2/3/4 beds», «a building rises a
    /// level with its facility's cost and the Bastion…» and «range, La Mula, the simulation room and training do what they
    /// say»;
    /// of «the sieges in the campaign (docs/FASE12.md §11.5)», «the clock counts missions…», «held: what broke stays
    /// broken…», «the Bastion's building keeps the hits on all of it…», «nobody starts a siege on a turret, a trap or the
    /// wire…», «fallen with someone left: another clearing…», «one siege in three by night, never the first», «each places
    /// their own defenders anywhere free on the clearing…» and «fallen with nobody left: the campaign is lost»;
    /// «the Camposanto › keeps the fallen with the day they fell…»;
    /// of «the world», «a sabotage mended slowly leaves a building without power…», «contacting regions costs credits…»,
    /// «alien facilities push the project until a squad destroys them» and «events stop the clock until both answer…»;
    /// «alien bodies › are sold after a won mission, and left behind after a lost one»;
    /// of «equipment», «a proposal says what it will cost…», «accessories are bought into a shared stock…» and «equipment
    /// reaches the battlefield and is lost with its bearer»;
    /// of «customization», «players restyle their own soldiers, and the look reaches the mission» and «new recruits come
    /// without a class, with a recruit look in their player colours».
    /// </summary>
    public class BaseTests
    {
        // ------------------------------------------------------------ customization

        [Fact(DisplayName = "customization › the default look is the armour soldiers had before customization")]
        public void TheDefaultLookIsTheArmourOfBefore()
        {
            // The client used to mix the player colour into grey with THREE.Color.lerp (linear space).
            Assert.Equal("#826cbd", Appearances.DefaultAppearance(0).Primary);
            Assert.Equal("#bc854b", Appearances.DefaultAppearance(1).Primary);
        }
    }
}
