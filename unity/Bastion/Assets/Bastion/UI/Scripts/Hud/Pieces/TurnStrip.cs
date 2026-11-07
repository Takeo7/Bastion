using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// Whose turn it is (hud.ts setTurn; docs/UNITY-UI.md §2.5): the frame round the screen in that player's colour
    /// (fainter for the partner's), and the action bar resting when the orders aren't ours (ActionBarView.Lock, set by the
    /// presenter); the stamp in the middle as each turn starts (Banner). The tab with «TU TURNO» and «OCULTOS» went, and the
    /// floors' (UI-COMBATE UI-03, UI-10; the floors keep their keys). At the top, the night and the soldier in play; then the
    /// players (setPlayers).
    /// </summary>
    public sealed class TurnStrip
    {
        static readonly Color Stamp = new Color32(0xb3, 0x26, 0x1e, 0xff);

        readonly VisualElement turn;
        readonly VisualElement frame;
        readonly VisualElement players;
        readonly TooltipLayer tips;
        string turnKey;
        string playersKey;

        public TurnStrip(VisualElement root, TooltipLayer tips)
        {
            turn = root.Q("turn");
            frame = root.Q("turn-frame");
            players = root.Q("players");
            this.tips = tips;
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
            Ui.Show(frame, v == null || !v.Alone);
            if (v == null) return;
            if (!string.IsNullOrEmpty(v.InPlay)) tips.Attach(Ui.Text(HudText.Turn.InPlay(v.InPlay), "turn__in-play", turn), HudText.Turn.InPlayTip);
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
                // Who holds the command, in words beside the name, in their colour (Arturo, 7/11: «no queda claro»).
                if (p.Command) Ui.Text(HudText.Players.Command, "player__command font-title", row).style.color = colour;
                if (!string.IsNullOrEmpty(p.Status)) Ui.Text(p.Status, "player__status", row);
            }
        }
    }
}
