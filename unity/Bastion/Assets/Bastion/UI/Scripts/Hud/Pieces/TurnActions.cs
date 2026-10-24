using Bastion.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// Bottom right (docs/UNITY-UI.md §2.6): «Terminar turno» in its six states (hud.ts setEndTurn); over it the turn
    /// clocks (tickClocks, every frame), «Ceder el mando» or «Ver a Ana» (setCommand), «Ir a Mateo» (setGoTo) and the
    /// camera switch, kept on this computer.
    /// </summary>
    public sealed class TurnActions
    {
        const string FollowKey = "bastion.followTurn";

        readonly VisualElement host;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        readonly VisualElement endCheck;
        readonly Button end;
        readonly Label endLabel;
        readonly Label endDetail;
        readonly Button pass;
        readonly Button watch;
        readonly Button go;
        readonly Button follow;
        readonly VisualElement clocks;
        readonly VisualElement[] clockBars = new VisualElement[2];
        readonly VisualElement[] clockFills = new VisualElement[2];
        string endKey;
        bool followOn;

        public TurnActions(VisualElement host, IHudHandlers handlers, TooltipLayer tips, System.Func<string, Sprite> icon = null)
        {
            this.host = host;
            this.handlers = handlers;
            this.tips = tips;
            Ui.CloneInto("TurnActions", host);
            end = host.Q<Button>("end");
            endLabel = host.Q<Label>("end-label");
            endDetail = host.Q<Label>("end-detail");
            endCheck = host.Q("end-check");
            var check = icon?.Invoke("ui:check");
            if (check != null) endCheck.style.backgroundImage = new StyleBackground(check);
            pass = host.Q<Button>("pass");
            watch = host.Q<Button>("watch");
            go = host.Q<Button>("go");
            follow = host.Q<Button>("follow");
            clocks = host.Q("clocks");
            clockBars[0] = host.Q("clock-mine");
            clockBars[1] = host.Q("clock-theirs");
            clockFills[0] = host.Q("clock-mine-fill");
            clockFills[1] = host.Q("clock-theirs-fill");

            end.clicked += () => handlers.OnEndTurn();
            pass.clicked += () => handlers.OnPassCommand();
            watch.clicked += () => handlers.OnWatchPartner();
            go.clicked += () => handlers.OnGoToSoldier();
            follow.clicked += () =>
            {
                followOn = !followOn;
                PlayerPrefs.SetInt(FollowKey, followOn ? 1 : 0);
                DrawFollow();
                handlers.OnFollowTurn(followOn);
            };
            host.Q<Label>("pass-label").text = HudText.Command.Pass;
            tips.Attach(pass, HudText.Command.PassTip);
            tips.Attach(watch, HudText.Command.WatchTip);
            tips.Attach(go, HudText.Command.GoTip);
            host.Q<Label>("follow-label").text = Lore.SettingsText.CameraFollow;
            followOn = PlayerPrefs.GetInt(FollowKey, 1) != 0;
            DrawFollow();
            Ui.Show(pass, false);
            Ui.Show(watch, false);
            Ui.Show(go, false);
            Ui.Show(clocks, false);
        }

        /// <summary>Whether the camera goes to the soldier whose turn it is (game.ts turnCamera).</summary>
        public bool FollowsTurn => followOn;

        public void SetEndTurn(EndTurnView v)
        {
            var k = Ui.Key(v);
            if (k == endKey) return;
            endKey = k;
            var mode = v?.Mode ?? "disabled";
            foreach (var m in new[] { "ready", "waiting", "confirm", "enemy" })
                end.EnableInClassList("end-turn--" + m, mode == m);
            end.SetEnabled(mode != "disabled" && mode != "waiting" && mode != "enemy");
            var detail = v?.Detail;
            Ui.Show(endCheck, mode == "ready");
            switch (mode)
            {
                case "confirm":
                    endLabel.text = HudText.EndTurn.Confirm;
                    endDetail.text = string.IsNullOrEmpty(detail) ? HudText.EndTurn.ConfirmAgain : detail;
                    break;
                case "ready":
                    endLabel.text = HudText.EndTurn.Ready;
                    endDetail.text = string.IsNullOrEmpty(detail) ? HudText.EndTurn.ReadyCancel : HudText.EndTurn.Waiting(detail);
                    break;
                case "waiting":
                    endLabel.text = HudText.EndTurn.Command(detail ?? "");
                    endDetail.text = HudText.EndTurn.Plan;
                    break;
                case "enemy":
                    // The enemy turn plays (docs/UX-RITMO.md): the space bar held shows it faster.
                    endLabel.text = HudText.EndTurn.EnemyTurn;
                    endDetail.text = HudText.EndTurn.EnemyFaster;
                    break;
                default:
                    endLabel.text = HudText.EndTurn.Act;
                    endDetail.text = HudText.EndTurn.ActKey;
                    break;
            }
        }

        /// <summary>«Ceder el mando» only for its holder; «Ver a Ana» for the other one; nothing alone.</summary>
        public void SetCommand(CommandView v)
        {
            var shared = v != null && v.Shared;
            Ui.Show(pass, shared && v.Mine);
            pass.SetEnabled(shared && v.CanPass);
            Ui.Show(watch, shared && !v.Mine);
            host.Q<Label>("watch-label").text = shared && !v.Mine ? HudText.Command.Watch(v.Holder) : "";
        }

        /// <summary>«Ir a Mateo» while this player gives the orders and someone still has actions; null hides it.</summary>
        public void SetGoTo(string name)
        {
            Ui.Show(go, !string.IsNullOrEmpty(name));
            host.Q<Label>("go-label").text = string.IsNullOrEmpty(name) ? "" : HudText.Command.GoTo(name);
        }

        /// <summary>
        /// The clocks, every frame (game.ts frame): this player's [0] and the partner's [1], as the bars' width; amber
        /// when little is left, red at the end; gone outside the squad's turn. Only styles change: no layout is rebuilt.
        /// </summary>
        public void TickClocks(ClockView mine, ClockView theirs)
        {
            var any = Tick(0, mine);
            any |= Tick(1, theirs);
            Ui.Show(clocks, any);
        }

        bool Tick(int i, ClockView c)
        {
            var shown = c != null && c.Shown;
            Ui.Show(clockBars[i], shown);
            if (!shown) return false;
            var left = Mathf.Clamp01(c.Left);
            clockFills[i].style.width = Length.Percent(left * 100);
            // In the player's colour; amber and red (USS) when little is left.
            if (left < 0.25f) clockFills[i].style.backgroundColor = StyleKeyword.Null;
            else clockFills[i].style.backgroundColor = Ui.Hex(c.Color, new Color32(0xf2, 0xc2, 0x30, 0xff));
            clockBars[i].EnableInClassList("turn-clock--running", c.Running);
            clockBars[i].EnableInClassList("turn-clock--low", left < 0.25f);
            clockBars[i].EnableInClassList("turn-clock--out", left < 0.1f);
            return true;
        }

        void DrawFollow()
        {
            follow.EnableInClassList("follow-turn--on", followOn);
            // What it does, and how it is now (hud.ts drawFollow).
            tips.Attach(follow, Lore.SettingsText.CameraFollowAbout + (followOn ? HudText.Command.On : HudText.Command.Off));
        }
    }
}
