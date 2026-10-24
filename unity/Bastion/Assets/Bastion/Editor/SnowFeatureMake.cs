using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The snow on what faces up (Bastion.Client.Map.SnowCover, 0.10.1's snow.ts): URP's decals («Nieve», DBuffer, its
    /// albedo only, by rendering layers) in the project's three renderers, off until a snowy map turns them on; the snow's
    /// texture (Alpine Mountain's Snow_01, tinted as 0.10.1 tinted it: 0.89, 0.95, 1 in linear) into Assets/Synty/Extra (off
    /// git); its decal material (Resources/SnowDecal.mat, URP's decal shader graph) and the snowbanks' (Resources/Snowbank.mat).
    /// Run it again when the texture changes:
    /// unity/tools/unity.sh -executeMethod Bastion.EditorTools.SnowFeatureMake.Run -quit
    /// </summary>
    public static class SnowFeatureMake
    {
        static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/Bajo_Renderer.asset" };
        const string Source = "assets-src/synty/POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3/Textures/Snow_01.png";
        const string Texture = "Assets/Synty/Extra/Snow_01_Bastion.png";
        const string DecalMaterial = "Assets/Bastion/Client/Resources/SnowDecal.mat";
        const string BankMaterial = "Assets/Bastion/Client/Resources/Snowbank.mat";

        public static void Run()
        {
            var texture = MakeTexture();
            var decal = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterial);
            // URP's decal graph with its angle fade on and the albedo alone (Client/Shaders/SnowDecal.shadergraph).
            var shader = Shader.Find("Shader Graphs/SnowDecal");
            if (decal == null && shader != null)
            {
                decal = new Material(shader) { name = "SnowDecal" };
                AssetDatabase.CreateAsset(decal, DecalMaterial);
            }
            if (decal != null)
            {
                if (shader != null) decal.shader = shader;
                decal.SetTexture("Base_Map", texture);
                EditorUtility.SetDirty(decal);
            }
            else Debug.LogWarning("[snow] no Shader Graphs/SnowDecal");
            // The snowbanks (mapView.ts kit.snowbank): the snow's texture, a blue-white, rough.
            var bank = AssetDatabase.LoadAssetAtPath<Material>(BankMaterial);
            if (bank == null)
            {
                var lit = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial : null;
                bank = lit != null ? new Material(lit) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bank.name = "Snowbank";
                AssetDatabase.CreateAsset(bank, BankMaterial);
            }
            bank.SetTexture("_BaseMap", texture);
            bank.SetColor("_BaseColor", new Color32(0xe3, 0xf0, 0xff, 255));
            bank.SetFloat("_Smoothness", 0.02f);
            EditorUtility.SetDirty(bank);
            foreach (string path in Renderers) Feature(path);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The snow's texture, tinted (snow.ts: its colour times 0.89, 0.95, 1, in linear), repeating.</summary>
        static Texture2D MakeTexture()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string from = Path.Combine(root, Source);
            if (!File.Exists(from))
            {
                Debug.LogWarning($"[snow] {Source}: not there");
                return AssetDatabase.LoadAssetAtPath<Texture2D>(Texture);
            }
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            image.LoadImage(File.ReadAllBytes(from));
            var tint = new[] { 0.89f, 0.95f, 1.0f };
            var pixels = image.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                pixels[i] = new Color(Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(c.r) * tint[0]), Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(c.g) * tint[1]), Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(c.b) * tint[2]), 1);
            }
            image.SetPixels(pixels);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Texture)));
            File.WriteAllBytes(Texture, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(Texture, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Texture);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Texture);
        }

        /// <summary>The decals in renderer `path`: DBuffer, the albedo alone, by rendering layers; off (SnowCover turns it on).</summary>
        static void Feature(string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
            {
                Debug.Log($"[snow] {path}: not found");
                return;
            }
            var feature = data.rendererFeatures.FirstOrDefault(f => f is DecalRendererFeature && f.name == "Nieve");
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                feature.name = "Nieve";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                if (typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(data) is List<long> map) map.Add(id);
                Debug.Log($"[snow] {path}: decals added");
            }
            // Its settings are internal: as its inspector sets them.
            var so = new SerializedObject(feature);
            so.FindProperty("m_Settings.technique").enumValueIndex = 1;
            so.FindProperty("m_Settings.maxDrawDistance").floatValue = 1000;
            so.FindProperty("m_Settings.decalLayers").boolValue = true;
            so.FindProperty("m_Settings.dBufferSettings.surfaceData").enumValueIndex = 0;
            so.FindProperty("m_Active").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            Debug.Log($"[snow] {path}: decals set (DBuffer, albedo, rendering layers, off)");
        }
    }
}
