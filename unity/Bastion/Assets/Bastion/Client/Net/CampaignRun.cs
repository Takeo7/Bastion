using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;

namespace Bastion.Client.Net
{
    /// <summary>
    /// For tests with two apps, without clicking (BASTION_CAMPANA=1 in both, with BASTION_ANFITRION or BASTION_UNIRSE): a
    /// campaign from a new one to its missions and back, as far as the network goes; never in a game of the players.
    /// The host opens a new campaign once both are in. On the base, each app gives its key to launch, BASTION_LANZAR
    /// seconds in (8 unless said; 14 after a mission, for the debrief). In the mission, MissionBot plays this seat's
    /// soldiers to the end (BASTION_ABANDONAR=&lt;seconds&gt;: the host leaves it then instead, and the partner says yes);
    /// once over, the host takes both back to the base. BASTION_MISIONES missions (2 unless said).
    ///
    /// BASTION_FOTOS=&lt;folder&gt;: the screen a few seconds into each step: 1-base, 1-mision, 1-final, 1-parte, 2-base…
    ///
    /// It goes by the room's phase (NetLink.LastRoom) and a clock, and for the mission by a copy of its state of its own
    /// (Observe: the snapshot and the events applied, as the screen does): it never listens to NetLink.Message, whose
    /// messages the battle's scene waits for, and changes nothing of what NetLink hands on.
    /// </summary>
    internal sealed class CampaignRun
    {
        private enum Step
        {
            Lobby,
            Base,
            Mission,
            Over,
            Done,
        }

        private readonly float launchAfter;
        private readonly float? leaveAfter;
        private readonly int missions;
        private readonly string? shots;
        private readonly Action<string> log;
        private Step step = Step.Lobby;
        /// <summary>The mission being played or last played, from 1.</summary>
        private int round;
        /// <summary>When the step began (Time.realtimeSinceStartup).</summary>
        private float since;
        private float nextSend;
        private readonly HashSet<string> shot = new HashSet<string>();

        /// <summary>The mission as this app has heard it: the last snapshot with the events since applied; null without one.</summary>
        private GameState? mission;
        /// <summary>The seq the last order was sent at, and when: the next waits for its answer (or a while).</summary>
        private int sentAt = -1;
        private float sentWhen;

        private CampaignRun(float launchAfter, float? leaveAfter, int missions, string? shots, Action<string> log)
        {
            this.launchAfter = launchAfter;
            this.leaveAfter = leaveAfter;
            this.missions = missions;
            this.shots = shots;
            this.log = log;
        }

        /// <summary>The run BASTION_CAMPANA asks for, or null.</summary>
        public static CampaignRun? FromEnvironment(Action<string> log)
        {
            if (Environment.GetEnvironmentVariable("BASTION_CAMPANA") != "1") return null;
            var shots = Environment.GetEnvironmentVariable("BASTION_FOTOS");
            if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots!);
            var leave = Number("BASTION_ABANDONAR");
            return new CampaignRun(Number("BASTION_LANZAR") ?? 8, leave, (int)(Number("BASTION_MISIONES") ?? 2), string.IsNullOrEmpty(shots) ? null : shots, log);
        }

        private static float? Number(string name)
        {
            return float.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : (float?)null;
        }

        /// <summary>A message from the host, before NetLink hands it on: only read, for the copy of the mission.</summary>
        public void Observe(ServerMessage msg)
        {
            switch (msg)
            {
                case SnapshotMessage snapshot:
                    // A copy: the screen applies the events to the one it gets.
                    mission = snapshot.State == null ? null : Json.Read<GameState>(Json.Write(snapshot.State));
                    sentAt = -1;
                    break;
                case EventsMessage events:
                    if (mission == null) break;
                    // A batch missed: the feed asks for the whole state again, and the copy waits for it.
                    if (mission.Seq + events.Events.Count != events.Seq)
                    {
                        mission = null;
                        break;
                    }
                    foreach (var e in Json.Read<List<GameEvent>>(Json.Write(events.Events))) Events.ApplyEvent(mission, e);
                    break;
                case RejectedMessage rejected:
                    log($"[prueba] campaña: orden rechazada: {rejected.Message}");
                    sentAt = -1;
                    break;
            }
        }

        public void Tick(NetLink link, float now)
        {
            var room = link.LastRoom;
            if (room == null || link.Slot < 0 || step == Step.Done) return;
            var phase = room.Phase;
            switch (step)
            {
                case Step.Lobby:
                    if (phase == RoomPhases.Base)
                    {
                        round = 1;
                        Enter(Step.Base, now, "en la base");
                        break;
                    }
                    if (link.Hosting && phase == RoomPhases.Lobby && room.Players.Count((p) => p.Connected) == 2 && Due(now, 3)) link.Send(new NewCampaignMessage());
                    break;
                case Step.Base:
                    if (round == 1) Shoot("1-base", now, 4);
                    else
                    {
                        Shoot($"{round - 1}-parte", now, 3);
                        Shoot($"{round}-base", now, 10);
                    }
                    if (round > missions && now - since > 11)
                    {
                        log("[prueba] campaña: terminada");
                        step = Step.Done;
                        break;
                    }
                    if (phase == RoomPhases.Mission)
                    {
                        Enter(Step.Mission, now, $"misión {round}");
                        break;
                    }
                    // Its key, again until both are in: the room takes it once it can.
                    if (round <= missions && now - since >= (round == 1 ? launchAfter : Math.Max(launchAfter, 14)) && Due(now, 2)) link.Send(new CampaignCmdMessage { Cmd = new LaunchCommand { Ready = true } });
                    break;
                case Step.Mission:
                    Shoot($"{round}-mision", now, 12);
                    if (phase == RoomPhases.Base)
                    {
                        round++;
                        Enter(Step.Base, now, "de vuelta en la base");
                        break;
                    }
                    if (mission?.Outcome != null)
                    {
                        Enter(Step.Over, now, $"misión {round} terminada: {mission.Outcome} ({mission.OutcomeReason})");
                        break;
                    }
                    if (leaveAfter != null) Leave(link, now, leaveAfter.Value);
                    else Play(link, room, now);
                    break;
                case Step.Over:
                    Shoot($"{round}-final", now, 4);
                    if (phase == RoomPhases.Base)
                    {
                        round++;
                        Enter(Step.Base, now, "de vuelta en la base");
                        break;
                    }
                    if (link.Hosting && now - since >= 8 && Due(now, 2)) link.Send(new ReturnToBaseMessage());
                    break;
            }
        }

        /// <summary>This seat's next order, MissionBot's, once the last one has its answer (or after a while).</summary>
        private void Play(NetLink link, RoomMessage room, float now)
        {
            var s = mission;
            if (s == null) return;
            if (sentAt >= 0 && s.Seq == sentAt && now - sentWhen < 4) return;
            if (now - sentWhen < 0.4f) return;
            var connected = room.Players.Where((p) => p.Connected).Select((p) => p.Slot).ToList();
            var cmd = MissionBot.Next(s, link.Slot, connected);
            if (cmd == null) return;
            link.Send(cmd);
            sentAt = s.Seq;
            sentWhen = now;
        }

        /// <summary>BASTION_ABANDONAR: the host asks to leave, the partner says yes.</summary>
        private void Leave(NetLink link, float now, float after)
        {
            if (now - since < after || !Due(now, 2)) return;
            if (link.Hosting) link.Send(new AbandonMessage());
            else link.Send(new AbandonAnswerMessage { Accept = true });
        }

        private void Enter(Step next, float now, string what)
        {
            step = next;
            since = now;
            nextSend = 0;
            log($"[prueba] campaña: {what}");
        }

        /// <summary>Time to send again, `every` seconds apart.</summary>
        private bool Due(float now, float every)
        {
            if (now < nextSend) return false;
            nextSend = now + every;
            return true;
        }

        /// <summary>The screen as `name`.png, once, `after` seconds into the step (BASTION_FOTOS).</summary>
        private void Shoot(string name, float now, float after)
        {
            if (shots == null || now - since < after || !shot.Add(name)) return;
            var file = Path.Combine(shots, name + ".png");
            ScreenCapture.CaptureScreenshot(file);
            log($"[prueba] campaña: foto {file}");
        }
    }
}
