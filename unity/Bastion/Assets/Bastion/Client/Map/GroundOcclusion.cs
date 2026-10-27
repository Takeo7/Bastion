using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// Contact occlusion baked into a texture over the map (0.10.1's groundAO.ts; UI's): every car, wall and heap sits in
    /// its own soft dark, on every graphics level and at no cost a frame. Once the map is built, and again whenever it
    /// changes, the height of everything solid seen from above is drawn, then for each texel how much of the sky the things
    /// round it hide. The ground reads it with one lookup (_GroundAO, global, so the floors may too: _GroundAOArea says
    /// where it lies, x and z of its corner and its size in metres; r the sky seen from the top of what is there, g that
    /// height, b the sky seen from the ground under it, the ground's own). Trees, poles, wires and canopies are left out: seen
    /// from above they would stand in for the ground under them. Baked just before the next frame is drawn, so the map is
    /// whole by then (its walls and props come after the ground) and it works in the editor's captures too.
    /// </summary>
    [ExecuteAlways]
    public sealed class GroundOcclusion : MonoBehaviour
    {
        /// <summary>Texels per tile of the occlusion (and of the height field it comes from).</summary>
        const int Texels = 6;

        static readonly int AoId = Shader.PropertyToID("_GroundAO");
        static readonly int AreaId = Shader.PropertyToID("_GroundAOArea");
        static readonly int BakeSizeId = Shader.PropertyToID("_BakeSize");
        static readonly string[] Left = { "tree", "pine", "pole", "light", "lamp", "wire", "cable", "canopy", "sign", "flag",
            // The buildings' floors, storeys' slabs, decks and roofs (MapBuilder: Suelos, Plantas, Azoteas, Tejados), as 0.10.1
            // leaves them out (buildings.ts occluders: the walls, raised ground and solid blocks): over a room they stood in
            // for its floor, and the floor had no occlusion of its walls (Arte, 26/10).
            // (A wall's cap is left out by GroundBake: 0.10.1's walls, upright planes, put nothing in the heights but the
            // tops of their doorways and windows. With that, a clinic's walls go in as any others: left out, as on 27/10,
            // its rooms came out lighter than 0.10.1's along the outer walls.)
            "suelos", "plantas", "azoteas", "tejados",
            // The outline round what is played (FrameView: «Marco»), as 0.10.1 has its shapes and props darken no ground
            // (mapView.ts: occlude false); in, it darkened the streets along it.
            "marco" };

        /// <summary>What it is drawn from: the map's objects under it, but the ground's own.</summary>
        public Transform Map;
        public Transform Ground;
        public int Width;
        public int Height;

        /// <summary>A trunk as the occlusion sees it: a six-sided post 0.24 of a tile wide at the foot, 0.16 at the top, 1.3 high.</summary>
        static Mesh trunk;

        RenderTexture heights;
        RenderTexture occlusion;
        Material bake;
        bool dirty = true;

        /// <summary>The map changed (a door, a car blown up): baked again before the next frame.</summary>
        public void MarkDirty() => dirty = true;

        void OnEnable()
        {
            RenderPipelineManager.beginContextRendering += BeforeRendering;
            dirty = true;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= BeforeRendering;
        }

        void OnDestroy()
        {
            if (heights != null) heights.Release();
            if (occlusion != null) occlusion.Release();
            if (Shader.GetGlobalTexture(AoId) == occlusion) Shader.SetGlobalTexture(AoId, Texture2D.whiteTexture);
        }

        void BeforeRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!dirty || Map == null || Width <= 0 || Height <= 0) return;
            dirty = false;
            if (bake == null)
            {
                var source = Resources.Load<Material>("GroundBake");
                if (source == null)
                {
                    Debug.LogError("Ground: Resources/GroundBake.mat missing (Bastion.EditorTools.GroundMake.Run)");
                    return;
                }
                bake = new Material(source);
            }
            int w = Width * Texels, h = Height * Texels;
            if (heights == null || heights.width != w || heights.height != h)
            {
                if (heights != null) heights.Release();
                if (occlusion != null) occlusion.Release();
                heights = new RenderTexture(w, h, 24, RenderTextureFormat.RFloat) { name = "Ground heights", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                occlusion = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf) { name = "Ground occlusion", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                heights.Create();
                occlusion.Create();
            }
            float width = Width * MapSpace.Tile, depth = Height * MapSpace.Tile;
            // From above, looking down: x east across, north up the texture (v = (z + depth) / depth).
            var eye = new Vector3(width / 2, 100, -depth / 2);
            var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(eye, Quaternion.LookRotation(Vector3.down, Vector3.forward), Vector3.one).inverse;
            var projection = Matrix4x4.Ortho(-width / 2, width / 2, -depth / 2, depth / 2, 0.1f, 200);
            var cmd = new CommandBuffer { name = "Ground occlusion" };
            cmd.SetRenderTarget(heights);
            cmd.ClearRenderTarget(true, true, Color.clear);
            cmd.SetViewProjectionMatrices(view, projection);
            foreach (var r in Occluders())
            {
                var filter = r.GetComponent<MeshFilter>();
                int subs = filter != null && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount : 1;
                for (int k = 0; k < subs; k++) cmd.DrawRenderer(r, bake, k, 0);
            }
            // Each tree's trunk, its crown left out (mapView.ts TRUNK_OCCLUDER): without it they stood on the ground with
            // nothing at their foot, as if lifted off it (playtest 21/10 #12).
            if (trunk == null) trunk = TrunkMesh();
            foreach (var t in Map.GetComponentsInChildren<Transform>())
                if (TreeFade.Crowns.Contains(t.name) && (Ground == null || !t.IsChildOf(Ground))) cmd.DrawMesh(trunk, Matrix4x4.Translate(t.position), bake, 0, 0);
            bake.SetVector(BakeSizeId, new Vector4(width, depth, 0, 0));
            cmd.Blit(heights, occlusion, bake, 1);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
            cmd.Release();
            Shader.SetGlobalTexture(AoId, occlusion);
            Shader.SetGlobalVector(AreaId, new Vector4(0, -depth, width, depth));
        }

        /// <summary>
        /// What hides the sky: the map's meshes (walls, floors, cars, props), not the ground's nor what is left out, and only
        /// what casts a shadow, as 0.10.1 has it (mapView.ts: a prop occludes as it casts; the glass, the rugs and what lies
        /// flat on a storey's floor don't).
        /// </summary>
        IEnumerable<MeshRenderer> Occluders()
        {
            foreach (var r in Map.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.shadowCastingMode == ShadowCastingMode.Off || (Ground != null && r.transform.IsChildOf(Ground))) continue;
                var name = string.Join("/", r.GetComponentsInParent<Transform>().Select(t => t.name)).ToLowerInvariant();
                if (Left.Any(name.Contains)) continue;
                yield return r;
            }
        }

        /// <summary>The trunk's post (three.js CylinderGeometry(0.16, 0.24, 1.3, 6) on the ground), in metres; its sides and top.</summary>
        static Mesh TrunkMesh()
        {
            const int sides = 6;
            float bottom = 0.24f * MapSpace.Tile, top = 0.16f * MapSpace.Tile, high = 1.3f * MapSpace.Tile;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int k = 0; k < sides; k++)
            {
                float a = k * Mathf.PI * 2 / sides;
                vertices.Add(new Vector3(Mathf.Sin(a) * bottom, 0, Mathf.Cos(a) * bottom));
                vertices.Add(new Vector3(Mathf.Sin(a) * top, high, Mathf.Cos(a) * top));
            }
            vertices.Add(new Vector3(0, high, 0));
            for (int k = 0; k < sides; k++)
            {
                int a = k * 2, b = (k + 1) % sides * 2;
                triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1, a + 1, sides * 2, b + 1 });
            }
            var mesh = new Mesh { name = "Tronco (oclusión)" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
