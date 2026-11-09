using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The soldiers' wardrobe (0.12.15; Arturo, 9/11: backpacks, helmets, police kit and street clothes): those born from now
    /// on carry Appearances.Wardrobe, and the client dresses them from the new list; those already there carry none and
    /// keep the clothes and the head they had. Nobody changes it: not the barracks, not a change of owner.
    /// </summary>
    public class WardrobeTests
    {
        private static CampaignResult Run(CampaignState c, CampaignCommand cmd)
        {
            var r = Campaign.ExecuteCampaignCommand(c, cmd, new CampaignContext { Slot = 0, Connected = new[] { 0 }, Rng = new Rng(11) });
            Assert.True(r.Ok, r.Error);
            return r;
        }

        private static Appearance Copy(Appearance a)
        {
            return new Appearance { Primary = a.Primary, Secondary = a.Secondary, Visor = a.Visor, Head = a.Head, Build = a.Build, Sex = a.Sex, Helmet = a.Helmet, Torso = a.Torso, Arms = a.Arms, Legs = a.Legs, Character = a.Character, Wardrobe = a.Wardrobe };
        }

        /// <summary>A campaign's soldiers as they were before 0.12.15: no wardrobe, and the heads of before.</summary>
        private static void AsBefore(CampaignState c)
        {
            foreach (var s in c.Soldiers) s.Appearance = Appearances.DefaultAppearance(s.Owner, null, s.Appearance.Sex, s.Id);
        }

        [Fact]
        public void SoldiersBornNowCarryTheLatestWardrobe()
        {
            var c = Campaign.CreateCampaign(new[] { 0, 1 }, new Rng(4));
            Assert.All(c.Soldiers, (s) => Assert.Equal(Appearances.Wardrobe, s.Appearance.Wardrobe));
            foreach (var s in c.Soldiers.Skip(2)) s.Alive = false;
            Run(c, new RecruitCommand());
            Assert.Equal(Appearances.Wardrobe, c.Soldiers.Last().Appearance.Wardrobe);
            var skirmish = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0, 1 }, Rng = new Rng(1) });
            Assert.All(skirmish.Units.Where((u) => u.Team == Teams.Xcom), (u) => Assert.Equal(Appearances.Wardrobe, u.Appearance?.Wardrobe));
        }

        [Fact]
        public void TheNewHeadgearIsOnlyForSoldiersBornNow()
        {
            var seeds = Enumerable.Range(1, 400).Select((i) => $"s{i}").ToList();
            var before = seeds.Select((id) => Appearances.DefaultAppearance(0, null, Sexes.F, id).Head).ToHashSet();
            var now = seeds.Select((id) => Appearances.DefaultAppearance(0, null, Sexes.F, id, Appearances.Wardrobe).Head).ToHashSet();
            Assert.Equal(new[] { HeadIds.Helmet, HeadIds.Hood, HeadIds.Beret, HeadIds.Cap, HeadIds.Mask, HeadIds.Bare }.OrderBy((h) => h), before.OrderBy((h) => h));
            // Every head but the riot helmet, which is the barracks' only.
            Assert.Equal(HeadIds.All.Where((h) => h != HeadIds.RiotHelmet).OrderBy((h) => h), now.OrderBy((h) => h));
            Assert.All(HeadIds.All, (h) => Assert.True(Appearances.Heads.ContainsKey(h), h));
            Assert.Null(Appearances.DefaultAppearance(0, null, Sexes.F, "s1").Wardrobe);
        }

        [Fact]
        public void TheBarracksNeverChangesIt()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(4));
            var born = c.Soldiers[0];
            var old = c.Soldiers[1];
            old.Appearance.Wardrobe = null;
            // A look without it keeps the soldier's; one with another keeps it too.
            var look = Copy(born.Appearance);
            look.Wardrobe = null;
            look.Head = HeadIds.RiotHelmet;
            Run(c, new CustomizeCommand { Soldier = born.Id, Name = born.Name, Nickname = "", Appearance = look });
            Assert.Equal(Appearances.Wardrobe, born.Appearance.Wardrobe);
            Assert.Equal(HeadIds.RiotHelmet, born.Appearance.Head);
            look = Copy(old.Appearance);
            look.Wardrobe = Appearances.Wardrobe;
            Run(c, new CustomizeCommand { Soldier = old.Id, Name = old.Name, Nickname = "", Appearance = look });
            Assert.Null(old.Appearance.Wardrobe);
            // One the engine doesn't know isn't a look.
            look.Wardrobe = Appearances.Wardrobe + 1;
            Assert.False(Appearances.IsAppearance(look));
            look.Wardrobe = 0;
            Assert.False(Appearances.IsAppearance(look));
        }

        [Fact]
        public void RookiesWhoChangeHandsKeepTheirs()
        {
            // Born before: still the default look of before, so they take the new commander's colours, and no wardrobe.
            var d = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            AsBefore(d);
            Campaign.AddPlayer(d, 1, new Rng(1));
            var passed = d.Soldiers.Where((x) => x.Owner == 1 && x.Appearance.Wardrobe == null).ToList();
            Assert.True(passed.Count > 0, "nobody passed");
            foreach (var s in passed) Assert.Equal(CampaignJsonTests.Write(Appearances.DefaultAppearance(1, null, s.Appearance.Sex, s.Id)), CampaignJsonTests.Write(s.Appearance));
            // Born now: the same with theirs.
            var e = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            Campaign.AddPlayer(e, 1, new Rng(1));
            var mine = e.Soldiers.Where((x) => x.Owner == 1).ToList();
            Assert.True(mine.Count > 0);
            foreach (var s in mine) Assert.Equal(CampaignJsonTests.Write(Appearances.DefaultAppearance(1, null, s.Appearance.Sex, s.Id, Appearances.Wardrobe)), CampaignJsonTests.Write(s.Appearance));
        }

        [Fact]
        public void TheSquadTakesItIntoTheMission()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            c.NextEventDay = 999;
            c.Soldiers[0].Appearance.Wardrobe = null;
            CampaignHelpers.Run(c, new ChooseMissionCommand { Id = c.Offers.First((o) => (o.Story ?? 0) == 0).Id });
            var offer = Campaign.ActiveOffer(c)!;
            var squad = Campaign.BuildSquad(c);
            var s = Setup.CreateMission(new MissionOptions { Map = Campaign.OfferMap(c, offer), Slots = c.Players, Rng = new Rng(2), Kind = offer.Kind, Squad = squad, Pods = offer.Pods });
            foreach (var m in squad)
            {
                var soldier = c.Soldiers.First((x) => x.Id == m.CampaignId);
                var unit = s.Units.First((u) => u.Team == Teams.Xcom && u.Name == m.Name);
                Assert.Equal(soldier.Appearance.Wardrobe, unit.Appearance?.Wardrobe);
            }
        }
    }
}
