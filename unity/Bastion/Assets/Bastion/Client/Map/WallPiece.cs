using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// One kit piece where a building's wall has it (0.10.1's buildings.ts buildWalls `put`): which piece, the matrix that
    /// places its frame (Pieces.Parts' fit) in 0.10.1's world, in tiles (x east, y up, z south), and how much it is
    /// thickened (applied before it, as `thicken`; a door's leaf swings between the two). And what else the drawing
    /// needs to know of it: the building, the storey whose walls it is, the way its outer face looks, how it is drawn.
    /// </summary>
    public sealed class WallPiece
    {
        public string id;
        /// <summary>buildWalls' `m`: where the piece goes (its turn, `along` and `tall` in its scale), in tiles.</summary>
        public Matrix4x4 matrix;
        /// <summary>buildWalls' thicken: `thick` across (THICK, or the kit's), along too if `square` (a corner post).</summary>
        public float thick = 1.75f;
        public bool square;
        /// <summary>The building it belongs to (BuildingPlan.buildings), −1 none.</summary>
        public int building;
        /// <summary>The storey whose walls are being laid (0 the ground): what it goes with in MapView's storeys.</summary>
        public int level;
        /// <summary>The tile it was laid for (a door leaf's key; what a change of it redoes).</summary>
        public int tile;
        /// <summary>Its storey's floor (tiles).</summary>
        public float floor;
        /// <summary>The way its outer face looks (x, z of DIRS4 in 0.10.1's frame).</summary>
        public Vector2 outward;
        /// <summary>A wall between rooms (both its faces inside).</summary>
        public bool between;
        /// <summary>A façade's upper storey, which goes with the roof in the cutaway.</summary>
        public bool roof;
        /// <summary>In the building's colours (the first of a list of pieces).</summary>
        public bool tint;
        /// <summary>A door leaf swung open; hinged on the other jamb.</summary>
        public bool open;
        public bool otherHinge;
        /// <summary>Its cap (the top of the wall) is drawn.</summary>
        public bool cap;
    }

    /// <summary>A window's pane (buildWalls `pane`): the kit's glass (WallLayout.PaneMesh) at that matrix (tiles).</summary>
    public sealed class WallPane
    {
        public string kit;
        public Matrix4x4 matrix;
        public int building;
        public int level;
        public int tile;
        public bool roof;
        /// <summary>As its wall's: the way it looks, between rooms, its storey's floor (tiles).</summary>
        public Vector2 outward;
        public bool between;
        public float floor;
    }

    /// <summary>
    /// A thing hung on a wall or stood by it (a shed's roller shutter, a shop's sign, a motel's): a model of the map's
    /// catalog (MapArt), set up as 0.10.1's syntyProps did (MapBuilder.Place), at that matrix (tiles).
    /// </summary>
    public sealed class WallProp
    {
        public string id;
        public Matrix4x4 matrix;
        public int building;
        public int level;
        public int tile;
    }
}
