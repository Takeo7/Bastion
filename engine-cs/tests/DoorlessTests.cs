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
    /// M1 (Arturo, cuadros 133 and 134 of 23/11; gameplay/notas-23-11/SIN-PUERTAS.md): a building with a hole to the outside
    /// that no door closes (a nave's loading bay, a breach) is seen as the street, whole; on a map found room by room, the
    /// room of the hole. One with all its doors, as before.
    /// </summary>
    public class DoorlessTests
    {
        /// <summary>`rows`, `storeys` high over the tiles of x `x0`–`x1`, y `y0`–`y1`; out of doors round them.</summary>
        private static GameState Built(string[] rows, int storeys = 1, int x0 = 2, int x1 = 6, int y0 = 1, int y1 = 5)
        {
            var s = StateFrom(rows);
            s.Storeys = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            for (var y = y0; y <= y1; y++) for (var x = x0; x <= x1; x++) s.Storeys[y * s.Width + x] = storeys;
            return s;
        }

        // A nave (walls x 2–6, y 1–5, a window at (2,3)): its south wall a loading bay of three, no door.
        private static readonly string[] NaveRows = { "............", "..#####.....", "..#...#.....", "..=...#.....", "..#...#.....", "..#...#.....", "............", "............" };
        // A house the same, with a door on its south wall at (4,5).
        private static readonly string[] HouseRows = { "............", "..#####.....", "..#...#.....", "..=...#.....", "..#...#.....", "..##+##.....", "............", "............" };
        // The house with a door and, on its east side, a gap of three (x 6, y 2–4).
        private static readonly string[] MixedRows = { "............", "..#####.....", "..#.........", "..=.........", "..#.........", "..##+##.....", "............", "............" };
        // The house's first floor, walled the same.
        private static readonly string[] Upstairs = { "            ", "  #####     ", "  #...#     ", "  #...#     ", "  #...#     ", "  #####     ", "            ", "            " };

        private static int[] Inside(GameState s) => new[] { (3, 2), (4, 3), (5, 4) }.Select((p) => Grid.TileIndex(s, p.Item1, p.Item2)).ToArray();

        private static void Breach(GameState s, Pos p) => Events.ApplyEvent(s, new TileChangedEvent { Pos = p, Kind = TileKinds.Low, Decor = DecorKinds.Breach });

        [Fact(DisplayName = "sin puertas › a nave with a bay is seen as the street: whoever is inside shows from out of doors, as they already saw out")]
        public void ANaveWithABayIsSeenAsTheStreet()
        {
            var s = Built(NaveRows);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 4, 7, extra: (u) => u.Facing = -Math.PI / 2);
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 4, 3, extra: (u) => u.Facing = Math.PI / 2);
            // As the 0.14.1 had it: hidden, though the one inside saw out through the bay.
            Assert.False(Los.CanSee(s, soldier, zombie));
            Assert.True(Los.CanSee(s, zombie, soldier));
            Assert.True(Buildings.FindOpen(s));
            Assert.True(Los.CanSee(s, soldier, zombie));
            Assert.True(Los.CanSee(s, zombie, soldier));
            Assert.All(Inside(s), (i) => Assert.Contains(i, s.Discovered!));
            // Once is enough: nothing more to find.
            Assert.False(Buildings.FindOpen(s));
        }

        [Fact(DisplayName = "sin puertas › a house with all its doors is as before: nothing found till the squad goes in or looks in")]
        public void AHouseWithAllItsDoorsIsAsBefore()
        {
            var s = Built(HouseRows);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 4, 7, extra: (u) => u.Facing = -Math.PI / 2);
            Assert.False(Buildings.FindOpen(s));
            Assert.Null(s.Discovered);
            Assert.True(Buildings.Hidden(s, P(4, 3), new[] { soldier.Pos }));
            // A door blown open by a blast is a door: as before, found from just before it only (Arturo, 5/11 #2).
            Events.ApplyEvent(s, new TileChangedEvent { Pos = P(4, 5), Kind = TileKinds.DoorOpen });
            Assert.Null(s.Discovered);
        }

        [Fact(DisplayName = "sin puertas › a boarded-up door is shut: high, nothing seen through it, no gap")]
        public void ABoardedUpDoorIsShut()
        {
            var s = Built(HouseRows);
            s.Tiles[Grid.TileIndex(s, 4, 5)] = TileKinds.High;
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            s.Decor[Grid.TileIndex(s, 4, 5)] = Decor.DecorCode(DecorKinds.DoorBoards, 0);
            Assert.False(Buildings.FindOpen(s));
            Assert.True(Buildings.Hidden(s, P(4, 3)));
        }

        [Fact(DisplayName = "sin puertas › a window a blast broke is still a window: shut, no gap")]
        public void ABrokenWindowIsStillAWindow()
        {
            var s = Built(HouseRows);
            Events.ApplyEvent(s, new TileChangedEvent { Pos = P(2, 3), Kind = TileKinds.WindowBroken });
            Assert.False(Buildings.FindOpen(s));
            Assert.Null(s.Discovered);
            Assert.True(Buildings.Hidden(s, P(4, 3)));
        }

        [Fact(DisplayName = "sin puertas › a building with a door and a gap besides is seen too (cuadro 134: «También a la vista»)")]
        public void ADoorAndAGapBesidesIsSeenToo()
        {
            var s = Built(MixedRows);
            Assert.True(Buildings.FindOpen(s));
            Assert.All(Inside(s), (i) => Assert.Contains(i, s.Discovered!));
        }

        [Fact(DisplayName = "sin puertas › a grenade's breach opens the whole house, every storey, as its event plays (cuadro 134: «Abre la casa entera»)")]
        public void AGrenadesBreachOpensTheWholeHouse()
        {
            var s = WithLevels(Built(HouseRows, 2), new[] { Upstairs });
            AddUnit(s, "s1", TemplateIds.Assault, 10, 7, extra: (u) => u.Facing = Math.PI);
            var before = Events.CloneState(s);
            Assert.True(Buildings.Hidden(s, P(4, 3)));
            Assert.True(Buildings.Hidden(s, P(4, 3, 1)));
            var r = Exec(s, Ability("s1", AbilityIds.Grenade, tile: P(4, 6)));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(TileKinds.Low, Grid.KindAt(s, P(3, 5)));
            Assert.False(Buildings.Hidden(s, P(4, 3)));
            Assert.False(Buildings.Hidden(s, P(4, 3, 1)));
            // The partner, playing the events: hidden till the event of the first breach beside its inside, found from it on,
            // the same as the host. (The corner's, (2,5), gives onto no room: it opens nothing.)
            var opened = -1;
            for (var k = 0; k < r.Events!.Count; k++)
            {
                Events.ApplyEvent(before, r.Events[k]);
                if (opened < 0 && !Buildings.Hidden(before, P(4, 3))) opened = k;
            }
            Assert.Contains(r.Events, (e) => e is TileChangedEvent t && Grid.SamePos(t.Pos, P(2, 5)) && t.Decor == DecorKinds.Breach);
            var breach = r.Events.FindIndex((e) => e is TileChangedEvent t && Grid.SamePos(t.Pos, P(3, 5)) && t.Decor == DecorKinds.Breach);
            Assert.True(breach > 0);
            Assert.Equal(breach, opened);
            Assert.Equal(s.Discovered!.OrderBy((i) => i), before.Discovered!.OrderBy((i) => i));
        }

        [Fact(DisplayName = "sin puertas › a breach upstairs opens the whole house too, its ground floor as well")]
        public void ABreachUpstairsOpensTheWholeHouse()
        {
            var s = WithLevels(Built(HouseRows, 2), new[] { Upstairs });
            Breach(s, P(2, 3, 1));
            Assert.False(Buildings.Hidden(s, P(4, 3)));
            Assert.False(Buildings.Hidden(s, P(4, 3, 1)));
        }

        [Fact(DisplayName = "sin puertas › a breach between two houses opens neither: it gives on no street")]
        public void ABreachBetweenTwoHousesOpensNeither()
        {
            var s = Built(new[] { "............", "..#########.", "..#...#...#.", "..#...#...#.", "..#...#...#.", "..##+###+##.", "............", "............" }, 1, 2, 10);
            Breach(s, P(6, 3));
            Assert.Null(s.Discovered);
            Assert.True(Buildings.Hidden(s, P(4, 3)));
            Assert.True(Buildings.Hidden(s, P(8, 3)));
        }

        [Fact(DisplayName = "sin puertas › a breach in a wall of no building (a yard's) opens nothing")]
        public void ABreachInAYardWallOpensNothing()
        {
            var s = Built(new[] { "............", "..#####.....", "..#...#.....", "..=...#..#..", "..#...#..#..", "..##+##..#..", "............", "............" });
            Breach(s, P(9, 4));
            Assert.Null(s.Discovered);
            Assert.True(Buildings.Hidden(s, P(4, 3)));
        }

        // A one-building map (H7): rooms A (x 2–4) and B (x 6–9), a door between them at (5,3) and one out at (3,5).
        private static GameState OneBuilding(string? between = null)
        {
            var s = Built(new[] { "............", ".##########.", ".#...#....#.", ".#...+....#.", ".#...#....#.", ".##+#######.", "............", "............" }, 1, 1, 10);
            s.Mission.Inside = true;
            if (between != null) s.Tiles[Grid.TileIndex(s, 5, 3)] = between;
            return s;
        }

        [Fact(DisplayName = "sin puertas › on a one-building map, a breach in an outer wall opens the room behind it alone; one in a wall between two rooms, none")]
        public void OnAOneBuildingMapTheRoomBehindTheBreach()
        {
            var s = OneBuilding();
            Breach(s, P(10, 3));
            Assert.False(Buildings.Hidden(s, P(8, 3)));
            Assert.True(Buildings.Hidden(s, P(3, 3)));
            var w = OneBuilding();
            Breach(w, P(5, 2));
            Assert.Null(w.Discovered);
            Assert.True(Buildings.Hidden(w, P(3, 3)));
            Assert.True(Buildings.Hidden(w, P(8, 3)));
        }

        [Fact(DisplayName = "sin puertas › a cell (behind bars) breached to the outside opens itself alone; breached to the corridor, nothing")]
        public void ACellOpensItselfAlone()
        {
            var s = OneBuilding(TileKinds.Bars);
            Breach(s, P(10, 3));
            Assert.False(Buildings.Hidden(s, P(8, 3)));
            Assert.True(Buildings.Hidden(s, P(3, 3)));
            var w = OneBuilding(TileKinds.Bars);
            Breach(w, P(5, 4));
            Assert.Null(w.Discovered);
        }

        [Fact(DisplayName = "sin puertas › a nave in plain view wakes nobody: the dead wake as before, seeing or hearing the squad")]
        public void BeingSeenWakesNobody()
        {
            var s = Built(NaveRows);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 4, 7, extra: (u) => u.Facing = -Math.PI / 2);
            // Asleep, its back to the bay.
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 4, 2, extra: (u) => { u.Facing = -Math.PI / 2; u.PodId = "p1"; });
            s.Pods = new List<Pod> { new Pod { Id = "p1", Active = false, UnitIds = new List<string> { "a1" } } };
            Buildings.FindOpen(s);
            Assert.True(Los.CanSee(s, soldier, zombie));
            var r = Exec(s, new EndTurnCommand { Ready = true });
            Assert.True(r.Ok, r.Error);
            Assert.DoesNotContain(r.Events!, (e) => e is PodActivatedEvent);
            Assert.False(s.Pods[0].Active);
        }

        [Fact(DisplayName = "sin puertas › a launched fight has its naves found from the start, its houses not; a fight of the 0.14.1 gets them when loaded")]
        public void ALaunchedFightAndOneOfBefore()
        {
            // The roadside workshop of gen-roadside-3606 (SIN-PUERTAS.md): a bay of three at x 29, y 20–22; a house with a door at (47,21).
            var s = WayOutTests.Launch(new RunNode { Kind = NodeKinds.Normal, Site = Places.Roadside, Seed = 3606, Difficulty = 2 }, 1);
            Assert.Equal("gen-roadside-3606", s.MapId);
            Assert.Contains(Grid.TileIndex(s, 24, 21), s.Discovered!);
            Assert.False(Buildings.Hidden(s, P(24, 21)));
            Assert.True(Buildings.Hidden(s, P(49, 22)));
            var found = s.Discovered!.OrderBy((i) => i).ToList();
            // Saved before M1: nothing found where nobody has been. Loaded (the room's Setup.UpToDate): the same as set out now.
            s.Discovered = null;
            Setup.UpToDate(s);
            Assert.Equal(found, s.Discovered!.OrderBy((i) => i).ToList());
        }
    }
}
