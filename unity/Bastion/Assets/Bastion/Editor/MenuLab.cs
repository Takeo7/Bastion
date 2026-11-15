using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The menu's backdrops (Bastion.Client.Menu.MenuBackdrop; Arturo's notes of 14/11, menu-inicial p1 and p2; Arte, 0.12.24).
    ///   Make: Resources/Menu/MenuArt.asset, the models the backdrops are built from (MenuBackdrop.Models), found in
    ///   Assets/Synty as the map's catalog finds its own (MapLab.Prefabs). Run it again when the models change:
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.MenuLab.Make. Road and Crossroads make it first if it
    ///   isn't up to date.
    ///   Road, Crossroads: the backdrop in an empty scene with a camera as the start screen's (Inicio's «Cámara», which it
    ///   turns off), for captura.sh with -shotCamera "Cámara del menú" (1920x1080 and 1280x720 for Arte's review; with
    ///   BASTION_MENU_OCULTA=1 it is shown and hidden again, and the start screen's camera draws). BASTION_MENU_CAM=x,y,z,yaw,
    ///   down,fov puts its camera elsewhere (to try a framing).
    ///   Coste: how long each backdrop takes to build (the first time, with its models loading, and again), and a frame's ms
    ///   at 1920×1080 on «Alto» (and each of BASTION_CALIDAD) as RendimientoLab measures them (its camera into a texture, a
    ///   pixel read back so the GPU's work is in it; the post-processing on, as the game's), against the start screen's
    ///   camera alone: unity/tools/unity.sh -executeMethod Bastion.EditorTools.MenuLab.Coste.
    ///   Muestras: the Synty models a backdrop could be built with, one a frame, each framed whole, their sizes in the log.
    /// </summary>
    public static class MenuLab
    {
        const string ArtAsset = "Assets/Bastion/Client/Resources/Menu/MenuArt.asset";

        public static void Make()
        {
            var prefabOf = MapLab.Prefabs();
            var keys = MenuBackdrop.Models.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var found = keys.Where(k => prefabOf(k) != null).ToList();
            var missing = keys.Except(found).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(ArtAsset));
            var art = AssetDatabase.LoadAssetAtPath<MenuArt>(ArtAsset);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<MenuArt>();
                AssetDatabase.CreateAsset(art, ArtAsset);
            }
            art.modelNames = found.ToArray();
            art.models = found.Select(prefabOf).ToArray();
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"[menu] {ArtAsset}: {art.models.Length} models" + (missing.Count > 0 ? "; missing: " + string.Join(", ", missing) : ""));
            // The textures they bring, the biggest first, and which models (as the editor counts them: its readable copies too).
            var deps = EditorUtility.CollectDependencies(new UnityEngine.Object[] { art });
            foreach (var texture in deps.OfType<Texture>().OrderByDescending(x => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(x)))
            {
                var users = found.Where(k => EditorUtility.CollectDependencies(new UnityEngine.Object[] { prefabOf(k) }).Contains(texture)).ToList();
                Debug.Log(FormattableString.Invariant($"[menu] {AssetDatabase.GetAssetPath(texture)} {texture.width}×{texture.height}: {UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture) / 1048576.0:0.0} MB, for {string.Join(", ", users)}"));
            }
        }

        /// <summary>MenuArt made again if it doesn't name what the backdrops use.</summary>
        static void Ready()
        {
            var art = AssetDatabase.LoadAssetAtPath<MenuArt>(ArtAsset);
            if (art == null || !art.modelNames.SequenceEqual(MenuBackdrop.Models.OrderBy(k => k, StringComparer.Ordinal))) Make();
        }

        public static void Road() => Shown(MenuBackdrop.Scene.Road);

        public static void Crossroads() => Shown(MenuBackdrop.Scene.Crossroads);

        static void Shown(MenuBackdrop.Scene s)
        {
            Ready();
            StartScreen();
            if (Environment.GetEnvironmentVariable("BASTION_MENU_JUEGO") == "1")
            {
                // Shown as the game shows it, once in play (-shotPlay): before, entering play put the snow's decals off
                // (SnowCover.Starting) and the 4×4's lamps came out dark. Shot from a camera of the lab's that follows its own.
                var follower = new GameObject("Cámara de la prueba").AddComponent<Camera>();
                follower.gameObject.AddComponent<InPlay>().scene = s;
                return;
            }
            MenuBackdrop.Show(s);
            if (Environment.GetEnvironmentVariable("BASTION_MENU_OCULTA") == "1")
            {
                MenuBackdrop.Hide();
                return;
            }
            // The shot's shape as the camera's (out of play, Shots draws it into a texture and leaves its aspect as it was:
            // its fov, as wide on any screen as on a 16:9 one, would be reckoned on another).
            var args = Environment.GetCommandLineArgs();
            int sized = Array.IndexOf(args, "-shotSize");
            if (sized >= 0 && sized + 1 < args.Length && args[sized + 1].Split('x') is string[] wh && wh.Length == 2)
                MenuBackdrop.Current.Eye.aspect = float.Parse(wh[0], CultureInfo.InvariantCulture) / float.Parse(wh[1], CultureInfo.InvariantCulture);
            if (Environment.GetEnvironmentVariable("BASTION_MENU_CAM") is string cam)
            {
                var v = cam.Split(',').Select(t => float.Parse(t, CultureInfo.InvariantCulture)).ToArray();
                var eye = MenuBackdrop.Current.Eye;
                eye.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), Quaternion.Euler(v[4], v[3], 0));
                MenuBackdrop.Current.Fov = v[5];
            }
        }

        /// <summary>
        /// The backdrop shown once the game runs (BASTION_MENU_JUEGO=1), and this camera kept on it: where its own stands,
        /// as it frames (its fov as it widens it), drawing as it draws.
        /// </summary>
        sealed class InPlay : MonoBehaviour
        {
            public MenuBackdrop.Scene scene;
            Camera own;

            void Start()
            {
                MenuBackdrop.Show(scene);
                own = GetComponent<Camera>();
                own.enabled = true;
                var data = own.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
                own.clearFlags = CameraClearFlags.SolidColor;
            }

            void LateUpdate()
            {
                var eye = MenuBackdrop.Current != null ? MenuBackdrop.Current.Eye : null;
                if (eye == null || own == null) return;
                transform.SetPositionAndRotation(eye.transform.position, eye.transform.rotation);
                own.nearClipPlane = eye.nearClipPlane;
                own.farClipPlane = eye.farClipPlane;
                own.backgroundColor = eye.backgroundColor;
                float wide = Camera.VerticalToHorizontalFieldOfView(MenuBackdrop.Current.Fov, 16f / 9);
                own.fieldOfView = own.aspect >= 16f / 9 ? MenuBackdrop.Current.Fov : Camera.HorizontalToVerticalFieldOfView(wide, own.aspect);
            }
        }

        /// <summary>An empty scene with the start screen's camera (Inicio's «Cámara»: a dark colour, no post-processing).</summary>
        static Camera StartScreen()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Cámara").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.06f, 1);
            camera.fieldOfView = 60;
            return camera;
        }

        public static void Coste()
        {
            Ready();
            int was = QualitySettings.GetQualityLevel();
            var levels = (Environment.GetEnvironmentVariable("BASTION_CALIDAD") ?? "Alto").Split(',');
            int frames = int.TryParse(Environment.GetEnvironmentVariable("BASTION_FRAMES"), out int f) ? f : 120;
            // What MenuArt brings in with it (its models' meshes and textures; in the editor, as Unity counts them there).
            var art = AssetDatabase.LoadAssetAtPath<MenuArt>(ArtAsset);
            var deps = EditorUtility.CollectDependencies(new UnityEngine.Object[] { art });
            long meshes = deps.OfType<Mesh>().Sum(m => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m));
            long textures = deps.OfType<Texture>().Sum(x => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(x));
            Debug.Log(FormattableString.Invariant($"[menu] MenuArt: {art.models.Length} models, {deps.OfType<Mesh>().Count()} meshes ({meshes / 1048576.0:0.0} MB), {deps.OfType<Texture>().Count()} textures ({textures / 1048576.0:0.0} MB), as the editor counts them"));
            try
            {
                foreach (var s in new[] { MenuBackdrop.Scene.Road, MenuBackdrop.Scene.Crossroads })
                {
                    var start = StartScreen();
                    var first = System.Diagnostics.Stopwatch.StartNew();
                    MenuBackdrop.Show(s);
                    double cold = first.Elapsed.TotalMilliseconds;
                    // Again, its models loaded: what showing it once more costs (from the other menu screen and back).
                    var builds = new List<double>();
                    for (int k = 0; k < 3; k++)
                    {
                        MenuBackdrop.Hide();
                        var again = System.Diagnostics.Stopwatch.StartNew();
                        MenuBackdrop.Show(s);
                        builds.Add(again.Elapsed.TotalMilliseconds);
                    }
                    var backdrop = MenuBackdrop.Current;
                    var renderers = backdrop.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
                    Debug.Log(FormattableString.Invariant($"[menu] {s}: built in {cold:0} ms the first time, then {builds.Min():0}–{builds.Max():0} ms; {renderers.Count} renderers, {renderers.Sum(r => r.sharedMaterials.Length)} materials, {backdrop.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount)} flakes"));
                    foreach (string level in levels)
                    {
                        int index = Array.IndexOf(QualitySettings.names, level);
                        if (index < 0) throw new ArgumentException($"no quality level {level}");
                        QualitySettings.SetQualityLevel(index, true);
                        var (all, cpu) = Frames(MenuBackdrop.Current.Eye, frames);
                        // What its parts weigh: without the sun's shadows, without the pines, without the snow's cap, without the flakes.
                        var parts = new List<string>();
                        foreach (var (part, off) in Parts())
                        {
                            var back = off();
                            var (less, _) = Frames(MenuBackdrop.Current.Eye, frames);
                            back();
                            parts.Add(FormattableString.Invariant($"{part} {less - all:+0.00;-0.00}"));
                        }
                        MenuBackdrop.Hide();
                        var (bare, bareCpu) = Frames(start, frames);
                        MenuBackdrop.Show(s);
                        Debug.Log(FormattableString.Invariant($"[menu] {s} on {level}: {all:0.00} ms a frame ({cpu:0.00} in camera.Render), the start screen's camera alone {bare:0.00} ({bareCpu:0.00}); {string.Join(", ", parts)}"));
                    }
                    MenuBackdrop.Hide();
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(was, true);
            }
        }

        /// <summary>Each part of the backdrop up now taken away (and how to put it back), to weigh it (Coste).</summary>
        static IEnumerable<(string name, Func<Action> off)> Parts()
        {
            Action Off(IEnumerable<Behaviour> things)
            {
                var list = things.Where(b => b.enabled).ToList();
                foreach (var b in list) b.enabled = false;
                return () => { foreach (var b in list) b.enabled = true; };
            }
            Action Hidden(IEnumerable<Renderer> things)
            {
                var list = things.Where(r => r.enabled).ToList();
                foreach (var r in list) r.enabled = false;
                return () => { foreach (var r in list) r.enabled = true; };
            }
            var root = MenuBackdrop.Current.transform;
            yield return ("sin sombras", () =>
            {
                var suns = root.GetComponentsInChildren<Light>().Where(l => l.shadows != LightShadows.None).ToList();
                foreach (var l in suns) l.shadows = LightShadows.None;
                return () => { foreach (var l in suns) l.shadows = LightShadows.Soft; };
            });
            yield return ("sin pinos", () => Hidden(root.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("SM_Tree_"))));
            yield return ("sin nieve", () => Off(root.GetComponentsInChildren<UnityEngine.Rendering.Universal.DecalProjector>()));
            yield return ("sin copos", () => Hidden(root.GetComponentsInChildren<ParticleSystemRenderer>()));
            yield return ("sin luces", () => Off(root.GetComponentsInChildren<Light>().Where(l => l.type != LightType.Directional)));
            // Nothing of it drawn but its sky: what its camera's passes cost by themselves (the post-processing, the SSAO, the decals' prepass).
            yield return ("vacía", () => Hidden(root.GetComponentsInChildren<Renderer>().Where(r => !r.name.StartsWith("Cielo") && r.name != "Montañas")));
        }

        /// <summary>
        /// A frame's ms, and of it the camera.Render call's, at 1920×1080: `camera` into a texture and a pixel read back (the
        /// GPU waited for), the post-processing on; the scene stepped a 30th of a second each (its flakes, its lamps).
        /// </summary>
        static (double all, double cpu) Frames(Camera camera, int frames)
        {
            var target = new RenderTexture(1920, 1080, 24);
            var had = camera.targetTexture;
            bool post = camera.GetUniversalAdditionalCameraData().renderPostProcessing;
            bool enabled = camera.enabled;
            camera.enabled = true;
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var render = new System.Diagnostics.Stopwatch();
            void One()
            {
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(1 / 30f, false, false, true);
                    foreach (var step in root.GetComponentsInChildren<IShotStep>()) step.ShotStep(1 / 30f);
                }
                render.Start();
                camera.Render();
                render.Stop();
                RenderTexture.active = target;
                probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                probe.Apply(false);
                RenderTexture.active = null;
            }
            for (int k = 0; k < 10; k++) One();
            render.Reset();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int k = 0; k < frames; k++) One();
            var result = (watch.Elapsed.TotalMilliseconds / frames, render.Elapsed.TotalMilliseconds / frames);
            camera.targetTexture = had;
            camera.enabled = enabled;
            UnityEngine.Object.DestroyImmediate(probe);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            return result;
        }

        /// <summary>The models looked at (Muestras), by key (MapArt.ModelKey's form, MapLab.Prefabs).</summary>
        static readonly string[] Candidates =
        {
            "apocalypse:SM_Veh_Car_Medium_01", "nature:SM_Tree_PolyPine_01", "nature:SM_Tree_PolyPine_02", "nature:SM_Tree_PolyPine_03",
            "nature:SM_Tree_Pine_Dead_01", "apocalypse:SM_Env_Tree_01", "apocalypse:SM_Env_Tree_Dead_01", "military:SM_Bld_GuardTower_01",
            "military:SM_Bld_GuardTower_02", "construction:SM_Prop_Fence_Concrete_01", "apocalypse:SM_Prop_Barrier_Long_01",
            "apocalypse:SM_Prop_Road_Barrier_01", "apocalypse:SM_Prop_Railing_01", "apocalypse:SM_Prop_Powerpole_Single_Line_01",
            "apocalypse:SM_Prop_Powerpole_Preset_01", "nature:SM_Rock_01", "nature:SM_Rock_Boulder_01", "nature:SM_Plant_Bush_01",
            "woodland:SM_Prop_Nature_Sign_01", "apocalypse:SM_Prop_Floodlights_01", "apocalypse:SM_Prop_SpikeWall_01",
            "apocalypse:SM_Prop_Wall_Quarantine_GateDoor_01", "farm:SM_Prop_Fence_Wire_01", "woodland:SM_Prop_Chain_Fence_Broken_01",
            "nature:SM_Tree_Log_01",
            "town:SM_Bld_House_Preset_01", "town:SM_Bld_House_Preset_02", "town:SM_Bld_House_Preset_03", "town:SM_Bld_House_Preset_04",
            "town:SM_Bld_House_Preset_05", "town:SM_Bld_House_Preset_06", "town:SM_Bld_House_Preset_07", "town:SM_Bld_House_Preset_08",
            "town:SM_Bld_House_Preset_09", "town:SM_Bld_House_Preset_10", "town:SM_Bld_House_Preset_11", "town:SM_Bld_House_Preset_Garage_01",
            "apocalypse:SM_Bld_House_01", "apocalypse:SM_Bld_House_02", "apocalypse:SM_Bld_House_03", "apocalypse:SM_Bld_AutoRepair_01",
            "farm:SM_Bld_Barn_01", "farm:SM_Bld_Barn_02", "farm:SM_Bld_Farmhouse_01", "farm:SM_Bld_Farmhouse_02", "farm:SM_Bld_Silo_01",
            "farm:SM_Bld_Shelter_01", "military:SM_Bld_GasStation_01", "apocalypse:SM_Prop_Gaspump_01", "apocalypse:SM_Prop_Car_Wrecked_01",
            "apocalypse:SM_Prop_Car_Wrecked_Rusted_01", "apocalypse:SM_Prop_Car_Wrecked_SUV_01", "town:SM_Veh_Pickup_01",
            "city:SM_Veh_Car_Sedan_01", "town:SM_Prop_LampStanding_01", "apocalypse:SM_Prop_LightPole_01", "town:SM_Prop_Lamp_01",
            "apocalypse:SM_Bld_WaterTower_01", "farm:SM_Prop_Windmill_01", "farm:SM_Prop_Fence_Wood_01", "apocalypse:SM_Prop_Sign_Stop_01",
        };

        /// <summary>Muestras: each candidate alone, a frame each, from its front's three quarters and above, on a plain floor.</summary>
        public static void Muestras()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Mood.Show(Mood.SnowDay);
            var prefabOf = MapLab.Prefabs();
            var shown = new List<(string key, GameObject o, Bounds box)>();
            foreach (string key in Candidates)
            {
                var prefab = prefabOf(key);
                if (prefab == null)
                {
                    Debug.Log($"[menu] {key}: none");
                    continue;
                }
                var o = UnityEngine.Object.Instantiate(prefab);
                o.name = key;
                var box = BoundsOf(o.transform);
                Debug.Log(FormattableString.Invariant($"[menu] {shown.Count:00} {key}: {box.size.x:0.00} × {box.size.y:0.00} × {box.size.z:0.00} m, middle {box.center.x:0.00}, {box.center.y:0.00}, {box.center.z:0.00} ({o.GetComponentsInChildren<Renderer>().Length} renderers, {o.GetComponentsInChildren<Renderer>().Sum(r => r.sharedMaterials.Length)} materials)"));
                o.SetActive(shown.Count == 0);
                shown.Add((key, o, box));
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 20;
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.fieldOfView = 35;
            camera.farClipPlane = 800;
            var cycle = new GameObject("Muestras").AddComponent<Cycle>();
            cycle.shown = shown;
            cycle.camera = camera;
            cycle.Frame();
        }

        /// <summary>One candidate a step, the camera on it (Muestras).</summary>
        sealed class Cycle : MonoBehaviour, IShotStep
        {
            public List<(string key, GameObject o, Bounds box)> shown;
            public Camera camera;
            int at;

            public void ShotStep(float dt)
            {
                at = Mathf.Min(at + 1, shown.Count - 1);
                for (int k = 0; k < shown.Count; k++) shown[k].o.SetActive(k == at);
                Frame();
            }

            public void Frame()
            {
                if (shown.Count == 0) return;
                var box = shown[at].box;
                float r = Mathf.Max(box.extents.magnitude, 0.5f);
                var from = new Vector3(0.75f, 0.45f, 1).normalized;
                camera.transform.position = box.center + from * (r / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f);
                camera.transform.LookAt(box.center);
            }
        }

        static Bounds BoundsOf(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(root.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
