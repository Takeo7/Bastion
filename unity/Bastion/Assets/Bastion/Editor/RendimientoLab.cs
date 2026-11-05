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
    /// fires, under a storm), «campo» (wilds-5-recovery, its pines), «poligono» (a city's industrial estate, seed 11); each
    /// as the game draws it (its units, its fog as the squad sees, its animals, its weather),
    /// the game's camera over the enemy at each of BASTION_DISTANCIAS (24 and 33.5 tiles, the farthest), at each of
    /// BASTION_CALIDAD (Medio, Alto), 1920×1080, in each of BASTION_VARIANTES in turns three times, the least of each (frames
    /// waited for, a pixel read back: no vsync to hold them):
    ///   base: as it is. lejos: the camera's far plane where the ground it can see ends. residente: URP's GPU Resident
    ///   Drawer (its renderer in Forward+). oclusion: that and its GPU occlusion culling. fusion: the map's still things
    ///   merged by material (StaticBatchingUtility). forward: Forward+ alone (what residente needs, on its own). mitad: a
    ///   quarter of the pixels (960×540), to tell what is paid by the pixel (what LOD wouldn't help) from what by the thing.
    ///   (Gameplay, 4/11: where a frame's ms go.) juego: as the game's camera draws, its post-processing on (bloom, tone
    ///   mapping and the grading's LUT: «base» has it off, as the camera here had on 1/11); and against it, each part away:
    ///   sinsombras, sinniebla (its pass, Niebla), singradacion (Gradación's pass), sinssao (Alto's), sinfiguras (the
    ///   units, their shadows with them), sinmapa (all the map: its weather, its animals); and what could be done:
    ///   stp75 and stp67 (the picture drawn at 75 % or 67 % and brought up by STP), cascadas (half the sun's shadow
    ///   cascades), sombramedia (its shadow map at half the size), sinsuaves (hard shadows), suavesbajas, suavesmedias and
    ///   suavesaltas (soft, at URP's low, medium or high quality), priming (URP's depth priming: the opaque pass shades only the nearest pixel, from the depth the
    ///   fog's and the SSAO's prepass already draws), ssaomitad (Alto's SSAO at half the size), ssaoligera (that and its
    ///   blur at low), bloomcuarto (the bloom from a quarter of the picture, not half), gradsincopia and nieblasincopia
    ///   (Gradación's or Niebla's pass without the copy of the picture it reads: what that copy costs, the picture wrong);
    ///   and of the figures:
    ///   sinmascara (without the stencil material each piece draws again for the fog, UnitViews.MasksOff), quietas (not
    ///   stepped: their bones still, nothing to skin again), figsinsombra (casting no shadow), sinbloques (without their
    ///   MaterialPropertyBlocks, the palette's, the tint's and the fade's, which keep a renderer out of the SRP Batcher),
    ///   horneadas (each skinned body baked into a still mesh where it stands: drawn the same, nothing skinned; measured
    ///   last, as fusion, for it can't be undone).
    /// Each frame's time, and of it the camera.Render call alone (the CPU's: culling, the passes, the draws handed on; the
    /// rest is waiting for the GPU). And the figures' step on the CPU (UnitViews, ms for a 60th of a second, the least of
    /// 60), all of them and only those seen (UnitViews.CullOutOfPlay); how many renderers and materials each figure and
    /// the map draw with. The assets are put back as they were.
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
                // (Phase 4 of #3: an estate's halls of Gang Warfare's sheet metal, two and three storeys of them.)
                case "poligono":
                    return MapLab.Generated("city", 11, "elimination", "industrial");
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
            Debug.Log($"[rendimiento] the levels in the apps: {ProjectSetup.AppLevels()}");
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
            // (BASTION_TODOS=1: every figure drawn, the fog's hidden too, as a horde in sight would be.)
            if (Environment.GetEnvironmentVariable("BASTION_TODOS") == "1") foreach (var u in state.Units) units.ForceVisible(u.Id);
            units.MasksNow();
            // What the figures and the map draw with: renderers, and their materials (a draw each, in each pass that draws it).
            static (int renderers, int materials, int skinned) Count(IEnumerable<Renderer> renderers)
            {
                var drawn = renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
                return (drawn.Count, drawn.Sum(r => r.sharedMaterials.Length), drawn.Count(r => r is SkinnedMeshRenderer));
            }
            foreach (var side in units.transform.Cast<Transform>().GroupBy(t => state.Units.FirstOrDefault(u => u.Id == t.name)?.Team == "xcom" ? "soldiers" : "others"))
            {
                var each = side.Select(t => Count(t.GetComponentsInChildren<Renderer>())).ToList();
                Debug.Log(FormattableString.Invariant($"[rendimiento] {name}: {each.Count} figures of the {side.Key}, {each.Average(c => c.renderers):0.0} renderers ({each.Average(c => c.skinned):0.0} skinned) and {each.Average(c => c.materials):0.0} materials each, {each.Sum(c => c.materials)} in all"));
                // (The first drawn one's, each renderer with its materials.)
                if (side.FirstOrDefault(t => Count(t.GetComponentsInChildren<Renderer>()).renderers > 0) is Transform first)
                    Debug.Log($"[rendimiento] {name}: {first.name} draws " + string.Join(", ", first.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => $"{r.name} {r.sharedMaterials.Length}{(r.shadowCastingMode == ShadowCastingMode.Off ? " (no shadow)" : "")}"
                        + (r is SkinnedMeshRenderer s && s.sharedMesh != null ? $" ({s.sharedMesh.vertexCount} vertices, {s.sharedMesh.subMeshCount} submeshes, {s.bones.Length} bones, {s.sharedMesh.blendShapeCount} blend shapes, {s.quality}, {string.Join("/", s.sharedMaterials.Select(m => m?.name))})" : ""))));
            }
            var map = Count(view.GetComponentsInChildren<Renderer>());
            Debug.Log($"[rendimiento] {name}: the map, {map.renderers} renderers and {map.materials} materials ({map.skinned} skinned)");
            var fog = new GameObject("Niebla").AddComponent<Fog>();
            fog.Reset(state);
            fog.Show(state, true);
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            var target = new RenderTexture(1920, 1080, 24);
            // (mitad: a quarter of the pixels, to tell what is paid by the pixel from what by the thing drawn.)
            var half = new RenderTexture(960, 540, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            int frames = int.TryParse(Environment.GetEnvironmentVariable("BASTION_FRAMES"), out int f) ? f : 120;
            // (The scene's animals, weather and particles stepped a little, so all is where it would be; the figures but in «quietas».)
            bool still = false;
            // (sinbloques: each figure's blocks kept here, to be put back.)
            Dictionary<Renderer, MaterialPropertyBlock> blocks = null;
            void Settle()
            {
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(1 / 30f, false, false, true);
                    foreach (var step in root.GetComponentsInChildren<IShotStep>())
                        if (!still || !(step is Bastion.Client.Units.UnitViews)) step.ShotStep(1 / 30f);
                }
                if (blocks != null)
                    foreach (var r in units.GetComponentsInChildren<Renderer>())
                        if (r.HasPropertyBlock())
                        {
                            if (!blocks.ContainsKey(r))
                            {
                                blocks[r] = new MaterialPropertyBlock();
                                r.GetPropertyBlock(blocks[r]);
                            }
                            r.SetPropertyBlock(null);
                        }
            }
            // A frame's ms, and the camera.Render call's.
            (double all, double cpu) Frames()
            {
                var render = new System.Diagnostics.Stopwatch();
                void One()
                {
                    Settle();
                    render.Start();
                    camera.Render();
                    render.Stop();
                    RenderTexture.active = camera.targetTexture;
                    probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    probe.Apply(false);
                    RenderTexture.active = null;
                }
                for (int k = 0; k < 10; k++) One();
                render.Reset();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int k = 0; k < frames; k++) One();
                return (watch.Elapsed.TotalMilliseconds / frames, render.Elapsed.TotalMilliseconds / frames);
            }
            // (The least of 60 steps, and their mean: the far ones of a crowd step every other frame, UnitViews.ThinOff.)
            (double least, double mean) Step()
            {
                double least = double.MaxValue, sum = 0;
                for (int k = 0; k < 60; k++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    units.ShotStep(1 / 60f);
                    least = Math.Min(least, watch.Elapsed.TotalMilliseconds);
                    sum += watch.Elapsed.TotalMilliseconds;
                }
                return (least, sum / 60);
            }
            var enemy = state.Units.Where(u => u.Alive && u.Team != "xcom").ToList();
            var middle = enemy.Count > 0 ? MapSpace.World((float)enemy.Average(u => u.Pos.X), (float)enemy.Average(u => u.Pos.Y)) : MapSpace.World(state.Width / 2, state.Height / 2);
            Debug.Log($"[rendimiento] {name}: {state.Width}×{state.Height}, {state.Units.Count(u => u.Alive)} units ({enemy.Count} of the enemy), weather {state.Weather ?? "clear"}, fires {state.Fires?.Count ?? 0}");

            int was = QualitySettings.GetQualityLevel();
            var distances = (Environment.GetEnvironmentVariable("BASTION_DISTANCIAS") ?? "24,33.5").Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var asked = (Environment.GetEnvironmentVariable("BASTION_VARIANTES") ?? "base,lejos,forward,residente,oclusion,fusion").Split(',');
            // (The merge can't be undone: it is measured last, on its own, after the rest.)
            Run(asked.Where(v => v != "fusion" && v != "horneadas").ToArray());
            if (asked.Contains("fusion")) Run(new[] { "fusion" });
            if (asked.Contains("horneadas")) Run(new[] { "horneadas" });
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
                float scale = pipeline.FindProperty("m_RenderScale").floatValue;
                int filter = pipeline.FindProperty("m_UpscalingFilter").intValue;
                int cascades = pipeline.FindProperty("m_ShadowCascadeCount").intValue;
                int shadowMap = pipeline.FindProperty("m_MainLightShadowmapResolution").intValue;
                bool soft = pipeline.FindProperty("m_SoftShadowsSupported").boolValue;
                int softQuality = pipeline.FindProperty("m_SoftShadowQuality").intValue;
                int priming = renderer.FindProperty("m_DepthPrimingMode").intValue;
                var ssao = data.rendererFeatures.FirstOrDefault(x => x is ScreenSpaceAmbientOcclusion);
                var ssaoData = ssao != null ? new SerializedObject(ssao) : null;
                bool ssaoHalf = ssaoData?.FindProperty("m_Settings.Downsample").boolValue ?? false;
                int ssaoBlur = ssaoData?.FindProperty("m_Settings.BlurQuality").intValue ?? 0;
                Bloom bloom = null;
                (pipeline.FindProperty("m_VolumeProfile").objectReferenceValue as VolumeProfile)?.TryGet(out bloom);
                var bloomDown = bloom != null ? bloom.downscale.value : BloomDownscaleMode.Half;
                var casting = units.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r, r => r.shadowCastingMode);
                var features = data.rendererFeatures.Where(x => x != null).ToDictionary(x => x, x => x.isActive);
                GameObject[] merged = null;
                bool baked = false;
                void Put(string variant, float tiles)
                {
                    renderer.Update();
                    bool plus = variant == "forward" || variant == "residente" || variant == "oclusion";
                    renderer.FindProperty("m_RenderingMode").intValue = plus ? 2 : mode;
                    renderer.FindProperty("m_DepthPrimingMode").intValue = variant == "priming" ? (int)DepthPrimingMode.Forced : priming;
                    renderer.ApplyModifiedPropertiesWithoutUndo();
                    if (ssaoData != null)
                    {
                        ssaoData.Update();
                        ssaoData.FindProperty("m_Settings.Downsample").boolValue = variant == "ssaomitad" || variant == "ssaoligera" || ssaoHalf;
                        ssaoData.FindProperty("m_Settings.BlurQuality").intValue = variant == "ssaoligera" ? 2 : ssaoBlur;
                        ssaoData.ApplyModifiedPropertiesWithoutUndo();
                        ssao.Create();
                    }
                    if (bloom != null) bloom.downscale.value = variant == "bloomcuarto" ? BloomDownscaleMode.Quarter : bloomDown;
                    pipeline.Update();
                    pipeline.FindProperty("m_GPUResidentDrawerMode").intValue = variant == "residente" || variant == "oclusion" ? 1 : resident;
                    pipeline.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras").boolValue = variant == "oclusion" || occlusion && variant != "residente";
                    pipeline.FindProperty("m_RenderScale").floatValue = variant == "stp75" ? 0.75f : variant == "stp67" ? 0.67f : scale;
                    pipeline.FindProperty("m_UpscalingFilter").intValue = variant.StartsWith("stp") ? (int)UpscalingFilterSelection.STP : filter;
                    pipeline.FindProperty("m_ShadowCascadeCount").intValue = variant == "cascadas" ? Math.Max(1, cascades / 2) : cascades;
                    pipeline.FindProperty("m_MainLightShadowmapResolution").intValue = variant == "sombramedia" ? shadowMap / 2 : shadowMap;
                    pipeline.FindProperty("m_SoftShadowsSupported").boolValue = soft && variant != "sinsuaves";
                    pipeline.FindProperty("m_SoftShadowQuality").intValue = variant == "suavesbajas" ? (int)SoftShadowQuality.Low : variant == "suavesmedias" ? (int)SoftShadowQuality.Medium
                        : variant == "suavesaltas" ? (int)SoftShadowQuality.High : softQuality;
                    pipeline.ApplyModifiedPropertiesWithoutUndo();
                    still = variant == "quietas";
                    if (variant == "sinbloques") blocks ??= new Dictionary<Renderer, MaterialPropertyBlock>();
                    else if (blocks != null)
                    {
                        foreach (var (r, block) in blocks) if (r != null) r.SetPropertyBlock(block);
                        blocks = null;
                    }
                    foreach (var (r, mode) in casting) if (r != null) r.shadowCastingMode = variant == "figsinsombra" ? ShadowCastingMode.Off : mode;
                    if (Bastion.Client.Units.UnitViews.MasksOff != (variant == "sinmascara"))
                    {
                        Bastion.Client.Units.UnitViews.MasksOff = variant == "sinmascara";
                        units.MasksNow();
                    }
                    // (The parts: «base» as on 1/11, the rest as the game's camera, its post-processing on.)
                    cameraData.renderPostProcessing = variant != "base" && variant != "lejos" && variant != "forward" && variant != "residente" && variant != "oclusion" && variant != "fusion" && variant != "mitad";
                    cameraData.renderShadows = variant != "sinsombras";
                    foreach (var (feature, active) in features)
                    {
                        feature.SetActive(active && !(variant == "sinniebla" && feature.name == "Niebla" || variant == "singradacion" && feature.name == "Gradación" || variant == "sinssao" && feature is ScreenSpaceAmbientOcclusion));
                        if (feature is FullScreenPassRendererFeature full)
                        {
                            full.fetchColorBuffer = !(variant == "gradsincopia" && feature.name == "Gradación" || variant == "nieblasincopia" && feature.name == "Niebla");
                            full.Create();
                        }
                    }
                    units.gameObject.SetActive(variant != "sinfiguras");
                    view.gameObject.SetActive(variant != "sinmapa");
                    data.SetDirty();
                    // (Where the ground the camera can see ends at 45° down with 40° of view, and a margin.)
                    camera.farClipPlane = variant == "lejos" ? tiles * MapSpace.Tile * 1.9f + 15 : 800;
                    camera.targetTexture = variant == "mitad" ? half : target;
                    if (variant == "horneadas" && !baked)
                    {
                        baked = true;
                        foreach (var skinned in units.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            if (!skinned.enabled || skinned.sharedMesh == null) continue;
                            var mesh = new Mesh();
                            // (In its renderer's space, without its scale: under it, the scale comes again.)
                            skinned.BakeMesh(mesh, false);
                            var held = new GameObject(skinned.name + " (horneado)");
                            held.transform.SetParent(skinned.transform, false);
                            held.AddComponent<MeshFilter>().sharedMesh = mesh;
                            var drawn = held.AddComponent<MeshRenderer>();
                            drawn.sharedMaterials = skinned.sharedMaterials;
                            drawn.shadowCastingMode = skinned.shadowCastingMode;
                            var block = new MaterialPropertyBlock();
                            skinned.GetPropertyBlock(block);
                            drawn.SetPropertyBlock(block);
                            skinned.enabled = false;
                        }
                    }
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
                    var cpu = variants.ToDictionary(v => v, v => double.MaxValue);
                    for (int turn = 0; turn < 3; turn++)
                        foreach (string variant in variants)
                        {
                            Put(variant, tiles);
                            var (frame, render) = Frames();
                            least[variant] = Math.Min(least[variant], frame);
                            cpu[variant] = Math.Min(cpu[variant], render);
                        }
                    Put(variants[0], tiles);
                    foreach (string variant in variants)
                        Debug.Log(FormattableString.Invariant($"[rendimiento] {name}, {level}, {tiles} tiles: {variant} {least[variant]:0.00} ms a frame ({least[variant] - least[variants[0]]:+0.00;-0.00} against {variants[0]}), camera.Render {cpu[variant]:0.00}"));
                    double all = Step().least;
                    Bastion.Client.Units.UnitViews.CullOutOfPlay = true;
                    Bastion.Client.Units.UnitViews.ThinOff = true;
                    var seenOnly = Step();
                    Bastion.Client.Units.UnitViews.ThinOff = false;
                    var thinned = Step();
                    Bastion.Client.Units.UnitViews.CullOutOfPlay = false;
                    Debug.Log(FormattableString.Invariant($"[rendimiento] {name}, {level}, {tiles} tiles: figures' step {all:0.00} ms, only those seen {seenOnly.least:0.00} ms (mean {seenOnly.mean:0.00}), the far ones every other frame {thinned.mean:0.00} ms a frame on average"));
                }
                // The assets as they were.
                renderer.Update();
                renderer.FindProperty("m_RenderingMode").intValue = mode;
                renderer.ApplyModifiedPropertiesWithoutUndo();
                pipeline.Update();
                pipeline.FindProperty("m_GPUResidentDrawerMode").intValue = resident;
                pipeline.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras").boolValue = occlusion;
                pipeline.FindProperty("m_RenderScale").floatValue = scale;
                pipeline.FindProperty("m_UpscalingFilter").intValue = filter;
                pipeline.FindProperty("m_ShadowCascadeCount").intValue = cascades;
                pipeline.FindProperty("m_MainLightShadowmapResolution").intValue = shadowMap;
                pipeline.FindProperty("m_SoftShadowsSupported").boolValue = soft;
                pipeline.FindProperty("m_SoftShadowQuality").intValue = softQuality;
                pipeline.ApplyModifiedPropertiesWithoutUndo();
                renderer.Update();
                renderer.FindProperty("m_DepthPrimingMode").intValue = priming;
                renderer.ApplyModifiedPropertiesWithoutUndo();
                if (ssaoData != null)
                {
                    ssaoData.Update();
                    ssaoData.FindProperty("m_Settings.Downsample").boolValue = ssaoHalf;
                    ssaoData.FindProperty("m_Settings.BlurQuality").intValue = ssaoBlur;
                    ssaoData.ApplyModifiedPropertiesWithoutUndo();
                    ssao.Create();
                }
                if (bloom != null) bloom.downscale.value = bloomDown;
                foreach (var (feature, active) in features)
                {
                    feature.SetActive(active);
                    if (feature is FullScreenPassRendererFeature full)
                    {
                        full.fetchColorBuffer = true;
                        full.Create();
                    }
                }
                data.SetDirty();
            }
            }
        }

        /// <summary>
        /// What the interface costs (Gameplay, 4/11): UI's play lab in «Partida» (HudShots.PartidaShot, or BASTION_LAB another
        /// of its labs; by name, from its own assembly), and once it has settled (BASTION_ESPERA frames, 150), the main
        /// thread's profiler markers over 300 frames, averaged per frame: those of the UI (UI Toolkit's panels, their layout,
        /// styles and repaint) and the costliest of all.
        ///   unity/tools/captura.sh -shotScene Assets/Bastion/Scenes/Partida.unity -shotSetup Bastion.EditorTools.RendimientoLab.Interfaz
        ///     -shotPlay -shotWarm 60 -shotFrames 1 -shotOut &lt;file&gt;
        /// </summary>
        public static void Interfaz()
        {
            Type.GetType("Bastion.UI.EditorTools.HudShots, Bastion.UI.Editor")?.GetMethod(Environment.GetEnvironmentVariable("BASTION_LAB") ?? "PartidaShot")?.Invoke(null, null);
            int wait = int.TryParse(Environment.GetEnvironmentVariable("BASTION_ESPERA"), out int w) ? w : 150;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                int from = -1;
                Debug.Log("[interfaz] in play");
                var recorders = new List<(string name, string category, Unity.Profiling.ProfilerRecorder recorder)>();
                var sums = new Dictionary<string, double>();
                int frame = -1, frames = 0;
                void Step()
                {
                    if (from < 0) from = Time.frameCount + wait;
                    if (Time.frameCount < from || Time.frameCount == frame) return;
                    frame = Time.frameCount;
                    if (recorders.Count == 0)
                    {
                        var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
                        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
                        foreach (var h in handles)
                        {
                            var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
                            if (d.UnitType != Unity.Profiling.ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                            recorders.Add((d.Name, d.Category.Name, Unity.Profiling.ProfilerRecorder.StartNew(d.Category, d.Name, 1)));
                        }
                        Debug.Log($"[interfaz] {recorders.Count} markers, from frame {frame}");
                        return;
                    }
                    frames++;
                    foreach (var (marker, category, recorder) in recorders)
                        if (recorder.Valid && recorder.LastValue > 0) sums[$"{category}/{marker}"] = (sums.TryGetValue($"{category}/{marker}", out var v) ? v : 0) + recorder.LastValue;
                    if (frames < 300) return;
                    string Each(IEnumerable<KeyValuePair<string, double>> some) => string.Join(" · ", some.Select(p => FormattableString.Invariant($"{p.Key} {p.Value / frames / 1e6:0.000}")));
                    Debug.Log($"[interfaz] {frames} frames; the UI's, ms a frame: " + Each(sums.Where(p => p.Key.StartsWith("GUI/") || p.Key.Contains("UIElements") || p.Key.Contains("UIR") || p.Key.Contains("Panel")).OrderByDescending(p => p.Value)));
                    Debug.Log($"[interfaz] the costliest, ms a frame: " + Each(sums.OrderByDescending(p => p.Value).Take(40)));
                    // (And the figures': their skinning, animation and deformation.)
                    Debug.Log($"[interfaz] the figures', ms a frame: " + Each(sums.Where(p => p.Key.Contains("Skin") || p.Key.Contains("Anim") || p.Key.Contains("Deform") || p.Key.Contains("UnitViews")).OrderByDescending(p => p.Value).Take(25)));
                    foreach (var (_, _, recorder) in recorders) recorder.Dispose();
                    recorders.Clear();
                    EditorApplication.update -= Step;
                }
                EditorApplication.update += Step;
            };
        }
    }
}
