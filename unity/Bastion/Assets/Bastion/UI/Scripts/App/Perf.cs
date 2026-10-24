using System;
using Bastion.Client.Net;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// How the game runs on each player's computer, told to the host (perf.ts of the 0.10.1): every ten seconds of play,
    /// one line with the frame rate and the refresh that caps it, the slowest frames, the time a frame takes on the main
    /// thread (its work, timed here, and apart what the frame waited) and on the graphics card (mean / 95th percentile,
    /// Unity's FrameTimingManager), the draw calls and
    /// triangles, the graphics card, the settings and the size; and one line when the player goes somewhere else
    /// (combate, menú). They go to the host's red.log by Red's report («[rend Ana] …»), so a partner's machine is measured
    /// without them doing anything. And the frames-per-second counter of Ajustes, in a corner.
    ///
    /// A frame costs a few reads and sums and a slot of a ring: no allocations. Out of focus, nothing is counted.
    /// </summary>
    public sealed class Perf : MonoBehaviour
    {
        /// <summary>Seconds of frames per line.</summary>
        const float Window = 10;
        /// <summary>After a change of view, scenes build and shaders compile: those frames wait.</summary>
        const float WarmUp = 2;
        /// <summary>On a change of view, what was measured of the last goes out if it was at least this long.</summary>
        const float Flush = 3;
        /// <summary>A frame this long is a hitch the player sees.</summary>
        const float Hitch = 0.05f;
        /// <summary>A gap this long is a pause (a hang, the window dragged): it is no frame.</summary>
        const float Pause = 1;
        /// <summary>Times kept per line: ten seconds at 240 Hz; beyond that the oldest are overwritten.</summary>
        const int Ring = 2400;

        /// <summary>The battle's scene (AppFlow's); any other is the menu.</summary>
        public string BattleScene = "Partida";

        /// <summary>The counter's label, on AppFlow's document; null, none.</summary>
        public Label Counter;

        readonly float[] intervals = new float[Ring];
        readonly float[] cpuTimes = new float[Ring];
        readonly float[] gpuTimes = new float[Ring];
        readonly float[] sorted = new float[Ring];
        readonly FrameTiming[] timing = new FrameTiming[1];
        ProfilerRecorder drawCalls;
        ProfilerRecorder triangles;

        string view = "";
        float warmUntil;
        bool announced = true;
        int frames;
        float spent;
        int hitches;
        int cpuCount;
        float cpuSum;
        float waitSum;
        int gpuCount;
        float gpuSum;
        double calls;
        double tris;

        int counterFrames;
        float counterFrom;

        /// <summary>
        /// The main thread's work in the last frame, in ms: from the start of the player loop's Initialization to the end
        /// of its PostLateUpdate (rendering included). The waits between frames (for the screen, for the frame cap) fall
        /// outside: Unity's own `cpuMainThreadFrameTime` counts them (24/10: a still menu at 60 fps read 14 ms).
        /// </summary>
        static double work;
        static long workFrom;

        /// <summary>The main thread's work in the last frame, in ms (Perfil shows it next to the markers).</summary>
        public static double Work => work;

        struct WorkStart { }
        struct WorkEnd { }

        /// <summary>The two stamps in the player loop, once (a capture's play mode keeps the loop from the last one).</summary>
        static void Hook()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (var i = 0; i < loop.subSystemList.Length; i++)
            {
                var phase = loop.subSystemList[i];
                var subs = phase.subSystemList ?? new PlayerLoopSystem[0];
                if (Array.Exists(subs, x => x.type == typeof(WorkStart) || x.type == typeof(WorkEnd))) return;
                if (phase.type == typeof(UnityEngine.PlayerLoop.Initialization))
                {
                    var with = new PlayerLoopSystem[subs.Length + 1];
                    with[0] = new PlayerLoopSystem { type = typeof(WorkStart), updateDelegate = () => workFrom = System.Diagnostics.Stopwatch.GetTimestamp() };
                    Array.Copy(subs, 0, with, 1, subs.Length);
                    loop.subSystemList[i].subSystemList = with;
                }
                else if (phase.type == typeof(UnityEngine.PlayerLoop.PostLateUpdate))
                {
                    var with = new PlayerLoopSystem[subs.Length + 1];
                    Array.Copy(subs, with, subs.Length);
                    with[subs.Length] = new PlayerLoopSystem { type = typeof(WorkEnd), updateDelegate = () => { if (workFrom != 0) work = (System.Diagnostics.Stopwatch.GetTimestamp() - workFrom) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; } };
                    loop.subSystemList[i].subSystemList = with;
                }
            }
            PlayerLoop.SetPlayerLoop(loop);
        }

        void OnEnable()
        {
            Hook();
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            GameSettings.Changed += OnSettings;
            OnSettings();
        }

        void OnDisable()
        {
            drawCalls.Dispose();
            triangles.Dispose();
            GameSettings.Changed -= OnSettings;
        }

        /// <summary>The settings changed: the counter shows or goes, and the next line starts afresh with them.</summary>
        void OnSettings()
        {
            if (Counter != null) Ui.Show(Counter, GameSettings.ShowFps);
            if (view == "") return;
            if (spent >= Flush) Sample();
            Restart();
            warmUntil = Time.realtimeSinceStartup + WarmUp / 2;
            announced = false;
        }

        void Update()
        {
            var now = Time.realtimeSinceStartup;
            var dt = Time.unscaledDeltaTime;
            FrameTimingManager.CaptureFrameTimings();
            Count(now);
            // Out of focus (another window on top, the partner's app beside it) nothing counts; back, half the warm-up.
            if (!Application.isFocused && !Application.isBatchMode)
            {
                warmUntil = Mathf.Max(warmUntil, now + WarmUp / 2);
                return;
            }
            var where = SceneManager.GetActiveScene().name == BattleScene ? "combate" : "menú";
            if (where != view)
            {
                if (view != "" && spent >= Flush) Sample();
                Restart();
                view = where;
                warmUntil = now + WarmUp;
                announced = false;
                return;
            }
            if (dt <= 0 || dt > Pause || now < warmUntil) return;
            if (!announced) Announce();
            intervals[frames % Ring] = dt * 1000;
            frames++;
            spent += dt;
            if (dt >= Hitch) hitches++;
            // The main thread's work, timed in the loop (with or without the frame timings); the rest of Unity's
            // main-thread frame was waiting (for the screen, Metal's next drawable on a Mac; for the frame cap), no work.
            var known = FrameTimingManager.GetLatestTimings(1, timing) > 0;
            var cpu = (float)work;
            if (cpu > 0 && cpu < Pause * 1000)
            {
                cpuTimes[cpuCount++ % Ring] = cpu;
                cpuSum += cpu;
                if (known) waitSum += Mathf.Max(0, (float)timing[0].cpuMainThreadFrameTime - cpu);
            }
            var gpu = known ? (float)timing[0].gpuFrameTime : 0;
            if (gpu > 0 && gpu < Pause * 1000)
            {
                gpuTimes[gpuCount++ % Ring] = gpu;
                gpuSum += gpu;
            }
            if (drawCalls.Valid) calls += drawCalls.LastValue;
            if (triangles.Valid) tris += triangles.LastValue;
            if (spent >= Window)
            {
                Sample();
                Restart();
            }
        }

        /// <summary>The counter in the corner: frames in the last second.</summary>
        void Count(float now)
        {
            counterFrames++;
            if (now - counterFrom < 1) return;
            if (Counter != null && GameSettings.ShowFps)
            {
                Counter.text = $"{Mathf.RoundToInt(counterFrames / (now - counterFrom))} FPS";
                // In the battle, beside the Menú tab.
                Counter.style.right = SceneManager.GetActiveScene().name == BattleScene ? 74 : 8;
            }
            counterFrames = 0;
            counterFrom = now;
        }

        void Restart()
        {
            frames = 0;
            spent = 0;
            hitches = 0;
            cpuCount = 0;
            cpuSum = 0;
            waitSum = 0;
            gpuCount = 0;
            gpuSum = 0;
            calls = 0;
            tris = 0;
        }

        /// <summary>Where the player went, once the view has settled: the graphics card, the settings, the size, the cores.</summary>
        void Announce()
        {
            announced = true;
            Send($"→ {view} · {Gpu()} · {GameSettings.Summary()} · {Size()} · {SystemInfo.processorCount} núcleos");
        }

        /// <summary>
        /// One line: frames per second and the refresh that caps them, the slowest frames, the main thread's time and the
        /// graphics card's (mean / 95th percentile), what was drawn per frame, and the machine and settings.
        /// </summary>
        void Sample()
        {
            if (frames == 0 || spent <= 0) return;
            var fps = Mathf.RoundToInt(frames / spent);
            var p95 = Percentile(intervals, Math.Min(frames, Ring), 0.95f);
            var sync = QualitySettings.vSyncCount;
            var refresh = (float)Screen.currentResolution.refreshRateRatio.value;
            var cap = sync > 0 && refresh > 0 ? $" (tope {Mathf.RoundToInt(refresh / sync)} Hz)" : Application.targetFrameRate > 0 ? $" (límite {Application.targetFrameRate})" : "";
            var slow = hitches > 0 ? $" · {hitches} {(hitches == 1 ? "tirón" : "tirones")}" : "";
            var cpu = cpuCount > 0 ? $"{Ms(cpuSum / cpuCount)}/{Ms(Percentile(cpuTimes, Math.Min(cpuCount, Ring), 0.95f))} ms (+{Ms(waitSum / cpuCount)} de espera)" : "n/d";
            var gpu = gpuCount > 0 ? $"{Ms(gpuSum / gpuCount)}/{Ms(Percentile(gpuTimes, Math.Min(gpuCount, Ring), 0.95f))} ms" : "n/d";
            var drawn = drawCalls.Valid ? $" · {Amount(calls / frames)} llamadas · {Amount(tris / frames)} tri" : "";
            Send($"{view} {fps} fps{cap} · p95 {Ms(p95)} ms{slow} · cpu {cpu} · gpu {gpu}{drawn} · {Gpu()} · {GameSettings.Summary()} · {Size()}");
        }

        /// <summary>One line to the host's red.log, tagged with this player's name (the host puts the address if there is none).</summary>
        static void Send(string text)
        {
            var link = NetLink.Instance;
            var name = (link.LastName ?? "").Trim();
            if (name.Length > 24) name = name.Substring(0, 24);
            link.Report($"[rend{(name.Length > 0 ? " " + name : "")}] {text}");
        }

        /// <summary>The `q` quantile of the first `n` values (copied, so the ring stays as it is).</summary>
        float Percentile(float[] values, int n, float q)
        {
            Array.Copy(values, sorted, n);
            Array.Sort(sorted, 0, n);
            return sorted[Math.Min(n - 1, (int)(n * q))];
        }

        static string Gpu()
        {
            var name = SystemInfo.graphicsDeviceName ?? "";
            return string.IsNullOrEmpty(name) ? "tarjeta desconocida" : name.Length > 48 ? name.Substring(0, 48) : name;
        }

        static string Size() => $"{Screen.width}×{Screen.height}";

        /// <summary>Milliseconds: a decimal under ten.</summary>
        static string Ms(float v) => v < 10 ? v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : Mathf.RoundToInt(v).ToString();

        /// <summary>A count per frame: 412, 38 k, 1.1 M.</summary>
        static string Amount(double v)
        {
            if (v >= 1e6) return (v / 1e6).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " M";
            if (v >= 1e4) return Math.Round(v / 1e3) + " k";
            return Math.Round(v).ToString();
        }
    }
}
