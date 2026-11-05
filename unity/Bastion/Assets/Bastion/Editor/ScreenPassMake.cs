using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bastion.Client.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The renderers' full-screen passes as ScreenPass (Arturo, 4/11: no copy of the picture to read it): FogFeatureMake's
    /// and GradeMake's, each put where the one of its name was (URP's FullScreenPassRendererFeature till then), or added.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.FogFeatureMake.Run -quit (and GradeMake.Run)
    /// </summary>
    public static class ScreenPassMake
    {
        public static ScreenPass Put(UniversalRendererData data, string name, Material material, RenderPassEvent at, ScriptableRenderPassInput needs, bool stencil = false, int keep = -1)
        {
            var list = data.rendererFeatures;
            int index = list.FindIndex(f => f != null && f.name == name);
            var pass = index >= 0 ? list[index] as ScreenPass : null;
            // (The renderer keeps its features' local ids alongside, in the same order: what its inspector would keep.)
            var map = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(data) as List<long>;
            if (pass == null)
            {
                pass = ScriptableObject.CreateInstance<ScreenPass>();
                pass.name = name;
                AssetDatabase.AddObjectToAsset(pass, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pass, out _, out long id);
                if (index >= 0)
                {
                    var old = list[index];
                    list[index] = pass;
                    if (map != null && index < map.Count) map[index] = id;
                    AssetDatabase.RemoveObjectFromAsset(old);
                    Object.DestroyImmediate(old, true);
                    Debug.Log($"[pantalla] {AssetDatabase.GetAssetPath(data)}: {name} as a ScreenPass, in its place");
                }
                else
                {
                    list.Add(pass);
                    map?.Add(id);
                    Debug.Log($"[pantalla] {AssetDatabase.GetAssetPath(data)}: {name} added");
                }
            }
            pass.passMaterial = material;
            pass.injectionPoint = at;
            pass.requirements = needs;
            pass.bindDepthStencilAttachment = stencil;
            pass.keepPass = keep;
            pass.Create();
            EditorUtility.SetDirty(pass);
            data.SetDirty();
            EditorUtility.SetDirty(data);
            return pass;
        }
    }
}
