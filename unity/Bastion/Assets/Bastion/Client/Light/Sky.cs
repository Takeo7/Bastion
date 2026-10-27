using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client
{
    /// <summary>
    /// The sky round every scene (sky.ts; UI's, Arte's review of 27/10: Unity showed the haze's colour alone): a dome shaded
    /// from the haze at the horizon to the zenith, the sun's glow on it; on the horizon the mountains round the valley (snow
    /// on them in winter) and, in a town or by a factory, roofs before them; clouds in rings above. Each part is centred on
    /// the camera as it draws, at its share of the far plane, so none of it ever comes nearer or moves past. It follows the
    /// mood (Mood.Current.Now, read each frame): none with no mood up (a lab of the interface alone). The land is the battle's
    /// (Land, set as its ground is built) or the base's, the country.
    /// </summary>
    public sealed class Sky : MonoBehaviour
    {
        /// <summary>What stands on the horizon: a town's roofs before the mountains, a factory's lower ones, or the mountains alone.</summary>
        public enum Lands { Country, Town, Industry }

        /// <summary>The land on the horizon now (GroundView sets the battle's; the base's is the country).</summary>
        public static Lands Land = Lands.Country;

        /// <summary>Cameras it stays out of (the valley's map); a camera that doesn't draw the Default layer is left out anyway.</summary>
        public static readonly HashSet<Camera> Without = new HashSet<Camera>();

        /// <summary>Each part's distance, a share of the camera's far plane: the dome furthest, the clouds nearest.</summary>
        const float DomeAt = 0.9f, MountainsAt = 0.8f, RoofsFar = 0.74f, RoofsNear = 0.7f, CloudsAt = 0.62f;

        /// <summary>The mountains' band (shares of its distance, from below the horizon), and how many times it goes round.</summary>
        const float MountainBottom = -0.035f, MountainTop = 0.15f, MountainRepeat = 3;

        /// <summary>The roofs' rings for each land: the skyline's strip (0 the darkest of four), its top, its distance, how often it goes round.</summary>
        static readonly Dictionary<Lands, (int strip, float top, float at, float repeat)[]> RoofRings = new Dictionary<Lands, (int, float, float, float)[]>
        {
            [Lands.Town] = new[] { (2, 0.1f, RoofsFar, 4f), (1, 0.075f, RoofsNear, 6f) },
            [Lands.Industry] = new[] { (3, 0.06f, RoofsNear, 5f) },
            [Lands.Country] = new (int, float, float, float)[0],
        };

        /// <summary>The cloud rings (MapArt ids): by any weather, and a thinner, higher one in snow when the sky is grey.</summary>
        static readonly (string id, bool snow, float lift, float turn)[] CloudRings = { ("cloudRing", false, 0, 0), ("cloudRingThin", true, 0.08f, 1.9f) };

        /// <summary>The sky's stops by mood (renderer.ts MOODS sky: the zenith, then the middle).</summary>
        static readonly int[] DaySky = { 0x8f97b4, 0xc8c0cc }, NightSky = { 0x081419, 0x0f2328 }, SnowSky = { 0x9eafc8, 0xc7d6e3 };

        static Sky current;

        sealed class Part
        {
            public Transform Transform;
            public Renderer[] Renderers;
            public float At;
            public float Turn;
            public Lands? Land;
            public bool Snow;
        }

        readonly List<Part> parts = new List<Part>();
        readonly HashSet<Lands> roofsMade = new HashSet<Lands>();
        readonly HashSet<string> cloudsMade = new HashSet<string>();
        Shader shader;
        Mood.Values shown;
        bool snow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Make()
        {
            if (current != null) return;
            var holder = new GameObject("Cielo");
            DontDestroyOnLoad(holder);
            current = holder.AddComponent<Sky>();
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += Follow;

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Follow;

        void LateUpdate()
        {
            var mood = Mood.Current != null ? Mood.Current.Now : null;
            if (mood == null || ReferenceEquals(mood, shown)) return;
            shown = mood;
            if (parts.Count == 0) Build();
            Colours(mood);
        }

        /// <summary>The dome and the mountains (the roofs and the clouds come as they are first needed).</summary>
        void Build()
        {
            shader = Resources.Load<Shader>("Sky");
            if (shader == null)
            {
                Debug.LogError("Sky: Resources/Sky.shader missing");
                enabled = false;
                return;
            }
            var dome = MakeMaterial("_DOME", null, 0);
            Add("Cúpula", SphereMesh(32, 16), dome, DomeAt, 0);
            var mountains = MakeMaterial("_BAND", Resources.Load<Texture2D>("Sky/mountains"), 0);
            mountains.SetVector("_Rows", new Vector4(0, 1, 0, 0));
            mountains.SetFloat("_Repeat", MountainRepeat);
            mountains.SetFloat("_Air", 0.3f);
            mountains.SetFloat("_Foot", 0.55f);
            Add("Montañas", RingMesh(MountainBottom, MountainTop), mountains, MountainsAt, 0.7f);
        }

        Material MakeMaterial(string keyword, Texture texture, int cull)
        {
            var m = new Material(shader) { name = "Cielo " + keyword.Trim('_').ToLowerInvariant() };
            m.EnableKeyword(keyword);
            if (texture != null)
            {
                texture.wrapModeU = TextureWrapMode.Repeat;
                m.SetTexture("_MainTex", texture);
            }
            m.SetFloat("_Cull", cull);
            return m;
        }

        Part Add(string name, Mesh mesh, Material material, float at, float turn, Lands? land = null, bool snowOnly = false)
        {
            var o = new GameObject(name);
            o.transform.SetParent(transform, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var part = new Part { Transform = o.transform, Renderers = new Renderer[] { r }, At = at, Turn = turn, Land = land, Snow = snowOnly };
            parts.Add(part);
            return part;
        }

        /// <summary>The mood's colours (sky.ts setMood), as globals the parts read, in linear.</summary>
        void Colours(Mood.Values m)
        {
            var night = ReferenceEquals(m, Mood.Night);
            snow = ReferenceEquals(m, Mood.SnowDay);
            var stops = night ? NightSky : snow ? SnowSky : DaySky;
            var haze = Linear(m.haze);
            var sun = Linear(m.sun);
            var light = Linear(m.skyLight);
            Shader.SetGlobalVector("_SkyHorizon", haze);
            Shader.SetGlobalVector("_SkyMiddle", Vector4.Lerp(Linear(stops[1]), haze, 0.25f));
            Shader.SetGlobalVector("_SkyZenith", Linear(stops[0]));
            // From the map's middle to the sun, in 0.10.1's frame (z south): Unity's north.
            Shader.SetGlobalVector("_SkySunDir", new Vector3(m.sunFrom.x, m.sunFrom.y, -m.sunFrom.z).normalized);
            Shader.SetGlobalVector("_SkySun", sun * (night ? 0.25f : snow ? 0.35f : 0.8f));
            Shader.SetGlobalFloat("_SkyGlow", night ? 0.3f : snow ? 0.4f : 1);
            // The land as its light has it: lit by the sky and the sun by day, dark and blue by night.
            Shader.SetGlobalVector("_SkyLand", Vector4.Lerp(light, sun, 0.35f) * (night ? 0.35f : 1.05f));
            Shader.SetGlobalFloat("_SkySnow", snow ? 0.75f : 0);
            var lit = Vector4.Lerp(Vector4.one, sun, 0.25f);
            var shade = Vector4.Lerp(light, haze, 0.5f);
            if (night)
            {
                lit = light * 0.45f;
                shade = haze * 0.7f;
            }
            else if (snow)
            {
                lit = Linear(0xdfe5ec);
                shade = Linear(0x9aa6b4);
            }
            Shader.SetGlobalVector("_SkyCloudLit", lit);
            Shader.SetGlobalVector("_SkyCloudShade", shade);
        }

        static Vector4 Linear(int hex)
        {
            var c = new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f).linear;
            return new Vector4(c.r, c.g, c.b, 1);
        }

        /// <summary>Each part on the camera about to draw, at its distance; the roofs of the land now; none for a camera it stays out of.</summary>
        void Follow(ScriptableRenderContext context, Camera camera)
        {
            if (parts.Count == 0) return;
            // Not for a flat view (the valley's map, from above) nor one that leaves the world out (the busts' studio).
            var on = Mood.Current != null && !camera.orthographic && (camera.cullingMask & 1) != 0 && !Without.Contains(camera) && camera.cameraType != CameraType.Preview;
            if (on)
            {
                Roofs(Land);
                Clouds();
            }
            var far = camera.farClipPlane;
            var at = camera.transform.position;
            foreach (var p in parts)
            {
                var shown = on && (p.Land == null || p.Land == Land) && (!p.Snow || snow);
                foreach (var r in p.Renderers) r.enabled = shown;
                if (!shown) continue;
                p.Transform.SetPositionAndRotation(at, Quaternion.Euler(0, p.Turn * Mathf.Rad2Deg, 0));
                p.Transform.localScale = Vector3.one * (far * p.At);
            }
        }

        /// <summary>The roofs before the mountains for `land`, made once.</summary>
        void Roofs(Lands land)
        {
            if (!roofsMade.Add(land)) return;
            var skyline = Resources.Load<Texture2D>("Sky/skyline");
            var rings = RoofRings[land];
            for (int i = 0; i < rings.Length; i++)
            {
                var (strip, top, at, repeat) = rings[i];
                var m = MakeMaterial("_BAND", skyline, 0);
                // The skyline's four strips, darkest at the top: this one's rows (v from the bottom), inset a little so the next doesn't bleed in.
                m.SetVector("_Rows", new Vector4(1 - (strip + 1) / 4f + 0.004f, 1 - strip / 4f - 0.004f, 0, 0));
                m.SetFloat("_Repeat", repeat);
                m.SetFloat("_Air", 0.22f - i * 0.1f);
                m.SetFloat("_Foot", 0.45f);
                m.renderQueue = shader.renderQueue + 1 + i;
                Add($"Tejados {land} {i}", RingMesh(-0.01f, top), m, at, 1.3f + i * 2.1f, land);
            }
        }

        /// <summary>The cloud rings, once the map's catalogue has their models (MapArt's cloudRing and cloudRingThin).</summary>
        void Clouds()
        {
            if (cloudsMade.Count == CloudRings.Length) return;
            var catalog = MapCatalog.Load();
            var prefabOf = catalog?.PrefabOf();
            foreach (var (id, snowOnly, lift, turn) in CloudRings)
            {
                if (cloudsMade.Contains(id)) continue;
                var art = MapArt.Prop(id);
                var prefab = art != null && prefabOf != null ? prefabOf(MapArt.ModelKey(art)) : null;
                cloudsMade.Add(id);
                if (prefab == null) continue;
                // The ring made one of radius 1 round the origin, its height as it is (a share of that radius).
                var meshes = prefab.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).ToList();
                if (meshes.Count == 0) continue;
                var root = prefab.transform.worldToLocalMatrix;
                var combine = meshes.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = root * f.transform.localToWorldMatrix }).ToArray();
                var mesh = new Mesh { name = "Nubes " + id, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(combine, true, true);
                var box = mesh.bounds;
                var radius = Mathf.Max(box.size.x, box.size.z) / 2;
                if (radius <= 0) radius = 1;
                var vertices = mesh.vertices;
                for (int k = 0; k < vertices.Length; k++)
                    vertices[k] = new Vector3((vertices[k].x - box.center.x) / radius, vertices[k].y / radius + lift, (vertices[k].z - box.center.z) / radius);
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                var m = MakeMaterial("_CLOUD", null, 2);
                m.renderQueue = shader.renderQueue + 4;
                Add("Nubes " + id, mesh, m, CloudsAt, turn, null, snowOnly);
            }
        }

        /// <summary>A sphere of radius 1 (seen from inside), its vertices its directions.</summary>
        static Mesh SphereMesh(int around, int up)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int j = 0; j <= up; j++)
            {
                var v = Mathf.PI * j / up;
                for (int i = 0; i <= around; i++)
                {
                    var u = 2 * Mathf.PI * i / around;
                    vertices.Add(new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u)));
                }
            }
            for (int j = 0; j < up; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = j * (around + 1) + i, b = a + around + 1;
                    triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            var mesh = new Mesh { name = "Cúpula del cielo" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>An open ring round the origin, radius 1, from `bottom` to `top` (shares of the radius), u round it and v up it.</summary>
        static Mesh RingMesh(float bottom, float top)
        {
            const int around = 128;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                var u = 2 * Mathf.PI * i / around;
                var d = new Vector3(Mathf.Sin(u), 0, Mathf.Cos(u));
                vertices.Add(d + Vector3.up * bottom);
                vertices.Add(d + Vector3.up * top);
                uvs.Add(new Vector2((float)i / around, 0));
                uvs.Add(new Vector2((float)i / around, 1));
                normals.Add(d);
                normals.Add(d);
            }
            for (int i = 0; i < around; i++)
            {
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            }
            var mesh = new Mesh { name = "Anillo del cielo" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
