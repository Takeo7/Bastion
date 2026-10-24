using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;

namespace Bastion.Client.Net.EditorTools
{
    /// <summary>
    /// How long the room takes to save after an order (Room.Persist: the whole mission, written and synced to disk), and a
    /// player's app to read the whole mission (a snapshot: ClientSession reads it on the network thread), in Unity's own
    /// runtime. In batchmode: unity/tools/unity.sh -executeMethod Bastion.Client.Net.EditorTools.NetBench.SaveTime
    /// [-mision &lt;file&gt;], a recorded mission (engine-cs/golden) or a save; the largest of golden/step2 if none. The result
    /// goes to the log, on the lines «[red] …».
    /// </summary>
    public static class NetBench
    {
        public static void SaveTime()
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-mision");
            var path = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath(Path.Combine(Application.dataPath, "../../../engine-cs/golden/step2/city-6-hard-veterans.json"));
            var state = EngineRules.Recorded(path).CreateMission(new SkirmishOptions(), new Rng(1));
            var dir = Path.Combine(Path.GetTempPath(), "bastion-bench-" + Guid.NewGuid().ToString("N"));
            var store = new SaveStore(Path.Combine(dir, "save.json"));
            var save = new SaveFile { State = state, Phase = RoomPhases.Mission, Rng = new long[] { 1, 2, 3, 4 } };
            var write = new List<double>();
            var whole = new List<double>();
            var read = new List<double>();
            var snapshot = Json.Write(new SnapshotMessage { State = state });
            try
            {
                for (int i = 0; i < 60; i++)
                {
                    var w = Stopwatch.StartNew();
                    Json.Write(save);
                    write.Add(w.Elapsed.TotalMilliseconds);
                    w.Restart();
                    store.Save(save);
                    whole.Add(w.Elapsed.TotalMilliseconds);
                    w.Restart();
                    Json.Read<ServerMessage>(snapshot);
                    read.Add(w.Elapsed.TotalMilliseconds);
                }
                UnityEngine.Debug.Log($"[red] {Path.GetFileName(path)}: {new FileInfo(Path.Combine(dir, "save.json")).Length / 1024} KB");
                UnityEngine.Debug.Log($"[red] serializar: {Spread(write)}");
                UnityEngine.Debug.Log($"[red] guardar entero (serializar, escribir, fsync, mover): {Spread(whole)}");
                UnityEngine.Debug.Log($"[red] leer el snapshot (en el hilo de red, fuera del fotograma): {Spread(read)}");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        /// <summary>The first ten left out (the JIT warming up): median, 90th percentile and the worst.</summary>
        private static string Spread(List<double> times)
        {
            var s = times.Skip(10).OrderBy((x) => x).ToList();
            return $"mediana {s[s.Count / 2]:F1} ms · p90 {s[(int)(s.Count * 0.9)]:F1} ms · máx {s[s.Count - 1]:F1} ms";
        }
    }
}
