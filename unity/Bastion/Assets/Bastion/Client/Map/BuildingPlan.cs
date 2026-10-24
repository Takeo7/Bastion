using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Client.Map
{
    // 0.10.1's buildings.ts, its layout: which tiles make which building (second playtest of the Synty look: «poder
    // entrar en las casas»). The rules see a building as wall tiles round floor tiles; the plan finds each building,
    // what it is (a house, a shop, a warehouse…), the kit its walls are built from and how tall it stands.

    /// <summary>BuildingKind: what a building is.</summary>
    public static class BuildingKinds
    {
        public const string House = "house";
        public const string Shop = "shop";
        public const string Office = "office";
        public const string Hall = "hall";
        public const string Barn = "barn";
        public const string Ruin = "ruin";
        public const string Motel = "motel";
        public const string Medical = "medical";

        public static readonly IReadOnlyList<string> All = new[] { House, Shop, Office, Hall, Barn, Ruin, Motel, Medical };
    }

    /// <summary>
    /// KitId: the wall kit a building is built from: Town's houses (siding outside, plaster inside), Apocalypse's
    /// concrete bunker walls (labs), the brick of its warehouses (warehouses, hangars), the quarantine wall (a facility's
    /// perimeter); sheet metal in three colours (sheds: #25).
    /// </summary>
    public static class KitIds
    {
        public const string House = "house";
        public const string Bunker = "bunker";
        public const string Brick = "brick";
        public const string Fence = "fence";
        public const string ShedGrey = "shedGrey";
        public const string ShedRed = "shedRed";
        public const string ShedBlue = "shedBlue";
        public const string Medical = "medical";
        public const string ShopFront = "shopFront";

        public static readonly IReadOnlyList<string> All = new[] { House, Bunker, Brick, Fence, ShedGrey, ShedRed, ShedBlue, Medical, ShopFront };
    }

    /// <summary>A building of the plan. Heights (`base`, `top`) in tiles, as in 0.10.1 (BuildingPlan.Storey).</summary>
    public sealed class Building
    {
        /// <summary>Index in the plan (the cutaway's texel).</summary>
        public int index;
        /// <summary>Tile bounds, walls included.</summary>
        public int x0, y0, x1, y1;
        public string kind; // BuildingKinds
        public string kit; // KitIds
        /// <summary>A medical building's wear (MedicalSite.wear); null on any other.</summary>
        public double? medicalWear;
        /// <summary>Stable number in [0, 1) for its looks.</summary>
        public double look;
        /// <summary>World height of its floor, in tiles.</summary>
        public float @base;
        /// <summary>World height of the top of its roof, in tiles.</summary>
        public float top;
        /// <summary>
        /// Storeys it stands (Synty's captures: shops and offices of two to four). Without levels only the ground floor is
        /// played: the ones above are a façade, and go with the roof in the cutaway.
        /// </summary>
        public int storeys;
        /// <summary>The side its doors open onto (a motel's rooms), as a DIRS4 index; -1 none in particular.</summary>
        public int front;
        /// <summary>
        /// Its storeys are real (GameState.levels, docs/FASE6-PLAN.md): each is drawn from its own tiles (floor, walls,
        /// furniture, stairs) instead of as a façade, and the floors above the one in view fade in the cutaway.
        /// </summary>
        public bool leveled;
        /// <summary>Its top storey is a roof one can walk on (a flat roof with stairs or a ladder up): drawn as a deck, open where the stairs come up.</summary>
        public bool walkableRoof;
        /// <summary>A siege's Sala de radio, the heart's building: its mast on the roof (roofProps). False: absent in 0.10.1.</summary>
        public bool radio;
        /// <summary>A block of the outline (docs/MAPAS.md §5.10, GameState.frame): shut, nobody goes in; its inside is plan.Shut. False: absent in 0.10.1.</summary>
        public bool frame;
        /// <summary>A supermarket (GameState.markets, playtest 20/10 #18): the side (DIRS4) its glass front and sign look onto; null on any other.</summary>
        public int? market;
    }

    /// <summary>Which tiles make which building: walls round a floor, at ground level.</summary>
    public sealed class BuildingPlan
    {
        /// <summary>
        /// A storey's height in tiles (0.10.1's coords.ts STOREY). The plan's world heights (Building.base, top) are in
        /// tiles, as in 0.10.1, not in MapSpace's metres: times MapSpace.Tile for those (MapSpace.Storey is this one in metres).
        /// </summary>
        public const float Storey = 2.3f;

        /// <summary>How a raised tile is drawn: a block of flats, a loading platform, a hill.</summary>
        public const byte RaisedFlats = 1;
        public const byte RaisedPlatform = 2;
        public const byte RaisedHill = 3;
        /// <summary>A siege's watchtower (docs/FASE12.md §10): a deck on legs, the guard tower's own (mapView); the ground shows under it.</summary>
        public const byte RaisedTower = 4;

        static readonly HashSet<string> Wallish = new HashSet<string> { TileKinds.Wall, TileKinds.Door, TileKinds.DoorOpen, TileKinds.Window, TileKinds.WindowBroken };
        /// <summary>BuildingPlan.before: what a tile was when the map was built.</summary>
        const int WasWall = 1;
        const int WasSlab = 2;

        public static bool IsWallLike(string k) => k != null && Wallish.Contains(k);

        /// <summary>The ground's tile (x, y), null off the map (buildings.ts `at`).</summary>
        internal static string TileAt(GameState s, int x, int y) => x < 0 || y < 0 || x >= s.Width || y >= s.Height ? null : s.Tiles[y * s.Width + x];

        /// <summary>The sheds' kits, picked per building (twice as often grey).</summary>
        static readonly string[] Sheds = { KitIds.ShedGrey, KitIds.ShedGrey, KitIds.ShedRed, KitIds.ShedBlue };

        internal static bool IsShed(string kit) => kit.StartsWith("shed", StringComparison.Ordinal);

        /// <summary>Stable pseudo-random value in [0, 1), as buildings.ts hash (JavaScript's 32-bit integers: Math.imul, >>>).</summary>
        internal static double Hash(int x, int y, int salt = 0)
        {
            unchecked
            {
                int h = (x + 1) * 374761393 + (y + 7) * 668265263 + (salt + 3) * 2147483647;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>Wall pieces stand on tile middles, half a tile each way: these are the tiles a building of bounds b spans.</summary>
        readonly struct Span
        {
            public readonly int x0, y0, x1, y1;

            public Span(int x0, int y0, int x1, int y1)
            {
                this.x0 = x0;
                this.y0 = y0;
                this.x1 = x1;
                this.y1 = y1;
            }
        }

        /// <summary>
        /// The state as it is now (Follow): Breached, Holed and KindAt read it, against the map as it was built (0.10.1's
        /// plan saw the state the engine changes in place; here each change is a new one).
        /// </summary>
        public GameState S { get; private set; }

        /// <summary>The plan made for the map, on the state as it is now (the same map: a door opened, a wall blown down).</summary>
        public void Follow(GameState now) => S = now;

        public readonly List<Building> Buildings = new List<Building>();
        /// <summary>The building each tile belongs to (its walls and the floor inside), -1 none.</summary>
        public readonly short[] Owner;
        /// <summary>Floor inside a building (not its walls).</summary>
        public readonly byte[] Inside;
        /// <summary>How each raised tile is drawn (RaisedFlats, RaisedPlatform, RaisedHill), 0 on the ground.</summary>
        public readonly byte[] Raised;
        /// <summary>Floor tiles the buildings draw their own floor on (not ruins: the ground shows there).</summary>
        public readonly byte[] Floor;
        /// <summary>Wall tiles in a solid mass two tiles thick or more (a reactor's core): drawn as a block, not as thin walls.</summary>
        public readonly byte[] Massive;
        /// <summary>Out of play (ground 4, docs/MAPAS.md §5.10): the outline's, which frameArt.ts draws; no building's, no loose wall.</summary>
        public readonly byte[] Out;
        /// <summary>The inside of the outline's blocks (Building.frame): the walls take it for a floor, so only its outline is laid.</summary>
        public readonly byte[] Shut;
        /// <summary>The kit of walls that belong to no building (a perimeter, a lone stretch).</summary>
        public readonly string LooseKit;
        /// <summary>A base to defend (docs/FASE12.md §10): its wall is Quarantine's, its gates sheet metal, its corners watchtowers.</summary>
        public readonly bool Siege;
        /// <summary>
        /// What each tile was when the map was built, per storey (0 the ground): WasWall for a wall, a door or a window,
        /// WasSlab for a storey's floor. The rules only ever take these away (docs/FASE7.md §4, §5): a wall that is low
        /// cover now was blown down, a slab that is gone was blown through.
        /// </summary>
        readonly List<byte[]> before = new List<byte[]>();

        public BuildingPlan(GameState s)
        {
            S = s;
            int w = s.Width, h = s.Height;
            int n = w * h;
            Owner = new short[n];
            Array.Fill(Owner, (short)-1);
            Inside = new byte[n];
            Raised = new byte[n];
            Floor = new byte[n];
            Massive = new byte[n];
            Out = new byte[n];
            Shut = new byte[n];
            if (s.Ground != null) for (int i = 0; i < n; i++) if (i < s.Ground.Count && s.Ground[i] == 4) Out[i] = 1;
            Siege = s.Mission.Siege != null;
            LooseKit = s.Biome == Biomes.Facility || Siege ? KitIds.Fence : KitIds.Brick;
            FindRaised();
            for (int y = 0; y + 1 < h; y++)
            {
                for (int x = 0; x + 1 < w; x++)
                {
                    var square = new[] { y * w + x, y * w + x + 1, (y + 1) * w + x, (y + 1) * w + x + 1 };
                    if (square.All(WallIn)) foreach (int i in square) Massive[i] = 1;
                }
            }
            var area = new byte[n];
            if (s.Ground != null)
            {
                // Procedural maps say what is indoors (ruins are open ground again).
                for (int i = 0; i < n; i++) if (i < s.Ground.Count && s.Ground[i] == 2 && ElevAt(i) == 0) area[i] = 1;
            }
            else
            {
                // Hand-made maps: what can't be reached from the edge without going through a wall is inside.
                var outside = new byte[n];
                var queue = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    int x = i % w;
                    int y = (i - x) / w;
                    if ((x == 0 || y == 0 || x == w - 1 || y == h - 1) && !WallIn(i))
                    {
                        outside[i] = 1;
                        queue.Add(i);
                    }
                }
                for (int k = 0; k < queue.Count; k++)
                {
                    int i = queue[k];
                    int x = i % w;
                    int y = (i - x) / w;
                    foreach (var d in Bastion.Engine.Grid.Dirs4)
                    {
                        int nx = x + d.X;
                        int ny = y + d.Y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (outside[j] != 0 || WallIn(j)) continue;
                        outside[j] = 1;
                        queue.Add(j);
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    if (outside[i] != 0 || WallIn(i) || ElevAt(i) != 0) continue;
                    int x = i % w;
                    int y = (i - x) / w;
                    area[i] = 1;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < w && ny < h && WallIn(ny * w + nx) && Massive[ny * w + nx] == 0) area[ny * w + nx] = 1;
                        }
                    }
                }
            }
            // Each building is a connected piece of that area.
            for (int start = 0; start < n; start++)
            {
                if (area[start] == 0 || Owner[start] != -1) continue;
                int index = Buildings.Count;
                var tiles = new List<int> { start };
                Owner[start] = unchecked((short)index);
                int x0 = w, y0 = h, x1 = -1, y1 = -1;
                int walls = 0;
                int gaps = 0;
                var kinds = new HashSet<string>();
                for (int k = 0; k < tiles.Count; k++)
                {
                    int i = tiles[k];
                    int x = i % w;
                    int y = (i - x) / w;
                    x0 = Math.Min(x0, x);
                    y0 = Math.Min(y0, y);
                    x1 = Math.Max(x1, x);
                    y1 = Math.Max(y1, y);
                    if (WallIn(i)) walls++;
                    var look = Bastion.Engine.Decor.DecorOf(CodeAt(s.Decor, i));
                    if (look != null) kinds.Add(look.Kind);
                    foreach (var d in Bastion.Engine.Grid.Dirs4)
                    {
                        int nx = x + d.X;
                        int ny = y + d.Y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (area[j] == 0 || Owner[j] != -1) continue;
                        Owner[j] = unchecked((short)index);
                        tiles.Add(j);
                    }
                }
                foreach (int i in tiles)
                {
                    int x = i % w;
                    int y = (i - x) / w;
                    if (!WallIn(i)) Inside[i] = 1;
                    // A gap in the outline: a loading bay, a doorway with no door.
                    if (!WallIn(i) && (x == x0 || x == x1 || y == y0 || y == y1)) gaps++;
                }
                if (walls == 0 || tiles.Count < 9)
                {
                    foreach (int i in tiles)
                    {
                        Owner[i] = -1;
                        Inside[i] = 0;
                    }
                    continue;
                }
                float @base = ElevAt(start) * Storey;
                // A row of shops wall to wall (a high street): one building each, split at the walls they share.
                var parts = PartyWalls(new Span(x0, y0, x1, y1));
                for (int k = 0; k < parts.Count; k++)
                {
                    var r = parts[k];
                    int own = k == 0 ? index : Buildings.Count;
                    if (k > 0)
                    {
                        // The shared wall stays with the part before; the rest of this part is its own.
                        for (int y = r.y0; y <= r.y1; y++) for (int x = r.x0; x <= r.x1; x++) if ((k == 0 || x > parts[k - 1].x1 || y > parts[k - 1].y1) && Owner[y * w + x] == index) Owner[y * w + x] = unchecked((short)own);
                    }
                    var decor = new Dictionary<string, int>();
                    var doorsBySide = new[] { 0, 0, 0, 0 };
                    for (int y = r.y0; y <= r.y1; y++)
                    {
                        for (int x = r.x0; x <= r.x1; x++)
                        {
                            int i = y * w + x;
                            var item = Bastion.Engine.Decor.DecorOf(CodeAt(s.Decor, i));
                            if (item != null) decor[item.Kind] = (decor.TryGetValue(item.Kind, out int c) ? c : 0) + 1;
                            string t = s.Tiles[i];
                            if (t == TileKinds.Door || t == TileKinds.DoorOpen)
                            {
                                if (y == r.y0) doorsBySide[0]++;
                                if (x == r.x1) doorsBySide[1]++;
                                if (y == r.y1) doorsBySide[2]++;
                                if (x == r.x0) doorsBySide[3]++;
                            }
                        }
                    }
                    int span = Math.Min(r.x1 - r.x0 + 1, r.y1 - r.y0 + 1);
                    int most = Array.IndexOf(doorsBySide, doorsBySide.Max());
                    int Count(string decorKind) => decor.TryGetValue(decorKind, out int c) ? c : 0;
                    string kind = BuildingKinds.House;
                    var medical = s.Medical?.FirstOrDefault((b) => r.x0 >= b.X && r.y0 >= b.Y && r.x1 < b.X + b.W && r.y1 < b.Y + b.H);
                    // Older saved clinics have no bounds yet. Their existing clinical
                    // furniture identifies them without repainting ordinary houses.
                    double? medicalWear = medical?.Wear ?? (Count(DecorKinds.HospitalBed) != 0 || Count(DecorKinds.MedicalShelf) != 0 ? (s.CampaignMission?.Story == 1 ? 0.08 : 0.65) : (double?)null);
                    // A house says it is one (GameState.homes, playtest 18/10 #15): its kitchen's counters don't make it a shop.
                    bool home = s.Homes != null && s.Homes.Any((b) => r.x0 >= b.X && r.y0 >= b.Y && r.x1 < b.X + b.W && r.y1 < b.Y + b.H);
                    // And a supermarket (GameState.markets, playtest 20/10 #18): a shop of brick, its front all glass.
                    var market = s.Markets?.FirstOrDefault((b) => r.x0 >= b.X && r.y0 >= b.Y && r.x1 < b.X + b.W && r.y1 < b.Y + b.H);
                    if (medicalWear != null) kind = BuildingKinds.Medical;
                    else if (market != null) kind = BuildingKinds.Shop;
                    else if (Count(DecorKinds.Bed) != 0 && doorsBySide[most] >= 3) kind = BuildingKinds.Motel;
                    else if (home) kind = BuildingKinds.House;
                    // Hay says barn, however small (playtest 18/10, #53: one came out an office block of tiles).
                    else if (Count(DecorKinds.Hay) >= 2) kind = BuildingKinds.Barn;
                    else if (Count(DecorKinds.Counter) != 0) kind = BuildingKinds.Shop;
                    // A warehouse is rows of shelves, whatever desk its office corner has; a small one full of them too (a sawmill).
                    else if (
                        (span >= 11 && ((Count(DecorKinds.Shelf) != 0 && Count(DecorKinds.Shelf) >= 2 * (Count(DecorKinds.Desk) + Count(DecorKinds.Rack))) || Count(DecorKinds.Hay) != 0 || gaps >= 2)) ||
                        (span >= 9 && Count(DecorKinds.Shelf) >= 6 && Count(DecorKinds.Shelf) >= 2 * (Count(DecorKinds.Desk) + Count(DecorKinds.Rack)))
                    )
                        kind = s.Biome == Biomes.Wilds ? BuildingKinds.Barn : BuildingKinds.Hall;
                    else if (Count(DecorKinds.Desk) != 0 || Count(DecorKinds.Rack) != 0) kind = BuildingKinds.Office;
                    else if (span >= 10) kind = BuildingKinds.Office;
                    double look = Hash(r.x0, r.y0, 5);
                    // Sheet metal (#25): the sawmill by the forest road, three warehouses in four on an industrial estate.
                    bool shed = (kind == BuildingKinds.Barn && s.Site == Sites.Roadside) || (kind == BuildingKinds.Hall && s.Site == Sites.Industrial && look < 0.75);
                    string kit = kind == BuildingKinds.Medical ? KitIds.House : shed ? Sheds[(int)Math.Floor(Hash(r.x0, r.y0, 21) * Sheds.Length)] : kind == BuildingKinds.Hall || market != null ? KitIds.Brick : s.Biome == Biomes.Facility ? KitIds.Bunker : KitIds.House;
                    // A siege's base (docs/FASE12.md §10): the old military station's own, a storey high and flat-roofed: the
                    // Sala de radio (the one in the middle of the map) of concrete under its mast, the barracks of block, the
                    // infirmary of concrete, the workshop and the store of sheet metal.
                    bool radio = false;
                    if (Siege)
                    {
                        int mx = w / 2, my = h / 2;
                        radio = mx > r.x0 && mx < r.x1 && my > r.y0 && my < r.y1;
                        bool west = r.x0 + r.x1 < w;
                        bool north = r.y0 + r.y1 < h;
                        (kind, kit) = radio ? (BuildingKinds.Office, KitIds.Bunker) : west ? (BuildingKinds.Office, north ? KitIds.Brick : KitIds.Bunker) : (BuildingKinds.Hall, north ? KitIds.ShedGrey : KitIds.ShedRed);
                    }
                    // As tall as the engine says (it stops sight up to its roof: GameState.storeys), read on a tile of its floor
                    // (a wall two share takes the taller one's); older saves and hand-made maps: town's shops and offices two or three, some houses two.
                    int FloorTile()
                    {
                        for (int y = r.y0 + 1; y < r.y1; y++) for (int x = r.x0 + 1; x < r.x1; x++) if (!WallIn(y * w + x)) return y * w + x;
                        return r.y0 * w + r.x0;
                    }
                    int floorTile = FloorTile();
                    int? told = s.Storeys != null && floorTile < s.Storeys.Count ? s.Storeys[floorTile] : (int?)null;
                    int tall = Siege || s.Biome != Biomes.City || @base > 0 ? 0 : kind == BuildingKinds.Shop ? (look < 0.55 ? 1 : 2) : kind == BuildingKinds.Office ? (look < 0.6 ? 1 : 2) : kind == BuildingKinds.House ? (look < 0.3 ? 1 : 0) : 0;
                    int storeys = told != null && told > 0 ? told.Value : 1 + tall;
                    bool leveled = SlabsOver(r) > 0;
                    bool walkableRoof = leveled && WalkableAt(r, storeys);
                    Buildings.Add(new Building
                    {
                        index = own, x0 = r.x0, y0 = r.y0, x1 = r.x1, y1 = r.y1, kind = kind, kit = kit, medicalWear = medicalWear, look = look, @base = @base,
                        top = @base + Storey * storeys, storeys = storeys, front = kind == BuildingKinds.Motel ? most : -1, leveled = leveled, walkableRoof = walkableRoof,
                        radio = radio, market = market?.Front,
                    });
                }
                foreach (int i in tiles) if (Inside[i] != 0) Floor[i] = 1;
            }
            FrameBlocks();
            if (s.Biome == Biomes.Wilds) FindRuins();
            for (int k = 0; k <= (s.Levels?.Count ?? 0); k++)
            {
                IList<string> tiles = k != 0 ? s.Levels[k - 1].Tiles : s.Tiles;
                var was = new byte[tiles.Count];
                for (int i = 0; i < was.Length; i++) was[i] = (byte)((IsWallLike(tiles[i]) ? WasWall : 0) | (k != 0 && tiles[i] != null ? WasSlab : 0));
                before.Add(was);
            }
        }

        /// <summary>`elev[i] ?? 0`.</summary>
        int ElevAt(int i) => S.Elev != null && i >= 0 && i < S.Elev.Count ? S.Elev[i] : 0;

        /// <summary>`decor?.[i]`: null past its end, or without decor.</summary>
        static int? CodeAt(List<int> decor, int i) => decor != null && i >= 0 && i < decor.Count ? decor[i] : (int?)null;

        /// <summary>`tiles?.[i]`: null past its end, or without tiles.</summary>
        static string TileIn(IList<string> tiles, int i) => tiles != null && i >= 0 && i < tiles.Count ? tiles[i] : null;

        /// <summary>`before[k]?.[i] ?? 0`.</summary>
        int BeforeAt(int k, int i) => k >= 0 && k < before.Count && i >= 0 && i < before[k].Length ? before[k][i] : 0;

        /// <summary>`levels?.[k - 1]`: storey k (≥ 1), null where there is none.</summary>
        Level LevelAt(int k) => S.Levels != null && k >= 1 && k <= S.Levels.Count ? S.Levels[k - 1] : null;

        /// <summary>
        /// The outline's blocks shut up (docs/MAPAS.md §5.10): each 'block' of GameState.frame a building of its storeys,
        /// its outline all wall (no door; windows on the storeys above, as any façade's), flat-roofed, with nobody in it.
        /// A strip too thin to have an inside is frameArt.ts's, walled off like a lot.
        /// </summary>
        void FrameBlocks()
        {
            var s = S;
            int w = s.Width;
            if (s.Frame == null) return;
            foreach (var p in s.Frame)
            {
                if (p.Kind != FrameKinds.Block || p.W < 3 || p.H < 3) continue;
                int index = Buildings.Count;
                var r = new Span(p.X, p.Y, p.X + p.W - 1, p.Y + p.H - 1);
                for (int y = r.y0; y <= r.y1; y++)
                {
                    for (int x = r.x0; x <= r.x1; x++)
                    {
                        int i = y * w + x;
                        Owner[i] = unchecked((short)index);
                        if (x == r.x0 || y == r.y0 || x == r.x1 || y == r.y1) continue;
                        Inside[i] = 1;
                        Shut[i] = 1;
                    }
                }
                double look = Hash(r.x0, r.y0, 5);
                string kind = look < 0.45 ? BuildingKinds.Office : BuildingKinds.Shop;
                string kit = s.Biome == Biomes.Facility ? KitIds.Bunker : Hash(r.x0, r.y0, 22) < 0.35 ? KitIds.Brick : KitIds.House;
                int storeys = Math.Max(1, p.Storeys ?? 2);
                Buildings.Add(new Building
                {
                    index = index, x0 = r.x0, y0 = r.y0, x1 = r.x1, y1 = r.y1, kind = kind, kit = kit, look = look, @base = 0, top = Storey * storeys,
                    storeys = storeys, front = -1, leveled = false, walkableRoof = false, frame = true,
                });
            }
        }

        /// <summary>Storey k's tiles (0 the ground) as they are now.</summary>
        IList<string> TilesAt(int k) => k != 0 ? LevelAt(k)?.Tiles : S.Tiles;

        /// <summary>
        /// Tile i of storey k was a wall and is low cover now: blown down to rubble (docs/FASE7.md §4). The engine marks it
        /// ('breach'), so it is known after a reload too; before that, against the map as it was built.
        /// </summary>
        public bool Breached(int i, int k)
        {
            if (TileIn(TilesAt(k), i) != TileKinds.Low) return false;
            if ((BeforeAt(k, i) & WasWall) != 0) return true;
            var decor = k != 0 ? LevelAt(k)?.Decor : S.Decor;
            return Bastion.Engine.Decor.DecorOf(CodeAt(decor, i))?.Kind == DecorKinds.Breach;
        }

        /// <summary>The ground's tile i is part of a building's walls: a wall, a door, a window, or one blown down (breached).</summary>
        bool WallIn(int i)
        {
            if (Out[i] != 0) return false;
            return IsWallLike(S.Tiles[i]) || (S.Tiles[i] == TileKinds.Low && Bastion.Engine.Decor.DecorOf(CodeAt(S.Decor, i))?.Kind == DecorKinds.Breach);
        }

        /// <summary>Storey k's (≥ 1) slab at tile i was there and is gone: blown through (docs/FASE7.md §5).</summary>
        public bool Holed(int i, int k)
        {
            if (k <= 0 || (BeforeAt(k, i) & WasSlab) == 0) return false;
            // `tilesAt(k)?.[i] === null`: a tile there, and null; past the end (undefined) is not.
            var tiles = TilesAt(k);
            return tiles != null && i >= 0 && i < tiles.Count && tiles[i] == null;
        }

        /// <summary>What stands on storey k (≥ 1) of tile (x, y): null where there's no slab (open air, a stairwell), or off the map.</summary>
        public string KindAt(int x, int y, int k)
        {
            var s = S;
            if (k < 1 || x < 0 || y < 0 || x >= s.Width || y >= s.Height) return null;
            return TileIn(LevelAt(k)?.Tiles, y * s.Width + x);
        }

        /// <summary>How many slabs (of any storey) stand over the span.</summary>
        int SlabsOver(Span r)
        {
            int n = 0;
            for (int k = 1; k <= (S.Levels?.Count ?? 0); k++) for (int y = r.y0; y <= r.y1; y++) for (int x = r.x0; x <= r.x1; x++) if (KindAt(x, y, k) != null) n++;
            return n;
        }

        /// <summary>Whether storey k has somewhere to stand on within the span.</summary>
        bool WalkableAt(Span r, int k)
        {
            for (int y = r.y0; y <= r.y1; y++) for (int x = r.x0; x <= r.x1; x++) if (Bastion.Engine.Grid.IsWalkable(KindAt(x, y, k))) return true;
            return false;
        }

        /// <summary>
        /// Raised areas: in town, a block of flats wherever one reaches two storeys; other raised areas in town and
        /// facilities are loading platforms; in the wilds, hills.
        /// </summary>
        void FindRaised()
        {
            var s = S;
            int w = s.Width, h = s.Height;
            var seen = new byte[w * h];
            for (int start = 0; start < w * h; start++)
            {
                if (seen[start] != 0 || ElevAt(start) == 0) continue;
                var tiles = new List<int> { start };
                seen[start] = 1;
                bool tall = false;
                for (int k = 0; k < tiles.Count; k++)
                {
                    int i = tiles[k];
                    if (ElevAt(i) >= 2) tall = true;
                    int x = i % w;
                    int y = (i - x) / w;
                    foreach (var d in Bastion.Engine.Grid.Dirs4)
                    {
                        int nx = x + d.X;
                        int ny = y + d.Y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (seen[j] != 0 || ElevAt(j) == 0) continue;
                        seen[j] = 1;
                        tiles.Add(j);
                    }
                }
                byte style = s.Mission.Siege != null ? RaisedTower : s.Biome == Biomes.Wilds ? RaisedHill : tall && s.Biome == Biomes.City ? RaisedFlats : RaisedPlatform;
                foreach (int i in tiles) Raised[i] = style;
            }
        }

        /// <summary>
        /// Ruins in the wilds: the walls left of a farmhouse (their floor is open ground again). Walls that belong to no
        /// building, a few tiles apart at most, round a space at least 5 tiles a side, make a roofless building: the walls
        /// get their inside and outside faces, and its floor is the ground.
        /// </summary>
        void FindRuins()
        {
            var s = S;
            int w = s.Width, h = s.Height;
            bool Loose(int i) => WallIn(i) && Owner[i] == -1 && Massive[i] == 0;
            var seen = new byte[w * h];
            for (int start = 0; start < w * h; start++)
            {
                if (seen[start] != 0 || !Loose(start)) continue;
                var tiles = new List<int> { start };
                seen[start] = 1;
                int x0 = w, y0 = h, x1 = -1, y1 = -1;
                for (int k = 0; k < tiles.Count; k++)
                {
                    int i = tiles[k];
                    int x = i % w;
                    int y = (i - x) / w;
                    x0 = Math.Min(x0, x);
                    y0 = Math.Min(y0, y);
                    x1 = Math.Max(x1, x);
                    y1 = Math.Max(y1, y);
                    // Gaps where the walls were knocked down are up to three tiles long.
                    for (int dy = -4; dy <= 4; dy++)
                    {
                        for (int dx = -4; dx <= 4; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || (dx != 0 && dy != 0)) continue;
                            int j = ny * w + nx;
                            if (seen[j] != 0 || !Loose(j)) continue;
                            seen[j] = 1;
                            tiles.Add(j);
                        }
                    }
                }
                if (x1 - x0 < 4 || y1 - y0 < 4) continue;
                int index = Buildings.Count;
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = y * w + x;
                        if (Owner[i] != -1) continue;
                        bool edge = x == x0 || y == y0 || x == x1 || y == y1;
                        if (WallIn(i)) Owner[i] = unchecked((short)index);
                        else if (!edge)
                        {
                            Owner[i] = unchecked((short)index);
                            Inside[i] = 1;
                        }
                    }
                }
                Buildings.Add(new Building
                {
                    index = index, x0 = x0, y0 = y0, x1 = x1, y1 = y1, kind = BuildingKinds.Ruin, kit = KitIds.House, look = Hash(x0, y0, 5), @base = 0, top = Storey,
                    storeys = 1, front = -1, leveled = false, walkableRoof = false,
                });
            }
        }

        /// <summary>
        /// Splits a building's bounds at the walls it shares with a neighbour: a straight line of plain wall right across
        /// it, with shops on both sides (shelves, counters), or seven tiles or more of building either side (a town's
        /// houses wall to wall, docs/MAPAS.md §5.3). A motel's rooms are walled off from each other too, but they're one
        /// building: narrower.
        /// </summary>
        List<Span> PartyWalls(Span r)
        {
            var s = S;
            int w = s.Width;
            bool ShopAt(int x0, int y0, int x1, int y1)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        string k = Bastion.Engine.Decor.DecorOf(CodeAt(s.Decor, y * w + x))?.Kind;
                        if (k == DecorKinds.Counter || k == DecorKinds.Shelf) return true;
                    }
                }
                return false;
            }
            for (int x = r.x0 + 4; x <= r.x1 - 4; x++)
            {
                bool solid = true;
                for (int y = r.y0; y <= r.y1 && solid; y++) solid = s.Tiles[y * w + x] == TileKinds.Wall;
                bool wide = x - r.x0 >= 6 && r.x1 - x >= 6;
                if (solid && (wide || (ShopAt(r.x0, r.y0, x - 1, r.y1) && ShopAt(x + 1, r.y0, r.x1, r.y1))))
                {
                    var split = new List<Span> { new Span(r.x0, r.y0, x, r.y1) };
                    split.AddRange(PartyWalls(new Span(x, r.y0, r.x1, r.y1)));
                    return split;
                }
            }
            for (int y = r.y0 + 4; y <= r.y1 - 4; y++)
            {
                bool solid = true;
                for (int x = r.x0; x <= r.x1 && solid; x++) solid = s.Tiles[y * w + x] == TileKinds.Wall;
                bool wide = y - r.y0 >= 6 && r.y1 - y >= 6;
                if (solid && (wide || (ShopAt(r.x0, r.y0, r.x1, y - 1) && ShopAt(r.x0, y + 1, r.x1, r.y1))))
                {
                    var split = new List<Span> { new Span(r.x0, r.y0, r.x1, y) };
                    split.AddRange(PartyWalls(new Span(r.x0, y, r.x1, r.y1)));
                    return split;
                }
            }
            return new List<Span> { r };
        }

        /// <summary>Floor of building `b` at tile (x, y).</summary>
        public bool Interior(int b, int x, int y)
        {
            int w = S.Width, h = S.Height;
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            int i = y * w + x;
            return Owner[i] == b && Inside[i] == 1;
        }

        /// <summary>The building whose floor tile `p` is, or -1.</summary>
        public int At(Vec2 p)
        {
            int w = S.Width, h = S.Height;
            if (p.X < 0 || p.Y < 0 || p.X >= w || p.Y >= h) return -1;
            int i = p.Y * w + p.X;
            return Inside[i] != 0 ? Owner[i] : -1;
        }
    }
}
