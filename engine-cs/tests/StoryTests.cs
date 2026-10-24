using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.CampaignHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/story.test.ts: the story's chapters, Simão and the final mission (lore.ts, campaign.ts), test by
    /// test. `toMatchObject` and `toEqual` on objects are compared field by field, or through their JSON (Step2Json for a
    /// mission, CampaignJsonTests.Write for the campaign's).
    /// </summary>
    public class StoryTests
    {
        // ------------------------------------------------------------ the test file's own helpers

        /// <summary>`run`: the command with `new Rng(5)`; a refusal throws.</summary>
        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            return Ok(c, cmd, slot, connected, 5);
        }

        private static CampaignState Solo()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            c.NextEventDay = 999;
            return c;
        }

        private static MissionOffer? Chapter(CampaignState c, int n) => c.Offers.FirstOrDefault((o) => o.Story == n);

        /// <summary>Flies `offer` and brings it back with `outcome`; `deadAliens` lie on the field.</summary>
        private static MissionReport Fly(CampaignState c, MissionOffer offer, string outcome, bool deadAliens = false)
        {
            Run(c, new ChooseMissionCommand { Id = offer.Id });
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0 }, Rng = new Rng(2), Kind = offer.Kind, Pods = offer.Pods });
            if (deadAliens) foreach (var u in mission.Units) if (u.Team == Teams.Alien && !u.Objective) u.Alive = false;
            mission.Outcome = outcome;
            return Campaign.ApplyMissionResult(c, mission, new Rng(1));
        }

        private static void SameJson(object? expected, object? actual)
        {
            var d = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "$");
            Assert.True(d == null, d);
        }

        // ------------------------------------------------------------ story

        [Fact(DisplayName = "story › a saved campaign of Fase 10 keeps its soldiers' bars, growth and board")]
        public void ASavedCampaignOfFase10KeepsItsSoldiersBarsGrowthAndBoard()
        {
            var old = Solo();
            var s = old.Soldiers[0];
            s.Board = new List<string> { "steadyHand" };
            var skills = CampaignJsonTests.Write(s.Skills);
            var growth = CampaignJsonTests.Write(s.Growth);
            var updated = Campaign.MigrateCampaign(old);
            Assert.Equal(skills, CampaignJsonTests.Write(updated.Soldiers[0].Skills));
            Assert.Equal(growth, CampaignJsonTests.Write(updated.Soldiers[0].Growth));
            Assert.Equal(new[] { "steadyHand" }, updated.Soldiers[0].Board);
        }

        [Fact(DisplayName = "story › updates the presentation of a saved opening battle without resetting the battle")]
        public void UpdatesThePresentationOfASavedOpeningBattleWithoutResettingTheBattle()
        {
            var state = Setup.CreateMission(new MissionOptions
            {
                Map = MapSource.Generated(Biomes.City, 2),
                Slots = new List<int> { 0 },
                Rng = new Rng(2),
                CampaignMission = new CampaignMission { Id = "opening", Story = 1, Name = "Primer invierno", Kicker = "DIARIO · HOJA 1", Briefing = "Old report." },
            });
            var expected = Events.CloneState(state);
            expected.CampaignMission = new CampaignMission { Id = "opening", Story = 1, Name = Lore.Story[0].Title, Briefing = Lore.Story[0].Briefing };
            SameJson(expected, Setup.NormalizeMission(state));
        }

        [Fact(DisplayName = "story › chapter 1 is there from the start, never expires and survives a defeat")]
        public void Chapter1IsThereFromTheStartNeverExpiresAndSurvivesADefeat()
        {
            var c = Solo();
            var first = Chapter(c, 1)!;
            Assert.Equal((Lore.Story[0].Title, MissionKinds.Recovery, RegionIds.Weu, (int?)null), (first.Name, first.Kind, first.Region, first.ExpiresDay));
            Fly(c, first, Outcomes.Defeat);
            Assert.NotNull(Chapter(c, 1));
            Assert.Equal(0, c.Story.Done);
        }

        [Fact(DisplayName = "story › winning a chapter opens the next one once the Bastion reaches its level")]
        public void WinningAChapterOpensTheNextOneOnceTheBastionReachesItsLevel()
        {
            var c = Solo();
            var report = Fly(c, Chapter(c, 1)!, Outcomes.Victory);
            Assert.Equal(1, report.Story);
            Assert.Equal(1, c.Story.Done);
            Assert.Null(Chapter(c, 1));
            Assert.Null(Chapter(c, 2));
            LevelTo(c, 2, new Rng(1));
            // Chapters don't chain back to back: the next one waits some days after the last win.
            Assert.Null(Chapter(c, 2));
            for (var i = 0; i < 10 && Chapter(c, 2) == null; i++) Campaign.AdvanceTime(c, new Rng(7 + i));
            Assert.True(c.Day >= c.Story.NextDay!.Value, $"day {c.Day}, nextDay {c.Story.NextDay}");
            var second = Chapter(c, 2)!;
            Assert.Equal((MissionKinds.Rescue, RegionIds.Afr, (int?)null), (second.Kind, second.Region, second.ExpiresDay));
            Assert.Equal("DIARIO · HOJA 2", Campaign.MissionInfo(c, second).Kicker);
            Assert.Equal("Dra. Amara Nwosu", Campaign.OfferVip(c, second));
        }

        [Fact(DisplayName = "story › Simão knocks before chapter 5; turning him away makes it harder and changes who waits inside")]
        public void SimaoKnocksBeforeChapter5TurningHimAwayMakesItHarder()
        {
            (CampaignState c, MissionOffer offer, int offers) Answer(int option)
            {
                var c = Solo();
                c.Story.Done = Lore.SimonChapter - 1;
                LevelTo(c, Lore.SimonChapter, new Rng(1));
                Assert.Equal(CampaignEventIds.Simon, c.Event);
                Assert.Null(Chapter(c, Lore.SimonChapter));
                var offers = c.Offers.Count;
                Run(c, new AnswerEventCommand { Option = option });
                return (c, Chapter(c, Lore.SimonChapter)!, offers);
            }
            var trusted = Answer(0);
            Assert.Equal(Lore.SimonChoices.Trusted, trusted.c.Story.Simon);
            Assert.Equal(Lore.Story[Lore.SimonChapter - 1].Difficulty, trusted.offer.Difficulty);
            Assert.Equal("Simão Ferreira", Campaign.OfferVip(trusted.c, trusted.offer));
            var refused = Answer(1);
            Assert.Equal(Lore.SimonChoices.Refused, refused.c.Story.Simon);
            // What he knew, now that the Bastion has no experience (18/10 #40): a new operation, besides the chapter.
            Assert.Equal(refused.offers + 2, refused.c.Offers.Count);
            Assert.Equal(Lore.Story[Lore.SimonChapter - 1].Difficulty + 1, refused.offer.Difficulty);
            Assert.Equal("Noor Haddad", Campaign.OfferVip(refused.c, refused.offer));
        }

        [Fact(DisplayName = "story › the rescue VIP of a chapter reaches the battlefield with their name")]
        public void TheRescueVipOfAChapterReachesTheBattlefieldWithTheirName()
        {
            var c = Solo();
            c.Story.Done = 6;
            LevelTo(c, 7, new Rng(1));
            var offer = Chapter(c, 7)!;
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0 }, Rng = new Rng(2), Kind = offer.Kind, Pods = offer.Pods, VipName = Campaign.OfferVip(c, offer) });
            Assert.Equal("Celia Marsh", mission.Units.FirstOrDefault((u) => u.Template == TemplateIds.Vip)?.Name);
        }

        [Fact(DisplayName = "story › Dr Nwosu makes the bodies worth a quarter more")]
        public void DrNwosuMakesTheBodiesWorthAQuarterMore()
        {
            int Salvage(int done)
            {
                var c = Solo();
                c.Story.Done = done;
                return Fly(c, c.Offers.First((o) => o.Story == null), Outcomes.Victory, true).Salvage;
            }
            var before = Salvage(1);
            Assert.True(before > 0);
            Assert.Equal((int)Js.Round(before * 1.25), Salvage(2));
        }

        [Fact(DisplayName = "story › the final mission needs every chapter, and ends with a decision taken with both keys")]
        public void TheFinalMissionNeedsEveryChapterAndEndsWithADecisionTakenWithBothKeys()
        {
            var c = Campaign.CreateCampaign(new[] { 0, 1 }, new Rng(4));
            // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
            c.Siege.In = 99;
            c.NextEventDay = 999;
            c.Story.Done = 6;
            c.Story.Simon = Lore.SimonChoices.Trusted;
            LevelTo(c, Progression.MaxLevel, new Rng(1));
            Assert.DoesNotContain(c.Offers, (o) => o.Final);
            Fly(c, Chapter(c, 7)!, Outcomes.Victory);
            var final = c.Offers.FirstOrDefault((o) => o.Final);
            Assert.NotNull(final);
            Fly(c, final!, Outcomes.Victory);
            Assert.Equal(CampaignEventIds.Finale, c.Event);
            Assert.Null(c.Outcome);
            // One commander proposes, the other turns the second key.
            Run(c, new AnswerEventCommand { Option = 1 }, 0, Both);
            Assert.Null(c.Outcome);
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Equal(Endings.Record, c.Ending);
            Assert.Equal(Outcomes.Victory, c.Outcome);
        }

        [Fact(DisplayName = "story › random events never bring a story event")]
        public void RandomEventsNeverBringAStoryEvent()
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var c = Solo();
                c.NextEventDay = c.Day + 1;
                Campaign.AdvanceTime(c, new Rng(seed));
                if (c.Event != null) Assert.Null(Campaign.EventDef(c)?.Story);
            }
        }
    }
}
