using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The radio's voices' clothes dyed and skins changed (portraits.ts VOICES dyes and skin), baked for Bastion.Client.Voices
    /// into Resources/Voices: a copy of the body's mesh in two submeshes, what 0.10.1 dyed and the rest (&lt;speaker&gt;.asset),
    /// the atlas the first wears (&lt;speaker&gt;.png) and, with a skin of its own, the one the rest wears (&lt;speaker&gt;-piel.png).
    /// As 0.10.1 (tools/synty/convert.py write_parts, and synty.ts's dye shader): the torso is what hangs from the spine and
    /// the clavicles, the arms what hangs from the shoulders and elbows, but for the owner's band (the upper arm from 0.18
    /// to 0.5 of the way to the elbow, left as it was when it has no colour); only its cloth takes the dye (not near black
    /// nor white, not the character's skin: the colours of its face), at 0.6, keeping its shading (the colour times its
    /// luminance over 0.14, from 0.35 to 1.6); Synty's skin swatch (255, 204, 174) becomes the skin given.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.VoiceDyesMake.Run -quit
    /// </summary>
    public static class VoiceDyesMake
    {
        const string Folder = "Assets/Bastion/Client/Resources/" + Voices.Folder;
        static readonly string[] Packs = { "Assets/Synty/PolygonOffice/Prefabs/Characters", "Assets/Synty/PolygonCity/Prefabs/Characters", "Assets/Synty/PolygonApocalypse/Prefabs/Characters" };

        /// <summary>convert.py PART: the armour piece of each bone (2 torso, 3 arms, 4 legs), and the band (5).</summary>
        static readonly Dictionary<string, int> Part = new Dictionary<string, int>
        {
            ["Hips"] = 4, ["UpperLeg_L"] = 4, ["UpperLeg_R"] = 4, ["LowerLeg_L"] = 4, ["LowerLeg_R"] = 4,
            ["Spine_01"] = 2, ["Spine_02"] = 2, ["Spine_03"] = 2, ["Clavicle_L"] = 2, ["Clavicle_R"] = 2,
            ["Shoulder_L"] = 3, ["Shoulder_R"] = 3, ["Elbow_L"] = 3, ["Elbow_R"] = 3,
        };
        const int Band = 5;

        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var made = new List<string>();
            foreach (var (speaker, look) in Voices.Looks.Select(kv => (kv.Key, kv.Value)))
            {
                if (look.dye == null && look.skin == null) continue;
                var prefab = Packs.Select(f => AssetDatabase.LoadAssetAtPath<GameObject>($"{f}/{look.prefab}.prefab")).FirstOrDefault(p => p != null);
                var body = prefab?.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.gameObject.activeSelf);
                var atlas = Voices.AtlasOf(body?.sharedMaterial) as Texture2D;
                if (body == null || atlas == null)
                {
                    Debug.LogWarning($"[voces] {speaker}: no body or atlas in {look.prefab}");
                    continue;
                }
                var pixels = Read(atlas);
                int w = atlas.width, h = atlas.height;
                bool hasSkin = ColorUtility.TryParseHtmlString(look.skin ?? "", out var skin);
                var rest = hasSkin ? Skinned(pixels, skin) : null;
                var worn = hasSkin ? rest.ToArray() : pixels.ToArray();
                int dyed = 0;
                if (ColorUtility.TryParseHtmlString(look.dye ?? "", out var dye))
                {
                    var mesh = body.sharedMesh;
                    var parts = Parts(body);
                    var skins = SkinColours(mesh, parts.bones, pixels, w, h);
                    var dyedTris = new List<int>();
                    var otherTris = new List<int>();
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        var tris = mesh.GetTriangles(s);
                        for (int k = 0; k + 2 < tris.Length; k += 3)
                        {
                            bool on = Enumerable.Range(0, 3).All(c => parts.of[tris[k + c]] == 2 || parts.of[tris[k + c]] == 3);
                            (on ? dyedTris : otherTris).AddRange(new[] { tris[k], tris[k + 1], tris[k + 2] });
                        }
                    }
                    var mask = Swatches(Cover(mesh.uv, dyedTris, w, h), pixels, w, h);
                    var d = dye.linear;
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        if (!mask[i] || !Dyeable(pixels[i], skins)) continue;
                        var c = worn[i].linear;
                        float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        var shaded = d * Mathf.Clamp(lum / 0.14f, 0.35f, 1.6f);
                        var mixed = Color.Lerp(c, shaded, 0.6f);
                        mixed.a = c.a;
                        worn[i] = mixed.gamma;
                        dyed++;
                    }
                    // The body again, what is dyed one submesh and the rest the other.
                    var split = Object.Instantiate(mesh);
                    split.name = $"{look.prefab} ({speaker})";
                    split.subMeshCount = 2;
                    split.SetTriangles(dyedTris, 0);
                    split.SetTriangles(otherTris, 1);
                    string meshPath = $"{Folder}/{speaker}.asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(split, meshPath);
                    Debug.Log($"[voces] {speaker}: {dyedTris.Count / 3} triangles dyed of {(dyedTris.Count + otherTris.Count) / 3} ({parts.band} corners of the band left), skins {string.Join(" ", skins.Select(k => "#" + ColorUtility.ToHtmlStringRGB(k)))}");
                }
                made.Add(Write($"{Folder}/{speaker}.png", worn, w, h));
                if (rest != null && look.dye != null) made.Add(Write($"{Folder}/{speaker}-piel.png", rest, w, h));
                Debug.Log($"[voces] {speaker}: {look.prefab}, {dyed} texels dyed {look.dye}{(hasSkin ? ", skin " + look.skin : "")}");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            foreach (string path in made)
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = true;
                    // A bust is 160 px: its atlas at 1024, compressed (4096 uncompressed was 85 MB a voice).
                    importer.maxTextureSize = 1024;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.SaveAndReimport();
                }
            Debug.Log($"[voces] {made.Count} atlases in {Folder}");
        }

        static string Write(string path, Color[] pixels, int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return path;
        }

        /// <summary>The atlas with Synty's skin swatch made `skin` (synty.ts: by its distance, 0.05 to 0.14 away).</summary>
        static Color[] Skinned(Color[] pixels, Color skin)
        {
            var s = skin.linear;
            var out_ = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i].linear;
                float k = 1 - Smooth(0.05f, 0.14f, Distance(c, SkinSwatch));
                if (k <= 0)
                {
                    out_[i] = pixels[i];
                    continue;
                }
                var mixed = Color.Lerp(c, s, k);
                mixed.a = c.a;
                out_[i] = mixed.gamma;
            }
            return out_;
        }

        /// <summary>Each vertex's piece (by its main bone; the band where the upper arm has it), and the bones' names.</summary>
        static (int[] of, string[] bones, int band) Parts(SkinnedMeshRenderer body)
        {
            var mesh = body.sharedMesh;
            var names = body.bones.Select(b => b != null ? b.name : "").ToArray();
            var weights = mesh.boneWeights;
            var vertices = mesh.vertices;
            var binds = mesh.bindposes;
            Vector3 Joint(string name)
            {
                int k = System.Array.IndexOf(names, name);
                return k >= 0 && k < binds.Length ? binds[k].inverse.MultiplyPoint3x4(Vector3.zero) : Vector3.zero;
            }
            var of = new int[vertices.Length];
            int band = 0;
            for (int v = 0; v < vertices.Length; v++)
            {
                string bone = v < weights.Length && weights[v].boneIndex0 < names.Length ? names[weights[v].boneIndex0] : "";
                of[v] = Part.TryGetValue(bone, out int p) ? p : 0;
                if (bone == "Shoulder_L" || bone == "Shoulder_R")
                {
                    // Measured to the elbow joint, as convert.py does.
                    Vector3 a = Joint(bone), z = Joint(bone.Replace("Shoulder", "Elbow"));
                    float t = Vector3.Dot(vertices[v] - a, z - a) / Mathf.Max(1e-6f, (z - a).sqrMagnitude);
                    if (t > 0.18f && t < 0.5f)
                    {
                        of[v] = Band;
                        band++;
                    }
                }
            }
            return (of, names, band);
        }

        /// <summary>convert.py skin_colours: the colours covering a good share (12 %) of the head and neck, up to three (sRGB, in 24ths).</summary>
        static List<Color> SkinColours(Mesh mesh, string[] bones, Color[] pixels, int w, int h)
        {
            var weights = mesh.boneWeights;
            var uv = mesh.uv;
            var count = new Dictionary<Vector3Int, int>();
            int total = 0;
            var tris = mesh.triangles;
            foreach (int v in tris)
            {
                if (v >= weights.Length) continue;
                string bone = weights[v].boneIndex0 < bones.Length ? bones[weights[v].boneIndex0] : "";
                if (bone != "Head" && bone != "Neck") continue;
                var c = pixels[Mathf.Min(h - 1, Mathf.FloorToInt(Mathf.Clamp01(uv[v].y) * h)) * w + Mathf.Min(w - 1, Mathf.FloorToInt(Mathf.Clamp01(uv[v].x) * w))];
                var key = new Vector3Int(Mathf.RoundToInt(c.r * 24), Mathf.RoundToInt(c.g * 24), Mathf.RoundToInt(c.b * 24));
                count[key] = count.TryGetValue(key, out int n) ? n + 1 : 1;
                total++;
            }
            return count.OrderByDescending(kv => kv.Value).Take(3).Where(kv => kv.Value >= total * 0.12f).Select(kv => new Color(kv.Key.x / 24f, kv.Key.y / 24f, kv.Key.z / 24f)).ToList();
        }

        /// <summary>convert.py dyeable: not near black (boots, belts, hair), not near white (eyes, teeth), not the character's skin.</summary>
        static bool Dyeable(Color rgb, List<Color> skins)
        {
            Color.RGBToHSV(rgb, out _, out float s, out float v);
            if (v < 0.16f || (v > 0.93f && s < 0.08f)) return false;
            return skins.All(k => (rgb.r - k.r) * (rgb.r - k.r) + (rgb.g - k.g) * (rgb.g - k.g) + (rgb.b - k.b) * (rgb.b - k.b) > 0.0045f);
        }

        /// <summary>Synty's skin swatch, linear (synty.ts: vec3(1.0, 0.604, 0.423)).</summary>
        static readonly Color SkinSwatch = new Color(1, 0.604f, 0.423f);

        /// <summary>GLSL's smoothstep (Mathf.SmoothStep is another thing: an interpolation).</summary>
        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        static float Distance(Color a, Color b) => Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        /// <summary>
        /// The marked texels grown to the whole of each swatch they are in (the texels next to them of the same colour):
        /// the atlas's swatches are blocks of one colour, which its smaller mips blend whole.
        /// </summary>
        static bool[] Swatches(bool[] marked, Color[] pixels, int w, int h)
        {
            var grown = (bool[])marked.Clone();
            var queue = new Queue<int>();
            for (int i = 0; i < marked.Length; i++) if (marked[i]) queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (grown[j] || Distance(pixels[j], pixels[i]) > 0.02f) continue;
                    grown[j] = true;
                    queue.Enqueue(j);
                }
            }
            return grown;
        }

        /// <summary>The atlas's texels (sRGB, as stored), read through a render texture (Synty's atlases aren't readable).</summary>
        static Color[] Read(Texture2D atlas)
        {
            var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(atlas, rt);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
            copy.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            var pixels = copy.GetPixels();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        /// <summary>The texels `tris` cover on the atlas (a texel's breadth round them, and their corners and edges if they have no area).</summary>
        static bool[] Cover(Vector2[] uv, List<int> tris, int w, int h)
        {
            var mask = new bool[w * h];
            void Mark(float px, float py)
            {
                int x = Mathf.Min(w - 1, Mathf.FloorToInt(px)), y = Mathf.Min(h - 1, Mathf.FloorToInt(py));
                if (x >= 0 && y >= 0) mask[y * w + x] = true;
            }
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                var a = new Vector2(Mathf.Clamp01(uv[tris[t]].x) * w, Mathf.Clamp01(uv[tris[t]].y) * h);
                var b = new Vector2(Mathf.Clamp01(uv[tris[t + 1]].x) * w, Mathf.Clamp01(uv[tris[t + 1]].y) * h);
                var c = new Vector2(Mathf.Clamp01(uv[tris[t + 2]].x) * w, Mathf.Clamp01(uv[tris[t + 2]].y) * h);
                foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                {
                    int steps = Mathf.CeilToInt((q - p).magnitude) + 1;
                    for (int k = 0; k <= steps; k++)
                    {
                        var m = Vector2.Lerp(p, q, k / (float)steps);
                        Mark(m.x, m.y);
                    }
                }
                float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (Mathf.Abs(area) < 1) continue;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)) - 1), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)) - 1), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)) + 1);
                float sign = Mathf.Sign(area);
                float Edge(Vector2 p, Vector2 q, Vector2 r) => sign * ((q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x)) / Mathf.Max((q - p).magnitude, 1e-4f);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var r = new Vector2(x + 0.5f, y + 0.5f);
                        if (Edge(a, b, r) >= -1 && Edge(b, c, r) >= -1 && Edge(c, a, r) >= -1) mask[y * w + x] = true;
                    }
            }
            return mask;
        }
    }
}
