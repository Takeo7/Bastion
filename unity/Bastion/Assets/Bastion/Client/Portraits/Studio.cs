using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Light = UnityEngine.Light;

namespace Bastion.Client
{
    /// <summary>
    /// The studio the game's pictures are shot in (0.10.1's offscreen renderers: portraits.ts, pieceIcons.ts,
    /// lab/renders/iconRenders.ts): what is on its layer alone, lit only by its own lamps and a hemisphere sky (three's
    /// HemisphereLight), on nothing (clear), toned as three tones (ACES Filmic or Khronos Neutral, at an exposure) into a
    /// texture with its alpha straight. Everything the scene had is as it was after.
    /// </summary>
    public static class Studio
    {
        /// <summary>The layer what is shot is put on (unused by the map and the figures).</summary>
        public const int Layer = 30;
        /// <summary>Where a studio may stand: far under the map, out of every other camera's way.</summary>
        public static readonly Vector3 At = new Vector3(0, -5000, 0);
        const string Lights = "Luces del estudio";

        /// <summary>A directional lamp: its colour, three's intensity, where it shines from (Unity's world).</summary>
        public readonly struct Lamp
        {
            public readonly int hex;
            public readonly float intensity;
            public readonly Vector3 from;
            public Lamp(int hex, float intensity, Vector3 from)
            {
                this.hex = hex;
                this.intensity = intensity;
                this.from = from;
            }
        }

        /// <summary>three's tone mappings.</summary>
        public enum Tone { AcesFilmic = 0, Neutral = 1 }

        static Material tone;

        /// <summary>The tone's material (Resources/PortraitTone, made by PortraitLab.EnsureTone), a copy of its own: its settings change per shot.</summary>
        static Material ToneMaterial()
        {
            if (tone != null) return tone;
            var asset = Resources.Load<Material>("PortraitTone");
            return tone = asset != null ? new Material(asset) { hideFlags = HideFlags.HideAndDontSave } : null;
        }

        public static Color Hex(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        /// <summary>
        /// What `camera` sees on the studio's layer, shot into `target`: lit by `lamps` and a sky of `sky` over `ground` at
        /// `hemi` (three's intensities: over π here, as URP's lights have no 1/π), toned with `tone` at `exposure`,
        /// multisampled. The camera is set up for it (clear, HDR, no post nor shadows) and left disabled.
        /// </summary>
        public static void Shoot(Camera camera, RenderTexture target, IReadOnlyList<Lamp> lamps, Color sky, Color ground, float hemi, Tone tone = Tone.AcesFilmic, float exposure = 1.1f)
        {
            camera.enabled = false;
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            var holder = new GameObject(Lights);
            var lights = new List<Light>();
            foreach (var lamp in lamps)
            {
                var l = new GameObject("Luz").AddComponent<Light>();
                l.transform.SetParent(holder.transform, false);
                l.type = LightType.Directional;
                l.color = Hex(lamp.hex);
                l.intensity = lamp.intensity / Mathf.PI;
                l.shadows = LightShadows.None;
                l.transform.rotation = Quaternion.LookRotation(-lamp.from.normalized);
                lights.Add(l);
            }
            var hdr = RenderTexture.GetTemporary(new RenderTextureDescriptor(target.width, target.height, RenderTextureFormat.ARGBHalf, 24) { msaaSamples = 4 });
            try
            {
                using (new Isolation(lights.Count > 0 ? lights[0] : null, sky, ground, hemi))
                {
                    var request = new RenderPipeline.StandardRequest { destination = hdr };
                    if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                    else
                    {
                        camera.targetTexture = hdr;
                        camera.Render();
                        camera.targetTexture = null;
                    }
                }
                var material = ToneMaterial();
                if (material != null)
                {
                    material.SetFloat("_Exposure", exposure);
                    material.SetFloat("_Mode", (float)tone);
                    Graphics.Blit(hdr, target, material);
                }
                else Graphics.Blit(hdr, target);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(hdr);
                if (Application.isPlaying) UnityEngine.Object.Destroy(holder);
                else UnityEngine.Object.DestroyImmediate(holder);
            }
        }


        /// <summary>
        /// While a picture is shot: every other light off, the studio's sky as the ambient (three's hemisphere light: sky above,
        /// ground below), no haze, and the renderers' passes off (the fog of war, the grade, the snow, the occlusion);
        /// all of it as it was after.
        /// </summary>
        sealed class Isolation : IDisposable
        {
            readonly List<Light> off = new List<Light>();
            readonly List<ScriptableRendererFeature> features = new List<ScriptableRendererFeature>();
            readonly SphericalHarmonicsL2 probe;
            readonly bool fog;
            readonly Light sun;
            readonly DefaultReflectionMode reflection;
            readonly Texture cube;
            readonly float shine;
            static Cubemap dark;

            /// <summary>How much a directional light of 1 added to Unity's harmonics gives along its own axis, over its linear band.</summary>
            static float Lobe()
            {
                var one = new SphericalHarmonicsL2();
                one.AddDirectionalLight(Vector3.up, Color.white, 1);
                var at = new Color[2];
                one.Evaluate(new[] { Vector3.up, Vector3.down }, at);
                return Mathf.Max(1e-3f, (at[0].r - at[1].r) * 0.5f);
            }

            static Cubemap Dark()
            {
                if (dark != null) return dark;
                dark = new Cubemap(4, TextureFormat.RGBA32, false) { name = "Estudio sin cielo", hideFlags = HideFlags.HideAndDontSave };
                var black = Enumerable.Repeat(Color.clear, 16).ToArray();
                foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
                    dark.SetPixels(black, face);
                dark.Apply();
                return dark;
            }

            public Isolation(Light key, Color sky, Color ground, float intensity)
            {
                foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.enabled && l.gameObject.layer != Layer && l.transform.parent?.name != Lights)
                    {
                        l.enabled = false;
                        off.Add(l);
                    }
                sun = RenderSettings.sun;
                RenderSettings.sun = key;
                probe = RenderSettings.ambientProbe;
                // three's hemisphere: lerp(ground, sky, (n.y + 1) / 2): its middle all round, and half their difference along up.
                var s = sky.linear * (intensity / Mathf.PI);
                var g = ground.linear * (intensity / Mathf.PI);
                var sh = new SphericalHarmonicsL2();
                sh.AddAmbientLight((s + g) * 0.5f);
                float lobe = Lobe();
                sh.AddDirectionalLight(Vector3.up, (s - g) * (0.5f / lobe), 1);
                sh.AddDirectionalLight(Vector3.down, (s - g) * (-0.5f / lobe), 1);
                RenderSettings.ambientProbe = sh;
                // three's scene has no environment: nothing of the sky in the shine either.
                reflection = RenderSettings.defaultReflectionMode;
                cube = RenderSettings.customReflectionTexture;
                shine = RenderSettings.reflectionIntensity;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = Dark();
                RenderSettings.reflectionIntensity = 0;
                fog = RenderSettings.fog;
                RenderSettings.fog = false;
                if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset urp || GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                {
                    var asset = (UniversalRenderPipelineAsset)(QualitySettings.renderPipeline ?? GraphicsSettings.currentRenderPipeline);
                    foreach (var data in asset.rendererDataList)
                        if (data != null)
                            foreach (var f in data.rendererFeatures)
                                if (f != null && f.isActive)
                                {
                                    f.SetActive(false);
                                    features.Add(f);
                                }
                }
            }

            public void Dispose()
            {
                foreach (var l in off) if (l != null) l.enabled = true;
                RenderSettings.sun = sun;
                RenderSettings.ambientProbe = probe;
                RenderSettings.defaultReflectionMode = reflection;
                RenderSettings.customReflectionTexture = cube;
                RenderSettings.reflectionIntensity = shine;
                RenderSettings.fog = fog;
                foreach (var f in features) if (f != null) f.SetActive(true);
            }
        }

    }
}
