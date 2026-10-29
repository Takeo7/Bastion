using System;
using System.Linq;
using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base over the network (main.ts's handling of the host's messages on the base; UI's), as CombatSession is the
    /// mission's: AppFlow mounts it with the base's screens while the room's phase is `base`. What the host says (the
    /// room, the campaign, the partner's tab, the proposal's deadline, an order turned down) goes to the controller, and
    /// the controller's messages to the host. It listens to NetLink's Message only while it is up, so what comes after it
    /// goes waits for the battle's scene, its snapshot first.
    /// </summary>
    [RequireComponent(typeof(StrategyRoot))]
    public sealed class BaseSession : MonoBehaviour
    {
        /// <summary>The campaign as the squad took off on the last mission (CombatSession), for the windows of what it changed.</summary>
        public static CampaignState TookOff;

        NetLink net;
        BaseController control;
        /// <summary>The tabs' tour (BASTION_PESTANAS) goes once a run: the first time on the base.</summary>
        static bool toured;
        /// <summary>Times on the base this run: BASTION_RECORRIDO_BASE plays on the first, BASTION_RECORRIDO_BASE2 on the second.</summary>
        static int visits;

        public BaseController Control => control;

        void Start()
        {
            // The battle's time running whatever it was left at (a pause, «Rápida»): the camp's life runs on it.
            Time.timeScale = 1;
            net = NetLink.Instance;
            var root = GetComponent<StrategyRoot>();
            control = new BaseController(root, Math.Max(0, net.Slot), msg => net.Send(msg))
            {
                Quit = App.AppFlow.Quit,
                Hosting = () => net.Hosting,
            };
            control.Before = TookOff;
            TookOff = null;
            if (net.LastRoom != null) control.Players(net.LastRoom.Players);
            var shots = Environment.GetEnvironmentVariable("BASTION_FOTOS");
            if (!toured && Environment.GetEnvironmentVariable("BASTION_PESTANAS") == "1" && !string.IsNullOrEmpty(shots))
            {
                toured = true;
                _ = TabTour(shots);
            }
            // Tests with the app (TestSteps): this visit's steps, BaseRun's on the base («tab:geoscape», «take:sandbags»…)
            // and «unwalk» to stop walking.
            var tour = Environment.GetEnvironmentVariable(visits == 0 ? "BASTION_RECORRIDO_BASE" : "BASTION_RECORRIDO_BASE2");
            visits++;
            if (!string.IsNullOrEmpty(tour)) _ = TestSteps.Run(tour, step =>
            {
                if (step == "unwalk") Bastion.Client.Settlement.Instance?.WalkAbout(false);
                else BaseRun.Do(control, step);
                return true;
            }, destroyCancellationToken);
            // Walking about the clearing (Arte's): the panels off, how to walk at the bottom.
            walkingOf = Bastion.Client.Settlement.Instance;
            if (walkingOf != null) walkingOf.WalkingChanged += root.Walking;
            // Last: what NetLink held for the base comes in as it is listened to, and what it throws ends Start there.
            net.Message += OnMessage;
        }

        /// <summary>The clearing whose walk the screens follow.</summary>
        Bastion.Client.Settlement walkingOf;

        /// <summary>
        /// For tests with the app (BASTION_PESTANAS=1 with Red's BASTION_FOTOS): the first time on the base, once the
        /// premise is closed (15 s at most), each tab in turn as a player would open it, its screen saved two seconds in
        /// (pestana-1-geoscape.png…); then a proposal and its vote: the host proposes another mission than the one chosen
        /// (propuesta-1.png on each side, as it waits; Red's run says yes on the partner's), and back to Soldados once it
        /// is answered (propuesta-2.png). Set BASTION_LANZAR past it (≈35 s), so the squad doesn't leave meanwhile.
        /// </summary>
        async Awaitable TabTour(string folder)
        {
            var ct = destroyCancellationToken;
            var root = GetComponent<StrategyRoot>();
            try
            {
                while (control?.State == null) await Awaitable.WaitForSecondsAsync(0.25f, ct);
                for (var waited = 0f; root.ModalOpen && waited < 15; waited += 0.25f) await Awaitable.WaitForSecondsAsync(0.25f, ct);
                await Awaitable.WaitForSecondsAsync(1.5f, ct);
                var tabs = new[] { "geoscape", "engineering", "barracks", "facilities" };
                for (var i = 0; i < tabs.Length; i++)
                {
                    Step($"pestaña {tabs[i]}", () => control.OnTab(tabs[i]));
                    await Awaitable.WaitForSecondsAsync(2f, ct);
                    Shot(folder, $"pestana-{i + 1}-{tabs[i]}");
                    await Awaitable.WaitForSecondsAsync(0.5f, ct);
                }
                var c = control.State;
                var other = c.Offers.FirstOrDefault(o => o.Id != c.ActiveMission && o.Siege != true && !o.Final);
                if (net.Hosting && other != null)
                {
                    Step($"propuesta: {other.Name}", () =>
                    {
                        control.OnTab("geoscape");
                        control.OnOffer(other.Id);
                        control.OnMapAction("deploy");
                    });
                }
                for (var waited = 0f; control.State.Proposal == null && waited < 10; waited += 0.25f) await Awaitable.WaitForSecondsAsync(0.25f, ct);
                if (control.State.Proposal != null)
                {
                    await Awaitable.WaitForSecondsAsync(1.5f, ct);
                    Shot(folder, "propuesta-1");
                    for (var waited = 0f; control.State.Proposal != null && waited < 40; waited += 0.25f) await Awaitable.WaitForSecondsAsync(0.25f, ct);
                    await Awaitable.WaitForSecondsAsync(1f, ct);
                    Shot(folder, "propuesta-2");
                    // (The shot is taken as the frame ends: a tab opened in it would be in it.)
                    await Awaitable.WaitForSecondsAsync(0.5f, ct);
                }
                Step("pestaña barracks", () => control.OnTab("barracks"));
            }
            catch (OperationCanceledException)
            {
                // The base went (a mission) mid-tour.
            }
        }

        /// <summary>A step of the tour, logged; what it throws is logged too and the tour goes on.</summary>
        static void Step(string what, Action act)
        {
            Debug.Log($"[base] {what}");
            try
            {
                act();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static void Shot(string folder, string name)
        {
            var file = System.IO.Path.Combine(folder, name + ".png");
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log($"[base] foto {file}");
        }

        /// <summary>The longest the take-off holds the mission back, whatever happens to the drawing (screen.ts: 9 s for the depart).</summary>
        const float LeaveAtMost = 12;

        /// <summary>
        /// Take-off (screen.ts launch), before the mission loads (AppFlow waits for it): the panels go but the stamp, the
        /// squad gets into La Lata and it drives off (Arte's Settlement.Leave, ≈10 s with the bus); a siege, nobody leaves:
        /// «Ya están aquí» a moment. What the host sends meanwhile waits for the battle (this stopped listening).
        /// </summary>
        public async Awaitable Depart()
        {
            var root = GetComponent<StrategyRoot>();
            var c = control?.State;
            var siege = c != null && Campaign.ActiveOffer(c)?.Siege == true;
            root.Launching(siege ? Lore.BaseSiegeText.Here.TrimEnd('.') : StrategyText.Launch);
            var clearing = Bastion.Client.Settlement.Instance;
            var ct = destroyCancellationToken;
            try
            {
                if (siege || clearing == null)
                {
                    await Awaitable.WaitForSecondsAsync(1.6f, ct);
                    return;
                }
                var started = Time.realtimeSinceStartup;
                var going = clearing.Leave(ct);
                while (clearing.Leaving && Time.realtimeSinceStartup - started < LeaveAtMost) await Awaitable.NextFrameAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // The scene went first.
            }
        }

        void OnDestroy()
        {
            if (net != null) net.Message -= OnMessage;
            if (walkingOf != null) walkingOf.WalkingChanged -= GetComponent<StrategyRoot>().Walking;
        }

        void OnMessage(ServerMessage m)
        {
            switch (m)
            {
                case RoomMessage room:
                    control.Players(room.Players);
                    // Off the base (to a mission, or the menu): the rest is for the next scene (the mission's snapshot), in
                    // order. Only the room as it is now: an old one held for this screen says nothing.
                    if (room.Phase != RoomPhases.Base && room == net.LastRoom) net.Message -= OnMessage;
                    break;
                case CampaignMessage campaign:
                    control.Campaign(campaign.State);
                    break;
                case PartnerBaseViewMessage partner:
                    control.PartnerView(partner.Slot, partner.View);
                    break;
                case DeadlineMessage deadline:
                    control.Deadline(deadline.Kind, deadline.Ms);
                    break;
                case RejectedMessage rejected:
                    control.Note(rejected.Message);
                    break;
                case ErrorMessage error:
                    control.Note(error.Message);
                    break;
                case PartnerBaseCursorMessage cursor:
                    control.PartnerCursor(cursor.Slot, cursor.Piece, cursor.X, cursor.Y, cursor.Rot);
                    break;
            }
        }

        /// <summary>Esc for the base (the radio, the board, the menu); Intro and Espacio move the radio on.</summary>
        void Update()
        {
            var keys = Keyboard.current;
            var root = GetComponent<StrategyRoot>();
            if (control == null || !root.Ready) return;
            control.Tick();
            // F12: a capture of the screen (Red, 28/10).
            if (keys != null && keys.f12Key.wasPressedThisFrame) Bastion.UI.App.Screenshots.Take(root.Toast);
            // Not with Cmd or Ctrl held: the system's (Shortcuts).
            if (keys == null || Bastion.UI.App.Shortcuts.Elsewhere(keys)) return;
            // Walking about the clearing, Esc is the walk's (Arte's: it ends it in its LateUpdate, so Walking is still on here).
            var walking = Bastion.Client.Settlement.Instance != null && Bastion.Client.Settlement.Instance.Walking;
            if (keys.escapeKey.wasPressedThisFrame && !walking) control.Escape();
            else if (keys.enterKey.wasPressedThisFrame) root.RadioKey("enter");
            else if (keys.spaceKey.wasPressedThisFrame) root.RadioKey("space");
            // B: in and out of Construir (screen.ts onKey), not while a name is typed.
            else if (keys.bKey.wasPressedThisFrame && !walking && !root.Typing) control.ToggleBuild();
        }
    }
}
