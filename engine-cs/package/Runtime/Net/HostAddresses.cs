using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Bastion.Net
{
    /// <summary>A network address of the host: same local network, or Tailscale (100.64.0.0/10).</summary>
    public sealed class HostAddress
    {
        public string Address { get; set; } = "";

        /// <summary>"lan" or "tailscale".</summary>
        public string Kind { get; set; } = "lan";
    }

    /// <summary>
    /// The host's addresses a partner can reach, for the menu to show them (server index.ts hostAddresses). The default
    /// route's goes first: of the local network ones, the one a partner on the same network most likely reaches.
    /// </summary>
    public static class HostAddresses
    {
        /// <summary>
        /// Virtual adapters (virtual machines, containers, WSL): no partner is on the other side. Windows names them in
        /// words; macOS and Linux by prefix. On macOS, bridge100 and up are made for virtual machines and containers
        /// (OrbStack, Docker); bridge0 is the Thunderbolt bridge, a real cable, and stays. So does utun, which is Tailscale.
        /// </summary>
        private static readonly Regex Virtual = new Regex(@"vEthernet|Hyper-V|WSL|VirtualBox|VMware|^bridge1\d\d|^vmnet|^vboxnet|^docker|^veth|^br-|^virbr", RegexOptions.IgnoreCase);

        /// <summary>IPv4 addresses a partner can reach, the default route's first.</summary>
        public static List<HostAddress> List()
        {
            string? main = Outbound();
            var found = new List<HostAddress>();
            foreach (var nic in Interfaces())
            {
                foreach (var a in nic.GetIPProperties().UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(a.Address)) continue;
                    string address = a.Address.ToString();
                    if (found.Any((h) => h.Address == address)) continue;
                    found.Add(new HostAddress { Address = address, Kind = IsTailscale(a.Address) ? "tailscale" : "lan" });
                }
            }
            return found.OrderByDescending((h) => h.Address == main).ToList();
        }

        /// <summary>The network interfaces that are up and not virtual (by name or description).</summary>
        internal static IEnumerable<NetworkInterface> Interfaces()
        {
            NetworkInterface[] all;
            try
            {
                all = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (NetworkInformationException)
            {
                return Array.Empty<NetworkInterface>();
            }
            return all.Where((n) => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && !Virtual.IsMatch(n.Name) && !Virtual.IsMatch(n.Description));
        }

        /// <summary>Tailscale's range, 100.64.0.0/10.</summary>
        public static bool IsTailscale(IPAddress address)
        {
            var b = address.GetAddressBytes();
            return b.Length == 4 && b[0] == 100 && b[1] >= 64 && b[1] < 128;
        }

        /// <summary>
        /// An IPv4 address on the internet: not a private network's, nor CG-NAT's or Tailscale's (100.64.0.0/10), nor this
        /// machine's, a self-assigned or an unspecified one.
        /// </summary>
        public static bool OnTheInternet(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            var b = address.GetAddressBytes();
            if (b.Length != 4) return false;
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || IsTailscale(address) || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] >= 16 && b[1] < 32) || (b[0] == 192 && b[1] == 168));
        }

        /// <summary>Whether a connection's peer (address:port, as IConnection.Peer says it) comes from the internet: through a port the router opened.</summary>
        public static bool FromTheInternet(string peer)
        {
            var at = peer.LastIndexOf(':');
            var host = (at > 0 ? peer.Substring(0, at) : peer).Trim('[', ']');
            return IPAddress.TryParse(host, out var address) && OnTheInternet(address);
        }

        /// <summary>
        /// The address this machine goes out from (its default route), or null without one. A UDP socket that only
        /// connects sends nothing: the system just picks the route. TEST-NET-1 (RFC 5737): nobody there, but routed like
        /// any outside address.
        /// </summary>
        public static string? Outbound()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 9));
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
            }
            catch (SocketException)
            {
                return null;
            }
        }
    }
}
