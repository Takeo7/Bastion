#nullable enable
// The library's «Contar» (docs/BIBLIOTECA.md §4, phase 4): how often each of MapArt's objects and sets comes up on the
// generated maps, by place and mission, as Unity's MapBuilder.Decor would draw them; without Unity. Writes
// unity/tools/biblioteca/cuentas.json. Run from the repository: dotnet run --project engine-cs/tools/ContarMapas [seeds]
//
// MapBuilder's rule is copied here (DecorSet, Hash, the pick and which kinds are drawn otherwise): Arte's, as of 0.11.9.
// What is drawn by other rules (cars and blocks over several tiles, fountains, loot boxes, shop shelves, the clinic's
// furniture, door boards, window barricades) is left out, in `sinContar`.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Engine;

static class Program
{
    /// <summary>The places a mission is laid out in, as the player sees them.</summary>
    static readonly (string Biome, string? Site, string Name)[] Places =
    {
        (Biomes.City, null, "Ciudad"),
        (Biomes.City, Sites.Industrial, "Ciudad · Polígono"),
        (Biomes.Wilds, null, "Afueras"),
        (Biomes.Wilds, Sites.Roadside, "Afueras · Área de servicio"),
        (Biomes.Facility, null, "Fábrica"),
    };

    static readonly List<int> Pods = new List<int> { 4, 3, 3 };

    // MapBuilder.Blocks.cs BlockParts and MapBuilder.cs FountainParts: what each stands on as made.
    static readonly Dictionary<string, string> BlockParts = new Dictionary<string, string>
    {
        ["truck"] = "high", ["container"] = "high", ["waterTower"] = "high", ["silo"] = "high", ["logPile"] = "low", ["bench"] = "low",
    };
    static readonly Dictionary<string, string> FountainParts = new Dictionary<string, string> { ["fountain"] = "low", ["fountainCorner"] = "low", ["statue"] = "high" };

    // MedicalFurniture.cs FurnitureKinds.
    static readonly HashSet<string> Medical = new HashSet<string>
    {
        "medicalCabinet", "medicalCart", "wheelchair", "receptionDesk", "waitingSeats", "examTable", "medicalMonitor", "xray", "bedsideTable",
        "visitorChair", "clinicalSink", "sterilizer", "ultrasound", "waitingTable", "waterDispenser", "toilet", "clinicalBin", "consultDesk",
    };
    static bool MedicalHigh(string kind) => kind == "medicalCabinet" || kind == "xray" || kind == "waterDispenser";

    static Dictionary<string, string[]> sets = new Dictionary<string, string[]>();
    /// <summary>The cover each set was drawn on: low (true) or high (false).</summary>
    static readonly Dictionary<string, HashSet<bool>> covers = new Dictionary<string, HashSet<bool>>();

    static void Cover(string set, bool low)
    {
        if (!sets.ContainsKey(set)) return;
        if (!covers.TryGetValue(set, out var on)) covers[set] = on = new HashSet<bool>();
        on.Add(low);
    }

    static int Main(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Run it from the repository.");
        int seeds = args.Length > 0 ? int.Parse(args[0]) : 100;
        var art = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "unity/Bastion/Assets/Bastion/Client/Resources/MapArt.json")))!;
        sets = art["sets"]!.AsObject().ToDictionary((kv) => kv.Key, (kv) => kv.Value!.AsArray().Select((v) => (string)v!).ToArray());
        var propIds = art["props"]!.AsObject().Select((kv) => kv.Key).ToList();

        // Per map type: each object's and set's count on every map (missing = 0).
        var maps = new List<(string Key, string Name, int N, List<double> Bare)>();
        var objects = new Dictionary<string, Dictionary<string, List<int>>>();
        var lists = new Dictionary<string, Dictionary<string, List<int>>>();
        var otherwise = new HashSet<string>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var place in Places)
        {
            foreach (var kind in MissionKinds.All)
            {
                var key = $"{place.Biome}{(place.Site != null ? "-" + place.Site : "")}/{kind}";
                var name = $"{place.Name} · {Setup.MissionNames[kind]}";
                var bare = new List<double>();
                for (int n = 0; n < seeds; n++)
                {
                    var layout = Mapgen.GenerateLayout(new GenOptions
                    {
                        Biome = place.Biome,
                        // Setup.LayoutKind: a Saqueo is laid out like a recovery.
                        Kind = kind == MissionKinds.Scavenge ? MissionKinds.Recovery : kind,
                        PodSizes = Pods,
                        Seed = 1000 + n * 7919,
                        Site = place.Site,
                    });
                    var tally = Count(layout, place.Biome, place.Site, otherwise);
                    bare.Add(tally.Bare);
                    Add(objects, key, n, tally.Objects);
                    Add(lists, key, n, tally.Sets);
                }
                maps.Add((key, name, seeds, bare));
                Console.WriteLine($"{key}: {seeds} mapas, {clock.Elapsed.TotalSeconds:0.0} s");
            }
        }

        var counted = new HashSet<string>(objects.Keys.Concat(lists.Keys));
        // Named by no runtime code and not reached by DecorSet: not drawn at all.
        var code = string.Join("\n", Directory.GetFiles(Path.Combine(root, "unity/Bastion/Assets/Bastion/Client"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        bool Named(string id) => code.Contains($"\"{id}\"");
        var namedSets = sets.Keys.Where((s) => counted.Contains(s) || Named(s)).ToHashSet();
        var unnamedSets = sets.Keys.Where((s) => !namedSets.Contains(s)).OrderBy((s) => s, StringComparer.Ordinal).ToList();
        var unnamedProps = propIds.Where((p) => !counted.Contains(p) && !Named(p) && !namedSets.Any((s) => sets[s].Contains(p))).OrderBy((p) => p, StringComparer.Ordinal).ToList();
        var notCounted = sets.Keys.Concat(propIds).Distinct()
            .Where((id) => !counted.Contains(id) && !unnamedSets.Contains(id) && !unnamedProps.Contains(id))
            .OrderBy((id) => id, StringComparer.Ordinal).ToList();

        var version = File.ReadAllLines(Path.Combine(root, "unity/Bastion/ProjectSettings/ProjectSettings.asset"))
            .First((l) => l.TrimStart().StartsWith("bundleVersion:")).Split(':')[1].Trim();
        var outPath = Path.Combine(root, "unity/tools/biblioteca/cuentas.json");
        using (var stream = File.Create(outPath))
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", 1);
            w.WriteString("hecho", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            w.WriteString("version", version);
            w.WriteNumber("semillas", seeds);
            w.WriteString("_", "Mapas generados con el motor y contados con la regla de MapBuilder.Decor (engine-cs/tools/ContarMapas). «sinDibujo»: casillas de cobertura en el suelo, a ras, sin decorado, que no dibujan nada (deberían ser 0). «sinContar»: lo que Unity pone con otra regla. «coberturas»: en qué cobertura sale cada lista. «sinNombrar»: listas y objetos que no nombra el código del juego ni salen de DecorSet: no se dibujan.");
            w.WriteStartObject("mapas");
            foreach (var m in maps)
            {
                w.WriteStartObject(m.Key);
                w.WriteString("nombre", m.Name);
                w.WriteNumber("n", m.N);
                w.WritePropertyName("sinDibujo");
                Stats(w, m.Bare);
                w.WriteEndObject();
            }
            w.WriteEndObject();
            void Table(string name, Dictionary<string, Dictionary<string, List<int>>> table)
            {
                w.WriteStartObject(name);
                foreach (var id in table.Keys.OrderBy((k) => k, StringComparer.Ordinal))
                {
                    w.WriteStartObject(id);
                    foreach (var m in maps)
                    {
                        if (!table[id].TryGetValue(m.Key, out var counts) || counts.All((c) => c == 0)) continue;
                        w.WritePropertyName(m.Key);
                        Stats(w, counts.Select((c) => (double)c).ToList());
                    }
                    w.WriteEndObject();
                }
                w.WriteEndObject();
            }
            Table("objetos", objects);
            Table("listas", lists);
            // (Which cover each set stands on, for the page's height warning: «baja», «alta» or «las dos».)
            w.WriteStartObject("coberturas");
            foreach (var kv in covers.OrderBy((k) => k.Key, StringComparer.Ordinal))
                w.WriteString(kv.Key, kv.Value.Count == 2 ? "las dos" : kv.Value.Contains(true) ? "baja" : "alta");
            w.WriteEndObject();
            w.WriteStartArray("sinContar");
            foreach (var id in notCounted) w.WriteStringValue(id);
            w.WriteEndArray();
            w.WriteStartObject("sinNombrar");
            w.WriteStartArray("listas");
            foreach (var id in unnamedSets) w.WriteStringValue(id);
            w.WriteEndArray();
            w.WriteStartArray("objetos");
            foreach (var id in unnamedProps) w.WriteStringValue(id);
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndObject();
        }
        Console.WriteLine($"{outPath}: {objects.Count} objetos, {lists.Count} listas, {notCounted.Count} sin contar, sin nombrar {unnamedSets.Count} listas y {unnamedProps.Count} objetos ({string.Join(", ", unnamedSets)})");
        Console.WriteLine($"Otra regla, por tipo de decorado: {string.Join(", ", otherwise.OrderBy((k) => k, StringComparer.Ordinal))}");
        return 0;
    }

    static void Add(Dictionary<string, Dictionary<string, List<int>>> table, string key, int n, Dictionary<string, int> tally)
    {
        foreach (var kv in tally)
        {
            if (!table.TryGetValue(kv.Key, out var byMap)) table[kv.Key] = byMap = new Dictionary<string, List<int>>();
            if (!byMap.TryGetValue(key, out var counts)) byMap[key] = counts = new List<int>();
            while (counts.Count < n) counts.Add(0);
            counts.Add(kv.Value);
        }
        // The maps after this one that have none of it are filled in by Stats' caller: pad here to n + 1.
        foreach (var byMap in table.Values)
            if (byMap.TryGetValue(key, out var counts))
                while (counts.Count < n + 1) counts.Add(0);
    }

    static void Stats(Utf8JsonWriter w, List<double> counts)
    {
        w.WriteStartObject();
        w.WriteNumber("media", Math.Round(counts.Average(), 2));
        w.WriteNumber("min", counts.Min());
        w.WriteNumber("max", counts.Max());
        w.WriteNumber("mapas", Math.Round(counts.Count((c) => c > 0) / (double)counts.Count, 2));
        w.WriteEndObject();
    }

    sealed class Tally
    {
        public readonly Dictionary<string, int> Objects = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Sets = new Dictionary<string, int>();
        public int Bare;
        public void Object(string id, int n = 1) => Objects[id] = (Objects.TryGetValue(id, out var c) ? c : 0) + n;
        public void Set(string id) { if (sets.ContainsKey(id)) Sets[id] = (Sets.TryGetValue(id, out var c) ? c : 0) + 1; }
    }

    /// <summary>One map's objects and sets, storey by storey (MapBuilder.Decor over every tile).</summary>
    static Tally Count(MissionLayout layout, string biome, string? site, HashSet<string> otherwise)
    {
        var t = new Tally();
        int w = layout.Width, h = layout.Height;
        int storeys = 1 + (layout.Levels?.Count ?? 0);
        for (int k = 0; k < storeys; k++)
        {
            IList<string?> tiles = k == 0 ? layout.Tiles.Cast<string?>().ToList() : layout.Levels![k - 1].Tiles;
            var decor = (k == 0 ? layout.Decor : layout.Levels![k - 1].Decor) ?? new List<int>();
            var done = new HashSet<int>();
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile == TileKinds.Console)
                {
                    if (sets.TryGetValue("console", out var desks) && desks.Length > 0) { t.Object(desks[0]); t.Set("console"); }
                    continue;
                }
                if (tile != TileKinds.Low && tile != TileKinds.High) continue;
                var look = i < decor.Count ? Decor.DecorOf(decor[i]) : null;
                // (Upstairs or up on raised ground, cover with no decor is a parapet, which the walls draw.)
                if (look == null) { if (k == 0 && (i >= layout.Elev.Count || layout.Elev[i] == 0)) t.Bare++; continue; }
                if (done.Contains(i)) continue;
                var thing = Decor.ObjectAt(decor, w, i);
                bool oldBlock = thing != null && Decor.BlockKinds.Contains(thing.Kind) && (thing.Tiles.Count < 2 || thing.Tiles[1] >= decor.Count || Decor.DecorOf(decor[thing.Tiles[1]])?.Kind != "part");
                if (thing != null && thing.Tiles.Count > 1 && !oldBlock)
                {
                    foreach (var j in thing.Tiles) done.Add(j);
                    otherwise.Add(thing.Kind);
                    continue;
                }
                if (Joined(BlockParts, look.Kind, i, w, h, tiles, decor, done) || (!(look.Kind == "statue" && site == Sites.Roadside) && Joined(FountainParts, look.Kind, i, w, h, tiles, decor, done)))
                {
                    otherwise.Add(look.Kind);
                    continue;
                }
                if (look.Kind == "part") continue;
                if (look.Kind == DecorKinds.DoorBoards || look.Kind == "windowBarricade" || look.Kind.StartsWith("loot", StringComparison.Ordinal)
                    || (Medical.Contains(look.Kind) && tile == (MedicalHigh(look.Kind) ? TileKinds.High : TileKinds.Low)))
                {
                    otherwise.Add(look.Kind);
                    continue;
                }
                bool low = tile == TileKinds.Low;
                var set = DecorSet(look.Kind, low, biome, site);
                if (set == null)
                {
                    // (DecorSet: drawn otherwise, or not yet in Unity.)
                    otherwise.Add(look.Kind);
                    continue;
                }
                int x = i % w, y = i / w;
                // MapBuilder.Stacked: what is piled up, or lies on its face.
                switch (look.Kind)
                {
                    case "pallets": t.Object("pallet", low ? 3 : 6); continue;
                    case "crates" when !low: t.Object("crates"); t.Object("crate"); continue;
                    case "tvStand" when low: t.Object("tvStand"); t.Object("television"); continue;
                    case "machine" when !low: t.Object("machine"); continue;
                    case "wardrobe" when low:
                    case "cabinet" when low:
                        // (Knocked down: on its face, from its own set.)
                        t.Set(look.Kind);
                        Cover(look.Kind, true);
                        t.Object(Pick(look.Kind, x, y));
                        continue;
                }
                // MapBuilder.Furnished: counters, shelves and cabinets by the building they are in.
                if (look.Kind == "counter" || look.Kind == "shelf" || look.Kind == "cabinet")
                {
                    otherwise.Add(look.Kind);
                    continue;
                }
                t.Set(set);
                Cover(set, low);
                t.Object(Pick(set, x, y));
            }
        }
        return t;
    }

    /// <summary>MapBuilder.Block and Fountain: tile i's kind joined to the same kinds round it, all on the tiles they stand on as made; marked done.</summary>
    static bool Joined(Dictionary<string, string> parts, string kind, int i, int w, int h, IList<string?> tiles, IList<int> decor, HashSet<int> done)
    {
        if (!parts.ContainsKey(kind)) return false;
        var block = new List<int> { i };
        var seen = new HashSet<int> { i };
        for (int n = 0; n < block.Count; n++)
        {
            int tx = block[n] % w, ty = block[n] / w;
            foreach (var d in Grid.Dirs4)
            {
                int nx = tx + d.X, ny = ty + d.Y, j = ny * w + nx;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen.Contains(j) || j >= decor.Count) continue;
                var other = Decor.DecorOf(decor[j]);
                // (A block joins its own kind; a fountain any of its parts.)
                if (other == null || (parts == BlockParts ? other.Kind != kind : !parts.ContainsKey(other.Kind))) continue;
                seen.Add(j);
                block.Add(j);
            }
        }
        bool whole = block.All((b) => b < tiles.Count && tiles[b] == parts[Decor.DecorOf(decor[b])!.Kind]);
        if (!whole) return false;
        foreach (var b in block) done.Add(b);
        return true;
    }

    /// <summary>One of a set's props for tile (x, y), as MapBuilder.Decor picks it (MapArt.Set: the id alone when no set has that name).</summary>
    static string Pick(string set, int x, int y)
    {
        var ids = sets.TryGetValue(set, out var s) ? s : new[] { set };
        return ids[Math.Min(ids.Length - 1, (int)Math.Floor(Hash(x, y, 1) * ids.Length))];
    }

    /// <summary>MapArt.Hash(int, int, int).</summary>
    static float Hash(int x, int y, int salt = 0)
    {
        int a = unchecked((int)((x + salt * 31L) * 73856093L));
        int b = unchecked((int)(y * 19349663L));
        int c = unchecked((int)(salt * 83492791L));
        return (uint)(a ^ b ^ c) % 1000 / 1000f;
    }

    /// <summary>MapArt.DecorSet, as of 0.11.9.</summary>
    static string? DecorSet(string kind, bool low, string biome, string? site)
    {
        switch (kind)
        {
            case "carFront":
            case "carRear":
                return low ? "carFront" : null;
            case "planter": case "bin": case "streetTree": case "bollard": case "bench": case "stove": case "bathtub": case "washbasin": case "wc":
            case "tvStand": case "counter": case "table": case "sofa": case "bed": case "hospitalBed": case "gurney": case "pipe": case "bush":
                return low ? kind : null;
            case "tank":
                return low ? (biome == "city" || site == "roadside" ? "pump" : null) : kind;
            case "pylon":
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
                return null;
        }
    }
}
