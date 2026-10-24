using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// Quads with a colour each, gathered into one mesh (the marks on the map: overlays.ts's InstancedMeshes and Line2s),
    /// for the Bastion/Marks shader: colours in sRGB, and lines whose width is in pixels (Line).
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Vector4> wide = new List<Vector4>();
        readonly List<int> triangles = new List<int>();

        public bool Empty => vertices.Count == 0;

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            wide.Clear();
            triangles.Clear();
        }

        void Vertex(Vector3 p, Color32 color, Vector2 uv, Vector4 w)
        {
            vertices.Add(p);
            colors.Add(color);
            uvs.Add(uv);
            wide.Add(w);
        }

        void Two(int i)
        {
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
            triangles.Add(i);
            triangles.Add(i + 2);
            triangles.Add(i + 3);
        }

        /// <summary>A quad a, b, c, d (in order round it), its texture's corners on them (the marks are seen from both sides).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            Quad(a, b, c, d, color, color, color, color);
        }

        /// <summary>A quad with a colour on each corner (a fade).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            var i = vertices.Count;
            Vertex(a, ca, new Vector2(0, 0), Vector4.zero);
            Vertex(b, cb, new Vector2(1, 0), Vector4.zero);
            Vertex(c, cc, new Vector2(1, 1), Vector4.zero);
            Vertex(d, cd, new Vector2(0, 1), Vector4.zero);
            Two(i);
        }

        /// <summary>A quad seen only from the side `outward` points to (a solid's face, to be lit: Build with normals).</summary>
        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color32 color)
        {
            // Unity's front face turns clockwise to the eye: cross(b − a, c − a) points out of it.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0)
            {
                var t = b;
                b = d;
                d = t;
            }
            Quad(a, b, c, d, color);
        }

        /// <summary>A flat quad lying at height `y`, its corners (x0, z0)–(x1, z1).</summary>
        public void Flat(float x0, float z0, float x1, float z1, float y, Color32 color)
        {
            Quad(new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1), color);
        }

        /// <summary>A line `px` pixels wide on screen from `from` to `to`, whatever the distance (the shader widens it).</summary>
        public void Line(Vector3 from, Vector3 to, float px, Color32 color)
        {
            var dir = to - from;
            if (dir.sqrMagnitude < 1e-8f) return;
            var i = vertices.Count;
            Vertex(from, color, new Vector2(0, 0), new Vector4(dir.x, dir.y, dir.z, -px));
            Vertex(to, color, new Vector2(1, 0), new Vector4(dir.x, dir.y, dir.z, -px));
            Vertex(to, color, new Vector2(1, 1), new Vector4(dir.x, dir.y, dir.z, px));
            Vertex(from, color, new Vector2(0, 1), new Vector4(dir.x, dir.y, dir.z, px));
            Two(i);
        }

        /// <summary>A flat band round `c` from radius r0 to r1, from angle a0 to a1 (radians, from east towards north).</summary>
        public void Arc(Vector3 c, float r0, float r1, float a0, float a1, int segments, Color32 color)
        {
            for (var k = 0; k < segments; k++)
            {
                var t0 = Mathf.Lerp(a0, a1, (float)k / segments);
                var t1 = Mathf.Lerp(a0, a1, (float)(k + 1) / segments);
                var d0 = new Vector3(Mathf.Cos(t0), 0, Mathf.Sin(t0));
                var d1 = new Vector3(Mathf.Cos(t1), 0, Mathf.Sin(t1));
                Quad(c + d0 * r0, c + d1 * r0, c + d1 * r1, c + d0 * r1, color);
            }
        }

        /// <summary>The mesh, made again (the marks change only on a pick, a move or the pointer's tile); `normals` for Faces.</summary>
        public Mesh Build(Mesh mesh, bool normals = false)
        {
            if (mesh == null) mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, wide);
            mesh.SetTriangles(triangles, 0);
            if (normals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // A line's width is added on screen: its bounds a little wider, so it isn't culled at the edge.
            var b = mesh.bounds;
            b.Expand(1f);
            mesh.bounds = b;
            return mesh;
        }

        /// <summary>
        /// A material for the marks (Bastion/Marks): drawn in `queue`; over everything (`overAll`, as the web's lines
        /// and shields: depthTest false) or behind what stands in front; `shade`, blending by the minimum (a dark stroke
        /// that leaves dark ground as it is).
        /// </summary>
        public static Material Material(int queue, bool overAll = false, bool shade = false)
        {
            var m = new Material(Resources.Load<Shader>("Combat/Marks")) { renderQueue = queue };
            m.SetFloat("_ZTest", (float)(overAll ? CompareFunction.Always : CompareFunction.LessEqual));
            m.SetFloat("_BlendOp", (float)(shade ? BlendOp.Min : BlendOp.Add));
            m.SetFloat("_SrcBlend", (float)(shade ? BlendMode.One : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(shade ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            return m;
        }
    }
}
