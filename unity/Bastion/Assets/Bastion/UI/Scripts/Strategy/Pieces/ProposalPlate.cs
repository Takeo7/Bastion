using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The plate under the top strip (screen.ts renderProposal and renderWaiting, mixta-base.css .strat-proposal): a
    /// proposal waiting for both keys (whose, what, what it costs, its detail, the clock draining, Ver, Aceptar and
    /// Rechazar, or Retirar for one's own), or «Te esperan» when the partner is ready to go. The clock is the server's;
    /// `Tick` shows how much is left.
    /// </summary>
    public sealed class ProposalPlate
    {
        readonly VisualElement host;
        readonly IStrategyHandlers handlers;
        Label clockText;
        VisualElement clockFill;
        string key;

        public ProposalPlate(VisualElement host, IStrategyHandlers handlers)
        {
            this.host = host;
            this.handlers = handlers;
        }

        /// <summary>The proposal; with none, «Te esperan» if `waiting` has text; else nothing.</summary>
        public void Set(ProposalView p, WaitingView waiting)
        {
            var k = Ui.Key(p) + "|" + Ui.Key(waiting);
            if (k == key) return;
            key = k;
            host.Clear();
            clockText = null;
            clockFill = null;
            var show = p != null || !string.IsNullOrEmpty(waiting?.Text);
            Ui.Show(host, show);
            if (!show) return;
            host.EnableInClassList("strat-proposal--mine", p != null && p.Mine);
            if (p == null)
            {
                Ui.Text("TE ESPERAN", "strat-proposal__kicker font-title", host);
                Ui.Text(waiting.Text, "strat-proposal__text", host);
                var go = new Button(handlers.OnWaitingGo) { text = waiting.Button.ToUpperInvariant() };
                go.AddToClassList("btn-primary");
                go.AddToClassList("font-title");
                Ui.El("strat-proposal__actions", host).Add(go);
                return;
            }
            Ui.Text((p.Mine ? StrategyText.Proposal.MineKicker : StrategyText.Proposal.Kicker).ToUpperInvariant(), "strat-proposal__kicker font-title", host);
            var color = string.IsNullOrEmpty(p.PartnerColor) ? "#a678ff" : p.PartnerColor;
            Ui.Text(p.Mine ? StrategyText.Proposal.Mine(p.Text, p.Partner) : StrategyText.Proposal.Theirs(p.Partner, color, p.Text), "strat-proposal__text", host);
            if (!string.IsNullOrEmpty(p.Cost))
                Ui.Text(p.Mine ? StrategyText.Proposal.CostMine(p.Cost) : StrategyText.Proposal.CostTheirs(p.Cost), "strat-proposal__cost", host);
            if (p.Rows.Count > 0)
            {
                var detail = Ui.El("strat-proposal__detail well", host);
                foreach (var r in p.Rows)
                {
                    var row = Ui.El("strat-detail-row", detail);
                    Ui.Text(r.Key.ToUpperInvariant(), "strat-detail-row__key font-title", row);
                    Ui.Text(r.Value, "strat-detail-row__value", row);
                }
            }
            var clock = Ui.El("strat-proposal__clock", host);
            clockText = Ui.Text("", "strat-proposal__clock-text", clock);
            var track = Ui.El("strat-proposal__track well", clock);
            clockFill = Ui.El("strat-proposal__fill", track);
            var actions = Ui.El("strat-proposal__actions", host);
            if (p.Mine)
            {
                actions.Add(new Button(handlers.OnWithdraw) { text = StrategyText.Proposal.Withdraw.ToUpperInvariant() });
            }
            else
            {
                if (p.CanSee) actions.Add(new Button(handlers.OnSeeProposal) { text = StrategyText.Proposal.See.ToUpperInvariant() });
                var yes = new Button(() => handlers.OnVote(true)) { text = StrategyText.Proposal.Accept.ToUpperInvariant() };
                yes.AddToClassList("btn-primary");
                actions.Add(yes);
                var no = new Button(() => handlers.OnVote(false)) { text = StrategyText.Proposal.Reject.ToUpperInvariant() };
                no.AddToClassList("strat-btn-bad");
                actions.Add(no);
            }
            foreach (var b in actions.Children()) b.AddToClassList("font-title");
        }

        /// <summary>The clock (ui/deadline.ts): its words (proposalClock), how much is left (0–1), hurrying at 10 s or less.</summary>
        public void Tick(string text, float left, bool hurry)
        {
            if (clockText == null) return;
            Ui.Show(clockText.parent, !string.IsNullOrEmpty(text));
            clockText.text = text ?? "";
            clockFill.style.width = Length.Percent(Mathf.Clamp01(left) * 100);
            clockText.parent.EnableInClassList("strat-proposal__clock--hurry", hurry);
        }
    }
}
