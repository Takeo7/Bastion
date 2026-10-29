#nullable enable
// base.ts: the Bastion as a map (docs/FASE12.md §11): a clearing in the pines with a
// caravan, and what is built on it. Pure functions over the base's state: the
// campaign pays for them (campaign.ts), the siege fights over them (setup.ts).
// PieceId (PieceIds) and BasePiece are in Types/BaseTypes.cs, for GameState.base.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>
    /// The base's ground (§11.1): the clearing and the pines round it, and what is
    /// built. `trees` are tile indices (y * width + x), in order.
    /// </summary>
    public sealed class BaseState
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<int> Trees { get; set; } = new List<int>();
        /// <summary>Where a pine was felled and nothing stands yet: a stump to draw, flat, no cover (tile indices, in the order felled).</summary>
        public List<int>? Felled { get; set; }
        public List<BasePiece> Pieces { get; set; } = new List<BasePiece>();
        public int NextPiece { get; set; }
        /// <summary>The Bastion's level, as bought (campaign.ts `level`): what stands on its lot (bastionFootprint). Missing: 1.</summary>
        public int? Level { get; set; }
    }

    /// <summary>PieceDef.kind: `'building' | 'defence' | 'trap'`.</summary>
    public static class PieceKinds
    {
        public const string Building = "building";
        public const string Defence = "defence";
        public const string Trap = "trap";

        public static readonly IReadOnlyList<string> All = new[] { Building, Defence, Trap };
    }

    /// <summary>
    /// One tile of a piece, unrotated: what it is in a fight (a tile kind; `ladder`
    /// for a tower's way up), and its height in storeys (a tower's deck).
    /// </summary>
    public sealed class PieceCell
    {
        public int Dx { get; set; }
        public int Dy { get; set; }
        public string Tile { get; set; } = ""; // TileKinds
        public int? Elev { get; set; }
    }

    /// <summary>
    /// A piece's tile once turned and set on the map (pieceCells, cellsOf): `PieceCell &amp; Vec2`. A Vec2, so it goes
    /// wherever a tile does.
    /// </summary>
    public sealed class PlacedCell : Vec2
    {
        public int Dx { get; set; }
        public int Dy { get; set; }
        public string Tile { get; set; } = ""; // TileKinds
        public int? Elev { get; set; }
    }

    /// <summary>PieceDef.cost: `{ supplies: number; parts?: number }` (repairCost gives both).</summary>
    public sealed class PieceCost
    {
        public int Supplies { get; set; }
        public int? Parts { get; set; }
    }

    public sealed class PieceDef
    {
        public string Id { get; set; } = ""; // PieceIds
        /// <summary>Provisional: the names are Lore's (lore.ts).</summary>
        public string Name { get; set; } = "";
        public string Kind { get; set; } = ""; // PieceKinds
        /// <summary>Tiles across (x) and deep (y), unrotated.</summary>
        public IReadOnlyList<int> Size { get; set; } = new int[0]; // [number, number]
        /// <summary>Every tile of it, unrotated; missing: all `tile`, on the ground.</summary>
        public IReadOnlyList<PieceCell>? Cells { get; set; }
        /// <summary>What it is in a fight, when every tile is the same.</summary>
        public string? Tile { get; set; } // TileKinds
        /// <summary>Suministros and piezas.</summary>
        public PieceCost Cost { get; set; } = new PieceCost();
        /// <summary>What it holds against the dead's blows (a Zombi takes off 3, SIEGE in content.ts).</summary>
        public int Hp { get; set; }
        /// <summary>Bastion level it opens at.</summary>
        public int Level { get; set; }
        /// <summary>Only one on the base.</summary>
        public bool? Unique { get; set; }
        /// <summary>A building: the facility whose effects it has (campaign.ts FACILITIES), by its level 1–3.</summary>
        public string? Facility { get; set; } // FacilityId (campaign.ts)
        /// <summary>A trap or a turret: uses before it has to be set again (or reloaded), with piezas.</summary>
        public int? Charges { get; set; }
    }

    public static class Base
    {
        private static IReadOnlyList<PieceCell> Block(int w, int h, string tile)
        {
            var cells = new PieceCell[w * h];
            for (var i = 0; i < w * h; i++) cells[i] = new PieceCell { Dx = i % w, Dy = (int)Math.Floor((double)i / w), Tile = tile };
            return cells;
        }

        /// <summary>`block(…).map((c) => ({ ...c, elev: 1 }))`: the same tiles, a storey up.</summary>
        private static IEnumerable<PieceCell> Raised(IReadOnlyList<PieceCell> cells)
        {
            return cells.Select((c) => new PieceCell { Dx = c.Dx, Dy = c.Dy, Tile = c.Tile, Elev = 1 });
        }

        /// <summary>
        /// What can stand on the base (§11). A building is solid in a fight: nobody goes
        /// in, it gives full cover and the dead only beat on it when it is in their way.
        /// The costs of the buildings are the facilities' of before, so the campaign's
        /// economy stays as it was; those of the defences, a first guess to tune by
        /// playing. Sizes as drawn, from Arte's models (docs/MAPAS.md §4.9, phase 6);
        /// a base built before keeps what stood at the size it had (BasePiece.size).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, PieceDef> Pieces = new Dictionary<string, PieceDef>
        {
            [PieceIds.Caravan] = new PieceDef { Id = PieceIds.Caravan, Name = "Autocaravana", Kind = PieceKinds.Building, Size = new[] { 5, 2 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 0 }, Hp = 40, Level = 1, Unique = true },
            // Not at the caravan's level (Ana, 21/10 #36): the radio, the Cuarentena and the Camposanto from the next.
            [PieceIds.Radio] = new PieceDef { Id = PieceIds.Radio, Name = "Sala de radio", Kind = PieceKinds.Building, Size = new[] { 4, 4 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 60 }, Hp = 30, Level = 2, Unique = true, Facility = "relay" },
            [PieceIds.Quarters] = new PieceDef { Id = PieceIds.Quarters, Name = "Barracón", Kind = PieceKinds.Building, Size = new[] { 6, 5 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 40 }, Hp = 30, Level = 1, Facility = "quarters" },
            [PieceIds.Infirmary] = new PieceDef { Id = PieceIds.Infirmary, Name = "Enfermería", Kind = PieceKinds.Building, Size = new[] { 6, 5 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 60 }, Hp = 30, Level = 1, Unique = true, Facility = "infirmary" },
            [PieceIds.Workshop] = new PieceDef { Id = PieceIds.Workshop, Name = "Taller", Kind = PieceKinds.Building, Size = new[] { 5, 4 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 85 }, Hp = 30, Level = 1, Unique = true, Facility = "workshop" },
            [PieceIds.Garden] = new PieceDef { Id = PieceIds.Garden, Name = "Huerto", Kind = PieceKinds.Building, Size = new[] { 7, 6 }, Tile = TileKinds.Low, Cost = new PieceCost { Supplies = 40 }, Hp = 20, Level = 1, Facility = "garden" },
            [PieceIds.Quarantine] = new PieceDef { Id = PieceIds.Quarantine, Name = "Cuarentena", Kind = PieceKinds.Building, Size = new[] { 4, 4 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 50 }, Hp = 30, Level = 2, Unique = true, Facility = "quarantine" },
            [PieceIds.Memorial] = new PieceDef { Id = PieceIds.Memorial, Name = "Camposanto", Kind = PieceKinds.Building, Size = new[] { 4, 4 }, Tile = TileKinds.Low, Cost = new PieceCost { Supplies = 40 }, Hp = 20, Level = 2, Unique = true, Facility = "memorial" },
            [PieceIds.Range] = new PieceDef { Id = PieceIds.Range, Name = "Campo de tiro", Kind = PieceKinds.Building, Size = new[] { 10, 3 }, Tile = TileKinds.Low, Cost = new PieceCost { Supplies = 90 }, Hp = 20, Level = 2, Unique = true, Facility = "range" },
            [PieceIds.Training] = new PieceDef { Id = PieceIds.Training, Name = "Centro de entrenamiento", Kind = PieceKinds.Building, Size = new[] { 5, 5 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 140 }, Hp = 30, Level = 2, Unique = true, Facility = "training" },
            [PieceIds.Simulation] = new PieceDef { Id = PieceIds.Simulation, Name = "Sala de mapas", Kind = PieceKinds.Building, Size = new[] { 6, 6 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 100 }, Hp = 30, Level = 3, Unique = true, Facility = "simulation" },
            [PieceIds.Garage] = new PieceDef { Id = PieceIds.Garage, Name = "Chapistería", Kind = PieceKinds.Building, Size = new[] { 7, 5 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 110 }, Hp = 30, Level = 3, Unique = true, Facility = "garage" },
            [PieceIds.Echoes] = new PieceDef { Id = PieceIds.Echoes, Name = "Taller de oficios", Kind = PieceKinds.Building, Size = new[] { 4, 4 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 130 }, Hp = 30, Level = 5, Unique = true, Facility = "echoes" },
            [PieceIds.Palisade] = new PieceDef { Id = PieceIds.Palisade, Name = "Empalizada", Kind = PieceKinds.Defence, Size = new[] { 1, 1 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 6 }, Hp = 12, Level = 1 },
            [PieceIds.Wall] = new PieceDef { Id = PieceIds.Wall, Name = "Muro", Kind = PieceKinds.Defence, Size = new[] { 1, 1 }, Tile = TileKinds.Wall, Cost = new PieceCost { Supplies = 15, Parts = 1 }, Hp = 36, Level = 2 },
            [PieceIds.Gate] = new PieceDef { Id = PieceIds.Gate, Name = "Puerta", Kind = PieceKinds.Defence, Size = new[] { 1, 1 }, Tile = TileKinds.Door, Cost = new PieceCost { Supplies = 10 }, Hp = 18, Level = 1 },
            [PieceIds.Sandbags] = new PieceDef { Id = PieceIds.Sandbags, Name = "Barricada de sacos", Kind = PieceKinds.Defence, Size = new[] { 1, 1 }, Tile = TileKinds.Low, Cost = new PieceCost { Supplies = 6 }, Hp = 15, Level = 1 },
            // Lore (18/10): what is felled, stacked; «Cajas» are the marked loot boxes already.
            [PieceIds.Logs] = new PieceDef { Id = PieceIds.Logs, Name = "Pila de troncos", Kind = PieceKinds.Defence, Size = new[] { 1, 1 }, Tile = TileKinds.Low, Cost = new PieceCost { Supplies = 3 }, Hp = 9, Level = 1 },
            [PieceIds.Tower] = new PieceDef
            {
                Id = PieceIds.Tower,
                Name = "Torre de vigía",
                Kind = PieceKinds.Defence,
                Size = new[] { 3, 4 },
                // The deck a storey up, three by three, and the ladder up to it in front, in the middle.
                Cells = Raised(Block(3, 3, TileKinds.Floor)).Concat(new[] { new PieceCell { Dx = 1, Dy = 3, Tile = TileKinds.Ladder } }).ToList(),
                Cost = new PieceCost { Supplies = 25 },
                Hp = 30,
                Level = 1,
            },
            [PieceIds.Floodlight] = new PieceDef { Id = PieceIds.Floodlight, Name = "Foco", Kind = PieceKinds.Defence, Size = new[] { 1, 1 }, Tile = TileKinds.High, Cost = new PieceCost { Supplies = 12 }, Hp = 6, Level = 2 },
            [PieceIds.Wire] = new PieceDef { Id = PieceIds.Wire, Name = "Alambre de espino", Kind = PieceKinds.Defence, Size = new[] { 1, 1 }, Tile = TileKinds.Floor, Cost = new PieceCost { Supplies = 5 }, Hp = 6, Level = 2 },
            [PieceIds.Mine] = new PieceDef { Id = PieceIds.Mine, Name = "Mina", Kind = PieceKinds.Trap, Size = new[] { 1, 1 }, Tile = TileKinds.Floor, Cost = new PieceCost { Supplies = 5, Parts = 1 }, Hp = 3, Level = 3, Charges = 1 },
            [PieceIds.Spikes] = new PieceDef { Id = PieceIds.Spikes, Name = "Foso con pinchos", Kind = PieceKinds.Trap, Size = new[] { 1, 1 }, Tile = TileKinds.Floor, Cost = new PieceCost { Supplies = 8 }, Hp = 9, Level = 2, Charges = 3 },
            [PieceIds.Snare] = new PieceDef { Id = PieceIds.Snare, Name = "Cepo", Kind = PieceKinds.Trap, Size = new[] { 1, 1 }, Tile = TileKinds.Floor, Cost = new PieceCost { Supplies = 6, Parts = 1 }, Hp = 6, Level = 2, Charges = 1 },
            [PieceIds.Turret] = new PieceDef { Id = PieceIds.Turret, Name = "Torreta", Kind = PieceKinds.Trap, Size = new[] { 1, 1 }, Tile = TileKinds.Floor, Cost = new PieceCost { Supplies = 40, Parts = 4 }, Hp = 12, Level = 4, Unique = false, Charges = 6 },
        };

        /// <summary>
        /// `Object.values(PIECES)`'s order: its keys as base.ts writes them, which is not PieceIds.All's (the Sala de
        /// mapas, the Chapistería and the Taller de oficios go the other way round).
        /// </summary>
        public static readonly IReadOnlyList<string> PieceOrder = new[]
        {
            PieceIds.Caravan, PieceIds.Radio, PieceIds.Quarters, PieceIds.Infirmary, PieceIds.Workshop, PieceIds.Garden,
            PieceIds.Quarantine, PieceIds.Memorial, PieceIds.Range, PieceIds.Training, PieceIds.Simulation, PieceIds.Garage,
            PieceIds.Echoes, PieceIds.Palisade, PieceIds.Wall, PieceIds.Gate, PieceIds.Sandbags, PieceIds.Logs, PieceIds.Tower,
            PieceIds.Floodlight, PieceIds.Wire, PieceIds.Mine, PieceIds.Spikes, PieceIds.Snare, PieceIds.Turret,
        };

        /// <summary>What each piece measured before the base grew (docs/MAPAS.md phase 6), unturned: what a base built then has standing.</summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<int>> OldPieceSizes = new Dictionary<string, IReadOnlyList<int>>
        {
            [PieceIds.Caravan] = new[] { 4, 2 },
            [PieceIds.Radio] = new[] { 3, 3 },
            [PieceIds.Quarters] = new[] { 4, 3 },
            [PieceIds.Infirmary] = new[] { 4, 3 },
            [PieceIds.Workshop] = new[] { 4, 3 },
            [PieceIds.Garden] = new[] { 4, 4 },
            [PieceIds.Quarantine] = new[] { 3, 3 },
            [PieceIds.Memorial] = new[] { 3, 3 },
            [PieceIds.Range] = new[] { 6, 2 },
            [PieceIds.Training] = new[] { 4, 3 },
            [PieceIds.Simulation] = new[] { 3, 3 },
            [PieceIds.Garage] = new[] { 4, 3 },
            [PieceIds.Echoes] = new[] { 3, 3 },
            [PieceIds.Tower] = new[] { 2, 3 },
        };

        /// <summary>The watchtower as it was built before the base grew (docs/MAPAS.md phase 6): a deck of two by two, the ladder at a corner.</summary>
        private static readonly IReadOnlyList<PieceCell> OldTower = Raised(Block(2, 2, TileKinds.Floor)).Concat(new[] { new PieceCell { Dx = 0, Dy = 2, Tile = TileKinds.Ladder } }).ToList();

        /// <summary>
        /// The Bastion's lot (Arturo, 18/10 #40: the caravan grows into a building
        /// with the level): 9 × 9 round the caravan (docs/MAPAS.md §4.9), from two
        /// tiles left and three up of it, where nothing else is built; what isn't the
        /// building is its yard.
        /// </summary>
        /// <remarks>BASTION_LOT: BastionLotSize here, BastionLot being the function.</remarks>
        public const int BastionLotSize = 9;
        /// <summary>What the Bastion takes up at each stage (two levels each): the caravan, the mobile home, the barracón, the house (Arte; §4.9).</summary>
        public static readonly IReadOnlyList<IReadOnlyList<int>> BastionStages = new[]
        {
            new[] { 5, 2 },
            new[] { 7, 3 },
            new[] { 7, 5 },
            new[] { 7, 7 },
        };

        /// <summary>The lot, from the caravan; null with no caravan.</summary>
        public static PlanRect? BastionLot(BaseState @base)
        {
            var caravan = @base.Pieces.FirstOrDefault((p) => p.Piece == PieceIds.Caravan);
            return caravan != null ? new PlanRect { X = caravan.X - 2, Y = caravan.Y - 3, W = BastionLotSize, H = BastionLotSize } : null;
        }

        /// <summary>The stage the Bastion's level shows (0 to 3).</summary>
        public static int BastionStage(int? level)
        {
            return Math.Min(BastionStages.Count - 1, Math.Max(0, (int)Math.Ceiling((level ?? 1) / 2.0) - 1));
        }

        /// <summary>The stage the Bastion's level shows (0 to 3): `bastionStage(base)`.</summary>
        public static int BastionStage(BaseState @base)
        {
            return BastionStage(@base.Level);
        }

        /// <summary>The tiles the Bastion's building stands on, at the middle of its lot (rounded down).</summary>
        public static List<Vec2> BastionFootprint(BaseState @base)
        {
            var lot = BastionLot(@base);
            if (lot == null) return new List<Vec2>();
            var stage = BastionStages[BastionStage(@base)];
            var w = stage[0];
            var h = stage[1];
            var x0 = lot.X + (int)Math.Floor((lot.W - w) / 2.0);
            var y0 = lot.Y + (int)Math.Floor((lot.H - h) / 2.0);
            var cells = new List<Vec2>();
            for (var y = y0; y < y0 + h; y++) for (var x = x0; x < x0 + w; x++) cells.Add(new Vec2 { X = x, Y = y });
            return cells;
        }

        /// <summary>An element of BASTION_YARD: what is always there, and what its second level adds, as [x, y] of its lot.</summary>
        private sealed class YardThings
        {
            public IReadOnlyList<int[]> Always { get; set; } = new int[0][];
            public IReadOnlyList<int[]> More { get; set; } = new int[0][];
        }

        /// <summary>
        /// What stands in the Bastion's yard at each stage (Arte's drawing of it,
        /// client basePieces.ts drawBastion, on the lot of 9 × 9: docs/MAPAS.md phase
        /// 6), by tile of its lot (x across, y deep, its front the last row): what is
        /// always there, and what its second level adds.
        ///   - the caravan: the fire with a chair either side, the wood; then its steps,
        ///     the table and chairs under its awning by its side (two by two), the
        ///     generator by its end;
        ///   - the mobile home: the fire and its chairs along the front row, the wood;
        ///     then the deck along its front and its steps;
        ///   - the barracón: its steps, the fire and its chairs in the front corner, the
        ///     wood; then the water tower, the solar panel and the generator up the free
        ///     column, sandbags either side of its steps;
        ///   - the house (its porch the model's own, inside its 7 × 7): the wood; then
        ///     the floodlight in the other corner, sandbags across the front row either
        ///     side of the way in, tank traps in the back corners (playtest 20/10 #13).
        /// </summary>
        /// <remarks>BASTION_YARD: BastionYardThings here, BastionYard being the function.</remarks>
        private static readonly IReadOnlyList<YardThings> BastionYardThings = new[]
        {
            new YardThings
            {
                Always = new[] { new[] { 5, 7 }, new[] { 4, 7 }, new[] { 6, 7 }, new[] { 0, 8 } },
                More = new[] { new[] { 5, 5 }, new[] { 0, 6 }, new[] { 1, 6 }, new[] { 0, 7 }, new[] { 1, 7 }, new[] { 7, 3 } },
            },
            new YardThings
            {
                Always = new[] { new[] { 5, 8 }, new[] { 4, 8 }, new[] { 6, 8 }, new[] { 0, 8 } },
                More = new[] { new[] { 3, 6 }, new[] { 4, 6 }, new[] { 5, 6 }, new[] { 6, 6 }, new[] { 7, 6 }, new[] { 2, 6 } },
            },
            new YardThings
            {
                Always = new[] { new[] { 4, 7 }, new[] { 7, 8 }, new[] { 6, 8 }, new[] { 8, 8 }, new[] { 0, 8 } },
                More = new[] { new[] { 8, 0 }, new[] { 8, 1 }, new[] { 8, 2 }, new[] { 8, 3 }, new[] { 3, 7 }, new[] { 5, 7 } },
            },
            new YardThings
            {
                Always = new[] { new[] { 0, 8 } },
                More = new[] { new[] { 8, 8 }, new[] { 1, 8 }, new[] { 2, 8 }, new[] { 6, 8 }, new[] { 7, 8 }, new[] { 0, 0 }, new[] { 8, 0 } },
            },
        };

        /// <summary>
        /// The tiles of the Bastion's yard that what is drawn there stands on (Arturo,
        /// 19/10: «que ocupen»; BASTION_YARD), and those of the lot it shuts in, that
        /// nobody could get to: in a siege, low cover nobody stands on (baseLayout).
        /// The client draws the yard as it always has, and nothing else there.
        /// </summary>
        /// <remarks>
        /// It reads only `pieces` and `level` (`Pick&lt;BaseState, 'pieces' | 'level'&gt;`). The `${x},${y}` keys are (x, y)
        /// tuples here: the same tiles, and `out`, which is walked, keeps its order (JsSet).
        /// </remarks>
        public static List<Vec2> BastionYard(BaseState @base)
        {
            var lot = BastionLot(@base);
            if (lot == null) return new List<Vec2>();
            var level = @base.Level ?? 1;
            var yard = BastionYardThings[BastionStage(@base)];
            var solid = new HashSet<(int, int)>(BastionFootprint(@base).Select((p) => (p.X - lot.X, p.Y - lot.Y)));
            var things = yard.Always.Concat(level % 2 == 0 ? yard.More : new int[0][]).Select((t) => (t[0], t[1])).ToList();
            foreach (var t in things) solid.Add(t);
            // What is left of the lot joins the clearing by its edge, or it is shut in.
            var open = new HashSet<(int, int)>();
            var queue = new List<(int, int)>();
            for (var y = 0; y < lot.H; y++)
            {
                for (var x = 0; x < lot.W; x++)
                {
                    if ((x == 0 || y == 0 || x == lot.W - 1 || y == lot.H - 1) && !solid.Contains((x, y)))
                    {
                        open.Add((x, y));
                        queue.Add((x, y));
                    }
                }
            }
            while (queue.Count > 0)
            {
                var (x, y) = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var k = (x + dx, y + dy);
                    if (x + dx < 0 || y + dy < 0 || x + dx >= lot.W || y + dy >= lot.H || solid.Contains(k) || open.Contains(k)) continue;
                    open.Add(k);
                    queue.Add((x + dx, y + dy));
                }
            }
            var @out = new JsSet<(int, int)>(things);
            for (var y = 0; y < lot.H; y++) for (var x = 0; x < lot.W; x++) if (!solid.Contains((x, y)) && !open.Contains((x, y))) @out.Add((x, y));
            return @out.Select((k) => new Vec2 { X = lot.X + k.Item1, Y = lot.Y + k.Item2 }).ToList();
        }

        /// <summary>Whether (x, y) lies on the Bastion's lot.</summary>
        public static bool OnBastionLot(BaseState @base, int x, int y)
        {
            var lot = BastionLot(@base);
            return lot != null && x >= lot.X && y >= lot.Y && x < lot.X + lot.W && y < lot.Y + lot.H;
        }

        /// <summary>The base's map, across and deep (docs/MAPAS.md §4.9: 56, a base built on 40 set in its middle).</summary>
        public const int BaseMap = 56;
        /// <summary>The dead come in by this band at the edge: nothing is built in it.</summary>
        public const int BaseEdge = 2;
        /// <summary>Suministros felling a tree costs; it gives nothing back (Arturo, 18/10 #41: «solo cuesta recursos y no da nada»).</summary>
        public const int FellCost = 3;
        /// <summary>Of a piece's cost, what dismantling it gives back.</summary>
        public const double DismantleBack = 0.5;

        /// <summary>A piece's tiles once turned `rot` quarter turns clockwise and set at (x, y); `size`, one built at another (BasePiece.size).</summary>
        public static List<PlacedCell> PieceCells(string id, int x, int y, int rot, IReadOnlyList<int>? size = null)
        {
            var def = Pieces[id];
            var wh = size ?? def.Size;
            var w = wh[0];
            var h = wh[1];
            var cells = size != null ? (id == PieceIds.Tower ? OldTower : Block(w, h, def.Tile ?? TileKinds.Wall)) : (def.Cells ?? Block(w, h, def.Tile ?? TileKinds.Wall));
            var r = ((rot % 4) + 4) % 4;
            return cells.Select((c) =>
            {
                // Turned about the piece's own box, then set with its top-left at (x, y).
                var dx = r == 0 ? c.Dx : r == 1 ? h - 1 - c.Dy : r == 2 ? w - 1 - c.Dx : c.Dy;
                var dy = r == 0 ? c.Dy : r == 1 ? c.Dx : r == 2 ? h - 1 - c.Dy : w - 1 - c.Dx;
                return new PlacedCell { Dx = dx, Dy = dy, Tile = c.Tile, Elev = c.Elev, X = x + dx, Y = y + dy };
            }).ToList();
        }

        /// <summary>The tiles a piece on the base stands on, at the size it was built (BasePiece.size).</summary>
        public static List<PlacedCell> CellsOf(BasePiece p)
        {
            return PieceCells(p.Piece, p.X, p.Y, p.Rot, p.Size);
        }

        /// <summary>
        /// The tiles of the turrets, the traps and the wire still standing: ground (floor) on the map, but nobody
        /// starts a siege on them (a defender on a turret's tile shared it with the turret).
        /// </summary>
        public static JsSet<int> ArmedTiles(BaseState @base)
        {
            var armed = new JsSet<int>();
            foreach (var p in @base.Pieces)
            {
                if (p.Hp <= 0 || (Pieces[p.Piece].Kind != PieceKinds.Trap && p.Piece != PieceIds.Wire)) continue;
                foreach (var c in CellsOf(p)) armed.Add(c.Y * @base.Width + c.X);
            }
            return armed;
        }

        /// <summary>The piece standing on a tile, if any.</summary>
        public static BasePiece? PieceAt(BaseState @base, int x, int y)
        {
            return @base.Pieces.FirstOrDefault((p) => CellsOf(p).Any((c) => c.X == x && c.Y == y));
        }

        public static bool TreeAt(BaseState @base, int x, int y)
        {
            return SortedHas(@base.Trees, y * @base.Width + x);
        }

        private static bool SortedHas(List<int> list, int n)
        {
            var lo = 0;
            var hi = list.Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) >> 1;
                if (list[mid] == n) return true;
                if (list[mid] < n) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        /// <summary>
        /// Why `id` can't go at (x, y) turned `rot`, or null if it can: on the map but
        /// off the edge band where the dead come in, on cleared ground (no tree) and
        /// free, and only once if it is unique. Costs and the Bastion's level are the
        /// campaign's to check.
        /// </summary>
        public static string? PlaceBlocker(BaseState @base, string id, int x, int y, int rot)
        {
            var def = Pieces[id];
            if (def.Unique == true && @base.Pieces.Any((p) => p.Piece == id)) return "Ya está en la base.";
            foreach (var c in PieceCells(id, x, y, rot))
            {
                if (c.X < BaseEdge || c.Y < BaseEdge || c.X >= @base.Width - BaseEdge || c.Y >= @base.Height - BaseEdge) return "Ahí no: es el borde del claro.";
                if (TreeAt(@base, c.X, c.Y)) return "Hay árboles: tala antes.";
                if (PieceAt(@base, c.X, c.Y) != null) return "Ahí ya hay algo.";
                if (id != PieceIds.Caravan && OnBastionLot(@base, c.X, c.Y)) return "Ahí no: ese sitio es para el Bastión, que crece con cada nivel.";
            }
            return null;
        }

        /// <summary>Puts `id` at (x, y) turned `rot`, whole (a building at level 1, a trap set). The caller has checked placeBlocker.</summary>
        public static BasePiece PlacePiece(BaseState @base, string id, int x, int y, int rot)
        {
            var def = Pieces[id];
            var piece = new BasePiece
            {
                Id = $"b{@base.NextPiece++}",
                Piece = id,
                X = x,
                Y = y,
                Rot = ((rot % 4) + 4) % 4,
                Hp = def.Hp,
            };
            if (def.Kind == PieceKinds.Building) piece.Level = 1;
            if (def.Charges is int charges && charges != 0) piece.Charges = charges;
            @base.Pieces.Add(piece);
            // Built over, a stump is gone.
            if (@base.Felled != null && @base.Felled.Count > 0)
            {
                var under = new HashSet<int>(PieceCells(id, x, y, rot).Select((c) => c.Y * @base.Width + c.X));
                @base.Felled = @base.Felled.Where((i) => !under.Contains(i)).ToList();
            }
            return piece;
        }

        /// <summary>
        /// Whether a piece built before the base grew (BasePiece.size) now fits at its kind's size where it stands, as it is
        /// turned: what raising it makes it, if so (campaign.ts 'raise').
        /// </summary>
        public static bool GrownFits(BaseState @base, BasePiece p)
        {
            if (p.Size == null) return false;
            return PieceCells(p.Piece, p.X, p.Y, p.Rot).All((c) =>
            {
                if (c.X < BaseEdge || c.Y < BaseEdge || c.X >= @base.Width - BaseEdge || c.Y >= @base.Height - BaseEdge || TreeAt(@base, c.X, c.Y)) return false;
                var there = PieceAt(@base, c.X, c.Y);
                return (there == null || there == p) && (p.Piece == PieceIds.Caravan || !OnBastionLot(@base, c.X, c.Y));
            });
        }

        /// <summary>
        /// A base built on a smaller map, on one `size` across (docs/MAPAS.md phase 6, decided by Arturo 19/10: «con la base
        /// guardada en el centro y lo construido con su tamaño»): all of it set in the middle, what stood at the size it was
        /// built (BasePiece.size), pines round it as thick as freshBase grows them, its track clear. What
        /// stands on the Bastion's bigger lot comes down: returned, for the caller to give back what it cost.
        /// </summary>
        public static List<BasePiece> GrowBase(BaseState @base, int size, Rng rng)
        {
            var old = @base.Width;
            var o = (int)Math.Floor((size - old) / 2.0);
            if (o <= 0) return new List<BasePiece>();
            int Shift(int i) => ((int)Math.Floor((double)i / old) + o) * size + (i % old) + o;
            var trees = new JsSet<int>(@base.Trees.Select(Shift));
            var thickness = 0.5 + rng.Next() * 0.25;
            const int step = 6;
            var gw = (int)Math.Ceiling((double)size / step) + 2;
            var grid = new double[gw * gw];
            for (var k = 0; k < grid.Length; k++) grid[k] = rng.Next();
            double Smooth(double t) => t * t * (3 - 2 * t);
            double Noise(int x, int y)
            {
                var fx = (double)x / step;
                var fy = (double)y / step;
                var gx = (int)Math.Floor(fx);
                var gy = (int)Math.Floor(fy);
                var tx = Smooth(fx - gx);
                var ty = Smooth(fy - gy);
                double At(int a, int b) => grid[b * gw + a];
                return (At(gx, gy) * (1 - tx) + At(gx + 1, gy) * tx) * (1 - ty) + (At(gx, gy + 1) * (1 - tx) + At(gx + 1, gy + 1) * tx) * ty;
            }
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inside = x >= o && y >= o && x < o + old && y < o + old;
                    if (!inside && !OnTrack(size, size, x, y) && Noise(x, y) < thickness) trees.Add(y * size + x);
                }
            }
            var sorted = trees.ToList();
            Js.Sort(sorted, (a, b) => a.CompareTo(b));
            @base.Trees = sorted;
            @base.Felled = @base.Felled?.Select(Shift).ToList();
            foreach (var p in @base.Pieces)
            {
                p.X += o;
                p.Y += o;
                var was = OldPieceSizes.TryGetValue(p.Piece, out var oldSize) ? oldSize : null;
                if (p.Piece != PieceIds.Caravan && was != null && (was[0] != Pieces[p.Piece].Size[0] || was[1] != Pieces[p.Piece].Size[1])) p.Size = was.ToArray();
            }
            @base.Width = size;
            @base.Height = size;
            var down = @base.Pieces.Where((p) => p.Piece != PieceIds.Caravan && CellsOf(p).Any((t) => OnBastionLot(@base, t.X, t.Y))).ToList();
            @base.Pieces = @base.Pieces.Where((p) => !down.Contains(p)).ToList();
            // Nothing grows on the Bastion's lot.
            if (BastionLot(@base) != null) @base.Trees = @base.Trees.Where((i) => !OnBastionLot(@base, i % size, (int)Math.Floor((double)i / size))).ToList();
            return down;
        }

        /// <summary>
        /// Why the tree at (x, y) can't be felled, or null if it can. Not in the band at the edge where nothing is built
        /// (BASE_EDGE): the forest goes on from there, and where felling stops is where the clearing ends (Ana, 22/10 #30).
        /// </summary>
        public static string? FellBlocker(BaseState @base, int x, int y)
        {
            if (!TreeAt(@base, x, y)) return "Ahí no hay ningún árbol.";
            if (x < BaseEdge || y < BaseEdge || x >= @base.Width - BaseEdge || y >= @base.Height - BaseEdge) return "Ese ya es el bosque: no se tala.";
            return null;
        }

        /// <summary>Fells the tree at (x, y): the ground is clear. 1 if there was one, else 0 (what it costs: FELL_COST each).</summary>
        public static int FellTree(BaseState @base, int x, int y)
        {
            var i = y * @base.Width + x;
            var at = @base.Trees.IndexOf(i);
            if (at < 0) return 0;
            @base.Trees.RemoveAt(at);
            (@base.Felled ??= new List<int>()).Add(i);
            return 1;
        }

        /// <summary>What mending `p` costs: its cost in proportion to what it has lost, a suministro at least.</summary>
        public static PieceCost RepairCost(BasePiece p)
        {
            var def = Pieces[p.Piece];
            var lost = 1 - (double)p.Hp / def.Hp;
            if (lost <= 0) return new PieceCost { Supplies = 0, Parts = 0 };
            return new PieceCost { Supplies = Math.Max(1, (int)Math.Ceiling(def.Cost.Supplies * lost)), Parts = (int)Math.Ceiling((def.Cost.Parts ?? 0) * lost) };
        }

        /// <summary>What setting a spent trap again (or reloading a turret) costs: piezas, one at least.</summary>
        public static int RearmCost(BasePiece p)
        {
            var def = Pieces[p.Piece];
            if (def.Charges == null || def.Charges == 0 || (p.Charges ?? 0) >= def.Charges) return 0;
            return Math.Max(1, def.Cost.Parts ?? 1);
        }

        /// <summary>What dismantling `p` gives back: half its cost, if it is whole; the caravan never goes.</summary>
        public static string? DismantleBlocker(BasePiece p)
        {
            return p.Piece == PieceIds.Caravan ? "La autocaravana no se desmonta." : null;
        }

        public static int DismantleRefund(BasePiece p)
        {
            var def = Pieces[p.Piece];
            return (int)Math.Floor(def.Cost.Supplies * DismantleBack * ((double)p.Hp / def.Hp));
        }

        /// <summary>Pieces touching `p` side by side (§11.3: «junto a» is next to it on the map).</summary>
        public static List<BasePiece> BesidePieces(BaseState @base, BasePiece p)
        {
            // The caravan is its whole lot (nothing is built on it): beside the lot is beside the caravan.
            var caravan = @base.Pieces.FirstOrDefault((q) => q.Piece == PieceIds.Caravan);
            var lot = BastionLot(@base);
            var lotCells = new List<Vec2>();
            if (lot != null) for (var y = lot.Y; y < lot.Y + lot.H; y++) for (var x = lot.X; x < lot.X + lot.W; x++) lotCells.Add(new Vec2 { X = x, Y = y });
            IReadOnlyList<Vec2> mine = p == caravan ? lotCells : CellsOf(p);
            BasePiece? At(int x, int y) => caravan != null && OnBastionLot(@base, x, y) ? caravan : PieceAt(@base, x, y);
            // A Set of pieces, by reference (BasePiece has no Equals of its own), in the order found.
            var @out = new JsSet<BasePiece>();
            foreach (var c in mine)
            {
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var q = At(c.X + dx, c.Y + dy);
                    if (q != null && q != p) @out.Add(q);
                }
            }
            return @out.ToList();
        }

        /// <summary>The side of the map the base's one track runs out to (a DIRS4 index: the south).</summary>
        public const int TrackSide = 2;

        /// <summary>
        /// On the base's track: a band three tiles wide from the middle out to the south side, the one La Lata comes and goes
        /// by. One, not one to each side (Ana's playtest 28/10: «sólo uno, por donde ha llegado el coche»); the dead come
        /// in from between the pines (siegeTrails).
        /// </summary>
        public static bool OnTrack(int width, int height, int x, int y)
        {
            return Math.Abs(x - (width - 1) / 2.0) <= 1 && y >= (height - 1) / 2.0 - 1;
        }

        /// <summary>On one of the four tracks a base had till 28/10: the band through the middle, each way, out to every side.</summary>
        public static bool OnOldTrack(int width, int height, int x, int y)
        {
            return Math.Abs(x - (width - 1) / 2.0) <= 1 || Math.Abs(y - (height - 1) / 2.0) <= 1;
        }

        /// <summary>
        /// A base from before 28/10 (migrateCampaign, version 26): its three tracks gone, pines where the wood stood either
        /// side of them, as thick as it is there; the clearing, what was felled and built, and the squad's places stay open.
        /// A tile of an old track is planted where a tile beside the band, across it, is a pine.
        /// </summary>
        public static void PlantOldTracks(BaseState @base, IEnumerable<Vec2> keep)
        {
            var width = @base.Width;
            var height = @base.Height;
            var trees = new HashSet<int>(@base.Trees);
            var felled = new HashSet<int>(@base.Felled ?? new List<int>());
            var kept = new HashSet<int>(keep.Select((p) => p.Y * width + p.X));
            bool Band(int x, int y) => OnOldTrack(width, height, x, y);
            // The first tile past the band from (x, y), stepping (dx, dy): a pine?
            bool PineBeyond(int x, int y, int dx, int dy)
            {
                for (var k = 1; k <= 3; k++)
                {
                    var a = x + dx * k;
                    var b = y + dy * k;
                    if (a < 0 || b < 0 || a >= width || b >= height) return false;
                    if (!Band(a, b)) return trees.Contains(b * width + a);
                }
                return false;
            }
            var planted = new List<int>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    if (!Band(x, y) || OnTrack(width, height, x, y) || trees.Contains(i) || felled.Contains(i) || kept.Contains(i) || PieceAt(@base, x, y) != null) continue;
                    // Across the band: sideways on a track running north and south, up and down on one running east and west.
                    var northSouth = Math.Abs(x - (width - 1) / 2.0) <= 1;
                    var flanked = northSouth ? PineBeyond(x, y, -1, 0) || PineBeyond(x, y, 1, 0) : PineBeyond(x, y, 0, -1) || PineBeyond(x, y, 0, 1);
                    if (flanked) planted.Add(i);
                }
            }
            if (planted.Count == 0) return;
            var all = @base.Trees.Concat(planted).ToList();
            Js.Sort(all, (a, b) => a.CompareTo(b));
            @base.Trees = all;
        }

        /// <summary>
        /// A new base (§11.1): the caravan in the middle of a clearing in the pines.
        /// Every campaign's ground is its own: sometimes thicker pines, sometimes more
        /// clearings, though few big ones; the middle always clear round the caravan,
        /// and a track out to the south for La Lata (one since 28/10; the dead come by ways through the pines).
        /// </summary>
        public static BaseState FreshBase(Rng rng, int size = BaseMap)
        {
            var width = size;
            var height = size;
            // Smooth noise: a coarse grid of random values, blended across each cell.
            const int step = 6;
            var gw = (int)Math.Ceiling((double)width / step) + 2;
            var grid = new double[gw * gw];
            for (var k = 0; k < grid.Length; k++) grid[k] = rng.Next();
            double At(int gx, int gy) => grid[gy * gw + gx];
            double Smooth(double t) => t * t * (3 - 2 * t);
            double Noise(int x, int y)
            {
                var fx = (double)x / step;
                var fy = (double)y / step;
                var gx = (int)Math.Floor(fx);
                var gy = (int)Math.Floor(fy);
                var tx = Smooth(fx - gx);
                var ty = Smooth(fy - gy);
                var top = At(gx, gy) * (1 - tx) + At(gx + 1, gy) * tx;
                var bottom = At(gx, gy + 1) * (1 - tx) + At(gx + 1, gy + 1) * tx;
                return top * (1 - ty) + bottom * ty;
            }
            // How thick the pines are this time: between half and three quarters of the ground beyond the clearing.
            var thickness = 0.5 + rng.Next() * 0.25;
            var cx = (width - 1) / 2.0;
            var cy = (height - 1) / 2.0;
            // A bigger clearing on a bigger map: 6 to 8 tiles round the middle on 56 (smaller since Ana's playtest 28/10; it was
            // 8 to 11), with its glades.
            var clearing = Js.Round((4 + rng.Int(3)) * ((double)size / 40));
            var trees = new List<int>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var d = Js.Hypot(x - cx, y - cy);
                    if (d <= clearing || OnTrack(width, height, x, y)) continue;
                    // Thicker away from the clearing; the noise makes the stands and the glades.
                    var fall = Math.Min(1, (d - clearing) / 6);
                    if (Noise(x, y) < thickness * fall) trees.Add(y * width + x);
                }
            }
            var @base = new BaseState { Width = width, Height = height, Trees = trees, Felled = new List<int>(), Pieces = new List<BasePiece>(), NextPiece = 1 };
            var w = Pieces[PieceIds.Caravan].Size[0];
            var h = Pieces[PieceIds.Caravan].Size[1];
            PlacePiece(@base, PieceIds.Caravan, (int)Math.Floor(cx - w / 2.0) + 1, (int)Math.Floor(cy - h / 2.0) + 1, 0);
            return @base;
        }

        // ------------------------------------------------------------- the siege

        /// <summary>What each piece looks like when the client has nothing better (decor.ts); the base's own drawing is Arte's, from GameState.base.</summary>
        private static readonly IReadOnlyDictionary<string, string> PieceLook = new Dictionary<string, string>
        {
            [PieceIds.Sandbags] = DecorKinds.Barricade,
            [PieceIds.Logs] = DecorKinds.LogPile,
            [PieceIds.Palisade] = DecorKinds.Fence,
            [PieceIds.Wire] = DecorKinds.Wire,
        };

        /// <summary>Places round the caravan for the squad of a siege: every soldier both players can have.</summary>
        private const int BaseDefenders = 24;

        /// <summary>How far apart along each side the ways through the pines start, and how far off the tracks.</summary>
        private const int TrailEvery = 6;
        private const int TrailTrackGap = 4;

        /// <summary>
        /// Narrow ways through the pines for a siege (Ana, 23/10 #33): from every side, every TRAIL_EVERY tiles but by the
        /// track, winding in from the edge till they reach the open ground joined to the middle, so the dead come out from
        /// between the trees. Since 28/10, with one track, they go all the way (a way that stopped in a glade the pines close
        /// off let no one in by its side, and a wave with nowhere to land never came). Only on the siege's map: the
        /// clearing's own pines stay as they are. Fixed by the base, so the same base always has the same ways.
        /// </summary>
        public static List<int> SiegeTrails(BaseState @base)
        {
            var width = @base.Width;
            var height = @base.Height;
            // Only ever asked `has`.
            var trees = new HashSet<int>(@base.Trees);
            // The open ground joined to the middle of the clearing, by the four ways round.
            var joined = new HashSet<int>();
            var middle = (int)Math.Floor((height - 1) / 2.0) * width + (int)Math.Floor((width - 1) / 2.0);
            var queue = new List<int> { middle };
            joined.Add(middle);
            while (queue.Count > 0)
            {
                var i = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                var x = i % width;
                var y = i / width;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var a = x + dx;
                    var b = y + dy;
                    var j = b * width + a;
                    if (a < 0 || b < 0 || a >= width || b >= height || trees.Contains(j) || joined.Contains(j)) continue;
                    joined.Add(j);
                    queue.Add(j);
                }
            }
            var @out = new JsSet<int>();
            int Mix(int a, int b) => (int)(unchecked((uint)((a * 73856093) ^ (b * 19349663))) % 1000);
            // The tile `depth` in from a side's edge, `along` it; -1 off the map.
            int TileOf(int side, int depth, int along)
            {
                var x = side == 0 ? along : side == 1 ? width - 1 - depth : side == 2 ? along : depth;
                var y = side == 0 ? depth : side == 1 ? along : side == 2 ? height - 1 - depth : along;
                return x < 0 || y < 0 || x >= width || y >= height ? -1 : y * width + x;
            }
            for (var side = 0; side < 4; side++)
            {
                var span = side % 2 == 0 ? width : height;
                var mid = (span - 1) / 2.0;
                for (var k = 3; k < span - 3; k += TrailEvery)
                {
                    if (side == TrackSide && Math.Abs(k - mid) < TrailTrackGap) continue;
                    // From the edge inwards, a step aside now and then; until it reaches the open ground joined to the middle,
                    // past the band of the edge.
                    var along = k;
                    for (var depth = 0; depth < span / 2.0; depth++)
                    {
                        var i = TileOf(side, depth, along);
                        if (i < 0) break;
                        if (depth > BaseEdge + 1 && joined.Contains(i)) break;
                        if (trees.Contains(i)) @out.Add(i);
                        var turn = Mix(side * 1000 + k, depth);
                        var was = along;
                        if (turn < 220) along = Math.Max(1, along - 1);
                        else if (turn > 780) along = Math.Min(span - 2, along + 1);
                        if (along == was) continue;
                        // The step aside by the tile beside, not across the corner: nobody cuts between two pines (paths.ts).
                        var beside = TileOf(side, depth, along);
                        if (depth > BaseEdge + 1 && joined.Contains(beside)) break;
                        if (trees.Contains(beside)) @out.Add(beside);
                    }
                }
            }
            var sorted = @out.ToList();
            Js.Sort(sorted, (a, b) => a.CompareTo(b));
            return sorted;
        }

        /// <summary>
        /// The base as a battlefield for its siege (docs/FASE12.md §11.5): the pines
        /// standing, every piece on its tiles (a building solid, a tower's deck a
        /// storey up), what each holds against the dead's blows, and the squad's
        /// places round the caravan. Traps and turrets are set by the siege itself
        /// (setup.ts), from GameState.base.
        /// </summary>
        public static MissionLayout BaseLayout(BaseState @base)
        {
            var width = @base.Width;
            var height = @base.Height;
            var tiles = Enumerable.Repeat(TileKinds.Floor, width * height).ToList();
            var elev = Enumerable.Repeat(0, width * height).ToList();
            var decor = Enumerable.Repeat(0, width * height).ToList();
            // The tracks are trodden earth (a yard, ground.ts); the rest, open ground.
            var ground = Enumerable.Range(0, width * height).Select((i) => OnTrack(width, height, i % width, (int)Math.Floor((double)i / width)) ? 3 : 0).ToList();
            foreach (var i in @base.Trees)
            {
                tiles[i] = TileKinds.High;
                decor[i] = Decor.DecorCode(DecorKinds.Tree, i % 4);
            }
            // Ways through the pines (Ana, 23/10 #33: «no llegan de entre los árboles; sólo salen de los caminos»).
            foreach (var i in SiegeTrails(@base))
            {
                if (tiles[i] != TileKinds.High) continue;
                tiles[i] = TileKinds.Floor;
                decor[i] = 0;
            }
            var breakables = new Dictionary<int, int>();
            foreach (var p in @base.Pieces)
            {
                // What a siege broke and nobody has mended is a gap (the client draws its ruin from GameState.base).
                if (p.Hp <= 0) continue;
                var look = PieceLook.TryGetValue(p.Piece, out var kind) ? kind : null;
                // The caravan stands for the Bastion: the building of its stage, solid, holding what the caravan holds.
                var cells = p.Piece == PieceIds.Caravan ? BastionFootprint(@base).Select((t) => new PlacedCell { X = t.X, Y = t.Y, Tile = TileKinds.Wall }).ToList() : CellsOf(p);
                foreach (var c in cells)
                {
                    var i = c.Y * width + c.X;
                    tiles[i] = c.Tile;
                    elev[i] = c.Elev ?? 0;
                    if (look != null) decor[i] = Decor.DecorCode(look, (p.Rot + 1) % 4);
                    // What stands in the way holds its piece's health; the ground of a trap or a deck doesn't.
                    if (c.Tile == TileKinds.Wall || c.Tile == TileKinds.Door || c.Tile == TileKinds.Low || c.Tile == TileKinds.High) breakables[i] = p.Hp;
                }
            }
            // What stands in its yard takes its tile (19/10): the fire, the chairs, the wood, the porch… low, and nobody starts there.
            if (@base.Pieces.Any((p) => p.Piece == PieceIds.Caravan && p.Hp > 0))
            {
                foreach (var p in BastionYard(@base))
                {
                    // `tiles[i]` off the map is undefined, never 'floor'.
                    var i = p.Y * width + p.X;
                    if (i >= 0 && i < tiles.Count && tiles[i] == TileKinds.Floor) tiles[i] = TileKinds.Low;
                }
            }
            // The squad round the Bastion: the free tiles nearest the middle of its lot.
            var lot = BastionLot(@base);
            var middleX = lot != null ? lot.X + lot.W / 2.0 : width / 2.0;
            var middleY = lot != null ? lot.Y + lot.H / 2.0 : height / 2.0;
            var armed = ArmedTiles(@base);
            var spawns = new List<Vec2>();
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) if (tiles[y * width + x] == TileKinds.Floor && elev[y * width + x] == 0 && !armed.Has(y * width + x)) spawns.Add(new Vec2 { X = x, Y = y });
            Js.Sort(spawns, (a, b) =>
            {
                // `d || a.y - b.y || a.x - b.x`: 0 (or NaN) goes on to the next.
                var d = Js.Hypot(a.X - middleX, a.Y - middleY) - Js.Hypot(b.X - middleX, b.Y - middleY);
                if (d != 0 && !double.IsNaN(d)) return d;
                if (a.Y - b.Y != 0) return a.Y - b.Y;
                return a.X - b.X;
            });
            return new MissionLayout
            {
                Id = "base",
                Name = "El Bastión",
                Biome = Biomes.Wilds,
                Width = width,
                Height = height,
                Tiles = tiles,
                Elev = elev,
                Ground = ground,
                Decor = decor,
                // Everyone fit defends (§11.5): room for both players' whole rosters.
                Spawns = spawns.Take(BaseDefenders).ToList(),
                Pods = new List<PodSpot>(),
                Item = null,
                Terminal = null,
                ObjectiveSpot = null,
                EvacZone = new List<Vec2>(),
                Siege = true,
                Breakables = breakables,
                // The floodlights light the night twice as far as a street light (§11.4; a siege by night).
                Lights = @base.Pieces.Where((p) => p.Piece == PieceIds.Floodlight && p.Hp > 0).Select((p) => new LayoutLight { X = p.X, Y = p.Y, Radius = Content.Night.Floodlight }).ToList(),
            };
        }
    }
}
