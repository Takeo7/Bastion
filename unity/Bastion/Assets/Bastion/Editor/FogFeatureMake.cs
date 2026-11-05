using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The fog of war's full-screen pass (Bastion/Fog; a ScreenPass) in the project's renderers, before the post-processing,
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
                // A ScreenPass (4/11: no copy of the picture), where URP's full-screen pass was; there already, brought up to
                // date. The depth-stencil bound: the figures' stencil bit keeps the fog off them (Bastion/FigureMask), and its
                // second pass draws the picture there as it was.
                ScreenPassMake.Put(data, "Niebla", material, RenderPassEvent.BeforeRenderingPostProcessing, Needs, true, 1);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
