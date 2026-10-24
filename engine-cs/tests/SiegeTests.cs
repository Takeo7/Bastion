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
    /// packages/engine/test/siege.test.ts: Fase 12, hito 1 (docs/FASE12.md), the base as a battlefield, and a siege of
    /// it, test by test.
    /// </summary>
    public class SiegeTests
    {
        private static Sim RunOf(GameState s, int seed = 1)
        {
            return NewSim(s, seed);
        }

        private static Pos Ground(int x, int y)
        {
            return P(x, y, 0);
        }

        /// <summary>
        /// A small walled yard: a door (north) and two barricades (south). In a siege
        /// every door and barricade holds what SIEGE says.
        /// </summary>
        private static GameState Yard()
        {
            var s = StateFrom(new[]
            {
                "..........",
                ".###+####.",
                ".#......#.",
                ".#......#.",
                ".#......#.",
                ".###hh###.",
                "..........",
            });
            s.Mission.Siege = new Siege { Hold = Content.Siege.Hold };
            s.Breakables = new Dictionary<int, int>
            {
                [Grid.TileIndex(s, 4, 1)] = Content.Siege.Door,
                [Grid.TileIndex(s, 4, 5)] = Content.Siege.Barricade,
                [Grid.TileIndex(s, 5, 5)] = Content.Siege.Barricade,
            };
            return s;
        }

        // ------------------------------------------------------------ the base as a battlefield

        [Fact(DisplayName = "the base as a battlefield › «El Bastión» is a walled base: a gate and a barricade on each side, a watchtower on each corner, the Sala de radio in the middle")]
        public void ElBastionIsAWalledBase()
        {
            var def = Maps.All["bastion"];
            var map = Maps.ParseMap(def);
            Assert.Equal(true, def.Siege);
            Assert.Equal(36, map.Width);
            Assert.Equal(36, map.Height);
            Assert.Equal(12, map.Barricades.Count);
            Assert.Equal(8 + 5, map.Tiles.Count((k) => k == TileKinds.Door));
            // The barricades look like barricades, facing across the wall they stand in.
            DecorLook? Look(int x, int y) => Decor.DecorOf(map.Decor != null && y * map.Width + x < map.Decor.Count ? map.Decor[y * map.Width + x] : (int?)null);
            Assert.Equal((DecorKinds.Barricade, 0), (Look(24, 6)?.Kind, Look(24, 6)?.Facing));
            Assert.Equal((DecorKinds.Barricade, 1), (Look(29, 11)?.Kind, Look(29, 11)?.Facing));
            // The towers stand a storey up, with a ladder from inside.
            Assert.Equal(1, map.Elev[7 * map.Width + 7]);
            Assert.Equal(TileKinds.Ladder, map.Tiles[7 * map.Width + 9]);
            Assert.True(map.Spawns.Count >= 6);
        }

        [Fact(DisplayName = "the base as a battlefield › shut, the wall keeps the dead out: no way in from any side, nor up the towers")]
        public void ShutTheWallKeepsTheDeadOut()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("bastion"), Slots = new List<int> { 0 }, Rng = new Rng(2) });
            bool Inside(int n)
            {
                var x = n % s.Width;
                var y = n / s.Width;
                return x > 6 && x < 29 && y > 6 && y < 29;
            }
            var starts = new[] { (17, 1), (34, 17), (17, 34), (1, 17), (1, 1), (34, 34) };
            for (var i = 0; i < starts.Length; i++)
            {
                var (x, y) = starts[i];
                var z = AddUnit(s, $"z{i}", TemplateIds.Zombie, x, y);
                var reach = Paths.ComputeReach(s, z, double.PositiveInfinity);
                Assert.DoesNotContain(reach.Cost.Keys, Inside);
                // The towers' platforms stay out of reach too (a storey up, their ladders inside).
                Assert.DoesNotContain(reach.Cost.Keys, (n) => s.Elev[n] > 0);
            }
        }

        [Fact(DisplayName = "the base as a battlefield › a mission on it is always a siege: no enemies yet, waves from every front, every door and barricade holding, magazines at hand")]
        public void AMissionOnItIsAlwaysASiege()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("bastion"), Slots = new List<int> { 0, 1 }, Rng = new Rng(7), Kind = MissionKinds.Recovery });
            Assert.Equal(MissionKinds.Elimination, s.Mission.Kind);
            Assert.Equal(Step2Json.Element(new Siege { Hold = Content.Siege.Hold }).GetRawText(), Step2Json.Element(s.Mission.Siege).GetRawText());
            Assert.DoesNotContain(s.Units, (u) => u.Team == Teams.Alien);
            Assert.False(s.Concealed);
            var soldiers = s.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            Assert.Equal(6, soldiers.Count);
            foreach (var u in soldiers) Assert.True(u.Mags >= Content.Siege.Mags);
            var holds = s.Breakables!.ToList();
            Assert.Equal(13, holds.Count((h) => h.Value == Content.Siege.Door));
            Assert.Equal(12, holds.Count((h) => h.Value == Content.Siege.Barricade));
            foreach (var h in holds) Assert.Contains(s.Tiles[h.Key], new[] { TileKinds.Door, TileKinds.Low });
            // Five waves that overlap, each over several sides at once (Ana, 23/10: from every side and together).
            var waves = s.Mission.Reinforcements;
            Assert.Equal(new[] { 1, 2, 3, 5, 6 }, waves.Select((r) => r.Turn).Distinct());
            foreach (var turn in new[] { 2, 3, 5, 6 }) Assert.True(waves.Where((r) => r.Turn == turn).Select((r) => r.Side).Distinct().Count() > 1, $"turn {turn}");
            foreach (var r in waves)
            {
                Assert.Equal(true, r.Horde);
                Assert.True(r.Units.Count <= 5);
                Assert.True(r.Side >= 0);
                Assert.True(r.Side < 4);
            }
            Assert.Contains(TemplateIds.Sectoid, waves.SelectMany((r) => r.Units));
            // Over a few sieges, every side.
            var sides = new HashSet<int?>(new[] { 7, 8, 9, 10 }.SelectMany((seed) => Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("bastion"), Slots = new List<int> { 0 }, Rng = new Rng(seed) }).Mission.Reinforcements.Select((r) => r.Side)));
            Assert.Equal(new int?[] { 0, 1, 2, 3 }, sides.OrderBy((x) => x));
        }

        // ------------------------------------------------------------ the dead against doors and barricades

        [Fact(DisplayName = "the dead against doors and barricades › in a siege the dead never open a door, the squad does; elsewhere they still do")]
        public void InASiegeTheDeadNeverOpenADoor()
        {
            var s = Yard();
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 4, 0);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 4, 2);
            var door = Grid.NodeOf(s, Ground(4, 1));
            Assert.False(Paths.ComputeReach(s, z, 100).Cost.Has(door));
            Assert.True(Paths.ComputeReach(s, soldier, 100).Cost.Has(door));
            s.Mission.Siege = null;
            Assert.True(Paths.ComputeReach(s, z, 100).Cost.Has(door));
        }

        [Fact(DisplayName = "the dead against doors and barricades › with no way to the soldiers it beats on the door beside it: a Zombi takes off 3 a blow, twice a turn, and the blow is heard")]
        public void WithNoWayItBeatsOnTheDoor()
        {
            var s = Yard();
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 4, 0);
            var far = AddUnit(s, "z2", TemplateIds.Zombie, 9, 6, extra: (u) => u.Heard = null);
            Pod(s, "p1", new[] { z });
            Pod(s, "p2", new[] { far });
            AddUnit(s, "s1", TemplateIds.Rookie, 4, 4);
            SamePlaces(new[] { Ground(4, 1) }, Abilities.BreakablesBeside(s, z.Pos));
            Assert.Null(Abilities.AbilityBlocker(s, z, AbilityIds.Smash));
            var run = RunOf(s);
            Ai.TakeZombieTurn(run, z);
            var blows = Of<SmashedEvent>(run.Events);
            Assert.Equal(new[]
            {
                (3, Content.Siege.Door - 3),
                (3, Content.Siege.Door - 6),
            }, blows.Select((e) => (e.Amount, e.Left)));
            Assert.Equal(Content.Siege.Door - 6, s.Breakables![Grid.TileIndex(s, 4, 1)]);
            SamePlace(Ground(4, 0), z.Pos);
            // Heard by the dead round about: they come to the weak point.
            Assert.Contains(Of<NoiseEvent>(run.Events), (e) => e.Radius == Content.Siege.SmashNoise && e.Heard.Contains("z2"));
        }

        [Fact(DisplayName = "the dead against doors and barricades › at nothing it gives way: the way in is open ground, and the next one walks in")]
        public void AtNothingItGivesWay()
        {
            var s = Yard();
            s.Breakables![Grid.TileIndex(s, 4, 1)] = 3;
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 4, 0);
            Pod(s, "p1", new[] { z });
            AddUnit(s, "s1", TemplateIds.Rookie, 7, 4);
            var run = RunOf(s);
            Ai.TakeZombieTurn(run, z);
            var blow = Of<SmashedEvent>(run.Events)[0];
            Assert.Equal((3, 0), (blow.Amount, blow.Left));
            var changed = Of<TileChangedEvent>(run.Events)[0];
            SamePlace(Ground(4, 1), changed.Pos);
            Assert.Equal(TileKinds.Floor, changed.Kind);
            Assert.Equal(TileKinds.Floor, Grid.KindAt(s, Ground(4, 1)));
            // A breach, kept as such.
            Assert.Equal(0, s.Breakables[Grid.TileIndex(s, 4, 1)]);
            // With its second action it is through.
            Assert.True(z.Pos.Y > 1, $"y {z.Pos.Y}");
        }

        [Fact(DisplayName = "the dead against doors and barricades › it goes for the weakest point, counting the walk there and the way left to the soldier")]
        public void ItGoesForTheWeakestPoint()
        {
            var s = Yard();
            // A barricade in the north wall too, two tiles off.
            s.Tiles[Grid.TileIndex(s, 2, 1)] = TileKinds.Low;
            s.Breakables![Grid.TileIndex(s, 2, 1)] = Content.Siege.Barricade;
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 4, 0);
            AddUnit(s, "s1", TemplateIds.Rookie, 4, 4);
            // The barricade holds half of what the door does: worth the two steps.
            var plan = Ai.BreachPlanOf(s, z, Ground(4, 3));
            Assert.NotNull(plan);
            SamePlace(Ground(2, 1), plan!.Tile);
            Assert.Equal(0, plan.Stand.Y);
            // As strong as the door, the door beside it.
            s.Breakables[Grid.TileIndex(s, 2, 1)] = Content.Siege.Door;
            plan = Ai.BreachPlanOf(s, z, Ground(4, 3));
            Assert.NotNull(plan);
            SamePlace(Ground(4, 1), plan!.Tile);
            SamePlace(Ground(4, 0), plan.Stand);
        }

        [Fact(DisplayName = "the dead against doors and barricades › it never beats on what opens nothing new: the other leaf of a gate already broken")]
        public void ItNeverBeatsOnWhatOpensNothingNew()
        {
            var s = StateFrom(new[]
            {
                "............",
                ".####++####.",
                ".#........#.",
                ".#..####..#.",
                ".#..#..+..#.",
                ".#..####..#.",
                ".##########.",
            });
            s.Mission.Siege = new Siege { Hold = Content.Siege.Hold };
            s.Breakables = new Dictionary<int, int>
            {
                [Grid.TileIndex(s, 5, 1)] = Content.Siege.Door,
                [Grid.TileIndex(s, 6, 1)] = Content.Siege.Door,
                [Grid.TileIndex(s, 7, 4)] = Content.Siege.Door,
            };
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 6, 0);
            AddUnit(s, "s1", TemplateIds.Rookie, 9, 5);
            // Both leaves shut: the one on the way in.
            Assert.Equal(1, Ai.BreachPlanOf(s, z, Ground(5, 4))?.Tile.Y);
            // One broken: through it, for the door of the room where the goal is.
            s.Tiles[Grid.TileIndex(s, 5, 1)] = TileKinds.Floor;
            s.Breakables[Grid.TileIndex(s, 5, 1)] = 0;
            var plan = Ai.BreachPlanOf(s, z, Ground(5, 4));
            Assert.NotNull(plan);
            SamePlace(Ground(7, 4), plan!.Tile);
            Assert.NotNull(plan.Stand);
        }

        [Fact(DisplayName = "the dead against doors and barricades › with a way in it walks, and it bites before it beats")]
        public void WithAWayInItWalks()
        {
            var s = Yard();
            s.Tiles[Grid.TileIndex(s, 4, 1)] = TileKinds.DoorOpen;
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 4, 0);
            Pod(s, "p1", new[] { z });
            AddUnit(s, "s1", TemplateIds.Rookie, 7, 4);
            Assert.Null(Ai.BreachPlanOf(s, z, Ground(7, 4)));
            var run = RunOf(s);
            Ai.TakeZombieTurn(run, z);
            Assert.Empty(Of<SmashedEvent>(run.Events));
            Assert.NotEmpty(Of<MovedEvent>(run.Events));

            var t = Yard();
            var biter = AddUnit(t, "z1", TemplateIds.Zombie, 3, 0);
            Pod(t, "p1", new[] { biter });
            var soldier = AddUnit(t, "s1", TemplateIds.Rookie, 2, 0, extra: (u) =>
            {
                u.Hp = 20;
                u.MaxHp = 20;
            });
            var run2 = RunOf(t);
            Ai.TakeZombieTurn(run2, biter);
            Assert.Empty(Of<SmashedEvent>(run2.Events));
            Assert.All(Of<AbilityUsedEvent>(run2.Events), (e) => Assert.Equal(AbilityIds.Claw, e.Ability));
            Assert.True(soldier.Hp < 20, $"hp {soldier.Hp}");
        }

        [Fact(DisplayName = "the dead against doors and barricades › a grenade that takes a barricade leaves a breach too; one that blows a door open does not")]
        public void AGrenadeThatTakesABarricadeLeavesABreach()
        {
            var s = Yard();
            var run = RunOf(s);
            run.Emit(new TileChangedEvent { Pos = Ground(4, 5), Kind = TileKinds.Floor });
            run.Emit(new TileChangedEvent { Pos = Ground(4, 1), Kind = TileKinds.DoorOpen });
            Assert.Equal(0, s.Breakables![Grid.TileIndex(s, 4, 5)]);
            Assert.Equal(Content.Siege.Door, s.Breakables[Grid.TileIndex(s, 4, 1)]);
            SamePlaces(new[] { Ground(5, 5) }, Abilities.BreakablesBeside(s, Ground(4, 6)));
        }

        [Fact(DisplayName = "the dead against doors and barricades › only those with claws beat: a Hinchado waits at the wall")]
        public void OnlyThoseWithClawsBeat()
        {
            Assert.Contains(AbilityIds.Smash, Content.Templates[TemplateIds.Zombie].Abilities);
            Assert.Contains(AbilityIds.Smash, Content.Templates[TemplateIds.Xenoid].Abilities);
            Assert.Contains(AbilityIds.Smash, Content.Templates[TemplateIds.Mec].Abilities);
            Assert.DoesNotContain(AbilityIds.Smash, Content.Templates[TemplateIds.Bloater].Abilities);
            var s = Yard();
            var b = AddUnit(s, "b1", TemplateIds.Bloater, 4, 0);
            Pod(s, "p1", new[] { b });
            AddUnit(s, "s1", TemplateIds.Rookie, 4, 4);
            var run = RunOf(s);
            Ai.TakeZombieTurn(run, b);
            Assert.Empty(Of<SmashedEvent>(run.Events));
        }

        // ------------------------------------------------------------ how a siege ends

        private static Reinforcement Wave(int turn, bool landed)
        {
            return new Reinforcement { Id = "r1", Turn = turn, Pos = null, Units = new List<string> { TemplateIds.Zombie }, Landed = landed, Horde = true, Side = 0 };
        }

        [Fact(DisplayName = "how a siege ends › the dead inside are no defeat; nobody left to defend it is")]
        public void TheDeadInsideAreNoDefeat()
        {
            var s = Yard();
            s.Mission.Reinforcements = new List<Reinforcement> { Wave(5, false) };
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 7, 4);
            AddUnit(s, "z1", TemplateIds.Zombie, 4, 3);
            var run = RunOf(s);
            run.CheckOutcome();
            Assert.Null(s.Outcome);
            run.Damage(soldier, 99, 0);
            run.CheckOutcome();
            Assert.Equal((Outcomes.Defeat, EndReasons.Wiped), (s.Outcome, s.OutcomeReason));
        }

        [Fact(DisplayName = "how a siege ends › the base stands only once every wave is in and down, never after a set turn (Ana, 23/10)")]
        public void TheBaseStandsOnlyOnceEveryWaveIsInAndDown()
        {
            var s = Yard();
            s.Mission.Reinforcements = new List<Reinforcement> { Wave(20, false) };
            AddUnit(s, "s1", TemplateIds.Rookie, 4, 3);
            s.Turn = Content.Siege.Hold - 1;
            RunOf(s).RunAlienTurn();
            RunOf(s).RunAlienTurn();
            Assert.Null(s.Outcome);

            var t = Yard();
            t.Mission.Reinforcements = new List<Reinforcement> { Wave(1, true) };
            AddUnit(t, "s1", TemplateIds.Rookie, 4, 3);
            RunOf(t).CheckOutcome();
            Assert.Equal((Outcomes.Victory, EndReasons.Objective), (t.Outcome, t.OutcomeReason));
        }

        [Fact(DisplayName = "how a siege ends › each group of a wave comes in by its side, alert")]
        public void EachGroupOfAWaveComesInByItsSide()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("bastion"), Slots = new List<int> { 0 }, Rng = new Rng(3) });
            var first = s.Mission.Reinforcements.Where((r) => r.Turn == 1).ToList();
            var run = RunOf(s);
            run.LandReinforcements(1);
            Assert.Equal(first.Sum((r) => r.Units.Count), s.Units.Count((u) => u.Team == Teams.Alien));
            foreach (var r in first)
            {
                var side = r.Side!.Value;
                bool OnSide(Unit u) => new[] { u.Pos.Y < 2, u.Pos.X >= s.Width - 2, u.Pos.Y >= s.Height - 2, u.Pos.X < 2 }[side];
                // In the middle half of that side, not round the corner.
                int Along(Unit u) => side % 2 == 0 ? u.Pos.X : u.Pos.Y;
                foreach (var u in s.Units.Where((x) => x.Id.StartsWith($"{r.Id}u", StringComparison.Ordinal)))
                {
                    Assert.True(OnSide(u));
                    Assert.True(Along(u) >= 9);
                    Assert.True(Along(u) < 27);
                    // They know where the living are: nothing to listen for.
                    Assert.Null(u.Heard);
                }
            }
            foreach (var p in s.Pods) Assert.True(p.Active);
        }

        [Fact(DisplayName = "how a siege ends › seeing nobody, the dead of a siege go for the nearest soldier")]
        public void SeeingNobodyTheDeadGoForTheNearestSoldier()
        {
            var s = Yard();
            s.Tiles[Grid.TileIndex(s, 4, 1)] = TileKinds.DoorOpen;
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 9, 0, extra: (u) => u.Heard = Ground(0, 6));
            Pod(s, "p1", new[] { z });
            AddUnit(s, "s1", TemplateIds.Rookie, 5, 4);
            var run = RunOf(s);
            Ai.TakeZombieTurn(run, z);
            // Through the open door, not to the noise.
            Assert.True(z.Pos.Y > 0, $"y {z.Pos.Y}");
            Assert.True(Math.Abs(z.Pos.X - 5) + Math.Abs(z.Pos.Y - 4) < 9 + 4, $"at {z.Pos.X},{z.Pos.Y}");
        }

        [Fact(DisplayName = "how a siege ends › left undefended, the dead break in and go for the soldiers")]
        public void LeftUndefendedTheDeadBreakIn()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("bastion"), Slots = new List<int> { 0 }, Rng = new Rng(11), Difficulty = 4 });
            // The squad shut in the barracks, out of the way: nobody to hold the wall.
            var barracks = new[] { Ground(10, 11), Ground(11, 11), Ground(12, 11), Ground(13, 11), Ground(10, 12), Ground(11, 12) };
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            for (var i = 0; i < squad.Count; i++) squad[i].Pos = barracks[i];
            var rng = new Rng(5);
            var events = new List<GameEvent>();
            for (var turn = 0; turn < Content.Siege.Hold && s.Outcome == null; turn++)
            {
                var r = Exec(s, new EndTurnCommand { Ready = true }, rng);
                Assert.True(r.Ok, r.Error);
                if (r.Ok) events.AddRange(r.Events!);
            }
            Assert.NotEmpty(Of<SmashedEvent>(events));
            Assert.Contains(Of<TileChangedEvent>(events), (e) => e.Kind == TileKinds.Floor);
            // Shut in, they are hunted down: bitten, and some of them lost.
            Assert.Contains(Of<AbilityUsedEvent>(events), (e) => e.Ability == AbilityIds.Claw);
            Assert.Contains(Of<DiedEvent>(events), (e) => s.Units.Find((u) => u.Id == e.Unit)?.Team == Teams.Xcom);
        }
    }
}
