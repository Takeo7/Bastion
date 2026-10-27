using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The fog of war's full-screen pass (Bastion/Fog) in both of the project's renderers, before the post-processing,
    /// with the depth and the picture it needs; and its material. Run once (again does nothing):
    /// unity/tools/unity.sh -executeMethod Bastion.EditorTools.FogFeatureMake.Run -quit
    /// </summary>
    public static class FogFeatureMake
    {
        const string MaterialPath = "Assets/Bastion/Client/Shaders/Fog.mat";
        static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/Bajo_Renderer.asset" };

        /// <summary>The picture, its depth (where each pixel is) and its normals (which way a wall looks).</summary>
        const ScriptableRenderPassInput Needs = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color;

        public static void Run()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Bastion/Fog")) { name = "Fog" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            foreach (string path in Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null)
                {
                    Debug.Log($"[fog] {path}: not found");
                    continue;
                }
                // There already: what it needs brought up to date (the normals came with the buildings' walls).
                if (data.rendererFeatures.FirstOrDefault(f => f is FullScreenPassRendererFeature && f.name == "Niebla") is FullScreenPassRendererFeature there)
                {
                    there.requirements = Needs;
                    there.passMaterial = material;
                    // (The depth-stencil bound: the figures' stencil bit keeps the fog off them, Bastion/FigureMask.)
                    there.bindDepthStencilAttachment = true;
                    EditorUtility.SetDirty(there);
                    EditorUtility.SetDirty(data);
                    Debug.Log($"[fog] {path}: already there, its needs set");
                    continue;
                }
                var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "Niebla";
                feature.passMaterial = material;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                feature.requirements = Needs;
                feature.fetchColorBuffer = true;
                feature.bindDepthStencilAttachment = true;
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                // The renderer keeps its features' local ids alongside (what its inspector would add).
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                if (typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(data) is List<long> map) map.Add(id);
                else Debug.LogWarning($"[fog] {path}: no feature map to note the pass in");
                data.SetDirty();
                EditorUtility.SetDirty(data);
                Debug.Log($"[fog] {path}: pass added");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
