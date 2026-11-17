using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The camping (runs, H9 §5; .dev-snapshots/arte/unity/runs/camping/): what Apocalypse's mobile homes are made of, to
    /// fit them to the engine's caravans (their parts, sizes, floor, door and windows), with pictures of each.
    /// </summary>
    public static class CampingLab
    {
        static readonly string[] Trailers =
        {
            "Assets/Synty/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Trailer_01.prefab",
            "Assets/Synty/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Trailer_02.prefab",
            "Assets/Synty/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Trailer_Damaged_01.prefab",
            "Assets/Synty/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Trailer_Damaged_02.prefab",
        };

        /// <summary>
        /// Each mobile home's parts (their boxes in the prefab's metres, vertices, materials), the pieces of its glass and of
        /// its inside (each island's box), the heights its up-facing surfaces stand at (its floor), and pictures from above
        /// (whole, inside only) and from each side into BASTION_OUT.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.CampingLab.Remolques -quit
        /// </summary>
        public static void Remolques()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapLab.Lighting(true);
            string folder = Environment.GetEnvironmentVariable("BASTION_OUT");
            if (folder != null) Directory.CreateDirectory(folder);
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200;
            foreach (string path in Trailers)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.Log($"[camping] missing {path}");
                    continue;
                }
                string name = Path.GetFileNameWithoutExtension(path);
                var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                made.transform.position = Vector3.zero;
                var whole = new Bounds();
                bool first = true;
                foreach (var f in made.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = f.sharedMesh;
                    if (mesh == null) continue;
                    var r = f.GetComponent<MeshRenderer>();
                    var box = r != null ? r.bounds : new Bounds();
                    if (first) whole = box;
                    else whole.Encapsulate(box);
                    first = false;
                    int tris = 0;
                    for (int k = 0; k < mesh.subMeshCount; k++) tris += (int)mesh.GetIndexCount(k) / 3;
                    Debug.Log($"[camping] {name}/{f.name}: mesh {mesh.name}, at {f.transform.localPosition:F3} turned {f.transform.localEulerAngles:F1} scaled {f.transform.localScale:F2}; box {box.min:F2}–{box.max:F2}; {mesh.vertexCount} vertices, {tris} triangles, {mesh.subMeshCount} submeshes, readable {mesh.isReadable}; materials {string.Join(", ", r != null ? r.sharedMaterials.Select(m => m != null ? m.name + " [" + m.shader.name + "]" : "null") : new string[0])}");
                    // (In the editor, out of play, a mesh is read whether it is marked readable or not.)
                    if (mesh.vertices.Length == 0) continue;
                    var islands = Islands(mesh, f.transform.localToWorldMatrix);
                    if (f.name.Contains("Glass") || f.name.Contains("Interior") || islands.Count <= 40)
                        foreach (var (b, n) in islands.OrderBy(i => i.box.center.z).ThenBy(i => i.box.center.x))
                            Debug.Log($"[camping]   {f.name} island: {b.min:F2}–{b.max:F2} ({n} triangles)");
                    // What faces up and how much of it stands at each height (10 cm steps): the floor stands out.
                    var heights = new SortedDictionary<int, float>();
                    var v = mesh.vertices;
                    var m = f.transform.localToWorldMatrix;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        var ix = mesh.GetTriangles(s);
                        for (int t = 0; t < ix.Length; t += 3)
                        {
                            Vector3 a = m.MultiplyPoint3x4(v[ix[t]]), b = m.MultiplyPoint3x4(v[ix[t + 1]]), c = m.MultiplyPoint3x4(v[ix[t + 2]]);
                            var cross = Vector3.Cross(b - a, c - a);
                            if (cross.magnitude < 1e-6f || cross.normalized.y < 0.95f) continue;
                            int h = Mathf.RoundToInt((a.y + b.y + c.y) / 3 * 10);
                            heights[h] = (heights.TryGetValue(h, out float had) ? had : 0) + cross.magnitude / 2;
                        }
                    }
                    Debug.Log($"[camping]   {f.name} up-facing area by height: {string.Join(" ", heights.Where(kv => kv.Value > 0.3f).Select(kv => $"{kv.Key / 10f:0.0}m:{kv.Value:0.0}"))}");
                }
                Debug.Log($"[camping] {name}: whole box {whole.min:F2}–{whole.max:F2}, size {whole.size:F2}");
                if (folder != null)
                {
                    var middle = whole.center;
                    float half = Mathf.Max(whole.size.x, whole.size.z) / 2 + 0.5f;
                    void Shot(string view, Vector3 from, Vector3 up, float size, float aspect)
                    {
                        camera.transform.position = middle + from * 30;
                        camera.transform.rotation = Quaternion.LookRotation(-from, up);
                        camera.orthographicSize = size;
                        int h = 900, w = Mathf.RoundToInt(h * aspect);
                        Shots.Render(camera, w, h, null);
                        Shots.Render(camera, w, h, Path.Combine(folder, $"{name}-{view}.png"));
                    }
                    Shot("arriba", Vector3.up, Vector3.forward, half, whole.size.x / whole.size.z + 0.1f);
                    Shot("este", Vector3.right, Vector3.up, Mathf.Max(whole.size.y, 3) / 2 + 0.3f, (whole.size.z + 1) / (Mathf.Max(whole.size.y, 3) + 0.6f));
                    Shot("oeste", Vector3.left, Vector3.up, Mathf.Max(whole.size.y, 3) / 2 + 0.3f, (whole.size.z + 1) / (Mathf.Max(whole.size.y, 3) + 0.6f));
                    Shot("sur", Vector3.back, Vector3.up, Mathf.Max(whole.size.y, 3) / 2 + 0.3f, (whole.size.x + 1) / (Mathf.Max(whole.size.y, 3) + 0.6f));
                    // The inside alone: the shell, its glass and skirt off.
                    foreach (var t in made.GetComponentsInChildren<MeshRenderer>(true))
                        if (!t.name.Contains("Interior")) t.enabled = false;
                    Shot("dentro", Vector3.up, Vector3.forward, half, whole.size.x / whole.size.z + 0.1f);
                    Shot("dentro-este", (Vector3.right * 0.6f + Vector3.up).normalized, Vector3.up, half * 0.7f, 1.6f);
                    Shot("dentro-oeste", (Vector3.left * 0.6f + Vector3.up).normalized, Vector3.up, half * 0.7f, 1.6f);
                }
                UnityEngine.Object.DestroyImmediate(made);
            }
        }

        /// <summary>
        /// The two mobile homes the game draws cut through from above at BASTION_CORTE metres (2.4; what is higher is left
        /// out: a plan of their walls, doors and inside), whole and the shell alone, and each seen from its door's side
        /// close, into BASTION_OUT.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.CampingLab.Cortes -quit
        /// </summary>
        public static void Cortes()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapLab.Lighting(true);
            string folder = Environment.GetEnvironmentVariable("BASTION_OUT") ?? throw new ArgumentException("BASTION_OUT is missing");
            Directory.CreateDirectory(folder);
            float at = float.TryParse(Environment.GetEnvironmentVariable("BASTION_CORTE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float c) ? c : 2.4f;
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            foreach (string path in Trailers.Take(2))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                var made = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                made.transform.position = Vector3.zero;
                foreach (bool shellOnly in new[] { false, true })
                {
                    foreach (var r in made.GetComponentsInChildren<MeshRenderer>(true))
                        r.enabled = !r.name.Contains("Skirt") && (!shellOnly || !r.name.Contains("Interior"));
                    camera.orthographic = true;
                    camera.orthographicSize = 6.6f;
                    camera.nearClipPlane = 0.01f;
                    camera.farClipPlane = 20;
                    camera.transform.position = new Vector3(0, at, 0);
                    camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    Shots.Render(camera, 600, 1320, null);
                    Shots.Render(camera, 600, 1320, Path.Combine(folder, $"{name}-corte{(shellOnly ? "-casco" : "")}.png"));
                }
                // Its door's side close, a man's height up, the shell and glass alone.
                foreach (var r in made.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = !r.name.Contains("Skirt") && !r.name.Contains("Interior");
                camera.orthographic = false;
                camera.fieldOfView = 60;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 100;
                foreach (int side in new[] { -1, 1 })
                {
                    camera.transform.position = new Vector3(side * 7.5f, 1.8f, 0);
                    camera.transform.LookAt(new Vector3(0, 1.6f, 0));
                    Shots.Render(camera, 1600, 700, null);
                    Shots.Render(camera, 1600, 700, Path.Combine(folder, $"{name}-lado{(side < 0 ? "-x" : "+x")}.png"));
                }
                UnityEngine.Object.DestroyImmediate(made);
            }
        }

        /// <summary>The prefabs the game's caravans are drawn from (Map.Caravans), in Resources/CaravanArt.asset.</summary>
        static readonly string[] Drawn = { Trailers[0], Trailers[1] };

        /// <summary>
        /// Makes Resources/CaravanArt.asset (Bastion.Client.Map.CaravanArt): the mobile homes the camping's caravans are drawn
        /// from, their FBX imported readable (the game reads their meshes into its own). Run it again when they change:
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.CampingLab.MakeArt -quit
        /// </summary>
        public static void MakeArt()
        {
            const string asset = "Assets/Bastion/Client/Resources/CaravanArt.asset";
            var models = Drawn.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToList();
            foreach (var model in models.Where(m => m != null))
                foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null || f.sharedMesh.isReadable) continue;
                    if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(f.sharedMesh)) is ModelImporter importer) || importer.isReadable) continue;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    Debug.Log($"[camping] readable: {importer.assetPath}");
                }
            var art = AssetDatabase.LoadAssetAtPath<Bastion.Client.Map.CaravanArt>(asset);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<Bastion.Client.Map.CaravanArt>();
                AssetDatabase.CreateAsset(art, asset);
            }
            var found = Enumerable.Range(0, Drawn.Length).Where(k => models[k] != null).ToList();
            art.names = found.Select(k => Path.GetFileNameWithoutExtension(Drawn[k])).ToArray();
            art.models = found.Select(k => models[k]).ToArray();
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"[camping] {asset}: {string.Join(", ", art.names)}" + (found.Count < Drawn.Length ? "; missing: " + string.Join(", ", Drawn.Where((d, k) => models[k] == null)) : ""));
        }

        /// <summary>A mesh's pieces that share no corner (welded by place, a millimetre), each one's box in the prefab and its triangles.</summary>
        static List<(Bounds box, int n)> Islands(Mesh mesh, Matrix4x4 m)
        {
            var v = mesh.vertices;
            var key = new int[v.Length];
            var at = new Dictionary<Vector3Int, int>();
            for (int k = 0; k < v.Length; k++)
            {
                var p = Vector3Int.RoundToInt(v[k] * 1000);
                if (!at.TryGetValue(p, out int id)) at[p] = id = at.Count;
                key[k] = id;
            }
            var parent = Enumerable.Range(0, at.Count).ToArray();
            int Find(int a)
            {
                while (parent[a] != a) a = parent[a] = parent[parent[a]];
                return a;
            }
            var tris = new List<int>();
            for (int s = 0; s < mesh.subMeshCount; s++) tris.AddRange(mesh.GetTriangles(s));
            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = Find(key[tris[t]]), b = Find(key[tris[t + 1]]), c = Find(key[tris[t + 2]]);
                parent[b] = a;
                parent[Find(c)] = Find(a);
            }
            var boxes = new Dictionary<int, (Bounds box, int n)>();
            for (int t = 0; t < tris.Count; t += 3)
            {
                int root = Find(key[tris[t]]);
                for (int c = 0; c < 3; c++)
                {
                    var p = m.MultiplyPoint3x4(v[tris[t + c]]);
                    if (!boxes.TryGetValue(root, out var had)) had = (new Bounds(p, Vector3.zero), 0);
                    had.box.Encapsulate(p);
                    boxes[root] = (had.box, had.n + (c == 0 ? 1 : 0));
                }
            }
            return boxes.Values.ToList();
        }
    }
}
