using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The map from a recorded state (engine-cs/golden/step1/*.json, the engine's own states of nine missions), as setups
    /// for Shots: built by MapBuilder under the camera of 0.10.1's capture view (over the middle, 40 tiles off). BASTION_MAP
    /// names the state (city-7 unless said).
    /// </summary>
    public static class MapLab
    {
        public static void Day() => Build(true);

        public static void Night() => Build(false);

        /// <summary>
        /// What the sun's shadows cost on a battle's map (Gameplay 26/10: ShadowFit, the shadows over what the camera sees):
        /// BASTION_MAP (city-7) by day, the camera over its middle at each of BASTION_DISTANCIAS (tiles: 24, the game's, and
        /// 42, its farthest), at each quality level (BASTION_CALIDAD=Medio,Alto; all if not said), drawn n times (BASTION_FRAMES,
        /// 120) at 1920×1080, each frame waited for (a pixel read back), with the pipeline's own shadows (50 m) and fitted, in
        /// turns three times, in ms a frame (the least of each). In the editor, on this machine's GPU.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapLab.Cost -quit
        /// </summary>
        public static void Cost()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            var state = Golden(name);
            Lighting(true);
            var view = Map(state, name);
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            camera.fieldOfView = 40;
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
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int k = 0; k < frames; k++) One();
                return watch.Elapsed.TotalMilliseconds / frames;
            }
            var fit = camera.gameObject.AddComponent<Bastion.Client.ShadowFit>();
            fit.Camera = camera;
            var middle = MapSpace.World(state.Width / 2, state.Height / 2);
            var distances = (Environment.GetEnvironmentVariable("BASTION_DISTANCIAS") ?? "24,42").Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            string asked = Environment.GetEnvironmentVariable("BASTION_CALIDAD");
            int was = QualitySettings.GetQualityLevel();
            foreach (string level in asked != null ? asked.Split(',') : QualitySettings.names)
            {
                int index = Array.IndexOf(QualitySettings.names, level);
                if (index < 0)
                {
                    Debug.LogWarning($"[cost] no quality level {level}");
                    continue;
                }
                QualitySettings.SetQualityLevel(index, true);
                foreach (float tiles in distances)
                {
                    // The game's camera (CameraRig: 52° down, turned 45°).
                    float pitch = 52 * Mathf.Deg2Rad, yaw = 45 * Mathf.Deg2Rad, back = Mathf.Cos(pitch) * tiles * MapSpace.Tile;
                    camera.transform.position = middle + new Vector3(Mathf.Sin(yaw) * back, Mathf.Sin(pitch) * tiles * MapSpace.Tile, -Mathf.Cos(yaw) * back);
                    camera.transform.LookAt(middle);
                    // In turns, three times each, the least of each (other apps on the GPU only ever add to it).
                    double own = double.MaxValue, fitted = double.MaxValue;
                    for (int turn = 0; turn < 3; turn++)
                    {
                        Bastion.Client.Mood.ShadowReach(null);
                        own = Math.Min(own, Frames());
                        fit.Fit();
                        fitted = Math.Min(fitted, Frames());
                    }
                    var urp = (QualitySettings.renderPipeline ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline) as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                    Debug.Log(FormattableString.Invariant($"[cost] {level}, {tiles} tiles: {own:0.00} ms a frame with the shadows to 50 m, {fitted:0.00} fitted (ground {fit.Near:0}–{fit.Far / 1.08f:0} m deep, shadows to {urp?.shadowDistance:0} m, {urp?.shadowCascadeCount} cascades; {frames} frames, 1920×1080)"));
                }
            }
            Bastion.Client.Mood.ShadowReach(null);
            QualitySettings.SetQualityLevel(was, true);
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>
        /// What each way of drawing costs at a quality level (Gameplay 26/10: «medio» spent more of the GPU than «alto» in the
        /// city): BASTION_MAP (city-7) by day under the game's camera at 24 tiles, at BASTION_CALIDAD (Medio), with each of
        /// BASTION_VARIANTES in turns three times, the least of each, in ms a frame at 1920×1080: «tal» as the project has it,
        /// «fplus» its renderer in Forward+, «sinsombras» without the other lights' shadows, «sinluces» without other lights
        /// (the fill too), «sinssao» without ambient occlusion, «sinniebla» and «singradacion» without those passes. The
        /// assets are put back as they were.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapLab.Variants -quit
        /// </summary>
        public static void Variants()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            var state = Golden(name);
            Lighting(true);
            Map(state, name);
            // BASTION_FIGURAS=1: every unit of the game drawn too, the camera over the middle of the enemy (a crowd's cost).
            Bastion.Client.Units.UnitViews units = null;
            if (Environment.GetEnvironmentVariable("BASTION_FIGURAS") == "1")
            {
                units = new GameObject("Unidades").AddComponent<Bastion.Client.Units.UnitViews>();
                units.Sync(state);
                units.MasksNow();
            }
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            camera.fieldOfView = 40;
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
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int k = 0; k < frames; k++) One();
                return watch.Elapsed.TotalMilliseconds / frames;
            }
            float tiles = 24, pitch = 52 * Mathf.Deg2Rad, yaw = 45 * Mathf.Deg2Rad, back = Mathf.Cos(pitch) * tiles * MapSpace.Tile;
            var middle = MapSpace.World(state.Width / 2, state.Height / 2);
            var enemy = state.Units.Where(u => u.Alive && u.Team != "xcom").ToList();
            if (units != null && enemy.Count > 0) middle = MapSpace.World((float)enemy.Average(u => u.Pos.X), (float)enemy.Average(u => u.Pos.Y));
            if (units != null) Debug.Log($"[variant] {state.Units.Count(u => u.Alive)} units, {enemy.Count} of the enemy; drawn round {middle}");
            camera.transform.position = middle + new Vector3(Mathf.Sin(yaw) * back, Mathf.Sin(pitch) * tiles * MapSpace.Tile, -Mathf.Cos(yaw) * back);
            camera.transform.LookAt(middle);
            int was = QualitySettings.GetQualityLevel();
            string level = Environment.GetEnvironmentVariable("BASTION_CALIDAD") ?? "Medio";
            int index = Array.IndexOf(QualitySettings.names, level);
            if (index < 0)
            {
                Debug.LogWarning($"[variant] no quality level {level}");
                return;
            }
            QualitySettings.SetQualityLevel(index, true);
            var urp = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            var pipeline = new SerializedObject(urp);
            var data = (UnityEngine.Rendering.Universal.UniversalRendererData)pipeline.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
            var renderer = new SerializedObject(data);
            var features = data.rendererFeatures.Where(x => x != null).ToDictionary(x => x.name, x => x.isActive);
            int mode = renderer.FindProperty("m_RenderingMode").intValue, lights = pipeline.FindProperty("m_AdditionalLightsRenderingMode").intValue;
            bool lightShadows = pipeline.FindProperty("m_AdditionalLightShadowsSupported").boolValue;
            void Put(string variant)
            {
                renderer.Update();
                pipeline.Update();
                renderer.FindProperty("m_RenderingMode").intValue = variant == "fplus" ? 2 : mode;
                pipeline.FindProperty("m_AdditionalLightsRenderingMode").intValue = variant == "sinluces" ? 0 : lights;
                pipeline.FindProperty("m_AdditionalLightShadowsSupported").boolValue = variant != "sinsombras" && lightShadows;
                renderer.ApplyModifiedPropertiesWithoutUndo();
                pipeline.ApplyModifiedPropertiesWithoutUndo();
                foreach (var feature in data.rendererFeatures.Where(x => x != null))
                {
                    bool off = variant == "sinssao" && feature is UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion
                        || variant == "sinniebla" && feature.name == "Niebla" || variant == "singradacion" && feature.name.StartsWith("Grada");
                    feature.SetActive(!off && features[feature.name]);
                }
                data.SetDirty();
                if (units != null)
                {
                    Bastion.Client.Units.UnitViews.MasksOff = variant == "sinmascara";
                    units.MasksNow();
                }
            }
            var variants = (Environment.GetEnvironmentVariable("BASTION_VARIANTES") ?? "tal,fplus,sinsombras,sinluces,sinniebla,singradacion").Split(',');
            var least = variants.ToDictionary(v => v, v => double.MaxValue);
            for (int turn = 0; turn < 3; turn++)
                foreach (var variant in variants)
                {
                    Put(variant);
                    least[variant] = Math.Min(least[variant], Frames());
                }
            Put("tal");
            foreach (var variant in variants)
                Debug.Log(FormattableString.Invariant($"[variant] {level} {variant}: {least[variant]:0.00} ms a frame ({frames} frames, 1920×1080, {tiles} tiles)"));
            QualitySettings.SetQualityLevel(was, true);
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>
        /// How much a point light gives in URP (26/10: the night's lamps lit far less than 0.10.1's): a white plane, the
        /// ground's own material and URP's Lit, under one point light of intensity 1 at 1 m over it, no other light, drawn
        /// to a float target with no post; the pixel under it read back (linear). URP: albedo × intensity / d².
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapLab.LightUnit -quit
        /// </summary>
        public static void LightUnit()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0, 5, 0), Quaternion.Euler(90, 0, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBFloat);
            camera.targetTexture = target;
            var probe = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            var light = new GameObject("Luz").AddComponent<UnityEngine.Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.transform.position = new Vector3(0, 1, 0);
            light.range = 100;
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var shaders = new[] { ("Lit", Shader.Find("Universal Render Pipeline/Lit")), ("Ground", Resources.Load<Material>("Ground")?.shader), ("Building", Shader.Find("Bastion/Building")) };
            foreach (var (name, shader) in shaders)
            {
                if (shader == null)
                {
                    Debug.Log($"[light] no {name} shader");
                    continue;
                }
                var material = new Material(shader);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0);
                plane.GetComponent<Renderer>().sharedMaterial = material;
                foreach (float intensity in new[] { 0f, 1f })
                {
                    light.intensity = intensity;
                    light.color = Color.white;
                    camera.Render();
                    RenderTexture.active = target;
                    probe.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
                    probe.Apply(false);
                    RenderTexture.active = null;
                    Debug.Log(FormattableString.Invariant($"[light] {name}, intensity {intensity}: {probe.GetPixel(0, 0)}"));
                }
            }
            // The night's own light on the white plane (Mood: the sky's hemisphere, the moon, the fill), against what the fog's
            // pools reckon it to be (Fog.shader: the sky, and the moon by its angle).
            light.intensity = 0;
            plane.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            plane.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", Color.white);
            plane.GetComponent<Renderer>().sharedMaterial.SetFloat("_Smoothness", 0);
            foreach (bool day in new[] { false, true })
            {
                Lighting(day);
                RenderSettings.fog = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.Render();
                RenderTexture.active = target;
                probe.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
                probe.Apply(false);
                RenderTexture.active = null;
                Vector4 sky = Shader.GetGlobalVector("_LightSky"), sun = Shader.GetGlobalVector("_LightSun"), from = Shader.GetGlobalVector("_LightSunFrom");
                var reckoned = (Vector3)sky + (Vector3)sun * Mathf.Max(0, from.y);
                Debug.Log(FormattableString.Invariant($"[light] {(day ? "day" : "night")}: {probe.GetPixel(0, 0)} on a white floor; the fog reckons {reckoned} (sky {(Vector3)sky}, sun {(Vector3)sun} × {from.y:0.00})"));
            }
        }

        /// <summary>
        /// A kit piece's parts as the catalog has them (BASTION_PIEZA, houseWindow unless said): each mesh, its submeshes'
        /// materials and the box of their UVs, to tell which faces draw what.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapLab.Piece -quit
        /// </summary>
        public static void Piece()
        {
            string id = Environment.GetEnvironmentVariable("BASTION_PIEZA") ?? "houseWindow";
            foreach (var part in Bastion.Client.Map.Pieces.Parts(id))
                for (int k = 0; k < part.mesh.subMeshCount; k++)
                {
                    var tris = part.mesh.GetTriangles(k);
                    var uv = new List<Vector2>();
                    part.mesh.GetUVs(0, uv);
                    var used = tris.Select(t => uv.Count > t ? uv[t] : Vector2.zero).ToList();
                    Debug.Log(FormattableString.Invariant($"[pieza] {id}/{part.name} sub {k}: {tris.Length / 3} triangles, {(k < part.materials.Length && part.materials[k] != null ? part.materials[k].name + " (" + part.materials[k].shader.name + ")" : "none")}, uv {used.Min(v => v.x):0.000}-{used.Max(v => v.x):0.000} × {used.Min(v => v.y):0.000}-{used.Max(v => v.y):0.000}"));
                }
        }

        /// <summary>A recorded state by its name (engine-cs/golden/step1/&lt;name&gt;.json).</summary>
        static void AoRow(GameState state, int y, int x0, int x1)
        {
            // (Worked out as something is first drawn: GroundOcclusion's BeforeRendering.)
            var probe = new GameObject("Sonda").AddComponent<Camera>();
            probe.targetTexture = RenderTexture.GetTemporary(64, 64, 24);
            probe.Render();
            RenderTexture.ReleaseTemporary(probe.targetTexture);
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            var rt = Shader.GetGlobalTexture("_GroundAO") as RenderTexture;
            if (rt == null) { Debug.Log("[ao] no texture"); return; }
            var read = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            read.Apply();
            RenderTexture.active = was;
            int per = rt.width / state.Width;
            Debug.Log($"[ao] row {y}, {per} texels a tile: " + string.Join(" ", Enumerable.Range(x0, x1 - x0 + 1).Select(x => state.Tiles[y * state.Width + x][0])));
            // (North up the texture: tile row y's middle at v = 1 − (y + 0.5) / height.)
            int ty = Mathf.Clamp(Mathf.FloorToInt((1 - (y + 0.5f) / state.Height) * rt.height), 0, rt.height - 1);
            var cells = new System.Collections.Generic.List<string>();
            for (int tx = x0 * per; tx < (x1 + 1) * per; tx++)
            {
                var c = read.GetPixel(tx, ty);
                cells.Add($"{c.r:0.00}/{c.g:0.0}");
            }
            for (int k = 0; k < cells.Count; k += per) Debug.Log($"[ao] x {x0 + k / per}: " + string.Join(" ", cells.Skip(k).Take(per)));
        }

        static void AoDump(GameState state, string file)
        {
            var probe = new GameObject("Sonda").AddComponent<Camera>();
            probe.targetTexture = RenderTexture.GetTemporary(64, 64, 24);
            // (Its first frame bakes the occlusion: what that takes, and a second frame's without it.)
            var clock = System.Diagnostics.Stopwatch.StartNew();
            probe.Render();
            double first = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            probe.Render();
            double next = clock.Elapsed.TotalMilliseconds;
            // (And baked again, as the game does when the map changes: the least of three.)
            double again = double.MaxValue;
            for (int k = 0; k < 3; k++)
            {
                UnityEngine.Object.FindFirstObjectByType<GroundOcclusion>()?.MarkDirty();
                clock.Restart();
                probe.Render();
                again = Math.Min(again, clock.Elapsed.TotalMilliseconds);
            }
            Debug.Log(FormattableString.Invariant($"[ao] first frame {first:0.0} ms, the next {next:0.0} ms, baked again {again:0.0} ms"));
            RenderTexture.ReleaseTemporary(probe.targetTexture);
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            var rt = Shader.GetGlobalTexture("_GroundAO") as RenderTexture;
            if (rt == null) { Debug.Log("[ao] no texture"); return; }
            var read = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            read.Apply();
            RenderTexture.active = was;
            int per = rt.width / state.Width;
            var lines = new System.Collections.Generic.List<string>();
            for (int y = 0; y < state.Height; y++)
            {
                var row = new System.Collections.Generic.List<string>();
                for (int x = 0; x < state.Width; x++)
                {
                    float sum = 0;
                    for (int a = 0; a < per; a++)
                        for (int b = 0; b < per; b++) sum += read.GetPixel(x * per + b, (state.Height - 1 - y) * per + a).r;
                    row.Add((sum / (per * per)).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                }
                lines.Add(string.Join(" ", row));
            }
            System.IO.File.WriteAllLines(file, lines);
            Debug.Log($"[ao] volcado en {file}");
        }

        /// <summary>reference.ts's mission with a medical site (`medical`: clinic, ward, hospital), as it makes it.</summary>
        internal static GameState Medical(string medical)
        {
            int seed = int.TryParse(Environment.GetEnvironmentVariable("BASTION_SEED"), out int n) ? n : 7;
            int? story = medical == "clinic" ? 1 : medical == "hospital" ? 2 : (int?)null;
            return Setup.CreateMission(new MissionOptions
            {
                Map = new MapSource { Kind = MapSourceKinds.Generated, Biome = "city", Seed = seed, Medical = medical },
                Slots = new System.Collections.Generic.List<int> { 0 },
                Rng = new Rng((uint)seed),
                Kind = medical == "hospital" ? "rescue" : "recovery",
                Difficulty = 3,
                CampaignMission = story != null ? new CampaignMission { Id = "medical-reference", Name = medical == "clinic" ? "El consultorio de Brena" : "La doctora", Story = story } : null,
            });
        }

        /// <summary>A generated mission (BASTION_GENERAR), with the police cars the engine puts in any (MissionState.PoliceCar; Arturo, 2/11, A13).</summary>
        internal static GameState Generated(string biome, int seed, string kind, string site = null) => Setup.CreateMission(new MissionOptions
        {
            Map = new MapSource { Kind = MapSourceKinds.Generated, Biome = biome, Seed = seed, Site = site },
            Slots = new System.Collections.Generic.List<int> { 0 },
            Rng = new Rng((uint)seed),
            Kind = kind,
            Difficulty = 3,
        });

        /// <summary>
        /// The floors' and roofs' faces with no normal (a polygon's first three corners in a line: the shader's NaN black,
        /// and the bloom spreads it over the screen; the 0.11.3's square houses, 1/11), in each map of BASTION_MAP (the
        /// goldens of step1 if not said; a saved game's file by its path too).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapLab.Normales -quit
        /// </summary>
        public static void Normales()
        {
            var names = Environment.GetEnvironmentVariable("BASTION_MAP")?.Split(',')
                ?? Directory.GetFiles(Path.Combine(Application.dataPath, "../../../engine-cs/golden/step1"), "*.json").Select(Path.GetFileNameWithoutExtension).Where(n => n != "content" && n != "math").ToArray();
            foreach (string name in names)
            {
                var state = Golden(name);
                var set = BuildingSurfaces.Of(state, new BuildingPlan(state));
                var parts = typeof(SurfaceSet).GetFields().Where(f => f.FieldType == typeof(Surfaces)).Select(f => (f.Name, surfaces: (Surfaces)f.GetValue(set))).Where(p => p.surfaces != null);
                var counts = parts.Select(p =>
                {
                    int none = 0, all = p.surfaces.Normal.Count / 3;
                    for (int k = 0; k < all; k++) if (p.surfaces.Normal[3 * k] == 0 && p.surfaces.Normal[3 * k + 1] == 0 && p.surfaces.Normal[3 * k + 2] == 0) none++;
                    return $"{p.Name} {none} of {all}";
                });
                Debug.Log($"[normales] {Path.GetFileNameWithoutExtension(name)}: vertices with no normal: {string.Join(", ", counts)}");
            }
        }

        /// <summary>
        /// The window columns (WindowColumns; Ana's note 17, Arturo, 7/11) against the windows: in each map of BASTION_MAP
        /// (step1's and step2's goldens and step3's towns and cities if not said; a saved game's file by its path too), for each building of the plan, the
        /// windows of its ground floor and of its real storeys out of their columns as WallLayout counts them (its bounds,
        /// WallLayout.ColumnsEvery), and how many windows a storey its façade storeys get (WallLayout.Storeys).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapLab.Columnas -quit
        /// </summary>
        public static void Columnas()
        {
            string golden = Path.Combine(Application.dataPath, "../../../engine-cs/golden");
            var names = Environment.GetEnvironmentVariable("BASTION_MAP")?.Split(',')
                ?? Directory.GetFiles(Path.Combine(golden, "step1"), "*.json").Concat(Directory.GetFiles(Path.Combine(golden, "step2"), "*.json"))
                    .Concat(new[] { "town", "city" }.Select(n => Path.Combine(golden, "step3", n + ".json")))
                    .Where(f => !new[] { "content", "math" }.Contains(Path.GetFileNameWithoutExtension(f))).ToArray();
            // (step3's are cases, each a state: town.json's towns.)
            var states = new List<(string name, Func<GameState> state)>();
            foreach (string name in names)
            {
                var file = File.Exists(name) ? Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(name)) : null;
                if (file?["cases"] is Newtonsoft.Json.Linq.JArray cases)
                    foreach (var c in cases) states.Add(($"{Path.GetFileNameWithoutExtension(name)}:{c["name"]}", () => Bastion.Net.Json.Read<GameState>(c["state"].ToString(Newtonsoft.Json.Formatting.None))));
                else states.Add((Path.GetFileNameWithoutExtension(name), () => Golden(name)));
            }
            foreach (var (name, read) in states)
            {
                GameState state;
                try { state = read(); }
                catch (Exception e) { Debug.Log($"[columnas] {name}: not read ({e.GetType().Name})"); continue; }
                var plan = new BuildingPlan(state);
                int w = state.Width, windows = 0, off = 0, upper = 0, upperOff = 0, facades = 0;
                var lines = new List<string>();
                foreach (var bd in plan.Buildings)
                {
                    if (bd.kind == BuildingKinds.Ruin) continue;
                    int every = WallLayout.ColumnsEvery(state, bd);
                    // Each side's tiles (DIRS4: 0 north, the row y0), and the windows on them of the ground and of each real storey.
                    var sides = new List<string>();
                    for (int side = 0; side < 4; side++)
                    {
                        int n = side % 2 == 0 ? bd.x1 - bd.x0 + 1 : bd.y1 - bd.y0 + 1;
                        var columns = WindowColumns.Along(n, every);
                        (int x, int y) At(int u) => side == 0 ? (bd.x0 + u, bd.y0) : side == 1 ? (bd.x1, bd.y0 + u) : side == 2 ? (bd.x0 + u, bd.y1) : (bd.x0, bd.y0 + u);
                        var floors = new List<IList<string>> { state.Tiles };
                        if (bd.leveled && state.Levels != null) floors.AddRange(state.Levels.Take(bd.storeys - 1).Select(l => (IList<string>)l.Tiles));
                        var found = new List<string>();
                        for (int k = 0; k < floors.Count; k++)
                        {
                            var us = Enumerable.Range(0, n).Where(u => { var (x, y) = At(u); string tk = floors[k][y * w + x]; return tk == TileKinds.Window || tk == TileKinds.WindowBroken; }).ToList();
                            int outOf = us.Count(u => !columns.Contains(u));
                            if (k == 0) { windows += us.Count; off += outOf; } else { upper += us.Count; upperOff += outOf; }
                            if (outOf > 0) found.Add($"storey {k}: {string.Join(" ", us)}");
                        }
                        if (found.Count > 0) sides.Add($"side {side} (n {n}, columns {string.Join(" ", columns)}): {string.Join("; ", found)}");
                    }
                    if (!bd.leveled && bd.storeys > 1)
                    {
                        facades++;
                        // A façade over a ground floor with openings in it: the ones whose columns line up to be seen.
                        int open = 0;
                        for (int y = bd.y0; y <= bd.y1; y++) for (int x = bd.x0; x <= bd.x1; x++)
                            if ((x == bd.x0 || x == bd.x1 || y == bd.y0 || y == bd.y1) && state.Tiles[y * w + x] is string tk && tk != TileKinds.Wall && BuildingPlan.IsWallLike(tk)) open++;
                        if (open > 0) lines.Add($"  façade {bd.index} {bd.kind} ({bd.x0},{bd.y0})–({bd.x1},{bd.y1}) storeys {bd.storeys} every {every}: {open} openings below");
                    }
                    // (BASTION_COLUMNAS_TODOS=1: every building, its columns or not.)
                    if (sides.Count == 0 && Environment.GetEnvironmentVariable("BASTION_COLUMNAS_TODOS") == "1")
                        lines.Add($"  building {bd.index} {bd.kind} ({bd.x0},{bd.y0})–({bd.x1},{bd.y1}){(bd.leveled ? " leveled" : "")} storeys {bd.storeys} every {every}: in their columns");
                    if (sides.Count > 0) lines.Add($"  building {bd.index} {bd.kind} ({bd.x0},{bd.y0})–({bd.x1},{bd.y1}){(bd.leveled ? " leveled" : "")} storeys {bd.storeys} every {every}: {string.Join(" | ", sides)}");
                }
                Debug.Log($"[columnas] {name}: {plan.Buildings.Count} buildings ({facades} with façade storeys); ground windows {windows}, {off} out of their columns; real storeys' {upper}, {upperOff} out" + (lines.Count > 0 ? "\n" + string.Join("\n", lines) : ""));
            }
        }

        internal static GameState Golden(string name)
        {
            // (Or a saved game's file by its path: a lab's, UI's lab-asedio.json.)
            string path = name.EndsWith(".json") && File.Exists(name) ? name : Path.GetFullPath(Path.Combine(Application.dataPath, $"../../../engine-cs/golden/step1/{name}.json"));
            var file = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            return Bastion.Net.Json.Read<GameState>(file["state"].ToString(Newtonsoft.Json.Formatting.None));
        }

        /// <summary>
        /// The whole cars of the street (MapBuilder.PaintWhole) in a grid: one a row, each of its paints a column, fitted as
        /// the map fits them, by day, under the game's camera; BASTION_DESGASTE=0 in Synty's paint as it is (to set them
        /// side by side), BASTION_COCHES_ENTEROS=a,b the rows (all of carFront's whole ones if not said).
        /// </summary>
        public static void Cars()
        {
            MapBuilder.Worn = Environment.GetEnvironmentVariable("BASTION_DESGASTE") != "0";
            Lighting(true);
            var ids = Environment.GetEnvironmentVariable("BASTION_COCHES_ENTEROS")?.Split(',') ?? MapArt.Set("carFront").Where(c => MapBuilder.WholePaintsOf(c).Length > 0).Distinct().ToArray();
            var prefabs = Prefabs();
            var materials = Materials();
            var root = new GameObject("Coches").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 10;
            ground.GetComponent<Renderer>().sharedMaterial = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Plain, "Suelo");
            ground.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.5f));
            float across = 2.2f * MapSpace.Tile, along = 1.6f * MapSpace.Tile;
            int columns = ids.Max(c => MapBuilder.WholePaintsOf(c).Length);
            for (int row = 0; row < ids.Length; row++)
            {
                var art = MapArt.Prop(ids[row]);
                var model = art != null ? prefabs(MapArt.ModelKey(art)) : null;
                if (model == null)
                {
                    Debug.LogWarning($"[coches] {ids[row]}: no model");
                    continue;
                }
                var paints = MapBuilder.WholePaintsOf(ids[row]);
                for (int col = 0; col < paints.Length; col++)
                {
                    var car = (GameObject)UnityEngine.Object.Instantiate(model, root);
                    car.name = $"{ids[row]} {paints[col] ?? "propia"}";
                    // Its long side along the rows, fitted as the map fits it (its longest side to `fit` tiles).
                    Bounds Box() => car.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    if (Box().size.x > Box().size.z) car.transform.rotation = Quaternion.Euler(0, 90, 0);
                    var box = Box();
                    float k = art.fit > 0 ? art.fit * MapSpace.Tile / Mathf.Max(box.size.x, box.size.z) : 1;
                    car.transform.localScale *= k;
                    car.transform.position = new Vector3((col - (columns - 1) / 2f) * along, 0, -(row - (ids.Length - 1) / 2f) * across);
                    if (!MapBuilder.PaintWhole(car, ids[row], paints[col], materials)) Debug.LogWarning($"[coches] {ids[row]}: no {paints[col]}");
                }
            }
            Debug.Log($"[coches] {ids.Length} cars, worn {MapBuilder.Worn}: {string.Join(", ", ids.Select(c => $"{c} ({MapBuilder.WholePaintsOf(c).Length})"))}");
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            float distance = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 22;
            MapSpace.Aim(camera, Vector3.zero, distance, 0);
            Lighting(true);
        }

        /// <summary>
        /// Every car model of the street (carFront's, the open wreck and the patrol car) as MapBuilder.Prop stands it facing
        /// north, each over its two tiles: a red disc on the tile the engine calls its nose (the CarFront, Decor.CarPartner),
        /// a blue one on its boot's (Looting.BootOf), from the side (BASTION_GIRO degrees, 90), to tell a model turned back
        /// to front. BASTION_COCHES_ENTEROS=a,b the models.
        /// </summary>
        public static void CarNoses()
        {
            Lighting(true);
            var ids = Environment.GetEnvironmentVariable("BASTION_COCHES_ENTEROS")?.Split(',') ?? MapArt.Set("carFront").Concat(new[] { "carOpen", "sirenPolice" }).Distinct().ToArray();
            var prefabs = Prefabs();
            var root = new GameObject("Coches").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 10;
            ground.GetComponent<Renderer>().sharedMaterial = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Plain, "Suelo");
            ground.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.5f));
            Material Disc(Color c)
            {
                var m = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Plain, "Marca");
                m.SetColor("_BaseColor", c);
                return m;
            }
            Material nose = Disc(new Color(0.85f, 0.1f, 0.08f)), boot = Disc(new Color(0.1f, 0.3f, 0.9f));
            int columns = 5, rows = (ids.Length + columns - 1) / columns;
            for (int k = 0; k < ids.Length; k++)
            {
                var car = MapBuilder.Prop(root, ids[k], prefabs);
                if (car == null)
                {
                    Debug.LogWarning($"[morros] {ids[k]}: no model");
                    continue;
                }
                // Its two tiles north–south, the middle between them (MapBuilder.Thing): the nose's to the north.
                var at = new Vector3((k % columns - (columns - 1) / 2f) * 2.6f * MapSpace.Tile, 0, -(k / columns - (rows - 1) / 2f) * 3.4f * MapSpace.Tile);
                car.transform.position = at;
                foreach (var (material, z) in new[] { (nose, 0.5f), (boot, -0.5f) })
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    disc.transform.position = at + new Vector3(1.1f * MapSpace.Tile, 0.02f, z * MapSpace.Tile);
                    disc.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
                    disc.GetComponent<Renderer>().sharedMaterial = material;
                }
                var box = car.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Debug.Log($"[morros] {ids[k]} ({MapArt.Prop(ids[k])?.pack}:{MapArt.Prop(ids[k])?.fbx}, yaw {MapArt.Prop(ids[k])?.yaw}): row {k / columns}, column {k % columns}, box {box.size.x:F2} × {box.size.z:F2} m");
            }
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            float distance = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 24;
            float turn = float.TryParse(Environment.GetEnvironmentVariable("BASTION_GIRO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g) ? g : 90;
            MapSpace.Aim(camera, Vector3.zero, distance, turn);
            Lighting(true);
        }

        /// <summary>
        /// The line Bastion/Mark draws (Kit/Mark's, the target's), its colour as it reaches the screen in linear light, and
        /// one set to UI's bone (SearchOutline's): a sphere each drawn into a float target, the line's pixels read (the
        /// target's must stay linear 1, 0.16, 0.08 with _LineColor's sRGB default).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapLab.MarkColour -quit
        /// </summary>
        public static void MarkColour()
        {
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var camera = new GameObject("Marca").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            camera.transform.position = new Vector3(0, 0, -4);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ColorUtility.TryParseHtmlString("#f3ecdc", out var bone);
            foreach (var (name, colour) in new[] { ("del objetivo", (Color?)null), ("hueso", (Color?)bone) })
            {
                var mark = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Mark, "Prueba");
                mark.SetFloat("_Tint", 0);
                mark.SetFloat("_Base", 0);
                mark.SetFloat("_Rim", 0);
                mark.SetFloat("_Mark", 1);
                mark.SetFloat("_Line", 4);
                if (colour is Color c) mark.SetColor("_LineColor", c);
                ball.GetComponent<Renderer>().sharedMaterials = new[] { mark };
                camera.Render();
                RenderTexture.active = target;
                var read = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
                read.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                RenderTexture.active = null;
                var lit = read.GetPixels().Where(p => p.r > 0.2f).ToArray();
                var mean = lit.Length > 0 ? new Color(lit.Average(p => p.r), lit.Average(p => p.g), lit.Average(p => p.b)) : Color.clear;
                Debug.Log(FormattableString.Invariant($"[marca] {name}: {lit.Length} pixels of line, linear {mean.r:0.0000} {mean.g:0.0000} {mean.b:0.0000} (material {mark.GetColor("_LineColor")})"));
            }
        }

        /// <summary>
        /// The whole cars with their boot open as the map opens it when searched (CarBoot), each beside itself shut, at the
        /// map's size (MapArt.FootprintOf), facing north, painted their first paint, by day, the game's camera from behind
        /// them (BASTION_GIRO degrees, 20; BASTION_DIST tiles, 26). BASTION_COCHES_ENTEROS=a,b the models.
        /// </summary>
        public static void CarBoots()
        {
            Lighting(true);
            var ids = Environment.GetEnvironmentVariable("BASTION_COCHES_ENTEROS")?.Split(',') ?? new[] { "carSedan", "carTaxi", "sirenPolice", "carMuscle", "carConvertible", "carSmall", "carMedium", "carHatch", "carPickup", "carVan", "carUte" };
            var prefabs = Prefabs();
            var materials = Materials();
            var root = new GameObject("Coches").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 10;
            ground.GetComponent<Renderer>().sharedMaterial = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Plain, "Suelo");
            ground.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.5f));
            int columns = 4, rows = (ids.Length + columns - 1) / columns;
            for (int k = 0; k < ids.Length; k++)
                for (int open = 0; open < 2; open++)
                {
                    var car = MapBuilder.Prop(root, ids[k], prefabs);
                    if (car == null)
                    {
                        Debug.LogWarning($"[maletero] {ids[k]}: no model");
                        break;
                    }
                    // At the map's size (its footprint's fit against its own), as MapBuilder.Place makes it.
                    var art = MapArt.Prop(ids[k]);
                    var footprint = MapArt.FootprintOf(ids[k]);
                    if (art.fit > 0 && footprint != null && footprint.fit > 0) car.transform.localScale *= footprint.fit / art.fit;
                    car.transform.position = new Vector3(((k % columns) * 2 + open - columns + 0.5f) * 2.2f * MapSpace.Tile, 0, -(k / columns - (rows - 1) / 2f) * 5f * MapSpace.Tile);
                    if (open == 1) Debug.Log($"[maletero] {ids[k]}: {(Bastion.Client.Map.CarBoot.Open(car, ids[k]) ? "open" : "NOT OPEN")}");
                    var paints = MapBuilder.WholePaintsOf(ids[k]);
                    if (paints.Length > 0) MapBuilder.PaintWhole(car, ids[k], paints[0], materials);
                }
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            float distance = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 26;
            float turn = float.TryParse(Environment.GetEnvironmentVariable("BASTION_GIRO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g) ? g : 20;
            MapSpace.Aim(camera, Vector3.zero, distance, turn);
            Lighting(true);
        }

        /// <summary>
        /// Each whole car's parts as MapBuilder.Prop stands it facing north (x across, y up, z to the nose, metres): every
        /// renderer's name, vertices and box, and its body's and glass's profile from the back to the nose (BASTION_TROZOS
        /// slices, 24): the highest point of each and the lowest glass, to tell where a boot lid or a hatch ends.
        /// </summary>
        public static void CarProfiles()
        {
            var ids = Environment.GetEnvironmentVariable("BASTION_COCHES_ENTEROS")?.Split(',') ?? new[] { "carSedan", "carSmall", "carMedium", "carMuscle", "carTaxi", "carVan", "carConvertible", "carPickup", "carUte", "carHatch", "sirenPolice" };
            int slices = int.TryParse(Environment.GetEnvironmentVariable("BASTION_TROZOS"), out int n) ? n : 24;
            var prefabs = Prefabs();
            var root = new GameObject("Coches").transform;
            var log = new System.Text.StringBuilder();
            foreach (string id in ids)
            {
                var car = MapBuilder.Prop(root, id, prefabs);
                if (car == null) continue;
                var space = car.transform;
                log.AppendLine($"== {id} ({MapArt.Prop(id)?.pack}:{MapArt.Prop(id)?.fbx})");
                var parts = new List<(string name, Vector3[] v, int[] t)>();
                foreach (var f in car.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = f.sharedMesh;
                    if (mesh == null) continue;
                    var v = mesh.vertices.Select(p => space.InverseTransformPoint(f.transform.TransformPoint(p))).ToArray();
                    var b = new Bounds(v[0], Vector3.zero);
                    foreach (var p in v) b.Encapsulate(p);
                    var r = f.GetComponent<Renderer>();
                    log.AppendLine(FormattableString.Invariant($"  {f.name}: {v.Length} vertices, {mesh.subMeshCount} submeshes ({string.Join(", ", r.sharedMaterials.Select(m => m == null ? "null" : m.name))}), x {b.min.x:0.00}..{b.max.x:0.00} y {b.min.y:0.00}..{b.max.y:0.00} z {b.min.z:0.00}..{b.max.z:0.00}"));
                    parts.Add((f.name, v, mesh.triangles));
                }
                var body = parts.OrderByDescending(p => p.v.Length).First();
                var glass = parts.FirstOrDefault(p => p.name.Contains("Glass") && !p.name.Contains("Door"));
                float z0 = body.v.Min(p => p.z), z1 = body.v.Max(p => p.z);
                for (int k = 0; k < slices; k++)
                {
                    float a = z0 + (z1 - z0) * k / slices, c = z0 + (z1 - z0) * (k + 1) / slices;
                    var inBody = body.v.Where(p => p.z >= a && p.z < c).ToList();
                    var inGlass = glass.v?.Where(p => p.z >= a && p.z < c).ToList() ?? new List<Vector3>();
                    log.AppendLine(FormattableString.Invariant($"  z {a:0.00}..{c:0.00}: body top {(inBody.Count > 0 ? inBody.Max(p => p.y) : -1):0.00}, glass {(inGlass.Count > 0 ? $"{inGlass.Min(p => p.y):0.00}..{inGlass.Max(p => p.y):0.00}" : "-")}"));
                }
            }
            Debug.Log("[perfiles]\n" + log);
        }

        /// <summary>
        /// Each whole car from its right side (CarProfiles' frame: the nose to the right of the picture), straight on, in two
        /// columns, each over a grid of 10 cm (the lines of 50 cm red, z = 0 and y = 0 blue): to read off where its boot or
        /// hatch is hinged and how low it goes.
        /// </summary>
        public static void CarSides()
        {
            Lighting(true);
            var ids = Environment.GetEnvironmentVariable("BASTION_COCHES_ENTEROS")?.Split(',') ?? new[] { "carSedan", "carSmall", "carMedium", "carMuscle", "carTaxi", "carVan", "carConvertible", "carPickup", "carUte", "carHatch", "sirenPolice", "carOpen" };
            var prefabs = Prefabs();
            var root = new GameObject("Coches").transform;
            Material Flat(Color c)
            {
                var m = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Plain, "Rejilla");
                m.SetColor("_BaseColor", c);
                return m;
            }
            Material paper = Flat(new Color(0.92f, 0.92f, 0.9f)), thin = Flat(new Color(0.55f, 0.55f, 0.55f)), half = Flat(new Color(0.8f, 0.15f, 0.1f)), zero = Flat(new Color(0.1f, 0.25f, 0.85f));
            void Quad(Vector3 at, Vector2 size, Material m)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.transform.SetParent(root, false);
                q.transform.position = at;
                q.transform.rotation = Quaternion.Euler(0, -90, 0);
                q.transform.localScale = new Vector3(size.x, size.y, 1);
                q.GetComponent<Renderer>().sharedMaterial = m;
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            const int rows = 6;
            for (int k = 0; k < ids.Length; k++)
            {
                var car = MapBuilder.Prop(root, ids[k], prefabs);
                if (car == null) continue;
                var at = new Vector3(0, (rows - 1 - k % rows) * 1.9f, (k / rows) * 3.8f);
                car.transform.position = at;
                Quad(at + new Vector3(-1.5f, 0.8f, 0), new Vector2(3.4f, 1.7f), paper);
                for (int i = -17; i <= 17; i++)
                    Quad(at + new Vector3(-1.49f, 0.8f, i * 0.1f), new Vector2(i == 0 || i % 5 == 0 ? 0.012f : 0.005f, 1.7f), i == 0 ? zero : i % 5 == 0 ? half : thin);
                for (int i = 0; i <= 16; i++)
                    Quad(at + new Vector3(-1.49f, i * 0.1f, 0), new Vector2(3.4f, i == 0 || i % 5 == 0 ? 0.012f : 0.005f), i == 0 ? zero : i % 5 == 0 ? half : thin);
            }
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = rows * 1.9f / 2;
            camera.transform.SetPositionAndRotation(new Vector3(20, (rows - 1) * 1.9f / 2 + 0.8f, 1.9f), Quaternion.Euler(0, -90, 0));
            camera.farClipPlane = 60;
            Lighting(true);
        }

        /// <summary>
        /// Models side by side as they come (BASTION_MODELOS=pack:fbx,…, MapArt's keys), 2 m apart, by day, the camera on them
        /// (BASTION_DIST tiles, 6): their size and parts in the log, to pick one for a prop.
        /// </summary>
        public static void Models()
        {
            Lighting(true);
            var keys = (Environment.GetEnvironmentVariable("BASTION_MODELOS") ?? "").Split(',').Where(k => k.Length > 0).ToArray();
            var prefabs = Prefabs();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.GetComponent<Renderer>().sharedMaterial = Bastion.Client.KitMaterials.New(Bastion.Client.KitMaterials.Plain, "Suelo");
            ground.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.5f));
            for (int i = 0; i < keys.Length; i++)
            {
                var model = prefabs(keys[i]);
                if (model == null)
                {
                    Debug.LogWarning($"[modelos] {keys[i]}: none");
                    continue;
                }
                var made = (GameObject)UnityEngine.Object.Instantiate(model);
                made.transform.position = new Vector3((i - (keys.Length - 1) / 2f) * 2, 0, 0);
                var renderers = made.GetComponentsInChildren<Renderer>();
                var box = renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Debug.Log(FormattableString.Invariant($"[modelos] {keys[i]} ({AssetDatabase.GetAssetPath(model)}): {box.size.x:0.00} × {box.size.y:0.00} × {box.size.z:0.00} m, from y {box.min.y:0.00}; {renderers.Length} parts: {string.Join(", ", renderers.Select(r => r.name + " [" + string.Join("+", r.sharedMaterials.Select(m => m != null ? m.name : "-")) + "]"))}"));
            }
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            float distance = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 6;
            MapSpace.Aim(camera, Vector3.zero, distance, 0);
            Lighting(true);
        }

        /// <summary>The weather a lab asks for (ClimaLab), over BASTION_CLIMA.</summary>
        internal static string WeatherOverride;

        /// <summary>The game's light (Bastion.Client.Mood), by day or by night.</summary>
        internal static void Lighting(bool day) => Bastion.Client.Mood.Show(day ? Bastion.Client.Mood.Day : Bastion.Client.Mood.Night);

        /// <summary>The map of `state` in a MapView of its own ("Mapa").</summary>
        internal static MapView Map(GameState state, string name)
        {
            var view = new GameObject("Mapa").AddComponent<MapView>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            // BASTION_JUNTAR=0: the still things left as they were made (MapBuilder.Still), to set the joined map against.
            bool join = MapBuilder.JoinStill;
            if (Environment.GetEnvironmentVariable("BASTION_JUNTAR") == "0") MapBuilder.JoinStill = false;
            var builder = view.Build(state, Prefabs(), Materials());
            MapBuilder.JoinStill = join;
            Debug.Log($"[map] {name}: {state.Width}×{state.Height}, {builder.Placed} props and {builder.Walls?.Laid} wall pieces in {clock.ElapsedMilliseconds} ms; missing: {string.Join(", ", builder.Missing.Concat(builder.Walls?.Missing ?? Enumerable.Empty<string>()).OrderBy(m => m))}");
            // BASTION_PROBAR=id[,id…]: each of those props' first one drawn: its parts, their materials and shaders, their boxes (a test).
            foreach (string probe in (Environment.GetEnvironmentVariable("BASTION_PROBAR") ?? "").Split(',').Where(k => k.Length > 0))
            {
                var found = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(o => o.name == probe).ToList();
                Debug.Log($"[probar] {probe}: {found.Count} drawn" + (found.Count > 0 ? ", on " + string.Join(" ", found.Select(o => { var t = MapSpace.WorldToTile(o.position); return $"{t.x},{t.y}"; })) : ""));
                if (found.Count == 0) continue;
                foreach (var r in found[0].GetComponentsInChildren<Renderer>(true))
                    Debug.Log($"[probar]   {r.name} ({r.GetType().Name}, on {r.enabled && r.gameObject.activeInHierarchy}): {string.Join(", ", r.sharedMaterials.Select(m => m == null ? "null" : $"{m.name} [{m.shader.name}, queue {m.renderQueue}, cutoff {(m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : -1):0.##}]"))}; box {r.bounds.size:F2}; mesh {(r.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount ?? 0)} vertices");
            }
            return view;
        }

        static void Build(bool day)
        {
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            // BASTION_MEDICAL=clinic|ward|hospital: the mission 0.10.1's referencia.html?medical= makes (city, seed 7 or
            // BASTION_SEED), generated here: the engines are the same to the bit.
            string medical = Environment.GetEnvironmentVariable("BASTION_MEDICAL");
            // BASTION_GENERAR=biome,seed[,kind[,site]]: the mission referencia.html?biome=…&seed=…&kind=… makes (difficulty 3), not a
            // recorded one; with a site, its estate or roadside (Sites: industrial, roadside).
            var made = Environment.GetEnvironmentVariable("BASTION_GENERAR")?.Split(',');
            var state = medical != null ? Medical(medical) : made != null && made.Length >= 2 ? Generated(made[0], int.Parse(made[1]), made.Length > 2 ? made[2] : "elimination", made.Length > 3 ? made[3] : null) : Golden(name);
            // BASTION_CLIMA=drizzle|downpour|storm|snowfall|blizzard (or ClimaLab's WeatherOverride): the mission's rain or snow.
            if ((WeatherOverride ?? Environment.GetEnvironmentVariable("BASTION_CLIMA")) is string weather) state.Weather = weather;
            // BASTION_ABRIR=x,y;…: those doors open (a door's look open, its leaf swung).
            foreach (string spec in (Environment.GetEnvironmentVariable("BASTION_ABRIR") ?? "").Split(';').Where(v => v.Length > 0))
            {
                var xy = spec.Split(',').Select(int.Parse).ToArray();
                int door = xy[1] * state.Width + xy[0];
                if (state.Tiles[door] == TileKinds.Door) state.Tiles[door] = TileKinds.DoorOpen;
            }
            // BASTION_NIEVE=1: a winter map (GameState.snow, as the campaign's winter: SnowCover), 0 not one.
            if (Environment.GetEnvironmentVariable("BASTION_NIEVE") is string snowy) state.Snow = snowy == "1";
            // BASTION_DENTRO=x,y: the squad's first soldier there (in a building, to see it cut away round them: BASTION_SELECT=1).
            if (Environment.GetEnvironmentVariable("BASTION_DENTRO")?.Split(',').Select(int.Parse).ToArray() is int[] inside && inside.Length == 2
                && state.Units.FirstOrDefault(u => u.Alive && u.Team == "xcom") is Unit first)
                first.Pos = new Pos { X = inside[0], Y = inside[1], Z = 0 };
            // BASTION_BENGALAS=x,y,radius;…: Bengalas (radius 3 or more) and fires (less) burning there, lighting what they reach
            // as the engine's (Los.LightTiles from the lamps' height), for the night's glow and pools.
            foreach (string spec in (Environment.GetEnvironmentVariable("BASTION_BENGALAS") ?? "").Split(';').Where(v => v.Length > 0))
            {
                var v = spec.Split(',').Select(k => double.Parse(k, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                var tile = new Pos { X = (int)v[0], Y = (int)v[1] };
                (state.Lights ??= new List<Bastion.Engine.Light>()).Add(new Bastion.Engine.Light { Id = $"lab{state.Lights.Count}", Tile = tile, Radius = v[2], Turns = 3, Tiles = Los.LightTiles(state, tile, v[2], Content.Night.LampHeight) });
            }
            // BASTION_REGISTRADO=x0,y0,x1,y1: every cover tile in there on the ground searched (the furniture opened, a car's boot up).
            var searched = Environment.GetEnvironmentVariable("BASTION_REGISTRADO")?.Split(',').Select(int.Parse).ToArray();
            if (searched != null && searched.Length == 4)
                for (int y = searched[1]; y <= searched[3]; y++)
                    for (int x = searched[0]; x <= searched[2]; x++)
                        if (state.Tiles[y * state.Width + x] == "low" || state.Tiles[y * state.Width + x] == "high")
                        {
                            (state.Searched ??= new List<int>()).Add(y * state.Width + x);
                            Debug.Log($"[registrado] {x},{y} {state.Tiles[y * state.Width + x]}: {Bastion.Engine.Decor.DecorOf(state.Decor[y * state.Width + x])?.Kind}");
                        }
            // BASTION_CONSOLA=x,y: a hack's terminal on tile (x, y) of the ground (the objective of a hack mission).
            if (Environment.GetEnvironmentVariable("BASTION_CONSOLA")?.Split(',').Select(int.Parse).ToArray() is int[] console && console.Length == 2)
                state.Tiles[console[1] * state.Width + console[0]] = "console";
            Bastion.Client.NightLights.StateForLab = state;
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, !day));
            // BASTION_ATLAS=1: each building's walls in one of its pack's twelve atlases, by the building (Ana's #3, 1/11: the
            // houses all of one colour), to weigh it.
            WallsView.MaterialOf = Environment.GetEnvironmentVariable("BASTION_ATLAS") == "1" ? AtlasVariants() : null;
            var view = Map(state, name);
            var root = view.transform;
            // BASTION_FIGURAS=1: the game's units drawn on it too, standing (to weigh their light against 0.10.1's figures);
            // =vistas, those the squad sees alone, as in the game.
            string figures = Environment.GetEnvironmentVariable("BASTION_FIGURAS");
            // BASTION_MUERTO=x,y: the first enemy lying dead there (a body to search: SearchOutline).
            if (Environment.GetEnvironmentVariable("BASTION_MUERTO")?.Split(',').Select(int.Parse).ToArray() is int[] dead && dead.Length == 2
                && state.Units.FirstOrDefault(u => u.Alive && u.Team == Teams.Alien && !u.Objective) is Unit corpse)
            {
                corpse.Pos = new Pos { X = dead[0], Y = dead[1], Z = 0 };
                corpse.Alive = false;
                corpse.Hp = 0;
                Debug.Log($"[contorno] {corpse.Id} ({corpse.Template}) lies dead on {dead[0]},{dead[1]}");
            }
            if (figures == "1" || figures == "vistas")
            {
                var units = new GameObject("Unidades").AddComponent<Bastion.Client.Units.UnitViews>();
                units.Sync(state);
                // (The dead too, seen or not: as 0.10.1's reference block draws them.)
                if (figures == "1") foreach (var u in state.Units) units.Reveal(u.Id);
                units.MasksNow();
            }
            // BASTION_CONTORNO=x,y,lit;…: SearchOutline's lines round what would be searched on those tiles (1 lit, 0 not), in
            // UI's bone and grey (CombatController.SearchLit, SearchFar); in play mode (-shotPlay), once UnitViews.Current is set.
            var outlined = (Environment.GetEnvironmentVariable("BASTION_CONTORNO") ?? "").Split(';').Where(v => v.Length > 0)
                .Select(v => v.Split(',').Select(int.Parse).ToArray()).Select(v => (new Pos { X = v[0], Y = v[1], Z = 0 }, v.Length < 3 || v[2] != 0)).ToList();
            if (outlined.Count > 0)
            {
                ColorUtility.TryParseHtmlString("#f3ecdc", out var lit);
                ColorUtility.TryParseHtmlString("#7d868c", out var far);
                void Outline(PlayModeStateChange change)
                {
                    if (change != PlayModeStateChange.EnteredPlayMode) return;
                    EditorApplication.playModeStateChanged -= Outline;
                    Bastion.Client.Units.SearchOutline.Show(state, outlined, lit, far);
                    Debug.Log($"[contorno] {outlined.Count} tiles outlined");
                    // (What each tile has drawn, outlined: its things, their lines' copies, and how high its top is.)
                    foreach (var (at, _) in outlined)
                    {
                        var things = MapView.Current?.Builder?.On(at) ?? new List<GameObject>();
                        int copies = things.Sum(o => o.GetComponentsInChildren<Transform>(true).Count(t => t.name.EndsWith(" (contorno)")));
                        Debug.Log($"[contorno] {at.X},{at.Y}: {Bastion.Engine.Grid.KindAt(state, at)}, {string.Join(" ", things.Select(o => o.name))}; {copies} copies; top {Bastion.Client.Units.SearchOutline.TopOf(state, at)?.ToString("0.00") ?? "none"}");
                    }
                }
                EditorApplication.playModeStateChanged += Outline;
            }
            // BASTION_AO_FILA=y,x0,x1: the contact occlusion (r) and its height (g) across row y from tile x0 to x1, texel by
            // texel (6 a tile), with the row's tiles (to set against 0.10.1's: what a floor by a wall gets).
            var aoRow = Environment.GetEnvironmentVariable("BASTION_AO_FILA")?.Split(',').Select(int.Parse).ToArray();
            if (aoRow != null && aoRow.Length == 3) AoRow(state, aoRow[0], aoRow[1], aoRow[2]);
            // BASTION_AO_VOLCADO=file: the whole contact occlusion, each tile's mean of r (rows north to south), as text (UI's).
            if (Environment.GetEnvironmentVariable("BASTION_AO_VOLCADO") is string aoDump) AoDump(state, aoDump);
            // BASTION_SIN_AO=1: the buildings' floors without the contact occlusion (to tell what it draws).
            Shader.SetGlobalFloat("_GroundAOOff", Environment.GetEnvironmentVariable("BASTION_SIN_AO") == "1" ? 1 : 0);
            // BASTION_LEVEL: the storey in view (UI's controller decides it in the game).
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_LEVEL"), out int level)) view.ShowLevel(level);
            // The cutaway's focus (UI's controller sets it in the game): BASTION_SELECT=1 the squad's first soldier selected,
            // BASTION_HOVER=x,y,z the cursor there, BASTION_ROOFS=1 the roofs on (the camera drawn back).
            var cutaway = view.GetComponent<Cutaway>();
            if (cutaway != null)
            {
                if (Environment.GetEnvironmentVariable("BASTION_SELECT") == "1") cutaway.Focus.selected = state.Units.FirstOrDefault(u => u.Alive && u.Team == "xcom")?.Id;
                var hover = Environment.GetEnvironmentVariable("BASTION_HOVER")?.Split(',');
                if (hover != null && hover.Length >= 2)
                {
                    // The cursor's storey the active one, as the controller has it.
                    cutaway.Focus.hover = new Pos { X = int.Parse(hover[0]), Y = int.Parse(hover[1]), Z = hover.Length > 2 ? int.Parse(hover[2]) : 0 };
                    cutaway.Focus.level = cutaway.Focus.hover.Z;
                }
                cutaway.Focus.roofs = Environment.GetEnvironmentVariable("BASTION_ROOFS") == "1";
                Debug.Log($"[map] cutaway focus: selected {cutaway.Focus.selected ?? "-"}, hover {(cutaway.Focus.hover == null ? "-" : $"{cutaway.Focus.hover.X},{cutaway.Focus.hover.Y},{cutaway.Focus.hover.Z}")}");
            }
            // BASTION_HIDE=glass,signs,shutters,crowns: those left out of the picture (to tell what draws what; crowns, the
            // trees with one, TreeFade.Crowns: to see what walks under the pines).
            var hide = (Environment.GetEnvironmentVariable("BASTION_HIDE") ?? "").Split(',');
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                bool glass = r.sharedMaterial != null && r.sharedMaterial.name == "Cristal";
                bool sign = r.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("sign") || t.name == "motelSign");
                bool shutter = r.GetComponentsInParent<Transform>().Any(t => t.name == "rollerShutter");
                bool crown = hide.Contains("crowns") && r.GetComponentsInParent<Transform>().Any(t => TreeFade.Crowns.Contains(t.name));
                if ((glass && hide.Contains("glass")) || (sign && hide.Contains("signs")) || (shutter && hide.Contains("shutters")) || crown) r.enabled = false;
            }
            // BASTION_SIN_SOMBRA=a,b: what is drawn under a part named so (its path holds it: "Tejados", "Planta 1") casts no shadow.
            var unshaded = (Environment.GetEnvironmentVariable("BASTION_SIN_SOMBRA") ?? "").Split(',').Where(k => k.Length > 0).ToArray();
            if (unshaded.Length > 0)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    // ("a/b": under a part named a and one named b, both.)
                    var names = r.GetComponentsInParent<Transform>().Select(t => t.name).ToList();
                    if (unshaded.Any(k => k.Split('/').All(names.Contains))) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            // BASTION_SOMBRAS=x,y: what is drawn over tile (x, y), up to 4 storeys, and whether it casts a shadow (what lets the
            // sun into a cut building or not).
            var shade = Environment.GetEnvironmentVariable("BASTION_SOMBRAS")?.Split(',');
            if (shade != null && shade.Length >= 2)
            {
                var over = MapSpace.World(int.Parse(shade[0]), int.Parse(shade[1]));
                foreach (var r in root.GetComponentsInChildren<Renderer>(true).Where(r => r.bounds.min.x <= over.x && r.bounds.max.x >= over.x && r.bounds.min.z <= over.z && r.bounds.max.z >= over.z && r.bounds.max.y > 0.5f))
                    Debug.Log(FormattableString.Invariant($"[sombras] {string.Join("/", r.GetComponentsInParent<Transform>().Reverse().Skip(1).Select(t => t.name))}: {r.shadowCastingMode}, on {r.enabled && r.gameObject.activeInHierarchy}, {r.sharedMaterial?.shader.name}, y {r.bounds.min.y:0.0}–{r.bounds.max.y:0.0}"));
            }
            // Lights brought in with the models (none should be: the map's light is the scene's).
            var lights = root.GetComponentsInChildren<UnityEngine.Light>(true);
            if (lights.Length > 0) Debug.LogWarning("[map] lights in the map: " + string.Join("; ", lights.GroupBy(l => $"{l.transform.parent?.name}/{l.name} {l.type} {l.color}").Select(g => $"{g.Key} ×{g.Count()}")));
            // The cars' models and materials, to check their paints.
            var cars = root.GetComponentsInChildren<Transform>().Where(t => t.parent != null && t.parent.name == "Decorado" && t.name.StartsWith("car"));
            Debug.Log("[map] cars: " + string.Join("; ", cars.GroupBy(c => c.name + " " + string.Join("+", c.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m != null ? m.name : "-").Distinct())).Select(g => $"{g.Key} ×{g.Count()}")));

            // 0.10.1's capture view (reference.ts VIEWS.capture): over the middle, 40 tiles off, the camera's first turn.
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            var target = new Vector3(state.Width * MapSpace.Tile / 2, 0, -state.Height * MapSpace.Tile / 2);
            MapSpace.Aim(camera, target, 40);
            // Its background the haze, the fill light along it (its weather's, if it has one).
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, !day));
            // BASTION_CHANGE=0|1: the map as a mission changes it (a door opened, a car's tile cleared, an upper floor's tile
            // given way), the camera on the door; with 1 the changes made (MapView.Rebuild), with 0 not, to compare.
            string change = Environment.GetEnvironmentVariable("BASTION_CHANGE");
            if (change != null)
            {
                int door = state.Tiles.FindIndex(t => t == "door");
                var next = Golden(name);
                var car = next.Decor?.FindIndex(d => Bastion.Engine.Decor.DecorOf(d)?.Kind == "carFront") ?? -1;
                if (door >= 0) next.Tiles[door] = "doorOpen";
                if (car >= 0) next.Tiles[car] = "floor";
                int broken = next.Levels != null && next.Levels.Count > 0 ? next.Levels[0].Tiles.FindIndex(t => t == "floor") : -1;
                if (broken >= 0) next.Levels[0].Tiles[broken] = null;
                if (change == "1")
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    int redone = view.Rebuild(next);
                    Debug.Log($"[map] changes: door {door}, car {car}, floor {broken}: {redone} tiles made again in {watch.ElapsedMilliseconds} ms");
                }
                if (door >= 0) MapSpace.Aim(camera, MapSpace.World(door % state.Width, door / state.Width), 12);
            }
            // BASTION_SQUAD=1: the game's camera on the squad, 24 tiles off.
            if (Environment.GetEnvironmentVariable("BASTION_SQUAD") == "1")
            {
                var squad = state.Units.Where(u => u.Alive && u.Team == "xcom").Select(u => MapSpace.TileToWorld(state, u.Pos)).ToList();
                if (squad.Count > 0) MapSpace.Aim(camera, squad.Aggregate(Vector3.zero, (a, b) => a + b) / squad.Count, 24);
            }
            // BASTION_NIEVE_TEJADOS=0: the buildings without their snow (Bastion/Building's Snow), to set it against.
            if (Environment.GetEnvironmentVariable("BASTION_NIEVE_TEJADOS") == "0") Shader.SetGlobalVector("_SnowSize", Vector4.zero);
            // BASTION_FOG=1: the fog of war as the squad sees from where it stands in the state.
            if (Environment.GetEnvironmentVariable("BASTION_FOG") == "1")
            {
                var fog = new GameObject("Niebla").AddComponent<Fog>();
                fog.Reset(state);
                fog.Show(state, true);
                // BASTION_FOG=all: everything in sight (0.10.1's reference block); the lit tiles and the night as they are.
                if (Environment.GetEnvironmentVariable("BASTION_FOG_ALL") == "1") fog.SeeAll();
                var squad = state.Units.Where(u => u.Alive && u.Team == "xcom").Select(u => $"{u.Id} ({u.Pos.X},{u.Pos.Y},{u.Pos.Z})");
                Debug.Log($"[map] fog: {fog.Counts().sight} tiles in sight, {fog.Counts().known} seen of {state.Width * state.Height}; squad {string.Join(", ", squad)}");
            }
            // BASTION_AT=x,y,distance[,storey[,yaw]]: on tile (x, y) (on that storey's floor), so many tiles off (reference.ts:
            // rig.focus on the tile's middle), the camera turned so (degrees, 45 the game's first turn).
            var at = Environment.GetEnvironmentVariable("BASTION_AT")?.Split(',');
            // (BASTION_POLICIA=1 without BASTION_AT: on the police car, BASTION_DIST tiles off, 12.)
            if (at == null && Environment.GetEnvironmentVariable("BASTION_POLICIA") == "1" && state.Mission?.PoliceCar is System.Collections.Generic.List<int> police && police.Count > 0)
            {
                Debug.Log($"[policia] the police car: its nose on {police[0] % state.Width},{police[0] / state.Width}, its boot on {police[^1] % state.Width},{police[^1] / state.Width}");
                at = new[] { (police[0] % state.Width).ToString(), (police[0] / state.Width).ToString(), Environment.GetEnvironmentVariable("BASTION_DIST") ?? "12" };
            }
            if (at != null && at.Length >= 3)
                MapSpace.Aim(camera, MapSpace.World(int.Parse(at[0]), int.Parse(at[1]), at.Length > 3 ? int.Parse(at[3]) : 0), float.Parse(at[2], System.Globalization.CultureInfo.InvariantCulture),
                    at.Length > 4 ? float.Parse(at[4], System.Globalization.CultureInfo.InvariantCulture) : 45);
            // BASTION_ARRIBA=1: over the whole map as the battle's «Mapa» (M) puts the camera (CameraRig.EnterOverhead: 89°
            // down, far enough for the map, CombatController.MiniMap's room to spare).
            if (Environment.GetEnvironmentVariable("BASTION_ARRIBA") == "1")
            {
                var centre = MapSpace.World(state.Width / 2, state.Height / 2);
                float w = state.Width * MapSpace.Tile, h = state.Height * MapSpace.Tile;
                float tan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float metres = Mathf.Max(h / 2 / tan, w / 2 / (tan * camera.aspect)) / 0.86f;
                float up = 89 * Mathf.Deg2Rad;
                camera.transform.position = centre + new Vector3(0, Mathf.Sin(up) * metres, -Mathf.Cos(up) * metres);
                camera.transform.LookAt(centre);
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, metres * 1.5f);
                Debug.Log(FormattableString.Invariant($"[map] from above: {metres:0} m up, far plane {camera.farClipPlane:0} m"));
            }
            // The sun's shadows over what the camera sees, as the battle's map has them (ShadowFit); BASTION_SOMBRAS_FIJAS=1, the
            // pipeline's own (50 m), to weigh them.
            if (Environment.GetEnvironmentVariable("BASTION_SOMBRAS_FIJAS") != "1")
            {
                var fit = camera.gameObject.AddComponent<Bastion.Client.ShadowFit>();
                fit.Camera = camera;
                fit.Fit();
                // BASTION_PARTES=a,b,c[,far]: the four cascades' splits (and the shadows' reach, m) put by hand, to try them.
                var parts = Environment.GetEnvironmentVariable("BASTION_PARTES")?.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                if (parts != null && parts.Length >= 3)
                {
                    fit.enabled = false;
                    var asset = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.QualitySettings.renderPipeline;
                    asset.cascade4Split = new Vector3(parts[0], parts[1], parts[2]);
                    if (parts.Length > 3) asset.shadowDistance = parts[3];
                }
                // BASTION_ESFERA=0|1: URP's conservative enclosing sphere for the cascades off or on, to try it.
                var sphere = Environment.GetEnvironmentVariable("BASTION_ESFERA");
                if (sphere != null)
                {
                    var asset = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.QualitySettings.renderPipeline;
                    asset.conservativeEnclosingSphere = sphere == "1";
                }
                var urp = (UnityEngine.QualitySettings.renderPipeline ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline) as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                Debug.Log(FormattableString.Invariant($"[map] shadows fitted: ground seen {fit.Near:0.#}–{fit.Far / 1.08f:0.#} m deep, to {urp?.shadowDistance:0.#} m, {urp?.shadowCascadeCount} cascades at {urp?.cascade4Split}"));
            }
            // BASTION_SIN_SOMBRAS=1: the sun's shadows off (after the light is put for the last time: against a picture with them, where each one falls).
            if (Environment.GetEnvironmentVariable("BASTION_SIN_SOMBRAS") == "1")
                foreach (var l in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(FindObjectsSortMode.None)) l.shadows = LightShadows.None;
            // BASTION_SHADOWTEST=x,y: a cube 2 m over tile (x, y), to see whose shadow falls where.
            var shadowTest = Environment.GetEnvironmentVariable("BASTION_SHADOWTEST")?.Split(',');
            if (shadowTest != null && shadowTest.Length >= 2)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = MapSpace.World(int.Parse(shadowTest[0]), int.Parse(shadowTest[1])) + Vector3.up * 2.5f;
                cube.transform.localScale = Vector3.one * 0.8f;
            }
            // BASTION_NAMED=rug: every model whose name starts so, its box in the world.
            string named = Environment.GetEnvironmentVariable("BASTION_NAMED");
            if (named != null)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(named) && t.parent != null && t.parent.name == "Decorado"))
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    var box = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }) : new Bounds(t.position, Vector3.zero);
                    Debug.Log($"[map] {t.name} at tile {MapSpace.WorldToTile(box.center)}: y {box.min.y:0.###}–{box.max.y:0.###} m, active {t.gameObject.activeInHierarchy}, {rs.Length} renderers");
                }
            // BASTION_CARS=1: every car the map has, its facing and which way its model's long side lies.
            if (Environment.GetEnvironmentVariable("BASTION_CARS") == "1" && state.Decor != null)
                foreach (Transform car in root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("car") && t.parent != null && t.parent.name == "Decorado"))
                {
                    var box = car.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    var tile = MapSpace.WorldToTile(box.center);
                    var thing = Bastion.Engine.Decor.ObjectAt(state.Decor, state.Width, tile.y * state.Width + tile.x);
                    Debug.Log($"[map] car {car.name} at {tile.x},{tile.y}: facing {thing?.Facing.ToString() ?? "?"} ({thing?.Tiles.Count ?? 0} tiles), long side {(box.size.x > box.size.z ? "east–west" : "north–south")}");
                }
            // BASTION_THING=i: what the engine makes of the object over ground tile i (its tiles, anchor, facing).
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_THING"), out int thingAt) && state.Decor != null)
            {
                var thing = Bastion.Engine.Decor.ObjectAt(state.Decor, state.Width, thingAt);
                Debug.Log(thing == null ? $"[map] no object at {thingAt}" : $"[map] object at {thingAt}: {thing.Kind} facing {thing.Facing}, anchor {thing.Anchor}, tiles {string.Join(" ", thing.Tiles)}");
                // What stands there: each model's box in the world (its long side along x is east–west).
                var here = MapSpace.World(thingAt % state.Width, thingAt / state.Width);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    if (Vector3.Distance(new Vector3(r.bounds.center.x, 0, r.bounds.center.z), new Vector3(here.x, 0, here.z)) < 3 * MapSpace.Tile && r.transform.root == root.root)
                        Debug.Log($"[map]   {r.transform.parent?.name}/{r.name}: centre {r.bounds.center}, size {r.bounds.size}");
            }
            // BASTION_FIND=<decor kind>: close on the first tile of the ground's decor of that kind, 10 tiles off.
            string find = Environment.GetEnvironmentVariable("BASTION_FIND");
            if (find != null && state.Decor != null)
            {
                int i = state.Decor.FindIndex(d => Bastion.Engine.Decor.DecorOf(d)?.Kind == find);
                if (i < 0) Debug.LogWarning($"[map] no {find} on the ground");
                else
                {
                    MapSpace.Aim(camera, MapSpace.World(i % state.Width, i / state.Width), 10);
                    Debug.Log($"[map] {find} at ({i % state.Width},{i / state.Width})");
                }
            }
            // BASTION_TOP=1: straight down on the whole map, north up (to lay it over 0.10.1's own).
            if (Environment.GetEnvironmentVariable("BASTION_TOP") == "1")
            {
                camera.orthographic = true;
                camera.orthographicSize = state.Height * MapSpace.Tile / 2;
                camera.transform.SetPositionAndRotation(target + Vector3.up * 200, Quaternion.Euler(90, 0, 0));
            }
        }

        /// <summary>
        /// A building's own atlas of its pack (Town's, City's, Apocalypse's, Office's: 01_A to 04_C, siding, roofs, brick
        /// and trims in other colours) for the 01_A its pieces are drawn in, picked by the building; a copy of the
        /// material each.
        /// </summary>
        static Func<Material, int, Material> AtlasVariants()
        {
            var packs = new Dictionary<string, string>
            {
                ["PolygonTown_01_A"] = "Assets/Synty/PolygonTown/Textures/Alts/PolygonTown_{0}.png",
                ["PolygonCity_01_A"] = "Assets/Synty/PolygonCity/Textures/PolygonCity_{0}.png",
                ["PolygonApocalypse_01_A"] = "Assets/Synty/PolygonApocalypse/Textures/Alt/PolygonApocalypse_{0}.png",
                ["PolygonOffice_01_A"] = "Assets/Synty/PolygonOffice/Textures/PolygonOffice_{0}.png",
            };
            string[] codes = { "01_A", "01_B", "01_C", "02_A", "02_B", "02_C", "03_A", "03_B", "03_C", "04_A", "04_B", "04_C" };
            var made = new Dictionary<(Material, int), Material>();
            return (m, b) =>
            {
                if (m == null || !packs.TryGetValue(m.name, out var path)) return m;
                int k = (int)(((uint)(b + 1) * 2654435761u >> 16) % (uint)codes.Length);
                if (made.TryGetValue((m, k), out var copy)) return copy;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(path, codes[k]));
                if (texture == null) return made[(m, k)] = m;
                copy = new Material(m) { name = $"{m.name} ({codes[k]})" };
                copy.SetTexture("_BaseMap", texture);
                Debug.Log($"[map] atlas {codes[k]} for {m.name}");
                return made[(m, k)] = copy;
            };
        }

        /// <summary>A Synty material by its name, the first time it is asked for.</summary>
        internal static Func<string, Material> Materials()
        {
            var loaded = new Dictionary<string, Material>();
            return name =>
            {
                if (loaded.TryGetValue(name, out var m)) return m;
                string guid = AssetDatabase.FindAssets($"{name} t:Material", new[] { "Assets/Synty" })
                    .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
                return loaded[name] = guid != null ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)) : null;
            };
        }

        /// <summary>
        /// 0.10.1's models (from the packs' source FBX) that Unity's packs make otherwise: the street pot with its plant is
        /// SM_Prop_Pot_Street_01's prefab (the plant a child of it); the clinic's gurney and bed, rigged in the source, are
        /// still meshes here.
        /// </summary>
        static readonly Dictionary<string, string> UnityNames = new Dictionary<string, string>
        {
            ["SM_Prop_Pot_Street_Plant_01"] = "SM_Prop_Pot_Street_01",
            ["SK_Prop_Bed_Gurney_01"] = "SM_Prop_Bed_Gurney_01",
            ["SK_Prop_Bed_Hospital_01"] = "SM_Prop_Bed_Hospital_01",
            // The caravan (the Bastion's first stage: basePieces.ts), rigged in the source for its door.
            ["SK_Veh_Caravan_01"] = "SM_Veh_Caravan_01",
        };

        /// <summary>
        /// The quarter turns a model stands from its source FBX, by where it comes from (MapArt.UnityTurn): Apocalypse's
        /// Unity package, one; any other package's, or a source FBX in Extra (as it is, or cut by Blender), two.
        /// </summary>
        internal static int UnityTurnOf(string path) => path.StartsWith("Assets/Synty/PolygonApocalypse/") ? 1 : 2;

        /// <summary>The packs' folders in Assets/Synty, by MapArt.json's pack names.</summary>
        /// <summary>Packs found last by a model's name alone (Prefabs): imported after the rest, with models of the others' names.</summary>
        static readonly string[] LatePacks = { "PolygonShops", "PolygonGangWarfare", "PolygonPalmCity" };

        internal static readonly Dictionary<string, string> PackFolders = new Dictionary<string, string>
        {
            ["apocalypse"] = "PolygonApocalypse", ["city"] = "PolygonCity", ["town"] = "PolygonTown", ["office"] = "PolygonOffice",
            ["military"] = "PolygonMilitary", ["construction"] = "PolygonConstruction", ["farm"] = "PolygonFarm", ["nature"] = "PolygonNature",
            ["alpine"] = "PolygonNatureBiomes", ["industrial"] = "PolygonMapsMilitaryWarehouse", ["woodland"] = "PolygonMapsWoodlandApocalypse",
            ["dog"] = "PolygonDog", ["shops"] = "PolygonShops", ["gang"] = "PolygonGangWarfare", ["simple"] = "SimpleForestAnimal", ["cats"] = "SimpleCats",
        };

        /// <summary>
        /// Every Synty prefab by its name (a model where there is no prefab), loaded the first time it is asked for; or by a
        /// prop's key (MapArt.ModelKey, `pack:fbx`), from that pack's folder, since several packs have models of the same
        /// name (Farm's and Apocalypse's wooden gate). The source's own models (SourceProps, in Extra) come before the
        /// packages' of the same name: Unity's can hold other parts that 0.10.1 draws apart (the house's glass, the trap's jaws).
        /// </summary>
        internal static Func<string, GameObject> Prefabs()
        {
            var models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Synty" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Synty" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            // A name's models, the one asked by name alone first (as it always was: Extra's, else the last prefab, else the last model).
            var candidates = new Dictionary<string, List<string>>();
            foreach (string p in Enumerable.Reverse(models).Where(p => p.StartsWith("Assets/Synty/Extra/")).Concat(Enumerable.Reverse(prefabs)).Concat(Enumerable.Reverse(models)))
            {
                string name = Path.GetFileNameWithoutExtension(p);
                if (!candidates.TryGetValue(name, out var list)) candidates[name] = list = new List<string>();
                if (!list.Contains(p)) list.Add(p);
            }
            // (The packs brought in for the buildings on 1/11 last, by a name alone: they have models of the others' names,
            // and a pistol, a crowbar, a prop asked so would have become theirs. Asked by their pack, theirs.)
            foreach (var list in candidates.Values)
            {
                var late = list.Where(p => LatePacks.Any(f => p.StartsWith($"Assets/Synty/{f}/"))).ToList();
                if (late.Count == 0 || late.Count == list.Count) continue;
                list.RemoveAll(late.Contains);
                list.AddRange(late);
            }
            string PathOf(string asked)
            {
                int colon = asked.IndexOf(':');
                string pack = colon >= 0 ? asked.Substring(0, colon) : null;
                // Some of MapArt's names carry the pack's folder (Props/…): the file's name is what is looked up.
                string file = Path.GetFileName(colon >= 0 ? asked.Substring(colon + 1) : asked);
                if (UnityNames.TryGetValue(file, out var other)) file = other;
                if (!candidates.TryGetValue(file, out var list)) return null;
                if (pack == null) return list[0];
                return list.FirstOrDefault(p => p.StartsWith("Assets/Synty/Extra/"))
                    ?? (PackFolders.TryGetValue(pack, out var folder) ? list.FirstOrDefault(p => p.StartsWith($"Assets/Synty/{folder}/")) : null)
                    ?? list[0];
            }
            var loaded = new Dictionary<string, GameObject>();
            return name =>
            {
                if (name == null) return null;
                if (!loaded.TryGetValue(name, out var prefab))
                {
                    string p = PathOf(name);
                    loaded[name] = prefab = p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
                }
                return prefab;
            };
        }
    }
}
