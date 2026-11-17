using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bastion.Client.Map;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The runs' maps that are one building (H9, MapBuilder.Interiors): the props' surfaces one puts things on, measured off
    /// their meshes. For each of BASTION_MODELOS (MapArt's ids, or model keys as MapLab.Prefabs takes them), the heights of
    /// what faces up (triangles whose normal is within 25° of up), area by area, in metres of the model as it is and in
    /// tiles as MapArt fits it: a shelf's boards, a bench's top, a desk's. Edit mode, nothing shot.
    ///   LOG=… BASTION_MODELOS=inShelves,inWorkbench unity/tools/unity.sh -executeMethod Bastion.EditorTools.InterioresLab.Superficies -quit
    /// </summary>
    public static class InterioresLab
    {
        public static void Superficies()
        {
            var prefabs = MapLab.Prefabs();
            foreach (var key in (Environment.GetEnvironmentVariable("BASTION_MODELOS") ?? "").Split(',').Where(k => k.Length > 0))
            {
                var art = MapArt.Prop(key);
                var model = prefabs(art != null ? MapArt.ModelKey(art) : key);
                if (model == null)
                {
                    Debug.Log($"[superficies] {key}: none");
                    continue;
                }
                var made = (GameObject)UnityEngine.Object.Instantiate(model);
                made.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var areas = new Dictionary<int, double>();
                var box = new Bounds();
                bool any = false;
                foreach (var f in made.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = f.sharedMesh;
                    if (mesh == null) continue;
                    var v = mesh.vertices.Select(p => f.transform.TransformPoint(p)).ToArray();
                    var t = mesh.triangles;
                    for (int k = 0; k + 2 < t.Length; k += 3)
                    {
                        Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                        foreach (var p in new[] { a, b, c })
                        {
                            if (!any) box = new Bounds(p, Vector3.zero);
                            else box.Encapsulate(p);
                            any = true;
                        }
                        var n = Vector3.Cross(b - a, c - a);
                        double area = n.magnitude / 2;
                        if (area < 1e-6 || n.normalized.y < 0.9f) continue;
                        int cm = Mathf.RoundToInt((a.y + b.y + c.y) / 3 * 100);
                        areas[cm] = (areas.TryGetValue(cm, out double was) ? was : 0) + area;
                    }
                }
                UnityEngine.Object.DestroyImmediate(made);
                // As MapArt fits it (MapBuilder.Prop): its height stood to `tall`, else times the fit along its longest side unless stretched.
                float scale = 1;
                if (art != null && art.tall > 0) scale = art.tall * MapSpace.Tile / Mathf.Max(box.size.y, 1e-4f);
                else if (art != null && art.fit > 0) scale = (art.stretch ? 1 : art.fit * MapSpace.Tile / Mathf.Max(box.size.x, box.size.z, 1e-4f)) * (art.thick > 0 ? art.thick : 1);
                var lines = areas.Where(kv => kv.Value > 0.02).OrderBy(kv => kv.Key)
                    .Select(kv => string.Format(CultureInfo.InvariantCulture, "{0:0.00} m ({1:0.00} tiles) {2:0.00} m²", kv.Key / 100.0, kv.Key / 100.0 * scale / MapSpace.Tile, kv.Value));
                Debug.Log(string.Format(CultureInfo.InvariantCulture, "[superficies] {0}: {1:0.00} × {2:0.00} × {3:0.00} m, from y {4:0.00}; up: {5}", key, box.size.x, box.size.y, box.size.z, box.min.y, string.Join("; ", lines)));
            }
        }
    }
}
