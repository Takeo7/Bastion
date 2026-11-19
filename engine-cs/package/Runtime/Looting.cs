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
        /// <summary>G19: a piece of furniture searched one by one beside its floor (SearchPieces.Furniture): not searched whole.</summary>
        public bool Pieces { get; set; }
    }

    /// <summary>Looting.KeyWhere: where La llave's key is (G23.1).</summary>
    public static class KeyPlaces
    {
        public const string Holder = "holder";
        public const string Floor = "floor";
        public const string Pack = "pack";
        public const string Used = "used";
    }

    /// <summary>
    /// G19 (Arturo, 18/11, cuadros 65–67; .dev-snapshots/gameplay/muebles/CONTRATO.md): what is searched piece by piece, as a
    /// box: the police station's own (its marks, InteriorSite.Dressing, PoliceDetailKinds) and the medical furniture
    /// anywhere (its decor). What Looting.SearchSpot gives for each, for its name (Lore).
    /// </summary>
    public static class SearchPieces
    {
        public const string WeaponRack = PoliceDetailKinds.WeaponRack;
        public const string WeaponLocker = PoliceDetailKinds.WeaponLocker;
        public const string GunCase = PoliceDetailKinds.GunCase;
        public const string AmmoCase = PoliceDetailKinds.AmmoCase;
        public const string EvidenceShelf = PoliceDetailKinds.EvidenceShelf;
        public const string LockerRow = PoliceDetailKinds.LockerRow;
        /// <summary>The dead officer (cuadro 67): on the floor, a body; no furniture, so its room is searched whole still.</summary>
        public const string CorpseOfficer = PoliceDetailKinds.CorpseOfficer;
        public const string MedicalCabinet = DecorKinds.MedicalCabinet;
        public const string MedicalShelf = DecorKinds.MedicalShelf;
        public const string MedicalCart = DecorKinds.MedicalCart;

        /// <summary>A police station's: marks of its InteriorSite.</summary>
        public static readonly IReadOnlyList<string> Police = new[] { WeaponRack, WeaponLocker, GunCase, AmmoCase, EvidenceShelf, LockerRow, CorpseOfficer };
        /// <summary>Anywhere: decor, by its anchor.</summary>
        public static readonly IReadOnlyList<string> Medical = new[] { MedicalCabinet, MedicalShelf, MedicalCart };
        public static readonly IReadOnlyList<string> All = Police.Concat(Medical).ToList();

        /// <summary>Furniture: its room isn't searched whole (LootRoom.Pieces). All but the officer's body.</summary>
        public static bool Furniture(string kind) => kind != CorpseOfficer;

        /// <summary>The tall ones (a `high` tile); the rest stand `low`.</summary>
        public static readonly IReadOnlyList<string> Tall = new[] { WeaponRack, WeaponLocker, EvidenceShelf, LockerRow, MedicalCabinet, MedicalShelf };

        /// <summary>
        /// Whether the piece of `kind` on `p` still stands to be searched (Arte, 18/11): a blast knocks a tall one down to low
        /// cover and blows a low one away (Sim's BlastResult), and what it held with it, as a box blown away (SearchSpot).
        /// The officer's body, wherever it lies.
        /// </summary>
        public static bool Standing(GameState s, Pos p, string kind)
        {
            if (kind == CorpseOfficer) return true;
            var tile = Grid.KindAt(s, p);
            return Tall.Contains(kind) ? tile == TileKinds.High : tile == TileKinds.Low || tile == TileKinds.High;
        }
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
            // The molotovs where there is a bottle and petrol; the grenades few and only in a warehouse (Arturo, 1/11).
            [Spots.Home] = new LootTable { Supplies = 30, Item = 8, Medicines = 3, Food = 20, Parts = 0, Weapon = 2, Mod = 0, Mags = 4, Items = new[] { ItemIds.FirstAid, ItemIds.SmokeGrenade, ItemIds.NanoVest, ItemIds.MindShield, ItemIds.Molotov } },
            [Spots.Office] = new LootTable { Supplies = 30, Item = 8, Medicines = 2, Food = 4, Parts = 15, Weapon = 0, Mod = 1, Mags = 3, Items = new[] { ItemIds.FirstAid, ItemIds.Flashbang, ItemIds.MindShield } },
            [Spots.Shop] = new LootTable { Supplies = 35, Item = 8, Medicines = 12, Food = 25, Parts = 2, Weapon = 0, Mod = 1, Mags = 6, Items = new[] { ItemIds.FirstAid, ItemIds.SmokeGrenade, ItemIds.Molotov } },
            [Spots.Store] = new LootTable { Supplies = 30, Item = 14, Medicines = 2, Food = 15, Parts = 15, Weapon = 3, Mod = 4, Mags = 10, Items = new[] { ItemIds.FragGrenade, ItemIds.SmokeGrenade, ItemIds.Flashbang } },
            // A boot left (G21, cuadro 72: one car in three rifled already, Setup.PlaceLootedCars): a little better than it was
            // (Supplies 30, Item 14, Food 5, Parts 15, Weapon 4, Mod 3, Mags 10; nothing in 11).
            [Spots.Trunk] = new LootTable { Supplies = 34, Item = 18, Medicines = 8, Food = 2, Parts = 8, Weapon = 9, Mod = 5, Mags = 16, Items = new[] { ItemIds.Molotov, ItemIds.FirstAid, ItemIds.NanoVest } },
            [Spots.Glovebox] = new LootTable { Supplies = 25, Item = 5, Medicines = 12, Food = 3, Parts = 5, Weapon = 1, Mod = 1, Mags = 8, Items = new[] { ItemIds.FirstAid } },
        };

        /// <summary>Rations and parts found at once.</summary>
        private static readonly IReadOnlyList<int> FoodFind = new[] { 2, 4 };
        private static readonly IReadOnlyList<int> PartsFind = new[] { 1, 2 };

        /// <summary>
        /// Which weapon turns up, by FOUND_WEAPONS weight, as it is found (docs/FASE9.md
        /// §5): somewhat worn, and a good one with its improvement mounted.
        /// </summary>
        private static Loot FoundWeapon(Rng rng, bool pistols)
        {
            // Object.values(FOUND_WEAPONS): in the order of its keys, FoundWeaponIds.All's. The pistol only where they are
            // counted (MissionState.Pistols; Arturo, 1/11): anywhere else everyone carries one already.
            var all = FoundWeaponIds.All.Where((id) => pistols || id != FoundWeaponIds.Pistol).Select((id) => Content.FoundWeapons[id]).ToList();
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
            return SpotsOf.TryGetValue(decor, out var spot) ? RollSpot(spot, difficulty, rng, night, mags, false) : null;
        }

        /// <summary>What searching a place of kind `spot` turns up: rollLoot by the kind of place, not the piece of furniture.</summary>
        /// <remarks>`modTimes`: how many times an accessory's share (Armero, T2: 2).</remarks>
        private static Loot? RollSpot(string spot, int difficulty, Rng rng, bool night, bool mags, bool pistols, int modTimes = 1)
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
            if (roll < (edge += t.Weapon)) return FoundWeapon(rng, pistols);
            if (roll < (edge += t.Mod * modTimes)) return FoundMod(rng);
            if (roll < (edge += t.Mags)) return mags ? new Loot { Mags = 1 } : null;
            return null;
        }

        // ------------------------------------------------------------ the furniture

        // ------------------------------------------------------- the loot boxes

        /// <summary>The special boxes (docs/FASE8.md §B): each always holds what it is marked for.</summary>
        public static readonly IReadOnlyList<string> LootBoxes = new[] { DecorKinds.LootWeapons, DecorKinds.LootMedical, DecorKinds.LootAmmo, DecorKinds.LootFood };

        /// <summary>
        /// A car's boot, while it is shut (Arturo, 2/11: «se puede lootear cualquier coche que tenga el maletero cerrado"):
        /// any whole car on the ground but a wreck, searched once as a box is, with what a boot holds (Spots.Trunk).
        /// </summary>
        public const string CarBoot = "carBoot";
        /// <summary>A police car's boot (MissionState.PoliceCar): a gun one time in PoliceBootGun (5, Arturo, 15/11: «aparecieron armas en el 100 % de los coches de policía»; it was 3), a
        /// boot's finds otherwise.</summary>
        public const string PoliceBoot = "policeBoot";
        public const int PoliceBootGun = 5;
        /// <summary>A dead enemy's body (Arturo, 2/11: «registras cualquier cuerpo»): what it carried (RollBody).</summary>
        public const string Body = "body";

        /// <summary>The gun a living enemy carried, as the Armería has it: dropped for whoever searches its body.</summary>
        private static readonly IReadOnlyDictionary<string, string> CarriedGuns = new Dictionary<string, string>
        {
            [TemplateIds.Trooper] = FoundWeaponIds.AssaultRifle,
            [TemplateIds.Officer] = FoundWeaponIds.AssaultRifle,
            [TemplateIds.Lancer] = FoundWeaponIds.PumpShotgun,
        };

        /// <summary>
        /// What searching `body` turns up (Arturo, 2/11): a living enemy's gun, always; on one of the dead, a gun one time in
        /// four if it wore a uniform (a policeman's pistol; a soldier's, now and then a rifle) and hardly ever otherwise.
        /// </summary>
        public static Loot? RollBody(Unit body, Rng rng, bool pistols)
        {
            if (CarriedGuns.TryGetValue(body.Template, out var gun)) return new Loot { Weapon = gun, Condition = rng.Range(FoundCondition[0], FoundCondition[1]) };
            var uniform = body.Outfit == Outfits.Police || body.Outfit == Outfits.Military;
            if (rng.Int(100) >= (uniform ? Content.Uniforms.Gun : Content.Uniforms.AnyGun)) return null;
            if (!uniform) return FoundWeapon(rng, pistols);
            var sidearm = pistols ? FoundWeaponIds.Pistol : FoundWeaponIds.PumpShotgun;
            var kind = body.Outfit == Outfits.Military && rng.Int(3) == 0 ? FoundWeaponIds.AssaultRifle : sidearm;
            return new Loot { Weapon = kind, Condition = rng.Range(FoundCondition[0], FoundCondition[1]) };
        }

        /// <summary>
        /// What opening a box of `kind` turns up: a good weapon, one or two
        /// medicines, two magazines (for a searcher who counts them; nothing for
        /// anyone else), or three to five rations.
        /// </summary>
        public static Loot? RollBox(string kind, Rng rng, bool mags, bool pistols = false, int difficulty = 2, bool night = false, int modTimes = 1)
        {
            switch (kind)
            {
                case CarBoot:
                    return RollSpot(Spots.Trunk, difficulty, rng, night, mags, pistols, modTimes);
                case PoliceBoot:
                    return rng.Int(PoliceBootGun) == 0 ? FoundWeapon(rng, pistols) : RollSpot(Spots.Trunk, difficulty, rng, night, mags, pistols, modTimes);
                case DecorKinds.LootWeapons:
                    // A weapon, or now and then an improvement for one (docs/FASE9.md §5).
                    return rng.Int(10) < 3 * modTimes ? FoundMod(rng) : FoundWeapon(rng, pistols);
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
        /// away. Furniture is no longer searched piece by piece (docs/FASE8.md §B): a
        /// room is, as a whole (roomAt). A car by its boot, and a dead enemy's body
        /// (Arturo, 2/11).
        /// </summary>
        public static string? SearchSpot(GameState s, Pos at)
        {
            var p = Grid.ToPos(at);
            if (!Grid.InBounds(s, p.X, p.Y)) return null;
            if (s.Mission.OutOfReach != null && s.Mission.OutOfReach.Contains(Grid.NodeOf(s, p))) return null;
            if (BodyAt(s, p) != null) return Body;
            // G19: a piece that holds something (MissionState.Pieces), the dead officer's on the floor too.
            if (s.Mission.Pieces != null && s.Mission.Pieces.Contains(Grid.NodeOf(s, p)) && PieceAt(s, p) is string piece) return SearchPieces.Standing(s, p, piece) ? piece : null;
            var decor = DecorLayer(s, p.Z);
            if (decor == null) return null;
            var kind = Grid.KindAt(s, p);
            if (kind != TileKinds.Low && kind != TileKinds.High) return null;
            var i = Grid.TileIndex(s, p.X, p.Y);
            if (p.Z == 0 && BootOf(s, i) is int nose) return (s.Mission.PoliceCar ?? new List<int>()).Contains(nose) ? PoliceBoot : CarBoot;
            var look = Decor.DecorOf(i < decor.Count ? decor[i] : (int?)null);
            return look != null && LootBoxes.Contains(look.Kind) ? look.Kind : null;
        }

        /// <summary>
        /// G19: the piece searched one by one on `p` (SearchPieces), whether it holds something or is empty from the start:
        /// a police station's mark there, or medical furniture whose anchor `p` is (`anyTile`: any of its tiles). Null with no
        /// MissionState.Pieces (a fight of the 0.13.2's), as if there were none.
        /// </summary>
        public static string? PieceAt(GameState s, Pos at, bool anyTile = false)
        {
            if (s.Mission.Pieces == null) return null;
            var p = Grid.ToPos(at);
            if (!Grid.InBounds(s, p.X, p.Y)) return null;
            foreach (var site in s.Interiors ?? new List<InteriorSite>())
            {
                if (site.Kind != InteriorKinds.Police) continue;
                foreach (var d in site.Dressing) if (d.X == p.X && d.Y == p.Y && d.Z == p.Z && SearchPieces.Police.Contains(d.Kind)) return d.Kind;
            }
            var decor = DecorLayer(s, p.Z);
            if (decor == null) return null;
            var i = Grid.TileIndex(s, p.X, p.Y);
            var o = Decor.ObjectAt(decor, s.Width, i);
            return o != null && (anyTile || o.Anchor == i) && SearchPieces.Medical.Contains(o.Kind) ? o.Kind : null;
        }

        /// <summary>The dead enemy lying on `p` that can be searched: not risen again, nor an objective, nor the alarm.</summary>
        public static Unit? BodyAt(GameState s, Pos p)
        {
            return s.Units.FirstOrDefault((u) => !u.Alive && !u.Evacuated && u.Team == Teams.Alien && !u.Objective && !u.Reanimated && u.Template != TemplateIds.Relay && Grid.SamePos(u.Pos, p));
        }

        /// <summary>
        /// The nose of the car whose boot is on ground tile `i`, or null: a whole car (Decor.ObjectAt, its CarFront the
        /// anchor), its boot the farthest of its tiles from the nose, and no wreck (GameState.Wrecks: its boot burst open)
        /// nor one rifled already (MissionState.Rifled, G21) nor burning (a fire whose source is its nose).
        /// </summary>
        public static int? BootOf(GameState s, int i)
        {
            if (s.Decor == null) return null;
            var car = Decor.ObjectAt(s.Decor, s.Width, i);
            if (car == null || car.Kind != DecorKinds.CarFront || car.Tiles.Count < 2) return null;
            if ((s.Wrecks ?? new List<Wreck>()).Any((w) => car.Tiles.Contains(w.Anchor))) return null;
            // Nor one rifled before the squad came (G21: MissionState.Rifled), nor one burning (GameState.Fires from its nose,
            // for ever): drawn rusted, both (Arturo, cuadro 74: «oxidado = no se registra»).
            if (s.Mission.Rifled?.Contains(car.Anchor) == true) return null;
            if ((s.Fires ?? new List<Fire>()).Any((f) => f.Source == car.Anchor)) return null;
            int nx = car.Anchor % s.Width, ny = car.Anchor / s.Width;
            var boot = car.Tiles.OrderByDescending((j) => Js.Hypot(j % s.Width - nx, j / s.Width - ny)).First();
            return boot == i ? car.Anchor : (int?)null;
        }

        // ------------------------------------------------------------ the key (G23.1)

        /// <summary>Whether the door on `p` is locked (MissionState.Locked): nobody through it, only the key opens it.</summary>
        public static bool LockedAt(GameState s, Pos p) => s.Mission.Locked != null && s.Mission.Locked.Contains(Grid.NodeOf(s, Grid.ToPos(p)));

        /// <summary>The armoury's key in `u`'s pack, or null.</summary>
        public static Gear? KeyOf(Unit u) => u.Pack?.Items.FirstOrDefault((p) => p.Gear.Kind == GearKinds.Key)?.Gear;

        /// <summary>
        /// Whether La llave's armoury is open (G23.1, its objective's second step; UI, U22): opened with the key (nothing
        /// locked any more), or a hole in its walls (Arturo, cuadro 79: «también volando la pared»), a tile of them neither
        /// wall nor door any more. False in another fight.
        /// </summary>
        public static bool ArmoryOpen(GameState s)
        {
            if (s.Mission.KeyArmory != true) return false;
            if (s.Mission.Locked == null) return true;
            var a = (s.Interiors ?? new List<InteriorSite>()).Where((x) => x.Kind == InteriorKinds.Police).SelectMany((x) => x.Rooms).FirstOrDefault((r) => r.Use == PoliceUses.Armory && r.Z == 0);
            if (a == null) return false;
            for (var y = a.Y - 1; y <= a.Y + a.H; y++)
            {
                for (var x = a.X - 1; x <= a.X + a.W; x++)
                {
                    if (x >= a.X && y >= a.Y && x < a.X + a.W && y < a.Y + a.H) continue;
                    if (!Grid.InBounds(s, x, y)) continue;
                    var kind = Grid.KindAt(s, new Pos { X = x, Y = y, Z = 0 });
                    if (kind != TileKinds.Wall && kind != TileKinds.Door && kind != TileKinds.DoorOpen) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Where La llave's key is (G23.1, for its objective): `holder` (on its holder, alive or a body not searched),
        /// `floor` (lying: FloorGear), `pack` (in a soldier's), `used` (the armoury open); null in another fight.
        /// </summary>
        public static string? KeyWhere(GameState s)
        {
            if (s.Mission.KeyArmory != true) return null;
            if (s.Mission.Key != null) return KeyPlaces.Holder;
            if ((s.FloorGear ?? new List<DroppedGear>()).Any((d) => d.Gear.Kind == GearKinds.Key)) return KeyPlaces.Floor;
            if (s.Units.Any((u) => u.Alive && KeyOf(u) != null)) return KeyPlaces.Pack;
            return s.Mission.Locked == null ? KeyPlaces.Used : KeyPlaces.Pack;
        }

        // ------------------------------------------------------------ the pieces

        /// <summary>RollPiece's shares in 100: a locker's vest and magazine, an evidence shelf's supplies and belt's thing, a medicine shelf's medicine and kit, a cart's medicine; the rest, nothing.</summary>
        public const int LockerVest = 30, LockerMag = 30, EvidenceSupplies = 30, EvidenceBelt = 20, ShelfMedicine = 40, ShelfFirstAid = 15, CartMedicine = 35;

        /// <summary>
        /// G19 (CONTRATO.md §3, cuadro 67): what searching a piece that holds something turns up, and the chance in 100 of a
        /// vest with it (a run's only, RunLoot.VestOf). The armoury always gives: its rack a long gun (an assault rifle two
        /// times in three, a shotgun), its locker a sidearm, its gun cases a sidearm or an improvement, its ammo boxes two
        /// magazines; the lockers a vest, a magazine or nothing; an evidence shelf supplies, a belt's thing or nothing; the
        /// officer's body what a policeman's does (RollBody: a gun one time in four, a vest now and then); a medicine cabinet
        /// a medicine or a first-aid kit, always; a medicine shelf one of them or nothing; a cart a medicine or nothing.
        /// A sidearm where pistols aren't counted (MissionState.Pistols) is a shotgun, as a body's (RollBody); magazines only
        /// for a searcher who counts them.
        /// </summary>
        public static (Loot? Loot, int Vest) RollPiece(string kind, Rng rng, bool mags, bool pistols = false, int difficulty = 2, bool night = false)
        {
            var d = Math.Max(1, Math.Min(4, difficulty));
            Loot Gun(string id) => new Loot { Weapon = id, Condition = rng.Range(FoundCondition[0], FoundCondition[1]) };
            var sidearm = pistols ? FoundWeaponIds.Pistol : FoundWeaponIds.PumpShotgun;
            switch (kind)
            {
                case SearchPieces.WeaponRack:
                    return (Gun(rng.Int(3) < 2 ? FoundWeaponIds.AssaultRifle : FoundWeaponIds.PumpShotgun), 0);
                case SearchPieces.WeaponLocker:
                    return (Gun(sidearm), 0);
                case SearchPieces.GunCase:
                    return (rng.Int(2) == 0 ? Gun(sidearm) : FoundMod(rng), 0);
                case SearchPieces.AmmoCase:
                    return (mags ? new Loot { Mags = 2 } : null, 0);
                case SearchPieces.LockerRow:
                {
                    var roll = rng.Int(100);
                    if (roll < LockerVest) return (null, 100);
                    return (roll < LockerVest + LockerMag && mags ? new Loot { Mags = 1 } : null, 0);
                }
                case SearchPieces.EvidenceShelf:
                {
                    var roll = rng.Int(100);
                    if (roll < EvidenceSupplies) return (new Loot { Supplies = Supplies(d, rng, night) }, 0);
                    return (roll < EvidenceSupplies + EvidenceBelt ? new Loot { Item = rng.Pick(Content.BeltItems) } : null, 0);
                }
                case SearchPieces.CorpseOfficer:
                    return (rng.Int(100) < Content.Uniforms.Gun ? Gun(sidearm) : null, RunLoot.VestChance);
                case SearchPieces.MedicalCabinet:
                    return (rng.Int(3) < 2 ? new Loot { Medicines = 1 } : new Loot { Item = ItemIds.FirstAid }, 0);
                case SearchPieces.MedicalShelf:
                {
                    var roll = rng.Int(100);
                    return (roll < ShelfMedicine ? new Loot { Medicines = 1 } : roll < ShelfMedicine + ShelfFirstAid ? new Loot { Item = ItemIds.FirstAid } : null, 0);
                }
                case SearchPieces.MedicalCart:
                    return (rng.Int(100) < CartMedicine ? new Loot { Medicines = 1 } : null, 0);
                default:
                    return (null, 0);
            }
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
            var pieces = false;
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
                        // G19: a piece searched one by one beside it, full or empty from the start.
                        if (!pieces && PieceAt(s, q, anyTile: true) is string piece && SearchPieces.Furniture(piece)) pieces = true;
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
            return new LootRoom { Id = id, Tiles = tiles, Spot = best, Pieces = pieces };
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
        public static Loot? RollRoom(LootRoom room, int difficulty, Rng rng, bool night = false, bool mags = false, string? market = null, bool pistols = false, int modTimes = 1)
        {
            if (market == MarketStocks.Full) return new Loot { Food = rng.Range(MarketFood[0], MarketFood[1]) };
            return RollSpot(market != null ? Spots.Shop : room.Spot, difficulty, rng, night, mags, pistols, modTimes);
        }

        /// <summary>Room in a soldier's backpack: BACKPACK_SLOTS, one more with the Chapistería (docs/FASE8.md §C).</summary>
        public static int PackSlots(Unit u)
        {
            return u.PackSlots ?? Content.BackpackSlots;
        }

        /// <summary>
        /// What `l` piles by, or null when it takes a slot of its own: parts, supplies, medicines or food alone, by their
        /// count; an accessory or an improvement alone, by its id. A weapon never (each has its condition and what is on
        /// it), nor a find of several things at once.
        /// </summary>
        private static string? PileKey(Loot l)
        {
            if (!string.IsNullOrEmpty(l.Weapon) || !string.IsNullOrEmpty(l.WeaponId) || l.Condition != null || l.Mods != null || l.Mags.GetValueOrDefault() != 0) return null;
            var keys = new List<string>();
            if (l.Supplies.GetValueOrDefault() != 0) keys.Add("supplies");
            if (l.Medicines.GetValueOrDefault() != 0) keys.Add("medicines");
            if (l.Food.GetValueOrDefault() != 0) keys.Add("food");
            if (l.Parts.GetValueOrDefault() != 0) keys.Add("parts");
            if (!string.IsNullOrEmpty(l.Item)) keys.Add("item:" + l.Item);
            if (!string.IsNullOrEmpty(l.Mod)) keys.Add("mod:" + l.Mod);
            return keys.Count == 1 ? keys[0] : null;
        }

        /// <summary>
        /// The slot of `u`'s backpack that `loot` piles onto, or -1 (Arturo, 7/11: «si ya tengo piezas y salen más, se
        /// acumulan, no ocupan otro hueco»; in the box, «lo que es igual, sin límite»).
        /// </summary>
        public static int PileFor(Unit u, Loot loot)
        {
            var key = PileKey(loot);
            if (key == null || u.Backpack == null) return -1;
            return u.Backpack.FindIndex((l) => PileKey(l) == key);
        }

        /// <summary>Whether `loot` fits in `u`'s backpack: onto a pile of the same, or in a free slot (magazines go to the spares).</summary>
        public static bool Fits(Unit u, Loot loot)
        {
            return loot.Mags.GetValueOrDefault() != 0 || PileFor(u, loot) >= 0 || (u.Backpack?.Count ?? 0) < PackSlots(u);
        }

        /// <summary>How many there are of `l`'s accessory or improvement (Loot.Count; absent, one).</summary>
        public static int CountOf(Loot l)
        {
            return l.Count ?? 1;
        }

        /// <summary>`loot` added onto `pile` (PileFor): the counts summed.</summary>
        public static void Pile(Loot pile, Loot loot)
        {
            if (loot.Supplies.GetValueOrDefault() != 0) pile.Supplies = (pile.Supplies ?? 0) + loot.Supplies;
            if (loot.Medicines.GetValueOrDefault() != 0) pile.Medicines = (pile.Medicines ?? 0) + loot.Medicines;
            if (loot.Food.GetValueOrDefault() != 0) pile.Food = (pile.Food ?? 0) + loot.Food;
            if (loot.Parts.GetValueOrDefault() != 0) pile.Parts = (pile.Parts ?? 0) + loot.Parts;
            if (!string.IsNullOrEmpty(loot.Item) || !string.IsNullOrEmpty(loot.Mod)) pile.Count = CountOf(pile) + CountOf(loot);
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

        private static readonly HashSet<string> Solid = new HashSet<string> { TileKinds.Wall, TileKinds.Door, TileKinds.Window, TileKinds.WindowBroken, TileKinds.Bars };

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
            var reach = 1 + Math.Max(0, u.Mods.SearchReach);
            var tiles = new List<Pos>();
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    var p = new Pos { X = u.Pos.X + dx, Y = u.Pos.Y + dy, Z = u.Pos.Z };
                    if (Grid.InBounds(s, p.X, p.Y) && IsSearchable(s, p) && ReachesToOpen(s, u.Pos, p, reach)) tiles.Add(p);
                }
            }
            // As they were found before: the eight beside first (Grid.Dirs8's order), then where it stands, then further.
            return tiles.OrderBy((p) => Math.Max(Math.Abs(p.X - u.Pos.X), Math.Abs(p.Y - u.Pos.Y)) == 0 ? 1 : Math.Max(Math.Abs(p.X - u.Pos.X), Math.Abs(p.Y - u.Pos.Y)) == 1 ? 0 : 2)
                .ThenBy((p) => Grid.Dirs8.ToList().FindIndex((d) => d.X == p.X - u.Pos.X && d.Y == p.Y - u.Pos.Y)).ToList();
        }

        /// <summary>
        /// Whether a soldier on `from` can open what is on `p`: beside it (ReachesToSearch), or standing on a body (the dead
        /// officer's too, G19); with
        /// `reach` 2 (Gancho, T2), two tiles off too, past a tile it reaches across to it on the way (ReachesToSearch twice).
        /// </summary>
        public static bool ReachesToOpen(GameState s, Pos from, Pos p, int reach = 1)
        {
            if (ReachesToSearch(s, from, p) || (Grid.SamePos(from, p) && (BodyAt(s, p) != null || PieceAt(s, p) == SearchPieces.CorpseOfficer))) return true;
            if (reach < 2 || Math.Max(Math.Abs(p.X - from.X), Math.Abs(p.Y - from.Y)) != 2) return false;
            foreach (var d in Grid.Dirs8)
            {
                var m = new Pos { X = from.X + d.X, Y = from.Y + d.Y, Z = from.Z };
                if (!Grid.InBounds(s, m.X, m.Y) || Math.Max(Math.Abs(p.X - m.X), Math.Abs(p.Y - m.Y)) != 1) continue;
                var kind = Grid.KindAt(s, m);
                if (kind != null && Solid.Contains(kind)) continue;
                if (ReachesToSearch(s, from, m) && ReachesToSearch(s, m, p)) return true;
            }
            return false;
        }

        // --------------------------------------------------------------- the haul

        public static LootTotals LootTotalsOf(IReadOnlyList<Loot> loot)
        {
            var out_ = new LootTotals();
            foreach (var l in loot)
            {
                out_.Supplies += l.Supplies ?? 0;
                if (!string.IsNullOrEmpty(l.Item)) for (var k = 0; k < CountOf(l); k++) out_.Items.Add(l.Item!);
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
                if (!string.IsNullOrEmpty(l.Mod)) for (var k = 0; k < CountOf(l); k++) (out_.Mods ??= new List<string>()).Add(l.Mod!);
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
            // A counted pistol (Arturo, 1/11); the one everyone carried before goes with its bearer.
            if (arms?.Pistol == true)
            {
                var pistol = new Loot { Weapon = FoundWeaponIds.Pistol };
                if (!string.IsNullOrEmpty(arms.PistolId)) pistol.WeaponId = arms.PistolId;
                out_.Add(pistol);
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
