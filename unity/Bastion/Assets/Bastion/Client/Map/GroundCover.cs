using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;
using Grid = Bastion.Engine.Grid;

namespace Bastion.Client.Map
{
    /// <summary>
    /// Small Synty props scattered off the grid (0.10.1's ground.ts scatterCover; UI's): grass tufts and flowers on lawns
    /// and in the wilds, papers and pebbles in town, leaves under trees, weeds at the kerb, what a town leaves lying about
    /// (boxes, a tyre, a bike, luggage), a drain now and then in the gutter and manholes down the middle of streets; and a
    /// filling station's island under each pair of pumps; and the streets' dressing (StreetDressing: street lights, poles
    /// and wires, stop signs, hydrants and mailboxes, bags against the façades). Where they fall clumps and thins out across
    /// tiles, with the 0.10.1's own hashes, so each falls where it fell there. Cosmetic, never picked; the cover casts no
    /// shadow, the dressing as it says. Merged by chunks of the map and by material: a few draws in all, and a change
    /// remakes only its chunks.
    /// </summary>
    public sealed class GroundCover
    {
        /// <summary>Tiles a side of a chunk (mapView.ts CHUNK).</summary>
        const int Chunk = 8;

        /// <summary>How much of it there is (0.10.1's COVER_DENSITY by the graphics level: 0.3, 0.6, 1); set by the game's settings.</summary>
        public static float Density = 1;

        /// <summary>A manhole every this many tiles down a street's middle, a drain in each gutter every this many.</summary>
        const int ManholeEvery = 16;
        /// <summary>None this near a crossing or a street's end (tiles along it).</summary>
        const int ManholeClear = 4;
        const int DrainEvery = 13;

        /// <summary>What a town leaves lying about, per tile: on open paving, in the road, in a yard.</summary>
        static readonly (string id, double amount)[] JunkOpen =
        {
            ("trash", 0.006), ("trashBag", 0.004), ("cardboard", 0.003), ("cardboard2", 0.002), ("cardboard4", 0.002), ("tire", 0.002), ("bike", 0.0015),
            ("luggage", 0.001), ("luggageRetro", 0.001), ("luggageOpen", 0.0015), ("holdall", 0.0015), ("trolley", 0.001), ("trolleyBroken", 0.001),
        };
        static readonly (string id, double amount)[] JunkStreet =
        {
            ("trash", 0.005), ("trashBag", 0.004), ("cardboard", 0.002), ("cardboard4", 0.002), ("tire", 0.003), ("bike", 0.0015), ("luggageOpen", 0.001),
            ("holdall", 0.001), ("trolleyBroken", 0.0005),
        };
        static readonly (string id, double amount)[] JunkYard =
        {
            ("trash", 0.005), ("trashBag", 0.004), ("cardboard", 0.003), ("cardboard2", 0.002), ("tire", 0.002), ("bike", 0.002), ("luggage", 0.001),
            ("luggageRetro", 0.001),
        };

        /// <summary>Ground cover per tile, by what the floor is (before the clumping); a lawn is Lawn + 10.</summary>
        static readonly Dictionary<string, Dictionary<int, (string id, double amount)[]>> Cover = new Dictionary<string, Dictionary<int, (string, double)[]>>
        {
            [Biomes.City] = new Dictionary<int, (string, double)[]>
            {
                [GroundMap.Open] = new[] { ("papers", 0.06), ("pebbles", 0.09) }.Concat(JunkOpen).ToArray(),
                [GroundMap.Lawn + 10] = new[] { ("tuftLow", 0.6), ("tuft", 0.25), ("flowers", 0.06) },
                [GroundMap.Street] = new[] { ("papers", 0.045), ("pebbles", 0.07) }.Concat(JunkStreet).ToArray(),
                [GroundMap.Yard] = new[] { ("papers", 0.04), ("pebbles", 0.08) }.Concat(JunkYard).ToArray(),
                [GroundMap.Roof] = new[] { ("pebbles", 0.08), ("papers", 0.02) },
            },
            [Biomes.Wilds] = new Dictionary<int, (string, double)[]>
            {
                [GroundMap.Open] = new[] { ("tuft", 0.55), ("tuftLow", 0.75), ("tuftWide", 0.3), ("flowers", 0.12), ("pebbles", 0.06) },
                [GroundMap.Street] = new[] { ("pebbles", 0.18), ("tuftLow", 0.12) },
                [GroundMap.Yard] = new[] { ("pebbles", 0.08), ("papers", 0.02), ("tuftLow", 0.04) },
                [GroundMap.Hill] = new[] { ("tuft", 0.5), ("tuftLow", 0.5), ("pebbles", 0.08) },
                [GroundMap.Furrow] = new[] { ("pebbles", 0.05) },
            },
            [Biomes.Facility] = new Dictionary<int, (string, double)[]>
            {
                [GroundMap.Open] = new[] { ("papers", 0.03), ("pebbles", 0.07), ("grassBlob", 0.03) },
                [GroundMap.Street] = new[] { ("papers", 0.02), ("pebbles", 0.05) },
                [GroundMap.Yard] = new[] { ("papers", 0.02), ("pebbles", 0.05) },
                [GroundMap.Roof] = new[] { ("pebbles", 0.05) },
            },
        };

        /// <summary>
        /// The ones that stand up made to lie (walked through, as all of the cover: upright they would read as something in
        /// the way): the trolleys on a side, the suitcase on its back; in 0.10.1's frame and tiles.
        /// </summary>
        static readonly Dictionary<string, Matrix4x4> Lying = new Dictionary<string, Matrix4x4>
        {
            ["trolley"] = Matrix4x4.Translate(new Vector3(0, 0.29f, -0.39f)) * Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)),
            ["trolleyBroken"] = Matrix4x4.Translate(new Vector3(0, 0.34f, -0.38f)) * Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)),
            ["luggage"] = Matrix4x4.Translate(new Vector3(0.325f, 0.09f, 0)) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)),
        };

        readonly PropParts parts;
        /// <summary>Where each street light's lamp hangs (Unity's metres) and the way its arm points: for the night's lights (Arte's).</summary>
        public IReadOnlyList<(Vector3 at, Vector3 facing)> Lamps { get; private set; } = new List<(Vector3, Vector3)>();

        readonly Dictionary<int, GameObject> chunks = new Dictionary<int, GameObject>();
        Transform root;
        int width;
        int height;

        /// <summary>The props the cover is made of (MapArt's, fitted; shared with the outline).</summary>
        public GroundCover(PropParts parts)
        {
            this.parts = parts ?? new PropParts();
        }

        /// <summary>All of `s`'s cover under `parent`.</summary>
        public void Build(GameState s, GroundMap ground, Transform parent)
        {
            root = new GameObject("Suelo: menudo").transform;
            root.SetParent(parent, false);
            width = s.Width;
            height = s.Height;
            Make(s, ground, null);
        }

        /// <summary>Only the chunks round `changed` (tile indices) made again: each tile's cover is its own, and stays on it.</summary>
        public void Rebuild(GameState s, GroundMap ground, IReadOnlyCollection<int> changed)
        {
            if (root == null || changed == null || changed.Count == 0) return;
            var redo = new HashSet<int>();
            foreach (int i in changed)
            {
                int x = i % s.Width, y = i / s.Width;
                redo.Add(ChunkOf(x, y));
            }
            Make(s, ground, redo);
        }

        int ChunkOf(int x, int y) => (y / Chunk) * ((width + Chunk - 1) / Chunk) + x / Chunk;

        /// <summary>The cover of the chunks in `only` (all when null), merged by chunk and material.</summary>
        void Make(GameState s, GroundMap ground, HashSet<int> only)
        {
            var placed = new Dictionary<int, List<(string id, Matrix4x4 m, bool cast)>>();
            foreach (var (id, x, y, m, cast) in Scatter(s, ground, only))
            {
                int c = ChunkOf(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
                if (!placed.TryGetValue(c, out var list)) placed[c] = list = new List<(string, Matrix4x4, bool)>();
                list.Add((id, m, cast));
            }
            foreach (int c in only ?? new HashSet<int>(chunks.Keys.Concat(placed.Keys)))
            {
                if (chunks.TryGetValue(c, out var old))
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(old);
                    else UnityEngine.Object.DestroyImmediate(old);
                    chunks.Remove(c);
                }
                if (placed.TryGetValue(c, out var list)) chunks[c] = Merge(c, list);
            }
        }

        /// <summary>One chunk's cover: a mesh per material, its props' parts merged at their places; no shadows.</summary>
        GameObject Merge(int c, List<(string id, Matrix4x4 m, bool cast)> list) => parts.Merge(root, $"Trozo {c}", list);

        // ------------------------------------------------------------ the scatter

        /// <summary>
        /// Where each piece of cover goes (scatterCover, tile by tile, its own randomness), as Unity's matrices: its tile, and
        /// where it stands. With `only`, the tiles of those chunks alone.
        /// </summary>
        IEnumerable<(string id, int x, int y, Matrix4x4 m, bool cast)> Scatter(GameState s, GroundMap ground, HashSet<int> only)
        {
            int w = s.Width, h = s.Height;
            var wildsRoad = s.Biome == Biomes.Wilds && s.Site == Sites.Roadside;
            var plan = Cover.TryGetValue(s.Biome, out var p) ? p : Cover[Biomes.City];
            if (wildsRoad)
            {
                plan = new Dictionary<int, (string, double)[]>(Cover[Biomes.Wilds]);
                plan[GroundMap.Street] = Cover[Biomes.City][GroundMap.Street];
            }
            var decor = s.Decor ?? new List<int>();
            var snowy = s.Snow ?? s.CampaignMission?.Story == 1;
            var siege = s.Mission?.Siege != null;
            string At(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? null : s.Tiles[y * w + x];
            int Elev(int x, int y) => y * w + x < s.Elev.Count ? s.Elev[y * w + x] : 0;
            var data = ground.Data;
            var output = new List<(string, int, int, Matrix4x4, bool)>();
            void Put(string id, int tx, int ty, double x, double y, double z, double turn, double size)
            {
                // 0.10.1's place (tiles, x east, z south, turned about y) in Unity's metres (z north: the turn the other way).
                var m = Matrix4x4.TRS(new Vector3((float)x * MapSpace.Tile, (float)y * MapSpace.Tile, -(float)z * MapSpace.Tile), Quaternion.Euler(0, -(float)(turn * Mathf.Rad2Deg), 0), Vector3.one * (float)size);
                if (Lying.TryGetValue(id, out var lying)) m *= PropParts.InMetres(lying);
                output.Add((id, tx, ty, m, false));
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (s.Tiles[i] != TileKinds.Floor || (only != null && !only.Contains(ChunkOf(x, y)))) continue;
                    int e = Elev(x, y);
                    // A siege's watchtower deck is planks: nothing grows on it.
                    if (e != 0 && siege) continue;
                    var kind = ground.Kinds[i];
                    if (kind == GroundMap.Indoor || kind == GroundMap.Water) continue;
                    var lawn = s.Biome == Biomes.City && kind == GroundMap.Open && data[i * 4 + 2] == GroundMap.Lawn;
                    var list = new List<(string id, double amount)>(plan.TryGetValue(lawn ? GroundMap.Lawn + 10 : kind, out var l) ? l : Array.Empty<(string, double)>());
                    var r = Random(Hash2(x, y, 23) * 4294967296.0);
                    bool Near(Func<string, int, int, bool> test) => Grid.Dirs4.Any(d => test(At(x + d.X, y + d.Y), x + d.X, y + d.Y));
                    // Leaves round trees, weeds where the paving meets the street.
                    bool Tree(string k, int nx, int ny)
                    {
                        if (k != TileKinds.High) return false;
                        var look = Decor.DecorOf(ny * w + nx < decor.Count ? decor[ny * w + nx] : (int?)null)?.Kind;
                        return look == DecorKinds.Tree || look == DecorKinds.StreetTree;
                    }
                    if (Near(Tree)) list.Add(("leaves", 0.5));
                    if (s.Biome == Biomes.City && kind == GroundMap.Open && Near((_, nx, ny) => ground.Kinds[ny * w + nx] == GroundMap.Street)) list.Add(("grassBlob", 0.16));
                    // The middle of a square or a car park (open paving all round), so it never reads as an empty slab: weeds
                    // through the cracks, leaves blown in, more litter. All of it flat.
                    bool Paved(int nx, int ny) => nx >= 0 && ny >= 0 && nx < w && ny < h && s.Tiles[ny * w + nx] == TileKinds.Floor && ground.Kinds[ny * w + nx] == GroundMap.Open && data[(ny * w + nx) * 4 + 2] != GroundMap.Lawn && Elev(nx, ny) == e;
                    if (s.Biome == Biomes.City && kind == GroundMap.Open && !lawn && Grid.Dirs8.All(d => Paved(x + d.X, y + d.Y)))
                    {
                        list.Add(("grassBlob", 0.12));
                        list.Add(("leaves", 0.06));
                        list.Add(("papers", 0.05));
                        list.Add(("pebbles", 0.06));
                    }
                    // Margins on the sides that walls, cover or a higher floor close.
                    var margin = Grid.Dirs4.Select(d =>
                    {
                        var k = At(x + d.X, y + d.Y);
                        return k == null || (k != TileKinds.Floor && k != TileKinds.DoorOpen) || Elev(x + d.X, y + d.Y) > e ? 0.2 : 0;
                    }).ToArray();
                    double clump = 0.25 + 1.5 * Noise2(x / 4.5, y / 4.5, 3);
                    foreach (var (id, amount) in list)
                    {
                        // The winter has buried flowers, leaves and short grass; a few tall tufts still show.
                        if (snowy && (id == "flowers" || id == "leaves" || id == "tuftLow" || id == "grassBlob")) continue;
                        int n = (int)Math.Floor(amount * Density * (snowy && (id == "tuft" || id == "tuftWide") ? 0.15 : 1) * (id == "leaves" ? 1 : clump) + r());
                        while (n-- > 0)
                        {
                            double px = Clamp(x + r(), x + margin[3], x + 1 - margin[1]);
                            double pz = Clamp(y + r(), y + margin[0], y + 1 - margin[2]);
                            Put(id, x, y, px, e * BuildingPlan.Storey, pz, r() * Math.PI * 2, 0.8 + r() * 0.45);
                        }
                    }
                    // Street furniture in the ground, spaced as a street has it: a manhole on each street's centre line every
                    // ManholeEvery tiles, never within ManholeClear of a crossing or its end, a drain in each gutter every
                    // DrainEvery; each street's own offset, so they never line up across the map.
                    if (s.Biome == Biomes.City && kind == GroundMap.Street && e == 0)
                    {
                        int axis = ground.LineAxis[i];
                        // The centre line's place across the tile: on a tile's edge in a street of even width, where it is the
                        // second of two rows' (offset 1), the first row's cover is the street's one.
                        double offset = ground.LineOffset[i];
                        if (axis != 0 && offset < 1)
                        {
                            int along = axis == 1 ? x : y, across = axis == 1 ? y : x;
                            double line = across + offset;
                            bool clear = true;
                            for (int k = -ManholeClear; k <= ManholeClear && clear; k++)
                            {
                                int nx = axis == 1 ? x + k : x, ny = axis == 1 ? y : y + k;
                                clear = nx >= 0 && ny >= 0 && nx < w && ny < h && ground.LineAxis[ny * w + nx] == axis;
                            }
                            if (clear && (along + (int)Math.Floor(Hash2((int)Math.Round(line * 2, MidpointRounding.AwayFromZero), axis, 34) * ManholeEvery)) % ManholeEvery == 0)
                                Put("manhole", x, y, axis == 1 ? x + 0.5 : x + offset, 0.002, axis == 1 ? y + offset : y + 0.5, (int)(Hash2(x, y, 32) * 4), 1);
                        }
                        int kerb = -1;
                        for (int k = 0; k < 4 && kerb < 0; k++)
                        {
                            var d = Grid.Dirs4[k];
                            int ni = (y + d.Y) * w + x + d.X;
                            if (ni >= 0 && ni < ground.Kinds.Length && ground.Kinds[ni] == GroundMap.Open && At(x + d.X, y + d.Y) != null) kerb = k;
                        }
                        if (kerb >= 0)
                        {
                            var d = Grid.Dirs4[kerb];
                            // Along the kerb: x for one to the north or south, y for one to the east or west.
                            int along = d.Y != 0 ? x : y, across = d.Y != 0 ? y * 4 + kerb : x * 4 + kerb;
                            if ((along + (int)Math.Floor(Hash2(across, kerb, 35) * DrainEvery)) % DrainEvery == 0)
                                Put("drain", x, y, x + 0.5 + d.X * 0.36, 0.002, y + 0.5 + d.Y * 0.36, Math.Atan2(d.X, d.Y), 1);
                        }
                    }
                }
            }
            // A filling station's pumps (decor tank, low): an island under each pair (mapView.ts), its first tile (west or north) drawing it.
            var pumps = new HashSet<int>();
            for (int i = 0; i < s.Tiles.Count; i++)
                if (s.Tiles[i] == TileKinds.Low && Decor.DecorOf(i < decor.Count ? decor[i] : (int?)null)?.Kind == DecorKinds.Tank && (i < s.Elev.Count ? s.Elev[i] : 0) == 0) pumps.Add(i);
            foreach (int i in pumps)
            {
                int x = i % w, y = i / w;
                var east = pumps.Contains(i + 1) && x + 1 < w;
                var south = pumps.Contains(i + w);
                if ((!east && !south) || (only != null && !only.Contains(ChunkOf(x, y)))) continue;
                var m = Matrix4x4.TRS(new Vector3((x + (east ? 1 : 0.5f)) * MapSpace.Tile, 0, -(y + (south ? 1 : 0.5f)) * MapSpace.Tile), Quaternion.Euler(0, east ? -90 : 0, 0), new Vector3(0.55f, 0.3f, 0.55f));
                output.Add(("pumpIsland", x, y, m, false));
            }
            // The streets' dressing (street lights, poles and wires, stop signs, hydrants, bags, weeds): worked out over the
            // whole map (its spacing), placed in the chunks asked for; the street lights' lamps noted for the night's lights.
            var lamps = new List<(Vector3 at, Vector3 facing)>();
            foreach (var d in StreetDressing.Dress(s, ground, Density))
            {
                if (d.Id == "streetLight") lamps.Add(StreetDressing.LampOf(d));
                int x = Mathf.FloorToInt(d.Matrix.m03), y = Mathf.FloorToInt(d.Matrix.m23);
                if (only != null && !only.Contains(ChunkOf(Mathf.Clamp(x, 0, w - 1), Mathf.Clamp(y, 0, h - 1)))) continue;
                output.Add((d.Id, x, y, PropParts.InMetres(d.Matrix), d.Cast));
            }
            Lamps = lamps;
            return output;
        }

        static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

        /// <summary>ground.ts random: mulberry32 from a seed, exactly as JavaScript's 32-bit integers have it.</summary>
        static Func<double> Random(double seed)
        {
            uint s = (uint)(long)seed;
            if (s == 0) s = 1;
            return () =>
            {
                unchecked
                {
                    s += 0x6d2b79f5;
                    uint t = (s ^ (s >> 15)) * (1 | s);
                    t = (t + (t ^ (t >> 7)) * (61 | t)) ^ t;
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            };
        }

        /// <summary>ground.ts hash2: a stable value in [0, 1) for a lattice point.</summary>
        static double Hash2(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 2147483647u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>ground.ts noise2: smooth value noise, for where the details clump.</summary>
        static double Noise2(double x, double y, int salt)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double fx = x - ix, fy = y - iy;
            double u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
            double a = Hash2(ix, iy, salt), b = Hash2(ix + 1, iy, salt), c = Hash2(ix, iy + 1, salt), d = Hash2(ix + 1, iy + 1, salt);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }
    }
}
