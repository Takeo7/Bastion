using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The cards in the middle of the screen (docs/UNITY-UI.md §2.10, §2.11): the partner's call waiting for an answer
    /// (to evacuate, to abandon) with its time left; the arrival card, closed only by «Entendido», Intro or Space, which
    /// shrinks into the objective's card; and the mission's end. While the call or the arrival is up, the turn clock
    /// waits (Covering).
    /// </summary>
    public sealed class Cards
    {
        readonly VisualElement host;
        readonly IHudHandlers handlers;
        readonly Action<string> sound;
        VisualElement call;
        string callKey;
        IVisualElementScheduledItem callClock;
        VisualElement intro;
        IntroView introView;
        VisualElement outcome;

        public Cards(VisualElement host, IHudHandlers handlers, Action<string> sound)
        {
            this.host = host;
            this.handlers = handlers;
            this.sound = sound;
        }

        /// <summary>Something the game put up covers the field to be read: the clock doesn't run meanwhile.</summary>
        public bool Covering => call != null || intro != null;

        /// <summary>Fired when the arrival card closes, to show the objective's card again.</summary>
        public event Action IntroClosed;

        // ------------------------------------------------------------- call

        public void SetCall(CallView v)
        {
            var k = Ui.Key(v);
            if (k == callKey) return;
            callKey = k;
            callClock?.Pause();
            call?.RemoveFromHierarchy();
            call = null;
            if (v == null) return;
            call = Ui.El("card panel card--call", host);
            // Kicker and title in capitals, as the web's .call-kicker and .call-title.
            Ui.Text((v.Kicker ?? "").ToUpperInvariant(), "card__kicker", call);
            Ui.Text((v.Title ?? "").ToUpperInvariant(), "card__title font-title", call);
            Ui.Text(v.Text, "card__text", call);
            var clock = Ui.Text("", "card__clock", call);
            Ui.Text(v.ClockNote, "card__text muted", call);
            var actions = Ui.El("card__actions", call);
            var refuse = new Button(() => handlers.OnCallAnswer(false)) { text = v.Refuse };
            var accept = new Button(() => handlers.OnCallAnswer(true)) { text = v.Accept };
            accept.AddToClassList("btn-primary");
            actions.Add(refuse);
            actions.Add(accept);
            PlateFrame.Dress(call);
            Slant.Dress(accept);
            // The time left, counted down on this computer's clock from when it came (the server's deadline).
            Ui.Show(clock, v.Ms >= 0);
            if (v.Ms < 0) return;
            var ends = Time.realtimeSinceStartup + v.Ms / 1000f;
            void Tick()
            {
                var left = Mathf.Max(0, Mathf.CeilToInt(ends - Time.realtimeSinceStartup));
                clock.text = $"{left / 60}:{left % 60:00}";
            }
            Tick();
            callClock = call.schedule.Execute(Tick).Every(250);
        }

        // ------------------------------------------------------------ intro

        /// <summary>The arrival card; Close (its button, Intro, Space) shrinks it into the objective's card.</summary>
        public void ShowIntro(IntroView v)
        {
            CloseIntro(true);
            introView = v;
            if (v == null) return;
            intro = Ui.El("card panel card--intro", host);
            if (!string.IsNullOrEmpty(v.Kicker)) Ui.Text(v.Kicker, "card__kicker", intro);
            Ui.Text((v.Title ?? "").ToUpperInvariant(), "card__title font-title", intro);
            if (!string.IsNullOrEmpty(v.Objective)) Ui.Text(HudText.Mission.IntroObjective + v.Objective, "card__objective", intro);
            if (!string.IsNullOrEmpty(v.Turns)) Ui.Text(v.Turns.ToUpperInvariant(), "card__turns", intro);
            // Lore's marks, as hud.ts marked(briefing): the key words in white bold, the objective in the accent.
            if (!string.IsNullOrEmpty(v.Briefing)) Ui.Text(Strategy.StrategyText.Marked(v.Briefing), "card__text", intro);
            var actions = Ui.El("card__actions", intro);
            var ok = new Button(() => CloseIntro(false)) { text = HudText.Mission.IntroClose };
            ok.AddToClassList("btn-primary");
            actions.Add(ok);
            PlateFrame.Dress(intro);
            Slant.Dress(ok);
            sound?.Invoke("paper");
        }

        /// <summary>The arrival card up again, from the objective's card (Ana, 20/10 #32).</summary>
        public void ReopenIntro()
        {
            if (intro == null && introView != null) ShowIntro(introView);
        }

        /// <summary>Closed by its button, Intro or Space; `quiet`, without its sound nor the handler (a new one replaces it).</summary>
        public bool CloseIntro(bool quiet)
        {
            if (intro == null) return false;
            var leaving = intro;
            intro = null;
            leaving.AddToClassList("card--leaving");
            leaving.schedule.Execute(() => leaving.RemoveFromHierarchy()).StartingIn(400);
            if (quiet) return true;
            sound?.Invoke("confirm");
            IntroClosed?.Invoke();
            handlers.OnIntroClosed();
            return true;
        }

        // ---------------------------------------------------------- outcome

        public void SetOutcome(OutcomeView v)
        {
            outcome?.RemoveFromHierarchy();
            outcome = null;
            if (v == null) return;
            sound?.Invoke("stamp");
            outcome = Ui.El("card panel outcome " + (v.Victory ? "outcome--victory" : "outcome--defeat"), host);
            Ui.Text(v.Victory ? HudText.Mission.Victory : HudText.Mission.Defeat, "outcome__title font-title", outcome);
            if (!string.IsNullOrEmpty(v.Line)) Ui.Text(v.Line, "card__text outcome__line", outcome);
            var actions = Ui.El("card__actions", outcome);
            PlateFrame.Dress(outcome);
            if (v.Campaign)
            {
                var back = new Button(() => handlers.OnReturnToBase()) { text = HudText.Mission.ToBase };
                back.AddToClassList("btn-primary");
                actions.Add(back);
                Slant.Dress(back);
                return;
            }
            var again = new Button(() => handlers.OnNewMission(false)) { text = v.Siege ? HudText.Mission.AgainSiege : HudText.Mission.Again };
            again.AddToClassList("btn-primary");
            actions.Add(again);
            Slant.Dress(again);
            actions.Add(new Button(() => handlers.OnNewMission(true)) { text = HudText.Mission.Random });
            actions.Add(new Button(() => handlers.OnBackToMenu()) { text = HudText.Mission.ToMenu });
        }
    }
}
