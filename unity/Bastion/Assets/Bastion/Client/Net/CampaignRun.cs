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
    /// campaign from a new one to its missions and back; never in a game of the players. What it does is the engine-cs
    /// tests' CampaignAutoRun, which plays the same through a room: the host opens a new campaign once both are in; on the
    /// base each answers what waits and gives its key (HeardGame.BaseOrder), BASTION_LANZAR seconds in (8 unless said;
    /// 14 after a mission, for the debrief); in the mission each plays its soldiers (MissionBot) on what it has heard, an
    /// order once the screen has played the last one's events (its `idle`) or a few seconds have gone; once over, the host
    /// takes both back to the base. BASTION_ABANDONAR=&lt;seconds&gt;: the host leaves the mission then instead, and the
    /// partner says yes. BASTION_MISIONES missions (2 unless said). BASTION_SEMILLA (NetLink): the room's Rng, so the
    /// campaign is CampaignAutoRun's with that seed.
    ///
    /// BASTION_FOTOS=&lt;folder&gt;: the screen a few seconds into each step: 1-base, 1-mision, 1-final, 1-parte, 2-base…;
    /// once seen, what a player would close is closed (UiPress): the premise, the arrival card, the debrief.
    /// Done, or BASTION_LIMITE seconds after it starts (900 unless said), the app quits (Application.Quit): a test never
    /// leaves one open. It goes by what it has heard (Observe, before NetLink hands each message on) and a clock: it never
    /// listens to NetLink.Message, whose messages the battle's scene waits for, and changes nothing of what NetLink does.
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
        private readonly float limit;
        private readonly string? shots;
        private readonly Action<string> log;
        private readonly HeardGame heard = new HeardGame();
        private Step step = Step.Lobby;
        /// <summary>The mission being played or last played, from 1.</summary>
        private int round;
        /// <summary>When the step began (Time.realtimeSinceStartup).</summary>
        private float since;
        private float nextSend;
        private float nextPress;
        private readonly HashSet<string> shot = new HashSet<string>();
        /// <summary>The seq the last order was sent at, and when: the next waits for its answer (or a while).</summary>
        private int sentAt = -1;
        private float sentWhen;
        private bool lastWasEnd;
        /// <summary>The screen has played every event it was sent (the battle's `idle`, NetLink.Send).</summary>
        private bool screenIdle;
        /// <summary>This mission's orders, and those sent without the screen's `idle` (its wait ran out).</summary>
        private int orders, late, stamps, idles;
        /// <summary>When the screen last said it had played everything.</summary>
        private float idleAt = -1;
        /// <summary>The last order on the base was refused: the next waits longer.</summary>
        private bool refusedOnBase;
        private float quitAt = -1;

        private CampaignRun(float launchAfter, float? leaveAfter, int missions, float limit, string? shots, Action<string> log)
        {
            this.launchAfter = launchAfter;
            this.leaveAfter = leaveAfter;
            this.missions = missions;
            this.limit = limit;
            this.shots = shots;
            this.log = log;
        }

        /// <summary>The run BASTION_CAMPANA asks for, or null.</summary>
        public static CampaignRun? FromEnvironment(Action<string> log)
        {
            if (Environment.GetEnvironmentVariable("BASTION_CAMPANA") != "1") return null;
            var shots = Environment.GetEnvironmentVariable("BASTION_FOTOS");
            if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots!);
            return new CampaignRun(Number("BASTION_LANZAR") ?? 8, Number("BASTION_ABANDONAR"), (int)(Number("BASTION_MISIONES") ?? 2), Number("BASTION_LIMITE") ?? 900, string.IsNullOrEmpty(shots) ? null : shots, log);
        }

        /// <summary>BASTION_SEMILLA, with BASTION_CAMPANA or BASTION_MEDIR (never in a game of the players): the host room's Rng (NetLink), or null.</summary>
        public static int? Seed()
        {
            if (Environment.GetEnvironmentVariable("BASTION_CAMPANA") != "1" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BASTION_MEDIR"))) return null;
            return int.TryParse(Environment.GetEnvironmentVariable("BASTION_SEMILLA"), out var seed) ? seed : (int?)null;
        }

        private static float? Number(string name)
        {
            return float.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : (float?)null;
        }

        /// <summary>A message from the host, before NetLink hands it on: only read.</summary>
        public void Observe(ServerMessage msg)
        {
            heard.Observe(msg);
            if (msg is SnapshotMessage) sentAt = -1;
            if (msg is RejectedMessage rejected)
            {
                log($"[prueba] campaña: orden rechazada: {rejected.Message}");
                sentAt = -1;
                refusedOnBase = step == Step.Base;
            }
        }

        /// <summary>What this app sends, before it goes as it is: the battle's `idle`, for the pace of MissionBot.</summary>
        public void Sent(ClientMessage msg)
        {
            if (msg is IdleMessage idle)
            {
                if (idle.Idle && !screenIdle) idles++;
                if (idle.Idle) idleAt = Time.realtimeSinceStartup;
                screenIdle = idle.Idle;
            }
        }

        public void Tick(NetLink link, float now)
        {
            if (quitAt >= 0)
            {
                if (now >= quitAt) Application.Quit();
                return;
            }
            if (now > limit)
            {
                log($"[prueba] campaña: tiempo agotado ({limit} s) en {step}, misión {round}: se cierra la app");
                quitAt = now;
                return;
            }
            if (step == Step.Done)
            {
                quitAt = now + 5;
                return;
            }
            var room = link.LastRoom;
            if (room == null || link.Slot < 0) return;
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
                    // What a player reads first, then closes: the premise, the debrief («Continuar»).
                    if (round == 1)
                    {
                        Shoot("1-base", now, 4);
                        if (now - since > 6) Press(now, "Entendido");
                    }
                    else
                    {
                        Shoot($"{round - 1}-parte", now, 3);
                        if (now - since > 5) Press(now, "Continuar");
                        Shoot($"{round}-base", now, 10);
                    }
                    if (round > missions)
                    {
                        if (now - since > 11)
                        {
                            log("[prueba] campaña: terminada");
                            step = Step.Done;
                        }
                        break;
                    }
                    if (phase == RoomPhases.Mission)
                    {
                        Enter(Step.Mission, now, $"misión {round}");
                        break;
                    }
                    if (now - since < (round == 1 ? launchAfter : Math.Max(launchAfter, 14)) || !Due(now, refusedOnBase ? 6 : 2)) break;
                    refusedOnBase = false;
                    var order = heard.BaseOrder(link.Slot, link.Hosting);
                    if (order != null) link.Send(new CampaignCmdMessage { Cmd = order });
                    break;
                case Step.Mission:
                    Shoot($"{round}-mision", now, 12);
                    // The arrival card closed once it is seen, and the turn's stamp let go, as a player would: the screen
                    // then says when it has played everything (its `idle` waits for nothing over the field). The orders
                    // wait for that, from the card's closing on: sent faster, the screen falls behind and never catches up.
                    if (now - since < 13) break;
                    Press(now, "Entendido");
                    if (UiPress.Click("clic para seguir") > 0) stamps++;
                    if (phase == RoomPhases.Base)
                    {
                        round++;
                        Enter(Step.Base, now, "de vuelta en la base");
                        break;
                    }
                    var s = heard.Mission;
                    if (s?.Outcome != null)
                    {
                        Enter(Step.Over, now, $"misión {round} terminada: {s.Outcome} ({s.OutcomeReason}) en el turno {s.Turn} · {orders} órdenes, {late} sin esperar a la pantalla, {stamps} sellos de turno soltados, la pantalla quieta {idles} veces");
                        orders = late = stamps = idles = 0;
                        break;
                    }
                    if (leaveAfter != null) Leave(link, now, leaveAfter.Value);
                    else Play(link, room, now);
                    break;
                case Step.Over:
                    // The end as the screen shows it: once it says it has played everything since (or 180 s on). The
                    // stamps and cards of the end let go meanwhile, as before.
                    if (UiPress.Click("clic para seguir") > 0) stamps++;
                    if ((screenIdle && idleAt > since) || now - since > 180) Shoot($"{round}-final", now, 1);
                    if (phase == RoomPhases.Base)
                    {
                        round++;
                        Enter(Step.Base, now, "de vuelta en la base");
                        break;
                    }
                    // Back once its own end is seen, and the partner's had time to be (25 s at least).
                    if (link.Hosting && shot.Contains($"{round}-final") && now - since > 25 && Due(now, 3)) link.Send(new ReturnToBaseMessage());
                    break;
            }
        }

        /// <summary>This seat's next order, MissionBot's, once the last one has its answer and the screen has played it.</summary>
        private void Play(NetLink link, RoomMessage room, float now)
        {
            var s = heard.Mission;
            if (s == null) return;
            // The answer heard, and played on the screen (its `idle`, with nothing over the field: Mission closes the
            // card and lets the stamp go); 90 s at most, should the screen never say so (the dead's turn can take 30 s).
            if (sentAt >= 0 && s.Seq == sentAt && now - sentWhen < 6) return;
            if (!screenIdle && now - sentWhen < 90) return;
            if (now - sentWhen < 0.6f) return;
            var connected = room.Players.Where((p) => p.Connected).Select((p) => p.Slot).ToList();
            var cmd = MissionBot.Next(s, link.Slot, connected);
            if (cmd == null) return;
            orders++;
            if (!screenIdle) late++;
            lastWasEnd = cmd is EndTurnCommand;
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

        /// <summary>A button of the screen, pressed (UiPress), once a second at most.</summary>
        private void Press(float now, string text)
        {
            if (now < nextPress) return;
            nextPress = now + 1;
            if (UiPress.Button(text) > 0) log($"[prueba] campaña: «{text}» pulsado");
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
