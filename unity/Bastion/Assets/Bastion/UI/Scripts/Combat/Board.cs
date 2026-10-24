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

        /// <summary>A way's point on place `p` (a path, a plan): its floor's, or on a flight of stairs the treads over its middle.</summary>
        public static Vector3 WayPoint(GameState s, Pos p, float above = 0) => MapSpace.TileToWorld(s, p, above + StairLift(s, p));

        /// <summary>How high over its floor one stands on place `p`: on a flight of stairs, on its treads (Arte's, as the figures stand); else 0.</summary>
        public static float StairLift(GameState s, Pos p) => MapSpace.StairLift(s, p);

        /// <summary>The north-west corner of tile (x, y) at height `h`.</summary>
        public static Vector3 Corner(int x, int y, float h) => new Vector3(Tile * x, h, -Tile * y);

        /// <summary>The tile under a world point (its level aside).</summary>
        public static Vector2Int TileOf(Vector3 world) => MapSpace.WorldToTile(world);

        /// <summary>The place a ray lands on, on storey `level` (0: the ground, with its heights); false off the map.</summary>
        public static bool RayToPlace(GameState s, Ray ray, int level, out Pos place) => MapSpace.Pick(s, ray, level, out place);
    }
}
