using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The mission over the network (main.ts's handling of the host's messages, and game.ts onEvents; UI's): in the scene
    /// «Partida», what the host says goes to the combat's controller (the room, the snapshot, the events, the partner's
    /// presence, the calls' deadlines, the turn clocks), and the controller's orders go to the host. It listens to
    /// NetLink's Message alone in this scene: what came while the scene was loading waits for it, in order.
    /// The events go on State at once (orders go by it) and to the event player, which plays them into View, the state
    /// seen. The map is built from each snapshot (Arte's Battlefield, which does nothing if the map is the same).
    /// </summary>
    public sealed class CombatSession : MonoBehaviour
    {
        /// <summary>The HUD over the scene (AppFlow mounts it).</summary>
        public HudRoot Hud;

        /// <summary>A mission's state came whole (its start, a return, a resync): the map to build from it (Arte's).</summary>
        public static event Action<GameState> Snapshot;

        /// <summary>The latest state, for whoever builds from it after the snapshot (Arte's map, the fog).</summary>
        public static GameState Current { get; private set; }

        NetLink net;
        CombatController control;
        EventPlayer player;

        /// <summary>The calls' deadlines by kind (evac, abandon): when they fall due and how long they were, on this machine's clock.</summary>
        readonly Dictionary<string, (float at, float total)> deadlines = new Dictionary<string, (float, float)>();

        /// <summary>The turn clocks by slot, and when they were heard (turnClock.ts).</summary>
        List<TurnClock> clocks;
        float heard;

        /// <summary>The campaign as it was when the squad took off (the first `campaign` of the mission), for the base after it.</summary>
        CampaignState tookOff;

        /// <summary>
        /// The presence last sent (the picked one and the pointer's tile), and when, so it goes at most a few times a second.
        /// Kept as they are, not as a text: one made every frame to compare was garbage every frame (review of 10/11).
        /// </summary>
        string presenceUnit;
        (int x, int y, int z)? presenceTile;
        bool presenceSent;
        float presenceAt;
        bool? idleSent;

        void Start()
        {
            net = NetLink.Instance;
            var cam = Camera.main;
            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
            var marks = new GameObject("Marcas");
            control = new GameObject("Combate").AddComponent<CombatController>();
            control.Rig = rig;
            control.Sfx.Attach(cam, () => rig.Target, Board.Tile, (s, p) => Board.TileToWorld(s, p, Board.Tile));
            control.Overlay = marks.AddComponent<MoveOverlay>();
            control.AimMarks = marks.AddComponent<AimOverlay>();
            control.Marks = marks.AddComponent<MissionMarks>();
            control.Hud = Hud;
            control.MySlot = Math.Max(0, net.Slot);
            control.DeadlineLeft = DeadlineLeft;
            control.Order += cmd => net.Send(cmd);
            control.Message += msg => net.Send(msg);
            // (The loading sheet holds them too: the opening shot isn't moved under it.)
            rig.Held = () => Hud != null && Hud.MenuOpen || App.AppFlow.WindowShown;
            rig.OverHud = at => Hud != null && Hud.Over(at);
            if (Hud != null)
            {
                Hud.MenuChose += control.MenuChose;
                Hud.SettingChanged += control.SettingChanged;
            }
            player = new EventPlayer(control, destroyCancellationToken);
            control.SeeMoment = player.SeeMoment;
            control.CloseFocus = () => player.CloseFocus;
            control.ActingFocus = () => player.Acting;
            control.Busy = player.IsBusy;
            control.Playing = () => player.Busy;
            control.EnemyTurnPlaying = () => player.EnemyTurnPlaying;
            control.Hurry = player.SetHurry;
            control.Halt = player.SetHalted;
            if (Hud != null) Hud.ClockOf = ClockOf;
            if (net.LastRoom != null) control.SetPlayers(net.LastRoom.Players);
            // The campaign as the squad took off, for the soldiers' boards on their sheets.
            control.Campaign = () => tookOff;
            // Tests with two apps (BASTION_PASOS_COMBATE): the HUD pressed as a player would.
            CombatRun.FromEnvironment(control);
            net.Message += OnMessage;
        }

        /// <summary>The window loses the keyboard: the space bar can't be let go any more, so the chosen speed is back.</summary>
        void OnApplicationFocus(bool focus)
        {
            if (!focus) player?.SetHurry(false);
        }

        void OnDestroy()
        {
            Time.timeScale = 1;
            control?.Sfx.Siren(null);
            if (net != null) net.Message -= OnMessage;
            if (Current == control?.State) Current = null;
        }

        /// <summary>The squad's figures being made under the window (cuadro 136): what the host sends waits till they are.</summary>
        bool preparing;
        readonly List<ServerMessage> held = new List<ServerMessage>();

        void OnMessage(ServerMessage m)
        {
            if (preparing)
            {
                held.Add(m);
                return;
            }
            switch (m)
            {
                case RoomMessage room:
                    control.SetPlayers(room.Players);
                    // On the way back to the base (this player's «Volver a la base» or the partner's): the report says so.
                    if (room.Phase == RoomPhases.Base && room == net.LastRoom)
                    {
                        Hud?.Returning();
                        // For tests with the app (BASTION_FOTOS): the report saying so, as this frame ends.
                        var shots = Environment.GetEnvironmentVariable("BASTION_FOTOS");
                        if (!string.IsNullOrEmpty(shots)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, "volviendo.png"));
                    }
                    // Out of the mission (back to the base, or to the menu): what comes next is for the next scene, which
                    // gets it from NetLink in order (the base, its campaign), with the base as it took off.
                    if (room.Phase != RoomPhases.Mission && room == net.LastRoom)
                    {
                        net.Message -= OnMessage;
                        Bastion.UI.Strategy.BaseSession.TookOff = tookOff;
                    }
                    break;
                case CampaignMessage campaign when tookOff == null:
                    tookOff = campaign.State;
                    break;
                case SnapshotMessage snap when snap.State != null && control.State == null && Application.isPlaying:
                    _ = Prepared(snap.State);
                    break;
                case SnapshotMessage snap when snap.State != null:
                    _ = Load(snap.State, false);
                    break;
                case EventsMessage events:
                    Play(events);
                    break;
                case PartnerPresenceMessage p when p.Slot != control.MySlot:
                    control.PartnerPicked(p.Unit, Color(p.Slot));
                    control.Overlay?.ShowPartnerHover(control.State, p.Tile, Color(p.Slot));
                    break;
                case PartnerPlanMessage plan:
                    control.PartnerPlan(plan.Slot, plan.Unit, plan.Tile);
                    break;
                case PartnerPingMessage ping:
                    control.OnPing(ping.Slot, ping.Tile, ping.Kind);
                    break;
                case RejectedMessage rejected:
                    Hud?.Toast(rejected.Message);
                    control.TurnedDown();
                    break;
                case DeadlineMessage d:
                    if (d.Ms == null) deadlines.Remove(d.Kind);
                    else deadlines[d.Kind] = (Time.realtimeSinceStartup + d.Ms.Value / 1000f, Math.Max(1, d.Ms.Value / 1000f));
                    if (control.State != null) control.Refresh();
                    break;
                case TurnClockMessage t:
                    clocks = t.Clocks;
                    heard = Time.realtimeSinceStartup;
                    break;
                case ErrorMessage error:
                    Hud?.Toast(error.Message);
                    break;
            }
        }

        UnityEngine.Color Color(int slot)
        {
            var hex = control.Players.FirstOrDefault(p => p.Slot == slot)?.Color ?? Content.PlayerColors[slot];
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : UnityEngine.Color.white;
        }

        /// <summary>
        /// The first state (cuadro 136, with Arte's UnitViews.Prepare): the squad's figures made a step a frame under the
        /// loading window, its red ring traced as they come, then the load as ever; what came meanwhile after it, in order.
        /// </summary>
        async Awaitable Prepared(GameState state)
        {
            preparing = true;
            try
            {
                try
                {
                    // (The 0.14.2's review, point 5: what a failure here leaves; BASTION_PRUEBA_SALIDA=fallo, labs only.)
                    if (System.Environment.GetEnvironmentVariable("BASTION_PRUEBA_SALIDA") == "fallo") throw new InvalidOperationException("prueba: fallo al preparar");
                    Bastion.Client.Units.UnitViews.Prepare(state);
                    var all = Math.Max(1, Bastion.Client.Units.SoldierFigure.Waiting);
                    while (Bastion.Client.Units.UnitViews.Preparing)
                    {
                        App.AppFlow.Progress(1 - Bastion.Client.Units.SoldierFigure.Waiting / (float)all);
                        await Awaitable.NextFrameAsync(destroyCancellationToken);
                    }
                    App.AppFlow.Progress(1);
                    // A frame between the last soldier's last step and the load (Arte, 23/11: in the same frame, 0,2 s more).
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    // The figures not made ahead: the load makes them as it always did (the review's point 5).
                    Debug.LogException(e);
                }
                // (What the host sends keeps waiting through the spread load too: preparing goes after it.)
                await Load(state, true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                // Whatever the load met, what came meanwhile is played all the same: never held for good.
                Debug.LogException(e);
            }
            finally
            {
                preparing = false;
            }
            var later = held.ToList();
            held.Clear();
            foreach (var m in later) OnMessage(m);
        }

        /// <summary>
        /// The way back's window (cuadro 136): «Vuelta a la base», the stage's result as its end card says it (its title, or
        /// the mission's won or lost) and its line; null out of a battle.
        /// </summary>
        public static Bastion.UI.App.LoadView ReturnWindow()
        {
            var session = FindFirstObjectByType<CombatSession>();
            var end = session?.Hud?.Outcome;
            if (session == null || session.control?.State == null) return null;
            var title = end?.Title ?? (end == null ? null : end.Victory ? HudText.Mission.Victory : HudText.Mission.Defeat);
            var (_, stage) = session.control.VeilWords();
            return new Bastion.UI.App.LoadView
            {
                // Mid-expedition after a stage won, on to the map of stages (Lore: «Vuelta a la base» was false there).
                Kicker = end?.Back == HudText.Mission.RunGoOn ? Bastion.UI.App.LoadText.GoOn : Bastion.UI.App.LoadText.Back,
                Title = title ?? stage,
                Line = end?.Line,
                Place = title != null ? stage : null,
            };
        }

        /// <summary>A mission's state whole: the controller on it, the map and the figures built from it, and a fresh mission's arrival card.</summary>
        /// <remarks>
        /// `spread` (the first, under the loading window: cuadro 136): the map, the figures and the HUD each in a frame of its
        /// own (in one, 0,6 s in the editor: Arte's map, ShowSeen with the figures, the first Refresh); else all at once.
        /// </remarks>
        async Awaitable Load(GameState state, bool spread)
        {
            var first = control.State == null;
            // Each part of the first load timed, a line in the log (M2, cuadro 136: what of the load's frame is whose).
            var watch = System.Diagnostics.Stopwatch.StartNew();
            double Lap()
            {
                var ms = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                return ms;
            }
            control.State = state;
            Current = state;
            // A copy for the map: it keeps what it drew to tell the next change, and Play changes this state in place, so
            // with this very state the first change after a load (a boot searched, app 0.12.0) was already in it and drew nothing.
            var copy = Events.CloneState(state);
            var cloned = Lap();
            Bastion.Client.Battlefield.Current?.Show(copy);
            var shown = Lap();
            if (spread)
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                watch.Restart();
            }
            player.Load(state);
            var played = Lap();
            if (spread)
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                watch.Restart();
            }
            if (state.Outcome == null) control.Sfx.Start(state, !state.Concealed);
            control.Sfx.Siren(() => control.View);
            var sounds = Lap();
            Snapshot?.Invoke(state);
            control.Refresh();
            var refreshed = Lap();
            if (first) control.OpeningShot();
            if (first) Debug.Log(System.FormattableString.Invariant($"[carga] UI Load{(spread ? " (en tres fotogramas)" : "")}: copia {cloned:0} · mapa (Battlefield.Show) {shown:0} | EventPlayer.Load {played:0} | sonido {sounds:0} · Refresh {refreshed:0} · cámara {Lap():0} ms"));
            if (first)
            {
                var (kicker, title) = control.VeilWords();
                // A stage's name has its place already («Saqueo · Oficinas», the map's name too): no line of it again.
                App.AppFlow.VeilWords(kicker, title, state.Run != null ? null : state.MapName);
                _ = LiftVeil();
            }
            // A fresh mission opens with its briefing (main.ts: seq 0), once the veil is off (it waits for it: game.ts).
            if (state.Seq == 0) _ = IntroAfterVeil();
            // A mission already over (Red's N9, 17/11): whoever sits down after the end, or comes back having missed it,
            // gets the whole state with its end and never plays MissionEndedEvent, which puts the card up. Up from the
            // state, then; not again for whoever has it up. (Not by the state before: the events are on it as they come,
            // so an end heard and not yet played, which this Load drops from the queue, would leave no card.)
            if (state.Outcome != null && !player.OutcomeShown) player.ShowOutcome(state.Outcome, state.OutcomeReason);
        }

        /// <summary>
        /// The window goes once the stage is drawn with its map and figures (Arte's StageLoad.FirstDrawn, cuadro 136: never
        /// the battle half built nor a black frame); if that never comes (no figures), after FirstDrawnMost seconds.
        /// </summary>
        async Awaitable LiftVeil()
        {
            var raised = App.AppFlow.Raised;
            var asked = Time.realtimeSinceStartup;
            var drawn = false;
            void Drawn() => drawn = true;
            Bastion.Client.StageLoad.FirstDrawn += Drawn;
            var since = Time.realtimeSinceStartup;
            try
            {
                // (Drawn already, maybe, before this was asked: the event goes as its frame ends, a frame before the spread
                // load gets here; StageLoad.Drawn keeps it. The 0.14.2's review, point 1: 6 s always.)
                while (!drawn && !Bastion.Client.StageLoad.Drawn && Time.realtimeSinceStartup - since < FirstDrawnMost) await Awaitable.NextFrameAsync(destroyCancellationToken);
                drawn |= Bastion.Client.StageLoad.Drawn;
            }
            catch (OperationCanceledException)
            {
                // The scene went first: the next one's window takes over (and Perf's change isn't over by this: point 3).
                return;
            }
            finally
            {
                Bastion.Client.StageLoad.FirstDrawn -= Drawn;
            }
            // (Why and when it goes, for the strips: the review's point 1.)
            Debug.Log(System.FormattableString.Invariant($"[hoja] se quita: {(drawn ? "primer dibujo" : "tope de " + FirstDrawnMost + " s")}, a los {Time.realtimeSinceStartup - asked:0.00} s de pedirlo"));
            App.AppFlow.LiftVeil(raised);
        }

        const float FirstDrawnMost = 6;

        /// <summary>
        /// A fresh mission, once its sheet is going: the first turn's stamp, as any other turn's («TU TURNO», held till this
        /// player does something, or «TURNO DE ANA» to whoever doesn't hold the command: cuadro 139, no arrival card), and
        /// the log's notes (Intro). The camera stays on the opening shot, set under the sheet.
        /// </summary>
        async Awaitable IntroAfterVeil()
        {
            try
            {
                // (Gone whole, not only going: the stamp coming in under the fading sheet was half seen.)
                while (App.AppFlow.WindowShown) await Awaitable.NextFrameAsync(destroyCancellationToken);
                var s = control.State;
                if (s == null || s.Outcome != null) return;
                player.TurnStamp(s.ActiveTeam, s.Command, s.Concealed);
                control.Intro();
            }
            catch (OperationCanceledException)
            {
                // The scene went first.
            }
        }

        /// <summary>
        /// The host's events (game.ts onEvents): on this player's state at once, then to the event player. A gap in them is
        /// NetLink's to mend (ServerFeed asks for the whole state and drops the events till it comes): here only a guard.
        /// </summary>
        static readonly Unity.Profiling.ProfilerMarker MPlay = new Unity.Profiling.ProfilerMarker("Bastion.UI.Session.Play");

        void Play(EventsMessage m)
        {
            using var mark = MPlay.Auto();
            var s = control.State;
            if (s == null) return;
            foreach (var e in m.Events) Events.ApplyEvent(s, e);
            if (s.Seq != m.Seq) Debug.LogError($"[sync] Desfase de eventos tras aplicar un lote seguido ({s.Seq} ≠ {m.Seq})");
            control.Played(m.Events, m.By);
            player.Push(m.Events, m.By);
            control.Refresh();
        }

        void Update()
        {
            if (control == null || control.State == null) return;
            // What this player looks at, for the partner (game.ts sendPresence): the picked one and the pointer's tile.
            var tile = control.Hovered;
            var picked = control.PickedId;
            var at = tile != null ? (tile.X, tile.Y, tile.Z) : ((int, int, int)?)null;
            if ((!presenceSent || picked != presenceUnit || at != presenceTile) && Time.realtimeSinceStartup - presenceAt > 0.15f)
            {
                presenceSent = true;
                presenceUnit = picked;
                presenceTile = at;
                presenceAt = Time.realtimeSinceStartup;
                net.Send(new PresenceMessage { Unit = control.PickedId, Tile = tile });
            }
            // The turn clock runs only while nothing covers the field and every event has been seen (game.ts reportIdle: Arturo, 22/10).
            // (Nor while the loading sheet is up: the review's point 2, the clock ran under it.)
            var idle = (Hud == null || !Hud.Covering) && !player.Busy && !App.AppFlow.WindowShown;
            if (idle != idleSent)
            {
                idleSent = idle;
                // (When the turn clock may run, and whether the sheet is still up: the review's point 2.)
                Debug.Log(System.FormattableString.Invariant($"[reloj] libre {(idle ? "sí" : "no")}, con la hoja {(App.AppFlow.WindowShown ? "puesta" : "fuera")}, a los {Time.realtimeSinceStartup:0.00} s"));
                net.Send(new IdleMessage { Idle = idle });
            }
        }

        /// <summary>What is left of a call (evac, abandon), «1:25», or null when nothing waits (deadline.ts deadlineLeft).</summary>
        string DeadlineLeft(string kind)
        {
            if (!deadlines.TryGetValue(kind, out var d)) return null;
            var left = Math.Max(0, Mathf.CeilToInt(d.at - Time.realtimeSinceStartup));
            return $"{left / 60}:{left % 60:00}";
        }

        /// <summary>
        /// The turn clocks for the HUD (turnClock.ts turnClockOf): this player's (0) and the partner's (1), the share of the
        /// turn's time left and whether it runs; none outside the squad's turn, and the partner's only with a partner in the
        /// game (hud.ts setPlayers' clockSlots: playing alone, no thin bar under this player's).
        /// </summary>
        ClockView ClockOf(int which)
        {
            if (control == null) return null;
            var slot = which == 0 ? control.MySlot : 1 - control.MySlot;
            if (which == 1 && !control.Players.Any(p => p.Slot == slot)) return null;
            if (clocks == null || slot >= clocks.Count) return null;
            var c = clocks[slot];
            if (c == null || c.Total <= 0) return null;
            var ms = c.Running ? Math.Max(0, c.Left - (Time.realtimeSinceStartup - heard) * 1000) : c.Left;
            var hex = control.Players.FirstOrDefault(p => p.Slot == slot)?.Color ?? Content.PlayerColors[slot];
            return new ClockView { Shown = true, Left = (float)(ms / c.Total), Running = c.Running, Color = hex };
        }
    }
}
