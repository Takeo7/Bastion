using System.Collections.Generic;
using System.Linq;
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

        readonly System.Func<string, Sprite> icon;

        public ShotPanel(VisualElement host, IHudHandlers handlers, TooltipLayer tips, System.Func<string, Sprite> icon = null)
        {
            this.host = host;
            this.handlers = handlers;
            this.tips = tips;
            this.icon = icon;
            Ui.CloneInto("ShotPanel", host);
            // mixta.css: a big plate in its frame, and Disparar slanted (.hud button.fire).
            PlateFrame.Dress(host);
            Slant.Dress(host.Q<Button>("fire"));
            // Its words whole, as they are laid out (FitFire).
            var fireKey = host.Q<Button>("fire");
            fireKey.RegisterCallback<GeometryChangedEvent>(_ => FitFire(fireKey));
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
            // With others to aim at, how to go to them (Arturo, 5/11 #11): the arrows, Tab, or a click on their odds.
            // A single target, nothing («1/1» said nothing; B23).
            host.Q<Label>("index").text = v.Total > 1 ? HudText.Shot.Index(v.Index + 1, v.Total) : "";

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
            Mods(host.Q("mods"), attack ? v.Mods : null, HudText.Shot.Impact);
            // (The crit at 0 said why, under its empty bar: cuadro 158.)
            Mods(host.Q("crit-mods"), attack ? v.CritMods : null, HudText.Shot.Crit, attack && v.Crit == 0 ? v.CritNote : null);
            Text("graze", attack && v.Graze > 0 ? HudText.Shot.Graze(v.Graze, v.Damage.Graze) : null);
            Text("note", attack ? v.Note : null);
            Text("noise", self ? null : v.Noise);
            Text("hint", self ? (string.IsNullOrEmpty(v.Locked) ? v.Hint ?? HudText.Shot.SelfHint : HudText.Shot.SelfLocked) : null);

            var fire = host.Q<Button>("fire");
            var locked = !string.IsNullOrEmpty(v.Locked);
            fire.text = locked ? v.Locked : (v.Action ?? "").ToUpperInvariant();
            // (Each text measured again from the key's own size, beside Cancelar.)
            fire.style.fontSize = StyleKeyword.Null;
            fire.style.letterSpacing = StyleKeyword.Null;
            fire.parent.RemoveFromClassList("shot-actions--stacked");
            fire.SetEnabled(!locked);
            tips.Attach(fire, locked ? HudText.Shot.LockedTip : HudText.Shot.FireTip);
        }

        /// <summary>The least the fire key's words go down to (its own 16).</summary>
        const float LeastFire = 13f;

        /// <summary>
        /// The fire key's words whole, never cut nor on two lines (Producto, 18/11, U15: «REGISTRAR EL ESTANTE DE MEDICINAS»
        /// was cut at both ends): closer and smaller, as the busts' state words (SquadList.FitTag), down to LeastFire; still
        /// too long beside Cancelar, the key takes the whole row (Cancelar under it) and is fitted again there. Any that
        /// doesn't fit even so is said in the log, for Lore.
        /// </summary>
        void FitFire(Button fire)
        {
            var room = fire.contentRect.width;
            if (float.IsNaN(room) || room <= 0 || string.IsNullOrEmpty(fire.text)) return;
            var wide = fire.MeasureTextSize(fire.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
            if (wide <= room + 0.5f) return;
            var now = fire.resolvedStyle.fontSize;
            var row = fire.parent;
            var fits = Mathf.Floor(now * room / wide * 0.97f * 4) / 4;
            if (fits < LeastFire && !row.ClassListContains("shot-actions--stacked"))
            {
                // A row of its own, from its own size again.
                row.AddToClassList("shot-actions--stacked");
                fire.style.fontSize = StyleKeyword.Null;
                fire.style.letterSpacing = StyleKeyword.Null;
                return;
            }
            if (now <= LeastFire)
            {
                // (Past its padding into the plate's edge is still whole; only past the key itself is cut.)
                if (wide > fire.layout.width) Debug.Log($"[ui] «{fire.text}» no cabe entero en el botón ({wide:0.#} de {fire.layout.width:0.#} px a {LeastFire} px)");
                return;
            }
            fire.style.letterSpacing = 0;
            fire.style.fontSize = Mathf.Max(LeastFire, fits);
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

        /// <summary>
        /// The terms under their number's title (IMPACTO, CRÍTICO; U1, Arturo's cuadro 158: «la barra»): a bar from 0 to 100,
        /// the soldier's own grey from 0, what adds green on from it and what takes away red back over it, a mark where it
        /// ends; under it each term an icon and its figure, its words on hover. None, only `none` (the crit at 0, and why).
        /// </summary>
        void Mods(VisualElement list, List<ModView> mods, string title, string none = null)
        {
            list.Clear();
            var any = mods != null && mods.Count > 0;
            Ui.Show(list, any || none != null);
            if (!any && none == null) return;
            Ui.Text(title, "mods__title muted", list);
            // No two figures under one icon (Producto, cuadro 158): those that share one go together, their words in its help.
            mods = (mods ?? new List<ModView>()).GroupBy(m => m.Icon).Select(g => g.Count() == 1 ? g.First() : new ModView
            {
                Icon = g.Key,
                Value = g.Sum(m => m.Value),
                Label = string.Join(" · ", g.Select(m => $"{m.Label} {Signed(m.Value)}")),
                Note = g.Select(m => m.Note).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
            }).ToList();
            // The soldier's own first (its aim, its base crit), what adds, then what takes away: the red reads as what was
            // lost from where it got to.
            var ordered = mods.Where(IsOwn).Concat(mods.Where(m => !IsOwn(m) && m.Value >= 0).OrderByDescending(m => m.Value)).Concat(mods.Where(m => !IsOwn(m) && m.Value < 0).OrderBy(m => m.Value)).ToList();
            Bar(list, ordered, Mathf.Clamp(mods.Sum(m => m.Value), 0, 100));
            if (none != null) Ui.Text(none, "mods__none muted", list);
            if (!any) return;
            var row = Ui.El("terms", list);
            for (var i = 0; i < ordered.Count; i++)
            {
                var m = ordered[i];
                var own = IsOwn(m);
                var term = Ui.El("term " + (own ? "term--own" : m.Value >= 0 ? "term--pos" : "term--neg"), row);
                TermIcon(m.Icon, term);
                Ui.Text(own || i == 0 ? m.Value.ToString() : Signed(m.Value), "term__value font-title", term);
                if (!string.IsNullOrEmpty(m.Note)) Ui.Text(m.Note, "term__note", term);
                tips.Attach(term, HudText.Shot.TermTip(m.Label, m.Note));
            }
        }

        /// <summary>A term's icon, Arte's (HudIcons' «aim:…»), tinted by `.term__icon`.</summary>
        void TermIcon(string id, VisualElement parent) => Ui.Icon(icon, id, "term__icon", parent);

        /// <summary>The soldier's own term, its starting point: its aim, its base crit (Aim.PreviewShot's labels).</summary>
        static bool IsOwn(ModView m) => m.Icon == "aim:soldier";

        static string Signed(int n) => n < 0 ? "−" + (-n) : "+" + n;

        /// <summary>
        /// 0 to 100, the soldier's own grey from 0, then each term on from where the last left it, green forward and red back
        /// (over what it takes away), and a mark where it ends.
        /// </summary>
        static void Bar(VisualElement list, List<ModView> ordered, int total)
        {
            var bar = Ui.El("odds-bar", list);
            var at = 0;
            foreach (var m in ordered)
            {
                var from = Mathf.Clamp(at, 0, 100);
                var to = Mathf.Clamp(at + m.Value, 0, 100);
                at += m.Value;
                if (from == to) continue;
                var piece = Ui.El("odds-bar__piece " + (IsOwn(m) ? "odds-bar__piece--own" : m.Value >= 0 ? "odds-bar__piece--pos" : "odds-bar__piece--neg"), bar);
                piece.style.left = Length.Percent(Mathf.Min(from, to));
                piece.style.width = Length.Percent(Mathf.Abs(to - from));
            }
            Ui.El("odds-bar__end", bar).style.left = Length.Percent(total);
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
