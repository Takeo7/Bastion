using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The fog of war of the map in view (0.10.1's fogOfWar.ts), in three zones: in sight (where enemies show:
    /// TeamVisibility), as it is; seen (as far as the squad's line of sight goes, TeamView, and wherever it has seen this
    /// mission), under a haze and a low mist; never seen, a dark mist. Each tile's zone in a small texture, faded as it
    /// changes, which a full-screen pass (Bastion/Fog) reads at every pixel's place. UI's event player shows it the state
    /// seen (Show) after each event; Battlefield starts it with each map. On «Niebla» in Partida.
    /// </summary>
    public sealed class Fog : MonoBehaviour, IShotStep
    {
        public static Fog Current { get; private set; }

        /// <summary>Seconds a tile takes to come into sight or view or leave them (SIGHT_FADE).</summary>
        const float SightFade = 0.35f;
        /// <summary>fogOfWar.ts UNKNOWN, HAZE, MIST: the dark mist, the haze over what is seen, the low mist's tint.</summary>
        static readonly Color Unknown = Hex(0x161e28), Haze = Hex(0xa9b8c8), MistTint = Hex(0x2e3640);
        /// <summary>How thick the low mist is at most (MIST_LEVELS: 0, 0.35, 0.6; Arturo chose it dense).</summary>
        public float mist = 0.6f;

        static readonly int FogMap = Shader.PropertyToID("_FogMap"), FogSize = Shader.PropertyToID("_FogSize"), FogUnknown = Shader.PropertyToID("_FogUnknown"),
            FogHaze = Shader.PropertyToID("_FogHaze"), FogMistTint = Shader.PropertyToID("_FogMistTint"), FogMist = Shader.PropertyToID("_FogMist"),
            FogTime = Shader.PropertyToID("_FogTime");

        Texture2D map;
        Color32[] pixels;
        float[] sight, known, sightTo, knownTo;
        bool[] explored;
        int width, height;
        float time;
        bool changed;

        void Awake() => Current = this;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            Shader.SetGlobalVector(FogSize, Vector4.zero);
            if (Current == this) Current = null;
        }

        /// <summary>A new map: nothing seen yet; its tiles' floors for the low mist.</summary>
        public void Reset(GameState s)
        {
            width = s.Width;
            height = s.Height;
            int n = width * height;
            sight = new float[n];
            known = new float[n];
            sightTo = new float[n];
            knownTo = new float[n];
            explored = new bool[n];
            pixels = new Color32[n];
            if (map != null)
            {
                if (Application.isPlaying) Destroy(map);
                else DestroyImmediate(map);
            }
            map = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { name = "Niebla", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < n; i++)
            {
                float floor = MapSpace.FloorAt(s, new Pos { X = i % width, Y = i / width }) / MapSpace.Storey;
                pixels[i] = new Color32(0, 0, 0, (byte)Mathf.Clamp(Mathf.RoundToInt(floor / 8 * 255), 0, 255));
            }
            changed = true;
            Shader.SetGlobalTexture(FogMap, map);
            Shader.SetGlobalVector(FogSize, new Vector4(width, height, MapSpace.Tile, 1));
            // In the pass's linear colour: a global colour is passed as it is (a material's would be turned to linear).
            Shader.SetGlobalColor(FogUnknown, Unknown.linear);
            Shader.SetGlobalColor(FogHaze, Haze.linear);
            Shader.SetGlobalColor(FogMistTint, MistTint.linear);
            Shader.SetGlobalFloat(FogMist, mist);
        }

        /// <summary>
        /// What the squad sees in `seen` (the state as the player has been shown it): in sight, in view, and what it has
        /// seen this mission; `instant`, at once (a new map, a return), else fading over SIGHT_FADE.
        /// </summary>
        public void Show(GameState seen, bool instant = false)
        {
            if (map == null || seen.Width != width || seen.Height != height) Reset(seen);
            var visible = Los.TeamVisibility(seen, "xcom");
            var view = Los.TeamView(seen, "xcom");
            int n = width * height;
            // A tile is seen if any of its storeys is (the buildings' faces, roofs and rooms come next).
            for (int i = 0; i < n; i++)
            {
                bool inSight = false, inView = false;
                for (int k = i; k < visible.Length; k += n) inSight |= visible[k];
                for (int k = i; k < view.Length; k += n) inView |= view[k];
                explored[i] |= inView;
                sightTo[i] = inSight ? 1 : 0;
                knownTo[i] = inView || explored[i] ? 1 : 0;
                if (instant)
                {
                    sight[i] = sightTo[i];
                    known[i] = knownTo[i];
                }
            }
            changed = true;
            if (instant) Step(0);
        }

        /// <summary>How many tiles are in sight and seen (now, fading included).</summary>
        public (int sight, int known) Counts() => (sight?.Count(v => v > 0.5f) ?? 0, known?.Count(v => v > 0.5f) ?? 0);

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        public void ShotStep(float dt)
        {
            if (!Application.isPlaying) Step(dt);
        }

        void Step(float dt)
        {
            time += dt;
            Shader.SetGlobalFloat(FogTime, time);
            if (map == null) return;
            float step = dt / SightFade;
            for (int i = 0; i < sight.Length; i++)
            {
                if (sight[i] == sightTo[i] && known[i] == knownTo[i]) continue;
                sight[i] = Mathf.MoveTowards(sight[i], sightTo[i], step);
                known[i] = Mathf.MoveTowards(known[i], knownTo[i], step);
                changed = true;
            }
            if (!changed) return;
            changed = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].r = (byte)(sight[i] * 255);
                pixels[i].g = (byte)(known[i] * 255);
            }
            map.SetPixels32(pixels);
            map.Apply(false);
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
