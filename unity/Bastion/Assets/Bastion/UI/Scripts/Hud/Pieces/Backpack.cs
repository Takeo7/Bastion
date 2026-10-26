using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The backpack at the foot of the soldier's panel (hud.ts backpack, packMenuEl; docs/FASE2.md, FASE8.md §C): one slot
    /// per thing carried, § for suministros, the thing's icon otherwise; what lies on its tile, a fallen soldier's
    /// rucksack with one order that takes what fits; what the whole squad carries. One of the player's own takes clicks: a
    /// slot opens its menu over the panel (Soltar, Usar, Curar a, Dar a, Equipar, Ponerse, Al cinturón), a thing on the
    /// floor is picked up, or, with the backpack full, asks which slot to leave for it. The menu closes with ✕, its slot
    /// again, an order, Esc (Close) or a press anywhere else.
    /// </summary>
    public sealed class Backpack
    {
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        VisualElement host;
        string unit;
        PackView pack;
        /// <summary>The menu open: a slot's (its index), or the swap for a thing on the floor (its id); neither, -1 and null.</summary>
        int menuSlot = -1;
        string menuSwap;
        VisualElement menu;

        public Backpack(IHudHandlers handlers, TooltipLayer tips, Func<string, Sprite> icon)
        {
            this.handlers = handlers;
            this.tips = tips;
            this.icon = icon;
        }

        /// <summary>Whether its menu is open.</summary>
        public bool MenuOpen => menu != null;

        /// <summary>`pack` of `unit` drawn into `into` (the panel's own holder, made anew with the panel); null, nothing.</summary>
        public void Set(VisualElement into, string unit, PackView pack)
        {
            // A menu open on another soldier closes when this one is shown.
            if (this.unit != unit) Shut();
            host = into;
            this.unit = unit;
            this.pack = pack;
            Draw();
        }

        /// <summary>The swap for `id` on the floor opened (the notice's «Cambiar»): what to leave to take it.</summary>
        public void OpenSwap(string unit, string id)
        {
            if (unit != this.unit) return;
            menuSlot = -1;
            menuSwap = id;
            Draw();
        }

        /// <summary>Closes the menu (Esc); whether one was open.</summary>
        public bool Close()
        {
            if (menu == null) return false;
            Shut();
            Draw();
            return true;
        }

        /// <summary>A press on the HUD (its root's) or the map (null): outside the backpack, its menu closes.</summary>
        public void PressedAt(VisualElement target)
        {
            if (menu == null || host == null) return;
            for (var e = target; e != null; e = e.parent)
                if (e == host) return;
            Close();
        }

        void Shut()
        {
            menuSlot = -1;
            menuSwap = null;
        }

        void Draw()
        {
            if (host == null) return;
            host.Clear();
            menu = null;
            Ui.Show(host, pack != null);
            if (pack == null) return;
            tips.Attach(host, pack.Tip);
            var full = pack.Slots.Count >= pack.Size;

            // What lies on its tile: a click picks it up, or, with the backpack full, asks what to leave for it.
            if (pack.Live && pack.Floor.Count > 0)
            {
                var floor = Ui.El("pack-floor", host);
                Ui.Text(HudText.Pack.Floor, "pack-floor__title", floor);
                // A fallen soldier's rucksack (#49): its name, and one order that takes what fits.
                foreach (var f in pack.Fallen)
                {
                    var row = Ui.El("pack-fallen", floor);
                    Ui.Text(f.Mark, "pack-fallen__mark font-title", row);
                    var ids = f.Ids;
                    var take = new Button(() =>
                    {
                        Shut();
                        Draw();
                        handlers.OnTakeAll(ids);
                    }) { text = f.Action };
                    take.AddToClassList("pack-fallen__take");
                    take.SetEnabled(!f.Off);
                    tips.Attach(take, f.Tip);
                    row.Add(take);
                }
                foreach (var f in pack.Floor)
                {
                    var thing = f;
                    var swapping = menuSwap == thing.Id;
                    var item = new Button(() =>
                    {
                        if (!full)
                        {
                            Shut();
                            Draw();
                            handlers.OnItem(new PackOrder { Action = "take", Id = thing.Id });
                            return;
                        }
                        menuSlot = -1;
                        menuSwap = swapping ? null : thing.Id;
                        Draw();
                    });
                    item.AddToClassList("pack-floor-item");
                    item.AddToClassList("pack-floor-item--" + thing.Kind);
                    item.EnableInClassList("pack-floor-item--open", swapping);
                    Ui.Text(thing.Label, "pack-floor-item__label", item);
                    Ui.Text(full ? HudText.Pack.Swap : HudText.Pack.Take, "pack-floor-item__act", item);
                    // The partner holds the command: it can't be picked up yet, and says why.
                    item.SetEnabled(string.IsNullOrEmpty(pack.Locked));
                    tips.Attach(item, string.IsNullOrEmpty(pack.Locked) ? HudText.Pack.FloorTip(thing.Label, full) : pack.Locked);
                    floor.Add(item);
                }
            }

            var line = Ui.El("pack-line", host);
            var slots = Ui.El("pack-slots", line);
            for (var i = 0; i < pack.Size; i++) slots.Add(Slot(i));
            var squad = Ui.El("pack-squad", line);
            tips.Attach(squad, pack.SquadTip);
            Ui.Text(HudText.Pack.Squad, "pack-squad__title", squad);
            if (pack.Totals.Count == 0) Ui.Text(HudText.Pack.Nothing, "pack-squad__none", squad);
            else
            {
                var totals = Ui.El("pack-totals", squad);
                foreach (var t in pack.Totals)
                {
                    var e = Ui.El("pack-total pack-total--" + t.Kind, totals);
                    if (!t.IconAfter && !string.IsNullOrEmpty(t.Icon)) Ui.Icon(icon, t.Icon, "pack-total__icon", e);
                    Ui.Text(t.Text, "pack-total__n font-title", e);
                    if (t.IconAfter && !string.IsNullOrEmpty(t.Icon)) Ui.Icon(icon, t.Icon, "pack-total__icon pack-total__icon--after", e);
                }
            }

            menu = Menu();
            if (menu != null) host.Add(menu);
        }

        /// <summary>Slot `i`: its thing (a button on one's own, opening its menu), or a free one.</summary>
        VisualElement Slot(int i)
        {
            var s = i < pack.Slots.Count ? pack.Slots[i] : null;
            var look = Ui.El("pack-slot");
            if (s == null)
            {
                look.AddToClassList("pack-slot--empty");
                tips.Attach(look, HudText.Pack.Free);
                return look;
            }
            look.AddToClassList("pack-slot--" + s.Kind);
            look.pickingMode = PickingMode.Ignore;
            // The number and a drawn icon: rations and parts the number first; supplies only theirs («12§»).
            var after = s.Kind == "food" || s.Kind == "parts";
            if (!after && !string.IsNullOrEmpty(s.Icon)) Ui.Icon(icon, s.Icon, "pack-slot__icon pack-slot__icon--" + s.Kind, look);
            if (!string.IsNullOrEmpty(s.Text)) Ui.Text(s.Text, "pack-slot__n font-title", look).pickingMode = PickingMode.Ignore;
            if (after && !string.IsNullOrEmpty(s.Icon)) Ui.Icon(icon, s.Icon, "pack-slot__icon pack-slot__icon--small", look);
            foreach (var child in look.Children()) child.pickingMode = PickingMode.Ignore;
            if (!pack.Live)
            {
                look.pickingMode = PickingMode.Position;
                tips.Attach(look, s.Tip);
                return look;
            }
            // Its own soldier's: a click opens what can be done with it.
            var open = menuSlot == i;
            var button = new Button(() =>
            {
                menuSwap = null;
                menuSlot = open ? -1 : i;
                Draw();
            });
            button.AddToClassList("pack-slot-btn");
            button.EnableInClassList("pack-slot-btn--open", open);
            button.Add(look);
            var verbs = string.Join(", ", s.Acts.Select(a => a.Label.Split(' ')[0].ToLowerInvariant()).Distinct());
            tips.Attach(button, $"{s.Tip}{(verbs.Length > 0 ? $" · {verbs}" : "")}");
            return button;
        }

        /// <summary>The open menu, over the backpack: a slot's orders, or which slot to leave for a thing on the floor.</summary>
        VisualElement Menu()
        {
            if (!pack.Live || (menuSlot < 0 && menuSwap == null)) return null;
            var box = Ui.El("pack-menu");
            var head = Ui.El("pack-menu__head", box);
            var title = Ui.Text("", "pack-menu__title font-title", head);
            var x = new Button(() =>
            {
                Shut();
                Draw();
            }) { text = "✕" };
            x.AddToClassList("pack-menu__x");
            tips.Attach(x, HudText.Pack.Close);
            head.Add(x);
            if (menuSlot >= 0)
            {
                if (menuSlot >= pack.Slots.Count) return null;
                var s = pack.Slots[menuSlot];
                title.text = s.Label;
                foreach (var a in s.Acts)
                {
                    var act = a;
                    Act(box, act.Label, string.IsNullOrEmpty(act.Off) ? act.Cost : act.Off, act.Main, string.IsNullOrEmpty(act.Off), () =>
                    {
                        Shut();
                        Draw();
                        handlers.OnItem(act.Order);
                    });
                }
            }
            else
            {
                var f = pack.Floor.FirstOrDefault(t => t.Id == menuSwap);
                if (f == null) return null;
                title.text = HudText.Pack.SwapTitle(f.Name);
                var take = f.Id;
                for (var i = 0; i < pack.Slots.Count; i++)
                {
                    var drop = i;
                    Act(box, HudText.Pack.Leave(pack.Slots[i].Name), HudText.Pack.Gratis, false, true, () =>
                    {
                        Shut();
                        Draw();
                        handlers.OnSwap(take, drop);
                    });
                }
            }
            // Riveted steel, as the bar's menus (mixta-hud.css .pack-menu).
            PlateFrame.Dress(box, false);
            return box;
        }

        static void Act(VisualElement box, string label, string cost, bool main, bool on, Action pick)
        {
            var b = new Button(pick);
            b.AddToClassList("pack-act");
            b.EnableInClassList("pack-act--main", main);
            Ui.Text(label, "pack-act__label", b);
            Ui.Text(cost, "pack-act__cost", b);
            b.SetEnabled(on);
            box.Add(b);
        }
    }
}
