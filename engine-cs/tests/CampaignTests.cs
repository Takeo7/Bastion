using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bastion.Engine;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/campaign.test.ts: the campaign (campaign.ts) with progression.ts, skills.ts, base.ts, events.ts
    /// and setup.ts around it. All 35 tests are here. Where TypeScript says a text that text-fixes.ts changed, the test says
    /// the fixed one, with a comment.
    /// </summary>
    public class CampaignTests
    {
        private static readonly int[] Both = { 0, 1 };

        /// <summary>The file's `run`: both seats connected unless told, a fresh Rng(7), and it must go through.</summary>
        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            return CampaignHelpers.Ok(c, cmd, slot, connected ?? Both);
        }

        /// <summary>`expect(() => run(…)).toThrow(text)`: refused, and `text` is in why.</summary>
        private static void Throws(string text, CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            var r = CampaignHelpers.Run(c, cmd, slot, connected ?? Both);
            Assert.False(r.Ok, $"expected «{text}», and it went through");
            Assert.Contains(text, r.Error);
        }

        /// <summary>A deep copy, as `JSON.parse(JSON.stringify(c))`.</summary>
        private static CampaignState Copy(CampaignState c)
        {
            return JToken.Parse(CampaignJsonTests.Write(c)).ToObject<CampaignState>(CampaignJsonTests.Serializer)!;
        }

        /// <summary>`toEqual`, through their JSON: missing and null are the same, as undefined there.</summary>
        private static void Same(object expected, object? actual)
        {
            Assert.NotNull(actual);
            var a = CampaignJsonTests.Clean(JToken.Parse(CampaignJsonTests.Write(expected)));
            var b = CampaignJsonTests.Clean(JToken.Parse(CampaignJsonTests.Write(actual!)));
            var difference = CampaignJsonTests.FirstDifference(a, b);
            Assert.True(difference == null, difference);
        }

        /// <summary>`toMatchObject` on a record of numbers: every key of `expected` with its value (more keys are fine).</summary>
        private static void Has(IReadOnlyDictionary<string, int> expected, IReadOnlyDictionary<string, int>? actual)
        {
            Assert.NotNull(actual);
            foreach (var kv in expected)
            {
                Assert.True(actual!.TryGetValue(kv.Key, out var v), $"no {kv.Key}");
                Assert.True(kv.Value == v, $"{kv.Key}: expected {kv.Value}, got {v}");
            }
        }

        /// <summary>A story chapter's offer (`o.story` truthy).</summary>
        private static bool IsStory(MissionOffer o) => (o.Story ?? 0) != 0;

        /// <summary>The mission of `offer` for the squad `c` sends, as most tests here make it.</summary>
        private static GameState MissionFor(CampaignState c, MissionOffer offer)
        {
            return Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = Both.ToList(), Rng = new Rng(3), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods, Difficulty = offer.Difficulty });
        }

        private static Skills SkillsWith(params (string Skill, int Value)[] lines)
        {
            var s = Bars.NoSkills();
            foreach (var (skill, value) in lines) s[skill] = value;
            return s;
        }

        private static Growth GrowthWith(params (string Skill, int Value)[] lines)
        {
            var g = Bars.NoGrowth();
            foreach (var (skill, value) in lines) g[skill] = value;
            return g;
        }

        // ------------------------------------------------------------ campaign setup

        [Fact(DisplayName = "campaign setup › two players start with four soldiers each, credits, a level-1 Bastion, two operations and the first story chapter")]
        public void TwoPlayersStartWithFourSoldiersEach()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            Assert.Equal(4, c.Soldiers.Count((s) => s.Owner == 0));
            Assert.Equal(4, c.Soldiers.Count((s) => s.Owner == 1));
            Assert.Equal(2, c.Offers.Count((o) => !IsStory(o)));
            Assert.Equal(new int?[] { 1 }, c.Offers.Where(IsStory).Select((o) => o.Story));
            Assert.True(c.Credits > 0);
            Assert.Equal(1, Progression.BastionLevel(c));
        }

        [Fact(DisplayName = "campaign setup › a solo campaign gives all eight soldiers to the only player")]
        public void ASoloCampaignGivesAllEightSoldiersToTheOnlyPlayer()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            Assert.True(c.Soldiers.All((s) => s.Owner == 0));
            Assert.Equal(8, c.Soldiers.Count);
        }

        // ---------------------------------------------------------- shared decisions

        [Fact(DisplayName = "shared decisions › contacting a region is proposed to the partner and happens when accepted")]
        public void ContactingARegionIsProposedToThePartner()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            var credits = c.Credits;
            var cost = Campaign.ContactCost(c);
            var vega = c.Regions.First((r) => r.Id == "oce");
            Run(c, new ContactCommand { Region = "oce" }, 0);
            Assert.Equal(0, c.Proposal?.By);
            Assert.False(vega.Contacted);
            Assert.False(CampaignHelpers.Run(c, new VoteCommand { Accept = true }, 0, Both, 1).Ok);
            Run(c, new VoteCommand { Accept = true }, 1);
            Assert.True(vega.Contacted);
            Assert.Equal(credits - cost, c.Credits);
            Assert.Null(c.Proposal);
        }

        [Fact(DisplayName = "shared decisions › a rejected proposal changes nothing; alone, decisions apply directly")]
        public void ARejectedProposalChangesNothing()
        {
            // The Sala de radio is dear: it goes to a vote (docs/FASE12.md §11.7).
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            c.Level = 2;
            var spot = CampaignHelpers.FreeSpot(c, "radio");
            Run(c, new PlaceCommand { Piece = "radio", X = spot.X, Y = spot.Y, Rot = 0 }, 1);
            var action = Assert.IsType<PlaceAction>(c.Proposal?.Action);
            Assert.Equal(("radio", spot.X, spot.Y, 0), (action.Piece, action.X, action.Y, action.Rot));
            Run(c, new VoteCommand { Accept = false }, 0);
            Assert.DoesNotContain(c.Bastion.Pieces, (p) => p.Piece == "radio");
            Run(c, new PlaceCommand { Piece = "radio", X = spot.X, Y = spot.Y, Rot = 0 }, 1, new[] { 1 });
            Assert.Contains(c.Bastion.Pieces, (p) => p.Piece == "radio");
        }

        // ---------------------------------------------------------------------- time

        [Fact(DisplayName = "time › goes by with the missions, and a day at a time waiting at the Bastión, both agreeing (Ana and Arturo, 22/10)")]
        public void GoesByWithTheMissionsAndADayAtATime()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            c.NextEventDay = 999;
            // One alone waits for the other; a «ready» taken back changes nothing.
            Run(c, new AdvanceCommand { Ready = true }, 0);
            Assert.Equal(1, c.Day);
            Run(c, new AdvanceCommand { Ready = false }, 0);
            Run(c, new AdvanceCommand { Ready = true }, 1);
            Assert.Equal(1, c.Day);
            // Both: a day goes by, and what it ate is in the log.
            var food = c.Food!.Value;
            Run(c, new AdvanceCommand { Ready = true }, 0);
            Assert.Equal(2, c.Day);
            Assert.Equal(new[] { false, false }, c.AdvanceReady);
            Assert.True(c.Food < food);
            Assert.Contains(c.Log, (l) => l.Text.StartsWith("Día 2: se comen", StringComparison.Ordinal));
            var offer = c.Offers.First((o) => !IsStory(o));
            Run(c, new ChooseMissionCommand { Id = offer.Id }, 0, new[] { 0 });
            var mission = MissionFor(c, offer);
            mission.Outcome = "victory";
            Campaign.ApplyMissionResult(c, mission, new Rng(4));
            Assert.Equal(2 + Campaign.DaysPerMission, c.Day);
        }

        [Fact(DisplayName = "time › wounds heal day by day; the enemy project ends the campaign at its maximum")]
        public void WoundsHealDayByDay()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            c.NextEventDay = 999;
            c.Soldiers[0].WoundedDays = 2;
            Campaign.AdvanceTime(c, new Rng(7));
            Assert.True(c.Soldiers[0].WoundedDays < 2);
            c.Doom = Campaign.DoomMax - 1;
            for (var i = 0; i < 10 && c.Outcome == null; i++) Campaign.AdvanceTime(c, new Rng(7 + i));
            Assert.Equal("defeat", c.Outcome);
        }

        // ------------------------------------------------------------------ missions

        /// <summary>Two commanders with a squad of six (La Lata a furgoneta: 18/10 #45), three each.</summary>
        private static (CampaignState C, MissionOffer Offer, List<CampaignSoldier> Mine, List<CampaignSoldier> Theirs) Ready()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(2));
            c.Vehicle = 1;
            var offer = c.Offers[0];
            Run(c, new ChooseMissionCommand { Id = offer.Id }, 0, new[] { 0 });
            // Start from an empty squad to pick it by hand (it comes filled in: see "default squad").
            c.Squad = new List<string>();
            var mine = c.Soldiers.Where((s) => s.Owner == 0).ToList();
            var theirs = c.Soldiers.Where((s) => s.Owner == 1).ToList();
            foreach (var s in mine.Take(3)) Run(c, new SquadCommand { Soldier = s.Id, On = true }, 0);
            foreach (var s in theirs.Take(3)) Run(c, new SquadCommand { Soldier = s.Id, On = true }, 1);
            return (c, offer, mine, theirs);
        }

        [Fact(DisplayName = "missions › each player brings at most three of their own soldiers")]
        public void EachPlayerBringsAtMostThree()
        {
            var (c, _, mine, theirs) = Ready();
            Assert.Equal(6, c.Squad.Count);
            Assert.False(CampaignHelpers.Run(c, new SquadCommand { Soldier = mine[3].Id, On = true }, 0, Both, 1).Ok);
            Assert.False(CampaignHelpers.Run(c, new SquadCommand { Soldier = theirs[3].Id, On = true }, 0, Both, 1).Ok);
        }

        [Fact(DisplayName = "missions › each their own part: a part the other leaves empty stays empty, and nobody touches the other's (Arturo, 18/10 #43)")]
        public void EachTheirOwnPart()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(2));
            c.Vehicle = 1;
            var theirs = c.Soldiers.Where((s) => s.Owner == 1).ToList();
            // Player 1 has a single soldier fit to fight.
            foreach (var s in theirs.Skip(1)) s.WoundedDays = 5;
            Run(c, new ChooseMissionCommand { Id = c.Offers[0].Id }, 0, Both);
            Run(c, new VoteCommand { Accept = true }, 1, Both);
            int Count(int slot) => c.Squad.Count((id) => c.Soldiers.First((s) => s.Id == id).Owner == slot);
            Assert.Equal(new[] { 3, 1 }, new[] { Count(0), Count(1) });
            var outside = c.Soldiers.First((s) => s.Owner == 0 && !c.Squad.Contains(s.Id) && s.WoundedDays == 0);
            Throws("Máximo 3 soldados por jugador.", c, new SquadCommand { Soldier = outside.Id, On = true }, 0, Both);
            // The other's soldiers are theirs, here or not.
            Throws("Solo puedes elegir a tus soldados.", c, new SquadCommand { Soldier = theirs[0].Id, On = false }, 0, Both);
            Throws("Solo puedes elegir a tus soldados.", c, new SquadCommand { Soldier = theirs[0].Id, On = false }, 0, new[] { 0 });
            // Alone in the room, one fills La Lata with their own.
            Run(c, new SquadCommand { Soldier = outside.Id, On = true }, 0, new[] { 0 });
            Assert.Equal(4, Count(0));
        }

        [Fact(DisplayName = "missions › La Lata takes as many as its vehicle: 4, 6, 7 and 8, half each when both are here (18/10 #45)")]
        public void LaLataTakesAsManyAsItsVehicle()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(2));
            Assert.Equal(0, c.Vehicle);
            var seats = new[] { 0, 1, 2, 3 }.Select((v) =>
            {
                c.Vehicle = v;
                return new[] { Campaign.SquadMax(c), Campaign.SquadLimit(c, Both), Campaign.SquadLimit(c, new[] { 0 }) };
            }).ToList();
            Assert.Equal(new[] { new[] { 4, 2, 4 }, new[] { 6, 3, 6 }, new[] { 7, 4, 7 }, new[] { 8, 4, 8 } }, seats);
        }

        [Fact(DisplayName = "missions › a better vehicle turns up now and then; won, La Lata is it (18/10 #45)")]
        public void ABetterVehicleTurnsUpNowAndThen()
        {
            MissionOffer? found = null;
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            for (var seed = 1; seed <= 40 && found == null; seed++)
            {
                c = Campaign.CreateCampaign(new[] { 0 }, new Rng(seed));
                c.NextEventDay = 999;
                c.NextRumorDay = 999;
                c.MissionsFlown = 2;
                c.Offers = new List<MissionOffer>();
                for (var i = 0; i < 12 && found == null; i++)
                {
                    Campaign.AdvanceTime(c, new Rng(seed * 100 + i));
                    c.Event = null;
                    found = c.Offers.FirstOrDefault((o) => (o.Reward.Vehicle ?? 0) != 0);
                    // Room for more operations.
                    if (found == null) c.Offers = new List<MissionOffer>();
                }
            }
            Assert.NotNull(found);
            Assert.Equal("elimination", found!.Kind);
            Assert.Equal(1, found.Reward.Vehicle);
            Assert.Matches("^La furgoneta ", found.Name);
            Assert.Matches("una furgoneta", Campaign.OfferBriefing(c, found));
            // Not before its missions, and one at a time.
            var early = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            early.NextEventDay = 999;
            early.NextRumorDay = 999;
            early.Offers = new List<MissionOffer>();
            for (var i = 0; i < 30; i++)
            {
                Campaign.AdvanceTime(early, new Rng(i));
                early.Event = null;
                Assert.DoesNotContain(early.Offers, (o) => (o.Reward.Vehicle ?? 0) != 0);
                early.Offers = new List<MissionOffer>();
            }
            // Won: La Lata grows, and the squad with it.
            Run(c, new ChooseMissionCommand { Id = found.Id }, 0, new[] { 0 });
            var s = Setup.CreateMission(new MissionOptions { Map = found.Map, Slots = new List<int> { 0 }, Rng = new Rng(3), Kind = found.Kind, Pods = found.Pods });
            s.Outcome = "victory";
            Campaign.ApplyMissionResult(c, s, new Rng(4));
            Assert.Equal(1, c.Vehicle);
            Assert.Equal(6, Campaign.SquadMax(c));
            Assert.Contains(c.Log, (l) => l.Text == "La Lata ya es una furgoneta: caben seis.");
        }

        [Fact(DisplayName = "missions › a campaign from before 22 keeps its six seats, and what stood on the Bastion's lot comes down with its cost back")]
        public void ACampaignFromBefore22KeepsItsSixSeats()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var lot = Base.BastionLot(c.Bastion)!;
            var old = Copy(c);
            old.Version = 21;
            old.Vehicle = null;
            var credits = old.Credits;
            Base.PlacePiece(old.Bastion, "sandbags", lot.X, lot.Y, 0);
            var now = Campaign.MigrateCampaign(old);
            Assert.Equal(1, now.Vehicle);
            Assert.Equal(new[] { "caravan" }, now.Bastion.Pieces.Select((p) => p.Piece));
            Assert.Equal(credits + Base.Pieces["sandbags"].Cost.Supplies, now.Credits);
            Assert.Equal(1, now.Bastion.Level);
        }

        [Fact(DisplayName = "missions › a base built on the map of 40 grows to 56: in the middle, at the size it was built, pines round it (docs/MAPAS.md phase 6)")]
        public void ABaseBuiltOnTheMapOf40GrowsTo56()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(6));
            var old = Copy(c);
            old.Version = 22;
            old.Bastion = Base.FreshBase(new Rng(6), 40);
            var caravan = old.Bastion.Pieces[0];
            // A barracón as it was built then (4 × 3), and sandbags where the Bastion's lot now reaches.
            var hut = new BasePiece { Id = "b9", Piece = "quarters", X = caravan.X + 9, Y = caravan.Y, Rot = 0, Hp = 30, Level = 1 };
            var bags = new BasePiece { Id = "b10", Piece = "sandbags", X = caravan.X - 2, Y = caravan.Y + 1, Rot = 0, Hp = 15 };
            old.Bastion.Pieces.Add(hut);
            old.Bastion.Pieces.Add(bags);
            old.Bastion.Trees = old.Bastion.Trees.Where((i) => !Enumerable.Range(0, 4).Any((dx) => new[] { 0, 1, 2 }.Any((dy) => i == (hut.Y + dy) * 40 + hut.X + dx))).ToList();
            old.SiegePosts = new Dictionary<string, Vec2> { ["s1"] = new Vec2 { X = 5, Y = 6 } };
            var credits = old.Credits;
            // C# migrates the campaign where it is (TypeScript, a copy): where the barracón stood, before it moves.
            var (hutX, hutY) = (hut.X, hut.Y);
            var now = Campaign.MigrateCampaign(old);
            var @base = now.Bastion;
            Assert.Equal(new[] { Base.BaseMap, Base.BaseMap }, new[] { @base.Width, @base.Height });
            var kept = @base.Pieces.First((p) => p.Id == "b9");
            Assert.Equal(hutX + 8, kept.X);
            Assert.Equal(hutY + 8, kept.Y);
            Assert.Equal(new[] { 4, 3 }, kept.Size);
            Assert.Equal(12, Base.CellsOf(kept).Count);
            // What stood on the bigger lot comes down, all its cost back.
            Assert.DoesNotContain(@base.Pieces, (p) => p.Id == "b10");
            Assert.Equal(credits + Base.Pieces["sandbags"].Cost.Supplies, now.Credits);
            Same(new Dictionary<string, Vec2> { ["s1"] = new Vec2 { X = 13, Y = 14 } }, now.SiegePosts);
            // Pines round the old ground, none on what is built nor on the tracks; the caravan still in the middle.
            Assert.True(@base.Trees.Count((i) => i % Base.BaseMap < 8) > 100);
            foreach (var p in @base.Pieces) foreach (var t in Base.CellsOf(p)) Assert.False(Base.TreeAt(@base, t.X, t.Y));
            foreach (var i in @base.Trees) Assert.False(Base.OnTrack(Base.BaseMap, Base.BaseMap, i % Base.BaseMap, i / Base.BaseMap));
            var lot = Base.BastionLot(@base)!;
            Assert.True(Math.Abs(lot.X + lot.W / 2.0 - Base.BaseMap / 2.0) < 4);
            // Raised, it takes its size if it fits where it stands.
            now.Credits = 1000;
            now.Level = 3;
            for (var y = kept.Y; y < kept.Y + 6; y++)
            {
                for (var x = kept.X; x < kept.X + 6; x++)
                {
                    var tile = y * Base.BaseMap + x;
                    @base.Trees = @base.Trees.Where((i) => i != tile).ToList();
                }
            }
            Run(now, new RaiseCommand { Piece = kept.Id }, 0, new[] { 0 });
            Assert.Null(kept.Size);
            Assert.Equal(30, Base.CellsOf(kept).Count);
        }

        [Fact(DisplayName = "missions › a fallen soldier leaves its backpack, weapons and whole belt where it fell; brought home, its weapon carries on (18/10 #49)")]
        public void AFallenSoldierLeavesItsBackpackWeaponsAndBelt()
        {
            var (c, offer, _, _) = Ready();
            var dead = c.Soldiers.First((s) => c.Squad.Contains(s.Id));
            c.FoundWeapons = (c.FoundWeapons ?? new List<FoundWeapon>()).Append(new FoundWeapon { Id = "w99", Kind = "assaultRifle", Holder = dead.Id }).ToList();
            dead.Loadout.Utility = "firstAid";
            var s = MissionFor(c, offer);
            var u = s.Units.First((x) => x.CampaignId == dead.Id);
            u.Backpack = new List<Loot> { new Loot { Supplies = 5 } };
            Events.ApplyEvent(s, new DiedEvent { Unit = u.Id });
            var left = s.Dropped!.Where((d) => d.From == u.Id).ToList();
            Assert.True(left.All((d) => d.Pos.X == u.Pos.X && d.Pos.Y == u.Pos.Y));
            Assert.Equal(3, left.Count);
            Same(new Loot { Supplies = 5 }, left[0].Loot);
            Assert.Equal("assaultRifle", left[1].Loot.Weapon);
            Assert.Equal("w99", left[1].Loot.WeaponId);
            Same(new Loot { Item = "firstAid" }, left[2].Loot);
            Assert.NotNull(u.Backpack);
            Assert.Empty(u.Backpack);
            // Another picks the rifle up and comes home with it: it stays in the Armería, theirs.
            var other = s.Units.First((x) => x.Team == "xcom" && x.Alive && !string.IsNullOrEmpty(x.CampaignId));
            Events.ApplyEvent(s, new LootTakenEvent { Unit = other.Id, Id = left[1].Id });
            s.Outcome = "victory";
            Campaign.ApplyMissionResult(c, s, new Rng(1));
            Assert.Contains(c.FoundWeapons!, (w) => w.Id == "w99");
            Assert.DoesNotContain(c.Log, (l) => Regex.IsMatch(l.Text, "se pierde su fusil"));
        }

        [Fact(DisplayName = "missions › skills are only for their own commander, the other here or not (18/10 #42)")]
        public void SkillsAreOnlyForTheirOwnCommander()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(2));
            var theirs = c.Soldiers.First((s) => s.Owner == 1 && Bars.PointsLeft(s.Skills, s.Board) > 0);
            var cell = Board.Cells.Select((x) => x.Id).First((id) => Bars.UnlockBlocker(theirs.Skills, theirs.Board, id) == null);
            foreach (var connected in new[] { Both, new[] { 0 } })
            {
                Throws("Solo puedes elegir las habilidades de tus soldados.", c, new UnlockCommand { Soldier = theirs.Id, Cell = cell }, 0, connected);
            }
            Run(c, new UnlockCommand { Soldier = theirs.Id, Cell = cell }, 1, Both);
            Assert.Equal(new[] { cell }, theirs.Board);
        }

        // Playtest 2026-10-13: a new campaign opens with a mission straight away.
        [Fact(DisplayName = "missions › a new campaign opens with the first story operation chosen and the squad in the hangar")]
        public void ANewCampaignOpensWithTheFirstStoryOperationChosen()
        {
            var plain = Campaign.CreateCampaign(Both, new Rng(1));
            // On its own the campaign chooses nothing (older saves, tests): the server opens it.
            Assert.Null(plain.ActiveMission);
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            Campaign.OpeningMission(c);
            var first = c.Offers.FirstOrDefault((o) => o.Story == 1);
            Assert.NotNull(first);
            Assert.Equal(first!.Id, c.ActiveMission);
            Assert.Equal(Campaign.DefaultSquad(c, Both), c.Squad);
            Assert.True(c.Squad.Count > 0);
            Assert.Equal(new[] { false, false }, c.LaunchReady);
            Assert.Equal(c.Squad.OrderBy((x) => x, StringComparer.Ordinal), (c.Pads ?? new Dictionary<string, int>()).Keys.OrderBy((x) => x, StringComparer.Ordinal));
            Assert.Contains(c.Log, (l) => Regex.IsMatch(l.Text, "Primera salida: .* La escuadra espera junto a La Lata"));
            Assert.DoesNotContain(c.Log, (l) => Regex.IsMatch(l.Text, "Elegid la primera misión"));
            Assert.NotEqual("Elige una misión", Campaign.NextSteps(c, 0).FirstOrDefault()?.Label);
            // Nothing else changes: the same offers, soldiers and day as a campaign opened by hand.
            Same(plain.Offers, c.Offers);
            Same(plain.Soldiers, c.Soldiers);
            Assert.Equal(plain.Day, c.Day);
            // Ready to go: both press launch. (`launch` undefined in TypeScript is false here.)
            Assert.False(Run(c, new LaunchCommand { Ready = true }, 0).Launch);
            Assert.True(Run(c, new LaunchCommand { Ready = true }, 1).Launch);
            // A campaign that already has a mission chosen is left as it is.
            var other = c.Offers.First((o) => o.Id != first.Id);
            var later = Campaign.CreateCampaign(Both, new Rng(1));
            later.ActiveMission = other.Id;
            Campaign.OpeningMission(later);
            Assert.Equal(other.Id, later.ActiveMission);
        }

        [Fact(DisplayName = "missions › the opening mission can be swapped for another; choosing the same one again is refused")]
        public void TheOpeningMissionCanBeSwapped()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            Campaign.OpeningMission(c);
            var first = c.ActiveMission!;
            var other = c.Offers.First((o) => o.Id != first);
            Throws("Esa misión ya está elegida.", c, new ChooseMissionCommand { Id = first }, 0);
            Run(c, new ChooseMissionCommand { Id = other.Id }, 0);
            Run(c, new VoteCommand { Accept = true }, 1);
            Assert.Equal(other.Id, c.ActiveMission);
        }

        [Fact(DisplayName = "missions › launches only when both are ready")]
        public void LaunchesOnlyWhenBothAreReady()
        {
            var (c, _, _, _) = Ready();
            // (`launch` undefined in TypeScript is false here.)
            Assert.False(Run(c, new LaunchCommand { Ready = true }, 0).Launch);
            Assert.True(Run(c, new LaunchCommand { Ready = true }, 1).Launch);
        }

        [Fact(DisplayName = "missions › results come back: deaths, wounds, what each one learnt and rewards")]
        public void ResultsComeBack()
        {
            var (c, offer, _, _) = Ready();
            // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
            c.Siege.In = 99;
            var squad = Campaign.BuildSquad(c);
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = Both.ToList(), Rng = new Rng(3), Kind = offer.Kind, Squad = squad, Pods = offer.Pods, Difficulty = offer.Difficulty });
            var soldiers = mission.Units.Where((u) => u.Team == "xcom").ToList();
            soldiers[0].Alive = false;
            soldiers[0].Hp = 0;
            soldiers[0].Deeds = new Deeds { ["hit"] = 4 };
            soldiers[1].Hp = soldiers[1].MaxHp - 2;
            soldiers[2].Kills = 6;
            soldiers[2].Deeds = new Deeds { ["shot"] = 7, ["hit"] = 5, ["gunKill"] = 6, ["healOther"] = 1 };
            mission.Outcome = "victory";
            mission.OutcomeReason = "objective";
            var credits = c.Credits;
            var veteran = c.Soldiers.First((s) => s.Id == soldiers[2].CampaignId);
            var before = new Dictionary<string, int>(veteran.Skills);
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            Assert.True(report.Victory);
            Assert.Equal(6, report.Squad.Count);
            Assert.Equal("dead", report.Squad.FirstOrDefault((e) => e.Id == soldiers[0].CampaignId)?.Status);
            var hurt = report.Squad.First((e) => e.Id == soldiers[1].CampaignId);
            Assert.Equal("wounded", hurt.Status);
            Assert.Equal(2, hurt.WoundedDays);
            // Eighteen of Disparo, all of them (no cap: Arturo, 16/10).
            var best = report.Squad.First((e) => e.Id == soldiers[2].CampaignId);
            Assert.Equal(6, best.Kills);
            Has(SkillsWith(("shooting", 18), ("medicine", 2)), best.Learned);
            Has(new Deeds { ["shot"] = 7, ["hit"] = 5, ["gunKill"] = 6, ["healOther"] = 1 }, best.Deeds);
            Assert.False(c.Soldiers.First((s) => s.Id == soldiers[0].CampaignId).Alive);
            // The report says how bad it was, and a mission takes no days (docs/PRUEBA-2026-10-14.md P7): it is all still to heal.
            Assert.Equal(2, c.Soldiers.First((s) => s.Id == soldiers[1].CampaignId).WoundedDays);
            Assert.Equal(report.Day, c.Day);
            var after = new Dictionary<string, int>(before) { ["shooting"] = before["shooting"] + 18, ["medicine"] = before["medicine"] + 2 };
            Same(after, veteran.Skills);
            Assert.Equal(credits + (offer.Reward.Credits ?? 0) + report.Salvage, c.Credits);
            // The Bastion's level is bought now (18/10 #40): a mission brings it no experience.
            Assert.Equal(0, report.Xp);
            Assert.Equal(0, c.Xp);
            Assert.DoesNotContain(c.Offers, (o) => o.Id == offer.Id);
            Assert.Null(c.ActiveMission);
        }

        [Fact(DisplayName = "missions › nobody has a class: every recruit comes with a bar or two, and a bar filled is told with what it added (docs/FASE10.md)")]
        public void NobodyHasAClass()
        {
            var (c, offer, _, _) = Ready();
            // TypeScript also checks no soldier has a `cls`: CampaignSoldier has no such field.
            Assert.True(c.Soldiers.All((s) => s.Board.Count == 0));
            Assert.True(c.Soldiers.All((s) => Bars.TotalBars(s.Skills) >= 1 && Bars.TotalBars(s.Skills) <= 2));
            var mission = MissionFor(c, offer);
            var unit = mission.Units.First((u) => u.Team == "xcom");
            var s = c.Soldiers.First((x) => x.Id == unit.CampaignId);
            s.Skills = SkillsWith(("shooting", 22));
            s.Growth = GrowthWith(("shooting", 7));
            unit.Lines = Bars.MissionLinesOf(s.Skills, s.Growth);
            unit.Deeds = new Deeds { ["shot"] = 2, ["hit"] = 1 };
            mission.Outcome = "victory";
            mission.OutcomeReason = "objective";
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            var bar = report.Squad.First((e) => e.Id == s.Id).Bars!;
            Same(new List<BarFilled> { new BarFilled { Skill = "shooting", Filled = 2, Growth = new List<int> { s.Growth["shooting"] - 7 } } }, bar);
            Assert.Contains(c.Log, (l) => l.Text == $"{s.Name} llena una barra de Disparo: +{s.Growth["shooting"] - 7} % de puntería con armas de fuego. Tiene un punto de habilidad para el tablero.");
            // Two points: a cell of the first ring, then one of the second beside it (Disparo has the bar it asks for).
            Assert.True(Campaign.CanSpendPoints(c, s));
            Run(c, new UnlockCommand { Soldier = s.Id, Cell = "steadyHand" }, s.Owner);
            Run(c, new UnlockCommand { Soldier = s.Id, Cell = "lightTrigger" }, s.Owner);
            Assert.Equal(new[] { "steadyHand", "lightTrigger" }, s.Board);
            Assert.Equal($"{s.Name} aprende Dedo ligero.", c.Log[0].Text);
            Assert.False(Campaign.CanSpendPoints(c, s));
            Throws("No le quedan puntos", c, new UnlockCommand { Soldier = s.Id, Cell = "sidearm" }, s.Owner);
            Throws("al lado", c, new UnlockCommand { Soldier = s.Id, Cell = "killZone" }, s.Owner);
        }

        [Fact(DisplayName = "missions › it is winter until the story says so: the missions go under snow (playtest 16/10)")]
        public void ItIsWinterUntilTheStorySaysSo()
        {
            var (c, offer, _, _) = Ready();
            Assert.True(Campaign.MissionInfo(c, offer).Snow);
            c.Story.Done = Campaign.ThawAfterStory;
            Assert.Null(Campaign.MissionInfo(c, offer).Snow);
            Assert.True(Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = Both.ToList(), Rng = new Rng(3), Kind = offer.Kind, Snow = true }).Snow);
        }

        [Fact(DisplayName = "missions › the Taller de oficios makes a mission teach 20 % more for every level")]
        public void TheTallerDeOficiosMakesAMissionTeachMore()
        {
            var (c, offer, _, _) = Ready();
            Assert.Equal(0, Campaign.LearningBonus(c));
            CampaignHelpers.Raise(c, "echoes", 2);
            // toBeCloseTo(0.4): two digits.
            Assert.True(Math.Abs(Campaign.LearningBonus(c) - 0.4) < 0.005, $"{Campaign.LearningBonus(c)}");
            var mission = MissionFor(c, offer);
            var unit = mission.Units.First((u) => u.Team == "xcom");
            unit.Deeds = new Deeds { ["shot"] = 12 };
            mission.Outcome = "victory";
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            Assert.Equal(12 + 5, report.Squad.First((e) => e.Id == unit.CampaignId).Learned!["shooting"]);
        }

        [Fact(DisplayName = "missions › the Coro sends a little more with every few missions flown")]
        public void TheCoroSendsALittleMore()
        {
            var (c, offer, _, _) = Ready();
            Assert.Equal(0, Campaign.VeteranEnemies(c));
            var mission = MissionFor(c, offer);
            mission.Outcome = "victory";
            Campaign.ApplyMissionResult(c, mission, new Rng(4));
            Assert.Equal(1, c.MissionsFlown);
            c.MissionsFlown = 4;
            Assert.Equal(1, Campaign.VeteranEnemies(c));
            c.MissionsFlown = 40;
            Assert.Equal(2, Campaign.VeteranEnemies(c));
        }

        [Fact(DisplayName = "missions › bars, what they added, the board, veterancy and the armour the Taller made reach the battlefield")]
        public void BarsTheBoardVeterancyAndArmourReachTheBattlefield()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(5));
            var s = c.Soldiers[0];
            // Three bars of Disparo and two of Cuerpo a cuerpo: five points; four spent, still Novato.
            s.Skills = SkillsWith(("shooting", Bars.BarsCost(3)), ("melee", Bars.BarsCost(2)));
            s.Growth = GrowthWith(("shooting", 25), ("melee", 20));
            foreach (var cell in new[] { "steadyHand", "lightTrigger", "aimedShot", "grip" }) Run(c, new UnlockCommand { Soldier = s.Id, Cell = cell }, 0, new[] { 0 });
            // Plate armour made by the Taller (docs/FASE4.md): everyone wears it, whatever their veterancy.
            c.Level = 4;
            c.Crafted = new List<string> { "armor2" };
            var member = Campaign.SquadMember(c, s);
            Assert.Equal(1, member.WeaponTier);
            Assert.Equal(2, member.ArmorTier);
            Assert.Equal(0, member.Rank);
            Assert.Equal(new[] { "aimedShot" }, member.Perks);
            // 60 grown 25 % is 75, and +3 of Pulso firme; 60 grown 20 % is 72, and +3 of Buen agarre.
            Assert.Equal(18, member.Mods.GunAim);
            Assert.Equal(5, member.Mods.GunCrit);
            Assert.Equal(15, member.Mods.MeleeAim);
            Assert.Equal(0, member.Mods.Damage);
            Assert.Equal(5 + 2, member.MaxHp);
            Assert.Equal(Bars.VeteranWill(0), member.Will);

            var mission = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(1), Squad = new List<SquadMember> { member } });
            var shooter = mission.Units.First((u) => u.CampaignId == s.Id);
            // a3 belongs to the central pod, standing in the open.
            var target = mission.Units.First((u) => u.Id == "a3");
            shooter.Pos = new Pos { X = target.Pos.X, Y = target.Pos.Y + 5, Z = 0 };
            var shot = Aim.PreviewShot(mission, shooter, target)!;
            Assert.Equal("Puntería", shot.Mods[0].Label);
            Assert.Equal(Bars.BaseAim, shot.Mods[0].Value);
            Assert.Contains(shot.Mods, (m) => m.Label == "Tiro" && m.Value == 18);
            Assert.Contains("aimedShot", Abilities.AbilitiesOf(shooter));
            // Nobody carries grenades without them on the belt.
            Assert.DoesNotContain("grenade", Abilities.AbilitiesOf(shooter));
        }

        // --------------------------------------------------------------- progression

        [Fact(DisplayName = "progression › weapons have no tiers; the armour the Taller makes is for everyone (docs/FASE9.md §5)")]
        public void WeaponsHaveNoTiers()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var s = new CampaignSoldier { Id = "x" };
            c.Level = 6;
            c.Crafted = new List<string> { "armor2" };
            // weaponTier(c, s) reads neither: C#'s takes nothing.
            Assert.Equal(1, Progression.WeaponTier());
            Assert.Null(Progression.NextTierBlocker(c, s, "weapon"));
            Assert.Equal(2, Progression.ArmorTier(c, s));
            Assert.Matches("requiere la casa y", Progression.NextTierBlocker(c, s, "armor"));
        }

        [Fact(DisplayName = "progression › the Bastion goes up a level when it is bought, after enough missions (Arturo, 18/10 #40)")]
        public void TheBastionGoesUpALevelWhenItIsBought()
        {
            var c = Campaign.CreateCampaign(new[] { 0, 1 }, new Rng(1));
            Assert.Equal(1, Progression.BastionLevel(c));
            var next = Campaign.NextLevel(c);
            Assert.NotNull(next);
            Assert.Equal((2, Progression.LevelCost[1], Progression.LevelMissions[1], 0), (next!.Level, next.Cost, next.Missions, next.Flown));
            c.Credits = 5000;
            Throws($"Para la autocaravana con toldo hacen falta {Progression.LevelMissions[1]} misiones hechas: lleváis 0.", c, new LevelUpCommand(), 0, new[] { 0, 1 });
            c.MissionsFlown = Progression.LevelMissions[1];
            // Always together: a proposal, paid when the partner says yes.
            Run(c, new LevelUpCommand(), 0, new[] { 0, 1 });
            Assert.Equal("subir el Bastión a la autocaravana con toldo", c.Proposal?.Text);
            Run(c, new VoteCommand { Accept = true }, 1, new[] { 0, 1 });
            Assert.Equal(2, Progression.BastionLevel(c));
            Assert.Equal(5000 - Progression.LevelCost[1], c.Credits);
            Assert.Contains(c.Log, (l) => l.Text == "El Bastión ya es una autocaravana con toldo.");
            // Short of suministros, it waits.
            c.MissionsFlown = 99;
            c.Credits = Progression.LevelCost[2] - 1;
            Assert.Matches("suministros", Campaign.LevelUpBlocker(c));
            c.Level = Progression.MaxLevel;
            Assert.Null(Campaign.NextLevel(c));
            Assert.Equal("El Bastión ya está en su nivel más alto.", Campaign.LevelUpBlocker(c));
        }

        [Fact(DisplayName = "progression › a campaign from before keeps its level, and its operations stop expiring")]
        public void ACampaignFromBeforeKeepsItsLevel()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var old = Copy(c);
            old.Version = 20;
            old.Xp = 55;
            old.Level = null;
            old.Offers[0].ExpiresDay = old.Day + 4;
            var now = Campaign.MigrateCampaign(old);
            Assert.Equal(4, Progression.BastionLevel(now));
            Assert.True(now.Offers.All((o) => o.ExpiresDay == null));
        }

        [Fact(DisplayName = "progression › buying the top level with every story chapter won reveals the final mission")]
        public void BuyingTheTopLevelRevealsTheFinalMission()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            c.Story.Done = 7;
            c.Level = Progression.MaxLevel - 1;
            c.Credits = 5000;
            c.MissionsFlown = 99;
            Run(c, new LevelUpCommand(), 0, new[] { 0 });
            Assert.Equal(Progression.MaxLevel, Progression.BastionLevel(c));
            Assert.Contains(c.Offers, (o) => o.Final);
        }

        // ------------------------------------------------------- helping the players

        [Fact(DisplayName = "helping the players › a chosen mission comes with its squad: the most seasoned ready soldiers first")]
        public void AChosenMissionComesWithItsSquad()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(2));
            c.Vehicle = 1;
            var mine = c.Soldiers.Where((s) => s.Owner == 0).ToList();
            var veteran = mine[mine.Count - 1];
            veteran.Skills = SkillsWith(("shooting", Bars.BarsCost(4)), ("melee", Bars.BarsCost(4)));
            c.Soldiers.First((s) => s.Owner == 1).WoundedDays = 3;
            Run(c, new ChooseMissionCommand { Id = c.Offers[0].Id }, 0, new[] { 0, 1 });
            Run(c, new VoteCommand { Accept = true }, 1, new[] { 0, 1 });
            var picked = c.Squad.Select((id) => c.Soldiers.First((s) => s.Id == id)).ToList();
            Assert.Equal(3, picked.Count((s) => s.Owner == 0));
            Assert.Equal(3, picked.Count((s) => s.Owner == 1));
            Assert.Contains(veteran, picked);
            Assert.True(picked.All((s) => s.WoundedDays == 0));
            Assert.Same(veteran, picked[0]);
            // Adding someone already in it is fine; swapping is out then in.
            Run(c, new SquadCommand { Soldier = veteran.Id, On = true }, 0, new[] { 0, 1 });
            Assert.Equal(6, c.Squad.Count);
        }

        [Fact(DisplayName = "helping the players › in the hangar each commander has a half, and nobody moves when others come and go")]
        public void InTheHangarEachCommanderHasAHalf()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(2));
            c.Vehicle = 1;
            Run(c, new ChooseMissionCommand { Id = c.Offers[0].Id }, 0);
            Run(c, new VoteCommand { Accept = true }, 1);
            int? Owner(string? id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)?.Owner;
            var before = Campaign.SquadPads(c);
            Assert.Equal(new int?[] { 0, 0, 0, 1, 1, 1 }, before.Select(Owner));
            // The first commander takes out their second soldier: that pad is left free, the rest stay put.
            Run(c, new SquadCommand { Soldier = before[1]!, On = false }, 0);
            Assert.Equal(new[] { before[0], null, before[2], before[3], before[4], before[5] }, Campaign.SquadPads(c));
            // The partner swaps one of theirs: the newcomer takes the pad that was freed in their half.
            var spare = c.Soldiers.First((s) => s.Owner == 1 && !c.Squad.Contains(s.Id));
            Run(c, new SquadCommand { Soldier = before[4]!, On = false }, 1);
            Run(c, new SquadCommand { Soldier = spare.Id, On = true }, 1);
            Assert.Equal(new[] { before[0], null, before[2], before[3], spare.Id, before[5] }, Campaign.SquadPads(c));
        }

        [Fact(DisplayName = "helping the players › a pad kept on the other one's half is let go: each stands among their own (Ana, 23/10 #19)")]
        public void APadKeptOnTheOtherOnesHalfIsLetGo()
        {
            Func<CampaignState, Func<string?, int?>> owner = (k) => (id) => k.Soldiers.FirstOrDefault((s) => s.Id == id)?.Owner;
            // A squad picked across both halves (the partner away) stays where it stood, then each goes to their own half.
            var c = Campaign.CreateCampaign(Both, new Rng(2));
            c.Vehicle = 1;
            Run(c, new ChooseMissionCommand { Id = c.Offers[0].Id }, 0);
            Run(c, new VoteCommand { Accept = true }, 1);
            var ids = Campaign.SquadPads(c);
            c.Pads = ids.Select((id, i) => (id, i)).ToDictionary((p) => p.id!, (p) => (p.i + 3) % 6);
            Assert.Equal(new int?[] { 0, 0, 0, 1, 1, 1 }, Campaign.SquadPads(c).Select(owner(c)));
            // Alone, the whole hangar; then a partner arrives, takes over rookies, and the squad is picked again.
            var d = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            d.Vehicle = 1;
            Run(d, new ChooseMissionCommand { Id = d.Offers[0].Id }, 0, new[] { 0 });
            Campaign.SyncPads(d);
            Assert.True(Campaign.SquadPads(d).All((id) => owner(d)(id) == 0));
            Campaign.AddPlayer(d, 1, new Rng(1));
            // Half each (more than half, and the rest would stand on the other half, as ever).
            foreach (var id in d.Squad.Where((x) => owner(d)(x) == 0).Skip(3).ToList()) Run(d, new SquadCommand { Soldier = id, On = false }, 0);
            foreach (var s in d.Soldiers.Where((x) => x.Owner == 1 && !d.Squad.Contains(x.Id)).Take(2).ToList()) Run(d, new SquadCommand { Soldier = s.Id, On = true }, 1);
            var pads = Campaign.SquadPads(d);
            for (var i = 0; i < pads.Count; i++)
            {
                if (pads[i] != null) Assert.True(owner(d)(pads[i]) == (i < 3 ? 0 : 1), $"pad {i}");
            }
            Assert.Equal(2, pads.Count((id) => owner(d)(id) == 1));
        }

        [Fact(DisplayName = "helping the players › alone, a commander keeps both kits; a partner who arrives later takes over the untouched rookies")]
        public void AloneACommanderKeepsBothKits()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            Assert.Equal(12, Campaign.SoldierCap(c));
            var veteran = c.Soldiers[0];
            veteran.Missions = 2;
            Assert.Equal(4, Campaign.AddPlayer(c, 1, new Rng(1)));
            Assert.Equal(new[] { 0, 1 }, c.Players);
            Assert.Equal(4, c.Soldiers.Count((s) => s.Owner == 1));
            Assert.Equal(0, veteran.Owner);
            Assert.Equal(6, Campaign.SoldierCap(c));
            Assert.Equal(0, Campaign.AddPlayer(c, 1, new Rng(1)));
        }

        [Fact(DisplayName = "helping the players › a partner who arrives never takes the reserves the first commander needs: fresh recruits instead")]
        public void APartnerWhoArrivesNeverTakesTheReserves()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            // After a hard mission only three soldiers are fit, and they have never fought.
            for (var i = 0; i < c.Soldiers.Count; i++) c.Soldiers[i].WoundedDays = i < 5 ? 6 : 0;
            var fit = c.Soldiers.Where((s) => s.WoundedDays == 0).Select((s) => s.Id).ToList();
            Campaign.AddPlayer(c, 1, new Rng(1));
            // The fit three stay; the wounded rookies may change hands.
            Assert.True(c.Soldiers.Where((s) => fit.Contains(s.Id)).All((s) => s.Owner == 0));
            Assert.Equal(Campaign.LateKit, c.Soldiers.Count((s) => s.Owner == 1));
            // TypeScript's «Llega el segundo comandante» is «Se une el otro jugador» in C# (text-fixes.ts).
            Assert.Contains(c.Log, (l) => Regex.IsMatch(l.Text, "Se une el otro jugador"));
            // Three fit rookies are all the first commander has (the veterans are wounded): they stay, and the partner gets recruits.
            var d = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            foreach (var s in d.Soldiers.Skip(3))
            {
                s.Missions = 1;
                s.WoundedDays = 6;
            }
            Campaign.AddPlayer(d, 1, new Rng(1));
            Assert.True(d.Soldiers.Take(8).All((s) => s.Owner == 0));
            Assert.Equal(Campaign.LateKit, d.Soldiers.Count((s) => s.Owner == 1));
            Assert.Contains(d.Log, (l) => Regex.IsMatch(l.Text, "Se alistan a sus órdenes"));
        }

        [Fact(DisplayName = "helping the players › what to do now: the most pressing thing first")]
        public void WhatToDoNow()
        {
            var c = Campaign.CreateCampaign(Both, new Rng(1));
            // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
            c.Siege.In = 99;
            var first = Campaign.NextSteps(c, 0)[0];
            Assert.Equal("geoscape", first.View);
            Assert.Equal("Elige una misión", first.Label);
            Assert.Equal(c.Offers.Count, first.Count);
            // Operations don't expire (18/10 #39).
            Assert.True(c.Offers.All((o) => o.ExpiresDay == null));
            // Every recruit comes with a point or two to spend.
            var promoted = c.Soldiers.First((s) => s.Owner == 1);
            Assert.Contains(Campaign.NextSteps(c, 1), (x) => x.View == "barracks" && x.Go?.Soldier == promoted.Id);
            foreach (var s in c.Soldiers.Where((x) => x.Owner == 0)) s.Board = Bars.RandomBoard(s.Skills, new Rng(1));
            Assert.DoesNotContain(Campaign.NextSteps(c, 0), (x) => x.View == "barracks" && x.Label.StartsWith("Puntos", StringComparison.Ordinal));
            // Equipment only nags before a mission, and only for what can actually be fitted.
            Assert.DoesNotContain(Campaign.NextSteps(c, 0), (x) => x.Label.StartsWith("Equipa", StringComparison.Ordinal));
            Run(c, new ChooseMissionCommand { Id = c.Offers[0].Id }, 0, new[] { 0 });
            Assert.Equal("squad", Campaign.NextSteps(c, 0)[0].View);
            // Starting stock: 2 extra grenades and a medkit, all utility.
            Assert.Equal(3, Campaign.NextSteps(c, 0).FirstOrDefault((x) => x.Label.StartsWith("Equipa", StringComparison.Ordinal))?.Count);
        }

        [Fact(DisplayName = "helping the players › after a bad mission, too few ready soldiers puts recruiting before the next operation")]
        public void AfterABadMissionRecruitingComesFirst()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
            c.Siege.In = 99;
            var mine = c.Soldiers.Where((s) => s.Owner == 0).ToList();
            foreach (var s in mine.Skip(1)) s.WoundedDays = 8;
            // The caravan is full: first a barracón for the recruits to sleep in.
            // (TypeScript's «construye un barracón» is «construye un Barracón» in C#: text-fixes.ts.)
            var step = Campaign.NextSteps(c, 0)[0];
            Assert.Equal("facilities", step.View);
            Assert.Equal("Solo 1 soldado listo y sin camas: construye un Barracón", step.Label);
            CampaignHelpers.Raise(c, "quarters");
            step = Campaign.NextSteps(c, 0)[0];
            Assert.Equal("barracks", step.View);
            Assert.Equal("Solo 1 soldado listo: recluta (30 §)", step.Label);
            // Without the credits (or at the cap) there is nothing to do about it here.
            c.Credits = 0;
            Assert.DoesNotContain(Campaign.NextSteps(c, 0), (x) => x.Label.Contains("recluta"));
        }

        // ----------------------------------------------------------- points to spend

        // Fase 10: every bar filled is a point for the board; the Cuartel asks until they are spent.
        private static List<NextStep> Asks(CampaignState c) => Campaign.NextSteps(c, 0).Where((x) => Regex.IsMatch(x.Label, "^Puntos por gastar")).ToList();

        [Fact(DisplayName = "points to spend › a point asks to be spent, and stops once it is")]
        public void APointAsksToBeSpent()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var mine = c.Soldiers.Where((x) => x.Owner == 0).ToList();
            var s = mine[0];
            foreach (var x in mine) x.Skills = Bars.NoSkills();
            Assert.False(Campaign.CanSpendPoints(c, s));
            Assert.Empty(Asks(c));
            s.Skills["medicine"] = Bars.BarsCost(1);
            var ask = Assert.Single(Asks(c));
            Assert.Equal($"Puntos por gastar: {s.Name}", ask.Label);
            Assert.Equal(1, ask.Count);
            Same(new NextStepGo { Soldier = s.Id }, ask.Go);
            Run(c, new UnlockCommand { Soldier = s.Id, Cell = "bandage" }, 0, new[] { 0 });
            Assert.Empty(Asks(c));
            Throws("No le quedan puntos", c, new UnlockCommand { Soldier = s.Id, Cell = "calmVoice" }, 0, new[] { 0 });
        }
    }
}
