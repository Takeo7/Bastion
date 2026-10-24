using System;
using System.Collections.Generic;
using Bastion.Engine;

namespace Bastion.Client.Map
{
    // 0.10.1's buildings.ts, its swatches and colours: the patches of Town's atlas that floors and roofs are painted with
    // (repeated in world space), and the colours each kind of building is picked from. Plain C#, no UnityEngine: the
    // colours are three.js's (THREE.Color: doubles, linear), the patches as the shader reads them.

    /// <summary>
    /// A linear RGB colour as three.js holds one (THREE.Color in its working space, linear sRGB, in doubles: the
    /// buffers take it as floats at the end).
    /// </summary>
    public readonly struct Rgb
    {
        public readonly double r, g, b;

        public Rgb(double r, double g, double b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        /// <summary>`new THREE.Color(hex)`: an sRGB hex colour, linear (three's ColorManagement, SRGBToLinear).</summary>
        public static Rgb Hex(int hex) => new Rgb(Linear((hex >> 16) & 255), Linear((hex >> 8) & 255), Linear(hex & 255));

        /// <summary>
        /// three's SRGBToLinear on a channel of 0–255 (three 0.186: the same constants, the same order). Math.Pow is the C
        /// runtime's, V8's Math.pow its own: on 30 channels of the 256 the last bit of the double differs (V8's is the one
        /// off); as the floats the buffers and Unity take, every one is the same.
        /// </summary>
        static double Linear(int channel)
        {
            double c = channel / 255.0;
            return c < 0.04045 ? c * 0.0773993808 : Math.Pow(c * 0.9478672986 + 0.0521327014, 2.4);
        }

        /// <summary>`clone().multiplyScalar(k)`.</summary>
        public Rgb Scale(double k) => new Rgb(r * k, g * k, b * k);
    }

    /// <summary>
    /// A patch of an atlas (glTF uv: u right, v down) that Synty paints a whole surface with, repeated every `period`
    /// tiles in world space. A patch of no size is one of the palette's flat colours.
    /// </summary>
    public sealed class Swatch
    {
        /// <summary>The atlas it is on (AtlasId: "town").</summary>
        public readonly string atlas;
        /// <summary>u, v of its corner and its width and height, inset (as aSwatch takes it).</summary>
        public readonly float[] rect;
        /// <summary>Tiles it repeats every.</summary>
        public readonly double period;

        Swatch(string atlas, double u, double v, double w, double h, double period)
        {
            this.atlas = atlas;
            rect = new[] { (float)u, (float)v, (float)w, (float)h };
            this.period = period;
        }

        /// <summary>buildings.ts `swatch`: inset so mipmaps don't bleed the neighbouring patches in.</summary>
        public static Swatch Of(string atlas, double u0, double v0, double u1, double v1, double period, double inset = 0.004) =>
            new Swatch(atlas, u0 + inset, v0 + inset, Math.Max(0, u1 - u0 - 2 * inset), Math.Max(0, v1 - v0 - 2 * inset), period);

        /// <summary>A grey of the ramp along the bottom of Town's atlas, from black (0) to white (1): a flat colour.</summary>
        public static Swatch Grey(double k) => new Swatch("town", 0.0078 + Js.Round(k * 15) * 0.03125, 0.985, 0, 0, 1);

        // Town's atlas (PolygonTown_01_A): measured from the kit's own floor, roof and wall pieces.
        /// <summary>Floorboards (Interior_Floor).</summary>
        public static readonly Swatch Boards = Of("town", 0.2534, 0.2518, 0.4959, 0.4943, 2);
        /// <summary>Shingles (Roof_Angled): stripes down the slope.</summary>
        public static readonly Swatch Shingles = Of("town", 0.507, 0.2579, 0.6689, 0.4346, 2);
        /// <summary>Weatherboard siding (the walls' outer face).</summary>
        public static readonly Swatch Siding = Of("town", 0.5, 0.007, 0.669, 0.25, 2);
        /// <summary>Ceiling panels (Interior_Ceiling): tiled floors.</summary>
        public static readonly Swatch Panels = Of("town", 0.0103, 0.2591, 0.2407, 0.4895, 1.6);
        /// <summary>White: parapets, tar, concrete, in the colour they are given.</summary>
        public static readonly Swatch Flat = Grey(1);
    }

    /// <summary>The patches by name (`keyof typeof SWATCH`, `keyof typeof MEAN`): plaster has a mean colour and no swatch.</summary>
    public static class Patches
    {
        public const string Boards = "boards";
        public const string Shingles = "shingles";
        public const string Siding = "siding";
        public const string Plaster = "plaster";
        public const string Panels = "panels";
        public const string Flat = "flat";
    }

    /// <summary>
    /// How each kind of building looks, as sRGB colours picked per building: its siding, the plaster inside, its floor
    /// (on which patch) and its roof (shingles on gables, tar on flat roofs).
    /// </summary>
    public sealed class BuildingStyle
    {
        public int[] outer;
        public int[] inner;
        /// <summary>The floor's patch (Patches) and its colours.</summary>
        public string floorPatch;
        public int[] floors;
        /// <summary>Shingles of a gable roof.</summary>
        public int[] roof;
        /// <summary>A flat roof's membrane: pale concrete and render, as in Synty's captures (weathered in the shader); null: BuildingLooks.FlatRoofs.</summary>
        public int[] flat;
    }

    /// <summary>The colours of a building, as tints of the patches they go on (buildings.ts looksOf).</summary>
    public sealed class Looks
    {
        /// <summary>On the siding of walls and gables, and the plaster inside.</summary>
        public Rgb outer;
        public Rgb inner;
        /// <summary>A flat colour: parapets.</summary>
        public Rgb trim;
        public Rgb floor;
        public Swatch floorSwatch;
        public Rgb shingles;
        public Rgb tar;
    }

    /// <summary>The buildings' colour tables (buildings.ts MEAN, STYLES, HOME_FLOORS…).</summary>
    public static class BuildingLooks
    {
        /// <summary>The average colour Synty painted each patch (sRGB), to tint it towards another.</summary>
        static readonly Dictionary<string, int> Mean = new Dictionary<string, int>
        {
            [Patches.Boards] = 0x763d35, [Patches.Shingles] = 0x2f363e, [Patches.Siding] = 0xbdc2bf, [Patches.Plaster] = 0x8fa4aa,
            [Patches.Panels] = 0xb6b5b5, [Patches.Flat] = 0xffffff,
        };

        /// <summary>MEAN[patch].</summary>
        public static int MeanOf(string patch) => Mean[patch];

        /// <summary>SWATCH[patch]; plaster has none.</summary>
        public static Swatch SwatchOf(string patch)
        {
            switch (patch)
            {
                case Patches.Boards: return Swatch.Boards;
                case Patches.Shingles: return Swatch.Shingles;
                case Patches.Siding: return Swatch.Siding;
                case Patches.Panels: return Swatch.Panels;
                case Patches.Flat: return Swatch.Flat;
                default: throw new ArgumentException($"No swatch for '{patch}'", nameof(patch));
            }
        }

        /// <summary>The (linear) multiplier that turns a patch whose average is `mean` into `target` (both sRGB).</summary>
        public static Rgb TintTo(int target, int mean)
        {
            var t = Rgb.Hex(target);
            var m = Rgb.Hex(mean);
            return new Rgb(t.r / m.r, t.g / m.g, t.b / m.b);
        }

        /// <summary>Flat roofs that say nothing of their own.</summary>
        public static readonly int[] FlatRoofs = { 0xcdbfb3, 0xc2b8ae, 0xd5c8b8, 0xbab2aa, 0xc9bba4 };

        public static readonly IReadOnlyDictionary<string, BuildingStyle> Styles = new Dictionary<string, BuildingStyle>
        {
            [BuildingKinds.Medical] = new BuildingStyle
            {
                outer = new[] { 0xe0ddd0 }, inner = new[] { 0xe0ddd0 },
                floorPatch = Patches.Flat, floors = new[] { 0xe0ddd0 }, roof = new[] { 0x65645e },
                flat = new[] { 0xb7b5ac },
            },
            [BuildingKinds.House] = new BuildingStyle
            {
                outer = new[] { 0xeee4cc, 0xe8d49c, 0xc0d4b6, 0xb8cce0, 0xe8c2ae, 0xf2f2ec, 0xccccc6, 0xc0ded0 },
                inner = new[] { 0xd6ccb8, 0xc0ccb8, 0xdac0ac, 0xbac4ce, 0xccccc8 },
                floorPatch = Patches.Boards, floors = new[] { 0x966c46, 0x765236, 0xac845c, 0x88543e },
                roof = new[] { 0x484442, 0x763e32, 0x425262, 0x58624a, 0x665446 },
            },
            [BuildingKinds.Shop] = new BuildingStyle
            {
                outer = new[] { 0xe8d49c, 0xe8c2ae, 0xb8cce0, 0xc0ded0, 0xdcaa78, 0xaac896 },
                inner = new[] { 0xe6e0d4, 0xd6dce2, 0xe2d8c8 },
                floorPatch = Patches.Panels, floors = new[] { 0xd6d2c8, 0xc4ccd2, 0xdcd4be },
                roof = new[] { 0x5a5a58, 0x4e5256, 0x625a52 },
                flat = new[] { 0xd2c4b6, 0xc8bcae, 0xdacbb8, 0xc6b2a2 },
            },
            [BuildingKinds.Office] = new BuildingStyle
            {
                outer = new[] { 0xdcd6c8, 0xbebfbe, 0xd0c4b0, 0xc4ccd0 },
                inner = new[] { 0xe2e2de, 0xd8d4cc, 0xd2d8dc },
                floorPatch = Patches.Panels, floors = new[] { 0xaaaeb2, 0xbab4a8, 0xa4acb4 },
                roof = new[] { 0x58585a, 0x4c4e52 },
            },
            [BuildingKinds.Hall] = new BuildingStyle
            {
                outer = new[] { 0xaaa08c, 0x96a0a5, 0xb49678, 0x9c9c94 },
                inner = new[] { 0xb4b4ae, 0xaeb2b4 },
                floorPatch = Patches.Flat, floors = new[] { 0x8c8a86, 0x9a948a, 0x868c90 },
                roof = new[] { 0x4e4e4c, 0x5c544a, 0x484e54 },
                // Warehouses: sheet metal, darker.
                flat = new[] { 0x7c8086, 0x8a8478, 0x737a80 },
            },
            [BuildingKinds.Barn] = new BuildingStyle
            {
                outer = new[] { 0x9c3a2c, 0x8a3428, 0xa8a49a, 0x7c5a3c },
                inner = new[] { 0xa88c6c, 0x9c8468 },
                // Worn boards (#53), not a hall's concrete.
                floorPatch = Patches.Boards, floors = new[] { 0x7c6446, 0x6e5a40 },
                roof = new[] { 0x4a4a48, 0x6a4a3a, 0x5a5e62 },
            },
            // A roadside motel: pastel render and a flat roof.
            [BuildingKinds.Motel] = new BuildingStyle
            {
                outer = new[] { 0xe8d2b0, 0xd6e2d2, 0xf0c8b4, 0xcfdce6 },
                inner = new[] { 0xe6dccb, 0xd8d2c4 },
                floorPatch = Patches.Boards, floors = new[] { 0x8a6e52, 0x7a5e44 },
                roof = new[] { 0x5a5a58 },
                flat = new[] { 0xc9bcab, 0xd2c6b2 },
            },
            [BuildingKinds.Ruin] = new BuildingStyle
            {
                outer = new[] { 0xb8b0a0, 0xa8a49a, 0x9a9488 },
                inner = new[] { 0xa8a49a, 0x9c9890 },
                floorPatch = Patches.Flat, floors = new[] { 0x8c8a86 },
                roof = new[] { 0x4a4a48 },
            },
        };

        /// <summary>`list[Math.floor(k * list.length) % list.length]`, k in [0, 1).</summary>
        public static int Pick(int[] list, double k) => list[(int)Math.Floor(k * list.Length) % list.Length];

        /// <summary>
        /// A house's floor by room (GameState.homes, playtest 18/10 #15): boards in the living room, the bedrooms, the
        /// study and the hall, each its own shade; pale tiles in the kitchen and the bathroom. Its patch, its colours.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (string patch, int[] tints)> HomeFloors = new Dictionary<string, (string, int[])>
        {
            [HomeUses.Living] = (Patches.Boards, new[] { 0x966c46, 0xac845c, 0x8a5e3e }),
            [HomeUses.Bedroom] = (Patches.Boards, new[] { 0xb08c66, 0x9c7a58, 0xa47c5c }),
            [HomeUses.Study] = (Patches.Boards, new[] { 0x765236, 0x6e4a32 }),
            [HomeUses.Hall] = (Patches.Boards, new[] { 0x88543e, 0x7c5a44 }),
            [HomeUses.Kitchen] = (Patches.Panels, new[] { 0xd8d2c4, 0xc8c2b2, 0xd4ccbc }),
            [HomeUses.Bath] = (Patches.Panels, new[] { 0xc8d6dc, 0xd4dcdc, 0xbccad2 }),
            [HomeUses.Toilet] = (Patches.Panels, new[] { 0xc8d6dc, 0xd4dcdc, 0xbccad2 }),
            [HomeUses.Pantry] = (Patches.Panels, new[] { 0xd8d2c4, 0xc8c2b2, 0xd4ccbc }),
        };

        /// <summary>A zaguán's stone tiles (the panels' patch, greyed).</summary>
        public static readonly Rgb ZaguanFloor = TintTo(0xa49e94, Mean[Patches.Panels]);

        /// <summary>The patch and tint of a room's floor, picked by where it is.</summary>
        public static (Swatch swatch, Rgb tint) HomeFloor(HomeRoom r)
        {
            var (patch, tints) = HomeFloors[r.Use];
            return (SwatchOf(patch), TintTo(Pick(tints, BuildingPlan.Hash(r.X, r.Y, 7 + r.Z)), Mean[patch]));
        }

        /// <summary>A shed's gables and trim in its sheet metal's colour.</summary>
        public static readonly IReadOnlyDictionary<string, int> ShedTint = new Dictionary<string, int>
        {
            [KitIds.ShedGrey] = 0xa4a8aa, [KitIds.ShedRed] = 0x9a4436, [KitIds.ShedBlue] = 0x4a6a8a,
        };

        /// <summary>The colours of building b, as tints of the patches they go on (buildings.ts looksOf).</summary>
        public static Looks Of(Building b)
        {
            var st = Styles[b.kind];
            int outer = ShedTint.TryGetValue(b.kit, out int shed) ? shed : Pick(st.outer, b.look);
            return new Looks
            {
                outer = TintTo(outer, Mean[Patches.Siding]),
                inner = TintTo(Pick(st.inner, BuildingPlan.Hash(b.x0, b.y0, 13)), Mean[Patches.Plaster]),
                trim = Rgb.Hex(outer),
                floor = TintTo(Pick(st.floors, BuildingPlan.Hash(b.x0, b.y0, 9)), Mean[st.floorPatch]),
                floorSwatch = SwatchOf(st.floorPatch),
                shingles = TintTo(Pick(st.roof, BuildingPlan.Hash(b.x0, b.y0, 11)), Mean[Patches.Shingles]),
                tar = Rgb.Hex(Pick(st.flat ?? FlatRoofs, BuildingPlan.Hash(b.x0, b.y0, 11))),
            };
        }
    }
}
