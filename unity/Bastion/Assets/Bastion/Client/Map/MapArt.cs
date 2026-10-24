using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>A prop's Synty model and how it is fitted to its tile (0.10.1's syntyManifest.ts PropAsset, syntyProps.ts prepare).</summary>
    public sealed class PropArt
    {
        public string pack;
        public string fbx;
        /// <summary>Its longest side to this many tiles (0: as it is).</summary>
        public float fit;
        /// <summary>With fit, only that side; height and depth times `thick`.</summary>
        public bool stretch;
        public float thick = 1;
        /// <summary>Quarter turns (0.10.1's: counter-clockwise from above) that make it face north.</summary>
        public int yaw;
        /// <summary>Shifted along its own axes (tiles), after the turn.</summary>
        public float[] shift;
        /// <summary>On its own origin, not its middle (a pole with an arm).</summary>
        public bool origin;
        /// <summary>Sunk this far into the ground (tiles).</summary>
        public float sink;
        /// <summary>Stood this tall (tiles), its height alone stretched to it.</summary>
        public float tall;
    }

    /// <summary>
    /// The map's art as 0.10.1 drew it (mapView.ts): which props a tile's decor is drawn with, and the props themselves
    /// (Resources/MapArt.json, unity/tools/map-art-json.mts).
    /// </summary>
    public static class MapArt
    {
        static Dictionary<string, PropArt> props;
        static Dictionary<string, string[]> sets;
        static Dictionary<string, Footprint> footprints;

        /// <summary>What a prop over several tiles is fitted to (FOOTPRINT_FIT): its longest side, tiles; across it, times `thin`.</summary>
        public sealed class Footprint
        {
            public float fit;
            public float thin = 1;
        }

        public static Footprint FootprintOf(string id)
        {
            Load();
            return footprints.TryGetValue(id, out var f) ? f : null;
        }

        /// <summary>
        /// Models that Synty's Unity package has turned against its source FBX (which 0.10.1's art was set against): the
        /// quarter turns to take off 0.10.1's yaw. Apocalypse's wrecked cars lie along z here, along x there (24/10: every
        /// car of city-7 stood across its tiles); so does the caravan (its mesh, SM_Veh_Caravan_01, for the source's rigged one).
        /// </summary>
        static readonly Dictionary<string, int> UnityTurns = new Dictionary<string, int>
        {
            ["SM_Prop_Car_Wrecked_01"] = 1, ["SM_Prop_Car_Wrecked_Rusted_01"] = 1, ["SM_Prop_Car_Wrecked_SUV_01"] = 1, ["SM_Prop_Car_Wrecked_OpenBoot_01"] = 1,
            ["SK_Veh_Caravan_01"] = 1,
        };

        /// <summary>The quarter turns a model's Unity prefab stands from its source FBX (0 for most).</summary>
        public static int UnityTurn(string fbx) => fbx != null && UnityTurns.TryGetValue(System.IO.Path.GetFileName(fbx), out int k) ? k : 0;

        public static PropArt Prop(string id)
        {
            Load();
            return props.TryGetValue(id, out var p) ? p : null;
        }

        /// <summary>Every prop's Synty model by name, for the game's catalog (MapCatalog).</summary>
        public static IEnumerable<string> Models()
        {
            Load();
            foreach (var p in props.Values) if (!string.IsNullOrEmpty(p.fbx)) yield return p.fbx;
        }

        /// <summary>A set's props (PROP_SETS), or the prop of that id alone.</summary>
        public static string[] Set(string set)
        {
            Load();
            return sets.TryGetValue(set, out var ids) ? ids : new[] { set };
        }

        static void Load()
        {
            if (props != null) return;
            var text = Resources.Load<TextAsset>("MapArt") ?? throw new InvalidOperationException("Resources/MapArt.json is missing");
            var art = JsonConvert.DeserializeObject<Art>(text.text);
            props = art.props;
            sets = art.sets;
            footprints = art.footprints;
        }

        sealed class Art
        {
            public Dictionary<string, PropArt> props = new Dictionary<string, PropArt>();
            public Dictionary<string, string[]> sets = new Dictionary<string, string[]>();
            public Dictionary<string, Footprint> footprints = new Dictionary<string, Footprint>();
        }

        /// <summary>Stable pseudo-random value in [0, 1) per tile, as 0.10.1's mapView.ts hash (JavaScript's 32-bit integers).</summary>
        public static float Hash(int x, int y, int salt = 0)
        {
            int a = unchecked((int)((x + salt * 31L) * 73856093L));
            int b = unchecked((int)(y * 19349663L));
            int c = unchecked((int)(salt * 83492791L));
            return (uint)(a ^ b ^ c) % 1000 / 1000f;
        }

        /// <summary>Decor whose prop turns the way the generator said (mapView.ts FACING); the others are turned at random.</summary>
        public static readonly HashSet<string> Facing = new HashSet<string>
        {
            "carFront", "carRear", "fountain", "fountainCorner", "fence", "gatePost", "hay", "shelf", "counter", "table", "sofa", "bed",
            "hospitalBed", "medicalShelf", "gurney", "medicalCart", "medicalCabinet", "wheelchair", "receptionDesk", "waitingSeats",
            "examTable", "medicalMonitor", "xray", "bedsideTable", "visitorChair", "clinicalSink", "sterilizer", "ultrasound",
            "waitingTable", "waterDispenser", "toilet", "clinicalBin", "consultDesk", "desk", "wardrobe", "cabinet", "rack", "pipe",
            "barricade", "log", "sign", "wire", "pallets", "drums", "machine", "bench", "fridge", "stove", "bathtub", "washbasin", "wc",
            "tvStand", "windowBarricade",
        };

        /// <summary>How much a decor's prop is let off the grid (mapView.ts WIGGLE): 1 a little, 2 any way at all.</summary>
        public static readonly Dictionary<string, int> Wiggle = new Dictionary<string, int>
        {
            ["planter"] = 1, ["bin"] = 1, ["streetTree"] = 1, ["kiosk"] = 1, ["statue"] = 1, ["crates"] = 1, ["tank"] = 1, ["pylon"] = 1,
            ["tree"] = 2, ["bush"] = 2, ["rock"] = 2, ["rubble"] = 2,
        };

        /// <summary>
        /// The prop set a tile's decor is drawn with on a low or a high cover tile, by the biome and the site; null, none
        /// (drawn otherwise, or not at all). 0.10.1's mapView.ts decorSet, case by case.
        /// </summary>
        public static string DecorSet(string kind, bool low, string biome, string site)
        {
            switch (kind)
            {
                case "carFront":
                case "carRear":
                    return low ? "carFront" : null;
                case "planter": case "bin": case "streetTree": case "bench": case "stove": case "bathtub": case "washbasin": case "wc":
                case "tvStand": case "counter": case "table": case "sofa": case "bed": case "hospitalBed": case "gurney": case "pipe": case "bush":
                    return low ? kind : null;
                case "tank":
                    // In town and at the forest stop a low one is a filling station's pump.
                    return low ? (biome == "city" || site == "roadside" ? "pump" : null) : kind;
                case "pylon":
                    // In town, a building site's portaloo.
                    return low ? null : biome == "city" ? "portaloo" : kind;
                case "fountain":
                case "fountainCorner":
                    return low ? "rubble" : null;
                case "cabinet":
                    return low ? "officeCabinet" : kind;
                case "kiosk": case "statue": case "fridge": case "wardrobe": case "medicalShelf": case "rack":
                    return low ? null : kind;
                case "fence":
                    return low ? (site == "roadside" ? "woodFence" : "fencePost") : null;
                case "gatePost":
                    return low ? "gatePost" : "gatePillar";
                case "hay":
                    return low ? "hayBale" : "hayStack";
                case "shelf":
                    return low ? "shelfLow" : "shelfHigh";
                case "desk":
                    return low ? (biome == "facility" ? "labBench" : "desk") : null;
                case "crates":
                    return low ? (biome == "facility" ? "techCrate" : "crates") : "crates";
                case "barricade":
                    return low ? (biome == "facility" ? "techBarrier" : biome == "wilds" ? "sandbags" : "barricade") : null;
                case "windowBarricade":
                    return "barricade";
                case "tree":
                    return low ? "logs" : site == "roadside" ? "pines" : biome == "wilds" ? "forest" : "tree";
                case "rock":
                    return low ? "rock" : "spire";
                case "log":
                    return low ? "logs" : null;
                case "rubble":
                    return low ? "rubble" : "wallStub";
                case "breach":
                    return low ? "rubble" : null;
                case "lootWeapons": case "lootMedical": case "lootAmmo": case "lootFood":
                    return kind;
                case "truck": case "waterTower": case "container": case "sirenCar": case "silo": case "shed": case "washingLine": case "vegPatch":
                    return low ? "rubble" : null;
                case "logPile":
                    return low ? "logPile" : null;
                case "sign":
                    return low ? null : "roadSign";
                case "wire":
                    return low ? (biome == "city" ? "razorWire" : "chainFence") : null;
                case "pallets":
                    return "pallets";
                case "drums":
                    return low ? "drums" : null;
                case "machine":
                    return low ? null : "machine";
                default:
                    // The bus, stairs, the clinic's own furniture, what stands on a tile of its own, another tile of an
                    // object over several: drawn otherwise (or not yet in Unity).
                    return null;
            }
        }
    }
}
