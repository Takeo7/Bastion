using System.Collections.Generic;
using System.Linq;
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
                Line = choosing ? (choices.Count > 0 ? StrategyText.Runs.Choose : StrategyText.Runs.NoWay) : null,
                Start = StrategyText.Runs.Start,
                AtStart = run.At == null,
                Back = StrategyText.Runs.Back,
                BackTip = StrategyText.Runs.BackTip,
                BackWhy = Runs.EndRunRefusal(c),
                SquadHead = StrategyText.Runs.Squad,
            };
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
                    // Its line with what is known of it (Runs.StageLine: the alarm, the Asedio's turn, the Guarida's guard).
                    nv.Card.Add(Runs.StageLine(n));
                    // A Rescate with La Lata full (N2, 17/11): whoever doesn't fit is lost when it leaves, said before choosing it.
                    if (n.Kind == NodeKinds.Rescue && !Runs.SeatFree(c))
                    {
                        nv.Card.Add(Lore.RescueNoSeat);
                        nv.Warn = Lore.RescueNoSeatTag;
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
                    }
                    // What an event left (H4): on the stage it went to, or, still waiting (RunState.Pending), on the fights
                    // that can be chosen now, one of which takes it.
                    var added = n.Added ?? (choices.Contains(n.Id) && RunMap.IsFight(n.Kind) ? run.Pending : null);
                    if (added != null)
                    {
                        nv.AddedId = added;
                        nv.AddedIcon = ModifierIcon(added);
                        nv.Card.Add(ModifierCard(added) + StrategyText.Runs.ByEvent);
                    }
                }
                else nv.Card.Add(StrategyText.Runs.Unseen);
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
                    Id = s.Id, Name = FirstName(s), On = s == pick, Mine = own,
                    Color = own ? null : PlayerColor(s.Owner),
                    Hp = run.Hp.TryGetValue(s.Id, out var hp) ? hp : max, HpMax = max,
                    Tip = StrategyText.Stash.Health(run.Hp.TryGetValue(s.Id, out var left) ? left : max, max) + (own ? "" : " · " + StrategyText.Stash.Whose(PlayerName(s.Owner))),
                });
            }
            if (pick != null) v.Kit = Kit(pick, mine.Contains(pick));
            v.Stop = Stop(run, pick != null && mine.Contains(pick) ? pick : mine.FirstOrDefault(), mine);
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
                Text = StrategyText.Runs.NodeLine(node.Kind),
                Leave = StrategyText.Runs.Leave,
                LeaveWhy = Runs.LeaveStopRefusal(c),
            };
            switch (node.Kind)
            {
                case NodeKinds.Trade:
                    v.Purse = mine.Sum(s => Gears.Supplies(s.Kit.Pack)).ToString();
                    if (buyer != null) v.For = StrategyText.Runs.For(FirstName(buyer));
                    foreach (var w in stop.Wares ?? new List<Ware>())
                        v.Wares.Add(new WareView
                        {
                            Gear = Ware(w.Gear),
                            Price = w.Price.ToString(),
                            Why = Gears.BuyWareRefusal(c, Slot, Connected, new BuyWareCommand { Ware = w.Gear.Id, Soldier = buyer?.Id ?? "" }),
                        });
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
        static Gear.GearItemView Ware(Engine.Gear g)
        {
            var v = Item(g);
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

        /// <summary>A stage's icon by kind: the missions' own for today's objectives, Synty's map signs for the rest.</summary>
        static string NodeIcon(string kind)
        {
            switch (kind)
            {
                case NodeKinds.Normal: return "kind:scavenge";
                case NodeKinds.Elimination: case NodeKinds.Recovery: case NodeKinds.Sabotage: case NodeKinds.Hack: case NodeKinds.Rescue: return "kind:" + kind;
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
