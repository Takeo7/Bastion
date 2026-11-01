using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// «Terminar turno» as the bar's last key, and the turn's clock under the whole row (UI-COMBATE UI-04, UI-05; hud.ts
    /// setEndTurn, tickClocks; docs/UNITY-UI.md §2.6): a square key like the others with →| alone on it, in their grey; its
    /// state by its border and its icon (armed for the second press, the accent round it as a key armed; ready, the check;
    /// off while the turn isn't this player's to end), its words in its help. Under the row, this player's clock, a bar
    /// in their colour as wide as the row, and the partner's, thin, under it. «Ceder el mando», «Ver a Ana», «Ir a Mateo»
    /// and the camera switch are in the game's menu now (UI-07).
    /// </summary>
    public sealed class TurnActions
    {
        const string FollowKey = "bastion.followTurn";

        readonly TooltipLayer tips;
        readonly Button end;
        readonly VisualElement endIcon;
        readonly LineIcon endMark;
        readonly Sprite check;
        readonly VisualElement[] clockBars = new VisualElement[2];
        readonly VisualElement[] clockFills = new VisualElement[2];
        string endKey;
        bool paired;

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

            for (var i = 0; i < 2; i++)
            {
                clockBars[i] = Ui.El("turn-clock well " + (i == 0 ? "turn-clock--mine" : "turn-clock--theirs"), clocks);
                clockFills[i] = Ui.El("turn-clock__fill", clockBars[i]);
                clockBars[i].pickingMode = PickingMode.Ignore;
                clockFills[i].pickingMode = PickingMode.Ignore;
            }
            Ui.Show(clockBars[1], false);
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

        /// <summary>With a partner, the room for their clock under this player's, so the row doesn't move as it comes and goes.</summary>
        public bool Paired
        {
            set
            {
                paired = value;
                Ui.Show(clockBars[1], paired);
            }
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
                "ready" => string.IsNullOrEmpty(detail) ? HudText.EndTurn.Ready : HudText.EndTurn.Waiting(detail),
                "waiting" => HudText.EndTurn.Command(detail ?? ""),
                // The enemy turn plays (docs/UX-RITMO.md): the space bar held shows it faster.
                "enemy" => HudText.EndTurn.EnemyTurn,
                "act" => HudText.EndTurn.Act,
                _ => HudText.EndTurn.Off,
            });
        }

        /// <summary>
        /// The clocks, every frame (game.ts frame): this player's [0] and the partner's [1], as the bars' width; amber
        /// when little is left, red at the end. Outside the squad's turn the bars go but keep their room: the row stays
        /// where it is. Only styles change: no layout is rebuilt.
        /// </summary>
        public void TickClocks(ClockView mine, ClockView theirs)
        {
            Tick(0, mine);
            Tick(1, paired ? theirs : null);
        }

        void Tick(int i, ClockView c)
        {
            var shown = c != null && c.Shown;
            clockBars[i].style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
            if (!shown) return;
            var left = Mathf.Clamp01(c.Left);
            clockFills[i].style.width = Length.Percent(left * 100);
            // In the player's colour; amber and red (USS) when little is left.
            if (left < 0.25f) clockFills[i].style.backgroundColor = StyleKeyword.Null;
            else clockFills[i].style.backgroundColor = Ui.Hex(c.Color, new Color32(0xf2, 0xc2, 0x30, 0xff));
            clockBars[i].EnableInClassList("turn-clock--running", c.Running);
            clockBars[i].EnableInClassList("turn-clock--low", left < 0.25f);
            clockBars[i].EnableInClassList("turn-clock--out", left < 0.1f);
        }
    }
}
