using System;
using System.Linq;
using Bastion.Client.Map;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The police station's kits (H9 §3, WallLayout's «police», «policeBars», «policeCounter»): which way each piece's
    /// materials face as the walls lay it (its frame: x along the wall, z out of it), so a piece's brick goes out and its
    /// plaster in (Pieces.json `out`; the cut piece's submeshes need not come in the order of the pack's prefab). For each
    /// piece (BASTION_PIEZAS_ID=id,id…; every «police…» piece of the catalog unless said), each part's submeshes: its
    /// material and how much of its area looks out (+z), in (−z), up, and along the wall, in m².
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.ComisariaLab.Caras -quit
    /// The kit's pictures are MapLab's on the engine's sample (BASTION_MAP=…/comisaria.json wrapped as {"state": …}):
    /// .dev-snapshots/arte/unity/runs/comisaria/LEEME.md.
    /// </summary>
    public static class ComisariaLab
    {
        public static void Caras()
        {
            var ids = Environment.GetEnvironmentVariable("BASTION_PIEZAS_ID")?.Split(',')
                ?? PieceCatalog.Load().pieces.Select(p => p.id).Where(id => id.StartsWith("police")).ToArray();
            foreach (string id in ids)
                foreach (var part in Pieces.Parts(id))
                {
                    if (part.mesh == null) continue;
                    var vertices = part.mesh.vertices;
                    // (Where its bulk stands across the wall: out of its middle plane, z > 0, or in.)
                    var zs = vertices.Select(v => part.fit.MultiplyPoint3x4(v).z).ToList();
                    Debug.Log(FormattableString.Invariant($"[caras] {id}/{part.name}: z {zs.Min():0.000} to {zs.Max():0.000} m, its middle {zs.Average():0.000}"));
                    for (int k = 0; k < part.mesh.subMeshCount; k++)
                    {
                        float outward = 0, inward = 0, up = 0, along = 0;
                        var tris = part.mesh.GetTriangles(k);
                        for (int t = 0; t + 2 < tris.Length; t += 3)
                        {
                            Vector3 a = part.fit.MultiplyPoint3x4(vertices[tris[t]]), b = part.fit.MultiplyPoint3x4(vertices[tris[t + 1]]), c = part.fit.MultiplyPoint3x4(vertices[tris[t + 2]]);
                            var cross = Vector3.Cross(b - a, c - a);
                            float area = cross.magnitude / 2;
                            if (area < 1e-8f) continue;
                            var n = cross.normalized;
                            // (The frame may be mirrored: the winding then turns, and with it the normal; the mesh's own normals say.)
                            var given = part.fit.MultiplyVector(part.mesh.normals.Length > tris[t] ? part.mesh.normals[tris[t]] : n).normalized;
                            if (Vector3.Dot(n, given) < 0) n = -n;
                            if (n.z > 0.7f) outward += area;
                            else if (n.z < -0.7f) inward += area;
                            else if (Mathf.Abs(n.y) > 0.7f) up += area;
                            else along += area;
                        }
                        string material = k < part.materials.Length && part.materials[k] != null ? part.materials[k].name : "none";
                        Debug.Log(FormattableString.Invariant($"[caras] {id}/{part.name} sub {k} {material}: out {outward:0.00}, in {inward:0.00}, up/down {up:0.00}, along {along:0.00} m²"));
                    }
                }
        }
    }
}
