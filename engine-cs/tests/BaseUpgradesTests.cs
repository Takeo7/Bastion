using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The base's ten more upgrades (Arturo, cuadros 164 and 165; gameplay CONTRATO.md), the first six (0.14.9): Entrenamiento,
    /// Ruta de vuelta, Motor afinado, Huerto, Radio and Botiquín de salida.
    /// </summary>
    public class BaseUpgradesTests
    {
        private static void Up(CampaignState c, string id, int level) => (c.Upgrades ??= new Dictionary<string, int>())[id] = level;

        [Fact(DisplayName = "mejoras › the six, their levels and prices (cuadro 165)")]
        public void ThePrices()
        {
            Assert.Equal(new[] { 60, 120, 200 }, Content.UpgradePrices[UpgradeIds.Training]);
            Assert.Equal(new[] { 100 }, Content.UpgradePrices[UpgradeIds.Route]);
            Assert.Equal(new[] { 80, 160 }, Content.UpgradePrices[UpgradeIds.Engine]);
            Assert.Equal(new[] { 150, 250 }, Content.UpgradePrices[UpgradeIds.Garden]);
            Assert.Equal(new[] { 120 }, Content.UpgradePrices[UpgradeIds.Radio]);
            Assert.Equal(new[] { 100, 150 }, Content.UpgradePrices[UpgradeIds.Kit]);
            Assert.All(UpgradeIds.All, (id) => Assert.True(Content.UpgradePrices.ContainsKey(id), id));
            // Every one has its name and its line for every level.
            foreach (var id in UpgradeIds.All)
            {
                Assert.False(string.IsNullOrEmpty(Lore.UpgradeNames[id]));
                for (var level = 1; level <= Content.UpgradePrices[id].Count; level++) Assert.False(string.IsNullOrEmpty(Lore.UpgradeLine(id, 2, 4, Runs.MendHeal, level)));
            }
        }

        [Fact(DisplayName = "mejoras › Entrenamiento: whoever joins comes with a bar more a level")]
        public void Training()
        {
            CampaignSoldier Recruit(int level)
            {
                var c = Solo(5);
                if (level > 0) Up(c, UpgradeIds.Training, level);
                return Runs.NewSoldier(c, 0, new Rng(77));
            }
            var none = Bars.TotalBars(Recruit(0).Skills);
            for (var level = 1; level <= 3; level++) Assert.Equal(none + level, Bars.TotalBars(Recruit(level).Skills));
            Assert.Equal(none + 2, Bars.PointsLeft(Recruit(2).Skills, new List<string>()));
        }

        [Fact(DisplayName = "mejoras › Entrenamiento: a survivor who joins too, over their own bars")]
        public void TrainingTheSurvivor()
        {
            int Joined(int level)
            {
                var (c, s) = LaunchAt(NodeKinds.Rescue, 3, (x) => { if (level > 0) Up(x, UpgradeIds.Training, level); });
                var vip = Grid.GetUnit(s, s.Mission.Survivor!)!;
                Events.ApplyEvent(s, new VipRescuedEvent { Unit = vip.Id, Owner = 0 });
                foreach (var u in s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive && (u.CampaignId != null || u == vip)).ToList()) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
                var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
                return Bars.TotalBars(c.Soldiers.Single((x) => x.Id == report.Run!.Joined).Skills);
            }
            Assert.Equal(Joined(0) + 2, Joined(2));
        }

        [Fact(DisplayName = "mejoras › Ruta de vuelta: home from a Trade too; from the map, never")]
        public void RouteHome()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Trade);
            Assert.Equal(RunTexts.EndFromRefuge, Runs.EndRunRefusal(c));
            Up(c, UpgradeIds.Route, 1);
            Assert.Null(Runs.EndRunRefusal(c));
            Ok(c, new EndRunCommand());
            Assert.Null(c.Run);
            var d = Solo();
            Up(d, UpgradeIds.Route, 1);
            Start(d);
            Assert.Equal(RunTexts.EndFromRefugeOrTrade, Runs.EndRunRefusal(d));
        }

        [Fact(DisplayName = "mejoras › Motor afinado: the horde from the 3rd turn, the 5th at level 2")]
        public void Engine()
        {
            foreach (var (level, from) in new[] { (0, (int?)null), (1, 3), (2, 5) })
            {
                var (_, s) = LaunchAt(NodeKinds.Normal, 1, (c) => { if (level > 0) Up(c, UpgradeIds.Engine, level); });
                Assert.Equal(from, s.Mission.HordeFrom);
                var sim = Step2Helpers.NewSim(s);
                for (var turn = 1; turn <= 6; turn++)
                {
                    var before = sim.Events.Count;
                    sim.CallHorde(turn);
                    var came = sim.Events.Skip(before).OfType<ReinforcementsIncomingEvent>().Any();
                    if (from != null && turn < from) Assert.False(came, $"level {level}, turn {turn}");
                }
            }
        }

        [Fact(DisplayName = "mejoras › Huerto: back from an expedition, 25 or 50 § into the stash, however it went")]
        public void Garden()
        {
            foreach (var (level, supplies) in new[] { (1, 25), (2, 50) })
            {
                var c = Solo();
                Up(c, UpgradeIds.Garden, level);
                GoTo(c, NodeKinds.Refuge);
                var before = Gears.Supplies(c.Stash!);
                Ok(c, new EndRunCommand());
                Assert.Equal(before + supplies, Gears.Supplies(c.Stash!));
                Assert.Contains(c.Log, (e) => e.Text == RunTexts.Garden(supplies));
            }
            // Without it, nothing.
            var d = Solo();
            GoTo(d, NodeKinds.Refuge);
            var had = Gears.Supplies(d.Stash!);
            Ok(d, new EndRunCommand());
            Assert.Equal(had, Gears.Supplies(d.Stash!));
        }

        [Fact(DisplayName = "mejoras › Radio: a row more in sight, and every stage's modifier, those not in sight too")]
        public void Radio()
        {
            var c = Solo();
            Start(c);
            var rows = RunMap.VisibleRows(c);
            var far = c.Run!.Nodes.First((n) => n.Row > rows);
            Assert.False(RunMap.ModifierSeen(c, far));
            Up(c, UpgradeIds.Radio, 1);
            Assert.Equal(rows + 1, RunMap.VisibleRows(c));
            Assert.True(RunMap.ModifierSeen(c, c.Run.Nodes.First((n) => n.Row > rows + 1)));
        }

        [Fact(DisplayName = "mejoras › Botiquín de salida: what was given is still given after a fight (its kit copied there and home)")]
        public void KitThroughAFight()
        {
            var (c, s) = LaunchAt(NodeKinds.Normal, 1, (x) =>
            {
                Up(x, UpgradeIds.Kit, 1);
                foreach (var soldier in x.Soldiers) soldier.Kit!.Belt = new Gear?[2];
            });
            var unit = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
            Assert.Contains(unit.Kit!.Belt, (g) => g?.Type == ItemIds.FirstAid && g.Issued == true);
            foreach (var u in s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive).ToList()) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            var back = c.Soldiers.First((x) => x.Id == unit.CampaignId);
            var kit = back.Kit!.Belt.First((g) => g?.Type == ItemIds.FirstAid)!;
            Assert.True(kit.Issued);
            Assert.Equal(0, Content.SellPrice(kit, true));
        }

        [Fact(DisplayName = "mejoras › Botiquín de salida: out with a first aid kit in the belt, a molotov at level 2; never twice, never offered")]
        public void Kit()
        {
            var c = Solo();
            Up(c, UpgradeIds.Kit, 2);
            foreach (var s in c.Soldiers) s.Kit!.Belt = new Gear?[2];
            Start(c);
            foreach (var s in Squad(c))
            {
                Assert.Contains(s.Kit!.Belt, (g) => g?.Type == ItemIds.FirstAid && g.Issued == true);
                Assert.Contains(s.Kit.Belt, (g) => g?.Type == ItemIds.Molotov && g.Issued == true);
            }
            // At a Trade, not offered; and it makes no pile with one that wasn't given.
            GoTo(c, NodeKinds.Trade);
            var first = Squad(c)[0];
            var given = first.Kit!.Belt.First((g) => g?.Type == ItemIds.FirstAid)!;
            Assert.Equal(0, Content.SellPrice(given, false));
            Refused(RunTexts.NotForSale, c, new SellGearCommand { Soldier = first.Id, Gear = given.Id });
            Assert.False(Gears.SamePile(given, new Gear { Id = "x", Kind = GearKinds.Item, Type = ItemIds.FirstAid, Count = 1 }));
            // Through a fight and back (its kit copied there and home), still given (UI's catch, 0.14.9).
            Assert.True(Gears.CloneGear(given).Issued);
            var pile = new Gear { Id = "p", Kind = GearKinds.Item, Type = ItemIds.Molotov, Count = 2, Issued = true };
            Assert.True(Gears.Split(c, pile, 1).Issued);
            // Home and out again: what they still carry isn't given twice; a free place is filled.
            Up(c, UpgradeIds.Route, 1);
            Ok(c, new EndRunCommand());
            first.Kit.Belt[1] = null;
            Start(c);
            Assert.Single(first.Kit.Belt, (g) => g?.Type == ItemIds.FirstAid);
            Assert.Single(first.Kit.Belt, (g) => g?.Type == ItemIds.Molotov);
            // A full belt: nothing.
            var d = Solo();
            Up(d, UpgradeIds.Kit, 1);
            foreach (var s in d.Soldiers) s.Kit!.Belt = new Gear?[] { Gears.NewGear(d, GearKinds.Item, ItemIds.FragGrenade), Gears.NewGear(d, GearKinds.Item, ItemIds.SmokeGrenade) };
            Start(d);
            Assert.All(Squad(d), (s) => Assert.DoesNotContain(s.Kit!.Belt, (g) => g?.Type == ItemIds.FirstAid));
        }
    }
}
