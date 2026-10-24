#nullable enable
// loot.ts: Searching (docs/FASE2.md, «Saqueo»): which furniture can be searched, what
// turns up in it and what the squad brings home. The rolls happen on the host
// (Sim.search); the `searched` event carries what was found.
// The class is Looting, not Loot: that is the type of a thing found (Unit.cs).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>What kind of place a searchable piece of furniture belongs to: it sets what turns up in it.</summary>
    public static class Spots
    {
        public const string Home = "home";
        public const string Office = "office";
        public const string Shop = "shop";
        public const string Store = "store";
        public const string Trunk = "trunk";
        public const string Glovebox = "glovebox";

        public static readonly IReadOnlyList<string> All = new[] { Home, Office, Shop, Store, Trunk, Glovebox };
    }

    /// <summary>A room (docs/FASE8.md §B): its floor tiles on one level, the kind of place its furniture makes it, and its id (its first node).</summary>
    /// <remarks>loot.ts's `Room`: LootRoom here, Bastion.Net.Room being the game room.</remarks>
    public sealed class LootRoom
    {
        public int Id { get; set; }
        public List<Pos> Tiles { get; set; } = new List<Pos>();
        public string Spot { get; set; } = ""; // Spots
    }

    /// <summary>A weapon with its condition and mods, as it travels in a backpack.</summary>
    public sealed class CarriedWeapon
    {
        /// <summary>LootTotals.returned's only: the Armería's id of a weapon coming back (`CarriedWeapon &amp; { id: string }`).</summary>
        public string? Id { get; set; }
        public string Kind { get; set; } = ""; // FoundWeaponIds
        public double? Condition { get; set; }
        public List<string>? Mods { get; set; } // WeaponModIds
    }

    public sealed class LootTotals
    {
        public int Supplies { get; set; }
        public List<string> Items { get; set; } = new List<string>(); // ItemIds
        public int Medicines { get; set; }
        public int Food { get; set; }
        public int Parts { get; set; }
        /// <summary>Weapons found: new to the Armería.</summary>
        public List<string> Weapons { get; set; } = new List<string>(); // FoundWeaponIds
        /// <summary>The same, as found: their condition and mods (docs/FASE9.md §5).</summary>
        public List<CarriedWeapon>? Arms { get; set; }
        /// <summary>The Armería's own weapons coming back in a backpack (Loot.weaponId), swapped for a find (docs/FASE8.md §C).</summary>
        public List<CarriedWeapon>? Returned { get; set; }
        /// <summary>Loose improvements found, for the Armería (docs/FASE9.md §5).</summary>
        public List<string>? Mods { get; set; } // WeaponModIds
    }

    public static class Looting
    {
        /// <summary>Searchable decor (Lore's SEARCH_SPOTS): furniture, crates and both halves of a car.</summary>
        private static readonly IReadOnlyDictionary<string, string> SpotsOf = new Dictionary<string, string>
        {
            [DecorKinds.Wardrobe] = Spots.Home,
            [DecorKinds.Bed] = Spots.Home,
            [DecorKinds.Table] = Spots.Home,
            [DecorKinds.Desk] = Spots.Office,
            [DecorKinds.Cabinet] = Spots.Office,
            [DecorKinds.Shelf] = Spots.Shop,
            [DecorKinds.Counter] = Spots.Shop,
            [DecorKinds.Crates] = Spots.Store,
            [DecorKinds.Rack] = Spots.Store,
            [DecorKinds.CarRear] = Spots.Trunk,
            [DecorKinds.CarFront] = Spots.Glovebox,
        };

        public static readonly IReadOnlyList<string> Searchable = new[]
        {
            DecorKinds.Wardrobe, DecorKinds.Bed, DecorKinds.Table, DecorKinds.Desk, DecorKinds.Cabinet, DecorKinds.Shelf,
            DecorKinds.Counter, DecorKinds.Crates, DecorKinds.Rack, DecorKinds.CarRear, DecorKinds.CarFront,
        };

        private sealed class LootTable
        {
            /// <summary>Chances out of 100, in this order; whatever is left turns up nothing.</summary>
            public int Supplies;
            public int Item;
            public int Medicines;
            /// <summary>Rations: pantries, shops and stores most (docs/FASE4.md).</summary>
            public int Food;
            /// <summary>Parts: stores, car boots and offices.</summary>
            public int Parts;
            /// <summary>A good weapon (FOUND_WEAPONS): rare, in boots, stores and wardrobes.</summary>
            public int Weapon;
            /// <summary>An improvement for a weapon (docs/FASE9.md §5): stores and car boots most.</summary>
            public int Mod;
            /// <summary>A magazine (docs/FASE7.md), for a searcher who counts them: stores and car boots most.</summary>
            public int Mags;
            /// <summary>Accessories that turn up there (only those the difficulty opens: `level` ≤ difficulty + 2).</summary>
            public IReadOnlyList<string> Items = new string[0];
        }

        private static readonly IReadOnlyDictionary<string, LootTable> Tables = new Dictionary<string, LootTable>
        {
            // Special rounds aren't found any more: the weapons' mods took their place (docs/FASE9.md §5).
            [Spots.Home] = new LootTable { Supplies = 30, Item = 8, Medicines = 3, Food = 20, Parts = 0, Weapon = 2, Mod = 0, Mags = 4, Items = new[] { ItemIds.FirstAid, ItemIds.SmokeGrenade, ItemIds.NanoVest, ItemIds.MindShield } },
            [Spots.Office] = new LootTable { Supplies = 30, Item = 8, Medicines = 2, Food = 4, Parts = 15, Weapon = 0, Mod = 1, Mags = 3, Items = new[] { ItemIds.FirstAid, ItemIds.Flashbang, ItemIds.MindShield } },
            [Spots.Shop] = new LootTable { Supplies = 35, Item = 8, Medicines = 12, Food = 25, Parts = 2, Weapon = 0, Mod = 1, Mags = 6, Items = new[] { ItemIds.FirstAid, ItemIds.SmokeGrenade, ItemIds.FragGrenade } },
            [Spots.Store] = new LootTable { Supplies = 30, Item = 14, Medicines = 2, Food = 15, Parts = 15, Weapon = 3, Mod = 4, Mags = 10, Items = new[] { ItemIds.FragGrenade, ItemIds.SmokeGrenade, ItemIds.Flashbang } },
            [Spots.Trunk] = new LootTable { Supplies = 30, Item = 14, Medicines = 8, Food = 5, Parts = 15, Weapon = 4, Mod = 3, Mags = 10, Items = new[] { ItemIds.FragGrenade, ItemIds.FirstAid, ItemIds.NanoVest } },
            [Spots.Glovebox] = new LootTable { Supplies = 25, Item = 5, Medicines = 12, Food = 3, Parts = 5, Weapon = 1, Mod = 1, Mags = 8, Items = new[] { ItemIds.FirstAid } },
        };

        /// <summary>Rations and parts found at once.</summary>
        private static readonly IReadOnlyList<int> FoodFind = new[] { 2, 4 };
        private static readonly IReadOnlyList<int> PartsFind = new[] { 1, 2 };

        /// <summary>
        /// Which weapon turns up, by FOUND_WEAPONS weight, as it is found (docs/FASE9.md
        /// §5): somewhat worn, and a good one with its improvement mounted.
        /// </summary>
        private static Loot FoundWeapon(Rng rng)
        {
            // Object.values(FOUND_WEAPONS): in the order of its keys, FoundWeaponIds.All's.
            var all = FoundWeaponIds.All.Select((id) => Content.FoundWeapons[id]).ToList();
            var roll = rng.Int(all.Sum((w) => w.Weight));
            var def = all.First((w) => (roll -= w.Weight) < 0);
            var loot = new Loot { Weapon = def.Id, Condition = rng.Range(FoundCondition[0], FoundCondition[1]) };
            if (def.Mod != null) loot.Mods = new List<string> { def.Mod };
            return loot;
        }

        /// <summary>The condition a weapon is found in.</summary>
        private static readonly IReadOnlyList<int> FoundCondition = new[] { 40, 90 };

        /// <summary>Which improvement turns up.</summary>
        private static Loot FoundMod(Rng rng)
        {
            // Object.keys(WEAPON_MODS): WeaponModIds.All's order.
            return new Loot { Mod = rng.Pick(WeaponModIds.All) };
        }

        /// <summary>Suministros found at once: 5–14, and 2 more per difficulty step (up to 20); half as much again at night.</summary>
        private static int Supplies(int difficulty, Rng rng, bool night)
        {
            var found = rng.Range(5, 14) + 2 * (difficulty - 1);
            return night ? (int)Js.Round(found * Content.Night.Supplies) : found;
        }

        /// <summary>
        /// What a search of `decor` turns up at that difficulty (1–4), or null for
        /// nothing; more suministros at night. A magazine only for a searcher who
        /// counts them (`mags`); for anyone else that share turns up nothing, so the
        /// other rolls come out as before.
        /// </summary>
        public static Loot? RollLoot(string decor, int difficulty, Rng rng, bool night = false, bool mags = false)
        {
            return SpotsOf.TryGetValue(decor, out var spot) ? RollSpot(spot, difficulty, rng, night, mags) : null;
        }

        /// <summary>What searching a place of kind `spot` turns up: rollLoot by the kind of place, not the piece of furniture.</summary>
        private static Loot? RollSpot(string spot, int difficulty, Rng rng, bool night, bool mags)
        {
            var d = Math.Max(1, Math.Min(4, difficulty));
            var t = Tables[spot];
            var roll = rng.Int(100);
            if (roll < t.Supplies) return new Loot { Supplies = Supplies(d, rng, night) };
            if (roll < t.Supplies + t.Item)
            {
                var pool = t.Items.Where((id) => Content.Items[id].Level <= d + 2).ToList();
                return pool.Count > 0 ? new Loot { Item = rng.Pick(pool) } : new Loot { Supplies = Supplies(d, rng, night) };
            }
            var edge = t.Supplies + t.Item + t.Medicines;
            if (roll < edge) return new Loot { Medicines = 1 };
            if (roll < (edge += t.Food)) return new Loot { Food = rng.Range(FoodFind[0], FoodFind[1]) };
            if (roll < (edge += t.Parts)) return new Loot { Parts = rng.Range(PartsFind[0], PartsFind[1]) };
            if (roll < (edge += t.Weapon)) return FoundWeapon(rng);
            if (roll < (edge += t.Mod)) return FoundMod(rng);
            if (roll < (edge += t.Mags)) return mags ? new Loot { Mags = 1 } : null;
            return null;
        }

        // ------------------------------------------------------------ the furniture

        // ------------------------------------------------------- the loot boxes

        /// <summary>The special boxes (docs/FASE8.md §B): each always holds what it is marked for.</summary>
        public static readonly IReadOnlyList<string> LootBoxes = new[] { DecorKinds.LootWeapons, DecorKinds.LootMedical, DecorKinds.LootAmmo, DecorKinds.LootFood };

        /// <summary>
        /// What opening a box of `kind` turns up: a good weapon, one or two
        /// medicines, two magazines (for a searcher who counts them; nothing for
        /// anyone else), or three to five rations.
        /// </summary>
        public static Loot? RollBox(string kind, Rng rng, bool mags)
        {
            switch (kind)
            {
                case DecorKinds.LootWeapons:
                    // A weapon, or now and then an improvement for one (docs/FASE9.md §5).
                    return rng.Int(10) < 3 ? FoundMod(rng) : FoundWeapon(rng);
                case DecorKinds.LootMedical:
                    return new Loot { Medicines = rng.Range(1, 2) };
                case DecorKinds.LootAmmo:
                    return mags ? new Loot { Mags = 2 } : null;
                case DecorKinds.LootFood:
                    return new Loot { Food = rng.Range(3, 5) };
                default:
                    return null;
            }
        }

        /// <summary>The decor layer of level `z`: the ground's, or a storey's (`s.levels?.[z - 1]?.decor`).</summary>
        private static List<int>? DecorLayer(GameState s, int z)
        {
            return z == 0 ? s.Decor : Grid.LevelOf(s, z - 1)?.Decor;
        }

        /// <summary>
        /// The loot box on `p`, opened or not; null if there is none or it was blown
        /// away. Furniture and cars are no longer searched one by one (docs/FASE8.md
        /// §B): a room is, as a whole (roomAt).
        /// </summary>
        public static string? SearchSpot(GameState s, Pos at)
        {
            var p = Grid.ToPos(at);
            var decor = DecorLayer(s, p.Z);
            if (decor == null || !Grid.InBounds(s, p.X, p.Y)) return null;
            var kind = Grid.KindAt(s, p);
            if (kind != TileKinds.Low && kind != TileKinds.High) return null;
            var i = Grid.TileIndex(s, p.X, p.Y);
            var look = Decor.DecorOf(i < decor.Count ? decor[i] : (int?)null);
            return look != null && LootBoxes.Contains(look.Kind) ? look.Kind : null;
        }

        // ------------------------------------------------------------- the rooms

        /// <summary>The ground's mark for the inside of a building (mapgen.ts INDOOR).</summary>
        private const int IndoorGround = 2;

        /// <summary>Whether place (x, y) on level `z` is inside a building: an indoor tile on the ground, or a storey with a floor over it.</summary>
        private static bool Interior(GameState s, int x, int y, int z)
        {
            return z == 0 ? Js.At(s.Ground, Grid.TileIndex(s, x, y), 0) == IndoorGround : Grid.SlabAt(s, x, y, z + 1);
        }

        /// <summary>Floor a room is made of: walkable, but not a doorway nor a window (they part one room from the next).</summary>
        private static bool RoomFloor(string? kind)
        {
            return Grid.IsWalkable(kind) && kind != TileKinds.Door && kind != TileKinds.DoorOpen && kind != TileKinds.Window && kind != TileKinds.WindowBroken;
        }

        /// <summary>
        /// The room place `at` stands in (docs/FASE8.md §B): the floor joined to it on
        /// its level, inside the building, without crossing a doorway or a window.
        /// Its kind of place is the one most of its furniture makes it (a home, an
        /// office, a shop, a store; a piece over several tiles counts once), a home
        /// when it has none. Null outdoors, on a
        /// roof, in a doorway, or on a hand-made map (they mark no inside).
        /// </summary>
        public static LootRoom? RoomAt(GameState s, Pos at)
        {
            var start = Grid.ToPos(at);
            if (!Grid.InBounds(s, start.X, start.Y) || !RoomFloor(Grid.KindAt(s, start)) || !Interior(s, start.X, start.Y, start.Z)) return null;
            var z = start.Z;
            var decor = DecorLayer(s, z);
            var seen = new HashSet<int> { Grid.TileIndex(s, start.X, start.Y) };
            var tiles = new List<Pos>();
            var furniture = new JsMap<int, string>();
            var queue = new Queue<Pos>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                tiles.Add(p);
                foreach (var d in Grid.Dirs4)
                {
                    var x = p.X + d.X;
                    var y = p.Y + d.Y;
                    if (!Grid.InBounds(s, x, y)) continue;
                    var i = Grid.TileIndex(s, x, y);
                    var q = new Pos { X = x, Y = y, Z = z };
                    var kind = Grid.KindAt(s, q);
                    if ((kind == TileKinds.Low || kind == TileKinds.High) && decor != null)
                    {
                        // An object over several tiles (a bed, a table) by its anchor: once, whichever of its tiles is beside the floor.
                        var look = Decor.ObjectAt(decor, s.Width, i);
                        if (look != null && SpotsOf.TryGetValue(look.Kind, out var spot)) furniture.Set(look.Anchor, spot);
                    }
                    if (seen.Contains(i) || !RoomFloor(kind) || !Interior(s, x, y, z)) continue;
                    seen.Add(i);
                    queue.Enqueue(q);
                }
            }
            var counts = new JsMap<string, int>();
            foreach (var spot in furniture.Values) counts.Set(spot, counts.Get(spot, 0) + 1);
            var entries = counts.ToList();
            // a[0].localeCompare(b[0]): the spots are lowercase ASCII words, which collate as their code units.
            Js.Sort(entries, (a, b) => b.Value - a.Value != 0 ? b.Value - a.Value : string.CompareOrdinal(a.Key, b.Key));
            var best = entries.Count > 0 ? entries[0].Key : Spots.Home;
            var id = tiles.Min((p) => Grid.NodeOf(s, p));
            return new LootRoom { Id = id, Tiles = tiles, Spot = best };
        }

        /// <summary>Whether the squad has already searched `room` (docs/FASE8.md §B: once for everyone).</summary>
        public static bool RoomSearched(GameState s, LootRoom room)
        {
            return (s.RoomsSearched ?? new List<int>()).Contains(room.Id);
        }

        /// <summary>Rations a stocked supermarket always gives (playtest 21/10, #19).</summary>
        private static readonly IReadOnlyList<int> MarketFood = new[] { 6, 10 };

        /// <summary>The stock of the supermarket `room` is, or null if it is no supermarket's (GameState.markets).</summary>
        /// <returns>`'full' | 'picked' | null` (MarketStocks).</returns>
        public static string? MarketStock(GameState s, LootRoom room)
        {
            var p = room.Tiles.Count > 0 ? room.Tiles[0] : null;
            if (p == null || p.Z != 0) return null;
            var m = (s.Markets ?? new List<MarketSite>()).Find((r) => p.X > r.X && p.Y > r.Y && p.X < r.X + r.W - 1 && p.Y < r.Y + r.H - 1);
            return m != null ? (m.Stock ?? MarketStocks.Picked) : null;
        }

        /// <summary>
        /// What searching `room` turns up at that difficulty: the furniture's tables, rolled once for the room. A
        /// supermarket with its shelves still stocked (`market` 'full') always gives food, 6 to 10 rations (Ana, 21/10:
        /// «siempre que estén las estanterías llenas de cosas se pueda lutear»); a picked one, a shop's roll.
        /// </summary>
        public static Loot? RollRoom(LootRoom room, int difficulty, Rng rng, bool night = false, bool mags = false, string? market = null)
        {
            if (market == MarketStocks.Full) return new Loot { Food = rng.Range(MarketFood[0], MarketFood[1]) };
            return RollSpot(market != null ? Spots.Shop : room.Spot, difficulty, rng, night, mags);
        }

        /// <summary>Room in a soldier's backpack: BACKPACK_SLOTS, one more with the Chapistería (docs/FASE8.md §C).</summary>
        public static int PackSlots(Unit u)
        {
            return u.PackSlots ?? Content.BackpackSlots;
        }

        public static bool WasSearched(GameState s, Pos p)
        {
            return (s.Searched ?? new List<int>()).Contains(Grid.NodeOf(s, Grid.ToPos(p)));
        }

        /// <summary>Furniture on `p` that is still there to search (for highlighting it anywhere on the map).</summary>
        public static bool IsSearchable(GameState s, Pos p)
        {
            return SearchSpot(s, p) != null && !WasSearched(s, p);
        }

        private static readonly HashSet<string> Solid = new HashSet<string> { TileKinds.Wall, TileKinds.Door, TileKinds.Window, TileKinds.WindowBroken };

        /// <summary>Whether a soldier on `from` reaches `p` with the hands: beside it, at its height, and not round a wall's corner.</summary>
        public static bool ReachesToSearch(GameState s, Pos from, Pos at)
        {
            var p = Grid.ToPos(at);
            var dx = p.X - from.X;
            var dy = p.Y - from.Y;
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1 || Grid.HeightAt(s, from) != Grid.HeightAt(s, p)) return false;
            if (dx == 0 || dy == 0) return true;
            bool IsSolid(int x, int y)
            {
                var kind = Grid.KindAt(s, new Pos { X = x, Y = y, Z = from.Z });
                return kind != null && Solid.Contains(kind);
            }
            return !IsSolid(from.X + dx, from.Y) || !IsSolid(from.X, from.Y + dy);
        }

        /// <summary>Furniture `u` can search right now from where it stands (any backpack room aside).</summary>
        public static List<Pos> SearchableTiles(GameState s, Unit u)
        {
            return Grid.Dirs8.Select((d) => new Pos { X = u.Pos.X + d.X, Y = u.Pos.Y + d.Y, Z = u.Pos.Z }).Where((p) => IsSearchable(s, p) && ReachesToSearch(s, u.Pos, p)).ToList();
        }

        // --------------------------------------------------------------- the haul

        public static LootTotals LootTotalsOf(IReadOnlyList<Loot> loot)
        {
            var out_ = new LootTotals();
            foreach (var l in loot)
            {
                out_.Supplies += l.Supplies ?? 0;
                if (!string.IsNullOrEmpty(l.Item)) out_.Items.Add(l.Item!);
                out_.Medicines += l.Medicines ?? 0;
                out_.Food += l.Food ?? 0;
                out_.Parts += l.Parts ?? 0;
                CarriedWeapon? carried = null;
                if (!string.IsNullOrEmpty(l.Weapon))
                {
                    carried = new CarriedWeapon { Kind = l.Weapon! };
                    if (l.Condition != null) carried.Condition = l.Condition;
                    if (l.Mods != null && l.Mods.Count > 0) carried.Mods = new List<string>(l.Mods);
                }
                if (carried != null && !string.IsNullOrEmpty(l.WeaponId))
                {
                    carried.Id = l.WeaponId;
                    (out_.Returned ??= new List<CarriedWeapon>()).Add(carried);
                }
                else if (carried != null)
                {
                    out_.Weapons.Add(carried.Kind);
                    (out_.Arms ??= new List<CarriedWeapon>()).Add(carried);
                }
                if (!string.IsNullOrEmpty(l.Mod)) (out_.Mods ??= new List<string>()).Add(l.Mod!);
            }
            return out_;
        }

        /// <summary>Whether what `u` carries comes home: it got out, or it is standing when the mission is won. A fallen soldier's is lost.</summary>
        public static bool BringsLootHome(GameState s, Unit u)
        {
            return Content.Templates[u.Template].Team == Teams.Xcom && (u.Evacuated || (u.Alive && s.Outcome == Outcomes.Victory));
        }

        /// <summary>What the squad brings back from the mission (docs/FASE2.md): only what comes home counts.</summary>
        public static LootTotals BroughtBack(GameState s)
        {
            // A vest put on out there comes home on the soldier, as if in the backpack; a belt thing too, if not dipped into.
            return LootTotalsOf(s.Units.Where((u) => BringsLootHome(s, u)).SelectMany((u) => (u.Backpack ?? new List<Loot>()).Concat((u.Worn ?? new List<string>()).Select((item) => new Loot { Item = item })).Concat(BeltedBack(u))).ToList());
        }

        /// <summary>`Object.entries(ITEMS[item].charges ?? {})`.</summary>
        private static IReadOnlyDictionary<string, int> UsesOf(string item)
        {
            return Content.Items[item].Charges ?? new Dictionary<string, int>();
        }

        /// <summary>
        /// What a fallen soldier leaves where it fell (Arturo, 18/10 #49): its
        /// backpack, its weapons (the Armería's, by id: brought home, they go back) and
        /// what on its belt is still whole (beltedBack's count, then its own gear).
        /// </summary>
        public static List<Loot> FallenGear(Unit u)
        {
            var out_ = new List<Loot>(u.Backpack ?? new List<Loot>());
            var arms = u.Arms;
            if (!string.IsNullOrEmpty(arms?.Primary))
            {
                var weapon = new Loot { Weapon = arms!.Primary };
                if (!string.IsNullOrEmpty(arms.PrimaryId)) weapon.WeaponId = arms.PrimaryId;
                if (arms.Condition != null) weapon.Condition = arms.Condition;
                if (arms.Mods != null && arms.Mods.Count > 0) weapon.Mods = new List<string>(arms.Mods);
                out_.Add(weapon);
            }
            if (!string.IsNullOrEmpty(arms?.Melee))
            {
                var blade = new Loot { Weapon = arms!.Melee };
                if (!string.IsNullOrEmpty(arms.MeleeId)) blade.WeaponId = arms.MeleeId;
                out_.Add(blade);
            }
            var left = new Dictionary<string, int>(u.Charges);
            var all = (u.Belted ?? new List<string>()).Concat(u.Gear ?? new List<string>()).ToList();
            all.Reverse();
            foreach (var item in all)
            {
                var uses = UsesOf(item);
                if (!uses.All((e) => (left.TryGetValue(e.Key, out var n) ? n : 0) >= e.Value)) continue;
                foreach (var e in uses) left[e.Key] = (left.TryGetValue(e.Key, out var n) ? n : 0) - e.Value;
                out_.Add(new Loot { Item = item });
            }
            return out_;
        }

        /// <summary>
        /// The belt things found and put on out there that come home (playtest 18/10
        /// #5): those whose uses are all still there, counting the soldier's own as
        /// spent first (they are the Armería's, and it fills them again).
        /// </summary>
        public static List<Loot> BeltedBack(Unit u)
        {
            var left = new Dictionary<string, int>(u.Charges);
            var back = new List<Loot>();
            var all = new List<string>(u.Belted ?? new List<string>());
            all.Reverse();
            foreach (var item in all)
            {
                var uses = UsesOf(item);
                if (!uses.All((e) => (left.TryGetValue(e.Key, out var n) ? n : 0) >= e.Value)) continue;
                foreach (var e in uses) left[e.Key] = (left.TryGetValue(e.Key, out var n) ? n : 0) - e.Value;
                back.Add(new Loot { Item = item });
            }
            return back;
        }
    }
}
