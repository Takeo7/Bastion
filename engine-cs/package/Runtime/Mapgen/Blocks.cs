#nullable enable
// mapgen.ts, its buildings and districts (from building to sidewalk): a walled building and its plan, a house's hall,
// the blocks of flats, ruins, flats with a terrace, platforms; a district's streets, the side a lot shows to the street,
// cars along the curb and the sidewalks' furniture.
using System;
using System.Collections.Generic;
using System.Linq;
using HomeUse = Bastion.Engine.HomeUses; // TypeScript's HomeUse (Mapgen.HomeUses is the function homeUses).
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>
        /// Walled building on `b` (walls included) with its front on side `front`. The
        /// interior is floored indoors, divided by `plan` and furnished; doors open on
        /// the street side (another side if that one is blocked) and windows follow a
        /// regular spacing.
        /// </summary>
        private static Building MakeBuilding(Canvas c, Rect b, Rng rng, string plan, int front, Style style, string furnish, string medicalKind = MedicalSiteKinds.Clinic, bool hall = false, bool through = false)
        {
            for (var y = b.Y; y < b.Y + b.H; y++)
            {
                for (var x = b.X; x < b.X + b.W; x++)
                {
                    var edge = x == b.X || y == b.Y || x == b.X + b.W - 1 || y == b.Y + b.H - 1;
                    c.Force(x, y, edge ? TileKinds.Wall : TileKinds.Floor, 0);
                    c.Ground[c.I(x, y)] = Indoor;
                }
            }
            var rooms = new List<Rect>();
            var doors = new List<int?> { null, null, null, null };
            Rect? office = null;
            (Rect R, int U, Rect Stairs)? houseHall = null;
            List<MedicalRoom>? medicalRooms = furnish == Furnishes.Clinic ? MedicalPlan(c, b, front, rng, 0, medicalKind == MedicalSiteKinds.Hospital, medicalKind == MedicalSiteKinds.Ward, doors) : null;
            if (medicalRooms != null)
            {
                // A MedicalRoom is a Rect in TypeScript (its x, y, w, h): here a rect of its own, as nothing compares them.
                rooms.AddRange(medicalRooms.Select((m) => Rect(m.X, m.Y, m.W, m.H)));
            }
            else if (plan == Plans.Corridor && CorridorPlan(c, b, front, rng, rooms, doors))
            {
                // Rooms and doors are in place.
            }
            else if (plan == Plans.Shop)
            {
                ShopPlan(c, b, front, rng, rooms);
            }
            else if (plan == Plans.Hall)
            {
                office = HallPlan(c, b, front, rng, rooms);
            }
            else if (plan == Plans.Market)
            {
                rooms.Add(Inset(b, 1));
            }
            else if (hall && furnish == Furnishes.Home && Math.Min(SideLen(b, front), DepthOf(b, front)) >= 8)
            {
                // A house's hall, its front door in it (docs/MAPAS.md §4.3); or open at both ends, two wide, through to what is
                // behind the house under its storey above (`through`: a zaguán, §5.4).
                houseHall = HallPlan2(c, b, front, rng, rooms, false);
                var u = 1 + houseHall.Value.U;
                if (through)
                {
                    foreach (var s in new[] { front, Opposite(front) })
                    {
                        var d = Grid.Dirs4[s];
                        foreach (var p in TilesIn(houseHall.Value.R))
                        {
                            var q = Add(p, d);
                            if (InRect(houseHall.Value.R, q)) continue;
                            c.Force(q.X, q.Y, TileKinds.Floor);
                            foreach (var t in new[] { p, q, Add(q, d) }) c.Reserve(t.X, t.Y);
                        }
                        doors[s] = u;
                    }
                }
                else
                {
                    var door = LocalPt(b, front, u, 0);
                    c.Force(door.X, door.Y, TileKinds.Door);
                    foreach (var q in new[] { door, Add(door, Grid.Dirs4[front]), LocalPt(b, front, u, 1) }) c.Reserve(q.X, q.Y);
                    doors[front] = u;
                }
            }
            else
            {
                SplitRooms(c, Inset(b, 1), rng, rooms, plan == Plans.Open ? 8 : 6, plan == Plans.Open);
            }
            // A hall's loading bay is 3 wide; a supermarket's way in, a double door.
            var width = plan == Plans.Hall ? 3 : plan == Plans.Market ? 2 : 1;
            var gap = plan == Plans.Hall ? TileKinds.Floor : TileKinds.Door;
            foreach (var s in new[] { front, (front + 1) % 4, (front + 3) % 4, Opposite(front) })
            {
                if (doors.Any((d) => d != null)) break;
                doors[s] = Entrance(c, b, s, rng, width, gap) ?? Entrance(c, b, s, rng, 1);
            }
            // The side the way in opened on (the street's, unless it had no room).
            var way = doors.FindIndex((d) => d != null);
            // A back door for houses with more than one room.
            var back = Opposite(front);
            if (plan != Plans.Corridor && plan != Plans.Hall && rooms.Count > 1 && doors[back] == null && rng.Next() < 0.45) doors[back] = Entrance(c, b, back, rng);
            // A supermarket's staff door, onto its yard.
            if (plan == Plans.Market && doors[back] == null) doors[back] = Entrance(c, b, back, rng);
            // A house's rooms (playtest 18/10, #15): the one the front door opens into is the living room; a small house has
            // its room behind cut into a bedroom and a bathroom (one of a single room, a living room and a bedroom), its
            // kitchen in the living room. Before the windows, so that none comes where a new wall meets the outer one.
            (List<string> Uses, bool Kitchenette, Rect? Strip)? home = null;
            if (furnish == Furnishes.Home && medicalRooms == null)
            {
                var side = doors[front] != null ? front : doors.FindIndex((d) => d != null);
                Vec2? inside = side >= 0 ? LocalPt(b, side, doors[side]!.Value, 1) : null;
                // Through a hall, the room nearest the street (hallPlan2 lists it first).
                Rect? entry = houseHall != null ? rooms[0] : inside != null ? rooms.Find((r) => InRect(r, inside)) : null;
                var living = entry ?? rooms[0];
                if (houseHall != null)
                {
                    // A house of storeys (its hall, its stairs): down here the living room at the front and the kitchen at the back,
                    // any room between them a bedroom; cut off them where each keeps 3 a side, 2 deep (docs/MAPAS.md §10), a toilet
                    // (aseo) off the living room by the way in, else off the kitchen, and a pantry off the kitchen. Its bedrooms and
                    // its bathroom are upstairs (upperFloors).
                    var uses = rooms.Select((_, k) => k == 0 ? HomeUse.Living : k == rooms.Count - 1 ? HomeUse.Kitchen : HomeUse.Bedroom).ToList();
                    Rect? strip = null;
                    void CutOff(string use, string[] from)
                    {
                        foreach (var u in from)
                        {
                            var k = uses.IndexOf(u);
                            var cut = k >= 0 ? SplitOff(c, rooms[k], 2) : null;
                            if (cut == null) continue;
                            rooms.RemoveAt(k);
                            rooms.InsertRange(k, new[] { cut.Value.Item1, cut.Value.Item2 });
                            uses.RemoveAt(k);
                            uses.InsertRange(k, new[] { u, use });
                            strip ??= cut.Value.Item2;
                            return;
                        }
                    }
                    CutOff(HomeUse.Toilet, new[] { HomeUse.Living, HomeUse.Kitchen });
                    CutOff(HomeUse.Pantry, new[] { HomeUse.Kitchen });
                    home = (uses, !uses.Contains(HomeUse.Kitchen), strip);
                }
                else if (rooms.Count <= 2)
                {
                    // `back` in TypeScript (the room behind; the name is the back side's here).
                    var backRoom = rooms.Find((r) => r != living);
                    var cut = backRoom != null ? SplitOff(c, backRoom, 2) : SplitOff(c, living);
                    if (cut != null && backRoom != null)
                    {
                        var at = rooms.IndexOf(backRoom);
                        rooms.RemoveAt(at);
                        rooms.InsertRange(at, new[] { cut.Value.Item1, cut.Value.Item2 });
                    }
                    else if (cut != null)
                    {
                        var at = rooms.IndexOf(living);
                        rooms.RemoveAt(at);
                        rooms.InsertRange(at, new[] { cut.Value.Item1, cut.Value.Item2 });
                    }
                    home = (rooms.Select((r) => r == living || (backRoom == null && cut != null && r == cut.Value.Item1) ? HomeUse.Living : cut != null && r == cut.Value.Item2 ? (backRoom != null ? HomeUse.Bath : HomeUse.Bedroom) : HomeUse.Bedroom).ToList(), true, cut?.Item2);
                }
                else
                {
                    var uses = HomeUses(rooms, entry, 0);
                    // A pantry off the kitchen, 2 deep (docs/MAPAS.md §10), where the kitchen keeps 3 a side.
                    var kitchen = uses.IndexOf(HomeUse.Kitchen);
                    var cut = kitchen >= 0 ? SplitOff(c, rooms[kitchen], 2) : null;
                    if (cut != null)
                    {
                        rooms.RemoveAt(kitchen);
                        rooms.InsertRange(kitchen, new[] { cut.Value.Item1, cut.Value.Item2 });
                        uses.RemoveAt(kitchen);
                        uses.InsertRange(kitchen, new[] { HomeUse.Kitchen, HomeUse.Pantry });
                    }
                    home = (uses, !uses.Contains(HomeUse.Kitchen), cut?.Item2);
                }
            }
            // Shops show their goods: a window every other tile; a supermarket, its whole front.
            if (plan == Plans.Market && way >= 0) ShopFront(c, b, way);
            else Windows(c, b, plan == Plans.Shop ? 2 : style.WindowEvery, doors);
            // No window on the wall beside a hall's stairs: a flight before it would leave it no way in or out.
            if (houseHall != null)
            {
                foreach (var p in TilesIn(houseHall.Value.Stairs))
                {
                    foreach (var d in Grid.Dirs4)
                    {
                        var q = Add(p, d);
                        if (!InRect(houseHall.Value.R, q) && (c.Get(q.X, q.Y) == TileKinds.Window || c.Get(q.X, q.Y) == TileKinds.WindowBroken) && (q.X == b.X || q.Y == b.Y || q.X == b.X + b.W - 1 || q.Y == b.Y + b.H - 1)) c.Force(q.X, q.Y, TileKinds.Wall);
                    }
                }
            }
            // Now and then a room held out in: its windows barricaded from inside (#20).
            if (medicalRooms == null && plan != Plans.Hall && plan != Plans.Market && (furnish == Furnishes.Home || furnish == Furnishes.Shop || furnish == Furnishes.Office)) BarricadeRooms(c, b, rooms);
            if (medicalRooms != null)
            {
                foreach (var room in medicalRooms) FurnishMedical(c, room, rng, medicalKind == MedicalSiteKinds.Clinic ? null : medicalKind);
            }
            else if (plan == Plans.Hall)
            {
                FurnishHall(c, rooms[0], front, rng, furnish, office);
                // A barn's corner room is its tack room, with hay of its own (the same rng drawn as an office's).
                if (office != null) FurnishRoom(c, office, rng, furnish == Furnishes.Barn ? Furnishes.Barn : Furnishes.Office);
            }
            else if (plan == Plans.Market)
            {
                if (way >= 0) FurnishMarket(c, rooms[0], way, doors[way]!.Value);
            }
            else if (home != null)
            {
                // Each room as what it is (furnishHome).
                // The strip cut off a room is furnished on a roll of its own: the generator's rng draws as it did before.
                var (uses, kitchenette, strip) = home.Value;
                var wear = HomeWear(b, style);
                for (var k = 0; k < rooms.Count; k++)
                {
                    var room = rooms[k];
                    FurnishRoom(c, room, room == strip ? new Rng(Mix(room.X, room.Y)) : rng, Furnishes.Home, uses[k], wear, kitchenette && uses[k] == HomeUse.Living, rooms.Count == 1);
                }
                // (A zaguán is no room of the house: a way through, nothing furnished nor left in it.)
                var homeRooms = new List<HomeRoom>();
                if (houseHall != null && !through)
                {
                    var r = houseHall.Value.R;
                    homeRooms.Add(new HomeRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = 0, Use = HomeUse.Hall });
                }
                homeRooms.AddRange(rooms.Select((r, k) => new HomeRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = 0, Use = uses[k] }));
                // `raised` in TypeScript (the name is the other buildings' below).
                var raisedHome = new Building { Outer = b, Rooms = rooms, Furnish = furnish, HomeWear = wear, HomeRooms = homeRooms };
                if (houseHall != null)
                {
                    raisedHome.HouseHall = houseHall.Value.R;
                    raisedHome.HouseStairs = houseHall.Value.Stairs;
                    raisedHome.Front = front;
                }
                c.Buildings.Add(raisedHome);
                return raisedHome;
            }
            else
            {
                for (var k = 0; k < rooms.Count; k++) FurnishRoom(c, rooms[k], rng, furnish == Furnishes.Shop && k > 0 ? Furnishes.Store : furnish);
            }
            var raised = new Building { Outer = b, Rooms = rooms, Furnish = furnish };
            if (medicalRooms != null)
            {
                raised.MedicalKind = medicalKind;
                raised.MedicalRooms = medicalRooms;
                raised.Front = front;
            }
            if (plan == Plans.Hall) raised.Hall = true;
            if (plan == Plans.Market)
            {
                raised.Market = true;
                raised.Front = way;
            }
            c.Buildings.Add(raised);
            return raised;
        }

        /// <summary>
        /// A house's storey round its hall (docs/MAPAS.md §4.3: the stairwell is the hall, 2 wide): the hall down one side
        /// from the front to the back (the same side on every storey, by where the house stands), a wall along it, the rest
        /// cut into rooms (splitRooms), each with a door onto the hall. `kept` (upstairs): the stairwell's tiles kept clear.
        /// Returns the hall, where along the front its door goes (inside the walls), and the rooms, the nearest the street first.
        /// </summary>
        private static (Rect R, int U, Rect Stairs) HallPlan2(Canvas c, Rect b, int front, Rng rng, List<Rect> rooms, bool kept)
        {
            var inner = Inset(b, 1);
            var across = SideLen(inner, front);
            var deep = DepthOf(inner, front);
            var left = Mix(b.X * 7 + 3, b.Y * 11 + 5) % 2 == 0;
            var hu = left ? 0 : across - 2;
            var wu = left ? 2 : across - 3;
            var hall = Local(inner, front, hu, 0, 2, deep);
            for (var v = 0; v < deep; v++)
            {
                var p = LocalPt(inner, front, wu, v);
                if (!kept || c.Get(p.X, p.Y) == TileKinds.Floor) c.Force(p.X, p.Y, TileKinds.Wall);
            }
            var rest = Local(inner, front, left ? 3 : 0, 0, across - 3, deep);
            var own = new List<Rect>();
            SplitRooms(c, rest, rng, own, 6, false, kept);
            // How far in from the front a room starts and ends.
            int DepthAt(Vec2 p) => front == 0 ? p.Y - inner.Y : front == 2 ? inner.Y + inner.H - 1 - p.Y : front == 3 ? p.X - inner.X : inner.X + inner.W - 1 - p.X;
            (int, int) Span(Rect r)
            {
                var a = DepthAt(V(r.X, r.Y));
                var z = DepthAt(V(r.X + r.W - 1, r.Y + r.H - 1));
                return (Math.Min(a, z), Math.Max(a, z));
            }
            Js.Sort(own, (a, r) => Span(a).Item1 - Span(r).Item1);
            var side = Grid.Dirs4[left ? (front + 1) % 4 : (front + 3) % 4];
            foreach (var r in own)
            {
                var (v0, v1) = Span(r);
                // Its door off the hall at its middle; in a room six long or more, by its front end (the far end left for a
                // bathroom or a toilet cut off it: splitOff).
                var p = LocalPt(inner, front, wu, v1 - v0 >= 5 ? v0 + 1 : (int)Math.Floor((v0 + v1) / 2.0));
                c.Force(p.X, p.Y, TileKinds.Door);
                foreach (var q in new[] { p, Add(p, side), V(p.X - side.X, p.Y - side.Y) }) c.Reserve(q.X, q.Y);
            }
            rooms.AddRange(own);
            return (hall, hu + (Mix(b.X, b.Y * 3) % 2), Local(inner, front, left ? 0 : across - 1, 0, 1, deep));
        }

        /// <summary>
        /// One storey of a block of flats on `c` (docs/MAPAS.md §4.2: 16–18 × 9–11 here, two flats a landing; §4.3: the
        /// stairwell is a hall): down its middle from the front, the stair core, three wide and as deep as the block; either
        /// side of it a flat, its door onto the core near the front, cut into a room in front and one behind. Returns the core
        /// and each flat's rooms, the one its door opens into first.
        /// </summary>
        private static (Rect Core, List<List<Rect>> Flats) FlatsPlan(Canvas c, Rect b, int front, Rng rng, bool kept)
        {
            var inner = Inset(b, 1);
            var across = SideLen(inner, front);
            var deep = DepthOf(inner, front);
            var u0 = (int)Math.Floor((across - 3) / 2.0);
            var core = Local(inner, front, u0, 0, 3, deep);
            var flats = new List<List<Rect>>();
            foreach (var side in new[] { -1, 1 })
            {
                var wall = side < 0 ? u0 - 1 : u0 + 3;
                for (var v = 0; v < deep; v++)
                {
                    var p = LocalPt(inner, front, wall, v);
                    if (v == 1)
                    {
                        // Its door onto the landing, both sides of it kept clear.
                        c.Force(p.X, p.Y, TileKinds.Door);
                        var d = Grid.Dirs4[side < 0 ? (front + 3) % 4 : (front + 1) % 4];
                        foreach (var q in new[] { p, Add(p, d), V(p.X - d.X, p.Y - d.Y) }) c.Reserve(q.X, q.Y);
                    }
                    else if (!kept || c.Get(p.X, p.Y) == TileKinds.Floor) c.Force(p.X, p.Y, TileKinds.Wall);
                }
                var area = side < 0 ? Local(inner, front, 0, 0, u0 - 1, deep) : Local(inner, front, u0 + 4, 0, across - u0 - 4, deep);
                var rooms = new List<Rect>();
                SplitRooms(c, area, rng, rooms, 5, false, kept);
                // The room the door opens into first.
                var by = LocalPt(inner, front, side < 0 ? u0 - 2 : u0 + 4, 1);
                Js.Sort(rooms, (a, r) => (InRect(r, by) ? 1 : 0) - (InRect(a, by) ? 1 : 0));
                flats.Add(rooms);
            }
            return (core, flats);
        }

        /// <summary>
        /// What each room of a flat is (as a small house's ground floor): the one its door opens into the living room, a
        /// kitchenette in it; the one behind the bedroom, a bathroom cut off it (or off the living room). Returns the rooms,
        /// their uses, and the bathroom's strip (furnished on a roll of its own).
        /// </summary>
        private static (List<Rect> Rooms, List<string> Uses, Rect? Strip) FlatUses(Canvas c, List<Rect> rooms)
        {
            var living = rooms[0];
            var back = rooms.Find((r) => r != living);
            var cut = back != null ? SplitOff(c, back, 2) : SplitOff(c, living, 2);
            var out_ = new List<Rect>(rooms);
            if (cut != null)
            {
                var at = out_.IndexOf(back ?? living);
                out_.RemoveAt(at);
                out_.InsertRange(at, new[] { cut.Value.Item1, cut.Value.Item2 });
            }
            var uses = out_.Select((r) => r == living || (back == null && cut != null && r == cut.Value.Item1) ? HomeUse.Living : cut != null && r == cut.Value.Item2 ? HomeUse.Bath : HomeUse.Bedroom).ToList();
            return (out_, uses, cut?.Item2);
        }

        /// <summary>
        /// A block of flats one goes into (docs/MAPAS.md §4.2, phase 4): three storeys of two flats each round a stair core
        /// (flatsPlan), its door onto the street, a flat roof one walks on up the stairs (the storeys above: upperFloors).
        /// </summary>
        private static Building FlatsBlock(Canvas c, Rect b, Rng rng, int front, Style style)
        {
            foreach (var p in TilesIn(b))
            {
                var edge = p.X == b.X || p.Y == b.Y || p.X == b.X + b.W - 1 || p.Y == b.Y + b.H - 1;
                c.Force(p.X, p.Y, edge ? TileKinds.Wall : TileKinds.Floor, 0);
                c.Ground[c.I(p.X, p.Y)] = Indoor;
            }
            var (core, flats) = FlatsPlan(c, b, front, rng, false);
            // The street door, in the middle of the core's front.
            var doors = new List<int?> { null, null, null, null };
            var u = 1 + (int)Math.Floor((SideLen(Inset(b, 1), front) - 3) / 2.0) + 1;
            var door = LocalPt(b, front, u, 0);
            c.Force(door.X, door.Y, TileKinds.Door);
            foreach (var q in new[] { door, Add(door, Grid.Dirs4[front]), LocalPt(b, front, u, 1) }) c.Reserve(q.X, q.Y);
            doors[front] = u;
            Windows(c, b, style.WindowEvery, doors);
            var wear = HomeWear(b, style);
            var homeRooms = new List<HomeRoom> { new HomeRoom { X = core.X, Y = core.Y, W = core.W, H = core.H, Z = 0, Use = HomeUse.Hall } };
            var rooms = new List<Rect> { core };
            foreach (var flat in flats)
            {
                var (own, uses, strip) = FlatUses(c, flat);
                for (var k = 0; k < own.Count; k++)
                {
                    var room = own[k];
                    FurnishRoom(c, room, room == strip ? new Rng(Mix(room.X, room.Y)) : rng, Furnishes.Home, uses[k], wear, uses[k] == HomeUse.Living, false);
                }
                rooms.AddRange(own);
                homeRooms.AddRange(own.Select((r, k) => new HomeRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = 0, Use = uses[k] }));
            }
            var raised = new Building { Outer = b, Rooms = rooms, Furnish = Furnishes.Home, Storeys = 3, Flat = true, HomeWear = wear, HomeRooms = homeRooms, FlatsCore = core, Front = front };
            c.Buildings.Add(raised);
            return raised;
        }

        /// <summary>A building left in ruins: stretches of wall knocked down, glass gone, rubble about.</summary>
        private static void Ruin(Canvas c, Building b, Rng rng)
        {
            var outer = b.Outer;
            for (var s = 0; s < 4; s++)
            {
                var n = SideLen(outer, s);
                for (var k = rng.Range(1, 2); k > 0; k--)
                {
                    var len = rng.Range(2, 3);
                    var u0 = rng.Range(1, Math.Max(1, n - 1 - len));
                    for (var u = u0; u < u0 + len && u < n - 1; u++)
                    {
                        var p = LocalPt(outer, s, u, 0);
                        if (c.Get(p.X, p.Y) == TileKinds.Wall) c.Force(p.X, p.Y, TileKinds.Floor);
                    }
                }
            }
            foreach (var p in TilesIn(outer))
            {
                var i = c.I(p.X, p.Y);
                if (c.Tiles[i] == TileKinds.Window) c.Tiles[i] = TileKinds.WindowBroken;
                if (c.Tiles[i] == TileKinds.Door) c.Tiles[i] = TileKinds.Floor;
                c.Ground[i] = Open;
            }
            foreach (var room in b.Rooms) GroupNear(c, Centre(room), rng, Rubble, 1, 0, 4);
            // Nothing grows inside.
            foreach (var p in TilesIn(Inset(outer, 1))) c.Reserve(p.X, p.Y);
            b.Ruined = true;
        }

        /// <summary>
        /// Block of flats two storeys tall, stepped down to a one-storey terrace on side
        /// `terrace`: a ladder climbs from the street to the terrace and another one from
        /// the terrace to the roof. Both rooftops have a parapet with regular gaps.
        /// </summary>
        private static void Flats(Canvas c, Rect b, Rng rng, int terrace)
        {
            var n = SideLen(b, terrace);
            var depth = DepthOf(b, terrace) >= 9 ? 3 : 2;
            var roof = Cut(b, terrace, depth);
            foreach (var p in TilesIn(b))
            {
                c.Force(p.X, p.Y, TileKinds.Floor, InRect(roof, p) ? 2 : 1);
                c.Ground[c.I(p.X, p.Y)] = Open;
            }
            var out_ = Grid.Dirs4[terrace];
            // From the street up to the terrace, in the middle of its outer side.
            var feet = new[] { 0, -1, 1, -2, 2 }.Select((k) => (int)Math.Floor(n / 2.0) + k).ToList();
            foreach (var u in feet)
            {
                var edge = LocalPt(b, terrace, u, 0);
                var foot = Add(edge, out_);
                if (!c.Free(foot.X, foot.Y)) continue;
                c.Force(foot.X, foot.Y, TileKinds.Ladder);
                foreach (var p in new[] { foot, Add(foot, out_), edge, LocalPt(b, terrace, u, 1) }) c.Reserve(p.X, p.Y);
                break;
            }
            // From the terrace up to the roof, near one end so the climb crosses the terrace.
            var u2 = rng.Int(2) == 0 ? 1 : n - 2;
            var step = LocalPt(b, terrace, u2, depth - 1);
            c.Force(step.X, step.Y, TileKinds.Ladder);
            foreach (var p in new[] { step, LocalPt(b, terrace, u2, depth), LocalPt(b, terrace, u2, depth + 1) }) c.Reserve(p.X, p.Y);
            Parapet(c, roof, 2, new[] { 0, 1, 2, 3 });
            Parapet(c, Local(b, terrace, 0, 0, n, depth), 1, new[] { terrace, (terrace + 1) % 4, (terrace + 3) % 4 });
            // A water tank on the roof.
            GroupNear(c, Centre(roof), rng, new IReadOnlyList<Part>[] { new[] { P(0, 0, TileKinds.High, new Look(DecorKinds.Tank)) } }, 1, 2, 6);
        }

        /// <summary>Low parapet along the given sides of a rooftop, with a gap every four tiles.</summary>
        private static void Parapet(Canvas c, Rect r, int level, IReadOnlyList<int> sides)
        {
            foreach (var s in sides)
            {
                var n = SideLen(r, s);
                for (var u = 0; u < n; u++) if (u % 4 != 2 || u == 0 || u == n - 1) Put(c, LocalPt(r, s, u, 0), TileKinds.Low, null, level);
            }
        }

        /// <summary>One-storey solid block (shipping container, loading platform) with a ladder at each end.</summary>
        private static void Platform(Canvas c, Rect r, Rng rng, int ladders)
        {
            foreach (var p in TilesIn(r))
            {
                c.Force(p.X, p.Y, TileKinds.Floor, 1);
                c.Ground[c.I(p.X, p.Y)] = Open;
            }
            var ends = r.W >= r.H ? new List<int> { 3, 1 } : new List<int> { 0, 2 };
            // shuffle(ends, rng).slice(0, ladders): a negative end counts from the back.
            foreach (var s in Shuffle(ends, rng).Take(ladders < 0 ? Math.Max(0, ends.Count + ladders) : ladders).ToList())
            {
                var n = SideLen(r, s);
                foreach (var u in new[] { (int)Math.Floor(n / 2.0), (int)Math.Floor(n / 2.0) - 1, 0, n - 1 })
                {
                    var edge = LocalPt(r, s, u, 0);
                    var foot = Add(edge, Grid.Dirs4[s]);
                    if (!c.Free(foot.X, foot.Y)) continue;
                    c.Force(foot.X, foot.Y, TileKinds.Ladder);
                    foreach (var p in new[] { foot, Add(foot, Grid.Dirs4[s]), edge }) c.Reserve(p.X, p.Y);
                    break;
                }
            }
        }

        // -------------------------------------------------------------- districts

        // interface Road { r: Rect; vertical: boolean } is PlanRoad (townPlan.ts: the same shape, its `avenue` left null).
        // interface SplitRules is in Core.cs (shared with the towns).

        private static Rect SwapRect(Rect r) => Rect(r.Y, r.X, r.H, r.W);

        /// <summary>The shortest straight stretch of a street that bends: a building's length beside it.</summary>
        private const int Stretch = 8;

        /// <summary>
        /// A street across `r` (north–south) that bends: `k` straight stretches, each
        /// a step of two or three tiles sideways from the last, joined by short cross
        /// streets. Mostly the steps go the same way, so the street runs on a slant
        /// across the district; else back and forth. On each side, the land beside a
        /// stretch is a block of its own (with the cross street's corner when the
        /// street steps away from it). Null when `r` is too short or too narrow.
        /// </summary>
        private static (List<Rect> Parts, List<PlanRoad> Streets)? BentStreet(Rect r, int w, int k, Rng rng, SplitRules rules)
        {
            var lo = r.X + rules.MinBlock;
            var hi = r.X + r.W - rules.MinBlock - w;
            var free = r.H - k * Stretch - (k - 1) * w;
            if (k < 2 || hi - lo < 2 || free < 0) return null;
            // Where each stretch ends (its cross street starts): even stretches, the slack shared out at random.
            var ends = new List<int>();
            var y = r.Y;
            var slack = free;
            for (var i = 0; i < k - 1; i++)
            {
                var extra = rng.Int((int)Math.Floor((double)slack / (k - i)) * 2 + 1);
                var take = Math.Min(extra, slack);
                slack -= take;
                ends.Add(y + Stretch + take);
                y = ends[i] + w;
            }
            var slant = rng.Next() < 0.65;
            var dir = rng.Int(2) == 0 ? -1 : 1;
            var xs = new List<int> { Clamp((int)Js.Round((lo + hi) / 2.0 - (slant ? dir * (k - 1) * 1.25 : 0) + rng.Range(-1, 1)), lo, hi) };
            for (var i = 1; i < k; i++)
            {
                var prev = xs[i - 1];
                var step = (slant ? dir : rng.Int(2) == 0 ? -1 : 1) * rng.Range(2, 3);
                var x = prev + step;
                if (x < lo || x > hi) x = prev - step;
                x = Clamp(x, lo, hi);
                if (x == prev) x = prev + (prev < hi ? 1 : -1);
                xs.Add(x);
            }
            var bottom = r.Y + r.H;
            var right = r.X + r.W;
            int Top(int i) => i == 0 ? r.Y : ends[i - 1] + w;
            int End(int i) => i == k - 1 ? bottom : ends[i];
            var streets = new List<PlanRoad>();
            var parts = new List<Rect>();
            for (var i = 0; i < k; i++)
            {
                var a = xs[i];
                streets.Add(new PlanRoad { R = Rect(a, Top(i), w, End(i) - Top(i)), Vertical = true });
                if (i < k - 1) streets.Add(new PlanRoad { R = Rect(Math.Min(a, xs[i + 1]), ends[i], Math.Abs(xs[i + 1] - a) + w, w), Vertical = false });
                // The corner of a cross street goes to the side the street steps away from.
                var leftFrom = i == 0 ? r.Y : a < xs[i - 1] ? ends[i - 1] : Top(i);
                var leftTo = i == k - 1 ? bottom : a < xs[i + 1] ? ends[i] + w : End(i);
                var rightFrom = i == 0 ? r.Y : a > xs[i - 1] ? ends[i - 1] : Top(i);
                var rightTo = i == k - 1 ? bottom : a > xs[i + 1] ? ends[i] + w : End(i);
                parts.Add(Rect(r.X, leftFrom, a - r.X, leftTo - leftFrom));
                parts.Add(Rect(a + w, rightFrom, right - a - w, rightTo - rightFrom));
            }
            return (parts, streets);
        }

        /// <summary>One street across `r`, running north–south (the caller transposes for the other axis).</summary>
        private static (List<Rect> Parts, List<PlanRoad> Streets) CutStreet(Rect r, int w, int depth, Rng rng, SplitRules rules)
        {
            var most = rules.Bends != null ? Js.At(rules.Bends, Math.Min(depth, rules.Bends.Count - 1), 0) : 0;
            if (most >= 2 && rng.Next() < (rules.Bend != null ? Js.At(rules.Bend, Math.Min(depth, rules.Bend.Count - 1), 0.0) : 0))
            {
                var bent = BentStreet(r, w, rng.Range(Math.Max(2, most - 1), most), rng, rules);
                if (bent != null) return bent.Value;
            }
            var m = rules.MinBlock;
            var lo = r.X + m;
            var hi = r.X + r.W - m - w;
            var a = Clamp((int)Js.Round((lo + hi) / 2.0 + (rng.Next() - 0.5) * (hi - lo) * 0.9), lo, hi);
            var right = r.X + r.W;
            var bottom = r.Y + r.H;
            var roll = rng.Next();
            if (roll < rules.Jog && r.H >= 2 * m + w && hi - lo >= 2)
            {
                // The street steps sideways halfway along, with a short cross street.
                var yj = Clamp(r.Y + (int)Js.Round((r.H - w) / 2.0 + (rng.Next() - 0.5) * (r.H - 2 * m - w)), r.Y + m, bottom - m - w);
                var b = Clamp(a + (rng.Int(2) == 0 ? -1 : 1) * rng.Range(2, 4), lo, hi);
                if (Math.Abs(b - a) >= 2)
                {
                    var streets = new List<PlanRoad>
                    {
                        new PlanRoad { R = Rect(a, r.Y, w, yj - r.Y), Vertical = true },
                        new PlanRoad { R = Rect(Math.Min(a, b), yj, Math.Abs(b - a) + w, w), Vertical = false },
                        new PlanRoad { R = Rect(b, yj + w, w, bottom - yj - w), Vertical = true },
                    };
                    var parts =
                        a < b
                            ? new List<Rect> { Rect(r.X, r.Y, a - r.X, yj + w - r.Y), Rect(a + w, r.Y, right - a - w, yj - r.Y), Rect(r.X, yj + w, b - r.X, bottom - yj - w), Rect(b + w, yj, right - b - w, bottom - yj) }
                            : new List<Rect> { Rect(r.X, r.Y, a - r.X, yj - r.Y), Rect(a + w, r.Y, right - a - w, yj + w - r.Y), Rect(r.X, yj, b - r.X, bottom - yj), Rect(b + w, yj + w, right - b - w, bottom - yj - w) };
                    return (parts, streets);
                }
            }
            if (depth > 0 && roll < rules.Jog + rules.DeadEnd && r.H >= 2 * m + 2)
            {
                // A dead end: the street stops against a block that spans the whole width.
                var len = rng.Range(m + 1, r.H - m);
                if (rng.Int(2) == 0)
                {
                    return (
                        new List<Rect> { Rect(r.X, r.Y, a - r.X, len), Rect(a + w, r.Y, right - a - w, len), Rect(r.X, r.Y + len, r.W, r.H - len) },
                        new List<PlanRoad> { new PlanRoad { R = Rect(a, r.Y, w, len), Vertical = true } }
                    );
                }
                return (
                    new List<Rect> { Rect(r.X, bottom - len, a - r.X, len), Rect(a + w, bottom - len, right - a - w, len), Rect(r.X, r.Y, r.W, r.H - len) },
                    new List<PlanRoad> { new PlanRoad { R = Rect(a, bottom - len, w, len), Vertical = true } }
                );
            }
            return (
                new List<Rect> { Rect(r.X, r.Y, a - r.X, r.H), Rect(a + w, r.Y, right - a - w, r.H) },
                new List<PlanRoad> { new PlanRoad { R = Rect(a, r.Y, w, r.H), Vertical = true } }
            );
        }

        /// <summary>
        /// Jittered binary split of `area` into blocks separated by streets: a wide
        /// avenue first, narrower streets inside, some of them jogging or ending.
        /// </summary>
        private static (List<Rect> Blocks, List<PlanRoad> Roads) District(Rect area, Rng rng, SplitRules rules)
        {
            var blocks = new List<Rect>();
            var roads = new List<PlanRoad>();
            void Split(Rect r, int depth)
            {
                var range = rules.Widths[Math.Min(depth, rules.Widths.Count - 1)];
                var lo = range[0];
                var hi = range[1];
                var w = rng.Range(lo, hi);
                var canX = r.W >= 2 * rules.MinBlock + w;
                var canY = r.H >= 2 * rules.MinBlock + w;
                var big = r.W > rules.MaxBlock || r.H > rules.MaxBlock;
                if ((!canX && !canY) || (!big && rng.Next() < Js.At(rules.Stop, depth, 1.0)))
                {
                    blocks.Add(r);
                    return;
                }
                var vertical = canX && (!canY || r.W > r.H || (r.W == r.H && rng.Int(2) == 0));
                var result = CutStreet(vertical ? r : SwapRect(r), w, depth, rng, rules);
                foreach (var s in result.Streets) roads.Add(vertical ? s : new PlanRoad { R = SwapRect(s.R), Vertical = !s.Vertical });
                foreach (var p in result.Parts) Split(vertical ? p : SwapRect(p), depth + 1);
            }
            Split(area, 0);
            return (blocks, roads);
        }

        /// <summary>Whether side `s` of `r` looks onto a road for most of its length.</summary>
        private static bool FacesRoad(Canvas c, Rect r, int s)
        {
            var d = Grid.Dirs4[s];
            var n = SideLen(r, s);
            var road = 0;
            for (var u = 0; u < n; u++)
            {
                var p = Add(LocalPt(r, s, u, 0), d);
                if (c.GroundAt(p.X, p.Y) == Road && c.In(p.X, p.Y)) road++;
            }
            return road * 2 >= n;
        }

        /// <summary>The side a lot shows to the street: its longest side on a road, else the one towards the map centre.</summary>
        private static int FrontOf(Canvas c, Rect lot, bool[] roads, Rng rng)
        {
            // roads[s] past the end is undefined in TypeScript: no road.
            var sides = new[] { 0, 1, 2, 3 }.Where((s) => s < roads.Length && roads[s]).ToList();
            if (sides.Count > 0) return BestOf(sides, (s) => -SideLen(lot, s) + rng.Next() * 3)!.Value;
            var mid = c.Width / 2.0;
            var to = Centre(lot);
            return Math.Abs(mid - to.X) > Math.Abs(mid - to.Y) ? (mid > to.X ? 1 : 3) : mid > to.Y ? 2 : 0;
        }

        /// <summary>
        /// Cars parked along the curb of streets three or more tiles wide: three tiles
        /// each (decor.ts FOOTPRINT), in line with the street, never at a crossing.
        /// `more`: a further chance of a wreck where new cover may stand (the second
        /// look: the captures' streets are full of them).
        /// </summary>
        private static void ParkCars(Canvas c, List<PlanRoad> roads, Rng rng, double chance, double more = 0)
        {
            foreach (var road in roads)
            {
                var width = road.Vertical ? road.R.W : road.R.H;
                var length = road.Vertical ? road.R.H : road.R.W;
                if (width < 3) continue;
                Vec2 At(int lane, int t) => road.Vertical ? V(road.R.X + lane, road.R.Y + t) : V(road.R.X + t, road.R.Y + lane);
                var lanes = width >= 4 ? new[] { 0, width - 1 } : new[] { rng.Int(2) == 0 ? 0 : width - 1 };
                foreach (var lane in lanes)
                {
                    var curb = lane == 0 ? -1 : width;
                    var inner = lane == 0 ? 1 : width - 2;
                    // Cars keep to the right: on the far lane of a north–south street they face north, and so on.
                    var facing = road.Vertical ? (lane == 0 ? 2 : 0) : lane == 0 ? 3 : 1;
                    var long_ = Decor.Footprint.Car[1];
                    for (var t = 1 + rng.Int(3); t + long_ < length; t++)
                    {
                        var roll = rng.Next();
                        if (roll > chance + more) continue;
                        var car = Enumerable.Range(0, long_).Select((k) => At(lane, t + k)).ToList();
                        // Past `chance`, the second look's extra wrecks: only where new cover may stand (roomForCover).
                        if (roll > chance && !car.All((p) => RoomForCover(c, p, 2))) continue;
                        var kerb = Enumerable.Range(0, long_ + 2).Select((k) => At(curb, t + k - 1)).ToList();
                        var clear = new List<Vec2> { At(lane, t - 1), At(lane, t + long_) };
                        clear.AddRange(car.Select((_, k) => At(inner, t + k)));
                        if (!car.All((p) => c.Free(p.X, p.Y) && c.GroundAt(p.X, p.Y) == Road)) continue;
                        if (kerb.Any((p) => c.GroundAt(p.X, p.Y) == Road)) continue;
                        // Where houses stand on the street, never across a door or a window.
                        if (kerb.Any((p) => c.Get(p.X, p.Y) == TileKinds.Door || c.Get(p.X, p.Y) == TileKinds.Window)) continue;
                        if (!clear.All((p) => c.Get(p.X, p.Y) == TileKinds.Floor)) continue;
                        // Its nose (the anchor) the way it faces: on the far end of a car facing east or south.
                        if (facing == 1 || facing == 2) car.Reverse();
                        PutObject(c, car, TileKinds.Low, DecorKinds.CarFront, facing);
                        t += long_ + rng.Int(3);
                    }
                }
            }
        }

        /// <summary>Street trees, planters or bins (the same all along one side) at an even spacing along the sidewalks of a block.</summary>
        private static void Sidewalk(Canvas c, Rect block, bool[] roads, int every, string kind)
        {
            for (var s = 0; s < 4; s++)
            {
                // roads[s] past the end is undefined in TypeScript: no road.
                if (!(s < roads.Length && roads[s])) continue;
                var n = SideLen(block, s);
                var what = new[] { DecorKinds.StreetTree, DecorKinds.Planter, DecorKinds.StreetTree, DecorKinds.Bin }[Mix(block.X + s, block.Y) % 4];
                var first = 2 + (int)Math.Floor(((n - 5) % every) / 2.0);
                for (var u = first; u <= n - 3; u += every)
                {
                    var p = LocalPt(block, s, u, 0);
                    var q = LocalPt(block, s, u, 1);
                    // Never in front of a door or a window (a way in), or right against a facade's opening.
                    if (c.Get(q.X, q.Y) == TileKinds.Door || c.Get(q.X, q.Y) == TileKinds.Window || (c.Get(q.X, q.Y) == TileKinds.Floor && c.Keep[c.I(q.X, q.Y)])) continue;
                    Put(c, p, kind, new Look(what, s));
                }
            }
        }
    }
}
