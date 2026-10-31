using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Net;

namespace Bastion.Relay
{
    /// <summary>The relay's settings: where it listens, its key and certificate, and its limits (docs/UNIRSE-CON-CODIGO.md).</summary>
    public sealed class RelayOptions
    {
        public IPAddress Address { get; set; } = IPAddress.Any;
        public int Port { get; set; } = 443;

        /// <summary>What signs the codes (HMAC-SHA256): a host's signature proves the code its own, even to the relay restarted since.</summary>
        public byte[] Key { get; set; } = RandomNumberGenerator.GetBytes(32);

        /// <summary>TLS's, with the key the apps carry (RelayAddress.Keys); none, no TLS (the local tests').</summary>
        public X509Certificate2? Certificate { get; set; }

        /// <summary>For tests: every piece copied waits this long, as on a far network.</summary>
        public int DelayMs { get; set; }

        /// <summary>A code lasts this long at most from when it was given, its host linked or not.</summary>
        public long CodeLifeMs { get; set; } = 24 * 3600_000L;

        /// <summary>A connection says what it comes for in this time (TLS included), or it is closed.</summary>
        public int GreetMs { get; set; } = 5000;

        /// <summary>The host opens a joiner's pipe in this time, or the joiner hears Timeout.</summary>
        public int PipeWaitMs { get; set; } = 10_000;

        /// <summary>A host's link heard nothing for this long (its pings every 20 s): gone.</summary>
        public int LinkDeadMs { get; set; } = 60_000;

        /// <summary>
        /// Just restarted, a code it doesn't know may be one whose host hasn't come back yet: Away instead of Unknown, and
        /// not held against whoever asked.
        /// </summary>
        public long RestartGraceMs { get; set; } = 2 * 60_000L;

        public int CodesPerIp { get; set; } = 3;
        public int OpenPerIp { get; set; } = 10;
        public int NewPerIpPerMinute { get; set; } = 30;

        /// <summary>This many codes that aren't one in FailWindowMs, and that address can't join for BlockMs.</summary>
        public int FailsPerIp { get; set; } = 10;
        public long FailWindowMs { get; set; } = 10 * 60_000L;
        public long BlockMs { get; set; } = 30 * 60_000L;

        public int MaxCodes { get; set; } = 200;
        public int MaxOpen { get; set; } = 1000;
        public int PipesPerCode { get; set; } = 4;

        /// <summary>A game's pipes together: no faster (they wait), and no more a day (they are cut): nobody's free tunnel.</summary>
        public long BytesPerSecond { get; set; } = 2L * 1024 * 1024;
        public long BytesPerDay { get; set; } = 1024L * 1024 * 1024;

        /// <summary>The wall clock in ms: codes are signed with when they were given, which a restart must still read.</summary>
        public Func<long> Now { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// The relay of «Unirse con código» (docs/UNIRSE-CON-CODIGO.md): it gives each host a code, and joins each player who brings
    /// it to a pipe the host opens for them; then it only copies bytes, which it doesn't read (the game's Frame, its
    /// heartbeat and the hello with the version go through as they are). It keeps nothing on disk: its codes are in
    /// memory, and a host proves its own by the signature the relay gave it. Its log never has what is played, nor
    /// whole addresses (they are personal data): an address goes in as a short hash.
    /// </summary>
    public sealed class RelayServer : IDisposable
    {
        private readonly RelayOptions o;
        private readonly Action<string> log;
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private readonly object gate = new object();
        private readonly Dictionary<string, Hosted> codes = new Dictionary<string, Hosted>();
        private readonly Dictionary<string, Waiting> tickets = new Dictionary<string, Waiting>();
        private readonly Dictionary<IPAddress, Visitor> visitors = new Dictionary<IPAddress, Visitor>();
        private long startedAt;
        private int open;

        /// <summary>The port it listens on (the one asked for, or the one the system gave for 0).</summary>
        public int Port { get; private set; }

        public RelayServer(RelayOptions options, Action<string>? log = null)
        {
            o = options;
            this.log = log ?? Console.WriteLine;
            listener = new TcpListener(o.Address, o.Port);
        }

        public void Start()
        {
            startedAt = o.Now();
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = Task.Run(AcceptLoop);
            _ = Task.Run(SweepLoop);
            log($"escucha en {o.Address}:{Port}{(o.Certificate != null ? ", con TLS" : ", sin TLS")}");
        }

        public void Dispose()
        {
            cancel.Cancel();
            listener.Stop();
            lock (gate)
            {
                foreach (var h in codes.Values) h.Link?.Close();
                foreach (var w in tickets.Values) w.Pipe.TrySetCanceled();
            }
        }

        /// <summary>The codes given and still alive, for tests and the log.</summary>
        public IReadOnlyList<string> Codes
        {
            get
            {
                lock (gate) return codes.Keys.ToList();
            }
        }

        /// <summary>For tests: `code`'s host link cut, as a dropped Wi-Fi would; its pipes stay.</summary>
        public void CutLink(string code)
        {
            lock (gate)
                if (codes.TryGetValue(code, out var h)) h.Link?.Close();
        }

        /// <summary>For tests: `code`'s pipes cut, as a joiner's dropped Wi-Fi would.</summary>
        public void CutPipes(string code)
        {
            lock (gate)
                if (codes.TryGetValue(code, out var h))
                    foreach (var c in h.Open.ToList()) c.Close();
        }

        // ---------------------------------------------------------- connections

        private async Task AcceptLoop()
        {
            while (!cancel.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancel.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    if (cancel.IsCancellationRequested) return;
                    continue;
                }
                var ip = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
                if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
                var now = o.Now();
                bool let;
                lock (gate)
                {
                    var v = VisitorOf(ip);
                    v.Arrivals.RemoveAll((t) => now - t > 60_000);
                    let = open < o.MaxOpen && v.Open < o.OpenPerIp && v.Arrivals.Count < o.NewPerIpPerMinute;
                    v.Arrivals.Add(now);
                    if (let)
                    {
                        open++;
                        v.Open++;
                    }
                }
                if (!let)
                {
                    // Too many from here (or in all): not even read.
                    client.Close();
                    continue;
                }
                _ = Task.Run(() => Serve(client, ip));
            }
        }

        private async Task Serve(TcpClient client, IPAddress ip)
        {
            try
            {
                client.NoDelay = true;
                Stream stream = client.GetStream();
                string? line;
                // What it comes for, TLS included, in GreetMs: a socket opened and left silent would hold a place.
                using (var greet = new CancellationTokenSource(o.GreetMs))
                using (greet.Token.Register(() => client.Close()))
                {
                    if (o.Certificate != null)
                    {
                        var tls = new SslStream(stream, false);
                        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = o.Certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, cancel.Token).ConfigureAwait(false);
                        stream = tls;
                    }
                    line = await RelayWire.ReadLineAsync(stream, cancel.Token).ConfigureAwait(false);
                    if (greet.IsCancellationRequested) return;
                }
                if (line == null) return;
                var words = line.Split(' ');
                if (words.Length < 3 || words[0] != RelayWire.Magic)
                {
                    await Refuse(stream, RelayRefusal.Bad).ConfigureAwait(false);
                    return;
                }
                if (words[1] != RelayWire.Version.ToString())
                {
                    await Refuse(stream, RelayRefusal.Version).ConfigureAwait(false);
                    return;
                }
                switch (words[2])
                {
                    case RelayWire.Host:
                        await Hosting(client, stream, ip, words).ConfigureAwait(false);
                        break;
                    case RelayWire.Join:
                        await Joining(client, stream, ip, words).ConfigureAwait(false);
                        break;
                    case RelayWire.Pipe:
                        await Piping(client, stream, words).ConfigureAwait(false);
                        break;
                    default:
                        await Refuse(stream, RelayRefusal.Bad).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception)
            {
                /* closed, silent, broken or not to be trusted: it goes */
            }
            finally
            {
                client.Close();
                lock (gate)
                {
                    open--;
                    VisitorOf(ip).Open--;
                }
            }
        }

        private static async Task Refuse(Stream stream, string why)
        {
            using var late = new CancellationTokenSource(2000);
            await RelayWire.WriteLineAsync(stream, $"{RelayWire.Err} {why}", late.Token).ConfigureAwait(false);
        }

        // ---------------------------------------------------------------- hosts

        /// <summary>
        /// A host's link: a new code («HOST»), or its own back («HOST code issued signature»); then its PINGs answered, the
        /// PEERs sent down it, and BYE the code's end. Dropped, the code waits for it, Away to joiners, till it runs out.
        /// </summary>
        private async Task Hosting(TcpClient client, Stream stream, IPAddress ip, string[] words)
        {
            var now = o.Now();
            Hosted? h = null;
            string? refusal = null;
            var back = words.Length == 6;
            lock (gate)
            {
                if (back)
                {
                    var code = RelayWire.Normalize(words[3]);
                    if (code == null || !long.TryParse(words[4], out var issued) || !Signed(code, issued, words[5])) refusal = RelayRefusal.Bad;
                    else if (now - issued > o.CodeLifeMs) refusal = RelayRefusal.Unknown;
                    else if (codes.TryGetValue(code, out h))
                        // A link still open (not yet found dead) gives way to the new one, as a seat opened elsewhere does.
                        h.Link?.Close();
                    else if (codes.Count >= o.MaxCodes && !DropStale(null)) refusal = RelayRefusal.Full;
                    else codes[code] = h = new Hosted(code, issued, ip);
                }
                else if (words.Length == 3)
                {
                    // An address with its codes all used gives up its oldest unlinked one (an app that crashed, quit
                    // without BYE); one with all of them linked is refused.
                    if (codes.Values.Count((x) => x.Ip.Equals(ip)) >= o.CodesPerIp && !DropStale(ip)) refusal = RelayRefusal.Limit;
                    else if (codes.Count >= o.MaxCodes && !DropStale(null)) refusal = RelayRefusal.Full;
                    else
                    {
                        string code;
                        do code = NewCode();
                        while (codes.ContainsKey(code));
                        codes[code] = h = new Hosted(code, now, ip);
                    }
                }
                else refusal = RelayRefusal.Bad;
                if (h != null)
                {
                    h.Link = client;
                    h.LinkStream = stream;
                    h.Writing = new SemaphoreSlim(1, 1);
                    h.LinkLostAt = null;
                    h.Ip = ip;
                }
            }
            if (h == null)
            {
                log($"anfitrión {Who(ip)}: no ({refusal})");
                await Refuse(stream, refusal!).ConfigureAwait(false);
                return;
            }
            var writing = h.Writing!;
            int count;
            lock (gate) count = codes.Count;
            await Write(stream, writing, $"{RelayWire.Ok} {h.Code} {h.Issued} {Sign(h.Code, h.Issued)}").ConfigureAwait(false);
            log($"anfitrión {Who(ip)}: {h.Code} {(back ? "de vuelta" : "nuevo")} (códigos: {count})");
            var heardAt = Environment.TickCount64;
            var bye = false;
            using var watch = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!watch.IsCancellationRequested)
                    {
                        await Task.Delay(o.LinkDeadMs / 4, watch.Token).ConfigureAwait(false);
                        if (Environment.TickCount64 - Interlocked.Read(ref heardAt) > o.LinkDeadMs) client.Close();
                    }
                }
                catch (OperationCanceledException)
                {
                    /* the link ended */
                }
            });
            try
            {
                while (true)
                {
                    var line = await RelayWire.ReadLineAsync(stream, cancel.Token).ConfigureAwait(false);
                    if (line == null) break;
                    Interlocked.Exchange(ref heardAt, Environment.TickCount64);
                    if (line == RelayWire.Ping) await Write(stream, writing, RelayWire.Pong).ConfigureAwait(false);
                    else if (line == RelayWire.Bye)
                    {
                        bye = true;
                        break;
                    }
                }
            }
            catch (Exception)
            {
                /* the link dropped */
            }
            finally
            {
                watch.Cancel();
                lock (gate)
                {
                    if (h.Link == client)
                    {
                        h.Link = null;
                        h.LinkStream = null;
                        h.LinkLostAt = o.Now();
                        if (bye) codes.Remove(h.Code);
                    }
                }
                log($"anfitrión {Who(ip)}: {h.Code} {(bye ? "cerrado" : "sin enlace")}");
            }
        }

        /// <summary>An unlinked code dropped, the oldest (of `ip`'s, if given): true if one was.</summary>
        private bool DropStale(IPAddress? ip)
        {
            var stale = codes.Values.Where((x) => x.Link == null && (ip == null || x.Ip.Equals(ip))).OrderBy((x) => x.LinkLostAt ?? x.Issued).FirstOrDefault();
            if (stale == null) return false;
            codes.Remove(stale.Code);
            return true;
        }

        private static async Task Write(Stream stream, SemaphoreSlim writing, string line)
        {
            using var late = new CancellationTokenSource(5000);
            await writing.WaitAsync(late.Token).ConfigureAwait(false);
            try
            {
                await RelayWire.WriteLineAsync(stream, line, late.Token).ConfigureAwait(false);
            }
            finally
            {
                writing.Release();
            }
        }

        // -------------------------------------------------------------- joiners

        /// <summary>A joiner with a code: its host told (PEER), the pipe it opens awaited, both told OK, and the two joined.</summary>
        private async Task Joining(TcpClient client, Stream stream, IPAddress ip, string[] words)
        {
            var now = o.Now();
            var code = words.Length == 4 ? RelayWire.Normalize(words[3]) : null;
            string? refusal = null;
            Hosted? h = null;
            string ticket = "";
            Waiting? wait = null;
            Stream? link = null;
            SemaphoreSlim? writing = null;
            lock (gate)
            {
                var v = VisitorOf(ip);
                v.Fails.RemoveAll((t) => now - t > o.FailWindowMs);
                if (v.BlockedUntil > now) refusal = RelayRefusal.Limit;
                else if (code == null || !codes.TryGetValue(code, out h))
                {
                    if (code != null && now - startedAt < o.RestartGraceMs) refusal = RelayRefusal.Away;
                    else
                    {
                        refusal = RelayRefusal.Unknown;
                        v.Fails.Add(now);
                        if (v.Fails.Count >= o.FailsPerIp) v.BlockedUntil = now + o.BlockMs;
                    }
                }
                else if (h.Link == null) refusal = RelayRefusal.Away;
                else if (h.Pipes >= o.PipesPerCode) refusal = RelayRefusal.Full;
                else if (h.SpentToday(now) >= o.BytesPerDay) refusal = RelayRefusal.Limit;
                else
                {
                    ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                    tickets[ticket] = wait = new Waiting();
                    h.Pipes++;
                    link = h.LinkStream;
                    writing = h.Writing;
                }
            }
            if (wait == null)
            {
                log($"se une {Who(ip)} a {(code ?? "?")}: no ({refusal})");
                await Refuse(stream, refusal!).ConfigureAwait(false);
                return;
            }
            try
            {
                try
                {
                    await Write(link!, writing!, $"{RelayWire.Peer} {ticket} {ip}").ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Its link died just now (the relay finds out on its read): as if it were away.
                    await Refuse(stream, RelayRefusal.Away).ConfigureAwait(false);
                    return;
                }
                var done = await Task.WhenAny(wait.Pipe.Task, Task.Delay(o.PipeWaitMs, cancel.Token)).ConfigureAwait(false);
                if (done != wait.Pipe.Task)
                {
                    log($"se une {Who(ip)} a {code}: el anfitrión no abre el empalme");
                    await Refuse(stream, RelayRefusal.Timeout).ConfigureAwait(false);
                    return;
                }
                var pipe = await wait.Pipe.Task.ConfigureAwait(false);
                using (var late = new CancellationTokenSource(5000))
                {
                    await RelayWire.WriteLineAsync(pipe.Stream, RelayWire.Ok, late.Token).ConfigureAwait(false);
                    await RelayWire.WriteLineAsync(stream, RelayWire.Ok, late.Token).ConfigureAwait(false);
                }
                log($"se une {Who(ip)} a {code}");
                await Splice(client, stream, pipe.Client, pipe.Stream, h!).ConfigureAwait(false);
                log($"se va {Who(ip)} de {code}");
            }
            finally
            {
                lock (gate)
                {
                    tickets.Remove(ticket);
                    h!.Pipes--;
                }
                wait.Pipe.TrySetCanceled();
                wait.Done.TrySetResult(true);
            }
        }

        /// <summary>The host's pipe for a joiner, by its ticket: handed to the joiner's side, which joins them and ends it.</summary>
        private async Task Piping(TcpClient client, Stream stream, string[] words)
        {
            Waiting? wait;
            lock (gate) tickets.TryGetValue(words.Length == 4 ? words[3] : "", out wait);
            if (wait == null || !wait.Pipe.TrySetResult(new Pipe(client, stream)))
            {
                await Refuse(stream, RelayRefusal.Unknown).ConfigureAwait(false);
                return;
            }
            await wait.Done.Task.ConfigureAwait(false);
        }

        /// <summary>Bytes both ways till either end closes; then both are closed.</summary>
        private async Task Splice(TcpClient a, Stream aStream, TcpClient b, Stream bStream, Hosted h)
        {
            lock (gate)
            {
                h.Open.Add(a);
                h.Open.Add(b);
            }
            try
            {
                var one = Copy(aStream, bStream, h);
                var two = Copy(bStream, aStream, h);
                await Task.WhenAny(one, two).ConfigureAwait(false);
                a.Close();
                b.Close();
                try
                {
                    await Task.WhenAll(one, two).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    /* the other way, cut */
                }
            }
            finally
            {
                lock (gate)
                {
                    h.Open.Remove(a);
                    h.Open.Remove(b);
                }
            }
        }

        private async Task Copy(Stream from, Stream to, Hosted h)
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (true)
                {
                    var read = await from.ReadAsync(buffer.AsMemory(), cancel.Token).ConfigureAwait(false);
                    if (read == 0) return;
                    var wait = h.Spend(read, o.Now(), o.BytesPerSecond, o.BytesPerDay);
                    if (wait < 0)
                    {
                        log($"{h.Code}: gastado lo de hoy ({o.BytesPerDay / (1024 * 1024)} MB)");
                        return;
                    }
                    if (wait > 0) await Task.Delay((int)wait, cancel.Token).ConfigureAwait(false);
                    if (o.DelayMs > 0) await Task.Delay(o.DelayMs, cancel.Token).ConfigureAwait(false);
                    await to.WriteAsync(buffer.AsMemory(0, read), cancel.Token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                /* an end closed */
            }
        }

        // ---------------------------------------------------------------- upkeep

        /// <summary>Every minute: codes run out (their links closed: their hosts ask for new ones), and addresses not seen in an hour forgotten.</summary>
        private async Task SweepLoop()
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(60_000, cancel.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                Sweep();
            }
        }

        /// <summary>One sweep (public for tests, with a clock of their own).</summary>
        public void Sweep()
        {
            var now = o.Now();
            lock (gate)
            {
                foreach (var h in codes.Values.Where((x) => now - x.Issued > o.CodeLifeMs).ToList())
                {
                    codes.Remove(h.Code);
                    h.Link?.Close();
                    log($"{h.Code}: caducado");
                }
                foreach (var ip in visitors.Where((p) => p.Value.Open == 0 && p.Value.BlockedUntil < now && p.Value.Fails.All((t) => now - t > o.FailWindowMs) && p.Value.Arrivals.All((t) => now - t > 60_000)).Select((p) => p.Key).ToList())
                    visitors.Remove(ip);
            }
        }

        private Visitor VisitorOf(IPAddress ip)
        {
            if (!visitors.TryGetValue(ip, out var v)) visitors[ip] = v = new Visitor();
            return v;
        }

        private static string NewCode()
        {
            return new string(Enumerable.Range(0, RelayWire.CodeLength).Select((_) => RelayWire.Alphabet[RandomNumberGenerator.GetInt32(RelayWire.Alphabet.Length)]).ToArray());
        }

        private string Sign(string code, long issued)
        {
            using var hmac = new HMACSHA256(o.Key);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.ASCII.GetBytes($"{code} {issued}")), 0, 16).ToLowerInvariant();
        }

        private bool Signed(string code, long issued, string signature)
        {
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(code, issued)), Encoding.ASCII.GetBytes(signature));
        }

        /// <summary>An address for the log: a short hash, the same for the same address while the key is (personal data stays out).</summary>
        private string Who(IPAddress ip)
        {
            using var hmac = new HMACSHA256(o.Key);
            return "ip-" + Convert.ToHexString(hmac.ComputeHash(ip.GetAddressBytes()), 0, 4).ToLowerInvariant();
        }

        // ----------------------------------------------------------------- state

        private sealed class Hosted
        {
            public readonly string Code;
            public readonly long Issued;
            public IPAddress Ip;
            public TcpClient? Link;
            public Stream? LinkStream;
            public SemaphoreSlim? Writing;
            public long? LinkLostAt;
            public int Pipes;
            public readonly HashSet<TcpClient> Open = new HashSet<TcpClient>();
            private long day;
            private long spent;
            private double credit;
            private long creditAt;

            public Hosted(string code, long issued, IPAddress ip)
            {
                Code = code;
                Issued = issued;
                Ip = ip;
            }

            public long SpentToday(long now)
            {
                lock (this) return now / 86_400_000 == day ? spent : 0;
            }

            /// <summary>`bytes` more through its pipes: how long to wait first to keep under `perSecond`, or -1 past the day's `perDay`.</summary>
            public long Spend(int bytes, long now, long perSecond, long perDay)
            {
                lock (this)
                {
                    if (now / 86_400_000 != day)
                    {
                        day = now / 86_400_000;
                        spent = 0;
                    }
                    spent += bytes;
                    if (spent > perDay) return -1;
                    credit = Math.Min(perSecond, credit + (now - creditAt) * perSecond / 1000.0) - bytes;
                    creditAt = now;
                    return credit >= 0 ? 0 : (long)Math.Ceiling(-credit * 1000 / perSecond);
                }
            }
        }

        private sealed class Visitor
        {
            public int Open;
            public readonly List<long> Arrivals = new List<long>();
            public readonly List<long> Fails = new List<long>();
            public long BlockedUntil;
        }

        private sealed class Waiting
        {
            public readonly TaskCompletionSource<Pipe> Pipe = new TaskCompletionSource<Pipe>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed class Pipe
        {
            public readonly TcpClient Client;
            public readonly Stream Stream;

            public Pipe(TcpClient client, Stream stream)
            {
                Client = client;
                Stream = stream;
            }
        }
    }
}
