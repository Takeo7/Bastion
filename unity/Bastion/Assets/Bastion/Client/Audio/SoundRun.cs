using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bastion.Client.Audio.Mix;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bastion.Client.Audio
{
    /// <summary>
    /// For the sound's test only (BASTION_SONIDO_GUION=&lt;guion.json&gt;, unity/tools/sonido/mezcla.mjs --app; never in a
    /// game of the players): the app plays a script of guiones/ on the mix, in silence, keeps what came out in
    /// BASTION_DATOS/sonido.f32 (to compare with 0.10.1's render) with what the mix cost, and quits. Its samples from the
    /// WAVs of BASTION_SONIDO_WAV (the mix alone), or with BASTION_SONIDO_CLIPS=1 from the game's clips (Vorbis, as it
    /// plays), the streamed pieces' still from the WAVs (a stream's samples can't be read).
    /// </summary>
    internal sealed class SoundRun
    {
        public readonly Script script;
        private readonly float[] output;
        private readonly string where;
        private long frame;
        private readonly Stopwatch watch = new Stopwatch();
        private double total;
        private double worst;
        private int blocks;
        private int size;
        private volatile bool done;
        private bool written;

        private SoundRun(Script script, string where)
        {
            this.script = script;
            this.where = where;
            output = new float[script.Frames * 2];
        }

        /// <summary>The run BASTION_SONIDO_GUION asks for, its samples read (main thread), or null.</summary>
        public static SoundRun? FromEnvironment(Manifest? manifest, int rate, int block)
        {
            var path = Environment.GetEnvironmentVariable("BASTION_SONIDO_GUION");
            if (string.IsNullOrEmpty(path)) return null;
            var json = JObject.Parse(File.ReadAllText(path));
            if ((int)json["rate"]! != rate)
            {
                Debug.LogError($"[sonido] prueba: el guion es a {(int)json["rate"]!} Hz y la app mezcla a {rate}");
                Application.Quit(1);
                return null;
            }
            var wav = Environment.GetEnvironmentVariable("BASTION_SONIDO_WAV");
            var clips = Environment.GetEnvironmentVariable("BASTION_SONIDO_CLIPS") == "1" || string.IsNullOrEmpty(wav);
            var samples = new Dictionary<string, Sample?>();
            Sample? Load(string file)
            {
                if (samples.TryGetValue(file, out var s)) return s;
                var clip = clips ? Manifest.Clip(file) : null;
                if (clip != null && clip.loadType != AudioClipLoadType.Streaming) s = manifest?.Samples(file);
                else if (!string.IsNullOrEmpty(wav)) s = Sample.FromWav(File.ReadAllBytes(Path.Combine(wav!, file)));
                samples[file] = s;
                return s;
            }
            var script = new Script(rate, (double)json["seconds"]!, (uint)json["seed"]!, block, json["events"]!.Select(Event), Load);
            foreach (var f in script.Files) Load(f);
            var data = Environment.GetEnvironmentVariable("BASTION_DATOS");
            var where = Path.Combine(string.IsNullOrEmpty(data) ? Application.persistentDataPath : data!, "sonido.f32");
            Debug.Log($"[sonido] prueba: {Path.GetFileName(path)}, {script.Seconds} s a {rate} Hz en bloques de {block}, sonidos {(clips ? "del juego (Vorbis)" : "en WAV")} → {where}");
            return new SoundRun(script, where);
        }

        private static Script.Event Event(JToken e)
        {
            var ev = new Script.Event();
            foreach (var p in ((JObject)e).Properties())
            {
                switch (p.Name)
                {
                    case "at": ev.At = (double)p.Value; break;
                    case "do": ev.Do = (string)p.Value!; break;
                    case "id": ev.Id = (string?)p.Value; break;
                    case "file": ev.File = (string?)p.Value; break;
                    case "bus": ev.Bus = (string?)p.Value; break;
                    case "word": ev.Word = p.Value.Type == JTokenType.String ? (string?)p.Value : null; break;
                    case "patch":
                        foreach (var v in ((JObject)p.Value).Properties()) ev.Patch[v.Name] = (float)v.Value;
                        break;
                    default:
                        if (p.Value.Type == JTokenType.Float || p.Value.Type == JTokenType.Integer) ev.Numbers[p.Name] = (double)p.Value;
                        break;
                }
            }
            return ev;
        }

        /// <summary>The next block (audio thread): the script's events, the mix timed, what came out kept, silence to the speakers.</summary>
        public void Render(Mixer mixer, float[] data, int channels)
        {
            var frames = data.Length / channels;
            if (frame >= script.Frames)
            {
                Array.Clear(data, 0, data.Length);
                done = true;
                return;
            }
            script.Run(mixer, frame, frames);
            watch.Restart();
            mixer.Render(data, channels);
            var ms = watch.Elapsed.TotalMilliseconds;
            // The first blocks pay for the JIT.
            if (frame > script.Rate)
            {
                total += ms;
                worst = Math.Max(worst, ms);
                blocks++;
            }
            size = frames;
            for (var i = 0; i < frames && frame + i < script.Frames; i++)
            {
                output[(frame + i) * 2] = data[i * channels];
                output[(frame + i) * 2 + 1] = channels > 1 ? data[i * channels + 1] : data[i * channels];
            }
            Array.Clear(data, 0, data.Length);
            frame += frames;
        }

        /// <summary>Once it is over (main thread): the file written, the cost said, the app closed.</summary>
        public void Tick()
        {
            if (!done || written) return;
            written = true;
            var bytes = new byte[output.Length * 4];
            Buffer.BlockCopy(output, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(where, bytes);
            var block = 1000.0 * size / script.Rate;
            var mean = blocks > 0 ? total / blocks : 0;
            Debug.Log($"[sonido] prueba: hecha. La mezcla, {mean:F2} ms de media por bloque de {size} ({block:F1} ms: {100 * mean / block:F1} % del hilo de audio), {worst:F2} ms como mucho");
            Application.Quit();
        }
    }
}
