using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
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
    /// </summary>
    public sealed class SnowBuild : BuildPlayerProcessor, IPostprocessBuildWithReport
    {
        static readonly List<ScriptableRendererFeature> turned = new List<ScriptableRendererFeature>();

        // Before URP reads which features are on (ShaderBuildPreprocessor, UpdateShaderPrefilteringDataBeforeBuild).
        public override int callbackOrder => -1000;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            turned.Clear();
            foreach (var path in SnowFeatureMake.Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var snow = data != null ? data.rendererFeatures.FirstOrDefault(f => f is DecalRendererFeature && f.name == "Nieve") : null;
                if (snow == null)
                {
                    Debug.LogError($"[snow] {path}: sin «Nieve» (SnowFeatureMake.Run): la app no tendrá nieve");
                    continue;
                }
                if (snow.isActive) continue;
                snow.SetActive(true);
                turned.Add(snow);
            }
            Debug.Log($"[snow] decals on for the build in {turned.Count} renderers");
        }

        int IOrderedCallback.callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            foreach (var snow in turned) if (snow != null) snow.SetActive(false);
            turned.Clear();
        }
    }
}
