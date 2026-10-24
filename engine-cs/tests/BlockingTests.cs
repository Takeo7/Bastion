using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>packages/engine/test/blocking.test.ts, test by test.</summary>
    public class BlockingTests
    {
        // Arturo, 19/10: «los compañeros se bloquean». Nobody walks through anyone,
        // whoever's side, as in XCOM; and nobody is ever put where somebody stands.

        /// <summary>`executeCommand(s, cmd, ctx(seed))`: slot 0, alone.</summary>
        private static CommandResult Ctx(GameState s, Command cmd, int seed = 1)
        {
            return Exec(s, cmd, 0, seed, new[] { 0 });
        }

        /// <summary>Two living units on one place, if any, after every event of `events` replayed on `start`.</summary>
        private static string? Stacked(GameState start, IEnumerable<GameEvent> events)
        {
            var s = Events.CloneState(start);
            foreach (var e in events)
            {
                Events.ApplyEvent(s, e);
                var seen = new Dictionary<int, string>();
                foreach (var u in s.Units)
                {
                    if (!u.Alive) continue;
                    var node = Grid.NodeOf(s, u.Pos);
                    if (seen.TryGetValue(node, out var other)) return $"{other} and {u.Id} on {u.Pos.X},{u.Pos.Y},{u.Pos.Z} after {e.T}";
                    seen[node] = u.Id;
                }
            }
            return null;
        }

        private static Pod NewPod(string id, IEnumerable<string> ids)
        {
            return new Pod { Id = id, Active = true, UnitIds = ids.ToList(), Patrol = new List<Pos>(), PatrolIndex = 0 };
        }

        [Fact(DisplayName = "nobody walks through anyone (19/10) › in a corridor a tile wide, an ally is a wall: no way past")]
        public void InACorridorATileWideAnAllyIsAWall()
        {
            var s = StateFrom(new[] { "#########", "#.......#", "#########" });
            var u = AddUnit(s, "s1", TemplateIds.Assault, 1, 1);
            AddUnit(s, "s2", TemplateIds.Assault, 3, 1);
            var reach = Paths.ComputeReach(s, u, 1000);
            Assert.True(reach.Cost.Has(Grid.NodeOf(s, P(2, 1))));
            foreach (var x in new[] { 3, 4, 5, 6, 7 }) Assert.False(reach.Cost.Has(Grid.NodeOf(s, P(x, 1))));
            var r = Ctx(s, Move("s1", 5, 1));
            Assert.False(r.Ok);
            SamePlace(P(1, 1), u.Pos);
        }

        [Fact(DisplayName = "nobody walks through anyone (19/10) › with a way round, it goes round the ally, never through or past its corners")]
        public void WithAWayRoundItGoesRoundTheAlly()
        {
            // The corridor (row 1) and a loop under it (row 3), joined at both ends.
            var s = StateFrom(new[] { "#########", "#.......#", "#.#####.#", "#.......#", "#########" });
            var u = AddUnit(s, "s1", TemplateIds.Assault, 1, 1, extra: (x) => x.Ap = 2);
            AddUnit(s, "s2", TemplateIds.Assault, 4, 1);
            var reach = Paths.ComputeReach(s, u, Paths.MaxMoveCost(u));
            var path = Paths.BuildPath(s, u, reach, P(6, 1));
            Assert.NotNull(path);
            Assert.DoesNotContain(path!, (p) => p.X == 4 && p.Y == 1);
            Assert.Contains(path!, (p) => p.Y == 3);
            Assert.Equal(Paths.CostOrtho * 11, reach.Cost.Get(Grid.NodeOf(s, P(6, 1)), -1));
            var r = Ctx(s, Move("s1", 6, 1));
            Assert.True(r.Ok);
            if (r.Ok)
            {
                var steps = r.Events!.SelectMany((e) => e is MovedEvent m ? m.Path : new List<Pos>());
                Assert.DoesNotContain(steps, (p) => p.X == 4 && p.Y == 1);
            }
            SamePlace(P(6, 1), u.Pos);
        }

        [Fact(DisplayName = "nobody walks through anyone (19/10) › a door opens before the soldier steps into it, not after")]
        public void ADoorOpensBeforeTheSoldierStepsIntoIt()
        {
            var s = StateFrom(new[] { "#######", "#..+..#", "#######" });
            AddUnit(s, "s1", TemplateIds.Assault, 1, 1);
            var r = Ctx(s, Move("s1", 5, 1));
            Assert.True(r.Ok);
            if (!r.Ok) return;
            var order = r.Events!.Where((e) => e is MovedEvent || e is TileChangedEvent).ToList();
            var door = order.FindIndex((e) => e is TileChangedEvent);
            Assert.True(door > 0);
            // Up to the door's side first, then the door opens, then through it.
            var before = Assert.IsType<MovedEvent>(order[door - 1]);
            SamePlaces(new[] { P(2, 1) }, before.Path);
            var opened = (TileChangedEvent)order[door];
            SamePlace(P(3, 1), opened.Pos);
            Assert.Equal(TileKinds.DoorOpen, opened.Kind);
            var through = Assert.IsType<MovedEvent>(order[door + 1]);
            SamePlaces(new[] { P(3, 1), P(4, 1), P(5, 1) }, through.Path);
        }

        [Fact(DisplayName = "nobody walks through anyone (19/10) › the dead never stack: a horde queues and spreads round a room with one door, and still gets in to bite")]
        public void TheDeadNeverStackAHordeQueues()
        {
            var rows = new[]
            {
                "..........................",
                "..........................",
                ".........#######..........",
                ".........#.....#..........",
                ".........#.....+..........",
                ".........#.....#..........",
                ".........#######..........",
                "..........................",
                "..........................",
                "..........................",
            };
            var s = StateFrom(rows);
            // A siege's dead know where the living are (ai.ts takeZombieTurn); the door stands open.
            s.Mission.Siege = new Siege { Hold = 99 };
            s.Tiles[4 * rows[0].Length + 15] = TileKinds.DoorOpen;
            s.ActiveTeam = Teams.Alien;
            AddUnit(s, "s1", TemplateIds.Assault, 11, 4, extra: (u) =>
            {
                u.Hp = 99;
                u.MaxHp = 99;
            });
            var ids = new List<string>();
            for (var k = 0; k < 12; k++)
            {
                var id = $"z{k + 1}";
                AddUnit(s, id, TemplateIds.Zombie, 18 + (k % 6), 7 + k / 6, extra: (u) => u.PodId = "p1");
                ids.Add(id);
            }
            s.Pods.Add(NewPod("p1", ids));
            var bites = 0;
            for (var turn = 0; turn < 8 && s.Outcome == null; turn++)
            {
                var start = Events.CloneState(s);
                var sim = NewSim(s, turn + 1, 0);
                foreach (var u in s.Units) if (u.Alive && u.Team == Teams.Alien) u.Ap = 2;
                s.ActiveTeam = Teams.Alien;
                var thrown = Record.Exception(() => sim.RunAlienTurn());
                Assert.Null(thrown);
                Assert.Null(Stacked(start, sim.Events));
                bites += sim.Events.Count((e) => e is ShotEvent shot && shot.Ability == AbilityIds.Claw);
            }
            Assert.True(bites > 0);
            // In through the door, one at a time: some of them made it inside.
            var inside = s.Units.Where((u) => u.Alive && u.Team == Teams.Alien && u.Pos.X >= 10 && u.Pos.X <= 14 && u.Pos.Y >= 3 && u.Pos.Y <= 5).ToList();
            Assert.True(inside.Count > 0);
        }

        [Fact(DisplayName = "nobody walks through anyone (19/10) › in a corridor a tile wide the dead wait in line behind each other, the front one biting")]
        public void InACorridorTheDeadWaitInLine()
        {
            var s = StateFrom(new[] { "############", "#..........#", "############" });
            s.ActiveTeam = Teams.Alien;
            AddUnit(s, "s1", TemplateIds.Assault, 1, 1, extra: (u) =>
            {
                u.Hp = 99;
                u.MaxHp = 99;
            });
            var ids = new[] { "z1", "z2", "z3", "z4" };
            for (var k = 0; k < ids.Length; k++) AddUnit(s, ids[k], TemplateIds.Zombie, 7 + k, 1, extra: (u) => u.PodId = "p1");
            s.Pods.Add(NewPod("p1", ids));
            var bites = 0;
            for (var turn = 0; turn < 6; turn++)
            {
                var start = Events.CloneState(s);
                var sim = NewSim(s, turn + 7, 0);
                foreach (var u in s.Units) if (u.Alive && u.Team == Teams.Alien) u.Ap = 2;
                sim.RunAlienTurn();
                Assert.Null(Stacked(start, sim.Events));
                bites += sim.Events.Count((e) => e is ShotEvent shot && shot.Ability == AbilityIds.Claw);
            }
            Assert.True(bites > 0);
            // A queue, nose to tail, behind the one at the soldier.
            var xs = ids.Select((id) => s.Units.Find((u) => u.Id == id)!.Pos.X).OrderBy((x) => x).ToArray();
            Assert.Equal(new[] { 2, 3, 4, 5 }, xs);
        }

        [Fact(DisplayName = "nobody walks through anyone (19/10) › the Chillón never raises a corpse onto somebody: it gets up beside them, or not at all")]
        public void TheChillonNeverRaisesACorpseOntoSomebody()
        {
            var s = StateFrom(Enumerable.Range(0, 12).Select((_) => new string('.', 12)).ToArray());
            s.ActiveTeam = Teams.Alien;
            AddUnit(s, "s1", TemplateIds.Assault, 2, 2);
            var screamer = AddUnit(s, "a1", TemplateIds.Sectoid, 9, 2, extra: (u) =>
            {
                u.PodId = "p1";
                u.Ap = 2;
            });
            s.Pods.Add(NewPod("p1", new[] { "a1" }));
            var corpse = AddUnit(s, "a5", TemplateIds.Trooper, 7, 4, extra: (u) =>
            {
                u.Alive = false;
                u.Hp = 0;
            });
            // A soldier stands on the body.
            AddUnit(s, "s2", TemplateIds.Assault, 7, 4);
            Assert.Contains(corpse, Abilities.AbilityTargets(s, screamer, AbilityIds.Reanimate));
            var sim = NewSim(s, 1, 0);
            sim.Perform(screamer, AbilityIds.Reanimate, target: corpse);
            var zombie = s.Units.Find((u) => u.Template == TemplateIds.Zombie)!;
            Assert.True(zombie.Alive);
            Assert.False(Grid.SamePos(corpse.Pos, zombie.Pos));
            Assert.Equal(1, Math.Max(Math.Abs(zombie.Pos.X - 7), Math.Abs(zombie.Pos.Y - 4)));
            Assert.Single(s.Units, (u) => u.Alive && u.Pos.X == zombie.Pos.X && u.Pos.Y == zombie.Pos.Y);

            // Hemmed in all round, with somebody on it: it can't get up, and the Chillón doesn't try.
            var t = StateFrom(new[] { ".....", ".###.", ".#.#.", ".###.", "....." });
            t.ActiveTeam = Teams.Alien;
            var scream2 = AddUnit(t, "a1", TemplateIds.Sectoid, 0, 0, extra: (u) =>
            {
                u.PodId = "p1";
                u.Ap = 2;
            });
            t.Pods.Add(NewPod("p1", new[] { "a1" }));
            // Seen over the walls from above is not the point: the walls are low here, the body in sight.
            t.Tiles = t.Tiles.Select((k) => k == TileKinds.Wall ? TileKinds.Low : k).ToList();
            var body = AddUnit(t, "a5", TemplateIds.Trooper, 2, 2, extra: (u) =>
            {
                u.Alive = false;
                u.Hp = 0;
            });
            AddUnit(t, "s1", TemplateIds.Assault, 2, 2);
            Assert.Equal(TileKinds.Floor, Grid.KindAt(t, body.Pos));
            Assert.DoesNotContain(body, Abilities.AbilityTargets(t, scream2, AbilityIds.Reanimate));
        }

        [Fact(DisplayName = "nobody walks through anyone (19/10) › a silenced patrol car stays solid over both its tiles")]
        public void ASilencedPatrolCarStaysSolid()
        {
            var s = StateFrom(new[] { "......", "......", "......" });
            s.Mission.Kind = MissionKinds.Sabotage;
            s.Mission.Alarm = Alarms.Police;
            s.Mission.ObjectiveUnit = "relay";
            s.Mission.TurnsLeft = 5;
            var relay = AddUnit(s, "relay", TemplateIds.Relay, 2, 1, extra: (u) => u.Objective = true);
            s.Tiles[1 * 6 + 3] = TileKinds.High;
            AddUnit(s, "s1", TemplateIds.Assault, 0, 0);
            var sim = NewSim(s, 1, 0);
            sim.Damage(relay, 99, 0);
            Assert.False(relay.Alive);
            Assert.Equal(TileKinds.High, Grid.KindAt(s, relay.Pos));
            Assert.False(Paths.ComputeReach(s, s.Units.Find((u) => u.Id == "s1")!, 100).Cost.Has(Grid.NodeOf(s, relay.Pos)));
        }

        [Fact(DisplayName = "nobody walks through anyone (19/10) › those dropping round a flare with no room for all of them wait: nobody lands on anyone")]
        public void ThoseDroppingRoundAFlareWithNoRoomWait()
        {
            // A pocket of three tiles walled in, a soldier on one of them: two free within reach of the flare.
            var s = StateFrom(new[] { "#########", "#########", "#########", "###...###", "#########", "#########", "#########", "........." });
            AddUnit(s, "s1", TemplateIds.Assault, 0, 7);
            AddUnit(s, "s2", TemplateIds.Assault, 3, 3);
            s.Mission.Reinforcements.Add(new Reinforcement { Id = "r1", Turn = 1, Pos = P(4, 3), Units = new List<string> { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie }, Landed = false });
            var sim = NewSim(s, 1, 0);
            sim.LandReinforcements(1);
            Assert.DoesNotContain(sim.Events, (e) => e is SpawnedEvent);
            Assert.Equal(2, s.Units.Count((u) => u.Alive));
        }
    }
}
