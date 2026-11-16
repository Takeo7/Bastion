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
    /// The hospital only inside (H7; Sites.Hospital, Mapgen.HospitalOnly, MissionState.Inside, BuildingRooms.cs): a map that is
    /// the town's hospital and the strip at its main entrance, closed; every room and La Lata reached; found room by room,
    /// nobody showing in a room not found; on the run's map only with RunMap.HospitalSites.
    /// </summary>
    public class RunHospitalTests
    {
        private static readonly int[] Seeds = { 11, 12, 13, 14, 15, 16 };

        /// <summary>A run's fight of `kind` launched at the hospital on map seed `seed`: the campaign and the mission.</summary>
        public static (CampaignState C, GameState S) Hospital(string kind = NodeKinds.Normal, int seed = 11)
        {
            return LaunchAt(kind, node: (n) =>
            {
                n.Site = Places.Hospital;
                n.Seed = seed;
            });
        }

        private static List<Unit> Squad(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();

        /// <summary>Every node the squad's first reaches on foot, past anyone in the way, on every storey.</summary>
        private static HashSet<int> Reached(GameState s)
        {
            var reach = Paths.ComputeReach(s, Squad(s)[0], 1_000_000, ignore: (_) => true);
            return new HashSet<int>(reach.Cost.Keys);
        }

        /// <summary>The ground floor's room nearest where the squad starts: the one behind the main door.</summary>
        private static IReadOnlyList<int> Hall(GameState s)
        {
            var area = s.Width * s.Height;
            var squad = Squad(s);
            return Buildings.Rooms(s).Where((r) => r[0] < area).OrderBy((r) => r.Min((n) => squad.Min((u) => Grid.Distance(u.Pos, Grid.PosOf(s, n))))).First();
        }

        // ------------------------------------------------------------------ the map

        [Fact]
        public void TheHospitalAndItsStripOnly()
        {
            foreach (var seed in Seeds)
            {
                var (_, s) = Hospital(NodeKinds.Normal, seed);
                Assert.True(s.Mission.Inside);
                Assert.Equal(Places.Hospital, Places.Of(RunMap.MapOf(new RunNode { Kind = NodeKinds.Normal, Site = Places.Hospital, Seed = seed })!));
                var (w, h) = Mapgen.HospitalOnlySize;
                Assert.True((s.Width, s.Height) == (w, h) || (s.Width, s.Height) == (h, w), $"{s.Width}×{s.Height}");
                // Three storeys, inside and out of play: the outline's ground, never one to stand on.
                Assert.Equal(3, s.Storeys!.Max());
                var ground = s.Ground!;
                var strip = Enumerable.Range(0, ground.Count).Where((i) => ground[i] != 2 && ground[i] != 4).ToList();
                Assert.InRange(strip.Count, 1, w * Mapgen.HospitalStrip);
                Assert.All(Enumerable.Range(0, ground.Count).Where((i) => ground[i] == 4), (i) => Assert.False(Grid.IsWalkable(s.Tiles[i])));
                // The squad and La Lata on the strip; the bus too.
                Assert.All(Squad(s), (u) => Assert.Contains(Grid.TileIndex(s, u.Pos.X, u.Pos.Y), strip));
                Assert.All(s.Mission.EvacZone, (z) => Assert.Contains((int)z.Y * s.Width + (int)z.X, strip));
                Assert.NotNull(s.Mission.EvacBus);
                // Closed: on foot, nowhere but the strip and the inside.
                var area = s.Width * s.Height;
                Assert.All(Reached(s), (n) => Assert.True(n >= area || ground[n] != 4, $"seed {seed}: {Grid.PosOf(s, n)}"));
                // Nobody of the dead out on the strip.
                Assert.All(s.Units.Where((u) => u.Team == Teams.Alien), (u) => Assert.DoesNotContain(Grid.TileIndex(s, u.Pos.X, u.Pos.Y), strip));
                Assert.Contains(s.Units, (u) => u.Team == Teams.Alien);
            }
        }

        [Fact]
        public void EveryRoomAndLaLataAreReached()
        {
            foreach (var seed in Seeds)
            {
                var (_, s) = Hospital(NodeKinds.Normal, seed);
                var reached = Reached(s);
                var rooms = Buildings.Rooms(s);
                Assert.True(rooms.Count >= 10, $"seed {seed}: {rooms.Count} rooms");
                // On all three storeys.
                var area = s.Width * s.Height;
                Assert.Equal(new[] { 0, 1, 2 }, rooms.Select((r) => r[0] / area).Distinct().OrderBy((z) => z));
                foreach (var room in rooms) Assert.True(room.Any(reached.Contains), $"seed {seed}: a room out of reach at {Grid.PosOf(s, room[0])}");
                Assert.All(s.Mission.EvacZone, (z) => Assert.Contains((int)z.Y * s.Width + (int)z.X, reached));
            }
            // A Sigilo's item, inside, reached.
            foreach (var seed in Seeds)
            {
                var (_, s) = Hospital(NodeKinds.Stealth, seed);
                Assert.Equal(MissionKinds.Recovery, s.Mission.Kind);
                var item = s.Mission.Item!.Pos!;
                Assert.Equal(2, s.Ground![Grid.TileIndex(s, item.X, item.Y)]);
                Assert.Contains(Grid.NodeOf(s, item), Reached(s));
            }
        }

        // -------------------------------------------------------------- room by room

        [Fact]
        public void FoundRoomByRoom()
        {
            var (_, s) = Hospital();
            Assert.True(Buildings.ByRoom(s));
            Assert.Empty(s.Discovered ?? new List<int>());
            // In through the main door: the hall, nothing else.
            var hall = Hall(s);
            var u = Squad(s)[0];
            u.Pos = Grid.PosOf(s, hall[0]);
            Buildings.Discover(s, u.Pos, true);
            Assert.Equal(hall.OrderBy((n) => n), s.Discovered!.OrderBy((n) => n));
            var inside = Buildings.Rooms(s).Sum((r) => r.Count);
            Assert.True(s.Discovered.Count < inside / 4, $"{s.Discovered.Count} of {inside}");
            // Stopped before an open door off the hall: the room behind it too; walking past, not.
            var area = s.Width * s.Height;
            var (door, before, behind) = DoorOff(s, hall);
            s.Tiles[door] = TileKinds.DoorOpen;
            Buildings.Discover(s, Grid.PosOf(s, before), false);
            Assert.DoesNotContain(behind[0], s.Discovered);
            Buildings.Discover(s, Grid.PosOf(s, before), true);
            Assert.All(behind, (n) => Assert.Contains(n, s.Discovered));
            // Upstairs is found by going up: none of it yet.
            Assert.DoesNotContain(s.Discovered, (n) => n >= area);
            // The client's fog: what is found, by node; the ground floor's are its tiles.
            Assert.Superset(new HashSet<int>(s.Discovered), Buildings.FoundTiles(s));
        }

        /// <summary>A closed door between the hall and another room on the ground: its tile, the hall's tile before it, and the other room.</summary>
        private static (int Door, int Before, IReadOnlyList<int> Behind) DoorOff(GameState s, IReadOnlyList<int> hall)
        {
            var area = s.Width * s.Height;
            var rooms = Buildings.Rooms(s);
            foreach (var n in hall.Where((n) => n < area))
            {
                foreach (var d in Grid.Dirs4)
                {
                    int x = n % s.Width + d.X, y = n / s.Width + d.Y;
                    int x2 = x + d.X, y2 = y + d.Y;
                    if (!Grid.InBounds(s, x2, y2) || s.Tiles[y * s.Width + x] != TileKinds.Door) continue;
                    var behind = Buildings.RoomAt(s, new Pos { X = x2, Y = y2, Z = 0 });
                    if (behind != null && !behind.Contains(n)) return (y * s.Width + x, n, behind);
                }
            }
            throw new InvalidOperationException("no door off the hall");
        }

        [Fact]
        public void NobodyShowsInARoomNotFound()
        {
            var (_, s) = Hospital();
            var hall = Hall(s);
            var squad = Squad(s);
            squad[0].Pos = Grid.PosOf(s, hall[0]);
            Buildings.Discover(s, squad[0].Pos, true);
            var (door, before, behind) = DoorOff(s, hall);
            s.Tiles[door] = TileKinds.DoorOpen;
            // One of the dead in the room behind the door, a soldier in the hall just before it, looking in.
            var z = s.Units.First((x) => x.Team == Teams.Alien);
            z.Pos = Grid.PosOf(s, behind.OrderBy((n) => Grid.Distance(Grid.PosOf(s, n), Grid.PosOf(s, door))).First());
            var looker = squad[1];
            looker.Pos = Grid.PosOf(s, before);
            looker.Facing = Los.FacingTo(looker.Pos, z.Pos) ?? looker.Facing;
            // Not found, and not stopped there yet: hidden, though it stands in sight.
            var elsewhere = new[] { squad[0].Pos };
            Assert.True(Buildings.Hidden(s, z.Pos, elsewhere));
            // Who looks in from just before the door finds it (the client's Unfound asks with where they stand).
            Assert.False(Buildings.Hidden(s, z.Pos, new[] { looker.Pos }));
            Assert.True(Los.CanSee(s, looker, z));
            // Found, it shows to all.
            Buildings.Discover(s, looker.Pos, true);
            Assert.False(Buildings.Hidden(s, z.Pos, elsewhere));
            // In the doorway one shows; and a room upstairs, not found, keeps its own hidden.
            var area = s.Width * s.Height;
            var up = Buildings.Rooms(s).First((r) => r[0] >= area);
            Assert.True(Buildings.Hidden(s, Grid.PosOf(s, up[0]), elsewhere));
            Assert.False(Buildings.Hidden(s, Grid.PosOf(s, door), elsewhere));
            // The squad's sight (Los.TeamView) leaves out what is in a room not found.
            var view = Los.TeamView(s, Teams.Xcom);
            Assert.All(up, (n) => Assert.False(view[n]));
        }

        [Fact]
        public void ACopyKeepsItInside()
        {
            // Every copy of the state (the screen's, the lab's) is found room by room too (UI, 16/11: CloneState lost Inside).
            var (_, s) = Hospital();
            var copy = Events.CloneState(s);
            Assert.True(copy.Mission.Inside);
            Assert.True(Buildings.ByRoom(copy));
        }

        [Fact]
        public void AnyOtherMapAsBefore()
        {
            // A town: no rooms, found by building.
            var (_, s) = LaunchAt(NodeKinds.Normal, node: (n) =>
            {
                n.Site = Places.Town;
                n.Seed = 1;
            });
            Assert.Null(s.Mission.Inside);
            Assert.False(Buildings.ByRoom(s));
            Assert.Empty(Buildings.Rooms(s));
            Assert.Null(Buildings.RoomAt(s, Squad(s)[0].Pos));
            // The hospital found the old way, whole: all of its ground floor at once.
            var (_, h) = Hospital();
            var hall = Hall(h);
            var area = h.Width * h.Height;
            var ground = Buildings.Rooms(h).Where((r) => r[0] < area).SelectMany((r) => r).ToList();
            h.Mission.Inside = null;
            Buildings.Discover(h, Grid.PosOf(h, hall[0]), true);
            Assert.All(ground, (n) => Assert.Contains(n, h.Discovered!));
            Assert.True(ground.Count > hall.Count * 2);
        }

        [Fact]
        public void ItsDeadOnEveryStorey()
        {
            foreach (var seed in Seeds)
            {
                foreach (var kind in new[] { NodeKinds.Normal, NodeKinds.Stealth, NodeKinds.Hold })
                {
                    var (_, s) = Hospital(kind, seed);
                    var dead = s.Units.Where((u) => u.Team == Teams.Alien).ToList();
                    var by = new[] { 0, 1, 2 }.Select((z) => dead.Count((u) => u.Pos.Z == z)).ToArray();
                    // About a third on each storey.
                    Assert.All(by, (n) => Assert.InRange(n, dead.Count / 3 - 3, dead.Count / 3 + 4));
                    var area = s.Width * s.Height;
                    foreach (var u in dead.Where((x) => x.Pos.Z > 0))
                    {
                        // On a floor one can stand on, in a room (not the corridor), each group in its own and its round there.
                        Assert.True(Flights.CanStopAt(s, u.Pos), $"seed {seed}: {u.Id} at {u.Pos.X},{u.Pos.Y},{u.Pos.Z}");
                        var room = Buildings.RoomAt(s, u.Pos)!;
                        Assert.NotNull(room);
                        var corridor = Buildings.Rooms(s).Where((r) => r[0] / area == u.Pos.Z).OrderByDescending((r) => r.Count).First();
                        Assert.NotSame(corridor, room);
                        var pod = s.Pods.Single((p) => p.UnitIds.Contains(u.Id));
                        Assert.All(pod.UnitIds, (id) => Assert.Contains(Grid.NodeOf(s, Grid.GetUnit(s, id)!.Pos), room));
                        Assert.All(pod.Patrol, (p) => Assert.Contains(Grid.NodeOf(s, p), room));
                    }
                    // Nobody on another.
                    Assert.Equal(dead.Count, dead.Select((u) => Grid.NodeOf(s, u.Pos)).Distinct().Count());
                }
            }
            // In the wards, by the beds (furniture: nobody stands on one).
            var (_, h) = Hospital();
            var upstairs = h.Units.Where((u) => u.Team == Teams.Alien && u.Pos.Z > 0).ToList();
            var wards = h.Medical!.SelectMany((m) => m.Rooms ?? new List<MedicalRoom>()).Where((r) => r.Use == MedicalUses.Ward && r.Z > 0).ToList();
            Assert.True(upstairs.Count((u) => wards.Any((r) => r.Z == u.Pos.Z && u.Pos.X >= r.X && u.Pos.X < r.X + r.W && u.Pos.Y >= r.Y && u.Pos.Y < r.Y + r.H)) >= upstairs.Count / 2);
            var beds = h.Medical!.SelectMany((m) => m.Dressing ?? new List<MedicalDetail>()).Where((d) => d.Z > 0 && (d.Kind == MedicalDetailKinds.CoveredBed || d.Kind == MedicalDetailKinds.BareBed)).ToList();
            Assert.Contains(upstairs, (u) => beds.Any((b) => b.Z == u.Pos.Z && Math.Abs(b.X - u.Pos.X) + Math.Abs(b.Y - u.Pos.Y) == 1));
        }

        [Fact]
        public void ItsWavesLand()
        {
            // An Asedio's (a Travesía never goes there), and the horde's: by any edge there is room on (the strip, the rooms
            // by the outer walls).
            foreach (var kind in new[] { NodeKinds.Hold, NodeKinds.Normal })
            {
                foreach (var seed in Seeds)
                {
                    var (_, s) = Hospital(kind, seed);
                    Assert.Null(s.Mission.HordeSide);
                    Assert.All(s.Mission.Reinforcements, (r) => Assert.Null(r.Side));
                    var sim = NewSim(s);
                    sim.CallHorde(1);
                    foreach (var r in s.Mission.Reinforcements) r.Turn = Math.Min(r.Turn, 1);
                    sim.LandReinforcements(1);
                    Assert.All(s.Mission.Reinforcements, (r) => Assert.True(r.Landed, $"{kind} {seed}: {r.Id} not landed"));
                    var ground = s.Ground!;
                    Assert.All(Of<SpawnedEvent>(sim.Events).SelectMany((e) => e.Units), (u) => Assert.NotEqual(4, ground[Grid.TileIndex(s, u.Pos.X, u.Pos.Y)]));
                }
            }
        }

        // ------------------------------------------------------------- on the map

        [Fact]
        public void OnTheRunsMapOnlyWithItsFlag()
        {
            // On since 16/11 (Arte's camera and walls, UI's pass).
            Assert.True(RunMap.HospitalSites);
            var off = Enumerable.Range(0, 60).Select((seed) => RunMap.Generate(1, seed, false)).ToList();
            Assert.DoesNotContain(off.SelectMany((m) => m), (n) => n.Site == Places.Hospital);
            var on = Enumerable.Range(0, 60).Select((seed) => RunMap.Generate(1, seed, true)).ToList();
            var hospitals = on.SelectMany((m) => m).Where((n) => n.Site == Places.Hospital).ToList();
            Assert.NotEmpty(hospitals);
            // Normal nodes and specials both; never a sabotage nor the boss.
            Assert.Contains(hospitals, (n) => n.Kind == NodeKinds.Normal);
            Assert.Contains(hospitals, (n) => n.Kind != NodeKinds.Normal);
            Assert.DoesNotContain(hospitals, (n) => n.Kind == NodeKinds.Sabotage || n.Kind == NodeKinds.Boss || n.Kind == NodeKinds.Crossing);
            // An Asedio, yes (Gameplay, 16/11); a Travesía, never.
            Assert.Contains(hospitals, (n) => n.Kind == NodeKinds.Hold);
            // Nothing else changes: a hospital where a town or a city was.
            for (var k = 0; k < off.Count; k++)
            {
                Assert.Equal(off[k].Select((n) => (n.Id, n.Kind, n.Seed, n.Modifier)), on[k].Select((n) => (n.Id, n.Kind, n.Seed, n.Modifier)));
                foreach (var (a, b) in off[k].Zip(on[k], (a, b) => (a, b))) if (a.Site != b.Site) Assert.True(b.Site == Places.Hospital && (a.Site == Places.Town || a.Site == Places.City));
            }
            Assert.Equal("Saqueo · Hospital", RunTexts.FightName(new RunNode { Kind = NodeKinds.Normal, Site = Places.Hospital }, 1));
        }
    }
}
