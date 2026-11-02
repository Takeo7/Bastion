using System;
using System.Collections.Generic;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Escuadra (screen.ts renderSquad): one's own soldiers to pick on the left (the partner's on the right, to look at),
    /// who is ready above the mission, the mission's plate at the bottom with LANZAR and «Volver al Bastión», and a card
    /// under each figure of the line-up (LineupCards). With no mission chosen, only that and «Ir al mapa». The pointer on
    /// a soldier's row shows their sheet in short beside its list (Arturo, 1/11 #2).
    /// </summary>
    public sealed class SquadScreen
    {
        readonly VisualElement left;
        readonly VisualElement right;
        readonly VisualElement bottom;
        readonly VisualElement empty;
        readonly VisualElement view;
        readonly VisualElement peek;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        readonly LineupCards cards;
        bool shown;
        SquadView last;
        string key;
        /// <summary>Each row's soldier: its row, its sheet in short and whether its list is the left one.</summary>
        readonly Dictionary<string, (VisualElement row, SoldierPeekView peek, bool atLeft)> peeks = new Dictionary<string, (VisualElement, SoldierPeekView, bool)>();
        /// <summary>The soldier whose sheet shows, kept while the lists are drawn again (a click puts them in); null, none.</summary>
        string peeking;
        /// <summary>The row's top the sheet was put level with, in the view's pixels.</summary>
        float peekTop;

        public SquadScreen(VisualElement view, TooltipLayer tips, IStrategyHandlers handlers, Func<string, Sprite> icon = null)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            this.view = view;
            cards = new LineupCards(view, tips, handlers) { Floor = () => bottom.worldBound.yMin, Busts = false };
            left = Ui.El("strat-side strat-side--left strat-side--fit squad-list panel", view);
            right = Ui.El("strat-side strat-side--right strat-side--fit squad-list panel", view);
            bottom = Ui.El("squad-bottom", view);
            empty = Ui.El("squad-empty panel", view);
            peek = Ui.El("squad-peek panel", view);
            peek.pickingMode = PickingMode.Ignore;
            peek.RegisterCallback<GeometryChangedEvent>(_ => Clamp());
            Ui.Show(peek, false);
        }

        public LineupCards Cards => cards;

        /// <summary>A soldier's bust by id (Arte's Portraits.Soldier), on each row as on the combat's list; null, a dark square.</summary>
        public Func<string, Texture> PortraitOf;

        public void Show(bool on)
        {
            shown = on;
            Draw();
        }

        public void Set(SquadView v)
        {
            var k = Ui.Key(v) + "|" + StrategyRoot.PictureStamp;
            if (k == key) return;
            key = k;
            last = v;
            left.Clear();
            right.Clear();
            bottom.Clear();
            empty.Clear();
            peeks.Clear();
            Ui.Show(peek, false);
            cards.Set(v?.Cards);
            if (v == null) return;
            if (!string.IsNullOrEmpty(v.Empty))
            {
                Ui.Text(v.Empty, "squad-empty__text", empty);
                var go = new Button(handlers.OnGoMap) { text = "IR AL MAPA" };
                Ui.AddClasses(go, "btn-primary font-title");
                empty.Add(go);
                Draw();
                return;
            }
            List(v.MineHead, v.MineTip, v.Mine, left, v.PostHelp);
            if (!string.IsNullOrEmpty(v.PostHome))
            {
                var home = new Button(handlers.OnPostHome) { text = v.PostHome.ToUpperInvariant() };
                Ui.AddClasses(home, "squad-back squad-post-home font-title");
                left.Add(home);
            }
            if (v.Partner.Count > 0) List(v.PartnerHead, v.PartnerTip, v.Partner, right);
            if (v.Ready.Count > 0)
            {
                var board = Ui.El("ready-board", bottom);
                foreach (var r in v.Ready)
                {
                    var mark = Ui.El("ready-mark", board);
                    mark.EnableInClassList("ready-mark--on", r.Ready);
                    Ui.El("ready-mark__lamp", mark);
                    var name = Ui.Text(r.Name, "ready-mark__name font-title", mark);
                    name.style.color = Ui.Hex(r.Color, Color.white);
                    Ui.Text(r.Ready ? "A PUNTO" : "ELIGIENDO", "ready-mark__state font-title", mark);
                    if (!string.IsNullOrEmpty(r.Tip)) tips.Attach(mark, r.Tip);
                }
            }
            var plate = Ui.El("squad-mission panel", bottom);
            var words = Ui.El("squad-mission__words", plate);
            var title = Ui.El("squad-mission__title-row", words);
            Ui.Text(v.Title.ToUpperInvariant(), "squad-mission__title font-title", title);
            if (!string.IsNullOrEmpty(v.Night)) Ui.Text(v.Night.ToUpperInvariant(), "squad-mission__night font-title", title);
            Ui.Text(StrategyText.Marked(v.Briefing), "squad-mission__briefing", words);
            if (!string.IsNullOrEmpty(v.Warning)) Ui.Text(v.Warning, "squad-mission__warning", words);
            var actions = Ui.El("squad-mission__actions", plate);
            var launch = new Button(handlers.OnLaunch) { text = v.Launch.ToUpperInvariant() };
            Ui.AddClasses(launch, "squad-launch btn-primary font-title");
            launch.EnableInClassList("squad-launch--ready", v.LaunchReady);
            launch.EnableInClassList("squad-launch--waiting", v.LaunchWaiting);
            launch.SetEnabled(v.LaunchEnabled);
            if (!string.IsNullOrEmpty(v.LaunchWhy)) tips.Attach(launch, v.LaunchWhy);
            actions.Add(launch);
            if (!string.IsNullOrEmpty(v.Back))
            {
                var back = new Button(handlers.OnSquadBack) { text = v.Back.ToUpperInvariant() };
                Ui.AddClasses(back, "squad-back font-title");
                actions.Add(back);
            }
            Draw();
            // The sheet in short again, on the row drawn anew under the pointer, once it is laid out.
            if (peeking != null) peek.schedule.Execute(() => { if (peeking != null) Peek(peeking); });
        }

        /// <summary>A squad list: its head (and a help under it), and its rows (bust, hexagon, name, rank, a chip).</summary>
        void List(string head, string tip, List<PickRowView> rows, VisualElement parent, string help = null)
        {
            var h = Ui.El("strat-head", parent);
            Ui.Text(head.ToUpperInvariant(), "strat-head__text font-title", h);
            if (!string.IsNullOrEmpty(tip)) tips.Attach(h, tip);
            if (!string.IsNullOrEmpty(help)) Ui.Text(help, "squad-help muted", parent);
            foreach (var r in rows)
            {
                var id = r.Id;
                var row = new Button(() => handlers.OnSquadToggle(id, !r.In));
                row.AddToClassList("squad-row");
                row.EnableInClassList("squad-row--in", r.In);
                row.EnableInClassList("squad-row--picked", r.Picked);
                row.SetEnabled(r.Enabled);
                row.style.borderLeftColor = Ui.Hex(r.Accent, new Color(0.95f, 0.65f, 0.2f));
                // Every row its bust, picked or not (Ana, Arturo 31/10 #11).
                var bust = Ui.El("squad-row__bust", row);
                var portrait = PortraitOf?.Invoke(id);
                if (portrait is RenderTexture rt) bust.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                else if (portrait is Texture2D t2) bust.style.backgroundImage = new StyleBackground(t2);
                var hex = new SkillHex();
                hex.Set(r.Skills, tips);
                row.Add(hex);
                Ui.Text(r.Name, "squad-row__name font-title", row);
                if (r.Rank > 0)
                {
                    var rank = Ui.Icon(icon, "rank:" + Mathf.Clamp(r.Rank, 1, 7), "squad-row__rank rank-badge", row);
                    if (!string.IsNullOrEmpty(r.RankTip)) tips.Attach(rank, r.RankTip);
                }
                if (!string.IsNullOrEmpty(r.Chip))
                {
                    var chip = Ui.Text(r.Chip, "squad-row__chip font-title", row);
                    if (!string.IsNullOrEmpty(r.ChipTone)) chip.AddToClassList("squad-row__chip--" + r.ChipTone);
                    if (!string.IsNullOrEmpty(r.ChipTip)) tips.Attach(chip, r.ChipTip);
                }
                if (!string.IsNullOrEmpty(r.Tip)) tips.Attach(row, r.Tip);
                if (r.Peek != null)
                {
                    peeks[id] = (row, r.Peek, parent == left);
                    row.RegisterCallback<PointerEnterEvent>(_ => Peek(id));
                    row.RegisterCallback<PointerLeaveEvent>(_ =>
                    {
                        if (peeking == id) peeking = null;
                        Ui.Show(peek, false);
                    });
                }
                parent.Add(row);
            }
        }

        /// <summary>The pointer on a soldier's row as the lab puts it there (StrategyLab, BASTION_ESTRATEGIA_ENCIMA).</summary>
        public void LabPeek(string id) => Peek(id);

        /// <summary>
        /// A soldier's sheet in short (Arturo, 1/11 #2), beside their list and never over it (right of the left one, left of
        /// the right one), level with their row and above the mission's plate.
        /// </summary>
        void Peek(string id)
        {
            if (!peeks.TryGetValue(id, out var at)) return;
            peeking = id;
            var p = at.peek;
            peek.Clear();
            Ui.Text(p.Kicker.ToUpperInvariant(), "squad-peek__kicker font-title", peek);
            Ui.Text(p.Name, "squad-peek__name font-title", peek);
            var stats = Ui.El("squad-peek__stats", peek);
            foreach (var s in p.Stats)
            {
                var stat = Ui.El("squad-peek__stat", stats);
                Ui.Text(s.Value, "squad-peek__value font-title", stat);
                Ui.Text(s.Name, "squad-peek__label", stat);
            }
            void Line(string head, string text)
            {
                var line = Ui.El("squad-peek__line", peek);
                Ui.Text(head, "squad-peek__head font-title", line);
                Ui.Text(text, "squad-peek__text", line);
            }
            Line("ARMA", p.Weapon);
            if (!string.IsNullOrEmpty(p.Belt)) Line("CINTURÓN", p.Belt);
            Line("HABILIDADES", p.Abilities.Count > 0 ? string.Join(" · ", p.Abilities) : "Ninguna todavía");
            var health = Ui.Text(p.Health, "squad-peek__health font-title", peek);
            if (!string.IsNullOrEmpty(p.HealthTone)) health.AddToClassList("squad-peek__health--" + p.HealthTone);
            var list = at.atLeft ? left : right;
            var x = at.atLeft ? list.worldBound.xMax + PeekGap : list.worldBound.xMin - PeekGap - PeekWidth;
            var place = view.WorldToLocal(new Vector2(x, at.row.worldBound.yMin));
            peekTop = place.y;
            peek.style.left = place.x;
            peek.style.top = peekTop;
            Ui.Show(peek, true);
            Clamp();
        }

        /// <summary>The sheet's width (Squad.uss .squad-peek) and its gap to the list.</summary>
        const float PeekWidth = 228;
        const float PeekGap = 8;

        /// <summary>The sheet kept above the mission's plate once its height is known.</summary>
        void Clamp()
        {
            var height = peek.resolvedStyle.height;
            if (float.IsNaN(height) || peek.resolvedStyle.display == DisplayStyle.None) return;
            var floor = view.WorldToLocal(new Vector2(0, bottom.worldBound.yMin)).y - PeekGap;
            var top = Mathf.Max(PeekGap, Mathf.Min(peekTop, floor - height));
            if (!Mathf.Approximately(peek.resolvedStyle.top, top)) peek.style.top = top;
        }

        void Draw()
        {
            var isEmpty = last != null && !string.IsNullOrEmpty(last.Empty);
            Ui.Show(empty, shown && isEmpty);
            Ui.Show(left, shown && !isEmpty && last != null);
            Ui.Show(right, shown && !isEmpty && last != null && last.Partner.Count > 0);
            Ui.Show(bottom, shown && !isEmpty && last != null);
            if (!shown || isEmpty)
            {
                peeking = null;
                Ui.Show(peek, false);
            }
            cards.Show(shown && !isEmpty);
        }
    }
}
