using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The heavy cases, measured on this machine (Arturo, 1/11: the performance pass): «ciudad» (city-6-hard-veterans, 70
    /// units), «asedio» (the Bastion's siege with a wave of 60 dead in the clearing), «fuego» (city-3-elimination, its two
    /// fires, under a storm), «campo» (wilds-5-recovery, its pines); each as the game draws it (its units, its fog as the squad sees, its animals, its weather),
    /// the game's camera over the enemy at each of BASTION_DISTANCIAS (24 and 33.5 tiles, the farthest), at each of
    /// BASTION_CALIDAD (Medio, Alto), 1920×1080, in each of BASTION_VARIANTES in turns three times, the least of each (frames
    /// waited for, a pixel read back: no vsync to hold them):
    ///   base: as it is. lejos: the camera's far plane where the ground it can see ends. residente: URP's GPU Resident
    ///   Drawer (its renderer in Forward+). oclusion: that and its GPU occlusion culling. fusion: the map's still things
    ///   merged by material (StaticBatchingUtility). forward: Forward+ alone (what residente needs, on its own). mitad: a
    ///   quarter of the pixels (960×540), to tell what is paid by the pixel (what LOD wouldn't help) from what by the thing.
    /// And the figures' step on the CPU (UnitViews, ms for a 60th of a second, the least of 60), all of them and only those
    /// seen (UnitViews.CullOutOfPlay). The assets are put back as they were.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.RendimientoLab.Medir -quit
    /// </summary>
    public static class RendimientoLab
    {
        static GameState Case(string name)
        {
            switch (name)
            {
                case "asedio":
                {
                    var s = MapLab.Golden("../step2/bastion-siege");
                    var donor = MapLab.Golden("../step2/city-6-hard-veterans");
                    var plan = new BuildingPlan(s);
                    int w = s.Width, h = s.Height;
                    var taken = new HashSet<int>(s.Units.Where(u => u.Alive).Select(u => u.Pos.Y * w + u.Pos.X));
                    // A wave of the dead over the clearing, round the base: on open ground, a tile apart, outside its walls.
                    var free = Enumerable.Range(0, w * h).Where(i => plan.Owner[i] < 0 && Bastion.Engine.Grid.IsWalkable(s.Tiles[i]) && !taken.Contains(i))
                        .OrderBy(i => (uint)((i + 1) * 2654435761u) >> 8).ToList();
                    var dead = donor.Units.Where(u => u.Alive && u.Team != "xcom").ToList();
                    int n = 0;
                    foreach (int i in free)
                    {
                        if (n >= 60 || dead.Count == 0) break;
                        var u = dead[n % dead.Count];
                        if (n >= dead.Count) u = Bastion.Net.Json.Read<Unit>(Bastion.Net.Json.Write(u));
                        u.Id = $"ola{n}";
                        u.Pos = new Pos { X = i % w, Y = i / w, Z = 0 };
                        s.Units.Add(u);
                        n++;
                    }
                    return s;
                }
                case "campo":
                    return MapLab.Golden("../step2/wilds-5-recovery");
                case "fuego":
                {
                    var s = MapLab.Golden("../step2/city-3-elimination");
                    s.Weather = Weathers.Storm;
                    return s;
                }
                default:
                    return MapLab.Golden("../step2/city-6-hard-veterans");
            }
        }

        /// <summary>
        /// Where the triangles are (to weigh LOD): each case built as Medir builds it, and for each part of the map (the
        /// map's own children, and their children a level down) and the figures, how many renderers and triangles.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.RendimientoLab.Partes -quit
        /// </summary>
        public static void Partes()
        {
            // (Its parts as made, not joined: a joined thing's mesh is its whole batch.)
            MapBuilder.JoinStill = false;
            foreach (string name in (Environment.GetEnvironmentVariable("BASTION_CASOS") ?? "ciudad,asedio").Split(','))
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
                var state = Case(name);
                var view = MapLab.Map(state, name);
                var units = new GameObject("Unidades").AddComponent<Bastion.Client.Units.UnitViews>();
                units.Sync(state);
                static long Triangles(Renderer r)
                {
                    var mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) return 0;
                    long n = 0;
                    for (int k = 0; k < mesh.subMeshCount; k++) n += mesh.GetIndexCount(k) / 3;
                    return n;
                }
                void Report(Transform part, string label)
                {
                    var renderers = part.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
                    long tris = renderers.Sum(Triangles);
                    if (renderers.Count > 0) Debug.Log($"[partes] {name}: {label}: {renderers.Count} renderers, {tris / 1000}k triangles");
                }
                foreach (Transform part in view.transform)
                {
                    Report(part, part.name);
                    if (part.childCount < 40) foreach (Transform sub in part) Report(sub, $"{part.name}/{sub.name}");
                }
                Report(units.transform, "Unidades");
                long all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled).Sum(Triangles);
                Debug.Log($"[partes] {name}: all {all / 1000}k triangles");
            }
            MapBuilder.JoinStill = true;
        }

        public static void Medir()
        {
            var cases = (Environment.GetEnvironmentVariable("BASTION_CASOS") ?? "ciudad,asedio,fuego").Split(',');
            foreach (string name in cases) Measure(name);
        }

        static void Measure(string name)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var state = Case(name);
            Mood.Show(Mood.Of(state));
            NightLights.StateForLab = state;
            // (The map as it was before the still things were joined, so «fusion» weighs the joining: BASTION_JUNTAR=1 the game's.)
            MapBuilder.JoinStill = Environment.GetEnvironmentVariable("BASTION_JUNTAR") == "1";
            var view = MapLab.Map(state, name);
            MapBuilder.JoinStill = true;
            var units = new GameObject("Unidades").AddComponent<Bastion.Client.Units.UnitViews>();
            units.Sync(state);
            units.MasksNow();
            var fog = new GameObject("Niebla").AddComponent<Fog>();
            fog.Reset(state);
            fog.Show(state, true);
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            var target = new RenderTexture(1920, 1080, 24);
            // (mitad: a quarter of the pixels, to tell what is paid by the pixel from what by the thing drawn.)
            var half = new RenderTexture(960, 540, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            int frames = int.TryParse(Environment.GetEnvironmentVariable("BASTION_FRAMES"), out int f) ? f : 120;
            // (The scene's animals, weather and particles stepped a little, so all is where it would be.)
            void Settle()
            {
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(1 / 30f, false, false, true);
                    foreach (var step in root.GetComponentsInChildren<IShotStep>()) step.ShotStep(1 / 30f);
                }
            }
            double Frames()
            {
                void One()
                {
                    Settle();
                    camera.Render();
                    RenderTexture.active = camera.targetTexture;
                    probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    probe.Apply(false);
                    RenderTexture.active = null;
                }
                for (int k = 0; k < 10; k++) One();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int k = 0; k < frames; k++) One();
                return watch.Elapsed.TotalMilliseconds / frames;
            }
            double Step()
            {
                double least = double.MaxValue;
                for (int k = 0; k < 60; k++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    units.ShotStep(1 / 60f);
                    least = Math.Min(least, watch.Elapsed.TotalMilliseconds);
                }
                return least;
            }
            var enemy = state.Units.Where(u => u.Alive && u.Team != "xcom").ToList();
            var middle = enemy.Count > 0 ? MapSpace.World((float)enemy.Average(u => u.Pos.X), (float)enemy.Average(u => u.Pos.Y)) : MapSpace.World(state.Width / 2, state.Height / 2);
            Debug.Log($"[rendimiento] {name}: {state.Width}×{state.Height}, {state.Units.Count(u => u.Alive)} units ({enemy.Count} of the enemy), weather {state.Weather ?? "clear"}, fires {state.Fires?.Count ?? 0}");

            int was = QualitySettings.GetQualityLevel();
            var distances = (Environment.GetEnvironmentVariable("BASTION_DISTANCIAS") ?? "24,33.5").Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var asked = (Environment.GetEnvironmentVariable("BASTION_VARIANTES") ?? "base,lejos,forward,residente,oclusion,fusion").Split(',');
            // (The merge can't be undone: it is measured last, on its own, after the rest.)
            Run(asked.Where(v => v != "fusion").ToArray());
            if (asked.Contains("fusion")) Run(new[] { "fusion" });
            QualitySettings.SetQualityLevel(was, true);
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(half);

            void Run(string[] variants)
            {
            foreach (string level in (Environment.GetEnvironmentVariable("BASTION_CALIDAD") ?? "Medio,Alto").Split(','))
            {
                int index = Array.IndexOf(QualitySettings.names, level);
                if (index < 0) continue;
                QualitySettings.SetQualityLevel(index, true);
                var urp = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
                var pipeline = new SerializedObject(urp);
                var data = (UniversalRendererData)pipeline.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
                var renderer = new SerializedObject(data);
                int mode = renderer.FindProperty("m_RenderingMode").intValue;
                // (The GPU Resident Drawer's settings by their serialized names: its type is in an assembly of its own.)
                int resident = pipeline.FindProperty("m_GPUResidentDrawerMode").intValue;
                bool occlusion = pipeline.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras").boolValue;
                GameObject[] merged = null;
                void Put(string variant, float tiles)
                {
                    renderer.Update();
                    bool plus = variant == "forward" || variant == "residente" || variant == "oclusion";
                    renderer.FindProperty("m_RenderingMode").intValue = plus ? 2 : mode;
                    renderer.ApplyModifiedPropertiesWithoutUndo();
                    pipeline.Update();
                    pipeline.FindProperty("m_GPUResidentDrawerMode").intValue = variant == "residente" || variant == "oclusion" ? 1 : resident;
                    pipeline.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras").boolValue = variant == "oclusion" || occlusion && variant != "residente";
                    pipeline.ApplyModifiedPropertiesWithoutUndo();
                    data.SetDirty();
                    // (Where the ground the camera can see ends at 45° down with 40° of view, and a margin.)
                    camera.farClipPlane = variant == "lejos" ? tiles * MapSpace.Tile * 1.9f + 15 : 800;
                    camera.targetTexture = variant == "mitad" ? half : target;
                    if (variant == "fusion" && merged == null)
                    {
                        merged = view.GetComponentsInChildren<MeshRenderer>().Where(r => r.GetComponent<ParticleSystem>() == null && r.GetComponentInParent<Fauna>() == null && r.GetComponentInParent<Weather>() == null)
                            .Select(r => r.gameObject).ToArray();
                        StaticBatchingUtility.Combine(merged, view.gameObject);
                    }
                }
                foreach (float tiles in distances)
                {
                    MapSpace.Aim(camera, middle, tiles);
                    camera.fieldOfView = 40;
                    var least = variants.ToDictionary(v => v, v => double.MaxValue);
                    for (int turn = 0; turn < 3; turn++)
                        foreach (string variant in variants)
                        {
                            Put(variant, tiles);
                            least[variant] = Math.Min(least[variant], Frames());
                        }
                    Put(variants[0], tiles);
                    foreach (string variant in variants)
                        Debug.Log(FormattableString.Invariant($"[rendimiento] {name}, {level}, {tiles} tiles: {variant} {least[variant]:0.00} ms a frame ({least[variant] - least[variants[0]]:+0.00;-0.00} against {variants[0]})"));
                    double all = Step();
                    Bastion.Client.Units.UnitViews.CullOutOfPlay = true;
                    double seenOnly = Step();
                    Bastion.Client.Units.UnitViews.CullOutOfPlay = false;
                    Debug.Log(FormattableString.Invariant($"[rendimiento] {name}, {level}, {tiles} tiles: figures' step {all:0.00} ms, only those seen {seenOnly:0.00} ms"));
                }
                // The assets as they were.
                renderer.Update();
                renderer.FindProperty("m_RenderingMode").intValue = mode;
                renderer.ApplyModifiedPropertiesWithoutUndo();
                pipeline.Update();
                pipeline.FindProperty("m_GPUResidentDrawerMode").intValue = resident;
                pipeline.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras").boolValue = occlusion;
                pipeline.ApplyModifiedPropertiesWithoutUndo();
                data.SetDirty();
            }
            }
        }
    }
}
