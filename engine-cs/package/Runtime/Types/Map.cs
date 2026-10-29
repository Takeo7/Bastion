#nullable enable
// types.ts: the map's tiles, storeys, outline and decor.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>
    /// One storey above the ground (docs/FASE6-PLAN.md): `GameState.levels[k - 1]`
    /// is storey k, k storeys up. Only buildings have them, and only on ground at
    /// elevation 0.
    /// </summary>
    public sealed class Level
    {
        /// <summary>
        /// What stands on the storey's slab on each tile (index = y * width + x), as
        /// on the ground; null where there is no slab (open air, a stairwell).
        /// </summary>
        public List<string?> Tiles { get; set; } = new List<string?>(); // TileKinds
        /// <summary>What the storey's objects look like (decor.ts), as GameState.decor.</summary>
        public List<int>? Decor { get; set; }
    }

    /// <summary>
    /// Every tile also has an elevation (`GameState.elev`, in storeys). A walkable
    /// tile at elevation 1 is a rooftop: the block under it is solid.
    ///
    /// floor:        walkable.
    /// wall:         indestructible structure, one storey high — full cover, blocks movement and sight.
    /// high:         destructible full-cover object (container, pillar, tree) — degrades to `low`.
    /// low:          destructible half-cover object (crate, car, low wall) — degrades to `floor`.
    /// door:         closed door in a wall: walkable (opens when crossed), blocks sight, full cover.
    /// doorOpen:     open doorway: walkable, no cover.
    /// window:       window in a wall: crossed by vaulting (extra cost, can't stop on it),
    ///               transparent, full cover for whoever stands beside it. Breaks when crossed.
    /// windowBroken: same, without the glass.
    /// ladder:       walkable floor that also lets units climb to an orthogonal neighbour one storey up
    ///               (a ladder, or a building's stairs: decor 'stairs').
    /// console:      objective terminal — indestructible full-cover object.
    /// roof:         a storey's slab nobody stands on (a pitched roof, a roof with no way up):
    ///               only on GameState.levels.
    /// </summary>
    public static class TileKinds
    {
        public const string Floor = "floor";
        public const string Wall = "wall";
        public const string High = "high";
        public const string Low = "low";
        public const string Door = "door";
        public const string DoorOpen = "doorOpen";
        public const string Window = "window";
        public const string WindowBroken = "windowBroken";
        public const string Ladder = "ladder";
        public const string Console = "console";
        public const string Roof = "roof";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Floor, Wall, High, Low, Door, DoorOpen, Window, WindowBroken, Ladder, Console, Roof,
        };
    }

    /// <summary>Visual and generation theme of a map.</summary>
    public static class Biomes
    {
        public const string City = "city";
        public const string Wilds = "wilds";
        public const string Facility = "facility";

        public static readonly IReadOnlyList<string> All = new[] { City, Wilds, Facility };
    }

    // What a tile's floor is, on procedural maps: 0 open ground, 1 street or dirt track, 2 inside a building, 3 a yard,
    // 4 out of play (docs/MAPAS.md §5.10: the outline, a 'wall' the client draws as its piece of GameState.frame).
    //   export type GroundKind = 0 | 1 | 2 | 3 | 4;
    // An `int` in C#.

    /// <summary>
    /// What closes a map round what is played (docs/MAPAS.md §5.10): blocks shut up, fenced lots, ruins, works, a street
    /// that goes on past a barricade; a river or canal, the railway, the motorway or a military barrier along one side;
    /// in the country, containers, wood, a ravine, rocks or water.
    /// </summary>
    public static class FrameKinds
    {
        public const string Block = "block";
        public const string Lot = "lot";
        public const string Ruin = "ruin";
        public const string Works = "works";
        public const string Street = "street";
        public const string River = "river";
        public const string Rail = "rail";
        public const string Highway = "highway";
        public const string Barrier = "barrier";
        public const string Containers = "containers";
        public const string Forest = "forest";
        public const string Ravine = "ravine";
        public const string Rocks = "rocks";
        public const string Water = "water";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Block, Lot, Ruin, Works, Street, River, Rail, Highway, Barrier, Containers, Forest, Ravine, Rocks, Water,
        };
    }

    /// <summary>
    /// A piece of a map's outline (cosmetic): a rectangle of tiles out of play, every one of them 'wall' with ground 4,
    /// each in one piece only. `storeys` on a block, a ruin or works (and GameState.storeys of its tiles, for sight), maybe
    /// on the motorway and the railway; `side`, the map's side (a DIRS4 index) a river, the railway, the motorway or a
    /// barrier runs along.
    /// </summary>
    public sealed class FramePiece
    {
        public string Kind { get; set; } = ""; // FrameKinds
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int? Side { get; set; }
        public int? Storeys { get; set; }
    }

    /// <summary>
    /// What a piece of cover on a procedural map is meant to be, so the client can
    /// draw it as such: a parked car (two tiles, front and rear), a fence, a shelf…
    /// Purely cosmetic: cover, sight and movement only ever look at the tile kind.
    /// </summary>
    public static class DecorKinds
    {
        // Streets and car parks.
        public const string CarFront = "carFront";
        public const string CarRear = "carRear";
        public const string Planter = "planter";
        public const string Bin = "bin";
        public const string StreetTree = "streetTree";
        public const string Kiosk = "kiosk";
        public const string Fountain = "fountain";
        public const string FountainCorner = "fountainCorner";
        public const string Statue = "statue";
        // Farms and yards.
        public const string Fence = "fence";
        public const string GatePost = "gatePost";
        public const string Hay = "hay";
        // Indoors.
        public const string Shelf = "shelf";
        public const string Counter = "counter";
        public const string Table = "table";
        public const string Sofa = "sofa";
        public const string Bed = "bed";
        public const string HospitalBed = "hospitalBed";
        public const string MedicalShelf = "medicalShelf";
        public const string Gurney = "gurney";
        public const string MedicalCabinet = "medicalCabinet";
        public const string MedicalCart = "medicalCart";
        public const string Wheelchair = "wheelchair";
        public const string ReceptionDesk = "receptionDesk";
        public const string WaitingSeats = "waitingSeats";
        public const string ExamTable = "examTable";
        public const string MedicalMonitor = "medicalMonitor";
        public const string Xray = "xray";
        public const string BedsideTable = "bedsideTable";
        public const string VisitorChair = "visitorChair";
        public const string ClinicalSink = "clinicalSink";
        public const string Sterilizer = "sterilizer";
        public const string Ultrasound = "ultrasound";
        public const string WaitingTable = "waitingTable";
        public const string WaterDispenser = "waterDispenser";
        public const string Toilet = "toilet";
        public const string ClinicalBin = "clinicalBin";
        public const string ConsultDesk = "consultDesk";
        public const string Desk = "desk";
        public const string Wardrobe = "wardrobe";
        public const string Cabinet = "cabinet";
        public const string Rack = "rack";
        // Yards and facilities.
        public const string Crates = "crates";
        public const string Barricade = "barricade";
        public const string Pipe = "pipe";
        public const string Tank = "tank";
        public const string Pylon = "pylon";
        // Country.
        public const string Tree = "tree";
        public const string Bush = "bush";
        public const string Rock = "rock";
        public const string Log = "log";
        public const string Rubble = "rubble";
        // A building's stairs (docs/FASE6-PLAN.md): a `ladder` tile, facing the way up.
        public const string Stairs = "stairs";
        // A wall a grenade broke (docs/FASE7.md §4): its stump and the rubble, on a `low` tile.
        public const string Breach = "breach";
        // The bus parked by the evacuation zone (docs/PRUEBA-2026-10-14.md P1): `high` tiles the client draws it over.
        public const string Bus = "bus";
        // The special loot boxes (docs/FASE8.md §B), on a `low` tile: weapons, medicines, ammunition, food.
        public const string LootWeapons = "lootWeapons";
        public const string LootMedical = "lootMedical";
        public const string LootAmmo = "lootAmmo";
        public const string LootFood = "lootFood";
        // The new scenarios (playtest 2026-10-14, #25). Blocks of one kind, apart from each other: one object per block.
        /// <summary>A wrecked lorry, a 2 × 4 block of `high` tiles; facing: its nose.</summary>
        public const string Truck = "truck";
        /// <summary>A stack of logs, a 1 × 2 or 2 × 2 block of `low` tiles.</summary>
        public const string LogPile = "logPile";
        /// <summary>A water tower on its legs, a 2 × 2 block of `high` tiles.</summary>
        public const string WaterTower = "waterTower";
        /// <summary>A shipping container, a 2 × 5 block of `high` tiles; facing: its doors.</summary>
        public const string Container = "container";
        /// <summary>A big roadside sign on its post, one `high` tile; facing: towards the road.</summary>
        public const string Sign = "sign";
        /// <summary>Wire (a fallen fence or barbed wire), a line of `low` tiles like 'fence'.</summary>
        public const string Wire = "wire";
        // The factory (playtest 2026-10-15, #14 and #17).
        /// <summary>The nose of the patrol car or ambulance whose siren a sabotage silences, a `high` tile beside the objective; facing: away from it.</summary>
        public const string SirenCar = "sirenCar";
        /// <summary>Stacked pallets, a `low` tile; in rows between a factory's zones and by its loading docks.</summary>
        public const string Pallets = "pallets";
        /// <summary>Oil drums, a `low` tile.</summary>
        public const string Drums = "drums";
        /// <summary>A forklift, a `low` tile; facing: its forks. Not placed for now: no pack has one (playtest 15/10, #17).</summary>
        public const string Forklift = "forklift";
        /// <summary>Factory machinery, a `high` tile inside a hall.</summary>
        public const string Machine = "machine";
        /// <summary>A silo, a 3 × 3 block of `high` tiles (docs/MAPAS.md §5: 2 × 2 before phase 5).</summary>
        public const string Silo = "silo";
        /// <summary>A park bench on a square (playtest 2026-10-18, #12), two `low` tiles side by side; facing: the way one sits.</summary>
        public const string Bench = "bench";
        // A house's rooms by what they are (playtest 2026-10-18, #15): the kitchen's, the bathroom's, the living room's.
        /// <summary>A fridge, a `high` tile against a kitchen's wall; facing: its door.</summary>
        public const string Fridge = "fridge";
        /// <summary>A cooker, a `low` tile in a kitchen's run of counters; facing: its front.</summary>
        public const string Stove = "stove";
        /// <summary>A bath, a `low` tile in a bathroom's corner; facing: away from the wall.</summary>
        public const string Bathtub = "bathtub";
        /// <summary>A washbasin, a `low` tile against a bathroom's wall; facing: away from it.</summary>
        public const string Washbasin = "washbasin";
        /// <summary>The television on its stand, a `low` tile against a living room's wall, facing the sofa.</summary>
        public const string TvStand = "tvStand";
        /// <summary>A house's toilet (not the clinics' 'toilet'), a `low` tile in a bathroom's corner.</summary>
        public const string Wc = "wc";
        // What used to be drawn on free floor, now on tiles of its own (Arturo, 19/10: «que ocupen»; furnishing.ts). All `low`
        // but the drinks fridge; the client draws each as before (homeFurnishing.ts, dressing.ts).
        /// <summary>An armchair across the table from a sofa, or at its side; facing: the way one sits.</summary>
        public const string Armchair = "armchair";
        /// <summary>A coffee table before a sofa; facing: the sofa's.</summary>
        public const string CoffeeTable = "coffeeTable";
        /// <summary>A standard lamp in a corner.</summary>
        public const string FloorLamp = "floorLamp";
        /// <summary>A potted plant in a corner (or a dining chair's place in a bedroom's).</summary>
        public const string PottedPlant = "pottedPlant";
        /// <summary>A house's bedside table by its bed (its lamp on it); facing: the bed's.</summary>
        public const string Nightstand = "nightstand";
        /// <summary>A chair on its own in a bedroom's corner; facing: into the room.</summary>
        public const string DiningChair = "diningChair";
        /// <summary>A towel rail against a bathroom's wall; facing: away from it.</summary>
        public const string TowelRack = "towelRack";
        /// <summary>A coat stand in a hall's corner; facing: away from the wall.</summary>
        public const string CoatRack = "coatRack";
        /// <summary>A café's table, its two chairs tucked in on its tile.</summary>
        public const string CafeTable = "cafeTable";
        /// <summary>A bookshop's or a library's reading table, its chair tucked in on its tile.</summary>
        public const string ReadingTable = "readingTable";
        /// <summary>A grocer's drinks fridge, a `high` tile against a wall; facing: its door.</summary>
        public const string DrinksFridge = "drinksFridge";
        /// <summary>A barn's wheelbarrow.</summary>
        public const string Wheelbarrow = "wheelbarrow";
        /// <summary>A barn's tool leant against its wall (a pitchfork, a rake, a spade); facing: the wall.</summary>
        public const string Tools = "tools";
        /// <summary>A fire hydrant by the kerb; facing: the street.</summary>
        public const string Hydrant = "hydrant";
        /// <summary>A mailbox by the kerb; facing: the street.</summary>
        public const string Mailbox = "mailbox";
        /// <summary>A bollard along a sidewalk, low cover (since 28/10, where a street tree in a pot stood); facing: the street.</summary>
        public const string Bollard = "bollard";
        /// <summary>What lies about and stands up (homeDressing, medicalAbandonment: a chair over, bags, a fallen drip): its MedicalDetail draws it.</summary>
        public const string Strewn = "strewn";
        /// <summary>A garden shed at the back of a plot (docs/MAPAS.md §5.4), 2 × 3; facing: its door.</summary>
        public const string Shed = "shed";
        /// <summary>A washing line between two posts across a back garden, 1 × 3; facing: along it.</summary>
        public const string WashingLine = "washingLine";
        /// <summary>A vegetable patch in a back garden, 2 × 3, low cover; facing: along its rows.</summary>
        public const string VegPatch = "vegPatch";
        /// <summary>
        /// Furniture piled against a window from inside so the dead don't get in (playtest 21/10, #20: a wardrobe on its
        /// side, a table, a mattress, planks), a `high` tile; every window of its room has one. Facing: the window.
        /// </summary>
        public const string WindowBarricade = "windowBarricade";
        /// <summary>
        /// Another tile of an object that stands over several (docs/MAPAS.md, phase 1: a car 1 × 3, a bed 1 × 2): what it is
        /// is its anchor's, the tile that says the object's kind; facing: the step towards the anchor (decor.ts objectTiles).
        /// </summary>
        public const string Part = "part";

        public static readonly IReadOnlyList<string> All = new[]
        {
            CarFront, CarRear, Planter, Bin, StreetTree, Kiosk, Fountain, FountainCorner, Statue, Fence, GatePost, Hay,
            Shelf, Counter, Table, Sofa, Bed, HospitalBed, MedicalShelf, Gurney, MedicalCabinet, MedicalCart,
            Wheelchair, ReceptionDesk, WaitingSeats, ExamTable, MedicalMonitor, Xray, BedsideTable, VisitorChair,
            ClinicalSink, Sterilizer, Ultrasound, WaitingTable, WaterDispenser, Toilet, ClinicalBin, ConsultDesk, Desk,
            Wardrobe, Cabinet, Rack, Crates, Barricade, Pipe, Tank, Pylon, Tree, Bush, Rock, Log, Rubble, Stairs,
            Breach, Bus, LootWeapons, LootMedical, LootAmmo, LootFood, Truck, LogPile, WaterTower, Container, Sign,
            Wire, SirenCar, Pallets, Drums, Forklift, Machine, Silo, Bench, Fridge, Stove, Bathtub, Washbasin, TvStand,
            Wc, Armchair, CoffeeTable, FloorLamp, PottedPlant, Nightstand, DiningChair, TowelRack, CoatRack, CafeTable,
            ReadingTable, DrinksFridge, Wheelbarrow, Tools, Hydrant, Mailbox, Strewn, Shed, WashingLine, VegPatch,
            WindowBarricade, Bollard, Part,
        };
    }

    /// <summary>What makes the noise a sabotage has to silence (playtest 2026-10-15, #14): a patrol car, an ambulance, a factory's alarm.</summary>
    public static class Alarms
    {
        public const string Police = "police";
        public const string Ambulance = "ambulance";
        public const string Factory = "factory";

        public static readonly IReadOnlyList<string> All = new[] { Police, Ambulance, Factory };
    }

    /// <summary>A scenario a generated map can be (playtest 2026-10-14, #25): a forest's service area, a city's industrial estate.</summary>
    public static class Sites
    {
        public const string Roadside = "roadside";
        public const string Industrial = "industrial";

        public static readonly IReadOnlyList<string> All = new[] { Roadside, Industrial };
    }

    // A tile's decor hint packed in one number (0: none); see decor.ts.
    //   export type Decor = number;
    // An `int` in C# (kind * 4 + facing: decor.ts decorCode).

    // 0 = none, 1 = half (low), 2 = full (high).
    //   export type CoverLevel = 0 | 1 | 2;
    // An `int` in C#.
}
