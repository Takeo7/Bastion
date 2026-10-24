using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>What the Asentamiento has in hand, which only this screen knows (screen.ts: clearing.currentTool, piece, dismantling).</summary>
    public sealed class SettlementPick
    {
        /// <summary>The palette's piece in hand; null, none.</summary>
        public string InHand;
        /// <summary>Talar in hand.</summary>
        public bool Felling;
        /// <summary>The piece picked on the clearing (BasePiece.Id); null, none.</summary>
        public string Piece;
        /// <summary>«Desmontar» pressed once: the card asks.</summary>
        public bool Dismantling;
    }

    public sealed partial class StrategyPresenter
    {
        /// <summary>A proposal waiting: nothing else goes down meanwhile (buildMode.ts PENDING).</summary>
        const string Pending = "Ya hay una propuesta pendiente.";

        /// <summary>What goes down in a line by dragging (buildMode.ts LINE_PIECES).</summary>
        static readonly HashSet<string> LinePieces = new HashSet<string> { "palisade", "wall", "sandbags", "logs", "wire" };

        /// <summary>
        /// The Asentamiento (renderFacilities): Talar and the palette on the left (buildMode.ts palette), on the right the
        /// Bastion in figures (bastionStats), «Andar por la base» and the card of the piece picked (pieceCard).
        /// </summary>
        public SettlementView Settlement(SettlementPick pick)
        {
            pick = pick ?? new SettlementPick();
            var c = State;
            Fresh.Load(c);
            var level = Level;
            var v = new SettlementView { StatsHead = "Bastión", Walk = "Andar por la base" };
            v.Fell = new FellView { Label = Lore.BuildText.Fell, Supplies = Base.FellCost, Per = "por árbol", On = pick.Felling, Keys = Lore.BuildText.Felling, Tip = Lore.BuildText.Felling };
            if (Voting && c.Proposal != null) v.Pending = $"{Pending} {(c.Proposal.By == Slot ? $"Espera a que {Partner.Name} conteste." : "Contesta arriba.")}";
            foreach (var (title, kinds) in new[] { (Lore.BuildText.Tabs.Building, new[] { PieceKinds.Building }), (Lore.BuildText.Tabs.Defence, new[] { PieceKinds.Defence, PieceKinds.Trap }) })
            {
                var g = new PaletteGroupView { Title = title };
                // Only what the Bastion's level allows (playtest 20/10 #13); the caravan is never built.
                foreach (var id in Base.PieceOrder.Where(id => id != PieceIds.Caravan && Base.Pieces[id].Level <= level && kinds.Contains(Base.Pieces[id].Kind)))
                {
                    var def = Base.Pieces[id];
                    var built = def.Unique == true && c.Bastion.Pieces.Any(p => p.Piece == id);
                    var why = built ? null : PieceBlocker(c, id);
                    var line = Lore.PieceTexts.All[id].Line;
                    g.Tiles.Add(new PieceTileView
                    {
                        Id = id, Name = Lore.PieceTexts.All[id].Name, Supplies = def.Cost.Supplies, Parts = def.Cost.Parts ?? 0,
                        Built = built, Short = why != null, On = pick.InHand == id, Tip = why != null ? line + "\n" + why : line,
                        New = IsNew("piece:" + id), NewKey = "piece:" + id,
                    });
                    if (pick.InHand == id) g.Keys = Lore.HoldingLine(id) + (LinePieces.Contains(id) ? " Arrastra para poner una fila." : "");
                }
                v.Groups.Add(g);
            }
            Stats(c, v.Stats);
            var picked = pick.Piece != null ? c.Bastion.Pieces.FirstOrDefault(p => p.Id == pick.Piece) : null;
            if (picked != null) v.Card = Card(c, picked, pick.Dismantling);
            return v;
        }

        /// <summary>
        /// Why `piece` can't be taken up at all (buildMode.ts pieceBlocker, without the vote: the palette's): the level,
        /// only one, what it costs.
        /// </summary>
        static string PieceBlocker(CampaignState c, string piece)
        {
            var def = Base.Pieces[piece];
            if (def.Level > Progression.BastionLevel(c.Level)) return Lore.NeedsLevelLine(def.Level);
            if (def.Unique == true && c.Bastion.Pieces.Any(p => p.Piece == piece)) return "Ya está en la base.";
            var (supplies, parts) = Short(c, def.Cost.Supplies, def.Cost.Parts ?? 0);
            return supplies > 0 || parts > 0 ? Lore.BuildShortLine(supplies, parts) + "." : null;
        }

        /// <summary>What the stores lack of a cost (buildMode.ts short): suministros and piezas.</summary>
        static (int supplies, int parts) Short(CampaignState c, int supplies, int parts) =>
            (System.Math.Max(0, supplies - c.Credits), System.Math.Max(0, parts - (c.Parts ?? 0)));

        /// <summary>
        /// The Bastion in figures (bastionStats; Ana, 21/10 #38): the beds and the rations first, then what each kind of
        /// building adds, only those there are.
        /// </summary>
        void Stats(CampaignState c, List<BastionStatView> stats)
        {
            var level = Level;
            var alive = c.Soldiers.Count(s => s.Alive);
            var beds = Campaign.Beds(c);
            stats.Add(new BastionStatView { Icon = "base:beds", IconColor = "#b2c3d0", Value = $"{alive}/{beds}", Label = "Camas", Tone = alive >= beds ? "warn" : null });
            var eat = Campaign.RationsPerMission(c);
            var grow = Campaign.GardenPerMission(c);
            stats.Add(new BastionStatView { Icon = "store:food", IconColor = "#eab96c", Value = $"−{eat}", Label = "Raciones por misión", Tone = (c.Food ?? 0) + grow < eat ? "bad" : null });
            if (grow > 0) stats.Add(new BastionStatView { Icon = "base:garden", IconColor = "#8fd05a", Value = $"+{grow}", Label = "De los Huertos", Tone = "good" });
            stats.Add(new BastionStatView { Icon = "store:credits", Value = $"+{Campaign.ConvoyIncome(c)}", Label = $"Convoy {ConvoyWhen(c)}" });
            if (Campaign.FacilityCount(c, "infirmary") > 0) stats.Add(new BastionStatView { Icon = "store:medicines", Value = $"×{Campaign.HealRate(c)}", Label = "Curación" });
            var garage = Campaign.GarageCut(c).Wounds;
            if (garage != 0) stats.Add(new BastionStatView { Icon = "map:lata", Value = $"−{garage}", Label = "Días de baja" });
            var aim = Campaign.RangeAim(c);
            if (aim != 0) stats.Add(new BastionStatView { Icon = "stat:aim", Value = $"+{aim}", Label = "Puntería" });
            if (Campaign.FacilityCount(c, "workshop") > 0)
            {
                var cut = Js.Round((1 - (double)Campaign.ShopPrice(c, ItemIds.SmokeGrenade) / Content.Items[ItemIds.SmokeGrenade].Cost) * 100);
                stats.Add(new BastionStatView { Icon = "tab:engineering", Value = $"−{cut} %", Label = "Accesorios" });
            }
            if (Campaign.FacilityCount(c, "simulation") > 0) stats.Add(new BastionStatView { Icon = "store:credits", Value = $"+{Js.Round((Campaign.RewardMultiplier(c) - 1) * 100)} %", Label = "Suministros de misión" });
            if (Campaign.FacilityCount(c, "training") > 0) stats.Add(new BastionStatView { Icon = "tab:barracks", Value = $"{Campaign.SquadMax(c)}", Label = "Plazas en la escuadra" });
            var will = Campaign.MemorialWill(c);
            if (will != 0) stats.Add(new BastionStatView { Icon = "base:will", Value = $"+{will}", Label = "Voluntad" });
            var delay = Campaign.ListeningDelay(c);
            if (delay != 0) stats.Add(new BastionStatView { Icon = "kind:hack", Value = $"+{delay}", Label = $"Días: {Lore.DoomSubject(level)}" });
        }

        /// <summary>
        /// The card of a piece picked on the clearing (pieceCard; Ana, 21/10 #42): what it gives, how it stands, and
        /// mending, raising, setting again or taking it down; nothing while a vote waits, said on each.
        /// </summary>
        PieceCardView Card(CampaignState c, BasePiece p, bool dismantling)
        {
            var def = Base.Pieces[p.Piece];
            var text = Lore.PieceTexts.All[p.Piece];
            var level = p.Level ?? 1;
            var building = def.Kind == PieceKinds.Building && p.Piece != PieceIds.Caravan;
            var v = new PieceCardView { Title = p.Piece == PieceIds.Caravan ? Lore.BastionLevelName(Level) : text.Name, CloseTip = "Soltarla (Esc)" };
            v.Figure = building ? Lore.PieceFigure(p.Piece, level) : null;
            if (v.Figure == null) v.Line = text.Line;
            if (building)
            {
                // A building's levels in a row, what each gives, the one it is at lit (ladder); none if no level counts.
                var top = text.Levels?.Count ?? 3;
                var steps = Enumerable.Range(1, top).Select(n => (n, figure: Lore.PieceFigureShort(p.Piece, n))).ToList();
                if (steps.Any(s => s.figure != null))
                    foreach (var (n, figure) in steps) v.Ladder.Add(new LadderStepView { Label = $"Nivel {n}", Figure = figure ?? "—", Now = n == level, Past = n < level });
                if (text.Levels != null && level >= 1 && level <= text.Levels.Count) v.LevelLine = text.Levels[level - 1];
            }
            var hurt = p.Hp < def.Hp;
            v.Holds = Lore.HoldsLine(p.Hp, def.Hp);
            v.HoldsTip = "Lo que aguanta los golpes de los muertos";
            if (def.Charges != null) v.Uses = Lore.UsesLine(p.Charges ?? 0, def.Charges.Value);
            if (def.Kind == PieceKinds.Building && !Campaign.BuildingWorks(p)) v.State = Lore.PieceState.BrokenLine;
            else if (def.Charges != null && (p.Charges ?? 0) == 0) v.State = p.Piece == PieceIds.Turret ? Lore.PieceState.SilentLine : Lore.PieceState.SpentLine;
            var pending = Voting && c.Proposal != null ? Pending : "";
            if (hurt)
            {
                var cost = Base.RepairCost(p);
                var (s, pa) = Short(c, cost.Supplies, cost.Parts ?? 0);
                v.Actions.Add(new PieceActionView { Action = "mend", Label = Lore.ActionPrice("repair", cost.Supplies, cost.Parts ?? 0), Why = Or(pending, s > 0 || pa > 0 ? Lore.BuildShortLine(s, pa) + "." : "") });
            }
            if (building)
            {
                var next = Campaign.RaiseCost(p);
                if (next.Error != null) v.Actions.Add(new PieceActionView { Action = "raise", Label = Lore.BuildText.Upgrade, Why = next.Error });
                // An upgrade the Bastion's level doesn't allow yet isn't shown (#13); one a level has just brought says «Nuevo».
                else if (Level >= next.Level)
                {
                    var why = !Campaign.BuildingWorks(p) ? Lore.PieceState.BrokenLine : c.Credits < next.Cost ? Lore.BuildShortLine(next.Cost - c.Credits) + "." : pending;
                    var line = Lore.UpgradeLine(p.Piece, level + 1);
                    var key = $"raise:{p.Piece}:{level + 1}";
                    v.Actions.Add(new PieceActionView { Action = "raise", Label = Lore.ActionPrice("upgrade", next.Cost), Why = why, Tip = line, New = IsNew(key), NewKey = key });
                }
            }
            // The Bastion itself goes up here (Arturo, 20/10), by what it becomes; what is missing under the button, or
            // nothing when it can (Lore, 24/10: not the 0.10.1's «8 de 6 misiones…»); its line on hover.
            if (p.Piece == PieceIds.Caravan)
            {
                var up = Campaign.NextLevel(c);
                if (up == null) v.Note = Lore.LevelText.Top;
                else v.Actions.Add(new PieceActionView { Action = "levelUp", Label = $"Subir a {Lore.BastionLevelThe(up.Level)} · {up.Cost} §", Why = Or(pending, Campaign.LevelUpBlocker(c) ?? ""), Hover = Lore.LevelUpLine(up.Level, up.Cost) });
            }
            if (def.Charges != null && (p.Charges ?? 0) < def.Charges)
            {
                var parts = Base.RearmCost(p);
                v.Actions.Add(new PieceActionView { Action = "rearm", Label = Lore.ActionPrice("rearm", 0, parts), Why = Or(pending, (c.Parts ?? 0) < parts ? Lore.BuildShortLine(0, parts - (c.Parts ?? 0)) + "." : "") });
            }
            if (Base.DismantleBlocker(p) == null)
            {
                var back = Base.DismantleRefund(p);
                v.Actions.Add(new PieceActionView { Action = "dismantle", Label = Lore.ActionPrice("dismantle", back), Why = pending });
                if (dismantling) v.Confirm = new PieceConfirmView { Text = $"{Lore.BuildText.DismantleAsk} {Lore.DismantleBack(back)}", Yes = Lore.BuildText.DismantleYes, No = Lore.BuildText.DismantleNo };
            }
            foreach (var a in v.Actions) if (a.Why == "") a.Why = null;
            return v;
        }

        static string Or(string first, string then) => !string.IsNullOrEmpty(first) ? first : then;
    }
}
