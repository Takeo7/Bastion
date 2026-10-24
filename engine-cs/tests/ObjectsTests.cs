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
    /// packages/engine/test/objects.test.ts, test by test.
    /// </summary>
    public class ObjectsTests
    {
        // docs/MAPAS.md, phase 1: objects over several tiles (a car 1 × 3, a bed 1 × 2): the anchor says what it is, every
        // other tile is a part leading to it; they are cover along all of them, and they go as one.

        /// <summary>`executeCommand(s, cmd, ctx(seed))`: slot 0, alone.</summary>
        private static List<GameEvent> Run(GameState s, Command cmd, int seed = 1)
        {
            var r = Exec(s, cmd, 0, seed, new[] { 0 });
            if (!r.Ok) throw new Exception(r.Error);
            return r.Events!;
        }

        private static List<int> NoDecor(GameState s)
        {
            return Enumerable.Repeat(0, s.Width * s.Height).ToList();
        }

        /// <summary>A car facing east, its nose on (x, y) and its body two tiles behind it: low cover, dressed.</summary>
        private static void ParkCar(GameState s, int x, int y)
        {
            var decor = s.Decor ??= NoDecor(s);
            for (var k = 0; k < 3; k++) s.Tiles[y * s.Width + x - k] = TileKinds.Low;
            decor[y * s.Width + x] = Decor.DecorCode(DecorKinds.CarFront, 1);
            // Each part faces the step towards the anchor: east.
            decor[y * s.Width + x - 1] = Decor.DecorCode(DecorKinds.Part, 1);
            decor[y * s.Width + x - 2] = Decor.DecorCode(DecorKinds.Part, 1);
        }

        private static List<int> Sorted(IEnumerable<int> xs)
        {
            // `.sort()` with no comparer sorts as strings: these are all two digits.
            return xs.OrderBy((x) => x.ToString(), StringComparer.Ordinal).ToList();
        }

        [Fact(DisplayName = "objects over several tiles (docs/MAPAS.md, phase 1) › a part leads to its anchor; the anchor knows all its tiles; old cars and blocks are whole too")]
        public void APartLeadsToItsAnchor()
        {
            var s = StateFrom(new[] { "........", "........", "........" });
            ParkCar(s, 4, 1);
            var d = s.Decor!;
            Assert.Equal(1 * 8 + 4, Decor.AnchorOf(d, s.Width, 1 * 8 + 2));
            Assert.Equal(new[] { 10, 11, 12 }, Sorted(Decor.ObjectTiles(d, s.Width, 1 * 8 + 4)));
            var car = Decor.ObjectAt(d, s.Width, 10)!;
            Assert.Equal((DecorKinds.CarFront, 1, 12), (car.Kind, car.Facing, car.Anchor));
            // A part that leads nowhere is nothing.
            d[0] = Decor.DecorCode(DecorKinds.Part, 3);
            Assert.Equal(-1, Decor.AnchorOf(d, s.Width, 0));
            Assert.Null(Decor.ObjectAt(d, s.Width, 0));
            // A car of before: its two halves.
            d[2 * 8 + 6] = Decor.DecorCode(DecorKinds.CarFront, 1);
            d[2 * 8 + 5] = Decor.DecorCode(DecorKinds.CarRear, 1);
            var old = Decor.ObjectAt(d, s.Width, 2 * 8 + 5)!;
            Assert.Equal((DecorKinds.CarFront, 2 * 8 + 6), (old.Kind, old.Anchor));
            Assert.Equal(new[] { 22, 21 }, old.Tiles);
            // A lorry of before: the block of its kind.
            foreach (var i in new[] { 0 * 8 + 6, 0 * 8 + 7 }) d[i] = Decor.DecorCode(DecorKinds.Truck, 0);
            Assert.Equal(new[] { 6, 7 }, Decor.ObjectAt(d, s.Width, 7)?.Tiles);
            // Anything else on one tile stands alone.
            d[2 * 8 + 1] = Decor.DecorCode(DecorKinds.Bed, 2);
            Assert.Equal(new[] { 17 }, Decor.ObjectAt(d, s.Width, 17)?.Tiles);
        }

        [Fact(DisplayName = "objects over several tiles (docs/MAPAS.md, phase 1) › a parked car is cover along its whole length")]
        public void AParkedCarIsCoverAlongItsWholeLength()
        {
            var s = StateFrom(new[] { "........", "........", "........" });
            ParkCar(s, 5, 1);
            // Behind each of its tiles, from across it.
            foreach (var x in new[] { 3, 4, 5 }) Assert.Equal(1, Cover.CoverAgainst(s, P(x, 2), new Vec2 { X = x, Y = 0 }));
        }

        [Fact(DisplayName = "objects over several tiles (docs/MAPAS.md, phase 1) › a grenade that reaches one end of a car takes all of it; the partner replays it the same")]
        public void AGrenadeThatReachesOneEndOfACarTakesAllOfIt()
        {
            var s = StateFrom(new[] { "...........", "...........", "...........", "...........", "..........." });
            ParkCar(s, 9, 2);
            AddUnit(s, "s1", TemplateIds.Assault, 1, 2);
            var before = Events.CloneState(s);
            // At the car's rear: a radius that never reaches its nose, four tiles further on.
            var reached = Abilities.BlastTiles(s, AbilityIds.Grenade, P(5, 2), s.Units[0]).Select((p) => p.X).ToList();
            Assert.Contains(7, reached);
            Assert.DoesNotContain(9, reached);
            var events = Run(s, Ability("s1", AbilityIds.Grenade, tile: P(5, 2)));
            foreach (var x in new[] { 7, 8, 9 }) Assert.True(Grid.KindAt(s, P(x, 2)) == TileKinds.Floor, $"{x}");
            foreach (var e in events) Events.ApplyEvent(before, e);
            Assert.Equal(s.Tiles, before.Tiles);
        }

        [Fact(DisplayName = "objects over several tiles (docs/MAPAS.md, phase 1) › in the base each piece stands alone: a grenade takes only the log piles it reaches, not the whole row")]
        public void InTheBaseEachPieceStandsAlone()
        {
            var s = StateFrom(new[] { "....................", "....................", "....................", "....................", "...................." });
            var decor = s.Decor = NoDecor(s);
            for (var x = 2; x < 18; x++)
            {
                s.Tiles[2 * s.Width + x] = TileKinds.Low;
                decor[2 * s.Width + x] = Decor.DecorCode(DecorKinds.LogPile, 0);
            }
            s.Base = new SiegeBase { Pieces = new List<BasePiece>() };
            AddUnit(s, "s1", TemplateIds.Assault, 2, 4);
            var reached = new HashSet<int>(Abilities.BlastTiles(s, AbilityIds.Grenade, P(2, 1), s.Units[0]).Where((p) => p.Y == 2).Select((p) => p.X));
            Run(s, Ability("s1", AbilityIds.Grenade, tile: P(2, 1)));
            for (var x = 2; x < 18; x++) Assert.True(Grid.KindAt(s, P(x, 2)) == (reached.Contains(x) ? TileKinds.Floor : TileKinds.Low), $"{x}");
            Assert.Equal(TileKinds.Low, Grid.KindAt(s, P(17, 2)));
        }

        [Fact(DisplayName = "objects over several tiles (docs/MAPAS.md, phase 1) › a silenced patrol car takes the siren's tile into it, a part of it")]
        public void ASilencedPatrolCarTakesTheSirensTileIntoIt()
        {
            var s = StateFrom(new[] { "......", "......", "......" });
            s.Mission.Kind = MissionKinds.Sabotage;
            s.Mission.Alarm = Alarms.Police;
            s.Mission.ObjectiveUnit = "relay";
            s.Mission.TurnsLeft = 5;
            var relay = AddUnit(s, "relay", TemplateIds.Relay, 1, 1, extra: (u) => u.Objective = true);
            // Its nose (sirenCar, facing east) and a part beyond it: with the siren's tile, 1 × 3.
            s.Decor = NoDecor(s);
            s.Tiles[1 * 6 + 2] = s.Tiles[1 * 6 + 3] = TileKinds.High;
            s.Decor[1 * 6 + 2] = Decor.DecorCode(DecorKinds.SirenCar, 1);
            s.Decor[1 * 6 + 3] = Decor.DecorCode(DecorKinds.Part, 3);
            AddUnit(s, "s1", TemplateIds.Assault, 0, 0);
            var sim = NewSim(s, 1, 0);
            sim.Damage(relay, 99, 0);
            Assert.Equal(TileKinds.High, Grid.KindAt(s, relay.Pos));
            var car = Decor.ObjectAt(s.Decor, s.Width, 1 * 6 + 1)!;
            Assert.Equal((DecorKinds.SirenCar, 1 * 6 + 2), (car.Kind, car.Anchor));
            Assert.Equal(new[] { 7, 8, 9 }, Sorted(Decor.ObjectTiles(s.Decor, s.Width, 1 * 6 + 2)));
        }

        [Fact(DisplayName = "objects over several tiles (docs/MAPAS.md, phase 1) › a room is the kind its pieces make it, each counted once whatever tiles it takes")]
        public void ARoomIsTheKindItsPiecesMakeIt()
        {
            // A bed over two tiles and two desks: an office, not a bedroom (by tiles it would be two and two).
            var s = StateFrom(new[] { "#######", "#.....#", "#.....#", "#.....#", "#######" });
            s.Ground = s.Tiles.Select((_) => 2).ToList();
            s.Decor = NoDecor(s);
            void Put(int x, int y, int code)
            {
                s.Tiles[y * s.Width + x] = TileKinds.Low;
                s.Decor![y * s.Width + x] = code;
            }
            Put(1, 1, Decor.DecorCode(DecorKinds.Bed, 2));
            Put(1, 2, Decor.DecorCode(DecorKinds.Part, 0));
            Put(5, 1, Decor.DecorCode(DecorKinds.Desk, 2));
            Put(5, 3, Decor.DecorCode(DecorKinds.Desk, 0));
            Assert.Equal(Spots.Office, Looting.RoomAt(s, P(3, 2))?.Spot);
        }

        [Fact(DisplayName = "supermarkets (playtest 21/10, #19) › a stocked one always gives food; a picked one is a shop, maybe nothing")]
        public void AStockedOneAlwaysGivesFood()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 2), Slots = new List<int> { 0 }, Rng = new Rng(2) });
            var m = s.Markets![0];
            var room = Looting.RoomAt(s, P(m.X + 2, m.Y + 2))!;
            Assert.NotNull(room);
            foreach (var stock in new[] { MarketStocks.Full, MarketStocks.Picked })
            {
                m.Stock = stock;
                Assert.Equal(stock, Looting.MarketStock(s, room));
            }
            List<Loot?> Rolls(string stock) => Enumerable.Range(0, 60).Select((k) => Looting.RollRoom(room, 2, new Rng(k + 1), false, false, stock)).ToList();
            foreach (var loot in Rolls(MarketStocks.Full))
            {
                Assert.True(loot?.Food >= 6);
                Assert.True(loot?.Food <= 10);
            }
            Assert.Contains(Rolls(MarketStocks.Picked), (loot) => loot == null);
            // Elsewhere, no stock.
            var @out = Looting.RoomAt(s, s.Units.Find((u) => u.Team == Teams.Xcom)!.Pos);
            if (@out != null) Assert.Null(Looting.MarketStock(s, @out));
        }
    }
}
