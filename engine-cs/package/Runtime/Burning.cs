#nullable enable
// What burns (GameState.fires; Arturo, 1/11: «algún mapa con cosas incendiadas», «alumbra y quema»): the ground on fire,
// for the rules (Rules.Burn, Paths.ComputeReach) and the client. New in C#, not in the TypeScript engine.
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class Burning
    {
        /// <summary>Whether `p` is ground on fire (fires burn on the ground only).</summary>
        public static bool At(GameState s, Pos p)
        {
            if (s.Fires == null || s.Fires.Count == 0 || p.Z != 0 || !Grid.InBounds(s, p.X, p.Y)) return false;
            var i = Grid.TileIndex(s, p.X, p.Y);
            return s.Fires.Any((f) => f.Tiles.Contains(i));
        }

        /// <summary>Every tile on fire, by tile index (on the ground a node's own); null with none.</summary>
        public static HashSet<int>? Nodes(GameState s)
        {
            if (s.Fires == null || s.Fires.Count == 0) return null;
            return new HashSet<int>(s.Fires.SelectMany((f) => f.Tiles));
        }
    }
}
