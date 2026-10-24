using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Net;
using Newtonsoft.Json.Linq;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Two ends of a connection in memory, for the room's and the client's tests: what one sends, the other hears, in
    /// order, on the sender's thread; closing one closes both.
    /// </summary>
    public sealed class MemoryEnd : IConnection
    {
        private int closed;

        public MemoryEnd? Other { get; set; }
        public string Peer { get; }
        public bool IsClosed => Volatile.Read(ref closed) != 0;

        /// <summary>What this end heard, in order.</summary>
        public ConcurrentQueue<string> Heard { get; } = new ConcurrentQueue<string>();

        public event Action<string>? Message;
        public event Action<string>? Closed;

        public MemoryEnd(string peer)
        {
            Peer = peer;
            Message += (m) => Heard.Enqueue(m);
        }

        public static (MemoryEnd a, MemoryEnd b) Pair(string a = "cliente", string b = "anfitrión")
        {
            var x = new MemoryEnd(a);
            var y = new MemoryEnd(b);
            x.Other = y;
            y.Other = x;
            return (x, y);
        }

        public void Send(string json)
        {
            var other = Other;
            if (IsClosed || other == null || other.IsClosed) return;
            other.Message?.Invoke(json);
        }

        public void Close(string reason)
        {
            if (Interlocked.Exchange(ref closed, 1) != 0) return;
            Closed?.Invoke(reason);
            Other?.Close("cerrada");
        }

        /// <summary>The messages heard so far whose `t` is `kind`, taken out of the queue (the others stay).</summary>
        public List<JObject> Take(string kind)
        {
            var got = new List<JObject>();
            var rest = new List<string>();
            while (Heard.TryDequeue(out var m))
            {
                var o = JObject.Parse(m);
                if ((string?)o["t"] == kind) got.Add(o);
                else rest.Add(m);
            }
            foreach (var m in rest) Heard.Enqueue(m);
            return got;
        }
    }

    /// <summary>A clock the test moves by hand.</summary>
    public sealed class FakeClock : IClock
    {
        public long Now { get; set; } = 1000;

        public void Advance(long ms)
        {
            Now += ms;
        }
    }

    /// <summary>A token kept in memory.</summary>
    public sealed class MemoryTokens : ITokenStore
    {
        public string? Token { get; set; }

        public string? Load() => Token;

        public void Save(string token)
        {
            Token = token;
        }
    }

    public static class Wait
    {
        /// <summary>
        /// Calls `step` until `done`, a few milliseconds apart, for at most `ms`; throws if it never is. Generous, as it
        /// returns once done: a loaded machine (Unity compiling, the other tests, a pause of the collector) can hold
        /// the network's threads up a second or two, never ten.
        /// </summary>
        public static async Task Until(Func<bool> done, Action? step = null, int ms = 10_000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (true)
            {
                step?.Invoke();
                if (done()) return;
                if (DateTime.UtcNow > until) throw new TimeoutException("Wait.Until: never happened.");
                await Task.Delay(5);
            }
        }
    }
}
