using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The stage map's proposals for the cuadro (Arturo, 23/11, cuadro 124: «hazme propuestas y que el mapa salga más
    /// atractivo»), lab only: BASTION_MAPA_PROPUESTA=carretera|filas|valle over a runs sample (StrategyLab runs-…). Drawn with
    /// the expedition's real view (StrategyPresenter.Expedition) over the screen, the game's map hidden under it. All three
    /// share what Producto's review asked (REVISION-UI.md 14–17): the stage's card fixed and in parts (title, danger as a
    /// fact, what to do, the place, the warnings), a legend of the marks, the squad folded to its busts; they differ in the
    /// map: a road up the valley to the boss, the rows as cards from left to right, a plan of the valley with its places.
    /// </summary>
    public static class MapProposals
    {
        public static string Kind => Environment.GetEnvironmentVariable("BASTION_MAPA_PROPUESTA");

        static readonly Color Ink = new Color(0.93f, 0.93f, 0.9f), Muted = new Color(0.62f, 0.64f, 0.63f), Dim = new Color(0.38f, 0.4f, 0.4f);
        static readonly Color Accent = new Color(0.55f, 0.85f, 0.35f), Danger = new Color(0.88f, 0.39f, 0.31f), Plate = new Color(0.04f, 0.045f, 0.05f, 0.94f);

        /// <summary>The proposal over `screen` (the base's document root), for `v` of `p`'s campaign; the stage `pick` in the card.</summary>
        public static void Draw(VisualElement screen, StrategyPresenter p, RunMapView v, Func<string, Sprite> icon, Func<string, Texture> portrait)
        {
            screen.Q("mapa-propuesta")?.RemoveFromHierarchy();
            var kind = Kind;
            if (string.IsNullOrEmpty(kind) || v == null) return;
            // The game's own map and squad panel, hidden under it.
            screen.Query(className: "runmap").ForEach(e => e.style.display = DisplayStyle.None);
            screen.Query(className: "runmap-squad").ForEach(e => e.style.display = DisplayStyle.None);
            var c = p.State;
            var run = c.Run;
            // Over the whole screen under the top strip, opaque: the clearing's 3D off behind it, as the game's map has it.
            var layer = new VisualElement { name = "mapa-propuesta" };
            Abs(layer, 0, 44, 0, 0);
            layer.style.flexDirection = FlexDirection.Row;
            layer.style.paddingLeft = layer.style.paddingRight = 16;
            layer.style.paddingTop = layer.style.paddingBottom = 10;
            layer.style.backgroundColor = new Color(0.055f, 0.06f, 0.065f);
            screen.Add(layer);
            var pick = v.Nodes.FirstOrDefault(n => n.Open && n.Why == null) ?? v.Nodes.FirstOrDefault(n => n.At);
            // The map's side: its heading, its field (sized by the layout), the legend and the way home under it.
            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.marginRight = 14;
            layer.Add(left);
            var heading = new VisualElement();
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.alignItems = Align.FlexEnd;
            heading.style.justifyContent = Justify.SpaceBetween;
            left.Add(heading);
            var words = new VisualElement();
            heading.Add(words);
            Text(words, StrategyText.Runs.Head(run.Chapter).ToUpperInvariant(), 22, Ink, true);
            Text(words, v.Line ?? "Elige la siguiente etapa.", 12, Muted, false);
            var back = Text(heading, StrategyText.Runs.Back.ToUpperInvariant(), 13, Ink, true);
            back.style.backgroundColor = new Color(0.25f, 0.27f, 0.28f);
            back.style.paddingLeft = back.style.paddingRight = 12;
            back.style.paddingTop = back.style.paddingBottom = 5;
            back.style.whiteSpace = WhiteSpace.NoWrap;
            var field = new VisualElement();
            field.style.flexGrow = 1;
            field.style.marginTop = 8;
            field.style.overflow = Overflow.Hidden;
            field.style.backgroundColor = kind == "valle" ? new Color(0.09f, 0.11f, 0.09f) : kind == "carretera" ? new Color(0.1f, 0.1f, 0.085f) : new Color(0.075f, 0.08f, 0.085f);
            left.Add(field);
            Legend(left, icon);
            // The right column: the squad folded, the stage's card.
            var col = new VisualElement();
            col.style.width = Mathf.Min(340, screen.layout.width * 0.3f);
            col.style.flexShrink = 0;
            layer.Add(col);
            Squad(col, v, portrait);
            if (pick != null) Card(col, c, run, run.Nodes.First(n => n.Id == pick.Id), pick, icon);
            // The marks once the field has its size (a capture's scheduler doesn't run: the layout's own event).
            void Lay(GeometryChangedEvent e)
            {
                var r = field.contentRect;
                if (float.IsNaN(r.width) || r.width <= 0) return;
                field.UnregisterCallback<GeometryChangedEvent>(Lay);
                var at = Layout(kind, v, new Rect(30, 30, r.width - 60, r.height - 60));
                var ground = new VisualElement { pickingMode = PickingMode.Ignore };
                Abs(ground, 0, 0, 0, 0);
                if (kind == "valle") ground.generateVisualContent += ctx => Valley(ctx, r);
                else if (kind == "carretera") ground.generateVisualContent += ctx => Verge(ctx, r);
                field.Add(ground);
                var paint = new VisualElement { pickingMode = PickingMode.Ignore };
                Abs(paint, 0, 0, 0, 0);
                paint.generateVisualContent += ctx => Ways(ctx, kind, v, at);
                field.Add(paint);
                var step = kind == "filas" ? (r.width - 60) / (Math.Max(1, v.Nodes.Max(n => n.Row)) + 1) : 0;
                foreach (var n in v.Nodes) Node(field, kind, n, at[n.Id], n.Id == pick?.Id, run, icon, step);
                Start(field, kind, v, at["start"], icon);
            }
            field.RegisterCallback<GeometryChangedEvent>(Lay);
        }

        // ------------------------------------------------------------ where each goes

        /// <summary>The marks' places: «start» and each node, by the proposal's way of laying the rows.</summary>
        static Dictionary<string, Vector2> Layout(string kind, RunMapView v, Rect r)
        {
            var at = new Dictionary<string, Vector2>();
            var rows = Math.Max(1, v.Nodes.Max(n => n.Row));
            if (kind == "filas")
            {
                // From the left (the base) to the right (the boss), each row a column.
                var step = r.width / (rows + 1);
                at["start"] = new Vector2(r.x + 20, r.center.y);
                foreach (var n in v.Nodes)
                {
                    var x = r.x + 20 + step * n.Row + step / 2;
                    var gap = Mathf.Min(110, r.height / Math.Max(1, n.Cols));
                    var y = r.center.y + (n.Col - (n.Cols - 1) / 2f) * gap;
                    at[n.Id] = new Vector2(x, y);
                }
                return at;
            }
            // Up the screen: the base at the foot, the boss at the top; a road or a plan's trails swing the columns a little.
            var dy = (r.height - 30) / (rows + 0.6f);
            at["start"] = new Vector2(r.center.x, r.yMax - 20);
            foreach (var n in v.Nodes)
            {
                var y = r.yMax - 20 - dy * n.Row;
                var gap = Mathf.Min(170, r.width / Math.Max(1, n.Cols + 0.5f));
                var swing = kind == "carretera" ? Mathf.Sin(n.Row * 1.1f) * 36 : Mathf.Sin(n.Row * 2.3f + n.Col) * 18;
                var x = r.center.x + (n.Col - (n.Cols - 1) / 2f) * gap + swing;
                at[n.Id] = new Vector2(x, y);
            }
            return at;
        }

        // ------------------------------------------------------------ the grounds

        /// <summary>A road's verges: a dark field with faint furrows across, as the land the road crosses.</summary>
        static void Verge(MeshGenerationContext ctx, Rect r)
        {
            var g = ctx.painter2D;
            g.strokeColor = new Color(1, 1, 1, 0.025f);
            g.lineWidth = 1;
            for (var y = 6f; y < r.height; y += 9)
            {
                g.BeginPath();
                g.MoveTo(new Vector2(0, y));
                for (var x = 0f; x <= r.width; x += 40) g.LineTo(new Vector2(x, y + Mathf.Sin(x * 0.013f + y * 0.05f) * 3));
                g.Stroke();
            }
        }

        /// <summary>A plan of the valley: woods as dark blots, a river across, the height's lines round two hills.</summary>
        static void Valley(MeshGenerationContext ctx, Rect r)
        {
            var g = ctx.painter2D;
            var rng = new System.Random(7);
            g.fillColor = new Color(0.06f, 0.085f, 0.06f);
            for (var i = 0; i < 26; i++)
            {
                var c = new Vector2((float)rng.NextDouble() * r.width, (float)rng.NextDouble() * r.height);
                var rad = 30 + (float)rng.NextDouble() * 70;
                g.BeginPath();
                for (var k = 0; k <= 12; k++)
                {
                    var a = k / 12f * Mathf.PI * 2;
                    var rr = rad * (0.75f + 0.25f * Mathf.Sin(a * 3 + i));
                    var q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    if (k == 0) g.MoveTo(q); else g.LineTo(q);
                }
                g.ClosePath();
                g.Fill();
            }
            g.strokeColor = new Color(0.75f, 0.72f, 0.55f, 0.08f);
            g.lineWidth = 1;
            foreach (var hill in new[] { new Vector2(r.width * 0.2f, r.height * 0.3f), new Vector2(r.width * 0.82f, r.height * 0.62f) })
                for (var ring = 1; ring <= 5; ring++)
                {
                    g.BeginPath();
                    for (var k = 0; k <= 24; k++)
                    {
                        var a = k / 24f * Mathf.PI * 2;
                        var rr = ring * 22 * (1 + 0.18f * Mathf.Sin(a * 2 + ring));
                        var q = hill + new Vector2(Mathf.Cos(a) * rr * 1.4f, Mathf.Sin(a) * rr);
                        if (k == 0) g.MoveTo(q); else g.LineTo(q);
                    }
                    g.Stroke();
                }
            g.strokeColor = new Color(0.28f, 0.4f, 0.5f, 0.55f);
            g.lineWidth = 9;
            g.lineCap = LineCap.Round;
            g.BeginPath();
            g.MoveTo(new Vector2(-10, r.height * 0.18f));
            g.BezierCurveTo(new Vector2(r.width * 0.35f, r.height * 0.05f), new Vector2(r.width * 0.4f, r.height * 0.7f), new Vector2(r.width + 10, r.height * 0.55f));
            g.Stroke();
        }

        // ------------------------------------------------------------ the ways

        static void Ways(MeshGenerationContext ctx, string kind, RunMapView v, Dictionary<string, Vector2> at)
        {
            var g = ctx.painter2D;
            g.lineCap = LineCap.Round;
            var starts = v.Nodes.Where(n => n.Row == 1).ToList();
            // (The start's ways have no stage to come from: drawn from «start» below.)
            var all = v.Links.Where(l => l.From != null && l.To != null && at.ContainsKey(l.From) && at.ContainsKey(l.To)).Select(l => (a: at[l.From], b: at[l.To], l.Taken, l.Open, l.Dim)).ToList();
            foreach (var n in starts) all.Add((at["start"], at[n.Id], v.Nodes.Any(x => x.Row == 1 && x.Done) && n.Done, v.AtStart, false));
            // The unseen and the plain first, the open next, the way taken on top.
            foreach (var pass in new[] { 0, 1, 2 })
                foreach (var (a, b, taken, open, dim) in all)
                {
                    var tier = taken ? 2 : open ? 1 : 0;
                    if (tier != pass) continue;
                    if (kind == "carretera") Road(g, a, b, taken, open, dim);
                    else if (kind == "valle") Trail(g, a, b, taken, open, dim);
                    else Line(g, a, b, taken, open, dim);
                }
        }

        static void Curve(Painter2D g, Vector2 a, Vector2 b, bool across)
        {
            g.BeginPath();
            g.MoveTo(a);
            if (across) g.BezierCurveTo(new Vector2((a.x + b.x) / 2, a.y), new Vector2((a.x + b.x) / 2, b.y), b);
            else g.BezierCurveTo(new Vector2(a.x, (a.y + b.y) / 2), new Vector2(b.x, (a.y + b.y) / 2), b);
        }

        /// <summary>A road: asphalt with its verges, the centre dashed where it can be taken, lit where it was.</summary>
        static void Road(Painter2D g, Vector2 a, Vector2 b, bool taken, bool open, bool dim)
        {
            if (dim)
            {
                g.strokeColor = new Color(0.5f, 0.45f, 0.35f, 0.25f);
                g.lineWidth = 3;
                Curve(g, a, b, false);
                g.Stroke();
                return;
            }
            g.strokeColor = taken ? new Color(0.85f, 0.82f, 0.7f, 0.85f) : new Color(0.55f, 0.52f, 0.44f, 0.5f);
            g.lineWidth = 17;
            Curve(g, a, b, false);
            g.Stroke();
            g.strokeColor = new Color(0.17f, 0.17f, 0.17f);
            g.lineWidth = 13;
            Curve(g, a, b, false);
            g.Stroke();
            if (!open && !taken) return;
            // The centre line: dashes along it.
            g.strokeColor = taken ? new Color(0.95f, 0.85f, 0.45f) : new Color(0.95f, 0.95f, 0.9f, 0.85f);
            g.lineWidth = 2;
            var steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 8);
            for (var i = 0; i < steps; i += 2)
            {
                var p0 = Bez(a, b, i / (float)steps);
                var p1 = Bez(a, b, Mathf.Min(1, (i + 1) / (float)steps));
                g.BeginPath();
                g.MoveTo(p0);
                g.LineTo(p1);
                g.Stroke();
            }
        }

        static Vector2 Bez(Vector2 a, Vector2 b, float t)
        {
            var c1 = new Vector2(a.x, (a.y + b.y) / 2);
            var c2 = new Vector2(b.x, (a.y + b.y) / 2);
            var u = 1 - t;
            return u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b;
        }

        /// <summary>A plan's trail: dots, bright where it was walked, the open ones in the accent.</summary>
        static void Trail(Painter2D g, Vector2 a, Vector2 b, bool taken, bool open, bool dim)
        {
            var col = taken ? new Color(0.95f, 0.92f, 0.8f) : open ? Accent : new Color(0.75f, 0.72f, 0.6f, dim ? 0.18f : 0.4f);
            g.fillColor = col;
            var steps = Mathf.CeilToInt(Vector2.Distance(a, b) / (taken ? 7 : 10));
            for (var i = 0; i <= steps; i++)
            {
                var q = Bez(a, b, i / (float)steps);
                g.BeginPath();
                g.Arc(q, taken ? 2.6f : 1.8f, 0, 360);
                g.Fill();
            }
        }

        static void Line(Painter2D g, Vector2 a, Vector2 b, bool taken, bool open, bool dim)
        {
            g.strokeColor = taken ? new Color(0.95f, 0.95f, 0.9f) : open ? Accent : new Color(1, 1, 1, dim ? 0.08f : 0.22f);
            g.lineWidth = taken ? 4 : open ? 3 : 2;
            Curve(g, a, b, true);
            g.Stroke();
        }

        // ------------------------------------------------------------ the marks

        static void Start(VisualElement layer, string kind, RunMapView v, Vector2 p, Func<string, Sprite> icon)
        {
            var mark = Round(layer, p, 44, new Color(0.16f, 0.17f, 0.18f), v.AtStart ? Ink : Muted, 2);
            IconIn(mark, "map:lata", icon, 28, Ink);
            var l = Text(layer, (v.Start ?? "La base").ToUpperInvariant(), 11, Muted, true);
            Abs(l, p.x - 60, p.y + 24);
            l.style.width = 120;
            l.style.unityTextAlign = TextAnchor.UpperCenter;
        }

        static void Node(VisualElement layer, string kind, RunNodeView n, Vector2 p, bool picked, RunState run, Func<string, Sprite> icon, float step = 0)
        {
            var node = run.Nodes.First(x => x.Id == n.Id);
            var ring = n.Hidden ? Dim : picked ? Accent : n.Open ? new Color(0.55f, 0.85f, 0.35f, 0.7f) : n.At ? Ink : n.Done ? new Color(0.8f, 0.8f, 0.75f) : Muted;
            var place = node.Site != null && LineIcon.Has("place:" + node.Site) ? "place:" + node.Site : null;
            if (kind == "filas" && !n.Boss)
            {
                // A small card: the kind's icon and name, the place under it, the modifiers with their word.
                var cw = Mathf.Clamp(step - 12, 96, 156);
                var card = new VisualElement();
                Abs(card, p.x - cw / 2, p.y - 30);
                card.style.width = cw;
                card.style.height = 60;
                card.style.flexDirection = FlexDirection.Row;
                card.style.alignItems = Align.Center;
                card.style.backgroundColor = n.Hidden ? new Color(0.1f, 0.105f, 0.11f) : new Color(0.13f, 0.14f, 0.15f);
                Border(card, ring, picked ? 2 : 1);
                layer.Add(card);
                var box = new VisualElement();
                box.style.width = cw < 120 ? 28 : 40;
                box.style.height = cw < 120 ? 28 : 40;
                box.style.marginLeft = 5;
                box.style.marginRight = 5;
                box.style.flexShrink = 0;
                card.Add(box);
                if (n.Hidden) { var q = Text(card, "?", 26, Dim, true); return; }
                IconIn(box, n.Icon, icon, cw < 120 ? 26 : 32, Ink);
                var words = new VisualElement();
                words.style.flexShrink = 1;
                card.Add(words);
                Text(words, n.Name?.ToUpperInvariant(), 12, Ink, true);
                if (!n.Stop && node.Site != null && Places.Names.TryGetValue(node.Site, out var placeName)) Text(words, placeName, 10, Muted, false);
                if (n.Modifier != null) Text(words, n.Modifier, 10, new Color(0.65f, 0.75f, 0.95f), false);
                if (!string.IsNullOrEmpty(n.Warn)) Text(words, n.Warn, 10, new Color(0.95f, 0.78f, 0.45f), false);
                if (n.At) Here(layer, p + new Vector2(-cw / 2, -46), "AQUÍ");
                return;
            }
            var side = n.Boss ? 76f : 56f;
            var fill = n.Hidden ? new Color(0.1f, 0.105f, 0.11f) : n.Boss ? new Color(0.25f, 0.07f, 0.06f) : new Color(0.15f, 0.16f, 0.17f);
            if (kind == "valle" && !n.Boss)
            {
                // A pin: its round head with the place drawn, its point on the spot.
                var stem = new VisualElement { pickingMode = PickingMode.Ignore };
                stem.generateVisualContent += ctx =>
                {
                    var g = ctx.painter2D;
                    g.fillColor = ring;
                    g.BeginPath();
                    g.MoveTo(new Vector2(side / 2 - 9, side - 6));
                    g.LineTo(new Vector2(side / 2, side + 12));
                    g.LineTo(new Vector2(side / 2 + 9, side - 6));
                    g.ClosePath();
                    g.Fill();
                };
                Abs(stem, p.x - side / 2, p.y - side - 10);
                stem.style.width = side;
                stem.style.height = side + 14;
                layer.Add(stem);
                p += new Vector2(0, -side / 2 - 10);
            }
            var mark = Round(layer, p, side, fill, ring, picked || n.Boss ? 3 : 2);
            if (n.Hidden) Text(mark, "?", 24, Dim, true).style.unityTextAlign = TextAnchor.MiddleCenter;
            else if (!n.Stop && !n.Boss && place != null && kind != "filas")
            {
                // The place drawn big; the kind small on its rim, top left.
                IconIn(mark, place, icon, side * 0.62f, Ink);
                var k = Round(layer, p + new Vector2(-side / 2 + 6, -side / 2 + 6), 24, new Color(0.08f, 0.085f, 0.09f), ring, 1);
                IconIn(k, n.Icon, icon, 15, Ink);
            }
            else IconIn(mark, n.Icon, icon, side * 0.55f, n.Boss ? new Color(1, 0.6f, 0.5f) : Ink);
            // The modifiers on the mark itself, top right: the night's moon, the weather, the horde.
            var mods = new List<string>();
            if (n.ModifierId != null) mods.Add(n.ModifierId);
            if (n.AddedId != null) mods.Add(n.AddedId);
            for (var i = 0; i < mods.Count; i++)
            {
                var m = Round(layer, p + new Vector2(side / 2 - 6, -side / 2 + 6 + i * 22), 22, ModColor(mods[i]), new Color(0, 0, 0, 0.6f), 1);
                IconIn(m, ModIcon(mods[i]), icon, 14, Color.white);
            }
            if (!n.Hidden)
            {
                var name = Text(layer, n.Boss ? n.Name?.ToUpperInvariant() : n.Name, n.Boss ? 14 : 11, picked ? Accent : n.Open ? Ink : Muted, n.Boss || picked);
                Abs(name, p.x - 70, p.y + side / 2 + 3);
                name.style.width = 140;
                name.style.unityTextAlign = TextAnchor.UpperCenter;
                if (!string.IsNullOrEmpty(n.Warn))
                {
                    // «Sin plaza libre»: amber and small, a fact of the stage, not an error (review 15).
                    var warn = Text(layer, n.Warn, 9, new Color(0.95f, 0.78f, 0.45f), false);
                    Abs(warn, p.x - 70, p.y + side / 2 + 17);
                    warn.style.width = 140;
                    warn.style.unityTextAlign = TextAnchor.UpperCenter;
                }
            }
            if (n.At) Here(layer, p + new Vector2(-side / 2 - 44, -8), "AQUÍ");
            if (n.Boss)
            {
                var goal = Text(layer, "FIN DEL CAPÍTULO", 10, new Color(1, 0.6f, 0.5f), true);
                Abs(goal, p.x - 70, p.y - side / 2 - 18);
                goal.style.width = 140;
                goal.style.unityTextAlign = TextAnchor.UpperCenter;
            }
        }

        static Color ModColor(string id) => id == Modifiers.Night ? new Color(0.2f, 0.25f, 0.5f) : id == Modifiers.Storm ? new Color(0.3f, 0.38f, 0.45f) : new Color(0.6f, 0.15f, 0.12f);
        static string ModIcon(string id) => id == Modifiers.Night ? "night" : id == Modifiers.Storm ? "weather:storm" : "node:" + id;

        static void Here(VisualElement layer, Vector2 p, string text)
        {
            var l = Text(layer, text, 11, Ink, true);
            Abs(l, p.x, p.y);
            l.style.backgroundColor = new Color(0.85f, 0.85f, 0.8f);
            l.style.color = new Color(0.08f, 0.08f, 0.08f);
            l.style.paddingLeft = l.style.paddingRight = 5;
        }

        // ------------------------------------------------------------ the legend, the squad, the card

        static void Legend(VisualElement layer, Func<string, Sprite> icon)
        {
            var row = new VisualElement();
            row.style.marginTop = 8;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = Plate;
            row.style.paddingLeft = 12;
            row.style.paddingTop = row.style.paddingBottom = 6;
            layer.Add(row);
            Text(row, "LEYENDA", 10, Muted, true).style.marginRight = 14;
            foreach (var (id, words) in new[] { (Modifiers.Night, "De noche"), (Modifiers.Storm, "Mal tiempo"), (Modifiers.Awake, "Horda despierta") })
            {
                var m = new VisualElement();
                m.style.width = m.style.height = 20;
                m.style.backgroundColor = ModColor(id);
                Radius(m, 10);
                m.style.marginRight = 5;
                row.Add(m);
                IconIn(m, ModIcon(id), icon, 13, Color.white);
                Text(row, words + ": más botín", 11, Ink, false).style.marginRight = 16;
            }
            var sw = new VisualElement();
            sw.style.width = 30;
            sw.style.height = 3;
            sw.style.backgroundColor = Accent;
            sw.style.marginRight = 5;
            row.Add(sw);
            Text(row, "Se puede ir", 11, Ink, false).style.marginRight = 14;
            var tk = new VisualElement();
            tk.style.width = 30;
            tk.style.height = 3;
            tk.style.backgroundColor = Ink;
            tk.style.marginRight = 5;
            row.Add(tk);
            Text(row, "Recorrido", 11, Ink, false);
        }

        static void Squad(VisualElement col, RunMapView v, Func<string, Texture> portrait)
        {
            var box = new VisualElement();
            box.style.backgroundColor = Plate;
            box.style.paddingLeft = box.style.paddingRight = 10;
            box.style.paddingTop = box.style.paddingBottom = 8;
            box.style.marginBottom = 10;
            col.Add(box);
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            box.Add(top);
            Text(top, "ESCUADRA", 12, Muted, true);
            Text(top, "Equipo ▸", 11, Accent, true);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 6;
            box.Add(row);
            foreach (var s in v.Squad)
            {
                var cell = new VisualElement();
                cell.style.width = 70;
                cell.style.marginRight = 6;
                row.Add(cell);
                var bust = new VisualElement();
                bust.style.width = 70;
                bust.style.height = 60;
                bust.style.backgroundColor = new Color(0.35f, 0.25f, 0.5f);
                var t = portrait?.Invoke(s.Id);
                if (t is RenderTexture rt) bust.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                else if (t is Texture2D t2) bust.style.backgroundImage = new StyleBackground(t2);
                cell.Add(bust);
                Text(cell, s.Name?.Split(' ')[0], 11, Ink, true).style.unityTextAlign = TextAnchor.UpperCenter;
                var bar = new VisualElement();
                bar.style.height = 4;
                bar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                cell.Add(bar);
                var fill = new VisualElement();
                fill.style.height = 4;
                fill.style.width = Length.Percent(s.HpMax > 0 ? 100f * s.Hp / s.HpMax : 0);
                fill.style.backgroundColor = s.Hp * 2 < s.HpMax ? Danger : Accent;
                bar.Add(fill);
                Text(cell, $"{s.Hp} de {s.HpMax}", 9, Muted, false).style.unityTextAlign = TextAnchor.UpperCenter;
            }
        }

        /// <summary>The stage's card, in parts (review 16): kind and title, the danger as a fact, what to do, where, the warnings.</summary>
        static void Card(VisualElement col, CampaignState c, RunState run, RunNode node, RunNodeView n, Func<string, Sprite> icon)
        {
            var card = new VisualElement();
            card.style.backgroundColor = Plate;
            card.style.paddingLeft = card.style.paddingRight = 14;
            card.style.paddingTop = card.style.paddingBottom = 12;
            Border(card, new Color(1, 1, 1, 0.12f), 1);
            col.Add(card);
            Text(card, $"ETAPA · FILA {node.Row}", 10, Muted, true);
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.style.marginTop = 4;
            card.Add(top);
            var ic = new VisualElement();
            ic.style.width = ic.style.height = 34;
            ic.style.marginRight = 8;
            top.Add(ic);
            IconIn(ic, n.Icon, icon, 30, Ink);
            Text(top, n.Name?.ToUpperInvariant(), 22, Ink, true);
            // The facts as chips: its danger, its modifiers with their word.
            var chips = new VisualElement();
            chips.style.flexDirection = FlexDirection.Row;
            chips.style.flexWrap = Wrap.Wrap;
            chips.style.marginTop = 8;
            card.Add(chips);
            if (!n.Stop) Chip(chips, $"PELIGRO {node.Difficulty}", node.Difficulty >= 3 ? Danger : node.Difficulty == 2 ? new Color(0.85f, 0.6f, 0.25f) : new Color(0.45f, 0.6f, 0.4f));
            if (n.Modifier != null) Chip(chips, n.Modifier.ToUpperInvariant(), ModColor(n.ModifierId));
            if (n.AddedId != null) Chip(chips, StrategyText.Runs.ModifierName(n.AddedId).ToUpperInvariant() + " · SUCESO", ModColor(n.AddedId));
            Head(card, "Qué hay que hacer");
            Body(card, StrategyText.Runs.StageLine(node));
            if (!n.Stop && node.Site != null && Places.Names.TryGetValue(node.Site, out var placeName))
            {
                Head(card, "Dónde");
                var where = new VisualElement();
                where.style.flexDirection = FlexDirection.Row;
                where.style.alignItems = Align.FlexStart;
                card.Add(where);
                if (LineIcon.Has("place:" + node.Site))
                {
                    var pi = new VisualElement();
                    pi.style.width = pi.style.height = 40;
                    pi.style.marginRight = 8;
                    pi.style.flexShrink = 0;
                    where.Add(pi);
                    IconIn(pi, "place:" + node.Site, icon, 38, Ink);
                }
                var words = new VisualElement();
                words.style.flexShrink = 1;
                where.Add(words);
                Text(words, placeName, 13, Ink, true);
                if (Lore.SiteLine(node.Site) is string special) Body(words, special);
            }
            if (n.Modifier != null) { Head(card, n.Modifier); Body(card, StrategyText.Runs.ModifierLine(n.ModifierId)); }
            if (!string.IsNullOrEmpty(n.Warn))
            {
                var warn = new VisualElement();
                warn.style.marginTop = 10;
                warn.style.paddingLeft = warn.style.paddingRight = 8;
                warn.style.paddingTop = warn.style.paddingBottom = 6;
                warn.style.backgroundColor = new Color(0.95f, 0.78f, 0.45f, 0.12f);
                warn.style.borderLeftWidth = 3;
                warn.style.borderLeftColor = new Color(0.95f, 0.78f, 0.45f);
                card.Add(warn);
                Body(warn, Lore.RescueNoSeat);
            }
            var go = Text(card, "IR A ESTA ETAPA", 14, new Color(0.08f, 0.08f, 0.08f), true);
            go.style.marginTop = 14;
            go.style.backgroundColor = new Color(0.92f, 0.92f, 0.88f);
            go.style.unityTextAlign = TextAnchor.MiddleCenter;
            go.style.paddingTop = go.style.paddingBottom = 7;
        }

        static void Chip(VisualElement into, string text, Color color)
        {
            var l = Text(into, text, 11, Color.white, true);
            l.style.backgroundColor = color;
            l.style.paddingLeft = l.style.paddingRight = 7;
            l.style.paddingTop = l.style.paddingBottom = 2;
            l.style.marginRight = 6;
            l.style.marginBottom = 4;
        }

        static void Head(VisualElement into, string text)
        {
            var l = Text(into, text.ToUpperInvariant(), 10, Muted, true);
            l.style.marginTop = 10;
            l.style.marginBottom = 2;
        }

        static void Body(VisualElement into, string text)
        {
            var l = Text(into, text, 12, Ink, false);
            l.style.whiteSpace = WhiteSpace.Normal;
        }

        // ------------------------------------------------------------ helpers

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

        static VisualElement Round(VisualElement into, Vector2 p, float side, Color fill, Color ring, float width)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(e, p.x - side / 2, p.y - side / 2);
            e.style.width = e.style.height = side;
            e.style.backgroundColor = fill;
            e.style.alignItems = Align.Center;
            e.style.justifyContent = Justify.Center;
            Radius(e, side / 2);
            Border(e, ring, width);
            into.Add(e);
            return e;
        }

        static void Radius(VisualElement e, float r) => e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        static void Border(VisualElement e, Color color, float width)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = color;
        }

        static void IconIn(VisualElement parent, string id, Func<string, Sprite> icon, float size, Color tint)
        {
            if (string.IsNullOrEmpty(id)) return;
            VisualElement e;
            if (LineIcon.Has(id)) e = new LineIcon(id);
            else
            {
                e = new VisualElement();
                var s = icon?.Invoke(id);
                if (s != null) e.style.backgroundImage = new StyleBackground(s);
                e.style.unityBackgroundImageTintColor = tint;
            }
            e.pickingMode = PickingMode.Ignore;
            e.style.width = e.style.height = size;
            parent.Add(e);
        }
    }
}
