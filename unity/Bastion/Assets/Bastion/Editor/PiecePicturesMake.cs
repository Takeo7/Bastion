using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The pieces' pictures (Bastion.Client.PiecePictures) rendered into Resources/PiecePictures/&lt;id&gt;.png, 256 px: each piece as the
    /// clearing draws it, at its first level and unworn, alone on a little base of its own (20 tiles; nothing round it).
    /// As 0.10.1 made them:
    ///  - the buildings (lab/renders/iconRenders.ts piece-&lt;id&gt;): with the ground of their plot, framed on their own tiles
    ///    and as high as what stands on them, from (−1, 1.05, 1.25) in its frame, 18° of view, the portraits' studio light
    ///    (ACES at 1.1), cut square round what shows;
    ///  - the rest, defences and traps (strategy/pieceIcons.ts): the model alone, from (−0.85, 1.05, 1), 22°, its own
    ///    studio light (a stronger key, Neutral tone).
    /// BASTION_PIEZAS=a,b only those; BASTION_OUT=dir copies there too (to set beside 0.10.1's).
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.PiecePicturesMake.Run -quit
    /// </summary>
    public static class PiecePicturesMake
    {
        const string Folder = "Assets/Bastion/Client/Resources/" + PiecePictures.Folder;
        const int Side = 256, Shot = 512;
        const float Tile = MapSpace.Tile;

        public static void Run()
        {
            PortraitLab.EnsureTone();
            Directory.CreateDirectory(Folder);
            var only = Environment.GetEnvironmentVariable("BASTION_PIEZAS")?.Split(',');
            string copies = Environment.GetEnvironmentVariable("BASTION_OUT");
            if (copies != null) Directory.CreateDirectory(copies);
            var prefabs = MapLab.Prefabs();
            var materials = MapLab.Materials();
            var ids = Bastion.Engine.Base.Pieces.Keys.Where(id => id != PieceIds.Caravan && (only == null || only.Contains(id))).ToList();
            var made = new List<string>();
            foreach (string id in ids)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // The day's light settings (no haze: Studio turns it off), for what reads them as globals.
                Mood.Show(Mood.Day);
                try
                {
                    var png = Draw(id, prefabs, materials);
                    if (png == null) continue;
                    string path = $"{Folder}/{id}.png";
                    File.WriteAllBytes(path, png);
                    if (copies != null) File.WriteAllBytes(Path.Combine(copies, id + ".png"), png);
                    made.Add(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[piezas] {id}: {e}");
                }
            }
            AssetDatabase.Refresh();
            foreach (string path in made)
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            Debug.Log($"[piezas] {made.Count} of {ids.Count} pictures in {Folder}");
        }

        /// <summary>One piece's picture as a PNG, or null.</summary>
        static byte[] Draw(string id, Func<string, GameObject> prefabs, Func<string, Material> materials)
        {
            var def = Bastion.Engine.Base.Pieces[id];
            bool building = def.Kind == PieceKinds.Building;
            const int size = 20;
            var @base = new BaseState { Width = size, Height = size, Trees = new List<int>(), Felled = new List<int>(), Pieces = new List<BasePiece>(), NextPiece = 1 };
            int w = def.Size.Count > 0 ? def.Size[0] : 1, h = def.Size.Count > 1 ? def.Size[1] : 1;
            var piece = Bastion.Engine.Base.PlacePiece(@base, id, (size - w) / 2, (size - h) / 2, 0);
            var cells = Bastion.Engine.Base.CellsOf(piece);
            var state = Settlement.ClearingState(@base, false);
            var view = new GameObject("Mapa").AddComponent<MapView>();
            view.Build(state, prefabs, materials);
            // The plot, in Unity's metres (tile x east, y south: z = −y).
            int x0 = cells.Min(c => c.X), x1 = cells.Max(c => c.X) + 1, y0 = cells.Min(c => c.Y), y1 = cells.Max(c => c.Y) + 1;
            var plot = new Rect(x0 * Tile, -y1 * Tile, (x1 - x0) * Tile, (y1 - y0) * Tile);
            bool On(Bounds b, float margin) =>
                b.center.x >= plot.xMin - margin && b.center.x <= plot.xMax + margin && b.center.z >= plot.yMin - margin && b.center.z <= plot.yMax + margin;
            // What stands on its tiles, on the studio's layer; the rest (the woods, the ground beyond) off.
            Transform ground = null;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Suelo" || r.transform.name == "Suelo")
                {
                    ground = r.transform;
                    continue;
                }
                bool keep = r.enabled && r.gameObject.activeInHierarchy && On(r.bounds, 0.3f * Tile) && r.bounds.size.x < plot.width + 2 * Tile && r.bounds.size.z < plot.height + 2 * Tile;
                if (keep) r.gameObject.layer = Studio.Layer;
                else r.enabled = false;
            }
            if (ground != null)
            {
                // A building on its plot's ground only (the plane's shader paints it by where it is); the rest on nothing.
                if (building)
                {
                    var quad = new Mesh { name = "Parcela" };
                    float gy = ground.position.y;
                    quad.vertices = new[] { new Vector3(plot.xMin, 0, plot.yMax), new Vector3(plot.xMax, 0, plot.yMax), new Vector3(plot.xMax, 0, plot.yMin), new Vector3(plot.xMin, 0, plot.yMin) }
                        .Select(v => ground.InverseTransformPoint(new Vector3(v.x, gy, v.z))).ToArray();
                    quad.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
                    quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    quad.RecalculateNormals();
                    quad.RecalculateBounds();
                    ground.GetComponent<MeshFilter>().sharedMesh = quad;
                    ground.gameObject.layer = Studio.Layer;
                }
                else ground.GetComponent<Renderer>().enabled = false;
            }
            var kept = view.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.layer == Studio.Layer).ToList();
            if (kept.Count == 0)
            {
                Debug.LogWarning($"[piezas] {id}: nothing drawn on its tiles");
                return null;
            }
            Bounds frame;
            if (building)
            {
                // As high as what stands on its tiles (its roof, a mast).
                float top = Mathf.Min(Mathf.Max(1.5f * Tile, kept.Max(r => r.bounds.max.y)), 8 * Tile);
                frame = new Bounds();
                frame.SetMinMax(new Vector3(plot.xMin, 0, plot.yMin), new Vector3(plot.xMax, top, plot.yMax));
            }
            else
            {
                frame = kept[0].bounds;
                foreach (var r in kept) frame.Encapsulate(r.bounds);
            }
            // 0.10.1's views, its z south made north.
            var back = (building ? new Vector3(-1, 1.05f, -1.25f) : new Vector3(-0.85f, 1.05f, -1)).normalized;
            float fov = building ? 18 : 22;
            var camera = new GameObject("Cámara de las piezas").AddComponent<Camera>();
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400;
            camera.aspect = 1;
            Frame(camera, frame, back, fov, building ? 0.06f : 0.03f);
            var target = new RenderTexture(Shot, Shot, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            if (building)
                Studio.Shoot(camera, target, new[] { new Studio.Lamp(0xffe6c8, 2.6f, new Vector3(-1, 1.4f, -1.5f)), new Studio.Lamp(0xa8c8ff, 1.6f, new Vector3(1.2f, 0.8f, 1.2f)) },
                    Studio.Hex(0xd8e4ff), Studio.Hex(0x4a3a30), 1.4f, Studio.Tone.AcesFilmic, 1.1f);
            else
                Studio.Shoot(camera, target, new[] { new Studio.Lamp(0xfff0dc, 3.2f, new Vector3(-1.2f, 2, -1.4f)), new Studio.Lamp(0xb4ccff, 1.2f, new Vector3(1.5f, 1, 1.2f)) },
                    Studio.Hex(0xe2eaff), Studio.Hex(0x4a4036), 2.1f, Studio.Tone.Neutral, 1);
            var picture = Read(target);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            var output = building ? Filled(picture, Shot, 0.03f) : Resample(picture, Shot, new Rect(0, 0, Shot, Shot), Side);
            var texture = new Texture2D(Side, Side, TextureFormat.RGBA32, false);
            texture.SetPixels(output);
            texture.Apply();
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log($"[piezas] {id}: {(building ? "building on its plot" : "model")}, {kept.Count} renderers");
            return png;
        }

        /// <summary>
        /// The camera looking along −`back` at `box`, framed tight in its own axes with `margin` a side (iconRenders.ts
        /// shoot): its extent across and up, its middle, and back from it as far as the field of view needs, plus its depth.
        /// </summary>
        static void Frame(Camera camera, Bounds box, Vector3 back, float fov, float margin)
        {
            var centre = box.center;
            camera.transform.position = centre + back * 10;
            camera.transform.LookAt(centre, Vector3.up);
            Vector3 right = camera.transform.right, up = camera.transform.up;
            float x0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y0 = float.PositiveInfinity, y1 = float.NegativeInfinity, depth = 0;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) != 0 ? box.max.x : box.min.x, (i & 2) != 0 ? box.max.y : box.min.y, (i & 4) != 0 ? box.max.z : box.min.z) - centre;
                x0 = Mathf.Min(x0, Vector3.Dot(c, right));
                x1 = Mathf.Max(x1, Vector3.Dot(c, right));
                y0 = Mathf.Min(y0, Vector3.Dot(c, up));
                y1 = Mathf.Max(y1, Vector3.Dot(c, up));
                depth = Mathf.Max(depth, Mathf.Abs(Vector3.Dot(c, back)));
            }
            var target = centre + right * ((x0 + x1) / 2) + up * ((y0 + y1) / 2);
            float half = Mathf.Max(x1 - x0, y1 - y0) / 2 / (1 - margin * 2);
            float distance = half / Mathf.Tan(fov / 2 * Mathf.Deg2Rad) + depth;
            camera.transform.position = target + back * distance;
            camera.transform.LookAt(target, Vector3.up);
        }

        static Color[] Read(RenderTexture rt)
        {
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = was;
            var pixels = t.GetPixels();
            UnityEngine.Object.DestroyImmediate(t);
            return pixels;
        }

        /// <summary>The picture cut square round what shows, `margin` a side (iconRenders.ts filled), at the pictures' side.</summary>
        static Color[] Filled(Color[] src, int n, float margin)
        {
            int x0 = n, y0 = n, x1 = -1, y1 = -1;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (src[y * n + x].a < 8 / 255f) continue;
                    x0 = Mathf.Min(x0, x);
                    x1 = Mathf.Max(x1, x);
                    y0 = Mathf.Min(y0, y);
                    y1 = Mathf.Max(y1, y);
                }
            if (x1 < 0) return Resample(src, n, new Rect(0, 0, n, n), Side);
            float side = Mathf.Max(x1 - x0, y1 - y0) / (1 - margin * 2);
            float cx = (x0 + x1 + 1) / 2f, cy = (y0 + y1 + 1) / 2f;
            return Resample(src, n, new Rect(cx - side / 2, cy - side / 2, side, side), Side);
        }

        /// <summary>`crop` of the n×n picture at `side` px, each pixel the mean of 4×4 samples of it, the colour weighted by its alpha (no dark fringes).</summary>
        static Color[] Resample(Color[] src, int n, Rect crop, int side)
        {
            var dst = new Color[side * side];
            const int k = 4;
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < k; sy++)
                        for (int sx = 0; sx < k; sx++)
                        {
                            float u = crop.x + (x + (sx + 0.5f) / k) * crop.width / side - 0.5f;
                            float v = crop.y + (y + (sy + 0.5f) / k) * crop.height / side - 0.5f;
                            var c = Bilinear(src, n, u, v);
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    dst[y * side + x] = a > 1e-5f ? new Color(r / a, g / a, b / a, a / (k * k)) : Color.clear;
                }
            return dst;
        }

        static Color Bilinear(Color[] src, int n, float u, float v)
        {
            int ix = Mathf.FloorToInt(u), iy = Mathf.FloorToInt(v);
            float fx = u - ix, fy = v - iy;
            Color At(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? Color.clear : src[y * n + x];
            // Premultiplied while mixed, straight after.
            Color P(Color c) => new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            var m = Color.Lerp(Color.Lerp(P(At(ix, iy)), P(At(ix + 1, iy)), fx), Color.Lerp(P(At(ix, iy + 1)), P(At(ix + 1, iy + 1)), fx), fy);
            return m.a > 1e-5f ? new Color(m.r / m.a, m.g / m.a, m.b / m.a, m.a) : Color.clear;
        }
    }
}
