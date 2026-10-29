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

        internal static GameState Generated(string biome, int seed, string kind) => Setup.CreateMission(new MissionOptions
        {
            Map = new MapSource { Kind = MapSourceKinds.Generated, Biome = biome, Seed = seed },
            Slots = new System.Collections.Generic.List<int> { 0 },
            Rng = new Rng((uint)seed),
            Kind = kind,
            Difficulty = 3,
        });

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

        /// <summary>The game's light (Bastion.Client.Mood), by day or by night.</summary>
        internal static void Lighting(bool day) => Bastion.Client.Mood.Show(day ? Bastion.Client.Mood.Day : Bastion.Client.Mood.Night);

        /// <summary>The map of `state` in a MapView of its own ("Mapa").</summary>
        internal static MapView Map(GameState state, string name)
        {
            var view = new GameObject("Mapa").AddComponent<MapView>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var builder = view.Build(state, Prefabs(), Materials());
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
            // BASTION_GENERAR=biome,seed[,kind]: the mission referencia.html?biome=…&seed=…&kind=… makes (difficulty 3), not a recorded one.
            var made = Environment.GetEnvironmentVariable("BASTION_GENERAR")?.Split(',');
            var state = medical != null ? Medical(medical) : made != null && made.Length >= 2 ? Generated(made[0], int.Parse(made[1]), made.Length > 2 ? made[2] : "elimination") : Golden(name);
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
            Lighting(day);
            var view = Map(state, name);
            var root = view.transform;
            // BASTION_FIGURAS=1: the game's units drawn on it too, standing (to weigh their light against 0.10.1's figures);
            // =vistas, those the squad sees alone, as in the game.
            string figures = Environment.GetEnvironmentVariable("BASTION_FIGURAS");
            if (figures == "1" || figures == "vistas")
            {
                var units = new GameObject("Unidades").AddComponent<Bastion.Client.Units.UnitViews>();
                units.Sync(state);
                // (The dead too, seen or not: as 0.10.1's reference block draws them.)
                if (figures == "1") foreach (var u in state.Units) units.ForceVisible(u.Id);
                units.MasksNow();
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
            // Its background the haze, the fill light along it.
            Lighting(day);
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
            // BASTION_AT=x,y,distance[,storey]: on tile (x, y) (on that storey's floor), so many tiles off (reference.ts:
            // rig.focus on the tile's middle).
            var at = Environment.GetEnvironmentVariable("BASTION_AT")?.Split(',');
            if (at != null && at.Length >= 3)
                MapSpace.Aim(camera, MapSpace.World(int.Parse(at[0]), int.Parse(at[1]), at.Length > 3 ? int.Parse(at[3]) : 0), float.Parse(at[2], System.Globalization.CultureInfo.InvariantCulture));
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
        static readonly Dictionary<string, string> PackFolders = new Dictionary<string, string>
        {
            ["apocalypse"] = "PolygonApocalypse", ["city"] = "PolygonCity", ["town"] = "PolygonTown", ["office"] = "PolygonOffice",
            ["military"] = "PolygonMilitary", ["construction"] = "PolygonConstruction", ["farm"] = "PolygonFarm", ["nature"] = "PolygonNature",
            ["alpine"] = "PolygonNatureBiomes", ["industrial"] = "PolygonMapsMilitaryWarehouse", ["woodland"] = "PolygonMapsWoodlandApocalypse",
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
