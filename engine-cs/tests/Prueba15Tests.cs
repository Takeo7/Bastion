using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/prueba15.test.ts: the playtest of 2026-10-15 (docs/PRUEBA-2026-10-15.md), test by test.
    /// `client toEqual s` is Step2Json.StateDiff.
    /// </summary>
    public class Prueba15Tests
    {
        private static GameState Field(int w = 20, int h = 20)
        {
            return StateFrom(Enumerable.Range(0, h).Select((_) => new string('.', w)).ToArray());
        }

        /// <summary>`executeCommand(s, cmd, ctx(slot, connected))`: connected [0, 1] when not given, and `new Rng(1)`.</summary>
        private static CommandResult Try(GameState s, Command cmd, int slot, int[]? connected = null)
        {
            return Exec(s, cmd, slot, 1, connected ?? new[] { 0, 1 });
        }

        private static List<GameEvent> Run(GameState s, Command cmd, int slot, int[]? connected = null)
        {
            var r = Try(s, cmd, slot, connected);
            if (!r.Ok) throw new Exception(r.Error);
            return r.Events!;
        }

        // ------------------------------------------------------------ turns one soldier at a time (#9)

        private static (GameState s, Unit a, Unit b, Unit c) Two()
        {
            var s = Field();
            var a = AddUnit(s, "a", TemplateIds.Rookie, 2, 2, extra: (u) => u.Owner = 0);
            var b = AddUnit(s, "b", TemplateIds.Rookie, 2, 4, extra: (u) => u.Owner = 0);
            var c = AddUnit(s, "c", TemplateIds.Rookie, 4, 2, extra: (u) => u.Owner = 1);
            AddUnit(s, "z", TemplateIds.Zombie, 19, 19);
            s.Command = 0;
            return (s, a, b, c);
        }

        [Fact(DisplayName = "turns one soldier at a time (#9) › the soldier in play is finished first; done, the partner plays one of theirs")]
        public void TheSoldierInPlayIsFinishedFirst()
        {
            var (s, a, _, _) = Two();
            Run(s, Move("a", 3, 2), 0);
            Assert.Equal("a", s.ActiveUnit);
            var refused = Try(s, Move("b", 3, 4), 0);
            Assert.Equal((false, $"Primero termina con {a.Name}, o cede el turno."), (refused.Ok, refused.Error));
            Run(s, Move("a", 4, 3), 0);
            Assert.Equal(0, a.Ap);
            Assert.Null(s.ActiveUnit);
            Assert.Equal(1, s.Command);
            // The partner's soldier, then back.
            Run(s, Ability("c", AbilityIds.Overwatch), 1);
            Assert.Equal(0, s.Command);
            Run(s, Move("b", 3, 4), 0);
        }

        [Fact(DisplayName = "turns one soldier at a time (#9) › handing the turn over still works, and the soldier left half-done waits for later")]
        public void HandingTheTurnOverStillWorks()
        {
            var (s, a, _, _) = Two();
            Run(s, Move("a", 3, 2), 0);
            Run(s, new PassCommand(), 0);
            Assert.Equal(1, s.Command);
            Assert.Null(s.ActiveUnit);
            Assert.Equal(1, a.Ap);
        }

        [Fact(DisplayName = "turns one soldier at a time (#9) › alone, or with a partner who has nobody left to move, it is as before")]
        public void AloneItIsAsBefore()
        {
            var (s, _, _, _) = Two();
            Run(s, Move("a", 3, 2), 0, new[] { 0 });
            Run(s, Move("b", 3, 4), 0, new[] { 0 });
            var t = Two();
            t.c.Ap = 0;
            Run(t.s, Move("a", 3, 2), 0);
            Run(t.s, Move("b", 3, 4), 0);
        }

        [Fact(DisplayName = "turns one soldier at a time (#9) › a client replaying it ends with the host state")]
        public void AClientReplayingItEndsWithTheHostState()
        {
            var (s, _, _, _) = Two();
            var client = Events.CloneState(s);
            var events = new List<GameEvent>();
            events.AddRange(Run(s, Move("a", 3, 2), 0));
            events.AddRange(Run(s, Move("a", 4, 3), 0));
            events.AddRange(Run(s, Ability("c", AbilityIds.Overwatch), 1));
            foreach (var e in events) Events.ApplyEvent(client, e);
            Assert.Null(Step2Json.StateDiff(s, client));
        }

        // ------------------------------------------------------------ the bitten and the Cuarentena (#13)

        private static (GameState s, Unit bitten, Unit fine) Bus(bool? quarantine)
        {
            var s = Field();
            s.Mission.Kind = MissionKinds.Recovery;
            s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 0, Y = 0 }, new Vec2 { X = 1, Y = 0 } };
            s.Mission.Quarantine = quarantine;
            var bitten = AddUnit(s, "s1", TemplateIds.Rookie, 0, 0, extra: (u) => u.Infected = true);
            var fine = AddUnit(s, "s2", TemplateIds.Rookie, 1, 0);
            AddUnit(s, "z", TemplateIds.Zombie, 19, 19);
            return (s, bitten, fine);
        }

        [Fact(DisplayName = "the bitten and the Cuarentena (#13) › without a Cuarentena a bitten soldier does not board La Lata, nor asks to")]
        public void WithoutACuarentenaABittenSoldierDoesNotBoard()
        {
            var (s, bitten, fine) = Bus(false);
            var refused = Try(s, new EvacCommand { Unit = "s1" }, 0, new[] { 0 });
            Assert.Equal((false, "Con un mordisco no se sube a La Lata sin una Cuarentena en el Bastión."), (refused.Ok, refused.Error));
            Run(s, new EvacCommand { Unit = "s2" }, 0, new[] { 0 });
            Assert.True(fine.Evacuated);
            Assert.True(bitten.Lost);
        }

        [Fact(DisplayName = "the bitten and the Cuarentena (#13) › with one, or out of the campaign, they board")]
        public void WithOneOrOutOfTheCampaignTheyBoard()
        {
            foreach (var q in new bool?[] { true, null })
            {
                var (s, bitten, _) = Bus(q);
                Run(s, new EvacCommand { Unit = "s1" }, 0, new[] { 0 });
                Assert.True(bitten.Evacuated);
            }
        }

        [Fact(DisplayName = "the bitten and the Cuarentena (#13) › a bitten soldier loses a point of health at every turn of the squad, armour or not")]
        public void ABittenSoldierLosesAPointOfHealth()
        {
            var (s, bitten, fine) = Bus(true);
            bitten.Armor = 2;
            var hp = bitten.Hp;
            var sim = NewSim(s);
            sim.RunAlienTurn();
            Assert.Equal(hp - 1, bitten.Hp);
            Assert.Equal(fine.MaxHp, fine.Hp);
        }

        [Fact(DisplayName = "the bitten and the Cuarentena (#13) › the Cuarentena is built like any facility, and its levels hold the fever off a day each")]
        public void TheCuarentenaIsBuiltLikeAnyFacility()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var quarantine = Campaign.Facilities[FacilityIds.Quarantine];
            Assert.Equal(("Cuarentena", true, 1), (quarantine.Name, quarantine.Unique, quarantine.Level));
            var soldier = c.Soldiers[0];
            soldier.Infection = new SoldierInfection { Since = c.Day };
            var before = Campaign.TurnDay(c, soldier)!.Value;
            CampaignHelpers.Raise(c, PieceIds.Quarantine, 3);
            Assert.Equal(before + 2, Campaign.TurnDay(c, soldier));
        }

        // ------------------------------------------------------------ operations say who and what (#14, #20)

        [Fact(DisplayName = "operations say who and what (#14, #20) › a rescue names who is held and a sabotage what makes the noise; the map is called as the operation")]
        public void ARescueNamesWhoIsHeldAndASabotageWhatMakesTheNoise()
        {
            var rescue = false;
            var sabotage = false;
            for (var seed = 1; seed <= 40 && !(rescue && sabotage); seed++)
            {
                var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(seed));
                foreach (var o in c.Offers.Where((x) => x.Story == null && !x.Final))
                {
                    var info = Campaign.MissionInfo(c, o);
                    if (o.Kind == MissionKinds.Rescue)
                    {
                        rescue = true;
                        Assert.False(string.IsNullOrEmpty(o.Vip));
                        Assert.Equal(o.Vip, info.VipName);
                        var s = Setup.CreateMission(new MissionOptions { Map = o.Map, Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = o.Kind, VipName = info.VipName, CampaignMission = new CampaignMission { Id = o.Id, Name = o.Name } });
                        Assert.Equal(o.Vip, s.Units.First((u) => u.Captive).Name);
                        Assert.Equal(o.Name, s.MapName);
                    }
                    if (o.Kind == MissionKinds.Sabotage)
                    {
                        sabotage = true;
                        Assert.Contains(o.Alarm, new[] { "police", "ambulance", "factory" });
                        var s = Setup.CreateMission(new MissionOptions { Map = o.Map, Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = o.Kind, Alarm = info.Alarm, ObjectiveName = Lore.AlarmNames[info.Alarm!] });
                        Assert.Equal(o.Alarm, s.Mission.Alarm);
                        Assert.Equal(Lore.AlarmNames[o.Alarm!], s.Units.First((u) => u.Objective).Name);
                    }
                }
            }
            Assert.True(rescue && sabotage);
        }

        // ------------------------------------------------------------ the siren a sabotage silences (#14)

        [Fact(DisplayName = "the siren a sabotage silences (#14) › a patrol car or an ambulance blocks the tile of its nose, facing it; a factory alarm takes one tile")]
        public void APatrolCarOrAnAmbulanceBlocksTheTileOfItsNose()
        {
            for (var seed = 1; seed <= 6; seed++)
            {
                GameState At(string alarm) => Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.Facility, seed), Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = MissionKinds.Sabotage, Alarm = alarm });
                foreach (var alarm in new[] { Alarms.Police, Alarms.Ambulance })
                {
                    var s = At(alarm);
                    var spot = s.Units.Find((u) => u.Objective)!.Pos;
                    var noses = s.Decor!.Select((d, i) => (d, i)).Where((x) => Decor.DecorOf(x.d)?.Kind == DecorKinds.SirenCar).Select((x) => (x.i, facing: Decor.DecorOf(x.d)!.Facing)).ToList();
                    Assert.True(noses.Count == 1, $"{alarm} {seed}");
                    var (i, facing) = noses[0];
                    Assert.Equal(TileKinds.High, s.Tiles[i]);
                    Assert.Equal((spot.X + Grid.Dirs4[facing].X, spot.Y + Grid.Dirs4[facing].Y), (i % s.Width, i / s.Width));
                }
                Assert.DoesNotContain(At(Alarms.Factory).Decor!, (d) => Decor.DecorOf(d)?.Kind == DecorKinds.SirenCar);
            }
        }

        // ------------------------------------------------------------ medicines where medicines are kept (#15)

        [Fact(DisplayName = "medicines where medicines are kept (#15) › a medicine box is never out in the street")]
        public void AMedicineBoxIsNeverOutInTheStreet()
        {
            foreach (var biome in new[] { Biomes.City, Biomes.Wilds, Biomes.Facility })
            {
                for (var seed = 1; seed <= 6; seed++)
                {
                    var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, seed), Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = MissionKinds.Recovery });
                    for (var i = 0; i < s.Decor!.Count; i++)
                    {
                        if (Decor.DecorOf(s.Decor[i])?.Kind == DecorKinds.LootMedical) Assert.True(s.Ground?[i] == 2, $"{biome} {seed}");
                    }
                }
            }
        }

        // ------------------------------------------------------------ the turn clock (Arturo, 22/10)

        private static GameState ClockField()
        {
            return StateFrom(new[] { "h.........", "..........", "..........", ".........." });
        }

        [Fact(DisplayName = "the turn clock (Arturo, 22/10) › out of time, their soldiers with actions take cover where they have some, and the partner gets the command")]
        public void OutOfTimeTheirSoldiersTakeCover()
        {
            var s = ClockField();
            var covered = AddUnit(s, "s1", TemplateIds.Rookie, 0, 1);
            var open = AddUnit(s, "s2", TemplateIds.Rookie, 5, 2);
            AddUnit(s, "s3", TemplateIds.Rookie, 8, 0, extra: (u) => u.Owner = 1);
            s.Command = 0;
            var events = Rules.RunOutOfTime(s, 0, new[] { 0, 1 }, new Rng(1));
            Assert.Equal(new[] { "s1" }, Of<HunkerEvent>(events).Select((e) => e.Unit));
            Assert.Equal(0, covered.Ap);
            Assert.Equal(2, open.Ap);
            Assert.Equal(new[] { true, false }, s.Ready);
            Assert.Equal(1, s.Command);
            Assert.Equal(Teams.Xcom, s.ActiveTeam);
        }

        [Fact(DisplayName = "the turn clock (Arturo, 22/10) › with the partner done already, it ends the squad turn")]
        public void WithThePartnerDoneAlreadyItEndsTheSquadTurn()
        {
            var s = ClockField();
            AddUnit(s, "s1", TemplateIds.Rookie, 0, 1);
            AddUnit(s, "s3", TemplateIds.Rookie, 8, 0, extra: (u) => u.Owner = 1);
            s.Command = 0;
            s.Ready = new[] { false, true };
            var events = Rules.RunOutOfTime(s, 0, new[] { 0, 1 }, new Rng(1));
            Assert.Contains(Of<TurnBeganEvent>(events), (e) => e.Team == Teams.Alien);
        }

        [Fact(DisplayName = "the turn clock (Arturo, 22/10) › nothing for a player already done")]
        public void NothingForAPlayerAlreadyDone()
        {
            var s = ClockField();
            AddUnit(s, "s1", TemplateIds.Rookie, 0, 1);
            s.Ready = new[] { true, false };
            Assert.Empty(Rules.RunOutOfTime(s, 0, new[] { 0, 1 }, new Rng(1)));
        }
    }
}
