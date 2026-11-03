using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// A window barricaded from inside (mapView.ts barricadeWindow; playtest 21/10 #20, Ana: furniture piled up so the dead
    /// don't get in), on the tile in front of it: planks nailed across the window frame, and against them what the room
    /// had, a wardrobe pushed up to them, a table on its side, its top to the window, or a mattress stood up leaning on
    /// them; a box or two at their foot. From the street, the planks behind the glass. And a doorway boarded up from
    /// outside (BoardDoor). 0.10.1's matrices, in tiles.
    /// </summary>
    public sealed partial class MapBuilder
    {
        /// <summary>mapView.ts WINDOW_FACE, WINDOW_SILL: how far the wall's inner face is from the middle of the tile inside it, how high the sill is (tiles).</summary>
        const float WindowFace = 1 - 0.1f * 1.75f * (1 / 1.5f) * 1.2f, WindowSill = 0.62f;

        static readonly string[] Boards = { "boardsCross", "boardsMany", "boardsRails" };

        /// <summary>
        /// Tile i of storey k, (x, y), barricaded against the window on its side `facing` (0.10.1's DIRS4); `loose`, made
        /// again there under it, owned by no tile and joined with nothing (LooseBarricade).
        /// </summary>
        void BarricadeWindow(int k, int i, int x, int y, float floor, int facing, Transform loose = null)
        {
            var d = Bastion.Engine.Grid.Dirs4[facing];
            // This tile's frame: z out to the window, x along the wall.
            var frame = Matrix4x4.TRS(new Vector3(x + 0.5f, floor / MapSpace.Tile, y + 0.5f), Quaternion.AngleAxis(Mathf.Atan2(d.X, d.Y) * Mathf.Rad2Deg, Vector3.up), Vector3.one);
            float R(int n) => MapArt.Hash(x, y, 70 + n);
            void Put(string id, Matrix4x4 m)
            {
                var o = PlaceGame(loose != null ? loose : props[k], id, frame * m);
                if (loose == null) Own(o, k, new[] { i });
            }
            Matrix4x4 T(float tx, float ty, float tz) => Matrix4x4.Translate(new Vector3(tx, ty, tz));
            Matrix4x4 Turn(Vector3 axis, float radians) => Matrix4x4.Rotate(Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis));
            float face = WindowFace - 0.05f;
            Put(Boards[Mathf.Min(2, Mathf.FloorToInt(R(0) * 3))], T((R(1) - 0.5f) * 0.06f, WindowSill, face) * Turn(Vector3.forward, (R(2) - 0.5f) * 0.06f));
            float pile = R(3), against = face - 0.05f;
            if (pile < 0.4f)
                // The wardrobe, its back to the window, its doors to the room.
                Put("wardrobe", T((R(4) - 0.5f) * 0.12f, 0, against - 0.23f) * Turn(Vector3.up, (R(5) - 0.5f) * 0.12f));
            else if (pile < 0.7f)
                // The table tipped onto its edge, its top against the planks, its legs to the room.
                Put("table", T((R(4) - 0.5f) * 0.1f, 0.43f, against - 0.58f) * Turn(Vector3.right, Mathf.PI / 2));
            else
            {
                // The mattress stood on its end, leaning on the planks.
                const float lean = 0.2f;
                Put("mattress", T((R(4) - 0.5f) * 0.2f, 0, against - 1.28f * Mathf.Sin(lean) - 0.02f) * Turn(Vector3.right, lean) * T(0, 0.64f, 0) * Turn(Vector3.right, -Mathf.PI / 2));
            }
            // A box or two at their foot, or none.
            int boxes = Mathf.FloorToInt(R(6) * 3);
            for (int n = 0; n < boxes; n++)
            {
                float side = (n > 0 ? -1 : 1) * (0.28f + R(7 + n) * 0.1f);
                Put(n > 0 ? "cardboard2" : "cardboard", T(side, 0, against - 0.62f - R(9 + n) * 0.12f) * Turn(Vector3.up, R(11 + n) * Mathf.PI));
            }
        }

        /// <summary>
        /// How far a doorway's planks stand out from the middle of its tile (the wall's outer face and half a plank, tiles),
        /// and how high the lowest and the highest go across it.
        /// </summary>
        const float DoorFace = (float)BuildingSurfaces.WallHalf + 0.1f, DoorLow = 0.3f, DoorHigh = 1.35f;
        /// <summary>How high a door's leaf stands in the cutaway (WallsView's CutLow less LeafCut, tiles): planks under it stay.</summary>
        const float DoorCut = 0.65f;

        /// <summary>
        /// A doorway boarded up from outside (DecorKinds.DoorBoards; Arturo, 2/11: the crowbar takes them off), on its own
        /// tile: the door shut behind (the walls take the tile for one: Doors), and planks nailed across it from wall to
        /// wall, each a little askew, and one slanting over them. Facing out of the building (0.10.1's DIRS4); in the
        /// cutaway they go with the wall but those as low as the leaf left there (DoorCut). `loose`, as BarricadeWindow's.
        /// </summary>
        void BoardDoor(int k, int i, int x, int y, float floor, int facing, Transform loose = null)
        {
            var d = Bastion.Engine.Grid.Dirs4[facing];
            // This tile's frame: z out of the building, x along the wall.
            var frame = Matrix4x4.TRS(new Vector3(x + 0.5f, floor / MapSpace.Tile, y + 0.5f), Quaternion.AngleAxis(Mathf.Atan2(d.X, d.Y) * Mathf.Rad2Deg, Vector3.up), Vector3.one);
            float R(int n) => MapArt.Hash(x, y, 80 + n);
            int b = plan != null && i < plan.Owner.Length ? plan.Owner[i] : -1;
            void Put(Matrix4x4 m, bool low = false)
            {
                var o = PlaceGame(loose != null ? loose : props[k], "doorPlank", frame * m);
                if (o == null) return;
                if (loose == null) Own(o, k, new[] { i });
                // (The loose ones too, as they fall: they go with the wall.)
                if (b >= 0 && !low) Cutaway.Current?.TrackFixture(o, b, k, new Vector2(d.X, d.Y));
            }
            Matrix4x4 T(float tx, float ty, float tz) => Matrix4x4.Translate(new Vector3(tx, ty, tz));
            Matrix4x4 Turn(Vector3 axis, float radians) => Matrix4x4.Rotate(Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis));
            // The plank lies flat along x (its model turned): stood up on its edge, its face out, its middle on the place.
            var upright = Turn(Vector3.right, Mathf.PI / 2);
            int planks = R(0) < 0.5f ? 4 : 5;
            for (int n = 0; n < planks; n++)
            {
                float height = DoorLow + (DoorHigh - DoorLow) * n / (planks - 1) + (R(1 + n) - 0.5f) * 0.08f;
                Put(T((R(7 + n) - 0.5f) * 0.1f, height, DoorFace + n * 0.004f) * Turn(Vector3.forward, (R(13 + n) - 0.5f) * 0.18f) * upright, height + 0.1f < DoorCut);
            }
            // The slanting one, longer, nailed on top of them.
            float slant = (R(20) < 0.5f ? 1 : -1) * (0.8f + R(21) * 0.15f);
            Put(T((R(22) - 0.5f) * 0.1f, (DoorLow + DoorHigh) / 2, DoorFace + 0.035f) * Turn(Vector3.forward, slant) * Matrix4x4.Scale(new Vector3(1.4f, 1, 1)) * upright);
        }

        /// <summary>
        /// The map as the walls and the rooms are to see it: a boarded doorway (DoorBoards, `high` in the rules) the door it
        /// is, shut, behind its planks (BoardDoor); and pulled off, a door in the rules too, so the walls don't change. The
        /// state itself if it has none, else a shallow copy with its own tiles (as BasePieces.TerrainOf).
        /// </summary>
        static GameState Doors(GameState s)
        {
            if (s?.Decor == null) return s;
            List<string> tiles = null;
            for (int i = 0; i < s.Tiles.Count && i < s.Decor.Count; i++)
            {
                if (s.Tiles[i] != TileKinds.High || Bastion.Engine.Decor.DecorOf(s.Decor[i])?.Kind != DecorKinds.DoorBoards) continue;
                tiles ??= new List<string>(s.Tiles);
                tiles[i] = TileKinds.Door;
            }
            if (tiles == null) return s;
            var @out = new GameState();
            foreach (var p in BasePieces.StateProperties) p.SetValue(@out, p.GetValue(s));
            @out.Tiles = tiles;
            return @out;
        }

        /// <summary>
        /// The barricade by the window on `tile`, or the planks over its doorway, made again apart, to be pulled down
        /// (MapView.Topple; Arturo's A11 of 2/11): the joined ones (Still) can't move, so they are hidden and these stand in
        /// their place till the map is made again. Null if there are none.
        /// </summary>
        public Transform LooseBarricade(Pos tile)
        {
            if (tile == null) return null;
            var decor = tile.Z == 0 ? s.Decor : s.Levels != null && tile.Z - 1 < s.Levels.Count ? s.Levels[tile.Z - 1].Decor : null;
            int i = tile.Y * s.Width + tile.X;
            var look = decor != null && i < decor.Count ? Bastion.Engine.Decor.DecorOf(decor[i]) : null;
            if (look == null || (look.Kind != "windowBarricade" && look.Kind != DecorKinds.DoorBoards)) return null;
            var loose = new GameObject("tablas sueltas").transform;
            loose.SetParent(root, false);
            if (look.Kind == DecorKinds.DoorBoards) BoardDoor(tile.Z, i, tile.X, tile.Y, tile.Z * MapSpace.Storey, look.Facing, loose);
            else BarricadeWindow(tile.Z, i, tile.X, tile.Y, tile.Z * MapSpace.Storey, look.Facing, loose);
            foreach (var o in On(tile))
                foreach (var r in o.GetComponentsInChildren<Renderer>()) r.enabled = false;
            return loose;
        }
    }
}
