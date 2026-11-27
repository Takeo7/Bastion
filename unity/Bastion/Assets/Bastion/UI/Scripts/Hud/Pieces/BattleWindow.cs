using System;
using Bastion.UI.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The window over the battle (hud.ts modal: soldierSheet, enemyFile), only to look at: nothing is spent, the command
    /// stays where it was. The soldier's sheet (H, or a click on its name): who, its figures, what it carries and its
    /// skills; in a campaign its board beside, the Cuartel's window (SkillBoardModal), not to be touched. An enemy's file
    /// (right click): what it is, what it does, how to beat it. «CERRAR», Esc or a press outside the card closes it.
    /// </summary>
    public sealed class BattleWindow
    {
        readonly VisualElement layer;
        readonly TooltipLayer tips;
        readonly Action<string> sound;
        readonly StyleSheet boardSheet;
        VisualElement boardHost;
        SkillBoardModal board;
        /// <summary>The soldier whose board is shown, while its bust may still come (Portraits.Changed).</summary>
        string boardOf;

        /// <summary>The HUD's icons (HudRoot), for the sheet's figures.</summary>
        public Func<string, Sprite> Icon;

        public BattleWindow(VisualElement host, TooltipLayer tips, Action<string> sound)
        {
            this.tips = tips;
            this.sound = sound;
            layer = Ui.El("battle-window", host);
            // With the mouse the window sits over the squad's plate, not on it (cuadro 180): the sheet in the card stays in sight.
            layer.EnableInClassList("battle-window--up", !Bastion.Client.GameInput.Touch);
            // Its own sheet, and the Cuartel's for the board: loaded here, on the window alone (no UXML to import first).
            var own = Resources.Load<StyleSheet>("Hud/BattleWindow");
            if (own != null) layer.styleSheets.Add(own);
            boardSheet = Resources.Load<StyleSheet>("Strategy/Soldiers");
            // A press on the dim around the card closes it.
            layer.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == layer) Close();
            });
            Ui.Show(layer, false);
        }

        public bool Open { get; private set; }

        /// <summary>Closes it; whether it was open (Esc closes it before anything else).</summary>
        public bool Close()
        {
            Listen(null);
            if (!Open) return false;
            Open = false;
            Ui.Show(layer, false);
            return true;
        }

        public void Sheet(SoldierSheetView v)
        {
            var card = Begin("battle-window__card battle-window__card--sheet");
            card.EnableInClassList("battle-window__card--board", v.Board != null);
            var side = Ui.El("battle-sheet__side", card);
            Ui.Text(HudText.Window.SheetKicker, "battle-window__kicker font-title", side);
            // With the mouse, the sheet to read with the eyes (cuadro 180): figures under their icons, each one's name its
            // help; with the finger there is no pointer for that help, and the lines stay.
            if (!Bastion.Client.GameInput.Touch && v.Bars.Count > 0)
            {
                card.AddToClassList("battle-window__card--figures");
                Figures(side, v);
                // (With no board beside, its skills by name, as before.)
                if (v.Board == null && v.Skills.Count > 0)
                {
                    Ui.Text(HudText.Window.Skills.ToUpperInvariant(), "battle-sheet__head font-title", side);
                    foreach (var name in v.Skills) Ui.Text("· " + name, "battle-sheet__skill", side);
                }
                Ui.El("battle-sheet__fill", side);
                CloseButton(side);
                if (v.Board != null) Board(card, v);
                return;
            }
            Ui.Text(v.Name, "battle-window__title font-title", side);
            var rank = Ui.El("battle-sheet__rank", side);
            Ui.Text(v.Rank, "battle-sheet__rank-text", rank);
            if (!string.IsNullOrEmpty(v.Whose))
                Ui.Text(HudText.Window.Whose(v.Whose), "battle-sheet__whose", rank).style.color = Ui.Hex(v.WhoseColor, Color.white);
            Rows(v.Stats, "battle-sheet__rows", side);
            Ui.Text(HudText.Window.Carries.ToUpperInvariant(), "battle-sheet__head font-title", side);
            Rows(v.Carries, "battle-sheet__rows battle-sheet__rows--carries", side);
            if (v.Board == null && v.Skills.Count > 0)
            {
                Ui.Text(HudText.Window.Skills.ToUpperInvariant(), "battle-sheet__head font-title", side);
                foreach (var name in v.Skills) Ui.Text("· " + name, "battle-sheet__skill", side);
            }
            Ui.El("battle-sheet__fill", side);
            CloseButton(side);
            if (v.Board == null) return;
            Board(card, v);
        }

        void Board(VisualElement card, SoldierSheetView v)
        {
            // The Cuartel's board window, made once and kept (its wave and focus stay), its own «Cerrar» hidden: the sheet's closes.
            if (board == null)
            {
                boardHost = new VisualElement();
                boardHost.AddToClassList("battle-sheet__board");
                if (boardSheet != null) boardHost.styleSheets.Add(boardSheet);
                board = new SkillBoardModal(boardHost, new StrategyRelay(), sound, tips) { PortraitOf = Bastion.Client.Portraits.Soldier };
            }
            card.Add(boardHost);
            board.Set(v.Board);
            // Its bust, asked for as the sheet opened (the controller's Portraits.Know), in when drawn.
            Listen(v.Board.SoldierId);
        }

        /// <summary>Listens for `soldier`'s bust while the sheet is up (null: not any more).</summary>
        void Listen(string soldier)
        {
            if (boardOf == null && soldier != null) Bastion.Client.Portraits.Changed += OnPortrait;
            if (boardOf != null && soldier == null) Bastion.Client.Portraits.Changed -= OnPortrait;
            boardOf = soldier;
        }

        void OnPortrait(string id)
        {
            if (Open && id == boardOf) board?.Repaint();
        }

        public void File(EnemyFileView v)
        {
            Listen(null);
            var card = Begin("battle-window__card battle-window__card--file");
            Ui.Text(HudText.Window.FileKicker, "battle-window__kicker battle-window__kicker--file font-title", card);
            Ui.Text(v.Name, "battle-window__title font-title", card);
            var stats = Ui.Text(v.Stats, "battle-file__stats", card);
            if (!string.IsNullOrEmpty(v.StatsTip)) tips.Attach(stats, v.StatsTip);
            Ui.Text(v.What, "battle-file__text", card);
            Ui.Text(v.Does, "battle-file__text", card);
            Ui.Text(v.Tip, "battle-file__tip", card);
            CloseButton(card);
        }

        /// <summary>The window open with a new card in it.</summary>
        VisualElement Begin(string classes)
        {
            if (boardHost != null) boardHost.RemoveFromHierarchy();
            layer.Clear();
            var card = Ui.El("panel " + classes, layer);
            PlateFrame.Dress(card);
            if (!Open) sound?.Invoke("paper");
            Open = true;
            Ui.Show(layer, true);
            return card;
        }

        /// <summary>
        /// The sheet's left side, to read with the eyes (Arturo, cuadro 180): the bars; the soldier's own figures; each
        /// weapon with its picture, its odds («%», white) apart from its damage (the shot's panel's red) and the rest
        /// (grey); and what else it carries, as pictures. Whoever it is, on the board's head beside it; without a board,
        /// its name here.
        /// </summary>
        void Figures(VisualElement side, SoldierSheetView v)
        {
            if (v.Board == null)
            {
                Ui.Text(v.Name, "battle-window__title font-title", side);
                Ui.Text(v.Rank, "battle-sheet__rank-text", Ui.El("battle-sheet__rank", side));
            }
            if (!string.IsNullOrEmpty(v.Whose)) Ui.Text(HudText.Window.Whose(v.Whose), "battle-sheet__whose", side).style.color = Ui.Hex(v.WhoseColor, Color.white);
            VisualElement Pic(VisualElement parent, string id, string classes)
            {
                var e = Ui.El(classes, parent);
                e.pickingMode = PickingMode.Ignore;
                var sprite = Icon?.Invoke(id);
                if (sprite != null) e.style.backgroundImage = new StyleBackground(sprite);
                else if (LineIcon.Has(id)) e.Add(new LineIcon(id));
                return e;
            }
            string Help(string name, string tip) => string.IsNullOrEmpty(tip) ? name : name + "\n" + tip;

            var bars = Ui.El("sheet-bars", side);
            foreach (var b in v.Bars)
            {
                if (b.Max <= 0) continue;
                var row = Ui.El("sheet-bar", bars);
                Pic(row, b.Icon, "sheet-bar__icon");
                Ui.Pips(Ui.El("pips pips--" + b.Kind + " sheet-bar__pips", row), b.Now, b.Max);
                Ui.Text($"{b.Now}/{b.Max}", "sheet-bar__n font-title", row);
                tips.Attach(row, Help(b.Name, b.Tip));
            }
            void Figure(VisualElement parent, SheetFigureView f, string kind)
            {
                var e = Ui.El("sheet-figure sheet-figure--" + kind, parent);
                Pic(e, f.Icon, "sheet-figure__icon");
                Ui.Text(f.Value, "sheet-figure__n font-title", e).pickingMode = PickingMode.Ignore;
                tips.Attach(e, Help(f.Name, f.Tip));
            }
            var guards = Ui.El("sheet-guards", side);
            foreach (var g in v.Guards) Figure(guards, g, "own");

            VisualElement Thing(VisualElement parent, string kind, string type, string icon, int count, string name, string tip)
            {
                var (w, h) = Bastion.Engine.Content.GearSize(kind, type);
                var cell = Ui.El("sheet-thing", parent);
                cell.style.width = 46 * w;
                cell.style.height = 46 * h;
                if (Bastion.Client.GearPictures.Of(kind, type) is Texture2D picture) cell.style.backgroundImage = new StyleBackground(picture);
                else if (!string.IsNullOrEmpty(icon)) Pic(cell, icon, "sheet-thing__icon");
                if (name != null) Ui.Text(name, "sheet-thing__name", cell).pickingMode = PickingMode.Ignore;
                if (count > 1) Ui.Text(count.ToString(), "sheet-thing__n font-title", cell).pickingMode = PickingMode.Ignore;
                tips.Attach(cell, tip);
                return cell;
            }
            foreach (var w in v.Weapons)
            {
                var block = Ui.El("sheet-weapon", side);
                Thing(block, w.Kind, w.Type, null, 1, w.Name, w.Tip);
                var figures = Ui.El("sheet-weapon__figures", block);
                var odds = Ui.El("sheet-weapon__group", figures);
                foreach (var f in w.Odds) Figure(odds, f, "odds");
                var damage = Ui.El("sheet-weapon__group sheet-weapon__group--damage", figures);
                foreach (var f in w.Damage) Figure(damage, f, "damage");
                var rest = Ui.El("sheet-weapon__group sheet-weapon__group--rest", block);
                foreach (var f in w.Rest) Figure(rest, f, "rest");
            }
            if (v.Things.Count > 0)
            {
                var things = Ui.El("sheet-things", side);
                var packed = false;
                foreach (var t in v.Things)
                {
                    // The pack's after the backpack's own drawing.
                    if (t.Packed && !packed)
                    {
                        packed = true;
                        tips.Attach(Pic(things, "search", "sheet-things__pack"), HudText.Pack.PackHead);
                    }
                    Thing(things, t.Kind, t.Type, t.Icon, t.Count, null, t.Tip);
                }
            }
        }

        void Rows(System.Collections.Generic.List<SheetRowView> rows, string classes, VisualElement parent)
        {
            var list = Ui.El(classes, parent);
            foreach (var r in rows)
            {
                var row = Ui.El("battle-sheet__row", list);
                Ui.Text(r.Key, "battle-sheet__key", row);
                Ui.Text(r.Value, "battle-sheet__value", row);
                if (!string.IsNullOrEmpty(r.Tip)) tips.Attach(row, r.Tip);
            }
        }

        void CloseButton(VisualElement parent)
        {
            var foot = Ui.El("battle-window__foot", parent);
            var close = new Button(() => Close()) { text = HudText.Window.Close };
            Ui.AddClasses(close, "btn-primary font-title");
            foot.Add(close);
        }
    }
}
