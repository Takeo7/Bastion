using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>A seat at the campaign (the room's player list): its slot, its name and colour, and whether it is here.</summary>
    public sealed class SeatInfo
    {
        public int Slot;
        public string Name;
        public string Color;
        public bool Connected;
        /// <summary>The tab its player is on (Red's baseView); null, unknown.</summary>
        public string View;
    }

    /// <summary>
    /// The base's views from the campaign's state (strategy/screen.ts's render*, buildMode.ts, with the C# campaign's
    /// reads): what StrategyRoot shows, built again on every change. It decides nothing and sends nothing: the orders go
    /// by IStrategyHandlers (the campaign's commands come with Red's room). Screen by screen, in partial classes; what
    /// the 0.10.1 kept in the browser (news seen, «Nuevo») still to come.
    /// </summary>
    public sealed partial class StrategyPresenter
    {
        /// <summary>The campaign now (the room's snapshot).</summary>
        public CampaignState State;
        /// <summary>This player's seat.</summary>
        public int Slot;
        public List<SeatInfo> Seats = new List<SeatInfo>();

        int Level => Progression.BastionLevel(State.Level);

        SeatInfo Seat(int slot) => Seats.FirstOrDefault(s => s.Slot == slot);
        SeatInfo Partner => Seats.FirstOrDefault(s => s.Slot != Slot);
        string PlayerName(int slot) => Seat(slot)?.Name ?? $"Jugador {slot + 1}";
        string PlayerColor(int slot) => Seat(slot)?.Color ?? (slot >= 0 && slot < Content.PlayerColors.Count ? Content.PlayerColors[slot] : "#888888");

        /// <summary>The other one is here: everything on the base goes to a vote, one at a time (Arturo, 18/10 #55).</summary>
        bool Voting => State.Players.Count > 1 && Partner != null && Partner.Connected;

        List<int> Connected => Seats.Where(s => s.Connected).Select(s => s.Slot).ToList();

        static TipView Tip(string name, string text) => new TipView { Name = name, Lines = StrategyText.Sentences(text) };

        // ------------------------------------------------------------ the strip

        /// <summary>The top strip (renderTop): the outings, the siege, the day, the stores, the level, the doom.</summary>
        public TopView Top()
        {
            var c = State;
            var level = Level;
            var v = new TopView
            {
                Outings = c.MissionsFlown ?? 0,
                OutingsTip = Tip("Salidas", Lore.TimeHelp(level)),
                Day = c.Day,
                DayTip = Tip($"Día {c.Day}", Lore.TimeHelp(level)),
            };
            Siege(c, v);
            var owed = Owed(c);
            var credits = new StoreView { Id = "credits", Value = c.Credits.ToString(), TipName = "Suministros" };
            // What Metsovo's convoy brings and when, first (#38); the glossary's line under it.
            credits.Tip.Add($"+{Campaign.ConvoyIncome(c)} con el convoy, {ConvoyWhen(c)}");
            credits.Tip.AddRange(StrategyText.Sentences(Lore.Glossary["Suministros"]));
            OwedOn(c, credits, owed.credits);
            v.Stores.Add(credits);
            v.Stores.Add(new StoreView { Id = "medicines", Value = (c.Medicines ?? 0).ToString(), TipName = "Medicinas", Tip = StrategyText.Sentences(Lore.Glossary["Medicinas"]) });
            var eat = Campaign.RationsPerMission(c);
            var grow = Campaign.GardenPerMission(c);
            var food = new StoreView { Id = "food", Value = (c.Food ?? 0).ToString(), TipName = "Raciones", Bad = c.Hungry == true || (c.Food ?? 0) + grow < eat, Tip = RationsTip(c) };
            v.Stores.Add(food);
            var parts = new StoreView { Id = "parts", Value = (c.Parts ?? 0).ToString(), TipName = "Piezas", Tip = StrategyText.Sentences(Lore.Glossary["Piezas"]) };
            OwedOn(c, parts, owed.parts);
            v.Stores.Add(parts);
            // What the Bastion is now, by name (#13), and the missions flown towards the next (levelBadge).
            var next = Campaign.NextLevel(c);
            v.Level = Lore.BastionLevelName(level).ToUpperInvariant();
            if (next != null)
            {
                v.LevelFilled = System.Math.Min(next.Flown, next.Missions);
                v.LevelTotal = next.Missions;
                v.LevelTip = new TipView { Name = Lore.BastionLevelName(level), Lines = { $"Para {Lore.BastionLevelThe(next.Level)}: {next.Cost} § y {next.Missions} misiones hechas (llevas {next.Flown}).", Lore.LevelText.About } };
            }
            else v.LevelTip = new TipView { Name = Lore.BastionLevelName(level), Lines = { Lore.LevelText.Top } };
            // The doom by the name it has at this level: no Marea before the Partitura (Lore).
            var known = level >= Lore.PartituraLevel;
            v.Doom = Lore.DoomName(level).ToUpperInvariant();
            v.DoomFilled = c.Doom;
            v.DoomMax = Campaign.DoomMax;
            v.DoomFigure = known ? $"{System.Math.Max(0, Campaign.DoomMax - c.Doom)} km" : $"{c.Doom}/{Campaign.DoomMax}";
            v.DoomTip = Tip(Lore.DoomName(level), $"{Lore.DoomStatus(level, c.Doom)}. {(known ? Lore.Glossary["Marea"] : "Si se llena, la campaña está perdida.")} Quemar Nidos la hace retroceder.");
            return v;
        }

        /// <summary>The next siege (siegeEl): when, and with the Sala de radio how many and by where; red the mission before, and while it is on.</summary>
        void Siege(CampaignState c, TopView v)
        {
            if (c.Siege == null) return;
            var now = c.Offers.FirstOrDefault(o => o.Siege == true);
            if (now != null)
            {
                v.Siege = (Lore.SiegeText.Name + (now.Night == true ? " · " + Lore.NightText.Label : "")).ToUpperInvariant();
                v.SiegeNow = true;
                v.SiegeTip = Tip(Lore.SiegeText.Name, Lore.BaseSiegeText.Defenders);
                return;
            }
            var heard = Campaign.SiegeHeard(c);
            var full = heard.Count != null ? Lore.SiegeWarning(heard.In, heard.Count, (IReadOnlyList<int>)heard.Sides ?? new List<int>()) : Lore.SiegeWarning(heard.In);
            v.Siege = Lore.SiegeWarningShort(heard.In).ToUpperInvariant();
            v.SiegeNear = heard.In <= 1;
            v.SiegeTip = Tip(Lore.SiegeText.Name, full);
        }

        /// <summary>What a pending proposal would spend (owed): suministros, and a craft's piezas.</summary>
        (int credits, int parts) Owed(CampaignState c)
        {
            var a = c.Proposal?.Action;
            if (a == null) return (0, 0);
            var parts = a is CraftAction craft && Progression.Crafts.TryGetValue(craft.Project, out var def) ? def.Parts : 0;
            return (Campaign.ProposalCost(c, a), parts);
        }

        /// <summary>«−80» after a store's figure while a proposal would spend it, with who has to say yes (owedEl).</summary>
        void OwedOn(CampaignState c, StoreView store, int n)
        {
            if (n == 0 || c.Proposal == null) return;
            store.Owed = $"−{n}";
            store.OwedTip = c.Proposal.By == Slot
                ? $"Pendiente: se paga cuando {Partner?.Name ?? "el otro jugador"} diga que sí"
                : $"Pendiente: se paga si aceptas la propuesta de {PlayerName(c.Proposal.By)}";
        }

        /// <summary>The rations' tip (rationsTip): what a mission eats and the Huertos grow, how long it lasts, then the rule.</summary>
        static List<string> RationsTip(CampaignState c)
        {
            var eat = Campaign.RationsPerMission(c);
            var grow = Campaign.GardenPerMission(c);
            var net = eat - grow;
            int? lasts = net > 0 ? (int)System.Math.Floor((double)(c.Food ?? 0) / net) : (int?)null;
            var lines = new List<string>();
            if (c.Hungry == true) lines.Add("Hay hambre.");
            lines.Add($"−{eat} por misión");
            if (grow > 0) lines.Add($"+{grow} de los Huertos");
            lines.Add(lasts == null ? "Los Huertos dan lo que se come." : lasts > 0 ? $"Hay para {lasts} {(lasts == 1 ? "misión" : "misiones")}." : "No hay para la próxima misión.");
            lines.AddRange(StrategyText.Sentences(Lore.RationsHelp()));
            return lines;
        }

        /// <summary>When Metsovo's next convoy comes, in words (convoyWhen): «tras la próxima misión», «dentro de 6 misiones».</summary>
        static string ConvoyWhen(CampaignState c) => Lore.InMissions(System.Math.Max(1, Campaign.ConvoyIn(c)));

        // ------------------------------------------------------------- the tabs

        static readonly (string view, string label)[] Tabs = { ("geoscape", "Mapa"), ("engineering", "Armería"), ("barracks", "Soldados"), ("facilities", "Asentamiento") };

        /// <summary>
        /// The tabs (renderNav): Escuadra with a mission chosen; a number for the news this player hasn't looked at yet
        /// (the Mapa's: its missions and rumours, red if one matters), ▲n for their soldiers with points to spend; «Nuevo»
        /// where a level brought something not seen yet; the partner's initial on the tab they are on.
        /// </summary>
        public NavView Nav(string view)
        {
            var c = State;
            Fresh.Load(c);
            var news = MapNews(c);
            News.Load(c, news.Select(n => n.key));
            var v = new NavView { Hidden = view == "squad" || view == "debrief" };
            var items = Tabs.ToList();
            if (c.ActiveMission != null) items.Add(("squad", "Escuadra"));
            var promotions = c.Soldiers.Where(s => s.Owner == Slot && s.Alive && Campaign.CanSpendPoints(c, s)).ToList();
            var partner = Partner;
            // Where this player has something new a level brought (freshViews): a building's upgrade only if one can take it.
            var freshViews = new HashSet<string>(Fresh.Fresh(Level)
                .Where(t => t.RaisePiece == null || c.Bastion.Pieces.Any(p => p.Piece == t.RaisePiece && (p.Level ?? 1) == t.RaiseTo - 1))
                .Select(t => t.View));
            foreach (var (id, label) in items)
            {
                var tab = new TabView { Id = id, Label = label, On = view == id, Urgent = id == "squad" };
                // A tab's number is only for news not looked at yet, and goes once the screen is visited.
                if (id == "geoscape" && view != "geoscape")
                {
                    var unseen = news.Where(n => !News.Seen(n.key)).ToList();
                    if (unseen.Count > 0)
                    {
                        tab.Count = unseen.Count.ToString();
                        tab.CountUrgent = unseen.Any(n => n.urgent);
                        tab.Tip = string.Join(" · ", unseen.Select(n => n.label));
                    }
                }
                tab.New = freshViews.Contains(id) && tab.Count == null;
                // Only one's own: a soldier's skills are its owner's alone, there or not (playtest 18/10 #42). No talents (Lore, C1).
                if (id == "barracks" && promotions.Count > 0)
                {
                    tab.Count = $"▲{promotions.Count}";
                    tab.Tip = string.Join(" · ", promotions.Select(s => $"{s.Name}: puntos por gastar en el tablero"));
                }
                if (partner != null && partner.Connected && partner.View == id)
                {
                    tab.Partner = partner.Name;
                    tab.PartnerColor = partner.Color;
                }
                v.Tabs.Add(tab);
            }
            return v;
        }
    }
}
