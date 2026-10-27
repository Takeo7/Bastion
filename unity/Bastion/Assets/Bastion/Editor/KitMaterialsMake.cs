using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Makes the client's plain materials in Resources/Kit (Bastion.Client.KitMaterials), so that a build has their shaders
    /// and variants: URP's Unlit see-through, its Lit glowing, its Lit plain. Run again, it sets them as they are:
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.KitMaterialsMake.Run -quit
    /// </summary>
    public static class KitMaterialsMake
    {
        const string Parent = "Assets/Bastion/Client/Resources";
        const string Folder = Parent + "/Kit";

        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Parent, "Kit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Make("SeeThrough", unlit, m =>
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(1, 1, 1, 0.75f));
            });
            Make("Glowing", lit, m =>
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            Make("Plain", lit, m => { });
            var mark = Shader.Find("Bastion/Mark");
            Make("Mark", mark, m => m.renderQueue = (int)RenderQueue.Transparent);
            var fade = Shader.Find("Bastion/FigureFade");
            Make("Fade", fade, m => { });
            var rim = Shader.Find("Bastion/NightRim");
            Make("NightRim", rim, m => m.renderQueue = (int)RenderQueue.Transparent);
            var mask = Shader.Find("Bastion/FigureMask");
            Make("FigureMask", mask, m => m.renderQueue = (int)RenderQueue.AlphaTest + 49);
            AssetDatabase.SaveAssets();
            Debug.Log($"[kit] {Folder}: SeeThrough ({unlit?.name}), Glowing and Plain ({lit?.name}), Mark ({mark?.name}), Fade ({fade?.name}), NightRim ({rim?.name}), FigureMask ({mask?.name})");
        }

        static void Make(string name, Shader shader, Action<Material> set)
        {
            string path = $"{Folder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            set(m);
            EditorUtility.SetDirty(m);
        }
    }
}
