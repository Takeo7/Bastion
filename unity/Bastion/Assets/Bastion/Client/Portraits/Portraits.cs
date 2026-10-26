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
        /// <summary>How long the last bust took to make and draw (ms; one a frame while the game runs).</summary>
        public static float LastDrawMs { get; private set; }

        /// <summary>A soldier's or a voice's bust was drawn again (its id).</summary>
        public static event Action<string> Changed;

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
            /// <summary>What a soldier's was known as (to paint it again when the tint changes).</summary>
            public Appearance look;
            public string name, band;
        }

        static bool listening;

        static readonly Dictionary<string, Bust> soldiers = new Dictionary<string, Bust>();
        static readonly Dictionary<string, Bust> voices = new Dictionary<string, Bust>();
        static readonly Queue<(string id, Bust bust)> queue = new Queue<(string, Bust)>();
        static Worker worker;

        /// <summary>The campaign's soldiers (their looks; a bust made for each that changed or is new).</summary>
        public static void Know(CampaignState campaign)
        {
            if (campaign?.Soldiers == null) return;
            // Their clothes, by the order they joined, unless they chose (wardrobe.ts setRoster).
            Dress.Roster(campaign.Soldiers);
            foreach (var s in campaign.Soldiers) Know(s.Id, s.Appearance, s.Name, SquadTint.ColourOf(s.Owner));
        }

        /// <summary>
        /// A soldier as they look (`look`; who has none, a body of their sex by their name): dressed as in battle
        /// (Dress.Of, by their id), drawn again if that changed (Personalizar: the draft as it is edited).
        /// </summary>
        public static void Know(string id, Appearance look, string name = null, string band = null)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!listening)
            {
                listening = true;
                // Tinted or not, every bust again (SquadTint: their clothes in their colours, their band).
                SquadTint.Changed += _ =>
                {
                    foreach (var kv in soldiers.ToList()) Know(kv.Key, kv.Value.look, kv.Value.name, kv.Value.band);
                };
            }
            string sex = look == null || string.IsNullOrEmpty(look.Sex) ? (name != null ? Content.SexOfName(name) : null) : null;
            // The same bust while the same figure (Dress.Key: the roster's outfit too) in the same paint.
            var dress = Dress.Of(look, id, sex);
            string key = dress.Key + "|" + (SquadTint.On ? Newtonsoft.Json.JsonConvert.SerializeObject(new object[] { look, band }) : "plain");
            if (soldiers.TryGetValue(id, out var bust) && bust.key == key) return;
            if (bust == null) soldiers[id] = bust = new Bust { turn = -0.3f };
            bust.key = key;
            bust.look = look;
            bust.name = name;
            bust.band = band;
            // Its parts apart, as Soldados' soldier and Personalizar's previews: a quarter of the time, and the same figure.
            bust.make = () =>
            {
                var figure = SoldierFigure.Make(dress, "Retrato", true);
                SquadTint.Paint(figure, look, band);
                return figure;
            };
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
                var clock = System.Diagnostics.Stopwatch.StartNew();
                bool drawn = Draw(bust.make, bust.turn, bust.backlit, bust.build, bust.texture);
                LastDrawMs = (float)clock.Elapsed.TotalMilliseconds;
                if (drawn)
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

        /// <summary>How much further round than the body its head looks, and how far it bows (degrees; by eye, beside 0.10.1).</summary>
        const float HeadTurn = 20, HeadBow = 8;

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
            studio.transform.position = Studio.At;
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
            foreach (var t in figure.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Studio.Layer;
            var animator = figure.GetComponentInChildren<Animator>();
            FigureAnimator anim = null;
            if (animator != null && animator.isHuman)
            {
                // Posed though no camera sees it yet (POLYGON's prefabs cull their bones' updates when unseen).
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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
            // Lights: the studio's, from the camera's left, above and in front, and from behind on its right.
            var right = camera.transform.right;
            var up = Vector3.up;
            var toward = (camera.transform.position - at).normalized;
            if (backlit)
                Studio.Shoot(camera, target, new[] { new Studio.Lamp(0x7dffb0, 3.2f, 1.3f * right + 0.8f * up - toward), new Studio.Lamp(0x7dffb0, 3.2f, -1.3f * right + 0.8f * up - toward) },
                    Studio.Hex(0x203028), Studio.Hex(0x050807), 0.35f);
            else
                Studio.Shoot(camera, target, new[] { new Studio.Lamp(0xffe6c8, 2.4f, -1 * right + 1.2f * up + 1.5f * toward), new Studio.Lamp(0xa8c8ff, 1.4f, 1.2f * right + 0.6f * up - 1 * toward) },
                    Studio.Hex(0xd8e4ff), Studio.Hex(0x4a3a30), 1.3f);
            anim?.Dispose();
            Destroy(studio);
            return true;
        }

        static void Destroy(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
