using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The settlement's clearing for Shots, as 0.10.1's clearing lab (claro.html, lab/clearingLab.ts) has it: a fresh base
    /// (seed 3) with a clearing felled round its middle and the same pieces put in it, or a golden base (BASTION_BASE =
    /// a case of engine-cs/golden/step3/base.json, as base-70-siege), drawn as the map its siege would be fought on, with
    /// nobody on it (clearingArt.ts clearingState). Its camera as the lab's look(x, z, distance, yaw).
    ///   BASTION_BARE=1 none of the lab's pieces · BASTION_NIVEL=n the base's level · BASTION_SEED=s the fresh base's seed
    ///   BASTION_LOOK=x,z,distance[,yaw] the camera (the lab's look; yaw in radians, 0 from the south-east) · BASTION_CERCO=1
    ///   BASTION_SOMBRAS=m how far the shadows reach (90). To weigh the light against 0.10.1's: BASTION_GRADE=0 without the
    ///   grade · BASTION_SOL=0 without sun nor fill · with it BASTION_CIELO=0 without the sky's light, and BASTION_NIEBLA=1
    ///   the haze alone · BASTION_SOMBRAS_RES=texels,cascades,split the shadows' map
    ///   unity/tools/captura.sh -shotOut claro.png -shotSetup Bastion.EditorTools.SettlementLab.Clearing -shotSize 1600x900
    /// </summary>
    public static class SettlementLab
    {
        public static void Clearing()
        {
            var @base = Base();
            var state = ClearingState(@base);
            MapLab.Lighting(true);
            MapLab.Map(state, "claro");
            int c = @base.Width / 2;
            var look = Environment.GetEnvironmentVariable("BASTION_LOOK")?.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Look(look != null && look.Length >= 3 ? look[0] : c, look != null && look.Length >= 3 ? look[1] : c, look != null && look.Length >= 3 ? look[2] : 34, look != null && look.Length >= 4 ? look[3] : 0);
            MapLab.Lighting(true);
            // The sun's shadows over the whole clearing (0.10.1's shadow camera over all of it), as far as the view reaches.
            Mood.ShadowReach(float.TryParse(Environment.GetEnvironmentVariable("BASTION_SOMBRAS"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float reach) ? reach : 90);
            // BASTION_SOMBRAS_RES=texels,cascades,split: to weigh the shadows' sharpness (a test).
            var res = Environment.GetEnvironmentVariable("BASTION_SOMBRAS_RES")?.Split(',');
            if (res != null && (UnityEngine.QualitySettings.renderPipeline ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline) is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.mainLightShadowmapResolution = int.Parse(res[0]);
                urp.shadowCascadeCount = int.Parse(res[1]);
                urp.cascade2Split = float.Parse(res[2], System.Globalization.CultureInfo.InvariantCulture);
                Debug.Log($"[claro] shadows {urp.mainLightShadowmapResolution} texels, {urp.shadowCascadeCount} cascades, split {urp.cascade2Split}, {urp.shadowDistance} m");
            }
            // BASTION_SOL=0: no sun nor fill, the sky's light alone (a test, to weigh it against 0.10.1's).
            if (Environment.GetEnvironmentVariable("BASTION_SOL") == "0")
            {
                var alone = new Mood.Values();
                foreach (var f in typeof(Mood.Values).GetFields()) f.SetValue(alone, f.GetValue(Mood.Day));
                alone.sunIntensity = 0;
                alone.fillIntensity = 0;
                Mood.Show(alone);
                // BASTION_CIELO=0 as well: nor the sky's (what is left is what else lights the ground).
                if (Environment.GetEnvironmentVariable("BASTION_CIELO") == "0")
                {
                    RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
                    RenderSettings.fog = Environment.GetEnvironmentVariable("BASTION_NIEBLA") == "1";
                }
                Debug.Log($"[claro] ambient {RenderSettings.ambientMode} sky {RenderSettings.ambientSkyColor} equator {RenderSettings.ambientEquatorColor} ground {RenderSettings.ambientGroundColor}; probe up {Up(RenderSettings.ambientProbe)}; reflections {RenderSettings.reflectionIntensity} {RenderSettings.defaultReflectionMode}; skybox {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}");
            }
            // BASTION_GRADE=0: the picture as lit, without the grade (to weigh the light against 0.10.1's before its grade).
            if (Environment.GetEnvironmentVariable("BASTION_GRADE") == "0") Shader.SetGlobalFloat("_GradeOn", 0);
        }

        /// <summary>
        /// What the clearing costs to draw (Gameplay 24/10: the pinar past the edge, thousands of models): at each quality
        /// level (BASTION_CALIDAD=Medio,Alto; all three if not said) the lab's clearing (its env as Clearing's) drawn n times
        /// (BASTION_FRAMES, 120) at 1920×1080, each frame waited for (a pixel read back), with and without the pinar past
        /// the edge, in ms a frame; and how many renderers and triangles the pinar is. In the editor, on this machine's GPU.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.SettlementLab.Cost -quit
        /// </summary>
        public static void Cost()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Clearing();
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(k => k.name == "Shot camera");
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            int frames = int.TryParse(Environment.GetEnvironmentVariable("BASTION_FRAMES"), out int f) ? f : 120;
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
                var watch = Stopwatch.StartNew();
                for (int k = 0; k < frames; k++) One();
                return watch.Elapsed.TotalMilliseconds / frames;
            }
            var wood = GameObject.Find("Bosque");
            var renderers = wood != null ? wood.GetComponentsInChildren<MeshRenderer>() : Array.Empty<MeshRenderer>();
            long triangles = renderers.Sum(r => { var m = r.GetComponent<MeshFilter>()?.sharedMesh; return m == null ? 0L : Enumerable.Range(0, m.subMeshCount).Sum(k => (long)m.GetIndexCount(k)) / 3; });
            int all = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;
            Debug.Log($"[cost] the pinar past the edge: {renderers.Length} renderers, {triangles} triangles; the whole scene {all} renderers");
            string asked = Environment.GetEnvironmentVariable("BASTION_CALIDAD");
            var levels = asked != null ? asked.Split(',') : QualitySettings.names;
            int was = QualitySettings.GetQualityLevel();
            foreach (string level in levels)
            {
                int index = Array.IndexOf(QualitySettings.names, level);
                if (index < 0)
                {
                    Debug.LogWarning($"[cost] no quality level {level}");
                    continue;
                }
                QualitySettings.SetQualityLevel(index, true);
                // The shadows over the whole clearing, on this level's pipeline too.
                Mood.ShadowReach(90);
                if (wood != null) wood.SetActive(true);
                double with = Frames();
                if (wood != null) wood.SetActive(false);
                double without = Frames();
                Debug.Log($"[cost] {level}: {with:0.00} ms a frame with the pinar past the edge, {without:0.00} without ({frames} frames, 1920×1080)");
            }
            if (wood != null) wood.SetActive(true);
            // The project as it was: its level and its pipelines' shadows (the editor keeps the level it is left at).
            Mood.ShadowReach(null);
            QualitySettings.SetQualityLevel(was, true);
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>The lab's base (clearingLab.ts), or a golden one.</summary>
        static BaseState Base()
        {
            string golden = Environment.GetEnvironmentVariable("BASTION_BASE");
            if (golden != null)
            {
                var cases = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "../../../engine-cs/golden/step3/base.json")))["cases"];
                var found = cases.FirstOrDefault(k => (string)k["name"] == golden) ?? throw new ArgumentException($"no base {golden} in base.json");
                return found["options"]["map"]["base"].ToObject<BaseState>(Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }));
            }
            var @base = Bastion.Engine.Base.FreshBase(new Rng(int.TryParse(Environment.GetEnvironmentVariable("BASTION_SEED"), out int seed) ? seed : 3));
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_NIVEL"), out int level)) @base.Level = level;
            int c = @base.Width / 2;
            if (Environment.GetEnvironmentVariable("BASTION_BARE") == "1") return @base;
            for (int y = c - 9; y <= c + 9; y++)
                for (int x = c - 11; x <= c + 11; x++)
                    Bastion.Engine.Base.FellTree(@base, x, y);
            void Put(string id, int x, int y, int rot = 0, int lv = 1)
            {
                var p = Bastion.Engine.Base.PlacePiece(@base, id, x, y, rot);
                if (p.Level != 0) p.Level = lv;
            }
            Put("radio", c - 9, c - 8, 0, 2);
            Put("quarters", c - 5, c - 8, 0, 3);
            Put("workshop", c + 4, c - 8, 1, 1);
            Put("garden", c - 9, c + 3, 0, 2);
            Put("memorial", c + 1, c + 4);
            for (int y = c - 4; y <= c + 1; y++) Put(y == c - 2 ? "gate" : "palisade", c - 10, y);
            Put("sandbags", c - 6, c - 3);
            Put("sandbags", c - 5, c - 3);
            Put("logs", c + 4, c - 3);
            Put("tower", c + 7, c - 3);
            Put("floodlight", c - 7, c);
            Put("turret", c + 7, c + 1);
            Put("snare", c - 5, c + 2);
            // A palisade across the barracks' front and a tower by the caravan's end, in the way of both shots.
            if (Environment.GetEnvironmentVariable("BASTION_CERCO") == "1")
            {
                for (int x = c - 7; x <= c; x++) Put("palisade", x, c - 2);
                Put("tower", c + 4, c + 1);
            }
            return @base;
        }

        /// <summary>What the ambient probe gives a surface facing up (URP's SampleSH, linear).</summary>
        static Color Up(UnityEngine.Rendering.SphericalHarmonicsL2 sh)
        {
            var colours = new Color[1];
            sh.Evaluate(new[] { Vector3.up }, colours);
            return colours[0];
        }

        /// <summary>The clearing as a battlefield with nobody on it: the map its siege would be fought on (clearingArt.ts clearingState).</summary>
        public static GameState ClearingState(BaseState @base)
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.OfBase(@base), Slots = new System.Collections.Generic.List<int> { 0 }, Rng = new Rng(1), Difficulty = 1 });
            s.Units.Clear();
            s.Traps = null;
            if (s.Base != null) s.Base.Felled = @base.Felled ?? new System.Collections.Generic.List<int>();
            return s;
        }

        /// <summary>The lab's camera (clearingLab.ts look): on (x, z) tiles from `distance` tiles, its bearing `yaw` from the south-east, 50° down, 40° of view.</summary>
        static void Look(float x, float z, float distance, float yaw)
        {
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(k => k.name == "Shot camera")
                ?? new GameObject("Shot camera").AddComponent<Camera>();
            float a = Mathf.PI / 4 + yaw, down = 50 * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a) * Mathf.Cos(down), Mathf.Sin(down), Mathf.Cos(a) * Mathf.Cos(down));
            var target = new Vector3(x, 0, z);
            var at = target + dir * distance;
            // 0.10.1's tiles (z south) to Unity's metres.
            camera.transform.position = new Vector3(at.x, at.y, -at.z) * MapSpace.Tile;
            camera.transform.LookAt(new Vector3(target.x, target.y, -target.z) * MapSpace.Tile);
            camera.fieldOfView = 40;
            camera.farClipPlane = 800;
        }
    }
}
