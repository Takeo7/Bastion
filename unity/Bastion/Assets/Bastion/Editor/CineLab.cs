using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.EditorTools
{
    /// <summary>
    /// T7 (Arturo, 18/11: «quiero que tenga un aspecto visual más cinematográfico, con respecto a postprocesado, shaders
    /// etc… dame 5 opciones y variaciones»): five ways, three looks each, laid over the game's own as it is (Mood's light,
    /// Grade's grading, the volume's bloom), for photos and their cost; nothing of it in the game.
    ///   A, película de acción: a filmic tone curve (ACES over the Neutral), contrast and bloom; soft, teal and orange,
    ///      bleach bypass.
    ///   B, foco de cámara: URP's bokeh depth of field on where the camera looks; the background soft, a miniature
    ///      (tilt-shift), the cinema's bars.
    ///   C, atmósfera y luz: the haze thicker with distance, light shafts from the sun, haloes round the lights by night.
    ///   D, película analógica: grain, a colour fringe at the edges, halation (a red bloom); 35 mm, 16 mm, documentary.
    ///   E, claroscuro: the light itself; deep shadows, the golden hour, a blue night.
    /// How: before each frame (RenderPipelineManager.beginContextRendering) the look asked for over what Mood last set
    /// (taken again whenever Mood changes it): a global volume of its own (bloom, depth of field), Grade's _Cine globals
    /// and its grading's (Grade.shader), the sun, the ambient probe and the haze. BASTION_CINE=id (one look; 0 the game's
    /// as it is), or ids by commas: in play mode, each BASTION_CINE_CADA seconds (0.3) from BASTION_CINE_DESDE (the
    /// capture's warm-up, 0), in turn, its changes in the log ([cine]). RendimientoLab.Medir's «cine-id» variants and the
    /// edit-mode labs set Fixed.
    /// </summary>
    [InitializeOnLoad]
    public static class CineLab
    {
        public sealed class Look
        {
            public string id, name;
            // Grade: the filmic curve's share, exposure (times), saturation and contrast (times), the shade's and the light's
            // tints (times, per channel), the lift (added), grain, fringe, bars.
            public float curve, exposure = 1, saturation = 1, contrast = 1, grain, aberration, bars;
            public Vector3 shadowTint = Vector3.one, highlightTint = Vector3.one, lift = Vector3.zero;
            // Light shafts: strength, threshold, decay, length; their tint.
            public Vector4 rays;
            public Color raysTint = new Color(1f, 0.85f, 0.65f);
            // URP's bloom over the volume's (intensity added, threshold and scatter if set, tint), depth of field (aperture
            // f-number, focal length in mm; 0 none).
            public float bloom, bloomThreshold, bloomScatter;
            public Color bloomTint = Color.white;
            public float aperture, focal;
            // Light: the sun's intensity (times) and colour (times), its height (degrees; 0 as it is), the ambient (times,
            // per channel), the haze's density (times) and colour (times).
            public float sun = 1, sunHeight;
            public Vector3 sunColour = Vector3.one, ambient = Vector3.one, hazeColour = Vector3.one;
            public float haze = 1;
        }

        public static readonly Look[] Looks =
        {
            new Look { id = "0", name = "Como hoy" },
            new Look { id = "A1", name = "Fílmica suave", curve = 0.6f, exposure = 0.92f, contrast = 1.06f, bloom = 0.15f },
            new Look { id = "A2", name = "Naranja y azul", curve = 0.8f, exposure = 0.9f, contrast = 1.12f, saturation = 1.05f, shadowTint = new Vector3(0.86f, 0.98f, 1.1f), highlightTint = new Vector3(1.08f, 1f, 0.86f), bloom = 0.15f },
            new Look { id = "A3", name = "Blanqueado", curve = 0.8f, exposure = 0.95f, contrast = 1.25f, saturation = 0.55f, highlightTint = new Vector3(1f, 1.01f, 1.04f), lift = new Vector3(-0.015f, -0.015f, -0.01f) },
            new Look { id = "B1", name = "Fondo desenfocado", aperture = 4f, focal = 70 },
            new Look { id = "B2", name = "Miniatura", aperture = 1.4f, focal = 135, saturation = 1.1f, contrast = 1.05f },
            new Look { id = "B3", name = "Bandas de cine", bars = 0.744f, aperture = 5.6f, focal = 60, curve = 0.4f },
            new Look { id = "C1", name = "Bruma de distancia", haze = 2.4f, hazeColour = new Vector3(1.05f, 1f, 0.92f), contrast = 0.97f },
            new Look { id = "C2", name = "Rayos de sol", rays = new Vector4(1.6f, 0.7f, 0.95f, 0.85f), bloom = 0.15f, curve = 0.4f },
            new Look { id = "C3", name = "Halos", bloom = 0.9f, bloomThreshold = 0.6f, bloomScatter = 0.78f, bloomTint = new Color(1f, 0.86f, 0.7f) },
            new Look { id = "D1", name = "35 mm", grain = 0.035f, curve = 0.4f, aberration = 0.004f },
            new Look { id = "D2", name = "16 mm", grain = 0.085f, aberration = 0.014f, saturation = 0.88f, bloom = 0.5f, bloomThreshold = 0.7f, bloomTint = new Color(1f, 0.45f, 0.35f), curve = 0.5f },
            new Look { id = "D3", name = "Documental", grain = 0.055f, saturation = 0.68f, contrast = 1.05f, shadowTint = new Vector3(0.95f, 0.99f, 1.05f), curve = 0.5f },
            new Look { id = "E1", name = "Sombras profundas", ambient = new Vector3(0.55f, 0.55f, 0.6f), sun = 1.25f, contrast = 1.08f },
            new Look { id = "E2", name = "Hora dorada", sunHeight = 16, sunColour = new Vector3(1f, 0.78f, 0.52f), sun = 1.15f, ambient = new Vector3(0.9f, 0.8f, 0.72f), highlightTint = new Vector3(1.05f, 1f, 0.9f), hazeColour = new Vector3(1.1f, 0.92f, 0.75f) },
            new Look { id = "E3", name = "Noche azul", ambient = new Vector3(0.62f, 0.72f, 1.05f), sunColour = new Vector3(0.75f, 0.85f, 1.15f), sun = 1.3f, saturation = 0.82f, shadowTint = new Vector3(0.9f, 0.96f, 1.1f), curve = 0.3f },
        };

        /// <summary>A look set by a lab (RendimientoLab.Medir, an edit-mode capture), over BASTION_CINE; null, none.</summary>
        public static string Fixed;

        static readonly string[] Asked = (Environment.GetEnvironmentVariable("BASTION_CINE") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        static readonly float Every = Number("BASTION_CINE_CADA", 0.3f), From = Number("BASTION_CINE_DESDE", 0);

        static CineLab()
        {
            RenderPipelineManager.beginContextRendering += Before;
        }

        static float Number(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : or;

        static string shown;

        static void Before(ScriptableRenderContext context, List<Camera> cameras)
        {
            string id = Fixed ?? (Asked.Length == 1 ? Asked[0] : Asked.Length > 1 && Application.isPlaying ? Asked[Mathf.Clamp(Mathf.FloorToInt((Time.time - From) / Every), 0, Asked.Length - 1)] : null);
            if (id == null) return;
            if (id != shown)
            {
                shown = id;
                Debug.Log(FormattableString.Invariant($"[cine] {id} from frame {Time.frameCount}, {Time.time:0.00} s"));
            }
            Apply(Looks.FirstOrDefault(l => l.id == id) ?? Looks[0], cameras.FirstOrDefault(c => c != null && c.cameraType == CameraType.Game) ?? Camera.main);
        }

        // ------------------------------------------------------------ what Mood set, and what was written over it

        static Volume volume;
        static Bloom bloom;
        static DepthOfField depth;
        static readonly Dictionary<string, Vector4> written = new Dictionary<string, Vector4>(), seen = new Dictionary<string, Vector4>();
        static readonly string[] Globals = { "_GradeExposure", "_GradeLook", "_GradeShadowTint", "_GradeHighlightTint", "_GradeLift" };
        static Light sunWas;
        static float sunIntensity, writtenIntensity = float.NaN;
        static Color sunColour, writtenColour;
        static Quaternion sunTurn, writtenTurn;
        static SphericalHarmonicsL2 probe, writtenProbe;
        static bool probeTaken;
        static float fogDensity, writtenFog = float.NaN;
        static Color fogColour, writtenFogColour;

        /// <summary>`look` over the game's own, now (and before each frame while asked: Before).</summary>
        public static void Apply(Look look, Camera camera)
        {
            Grade(look);
            Post(look, camera);
            Lighting(look);
            Rays(look, camera);
        }

        static Vector4 Base(string name)
        {
            var now = name == "_GradeExposure" ? new Vector4(Shader.GetGlobalFloat(name), 0, 0, 0) : Shader.GetGlobalVector(name);
            // (Taken again whenever it isn't what was last written over it: Mood set it since.)
            if (!written.TryGetValue(name, out var w) || (now - w).sqrMagnitude > 1e-10f) seen[name] = now;
            return seen[name];
        }

        static void Write(string name, Vector4 v)
        {
            written[name] = v;
            if (name == "_GradeExposure") Shader.SetGlobalFloat(name, v.x);
            else Shader.SetGlobalVector(name, v);
        }

        static void Grade(Look look)
        {
            var exposure = Base("_GradeExposure");
            var grade = Base("_GradeLook");
            var shadow = Base("_GradeShadowTint");
            var light = Base("_GradeHighlightTint");
            var lift = Base("_GradeLift");
            Write("_GradeExposure", new Vector4(exposure.x * look.exposure, 0, 0, 0));
            Write("_GradeLook", new Vector4(grade.x * look.saturation, grade.y * look.contrast, grade.z, grade.w));
            Write("_GradeShadowTint", new Vector4(shadow.x * look.shadowTint.x, shadow.y * look.shadowTint.y, shadow.z * look.shadowTint.z, shadow.w));
            Write("_GradeHighlightTint", new Vector4(light.x * look.highlightTint.x, light.y * look.highlightTint.y, light.z * look.highlightTint.z, light.w));
            Write("_GradeLift", new Vector4(lift.x + look.lift.x, lift.y + look.lift.y, lift.z + look.lift.z, lift.w));
            Shader.SetGlobalFloat("_CineCurve", look.curve);
            Shader.SetGlobalFloat("_CineGrain", look.grain);
            Shader.SetGlobalFloat("_CineAberration", look.aberration);
            Shader.SetGlobalFloat("_CineBars", look.bars);
        }

        static void Post(Look look, Camera camera)
        {
            if (volume == null)
            {
                var o = new GameObject("CineLab") { hideFlags = HideFlags.DontSave };
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(o);
                volume = o.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1000;
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                bloom = volume.profile.Add<Bloom>(true);
                depth = volume.profile.Add<DepthOfField>(true);
            }
            // The bloom as the pipeline's own volume has it, with the look's over it.
            Bloom own = null;
            (QualitySettings.renderPipeline is UniversalRenderPipelineAsset urp ? new SerializedObject(urp).FindProperty("m_VolumeProfile").objectReferenceValue as VolumeProfile : null)?.TryGet(out own);
            float intensity = own != null ? own.intensity.value : 0, threshold = own != null ? own.threshold.value : 0.9f, scatter = own != null ? own.scatter.value : 0.7f;
            bloom.active = look.bloom > 0 || look.bloomThreshold > 0;
            bloom.intensity.Override(intensity + look.bloom);
            bloom.threshold.Override(look.bloomThreshold > 0 ? look.bloomThreshold : threshold);
            bloom.scatter.Override(look.bloomScatter > 0 ? look.bloomScatter : scatter);
            bloom.tint.Override(look.bloomTint);
            depth.active = look.aperture > 0;
            depth.mode.Override(DepthOfFieldMode.Bokeh);
            depth.focusDistance.Override(Focus(camera));
            depth.aperture.Override(Mathf.Max(1, look.aperture));
            depth.focalLength.Override(Mathf.Max(1, look.focal));
            var stack = VolumeManager.instance.stack?.GetComponent<DepthOfField>();
            var data = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
            if (look.aperture > 0 && Time.frameCount % 20 == 0) Debug.Log(FormattableString.Invariant($"[cine-dbg] dof own active {depth.active} mode {depth.mode.value}; stack {(stack != null ? stack.IsActive() + " " + stack.mode.value + " f " + stack.focusDistance.value + " ap " + stack.aperture.value : "none")}; camera {(camera != null ? camera.name : "none")} post {data?.renderPostProcessing} layer {data?.volumeLayerMask.value} trigger {data?.volumeTrigger}"));
        }

        /// <summary>How far from the camera the ground is where it looks (metres): the depth of field's focus.</summary>
        static float Focus(Camera camera)
        {
            if (camera == null) return 30;
            var forward = camera.transform.forward;
            return forward.y < -0.05f ? Mathf.Clamp(-camera.transform.position.y / forward.y, 1, 300) : 30;
        }

        static void Lighting(Look look)
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                if (sun != sunWas || !Mathf.Approximately(sun.intensity, writtenIntensity) || sun.color != writtenColour || sun.transform.rotation != writtenTurn)
                {
                    sunWas = sun;
                    sunIntensity = sun.intensity;
                    sunColour = sun.color;
                    sunTurn = sun.transform.rotation;
                }
                sun.intensity = writtenIntensity = sunIntensity * look.sun;
                sun.color = writtenColour = new Color(sunColour.r * look.sunColour.x, sunColour.g * look.sunColour.y, sunColour.b * look.sunColour.z);
                // (Lower in the sky by the look's height, its way round kept.)
                var turn = sunTurn;
                if (look.sunHeight > 0)
                {
                    var e = sunTurn.eulerAngles;
                    turn = Quaternion.Euler(look.sunHeight, e.y, e.z);
                }
                sun.transform.rotation = writtenTurn = turn;
            }
            var now = RenderSettings.ambientProbe;
            if (!probeTaken || !Same(now, writtenProbe))
            {
                probe = now;
                probeTaken = true;
            }
            var lit = probe;
            for (int c = 0; c < 3; c++)
                for (int k = 0; k < 9; k++) lit[c, k] = probe[c, k] * look.ambient[c];
            RenderSettings.ambientProbe = writtenProbe = lit;
            if (!Mathf.Approximately(RenderSettings.fogDensity, writtenFog) || RenderSettings.fogColor != writtenFogColour)
            {
                fogDensity = RenderSettings.fogDensity;
                fogColour = RenderSettings.fogColor;
            }
            RenderSettings.fogDensity = writtenFog = fogDensity * look.haze;
            RenderSettings.fogColor = writtenFogColour = new Color(fogColour.r * look.hazeColour.x, fogColour.g * look.hazeColour.y, fogColour.b * look.hazeColour.z);
        }

        static bool Same(SphericalHarmonicsL2 a, SphericalHarmonicsL2 b)
        {
            for (int c = 0; c < 3; c++)
                for (int k = 0; k < 9; k++)
                    if (Mathf.Abs(a[c, k] - b[c, k]) > 1e-6f) return false;
            return true;
        }

        /// <summary>The light shafts' globals: the sun's place on the screen, from where the camera is, towards the sun.</summary>
        static void Rays(Look look, Camera camera)
        {
            Shader.SetGlobalVector("_CineRays", look.rays);
            Shader.SetGlobalVector("_CineRaysTint", look.raysTint);
            var sun = RenderSettings.sun;
            if (look.rays.x <= 0 || sun == null || camera == null)
            {
                Shader.SetGlobalVector("_CineSun", Vector4.zero);
                return;
            }
            var at = camera.WorldToViewportPoint(camera.transform.position - sun.transform.forward * 1000);
            Shader.SetGlobalVector("_CineSun", new Vector4(at.x, at.y, at.z > 0 ? 1 : 0, 0));
        }
    }
}
