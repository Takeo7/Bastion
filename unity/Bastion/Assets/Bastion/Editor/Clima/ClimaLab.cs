using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The five weathers (Bastion.Client.Weather; Arturo 1/11: all five in the game, each in missions of its own) on
    /// MapLab's map (BASTION_MAP, BASTION_AT as there), as the game draws them: the map made with its GameState.weather
    /// set, its sky Mood.Of's. BASTION_CLIMA_SIN=particulas|pase: without the particles, or the pass, to tell what draws what.
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.ClimaLab.Aguacero -shotFrames 60 -shotFps 10 -shotWarm 2 …
    /// </summary>
    public static class ClimaLab
    {
        public static void Llovizna() => Show(Weathers.Drizzle);
        public static void Aguacero() => Show(Weathers.Downpour);
        public static void Tormenta() => Show(Weathers.Storm);
        public static void Nevada() => Show(Weathers.Snowfall);
        public static void Ventisca() => Show(Weathers.Blizzard);

        static void Show(string weather)
        {
            MapLab.WeatherOverride = weather;
            try
            {
                MapLab.Day();
            }
            finally
            {
                MapLab.WeatherOverride = null;
            }
            var made = Weather.Current;
            if (made == null)
            {
                Debug.LogWarning($"[clima] {weather}: the map made no weather");
                return;
            }
            string without = Environment.GetEnvironmentVariable("BASTION_CLIMA_SIN");
            if (without == "particulas") foreach (var r in made.GetComponentsInChildren<ParticleSystemRenderer>()) r.enabled = false;
            if (without == "pase") made.enabled = false;
        }

        /// <summary>
        /// What each weather costs on this machine (the Mac): BASTION_MAP (city-7) by day under the game's camera over its
        /// middle at BASTION_DIST tiles (24), at each of BASTION_CALIDAD (Medio,Alto), 1920×1080: ms a frame clear and under
        /// each, in turns three times, the least of each (frames waited for, a pixel read back); and the particles' own step
        /// on the CPU (ms for a 60th of a second, the least of 60).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.ClimaLab.Coste -quit
        /// </summary>
        public static void Coste()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            var state = MapLab.Golden(name);
            Mood.Show(Mood.Day);
            var view = MapLab.Map(state, name);
            var plan = new BuildingPlan(state);
            BuildingSurfaces.Of(state, plan);
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            float tiles = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 24;
            MapSpace.Aim(camera, MapSpace.World(state.Width / 2, state.Height / 2), tiles);
            var fit = camera.gameObject.AddComponent<ShadowFit>();
            fit.Camera = camera;
            fit.Fit();
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            const int frames = 120;
            double Frames()
            {
                void One()
                {
                    camera.Render();
                    RenderTexture.active = target;
                    probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    probe.Apply(false);
                    RenderTexture.active = null;
                }
                for (int k = 0; k < 10; k++) One();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int k = 0; k < frames; k++) One();
                return watch.Elapsed.TotalMilliseconds / frames;
            }
            Weather Under(string weather)
            {
                if (Weather.Current != null)
                {
                    var gone = Weather.Current;
                    gone.Take();
                    UnityEngine.Object.DestroyImmediate(gone.gameObject);
                }
                state.Weather = weather;
                Mood.Show(Mood.Of(state, false));
                return weather != null ? Weather.Make(view.transform, state, plan) : null;
            }
            int was = QualitySettings.GetQualityLevel();
            foreach (string level in (Environment.GetEnvironmentVariable("BASTION_CALIDAD") ?? "Medio,Alto").Split(','))
            {
                int index = Array.IndexOf(QualitySettings.names, level);
                if (index < 0) continue;
                QualitySettings.SetQualityLevel(index, true);
                var cost = new Dictionary<string, double> { ["clear"] = double.MaxValue };
                var steps = new Dictionary<string, double>();
                foreach (string w in Weathers.All) cost[w] = double.MaxValue;
                for (int turn = 0; turn < 3; turn++)
                {
                    Under(null);
                    cost["clear"] = Math.Min(cost["clear"], Frames());
                    foreach (string w in Weathers.All)
                    {
                        var made = Under(w);
                        cost[w] = Math.Min(cost[w], Frames());
                        var systems = made.GetComponentsInChildren<ParticleSystem>();
                        double least = double.MaxValue;
                        for (int k = 0; k < 60; k++)
                        {
                            var watch = System.Diagnostics.Stopwatch.StartNew();
                            foreach (var ps in systems) ps.Simulate(1 / 60f, true, false, false);
                            least = Math.Min(least, watch.Elapsed.TotalMilliseconds);
                        }
                        steps[w] = steps.TryGetValue(w, out double s) ? Math.Min(s, least) : least;
                    }
                }
                Under(null);
                foreach (var kv in cost)
                    Debug.Log(FormattableString.Invariant($"[clima] {level}, {tiles} tiles: {kv.Key} {kv.Value:0.00} ms a frame ({kv.Value - cost["clear"]:+0.00;-0.00} ms){(steps.TryGetValue(kv.Key, out double st) ? $", particles' step {st:0.00} ms" : "")}"));
            }
            QualitySettings.SetQualityLevel(was, true);
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
