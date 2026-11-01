using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Bastion.Net
{
    /// <summary>
    /// Where a player's app connects: an address as it is typed or pasted (desktop main.ts normalise). «192.168.1.20»,
    /// «192.168.1.20:41392», «100.116.6.72» (Tailscale) or a name; with no port, the host's first one. From the internet,
    /// with the room's key after it, as the host's menu gives it to copy: «83.45.12.7:41391 · KXRT5» (Room.Key).
    /// </summary>
    public static class GameAddress
    {
        public static bool TryParse(string? text, out string host, out int port)
        {
            return TryParse(text, out host, out port, out _);
        }

        /// <summary>The address, and the key after it if there is one (Room.NormalKey'd; null without).</summary>
        public static bool TryParse(string? text, out string host, out int port, out string? key)
        {
            host = "";
            port = 0;
            key = null;
            var keyed = Regex.Match((text ?? "").Trim(), @"^(\S+?)\s*(?:[·•|,;]\s*|\s+)([A-Za-z0-9][A-Za-z0-9 -]{2,10})$");
            if (keyed.Success)
            {
                text = keyed.Groups[1].Value;
                key = Room.NormalKey(keyed.Groups[2].Value);
                // Not a key's length («mac de arturo»): no address either.
                if (key.Length < 4 || key.Length > 6) return false;
            }
            var bare = Regex.Replace(Regex.Replace((text ?? "").Trim(), @"^[a-z]+://", "", RegexOptions.IgnoreCase), "/.*$", "");
            var m = Regex.Match(bare, @"^([\w.-]+)(?::(\d{1,5}))?$");
            if (!m.Success) return false;
            port = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : RoomHost.FirstPort;
            if (port < 1 || port > 65535) return false;
            host = m.Groups[1].Value;
            return true;
        }
    }

    /// <summary>
    /// What a player's app keeps between games, next to the save (conexion.json): the name last played with, the address
    /// last joined, and the seat's token at each host (the TypeScript app kept it in the page's storage, one per host).
    /// </summary>
    public sealed class NetSettings
    {
        public string? Name { get; set; }
        public string? Last { get; set; }
        public Dictionary<string, string> Tokens { get; set; } = new Dictionary<string, string>();

        [JsonIgnore]
        public string Path { get; private set; } = "";

        /// <summary>From `path`; a missing or unreadable file is a fresh start (it holds nothing that can't be typed again).</summary>
        public static NetSettings Load(string path)
        {
            NetSettings? read = null;
            try
            {
                if (File.Exists(path)) read = Json.Read<NetSettings>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                /* a fresh start */
            }
            read ??= new NetSettings();
            read.Tokens ??= new Dictionary<string, string>();
            read.Path = path;
            return read;
        }

        /// <summary>Written whole; a failure is left unsaid (the next game asks the name again, the host gives the seat by it).</summary>
        public void Save()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = Path + ".tmp";
                File.WriteAllText(tmp, Json.Write(this), new UTF8Encoding(false));
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(tmp, Path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                /* see above */
            }
        }

        /// <summary>The token store of the seat at `host` (address:port), kept here.</summary>
        public ITokenStore TokensFor(string host)
        {
            return new HostTokens(this, host);
        }

        private sealed class HostTokens : ITokenStore
        {
            private readonly NetSettings settings;
            private readonly string host;

            public HostTokens(NetSettings settings, string host)
            {
                this.settings = settings;
                this.host = host;
            }

            public string? Load() => settings.Tokens.TryGetValue(host, out var token) ? token : null;

            public void Save(string token)
            {
                if (Load() == token) return;
                settings.Tokens[host] = token;
                settings.Save();
            }
        }
    }
}
