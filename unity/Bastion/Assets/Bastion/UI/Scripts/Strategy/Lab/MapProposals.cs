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

        /// <summary>
        /// The C reworked (cuadro 126, Arturo 23/11: «se mezclan los caminos con los iconos»): «valle-senda» a thin solid trail,
        /// «valle-carretera» A's road thinner, «valle-cerca» the trail with only the way taken and the next choices strong and
        /// the far rows quieter. All three: the ways end at the marks' edge, straight columns spread over the field (no two
        /// cross: the engine's Next keeps them in order), the names on plates, the small badges at the marks' sides, the
        /// ground quieter and its river along the top.
        /// </summary>
        static bool Calm(string k) => k != null && k.StartsWith("valle-");
        static bool Vale(string k) => k == "valle" || Calm(k);
        /// <summary>A mark's half side by what it is (the ways stop there).</summary>
        static float Radius(RunNodeView n) => n.Boss ? 38 : 28;

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
            screen.Query(className: "runmap-side").ForEach(e => e.style.display = DisplayStyle.None);
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
            field.style.backgroundColor = Paper(kind) ? SheetOf(kind).Paper : Calm(kind) ? new Color(0.075f, 0.088f, 0.075f) : kind == "valle" ? new Color(0.09f, 0.11f, 0.09f) : kind == "carretera" ? new Color(0.1f, 0.1f, 0.085f) : new Color(0.075f, 0.08f, 0.085f);
            left.Add(field);
            if (Paper(kind)) PaperLegend(left, kind, icon);
            else Legend(left, icon);
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
                if (Paper(kind))
                {
                    // The map on paper (cuadro 131): the sheet and the valley, the ink, then the stages' symbols and words.
                    var sheet = new VisualElement { pickingMode = PickingMode.Ignore };
                    Abs(sheet, 0, 0, 0, 0);
                    PaperGround(sheet, r, kind);
                    field.Add(sheet);
                    var ink = new VisualElement { pickingMode = PickingMode.Ignore };
                    Abs(ink, 0, 0, 0, 0);
                    ink.generateVisualContent += ctx => PaperInk(ctx, kind, v, at, pick);
                    field.Add(ink);
                    foreach (var n in v.Nodes) PaperNode(field, kind, n, at[n.Id], n.Id == pick?.Id, run, icon);
                    PaperStart(field, kind, v, at["start"], icon);
                    return;
                }
                var ground = new VisualElement { pickingMode = PickingMode.Ignore };
                Abs(ground, 0, 0, 0, 0);
                // Arte's valley under the C3 (cuadro 129: «el valle de verdad, que se vea»), when there is one: the picture
                // filling the field, cropped, under a veil only as dark as the marks and plates need; else the drawn ground.
                var backdrop = Calm(kind) ? Backdrop(run.Chapter) : null;
                onPicture = backdrop != null;
                if (backdrop != null)
                {
                    ground.style.backgroundImage = backdrop;
#pragma warning disable CS0618
                    ground.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
#pragma warning restore CS0618
                    var veil = new VisualElement { pickingMode = PickingMode.Ignore };
                    Abs(veil, 0, 0, 0, 0);
                    veil.style.backgroundColor = new Color(0.03f, 0.035f, 0.03f, Veil());
                    ground.Add(veil);
                }
                else if (Calm(kind)) ground.generateVisualContent += ctx => ValleyCalm(ctx, r);
                else if (kind == "valle") ground.generateVisualContent += ctx => Valley(ctx, r);
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
                // The C reworked: the columns spread over the whole width, straight (a swing reordered them and the ways crossed).
                var gap = Spread(kind) ? r.width / Math.Max(1, n.Cols) : Mathf.Min(170, r.width / Math.Max(1, n.Cols + 0.5f));
                var swing = Spread(kind) ? 0 : kind == "carretera" ? Mathf.Sin(n.Row * 1.1f) * 36 : Mathf.Sin(n.Row * 2.3f + n.Col) * 18;
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
            var byId = v.Nodes.ToDictionary(n => n.Id);
            // The calm kinds: each end at its mark's edge (below the upper one, over the lower one).
            Vector2 From(string id, Vector2 p) => Calm(kind) ? p - new Vector2(0, id == "start" ? 24 : Radius(byId[id]) + 2) : p;
            Vector2 To(string id, Vector2 p) => Calm(kind) ? p + new Vector2(0, Radius(byId[id]) + 2) : p;
            var all = v.Links.Where(l => l.From != null && l.To != null && at.ContainsKey(l.From) && at.ContainsKey(l.To)).Select(l => (a: From(l.From, at[l.From]), b: To(l.To, at[l.To]), l.Taken, l.Open, l.Dim)).ToList();
            foreach (var n in starts) all.Add((From("start", at["start"]), To(n.Id, at[n.Id]), v.Nodes.Any(x => x.Row == 1 && x.Done) && n.Done, v.AtStart, false));
            // The unseen and the plain first, the open next, the way taken on top.
            foreach (var pass in new[] { 0, 1, 2 })
                foreach (var (a, b, taken, open, dim) in all)
                {
                    var tier = taken ? 2 : open ? 1 : 0;
                    if (tier != pass) continue;
                    if (Calm(kind)) CalmWay(g, kind, a, b, taken, open, dim);
                    else if (kind == "carretera") Road(g, a, b, taken, open, dim);
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

        /// <summary>
        /// The C reworked's way: a solid line on a dark casing, ending at the marks' edges. The way taken in bone, the ones that
        /// can be taken now in the accent, all the rest faint («valle-cerca»: fainter still); «valle-carretera», as A's road thinner.
        /// </summary>
        static void CalmWay(Painter2D g, string kind, Vector2 a, Vector2 b, bool taken, bool open, bool dim)
        {
            var strong = taken || open;
            if (kind == "valle-carretera")
            {
                g.strokeColor = strong ? new Color(0.06f, 0.06f, 0.06f, 0.9f) : new Color(0.06f, 0.06f, 0.06f, 0.35f);
                g.lineWidth = 9;
                Curve(g, a, b, false);
                g.Stroke();
                g.strokeColor = taken ? new Color(0.85f, 0.82f, 0.7f) : open ? new Color(0.6f, 0.62f, 0.58f) : new Color(0.4f, 0.4f, 0.38f, dim ? 0.18f : 0.32f);
                g.lineWidth = 6;
                Curve(g, a, b, false);
                g.Stroke();
                if (!strong) return;
                g.strokeColor = taken ? new Color(0.95f, 0.85f, 0.45f) : Accent;
                g.lineWidth = 1.5f;
                var steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 7);
                for (var i = 0; i < steps; i += 2)
                {
                    g.BeginPath();
                    g.MoveTo(Bez(a, b, i / (float)steps));
                    g.LineTo(Bez(a, b, Mathf.Min(1, (i + 1) / (float)steps)));
                    g.Stroke();
                }
                return;
            }
            var faint = kind == "valle-cerca" ? (dim ? 0.06f : 0.12f) : (dim ? 0.14f : 0.28f);
            if (!strong && onPicture)
            {
                // Over Arte's picture: a dark casing, the line a little stronger, so the far ways stay there on a light ground.
                g.strokeColor = new Color(0.02f, 0.02f, 0.02f, 0.4f);
                g.lineWidth = 4;
                Curve(g, a, b, false);
                g.Stroke();
                faint = Mathf.Max(faint * 2.5f, 0.3f);
            }
            if (strong)
            {
                g.strokeColor = new Color(0.03f, 0.03f, 0.03f, 0.75f);
                g.lineWidth = 7;
                Curve(g, a, b, false);
                g.Stroke();
            }
            g.strokeColor = taken ? new Color(0.95f, 0.93f, 0.85f) : open ? Accent : new Color(0.85f, 0.8f, 0.65f, faint);
            g.lineWidth = taken ? 3.5f : open ? 3 : 2;
            Curve(g, a, b, false);
            g.Stroke();
        }

        /// <summary>
        /// Arte's picture of the valley (cuadro 129), if any: BASTION_MAPA_FONDO, a PNG's path (to try them without Assets), else
        /// Resources/Strategy/mapa-etapas-fondo-{chapter} or mapa-etapas-fondo. Null, none: the drawn ground.
        /// </summary>
        static Texture2D Backdrop(int chapter)
        {
            var path = System.Environment.GetEnvironmentVariable("BASTION_MAPA_FONDO");
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                var t = new Texture2D(2, 2);
                if (t.LoadImage(System.IO.File.ReadAllBytes(path))) return t;
                Debug.LogWarning($"[mapa] no se lee {path}");
            }
            return Resources.Load<Texture2D>($"Strategy/mapa-etapas-fondo-{chapter}") ?? Resources.Load<Texture2D>("Strategy/mapa-etapas-fondo");
        }

        /// <summary>How dark the veil over Arte's picture is (0 none, 1 black): BASTION_MAPA_FONDO_VELO, else 0.35.</summary>
        static float Veil() => float.TryParse(System.Environment.GetEnvironmentVariable("BASTION_MAPA_FONDO_VELO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? Mathf.Clamp01(v) : 0.35f;

        /// <summary>Arte's picture under the network (Backdrop): the faint ways get a dark casing, or a light picture swallows them.</summary>
        static bool onPicture;

        /// <summary>The C reworked's ground: the valley's woods and heights quieter, its river along the top, out of the network.</summary>

        static void ValleyCalm(MeshGenerationContext ctx, Rect r)
        {
            var g = ctx.painter2D;
            var rng = new System.Random(7);
            g.fillColor = new Color(0.066f, 0.08f, 0.066f);
            for (var i = 0; i < 22; i++)
            {
                var c = new Vector2((float)rng.NextDouble() * r.width, (float)rng.NextDouble() * r.height);
                var rad = 40 + (float)rng.NextDouble() * 80;
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
            g.strokeColor = new Color(0.75f, 0.72f, 0.55f, 0.035f);
            g.lineWidth = 1;
            foreach (var hill in new[] { new Vector2(r.width * 0.12f, r.height * 0.55f), new Vector2(r.width * 0.9f, r.height * 0.35f) })
                for (var ring = 1; ring <= 4; ring++)
                {
                    g.BeginPath();
                    for (var k = 0; k <= 24; k++)
                    {
                        var a = k / 24f * Mathf.PI * 2;
                        var rr = ring * 20 * (1 + 0.18f * Mathf.Sin(a * 2 + ring));
                        var q = hill + new Vector2(Mathf.Cos(a) * rr * 1.3f, Mathf.Sin(a) * rr);
                        if (k == 0) g.MoveTo(q); else g.LineTo(q);
                    }
                    g.Stroke();
                }
            g.strokeColor = new Color(0.26f, 0.36f, 0.44f, 0.35f);
            g.lineWidth = 7;
            g.lineCap = LineCap.Round;
            g.BeginPath();
            g.MoveTo(new Vector2(-10, 26));
            g.BezierCurveTo(new Vector2(r.width * 0.3f, 6), new Vector2(r.width * 0.6f, 44), new Vector2(r.width + 10, 18));
            g.Stroke();
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
            var l = Text(layer, (v.AtStart && Calm(kind) ? "AQUÍ · " : "") + (v.Start ?? "La base").ToUpperInvariant(), 11, Muted, true);
            if (Calm(kind))
            {
                // On a plate at its right, as the stages' names (cuadro 126).
                Abs(l, p.x + 28, p.y - 9);
                l.style.whiteSpace = WhiteSpace.NoWrap;
                l.style.backgroundColor = new Color(0.04f, 0.045f, 0.05f, 0.88f);
                l.style.paddingLeft = l.style.paddingRight = 6;
                return;
            }
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
                // (The C reworked: on the mark's left side, off the ways that leave from its top and come into its foot.)
                var k = Round(layer, Calm(kind) ? p + new Vector2(-side / 2 - 8, -11) : p + new Vector2(-side / 2 + 6, -side / 2 + 6), 24, new Color(0.08f, 0.085f, 0.09f), ring, 1);
                IconIn(k, n.Icon, icon, 15, Ink);
            }
            else IconIn(mark, n.Icon, icon, side * 0.55f, n.Boss ? new Color(1, 0.6f, 0.5f) : Ink);
            // The modifiers on the mark itself, top right: the night's moon, the weather, the horde.
            var mods = new List<string>();
            if (n.ModifierId != null) mods.Add(n.ModifierId);
            if (n.AddedId != null) mods.Add(n.AddedId);
            for (var i = 0; i < mods.Count; i++)
            {
                var m = Round(layer, Calm(kind) ? p + new Vector2(-side / 2 - 8, 13 + i * 22) : p + new Vector2(side / 2 - 6, -side / 2 + 6 + i * 22), 22, ModColor(mods[i]), new Color(0, 0, 0, 0.6f), 1);
                IconIn(m, ModIcon(mods[i]), icon, 14, Color.white);
            }
            // The C reworked: the name on a plate at the mark's right, «AQUÍ» in it; the far ones quieter («valle-cerca»).
            if (Calm(kind))
            {
                // The far ones' marks at half light; their names not (cuadro 129: one plans looking at what comes), on the
                // same plate as the rest, in the muted grey.
                var quiet = kind == "valle-cerca" && !n.Open && !n.At && !n.Done && !n.Boss;
                if (quiet) mark.style.opacity = 0.5f;
                if (!n.Hidden)
                {
                    var plate = new VisualElement { pickingMode = PickingMode.Ignore };
                    Abs(plate, p.x + side / 2 + 6, p.y - 11);
                    plate.style.backgroundColor = new Color(0.04f, 0.045f, 0.05f, 0.88f);
                    plate.style.paddingLeft = plate.style.paddingRight = 6;
                    plate.style.paddingTop = plate.style.paddingBottom = 2;
                    if (picked) Border(plate, Accent, 1);
                    layer.Add(plate);
                    var line = (n.At ? "AQUÍ · " : "") + (n.Boss ? n.Name?.ToUpperInvariant() : n.Name);
                    Text(plate, line, n.Boss ? 13 : 11, picked ? Accent : n.Open || n.At ? Ink : Muted, n.Boss || picked || n.At).style.whiteSpace = WhiteSpace.NoWrap;
                    if (!string.IsNullOrEmpty(n.Warn)) Text(plate, n.Warn, 9, new Color(0.95f, 0.78f, 0.45f), false).style.whiteSpace = WhiteSpace.NoWrap;
                }
                if (n.Boss)
                {
                    var end = Text(layer, "FIN DEL CAPÍTULO", 10, new Color(1, 0.6f, 0.5f), true);
                    Abs(end, p.x + side / 2 + 6, p.y + 8);
                }
                return;
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

        // ------------------------------------------------------------ the map on paper (cuadro 131)

        /// <summary>
        /// The map drawn on paper (cuadro 131, Arturo 23/11: «que parezca un mapa dibujado y los iconos y las líneas también, en
        /// un papel»), on the C3's layout: «papel-claro» aged light paper in dark ink with a red marker, «papel-kraft» brown kraft
        /// in black marker with a white one, «papel-carretera» a folded road map whose ways are roads. The stages and the ways are
        /// part of the drawing: the place's symbol ringed by hand, the way taken gone over twice, the ones to take now in the
        /// marker, the far ones in pencil; the night, the weather and the horde noted beside the symbol; «AQUÍ» a pin. Arte's paper
        /// and valley when given (BASTION_MAPA_PAPEL, BASTION_MAPA_DIBUJO; Resources/Strategy/mapa-papel-{style},
        /// mapa-dibujo-{style} or mapa-dibujo); till then a paper of our own, with a few strokes of the valley at its edges.
        /// </summary>
        static bool Paper(string k) => k != null && k.StartsWith("papel-");
        /// <summary>The C3's straight columns over the whole width: the calm kinds and the paper ones.</summary>
        static bool Spread(string k) => Calm(k) || Paper(k);
        static float PaperRadius(RunNodeView n) => n.Boss ? 30 : 22;

        sealed class Sheet
        {
            public Color Paper, Edge, Ink, Pencil, Mark, Note, Red;
            public bool Folds, Roads, Fibres;
        }

        static Sheet SheetOf(string k) => k == "papel-kraft"
            ? new Sheet { Paper = new Color(0.69f, 0.55f, 0.39f), Edge = new Color(0.36f, 0.25f, 0.14f), Ink = new Color(0.08f, 0.07f, 0.06f), Pencil = new Color(0.14f, 0.1f, 0.06f, 0.78f), Mark = new Color(0.98f, 0.97f, 0.92f), Note = new Color(0.12f, 0.1f, 0.08f), Red = new Color(0.62f, 0.09f, 0.05f), Fibres = true }
            : k == "papel-carretera"
            ? new Sheet { Paper = new Color(0.95f, 0.94f, 0.88f), Edge = new Color(0.55f, 0.52f, 0.44f), Ink = new Color(0.14f, 0.16f, 0.19f), Pencil = new Color(0.42f, 0.44f, 0.47f, 0.75f), Mark = new Color(0.98f, 0.76f, 0.12f), Note = new Color(0.1f, 0.28f, 0.58f), Red = new Color(0.8f, 0.16f, 0.1f), Folds = true, Roads = true }
            : new Sheet { Paper = new Color(0.9f, 0.86f, 0.75f), Edge = new Color(0.5f, 0.4f, 0.26f), Ink = new Color(0.15f, 0.12f, 0.09f), Pencil = new Color(0.38f, 0.33f, 0.27f, 0.62f), Mark = new Color(0.76f, 0.14f, 0.09f), Note = new Color(0.14f, 0.25f, 0.52f), Red = new Color(0.7f, 0.1f, 0.06f) };

        static string Style(string k) => k.Substring("papel-".Length);

        /// <summary>A steady number from an id (string.GetHashCode isn't steady from one run to the next).</summary>
        static float Seed(string id)
        {
            var h = 17;
            foreach (var ch in id ?? "") h = unchecked(h * 31 + ch);
            return (h & 0xffff) / 6553.5f;
        }

        /// <summary>A way drawn by hand: the C3's curve, a little unsteady in the middle, steady at its ends.</summary>
        static List<Vector2> Wobble(Vector2 a, Vector2 b, float seed, float amp)
        {
            var pts = new List<Vector2>();
            var len = Vector2.Distance(a, b);
            var steps = Mathf.Max(8, Mathf.CeilToInt(len / 6));
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var d = Bez(a, b, Mathf.Min(1, t + 0.01f)) - Bez(a, b, Mathf.Max(0, t - 0.01f));
                var normal = new Vector2(-d.y, d.x).normalized;
                var w = (Mathf.PerlinNoise(t * len / 45f, seed) - 0.5f) * 2 * amp * Mathf.Sin(t * Mathf.PI);
                pts.Add(Bez(a, b, t) + normal * w);
            }
            return pts;
        }

        /// <summary>A ring drawn by hand: not quite round, a little past where it began.</summary>
        static List<Vector2> Ring(Vector2 c, float r, float seed)
        {
            var pts = new List<Vector2>();
            var from = Mathf.PerlinNoise(seed, 0.3f) * Mathf.PI * 2;
            const int n = 44;
            for (var i = 0; i <= n; i++)
            {
                var a = from + (Mathf.PI * 2 + 0.5f) * i / n;
                var k = 1 + (Mathf.PerlinNoise(i * 0.3f, seed) - 0.5f) * 0.12f + 0.05f * i / n;
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * k);
            }
            return pts;
        }

        static void Stroke(Painter2D g, List<Vector2> pts, Color colour, float width)
        {
            g.strokeColor = colour;
            g.lineWidth = width;
            g.BeginPath();
            g.MoveTo(pts[0]);
            for (var i = 1; i < pts.Count; i++) g.LineTo(pts[i]);
            g.Stroke();
        }

        static void Dot(Painter2D g, Vector2 c, float r, Color colour)
        {
            g.fillColor = colour;
            g.BeginPath();
            g.Arc(c, r, 0, 360);
            g.Fill();
        }

        /// <summary>Arte's picture for this style, if any: the env's path, else Resources/Strategy/{name}-{style}, else {name}.</summary>
        static Texture2D ArtOf(string env, string name, string style)
        {
            var path = Environment.GetEnvironmentVariable(env);
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                var t = new Texture2D(2, 2);
                if (t.LoadImage(System.IO.File.ReadAllBytes(path))) return t;
            }
            return Resources.Load<Texture2D>($"Strategy/{name}-{style}") ?? Resources.Load<Texture2D>($"Strategy/{name}");
        }

        /// <summary>
        /// The sheet: Arte's paper, or ours (its colour, a grain, darker towards its edges; kraft with fibres, the road map folded in
        /// six); then Arte's valley in the style's ink, or a few strokes of ours at the edges, out of the network: a river down
        /// the left, woods in the corners the rows leave free.
        /// </summary>
        static void PaperGround(VisualElement ground, Rect r, string kind)
        {
            var s = SheetOf(kind);
            var paper = ArtOf("BASTION_MAPA_PAPEL", "mapa-papel", Style(kind));
            if (paper != null)
            {
                ground.style.backgroundImage = paper;
#pragma warning disable CS0618
                ground.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
#pragma warning restore CS0618
            }
            else ground.generateVisualContent += ctx => OwnPaper(ctx, r, s);
            var drawing = ArtOf("BASTION_MAPA_DIBUJO", "mapa-dibujo", Style(kind));
            var valley = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(valley, 0, 0, 0, 0);
            if (drawing != null)
            {
                valley.style.backgroundImage = drawing;
#pragma warning disable CS0618
                valley.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
#pragma warning restore CS0618
                // How strong the valley's ink is (BASTION_MAPA_DIBUJO_TINTA, 0–1; 0.55 if not said): Arte's woods are faint at 0.55.
                var strength = float.TryParse(Environment.GetEnvironmentVariable("BASTION_MAPA_DIBUJO_TINTA"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var k) ? Mathf.Clamp01(k) : 0.55f;
                valley.style.unityBackgroundImageTintColor = new Color(s.Ink.r, s.Ink.g, s.Ink.b, strength);
            }
            else valley.generateVisualContent += ctx => OwnValley(ctx, r, s);
            ground.Add(valley);
        }

        static void OwnPaper(MeshGenerationContext ctx, Rect r, Sheet s)
        {
            var g = ctx.painter2D;
            float w = r.width, h = r.height;
            g.fillColor = s.Paper;
            g.BeginPath();
            g.MoveTo(Vector2.zero);
            g.LineTo(new Vector2(w, 0));
            g.LineTo(new Vector2(w, h));
            g.LineTo(new Vector2(0, h));
            g.ClosePath();
            g.Fill();
            var rnd = new System.Random(7);
            // The grain, and kraft's fibres.
            for (var i = 0; i < 1100; i++)
                Dot(g, new Vector2((float)rnd.NextDouble() * w, (float)rnd.NextDouble() * h), 0.4f + (float)rnd.NextDouble() * 0.9f, new Color(s.Edge.r, s.Edge.g, s.Edge.b, 0.05f + (float)rnd.NextDouble() * 0.08f));
            if (s.Fibres)
                for (var i = 0; i < 260; i++)
                {
                    var p = new Vector2((float)rnd.NextDouble() * w, (float)rnd.NextDouble() * h);
                    var a = (float)rnd.NextDouble() * Mathf.PI;
                    var l = 4 + (float)rnd.NextDouble() * 10;
                    Stroke(g, new List<Vector2> { p, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * l }, new Color(s.Edge.r, s.Edge.g, s.Edge.b, 0.1f), 0.8f);
                }
            // Darker towards the edges, as handled paper.
            for (var i = 0; i < 16; i++)
            {
                var inset = i * 3f + 1.5f;
                g.strokeColor = new Color(s.Edge.r, s.Edge.g, s.Edge.b, 0.11f * (1 - i / 16f));
                g.lineWidth = 3;
                g.BeginPath();
                g.MoveTo(new Vector2(inset, inset));
                g.LineTo(new Vector2(w - inset, inset));
                g.LineTo(new Vector2(w - inset, h - inset));
                g.LineTo(new Vector2(inset, h - inset));
                g.ClosePath();
                g.Stroke();
            }
            if (!s.Folds) return;
            // Folded in six: each panel a shade apart, the creases light with their shadow.
            for (var c = 0; c < 3; c++)
                for (var row = 0; row < 2; row++)
                {
                    if ((c + row) % 2 == 0) continue;
                    g.fillColor = new Color(s.Edge.r, s.Edge.g, s.Edge.b, 0.045f);
                    g.BeginPath();
                    g.MoveTo(new Vector2(w * c / 3, h * row / 2));
                    g.LineTo(new Vector2(w * (c + 1) / 3, h * row / 2));
                    g.LineTo(new Vector2(w * (c + 1) / 3, h * (row + 1) / 2));
                    g.LineTo(new Vector2(w * c / 3, h * (row + 1) / 2));
                    g.ClosePath();
                    g.Fill();
                }
            foreach (var x in new[] { w / 3, w * 2 / 3 })
            {
                Stroke(g, new List<Vector2> { new Vector2(x, 0), new Vector2(x, h) }, new Color(1, 1, 1, 0.55f), 2);
                Stroke(g, new List<Vector2> { new Vector2(x + 1.5f, 0), new Vector2(x + 1.5f, h) }, new Color(s.Edge.r, s.Edge.g, s.Edge.b, 0.28f), 1);
            }
            Stroke(g, new List<Vector2> { new Vector2(0, h / 2), new Vector2(w, h / 2) }, new Color(1, 1, 1, 0.55f), 2);
            Stroke(g, new List<Vector2> { new Vector2(0, h / 2 + 1.5f), new Vector2(w, h / 2 + 1.5f) }, new Color(s.Edge.r, s.Edge.g, s.Edge.b, 0.28f), 1);
        }

        /// <summary>Ours till Arte's comes: a river down the left edge, woods as little firs in the corners, hills along the top.</summary>
        static void OwnValley(MeshGenerationContext ctx, Rect r, Sheet s)
        {
            var g = ctx.painter2D;
            g.lineCap = LineCap.Round;
            g.lineJoin = LineJoin.Round;
            float w = r.width, h = r.height;
            var water = new Color(s.Note.r, s.Note.g, s.Note.b, 0.5f);
            var river = new List<Vector2>();
            for (var i = 0; i <= 40; i++)
            {
                var t = i / 40f;
                river.Add(new Vector2(14 + Mathf.Sin(t * 5.5f) * 7 + (Mathf.PerlinNoise(t * 6, 2) - 0.5f) * 6, t * h));
            }
            Stroke(g, river, water, 2.4f);
            Stroke(g, river.Select(p => p + new Vector2(6, 0)).ToList(), new Color(water.r, water.g, water.b, 0.3f), 1.2f);
            var tree = new Color(s.Ink.r, s.Ink.g, s.Ink.b, 0.28f);
            var rnd = new System.Random(11);
            void Wood(Rect area, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    var p = new Vector2(area.x + (float)rnd.NextDouble() * area.width, area.y + (float)rnd.NextDouble() * area.height);
                    var k = 4 + (float)rnd.NextDouble() * 3;
                    Stroke(g, new List<Vector2> { p + new Vector2(-k, k * 1.2f), p + new Vector2(0, -k * 1.2f), p + new Vector2(k, k * 1.2f) }, tree, 1.1f);
                    Stroke(g, new List<Vector2> { p + new Vector2(0, k * 1.2f), p + new Vector2(0, k * 1.9f) }, tree, 1.1f);
                }
            }
            Wood(new Rect(w * 0.05f, h * 0.04f, w * 0.26f, h * 0.22f), 26);
            Wood(new Rect(w * 0.68f, h * 0.04f, w * 0.27f, h * 0.22f), 26);
            Wood(new Rect(w * 0.05f, h * 0.86f, w * 0.3f, h * 0.11f), 16);
            Wood(new Rect(w * 0.64f, h * 0.86f, w * 0.32f, h * 0.11f), 16);
            // Hills along the top: little arcs with their hatching.
            for (var x = w * 0.36f; x < w * 0.62f; x += 26)
            {
                var hill = new List<Vector2>();
                for (var i = 0; i <= 8; i++) hill.Add(new Vector2(x + i * 3, 22 - Mathf.Sin(i / 8f * Mathf.PI) * 10));
                Stroke(g, hill, tree, 1.1f);
            }
        }

        /// <summary>
        /// The ink: the ways (the far ones in pencil, the ones to take now in the marker, the way taken in ink gone over twice;
        /// on the road map, roads: grey, yellow, red), the rings round the stages and the base (the boss in red, the far ones in
        /// pencil, the picked and the open in the marker), «AQUÍ»'s pin.
        /// </summary>
        static void PaperInk(MeshGenerationContext ctx, string kind, RunMapView v, Dictionary<string, Vector2> at, RunNodeView pick)
        {
            var s = SheetOf(kind);
            var g = ctx.painter2D;
            g.lineCap = LineCap.Round;
            g.lineJoin = LineJoin.Round;
            var byId = v.Nodes.ToDictionary(n => n.Id);
            Vector2 From(string id, Vector2 p) => p - new Vector2(0, id == "start" ? 21 : PaperRadius(byId[id]) + 3);
            Vector2 To(string id, Vector2 p) => p + new Vector2(0, PaperRadius(byId[id]) + 3);
            var all = v.Links.Where(l => l.From != null && l.To != null && at.ContainsKey(l.From) && at.ContainsKey(l.To)).Select(l => (a: From(l.From, at[l.From]), b: To(l.To, at[l.To]), seed: Seed(l.From + l.To), l.Taken, l.Open, l.Dim)).ToList();
            foreach (var n in v.Nodes.Where(n => n.Row == 1)) all.Add((From("start", at["start"]), To(n.Id, at[n.Id]), Seed("start" + n.Id), v.Nodes.Any(x => x.Row == 1 && x.Done) && n.Done, v.AtStart, false));
            foreach (var pass in new[] { 0, 1, 2 })
                foreach (var (a, b, seed, taken, open, dim) in all)
                {
                    if ((taken ? 2 : open ? 1 : 0) != pass) continue;
                    var line = Wobble(a, b, seed, s.Roads ? 0.6f : 1.6f);
                    if (s.Roads)
                    {
                        // A road map's roads: a cased road, the way taken red, the ones to take yellow, the rest thin grey.
                        if (taken) { Stroke(g, line, s.Ink, 7); Stroke(g, line, s.Red, 4.5f); }
                        else if (open) { Stroke(g, line, s.Ink, 7); Stroke(g, line, s.Mark, 4.5f); }
                        else { Stroke(g, line, new Color(s.Pencil.r, s.Pencil.g, s.Pencil.b, dim ? 0.35f : 0.6f), 3); Stroke(g, line, s.Paper, 1.4f); }
                        continue;
                    }
                    if (taken)
                    {
                        Stroke(g, line, s.Ink, 2.4f);
                        Stroke(g, Wobble(a, b, seed + 5, 1.8f), new Color(s.Ink.r, s.Ink.g, s.Ink.b, 0.75f), 1.6f);
                    }
                    else if (open) Stroke(g, line, s.Mark, 3.4f);
                    else Stroke(g, line, new Color(s.Pencil.r, s.Pencil.g, s.Pencil.b, s.Pencil.a * (dim ? 0.55f : 0.85f)), 1.3f);
                }
            // The rings.
            foreach (var n in v.Nodes)
            {
                var p = at[n.Id];
                var r = PaperRadius(n);
                var seed = Seed(n.Id);
                if (n.Hidden)
                {
                    // Not known yet: a dashed pencil ring.
                    var ring = Ring(p, r - 4, seed);
                    for (var i = 0; i + 1 < ring.Count; i += 3) Stroke(g, new List<Vector2> { ring[i], ring[i + 1] }, s.Pencil, 1.2f);
                    continue;
                }
                var picked = n.Id == pick?.Id;
                var quiet = !n.Open && !n.At && !n.Done && !n.Boss;
                var colour = n.Boss ? s.Red : picked || n.Open ? (s.Roads ? s.Ink : s.Mark) : quiet ? s.Pencil : s.Ink;
                var width = quiet ? 1.3f : n.Boss || picked ? 2.6f : 2f;
                // Paper under the ring, so the drawing doesn't show through the symbol.
                Dot(g, p, r, new Color(s.Paper.r, s.Paper.g, s.Paper.b, 0.85f));
                Stroke(g, Ring(p, r, seed), colour, width);
                if (n.Boss || picked || n.At) Stroke(g, Ring(p, r + 3, seed + 7), colour, width * 0.7f);
                if (s.Roads && (picked || n.Open)) Stroke(g, Ring(p, r + 3, seed + 7), s.Mark, 2.5f);
            }
            // The base: a double ring in ink.
            var home = at["start"];
            Dot(g, home, 19, new Color(s.Paper.r, s.Paper.g, s.Paper.b, 0.85f));
            Stroke(g, Ring(home, 19, 3), s.Ink, 2.2f);
            Stroke(g, Ring(home, 22, 9), s.Ink, 1.4f);
            // «AQUÍ»: a pin stuck at the ring's top right, its shadow on the paper.
            var here = v.AtStart ? home + new Vector2(14, -14) : v.Nodes.FirstOrDefault(n => n.At) is RunNodeView at0 ? at[at0.Id] + new Vector2(PaperRadius(at0) * 0.7f, -PaperRadius(at0) * 0.7f) : (Vector2?)null;
            if (here is Vector2 pin)
            {
                Stroke(g, new List<Vector2> { pin + new Vector2(2, 3), pin + new Vector2(9, 13) }, new Color(0, 0, 0, 0.25f), 2);
                Stroke(g, new List<Vector2> { pin, pin + new Vector2(-5, 9) }, new Color(0.35f, 0.35f, 0.38f), 1.6f);
                Dot(g, pin + new Vector2(2, 2), 7.5f, new Color(0, 0, 0, 0.25f));
                Dot(g, pin, 7.5f, s.Red);
                Dot(g, pin + new Vector2(-2.5f, -2.5f), 2.4f, new Color(1, 1, 1, 0.55f));
            }
        }

        /// <summary>An icon in a colour: our drawings (LineIcon) take their holder's tint, the sprites their own.</summary>
        static void PaperIcon(VisualElement layer, Vector2 centre, string id, Func<string, Sprite> icon, float size, Color colour)
        {
            if (string.IsNullOrEmpty(id)) return;
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(holder, centre.x - size / 2, centre.y - size / 2);
            holder.style.width = holder.style.height = size;
            holder.style.unityBackgroundImageTintColor = colour;
            if (LineIcon.Has(id)) holder.Add(new LineIcon(id) { pickingMode = PickingMode.Ignore });
            else if (icon?.Invoke(id) is Sprite sprite) holder.style.backgroundImage = new StyleBackground(sprite);
            layer.Add(holder);
        }

        static string NoteWord(string id) => id == Modifiers.Night ? "de noche" : id == Modifiers.Storm ? "mal tiempo" : "horda despierta";

        /// <summary>
        /// A stage written on the map: its place's symbol in the ring (the kind's, for a stop and the boss), the kind small at its
        /// left; at its right the name («AQUÍ · Rescate»), under it the notes (de noche, mal tiempo, horda despierta) and «Sin
        /// plaza libre», in their inks; the far ones in pencil.
        /// </summary>
        static void PaperNode(VisualElement layer, string kind, RunNodeView n, Vector2 p, bool picked, RunState run, Func<string, Sprite> icon)
        {
            var s = SheetOf(kind);
            if (n.Hidden)
            {
                var q = Text(layer, "?", 18, s.Pencil, true);
                Abs(q, p.x - 5, p.y - 13);
                return;
            }
            var node = run.Nodes.First(x => x.Id == n.Id);
            var place = !n.Stop && !n.Boss && node.Site != null && LineIcon.Has("place:" + node.Site) ? "place:" + node.Site : null;
            var quiet = !n.Open && !n.At && !n.Done && !n.Boss;
            var ink = n.Boss ? s.Red : quiet ? s.Pencil : s.Ink;
            var r = PaperRadius(n);
            PaperIcon(layer, p, place ?? n.Icon, icon, r * 1.2f, ink);
            if (place != null) PaperIcon(layer, p + new Vector2(-r - 12, -r * 0.45f), n.Icon, icon, 17, ink);
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            Abs(words, p.x + r + 8, p.y - 10);
            layer.Add(words);
            var name = Text(words, (n.At ? "AQUÍ · " : "") + (n.Boss ? n.Name?.ToUpperInvariant() : n.Name), n.Boss ? 15 : 13, ink, n.Boss || picked || n.At);
            name.style.whiteSpace = WhiteSpace.NoWrap;
            if (picked) name.style.unityFontStyleAndWeight = FontStyle.Bold;
            foreach (var id in new[] { n.ModifierId, n.AddedId }.Where(x => x != null))
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                words.Add(line);
                var mark = new VisualElement { pickingMode = PickingMode.Ignore };
                mark.style.width = mark.style.height = 12;
                mark.style.marginRight = 3;
                mark.style.unityBackgroundImageTintColor = id == Modifiers.Awake ? s.Red : s.Note;
                if (LineIcon.Has(ModIcon(id))) mark.Add(new LineIcon(ModIcon(id)) { pickingMode = PickingMode.Ignore });
                else if (icon?.Invoke(ModIcon(id)) is Sprite sprite) mark.style.backgroundImage = new StyleBackground(sprite);
                line.Add(mark);
                Text(line, NoteWord(id), 11, id == Modifiers.Awake ? s.Red : s.Note, false).style.whiteSpace = WhiteSpace.NoWrap;
            }
            if (!string.IsNullOrEmpty(n.Warn)) Text(words, n.Warn, 10, s.Red, false).style.whiteSpace = WhiteSpace.NoWrap;
            if (n.Boss) Text(words, "FIN DEL CAPÍTULO", 10, s.Red, true);
        }

        static void PaperStart(VisualElement layer, string kind, RunMapView v, Vector2 p, Func<string, Sprite> icon)
        {
            var s = SheetOf(kind);
            PaperIcon(layer, p, "map:lata", icon, 24, s.Ink);
            var l = Text(layer, (v.AtStart ? "AQUÍ · " : "") + (v.Start ?? "La base").ToUpperInvariant(), 13, s.Ink, true);
            Abs(l, p.x + 28, p.y - 9);
            l.style.whiteSpace = WhiteSpace.NoWrap;
        }

        /// <summary>The legend on the same paper, in the same inks: the notes, and the three ways.</summary>
        static void PaperLegend(VisualElement layer, string kind, Func<string, Sprite> icon)
        {
            var s = SheetOf(kind);
            var row = new VisualElement();
            row.style.marginTop = 8;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;
            row.style.backgroundColor = s.Paper;
            row.style.paddingLeft = 12;
            row.style.paddingTop = row.style.paddingBottom = 6;
            layer.Add(row);
            Text(row, "LEYENDA", 10, s.Ink, true).style.marginRight = 14;
            foreach (var id in new[] { Modifiers.Night, Modifiers.Storm, Modifiers.Awake })
            {
                var m = new VisualElement();
                m.style.width = m.style.height = 13;
                m.style.marginRight = 4;
                m.style.unityBackgroundImageTintColor = id == Modifiers.Awake ? s.Red : s.Note;
                if (LineIcon.Has(ModIcon(id))) m.Add(new LineIcon(ModIcon(id)));
                else if (icon?.Invoke(ModIcon(id)) is Sprite sprite) m.style.backgroundImage = new StyleBackground(sprite);
                row.Add(m);
                var word = NoteWord(id);
                Text(row, char.ToUpperInvariant(word[0]) + word.Substring(1) + ": más botín", 11, s.Ink, false).style.marginRight = 14;
            }
            void Sample(Color colour, float height, string words, Color? casing = null)
            {
                var sw = new VisualElement();
                sw.style.width = 28;
                sw.style.height = height;
                sw.style.backgroundColor = colour;
                if (casing is Color c) Border(sw, c, 1.5f);
                sw.style.marginRight = 5;
                row.Add(sw);
                Text(row, words, 11, s.Ink, false).style.marginRight = 14;
            }
            if (s.Roads)
            {
                Sample(s.Mark, 6, "Se puede ir", s.Ink);
                Sample(s.Red, 6, "Recorrido", s.Ink);
                Sample(s.Paper, 4, "Más adelante", s.Pencil);
            }
            else
            {
                Sample(s.Mark, 3, "Se puede ir");
                Sample(s.Ink, 3, "Recorrido");
                Sample(s.Pencil, 1.5f, "Más adelante");
            }
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
