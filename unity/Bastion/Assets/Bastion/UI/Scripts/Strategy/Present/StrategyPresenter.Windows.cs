using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    public sealed partial class StrategyPresenter
    {
        // -------------------------------------------------------------- proposal

        /// <summary>
        /// The proposal waiting for both keys (renderProposal): whose, the engine's sentence, what it costs (paid on the
        /// yes, playtest 16/10 #5) and what it is, and whether «Ver» can take one there. Null with none.
        /// </summary>
        public ProposalView Proposal()
        {
            var c = State;
            var p = c.Proposal;
            if (p == null) return null;
            var mine = p.By == Slot;
            var owed = Owed(c);
            var price = string.Join(" y ", new[] { owed.credits != 0 ? $"{owed.credits} §" : "", owed.parts != 0 ? $"{owed.parts} {(owed.parts == 1 ? "pieza" : "piezas")}" : "" }.Where(x => x != ""));
            var v = new ProposalView
            {
                Mine = mine, Partner = mine ? Partner?.Name ?? "el otro jugador" : PlayerName(p.By), PartnerColor = PlayerColor(p.By),
                Text = p.Text, Cost = price != "" ? price : null,
            };
            var (rows, see) = ProposalDetail(c, p.Action);
            v.Rows.AddRange(rows);
            v.CanSee = see;
            return v;
        }

        /// <summary>What a proposal costs, takes and does (proposalDetail): whoever decides should know as much as whoever proposed.</summary>
        (List<RowView> rows, bool see) ProposalDetail(CampaignState c, ProposalAction action)
        {
            RowView Row(string key, string value) => new RowView { Key = key, Value = value };
            var rows = new List<RowView>();
            switch (action)
            {
                case PlaceAction a:
                {
                    var def = Base.Pieces[a.Piece];
                    rows.Add(Row("Suministros", Campaign.CostText(def.Cost.Supplies)));
                    if ((def.Cost.Parts ?? 0) != 0) rows.Add(Row("Piezas", def.Cost.Parts.ToString()));
                    rows.Add(Row("Qué hace", Lore.PieceTexts.All[a.Piece].Line));
                    return (rows, true);
                }
                case RaiseAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault(x => x.Id == a.Piece);
                    var next = p != null ? Campaign.RaiseCost(p) : null;
                    if (p == null || next == null || next.Error != null) return (rows, false);
                    var text = Lore.PieceTexts.All[p.Piece];
                    var level = p.Level ?? 1;
                    rows.Add(Row("Suministros", Campaign.CostText(next.Cost)));
                    rows.Add(Row("Qué hace", text.Levels != null && level < text.Levels.Count ? text.Levels[level] : text.Line));
                    return (rows, true);
                }
                case PlaceLineAction a:
                {
                    var def = Base.Pieces[a.Piece];
                    var n = a.Cells.Count;
                    rows.Add(Row("Suministros", Campaign.CostText(def.Cost.Supplies * n)));
                    if ((def.Cost.Parts ?? 0) != 0) rows.Add(Row("Piezas", (def.Cost.Parts.Value * n).ToString()));
                    rows.Add(Row("Qué hace", $"{n} × {Lore.PieceTexts.All[a.Piece].Name.ToLowerInvariant()}. {Lore.PieceTexts.All[a.Piece].Line}"));
                    return (rows, n > 0);
                }
                case FellAction a:
                    rows.Add(Row("Suministros", Campaign.CostText(Base.FellCost * a.Tiles.Count)));
                    rows.Add(Row("Qué hace", a.Tiles.Count == 1 ? "Talar un árbol." : $"Talar {a.Tiles.Count} árboles."));
                    return (rows, a.Tiles.Count > 0);
                // Mending, setting again and taking down go to a vote too (18/10 #55).
                case MendAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault(x => x.Id == a.Piece);
                    if (p == null) return (rows, false);
                    var cost = Base.RepairCost(p);
                    rows.Add(Row("Suministros", Campaign.CostText(cost.Supplies)));
                    if ((cost.Parts ?? 0) != 0) rows.Add(Row("Piezas", cost.Parts.ToString()));
                    rows.Add(Row("Qué hace", $"Repara {Lore.PieceTexts.All[p.Piece].The}."));
                    return (rows, true);
                }
                case RearmAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault(x => x.Id == a.Piece);
                    if (p == null) return (rows, false);
                    rows.Add(Row("Piezas", Base.RearmCost(p).ToString()));
                    rows.Add(Row("Qué hace", $"Repone {Lore.PieceTexts.All[p.Piece].The}."));
                    return (rows, true);
                }
                case DismantleAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault(x => x.Id == a.Piece);
                    if (p == null) return (rows, false);
                    var back = Base.DismantleRefund(p);
                    rows.Add(Row("Qué hace", $"Desmonta {Lore.PieceTexts.All[p.Piece].The}{(back != 0 ? $": devuelve {Campaign.CostText(back)}" : "")}."));
                    return (rows, true);
                }
                case LevelUpAction _:
                {
                    var next = Campaign.NextLevel(c);
                    if (next == null) return (rows, false);
                    rows.Add(Row("Suministros", Campaign.CostText(next.Cost)));
                    rows.Add(Row("Qué hace", $"El Bastión pasa a ser {Lore.BastionLevelA(next.Level)}."));
                    return (rows, false);
                }
                case ChooseMissionAction a:
                {
                    var o = c.Offers.FirstOrDefault(x => x.Id == a.Id);
                    if (o == null) return (rows, false);
                    var reward = string.Join(" y ", new[] { o.Reward?.Credits is int cr && cr != 0 ? $"{cr} §" : "", o.Reward?.Recruit != null && !(o.Reward.Recruit is bool b && !b) ? "un recluta" : "" }.Where(x => x != ""));
                    var info = Campaign.MissionInfo(c, o);
                    // What there is to do there, in Lore's words (playtest 15/10 #4).
                    rows.Add(Row("Objetivo", Lore.OperationAim(o.Kind, info.Item, info.VipName, info.Alarm, o.Facility != null, o.Final)));
                    rows.Add(Row("Extra", $"{Setup.MissionNames[o.Kind]} en {Campaign.Regions[o.Region].Name} · dificultad {new string('★', o.Difficulty)}{(reward != "" ? $" · recompensa: {reward}" : "")}"));
                    return (rows, true);
                }
                case ContactAction a:
                    rows.Add(Row("Tiempo", "Al momento"));
                    rows.Add(Row("Suministros", Campaign.CostText(Campaign.ContactCost(c))));
                    rows.Add(Row("Qué hace", Lore.ContactAim(a.Region, Campaign.RegionIncome(c, a.Region))));
                    return (rows, true);
                case InvestigateAction a:
                {
                    var rumor = c.Rumors.FirstOrDefault(r => r.Id == a.Id);
                    if (rumor == null) return (rows, false);
                    rows.Add(Row("Tiempo", $"{rumor.DaysLeft} días escuchando; no hace pasar el tiempo"));
                    rows.Add(Row("Qué hace", Lore.RumorAim(Campaign.Regions[rumor.Region].Name)));
                    return (rows, true);
                }
                // TODO(tanda 3): the event's option's effect (eventDef).
                case EventAction a:
                {
                    var def = Campaign.EventDef(c);
                    if (def != null && a.Option >= 0 && a.Option < def.Options.Count) rows.Add(Row("Qué hace", def.Options[a.Option].Effect));
                    return (rows, false);
                }
                // Spending the common supplies is proposed too (Ana, 21/10 #31): a recruit, a purchase.
                case RecruitAction a:
                    rows.Add(Row("Suministros", Campaign.CostText(Campaign.RecruitCost(c))));
                    rows.Add(Row("Qué hace", $"Un recluta para {PlayerName(a.For)}."));
                    return (rows, true);
                case BuyAction a:
                    rows.Add(Row("Suministros", Campaign.CostText(Campaign.ShopPrice(c, a.Item))));
                    rows.Add(Row("Qué hace", $"Comprar: {Content.Items[a.Item].Name}."));
                    return (rows, true);
                case CraftAction a:
                {
                    var def = Progression.Crafts[a.Project];
                    rows.Add(Row("Tiempo", $"Sale {Lore.InMissions(MissionsFor(def.Days))}"));
                    rows.Add(Row("Suministros", $"{Campaign.CostText(def.Cost)} y {def.Parts} piezas"));
                    rows.Add(Row("Qué hace", Lore.CraftAim(def.Kind, def.The)));
                    return (rows, false);
                }
            }
            return (rows, false);
        }

        /// <summary>
        /// «Te esperan» (renderWaiting; Ana, 23/10 #31): the partner is ready to go and this player isn't; not on the
        /// Escuadra but for a siege, nor in the debrief. Null when not.
        /// </summary>
        public WaitingView Waiting(string view)
        {
            var c = State;
            var partner = Partner;
            var offer = Campaign.ActiveOffer(c);
            if (c.Proposal != null || offer == null || partner == null || !partner.Connected || !c.LaunchReady[partner.Slot] || c.LaunchReady[Slot] || view == "debrief") return null;
            var here = view == "squad";
            if (here && offer.Siege != true) return null;
            return new WaitingView
            {
                Text = $"<color={partner.Color}><b>{partner.Name}</b></color>{(offer.Siege == true ? " está a punto para defender el Bastión." : $" está a punto para salir: {offer.Name}.")}",
                Button = here ? null : "Ir a la Escuadra",
            };
        }

        // --------------------------------------------------------------- windows

        /// <summary>The campaign's end (renderModal, c.outcome): its title, its epilogue, back to the menu. Null while it goes on.</summary>
        public StrategyModalView End()
        {
            var c = State;
            if (c.Outcome == null) return null;
            var won = c.Outcome == "victory";
            return new StrategyModalView
            {
                Kind = "end", Kicker = "Fin de la campaña", Tone = won ? "good" : "bad",
                Title = won ? (c.Ending != null && Lore.EndingTitles.TryGetValue(c.Ending, out var t) ? t : "Campaña ganada") : "Campaña perdida",
                Text = Lore.Epilogue(c.Outcome, c.Ending, c.Doom >= Campaign.DoomMax),
                Buttons = { StrategyText.Leave.Yes },
            };
        }

        /// <summary>
        /// The pending event (renderModal, c.event and eventDef): the finale's, a side quest's (ENCARGO · its title) or an
        /// event's kicker, its two options (the one on the table marked; all off while anything is proposed; «Ahora no
        /// está a vuestro alcance» under one that can't be had), and under them the vote on the answer, or that time
        /// waits. Null without one, or in the debrief.
        /// </summary>
        public StrategyModalView Event(string view)
        {
            var c = State;
            var def = c.Event != null ? Campaign.EventDef(c) : null;
            if (def == null || view == "debrief") return null;
            var proposed = c.Proposal?.Action as EventAction;
            var kicker = c.Event == CampaignEventIds.Finale ? "Decisión final"
                : c.Event == CampaignEventIds.Quest && c.QuestEvent != null && Quests.QuestDefs.TryGetValue(c.QuestEvent, out var q) ? $"Encargo · {q.Title}"
                : "Acontecimiento";
            var v = new StrategyModalView { Kind = "event", Kicker = kicker, Title = def.Title, Text = def.Text };
            for (var i = 0; i < 2 && i < def.Options.Count; i++)
                v.Options.Add(new OptionView
                {
                    Label = def.Options[i].Label, Effect = def.Options[i].Effect, Proposed = proposed != null && proposed.Option == i, Locked = c.Proposal != null,
                    Blocked = Campaign.EventOptionAvailable(c, i) ? null : "Ahora no está a vuestro alcance",
                });
            if (proposed != null) v.Vote = Proposal();
            else v.Foot = "El tiempo no avanza hasta que decidáis.";
            return v;
        }

        /// <summary>«Volver al menú principal» from the base, asked only with the other one here (it takes them back too).</summary>
        public StrategyModalView Leave() => new StrategyModalView
        {
            Kind = "leave", Kicker = StrategyText.Leave.Kicker, Title = StrategyText.Leave.Title,
            Text = StrategyText.Leave.Text(Partner != null && Partner.Connected ? Partner.Name : null), Buttons = { StrategyText.Leave.Yes, StrategyText.Leave.No },
        };

        /// <summary>
        /// Who the players are, once per campaign on this computer, before the first mission (showPremise; Ana, 20/10 #15:
        /// only the words, Lore's key words in bold). Null when already seen or under way.
        /// </summary>
        public StrategyModalView Premise()
        {
            var c = State;
            if ((c.MissionsFlown ?? 0) > 0 || c.Day > 1) return null;
            var key = LocalMarks.Key("seenPremise", c);
            if (LocalMarks.Read(key) != null) return null;
            LocalMarks.Write(key, new[] { "1" });
            var v = new StrategyModalView { Kind = "alert", Buttons = { "Entendido" } };
            v.Body.AddRange(Lore.Premise.Lines);
            return v;
        }

        /// <summary>
        /// The windows for what happened between two states (collectAlerts): a page of the diary for each level bought,
        /// with what it brought; of what the campaign logged as a pop-up, only the siege (Ana, 21/10 #26, #28). The most
        /// important first. The controller queues them and shows one at a time.
        /// </summary>
        public List<StrategyModalView> Alerts(CampaignState before)
        {
            var c = State;
            var out_ = new List<(int rank, StrategyModalView view)>();
            for (var level = Progression.BastionLevel(before.Level) + 1; level <= Progression.BastionLevel(c.Level); level++)
            {
                var page = Lore.Expediente[level - 1];
                var v = new StrategyModalView { Kind = "alert", Tone = "good", Kicker = "Diario", Title = page.Title, Body = { page.Summary }, Text = $"El Bastión es {Lore.BastionLevelA(level)}.", Note = page.Note, Buttons = { "Entendido" } };
                v.Brought.AddRange(Brought(level));
                out_.Add((90, v));
            }
            var last = before.Log.FirstOrDefault();
            var seen = last != null ? c.Log.FindIndex(e => e.Day == last.Day && e.Text == last.Text) : c.Log.Count;
            var fresh = c.Log.Take(seen == -1 ? c.Log.Count : seen).Reverse();
            foreach (var e in fresh)
                if (e.Alert?.Kicker == "Asedio")
                    out_.Add((30, new StrategyModalView { Kind = "alert", Tone = e.Tone == "info" ? null : e.Tone, Kicker = e.Alert.Kicker, Title = e.Alert.Title, Text = e.Text, Buttons = { "Entendido" } }));
            return out_.OrderByDescending(x => x.rank).Select(x => x.view).ToList();
        }

        /// <summary>
        /// What a level brought (broughtList; Ana, 20/10 #31; 23/10 #38): grouped as where each is found, each its icon,
        /// its name and what it is in a few words: a building's level, its figure; anything else, Lore's NEW_BRIEF, or the
        /// first sentence of its line.
        /// </summary>
        static List<BroughtGroupView> Brought(int level)
        {
            string Brief(string text, string key = "") => key != "" && Lore.NewBrief.TryGetValue(key, out var b) ? b : (text ?? "").Split(new[] { ". " }, 2, System.StringSplitOptions.None)[0].TrimEnd('.');
            var pieces = Base.PieceOrder.Where(id => id != PieceIds.Caravan).ToList();
            var build = new BroughtGroupView { Title = "Para construir" };
            foreach (var id in pieces.Where(id => Base.Pieces[id].Level == level))
                build.Items.Add(new BroughtView { Icon = PieceIcon(id), Name = Lore.PieceTexts.All[id].Name, Brief = Brief(Lore.PieceTexts.All[id].Line, "piece:" + id) });
            var up = new BroughtGroupView { Title = "Suben de nivel" };
            foreach (var id in pieces.Where(id => Base.Pieces[id].Kind == PieceKinds.Building))
                foreach (var to in new[] { 2, 3 }.Where(to => Progression.UpgradeLevel(Base.Pieces[id].Level, to) == level))
                {
                    var levels = Lore.PieceTexts.All[id].Levels;
                    up.Items.Add(new BroughtView { Icon = PieceIcon(id), Name = $"{Lore.PieceTexts.All[id].Name}, nivel {to}", Brief = Lore.PieceFigure(id, to) ?? Brief(levels != null && to <= levels.Count ? levels[to - 1] : "") });
                }
            // The belt's only, as the Armería (fresh.ts, #39).
            var armory = new BroughtGroupView { Title = "En la Armería" };
            foreach (var i in Content.Items.Values.Where(i => i.Level == level && i.Slot == ItemSlots.Utility))
                armory.Items.Add(new BroughtView { Icon = ItemIcon(i.Id), Name = i.Name, Brief = Brief(i.Description, "item:" + i.Id) });
            var techs = new BroughtGroupView { Title = "Avances" };
            foreach (var t in Progression.Techs.Values.Where(t => t.Level == level))
                techs.Items.Add(new BroughtView { Icon = "weapon:rifle", Name = t.Name, Brief = Brief(t.Description, "tech:" + t.Id) });
            return new[] { build, up, armory, techs }.Where(g => g.Items.Count > 0).ToList();
        }

        /// <summary>A piece's icon among the HUD's, near the 0.10.1's PIECE_ICONS (the radio's comms, the barracks' helmet…).</summary>
        static string PieceIcon(string id)
        {
            switch (id)
            {
                case "radio": return "kind:hack";
                case "quarters": return "base:beds";
                case "infirmary": return "store:medicines";
                case "workshop": return "store:parts";
                case "garden": return "base:garden";
                case "simulation": return "tab:geoscape";
                case "garage": return "map:lata";
                case "range": return "stat:aim";
                case "memorial": return "base:will";
                case "mine": case "snare": case "spikes": return "grenade";
                default: return Base.Pieces[id].Kind == PieceKinds.Building ? "map:home" : "tab:facilities";
            }
        }
    }
}
