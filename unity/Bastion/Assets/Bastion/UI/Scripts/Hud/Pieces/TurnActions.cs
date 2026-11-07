using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// «Terminar turno» as the bar's last key, and the turn's clock under the whole row (UI-COMBATE UI-04, UI-05; hud.ts
    /// setEndTurn, tickClocks; docs/UNITY-UI.md §2.6): a square key like the others with →| alone on it, in their grey; its
    /// state by its border and its icon (armed for the second press, the accent round it as a key armed; ready, the check;
    /// off while the turn isn't this player's to end), its words in its help. Under the row, this player's clock alone, a
    /// bar in their colour as wide as the row (Ana, 7/11 #9: each sees their own; the partner's thin one is gone).
    /// «Ceder el mando», «Ver a Ana», «Ir a Mateo» and the camera switch are in the game's menu now (UI-07).
    /// </summary>
    public sealed class TurnActions
    {
        const string FollowKey = "bastion.followTurn";

        readonly TooltipLayer tips;
        readonly Button end;
        readonly VisualElement endIcon;
        readonly LineIcon endMark;
        readonly Sprite check;
        readonly VisualElement clockBar;
        readonly VisualElement clockFill;
        string endKey;

        /// <param name="slot">Where the key goes, beside the bar's.</param>
        /// <param name="clocks">Under the row, as wide as it.</param>
        public TurnActions(VisualElement slot, VisualElement clocks, IHudHandlers handlers, TooltipLayer tips, System.Func<string, Sprite> icon = null)
        {
            this.tips = tips;
            // The bar's own key (AbilityButton): its size, steel, well and stripes when off.
            var tree = Ui.Clone("AbilityButton");
            end = tree.Q<Button>("key");
            end.RemoveFromHierarchy();
            end.name = "end";
            end.AddToClassList("end-turn");
            var stripes = new Stripes();
            stripes.AddToClassList("ability__stripes");
            end.Insert(0, stripes);
            foreach (var mark in new[] { "note", "count", "cooldown", "stamp" }) Ui.Show(end.Q(mark), false);
            endIcon = end.Q("icon");
            endMark = new LineIcon("endTurn");
            endIcon.Add(endMark);
            check = icon?.Invoke("ui:check");
            slot.Add(end);
            end.clicked += () => handlers.OnEndTurn();

            clockBar = Ui.El("turn-clock well turn-clock--mine", clocks);
            clockFill = Ui.El("turn-clock__fill", clockBar);
            clockBar.pickingMode = PickingMode.Ignore;
            clockFill.pickingMode = PickingMode.Ignore;
            SetEndTurn(null);
        }

        /// <summary>
        /// Whether the camera goes to the soldier whose turn it is (game.ts turnCamera): «Cámara al soldado», in the game's
        /// menu now, kept on this computer.
        /// </summary>
        public static bool FollowsTurn
        {
            get => PlayerPrefs.GetInt(FollowKey, 1) != 0;
            set => PlayerPrefs.SetInt(FollowKey, value ? 1 : 0);
        }

        public void SetEndTurn(EndTurnView v)
        {
            var k = Ui.Key(v);
            if (k == endKey) return;
            endKey = k;
            var mode = v?.Mode ?? "disabled";
            foreach (var m in new[] { "ready", "waiting", "confirm", "enemy" })
                end.EnableInClassList("end-turn--" + m, mode == m);
            // Armed for its second press: the accent round it, as any key armed.
            end.EnableInClassList("ability--active", mode == "confirm");
            end.SetEnabled(mode != "disabled" && mode != "waiting" && mode != "enemy");
            var ready = mode == "ready" && check != null;
            endIcon.style.backgroundImage = ready ? new StyleBackground(check) : new StyleBackground(StyleKeyword.None);
            Ui.Show(endMark, !ready);
            var detail = v?.Detail;
            tips.Attach(end, mode switch
            {
                "confirm" => HudText.EndTurn.Confirm(detail),
                "ready" => !string.IsNullOrEmpty(v?.Holder) ? HudText.EndTurn.ReadyHolder(v.Holder) : string.IsNullOrEmpty(detail) ? HudText.EndTurn.Ready : HudText.EndTurn.Waiting(detail),
                "waiting" => HudText.EndTurn.Command(detail ?? ""),
                // The enemy turn plays (docs/UX-RITMO.md): the space bar held shows it faster.
                "enemy" => HudText.EndTurn.EnemyTurn,
                // A soldier in play: its turn first, the whole turn on a second press (Arturo, 7/11, Ana's #5).
                "done" => HudText.EndTurn.Done(v.InPlay, v.GoesTo),
                "act" => HudText.EndTurn.Act,
                _ => HudText.EndTurn.Off,
            });
        }

        /// <summary>
        /// This player's clock, every frame (game.ts frame), as the bar's width; white when little is left, red at the end
        /// (the partner's no longer shows: Ana, 7/11 #9). Outside the squad's turn the bar goes but keeps its room: the row
        /// stays where it is. Only styles change: no layout is rebuilt.
        /// </summary>
        public void TickClock(ClockView c)
        {
            var shown = c != null && c.Shown;
            clockBar.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
            if (!shown) return;
            var left = Mathf.Clamp01(c.Left);
            clockFill.style.width = Length.Percent(left * 100);
            // In the player's colour; white and red (USS) when little is left.
            if (left < 0.25f) clockFill.style.backgroundColor = StyleKeyword.Null;
            else clockFill.style.backgroundColor = Ui.Hex(c.Color, Color.white);
            clockBar.EnableInClassList("turn-clock--running", c.Running);
            clockBar.EnableInClassList("turn-clock--low", left < 0.25f);
            clockBar.EnableInClassList("turn-clock--out", left < 0.1f);
        }
    }
}
