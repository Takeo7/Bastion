using System.Collections.Generic;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// What a loading window says (M2, Arturo 23/11, cuadro 136: «ventana de carga en todas las esperas»): on paper, a stage
    /// being gone to (the C: «Saliendo hacia», its name, its place drawn and ringed, its danger, its notes, its line, who goes
    /// in their colours) or the way back; dark, the simple one (the game's title, a chapter, the way back with the result).
    /// </summary>
    public sealed class LoadView
    {
        public bool Paper;
        public string Kicker;
        public string Title;
        /// <summary>The symbol drawn big (paper): an id of the map's symbols (place:…, kind:…, node:…).</summary>
        public string Symbol;
        public string Place;
        /// <summary>«Peligro 2»; null, none.</summary>
        public string Danger;
        /// <summary>The stage's notes as the map writes them: (words, the warning's red, or the notes' blue).</summary>
        public List<(string Text, bool Red)> Notes = new List<(string, bool)>();
        public string Line;
        /// <summary>«Van», and each soldier's name in their player's colour (hex).</summary>
        public string WhoHead;
        public List<(string Name, string Color)> Who = new List<(string, string)>();
        /// <summary>To a stage from the base (the expedition's first): La Lata is seen leaving before it (cuadro 138).</summary>
        public bool Departs;
        /// <summary>
        /// The game's title where the start screen has it (Start.uss .start__side and .start__title: 7 % and 10 %, 92 px), not
        /// small in the middle: as the start screen fades in under the lifting sheet the title stays still, and only one shows
        /// (Producto, 24/11: with Unity's splash gone, the sheet's small one and the menu's big one were seen together).
        /// </summary>
        public bool AsStart;
    }

    /// <summary>The windows' words (cuadro 136; Lore's, 23/11).</summary>
    public static class LoadText
    {
        /// <summary>Over the stage's name: «Rescate» is what to do, not a place (Lore).</summary>
        public const string Going = "Siguiente etapa";
        public static string Who(int n) => n == 1 ? "Va" : "Van";
        public const string Back = "Vuelta a la base";
        /// <summary>A stage won mid-expedition: back to the map of stages, not to the base (the end card's «Seguir la expedición»).</summary>
        public const string GoOn = "Sigue la expedición";
        /// <summary>As the start screen's waits, in the gerund (not the buttons' infinitive), with no dots: a heading.</summary>
        public const string GoingOn = "Continuando la partida";
        public const string New = "Empezando una partida nueva";
    }

    /// <summary>
    /// A loading window over everything (AppFlow's document), drawn a frame before a scene loads and frozen with it: nothing
    /// in it waits on a frame to look whole. On paper, the map's sheet (Arte's paper and symbols, Resources/Strategy) with the
    /// stage written on it in the map's inks; dark, the game's ground with its words in the middle. `Progress` traces the red
    /// ring round the paper's symbol as far as something real has come (the squad's figures made, UnitViews.Preparing).
    /// </summary>
    public sealed class LoadWindow
    {
        // Black since the cuadro 143 (Arturo, 23/11: «el papel fuera»; «negra, con los datos nuevos»): the game's ground, its
        // words light, the notes in the map's blue, the red bright; the ring and the place's mark in the light ink.
        static readonly Color Marker = new Color(0.94f, 0.31f, 0.24f), Note = new Color(0.62f, 0.71f, 0.76f);
        static readonly Color Dark = new Color(0.047f, 0.055f, 0.06f), Light = new Color(0.93f, 0.93f, 0.9f), Muted = new Color(0.63f, 0.65f, 0.62f);

        public readonly VisualElement Root;
        readonly VisualElement ring;
        float progress = -1;

        public LoadWindow(LoadView v)
        {
            Root = new VisualElement { pickingMode = PickingMode.Position };
            var s = Root.style;
            s.position = Position.Absolute;
            s.left = s.top = s.right = s.bottom = 0;
            s.backgroundColor = Dark;
            s.alignItems = Align.Center;
            s.justifyContent = Justify.Center;
            // A wait's sheet (Paper: the name kept from when it was paper): on black, the place drawn big, ringed in the light
            // ink and, as far as the load has come, in red; only with a mark of the game's for it (none for La Lata's way back).
            if (v.Paper && LineIcon.Has(v.Symbol ?? "map:lata"))
            {
                ring = new VisualElement { pickingMode = PickingMode.Ignore };
                ring.style.width = ring.style.height = 210;
                ring.style.alignItems = Align.Center;
                ring.style.justifyContent = Justify.Center;
                ring.generateVisualContent += DrawRing;
                Root.Add(ring);
                Symbol(ring, v.Symbol ?? "map:lata", 120, Light);
            }
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.style.alignItems = Align.Center;
            words.style.maxWidth = 640;
            words.style.marginTop = v.Paper ? 10 : 0;
            Root.Add(words);
            var ink = Light;
            var soft = Muted;
            if (!string.IsNullOrEmpty(v.Kicker)) Text(words, v.Kicker.ToUpperInvariant(), 13, soft, true).style.letterSpacing = 2;
            if (!string.IsNullOrEmpty(v.Title) && v.AsStart) StartTitle(v.Title);
            else if (!string.IsNullOrEmpty(v.Title)) Text(words, v.Title.ToUpperInvariant(), 44, ink, true).style.letterSpacing = 1.5f;
            var facts = new VisualElement { pickingMode = PickingMode.Ignore };
            facts.style.flexDirection = FlexDirection.Row;
            facts.style.alignItems = Align.Center;
            facts.style.marginTop = 4;
            words.Add(facts);
            if (!string.IsNullOrEmpty(v.Place)) Text(facts, v.Place, 18, ink, true).style.marginRight = 10;
            if (!string.IsNullOrEmpty(v.Danger))
            {
                var d = Text(facts, v.Danger, 18, Marker, true);
                d.style.borderBottomWidth = 2;
                d.style.borderBottomColor = d.style.color.value;
            }
            foreach (var (text, red) in v.Notes)
            {
                var n = Text(words, text, 16, red ? Marker : Note, false);
                n.style.marginTop = 2;
            }
            if (!string.IsNullOrEmpty(v.Line))
            {
                var l = Text(words, v.Line, 17, ink, false);
                l.style.marginTop = 10;
                l.style.unityTextAlign = TextAnchor.UpperCenter;
            }
            if (v.Who.Count > 0)
            {
                // «Van: Anja, Rafael…», each in their player's colour (two players), a darker one on the paper.
                var who = new VisualElement { pickingMode = PickingMode.Ignore };
                who.style.flexDirection = FlexDirection.Row;
                who.style.flexWrap = Wrap.Wrap;
                who.style.justifyContent = Justify.Center;
                who.style.marginTop = 12;
                words.Add(who);
                Text(who, (v.WhoHead ?? "") + ":", 15, soft, false).style.marginRight = 5;
                for (var i = 0; i < v.Who.Count; i++)
                {
                    var (name, colour) = v.Who[i];
                    var c = string.IsNullOrEmpty(colour) ? ink : Ui.Hex(colour, ink);
                    var t = Text(who, name + (i + 1 < v.Who.Count ? "," : "."), 15, c, true);
                    t.style.marginRight = 5;
                }
            }
        }

        /// <summary>The red ring round the paper's symbol traced as far as `f` (0–1): what is really made, never a guess.</summary>
        public void Progress(float f)
        {
            if (ring == null) return;
            f = Mathf.Clamp01(f);
            if (Mathf.Abs(f - progress) < 0.001f) return;
            progress = f;
            ring.MarkDirtyRepaint();
        }

        void DrawRing(MeshGenerationContext ctx)
        {
            var g = ctx.painter2D;
            var c = new Vector2(105, 105);
            g.lineCap = LineCap.Round;
            // The ink ring by hand: not quite round, a little past where it began.
            g.strokeColor = new Color(Light.r, Light.g, Light.b, 0.55f);
            g.lineWidth = 3;
            g.BeginPath();
            for (var i = 0; i <= 60; i++)
            {
                var a = (12 + 370f * i / 60) * Mathf.Deg2Rad;
                var r = 96 * (1 + (Mathf.PerlinNoise(i * 0.25f, 3.1f) - 0.5f) * 0.05f);
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (i == 0) g.MoveTo(p);
                else g.LineTo(p);
            }
            g.Stroke();
            if (progress <= 0) return;
            // The marker's ring inside it, from the top, as far as the load has come.
            g.strokeColor = Marker;
            g.lineWidth = 2.6f;
            g.BeginPath();
            var steps = Mathf.Max(2, Mathf.CeilToInt(60 * progress));
            for (var i = 0; i <= steps; i++)
            {
                var a = (-90 + 360f * progress * i / steps) * Mathf.Deg2Rad;
                var r = 89 * (1 + (Mathf.PerlinNoise(i * 0.3f, 7.7f) - 0.5f) * 0.05f);
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (i == 0) g.MoveTo(p);
                else g.LineTo(p);
            }
            g.Stroke();
        }

        static void Symbol(VisualElement into, string id, float size, Color colour)
        {
            if (string.IsNullOrEmpty(id)) return;
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = e.style.height = size;
            e.style.unityBackgroundImageTintColor = colour;
            e.style.color = colour;
#pragma warning disable CS0618
            e.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#pragma warning restore CS0618
            into.Add(e);
            // The game's own mark (cuadro 143: no symbols by hand).
            if (LineIcon.Has(id)) e.Add(new LineIcon(id) { pickingMode = PickingMode.Ignore });
        }

        /// <summary>
        /// The title as the start screen's (AsStart; Start.uss .start__title, cuadro 149): in the middle at the top, its top at
        /// 5 %, bone; Arte's worn picture of it when it is there, placed as the start screen's (StartScreen.PlaceTitle).
        /// </summary>
        void StartTitle(string title)
        {
            var art = Resources.Load<Texture2D>(Bastion.UI.Start.StartScreen.TitleArt);
            var t = Text(Root, art != null ? "" : title.ToUpperInvariant(), 104, new Color(0.902f, 0.863f, 0.796f), true);
            t.style.position = Position.Absolute;
            t.style.left = t.style.right = 0;
            t.style.top = Length.Percent(5);
            t.style.marginLeft = t.style.marginRight = t.style.marginTop = t.style.marginBottom = 0;
            t.style.letterSpacing = 8;
            t.style.unityTextAlign = TextAnchor.UpperCenter;
            t.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 0, color = new Color(0, 0, 0, 0.45f) };
            if (art == null) return;
            // Where the start screen puts it (StartScreen.PlaceTitle), so the sheet lifts with it still.
            Root.RegisterCallback<GeometryChangedEvent>(_ => Bastion.UI.Start.StartScreen.PlaceTitle(t, art, Root.layout.width, Root.layout.height));
            t.style.backgroundImage = art;
            t.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }

        static Label Text(VisualElement into, string text, float size, Color color, bool title)
        {
            var l = new Label(text ?? "") { pickingMode = PickingMode.Ignore };
            if (title) l.AddToClassList("font-title");
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            into.Add(l);
            return l;
        }
    }
}
