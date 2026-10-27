using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The bodies left in the houses and clinics (0.10.1's tools/synty/bake-medical-corpses.mjs: mapView.ts dressing's
    /// corpseCivilian and corpseStaff): an Apocalypse character in the last frame of a death (FigureCatalog's clip, humanoid),
    /// its meshes baked into one, its middle over the origin, its lowest point on it, as a prefab in Assets/Synty/Extra
    /// (off git, as the rest of Synty) under the name MapArt.json gives the prop, so the map's catalog finds it. MapCatalogMake
    /// runs it first.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.CorpseBake.Run -quit
    /// </summary>
    public static class CorpseBake
    {
        const string Folder = "Assets/Synty/Extra";

        /// <summary>The prop's model name (MapArt.json), the character it is made of, the death it lies in.</summary>
        static readonly (string model, string character, string death)[] Corpses =
        {
            ("SK_Chr_Business_Male_01", "Assets/Synty/PolygonApocalypse/Prefabs/Characters/SM_Chr_Business_Male_01.prefab", "deathHeadFMasc"),
            ("SK_Chr_Waitress_Female_01", "Assets/Synty/PolygonApocalypse/Prefabs/Characters/SM_Chr_Waitress_Female_01.prefab", "deathStomachFemn"),
        };

        public static void Run()
        {
            var catalog = FigureCatalog.Load();
            if (!AssetDatabase.IsValidFolder($"{Folder}/Corpses")) AssetDatabase.CreateFolder(Folder, "Corpses");
            foreach (var (model, character, death) in Corpses)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(character);
                var clip = catalog != null ? catalog.Clip(death) : null;
                if (prefab == null || clip == null)
                {
                    Debug.LogWarning($"[corpses] {model}: no {(prefab == null ? character : death)}");
                    continue;
                }
                var body = (GameObject)Object.Instantiate(prefab);
                body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationMode.StartAnimationMode();
                try
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(body, clip, clip.length - 0.001f);
                    AnimationMode.EndSampling();
                    var combine = new List<CombineInstance>();
                    var materials = new List<Material>();
                    foreach (var r in body.GetComponentsInChildren<Renderer>())
                    {
                        Mesh mesh;
                        Matrix4x4 at;
                        if (r is SkinnedMeshRenderer skinned)
                        {
                            mesh = new Mesh();
                            skinned.BakeMesh(mesh, true);
                            at = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                        }
                        else if (r.GetComponent<MeshFilter>()?.sharedMesh is Mesh own)
                        {
                            mesh = own;
                            at = r.transform.localToWorldMatrix;
                        }
                        else continue;
                        for (int k = 0; k < mesh.subMeshCount; k++)
                        {
                            combine.Add(new CombineInstance { mesh = mesh, subMeshIndex = k, transform = at });
                            materials.Add(r.sharedMaterials[Mathf.Min(k, r.sharedMaterials.Length - 1)]);
                        }
                    }
                    var baked = new Mesh { name = model, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    baked.CombineMeshes(combine.ToArray(), false, true);
                    // Its middle over the origin, its lowest point on it (as 0.10.1's bake).
                    var box = baked.bounds;
                    baked.SetVertices(baked.vertices.Select(v => v - new Vector3(box.center.x, box.min.y, box.center.z)).ToArray());
                    baked.RecalculateBounds();
                    string meshPath = $"{Folder}/Corpses/{model}.asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(baked, meshPath);
                    var holder = new GameObject(model);
                    holder.AddComponent<MeshFilter>().sharedMesh = baked;
                    holder.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                    PrefabUtility.SaveAsPrefabAsset(holder, $"{Folder}/{model}.prefab");
                    Object.DestroyImmediate(holder);
                    Debug.Log(System.FormattableString.Invariant($"[corpses] {Folder}/{model}.prefab: {character.Split('/').Last()} in {death} ({clip.length:0.00} s), {baked.vertexCount} vertices, {baked.bounds.size} m"));
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                    Object.DestroyImmediate(body);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
