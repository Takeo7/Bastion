using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// By night the figures' moonlit rim (synty.ts dyeRim, syntyFigures.ts: 1 the squad, ENEMY_RIM 0.22 the armed and the
    /// dead, none the rest): Bastion/NightRim a material more over each body renderer, a copy of it for each of their
    /// materials with its colour map, the share in the renderer's own block (kept with what else it holds there).
    /// </summary>
    public sealed partial class UnitViews
    {
        const float EnemyRim = 0.22f;

        static readonly Dictionary<Material, Material> rimCopies = new Dictionary<Material, Material>();

        static Material RimCopy(Material m)
        {
            if (m == null) return null;
            if (rimCopies.TryGetValue(m, out var copy) && copy != null) return copy;
            copy = KitMaterials.New(KitMaterials.NightRim, m.name + " (luna)");
            if (copy == null) return null;
            var map = ColourMaps.Where(m.HasTexture).Select(m.GetTexture).FirstOrDefault(t => t != null);
            if (map != null) copy.SetTexture("_ColorMap", map);
            return rimCopies[m] = copy;
        }

        static bool IsRim(Material m) => m != null && rimCopies.ContainsValue(m);

        static bool IsMask(Material m) => m != null && m == figureMask;

        /// <summary>No figure's stencil bit (a lab's), and the masks put or taken now (out of play mode, where nothing steps).</summary>
        public static bool MasksOff;
        public void MasksNow() => StepMasks();

        static Material figureMask;

        /// <summary>
        /// Each figure's stencil bit where it is drawn (Bastion/FigureMask), over its body and what it carries, so the fog
        /// leaves it be: put on as it is made, again when its body or what it carries change.
        /// </summary>
        void StepMasks()
        {
            if (figureMask == null) figureMask = KitMaterials.New(KitMaterials.FigureMask, "Figura (niebla fuera)");
            if (figureMask == null) return;
            foreach (var v in views.Values)
            {
                if (v.body == null) continue;
                // (Off, for a lab to weigh what it costs: MapLab.Variants «sinmascara».)
                if (MasksOff)
                {
                    if (v.maskRenderers != null)
                        foreach (var r in v.maskRenderers)
                            if (r != null) r.sharedMaterials = r.sharedMaterials.Where(m => m != figureMask).ToArray();
                    v.maskRenderers = null;
                    v.maskBody = null;
                    continue;
                }
                int carried = v.carried.Count(kv => kv.Value != null);
                if (v.maskBody == v.body && v.maskCarried == carried && (v.maskRenderers?.All(r => r != null) ?? false)) continue;
                if (v.maskRenderers != null)
                    foreach (var r in v.maskRenderers)
                        if (r != null) r.sharedMaterials = r.sharedMaterials.Where(m => m != figureMask).ToArray();
                v.maskRenderers = MarkedRenderers(v);
                foreach (var r in v.maskRenderers) r.sharedMaterials = r.sharedMaterials.Where(m => m != figureMask).Append(figureMask).ToArray();
                v.maskBody = v.body;
                v.maskCarried = carried;
            }
        }

        static float RimOf(View v) => v.team == "xcom" ? 1 : v.deadKind || v.weapon != null ? EnemyRim : 0;

        /// <summary>Each view's rim on by night (while it is drawn whole), off by day.</summary>
        void StepRims()
        {
            bool night = Mood.Current != null && Mood.Current.Now == Mood.Night;
            foreach (var v in views.Values)
            {
                bool want = night && v.body != null && RimOf(v) > 0 && v.fade >= 1;
                if (want == v.rimmed && (!want || v.rimBody == v.body)) continue;
                Rim(v, false);
                if (want) Rim(v, true);
            }
        }

        /// <summary>v's rim put on its body's renderers, or taken off them.</summary>
        void Rim(View v, bool on)
        {
            if (!on)
            {
                if (v.rimRenderers != null)
                    foreach (var r in v.rimRenderers)
                        if (r != null) r.sharedMaterials = r.sharedMaterials.Where(m => !IsRim(m)).ToArray();
                v.rimRenderers = null;
                v.rimmed = false;
                v.rimBody = null;
                return;
            }
            v.rimRenderers = BodyRenderers(v);
            var block = new MaterialPropertyBlock();
            foreach (var r in v.rimRenderers)
            {
                var own = r.sharedMaterials;
                var rim = RimCopy(own.FirstOrDefault(m => m != null && !IsPass(m)));
                if (rim == null) continue;
                r.sharedMaterials = own.Append(rim).ToArray();
                r.GetPropertyBlock(block);
                block.SetFloat("_Rim", RimOf(v));
                r.SetPropertyBlock(block);
            }
            v.rimmed = true;
            v.rimBody = v.body;
        }
    }
}
