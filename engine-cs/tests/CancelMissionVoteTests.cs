using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// «Cancelar misión» goes to a vote with the partner here, as choosing a mission and contacting do (Arturo, 26/10):
    /// CancelMissionAction. Not in TypeScript, where `cancelMission` called it off at once; alone it still does, and it is
    /// refused as before.
    /// </summary>
    public class CancelMissionVoteTests
    {
        private static readonly int[] Both = CampaignHelpers.Both;

        /// <summary>Sieges.cs SiegeNow.</summary>
        private const string SiegeNow = "Viene el asedio: no hay otra cosa que hacer.";

        /// <summary>A campaign of two with its opening mission chosen, the squad picked and one key turned to launch.</summary>
        private static (CampaignState C, MissionOffer Offer) Chosen()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            Campaign.OpeningMission(c);
            c.LaunchReady[1] = true;
            return (c, Campaign.ActiveOffer(c)!);
        }

        /// <summary>What calling it off did before, and does now once agreed.</summary>
        private static void CalledOff(CampaignState c)
        {
            Assert.Null(c.ActiveMission);
            Assert.Empty(c.Squad);
            Assert.Equal(new[] { false, false }, c.LaunchReady);
            Assert.Null(c.Proposal);
            Assert.Equal("Misión cancelada.", c.Log[0].Text);
        }

        private static void Refused(string error, CampaignState c, CampaignCommand cmd, int slot, int[] connected)
        {
            var r = CampaignHelpers.Run(c, cmd, slot, connected);
            Assert.Equal((false, error), (r.Ok, r.Error));
        }

        [Fact(DisplayName = "cancel mission › alone it is called off at once, as before")]
        public void AloneItIsCalledOffAtOnce()
        {
            var (c, _) = Chosen();
            CampaignHelpers.Ok(c, new CancelMissionCommand(), 1, new[] { 1 });
            CalledOff(c);
            // A campaign of one: the same.
            var solo = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            Campaign.OpeningMission(solo);
            CampaignHelpers.Ok(solo, new CancelMissionCommand(), 0, new[] { 0 });
            CalledOff(solo);
        }

        [Fact(DisplayName = "cancel mission › with the partner here it is proposed: their yes calls it off, their no keeps it")]
        public void WithThePartnerHereItIsProposed()
        {
            var (c, offer) = Chosen();
            var squad = c.Squad.ToList();
            Assert.NotEmpty(squad);
            CampaignHelpers.Ok(c, new CancelMissionCommand(), 0, Both);
            var action = Assert.IsType<CancelMissionAction>(c.Proposal?.Action);
            Assert.Equal((0, offer.Id), (c.Proposal!.By, action.Id));
            Assert.Equal($"cancelar la misión «{offer.Name}»", c.Proposal.Text);
            Assert.Equal(0, Campaign.ProposalCost(c, action));
            // Nothing called off yet; the one who proposed doesn't vote, and nothing else is proposed meanwhile.
            Assert.Equal(offer.Id, c.ActiveMission);
            Assert.Equal(squad, c.Squad);
            Refused("No puedes votar tu propia propuesta.", c, new VoteCommand { Accept = true }, 0, Both);
            Refused("Ya hay una propuesta pendiente.", c, new CancelMissionCommand(), 1, Both);
            CampaignHelpers.Ok(c, new VoteCommand { Accept = true }, 1, Both);
            CalledOff(c);

            // A no: the mission stays, with its squad.
            var (d, kept) = Chosen();
            squad = d.Squad.ToList();
            CampaignHelpers.Ok(d, new CancelMissionCommand(), 1, Both);
            CampaignHelpers.Ok(d, new VoteCommand { Accept = false }, 0, Both);
            Assert.Null(d.Proposal);
            Assert.Equal(kept.Id, d.ActiveMission);
            Assert.Equal(squad, d.Squad);
            Assert.Equal($"Propuesta rechazada: cancelar la misión «{kept.Name}».", d.Log[0].Text);
        }

        [Fact(DisplayName = "cancel mission › refused as before, alone or proposing, and a yes that no longer goes through is said so")]
        public void RefusedAsBefore()
        {
            // Nothing chosen.
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            Refused("No hay misión elegida.", c, new CancelMissionCommand(), 0, new[] { 0 });
            Refused("No hay misión elegida.", c, new CancelMissionCommand(), 0, Both);
            Assert.Null(c.Proposal);
            // A siege isn't called off.
            var (s, siege) = Chosen();
            siege.Siege = true;
            Refused(SiegeNow, s, new CancelMissionCommand(), 0, new[] { 0 });
            Refused(SiegeNow, s, new CancelMissionCommand(), 0, Both);
            Assert.Null(s.Proposal);
            Assert.Equal(siege.Id, s.ActiveMission);

            // Checked again on the yes: a siege come meanwhile, and nothing called off.
            var (v, first) = Chosen();
            CampaignHelpers.Ok(v, new CancelMissionCommand(), 0, Both);
            first.Siege = true;
            CampaignHelpers.Ok(v, new VoteCommand { Accept = true }, 1, Both);
            Assert.Null(v.Proposal);
            Assert.Equal(first.Id, v.ActiveMission);
            Assert.Contains(v.Log, (l) => l.Text == $"Propuesta aceptada, pero ya no se puede: cancelar la misión «{first.Name}». {SiegeNow}");
            // The mission called off meanwhile, alone.
            var (w, gone) = Chosen();
            CampaignHelpers.Ok(w, new CancelMissionCommand(), 0, Both);
            CampaignHelpers.Ok(w, new CancelMissionCommand(), 0, new[] { 0 });
            CampaignHelpers.Ok(w, new VoteCommand { Accept = true }, 1, Both);
            Assert.Null(w.ActiveMission);
            Assert.Equal($"Propuesta aceptada, pero ya no se puede: cancelar la misión «{gone.Name}». No hay misión elegida.", w.Log[0].Text);
            // Another one chosen meanwhile, alone (the partner away): the yes was about the one before, and that one stays.
            var (x, before) = Chosen();
            CampaignHelpers.Ok(x, new CancelMissionCommand(), 0, Both);
            var next = x.Offers.First((o) => o.Id != before.Id);
            CampaignHelpers.Ok(x, new ChooseMissionCommand { Id = next.Id }, 0, new[] { 0 });
            CampaignHelpers.Ok(x, new VoteCommand { Accept = true }, 1, Both);
            Assert.Equal(next.Id, x.ActiveMission);
            Assert.Equal($"Propuesta aceptada, pero ya no se puede: cancelar la misión «{before.Name}». Esa misión ya no está elegida.", x.Log[0].Text);
        }
    }
}
