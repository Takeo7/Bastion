using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The aim's panel (hud.ts setTarget/fillTarget; docs/UNITY-UI.md §2.4). An attack: the target with its arrows, the
    /// odds on the dial and the crit beside it, the damage, the modifiers, graze, noise, fire and cancel. A support
    /// ability: target, effect, damage. A self one armed by its first press: cost, effect and how to use it, right over
    /// the key that was pressed (placeTarget). Without the command the button is off and says who holds it.
    /// </summary>
    public sealed class ShotPanel
    {
        readonly VisualElement host;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        readonly HitDial dial = new HitDial();
        string key;
        /// <summary>The key a self ability's panel sits over, while armed.</summary>
        VisualElement anchorKey;

        public ShotPanel(VisualElement host, IHudHandlers handlers, TooltipLayer tips)
        {
            this.host = host;
            this.handlers = handlers;
            this.tips = tips;
            Ui.CloneInto("ShotPanel", host);
            host.Q("dial-holder").Add(dial);
            host.Q<Button>("prev").clicked += () => handlers.OnCycleTarget(-1);
            host.Q<Button>("next").clicked += () => handlers.OnCycleTarget(1);
            host.Q<Button>("fire").clicked += () => handlers.OnConfirmTarget();
            var cancel = host.Q<Button>("cancel");
            cancel.text = HudText.Shot.Cancel;
            cancel.clicked += () => handlers.OnCancel();
            tips.Attach(host.Q("prev"), HudText.Shot.PrevTip);
            tips.Attach(host.Q("next"), HudText.Shot.NextTip);
            tips.Attach(cancel, HudText.Shot.CancelTip);
            host.Q<Label>("hit-label").text = HudText.Shot.Impact;
            host.Q<Label>("crit-label").text = HudText.Shot.Crit;
            tips.Attach(host.Q("noise"), HudText.Shot.NoiseTip);
            // Placed over its key once both are laid out (a key just drawn has no place yet).
            host.RegisterCallback<GeometryChangedEvent>(_ => Anchor(anchorKey));
            Ui.Show(host, false);
        }

        /// <summary>The view, and the key it is armed over (a self ability), whose position anchors it.</summary>
        public void Set(ShotView view, VisualElement armedKey = null)
        {
            var k = Ui.Key(view);
            if (k != key)
            {
                key = k;
                Fill(view);
            }
            var over = view != null && view.Kind == "self" ? armedKey : null;
            if (over != anchorKey && over != null) over.RegisterCallback<GeometryChangedEvent>(_ => Anchor(anchorKey));
            anchorKey = over;
            Anchor(over);
        }

        void Fill(ShotView v)
        {
            Ui.Show(host, v != null);
            if (v == null) return;
            var attack = v.Kind == "attack";
            var support = v.Kind == "support";
            var self = v.Kind == "self";
            host.EnableInClassList("shot-panel--support", support);
            host.EnableInClassList("shot-panel--self", self);

            // In capitals, as the web's .shot-title (USS has no text-transform).
            host.Q<Label>("title").text = (v.Title ?? "").ToUpperInvariant();
            Ui.Show(host.Q("head"), !self);
            host.Q<Label>("target").text = v.TargetName ?? "";
            host.Q<Label>("index").text = v.Total > 0 ? $"{v.Index + 1}/{v.Total}" : "";

            Ui.Show(host.Q("numbers"), attack);
            if (attack)
            {
                dial.Set(v.Hit);
                host.Q<Label>("hit-value").text = v.Hit + "%";
                host.Q<Label>("crit-value").text = v.Crit + "%";
            }

            var damage = host.Q("damage");
            Ui.Show(damage, (attack || support) && v.HasDamage);
            if (v.HasDamage) FillDamage(damage, v.Damage);

            Text("cost", self ? Join(v.Cost, v.Limits, v.Noise) : null);
            Text("effect", support || self ? v.Effect : null);
            Text("detail", self ? v.Detail : null);
            Mods(host.Q("mods"), attack ? v.Mods : null);
            Mods(host.Q("crit-mods"), attack ? v.CritMods : null);
            Text("graze", attack && v.Graze > 0 ? HudText.Shot.Graze(v.Graze, v.Damage.Graze) : null);
            Text("note", attack ? v.Note : null);
            Text("noise", self ? null : v.Noise);
            Text("hint", self ? (string.IsNullOrEmpty(v.Locked) ? v.Hint ?? HudText.Shot.SelfHint : HudText.Shot.SelfLocked) : null);

            var fire = host.Q<Button>("fire");
            var locked = !string.IsNullOrEmpty(v.Locked);
            fire.text = locked ? v.Locked : (v.Action ?? "").ToUpperInvariant();
            fire.SetEnabled(!locked);
            tips.Attach(fire, locked ? HudText.Shot.LockedTip : HudText.Shot.FireTip);
        }

        void FillDamage(VisualElement damage, DamageView d)
        {
            damage.Q<Label>("damage-line").text = HudText.Shot.Damage(d.Hit, d.Crit);
            var notes = damage.Q("damage-notes");
            notes.Clear();
            foreach (var n in d.Notes) Ui.Text(n, null, notes);
            var kill = damage.Q<Label>("kill");
            Ui.Show(kill, !string.IsNullOrEmpty(d.Kill));
            kill.text = d.Kill ?? "";
        }

        static void Mods(VisualElement list, List<ModView> mods)
        {
            list.Clear();
            Ui.Show(list, mods != null && mods.Count > 0);
            if (mods == null) return;
            foreach (var m in mods)
            {
                var row = Ui.El("mod", list);
                Ui.Text(m.Label, null, row);
                Ui.Text((m.Value > 0 ? "+" : "") + m.Value, m.Value >= 0 ? "mod__pos" : "mod__neg", row);
            }
        }

        void Text(string name, string text)
        {
            var l = host.Q<Label>(name);
            Ui.Show(l, !string.IsNullOrEmpty(text));
            l.text = text ?? "";
        }

        static string Join(params string[] parts)
        {
            var list = new List<string>();
            foreach (var p in parts)
                if (!string.IsNullOrEmpty(p)) list.Add(p);
            return string.Join(" · ", list);
        }

        /// <summary>
        /// Over the armed key, centred on it but never past the screen's edges (hud.ts placeTarget); else back to the
        /// right side, where the aim's panel waits (USS).
        /// </summary>
        void Anchor(VisualElement armedKey)
        {
            var anchored = armedKey != null && host.style.display != DisplayStyle.None;
            host.EnableInClassList("shot-panel--anchored", anchored);
            if (!anchored)
            {
                host.style.left = StyleKeyword.Null;
                return;
            }
            var parent = host.parent;
            if (parent == null || armedKey.panel == null) return;
            var keyBox = armedKey.worldBound;
            // Not laid out yet: GeometryChangedEvent brings it back here.
            if (float.IsNaN(keyBox.width) || keyBox.width <= 0) return;
            var width = host.layout.width;
            var screen = parent.layout.width;
            var left = Mathf.Clamp(keyBox.center.x - width / 2, 8, Mathf.Max(8, screen - width - 8));
            host.style.left = left;
        }
    }
}
