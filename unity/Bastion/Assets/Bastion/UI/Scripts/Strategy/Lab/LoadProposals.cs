using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The window while a stage loads, proposals for the cuadro (M2, Arturo 23/11: «ver cómo podemos poner ventanas de carga…
    /// se congela la imagen»; Producto's brief, ui/MENSAJE-DE-PRODUCTO-m2-cargas.md), lab only: BASTION_CARGA=a|b|c over the
    /// map on paper in the base's lab (StrategyLab runs-…), for its first stage that can be taken, as if just chosen. Each is
    /// a still picture, drawn a frame before the load starts and frozen with it: nothing moves, no bar.
    ///   a: on the paper itself: the rest of the screen dimmed, the way from «AQUÍ» to the stage in the marker, a note pinned
    ///      by it with the stage, its place, its danger and a line;
    ///   b: the stage's screen, dark as the game's: its place drawn big, its name, its place and danger, a line, the squad
    ///      going under it;
    ///   c: a whole sheet of paper: the place drawn big in ink and ringed, the stage's name written under it, its notes, who goes.
    /// </summary>
    public static class LoadProposals
    {
        public static string Kind => Environment.GetEnvironmentVariable("BASTION_CARGA");

        static readonly Color Paper = new Color(0.88f, 0.84f, 0.74f), Ink = new Color(0.15f, 0.12f, 0.09f), Far = new Color(0.235f, 0.212f, 0.184f);
        static readonly Color Marker = new Color(0.76f, 0.14f, 0.09f), Note = new Color(0.10f, 0.19f, 0.42f), BossInk = new Color(0.48f, 0.08f, 0.05f);
        static readonly Color Dark = new Color(0.047f, 0.055f, 0.06f), Light = new Color(0.93f, 0.93f, 0.9f), Muted = new Color(0.63f, 0.65f, 0.62f);

        /// <summary>The words, provisional (Lore's to write): what is said while it loads, instead of a bar.</summary>
        const string Going = "Saliendo hacia";
        const string Who = "Van";

        public static void Draw(VisualElement doc, RunMapView v, Func<string, Sprite> icon, Func<string, Texture> portrait)
        {
            doc.Q("carga-propuesta")?.RemoveFromHierarchy();
            var kind = Kind;
            var n = v?.Nodes.FirstOrDefault(x => x.Open && x.Why == null);
            if (string.IsNullOrEmpty(kind) || n == null) return;
            var layer = new VisualElement { name = "carga-propuesta", pickingMode = PickingMode.Ignore };
            Abs(layer, 0, 0, 0, 0);
            doc.Add(layer);
            if (kind == "a") OnPaper(doc, layer, v, n, icon);
            else if (kind == "b") StageScreen(layer, v, n, icon, portrait);
            else Sheet(layer, v, n, icon);
        }

        // ------------------------------------------------------------ a: on the paper

        static void OnPaper(VisualElement doc, VisualElement layer, RunMapView v, RunNodeView n, Func<string, Sprite> icon)
        {
            var field = doc.Q(className: "runmap-field")?.worldBound ?? new Rect(16, 100, 940, 640);
            var to = doc.Q("rnode-" + n.Id)?.worldBound.center ?? field.center;
            var here = v.Nodes.FirstOrDefault(x => x.At) is RunNodeView at && doc.Q("rnode-" + at.Id) is VisualElement e ? e.worldBound.center : new Vector2(field.center.x, field.yMax - 40);
            // Everything but the sheet dimmed: four dark bands round it.
            var shade = new Color(0, 0, 0, 0.72f);
            Band(layer, 0, 0, Screen(doc).x, field.y, shade);
            Band(layer, 0, field.yMax, Screen(doc).x, Screen(doc).y - field.yMax, shade);
            Band(layer, 0, field.y, field.x, field.height, shade);
            Band(layer, field.xMax, field.y, Screen(doc).x - field.xMax, field.height, shade);
            // The sheet a little paler (the map waits) and the way gone over in the marker, the stage ringed twice.
            Band(layer, field.x, field.y, field.width, field.height, new Color(Paper.r, Paper.g, Paper.b, 0.35f));
            var ink = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(ink, 0, 0, 0, 0);
            ink.generateVisualContent += ctx =>
            {
                var g = ctx.painter2D;
                g.lineCap = LineCap.Round;
                g.strokeColor = Marker;
                g.lineWidth = 4;
                g.BeginPath();
                g.MoveTo(here + new Vector2(0, -24));
                g.BezierCurveTo(new Vector2(here.x, (here.y + to.y) / 2), new Vector2(to.x, (here.y + to.y) / 2), to + new Vector2(0, 26));
                g.Stroke();
                for (var k = 0; k < 2; k++)
                {
                    g.lineWidth = k == 0 ? 3 : 2;
                    g.BeginPath();
                    g.Arc(to, 27 + k * 5, 0, 350);
                    g.Stroke();
                }
            };
            layer.Add(ink);
            // The note pinned by the stage, on the side with room.
            var right = to.x < field.center.x;
            var w = 300f;
            var x = right ? to.x + 48 : to.x - 48 - w;
            var y = Mathf.Clamp(to.y - 60, field.y + 16, field.yMax - 260);
            var note = PaperCard(layer, x, y, w);
            note.style.rotate = new Rotate(right ? -1.5f : 1.5f);
            Words(note, v, n, icon, Ink, Far, true);
            Pin(layer, new Vector2(x + w / 2, y + 4));
        }

        // ------------------------------------------------------------ b: the stage's screen

        static void StageScreen(VisualElement layer, RunMapView v, RunNodeView n, Func<string, Sprite> icon, Func<string, Texture> portrait)
        {
            layer.style.backgroundColor = Dark;
            layer.style.alignItems = Align.Center;
            layer.style.justifyContent = Justify.Center;
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.alignItems = Align.Center;
            col.style.maxWidth = 640;
            layer.Add(col);
            var symbol = Symbol(col, n.PlaceIcon ?? n.Icon, icon, 150, new Color(Light.r, Light.g, Light.b, 0.9f));
            symbol.style.marginBottom = 18;
            Text(col, (v.Head + " · " + n.Kicker).ToUpperInvariant(), 13, Muted, true).style.letterSpacing = 3;
            var title = Text(col, (n.Name ?? "").ToUpperInvariant(), 48, Light, true);
            title.style.letterSpacing = 2;
            title.style.marginTop = 2;
            var facts = new List<string>();
            if (n.Place != null) facts.Add(n.Place);
            if (n.Danger > 0) facts.Add(StrategyText.Runs.Danger(n.Danger));
            foreach (var m in new[] { n.ModifierId, n.AddedId }.Where(x => x != null)) facts.Add(Bastion.Engine.Lore.ModifierName(m));
            Text(col, string.Join(" · ", facts), 17, Light, false).style.marginTop = 4;
            var line = Text(col, n.What, 16, Muted, false);
            line.style.marginTop = 14;
            line.style.unityTextAlign = TextAnchor.UpperCenter;
            line.style.unityFontStyleAndWeight = FontStyle.Italic;
            // Who goes: their busts in a row at the foot.
            var squad = new VisualElement { pickingMode = PickingMode.Ignore };
            squad.style.position = Position.Absolute;
            squad.style.bottom = 40;
            squad.style.flexDirection = FlexDirection.Row;
            layer.Add(squad);
            foreach (var s in v.Squad)
            {
                var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                cell.style.alignItems = Align.Center;
                cell.style.marginLeft = cell.style.marginRight = 8;
                squad.Add(cell);
                var bust = new VisualElement { pickingMode = PickingMode.Ignore };
                bust.style.width = 64;
                bust.style.height = 64;
                bust.style.backgroundColor = new Color(1, 1, 1, 0.06f);
                var t = portrait?.Invoke(s.Id);
                if (t is RenderTexture rt) bust.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                else if (t is Texture2D t2) bust.style.backgroundImage = new StyleBackground(t2);
                cell.Add(bust);
                Text(cell, s.Name, 12, Light, true).style.marginTop = 3;
            }
        }

        // ------------------------------------------------------------ c: a whole sheet

        static void Sheet(VisualElement layer, RunMapView v, RunNodeView n, Func<string, Sprite> icon)
        {
            layer.style.backgroundColor = Dark;
            var paper = Load("BASTION_MAPA_PAPEL");
            var sheet = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(sheet, 40, 30, 40, 30);
            sheet.style.backgroundColor = Paper;
            if (paper != null)
            {
                sheet.style.backgroundImage = paper;
#pragma warning disable CS0618
                sheet.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
#pragma warning restore CS0618
            }
            sheet.style.alignItems = Align.Center;
            sheet.style.justifyContent = Justify.Center;
            layer.Add(sheet);
            // The place drawn big and ringed by hand.
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.style.width = ring.style.height = 210;
            ring.style.alignItems = Align.Center;
            ring.style.justifyContent = Justify.Center;
            ring.generateVisualContent += ctx =>
            {
                var g = ctx.painter2D;
                var c = new Vector2(105, 105);
                for (var k = 0; k < 2; k++)
                {
                    g.strokeColor = k == 0 ? Ink : Marker;
                    g.lineWidth = k == 0 ? 3 : 2.5f;
                    g.BeginPath();
                    g.Arc(c, 96 - k * 6, 12 + k * 30, 352 + k * 30);
                    g.Stroke();
                }
            };
            sheet.Add(ring);
            Symbol(ring, n.PlaceIcon ?? n.Icon, icon, 120, Ink);
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.style.alignItems = Align.Center;
            words.style.marginTop = 10;
            words.style.maxWidth = 620;
            sheet.Add(words);
            Words(words, v, n, icon, Ink, Far, false);
        }

        // ------------------------------------------------------------ the pieces

        /// <summary>The stage in ink: «Saliendo hacia», its name, its place and danger, its notes, a line, who goes.</summary>
        static void Words(VisualElement into, RunMapView v, RunNodeView n, Func<string, Sprite> icon, Color ink, Color soft, bool small)
        {
            Text(into, Going.ToUpperInvariant(), small ? 11 : 13, soft, true).style.letterSpacing = 2;
            Text(into, (n.Name ?? "").ToUpperInvariant(), small ? 26 : 44, n.Boss ? BossInk : ink, true);
            var facts = new VisualElement { pickingMode = PickingMode.Ignore };
            facts.style.flexDirection = FlexDirection.Row;
            facts.style.alignItems = Align.Center;
            facts.style.flexWrap = Wrap.Wrap;
            facts.style.marginTop = 4;
            into.Add(facts);
            if (small && n.PlaceIcon != null) Symbol(facts, n.PlaceIcon, icon, 22, ink).style.marginRight = 6;
            if (n.Place != null) Text(facts, n.Place, small ? 14 : 18, ink, true).style.marginRight = 10;
            if (n.Danger > 0)
            {
                var d = Text(facts, StrategyText.Runs.Danger(n.Danger), small ? 14 : 18, Marker, true);
                d.style.borderBottomWidth = 2;
                d.style.borderBottomColor = Marker;
            }
            foreach (var m in new[] { n.ModifierId, n.AddedId }.Where(x => x != null))
                Text(into, Bastion.Engine.Lore.ModifierName(m).ToLowerInvariant(), small ? 13 : 16, m == Bastion.Engine.Modifiers.Awake ? Marker : Note, false).style.marginTop = 2;
            var line = Text(into, n.What, small ? 13 : 17, ink, false);
            line.style.marginTop = 10;
            if (!small) line.style.unityTextAlign = TextAnchor.UpperCenter;
            if (!string.IsNullOrEmpty(n.WarnLine))
            {
                var warn = Text(into, n.WarnLine, small ? 12 : 15, ink, false);
                warn.style.marginTop = 6;
                warn.style.borderLeftWidth = 3;
                warn.style.borderLeftColor = Marker;
                warn.style.paddingLeft = 6;
            }
            var who = Text(into, $"{Who}: {string.Join(", ", v.Squad.Select(s => s.Name))}.", small ? 12 : 15, soft, false);
            who.style.marginTop = 10;
        }

        static VisualElement PaperCard(VisualElement layer, float x, float y, float w)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(card, x, y);
            card.style.width = w;
            card.style.paddingLeft = card.style.paddingRight = 16;
            card.style.paddingTop = 18;
            card.style.paddingBottom = 14;
            card.style.backgroundColor = new Color(0.93f, 0.9f, 0.8f);
            card.style.borderBottomWidth = card.style.borderRightWidth = 2;
            card.style.borderBottomColor = card.style.borderRightColor = new Color(0, 0, 0, 0.25f);
            layer.Add(card);
            return card;
        }

        static void Pin(VisualElement layer, Vector2 at)
        {
            var pin = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(pin, at.x - 10, at.y - 10);
            pin.style.width = pin.style.height = 20;
            pin.generateVisualContent += ctx =>
            {
                var g = ctx.painter2D;
                g.fillColor = new Color(0, 0, 0, 0.25f);
                g.BeginPath();
                g.Arc(new Vector2(12, 12), 8, 0, 360);
                g.Fill();
                g.fillColor = Marker;
                g.BeginPath();
                g.Arc(new Vector2(10, 10), 8, 0, 360);
                g.Fill();
                g.fillColor = new Color(1, 1, 1, 0.55f);
                g.BeginPath();
                g.Arc(new Vector2(7.5f, 7.5f), 2.4f, 0, 360);
                g.Fill();
            };
            layer.Add(pin);
        }

        static VisualElement Symbol(VisualElement into, string id, Func<string, Sprite> icon, float size, Color colour)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = e.style.height = size;
            e.style.unityBackgroundImageTintColor = colour;
#pragma warning disable CS0618
            e.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#pragma warning restore CS0618
            into.Add(e);
            if (string.IsNullOrEmpty(id)) return e;
            var folder = Environment.GetEnvironmentVariable("BASTION_MAPA_SIMBOLOS");
            var path = string.IsNullOrEmpty(folder) ? null : System.IO.Path.Combine(folder, id.Replace(':', '-') + ".png");
            var drawn = path != null && System.IO.File.Exists(path) ? Read(path) : Resources.Load<Texture2D>("Strategy/simbolos/" + id.Replace(':', '-'));
            if (drawn != null) e.style.backgroundImage = drawn;
            else if (LineIcon.Has(id)) e.Add(new LineIcon(id) { pickingMode = PickingMode.Ignore });
            else if (icon?.Invoke(id) is Sprite sprite) e.style.backgroundImage = new StyleBackground(sprite);
            return e;
        }

        static Texture2D Load(string env)
        {
            var path = Environment.GetEnvironmentVariable(env);
            return !string.IsNullOrEmpty(path) && System.IO.File.Exists(path) ? Read(path) : null;
        }

        static Texture2D Read(string path)
        {
            var t = new Texture2D(2, 2);
            return t.LoadImage(System.IO.File.ReadAllBytes(path)) ? t : null;
        }

        static Vector2 Screen(VisualElement doc) => new Vector2(doc.layout.width, doc.layout.height);

        static void Band(VisualElement layer, float x, float y, float w, float h, Color colour)
        {
            if (w <= 0 || h <= 0) return;
            var b = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(b, x, y);
            b.style.width = w;
            b.style.height = h;
            b.style.backgroundColor = colour;
            layer.Add(b);
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

        static void Abs(VisualElement e, float left, float top, float? right = null, float? bottom = null)
        {
            e.style.position = Position.Absolute;
            e.style.left = left;
            e.style.top = top;
            if (right != null) e.style.right = right.Value;
            if (bottom != null) e.style.bottom = bottom.Value;
        }
    }
}
