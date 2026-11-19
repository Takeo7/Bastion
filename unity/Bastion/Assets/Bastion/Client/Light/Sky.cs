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

        /// <summary>
        /// A5 (Arturo, 19/11, cuadro 74: «quiero otro cielo»), the proposals for his cuadro, a lab's alone (BASTION_CIELO; 0,
        /// the game's). Three skies of their own character, each with its light by day (ProposalLight: the sun, the sky's
        /// light and the haze, over Mood.Day), Synty's clouds one by one for the flat rings and its mountains in the round
        /// for the flat band and the roofs' silhouettes: 1 «Verano», a blue sky with white clouds; 2 «Encapotado», a low
        /// grey lid of big dark clouds, the sun through a break; 3 «Atardecer», orange to violet, its clouds against the
        /// light. By night and under the weather, the game's colours with those clouds, dark. Their models, the map's catalogue's.
        /// </summary>
        public static int Proposal = int.TryParse(System.Environment.GetEnvironmentVariable("BASTION_CIELO"), out int asked) ? asked : 0;

        /// <summary>
        /// The base's own sky and light, the game's warm one as it was (Arturo, 19/11, cuadro 84: «la suya, la cálida de
        /// hoy»): Settlement holds it while it shows the base itself; the stages and the halt between them have theirs.
        /// </summary>
        public static bool Hold;

        /// <summary>The stage's sky now: Proposal, or 0 (the game's as it was) while the base holds its own.</summary>
        static int Stage => Hold ? 0 : Proposal > 0 ? Proposal : Of(Halt ?? MapView.Current?.State?.Mission?.Sky);

        /// <summary>
        /// The sky of the stage just played, at the halt between stages (Settlement sets it from UI's «site/modifier/sky»);
        /// null in a fight, whose own is its mission's (MissionState.Sky: Bastion.Engine.Skies; null, the game's as it was).
        /// </summary>
        public static string Halt;

        static int Of(string sky) => sky == Bastion.Engine.Skies.Clear ? 1 : sky == Bastion.Engine.Skies.Overcast ? 2 : sky == Bastion.Engine.Skies.Dusk ? 3 : 0;
        int stageShown = -1;

        /// <summary>At «Bajo» and on iOS a stage's sky is its dome and its light alone: no clouds, no far town nor sierra (Producto, 19/11).</summary>
        static bool Lite
        {
            get
            {
                if (Application.platform == RuntimePlatform.IPhonePlayer) return true;
                int level = QualitySettings.GetQualityLevel();
                var names = QualitySettings.names;
                return level >= 0 && level < names.Length && names[level] == "Bajo";
            }
        }
        public static System.Func<string, GameObject> LabPrefabs;
        bool solidsMade;
        Mood.Values proposalMood;
        /// <summary>The sky up now: the proposal's by a fair day or by night; the lid (2) under rain, whatever the stage's (cuadro 84's table).</summary>
        int skyNow;

        /// <summary>The day's light under each proposal's sky: Mood.Day with its sun, its sky's light and its haze changed.</summary>
        static Mood.Values ProposalLight(int proposal)
        {
            var v = Mood.Day.Hazier(1);
            if (proposal == 1) (v.haze, v.sun, v.sunIntensity, v.skyLight, v.skyIntensity, v.saturation) = (0xc9dff2, 0xfff3d8, 3.9f, 0xa6c3f0, 1.9f, 1.0f);
            else if (proposal == 2) (v.haze, v.hazeDensity, v.sun, v.sunIntensity, v.skyLight, v.skyIntensity, v.saturation, v.fill) = (0x8f99a6, 0.0036f, 0xdfe6f0, 2.3f, 0x9fb0c8, 2.0f, 0.86f, 0xdfe6ee);
            else if (proposal == 3) (v.haze, v.hazeDensity, v.sun, v.sunIntensity, v.sunFrom, v.skyLight, v.skyIntensity, v.fill, v.fillIntensity) = (0xe3b79a, 0.0036f, 0xffb787, 3.3f, new Vector3(14, 15, -31), 0xb4b0dc, 2.3f, 0xffdcc0, 0.75f);
            return v;
        }

        sealed class Part
        {
            public Transform Transform;
            public Renderer[] Renderers;
            public float At;
            public float Turn;
            public Lands? Land;
            public bool Snow;
            /// <summary>The game's as it was (the flat band, the roofs, the cloud rings): only with no stage's sky (Stage 0). Or a stage's sky's own (its sierra, its far town): only with one.</summary>
            public bool Old, New;
            /// <summary>A proposal's clouds: shown only under that sky (skyNow: 1 summer, 2 the lid, 3 dusk); 0, under any.</summary>
            public int Kind;
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
            // (The base's hold taken or let go: its own day back, or the stage's.)
            if (mood != null && stageShown != Stage)
            {
                if (ReferenceEquals(mood, proposalMood) && Stage == 0) Mood.Show(Mood.Day);
                stageShown = Stage;
                shown = null;
                mood = Mood.Current != null ? Mood.Current.Now : null;
            }
            if (mood == null || ReferenceEquals(mood, shown)) return;
            // (A proposal's own daylight, in a lab: the day's mood swapped for it.)
            if (Stage > 0 && ReferenceEquals(mood, Mood.Day))
            {
                proposalMood = ProposalLight(Stage);
                shown = proposalMood;
                Mood.Show(proposalMood);
                mood = proposalMood;
            }
            else shown = mood;
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
            var band = Add("Montañas", RingMesh(MountainBottom, MountainTop), mountains, MountainsAt, 0.7f);
            band.Old = true;
        }

        /// <summary>The proposals' solids (2 and 3), once the lab's models are there: clouds one by one, mountains in the round.</summary>
        void Solids()
        {
            // (The map's catalogue has their models: MapArt's skyCloud1–3 and skyMountain1–5.)
            if (LabPrefabs == null && MapCatalog.Load()?.PrefabOf() is System.Func<string, GameObject> prefabOf)
                LabPrefabs = id => MapArt.Prop(id) is PropArt art ? prefabOf(MapArt.ModelKey(art)) : null;
            if (solidsMade || Stage < 1 || LabPrefabs == null) return;
            solidsMade = true;
            int made = parts.Count;
            Mesh Whole(string key, out float size)
            {
                size = 1;
                var prefab = LabPrefabs(key);
                var meshes = prefab != null ? prefab.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).ToList() : null;
                if (meshes == null || meshes.Count == 0) return null;
                var root = prefab.transform.worldToLocalMatrix;
                var mesh = new Mesh { name = key, indexFormat = IndexFormat.UInt32 };
                // (Every submesh: a building's glass is its second, and without it it was a frame.)
                mesh.CombineMeshes(meshes.SelectMany(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Select(s => new CombineInstance { mesh = f.sharedMesh, subMeshIndex = s, transform = root * f.transform.localToWorldMatrix })).ToArray(), true, true);
                size = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
                return mesh;
            }
            float R(int n, int salt) => Mathf.Repeat(Mathf.Sin(n * 127.1f + salt * 311.7f) * 43758.5453f, 1);
            // The solids of one kind joined into one mesh round the origin, each `wide` (a share of the radius) at its bearing and height.
            Part lastRing = null;
            void Ring(string name, string[] keys, int count, float low, float high, float narrow, float wide, float squash, Color tint, float mist, int queue, float fade = 0)
            {
                var all = new List<CombineInstance>();
                for (int n = 0; n < count; n++)
                {
                    var mesh = Whole(keys[n % keys.Length], out float size);
                    if (mesh == null) continue;
                    float bearing = (n + R(n, 1) * 0.7f) / count * 360, up = Mathf.Lerp(low, high, R(n, 2)), scale = Mathf.Lerp(narrow, wide, R(n, 3)) / size;
                    var centre = mesh.bounds.center;
                    var at = Quaternion.Euler(0, bearing, 0) * new Vector3(0, up, 1);
                    all.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(at, Quaternion.Euler(0, bearing + R(n, 4) * 360, 0), new Vector3(scale, scale * squash, scale)) * Matrix4x4.Translate(new Vector3(-centre.x, -mesh.bounds.min.y, -centre.z)) });
                }
                if (all.Count == 0) return;
                var joined = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                joined.CombineMeshes(all.ToArray(), true, true);
                if (System.Environment.GetEnvironmentVariable("BASTION_CIELO_LOG") == "1") Debug.Log($"[cielo] {name}: {joined.triangles.Length / 3} triangles");
                var m = MakeMaterial("_CLOUD", null, 2);
                m.SetColor("_Tint", tint);
                m.SetFloat("_Mist", mist);
                m.SetFloat("_Fade", fade);
                m.renderQueue = shader.renderQueue + queue;
                lastRing = Add(name, joined, m, queue < 4 ? MountainsAt : CloudsAt, 0);
            }
            string[] clouds = { "skyCloud1", "skyCloud2", "skyCloud3" };
            int first = parts.Count;
            Ring("Nubes de verano", clouds, 26, 0.12f, 0.6f, 0.3f, 0.6f, 0.9f, Color.white, 0, 4);
            for (int k = first; k < parts.Count; k++) parts[k].Kind = 1;
            first = parts.Count;
            // A lid: many, low all round and on up overhead, with breaks between them.
            Ring("Nubes bajas", clouds, 90, 0.05f, 0.2f, 0.4f, 0.65f, 0.4f, Color.white, 0.2f, 4);
            Ring("Nubes medias", clouds, 80, 0.2f, 0.5f, 0.45f, 0.75f, 0.35f, Color.white, 0.08f, 5);
            Ring("Nubes altas", clouds, 40, 0.6f, 1.3f, 0.5f, 0.8f, 0.35f, Color.white, 0, 6);
            for (int k = first; k < parts.Count; k++) parts[k].Kind = 2;
            first = parts.Count;
            Ring("Nubes del atardecer", clouds, 44, 0.05f, 0.3f, 0.18f, 0.4f, 0.45f, Color.white, 0, 4);
            for (int k = first; k < parts.Count; k++) parts[k].Kind = 3;
            Ring("Sierra", new[] { "skyMountain1", "skyMountain2", "skyMountain3", "skyMountain4", "skyMountain5" }, 14, -0.02f, -0.01f, 0.35f, 0.6f, 0.7f, new Color(0.42f, 0.45f, 0.5f), 0.6f, 1, 0.012f);
            // In a town or by a factory, buildings before the sierra, in the round (Generic's background ones), two rows of
            // them, the far one paler: not the flat silhouettes.
            // (Plain blocks, a box each with a setback on some: Synty's background buildings were a quarter of a million
            // triangles for what is a haze-coloured shape at this distance.)
            void Town(string name, int count, float narrow, float wide, float low, float tall, Color tint, float mist, int queue, Lands land)
            {
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var triangles = new List<int>();
                void Box(Vector3 at, Quaternion turn, Vector3 size)
                {
                    foreach (var (n, u, v) in new[] { (Vector3.right, Vector3.up, Vector3.forward), (Vector3.left, Vector3.forward, Vector3.up), (Vector3.up, Vector3.forward, Vector3.right), (Vector3.forward, Vector3.right, Vector3.up), (Vector3.back, Vector3.up, Vector3.right) })
                    {
                        int start = vertices.Count;
                        foreach (var (a, b) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                        {
                            vertices.Add(at + turn * Vector3.Scale(n + u * a + v * b, size / 2) + Vector3.up * size.y / 2);
                            normals.Add(turn * n);
                        }
                        triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    }
                }
                for (int n = 0; n < count; n++)
                {
                    float bearing = (n + R(n, 11) * 0.8f) / count * 360, across = Mathf.Lerp(narrow, wide, R(n, 12)), high = Mathf.Lerp(low, tall, R(n, 13) * R(n, 13));
                    var turn = Quaternion.Euler(0, bearing, 0);
                    var at = turn * new Vector3(0, -0.003f, 1);
                    Box(at, turn, new Vector3(across, high, across * 0.7f));
                    if (R(n, 14) < 0.35f) Box(at + Vector3.up * high, turn, new Vector3(across * 0.55f, high * 0.3f, across * 0.4f));
                }
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                if (System.Environment.GetEnvironmentVariable("BASTION_CIELO_LOG") == "1") Debug.Log($"[cielo] {name}: {triangles.Count / 3} triangles");
                var m = MakeMaterial("_CLOUD", null, 2);
                m.SetColor("_Tint", tint);
                m.SetFloat("_Mist", mist);
                m.SetFloat("_Fade", 0.009f);
                m.renderQueue = shader.renderQueue + queue;
                Add(name, mesh, m, MountainsAt, 0, land);
            }
            foreach (var land in new[] { Lands.Town, Lands.Industry })
            {
                bool town = land == Lands.Town;
                Town("Ciudad lejana " + land, town ? 240 : 90, 0.012f, 0.03f, 0.008f, town ? 0.05f : 0.02f, new Color(0.5f, 0.5f, 0.55f), 0.68f, 2, land);
                Town("Ciudad " + land, town ? 180 : 60, 0.016f, 0.036f, 0.01f, town ? 0.06f : 0.024f, new Color(0.45f, 0.45f, 0.5f), 0.5f, 3, land);
            }
            for (int k = made; k < parts.Count; k++) parts[k].New = true;
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
            var night = m.night;
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
            skyNow = 0;
            if (Stage > 0)
            {
                bool own = ReferenceEquals(m, proposalMood), weather = !own && !night && !snow && !ReferenceEquals(m, Mood.Day);
                skyNow = weather ? 2 : snow ? 0 : Stage;
                if (own && Stage == 1)
                {
                    Shader.SetGlobalVector("_SkyZenith", Linear(0x2c68bf));
                    Shader.SetGlobalVector("_SkyMiddle", Linear(0x86bdec));
                    (lit, shade) = (Linear(0xffffff), Linear(0xb4c6de));
                }
                else if (own && Stage == 2)
                {
                    Shader.SetGlobalVector("_SkyZenith", Linear(0x4d5661));
                    Shader.SetGlobalVector("_SkyMiddle", Linear(0x7c8794));
                    Shader.SetGlobalVector("_SkySun", Linear(0xe8eef6) * 1.2f);
                    Shader.SetGlobalFloat("_SkyGlow", 1.4f);
                    (lit, shade) = (Linear(0x7b8794), Linear(0x333a44));
                }
                else if (own)
                {
                    Shader.SetGlobalVector("_SkyZenith", Linear(0x4b4a82));
                    Shader.SetGlobalVector("_SkyMiddle", Linear(0xd98a62));
                    Shader.SetGlobalVector("_SkySun", Linear(0xffd9a0) * 1.2f);
                    Shader.SetGlobalFloat("_SkyGlow", 1.8f);
                    // (Against the light: dark, their lit side an ember.)
                    (lit, shade) = (Linear(0xe8935f), Linear(0x3c2f52));
                }
                else if (weather)
                {
                    // (The lid, darker the heavier the weather is: by how thick its haze.)
                    float heavy = Mathf.InverseLerp(0.004f, 0.02f, m.hazeDensity);
                    (lit, shade) = (Vector4.Lerp(Linear(0x7a8088), Linear(0x4a4f57), heavy), Vector4.Lerp(Linear(0x3a3f46), Linear(0x1c1f25), heavy));
                }
                // (By night, hardly there: a little over the sky's own dark.)
                else if (night) (lit, shade) = (Vector4.Lerp(Linear(NightSky[1]), light, 0.04f), Linear(NightSky[0]));
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
                if (!Lite) Solids();
            }
            var far = camera.farClipPlane;
            var at = camera.transform.position;
            foreach (var p in parts)
            {
                var shown = on && !(p.Old && Stage > 0) && !(p.New && (Stage == 0 || Lite)) && !(p.Kind > 0 && Lite) && (p.Land == null || p.Land == Land) && (!p.Snow || snow) && (p.Kind == 0 || p.Kind == skyNow);
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
                Add($"Tejados {land} {i}", RingMesh(-0.01f, top), m, at, 1.3f + i * 2.1f, land).Old = true;
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
                Add("Nubes " + id, mesh, m, CloudsAt, turn, null, snowOnly).Old = true;
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
