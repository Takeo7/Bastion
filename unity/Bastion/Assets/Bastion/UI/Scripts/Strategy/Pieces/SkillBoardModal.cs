using System;
using Bastion.Engine;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The skill board's window (skillSheet.ts treePanel): «TABLERO DE HABILIDADES · NOVATO», the soldier's round bust,
    /// name, rank and points to spend; the board on the left and the card of the cell under the pointer on the right
    /// (its icon, name, kind and line, its story and what it does); «Cerrar». The board is made once and kept across
    /// redraws (its wave and focus stay).
    /// </summary>
    public sealed class SkillBoardModal
    {
        readonly VisualElement host;
        readonly IStrategyHandlers handlers;
        readonly TooltipLayer tips;
        readonly SkillBoard board;
        readonly VisualElement head;
        readonly VisualElement card;
        SkillBoardView last;
        string key;

        /// <summary>A soldier's bust by id; null, the silhouette.</summary>
        public Func<string, Texture> PortraitOf;

        public SkillBoardModal(VisualElement host, IStrategyHandlers handlers, Action<string> sound, TooltipLayer tips = null)
        {
            this.host = host;
            this.handlers = handlers;
            this.tips = tips;
            var panel = Ui.El("board-modal panel", host);
            head = Ui.El("board-modal__head", panel);
            var body = Ui.El("board-modal__body", panel);
            var area = Ui.El("board-modal__board well", body);
            board = new SkillBoard(sound);
            area.Add(board);
            card = Ui.El("board-modal__card well", body);
            var foot = Ui.El("board-modal__foot", panel);
            var close = new Button(handlers.OnBoardClose) { text = "CERRAR" };
            Ui.AddClasses(close, "btn-primary font-title");
            foot.Add(close);
            board.Shown += id => Card(id);
            board.Bought += id => handlers.OnUnlock(id);
            Ui.Show(host, false);
        }

        public bool Open => host.resolvedStyle.display != DisplayStyle.None;

        public void Set(SkillBoardView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            last = v;
            Ui.Show(host, v != null);
            if (v == null) return;
            head.Clear();
            Ui.Text(v.Kicker.ToUpperInvariant(), "board-modal__kicker font-title", head);
            var row = Ui.El("board-modal__row", head);
            var bust = Ui.El("board-modal__bust", row);
            var portrait = PortraitOf?.Invoke(v.SoldierId);
            if (portrait is RenderTexture rt) bust.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else if (portrait is Texture2D t2) bust.style.backgroundImage = new StyleBackground(t2);
            Ui.Text(v.Name.ToUpperInvariant(), "board-modal__name font-title", row);
            Ui.Text(v.Rank.ToUpperInvariant(), "board-modal__rank font-title", row);
            var bar = Ui.El("board-modal__rank-bar", row);
            for (var i = 0; i < v.RankTotal; i++) Ui.El(i < v.RankFilled ? "board-modal__rank-seg board-modal__rank-seg--on" : "board-modal__rank-seg", bar);
            Ui.Text(v.Will, "board-modal__will", row);
            var pill = Ui.El("board-modal__points", row);
            pill.EnableInClassList("board-modal__points--some", v.Points > 0);
            Ui.Text(v.Points.ToString(), "board-modal__points-n font-title", pill);
            Ui.Text(v.Points == 1 ? "punto por gastar" : "puntos por gastar", "board-modal__points-text font-title", pill);
            tips?.Attach(pill, StrategyText.Board.PointsTip);
            board.Set(v.Board, v.Points, v.Editable);
            board.Select(Board.Core);
            Card(Board.Core);
        }

        /// <summary>The card of a cell: its icon, name, kind and line, its story, what it does (nothing for an out-of-reach one).</summary>
        void Card(string id)
        {
            card.Clear();
            if (last == null || !Board.BoardCells.TryGetValue(id, out var c)) return;
            var top = Ui.El("board-card__top", card);
            SkillBoard.LoadAtlases();
            var icon = Ui.El("board-card__icon", top);
            icon.style.backgroundImage = new StyleBackground(SkillBoard.IconOf(id, Bars.CellState(last.Board, id) == "unlocked"));
            var words = Ui.El("board-card__words", top);
            Ui.Text(c.Name, "board-card__name font-title", words);
            var kind = Board.CellKindNames.TryGetValue(c.Kind, out var k) ? k : c.Kind;
            var line = c.Line != null && Bars.SkillDefs.TryGetValue(c.Line, out var skill) ? " · " + skill.Name : "";
            Ui.Text((kind + line).ToUpperInvariant(), "board-card__kind font-title", words);
            if (Lore.BoardAbout.TryGetValue(id, out var about)) Ui.Text(about, "board-card__about", card);
            Ui.Text(c.Effect, "board-card__effect", card);
            if (last.Editable && last.Points < 1 && Bars.CellState(last.Board, id) == "open") Ui.Text("Sin puntos por gastar", "board-card__none", card);
        }
    }
}
