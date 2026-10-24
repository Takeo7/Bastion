using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// Whose turn it is, three ways at once (hud.ts setTurn; docs/UNITY-UI.md §2.5): the label on a tab of that player's
    /// colour, the frame round the screen in it (fainter for the partner's), and the action bar resting when the orders
    /// aren't ours (ActionBarView.Lock, set by the presenter). Beside it the concealment, the night and the soldier in
    /// play; then the floor (setFloor) and the players (setPlayers).
    /// </summary>
    public sealed class TurnStrip
    {
        static readonly Color Stamp = new Color32(0xb3, 0x26, 0x1e, 0xff);

        readonly VisualElement turn;
        readonly VisualElement frame;
        readonly VisualElement players;
        readonly VisualElement floor;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        readonly System.Func<string, Sprite> icon;
        string turnKey;
        string playersKey;
        string floorKey;

        public TurnStrip(VisualElement root, IHudHandlers handlers, TooltipLayer tips, System.Func<string, Sprite> icon = null)
        {
            this.icon = icon;
            turn = root.Q("turn");
            frame = root.Q("turn-frame");
            players = root.Q("players");
            floor = root.Q("floor");
            this.handlers = handlers;
            this.tips = tips;
            Ui.Show(floor, false);
        }

        public void SetTurn(TurnView v)
        {
            var k = Ui.Key(v);
            if (k == turnKey) return;
            turnKey = k;
            turn.Clear();
            var owner = v?.Owner ?? "over";
            var colour = owner == "enemy" ? Stamp : Ui.Hex(v?.Color, Stamp);
            foreach (var o in new[] { "mine", "partner", "enemy", "over" })
                frame.EnableInClassList("turn-frame--" + o, owner == o);
            foreach (var edge in frame.Children()) edge.style.backgroundColor = colour;
            if (v == null) return;

            var text = owner == "mine" ? HudText.Turn.Mine : owner == "partner" ? HudText.Turn.Partner(v.OwnerName) : owner == "enemy" ? HudText.Turn.Enemy : HudText.Turn.Over;
            var label = Ui.Text(text, "turn-label font-title turn-label--" + owner, turn);
            if (owner == "mine" || owner == "partner") label.style.backgroundColor = colour;
            // The number goes in the tooltip: the strip has no room for it next to a partner's name.
            tips.Attach(label, HudText.Turn.Number(v.Turn));
            if (!string.IsNullOrEmpty(v.InPlay)) tips.Attach(Ui.Text(HudText.Turn.InPlay(v.InPlay), "turn__in-play", turn), HudText.Turn.InPlayTip);
            Ui.Text(v.Concealed ? HudText.Turn.Concealed : HudText.Turn.Detected, v.Concealed ? "conceal conceal--on" : "conceal conceal--off", turn);
            if (!string.IsNullOrEmpty(v.NightLabel)) tips.Attach(Ui.Text(v.NightLabel.ToUpperInvariant(), "conceal conceal--night", turn), v.NightRules);
        }

        public void SetPlayers(PlayersView v)
        {
            var k = Ui.Key(v);
            if (k == playersKey) return;
            playersKey = k;
            players.Clear();
            if (v == null) return;
            foreach (var p in v.Players)
            {
                var colour = Ui.Hex(p.Color, Color.white);
                var row = Ui.El("player", players);
                row.EnableInClassList("player--me", p.Me);
                row.EnableInClassList("player--ready", p.Ready);
                row.EnableInClassList("player--offline", !p.Connected);
                row.EnableInClassList("player--command", p.Command);
                if (p.Command) row.style.borderBottomColor = colour;
                Ui.El("player__dot", row).style.backgroundColor = colour;
                Ui.Text(p.Name, "player__name", row);
                if (!string.IsNullOrEmpty(p.Status)) Ui.Text(p.Status, "player__status", row);
            }
        }

        public void SetFloor(FloorView v)
        {
            var k = Ui.Key(v);
            if (k == floorKey) return;
            floorKey = k;
            floor.Clear();
            Ui.Show(floor, v != null && v.Max > 0);
            if (v == null || v.Max <= 0) return;
            var down = new Button(() => handlers.OnFloor(-1));
            down.Add(new Triangle(false));
            down.SetEnabled(v.Level > 0);
            tips.Attach(down, HudText.Floor.Down);
            floor.Add(down);
            tips.Attach(Ui.Text(HudText.Floor.Of(v.Level, v.Max).ToUpperInvariant(), "floor-ind__label", floor), HudText.Floor.Tip);
            var up = new Button(() => handlers.OnFloor(1));
            up.Add(new Triangle(true));
            up.SetEnabled(v.Level < v.Max);
            tips.Attach(up, HudText.Floor.Up);
            floor.Add(up);
        }
    }
}
