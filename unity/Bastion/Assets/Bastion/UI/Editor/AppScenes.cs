using Bastion.UI.App;
using Bastion.UI.Start;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.EditorTools
{
    /// <summary>
    /// The app's start scene, «Inicio» (docs/UNITY-UI.md): AppFlow on its own (it lives across scenes, with its card and
    /// the network's line), a camera, and the screens' document with the start screen and the room menu (AppFlow turns
    /// on one or the other). Made again from scratch each time it is run.
    ///
    ///   unity/tools/unity.sh -executeMethod Bastion.UI.EditorTools.AppScenes.MakeStart
    /// </summary>
    public static class AppScenes
    {
        public const string StartPath = "Assets/Bastion/UI/Scenes/Inicio.unity";

        public static void MakeStart()
        {
            System.IO.Directory.CreateDirectory("Assets/Bastion/UI/Scenes");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Bastion/UI/Settings/HudPanel.asset");
            if (panel == null)
            {
                Debug.LogError("App scenes: HudPanel.asset missing (a HudShots capture makes it)");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Cámara").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.06f);
            var flow = new GameObject("App");
            flow.AddComponent<UIDocument>().panelSettings = panel;
            flow.AddComponent<AppFlow>();
            var screens = new GameObject("Pantallas");
            screens.AddComponent<UIDocument>().panelSettings = panel;
            screens.AddComponent<StartScreen>();
            screens.AddComponent<RoomMenu>().enabled = false;
            EditorSceneManager.SaveScene(scene, StartPath);
            Debug.Log("App scenes: " + StartPath);
        }
    }
}
