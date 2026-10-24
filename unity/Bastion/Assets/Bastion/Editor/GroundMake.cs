using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The ground's material (UI's; GroundView loads it): Bastion/Ground with the 0.10.1's tiling textures (syntyManifest
    /// TERRAIN: Town's road, the Alpine biome's grass and dirt), in Resources so the build takes them. Run again after an
    /// import, or when the shader's textures change.
    ///
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.GroundMake.Run
    /// </summary>
    public static class GroundMake
    {
        const string MaterialPath = "Assets/Bastion/Client/Resources/Ground.mat";
        /// <summary>The contact occlusion's bake (GroundOcclusion): its hidden shader kept in the build by a material in Resources.</summary>
        const string BakePath = "Assets/Bastion/Client/Resources/GroundBake.mat";

        static readonly (string Property, string Path)[] Textures =
        {
            ("_Asphalt", "Assets/Synty/PolygonTown/Textures/Misc/Road_01.png"),
            ("_GrassTex", "Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain/Textures/Grass_Texture_01.png"),
            ("_DirtTex", "Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain/Textures/Dirt_Texture_01.png"),
        };

        public static void Run()
        {
            var shader = Shader.Find("Bastion/Ground");
            if (shader == null)
            {
                Debug.LogError("Ground: shader Bastion/Ground not found");
                return;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Ground" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            foreach (var (property, path) in Textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) Debug.LogError($"Ground: {path} missing");
                material.SetTexture(property, texture);
            }
            EditorUtility.SetDirty(material);
            var bakeShader = Shader.Find("Hidden/Bastion/GroundBake");
            if (bakeShader == null) Debug.LogError("Ground: shader Hidden/Bastion/GroundBake not found");
            else if (AssetDatabase.LoadAssetAtPath<Material>(BakePath) == null) AssetDatabase.CreateAsset(new Material(bakeShader) { name = "GroundBake" }, BakePath);
            // The outline's flat shapes and its water (frameArt.ts: roughness 0.92; the water 0.22, a little metal).
            var flat = Shader.Find("Bastion/Flat");
            if (flat == null) Debug.LogError("Ground: shader Bastion/Flat not found");
            else
            {
                Flat(flat, "FrameShape", 0.08f, 0);
                Flat(flat, "FrameWater", 0.78f, 0.1f);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Ground: " + MaterialPath + ", " + BakePath);
        }

        static void Flat(Shader shader, string name, float smoothness, float metallic)
        {
            var path = $"Assets/Bastion/Client/Resources/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
        }
    }
}
