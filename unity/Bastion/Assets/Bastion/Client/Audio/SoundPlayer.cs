using System;
using System.Collections;
using System.Linq;
using Bastion.Client.Audio.Mix;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bastion.Client.Audio
{
    /// <summary>
    /// Where the game's sound comes out (docs/UNITY-SONIDO.md): one object kept across scenes, with the only AudioListener
    /// (the scenes' cameras' are switched off: all of the sound is mixed here, and placed by pan as in 0.10.1) and the Mixer
    /// on its filter, which overwrites Unity's own mix (silence: the streams' Taps take theirs). Made before the first
    /// scene; Sound, Music and Ambience reach it.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    internal sealed class SoundPlayer : MonoBehaviour
    {
        private static SoundPlayer? instance;
        private volatile Mixer? mixer;
        /// <summary>The sound's test, if this app is running one (BASTION_SONIDO_GUION): it alone plays.</summary>
        private SoundRun? run;
        private readonly System.Random random = new System.Random();

        internal Manifest? manifest;

        /// <summary>The player, or null while there is none (the editor out of play mode).</summary>
        public static SoundPlayer? Instance => instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Make()
        {
            if (instance != null) return;
            var o = new GameObject("Sonido");
            DontDestroyOnLoad(o);
            o.AddComponent<AudioListener>();
            instance = o.AddComponent<SoundPlayer>();
        }

        private void Awake()
        {
            manifest = Manifest.Load();
            if (manifest == null) Debug.LogWarning($"[sonido] no hay Resources/{Manifest.Folder}/manifiesto.json (unity/tools/sonido.sh): el juego va sin sonido");
            AudioSettings.GetDSPBufferSize(out var block, out _);
            var rate = AudioSettings.outputSampleRate;
            run = SoundRun.FromEnvironment(manifest, rate, block);
            recordTo = Environment.GetEnvironmentVariable("BASTION_SONIDO_GRABAR");
            if (!string.IsNullOrEmpty(recordTo)) recorded = new float[rate * 240 * 2];
            // The test's mix: the default volumes, and the reverb's noise and the loops' starts as 0.10.1's tools drew them.
            mixer = run != null ? new Mixer(rate, block, run.script.Random, Script.Volumes, false) : new Mixer(rate, block, Random, Values(), Volumes.Muted);
            Volumes.Changed += OnVolumes;
            SceneManager.sceneLoaded += OnScene;
            Debug.Log($"[sonido] mezcla a {rate} Hz en bloques de {block}{(manifest != null ? $", {manifest.recipes.Count} recetas, {manifest.recorded.Count} grupos grabados" : "")}");
            if (manifest != null && run == null) StartCoroutine(Warm());
            if (Sound.Tracing && run == null)
            {
                mixer.metering = true;
                StartCoroutine(Meter());
            }
        }

        private void OnDestroy()
        {
            Volumes.Changed -= OnVolumes;
            SceneManager.sceneLoaded -= OnScene;
            if (instance == this) instance = null;
        }

        /// <summary>The mix, for Sound, Music and Ambience (main thread).</summary>
        internal Mixer Mix => mixer!;

        /// <summary>From 0 to 1, as Math.random was in 0.10.1 (main thread only).</summary>
        internal double Random() => random.NextDouble();

        private static float[] Values() => Volumes.All.Select(Volumes.Get).ToArray();

        private void OnVolumes()
        {
            if (run == null) mixer?.SetVolumes(Values(), Volumes.Muted);
        }

        /// <summary>A scene's own listeners off: this one hears everything.</summary>
        private void OnScene(Scene scene, LoadSceneMode mode)
        {
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.gameObject != gameObject) l.enabled = false;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var m = mixer;
            if (m == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            if (run != null) run.Render(m, data, channels);
            else m.Render(data, channels);
            if (recorded != null) Record(data, channels);
        }

        private void Update() => run?.Tick();

        /// <summary>
        /// For the tests (BASTION_SONIDO_GRABAR=&lt;file&gt;): what comes out kept (stereo floats, up to four minutes), in
        /// silence, and written when the app quits: a mission or the base heard without anyone hearing it.
        /// </summary>
        private float[]? recorded;
        private int recordedFrames;
        private string? recordTo;

        private void Record(float[] data, int channels)
        {
            var frames = data.Length / channels;
            var room = Math.Min(frames, recorded!.Length / 2 - recordedFrames);
            for (var i = 0; i < room; i++)
            {
                recorded[(recordedFrames + i) * 2] = data[i * channels];
                recorded[(recordedFrames + i) * 2 + 1] = channels > 1 ? data[i * channels + 1] : data[i * channels];
            }
            recordedFrames += room;
            Array.Clear(data, 0, data.Length);
        }

        private void OnApplicationQuit()
        {
            if (recorded == null || recordTo == null) return;
            var bytes = new byte[recordedFrames * 2 * 4];
            Buffer.BlockCopy(recorded, 0, bytes, 0, bytes.Length);
            System.IO.File.WriteAllBytes(recordTo, bytes);
            Debug.Log($"[sonido] grabados {recordedFrames / (double)AudioSettings.outputSampleRate:F1} s a {AudioSettings.outputSampleRate} Hz → {recordTo}");
        }

        /// <summary>Each bus's level every ten seconds, for the tests (Sound.Tracing).</summary>
        private IEnumerator Meter()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(10);
                var levels = Mix.Levels();
                Debug.Log("[sonido] niveles · " + string.Join(" · ", Enum.GetValues(typeof(Bus)).Cast<Bus>().Select((b) => $"{b} {(double.IsNegativeInfinity(levels[(int)b]) ? "—" : $"{levels[(int)b]:F1} dB")}")));
            }
        }

        /// <summary>`what` after `seconds` of real time (a stream's end after its fade).</summary>
        internal void After(double seconds, Action what) => StartCoroutine(Later(seconds, what));

        private static IEnumerator Later(double seconds, Action what)
        {
            yield return new WaitForSecondsRealtime((float)seconds);
            what();
        }

        /// <summary>
        /// Every effect's samples read a few at a time, about 4 ms a frame, so that none is read in the middle of a fight
        /// (bank.ts prerender): the interface's first (the start screen is clicked while this goes on), then the most
        /// common. A sound asked for before its turn is read there and then, and plays: none is lost, nothing waits.
        /// </summary>
        private IEnumerator Warm()
        {
            var first = new[] { "ui.key", "ui.paper", "ui.page", "ui.stamp", "ui.return", "step", "shot.rifle.1", "impact.flesh", "impact.miss" };
            var m = manifest!;
            var files = Sound.InterfaceFiles
                .Concat(first.Where(m.recipes.ContainsKey).SelectMany((n) => m.recipes[n].files))
                .Concat(m.recipes.Values.SelectMany((r) => r.files.Concat(r.loopFile != null ? new[] { r.loopFile } : Array.Empty<string>())))
                .Concat(m.recorded.Where((g) => !g.Key.StartsWith("bed.", StringComparison.Ordinal)).SelectMany((g) => g.Value.Select((t) => t.file)))
                .Distinct().ToList();
            var started = Time.realtimeSinceStartup;
            var budget = System.Diagnostics.Stopwatch.StartNew();
            var one = new System.Diagnostics.Stopwatch();
            double slowest = 0;
            var frames = 0;
            foreach (var f in files)
            {
                if (m.Ready(f)) continue;
                one.Restart();
                m.Samples(f);
                slowest = Math.Max(slowest, one.Elapsed.TotalMilliseconds);
                if (budget.Elapsed.TotalMilliseconds < 4) continue;
                yield return null;
                frames++;
                budget.Restart();
            }
            Debug.Log($"[sonido] {files.Count} sonidos leídos en {Time.realtimeSinceStartup - started:F1} s, {frames} fotogramas (el más lento, {slowest:F0} ms)");
        }
    }
}
