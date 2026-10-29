using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;

namespace Bastion.Client.Net
{
    /// <summary>
    /// For measuring the frame's cost, alone, without clicking (BASTION_MEDIR=&lt;case&gt;, with BASTION_ANFITRION; never in a
    /// game of the players): the app hosts and plays the case while the telemetry writes its «[rend …]» lines to red.log,
    /// then quits. With BASTION_GRAFICOS (GameSettings) for the level and BASTION_SEMILLA (NetLink) for the room's dice: the
    /// same seed plays the same game at every level, to compare them. docs/PRUEBA-PC-3090.md runs them all.
    /// - ciudad, ciudad-noche, asedio: a Limpieza in the city by day, at night, or on El Bastión (the base as a battlefield);
    ///   the arrival card closed, MissionBot plays it, and another begins when one is over, for BASTION_SEGUNDOS (90);
    /// - base: a new campaign on its base, the premise closed, then the Asentamiento, Soldados and Mapa a third of it each
    ///   (120 s at least).
    /// Each step goes to red.log («[prueba] medir: …»), to tell its lines apart, and what the loads take: from the app's
    /// start to its first frame of the game, and from the case asked for (the mission, the campaign) to its first card
    /// showing («Entendido»), with the longest frame between. However it goes, the app quits BASTION_SEGUNDOS + 60 seconds in.
    /// </summary>
    internal sealed class Measure
    {
        private static readonly string[] Cases = { "ciudad", "ciudad-noche", "asedio", "base" };
        private static readonly string[] Tabs = { "facilities", "barracks", "geoscape" };

        private readonly string which;
        private readonly float seconds;
        private readonly Action<string> log;
        private readonly HeardGame heard = new HeardGame();
        private float started = -1;
        private float nextSend;
        private float nextPress;
        private int tab = -1;
        private float sentWhen;
        private int sentAt = -1;
        private bool screenIdle;
        private bool lastWasEnd;
        private bool done;
        private bool named;
        private bool booted;
        /// <summary>When the case's game was first asked for (-1, not yet): its load is timed from there to its first card.</summary>
        private float askedAt = -1;
        private float longest;
        private bool loaded;

        private Measure(string which, float seconds, Action<string> log)
        {
            this.which = which;
            // The base's three screens get 40 s each at least: three lines of telemetry past the warm-up.
            this.seconds = which == "base" ? Math.Max(seconds, 120) : seconds;
            this.log = log;
        }

        /// <summary>The case BASTION_MEDIR asks for, or null.</summary>
        public static Measure? FromEnvironment(Action<string> log)
        {
            var which = Environment.GetEnvironmentVariable("BASTION_MEDIR");
            if (string.IsNullOrEmpty(which)) return null;
            if (!Cases.Contains(which))
            {
                log($"[prueba] medir: «{which}» no es ningún caso ({string.Join(", ", Cases)})");
                return null;
            }
            var seconds = float.TryParse(Environment.GetEnvironmentVariable("BASTION_SEGUNDOS"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 90;
            return new Measure(which!, seconds, log);
        }

        /// <summary>With the sound's trace (BASTION_SONIDO_TRAZA=1): each batch of events to Unity's log, beside the sounds they made.</summary>
        private static readonly bool Tracing = Environment.GetEnvironmentVariable("BASTION_SONIDO_TRAZA") == "1";

        public void Observe(ServerMessage msg)
        {
            heard.Observe(msg);
            if (Tracing && msg is EventsMessage events && events.Events.Count > 0)
                Debug.Log($"[sonido] {Time.realtimeSinceStartup:F2} s · hechos: {string.Join(", ", events.Events.Select((e) => e.GetType().Name.Replace("Event", "")))}");
            if (msg is SnapshotMessage || msg is RejectedMessage) sentAt = -1;
        }

        public void Sent(ClientMessage msg)
        {
            if (msg is IdleMessage idle) screenIdle = idle.Idle;
        }

        public void Tick(NetLink link, float now)
        {
            if (done) return;
            if (!booted)
            {
                booted = true;
                log($"[prueba] medir: arranque, primer fotograma del juego a los {now:F1} s");
            }
            if (askedAt >= 0 && !loaded) Loading(now);
            // Whatever happens (a case that never begins, one stuck), the app goes in the end: the next case runs.
            if (now > seconds + 60)
            {
                log($"[prueba] medir: {which} sin terminar a los {seconds + 60} s: se cierra la app");
                done = true;
                Application.Quit();
                return;
            }
            var room = heard.Room ?? link.LastRoom;
            if (room == null || link.Slot < 0 || !link.Hosting) return;
            if (started >= 0 && now - started > seconds)
            {
                log($"[prueba] medir: {which} terminado, {seconds} s");
                done = true;
                Application.Quit();
                return;
            }
            var phase = room.Phase;
            if (which == "base")
            {
                Base(link, phase, now);
                return;
            }
            if (phase == RoomPhases.Lobby)
            {
                if (Due(now, 3)) Ask(link, Start(), now);
                return;
            }
            if (phase != RoomPhases.Mission) return;
            if (started < 0) started = now;
            if (!named && heard.Mission != null)
            {
                named = true;
                log($"[prueba] medir: {which} · {heard.Mission.MapName}");
            }
            // The arrival card, closed as a player would; one over, another.
            Press(now, "Entendido");
            var s = heard.Mission;
            if (s == null) return;
            if (s.Outcome != null)
            {
                if (Due(now, 4))
                {
                    log($"[prueba] medir: {s.MapName} {s.Outcome}; otra");
                    link.Send(new NewMissionMessage { Mission = MissionKinds.Elimination, Map = Map(), Night = which == "ciudad-noche" });
                }
                return;
            }
            Play(link, room, s, now);
        }

        private ClientMessage Start() => new StartMessage { Mission = MissionKinds.Elimination, Map = Map(), Night = which == "ciudad-noche" };

        private string Map() => which == "asedio" ? SkirmishMaps.Bastion : SkirmishMaps.City;

        private void Base(NetLink link, string phase, float now)
        {
            if (phase == RoomPhases.Lobby)
            {
                if (Due(now, 3)) Ask(link, new NewCampaignMessage(), now);
                return;
            }
            if (phase != RoomPhases.Base) return;
            if (started < 0) started = now;
            Press(now, "Entendido");
            // A third of the time on each screen.
            var next = Math.Min(Tabs.Length - 1, (int)((now - started) / (seconds / Tabs.Length)));
            if (next == tab) return;
            if (!UiPress.Tab(Tabs[next])) return;
            tab = next;
            log($"[prueba] medir: base · {Tabs[next]}");
        }

        /// <summary>The case's game asked for (again, every few seconds until it begins): its load timed from the first time.</summary>
        private void Ask(NetLink link, ClientMessage msg, float now)
        {
            if (askedAt < 0) askedAt = now;
            link.Send(msg);
        }

        /// <summary>The load, until the case's first card shows (the arrival's, the premise's): how long, and its longest frame.</summary>
        private void Loading(float now)
        {
            longest = Math.Max(longest, Time.unscaledDeltaTime);
            if (UiPress.Shown("Entendido") == 0) return;
            loaded = true;
            log($"[prueba] medir: {which} cargado a los {now - askedAt:F1} s de pedirlo · fotograma más largo {longest * 1000:F0} ms");
        }

        /// <summary>The seat's next order, as CampaignRun gives them: once the screen has played the last one, or a few seconds on.</summary>
        private void Play(NetLink link, RoomMessage room, GameState s, float now)
        {
            if (sentAt >= 0 && s.Seq == sentAt && now - sentWhen < 6) return;
            if (!screenIdle && now - sentWhen < (lastWasEnd ? 8 : 3)) return;
            if (now - sentWhen < 0.6f) return;
            // Who counts as here for the host: a dropped partner too, through its grace (their soldiers and command stay theirs).
            var cmd = MissionBot.Next(s, link.Slot, heard.Active());
            if (cmd == null) return;
            lastWasEnd = cmd is EndTurnCommand;
            link.Send(cmd);
            sentAt = s.Seq;
            sentWhen = now;
        }

        /// <summary>A button of the screen pressed, and the turn's stamp let go, once a second at most.</summary>
        private void Press(float now, string text)
        {
            if (now < nextPress) return;
            nextPress = now + 1;
            UiPress.Button(text);
            UiPress.Click("clic para seguir");
        }

        private bool Due(float now, float every)
        {
            if (now < nextSend) return false;
            nextSend = now + every;
            return true;
        }
    }
}
