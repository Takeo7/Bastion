using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The picture's grade as 0.10.1's (Bastion.Client.Mood, Grade.shader): its full-screen pass in the project's three
    /// renderers after URP's post-processing, and its material; URP's own tone mapping none and its grading HDR (so the
    /// lit picture comes through to Khronos' neutral tone mapping), and on the pipelines' volume profile the day's bloom
    /// (post.ts: 0.35, over 0.92), no tone mapping and no vignette of URP's (the grade has its own). Run again, it sets them
    /// as they are:
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.GradeMake.Run -quit
    /// </summary>
    public static class GradeMake
    {
        const string MaterialPath = "Assets/Bastion/Client/Shaders/Grade.mat";
        static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/Bajo_Renderer.asset" };
        static readonly string[] Pipelines = { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Medio_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" };

        public static void Run()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Bastion/Grade")) { name = "Grade" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            foreach (string path in Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null)
                {
                    Debug.Log($"[grade] {path}: not found");
                    continue;
                }
                if (data.rendererFeatures.FirstOrDefault(f => f is FullScreenPassRendererFeature && f.name == "Gradación") is FullScreenPassRendererFeature there)
                {
                    there.passMaterial = material;
                    there.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                    there.requirements = ScriptableRenderPassInput.Color;
                    EditorUtility.SetDirty(there);
                    EditorUtility.SetDirty(data);
                    continue;
                }
                var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "Gradación";
                feature.passMaterial = material;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                feature.requirements = ScriptableRenderPassInput.Color;
                feature.fetchColorBuffer = true;
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                // The renderer keeps its features' local ids alongside (what its inspector would add).
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                if (typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(data) is List<long> map) map.Add(id);
                data.SetDirty();
                EditorUtility.SetDirty(data);
                Debug.Log($"[grade] {path}: pass added");
            }
            var profiles = new HashSet<VolumeProfile>();
            foreach (string path in Pipelines)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                so.FindProperty("m_ColorGradingMode").intValue = (int)ColorGradingMode.HighDynamicRange;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (so.FindProperty("m_VolumeProfile")?.objectReferenceValue is VolumeProfile p) profiles.Add(p);
            }
            foreach (var profile in profiles)
            {
                T Of<T>() where T : VolumeComponent
                {
                    if (profile.TryGet(out T component)) return component;
                    component = profile.Add<T>();
                    AssetDatabase.AddObjectToAsset(component, profile);
                    return component;
                }
                var tonemapping = Of<Tonemapping>();
                tonemapping.active = true;
                tonemapping.mode.Override(TonemappingMode.None);
                var bloom = Of<Bloom>();
                bloom.active = true;
                bloom.intensity.Override(0.35f);
                bloom.threshold.Override(0.92f);
                if (profile.TryGet(out Vignette vignette)) vignette.active = false;
                foreach (var component in profile.components) EditorUtility.SetDirty(component);
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[grade] {MaterialPath}; grading HDR; {string.Join(", ", profiles.Select(p => p.name))}: tone mapping none, bloom 0.35 over 0.92, no vignette");
        }
    }
}
