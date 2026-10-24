using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/helpers.ts, the campaign's part (raise, freeSpot, levelTo), and the `run` most campaign test
    /// files have: a command for a seat with who is connected, and the Rng(7) they give it.
    /// </summary>
    public static class CampaignHelpers
    {
        public static readonly int[] Both = { 0, 1 };

        /// <summary>A command, as the test files' `run`: `slot`, `connected` (alone by default) and a fresh Rng(7).</summary>
        public static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null, int seed = 7)
        {
            return Campaign.ExecuteCampaignCommand(c, cmd, new CampaignContext { Slot = slot, Connected = connected ?? new[] { 0 }, Rng = new Rng(seed) });
        }

        /// <summary>As Run, and it must go through (the test files' `run` that throws on `!r.ok`).</summary>
        public static CampaignResult Ok(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null, int seed = 7)
        {
            var r = Run(c, cmd, slot, connected, seed);
            Assert.True(r.Ok, r.Error);
            return r;
        }

        /// <summary>`{ ...base, pieces, nextPiece }`: the same ground and pines (shared, as a spread shares them), other pieces.</summary>
        private static BaseState With(BaseState b, List<BasePiece>? pieces = null, int? nextPiece = null, List<int>? trees = null)
        {
            return new BaseState { Width = b.Width, Height = b.Height, Trees = trees ?? b.Trees, Felled = b.Felled, Pieces = pieces ?? b.Pieces, NextPiece = nextPiece ?? b.NextPiece, Level = b.Level };
        }

        /// <summary>
        /// Raises a building (or any piece) on a campaign's new base (docs/FASE12.md
        /// §11) at `level`: at the free place nearest the caravan touching nothing (no
        /// «junto a» bonus by chance), or wall to wall with `beside` when given; felling
        /// what is in the way, if anything.
        /// </summary>
        public static BasePiece Raise(CampaignState c, string piece, int level = 1, BasePiece? beside = null)
        {
            var b = c.Bastion;
            var spot = FreeSpot(c, piece, beside);
            foreach (var cell in Base.PieceCells(piece, spot.X, spot.Y, 0)) Base.FellTree(b, cell.X, cell.Y);
            var placed = Base.PlacePiece(b, piece, spot.X, spot.Y, 0);
            if (placed.Level != null) placed.Level = level;
            return placed;
        }

        /// <summary>The place nearest the caravan where `piece` fits unturned once the pines are felled: touching nothing, or wall to wall with `beside`.</summary>
        public static Vec2 FreeSpot(CampaignState c, string piece, BasePiece? beside = null)
        {
            var b = c.Bastion;
            var caravan = b.Pieces.First((p) => p.Piece == PieceIds.Caravan);
            var w = Base.Pieces[piece].Size[0];
            var h = Base.Pieces[piece].Size[1];
            var spots = new List<(int X, int Y, double D)>();
            for (var y = Base.BaseEdge; y <= b.Height - Base.BaseEdge - h; y++)
            {
                for (var x = Base.BaseEdge; x <= b.Width - Base.BaseEdge - w; x++) spots.Add((x, y, Js.Hypot(x + w / 2.0 - caravan.X - 2, y + h / 2.0 - caravan.Y - 1)));
            }
            Js.Sort(spots, (p, q) => p.D != q.D ? p.D - q.D : p.Y != q.Y ? p.Y - q.Y : (double)(p.X - q.X));
            bool Touches(int x, int y)
            {
                var p = Base.PlacePiece(With(b, new List<BasePiece>(), 0), piece, x, y, 0);
                var next = Base.BesidePieces(With(b, b.Pieces.Append(p).ToList()), p);
                return beside != null ? next.Contains(beside) : next.Count == 0;
            }
            var found = spots.First((s) => Base.PlaceBlocker(With(b, trees: new List<int>()), piece, s.X, s.Y, 0) == null && Touches(s.X, s.Y));
            return new Vec2 { X = found.X, Y = found.Y };
        }

        /// <summary>
        /// The Bastion straight up to `level`, every level bought as the players buy it
        /// (the `levelUp` order, alone at the base), without spending what the test
        /// set: the suministros and the missions flown are put back after.
        /// </summary>
        public static void LevelTo(CampaignState c, int level, Rng? rng = null)
        {
            rng ??= new Rng(1);
            var credits = c.Credits;
            var missionsFlown = c.MissionsFlown;
            var slot = c.Players[0];
            while (Progression.BastionLevel(c) < Math.Min(level, Progression.MaxLevel))
            {
                c.Credits = 1000000;
                c.MissionsFlown = 1000;
                var r = Campaign.ExecuteCampaignCommand(c, new LevelUpCommand(), new CampaignContext { Slot = slot, Connected = new[] { slot }, Rng = rng });
                if (!r.Ok) throw new InvalidOperationException(r.Error);
            }
            c.Credits = credits;
            c.MissionsFlown = missionsFlown;
        }
    }
}
