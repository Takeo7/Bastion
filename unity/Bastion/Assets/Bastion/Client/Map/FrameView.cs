using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The outline drawn (UI's; FrameArt says what it is): its props merged by chunks of the map (and past its edge), each
    /// casting a shadow or not as FrameArt says, but for its trees near what is played (Loose), in clumps of their own so
    /// their crowns fade as the map's do (TreeFade); its boxes and wedges in one mesh of flat colours, its water in another.
    /// Built once a map: the outline never changes.
    /// </summary>
    public static class FrameView
    {
        /// <summary>Tiles a side of a chunk of the outline's props.</summary>
        const int Chunk = 16;

        /// <summary>
        /// How near what is played (tiles, either way) the outline's trees with a crown stand apart (in clumps, LooseCell),
        /// named as TreeFade looks for them (its Crowns), not merged into their chunk: on the country's and the facilities'
        /// maps the squad began among them, under crowns that never faded (the 3D review's E-05, Arturo's choice B, 10/11:
        /// the crowns faded over the whole squad by then, but those were the outline's, merged by chunks, and TreeFade found
        /// none of them; on facility-2-hack all its pines are the outline's).
        /// </summary>
        const int Loose = 6;
        /// <summary>
        /// Tiles a side of the clumps those trees are merged in, each faded as one (TreeFade): one by one, 1068 trees on
        /// wilds-5-recovery, a renderer each more, cost 0.45 ms a frame on the Mac in «Alto» (RendimientoLab, campo).
        /// </summary>
        const int LooseCell = 2;
        /// <summary>BASTION_MARCO_JUNTO=1: all of them merged as before (a lab weighs what they cost: RendimientoLab, E-05).</summary>
        static readonly bool AllMerged = System.Environment.GetEnvironmentVariable("BASTION_MARCO_JUNTO") == "1";

        /// <summary>The outline of `s` under `parent` ("Marco"); null if the map has none.</summary>
        public static GameObject Build(GameState s, PropParts parts, Transform parent)
        {
            if (s.Frame == null || s.Frame.Count == 0) return null;
            List<FrameArt.Dressing> props;
            List<FrameArt.Shape> shapes;
            using (MapLoad.Time("dress")) (props, shapes) = FrameArt.Dress(s);
            var root = new GameObject("Marco");
            root.transform.SetParent(parent, false);
            var at = MapOutline.FrameOf(s);
            // How near what is played a prop stands (tiles, Chebyshev, Loose + 1 if further).
            int Off(FrameArt.Dressing d)
            {
                int tx = Mathf.FloorToInt(d.Matrix.m03), ty = Mathf.FloorToInt(d.Matrix.m23);
                int best = Loose + 1;
                for (int y = Mathf.Max(0, ty - Loose); y <= Mathf.Min(s.Height - 1, ty + Loose); y++)
                    for (int x = Mathf.Max(0, tx - Loose); x <= Mathf.Min(s.Width - 1, tx + Loose); x++)
                        if (at[y * s.Width + x] < 0) best = Mathf.Min(best, Mathf.Max(Mathf.Abs(x - tx), Mathf.Abs(y - ty)));
                return best;
            }
            List<int> off;
            using (MapLoad.Time("near")) off = props.Select(d => TreeFade.Crowns.Contains(d.Id) && !AllMerged ? Off(d) : Loose + 1).ToList();
            var loose = props.Where((d, i) => off[i] <= Loose).ToList();
            // By clumps of LooseCell tiles, each named as its first tree and put where they stand (their middle, as a map's
            // tree has its place: what TreeFade reads), their meshes merged round it.
            int clumps = 0;
            using (MapLoad.Time("clumps"))
                foreach (var clump in loose.GroupBy(d => (Mathf.FloorToInt(d.Matrix.m03 / LooseCell), Mathf.FloorToInt(d.Matrix.m23 / LooseCell))))
                {
                    var ms = clump.Select(d => PropParts.InMetres(d.Matrix)).ToList();
                    var tree = new GameObject(clump.First().Id).transform;
                    tree.SetParent(root.transform, false);
                    var middle = ms.Aggregate(Vector3.zero, (a, m) => a + (Vector3)m.GetColumn(3)) / ms.Count;
                    tree.localPosition = new Vector3(middle.x, ms.Min(m => m.m13), middle.z);
                    parts.Merge(tree, "partes", clump.Select((d, i) => (d.Id, Matrix4x4.Translate(-tree.localPosition) * ms[i], d.Cast)));
                    clumps++;
                }
            Debug.Log($"[map] outline: {loose.Count} trees apart in {clumps} clumps (within {Loose} tiles of play), {props.Count - loose.Count} props merged");
            using (MapLoad.Time("chunks"))
                foreach (var chunk in props.Where((d, i) => off[i] > Loose).GroupBy(d => (Mathf.FloorToInt(d.Matrix.m03 / Chunk), Mathf.FloorToInt(d.Matrix.m23 / Chunk))))
                    parts.Merge(root.transform, $"Trozo {chunk.Key.Item1},{chunk.Key.Item2}", chunk.Select(d => (d.Id, PropParts.InMetres(d.Matrix), d.Cast)));
            using (MapLoad.Time("shapes"))
            {
                Shapes(root.transform, "Formas", shapes.Where(x => x.Form != FrameArt.Water).ToList(), "FrameShape", true);
                Shapes(root.transform, "Agua", shapes.Where(x => x.Form == FrameArt.Water).ToList(), "FrameWater", false);
            }
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
