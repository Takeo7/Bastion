#nullable enable
// mapgen.ts in C# (Mapgen/*.cs): a building's inside, from partition to furnishBarn. Its walls and doors between rooms
// (partitions, splitRooms, the plans of offices, clinics, shops and halls), the doors and windows of its outer walls,
// the rooms held out in, a house's rooms by use, and the furniture of each room.
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;
// The type HomeUses (types.ts HomeUse): inside Mapgen the name HomeUses is homeUses(), the method.
using HomeUse = Bastion.Engine.HomeUses;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>
        /// A wall across a building with a door (or, `width` 2, an opening) somewhere
        /// along it; the tiles on both sides of the opening stay clear.
        /// </summary>
        private static void Partition(Canvas c, List<Vec2> line, Vec2 normal, Rng rng, int width = 1)
        {
            var at = rng.Int(line.Count - width + 1);
            for (var k = 0; k < line.Count; k++)
            {
                var p = line[k];
                var opening = k >= at && k < at + width;
                c.Force(p.X, p.Y, opening ? (width > 1 ? TileKinds.Floor : TileKinds.Door) : TileKinds.Wall);
                if (opening)
                {
                    c.Reserve(p.X, p.Y);
                    c.Reserve(p.X + normal.X, p.Y + normal.Y);
                    c.Reserve(p.X - normal.X, p.Y - normal.Y);
                }
            }
        }

        /// <summary>
        /// Splits an interior into rooms of at most `maxSide` tiles a side (never under
        /// 3), a wall with a door between each two. A new wall never ends against an
        /// existing door, and with `kept` never crosses a tile kept clear (upstairs,
        /// the stairwell: placeStairs).
        /// </summary>
        private static void SplitRooms(Canvas c, Rect r, Rng rng, List<Rect> out_, int maxSide, bool open, bool kept = false)
        {
            var vertical = r.W > r.H || (r.W == r.H && rng.Int(2) == 0);
            var span = vertical ? r.W : r.H;
            if (span <= maxSide || span < 7)
            {
                out_.Add(r);
                return;
            }
            var options = new List<int>();
            for (var o = 3; o <= span - 4; o++)
            {
                var ends = vertical ? new[] { V(r.X + o, r.Y - 1), V(r.X + o, r.Y + r.H) } : new[] { V(r.X - 1, r.Y + o), V(r.X + r.W, r.Y + o) };
                // c.keep past its end reads undefined (false) in TypeScript.
                if (kept && TilesIn(vertical ? Rect(r.X + o, r.Y, 1, r.H) : Rect(r.X, r.Y + o, r.W, 1)).Any((p) => Js.At(c.Keep, c.I(p.X, p.Y), false))) continue;
                if (ends.All((e) => c.Get(e.X, e.Y) == TileKinds.Wall)) options.Add(o);
            }
            var pick = BestOf(options, (o) => Math.Abs(o - (span - 1) / 2.0) + rng.Next() * 1.5);
            if (pick == null)
            {
                out_.Add(r);
                return;
            }
            var k = pick.Value;
            var line = vertical ? TilesIn(Rect(r.X + k, r.Y, 1, r.H)) : TilesIn(Rect(r.X, r.Y + k, r.W, 1));
            Partition(c, line, vertical ? Grid.Dirs4[1] : Grid.Dirs4[2], rng, open ? 2 : 1);
            SplitRooms(c, vertical ? Rect(r.X, r.Y, k, r.H) : Rect(r.X, r.Y, r.W, k), rng, out_, maxSide, open, kept);
            SplitRooms(c, vertical ? Rect(r.X + k + 1, r.Y, r.W - k - 1, r.H) : Rect(r.X, r.Y + k + 1, r.W, r.H - k - 1), rng, out_, maxSide, open, kept);
        }

        /// <summary>Splits `length` tiles into rooms of 3–6 with a 1-tile wall between each two, as evenly as possible.</summary>
        private static List<int> Segments(int length, Rng rng)
        {
            var n = Math.Max(1, (int)Math.Ceiling((length + 1) / 7.0));
            var total = length - (n - 1);
            var sizes = Enumerable.Repeat((int)Math.Floor((double)total / n), n).ToList();
            // slice(0, total - sizes[0] * n): the remainder, 0 to n - 1 (total is never negative here).
            foreach (var k in Shuffle(Enumerable.Range(0, n).ToList(), rng).Take(total - sizes[0] * n)) sizes[k]++;
            return sizes;
        }

        /// <summary>Outside the wall tile `p` (out of side `s`) there is open ground to step on.</summary>
        private static bool OpensOnto(Canvas c, Vec2 p, int s)
        {
            var q = Add(p, Grid.Dirs4[s]);
            return c.In(q.X, q.Y) && c.ElevAt(q.X, q.Y) == 0 && (c.Get(q.X, q.Y) == TileKinds.Floor || c.Get(q.X, q.Y) == TileKinds.Ladder) && c.GroundAt(q.X, q.Y) != Indoor;
        }

        /// <summary>Turns wall tile `p` (on side `s`) into `kind` and keeps both of its sides clear.</summary>
        private static void Opening(Canvas c, Vec2 p, int s, string kind)
        {
            var d = Grid.Dirs4[s];
            c.Force(p.X, p.Y, kind);
            c.Reserve(p.X, p.Y);
            c.Reserve(p.X + d.X, p.Y + d.Y);
            c.Reserve(p.X - d.X, p.Y - d.Y);
        }

        /// <summary>
        /// Offices and labs: a corridor two tiles wide runs from the front wall to the
        /// back one, with rooms off it on one or both sides. Every room has a door onto
        /// the corridor, and the corridor a double door onto the street.
        /// </summary>
        private static bool CorridorPlan(Canvas c, Rect b, int front, Rng rng, List<Rect> rooms, List<int?> doors)
        {
            var inner = Inset(b, 1);
            var across = SideLen(inner, front);
            var length = DepthOf(inner, front);
            if (across < 6 || length < 7) return false;
            var strips = new List<(int, int)>();
            var walls = new List<int>();
            int cu;
            if (across >= 10)
            {
                var d1 = (int)Math.Floor((across - 4) / 2.0) + (across % 2 == 1 ? rng.Int(2) : 0);
                strips.Add((0, d1));
                strips.Add((d1 + 4, across - d1 - 4));
                walls.Add(d1);
                walls.Add(d1 + 3);
                cu = d1 + 1;
            }
            else
            {
                var d = across - 3;
                var left = rng.Int(2) == 0;
                strips.Add(left ? (0, d) : (3, d));
                walls.Add(left ? d : 2);
                cu = left ? d + 1 : 0;
            }
            Vec2 At(int u, int v) => LocalPt(inner, front, u, v);
            foreach (var u in walls)
            {
                for (var v = 0; v < length; v++)
                {
                    var p = At(u, v);
                    c.Force(p.X, p.Y, TileKinds.Wall);
                }
            }
            for (var v = 0; v < length; v++)
            {
                foreach (var u in new[] { cu, cu + 1 })
                {
                    var p = At(u, v);
                    c.Reserve(p.X, p.Y);
                }
            }
            foreach (var (su, sw) in strips)
            {
                var toCorridor = su < cu ? 1 : -1;
                var wallU = su < cu ? su + sw : su - 1;
                var v0 = 0;
                var sizes = Segments(length, rng);
                for (var k = 0; k < sizes.Count; k++)
                {
                    var n = sizes[k];
                    if (k > 0)
                    {
                        for (var u = su; u < su + sw; u++)
                        {
                            var p = At(u, v0 - 1);
                            c.Force(p.X, p.Y, TileKinds.Wall);
                        }
                    }
                    rooms.Add(Local(inner, front, su, v0, sw, n));
                    var dv = v0 + (int)Math.Floor((n - 1) / 2.0);
                    var door = At(wallU, dv);
                    c.Force(door.X, door.Y, TileKinds.Door);
                    foreach (var p in new[] { door, At(wallU - toCorridor, dv), At(wallU + toCorridor, dv) }) c.Reserve(p.X, p.Y);
                    v0 += n + 1;
                }
            }
            // Double door at the front end of the corridor, a single one at the back.
            var ends = new[] { LocalPt(b, front, cu + 1, 0), LocalPt(b, front, cu + 2, 0) };
            if (ends.All((p) => OpensOnto(c, p, front)))
            {
                foreach (var p in ends) Opening(c, p, front, TileKinds.Door);
                doors[front] = cu + 1;
            }
            var back = Opposite(front);
            var rear = LocalPt(b, back, cu + 1 + rng.Int(2), 0);
            if (rng.Next() < 0.7 && OpensOnto(c, rear, back))
            {
                Opening(c, rear, back, TileKinds.Door);
                doors[back] = cu + 1;
            }
            return true;
        }

        /// <summary>Clinical rooms have a purpose, doors and a shared circulation space.</summary>
        private static List<MedicalRoom> MedicalPlan(Canvas c, Rect b, int front, Rng rng, int z, bool hospital, bool ward, List<int?> doors)
        {
            var inner = Inset(b, 1);
            var across = SideLen(inner, front);
            var length = DepthOf(inner, front);
            var rooms = new List<MedicalRoom>();
            Vec2 At(int u, int v) => LocalPt(inner, front, u, v);
            void Room(int u, int v, int w, int h, string use)
            {
                var q = Local(inner, front, u, v, w, h);
                rooms.Add(new MedicalRoom { X = q.X, Y = q.Y, W = q.W, H = q.H, Z = z, Use = use });
            }
            void Wall(int u, int v)
            {
                var p = At(u, v);
                c.Force(p.X, p.Y, TileKinds.Wall);
            }
            void Door(int u, int v, int du, int dv, bool wide = false)
            {
                var p = At(u, v);
                c.Force(p.X, p.Y, wide ? TileKinds.Floor : TileKinds.Door);
                foreach (var q in new[] { p, At(u + du, v + dv), At(u - du, v - dv) }) c.Reserve(q.X, q.Y);
            }
            if (!hospital && !ward)
            {
                // A compact waiting/reception area, with two closed clinical rooms behind.
                var lobby = length >= 7 ? 3 : 2;
                Room(0, 0, across, lobby, MedicalUses.Reception);
                for (var u = 0; u < across; u++) Wall(u, lobby);
                var left = (int)Math.Floor((across - 1) / 2.0);
                for (var v = lobby + 1; v < length; v++) Wall(left, v);
                Room(0, lobby + 1, left, length - lobby - 1, ward ? MedicalUses.Ward : MedicalUses.Consultation);
                Room(left + 1, lobby + 1, across - left - 1, length - lobby - 1, ward ? MedicalUses.Treatment : MedicalUses.Equipment);
                Door((int)Math.Floor(left / 2.0), lobby, 0, 1);
                Door(left + 1 + (int)Math.Floor((across - left - 1) / 2.0), lobby, 0, 1);
            }
            else
            {
                // Hospital rooms are three tiles deep from the corridor, with a door for each.
                // Small infirmaries have a narrower corridor and combine reception with waiting.
                var cw = hospital ? 2 : 1;
                var twoWings = across >= 7;
                var left = twoWings ? (int)Math.Floor((across - cw - 2) / 2.0) : across - cw - 1;
                var cu = left + 1;
                var strips = new List<(int, int, int)> { (0, left, left) };
                if (twoWings) strips.Add((cu + cw + 1, across - cu - cw - 1, cu + cw));
                for (var v = 0; v < length; v++)
                {
                    Wall(left, v);
                    if (twoWings) Wall(cu + cw, v);
                    for (var u = cu; u < cu + cw; u++)
                    {
                        var p = At(u, v);
                        c.Reserve(p.X, p.Y);
                    }
                }
                // Aligned partitions on every floor make real stairwells and short rooms.
                var count = Math.Max(2, (int)Math.Floor((length + 1) / (double)(hospital ? 4 : 3)));
                var total = length - count + 1;
                var sizes = Enumerable.Range(0, count).Select((k) => (int)Math.Floor((double)total / count) + (k < total % count ? 1 : 0)).ToList();
                // In an infirmary, two rooms per wing are enough for waiting/triage,
                // treatment, observation and medical stores; avoid cramped one-tile rooms.
                if (!hospital && sizes.Any((n) => n < 3) && length >= 7)
                {
                    sizes.Clear();
                    sizes.Add((int)Math.Ceiling((length - 1) / 2.0));
                    sizes.Add((int)Math.Floor((length - 1) / 2.0));
                }
                // A hospital's stairwell, each wing's last room, holds a flight with its foot and its landing (docs/MAPAS.md §4.4):
                // of three, keeping four rooms a wing three deep or more; else of two («2 donde no cabe»); else a room fewer.
                if (hospital)
                {
                    bool Fits(int n, int well) => length - n + 1 - well >= 3 * (n - 1);
                    var plans = new[] { Flight, ShortFlight }.SelectMany((run) => Enumerable.Range(0, count - Math.Min(count, 4) + 1).Select((j) => (N: count - j, Well: run + 2))).ToList();
                    var found = plans.FindIndex((q) => Fits(q.N, q.Well));
                    // `plan`, the one found (or { n: count, well: FLIGHT + 2 }): its n goes down below.
                    var planN = found >= 0 ? plans[found].N : count;
                    var planWell = found >= 0 ? plans[found].Well : Flight + 2;
                    while (planN > 2 && !Fits(planN, planWell)) planN--;
                    var rest = length - planN + 1 - planWell;
                    var head = Enumerable.Range(0, planN - 1).Select((k) => (int)Math.Floor((double)rest / (planN - 1)) + (k < rest % (planN - 1) ? 1 : 0)).ToList();
                    sizes.Clear();
                    sizes.AddRange(head);
                    sizes.Add(planWell);
                }
                var uses = !hospital
                    ? new[] { new[] { MedicalUses.Waiting, MedicalUses.Ward, MedicalUses.Hygiene }, new[] { MedicalUses.Treatment, MedicalUses.Supplies, MedicalUses.Ward } }
                    : z == 0
                        ? new[] { new[] { MedicalUses.Reception, MedicalUses.Consultation, MedicalUses.Equipment, MedicalUses.Treatment }, new[] { MedicalUses.Waiting, MedicalUses.Consultation, MedicalUses.Hygiene, MedicalUses.Supplies } }
                        : z == 1
                            ? new[] { new[] { MedicalUses.Ward, MedicalUses.Nurses, MedicalUses.Ward, MedicalUses.Ward }, new[] { MedicalUses.Ward, MedicalUses.Treatment, MedicalUses.Hygiene, MedicalUses.Supplies } }
                            : new[] { new[] { MedicalUses.Ward, MedicalUses.Nurses, MedicalUses.Ward, MedicalUses.Supplies }, new[] { MedicalUses.Ward, MedicalUses.Equipment, MedicalUses.Treatment, MedicalUses.Ward } };
                for (var side = 0; side < strips.Count; side++)
                {
                    var (u, width, wu) = strips[side];
                    var v = 0;
                    for (var k = 0; k < sizes.Count; k++)
                    {
                        var depth = sizes[k];
                        if (k != 0) for (var x = u; x < u + width; x++) Wall(x, v - 1);
                        var use = hospital && k == sizes.Count - 1 ? MedicalUses.Stairs : uses[side][k % uses[side].Length];
                        Room(u, v, width, depth, use);
                        var dv = v + (int)Math.Floor((depth - 1) / 2.0);
                        Door(wu, dv, 1, 0);
                        // Reception and waiting open onto the corridor, rather than a private bedroom entrance.
                        if ((use == MedicalUses.Waiting || use == MedicalUses.Reception) && depth >= 3) Door(wu, dv + 1, 1, 0, true);
                        v += depth + 1;
                    }
                }
                if (z == 0)
                {
                    for (var u = cu; u < cu + cw; u++)
                    {
                        var p = LocalPt(b, front, u + 1, 0);
                        if (OpensOnto(c, p, front))
                        {
                            Opening(c, p, front, TileKinds.Door);
                            doors[front] = u + 1;
                        }
                    }
                }
            }
            if (z == 0 && !hospital && !ward)
            {
                // Put the entrance directly into reception; never into a consultation room.
                var u = (int)Math.Floor(across / 2.0) + 1;
                var p = LocalPt(b, front, u, 0);
                if (OpensOnto(c, p, front))
                {
                    Opening(c, p, front, TileKinds.Door);
                    doors[front] = u;
                }
            }
            return rooms;
        }

        /// <summary>
        /// Whether a clinical room's floor is all one, and every piece of its furniture stands beside it (an object over
        /// several tiles, by any of them): one walks up to each.
        /// </summary>
        private static bool RoomJoined(Canvas c, Rect room)
        {
            var cells = TilesIn(room);
            var floor = cells.Where((p) => c.Get(p.X, p.Y) == TileKinds.Floor).ToList();
            var seen = new HashSet<int>();
            var queue = floor.Count > 0 ? new List<Vec2> { floor[0] } : new List<Vec2>();
            while (queue.Count > 0)
            {
                var p = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                var i = c.I(p.X, p.Y);
                if (seen.Contains(i) || !InRect(room, p) || c.Get(p.X, p.Y) != TileKinds.Floor) continue;
                seen.Add(i);
                foreach (var d in Grid.Dirs4) queue.Add(Add(p, d));
            }
            if (floor.Count == 0 || !floor.All((p) => seen.Contains(c.I(p.X, p.Y)))) return false;
            // A Map in TypeScript, only ever asked whether all its values are true: its order doesn't count.
            var beside = new Dictionary<int, bool>();
            foreach (var p in cells)
            {
                var i = c.I(p.X, p.Y);
                if (c.Decor[i] == 0) continue;
                var a = Decor.ObjectAt(c.Decor, c.Width, i)?.Anchor ?? i;
                beside[a] = (beside.TryGetValue(a, out var was) && was) || Grid.Dirs4.Any((d) => seen.Contains(c.I(p.X + d.X, p.Y + d.Y)));
            }
            return beside.Values.All((v) => v);
        }

        /// <summary>Takes an object just put on `cells` off again (bare floor), and out of c.objects.</summary>
        private static void TakeObject(Canvas c, IReadOnlyList<Vec2> cells)
        {
            var at = cells.Select((p) => c.I(p.X, p.Y)).ToList();
            foreach (var i in at)
            {
                c.Tiles[i] = TileKinds.Floor;
                c.Decor[i] = 0;
            }
            var k = c.Objects.FindIndex((o) => o.Count == at.Count && o.All((t) => at.Contains(t.I)));
            if (k >= 0) c.Objects.RemoveAt(k);
        }

        /// <summary>How deep the clinics' furniture over several tiles is, along the way it faces (decor.ts FOOTPRINT): a bed's head to the wall.</summary>
        private static readonly IReadOnlyDictionary<string, int> ClinicalDepth = new Dictionary<string, int>
        {
            [DecorKinds.HospitalBed] = Decor.Footprint.HospitalBed[1],
            [DecorKinds.Gurney] = Decor.Footprint.Gurney[1],
        };

        /// <summary>
        /// Clinical furniture on `p` of `room`, facing into it (inward), if the room stays joined (roomJoined); a hospital
        /// bed or a gurney two tiles deep from the wall (in a corner, from either of its walls). Its tiles, or null and
        /// nothing put.
        /// </summary>
        private static List<Vec2>? PlaceClinical(Canvas c, Rect room, string kind, Vec2 p, bool high)
        {
            var deep = ClinicalDepth.TryGetValue(kind, out var depth) ? depth : 1;
            var first = Inward(room, p);
            var facings = new List<int> { first };
            if (deep > 1) facings.AddRange(new[] { 0, 1, 2, 3 }.Where((s) => !InRect(room, Add(p, Grid.Dirs4[s]))).Select(Opposite).Where((f) => f != first));
            foreach (var f in facings)
            {
                var cells = Footprint(p, f, f, 1, deep);
                if (deep > 1 ? !PutFurniture(c, room, cells, high ? TileKinds.High : TileKinds.Low, kind, f) : !Put(c, p, high ? TileKinds.High : TileKinds.Low, new Look(kind, f))) continue;
                if (RoomJoined(c, room)) return cells;
                TakeObject(c, cells);
            }
            return null;
        }

        /// <summary>Furniture expresses each room's use and leaves its doorway and central aisle clear.</summary>
        private static void FurnishMedical(Canvas c, MedicalRoom room, Rng rng, string? care = null)
        {
            if (!string.IsNullOrEmpty(care))
            {
                FurnishCareRoom(c, room, rng, care!);
                return;
            }
            // The room as a Rect (in TypeScript a MedicalRoom is one).
            var r = Rect(room.X, room.Y, room.W, room.H);
            var long_ = Math.Max(room.W, room.H);
            var short_ = Math.Min(room.W, room.H);
            var alongX = room.W >= room.H;
            Vec2 P(int a, int b) => alongX ? V(room.X + a, room.Y + b) : V(room.X + b, room.Y + a);
            bool Place(string kind, IReadOnlyList<Vec2> points, bool high = false) => points.Any((p) => PlaceClinical(c, r, kind, p, high) != null);
            var edge = Shuffle(new List<Vec2> { P(0, 0), P(long_ - 1, 0), P(0, short_ - 1), P(long_ - 1, short_ - 1) }, rng);
            var near = new List<Vec2> { P((int)Math.Floor(long_ / 2.0), 0), P((int)Math.Floor(long_ / 2.0), short_ - 1) };
            near.AddRange(edge);
            switch (room.Use)
            {
                case MedicalUses.Reception:
                    Place(DecorKinds.ReceptionDesk, edge);
                    for (var a = 1; a < long_ - 1; a += 2) Place(DecorKinds.WaitingSeats, new[] { P(a, short_ - 1), P(a, 0) });
                    Place(DecorKinds.Wheelchair, Enumerable.Reverse(edge).ToList());
                    break;
                case MedicalUses.Consultation:
                    Place(DecorKinds.Desk, edge);
                    Place(DecorKinds.ExamTable, Enumerable.Reverse(edge).ToList());
                    Place(DecorKinds.MedicalCabinet, near, true);
                    Place(DecorKinds.MedicalCart, edge);
                    break;
                case MedicalUses.Treatment:
                    Place(DecorKinds.ExamTable, edge);
                    Place(DecorKinds.MedicalCart, near);
                    Place(DecorKinds.MedicalCabinet, Enumerable.Reverse(edge).ToList(), true);
                    Place(DecorKinds.MedicalMonitor, edge);
                    break;
                case MedicalUses.Equipment:
                    Place(DecorKinds.ExamTable, edge);
                    Place(DecorKinds.MedicalShelf, Enumerable.Reverse(edge).ToList(), true);
                    Place(DecorKinds.Xray, near, true);
                    Place(DecorKinds.MedicalMonitor, edge);
                    break;
                case MedicalUses.Ward:
                    for (var a = 0; a < long_; a += 2)
                    {
                        Place(DecorKinds.HospitalBed, new[] { P(a, 0) });
                        if (short_ >= 5) Place(DecorKinds.HospitalBed, new[] { P(a, short_ - 1) });
                    }
                    Place(DecorKinds.MedicalCart, near);
                    Place(DecorKinds.MedicalMonitor, Enumerable.Reverse(edge).ToList());
                    Place(DecorKinds.MedicalShelf, edge, true);
                    break;
                case MedicalUses.Nurses:
                    Place(DecorKinds.ReceptionDesk, edge);
                    Place(DecorKinds.MedicalShelf, Enumerable.Reverse(edge).ToList(), true);
                    Place(DecorKinds.Gurney, near);
                    Place(DecorKinds.MedicalCabinet, edge, true);
                    break;
            }
        }

        /// <summary>Compact rooms: equipment is placed by function, with connected floor and a free approach to every object.</summary>
        private static void FurnishCareRoom(Canvas c, MedicalRoom room, Rng rng, string care)
        {
            if (room.Use == MedicalUses.Stairs) return;
            // The room as a Rect (in TypeScript a MedicalRoom is one).
            var r = Rect(room.X, room.Y, room.W, room.H);
            var long_ = Math.Max(room.W, room.H);
            var short_ = Math.Min(room.W, room.H);
            var alongX = room.W >= room.H;
            Vec2 P(int a, int b) => alongX ? V(room.X + a, room.Y + b) : V(room.X + b, room.Y + a);
            var cells = TilesIn(r);
            var corners = Shuffle(new List<Vec2> { P(0, 0), P(long_ - 1, 0), P(0, short_ - 1), P(long_ - 1, short_ - 1) }, rng);
            var boundary = Shuffle(cells.Where((p) => p.X == room.X || p.Y == room.Y || p.X == room.X + room.W - 1 || p.Y == room.Y + room.H - 1).ToList(), rng);
            var positions = new List<Vec2>(corners);
            positions.AddRange(boundary);
            var high = new HashSet<string> { DecorKinds.MedicalCabinet, DecorKinds.MedicalShelf, DecorKinds.WaterDispenser, DecorKinds.Xray };
            // `points` defaults to positions.
            List<Vec2>? Fit(string kind, IReadOnlyList<Vec2>? points = null)
            {
                foreach (var p in points ?? positions)
                {
                    var placed = PlaceClinical(c, r, kind, p, high.Contains(kind));
                    if (placed != null) return placed;
                }
                return null;
            }
            bool Place(string kind, IReadOnlyList<Vec2>? points = null) => Fit(kind, points) != null;
            switch (room.Use)
            {
                case MedicalUses.Ward:
                {
                    // A bed of 1 × 2 in a room up to 3 × 4, two from 4 × 5 (docs/MAPAS.md §4.3). Keep beds and bedside tables
                    // together: by its head, else by its foot.
                    var beds = long_ >= 5 && short_ >= 4 ? 2 : 1;
                    for (var k = 0; k < beds; k++)
                    {
                        var points = k != 0 ? new List<Vec2> { P(long_ - 1, 0), P(long_ - 1, short_ - 1) } : new List<Vec2> { P(0, 0) };
                        points.AddRange(corners);
                        points.AddRange(boundary);
                        var bed = Fit(DecorKinds.HospitalBed, points);
                        if (bed != null) Place(DecorKinds.BedsideTable, bed.SelectMany((q) => Grid.Dirs4.Select((d) => Add(q, d))).Where((q) => InRect(r, q)).ToList());
                    }
                    Place(DecorKinds.MedicalMonitor);
                    Place(DecorKinds.MedicalCabinet);
                    Place(DecorKinds.VisitorChair);
                    Place(DecorKinds.ClinicalSink);
                    Place(DecorKinds.ClinicalBin);
                    break;
                }
                case MedicalUses.Consultation:
                    Place(DecorKinds.ConsultDesk); Place(DecorKinds.ExamTable, Enumerable.Reverse(corners).ToList()); Place(DecorKinds.ClinicalSink);
                    Place(DecorKinds.MedicalCabinet); Place(DecorKinds.VisitorChair); Place(DecorKinds.MedicalCart); Place(DecorKinds.ClinicalBin);
                    break;
                case MedicalUses.Treatment:
                    Place(rng.Int(2) != 0 ? DecorKinds.ExamTable : DecorKinds.Gurney); Place(DecorKinds.Sterilizer); Place(DecorKinds.ClinicalSink); Place(DecorKinds.MedicalCart);
                    Place(DecorKinds.MedicalCabinet); Place(DecorKinds.MedicalMonitor); Place(DecorKinds.ClinicalBin);
                    break;
                case MedicalUses.Equipment:
                    Place(rng.Int(2) != 0 ? DecorKinds.Xray : DecorKinds.Ultrasound); Place(DecorKinds.ExamTable); Place(DecorKinds.MedicalMonitor);
                    Place(DecorKinds.Sterilizer); Place(DecorKinds.MedicalCabinet); Place(DecorKinds.ClinicalSink); Place(DecorKinds.ClinicalBin);
                    break;
                case MedicalUses.Reception:
                    Place(DecorKinds.ReceptionDesk); Place(DecorKinds.WaitingSeats); Place(DecorKinds.MedicalCabinet);
                    Place(DecorKinds.WaitingTable); Place(DecorKinds.WaterDispenser); Place(DecorKinds.ClinicalBin);
                    break;
                case MedicalUses.Waiting:
                    if (care == MedicalSiteKinds.Ward) Place(DecorKinds.ReceptionDesk);
                    Place(DecorKinds.WaitingSeats); Place(DecorKinds.WaitingSeats, Enumerable.Reverse(corners).ToList()); Place(DecorKinds.WaitingTable);
                    Place(DecorKinds.WaterDispenser); Place(DecorKinds.Wheelchair); Place(DecorKinds.ClinicalBin);
                    break;
                case MedicalUses.Nurses:
                    Place(DecorKinds.ReceptionDesk); Place(DecorKinds.MedicalCabinet); Place(DecorKinds.MedicalCart);
                    Place(DecorKinds.MedicalMonitor); Place(DecorKinds.Sterilizer); Place(DecorKinds.ClinicalBin);
                    break;
                case MedicalUses.Supplies:
                    Place(DecorKinds.MedicalShelf); Place(DecorKinds.MedicalCabinet); Place(DecorKinds.MedicalCabinet);
                    Place(DecorKinds.MedicalCart); Place(DecorKinds.Sterilizer); Place(DecorKinds.ClinicalBin);
                    break;
                case MedicalUses.Hygiene:
                    Place(DecorKinds.Toilet); Place(DecorKinds.ClinicalSink); Place(DecorKinds.ClinicalBin); Place(DecorKinds.MedicalCabinet);
                    break;
            }
        }

        /// <summary>Shops: the shop floor on the street, with a stockroom behind it when deep enough.</summary>
        private static void ShopPlan(Canvas c, Rect b, int front, Rng rng, List<Rect> rooms)
        {
            var inner = Inset(b, 1);
            var across = SideLen(inner, front);
            var depth = DepthOf(inner, front);
            if (depth < 7)
            {
                rooms.Add(inner);
                return;
            }
            // Array.from({ length }) takes a negative length as 0.
            var line = Enumerable.Range(0, Math.Max(0, across)).Select((u) => LocalPt(inner, front, u, depth - 4)).ToList();
            Partition(c, line, Grid.Dirs4[front], rng);
            rooms.Add(Local(inner, front, 0, 0, across, depth - 4));
            rooms.Add(Local(inner, front, 0, depth - 3, across, 3));
        }

        /// <summary>Warehouses, barns and hangars: one open hall, sometimes with a small office in a back corner.</summary>
        private static Rect? HallPlan(Canvas c, Rect b, int front, Rng rng, List<Rect> rooms)
        {
            var inner = Inset(b, 1);
            var across = SideLen(inner, front);
            var depth = DepthOf(inner, front);
            rooms.Add(inner);
            if (across < 10 || depth < 10 || rng.Next() < 0.4) return null;
            var right = rng.Int(2) == 0;
            var u0 = right ? across - 4 : 0;
            var wallU = right ? across - 5 : 4;
            for (var v = depth - 4; v < depth; v++)
            {
                var p = LocalPt(inner, front, wallU, v);
                c.Force(p.X, p.Y, TileKinds.Wall);
            }
            var line = Enumerable.Range(0, 5).Select((k) => LocalPt(inner, front, Math.Min(u0, wallU) + k, depth - 5)).ToList();
            foreach (var p in line) c.Force(p.X, p.Y, TileKinds.Wall);
            // The door is in the office's front wall, away from the corner post.
            var door = LocalPt(inner, front, u0 + 1 + rng.Int(2), depth - 5);
            Opening(c, door, front, TileKinds.Door);
            var office = Local(inner, front, u0, depth - 4, 4, 4);
            rooms.Add(office);
            return office;
        }

        /// <summary>
        /// Opens `width` adjacent tiles of side `s` (doors, or a gap for a loading bay),
        /// as close to the middle as the rooms allow. Returns the index along the side.
        /// </summary>
        private static int? Entrance(Canvas c, Rect b, int s, Rng rng, int width = 1, string kind = TileKinds.Door)
        {
            var n = SideLen(b, s);
            var back = Grid.Dirs4[Opposite(s)];
            var options = new List<int>();
            for (var o = 1; o + width <= n - 1; o++)
            {
                var ok = true;
                for (var k = 0; k < width && ok; k++)
                {
                    var p = LocalPt(b, s, o + k, 0);
                    ok = c.Get(p.X, p.Y) == TileKinds.Wall && c.Get(p.X + back.X, p.Y + back.Y) == TileKinds.Floor && OpensOnto(c, p, s);
                }
                if (ok) options.Add(o);
            }
            var pick = BestOf(options, (o) => Math.Abs(o + (width - 1) / 2.0 - (n - 1) / 2.0) + rng.Next() * 1.2);
            if (pick == null) return null;
            var u = pick.Value;
            for (var k = 0; k < width; k++) Opening(c, LocalPt(b, s, u + k, 0), s, kind);
            return u + (int)Math.Floor((width - 1) / 2.0);
        }

        /// <summary>
        /// Windows every `every` tiles along each outer wall, counted from the door of
        /// that side (or its middle), never at a corner, beside an opening or where a
        /// partition meets the wall. The floor just inside each stays clear of the
        /// furniture (playtest 21/10, #20: a shelf or a wardrobe that happens to stand
        /// there reads as a window blocked by chance); barricadeRooms blocks a room's
        /// windows on purpose.
        /// </summary>
        private static void Windows(Canvas c, Rect b, int every, List<int?> doors)
        {
            for (var s = 0; s < 4; s++)
            {
                var n = SideLen(b, s);
                var anchor = doors[s] ?? (int)Math.Floor((n - 1) / 2.0);
                var back = Grid.Dirs4[Opposite(s)];
                var lateral = Grid.Dirs4[(s + 1) % 4];
                for (var u = 2; u <= n - 3; u++)
                {
                    var off = Math.Abs(u - anchor);
                    if (off % every != 0 || (off == 0 && doors[s] != null)) continue;
                    var p = LocalPt(b, s, u, 0);
                    var sides = new[] { Add(p, lateral), V(p.X - lateral.X, p.Y - lateral.Y) };
                    if (c.Get(p.X, p.Y) != TileKinds.Wall || c.Get(p.X + back.X, p.Y + back.Y) != TileKinds.Floor) continue;
                    if (!sides.All((q) => c.Get(q.X, q.Y) == TileKinds.Wall || c.Get(q.X, q.Y) == TileKinds.Window)) continue;
                    var q = Add(p, Grid.Dirs4[s]);
                    if (!c.In(q.X, q.Y) || c.Get(q.X, q.Y) == TileKinds.Wall) continue;
                    c.Force(p.X, p.Y, TileKinds.Window);
                    c.Reserve(p.X + back.X, p.Y + back.Y);
                }
            }
        }

        /// <summary>One room in BARRICADED has its windows barricaded from inside (barricadeRooms).</summary>
        private const int Barricaded = 4;

        /// <summary>
        /// Rooms of a house, a shop or an office whose windows were barricaded from
        /// inside by whoever held out there (playtest 21/10, #20, Ana: «si una ventana
        /// de una habitación está bloqueada, el resto también, aunque siempre se puede
        /// entrar por la puerta»): one in BARRICADED, by position (mix: no rng drawn),
        /// every window of the room at once, furniture piled against each
        /// ('windowBarricade', a `high` tile, facing the window). Not a room of fewer
        /// than 3 a side, nor one where a window's inside tile is beside a door, a
        /// ladder or the stairs: then none of its windows.
        /// </summary>
        private static void BarricadeRooms(Canvas c, Rect b, List<Rect> rooms)
        {
            bool Outer(Vec2 q) => q.X == b.X || q.Y == b.Y || q.X == b.X + b.W - 1 || q.Y == b.Y + b.H - 1;
            foreach (var room in rooms)
            {
                if (Math.Min(room.W, room.H) < 3 || Mix(room.X * 3 + 1, room.Y * 5 + 2) % Barricaded != 0) continue;
                var piles = new List<(Vec2 P, int Facing)>();
                var ok = true;
                foreach (var p in TilesIn(room))
                {
                    for (var facing = 0; facing < Grid.Dirs4.Count; facing++)
                    {
                        var d = Grid.Dirs4[facing];
                        var w = Add(p, d);
                        var kind = c.Get(w.X, w.Y);
                        if ((kind != TileKinds.Window && kind != TileKinds.WindowBroken) || !Outer(w)) continue;
                        var near = Grid.Dirs4.Any((e) =>
                        {
                            var t = c.Get(p.X + e.X, p.Y + e.Y);
                            return t == TileKinds.Door || t == TileKinds.DoorOpen || t == TileKinds.Ladder;
                        });
                        if (near || c.Get(p.X, p.Y) != TileKinds.Floor) ok = false;
                        else piles.Add((p, facing));
                    }
                }
                if (!ok || piles.Count == 0) continue;
                foreach (var (p, facing) in piles)
                {
                    c.Force(p.X, p.Y, TileKinds.High);
                    c.Dress(p, DecorKinds.WindowBarricade, facing);
                }
            }
        }

        /// <summary>
        /// The way an object at `p` against the walls of `room` faces: away from the
        /// nearest wall, and in a corner away from the long wall (a bed's head, the back
        /// of a wardrobe).
        /// </summary>
        private static int Inward(Rect room, Vec2 p)
        {
            var gaps = new[] { p.Y - room.Y, room.X + room.W - 1 - p.X, room.Y + room.H - 1 - p.Y, p.X - room.X };
            bool LongWall(int s) => (room.W >= room.H) == (s % 2 == 0);
            return Opposite(BestOf(new[] { 0, 1, 2, 3 }, (s) => gaps[s] * 2 + (LongWall(s) ? 0 : 1))!.Value);
        }

        /// <summary>
        /// A room of a house cut in two by a wall across its length with a door in
        /// it (playtest 2026-10-18, #15): a strip `small` tiles deep at one end (a
        /// bedroom 3; a bathroom 2, the one room of 2 × 3: docs/MAPAS.md §10) and the
        /// rest, at least 3 a side; [the rest, the strip]. Null if it is too short, or
        /// if the wall would end on a doorway or cross the way through one. By
        /// position (mix): no rng drawn.
        /// </summary>
        private static (Rect, Rect)? SplitOff(Canvas c, Rect r, int small = 3)
        {
            var alongX = r.W >= r.H;
            var long_ = alongX ? r.W : r.H;
            var short_ = alongX ? r.H : r.W;
            if (long_ < small + 1 + 3 || short_ < 3) return null;
            var m = Mix(r.X * 13 + 7, r.Y * 17 + 3);
            foreach (var atEnd in m % 2 != 0 ? new[] { true, false } : new[] { false, true })
            {
                var k = atEnd ? long_ - small - 1 : small;
                var line = alongX ? TilesIn(Rect(r.X + k, r.Y, 1, r.H)) : TilesIn(Rect(r.X, r.Y + k, r.W, 1));
                var ends = alongX ? new[] { V(r.X + k, r.Y - 1), V(r.X + k, r.Y + r.H) } : new[] { V(r.X - 1, r.Y + k), V(r.X + r.W, r.Y + k) };
                if (!ends.All((e) => c.Get(e.X, e.Y) == TileKinds.Wall) || !line.All((p) => c.Free(p.X, p.Y))) continue;
                // The strip a room off the rest, its door the new one: none of the room's own doors opens into it.
                var strip = atEnd ? (alongX ? Rect(r.X + k + 1, r.Y, r.W - k - 1, r.H) : Rect(r.X, r.Y + k + 1, r.W, r.H - k - 1)) : alongX ? Rect(r.X, r.Y, k, r.H) : Rect(r.X, r.Y, r.W, k);
                if (TilesIn(strip).Any((p) => Grid.Dirs4.Any((d) =>
                {
                    var q = Add(p, d);
                    var t = c.Get(q.X, q.Y);
                    return !InRect(r, q) && (t == TileKinds.Door || t == TileKinds.DoorOpen);
                }))) continue;
                // The door off the ends of the wall where there is room.
                var at = short_ > 2 ? 1 + (m % (short_ - 2)) : m % short_;
                var normal = alongX ? Grid.Dirs4[1] : Grid.Dirs4[2];
                for (var i = 0; i < line.Count; i++)
                {
                    var p = line[i];
                    c.Force(p.X, p.Y, i == at ? TileKinds.Door : TileKinds.Wall);
                    if (i == at) foreach (var q in new[] { p, Add(p, normal), Add(p, V(-normal.X, -normal.Y)) }) c.Reserve(q.X, q.Y);
                }
                var before = alongX ? Rect(r.X, r.Y, k, r.H) : Rect(r.X, r.Y, r.W, k);
                var after = alongX ? Rect(r.X + k + 1, r.Y, r.W - k - 1, r.H) : Rect(r.X, r.Y + k + 1, r.W, r.H - k - 1);
                return atEnd ? (before, after) : (after, before);
            }
            return null;
        }

        /// <summary>
        /// What each room of a house is, on its storey (`z`), by its size and by the
        /// one the way in opens into (`entry`, on the ground): there the living room,
        /// a kitchen beside it, the smallest the bathroom, the rest bedrooms and, in a
        /// big house, a study. Upstairs, bedrooms, a bathroom and maybe a study. A
        /// house of one or two rooms has its kitchen in the living room. Decided by
        /// size and place alone: the generator's rng draws as before.
        /// </summary>
        private static List<string> HomeUses(List<Rect> rooms, Rect? entry, int z)
        {
            var n = rooms.Count;
            int Area(Rect r) => r.W * r.H;
            var uses = rooms.Select((_) => HomeUse.Bedroom).ToList();
            var order = Enumerable.Range(0, n).ToList();
            Js.Sort(order, (a, b) =>
            {
                // area(a) - area(b) || x - x || y - y
                var d = Area(rooms[a]) - Area(rooms[b]);
                if (d != 0) return d;
                d = rooms[a].X - rooms[b].X;
                return d != 0 ? d : rooms[a].Y - rooms[b].Y;
            });
            bool Free(int k) => uses[k] == HomeUse.Bedroom;
            if (z == 0)
            {
                // indexOf by reference, as List.IndexOf on a class without Equals.
                var living = entry != null ? Math.Max(0, rooms.IndexOf(entry)) : order[n - 1];
                uses[living] = n > 2 && Area(rooms[living]) < 10 ? HomeUse.Hall : HomeUse.Living;
                if (n >= 3)
                {
                    // The kitchen through a wall from the living room, the bigger of those that are.
                    bool Touching(Rect a, Rect b) => a.X <= b.X + b.W && b.X <= a.X + a.W && a.Y <= b.Y + b.H && b.Y <= a.Y + a.H;
                    var beside = order.Where((k) => Free(k) && Touching(rooms[k], rooms[living])).ToList();
                    var rest = order.Where(Free).ToList();
                    // pop() of none is undefined, and uses[undefined] no room's.
                    var kitchen = beside.Count > 0 ? beside[beside.Count - 1] : rest.Count > 0 ? rest[rest.Count - 1] : -1;
                    if (kitchen >= 0) uses[kitchen] = HomeUse.Kitchen;
                }
                // A hall only where there is a living room too.
                if (uses[living] == HomeUse.Hall && !uses.Contains(HomeUse.Living))
                {
                    var free = order.Where(Free).ToList();
                    if (free.Count > 0) uses[free[free.Count - 1]] = HomeUse.Living;
                }
            }
            int? small = null;
            foreach (var k in order)
            {
                if (!Free(k)) continue;
                small = k;
                break;
            }
            if (small != null && (z > 0 ? n >= 2 : n >= 3)) uses[small.Value] = HomeUse.Bath;
            var left = order.Where(Free).ToList();
            if (left.Count >= (z > 0 ? 3 : 2) && n >= (z > 0 ? 3 : 5)) uses[left[left.Count - 1]] = HomeUse.Study;
            return uses;
        }

        /// <summary>
        /// How worn a house is (0 kept, near 1 wrecked): an old town's houses worse off
        /// than a grid's, each its own by where it stands (mix: no rng drawn).
        /// </summary>
        private static double HomeWear(Rect b, Style style)
        {
            var r = (Mix(b.X * 3 + 1, b.Y * 5 + 2) % 100) / 100.0;
            return style.Organic ? 0.35 + r * 0.6 : 0.1 + r * 0.55;
        }

        /// <summary>
        /// Furniture of one room, along its walls and in its middle; door approaches
        /// stay clear. Wardrobes, shelves and racks are full cover, the rest low. A
        /// house's room is furnished as what it is (`use`, homeUses; with a kitchen in
        /// the living room of a house that has none, `kitchenette`), and in a wrecked
        /// one (`wear`) a wardrobe may lie on its face, low cover.
        /// </summary>
        private static void FurnishRoom(Canvas c, Rect room, Rng rng, string kind, string? use = null, double wear = 0, bool kitchenette = false, bool studio = false)
        {
            var long_ = Math.Max(room.W, room.H);
            var short_ = Math.Min(room.W, room.H);
            var alongX = room.W >= room.H;
            // a runs along the long side, b across it.
            Vec2 P(int a, int b) => alongX ? V(room.X + a, room.Y + b) : V(room.X + b, room.Y + a);
            // The DIRS4 index of a step (da, db) in the room's frame.
            int Toward(int da, int db) => FindIndex(Grid.Dirs4, (d) => d.X == (alongX ? da : db) && d.Y == (alongX ? db : da));
            var corners = Shuffle(new List<Vec2> { P(0, 0), P(long_ - 1, 0), P(0, short_ - 1), P(long_ - 1, short_ - 1) }, rng);
            // A piece on one tile, where it cuts no way across the room (furnishing.ts keepsWays: the floor round it stays joined).
            bool PutIn(Vec2 p, string k, Look look) => c.Free(p.X, p.Y) && Furnishing.KeepsWays(c.Tiles, c.Decor, c.Width, c.Height, p, c.Elev) && Put(c, p, k, look);
            bool Corner(string k, string what) => corners.Any((p) => PutIn(p, k, new Look(what, Inward(room, p))));
            bool CornerObject(string k, string what, IReadOnlyList<int> size) => CornerFurniture(c, room, corners, k, what, size[0], size[1]) != null;
            bool Wide2(Vec2 at, int side, string k, string what, int facing)
            {
                var d = Grid.Dirs4[side];
                return PutFurniture(c, room, new[] { at, Add(at, d) }, k, what, facing) || PutFurniture(c, room, new[] { at, V(at.X - d.X, at.Y - d.Y) }, k, what, facing);
            }
            var mid = (int)Math.Floor(long_ / 2.0);
            switch (kind)
            {
                case Furnishes.Home:
                    if (!string.IsNullOrEmpty(use))
                    {
                        FurnishHome(c, room, use!, wear, kitchenette, studio, new RoomFrame(P, Toward, PutIn, Corner, CornerObject, Wide2, long_, short_, mid));
                        break;
                    }
                    Corner(TileKinds.High, DecorKinds.Wardrobe);
                    if (short_ >= 4)
                    {
                        // A living room (a sofa facing the table, two tiles long) or a dining room.
                        var dining = Mix(room.X, room.Y) % 2 == 0;
                        var row = (int)Math.Floor(short_ / 2.0);
                        PutFurniture(c, room, new[] { P(mid, row), P(mid + 1, row) }, TileKinds.Low, DecorKinds.Table, Toward(0, 1));
                        if (long_ >= 5 && !dining) Wide2(P(mid - 1, row), Toward(0, 1), TileKinds.Low, DecorKinds.Sofa, Toward(1, 0));
                    }
                    else
                    {
                        CornerObject(TileKinds.Low, DecorKinds.Bed, Decor.Footprint.Bed);
                    }
                    break;
                case Furnishes.Office:
                    if (short_ >= 4)
                    {
                        // Rows of desks facing each other across the room.
                        var rows = short_ >= 7 ? new[] { 2, short_ - 3 } : new[] { (int)Math.Floor(short_ / 2.0) };
                        foreach (var b in rows) for (var a = 1; a < long_ - 1; a++) if (long_ < 7 || a != mid) Put(c, P(a, b), TileKinds.Low, new Look(DecorKinds.Desk, Toward(0, b == short_ - 3 && rows.Length > 1 ? -1 : 1)));
                    }
                    else
                    {
                        Put(c, P(mid, short_ - 1), TileKinds.Low, new Look(DecorKinds.Desk, Toward(0, -1)));
                    }
                    Corner(TileKinds.High, DecorKinds.Cabinet);
                    break;
                case Furnishes.Lab:
                {
                    var wall = rng.Int(2) == 0 ? 0 : short_ - 1;
                    for (var a = 0; a < long_; a++) if (a % 3 != 2) Put(c, P(a, wall), TileKinds.High, new Look(DecorKinds.Rack, Toward(0, wall == 0 ? 1 : -1)));
                    if (short_ >= 5) for (var a = mid - 1; a <= mid; a++) Put(c, P(a, (int)Math.Floor(short_ / 2.0)), TileKinds.Low, new Look(DecorKinds.Desk, Toward(0, wall == 0 ? 1 : -1)));
                    break;
                }
                case Furnishes.Shop:
                    // A counter across the room and shelves against the side walls.
                    if (short_ >= 4) for (var a = 1; a < Math.Min(long_ - 1, 4); a++) Put(c, P(a, short_ - 2), TileKinds.Low, new Look(DecorKinds.Counter, Toward(0, -1)));
                    for (var a = 0; a < long_; a += 2) Put(c, P(a, 0), TileKinds.High, new Look(DecorKinds.Shelf, Toward(0, 1)));
                    break;
                case Furnishes.Store:
                    for (var a = 0; a < long_; a += 2) Put(c, P(a, short_ - 1), TileKinds.High, new Look(DecorKinds.Shelf, Toward(0, -1)));
                    Put(c, P(mid, 0), TileKinds.Low, new Look(DecorKinds.Crates));
                    break;
                case Furnishes.Barn:
                    Corner(TileKinds.Low, DecorKinds.Hay);
                    Corner(TileKinds.Low, DecorKinds.Hay);
                    break;
                case Furnishes.Clinic:
                    CornerObject(TileKinds.Low, DecorKinds.HospitalBed, Decor.Footprint.HospitalBed);
                    Corner(TileKinds.High, DecorKinds.MedicalShelf);
                    // Along the room's edge, with a central aisle. Different room shapes
                    // and shuffled corners give consultation rooms and wards their own plan.
                    if (short_ >= 3)
                    {
                        new[] { P(mid, short_ - 1), P(mid, 0), P(mid - 1, short_ - 1), P(mid - 1, 0) }
                            .Any((p) => Put(c, p, TileKinds.Low, new Look(DecorKinds.MedicalCart, Inward(room, p))));
                    }
                    if (short_ >= 5)
                    {
                        Corner(TileKinds.High, DecorKinds.MedicalCabinet);
                        if (rng.Int(2) != 0) CornerObject(TileKinds.Low, DecorKinds.Gurney, Decor.Footprint.Gurney);
                        else Corner(TileKinds.Low, DecorKinds.Wheelchair);
                    }
                    if (long_ >= 8 && short_ >= 5)
                    {
                        foreach (var a in new[] { 2, long_ - 3 }) PutFurniture(c, room, new[] { P(a, 0), P(a, 1) }, TileKinds.Low, DecorKinds.HospitalBed, Toward(0, 1));
                    }
                    break;
                case Furnishes.Bare:
                    break;
            }
        }

        /// <summary>
        /// furnishRoom's frame of a room: P(a, b) a along its long side and b across it, the way a step faces; a piece on a
        /// tile or in a corner where it cuts no way across the room; furniture over several tiles (decor.ts FOOTPRINT) in a
        /// corner, or two tiles wide from a tile, its second a step `side` (a DIRS4 index) or else the other way
        /// (putFurniture: where it closes no way).
        /// </summary>
        private sealed class RoomFrame
        {
            public readonly Func<int, int, Vec2> P;
            public readonly Func<int, int, int> Toward;
            public readonly Func<Vec2, string, Look, bool> PutIn;
            public readonly Func<string, string, bool> Corner;
            public readonly Func<string, string, IReadOnlyList<int>, bool> CornerObject;
            public readonly Func<Vec2, int, string, string, int, bool> Wide2;
            public readonly int Long;
            public readonly int Short;
            public readonly int Mid;

            public RoomFrame(Func<int, int, Vec2> p, Func<int, int, int> toward, Func<Vec2, string, Look, bool> putIn, Func<string, string, bool> corner, Func<string, string, IReadOnlyList<int>, bool> cornerObject, Func<Vec2, int, string, string, int, bool> wide2, int long_, int short_, int mid)
            {
                P = p;
                Toward = toward;
                PutIn = putIn;
                Corner = corner;
                CornerObject = cornerObject;
                Wide2 = wide2;
                Long = long_;
                Short = short_;
                Mid = mid;
            }
        }

        /// <summary>
        /// A house's room as what it is (playtest 2026-10-18, #15). By position alone
        /// (mix): nothing drawn from the generator's rng.
        ///   - living: a bookcase in a corner, the sofa facing the television or the
        ///     table, a kitchenette's counter and cooker along a wall if it is the kitchen
        ///     too, and a bed in a corner if it is all the house has (`studio`);
        ///   - kitchen: the fridge, counters and the cooker along one wall, a table in the middle of a big one;
        ///   - bedroom: the bed and the wardrobe in corners, a double bed in most of
        ///     those 3 × 4 or more;
        ///   - bath: the bath in a corner, the toilet (wc) and the washbasin; a toilet (aseo), the wc and the washbasin;
        ///   - pantry: racks along one wall;
        ///   - study: a desk, a bookcase and a cabinet;
        ///   - hall: a cupboard.
        /// The sofa, the tables, the beds and the bath over the tiles they take
        /// (decor.ts FOOTPRINT: docs/MAPAS.md, phase 1), and every piece only where
        /// it closes no way across the room.
        /// </summary>
        private static void FurnishHome(Canvas c, Rect room, string use, double wear, bool kitchenette, bool studio, RoomFrame f)
        {
            var P = f.P;
            var toward = f.Toward;
            var putIn = f.PutIn;
            var corner = f.Corner;
            var cornerObject = f.CornerObject;
            var wide2 = f.Wide2;
            var long_ = f.Long;
            var short_ = f.Short;
            var mid = f.Mid;
            var m = Mix(room.X * 7 + 3, room.Y * 11 + 5);
            // In a wrecked house a wardrobe may lie on its face (low cover).
            var fallen = wear > 0.65 && m % 3 == 0;
            switch (use)
            {
                case HomeUse.Living:
                {
                    corner(TileKinds.High, DecorKinds.Shelf);
                    var row = (int)Math.Floor(short_ / 2.0);
                    if (short_ >= 4)
                    {
                        // The table along the room, the sofa across it facing the table's end.
                        PutFurniture(c, room, new[] { P(mid, row), P(mid + 1, row) }, TileKinds.Low, DecorKinds.Table, toward(0, 1));
                        if (long_ >= 5) wide2(P(mid - 1, row), toward(0, 1), TileKinds.Low, DecorKinds.Sofa, toward(1, 0));
                    }
                    else
                    {
                        wide2(P(mid, 0), toward(1, 0), TileKinds.Low, DecorKinds.Sofa, toward(0, 1));
                        if (short_ >= 3) putIn(P(mid, short_ - 1), TileKinds.Low, new Look(DecorKinds.TvStand, toward(0, -1)));
                    }
                    if (kitchenette && long_ >= 5)
                    {
                        putIn(P(long_ - 1, short_ - 1), TileKinds.Low, new Look(DecorKinds.Counter, toward(0, -1)));
                        putIn(P(long_ - 2, short_ - 1), TileKinds.Low, new Look(DecorKinds.Stove, toward(0, -1)));
                    }
                    if (studio) cornerObject(TileKinds.Low, DecorKinds.Bed, Decor.Footprint.Bed);
                    break;
                }
                case HomeUse.Kitchen:
                {
                    // Along one long wall: the fridge at its end, the counters and the cooker.
                    var wall = m % 2 == 0 ? 0 : short_ - 1;
                    var into = wall == 0 ? 1 : -1;
                    var run = Math.Min(long_, 4);
                    for (var a = 0; a < run; a++)
                    {
                        var p = P(a, wall);
                        if (a == 0) putIn(p, TileKinds.High, new Look(DecorKinds.Fridge, toward(0, into)));
                        else putIn(p, TileKinds.Low, new Look(a == 2 ? DecorKinds.Stove : DecorKinds.Counter, toward(0, into)));
                    }
                    if (short_ >= 4 && long_ >= 4) wide2(P(mid, wall == 0 ? short_ - 2 : 1), toward(1, 0), TileKinds.Low, DecorKinds.Table, toward(0, -into));
                    break;
                }
                case HomeUse.Bedroom:
                    // A double bed only where it leaves room (3 × 4 or more), else a single.
                    if (!(short_ >= 3 && long_ >= 4 && m % 3 != 2 && cornerObject(TileKinds.Low, DecorKinds.Bed, Decor.Footprint.DoubleBed))) cornerObject(TileKinds.Low, DecorKinds.Bed, Decor.Footprint.Bed);
                    corner(fallen ? TileKinds.Low : TileKinds.High, DecorKinds.Wardrobe);
                    break;
                case HomeUse.Bath:
                    cornerObject(TileKinds.Low, DecorKinds.Bathtub, Decor.Footprint.Bathtub);
                    corner(TileKinds.Low, DecorKinds.Wc);
                    if (long_ >= 3) putIn(P(mid, 0), TileKinds.Low, new Look(DecorKinds.Washbasin, toward(0, 1)));
                    break;
                case HomeUse.Toilet:
                    corner(TileKinds.Low, DecorKinds.Wc);
                    if (long_ >= 3) putIn(P(mid, 0), TileKinds.Low, new Look(DecorKinds.Washbasin, toward(0, 1)));
                    break;
                case HomeUse.Pantry:
                    // Racks along one long wall (loot.ts: a store's, food), the floor along the other.
                    foreach (var wall in m % 2 == 0 ? new[] { 0, short_ - 1 } : new[] { short_ - 1, 0 })
                    {
                        var n = 0;
                        for (var a = 0; a < long_; a++) if (putIn(P(a, wall), TileKinds.High, new Look(DecorKinds.Rack, toward(0, wall == 0 ? 1 : -1)))) n++;
                        if (n != 0) break;
                    }
                    break;
                case HomeUse.Study:
                    putIn(P(mid, short_ >= 4 ? 1 : 0), TileKinds.Low, new Look(DecorKinds.Desk, toward(0, 1)));
                    corner(TileKinds.High, DecorKinds.Shelf);
                    corner(fallen ? TileKinds.Low : TileKinds.High, DecorKinds.Cabinet);
                    break;
                case HomeUse.Hall:
                    corner(TileKinds.High, DecorKinds.Cabinet);
                    break;
            }
        }

        /// <summary>
        /// Rows of shelving along a hall's depth, two-tile aisles between them and a
        /// cross aisle halfway down long halls; a barn as a barn (furnishBarn).
        /// </summary>
        private static void FurnishHall(Canvas c, Rect inner, int front, Rng rng, string kind, Rect? avoid)
        {
            if (kind == Furnishes.Barn)
            {
                FurnishBarn(c, inner, front, avoid);
                return;
            }
            var across = SideLen(inner, front);
            var depth = DepthOf(inner, front);
            var rows = (int)Math.Floor((across - 2) / 3.0);
            var start = (int)Math.Floor((across - (3 * (rows - 1) + 1)) / 2.0);
            var cross = depth > 9 ? (int)Math.Floor(depth / 2.0) : -9;
            Rect? blocked = avoid != null ? Rect(avoid.X - 1, avoid.Y - 1, avoid.W + 2, avoid.H + 2) : null;
            for (var r = 0; r < rows; r++)
            {
                var u = start + 3 * r;
                for (var v = 2; v <= depth - 3; v++)
                {
                    if (v == cross || v == cross - 1) continue;
                    var p = LocalPt(inner, front, u, v);
                    if (blocked != null && InRect(blocked, p)) continue;
                    // Rows run along the depth: each piece faces across its row.
                    Put(c, p, rng.Next() < 0.8 ? TileKinds.High : TileKinds.Low, new Look(DecorKinds.Shelf, (front + 1) % 4));
                }
            }
        }

        /// <summary>A supermarket's shop window (playtest 20/10, #18): its whole front side `s` glazed, but its corners and doors.</summary>
        private static void ShopFront(Canvas c, Rect b, int s)
        {
            var back = Grid.Dirs4[Opposite(s)];
            for (var u = 1; u <= SideLen(b, s) - 2; u++)
            {
                var p = LocalPt(b, s, u, 0);
                var q = Add(p, Grid.Dirs4[s]);
                if (c.Get(p.X, p.Y) != TileKinds.Wall || c.Get(p.X + back.X, p.Y + back.Y) != TileKinds.Floor) continue;
                if (!c.In(q.X, q.Y) || c.Get(q.X, q.Y) == TileKinds.Wall) continue;
                c.Force(p.X, p.Y, TileKinds.Window);
                c.Reserve(p.X + back.X, p.Y + back.Y);
            }
        }

        /// <summary>
        /// A supermarket's floor (playtest 20/10, #18), from its front side `s`, its double door at `door` (along the outer
        /// wall, the first of the two): a walk along the windows; the checkouts by the way in, three at most, a lane beside
        /// each and two tiles clear of the doors' columns; a walk across; then the aisles of shelves running back, along both
        /// side walls and in rows between with two tiles between them, each row's end to the checkouts low; a walk along the
        /// back and the drinks fridges against the back wall. By position: no rng drawn.
        /// </summary>
        private static void FurnishMarket(Canvas c, Rect inner, int s, int door)
        {
            var across = SideLen(inner, s);
            var depth = DepthOf(inner, s);
            Vec2 P(int u, int v) => LocalPt(inner, s, u, v);
            var d0 = door - 1;
            var tills = new[] { d0 - 2, d0 + 3, d0 - 4, d0 + 5 }.Where((u) => u >= 0 && u < across).Take(3).ToList();
            foreach (var u in tills) Put(c, P(u, 1), TileKinds.Low, new Look(DecorKinds.Counter, (s + 1) % 4));
            // The rows between the side walls' shelves, an aisle of two at least either side of each.
            var span = across - 2;
            var n = Math.Max(0, (int)Math.Floor((span - 2) / 3.0));
            var first = 1 + (int)Math.Floor((span - 3 * n + 2) / 2.0);
            var rows = new List<int> { 0, across - 1 };
            rows.AddRange(Enumerable.Range(0, n).Select((k) => first + 3 * k));
            foreach (var u in rows)
            {
                for (var v = 3; v <= depth - 3; v++)
                {
                    var end = v == 3 && u != 0 && u != across - 1;
                    Put(c, P(u, v), end ? TileKinds.Low : TileKinds.High, new Look(DecorKinds.Shelf, (s + 1) % 4));
                }
            }
            for (var u = 0; u < across; u++) Put(c, P(u, depth - 1), TileKinds.High, new Look(DecorKinds.DrinksFridge, s));
        }

        /// <summary>
        /// A barn (playtest 18/10, #53: «tiene que parecer un granero»): its hay
        /// against the walls, the sides and the back, stacked high here and there
        /// with gaps between, and its old tractor (a lorry's block, of 2 × 3: decor.ts
        /// FOOTPRINT; the client draws it as one) on the floor in the middle, its nose
        /// to the doors; the way in from them kept clear, the tack room (`avoid`) left
        /// as it is. By position (mix): no rng drawn.
        /// </summary>
        private static void FurnishBarn(Canvas c, Rect inner, int front, Rect? avoid)
        {
            var across = SideLen(inner, front);
            var depth = DepthOf(inner, front);
            Rect? blocked = avoid != null ? Rect(avoid.X - 1, avoid.Y - 1, avoid.W + 2, avoid.H + 2) : null;
            var m0 = Mix(inner.X * 5 + 3, inner.Y * 7 + 1);
            // The tractor first, while the middle is clear: off the middle to one side.
            if (across >= 6 && depth >= 8)
            {
                var wide = Decor.Footprint.Tractor[0];
                var deep = Decor.Footprint.Tractor[1];
                var u0 = (int)Math.Floor(across / 2.0) - 1 + (m0 % 2 != 0 ? 1 : -1);
                var v0 = (int)Math.Floor((depth - deep) / 2.0);
                var cells = TilesIn(Rect(0, 0, wide, deep)).Select((q) => LocalPt(inner, front, u0 + q.X, v0 + q.Y)).ToList();
                if (cells.All((p) => c.Free(p.X, p.Y) && !(blocked != null && InRect(blocked, p))))
                {
                    foreach (var p in cells) Put(c, p, TileKinds.High, new Look(DecorKinds.Truck, front));
                    c.Objects.Add(cells.Select((p) => new ObjTile(c.I(p.X, p.Y), c.Decor[c.I(p.X, p.Y)])).ToList());
                }
            }
            for (var v = 2; v < depth; v++)
            {
                for (var u = 0; u < across; u++)
                {
                    if (u != 0 && u != across - 1 && v != depth - 1) continue;
                    var p = LocalPt(inner, front, u, v);
                    if (blocked != null && InRect(blocked, p)) continue;
                    var m = Mix(p.X * 7 + 1, p.Y * 13 + 5);
                    if (m % 5 == 0) continue;
                    Put(c, p, m % 3 == 0 ? TileKinds.High : TileKinds.Low, new Look(DecorKinds.Hay, Inward(inner, p)));
                }
            }
        }
    }
}
