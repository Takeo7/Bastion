using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G19 (Arturo, 18/11, cuadros 65–67; .dev-snapshots/gameplay/muebles/CONTRATO.md): a police station's own furniture
    /// and the medical furniture anywhere are searched piece by piece, as a box; their rooms aren't searched whole. The
    /// police station's strip cars are whole cars with a police boot; its dead officer, a body to search. La Lata stands
    /// clear of the building on the maps that are one (Producto: drawn through the wall with the 8 seats' bus).
    /// </summary>
    public class RunPiecesTests
    {
        private static readonly int[] Seeds = Enumerable.Range(1, 20).ToArray();

        private static Pos PosOf(GameState s, int node) => new Pos { X = node % s.Width, Y = node / s.Width % s.Height, Z = node / (s.Width * s.Height) };

        /// <summary>Every piece on the map (Looting.PieceAt), full or empty from the start: where, and what.</summary>
        private static List<(Pos P, string Kind, bool Held)> Pieces(GameState s)
        {
            var out_ = new List<(Pos, string, bool)>();
            for (var z = 0; z < Grid.LevelCount(s); z++)
                for (var y = 0; y < s.Height; y++)
                    for (var x = 0; x < s.Width; x++)
                    {
                        var p = new Pos { X = x, Y = y, Z = z };
                        if (Looting.PieceAt(s, p) is string kind) out_.Add((p, kind, s.Mission.Pieces!.Contains(Grid.NodeOf(s, p))));
                    }
            return out_;
        }

        private static Unit Soldier(GameState s) => s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive);

        [Theory]
        [InlineData(Places.Police)]
        [InlineData(Places.Hospital)]
        [InlineData(Places.Plaza)]
        public void EveryPieceWithSomethingIsASpotToSearch(string place)
        {
            foreach (var seed in Seeds)
            {
                var (_, s) = RunInteriorTests.At(place, NodeKinds.Normal, seed);
                Assert.All(Pieces(s).Where((x) => x.Held), (x) => Assert.True(Looting.SearchSpot(s, x.P) == x.Kind || s.Mission.OutOfReach?.Contains(Grid.NodeOf(s, x.P)) == true, $"{seed} {x.Kind} {x.P.X},{x.P.Y}"));
            }
        }

        [Fact]
        public void APieceKnockedDownFromTheStartNeverHoldsAnything()
        {
            // Producto, 18/11: none comes out of the map so (none in 100 seeds of each site), but were one to, nothing in it.
            var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, 12);
            var racks = Pieces(s).Where((x) => x.Kind == SearchPieces.WeaponRack).Select((x) => x.P).ToList();
            foreach (var p in racks.Take(3)) Events.ApplyEvent(s, new TileChangedEvent { Pos = p, Kind = TileKinds.Low });
            var (_, h) = RunInteriorTests.At(Places.Hospital, NodeKinds.Normal, 12);
            var cabinets = Pieces(h).Where((x) => x.Kind == SearchPieces.MedicalCabinet).Select((x) => x.P).ToList();
            foreach (var p in cabinets.Skip(1)) Events.ApplyEvent(h, new TileChangedEvent { Pos = p, Kind = TileKinds.Low });
            for (var seed = 1; seed <= 50; seed++)
            {
                Setup.PlacePieces(s, new Rng(seed));
                // The one rack still standing is the full one, every time.
                Assert.Equal(new[] { Grid.NodeOf(s, racks[3]) }, s.Mission.Pieces!.Where((n) => racks.Any((p) => Grid.NodeOf(s, p) == n)));
                Setup.PlacePieces(h, new Rng(seed));
                Assert.All(cabinets.Skip(1), (p) => Assert.DoesNotContain(Grid.NodeOf(h, p), h.Mission.Pieces!));
                // Its rooms piece by piece still.
                Assert.NotNull(h.Mission.Pieces);
            }
        }

        [Fact]
        public void TheAmmoBoxesGiveTheirTwoMagazinesToAnyoneInARun()
        {
            // Producto, 18/11 («Dan siempre dos cargadores»): a run's searcher with no count of its own (none such today) too.
            foreach (var seed in Seeds)
            {
                var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, seed);
                var u = Soldier(s);
                var box = Pieces(s).Where((x) => x.Kind == SearchPieces.AmmoCase).Select((x) => x.P).FirstOrDefault((p) => Beside(s, p, u) != null);
                if (box == null) continue;
                u.Pos = Beside(s, box, u)!;
                u.Mags = null;
                var (r, found) = Search(s, u, box);
                Assert.True(r.Ok, r.Error);
                Assert.True(found.Where((g) => g.Kind == GearKinds.Mags).Sum((g) => g.Count) >= 2);
                return;
            }
            Assert.Fail("no ammo box to open");
        }

        [Fact]
        public void APieceABlastKnocksDownHoldsNothingAnyMore()
        {
            // Arte, 18/11: a tall one knocked down to low cover is drawn fallen, its own nowhere; a low one blown away, gone.
            var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, 12);
            var rack = Pieces(s).Single((x) => x.Kind == SearchPieces.WeaponRack && x.Held).P;
            var crate = Pieces(s).First((x) => x.Kind == SearchPieces.AmmoCase || x.Kind == SearchPieces.GunCase).P;
            Assert.NotNull(Looting.SearchSpot(s, rack));
            Events.ApplyEvent(s, new TileChangedEvent { Pos = rack, Kind = TileKinds.Low });
            Events.ApplyEvent(s, new TileChangedEvent { Pos = crate, Kind = TileKinds.Floor });
            Assert.Null(Looting.SearchSpot(s, rack));
            Assert.Null(Looting.SearchSpot(s, crate));
            Assert.Equal(SearchPieces.WeaponRack, Looting.PieceAt(s, rack));
        }

        /// <summary>A free tile `u` can stand on that reaches `p` with the hands (its own if `on`), nobody else on it; null if none.</summary>
        private static Pos? Beside(GameState s, Pos p, Unit u, bool on = false)
        {
            var at = (on ? new[] { new Vec2 { X = 0, Y = 0 } } : Grid.Dirs8).Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z });
            return at.FirstOrDefault((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && (Grid.UnitAt(s, q) == null || Grid.UnitAt(s, q) == u) && Looting.ReachesToOpen(s, q, p));
        }

        /// <summary>`u` searches `p` from where it stands: the result, and what it found (packed and on the floor).</summary>
        private static (CommandResult R, List<Gear> Found) Search(GameState s, Unit u, Pos p)
        {
            u.Ap = Math.Max(u.Ap, 2);
            var r = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = p });
            var e = (r.Events ?? new List<GameEvent>()).OfType<SearchedEvent>().SingleOrDefault();
            var found = (e?.Packed ?? new List<Placed>()).Select((x) => x.Gear).Concat(e?.Floor ?? new List<Gear>()).ToList();
            return (r, found);
        }

        // ------------------------------------------------------------ which hold something

        [Fact]
        public void APoliceStationsArmouryHasOneFullRack()
        {
            var shelves = 0;
            var full = 0;
            foreach (var seed in Seeds)
            {
                var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, seed);
                Assert.NotNull(s.Mission.Pieces);
                var pieces = Pieces(s);
                // Its four racks, one with its rifles; the empty ones no spot to search, the full one a weapon rack.
                var racks = pieces.Where((x) => x.Kind == SearchPieces.WeaponRack).ToList();
                Assert.Equal(4, racks.Count);
                Assert.Single(racks, (x) => x.Held);
                Assert.All(racks, (x) => Assert.Equal(x.Held ? SearchPieces.WeaponRack : null, Looting.SearchSpot(s, x.P)));
                // The rest of the armoury, the lockers and the officer, always.
                foreach (var kind in new[] { SearchPieces.WeaponLocker, SearchPieces.LockerRow }) Assert.Contains(pieces, (x) => x.Kind == kind);
                Assert.All(pieces.Where((x) => x.Kind != SearchPieces.WeaponRack && x.Kind != SearchPieces.EvidenceShelf), (x) => Assert.True(x.Held, x.Kind));
                Assert.Equal(2, pieces.Count((x) => x.Kind == SearchPieces.GunCase || x.Kind == SearchPieces.AmmoCase));
                // Its six evidence shelves, five in six with something.
                var evidence = pieces.Where((x) => x.Kind == SearchPieces.EvidenceShelf).ToList();
                Assert.Equal(6, evidence.Count);
                shelves += evidence.Count;
                full += evidence.Count((x) => x.Held);
                // No box of its own in the armoury any more.
                Assert.DoesNotContain(Enumerable.Range(0, s.Width * s.Height), (i) => Decor.DecorOf(s.Decor![i])?.Kind == DecorKinds.LootWeapons);
            }
            Assert.InRange(full / (double)shelves, 0.7, 0.95);
        }

        [Fact]
        public void OneMedicineCabinetInFourAndOneABuilding()
        {
            var cabinets = 0;
            var full = 0;
            foreach (var seed in Seeds)
            {
                var (_, s) = RunInteriorTests.At(Places.Hospital, NodeKinds.Normal, seed);
                var pieces = Pieces(s);
                var mine = pieces.Where((x) => x.Kind == SearchPieces.MedicalCabinet).ToList();
                cabinets += mine.Count;
                full += mine.Count((x) => x.Held);
                // Its shelves and carts, always.
                Assert.All(pieces.Where((x) => x.Kind != SearchPieces.MedicalCabinet), (x) => Assert.True(x.Held, x.Kind));
                Assert.Contains(pieces, (x) => x.Kind == SearchPieces.MedicalCart);
                foreach (var m in s.Medical!)
                {
                    bool In(Pos p) => p.X >= m.X && p.Y >= m.Y && p.X < m.X + m.W && p.Y < m.Y + m.H;
                    if (mine.Any((x) => In(x.P))) Assert.Contains(mine, (x) => x.Held && In(x.P));
                }
            }
            Assert.InRange(full / (double)cabinets, 0.18, 0.34);
            // A clinic of a town (its cabinets few): one of them at least.
            var clinic = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 23), Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = MissionKinds.Recovery, Squad = Campaign.BuildSquad(Campaign.CreateCampaign(new List<int> { 0 }, new Rng(1))), CampaignMission = new CampaignMission { Id = "m", Name = "m", Story = 1 } });
            Assert.Contains(Pieces(clinic), (x) => x.Kind == SearchPieces.MedicalCabinet && x.Held);
            // The plaza's pharmacy: its shelves.
            var (_, plaza) = RunInteriorTests.At(Places.Plaza, NodeKinds.Normal, 3);
            Assert.Contains(Pieces(plaza), (x) => x.Kind == SearchPieces.MedicalShelf && x.Held && InteriorSite.UseAt(plaza, x.P) == ShopUses.Pharmacy);
        }

        // ------------------------------------------------------------ what each gives

        [Fact]
        public void EachPieceGivesItsOwn()
        {
            const int N = 3000;
            List<(Loot? Loot, int Vest)> Rolls(string kind, bool mags = true, bool pistols = true) => Enumerable.Range(1, N).Select((seed) => Looting.RollPiece(kind, new Rng(seed), mags, pistols)).ToList();
            double Share(List<(Loot? Loot, int Vest)> rolls, Func<(Loot? Loot, int Vest), bool> what) => rolls.Count(what) / (double)N;
            // The armoury: always.
            var rack = Rolls(SearchPieces.WeaponRack);
            Assert.All(rack, (x) => Assert.Contains(x.Loot!.Weapon, new[] { FoundWeaponIds.AssaultRifle, FoundWeaponIds.PumpShotgun }));
            Assert.InRange(Share(rack, (x) => x.Loot!.Weapon == FoundWeaponIds.AssaultRifle), 0.62, 0.71);
            Assert.All(Rolls(SearchPieces.WeaponLocker), (x) => Assert.Equal(FoundWeaponIds.Pistol, x.Loot!.Weapon));
            Assert.All(Rolls(SearchPieces.WeaponLocker, pistols: false), (x) => Assert.Equal(FoundWeaponIds.PumpShotgun, x.Loot!.Weapon));
            var cases = Rolls(SearchPieces.GunCase);
            Assert.All(cases, (x) => Assert.True(x.Loot!.Weapon == FoundWeaponIds.Pistol || x.Loot.Mod != null));
            Assert.InRange(Share(cases, (x) => x.Loot!.Weapon != null), 0.45, 0.55);
            Assert.All(Rolls(SearchPieces.AmmoCase), (x) => Assert.Equal(2, x.Loot!.Mags));
            Assert.All(Rolls(SearchPieces.AmmoCase, mags: false), (x) => Assert.Null(x.Loot));
            // The lockers: a vest, a magazine or nothing.
            var lockers = Rolls(SearchPieces.LockerRow);
            Assert.InRange(Share(lockers, (x) => x.Vest == 100 && x.Loot == null), 0.26, 0.34);
            Assert.InRange(Share(lockers, (x) => x.Loot?.Mags == 1), 0.26, 0.34);
            Assert.InRange(Share(lockers, (x) => x.Loot == null && x.Vest == 0), 0.36, 0.44);
            // An evidence shelf: supplies, a belt's thing or nothing.
            var evidence = Rolls(SearchPieces.EvidenceShelf);
            Assert.InRange(Share(evidence, (x) => x.Loot?.Supplies > 0), 0.26, 0.34);
            Assert.InRange(Share(evidence, (x) => x.Loot?.Item != null), 0.16, 0.24);
            Assert.All(evidence.Where((x) => x.Loot?.Item != null), (x) => Assert.Contains(x.Loot!.Item, Content.BeltItems));
            // The officer: a policeman's body.
            var officer = Rolls(SearchPieces.CorpseOfficer);
            Assert.InRange(Share(officer, (x) => x.Loot?.Weapon == FoundWeaponIds.Pistol), 0.21, 0.29);
            Assert.All(officer, (x) => Assert.Equal(RunLoot.VestChance, x.Vest));
            // The medicines: a cabinet always, a shelf and a cart now and then.
            var cabinet = Rolls(SearchPieces.MedicalCabinet);
            Assert.InRange(Share(cabinet, (x) => x.Loot!.Medicines == 1), 0.62, 0.71);
            Assert.All(cabinet, (x) => Assert.True(x.Loot!.Medicines == 1 || x.Loot.Item == ItemIds.FirstAid));
            var shelf = Rolls(SearchPieces.MedicalShelf);
            Assert.InRange(Share(shelf, (x) => x.Loot?.Medicines == 1), 0.36, 0.44);
            Assert.InRange(Share(shelf, (x) => x.Loot?.Item == ItemIds.FirstAid), 0.12, 0.18);
            var cart = Rolls(SearchPieces.MedicalCart);
            Assert.InRange(Share(cart, (x) => x.Loot?.Medicines == 1), 0.31, 0.39);
            Assert.InRange(Share(cart, (x) => x.Loot == null), 0.61, 0.69);
        }

        [Fact]
        public void APieceIsSearchedAsABoxOnceAndForAll()
        {
            var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, 12);
            var u = Soldier(s);
            var rack = Pieces(s).Single((x) => x.Kind == SearchPieces.WeaponRack && x.Held).P;
            u.Pos = Beside(s, rack, u)!;
            Assert.Contains(Looting.SearchableTiles(s, u), (p) => Grid.SamePos(p, rack));
            var (r, found) = Search(s, u, rack);
            Assert.True(r.Ok, r.Error);
            // Its long gun (and another, now and then, with luck); nothing of the site's more.
            Assert.NotEmpty(found);
            Assert.All(found, (g) => Assert.Contains(g.Type, new[] { FoundWeaponIds.AssaultRifle, FoundWeaponIds.PumpShotgun }));
            Assert.Contains(Grid.NodeOf(s, rack), s.Searched!);
            Assert.False(Looting.IsSearchable(s, rack));
            Assert.DoesNotContain(Looting.SearchableTiles(s, u), (p) => Grid.SamePos(p, rack));
            // An empty rack beside it is nothing to search.
            Assert.All(Pieces(s).Where((x) => x.Kind == SearchPieces.WeaponRack && !x.Held), (x) => Assert.False(Looting.IsSearchable(s, x.P)));
            // A medicine cabinet, its medicine or its kit, nothing of a site's (the plaza gives more on a room's find).
            var (_, h) = RunInteriorTests.At(Places.Hospital, NodeKinds.Normal, 12);
            var nurse = Soldier(h);
            var cabinet = Pieces(h).First((x) => x.Kind == SearchPieces.MedicalCabinet && x.Held && Beside(h, x.P, nurse) != null).P;
            nurse.Pos = Beside(h, cabinet, nurse)!;
            var (r2, found2) = Search(h, nurse, cabinet);
            Assert.True(r2.Ok, r2.Error);
            Assert.NotEmpty(found2);
            Assert.All(found2, (g) => Assert.True(g.Kind == GearKinds.Medicine || g.Type == ItemIds.FirstAid, g.Kind));
        }

        // ------------------------------------------------------------ the rooms

        [Fact]
        public void ARoomWithItsOwnFurnitureIsSearchedPieceByPiece()
        {
            var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, 12);
            var u = Soldier(s);
            // With Rebuscar (Board's patchUp), once.
            u.Perks.Add(PerkIds.PatchUp);
            u.Charges[ChargeIds.PatchUp] = 1;
            var station = s.Interiors!.Single();
            Pos Floor(string use)
            {
                var room = station.Rooms.First((r) => r.Use == use && r.Z == 0);
                return Enumerable.Range(room.Y, room.H).SelectMany((y) => Enumerable.Range(room.X, room.W).Select((x) => new Pos { X = x, Y = y, Z = 0 }))
                    .First((p) => Grid.IsWalkable(Grid.KindAt(s, p)) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null);
            }
            foreach (var use in new[] { PoliceUses.Armory, PoliceUses.Evidence, PoliceUses.Lockers })
            {
                u.Pos = Floor(use);
                Assert.True(Looting.RoomAt(s, u.Pos)!.Pieces, use);
                Assert.Equal(Lore.RoomByPieces, Abilities.AbilityBlocker(s, u, AbilityIds.SearchRoom));
                Assert.Equal(Lore.RoomByPieces, Abilities.AbilityBlocker(s, u, AbilityIds.PatchUp));
            }
            // The interrogation room, as before; the officer's body is no furniture.
            u.Pos = Floor(PoliceUses.Interrogation);
            Assert.False(Looting.RoomAt(s, u.Pos)!.Pieces);
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.SearchRoom));
            // The hospital: its rooms with a cabinet, a shelf or a cart, piece by piece; the rest, whole.
            var (_, h) = RunInteriorTests.At(Places.Hospital, NodeKinds.Normal, 12);
            var rooms = new Dictionary<int, LootRoom>();
            for (var y = 0; y < h.Height; y++)
                for (var x = 0; x < h.Width; x++)
                    if (Looting.RoomAt(h, new Pos { X = x, Y = y, Z = 0 }) is LootRoom r) rooms[r.Id] = r;
            Assert.Contains(rooms.Values, (r) => r.Pieces);
            Assert.Contains(rooms.Values, (r) => !r.Pieces);
        }

        [Fact]
        public void GanchoReachesAPieceTwoOff()
        {
            var (_, s) = RunInteriorTests.At(Places.Hospital, NodeKinds.Normal, 12);
            var u = Soldier(s);
            foreach (var (p, _, _) in Pieces(s).Where((x) => x.Held))
            {
                var two = Enumerable.Range(-2, 5).SelectMany((dy) => Enumerable.Range(-2, 5).Select((dx) => new Pos { X = p.X + dx, Y = p.Y + dy, Z = p.Z }))
                    .FirstOrDefault((q) => Math.Max(Math.Abs(q.X - p.X), Math.Abs(q.Y - p.Y)) == 2 && Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && Grid.UnitAt(s, q) == null && Looting.ReachesToOpen(s, q, p, 2));
                if (two == null) continue;
                u.Pos = two;
                u.Mods = new UnitMods();
                Assert.False(Abilities.CanTargetTile(s, u, AbilityIds.Search, p));
                u.Mods = Content.AddMods(u.Mods, new UnitMods { SearchReach = 1 });
                Assert.True(Abilities.CanTargetTile(s, u, AbilityIds.Search, p));
                var (r, _) = Search(s, u, p);
                Assert.True(r.Ok, r.Error);
                return;
            }
            Assert.Fail("no piece with a tile two off to reach it from");
        }

        // ------------------------------------------------------------ a fight of the 0.13.2's

        [Theory]
        [InlineData("comisaria-0132.json")]
        [InlineData("hospital-0132.json")]
        public void AFightSavedByThe0132GoesOnAsBefore(string file)
        {
            // Saved a turn in by the 0.13.2 (b40b6e0's engine): no pieces; every room searched whole; the armoury's box.
            var s = Bastion.Net.Json.Read<GameState>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "g19", file)));
            Assert.Null(s.Mission.Pieces);
            var u = Soldier(s);
            for (var y = 0; y < s.Height; y++)
                for (var x = 0; x < s.Width; x++)
                {
                    var p = new Pos { X = x, Y = y, Z = 0 };
                    Assert.Null(Looting.PieceAt(s, p));
                    Assert.False(Looting.RoomAt(s, p)?.Pieces ?? false);
                }
            if (s.Interiors?.Any((x) => x.Kind == InteriorKinds.Police) == true)
            {
                var station = s.Interiors.Single();
                var rack = station.Dressing.First((d) => d.Kind == PoliceDetailKinds.WeaponRack);
                Assert.Null(Looting.SearchSpot(s, new Pos { X = rack.X, Y = rack.Y, Z = rack.Z }));
                Assert.Contains(Enumerable.Range(0, s.Width * s.Height), (i) => Decor.DecorOf(s.Decor![i])?.Kind == DecorKinds.LootWeapons);
                // Its armoury, searched whole as it was.
                var room = station.Rooms.First((r) => r.Use == PoliceUses.Armory);
                u.Pos = Enumerable.Range(room.Y, room.H).SelectMany((y) => Enumerable.Range(room.X, room.W).Select((x) => new Pos { X = x, Y = y, Z = 0 }))
                    .First((p) => Grid.IsWalkable(Grid.KindAt(s, p)) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null);
            }
            else
            {
                var cabinet = Enumerable.Range(0, s.Width * s.Height).First((i) => Decor.DecorOf(s.Decor![i])?.Kind == DecorKinds.MedicalCabinet);
                var at = new Pos { X = cabinet % s.Width, Y = cabinet / s.Width, Z = 0 };
                Assert.Null(Looting.SearchSpot(s, at));
                u.Pos = Beside(s, at, u) ?? Beside(s, at, u, true)!;
            }
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.SearchRoom));
            u.Ap = 2;
            var done = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.SearchRoom });
            Assert.True(done.Ok, done.Error);
            Assert.Contains(done.Events!, (e) => e is RoomSearchedEvent);
        }

        // ------------------------------------------------------------ the strip's cars and the officer

        [Fact]
        public void APoliceStationsTwoPatrolCarsHaveTheirBoots()
        {
            foreach (var seed in Enumerable.Range(1, 30))
            {
                var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, seed);
                // Two whole cars on its strip, both police cars; no siren car any more.
                var police = s.Mission.PoliceCar!;
                Assert.Equal(4, police.Count);
                Assert.DoesNotContain(Enumerable.Range(0, s.Width * s.Height), (i) => Decor.DecorOf(s.Decor![i])?.Kind == DecorKinds.SirenCar);
                var reach = new HashSet<int>(Paths.ComputeReach(s, Soldier(s), 1_000_000, ignore: (_) => true).Cost.Keys);
                for (var k = 1; k < police.Count; k += 2)
                {
                    var boot = PosOf(s, police[k]);
                    Assert.Equal(Looting.PoliceBoot, Looting.SearchSpot(s, boot));
                    // A tile it is opened from that the squad walks to.
                    Assert.Contains(Grid.Dirs8, (d) =>
                    {
                        var q = new Pos { X = boot.X + d.X, Y = boot.Y + d.Y, Z = 0 };
                        return Grid.InBounds(s, q.X, q.Y) && reach.Contains(Grid.NodeOf(s, q)) && Flights.CanStopAt(s, q) && Looting.ReachesToSearch(s, q, boot);
                    });
                }
            }
        }

        [Fact]
        public void TheDeadOfficerIsABodyToSearch()
        {
            var tried = 0;
            foreach (var seed in Seeds)
            {
                var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, seed);
                var officer = Pieces(s).Where((x) => x.Kind == SearchPieces.CorpseOfficer).ToList();
                if (officer.Count == 0) continue;
                var at = officer[0].P;
                Assert.True(officer[0].Held);
                Assert.Equal(SearchPieces.CorpseOfficer, Looting.SearchSpot(s, at));
                var u = Soldier(s);
                // From on it, and from beside it.
                if (Grid.UnitAt(s, at) == null)
                {
                    u.Pos = Events.CopyPos(at);
                    Assert.Contains(Looting.SearchableTiles(s, u), (p) => Grid.SamePos(p, at));
                }
                u.Pos = Beside(s, at, u)!;
                var (r, found) = Search(s, u, at);
                Assert.True(r.Ok, r.Error);
                Assert.All(found, (g) => Assert.True(g.Kind == GearKinds.Vest || g.Type == FoundWeaponIds.Pistol, g.Kind));
                Assert.False(Looting.IsSearchable(s, at));
                tried++;
            }
            Assert.True(tried >= 5, $"{tried}");
        }

        // ------------------------------------------------------------ La Lata

        [Fact]
        public void LaLataStandsClearOfTheBuilding()
        {
            // As LaLata.Park draws it: centred on its place, as long as the longer of it and the vehicle (Setup.VehicleLength).
            var lengths = new[] { 3, 4, 6, 9, 2 };
            foreach (var site in new[] { Sites.Police, Sites.Hospital, Sites.Offices, Sites.Shed, Sites.Plaza, Sites.Camping })
            {
                var biome = site == Sites.Shed ? Biomes.Facility : site == Sites.Camping ? Biomes.Wilds : Biomes.City;
                foreach (var vehicle in new[] { 4, 0, 1, 3 })
                {
                    foreach (var seed in Enumerable.Range(1, 30))
                    {
                        var squad = Campaign.BuildSquad(Campaign.CreateCampaign(new List<int> { 0 }, new Rng(seed)));
                        var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, seed, site), Slots = new List<int> { 0 }, Rng = new Rng(seed), Kind = MissionKinds.Scavenge, Squad = squad, Vehicle = vehicle });
                        var bay = s.Mission.EvacBus!;
                        var d = Grid.Dirs4[bay.Heading];
                        double cx = bay.Tiles.Average((t) => t.X + 0.5), cy = bay.Tiles.Average((t) => t.Y + 0.5);
                        var half = Math.Max(lengths[vehicle], bay.Tiles.Count / 2.0) / 2;
                        var own = new HashSet<int>(bay.Tiles.Select((t) => t.Y * s.Width + t.X));
                        for (var y = 0; y < s.Height; y++)
                            for (var x = 0; x < s.Width; x++)
                            {
                                double px = x + 0.5 - cx, py = y + 0.5 - cy;
                                if (Math.Abs(px * d.X + py * d.Y) > half - 0.25 || Math.Abs(-px * d.Y + py * d.X) > 0.9 || own.Contains(y * s.Width + x)) continue;
                                var kind = s.Tiles[y * s.Width + x];
                                Assert.True(kind != TileKinds.Wall && kind != TileKinds.Window && kind != TileKinds.Door && kind != TileKinds.High, $"{site} {vehicle} {seed}: {x},{y} {kind}");
                            }
                    }
                }
            }
        }
    }
}
