using System;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;
using static Bastion.Engine.Tests.CampaignHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The contagion's code is out since the 0.12.18 (Gameplay's review of 10/11, row 15; no bite infects since the 0.12.6,
    /// Arturo 7/11). What an older version saved with it still opens, and its fields are read by nobody and written by
    /// nobody: a soldier's `infection`, the campaign's `treatment`, a report's `infected`, a proposal to treat, and in a
    /// mission a unit's `infected` and the mission's `quarantine` and `medicineCures`.
    /// </summary>
    public class ContagionGoneTests
    {
        private static readonly string[] Fields = { "\"infection\":", "\"treatment\":", "\"infected\":", "\"medicineCures\":", "\"quarantine\":true", "\"quarantine\":false" };

        /// <summary>golden/campaign/states.json's «treatment»: day 2 at version 26, c2 bitten on the mission and her treatment waiting for the other yes.</summary>
        private static JObject Treatment()
        {
            var states = (JArray)CampaignJsonTests.Golden("states.json")["states"]!;
            return (JObject)states.Single((s) => (string)s["name"]! == "treatment")["campaign"]!.DeepClone();
        }

        private static CampaignState Read(JObject campaign) => campaign.ToObject<CampaignState>(CampaignJsonTests.Serializer)!;

        [Fact(DisplayName = "an old campaign with a fever and a treatment to confirm opens, and neither is written back")]
        public void AnOldCampaignWithAFeverOpens()
        {
            var old = Treatment();
            Assert.NotNull(old["soldiers"]![1]!["infection"]);
            Assert.NotNull(old["treatment"]);
            Assert.NotNull(old["lastReport"]!["infected"]);
            var c = Read(old);
            var written = CampaignJsonTests.Write(c);
            foreach (var field in Fields) Assert.DoesNotContain(field, written);
            // The rest, to the letter.
            var difference = CampaignJsonTests.FirstDifference(CampaignJsonTests.Clean(CampaignJsonTests.Retired(old)), CampaignJsonTests.Clean(JObject.Parse(written)));
            Assert.True(difference == null, difference);
        }

        [Fact(DisplayName = "who had the fever is one more soldier: ready, and the days go by without her turning")]
        public void WhoHadTheFeverIsOneMore()
        {
            var c = Read(Treatment());
            var bitten = c.Soldiers.Single((s) => s.Id == "c2");
            Assert.True(Campaign.SoldierReady(bitten));
            c = Campaign.MigrateCampaign(c);
            Assert.Equal(Campaign.CampaignVersion, c.Version);
            var rng = new Rng(7);
            c.ActiveMission = null;
            for (var day = 0; day < 6; day++) Campaign.AdvanceTime(c, rng);
            bitten = c.Soldiers.Single((s) => s.Id == "c2");
            Assert.True(bitten.Alive);
            Assert.True(Campaign.SoldierReady(bitten));
        }

        [Fact(DisplayName = "a campaign saved with «tratar el mordisco» proposed opens; voted yes, it does nothing and costs nothing")]
        public void AProposalToTreatOpensAndDoesNothing()
        {
            var old = Treatment();
            old["proposal"] = new JObject
            {
                ["by"] = 1,
                ["action"] = new JObject { ["type"] = "treat", ["soldier"] = "c2" },
                ["text"] = "tratar el mordisco de la soldado (2 ✚)",
            };
            var c = Read(old);
            Assert.IsType<TreatAction>(c.Proposal!.Action);
            var medicines = c.Medicines;
            var credits = c.Credits;
            // The one who proposed can't vote it; the other says yes.
            var r = Run(c, new VoteCommand { Accept = true }, 0, Both);
            Assert.True(r.Ok, r.Error);
            Assert.Null(c.Proposal);
            Assert.Equal(medicines, c.Medicines);
            Assert.Equal(credits, c.Credits);
            Assert.True(Campaign.SoldierReady(c.Soldiers.Single((s) => s.Id == "c2")));
        }

        [Fact(DisplayName = "nobody can ask for a treatment: the command has no reader any more")]
        public void TheTreatCommandIsNoCommand()
        {
            var cmd = new JObject { ["type"] = "treat", ["soldier"] = "c2", ["ready"] = true };
            var e = Assert.ThrowsAny<Exception>(() => cmd.ToObject<CampaignCommand>(CampaignJsonTests.Serializer));
            Assert.Contains("treat", e.Message);
        }

        [Fact(DisplayName = "a mission saved with a bitten soldier, the Cuarentena and the medicines that cure opens without them")]
        public void AnOldMissionOpens()
        {
            var recorded = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "net", "mision-ciudad.json")))["state"]!;
            var old = (JObject)recorded.DeepClone();
            var soldier = (JObject)((JArray)old["units"]!).First((u) => (string)u["team"]! == "xcom");
            soldier["infected"] = true;
            ((JObject)old["mission"]!)["quarantine"] = true;
            ((JObject)old["mission"]!)["medicineCures"] = true;
            var state = old.ToObject<GameState>(Json.Serializer)!;
            var written = Json.Write(state);
            foreach (var field in Fields) Assert.DoesNotContain(field, written);
            var same = Json.Write(recorded.ToObject<GameState>(Json.Serializer)!);
            Assert.Equal(same, written);
        }

        [Fact(DisplayName = "an event of the contagion has no reader: `infected` and `cured` are no events")]
        public void TheContagionsEventsAreNoEvents()
        {
            var tags = new TagConverter<GameEvent>("t", (e) => e.T).Tags;
            Assert.DoesNotContain("infected", tags);
            Assert.DoesNotContain("cured", tags);
        }
    }
}
