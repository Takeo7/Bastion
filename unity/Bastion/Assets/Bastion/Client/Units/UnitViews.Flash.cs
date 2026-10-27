using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The red flash of a hit and of a death on any figure (unitViews.ts hitReact and die: pose.flash; synty.ts dyeFlash, an
    /// emission of (1, 0.23, 0.16) × 0.8 of it): what says a shot has landed. Drawn as one more pass over the body's meshes
    /// (Sidekick's, POLYGON's: one submesh each), added light, only while it lasts.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>synty.ts: the flash's light, in linear (0.10.1 added it so to the emission), and how much of it a flash of 1 is.</summary>
        static readonly Color FlashLight = new Color(1f, 0.23f, 0.16f);
        const float FlashShare = 0.8f;

        /// <summary>A hit's: from 1 down to nothing over HitFlash seconds; a death's: from DeathFlash down by a second every DeathFade.</summary>
        const float HitFlash = 0.4f, DeathFlash = 0.6f, DeathFade = 0.75f;

        sealed class Flashing
        {
            public float hit = -1, death = -1;
            public Material material;
            public List<Renderer> renderers;
            public bool on;
        }

        readonly Dictionary<View, Flashing> flashes = new Dictionary<View, Flashing>();

        /// <summary>A hit's flash on `v` from now (HitReact).</summary>
        void FlashHit(View v) => FlashOf(v).hit = 0;

        /// <summary>A death's flash on `v` from now (Die).</summary>
        void FlashDeath(View v) => FlashOf(v).death = 0;

        Flashing FlashOf(View v)
        {
            if (!flashes.TryGetValue(v, out var f)) flashes[v] = f = new Flashing();
            return f;
        }

        /// <summary>The flashes a step on: each one's light now over its figure, the pass taken off once it is out.</summary>
        void StepFlashes(float dt)
        {
            List<View> done = null;
            foreach (var pair in flashes)
            {
                var v = pair.Key;
                var f = pair.Value;
                float k = 0;
                if (f.hit >= 0)
                {
                    f.hit += dt;
                    k = Mathf.Max(k, 1 - f.hit / HitFlash);
                    if (f.hit >= HitFlash) f.hit = -1;
                }
                if (f.death >= 0)
                {
                    f.death += dt;
                    k = Mathf.Max(k, DeathFlash - f.death / DeathFade);
                    if (f.death >= DeathFlash * DeathFade) f.death = -1;
                }
                Light(v, f, k * FlashShare);
                if (f.hit < 0 && f.death < 0) (done ??= new List<View>()).Add(v);
            }
            if (done != null)
                foreach (var v in done)
                {
                    Light(v, flashes[v], 0);
                    if (flashes[v].material != null) DestroyNow(flashes[v].material);
                    flashes.Remove(v);
                }
        }

        /// <summary>`v`'s flash pass at `k` (0 off): its body's renderers, not what it carries, a pass more each.</summary>
        static void Light(View v, Flashing f, float k)
        {
            bool on = k > 0.005f && v.body != null;
            if (on && f.material == null)
            {
                f.material = KitMaterials.New(KitMaterials.SeeThrough, "Destello");
                if (f.material == null) return;
                // Added light over the front faces, as an emission is.
                f.material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                f.material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                f.material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            }
            if (on && f.renderers == null) f.renderers = BodyRenderers(v);
            if (f.material != null) f.material.SetColor("_BaseColor", (FlashLight * k).gamma);
            if (on == f.on || f.renderers == null) return;
            f.on = on;
            Pass(f.renderers, f.material, on);
        }

        /// <summary>The meshes of `v`'s body, not what it carries (the flash's and the mark's).</summary>
        static List<Renderer> BodyRenderers(View v)
        {
            var carried = new HashSet<Transform>(v.carried.Values.Where(t => t != null));
            bool Carried(Transform t)
            {
                for (; t != null && t != v.body; t = t.parent) if (carried.Contains(t)) return true;
                return false;
            }
            return v.body.GetComponentsInChildren<Renderer>(true).Where(r => (r is SkinnedMeshRenderer || r is MeshRenderer) && !Carried(r.transform)).ToList();
        }

        /// <summary>`material` as a pass more over each of `renderers` (`on`), or taken off.</summary>
        static void Pass(List<Renderer> renderers, Material material, bool on)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var materials = r.sharedMaterials.Where(m => m != material).ToList();
                if (on) materials.Add(material);
                r.sharedMaterials = materials.ToArray();
            }
        }

        // ------------------------------------------------------------ the target's mark

        /// <summary>
        /// syntyFigures.ts mark: the target being picked (Highlight) marked on its body, coming in and going in a sixth of
        /// a second (MARK_EASE), breathing by a quarter of it (MARK_PULSE) at 4.5 radians a second (MARK_RATE).
        /// </summary>
        const float MarkEase = 6, MarkPulse = 0.25f, MarkRate = 4.5f;

        sealed class Marking
        {
            public float k;
            public Material material;
            public List<Renderer> renderers;
            public bool on;
        }

        readonly Dictionary<View, Marking> marks = new Dictionary<View, Marking>();
        float markClock;

        /// <summary>The marks a step on: the target's coming in, any other's going, the pass taken off once it is out.</summary>
        void StepMarks(float dt)
        {
            markClock += dt;
            if (highlighted != null && views.TryGetValue(highlighted, out var target) && !target.dead && !marks.ContainsKey(target)) marks[target] = new Marking();
            List<View> done = null;
            foreach (var pair in marks)
            {
                var v = pair.Key;
                var m = pair.Value;
                bool on = v.id == highlighted && !v.dead && views.TryGetValue(v.id, out var now) && now == v;
                m.k = Mathf.Clamp01(m.k + (on ? 1 : -1) * Mathf.Min(dt, 0.25f) * MarkEase);
                Mark(v, m, m.k * (1 - MarkPulse * (0.5f + 0.5f * Mathf.Sin(markClock * MarkRate))));
                if (!on && m.k <= 0) (done ??= new List<View>()).Add(v);
            }
            if (done != null)
                foreach (var v in done)
                {
                    Mark(v, marks[v], 0);
                    if (marks[v].material != null) DestroyNow(marks[v].material);
                    marks.Remove(v);
                }
        }

        /// <summary>What the mark goes over: the body and what it carries (0.10.1 reddened the blades on a Cuervo's back too).</summary>
        static List<Renderer> MarkedRenderers(View v) =>
            v.body.GetComponentsInChildren<Renderer>(true).Concat(v.carried.Values.Where(t => t != null).SelectMany(t => t.GetComponentsInChildren<Renderer>(true)))
                .Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).Distinct().ToList();

        /// <summary>`v`'s mark at `k` (0 off): Bastion/Mark a pass more over its body's renderers and what it carries.</summary>
        static void Mark(View v, Marking m, float k)
        {
            bool on = k > 0.005f && v.body != null;
            if (on && m.material == null)
            {
                m.material = KitMaterials.New(KitMaterials.Mark, "Marca");
                if (m.material == null) return;
            }
            if (on && m.renderers == null) m.renderers = MarkedRenderers(v);
            if (m.material != null) m.material.SetFloat("_Mark", k);
            if (on == m.on || m.renderers == null) return;
            m.on = on;
            Pass(m.renderers, m.material, on);
        }
    }
}
