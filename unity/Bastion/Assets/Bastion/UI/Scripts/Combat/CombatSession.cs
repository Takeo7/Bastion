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

        /// <summary>The presence last sent (the picked one and the pointer's tile), and when, so it goes at most a few times a second.</summary>
        string presence;
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
            control.Overlay = marks.AddComponent<MoveOverlay>();
            control.AimMarks = marks.AddComponent<AimOverlay>();
            control.Marks = marks.AddComponent<MissionMarks>();
            control.Hud = Hud;
            control.MySlot = Math.Max(0, net.Slot);
            control.DeadlineLeft = DeadlineLeft;
            control.Order += cmd => net.Send(cmd);
            control.Message += msg => net.Send(msg);
            rig.Held = () => Hud != null && Hud.MenuOpen;
            rig.OverHud = at => Hud != null && Hud.Over(at);
            if (Hud != null)
            {
                Hud.MenuChose += control.MenuChose;
                Hud.SettingChanged += control.SettingChanged;
            }
            player = new EventPlayer(control, destroyCancellationToken);
            control.SeeMoment = player.SeeMoment;
            control.Busy = player.IsBusy;
            control.EnemyTurnPlaying = () => player.EnemyTurnPlaying;
            control.Hurry = player.SetHurry;
            if (Hud != null) Hud.ClockOf = ClockOf;
            if (net.LastRoom != null) control.Players = net.LastRoom.Players;
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
            if (net != null) net.Message -= OnMessage;
            if (Current == control?.State) Current = null;
        }

        void OnMessage(ServerMessage m)
        {
            switch (m)
            {
                case RoomMessage room:
                    control.Players = room.Players;
                    if (control.State != null) control.Refresh();
                    break;
                case SnapshotMessage snap when snap.State != null:
                    Load(snap.State);
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

        /// <summary>A mission's state whole: the controller on it, the map and the figures built from it, and a fresh mission's arrival card.</summary>
        void Load(GameState state)
        {
            var first = control.State == null;
            control.State = state;
            Current = state;
            Bastion.Client.Battlefield.Current?.Show(state);
            player.Load(state);
            Snapshot?.Invoke(state);
            control.Refresh();
            if (first) control.OpeningShot();
            // A fresh mission opens with its briefing (main.ts: seq 0).
            if (state.Seq == 0) control.Intro();
        }

        /// <summary>
        /// The host's events (game.ts onEvents): on this player's state at once, then to the event player. A gap in them is
        /// NetLink's to mend (ServerFeed asks for the whole state and drops the events till it comes): here only a guard.
        /// </summary>
        void Play(EventsMessage m)
        {
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
            var key = $"{control.PickedId}|{tile?.X},{tile?.Y},{tile?.Z}";
            if (key != presence && Time.realtimeSinceStartup - presenceAt > 0.15f)
            {
                presence = key;
                presenceAt = Time.realtimeSinceStartup;
                net.Send(new PresenceMessage { Unit = control.PickedId, Tile = tile });
            }
            // The turn clock runs only while nothing covers the field and every event has been seen (game.ts reportIdle: Arturo, 22/10).
            var idle = (Hud == null || !Hud.Covering) && !player.Busy;
            if (idle != idleSent)
            {
                idleSent = idle;
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
        /// turn's time left and whether it runs; none outside the squad's turn.
        /// </summary>
        ClockView ClockOf(int which)
        {
            if (control == null) return null;
            var slot = which == 0 ? control.MySlot : 1 - control.MySlot;
            if (clocks == null || slot >= clocks.Count) return null;
            var c = clocks[slot];
            if (c == null || c.Total <= 0) return null;
            var ms = c.Running ? Math.Max(0, c.Left - (Time.realtimeSinceStartup - heard) * 1000) : c.Left;
            var hex = control.Players.FirstOrDefault(p => p.Slot == slot)?.Color ?? Content.PlayerColors[slot];
            return new ClockView { Shown = true, Left = (float)(ms / c.Total), Running = c.Running, Color = hex };
        }
    }
}
