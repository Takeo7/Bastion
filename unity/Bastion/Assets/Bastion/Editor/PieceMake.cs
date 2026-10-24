using System.IO;
using System.Linq;
using Bastion.Client.Map;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The kit pieces for the game (docs/UNITY-ARTE.md): Assets/Synty/Pieces' cut FBX (unity/tools/pieces-cut.py) set to
    /// import as plain readable meshes, and Resources/PieceCatalog.asset made from them, each with the materials of its
    /// pack's own prefab of the same model (a part by its mesh's name). And PieceLab, every piece in rows to look at.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.PieceMake.Run -quit
    /// </summary>
    public static class PieceMake
    {
        const string Folder = "Assets/Synty/Pieces";
        const string Asset = "Assets/Bastion/Client/Resources/PieceCatalog.asset";
        const string GlassPath = "Assets/Bastion/Client/Shaders/Glass.mat";
        /// <summary>The pieces' own materials (Bastion/Building), one for each Synty material they are drawn with.</summary>
        const string MaterialFolder = "Assets/Bastion/Client/Map/Materials";

        /// <summary>Each pack's main atlas material (Woodland's pieces are painted from Apocalypse's: syntyManifest.ts PACKS).</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> MainMaterials = new System.Collections.Generic.Dictionary<string, string>
        {
            ["apocalypse"] = "PolygonApocalypse_01_A", ["town"] = "PolygonTown_01_A", ["city"] = "PolygonCity_01_A", ["office"] = "PolygonOffice_01_A",
            ["woodland"] = "PolygonApocalypse_01_A",
        };

        public static void Run()
        {
            foreach (string path in Directory.GetFiles(Folder, "*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer.isReadable && importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation) continue;
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
            }
            var art = (JObject)JObject.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/Pieces.json"))["pieces"];
            var prefabOf = MapLab.Prefabs();
            var materialOf = MapLab.Materials();
            var catalog = AssetDatabase.LoadAssetAtPath<PieceCatalog>(Asset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PieceCatalog>();
                AssetDatabase.CreateAsset(catalog, Asset);
            }
            catalog.pieces.Clear();
            var missing = new System.Collections.Generic.List<string>();
            var made = new System.Collections.Generic.Dictionary<Material, Material>();
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Bastion/Client/Map", "Materials");
            foreach (var p in art.Properties())
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{p.Name}.fbx");
                var synty = prefabOf((string)p.Value["fbx"]);
                if (model == null || synty == null)
                {
                    missing.Add(p.Name + (model == null ? " (not cut)" : " (no Synty model)"));
                    if (model == null) continue;
                }
                var theirs = synty != null ? synty.GetComponentsInChildren<Renderer>() : new Renderer[0];
                // Without the pack's own model in Unity (a few are only in its source files), its main atlas.
                var main = materialOf(MainMaterials.TryGetValue((string)p.Value["pack"], out var name) ? name : "PolygonApocalypse_01_A");
                var materials = model.GetComponentsInChildren<MeshFilter>().SelectMany(f =>
                {
                    var own = theirs.FirstOrDefault(r => r.name == f.name) ?? theirs.FirstOrDefault();
                    var mats = own != null ? own.sharedMaterials : new Material[0];
                    return Enumerable.Range(0, f.sharedMesh.subMeshCount).Select(k => mats.Length == 0 ? main : mats[Mathf.Min(k, mats.Length - 1)] ?? main);
                }).ToArray();
                catalog.pieces.Add(new PieceCatalog.PieceEntry { id = p.Name, model = model, materials = materials.Select(m => Own(m, made)).ToArray() });
            }
            catalog.glass = Glass();
            // Floors and roofs in swatches of Town's atlas (0.10.1: roughness 0.9 and 0.8, their front faces).
            var town = Atlas(materialOf("PolygonTown_01_A"));
            catalog.floors = Swatches("Suelos", town, 0.1f);
            catalog.roofs = Swatches("Tejados", town, 0.2f);
            Debug.Log("[pieces] materials: " + string.Join(", ", made.Select(m => $"{m.Key.name} ({m.Key.shader.name}{(m.Key.renderQueue >= 3000 ? ", transparent" : "")}) -> {(m.Value == m.Key ? "kept" : m.Value.name)}")));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[pieces] {Asset}: {catalog.pieces.Count} pieces" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : ""));
        }

        /// <summary>
        /// A piece's material for `synty`: Bastion/Building with its atlas, both faces (0.10.1: one material for every
        /// piece, roughness 0.85). A see-through one is kept as it is.
        /// </summary>
        static Material Own(Material synty, System.Collections.Generic.Dictionary<Material, Material> made)
        {
            if (synty == null) return null;
            if (made.TryGetValue(synty, out var own)) return own;
            if (synty.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent) return made[synty] = synty;
            own = MaterialAsset(synty.name, false);
            own.SetTexture("_BaseMap", Atlas(synty));
            own.SetColor("_BaseColor", synty.HasProperty("_BaseColor") ? synty.GetColor("_BaseColor") : Color.white);
            own.SetFloat("_Smoothness", 0.15f);
            own.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            EditorUtility.SetDirty(own);
            return made[synty] = own;
        }

        static Material Swatches(string name, Texture atlas, float smoothness)
        {
            var m = MaterialAsset(name, true);
            m.SetTexture("_BaseMap", atlas);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MaterialAsset(string name, bool swatches)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var shader = Shader.Find("Bastion/Building");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetFloat("_Swatch", swatches ? 1 : 0);
            if (swatches) m.EnableKeyword("_SWATCH");
            else m.DisableKeyword("_SWATCH");
            return m;
        }

        /// <summary>A Synty material's atlas (Generic_Basic's _Albedo_Map, or URP's _BaseMap).</summary>
        static Texture Atlas(Material synty)
        {
            if (synty == null) return null;
            foreach (string name in new[] { "_Albedo_Map", "_BaseMap", "_MainTex" })
                if (synty.HasProperty(name) && synty.GetTexture(name) != null) return synty.GetTexture(name);
            return null;
        }

        /// <summary>
        /// The windows' glass as 0.10.1's (0x9fc4d8 at 0.38, roughness 0.08, metalness 0.3, a dark emissive, both faces,
        /// no depth write): Bastion/Building's see-through panes (_GLASS), so the cutaway takes them with their walls.
        /// </summary>
        static Material Glass()
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            var shader = Shader.Find("Bastion/Building");
            if (glass == null)
            {
                glass = new Material(shader) { name = "Cristal" };
                AssetDatabase.CreateAsset(glass, GlassPath);
            }
            glass.shader = shader;
            glass.SetFloat("_Swatch", 0);
            glass.DisableKeyword("_SWATCH");
            glass.SetFloat("_Glass", 1);
            glass.EnableKeyword("_GLASS");
            glass.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            var colour = new Color32(0x9f, 0xc4, 0xd8, 255);
            glass.SetColor("_BaseColor", new Color(colour.r / 255f, colour.g / 255f, colour.b / 255f, 0.38f));
            glass.SetColor("_EmissionColor", new Color32(0x20, 0x30, 0x38, 255));
            glass.SetFloat("_Smoothness", 0.92f);
            EditorUtility.SetDirty(glass);
            return glass;
        }

        /// <summary>Every piece of the catalog in rows of eight, set as a tile's (0.10.1's prepare), looked at from the south.</summary>
        public static void Lab()
        {
            MapLab.Lighting(true);
            var row = new GameObject("Piezas").transform;
            var ids = PieceCatalog.Load().pieces.Select(p => p.id).ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                // In 0.10.1's frame (metres: x east, z south), two metres apart along a row, three between rows.
                var place = Matrix4x4.Translate(new Vector3(i % 8 * 2.4f, 0, i / 8 * 3.2f));
                var holder = new GameObject(ids[i]).transform;
                holder.SetParent(row, false);
                foreach (var part in Pieces.Parts(ids[i]))
                {
                    var m = MapSpace.FromGame(place * part.fit);
                    var o = new GameObject(part.name);
                    o.transform.SetParent(holder, false);
                    o.transform.SetPositionAndRotation(m.GetPosition(), m.rotation);
                    o.transform.localScale = m.lossyScale;
                    o.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                    o.AddComponent<MeshRenderer>().sharedMaterials = part.materials;
                }
            }
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.fieldOfView = 40;
            var middle = MapSpace.FromGame(Matrix4x4.Translate(new Vector3(3.5f * 2.4f, 1.6f, 2.5f * 3.2f))).GetPosition();
            camera.transform.position = middle + new Vector3(0, 7, -21);
            camera.transform.LookAt(middle);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
        }
    }
}
