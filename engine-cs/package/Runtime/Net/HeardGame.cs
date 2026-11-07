using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Net
{
    /// <summary>
    /// What a player's app has heard of the game, kept as its screen keeps it: the room, the campaign, and the mission
    /// (its snapshot with the events since applied, on a copy of its own; none while the room isn't in one). For the
    /// tests between two apps (the app's CampaignRun, NetCampaignRunTests): MissionBot plays on it, with Active for who
    /// counts as here, and BaseOrder says what to do on the base. Only reads what it is given.
    /// </summary>
    public sealed class HeardGame
    {
        private readonly IClock clock;

        /// <summary>When each player not connected now was heard to drop (the clock's ms).</summary>
        private readonly Dictionary<int, long> droppedAt = new Dictionary<int, long>();

        public HeardGame(IClock? clock = null)
        {
            this.clock = clock ?? new SystemClock();
        }

        public RoomMessage? Room { get; private set; }

        /// <summary>The campaign as last sent (the very object: read it only).</summary>
        public CampaignState? Campaign { get; private set; }

        /// <summary>The mission now, a copy of its own; null outside one, and after a batch of events missed until the next snapshot.</summary>
        public GameState? Mission { get; private set; }

        public void Observe(ServerMessage msg)
        {
            switch (msg)
            {
                case RoomMessage room:
                    foreach (var p in room.Players)
                    {
                        if (p.Connected) droppedAt.Remove(p.Slot);
                        // The host says how long they still count (also for one never seen here: a mission opened again without them).
                        else if (p.GraceMs != null) droppedAt[p.Slot] = clock.Now + p.GraceMs.Value - Grace(p);
                        else if (Room?.Players.FirstOrDefault((q) => q.Slot == p.Slot)?.Connected == true) droppedAt[p.Slot] = clock.Now;
                    }
                    Room = room;
                    // Out of the mission: what was heard of it is over (the next one comes with its snapshot).
                    if (room.Phase != RoomPhases.Mission) Mission = null;
                    break;
                case CampaignMessage campaign:
                    Campaign = campaign.State;
                    break;
                case SnapshotMessage snapshot:
                    // A copy: the screen applies the events to the one it gets.
                    Mission = snapshot.State == null ? null : Json.Read<GameState>(Json.Write(snapshot.State));
                    break;
                case EventsMessage events:
                    if (Mission == null) break;
                    // A batch missed: the whole state comes again (ServerFeed asks for it), and the copy waits for it.
                    if (Mission.Seq + events.Events.Count != events.Seq)
                    {
                        Mission = null;
                        break;
                    }
                    foreach (var e in Json.Read<List<GameEvent>>(Json.Write(events.Events))) Events.ApplyEvent(Mission, e);
                    break;
            }
        }

        /// <summary>
        /// The players who count as here (Room.ActiveSlots, as the battle screen keeps them): connected, or dropped within
        /// the host's grace (Room.ReconnectGraceMs; AwayGraceMs if their game was out of sight), from when this app heard
        /// it. Till then the host keeps the dropped one's soldiers and command theirs: MissionBot plays with these, not
        /// with the connected (29/10: it tried the dropped partner's soldiers every 0.6 s, «Es de la escuadra del otro
        /// jugador.»).
        /// </summary>
        public List<int> Active()
        {
            var now = clock.Now;
            return (Room?.Players ?? new List<PlayerInfo>())
                .Where((p) => p.Connected || (droppedAt.TryGetValue(p.Slot, out var at) && now - at < Grace(p)))
                .Select((p) => p.Slot)
                .ToList();
        }

        private static long Grace(PlayerInfo p) => p.Away == true ? Bastion.Net.Room.AwayGraceMs : Bastion.Net.Room.ReconnectGraceMs;

        /// <summary>
        /// What `slot` does on the base before launching: the partner's proposal, yes; an event, the host's first option it
        /// can take (the convoy's wants someone fit to send); with no operation chosen, a day's wait while someone is
        /// wounded, then the host's proposal of the easiest (a day's wait with none); with none of its soldiers in the squad
        /// and one fit to go (the recruit who comes when nobody is left: Arturo, 2/11), that one in; with nobody in the squad
        /// at all, a day's wait till someone heals or comes, or in a siege (it takes everyone fit by itself, and nobody can
        /// wait) the host's recruit straight into it; else its key to launch. Null: wait (one's own proposal, the partner's
        /// event or choice).
        /// </summary>
        public CampaignCommand? BaseOrder(int slot, bool hosting)
        {
            var c = Campaign;
            if (c == null) return null;
            if (c.Proposal != null) return c.Proposal.By != slot ? new VoteCommand { Accept = true } : null;
            if (c.Event != null) return hosting ? new AnswerEventCommand { Option = Engine.Campaign.EventOptionAvailable(c, 0) || !Engine.Campaign.EventOptionAvailable(c, 1) ? 0 : 1 } : null;
            // No operation chosen (no siege comes to choose it now: Arturo, 7/11, Campaign.SiegesOn): with someone wounded, a
            // day's wait till all are fit, since the wounded don't go (of seeds 1 to 200, 47 won the opening operation; the
            // operation after it was won once going without them, with one to three soldiers, and 5 times waiting); then the
            // host proposes the easiest one there is, and the partner says yes; with none, a day's wait for more.
            if (c.ActiveMission == null)
            {
                if (c.Soldiers.Any((s) => s.Alive && s.Infection == null && s.WoundedDays > 0)) return new AdvanceCommand { Ready = true };
                var easiest = c.Offers.Where((o) => !o.Final && o.Facility == null && o.Siege != true && (o.ExpiresDay == null || o.ExpiresDay >= c.Day))
                    .OrderBy((o) => o.Difficulty).ThenBy((o) => o.Id, System.StringComparer.Ordinal).FirstOrDefault();
                if (easiest == null) return new AdvanceCommand { Ready = true };
                return hosting ? new ChooseMissionCommand { Id = easiest.Id } : null;
            }
            var siege = c.Offers.FirstOrDefault((o) => o.Id == c.ActiveMission)?.Siege == true;
            if (c.ActiveMission != null && !siege && c.Squad.Count < Engine.Campaign.SquadMax(c))
            {
                var mine = c.Soldiers.Where((s) => s.Owner == slot).ToList();
                var left = mine.FirstOrDefault((s) => Engine.Campaign.SoldierReady(s) && !c.Squad.Contains(s.Id));
                if (left != null && !mine.Any((s) => c.Squad.Contains(s.Id))) return new SquadCommand { Soldier = left.Id, On = true };
            }
            if (c.ActiveMission != null && c.Squad.Count == 0) return !siege ? new AdvanceCommand { Ready = true } : hosting ? new RecruitCommand { Squad = true } : null;
            return new LaunchCommand { Ready = true };
        }
    }
}
