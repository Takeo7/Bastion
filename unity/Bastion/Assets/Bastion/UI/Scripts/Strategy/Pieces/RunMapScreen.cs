using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Gear;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The map of an expedition (H1; .dev-snapshots/gameplay/runs/PLAN-RUNS.md §3.1), in the dark (cuadro 143, Arturo 23/11:
    /// the paper of the cuadros 126–135 out, «el nuevo, en oscuro»: no paper, no valley drawn, no symbols by hand; the
    /// words below that say ink and paper are its light ink and its dark ground). The sheet fills the map's side, the rows from the base at the bottom to the boss
    /// at the top in straight columns over the whole width (the C3), and the stages and ways are part of the drawing: each
    /// stage its place's symbol in ink ringed by hand, its kind small at its left, its name and notes at its right on a halo
    /// of paper (no line of the valley under a word); the way taken gone over twice in ink, the ones to take now in the red
    /// marker, the far ones in pencil; «AQUÍ» a pin; what isn't seen yet a dashed pencil ring. Under the sheet the legend on
    /// the same paper. Around it the game's own, dark: the heading with «Volver a la base», and on the right the squad folded
    /// to its busts («Equipo ▸» opens the kits, KitPanel) over the stage's card in parts: the stage under the pointer, else
    /// the one picked, the one proposed to the vote or the first that can be taken; «Ir a esta etapa» there (OnNode: a vote
    /// with two). A click on a stage that can be taken picks it. A stop's plate over the sheet, the sheet dimmed under it.
    /// </summary>
    public sealed class RunMapScreen
    {
        /// <summary>The marks' radius (the ways stop at their rim), the base's.</summary>
        const float NodeR = 22, BossR = 30, StartR = 19;
        /// <summary>The kind's small mark at a stage's left (points; Producto, cuadro 135: 17 read barely, on the Duo 12 px).</summary>
        const float KindSide = 20;

        // In the dark since the cuadro 143 (Arturo, 23/11: «el nuevo, en oscuro»; the C3 of the cuadro 129 with all added
        // since): the map's ground under the words (Paper, the name kept), the light ink, the far stages at half light, the
        // ways to come faint, the red of the ways open now, the notes' blue, the boss's red; the game's colours (Mixta).
        static readonly Color Paper = new Color(0.067f, 0.078f, 0.086f), Edge = new Color(0.149f, 0.165f, 0.176f);
        static readonly Color Ink = new Color(0.933f, 0.941f, 0.918f), Far = new Color(0.631f, 0.655f, 0.616f);
        static readonly Color Pencil = new Color(1f, 1f, 1f, 0.26f), Marker = new Color(0.94f, 0.31f, 0.24f), Note = new Color(0.616f, 0.706f, 0.761f);
        static readonly Color BossInk = new Color(1f, 0.416f, 0.333f);

        readonly VisualElement backdrop;
        readonly VisualElement field;
        readonly VisualElement sheet;
        readonly VisualElement ink;
        readonly VisualElement marks;
        readonly VisualElement legend;
        readonly Label head;
        readonly Label line;
        readonly Button back;
        /// <summary>Where the button went (G22, U21): «Se vuelve a la base desde un Refugio…».</summary>
        readonly Label homeNote;
        readonly VisualElement side;
        readonly KitPanel squad;
        /// <summary>The squad's tabs and kits on the right (their busts: StrategyRoot).</summary>
        public KitPanel Kits => squad;
        readonly VisualElement card;
        readonly StopPlate stop;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;

        /// <summary>Where each mark is on the sheet as last laid out («start»: the base), and the ways as drawn.</summary>
        readonly Dictionary<string, Vector2> at = new Dictionary<string, Vector2>();
        readonly List<(Vector2 a, Vector2 b, float seed, int kind, int row)> ways = new List<(Vector2, Vector2, float, int, int)>();
        Vector2 laidFor;

        /// <summary>The stage under the pointer, and the one picked with a click (the card shows the first of them that is there).</summary>
        string hovered;
        string picked;

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
        const float RowFirst = 0.25f, RowGap = 0.2f, Pop = 0.3f, WayTime = 0.22f;
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
            var top = Ui.El("runmap-top", plate);
            head = Ui.Text("", "runmap-head font-title", top);
            line = Ui.Text("", "runmap-line muted", top);
            field = Ui.El("runmap-field", plate);
            sheet = Ui.El("runmap-sheet", field);
            sheet.pickingMode = PickingMode.Ignore;
            ink = Ui.El("runmap-ink", field);
            ink.pickingMode = PickingMode.Ignore;
            ink.generateVisualContent += DrawInk;
            marks = Ui.El("runmap-marks", field);
            marks.pickingMode = PickingMode.Ignore;
            field.RegisterCallback<GeometryChangedEvent>(_ => Lay());
            legend = Ui.El("runmap-legend", plate);
            // At the foot, «Volver a la base» and where one goes back from (the proposal's plate comes down over the top).
            var foot = Ui.El("runmap-foot", plate);
            back = new Button(() => handlers.OnEndRun());
            Ui.AddClasses(back, "runmap-back font-title");
            foot.Add(back);
            homeNote = Ui.Text("", "runmap-home muted", foot);
            stop = new StopPlate(plate, tips, icon, handlers) { Floor = NextRowTop };
            Sheet();
            // The right: the stage's card beside the map, and the chosen one's kit open always (Arturo, 25/11, O1: «no quiero
            // botón, que salga directamente»): its places, its pack and its board with the points to spend, no «Equipo ▸».
            side = Ui.El("runmap-side", view);
            card = Ui.El("panel runmap-card", side);
            squad = new KitPanel(side, "panel runmap-squad", tips, handlers, board) { Icon = icon };
            Show(false);
        }

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(backdrop, on);
            Ui.Show(side, on && view != null);
        }

        public void Set(RunMapView v)
        {
            view = v;
            Ui.Show(side, shown && v != null);
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
            // A stop over the map, the sheet dimmed under it; its plate is what to look at, not a stage's card.
            stop.Set(v.Stop);
            field.EnableInClassList("runmap-field--under", v.Stop != null);
            Legend(v);
            // The map anew only if it changed (the kits change more often).
            var k = Ui.Key(v.Nodes) + Ui.Key(v.Links) + v.AtStart + v.Start + v.Proposed;
            if (k != key)
            {
                key = k;
                if (picked != null && !v.Nodes.Any(n => n.Id == picked && n.Open)) picked = null;
                hovered = null;
                // A new expedition's map, the first time it is seen: built (T9).
                if (v.Fresh && v.Run != null && v.Run != built)
                {
                    built = v.Run;
                    buildAt = Time.time;
                    buildRows = v.Nodes.Count > 0 ? v.Nodes.Max(n => n.Row) : 0;
                }
                else if (!Building) buildAt = -1;
                laidFor = Vector2.zero;
                Lay();
            }
            ShowCard();
        }

        // ------------------------------------------------------------ the sheet

        /// <summary>The map's ground: plain dark (cuadro 143: no paper, no valley drawn).</summary>
        void Sheet()
        {
            sheet.style.backgroundColor = Paper;
        }

        // ------------------------------------------------------------ where each goes

        /// <summary>
        /// The marks laid out for the sheet's size, once it has one and whenever it changes (the window resized): the C3's,
        /// the base at the foot, the rows up to the boss, each row's stages spread over the whole width. Every chapter has
        /// RunMap.Rows rows: the rows close up to fit the sheet, never scrolling.
        /// </summary>
        void Lay()
        {
            var size = field.contentRect.size;
            if (view == null || float.IsNaN(size.x) || size.x <= 0 || size.y <= 0 || size == laidFor) return;
            laidFor = size;
            var r = new Rect(34, 30, size.x - 68, size.y - 56);
            var rows = Math.Max(1, view.Nodes.Count > 0 ? view.Nodes.Max(n => n.Row) : RunMap.Rows);
            var dy = (r.height - 20) / (rows + 0.6f);
            at.Clear();
            at["start"] = new Vector2(r.center.x, r.yMax - 10);
            foreach (var n in view.Nodes)
            {
                var gap = r.width / Math.Max(1, n.Cols);
                at[n.Id] = new Vector2(r.center.x + (n.Col - (n.Cols - 1) / 2f) * gap, r.yMax - 10 - dy * n.Row);
            }
            Draw(view);
            stop?.Refit();
        }

        static float RadiusOf(RunNodeView n) => n.Boss ? BossR : NodeR;

        /// <summary>
        /// The top of the stages one may go to next (the row over the one here: its rings and their kind's marks), in the
        /// plate's pixels; NaN, none. The Trueque's plate ends over it (Pruebas, 0.14.4: at 1920 it covered their top).
        /// </summary>
        float NextRowTop()
        {
            if (view == null || float.IsNaN(field.layout.y)) return float.NaN;
            var here = view.AtStart ? 0 : view.Nodes.FirstOrDefault(n => n.At)?.Row ?? -1;
            var next = here < 0 ? null : view.Nodes.Where(n => n.Row == here + 1 && at.ContainsKey(n.Id)).ToList();
            if (next == null || next.Count == 0) return float.NaN;
            return field.layout.y + next.Min(n => at[n.Id].y - Math.Max(RadiusOf(n), RadiusOf(n) * 0.45f + KindSide / 2f)) - 4;
        }

        void Draw(RunMapView v)
        {
            marks.Clear();
            rising.Clear();
            var byId = v.Nodes.ToDictionary(n => n.Id);
            Vector2 From(string id) => at[id] - new Vector2(0, id == "start" ? StartR + 3 : RadiusOf(byId[id]) + 3);
            Vector2 To(string id) => at[id] + new Vector2(0, RadiusOf(byId[id]) + 3);
            ways.Clear();
            foreach (var l in v.Links)
            {
                var from = l.From ?? "start";
                if (!at.ContainsKey(from) || !at.ContainsKey(l.To)) continue;
                ways.Add((From(from), To(l.To), Seed(from + l.To), l.Taken ? 3 : l.Open ? 2 : l.Dim ? 0 : 1, byId[l.To].Row));
            }
            // The pencil under, the marker over it, the way taken on top.
            ways.Sort((x, y) => x.kind.CompareTo(y.kind));

            // The base: La Lata in ink, its name at its right.
            var home = at["start"];
            Symbol(home, "map:lata", 22, Ink);
            Words(home + new Vector2(StartR + 9, -9), (v.AtStart ? StrategyText.Runs.Here + " · " : "") + (v.Start ?? "").ToUpperInvariant(), Ink, true, 0);

            foreach (var n in v.Nodes)
            {
                var p = at[n.Id];
                var r = RadiusOf(n);
                var id = n.Id;
                var open = n.Open && n.Why == null;
                // The hit area: the ring, the stage picked with a click; a card for any stage under the pointer.
                VisualElement hit;
                if (open) hit = new Button(() => Pick(id));
                else hit = new VisualElement();
                Ui.AddClasses(hit, "rnode");
                // By its id, for the labs (BaseRun «pasa:rnode-n4-0», «pulsa:rnode-n4-0»).
                hit.name = "rnode-" + id;
                hit.EnableInClassList("rnode--open", open);
                hit.style.left = p.x - r - 4;
                hit.style.top = p.y - r - 4;
                hit.style.width = hit.style.height = 2 * r + 8;
                hit.RegisterCallback<PointerEnterEvent>(_ => Hover(id, true));
                hit.RegisterCallback<PointerLeaveEvent>(_ => Hover(id, false));
                marks.Add(hit);
                Rise(hit, n.Row);
                if (n.Hidden)
                {
                    // A «?» at half light.
                    Ui.Text("?", "rnode__q font-title", hit).pickingMode = PickingMode.Ignore;
                    continue;
                }
                var far = !n.Open && !n.At && !n.Done && !n.Boss;
                var colour = n.Boss ? BossInk : far ? Far : Ink;
                // The place's symbol in the ring (the kind's for a stop and the boss); the kind small at its left.
                Symbol(p, n.PlaceIcon ?? n.Icon, r * 1.2f, colour, hit);
                if (n.PlaceIcon != null) Rise(Symbol(p + new Vector2(-r - 4 - KindSide / 2, -r * 0.45f), n.Icon, KindSide, colour), n.Row);
                // At its right: its name and, under it, its notes (night, weather, horde, no seat, whose proposal).
                var name = (n.At ? StrategyText.Runs.Here + " · " : "") + (n.Boss ? n.Name?.ToUpperInvariant() : n.Name);
                var words = Words(p + new Vector2(r + 9, -10), name, colour, !far, n.Row);
                foreach (var m in new[] { n.ModifierId, n.AddedId }.Where(x => x != null))
                    Aside(words, m == Modifiers.Night ? "night" : m == Modifiers.Storm ? "weather:storm" : "node:" + m, Lore.ModifierName(m).ToLowerInvariant(), m == Modifiers.Awake ? Marker : Note, m == Modifiers.Awake ? Ink : Note);
                // «Sin plaza libre»: a warning, its mark red and its words in ink, not read as the red of a way (cuadro 132, 4).
                if (!string.IsNullOrEmpty(n.Warn)) Aside(words, null, n.Warn, Marker, Ink);
                if (n.Id == v.Proposed && !string.IsNullOrEmpty(v.ProposedLine)) Aside(words, null, v.ProposedLine, Ui.Hex(v.ProposedColor, Marker), Ink);
                if (n.Boss)
                {
                    var end = Ui.Text(StrategyText.Runs.ChapterEnd, "runmap-note runmap-note--end font-title", words);
                    end.pickingMode = PickingMode.Ignore;
                }
            }
            ink.MarkDirtyRepaint();
        }

        /// <summary>An icon in a colour, centred on `p` (our drawings take their holder's tint; Synty's sprites theirs).</summary>
        VisualElement Symbol(Vector2 p, string id, float size, Color colour, VisualElement into = null)
        {
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.AddToClassList("runmap-symbol");
            holder.style.width = holder.style.height = size;
            holder.style.unityBackgroundImageTintColor = colour;
            if (into != null)
            {
                // In its ring's hit area, centred there.
                holder.style.position = Position.Absolute;
                holder.style.left = (into.style.width.value.value - size) / 2;
                holder.style.top = (into.style.height.value.value - size) / 2;
                into.Add(holder);
            }
            else
            {
                holder.style.left = p.x - size / 2;
                holder.style.top = p.y - size / 2;
                marks.Add(holder);
            }
            if (string.IsNullOrEmpty(id)) return holder;
            Paint(holder, id);
            return holder;
        }

        /// <summary>
        /// An id's picture in `e`, tinted by `e`'s tint: our line drawing, else the HUD's icon (cuadro 143: no symbols by hand).
        /// </summary>
        void Paint(VisualElement e, string id)
        {
            if (LineIcon.Has(id)) e.Add(new LineIcon(id) { pickingMode = PickingMode.Ignore });
            else if (icon?.Invoke(id) is Sprite sprite) e.style.backgroundImage = new StyleBackground(sprite);
        }

        /// <summary>A stage's words at `p`: its name on a halo of paper (no line of the valley under it), its notes under it.</summary>
        VisualElement Words(Vector2 p, string name, Color colour, bool strong, int row)
        {
            var words = Ui.El("runmap-words-at", marks);
            words.pickingMode = PickingMode.Ignore;
            words.style.left = p.x;
            words.style.top = p.y;
            var l = Ui.Text(name, strong ? "runmap-name runmap-name--strong" : "runmap-name", words);
            l.pickingMode = PickingMode.Ignore;
            l.style.color = colour;
            Rise(words, row);
            return words;
        }

        /// <summary>A note under a name: its small mark (an icon, or a dot) in `mark`'s colour, its words in `colour`.</summary>
        void Aside(VisualElement words, string id, string text, Color mark, Color colour)
        {
            var row = Ui.El("runmap-note", words);
            row.pickingMode = PickingMode.Ignore;
            var m = Ui.El("runmap-note__mark", row);
            m.pickingMode = PickingMode.Ignore;
            m.style.unityBackgroundImageTintColor = mark;
            if (id != null) Paint(m, id);
            else
            {
                m.AddToClassList("runmap-note__mark--dot");
                m.style.backgroundColor = mark;
            }
            var l = Ui.Text(text, "runmap-note__text", row);
            l.pickingMode = PickingMode.Ignore;
            l.style.color = colour;
        }

        // ------------------------------------------------------------ the pointer, the pick, the card

        void Hover(string id, bool over)
        {
            if (over) hovered = id;
            else if (hovered == id) hovered = null;
            ink.MarkDirtyRepaint();
            ShowCard();
        }

        /// <summary>A click on a stage that can be taken: picked, its card kept (the way goes with «Ir a esta etapa»).</summary>
        void Pick(string id)
        {
            picked = id;
            ink.MarkDirtyRepaint();
            ShowCard();
        }

        /// <summary>
        /// The stage the card is about: under the pointer, picked, proposed to the vote; with none to choose now, where the squad
        /// is. Null, a choice to make and none picked: the card asks for one (cuadro 135: never a stage the player didn't mark).
        /// </summary>
        RunNodeView Shown()
        {
            if (view == null) return null;
            RunNodeView By(string id) => id == null ? null : view.Nodes.FirstOrDefault(n => n.Id == id);
            return By(hovered) ?? By(picked) ?? By(view.Proposed) ?? (view.Nodes.Any(n => n.Open) ? null : view.Nodes.FirstOrDefault(n => n.At));
        }

        /// <summary>
        /// The stage's card in parts (cuadros 126 and 132; Producto's review 16): «Etapa · fila 4», its symbol and name, its
        /// danger and modifiers as facts, what to do, where, its notes, a warning in amber, why it can't be taken; «Ir a esta
        /// etapa» on one that can. Beside the kit, always open (O1, 25/11); not at a stop.
        /// </summary>
        void ShowCard()
        {
            var n = Shown();
            var on = view != null && view.Stop == null;
            Ui.Show(card, on);
            card.Clear();
            if (!on) return;
            if (n == null)
            {
                // A choice to make, none marked: what to do, no key (cuadro 135).
                Ui.Text(StrategyText.Runs.PickHead.ToUpperInvariant(), "rcard__name font-title", card);
                Ui.Text(StrategyText.Runs.PickHint(Bastion.Client.GameInput.Touch), "rcard__body rcard__hint", card);
                return;
            }
            Ui.Text((n.Kicker ?? "").ToUpperInvariant(), "rcard__kicker font-title", card);
            var top = Ui.El("rcard__top", card);
            if (!n.Hidden)
            {
                var ic = Ui.El("rcard__icon", top);
                if (LineIcon.Has(n.Icon)) ic.Add(new LineIcon(n.Icon) { pickingMode = PickingMode.Ignore });
                else Ui.Icon(icon, n.Icon, "rcard__icon-sprite", ic);
            }
            Ui.Text(n.Hidden ? "?" : (n.Name ?? "").ToUpperInvariant(), "rcard__name font-title", top);
            var chips = Ui.El("rcard__chips", card);
            if (n.Danger > 0) Ui.Text(StrategyText.Runs.Danger(n.Danger).ToUpperInvariant(), "rcard__chip rcard__chip--danger" + Math.Min(3, n.Danger) + " font-title", chips);
            foreach (var m in new[] { n.ModifierId, n.AddedId }.Where(x => x != null))
                Ui.Text(Lore.ModifierName(m).ToUpperInvariant() + (m == n.AddedId && m != n.ModifierId ? " · SUCESO" : ""), "rcard__chip rcard__chip--" + m + " font-title", chips);
            if (!string.IsNullOrEmpty(n.What))
            {
                if (!n.Hidden) Ui.Text((view.WhatHead ?? "").ToUpperInvariant(), "rcard__head font-title", card);
                Ui.Text(n.What, "rcard__body", card);
            }
            if (!string.IsNullOrEmpty(n.Place))
            {
                Ui.Text((view.WhereHead ?? "").ToUpperInvariant(), "rcard__head font-title", card);
                var where = Ui.El("rcard__where", card);
                if (n.PlaceIcon != null && LineIcon.Has(n.PlaceIcon))
                {
                    var pi = Ui.El("rcard__place-icon", where);
                    pi.Add(new LineIcon(n.PlaceIcon) { pickingMode = PickingMode.Ignore });
                }
                var said = Ui.El("rcard__place-words", where);
                Ui.Text(n.Place, "rcard__place font-title", said);
                if (!string.IsNullOrEmpty(n.PlaceLine)) Ui.Text(n.PlaceLine, "rcard__body", said);
            }
            foreach (var note in n.Notes) Ui.Text(note, "rcard__body rcard__note", card);
            if (!string.IsNullOrEmpty(n.WarnLine)) Ui.Text(n.WarnLine, "rcard__warn", card);
            if (n.Id == view.Proposed && !string.IsNullOrEmpty(view.ProposedLine))
            {
                var whose = Ui.Text(view.ProposedLine, "rcard__whose font-title", card);
                whose.style.borderLeftColor = Ui.Hex(view.ProposedColor, Marker);
            }
            // A stage proposed to the vote is answered on the proposal's plate, not here.
            if (!n.Open || n.Id == view.Proposed) return;
            if (n.Why != null) Ui.Text(n.Why, "rcard__warn", card);
            // Only the one marked goes (cuadro 135): under the pointer, another says how to mark it.
            if (n.Id != picked)
            {
                if (n.Why == null) Ui.Text(StrategyText.Runs.ToPick(Bastion.Client.GameInput.Touch), "rcard__body rcard__hint", card);
                return;
            }
            var id = n.Id;
            var go = new Button(() => handlers.OnNode(id)) { text = (view.Go ?? "").ToUpperInvariant() };
            Ui.AddClasses(go, "rcard__go btn-primary font-title");
            go.SetEnabled(n.Why == null);
            card.Add(go);
        }

        // ------------------------------------------------------------ the legend

        string legendKey;

        /// <summary>Under the sheet, on the same paper and in the same inks: the notes, and the three ways.</summary>
        void Legend(RunMapView v)
        {
            var k = string.Join("|", v.Legend);
            if (k == legendKey) return;
            legendKey = k;
            legend.Clear();
            if (v.Legend.Count < 6) return;
            Ui.Text(StrategyText.Runs.LegendHead, "runmap-legend__head font-title", legend);
            var ids = new[] { "night", "weather:storm", "node:" + Modifiers.Awake };
            for (var i = 0; i < 3; i++)
            {
                var m = Ui.El("runmap-legend__mark", legend);
                m.style.unityBackgroundImageTintColor = i == 2 ? Marker : Note;
                Paint(m, ids[i]);
                Ui.Text(v.Legend[i], "runmap-legend__text", legend);
            }
            for (var i = 0; i < 3; i++)
            {
                // «Se puede ir», «Recorrido», «Más adelante»: the marker, the ink, the pencil.
                var kind = new[] { 2, 3, 1 }[i];
                var sample = Ui.El("runmap-legend__way", legend);
                sample.generateVisualContent += ctx =>
                {
                    var r = ctx.visualElement.contentRect;
                    var g = ctx.painter2D;
                    g.lineCap = LineCap.Round;
                    var a = new Vector2(2, r.height / 2);
                    var b = new Vector2(r.width - 2, r.height / 2 - 1);
                    Way(g, a, b, 3.3f, kind, 1);
                };
                Ui.Text(v.Legend[3 + i], "runmap-legend__text", legend);
            }
        }

        // ------------------------------------------------------------ the ink

        /// <summary>
        /// The ways, the rings, the pin, as drawn by hand (cuadro 132, 1: not ruled): each way the C3's curve a little
        /// unsteady, its width changing as a hand's pressure does; the way taken in ink and gone over a second time beside
        /// itself, the ones to take now in the red marker, the far ones in pencil; the rings not quite round nor closed,
        /// the picked and the one under the pointer gone over, the proposed one in its player's colour.
        /// </summary>
        void DrawInk(MeshGenerationContext ctx)
        {
            if (view == null || !at.ContainsKey("start")) return;
            var g = ctx.painter2D;
            g.lineCap = LineCap.Round;
            g.lineJoin = LineJoin.Round;
            var t = Time.time - buildAt;
            float Up(int row) => buildAt < 0 ? 1 : Mathf.Clamp01((t - RowAt(row) + WayTime) / WayTime);
            foreach (var (a, b, seed, kind, row) in ways)
            {
                var f = Up(row);
                if (f > 0) Way(g, a, b, seed, kind, f);
            }
            foreach (var n in view.Nodes)
            {
                if (!at.TryGetValue(n.Id, out var p) || Up(n.Row) < 1) continue;
                var r = RadiusOf(n);
                var seed = Seed(n.Id);
                if (n.Hidden)
                {
                    var dashed = Ring(p, r - 3, seed);
                    for (var i = 0; i + 1 < dashed.Count; i += 3) Hand(g, new List<Vector2> { dashed[i], dashed[i + 1] }, Pencil, 1.2f, seed, 0);
                    continue;
                }
                var open = n.Open && n.Why == null;
                var far = !n.Open && !n.At && !n.Done && !n.Boss;
                var colour = n.Boss ? Marker : open ? Marker : far ? Pencil : Ink;
                var width = far ? 1.3f : n.Boss ? 2.6f : 2.1f;
                // Paper under the ring, so that the valley doesn't run through the symbol.
                g.fillColor = new Color(Paper.r, Paper.g, Paper.b, 0.88f);
                g.BeginPath();
                g.Arc(p, r, 0, 360);
                g.Fill();
                Hand(g, Ring(p, r, seed), colour, width, seed, 0);
                var lit = n.Id == picked || n.Id == hovered;
                if (n.Boss || n.At || lit) Hand(g, Ring(p, r + 3.5f, seed + 7), lit && open ? Marker : colour, width * 0.75f, seed + 3, 0);
                if (n.Id == view.Proposed) Hand(g, Ring(p, r + 6.5f, seed + 11), Ui.Hex(view.ProposedColor, Marker), 2.2f, seed + 5, 0);
            }
            // The base: a double ring in ink.
            var home = at["start"];
            g.fillColor = new Color(Paper.r, Paper.g, Paper.b, 0.88f);
            g.BeginPath();
            g.Arc(home, StartR, 0, 360);
            g.Fill();
            Hand(g, Ring(home, StartR, 3), Ink, 2.2f, 3, 0);
            Hand(g, Ring(home, StartR + 3.5f, 9), Ink, 1.4f, 9, 0);
            // «AQUÍ»: a pin stuck at the ring's top right, its shadow on the paper.
            var here = view.AtStart ? home + new Vector2(13, -13) : view.Nodes.FirstOrDefault(n => n.At) is RunNodeView now && at.TryGetValue(now.Id, out var np) ? np + new Vector2(RadiusOf(now) * 0.7f, -RadiusOf(now) * 0.7f) : (Vector2?)null;
            if (here is Vector2 pin && (buildAt < 0 || t > RowAt(0)))
            {
                g.strokeColor = new Color(0, 0, 0, 0.25f);
                g.lineWidth = 2;
                g.BeginPath();
                g.MoveTo(pin + new Vector2(2, 3));
                g.LineTo(pin + new Vector2(9, 13));
                g.Stroke();
                g.strokeColor = new Color(0.35f, 0.35f, 0.38f);
                g.lineWidth = 1.6f;
                g.BeginPath();
                g.MoveTo(pin);
                g.LineTo(pin + new Vector2(-5, 9));
                g.Stroke();
                Dot(g, pin + new Vector2(2, 2), 7.5f, new Color(0, 0, 0, 0.25f));
                Dot(g, pin, 7.5f, Marker);
                Dot(g, pin + new Vector2(-2.5f, -2.5f), 2.4f, new Color(1, 1, 1, 0.55f));
            }
        }

        /// <summary>A way of `kind` (0 unseen, 1 ahead, 2 to take now, 3 taken), as far as `f` of it.</summary>
        static void Way(Painter2D g, Vector2 a, Vector2 b, float seed, int kind, float f)
        {
            var line = Wobble(a, b, seed, kind >= 2 ? 1.6f : 1.2f);
            if (f < 1) line = line.Take(Mathf.Max(2, Mathf.CeilToInt(line.Count * f))).ToList();
            switch (kind)
            {
                case 3:
                    // The way taken: the light ink, once (cuadro 143: no hand gone over twice).
                    Hand(g, line, Ink, 2.5f, seed, 0);
                    break;
                case 2:
                    // The ways open now, in the red, a little wider.
                    Hand(g, line, Marker, 3f, seed, 0);
                    break;
                default:
                    // The far ones fine and faint, fainter still where nothing is seen yet (the C3's).
                    Hand(g, line, new Color(Pencil.r, Pencil.g, Pencil.b, Pencil.a * (kind == 0 ? 0.55f : 0.9f)), 1.35f, seed, 0);
                    break;
            }
        }

        /// <summary>
        /// A stroke by hand: piece by piece, its width changing along it (a hand's pressure: `pulse` of it, more in the middle
        /// than at the ends); `grain`, a pencil's, the pieces alternately fainter.
        /// </summary>
        static void Hand(Painter2D g, List<Vector2> pts, Color colour, float width, float seed, float pulse, bool grain = false)
        {
            if (pts.Count < 2) return;
            for (var i = 1; i < pts.Count; i++)
            {
                var t = i / (float)(pts.Count - 1);
                var pressure = 1 + (Mathf.PerlinNoise(t * 6.5f, seed * 1.7f) - 0.5f) * 2 * pulse;
                // (No hand since the cuadro 143: `pulse` 0 everywhere, the same width all along.)
                var taper = pulse > 0 ? Mathf.Lerp(0.7f, 1, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 1.4f) : 1;
                var c = colour;
                if (grain) c.a *= 0.65f + 0.35f * Mathf.PerlinNoise(i * 0.9f, seed);
                g.strokeColor = c;
                g.lineWidth = Mathf.Max(0.6f, width * pressure * Mathf.Min(1, taper));
                g.BeginPath();
                g.MoveTo(pts[i - 1]);
                g.LineTo(pts[i]);
                g.Stroke();
            }
        }

        static void Dot(Painter2D g, Vector2 c, float r, Color colour)
        {
            g.fillColor = colour;
            g.BeginPath();
            g.Arc(c, r, 0, 360);
            g.Fill();
        }

        /// <summary>A steady number from an id (string.GetHashCode isn't steady from one run to the next).</summary>
        static float Seed(string id)
        {
            var h = 17;
            foreach (var ch in id ?? "") h = unchecked(h * 31 + ch);
            return (h & 0xffff) / 6553.5f;
        }

        static Vector2 Bez(Vector2 a, Vector2 b, float t)
        {
            var c1 = new Vector2(a.x, (a.y + b.y) / 2);
            var c2 = new Vector2(b.x, (a.y + b.y) / 2);
            var u = 1 - t;
            return u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b;
        }

        /// <summary>A way's line by hand: the C3's curve, unsteady in the middle, steady at its ends.</summary>
        static List<Vector2> Wobble(Vector2 a, Vector2 b, float seed, float amp)
        {
            var pts = new List<Vector2>();
            var len = Vector2.Distance(a, b);
            var steps = Mathf.Max(8, Mathf.CeilToInt(len / 5));
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var d = Bez(a, b, Mathf.Min(1, t + 0.01f)) - Bez(a, b, Mathf.Max(0, t - 0.01f));
                var normal = new Vector2(-d.y, d.x).normalized;
                // (Steady since the cuadro 143: the C3's curve as it is.)
                var w = 0f;
                pts.Add(Bez(a, b, t) + normal * w);
            }
            return pts;
        }

        /// <summary>A stage's ring: round and closed (cuadro 143: no ring by hand).</summary>
        static List<Vector2> Ring(Vector2 c, float r, float seed)
        {
            var pts = new List<Vector2>();
            const int n = 48;
            for (var i = 0; i <= n; i++)
            {
                var a = Mathf.PI * 2 * i / n;
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return pts;
        }

        // ------------------------------------------------------------ building (T9)

        bool Building => buildAt >= 0 && Time.time - buildAt < RowAt(buildRows) + Pop;

        /// <summary>A stage's mark (or its words) of `row`, to come up as the map builds: hidden till its time (Grow).</summary>
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

        /// <summary>The build's frame: each mark as far as its row has come, the ink redrawn; done, all as they are.</summary>
        void Tick()
        {
            var t = Time.time - buildAt;
            foreach (var (e, row) in rising) Grow(e, row, t);
            ink.MarkDirtyRepaint();
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
    }
}
