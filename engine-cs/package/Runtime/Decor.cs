#nullable enable
// decor.ts: what each piece of cover on a generated map is meant to look like.
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>decorOf's answer: `{ kind: DecorKind; facing: number }`.</summary>
    public sealed class DecorLook
    {
        public string Kind { get; set; } = ""; // DecorKinds
        public int Facing { get; set; }
    }

    /// <summary>objectAt's answer: `{ kind: DecorKind; facing: number; anchor: number; tiles: number[] }`.</summary>
    public sealed class DecorObject
    {
        public string Kind { get; set; } = ""; // DecorKinds
        public int Facing { get; set; }
        public int Anchor { get; set; }
        public List<int> Tiles { get; set; } = new List<int>();
    }

    // Decor hints: what the generator meant each piece of cover to be (a parked
    // car, a fence, a warehouse shelf…) and which way it faces, packed in one
    // small number per tile so the layer weighs no more than the ground layer.
    //
    // `facing` is a DIRS4 index: the way the object's front looks (a car's nose, a
    // sofa's seat, the doors of a wardrobe). Objects that come in lines (fences,
    // shelf rows, pipes, a counter) face across their line, so a line running
    // east–west faces north or south.
    //
    // An object over several tiles (docs/MAPAS.md, phase 1: a car 1 × 3, a bed
    // 1 × 2) says what it is on one of them, its anchor; each of its other tiles
    // is a `part` facing the step towards the anchor (objectTiles). A mission
    // saved before has none: each object is on one tile, a car on two halves
    // (carPartner), a lorry or a container a block of its kind (BLOCK_KINDS).
    //
    // The codes are kept in saved missions: add new kinds at the end, never
    // renumber.
    public static class Decor
    {
        private static readonly IReadOnlyDictionary<string, int> Ids = new Dictionary<string, int>
        {
            [DecorKinds.CarFront] = 1,
            [DecorKinds.CarRear] = 2,
            [DecorKinds.Planter] = 3,
            [DecorKinds.Bin] = 4,
            [DecorKinds.StreetTree] = 5,
            [DecorKinds.Kiosk] = 6,
            [DecorKinds.Fountain] = 7,
            [DecorKinds.FountainCorner] = 8,
            [DecorKinds.Statue] = 9,
            [DecorKinds.Fence] = 10,
            [DecorKinds.GatePost] = 11,
            [DecorKinds.Hay] = 12,
            [DecorKinds.Shelf] = 13,
            [DecorKinds.Counter] = 14,
            [DecorKinds.Table] = 15,
            [DecorKinds.Sofa] = 16,
            [DecorKinds.Bed] = 17,
            [DecorKinds.Desk] = 18,
            [DecorKinds.Wardrobe] = 19,
            [DecorKinds.Cabinet] = 20,
            [DecorKinds.Rack] = 21,
            [DecorKinds.Crates] = 22,
            [DecorKinds.Barricade] = 23,
            [DecorKinds.Pipe] = 24,
            [DecorKinds.Tank] = 25,
            [DecorKinds.Pylon] = 26,
            [DecorKinds.Tree] = 27,
            [DecorKinds.Bush] = 28,
            [DecorKinds.Rock] = 29,
            [DecorKinds.Log] = 30,
            [DecorKinds.Rubble] = 31,
            [DecorKinds.Stairs] = 32,
            [DecorKinds.Breach] = 33,
            [DecorKinds.LootWeapons] = 34,
            [DecorKinds.LootMedical] = 35,
            [DecorKinds.LootAmmo] = 36,
            [DecorKinds.LootFood] = 37,
            [DecorKinds.Bus] = 38,
            [DecorKinds.Truck] = 39,
            [DecorKinds.LogPile] = 40,
            [DecorKinds.WaterTower] = 41,
            [DecorKinds.Container] = 42,
            [DecorKinds.Sign] = 43,
            [DecorKinds.Wire] = 44,
            [DecorKinds.SirenCar] = 45,
            [DecorKinds.Pallets] = 46,
            [DecorKinds.Drums] = 47,
            [DecorKinds.Forklift] = 48,
            [DecorKinds.Machine] = 49,
            [DecorKinds.Silo] = 50,
            // The medical centres (PR #1, Ana): numbered after the factory's of the 0.3.0, which saved missions keep.
            [DecorKinds.HospitalBed] = 51,
            [DecorKinds.MedicalShelf] = 52,
            [DecorKinds.Gurney] = 53,
            [DecorKinds.MedicalCabinet] = 54,
            [DecorKinds.MedicalCart] = 55,
            [DecorKinds.Wheelchair] = 56,
            [DecorKinds.ReceptionDesk] = 57,
            [DecorKinds.WaitingSeats] = 58,
            [DecorKinds.ExamTable] = 59,
            [DecorKinds.MedicalMonitor] = 60,
            [DecorKinds.Xray] = 61,
            [DecorKinds.BedsideTable] = 62,
            [DecorKinds.VisitorChair] = 63,
            [DecorKinds.ClinicalSink] = 64,
            [DecorKinds.Sterilizer] = 65,
            [DecorKinds.Ultrasound] = 66,
            [DecorKinds.WaitingTable] = 67,
            [DecorKinds.WaterDispenser] = 68,
            [DecorKinds.Toilet] = 69,
            [DecorKinds.ClinicalBin] = 70,
            [DecorKinds.ConsultDesk] = 71,
            // The squares (playtest 2026-10-18, #12).
            [DecorKinds.Bench] = 72,
            // A house's rooms (playtest 2026-10-18, #15).
            [DecorKinds.Fridge] = 73,
            [DecorKinds.Stove] = 74,
            [DecorKinds.Bathtub] = 75,
            [DecorKinds.Washbasin] = 76,
            [DecorKinds.TvStand] = 77,
            [DecorKinds.Wc] = 78,
            // What stands on the floor, on tiles of its own (Arturo, 19/10: «que ocupen»; furnishing.ts).
            [DecorKinds.Armchair] = 79,
            [DecorKinds.CoffeeTable] = 80,
            [DecorKinds.FloorLamp] = 81,
            [DecorKinds.PottedPlant] = 82,
            [DecorKinds.Nightstand] = 83,
            [DecorKinds.DiningChair] = 84,
            [DecorKinds.TowelRack] = 85,
            [DecorKinds.CoatRack] = 86,
            [DecorKinds.CafeTable] = 87,
            [DecorKinds.ReadingTable] = 88,
            [DecorKinds.DrinksFridge] = 89,
            [DecorKinds.Wheelbarrow] = 90,
            [DecorKinds.Tools] = 91,
            [DecorKinds.Hydrant] = 92,
            [DecorKinds.Mailbox] = 93,
            [DecorKinds.Strewn] = 94,
            // Objects over several tiles (docs/MAPAS.md, phase 1): every tile but the anchor.
            [DecorKinds.Part] = 95,
            // A block's back gardens (docs/MAPAS.md §5.4, phase 4).
            [DecorKinds.Shed] = 96,
            [DecorKinds.WashingLine] = 97,
            [DecorKinds.VegPatch] = 98,
            // A room's windows barricaded from inside (playtest 21/10, #20).
            [DecorKinds.WindowBarricade] = 99,
            // Along the sidewalks, where the street trees in pots stood (Ana's playtest 28/10).
            [DecorKinds.Bollard] = 100,
        };

        private static readonly string?[] Kinds = MakeKinds();

        private static string?[] MakeKinds()
        {
            var kinds = new string?[Ids.Values.Max() + 1];
            foreach (var e in Ids) kinds[e.Value] = e.Key;
            return kinds;
        }

        /// <summary>The code of a decor kind (IDS), for what reads it as a number.</summary>
        public static int IdOf(string kind)
        {
            return Ids[kind];
        }

        /// <summary>Packs a hint; `facing` is a DIRS4 index.</summary>
        public static int DecorCode(string kind, int facing = 0)
        {
            return Ids[kind] * 4 + (((facing % 4) + 4) % 4);
        }

        /// <summary>Unpacks a hint; null for none (0, missing, or a kind this version doesn't know).</summary>
        public static DecorLook? DecorOf(int? code)
        {
            if (code == null || code == 0) return null;
            var id = code.Value >> 2;
            var kind = id >= 0 && id < Kinds.Length ? Kinds[id] : null;
            return kind != null ? new DecorLook { Kind = kind, Facing = code.Value & 3 } : null;
        }

        /// <summary>`decor[i]`, undefined (null) past its end.</summary>
        private static int? CodeAt(IReadOnlyList<int>? decor, int i)
        {
            return decor != null && i >= 0 && i < decor.Count ? decor[i] : (int?)null;
        }

        /// <summary>
        /// For half of a parked car, the tile of its other half (the front is the half
        /// the car faces, the rear the one behind it); -1 if `i` is not half of a whole car.
        /// </summary>
        public static int CarPartner(IReadOnlyList<int> decor, int width, int i)
        {
            var look = DecorOf(CodeAt(decor, i));
            if (look == null || (look.Kind != DecorKinds.CarFront && look.Kind != DecorKinds.CarRear)) return -1;
            var d = Grid.Dirs4[look.Facing];
            var k = look.Kind == DecorKinds.CarFront ? -1 : 1;
            var x = (i % width) + d.X * k;
            var y = (int)System.Math.Floor((double)i / width) + d.Y * k;
            if (x < 0 || y < 0 || x >= width || y * width >= decor.Count) return -1;
            var j = y * width + x;
            var other = DecorOf(CodeAt(decor, j));
            return other != null && other.Facing == look.Facing && other.Kind == (look.Kind == DecorKinds.CarFront ? DecorKinds.CarRear : DecorKinds.CarFront) ? j : -1;
        }

        /// <summary>
        /// How many tiles each of the generator's objects over several takes
        /// (docs/MAPAS.md §4.5–4.6), as [wide, deep]: wide across the way it faces,
        /// deep along it. A car's anchor is its nose; a patrol car's or an
        /// ambulance's tiles count the one its siren stands on; a lorry is 2 × 6 where
        /// it fits and 2 × 4 where not; a van is for the car parks of phase 3.
        /// </summary>
        public static class Footprint
        {
            public static readonly IReadOnlyList<int> Car = new[] { 1, 3 };
            public static readonly IReadOnlyList<int> PatrolCar = new[] { 1, 3 };
            public static readonly IReadOnlyList<int> Ambulance = new[] { 2, 4 };
            public static readonly IReadOnlyList<int> Van = new[] { 2, 4 };
            public static readonly IReadOnlyList<int> Tractor = new[] { 2, 3 };
            public static readonly IReadOnlyList<int> Truck = new[] { 2, 6 };
            public static readonly IReadOnlyList<int> TruckShort = new[] { 2, 4 };
            public static readonly IReadOnlyList<int> Container = new[] { 2, 5 };
            public static readonly IReadOnlyList<int> Bus = new[] { 2, 5 };
            public static readonly IReadOnlyList<int> Bed = new[] { 1, 2 };
            public static readonly IReadOnlyList<int> DoubleBed = new[] { 2, 2 };
            public static readonly IReadOnlyList<int> Sofa = new[] { 2, 1 };
            public static readonly IReadOnlyList<int> Bathtub = new[] { 2, 1 };
            public static readonly IReadOnlyList<int> Table = new[] { 2, 1 };
            public static readonly IReadOnlyList<int> HospitalBed = new[] { 1, 2 };
            public static readonly IReadOnlyList<int> Gurney = new[] { 1, 2 };
            public static readonly IReadOnlyList<int> Shed = new[] { 2, 3 };
            public static readonly IReadOnlyList<int> WashingLine = new[] { 1, 3 };
            public static readonly IReadOnlyList<int> VegPatch = new[] { 2, 3 };
        }

        /// <summary>
        /// Objects saved as blocks, every tile of one carrying its kind (playtest
        /// 14/10, #25): a lorry (a barn's tractor too), a container, a water tower, a
        /// silo, a log pile, a square's bench. The generator never lets two touch, so
        /// one is the tiles of its kind joined to each other.
        /// </summary>
        public static readonly IReadOnlyCollection<string> BlockKinds = new HashSet<string>
        {
            DecorKinds.Truck, DecorKinds.Container, DecorKinds.WaterTower, DecorKinds.Silo, DecorKinds.LogPile, DecorKinds.Bench,
        };

        /// <summary>The longest way from a part to its anchor (the biggest object, a lorry of 2 × 6, takes 6).</summary>
        private const int Reach = 16;

        /// <summary>The tile next to tile i, `f` a DIRS4 index; -1 off the map.</summary>
        private static int Next(int width, int size, int i, int f)
        {
            var d = Grid.Dirs4[f];
            var x = (i % width) + d.X;
            var y = (int)System.Math.Floor((double)i / width) + d.Y;
            return x < 0 || y < 0 || x >= width || y * width >= size ? -1 : y * width + x;
        }

        /// <summary>
        /// How tall the objects that are full cover but lower than a soldier's eyes stand, for sight (Arturo, 22/10: «los
        /// campos de visión han de ser reales… no puede ser que ese objeto bajo tape la visión como si fuese una columna
        /// gigante»): sight goes over them (grid.ts groundSightHeight). Cover is the same. The rest stand a whole full
        /// cover's height, taller than the eyes. Measured on what is drawn (Arte, 22/10) and put on the rules' scale: a
        /// drawn soldier's eyes are at 1.24 world units, the rules' at EYE_HEIGHT (0.7 storeys, 1.61), so each drawn height
        /// counts 1.3 times, and whatever is below a drawn soldier's eyes is below the rules' too. In storeys.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, double> SightHeight = new Dictionary<string, double>
        {
            // A gas tank lying on its legs, 1.09 drawn (playtest 22/10 #47).
            [DecorKinds.Tank] = 0.62,
            // Stacked: crates 1.11, pallets 1.11, drums 1.16; the big generator 1.16; a drinks fridge 1.07.
            [DecorKinds.Crates] = 0.63,
            [DecorKinds.Pallets] = 0.63,
            [DecorKinds.Drums] = 0.66,
            [DecorKinds.Machine] = 0.66,
            [DecorKinds.DrinksFridge] = 0.6,
            // A workshop's shelf 1.22, a water dispenser 1.20: just under the eyes.
            [DecorKinds.Rack] = 0.69,
            [DecorKinds.WaterDispenser] = 0.68,
        };

        /// <summary>For sight, the height of the object tile `i` is part of, when it is one of SIGHT_HEIGHT's; null for any other.</summary>
        public static double? DecorSightHeight(IReadOnlyList<int>? decor, int width, int i)
        {
            if (decor == null) return null;
            var a = AnchorOf(decor, width, i);
            var look = a >= 0 ? DecorOf(CodeAt(decor, a)) : null;
            return look != null && SightHeight.TryGetValue(look.Kind, out var h) ? h : (double?)null;
        }

        /// <summary>The anchor of the object tile i is a part of (i itself for anything else dressed); -1 for nothing, or a part that leads nowhere.</summary>
        public static int AnchorOf(IReadOnlyList<int> decor, int width, int i)
        {
            var j = i;
            for (var n = 0; n < Reach && j >= 0; n++)
            {
                var look = DecorOf(CodeAt(decor, j));
                if (look == null) return -1;
                if (look.Kind != DecorKinds.Part) return j;
                j = Next(width, decor.Count, j, look.Facing);
            }
            return -1;
        }

        /// <summary>The tiles of the object anchored on tile `a`: the anchor, then every part that leads to it, nearest first.</summary>
        public static List<int> ObjectTiles(IReadOnlyList<int> decor, int width, int a)
        {
            var out_ = new List<int> { a };
            for (var k = 0; k < out_.Count; k++)
            {
                for (var f = 0; f < 4; f++)
                {
                    var j = Next(width, decor.Count, out_[k], f);
                    var look = j >= 0 ? DecorOf(CodeAt(decor, j)) : null;
                    // A part beside it that steps onto it.
                    if (look?.Kind == DecorKinds.Part && look.Facing == (f + 2) % 4 && !out_.Contains(j)) out_.Add(j);
                }
            }
            return out_;
        }

        /// <summary>
        /// The whole object tile i is (part of), in any of the forms maps have had: an
        /// anchor and its parts; a car on two halves (carPartner); a block of its kind
        /// (BLOCK_KINDS). Its kind and facing, its anchor and its tiles (the anchor
        /// first). Null where nothing is dressed, or for a part that leads nowhere.
        /// </summary>
        public static DecorObject? ObjectAt(IReadOnlyList<int> decor, int width, int i)
        {
            var anchor = AnchorOf(decor, width, i);
            if (anchor < 0) return null;
            var look = DecorOf(CodeAt(decor, anchor))!;
            var tiles = ObjectTiles(decor, width, anchor);
            if (tiles.Count > 1) return new DecorObject { Kind = look.Kind, Facing = look.Facing, Anchor = anchor, Tiles = tiles };
            if (look.Kind == DecorKinds.CarFront || look.Kind == DecorKinds.CarRear)
            {
                var other = CarPartner(decor, width, anchor);
                if (other >= 0)
                {
                    if (look.Kind == DecorKinds.CarFront) return new DecorObject { Kind = look.Kind, Facing = look.Facing, Anchor = anchor, Tiles = new List<int> { anchor, other } };
                    var front = DecorOf(CodeAt(decor, other))!;
                    return new DecorObject { Kind = front.Kind, Facing = front.Facing, Anchor = other, Tiles = new List<int> { other, anchor } };
                }
            }
            if (BlockKinds.Contains(look.Kind))
            {
                var block = new List<int> { anchor };
                for (var k = 0; k < block.Count; k++)
                {
                    for (var f = 0; f < 4; f++)
                    {
                        var j = Next(width, decor.Count, block[k], f);
                        if (j >= 0 && DecorOf(CodeAt(decor, j))?.Kind == look.Kind && !block.Contains(j)) block.Add(j);
                    }
                }
                // Distinct numbers: any sort gives the same order.
                block.Sort();
                return new DecorObject { Kind = look.Kind, Facing = look.Facing, Anchor = block[0], Tiles = block };
            }
            return new DecorObject { Kind = look.Kind, Facing = look.Facing, Anchor = anchor, Tiles = tiles };
        }
    }
}
