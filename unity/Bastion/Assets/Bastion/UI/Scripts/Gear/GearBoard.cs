using System;
using System.Collections.Generic;
using Bastion.Client;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Gear
{
    /// <summary>
    /// The grids of the runs (.dev-snapshots/gameplay/runs/PLAN-RUNS.md, H0-DISENO.md §2): one piece for the base's stash,
    /// the soldiers' packs and kits, and the pack in combat. Each grid, kit place and the bin is drawn into a holder the
    /// screen owns; one thing is dragged at a time from any of them to any other.
    /// - **Dragging:** a press and a move of a few points lifts the thing; under the pointer, the cells it would take, green
    ///   if the engine takes it there (IGearHandlers.GearRefusal), red with its reason beside the pointer if not; on a pile
    ///   of the same, that pile; a weapon mod on a weapon, that weapon (to mount it); the kit's places that take it, lit. Letting go where it can gives the order (GearMoved);
    ///   anywhere else, nothing. R or the right button turn it as it is dragged; with Mayús held as it lifts, half the pile.
    ///   Esc lets it go back.
    /// - **A click** picks it: under it its orders, Girar (in place), Partir (how many, into the first room of its grid),
    ///   Tirar and the handlers' own (Usar…). The same for the finger, which can't press R.
    /// - **The bin:** let go over it, or Tirar, asks first; then GearDropped.
    /// - **The card** on hover, the handlers' (name, figures); not while dragging.
    /// Until the engine's answer comes the dropped thing is hidden where it was (1.5 s at most: refused, it shows again).
    /// </summary>
    public sealed class GearBoard
    {
        /// <summary>A cell's side, in panel points (1440×900): 52 in the base, 40 in combat. Arte's pictures, 128 px a cell.</summary>
        public float Cell = 52;
        /// <summary>Arte's picture of a kind and type (GearPictures.Of); null or none, the HUD's icon.</summary>
        public Func<string, string, Texture> PictureOf;
        /// <summary>Things can be thrown away (the base's bin, Tirar); not in combat, where Soltar leaves them on the floor.</summary>
        public bool CanThrow = true;

        const float LiftPoints = 6;
        const float PendingSeconds = 1.5f;

        readonly IGearHandlers handlers;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        /// <summary>Over the screen: the dragged thing, its reason, the picked thing's orders.</summary>
        readonly VisualElement layer;
        /// <summary>Over everything while a press lasts: it holds the pointer, so that a grid drawn again doesn't drop it.</summary>
        readonly VisualElement catcher;
        readonly Label why;
        readonly VisualElement orders;

        readonly List<Target> targets = new List<Target>();
        readonly Dictionary<VisualElement, Target> byHolder = new Dictionary<VisualElement, Target>();
        Press press;
        /// <summary>The thing picked by a click: its orders show under it.</summary>
        string picked;
        /// <summary>Partir open for the picked thing: how many go.</summary>
        int splitting;
        /// <summary>The thing asked about at the bin.</summary>
        GearItemView throwing;
        string pending;
        float pendingUntil;
        /// <summary>Grids and places drawn anew so far: an order whose answer drew them at once needs no wait.</summary>
        int redraws;

        public GearBoard(VisualElement root, TooltipLayer tips, Func<string, Sprite> icon, IGearHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            var sheet = Resources.Load<StyleSheet>("Gear/Gear");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            layer = Ui.El("gear-layer", root);
            layer.pickingMode = PickingMode.Ignore;
            catcher = Ui.El("gear-catcher", layer);
            Ui.Show(catcher, false);
            catcher.RegisterCallback<PointerMoveEvent>(e => Moved(e.pointerId, e.position));
            catcher.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button == 0 || GameInput.Touch) Up(e.pointerId);
            });
            catcher.RegisterCallback<PointerDownEvent>(e =>
            {
                // The right button, as it is dragged: turns it.
                if (press != null && press.Lifted && e.button == 1) Turn();
            });
            catcher.RegisterCallback<PointerCaptureOutEvent>(_ => Cancel());
            why = Ui.Text("", "gear-why", layer);
            why.pickingMode = PickingMode.Ignore;
            Ui.Show(why, false);
            orders = Ui.El("gear-orders panel", layer);
            Ui.Show(orders, false);
            // A press anywhere else closes the picked thing's orders.
            root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (picked == null || press != null) return;
                for (var t = e.target as VisualElement; t != null; t = t.parent)
                    if (t == orders) return;
                Unpick();
            }, TrickleDown.TrickleDown);
        }

        /// <summary>Whether a thing is being dragged.</summary>
        public bool Dragging => press != null && press.Lifted;

        /// <summary>Each frame, from the screen's Update: R turns what is dragged, Esc lets it go; the dropped thing's wait.</summary>
        public void Tick()
        {
            if (Dragging && GameInput.Down(GameInput.Key.Rotate)) Turn();
            if (pending != null && Time.realtimeSinceStartup > pendingUntil) Unhide();
        }

        /// <summary>Esc: what is dragged goes back, else the bin's question or the orders close. Whether it took it.</summary>
        public bool Close()
        {
            if (press != null)
            {
                Cancel();
                return true;
            }
            if (throwing != null)
            {
                Ask(null);
                return true;
            }
            if (picked != null)
            {
                Unpick();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>A grid drawn into `holder` (anew only if it changed). Its cells scroll past `v.Rows`.</summary>
        public void SetGrid(VisualElement holder, GearGridView v)
        {
            var key = "grid:" + Ui.Key(v);
            var t = Own(holder, key, TargetKind.Grid);
            if (t == null) return;
            t.Owner = v.Owner;
            t.Slot = v.Slot;
            t.W = v.W;
            t.H = v.H;
            t.Mine = v.Mine;
            t.Items = v.Items;
            var box = Ui.El("gear-grid", holder);
            box.EnableInClassList("gear-grid--theirs", !v.Mine);
            var cells = new VisualElement();
            cells.AddToClassList("gear-cells");
            cells.style.width = v.W * Cell;
            cells.style.height = v.H * Cell;
            var w = v.W;
            var h = v.H;
            var cell = Cell;
            cells.generateVisualContent += ctx => Lines(ctx, w, h, cell);
            if (v.Rows > 0 && v.Rows < v.H)
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Auto, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                scroll.AddToClassList("gear-scroll");
                scroll.style.height = v.Rows * Cell;
                scroll.style.width = v.W * Cell;
                scroll.Add(cells);
                box.Add(scroll);
                t.Clip = scroll.contentViewport;
            }
            else
            {
                box.Add(cells);
                t.Clip = null;
            }
            t.Element = cells;
            foreach (var p in v.Items)
            {
                var (iw, ih) = Size(p.Gear, p.Turned);
                var el = Item(p.Gear, iw * Cell, ih * Cell, p.Gear.W * Cell, p.Gear.H * Cell, p.Turned, t);
                el.style.left = p.X * Cell;
                el.style.top = p.Y * Cell;
                cells.Add(el);
            }
            Redrawn();
        }

        /// <summary>One of a kit's places drawn into `holder`: its name while empty, else its thing fitted to the box.</summary>
        public void SetSlot(VisualElement holder, GearSlotView v)
        {
            var key = "slot:" + Ui.Key(v);
            var t = Own(holder, key, TargetKind.Slot);
            if (t == null) return;
            t.Owner = v.Owner;
            t.Slot = v.Slot;
            t.W = v.W;
            t.H = v.H;
            t.Mine = v.Mine;
            t.Gear = v.Gear;
            var box = Ui.El("gear-slot", holder);
            box.EnableInClassList("gear-slot--theirs", !v.Mine);
            box.style.width = v.W * Cell;
            box.style.height = v.H * Cell;
            t.Element = box;
            if (v.Gear == null)
            {
                var label = Ui.Text(v.Label ?? GearText.SlotLabel(v.Slot), "gear-slot__label font-title", box);
                label.pickingMode = PickingMode.Ignore;
            }
            else
            {
                // Lying across in its box, as big as fits.
                var gw = Mathf.Min(v.Gear.W, v.W) * Cell;
                var gh = Mathf.Min(v.Gear.H, v.H) * Cell;
                var el = Item(v.Gear, v.W * Cell, v.H * Cell, gw, gh, false, t);
                el.style.left = 0;
                el.style.top = 0;
                box.Add(el);
            }
            Redrawn();
        }

        /// <summary>The bin drawn into `holder`: a thing let go over it is asked about, then thrown away.</summary>
        public void SetBin(VisualElement holder)
        {
            var key = "bin:" + (throwing?.Id ?? "");
            var t = Own(holder, key, TargetKind.Bin);
            if (t == null) return;
            var bin = Ui.El("gear-bin", holder);
            t.Element = bin;
            if (throwing == null)
            {
                Ui.Text(GearText.Bin, "gear-bin__label font-title", bin).pickingMode = PickingMode.Ignore;
                return;
            }
            bin.AddToClassList("gear-bin--asking");
            Ui.Text(GearText.ThrowAsk(throwing.The ?? throwing.Name?.ToLowerInvariant(), throwing.Count), "gear-bin__ask", bin);
            var row = Ui.El("gear-bin__row", bin);
            var id = throwing.Id;
            Button(row, GearText.Throw, "gear-order--danger", () =>
            {
                Ask(null);
                Give(id, () => handlers.GearDropped(id));
            });
            Button(row, GearText.No, null, () => Ask(null));
        }

        /// <summary>The holder's target, drawn anew (cleared) if its key changed; null if it is as it was.</summary>
        Target Own(VisualElement holder, string key, TargetKind kind)
        {
            if (!byHolder.TryGetValue(holder, out var t))
            {
                // Kept while its holder is out of a panel: a capture moves the whole document to a panel of its own after
                // the screens are built. The gone ones are dropped as a drag starts (Lift).
                t = new Target { Holder = holder, Kind = kind };
                byHolder[holder] = t;
                targets.Add(t);
            }
            if (t.Key == key && t.Element != null && t.Element.panel != null) return null;
            t.Key = key;
            t.Kind = kind;
            holder.Clear();
            // The state changed: what was let go is wherever it now is.
            redraws++;
            pending = null;
            return t;
        }

        /// <summary>A thing's tile: `w`×`h` points, its picture `pw`×`ph` lying across in the middle (turned a quarter if so).</summary>
        VisualElement Item(GearItemView g, float w, float h, float pw, float ph, bool turned, Target from)
        {
            var el = Ui.El("gear-item gear-item--" + (g.Kind ?? "thing"));
            el.style.width = w;
            el.style.height = h;
            el.userData = g.Id;
            el.EnableInClassList("gear-item--picked", g.Id == picked);
            el.EnableInClassList("gear-item--away", g.Id == pending || (press != null && press.Lifted && press.Gear.Id == g.Id));
            Picture(el, g, w, h, pw, ph, turned);
            // The weapons' names, where the tile is wide enough to read them (not on one turned on end).
            if (g.Kind == "weapon" && !string.IsNullOrEmpty(g.Name) && w >= 2 * Cell - 1) Ui.Text(g.Name, "gear-item__name", el).pickingMode = PickingMode.Ignore;
            if (g.Pile > 1) Ui.Text(g.Count.ToString(), "gear-item__count font-title", el).pickingMode = PickingMode.Ignore;
            if (g.Card != null) tips.Attach(el, g.Card);
            else tips.Attach(el, g.Tip ?? g.Name);
            el.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!(e.button == 0 || GameInput.Touch) || press != null) return;
                var p = (Vector2)e.position;
                var at = p - el.worldBound.position;
                press = new Press
                {
                    Gear = g,
                    From = from,
                    Pointer = e.pointerId,
                    Start = p,
                    // Where it was grabbed, in cells of the thing as it lies now.
                    Grab = new Vector2(at.x / Cell, at.y / Cell),
                    Turned = turned,
                    WasTurned = turned,
                    Count = GameInput.Shift && g.Count > 1 ? g.Count / 2 : 0,
                };
                // In a kit's place it is drawn fitted to the box: grabbed in its middle.
                if (from.Kind == TargetKind.Slot) press.Grab = new Vector2(g.W / 2f, g.H / 2f);
                Ui.Show(catcher, true);
                layer.BringToFront();
                catcher.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            return el;
        }

        void Picture(VisualElement el, GearItemView g, float w, float h, float pw, float ph, bool turned)
        {
            var pic = Ui.El("gear-item__pic", el);
            pic.pickingMode = PickingMode.Ignore;
            pic.style.width = pw;
            pic.style.height = ph;
            pic.style.left = (w - pw) / 2;
            pic.style.top = (h - ph) / 2;
            if (turned) pic.style.rotate = new StyleRotate(new Rotate(new Angle(90)));
            var art = PictureOf?.Invoke(g.Kind, g.Type) as Texture2D;
            if (art != null) pic.style.backgroundImage = new StyleBackground(art);
            else if (g.Icon != null && g.Icon.Length == 1)
            {
                // A sign of the game's fonts: the supplies' «§», our crate.
                var glyph = Ui.Text(g.Icon, "gear-item__glyph font-title", pic);
                glyph.pickingMode = PickingMode.Ignore;
                glyph.style.fontSize = Mathf.Min(pw, ph) * 0.62f;
            }
            else if (LineIcon.Has(g.Icon))
            {
                // Our own line drawings (the molotov), square in the middle.
                pic.AddToClassList("gear-item__pic--icon");
                var side = Mathf.Min(pw, ph);
                var line = new LineIcon(g.Icon) { pickingMode = PickingMode.Ignore };
                line.AddToClassList("gear-item__line");
                line.style.width = side;
                line.style.height = side;
                line.style.left = (pw - side) / 2;
                line.style.top = (ph - side) / 2;
                pic.Add(line);
            }
            else
            {
                // The HUD's icon, square, in the middle of the thing's size.
                pic.AddToClassList("gear-item__pic--icon");
                var sprite = icon?.Invoke(g.Icon);
                if (sprite != null) pic.style.backgroundImage = new StyleBackground(sprite);
            }
        }

        static void Lines(MeshGenerationContext ctx, int w, int h, float cell)
        {
            var p = ctx.painter2D;
            p.strokeColor = new Color(1, 1, 1, 0.07f);
            p.lineWidth = 1;
            p.BeginPath();
            for (var x = 1; x < w; x++)
            {
                p.MoveTo(new Vector2(x * cell, 0));
                p.LineTo(new Vector2(x * cell, h * cell));
            }
            for (var y = 1; y < h; y++)
            {
                p.MoveTo(new Vector2(0, y * cell));
                p.LineTo(new Vector2(w * cell, y * cell));
            }
            p.Stroke();
        }

        static (int, int) Size(GearItemView g, bool turned) => turned ? (g.H, g.W) : (g.W, g.H);

        // ------------------------------------------------------------------ dragging

        void Moved(int pointer, Vector2 p)
        {
            if (press == null || press.Pointer != pointer) return;
            press.At = p;
            if (!press.Lifted)
            {
                if ((p - press.Start).magnitude < LiftPoints) return;
                Lift();
            }
            Hover();
        }

        void Up(int pointer)
        {
            if (press == null || press.Pointer != pointer) return;
            var was = press;
            if (!was.Lifted)
            {
                End();
                Pick(was.Gear, was.From);
                return;
            }
            var move = was.Move;
            var ok = move != null && was.Why == null;
            End();
            if (ok && move.Throw) Ask(was.Gear);
            else if (ok) Give(was.Gear.Id, () => handlers.GearMoved(move));
        }

        void Lift()
        {
            press.Lifted = true;
            tips.Hide();
            Unpick();
            foreach (var gone in targets.FindAll(t => t.Element == null || t.Element.panel == null)) byHolder.Remove(gone.Holder);
            targets.RemoveAll(t => t.Element == null || t.Element.panel == null);
            var g = press.Gear;
            var ghost = Ui.El("gear-ghost", layer);
            ghost.pickingMode = PickingMode.Ignore;
            press.Ghost = ghost;
            Ghost();
            Away(g.Id, true);
            // The kit's places that take it, lit while it is dragged.
            foreach (var t in targets)
            {
                if (t.Kind != TargetKind.Slot || !t.Mine) continue;
                var m = new GearMove { Gear = g.Id, Count = press.Count, To = t.Owner, Slot = t.Slot };
                t.Element.EnableInClassList("gear-slot--takes", handlers.GearRefusal(m) == null);
            }
        }

        /// <summary>The dragged thing's picture, as it lies now (turned or not).</summary>
        void Ghost()
        {
            var g = press.Gear;
            var ghost = press.Ghost;
            ghost.Clear();
            var (w, h) = Size(g, press.Turned);
            ghost.style.width = w * Cell;
            ghost.style.height = h * Cell;
            Picture(ghost, g, w * Cell, h * Cell, g.W * Cell, g.H * Cell, press.Turned);
            var count = press.Count > 0 ? press.Count : g.Count;
            if (g.Pile > 1) Ui.Text(count.ToString(), "gear-item__count font-title", ghost).pickingMode = PickingMode.Ignore;
        }

        void Turn()
        {
            if (!Dragging) return;
            press.Turned = !press.Turned;
            press.Grab = new Vector2(press.Grab.y, press.Grab.x);
            Ghost();
            press.Asked = null;
            Hover();
        }

        /// <summary>Where it would go under the pointer, asked of the engine when that changes; the ghost, the cells and the reason drawn.</summary>
        void Hover()
        {
            var p = press.At;
            var g = press.Gear;
            var (w, h) = Size(g, press.Turned);
            press.Ghost.style.left = p.x - press.Grab.x * Cell;
            press.Ghost.style.top = p.y - press.Grab.y * Cell;
            // The reason follows the pointer.
            if (press.Why != null) Why(p);

            Target over = null;
            for (var i = targets.Count - 1; i >= 0; i--)
            {
                var t = targets[i];
                if (t.Element == null || t.Element.panel == null) continue;
                if (t.Clip != null && !t.Clip.worldBound.Contains(p)) continue;
                if (t.Element.worldBound.Contains(p))
                {
                    over = t;
                    break;
                }
            }
            GearMove move = null;
            PlacedView pile = null;
            PlacedView onto = null;
            if (over != null && over.Kind == TargetKind.Slot && g.Kind == "mod" && over.Gear?.Kind == "weapon")
                move = new GearMove { Gear = g.Id, Onto = over.Gear.Id };
            else if (over != null && over.Kind == TargetKind.Grid)
            {
                var local = p - over.Element.worldBound.position;
                var x = Mathf.Clamp(Mathf.FloorToInt(local.x / Cell - press.Grab.x + 0.5f), 0, Mathf.Max(0, over.W - w));
                var y = Mathf.Clamp(Mathf.FloorToInt(local.y / Cell - press.Grab.y + 0.5f), 0, Mathf.Max(0, over.H - h));
                // On a pile of the same: onto it.
                var cx = Mathf.FloorToInt(local.x / Cell);
                var cy = Mathf.FloorToInt(local.y / Cell);
                if (over.Items != null)
                    foreach (var it in over.Items)
                    {
                        if (it.Gear.Id == g.Id) continue;
                        var (iw, ih) = Size(it.Gear, it.Turned);
                        if (!(cx >= it.X && cx < it.X + iw && cy >= it.Y && cy < it.Y + ih)) continue;
                        if (g.Pile > 1 && it.Gear.Kind == g.Kind && it.Gear.Type == g.Type) pile = it;
                        // A mod on a weapon: mounted on it.
                        else if (g.Kind == "mod" && it.Gear.Kind == "weapon") onto = it;
                    }
                move = onto != null
                    ? new GearMove { Gear = g.Id, Onto = onto.Gear.Id }
                    : pile != null
                    ? new GearMove { Gear = g.Id, Count = press.Count, To = over.Owner, Slot = over.Slot, X = pile.X, Y = pile.Y, Turned = pile.Turned }
                    : new GearMove { Gear = g.Id, Count = press.Count, To = over.Owner, Slot = over.Slot, X = x, Y = y, Turned = press.Turned };
            }
            else if (over != null && over.Kind == TargetKind.Slot)
                move = new GearMove { Gear = g.Id, Count = press.Count, To = over.Owner, Slot = over.Slot };
            else if (over != null && over.Kind == TargetKind.Bin)
                move = new GearMove { Gear = g.Id, Throw = true };

            var asked = move?.ToString() ?? "";
            if (asked == press.Asked && over == press.Over) return;
            press.Asked = asked;
            Unmark(press);
            press.Over = over;
            press.Move = move;
            press.Why = move != null ? handlers.GearRefusal(move) : null;
            var ok = press.Why == null;

            if (over != null && over.Kind == TargetKind.Grid && move != null)
            {
                var foot = Ui.El("gear-foot " + (ok ? "gear-foot--ok" : "gear-foot--no"), over.Element);
                foot.pickingMode = PickingMode.Ignore;
                var on = onto ?? pile;
                if (on != null)
                {
                    move.X = on.X;
                    move.Y = on.Y;
                }
                var (fw, fh) = on != null ? Size(on.Gear, on.Turned) : (w, h);
                foot.style.left = move.X * Cell;
                foot.style.top = move.Y * Cell;
                foot.style.width = fw * Cell;
                foot.style.height = fh * Cell;
                press.Foot = foot;
            }
            else if (over != null && over.Kind == TargetKind.Slot) over.Element.AddToClassList(ok ? "gear-slot--ok" : "gear-slot--no");
            else if (over != null && over.Kind == TargetKind.Bin)
            {
                over.Element.AddToClassList(ok ? "gear-bin--over" : "gear-bin--no");
                var label = over.Element.Q<Label>(className: "gear-bin__label");
                if (label != null) label.text = GearText.BinOver;
            }

            why.text = press.Why ?? "";
            Ui.Show(why, !ok);
            Why(p);
            press.Ghost.EnableInClassList("gear-ghost--no", !ok || move == null);
        }

        /// <summary>
        /// The reason beside the pointer, below it to the right; near the screen's right edge, to its left, anchored by its
        /// right side (an absolute label shrinks against the edge instead of growing past it, so its width can't tell).
        /// </summary>
        void Why(Vector2 p)
        {
            const float Widest = 300;
            var room = layer.layout.width;
            var left = float.IsNaN(room) || p.x + 18 + Widest <= room - 8;
            why.style.top = p.y + 20;
            if (left)
            {
                why.style.left = p.x + 18;
                why.style.right = StyleKeyword.Auto;
            }
            else
            {
                why.style.left = StyleKeyword.Auto;
                why.style.right = room - (p.x - 18);
            }
        }

        /// <summary>The marks of where it was over taken off.</summary>
        static void Unmark(Press press)
        {
            press.Foot?.RemoveFromHierarchy();
            press.Foot = null;
            var t = press.Over;
            if (t?.Element == null) return;
            t.Element.RemoveFromClassList("gear-slot--ok");
            t.Element.RemoveFromClassList("gear-slot--no");
            if (t.Kind == TargetKind.Bin)
            {
                t.Element.RemoveFromClassList("gear-bin--over");
                t.Element.RemoveFromClassList("gear-bin--no");
                var label = t.Element.Q<Label>(className: "gear-bin__label");
                if (label != null) label.text = GearText.Bin;
            }
        }

        /// <summary>Esc, or the pointer lost: it goes back.</summary>
        void Cancel()
        {
            if (press == null) return;
            End();
        }

        void End()
        {
            var was = press;
            if (was == null) return;
            press = null;
            // (The tests' press has no pointer of its own.)
            if (was.Pointer >= 0 && catcher.HasPointerCapture(was.Pointer)) catcher.ReleasePointer(was.Pointer);
            Ui.Show(catcher, false);
            Ui.Show(why, false);
            if (!was.Lifted) return;
            Unmark(was);
            was.Ghost?.RemoveFromHierarchy();
            foreach (var t in targets) t.Element?.RemoveFromClassList("gear-slot--takes");
            Away(was.Gear.Id, was.Gear.Id == pending);
        }

        /// <summary>The thing's tiles dimmed (`on`) where they are drawn.</summary>
        void Away(string id, bool on)
        {
            foreach (var t in targets)
                t.Element?.Query(className: "gear-item").ForEach(e =>
                {
                    if (e.userData as string == id) e.EnableInClassList("gear-item--away", on);
                });
        }

        /// <summary>Hidden where it was until the engine's answer draws it again (or not, and it shows).</summary>
        void Hide(string id)
        {
            pending = id;
            pendingUntil = Time.realtimeSinceStartup + PendingSeconds;
            Away(id, true);
        }

        void Unhide()
        {
            var id = pending;
            pending = null;
            Away(id, false);
        }

        /// <summary>A grid or place drawn anew: the picked thing's orders follow it.</summary>
        void Redrawn()
        {
            if (picked != null) Pick(picked);
        }

        /// <summary>The order given; its thing hidden where it was until the state comes, unless it came at once.</summary>
        void Give(string id, Action order)
        {
            var before = redraws;
            order();
            if (redraws == before) Hide(id);
        }

        // ------------------------------------------------------------------ picking

        void Pick(GearItemView g, Target from)
        {
            if (!from.Mine) return;
            if (picked == g.Id)
            {
                Unpick();
                return;
            }
            picked = g.Id;
            splitting = 0;
            Pick(g.Id);
        }

        /// <summary>The picked thing's orders under its tile (again after a grid is drawn anew; gone if it is no more).</summary>
        void Pick(string id)
        {
            VisualElement tile = null;
            Target from = null;
            foreach (var t in targets)
            {
                if (t.Element == null || t.Element.panel == null) continue;
                t.Element.Query(className: "gear-item").ForEach(e =>
                {
                    var mine = e.userData as string == id;
                    e.EnableInClassList("gear-item--picked", mine);
                    if (mine)
                    {
                        tile = e;
                        from = t;
                    }
                });
            }
            if (tile == null)
            {
                Unpick();
                return;
            }
            var g = Find(from, id);
            if (g == null)
            {
                Unpick();
                return;
            }
            orders.Clear();
            Ui.Show(orders, true);
            layer.BringToFront();
            if (splitting > 0) SplitRow(g, from);
            else
            {
                var placed = from.Kind == TargetKind.Grid ? from.Items.Find(p => p.Gear.Id == id) : null;
                // Girar, for what isn't square.
                if (placed != null && g.W != g.H)
                {
                    var turn = new GearMove { Gear = id, To = from.Owner, Slot = from.Slot, X = placed.X, Y = placed.Y, Turned = !placed.Turned };
                    var no = handlers.GearRefusal(turn);
                    Button(orders, GearText.Turn, null, () =>
                    {
                        Unpick();
                        Give(id, () => handlers.GearMoved(turn));
                    }, no);
                }
                if (g.Count > 1 && from.Kind == TargetKind.Grid)
                    Button(orders, GearText.Split, null, () =>
                    {
                        splitting = Math.Max(1, g.Count / 2);
                        Pick(id);
                    });
                foreach (var a in handlers.GearActions(id) ?? Array.Empty<GearActionView>())
                {
                    var action = a.Id;
                    Button(orders, a.Label, null, () =>
                    {
                        Unpick();
                        handlers.GearAction(id, action);
                    }, a.Why);
                }
                if (CanThrow)
                    Button(orders, GearText.Throw, "gear-order--danger", () =>
                    {
                        Unpick();
                        Ask(g);
                    });
            }
            // Under its tile, kept inside the screen (placed once its size is known).
            orders.style.left = tile.worldBound.xMin;
            orders.style.top = tile.worldBound.yMax + 4;
            orders.schedule.Execute(() =>
            {
                if (tile.panel == null) return;
                var root = layer.parent?.worldBound ?? Rect.zero;
                var x = Mathf.Clamp(tile.worldBound.xMin, root.xMin + 8, root.xMax - orders.layout.width - 8);
                var y = tile.worldBound.yMax + 4;
                if (y + orders.layout.height > root.yMax - 8) y = tile.worldBound.yMin - orders.layout.height - 4;
                orders.style.left = x;
                orders.style.top = y;
            });
        }

        /// <summary>Partir: how many go (−, +), and the first room in its grid that takes them.</summary>
        void SplitRow(GearItemView g, Target from)
        {
            var id = g.Id;
            Button(orders, "−", "gear-order--step", () =>
            {
                splitting = Math.Max(1, splitting - 1);
                Pick(id);
            }, splitting <= 1 ? "" : null);
            Ui.Text(GearText.SplitCount(splitting, g.Count), "gear-orders__count font-title", orders);
            Button(orders, "+", "gear-order--step", () =>
            {
                splitting = Math.Min(g.Count - 1, splitting + 1);
                Pick(id);
            }, splitting >= g.Count - 1 ? "" : null);
            var room = Room(from, g, splitting);
            Button(orders, GearText.Split, null, () =>
            {
                Unpick();
                handlers.GearMoved(room);
            }, room == null ? GearText.NoRoomToSplit : null);
            Button(orders, GearText.No, null, () =>
            {
                splitting = 0;
                Pick(id);
            });
        }

        /// <summary>The first place in `t`, row by row, lying across first, where `count` of `g` can go.</summary>
        GearMove Room(Target t, GearItemView g, int count)
        {
            for (var y = 0; y < t.H; y++)
                for (var x = 0; x < t.W; x++)
                    foreach (var turned in new[] { false, true })
                    {
                        var (w, h) = Size(g, turned);
                        if (x + w > t.W || y + h > t.H) continue;
                        var m = new GearMove { Gear = g.Id, Count = count, To = t.Owner, Slot = t.Slot, X = x, Y = y, Turned = turned };
                        if (Free(t, m, w, h) && handlers.GearRefusal(m) == null) return m;
                    }
            return null;
        }

        /// <summary>No other thing in those cells (asked before the engine, so that a pile is not split onto another).</summary>
        static bool Free(Target t, GearMove m, int w, int h)
        {
            foreach (var p in t.Items)
            {
                var (pw, ph) = Size(p.Gear, p.Turned);
                if (m.X < p.X + pw && p.X < m.X + w && m.Y < p.Y + ph && p.Y < m.Y + h) return false;
            }
            return true;
        }

        static GearItemView Find(Target t, string id)
        {
            if (t.Kind == TargetKind.Slot) return t.Gear?.Id == id ? t.Gear : null;
            return t.Items?.Find(p => p.Gear.Id == id)?.Gear;
        }

        void Unpick()
        {
            if (picked == null) return;
            var id = picked;
            picked = null;
            splitting = 0;
            orders.Clear();
            Ui.Show(orders, false);
            foreach (var t in targets)
                t.Element?.Query(className: "gear-item").ForEach(e =>
                {
                    if (e.userData as string == id) e.RemoveFromClassList("gear-item--picked");
                });
        }

        /// <summary>The bin asks about `g` (null: no more); each bin drawn anew.</summary>
        void Ask(GearItemView g)
        {
            throwing = g;
            foreach (var t in targets.ToArray())
                if (t.Kind == TargetKind.Bin && t.Holder.panel != null) SetBin(t.Holder);
        }

        /// <summary>An order's button; with `why`, off, and why on hover (empty: off, no words).</summary>
        void Button(VisualElement parent, string text, string classes, Action click, string why = null)
        {
            var b = new Button(click) { text = text };
            Ui.AddClasses(b, "gear-order font-title " + (classes ?? ""));
            parent.Add(b);
            if (why == null) return;
            b.SetEnabled(false);
            b.AddToClassList("gear-order--off");
            if (why.Length > 0) tips.Attach(b, why);
        }

        // ------------------------------------------------------------------ tests

        /// <summary>Tests (the lab): a press on `id`'s tile at its middle, dragged to `panel` points, turned `turns` times, let go or not.</summary>
        public bool TestDrag(string id, Vector2 to, int turns = 0, bool release = true, bool half = false)
        {
            VisualElement tile = null;
            Target from = null;
            foreach (var t in targets)
                t.Element?.Query(className: "gear-item").ForEach(e =>
                {
                    if (tile == null && e.userData as string == id)
                    {
                        tile = e;
                        from = t;
                    }
                });
            var g = from != null ? Find(from, id) : null;
            if (g == null) return false;
            // A drag of the tests' still held goes back first.
            if (press != null) End();
            var start = tile.worldBound.center;
            var turned = from.Kind == TargetKind.Grid && from.Items.Find(p => p.Gear.Id == id).Turned;
            var (w, h) = Size(g, turned);
            press = new Press { Gear = g, From = from, Pointer = -7, Start = start, Grab = new Vector2(w / 2f, h / 2f), Turned = turned, WasTurned = turned, Count = half && g.Count > 1 ? g.Count / 2 : 0 };
            press.At = to;
            Lift();
            for (var i = 0; i < turns; i++) Turn();
            Hover();
            if (release) Up(-7);
            return true;
        }

        /// <summary>
        /// Tests: `id` dragged over the grid or kit place of `owner` («stash» or a soldier's id) and `slot` (null for the
        /// stash, «pack», a kit place), at cell `x`, `y` in a grid (its middle), turned or not; let go or held there.
        /// </summary>
        public bool TestDragTo(string id, string owner, string slot, int x = 0, int y = 0, bool turned = false, bool release = false)
        {
            var t = targets.Find(a => a.Element != null && a.Element.panel != null && a.Owner == owner && (a.Slot ?? "") == (slot ?? "") && a.Kind != TargetKind.Bin);
            if (t == null) return false;
            GearItemView g = null;
            bool was = false;
            foreach (var s in targets)
            {
                if (g != null) break;
                g = s.Element != null ? Find(s, id) : null;
                if (g != null && s.Kind == TargetKind.Grid) was = s.Items.Find(p => p.Gear.Id == id).Turned;
            }
            if (g == null) return false;
            var (w, h) = Size(g, turned);
            var at = t.Kind == TargetKind.Slot ? t.Element.worldBound.center : t.Element.worldBound.position + new Vector2((x + w / 2f) * Cell, (y + h / 2f) * Cell);
            return TestDrag(id, at, turned != was ? 1 : 0, release);
        }

        /// <summary>Tests: a click on `id`'s tile (its orders open), with Partir open at `split` if above 0.</summary>
        public bool TestPick(string id, int split = 0)
        {
            foreach (var t in targets)
            {
                var g = t.Element != null ? Find(t, id) : null;
                if (g == null) continue;
                picked = null;
                Pick(g, t);
                if (split > 0)
                {
                    splitting = split;
                    Pick(id);
                }
                return true;
            }
            return false;
        }

        /// <summary>Tests: the bin's question about `id`.</summary>
        public bool TestThrow(string id)
        {
            foreach (var t in targets)
            {
                var g = t.Element != null ? Find(t, id) : null;
                if (g == null) continue;
                Ask(g);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ kinds

        enum TargetKind { Grid, Slot, Bin }

        sealed class Target
        {
            public VisualElement Holder;
            /// <summary>Where the cells (or the box) are: what the pointer is tested against.</summary>
            public VisualElement Element;
            /// <summary>The scroll's viewport, when the grid scrolls: the pointer must be inside it too.</summary>
            public VisualElement Clip;
            public string Key;
            public TargetKind Kind;
            public string Owner;
            public string Slot;
            public int W, H;
            public bool Mine;
            public List<PlacedView> Items;
            public GearItemView Gear;
        }

        sealed class Press
        {
            public GearItemView Gear;
            public Target From;
            public int Pointer;
            public Vector2 Start;
            public Vector2 At;
            /// <summary>Where it was grabbed, in cells from the top-left of the thing as it lies now.</summary>
            public Vector2 Grab;
            public bool Turned;
            public bool WasTurned;
            /// <summary>0: the whole pile; else half of it (Mayús).</summary>
            public int Count;
            public bool Lifted;
            public VisualElement Ghost;
            public VisualElement Foot;
            public Target Over;
            public GearMove Move;
            public string Why;
            /// <summary>The last place asked about, so that the engine is asked once per place.</summary>
            public string Asked;
        }
    }
}
