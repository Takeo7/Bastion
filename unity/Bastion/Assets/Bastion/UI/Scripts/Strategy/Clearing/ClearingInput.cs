using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bastion.Client;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bastion.UI.Strategy
{
    /// <summary>What answers the pointer on the clearing (clearing.ts ClearingHandlers): the rules and the orders, BaseController's.</summary>
    public interface IClearingHandlers
    {
        string Blocker(string piece, int x, int y, int rot);
        string PlaceHint(string piece, string blocked);
        void Place(string piece, int x, int y, int rot);
        string LineBlocker(string piece, IReadOnlyList<Vec2> cells, int rot);
        string LineHint(string piece, int n, string blocked);
        void PlaceLine(string piece, List<Vec2> cells, int rot);
        string FellBlocker(int trees);
        string FellHint(int trees, string blocked);
        void Fell(List<Vec2> tiles);
        /// <summary>A piece picked on the clearing (null: let go).</summary>
        void Select(string id);
        /// <summary>The tool let go (a right click while holding one).</summary>
        void Cancel();
        /// <summary>This player's piece in hand and where it would go, for the partner (null: none); on change, ten times a second at most.</summary>
        void Cursor(Settlement.PieceCursor? c);
        string PostBlocker(string soldier, int x, int y);
        string PostHint(string soldier, string blocked);
        void Post(string soldier, int x, int y);
        void PickPost(string soldier);
    }

    /// <summary>
    /// The pointer on the clearing (clearing.ts ClearingSet: its tools, the press, the drag and the keys; and `refresh`,
    /// which says what goes over the ground): a click puts the piece in hand down where it fits, a drag lays a line of
    /// walls or boxes the trees to fell, R turns the piece (Mayús: the other way), a right click lets go; with nothing in
    /// hand a click picks a piece, or one's own defender in a siege. What it shows goes to Arte's Settlement (the marks, the
    /// cursor, the ghosts, the piece picked, the posts); the words by the pointer to StrategyRoot. The mouse never moves
    /// the view here (playtest 20/10 #6): the camera is the rig's (ClearingCamera).
    /// </summary>
    public sealed class ClearingInput
    {
        public enum Kind { Look, Place, Fell, Post }

        /// <summary>Felling sends at most this many tiles at a time (protocol.ts).</summary>
        const int FellMax = 400;
        /// <summary>A line sends at most this many tiles (protocol.ts placeLine).</summary>
        const int LineMax = 60;
        /// <summary>A press that has moved this far (UI pixels) is a drag, not a click.</summary>
        const float DragFrom = 4;

        readonly IClearingHandlers handlers;
        readonly StrategyRoot root;

        Kind tool = Kind.Look;
        string piece;
        int rot;
        string soldier;
        bool building;
        bool held;
        string selected;
        Vector2Int? hover;
        Press press;
        (Vector2Int from, Vector2Int to)? fellBox;
        List<Vector2Int> line;
        readonly List<Settlement.SiegePost> posts = new List<Settlement.SiegePost>();
        string postHeld;
        /// <summary>What was last given to Settlement, as text: given again only when it changes (or the campaign does).</summary>
        string drawn;
        bool shown;
        /// <summary>What a click there picks (a piece, one's own defender): the game's yellow arrow (#5).</summary>
        bool hot;
        /// <summary>This player's hand as last sent, when, and what waits to go.</summary>
        string cursorKey = "null";
        float cursorAt = -1;
        (string key, Settlement.PieceCursor? c)? cursorWaiting;

        sealed class Press
        {
            public Vector2 At;
            public Vector2Int? Tile;
            public bool Moved;
            public int Button;
        }

        /// <summary>
        /// For tests (BaseRun): the pointer at a point of the screen (pixels of the clearing's camera, y up) and its left
        /// button, instead of the mouse; null, the mouse. It stays where it was put, as a mouse does while the camera moves.
        /// </summary>
        public static (Vector2 at, bool down)? Fake;
        bool fakeWas;

        public ClearingInput(IClearingHandlers handlers, StrategyRoot root)
        {
            this.handlers = handlers;
            this.root = root;
        }

        public Kind Tool => tool;
        public string Piece => piece;
        public int Rot => rot;
        public string Soldier => soldier;

        /// <summary>The tool in hand: a piece to place (turned as it was, if the same), the axe, a defender; or none.</summary>
        public void SetTool(Kind kind, string what = null)
        {
            if (kind == Kind.Place && (tool != Kind.Place || piece != what)) rot = 0;
            tool = kind;
            piece = kind == Kind.Place ? what : null;
            soldier = kind == Kind.Post ? what : null;
            fellBox = null;
            line = null;
            if (kind != Kind.Look) selected = null;
            drawn = null;
        }

        /// <summary>R: the piece in hand a quarter turn clockwise (Mayús: the other way).</summary>
        public void Turn(int dir)
        {
            if (tool != Kind.Place) return;
            rot = (rot + dir + 4) % 4;
            drawn = null;
        }

        /// <summary>A piece picked (by its id; null, none), told to the handlers unless it came from them.</summary>
        public void Select(string id, bool tell)
        {
            if (selected == id) return;
            selected = id;
            drawn = null;
            if (tell) handlers.Select(id);
        }

        /// <summary>Construir: the grid and the edge band, and the cursor over the empty ground; off, nothing in hand.</summary>
        public void SetBuilding(bool on)
        {
            if (building == on) return;
            building = on;
            Settlement.Instance?.SetBuilding(on);
            if (!on) SetTool(Kind.Look);
            drawn = null;
        }

        /// <summary>
        /// A fixed shot (Soldados, the Armería, the line-up), a window over the base, or the walk about the clearing: the
        /// pointer neither builds nor picks, and what is in hand is let go (clearing.ts hold).
        /// </summary>
        public void Hold(bool on)
        {
            if (held == on) return;
            held = on;
            if (on)
            {
                if (tool != Kind.Post) SetTool(Kind.Look);
                Leave();
            }
            drawn = null;
        }

        /// <summary>The siege's posts (#58), and which is in hand (its id; null, none).</summary>
        public void SetPosts(IReadOnlyList<Settlement.SiegePost> list, string inHand)
        {
            var key = PostsKey(list, inHand);
            if (key == PostsKey(posts, postHeld)) return;
            posts.Clear();
            if (list != null) posts.AddRange(list);
            postHeld = inHand;
            Settlement.Instance?.SetPosts(posts, inHand);
            drawn = null;
        }

        /// <summary>The campaign changed: what the pointer shows is worked out again (the rules may say otherwise now).</summary>
        public void Changed() => drawn = null;

        /// <summary>Nothing stays under a pointer that is gone: the press, the box, the marks and the words.</summary>
        public void Leave()
        {
            press = null;
            fellBox = null;
            line = null;
            hover = null;
            Clear();
        }

        /// <summary>Each frame, while the base is up.</summary>
        public void Tick()
        {
            var clearing = Settlement.Instance;
            var mouse = Mouse.current;
            var on = clearing != null && clearing.Base != null && (mouse != null || Fake != null) && !held && !clearing.MapOn && !clearing.Walking;
            if (!on)
            {
                if (shown) Leave();
                Flush(clearing);
                return;
            }
            var b = clearing.Base;
            Vector2 at;
            bool leftDown, leftUp, rightDown, rightUp;
            if (Fake is var fake && fake != null)
            {
                at = fake.Value.at;
                leftDown = fake.Value.down && !fakeWas;
                leftUp = !fake.Value.down && fakeWas;
                rightDown = rightUp = false;
                fakeWas = fake.Value.down;
            }
            else if (mouse != null)
            {
                at = mouse.position.ReadValue();
                (leftDown, leftUp) = (mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame);
                (rightDown, rightUp) = (mouse.rightButton.wasPressedThisFrame, mouse.rightButton.wasReleasedThisFrame);
            }
            else return;
            var over = root.Over(at);
            var tile = over ? null : clearing.TileAt(at);
            if (tile is Vector2Int t && (t.x < 0 || t.y < 0 || t.x >= b.Width || t.y >= b.Height)) tile = null;
            if (!over && press == null && (leftDown || rightDown)) press = new Press { At = at, Tile = tile, Button = leftDown ? 0 : 1 };
            if (press != null)
            {
                if (Mathf.Abs(at.x - press.At.x) + Mathf.Abs(at.y - press.At.y) > DragFrom * Mathf.Max(1, Screen.height / 900f)) press.Moved = true;
                if (press.Moved && press.Button == 0 && press.Tile is Vector2Int from && tile is Vector2Int to)
                {
                    if (tool == Kind.Fell && fellBox != (from, to))
                    {
                        fellBox = (from, to);
                        drawn = null;
                    }
                    if (tool == Kind.Place && StrategyPresenter.Lineable(piece))
                    {
                        var cells = LineCells(from, to);
                        if (line == null || !Same(line, cells))
                        {
                            line = cells;
                            drawn = null;
                        }
                    }
                }
                if (press.Button == 0 ? leftUp : rightUp) Up(clearing, tile);
            }
            var keys = Keyboard.current;
            if (keys != null && tool == Kind.Place && keys.rKey.wasPressedThisFrame) Turn(keys.shiftKey.isPressed ? -1 : 1);
            if (tile != hover)
            {
                hover = tile;
                drawn = null;
            }
            Refresh(clearing, at);
            if (hot && tile != null) App.GameCursor.Ask(App.GameCursor.Kind.Hot);
            Flush(clearing);
        }

        void Up(Settlement clearing, Vector2Int? at)
        {
            var pressed = press;
            press = null;
            if (pressed.Button == 1)
            {
                // A right click without dragging lets go of what is in hand.
                if (!pressed.Moved && tool != Kind.Look) handlers.Cancel();
                return;
            }
            var tile = at ?? pressed.Tile;
            var b = clearing.Base;
            if (tool == Kind.Fell)
            {
                var box = fellBox ?? (tile is Vector2Int one ? (one, one) : ((Vector2Int, Vector2Int)?)null);
                fellBox = null;
                drawn = null;
                if (box == null) return;
                var trees = TreesIn(b, box.Value);
                if (trees.Count > 0 && handlers.FellBlocker(trees.Count) == null) handlers.Fell(trees.ConvertAll(Vec));
                return;
            }
            if (line != null)
            {
                var cells = line;
                line = null;
                drawn = null;
                if (tool != Kind.Place) return;
                var vecs = cells.ConvertAll(Vec);
                if (handlers.LineBlocker(piece, vecs, rot) != null) return;
                if (cells.Count == 1) handlers.Place(piece, cells[0].x, cells[0].y, rot);
                else handlers.PlaceLine(piece, vecs, rot);
                return;
            }
            if (pressed.Moved || !(tile is Vector2Int t)) return;
            if (tool == Kind.Place)
            {
                var corner = PlaceAt(piece, rot, t);
                if (handlers.Blocker(piece, corner.x, corner.y, rot) == null) handlers.Place(piece, corner.x, corner.y, rot);
                return;
            }
            // Another of this player's defenders clicked: that one is taken up instead; else the one in hand goes there.
            var own = posts.FindIndex(p => p.mine && p.x == t.x && p.y == t.y);
            if (tool == Kind.Post)
            {
                if (own >= 0 && posts[own].id != soldier) handlers.PickPost(posts[own].id);
                else if (handlers.PostBlocker(soldier, t.x, t.y) == null) handlers.Post(soldier, t.x, t.y);
                return;
            }
            if (own >= 0)
            {
                handlers.PickPost(posts[own].id);
                return;
            }
            Select(Base.PieceAt(b, t.x, t.y)?.Id, true);
        }

        /// <summary>Everything over the ground, as the tool, the pointer and the pick stand now (clearing.ts refresh).</summary>
        void Refresh(Settlement clearing, Vector2 at)
        {
            if (drawn != null)
            {
                root.ClearingHintAt(at);
                return;
            }
            var b = clearing.Base;
            var marks = new List<(Vector2Int, Settlement.Mark)>();
            hot = false;
            string hint = null;
            var bad = false;
            RectInt? outline = null;
            (string piece, int rot, Vector2Int at, bool ok)? ghost = null;
            (string piece, int rot, List<Vector2Int> cells, bool ok)? row = null;
            if (line != null && tool == Kind.Place)
            {
                // A line being dragged: each tile in green or red, the whole of it all or nothing.
                var why = handlers.LineBlocker(piece, line.ConvertAll(Vec), rot);
                foreach (var c in line) marks.Add((c, Bad(b, c.x, c.y, why) ? Settlement.Mark.Bad : Settlement.Mark.Ok));
                row = (piece, rot, line, why == null);
                hint = handlers.LineHint(piece, line.Count, why);
                bad = why != null;
            }
            else if (hover is Vector2Int t && tool == Kind.Place)
            {
                var corner = PlaceAt(piece, rot, t);
                var why = handlers.Blocker(piece, corner.x, corner.y, rot);
                foreach (var c in Base.PieceCells(piece, corner.x, corner.y, rot)) marks.Add((new Vector2Int(c.X, c.Y), Bad(b, c.X, c.Y, why) ? Settlement.Mark.Bad : Settlement.Mark.Ok));
                ghost = (piece, rot, corner, why == null);
                hint = handlers.PlaceHint(piece, why);
                bad = why != null;
            }
            else if (hover is Vector2Int tp && tool == Kind.Post)
            {
                // A defender in hand (#58): the tile in green or red (amber on another of one's own), and why not.
                var own = posts.FindIndex(p => p.mine && p.x == tp.x && p.y == tp.y && p.id != soldier);
                var why = own >= 0 ? null : handlers.PostBlocker(soldier, tp.x, tp.y);
                marks.Add((tp, why != null ? Settlement.Mark.Bad : own >= 0 ? Settlement.Mark.Amber : Settlement.Mark.Ok));
                outline = new RectInt(tp.x, tp.y, 1, 1);
                hint = own >= 0 ? posts[own].name : handlers.PostHint(soldier, why);
                bad = why != null;
            }
            else if (tool == Kind.Fell && (fellBox ?? (hover is Vector2Int one ? (one, one) : ((Vector2Int, Vector2Int)?)null)) is var box && box != null)
            {
                var trees = TreesIn(b, box.Value);
                var k = Math.Min(trees.Count, FellMax);
                for (var i = 0; i < k; i++) marks.Add((trees[i], Settlement.Mark.Amber));
                var (lo, hi) = Corners(box.Value);
                outline = new RectInt(lo.x, lo.y, hi.x - lo.x + 1, hi.y - lo.y + 1);
                var why = k > 0 ? handlers.FellBlocker(k) : null;
                hint = handlers.FellHint(k, why);
                bad = k == 0 || why != null;
            }
            else if (hover is Vector2Int tl)
            {
                var p = Base.PieceAt(b, tl.x, tl.y);
                var post = posts.FindIndex(x => x.x == tl.x && x.y == tl.y);
                if (post >= 0)
                {
                    hint = posts[post].name;
                    outline = new RectInt(tl.x, tl.y, 1, 1);
                    hot = posts[post].mine;
                }
                else if (p != null)
                {
                    hot = true;
                    hint = StrategyPresenter.PieceLine(p);
                    var (w, h) = Footprint(p.Piece, p.Rot);
                    outline = new RectInt(p.X, p.Y, w, h);
                }
                else if (building) outline = new RectInt(tl.x, tl.y, 1, 1);
            }
            clearing.Marks(marks);
            clearing.Outline(outline);
            // The loose ghost and the line's share their material (as the 0.10.1's): the one let go first, then the one shown.
            if (row is var r && r != null)
            {
                clearing.Ghost(null, 0, Vector2Int.zero, true);
                clearing.GhostLine(r.Value.piece, r.Value.rot, r.Value.cells, r.Value.ok);
            }
            else
            {
                clearing.GhostLine(null, 0, null, true);
                if (ghost is var g && g != null) clearing.Ghost(g.Value.piece, g.Value.rot, g.Value.at, g.Value.ok);
                else clearing.Ghost(null, 0, Vector2Int.zero, true);
            }
            clearing.Select(selected);
            root.ClearingHint(hint, bad, at);
            shown = true;
            drawn = "1";
            // The partner sees the piece in hand where it would go; a line, from its first tile.
            if (ghost is var h2 && h2 != null) Send(new Settlement.PieceCursor { piece = h2.Value.piece, rot = h2.Value.rot, x = h2.Value.at.x, y = h2.Value.at.y });
            else if (row is var r2 && r2 != null) Send(new Settlement.PieceCursor { piece = r2.Value.piece, rot = r2.Value.rot, x = r2.Value.cells[0].x, y = r2.Value.cells[0].y });
            else Send(null);
        }

        /// <summary>Nothing over the ground but the piece picked, nothing by the pointer, nothing in hand for the partner.</summary>
        void Clear()
        {
            hot = false;
            var clearing = Settlement.Instance;
            if (clearing != null && shown)
            {
                clearing.Marks(null);
                clearing.Outline(null);
                clearing.Ghost(null, 0, Vector2Int.zero, true);
                clearing.GhostLine(null, 0, null, true);
                clearing.Select(selected);
            }
            root.ClearingHint(null, false, Vector2.zero);
            shown = false;
            drawn = null;
            Send(null);
        }

        /// <summary>This player's hand for the partner: only on change, at most every 100 ms; the last one always goes.</summary>
        void Send(Settlement.PieceCursor? c)
        {
            var key = c is Settlement.PieceCursor x ? $"{x.piece}:{x.x}:{x.y}:{x.rot}" : "null";
            cursorWaiting = key == cursorKey ? ((string, Settlement.PieceCursor?)?)null : (key, c);
        }

        void Flush(Settlement clearing)
        {
            if (cursorWaiting == null || Time.unscaledTime < cursorAt + 0.1f) return;
            var (key, c) = cursorWaiting.Value;
            cursorWaiting = null;
            cursorKey = key;
            cursorAt = Time.unscaledTime;
            handlers.Cursor(c);
        }

        /// <summary>
        /// A tile in red, of a plot that can't go down: the ones off the edge, under a tree or a piece, or all of them when
        /// the reason is not the place (the cost, the level, a vote waiting).
        /// </summary>
        static bool Bad(BaseState b, int x, int y, string why)
        {
            if (why == null) return false;
            var off = x < Base.BaseEdge || y < Base.BaseEdge || x >= b.Width - Base.BaseEdge || y >= b.Height - Base.BaseEdge;
            return off || Base.TreeAt(b, x, y) || Base.PieceAt(b, x, y) != null || !Regex.IsMatch(why, "árbol|borde|ya hay", RegexOptions.IgnoreCase);
        }

        /// <summary>The trees inside a dragged rectangle that can be felled (not the forest's past the clearing's edge: fellBlocker).</summary>
        static List<Vector2Int> TreesIn(BaseState b, (Vector2Int from, Vector2Int to) box)
        {
            var (lo, hi) = Corners(box);
            var out_ = new List<Vector2Int>();
            for (var y = lo.y; y <= hi.y; y++)
                for (var x = lo.x; x <= hi.x; x++)
                    if (Base.FellBlocker(b, x, y) == null && out_.Count < FellMax) out_.Add(new Vector2Int(x, y));
            return out_;
        }

        static (Vector2Int lo, Vector2Int hi) Corners((Vector2Int from, Vector2Int to) box) =>
            (Vector2Int.Min(box.from, box.to), Vector2Int.Max(box.from, box.to));

        /// <summary>A piece's tiles across and deep once turned.</summary>
        public static (int w, int h) Footprint(string piece, int rot)
        {
            var size = Base.Pieces[piece].Size;
            return rot % 2 == 1 ? (size[1], size[0]) : (size[0], size[1]);
        }

        /// <summary>The corner tile for `piece` turned `rot` with the pointer on `tile`: the pointer holds its middle.</summary>
        public static Vector2Int PlaceAt(string piece, int rot, Vector2Int tile)
        {
            var (w, h) = Footprint(piece, rot);
            return new Vector2Int(tile.x - (w - 1) / 2, tile.y - (h - 1) / 2);
        }

        /// <summary>The tiles of a straight line from `a` towards `b`, along whichever way it went further.</summary>
        static List<Vector2Int> LineCells(Vector2Int a, Vector2Int b)
        {
            var dx = b.x - a.x;
            var dy = b.y - a.y;
            var along = Math.Abs(dx) >= Math.Abs(dy);
            var n = Math.Min(LineMax, (along ? Math.Abs(dx) : Math.Abs(dy)) + 1);
            var step = Math.Sign(along ? dx : dy);
            var out_ = new List<Vector2Int>(n);
            for (var i = 0; i < n; i++) out_.Add(along ? new Vector2Int(a.x + i * step, a.y) : new Vector2Int(a.x, a.y + i * step));
            return out_;
        }

        static bool Same(List<Vector2Int> a, List<Vector2Int> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static Vec2 Vec(Vector2Int v) => new Vec2 { X = v.x, Y = v.y };

        static string PostsKey(IReadOnlyList<Settlement.SiegePost> list, string inHand)
        {
            var parts = new List<string> { inHand ?? "" };
            if (list != null) foreach (var p in list) parts.Add($"{p.id}:{p.name}:{p.colour}:{p.x}:{p.y}:{p.mine}");
            return string.Join("|", parts);
        }
    }
}
