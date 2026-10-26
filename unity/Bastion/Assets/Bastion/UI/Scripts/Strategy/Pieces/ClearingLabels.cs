using System.Collections.Generic;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>A name floating over the clearing: the partner's over their piece in hand, a defender's over their post.</summary>
    public struct ClearingTag
    {
        public string Key;
        public string Text;
        /// <summary>The commander's colour (CSS hex), the strip on its left.</summary>
        public string Colour;
        /// <summary>Where it goes (world, metres): Arte's Settlement.PartnerTagWorld or PostWorld.</summary>
        public Vector3 World;
        /// <summary>A post of this player's (brighter), the one in hand (lit); the partner's piece in hand.</summary>
        public bool Mine, Held, Partner;
    }

    /// <summary>
    /// The words over the clearing, under the panels (clearing.ts .clearing-hint, .clearing-partner, .clearing-post): what
    /// the pointer would do there (its cost, or why not, in red), by the pointer; and the names that float over the 3D,
    /// placed every frame through the clearing's camera as the combat's marks are.
    /// </summary>
    public sealed class ClearingLabels
    {
        readonly VisualElement layer;
        readonly Label hint;
        readonly Dictionary<string, Label> tags = new Dictionary<string, Label>();
        readonly HashSet<string> seen = new HashSet<string>();

        public ClearingLabels(VisualElement parent)
        {
            layer = Ui.El("clearing-labels");
            layer.pickingMode = PickingMode.Ignore;
            parent.Insert(0, layer);
            hint = Ui.Text("", "clearing-hint", layer);
            hint.pickingMode = PickingMode.Ignore;
            Ui.Show(hint, false);
        }

        /// <summary>The words by the pointer at a point of `camera`'s picture (pixels, y up: the mouse's); null, none.</summary>
        public void Hint(string text, bool bad, Vector2 screen, Camera camera)
        {
            Ui.Show(hint, !string.IsNullOrEmpty(text));
            if (string.IsNullOrEmpty(text)) return;
            hint.text = text;
            hint.EnableInClassList("clearing-hint--bad", bad);
            HintAt(screen, camera);
        }

        /// <summary>The words follow the pointer: 16 px right of it and 18 below, as the 0.10.1's.</summary>
        public void HintAt(Vector2 screen, Camera camera)
        {
            // (What was set, not what was laid out: just shown, it is laid out only at the next frame.)
            if (hint.style.display.value == DisplayStyle.None || layer.panel == null) return;
            var p = ToPanel(screen, camera);
            hint.style.left = p.x + 16;
            hint.style.top = p.y + 18;
        }

        /// <summary>
        /// A point of the camera's picture (pixels, y up) on the panel: by the picture's size, which is the screen's in the
        /// game and the shot's texture in the captures (Shots draws the cameras into one of its own).
        /// </summary>
        Vector2 ToPanel(Vector2 at, Camera camera)
        {
            var size = layer.panel.visualTree.layout.size;
            var w = camera != null ? camera.pixelWidth : Screen.width;
            var h = camera != null ? camera.pixelHeight : Screen.height;
            return new Vector2(at.x / Mathf.Max(1, w) * size.x, (1 - at.y / Mathf.Max(1, h)) * size.y);
        }

        /// <summary>The names over the clearing now, through `camera`; those gone are taken away.</summary>
        public void Tags(IReadOnlyList<ClearingTag> list, Camera camera)
        {
            seen.Clear();
            if (list != null && camera != null && layer.panel != null)
                foreach (var t in list)
                {
                    seen.Add(t.Key);
                    if (!tags.TryGetValue(t.Key, out var label))
                    {
                        label = Ui.Text("", "clearing-tag", layer);
                        label.pickingMode = PickingMode.Ignore;
                        tags[t.Key] = label;
                    }
                    label.text = t.Text;
                    label.EnableInClassList("clearing-tag--mine", t.Mine);
                    label.EnableInClassList("clearing-tag--held", t.Held);
                    label.EnableInClassList("clearing-tag--partner", t.Partner);
                    if (ColorUtility.TryParseHtmlString(t.Colour, out var colour)) label.style.borderLeftColor = colour;
                    var s = camera.WorldToScreenPoint(t.World);
                    Ui.Show(label, s.z > 0);
                    if (s.z <= 0) continue;
                    var p = ToPanel(s, camera);
                    label.style.left = p.x;
                    label.style.top = p.y;
                }
            var gone = new List<string>();
            foreach (var key in tags.Keys) if (!seen.Contains(key)) gone.Add(key);
            foreach (var key in gone)
            {
                tags[key].RemoveFromHierarchy();
                tags.Remove(key);
            }
        }
    }
}
