using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A figure drawn in part (0.10.1's syntyFigures.ts fadeIn and veil, synty.ts dyeFade): an enemy coming into sight
    /// fades in over FadeIn seconds (not on the first look at the map: what is there at the start just is), and one in the
    /// way of the shot over a shoulder goes out over VeilSeconds and back (Veil). Meanwhile its body is drawn with
    /// Bastion/FigureFade in its materials' place, dithered, casting no shadow; what it carries goes, and comes back, halfway.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>unitViews.ts FADE_IN, syntyFigures.ts VEIL_SECONDS: seconds to come into sight, to go out of the camera's way.</summary>
        const float FadeIn = 0.28f, VeilSeconds = 0.2f;

        sealed class Fading
        {
            /// <summary>Each body renderer and its own materials, put back once it is all there.</summary>
            public List<(Renderer renderer, Material[] own)> saved;
            public MaterialPropertyBlock block;
        }

        readonly Dictionary<View, Fading> fadings = new Dictionary<View, Fading>();

        /// <summary>Bastion/FigureFade's copy of each figure material, with its colour map.</summary>
        static readonly Dictionary<Material, Material> fadeCopies = new Dictionary<Material, Material>();

        static readonly string[] ColourMaps = { "_ColorMap", "_Albedo_Map", "_BaseMap", "_MainTex" };

        static Material FadeCopy(Material m)
        {
            if (m == null) return null;
            if (fadeCopies.TryGetValue(m, out var copy) && copy != null) return copy;
            copy = KitMaterials.New(KitMaterials.Fade, m.name + " (fundido)");
            if (copy == null) return m;
            var map = ColourMaps.Where(m.HasTexture).Select(m.GetTexture).FirstOrDefault(t => t != null);
            if (map != null) copy.SetTexture("_BaseMap", map);
            return fadeCopies[m] = copy;
        }

        /// <summary>From nothing to all of it over `seconds` (syntyFigures.ts fadeIn), dithered from this frame on (not drawn whole for one first).</summary>
        void StartFade(View v, float seconds)
        {
            v.fade = 0;
            v.fadeGoal = 1;
            v.fadeRate = 1 / Mathf.Max(0.05f, seconds);
            StepFade(v, 0);
        }

        /// <summary>
        /// `v`'s fade a step on: its body on Bastion/FigureFade while it is short of all of it, its own materials back
        /// once it is there; nothing drawn at none (veiled); what it carries shown from halfway.
        /// </summary>
        void StepFade(View v, float dt)
        {
            if (v.fade != v.fadeGoal)
                v.fade = v.fadeGoal > v.fade ? Mathf.Min(v.fadeGoal, Mathf.Max(0.001f, v.fade + Mathf.Min(dt, 0.25f) * v.fadeRate))
                    : Mathf.Max(v.fadeGoal, v.fade - Mathf.Min(dt, 0.25f) * v.fadeRate);
            bool part = v.fade < 1 && v.body != null;
            fadings.TryGetValue(v, out var f);
            if (part && f == null)
            {
                f = fadings[v] = new Fading { block = new MaterialPropertyBlock(), saved = new List<(Renderer, Material[])>() };
                foreach (var r in BodyRenderers(v))
                {
                    var own = r.sharedMaterials;
                    f.saved.Add((r, own));
                    // (The flash's and the mark's passes, over it, left as they are.)
                    r.sharedMaterials = own.Select(m => m == null || IsPass(m) ? m : FadeCopy(m)).ToArray();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                foreach (var w in v.carried.Values) if (w != null) Show(w, false);
            }
            if (f == null) return;
            if (part)
            {
                f.block.SetFloat("_Fade", v.fade);
                foreach (var (r, _) in f.saved) if (r != null) r.SetPropertyBlock(f.block);
                if (!v.cut && !v.gone) foreach (var w in v.carried.Values) if (w != null) Show(w, v.fade >= 0.5f);
                return;
            }
            // All there: its own materials (and any pass laid over them since), its shadow, what it carries.
            foreach (var (r, own) in f.saved)
            {
                if (r == null) continue;
                var now = r.sharedMaterials;
                r.sharedMaterials = own.Concat(now.Skip(own.Length).Where(IsPass)).ToArray();
                r.SetPropertyBlock(null);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (!v.cut) foreach (var w in v.carried.Values) if (w != null) Show(w, true);
            fadings.Remove(v);
        }

        /// <summary>A pass laid over a body (the flash, the mark), not one of its own materials.</summary>
        bool IsPass(Material m) => flashes.Values.Any(f => f.material == m) || marks.Values.Any(k => k.material == m) || IsRim(m) || IsMask(m);

        static void Show(Transform carried, bool on)
        {
            foreach (var r in carried.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }
    }
}
