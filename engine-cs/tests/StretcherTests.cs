using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// La Camilla, the last of the base's ten more upgrades (Arturo, cuadros 164 and 165; 0.14.11): once an expedition, the
    /// squad's first soldier to fall is left on the floor alive (Unit.Downed), out of the fight as El herido's wounded
    /// (Lame): carried, set down, passed on; hurt again, dead; in La Lata, home with 1 of health; left out, dead.
    /// </summary>
    public class StretcherTests
    {
        private static void Up(CampaignState c, string id, int level) => (c.Upgrades ??= new Dictionary<string, int>())[id] = level;

        private static (CampaignState C, GameState S) Fight(string kind = NodeKinds.Normal, bool stretcher = true) => LaunchAt(kind, 1, (c) =>
        {
            if (stretcher) Up(c, UpgradeIds.Stretcher, 1);
        });

        private static List<Unit> Soldiers(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive).ToList();

        /// <summary>A free tile beside `p`, on its level, reached from it as a box is searched.</summary>
        private static Pos Beside(GameState s, Pos p) => Grid.Dirs8.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z })
            .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && Grid.UnitAt(s, q) == null
                && Looting.ReachesToSearch(s, p, q));

        /// <summary>The first soldier felled with the Camilla ready: downed.</summary>
        private static Unit Downed(GameState s, Sim sim)
        {
            var a = Soldiers(s)[0];
            sim.Damage(a, 99, 0);
            return a;
        }

        [Fact(DisplayName = "camilla › its price and words; ready in a fight of an expedition with it, never without")]
        public void ThePrice()
        {
            Assert.Equal(new[] { 250 }, Content.UpgradePrices[UpgradeIds.Stretcher]);
            Assert.Contains(UpgradeIds.Stretcher, UpgradeIds.All);
            Assert.False(string.IsNullOrEmpty(Lore.UpgradeNames[UpgradeIds.Stretcher]));
            Assert.False(string.IsNullOrEmpty(Lore.UpgradeLine(UpgradeIds.Stretcher)));
            Assert.True(Fight().S.Mission.Stretcher);
            Assert.Null(Fight(stretcher: false).S.Mission.Stretcher);
        }

        [Fact(DisplayName = "camilla › the first soldier to fall lives on the floor, no health, no actions; the next one dies; hurt again, dead")]
        public void TheFirstToFallLives()
        {
            var (_, s) = Fight();
            var sim = NewSim(s);
            var a = Downed(s, sim);
            Assert.True(a.Alive);
            Assert.True(a.Downed);
            Assert.True(a.Lame);
            Assert.Equal(0, a.Hp);
            Assert.Equal(0, a.Ap);
            Assert.False(s.Mission.Stretcher);
            Assert.Contains(sim.Events, (e) => e is DownedEvent d && d.Unit == a.Id);
            Assert.DoesNotContain(sim.Events, (e) => e is DiedEvent d && d.Unit == a.Id);
            // Out of the fight: no turn of its own, nothing to order, no first aid.
            Assert.Equal(0, Content.ApPerTurnOf(a));
            Assert.Equal(0, Paths.FullMoves(a));
            Assert.False(Exec(s, Move(a.Id, a.Pos.X + 1, a.Pos.Y)).Ok);
            var medic = Soldiers(s).First((x) => x != a);
            medic.Pos = Beside(s, a.Pos);
            Assert.DoesNotContain(a, Abilities.AllyTargets(s, medic, AbilityIds.FirstAid));
            // The next one to fall, dead.
            var b = Soldiers(s).First((x) => x != a && x != medic);
            sim.Damage(b, 99, 0);
            Assert.False(b.Alive);
            Assert.Null(b.Downed);
            // Hurt again, whatever, dead.
            sim.Damage(a, 1, 0);
            Assert.False(a.Alive);
            Assert.Contains(sim.Events, (e) => e is DiedEvent d && d.Unit == a.Id);
            // Without the Camilla, the first dies as ever.
            var (_, t) = Fight(stretcher: false);
            var x = Soldiers(t)[0];
            NewSim(t).Damage(x, 99, 0);
            Assert.False(x.Alive);
            Assert.Null(x.Downed);
        }

        [Fact(DisplayName = "camilla › the downed doesn't rise with the dead's turn")]
        public void TheyDontRise()
        {
            var (_, s) = Fight();
            var sim = NewSim(s);
            var a = Downed(s, sim);
            s.ActiveTeam = Teams.Xcom;
            sim.RunAlienTurn();
            Assert.DoesNotContain(sim.Events, (e) => e is ReanimatedEvent r && r.Corpse == a.Id);
        }

        [Fact(DisplayName = "camilla › carried by one beside; set down on La Lata, in at once; home with 1 of health, spent for the expedition")]
        public void CarriedHome()
        {
            var (c, s) = Fight();
            var sim = NewSim(s);
            var a = Downed(s, sim);
            var b = Soldiers(s).First((x) => x != a);
            b.Pos = Beside(s, a.Pos);
            b.Ap = 2;
            Assert.Contains(AbilityIds.Carry, Abilities.CarryAbilities(s, b));
            var up = Exec(s, Ability(b.Id, AbilityIds.Carry));
            Assert.True(up.Ok, up.Error);
            Assert.Equal(b.Id, a.CarriedBy);
            // Off the field while carried: no SurvivorFound for them (not a Rescate's survivor).
            Assert.Null(s.Mission.SurvivorFound);
            var z = s.Mission.EvacZone.Select((v) => new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }).First((p) => Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p) && Abilities.InEvacZone(s, p));
            b.Pos = Beside(s, z);
            Events.ApplyEvent(s, new MovedEvent { Unit = b.Id, Path = new List<Pos> { Events.CopyPos(b.Pos) }, Ap = 0, Moves = 0 });
            b.Ap = 2;
            var down = Exec(s, Ability(b.Id, AbilityIds.SetDown, tile: z));
            Assert.True(down.Ok, down.Error);
            Assert.True(a.Aboard);
            Assert.Null(Rules.DownedOf(s));
            // Home: alive, with 1 of health, and the Camilla spent for the rest of the expedition.
            EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            var soldier = c.Soldiers.Single((x) => x.Id == a.CampaignId);
            Assert.True(soldier.Alive);
            Assert.Equal(1, c.Run!.Hp[soldier.Id]);
            Assert.True(c.Run.StretcherUsed);
            Assert.Contains(c.Log, (e) => e.Text.Contains(soldier.Name) && e.Text.Contains("Camilla"));
            Assert.Contains(c.Log, (e) => e.Text == Lore.StretcherText.Spent);
            var n = RunMap.Choices(c.Run)[0];
            n.Kind = NodeKinds.Normal;
            n.Modifier = null;
            n.Site ??= Places.Country;
            n.Seed ??= 3;
            Assert.True(Ok(c, new ChooseNodeCommand { Node = n.Id }).Launch);
            var next = new Bastion.Net.EngineCampaignRules().LaunchMission(c, new Rng(55))!.Value.state;
            Assert.Null(next.Mission.Stretcher);
        }

        [Fact(DisplayName = "camilla › La Lata doesn't go by itself without the downed; called, it goes, and they are left behind, dead")]
        public void LeftBehind()
        {
            var (c, s) = Fight();
            var sim = NewSim(s);
            var a = Downed(s, sim);
            // Everyone else in La Lata: not «caen todos», and it waits (alive, of the squad: AllAboard).
            foreach (var u in Soldiers(s).Where((x) => x != a).ToList()) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            Assert.False(Rules.AllAboard(s));
            sim.CheckOutcome();
            Assert.Null(s.Outcome);
            // Called, it goes: left behind.
            sim.DepartEvac();
            Assert.True(a.Lost);
            Assert.NotNull(s.Outcome);
            EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.False(c.Soldiers.Single((x) => x.Id == a.CampaignId).Alive);
            Assert.True(c.Run?.StretcherUsed ?? true);
        }

        [Fact(DisplayName = "camilla › the last one standing fallen, the fight is lost and the downed dies with them")]
        public void TheLastOneFalls()
        {
            var (_, s) = Fight();
            var sim = NewSim(s);
            var a = Downed(s, sim);
            foreach (var u in Soldiers(s).Where((x) => x != a).ToList()) sim.Damage(u, 99, 0);
            sim.CheckOutcome();
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            Assert.Equal(EndReasons.Wiped, s.OutcomeReason);
            Assert.False(a.Alive);
        }

        [Fact(DisplayName = "camilla › the soldier who carries falls first: the carried survivor on the floor, the carrier downed beside")]
        public void TheCarrierDowned()
        {
            var (_, s) = LaunchAt(NodeKinds.Wounded, 7, (c) => Up(c, UpgradeIds.Stretcher, 1), (n) =>
            {
                n.Site = Places.Country;
                n.Seed = 7;
                n.Modifier = null;
            });
            var w = s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);
            var u = Soldiers(s)[0];
            u.Pos = Beside(s, w.Pos);
            u.Ap = 2;
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.Carry)).Ok);
            Assert.Equal(u.Id, w.CarriedBy);
            var sim = NewSim(s);
            sim.Damage(u, 99, 0);
            Assert.True(u.Downed);
            Assert.True(w.Alive);
            Assert.Null(w.CarriedBy);
            // Two to carry now, El herido's first when both are beside.
            var v = Soldiers(s).First((x) => x != u);
            v.Pos = Beside(s, w.Pos);
            Assert.Equal(w, Abilities.CarryTarget(s, v));
            Assert.Equal(2, Rules.Carriable(s).Count);
        }

        [Fact(DisplayName = "camilla › not for one who joined on the map: they die, and it stays ready")]
        public void NotForTheSurvivor()
        {
            var (_, s) = Fight(NodeKinds.Rescue);
            var vip = s.Units.Single((u) => u.Id == s.Mission.Survivor);
            Events.ApplyEvent(s, new VipRescuedEvent { Unit = vip.Id, Owner = 0 });
            NewSim(s).Damage(vip, 99, 0);
            Assert.False(vip.Alive);
            Assert.Null(vip.Downed);
            Assert.True(s.Mission.Stretcher);
        }

        // ------------------------------------------------------------------ Producto's review of 26/11

        /// <summary>`u` set on `p` as by a walk, with its turn whole.</summary>
        private static void Put(GameState s, Unit u, Pos p)
        {
            u.Pos = Events.CopyPos(p);
            Events.ApplyEvent(s, new MovedEvent { Unit = u.Id, Path = new List<Pos> { Events.CopyPos(p) }, Ap = 0, Moves = 0 });
            u.Ap = 2;
            u.Moves = null;
        }

        /// <summary>The 8 tiles round `u` on fire: whatever way it walks, its first step burns.</summary>
        private static void RingOfFire(GameState s, Unit u)
        {
            var round = Grid.Dirs8.Select((d) => new Pos { X = u.Pos.X + d.X, Y = u.Pos.Y + d.Y, Z = u.Pos.Z }).Where((q) => Grid.InBounds(s, q.X, q.Y)).Select((q) => Grid.NodeOf(s, q)).ToList();
            s.Fires = new List<Fire> { new Fire { Id = "f1", Source = round[0], Tiles = round } };
        }

        [Fact(DisplayName = "camilla › downed on the way, the walk ends there: not on to La Lata, nothing after it (review B1)")]
        public void TheWalkEndsWhereTheyFall()
        {
            var (_, s) = Fight();
            var a = Soldiers(s)[0];
            var z = s.Mission.EvacZone.Select((v) => new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }).First((p) => Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p) && Abilities.InEvacZone(s, p));
            // From 3 to 5 tiles off La Lata, a walk to it of 3 steps or more.
            List<Pos>? path = null;
            foreach (var d in Enumerable.Range(-5, 11).SelectMany((x) => Enumerable.Range(-5, 11).Select((y) => (x, y))).Where((d) => Math.Max(Math.Abs(d.x), Math.Abs(d.y)) >= 3))
            {
                var from = new Pos { X = z.X + d.x, Y = z.Y + d.y, Z = 0 };
                if (!Grid.InBounds(s, from.X, from.Y) || !Grid.IsWalkable(Grid.KindAt(s, from)) || !Flights.CanStopAt(s, from) || Grid.UnitAt(s, from) != null) continue;
                Put(s, a, from);
                path = Paths.BuildPath(s, a, Paths.ComputeReach(s, a, Paths.MoveBudget(s, a)), z);
                if (path != null && path.Count >= 3 && path.All((q) => q.Z == 0)) break;
                path = null;
            }
            Assert.NotNull(path);
            var start = Events.CopyPos(a.Pos);
            a.Hp = 1;
            RingOfFire(s, a);
            var r = Exec(s, Move(a.Id, z.X, z.Y));
            Assert.True(r.Ok, r.Error);
            Assert.True(a.Downed);
            Assert.True(a.Alive);
            Assert.Null(a.Aboard);
            Assert.True(Aim.Adjacent(start, a.Pos));
            Assert.False(Grid.SamePos(a.Pos, z));
            // Nothing of it after it fell.
            var fell = r.Events!.FindIndex((e) => e is DownedEvent d && d.Unit == a.Id);
            Assert.True(fell >= 0);
            Assert.DoesNotContain(r.Events.Skip(fell + 1), (e) => e is MovedEvent m && m.Unit == a.Id);
            // And it does nothing more, not even what is free.
            Assert.Equal(Lore.StretcherText.NoPack(a.Appearance?.Sex), Abilities.AbilityBlocker(s, a, AbilityIds.Overwatch));
        }

        [Fact(DisplayName = "camilla › a Limpieza: everyone else in La Lata waits for the downed, no «caen todos»; won with them on someone's back, home with their kit (review B2, B3)")]
        public void ALimpieza()
        {
            var (_, s) = Fight(NodeKinds.Elimination);
            Assert.Equal(MissionKinds.Elimination, s.Mission.Kind);
            var sim = NewSim(s);
            var a = Downed(s, sim);
            foreach (var u in Soldiers(s).Where((x) => x != a).ToList()) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            sim.CheckOutcome();
            Assert.Null(s.Outcome);
            Assert.True(a.Alive);
            // Won with them on a back: alive, 1 of health, their kit kept.
            var (c, t) = Fight(NodeKinds.Elimination);
            var b = Downed(t, NewSim(t));
            var carrier = Soldiers(t).First((x) => x != b);
            Put(t, carrier, Beside(t, b.Pos));
            var up = Exec(t, Ability(carrier.Id, AbilityIds.Carry));
            Assert.True(up.Ok, up.Error);
            Assert.False(Runs.FellInFight(b));
            EndFight(c, t, Outcomes.Victory, EndReasons.Objective);
            var soldier = c.Soldiers.Single((x) => x.Id == b.CampaignId);
            Assert.True(soldier.Alive);
            Assert.NotNull(soldier.Kit);
            Assert.Equal(1, c.Run!.Hp[soldier.Id]);
        }

        [Fact(DisplayName = "camilla › on the back of one left behind as La Lata goes: left behind with them, dead (review C1)")]
        public void LeftBehindOnABack()
        {
            var (c, s) = Fight();
            var a = Downed(s, NewSim(s));
            var b = Soldiers(s).First((x) => x != a);
            Put(s, b, Beside(s, a.Pos));
            Assert.True(Exec(s, Ability(b.Id, AbilityIds.Carry)).Ok);
            // Everyone else in, the carrier out of La Lata's zone.
            foreach (var u in Soldiers(s).Where((x) => x != a && x != b).ToList()) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            Assert.False(Abilities.InEvacZone(s, b.Pos));
            NewSim(s).DepartEvac();
            Assert.True(b.Lost);
            Assert.True(a.Lost);
            EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.False(c.Soldiers.Single((x) => x.Id == a.CampaignId).Alive);
        }

        [Fact(DisplayName = "camilla › what of the fight's own the downed carried (key, antiviral, charges) on the floor; the rest stays with them (review C5)")]
        public void TheFightsOwnOnTheFloor()
        {
            var (_, s) = Fight();
            var a = Soldiers(s)[0];
            var key = new Gear { Id = "k1", Kind = GearKinds.Key, Count = 1 };
            a.Pack!.Items.Add(new Placed { Gear = key, X = 3, Y = 2 });
            var mine = a.Pack.Items.Where((p) => p.Gear != key).Select((p) => p.Gear).ToList();
            NewSim(s).Damage(a, 99, 0);
            Assert.True(a.Downed);
            Assert.Contains(s.FloorGear!, (d) => d.Gear == key && d.From == a.Id);
            Assert.DoesNotContain(a.Pack.Items, (p) => p.Gear == key);
            Assert.All(mine, (g) => Assert.Contains(a.Pack.Items, (p) => p.Gear == g));
        }

        [Fact(DisplayName = "camilla › the turn's command never opens with a player whose only soldier is the downed (review C4)")]
        public void NoCommandForTheDowned()
        {
            var (_, s) = Fight();
            var a = Soldiers(s)[0];
            foreach (var u in Soldiers(s).Where((x) => x != a)) u.Owner = 1;
            a.Owner = 0;
            var sim = NewSim(s, 1, 0, 1);
            Assert.Equal(0, sim.OpeningCommander(1));
            sim.Damage(a, 99, 0);
            Assert.True(a.Downed);
            Assert.Equal(1, sim.OpeningCommander(1));
        }

        [Fact(DisplayName = "camilla › Separados: on someone's back their group isn't fallen, and goes if their carrier does (review C8)")]
        public void SeparadosOnABack()
        {
            var (_, s) = Fight();
            var squad = Soldiers(s);
            var a = squad[0];
            s.Mission.Split = new List<List<string>> { new List<string> { a.Id }, squad.Skip(1).Select((x) => x.Id).ToList() };
            Downed(s, NewSim(s));
            var b = squad[1];
            Put(s, b, Beside(s, a.Pos));
            Assert.True(Exec(s, Ability(b.Id, AbilityIds.Carry)).Ok);
            Assert.False(a.Alive);
            Assert.False(Rules.SplitLost(s));
        }

        [Fact(DisplayName = "camilla › the downed and the Camilla spent, copied and in JSON (who comes in late sees them)")]
        public void Copied()
        {
            var (_, s) = Fight();
            var a = Downed(s, NewSim(s));
            foreach (var copy in new[] { Events.CloneState(s), Bastion.Net.Json.Read<GameState>(Bastion.Net.Json.Write(s)) })
            {
                Assert.True(copy.Units.Single((u) => u.Id == a.Id).Downed);
                Assert.True(copy.Units.Single((u) => u.Id == a.Id).Lame);
                Assert.False(copy.Mission.Stretcher);
            }
        }
    }
}
