using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
    /// on one or the other). Made again from scratch each time it is run; it fails if the saved scene doesn't hold the
    /// panel on both documents (24/10: the scene went out with none, and the app came up black after «Made with Unity»).
    ///
    ///   unity/tools/unity.sh -executeMethod Bastion.UI.EditorTools.AppScenes.MakeStart
    /// </summary>
    public static class AppScenes
    {
        public const string StartPath = "Assets/Bastion/UI/Scenes/Inicio.unity";
        /// <summary>The panel every document of the app uses (in Resources: AppFlow's fallback loads it from there).</summary>
        public const string PanelPath = "Assets/Bastion/UI/Resources/HudPanel.asset";

        public static void MakeStart()
        {
            Directory.CreateDirectory("Assets/Bastion/UI/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Loaded after the new scene: opening it unloads the assets nothing holds, and a panel loaded before was gone
            // by then (the scene went to disk with {fileID: 0}, 24/10).
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null) throw new System.Exception($"App scenes: {PanelPath} missing (a HudShots capture makes it)");
            var camera = new GameObject("Cámara").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.06f);
            var flow = new GameObject("App");
            Assign(flow.AddComponent<UIDocument>(), panel);
            flow.AddComponent<AppFlow>();
            var screens = new GameObject("Pantallas");
            Assign(screens.AddComponent<UIDocument>(), panel);
            screens.AddComponent<StartScreen>();
            screens.AddComponent<RoomMenu>().enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, StartPath)) throw new System.Exception("App scenes: " + StartPath + " not saved");
            // What went to disk, read back: both documents with the panel.
            var missing = PanelProblems(StartPath);
            if (missing.Count > 0) throw new System.Exception("App scenes: " + string.Join("; ", missing));
            Debug.Log($"App scenes: {StartPath}, both documents on {PanelPath}");
        }

        /// <summary>The panel set through its serialized field too, which is what the scene file keeps.</summary>
        static void Assign(UIDocument doc, PanelSettings panel)
        {
            doc.panelSettings = panel;
            var so = new SerializedObject(doc);
            so.FindProperty("m_PanelSettings").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(doc);
        }

        /// <summary>
        /// The documents of a scene file without a panel, read from the file itself (no scene opened): each UIDocument's
        /// `m_PanelSettings` must name an asset. Empty when all have one.
        /// </summary>
        public static List<string> PanelProblems(string scenePath)
        {
            var problems = new List<string>();
            var text = File.ReadAllText(scenePath);
            var documents = Regex.Matches(text, @"m_PanelSettings: \{fileID: (-?\d+)(?:, guid: (\w+))?");
            if (documents.Count == 0) return problems;
            for (var i = 0; i < documents.Count; i++)
                if (documents[i].Groups[1].Value == "0" || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(documents[i].Groups[2].Value)))
                    problems.Add($"{scenePath}: UIDocument {i + 1} of {documents.Count} without PanelSettings");
            return problems;
        }
    }
}
