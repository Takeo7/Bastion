using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The help on hover, which the web gave with `title`: UI Toolkit shows `tooltip` only in the editor, never in the
    /// game (docs/UNITY-UI.md §4). One layer over the HUD: an element gets its words (or a card built on demand, as the
    /// ability's), shown a moment after the pointer enters it, above it and kept inside the screen, gone as it leaves.
    /// </summary>
    public sealed class TooltipLayer
    {
        const long DelayMs = 350;
        /// <summary>With the finger the help is a long press (Lore, 7/11; GameInput.LongTime): held this long, it shows.</summary>
        const long TouchDelayMs = 500;
        const float Gap = 6;
        const string HookedClass = "has-tip";

        readonly VisualElement layer;
        readonly VisualElement box;
        /// <summary>
        /// Each element's help, held by the element (weakly): a dropped one takes it along, and one taken out of its panel
        /// and put back keeps it (a capture moves the whole document to its own panel, after the screens are built).
        /// </summary>
        readonly ConditionalWeakTable<VisualElement, Tip> tipsOf = new ConditionalWeakTable<VisualElement, Tip>();
        VisualElement over;
        /// <summary>The element whose help stays as the pointer leaves it (tests: Pin).</summary>
        VisualElement pinned;
        IVisualElementScheduledItem pending;
        /// <summary>The finger down on an element with help (its pointer), so that once the help shows, lifting it presses nothing.</summary>
        int? heldPointer;

        public TooltipLayer(VisualElement root)
        {
            layer = Ui.El("tooltip-layer", root);
            layer.pickingMode = PickingMode.Ignore;
            box = Ui.El("tooltip", layer);
            box.pickingMode = PickingMode.Ignore;
            // Placed again whenever its size changes: filled outside the panel's update (a pin, new words while open), the
            // scheduled Place may come before its layout.
            box.RegisterCallback<GeometryChangedEvent>(_ => Place());
            Ui.Show(box, false);
            // A panel hidden without being taken down (the board's totals in Soldados and Almacén, Pruebas 24/11): no leave
            // nor detach comes, so its help is watched and goes with it; with the finger too.
            layer.schedule.Execute(Watch).Every(WatchMs);
        }

        const long WatchMs = 150;

        /// <summary>
        /// The help shown goes once its element is out of sight: out of its panel, or it or one round it hidden (a test's
        /// pinned one too).
        /// </summary>
        void Watch()
        {
            if (over == null || Shown(over)) return;
            Hide();
        }

        static bool Shown(VisualElement e)
        {
            if (e.panel == null) return false;
            for (var x = e; x != null; x = x.parent)
                if (x.resolvedStyle.display == DisplayStyle.None || x.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }

        /// <summary>Back over everything (a layer added after it, as the game's menu).</summary>
        public void Front() => layer.BringToFront();

        /// <summary>`target` says `text` on hover (empty: nothing). Called again, it replaces the words.</summary>
        public void Attach(VisualElement target, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                tipsOf.Remove(target);
                return;
            }
            Hook(target);
            var tip = tipsOf.GetOrCreateValue(target);
            tip.Text = text;
            tip.Card = null;
            if (over == target) Fill();
        }

        /// <summary>`target` shows the card `build` makes on hover (the ability's: name, cost, figures, why not).</summary>
        public void Attach(VisualElement target, Func<VisualElement> build)
        {
            Hook(target);
            var tip = tipsOf.GetOrCreateValue(target);
            tip.Text = null;
            tip.Card = build;
            if (over == target) Fill();
        }

        /// <summary>
        /// Tests: `target`'s help shown now, without the wait, and kept wherever the pointer goes. In a batch capture the
        /// editor's own pointer lets a synthetic enter go, or enters another element with help.
        /// </summary>
        public void Pin(VisualElement target)
        {
            pinned = target;
            over = target;
            pending?.Pause();
            Fill();
        }

        public void Hide()
        {
            pending?.Pause();
            over = null;
            pinned = null;
            Ui.Show(box, false);
        }

        /// <summary>Its callbacks once (marked on the element itself).</summary>
        void Hook(VisualElement target)
        {
            if (target.ClassListContains(HookedClass)) return;
            target.AddToClassList(HookedClass);
            target.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (pinned == null) Enter(target);
            });
            // With the finger: which finger is down on it (Fill lets its press go once the help shows).
            if (Bastion.Client.GameInput.Touch)
            {
                target.RegisterCallback<PointerDownEvent>(e => heldPointer = e.pointerId, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerUpEvent>(_ => heldPointer = null, TrickleDown.TrickleDown);
            }
            target.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (over == target && pinned != target) Hide();
            });
            target.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (pinned == target) pinned = null;
                if (over == target) Hide();
            });
        }

        void Enter(VisualElement target)
        {
            over = target;
            pending?.Pause();
            pending = layer.schedule.Execute(() =>
            {
                if (over != target) return;
                Fill();
                // With the finger the help was a long press: lifting it must not press what is under it (its press let go).
                if (Bastion.Client.GameInput.Touch && heldPointer is int id && target.panel?.GetCapturingElement(id) is IEventHandler holder)
                    holder.ReleasePointer(id);
            }).StartingIn(Bastion.Client.GameInput.Touch ? TouchDelayMs : DelayMs);
        }

        void Fill()
        {
            box.Clear();
            var tip = over != null && tipsOf.TryGetValue(over, out var t) ? t : null;
            if (tip?.Card != null) box.Add(tip.Card());
            else if (!string.IsNullOrEmpty(tip?.Text)) Ui.Text(tip.Text, "tooltip__text", box);
            else
            {
                Ui.Show(box, false);
                return;
            }
            Ui.Show(box, true);
            // Placed once its size is known: above the target, centred on it, inside the screen; below if no room above.
            box.style.visibility = Visibility.Hidden;
            box.schedule.Execute(Place);
        }

        /// <summary>
        /// Elements tips stay clear of, going up over them (the picked one's sheet over the cards: Producto, 18/11, a bust's
        /// help lay on it and both read mixed; U27, the notes and the bar's «Turno de Ana…» plate under a key's help); not
        /// for their own. Asked for as each tip is placed: some are made again as they change (the bar's plate).
        /// </summary>
        public readonly List<Func<VisualElement>> KeepClear = new List<Func<VisualElement>>();

        void Place()
        {
            if (over == null) return;
            var t = over.worldBound;
            var size = box.layout.size;
            // Not laid out yet: its GeometryChangedEvent comes.
            if (!(size.x > 0)) return;
            var screen = layer.layout;
            var x = Mathf.Clamp(t.center.x - size.x / 2, Gap, Mathf.Max(Gap, screen.width - size.x - Gap));
            var y = t.yMin - size.y - Gap;
            // Up over each in the way; twice, as going over one may land on another.
            for (var pass = 0; pass < 2; pass++)
                foreach (var of in KeepClear)
                {
                    var keep = of?.Invoke();
                    if (keep == null || keep.resolvedStyle.display == DisplayStyle.None || keep.panel == null || keep.Contains(over)) continue;
                    var c = keep.worldBound;
                    if (!(c.height > 0)) continue;
                    if (x < c.xMax && x + size.x > c.xMin && y < c.yMax && y + size.y > c.yMin) y = c.yMin - size.y - Gap;
                }
            if (y < Gap) y = t.yMax + Gap;
            box.style.left = x;
            box.style.top = y;
            box.style.visibility = Visibility.Visible;
        }

        sealed class Tip
        {
            public string Text;
            public Func<VisualElement> Card;
        }
    }
}
