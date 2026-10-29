using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// A soldier's line by the name (skillSheet.ts skillHex, Ana 23/10 #30): a hexagon of the colour of the board's line
    /// it has bought most cells of (Arturo, 29/10: Ana's all blue on the board had it green, by the experience), cut like
    /// a pie between the lines tied at the top (from the top, clockwise, as the CSS conic-gradient was); grey before it
    /// has any. USS has no clip-path nor conic-gradient: Painter2D draws it.
    /// </summary>
    public sealed class SkillHex : VisualElement
    {
        /// <summary>The lines in SkillIds.All order and their colours (hexBoard.css --sk-*).</summary>
        public static readonly Color[] Colors =
        {
            new Color32(0x5a, 0xa9, 0xec, 0xff), // shooting
            new Color32(0xec, 0x64, 0x4e, 0xff), // melee
            new Color32(0xa5, 0x85, 0xea, 0xff), // throwing
            new Color32(0x4f, 0xca, 0xa3, 0xff), // medicine
            new Color32(0xb7, 0xc6, 0x4a, 0xff), // searching
            new Color32(0xe5, 0x7f, 0xae, 0xff), // fitness
        };

        /// <summary>Their names, in the same order (Lore's, in the engine's Bars).</summary>
        public static readonly string[] Names = SkillIds.All.Select(id => Bars.SkillDefs[id].Name).ToArray();

        static readonly Color None = new Color(201 / 255f, 205 / 255f, 200 / 255f, 0.28f);

        /// <summary>The hexagon's corners in its box, pointy at top and bottom (the CSS polygon: 50% 0, 100% 25%, …).</summary>
        static readonly Vector2[] Corners =
        {
            new Vector2(0.5f, 0f), new Vector2(1f, 0.25f), new Vector2(1f, 0.75f),
            new Vector2(0.5f, 1f), new Vector2(0f, 0.75f), new Vector2(0f, 0.25f),
        };

        readonly List<int> top = new List<int>();

        public SkillHex()
        {
            AddToClassList("skill-hex");
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
        }

        /// <summary>The cells of each line (SkillIds.All order) among `cells`, a board's cell ids; the core counts for none.</summary>
        public static float[] Branches(IEnumerable<string> cells)
        {
            var counts = new float[SkillIds.All.Count];
            if (cells == null) return counts;
            foreach (var id in cells)
            {
                var line = id != null && Board.BoardCells.TryGetValue(id, out var cell) ? cell.Line : null;
                var i = line != null ? IndexOf(line) : -1;
                if (i >= 0) counts[i]++;
            }
            return counts;
        }

        /// <summary>The same from a unit's perks, for one with no board (a skirmish's veterans): the cells that give them.</summary>
        public static float[] BranchesOfPerks(IEnumerable<string> perks) =>
            Branches(perks?.Select(p => Board.Cells.FirstOrDefault(c => c.Perk == p)?.Id));

        static int IndexOf(string line)
        {
            for (var i = 0; i < SkillIds.All.Count; i++)
                if (SkillIds.All[i] == line) return i;
            return -1;
        }

        /// <summary>The cells bought of each line (Branches); the tooltip says which lines lead.</summary>
        public void Set(float[] skills, TooltipLayer tips)
        {
            top.Clear();
            var best = 0f;
            if (skills != null)
                foreach (var v in skills) best = Mathf.Max(best, v);
            if (best > 0)
                for (var i = 0; i < skills.Length && i < Colors.Length; i++)
                    if (skills[i] == best) top.Add(i);
            var names = new List<string>();
            foreach (var i in top) names.Add(Names[i]);
            var tip = top.Count == 0 ? HudText.Skills.None : top.Count == 1 ? HudText.Skills.Best(names[0]) : HudText.Skills.Tied(Ui.List(names));
            tips?.Attach(this, tip);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = ctx.painter2D;
            if (top.Count <= 1)
            {
                p.fillColor = top.Count == 0 ? None : Colors[top[0]];
                p.BeginPath();
                for (var i = 0; i < Corners.Length; i++)
                {
                    var c = At(r, Corners[i]);
                    if (i == 0) p.MoveTo(c);
                    else p.LineTo(c);
                }
                p.ClosePath();
                p.Fill();
                return;
            }
            // A slice per tied line: the middle, the edge where it starts, the corners it passes, the edge where it ends.
            var slice = 360f / top.Count;
            for (var s = 0; s < top.Count; s++)
            {
                var from = s * slice;
                var to = (s + 1) * slice;
                p.fillColor = Colors[top[s]];
                p.BeginPath();
                p.MoveTo(At(r, new Vector2(0.5f, 0.5f)));
                p.LineTo(At(r, Edge(from)));
                foreach (var c in Corners)
                {
                    var a = AngleOf(c);
                    if (a > from && a < to) p.LineTo(At(r, c));
                }
                p.LineTo(At(r, Edge(to)));
                p.ClosePath();
                p.Fill();
            }
        }

        static Vector2 At(Rect r, Vector2 unit) => new Vector2(r.x + unit.x * r.width, r.y + unit.y * r.height);

        /// <summary>A point's angle from the middle, from the top, clockwise, in degrees (0–360), as conic-gradient counts.</summary>
        static float AngleOf(Vector2 unit)
        {
            var a = Mathf.Atan2(unit.x - 0.5f, -(unit.y - 0.5f)) * Mathf.Rad2Deg;
            return a < 0 ? a + 360 : a;
        }

        /// <summary>Where the ray from the middle at `degrees` (from the top, clockwise) meets the hexagon's edge.</summary>
        static Vector2 Edge(float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
            var o = new Vector2(0.5f, 0.5f);
            var best = float.MaxValue;
            for (var i = 0; i < Corners.Length; i++)
            {
                var a = Corners[i];
                var b = Corners[(i + 1) % Corners.Length];
                var e = b - a;
                var den = dir.x * e.y - dir.y * e.x;
                if (Mathf.Abs(den) < 1e-6f) continue;
                var d = a - o;
                var t = (d.x * e.y - d.y * e.x) / den;
                var u = (d.x * dir.y - d.y * dir.x) / den;
                if (t >= 0 && u >= -1e-4f && u <= 1 + 1e-4f && t < best) best = t;
            }
            return best == float.MaxValue ? o : o + dir * best;
        }
    }
}
