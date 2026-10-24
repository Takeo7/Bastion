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
    /// packages/engine/test/fase7.test.ts, test by test.
    /// </summary>
    public class Fase7Tests
    {
        // Phase 7 (docs/FASE7.md): magazines are counted (§3), walls (§4) and floors (§5) break.

        private static CommandContext Ctx(int seed = 1)
        {
            return new CommandContext { Slot = 0, Connected = new[] { 0 }, Rng = new Rng(seed) };
        }

        private static List<GameEvent> Run(GameState s, Command cmd, int seed = 1)
        {
            var r = Rules.ExecuteCommand(s, cmd, Ctx(seed));
            if (!r.Ok) throw new Exception(r.Error);
            return r.Events!;
        }

        private static Command Reload(string unit)
        {
            return Ability(unit, AbilityIds.Reload);
        }

        private static Pos At(int x, int y, int z = 0)
        {
            return P(x, y, z);
        }

        /// <summary>toEqual on anything: the same JSON, null and missing alike.</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            if (diff != null) throw new Xunit.Sdk.XunitException(diff);
        }

        // ------------------------------------------------------------ counted magazines

        [Fact(DisplayName = "counted magazines › a soldier takes its spares into the mission; enemies and weapons without a magazine count none")]
        public void ASoldierTakesItsSparesIntoTheMission()
        {
            var s = StateFrom(new[] { "....." });
            Assert.Equal(Content.SpareMags, AddUnit(s, "s1", TemplateIds.Assault, 0, 0).Mags);
            Assert.Null(AddUnit(s, "a1", TemplateIds.Trooper, 4, 0).Mags);
            Assert.Null(AddUnit(s, "z1", TemplateIds.Zombie, 3, 0).Mags);
        }

        [Fact(DisplayName = "counted magazines › reloading spends a spare, and with none left the soldier cannot reload")]
        public void ReloadingSpendsASpare()
        {
            var s = StateFrom(new[] { "....." });
            var u = AddUnit(s, "s1", TemplateIds.Assault, 0, 0, extra: (x) =>
            {
                x.Ammo = 0;
                x.Mags = 1;
            });
            Run(s, Reload("s1"));
            Assert.True(u.Ammo > 0);
            Assert.Equal(0, u.Mags);
            u.Ammo = 0;
            u.Ap = 2;
            Assert.Equal("No quedan cargadores.", Abilities.AbilityBlocker(s, u, AbilityIds.Reload));
            Assert.Equal("Sin munición y sin cargadores.", Abilities.AbilityBlocker(s, u, AbilityIds.Shoot));
            Assert.False(Rules.ExecuteCommand(s, Reload("s1"), Ctx()).Ok);
        }

        [Fact(DisplayName = "counted magazines › an enemy without the count reloads as before")]
        public void AnEnemyWithoutTheCountReloadsAsBefore()
        {
            var s = StateFrom(new[] { "....." });
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 4, 0, extra: (x) => x.Ammo = 0);
            a.Ap = 2;
            Assert.Null(Abilities.AbilityBlocker(s, a, AbilityIds.Reload));
        }

        [Fact(DisplayName = "counted magazines › a search may turn up a magazine, only for a searcher who counts them, and it goes to the spares")]
        public void ASearchMayTurnUpAMagazine()
        {
            var found = 0;
            for (var seed = 0; seed < 400; seed++)
            {
                var counted = Looting.RollLoot(DecorKinds.Crates, 2, new Rng(seed), false, true);
                var plain = Looting.RollLoot(DecorKinds.Crates, 2, new Rng(seed), false, false);
                if (counted?.Mags is int mags && mags != 0)
                {
                    found++;
                    // For anyone else, that same roll turns up nothing; every other roll is the same.
                    Assert.Null(plain);
                }
                else Same(counted, plain);
            }
            Assert.True(found > 10, $"{found} magazines found");
            var s = StateFrom(new[] { "....." });
            var u = AddUnit(s, "s1", TemplateIds.Assault, 0, 0, extra: (x) => x.Mags = 0);
            Events.ApplyEvent(s, new SearchedEvent { Unit = "s1", Tile = P(1, 0, 0), Loot = new Loot { Mags = 1 } });
            Assert.Equal(1, u.Mags);
            Assert.Empty(u.Backpack ?? new List<Loot>());
        }

        [Fact(DisplayName = "counted magazines › a mission saved before magazines were counted gives the squad its spares")]
        public void AMissionSavedBeforeMagazinesWereCountedGivesTheSquadItsSpares()
        {
            var s = StateFrom(new[] { "....." });
            var u = AddUnit(s, "s1", TemplateIds.Assault, 0, 0);
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 4, 0);
            u.Mags = null;
            var old = Setup.NormalizeMission(Events.CloneState(s));
            Assert.Equal(Content.SpareMags, old.Units.Find((o) => o.Id == u.Id)!.Mags);
            Assert.Null(old.Units.Find((o) => o.Id == a.Id)!.Mags);
        }

        // ------------------------------------------------------------ walls that break

        /// <summary>A room (x 1–7, y 1–5) with the street round it.</summary>
        private static GameState Room()
        {
            return StateFrom(new[]
            {
                ".........",
                ".#######.",
                ".#.....#.",
                ".#.....#.",
                ".#.....#.",
                ".#######.",
                ".........",
            });
        }

        private static List<GameEvent> ThrowAt(GameState s, int x, int y, int z = 0)
        {
            return Run(s, Ability("s1", AbilityIds.Grenade, tile: At(x, y, z)));
        }

        [Fact(DisplayName = "walls that break › a grenade leaves the walls it reaches as rubble, low cover, and nothing further off")]
        public void AGrenadeLeavesTheWallsItReachesAsRubble()
        {
            var s = Room();
            AddUnit(s, "s1", TemplateIds.Assault, 6, 4);
            var before = Events.CloneState(s);
            var events = ThrowAt(s, 2, 3);
            var broken = events.OfType<TileChangedEvent>().Where((e) => Grid.KindAt(before, e.Pos) == TileKinds.Wall).ToList();
            Assert.NotEmpty(broken);
            Assert.All(broken, (e) => Assert.Equal(TileKinds.Low, e.Kind));
            // The west wall beside the blast is rubble; the far east wall stands.
            Assert.Equal(TileKinds.Low, Grid.KindAt(s, At(1, 3)));
            Assert.Equal(TileKinds.Wall, Grid.KindAt(s, At(7, 3)));
            // It keeps the look of a breached wall, so a reloaded mission draws it so too.
            Assert.Equal(DecorKinds.Breach, Decor.DecorOf(s.Decor![1 + 3 * s.Width])?.Kind);
            // The partner, replaying the events, sees the same walls.
            foreach (var e in events) Events.ApplyEvent(before, e);
            Assert.Equal(s.Tiles, before.Tiles);
            Assert.Equal(s.Decor, before.Decor);
        }

        [Fact(DisplayName = "walls that break › smoke or a flashbang leave the walls alone")]
        public void SmokeOrAFlashbangLeaveTheWallsAlone()
        {
            var s = Room();
            foreach (var ability in new[] { AbilityIds.Smoke, AbilityIds.Flashbang }) Assert.Empty(Abilities.BreachedWalls(s, ability, At(2, 3)));
            Assert.NotEmpty(Abilities.BreachedWalls(s, AbilityIds.Launch, At(2, 3)));
        }

        [Fact(DisplayName = "walls that break › upstairs, the walls of that floor break and the ones below stand")]
        public void UpstairsTheWallsOfThatFloorBreak()
        {
            var s = WithLevels(Room(), new[]
            {
                new[]
                {
                    "         ",
                    " ####### ",
                    " #.....# ",
                    " #.....# ",
                    " #.....# ",
                    " ####### ",
                    "         ",
                },
            });
            var walls = Abilities.BreachedWalls(s, AbilityIds.Grenade, At(2, 3, 1));
            Assert.NotEmpty(walls);
            Assert.All(walls, (p) => Assert.Equal(1, p.Z));
        }

        // ------------------------------------------------------------ floors that break

        /// <summary>A room (x 1–7, y 1–5) with a first floor over it, walled the same.</summary>
        private static GameState House()
        {
            return WithLevels(
                StateFrom(new[]
                {
                    ".........",
                    ".#######.",
                    ".#.....#.",
                    ".#.....#.",
                    ".#.....#.",
                    ".#######.",
                    ".........",
                }),
                new[]
                {
                    new[]
                    {
                        "         ",
                        " ####### ",
                        " #.....# ",
                        " #.....# ",
                        " #.....# ",
                        " ####### ",
                        "         ",
                    },
                });
        }

        [Fact(DisplayName = "floors that break › a grenade upstairs opens the floor where it lands: who stands there falls a storey, hurt through the armour")]
        public void AGrenadeUpstairsOpensTheFloorWhereItLands()
        {
            var s = House();
            AddUnit(s, "s1", TemplateIds.Assault, 6, 4, z: 1);
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 3, 3, z: 1, extra: (u) =>
            {
                u.Hp = 50;
                u.MaxHp = 50;
                u.Armor = 2;
            });
            var before = Events.CloneState(s);
            var events = Run(s, Ability("s1", AbilityIds.Grenade, tile: At(3, 3, 1)));
            Assert.False(Grid.SlabAt(s, 3, 3, 1));
            Assert.True(Grid.SlabAt(s, 4, 3, 1));
            var i = events.FindIndex((e) => e.T == "fell");
            Same(new FellEvent { Unit = "z1", From = At(3, 3, 1), To = At(3, 3) }, events[i]);
            Assert.IsType<FellEvent>(events[i]);
            Assert.True(events.FindIndex((e) => e.T == "floorBroken") < i);
            var damaged = Assert.IsType<DamagedEvent>(events[i + 1]);
            Assert.Equal(("z1", Content.FallDamage, 0), (damaged.Unit, damaged.Amount, damaged.Mitigated));
            SamePlace(At(3, 3), z.Pos);
            // The partner, replaying the events, has the same hole and the zombie in the same place.
            foreach (var e in events) Events.ApplyEvent(before, e);
            Same(s.Levels, before.Levels);
            SamePlace(At(3, 3), before.Units.First((u) => u.Id == "z1").Pos);
            // The hole is a way down, like an edge.
            Assert.Equal(Paths.CostOrtho + Paths.CostDrop, Paths.StepBetween(s, At(2, 3, 1), At(3, 3)));
        }

        [Fact(DisplayName = "floors that break › who falls onto someone lands on the nearest free place beside them")]
        public void WhoFallsOntoSomeoneLandsOnTheNearestFreePlaceBesideThem()
        {
            var s = House();
            AddUnit(s, "s1", TemplateIds.Assault, 6, 4, z: 1);
            AddUnit(s, "z1", TemplateIds.Zombie, 3, 3, z: 1, extra: (u) =>
            {
                u.Hp = 50;
                u.MaxHp = 50;
            });
            AddUnit(s, "z2", TemplateIds.Zombie, 3, 3, extra: (u) =>
            {
                u.Hp = 50;
                u.MaxHp = 50;
            });
            var events = Run(s, Ability("s1", AbilityIds.Grenade, tile: At(3, 3, 1)));
            var fell = events.OfType<FellEvent>().First();
            Assert.Equal(0, fell.To.Z);
            Assert.Equal(1, Math.Max(Math.Abs(fell.To.X - 3), Math.Abs(fell.To.Y - 3)));
        }

        [Fact(DisplayName = "floors that break › only a frag grenade on a storey: not on the ground, not smoke")]
        public void OnlyAFragGrenadeOnAStorey()
        {
            var s = House();
            Assert.True(Abilities.BreaksFloor(s, AbilityIds.Grenade, At(3, 3, 1)));
            Assert.False(Abilities.BreaksFloor(s, AbilityIds.Grenade, At(3, 3)));
            Assert.False(Abilities.BreaksFloor(s, AbilityIds.Smoke, At(3, 3, 1)));
            Assert.False(Abilities.BreaksFloor(s, AbilityIds.Flashbang, At(3, 3, 1)));
        }

        // ------------------------------------------------------------ sight through windows (docs/PRUEBA-2026-10-14.md)

        [Fact(DisplayName = "sight through windows (docs/PRUEBA-2026-10-14.md) › a line of sight goes through one window at most, both ways (playtest 2026-10-16): in, never out again")]
        public void ALineOfSightGoesThroughOneWindowAtMost()
        {
            // Three walls with a window each, one after another along the row.
            var s = StateFrom(new[] { ".........", ".#.#.#...", ".=.=.=...", ".#.#.#...", "........." });
            Assert.True(Los.LineClear(s, At(0, 2), At(2, 2)));
            Assert.False(Los.LineClear(s, At(0, 2), At(4, 2)));
            Assert.False(Los.LineClear(s, At(4, 2), At(0, 2)));
            Assert.Equal(1, Los.MaxWindowsSeenThrough);
        }

        [Fact(DisplayName = "sight through windows (docs/PRUEBA-2026-10-14.md) › upstairs too, counting the windows of the floor the line crosses")]
        public void UpstairsTooCountingTheWindowsOfTheFloorTheLineCrosses()
        {
            var ground = new[] { ".........", ".........", ".........", ".........", "........." };
            var s = WithLevels(StateFrom(ground), new[] { new[] { "         ", " # # #   ", " = = =   ", " # # #   ", "         " }.Select((r) => r.Replace(' ', '.')).ToArray() });
            Assert.True(Los.LineClear(s, At(0, 2, 1), At(2, 2, 1)));
            Assert.False(Los.LineClear(s, At(0, 2, 1), At(4, 2, 1)));
            // The same line on the ground floor, under them, crosses no window.
            Assert.True(Los.LineClear(s, At(0, 2), At(6, 2)));
            var eye = AddUnit(s, "s1", TemplateIds.Assault, 0, 2, z: 1);
            var fog = Los.TeamVisibility(s, Teams.Xcom);
            int Node(Pos p) => p.Z * s.Width * s.Height + p.Y * s.Width + p.X;
            Assert.True(fog[Node(At(2, 2, 1))]);
            Assert.False(fog[Node(At(4, 2, 1))]);
            Assert.Equal(1, eye.Pos.Z);
        }
    }
}
