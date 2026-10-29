using System;
using System.Collections.Generic;
using Bastion.Engine;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The skill board (hexBoard.ts of the 0.10.1): the engine's 169 cells as pointy hexagons round the core, inside a
    /// ring with six spokes and ticks every 5°; bought ones in their line's colour, those beside them dull with their
    /// icon, the rest dark and blank. Over a cell, its card; held for a second, an open cell is bought (with a point to
    /// spend). The cells near the pointer rise (the wave); the arrows move between neighbours. The camera is fixed: the
    /// board fits its box.
    /// </summary>
    public sealed class SkillBoard : VisualElement
    {
        /// <summary>Board units (hexBoard.ts): a hexagon's size, the gap between them, the ring's radius.</summary>
        const float CellSize = 30f;
        const float Gap = 3.2f;
        const float Outer = 424f;
        /// <summary>How long a cell is held to buy it.</summary>
        const float HoldSeconds = 1f;
        /// <summary>The wave (hexBoard.ts LIFT, REACH): how high a cell rises (board units), how far it reaches (in cells' radii).</summary>
        const float Lift = 7f;
        const float Reach = 3.4f;
        /// <summary>Bought (hexBoard.css hx-bounce, hx-pulse): the bounce's and the pulse's seconds.</summary>
        const float BounceSeconds = 0.42f;
        const float PulseSeconds = 0.75f;

        static Texture2D grey, dark, core;
        static Sprite coreIcon;
        static readonly Dictionary<string, Sprite> greyIcons = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> darkIcons = new Dictionary<string, Sprite>();

        readonly Dictionary<string, HexCell> cells = new Dictionary<string, HexCell>();
        readonly Action<string> sound;
        float scale = 1;
        Vector2 middle;
        Vector2? pointer;
        HexCell held;
        float heldSince;
        IReadOnlyList<string> board = new List<string>();
        int points;
        bool editable;
        /// <summary>The focus moved by the keys (the web's :focus-visible): only then the focused cell has its white ring.</summary>
        bool keyboard;
        HexCell ringed;

        /// <summary>The cell the card shows (over, focused); null, the core's.</summary>
        public event Action<string> Shown;
        /// <summary>A cell bought (held a second).</summary>
        public event Action<string> Bought;

        public SkillBoard(Action<string> sound)
        {
            this.sound = sound;
            AddToClassList("skill-board-area");
            generateVisualContent += DrawRing;
            LoadAtlases();
            // Outer cells first (hexBoard.ts): the inner ones, drawn later, lie over a rising neighbour's shadow.
            foreach (var c in System.Linq.Enumerable.OrderByDescending(Board.Cells, x => x.Ring))
            {
                var cell = new HexCell(c);
                cells[c.Id] = cell;
                Add(cell);
                cell.RegisterCallback<PointerEnterEvent>(_ => Show(cell));
                cell.RegisterCallback<FocusInEvent>(_ => Show(cell));
                cell.RegisterCallback<PointerDownEvent>(e =>
                {
                    keyboard = false;
                    StartHold(cell, e.pointerId);
                });
                cell.RegisterCallback<PointerUpEvent>(_ => StopHold());
                cell.RegisterCallback<PointerLeaveEvent>(_ => StopHold());
                cell.RegisterCallback<KeyDownEvent>(e => Key(cell, e));
                cell.RegisterCallback<KeyUpEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space) StopHold();
                });
            }
            RegisterCallback<GeometryChangedEvent>(_ => Lay());
            RegisterCallback<PointerMoveEvent>(e => pointer = e.localPosition);
            RegisterCallback<PointerDownEvent>(_ => keyboard = false, TrickleDown.TrickleDown);
            RegisterCallback<PointerLeaveEvent>(_ => pointer = null);
            schedule.Execute(Tick).Every(16);
        }

        /// <summary>The soldier's cells and points; editable, theirs to spend.</summary>
        public void Set(IReadOnlyList<string> unlocked, int pointsLeft, bool canEdit)
        {
            var before = board;
            board = unlocked ?? new List<string>();
            // Those just bought bounce and pulse (hexBoard.ts celebrate: new in the data).
            if (before.Count > 0)
                foreach (var id in board)
                    if (!System.Linq.Enumerable.Contains(before, id) && cells.TryGetValue(id, out var bought)) bought.BoughtAt = Time.unscaledTime;
            points = pointsLeft;
            editable = canEdit;
            foreach (var pair in cells)
            {
                var c = pair.Value;
                var state = Bars.CellState(board, pair.Key);
                c.State = state;
                c.Buyable = editable && state == "open" && points > 0;
                c.Icon.style.backgroundImage = state == "locked" ? new StyleBackground((Sprite)null) : new StyleBackground(IconOf(pair.Key, state == "unlocked"));
                c.focusable = state != "locked";
                c.MarkDirtyRepaint();
            }
        }

        public static void LoadAtlases()
        {
            if (grey != null) return;
            grey = Resources.Load<Texture2D>("Board/grey");
            dark = Resources.Load<Texture2D>("Board/dark");
            core = Resources.Load<Texture2D>("Board/core");
        }

        /// <summary>A cell's icon: dark on a bought cell's colour, grey on a dull one; the core's own picture.</summary>
        public static Sprite IconOf(string id, bool bought)
        {
            if (id == Board.Core)
            {
                if (coreIcon == null && core != null) coreIcon = Sprite.Create(core, new Rect(0, 0, core.width, core.height), Vector2.one * 0.5f);
                return coreIcon;
            }
            var table = bought ? darkIcons : greyIcons;
            if (table.TryGetValue(id, out var s)) return s;
            var atlas = bought ? dark : grey;
            if (atlas == null || !BoardIconIndex.Index.TryGetValue(id, out var i)) return null;
            // The cell's size from the texture as Unity has it (it may scale a 1664 px atlas to a power of two).
            var w = atlas.width / (float)BoardIconIndex.Cols;
            var h = atlas.height / (float)BoardIconIndex.Rows;
            var col = i % BoardIconIndex.Cols;
            var row = i / BoardIconIndex.Cols;
            // Texture rows go up from the bottom.
            s = Sprite.Create(atlas, new Rect(col * w, atlas.height - (row + 1) * h, w, h), Vector2.one * 0.5f);
            return table[id] = s;
        }

        /// <summary>The board fitted to its box: each cell at its axial place (pointy tops), scaled.</summary>
        void Lay()
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            scale = Mathf.Min(r.width, r.height) / (2 * Outer + 40);
            middle = r.center;
            // A pointy hexagon of radius R (its size less half the gap): 2R high, √3·R wide.
            var radius = (CellSize - Gap / 2) * scale;
            var width = Mathf.Sqrt(3) * radius;
            foreach (var pair in cells)
            {
                var c = pair.Value;
                var at = middle + Axial(c.Cell.Q, c.Cell.R) * scale;
                c.Centre = at;
                c.style.left = at.x - width / 2;
                c.style.top = at.y - radius;
                c.style.width = width;
                c.style.height = radius * 2;
            }
            MarkDirtyRepaint();
        }

        /// <summary>A cell's centre in board units (hexBoard.ts centre): pointy tops, the first sector (Disparo) on top, r going up.</summary>
        static Vector2 Axial(int q, int r) => new Vector2(CellSize * Mathf.Sqrt(3) * (q + r / 2f), -CellSize * 1.5f * r);

        /// <summary>The ring: a circle, six spokes from it inwards, ticks every 5° (longer every 15°).</summary>
        void DrawRing(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var radius = Outer * scale;
            p.strokeColor = new Color(0.79f, 0.8f, 0.78f, 0.28f);
            p.lineWidth = 1.2f;
            p.BeginPath();
            p.Arc(middle, radius, 0, 360);
            p.Stroke();
            for (var a = 0; a < 360; a += 5)
            {
                var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                var inner = a % 60 == 0 ? radius * 0.92f : a % 15 == 0 ? radius - 9 : radius - 4;
                p.BeginPath();
                p.MoveTo(middle + dir * inner);
                p.LineTo(middle + dir * radius);
                p.Stroke();
            }
        }

        void Show(HexCell cell)
        {
            if (cell.State == "locked") return;
            sound?.Invoke("cellHover");
            Select(cell.Cell.Id);
            Shown?.Invoke(cell.Cell.Id);
        }

        /// <summary>The cell the card shows (the core until another is looked at). Nothing on the cell itself says so: in 0.10.1 a hover or a click changes no cell (Ana, 28/10: «se queda naranja así seleccionado»).</summary>
        public void Select(string id)
        {
            foreach (var pair in cells)
            {
                var on = pair.Key == id;
                if (pair.Value.Selected == on) continue;
                pair.Value.Selected = on;
                pair.Value.MarkDirtyRepaint();
            }
        }

        void StartHold(HexCell cell, int pointerId)
        {
            if (!cell.Buyable) return;
            held = cell;
            heldSince = Time.unscaledTime;
            sound?.Invoke("cellHold");
        }

        /// <summary>
        /// For the lab's captures (BASTION_TABLERO_RATON): the pointer resting on cell `id`, as a hover (the wave and its
        /// card), and with `hold` over 0 that share of its hold run, standing still.
        /// </summary>
        public void LabPoint(string id, float hold)
        {
            if (!cells.TryGetValue(id, out var cell)) return;
            pointer = cell.Centre;
            Show(cell);
            if (hold <= 0) return;
            cell.Hold = hold;
            cell.MarkDirtyRepaint();
        }

        /// <summary>
        /// For the lab's strips (BASTION_TABLERO_RATON=cruce): the pointer crossing the board from `from` to `to` (board
        /// units from its middle, y down) in `seconds` of the game's time, then off it.
        /// </summary>
        public void LabSweep(Vector2 from, Vector2 to, float seconds)
        {
            sweep = (from, to, Time.time, seconds);
        }

        (Vector2 from, Vector2 to, float start, float seconds)? sweep;

        /// <summary>Esc while a cell is held (hexBoard.ts key: cancelHold): the purchase let go, not the board; whether one was held.</summary>
        public bool CancelHold()
        {
            if (held == null) return false;
            StopHold();
            return true;
        }

        void StopHold()
        {
            if (held == null) return;
            sound?.Invoke("cellHold.stop");
            held.Hold = 0;
            held.MarkDirtyRepaint();
            held = null;
        }

        /// <summary>The arrows: to the neighbour closest to that way; Intro or the space held, as a press.</summary>
        void Key(HexCell cell, KeyDownEvent e)
        {
            keyboard = true;
            Vector2 way;
            switch (e.keyCode)
            {
                case KeyCode.LeftArrow: way = Vector2.left; break;
                case KeyCode.RightArrow: way = Vector2.right; break;
                case KeyCode.UpArrow: way = Vector2.down; break;
                case KeyCode.DownArrow: way = Vector2.up; break;
                case KeyCode.Return:
                case KeyCode.Space:
                    if (held == null) StartHold(cell, -1);
                    e.StopPropagation();
                    return;
                default: return;
            }
            HexCell best = null;
            var bestDot = 0.3f;
            foreach (var id in Board.BoardNeighbors(cell.Cell.Id))
            {
                var n = cells[id];
                if (n.State == "locked") continue;
                var d = Vector2.Dot((n.Centre - cell.Centre).normalized, way);
                if (d > bestDot)
                {
                    bestDot = d;
                    best = n;
                }
            }
            best?.Focus();
            e.StopPropagation();
        }

        /// <summary>Each frame: the wave under the pointer, and the cell held.</summary>
        void Tick()
        {
            // The wave (hexBoard.ts tick): each cell eases to its height under the pointer, by the square of how near it
            // is; quick to rise, slow to settle once the pointer has left the board. Its shadow stays on the board.
            if (sweep is var (sweepFrom, sweepTo, start, seconds))
            {
                var t = (Time.time - start) / seconds;
                pointer = t <= 1 ? middle + Vector2.Lerp(sweepFrom, sweepTo, Mathf.Max(0, t)) * scale : (Vector2?)null;
                if (t > 1) sweep = null;
            }
            var reach = CellSize * Reach * scale;
            var now = Time.unscaledTime;
            foreach (var c in cells.Values)
            {
                var dist = pointer.HasValue ? Vector2.Distance(pointer.Value, c.Centre) : float.MaxValue;
                var to = dist < reach ? Lift * Mathf.Pow(1 - dist / reach, 2) : 0;
                var k = to > c.Lift ? 0.35f : pointer.HasValue ? 0.2f : 0.06f;
                var next = c.Lift + (to - c.Lift) * k;
                var celebrating = now - c.BoughtAt < PulseSeconds;
                if (Mathf.Abs(next - c.Lift) > 0.001f)
                {
                    c.Lift = Mathf.Abs(next) < 0.01f && to == 0 ? 0 : next;
                    c.style.translate = new Translate(0, -c.Lift * scale);
                    c.MarkDirtyRepaint();
                }
                if (celebrating || c.Celebrating)
                {
                    c.Celebrating = celebrating;
                    // The bounce (cubic-bezier(0.3, 1.6, 0.5, 1) to 1.14 at 40 %): up and back, a little past.
                    var t = Mathf.Clamp01((now - c.BoughtAt) / BounceSeconds);
                    c.style.scale = new Scale(Vector3.one * (celebrating ? 1 + 0.14f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.75f)) : 1));
                    c.MarkDirtyRepaint();
                }
            }
            // The keys' white ring on the focused cell, never the pointer's.
            var ring = keyboard ? focusController?.focusedElement as HexCell : null;
            if (ring != ringed)
            {
                if (ringed != null)
                {
                    ringed.Ring = false;
                    ringed.MarkDirtyRepaint();
                }
                ringed = ring;
                if (ring != null)
                {
                    ring.Ring = true;
                    ring.MarkDirtyRepaint();
                }
            }
            if (held == null) return;
            held.Hold = Mathf.Clamp01((Time.unscaledTime - heldSince) / HoldSeconds);
            held.MarkDirtyRepaint();
            if (held.Hold < 1) return;
            var id = held.Cell.Id;
            // Bought: the hold's sound rings out.
            sound?.Invoke("cellHold.done");
            StopHold();
            sound?.Invoke("cellBuy");
            Bought?.Invoke(id);
        }

        /// <summary>One hexagon: its face in the line's colour (or dull, or dark), its edge, its icon, the hold's ring.</summary>
        sealed class HexCell : VisualElement
        {
            public readonly BoardCell Cell;
            public readonly VisualElement Icon;
            public string State = "locked";
            public bool Buyable;
            public Vector2 Centre;
            /// <summary>Risen in the wave (board units).</summary>
            public float Lift;
            public float Hold;
            public bool Selected;
            /// <summary>The keys' focus on it (the white ring).</summary>
            public bool Ring;
            /// <summary>When it was bought here (the bounce and the pulse), and whether they still run.</summary>
            public float BoughtAt = -10;
            public bool Celebrating;

            public HexCell(BoardCell cell)
            {
                Cell = cell;
                AddToClassList("hex-cell");
                style.position = Position.Absolute;
                Icon = new VisualElement { pickingMode = PickingMode.Ignore };
                Icon.AddToClassList("hex-cell__icon");
                Add(Icon);
                generateVisualContent += Draw;
            }

            Color Line => Cell.Id == Board.Core ? new Color32(0xf2, 0xc2, 0x30, 0xff) : SkillHex.Colors[Mathf.Clamp(Cell.Sector, 0, SkillHex.Colors.Length - 1)];

            /// <summary>Hit inside the hexagon, not its box.</summary>
            public override bool ContainsPoint(Vector2 local)
            {
                var r = contentRect;
                var d = (local - r.center) / (r.height / 2);
                return Mathf.Abs(d.x) <= 0.866f && Mathf.Abs(d.x) * 0.577f + Mathf.Abs(d.y) <= 1f;
            }

            void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                var r = contentRect;
                var c = r.center;
                var radius = r.height / 2;
                // A board unit in pixels (the hexagon's radius is the size less half the gap).
                var unit = radius / (CellSize - Gap / 2);
                var line = Line;
                var isCore = Cell.Id == Board.Core;
                // hexBoard.css: the face and the edge mixed from the sector's colour (color-mix in srgb), by state; the core
                // steel in the accent.
                Color face, edge;
                if (isCore)
                {
                    face = new Color32(0x2a, 0x2f, 0x31, 0xff);
                    edge = new Color32(0xf2, 0xc2, 0x30, 0xff);
                }
                else if (State == "unlocked")
                {
                    face = line;
                    edge = Color.Lerp(Color.white, line, 0.62f);
                }
                else if (State == "open")
                {
                    face = Color.Lerp(new Color32(0x1d, 0x21, 0x23, 0xff), line, 0.10f);
                    edge = Color.Lerp(new Color32(0x2b, 0x30, 0x33, 0xff), line, 0.34f);
                }
                else
                {
                    face = Color.Lerp(new Color32(0x12, 0x15, 0x17, 0xff), line, 0.04f);
                    edge = Color.Lerp(new Color32(0x1e, 0x22, 0x25, 0xff), line, 0.12f);
                }
                // Its shadow, 3 below and left on the board as the cell rises (darker raised); the core's in a brass glow.
                Hex(p, c + new Vector2(0, (3 + Lift) * unit), radius);
                p.fillColor = new Color(0, 0, 0, Lift > 0.5f ? 0.72f : 0.55f);
                p.Fill();
                if (isCore)
                {
                    p.strokeColor = new Color(0.95f, 0.76f, 0.19f, 0.45f);
                    p.lineWidth = 7 * unit;
                    p.Stroke();
                }
                Hex(p, c, radius);
                p.fillColor = face;
                p.Fill();
                // The keys' focus: a thin white ring (a hover or a click changes nothing).
                p.strokeColor = Ring ? new Color32(0xf4, 0xf5, 0xf1, 0xff) : edge;
                p.lineWidth = (Ring ? 2.6f : isCore ? 3f : 1.6f) * unit;
                p.Stroke();
                if (Hold > 0)
                {
                    // The hold: a light running round the outline from its top in a second, in the line's glow.
                    Outline(p, c, radius, Hold);
                    p.strokeColor = new Color(line.r, line.g, line.b, 0.45f);
                    p.lineWidth = 7 * unit;
                    p.lineCap = LineCap.Round;
                    p.Stroke();
                    Outline(p, c, radius, Hold);
                    p.strokeColor = Color.white;
                    p.lineWidth = 3.2f * unit;
                    p.Stroke();
                }
                var since = Time.unscaledTime - BoughtAt;
                if (since >= 0 && since < PulseSeconds)
                {
                    // Bought: a pulse running out from the outline (to ×1.45, fading).
                    var t = since / PulseSeconds;
                    var ease = 1 - (1 - t) * (1 - t);
                    Hex(p, c, radius * (1 + 0.45f * ease));
                    p.strokeColor = new Color(Mathf.Lerp(1, line.r, 0.7f), Mathf.Lerp(1, line.g, 0.7f), Mathf.Lerp(1, line.b, 0.7f), 0.95f * (1 - ease));
                    p.lineWidth = 2.5f * unit;
                    p.Stroke();
                }
            }

            /// <summary>A pointy hexagon's path round `c`, from its top.</summary>
            static void Hex(Painter2D p, Vector2 c, float radius)
            {
                p.BeginPath();
                for (var i = 0; i < 6; i++)
                {
                    var at = c + Corner(i) * radius;
                    if (i == 0) p.MoveTo(at);
                    else p.LineTo(at);
                }
                p.ClosePath();
            }

            /// <summary>The first `share` of the outline, clockwise from the top corner.</summary>
            static void Outline(Painter2D p, Vector2 c, float radius, float share)
            {
                p.BeginPath();
                p.MoveTo(c + Corner(0) * radius);
                var sides = share * 6;
                for (var i = 0; i < 6 && sides > 0; i++)
                {
                    var a = c + Corner(i) * radius;
                    var b = c + Corner(i + 1) * radius;
                    p.LineTo(Vector2.Lerp(a, b, Mathf.Min(1, sides)));
                    sides -= 1;
                }
            }

            static Vector2 Corner(int i)
            {
                var a = (60 * i - 90) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }
    }
}
