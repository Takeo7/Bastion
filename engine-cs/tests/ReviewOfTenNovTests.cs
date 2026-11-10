using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.CampaignHelpers;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>The review of 10/11 (.dev-snapshots/gameplay/REVISION-2026-11-10.md), the engine's part: rows 12, 13 and 14.</summary>
    public class ReviewOfTenNovTests
    {
        // ------------------------------------------------------------ #12: the captive VIP

        /// <summary>A rescue on an open field: the VIP captive at (8, 8), as Setup leaves it but for what each test sets, and a soldier at (0, 8).</summary>
        static (GameState s, Unit vip) Rescue()
        {
            var s = ZombiesTests.Field(20, 20);
            s.Mission.Kind = MissionKinds.Rescue;
            s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 0, Y = 19 }, new Vec2 { X = 1, Y = 19 } };
            var vip = Setup.MakeUnit("vip", TemplateIds.Vip, "VIP", null, P(8, 8), null);
            vip.Captive = true;
            s.Units.Add(vip);
            s.Mission.ObjectiveUnit = "vip";
            AddUnit(s, "s1", TemplateIds.Assault, 0, 8);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 0);
            return (s, vip);
        }

        [Fact(DisplayName = "#12 the captive VIP › nobody orders it while it is held, with all its movement and actions too; once reached, whoever reached it does")]
        public void NobodyOrdersACaptive()
        {
            var (s, vip) = Rescue();
            // As on the first turn before the fix: no TurnBegan has taken its movement away.
            vip.Ap = 2;
            vip.Moves = null;
            foreach (var cmd in new Command[] { Move("vip", 9, 8), Ability("vip", AbilityIds.Hunker), new DoneCommand { Unit = "vip" } })
            {
                var r = Exec(s, cmd);
                Assert.False(r.Ok);
                Assert.Equal("No puedes dar órdenes a esa unidad.", r.Error);
            }
            Assert.Equal((8, 8), (vip.Pos.X, vip.Pos.Y));
            Assert.True(vip.Captive);
            // Reached, it is that player's, and moves on the turn after (TurnBegan gives it its movement).
            Assert.True(Exec(s, Move("s1", 7, 8)).Ok);
            Assert.False(vip.Captive);
            Assert.Equal(0, vip.Owner);
        }

        [Fact(DisplayName = "#12 the captive VIP › a rescue starts with the VIP without actions or movement, and it keeps nobody's turn open")]
        public void ARescueStartsWithTheVipStill()
        {
            foreach (var seed in new[] { 3, 11 })
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Slots = new List<int> { 0 }, Rng = new Rng(seed), Kind = MissionKinds.Rescue });
                var vip = s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);
                Assert.True(vip.Captive);
                Assert.Equal(0, vip.Ap);
                Assert.Equal(0, vip.Moves);
                Assert.False(Paths.HasTurnLeft(vip));
                Assert.DoesNotContain(vip, Grid.Combatants(s, Teams.Xcom));
            }
        }

        // ------------------------------------------------------------ #13: «Convoy emboscado»

        /// <summary>A campaign with its first mission chosen, the squad in the hangar and the convoy's event on the table.</summary>
        static CampaignState WithConvoy()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(2));
            Campaign.OpeningMission(c);
            Assert.True(c.Squad.Count > 0);
            c.Event = CampaignEventIds.Convoy;
            return c;
        }

        [Fact(DisplayName = "#13 the convoy › whoever goes to help comes back wounded and is out of the chosen squad and of the hangar")]
        public void TheWoundedOfTheConvoyLeavesTheSquad()
        {
            var c = WithConvoy();
            // Only those of the squad are fit, so the one sent is one of them.
            foreach (var s in c.Soldiers.Where((s) => !c.Squad.Contains(s.Id))) s.WoundedDays = 9;
            var before = c.Squad.ToList();
            Ok(c, new AnswerEventCommand { Option = 0 });
            var sent = c.Soldiers.Single((s) => s.WoundedDays == 4);
            Assert.Contains(sent.Id, before);
            Assert.DoesNotContain(sent.Id, c.Squad);
            Assert.Equal(before.Count - 1, c.Squad.Count);
            Assert.DoesNotContain(sent.Id, (c.Pads ?? new Dictionary<string, int>()).Keys);
            Assert.Equal(c.Squad.Count, Campaign.BuildSquad(c).Count);
        }

        [Fact(DisplayName = "#13 the convoy › with nobody fit left in the squad, the mission doesn't launch: «La escuadra está vacía.»")]
        public void AnEmptySquadDoesntLaunch()
        {
            var c = WithConvoy();
            var only = c.Squad[0];
            c.Squad = new List<string> { only };
            foreach (var s in c.Soldiers.Where((s) => s.Id != only)) s.WoundedDays = 9;
            Ok(c, new AnswerEventCommand { Option = 0 });
            Assert.Empty(c.Squad);
            var r = Run(c, new LaunchCommand { Ready = true });
            Assert.False(r.Ok);
            Assert.Equal("La escuadra está vacía.", r.Error);
            Assert.False(c.LaunchReady[0]);
            // The same with one chosen who is no longer fit (wounded some other way): those who can go, not those chosen.
            c.Squad = new List<string> { only };
            Assert.Equal("La escuadra está vacía.", Run(c, new LaunchCommand { Ready = true }).Error);
            // Taking the ready back is always allowed, and with someone fit it launches.
            Assert.True(Run(c, new LaunchCommand { Ready = false }).Ok);
            c.Soldiers.Single((s) => s.Id == only).WoundedDays = 0;
            Assert.True(Run(c, new LaunchCommand { Ready = true }).Launch);
        }

        // ------------------------------------------------------------ #14: a point of movement is no turn left

        [Fact(DisplayName = "#14 a point of movement › less than a tile of movement and no actions is nothing left: a tile's worth still is")]
        public void LessThanATileIsNothingLeft()
        {
            var s = ZombiesTests.Field(20, 20);
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5);
            u.Ap = 0;
            u.Moves = Paths.CostOrtho - 1;
            Assert.False(Paths.HasTurnLeft(u));
            Assert.Equal("Sin acciones.", Abilities.AbilityBlocker(s, u, AbilityIds.OpenDoor));
            u.Moves = Paths.CostOrtho;
            Assert.True(Paths.HasTurnLeft(u));
            u.Moves = Paths.CostOrtho - 1;
            u.Ap = 1;
            Assert.True(Paths.HasTurnLeft(u));
        }

        [Fact(DisplayName = "#14 a point of movement › a step over barbed wire that leaves one point ends the soldier's turn, and the squad's when it was the last")]
        public void AStepThatLeavesOnePointEndsTheTurn()
        {
            var s = ZombiesTests.Field(20, 20);
            s.Concealed = false;
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5);
            var other = AddUnit(s, "s2", TemplateIds.Rookie, 1, 1);
            AddUnit(s, "a1", TemplateIds.Zombie, 19, 19);
            // Wire on the next tile: the step costs a tile and the wire, an odd number (the only one there is).
            s.Slow = new List<int> { Grid.NodeOf(s, P(6, 5)) };
            Assert.Equal(1, (Paths.CostOrtho + Content.Traps.Wire) % Paths.CostOrtho);
            u.Ap = 0;
            u.Moves = Paths.CostOrtho + Content.Traps.Wire + 1;
            other.Ap = 0;
            other.Moves = 0;
            var r = Exec(s, Move("s1", 6, 5));
            Assert.True(r.Ok, r.Error);
            // The dead have had their turn and it is the squad's again, without «Hecho».
            Assert.Contains(r.Events!, (e) => e is TurnBeganEvent t && t.Team == Teams.Alien);
            Assert.Equal(2, s.Turn);
            Assert.Equal(Teams.Xcom, s.ActiveTeam);
        }
    }
}
