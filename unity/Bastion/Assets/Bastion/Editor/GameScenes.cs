using System.IO;
using Bastion.Client;
using Bastion.Client.Effects;
using Bastion.Client.Map;
using Bastion.Client.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The combat's scene, «Partida» (Arte's; UI's AppFlow loads it by name when the room goes into a mission and mounts
    /// the HUD itself), and the build's scene list: UI's «Inicio» first, «Partida» after.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.GameScenes.Make
    /// </summary>
    public static class GameScenes
    {
        public const string Partida = "Assets/Bastion/Scenes/Partida.unity";
        const string Inicio = "Assets/Bastion/UI/Scenes/Inicio.unity";

        public static void Make()
        {
            if (!File.Exists(Partida)) MakePartida();
            Furnish();
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if (File.Exists(Inicio)) scenes.Add(new EditorBuildSettingsScene(Inicio, true));
            else Debug.LogWarning($"[scenes] {Inicio} is not there yet (UI's AppScenes.MakeStart makes it)");
            scenes.Add(new EditorBuildSettingsScene(Partida, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[scenes] build: {string.Join(", ", scenes.ConvertAll(s => s.path))}");
            EditorApplication.Exit(0);
        }

        /// <summary>The scene as it starts: the game's camera (tagged MainCamera, for UI's marks), a sun, the map's root.</summary>
        static void MakePartida()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Partida));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Cámara").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 40;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600;
            camera.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.transform.SetPositionAndRotation(new Vector3(0, 28.4f, -22.2f), Quaternion.Euler(52, 0, 0));
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            EditorSceneManager.SaveScene(scene, Partida);
            Debug.Log($"[scenes] {Partida} made");
        }

        /// <summary>
        /// What Partida's views hang from, put in if it is not there (the scene may have more, UI's or later): the map
        /// (MapView) and what builds it from the state (Battlefield), the units' figures (UnitViews), the effects (Fx) and
        /// the fog of war (Fog), each reached by its Current in the game.
        /// </summary>
        static void Furnish()
        {
            var scene = EditorSceneManager.OpenScene(Partida, OpenSceneMode.Single);
            bool changed = false;
            void Root<T>(string name) where T : Component
            {
                var holder = GameObject.Find(name) ?? new GameObject(name);
                if (holder.GetComponent<T>() != null) return;
                holder.AddComponent<T>();
                changed = true;
            }
            Root<MapView>("Mapa");
            Root<Battlefield>("Mapa");
            Root<UnitViews>("Unidades");
            Root<Fx>("Efectos");
            Root<Fog>("Niebla");
            if (changed) EditorSceneManager.SaveScene(scene);
            Debug.Log($"[scenes] {Partida}: " + (changed ? "map, battlefield, units, effects and fog put in" : "map, battlefield, units, effects and fog already there"));
        }
    }
}
