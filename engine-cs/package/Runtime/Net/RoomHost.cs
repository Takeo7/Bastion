using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bastion.Net
{
    /// <summary>
    /// The host's side of a game (index.ts): the room on a thread of its own, and the TCP port the players' apps connect
    /// to, the host's own by 127.0.0.1. Stop on quitting, so the partner hears the host is gone and the game is saved.
    /// </summary>
    public sealed class RoomHost : IDisposable
    {
        /// <summary>
        /// The game's first port, and what a typed address without one means. Not the TypeScript app's 3000: the two can be
        /// open on one computer, and on Arturo's Mac OrbStack holds 127.0.0.1:3000.
        /// </summary>
        public const int FirstPort = 41391;

        private readonly Room room;
        private readonly TcpHost tcp;
        private readonly ConcurrentDictionary<TcpConnection, byte> open = new ConcurrentDictionary<TcpConnection, byte>();
        private Thread? thread;
        private volatile bool stopping;
        private int stopped;

        /// <summary>On `port` (0: any free one), on every address unless `address` says one.</summary>
        public RoomHost(Room room, int port, IPAddress? address = null)
        {
            this.room = room;
            tcp = new TcpHost(port, address);
            tcp.Connected += (c) =>
            {
                // Kept until its socket is shut, so Stop waits for what it still had to send.
                open[c] = 0;
                c.Ended.ContinueWith((t) => open.TryRemove(c, out _));
                room.Connect(c);
            };
        }

        /// <summary>The port it listens on.</summary>
        public int Port => tcp.Port;

        /// <summary>Started on the first free port of `tries` from `first` (another game or program may hold one); throws if none is.</summary>
        public static RoomHost StartFree(Room room, int first = FirstPort, int tries = 10, IPAddress? address = null)
        {
            for (int i = 0; ; i++)
            {
                var host = new RoomHost(room, first + i, address);
                try
                {
                    host.Start();
                    return host;
                }
                catch (SocketException) when (i + 1 < tries)
                {
                    host.Stop(0);
                }
            }
        }

        /// <summary>Starts listening and the room's thread; throws if the port is taken.</summary>
        public void Start()
        {
            tcp.Start();
            thread = new Thread(Run) { IsBackground = true, Name = "Bastion room" };
            thread.Start();
        }

        private void Run()
        {
            while (!stopping)
            {
                room.Pump();
                room.Posted.WaitOne(room.WaitMs(1000));
            }
            room.Pump();
        }

        /// <summary>
        /// Shuts down (the app quits): the game saved and everyone told (Room.Closing), no more connections taken, and
        /// back once what was sent has gone out, or after `waitMs` whatever happens.
        /// </summary>
        public void Stop(int waitMs = 1500)
        {
            if (Interlocked.Exchange(ref stopped, 1) != 0) return;
            tcp.Dispose();
            if (thread == null) return;
            var closed = new ManualResetEventSlim(false);
            room.Post(() =>
            {
                room.Closing();
                closed.Set();
            });
            closed.Wait(waitMs);
            stopping = true;
            room.Post(() => { });
            thread.Join(waitMs);
            Task.WaitAll(open.Keys.Select((c) => c.Ended).ToArray(), waitMs);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
