using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Bastion.Net
{
    /// <summary>How far opening the game's port on the router has got (RouterPort).</summary>
    public enum RouterState
    {
        /// <summary>Looking for the router and asking it.</summary>
        Trying,

        /// <summary>Open: RouterPort.Public is where the partner joins from the internet.</summary>
        Open,

        /// <summary>No router answered, by NAT-PMP or UPnP (it has them switched off, or there is none).</summary>
        NoRouter,

        /// <summary>
        /// The router's own address isn't on the internet (CG-NAT: the line shared with others; or a router behind another
        /// one): from outside, nobody gets in. The port is taken back.
        /// </summary>
        Shared,

        /// <summary>A router answered, but wouldn't open the port.</summary>
        Refused,
    }

    /// <summary>
    /// Where RouterPort asks: the routers' NAT-PMP endpoints, and where UPnP's search (SSDP) goes and from which of this
    /// machine's addresses. OfThisMachine's, for the game; the tests give their own, on 127.0.0.1.
    /// </summary>
    public sealed class RouterPlaces
    {
        /// <summary>UPnP's search goes to this multicast group (SSDP).</summary>
        public static readonly IPEndPoint Multicast = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

        /// <summary>NAT-PMP's port on the router.</summary>
        public const int PmpPort = 5351;

        public List<IPEndPoint> Pmp { get; set; } = new List<IPEndPoint>();

        public IPEndPoint Ssdp { get; set; } = Multicast;

        public List<IPAddress> Locals { get; set; } = new List<IPAddress>();

        /// <summary>How long the routers have to answer the search.</summary>
        public int SearchMs { get; set; } = 2500;

        /// <summary>
        /// This machine's: its networks' gateways for NAT-PMP, and its local network addresses for the search (not
        /// Tailscale's: no router is there). A gateway the system doesn't say is guessed as the network's .1.
        /// </summary>
        public static RouterPlaces OfThisMachine()
        {
            var places = new RouterPlaces();
            var gateways = new List<IPAddress>();
            foreach (var nic in HostAddresses.Interfaces())
            {
                try
                {
                    foreach (var g in nic.GetIPProperties().GatewayAddresses)
                        if (g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any) && !HostAddresses.IsTailscale(g.Address)) gateways.Add(g.Address);
                }
                catch (Exception)
                {
                    // Not every system says its gateways (Mono's on some): the guess below.
                }
            }
            var main = HostAddresses.Outbound();
            if (gateways.Count == 0 && main != null && IPAddress.TryParse(main, out var at) && !HostAddresses.IsTailscale(at))
            {
                var b = at.GetAddressBytes();
                b[3] = 1;
                gateways.Add(new IPAddress(b));
            }
            places.Pmp = gateways.Distinct().Select((g) => new IPEndPoint(g, PmpPort)).ToList();
            places.Locals = HostAddresses.List().Where((a) => a.Kind == "lan").Select((a) => IPAddress.Parse(a.Address)).ToList();
            return places;
        }
    }

    /// <summary>
    /// The game's port opened on the router while this app hosts, so the partner joins from another house by the
    /// router's address (Arturo, 1/11: without a server of ours or opening ports by hand; Tailscale where it can't). It
    /// asks by NAT-PMP (Apple's routers, and miniupnpd's) and else by UPnP (IGD: SSDP, then SOAP), for the same TCP port
    /// outside as inside or the next free ones, leased for an hour and renewed at half. The router's own address says
    /// where (Public), unless it isn't on the internet itself (Shared). Nothing leaves the local network: the address is
    /// the router's word, not an outside service's. Dispose takes the port back.
    /// </summary>
    public sealed class RouterPort : IDisposable
    {
        /// <summary>How long the router keeps the port without a renewal: a host that dies leaves it open no longer.</summary>
        public const int LeaseSeconds = 3600;

        /// <summary>Each request to the router gives up after this.</summary>
        private const int HttpMs = 3000;

        /// <summary>Ports tried outside, from the game's own, when one is another machine's.</summary>
        private const int PortsTried = 10;

        /// <summary>The name the router shows for it.</summary>
        private const string Description = "Bastion";

        private static readonly string[] Gateways = { "urn:schemas-upnp-org:device:InternetGatewayDevice:1", "urn:schemas-upnp-org:device:InternetGatewayDevice:2" };

        private readonly int port;
        private readonly Action<string> log;
        private readonly RouterPlaces places;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private readonly object gate = new object();
        private Mapping? mapping;
        private volatile RouterState state = RouterState.Trying;
        private volatile string? where;

        /// <summary>How it stands.</summary>
        public RouterState State => state;

        /// <summary>Open, where the partner joins from the internet: the router's address and the port outside; null otherwise.</summary>
        public string? Public => where;

        /// <summary>State or Public changed: on a thread of its own.</summary>
        public event Action? Changed;

        private RouterPort(int port, Action<string>? log, RouterPlaces places)
        {
            this.port = port;
            this.log = log ?? ((_) => { });
            this.places = places;
            _ = Task.Run(Run);
        }

        /// <summary>Starts asking the router for TCP `port`; State and Changed say how it goes. `log`: lines for red.log.</summary>
        public static RouterPort Open(int port, Action<string>? log = null, RouterPlaces? places = null)
        {
            return new RouterPort(port, log, places ?? RouterPlaces.OfThisMachine());
        }

        /// <summary>Stops renewing and takes the port back, waiting for the router at most `ms`.</summary>
        public void Close(int ms = 1500)
        {
            if (cancel.IsCancellationRequested) return;
            cancel.Cancel();
            Mapping? m;
            lock (gate)
            {
                m = mapping;
                mapping = null;
            }
            if (m == null) return;
            try
            {
                if (m.Remove(ms).Wait(ms)) log($"[router] puerto {m.Outside} cerrado ({m.Kind})");
                else log($"[router] el router no ha contestado al cerrar el puerto {m.Outside}: se cierra solo en una hora");
            }
            catch (Exception e)
            {
                log($"[router] no se ha cerrado el puerto {m.Outside}: {Why(e)}");
            }
        }

        public void Dispose()
        {
            Close();
        }

        private void Set(RouterState now, string? at)
        {
            state = now;
            where = at;
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                log($"[router] {Why(e)}");
            }
        }

        private async Task Run()
        {
            var answered = false;
            try
            {
                var c = cancel.Token;
                Mapping? made = null;
                foreach (var router in places.Pmp)
                {
                    try
                    {
                        var (heard, m) = await PmpMapping.Make(router, port, c);
                        answered |= heard;
                        if (m != null)
                        {
                            made = m;
                            break;
                        }
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        log($"[router] NAT-PMP en {router.Address}: {Why(e)}");
                    }
                }
                if (made == null)
                {
                    List<Uri> found;
                    try
                    {
                        found = await Search(places, c);
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        log($"[router] búsqueda UPnP: {Why(e)}");
                        found = new List<Uri>();
                    }
                    foreach (var location in found)
                    {
                        var (heard, m) = await UpnpMapping.Make(location, port, log, c);
                        answered |= heard;
                        if (m != null)
                        {
                            made = m;
                            break;
                        }
                    }
                }
                if (made == null)
                {
                    log(answered ? "[router] el router no abre el puerto" : "[router] ningún router contesta por NAT-PMP ni UPnP");
                    Set(answered ? RouterState.Refused : RouterState.NoRouter, null);
                    return;
                }
                lock (gate)
                {
                    if (!c.IsCancellationRequested) mapping = made;
                }
                if (c.IsCancellationRequested)
                {
                    await made.Remove(HttpMs);
                    return;
                }
                if (!await Say(made, c)) return;
                // Renewed at half its lease (a router that only keeps them for good: its address asked again now and then).
                while (!c.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(made.Lease > 0 ? Math.Max(30, made.Lease / 2) : 600), c);
                    try
                    {
                        if (made.Lease > 0) await made.Renew(c);
                        if (!await Say(made, c)) return;
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        log($"[router] no se ha renovado el puerto {made.Outside}: {Why(e)}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                log($"[router] {Why(e)}");
                Set(answered ? RouterState.Refused : RouterState.NoRouter, null);
            }
        }

        /// <summary>
        /// The router's address said (Open); or the port taken back if it isn't on the internet (Shared) or the router
        /// doesn't say it (Refused: nobody would know where to join). Whether it goes on.
        /// </summary>
        private async Task<bool> Say(Mapping m, CancellationToken c)
        {
            var address = await m.Address(c);
            if (address == null || Shared(address))
            {
                log(address == null ? "[router] el router no dice su dirección en internet" : $"[router] el router sale a internet por {address}: CG-NAT o un router detrás de otro; desde fuera no se llega");
                lock (gate)
                {
                    if (mapping == m) mapping = null;
                }
                try
                {
                    await m.Remove(HttpMs);
                }
                catch (Exception e)
                {
                    log($"[router] no se ha cerrado el puerto {m.Outside}: {Why(e)}");
                }
                Set(address == null ? RouterState.Refused : RouterState.Shared, null);
                return false;
            }
            var at = $"{address}:{m.Outside}";
            if (state != RouterState.Open || where != at)
            {
                log($"[router] puerto abierto por {m.Kind}: {at} → este equipo, {port} (dura {(m.Lease > 0 ? $"{m.Lease} s" : "hasta cerrarlo")})");
                Set(RouterState.Open, at);
            }
            return true;
        }

        /// <summary>
        /// A router's address that isn't on the internet: a private network's, CG-NAT's (100.64.0.0/10: Tailscale's range
        /// too, but no router goes out by it), a self-assigned or an unspecified one.
        /// </summary>
        public static bool Shared(IPAddress address)
        {
            return address.AddressFamily == AddressFamily.InterNetwork && !HostAddresses.OnTheInternet(address);
        }

        private static string Why(Exception e)
        {
            return e is AggregateException a && a.InnerException != null ? Why(a.InnerException) : $"{e.GetType().Name}: {e.Message}";
        }

        // ---------------------------------------------------------- the mappings

        private abstract class Mapping
        {
            public int Outside;

            /// <summary>Seconds the router keeps it; 0, for good.</summary>
            public int Lease;

            public abstract string Kind { get; }

            public abstract Task Renew(CancellationToken c);

            /// <summary>The router's address on the internet, as it says it; null if it doesn't.</summary>
            public abstract Task<IPAddress?> Address(CancellationToken c);

            /// <summary>Takes it back, waiting at most `ms`.</summary>
            public abstract Task Remove(int ms);
        }

        /// <summary>NAT-PMP (RFC 6886): two UDP exchanges with the router, its address and the port.</summary>
        private sealed class PmpMapping : Mapping
        {
            private IPEndPoint router = null!;
            private int inside;
            private IPAddress? address;

            public override string Kind => "NAT-PMP";

            /// <summary>Whether the router answered at all, and the port opened on it (or null).</summary>
            public static async Task<(bool, Mapping?)> Make(IPEndPoint router, int inside, CancellationToken c)
            {
                var answer = await Ask(router, new byte[] { 0, 0 }, 12, c);
                if (answer == null) return (false, null);
                var m = new PmpMapping { router = router, inside = inside, Outside = inside };
                if (Result(answer) == 0) m.address = new IPAddress(answer.Skip(8).Take(4).ToArray());
                return await m.Map(LeaseSeconds, c) ? (true, m) : (true, null);
            }

            private async Task<bool> Map(int lease, CancellationToken c)
            {
                var request = new byte[12];
                request[1] = 2; // TCP
                Put16(request, 4, inside);
                Put16(request, 6, lease > 0 ? Outside : 0);
                Put32(request, 8, lease);
                var answer = await Ask(router, request, 16, c);
                if (answer == null || Result(answer) != 0) return false;
                if (lease == 0) return true;
                Outside = (answer[10] << 8) | answer[11];
                Lease = (int)Get32(answer, 12);
                return true;
            }

            public override async Task Renew(CancellationToken c)
            {
                if (!await Map(LeaseSeconds, c)) throw new IOException("el router no renueva el puerto");
                var answer = await Ask(router, new byte[] { 0, 0 }, 12, c);
                if (answer != null && Result(answer) == 0) address = new IPAddress(answer.Skip(8).Take(4).ToArray());
            }

            public override Task<IPAddress?> Address(CancellationToken c)
            {
                return Task.FromResult(address);
            }

            public override async Task Remove(int ms)
            {
                using var timeout = new CancellationTokenSource(ms);
                await Map(0, timeout.Token);
            }

            private static int Result(byte[] answer)
            {
                return (answer[2] << 8) | answer[3];
            }

            /// <summary>
            /// A request and its answer (the opcode's + 128, at least `length` bytes), sent again at 250, 500 and 1000 ms;
            /// null if none came. A router without NAT-PMP mostly says so at once (the port closed).
            /// </summary>
            private static async Task<byte[]?> Ask(IPEndPoint router, byte[] request, int length, CancellationToken c)
            {
                using var udp = new UdpClient(AddressFamily.InterNetwork);
                Task<UdpReceiveResult>? receive = null;
                try
                {
                    udp.Connect(router);
                    foreach (var wait in new[] { 250, 500, 1000 })
                    {
                        await udp.SendAsync(request, request.Length);
                        var until = Task.Delay(wait, c);
                        while (true)
                        {
                            receive ??= udp.ReceiveAsync();
                            if (await Task.WhenAny(receive, until) != receive) break;
                            var got = (await receive).Buffer;
                            receive = null;
                            if (got.Length >= length && got[0] == 0 && got[1] == request[1] + 128) return got;
                        }
                        c.ThrowIfCancellationRequested();
                    }
                    return null;
                }
                catch (SocketException)
                {
                    return null;
                }
                finally
                {
                    Forget(receive);
                }
            }
        }

        /// <summary>UPnP (IGD): the router's WANIPConnection or WANPPPConnection service, by SOAP.</summary>
        private sealed class UpnpMapping : Mapping
        {
            private Uri control = null!;
            private string service = "";
            private string inside = "";
            private int insidePort;

            public override string Kind => "UPnP";

            /// <summary>Whether a router answered at all, and the port opened on the first of its services that does it (or null).</summary>
            public static async Task<(bool, Mapping?)> Make(Uri location, int port, Action<string> log, CancellationToken c)
            {
                List<(Uri, string)> services;
                try
                {
                    var (status, text) = await Http(location, "GET", null, Array.Empty<(string, string)>(), c);
                    if (status != 200)
                    {
                        log($"[router] {location} da HTTP {status} a su descripción");
                        return (false, null);
                    }
                    services = Services(location, text);
                    if (services.Count == 0) log($"[router] {location} no describe su conexión a internet (WANIPConnection ni WANPPPConnection)");
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    log($"[router] {location} no da su descripción: {Why(e)}");
                    return (false, null);
                }
                // A router that describes its connection to the internet is there, whether or not it then opens the port.
                var heard = services.Count > 0;
                foreach (var (control, service) in services)
                {
                    var local = LocalFor(control);
                    if (local == null) continue;
                    var m = new UpnpMapping { control = control, service = service, inside = local.ToString(), insidePort = port, Lease = LeaseSeconds };
                    for (var outside = port; outside < port + PortsTried; outside++)
                    {
                        m.Outside = outside;
                        try
                        {
                            await m.Add(c);
                            return (true, m);
                        }
                        catch (UpnpFault f) when (f.Code == 725 && m.Lease > 0)
                        {
                            // OnlyPermanentLeasesSupported: kept for good, and taken back by Dispose.
                            heard = true;
                            m.Lease = 0;
                            outside--;
                        }
                        catch (UpnpFault f) when (f.Code == 718)
                        {
                            // ConflictInMappingEntry: another machine's, or this one's left by a run that died (then it's ours).
                            heard = true;
                            if (await m.Ours(c)) return (true, m);
                        }
                        catch (UpnpFault f)
                        {
                            heard = true;
                            log($"[router] {service.Split(':').ElementAtOrDefault(3)} no abre el puerto {outside}: {f.Code} {f.Message}");
                            break;
                        }
                        catch (Exception e) when (!(e is OperationCanceledException))
                        {
                            log($"[router] {control} no contesta: {Why(e)}");
                            break;
                        }
                    }
                }
                return (heard, null);
            }

            private Task Add(CancellationToken c)
            {
                return Soap(control, service, "AddPortMapping", new[]
                {
                    ("NewRemoteHost", ""), ("NewExternalPort", Outside.ToString()), ("NewProtocol", "TCP"), ("NewInternalPort", insidePort.ToString()),
                    ("NewInternalClient", inside), ("NewEnabled", "1"), ("NewPortMappingDescription", Description), ("NewLeaseDuration", Lease.ToString()),
                }, c);
            }

            /// <summary>Whether the mapping on Outside already sends to this machine's port.</summary>
            private async Task<bool> Ours(CancellationToken c)
            {
                try
                {
                    var entry = await Soap(control, service, "GetSpecificPortMappingEntry", new[] { ("NewRemoteHost", ""), ("NewExternalPort", Outside.ToString()), ("NewProtocol", "TCP") }, c);
                    return entry.TryGetValue("NewInternalClient", out var client) && client == inside && entry.TryGetValue("NewInternalPort", out var p) && p == insidePort.ToString();
                }
                catch (UpnpFault)
                {
                    return false;
                }
            }

            public override Task Renew(CancellationToken c)
            {
                return Add(c);
            }

            public override async Task<IPAddress?> Address(CancellationToken c)
            {
                try
                {
                    var answer = await Soap(control, service, "GetExternalIPAddress", Array.Empty<(string, string)>(), c);
                    return answer.TryGetValue("NewExternalIPAddress", out var text) && IPAddress.TryParse(text.Trim(), out var a) ? a : null;
                }
                catch (UpnpFault)
                {
                    return null;
                }
            }

            public override async Task Remove(int ms)
            {
                using var timeout = new CancellationTokenSource(ms);
                await Soap(control, service, "DeletePortMapping", new[] { ("NewRemoteHost", ""), ("NewExternalPort", Outside.ToString()), ("NewProtocol", "TCP") }, timeout.Token);
            }

            /// <summary>This machine's address the router sees (the route's to it): where it sends the port.</summary>
            private static IPAddress? LocalFor(Uri control)
            {
                try
                {
                    var router = IPAddress.TryParse(control.Host, out var a) ? a : Dns.GetHostAddresses(control.Host).FirstOrDefault((x) => x.AddressFamily == AddressFamily.InterNetwork);
                    if (router == null) return null;
                    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Connect(new IPEndPoint(router, control.Port));
                    return (socket.LocalEndPoint as IPEndPoint)?.Address;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>A router's SOAP fault: its UPnPError's code and description.</summary>
        private sealed class UpnpFault : Exception
        {
            public readonly int Code;

            public UpnpFault(int code, string description) : base(description)
            {
                Code = code;
            }
        }

        // ------------------------------------------------------- UPnP's protocol

        /// <summary>
        /// The routers' descriptions (LOCATION) that answer UPnP's search for an Internet Gateway Device, from each of the
        /// local addresses, within SearchMs. Only http, as the protocol has it.
        /// </summary>
        internal static async Task<List<Uri>> Search(RouterPlaces places, CancellationToken c)
        {
            var found = new List<Uri>();
            var clients = new List<UdpClient>();
            try
            {
                foreach (var local in places.Locals)
                {
                    try
                    {
                        var udp = new UdpClient(new IPEndPoint(local, 0));
                        try
                        {
                            udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                            udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
                        }
                        catch (SocketException)
                        {
                            // The system's choice of interface, then.
                        }
                        clients.Add(udp);
                    }
                    catch (SocketException)
                    {
                    }
                }
                foreach (var udp in clients)
                    foreach (var target in Gateways)
                    {
                        var search = Encoding.ASCII.GetBytes($"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: {target}\r\n\r\n");
                        try
                        {
                            await udp.SendAsync(search, search.Length, places.Ssdp);
                        }
                        catch (SocketException)
                        {
                        }
                    }
                var until = Task.Delay(places.SearchMs, c);
                var pending = clients.ToDictionary((u) => u, (u) => Receive(u));
                while (pending.Count > 0)
                {
                    var done = await Task.WhenAny(pending.Values.Cast<Task>().Append(until));
                    if (done == until) break;
                    var udp = pending.First((p) => p.Value == done).Key;
                    var got = await pending[udp];
                    if (got == null)
                    {
                        pending.Remove(udp);
                        continue;
                    }
                    pending[udp] = Receive(udp);
                    var location = Header(Encoding.ASCII.GetString(got), "LOCATION");
                    if (location != null && Uri.TryCreate(location.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "http" && !found.Contains(uri)) found.Add(uri);
                    // The answers that came, once the first router has had a moment for its other services.
                    if (found.Count > 0 && !until.IsCompleted) until = Task.WhenAny(until, Task.Delay(300, c)).Unwrap();
                }
                foreach (var p in pending.Values) Forget(p);
                c.ThrowIfCancellationRequested();
                return found;
            }
            finally
            {
                foreach (var udp in clients) udp.Dispose();
            }
        }

        private static async Task<byte[]?> Receive(UdpClient udp)
        {
            try
            {
                return (await udp.ReceiveAsync()).Buffer;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A header's value in an HTTP-like message (any case), or null.</summary>
        internal static string? Header(string message, string name)
        {
            foreach (var line in message.Split('\n'))
            {
                var colon = line.IndexOf(':');
                if (colon > 0 && string.Equals(line.Substring(0, colon).Trim(), name, StringComparison.OrdinalIgnoreCase)) return line.Substring(colon + 1).Trim();
            }
            return null;
        }

        /// <summary>A device description's WAN connection services, IP's before PPP's: where to call each, and its type.</summary>
        internal static List<(Uri Control, string Service)> Services(Uri location, string description)
        {
            var doc = Xml(description);
            var root = location;
            var urlBase = doc.GetElementsByTagName("*").Cast<XmlElement>().FirstOrDefault((e) => e.LocalName == "URLBase")?.InnerText.Trim();
            if (!string.IsNullOrEmpty(urlBase) && Uri.TryCreate(urlBase, UriKind.Absolute, out var b)) root = b;
            var found = new List<(Uri, string)>();
            foreach (var e in doc.GetElementsByTagName("*").Cast<XmlElement>().Where((e) => e.LocalName == "service"))
            {
                var type = Child(e, "serviceType");
                var control = Child(e, "controlURL");
                if (type == null || control == null || !(type.Contains(":WANIPConnection:") || type.Contains(":WANPPPConnection:"))) continue;
                if (Uri.TryCreate(root, control, out var at) && at.Scheme == "http") found.Add((at, type));
            }
            return found.OrderBy((s) => s.Item2.Contains(":WANPPPConnection:")).ToList();
        }

        private static string? Child(XmlElement e, string name)
        {
            return e.ChildNodes.OfType<XmlElement>().FirstOrDefault((x) => x.LocalName == name)?.InnerText.Trim();
        }

        /// <summary>The router's XML, without its DTD or anything it would fetch.</summary>
        private static XmlDocument Xml(string text)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            doc.Load(reader);
            return doc;
        }

        /// <summary>A SOAP action on a router's service: its answer's arguments by name, or its fault (UpnpFault).</summary>
        private static async Task<Dictionary<string, string>> Soap(Uri control, string service, string action, (string Name, string Value)[] arguments, CancellationToken c)
        {
            var body = new StringBuilder();
            body.Append("<?xml version=\"1.0\"?>\r\n<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>");
            body.Append($"<u:{action} xmlns:u=\"{service}\">");
            foreach (var (name, value) in arguments) body.Append($"<{name}>{Escape(value)}</{name}>");
            body.Append($"</u:{action}></s:Body></s:Envelope>\r\n");
            var (status, text) = await Http(control, "POST", body.ToString(), new[] { ("Content-Type", "text/xml; charset=\"utf-8\""), ("SOAPAction", $"\"{service}#{action}\"") }, c);
            XmlDocument doc;
            try
            {
                doc = Xml(text);
            }
            catch (XmlException)
            {
                throw new UpnpFault(status, $"HTTP {status}, sin XML");
            }
            var all = doc.GetElementsByTagName("*").Cast<XmlElement>().ToList();
            if (status != 200)
            {
                var code = all.FirstOrDefault((e) => e.LocalName == "errorCode")?.InnerText.Trim();
                var description = all.FirstOrDefault((e) => e.LocalName == "errorDescription")?.InnerText.Trim();
                throw new UpnpFault(int.TryParse(code, out var n) ? n : status, description ?? $"HTTP {status}");
            }
            var answer = all.FirstOrDefault((e) => e.LocalName == action + "Response");
            if (answer == null) throw new UpnpFault(status, $"sin {action}Response");
            return answer.ChildNodes.OfType<XmlElement>().ToDictionary((e) => e.LocalName, (e) => e.InnerText);
        }

        private static string Escape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        /// <summary>
        /// An HTTP/1.1 request to the router, with `body` (UTF-8) if any: the status and the body. By hand on a socket: the
        /// router is next door, and HttpClient would go through the system's proxy, if it has one. At most 256 KB, within
        /// HttpMs.
        /// </summary>
        private static async Task<(int Status, string Body)> Http(Uri uri, string method, string? body, (string Name, string Value)[] headers, CancellationToken c)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(c);
            timeout.CancelAfter(HttpMs);
            using var client = new TcpClient(AddressFamily.InterNetwork);
            using var stop = timeout.Token.Register(() => client.Close());
            try
            {
                await client.ConnectAsync(uri.Host, uri.Port);
                var stream = client.GetStream();
                var content = body != null ? Encoding.UTF8.GetBytes(body) : Array.Empty<byte>();
                var request = new StringBuilder($"{method} {uri.PathAndQuery} HTTP/1.1\r\nHost: {uri.Host}:{uri.Port}\r\nConnection: close\r\n");
                foreach (var (name, value) in headers) request.Append($"{name}: {value}\r\n");
                if (body != null) request.Append($"Content-Length: {content.Length}\r\n");
                request.Append("\r\n");
                var head = Encoding.ASCII.GetBytes(request.ToString());
                await stream.WriteAsync(head, 0, head.Length);
                if (content.Length > 0) await stream.WriteAsync(content, 0, content.Length);
                var got = new MemoryStream();
                var buffer = new byte[8192];
                while (true)
                {
                    var n = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (n == 0) break;
                    got.Write(buffer, 0, n);
                    if (got.Length > 256 * 1024) throw new IOException("respuesta de más de 256 KB");
                    if (Complete(got.ToArray()) != null) break;
                }
                return Complete(got.ToArray(), true) ?? throw new IOException("respuesta sin cabecera");
            }
            catch (Exception) when (timeout.IsCancellationRequested)
            {
                c.ThrowIfCancellationRequested();
                throw new TimeoutException($"{uri} no contesta en {HttpMs / 1000} s");
            }
        }

        /// <summary>
        /// The status and body of a response read so far, if it is whole (by Content-Length, or chunked to its last chunk;
        /// or, `ended`, whatever came before the connection closed); null if not yet.
        /// </summary>
        private static (int, string)? Complete(byte[] bytes, bool ended = false)
        {
            var text = Encoding.UTF8.GetString(bytes);
            var split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (split < 0) return null;
            var head = text.Substring(0, split);
            var status = head.Split(' ').Length > 1 && int.TryParse(head.Split(' ')[1], out var s) ? s : 0;
            var rest = bytes.Skip(Encoding.UTF8.GetByteCount(text.Substring(0, split + 4))).ToArray();
            if (string.Equals(Header(head, "Transfer-Encoding"), "chunked", StringComparison.OrdinalIgnoreCase))
            {
                var whole = new MemoryStream();
                var at = 0;
                while (true)
                {
                    var line = IndexOf(rest, at);
                    if (line < 0) return ended ? (status, Encoding.UTF8.GetString(whole.ToArray())) : ((int, string)?)null;
                    var size = Convert.ToInt32(Encoding.ASCII.GetString(rest, at, line - at).Split(';')[0].Trim(), 16);
                    if (size == 0) return (status, Encoding.UTF8.GetString(whole.ToArray()));
                    if (line + 2 + size > rest.Length) return ended ? (status, Encoding.UTF8.GetString(whole.ToArray())) : ((int, string)?)null;
                    whole.Write(rest, line + 2, size);
                    at = line + 2 + size + 2;
                }
            }
            if (int.TryParse(Header(head, "Content-Length"), out var length))
                return rest.Length >= length ? (status, Encoding.UTF8.GetString(rest, 0, length)) : ended ? (status, Encoding.UTF8.GetString(rest)) : ((int, string)?)null;
            return ended ? (status, Encoding.UTF8.GetString(rest)) : ((int, string)?)null;
        }

        private static int IndexOf(byte[] bytes, int from)
        {
            for (var i = from; i + 1 < bytes.Length; i++)
                if (bytes[i] == '\r' && bytes[i + 1] == '\n') return i;
            return -1;
        }

        private static void Put16(byte[] b, int at, int value)
        {
            b[at] = (byte)(value >> 8);
            b[at + 1] = (byte)value;
        }

        private static void Put32(byte[] b, int at, int value)
        {
            b[at] = (byte)(value >> 24);
            b[at + 1] = (byte)(value >> 16);
            b[at + 2] = (byte)(value >> 8);
            b[at + 3] = (byte)value;
        }

        private static uint Get32(byte[] b, int at)
        {
            return (uint)((b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3]);
        }

        /// <summary>A receive left waiting when its socket closes: its error, seen, so it isn't reported as unobserved.</summary>
        private static void Forget(Task? task)
        {
            task?.ContinueWith((t) => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
