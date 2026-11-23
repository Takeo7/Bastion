#nullable enable
// What burns (GameState.fires; Arturo, 1/11: «algún mapa con cosas incendiadas», «alumbra y quema»): the ground on fire,
// for the rules (Rules.Burn, Paths.ComputeReach) and the client. New in C#, not in the TypeScript engine.
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class Burning
    {
        /// <summary>
        /// Whether `p` is on fire: on the ground, or on a storey's floor since a molotov can land there (N4, Arturo 22/11, cuadro
        /// 120). Fire.Tiles are nodes (Grid.NodeOf): on the ground a tile's own index, as fires saved before have them.
        /// </summary>
        public static bool At(GameState s, Pos p)
        {
            if (s.Fires == null || s.Fires.Count == 0 || !Grid.InBounds(s, p.X, p.Y)) return false;
            var i = Grid.NodeOf(s, p);
            return s.Fires.Any((f) => f.Tiles.Contains(i));
        }

        /// <summary>Every place on fire, by node (on the ground a tile's own index); null with none.</summary>
        public static HashSet<int>? Nodes(GameState s)
        {
            if (s.Fires == null || s.Fires.Count == 0) return null;
            return new HashSet<int>(s.Fires.SelectMany((f) => f.Tiles));
        }

        /// <summary>
        /// Whether the car whose nose is ground tile `anchor` burns: one of the map's own fires from it (Fire.Turns absent,
        /// for ever). Never a molotov's, even one landing on its nose (Arturo, 1/11: it leaves the scenery alone; it took
        /// the boot away, Gameplay 19/11). For the boot (Looting.BootOf) and the client's burning wreck.
        /// </summary>
        public static bool Car(GameState s, int anchor)
        {
            return s.Fires != null && s.Fires.Any((f) => f.Turns == null && f.Source == anchor);
        }
    }
}
