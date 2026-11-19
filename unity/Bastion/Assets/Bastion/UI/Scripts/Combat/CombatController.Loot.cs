using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The marks over the special loot boxes (lootMarks.ts; mapView.ts markLoot, docs/FASE8.md §B): the icon of what is in
    /// each on a dark plate ringed in its colour, hung over the box at the map's scale. One shows once its box has been in
    /// sight (the fog's explored, in a siege too: Arturo, 31/10 #5), with a soldier of the squad within four tiles (Arturo,
    /// 22/10: not across the map), and not on a storey above the one in view; once the box is opened it goes.
    /// </summary>
    public sealed partial class CombatController
    {
        /// <summary>How near a soldier of the squad must be (mapView.ts LOOT_MARK_REACH, tiles).</summary>
        const float LootMarkReach = 4;

        /// <summary>A mark's width (lootMarks.ts SIZE, tiles), and how high over its floor it hangs (the box's top and 0.95 more).</summary>
        const float LootMarkSize = 0.52f;
        const float LootMarkHeight = 1.45f;

        /// <summary>Each kind's icon and ring (lootMarks.ts LOOKS): weapons and ammunition white (Ana, 7/11 #6: no yellow, no amber), medicines green, food brass.</summary>
        static readonly Dictionary<string, (string icon, string ring)> LootLooks = new Dictionary<string, (string, string)>
        {
            ["lootWeapons"] = ("weapon:rifle", "#ffffff"),
            ["lootMedical"] = ("firstAid", "#8fd05a"),
            ["lootAmmo"] = ("stat:ammo", "#ffffff"),
            ["lootFood"] = ("store:food", "#d9a464"),
        };

        /// <summary>
        /// The icon over a thing to act on (Arturo, 5/11 #12, and 7/11 for the rest): in the bone of the lines where a click
        /// does it, greyed where it can't from here; how high over its floor (a body lies on the ground, a boot is a car's
        /// height, a box, a door or the boards LootMarkHeight).
        /// </summary>
        const string SearchLit = "#f3ecdc";
        const string SearchFar = "#7d868c";
        static readonly Color SearchLitColor = Ui.Hex(SearchLit, Color.white);
        static readonly Color SearchFarColor = Ui.Hex(SearchFar, Color.grey);
        const float SearchBodyHeight = 0.75f;
        const float SearchBootHeight = 1.25f;
        /// <summary>How far over the thing's top its icon hangs (metres; SearchOutline.TopOf).</summary>
        const float SearchOverTop = 0.45f;

        /// <summary>The bodies and boots under the pointer or within Registrar's reach, each lit or not (ShowSearchMarks).</summary>
        List<(Pos at, bool lit)> searchMarks = new List<(Pos, bool)>();
        string searchKey = "";
        /// <summary>What ShowWay's pass puts there (its branches and ShowArmed), shown as it ends.</summary>
        List<(Pos at, bool lit)> searchNext;

        static bool IsBodyOrBoot(string spot) => BodyLike(spot) || spot == Looting.CarBoot || spot == Looting.PoliceBoot;

        /// <summary>
        /// A body on the floor, whose tile is walked on: one of the dead's, or a police station's dead officer (G19; Producto,
        /// 18/11, U15: «como los cuerpos de zombi»), marked only where a click searches it (U9).
        /// </summary>
        static bool BodyLike(string spot) => spot == Looting.Body || spot == SearchPieces.CorpseOfficer;

        /// <summary>
        /// Whether `p`, on a storey under `level` (the one in view), is inside a building that has a storey over it (Interior):
        /// its mark would float over the floor in view, where nothing is (Producto, 18/11, U15: «una marca flotando sobre un
        /// suelo donde no hay nada confunde»). In the street, under the sky, never: the cars and bodies there show.
        /// </summary>
        static bool Covered(GameState s, Pos p, int level) => p.Z < level && Interior(s, p);

        /// <summary>A piece searched one by one (G19: a police station's, the medical furniture anywhere; the dead officer too).</summary>
        static bool IsPiece(string spot) => spot != null && SearchPieces.All.Contains(spot);

        /// <summary>Its name with its article, as Lore has it («el armero», «las taquillas»); `fallback` with none.</summary>
        static string SpotName(string spot, string fallback = "la caja") => spot != null && Lore.SearchSpots.TryGetValue(spot, out var s) ? s.Name : fallback;

        /// <summary>
        /// What a click does to `spot`, in words, Lore's verb (SearchSpot.Verb; Arturo, 18/11, cuadro 66): «abrir» what has a
        /// door or a lid (a box, a boot, a locker), «registrar» what is open (a rack, a shelf, a cart, a body).
        /// </summary>
        static string SearchVerb(string spot) => (spot != null && Lore.SearchSpots.TryGetValue(spot, out var s) ? s.Verb : "Abrir").ToLowerInvariant();

        /// <summary>
        /// Whether a click on `tile` searches it now: within `u`'s hands (HandTiles) and nothing stops it. A body is one only
        /// then; elsewhere its tile is a tile like any other (Arturo, 18/11, U9: «solo cuando estás al lado»).
        /// </summary>
        bool SearchesAt(Unit u, Pos tile) => HandTiles(AbilityIds.Search).Any(t => Same(t, tile)) && Abilities.AbilityBlocker(State, u, AbilityIds.Search) == null;

        /// <summary>
        /// One of the squad fallen on `tile` whose things still lie there (U12, Arturo 18/11: «registrar el cuerpo de mis
        /// aliados caídos y recuperar lo que llevaban»; RunPack.Fallen leaves them there, From it): searched as a body is,
        /// the pack opened on them. Null: nobody, or nothing left.
        /// </summary>
        static Unit FallenWithGear(GameState s, Pos tile)
        {
            if (s?.FloorGear == null || tile == null) return null;
            foreach (var d in s.FloorGear)
                if (d.From != null && Bastion.Engine.Grid.SamePos(d.Pos, tile) && UnitById(s, d.From) is Unit u && !u.Alive && !u.Evacuated && u.Team == Teams.Xcom) return u;
            return null;
        }

        /// <summary>Whether `tile` is La llave's armoury door, locked, and seen (U22): the fog's explored, where there is fog.</summary>
        bool LockedSeen(Pos tile)
        {
            if (State?.Mission.Locked == null || tile == null) return false;
            if (Bastion.Engine.Grid.KindAt(State, tile) != TileKinds.Door || !Looting.LockedAt(State, tile)) return false;
            var fog = Bastion.Client.Map.Fog.Current;
            return fog == null || fog.Explored(Bastion.Engine.Grid.NodeOf(State, tile));
        }

        /// <summary>La llave's key's icon (HudIcons: Synty's map key, as its stage's).</summary>
        internal const string KeyIcon = "key";

        /// <summary>Whether La llave's key lies on `tile` (U22): its mark there is the key, not the rucksack.</summary>
        static bool KeyLiesAt(GameState v, Pos tile) => v.Mission.KeyArmory == true && (v.FloorGear ?? new List<DroppedGear>()).Any(d => d.Gear.Kind == GearKinds.Key && Bastion.Engine.Grid.SamePos(d.Pos, tile));

        /// <summary>Mordido's antiviral's icon (HudIcons: Dark Fantasy's flask, till Arte's picture).</summary>
        internal const string AntiviralIcon = "antiviral";

        /// <summary>Whether Mordido's antiviral lies on `tile` (U23): its mark there is the flask.</summary>
        static bool AntiviralLiesAt(GameState v, Pos tile) => v.Mission.Bitten == true && (v.FloorGear ?? new List<DroppedGear>()).Any(d => d.Gear.Kind == GearKinds.Antiviral && Bastion.Engine.Grid.SamePos(d.Pos, tile));

        /// <summary>Whether `u` takes what a fallen comrade left on `tile` now: from beside its body (RunPack.InReach, G16) and its figure there.</summary>
        bool TakesFromFallen(Unit u, Pos tile) => (State.FloorGear ?? new List<DroppedGear>()).Any(d => d.From != null && Same(d.Pos, tile) && RunPack.InReach(State, u, d)) && StandsWhereSeen(u);

        /// <summary>Whether `u`'s figure stands where the game has it: not still walking there (what is seen goes behind the game while it plays).</summary>
        bool StandsWhereSeen(Unit u) => UnitById(Shown, u.Id) is Unit seen && Same(seen.Pos, u.Pos);

        /// <summary>
        /// Shift held (U11, Arturo 18/11: «al mantener el shift pulsado, se resalte todo lo looteable»): everything to search
        /// the squad has seen, outlined with its rucksack, in place of what the pointer or Registrar marks (ShowWay). Not while
        /// an action is watched, when the marks hide.
        /// </summary>
        bool lootAll;

        /// <summary>With the finger, the rucksack's button on (OnSearchAll): as Mayús held, till another tap.</summary>
        bool searchTouch;

        /// <summary>From above (M), the narrowest a mark is drawn (pixels), and the line round the things (Arturo, 18/11, cuadro 55).</summary>
        const float MapMarkPixels = 24;
        const float MapOutline = 3.5f;

        /// <summary>Shift pressed or let go (Update): the marks made again in the same frame.</summary>
        void LootAllKey()
        {
            var held = Bastion.Client.GameInput.Touch ? searchTouch : Bastion.Client.GameInput.HasKeys && Bastion.Client.GameInput.Shift && !Bastion.Client.GameInput.SystemHeld;
            var on = held && !Film && !(Rig != null && Rig.Away);
            if (on == lootAll) return;
            lootAll = on;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ShowWay();
            // What the press costs, once: the marks and their lines made (a car's copy each).
            if (on) Debug.Log($"[mayús] {searchMarks.Count} marcas en {watch.Elapsed.TotalMilliseconds:0.0} ms");
        }

        /// <summary>
        /// Everything to search the squad has seen (U11): the bodies, the boots, the boxes and the pieces (G19) not searched yet
        /// (Looting.SearchSpot), explored (the fog's, as the boxes' marks) and not inside a building not found (FoundInside),
        /// on the storeys up to the one in view, a lower one's only where no storey is over it (Covered); lit what the picked
        /// one can search now from where it stands. Doors and boards are not loot.
        /// </summary>
        List<(Pos at, bool lit)> AllToSearch()
        {
            var s = State;
            var list = new List<(Pos, bool)>();
            if (s == null || !HasSpots) return list;
            var u = Unit();
            var can = u != null && CanAct() && Commands(u) && StandsWhereSeen(u);
            var fog = Bastion.Client.Map.Fog.Current;
            var found = FoundInside();
            var area = s.Width * s.Height;
            var top = Mathf.Min(Level, s.Levels?.Count ?? 0);
            var taken = new HashSet<int>();
            void Add(Pos p)
            {
                var node = Bastion.Engine.Grid.NodeOf(s, p);
                if (!taken.Add(node) || Looting.WasSearched(s, p) || Covered(s, p, top)) return;
                if (fog != null && !fog.Explored(node)) return;
                if (node < found.Length && !found[node]) return;
                list.Add((p, can && SearchesAt(u, p)));
            }
            // The bodies by their units; the rest only where something stands (Low or High), not every tile through
            // SearchSpot (it looks for a body among all the units at each: 12 ms over a city, 7/11).
            foreach (var a in s.Units)
                if (!a.Alive && a.Pos.Z <= top && Looting.SearchSpot(s, a.Pos) == Looting.Body) Add(a.Pos);
            // The pieces by their nodes (G19, MissionState.Pieces): the dead officer lies on the floor, which the tiles' loop
            // below passes over; empty from the start, they aren't there; searched, Add leaves them.
            foreach (var node in s.Mission.Pieces ?? new List<int>())
            {
                var p = Bastion.Engine.Grid.PosOf(s, node);
                if (p.Z <= top && Looting.SearchSpot(s, p) != null) Add(p);
            }
            // The squad's fallen with their things still there (U12), lit where a click opens the pack on them.
            foreach (var d in s.FloorGear ?? new List<DroppedGear>())
                if (d.From != null && d.Pos.Z <= top && FallenWithGear(s, d.Pos) != null)
                {
                    var node = Bastion.Engine.Grid.NodeOf(s, d.Pos);
                    if (taken.Contains(node) || Covered(s, d.Pos, top) || (fog != null && !fog.Explored(node)) || (node < found.Length && !found[node])) continue;
                    taken.Add(node);
                    list.Add((d.Pos, can && TakesFromFallen(u, d.Pos)));
                }
            // La llave's key or Mordido's antiviral on the floor (U22, U23), with the rest: its own mark, lit with the picked one
            // on it (taken from there).
            if (MissionGearOnFloor(s) is Pos key && key.Z <= top && !Covered(s, key, top))
            {
                var node = Bastion.Engine.Grid.NodeOf(s, key);
                if (!taken.Contains(node) && (fog == null || fog.Explored(node)))
                {
                    taken.Add(node);
                    list.Add((key, can && Same(u.Pos, key)));
                }
            }
            for (var z = 0; z <= top; z++)
                for (var i = 0; i < area; i++)
                {
                    var p = new Pos { X = i % s.Width, Y = i / s.Width, Z = z };
                    var kind = Bastion.Engine.Grid.KindAt(s, p);
                    if (kind != TileKinds.Low && kind != TileKinds.High) continue;
                    var spot = Looting.SearchSpot(s, p);
                    if (spot != null && spot != Looting.Body) Add(p);
                }
            return list;
        }

        /// <summary>The rooms of RoomsToSearch as last made (Mayús held).</summary>
        List<(List<Pos> tiles, bool lit)> roomMarks = new List<(List<Pos>, bool)>();

        /// <summary>
        /// The rooms still to search the squad has found (U18): inside a building found (FoundInside), on the storeys up to
        /// the one in view and not under one (Covered), not searched (Looting.RoomSearched) nor searched piece by piece
        /// (LootRoom.Pieces); lit the one the picked soldier stands in where Registrar la habitación can go now. Each room
        /// once, by walking the floors inside (Looting.RoomAt).
        /// </summary>
        List<(List<Pos> tiles, bool lit)> RoomsToSearch()
        {
            var s = State;
            var list = new List<(List<Pos>, bool)>();
            if (s == null || s.Storeys == null) return list;
            var u = Unit();
            var can = u != null && CanAct() && Commands(u) && StandsWhereSeen(u) && Abilities.AbilityBlocker(s, u, AbilityIds.SearchRoom) == null;
            var mine = can ? Looting.RoomAt(s, u.Pos) : null;
            var found = FoundInside();
            var area = s.Width * s.Height;
            var top = Mathf.Min(Level, s.Levels?.Count ?? 0);
            var seen = new HashSet<int>();
            for (var z = 0; z <= top; z++)
                for (var i = 0; i < area; i++)
                {
                    if (s.Storeys[i] <= z) continue;
                    var p = new Pos { X = i % s.Width, Y = i / s.Width, Z = z };
                    var node = Bastion.Engine.Grid.NodeOf(s, p);
                    if (seen.Contains(node) || Covered(s, p, top)) continue;
                    var room = Looting.RoomAt(s, p);
                    if (room == null) continue;
                    foreach (var t in room.Tiles) seen.Add(Bastion.Engine.Grid.NodeOf(s, t));
                    if (room.Pieces || Looting.RoomSearched(s, room)) continue;
                    // Found: a room is, as a whole, once one of its tiles is (FoundInside, Buildings.Veil).
                    if (!room.Tiles.Any(t => Bastion.Engine.Grid.NodeOf(s, t) is var n && n < found.Length && found[n])) continue;
                    list.Add((room.Tiles, mine != null && mine.Id == room.Id));
                }
            return list;
        }

        /// <summary>
        /// The rooms still to search with Mayús held (or none), their floor washed and their border drawn (MissionMarks.
        /// ShowRooms; Arturo, cuadro 73: the floor, not an icon), over the roofs from above.
        /// </summary>
        void ShowRooms(bool on)
        {
            var watch = on ? System.Diagnostics.Stopwatch.StartNew() : null;
            roomMarks = on ? RoomsToSearch() : new List<(List<Pos>, bool)>();
            if (on) Debug.Log($"[mayús] {roomMarks.Count} habitaciones por registrar en {watch.Elapsed.TotalMilliseconds:0.0} ms");
            Marks?.ShowRooms(State, roomMarks, Overhead);
        }

        /// <summary>
        /// What the picked one searches with a click now from where it stands, lit, with no pointer (U14, Arturo 18/11, cuadro
        /// 64: «todo lo que tiene al lado»): the bodies, boots, boxes and furniture within Registrar's reach (Looting.
        /// SearchableTiles: beside, two off with Gancho) while nothing stops it, and a fallen comrade's things beside, taken for
        /// free (G16) with no actions left too (Producto, 18/11). Doors and boards only under the pointer. Only while BesideQuiet,
        /// on the storeys up to the one in view, as Mayús (Covered).
        /// </summary>
        List<(Pos at, bool lit)> BesideToSearch()
        {
            var list = new List<(Pos at, bool lit)>();
            var u = Unit();
            if (u == null || !BesideQuiet(u)) return list;
            if (HasSpots && Abilities.AbilityBlocker(State, u, AbilityIds.Search) == null)
                foreach (var t in Looting.SearchableTiles(State, u))
                    if (t.Z <= Level && !Covered(State, t, Level)) list.Add((t, true));
            foreach (var d in State.FloorGear ?? new List<DroppedGear>())
                if (d.From != null && d.Pos.Z <= Level && !list.Any(m => Same(m.at, d.Pos)) && FallenWithGear(State, d.Pos) != null && RunPack.InReach(State, u, d))
                    list.Add((d.Pos, true));
            return list;
        }

        /// <summary>
        /// Whether what `u` has beside it is marked now (BesideToSearch), as its walking range is (Refresh: Producto, 18/11, «a
        /// la vez que el área de andar»): ours to command, in our turn with the command (not planning) and nobody else of ours
        /// to finish first (WaitingFor), moving (not aiming, throwing, by hand or getting off La Lata), its order on its way
        /// or its events still seen (sent, Busy) no more and its figure where the game has it; not over a shoulder, nor from
        /// above (M), where a click brings the camera down and searches nothing (Producto, 18/11; with Mayús, as there). The
        /// partner's actions and the dead's answers to its own don't take them away, as they don't take the range.
        /// </summary>
        bool BesideQuiet(Unit u) =>
            State != null && mode == Mode.Move && !AlightArmed() && CanAct() && Commands(u) && HasCommand() && WaitingFor(u.Id) == null
            && u.Id != sent && !Busy(u.Id) && StandsWhereSeen(u) && !Film && !(Rig != null && (Rig.Away || Rig.Overhead));

        /// <summary>The soldier whose things beside are marked (BesideTick); null, none.</summary>
        string besideFor;

        /// <summary>
        /// Each frame (Update): the marks beside come as the figure gets there and its action ends, and go as it starts walking
        /// or acting, or the map is seen from above; made again only then (ShowWay), as Mayús's.
        /// </summary>
        void BesideTick()
        {
            var u = Unit();
            var now = u != null && BesideQuiet(u) ? u.Id : null;
            if (now == besideFor) return;
            besideFor = now;
            ShowWay();
        }

        /// <summary>
        /// What a click on a thing does, as its icon (Arturo, 7/11): the boards off (the crowbar), a door opened or shut
        /// (LineIcon's), else searched (the rucksack).
        /// </summary>
        static string MarkIcon(GameState v, Pos at)
        {
            if (KeyLiesAt(v, at)) return KeyIcon;
            if (AntiviralLiesAt(v, at)) return AntiviralIcon;
            if (Abilities.Boards(v, at) != null) return AbilityIds.Pry;
            var kind = Bastion.Engine.Grid.KindAt(v, at);
            if (kind == TileKinds.Door) return AbilityIds.OpenDoor;
            if (kind == TileKinds.DoorOpen) return AbilityIds.CloseDoor;
            return AbilityIds.Search;
        }

        /// <summary>
        /// A thing to act on shown as itself (#12 of 5/11; boxes, doors, boards and furniture since 7/11): its action's icon
        /// over each and, Arte's, the thing outlined; null or empty, none. The tags over the map again only when they change.
        /// </summary>
        void ShowSearchMarks(List<(Pos at, bool lit)> marks)
        {
            marks ??= new List<(Pos, bool)>();
            var key = string.Join("|", marks.Select(m => $"{m.at.X},{m.at.Y},{m.at.Z}:{m.lit}"));
            if (key == searchKey) return;
            searchKey = key;
            searchMarks = marks;
            if (marks.Count > 0) Debug.Log($"[registro] {key}");
            // The body or the whole car outlined in the same colours (Arte's SearchOutline; empty, none).
            if (State != null) Bastion.Client.Units.SearchOutline.Show(State, marks, SearchLitColor, SearchFarColor);
            if (Hud != null && State != null) Hud.SetWorldTags(WorldTags(Shown));
        }

        /// <summary>The rucksacks of ShowSearchMarks, added to the tags over the map.</summary>
        void SearchMarks(GameState v, List<WorldTagView> list)
        {
            foreach (var (at, lit) in searchMarks)
            {
                // Over the thing's top (Arte's SearchOutline.TopOf: a box's lid, a door's leaf, the furniture against a
                // window), else at the height each kind had.
                var spot = Looting.SearchSpot(v, at);
                var high = BodyLike(spot) || FallenWithGear(v, at) != null ? SearchBodyHeight : spot == Looting.CarBoot || spot == Looting.PoliceBoot ? SearchBootHeight : LootMarkHeight;
                var world = Board.TileToWorld(v, at, high * Board.Tile);
                if (Bastion.Client.Units.SearchOutline.TopOf(v, at) is float top) world.y = top + SearchOverTop;
                list.Add(new WorldTagView
                {
                    Kind = "loot", Icon = MarkIcon(v, at), Ring = lit ? SearchLit : SearchFar, Size = LootMarkSize * Board.Tile,
                    World = world,
                });
            }
        }

        /// <summary>The marks of the boxes to show now, added to the tags over the map.</summary>
        void LootMarks(GameState v, List<WorldTagView> list)
        {
            var squad = v.Units.Where(u => u.Alive && u.Team == Teams.Xcom && !u.Evacuated).ToList();
            if (squad.Count == 0) return;
            var fog = Bastion.Client.Map.Fog.Current;
            var all = fog == null;
            var area = v.Width * v.Height;
            // Only the tiles within reach of one of them, the same as each tile's test was (SearchSpot looks for a body
            // among all the units: tile by tile over the whole map, 12 ms a refresh in a city with eighty of the dead; Red
            // and Arte, 7/11).
            var near = new bool[area];
            var r = Mathf.CeilToInt(LootMarkReach);
            foreach (var u in squad)
                for (var y = Mathf.Max(0, u.Pos.Y - r); y <= Mathf.Min(v.Height - 1, u.Pos.Y + r); y++)
                    for (var x = Mathf.Max(0, u.Pos.X - r); x <= Mathf.Min(v.Width - 1, u.Pos.X + r); x++)
                        if (Mathf.Sqrt((u.Pos.X - x) * (u.Pos.X - x) + (u.Pos.Y - y) * (u.Pos.Y - y)) <= LootMarkReach) near[y * v.Width + x] = true;
            var top = Mathf.Min(Level, v.Levels?.Count ?? 0);
            for (var z = 0; z <= top; z++)
                for (var i = 0; i < area; i++)
                {
                    if (!near[i]) continue;
                    var p = new Pos { X = i % v.Width, Y = i / v.Width, Z = z };
                    var kind = Looting.SearchSpot(v, p);
                    if (kind == null || !LootLooks.TryGetValue(kind, out var look) || Looting.WasSearched(v, p)) continue;
                    if (!all && !fog.Explored(z * area + i)) continue;
                    list.Add(new WorldTagView
                    {
                        Kind = "loot", Icon = look.icon, Ring = look.ring, Size = LootMarkSize * Board.Tile,
                        World = Board.TileToWorld(v, p, LootMarkHeight * Board.Tile),
                    });
                }
        }
    }
}
