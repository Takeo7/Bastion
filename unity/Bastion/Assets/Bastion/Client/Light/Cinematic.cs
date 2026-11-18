using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion.Client
{
    /// <summary>
    /// The cinematic look (T7; Arturo's choice of 18/11 in the cuadro, the three together, «montar lo elegido»; the looks
    /// tried in Editor/CineLab): A1 «Fílmica suave», the film's tone curve half over the game's (Grade.shader's _CineCurve),
    /// a little darker, a little more contrast, a little more bloom; C1 «Bruma de distancia», the haze thicker and warmer by
    /// day (by night as it was: thicker, its teal turned green and hid what the squad plays in, Producto's two cares); B1
    /// «Fondo desenfocado», URP's Gaussian depth of field from a little behind where the camera looks (BlurFrom) to
    /// further back (BlurTo), not at «Bajo» nor on iOS (its cost, +0.3 ms in «medio» on the M2 Pro). Mood applies the first
    /// two with its light, and follows the camera with the third (Follow). On unless Ajustes says otherwise (UI's
    /// GameSettings sets On); BASTION_SIN_CINE=1, off from the start (the labs' «before»).
    /// URP keeps the Gaussian depth of field's shader in a build only while a volume profile in Assets has a
    /// DepthOfField (ShaderBuildPreprocessor): Settings/DefaultVolumeProfile.asset has one (its mode off).
    /// </summary>
    public static class Cinematic
    {
        /// <summary>Whether the look is on; changed, the mood shown now is applied again with it or without it.</summary>
        public static bool On
        {
            get => on;
            set
            {
                if (on == value) return;
                on = value;
                Mood.Current?.Reapply();
            }
        }

        static bool on = Environment.GetEnvironmentVariable("BASTION_SIN_CINE") != "1";

        // ------------------------------------------------------------ A1, the grade (Grade.shader, as Mood sets it)

        /// <summary>The film curve's share over the game's Neutral (Grade.shader _CineCurve); exposure and contrast, times.</summary>
        const float Curve = 0.6f, Exposure = 0.92f, Contrast = 1.06f;
        /// <summary>The bloom's intensity added to the pipeline's volume's.</summary>
        const float BloomMore = 0.15f;

        /// <summary>Mood's exposure and contrast as the look has them.</summary>
        public static float ExposureOf(float exposure) => on ? exposure * Exposure : exposure;
        public static float ContrastOf(float contrast) => on ? contrast * Contrast : contrast;
        public static float CurveNow => on ? Curve : 0;

        // ------------------------------------------------------------ C1, the haze (RenderSettings' fog, as Mood sets it)

        /// <summary>By day: the haze's density times, its colour times per channel. By night as it was.</summary>
        const float HazeMore = 2.4f;
        static readonly Vector3 HazeTint = new Vector3(1.05f, 1f, 0.92f);

        public static float HazeOf(float density, bool night) => on && !night ? density * HazeMore : density;

        public static Color HazeColourOf(Color colour, bool night) =>
            on && !night ? new Color(colour.r * HazeTint.x, colour.g * HazeTint.y, colour.b * HazeTint.z, colour.a) : colour;

        // ------------------------------------------------------------ B1, the background's blur (and A1's bloom)

        /// <summary>Where the blur begins and is whole, metres behind where the camera looks; its most radius (pixels at 1080).</summary>
        const float BlurFrom = 4, BlurTo = 22, BlurRadius = 1.5f;

        /// <summary>Whether the blur is drawn at the quality now: not at «Bajo», nor on iOS.</summary>
        public static bool BlurAllowed
        {
            get
            {
                if (Application.platform == RuntimePlatform.IPhonePlayer) return false;
                int level = QualitySettings.GetQualityLevel();
                var names = QualitySettings.names;
                return level < 0 || level >= names.Length || names[level] != "Bajo";
            }
        }

        static Volume volume;
        static Bloom bloom;
        static DepthOfField depth;

        /// <summary>
        /// Before each frame (Mood.Follow): the look's own volume (bloom over the pipeline's, the blur on where `camera`
        /// looks), made the first time; off with the look.
        /// </summary>
        public static void Follow(Camera camera)
        {
            if (!on)
            {
                if (volume != null) volume.enabled = false;
                return;
            }
            if (volume == null)
            {
                var o = new GameObject("Cine") { hideFlags = HideFlags.DontSave };
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(o);
                volume = o.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100;
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.profile.hideFlags = HideFlags.DontSave;
                bloom = volume.profile.Add<Bloom>();
                depth = volume.profile.Add<DepthOfField>();
                depth.mode.Override(DepthOfFieldMode.Gaussian);
                depth.gaussianMaxRadius.Override(BlurRadius);
                depth.highQualitySampling.Override(false);
            }
            volume.enabled = true;
            // (The pipeline's bloom, read each time: «Gráficos» changes the asset.)
            Bloom own = null;
            (QualitySettings.renderPipeline as UniversalRenderPipelineAsset)?.volumeProfile?.TryGet(out own);
            bloom.intensity.Override((own != null ? own.intensity.value : 0.35f) + BloomMore);
            bool blur = BlurAllowed && camera != null;
            depth.active = blur;
            if (!blur) return;
            float focus = Focus(camera);
            depth.gaussianStart.Override(focus + BlurFrom);
            depth.gaussianEnd.Override(focus + BlurTo);
        }

        /// <summary>How far from the camera the ground is where it looks (metres); level or up, 30.</summary>
        static float Focus(Camera camera)
        {
            var forward = camera.transform.forward;
            return forward.y < -0.05f ? Mathf.Clamp(-camera.transform.position.y / forward.y, 1, 400) : 30;
        }
    }
}
