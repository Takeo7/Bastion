#nullable enable
// Where a building's windows go (playtest note 17; Arturo, 7/11): one set of columns a side, the same on every storey.
// Mapgen lays the windows of the ground floor and of the storeys above in them (Rooms.cs Windows, Upstairs.cs
// UpperWindows), and keeps the walls between rooms off them; the client draws by them the windows of the storeys it
// draws as a façade (WallLayout.Storeys), so that those line up with the ones below.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>
    /// The window columns of a building's sides. On each side, columns `every` apart (Every), centred on it, never nearer
    /// a corner than its third tile (Margin); where what is left over is odd, the tile over goes to the east or south end.
    /// Every storey has its windows in them: the ground floor but at a door and beside it (ByOpening) and where outside is
    /// a wall; any storey but where a wall meets the outer one from inside, or another building stands outside.
    /// <para>All in the map as played (GameState): a building by its tiles, walls included (x0, y0 to x1, y1); a side by
    /// its DIRS4 index (0 north, 1 east, 2 south, 3 west: the way out of it); `u`, a tile's place along the side counted
    /// from its west end on sides 0 and 2 (x - x0) and from its north end on sides 1 and 3 (y - y0), so that 0 and n - 1
    /// are the corners of a side of n tiles.</para>
    /// </summary>
    public static class WindowColumns
    {
        /// <summary>From one window to the next on a home: a house, a block of flats, a motel (GameState.homes).</summary>
        public const int HomeEvery = 3;
        /// <summary>From one window to the next on any other building: shops, offices, clinics, halls.</summary>
        public const int OtherEvery = 2;
        /// <summary>The nearest a window comes to a corner: the third tile of the side (u = 2 and n - 3).</summary>
        public const int Margin = 2;

        /// <summary>How far apart the columns of a building are, on all its sides and storeys: `home`, whether it is a home.</summary>
        public static int Every(bool home) => home ? HomeEvery : OtherEvery;

        /// <summary>The columns along a side of `n` tiles (corners included), `every` apart, from west or north: their `u`.</summary>
        public static List<int> Along(int n, int every) => Along(n, every, false);

        /// <summary>Whether `u` is a column of a side of `n` tiles, `every` apart.</summary>
        public static bool IsColumn(int n, int u, int every) => IsColumn(n, u, every, false);

        /// <summary>
        /// Whether tile (x, y) of side `side` of the building whose tiles go from (x0, y0) to (x1, y1), walls included, is a
        /// column, `every` apart. Only where along the side the tile is counts: (x, y) is meant to be on that side.
        /// </summary>
        public static bool IsColumn(int x0, int y0, int x1, int y1, int side, int x, int y, int every)
        {
            var across = side % 2 == 0;
            return IsColumn(across ? x1 - x0 + 1 : y1 - y0 + 1, across ? x - x0 : y - y0, every);
        }

        /// <summary>
        /// Whether the ground floor has no window in a column: the wall tile there (`at`) is an opening (a door, open or
        /// boarded up, a way through), or one beside it along the wall is (`before`, `after`). The storeys above have no
        /// such exception: a window over a door's column too.
        /// </summary>
        public static bool ByOpening(string? at, string? before, string? after) => !Solid(at) || !Solid(before) || !Solid(after);

        private static bool Solid(string? kind) => kind == TileKinds.Wall || kind == TileKinds.Window || kind == TileKinds.WindowBroken;

        /// <summary>
        /// Along(), for Mapgen's canvas: `backwards`, the side's u runs east to west or south to north in the map as played
        /// (the canvas is mirrored or transposed at the end), so the tile over goes to its start.
        /// </summary>
        internal static List<int> Along(int n, int every, bool backwards)
        {
            var out_ = new List<int>();
            var first = First(n, every, backwards);
            if (first < 0) return out_;
            for (var u = first; u <= n - 1 - Margin; u += every) out_.Add(u);
            return out_;
        }

        /// <summary>IsColumn(), for Mapgen's canvas (Along's `backwards`).</summary>
        internal static bool IsColumn(int n, int u, int every, bool backwards)
        {
            var first = First(n, every, backwards);
            return first >= 0 && u >= first && u <= n - 1 - Margin && (u - first) % every == 0;
        }

        /// <summary>The first column of a side of `n` tiles, or -1 where none fits.</summary>
        private static int First(int n, int every, bool backwards)
        {
            // From the first place a window may take to the last.
            var room = n - 1 - 2 * Margin;
            if (room < 0 || every < 1) return -1;
            var spare = room % every;
            return Margin + (backwards ? spare - spare / 2 : spare / 2);
        }
    }
}
