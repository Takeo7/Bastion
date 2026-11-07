using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// Where the buildings' walls go (0.10.1's buildings.ts buildWalls), from the state and its BuildingPlan: every kit
    /// piece, window pane and thing hung on a wall (a shed's roller shutter, a shop's sign, a motel's) with its matrix in
    /// 0.10.1's world, in tiles; WallsView draws them. Laid whole every time (a few milliseconds): the drawing makes
    /// again only the chunks that changed. Not here yet: raised ground's faces (layRaised), the parapets, what stands on
    /// the roofs (roofProps) and the walls' caps.
    /// </summary>
    public sealed class WallLayout
    {
        /// <summary>A building kit's pieces (buildings.ts Kit): its walls, windows, doors and lintels, the first of each list in the building's colours.</summary>
        sealed class Kit
        {
            public string[] wall, window, door, lintel;
            public string leaf;
            public string post;
            /// <summary>The post's length along the wall (a strip of wall), and whether it's square (a corner post, thickened both ways).</summary>
            public float postAlong;
            public bool postSquare;
            /// <summary>The post in the building's colours too (a shop's: its render's corner painted as its walls).</summary>
            public bool postTint;
            public float thick;
            public float storey;
            /// <summary>Its pane: width, height, middle's height and how far out, in the kit's metres.</summary>
            public float[] glass;
            public string top;
            /// <summary>An office's cornice along the top of its top storey, and its plinth at the foot of its ground floor (phase 3 of #3).</summary>
            public string crown, plinth;
            /// <summary>
            /// How far out from the wall's middle the face of its facing stands (tiles; 0, none): the facing (its wall's pieces
            /// after the first) runs on that much round a corner's outside, and covers its door's jambs (Ana, 7/11 #18: the
            /// brick stopped short of each corner and of the door, the concrete between showing, toothed).
            /// </summary>
            public float facingOut;

            public Kit With(string[] wall = null, string[] window = null, string[] door = null, string[] lintel = null, bool noLeaf = false, float storey = 0, float[] glass = null,
                float facingOut = -1)
            {
                var k = (Kit)MemberwiseClone();
                if (facingOut >= 0) k.facingOut = facingOut;
                if (wall != null) k.wall = wall;
                if (window != null) k.window = window;
                if (door != null) k.door = door;
                if (lintel != null) k.lintel = lintel;
                if (noLeaf) k.leaf = null;
                if (storey > 0) k.storey = storey;
                if (glass != null) k.glass = glass;
                return k;
            }
        }

        /// <summary>
        /// Walls are drawn this much thicker than Synty's 0.2 m, so a soldier in cover against one reads as touching it
        /// (buildings.ts THICK). Doors, windows and posts thicken with them.
        /// </summary>
        public const float Thick = 1.75f;
        /// <summary>How far out a leaf goes in a faced wall (tiles): from its own face (0.161 m out, thickened) to the facing's (0.294).</summary>
        const float LeafOut = (0.294f - 0.161f) / 1.5f;
        /// <summary>Where a door's jamb's facing is centred (tiles from the door's middle): from the leaf's edge to the tile's.</summary>
        const float JambFace = (0.604f / 1.5f + 0.5f) / 2;
        const float Storey = BuildingPlan.Storey;
        /// <summary>Tiles a metre of the kit (SYNTY_SCALE · KIT_SCALE), and a kit's module (KIT_TILE, m).</summary>
        const float KitTiles = 1.2f / 1.5f;
        const float KitTile = 1.5f / 1.2f;
        /// <summary>Half a wall's thickness, in tiles.</summary>
        const float WallHalf = 0.1f * Thick * KitTiles;
        /// <summary>A wall blown down to low cover: its stump this tall (storeys).</summary>
        const float BreachHeight = 0.36f;
        /// <summary>A wall a car was driven into (GameState.wrecks, «wall»): its stump, lower.</summary>
        const float CarBreach = 0.12f;
        /// <summary>The barbed wire's length in tiles (one piece is stretched to a tile).</summary>
        const float WireLength = 3.94f * KitTiles;
        /// <summary>Tiles a metre of a prop (SYNTY_SCALE).</summary>
        const float PropTiles = 1 / 1.5f;
        /// <summary>A shop's front: a shop window and a glass door as tall as a house's door (m).</summary>
        const float ShopFrontTall = 1.86f;

        static readonly Dictionary<string, Kit> Kits = MakeKits();
        /// <summary>Walls between rooms: Town's interior walls, whatever the building.</summary>
        static readonly Kit Between = Kits[KitIds.House].With(wall: new[] { "roomWall" }, door: new[] { "roomDoorWall" }, lintel: new[] { "roomLintel" });

        static Dictionary<string, Kit> MakeKits()
        {
            var house = new Kit
            {
                wall = new[] { "houseWall" }, window = new[] { "houseWindowWall", "houseWindow" }, door = new[] { "houseDoorWall", "houseDoor" },
                lintel = new[] { "houseLintel" }, post = "houseCorner", postAlong = 1, postSquare = true, thick = Thick, storey = 2.9f,
                glass = new[] { 0.89f, 1.48f, 1.45f, -0.03f },
            };
            // Its windows on a sill a metre up (the steel frame is a door's), the glass from there to the frame's top.
            var bunker = new Kit
            {
                wall = new[] { "bunkerWall" }, window = new[] { "bunkerWindow", "bunkerSill" }, door = new[] { "bunkerLintel" }, leaf = "metalDoor",
                lintel = new[] { "bunkerLintel" }, post = "bunkerWall", postAlong = 0.3f, postSquare = false, thick = Thick, storey = 3,
                glass = new[] { 0.93f, 1.27f, 1.645f, 0 },
            };
            var brick = bunker.With(wall: new[] { "bunkerWall", "brickFace" }, window: new[] { "bunkerWindow", "bunkerSill", "brickSill" },
                door: new[] { "bunkerLintel", "brickLintel" }, lintel: new[] { "bunkerLintel", "brickLintel" });
            // (The brick's face 0.294 m out, thickened: EdificiosLab.Grosor.)
            brick.facingOut = 0.294f / 1.5f;
            var fence = new Kit
            {
                wall = new[] { "fenceWall" }, window = new[] { "bunkerWindow" }, door = new[] { "bunkerLintel" }, leaf = "metalDoor",
                lintel = new[] { "bunkerLintel" }, post = "fenceColumn", postAlong = 1, postSquare = false, thick = 1, storey = 3,
                glass = new[] { 0.93f, 1.85f, 1.355f, 0 }, top = "barbedWire",
            };
            // A shed: sheet metal outside a bunker wall (as the warehouses' brick), steel-framed windows, a metal door.
            Kit Shed(string sheet, string lintel, string sill) => brick.With(wall: new[] { "bunkerWall", sheet }, window: new[] { "bunkerWindow", "bunkerSill", sill },
                door: new[] { "bunkerLintel", lintel }, lintel: new[] { "bunkerLintel", lintel }, facingOut: 0);
            // A shop's facing (phase 2 of #3): POLYGON Shops' wall, its sash window, its door and lintel in that brick or
            // render; for a post, a square of the wall as thick as it, closed all round. The window's pane as the pack's
            // glass (0.72 by 1.31 m, its middle 1.3 m up, 6 cm in).
            var kits = new Dictionary<string, Kit>();
            foreach (string facing in new[] { ShopFacings.White, ShopFacings.Red, ShopFacings.Coloured, ShopFacings.Render })
            {
                string of = facing == ShopFacings.White ? "" : facing;
                kits[ShopKit(facing)] = new Kit
                {
                    wall = new[] { "shopWall" + of }, window = new[] { "shopWindowWall" + of }, door = new[] { "shopDoorWall" + of, "shopSideDoor" },
                    lintel = new[] { "shopLintel" + of }, post = "shopCorner" + of, postAlong = 1, postSquare = true, postTint = true, thick = Thick, storey = 3,
                    glass = new[] { 0.718f, 1.314f, 1.299f, -0.064f },
                };
            }
            // An office's facing (phase 3 of #3, the centre's of stone with a cornice, as City's old offices): Shops' wall, door
            // and corner as a shop's, its tall sash window (its pane as its two sashes' glass: 1.09 by 1.82 m, the middle
            // 1.44 m up), the cornice and the plinth, in stone blocks (painted as the building) or concrete.
            foreach (string facing in new[] { OfficeFacings.Ashlar, OfficeFacings.Concrete })
                kits[OfficeKit(facing)] = new Kit
                {
                    wall = new[] { "officeWall" + facing }, window = new[] { "officeWindowWall" + facing }, door = new[] { "officeDoorWall" + facing, "shopSideDoor" },
                    lintel = new[] { "officeLintel" + facing }, post = "officeCorner" + facing, postAlong = 1, postSquare = true, postTint = true, thick = Thick, storey = 3,
                    glass = new[] { 1.087f, 1.821f, 1.4425f, 0f }, crown = "officeCrown" + facing, plinth = "officePlinth" + facing,
                };
            // A hall's facing (phase 4 of #3, the estates' warehouses): Gang Warfare's brick wall (0.39 m thick, as Shops'
            // thickened), its factory window a bay a tile (the pane the bay's glass: 1.57 m tall, its middle 1.87 m up, 9 cm
            // out), its door in its frame and its leaf; or its sheet metal (0.22 m, thickened as any wall), its ribbon window
            // (1.07 m of glass, its middle 1.47 m up, 7 cm out).
            foreach (string facing in HallFacings.Brick)
                kits[HallKit(facing)] = new Kit
                {
                    wall = new[] { "hallWall" + facing }, window = new[] { "hallWindowWall" + facing }, door = new[] { "hallDoorWall" + facing, "hallDoor" + facing },
                    lintel = new[] { "hallLintel" + facing }, post = "hallCorner" + facing, postAlong = 1, postSquare = true, thick = 1, storey = 3,
                    glass = new[] { KitTile, 1.57f, 1.868f, 0.093f },
                };
            foreach (string facing in HallFacings.Sheet)
                kits[HallKit(facing)] = new Kit
                {
                    wall = new[] { "sheetWall" + facing }, window = new[] { "sheetWindowWall" + facing }, door = new[] { "sheetDoorWall" + facing, "sheetDoor" + facing },
                    lintel = new[] { "sheetLintel" + facing }, post = "sheetCorner" + facing, postAlong = 1, postSquare = true, thick = Thick, storey = 3,
                    glass = new[] { KitTile, 1.073f, 1.473f, 0.071f },
                };
            return new Dictionary<string, Kit>(kits)
            {
                [KitIds.House] = house, [KitIds.Bunker] = bunker, [KitIds.Brick] = brick, [KitIds.Fence] = fence,
                [KitIds.ShedGrey] = Shed("sheetGrey", "sheetGreyLintel", "sheetGreySill"),
                [KitIds.ShedRed] = Shed("sheetRed", "sheetRedLintel", "sheetRedSill"),
                [KitIds.ShedBlue] = Shed("sheetBlue", "sheetBlueLintel", "sheetBlueSill"),
                [KitIds.Medical] = bunker.With(),
                // A shop's front at street level: its windows and doors on the side of its door, whatever its building's kit.
                [KitIds.ShopFront] = brick.With(window: new[] { "shopWindow" }, door: new[] { "shopDoor" }, noLeaf: true, storey: 3 * Storey / ShopFrontTall,
                    glass: new[] { KitTile, 2.71f, 1.495f, 0 }),
            };
        }

        /// <summary>A shop facing's kit, an office facing's and a hall facing's (BuildingLooks.Facing).</summary>
        static string ShopKit(string facing) => "shop" + facing;
        static string OfficeKit(string facing) => "office" + facing;
        static string HallKit(string facing) => "hall" + facing;

        /// <summary>The kit building `bd`'s outer walls are of: a shop's, an office's or a hall's its facing's, any other's its own; a wall of nobody's, the map's.</summary>
        string KitIdOf(Building bd) => bd == null ? plan.LooseKit : BuildingLooks.Facing(bd) is string facing
            ? (bd.kind == BuildingKinds.Office ? OfficeKit(facing) : bd.kind == BuildingKinds.Hall || bd.kind == BuildingKinds.Barn ? HallKit(facing) : ShopKit(facing)) : bd.kit;

        /// <summary>
        /// Whether `level` is building bd's top storey, the one its cornice runs along: laid from its own tiles (a building of
        /// real storeys), or its ground floor if it has no other (the storeys above a ground floor are Storeys').
        /// </summary>
        static bool TopOf(Building bd, int level) => bd != null && (bd.leveled ? level == bd.storeys - 1 : bd.storeys == 1 && level == 0);
        Kit KitOf(Building bd) => Kits[KitIdOf(bd)];

        /// <summary>Town's window frames and front doors a house picks from (Ana's #3, 1/11: not every house alike), and how many in a hundred have shutters.</summary>
        static readonly string[] HouseWindows = { "houseWindow", "houseWindow2", "houseWindow3", "houseWindow4", "houseWindow5" };
        static readonly string[] HouseDoors = { "houseDoor", "houseDoor2", "houseDoor3" };
        const double ShutteredShare = 0.4;
        /// <summary>How many houses in a hundred with a long plain wall onto open ground have a garage door in it; its width (tiles).</summary>
        const double GarageShare = 0.5;
        const int GarageSpan = 4;

        /// <summary>A house's own kit (its window frame and its door from Town's) and whether its windows have shutters, by building.</summary>
        readonly Dictionary<int, (Kit kit, bool shutters)> houseLooks = new Dictionary<int, (Kit, bool)>();
        /// <summary>The ground floor's wall tiles a garage door takes (Lay): whether it is the stretch's first, where the stretch's middle is (tiles).</summary>
        readonly Dictionary<int, (bool first, float x, float z)> garages = new Dictionary<int, (bool, float, float)>();

        /// <summary>
        /// The kit `kit` as building `bd` has it: a house's (Town's kit, the house's own: HouseLook) its own frame and door
        /// and its shutters; any other's as it is, without shutters.
        /// </summary>
        (Kit kit, bool shutters) LookOf(Building bd, Kit kit)
        {
            if (bd == null || bd.kind != BuildingKinds.House || kit != Kits[KitIds.House]) return (kit, false);
            if (houseLooks.TryGetValue(bd.index, out var look)) return look;
            string window = HouseWindows[(int)Math.Floor(BuildingPlan.Hash(bd.x0, bd.y0, 32) * HouseWindows.Length)];
            string door = HouseDoors[(int)Math.Floor(BuildingPlan.Hash(bd.x0, bd.y0, 33) * HouseDoors.Length)];
            bool shutters = BuildingPlan.Hash(bd.x0, bd.y0, 34) < ShutteredShare;
            return houseLooks[bd.index] = (kit.With(window: new[] { kit.window[0], window }, door: new[] { kit.door[0], door }), shutters);
        }

        /// <summary>
        /// The pieces with no cap: a doorway's, the lintels over them, a gate's leaves, the wire along a fence's top and a
        /// fence's mesh, which is seen through.
        /// </summary>
        static readonly HashSet<string> NoCap = new HashSet<string>(
            Kits.Values.Append(Between).SelectMany(k => k.door.Concat(k.lintel).Concat(new[] { k.leaf, k.top, k.crown, k.plinth }.Where(id => id != null)))
                .Concat(new[] { "fenceWall", "fenceColumn", "gateLeaf", "gateLeafFar" }));

        /// <summary>Shop signs and how long each is (tiles): the long ones are scaled down to fit over a door.</summary>
        static readonly Dictionary<string, float> Signs = new Dictionary<string, float> { ["signShop"] = 1.7f, ["signFood"] = 2.2f, ["signMarket"] = 2.64f, ["signDiner"] = 1.97f };
        /// <summary>MARKET only over a supermarket's door: a shop of one storey this wide at least.</summary>
        const int MarketSpan = 7;
        /// <summary>
        /// A sign's middle this high over its floor, no taller than SignTall (m; the models are 0.63): over a shop's awning
        /// (Unity's own: 0.10.1 had it 0.22 down, and no awnings).
        /// </summary>
        const float SignUp = Storey - 0.16f;
        const float SignTall = 0.42f;
        /// <summary>A supermarket's MARKET, over its double door on its brick front's deeper band: this big.</summary>
        const float MarketSign = 0.95f;
        /// <summary>A grocer: its drinks fridge and this many shelves of stock (homeFurnishing.ts GROCER_SHELVES).</summary>
        const int GrocerShelves = 3;

        public readonly List<WallPiece> Pieces = new List<WallPiece>();
        public readonly List<WallPane> Panes = new List<WallPane>();
        public readonly List<WallProp> Props = new List<WallProp>();

        readonly GameState s;
        readonly BuildingPlan plan;
        readonly int w, h;
        /// <summary>The storey whose walls are being laid (0 the ground), its tiles, and the tile being laid.</summary>
        int level;
        IList<string> layer;
        int tile = -1;

        WallLayout(GameState s, BuildingPlan plan)
        {
            this.s = s;
            this.plan = plan;
            w = s.Width;
            h = s.Height;
        }

        /// <summary>The walls of state `s` (its plan made from the map as it was built: BuildingPlan).</summary>
        public static WallLayout Of(GameState s, BuildingPlan plan)
        {
            var layout = new WallLayout(s, plan);
            layout.Lay();
            return layout;
        }

        static bool IsWallLike(string k) => BuildingPlan.IsWallLike(k);
        static bool IsDoor(string t) => t == TileKinds.Door || t == TileKinds.DoorOpen;
        static string TileIn(IList<string> tiles, int i) => tiles != null && i >= 0 && i < tiles.Count ? tiles[i] : null;
        string At(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? null : TileIn(layer, y * w + x);
        bool InMap(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
        Building BuildingOf(int b) => b >= 0 && b < plan.Buildings.Count ? plan.Buildings[b] : null;

        /// <summary>
        /// Wall-like, or a wall blown down (it still joins the walls beside it); a siege's watchtower carries the wall on round
        /// its base. Not the outline's own walls out of play, nobody's (frameArt's lots, never laid here: a block of the
        /// outline against one put half a wall towards it on every tile, a comb along its face; Gameplay, 1/11).
        /// </summary>
        bool Wallish(int x, int y) =>
            (IsWallLike(At(x, y)) && !(InMap(x, y) && plan.Out[y * w + x] != 0 && plan.Owner[y * w + x] < 0))
            || (InMap(x, y) && (plan.Breached(y * w + x, level) || (level == 0 && plan.Raised[y * w + x] == BuildingPlan.RaisedTower)));

        /// <summary>three.js's compose(position, a turn about y, scale), in 0.10.1's world (tiles).</summary>
        static Matrix4x4 Compose(float x, float y, float z, float yaw, Vector3 scale) =>
            Matrix4x4.Translate(new Vector3(x, y, z)) * Map.Pieces.RotationY(yaw) * Matrix4x4.Scale(scale);

        static float Yaw(Vec2 o) => Mathf.Atan2(o.X, o.Y);

        /// <summary>
        /// One piece at (x, z), its outer face towards `o`, `along` its length in tiles, `thick` its thickness against
        /// the kit's, `square` thickened both ways (a corner post); `tint` with the building's colours, `open` a door leaf
        /// swung open; `roof` a façade's upper storey (it goes with the roof in the cutaway).
        /// </summary>
        void Put(string id, float x, float z, float @base, Vec2 o, bool between, int b, float along = 1, float thick = Thick, bool square = false, bool tint = false,
            bool open = false, bool roof = false, float tall = 1, bool otherHinge = false, bool cap = false)
        {
            Pieces.Add(new WallPiece
            {
                id = id, matrix = Compose(x, @base, z, Yaw(o), new Vector3(along, tall, 1)), thick = thick, square = square, building = b, level = level, tile = tile,
                floor = @base, outward = new Vector2(o.X, o.Y), between = between, roof = roof, tint = tint, open = open, otherHinge = otherHinge,
                // Its cap, on the storey in view's walls: the first of a stretch's pieces (the wall itself) and posts.
                cap = (tint || cap) && !roof && !open && !NoCap.Contains(id),
            });
        }

        /// <summary>A list of pieces: the first one in the building's colours.</summary>
        void PutAll(string[] ids, float x, float z, float @base, Vec2 o, bool between, int b, Kit kit, bool open = false, bool roof = false, bool otherHinge = false)
        {
            for (int k = 0; k < ids.Length; k++) Put(ids[k], x, z, @base, o, between, b, thick: kit.thick, tint: k == 0, open: open, roof: roof, otherHinge: otherHinge);
        }

        void Post(Kit kit, float x, float z, float @base, Vec2 o, bool between, int b, bool roof = false) =>
            Put(kit.post, x, z, @base, o, between, b, along: kit.postAlong, thick: kit.thick, square: kit.postSquare, tint: kit.postTint, roof: roof, cap: true);

        /// <summary>A window pane at (x, z), `base` its storey's floor (upper storeys' go with the roof).</summary>
        void Pane(string kit, float x, float z, float @base, Vec2 o, int b, bool roof, bool between = false)
        {
            Panes.Add(new WallPane
            {
                kit = kit, matrix = Compose(x, @base, z, Yaw(o), Vector3.one), building = b, level = level, tile = tile, roof = roof,
                outward = new Vector2(o.X, o.Y), between = between, floor = @base,
            });
        }

        void Prop(string id, Matrix4x4 matrix, int b) => Props.Add(new WallProp { id = id, matrix = matrix, building = b, level = level, tile = tile });

        /// <summary>
        /// The storeys above the ground floor of a stretch of wall (`along` its length, centred on (x, z)): the building's
        /// own outer walls up to its roof, windows over doors and windows (and on shops and offices every other tile, the
        /// same on every storey, in columns: Arturo, 2/11, Ana's #3, missing rows of windows; 0.10.1's buildings.ts moved
        /// them a tile along from one storey to the next, a checkerboard); over a wall shared with a lower neighbour, what
        /// stands above the neighbour's roof, facing it.
        /// </summary>
        void Storeys(string ground, float x, float z, int t, Vec2 o, bool between, int b, int other, float along = 1, Vec2 d = default, float reach = 0)
        {
            var bd = BuildingOf(b);
            // Real storeys are laid from their own tiles.
            if (bd == null || bd.leveled || (other >= 0 && plan.Buildings[other].leveled)) return;
            int from = 1, to = between ? 1 : bd.storeys;
            int owner = b;
            var face = o;
            if (other >= 0)
            {
                // A party wall: only what one of the two stands above the other.
                int them = plan.Buildings[other].storeys;
                from = Math.Min(bd.storeys, them);
                to = Math.Max(bd.storeys, them);
                owner = bd.storeys >= them ? b : other;
                face = bd.storeys >= them ? o : new Vec2 { X = -o.X, Y = -o.Y };
            }
            var ob = plan.Buildings[owner];
            var (kit, shutters) = LookOf(ob, KitOf(ob));
            for (int k = from; k < to; k++)
            {
                float y0 = ob.@base + k * Storey;
                bool glazed = other < 0 && (ground != TileKinds.Wall || (ob.kind != BuildingKinds.House && ob.kind != BuildingKinds.Motel
                    && t % 2 == 0));
                // (An office's cornice along its top storey: phase 3.)
                if (kit.crown != null && k == ob.storeys - 1) Put(kit.crown, x, z, y0, face, false, owner, along: along, thick: kit.thick, tint: true, roof: true);
                if (along < 1)
                {
                    // (A corner's arm: its facing round the corner's outside, as on the ground: `reach` back along `d`.)
                    for (int n = 0; n < kit.wall.Length; n++)
                    {
                        float r = n > 0 && owner == b ? reach : 0;
                        Put(kit.wall[n], x - d.X * r / 2, z - d.Y * r / 2, y0, face, false, owner, along: along + r, thick: kit.thick, tint: n == 0, roof: true);
                    }
                    continue;
                }
                PutAll(glazed ? kit.window : kit.wall, x, z, y0, face, false, owner, kit, false, true);
                if (glazed && shutters) Put("houseShutters", x, z, y0, face, false, owner, thick: kit.thick, roof: true);
                if (glazed && kit.top == null) Pane(KitIdOf(ob), x, z, y0, face, owner, true);
            }
        }

        /// <summary>Whether tile (x, y) of building `bd`'s outline is on its side `side` (DIRS4).</summary>
        static bool OnSide(Building bd, int x, int y, int side) =>
            side == 0 ? y == bd.y0 : side == 1 ? x == bd.x1 : side == 2 ? y == bd.y1 : x == bd.x0;

        /// <summary>How many doors in a row along `along` tile (x, y) is one of.</summary>
        int DoorRow(int x, int y, Vec2 along)
        {
            int n = 1;
            foreach (int side in new[] { -1, 1 })
                for (int k = 1; IsDoor(At(x + along.X * k * side, y + along.Y * k * side)); k++) n++;
            return n;
        }

        /// <summary>Where the door leaf among pieces `ids` is hinged (x in the piece's frame); 0 if none has one.</summary>
        static float LeafHinge(IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                var leaf = Map.Pieces.Parts(id).FirstOrDefault(p => p.leaf);
                if (leaf != null) return leaf.hinge.x;
            }
            return 0;
        }

        string ShopSign(Building b) => ShopSign(s, b);

        /// <summary>What a shop's sign says (homeFurnishing.ts shopSign): only what its ground floor shows.</summary>
        public static string ShopSign(GameState s, Building b)
        {
            int w = s.Width;
            int tables = 0, fridges = 0, shelves = 0;
            for (int y = b.y0; y <= b.y1; y++)
                for (int x = b.x0; x <= b.x1; x++)
                {
                    int i = y * w + x;
                    string kind = s.Decor != null && i < s.Decor.Count ? Bastion.Engine.Decor.DecorOf(s.Decor[i])?.Kind : null;
                    if (kind == "cafeTable") tables++;
                    else if (kind == "drinksFridge") fridges++;
                    else if (kind == "shelf") shelves++;
                }
            return tables > 0 ? "cafe" : fridges > 0 && shelves >= GrocerShelves ? "grocer" : "shop";
        }

        void Lay()
        {
            // The ground as the walls see it: an outline block's inside a floor (BuildingPlan.Shut), so only its outline is laid.
            IList<string> groundTiles = plan.Shut.Contains((byte)1) ? s.Tiles.Select((t, i) => plan.Shut[i] != 0 ? TileKinds.Floor : t).ToList() : (IList<string>)s.Tiles;
            var dirs = Bastion.Engine.Grid.Dirs4;

            // Each building's first door on its outline (in tile order): where its sign goes.
            var signDoor = new Dictionary<int, int>();
            foreach (var bd in plan.Buildings)
            {
                int first = -1;
                void Look(int x, int y)
                {
                    int i = y * w + x;
                    string t = TileIn(s.Tiles, i);
                    if (IsDoor(t) && plan.Owner[i] == bd.index && (first < 0 || i < first) && (bd.market == null || OnSide(bd, x, y, bd.market.Value))) first = i;
                }
                for (int x = bd.x0; x <= bd.x1; x++)
                {
                    Look(x, bd.y0);
                    Look(x, bd.y1);
                }
                for (int y = bd.y0 + 1; y < bd.y1; y++)
                {
                    Look(bd.x0, y);
                    Look(bd.x1, y);
                }
                if (first >= 0) signDoor[bd.index] = first;
            }
            // A shop's front, at street level: whether tile (x, y) of its building is on the side of its sign's door.
            bool ShopFront(Building bd, int x, int y)
            {
                if (bd == null || bd.kind != BuildingKinds.Shop || plan.Siege || !signDoor.TryGetValue(bd.index, out int door)) return false;
                if (bd.market != null) return OnSide(bd, x, y, bd.market.Value);
                int dx = door % w, dy = door / w;
                return dy == bd.y0 ? y == bd.y0 : dy == bd.y1 ? y == bd.y1 : dx == bd.x0 ? x == bd.x0 : x == bd.x1;
            }
            // Where a shed's roller shutters go: the middle of the longest stretch of plain wall, four tiles or more, on
            // each of two of its sides; how wide each is. A hall of brick's too (phase 4 of #3: its loading doors).
            var shutters = new Dictionary<int, float>();
            foreach (var bd in plan.Buildings)
            {
                if (!(BuildingPlan.IsShed(bd.kit) || Array.IndexOf(HallFacings.Brick, bd.facing) >= 0) || bd.@base > 0) continue;
                var runs = new List<(int tile, int length, int side)>();
                var sides = new[]
                {
                    (bd.x0 + 1, bd.y0, 1, 0, bd.x1 - bd.x0 - 1), (bd.x0 + 1, bd.y1, 1, 0, bd.x1 - bd.x0 - 1),
                    (bd.x0, bd.y0 + 1, 0, 1, bd.y1 - bd.y0 - 1), (bd.x1, bd.y0 + 1, 0, 1, bd.y1 - bd.y0 - 1),
                };
                for (int side = 0; side < 4; side++)
                {
                    var (x0, y0, dx, dy, n) = sides[side];
                    int start = -1;
                    for (int k = 0; k <= n; k++)
                    {
                        int i = (y0 + dy * k) * w + x0 + dx * k;
                        bool plain = k < n && TileIn(s.Tiles, i) == TileKinds.Wall && plan.Owner[i] == bd.index;
                        if (plain && start < 0) start = k;
                        if (!plain && start >= 0)
                        {
                            int mid = (start + k - 1) / 2;
                            runs.Add(((y0 + dy * mid) * w + x0 + dx * mid, k - start, side));
                            start = -1;
                        }
                    }
                }
                // Longest first, ties in the order found (Array.prototype.sort is stable).
                runs = runs.OrderByDescending(r => r.length).ToList();
                var sided = new HashSet<int>();
                foreach (var r in runs)
                {
                    if (r.length < 4 || sided.Contains(r.side) || sided.Count >= 2) continue;
                    sided.Add(r.side);
                    shutters[r.tile] = Math.Min(3, r.length - 1.4f);
                }
            }
            // A garage door (Ana's #3, 1/11) on half the houses with room for one: Town's roller door, GarageSpan tiles of
            // the ground floor's plain outer wall onto open ground (nobody's building, and walkable), in the middle of the
            // longest such stretch; the tiles it takes and the middle one's place.
            garages.Clear();
            foreach (var bd in plan.Buildings)
            {
                if (bd.kind != BuildingKinds.House || bd.kit != KitIds.House || bd.@base > 0 || BuildingPlan.Hash(bd.x0, bd.y0, 35) >= GarageShare) continue;
                var sides = new[]
                {
                    (bd.x0 + 1, bd.y0, 1, 0, bd.x1 - bd.x0 - 1, 0, -1), (bd.x0 + 1, bd.y1, 1, 0, bd.x1 - bd.x0 - 1, 0, 1),
                    (bd.x0, bd.y0 + 1, 0, 1, bd.y1 - bd.y0 - 1, -1, 0), (bd.x1, bd.y0 + 1, 0, 1, bd.y1 - bd.y0 - 1, 1, 0),
                };
                (int start, int length, int side) best = (0, 0, -1);
                for (int side = 0; side < 4; side++)
                {
                    var (x0, y0, dx, dy, n, ox, oy) = sides[side];
                    int start = -1;
                    for (int k = 0; k <= n; k++)
                    {
                        int x = x0 + dx * k, y = y0 + dy * k, i = y * w + x;
                        bool plain = k < n && TileIn(s.Tiles, i) == TileKinds.Wall && plan.Owner[i] == bd.index && InMap(x + ox, y + oy)
                            && OwnerAt((y + oy) * w + x + ox) < 0 && Bastion.Engine.Grid.IsWalkable(TileIn(s.Tiles, (y + oy) * w + x + ox));
                        if (plain && start < 0) start = k;
                        if (!plain && start >= 0)
                        {
                            if (k - start > best.length) best = (start, k - start, side);
                            start = -1;
                        }
                    }
                }
                if (best.length < GarageSpan) continue;
                var (gx, gy, gdx, gdy, _, _, _) = sides[best.side];
                int from = best.start + (best.length - GarageSpan) / 2;
                float mx = gx + gdx * (from + GarageSpan / 2f) + (gdx == 0 ? 0.5f : 0), mz = gy + gdy * (from + GarageSpan / 2f) + (gdy == 0 ? 0.5f : 0);
                for (int k = 0; k < GarageSpan; k++) garages[(gy + gdy * (from + k)) * w + gx + gdx * (from + k)] = (k == 0, mx, mz);
            }

            // A roller shutter `width` tiles wide on a shed's wall, shut, `tall` of a storey tall, on its outer face. (The
            // model is 6.84 m wide from x = 0.35, 4.19 m tall, its face to +z; stood on the floor.)
            void Shutter(Building bd, float x, float z, float @base, Vec2 o, float width, float tall = 0.75f)
            {
                float sx = width / (6.84f * PropTiles);
                float sy = tall * Storey / (4.19f * PropTiles);
                float off = WallHalf + 0.02f;
                float yaw = Mathf.Atan2(o.X, o.Y);
                float alongX = Mathf.Cos(yaw), alongZ = -Mathf.Sin(yaw);
                float left = 3.77f * PropTiles * sx;
                Prop("rollerShutter", Compose(x + o.X * off - alongX * left, @base, z + o.Y * off - alongZ * left, yaw, new Vector3(sx, sy, 0.6f)), bd.index);
            }
            // A sign over a door, on the wall's outer face, its face out.
            void Sign(Building bd, float x, float z, float @base, Vec2 o)
            {
                bool market = bd.kind == BuildingKinds.Shop && bd.storeys == 1 && Math.Min(bd.x1 - bd.x0, bd.y1 - bd.y0) + 1 >= MarketSpan;
                string trade = bd.market != null ? null : ShopSign(bd);
                string id = trade == null ? "signMarket" : trade == "grocer" ? (market ? "signMarket" : "signFood") : trade == "cafe" ? "signDiner" : "signShop";
                float length = Signs[id];
                // A supermarket's double door: the sign over the middle of the two.
                if (bd.market != null)
                {
                    var along = o.X == 0 ? new Vec2 { X = 1, Y = 0 } : new Vec2 { X = 0, Y = 1 };
                    string next = TileIn(s.Tiles, ((int)Math.Floor(z) + along.Y) * w + (int)Math.Floor(x) + along.X);
                    float shift = IsDoor(next) ? 0.5f : -0.5f;
                    x += along.X * shift;
                    z += along.Y * shift;
                }
                float size = bd.market != null ? MarketSign : Mathf.Min(1, Mathf.Min(2.2f / length, SignTall / 0.63f));
                float off = WallHalf + 0.05f;
                Prop(id, Compose(x + o.X * off, @base + SignUp, z + o.Y * off, Mathf.Atan2(-o.Y, o.X), Vector3.one * size), bd.index);
            }

            // The ground's walls, then each storey's (GameState.Levels).
            int levels = s.Levels?.Count ?? 0;
            for (level = 0; level <= levels; level++)
            {
                layer = level > 0 ? s.Levels[level - 1].Tiles : groundTiles;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        LayTile(x, y, dirs, ShopFront, signDoor, shutters, Shutter, Sign);
            }
            level = 0;
            layer = s.Tiles;

            Raised(dirs);
            if (plan.Siege) Towers(dirs);
            MotelSigns(dirs);
        }

        int Elev(int x, int y) => !InMap(x, y) ? 0 : y * w + x < s.Elev.Count ? s.Elev[y * w + x] : 0;

        /// <summary>A piece standing at (x, base, z), its outer face (+z) towards `o`, as it is (no thickening, no colours).</summary>
        void Stand(string id, float x, float @base, float z, Vec2 o) =>
            Pieces.Add(new WallPiece { id = id, matrix = Compose(x, @base, z, Yaw(o), Vector3.one), thick = 1, building = -1, level = level, tile = tile, floor = @base, outward = new Vector2(o.X, o.Y) });

        /// <summary>
        /// What stands on raised ground, as it is now (buildings.ts layRaised): flats get a brick façade on every side
        /// above lower ground, a storey at a time (one window a tile, now and then plain brick, arched windows under a
        /// cornice at the top); platforms, container sides; hills, rock faces; solid wall masses, concrete. And ladders on
        /// every climb. (The flats' parapets are surfaces: BuildingSurfaces.)
        /// </summary>
        void Raised(IReadOnlyList<Vec2> dirs)
        {
            int storeys = s.Levels?.Count ?? 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    tile = i;
                    level = 0;
                    int e = Elev(x, y);
                    // Ladders: against the side of the tile one storey up, whatever it is (a building's roof one walks on,
                    // too); a building's stairs, every tile of a flight, are drawn as stairs (BuildingSurfaces).
                    if (TileIn(s.Tiles, i) == TileKinds.Ladder && Flights.FlightAt(s, new Pos { X = x, Y = y, Z = 0 }) == null)
                        foreach (var d in dirs)
                        {
                            bool up = Elev(x + d.X, y + d.Y) == e + 1 || (e == 0 && Bastion.Engine.Grid.IsWalkable(plan.KindAt(x + d.X, y + d.Y, 1)));
                            if (up) Stand("ladder", x + 0.5f + d.X * 0.44f, e * Storey, y + 0.5f + d.Y * 0.44f, new Vec2 { X = -d.X, Y = -d.Y });
                        }
                    // On a storey: a ladder up to the next one.
                    for (int k = 1; k <= storeys; k++)
                    {
                        if (plan.KindAt(x, y, k) != TileKinds.Ladder || Flights.FlightAt(s, new Pos { X = x, Y = y, Z = k }) != null) continue;
                        level = k;
                        foreach (var d in dirs)
                            if (Bastion.Engine.Grid.IsWalkable(plan.KindAt(x + d.X, y + d.Y, k + 1)))
                                Stand("ladder", x + 0.5f + d.X * 0.44f, k * Storey, y + 0.5f + d.Y * 0.44f, new Vec2 { X = -d.X, Y = -d.Y });
                        level = 0;
                    }
                    // A solid mass of wall: concrete on its sides.
                    if (plan.Massive[i] != 0)
                        foreach (var d in dirs)
                        {
                            string n = BuildingPlan.TileAt(s, x + d.X, y + d.Y);
                            if (n != null && IsWallLike(n) && plan.Massive[(y + d.Y) * w + x + d.X] != 0) continue;
                            Stand("bunkerWall", x + 0.5f + d.X * 0.42f, e * Storey, y + 0.5f + d.Y * 0.42f, d);
                        }
                    byte style = plan.Raised[i];
                    // A watchtower's sides are its legs (the guard tower, MapBuilder).
                    if (style == 0 || style == BuildingPlan.RaisedTower) continue;
                    foreach (var d in dirs)
                    {
                        int low = Elev(x + d.X, y + d.Y);
                        if (low >= e) continue;
                        int t = d.X != 0 ? y : x;
                        float ex = x + 0.5f + d.X * 0.5f, ez = y + 0.5f + d.Y * 0.5f;
                        if (style == BuildingPlan.RaisedHill)
                        {
                            // Two rocks stacked up the side, each its own way, bulging out a little.
                            double R(int salt) => BuildingPlan.Hash(x * 4 + d.X + 2, y * 4 + d.Y + 2, salt);
                            for (int k = low; k < e; k++)
                            {
                                float b0 = k * Storey;
                                Rock(R(1) < 0.5 ? "cliff" : "cliff2", ex - d.X * 0.02f, b0 - 0.25f, ez - d.Y * 0.02f, d, 1.9f + (float)R(2) * 0.3f, 1.0f + (float)R(3) * 0.25f, R(4) < 0.5 ? 0 : Mathf.PI);
                                Rock(R(5) < 0.5 ? "cliff" : "cliff2", ex - d.X * 0.08f, b0 + 1.0f, ez - d.Y * 0.08f, d, 1.6f + (float)R(6) * 0.3f, 0.9f + (float)R(7) * 0.25f, R(8) < 0.5 ? 0 : Mathf.PI);
                            }
                            continue;
                        }
                        if (style == BuildingPlan.RaisedPlatform)
                        {
                            for (int k = low; k < e; k++) Stand("containerWall", ex - d.X * 0.1f, k * Storey, ez - d.Y * 0.1f, d);
                            continue;
                        }
                        // Flats: along the face every third tile plain brick, and where a ladder leans on it.
                        bool ladder = InMap(x + d.X, y + d.Y) && TileIn(s.Tiles, (y + d.Y) * w + x + d.X) == TileKinds.Ladder;
                        for (int k = low; k < e; k++)
                        {
                            bool plain = ladder || (t + k) % 3 == 1;
                            string id = k == e - 1 ? (plain ? "blockTopPlain" : "blockTopWindow") : plain ? "blockPlain" : "blockWindow";
                            Stand(id, ex, k * Storey, ez, d);
                        }
                    }
                }
            level = 0;
        }

        /// <summary>A rock of a hill's side, its face out towards `o`, stretched `tall` times, turned `turn` (it lies along x).</summary>
        void Rock(string id, float x, float @base, float z, Vec2 o, float tall, float along, float turn) =>
            Prop(id, Compose(x, @base, z, Mathf.Atan2(o.X, o.Y) + Mathf.PI / 2 + turn, new Vector3(along, tall, 1.7f)), -1);

        void LayTile(int x, int y, IReadOnlyList<Vec2> dirs, Func<Building, int, int, bool> shopFront, Dictionary<int, int> signDoor, Dictionary<int, float> shutters,
            Action<Building, float, float, float, Vec2, float, float> shutter, Action<Building, float, float, float, Vec2> sign)
        {
            int i = y * w + x;
            string kind = TileIn(layer, i);
            if (kind == null || kind == TileKinds.Roof) return;
            tile = i;
            int b = plan.Owner[i];
            bool loose = b < 0;
            // A wall blown down to low cover: laid as a wall, but its stump and the wall above the gap.
            bool broken = kind == TileKinds.Low && plan.Breached(i, level);
            if (loose && (level > 0 || (!IsWallLike(kind) && !broken) || plan.Massive[i] != 0 || plan.Out[i] != 0)) return;
            if (level == 0 && plan.Massive[i] != 0) return;
            var building = loose ? null : plan.Buildings[b];
            float @base = building != null ? building.@base + level * Storey : (i < s.Elev.Count ? s.Elev[i] : 0) * Storey;
            var outerKit = KitOf(building);
            float cx = x + 0.5f, cz = y + 0.5f;
            if (!IsWallLike(kind) && !broken)
            {
                // A gap in the outline (a loading bay): a lintel over it. Ruins are just broken.
                var bd = building;
                if (bd.kind == BuildingKinds.Ruin) return;
                bool edgeX = y == bd.y0 || y == bd.y1;
                bool edgeZ = x == bd.x0 || x == bd.x1;
                if (plan.Inside[i] == 0 || !(edgeX || edgeZ) || (kind != TileKinds.Floor && kind != TileKinds.Low)) return;
                var dir = edgeX ? new Vec2 { X = 0, Y = y == bd.y0 ? -1 : 1 } : new Vec2 { X = x == bd.x0 ? -1 : 1, Y = 0 };
                var step = edgeX ? new Vec2 { X = 1, Y = 0 } : new Vec2 { X = 0, Y = 1 };
                bool Bounded(int sign)
                {
                    for (int k = 1; k <= 4; k++)
                    {
                        int px = x + step.X * k * sign, py = y + step.Y * k * sign;
                        string t = At(px, py);
                        if (IsWallLike(t)) return true;
                        if (t == null || OwnerAt(py * w + px) != b) return false;
                    }
                    return false;
                }
                if (Bounded(1) && Bounded(-1)) PutAll(outerKit.lintel, cx, cz, @base, dir, false, b, outerKit);
                return;
            }
            var conn = dirs.Select(d => Wallish(x + d.X, y + d.Y)).ToArray();
            bool nsConn = conn[0] || conn[2];
            bool ewConn = conn[1] || conn[3];

            // Which way the outer face of a wall along `run` (0: x, 1: z) looks (judged on the tiles beside (x + ox,
            // y + oy), for building `bb`), whether it's between rooms, and the neighbour it's shared with (a party wall:
            // between, facing that neighbour), or -1.
            (Vec2 o, bool between, int other) Facing(int run, int ox, int oy, int bb)
            {
                var a = run == 0 ? new Vec2 { X = 0, Y = -1 } : new Vec2 { X = -1, Y = 0 };
                int pax = x + ox + a.X, pay = y + oy + a.Y, pbx = x + ox - a.X, pby = y + oy - a.Y;
                bool inA = bb >= 0 && plan.Interior(bb, pax, pay);
                bool inB = bb >= 0 && plan.Interior(bb, pbx, pby);
                int Neighbour(int px, int py)
                {
                    if (bb < 0 || !InMap(px, py)) return -1;
                    int o = plan.Owner[py * w + px];
                    return o >= 0 && o != bb && plan.Inside[py * w + px] != 0 ? o : -1;
                }
                var back = new Vec2 { X = -a.X, Y = -a.Y };
                if (inA && inB) return (back, true, -1);
                if (inA)
                {
                    int them = Neighbour(pbx, pby);
                    return (back, them >= 0, them);
                }
                int other = inB ? Neighbour(pax, pay) : -1;
                return (a, other >= 0, other);
            }

            bool straight = (ewConn && !nsConn) || (nsConn && !ewConn) || (!ewConn && !nsConn);
            if (straight)
            {
                // Along x unless the wall runs north–south (a lone tile follows the floor beside it).
                int run = nsConn || (!ewConn && plan.Interior(b, x - 1, y) && plan.Interior(b, x + 1, y)) ? 1 : 0;
                var (o, between, other) = Facing(run, 0, 0, b);
                bool front = level == 0 && !between && kind != TileKinds.Wall && kind != TileKinds.Low && shopFront(building, x, y);
                var kit = between && building?.kind != BuildingKinds.Medical ? Between : front ? Kits[KitIds.ShopFront] : outerKit;
                bool shuttered = false;
                if (kit == outerKit) (kit, shuttered) = LookOf(building, kit);
                var along = run == 0 ? new Vec2 { X = 1, Y = 0 } : new Vec2 { X = 0, Y = 1 };
                if (!loose) Storeys(broken ? TileKinds.Wall : kind, cx, cz, run == 0 ? x : y, o, between, b, other);
                if (IsDoor(kind) && loose && plan.Siege)
                {
                    // A siege's gate: a sheet-metal leaf to a tile under the open sky, each hinged at its end of the gate
                    // (the piece's near end is the other way from its +x; the partner turns on its far end), and a column
                    // of the wall where the gate meets it.
                    bool GateAt(int k) => IsDoor(At(x + along.X * k, y + along.Y * k));
                    int partner = GateAt(1) ? 1 : GateAt(-1) ? -1 : IsWallLike(At(x + along.X, y + along.Y)) ? -1 : 1;
                    float turn = Mathf.Atan2(o.X, o.Y);
                    bool near = (Mathf.Cos(turn) * along.X - Mathf.Sin(turn) * along.Y) * partner > 0;
                    if (GateAt(1) && GateAt(-1))
                    {
                        // Between two others (a gate three wide or more): no post to hang it on but one between two
                        // doorways, so it slides: shut in its place, open run along outside the next one (which swings in).
                        float slide = kind == TileKinds.DoorOpen ? 0.9f : 0;
                        Put("gateLeaf", cx + along.X * slide + o.X * slide * 0.16f, cz + along.Y * slide + o.Y * slide * 0.16f, @base, o, false, b, thick: kit.thick, open: false);
                    }
                    else Put(near ? "gateLeaf" : "gateLeafFar", cx, cz, @base, o, false, b, thick: kit.thick, open: kind == TileKinds.DoorOpen);
                    foreach (int side in new[] { -1, 1 })
                        if (!GateAt(side) && IsWallLike(At(x + along.X * side, y + along.Y * side))) Post(kit, cx + along.X * side * 0.5f, cz + along.Y * side * 0.5f, @base, o, false, b);
                }
                else if (IsDoor(kind) && building != null && !between && DoorRow(x, y, along) >= 3)
                {
                    // A bay of three doors or more (a dock's, a hall's gate): no leaf to hang but on a post between two
                    // doorways, so a roller shutter instead, down to the floor while shut and up under the lintel once open.
                    PutAll(kit.lintel, cx, cz, @base, o, between, b, kit);
                    if (kind == TileKinds.Door) shutter(building, cx, cz, @base, o, 1.02f, 0.82f);
                }
                else if (IsDoor(kind))
                {
                    // The leaf hinged on a jamb against a wall (or a window), never on one between two doorways.
                    int hingeSide = Math.Sign(LeafHinge(kit.door.Concat(kit.leaf != null ? new[] { kit.leaf } : Array.Empty<string>())) * (o.Y * along.X - o.X * along.Y));
                    bool Shuts(int k)
                    {
                        string t = At(x + along.X * k, y + along.Y * k);
                        return !Bastion.Engine.Grid.IsWalkable(t) || t == TileKinds.Window || t == TileKinds.WindowBroken;
                    }
                    bool otherHinge = hingeSide != 0 && !Shuts(hingeSide) && Shuts(-hingeSide);
                    bool open = kind == TileKinds.DoorOpen;
                    // A shop front's glass takes none of the building's paint (its lintel does).
                    if (front)
                    {
                        foreach (string id in kit.door) Put(id, cx, cz, @base, o, between, b, thick: kit.thick, tint: false, open: open, otherHinge: otherHinge);
                        PutAll(outerKit.lintel, cx, cz, @base, o, between, b, outerKit);
                        Awning(building, x, y, cx, cz, @base, o, shopFront);
                    }
                    else PutAll(kit.door, cx, cz, @base, o, between, b, kit, open, false, otherHinge);
                    // A shop's name over its front door: the first door of its outline. Not an office's.
                    if (level == 0 && !between && !plan.Siege && building != null && signDoor.TryGetValue(b, out int first) && first == i && building.kind == BuildingKinds.Shop)
                        sign(building, cx, cz, @base, o);
                    if (kit.leaf != null)
                    {
                        // (With a facing, the leaf as far out as its face, so the jambs' toothed ends don't show past it: Ana, 7/11 #18.)
                        float lean = kit.facingOut > 0 && !between ? LeafOut : 0;
                        Put(kit.leaf, cx + o.X * lean, cz + o.Y * lean, @base, o, between, b, thick: kit.thick, open: open, otherHinge: otherHinge);
                        // Jambs either side of the leaf, faced as the wall up to the leaf's edge (0.604 m from the middle).
                        foreach (int side in new[] { -1, 1 })
                        {
                            Put(kit.post, cx + along.X * side * 0.46f, cz + along.Y * side * 0.46f, @base, o, between, b, along: 0.08f, thick: kit.thick);
                            if (kit.facingOut > 0 && !between)
                                for (int n = 1; n < kit.wall.Length; n++)
                                    Put(kit.wall[n], cx + along.X * side * JambFace, cz + along.Y * side * JambFace, @base, o, between, b, along: 1 - 2 * JambFace, thick: kit.thick);
                        }
                    }
                }
                else if (kind == TileKinds.Window || kind == TileKinds.WindowBroken)
                {
                    if (front)
                    {
                        foreach (string id in kit.window) Put(id, cx, cz, @base, o, between, b, thick: kit.thick, tint: false);
                        PutAll(outerKit.lintel, cx, cz, @base, o, between, b, outerKit);
                        Awning(building, x, y, cx, cz, @base, o, shopFront);
                    }
                    else PutAll(kit.window, cx, cz, @base, o, between, b, kit);
                    if (shuttered) Put("houseShutters", cx, cz, @base, o, between, b, thick: kit.thick);
                    if (kind == TileKinds.Window)
                        Pane(between ? KitIds.House : front ? KitIds.ShopFront : KitIdOf(building), cx, cz, @base, o, b, false, between);
                }
                else if (broken)
                {
                    // Blown down: a stump of the wall at low cover's height and the wall over the gap left standing, its
                    // lintel (the storey above stands); the rubble on it is the map's.
                    // (One a car was driven into broken nearly to the floor: the car went through it.)
                    float stump = level == 0 && plan.CarInWall(i) ? CarBreach : BreachHeight;
                    for (int n = 0; n < kit.wall.Length; n++) Put(kit.wall[n], cx, cz, @base, o, between, b, thick: kit.thick, tint: n == 0, tall: stump);
                    PutAll(kit.lintel, cx, cz, @base, o, between, b, kit);
                }
                else
                {
                    // A garage door's stretch: the door in its wall, laid once, from the stretch's first tile.
                    if (level == 0 && !between && garages.TryGetValue(i, out var garage))
                    {
                        if (garage.first) Put("houseGarage", garage.x, garage.z, @base, o, between, b, thick: kit.thick, tint: true);
                    }
                    else PutAll(kit.wall, cx, cz, @base, o, between, b, kit);
                    if (level == 0 && !between && building != null && shutters.TryGetValue(i, out float width)) shutter(building, cx, cz, @base, o, width, 0.75f);
                }
                if (kit.top != null && kind == TileKinds.Wall) Put(kit.top, cx, cz, @base + Storey, o, between, b, along: 1 / WireLength, thick: 1);
                // An office's cornice along its top storey, and its plinth along its ground floor but under its doors (phase 3).
                if (!between && kit.crown != null && TopOf(building, level)) Put(kit.crown, cx, cz, @base, o, false, b, thick: kit.thick, tint: true);
                if (!between && kit.plinth != null && level == 0 && !IsDoor(kind)) Put(kit.plinth, cx, cz, @base, o, false, b, thick: kit.thick, tint: true);
                // The open end of a wall gets a post, so its hollow doesn't show (on a perimeter, a gate's column).
                foreach (int k in run == 0 ? new[] { 1, 3 } : new[] { 0, 2 })
                {
                    if (conn[k]) continue;
                    var d = dirs[k];
                    Post(kit, cx + d.X * 0.5f, cz + d.Y * 0.5f, @base, o, between, b);
                    if (building != null && !building.leveled && !between)
                        for (int n = 1; n < building.storeys; n++) Post(kit, cx + d.X * 0.5f, cz + d.Y * 0.5f, @base + n * Storey, o, false, b, true);
                }
                return;
            }
            // A corner or a junction: half a wall from the middle towards each neighbour.
            int arms = 0;
            for (int k = 0; k < 4; k++)
            {
                if (!conn[k]) continue;
                arms++;
                var d = dirs[k];
                int run = d.X != 0 ? 0 : 1;
                // An arm running on into a neighbour's wall (a shop front past a party wall) is the neighbour's.
                int next = OwnerAt((y + d.Y) * w + x + d.X);
                int bb = !loose && next >= 0 && next != b && IsWallLike(At(x + d.X, y + d.Y)) ? next : b;
                var (o, between, other) = Facing(run, d.X, d.Y, bb);
                var kit = between && building?.kind != BuildingKinds.Medical ? Between : bb == b ? outerKit : KitOf(BuildingOf(bb));
                float ax = cx + d.X * 0.25f, az = cz + d.Y * 0.25f;
                // On a corner's outside (its face away from the other arm), a facing runs on round it to meet the other's.
                int bend = Enumerable.Range(0, 4).FirstOrDefault(j => j != k && conn[j]);
                float reach = conn.Count(c => c) == 2 && !between && o.X == -dirs[bend].X && o.Y == -dirs[bend].Y ? kit.facingOut : 0;
                for (int n = 0; n < kit.wall.Length; n++)
                {
                    float r = n > 0 ? reach : 0;
                    Put(kit.wall[n], ax - d.X * r / 2, az - d.Y * r / 2, @base, o, between, bb, along: 0.5f + r, thick: kit.thick, tint: n == 0, tall: broken ? BreachHeight : 1);
                }
                if (broken) for (int n = 0; n < kit.lintel.Length; n++) Put(kit.lintel[n], ax, az, @base, o, between, bb, along: 0.5f, thick: kit.thick, tint: n == 0);
                if (!loose) Storeys(TileKinds.Wall, ax, az, 0, o, between, bb, other, 0.5f, d, reach);
                if (kit.top != null) Put(kit.top, ax, az, @base + Storey, o, between, b, along: 0.5f / WireLength, thick: 1);
                if (!between && kit.crown != null && TopOf(BuildingOf(bb), level)) Put(kit.crown, ax, az, @base, o, false, bb, along: 0.5f, thick: kit.thick, tint: true);
                if (!between && kit.plinth != null && level == 0) Put(kit.plinth, ax, az, @base, o, false, bb, along: 0.5f, thick: kit.thick, tint: true);
            }
            if (arms == 2)
            {
                var (o, between, _) = Facing(0, conn[1] ? 1 : -1, 0, b);
                Post(between && building?.kind != BuildingKinds.Medical ? Between : outerKit, cx, cz, @base, o, between, b);
                if (building != null && !building.leveled && !between)
                    for (int n = 1; n < building.storeys; n++) Post(outerKit, cx, cz, @base + n * Storey, o, false, b, true);
            }
        }

        /// <summary>
        /// The awning over a shop front's window or door (phase 2 of #3: BuildingLooks.ShopAwning), not thickened: its two
        /// half tiles, each the awning's end where the tile on that side is no window or door of the same front, else its
        /// middle, so a run of them is one awning. (A piece's x along the wall is (o.y, −o.x); its −x the model's start.)
        /// </summary>
        void Awning(Building bd, int x, int y, float cx, float cz, float @base, Vec2 o, Func<Building, int, int, bool> shopFront)
        {
            string style = BuildingLooks.ShopAwning(bd);
            if (style == null) return;
            bool On(int tx, int ty)
            {
                string t = At(tx, ty);
                return (IsDoor(t) || t == TileKinds.Window || t == TileKinds.WindowBroken) && OwnerAt(ty * w + tx) == bd.index && shopFront(bd, tx, ty);
            }
            int px = o.Y, py = -o.X;
            Put($"awning{style}{(On(x - px, y - py) ? "MidL" : "EndL")}", cx, cz, @base, o, false, bd.index, thick: 1);
            Put($"awning{style}{(On(x + px, y + py) ? "MidR" : "EndR")}", cx, cz, @base, o, false, bd.index, thick: 1);
        }

        /// <summary>plan.Owner at index i as JS reads it: past either end, none.</summary>
        int OwnerAt(int i) => i >= 0 && i < plan.Owner.Length ? plan.Owner[i] : -1;

        /// <summary>
        /// A siege's watchtowers stand on a block nobody gets under or sees through: on the sides out of the base,
        /// Quarantine's wall goes on under the deck as the wall does; on the sides in, a shed of sheet metal closes it, its
        /// door shut in the middle of the side without the ladder. Each stretch runs on to the wall or the next side, a
        /// column where it turns.
        /// </summary>
        void Towers(IReadOnlyList<Vec2> dirs)
        {
            bool Tower(int x, int y) => InMap(x, y) && plan.Raised[y * w + x] == BuildingPlan.RaisedTower;
            bool Solid(int x, int y) => Tower(x, y) || (InMap(x, y) && IsWallLike(s.Tiles[y * w + x]));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Tower(x, y)) continue;
                    tile = y * w + x;
                    float cx = x + 0.5f, cz = y + 0.5f;
                    foreach (var d in dirs)
                    {
                        if (Tower(x + d.X, y + d.Y)) continue;
                        var r = new Vec2 { X = Math.Abs(d.Y), Y = Math.Abs(d.X) };
                        float a = Solid(x - r.X, y - r.Y) ? -0.5f : 0;
                        float e = Solid(x + r.X, y + r.Y) ? 0.5f : 0;
                        if (e - a <= 0) continue;
                        bool outward = d.X * (cx - w / 2f) + d.Y * (cz - h / 2f) > 0;
                        var kit = outward ? Kits[KitIds.Fence] : Kits[KitIds.ShedGrey];
                        // The ladder leans on this side: on the tile out from this one or from either beside it.
                        bool ladder = new[] { -1, 0, 1 }.Any(n => TileIn(s.Tiles, (y + d.Y + r.Y * n) * w + x + d.X + r.X * n) == TileKinds.Ladder);
                        bool door = !outward && !ladder && Tower(x - r.X, y - r.Y) && Tower(x + r.X, y + r.Y);
                        // The way in is the steel frame alone, open to the deck (no sill: it is walked through).
                        var ids = door ? new[] { "bunkerWindow" } : kit.wall;
                        float c = (a + e) / 2;
                        for (int n = 0; n < ids.Length; n++) Put(ids[n], cx + r.X * c, cz + r.Y * c, 0, d, false, -1, along: e - a, thick: kit.thick, tint: n == 0);
                        // The column where it turns, kept under the deck (Quarantine's is a little taller than a storey).
                        if (a == 0 || e == 0) Put(kit.post, cx, cz, 0, d, false, -1, along: kit.postAlong, thick: kit.thick, square: kit.postSquare, tall: outward ? 0.86f : 1);
                    }
                }
        }

        /// <summary>A motel's sign on its pole, by the road at one end of its car park.</summary>
        void MotelSigns(IReadOnlyList<Vec2> dirs)
        {
            foreach (var b in plan.Buildings)
            {
                if (b.kind != BuildingKinds.Motel || b.front < 0) continue;
                var d = dirs[b.front];
                var along = new Vec2 { X = -d.Y, Y = d.X };
                int end = BuildingPlan.Hash(b.x0, b.y0, 71) < 0.5 ? -1 : 1;
                float midX = (b.x0 + b.x1) / 2f + 0.5f, midZ = (b.y0 + b.y1) / 2f + 0.5f;
                float halfX = (b.x1 - b.x0) / 2f, halfZ = (b.y1 - b.y0) / 2f;
                float reach = Math.Abs(d.X) * halfX + Math.Abs(d.Y) * halfZ + 4.5f;
                float side = Math.Abs(along.X) * halfX + Math.Abs(along.Y) * halfZ - 1;
                float px = midX + d.X * reach + along.X * side * end, pz = midZ + d.Y * reach + along.Y * side * end;
                tile = Mathf.Clamp((int)Math.Floor(pz), 0, h - 1) * w + Mathf.Clamp((int)Math.Floor(px), 0, w - 1);
                Prop("motelSign", Compose(px, b.@base, pz, Mathf.Atan2(d.X, d.Y) + Mathf.PI / 2, Vector3.one), -1);
            }
        }

        /// <summary>
        /// Each kit's pane (BuildingsView's glassGeometry): a quad as wide and tall as its window's opening, at the same
        /// stretch as its walls, facing +z; in 0.10.1's frame, in metres.
        /// </summary>
        public static Mesh PaneMesh(string kitId)
        {
            var kit = Kits.TryGetValue(kitId, out var k) ? k : Kits[KitIds.House];
            float gw = kit.glass[0], gh = kit.glass[1], gy = kit.glass[2], gz = kit.glass[3];
            // A kit's metre is KIT_SCALE of 0.10.1's tiles across and in depth, its storey stretched to the game's (buildings.ts:
            // PlaneGeometry(gw × KIT, gh × KIT × fit), in tiles); in metres here. (Its width and depth were in metres of the
            // kit, a tile short of 0.10.1's: the pane a third narrower and back in the wall, the window's sides pale round it.)
            float fit = Storey * MapSpace.Tile / (kit.storey * Map.Pieces.KitScale);
            float halfW = gw * Map.Pieces.KitScale * MapSpace.Tile / 2, halfH = gh * Map.Pieces.KitScale * fit / 2;
            float y = gy * Map.Pieces.KitScale * fit, z = gz * Map.Pieces.KitScale * kit.thick * MapSpace.Tile;
            var mesh = new Mesh { name = $"Cristal {kitId}" };
            mesh.vertices = new[] { new Vector3(-halfW, y - halfH, z), new Vector3(halfW, y - halfH, z), new Vector3(halfW, y + halfH, z), new Vector3(-halfW, y + halfH, z) };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
