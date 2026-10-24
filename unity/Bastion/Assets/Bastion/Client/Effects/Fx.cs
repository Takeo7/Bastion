using System;
using System.Collections.Generic;
using System.Threading;
using Bastion.Client.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Effects
{
    /// <summary>
    /// The battle's effects, as 0.10.1's fx.ts: shots, blows, blasts, smoke and dust, drawn as Synty's Particle FX draws
    /// its own (faceted puffs and chunks, flat sprites, soft additive glows). Those UI's event player waits for (a
    /// bullet's flight, a blast) return an Awaitable that ends as they land; the rest are fired and left. Sizes,
    /// speeds and times are 0.10.1's, in tiles (T metres each), so the pace is the same.
    /// </summary>
    public sealed class Fx : MonoBehaviour, IShotStep
    {
        public static Fx Current { get; private set; }

        const float T = MapSpace.Tile;
        /// <summary>How many lights the effects have, always there, dark while unused (fx.ts LAMPS).</summary>
        const int Lamps = 2;
        /// <summary>three.js's light intensities (physical, decay 2) to URP's.</summary>
        const float LampScale = 0.25f;

        static readonly Color[] GoreColours = { Hex(0x5a1410), Hex(0x7a1e14), Hex(0x3e0c08) };
        static readonly Color[] Blood = { Hex(0x6a0c08), Hex(0x8a120c), Hex(0x4a0805) };
        /// <summary>The Chillón's shriek: a pale, dusty white, not a colour of magic.</summary>
        static readonly Color Shriek = Hex(0xf0e4d4);

        sealed class Particle
        {
            public Transform obj;
            public Renderer renderer;
            public Texture texture;
            public Color colour;
            /// <summary>The colour it turns to over its life (smoke clearing to grey); null, the same.</summary>
            public Color? colourTo;
            /// <summary>Faded by its colour's alpha; otherwise (a lit shape) it pops in and shrinks away.</summary>
            public bool fades;
            /// <summary>Turned to the camera every frame (a sprite), rolled this much.</summary>
            public bool sprite;
            public float roll;
            public Vector3 velocity;
            public Vector3 spin;
            public float gravity;
            public float drag;
            public float age;
            public float life;
            public float grow;
            public Vector3 scale;
            public float opacity;
            /// <summary>Where it bounces (the floor of its storey).</summary>
            public float floor;
        }

        sealed class Tween
        {
            public float seconds;
            public float age;
            public Action<float> step;
            public AwaitableCompletionSource done;
        }

        sealed class Lamp
        {
            public UnityEngine.Light light;
            public int use;
        }

        readonly List<Particle> particles = new List<Particle>();
        readonly List<Tween> tweens = new List<Tween>();
        readonly List<(float at, Action what)> later = new List<(float, Action)>();
        readonly Lamp[] lamps = new Lamp[Lamps];
        float clock;
        System.Random rng = new System.Random(1);
        MaterialPropertyBlock block;

        FxArt art;
        Material added;
        Material blended;
        Material decals;
        Material hotMaterial;
        Material solid;
        Texture2D glow;
        Mesh quad;
        Mesh cube;
        Mesh sphere;
        Mesh cylinder;
        Mesh blast;

        void Awake() => Current = this;

        void OnEnable() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        /// <summary>Captures in edit mode step it by hand (Shots); in play mode it steps itself.</summary>
        public void ShotStep(float dt)
        {
            if (!Application.isPlaying) Step(dt);
        }

        /// <summary>The effects drawn again the same each time (a strip, a test).</summary>
        public void Seed(int seed) => rng = new System.Random(seed);

        // ------------------------------------------------------------ what it is drawn with

        void Load()
        {
            if (quad != null) return;
            block = new MaterialPropertyBlock();
            art = Resources.Load<FxArt>("FxArt");
            quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            blast = Icosahedron();
            var unlit = art != null && art.unlit != null ? art.unlit : Shader.Find("Bastion/FxUnlit");
            var lit = art != null && art.lit != null ? art.lit : Shader.Find("Universal Render Pipeline/Lit");
            added = Unlit(unlit, BlendMode.SrcAlpha, BlendMode.One, false, 3100);
            blended = Unlit(unlit, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false, 3000);
            decals = Unlit(unlit, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false, 2950);
            decals.SetFloat("_Offset", -2);
            hotMaterial = Unlit(unlit, BlendMode.One, BlendMode.Zero, true, 2000);
            solid = new Material(lit) { name = "Fx solid", enableInstancing = true };
            solid.SetFloat("_Smoothness", 0.05f);
            glow = Glow();
            for (int k = 0; k < Lamps; k++)
            {
                var light = new GameObject("Luz de efecto").AddComponent<UnityEngine.Light>();
                light.transform.SetParent(transform, false);
                light.type = LightType.Point;
                light.intensity = 0;
                light.shadows = LightShadows.None;
                lamps[k] = new Lamp { light = light };
            }
        }

        static Material Unlit(Shader shader, BlendMode source, BlendMode destination, bool depth, int queue)
        {
            var m = new Material(shader) { name = "Fx " + destination, renderQueue = queue };
            m.SetFloat("_SrcBlend", (float)source);
            m.SetFloat("_DstBlend", (float)destination);
            m.SetFloat("_ZWrite", depth ? 1 : 0);
            m.SetFloat("_Cull", (float)CullMode.Off);
            return m;
        }

        /// <summary>A blast's fireball, faceted like Synty's (three.js's IcosahedronGeometry(1, 1)): 80 faces, 1 across.</summary>
        static Mesh Icosahedron()
        {
            float g = (1 + Mathf.Sqrt(5)) / 2;
            var corners = new List<Vector3>
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // Each face in four, its corners out on the sphere; every triangle its own corners (flat, faceted).
            var vertices = new List<Vector3>();
            Vector3 Out(Vector3 v) => v.normalized * 0.5f;
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = corners[faces[f]], b = corners[faces[f + 1]], c = corners[faces[f + 2]];
                Vector3 ab = (a + b) / 2, bc = (b + c) / 2, ca = (c + a) / 2;
                foreach (var v in new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca }) vertices.Add(Out(v));
            }
            var mesh = new Mesh { name = "Fx blast" };
            mesh.SetVertices(vertices);
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetTriangles(indices, 0);
            mesh.SetUVs(0, vertices.ConvertAll(v => new Vector2(0.5f, 0.5f)));
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The soft radial glow of flashes, sparks and tracers (fx.ts glowTexture).</summary>
        static Texture2D Glow()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Fx glow", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = Mathf.Min(1, new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude / (n / 2f));
                float a = r < 0.35f ? Mathf.Lerp(1, 0.6f, r / 0.35f) : Mathf.Lerp(0.6f, 0, (r - 0.35f) / 0.65f);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // Both load first: they are called for the arguments of what draws, before it loads.
        Mesh Shape(Func<FxArt, Mesh> pick)
        {
            Load();
            return art != null && pick(art) is Mesh m && m != null ? m : null;
        }

        Texture TextureOf(Func<FxArt, Texture2D> pick)
        {
            Load();
            return art != null && pick(art) is Texture2D t && t != null ? t : glow;
        }

        /// <summary>
        /// A thing drawn with `mesh`: fitted into a unit cube round its middle when `fit` (Synty's shapes come at
        /// their own size and pivot), as it is otherwise.
        /// </summary>
        Transform Make(string name, Mesh mesh, Material material, bool fit, bool shadows, out Renderer renderer)
        {
            var obj = new GameObject(name).transform;
            obj.SetParent(transform, false);
            var holder = obj;
            if (fit)
            {
                var b = mesh.bounds;
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z, 1e-4f);
                holder = new GameObject("forma").transform;
                holder.SetParent(obj, false);
                holder.localScale = Vector3.one / size;
                holder.localPosition = -b.center / size;
            }
            holder.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = holder.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = material == solid;
            renderer = r;
            return obj;
        }

        void Paint(Renderer r, Texture texture, Color colour)
        {
            block.Clear();
            if (texture != null) block.SetTexture("_BaseMap", texture);
            block.SetColor("_BaseColor", colour);
            r.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------ the particles

        /// <summary>A glow sprite (light): added, faded (fx.ts glowSprite).</summary>
        Particle GlowSprite(Vector3 at, Color colour, float size, float opacity = 1)
        {
            Load();
            return Sprite(at, glow, colour, size, opacity, true);
        }

        /// <summary>One of Particle FX's sprites, tinted; `additive` for light (fx.ts sprite).</summary>
        Particle Sprite(Vector3 at, Texture texture, Color colour, float size, float opacity = 1, bool additive = false)
        {
            Load();
            var obj = Make("brillo", quad, additive ? added : blended, false, false, out var r);
            obj.position = at;
            return new Particle { obj = obj, renderer = r, texture = texture, colour = colour, fades = true, sprite = true, roll = Rand(0, 360), scale = Vector3.one * size * T, opacity = opacity };
        }

        /// <summary>A lit faceted shape (a puff, a chunk, a chip): shared colour, it pops in and shrinks away.</summary>
        Particle Solid(Vector3 at, Mesh mesh, Color colour, float size)
        {
            Load();
            var obj = Make("pieza", mesh ?? cube, solid, mesh != null, true, out var r);
            obj.position = at;
            obj.rotation = Quaternion.Euler(Rand(0, 360), Rand(0, 360), Rand(0, 360));
            return new Particle { obj = obj, renderer = r, colour = colour, scale = Vector3.one * size * T, opacity = 1 };
        }

        /// <summary>Into the list: its size at birth set (it would otherwise show at full size until the next step).</summary>
        void Spawn(Particle p, float life, float grow = 0, float gravity = 0, float drag = 0, Vector3 velocity = default, Vector3 spin = default)
        {
            p.life = life;
            p.grow = grow;
            p.gravity = gravity * T;
            p.drag = drag;
            p.velocity = velocity * T;
            p.spin = spin * Mathf.Rad2Deg;
            p.floor = Mathf.Floor(p.obj.position.y / MapSpace.Storey + 0.05f) * MapSpace.Storey + 0.06f * T;
            p.obj.localScale = p.scale * (p.fades ? 1 : 0.3f);
            if (p.fades) Paint(p.renderer, p.texture, WithAlpha(p.colour, p.opacity));
            else Paint(p.renderer, null, p.colour);
            Face(p);
            particles.Add(p);
        }

        void Face(Particle p)
        {
            if (!p.sprite) return;
            var camera = Camera.main;
            if (camera != null) p.obj.rotation = camera.transform.rotation * Quaternion.Euler(0, 0, p.roll);
        }

        readonly List<Action> due = new List<Action>();
        readonly List<Tween> finished = new List<Tween>();

        void Step(float dt)
        {
            clock += dt;
            // What is due and what has ended are gathered first and done after: what they start (or Clear) changes the lists.
            due.Clear();
            for (int k = later.Count - 1; k >= 0; k--)
            {
                if (later[k].at > clock) continue;
                due.Add(later[k].what);
                later.RemoveAt(k);
            }
            foreach (var what in due) what();
            finished.Clear();
            for (int k = tweens.Count - 1; k >= 0; k--)
            {
                var w = tweens[k];
                w.age += dt;
                float t = Mathf.Min(1, w.age / w.seconds);
                w.step(t);
                if (t < 1) continue;
                tweens.RemoveAt(k);
                finished.Add(w);
            }
            int kept = 0;
            for (int k = 0; k < particles.Count; k++)
            {
                var p = particles[k];
                p.age += dt;
                float t = p.age / p.life;
                if (t >= 1)
                {
                    DestroyNow(p.obj.gameObject);
                    continue;
                }
                particles[kept++] = p;
                p.velocity.y -= p.gravity * dt;
                if (p.drag > 0) p.velocity *= Mathf.Exp(-p.drag * dt);
                var at = p.obj.position + p.velocity * dt;
                if (p.gravity > 0 && at.y < p.floor)
                {
                    at.y = p.floor;
                    p.velocity = new Vector3(p.velocity.x * 0.55f, -p.velocity.y * 0.35f, p.velocity.z * 0.55f);
                }
                p.obj.position = at;
                if (p.sprite) Face(p);
                else if (p.spin != Vector3.zero) p.obj.rotation *= Quaternion.Euler(p.spin * dt);
                float size = 1 + p.grow * t;
                if (p.fades)
                {
                    p.obj.localScale = p.scale * size;
                    Paint(p.renderer, p.texture, WithAlpha(p.colour, p.opacity * (1 - t * t)));
                }
                // Pops in, then shrinks away (no fading: Synty's shapes are opaque).
                else
                {
                    p.obj.localScale = p.scale * (size * Mathf.Min(1, 0.3f + t * 7) * (1 - t * t * t));
                    if (p.colourTo is Color to) Paint(p.renderer, null, Color.Lerp(p.colour, to, Mathf.SmoothStep(0, 1, t)));
                }
            }
            particles.RemoveRange(kept, particles.Count - kept);
            foreach (var w in finished) w.done.TrySetResult();
        }

        /// <summary>Runs `step(t)`, t from 0 to 1 over `seconds`, on the effects' own clock; ends with t = 1.</summary>
        Awaitable Run(float seconds, Action<float> step, CancellationToken ct = default)
        {
            var w = new Tween { seconds = Mathf.Max(1e-4f, seconds), step = step, done = new AwaitableCompletionSource() };
            if (ct.CanBeCanceled)
                ct.Register(() =>
                {
                    if (!tweens.Remove(w)) return;
                    w.step(1);
                    w.done.TrySetCanceled();
                });
            tweens.Add(w);
            return w.done.Awaitable;
        }

        void After(float seconds, Action what) => later.Add((clock + seconds, what));

        /// <summary>A light at `at` for an effect: a dark one, or the dimmest; it is that effect's until another takes it.</summary>
        (Action<float> set, Action done) LampAt(Vector3 at, Color colour, float intensity, float distance)
        {
            Load();
            int k = Array.FindIndex(lamps, l => l.light.intensity == 0);
            if (k < 0)
            {
                k = 0;
                for (int i = 1; i < Lamps; i++) if (lamps[i].light.intensity < lamps[k].light.intensity) k = i;
            }
            var lamp = lamps[k];
            int use = ++lamp.use;
            lamp.light.color = colour;
            lamp.light.range = distance * T;
            lamp.light.transform.position = at;
            lamp.light.intensity = intensity * LampScale;
            return (i =>
            {
                if (lamp.use == use) lamp.light.intensity = Mathf.Max(0, i) * LampScale;
            }, () =>
            {
                if (lamp.use == use) lamp.light.intensity = 0;
            });
        }

        /// <summary>Every effect taken away at once, those in flight too (their waits end).</summary>
        public void Clear()
        {
            foreach (var p in particles) DestroyNow(p.obj.gameObject);
            particles.Clear();
            later.Clear();
            foreach (var w in tweens) w.done.TrySetResult();
            tweens.Clear();
            foreach (var lamp in lamps)
            {
                if (lamp == null) continue;
                lamp.use++;
                lamp.light.intensity = 0;
            }
        }

        // ------------------------------------------------------------ the toolkit

        /// <summary>
        /// Faceted puffs (smoke, dust, gas): lit, they swell and shrink away. `spread` scatters them round `at`, `rise`
        /// lifts them, `away` pushes them out from the middle (tiles, tiles a second).
        /// </summary>
        public void Puffs(Vector3 at, Color colour, int count, float size = 0.4f, float spread = 0.3f, float rise = 0.5f, float away = 0.3f, float life = 1.2f, float grow = 0.8f, Color? clears = null)
        {
            Load();
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0, Mathf.PI * 2);
                float d = Rand(0, spread);
                var p = Solid(at + new Vector3(Mathf.Cos(a) * d, Rand(0, 0.15f), Mathf.Sin(a) * d) * T, Shape(f => f.smokePuff), colour, size * Rand(0.7f, 1.3f));
                p.colourTo = clears;
                Spawn(p, life * Rand(0.75f, 1.25f), grow, drag: 1.2f,
                    velocity: new Vector3(Mathf.Cos(a) * away * Rand(0.5f, 1), rise * Rand(0.6f, 1.2f), Mathf.Sin(a) * away * Rand(0.5f, 1)),
                    spin: new Vector3(Rand(-0.6f, 0.6f), Rand(-0.6f, 0.6f), 0));
            }
        }

        /// <summary>Licks of flame rising from round `at` (within `radius` tiles), with a few embers.</summary>
        void Flames(Vector3 at, int count, float radius = 0.3f, float size = 0.45f, float life = 0.7f)
        {
            Load();
            // Yellower than fire looks: past white the tone mapping turns a saturated orange pink.
            int[] colours = { 0xffb030, 0xffd050, 0xff9a20, 0xffe080 };
            var mesh = Shape(f => f.flame);
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0, Mathf.PI * 2);
                float d = Mathf.Sqrt(Rand(0, 1)) * radius;
                float s = size * Rand(0.6f, 1.2f);
                var obj = Make("llama", mesh ?? sphere, hotMaterial, mesh != null, false, out var r);
                obj.position = at + new Vector3(Mathf.Cos(a) * d, s * 0.4f, Mathf.Sin(a) * d) * T;
                obj.rotation = Quaternion.Euler(0, Rand(0, 360), 0);
                var p = new Particle { obj = obj, renderer = r, colour = Hex(colours[i % colours.Length]) * 1.3f, scale = Vector3.one * s * T, opacity = 1 };
                Spawn(p, life * Rand(0.6f, 1.3f), 0.3f, velocity: new Vector3(0, Rand(0.3f, 0.8f), 0), spin: new Vector3(0, Rand(-2, 2), 0));
            }
            Embers(at, Mathf.CeilToInt(count / 2f), radius);
        }

        void Embers(Vector3 at, int count, float radius = 0.3f, Color? colour = null)
        {
            for (int i = 0; i < count; i++)
            {
                var s = GlowSprite(at + new Vector3(Rand(-radius, radius), Rand(0.1f, 0.5f), Rand(-radius, radius)) * T, colour ?? Hex(i % 2 == 1 ? 0xffa040 : 0xff6a20), Rand(0.06f, 0.11f));
                Spawn(s, Rand(0.6f, 1.2f), velocity: new Vector3(Rand(-0.3f, 0.3f), Rand(0.8f, 1.6f), Rand(-0.3f, 0.3f)));
            }
        }

        /// <summary>A sparkle star and its light: a gun's mouth, a bang.</summary>
        void Flash(Vector3 at, Color colour, float size = 0.8f, float light = 9, float life = 0.09f)
        {
            Spawn(Sprite(at, TextureOf(f => f.sparkle), colour, size, 1, true), life, 0.5f);
            Spawn(GlowSprite(at, Color.white, size * 0.45f), life * 0.7f);
            if (light <= 0) return;
            var (set, done) = LampAt(at, colour, light, 6);
            _ = Run(Mathf.Max(0.08f, life), t => set(light * (1 - t)));
            After(Mathf.Max(0.08f, life), done);
        }

        /// <summary>A sprite lying on the ground (a crack, a ring, a splash), fading over `life`.</summary>
        void Decal(Vector3 at, Texture texture, Color colour, float size, float life, float opacity = 0.85f, float grow = 0, bool additive = false)
        {
            Load();
            var obj = Make("mancha", quad, additive ? added : decals, false, false, out var r);
            obj.position = new Vector3(at.x, at.y + (0.02f + Rand(0, 0.01f)) * T, at.z);
            obj.rotation = Quaternion.Euler(90, Rand(0, 360), 0);
            var p = new Particle { obj = obj, renderer = r, texture = texture, colour = colour, fades = true, scale = Vector3.one * size * T, opacity = opacity };
            Spawn(p, life, grow);
        }

        void Gore(Vector3 at, int count, float speed = 4)
        {
            Mesh[] chunks = { Shape(f => f.goreChunk1), Shape(f => f.goreChunk2), Shape(f => f.goreChunk3) };
            for (int i = 0; i < count; i++)
            {
                var p = Solid(at, chunks[i % chunks.Length], GoreColours[i % GoreColours.Length], Rand(0.07f, 0.15f));
                var dir = new Vector3(Rand(-1, 1), Rand(0.5f, 1.4f), Rand(-1, 1)).normalized;
                Spawn(p, Rand(1.6f, 2.6f), gravity: 12, velocity: dir * speed * Rand(0.5f, 1), spin: new Vector3(Rand(-10, 10), Rand(-10, 10), 0));
            }
        }

        /// <summary>Splinters (wood, glass, paper) flying out.</summary>
        void Chips(Vector3 at, Color colour, int count, float speed = 3)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Solid(at, Shape(f => f.woodChip), colour, Rand(0.09f, 0.16f));
                var dir = new Vector3(Rand(-1, 1), Rand(0.4f, 1.2f), Rand(-1, 1)).normalized;
                Spawn(p, Rand(0.9f, 1.5f), gravity: 11, velocity: dir * speed * Rand(0.5f, 1), spin: new Vector3(Rand(-14, 14), Rand(-14, 14), Rand(-14, 14)));
            }
        }

        void Motes(Vector3 at, Color colour, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var s = GlowSprite(at + new Vector3(Rand(-0.35f, 0.35f), Rand(-0.4f, 0.5f), Rand(-0.35f, 0.35f)) * T, colour, Rand(0.1f, 0.16f));
                Spawn(s, Rand(0.8f, 1.3f), velocity: new Vector3(0, Rand(0.7f, 1.4f), 0));
            }
        }

        void DebrisPiece(Vector3 at, Color colour, float speed)
        {
            var p = Solid(new Vector3(at.x, Mathf.Max(at.y, FloorOf(at) + 0.3f * T), at.z), null, colour, 0.12f * Rand(0.6f, 1.8f));
            var dir = new Vector3(Rand(-0.5f, 0.5f), Rand(0.6f, 1.5f), Rand(-0.5f, 0.5f)).normalized;
            Spawn(p, Rand(1.4f, 2f), gravity: 14, velocity: dir * speed, spin: new Vector3(Rand(0, 12), Rand(0, 12), 0));
        }

        // ------------------------------------------------------------ weapons

        /// <summary>A star at the muzzle, its light, and a wisp of smoke off the barrel.</summary>
        public void MuzzleFlash(Vector3 at, Color colour)
        {
            Flash(at, colour, 0.75f);
            Puffs(at, Hex(0x8a8a86), 1, size: 0.12f, spread: 0.02f, rise: 0.4f, away: 0.1f, life: 0.6f, grow: 1.2f);
        }

        /// <summary>A bright streak from `from` to `to` at `speed` tiles a second; `heavy` for sniper rounds. Ends as it lands.</summary>
        public async Awaitable Bullet(Vector3 from, Vector3 to, Color colour, float speed = 60, bool heavy = false, CancellationToken ct = default)
        {
            Load();
            var streak = Make("bala", cylinder, added, false, false, out var r);
            // Unity's cylinder: 1 across, 2 long along its y.
            streak.localScale = heavy ? new Vector3(0.08f, 0.8f, 0.08f) * T : new Vector3(0.036f, 0.45f, 0.036f) * T;
            var dir = to - from;
            if (dir.sqrMagnitude > 1e-6f) streak.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
            Paint(r, null, colour);
            try
            {
                await Run(Mathf.Max(0.05f, dir.magnitude / T / speed), t => streak.position = Vector3.Lerp(from, to, t), ct);
            }
            finally
            {
                DestroyNow(streak.gameObject);
            }
        }

        public void Sparks(Vector3 at, Color colour, int count = 9)
        {
            for (int i = 0; i < count; i++)
            {
                var s = GlowSprite(at, colour, 0.12f + Rand(0, 0.08f));
                var dir = new Vector3(Rand(-0.5f, 0.5f), Rand(0, 0.8f), Rand(-0.5f, 0.5f)).normalized;
                Spawn(s, 0.3f + Rand(0, 0.25f), gravity: 9, velocity: dir * (2.5f + Rand(0, 3)));
            }
            Spawn(GlowSprite(at, colour, 0.5f, 0.8f), 0.15f, 1);
        }

        /// <summary>A blade's arc flashing across the target.</summary>
        public void Slash(Vector3 at, Color colour)
        {
            var arc = Sprite(at, TextureOf(f => f.semiCircle), colour, 1.2f, 1, true);
            Spawn(arc, 0.28f, 0.6f);
            Sparks(at, colour, 10);
        }

        /// <summary>A medkit: green motes rising and a first-aid cross.</summary>
        public void Heal(Vector3 at)
        {
            Motes(at, Hex(0x6dff9a), 12);
            Load();
            var mesh = Shape(f => f.medCross);
            var obj = Make("cruz", mesh ?? cube, blended, mesh != null, false, out var r);
            obj.position = at + Vector3.up * 0.7f * T;
            var p = new Particle { obj = obj, renderer = r, colour = Hex(0x6dff9a) * 1.4f, fades = true, scale = Vector3.one * 0.26f * T, opacity = 0.95f };
            Spawn(p, 1.2f, velocity: new Vector3(0, 0.55f, 0), spin: new Vector3(0, 2.2f, 0));
        }

        /// <summary>A brief protective bubble (Aid Protocol).</summary>
        public void Shield(Vector3 at)
        {
            Load();
            var obj = Make("escudo", sphere, added, false, false, out var r);
            obj.position = at;
            var p = new Particle { obj = obj, renderer = r, colour = Hex(0x54d1ff), fades = true, scale = Vector3.one * 1.7f * T, opacity = 0.35f };
            Spawn(p, 1.1f, 0.25f);
        }

        /// <summary>A volley of small missiles converging on `targets`; ends when the last one lands.</summary>
        public async Awaitable Missiles(Vector3 from, IReadOnlyList<Vector3> targets, CancellationToken ct = default)
        {
            Load();
            var flights = new List<Awaitable>();
            for (int i = 0; i < targets.Count; i++) flights.Add(Missile(from, targets[i], i * 0.09f, ct));
            foreach (var f in flights) await f;
        }

        async Awaitable Missile(Vector3 from, Vector3 to, float delay, CancellationToken ct)
        {
            if (delay > 0) await Run(delay, _ => { }, ct);
            var missile = Make("misil", cylinder, solid, false, true, out var r);
            missile.localScale = new Vector3(0.09f, 0.16f, 0.09f) * T;
            Paint(r, null, Hex(0x9aa3ad));
            float height = (2.5f + Vector3.Distance(from, to) / T * 0.2f) * T;
            var last = from;
            try
            {
                await Run(0.7f, t =>
                {
                    var at = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * height;
                    if ((at - last).sqrMagnitude > 1e-6f) missile.rotation = Quaternion.LookRotation(at - last) * Quaternion.Euler(90, 0, 0);
                    missile.position = at;
                    if (Vector3.Distance(at, last) > 0.4f * T)
                    {
                        Puffs(at, Hex(0x8a8a86), 1, size: 0.22f, spread: 0, rise: 0.2f, away: 0.05f, life: 0.8f, grow: 1.4f);
                        last = at;
                    }
                }, ct);
            }
            finally
            {
                DestroyNow(missile.gameObject);
            }
        }

        /// <summary>Someone leaving the map, or a body getting up: dust kicked up round the spot and a little grit.</summary>
        public void EvacBeam(Vector3 at)
        {
            var ground = new Vector3(at.x, FloorOf(at), at.z);
            Dust(ground, 8);
            Chips(ground + Vector3.up * 0.2f * T, Hex(0x6e6152), 5, 2);
            Decal(ground, TextureOf(f => f.shockRing), Hex(0xb8a88e), 0.5f, 0.6f, 0.45f, 2.2f);
        }

        // ------------------------------------------------------------ explosives

        public async Awaitable GrenadeArc(Vector3 from, Vector3 to, CancellationToken ct = default)
        {
            Load();
            var shell = Make("granada", sphere, solid, false, true, out var r);
            // A hand grenade's size, 14 cm (0.10.1's was a tile-unit 0.18 across: 27 cm, a head's; Gameplay 24/10), and a
            // faint glow round it so that it shows from the game's camera.
            shell.localScale = Vector3.one * 0.14f;
            Paint(r, null, Hex(0x3b4a3a));
            var halo = Make("brillo", quad, added, false, false, out var hr);
            halo.SetParent(shell, false);
            halo.localScale = Vector3.one * 0.35f / 0.14f;
            Paint(hr, glow, WithAlpha(Hex(0xfff2d8), 0.45f));
            float d = Vector3.Distance(from, to) / T;
            float height = (1.5f + d * 0.3f) * T;
            try
            {
                await Run(0.55f + d * 0.03f, t =>
                {
                    shell.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * height;
                    shell.rotation *= Quaternion.Euler(23, 0, 0);
                    if (Camera.main != null) halo.rotation = Camera.main.transform.rotation;
                }, ct);
            }
            finally
            {
                DestroyNow(shell.gameObject);
            }
        }

        /// <summary>
        /// A stone thrown: a low, quick arc from the hand to `to`, a dull knock where it lands and it lies there a
        /// moment. `struck`: `to` is someone's chest, and from there it drops at their feet. Ends as it lands or strikes.
        /// </summary>
        public async Awaitable Stone(Vector3 from, Vector3 to, bool struck = false, CancellationToken ct = default)
        {
            Load();
            var rock = Make("piedra", sphere, solid, false, true, out var r);
            rock.localScale = new Vector3(0.12f, 0.086f, 0.106f) * T;
            Paint(r, null, Hex(0x77736b));
            float d = Vector3.Distance(from, to) / T;
            float height = ((struck ? 0.25f : 0.5f) + d * 0.12f) * T;
            try
            {
                await Run(0.3f + d * 0.025f, t =>
                {
                    rock.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * height;
                    rock.rotation *= Quaternion.Euler(29, 0, 17);
                }, ct);
            }
            catch (OperationCanceledException)
            {
                DestroyNow(rock.gameObject);
                throw;
            }
            var ground = struck ? new Vector3(to.x, FloorOf(to), to.z) : to;
            var away = new Vector3(to.x - from.x, 0, to.z - from.z).normalized * (struck ? 0.2f : 0.18f) * T;
            if (!struck) Dust(ground, 2);
            // Off the body down to the feet, or a short hop on from where it hit the ground; then it lies a while.
            var start = rock.position;
            float hop = struck ? 0.26f : 0.16f;
            _ = Run(hop, t =>
            {
                var at = Vector3.Lerp(start, ground + away, t);
                at.y = struck ? Mathf.Lerp(start.y, ground.y + 0.04f * T, t * t) : ground.y + (0.04f + Mathf.Sin(t * Mathf.PI) * 0.1f) * T;
                rock.position = at;
            });
            After(hop, () =>
            {
                if (struck) Dust(rock.position, 1);
            });
            After(hop + 1.6f, () => DestroyNow(rock.gameObject));
        }

        /// <summary>A fireball with its light, shock ring, debris and smoke; `radius` in tiles. Ends as the fire is out (0.6 s).</summary>
        public async Awaitable Explosion(Vector3 at, float radius, CancellationToken ct = default)
        {
            Load();
            var fire = Make("fuego", blast, added, false, false, out var r);
            fire.position = at + Vector3.up * 0.45f * T;
            fire.rotation = Quaternion.Euler(Rand(0, 360), Rand(0, 360), 0);
            var (set, done) = LampAt(at + Vector3.up * 1.35f * T, Hex(0xff8a30), 45, radius * 6);
            Decal(at, TextureOf(f => f.shockRing), Hex(0xffd9a0), radius * 0.6f, 0.6f, 0.8f, 4.5f, true);
            Flash(at + Vector3.up * 0.6f * T, Hex(0xffe0a0), radius * 1.2f, 0, 0.18f);
            for (int i = 0; i < 14; i++) DebrisPiece(at, Hex(i % 3 == 0 ? 0x5a4a3a : 0x2c2f33), 5 + Rand(0, 4));
            // The scorch left behind, and smoke rolling up out of the fire.
            Decal(at, TextureOf(f => f.groundCrack), Hex(0x1a1714), radius * 1.3f, 7, 0.9f);
            Flames(at, 8, radius * 0.4f, 0.6f, 0.6f);
            // (Close up the dark puffs read as rocks left lying: Gameplay 24/10. They clear to grey as they rise and go.)
            After(0.15f, () => Puffs(at, Hex(0x3b3936), 8, size: 0.55f, spread: radius * 0.5f, rise: 1.5f, away: 0.6f, life: 2.2f, grow: 1.4f, clears: Hex(0xa29e98)));
            try
            {
                await Run(0.6f, t =>
                {
                    // three.js's icosahedron of radius 1; ours is 1 across.
                    fire.localScale = Vector3.one * 2 * (0.3f + radius * Mathf.Sqrt(t)) * T;
                    var c = Color.HSVToRGB(0.08f - t * 0.06f, 1, 1);
                    Paint(r, null, WithAlpha(Color.Lerp(c, Color.white, 0.2f) * (1.2f - t * 0.6f), 0.95f * (1 - t)));
                    set(45 * (1 - t));
                }, ct);
            }
            finally
            {
                DestroyNow(fire.gameObject);
                done();
            }
        }

        /// <summary>Chunks flying out of a cover block broken.</summary>
        public void Debris(Vector3 at, Color colour, int count = 8)
        {
            for (int i = 0; i < count; i++) DebrisPiece(at, colour, 3 + Rand(0, 2));
            Puffs(at, Hex(0x8f8676), 3, size: 0.35f, spread: 0.3f, rise: 0.4f, away: 0.5f, life: 1.2f);
        }

        /// <summary>
        /// A blow on a siege's door ("door": sparks, rust and paint, the dust it shakes off) or barricade (earth
        /// bursting out, grit), `at` where it lands; `gives`, the blow that brings it down: a good deal more.
        /// </summary>
        public void Smash(Vector3 at, string kind, bool gives = false)
        {
            float k = gives ? 2.5f : 1;
            if (kind == "door")
            {
                Sparks(at, Hex(0xffc46b), Mathf.RoundToInt(6 * k));
                Chips(at, Hex(0x6f6a5e), Mathf.RoundToInt(4 * k), 2.6f);
                Puffs(at - Vector3.up * 0.5f * T, Hex(0x8f8a80), Mathf.RoundToInt(2 * k), size: 0.3f, spread: 0.35f, rise: 0.25f, away: 0.45f, life: 1.1f);
                if (gives) Debris(at - Vector3.up * 0.8f * T, Hex(0x5a5e62), 6);
            }
            else
            {
                Puffs(at, Hex(0x9a8462), Mathf.RoundToInt(3 * k), size: 0.34f, spread: 0.3f, rise: 0.35f, away: 0.6f, life: 1.3f, grow: 1.3f);
                Chips(at, Hex(0xb39a6c), Mathf.RoundToInt(6 * k), 2.2f);
                if (gives) Dust(at - Vector3.up * 0.5f * T, 10);
            }
        }

        // ------------------------------------------------------------ the dead

        /// <summary>The Hinchado bursting (on top of the blast): a sickly green cloud, gore, dark splashes round it.</summary>
        public void Burst(Vector3 at, float radius)
        {
            var cloud = at + Vector3.up * 0.3f * T;
            Puffs(cloud, Hex(0x7f9a3c), 9, size: 0.7f, spread: radius * 0.35f, rise: 0.25f, away: radius * 0.7f, life: 3.2f, grow: 1.6f);
            Puffs(cloud, Hex(0xa3b452), 5, size: 0.5f, spread: radius * 0.25f, rise: 0.35f, away: radius * 0.5f, life: 2.6f, grow: 1.4f);
            Gore(at + Vector3.up * 0.8f * T, 14, 5);
            for (int i = 0; i < 7; i++)
            {
                float a = Rand(0, Mathf.PI * 2);
                float d = 0.2f + Rand(0, radius * 0.9f);
                Decal(at + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d) * T, TextureOf(f => f.polyDisc), Blood[i % Blood.Length], Rand(0.4f, 0.9f), 6 + Rand(0, 3), 0.9f);
            }
        }

        /// <summary>The Chillón's scream: rings of pressure rolling out from its head.</summary>
        public void Scream(Vector3 head)
        {
            for (int i = 0; i < 3; i++)
                After(i * 0.13f, () =>
                {
                    Load();
                    var obj = Make("grito", quad, added, false, false, out var r);
                    obj.position = head;
                    obj.rotation = Quaternion.Euler(90 + Rand(-11, 11), Rand(0, 360), 0);
                    // A ring of radius 0.25 tiles at first: the quad is its width.
                    var p = new Particle { obj = obj, renderer = r, texture = TextureOf(f => f.shockRing), colour = Shriek, fades = true, scale = Vector3.one * 0.5f * T, opacity = 0.6f };
                    Spawn(p, 0.75f, 7);
                });
        }

        /// <summary>A bite or a claw that gets in: blood spurting away from the attacker (`from`), a drop or two on the ground.</summary>
        public void Bite(Vector3 at, Vector3 from)
        {
            var away = at - from;
            away.y = 0;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            for (int i = 0; i < 12; i++)
            {
                var drop = Sprite(at, TextureOf(f => f.polyDisc), Blood[i % Blood.Length], Rand(0.07f, 0.14f), 0.95f);
                var dir = away * (0.8f + Rand(0, 1)) + new Vector3(Rand(-0.45f, 0.45f), 0.5f + Rand(0, 0.9f), Rand(-0.45f, 0.45f));
                Spawn(drop, 0.45f + Rand(0, 0.3f), gravity: 9, velocity: dir * (1.8f + Rand(0, 1.6f)));
            }
            Gore(at, 2, 2.5f);
            var ground = at + away * 0.5f * T;
            ground.y = FloorOf(at);
            Decal(ground, TextureOf(f => f.polyDisc), Blood[1], Rand(0.25f, 0.4f), 5, 0.85f);
        }

        /// <summary>Dust kicked up by feet or wheels at the ground point `at`.</summary>
        public void Dust(Vector3 at, int amount = 6) =>
            Puffs(at + Vector3.up * 0.1f * T, Hex(0x9a8a72), amount, size: 0.32f, spread: 0.45f, rise: 0.35f, away: 0.4f, life: 1.3f, grow: 1.4f);

        /// <summary>
        /// The Chillón's shriek reaching someone (a soldier it rattles, a corpse it raises): ripples of sound rolling to
        /// them and a jolt of pressure where they land. Ends when the wave arrives.
        /// </summary>
        public async Awaitable PsiBeam(Vector3 from, Vector3 to, Color? colour = null, CancellationToken ct = default)
        {
            Load();
            var c = colour ?? Shriek;
            const float travel = 0.45f;
            var dir = to - from;
            var turn = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            for (int i = 0; i < 5; i++)
                After(i * 0.07f, () =>
                {
                    var ring = Make("onda", quad, added, false, false, out var r);
                    ring.rotation = turn;
                    _ = Run(travel, t =>
                    {
                        ring.position = Vector3.Lerp(from, to, t);
                        ring.localScale = Vector3.one * 2 * (0.12f + t * 0.45f) * T;
                        Paint(r, TextureOf(f => f.shockRing), WithAlpha(c, 0.4f * (1 - t * 0.7f)));
                    });
                    After(travel, () => DestroyNow(ring.gameObject));
                });
            await Run(travel, _ => { }, ct);
            Spawn(Sprite(to, TextureOf(f => f.shockRing), c, 0.4f, 0.8f, true), 0.35f, 3);
            Dust(new Vector3(to.x, FloorOf(to), to.z), 3);
            await Run(4 * 0.07f, _ => { }, ct);
        }

        // ------------------------------------------------------------ small things

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>The floor of the storey `at` is on.</summary>
        static float FloorOf(Vector3 at) => Mathf.Floor(at.y / MapSpace.Storey + 0.05f) * MapSpace.Storey;

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static void DestroyNow(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
