using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The log under the objective (hud.ts log/renderLog; docs/UNITY-UI.md §2.8): what happened, a line each, the newest
    /// on top, a stroke on its left in the colour of its tone (one's own hurt, the enemy's, enemies among themselves, good
    /// news), the words in the log's own (styles.css .log-line). Folded it
    /// shows the last two and «▾ 3 más»; open, the last thirty and «▴ Plegar». Lines come from the events, in Lore's words.
    /// </summary>
    public sealed class Log
    {
        const int Keep = 30;
        const int Folded = 2;

        readonly VisualElement host;
        readonly ScrollView lines;
        readonly Button toggle;
        readonly Triangle toggleArrow;
        readonly Label toggleText;
        /// <summary>Newest first.</summary>
        readonly List<Label> kept = new List<Label>();
        bool open;

        public Log(VisualElement host, System.Func<string, Sprite> icon = null)
        {
            this.host = host;
            lines = new ScrollView(ScrollViewMode.Vertical);
            lines.AddToClassList("log__lines");
            lines.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            host.Add(lines);
            toggle = new Button(() =>
            {
                open = !open;
                Draw();
            });
            toggle.AddToClassList("log__toggle");
            toggleArrow = new Triangle(false);
            toggleArrow.AddToClassList("log__arrow");
            toggle.Add(toggleArrow);
            toggleText = Ui.Text("", "log__toggle-text", toggle);
            toggleText.pickingMode = PickingMode.Ignore;
            host.Add(toggle);
            Draw();
        }

        public void Add(string text, string color = null)
        {
            var line = Ui.Text(text, "log__line");
            if (!string.IsNullOrEmpty(color)) line.style.borderLeftColor = Ui.Hex(color, Color.white);
            kept.Insert(0, line);
            lines.Insert(0, line);
            while (kept.Count > Keep)
            {
                kept[kept.Count - 1].RemoveFromHierarchy();
                kept.RemoveAt(kept.Count - 1);
            }
            Draw();
        }

        public void Clear()
        {
            kept.Clear();
            lines.Clear();
            open = false;
            Draw();
        }

        void Draw()
        {
            host.EnableInClassList("log--open", open);
            // Its scrollbar only open, when thirty lines may not fit.
            lines.verticalScrollerVisibility = open ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            Ui.Show(host, kept.Count > 0);
            for (var i = 0; i < kept.Count; i++) Ui.Show(kept[i], open || i < Folded);
            var more = kept.Count - Folded;
            Ui.Show(toggle, more > 0 || open);
            toggleText.text = open ? HudText.Log.Fold : HudText.Log.More(more);
            toggleArrow.Up = open;
            if (open) lines.scrollOffset = Vector2.zero;
        }
    }
}
