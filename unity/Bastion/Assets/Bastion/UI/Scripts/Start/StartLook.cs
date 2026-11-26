using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Start
{
    /// <summary>
    /// The start screen's look as Arturo approved it (cuadros 149–151, ronda2-1280.png), shared by the start screen's pages and
    /// the room: the dark veil with Arte's grain over the top left, on a slant; the entries with no plates, the chosen one on
    /// Arte's brush stroke, its words dark.
    /// </summary>
    public static class StartLook
    {
        // ------------------------------------------------------------------ the veil

        /// <summary>The veil, to go over the scene and under the words: it draws itself to its own size.</summary>
        public static VisualElement Veil()
        {
            grain = grain != null ? grain : Resources.Load<Texture2D>("Start/grano");
            var veil = new VisualElement { pickingMode = PickingMode.Ignore };
            veil.AddToClassList("start__veil");
            veil.generateVisualContent += DrawVeil;
            veil.RegisterCallback<GeometryChangedEvent>(_ => veil.MarkDirtyRepaint());
            return veil;
        }

        static Texture2D grain;

        /// <summary>
        /// The veil of Arturo's picture (cuadro 150: «el degradado en diagonal»): the top left darker, with Arte's grain strong,
        /// its edge falling on a slant from the top right (VeilRight) to the left (VeilLeft), behind the tower, soft over
        /// VeilSoft each side; under it the scene clean, as a beam of light. In cells of VeilCell, each corner's strength by how
        /// far over the edge it is; the grain's cells on its tiles of 512.
        /// </summary>
        static void DrawVeil(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            // Its cells bigger on a very wide screen: one mesh holds 65 535 vertices (the review: 5120 × 1440 made 69 160).
            var cellSize = VeilCell;
            int nx, ny;
            while ((nx = Mathf.CeilToInt(r.width / cellSize)) * (ny = Mathf.CeilToInt(r.height / cellSize)) * 4 > 64000) cellSize += 4;
            float Weight(float x, float y)
            {
                var edge = Mathf.Lerp(VeilLeft.y, VeilRight.y, x / r.width) * r.height;
                return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-VeilSoft * r.height, VeilSoft * r.height, edge - y));
            }
            void Cells(MeshWriteData mesh, Func<float, float, Color32> tint, bool textured)
            {
                var region = mesh.uvRegion;
                ushort at = 0;
                for (var j = 0; j < ny; j++)
                    for (var i = 0; i < nx; i++)
                    {
                        float x0 = i * cellSize, y0 = j * cellSize, x1 = Mathf.Min(x0 + cellSize, r.width), y1 = Mathf.Min(y0 + cellSize, r.height);
                        Vertex V(float x, float y, float ux, float uy) => new Vertex
                        {
                            position = new Vector3(x, y, Vertex.nearZ), tint = tint(x, y),
                            uv = textured ? new Vector2(region.x + ux / GrainTile * region.width, region.y + (1 - uy / GrainTile) * region.height) : Vector2.zero,
                        };
                        float u0 = x0 % GrainTile, v0 = y0 % GrainTile;
                        mesh.SetNextVertex(V(x0, y0, u0, v0));
                        mesh.SetNextVertex(V(x1, y0, u0 + x1 - x0, v0));
                        mesh.SetNextVertex(V(x1, y1, u0 + x1 - x0, v0 + y1 - y0));
                        mesh.SetNextVertex(V(x0, y1, u0, v0 + y1 - y0));
                        foreach (var k in new[] { 0, 1, 2, 0, 2, 3 }) mesh.SetNextIndex((ushort)(at + k));
                        at += 4;
                    }
            }
            // The canvas (Arturo, 26/11: «no está por encima»): the dark blue grey mottled with a lighter one in big and small
            // blotches, as the picture's, so that it reads over a scene as dark as the menu's.
            Color32 Dark(float x, float y)
            {
                var h = r.height;
                var m = MottleBig * Mathf.PerlinNoise(x / (MottleBigSize * h) + 3.1f, y / (MottleBigSize * h) + 7.7f)
                    + (1 - MottleBig) * Mathf.PerlinNoise(x / (MottleSmallSize * h) + 11.3f, y / (MottleSmallSize * h) + 5.9f);
                var c = Color.Lerp(VeilColour, VeilLight, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(MottleFrom, 1, m)) * MottleStrength);
                // Darkest behind the words, on the left, as the picture's: VeilLeftAlpha there, VeilColour's from VeilReach of the
                // width on (measured against the app's picture of the 0.14.11, Pruebas, 26/11).
                c.a = Mathf.Lerp(VeilLeftAlpha, VeilColour.a, Mathf.SmoothStep(0, 1, x / r.width / VeilReach)) * Weight(x, y);
                return c;
            }
            Cells(ctx.Allocate(nx * ny * 4, nx * ny * 6), Dark, false);
            if (grain == null) return;
            Color32 Grain(float x, float y) => new Color(1, 1, 1, GrainStrength * Weight(x, y));
            Cells(ctx.Allocate(nx * ny * 4, nx * ny * 6, grain), Grain, true);
        }

        /// <summary>
        /// The veil's edge where it meets the right side and the left (as parts of the height), how soft (a part of the height
        /// each side), its colour and the grain's strength over it. (CORRECCIONES-2: the edge from (100 %, 9 %) to (0 %, 80 %),
        /// through the tower's foot; a dark, dull blue grey, thin enough for the left's pines to show through it. Producto's
        /// final word, 24/11: ronda2's veil over Arte's fixed scene.)
        /// (Arturo, 26/11, «no está por encima»: in the app, over the menu's night scene with its pines sharp and snowy, the
        /// 0.6 of a dark blue hardly showed. Stronger behind the words (0.85, to 0.62 from the middle on), greyer, and mottled
        /// as a canvas: VeilLight's blotches, big and small, by Perlin noise, a part of the height across; the grain's dots a
        /// little bigger. Set by its effect on the app's picture of the 0.14.11: the lab's shot draws the scene hazier and
        /// lays the panels over it apart, so it isn't the measure.)
        /// </summary>
        static readonly Vector2 VeilRight = new Vector2(1f, 0.09f), VeilLeft = new Vector2(0f, 0.80f);
        const float VeilSoft = 0.012f, VeilCell = 16, GrainTile = 320, GrainStrength = 1f;
        static readonly Color VeilColour = new Color(20 / 255f, 27 / 255f, 45 / 255f, 0.62f), VeilLight = new Color(70 / 255f, 82 / 255f, 108 / 255f, 1f);
        const float VeilLeftAlpha = 0.85f, VeilReach = 0.5f;
        const float MottleBig = 0.65f, MottleBigSize = 0.30f, MottleSmallSize = 0.08f, MottleFrom = 0.35f, MottleStrength = 0.5f;

        // ---------------------------------------------------------------- the entries

        /// <summary>
        /// Arturo's picture's measures (Producto, CORRECCIONES-2, on the screen's height and width): an entry's step, its brush
        /// stroke's height, where the stroke starts and how long it is. (The stroke's paint is inset in its picture, 2 % at the
        /// ends and 8 % above and below: measured the same way on both images at 1280 × 800, its paint from 4 % to 25.3 % of
        /// the width and 6 % high, as the picture's.)
        /// </summary>
        const float EntryStep = 0.065f, BrushHigh = 0.075f, BrushFrom = 0.036f, BrushLong = 0.233f;

        /// <summary>
        /// A screen's entries (a page's column, a form's keys): one chosen at a time, by the pointer over it, the finger on it or
        /// the arrows (Keys); Intro or the space presses it.
        /// </summary>
        public sealed class Entries
        {
            readonly VisualElement screen;
            readonly List<(Button key, Action click)> all = new List<(Button, Action)>();
            Button chosen;

            /// <summary>An entry sized (its brush stroke's left edge and width, in its own pixels): for what goes on its line.</summary>
            public event Action<Button, float, float> Sized;

            /// <param name="screen">What the measures are of: the screen's whole panel.</param>
            public Entries(VisualElement screen) => this.screen = screen;

            /// <summary>
            /// `b` as an entry: its words as written («Nueva partida»), over the brush stroke shown when it is the chosen one
            /// (Arte's, else plain amber). `column`, one of a page's column, as the picture's (its step and its stroke's place
            /// and length on the screen); else a key of a form, its stroke round its words.
            /// </summary>
            public Button Make(Button b, string text, Action click, bool column = true)
            {
                b.text = "";
                // Out of UI Toolkit's own navigation: the arrows and Intro are Keys()'s alone (the 0.14.5's independent review:
                // its focus, invisible, and the stroke went apart, and one Intro would have pressed two entries).
                b.focusable = false;
                b.RemoveFromClassList("start__key");
                b.RemoveFromClassList("start__key--go");
                b.AddToClassList("start__entry");
                b.EnableInClassList("start__entry--short", !column);
                var brush = new VisualElement { pickingMode = PickingMode.Ignore };
                brush.AddToClassList("start__brush");
                if (Resources.Load<Texture2D>("Start/brochazo") is Texture2D art)
                {
                    brush.style.backgroundImage = art;
                    brush.AddToClassList("start__brush--art");
                }
                b.Add(brush);
                var words = new Label(text) { pickingMode = PickingMode.Ignore };
                words.AddToClassList("start__entry-text");
                words.AddToClassList("font-title");
                b.Add(words);
                words.RegisterCallback<GeometryChangedEvent>(_ => Size(b, brush, words, column));
                screen.RegisterCallback<GeometryChangedEvent>(_ => Size(b, brush, words, column));
                if (click != null) b.clicked += click;
                b.clicked += () => Debug.Log($"[entrada] pulsada «{(b.Q<Label>(className: "start__entry-text")?.text ?? text)}»");
                b.RegisterCallback<PointerEnterEvent>(_ => Choose(b));
                b.RegisterCallback<PointerDownEvent>(_ => Choose(b), TrickleDown.TrickleDown);
                all.Add((b, click));
                return b;
            }

            /// <summary>Its words changed («Unirse», «Empezar»…).</summary>
            public static void SetText(Button b, string text)
            {
                if (b.Q<Label>(className: "start__entry-text") is Label words) words.text = text;
                else b.text = text;
            }

            /// <summary>Gone (a page drawn again): none of them chosen nor kept.</summary>
            public void Clear()
            {
                all.Clear();
                chosen = null;
            }

            /// <summary>`key` the chosen entry: its brush stroke, the others none.</summary>
            public void Choose(Button key)
            {
                if (chosen == key) return;
                chosen = key;
                foreach (var (b, _) in all) b.EnableInClassList("start__entry--chosen", b == key);
            }

            /// <summary>The entries one can choose now (in a panel, shown, they and all round them, and on), in their order.</summary>
            List<(Button key, Action click)> Choosable() => all.FindAll(x => Shown(x.key) && x.key.enabledInHierarchy);

            static bool Shown(VisualElement e)
            {
                if (e.panel == null) return false;
                for (var x = e; x != null; x = x.parent)
                    if (x.resolvedStyle.display == DisplayStyle.None || x.resolvedStyle.visibility == Visibility.Hidden) return false;
                return true;
            }

            /// <summary>With none chosen, or the chosen one gone (no saved game: «Continuar partida» hidden), the first there is.</summary>
            public void ChooseFirst()
            {
                var can = Choosable();
                if (can.Count > 0 && (chosen == null || !can.Exists(x => x.key == chosen))) Choose(can[0].key);
            }

            /// <summary>The arrows (and W, S) and Intro or the space, this frame (cuadro 149: «el foco con teclado»).</summary>
            public void Keys()
            {
                var keys = UnityEngine.InputSystem.Keyboard.current;
                if (keys == null) return;
                var can = Choosable();
                if (can.Count == 0) return;
                var at = can.FindIndex(x => x.key == chosen);
                if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) Choose(can[(at + 1 + can.Count) % can.Count].key);
                else if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) Choose(can[(Math.Max(at, 0) - 1 + can.Count) % can.Count].key);
                else if ((keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame) && at >= 0)
                {
                    Bastion.Client.Audio.UiSounds.Play("confirm");
                    can[at].click?.Invoke();
                }
            }

            /// <summary>
            /// A column's entry as the picture's: its step 6.5 % of the screen's height, its words in the middle of it; its
            /// stroke 6 % high, from 4 % of the width and 21.7 % long whatever its words (never shorter than them with a
            /// margin). A form's key: its stroke as high, round its words, longer after them, where it frays.
            /// </summary>
            void Size(Button entry, VisualElement brush, Label words, bool column)
            {
                var s = screen.layout;
                var t = words.layout;
                if (float.IsNaN(s.height) || float.IsNaN(t.width) || t.width <= 0) return;
                var h = Mathf.Round(Mathf.Max(s.height * BrushHigh, t.height + 4));
                float left, w;
                if (column)
                {
                    var step = Mathf.Round(s.height * EntryStep);
                    if (Mathf.Abs(entry.resolvedStyle.height - step) > 0.5f) entry.style.height = step;
                    left = Mathf.Round(s.width * BrushFrom - entry.worldBound.x + screen.worldBound.x);
                    w = Mathf.Round(Mathf.Max(s.width * BrushLong, t.xMax - left + 24));
                    // Pressed anywhere along its stroke (the review: «Ajustes»'s right half answered nothing).
                    var reach = Mathf.Max(0, left + w);
                    if (Mathf.Abs(entry.resolvedStyle.minWidth.value - reach) > 0.5f) entry.style.minWidth = reach;
                }
                else
                {
                    // A little before its words (within the form's plate: its padding is 16) and more after them, where the
                    // stroke frays (its last tenth), as the column's.
                    left = Mathf.Round(t.x - 12);
                    w = Mathf.Round(t.width + 12 + Mathf.Max(34, h * 0.7f));
                }
                brush.style.left = left;
                brush.style.top = Mathf.Round(t.y + (t.height - h) / 2);
                brush.style.width = w;
                brush.style.height = h;
                brush.style.right = StyleKeyword.Auto;
                brush.style.bottom = StyleKeyword.Auto;
                Sized?.Invoke(entry, left, w);
            }
        }
    }
}
