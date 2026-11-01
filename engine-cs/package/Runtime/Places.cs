#nullable enable
// The kind of place a mission goes to (Arturo, 31/10, docs/PRUEBA-2026-10-31-ARTURO.md #6 and #9; new in C#): its name on
// the mission's card, how many of the dead it holds next to a city, and so how hard it is.
using System;
using System.Collections.Generic;

namespace Bastion.Engine
{
    public static class Places
    {
        public const string City = "city";
        public const string Town = "town";
        public const string Estate = "estate";
        public const string Factory = "factory";
        public const string Roadside = "roadside";
        public const string Country = "country";

        public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
        {
            [City] = "Ciudad",
            [Town] = "Pueblo",
            [Estate] = "Polígono",
            [Factory] = "Fábrica",
            [Roadside] = "Área de servicio",
            [Country] = "Campo",
        };

        /// <summary>How many of the dead a place holds, as a share of what a city holds (Arturo, 31/10): the city as it was.</summary>
        public static readonly IReadOnlyDictionary<string, double> DeadShare = new Dictionary<string, double>
        {
            [City] = 1,
            [Town] = 0.7,
            [Estate] = 0.8,
            [Factory] = 0.8,
            [Roadside] = 0.6,
            [Country] = 0.5,
        };

        /// <summary>
        /// Whether a town is an old one, with crooked streets (townPlan.ts organic), by its map's seed: once a roll inside
        /// the generator, now known before the map is made, for the card to say «Pueblo».
        /// </summary>
        public static bool OldTownSeed(int seed)
        {
            return Math.Abs(seed) / 3 % 2 == 0;
        }

        /// <summary>
        /// What a town is mostly (Arturo, 1/11: «quizás es zona más residencial, o más comercial… que sea random pero con su
        /// tipo de ciudad»), by its map's seed, as OldTownSeed: TownKinds.
        /// </summary>
        public static string TownKindSeed(int seed)
        {
            var k = Math.Abs(seed) / 6 % 3;
            return k == 0 ? TownKinds.Residential : k == 1 ? TownKinds.Commercial : TownKinds.Mixed;
        }

        /// <summary>
        /// Where a mission goes: null on a hand-made map and on the base. A clinic's, a ward's or a hospital's town is laid
        /// out on a grid (Mapgen.Town), as are the first two missions of the story (a clinic, then a hospital).
        /// </summary>
        public static string? Of(MapSource map, int? story = null)
        {
            if (map.Kind != MapSourceKinds.Generated) return null;
            if (map.Biome == Biomes.Facility) return Factory;
            if (map.Biome == Biomes.Wilds) return map.Site == Sites.Roadside ? Roadside : Country;
            if (map.Biome != Biomes.City) return null;
            if (map.Site == Sites.Industrial) return Estate;
            var grid = story == 1 || story == 2 || !string.IsNullOrEmpty(map.Medical);
            return !grid && OldTownSeed(map.Seed ?? 0) ? Town : City;
        }
    }

    /// <summary>What a town is mostly (Places.TownKindSeed): homes, shops and offices, or both as before.</summary>
    public static class TownKinds
    {
        public const string Residential = "residential";
        public const string Commercial = "commercial";
        public const string Mixed = "mixed";

        /// <summary>How much more or less likely each kind makes a lot's program (Mapgen's CityPrograms), next to a mixed town's.</summary>
        public static double Weight(string kind, string program)
        {
            if (kind == Residential)
            {
                return program == "houses" ? 1.8 : program == "flats" ? 1.2 : program == "shops" ? 0.8 : program == "highStreet" ? 0.5 : program == "office" ? 0.4 : program == "warehouse" ? 0.6 : 1;
            }
            if (kind == Commercial)
            {
                return program == "houses" ? 0.5 : program == "shops" ? 1.5 : program == "highStreet" ? 1.8 : program == "office" ? 1.6 : program == "warehouse" ? 0.8 : 1;
            }
            return 1;
        }
    }
}
