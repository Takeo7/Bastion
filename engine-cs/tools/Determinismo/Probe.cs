using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Bastion.Engine;
using Bastion.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bastion.Tools
{
    /// <summary>
    /// The rules the same to the bit on another runtime (docs/IOS.md, phase 1: iOS builds with IL2CPP, whose C++ compiler
    /// may fuse a multiply and an add, where the apps on the desktop run Mono and the tests .NET). Missions on generated
    /// maps of each biome, played by MissionBot for one seat: a line each, with the orders, the outcome and a hash of the
    /// state taken every 25 orders and at the end; with `dump`, each final state's JSON there, to compare. The same lines on two runtimes, the same rules. This file goes as it is
    /// into the Unity probe (Bastion-ios/determinismo, outside the repository), so only the engine and the BCL.
    /// </summary>
    public static class DeterminismProbe
    {
        public static IEnumerable<string> Run(int seeds, string? dump = null)
        {
            foreach (var biome in Biomes.All)
            {
                for (var seed = 1; seed <= seeds; seed++)
                {
                    var state = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, seed), Slots = new List<int> { 0 }, Rng = new Rng(seed) });
                    var rng = new Rng(seed * 7919 + 13);
                    var connected = new List<int> { 0 };
                    var trail = new StringBuilder(Hash(state));
                    int orders = 0, refused = 0, idle = 0;
                    while (state.Outcome == null && orders < 1500 && idle < 3)
                    {
                        var cmd = MissionBot.Next(state, 0, connected);
                        if (cmd == null) { idle++; continue; }
                        orders++;
                        if (Rules.ExecuteCommand(state, cmd, 0, connected, rng).Ok) idle = 0;
                        else { refused++; idle++; }
                        if (orders % 25 == 0) trail.Append(Hash(state));
                    }
                    trail.Append(Hash(state));
                    if (dump != null) File.WriteAllText(Path.Combine(dump, $"{biome}-{seed}.json"), Json.Write(state));
                    yield return $"{biome} {seed}: {orders} órdenes ({refused} rechazadas), {state.Outcome ?? "sin acabar"} · estado {Hash(state)} · camino {Digest(trail.ToString())} · texto {Digest(Json.Write(state))}";
                }
            }
        }

        /// <summary>
        /// The state's values, not its text: Mono (and IL2CPP, with Mono's libraries) writes a double with 17 digits where
        /// .NET writes the shortest that reads back the same (0.84199999999999986 and 0.8419999999999999 are one double).
        /// So the JSON read again, and each number with a fraction by its bits. «texto» in the line is the text's own hash.
        /// </summary>
        private static string Hash(GameState s)
        {
            var canon = new StringBuilder();
            Canon(JToken.Parse(Json.Write(s)), canon);
            return Digest(canon.ToString());
        }

        private static void Canon(JToken t, StringBuilder to)
        {
            switch (t)
            {
                case JObject o:
                    to.Append('{');
                    foreach (var p in o.Properties())
                    {
                        to.Append(p.Name).Append(':');
                        Canon(p.Value, to);
                        to.Append(',');
                    }
                    to.Append('}');
                    break;
                case JArray a:
                    to.Append('[');
                    foreach (var x in a)
                    {
                        Canon(x, to);
                        to.Append(',');
                    }
                    to.Append(']');
                    break;
                case JValue v when v.Type == JTokenType.Float:
                    to.Append('d').Append(BitConverter.DoubleToInt64Bits(v.Value<double>()).ToString("x"));
                    break;
                default:
                    to.Append(t.ToString(Formatting.None));
                    break;
            }
        }

        private static string Digest(string text)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var hex = new StringBuilder(12);
            for (var i = 0; i < 6; i++) hex.Append(bytes[i].ToString("x2"));
            return hex.ToString();
        }
    }
}
