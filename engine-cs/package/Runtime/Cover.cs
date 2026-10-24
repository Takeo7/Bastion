#nullable enable
// cover.ts: the cover a unit gets on each side, and against an attacker.
using System;
using System.Linq;

namespace Bastion.Engine
{
    public static class Cover
    {
        /// <summary>
        /// Math.cos((COVER_MAX_ANGLE_DEG * Math.PI) / 180), as V8 gives it: written out, so every platform's C library
        /// has the same bits (the golden tests check it against TypeScript's).
        /// </summary>
        public const double CoverMinDot = 0.3420201433256688;

        /// <summary>
        /// Cover offered on each side of place `p`, in N, E, S, W order. Heights count:
        /// a rooftop edge gives nothing to whoever stands on the roof, while the wall of
        /// a raised block is full cover for whoever stands at its foot; upstairs, what
        /// stands on that storey (grid.ts coverAt). Without a level, on the ground.
        /// </summary>
        public static int[] CoverSides(GameState s, Pos at)
        {
            var p = Grid.ToPos(at);
            return Grid.Dirs4.Select((d) => Grid.CoverAt(s, p, new Vec2 { X = p.X + d.X, Y = p.Y + d.Y })).ToArray();
        }

        public static bool HasAnyCover(GameState s, Pos p)
        {
            return CoverSides(s, p).Any((c) => c > 0);
        }

        /// <summary>
        /// Best cover a unit at `target` gets against an attacker at `attacker`.
        /// 0 means the target is flanked.
        /// </summary>
        public static int CoverAgainst(GameState s, Pos target, Vec2 attacker)
        {
            var vx = attacker.X - target.X;
            var vy = attacker.Y - target.Y;
            var len = Js.Hypot(vx, vy);
            if (len == 0) return 0;
            var best = 0;
            var sides = CoverSides(s, target);
            for (var i = 0; i < sides.Length; i++)
            {
                var level = sides[i];
                if (level <= best) continue;
                var d = Grid.Dirs4[i];
                if ((double)(vx * d.X + vy * d.Y) / len >= CoverMinDot) best = level;
            }
            return best;
        }
    }
}
