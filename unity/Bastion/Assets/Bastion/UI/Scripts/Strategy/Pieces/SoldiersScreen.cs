using System;
using System.Linq;
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
        /// <summary>Under the clearing, in the middle (strategy.css .strat-center-bottom): «ESPERAR A QUE SE CUREN».</summary>
        readonly VisualElement bottom;
        /// <summary>The clearing between the panels, where dragging turns the soldier shown (Ana's #5, Arturo 29/10).</summary>
        readonly VisualElement turnPad;
        /// <summary>The pointer's last x while it drags on the pad.</summary>
        float dragX;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        SoldiersView last;
        string key;
        /// <summary>The choice open on the sheet (its id); null, none. Picking another soldier closes it (screen.ts: picking = null).</summary>
        string open;
        /// <summary>The soldier whose sheet is up, to close the choice when another is picked.</summary>
        string sheetOf;
        /// <summary>Soldados is the screen up (its panels may show).</summary>
        bool shown;
        /// <summary>The colour row whose «Otro color» is open; null, none.</summary>
        string picking;
        /// <summary>Personalizar's title, kept live as the name is typed (the fields aren't drawn again meanwhile).</summary>
        Label customTitle;

        /// <summary>A soldier's bust by id; null, the silhouette.</summary>
        public Func<string, Texture> PortraitOf;

        /// <summary>The soldier shown, turned so many degrees (its front to the right on the screen): Arte's TurnShowcase.</summary>
        public Action<float> Turn;

        /// <summary>Degrees the soldier turns for each point the pointer drags.</summary>
        const float TurnPerPoint = 0.4f;

        public SoldiersScreen(VisualElement view, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            // Under the panels: a drag on the clearing turns the soldier, a drag on a panel doesn't.
            turnPad = Ui.El("soldiers-turn", view);
            turnPad.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                dragX = e.position.x;
                turnPad.CapturePointer(e.pointerId);
            });
            turnPad.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!turnPad.HasPointerCapture(e.pointerId)) return;
                var dx = e.position.x - dragX;
                dragX = e.position.x;
                if (dx != 0) Turn?.Invoke(dx * TurnPerPoint);
            });
            turnPad.RegisterCallback<PointerUpEvent>(e =>
            {
                if (turnPad.HasPointerCapture(e.pointerId)) turnPad.ReleasePointer(e.pointerId);
            });
            left = Ui.El("strat-side strat-side--left panel", view);
            right = Ui.El("strat-side strat-side--right panel", view);
            bottom = Ui.El("strat-center-bottom", view);
            bottom.pickingMode = PickingMode.Ignore;
            // They scroll with the wheel, without a bar (as the 0.10.1's panels).
            left.Add(new ScrollView(ScrollViewMode.Vertical) { name = "left-scroll", verticalScrollerVisibility = ScrollerVisibility.Auto, horizontalScrollerVisibility = ScrollerVisibility.Hidden });
            right.Add(new ScrollView(ScrollViewMode.Vertical) { name = "right-scroll", verticalScrollerVisibility = ScrollerVisibility.Auto, horizontalScrollerVisibility = ScrollerVisibility.Hidden });
        }

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(left, on);
            Ui.Show(right, on && (last?.Sheet != null || last?.Customizer != null));
            Ui.Show(turnPad, on && (last?.Sheet != null || last?.Customizer != null));
            Ui.Show(bottom, on && last?.MustWait != null);
        }

        /// <summary>The choice open on the sheet (hand, belt, a slot); null, none.</summary>
        public string OpenChoice => open;

        /// <summary>For tests (BaseRun «choice:hand»): a choice opened, as a click on its head.</summary>
        public void Open(string id)
        {
            open = id;
            key = null;
            Set(last);
        }

        /// <summary>Esc (screen.ts back): the choice open on the sheet, closed; whether there was one.</summary>
        public bool CloseChoice()
        {
            if (open == null) return false;
            open = null;
            key = null;
            Set(last);
            return true;
        }

        public void Set(SoldiersView v)
        {
            var now = v?.Mine.Concat(v.Partner).FirstOrDefault(t => t.Selected)?.Id;
            if (now != sheetOf) open = null;
            sheetOf = now;
            var k = KeyOf(v) + "|" + open + "|" + picking + "|" + StrategyRoot.PictureStamp;
            if (k == key) return;
            key = k;
            last = v;
            var leftBody = left.Q<ScrollView>("left-scroll").contentContainer;
            var rightBody = right.Q<ScrollView>("right-scroll").contentContainer;
            leftBody.Clear();
            rightBody.Clear();
            bottom.Clear();
            Ui.Show(bottom, shown && v?.MustWait != null);
            Ui.Show(turnPad, shown && (v?.Sheet != null || v?.Customizer != null));
            if (v == null) return;
            if (v.MustWait != null) MustWait(v.MustWait);
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
        /// Nobody can go out (screen.ts waitEl): the big key, as Escuadra's «Lanzar» (green when pressed, outlined when the
        /// partner asked), and its line on a plate of its own over the clearing.
        /// </summary>
        void MustWait(WaitDayView w)
        {
            var b = new Button(handlers.OnWait) { text = w.Label };
            Ui.AddClasses(b, "squad-launch strat-wait font-title");
            b.EnableInClassList("squad-launch--ready", w.Ready);
            b.EnableInClassList("squad-launch--waiting", w.Asked);
            bottom.Add(b);
            if (!string.IsNullOrEmpty(w.Note)) Ui.Text(w.Note, "strat-center-bottom__line", bottom);
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
                    if (!string.IsNullOrEmpty(ch.Tip)) tips.Attach(b, ch.Tip);
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
            tips.Attach(Action(actions, "Restablecer", "reset"), "Vuelve al aspecto de serie de este soldado");
            // On a holder: off (no colours, or no one else), the key takes no hover, and its help says why.
            var copyHolder = Ui.El("custom__copy-holder", actions);
            var copy = Action(copyHolder, c.CopyArmed ? $"¿Pintar a tus {c.CopyCount} soldados? Pulsa otra vez" : "Copiar a todos mis soldados", "copy");
            copy.EnableInClassList("custom__copy--armed", c.CopyArmed);
            copy.SetEnabled(c.CanCopy);
            if (!string.IsNullOrEmpty(c.CopyTip)) tips.Attach(copyHolder, c.CopyTip);
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
            // A locked row is off, and an off element takes no hover: its help on a holder round it.
            var holder = string.IsNullOrEmpty(r.Tip) ? parent : Ui.El("custom-color-holder", parent);
            if (holder != parent) tips.Attach(holder, r.Tip);
            var row = Ui.El("custom-color", holder);
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
                if (!string.IsNullOrEmpty(r.ResetTip)) tips.Attach(reset, r.ResetTip);
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
                // Its code (screen.ts: title), to ask for it again in «Otro color».
                tips.Attach(b, color);
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
                if (!string.IsNullOrEmpty(t.FullName)) tips.Attach(tile, t.FullName);
                var face = Ui.El("soldier-tile__face well", tile);
                var portrait = PortraitOf?.Invoke(t.Id);
                if (portrait is RenderTexture rt) face.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                else if (portrait is Texture2D t2) face.style.backgroundImage = new StyleBackground(t2);
                else Ui.Icon(icon, "tab:barracks", "soldier-tile__silhouette", face);
                if (t.Rank > 0)
                {
                    // The rank as the Military Combat HUD's chevron (mixta.css .rank), bronze, silver, gold.
                    var rank = Ui.Icon(icon, "rank:" + Mathf.Clamp(t.Rank, 1, 7), "soldier-tile__rank rank-badge", tile);
                    if (!string.IsNullOrEmpty(t.RankTip)) tips.Attach(rank, t.RankTip);
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
                if (!string.IsNullOrEmpty(s.Care.Title)) SubHead(s.Care.Title, parent);
                if (!string.IsNullOrEmpty(s.Care.Line)) Ui.Text(s.Care.Line, "sheet__care-line muted", parent);
                var actions = Ui.El("sheet__care", parent);
                for (var i = 0; i < s.Care.Buttons.Count; i++)
                {
                    var index = i;
                    var b = new Button(() => handlers.OnCare(index)) { text = s.Care.Buttons[i].ToUpperInvariant() };
                    b.AddToClassList("font-title");
                    if (i == 0) b.AddToClassList("btn-primary");
                    if (i == 0 && s.Care.Pulse) StrategyRoot.Pulse(b);
                    b.SetEnabled(i >= s.Care.Enabled.Count || s.Care.Enabled[i]);
                    actions.Add(b);
                    if (i == 0 && !string.IsNullOrEmpty(s.Care.Tip)) tips.Attach(b, s.Care.Tip);
                }
                if (!string.IsNullOrEmpty(s.Care.Why)) Ui.Text(s.Care.Why, "strat-why strat-why--small", parent);
                if (!string.IsNullOrEmpty(s.Care.Waiting)) Ui.Text(s.Care.Waiting, "strat-why strat-why--wait", parent);
            }
            if (s.Traits.Count > 0)
            {
                SubHead("Rasgos", parent);
                Chips(s.Traits, parent);
            }
            tips.Attach(SubHead(s.ArmsHead, parent), StrategyText.Sheet.ArmsTip);
            foreach (var p in s.Arms) Pick(p, parent);
            if (s.Armor != null) Armor(s.Armor, parent);
            tips.Attach(SubHead(s.BeltHead, parent), StrategyText.Sheet.BeltTip);
            foreach (var p in s.Belt) Pick(p, parent);
            if (s.Experience != null) Experience(s.Experience, parent);
        }

        static Label SubHead(string text, VisualElement parent) => Ui.Text(text.ToUpperInvariant(), "sheet__head font-title", parent);

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
                // arms.ts weaponPick: the picture, the name and its marks; what it is; against what it carries now.
                var top = Ui.El("pick__option-top", row);
                if (!string.IsNullOrEmpty(o.Icon)) Ui.Icon(icon, o.Icon, "pick__option-icon", top);
                Ui.Text(o.Name, "pick__option-name font-title", top);
                foreach (var t in o.Tags)
                {
                    var tag = Ui.Text(t.Text, "armory-row__tag font-title", top);
                    if (!string.IsNullOrEmpty(t.Tone)) tag.AddToClassList("armory-row__tag--" + t.Tone);
                    if (!string.IsNullOrEmpty(t.Tip)) tips.Attach(tag, t.Tip);
                }
                if (!string.IsNullOrEmpty(o.Note)) Ui.Text(o.Note, "pick__option-note", row);
                if (o.Diff.Count > 0)
                {
                    var diff = Ui.El("pick__option-diff", row);
                    foreach (var x in o.Diff)
                    {
                        var chip = Ui.Text(x.Text, "armory-diff", diff);
                        if (!string.IsNullOrEmpty(x.Tone)) chip.AddToClassList("armory-diff--" + x.Tone);
                        if (!string.IsNullOrEmpty(x.Tip)) tips.Attach(chip, x.Tip);
                    }
                }
                if (!string.IsNullOrEmpty(o.Tip)) tips.Attach(row, o.Tip);
                list.Add(row);
            }
            if (!string.IsNullOrEmpty(p.Empty)) Ui.Text(p.Empty, "pick__empty muted", list);
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
                var name = Ui.Text(r.Name, "skill-row__name font-title", top);
                if (!string.IsNullOrEmpty(r.Tip)) tips.Attach(name, r.Tip);
                if (!string.IsNullOrEmpty(r.Growth))
                {
                    var growth = Ui.Text(r.Growth, "skill-row__growth font-title", top);
                    if (!string.IsNullOrEmpty(r.GrowthTip)) tips.Attach(growth, r.GrowthTip);
                }
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
            tips.Attach(marks, StrategyText.Sheet.RankTip);
            var will = Ui.Text(x.Will, "skill-rank__will", rankTop);
            if (!string.IsNullOrEmpty(x.WillTip)) tips.Attach(will, x.WillTip);
            Ui.Text(x.RankLine, "skill-rank__line", rank);
            if (!string.IsNullOrEmpty(x.Gives)) Ui.Text(x.Gives, "skill-rank__line skill-rank__gives", rank);
            var board = new Button(handlers.OnBoard) { text = x.Board.ToUpperInvariant() };
            Ui.AddClasses(board, "skill-board font-title");
            board.EnableInClassList("skill-board--pulse", x.BoardPulse);
            parent.Add(board);
        }
    }
}
