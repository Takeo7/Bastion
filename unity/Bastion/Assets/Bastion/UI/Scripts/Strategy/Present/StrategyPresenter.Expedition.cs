using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The map of an expedition (H1) from the campaign's run (RunState): its stages, which are seen (RunMap.Visible, the
    /// engine's, so that both players see the same), the way taken, the ones to choose now and why not
    /// (Runs.ChooseNodeRefusal); the squad with its health and kits; whether it can go back (Runs.EndRunRefusal).
    /// </summary>
    public sealed partial class StrategyPresenter
    {
        /// <summary>The map with `chosen`'s kit on the side (a soldier's id; null or not in the squad, the player's first); null, no run.</summary>
        public RunMapView Expedition(string chosen)
        {
            var c = State;
            var run = c.Run;
            if (run == null) return null;
            var choosing = run.Phase == RunPhases.Choose;
            var choices = choosing ? new HashSet<string>(RunMap.Choices(run).Select(n => n.Id)) : new HashSet<string>();
            var path = run.Path;
            var cols = run.Nodes.GroupBy(n => n.Row).ToDictionary(g => g.Key, g => g.Count());
            var v = new RunMapView
            {
                Head = StrategyText.Runs.Head(run.Chapter),
                // (U2, cuadro 157: «Elige la siguiente etapa» went; the open stages say it. With none, why.)
                Line = choosing && choices.Count == 0 ? StrategyText.Runs.NoWay : null,
                Start = StrategyText.Runs.Start,
                AtStart = run.At == null,
                // G22 (Arturo, 18/11, cuadro 72; U21): home only from a Refugio, its own plate's button, or after the boss,
                // by itself; the map's button only on an expedition started before (RunState.HomeFromRefuge missing), and
                // where it went a line saying where one goes back from (not at the Refugio, whose plate says it).
                Back = run.HomeFromRefuge == true ? null : StrategyText.Runs.Back,
                // Supervivencia's (N9): the wounded stay wounded (Lore.WoundsText), not «todos se curan».
                BackTip = Levels.Of(c).Wounds ? Lore.WoundsText.ReturnHelp : StrategyText.Runs.BackTip,
                BackWhy = Runs.EndRunRefusal(c),
                // (With the Ruta de vuelta, from a Trueque too: cuadro 165, Lore's line.)
                HomeNote = run.HomeFromRefuge == true && !Runs.AtRefuge(run) && !(Runs.Level(c, UpgradeIds.Route) > 0 && Runs.AtTrade(run))
                    ? (Runs.Level(c, UpgradeIds.Route) > 0 ? Lore.ExpeditionText.ReturnWhereRoute : Lore.ExpeditionText.ReturnWhere) : null,
                SquadHead = StrategyText.Runs.Squad,
                Run = $"{run.Chapter}:{run.Seed}",
                Fresh = run.At == null && run.Path.Count == 0,
                Go = StrategyText.Runs.Go,
                WhatHead = StrategyText.Runs.WhatHead,
                WhereHead = StrategyText.Runs.WhereHead,
            };
            v.Legend.AddRange(StrategyText.Runs.Legend);
            // A stage proposed to the vote (two players): marked on the map in whose colour it is (cuadro 132).
            if (c.Proposal?.Action is ChooseNodeAction proposed)
            {
                v.Proposed = proposed.Node;
                v.ProposedLine = c.Proposal.By == Slot ? StrategyText.Runs.ProposedMine : StrategyText.Runs.ProposedBy(PlayerName(c.Proposal.By));
                v.ProposedColor = PlayerColor(c.Proposal.By);
            }
            foreach (var n in run.Nodes.OrderBy(n => n.Row).ThenBy(n => n.Col))
            {
                var seen = RunMap.Visible(c, n);
                var nv = new RunNodeView
                {
                    Id = n.Id, Row = n.Row, Col = n.Col, Cols = cols[n.Row],
                    Hidden = !seen,
                    Done = path.Contains(n.Id), At = run.At == n.Id,
                    Open = choices.Contains(n.Id),
                    // Only once seen: hidden, the boss's is a «?» like the rest, not bigger nor red (Arturo, 17/11: unseen
                    // till the squad is near, as the far rows).
                    Boss = seen && n.Kind == NodeKinds.Boss,
                };
                if (seen)
                {
                    nv.Name = StrategyText.Runs.NodeName(n.Kind, run.Chapter);
                    nv.Icon = NodeIcon(n.Kind);
                    nv.Stop = NodeKinds.IsStop(n.Kind);
                    // The card in parts (cuadro 132): its row, its danger (a fight's), what to do, where.
                    nv.Kicker = StrategyText.Runs.Kicker(n.Row);
                    nv.What = StrategyText.Runs.StageLine(n);
                    if (!nv.Stop && n.Kind != NodeKinds.Boss) nv.Danger = n.Difficulty;
                    if (!nv.Stop && n.Site != null && Places.Names.TryGetValue(n.Site, out var where))
                    {
                        nv.Place = where;
                        nv.PlaceIcon = Bastion.UI.Hud.LineIcon.Has("place:" + n.Site) ? "place:" + n.Site : null;
                        nv.PlaceLine = Lore.SiteLine(n.Site);
                    }
                    // Its line with what is known of it (Runs.StageLine: the alarm, the Asedio's turn, the Guarida's guard).
                    nv.Card.Add(StrategyText.Runs.StageLine(n));
                    // A Rescate with La Lata full (N2, 17/11): whoever doesn't fit is lost when it leaves, said before choosing it.
                    // A Mordido too (Producto, 19/11): winning it takes the bitten aboard, cured, and with no seat free a soldier
                    // stays on the ground (Lore's line says it as it is).
                    if ((n.Kind == NodeKinds.Rescue || n.Kind == NodeKinds.Bitten) && !Runs.SeatFree(c))
                    {
                        nv.Card.Add(Lore.RescueNoSeat);
                        nv.Warn = Lore.RescueNoSeatTag;
                        nv.WarnLine = Lore.RescueNoSeat;
                    }
                    // A special map (H7, H9: not one of the six places a fight is in otherwise): its mark (Arturo, 17/11).
                    if (n.Site != null && !OrdinaryPlaces.Contains(n.Site) && Bastion.UI.Hud.LineIcon.Has("place:" + n.Site)) nv.SiteIcon = "place:" + n.Site;
                    if (!nv.Stop)
                    {
                        if (n.Site != null && Places.Names.TryGetValue(n.Site, out var place)) nv.Card.Add(StrategyText.Runs.Site(place));
                        // A special map's line under it (Lore, 17/11): only inside, and what its searches give more of.
                        if (n.Site != null && Lore.SiteLine(n.Site) is string special) nv.Card.Add(special);
                        nv.Card.Add(StrategyText.Runs.Difficulty(n.Difficulty));
                    }
                    if (n.Modifier != null)
                    {
                        nv.ModifierId = n.Modifier;
                        nv.Modifier = StrategyText.Runs.ModifierName(n.Modifier);
                        nv.ModifierIcon = ModifierIcon(n.Modifier);
                        nv.Card.Add(ModifierCard(n.Modifier));
                        nv.Notes.Add(ModifierCard(n.Modifier));
                    }
                    // What an event left (H4): on the stage it went to, or, still waiting (RunState.Pending), on the fights
                    // that can be chosen now, one of which takes it.
                    var added = n.Added ?? (choices.Contains(n.Id) && RunMap.IsFight(n.Kind) ? run.Pending : null);
                    if (added != null)
                    {
                        nv.AddedId = added;
                        nv.AddedIcon = ModifierIcon(added);
                        nv.Card.Add(ModifierCard(added) + StrategyText.Runs.ByEvent);
                        nv.Notes.Add(ModifierCard(added) + StrategyText.Runs.ByEvent);
                    }
                }
                // The Radio (cuadros 164 and 165): a «?»'s modifier known too (RunMap.ModifierSeen), its mark by it.
                else if (n.Modifier != null && RunMap.ModifierSeen(c, n))
                {
                    nv.ModifierId = n.Modifier;
                    nv.Modifier = StrategyText.Runs.ModifierName(n.Modifier);
                    nv.ModifierIcon = ModifierIcon(n.Modifier);
                    nv.Notes.Add(ModifierCard(n.Modifier));
                }
                else
                {
                    nv.Card.Add(StrategyText.Runs.Unseen);
                    nv.Kicker = StrategyText.Runs.Kicker(n.Row);
                    nv.What = StrategyText.Runs.Unseen;
                }
                if (nv.Open) nv.Why = Runs.ChooseNodeRefusal(c, n.Id);
                v.Nodes.Add(nv);
                foreach (var next in n.Next)
                {
                    var to = RunMap.Node(run, next);
                    var i = path.IndexOf(n.Id);
                    v.Links.Add(new RunLinkView
                    {
                        From = n.Id, To = next,
                        Taken = i >= 0 && i + 1 < path.Count && path[i + 1] == next,
                        Open = run.At == n.Id && choices.Contains(next),
                        Dim = to == null || !RunMap.Visible(c, to),
                    });
                }
            }
            // From the start to the first row.
            foreach (var n in run.Nodes.Where(n => n.Row == 1))
                v.Links.Add(new RunLinkView
                {
                    From = null, To = n.Id,
                    Taken = path.Count > 0 && path[0] == n.Id,
                    Open = run.At == null && choices.Contains(n.Id),
                    Dim = !RunMap.Visible(c, n),
                });

            // The squad: theirs with their health; the chosen one's kit, to sort between stages.
            var squad = run.Squad.Select(id => c.Soldiers.FirstOrDefault(s => s.Id == id)).Where(s => s != null && s.Alive && s.Kit != null).ToList();
            var mine = squad.Where(s => Runs.Controls(c, Slot, Connected, s)).ToList();
            var pick = squad.FirstOrDefault(s => s.Id == chosen) ?? mine.FirstOrDefault() ?? squad.FirstOrDefault();
            foreach (var s in mine.Concat(squad.Except(mine)))
            {
                var own = mine.Contains(s);
                var max = Runs.MaxHp(s);
                v.Squad.Add(new KitTabView
                {
                    Id = s.Id, Name = FirstName(s), On = s == pick, Mine = own, Theirs = s.Owner != Slot,
                    Color = own ? null : PlayerColor(s.Owner),
                    Hp = run.Hp.TryGetValue(s.Id, out var hp) ? hp : max, HpMax = max,
                    Tip = StrategyText.Stash.Health(run.Hp.TryGetValue(s.Id, out var left) ? left : max, max) + (own ? "" : " · " + StrategyText.Stash.Whose(PlayerName(s.Owner))),
                    // A cell to learn now, as Soldados marks it (U10): only one's own, who can spend it.
                    Points = s.Owner == Slot && Campaign.CanSpendPoints(c, s), PointsTip = "Puntos por gastar en el tablero",
                });
            }
            if (pick != null)
            {
                v.Kit = Kit(pick, mine.Contains(pick));
                // Its board between stages (U10): Soldados' key; on one's own, its points and pressing for them (the
                // partner's, only to look at: neither, Producto 18/11).
                var own = pick.Owner == Slot;
                var points = own ? Bars.PointsLeft(pick.Skills, pick.Board) : 0;
                v.Kit.Board = "Tablero de habilidades";
                v.Kit.BoardPoints = points > 0 ? $"{points} {(points == 1 ? "punto" : "puntos")} por gastar" : null;
                v.Kit.BoardPulse = own && Campaign.CanSpendPoints(c, pick);
            }
            v.Stop = Stop(run, pick != null && mine.Contains(pick) ? pick : mine.FirstOrDefault(), mine);
            return v;
        }

        /// <summary>
        /// The stage the squad is going to, on paper, for the loading window (cuadro 136, the C): «Saliendo hacia», its name,
        /// its place drawn, its danger, the map's notes (de noche, mal tiempo, horda despierta; «Sin plaza libre»), its line,
        /// and who goes, each in their player's colour. Which stage: the campaign's once it says the squad is in its fight;
        /// before that word comes (the room's phase first), the one proposed to the vote, else the one this player sent it to
        /// (`chosen`). Null without a stage of an expedition under way.
        /// </summary>
        public Bastion.UI.App.LoadView StageSheet(string chosen)
        {
            var c = State;
            var run = c?.Run;
            var id = run == null ? null : run.Phase == RunPhases.Fight ? run.At : (c.Proposal?.Action as ChooseNodeAction)?.Node ?? chosen;
            var n = id != null ? RunMap.Node(run, id) : null;
            if (n == null || NodeKinds.IsStop(n.Kind)) return null;
            var v = new Bastion.UI.App.LoadView
            {
                Paper = true,
                Kicker = Bastion.UI.App.LoadText.Going,
                Title = StrategyText.Runs.NodeName(n.Kind, run.Chapter),
                Line = StrategyText.Runs.StageLine(n),

            };
            // The first stage of the expedition leaves from the base: La Lata is seen going (cuadro 138).
            v.Departs = n.Row == 1;
            var place = n.Site != null && Places.Names.TryGetValue(n.Site, out var where) ? where : null;
            v.Place = place;
            v.Symbol = n.Site != null && Bastion.UI.Hud.LineIcon.Has("place:" + n.Site) ? "place:" + n.Site : NodeIcon(n.Kind);
            if (n.Kind != NodeKinds.Boss) v.Danger = StrategyText.Runs.Danger(n.Difficulty);
            foreach (var m in new[] { n.Modifier, n.Added }.Where(x => x != null).Distinct())
                v.Notes.Add((Lore.ModifierName(m).ToLowerInvariant(), m == Modifiers.Awake));
            if ((n.Kind == NodeKinds.Rescue || n.Kind == NodeKinds.Bitten) && !Runs.SeatFree(c)) v.Notes.Add((Lore.RescueNoSeatTag, true));
            var two = c.Players.Count > 1;
            foreach (var s in run.Squad.Select(id => c.Soldiers.FirstOrDefault(x => x.Id == id)).Where(s => s != null && s.Alive))
                v.Who.Add((FirstName(s), two ? PlayerColor(s.Owner) : null));
            v.WhoHead = Bastion.UI.App.LoadText.Who(v.Who.Count);
            return v;
        }

        /// <summary>The stop the squad is at (H4): what it offers or asks, what happened, «Seguir»; null if not at one.</summary>
        StopView Stop(RunState run, CampaignSoldier buyer, List<CampaignSoldier> mine)
        {
            var c = State;
            var node = RunMap.Node(run, run.At);
            var stop = run.Stop;
            if (run.Phase != RunPhases.Stop || node == null || stop == null) return null;
            var v = new StopView
            {
                Kind = node.Kind,
                Kicker = StrategyText.Runs.NodeName(node.Kind),
                Title = StrategyText.Runs.NodeName(node.Kind),
                // (U2: a Trueque's line said what its title does; a Refugio's, what its outcome and its key do.)
                Text = node.Kind == NodeKinds.Trade || node.Kind == NodeKinds.Refuge ? null : StrategyText.Runs.NodeLine(node.Kind),
                Leave = StrategyText.Runs.Leave,
                LeaveWhy = Runs.LeaveStopRefusal(c),
            };
            // A Refugio (G22, U21): the two ways out, «Seguir la expedición» first and «Volver a la base» after it; a Trueque
            // too with the Ruta de vuelta (cuadro 165).
            if ((node.Kind == NodeKinds.Refuge || (node.Kind == NodeKinds.Trade && Runs.Level(c, UpgradeIds.Route) > 0)) && run.HomeFromRefuge == true)
            {
                v.Leave = StrategyText.Runs.EndNo;
                v.Home = StrategyText.Runs.Back;
                v.HomeTip = Levels.Of(c).Wounds ? Lore.WoundsText.ReturnHelp : StrategyText.Runs.BackTip;
                v.HomeWhy = Runs.EndRunRefusal(c);
            }
            switch (node.Kind)
            {
                case NodeKinds.Trade:
                    v.Purse = mine.Sum(s => Gears.Supplies(s.Kit.Pack)).ToString();
                    if (buyer != null)
                    {
                        v.For = StrategyText.Runs.For(FirstName(buyer));
                        v.ForName = FirstName(buyer);
                    }
                    v.PurseNote = StrategyText.Runs.PurseOnly;
                    var purse = mine.Sum(s => Gears.Supplies(s.Kit.Pack));
                    foreach (var w in stop.Wares ?? new List<Ware>())
                        v.Wares.Add(new WareView
                        {
                            Gear = Ware(c, w.Gear, buyer),
                            Price = w.Price.ToString(),
                            Why = Gears.BuyWareRefusal(c, Slot, Connected, new BuyWareCommand { Ware = w.Gear.Id, Soldier = buyer?.Id ?? "" }),
                            // (U2: short of supplies, said by the price in red.)
                            Short = w.Price > purse,
                        });
                    // The other side (M10, cuadro 141): what the same soldier wears and carries, at what the trader pays (the
                    // engine's price, «Buen trato» in it) and why not; never what has no price (the knife, §, the key).
                    if (buyer?.Kit != null)
                    {
                        var haggle = Runs.Level(c, UpgradeIds.Haggle) > 0;
                        v.Offers = new List<WareView>();
                        v.OffersFrom = StrategyText.Runs.OffersFrom(FirstName(buyer));
                        foreach (var spot in Gears.OfKit(buyer))
                        {
                            var price = Content.SellPrice(spot.Gear, haggle);
                            // (What the base gave, cuadro 165: shown, off, with why; the rest with no price, not at all.)
                            var given = spot.Gear.Issued == true;
                            if (price <= 0 && !given) continue;
                            v.Offers.Add(new WareView
                            {
                                Gear = Ware(c, spot.Gear, buyer),
                                Price = given ? null : "+" + price,
                                Why = given ? RunTexts.NotForSale : Gears.SellRefusal(c, Slot, Connected, new SellGearCommand { Soldier = buyer.Id, Gear = spot.Gear.Id }),
                                Note = spot.Slot != KitSlots.Pack ? StrategyText.Runs.WornOf(spot.Gear.Kind, spot.Gear.Type, spot.Gear.Count) : null,
                            });
                        }
                        if (v.Offers.Count == 0) v.OffersFrom = StrategyText.Runs.NothingToOffer(FirstName(buyer));
                        if (mine.Count > 1) v.OffersOther = StrategyText.Runs.OffersOther;
                    }
                    break;
                case NodeKinds.Event:
                    var id = stop.Event ?? "";
                    if (Lore.RunEvents.TryGetValue(id, out var told))
                    {
                        v.Title = told.Title;
                        v.Text = told.Text;
                    }
                    else if (RunTexts.Events.TryGetValue(id, out var placeholder)) v.Title = placeholder.Title;
                    if (stop.Outcome == null)
                        for (var i = 0; i < 2; i++)
                            v.Options.Add(new StopOptionView
                            {
                                Index = i,
                                Label = told != null ? told.Options[i].Label : RunTexts.EventOption(id, i),
                                Short = EventShort(id, i),
                                Why = Runs.AnswerStopRefusal(c, i),
                            });
                    break;
            }
            if (stop.Outcome != null || stop.Result != null) v.Outcome = StopOutcome(stop);
            // With two: who has asked to go on.
            var asked = stop.Leaving != null && Slot >= 0 && Slot < stop.Leaving.Length && stop.Leaving[Slot];
            var partner = Partner;
            var theyAsked = partner != null && stop.Leaving != null && partner.Slot >= 0 && partner.Slot < stop.Leaving.Length && stop.Leaving[partner.Slot];
            v.LeaveOn = asked && Voting;
            if (Voting && asked && !theyAsked) v.Waiting = StrategyText.Runs.Waiting(partner.Name);
            else if (Voting && theyAsked && !asked) v.Waiting = StrategyText.Runs.TheyWait(partner.Name);
            return v;
        }

        /// <summary>A ware as the Trueque lists it (Lore.GearName): one, its name in the singular; more, how many and the plural («2 medicinas»).</summary>
        static Gear.GearItemView Ware(CampaignState c, Engine.Gear g, CampaignSoldier buyer)
        {
            // Against the soldier it would go to (T4), as it would have it (O7).
            var v = Item(g, Against(c, buyer));
            v.Name = Lore.GearName(g.Kind, g.Type, g.Count);
            return v;
        }

        /// <summary>«Horda despierta: todos los zombis…»: a modifier's name and its line, for a stage's card.</summary>
        static string ModifierCard(string modifier)
        {
            var said = StrategyText.Runs.ModifierLine(modifier);
            return $"{StrategyText.Runs.ModifierName(modifier)}: {char.ToLowerInvariant(said[0])}{said.Substring(1)}";
        }

        /// <summary>An event's option in short, by its button (Lore.RunEventEffects with Content.RunEvents' figures, H4).</summary>
        static string EventShort(string id, int option)
        {
            if (!Content.RunEvents.TryGetValue(id, out var e) || option >= e.Options.Count) return null;
            var o = e.Options[option];
            // (No recruit from an event any more: «herido» and «radio» mark a Rescate ahead, N3.)
            return Lore.RunEventEffects(o.Supplies, o.HurtOne, o.HurtAll, o.HealAll, o.GearKind, o.GearType, o.GearCount, o.Modifier, rescue: o.Rescue);
        }

        /// <summary>What happened at the stop (H4: StopState.Result, Lore's line with who it fell to and who joined); else the engine's.</summary>
        /// <summary>The places a fight goes to on its own; any other is a special map with its mark.</summary>
        static readonly HashSet<string> OrdinaryPlaces = new HashSet<string> { Places.City, Places.Town, Places.Estate, Places.Factory, Places.Roadside, Places.Country };

        static string StopOutcome(StopState stop)
        {
            var r = stop.Result;
            if (r != null && Lore.RunEvents.TryGetValue(r.Event, out var told) && r.Option >= 0 && r.Option < told.Options.Count)
                return told.Options[r.Option].Result(r.SoldierName, r.SurvivorName) + (r.Lost == true ? " " + Lore.RunEventLost : "");
            return stop.Outcome != null && RunTexts.Outcomes.TryGetValue(stop.Outcome, out var said) ? said : stop.Outcome;
        }

        /// <summary>
        /// Each stage kind's icon and each modifier's (U26, after El herido's came out as an empty ring in the R10): taken from
        /// the engine's constants, not its lists, so that a new kind is in. The editor's NodeIconsCheck looks for each in the
        /// table, on every compile.
        /// </summary>
        public static IEnumerable<(string Kind, string Icon)> StageIcons()
        {
            foreach (var f in typeof(NodeKinds).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.GetValue(null) is string kind) yield return (kind, NodeIcon(kind));
            foreach (var f in typeof(Modifiers).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.GetValue(null) is string modifier) yield return (modifier, ModifierIcon(modifier));
        }

        /// <summary>A stage's icon by kind: the missions' own for today's objectives, Synty's map signs for the rest.</summary>
        static string NodeIcon(string kind)
        {
            switch (kind)
            {
                case NodeKinds.Normal: return "kind:scavenge";
                case NodeKinds.Elimination: case NodeKinds.Recovery: case NodeKinds.Sabotage: case NodeKinds.Rescue: return "kind:" + kind;
                case NodeKinds.Stealth: return "hide";
                default: return "node:" + kind;
            }
        }

        static string ModifierIcon(string modifier)
        {
            switch (modifier)
            {
                case Modifiers.Night: return "night";
                case Modifiers.Storm: return "weather:storm";
                default: return "node:" + modifier;
            }
        }
    }
}
