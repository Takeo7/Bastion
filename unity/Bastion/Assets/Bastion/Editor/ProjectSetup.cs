using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The project's settings that are Arte's (docs/UNITY-ARTE.md): the three quality levels UI's Ajustes picks by name
    /// («Bajo», «Medio», «Alto»; Gameplay's rule: «alto» ≤4.2 ms a frame on the 3090, «medio» ≥30 fps on the Mac), the
    /// frame timings the telemetry reads (FrameTimingManager), and the app's company and product names (where its data
    /// goes: ~/Library/Application Support/Bastion-Unity/Bastion, not inside the Electron app's …/Bastion) and identifier.
    /// Run again, it sets them as they are.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.ProjectSetup.Run -quit
    /// </summary>
    public static class ProjectSetup
    {
        const string High = "Assets/Settings/PC_RPAsset.asset";
        const string Medium = "Assets/Settings/Medio_RPAsset.asset";
        const string Low = "Assets/Settings/Mobile_RPAsset.asset";
        const string MediumRenderer = "Assets/Settings/Mobile_Renderer.asset";
        const string LowRenderer = "Assets/Settings/Bajo_Renderer.asset";

        public static void Run()
        {
            // The product's name without its accent: it names the Mac's executable (Contents/MacOS/…), and an accent there
            // breaks the app's ad hoc signature («a sealed resource is missing or invalid», Gameplay 24/10); the name the
            // player sees is AppBuild's (Info.plist). Its own identifier, not the template's nor the Electron app's.
            PlayerSettings.companyName = "Bastion-Unity";
            PlayerSettings.productName = "Bastion";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.takeo7.bastion.unity");
            PlayerSettings.enableFrameTimingStats = true;

            // Medio: PC's pipeline with the renderer without SSAO, cheaper shadows (two cascades, 1024, hard), full scale.
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Medium) == null) AssetDatabase.CopyAsset(High, Medium);
            var medium = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Medium));
            var renderers = medium.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(MediumRenderer);
            medium.FindProperty("m_DefaultRendererIndex").intValue = 0;
            medium.FindProperty("m_ShadowCascadeCount").intValue = 2;
            medium.FindProperty("m_MainLightShadowmapResolution").intValue = 1024;
            medium.FindProperty("m_SoftShadowsSupported").boolValue = false;
            medium.FindProperty("m_RenderScale").floatValue = 1;
            medium.ApplyModifiedPropertiesWithoutUndo();
            // Bajo: Mobile's as it came (one cascade of 1024, hard shadows, the picture at 0.8), on a renderer without
            // post-processing (Mobile's own, the fog's pass in it too, without its post data).
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LowRenderer) == null) AssetDatabase.CopyAsset(MediumRenderer, LowRenderer);
            var lowRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LowRenderer);
            lowRenderer.postProcessData = null;
            EditorUtility.SetDirty(lowRenderer);
            var low = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Low));
            var lowRenderers = low.FindProperty("m_RendererDataList");
            lowRenderers.arraySize = 1;
            lowRenderers.GetArrayElementAtIndex(0).objectReferenceValue = lowRenderer;
            low.FindProperty("m_DefaultRendererIndex").intValue = 0;
            low.FindProperty("m_RenderScale").floatValue = 0.8f;
            low.FindProperty("m_ShadowCascadeCount").intValue = 1;
            low.FindProperty("m_SoftShadowsSupported").boolValue = false;
            low.ApplyModifiedPropertiesWithoutUndo();

            // The levels, in order: Bajo, Medio, Alto (UI's Ajustes finds them by name).
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            while (levels.arraySize < 3) levels.InsertArrayElementAtIndex(levels.arraySize - 1);
            levels.arraySize = 3;
            string[] names = { "Bajo", "Medio", "Alto" };
            string[] pipelines = { Low, Medium, High };
            for (int k = 0; k < 3; k++)
            {
                var level = levels.GetArrayElementAtIndex(k);
                level.FindPropertyRelative("name").stringValue = names[k];
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelines[k]);
            }
            // The editor (and the captures) as before: on PC's pipeline, now «Alto»; every platform's default «Alto»
            // (UI's Ajustes sets the player's own at start: «Medio» on the Mac).
            quality.FindProperty("m_CurrentQuality").intValue = 2;
            var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
            for (int k = 0; k < defaults.arraySize; k++) defaults.GetArrayElementAtIndex(k).FindPropertyRelative("second").intValue = 2;
            quality.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[project] {PlayerSettings.companyName} / {PlayerSettings.productName} ({PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone)}); frame timing {PlayerSettings.enableFrameTimingStats}; levels {string.Join(", ", QualitySettings.names)}");
        }
    }
}
