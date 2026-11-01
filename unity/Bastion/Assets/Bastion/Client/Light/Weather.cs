using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Bastion.Client
{
    /// <summary>
    /// The rain or the snow of a mission (GameState.weather, Arturo 1/11: the five tries of ClimaLab, each in missions of
    /// its own): its sky (Mood.For takes WeatherMood), what falls as particles over the ground the camera sees (following
    /// it, wider as it draws back), and over the picture (Clima.shader, a pass enqueued on the game's camera, the project's
    /// renderers untouched) the wet, the puddles with rings where drops fall, the snow lying, a storm's lightning. Never
    /// under a roof (a map of each tile's roof height, read by the pass and the particles), nor over the figures (their
    /// stencil bit). The winter's snow cover (SnowCover) is as it was: snowfall and blizzard go over it.
    ///   drizzle: a fine drizzle in a mist, everything damp. downpour: rain slanting in the wind, splashes, puddles.
    ///   storm: heavier, in gusts, darker, the haze closer, lightning. snowfall: flakes drifting down, snow lying on what
    ///   faces up. blizzard: snow blown sideways, wisps along the ground, a white-out haze.
    /// </summary>
    public sealed class Weather : MonoBehaviour, IShotStep
    {
        public static Weather Current { get; private set; }

        /// <summary>A kind of particle: a square metre's a second, how long each lasts, how fast it falls and is blown, how big, its colour; stretched along its way by `stretch` (0: round), stirred by `noise`.</summary>
        sealed class Fall
        {
            public string name;
            public float rate, life, speed, sizeMin, sizeMax, stretch, noise, noiseFrequency;
            public Vector3 wind;
            public Color colour;
            /// <summary>Where they start: from the ground up to this high (m; null: high enough to land at the end of their life).</summary>
            public float? low, high;
            /// <summary>Growing over their life (splashes), fading in and out (snow).</summary>
            public bool grow, fade;
            /// <summary>How much brighter than `colour` they are drawn (unlit, a flake would be darker than the lit snow it falls on).</summary>
            public float gain = 1.3f;
        }

        sealed class Kind
        {
            public Mood.Values mood;
            public float wet, puddles, ripples, snow;
            /// <summary>What wet faces and puddles show (sRGB).</summary>
            public int sky;
            public Fall[] falls;
            /// <summary>Lightning, every so many seconds (0: none); the wind's gusts (how much of it comes and goes, how fast).</summary>
            public float lightning, gust, gustRate;
        }

        static Mood.Values Sky(int haze, float density, int sun, float sunIntensity, int skyLight, int groundLight, float exposure, float saturation, float contrast, float vignette, Vector3 lift) =>
            new Mood.Values
            {
                haze = haze, hazeDensity = density, sun = sun, sunIntensity = sunIntensity, sunFrom = Mood.Day.sunFrom,
                skyLight = skyLight, groundLight = groundLight, skyIntensity = Mood.Day.skyIntensity, fill = 0xeef2f6, fillIntensity = 0.3f, exposure = exposure,
                bloomStrength = Mood.Day.bloomStrength, bloomThreshold = Mood.Day.bloomThreshold, saturation = saturation, contrast = contrast, vignette = vignette,
                shadowTint = new Vector3(0.97f, 1.0f, 1.04f), highlightTint = new Vector3(1.0f, 1.0f, 1.02f), lift = lift,
            };

        static Fall Splash(float rate) => new Fall
        {
            name = "Salpicaduras", rate = rate, life = 0.22f, speed = 0, sizeMin = 0.1f, sizeMax = 0.22f, colour = new Color(0.86f, 0.9f, 0.95f, 0.45f),
            low = 0.04f, high = 0.06f, grow = true,
        };

        static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            [Weathers.Drizzle] = new Kind
            {
                wet = 0.55f, sky = 0xb4bcc6,
                mood = Sky(0xaeb6bc, 0.014f, 0xd8e0ea, 1.4f, 0xa8b2c0, 0x5e5a56, 1.34f, 0.72f, 1.0f, 0.22f, new Vector3(0.06f, 0.065f, 0.07f)),
                falls = new[] { new Fall { name = "Llovizna", rate = 0.9f, life = 1.6f, speed = 9, sizeMin = 0.045f, sizeMax = 0.06f, stretch = 0.06f, wind = new Vector3(1.2f, 0, 0.6f), colour = new Color(0.84f, 0.88f, 0.92f, 0.3f) } },
            },
            [Weathers.Downpour] = new Kind
            {
                wet = 0.9f, puddles = 0.8f, ripples = 1.6f, sky = 0xa6b0bc,
                mood = Sky(0x9aa2aa, 0.009f, 0xdde4ee, 1.2f, 0x9aa6b6, 0x4e4a46, 1.36f, 0.7f, 1.02f, 0.24f, new Vector3(0.05f, 0.055f, 0.06f)),
                falls = new[]
                {
                    new Fall { name = "Lluvia", rate = 2.2f, life = 0.9f, speed = 24, sizeMin = 0.05f, sizeMax = 0.07f, stretch = 0.045f, wind = new Vector3(3, 0, 1.5f), colour = new Color(0.78f, 0.84f, 0.92f, 0.32f) },
                    Splash(1.0f),
                },
            },
            [Weathers.Storm] = new Kind
            {
                wet = 1, puddles = 1, ripples = 2.4f, sky = 0x8a96a6, lightning = 9, gust = 0.6f, gustRate = 0.7f,
                mood = Sky(0x6c757e, 0.013f, 0xc8d2e0, 0.7f, 0x7c8898, 0x3a3836, 1.3f, 0.62f, 1.08f, 0.32f, new Vector3(0.035f, 0.04f, 0.05f)),
                falls = new[]
                {
                    new Fall { name = "Lluvia", rate = 3.4f, life = 0.8f, speed = 28, sizeMin = 0.05f, sizeMax = 0.075f, stretch = 0.045f, wind = new Vector3(8, 0, 3), colour = new Color(0.76f, 0.82f, 0.9f, 0.35f) },
                    Splash(1.5f),
                },
            },
            [Weathers.Snowfall] = new Kind
            {
                wet = 0.15f, snow = 0.75f, sky = 0xc8d4e0,
                mood = Sky(0xc8d2dc, 0.01f, 0xeef4ff, 2.0f, 0xc1d5ed, 0x8a98a8, 1.18f, 0.82f, 1.03f, 0.2f, new Vector3(0.045f, 0.055f, 0.07f)),
                falls = new[] { new Fall { name = "Copos", rate = 0.6f, life = 10, speed = 1.1f, sizeMin = 0.07f, sizeMax = 0.13f, noise = 0.5f, noiseFrequency = 0.35f, wind = new Vector3(0.5f, 0, 0.3f), colour = new Color(1, 1, 1, 0.9f), high = 11, fade = true, gain = 2.6f } },
            },
            [Weathers.Blizzard] = new Kind
            {
                wet = 0.1f, snow = 1, sky = 0xd8e2ea, gust = 0.35f, gustRate = 0.5f,
                mood = Sky(0xd6dee6, 0.021f, 0xeef4ff, 1.4f, 0xd0dcea, 0x98a6b4, 1.15f, 0.75f, 1.0f, 0.28f, new Vector3(0.05f, 0.06f, 0.075f)),
                falls = new[]
                {
                    new Fall { name = "Copos", rate = 1.6f, life = 3.5f, speed = 2.5f, sizeMin = 0.06f, sizeMax = 0.1f, stretch = 0.02f, noise = 1.2f, noiseFrequency = 0.5f, wind = new Vector3(14, 0, 5), colour = new Color(1, 1, 1, 0.85f), high = 10, fade = true, gain = 2.4f },
                    new Fall { name = "Ventisca", rate = 0.09f, life = 3, speed = 0, sizeMin = 2.5f, sizeMax = 4.5f, wind = new Vector3(9, 0, 3.2f), colour = new Color(1, 1, 1, 0.2f), low = 0.1f, high = 1.2f, fade = true, gain = 2.2f },
                },
            },
        };

        /// <summary>The sky by day under weather `id` (Mood.For); null for clear or one it doesn't know.</summary>
        public static Mood.Values WeatherMood(string id) => id != null && Kinds.TryGetValue(id, out var k) ? k.mood : null;

        /// <summary>
        /// By night the haze closes in under rain and snow (the night's own, denser by so much), the particles drawn darker
        /// (unlit, they would glow), the lightning brighter.
        /// </summary>
        public static readonly Dictionary<string, float> NightHaze = new Dictionary<string, float>
        {
            [Weathers.Drizzle] = 1.2f, [Weathers.Downpour] = 1.3f, [Weathers.Storm] = 1.5f, [Weathers.Snowfall] = 1.2f, [Weathers.Blizzard] = 1.7f,
        };
        const float NightGain = 0.35f;
        /// <summary>The most ground the particles cover (m², a square 60 m a side): drawn back further, they thin out.</summary>
        const float MostArea = 3600;

        Kind kind;
        readonly List<(Fall fall, ParticleSystem system, Material material)> falling = new List<(Fall, ParticleSystem, Material)>();
        Texture2D roofs;
        WeatherPass pass;
        float clock, nextFlash, span;
        System.Random random;

        /// <summary>The weather of map `s` (its buildings as `plan` has them) under `parent`; none if it is clear.</summary>
        public static Weather Make(Transform parent, GameState s, BuildingPlan plan)
        {
            if (s.Weather == null || !Kinds.TryGetValue(s.Weather, out var kind)) return null;
            var weather = new GameObject("Clima").AddComponent<Weather>();
            Current = weather;
            weather.transform.SetParent(parent, false);
            weather.kind = kind;
            weather.random = new System.Random(s.Width * 7919 + s.Height * 104729 + (s.Tiles.Count > 0 ? s.Tiles[s.Tiles.Count / 2]?.Length ?? 0 : 0));
            weather.nextFlash = kind.lightning > 0 ? 2 + (float)weather.random.NextDouble() * kind.lightning : float.MaxValue;
            weather.Roofs(s, plan);
            var dot = Dot();
            foreach (var fall in kind.falls)
            {
                var material = KitMaterials.New(KitMaterials.ClimaGota, fall.name);
                if (material == null) continue;
                material.mainTexture = dot;
                weather.falling.Add((fall, Emitter(weather.transform, fall, material), material));
            }
            weather.pass = new WeatherPass();
            RenderPipelineManager.beginCameraRendering += weather.Hook;
            weather.Follow(true);
            foreach (var (fall, system, _) in weather.falling) system.Simulate(fall.life + 0.5f, true, true, false);
            if (Application.isPlaying) foreach (var (_, system, _) in weather.falling) system.Play();
            Debug.Log($"[clima] {s.Weather}: {weather.falling.Count} kinds falling");
            return weather;
        }

        void OnEnable() => Current = this;

        void OnDestroy() => Take();

        /// <summary>
        /// The weather gone: its pass off the cameras, the picture's numbers cleared, its textures freed. (A lab calls it
        /// before taking the weather away: out of play, OnDestroy isn't called.)
        /// </summary>
        public void Take()
        {
            RenderPipelineManager.beginCameraRendering -= Hook;
            if (Current == this) Current = null;
            Shader.SetGlobalVector("_ClimaSize", Vector4.zero);
            Shader.SetGlobalFloat("_ClimaWet", 0);
            Shader.SetGlobalFloat("_ClimaPuddles", 0);
            Shader.SetGlobalFloat("_ClimaSnow", 0);
            Shader.SetGlobalFloat("_ClimaFlash", 0);
            foreach (var (_, _, material) in falling) if (material != null) Destroy(material);
            if (roofs != null) Destroy(roofs);
        }

        /// <summary>
        /// What is under a roof (no rain nor snow there): each tile inside a building the height of its walls' top (tiles;
        /// its roof's top would leave a pitched roof's slopes below it, dry), the rest 0.
        /// </summary>
        void Roofs(GameState s, BuildingPlan plan)
        {
            roofs = new Texture2D(s.Width, s.Height, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Tejados", hideFlags = HideFlags.DontSave };
            var heights = new float[s.Width * s.Height];
            for (int i = 0; i < heights.Length; i++)
                if (plan.Owner[i] >= 0 && plan.Inside[i] != 0)
                {
                    var b = plan.Buildings[plan.Owner[i]];
                    heights[i] = b.@base + BuildingPlan.Storey * b.storeys;
                }
            roofs.SetPixelData(heights, 0);
            roofs.Apply(false);
            Shader.SetGlobalTexture("_ClimaRoofs", roofs);
            Shader.SetGlobalVector("_ClimaSize", new Vector4(s.Width, s.Height, MapSpace.Tile, 1));
        }

        /// <summary>A soft round spot (white, its alpha falling off to the edge), the particles' texture.</summary>
        static Texture2D Dot()
        {
            const int size = 64;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Gota", hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d)));
                }
            t.SetPixels32(pixels);
            t.Apply(true);
            return t;
        }

        /// <summary>The particles of `fall` (their place and their reach set as the camera moves: Follow).</summary>
        static ParticleSystem Emitter(Transform parent, Fall fall, Material material)
        {
            var ps = new GameObject(fall.name).AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.SetParent(parent, false);
            var main = ps.main;
            main.duration = 600;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(fall.life * 0.85f, fall.life);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(fall.sizeMin, fall.sizeMax);
            main.startColor = fall.colour;
            main.gravityModifier = 0;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = Mathf.CeilToInt(fall.rate * MostArea * fall.life * 1.2f) + 10;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = fall.wind.x;
            velocity.y = -fall.speed;
            velocity.z = fall.wind.z;
            if (fall.noise > 0)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = fall.noise;
                noise.frequency = fall.noiseFrequency;
                noise.scrollSpeed = 0.2f;
                noise.quality = ParticleSystemNoiseQuality.Medium;
            }
            if (fall.grow)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.3f, 1, 1));
            }
            if (fall.grow || fall.fade)
            {
                var colour = ps.colorOverLifetime;
                colour.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    fall.grow ? new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) } : new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
                colour.color = g;
            }
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = fall.stretch > 0 ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = fall.stretch;
            renderer.lengthScale = 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // (None big on the screen: one that falls past close to the camera.)
            renderer.maxParticleSize = 0.012f;
            return ps;
        }

        /// <summary>The camera the weather is for: the game's (or a lab's shot camera).</summary>
        static Camera Eye()
        {
            var main = Camera.main;
            if (main != null) return main;
            var shot = GameObject.Find("Shot camera");
            return shot != null ? shot.GetComponent<Camera>() : null;
        }

        /// <summary>
        /// The particles over the ground the camera looks at (where the middle of its view meets the ground), over as wide a
        /// square as it sees, upwind by as far as they drift; as many a square metre up to MostArea, thinner beyond.
        /// </summary>
        void Follow(bool always)
        {
            var eye = Eye();
            if (eye == null) return;
            var ray = new Ray(eye.transform.position, eye.transform.forward);
            float along = ray.direction.y < -1e-3f ? -ray.origin.y / ray.direction.y : 40;
            var target = ray.GetPoint(along);
            float wide = Mathf.Max(30, along * 1.8f);
            bool first = span == 0;
            bool resize = always || first || Mathf.Abs(wide - span) > span * 0.1f;
            if (resize) span = wide;
            foreach (var (fall, system, _) in falling)
            {
                float high = fall.high ?? fall.speed * fall.life;
                float low = fall.low ?? high;
                var drift = fall.wind * fall.life;
                system.transform.position = target + new Vector3(-drift.x * 0.5f, (low + high) / 2, -drift.z * 0.5f);
                if (!resize) continue;
                var shape = system.shape;
                shape.scale = new Vector3(span, Mathf.Max(0.01f, high - low), span);
                var emission = system.emission;
                emission.rateOverTime = fall.rate * Mathf.Min(span * span, MostArea);
                // (The camera found only now, as a lab makes it after the map: the air filled where it looks.)
                if (first && !always) system.Simulate(fall.life + 0.5f, true, true, false);
            }
        }

        void Update() => Step(Time.deltaTime);

        public void ShotStep(float dt) => Step(dt);

        void Step(float dt)
        {
            if (kind == null) return;
            clock += dt;
            Follow(false);
            // Gusts: the wind of each fall swells and drops.
            if (kind.gust > 0)
            {
                float sway = 1 + kind.gust * Mathf.Sin(clock * kind.gustRate * Mathf.PI * 2) * (0.6f + 0.4f * Mathf.Sin(clock * 2.3f));
                foreach (var (fall, system, _) in falling)
                {
                    var velocity = system.velocityOverLifetime;
                    velocity.x = fall.wind.x * sway;
                    velocity.z = fall.wind.z * sway;
                }
            }
            if (clock > nextFlash + 0.35f) nextFlash = clock + kind.lightning * (0.5f + (float)random.NextDouble());
        }

        /// <summary>A flash: two quick strokes and a fading glow, a third of a second.</summary>
        float Flash()
        {
            float k = clock - nextFlash;
            if (k < 0 || k > 0.35f) return 0;
            return k < 0.05f ? 1 : k < 0.09f ? 0.25f : k < 0.14f ? 0.85f : 0.85f * (1 - (k - 0.14f) / 0.21f);
        }

        /// <summary>Before the game's camera draws: the pass's numbers (the clock, the flash), the particles' brightness by day or night, and the pass.</summary>
        void Hook(ScriptableRenderContext context, Camera camera)
        {
            // (Gone without OnDestroy, in the editor out of play: off the cameras.)
            if (this == null)
            {
                RenderPipelineManager.beginCameraRendering -= Hook;
                return;
            }
            if (kind == null || !enabled || camera == null || camera != Eye()) return;
            bool night = Mood.Dark;
            Shader.SetGlobalFloat("_ClimaTime", clock);
            Shader.SetGlobalFloat("_ClimaWet", kind.wet);
            Shader.SetGlobalFloat("_ClimaPuddles", kind.puddles);
            Shader.SetGlobalFloat("_ClimaRipples", kind.ripples);
            Shader.SetGlobalFloat("_ClimaSnow", kind.snow);
            Shader.SetGlobalVector("_ClimaSky", Hex(kind.sky).linear * (night ? 0.25f : 1));
            Shader.SetGlobalVector("_ClimaSnowTint", new Color(0.86f, 0.9f, 0.96f).linear);
            Shader.SetGlobalFloat("_ClimaFlash", Flash() * (night ? 1.6f : 1));
            foreach (var (fall, _, material) in falling) if (material != null) material.SetFloat("_Gain", fall.gain * (night ? NightGain : 1));
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>
        /// The pass over the picture (Clima.shader), after the opaque things and before the transparent ones: the picture
        /// copied, then drawn back through the shader with the depth bound (its stencil keeps the figures out).
        /// </summary>
        sealed class WeatherPass : ScriptableRenderPass
        {
            readonly Material material = KitMaterials.New(KitMaterials.Clima, "Clima");

            public WeatherPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                requiresIntermediateTexture = true;
            }

            sealed class Data
            {
                public TextureHandle copy;
                public Material material;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                if (material == null) return;
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                desc.name = "Clima copia";
                desc.clearBuffer = false;
                var copy = graph.CreateTexture(desc);
                graph.AddBlitPass(source, copy, Vector2.one, Vector2.zero, passName: "Clima copia");
                using (var builder = graph.AddRasterRenderPass<Data>("Clima", out var data))
                {
                    data.copy = copy;
                    data.material = material;
                    builder.UseTexture(copy);
                    // (The depth and normals textures, read as globals.)
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(source, 0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((Data d, RasterGraphContext c) => Blitter.BlitTexture(c.cmd, d.copy, new Vector4(1, 1, 0, 0), d.material, 0));
                }
            }
        }
    }
}
