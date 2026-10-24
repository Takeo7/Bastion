#nullable enable
// types.ts: where things are on the map.

namespace Bastion.Engine
{
    public class Vec2
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>
    /// A place a unit can be (docs/FASE6-PLAN.md): a tile and its level, 0 the
    /// ground (`tiles` and `elev`), 1–3 a building's storeys (`GameState.levels`).
    /// On the wire and in older saves `z` may be missing: it means 0.
    /// </summary>
    public sealed class Pos : Vec2
    {
        public int Z { get; set; }
    }

    // A place as the client or the wire may give it (a hovered tile, a command):
    // without `z`, on the ground. The state always stores a Pos.
    //   export type Place = Vec2 & { z?: number };
    // In C# a Place is a Pos: a missing `z` reads as 0, the ground, which is what it means (grid.ts toPos). So the
    // functions that take a Place in TypeScript take a Pos, and a Pos needs no conversion to be passed as one.
}
