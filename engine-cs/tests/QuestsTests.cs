using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Newtonsoft.Json.Linq;
using Xunit;
using static Bastion.Engine.Tests.CampaignHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/quests.test.ts: rumours and side quests (quests.ts, campaign.ts), test by test.
    /// `toMatchObject` and `toEqual` on objects are compared field by field, or through their JSON (CampaignJsonTests.Write).
    /// </summary>
    public class QuestsTests
    {
        // ------------------------------------------------------------ the test file's own helpers

        /// <summary>`run`: the command with `new Rng(5)`; a refusal throws.</summary>
        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            return Ok(c, cmd, slot, connected, 5);
        }

        /// <summary>A solo campaign with no random events and no rumours of its own.</summary>
        private static CampaignState Solo(int seed = 3)
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(seed));
            c.NextEventDay = 999;
            c.NextRumorDay = 999;
            return c;
        }

        private static void AdvanceUntil(CampaignState c, Func<bool> done, int seed = 1)
        {
            for (var i = 0; i < 30 && !done(); i++)
            {
                c.Event = null;
                Campaign.AdvanceTime(c, new Rng(seed + i));
            }
            Assert.True(done());
        }

        /// <summary>Flies `offer` and brings it back with `outcome`.</summary>
        private static GameState Fly(CampaignState c, MissionOffer offer, string outcome)
        {
            Run(c, new ChooseMissionCommand { Id = offer.Id });
            var info = Campaign.MissionInfo(c, offer);
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0 }, Rng = new Rng(2), Kind = offer.Kind, Pods = offer.Pods, VipName = info.VipName, Leader = info.Leader });
            mission.Outcome = outcome;
            Campaign.ApplyMissionResult(c, mission, new Rng(1));
            return mission;
        }

        private static MissionOffer QuestOffer(CampaignState c, string id) => c.Offers.First((o) => o.Quest == id);

        private static RegionState RegionOf(CampaignState c, string id) => c.Regions.First((r) => r.Id == id);

        private static int Count(Dictionary<string, int> copies, string id) => copies.TryGetValue(id, out var n) ? n : 0;

        private static string? OutcomeOf(CampaignState c, string quest) => c.QuestLog.TryGetValue(quest, out var entry) ? entry.Outcome : null;

        /// <summary>Puts a rumour in `region`, listens to it and scans until it resolves.</summary>
        private static void Listen(CampaignState c, string region)
        {
            RegionOf(c, region).Contacted = true;
            c.Rumors = new List<Rumor> { new Rumor { Id = "r99", Region = region, Text = "algo", ExpiresDay = c.Day + 10, DaysLeft = 1 } };
            Run(c, new InvestigateCommand { Id = "r99" });
            Assert.Equal("r99", c.Listening);
            Campaign.AdvanceTime(c, new Rng(1));
            Assert.Empty(c.Rumors);
            Assert.Null(c.Listening);
        }

        /// <summary>The squad going out on the campaign's first offer (read again once chosen, as the test does), its mission won and brought back with `new Rng(seed)`.</summary>
        private static void FlyFirstOffer(CampaignState c, int seed)
        {
            Run(c, new ChooseMissionCommand { Id = c.Offers[0].Id });
            var mission = Setup.CreateMission(new MissionOptions { Map = c.Offers[0].Map, Slots = new List<int> { 0 }, Rng = new Rng(2), Kind = c.Offers[0].Kind, Squad = c.Squad.Select((id) => Campaign.SquadMember(c, c.Soldiers.First((s) => s.Id == id))).ToList() });
            mission.Outcome = Outcomes.Victory;
            Campaign.ApplyMissionResult(c, mission, new Rng(seed));
        }

        // ------------------------------------------------------------ rumours

        [Fact(DisplayName = "rumours › appear while scanning, with a pop-up, and cool down if nobody listens; none without a Sala de radio (Ana, 23/10)")]
        public void AppearWhileScanningWithAPopUpAndCoolDownIfNobodyListens()
        {
            var c = Solo();
            // No radio, no rumours: they come over the air.
            c.NextRumorDay = c.Day + 1;
            Campaign.AdvanceTime(c, new Rng(1));
            Assert.Empty(c.Rumors);
            Raise(c, PieceIds.Radio);
            c.NextRumorDay = c.Day + 1;
            Campaign.AdvanceTime(c, new Rng(1));
            Assert.Single(c.Rumors);
            Assert.Contains(c.Log, (l) => l.Alert?.Kicker == "RUMOR");
            var rumor = c.Rumors[0];
            AdvanceUntil(c, () => c.Day > rumor.ExpiresDay && !c.Rumors.Any((r) => r.Id == rumor.Id));
        }

        [Fact(DisplayName = "rumours › listening to one in a contacted region opens that region's side quest")]
        public void ListeningToOneInAContactedRegionOpensThatRegionsSideQuest()
        {
            var c = Solo();
            Listen(c, RegionIds.Weu);
            Assert.Equal(new[] { QuestIds.Bells }, c.Quests.Select((q) => q.Id));
            Assert.Equal(CampaignEventIds.Quest, c.Event);
            Assert.Equal(Quests.QuestDefs[QuestIds.Bells].Title, Campaign.EventDef(c)?.Title);
        }

        // ------------------------------------------------------------ side quests

        [Fact(DisplayName = "side quests › a decision leads to an operation, and winning it pays off for good")]
        public void ADecisionLeadsToAnOperationAndWinningItPaysOffForGood()
        {
            var c = Solo();
            Listen(c, RegionIds.Weu);
            Run(c, new AnswerEventCommand { Option = 0 });
            var offer = QuestOffer(c, QuestIds.Bells);
            Assert.Equal((MissionKinds.Sabotage, RegionIds.Weu), (offer.Kind, offer.Region));
            // Operations don't expire (18/10 #39), a quest's either.
            Assert.Null(offer.ExpiresDay);
            var flashbangs = Count(c.Inventory, ItemIds.Flashbang);
            Fly(c, offer, Outcomes.Victory);
            Assert.Empty(c.Quests);
            Assert.Equal("success", OutcomeOf(c, QuestIds.Bells));
            Assert.Equal(flashbangs + 2, c.Inventory[ItemIds.Flashbang]);
            Assert.Equal(40, Campaign.RegionIncome(c, RegionIds.Weu));
        }

        [Fact(DisplayName = "side quests › an operation lost loses the quest with its consequence; left waiting, it waits (18/10 #39)")]
        public void AnOperationLostLosesTheQuestWithItsConsequenceLeftWaitingItWaits()
        {
            var c = Solo();
            Listen(c, RegionIds.Na);
            var offer = QuestOffer(c, QuestIds.Route50);
            Assert.Equal("Lola Haskins", Campaign.MissionInfo(c, offer).VipName);
            for (var i = 0; i < 6; i++)
            {
                c.Event = null;
                Campaign.AdvanceTime(c, new Rng(1 + i));
            }
            Assert.Null(OutcomeOf(c, QuestIds.Route50));
            Assert.Contains(offer, c.Offers);
            Fly(c, offer, Outcomes.Defeat);
            Assert.Equal("failure", OutcomeOf(c, QuestIds.Route50));
            Assert.Equal(25, Campaign.RegionIncome(c, RegionIds.Na));
        }

        [Fact(DisplayName = "side quests › Lindqvist (quest «ibarra») leads her own squad, and capturing her brings her back scarred")]
        public void LindqvistLeadsHerOwnSquadAndCapturingHerBringsHerBackScarred()
        {
            var c = Solo();
            // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
            c.Siege.In = 99;
            c.Story.Done = 1;
            LevelTo(c, 3, new Rng(1));
            Fly(c, QuestOffer(c, QuestIds.Ibarra), Outcomes.Victory);
            var hunt = QuestOffer(c, QuestIds.Ibarra);
            Assert.Equal(MissionKinds.Elimination, hunt.Kind);
            var mission = Fly(c, hunt, Outcomes.Victory);
            Assert.Equal(TemplateIds.Officer, mission.Units.FirstOrDefault((u) => u.Team == Teams.Alien && u.Name == "Vera Lindqvist")?.Template);
            Assert.Equal("Vera Lindqvist", Campaign.EventDef(c)?.Title);
            Run(c, new AnswerEventCommand { Option = 0 });
            var ibarra = c.Soldiers.First((s) => s.Name == "Vera Lindqvist");
            // She has been out there: three levels more than a recruit (docs/FASE9.md).
            Assert.Equal(new[] { TraitIds.Scarred }, ibarra.Traits);
            Assert.True(Bars.TotalBars(ibarra.Skills) >= 4);
            // Her model is a woman's, whatever name the recruit drew first.
            Assert.Equal(CampaignJsonTests.Write(Appearances.DefaultAppearance(ibarra.Owner, null, "f", ibarra.Id)), CampaignJsonTests.Write(ibarra.Appearance));
            // `{ ...ibarra, traits: [] }`
            var plain = JObject.Parse(CampaignJsonTests.Write(ibarra)).ToObject<CampaignSoldier>(CampaignJsonTests.Serializer)!;
            plain.Traits = new List<string>();
            Assert.True(Campaign.SquadMember(c, ibarra).Will < Campaign.SquadMember(c, plain).Will);
        }

        [Fact(DisplayName = "side quests › the doctor's assistant brings antibiotics before their level")]
        public void TheDoctorsAssistantBringsAntibioticsBeforeTheirLevel()
        {
            var c = Solo();
            c.Story.Done = 2;
            RegionOf(c, RegionIds.Afr).Contacted = true;
            Campaign.AdvanceTime(c, new Rng(1));
            Assert.Equal(QuestIds.Amara, c.QuestEvent);
            Assert.False(Progression.HasTech(c, "nanoMedkit"));
            Run(c, new AnswerEventCommand { Option = 0 });
            Fly(c, QuestOffer(c, QuestIds.Amara), Outcomes.Victory);
            Run(c, new AnswerEventCommand { Option = 0 });
            Assert.Equal("rescued", OutcomeOf(c, QuestIds.Amara));
            Assert.True(Progression.HasTech(c, "nanoMedkit"));
        }

        [Fact(DisplayName = "side quests › the numbers station adds a contact")]
        public void TheNumbersStationAddsAContact()
        {
            var c = Solo();
            // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
            c.Siege.In = 99;
            c.Day = 20;
            LevelTo(c, 2, new Rng(1));
            var before = Campaign.ContactCapacity(c);
            Fly(c, QuestOffer(c, QuestIds.Numbers), Outcomes.Victory);
            Fly(c, QuestOffer(c, QuestIds.Numbers), Outcomes.Victory);
            Assert.Equal(before + 1, Campaign.ContactCapacity(c));
        }

        [Fact(DisplayName = "side quests › the mole: watching someone gives a clue, and only the right accusation pays")]
        public void TheMoleWatchingSomeoneGivesAClueAndOnlyTheRightAccusationPays()
        {
            (CampaignState c, int doom) Accuse(bool right)
            {
                var c = Solo();
                c.Seen[CampaignEventIds.Sabotage] = 2;
                c.Credits = 100;
                Campaign.AdvanceTime(c, new Rng(1));
                Assert.Equal(QuestIds.Mole, c.QuestEvent);
                var secret = c.Quests[0].Secret;
                Run(c, new AnswerEventCommand { Option = 0 });
                // The clue opens the second decision.
                Assert.Contains(secret == 0 ? "antena vieja" : "no se mueve", Campaign.EventDef(c)?.Text);
                var doom = c.Doom;
                Run(c, new AnswerEventCommand { Option = right ? secret : 1 - secret });
                return (c, doom);
            }
            var rightOne = Accuse(true);
            Assert.Equal("success", OutcomeOf(rightOne.c, QuestIds.Mole));
            Assert.Equal(rightOne.doom - 1, rightOne.c.Doom);
            var wrong = Accuse(false);
            Assert.Equal("failure", OutcomeOf(wrong.c, QuestIds.Mole));
            Assert.Equal(40, wrong.c.Credits);
        }

        [Fact(DisplayName = "side quests › turning down a soldier who saw a relative leaves a grudge")]
        public void TurningDownASoldierWhoSawARelativeLeavesAGrudge()
        {
            for (var seed = 0; seed < 60; seed++)
            {
                var c = Solo(seed);
                LevelTo(c, 2, new Rng(1));
                c.Quests = new List<QuestState>();
                c.Offers = c.Offers.Where((o) => o.Quest == null).ToList();
                foreach (var s in c.Soldiers) s.Nickname = "Mula";
                FlyFirstOffer(c, seed);
                var faces = c.Quests.FirstOrDefault((q) => q.Id == QuestIds.Faces);
                if (faces == null) continue;
                Assert.Equal(QuestIds.Faces, c.QuestEvent);
                Assert.Contains(faces.Names["relative"], Campaign.EventDef(c)?.Text);
                var soldier = c.Soldiers.First((s) => s.Id == faces.Soldier);
                var aim = Campaign.SquadMember(c, soldier).Mods.Aim;
                Run(c, new AnswerEventCommand { Option = 1 });
                Assert.Equal(new[] { TraitIds.Grudge }, soldier.Traits);
                Assert.Equal(aim + 10, Campaign.SquadMember(c, soldier).Mods.Aim);
                return;
            }
            throw new InvalidOperationException("No soldier recognised anyone in 60 campaigns");
        }

        [Fact(DisplayName = "side quests › the relative a soldier recognises never has the soldier’s own first name")]
        public void TheRelativeASoldierRecognisesNeverHasTheSoldiersOwnFirstName()
        {
            var found = 0;
            for (var seed = 0; seed < 300 && found < 12; seed++)
            {
                var c = Solo(seed);
                LevelTo(c, 2, new Rng(1));
                c.Quests = new List<QuestState>();
                c.Offers = c.Offers.Where((o) => o.Quest == null).ToList();
                // Every soldier shares a first name with one of the relatives.
                foreach (var s in c.Soldiers)
                {
                    s.Nickname = "Mula";
                    s.Name = $"Mariam {string.Join(" ", s.Name.Split(' ').Skip(1))}";
                }
                FlyFirstOffer(c, seed);
                var faces = c.Quests.FirstOrDefault((q) => q.Id == QuestIds.Faces);
                if (faces == null) continue;
                found++;
                Assert.NotEqual("Mariam", faces.Names["relative"].Split(' ')[0]);
            }
            Assert.True(found >= 12, $"found {found}");
        }

        // ------------------------------------------------------------ what comes by radio (Ana, 23/10 #27)

        [Fact(DisplayName = "what comes by radio (Ana, 23/10 #27) › the intercepted transmission only with a Sala de radio")]
        public void TheInterceptedTransmissionOnlyWithASalaDeRadio()
        {
            for (var seed = 1; seed <= 40; seed++)
            {
                var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(seed));
                c.NextRumorDay = 999;
                c.NextEventDay = c.Day + 1;
                Campaign.AdvanceTime(c, new Rng(seed));
                Assert.NotEqual(CampaignEventIds.Signal, c.Event);
            }
        }
    }
}
