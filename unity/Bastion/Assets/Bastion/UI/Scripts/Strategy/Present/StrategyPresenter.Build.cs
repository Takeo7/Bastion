using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Building on the clearing, its rules and its words by the pointer (buildMode.ts placeBlockerFor, lineBlockerFor,
    /// fellBlockerFor, placeLine, lineLine; clearing.ts pieceLine; screen.ts's postBlocker and postHint): the place first
    /// (what the pointer shows), then the Bastion's level, only one, the cost; with the partner here, a vote waiting.
    /// </summary>
    public sealed partial class StrategyPresenter
    {
        /// <summary>Whether `piece` goes down in a line by dragging (walls, sandbags, wire…).</summary>
        public static bool Lineable(string piece) => piece != null && LinePieces.Contains(piece);

        /// <summary>With the partner here everything on the base goes to a vote (#55): one's own goes as a proposal.</summary>
        public bool VotingNow => State != null && Voting;

        /// <summary>Why `piece` can't go at (x, y) turned `rot` (its corner tile, as the engine's): the place, then the rest.</summary>
        public string PlaceBlockerFor(string piece, int x, int y, int rot)
        {
            var c = State;
            if (c == null) return "Un momento.";
            return Base.PlaceBlocker(c.Bastion, piece, x, y, rot) ?? TakeBlocker(c, piece);
        }

        /// <summary>Why the line can't go down (all or nothing): a tile first, then a vote waiting, the level, what they cost together.</summary>
        public string LineBlockerFor(string piece, IReadOnlyList<Vec2> cells, int rot)
        {
            var c = State;
            if (c == null) return "Un momento.";
            foreach (var cell in cells)
            {
                var why = Base.PlaceBlocker(c.Bastion, piece, cell.X, cell.Y, rot);
                if (why != null) return why;
            }
            if (Voting && c.Proposal != null) return Pending;
            var def = Base.Pieces[piece];
            if (def.Level > Progression.BastionLevel(c.Level)) return Lore.NeedsLevelLine(def.Level);
            var (supplies, parts) = Short(c, def.Cost.Supplies * cells.Count, (def.Cost.Parts ?? 0) * cells.Count);
            return supplies > 0 || parts > 0 ? Lore.BuildShortLine(supplies, parts) + "." : null;
        }

        /// <summary>Why `trees` trees can't be felled now (#41: each costs Base.FellCost), or null; a vote waiting (#55).</summary>
        public string FellBlockerFor(int trees)
        {
            var c = State;
            if (c == null) return "Un momento.";
            if (Voting && c.Proposal != null) return Pending;
            var (supplies, _) = Short(c, Base.FellCost * trees, 0);
            return supplies > 0 ? Lore.BuildShortLine(supplies) + "." : null;
        }

        /// <summary>By the pointer with a piece in hand: what it costs and that it is proposed, or why it doesn't fit.</summary>
        public string PlaceHintFor(string piece, string blocked)
        {
            var def = Base.Pieces[piece];
            return Lore.PlaceHint(piece, def.Cost.Supplies, def.Cost.Parts ?? 0, VotingNow, blocked);
        }

        /// <summary>By the pointer while dragging a line: «Muro × 6 · Cuesta 36 §», or why not.</summary>
        public string LineHintFor(string piece, int n, string blocked)
        {
            var def = Base.Pieces[piece];
            var name = $"{Lore.PieceTexts.All[piece].Name} × {n}";
            if (!string.IsNullOrEmpty(blocked)) return $"{name} · {blocked}";
            return $"{name} · {Lore.BuildCostLine(def.Cost.Supplies * n, (def.Cost.Parts ?? 0) * n)}{(VotingNow ? $" · {Lore.BuildText.Vote}" : "")}";
        }

        /// <summary>By the pointer felling: how many and what they cost, or why not.</summary>
        public string FellHintFor(int trees, string blocked)
        {
            if (trees == 0) return Lore.BuildText.NoTrees;
            return string.IsNullOrEmpty(blocked) ? Lore.FellHint(trees) : $"{Lore.FellHint(trees)} · {blocked}";
        }

        /// <summary>A piece under the pointer: its name and how it is (out of use, hurt), or its level (clearing.ts pieceLine).</summary>
        public static string PieceLine(BasePiece p)
        {
            var def = Base.Pieces[p.Piece];
            string state = null;
            if (def.Kind == PieceKinds.Building && !Campaign.BuildingWorks(p)) state = "broken";
            else if (def.Charges != null && (p.Charges ?? 0) == 0) state = p.Piece == "turret" ? "silent" : "spent";
            var level = def.Kind == PieceKinds.Building && p.Piece != PieceIds.Caravan && (p.Level ?? 1) > 1 ? p.Level.Value : 0;
            return Lore.PieceHint(p.Piece, p.Hp < def.Hp ? (p.Hp, def.Hp) : ((int, int)?)null, state, level);
        }

        /// <summary>Why `soldier` can't start the siege at (x, y) (#58), or null.</summary>
        public string PostBlockerFor(string soldier, int x, int y)
        {
            var c = State;
            if (c == null) return "Un momento.";
            return Campaign.PostBlocker(c, Slot, soldier, new Vec2 { X = x, Y = y });
        }

        /// <summary>By the pointer with a defender in hand: their name, and why not.</summary>
        public string PostHintFor(string soldier, string blocked)
        {
            var s = State?.Soldiers.FirstOrDefault(x => x.Id == soldier);
            var name = s != null ? Appearances.DisplayName(s.Name, s.Nickname) : "";
            return string.IsNullOrEmpty(blocked) ? name : $"{name} · {blocked}";
        }

        /// <summary>Why `piece` can't be taken up at all, with the vote (buildMode.ts pieceBlocker): a proposal waiting first.</summary>
        string TakeBlocker(CampaignState c, string piece)
        {
            if (Voting && c.Proposal != null) return Pending;
            return PieceBlocker(c, piece);
        }

        /// <summary>A player's name and colour, for the partner's piece in hand and the siege's posts.</summary>
        public string NameOf(int slot) => PlayerName(slot);
        public string ColorOf(int slot) => PlayerColor(slot);
    }
}
