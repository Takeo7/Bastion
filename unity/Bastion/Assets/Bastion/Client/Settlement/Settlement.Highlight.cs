using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The soldier of Soldados lit while the pointer is on them: a click there opens Personalizar (Arturo, 14/11, the PLAN
    /// of the notes of 14/11: «al pasar el ratón por el soldado en 3D se ilumina, y al pulsarlo se abre Personalizar»; UI
    /// reads the pointer and the click, SoldiersScreen.OverFigure and LightFigure). FigureAt says whose figure is under a
    /// point of the screen; Highlight lights it or puts it out: a thin line round it, round the whole figure only
    /// (Bastion/Mark's line, as SearchOutline's round what a soldier would search, kept off the figure by the stencil its
    /// inside marks first: a line round each of its pieces drew the eyes, the brows and the vest), and a soft light from its
    /// edges and over its colours (Bastion/NightRim's rim, as the squad's by night), on the body and what it holds, eased
    /// in and out (Ease), and kept on through the figure made again (Personalizar's draft, the Armería's weapon).
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>How long the light takes to come on or go out (s).</summary>
        const float Ease = 0.15f;

        /// <summary>
        /// The highlight's line (pixels of a screen 900 tall, the panels' reference: thicker on a taller one, as the panels;
        /// SearchOutline's is 2), its colour (UI's white, «what to press next»: Mixta.uss), and its rim (Bastion/NightRim's
        /// _Rim: the squad's by night is 1; at 2.4 the figure looked frosted, at 0.8 lit). The labs try others
        /// (SettlementLab's BASTION_RESALTE).
        /// </summary>
        public static float HighlightLine = 2.5f, HighlightRim = 0.8f;
        public static Color HighlightColour = new Color(1, 1, 1, 0.9f);

        /// <summary>The soldier asked to be lit (Highlight); null, none.</summary>
        string litId;
        /// <summary>How lit the figure is now (0 to 1), and the figure it was put on (the renderers holding the passes).</summary>
        float litK;
        Transform litFigure;
        List<Renderer> litRenderers;
        /// <summary>Bastion/Mark twice: its inside on the stencil, every piece of the figure first; then the line off it.</summary>
        Material litInside, litLine;
        readonly Dictionary<Material, Material> litRims = new Dictionary<Material, Material>();

        /// <summary>
        /// The soldier whose figure is under `screen` (Unity's screen pixels, from the bottom left, as Input System's
        /// Mouse.current.position and Camera.ScreenPointToRay take them), on Soldados while the clearing is shown; null if
        /// the point is off the figure (by its bones: the body, head, arms and legs, a little wide of them), or there is
        /// none (the fallen's grave, another tab).
        /// </summary>
        public string FigureAt(Vector2 screen)
        {
            if (view != Views.Barracks || MapOn || showFigure == null || !showFigure.gameObject.activeInHierarchy || showAnimator == null) return null;
            var camera = Camera;
            if (camera == null || !camera.enabled) return null;
            var ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            return OnFigure(ray) ? showTurnOf : null;
        }

        /// <summary>
        /// The bones FigureAt measures against (each from its bone to the next, as wide as `radius` metres on a figure of
        /// Figures.CharacterScale; the head over its bone, and the gun by its boxes).
        /// </summary>
        static readonly (HumanBodyBones from, HumanBodyBones to, float radius)[] Limbs =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine, 0.17f), (HumanBodyBones.Spine, HumanBodyBones.Chest, 0.17f),
            (HumanBodyBones.Chest, HumanBodyBones.Neck, 0.17f), (HumanBodyBones.Neck, HumanBodyBones.Head, 0.08f),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.07f), (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.06f),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.07f), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.06f),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.10f), (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.08f),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.10f), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.08f),
            (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 0.07f), (HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 0.07f),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, 0.12f), (HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, 0.12f),
        };

        /// <summary>How much wider than its bones a point still counts as on the figure (m).</summary>
        const float FigureSlack = 0.03f;

        bool OnFigure(Ray ray)
        {
            var a = showAnimator;
            if (a == null || !a.isHuman) return false;
            float scale = Units.Figures.CharacterScale;
            foreach (var (from, to, radius) in Limbs)
            {
                var p = a.GetBoneTransform(from);
                var q = a.GetBoneTransform(to);
                if (p == null || q == null) continue;
                if (RayToSegment(ray, p.position, q.position) <= radius * scale + FigureSlack) return true;
            }
            // The head round its middle, over its bone (the hair and what is worn on it; up to its bone it is the neck's).
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            if (head != null && RayToSegment(ray, head.position + Vector3.up * 0.05f * scale, head.position + Vector3.up * 0.1f * scale) <= 0.12f * scale + FigureSlack) return true;
            // What it holds: each piece of the gun by its own box, turned with it (the world's box of a rifle held across
            // took in half the room beside the soldier).
            if (showGun == null) return false;
            foreach (var filter in showGun.GetComponentsInChildren<MeshFilter>())
            {
                var r = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || r == null || !r.enabled) continue;
                var local = filter.transform.worldToLocalMatrix;
                var inside = new Ray(local.MultiplyPoint3x4(ray.origin), local.MultiplyVector(ray.direction));
                if (filter.sharedMesh.bounds.IntersectRay(inside)) return true;
            }
            return false;
        }

        /// <summary>How near `ray` comes to the segment from `a` to `b` (m).</summary>
        static float RayToSegment(Ray ray, Vector3 a, Vector3 b)
        {
            var v = b - a;
            float vv = Vector3.Dot(v, v);
            // The segment's point nearest the ray's line, then the ray's nearest that (in front of the camera), then back.
            var u = ray.direction;
            var w = ray.origin - a;
            float uv = Vector3.Dot(u, v), den = vv - uv * uv;
            float t = den > 1e-8f ? Mathf.Clamp01((Vector3.Dot(v, w) - uv * Vector3.Dot(u, w)) / den) : 0;
            float s = Mathf.Max(0, Vector3.Dot(a + v * t - ray.origin, u));
            if (vv > 1e-8f) t = Mathf.Clamp01(Vector3.Dot(ray.origin + u * s - a, v) / vv);
            return Vector3.Distance(ray.origin + u * s, a + v * t);
        }

        /// <summary>
        /// Soldier `id`'s figure lit (`on`) or put out, eased (out of play, the labs: at once); a null id, the soldier shown
        /// (UI's LightFigure has no id). Lit while it is them on Soldados, and again if their figure is made anew; another
        /// soldier shown, or `id` (or null) put out, it goes out.
        /// </summary>
        public void Highlight(string id, bool on)
        {
            if (on) litId = id ?? showTurnOf;
            else if (litId == id || id == null) litId = null;
            if (!Application.isPlaying) StepHighlight(Ease);
        }

        /// <summary>Each frame (LateUpdate): the light eased towards on or off, its passes put on the figure as it is now.</summary>
        void StepHighlight(float dt)
        {
            bool want = litId != null && litId == showTurnOf && showFigure != null && view == Views.Barracks;
            float to = want ? 1 : 0;
            // (Lit, it is set again every frame: the line's width follows the screen's size.)
            if (litK == 0 && to == 0) return;
            litK = Mathf.MoveTowards(litK, to, dt / Ease);
            // The figure made again since (Showcase destroys the old one with its renderers): the passes onto the new one.
            if (litFigure != showFigure || litRenderers == null || litRenderers.Any(r => r == null))
            {
                LitPasses(false);
                litFigure = null;
            }
            if (litK <= 0)
            {
                LitPasses(false);
                litFigure = null;
                return;
            }
            if (litFigure == null && showFigure != null)
            {
                litFigure = showFigure;
                LitPasses(true);
            }
            float k = litK * litK * (3 - 2 * litK);
            if (litLine != null)
            {
                var camera = Camera;
                float tall = camera != null ? SizeOf(camera).y : 900;
                litLine.SetFloat("_Line", Mathf.Clamp(HighlightLine * tall / 900, 1, 6));
                litLine.SetColor("_LineColor", HighlightColour);
                litLine.SetFloat("_Mark", k);
            }
            foreach (var rim in litRims.Values)
                if (rim != null) rim.SetFloat("_Rim", HighlightRim * k);
        }

        /// <summary>
        /// The line and the rim as passes more over the lit figure's renderers (the body's and what it holds), or taken off
        /// them. The rim is a copy of Bastion/NightRim for each of their materials, with its colour map (a Sidekick figure's
        /// own palette is in its renderers' blocks: SoldierFigure.OwnColours, which the copy reads); its strength is the
        /// copy's own (a block's _Rim would also light Bastion/Mark's red glow, whose property has that name).
        /// </summary>
        void LitPasses(bool on)
        {
            if (!on)
            {
                if (litRenderers != null)
                    foreach (var r in litRenderers)
                        if (r != null) r.sharedMaterials = r.sharedMaterials.Where(m => m != litLine && m != litInside && !litRims.ContainsValue(m)).ToArray();
                litRenderers = null;
                return;
            }
            if (litFigure == null) return;
            if (litLine == null)
            {
                // Its inside marked on the stencil (whatever is before it), drawn before every line (a queue lower)...
                litInside = LitMark("Resalte (dentro)", 2999);
                if (litInside != null)
                {
                    litInside.SetFloat("_Line", 0);
                    litInside.SetFloat("_InsideTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    litInside.SetFloat("_InsideMark", (float)UnityEngine.Rendering.StencilOp.Replace);
                }
                // ...and the line only off it.
                litLine = LitMark("Resalte (línea)", 3001);
                if (litLine != null) litLine.SetFloat("_LineStencil", (float)UnityEngine.Rendering.CompareFunction.NotEqual);
            }
            litRenderers = litFigure.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToList();
            foreach (var r in litRenderers)
            {
                var own = r.sharedMaterials;
                var rim = LitRim(own.FirstOrDefault(m => m != null));
                var more = new List<Material>(own);
                if (rim != null) more.Add(rim);
                if (litInside != null) more.Add(litInside);
                if (litLine != null) more.Add(litLine);
                r.sharedMaterials = more.ToArray();
            }
        }

        /// <summary>Bastion/Mark with nothing but what is set after (no tint, no glow), at `queue`.</summary>
        static Material LitMark(string name, int queue)
        {
            var m = KitMaterials.New(KitMaterials.Mark, name);
            if (m == null) return null;
            m.SetFloat("_Tint", 0);
            m.SetFloat("_Base", 0);
            m.SetFloat("_Rim", 0);
            m.renderQueue = queue;
            return m;
        }

        static readonly string[] LitMaps = { "_ColorMap", "_Albedo_Map", "_BaseMap", "_MainTex" };

        /// <summary>The rim's copy for a material (its colour map on it, as UnitViews.RimCopy's), made the first time.</summary>
        Material LitRim(Material m)
        {
            if (m == null) return null;
            if (litRims.TryGetValue(m, out var copy) && copy != null) return copy;
            copy = KitMaterials.New(KitMaterials.NightRim, m.name + " (resalte)");
            if (copy == null) return null;
            var map = LitMaps.Where(m.HasTexture).Select(m.GetTexture).FirstOrDefault(t => t != null);
            if (map != null) copy.SetTexture("_ColorMap", map);
            copy.SetFloat("_Rim", 0);
            return litRims[m] = copy;
        }
    }
}
