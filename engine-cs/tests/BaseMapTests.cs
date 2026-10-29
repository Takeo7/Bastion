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
    /// packages/engine/test/basemap.test.ts, whole: the base (base.ts) and the siege on it (setup.ts createMission with a
    /// base's map, siegeWaves, the rules). Nothing left out.
    /// </summary>
    public class BaseMapTests
    {
        // Fase 12, the new base (docs/FASE12.md §11): a clearing in the pines with a caravan, and what is built on it.

        /// <summary>A base with no trees, for the rules of building.</summary>
        private static BaseState Bare()
        {
            return new BaseState { Width = Base.BaseMap, Height = Base.BaseMap, Trees = new List<int>(), Pieces = new List<BasePiece>(), NextPiece = 1 };
        }

        /// <summary>`{ ...base, level }`: the same base (its lists shared), at another level.</summary>
        private static BaseState AtLevel(BaseState @base, int level)
        {
            return new BaseState
            {
                Width = @base.Width,
                Height = @base.Height,
                Trees = @base.Trees,
                Felled = @base.Felled,
                Pieces = @base.Pieces,
                NextPiece = @base.NextPiece,
                Level = level,
            };
        }

        /// <summary>`createMission({ map: { kind: 'base', base }, slots: [0], rng: new Rng(seed) })`, and what `edit` adds to the options.</summary>
        private static GameState Siege(BaseState @base, int seed, Action<MissionOptions>? edit = null)
        {
            var opts = new MissionOptions { Map = MapSource.OfBase(@base), Slots = new List<int> { 0 }, Rng = new Rng(seed) };
            edit?.Invoke(opts);
            return Setup.CreateMission(opts);
        }

        /// <summary>`s.breakables?.[i]`: what the tile holds, null (undefined) if nothing.</summary>
        private static int? Breakable(GameState s, int i)
        {
            return s.Breakables != null && s.Breakables.TryGetValue(i, out var hp) ? hp : (int?)null;
        }

        /// <summary>toEqual: the same JSON (Step2Json.Diff: null and missing alike, numbers by value).</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            Assert.True(diff == null, diff);
        }

        // ------------------------------------------------------------ the clearing

        [Fact(DisplayName = "the clearing › starts with the caravan in the middle of a clearing, a track out to the south")]
        public void StartsWithTheCaravanInTheMiddleOfAClearing()
        {
            var @base = Base.FreshBase(new Rng(1));
            Assert.Equal(Base.BaseMap, @base.Width);
            Assert.Equal(new[] { PieceIds.Caravan }, @base.Pieces.Select((p) => p.Piece));
            var caravan = @base.Pieces[0];
            Assert.Equal(Base.Pieces[PieceIds.Caravan].Hp, caravan.Hp);
            Assert.True(Math.Abs(caravan.X + 2 - Base.BaseMap / 2.0) <= 1, $"caravan x {caravan.X}");
            // Round the caravan, clear ground.
            for (var y = caravan.Y - 4; y <= caravan.Y + 5; y++) for (var x = caravan.X - 3; x <= caravan.X + 6; x++) Assert.False(Base.TreeAt(@base, x, y), $"tree at {x},{y}");
            // And La Lata's way in, from the south (one track since 28/10; the dead come from between the pines).
            var mid = (int)Math.Floor((Base.BaseMap - 1) / 2.0);
            for (var i = mid; i < Base.BaseMap; i++) Assert.False(Base.TreeAt(@base, mid, i), $"tree at {mid},{i}");
            Assert.Contains(Enumerable.Range(0, mid - 8), (i) => Base.TreeAt(@base, mid, i) || Base.TreeAt(@base, i, mid));
        }

        [Fact(DisplayName = "the clearing › is its own every time: sometimes thicker pines, sometimes more glades")]
        public void IsItsOwnEveryTime()
        {
            var shares = new[] { 1, 2, 3, 4, 5, 6, 7, 8 }.Select((seed) => (double)Base.FreshBase(new Rng(seed)).Trees.Count / (Base.BaseMap * Base.BaseMap)).ToList();
            Assert.Equal(8, new HashSet<double>(shares).Count);
            Assert.True(shares.Min() > 0.15, $"min {shares.Min()}");
            Assert.True(shares.Max() < 0.7, $"max {shares.Max()}");
            Assert.True(shares.Max() - shares.Min() > 0.05, $"spread {shares.Max() - shares.Min()}");
            // The same seed, the same ground.
            Same(Base.FreshBase(new Rng(3)), Base.FreshBase(new Rng(3)));
        }

        // ------------------------------------------------------------ the Bastion's lot (18/10 #40)

        [Fact(DisplayName = "the Bastion's lot (18/10 #40) › is 9 × 9 round the caravan, nothing else is built on it, and the building on it grows with the level")]
        public void IsNineByNineRoundTheCaravan()
        {
            var @base = Base.FreshBase(new Rng(1));
            var caravan = @base.Pieces[0];
            Same(new PlanRect { X = caravan.X - 2, Y = caravan.Y - 3, W = 9, H = 9 }, Base.BastionLot(@base));
            Assert.Equal("Ahí no: ese sitio es para el Bastión, que crece con cada nivel.", Base.PlaceBlocker(@base, PieceIds.Sandbags, caravan.X - 2, caravan.Y - 3, 0));
            Assert.Null(Base.PlaceBlocker(@base, PieceIds.Sandbags, caravan.X - 3, caravan.Y - 3, 0));
            // Each stage, two levels: the caravan where it is, the mobile home, the barracón, the house.
            int[] Box(int level)
            {
                var cells = Base.BastionFootprint(AtLevel(@base, level));
                var xs = cells.Select((c) => c.X).ToList();
                var ys = cells.Select((c) => c.Y).ToList();
                return new[] { xs.Min() - caravan.X, ys.Min() - caravan.Y, xs.Max() - xs.Min() + 1, ys.Max() - ys.Min() + 1 };
            }
            // (docs/MAPAS.md §4.9: 2 × 5, 3 × 7, 5 × 7 and 7 × 7, in the middle of the lot.)
            Assert.Equal(new[]
            {
                new[] { 0, 0, 5, 2 },
                new[] { 0, 0, 5, 2 },
                new[] { -1, 0, 7, 3 },
                new[] { -1, -1, 7, 5 },
                new[] { -1, -2, 7, 7 },
                new[] { -1, -2, 7, 7 },
            }, new[] { 1, 2, 3, 5, 7, 8 }.Select(Box).ToArray());
            // In a siege, the building is solid and holds the caravan's strength; the squad stands outside it.
            var s = Siege(AtLevel(@base, 7), 2);
            foreach (var t in Base.BastionFootprint(AtLevel(@base, 7)))
            {
                Assert.Equal(TileKinds.Wall, s.Tiles[t.Y * s.Width + t.X]);
                Assert.Equal(Base.Pieces[PieceIds.Caravan].Hp, Breakable(s, t.Y * s.Width + t.X));
            }
            foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom)) Assert.Equal(TileKinds.Floor, s.Tiles[u.Pos.Y * s.Width + u.Pos.X]);
        }

        [Fact(DisplayName = "the Bastion's lot (18/10 #40) › beside the lot is beside the caravan")]
        public void BesideTheLotIsBesideTheCaravan()
        {
            var @base = Base.FreshBase(new Rng(1));
            var lot = Base.BastionLot(@base)!;
            var bags = Base.PlacePiece(@base, PieceIds.Sandbags, lot.X - 1, lot.Y + 2, 0);
            Same(new[] { @base.Pieces[0] }, Base.BesidePieces(@base, bags));
            Same(new[] { bags }, Base.BesidePieces(@base, @base.Pieces[0]));
        }

        // ------------------------------------------------------------ building

        [Fact(DisplayName = "building › turns a piece about its own box")]
        public void TurnsAPieceAboutItsOwnBox()
        {
            List<string> At(int rot) => Base.PieceCells(PieceIds.Caravan, 10, 10, rot).Select((c) => $"{c.X},{c.Y}").OrderBy((k) => k, StringComparer.Ordinal).ToList();
            Assert.Equal(new[] { "10,10", "10,11", "11,10", "11,11", "12,10", "12,11", "13,10", "13,11", "14,10", "14,11" }, At(0));
            // A quarter turn: two across, five deep.
            Assert.Equal(new[] { "10,10", "10,11", "10,12", "10,13", "10,14", "11,10", "11,11", "11,12", "11,13", "11,14" }, At(1));
            // The tower's ladder goes in front, in the middle, wherever it faces.
            PlacedCell Ladder(int rot) => Base.PieceCells(PieceIds.Tower, 10, 10, rot).First((c) => c.Tile == TileKinds.Ladder);
            Assert.Equal(new[] { 11, 13 }, new[] { Ladder(0).X, Ladder(0).Y });
            Assert.Equal(new[] { 10, 11 }, new[] { Ladder(1).X, Ladder(1).Y });
            Assert.Equal(new[] { 11, 10 }, new[] { Ladder(2).X, Ladder(2).Y });
            Assert.Equal(new[] { 13, 11 }, new[] { Ladder(3).X, Ladder(3).Y });
            Assert.Equal(9, Base.PieceCells(PieceIds.Tower, 10, 10, 0).Count((c) => c.Elev == 1));
            // One built before the base grew keeps its old shape (docs/MAPAS.md phase 6).
            Assert.Equal(4, Base.PieceCells(PieceIds.Tower, 10, 10, 0, new[] { 2, 3 }).Count((c) => c.Elev == 1));
            Assert.Equal(12, Base.PieceCells(PieceIds.Quarters, 10, 10, 0, new[] { 4, 3 }).Count);
        }

        [Fact(DisplayName = "building › only on cleared, free ground off the edge, and the unique only once")]
        public void OnlyOnClearedFreeGround()
        {
            var @base = Bare();
            Assert.Equal("Ahí no: es el borde del claro.", Base.PlaceBlocker(@base, PieceIds.Palisade, 1, 10, 0));
            Assert.Null(Base.PlaceBlocker(@base, PieceIds.Palisade, Base.BaseEdge, 10, 0));
            Assert.Equal("Ahí no: es el borde del claro.", Base.PlaceBlocker(@base, PieceIds.Quarters, Base.BaseMap - Base.BaseEdge - 3, 10, 0));
            @base.Trees = new List<int> { 10 * Base.BaseMap + 12 };
            Assert.Equal("Hay árboles: tala antes.", Base.PlaceBlocker(@base, PieceIds.Quarters, 10, 10, 0));
            Assert.Null(Base.PlaceBlocker(@base, PieceIds.Quarters, 10, 11, 0));
            Base.PlacePiece(@base, PieceIds.Quarters, 10, 11, 0);
            Assert.Equal("Ahí ya hay algo.", Base.PlaceBlocker(@base, PieceIds.Sandbags, 13, 13, 0));
            // More than one barracón, one radio.
            Assert.Null(Base.PlaceBlocker(@base, PieceIds.Quarters, 20, 20, 0));
            Base.PlacePiece(@base, PieceIds.Radio, 20, 5, 0);
            Assert.Equal("Ya está en la base.", Base.PlaceBlocker(@base, PieceIds.Radio, 5, 20, 0));
        }

        [Fact(DisplayName = "building › a new piece is whole: a building at level 1, a trap set")]
        public void ANewPieceIsWhole()
        {
            var @base = Bare();
            var hut = Base.PlacePiece(@base, PieceIds.Infirmary, 5, 5, 1);
            Assert.Equal(("b1", PieceIds.Infirmary, 1, Base.Pieces[PieceIds.Infirmary].Hp, (int?)1), (hut.Id, hut.Piece, hut.Rot, hut.Hp, hut.Level));
            var mine = Base.PlacePiece(@base, PieceIds.Mine, 3, 3, 0);
            Assert.Equal(("b2", (int?)1), (mine.Id, mine.Charges));
            Assert.Equal(Base.Pieces[PieceIds.Turret].Charges, Base.PlacePiece(@base, PieceIds.Turret, 3, 4, 0).Charges);
            // Six across and five deep, turned: five across and six deep.
            Assert.Same(hut, Base.PieceAt(@base, 9, 10));
            Assert.Null(Base.PieceAt(@base, 10, 6));
        }

        [Fact(DisplayName = "building › felling a tree clears its tile (what it costs, the campaign charges)")]
        public void FellingATreeClearsItsTile()
        {
            var @base = Bare();
            @base.Trees = new List<int> { 10 * Base.BaseMap + 10, 10 * Base.BaseMap + 11 };
            Assert.Equal("Ahí no hay ningún árbol.", Base.FellBlocker(@base, 12, 10));
            Assert.Null(Base.FellBlocker(@base, 11, 10));
            Assert.Equal(1, Base.FellTree(@base, 11, 10));
            Assert.False(Base.TreeAt(@base, 11, 10));
            Assert.True(Base.TreeAt(@base, 10, 10));
            Assert.Equal(0, Base.FellTree(@base, 11, 10));
            // A stump stays where it was felled, till something is built on it.
            Assert.Equal(new[] { 10 * Base.BaseMap + 11 }, @base.Felled);
            Base.PlacePiece(@base, PieceIds.Sandbags, 11, 10, 0);
            Assert.Equal(new int[0], @base.Felled);
        }

        [Fact(DisplayName = "building › mending costs what was lost; a spent trap is set again with piezas; dismantling gives half back, never the caravan")]
        public void MendingCostsWhatWasLost()
        {
            var @base = Bare();
            var wall = Base.PlacePiece(@base, PieceIds.Wall, 5, 5, 0);
            Same(new PieceCost { Supplies = 0, Parts = 0 }, Base.RepairCost(wall));
            // (36 halves whole: `/ 2` as in TypeScript.)
            Assert.Equal(0, Base.Pieces[PieceIds.Wall].Hp % 2);
            wall.Hp = Base.Pieces[PieceIds.Wall].Hp / 2;
            Same(new PieceCost { Supplies = (int)Math.Ceiling(Base.Pieces[PieceIds.Wall].Cost.Supplies / 2.0), Parts = 1 }, Base.RepairCost(wall));
            var mine = Base.PlacePiece(@base, PieceIds.Mine, 6, 5, 0);
            Assert.Equal(0, Base.RearmCost(mine));
            mine.Charges = 0;
            Assert.Equal(1, Base.RearmCost(mine));
            var gate = Base.PlacePiece(@base, PieceIds.Gate, 7, 5, 0);
            Assert.Equal(5, Base.DismantleRefund(gate));
            var caravan = Base.PlacePiece(@base, PieceIds.Caravan, 20, 20, 0);
            Assert.Equal("La autocaravana no se desmonta.", Base.DismantleBlocker(caravan));
            Assert.Null(Base.DismantleBlocker(gate));
        }

        [Fact(DisplayName = "building › «junto a» is side by side on the map")]
        public void JuntoAIsSideBySide()
        {
            var @base = Bare();
            var infirmary = Base.PlacePiece(@base, PieceIds.Infirmary, 10, 10, 0);
            var quarters = Base.PlacePiece(@base, PieceIds.Quarters, 14, 10, 0);
            var far = Base.PlacePiece(@base, PieceIds.Workshop, 10, 20, 0);
            Same(new[] { quarters }, Base.BesidePieces(@base, infirmary));
            Same(new BasePiece[0], Base.BesidePieces(@base, far));
        }

        // ------------------------------------------------------------ the siege of the base

        /// <summary>A base with a ring of palisades round the caravan, a gate in it, a tower and some sandbags.</summary>
        private static BaseState Walled(int seed = 1)
        {
            var @base = Base.FreshBase(new Rng(seed));
            var caravan = @base.Pieces[0];
            // Round the Bastion's lot, a tile off; the tower outside the ring.
            var lot = Base.BastionLot(@base)!;
            var (x0, y0, x1, y1) = (lot.X - 2, lot.Y - 2, lot.X + lot.W + 1, lot.Y + lot.H + 1);
            for (var x = x0; x <= x1; x++)
            {
                foreach (var y in new[] { y0, y1 }) Base.PlacePiece(@base, x == caravan.X + 1 && y == y0 ? PieceIds.Gate : PieceIds.Palisade, x, y, 0);
            }
            for (var y = y0 + 1; y < y1; y++) foreach (var x in new[] { x0, x1 }) Base.PlacePiece(@base, PieceIds.Palisade, x, y, 0);
            Base.PlacePiece(@base, PieceIds.Sandbags, lot.X, y1 - 1, 0);
            Base.PlacePiece(@base, PieceIds.Tower, x1 + 2, y0, 0);
            return @base;
        }

        [Fact(DisplayName = "the siege of the base › every side has ways in from between the pines, joined to the clearing (28/10)")]
        public void EverySideHasWaysInJoinedToTheClearing()
        {
            // One track since 28/10, and the dead never come down it while there's another way: a side whose ways stopped in
            // a glade the pines close off let no wave land, and the siege never ended.
            for (var seed = 1; seed <= 12; seed++)
            {
                var @base = Base.FreshBase(new Rng((uint)seed));
                var l = Base.BaseLayout(@base);
                var open = new HashSet<int>();
                var queue = new List<Vec2> { l.Spawns[0] };
                open.Add(l.Spawns[0].Y * l.Width + l.Spawns[0].X);
                while (queue.Count > 0)
                {
                    var p = queue[queue.Count - 1];
                    queue.RemoveAt(queue.Count - 1);
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int x = p.X + dx, y = p.Y + dy, i = y * l.Width + x;
                        if (x < 0 || y < 0 || x >= l.Width || y >= l.Height || open.Contains(i) || l.Tiles[i] != TileKinds.Floor) continue;
                        open.Add(i);
                        queue.Add(new Vec2 { X = x, Y = y });
                    }
                }
                for (var side = 0; side < 4; side++)
                {
                    var span = side % 2 == 0 ? l.Width : l.Height;
                    var ways = Enumerable.Range(span / 4, span / 2).Count((k) =>
                    {
                        var (x, y) = side == 0 ? (k, 0) : side == 1 ? (l.Width - 1, k) : side == 2 ? (k, l.Height - 1) : (0, k);
                        return open.Contains(y * l.Width + x) && !Base.OnTrack(l.Width, l.Height, x, y);
                    });
                    Assert.True(ways > 0, $"seed {seed}: no way in by side {side}");
                }
            }
        }

        [Fact(DisplayName = "the siege of the base › is a siege on the base as it stands: pines, pieces on their tiles, what each holds, the squad by the caravan")]
        public void IsASiegeOnTheBaseAsItStands()
        {
            var @base = Walled();
            var s = Siege(@base, 2);
            Assert.Equal("base", s.MapId);
            Assert.Equal(MissionKinds.Elimination, s.Mission.Kind);
            // `{ hold: expect.any(Number) }`: Siege.Hold is a number.
            Assert.NotNull(s.Mission.Siege);
            int At(int x, int y) => y * s.Width + x;
            var tree = @base.Trees[0];
            Assert.Equal(TileKinds.High, s.Tiles[tree]);
            Assert.Equal(DecorKinds.Tree, Decor.DecorOf(s.Decor?[tree])?.Kind);
            var palisade = @base.Pieces.First((p) => p.Piece == PieceIds.Palisade);
            Assert.Equal(TileKinds.Wall, s.Tiles[At(palisade.X, palisade.Y)]);
            Assert.Equal(Base.Pieces[PieceIds.Palisade].Hp, Breakable(s, At(palisade.X, palisade.Y)));
            var gate = @base.Pieces.First((p) => p.Piece == PieceIds.Gate);
            Assert.Equal(TileKinds.Door, s.Tiles[At(gate.X, gate.Y)]);
            // The tower's deck, a storey up, with its ladder; the deck holds nothing to break.
            var tower = @base.Pieces.First((p) => p.Piece == PieceIds.Tower);
            Assert.Equal(1, s.Elev[At(tower.X, tower.Y)]);
            Assert.Equal(TileKinds.Ladder, s.Tiles[At(tower.X + 1, tower.Y + 3)]);
            Assert.Null(Breakable(s, At(tower.X, tower.Y)));
            // The track, trodden earth (to the south); the rest, open ground.
            var mid = (int)Math.Floor((s.Width - 1) / 2.0);
            Assert.Equal((int?)3, s.Ground?[At(mid, s.Height - 4)]);
            Assert.Equal((int?)0, s.Ground?[At(mid, 3)]);
            Assert.Equal((int?)0, s.Ground?[At(3, 3)]);
            // The base goes with it, for the client to draw and the campaign to take back; its stumps too.
            Same(@base.Pieces, s.Base?.Pieces);
            Assert.Null(s.Base?.Felled);
            Base.FellTree(@base, tree % @base.Width, (int)Math.Floor((double)tree / @base.Width));
            Assert.Equal(new[] { tree }, Siege(@base, 2).Base?.Felled);
            Assert.NotSame(@base.Pieces[0], s.Base?.Pieces[0]);
            // The squad round the caravan, inside the ring.
            var caravan = @base.Pieces[0];
            foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom))
            {
                var d = Js.Hypot(u.Pos.X - caravan.X - 2, u.Pos.Y - caravan.Y - 1);
                Assert.True(d < 4, $"{u.Id} at {u.Pos.X},{u.Pos.Y}: {d}");
            }
        }

        [Fact(DisplayName = "the siege of the base › the dead break the palisade or the gate to get at the soldiers")]
        public void TheDeadBreakThePalisadeOrTheGate()
        {
            var s = Siege(Walled(3), 4, (o) => o.Difficulty = 4);
            var rng = new Rng(5);
            var events = new List<GameEvent>();
            for (var turn = 0; turn < 8 && s.Outcome == null; turn++)
            {
                var r = Exec(s, new EndTurnCommand { Ready = true }, rng);
                if (r.Ok) events.AddRange(r.Events!);
            }
            var blows = Of<SmashedEvent>(events);
            Assert.True(blows.Count > 0, "no blows");
            var kinds = new[] { TileKinds.Wall, TileKinds.Door, TileKinds.Floor, TileKinds.Low };
            Assert.True(blows.All((e) => kinds.Contains(s.Tiles[e.Tile.Y * s.Width + e.Tile.X])));
        }

        [Fact(DisplayName = "the siege of the base › a campaign siege brings as many as its clock says, and someone by every side it heard")]
        public void ACampaignSiegeBringsAsManyAsItsClockSays()
        {
            foreach (var (count, sides) in new[] { (14, new[] { 1, 3 }), (20, new[] { 0, 1, 2 }), (31, new[] { 2 }), (10, new[] { 0, 3 }) })
            {
                for (var seed = 1; seed <= 20; seed++)
                {
                    var waves = Setup.SiegeWaves(2, new Rng(seed), new SiegePlan { Count = count, Sides = sides.ToList() });
                    Assert.Equal(count, waves.SelectMany((w) => w.Units).Count());
                    var heard = new HashSet<int?>(waves.Select((w) => w.Side));
                    Assert.True(heard.SetEquals(sides.Select((x) => (int?)x)), $"count {count}, seed {seed}: sides {string.Join(",", heard)}");
                    // More each time, and the five specials as ever.
                    var sizes = new[] { 1, 2, 3, 5, 6 }.Select((t) => waves.Where((w) => w.Turn == t).Sum((w) => w.Units.Count)).ToList();
                    Assert.True(sizes.Select((n, i) => i == 0 || n >= sizes[i - 1]).All((x) => x), $"count {count}, seed {seed}: sizes {string.Join(",", sizes)}");
                    Assert.Equal(
                        new[] { TemplateIds.Bloater, TemplateIds.Mec, TemplateIds.Sectoid, TemplateIds.Xenoid, TemplateIds.Xenoid },
                        waves.SelectMany((w) => w.Units).Where((u) => u != TemplateIds.Zombie).OrderBy((u) => u, StringComparer.Ordinal).ToList());
                }
            }
        }

        [Fact(DisplayName = "the siege of the base › a wave never comes in where the pines close it off from the base")]
        public void AWaveNeverComesInWhereThePinesCloseItOff()
        {
            // 18/10 test: a wave landed in a glade by the east edge with pines all round, and stood there.
            var landed = 0;
            for (var seed = 1; seed <= 6; seed++)
            {
                var @base = Walled(seed);
                var s = Siege(@base, seed, (o) => o.SiegePlan = new SiegePlan { Count = 30, Sides = new List<int> { 0, 1, 2, 3 } });
                // The squad stays put, by the caravan, behind its ring.
                var at = new Dictionary<string, int>();
                var rng = new Rng(seed);
                for (var turn = 0; turn < 10 && s.Outcome == null; turn++)
                {
                    var r = Exec(s, new EndTurnCommand { Ready = true }, rng);
                    if (r.Ok) foreach (var e in r.Events!) if (e is SpawnedEvent spawned) foreach (var u in spawned.Units) at[u.Id] = u.Pos.Y * s.Width + u.Pos.X;
                }
                // Joined to the caravan by ground or by what can be broken; the pines never are.
                var caravan = @base.Pieces[0];
                var open = new HashSet<int> { caravan.Y * s.Width + caravan.X - 1 };
                var queue = open.ToList();
                while (queue.Count > 0)
                {
                    var i = queue[queue.Count - 1];
                    queue.RemoveAt(queue.Count - 1);
                    foreach (var j in new[] { i - 1, i + 1, i - s.Width, i + s.Width })
                    {
                        if (j < 0 || j >= s.Tiles.Count || open.Contains(j) || Math.Abs((j % s.Width) - (i % s.Width)) > 1) continue;
                        if (s.Tiles[j] == TileKinds.High && Breakable(s, j) == null) continue;
                        open.Add(j);
                        queue.Add(j);
                    }
                }
                foreach (var i in at.Values) Assert.True(open.Contains(i), $"seed {seed}: landed at {i % s.Width},{i / s.Width}, cut off");
                landed += at.Count;
            }
            Assert.True(landed > 50, $"landed {landed}");
            // Sixty enemy turns of hordes of thirty a side, each of the dead finding its way round the rest (19/10): time for it.
        }

        // ------------------------------------------------------------ the defences that act on their own

        /// <summary>A bare base: a caravan, and the defences given, all round (20, 20).</summary>
        private static GameState Armed(Action<BaseState> build)
        {
            var @base = Bare();
            Base.PlacePiece(@base, PieceIds.Caravan, 18, 19, 0);
            build(@base);
            return Siege(@base, 2);
        }

        private static Unit ZombieAt(GameState s, string id, int x, int y)
        {
            var u = Setup.MakeUnit(id, TemplateIds.Zombie, "Zombi", null, P(x, y), "pz");
            s.Units.Add(u);
            s.Pods.Add(new Pod { Id = "pz", Active = true, UnitIds = new List<string> { id }, Patrol = new List<Pos>(), PatrolIndex = 0 });
            return s.Units[s.Units.Count - 1];
        }

        [Fact(DisplayName = "the defences that act on their own › they are set from the base: turrets as units nobody commands, traps, barbed wire, floodlights")]
        public void TheyAreSetFromTheBase()
        {
            var s = Armed((@base) =>
            {
                Base.PlacePiece(@base, PieceIds.Turret, 25, 20, 0);
                Base.PlacePiece(@base, PieceIds.Mine, 26, 20, 0);
                var spent = Base.PlacePiece(@base, PieceIds.Snare, 27, 20, 0);
                spent.Charges = 0;
                Base.PlacePiece(@base, PieceIds.Wire, 28, 20, 0);
                Base.PlacePiece(@base, PieceIds.Floodlight, 29, 20, 0);
            });
            var turret = s.Units.First((u) => u.Template == TemplateIds.Turret);
            Assert.Equal(Teams.Xcom, turret.Team);
            Assert.Null(turret.Owner);
            Assert.Equal((25, 20), (turret.Pos.X, turret.Pos.Y));
            Assert.Equal(Base.Pieces[PieceIds.Turret].Charges, turret.Ammo);
            Assert.Equal("b2", turret.Automaton?.Piece);
            Same(new[] { new Trap { Piece = "b3", Kind = TrapKinds.Mine, Node = 20 * s.Width + 26, Charges = 1 } }, s.Traps);
            Assert.Equal(new[] { 20 * s.Width + 28 }, s.Slow);
            Assert.Equal(new[] { new[] { 29, 20 } }, s.Lights?.Select((l) => new[] { l.Tile.X, l.Tile.Y }).ToArray());
            // Nobody gives it orders.
            var r = Exec(s, Ability(turret.Id, AbilityIds.Overwatch), seed: 1);
            Assert.Equal((false, "Va sola: nadie le da órdenes."), (r.Ok, r.Error));
            Assert.Null(r.Events);
        }

        [Fact(DisplayName = "the defences that act on their own › by night, a floodlight lights twice as far as a street light, and nobody but the waves comes")]
        public void ByNightAFloodlightLightsTwiceAsFar()
        {
            // Arturo, 18/10: some sieges are by night, and the floodlight is what lets the squad see far in them.
            var @base = Bare();
            Base.PlacePiece(@base, PieceIds.Caravan, 18, 19, 0);
            Base.PlacePiece(@base, PieceIds.Floodlight, 29, 20, 0);
            var s = Siege(@base, 2, (o) => o.Night = true);
            Assert.True(s.Mission.Night);
            var light = s.Lights![0];
            Assert.Equal(Content.Night.Floodlight, light.Radius);
            Assert.True(Content.Night.Floodlight > Content.Night.StreetLight);
            Assert.Contains(20 * s.Width + 29 - Content.Night.Floodlight, light.Tiles!);
            Assert.DoesNotContain(s.Units, (u) => u.Team != Teams.Xcom);
        }

        [Fact(DisplayName = "the defences that act on their own › a mine blows up whoever steps on it; spikes hurt and stop them; a snare holds them")]
        public void AMineBlowsUpWhoeverStepsOnIt()
        {
            var s = Armed((@base) =>
            {
                Base.PlacePiece(@base, PieceIds.Mine, 24, 12, 0);
                Base.PlacePiece(@base, PieceIds.Spikes, 24, 14, 0);
                Base.PlacePiece(@base, PieceIds.Snare, 24, 16, 0);
            });
            var run = new Sim(s, new Rng(1), new[] { 0 });
            var a = ZombieAt(s, "z1", 23, 12);
            run.MoveAlong(a, new List<Pos> { P(24, 12), P(25, 12) }, 1);
            Assert.False(a.Alive);
            Assert.Equal(0, s.Traps!.First((t) => t.Kind == TrapKinds.Mine).Charges);
            var b = ZombieAt(s, "z2", 23, 14);
            b.Hp = b.MaxHp = 9;
            run.MoveAlong(b, new List<Pos> { P(24, 14), P(25, 14) }, 1);
            Assert.Equal(new[] { 24, 9 - Content.Traps.Spikes }, new[] { b.Pos.X, b.Hp });
            Assert.Equal(2, s.Traps!.First((t) => t.Kind == TrapKinds.Spikes).Charges);
            var c = ZombieAt(s, "z3", 23, 16);
            run.MoveAlong(c, new List<Pos> { P(24, 16), P(25, 16) }, 1);
            Assert.Equal((24, true), (c.Pos.X, c.Stunned));
            // Spent, a trap does nothing.
            var d = ZombieAt(s, "z4", 23, 12);
            run.MoveAlong(d, new List<Pos> { P(24, 12), P(25, 12) }, 1);
            Assert.Equal((25, true), (d.Pos.X, d.Alive));
        }

        [Fact(DisplayName = "the defences that act on their own › barbed wire costs more to cross")]
        public void BarbedWireCostsMoreToCross()
        {
            var s = Armed((@base) => Base.PlacePiece(@base, PieceIds.Wire, 24, 12, 0));
            var z = ZombieAt(s, "z1", 23, 12);
            var reach = Paths.ComputeReach(s, z, 100);
            Assert.True(reach.Cost.TryGetValue(12 * s.Width + 24, out var cost), "the wire's tile out of reach");
            Assert.Equal(2 + Content.Traps.Wire, cost);
        }

        [Fact(DisplayName = "the defences that act on their own › a turret keeps watch through the enemy turn and shoots who comes in sight, but holds no base on its own")]
        public void ATurretKeepsWatchThroughTheEnemyTurn()
        {
            var s = Armed((@base) => Base.PlacePiece(@base, PieceIds.Turret, 22, 12, 0));
            var turret = s.Units.First((u) => u.Template == TemplateIds.Turret);
            ZombieAt(s, "z1", 22, 4);
            var run = new Sim(s, new Rng(3), new[] { 0 });
            run.RunAlienTurn();
            var shots = Of<ShotEvent>(run.Events).Where((e) => e.Unit == turret.Id).ToList();
            Assert.True(shots.Count > 0, "no shots");
            Assert.Equal(Base.Pieces[PieceIds.Turret].Charges!.Value - shots.Count, turret.Ammo);
            // Made to fire at what comes in: no loss of aim on a reaction shot.
            var z = s.Units.First((u) => u.Id == "z1");
            var watch = Aim.PreviewShot(s, turret, z, new ShotOptions { Reaction = true })!;
            Assert.DoesNotContain(watch.Mods, (m) => m.Label == "Fuego de reacción");
            Assert.Equal(Aim.PreviewShot(s, turret, z)!.Hit, watch.Hit);
            // With every soldier down, a turret still standing doesn't hold the base.
            var t = Armed((@base) => Base.PlacePiece(@base, PieceIds.Turret, 22, 12, 0));
            foreach (var u in t.Units.Where((x) => x.Team == Teams.Xcom && x.Automaton == null)) u.Alive = false;
            new Sim(t, new Rng(1), new[] { 0 }).CheckOutcome();
            Assert.Equal(new[] { "defeat", "wiped" }, new[] { t.Outcome, t.OutcomeReason });
        }
    }
}
