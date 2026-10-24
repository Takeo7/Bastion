using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The action bar, bottom centre (hud.ts setAbilities; docs/UNITY-UI.md §2.3): a key per ability, the icon alone
    /// (Ana 22/10 #35); its card on hover (name, key, figures, what it does, why not now); a menu (Lanzar, Vigilancia,
    /// Investigar) is one key when it has one choice, and opens its choices above it, numbered, when it has more.
    /// </summary>
    public sealed class ActionBar
    {
        readonly VisualElement host;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        string key;
        ActionBarView last;
        /// <summary>The menu open, and its choices as drawn (the number keys pick them while it is open).</summary>
        string menu;
        readonly List<AbilityView> menuChoices = new List<AbilityView>();

        /// <param name="icon">The sprite of an ability's icon by its id (or a weapon's, for Disparar); null leaves it blank.</param>
        public ActionBar(VisualElement host, IHudHandlers handlers, TooltipLayer tips, Func<string, Sprite> icon)
        {
            this.host = host;
            this.handlers = handlers;
            this.tips = tips;
            this.icon = icon;
            // A menu closes with a press anywhere else (hud.ts onPressAnywhere): on the panel's root, once it has one.
            if (host.panel != null) Listen(host.panel);
            else host.RegisterCallback<AttachToPanelEvent>(e => Listen(e.destinationPanel));
        }

        void Listen(IPanel panel)
        {
            panel.visualTree.RegisterCallback<PointerDownEvent>(PressAnywhere, TrickleDown.TrickleDown);
        }

        public bool MenuOpen => menu != null;

        public void Set(ActionBarView view)
        {
            var k = Ui.Key(view) + "|" + menu;
            if (k == key) return;
            key = k;
            last = view;
            Draw();
        }

        /// <summary>The open menu's n-th choice (from 1), as a click on it; whether there was one.</summary>
        public bool PickMenu(int n)
        {
            if (menu == null || n < 1 || n > menuChoices.Count) return false;
            var id = menuChoices[n - 1].Id;
            CloseMenu();
            handlers.OnAbility(id);
            return true;
        }

        /// <summary>A menu of the bar opened (or closed) by its key's number.</summary>
        public void ToggleMenu(string id)
        {
            menu = menu == id ? null : id;
            Redraw();
        }

        public bool CloseMenu()
        {
            if (menu == null) return false;
            menu = null;
            Redraw();
            return true;
        }

        void Redraw()
        {
            key = null;
            Set(last);
        }

        void Draw()
        {
            host.Clear();
            menuChoices.Clear();
            var view = last;
            if (view == null) return;
            host.EnableInClassList("abilities--locked", !string.IsNullOrEmpty(view.Lock));
            if (!string.IsNullOrEmpty(view.Lock)) Ui.Text(view.Lock, "abilities__lock", host).pickingMode = PickingMode.Ignore;
            var keys = new List<AbilityView>();
            var choices = new Dictionary<string, List<AbilityView>>();
            foreach (var a in view.Abilities)
            {
                if (string.IsNullOrEmpty(a.Menu)) keys.Add(a);
                else
                {
                    if (!choices.TryGetValue(a.Menu, out var list)) choices[a.Menu] = list = new List<AbilityView>();
                    list.Add(a);
                }
            }
            // A menu whose choices are gone (another soldier, the turn over), down to one, or off, closes.
            if (menu != null && (!choices.TryGetValue(menu, out var open) || open.Count < 2 || !keys.Exists(x => x.Id == menu && x.Enabled))) menu = null;
            foreach (var a in keys)
            {
                choices.TryGetValue(a.Id, out var options);
                var btn = Key(a, () => Press(a, options));
                if (a.Id != menu)
                {
                    host.Add(btn);
                    continue;
                }
                btn.AddToClassList("ability--active");
                var slot = Ui.El("ability-slot", host);
                slot.Add(btn);
                var list = Ui.El("ability-menu panel", slot);
                for (var i = 0; i < options.Count; i++)
                {
                    var o = options[i];
                    menuChoices.Add(o);
                    var numbered = Copy(o, (i + 1).ToString());
                    list.Add(Key(numbered, () =>
                    {
                        menu = null;
                        handlers.OnAbility(o.Id);
                        Redraw();
                    }));
                }
            }
        }

        void Press(AbilityView a, List<AbilityView> options)
        {
            if (options == null || options.Count == 0)
            {
                handlers.OnAbility(a.Id);
                return;
            }
            if (options.Count == 1)
            {
                handlers.OnAbility(options[0].Id);
                return;
            }
            menu = menu == a.Id ? null : a.Id;
            Redraw();
        }

        void PressAnywhere(PointerDownEvent e)
        {
            if (menu == null) return;
            if (e.target is VisualElement v && host.Contains(v)) return;
            CloseMenu();
        }

        Button Key(AbilityView a, Action onClick)
        {
            var tree = Ui.Clone("AbilityButton");
            var btn = tree.Q<Button>("key");
            btn.clicked += onClick;
            btn.SetEnabled(a.Enabled);
            btn.EnableInClassList("ability--active", a.Active);
            btn.EnableInClassList("ability--free", a.Free);
            btn.EnableInClassList("ability--depleted", a.Depleted);
            var sprite = icon?.Invoke(!string.IsNullOrEmpty(a.Weapon) ? "weapon:" + a.Weapon : a.Icon ?? a.Id);
            if (sprite != null) btn.Q("icon").style.backgroundImage = new StyleBackground(sprite);
            var note = btn.Q<Label>("note");
            Ui.Show(note, !a.Enabled && !string.IsNullOrEmpty(a.Note));
            note.text = a.Note ?? "";
            var count = btn.Q<Label>("count");
            Ui.Show(count, a.Count >= 0);
            count.text = a.Count >= 0 ? "×" + a.Count : "";
            var cd = btn.Q<Label>("cooldown");
            Ui.Show(cd, a.Cooldown > 0);
            cd.text = a.Cooldown > 0 ? a.Cooldown.ToString() : "";
            Ui.Show(btn.Q("stamp"), a.Depleted);
            tips.Attach(btn, () => Card(a));
            // The button out of its template container: the bar lays out buttons, not containers.
            btn.RemoveFromHierarchy();
            return btn;
        }

        /// <summary>What hovering a key tells before anything is pressed (hud.ts abilityCard).</summary>
        static VisualElement Card(AbilityView a)
        {
            var card = Ui.El("ability-card");
            var head = Ui.El("ac-head", card);
            Ui.Text(a.Name, "ac-head__name font-title", head);
            if (!string.IsNullOrEmpty(a.Hotkey)) Ui.Text(a.Hotkey, "ac-head__key", head);
            if (a.Stats.Count > 0)
            {
                var stats = Ui.El("ac-stats", card);
                foreach (var s in a.Stats)
                {
                    var row = Ui.El("ac-stat", stats);
                    Ui.Text(s.Key.ToUpperInvariant(), "ac-stat__k", row);
                    Ui.Text(s.Value, "ac-stat__v", row);
                }
            }
            else
            {
                var parts = new List<string>();
                foreach (var p in new[] { a.Cost, a.Limits, a.Noise })
                    if (!string.IsNullOrEmpty(p)) parts.Add(p);
                if (parts.Count > 0) Ui.Text(string.Join(" · ", parts), "ac-cost", card);
                if (!string.IsNullOrEmpty(a.Damage)) Ui.Text(a.Damage, "ac-dmg", card);
            }
            Ui.Text(a.Description, "ac-desc", card);
            if (!string.IsNullOrEmpty(a.Reason)) Ui.Text(a.Reason, "ac-why", card);
            return card;
        }

        /// <summary>A menu's choice with the number it is picked by in place of its own key.</summary>
        static AbilityView Copy(AbilityView a, string hotkey)
        {
            var c = JsonUtility.FromJson<AbilityView>(JsonUtility.ToJson(a));
            c.Hotkey = hotkey;
            return c;
        }
    }
}
