using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The selected soldier outlined in its player's colour (Arturo's note on the battle, 15/11, through Gameplay and UI:
    /// «cuando está seleccionado el soldado tiene que estar resaltado de alguna forma»; the white ring at its feet hardly
    /// showed on snow). One of the three marks UI puts to him, beside its own two (a thicker ring, a mark over the head).
    /// Settlement.Highlight's line (0.12.24, the base's Soldados): Bastion/Mark again, its inside first marked on the
    /// stencil over every piece of the figure and what it carries, whatever stands before them, then the line only off
    /// that (round each piece, it drew the eyes, the brows and the vest), here with a dark edge outside it, so the colour
    /// reads on snow and on asphalt, by day and by night. Not the base's light over the figure: in the battle the squad
    /// has its moon rim by night, and lit by day a figure on snow stands out less, not more. Depth tested as the figure
    /// is (nothing seen through a wall), and drawn by the figure's own renderers, so it goes with it wherever it isn't
    /// drawn (Drawn: the fog, the cutaway's storey, faded out) and fades as it fades (Fade, Veil).
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>
        /// The line (pixels of a picture 900 tall, thicker on a taller one, as Settlement's and the panels'; SearchOutline's
        /// is 2, the target's 1.5), the dark edge outside it (pixels more), and the edge's colour. The labs try others
        /// (CombatLab's BASTION_RESALTE_LINEA).
        /// </summary>
        public static float SelectLine = 2.5f, SelectEdge = 1.5f;
        public static Color SelectEdgeColour = new Color(0.03f, 0.03f, 0.04f, 0.75f);

        /// <summary>Seconds the line takes to come on or go out (Settlement's 0.15).</summary>
        const float SelectEase = 0.15f;

        /// <summary>Who is to be outlined (null, none), in which colour (sRGB, as UI's).</summary>
        string selectId;
        Color selectColour;
        /// <summary>The figure the line is on, how far on (0 to 1), and the renderers it was put over (for which body, what carried).</summary>
        View selectView;
        float selectK;
        Transform selectBody;
        int selectCarried;
        List<Renderer> selectRenderers;
        /// <summary>Bastion/Mark three times: its inside on the stencil; the dark edge, then the line, off it. Made once, kept.</summary>
        static Material selectInside, selectEdge, selectLine;

        static readonly Unity.Profiling.ProfilerMarker MSelect = new Unity.Profiling.ProfilerMarker("Bastion.Units.Resalte");

        /// <summary>
        /// Soldier `id` outlined in `color` as the one selected (UI passes its player's colour: #a678ff Ana's, #ffa630
        /// Arturo's), eased in; asked for another id, the line goes over to that one. `color` null puts out `id`'s (a null
        /// id, whoever's). One at a time. Not on the dead, nor while the figure isn't drawn.
        /// </summary>
        public void Highlight(string id, Color? color)
        {
            if (color is Color c && id != null)
            {
                selectId = id;
                selectColour = c;
            }
            else if (id == null || id == selectId) selectId = null;
            // (Out of play, the labs: at once.)
            if (!Application.isPlaying) StepSelect(SelectEase);
        }

        /// <summary>A pass of the line (UnitViews.Fade leaves them as they are).</summary>
        static bool IsSelect(Material m) => m != null && (m == selectInside || m == selectEdge || m == selectLine);

        /// <summary>Each frame (Step): the line eased on or off, onto the figure as it is now, as wide as the picture asks.</summary>
        void StepSelect(float dt)
        {
            using (MSelect.Auto())
            {
                View want = selectId != null && views.TryGetValue(selectId, out var asked) && asked.body != null && !asked.dead ? asked : null;
                // Over to another figure at once, its line on from nothing; on the same one, eased in or out.
                if (want != null && selectView != null && want != selectView)
                {
                    SelectPasses(false);
                    selectK = 0;
                }
                if (want != null) selectView = want;
                if (selectView == null) return;
                var v = selectView;
                selectK = Mathf.MoveTowards(selectK, want != null ? 1 : 0, dt / SelectEase);
                if (selectK <= 0 || v.body == null || v.root == null)
                {
                    SelectPasses(false);
                    selectView = null;
                    selectK = 0;
                    return;
                }
                // Its body made again, or what it carries changed since (a weapon taken from the pack): onto what it is now.
                if (selectBody != v.body || selectCarried != CarriedKey(v) || !AllThere(selectRenderers))
                {
                    SelectPasses(false);
                    SelectPasses(true);
                }
                if (selectLine == null) return;
                var eye = Camera.main;
                float tall = eye == null ? Screen.height : eye.targetTexture != null ? eye.targetTexture.height : eye.pixelHeight;
                // (Drawn at a lower resolution (URP's render scale), as many of its pixels.)
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp) tall *= urp.renderScale;
                float line = Mathf.Clamp(SelectLine * tall / 900, 1, 6);
                // (Dithered in or out, the line with it.)
                float k = selectK * selectK * (3 - 2 * selectK) * Mathf.Clamp01(v.fade);
                selectLine.SetFloat("_Line", line);
                selectLine.SetColor("_LineColor", selectColour);
                selectLine.SetFloat("_Mark", k);
                selectEdge.SetFloat("_Line", SelectEdge > 0 ? line + SelectEdge * tall / 900 : 0);
                selectEdge.SetColor("_LineColor", SelectEdgeColour);
                selectEdge.SetFloat("_Mark", k);
            }
        }

        /// <summary>What `v` carries, as a number that changes when any of it does.</summary>
        static int CarriedKey(View v)
        {
            int key = 17;
            foreach (var t in v.carried.Values)
                if (t != null) key = key * 31 + t.GetInstanceID();
            return key;
        }

        static bool AllThere(List<Renderer> renderers)
        {
            if (renderers == null) return false;
            foreach (var r in renderers)
                if (r == null) return false;
            return true;
        }

        /// <summary>The line's passes over the body and what it carries of the figure it is on (MarkedRenderers), or taken off them.</summary>
        void SelectPasses(bool on)
        {
            if (!on)
            {
                if (selectRenderers != null)
                    foreach (var r in selectRenderers)
                        if (r != null) r.sharedMaterials = r.sharedMaterials.Where(m => !IsSelect(m)).ToArray();
                selectRenderers = null;
                selectBody = null;
                return;
            }
            var v = selectView;
            if (v == null || v.body == null) return;
            if (selectLine == null)
            {
                // Its inside marked on the stencil (whatever is before it), drawn before every line (a queue lower)...
                selectInside = SelectMark("Resalte (dentro)", 2999, true);
                // ...then the dark edge and the line over it, only off it.
                selectEdge = SelectMark("Resalte (borde)", 3001, false);
                selectLine = SelectMark("Resalte (línea)", 3002, false);
                if (selectInside == null || selectEdge == null || selectLine == null)
                {
                    selectInside = selectEdge = selectLine = null;
                    return;
                }
            }
            selectRenderers = MarkedRenderers(v);
            foreach (var r in selectRenderers)
                r.sharedMaterials = r.sharedMaterials.Where(m => !IsSelect(m)).Append(selectInside).Append(selectEdge).Append(selectLine).ToArray();
            selectBody = v.body;
            selectCarried = CarriedKey(v);
        }

        /// <summary>
        /// Bastion/Mark with only the pass it is for (each pass more is a draw more of every piece; and the red glow's would
        /// take a night rim's _Rim from the renderers' blocks: UnitViews.Rim): the inside's (no colour, the stencil marked
        /// with no depth test), or the line's (off that, depth tested), at `queue`.
        /// </summary>
        static Material SelectMark(string name, int queue, bool inside)
        {
            var m = KitMaterials.New(KitMaterials.Mark, name);
            if (m == null) return null;
            m.SetFloat("_Tint", 0);
            m.SetFloat("_Base", 0);
            m.SetFloat("_Rim", 0);
            m.SetFloat("_Mark", 0);
            m.renderQueue = queue;
            m.SetShaderPassEnabled("SRPDefaultUnlit", inside);
            m.SetShaderPassEnabled("UniversalForward", false);
            m.SetShaderPassEnabled("UniversalForwardOnly", !inside);
            if (inside)
            {
                m.SetFloat("_Line", 0);
                m.SetFloat("_InsideTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                m.SetFloat("_InsideMark", (float)UnityEngine.Rendering.StencilOp.Replace);
            }
            else m.SetFloat("_LineStencil", (float)UnityEngine.Rendering.CompareFunction.NotEqual);
            return m;
        }
    }
}
