using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Effects;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Makes Resources/FxArt.asset (Bastion.Client.Effects.FxArt) from Particle FX: its shapes, baked upright as the
    /// pack's models stand (their own turn put into the vertices), its sprites and the effects' shaders. Run it again
    /// after a new import of the pack: unity/tools/unity.sh -executeMethod Bastion.EditorTools.FxArtMake.Run -quit.
    /// </summary>
    public static class FxArtMake
    {
        const string Pack = "Assets/Synty/PolygonParticleFX";
        const string Path = "Assets/Bastion/Client/Resources/FxArt.asset";

        /// <summary>A molotov's bottle (FxArt.molotov): Apocalypse's.</summary>
        const string Molotov = "Assets/Synty/PolygonApocalypse/Prefabs/Weapons/Misc/SM_Wep_Molotov_01.prefab";

        public static void Run()
        {
            var missing = new List<string>();
            var art = AssetDatabase.LoadAssetAtPath<FxArt>(Path);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<FxArt>();
                AssetDatabase.CreateAsset(art, Path);
            }
            // The shapes baked before are sub-assets: they go, and are made again.
            foreach (var old in AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Mesh>()) Object.DestroyImmediate(old, true);

            art.unlit = Shader.Find("Bastion/FxUnlit");
            art.lit = Shader.Find("Universal Render Pipeline/Lit");
            if (art.unlit == null) missing.Add("Bastion/FxUnlit");

            Mesh Shape(string fbx)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Pack}/Models/{fbx}.fbx");
                var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null)
                {
                    missing.Add(fbx);
                    return null;
                }
                var m = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = fbx;
                mesh.vertices = mesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
                mesh.normals = mesh.normals.Select(n => m.MultiplyVector(n).normalized).ToArray();
                mesh.RecalculateBounds();
                AssetDatabase.AddObjectToAsset(mesh, art);
                return mesh;
            }

            Texture2D Sprite(string png)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Pack}/Textures/{png}.png");
                if (tex == null) missing.Add(png);
                return tex;
            }

            art.smokePuff = Shape("FX_Sphere_Puff_01");
            art.goreChunk1 = Shape("SM_GoreChunk_01");
            art.goreChunk2 = Shape("SM_GoreChunk_02");
            art.goreChunk3 = Shape("SM_GoreChunk_04");
            art.flame = Shape("SM_Flame_FX");
            art.medCross = Shape("FX_Healthcube_01");
            art.woodChip = Shape("FX_WoodChip_01");

            art.polyDisc = Sprite("PolygonParticles_Circle_01");
            art.sparkle = Sprite("PolygonParticles_Sparkle_01");
            art.shockRing = Sprite("PolygonParticles_Ring_01");
            art.groundCrack = Sprite("PolygonParticles_GroundBreak_01");
            art.lightning = Sprite("PolygonParticles_Lightning_01");
            art.semiCircle = Sprite("PolygonParticles_SemiCircle_01");

            GameObject Effect(string name)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Pack}/Prefabs/{name}.prefab");
                if (prefab == null) missing.Add(name);
                return prefab;
            }

            art.fireSmall = Effect("FX_Fire_Small_02");
            art.fire = Effect("FX_Fire_01");
            art.fireBig = Effect("FX_Fire_Big_01");
            art.embers = Effect("FX_Embers_01");
            art.smoke = Effect("FX_Smoke_Black_01");
            art.molotov = AssetDatabase.LoadAssetAtPath<GameObject>(Molotov);
            if (art.molotov == null) missing.Add(Molotov);

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log("[fx] " + Path + (missing.Count > 0 ? "; missing: " + string.Join(", ", missing) : "; all there"));
        }
    }
}
