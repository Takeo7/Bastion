using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The outline drawn (UI's; FrameArt says what it is): its props merged by chunks of the map (and past its edge), each
    /// casting a shadow or not as FrameArt says; its boxes and wedges in one mesh of flat colours, its water in another.
    /// Built once a map: the outline never changes.
    /// </summary>
    public static class FrameView
    {
        /// <summary>Tiles a side of a chunk of the outline's props.</summary>
        const int Chunk = 16;

        /// <summary>The outline of `s` under `parent` ("Marco"); null if the map has none.</summary>
        public static GameObject Build(GameState s, PropParts parts, Transform parent)
        {
            if (s.Frame == null || s.Frame.Count == 0) return null;
            var (props, shapes) = FrameArt.Dress(s);
            var root = new GameObject("Marco");
            root.transform.SetParent(parent, false);
            foreach (var chunk in props.GroupBy(d => (Mathf.FloorToInt(d.Matrix.m03 / Chunk), Mathf.FloorToInt(d.Matrix.m23 / Chunk))))
                parts.Merge(root.transform, $"Trozo {chunk.Key.Item1},{chunk.Key.Item2}", chunk.Select(d => (d.Id, PropParts.InMetres(d.Matrix), d.Cast)));
            Shapes(root.transform, "Formas", shapes.Where(x => x.Form != FrameArt.Water).ToList(), "FrameShape", true);
            Shapes(root.transform, "Agua", shapes.Where(x => x.Form == FrameArt.Water).ToList(), "FrameWater", false);
            return root;
        }

        /// <summary>The shapes in one mesh: each unit box, wedge or square placed by its matrix (0.10.1's frame, tiles) in metres, its colour per vertex.</summary>
        static void Shapes(Transform parent, string name, List<FrameArt.Shape> shapes, string material, bool cast)
        {
            if (shapes.Count == 0) return;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            foreach (var shape in shapes)
            {
                var m = shape.Matrix;
                var normalMatrix = m.inverse.transpose;
                var colour = Hex(shape.Color).linear;
                var (points, faces) = Unit(shape.Form);
                foreach (var face in faces)
                {
                    // A face's own vertices (flat), its normal from its first triangle, in Unity's frame (z north: the
                    // mirror turns the winding round).
                    var a = m.MultiplyPoint3x4(points[face[0]]);
                    var b = m.MultiplyPoint3x4(points[face[1]]);
                    var c = m.MultiplyPoint3x4(points[face[2]]);
                    var n = Vector3.Cross(b - a, c - a).normalized;
                    n = new Vector3(n.x, n.y, -n.z);
                    int start = vertices.Count;
                    foreach (int k in face)
                    {
                        var p = m.MultiplyPoint3x4(points[k]);
                        vertices.Add(new Vector3(p.x * MapSpace.Tile, p.y * MapSpace.Tile, -p.z * MapSpace.Tile));
                        normals.Add(n);
                        colors.Add(colour);
                    }
                    for (int t = 1; t + 1 < face.Length; t++)
                    {
                        triangles.Add(start);
                        triangles.Add(start + t + 1);
                        triangles.Add(start + t);
                    }
                }
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Resources.Load<Material>(material);
            if (renderer.sharedMaterial == null) Debug.LogError($"Frame: Resources/{material}.mat missing (Bastion.EditorTools.GroundMake.Run)");
            renderer.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        static readonly Vector3[] BoxPoints =
        {
            new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f),
            new Vector3(-0.5f, 1, -0.5f), new Vector3(0.5f, 1, -0.5f), new Vector3(0.5f, 1, 0.5f), new Vector3(-0.5f, 1, 0.5f),
        };

        /// <summary>A box's faces, each wound outwards as three has them (counter-clockwise seen from outside).</summary>
        static readonly int[][] BoxFaces =
        {
            new[] { 4, 7, 6, 5 }, new[] { 0, 1, 2, 3 }, new[] { 0, 4, 5, 1 }, new[] { 3, 2, 6, 7 }, new[] { 0, 3, 7, 4 }, new[] { 1, 5, 6, 2 },
        };

        /// <summary>frameArt.ts wedgeGeometry: its base 1 × 1 on y = 0, rising from z = −½ to 1 at z = +½.</summary>
        static readonly Vector3[] WedgePoints =
        {
            new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(-0.5f, 1, 0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 1, 0.5f),
        };

        static readonly int[][] WedgeFaces =
        {
            new[] { 0, 2, 5 }, new[] { 0, 5, 3 }, new[] { 1, 4, 5 }, new[] { 1, 5, 2 }, new[] { 0, 3, 4 }, new[] { 0, 4, 1 }, new[] { 0, 1, 2 }, new[] { 3, 5, 4 },
        };

        static readonly Vector3[] WaterPoints = { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
        static readonly int[][] WaterFaces = { new[] { 0, 1, 2, 3 } };

        static (Vector3[], int[][]) Unit(string form) => form == FrameArt.Wedge ? (WedgePoints, WedgeFaces) : form == FrameArt.Water ? (WaterPoints, WaterFaces) : (BoxPoints, BoxFaces);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
