using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// How Unity imports the building kits' FBX (their axes and units against the FBX's, which 0.10.1's piece boxes are
    /// in): each model's nodes and each mesh's box in the model's frame.
    /// unity/tools/unity.sh -executeMethod Bastion.EditorTools.PieceProbe.Run -quit
    /// </summary>
    public static class PieceProbe
    {
        public static void Run()
        {
            foreach (string path in new[]
            {
                "Assets/Synty/PolygonTown/Models/SM_Bld_House_ExteriorWall_GroundFloor_01.fbx",
                "Assets/Synty/PolygonTown/Models/SM_Bld_House_ExteriorWall_GroundFloor_Corner_01.fbx",
                "Assets/Synty/PolygonApocalypse/Models/SM_Bld_Bunker_Wall_Concrete_x1_01.fbx",
                "Assets/Synty/PolygonCity/Models/SM_Bld_Apartment_01.fbx",
                "Assets/Synty/PolygonOffice/Models/SM_Bld_Wall_Glass_01.fbx",
            }.Concat(System.IO.Directory.Exists("Assets/Synty/Pieces") ? System.IO.Directory.GetFiles("Assets/Synty/Pieces", "*.fbx") : new string[0]))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    string found = AssetDatabase.FindAssets($"{name} t:Model").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
                    Debug.Log($"[pieces] {path}: not there" + (found != null ? $" (found at {found})" : ""));
                    if (found == null) continue;
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(found);
                }
                var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(model));
                Debug.Log($"[pieces] {model.name}: root rot {model.transform.localEulerAngles} scale {model.transform.localScale}; import scale {importer.globalScale}, useFileScale {importer.useFileScale}, bakeAxis {importer.bakeAxisConversion}");
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    var m = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var v = filter.sharedMesh.vertices.Select(p => m.MultiplyPoint3x4(p)).ToArray();
                    var b = new Bounds(v[0], Vector3.zero);
                    foreach (var p in v) b.Encapsulate(p);
                    Debug.Log($"[pieces]   {filter.name}: local pos {filter.transform.localPosition} rot {filter.transform.localEulerAngles} scale {filter.transform.localScale}; mesh {filter.sharedMesh.bounds.min:F3}..{filter.sharedMesh.bounds.max:F3}; in the model {b.min:F3}..{b.max:F3}; materials {string.Join(",", filter.GetComponent<Renderer>().sharedMaterials.Select(x => x != null ? x.name : "-"))}");
                }
            }
        }
    }
}
