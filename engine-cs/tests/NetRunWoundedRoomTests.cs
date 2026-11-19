using System;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G23.3 El herido in the room (Red, 19/11; Producto, by Gameplay: «que entrar tarde y la reconexión vean al herido a
    /// hombros»): a fight restored from its save with the wounded on Ana's soldier's back. Who plays on sees them go where
    /// their carrier goes and change backs, event by event (Events.ApplyEvent: KeepCarried, Hauled); who comes late, and
    /// who comes back, get the room's own state in its snapshot: carried, on whose back, where, how far walked with them.
    /// </summary>
    public class NetRunWoundedRoomTests
    {
        private sealed class Seat
        {
            public MemoryEnd End = null!;
            public HeardGame Heard = new HeardGame();
        }

        private static Seat Join(Room room, string name, int port)
        {
            var (end, host) = MemoryEnd.Pair(name, $"127.0.0.1:{port}");
            room.Connect(host);
            room.Pump();
            var seat = new Seat { End = end };
            end.Send(new JObject { ["t"] = "hello", ["name"] = name, ["version"] = "1.0.0" }.ToString(Newtonsoft.Json.Formatting.None));
            room.Pump();
            Drain(seat);
            return seat;
        }

        private static void Drain(Seat seat)
        {
            while (seat.End.Heard.TryDequeue(out var text)) seat.Heard.Observe(Json.Read<ServerMessage>(text));
        }

        private static void Order(Room room, Seat seat, Command cmd)
        {
            seat.End.Send(new JObject { ["t"] = "cmd", ["cmd"] = JObject.Parse(Json.Write(cmd)) }.ToString(Newtonsoft.Json.Formatting.None));
            room.Pump();
        }

        /// <summary>A free tile beside `p`, on its level, reached from it as a box is searched, out of La Lata.</summary>
        private static Pos Beside(GameState s, Pos p) => Grid.Dirs8.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z })
            .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && Grid.UnitAt(s, q) == null
                && Looting.ReachesToSearch(s, p, q) && !Abilities.InEvacZone(s, q));

        /// <summary>What a view says of the wounded and of everyone of the squad: carried by whom, carrying whom, where, walked with them.</summary>
        private static string Seen(GameState s)
        {
            var w = s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);
            return $"wounded {s.Mission.Wounded} lame {w.Lame} alive {w.Alive} by {w.CarriedBy ?? "-"} at {w.Pos.X},{w.Pos.Y},{w.Pos.Z}; "
                + string.Join(" ", s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).OrderBy((u) => u.Id, StringComparer.Ordinal)
                    .Select((u) => $"{u.Id}@{u.Pos.X},{u.Pos.Y} carrying {u.Carrying ?? "-"} hauled {u.Hauled?.ToString() ?? "-"}"));
        }

        [Fact]
        public void WhoComesLateOrBackSeesThemCarriedAsTheRoomDoes()
        {
            // The fight: El herido at the police station, Ana's two and Pepe's two; Ana's first carrying them, her second
            // beside it. Ana has the command.
            var c = Two(5);
            var run = Start(c, 5);
            var n = RunMap.Choices(run)[0];
            n.Kind = NodeKinds.Wounded;
            n.Modifier = null;
            n.Site = Places.Police;
            n.Seed = 105;
            Agree(c, new ChooseNodeCommand { Node = n.Id });
            var rules = new EngineCampaignRules();
            var s = rules.LaunchMission(c, new Rng(5))!.Value.state;
            s.Units.RemoveAll((u) => u.Team == Teams.Alien);
            s.Pods.Clear();
            var w = s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);
            var ana = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Owner == 0).ToList();
            Assert.Equal(2, ana.Count);
            ana[0].Pos = Beside(s, w.Pos);
            ana[0].Ap = 2;
            s.Command = 0;
            Assert.True(Exec(s, new AbilityCommand { Unit = ana[0].Id, Ability = AbilityIds.Carry }, connected: Both).Ok);
            ana[1].Pos = Beside(s, ana[0].Pos);
            ana[1].Ap = 2;
            ana[1].Moves = null;
            ana[0].Ap = 2;
            s.ActiveUnit = null;

            var dir = Path.Combine(Path.GetTempPath(), "bastion-herido-" + Guid.NewGuid().ToString("N"));
            var file = Path.Combine(dir, "save.json");
            Directory.CreateDirectory(dir);
            try
            {
                new SaveStore(file, null, rules).Save(new SaveFile { Phase = RoomPhases.Mission, Mode = "campaign", State = s, Campaign = c });
                var room = new Room(new SaveStore(file, null, rules), new EngineRules(), "1.0.0", null, new FakeClock(), new Rng(7), null, rules);

                // Ana back in the restored fight: carried, on her first's back.
                var a = Join(room, "Ana", 5000);
                var seen = a.Heard.Mission!;
                var wa = seen.Units.Single((u) => u.Id == w.Id);
                Assert.True(seen.Mission.Wounded);
                Assert.True(wa.Lame);
                Assert.False(wa.Alive);
                Assert.Equal(ana[0].Id, wa.CarriedBy);
                Assert.Equal(w.Id, seen.Units.Single((u) => u.Id == ana[0].Id).Carrying);

                // Passed to her second, who walks two tiles off with them: her view, event by event, as the room's.
                var first = seen.Units.Single((u) => u.Id == ana[0].Id);
                var second = seen.Units.Single((u) => u.Id == ana[1].Id);
                Order(room, a, new AbilityCommand { Unit = first.Id, Ability = AbilityIds.Pass, Target = second.Id });
                Drain(a);
                Assert.Equal(ana[1].Id, a.Heard.Mission!.Units.Single((u) => u.Id == w.Id).CarriedBy);
                var reach = Paths.ComputeReach(a.Heard.Mission!, a.Heard.Mission!.Units.Single((u) => u.Id == ana[1].Id), Paths.MoveBudget(a.Heard.Mission!, a.Heard.Mission!.Units.Single((u) => u.Id == ana[1].Id)));
                var to = Paths.Destinations(a.Heard.Mission!, a.Heard.Mission!.Units.Single((u) => u.Id == ana[1].Id), reach)
                    .Where((d) => !Abilities.InEvacZone(a.Heard.Mission!, d.Pos)).OrderByDescending((d) => d.Cost).ThenBy((d) => Grid.NodeOf(a.Heard.Mission!, d.Pos)).First().Pos;
                Order(room, a, new MoveCommand { Unit = ana[1].Id, To = to });
                Drain(a);
                var mine = a.Heard.Mission!;
                Assert.True(Grid.SamePos(to, mine.Units.Single((u) => u.Id == ana[1].Id).Pos), Seen(mine));
                Assert.True(Grid.SamePos(to, mine.Units.Single((u) => u.Id == w.Id).Pos), Seen(mine));
                Assert.True(mine.Units.Single((u) => u.Id == ana[1].Id).Hauled > 0);

                // The room's own, from its save as it is now.
                var kept = new SaveStore(file, null, rules).Load()!.State!;
                Assert.Equal(Seen(kept), Seen(mine));

                // Pepe comes late: his snapshot, the same.
                var p = Join(room, "Pepe", 5001);
                Assert.Equal(Seen(kept), Seen(p.Heard.Mission!));

                // Ana's window drops and comes back: the same.
                a.End.Close("caída");
                room.Pump();
                var back = Join(room, "Ana", 5002);
                Assert.Equal(Seen(kept), Seen(back.Heard.Mission!));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
