using System;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Soldados (screen.ts renderBarracks): on the left one's own soldiers in a grid of busts, the recruit's card and the
    /// partner's soldiers while the partner is away; on the right the picked soldier's sheet (figures, care, traits, the
    /// weapon in each hand and the armour, the belt, the experience with its rank and the board's button). The choices
    /// open in place (one at a time). The busts are Arte's (Portraits); a silhouette until they are there.
    /// </summary>
    public sealed class SoldiersScreen
    {
        readonly VisualElement left;
        readonly VisualElement right;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        SoldiersView last;
        string key;
        /// <summary>The choice open on the sheet (its id); null, none.</summary>
        string open;
        /// <summary>Soldados is the screen up (its panels may show).</summary>
        bool shown;
        /// <summary>The colour row whose «Otro color» is open; null, none.</summary>
        string picking;
        /// <summary>Personalizar's title, kept live as the name is typed (the fields aren't drawn again meanwhile).</summary>
        Label customTitle;

        /// <summary>A soldier's bust by id; null, the silhouette.</summary>
        public Func<string, Texture> PortraitOf;

        public SoldiersScreen(VisualElement view, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            left = Ui.El("strat-side strat-side--left panel", view);
            right = Ui.El("strat-side strat-side--right panel", view);
            // They scroll with the wheel, without a bar (as the 0.10.1's panels).
            left.Add(new ScrollView(ScrollViewMode.Vertical) { name = "left-scroll", verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden });
            right.Add(new ScrollView(ScrollViewMode.Vertical) { name = "right-scroll", verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden });
        }

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(left, on);
            Ui.Show(right, on && (last?.Sheet != null || last?.Customizer != null));
        }

        public void Set(SoldiersView v)
        {
            var k = KeyOf(v) + "|" + open + "|" + picking;
            if (k == key) return;
            key = k;
            last = v;
            var leftBody = left.Q<ScrollView>("left-scroll").contentContainer;
            var rightBody = right.Q<ScrollView>("right-scroll").contentContainer;
            leftBody.Clear();
            rightBody.Clear();
            if (v == null) return;
            Head(v.MineHead, null, leftBody);
            Grid(v.Mine, leftBody);
            if (v.Recruit != null) Recruit(v.Recruit, leftBody);
            if (v.Partner.Count > 0)
            {
                Head(v.PartnerHead, v.PartnerNote, leftBody);
                Grid(v.Partner, leftBody);
            }
            Ui.Show(right, shown && (v.Sheet != null || v.Customizer != null));
            if (v.Customizer != null) Customizer(v.Customizer, rightBody);
            else if (v.Sheet != null) Sheet(v.Sheet, rightBody);
        }

        /// <summary>
        /// The view's key without what is typed (the name, the nickname, the title that follows them): typing mustn't draw
        /// the fields again (the 0.10.1 had to put the caret back by hand after each redraw).
        /// </summary>
        static string KeyOf(SoldiersView v)
        {
            var c = v?.Customizer;
            if (c == null) return Ui.Key(v);
            var (name, nickname, title) = (c.Name, c.Nickname, c.Title);
            c.Name = c.Nickname = c.Title = null;
            var key = Ui.Key(v);
            (c.Name, c.Nickname, c.Title) = (name, nickname, title);
            return key;
        }

        /// <summary>Personalizar: name and nickname, Ropa, Cabeza and Complexión, the colours, its buttons.</summary>
        void Customizer(CustomizerView c, VisualElement parent)
        {
            Ui.Text(c.Kicker.ToUpperInvariant(), "sheet__kicker font-title", parent);
            customTitle = Ui.Text((c.Title ?? "").ToUpperInvariant(), "sheet__name font-title", parent);
            var name = Field("Nombre", c.Name, c.NameMax, null, parent);
            var nickname = Field("Apodo", c.Nickname, c.NicknameMax, "Sin apodo", parent);
            void Typed()
            {
                customTitle.text = (string.IsNullOrWhiteSpace(name.value) ? c.Title ?? "" : name.value).ToUpperInvariant();
                handlers.OnCustomName(name.value, nickname.value);
            }
            name.RegisterValueChangedCallback(_ => Typed());
            nickname.RegisterValueChangedCallback(_ => Typed());
            foreach (var g in c.Groups)
            {
                Ui.Text(g.Title.ToUpperInvariant(), "custom__head font-title", parent);
                var row = Ui.El("custom__choices", parent);
                foreach (var ch in g.Choices)
                {
                    var (group, id) = (g.Id, ch.Id);
                    var b = new Button(() => handlers.OnCustomChoice(group, id, false)) { text = ch.Label };
                    b.AddToClassList("custom__choice");
                    b.EnableInClassList("custom__choice--on", ch.On);
                    // Over an outfit, the figure wears it for a look; away, back to the draft.
                    b.RegisterCallback<PointerEnterEvent>(_ => handlers.OnCustomChoice(group, id, true));
                    b.RegisterCallback<PointerLeaveEvent>(_ => handlers.OnCustomChoice(group, null, true));
                    row.Add(b);
                }
            }
            var head = Ui.El("strat-head custom__colors-head", parent);
            Ui.Text(c.ColorsHead.ToUpperInvariant(), "strat-head__text font-title", head);
            if (!string.IsNullOrEmpty(c.ColorsOff)) Ui.Text(c.ColorsOff, "custom__off", parent);
            var rows = Ui.El("custom__rows", parent);
            rows.SetEnabled(string.IsNullOrEmpty(c.ColorsOff));
            foreach (var r in c.Colors) ColorRow(r, rows);
            var actions = Ui.El("custom__actions", parent);
            Action(actions, "Aleatorio", "random");
            Action(actions, "Restablecer", "reset");
            Action(actions, c.CopyArmed ? $"¿Pintar a tus {c.CopyCount} soldados? Pulsa otra vez" : "Copiar a todos mis soldados", "copy").EnableInClassList("custom__copy--armed", c.CopyArmed);
            var foot = Ui.El("custom__foot", parent);
            var save = Action(foot, "Guardar", "save");
            save.AddToClassList("btn-primary");
            save.SetEnabled(c.CanSave);
            Action(foot, "Cancelar", "cancel");
            if (!c.CanSave && !string.IsNullOrEmpty(c.SaveWhy)) Ui.Text(c.SaveWhy, "strat-why", parent);
        }

        TextField Field(string label, string value, int max, string placeholder, VisualElement parent)
        {
            Ui.Text(label.ToUpperInvariant(), "custom__head font-title", parent);
            var field = new TextField { value = value ?? "", maxLength = max };
            field.AddToClassList("custom__field");
            if (!string.IsNullOrEmpty(placeholder)) field.textEdition.placeholder = placeholder;
            parent.Add(field);
            return field;
        }

        Button Action(VisualElement parent, string text, string action)
        {
            var b = new Button(() => handlers.OnCustomAction(action)) { text = text.ToUpperInvariant() };
            b.AddToClassList("font-title");
            parent.Add(b);
            return b;
        }

        /// <summary>A part's row: its swatch and name, a note or «↺», the palette and «Otro color» (the picker under it).</summary>
        void ColorRow(ColorRowView r, VisualElement parent)
        {
            var row = Ui.El("custom-color", parent);
            row.SetEnabled(!r.Locked);
            var top = Ui.El("custom-color__top", row);
            var dot = Ui.El("custom-color__dot", top);
            dot.style.backgroundColor = Ui.Hex(r.Color, new Color(0.4f, 0.4f, 0.4f));
            Ui.Text(r.Label.ToUpperInvariant(), "custom-color__label font-title", top);
            var part = r.Id;
            if (!string.IsNullOrEmpty(r.Reset))
            {
                var reset = new Button(() => handlers.OnCustomColor(part, null, false)) { text = r.Reset };
                reset.AddToClassList("custom-color__reset");
                top.Add(reset);
            }
            else if (!string.IsNullOrEmpty(r.Note)) Ui.Text(r.Note, "custom-color__note", top);
            if (r.Locked) return;
            var swatches = Ui.El("custom-color__swatches", row);
            foreach (var hex in r.Swatches)
            {
                var color = hex;
                var b = new Button(() => handlers.OnCustomColor(part, color, false));
                b.AddToClassList("custom-color__swatch");
                b.EnableInClassList("custom-color__swatch--on", string.Equals(color, r.Color, System.StringComparison.OrdinalIgnoreCase));
                b.style.backgroundColor = Ui.Hex(color, Color.grey);
                swatches.Add(b);
            }
            var any = new Button(() =>
            {
                picking = picking == part ? null : part;
                key = null;
                Set(last);
            });
            Ui.AddClasses(any, "custom-color__swatch custom-color__any");
            tips.Attach(any, "Otro color");
            swatches.Add(any);
            if (picking != part) return;
            var picker = new ColorPicker(Ui.Hex(r.Color, Color.grey));
            picker.Changed += (color, preview) => handlers.OnCustomColor(part, "#" + ColorUtility.ToHtmlStringRGB(color), preview);
            row.Add(picker);
        }

        void Head(string text, string note, VisualElement parent)
        {
            var head = Ui.El("strat-head", parent);
            Ui.Text(text.ToUpperInvariant(), "strat-head__text font-title", head);
            if (!string.IsNullOrEmpty(note)) Ui.Text("· " + note, "strat-head__note", head);
        }

        void Grid(System.Collections.Generic.List<SoldierTileView> tiles, VisualElement parent)
        {
            var grid = Ui.El("soldier-grid", parent);
            foreach (var t in tiles)
            {
                var id = t.Id;
                var tile = new Button(() => handlers.OnSoldier(id));
                tile.AddToClassList("soldier-tile");
                tile.EnableInClassList("soldier-tile--on", t.Selected);
                tile.EnableInClassList("soldier-tile--dead", t.Dead);
                tile.style.borderBottomColor = Ui.Hex(t.Accent, new Color(0.95f, 0.65f, 0.2f));
                var face = Ui.El("soldier-tile__face well", tile);
                var portrait = PortraitOf?.Invoke(t.Id);
                if (portrait is RenderTexture rt) face.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                else if (portrait is Texture2D t2) face.style.backgroundImage = new StyleBackground(t2);
                else Ui.Icon(icon, "tab:barracks", "soldier-tile__silhouette", face);
                if (t.Rank > 0)
                {
                    var rank = Ui.El("soldier-tile__rank", tile);
                    for (var i = 0; i < t.Rank; i++) Ui.El("rank__chevron", rank);
                }
                if (t.Points)
                {
                    var points = Ui.Text("▲", "soldier-tile__points font-title", tile);
                    tips.Attach(points, t.PointsTip);
                }
                var name = Ui.El("soldier-tile__name-row", tile);
                var hex = new SkillHex();
                hex.Set(t.Skills, tips);
                name.Add(hex);
                Ui.Text(t.Name, "soldier-tile__name font-title", name);
                if (!string.IsNullOrEmpty(t.Tag))
                {
                    var tag = Ui.Text(t.Tag, "soldier-tile__tag", tile);
                    if (!string.IsNullOrEmpty(t.TagTone)) tag.AddToClassList("soldier-tile__tag--" + t.TagTone);
                    tips.Attach(tag, t.TagTip);
                }
                grid.Add(tile);
            }
        }

        void Recruit(RecruitView r, VisualElement parent)
        {
            var card = Ui.El("recruit-card", parent);
            Ui.Text(r.Title.ToUpperInvariant(), "recruit-card__title font-title", card);
            Ui.Text(r.Blurb, "recruit-card__blurb", card);
            Chips(r.Chips, card);
            var b = new Button(handlers.OnRecruit) { text = r.Button.ToUpperInvariant() };
            Ui.AddClasses(b, "recruit-card__button btn-primary font-title");
            b.SetEnabled(r.Enabled);
            card.Add(b);
            if (!string.IsNullOrEmpty(r.Why)) Ui.Text(r.Why, "strat-why", card);
        }

        void Chips(System.Collections.Generic.List<ChipView> chips, VisualElement parent)
        {
            var row = Ui.El("strat-chips", parent);
            foreach (var c in chips)
            {
                var chip = Ui.Text(c.Text, "strat-chip", row);
                if (!string.IsNullOrEmpty(c.Tone)) chip.AddToClassList("strat-chip--" + c.Tone);
                if (!string.IsNullOrEmpty(c.Tip)) tips.Attach(chip, c.Tip);
            }
        }

        void Sheet(SoldierSheetView s, VisualElement parent)
        {
            Ui.Text(s.Kicker.ToUpperInvariant(), "sheet__kicker font-title", parent);
            var name = Ui.El("sheet__name-row", parent);
            var hex = new SkillHex();
            hex.Set(s.Skills, tips);
            name.Add(hex);
            var title = Ui.Text(s.Name.ToUpperInvariant(), "sheet__name font-title", name);
            tips.Attach(title, s.NameTip);
            Ui.Text(s.Subtitle, "sheet__subtitle muted", parent);
            if (!string.IsNullOrEmpty(s.Note)) Ui.Text(s.Note, "sheet__note", parent);
            if (!string.IsNullOrEmpty(s.Customize))
            {
                var custom = new Button(handlers.OnCustomize) { text = s.Customize.ToUpperInvariant() };
                Ui.AddClasses(custom, "sheet__customize font-title");
                parent.Add(custom);
            }
            var stats = Ui.El("sheet__stats well", parent);
            foreach (var st in s.Stats)
            {
                var stat = Ui.El("sheet__stat", stats);
                var i = Ui.Icon(icon, st.Icon, "sheet__stat-icon", stat);
                var color = Ui.Hex(st.Color, Color.white);
                i.style.unityBackgroundImageTintColor = color;
                var value = Ui.Text(st.Value, "sheet__stat-value font-title", stat);
                value.style.color = color;
                tips.Attach(stat, st.Tip);
            }
            var health = Ui.Text(s.Health, "sheet__health font-title", stats);
            if (!string.IsNullOrEmpty(s.HealthTone)) health.AddToClassList("sheet__health--" + s.HealthTone);
            tips.Attach(health, s.HealthTip);
            if (s.Care != null)
            {
                SubHead(s.Care.Title, parent);
                var actions = Ui.El("sheet__care", parent);
                for (var i = 0; i < s.Care.Buttons.Count; i++)
                {
                    var index = i;
                    var b = new Button(() => handlers.OnCare(index)) { text = s.Care.Buttons[i].ToUpperInvariant() };
                    b.AddToClassList("font-title");
                    if (i == 0) b.AddToClassList("btn-primary");
                    b.SetEnabled(i >= s.Care.Enabled.Count || s.Care.Enabled[i]);
                    actions.Add(b);
                }
                if (!string.IsNullOrEmpty(s.Care.Waiting)) Ui.Text(s.Care.Waiting, "strat-why strat-why--wait", parent);
            }
            if (s.Traits.Count > 0)
            {
                SubHead("Rasgos", parent);
                Chips(s.Traits, parent);
            }
            SubHead(s.ArmsHead, parent);
            foreach (var p in s.Arms) Pick(p, parent);
            if (s.Armor != null) Armor(s.Armor, parent);
            SubHead(s.BeltHead, parent);
            foreach (var p in s.Belt) Pick(p, parent);
            if (s.Experience != null) Experience(s.Experience, parent);
        }

        static void SubHead(string text, VisualElement parent) => Ui.Text(text.ToUpperInvariant(), "sheet__head font-title", parent);

        /// <summary>A choice: «LABEL Value ▾» and its lines; open, its options under it.</summary>
        void Pick(PickView p, VisualElement parent)
        {
            var box = Ui.El("pick", parent);
            var id = p.Id;
            var head = new Button(() =>
            {
                if (!p.Editable) return;
                open = open == id ? null : id;
                key = null;
                Set(last);
            });
            head.AddToClassList("pick__head");
            head.EnableInClassList("pick__head--fixed", !p.Editable);
            Ui.Text(p.Label.ToUpperInvariant(), "pick__label font-title", head);
            Ui.Text(p.Value, "pick__value font-title", head);
            if (p.Editable) Ui.Text("▾", "pick__arrow", head);
            box.Add(head);
            if (p.Lines.Count > 0 || p.Condition >= 0 || p.Tags.Count > 0)
            {
                var lines = Ui.El("pick__lines", box);
                var first = Ui.El("pick__line-row", lines);
                if (p.Lines.Count > 0) Ui.Text(p.Lines[0], "pick__line", first);
                if (p.Condition >= 0)
                {
                    var gauge = Ui.El("pick__gauge well", first);
                    var fill = Ui.El("pick__gauge-fill", gauge);
                    fill.style.width = Length.Percent(Mathf.Clamp(p.Condition, 0, 100));
                    fill.EnableInClassList("pick__gauge-fill--worn", p.Condition < 30);
                    Ui.Text(p.ConditionText, "pick__line", first);
                }
                foreach (var tag in p.Tags) Ui.Text(tag, "pick__tag font-title", first);
                for (var i = 1; i < p.Lines.Count; i++) Ui.Text(p.Lines[i], "pick__line", lines);
            }
            if (open != id || !p.Editable) return;
            var list = Ui.El("pick__options well", box);
            foreach (var o in p.Options)
            {
                var option = o.Id;
                var row = new Button(() =>
                {
                    open = null;
                    handlers.OnPick(id, option);
                });
                row.AddToClassList("pick__option");
                row.EnableInClassList("pick__option--current", o.Current);
                row.SetEnabled(o.Enabled);
                Ui.Text(o.Name, "pick__option-name font-title", row);
                if (!string.IsNullOrEmpty(o.Note)) Ui.Text(o.Note, "pick__option-note", row);
                list.Add(row);
            }
        }

        void Armor(ArmorView a, VisualElement parent)
        {
            var row = Ui.El("sheet__armor", parent);
            var marks = Ui.El("tier-marks", row);
            for (var i = 0; i < Mathf.Max(1, a.Tier); i++) Ui.Text("◆", "tier-marks__mark", marks);
            Ui.Text(a.Name, "sheet__armor-name font-title", row);
            Ui.Text(a.Figures, "sheet__armor-figures muted", row);
            if (!string.IsNullOrEmpty(a.Next))
            {
                var next = Ui.Text(a.Next, "strat-chip", row);
                tips.Attach(next, a.NextTip);
            }
        }

        void Experience(SkillsView x, VisualElement parent)
        {
            SubHead(x.Head, parent);
            foreach (var r in x.Rows)
            {
                var row = Ui.El("skill-row", parent);
                var top = Ui.El("skill-row__top", row);
                var color = SkillHex.Colors[Mathf.Clamp(r.Line, 0, SkillHex.Colors.Length - 1)];
                Ui.El("skill-row__dot", top).style.backgroundColor = color;
                Ui.Text(r.Name, "skill-row__name font-title", top);
                if (!string.IsNullOrEmpty(r.Growth)) Ui.Text(r.Growth, "skill-row__growth font-title", top);
                var bottom = Ui.El("skill-row__bottom", row);
                var bar = Ui.El("skill-row__bar", bottom);
                for (var i = 0; i < r.Total; i++)
                {
                    var seg = Ui.El("skill-row__seg", bar);
                    seg.style.backgroundColor = i < r.Filled ? color : new Color(color.r, color.g, color.b, 0.18f);
                }
                var reward = Ui.Text(r.Reward, "skill-row__reward", bottom);
                tips.Attach(reward, r.RewardTip);
            }
            var rank = Ui.El("skill-rank well", parent);
            var rankTop = Ui.El("skill-rank__top", rank);
            Ui.Text(x.Rank.ToUpperInvariant(), "skill-rank__name font-title", rankTop);
            var marks = Ui.El("skill-rank__bar", rankTop);
            for (var i = 0; i < x.RankTotal; i++) Ui.El(i < x.RankFilled ? "skill-rank__seg skill-rank__seg--on" : "skill-rank__seg", marks);
            Ui.Text(x.Will, "skill-rank__will", rankTop);
            Ui.Text(x.RankLine, "skill-rank__line", rank);
            var board = new Button(handlers.OnBoard) { text = x.Board.ToUpperInvariant() };
            Ui.AddClasses(board, "skill-board font-title");
            board.EnableInClassList("skill-board--pulse", x.BoardPulse);
            parent.Add(board);
        }
    }
}
