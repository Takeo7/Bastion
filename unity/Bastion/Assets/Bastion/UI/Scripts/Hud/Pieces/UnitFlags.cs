using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The layer over the units (docs/UNITY-UI.md §2.9): a flag over each unit in sight (unitViews.ts renderFlag:
    /// name, health, armour, cover, states), the hover line over an enemy (setTags), and the floating words (fx.ts
    /// floatText, UI's now). Every frame each is put over its point in the world with
    /// RuntimePanelUtils.CameraTransformWorldToPanel, only its translate changing (no layout is rebuilt); one behind the
    /// camera is hidden. Over the shoulder only the target's flag stays.
    /// </summary>
    public sealed class UnitFlags
    {
        const float FloatRise = 40f;
        const float FloatSeconds = 1.4f;

        readonly VisualElement layer;
        readonly Dictionary<string, VisualElement> flags = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, string> keys = new Dictionary<string, string>();
        readonly List<Floating> floats = new List<Floating>();
        readonly List<string> gone = new List<string>();

        sealed class Floating
        {
            public VisualElement Label;
            public Vector3 World;
            public float Born;
            /// <summary>A pin's: how long it stays.</summary>
            public float Seconds;
        }

        readonly Func<string, Sprite> icon;

        public UnitFlags(VisualElement layer, Func<string, Sprite> icon = null)
        {
            this.layer = layer;
            this.icon = icon;
        }

        /// <summary>The flags of the units in sight; those left out go.</summary>
        public void Set(FlagsView view)
        {
            gone.Clear();
            gone.AddRange(flags.Keys);
            if (view != null)
                foreach (var f in view.Flags)
                {
                    gone.Remove(f.Id);
                    var k = Ui.Key(f);
                    if (keys.TryGetValue(f.Id, out var was) && was == k) continue;
                    keys[f.Id] = k;
                    if (!flags.TryGetValue(f.Id, out var el))
                    {
                        el = Ui.El("uflag");
                        el.pickingMode = PickingMode.Ignore;
                        layer.Add(el);
                        flags[f.Id] = el;
                    }
                    Fill(el, f);
                }
            foreach (var id in gone)
            {
                flags[id].RemoveFromHierarchy();
                flags.Remove(id);
                keys.Remove(id);
            }
        }

        /// <summary>The shot over the shoulder: only the target's flag (and the floats) stay.</summary>
        public void SetCloseShot(bool on)
        {
            layer.EnableInClassList("flags-layer--close", on);
        }

        /// <summary>
        /// A word or number rising and fading over `world` (damage, «CRÍTICO −5», «ATURDIDO»…); kind: damage, crit, info, warn,
        /// callout; `iconId`, a HUD icon before it (a weapon found: Ana's notes, Arturo 29/10).
        /// </summary>
        public void Float(Vector3 world, string text, string kind, string iconId = null)
        {
            VisualElement label;
            // With a picture, a row of it and the word (a Label with children no longer measures its text).
            if (iconId != null && icon?.Invoke(iconId) is Sprite picture)
            {
                label = Ui.El("float float--" + kind + " float--icon", layer);
                Ui.El("float__icon", label).style.backgroundImage = new StyleBackground(picture);
                Ui.Text(text, "float__text", label);
                label.Query<VisualElement>().ForEach(x => x.pickingMode = PickingMode.Ignore);
            }
            else label = Ui.Text(text, "float float--" + kind, layer);
            label.pickingMode = PickingMode.Ignore;
            floats.Add(new Floating { Label = label, World = world, Born = Time.time });
        }

        /// <summary>
        /// A tag that stays over a point a while and fades at the end (overlays.ts pingTag: whose ping it is, 4 s), on a
        /// plate of `color`.
        /// </summary>
        public void Pin(Vector3 world, string text, string color, float seconds)
        {
            var label = Ui.Text(text, "pin", layer);
            label.pickingMode = PickingMode.Ignore;
            if (ColorUtility.TryParseHtmlString(color, out var c))
            {
                label.style.backgroundColor = new Color(c.r * 0.78f, c.g * 0.78f, c.b * 0.78f, 1);
                label.style.borderTopColor = label.style.borderBottomColor = label.style.borderLeftColor = label.style.borderRightColor = new Color(c.r, c.g, c.b, 0.45f);
            }
            pins.Add(new Floating { Label = label, World = world, Born = Time.time, Seconds = seconds });
        }

        readonly List<Floating> pins = new List<Floating>();

        /// <summary>The tags that stay over the map (a siege's bars, the fallen's rucksacks, the loot boxes' marks), where, and how wide in the world (0: as laid out).</summary>
        readonly List<(VisualElement el, Vector3 world, float size)> tags = new List<(VisualElement, Vector3, float)>();

        /// <summary>A loot box's mark as laid out (Flags.uss .wtag--loot), scaled to its width in the world.</summary>
        const float MarkPixels = 64;
        string tagsKey;

        /// <summary>The tags over the map (WorldTagView), made again only when they change.</summary>
        public void SetTags(List<WorldTagView> list)
        {
            var key = list == null ? "" : string.Join("|", list.ConvertAll(Ui.Key));
            if (key == tagsKey) return;
            tagsKey = key;
            foreach (var t in tags) t.el.RemoveFromHierarchy();
            tags.Clear();
            if (list == null) return;
            foreach (var v in list)
            {
                var el = Ui.El("wtag wtag--" + v.Kind);
                el.pickingMode = PickingMode.Ignore;
                if (v.Kind == "loot")
                {
                    // The icon of what is in it on a plate of dark steel, ringed in its colour (lootMarks.ts material).
                    el.style.borderTopColor = el.style.borderRightColor = el.style.borderBottomColor = el.style.borderLeftColor = Ui.Hex(v.Ring, Color.white);
                    var mark = Ui.El("wtag__icon", el);
                    mark.pickingMode = PickingMode.Ignore;
                    if (icon?.Invoke(v.Icon) is Sprite sprite) mark.style.backgroundImage = new StyleBackground(sprite);
                    layer.Insert(0, el);
                    tags.Add((el, v.World, v.Size));
                    continue;
                }
                if (v.Kind == "bar")
                {
                    el.EnableInClassList("wtag--low", v.Low);
                    var track = Ui.El("wtag__track", el);
                    Ui.El("wtag__fill", track).style.width = Length.Percent(Mathf.Clamp01(v.Fill) * 100);
                }
                Ui.Text(v.Text, "wtag__text", el).pickingMode = PickingMode.Ignore;
                layer.Insert(0, el);
                tags.Add((el, v.World, 0));
            }
        }

        /// <summary>The line on the tile under the pointer (overlays.ts setNote), and where it stands.</summary>
        Label note;
        Vector3 noteAt;

        /// <summary>One short line on a tile («Ruido 3: corre», «La caja de armas · clic: abrir»), or none (null).</summary>
        public void SetNote(Vector3? world, string text)
        {
            if (world == null || string.IsNullOrEmpty(text))
            {
                note?.RemoveFromHierarchy();
                note = null;
                return;
            }
            if (note == null)
            {
                note = Ui.Text(text, "tile-note", layer);
                note.pickingMode = PickingMode.Ignore;
            }
            else note.text = text;
            noteAt = world.Value;
        }

        /// <summary>
        /// Every frame, after the camera moved: each flag over its unit's head (`headOf`: its point in the world, null
        /// when it has no body to follow), each float over its point, rising.
        /// </summary>
        public void Follow(Camera camera, Func<string, Vector3?> headOf)
        {
            var panel = layer.panel;
            if (panel == null || camera == null) return;
            var wide = panel.visualTree.layout.width;
            foreach (var pair in flags)
            {
                var head = headOf(pair.Key);
                if (Place(pair.Value, panel, camera, head, 0, 1f) is float left) KeepLinesIn(pair.Value, left, wide);
            }
            var now = Time.time;
            for (var i = floats.Count - 1; i >= 0; i--)
            {
                var f = floats[i];
                var t = (now - f.Born) / FloatSeconds;
                if (t >= 1)
                {
                    f.Label.RemoveFromHierarchy();
                    floats.RemoveAt(i);
                    continue;
                }
                Place(f.Label, panel, camera, f.World, -FloatRise * t, 0.5f);
                f.Label.style.opacity = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            }
            foreach (var t in tags)
            {
                if (t.size <= 0)
                {
                    Place(t.el, panel, camera, t.world, 0, 1f);
                    continue;
                }
                // At the map's scale (lootMarks.ts SIZE): its width in the world across the screen, wherever the camera is.
                Place(t.el, panel, camera, t.world, 0, 0.5f);
                var a = camera.WorldToViewportPoint(t.world);
                var b = camera.WorldToViewportPoint(t.world + camera.transform.right * t.size);
                var k = Mathf.Abs(b.x - a.x) * panel.visualTree.layout.width / MarkPixels;
                t.el.style.scale = new Scale(new Vector3(k, k, 1));
            }
            // Under the tile, a little below its middle (the web's margin-top: 30px); near the edge, slid in to be read whole.
            if (note != null) Place(note, panel, camera, noteAt, 30, 0, true);
            for (var i = pins.Count - 1; i >= 0; i--)
            {
                var f = pins[i];
                var t = (now - f.Born) / f.Seconds;
                if (t >= 1)
                {
                    f.Label.RemoveFromHierarchy();
                    pins.RemoveAt(i);
                    continue;
                }
                Place(f.Label, panel, camera, f.World, 0, 1f);
                f.Label.style.opacity = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
            }
        }

        /// <summary>How far from the screen's edge a line slid in keeps.</summary>
        const float Edge = 6;

        /// <summary>
        /// A flag near the screen's edge: its lines (what the pointer says of that enemy) slide in to be read whole, the
        /// flag itself staying over its head. `left`: where the flag begins across.
        /// </summary>
        static void KeepLinesIn(VisualElement flag, float left, float wide)
        {
            foreach (var line in flag.Children())
            {
                if (!line.ClassListContains("uflag__hover")) continue;
                var from = left + line.layout.x;
                var to = from + line.layout.width;
                if (float.IsNaN(from) || float.IsNaN(to) || float.IsNaN(wide)) continue;
                var shift = from < Edge ? Edge - from : to > wide - Edge ? wide - Edge - to : 0;
                line.style.translate = new Translate(shift, 0);
            }
        }

        /// <summary>
        /// `e` over `world`: its middle across, and `anchorY` of its height up (1: its bottom on the point); `inside`, slid
        /// in from the screen's edge to be read whole. Where it begins across, or null when it isn't on screen.
        /// </summary>
        static float? Place(VisualElement e, IPanel panel, Camera camera, Vector3? world, float lift, float anchorY, bool inside = false)
        {
            var vp = world != null ? camera.WorldToViewportPoint(world.Value) : Vector3.zero;
            var screen = panel.visualTree.layout.size;
            if (world == null || vp.z <= 0 || float.IsNaN(screen.x))
            {
                e.style.visibility = Visibility.Hidden;
                return null;
            }
            // By the camera's viewport onto the panel's own size: right whether both draw to the screen (the game) or the
            // panel to a texture of its own (Arte's captures), where the screen's pixels aren't the panel's.
            var p = new Vector2(vp.x * screen.x, (1 - vp.y) * screen.y);
            // Its size once laid out (NaN before its first layout: placed by its corner for that one frame).
            var size = e.layout.size;
            var w = float.IsNaN(size.x) ? 0 : size.x;
            var h = float.IsNaN(size.y) ? 0 : size.y;
            e.style.visibility = Visibility.Visible;
            var x = p.x - w / 2;
            if (inside) x = Mathf.Clamp(x, Edge, Mathf.Max(Edge, screen.x - Edge - w));
            e.style.translate = new Translate(x, p.y - h * anchorY + lift);
            return x;
        }

        static void Fill(VisualElement el, FlagView f)
        {
            el.Clear();
            el.EnableInClassList("uflag--enemy", f.Enemy);
            el.EnableInClassList("uflag--target", f.Target);
            if (!string.IsNullOrEmpty(f.HoverText)) Ui.Text(f.HoverText, "uflag__hover uflag__hover--" + f.HoverKind, el);
            // The name only on the picked one's and the one under the pointer (unitViews.ts updateNames: flags kept compact).
            if (!string.IsNullOrEmpty(f.Name)) Ui.Text(f.Name, "uflag__name", el);
            var row = Ui.El("uflag__row", el);
            if (f.Watching)
            {
                var eye = new Eye();
                eye.AddToClassList("uflag__eye");
                row.Add(eye);
            }
            if (f.Cover >= 0) Ui.El("uflag__cover uflag__cover--" + f.Cover, row);
            Ui.Pips(Ui.El("pips pips--hp", row), f.Hp, f.MaxHp);
            if (f.Armor > 0)
            {
                var armor = Ui.El("uflag__armor", row);
                for (var i = 0; i < f.Armor; i++) Ui.El("uflag__armor-pip", armor);
            }
            foreach (var t in f.Tags) Ui.Text(t.Text, "tag tag--" + t.Kind, row);
            // Nothing in a flag takes the pointer: the clicks go through to the map.
            el.Query<VisualElement>().ForEach(x => x.pickingMode = PickingMode.Ignore);
        }
    }
}
