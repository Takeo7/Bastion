using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The snow's decals («Nieve») on in the three renderers while the app builds, and off again after (28/10, Ana's screen
    /// gone magenta on a clinic's third floor). URP strips the decal shaders, and the DBuffer variants of the lit ones, from
    /// a build whose renderers have no decal feature on; «Nieve» is saved off (SnowCover turns it on for a snowy map), so
    /// the app had the snow decal without a single variant. Drawn as the error shader, it went unseen from inside its box
    /// but covered the whole map once the camera rose past its top (40 m), and the props never had their snow (p2–p3).
    /// SnowCover turns it off again as the game starts.
    ///
    /// And beside each renderer in its pipeline assets, for the build only, a copy of it with «Nieve» off (29/10, ebd6223's
    /// app: soldiers, cars and props black on every map without snow). With DBuffer decals on in every renderer URP strips
    /// the lit shaders' variant without them too (ShaderStripTool.StripMultiCompile, the OFF variant), and a map without
    /// snow, its decals off, drew them with a DBuffer variant reading a buffer never written. URP keeps a variant that any
    /// one renderer of the build wants: the copies want the ones without. The app takes them in its assets' lists, never
    /// drawn with (each asset's renderer is its first); the project, out of them again after the build.
    /// </summary>
    public sealed class SnowBuild : BuildPlayerProcessor, IPostprocessBuildWithReport
    {
        static readonly List<ScriptableRendererFeature> turned = new List<ScriptableRendererFeature>();
        const string Copy = "_SinNieve.asset";

        // Before URP reads which features are on (ShaderBuildPreprocessor, UpdateShaderPrefilteringDataBeforeBuild).
        public override int callbackOrder => -1000;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            // What a build that failed left (OnPostprocessBuild never came).
            Remove();
            turned.Clear();
            var assets = Assets();
            int copies = 0;
            foreach (var path in SnowFeatureMake.Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var snow = data != null ? Snow(data) : null;
                if (snow == null)
                {
                    Debug.LogError($"[snow] {path}: sin «Nieve» (SnowFeatureMake.Run): la app no tendrá nieve");
                    continue;
                }
                var copy = path.Substring(0, path.Length - ".asset".Length) + Copy;
                if (AssetDatabase.CopyAsset(path, copy) && AssetDatabase.LoadAssetAtPath<UniversalRendererData>(copy) is UniversalRendererData without && Snow(without) is ScriptableRendererFeature off)
                {
                    off.SetActive(false);
                    EditorUtility.SetDirty(without);
                    AssetDatabase.SaveAssetIfDirty(without);
                    foreach (var asset in assets)
                    {
                        var list = new SerializedObject(asset);
                        var renderers = list.FindProperty("m_RendererDataList");
                        if (!Enumerable.Range(0, renderers.arraySize).Any(i => renderers.GetArrayElementAtIndex(i).objectReferenceValue == data)) continue;
                        renderers.arraySize++;
                        renderers.GetArrayElementAtIndex(renderers.arraySize - 1).objectReferenceValue = without;
                        list.ApplyModifiedPropertiesWithoutUndo();
                        copies++;
                    }
                }
                else Debug.LogError($"[snow] {path}: sin su copia sin «Nieve»: en la app, sin nieve, lo iluminado saldrá negro");
                if (snow.isActive) continue;
                snow.SetActive(true);
                turned.Add(snow);
            }
            Debug.Log($"[snow] decals on for the build in {turned.Count} renderers, and {copies} copies without them");
        }

        int IOrderedCallback.callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            foreach (var snow in turned) if (snow != null) snow.SetActive(false);
            turned.Clear();
            Remove();
        }

        static ScriptableRendererFeature Snow(UniversalRendererData data) => data.rendererFeatures.FirstOrDefault(f => f is DecalRendererFeature && f.name == "Nieve");

        /// <summary>The pipeline assets the app is built with: its quality levels', and the default one.</summary>
        static List<UniversalRenderPipelineAsset> Assets()
        {
            var all = Enumerable.Range(0, QualitySettings.count).Select(QualitySettings.GetRenderPipelineAssetAt).Append(GraphicsSettings.defaultRenderPipeline);
            return all.OfType<UniversalRenderPipelineAsset>().Distinct().ToList();
        }

        /// <summary>The copies without «Nieve» out of the pipeline assets, and gone.</summary>
        static void Remove()
        {
            foreach (var asset in Assets())
            {
                var list = new SerializedObject(asset);
                var renderers = list.FindProperty("m_RendererDataList");
                bool changed = false;
                for (int i = renderers.arraySize - 1; i >= 0; i--)
                {
                    var r = renderers.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (r == null || !AssetDatabase.GetAssetPath(r).EndsWith(Copy)) continue;
                    renderers.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    renderers.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
                if (!changed) continue;
                list.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            foreach (var path in SnowFeatureMake.Renderers)
            {
                var copy = path.Substring(0, path.Length - ".asset".Length) + Copy;
                if (File.Exists(copy)) AssetDatabase.DeleteAsset(copy);
            }
        }
    }
}
