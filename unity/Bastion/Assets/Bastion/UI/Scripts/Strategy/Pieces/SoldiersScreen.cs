using System;
using System.Linq;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Soldados (screen.ts renderBarracks; Arturo's notes of 14/11, vista-soldados): on the left one grid of busts, one's
    /// own, the partner's while the partner is away and the recruit's tile; on the right the picked soldier's sheet (the
    /// name, the battle's three lines, how they are, care, traits, the experience with its rank and the board's button);
    /// round the figure in the clearing its gear in two columns, whose choices open beside them (one at a time). A click on
    /// the figure opens Personalizar. The busts are Arte's (Portraits); a silhouette until they are there.
    /// </summary>
    public sealed class SoldiersScreen
    {
        readonly VisualElement left;
        readonly VisualElement right;
        /// <summary>Under the clearing, in the middle (strategy.css .strat-center-bottom): «ESPERAR A QUE SE CUREN».</summary>
        readonly VisualElement bottom;
        /// <summary>The clearing between the panels, where dragging turns the soldier shown (Ana's #5, Arturo 29/10).</summary>
        readonly VisualElement turnPad;
        /// <summary>The pointer's last x while it drags on the pad; where it went down, and whether it has dragged since.</summary>
        float dragX;
        Vector2 downAt;
        bool dragged;
        /// <summary>The gear's two columns round the figure, over the pad (Gear).</summary>
        readonly VisualElement gear;
        /// <summary>Between the columns, where the figure stands: a click there opens Personalizar (OverFigure), «Personalizar» at its foot meanwhile.</summary>
        readonly VisualElement figure;
        readonly Label figureCue;
        /// <summary>The pointer is on the figure (LightFigure told).</summary>
        bool lit;
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
        /// <summary>A press that moves less than this (panel points) is a click, not a turn.</summary>
        const float ClickSlop = 4;

        /// <summary>
        /// Whether a point of the panel is on the soldier shown (Arte's Settlement.FigureAt); null when there is no clearing
        /// to ask (the labs' flat sky): then the room between the gear's columns, where the figure stands.
        /// </summary>
        public Func<Vector2, bool?> OverFigure;

        /// <summary>The figure lit while the pointer is on it, or not (Arte's Settlement.Highlight).</summary>
        public Action<bool> LightFigure;

        /// <summary>«Personalizar», at the figure's foot while the pointer is on it.</summary>
        public static string Customize = "Personalizar";

        public SoldiersScreen(VisualElement view, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            // Under the panels: a drag on the clearing turns the soldier, a drag on a panel doesn't.
            // A press on the figure that doesn't drag opens Personalizar (Arturo, 14/11).
            turnPad = Ui.El("soldiers-turn", view);
            turnPad.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                dragX = e.position.x;
                downAt = e.position;
                dragged = false;
                turnPad.CapturePointer(e.pointerId);
            });
            turnPad.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!turnPad.HasPointerCapture(e.pointerId))
                {
                    Hover(e.position);
                    return;
                }
                if (((Vector2)e.position - downAt).magnitude > ClickSlop) dragged = true;
                var dx = e.position.x - dragX;
                dragX = e.position.x;
                if (dx != 0) Turn?.Invoke(dx * TurnPerPoint);
            });
            turnPad.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!turnPad.HasPointerCapture(e.pointerId)) return;
                turnPad.ReleasePointer(e.pointerId);
                if (!dragged && OnFigure(e.position)) handlers.OnCustomize();
            });
            turnPad.RegisterCallback<PointerLeaveEvent>(_ => Hover(null));
            gear = Ui.El("soldier-gear", view);
            gear.pickingMode = PickingMode.Ignore;
            figure = Ui.El("soldier-figure", gear);
            figure.pickingMode = PickingMode.Ignore;
            figureCue = Ui.Text(Customize.ToUpperInvariant(), "soldier-figure__cue font-title", figure);
            figureCue.pickingMode = PickingMode.Ignore;
            Ui.Show(figureCue, false);
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
            Ui.Show(gear, on && last?.Sheet != null && last.Customizer == null);
            Ui.Show(bottom, on && last?.MustWait != null);
            if (!on) Hover(null);
        }

        /// <summary>The figure can be clicked into Personalizar: its sheet up (not Personalizar already), one's to dress.</summary>
        bool Customizable => last?.Customizer == null && last?.Sheet?.Customizable == true;

        bool OnFigure(Vector2 p) => Customizable && (OverFigure?.Invoke(p) ?? figure.worldBound.Contains(p));

        /// <summary>The pointer at `p` (null, gone): on the figure, it lights (Arte's) and «Personalizar» shows at its foot.</summary>
        void Hover(Vector2? p)
        {
            var on = p != null && OnFigure(p.Value);
            if (on == lit) return;
            lit = on;
            Ui.Show(figureCue, on);
            turnPad.EnableInClassList("soldiers-turn--figure", on);
            LightFigure?.Invoke(on);
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
            gear.Clear();
            gear.Add(figure);
            Ui.Show(bottom, shown && v?.MustWait != null);
            Ui.Show(turnPad, shown && (v?.Sheet != null || v?.Customizer != null));
            Ui.Show(gear, shown && v?.Sheet != null && v.Customizer == null);
            if (!Customizable) Hover(null);
            if (v == null) return;
            if (v.MustWait != null) MustWait(v.MustWait);
            Grid(v, leftBody);
            Ui.Show(right, shown && (v.Sheet != null || v.Customizer != null));
            // The sheet as tall as it is (Arturo, 14/11); Personalizar, as before, down to the foot.
            right.EnableInClassList("soldiers-sheet", v.Customizer == null);
            if (v.Customizer != null) Customizer(v.Customizer, rightBody);
            else if (v.Sheet != null)
            {
                Sheet(v.Sheet, rightBody);
                Gear(v.Sheet);
            }
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

        /// <summary>
        /// The grid (Arturo, 14/11): one's own, then the partner's while away, then the recruit's tile, with no heads; the
        /// picked one known by its name's band.
        /// </summary>
        void Grid(SoldiersView v, VisualElement parent)
        {
            var grid = Ui.El("soldier-grid", parent);
            foreach (var t in v.Mine.Concat(v.Partner)) grid.Add(Tile(t));
            if (v.Recruit != null) grid.Add(Recruit(v.Recruit));
        }

        /// <summary>One's own soldiers' studio light (Arturo, 14/11: purple), and the partner's (turquoise: Arturo's choice, 15/11).</summary>
        public static Color MineLight = new Color32(0xa6, 0x78, 0xff, 0xff);
        public static Color TheirLight = new Color32(0x3f, 0xb8, 0xc8, 0xff);

        /// <summary>A bust's backdrop: its light's colour dark, lit in its middle (as the battle's squad cards: SquadList.Studio).</summary>
        static void Studio(VisualElement face, bool theirs)
        {
            var light = theirs ? TheirLight : MineLight;
            face.style.backgroundColor = new Color(light.r * 0.3f, light.g * 0.3f, light.b * 0.3f, 1);
            var studio = Ui.El("soldier-tile__studio", face);
            studio.pickingMode = PickingMode.Ignore;
            studio.style.backgroundImage = new StyleBackground(SquadList.Studio());
            studio.style.unityBackgroundImageTintColor = new Color(light.r, light.g, light.b, 0.85f);
        }

        Button Tile(SoldierTileView t)
        {
            var id = t.Id;
            var tile = new Button(() => handlers.OnSoldier(id));
            tile.AddToClassList("soldier-tile");
            tile.EnableInClassList("soldier-tile--on", t.Selected);
            tile.EnableInClassList("soldier-tile--dead", t.Dead);
            tile.EnableInClassList("soldier-tile--theirs", t.Theirs);
            if (!string.IsNullOrEmpty(t.FullName)) tips.Attach(tile, t.FullName);
            var face = Ui.El("soldier-tile__face", tile);
            Studio(face, t.Theirs);
            // The bust's edges lit from behind in its studio's colour (Arte's Portraits.Light, 0.12.24).
            Bastion.Client.Portraits.Light(t.Id, t.Theirs ? TheirLight : MineLight);
            var bust = Ui.El("soldier-tile__bust", face);
            var portrait = PortraitOf?.Invoke(t.Id);
            if (portrait is RenderTexture rt) bust.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else if (portrait is Texture2D t2) bust.style.backgroundImage = new StyleBackground(t2);
            else Ui.Icon(icon, "tab:barracks", "soldier-tile__silhouette", bust);
            if (t.Rank > 0)
            {
                // The rank as the Military Combat HUD's chevron (mixta.css .rank), bronze, silver, gold.
                var rank = Ui.Icon(icon, "rank:" + Mathf.Clamp(t.Rank, 1, 7), "soldier-tile__rank rank-badge", face);
                if (!string.IsNullOrEmpty(t.RankTip)) tips.Attach(rank, t.RankTip);
            }
            if (t.Points)
            {
                var points = Ui.Text("▲", "soldier-tile__points font-title", face);
                tips.Attach(points, t.PointsTip);
            }
            if (!string.IsNullOrEmpty(t.Tag))
            {
                var tag = Ui.Text(t.Tag, "soldier-tile__tag", face);
                if (!string.IsNullOrEmpty(t.TagTone)) tag.AddToClassList("soldier-tile__tag--" + t.TagTone);
                tips.Attach(tag, t.TagTip);
            }
            Ui.Text(t.Name, "soldier-tile__name font-title", tile);
            return tile;
        }

        /// <summary>
        /// Recruiting as one more tile (Arturo, 14/11): see-through, a «+» of worn steel, the price under it and «RECLUTAR»
        /// in its band; what a recruit is and why not, on hover (off, it says why and does nothing).
        /// </summary>
        Button Recruit(RecruitView r)
        {
            var tile = new Button(() =>
            {
                if (r.Enabled) handlers.OnRecruit();
            });
            Ui.AddClasses(tile, "soldier-tile recruit-tile");
            tile.EnableInClassList("recruit-tile--off", !r.Enabled);
            var face = Ui.El("soldier-tile__face recruit-tile__face", tile);
            Plus(face, "recruit-tile__plus");
            if (!string.IsNullOrEmpty(r.Cost)) Ui.Text(r.Cost, "recruit-tile__cost font-title", face);
            Ui.Text(r.Label.ToUpperInvariant(), "soldier-tile__name font-title", tile);
            tips.Attach(tile, string.IsNullOrEmpty(r.Why) ? r.Tip : $"{r.Why}\n{r.Tip}");
            return tile;
        }

        /// <summary>A «+» of worn steel (two bars with the plates' grain), for the recruit and an empty place of the belt.</summary>
        static void Plus(VisualElement parent, string classes)
        {
            var plus = Ui.El("steel-plus " + classes, parent);
            plus.pickingMode = PickingMode.Ignore;
            Ui.El("steel-plus__bar steel-plus__bar--across", plus).pickingMode = PickingMode.Ignore;
            Ui.El("steel-plus__bar steel-plus__bar--down", plus).pickingMode = PickingMode.Ignore;
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

        /// <summary>
        /// The sheet (Arturo, 14/11, vista-soldados p2): the name to the margin, the battle HUD's three lines under it as
        /// wide as the sheet, the partner's note and how they are when hurt or fallen, what can be done for them, the
        /// traits, and the experience with the rank and the board's button. The gear goes round the figure (Gear).
        /// </summary>
        void Sheet(SoldierSheetView s, VisualElement parent)
        {
            var title = Ui.Text(s.Name.ToUpperInvariant(), "sheet__name font-title", parent);
            tips.Attach(title, s.NameTip);
            var lines = Ui.El("sheet-lines", parent);
            Line(lines, "pips--hp", s.Hp, s.HpTip);
            Line(lines, "pips--ap", s.Ap, s.ApTip);
            Line(lines, "pips--moves", s.Moves, s.MovesTip);
            if (!string.IsNullOrEmpty(s.Note)) Ui.Text(s.Note, "sheet__note", parent);
            if (!string.IsNullOrEmpty(s.Health))
            {
                var health = Ui.Text(s.Health, "sheet__health font-title", parent);
                if (!string.IsNullOrEmpty(s.HealthTone)) health.AddToClassList("sheet__health--" + s.HealthTone);
                tips.Attach(health, s.HealthTip);
            }
            if (s.Care != null)
            {
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
            if (s.Experience != null) Experience(s.Experience, parent);
        }

        /// <summary>One of the battle's lines (its squad cards'): `n` pieces as wide as the sheet, all lit.</summary>
        void Line(VisualElement parent, string kind, int n, string tip)
        {
            var row = Ui.El("pips sheet-lines__row " + kind, parent);
            Ui.Pips(row, n, n);
            tips.Attach(row, tip);
        }

        static Label SubHead(string text, VisualElement parent) => Ui.Text(text.ToUpperInvariant(), "sheet__head font-title", parent);

        // ------------------------------------------------------------ the gear round the figure

        /// <summary>
        /// Round the figure (Arturo, 14/11): on its left the hand, the side and the belt, on its right the clothes and the
        /// belt's two places, three tiles a side in the same rows (a place that isn't there keeps its row), each column on a
        /// dark curtain with soft edges; the open choice's options beside its column.
        /// </summary>
        void Gear(SoldierSheetView s)
        {
            // The runs: the kit is Almacén's, none round the figure.
            if (s.Arms.Count == 0 && s.Armor == null && s.Belt.Count == 0) return;
            var left = Column("soldier-gear__col--left");
            var arms = s.Arms;
            for (var i = 0; i < 3; i++)
            {
                // The hand first and the belt last: with no side (pistols not counted), its row stays empty.
                var p = i == 0 ? arms.FirstOrDefault() : i == 2 ? (arms.Count > 1 ? arms[arms.Count - 1] : null) : arms.Count > 2 ? arms[1] : null;
                if (p != null) GearTile(p, left);
                else Ui.El("gear-tile gear-tile--none", left).pickingMode = PickingMode.Ignore;
            }
            var right = Column("soldier-gear__col--right");
            if (s.Armor != null) ArmorTile(s.Armor, right);
            foreach (var p in s.Belt.Take(2)) GearTile(p, right);
            var chosen = arms.Concat(s.Belt).FirstOrDefault(p => p.Id == open && p.Editable);
            if (chosen != null) Options(chosen, arms.Contains(chosen));
        }

        VisualElement Column(string side)
        {
            var col = Ui.El("soldier-gear__col " + side, gear);
            col.pickingMode = PickingMode.Ignore;
            var curtain = Ui.El("soldier-gear__curtain", col);
            curtain.pickingMode = PickingMode.Ignore;
            curtain.generateVisualContent += Curtain;
            return col;
        }

        /// <summary>
        /// A column's curtain (Arturo, 14/11: «una cortina oscura semitransparente con bordes suaves»): dark in the middle,
        /// fading to nothing over Soft at every edge.
        /// </summary>
        static void Curtain(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            float w = r.width, h = r.height;
            if (w <= 2 * CurtainSoft || h <= 2 * CurtainSoft) return;
            var dark = new Color32(4, 5, 6, 150);
            var clear = new Color32(4, 5, 6, 0);
            float[] xs = { 0, CurtainSoft, w - CurtainSoft, w };
            float[] ys = { 0, CurtainSoft, h - CurtainSoft, h };
            var vertices = new Vertex[16];
            for (var j = 0; j < 4; j++)
                for (var i = 0; i < 4; i++)
                    vertices[j * 4 + i] = new Vertex { position = new Vector3(xs[i], ys[j], Vertex.nearZ), tint = i == 0 || i == 3 || j == 0 || j == 3 ? clear : dark };
            var indices = new ushort[54];
            var k = 0;
            for (var j = 0; j < 3; j++)
                for (var i = 0; i < 3; i++)
                {
                    var a = (ushort)(j * 4 + i);
                    foreach (var n in new[] { 0, 1, 5, 0, 5, 4 }) indices[k++] = (ushort)(a + n);
                }
            var mesh = ctx.Allocate(16, 54);
            mesh.SetAllVertices(vertices);
            mesh.SetAllIndices(indices);
        }

        /// <summary>How far a curtain fades at its edges (panel pixels).</summary>
        const float CurtainSoft = 18;

        /// <summary>
        /// A place of the gear: what is there (its picture; an empty place, a «+»), its name under it, a worn weapon's
        /// state at its foot; what it is on hover; a click opens its choice (or shuts it).
        /// </summary>
        void GearTile(PickView p, VisualElement col)
        {
            var id = p.Id;
            var tile = new Button(() =>
            {
                if (!p.Editable) return;
                open = open == id ? null : id;
                key = null;
                Set(last);
            });
            tile.name = "gear-" + id;
            tile.AddToClassList("gear-tile");
            tile.EnableInClassList("gear-tile--open", open == id);
            tile.EnableInClassList("gear-tile--fixed", !p.Editable);
            var face = Ui.El("gear-tile__face", tile);
            face.pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(p.Icon)) Ui.Icon(icon, p.Icon, "gear-tile__icon", face).pickingMode = PickingMode.Ignore;
            else Plus(face, "gear-tile__plus");
            if (p.Condition >= 0)
            {
                var gauge = Ui.El("gear-tile__gauge", face);
                var fill = Ui.El("gear-tile__gauge-fill", gauge);
                fill.style.width = Length.Percent(Mathf.Clamp(p.Condition, 0, 100));
                fill.EnableInClassList("gear-tile__gauge-fill--worn", p.Condition < 30);
            }
            Ui.Text(p.Label.ToUpperInvariant(), "gear-tile__label font-title", tile).pickingMode = PickingMode.Ignore;
            var help = new System.Collections.Generic.List<string> { p.Value };
            help.AddRange(p.Lines);
            if (!string.IsNullOrEmpty(p.ConditionText)) help.Add(p.ConditionText);
            if (p.Tags.Count > 0) help.Add(string.Join(" · ", p.Tags));
            tips.Attach(tile, string.Join("\n", help.Where(x => !string.IsNullOrEmpty(x))));
            col.Add(tile);
        }

        /// <summary>The clothes (the Taller's, everyone's): their picture, their name and what they give; the next ones and why not, on hover.</summary>
        void ArmorTile(ArmorView a, VisualElement col)
        {
            var tile = Ui.El("gear-tile gear-tile--fixed gear-tile--armor", col);
            var face = Ui.El("gear-tile__face", tile);
            Ui.Icon(icon, "stat:armor", "gear-tile__icon", face);
            Ui.Text(a.Name.ToUpperInvariant(), "gear-tile__label gear-tile__label--name font-title", tile);
            Ui.Text(a.Figures, "gear-tile__figures", tile);
            tips.Attach(tile, string.IsNullOrEmpty(a.Next) ? $"{a.Name}: {a.Figures}" : $"{a.Name}: {a.Figures}\n{a.Next}: {a.NextTip}");
        }

        /// <summary>
        /// A choice open (arms.ts weaponPick): its options in a plate beside its column, inwards over the figure: the
        /// picture, the name and its marks, what it is, against what it carries now; a click picks it.
        /// </summary>
        void Options(PickView p, bool left)
        {
            var plate = Ui.El("gear-options panel " + (left ? "gear-options--left" : "gear-options--right"), gear);
            Ui.Text($"{p.Label.ToUpperInvariant()} · {p.Value}", "gear-options__head font-title", plate);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Auto, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("gear-options__scroll");
            plate.Add(scroll);
            var list = scroll.contentContainer;
            var id = p.Id;
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

        // ------------------------------------------------------------ experience

        /// <summary>
        /// The experience (Arturo, 14/11, p2): each line its name and its green growth on the left, its experience into the
        /// bar on the right, a continuous bar under them (white done, grey to go); «Rango» and the rank's name over its five
        /// pieces, as wide; the board's button with its own plate and full outline, the points to spend beside it.
        /// </summary>
        void Experience(SkillsView x, VisualElement parent)
        {
            SubHead(x.Head, parent);
            foreach (var r in x.Rows)
            {
                var row = Ui.El("skill-row", parent);
                var top = Ui.El("skill-row__top", row);
                Ui.Text(r.Name, "skill-row__name font-title", top);
                if (!string.IsNullOrEmpty(r.Growth))
                {
                    var growth = Ui.Text(r.Growth, "skill-row__growth font-title", top);
                    if (!string.IsNullOrEmpty(r.GrowthTip)) tips.Attach(growth, r.GrowthTip);
                }
                Ui.Text(r.Value.ToString(), "skill-row__value font-title", top);
                var bar = Ui.El("skill-row__bar", row);
                Ui.El("skill-row__fill", bar).style.width = Length.Percent(Mathf.Clamp01(r.Share) * 100);
                if (!string.IsNullOrEmpty(r.Tip)) tips.Attach(row, r.Tip);
            }
            var rank = Ui.El("skill-row skill-rank", parent);
            var rankTop = Ui.El("skill-row__top", rank);
            Ui.Text(x.RankLabel, "skill-row__name font-title", rankTop);
            Ui.Text(x.Rank, "skill-row__value font-title", rankTop);
            var marks = Ui.El("skill-rank__bar", rank);
            for (var i = 0; i < x.RankTotal; i++) Ui.El(i < x.RankFilled ? "skill-rank__seg skill-rank__seg--on" : "skill-rank__seg", marks);
            if (!string.IsNullOrEmpty(x.RankTip)) tips.Attach(rank, x.RankTip);
            var foot = Ui.El("skill-foot", parent);
            var board = new Button(handlers.OnBoard) { text = x.Board.ToUpperInvariant() };
            Ui.AddClasses(board, "skill-board font-title");
            board.EnableInClassList("skill-board--pulse", x.BoardPulse);
            foot.Add(board);
            if (!string.IsNullOrEmpty(x.BoardPoints)) Ui.Text(x.BoardPoints, "skill-foot__points font-title", foot);
        }
    }
}
