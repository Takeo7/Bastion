using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The map's places in the world, as Arte's MapSpace has them (docs/UNITY-ARTE.md §7: east +x, north +z, a tile 1.5 m,
    /// a storey 2.3 tiles), with what the marks on the map need besides: a tile's corner, and the picked place.
    /// </summary>
    public static class Board
    {
        public const float Tile = MapSpace.Tile;
        public const float Storey = MapSpace.Storey;

        /// <summary>The centre of place `p` at its floor, `above` metres over it.</summary>
        public static Vector3 TileToWorld(GameState s, Pos p, float above = 0) => MapSpace.TileToWorld(s, p, above);

        /// <summary>The north-west corner of tile (x, y) at height `h`.</summary>
        public static Vector3 Corner(int x, int y, float h) => new Vector3(Tile * x, h, -Tile * y);

        /// <summary>The tile under a world point (its level aside).</summary>
        public static Vector2Int TileOf(Vector3 world) => MapSpace.WorldToTile(world);

        /// <summary>The place a ray lands on, on storey `level` (0: the ground, with its heights); false off the map.</summary>
        public static bool RayToPlace(GameState s, Ray ray, int level, out Pos place) => MapSpace.Pick(s, ray, level, out place);
    }
}
