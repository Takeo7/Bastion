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
            foreach (var c in Board.Cells)
            {
                var cell = new HexCell(c);
                cells[c.Id] = cell;
                Add(cell);
                cell.RegisterCallback<PointerEnterEvent>(_ => Show(cell));
                cell.RegisterCallback<FocusInEvent>(_ => Show(cell));
                cell.RegisterCallback<PointerDownEvent>(e => StartHold(cell, e.pointerId));
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
            RegisterCallback<PointerLeaveEvent>(_ => pointer = null);
            schedule.Execute(Tick).Every(16);
        }

        /// <summary>The soldier's cells and points; editable, theirs to spend.</summary>
        public void Set(IReadOnlyList<string> unlocked, int pointsLeft, bool canEdit)
        {
            board = unlocked ?? new List<string>();
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

        /// <summary>The cell the card shows, rimmed in the accent (the core until another is looked at).</summary>
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

        void StopHold()
        {
            if (held == null) return;
            held.Hold = 0;
            held.MarkDirtyRepaint();
            held = null;
        }

        /// <summary>The arrows: to the neighbour closest to that way; Intro or the space held, as a press.</summary>
        void Key(HexCell cell, KeyDownEvent e)
        {
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
            var reach = CellSize * 3.2f * scale;
            foreach (var c in cells.Values)
            {
                var target = pointer.HasValue ? Mathf.Clamp01(1 - Vector2.Distance(pointer.Value, c.Centre) / reach) : 0;
                if (Mathf.Abs(target - c.Lift) < 0.002f) continue;
                c.Lift += (target - c.Lift) * 0.25f;
                c.style.translate = new Translate(0, -c.Lift * 5);
                c.style.scale = new Scale(Vector3.one * (1 + c.Lift * 0.06f));
            }
            if (held == null) return;
            held.Hold = Mathf.Clamp01((Time.unscaledTime - heldSince) / HoldSeconds);
            held.MarkDirtyRepaint();
            if (held.Hold < 1) return;
            var id = held.Cell.Id;
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
            public float Lift;
            public float Hold;
            public bool Selected;

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
                var line = Line;
                // The core shows its picture in the accent on a dark face (hexBoard.css .hx.core); the others bought, in their colour.
                var isCore = Cell.Id == Board.Core;
                // Out of reach: nearly gone, as hexBoard.css .hx.locked mixes it: 4 % of its sector's colour in the face and
                // 12 % in an edge hardly lighter than it, both opaque.
                var face = isCore ? new Color(0.23f, 0.19f, 0.1f) : State == "unlocked" ? line : State == "open" ? Color.Lerp(new Color(0.11f, 0.12f, 0.13f), line, 0.16f) : Color.Lerp(new Color32(0x12, 0x15, 0x17, 0xff), line, 0.04f);
                var edge = State == "unlocked" ? Color.Lerp(line, Color.white, 0.3f) : State == "open" ? new Color(line.r, line.g, line.b, 0.55f) : Color.Lerp(new Color32(0x1e, 0x22, 0x25, 0xff), line, 0.12f);
                var rim = Selected || focusController?.focusedElement == this;
                if (rim) edge = new Color32(0xf2, 0xc2, 0x30, 0xff);
                p.fillColor = face;
                p.strokeColor = edge;
                p.lineWidth = rim ? 3f : State == "unlocked" ? 1.6f : 1f;
                p.BeginPath();
                for (var i = 0; i < 6; i++)
                {
                    var a = (60 * i - 90) * Mathf.Deg2Rad;
                    var at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    if (i == 0) p.MoveTo(at);
                    else p.LineTo(at);
                }
                p.ClosePath();
                p.Fill();
                p.Stroke();
                if (Hold <= 0) return;
                // The hold's ring, filling clockwise from the top.
                p.strokeColor = new Color32(0xf2, 0xc2, 0x30, 0xff);
                p.lineWidth = 3;
                p.BeginPath();
                p.Arc(c, radius * 0.78f, -90, -90 + 360 * Hold);
                p.Stroke();
            }
        }
    }
}
