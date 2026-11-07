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
    /// camera is hidden. Over the shoulder only the target's flag stays. An enemy just seen carries a mark at the start of
    /// its flag for 2 s (Spot; Arturo's B2 of 2/11: marks, not callouts).
    /// </summary>
    public sealed class UnitFlags
    {
        const float FloatRise = 40f;
        const float FloatSeconds = 1.4f;
        const float SpotSeconds = 2f;

        readonly VisualElement layer;
        readonly Dictionary<string, VisualElement> flags = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, string> keys = new Dictionary<string, string>();
        readonly List<Floating> floats = new List<Floating>();
        readonly List<string> gone = new List<string>();
        /// <summary>The enemies just seen (Spot): whether they have noticed the squad, and since when.</summary>
        readonly Dictionary<string, (bool alert, float born)> spots = new Dictionary<string, (bool, float)>();

        sealed class Floating
        {
            public VisualElement Label;
            public Vector3 World;
            public float Born;
            /// <summary>A pin's: how long it stays.</summary>
            public float Seconds;
        }

        readonly Func<string, Sprite> icon;

        /// <summary>Another target's line clicked (FlagView.Other): aim at that one (Arturo, 5/11 #11).</summary>
        public event Action<string> TargetPicked;

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
                    Mark(el, f.Id);
                    // Another target's odds take a click (over the shoulder, its only way to another one besides Tab).
                    if (f.Other && el.Q(className: "uflag__hover") is VisualElement line)
                    {
                        var id = f.Id;
                        line.pickingMode = PickingMode.Position;
                        line.name = "pick-" + id;
                        line.AddToClassList("uflag__hover--pick");
                        line.RegisterCallback<ClickEvent>(e =>
                        {
                            e.StopPropagation();
                            TargetPicked?.Invoke(id);
                        });
                    }
                }
            foreach (var id in gone)
            {
                flags[id].RemoveFromHierarchy();
                flags.Remove(id);
                keys.Remove(id);
            }
        }

        /// <summary>
        /// An enemy just seen (Arturo's B2 of 2/11): for 2 s, at the start of its flag, a grey eye if it hasn't noticed the
        /// squad, a red «!» if it has; its flag made now or later (Fill), the mark fading at the end (Follow).
        /// </summary>
        public void Spot(string id, bool alert)
        {
            spots[id] = (alert, Time.time);
            if (flags.TryGetValue(id, out var el)) Mark(el, id);
        }

        /// <summary>A flag's mark of being just seen (Spot), put again at the start of its row; none once it is over.</summary>
        void Mark(VisualElement el, string id)
        {
            el.Q(className: "uflag__spot")?.RemoveFromHierarchy();
            if (!spots.TryGetValue(id, out var spot) || Time.time - spot.born >= SpotSeconds) return;
            var row = el.Q(className: "uflag__row");
            if (row == null) return;
            VisualElement mark = spot.alert ? Ui.Text("!", "uflag__spot uflag__spot--alert") : new EyeMark("uflag__spot uflag__spot--eye");
            mark.pickingMode = PickingMode.Ignore;
            row.Insert(0, mark);
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
                    // A door's, drawn (LineIcon: opened, shut), tinted as the sprites are (Arturo, 7/11).
                    else if (LineIcon.Has(v.Icon))
                    {
                        var drawn = new LineIcon(v.Icon) { pickingMode = PickingMode.Ignore };
                        drawn.style.flexGrow = 1;
                        mark.Add(drawn);
                    }
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

        /// <summary>The line on the tile under the pointer (overlays.ts setNote), what it says, and where it stands.</summary>
        VisualElement note;
        string noteKey;
        Vector3 noteAt;

        /// <summary>
        /// One short line on a tile («La caja de armas · clic: abrir»), or none (null); `iconId`, a drawn icon before it
        /// (LineIcon: a move that is heard, «noise», Ana's 7/11 #3, in place of «Ruido 3: corre»), alone if there are no
        /// words.
        /// </summary>
        public void SetNote(Vector3? world, string text, string iconId = null)
        {
            if (world == null || string.IsNullOrEmpty(text) && !LineIcon.Has(iconId))
            {
                note?.RemoveFromHierarchy();
                note = null;
                noteKey = null;
                return;
            }
            var key = $"{iconId}|{text}";
            if (note == null || key != noteKey)
            {
                note?.RemoveFromHierarchy();
                note = Ui.El("tile-note", layer);
                if (LineIcon.Has(iconId))
                {
                    note.AddToClassList("tile-note--icon");
                    var box = Ui.El("tile-note__icon", note);
                    box.Add(new LineIcon(iconId));
                }
                if (!string.IsNullOrEmpty(text)) Ui.Text(text, "tile-note__text", note);
                note.Query<VisualElement>().ForEach(x => x.pickingMode = PickingMode.Ignore);
                noteKey = key;
            }
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
            // The marks of those just seen, fading over their last half second and gone after 2 s.
            if (spots.Count > 0)
            {
                gone.Clear();
                foreach (var pair in spots)
                {
                    var t = (now - pair.Value.born) / SpotSeconds;
                    var mark = flags.TryGetValue(pair.Key, out var flag) ? flag.Q(className: "uflag__spot") : null;
                    if (t >= 1)
                    {
                        mark?.RemoveFromHierarchy();
                        gone.Add(pair.Key);
                    }
                    else if (mark != null) mark.style.opacity = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
                }
                foreach (var id in gone) spots.Remove(id);
            }
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
            el.EnableInClassList("uflag--other", f.Other);
            // Pointed at from the list (Arturo, 31/10 #11): an arrow over all of it, bobbing.
            if (f.Pointed) el.Add(new PointerArrow());
            // Over it, what the pointer's tile means, the odds; on a plate, kept whole near the screen's edge (KeepLinesIn).
            // (Whether it would see the picked one there is its gauge's eye now: Ana, 7/11 #1.)
            if (!string.IsNullOrEmpty(f.HoverText))
            {
                var line = Ui.El("uflag__hover uflag__hover--" + (f.HoverKind ?? "sight"), el);
                Ui.Text(f.HoverText, "uflag__hover-text", line);
            }
            // The name only on the one under the pointer (Ana, Arturo 31/10 #9: no longer on the picked one's).
            if (!string.IsNullOrEmpty(f.Name)) Ui.Text(f.Name, "uflag__name", el);
            var row = Ui.El("uflag__row", el);
            // (No cover's square by the health: the shield is on the soldier's panel now, Arturo 7/11.)
            if (f.Eye != null)
            {
                // An enemy's one gauge (Ana, 7/11 #1): Synty INTERFACE Fantasy Menus' SPR_FantasyMenus_Menu_Button_08_Background,
                // its health in the long part, in the circle its eye, open if it sees the picked one, closed if not.
                var gauge = Ui.El("uflag__gauge", row);
                gauge.Add(new EyeMark("uflag__gauge-eye uflag__gauge-eye--" + f.Eye, f.Eye == "closed"));
                Ui.Pips(Ui.El("uflag__gauge-life", gauge), f.Hp, f.MaxHp);
            }
            else Ui.Pips(Ui.El("pips pips--hp", row), f.Hp, f.MaxHp);
            if (f.Armor > 0)
            {
                var armor = Ui.El("uflag__armor", row);
                for (var i = 0; i < f.Armor; i++) Ui.El("uflag__armor-pip", armor);
            }
            foreach (var t in f.Tags) Ui.Text(t.Text, "tag tag--" + t.Kind, row);
            // Nothing in a flag takes the pointer: the clicks go through to the map.
            el.Query<VisualElement>().ForEach(x => x.pickingMode = PickingMode.Ignore);
        }

        /// <summary>
        /// An eye, drawn as the move zone's (MoveOverlay.EyeTexture, styles.css's mask): an almond and its pupil, in the
        /// colour of its classes: an enemy just seen that hasn't noticed the squad (Spot: .uflag__spot--eye), an enemy's in
        /// its gauge (.uflag__gauge-eye--open, it sees the picked one); `closed`, its lid down with three lashes
        /// (--closed, it doesn't).
        /// </summary>
        sealed class EyeMark : VisualElement
        {
            readonly bool closed;

            public EyeMark(string classes, bool closed = false)
            {
                this.closed = closed;
                foreach (var c in classes.Split(' ')) AddToClassList(c);
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width <= 0 || r.height <= 0) return;
                // In the drawing's 16 × 10 units, centred.
                var k = Mathf.Min(r.width / 16, r.height / 10);
                var o = new Vector2(r.x + (r.width - 16 * k) / 2, r.y + (r.height - 10 * k) / 2);
                Vector2 At(float x, float y) => o + new Vector2(x, y) * k;
                var p = ctx.painter2D;
                p.strokeColor = p.fillColor = resolvedStyle.color;
                p.lineWidth = 1.6f * k;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                if (closed)
                {
                    p.BeginPath();
                    p.MoveTo(At(0.8f, 3.6f));
                    p.BezierCurveTo(At(4, 7.6f), At(12, 7.6f), At(15.2f, 3.6f));
                    p.Stroke();
                    foreach (var (x0, y0, x1, y1) in new[] { (4.2f, 6.2f, 3.2f, 8.8f), (8f, 6.9f, 8f, 9.6f), (11.8f, 6.2f, 12.8f, 8.8f) })
                    {
                        p.BeginPath();
                        p.MoveTo(At(x0, y0));
                        p.LineTo(At(x1, y1));
                        p.Stroke();
                    }
                    return;
                }
                p.BeginPath();
                p.MoveTo(At(0.8f, 5));
                p.BezierCurveTo(At(4, 0.2f), At(12, 0.2f), At(15.2f, 5));
                p.BezierCurveTo(At(12, 9.8f), At(4, 9.8f), At(0.8f, 5));
                p.ClosePath();
                p.Stroke();
                p.BeginPath();
                p.Arc(At(8, 5), 2.2f * k, Angle.Degrees(0), Angle.Degrees(360));
                p.Fill();
            }
        }

        /// <summary>
        /// The arrow over a soldier pointed at from the list (#11): pointing down, in the lit ring's warm light
        /// (Flags.uss .uflag__pointer) over a dark stroke, bobbing a few pixels.
        /// </summary>
        sealed class PointerArrow : VisualElement
        {
            public PointerArrow()
            {
                AddToClassList("uflag__pointer");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                var start = Time.time;
                schedule.Execute(() => style.translate = new Translate(0, Mathf.Sin((Time.time - start) * 7) * 3)).Every(16).Until(() => panel == null);
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width <= 0 || r.height <= 0) return;
                var k = Mathf.Min(r.width, r.height) / 24;
                var o = new Vector2(r.x + (r.width - 24 * k) / 2, r.y + (r.height - 24 * k) / 2);
                Vector2 At(float x, float y) => o + new Vector2(x, y) * k;
                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                foreach (var (width, colour) in new[] { (6.5f, new Color(0.04f, 0.05f, 0.05f, 0.85f)), (3.4f, resolvedStyle.color) })
                {
                    p.strokeColor = colour;
                    p.lineWidth = width * k;
                    p.BeginPath();
                    p.MoveTo(At(12, 3));
                    p.LineTo(At(12, 19));
                    p.MoveTo(At(5.5f, 12.5f));
                    p.LineTo(At(12, 19.5f));
                    p.LineTo(At(18.5f, 12.5f));
                    p.Stroke();
                }
            }
        }
    }
}
