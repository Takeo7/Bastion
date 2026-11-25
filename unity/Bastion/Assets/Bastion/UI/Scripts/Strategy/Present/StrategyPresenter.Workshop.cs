using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The Taller and the Chatarrero (cuadros 164 and 165, tanda 2): the base's tab beside the Almacén (BaseViews.Engineering,
    /// the 0.10.1's Armería), there with either upgrade. The Taller's wares up to its level, each made with the stash's §
    /// (WorkshopCommand, Gears.WorkshopRefusal); the stash's gear the Chatarrero takes (Content.ScrapPrice over 0), each with
    /// what it pays (ScrapGearCommand, Gears.ScrapRefusal). The stash's grid stays on the left, where both show.
    /// </summary>
    public sealed partial class StrategyPresenter
    {
        /// <summary>The tab's there: at the base of the runs, the Taller or the Chatarrero bought.</summary>
        public bool HasWorkshop => State?.Stash != null && (Runs.Level(State, UpgradeIds.Workshop) > 0 || Runs.Level(State, UpgradeIds.Scrap) > 0);

        /// <summary>The tab's name: the Taller's, or the Chatarrero's with that one alone.</summary>
        public string WorkshopTab => Runs.Level(State, UpgradeIds.Workshop) > 0 ? StrategyText.Runs.WorkshopHead : StrategyText.Runs.ScrapHead;

        public WorkshopView Workshop()
        {
            var c = State;
            if (c?.Stash == null) return null;
            var v = new WorkshopView();
            var level = Runs.Level(c, UpgradeIds.Workshop);
            if (level > 0)
            {
                v.MakesHead = StrategyText.Runs.WorkshopHead;
                v.Makes = new List<WareView>();
                var have = Gears.Supplies(c.Stash);
                foreach (var id in WorkshopIds.All)
                {
                    if (!Content.WorkshopWares.TryGetValue(id, out var w) || w.Level > level) continue;
                    // (Its id the ware's: the key orders it, WorkshopCommand.Ware.)
                    var item = Item(new Engine.Gear { Id = id, Kind = w.Kind, Type = w.Type, Count = w.Count });
                    item.Name = Lore.GearName(w.Kind, w.Type, w.Count);
                    v.Makes.Add(new WareView
                    {
                        Gear = item,
                        Price = w.Price.ToString(),
                        Why = Gears.WorkshopRefusal(c, new WorkshopCommand { Ware = id }),
                        // (Short of §: the price red, why on hover, as the Trueque's; U2.)
                        Short = w.Price > have,
                    });
                }
            }
            if (Runs.Level(c, UpgradeIds.Scrap) > 0)
            {
                v.OffersHead = StrategyText.Runs.ScrapHead;
                v.OffersSub = StrategyText.Runs.Offers;
                v.Offers = new List<WareView>();
                // As the grid reads, row by row; only what it takes (no §, knife, mission's or base's things: Lore, «sin frases»).
                foreach (var p in c.Stash.Items.OrderBy(p => p.Y).ThenBy(p => p.X))
                {
                    var price = Content.ScrapPrice(p.Gear);
                    if (price <= 0) continue;
                    var item = Item(p.Gear);
                    item.Name = Lore.GearName(p.Gear.Kind, p.Gear.Type, p.Gear.Count);
                    v.Offers.Add(new WareView
                    {
                        Gear = item,
                        Price = "+" + price,
                        Why = Gears.ScrapRefusal(c, new ScrapGearCommand { Gear = p.Gear.Id }),
                    });
                }
                if (v.Offers.Count == 0) v.OffersNone = StrategyText.Runs.ScrapNone;
            }
            return v;
        }
    }
}
