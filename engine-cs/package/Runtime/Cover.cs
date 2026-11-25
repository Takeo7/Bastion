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
        /// <remarks>`byKind`: what the tiles' kinds give (Grid.CoverAt): what the dead see behind it (Los.CoverShare, cuadro 157, P6).</remarks>
        public static int[] CoverSides(GameState s, Pos at, bool byKind = false)
        {
            var p = Grid.ToPos(at);
            return Grid.Dirs4.Select((d) => Grid.CoverAt(s, p, new Vec2 { X = p.X + d.X, Y = p.Y + d.Y }, byKind)).ToArray();
        }

        public static bool HasAnyCover(GameState s, Pos p)
        {
            return CoverSides(s, p).Any((c) => c > 0);
        }

        /// <summary>
        /// Best cover a unit at `target` gets against an attacker at `attacker`.
        /// 0 means the target is flanked.
        /// </summary>
        /// <remarks>`byKind`: by the tiles' kinds, all furniture as tall as its kind (CoverSides).</remarks>
        public static int CoverAgainst(GameState s, Pos target, Vec2 attacker, bool byKind = false)
        {
            return Against(s, target, attacker, byKind).Level;
        }

        /// <summary>
        /// The tile beside `target` whose cover counts against an attacker at `attacker` (CoverAgainst's best side: the first
        /// of N, E, S, W at that level), on the target's storey; null when flanked (cuadro 157, P5: «que se vea»).
        /// </summary>
        public static Pos? CoverTileAgainst(GameState s, Pos target, Vec2 attacker)
        {
            var (level, side) = Against(s, target, attacker);
            if (level == 0) return null;
            var d = Grid.Dirs4[side];
            return new Pos { X = target.X + d.X, Y = target.Y + d.Y, Z = target.Z };
        }

        private static (int Level, int Side) Against(GameState s, Pos target, Vec2 attacker, bool byKind = false)
        {
            var vx = attacker.X - target.X;
            var vy = attacker.Y - target.Y;
            var len = Js.Hypot(vx, vy);
            if (len == 0) return (0, -1);
            var best = 0;
            var from = -1;
            var sides = CoverSides(s, target, byKind);
            for (var i = 0; i < sides.Length; i++)
            {
                var level = sides[i];
                if (level <= best) continue;
                var d = Grid.Dirs4[i];
                if ((double)(vx * d.X + vy * d.Y) / len >= CoverMinDot)
                {
                    best = level;
                    from = i;
                }
            }
            return (best, from);
        }
    }
}
