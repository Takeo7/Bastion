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

        public BattleWindow(VisualElement host, TooltipLayer tips, Action<string> sound)
        {
            this.tips = tips;
            this.sound = sound;
            layer = Ui.El("battle-window", host);
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
            Ui.Text(v.Stats, "battle-file__stats", card);
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
