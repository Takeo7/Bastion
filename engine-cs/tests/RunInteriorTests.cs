using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The offices and the shed only inside (H9, .dev-snapshots/gameplay/runs/H9-INTERIORES.md; Mapgen.OfficesOnly, ShedOnly,
    /// InteriorSite): each the building and the strip at its main way in, closed; every room and La Lata reached; their
    /// rooms by use and their marks as the contract has them; found room by room (the shed's floor at once); what each
    /// gives more; and on the run's map, all the special maps together one fight in five, each where a place like it was.
    /// </summary>
    public class RunInteriorTests
    {
        private static readonly int[] Seeds = { 11, 12, 13, 14, 15, 16 };
        private static readonly string[] Both = { Places.Offices, Places.Shed, Places.Police };

        /// <summary>A run's fight of `kind` launched at `place` on map seed `seed`: the campaign and the mission.</summary>
        public static (CampaignState C, GameState S) At(string place, string kind = NodeKinds.Normal, int seed = 11)
        {
            return LaunchAt(kind, node: (n) =>
            {
                n.Site = place;
                n.Seed = seed;
            });
        }

        private static List<Unit> Squad(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();

        private static HashSet<int> Reached(GameState s) => new HashSet<int>(Paths.ComputeReach(s, Squad(s)[0], 1_000_000, ignore: (_) => true).Cost.Keys);

        // ------------------------------------------------------------------ the maps

        [Fact]
        public void TheBuildingAndItsStripOnly()
        {
            foreach (var place in Both)
            {
                foreach (var seed in Seeds)
                {
                    var (_, s) = At(place, NodeKinds.Normal, seed);
                    Assert.True(s.Mission.Inside);
                    Assert.Equal(place, Places.Of(RunMap.MapOf(new RunNode { Kind = NodeKinds.Normal, Site = place, Seed = seed })!));
                    var (w, h) = place == Places.Shed ? Mapgen.ShedOnlySize : place == Places.Police ? Mapgen.PoliceOnlySize : Mapgen.OfficesOnlySize;
                    Assert.True((s.Width, s.Height) == (w, h) || (s.Width, s.Height) == (h, w), $"{place} {seed}: {s.Width}×{s.Height}");
                    var ground = s.Ground!;
                    var strip = Enumerable.Range(0, ground.Count).Where((i) => ground[i] != 2 && ground[i] != 4).ToList();
                    // (The police station's yard behind its back door too.)
                    Assert.InRange(strip.Count, 1, w * (place == Places.Shed ? Mapgen.ShedStrip : place == Places.Police ? Mapgen.PoliceStrip + Mapgen.PoliceYard : Mapgen.OfficesStrip));
                    Assert.All(Enumerable.Range(0, ground.Count).Where((i) => ground[i] == 4), (i) => Assert.False(Grid.IsWalkable(s.Tiles[i])));
                    // Storeys: the offices two or three, the shed one.
                    var top = s.Storeys!.Max();
                    if (place == Places.Shed) Assert.Equal(1, top);
                    else if (place == Places.Police) Assert.InRange(top, 1, 2);
                    else Assert.InRange(top, 2, 3);
                    // The squad and La Lata on the strip, its bus too; the dead all indoors.
                    Assert.All(Squad(s), (u) => Assert.Contains(Grid.TileIndex(s, u.Pos.X, u.Pos.Y), strip));
                    Assert.All(s.Mission.EvacZone, (z) => Assert.Contains((int)z.Y * s.Width + (int)z.X, strip));
                    Assert.NotNull(s.Mission.EvacBus);
                    Assert.Contains(s.Units, (u) => u.Team == Teams.Alien);
                    Assert.All(s.Units.Where((u) => u.Team == Teams.Alien), (u) => Assert.Equal(2, ground[Grid.TileIndex(s, u.Pos.X, u.Pos.Y)]));
                    // Closed: on foot, nowhere but the strip and the inside.
                    var area = s.Width * s.Height;
                    Assert.All(Reached(s), (n) => Assert.True(n >= area || ground[n] != 4, $"{place} {seed}: {Grid.PosOf(s, n)}"));
                }
            }
        }

        [Fact]
        public void EveryRoomAndLaLataAreReached()
        {
            foreach (var place in Both)
            {
                foreach (var seed in Seeds)
                {
                    var (_, s) = At(place, NodeKinds.Normal, seed);
                    var reached = Reached(s);
                    var rooms = Buildings.Rooms(s);
                    Assert.True(rooms.Count >= (place == Places.Shed ? 3 : 2), $"{place} {seed}: {rooms.Count} rooms");
                    // A police station's cells are shut: nobody goes in.
                    var cells = s.Interiors!.Single().Rooms.Where((r) => r.Use == PoliceUses.Cells).ToList();
                    bool InCell(int n) => cells.Any((r) => Grid.PosOf(s, n) is var p && p.Z == r.Z && p.X >= r.X && p.Y >= r.Y && p.X < r.X + r.W && p.Y < r.Y + r.H);
                    foreach (var room in rooms.Where((r) => !InCell(r[0]))) Assert.True(room.Any(reached.Contains), $"{place} {seed}: a room out of reach at {Grid.PosOf(s, room[0])}");
                    Assert.All(rooms.Where((r) => InCell(r[0])), (room) => Assert.DoesNotContain(room, reached.Contains));
                    Assert.All(s.Mission.EvacZone, (z) => Assert.Contains((int)z.Y * s.Width + (int)z.X, reached));
                    // Every storey of the offices.
                    var area = s.Width * s.Height;
                    Assert.Equal(Enumerable.Range(0, s.Storeys!.Max()), rooms.Select((r) => r[0] / area).Distinct().OrderBy((z) => z));
                }
            }
        }

        [Fact]
        public void TheirRoomsAndMarksAsTheContractHasThem()
        {
            foreach (var seed in Seeds)
            {
                // The offices: a reception behind the main door, the stairs on every storey but the top one (the flight up
                // stands on the storey below), the uses and marks of the contract only.
                var (_, o) = At(Places.Offices, NodeKinds.Normal, seed);
                var site = o.Interiors!.Single();
                Assert.Equal(InteriorKinds.Offices, site.Kind);
                Assert.All(site.Rooms, (r) => Assert.Contains(r.Use, OfficeUses.All));
                Assert.Contains(site.Rooms, (r) => r.Z == 0 && r.Use == OfficeUses.Reception);
                Assert.Contains(site.Rooms, (r) => r.Use == OfficeUses.Stairs);
                Assert.Contains(site.Rooms, (r) => r.Use == OfficeUses.OpenPlan || r.Use == OfficeUses.Office);
                Assert.All(site.Dressing, (d) => Assert.Contains(d.Kind, OfficeDetailKinds.All));
                Assert.Contains(site.Dressing, (d) => d.Kind == OfficeDetailKinds.Desk || d.Kind == OfficeDetailKinds.MeetingTable);
                // A desk's mark on its desk (the tile covers); a chair's and the papers' on free floor.
                var area = o.Width * o.Height;
                string? KindAt(InteriorDetail d) => d.Z == 0 ? o.Tiles[d.Y * o.Width + d.X] : o.Levels![d.Z - 1].Tiles[d.Y * o.Width + d.X];
                Assert.All(site.Dressing.Where((d) => d.Kind == OfficeDetailKinds.Desk), (d) => Assert.Equal(TileKinds.Low, KindAt(d)));
                Assert.All(site.Dressing.Where((d) => d.Kind == OfficeDetailKinds.DeskChair || d.Kind == OfficeDetailKinds.ScatteredPapers), (d) => Assert.Equal(TileKinds.Floor, KindAt(d)));
                // Inside the building.
                Assert.All(site.Dressing, (d) => Assert.True(d.X >= site.X && d.Y >= site.Y && d.X < site.X + site.W && d.Y < site.Y + site.H));

                // The shed: its floor, loading bay, office and lockers; shelving tall (a `high` tile under it).
                var (_, n) = At(Places.Shed, NodeKinds.Normal, seed);
                var shed = n.Interiors!.Single();
                Assert.Equal(InteriorKinds.Shed, shed.Kind);
                Assert.Equal(new[] { ShedUses.Floor, ShedUses.LoadingBay, ShedUses.Office, ShedUses.Lockers }, shed.Rooms.Select((r) => r.Use));
                Assert.All(shed.Dressing, (d) => Assert.Contains(d.Kind, ShedDetailKinds.All));
                Assert.Contains(shed.Dressing, (d) => d.Kind == ShedDetailKinds.Shelving);
                Assert.All(shed.Dressing.Where((d) => d.Kind == ShedDetailKinds.Shelving), (d) => Assert.Equal(TileKinds.High, n.Tiles[d.Y * n.Width + d.X]));
                Assert.Contains(shed.Dressing, (d) => d.Kind == ShedDetailKinds.LockerRow);
                // The loading bay inside the floor, along the front.
                var floor = shed.Rooms[0];
                var bay = shed.Rooms[1];
                Assert.True(bay.X >= floor.X && bay.Y >= floor.Y && bay.X + bay.W <= floor.X + floor.W && bay.Y + bay.H <= floor.Y + floor.H);
                Assert.Equal(3, Math.Min(bay.W, bay.H));

                // The police station: its rooms, 3–5 cells, the armoury with the best box; its marks, the counter on its window.
                var (_, c) = At(Places.Police, NodeKinds.Normal, seed);
                var station = c.Interiors!.Single();
                Assert.Equal(InteriorKinds.Police, station.Kind);
                Assert.All(station.Rooms, (r) => Assert.Contains(r.Use, PoliceUses.All));
                foreach (var use in new[] { PoliceUses.Reception, PoliceUses.Office, PoliceUses.Armory, PoliceUses.Evidence, PoliceUses.Interrogation, PoliceUses.Lockers, PoliceUses.Kitchen }) Assert.Contains(station.Rooms, (r) => r.Z == 0 && r.Use == use);
                Assert.InRange(station.Rooms.Count((r) => r.Use == PoliceUses.Cells), 3, 5);
                Assert.All(station.Dressing, (d) => Assert.Contains(d.Kind, PoliceDetailKinds.All));
                foreach (var kind in new[] { PoliceDetailKinds.Counter, PoliceDetailKinds.CellBed, PoliceDetailKinds.CellToilet, PoliceDetailKinds.WeaponRack, PoliceDetailKinds.EvidenceShelf, PoliceDetailKinds.InterrogationTable, PoliceDetailKinds.Bench }) Assert.Contains(station.Dressing, (d) => d.Kind == kind);
                var counter = station.Dressing.Single((d) => d.Kind == PoliceDetailKinds.Counter);
                Assert.Equal(TileKinds.Window, c.Tiles[counter.Y * c.Width + counter.X]);
                var armory = station.Rooms.Single((r) => r.Use == PoliceUses.Armory);
                Assert.Contains(Enumerable.Range(armory.Y, armory.H).SelectMany((y) => Enumerable.Range(armory.X, armory.W).Select((x) => y * c.Width + x)), (i) => Decor.DecorOf(c.Decor[i])?.Kind == DecorKinds.LootWeapons);
            }
        }

        [Fact]
        public void ThePoliceCellsBehindBars()
        {
            foreach (var seed in Seeds)
            {
                var (_, s) = At(Places.Police, NodeKinds.Normal, seed);
                var cells = s.Interiors!.Single().Rooms.Where((r) => r.Use == PoliceUses.Cells).ToList();
                foreach (var cell in cells)
                {
                    // Its front, two tiles of bars onto the corridor; solid walls to the other cells.
                    var ring = Enumerable.Range(cell.X - 1, cell.W + 2).SelectMany((x) => new[] { (x, cell.Y - 1), (x, cell.Y + cell.H) })
                        .Concat(Enumerable.Range(cell.Y, cell.H).SelectMany((y) => new[] { (cell.X - 1, y), (cell.X + cell.W, y) }))
                        .Select((p) => s.Tiles[p.Item2 * s.Width + p.Item1]).ToList();
                    Assert.Equal(2, ring.Count((k) => k == TileKinds.Bars));
                    Assert.Equal(ring.Count - 2, ring.Count((k) => k == TileKinds.Wall));
                    // Its floor (the rest a bed and a toilet), nobody's to walk to.
                    Assert.Contains(Enumerable.Range(cell.Y, cell.H).SelectMany((y) => Enumerable.Range(cell.X, cell.W).Select((x) => s.Tiles[y * s.Width + x])), (k) => k == TileKinds.Floor);
                }
            }
            // Sight and fire through bars, no cover, no way through.
            Assert.False(Grid.IsWalkable(TileKinds.Bars));
            var (_, t) = At(Places.Police);
            var one = t.Interiors!.Single().Rooms.First((r) => r.Use == PoliceUses.Cells);
            var bar = Enumerable.Range(one.X - 1, one.W + 2).SelectMany((x) => Enumerable.Range(one.Y - 1, one.H + 2).Select((y) => new Pos { X = x, Y = y })).First((p) => t.Tiles[p.Y * t.Width + p.X] == TileKinds.Bars);
            Assert.Equal(0, Grid.ObstacleHeight(TileKinds.Bars));
            Assert.False(Grid.BlocksSightAt(t, bar.X, bar.Y, Grid.BodyHeight));
            // In the corridor just before the bars, stopped: the cell is found; nobody in it shows till then.
            var inside = Grid.Dirs4.Select((d) => new Pos { X = bar.X + d.X, Y = bar.Y + d.Y }).First((p) => p.X >= one.X && p.Y >= one.Y && p.X < one.X + one.W && p.Y < one.Y + one.H);
            var before = new Pos { X = 2 * bar.X - inside.X, Y = 2 * bar.Y - inside.Y };
            var z = t.Units.First((x) => x.Team == Teams.Alien);
            z.Pos = Buildings.RoomAt(t, inside) != null ? Grid.PosOf(t, Buildings.RoomAt(t, inside)!.First((n) => t.Tiles[n] == TileKinds.Floor)) : inside;
            var u = t.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null);
            u.Pos = new Pos { X = 0, Y = 0 };
            Assert.True(Buildings.Hidden(t, z.Pos, new[] { u.Pos }));
            u.Pos = before;
            Assert.False(Buildings.Hidden(t, z.Pos, new[] { before }));
            u.Facing = Los.FacingTo(u.Pos, z.Pos) ?? u.Facing;
            Assert.True(Los.CanSee(t, u, z));
        }

        // -------------------------------------------------------------- room by room

        [Fact]
        public void TheShedsFloorIsFoundAtOnceItsRoomsNot()
        {
            var (_, s) = At(Places.Shed);
            var site = s.Interiors!.Single();
            var floor = site.Rooms.Single((r) => r.Use == ShedUses.Floor);
            var office = site.Rooms.Single((r) => r.Use == ShedUses.Office);
            var u = Squad(s)[0];
            // In through the dock: the whole floor, at once.
            var inside = Buildings.Rooms(s).OrderByDescending((r) => r.Count).First();
            u.Pos = Grid.PosOf(s, inside[0]);
            Buildings.Discover(s, u.Pos, true);
            Assert.Equal(inside.OrderBy((n) => n), s.Discovered!.OrderBy((n) => n));
            Assert.True(inside.Count >= floor.W * floor.H / 2, $"{inside.Count} of {floor.W * floor.H}");
            // Not the office: one of the dead in it stays hidden.
            var z = s.Units.First((x) => x.Team == Teams.Alien);
            z.Pos = new Pos { X = office.X + 1, Y = office.Y + 1, Z = 0 };
            Assert.True(Buildings.Hidden(s, z.Pos, new[] { u.Pos }));
            // Nor anyone on the floor.
            z.Pos = Grid.PosOf(s, inside[inside.Count / 2]);
            Assert.False(Buildings.Hidden(s, z.Pos, new[] { u.Pos }));
        }

        [Fact]
        public void TheOfficesAreFoundRoomByRoom()
        {
            foreach (var seed in Seeds)
            {
                var (_, s) = At(Places.Offices, NodeKinds.Normal, seed);
                var reception = s.Interiors!.Single().Rooms.First((r) => r.Use == OfficeUses.Reception);
                var u = Squad(s)[0];
                u.Pos = new Pos { X = reception.X + reception.W / 2, Y = reception.Y + reception.H / 2, Z = 0 };
                var room = Buildings.RoomAt(s, u.Pos);
                if (room == null) continue;
                Buildings.Discover(s, u.Pos, true);
                Assert.Equal(room.OrderBy((n) => n), s.Discovered!.OrderBy((n) => n));
                // Upstairs, none of it.
                var area = s.Width * s.Height;
                Assert.DoesNotContain(s.Discovered, (n) => n >= area);
                var up = Buildings.Rooms(s).First((r) => r[0] >= area);
                Assert.True(Buildings.Hidden(s, Grid.PosOf(s, up[0]), new[] { u.Pos }));
                return;
            }
            Assert.Fail("no reception to stand in");
        }

        [Fact]
        public void TheDeadUpTheOffices()
        {
            var up = 0;
            foreach (var seed in Seeds)
            {
                var (_, s) = At(Places.Offices, NodeKinds.Normal, seed);
                var dead = s.Units.Where((u) => u.Team == Teams.Alien).ToList();
                up += dead.Count((u) => u.Pos.Z > 0);
                Assert.All(dead.Where((u) => u.Pos.Z > 0), (u) => Assert.True(Flights.CanStopAt(s, u.Pos)));
            }
            Assert.True(up > 0);
        }

        // ------------------------------------------------------------------ the plaza

        [Fact]
        public void ThePlazaItsSquareAndItsShops()
        {
            foreach (var seed in Seeds)
            {
                var (_, s) = At(Places.Plaza, NodeKinds.Normal, seed);
                // Out of doors: not one building, found as any town is.
                Assert.Null(s.Mission.Inside);
                Assert.Equal((Mapgen.PlazaSize, Mapgen.PlazaSize), (s.Width, s.Height));
                Assert.Equal(1, s.Storeys!.Max());
                var site = s.Interiors!.Single();
                Assert.Equal(InteriorKinds.Plaza, site.Kind);
                // Nine shops, one the cinema, a pharmacy among them, each its own use.
                Assert.Equal(9, site.Rooms.Count);
                Assert.All(site.Rooms, (r) => Assert.Contains(r.Use, ShopUses.All));
                Assert.Equal(1, site.Rooms.Count((r) => r.Use == ShopUses.Cinema));
                Assert.Contains(site.Rooms, (r) => r.Use == ShopUses.Pharmacy);
                Assert.Equal(site.Rooms.Count, site.Rooms.Select((r) => r.Use).Distinct().Count());
                Assert.True(site.Rooms.Single((r) => r.Use == ShopUses.Cinema).W * site.Rooms.Single((r) => r.Use == ShopUses.Cinema).H > site.Rooms.Where((r) => r.Use != ShopUses.Cinema).Max((r) => r.W * r.H));
                // Each front all glass but its double door: windows, some broken.
                foreach (var r in site.Rooms)
                {
                    var ring = Enumerable.Range(r.X - 1, r.W + 2).SelectMany((x) => new[] { (x, r.Y - 1), (x, r.Y + r.H) })
                        .Concat(Enumerable.Range(r.Y, r.H).SelectMany((y) => new[] { (r.X - 1, y), (r.X + r.W, y) }))
                        .Select((p) => s.Tiles[p.Item2 * s.Width + p.Item1]).ToList();
                    Assert.Equal(2, ring.Count((k) => k == TileKinds.Door));
                    Assert.True(ring.Count((k) => Grid.IsWindow(k)) >= 2, $"seed {seed}: {r.Use}");
                }
                Assert.Contains(s.Tiles, (k) => k == TileKinds.WindowBroken);
                // Its marks, those of the contract; an awning and a sign for each shop.
                Assert.All(site.Dressing, (d) => Assert.Contains(d.Kind, PlazaDetailKinds.All));
                Assert.Equal(9, site.Dressing.Count((d) => d.Kind == PlazaDetailKinds.ShopSign));
                Assert.Contains(site.Dressing, (d) => d.Kind == PlazaDetailKinds.Planter);
                Assert.Contains(site.Dressing, (d) => d.Kind == PlazaDetailKinds.Popcorn);
                // La Lata on the access street, the squad at its other end; every shop and La Lata reached, nothing out of play.
                var reached = Reached(s);
                var ground = s.Ground!;
                Assert.All(reached.Where((n) => n < s.Width * s.Height), (n) => Assert.NotEqual(4, ground[n]));
                Assert.All(site.Rooms, (r) => Assert.Contains(Enumerable.Range(r.Y, r.H).SelectMany((y) => Enumerable.Range(r.X, r.W).Select((x) => y * s.Width + x)), reached.Contains));
                Assert.All(s.Mission.EvacZone, (z) => Assert.Contains((int)z.Y * s.Width + (int)z.X, reached));
                Assert.NotNull(s.Mission.EvacBus);
                Assert.InRange(RunNearEvacTests.Walk(s), 20, 40);
            }
        }

        [Fact]
        public void APlazaTakesATravesia()
        {
            // Out of doors: a Travesía and a Sabotaje may go there (H9), and only there, of the special maps.
            var crossing = new RunNode { Kind = NodeKinds.Crossing, Site = Places.Town, Seed = 5 };
            Assert.Equal(Places.Plaza, RunMap.SpecialFor(crossing));
            var (_, s) = At(Places.Plaza, NodeKinds.Crossing, 11);
            Assert.NotNull(s.Mission.HordeSide);
            Assert.True(RunNearEvacTests.Walk(s) > 0);
        }

        [Fact]
        public void APlazasSabotageHasAPatrolCarsSiren()
        {
            // A sabotage (its place a factory) may go to the plaza; its alarm, a patrol car's (Lore, 17/11), a factory's elsewhere.
            Assert.Equal(Places.Plaza, RunMap.SpecialFor(new RunNode { Kind = NodeKinds.Sabotage, Site = Places.Factory, Seed = 10 }));
            Assert.Equal(Alarms.Police, Runs.AlarmOf(new RunNode { Kind = NodeKinds.Sabotage, Site = Places.Plaza }));
            Assert.Equal(Alarms.Factory, Runs.AlarmOf(new RunNode { Kind = NodeKinds.Sabotage, Site = Places.Factory }));
            Assert.Equal(Lore.StageLine(NodeKinds.Sabotage, alarm: Alarms.Police), Runs.StageLine(new RunNode { Kind = NodeKinds.Sabotage, Site = Places.Plaza }));
            var (_, s) = LaunchAt(NodeKinds.Sabotage, node: (n) =>
            {
                n.Site = Places.Plaza;
                n.Seed = 11;
            });
            Assert.Equal(MissionKinds.Sabotage, s.Mission.Kind);
            Assert.Contains(s.Decor!, (d) => Decor.DecorOf(d)?.Kind == DecorKinds.SirenCar);
        }

        // ---------------------------------------------------------------- the camping

        [Fact]
        public void TheCampingItsClearing()
        {
            foreach (var seed in Seeds)
            {
                var (_, s) = At(Places.Camping, NodeKinds.Normal, seed);
                Assert.Null(s.Mission.Inside);
                Assert.Equal((Mapgen.CampingSize, Mapgen.CampingSize), (s.Width, s.Height));
                var site = s.Interiors!.Single();
                Assert.Equal(InteriorKinds.Camping, site.Kind);
                Assert.All(site.Rooms, (r) => Assert.Contains(r.Use, CampUses.All));
                Assert.Equal(1, site.Rooms.Count((r) => r.Use == CampUses.Military));
                Assert.Equal(1, site.Rooms.Count((r) => r.Use == CampUses.FirePit));
                Assert.InRange(site.Rooms.Count((r) => r.Use == CampUses.Trailer), 3, 5);
                Assert.True(site.Rooms.Count((r) => r.Use == CampUses.Pitch) >= 2, $"seed {seed}");
                Assert.All(site.Dressing, (d) => Assert.Contains(d.Kind, CampDetailKinds.All));
                Assert.Equal(site.Rooms.Count((r) => r.Use == CampUses.Trailer), site.Dressing.Count((d) => d.Kind == CampDetailKinds.Trailer));
                Assert.Equal(2, site.Dressing.Count((d) => d.Kind == CampDetailKinds.TentMilitary));
                Assert.Contains(site.Dressing, (d) => d.Kind == CampDetailKinds.TentDome || d.Kind == CampDetailKinds.TentLong);
                // A caravan is a building: inside it, a room one goes into by its door.
                var ground = s.Ground!;
                Assert.All(site.Rooms.Where((r) => r.Use == CampUses.Trailer), (r) => Assert.Equal(2, ground[(r.Y + 1) * s.Width + r.X]));
                // The pines round it; every part, La Lata and the caravans' rooms reached.
                Assert.True(s.Tiles.Count((k) => k == TileKinds.High) > s.Width * s.Height / 3);
                var reached = Reached(s);
                Assert.All(site.Rooms.Where((r) => r.Use != CampUses.Toilets), (r) => Assert.Contains(Enumerable.Range(r.Y, r.H).SelectMany((y) => Enumerable.Range(r.X, r.W).Select((x) => y * s.Width + x)), reached.Contains));
                Assert.All(s.Mission.EvacZone, (z) => Assert.Contains((int)z.Y * s.Width + (int)z.X, reached));
                Assert.NotNull(s.Mission.EvacBus);
                Assert.InRange(RunNearEvacTests.Walk(s), 20, 40);
            }
        }

        [Fact]
        public void ACampingsSabotageHasAnAmbulanceInItsQuarantine()
        {
            Assert.Equal(Places.Camping, RunMap.SpecialFor(new RunNode { Kind = NodeKinds.Sabotage, Site = Places.Factory, Seed = 5 }));
            Assert.Equal(Places.Camping, RunMap.SpecialFor(new RunNode { Kind = NodeKinds.Crossing, Site = Places.Country, Seed = 5 }));
            Assert.Equal(Alarms.Ambulance, Runs.AlarmOf(new RunNode { Kind = NodeKinds.Sabotage, Site = Places.Camping }));
            foreach (var seed in Seeds)
            {
                var (_, s) = LaunchAt(NodeKinds.Sabotage, node: (n) =>
                {
                    n.Site = Places.Camping;
                    n.Seed = seed;
                });
                Assert.Equal(MissionKinds.Sabotage, s.Mission.Kind);
                var zone = s.Interiors!.Single().Rooms.Single((r) => r.Use == CampUses.Military);
                // The siren to cut: its relay on its spot.
                var spot = s.Units.Single((u) => u.Id == "relay").Pos;
                Assert.True(spot.X >= zone.X && spot.Y >= zone.Y && spot.X < zone.X + zone.W && spot.Y < zone.Y + zone.H, $"seed {seed}: {spot.X},{spot.Y}");
                Assert.Contains(s.Decor!, (d) => Decor.DecorOf(d)?.Kind == DecorKinds.SirenCar);
            }
        }

        // ------------------------------------------------------------- what they give

        [Fact]
        public void WhatEachGivesMore()
        {
            var run = new RunFight { Node = "n1-0", NodeKind = NodeKinds.Normal, Row = 1 };
            var offices = new List<Gear>();
            var shed = new List<Gear>();
            for (var seed = 1; seed <= 200; seed++)
            {
                Assert.Empty(RunLoot.SiteExtra(Sites.Hospital, run, new Rng(seed), new RunLoot.Maker(0)));
                Assert.Empty(RunLoot.SiteExtra(null, run, new Rng(seed), new RunLoot.Maker(0)));
                offices.AddRange(RunLoot.SiteExtra(Sites.Offices, run, new Rng(seed), new RunLoot.Maker(0)));
                shed.AddRange(RunLoot.SiteExtra(Sites.Shed, run, new Rng(seed), new RunLoot.Maker(0)));
            }
            // Half the time; the offices supplies and improvements, the shed magazines too.
            Assert.InRange(offices.Count, 70, 130);
            Assert.Equal(new[] { GearKinds.Mod, GearKinds.Supplies }, offices.Select((g) => g.Kind).Distinct().OrderBy((k) => k));
            Assert.Equal(new[] { GearKinds.Mags, GearKinds.Mod, GearKinds.Supplies }, shed.Select((g) => g.Kind).Distinct().OrderBy((k) => k));
            // The plaza: supplies and a belt's things; its pharmacy, medicines.
            var plaza = Enumerable.Range(1, 200).SelectMany((seed) => RunLoot.SiteExtra(Sites.Plaza, run, new Rng(seed), new RunLoot.Maker(0))).ToList();
            Assert.Equal(new[] { GearKinds.Item, GearKinds.Supplies }, plaza.Select((g) => g.Kind).Distinct().OrderBy((k) => k));
            Assert.All(plaza.Where((g) => g.Kind == GearKinds.Item), (g) => Assert.Contains(g.Type, Content.BeltItems));
            var pharmacy = Enumerable.Range(1, 200).SelectMany((seed) => RunLoot.SiteExtra(Sites.Plaza, run, new Rng(seed), new RunLoot.Maker(0), ShopUses.Pharmacy)).ToList();
            Assert.Equal(new[] { GearKinds.Medicine }, pharmacy.Select((g) => g.Kind).Distinct());
            // The camping: a little of everything.
            var camping = Enumerable.Range(1, 300).SelectMany((seed) => RunLoot.SiteExtra(Sites.Camping, run, new Rng(seed), new RunLoot.Maker(0))).ToList();
            Assert.Equal(new[] { GearKinds.Item, GearKinds.Mags, GearKinds.Medicine, GearKinds.Mod, GearKinds.Supplies }, camping.Select((g) => g.Kind).Distinct().OrderBy((k) => k));
            // The police station: weapons, magazines and vests.
            var police = Enumerable.Range(1, 200).SelectMany((seed) => RunLoot.SiteExtra(Sites.Police, run, new Rng(seed), new RunLoot.Maker(0))).ToList();
            Assert.Equal(new[] { GearKinds.Mags, GearKinds.Vest, GearKinds.Weapon }, police.Select((g) => g.Kind).Distinct().OrderBy((k) => k));
            Assert.All(police.Where((g) => g.Kind == GearKinds.Weapon), (g) => Assert.Contains(g.Type, RunLoot.PoliceWeapons));
        }

        // ------------------------------------------------------------- on the map

        [Fact]
        public void OneFightInFiveIsASpecialMap()
        {
            var maps = Enumerable.Range(0, 300).Select((seed) => (Off: RunMap.Generate(1, seed, false), On: RunMap.Generate(1, seed, true))).ToList();
            var fights = maps.SelectMany((m) => m.On).Where((n) => RunMap.IsFight(n.Kind) && n.Kind != NodeKinds.Boss).ToList();
            var special = fights.Where((n) => n.Site == Places.Hospital || n.Site == Places.Offices || n.Site == Places.Shed || n.Site == Places.Police || n.Site == Places.Plaza || n.Site == Places.Camping).ToList();
            // One in five of those whose place has a special map like it (the service area, none; a Travesía in a town, a
            // city or the country, the plaza's or the camping's).
            var like = maps.SelectMany((m) => m.Off).Where((n) => RunMap.IsFight(n.Kind) && n.Kind != NodeKinds.Boss && (n.Kind == NodeKinds.Sabotage || ((n.Kind != NodeKinds.Crossing || n.Site == Places.Town || n.Site == Places.City || n.Site == Places.Country) && new[] { Places.Town, Places.City, Places.Estate, Places.Factory, Places.Country }.Contains(n.Site)))).Count();
            Assert.InRange(special.Count / (double)like, 0.15, 0.25);
            // All of them; each where a place like it was; never the boss, a sabotage nor a Travesía.
            var order = new[] { Places.Hospital, Places.Offices, Places.Shed, Places.Police, Places.Plaza, Places.Camping };
            Assert.Equal(order, special.Select((n) => n.Site!).Distinct().OrderBy((x) => Array.IndexOf(order, x)));
            Assert.DoesNotContain(special, (n) => n.Kind == NodeKinds.Boss);
            Assert.DoesNotContain(special.Where((n) => n.Site != Places.Plaza && n.Site != Places.Camping), (n) => n.Kind == NodeKinds.Sabotage || n.Kind == NodeKinds.Crossing);
            foreach (var (off, on) in maps)
            {
                foreach (var (a, b) in off.Zip(on, (a, b) => (a, b)))
                {
                    if (a.Site == b.Site) continue;
                    if (b.Site == Places.Shed) Assert.Contains(a.Site, new[] { Places.Estate, Places.Factory });
                    // A sabotage's place is a factory: on the plaza or the camping it stands for it (H9).
                    else if (a.Kind == NodeKinds.Sabotage) Assert.Contains(b.Site, new[] { Places.Plaza, Places.Camping });
                    else if (b.Site == Places.Camping) Assert.Equal(Places.Country, a.Site);
                    else Assert.Contains(a.Site, new[] { Places.Town, Places.City });
                }
            }
            Assert.Equal("Saqueo · Oficinas", RunTexts.FightName(new RunNode { Kind = NodeKinds.Normal, Site = Places.Offices }, 1));
            Assert.Equal("Saqueo · Nave", RunTexts.FightName(new RunNode { Kind = NodeKinds.Normal, Site = Places.Shed }, 1));
            Assert.Equal("Saqueo · Comisaría", RunTexts.FightName(new RunNode { Kind = NodeKinds.Normal, Site = Places.Police }, 1));
            Assert.Equal("Saqueo · Centro comercial", RunTexts.FightName(new RunNode { Kind = NodeKinds.Normal, Site = Places.Plaza }, 1));
            Assert.Equal("Saqueo · Camping", RunTexts.FightName(new RunNode { Kind = NodeKinds.Normal, Site = Places.Camping }, 1));
        }
    }
}
