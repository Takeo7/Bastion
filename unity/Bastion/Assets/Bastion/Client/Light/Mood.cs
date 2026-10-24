using Bastion.Engine;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Light = UnityEngine.Light;

namespace Bastion.Client
{
    /// <summary>
    /// The battle's light (0.10.1's renderer.ts MOODS, set against Synty's promo captures): by day a low, warm sun from the
    /// upper right of the default view throwing long shadows down and to the left, a lavender sky filling the shade and the
    /// ground bouncing warm light back, a soft light from the camera so the soldiers' sides don't go blue, a thin warm haze
    /// over the far streets; by night a cold moon and a dense teal haze. And the picture's grade after it is lit
    /// (Grade.shader: exposure, Khronos' neutral tone mapping, saturation, contrast, split toning, lifted blacks, vignette,
    /// at Arturo's «vivo» level); the bloom is the default volume's (GradeMake: the day's strength and threshold).
    /// three's lights are divided by π here: its BRDF is albedo/π, URP's albedo.
    /// On «Luz» (made if missing); the fill follows the camera.
    /// </summary>
    public sealed class Mood : MonoBehaviour, IShotStep
    {
        public static Mood Current { get; private set; }

        /// <summary>One of the moods (renderer.ts Mood): colours as three's hex (sRGB), intensities as three's.</summary>
        public sealed class Values
        {
            public int haze;
            public float hazeDensity;
            public int sun;
            public float sunIntensity;
            /// <summary>From the map's middle to the sun, in 0.10.1's frame (x east, y up, z south).</summary>
            public Vector3 sunFrom;
            public int skyLight, groundLight;
            public float skyIntensity;
            public int fill;
            public float fillIntensity;
            public float exposure;
            public float bloomStrength, bloomThreshold;
            public float saturation, contrast, vignette;
            public Vector3 shadowTint, highlightTint, lift;
        }

        public static readonly Values Day = new Values
        {
            haze = 0xd4c8bc, hazeDensity = 0.0032f, sun = 0xffd6b0, sunIntensity = 3.5f, sunFrom = new Vector3(5, 26, -31),
            skyLight = 0xbdb6d8, groundLight = 0x82674f, skyIntensity = 1.75f, fill = 0xfff0e0, fillIntensity = 0.3f, exposure = 1.34f,
            bloomStrength = 0.35f, bloomThreshold = 0.92f, saturation = 0.9f, contrast = 1.04f, vignette = 0.2f,
            shadowTint = new Vector3(1.0f, 0.92f, 1.02f), highlightTint = new Vector3(1.04f, 1.0f, 0.95f), lift = new Vector3(0.075f, 0.055f, 0.07f),
        };

        public static readonly Values Night = new Values
        {
            haze = 0x1b3035, hazeDensity = 0.024f, sun = 0x9cc2e0, sunIntensity = 1.0f, sunFrom = new Vector3(-18, 30, -14),
            skyLight = 0x4a7480, groundLight = 0x161c20, skyIntensity = 1.7f, fill = 0x9fb8d0, fillIntensity = 0.15f, exposure = 1.2f,
            bloomStrength = 0.45f, bloomThreshold = 0.88f, saturation = 1.0f, contrast = 1.06f, vignette = 0.34f,
            shadowTint = new Vector3(0.9f, 1.0f, 1.04f), highlightTint = new Vector3(1.06f, 1.0f, 0.9f), lift = new Vector3(0.015f, 0.03f, 0.035f),
        };

        /// <summary>
        /// How vivid the colours are over each mood's grade (post.ts VIVID_LEVELS; VIVID_DEFAULT 1, Arturo's choice of
        /// 22/10, «bastante más vivo»): saturation, vibrance, contrast, lift, gain.
        /// </summary>
        static readonly float[][] VividLevels = { new[] { 1f, 0, 0, 1, 0 }, new[] { 1.08f, 0.3f, 0.15f, 0.8f, 0.08f }, new[] { 1.16f, 0.6f, 0.3f, 0.6f, 0.16f } };
        const int VividDefault = 1;

        Light sun, fill;
        public Values Now { get; private set; } = Day;

        void OnEnable() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Shader.SetGlobalFloat("_GradeOn", 0);
            ShadowReach(null);
        }

        /// <summary>The pipelines' own shadow distances, while a view has its own (ShadowReach).</summary>
        static readonly Dictionary<UniversalRenderPipelineAsset, float> projectShadows = new Dictionary<UniversalRenderPipelineAsset, float>();

        /// <summary>
        /// How far from the camera the sun's shadows reach (the pipeline's shadow distance, metres), for a view that takes in
        /// more than the battle's: the settlement's clearing whole, as 0.10.1's shadow camera over all of it (clearingArt.ts
        /// aim). Null puts the project's back, as the mood going does.
        /// </summary>
        public static void ShadowReach(float? metres)
        {
            var asset = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (metres == null)
            {
                foreach (var kv in projectShadows) if (kv.Key != null) kv.Key.shadowDistance = kv.Value;
                projectShadows.Clear();
                return;
            }
            if (asset == null) return;
            if (!projectShadows.ContainsKey(asset)) projectShadows[asset] = asset.shadowDistance;
            asset.shadowDistance = metres.Value;
        }

        /// <summary>The light for `state` (by night if its mission is), on «Luz».</summary>
        public static Mood For(GameState state) => Show(state?.Mission?.Night == true ? Night : Day);

        public static Mood Show(Values values)
        {
            var mood = Current != null ? Current : FindFirstObjectByType<Mood>();
            if (mood == null) mood = new GameObject("Luz").AddComponent<Mood>();
            Current = mood;
            mood.Apply(values);
            return mood;
        }

        static Color Hex(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        /// <summary>A colour three gives in linear times k, as Unity's sRGB colour (which it makes linear again).</summary>
        static Color Scaled(int hex, float k)
        {
            var c = Hex(hex).linear * k;
            return new Color(c.r, c.g, c.b).gamma;
        }

        void Apply(Values m)
        {
            Now = m;
            // The sun: the scene's directional light if there is one (the scenes' «Sol»), else its own.
            if (sun == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && l.name != "Relleno") { sun = l; break; }
                if (sun == null)
                {
                    sun = new GameObject("Sol").AddComponent<Light>();
                    sun.type = LightType.Directional;
                    sun.transform.SetParent(transform, false);
                }
            }
            sun.color = Hex(m.sun);
            sun.intensity = m.sunIntensity / Mathf.PI;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1;
            // It shines from sunFrom towards the map's middle (0.10.1's frame: z south, Unity's north).
            sun.transform.rotation = Quaternion.LookRotation(-new Vector3(m.sunFrom.x, m.sunFrom.y, -m.sunFrom.z).normalized, Vector3.up);
            if (fill == null)
            {
                fill = new GameObject("Relleno").AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.shadows = LightShadows.None;
                fill.transform.SetParent(transform, false);
            }
            fill.color = Hex(m.fill);
            fill.intensity = m.fillIntensity / Mathf.PI;
            Follow();
            // The sky's hemisphere light as the ambient: sky above, ground below, their mean round the horizon.
            float k = m.skyIntensity / Mathf.PI;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Scaled(m.skyLight, k);
            RenderSettings.ambientGroundColor = Scaled(m.groundLight, k);
            var equator = (Hex(m.skyLight).linear + Hex(m.groundLight).linear) * 0.5f * k;
            RenderSettings.ambientEquatorColor = new Color(equator.r, equator.g, equator.b).gamma;
            // No reflections of the surroundings: 0.10.1's scene has no environment map (glossy glass reflected Unity's
            // default sky, and the windows went pale where 0.10.1's are dark).
            RenderSettings.reflectionIntensity = 0;
            // The haze (three's FogExp2, its density a tile): Unity's in metres.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Hex(m.haze);
            RenderSettings.fogDensity = m.hazeDensity / Map.MapSpace.Tile;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Hex(m.haze);
            }
            // The grade (Grade.shader), as globals: display values, after the tone mapping.
            var v = VividLevels[VividDefault];
            Shader.SetGlobalFloat("_GradeOn", 1);
            // What glows only at night (the lit windows: Bastion/Building).
            Shader.SetGlobalFloat("_Night", m == Night ? 1 : 0);
            // The light that reaches a surface, as the fog's lamp pools reckon its own colour by (Fog.shader): the sky's
            // hemisphere and the sun (or moon), linear, as URP's.
            Shader.SetGlobalVector("_LightSky", Hex(m.skyLight).linear * k);
            Shader.SetGlobalVector("_LightGround", Hex(m.groundLight).linear * k);
            Shader.SetGlobalVector("_LightSun", Hex(m.sun).linear * (m.sunIntensity / Mathf.PI));
            Shader.SetGlobalVector("_LightSunFrom", new Vector3(m.sunFrom.x, m.sunFrom.y, -m.sunFrom.z).normalized);
            Shader.SetGlobalFloat("_GradeExposure", m.exposure);
            Shader.SetGlobalVector("_GradeLook", new Vector4(m.saturation, m.contrast, m.vignette, 0));
            Shader.SetGlobalVector("_GradeShadowTint", m.shadowTint);
            Shader.SetGlobalVector("_GradeHighlightTint", m.highlightTint);
            Shader.SetGlobalVector("_GradeLift", m.lift);
            Shader.SetGlobalVector("_GradeVivid", new Vector4(v[0], v[1], v[2], v[3]));
            Shader.SetGlobalFloat("_GradeGain", v[4]);
        }

        /// <summary>The fill shines from the camera, the way it looks (renderer.ts: from the camera's place along its view).</summary>
        void Follow()
        {
            var camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (fill != null && camera != null) fill.transform.rotation = camera.transform.rotation;
        }

        void LateUpdate() => Follow();

        public void ShotStep(float dt) => Follow();
    }
}
