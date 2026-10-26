using System.Collections.Generic;
using Bastion.Engine;

namespace Bastion.Net
{
    /// <summary>
    /// What a player's app has heard of the game, kept as its screen keeps it: the room, the campaign, and the mission
    /// (its snapshot with the events since applied, on a copy of its own; none while the room isn't in one). For the
    /// tests between two apps (the app's CampaignRun, NetCampaignRunTests): MissionBot plays on it, and BaseOrder says
    /// what to do on the base. Only reads what it is given.
    /// </summary>
    public sealed class HeardGame
    {
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
        /// What `slot` does on the base before launching: the partner's proposal, yes; an event, the host's first option;
        /// else its key to launch. Null: wait (one's own proposal, the partner's event).
        /// </summary>
        public CampaignCommand? BaseOrder(int slot, bool hosting)
        {
            var c = Campaign;
            if (c == null) return null;
            if (c.Proposal != null) return c.Proposal.By != slot ? new VoteCommand { Accept = true } : null;
            if (c.Event != null) return hosting ? new AnswerEventCommand { Option = 0 } : null;
            return new LaunchCommand { Ready = true };
        }
    }
}
