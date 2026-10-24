using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Bastion.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Captures from the command line, in batchmode, so that a session without the editor sees what it made
    /// (docs/UNITY-ARTE.md §6): one picture, or a strip of frames at a fixed rate, the scene stepped by hand so
    /// the same strip comes out every time. Run it with unity/tools/captura.sh:
    ///   -shotOut &lt;file.png, or a folder for a strip&gt;
    ///   -shotScene Assets/….unity       the scene to open (none: an empty one with a light)
    ///   -shotSetup Namespace.Type.Method a static method with no arguments that builds or sets up what is shot
    ///   -shotSize 1600x900
    ///   -shotCamera &lt;name&gt;             the scene's camera to shoot from (none: the main one, or a new one)
    ///   -shotFrom x,y,z -shotAt x,y,z   where the camera stands and what it looks at
    ///   -shotFov 40
    ///   -shotFrames 20 -shotFps 10      a strip: so many frames, so many a second (f000.png, f001.png…)
    ///   -shotWarm 1                     seconds stepped before the first frame (animations settle)
    ///   -shotPlay                       in play mode, for what only draws with the game running: the HUD's panels
    ///                                   (UI Toolkit) are drawn into a texture of their own and laid over the camera's;
    ///                                   time goes at -shotFps a frame (Time.captureDeltaTime), so a strip repeats
    /// </summary>
    public static class Shots
    {
        public static void Run()
        {
            try
            {
                Shoot();
            }
            catch (Exception e)
            {
                Fail(e);
                return;
            }
            // In play mode the shots are taken frames later (Play): it quits then.
            if (!Flag("-shotPlay")) EditorApplication.Exit(0);
        }

        static void Fail(Exception e)
        {
            Debug.LogError("[shots] " + e);
            EditorApplication.Exit(1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static bool Flag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static void Shoot()
        {
            string outPath = Arg("-shotOut") ?? throw new ArgumentException("-shotOut is missing");
            string scene = Arg("-shotScene");
            if (scene != null) EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            else EmptyScene();

            string setup = Arg("-shotSetup");
            if (setup != null) Call(setup);

            var size = (Arg("-shotSize") ?? "1600x900").Split('x');
            int width = int.Parse(size[0], CultureInfo.InvariantCulture);
            int height = int.Parse(size[1], CultureInfo.InvariantCulture);
            var camera = CameraFor(Arg("-shotCamera"));
            if (Arg("-shotFrom") is string from) camera.transform.position = Vector(from);
            if (Arg("-shotAt") is string at) camera.transform.LookAt(Vector(at));
            if (Arg("-shotFov") is string fov) camera.fieldOfView = Number(fov);

            int frames = int.Parse(Arg("-shotFrames") ?? "1", CultureInfo.InvariantCulture);
            float fps = Number(Arg("-shotFps") ?? "10");
            float warm = Number(Arg("-shotWarm") ?? "0");
            if (Flag("-shotPlay"))
            {
                Play(camera, width, height, outPath, frames, fps, warm);
                return;
            }
            for (float t = 0; t < warm; t += 1 / fps) Step(1 / fps);
            // A frame drawn and thrown away first: the first one a run draws has the ambient light and the reflections
            // not yet made (a darker sky light, and a tint of whatever the GPU had over the Synty materials: red, pink,
            // blue, a different one each run).
            Render(camera, width, height, null);

            if (frames <= 1)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                Render(camera, width, height, outPath);
                Debug.Log("[shots] " + Path.GetFullPath(outPath));
                return;
            }
            Directory.CreateDirectory(outPath);
            for (int k = 0; k < frames; k++)
            {
                if (k > 0) Step(1 / fps);
                Render(camera, width, height, Path.Combine(outPath, $"f{k:000}.png"));
            }
            Debug.Log($"[shots] {frames} frames at {fps} fps in {Path.GetFullPath(outPath)}");
        }

        /// <summary>Frames waited in play mode before the first shot (the panels lay out and draw), and at most.</summary>
        const int PlaySettle = 10;
        const int PlayLimit = 2000;

        /// <summary>
        /// The shots in play mode. The scene as the setup left it goes in as it is: no scene or domain reload (the
        /// project's options are put back before quitting), so neither the setup's objects nor this are lost.
        /// </summary>
        static void Play(Camera camera, int width, int height, string outPath, int frames, float fps, float warm)
        {
            bool optionsWere = EditorSettings.enterPlayModeOptionsEnabled;
            var optionsHad = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            int frame = 0;
            int shot = 0;
            int lastFrame = -1;
            float firstTime = 0;
            int first = PlaySettle + Mathf.RoundToInt(warm * fps);
            RenderTexture panels = null;
            void Quit(int code)
            {
                EditorApplication.update -= Tick;
                Time.captureDeltaTime = 0;
                EditorSettings.enterPlayModeOptionsEnabled = optionsWere;
                EditorSettings.enterPlayModeOptions = optionsHad;
                EditorApplication.Exit(code);
            }
            void Tick()
            {
                try
                {
                    if (!EditorApplication.isPlaying) return;
                    // Entering play mode forgets the fixed step: set again in it, so each frame is 1/fps of the game's time.
                    if (Time.captureDeltaTime != 1 / fps) Time.captureDeltaTime = 1 / fps;
                    // The camera and the panels at the shot's size from the start, not only as it is taken (UI, 24/10: the
                    // marks laid out while they were 4:3, the batch screen's, stood a fifth too far out in a 16:10 shot).
                    camera.aspect = width / (float)height;
                    // The editor's updates come more often than the game's frames: one shot a frame of the game.
                    if (Time.frameCount == lastFrame) return;
                    lastFrame = Time.frameCount;
                    frame++;
                    if (frame > PlayLimit) throw new TimeoutException($"{PlayLimit} frames in play mode and no shot");
                    if (frame == 1) panels = PanelsInto(width, height);
                    if (frame < first) return;
                    string path = frames <= 1 ? outPath : Path.Combine(outPath, $"f{shot:000}.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                    Render(camera, width, height, path, panels);
                    if (shot == 0) firstTime = Time.time;
                    if (++shot < frames) return;
                    Debug.Log(frames <= 1 ? "[shots] " + Path.GetFullPath(outPath) : $"[shots] {frames} frames at {fps} fps in {Path.GetFullPath(outPath)}, game time {firstTime:0.00} to {Time.time:0.00} s");
                    Quit(0);
                }
                catch (Exception e)
                {
                    Debug.LogError("[shots] " + e);
                    Quit(1);
                }
            }
            Time.captureDeltaTime = 1 / fps;
            camera.aspect = width / (float)height;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Every UI Toolkit panel in the scene drawn into one texture of the shot's size, clear where it has nothing. On
        /// copies of their PanelSettings: the assets themselves are left as they are (a target set on one would stay).
        /// </summary>
        static RenderTexture PanelsInto(int width, int height)
        {
            var documents = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.panelSettings != null).ToArray();
            if (documents.Length == 0) return null;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var copies = new Dictionary<PanelSettings, PanelSettings>();
            foreach (var document in documents)
            {
                if (!copies.TryGetValue(document.panelSettings, out var copy))
                {
                    copy = UnityEngine.Object.Instantiate(document.panelSettings);
                    copy.targetTexture = target;
                    copy.clearColor = copies.Count == 0;
                    copy.colorClearValue = Color.clear;
                    copies[document.panelSettings] = copy;
                }
                document.panelSettings = copy;
            }
            return target;
        }

        /// <summary>A new scene with a sun and nothing else, for a setup to fill.</summary>
        static void EmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        /// <summary>Calls a static method by its full name (Namespace.Type.Method), in any loaded assembly.</summary>
        static void Call(string fullName)
        {
            int dot = fullName.LastIndexOf('.');
            string typeName = fullName.Substring(0, dot);
            string methodName = fullName.Substring(dot + 1);
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null)
                ?? throw new ArgumentException($"no type {typeName}");
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null)
                ?? throw new ArgumentException($"no static method {methodName}() in {typeName}");
            method.Invoke(null, null);
        }

        static Camera CameraFor(string name)
        {
            var camera = name != null
                ? UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == name)
                : Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                if (name != null) throw new ArgumentException($"no camera {name}");
                camera = new GameObject("Shot camera").AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0, 6, -10), Quaternion.Euler(30, 0, 0));
            }
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return camera;
        }

        /// <summary>Moves the scene on by dt: its animators, its particles, and whatever takes its own step (IShotStep).</summary>
        static void Step(float dt)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                // (An animator driven by its own step, a clip player's graph, is left to it.)
                foreach (var animator in root.GetComponentsInChildren<Animator>()) if (animator.GetComponent<IShotStep>() == null) animator.Update(dt);
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(dt, false, false, true);
                foreach (var step in root.GetComponentsInChildren<IShotStep>()) step.ShotStep(dt);
            }
        }

        static void Render(Camera camera, int width, int height, string path, RenderTexture over = null)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }
            var was = RenderTexture.active;
            RenderTexture.active = target;
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();
            RenderTexture.active = was;
            if (over != null) Lay(picture, over);
            if (path != null) File.WriteAllBytes(path, picture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(picture);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>The panels' texture over the camera's picture, by its alpha.</summary>
        static void Lay(Texture2D picture, RenderTexture over)
        {
            var was = RenderTexture.active;
            RenderTexture.active = over;
            var top = new Texture2D(over.width, over.height, TextureFormat.RGBA32, false);
            top.ReadPixels(new Rect(0, 0, over.width, over.height), 0, 0);
            RenderTexture.active = was;
            var below = picture.GetPixels32();
            var above = top.GetPixels32();
            for (int i = 0; i < below.Length; i++)
            {
                int a = above[i].a;
                below[i].r = (byte)((above[i].r * a + below[i].r * (255 - a)) / 255);
                below[i].g = (byte)((above[i].g * a + below[i].g * (255 - a)) / 255);
                below[i].b = (byte)((above[i].b * a + below[i].b * (255 - a)) / 255);
            }
            picture.SetPixels32(below);
            picture.Apply();
            UnityEngine.Object.DestroyImmediate(top);
        }

        static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);

        static Vector3 Vector(string text)
        {
            var parts = text.Split(',').Select(Number).ToArray();
            return new Vector3(parts[0], parts[1], parts[2]);
        }
    }
}
