using System;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The Taller and the Chatarrero (cuadros 164 and 165, tanda 2), on the right of the Almacén's grid, where the kit is on
    /// the Almacén: a plate as the Trueque's (StopPlate's rows), no sentences (Producto). The Taller's wares, each its picture,
    /// its name and «HACER 15 [caja]», the price on the key as Mejoras' (Lore); short of §, the key off and the price red,
    /// why on hover; another why under the name («No cabe en el almacén.»). Under it the Chatarrero's: «LO QUE LE OFRECES»,
    /// what of the stash it takes, each with what it pays in green and «Cambiar». The list scrolls, kept where it was when
    /// drawn again.
    /// </summary>
    public sealed class WorkshopScreen
    {
        readonly VisualElement root;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        ScrollView list;
        Vector2 listAt;
        bool shown;
        WorkshopView view;
        /// <summary>
        /// When a key last went (the real clock): none heeds a press for a moment after, so the second click of a double click
        /// neither sells the row that comes up under it (as the Trueque's, the 0.14.4's review) nor makes and pays for a
        /// second one (the 0.14.10's independent review, 1).
        /// </summary>
        float pressedAt = -10;
        const float Grace = 0.5f;

        /// <summary>The press, unless one went a moment ago.</summary>
        void Press(Action order)
        {
            if (Time.realtimeSinceStartup - pressedAt < Grace) return;
            pressedAt = Time.realtimeSinceStartup;
            order();
        }

        public WorkshopScreen(VisualElement parent, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            root = Ui.El("strat-side strat-side--right panel workshop", parent);
            Show(false);
        }

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(root, on && view != null);
        }

        public void Set(WorkshopView v)
        {
            view = v;
            if (list != null) listAt = list.scrollOffset;
            root.Clear();
            Ui.Show(root, shown && v != null);
            if (v == null) return;
            list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("workshop__list");
            root.Add(list);
            if (v.Makes != null)
            {
                Ui.Text((v.MakesHead ?? "").ToUpperInvariant(), "workshop__head font-title", list);
                foreach (var w in v.Makes) Row(list, w, false);
            }
            if (v.Offers != null)
            {
                Ui.Text((v.OffersHead ?? "").ToUpperInvariant(), "workshop__head font-title" + (v.Makes != null ? " workshop__head--next" : ""), list);
                Ui.Text((v.OffersSub ?? "").ToUpperInvariant(), "runstop__side-head workshop__sub font-title", list);
                if (!string.IsNullOrEmpty(v.OffersNone)) Ui.Text(v.OffersNone, "workshop__none muted", list);
                foreach (var w in v.Offers) Row(list, w, true);
            }
            // Where it was, once laid out (a thing made or sold draws it anew).
            var at = listAt;
            void Back(GeometryChangedEvent e)
            {
                list.scrollOffset = at;
                list.UnregisterCallback<GeometryChangedEvent>(Back);
            }
            list.RegisterCallback<GeometryChangedEvent>(Back);
        }

        void Row(VisualElement into, WareView w, bool offered)
        {
            var row = Ui.El("runstop__ware workshop__row", into);
            var tile = Ui.El("runstop__tile gear-item--" + (w.Gear.Kind ?? "thing"), row);
            var id = w.Gear.Icon;
            // Arte's picture, as in the grids; else the HUD's icon.
            if (Bastion.Client.GearPictures.Of(w.Gear.Kind, w.Gear.Type) is Texture2D art)
            {
                var pic = Ui.El("runstop__art", tile);
                pic.pickingMode = PickingMode.Ignore;
                pic.style.backgroundImage = new StyleBackground(art);
            }
            else if (id != null && id.Length == 1) Ui.Text(id, "runstop__glyph font-title", tile);
            else if (LineIcon.Has(id)) tile.Add(new LineIcon(id) { pickingMode = PickingMode.Ignore });
            else Ui.Icon(icon, id, "runstop__icon", tile);
            var words = Ui.El("runstop__ware-words", row);
            tips.Attach(Ui.Text(w.Gear.Name, "runstop__ware-name", words), w.Gear.Tip);
            // Short of §, said by the price in red (U2); another reason stays written (B18).
            if (w.Why != null && !w.Short) Ui.Text(w.Why, "runstop__ware-why", words);
            var ware = w.Gear.Id;
            Button b;
            if (!offered)
            {
                // «HACER 15 [caja]»: its word and its price on the key, as Mejoras' (Lore's TEXTOS.md).
                b = new Button(() => Press(() => handlers.OnWorkshop(ware)));
                Ui.AddClasses(b, "upgrade__button workshop__make font-title");
                Ui.Text(StrategyText.Runs.Make.ToUpperInvariant(), "upgrade__button-text", b).pickingMode = PickingMode.Ignore;
                SuppliesMark.Make(b, w.Price, "supplies-mark--small upgrade__price" + (w.Short ? " workshop__price--short" : "")).pickingMode = PickingMode.Ignore;
            }
            else
            {
                SuppliesMark.Make(row, w.Price, "supplies-mark--small runstop__price runstop__price--gain");
                b = new Button(() => Press(() => handlers.OnScrap(ware))) { text = StrategyText.Runs.Swap };
                Ui.AddClasses(b, "runstop__buy font-title");
            }
            row.Add(b);
            if (w.Why != null)
            {
                b.SetEnabled(false);
                tips.Attach(b, w.Why);
            }
        }
    }
}
