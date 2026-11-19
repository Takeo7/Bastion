using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.UI.Gear;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The map of an expedition (H1; .dev-snapshots/gameplay/runs/PLAN-RUNS.md §3.1), flat, on its own ground with no
    /// clearing behind (Arte turns it off): the start at the bottom, five rows of stages, the Refugio and the boss at the
    /// top, the ways between them as lines. What isn't seen yet is dark, without kind nor modifier; the way taken is white;
    /// the stages the squad can go to now are lit, and a click on one goes there (OnNode: a vote with two). Each stage's
    /// card on hover. On the right, the squad: their health and their kits, which can be sorted between stages
    /// (KitPanel). Under the map, «Volver a la base» (OnEndRun).
    /// </summary>
    public sealed class RunMapScreen
    {
        const float MapW = 760, MapH = 690, Bottom = 46, Step = 88, ColGap = 150;
        const float NodeSide = 58, BossSide = 78, StartSide = 50;

        readonly VisualElement backdrop;
        readonly VisualElement field;
        readonly VisualElement links;
        readonly Label head;
        readonly Label line;
        readonly Button back;
        /// <summary>Where the button went (G22, U21): «Se vuelve a la base desde un Refugio…».</summary>
        readonly Label homeNote;
        readonly KitPanel squad;
        /// <summary>The squad's tabs and kits on the right (their busts: StrategyRoot).</summary>
        public KitPanel Kits => squad;
        readonly StopPlate stop;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        readonly List<(Vector2 a, Vector2 b, int kind, int row)> segments = new List<(Vector2, Vector2, int, int)>();

        /// <summary>
        /// The map building itself as an expedition starts (Arturo, 18/11 T9: «que el mapa al darle a empezar una run haga
        /// como una animación de construirse»): the rows from the bottom, each stage fading in and growing, the ways to it
        /// drawn from where they start just before; once an expedition (the last one built), under two seconds. On the
        /// battle's clock (the base's runs at 1): captures step it frame by frame, so a strip shows it.
        /// </summary>
        static string built;
        float buildAt = -1;
        int buildRows;
        IVisualElementScheduledItem building;
        readonly List<(VisualElement e, int row)> rising = new List<(VisualElement, int)>();
        const float RowFirst = 0.25f, RowGap = 0.2f, Pop = 0.3f, Way = 0.22f;
        static float RowAt(int row) => RowFirst + row * RowGap;
        string key;
        bool shown;
        RunMapView view;

        public RunMapScreen(VisualElement view, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers, GearBoard board)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            backdrop = Ui.El("runmap", view);
            var plate = Ui.El("runmap-plate", backdrop);
            head = Ui.Text("", "runmap-head font-title", plate);
            line = Ui.Text("", "runmap-line muted", plate);
            field = Ui.El("runmap-field", plate);
            field.style.width = MapW;
            field.style.height = MapH;
            links = Ui.El("runmap-links", field);
            links.pickingMode = PickingMode.Ignore;
            links.generateVisualContent += DrawLinks;
            back = new Button(() => handlers.OnEndRun());
            Ui.AddClasses(back, "runmap-back font-title");
            plate.Add(back);
            homeNote = Ui.Text("", "runmap-home muted", plate);
            stop = new StopPlate(plate, tips, icon, handlers);
            squad = new KitPanel(view, "strat-side strat-side--right panel stash-right runmap-squad", tips, handlers, board) { Icon = icon };
            Show(false);
        }

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(backdrop, on);
            Ui.Show(squad.Root, on && view != null);
        }

        public void Set(RunMapView v)
        {
            view = v;
            Ui.Show(squad.Root, shown && v != null);
            if (v == null) return;
            head.text = (v.Head ?? "").ToUpperInvariant();
            line.text = v.Line ?? "";
            Ui.Show(line, !string.IsNullOrEmpty(v.Line));
            Ui.Show(back, !string.IsNullOrEmpty(v.Back));
            back.text = (v.Back ?? "").ToUpperInvariant();
            back.SetEnabled(v.BackWhy == null);
            tips.Attach(back, v.BackWhy ?? v.BackTip);
            homeNote.text = v.HomeNote ?? "";
            Ui.Show(homeNote, !string.IsNullOrEmpty(v.HomeNote));
            squad.Set(v.Squad, v.Kit, v.SquadHead);
            // A stop over the map, the map dimmed under it.
            stop.Set(v.Stop);
            field.EnableInClassList("runmap-field--under", v.Stop != null);
            // The map anew only if it changed (the kits change more often).
            var k = Ui.Key(v.Nodes) + Ui.Key(v.Links) + v.AtStart + v.Start;
            if (k == key) return;
            key = k;
            Draw(v);
        }

        static Vector2 Place(int row, int col, int cols) => new Vector2(MapW / 2 + (col - (cols - 1) / 2f) * ColGap, MapH - Bottom - row * Step);

        void Draw(RunMapView v)
        {
            foreach (var e in new List<VisualElement>(field.Children()))
                if (e != links) e.RemoveFromHierarchy();
            var at = new Dictionary<string, Vector2>();
            var start = Place(0, 0, 1);
            foreach (var n in v.Nodes) at[n.Id] = Place(n.Row, n.Col, Math.Max(1, n.Cols));

            // A new expedition's map, the first time it is seen: built (T9).
            if (v.Fresh && v.Run != null && v.Run != built)
            {
                built = v.Run;
                buildAt = Time.time;
                buildRows = v.Nodes.Count > 0 ? v.Nodes.Max(n => n.Row) : 0;
            }
            else if (!Building) buildAt = -1;
            rising.Clear();
            var rowOf = v.Nodes.ToDictionary(n => n.Id, n => n.Row);
            segments.Clear();
            foreach (var l in v.Links)
            {
                var a = l.From == null ? start : at.TryGetValue(l.From, out var pa) ? pa : start;
                if (!at.TryGetValue(l.To, out var b)) continue;
                segments.Add((a, b, l.Taken ? 3 : l.Open ? 2 : l.Dim ? 0 : 1, rowOf.TryGetValue(l.To, out var row) ? row : 0));
            }
            // The dim under, the way taken over all.
            segments.Sort((x, y) => x.kind.CompareTo(y.kind));
            links.MarkDirtyRepaint();

            // The start: the base, where the squad sets off from in La Lata.
            var home = Ui.El("rnode rnode--start", field);
            Round(home, start, StartSide);
            home.EnableInClassList("rnode--at", v.AtStart);
            IconIn(home, "map:lata", "rnode__icon");
            Name(start, StartSide, v.Start, "rnode__name");
            if (v.AtStart) Here(start, StartSide);

            foreach (var n in v.Nodes)
            {
                var p = at[n.Id];
                var side = n.Boss ? BossSide : NodeSide;
                var id = n.Id;
                var open = n.Open && n.Why == null;
                VisualElement el;
                if (open) el = new Button(() => handlers.OnNode(id));
                else el = new VisualElement();
                Ui.AddClasses(el, "rnode");
                // By its id, for the labs (BaseRun «ayuda:rnode-n4-0»: its card pinned open).
                el.name = "rnode-" + id;
                field.Add(el);
                Round(el, p, side);
                el.EnableInClassList("rnode--hidden", n.Hidden);
                el.EnableInClassList("rnode--stop", n.Stop && !n.Hidden);
                el.EnableInClassList("rnode--boss", n.Boss);
                el.EnableInClassList("rnode--done", n.Done && !n.At);
                el.EnableInClassList("rnode--at", n.At);
                el.EnableInClassList("rnode--open", open);
                el.EnableInClassList("rnode--blocked", n.Open && n.Why != null);
                // (Tests pin a modifier's card by it.)
                el.EnableInClassList("rnode--mod", !n.Hidden && n.ModifierId != null);
                if (n.Hidden) Ui.Text("?", "rnode__q font-title", el).pickingMode = PickingMode.Ignore;
                else IconIn(el, n.Icon, "rnode__icon");
                if (!n.Hidden && !string.IsNullOrEmpty(n.ModifierIcon))
                {
                    var badge = Ui.El("rnode__mod rnode__mod--" + (n.ModifierId ?? "x"), el);
                    badge.pickingMode = PickingMode.Ignore;
                    IconIn(badge, n.ModifierIcon, "rnode__mod-icon");
                }
                // An event's (H4), on the other side, ringed: it comes from the way, not from the stage.
                // A special map: its place's drawing, low on the right, apart from the modifiers' (Arturo, 17/11).
                if (!n.Hidden && !string.IsNullOrEmpty(n.SiteIcon))
                {
                    var site = Ui.El("rnode__site", el);
                    site.pickingMode = PickingMode.Ignore;
                    IconIn(site, n.SiteIcon, "rnode__site-icon");
                }
                if (!n.Hidden && !string.IsNullOrEmpty(n.AddedIcon))
                {
                    var badge = Ui.El("rnode__mod rnode__mod--added rnode__mod--" + (n.AddedId ?? "x"), el);
                    badge.pickingMode = PickingMode.Ignore;
                    IconIn(badge, n.AddedIcon, "rnode__mod-icon");
                }
                if (!n.Hidden) Rise(Name(p, side, n.Name, open ? "rnode__name rnode__name--open" : "rnode__name"), n.Row);
                // A Rescate with no seat left (N2): said under its name, before choosing it.
                if (!n.Hidden && !string.IsNullOrEmpty(n.Warn)) Rise(Name(p + new Vector2(0, 15), side, n.Warn, "rnode__name rnode__warn"), n.Row);
                Rise(el, n.Row);
                if (n.At) Here(p, side);
                var node = n;
                var card = node.Card.FindAll(l => !string.IsNullOrEmpty(l));
                if (node.Why != null) card.Add(node.Why);
                if (!n.Hidden || card.Count > 0) tips.Attach(el, () => TopStrip.Tip(node.Hidden ? null : node.Name, card));
            }
        }

        /// <summary>A round mark of `side` points centred on `p`.</summary>
        static void Round(VisualElement e, Vector2 p, float side)
        {
            e.style.left = p.x - side / 2;
            e.style.top = p.y - side / 2;
            e.style.width = side;
            e.style.height = side;
            var r = side / 2;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        /// <summary>A stage's name under it.</summary>
        Label Name(Vector2 p, float side, string text, string classes)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var l = Ui.Text(text, classes, field);
            l.pickingMode = PickingMode.Ignore;
            l.style.left = p.x - 80;
            l.style.top = p.y + side / 2 + 3;
            return l;
        }

        bool Building => buildAt >= 0 && Time.time - buildAt < RowAt(buildRows) + Pop;

        /// <summary>A stage's mark (or its name) of `row`, to come up as the map builds: hidden till its time (Grow).</summary>
        void Rise(VisualElement e, int row)
        {
            if (e == null || buildAt < 0) return;
            rising.Add((e, row));
            Grow(e, row, Time.time - buildAt);
            building ??= field.schedule.Execute(Tick).Every(16);
            building.Resume();
        }

        static void Grow(VisualElement e, int row, float t)
        {
            var f = Mathf.Clamp01((t - RowAt(row)) / Pop);
            var eased = 1 - (1 - f) * (1 - f);
            e.style.opacity = f;
            var s = Mathf.Lerp(0.4f, 1, eased);
            e.style.scale = new Scale(new Vector3(s, s, 1));
        }

        /// <summary>The build's frame: each mark as far as its row has come, the ways redrawn; done, all as they are.</summary>
        void Tick()
        {
            var t = Time.time - buildAt;
            foreach (var (e, row) in rising) Grow(e, row, t);
            links.MarkDirtyRepaint();
            if (Building) return;
            foreach (var (e, _) in rising)
            {
                e.style.opacity = StyleKeyword.Null;
                e.style.scale = StyleKeyword.Null;
            }
            rising.Clear();
            buildAt = -1;
            building?.Pause();
        }

        /// <summary>«Aquí» to the left of where the squad is (over it, it met the name of the stage above).</summary>
        void Here(Vector2 p, float side)
        {
            var l = Ui.Text(StrategyText.Runs.Here, "rnode__here font-title", field);
            l.pickingMode = PickingMode.Ignore;
            l.style.left = p.x - side / 2 - 8 - 160;
            l.style.top = p.y - 9;
        }

        /// <summary>An icon of the HUD's table, or one of our line drawings.</summary>
        void IconIn(VisualElement parent, string id, string classes)
        {
            if (LineIcon.Has(id))
            {
                var l = new LineIcon(id) { pickingMode = PickingMode.Ignore };
                Ui.AddClasses(l, classes);
                parent.Add(l);
            }
            else Ui.Icon(icon, id, classes, parent);
        }

        void DrawLinks(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            var t = Time.time - buildAt;
            foreach (var (a, b, kind, row) in segments)
            {
                // Building: each way drawn from where it starts, just before its stage comes up (T9).
                var f = buildAt < 0 ? 1 : Mathf.Clamp01((t - RowAt(row) + Way) / Way);
                if (f <= 0) continue;
                switch (kind)
                {
                    case 0: p.strokeColor = new Color(1, 1, 1, 0.08f); p.lineWidth = 2; break;
                    case 1: p.strokeColor = new Color(1, 1, 1, 0.26f); p.lineWidth = 2; break;
                    case 2: p.strokeColor = new Color(0.56f, 0.82f, 0.35f, 0.95f); p.lineWidth = 3; break;
                    default: p.strokeColor = Color.white; p.lineWidth = 4; break;
                }
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(Vector2.Lerp(a, b, f));
                p.Stroke();
            }
        }
    }
}
