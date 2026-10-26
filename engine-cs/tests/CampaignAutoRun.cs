using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json.Linq;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign the two test apps play by themselves (the app's CampaignRun, BASTION_CAMPANA), through a real room
    /// with the engine's rules: Ana hosts and opens a new campaign; on the base each answers what waits (HeardGame.
    /// BaseOrder) and gives its key; in the mission each plays its soldiers (MissionBot) on what it has heard; once over,
    /// the host takes both back. The room's Rng seeded as BASTION_SEMILLA seeds the app's: what MissionBot does depends on
    /// the state alone, so the apps play the same campaign. Without NetCampaignRunTests' xUnit, for the scratchpad too.
    /// </summary>
    public static class CampaignAutoRun
    {
        public sealed class Mission
        {
            public string Name = "";
            public string Outcome = "";
            public string? Reason;
            public int Turns;
            public int Orders;
        }

        /// <summary>Why a run stopped short (stuck on the base or in a mission), or null.</summary>
        [ThreadStatic] public static string? Stuck;

        /// <summary>Up to `missions` missions of a new campaign with the room's Rng at `seed`; `say`, a line for each step.</summary>
        public static List<Mission> Run(int seed, int missions, Action<string>? say = null)
        {
            var dir = Path.Combine(Path.GetTempPath(), "bastion-autocampana-" + Guid.NewGuid().ToString("N"));
            try
            {
                var rules = new EngineCampaignRules();
                var room = new Room(new SaveStore(Path.Combine(dir, "save.json"), null, rules), new EngineRules(), "1.0.0", null, new FakeClock(), new Rng(seed), null, rules);
                var players = new[] { Join(room, "Ana", 0), Join(room, "Pepe", 1) };
                Say(room, players[0], new JObject { ["t"] = "newCampaign" });
                var played = new List<Mission>();
                Mission? now = null;
                Stuck = null;
                var refused = new List<string>();
                int still = 0;
                string? was = null;
                for (int step = 0; step < 50_000; step++)
                {
                    foreach (var p in players)
                        while (p.End.Heard.TryDequeue(out var text))
                        {
                            var msg = Json.Read<ServerMessage>(text);
                            if (msg is RejectedMessage r) refused.Add($"{p.End.Peer}: {r.Message}");
                            p.Heard.Observe(msg);
                        }
                    var heard = players[0].Heard;
                    var phase = heard.Room?.Phase;
                    // Nothing moving for a while: said, and the run stops.
                    var c = heard.Campaign;
                    var where = $"{phase} · día {c?.Day} · acontecimiento {c?.Event ?? "-"} · propuesta {(c?.Proposal != null ? $"{c.Proposal.Action.Type} de {c.Proposal.By}" : "-")} · llaves {string.Join(",", c?.LaunchReady ?? new bool[0])} · operación {c?.ActiveMission ?? "-"} · seq {heard.Mission?.Seq}";
                    still = where == was ? still + 1 : 0;
                    was = where;
                    if (still > 200)
                    {
                        Stuck = $"{where} · rechazadas: {string.Join(" | ", refused.Distinct().TakeLast(4))}";
                        say?.Invoke("atascada: " + Stuck);
                        return played;
                    }
                    if (phase == RoomPhases.Base)
                    {
                        if (now != null)
                        {
                            say?.Invoke($"vuelta a la base tras «{now.Name}»");
                            now = null;
                        }
                        if (played.Count >= missions) return played;
                        foreach (var p in players)
                        {
                            var order = p.Heard.BaseOrder(p.Slot, p.Slot == 0);
                            if (order != null) Say(room, p, new JObject { ["t"] = "ccmd", ["cmd"] = JObject.Parse(Json.Write(order)) });
                        }
                        continue;
                    }
                    if (phase != RoomPhases.Mission || heard.Mission == null) continue;
                    if (now == null)
                    {
                        now = new Mission { Name = heard.Mission.CampaignMission?.Name ?? heard.Mission.MapName };
                        played.Add(now);
                        say?.Invoke($"misión «{now.Name}»");
                    }
                    if (heard.Mission.Outcome != null)
                    {
                        if (now.Outcome == "")
                        {
                            now.Outcome = heard.Mission.Outcome;
                            now.Reason = heard.Mission.OutcomeReason;
                            now.Turns = heard.Mission.Turn;
                            say?.Invoke($"«{now.Name}»: {now.Outcome} ({now.Reason}) en el turno {now.Turns}, {now.Orders} órdenes");
                        }
                        Say(room, players[0], new JObject { ["t"] = "returnToBase" });
                        continue;
                    }
                    var connected = heard.Room!.Players.Where((x) => x.Connected).Select((x) => x.Slot).ToList();
                    foreach (var p in players)
                    {
                        if (p.Heard.Mission == null) continue;
                        var cmd = MissionBot.Next(p.Heard.Mission, p.Slot, connected);
                        if (cmd == null) continue;
                        now.Orders++;
                        Say(room, p, new JObject { ["t"] = "cmd", ["cmd"] = JObject.Parse(Json.Write(cmd)) });
                    }
                }
                return played;
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception)
                {
                    /* a temporary folder */
                }
            }
        }

        private sealed class Player
        {
            public MemoryEnd End = null!;
            public int Slot;
            public readonly HeardGame Heard = new HeardGame();
        }

        private static Player Join(Room room, string name, int slot)
        {
            var (end, host) = MemoryEnd.Pair(name, $"10.0.0.{slot + 1}:5000");
            room.Connect(host);
            room.Pump();
            var p = new Player { End = end, Slot = slot };
            Say(room, p, new JObject { ["t"] = "hello", ["name"] = name, ["version"] = "1.0.0" });
            return p;
        }

        private static void Say(Room room, Player p, JObject message)
        {
            p.End.Send(message.ToString(Newtonsoft.Json.Formatting.None));
            room.Pump();
        }
    }
}
