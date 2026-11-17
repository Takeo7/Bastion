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
    /// runs it first. The police station's are Sidekick's (Officer).
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
            // The runs' offices (H9, corpseWorker): Office's people in their work clothes.
            ("SK_Chr_OfficeWorker_Female_01", "Assets/Synty/PolygonOffice/Prefabs/Characters/SM_Chr_Business_Female_01.prefab", "deathSpineFemn"),
            ("SK_Chr_OfficeWorker_Male_01", "Assets/Synty/PolygonOffice/Prefabs/Characters/SM_Chr_Developer_Male_01.prefab", "deathStomachMasc"),
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
            foreach (var officer in Officers) Officer(catalog, officer.model, officer.outfit, officer.sex, officer.death);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The runs' police station's dead (H9, corpseOfficer): Sidekick's Modern Police (the 0.12.15's outfits), bareheaded,
        /// made as a soldier is (SoldierFigure), each its model name, outfit, sex and death.
        /// </summary>
        static readonly (string model, string outfit, string sex, string death)[] Officers =
        {
            ("SK_Chr_PoliceOfficer_Male_01", "police4", "m", "deathHeadFMasc"),
            ("SK_Chr_PoliceOfficer_Female_01", "police5", "f", "deathSpineFemn"),
        };

        /// <summary>
        /// A Sidekick figure in `outfit` lying in `death`'s last frame, baked as the others are (Run), its palette kept as a
        /// texture of its own (Corpses/{model}_Palette) on the material the other bodies have (POLYGON's, Office's people's,
        /// its atlas swapped for the palette): Sidekick's own shader shares its colour map with another Sidekick figure drawn
        /// next to it (UNITY-ARTE.md §8, SRP Batcher), and a prop has no block of its own to hold it.
        /// </summary>
        static void Officer(FigureCatalog catalog, string model, string outfit, string sex, string death)
        {
            var clip = catalog != null ? catalog.Clip(death) : null;
            var look = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonOffice/Materials/Alts/PolygonOffice_01_A.mat");
            var dress = Dress.Of(new Bastion.Engine.Appearance { Character = outfit, Head = "bare", Build = "normal", Sex = sex, Wardrobe = 1 }, model, sex);
            var body = clip != null && look != null ? SoldierFigure.Make(dress, model) : null;
            var marks = body != null ? body.GetComponent<SoldierMarks>() : null;
            if (marks == null)
            {
                Debug.LogWarning($"[corpses] {model}: no {(clip == null ? death : look == null ? "PolygonOffice_01_A" : outfit)}");
                if (body != null) Object.DestroyImmediate(body);
                return;
            }
            body.transform.SetParent(null, false);
            body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            body.transform.localScale = Vector3.one;
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(body, clip, clip.length - 0.001f);
                AnimationMode.EndSampling();
                var combine = new List<CombineInstance>();
                foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!r.enabled || r.sharedMesh == null) continue;
                    var mesh = new Mesh();
                    r.BakeMesh(mesh, true);
                    var at = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                    for (int k = 0; k < mesh.subMeshCount; k++) combine.Add(new CombineInstance { mesh = mesh, subMeshIndex = k, transform = at });
                }
                var baked = new Mesh { name = model, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                baked.CombineMeshes(combine.ToArray(), true, true);
                var box = baked.bounds;
                baked.SetVertices(baked.vertices.Select(v => v - new Vector3(box.center.x, box.min.y, box.center.z)).ToArray());
                baked.RecalculateBounds();
                string meshPath = $"{Folder}/Corpses/{model}.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(baked, meshPath);
                // (Its palette as SoldierPalette lays it, a cell 2 × 2 texels, readable to be kept.)
                var texels = new Color32[32 * 32];
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        for (int n = 0; n < 4; n++)
                            texels[(31 - y * 2 - n / 2) * 32 + x * 2 + n % 2] = marks.palette[y * 16 + x];
                var palette = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = model + "_Palette" };
                palette.SetPixels32(texels);
                palette.Apply(false);
                string palettePath = $"{Folder}/Corpses/{model}_Palette.asset";
                AssetDatabase.DeleteAsset(palettePath);
                AssetDatabase.CreateAsset(palette, palettePath);
                var material = new Material(look) { name = model };
                material.SetTexture("_Albedo_Map", palette);
                material.SetTexture("_Emission_Map", null);
                string materialPath = $"{Folder}/Corpses/{model}.mat";
                AssetDatabase.DeleteAsset(materialPath);
                AssetDatabase.CreateAsset(material, materialPath);
                var holder = new GameObject(model);
                holder.AddComponent<MeshFilter>().sharedMesh = baked;
                holder.AddComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(holder, $"{Folder}/{model}.prefab");
                Object.DestroyImmediate(holder);
                Debug.Log(System.FormattableString.Invariant($"[corpses] {Folder}/{model}.prefab: Sidekick {outfit} ({sex}) in {death} ({clip.length:0.00} s), {baked.vertexCount} vertices, {baked.bounds.size} m"));
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(body);
            }
        }
    }
}
