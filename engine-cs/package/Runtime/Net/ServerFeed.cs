using System;
using System.Collections.Generic;

namespace Bastion.Net
{
    /// <summary>
    /// What the host says, on its way to the game (the Unity app's NetLink). The last `room` is kept. A gap in the events
    /// (a `seq` missed: game.ts onEvents) asks for the whole state, and the events are dropped until it comes. And what
    /// comes while nobody listens (the scene changing after the welcome) waits for the first who does.
    /// </summary>
    public sealed class ServerFeed
    {
        /// <summary>Messages kept while nobody listens, at most; past it, the mission's are dropped and a snapshot asked for.</summary>
        public const int MaxHeld = 2000;

        private Action<ServerMessage>? message;
        private readonly List<ServerMessage> held = new List<ServerMessage>();
        /// <summary>The state's seq after the last events heard; null before a snapshot.</summary>
        private int? seq;
        /// <summary>A snapshot asked for: the events until it comes are in it.</summary>
        private bool resyncing;

        /// <summary>
        /// The host's messages, in order. What came while nobody listened goes, in order, to the first who does: the room,
        /// the snapshot and the events since.
        /// </summary>
        public event Action<ServerMessage>? Message
        {
            add
            {
                message += value;
                if (value == null || held.Count == 0) return;
                var waiting = held.ToArray();
                held.Clear();
                foreach (var m in waiting) value(m);
            }
            remove
            {
                message -= value;
            }
        }

        /// <summary>The whole state is to be asked for (a `sync`): why, for red.log.</summary>
        public event Action<string>? Resync;

        /// <summary>Who is in the game and which screen it is on: the last `room`.</summary>
        public RoomMessage? LastRoom { get; private set; }

        public void Take(ServerMessage msg)
        {
            switch (msg)
            {
                case RoomMessage room:
                    LastRoom = room;
                    break;
                case SnapshotMessage snapshot:
                    seq = snapshot.State?.Seq;
                    resyncing = false;
                    break;
                case EventsMessage events:
                    if (resyncing) return;
                    // Each event moves the state's seq by one (events.ts applyEvent): a jump is a batch missed.
                    if (seq != null && seq.Value + events.Events.Count != events.Seq)
                    {
                        AskAgain($"[sync] desfase de eventos ({seq.Value + events.Events.Count} ≠ {events.Seq}): se pide el estado entero");
                        return;
                    }
                    seq = events.Seq;
                    break;
            }
            Deliver(msg);
        }

        /// <summary>Another connection: nothing of the last one is kept.</summary>
        public void Reset()
        {
            LastRoom = null;
            held.Clear();
            seq = null;
            resyncing = false;
        }

        private void Deliver(ServerMessage msg)
        {
            if (message != null)
            {
                message(msg);
                return;
            }
            // A snapshot makes what came before it of the mission's moot.
            if (msg is SnapshotMessage) held.RemoveAll(OfTheMission);
            held.Add(msg);
            if (held.Count <= MaxHeld) return;
            held.RemoveAll(OfTheMission);
            AskAgain($"[sync] más de {MaxHeld} mensajes sin nadie que los lea: se pide el estado entero");
        }

        private static bool OfTheMission(ServerMessage m) => m is SnapshotMessage || m is EventsMessage;

        private void AskAgain(string why)
        {
            resyncing = true;
            Resync?.Invoke(why);
        }
    }
}
