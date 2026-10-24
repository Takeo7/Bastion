using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Light = UnityEngine.Light;

namespace Bastion.Client
{
    /// <summary>
    /// The busts (0.10.1's strategy/portraits.ts): each soldier's head and shoulders in their resting pose, turned a little
    /// so it isn't a passport photo, in a soft studio light (a warm key from the front left, a cool rim from behind, a
    /// soft sky; three's ACES at 1.1), rendered once into a texture of its own (160 px, clear round the figure) and kept;
    /// made again when their looks change (Changed). And the radio's voices (Voice). Until one is drawn it is null: UI
    /// shows a silhouette.
    /// Soldiers are known by the campaign (Know); in play mode a bust is made a frame at a time, in edit mode (the labs)
    /// at Flush.
    /// </summary>
    public static class Portraits
    {
        public const int Size = 160;
        /// <summary>A soldier's or a voice's bust was drawn again (its id).</summary>
        public static event Action<string> Changed;

        /// <summary>The layer the studio's figure is drawn on (unused by the map and the figures).</summary>
        const int StudioLayer = 30;
        public static float SoldierTurn = -0.3f;

        /// <summary>Where the studio stands: far under the map, out of every other camera's way.</summary>
        static readonly Vector3 StudioAt = new Vector3(0, -5000, 0);
        const float Tile = Map.MapSpace.Tile;

        sealed class Bust
        {
            public string key;
            public Func<GameObject> make;
            public float turn;
            public bool backlit;
            /// <summary>Its body's clips ("masc", "femn": Anims).</summary>
            public string build = "masc";
            public RenderTexture texture;
            public bool drawn;
        }

        static readonly Dictionary<string, Bust> soldiers = new Dictionary<string, Bust>();
        static readonly Dictionary<string, Bust> voices = new Dictionary<string, Bust>();
        static readonly Queue<(string id, Bust bust)> queue = new Queue<(string, Bust)>();
        static Worker worker;

        /// <summary>The campaign's soldiers (their looks; a bust made for each that changed or is new).</summary>
        public static void Know(CampaignState campaign)
        {
            if (campaign?.Soldiers == null) return;
            foreach (var s in campaign.Soldiers) Know(s.Id, s.Appearance, s.Name);
        }

        /// <summary>
        /// A soldier as they look (`look`; who has none, a body of their sex by their name): dressed as in battle
        /// (Dress.Of, by their id), drawn again if that changed (Personalizar: the draft as it is edited).
        /// </summary>
        public static void Know(string id, Appearance look, string name = null)
        {
            if (string.IsNullOrEmpty(id)) return;
            string sex = look == null || string.IsNullOrEmpty(look.Sex) ? (name != null ? Content.SexOfName(name) : null) : null;
            string key = Newtonsoft.Json.JsonConvert.SerializeObject(new object[] { look, sex });
            if (soldiers.TryGetValue(id, out var bust) && bust.key == key) return;
            if (bust == null) soldiers[id] = bust = new Bust { turn = SoldierTurn };
            bust.key = key;
            var dress = Dress.Of(look, id, sex);
            bust.make = () => SoldierFigure.Make(dress, "Retrato");
            bust.build = dress.sex == "f" ? "femn" : "masc";
            Enqueue(id, bust);
        }

        /// <summary>A soldier's bust (null until it is drawn).</summary>
        public static Texture Soldier(string id) => id != null && soldiers.TryGetValue(id, out var b) && b.drawn ? b.texture : null;

        /// <summary>A radio voice's bust by its speaker (SpeakerIds; null until it is drawn: asking for it starts it).</summary>
        public static Texture Voice(string speaker)
        {
            if (speaker == null) return null;
            if (!voices.TryGetValue(speaker, out var bust))
            {
                var voice = Voices.Of(speaker);
                if (voice == null) return null;
                voices[speaker] = bust = new Bust { key = speaker, make = voice.Make, turn = voice.turn, backlit = voice.backlit, build = voice.female ? "femn" : "masc" };
                Enqueue(speaker, bust);
            }
            return bust.drawn ? bust.texture : null;
        }

        static void Enqueue(string id, Bust bust)
        {
            if (!queue.Any(q => q.bust == bust)) queue.Enqueue((id, bust));
            if (Application.isPlaying && worker == null)
            {
                var o = new GameObject("Retratos") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(o);
                worker = o.AddComponent<Worker>();
            }
        }

        /// <summary>Every bust still to be drawn, now (the labs, in edit mode).</summary>
        public static void Flush()
        {
            while (queue.Count > 0) Next();
        }

        /// <summary>The next bust in the queue, drawn.</summary>
        static void Next()
        {
            var (id, bust) = queue.Dequeue();
            try
            {
                if (bust.texture == null)
                {
                    bust.texture = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = $"Retrato {id}" };
                    bust.texture.Create();
                }
                if (Draw(bust.make, bust.turn, bust.backlit, bust.build, bust.texture))
                {
                    bust.drawn = true;
                    Changed?.Invoke(id);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[portraits] {id}: {e.Message}");
            }
        }

        /// <summary>Takes one bust a frame while the game runs.</summary>
        sealed class Worker : MonoBehaviour
        {
            void Update()
            {
                if (queue.Count > 0) Next();
            }
        }

        // -------------------------------------------------------------------------------------------------------- studio

        public static float HeadTurn = 25, HeadBow = 6;

        /// <summary>
        /// The head a little further round than the body, and bowed (0.10.1's idle turns it on its neck; Unity's humanoid
        /// copy of the clip keeps it over the chest): half on the neck, half on the head, `yaw` being the body's.
        /// </summary>
        static void Look(Animator animator, Transform head, float yaw)
        {
            var round = Quaternion.AngleAxis(HeadTurn * 0.5f, Vector3.up);
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (neck != null) neck.rotation = round * neck.rotation;
            else round *= round;
            head.rotation = round * head.rotation;
            var face = Quaternion.Euler(0, yaw + HeadTurn, 0) * Vector3.forward;
            head.rotation = Quaternion.AngleAxis(HeadBow, Vector3.Cross(Vector3.up, face)) * head.rotation;
        }

        /// <summary>
        /// The figure `make` gives, at rest, turned `turn` (0.10.1's, radians: counter-clockwise from above, so its face to
        /// the left), shot head and shoulders into `target` (portraits.ts bust and shoot: 20° of view, 1.45 tiles off,
        /// looking at its head). Whether there was a figure to shoot.
        /// </summary>
        static bool Draw(Func<GameObject> make, float turn, bool backlit, string build, RenderTexture target)
        {
            var studio = new GameObject("Estudio");
            studio.transform.position = StudioAt;
            var figure = make();
            if (figure == null)
            {
                Destroy(studio);
                return false;
            }
            figure.transform.SetParent(studio.transform, false);
            figure.transform.localPosition = Vector3.zero;
            figure.transform.localScale = Vector3.one * Units.Figures.CharacterScale;
            // three's turn the other way round here (Unity's y turns clockwise from above): its face to the camera's left.
            figure.transform.localRotation = Quaternion.Euler(0, -turn * Mathf.Rad2Deg, 0);
            foreach (var t in figure.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StudioLayer;
            var animator = figure.GetComponentInChildren<Animator>();
            FigureAnimator anim = null;
            if (animator != null && animator.isHuman)
            {
                var catalog = FigureCatalog.Load();
                var idle = catalog?.Clip(Anims.Motion(build, "idle", false));
                // The clips blend in over a few frames (portraits.ts: applyPose to 1.2 s).
                anim = new FigureAnimator(animator, m => m == "idle" ? idle : null);
                for (int k = 0; k < 12; k++) anim.Step(0.1f);
            }
            var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null) Look(animator, head, -turn * Mathf.Rad2Deg);
            var at = head != null ? head.position : figure.transform.position + Vector3.up * 1.6f;
            at += Vector3.up * 0.02f * Tile;
            // The camera in front of it, looking back at its head (three's +z, here −z of the figure's frame turned back).
            var front = studio.transform.forward;
            var cameraObject = new GameObject("Cámara del estudio");
            cameraObject.transform.SetParent(studio.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = at + Vector3.up * 0.03f * Tile + front * 1.45f * Tile;
            camera.transform.LookAt(at);
            camera.fieldOfView = 20;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 15;
            camera.cullingMask = 1 << StudioLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            // Lights: the studio's, from the camera's left, above and in front, and from behind on its right.
            var right = camera.transform.right;
            var up = Vector3.up;
            var toward = (camera.transform.position - at).normalized;
            var lights = new List<Light>();
            Light Directional(int hex, float intensity, Vector3 from)
            {
                var l = new GameObject("Luz").AddComponent<Light>();
                l.transform.SetParent(studio.transform, false);
                l.type = LightType.Directional;
                l.color = Hex(hex);
                l.intensity = intensity / Mathf.PI;
                l.shadows = LightShadows.None;
                l.transform.rotation = Quaternion.LookRotation(-from.normalized);
                lights.Add(l);
                return l;
            }
            Color sky, ground;
            float skyIntensity;
            if (backlit)
            {
                sky = Hex(0x203028);
                ground = Hex(0x050807);
                skyIntensity = 0.35f;
                foreach (float x in new[] { 1.3f, -1.3f }) Directional(0x7dffb0, 3.2f, x * right + 0.8f * up - toward);
            }
            else
            {
                sky = Hex(0xd8e4ff);
                ground = Hex(0x4a3a30);
                skyIntensity = 1.3f;
                Directional(0xffe6c8, 2.4f, -1 * right + 1.2f * up + 1.5f * toward);
                Directional(0xa8c8ff, 1.4f, 1.2f * right + 0.6f * up - 1 * toward);
            }
            var hdr = RenderTexture.GetTemporary(new RenderTextureDescriptor(Size, Size, RenderTextureFormat.ARGBHalf, 24) { msaaSamples = 4 });
            using (new Isolation(lights[0], sky, ground, skyIntensity))
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
            var tone = Resources.Load<Material>("PortraitTone");
            if (tone != null) Graphics.Blit(hdr, target, tone);
            else Graphics.Blit(hdr, target);
            RenderTexture.ReleaseTemporary(hdr);
            anim?.Dispose();
            Destroy(studio);
            return true;
        }

        static Color Hex(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        /// <summary>
        /// While a bust is shot: every other light off, the studio's sky as the ambient (three's hemisphere light: sky above,
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
                    if (l.enabled && l.gameObject.layer != StudioLayer && l.transform.parent?.name != "Estudio")
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

        static void Destroy(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
