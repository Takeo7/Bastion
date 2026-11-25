#nullable enable
// The runs' gear (.dev-snapshots/gameplay/runs/H0-DISENO.md §2 and §5; new in C#): the grids (a gear's cells, what fits
// where, the first place free, piles that fill up and split), where a gear is, and the gear commands without a vote:
// what each refuses (the *Refusal functions, pure, which the UI calls too to paint a cell) and what each does. Nothing
// in a fight (H3 has the combat pack's own); the stash only in the base; during a run, only the squad's.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>Where a gear is (Gears.Find): in a grid (the stash's, or a pack) or in a kit's place.</summary>
    public sealed class GearSpot
    {
        public Gear Gear { get; set; } = new Gear();
        /// <summary>Whose: null, the stash.</summary>
        public CampaignSoldier? Soldier { get; set; }
        /// <summary>KitSlots: Pack for a grid, the stash's too.</summary>
        public string Slot { get; set; } = KitSlots.Pack;
        /// <summary>In a grid: the grid, and where in it.</summary>
        public GearGrid? Grid { get; set; }
        public Placed? Placed { get; set; }
        /// <summary>In La Lata's trunk (RunState.Trunk; cuadro 165), not the stash.</summary>
        public bool Trunk { get; set; }
    }

    public static class Gears
    {
        // ------------------------------------------------------------------ grids

        /// <summary>The cells a gear takes, width × height: its size (Content.GearSize), swapped when turned.</summary>
        public static (int W, int H) Footprint(Gear g, bool turned)
        {
            var (w, h) = Content.GearSize(g.Kind, g.Type);
            return turned ? (h, w) : (w, h);
        }

        /// <summary>The gear lying on a cell of the grid, or null.</summary>
        public static Placed? At(GearGrid grid, int x, int y)
        {
            foreach (var p in grid.Items)
            {
                var (w, h) = Footprint(p.Gear, p.Turned);
                if (x >= p.X && x < p.X + w && y >= p.Y && y < p.Y + h) return p;
            }
            return null;
        }

        /// <summary>
        /// Whether `g` fits at x, y (its top-left cell), turned or not: inside the grid and on no other gear. The gear itself
        /// and those in `free` (about to leave) don't count.
        /// </summary>
        public static bool Fits(GearGrid grid, Gear g, int x, int y, bool turned, ICollection<Gear>? free = null)
        {
            var (w, h) = Footprint(g, turned);
            if (x < 0 || y < 0 || x + w > grid.W || y + h > grid.H) return false;
            foreach (var p in grid.Items)
            {
                if (p.Gear == g || (free != null && free.Contains(p.Gear))) continue;
                var (pw, ph) = Footprint(p.Gear, p.Turned);
                if (x < p.X + pw && p.X < x + w && y < p.Y + ph && p.Y < y + h) return false;
            }
            return true;
        }

        /// <summary>
        /// Where `g` would go by itself: the first place it fits, row by row and cell by cell, lying unturned; failing
        /// that, turned. Null if there's none. Not placed yet.
        /// </summary>
        public static Placed? FirstFit(GearGrid grid, Gear g, ICollection<Gear>? free = null)
        {
            foreach (var turned in new[] { false, true })
            {
                for (var y = 0; y < grid.H; y++)
                {
                    for (var x = 0; x < grid.W; x++)
                    {
                        if (Fits(grid, g, x, y, turned, free)) return new Placed { Gear = g, X = x, Y = y, Turned = turned };
                    }
                }
            }
            return null;
        }

        /// <summary>Lays `g` at x, y if it fits there; what was placed, or null.</summary>
        public static Placed? Place(GearGrid grid, Gear g, int x, int y, bool turned)
        {
            if (!Fits(grid, g, x, y, turned)) return null;
            var p = new Placed { Gear = g, X = x, Y = y, Turned = turned };
            grid.Items.Add(p);
            return p;
        }

        /// <summary>Takes a gear out of the grid; where it lay, or null if it wasn't there.</summary>
        public static Placed? Remove(GearGrid grid, Gear g)
        {
            var p = grid.Items.FirstOrDefault((x) => x.Gear == g);
            if (p != null) grid.Items.Remove(p);
            return p;
        }

        /// <summary>Two gears that make one pile: the same kind and type, piled (Content.PileOf past 1), nothing mounted.</summary>
        public static bool SamePile(Gear a, Gear b)
        {
            return a != b && a.Kind == b.Kind && a.Type == b.Type && Content.PileOf(a.Kind, a.Type) > 1 && a.Mods == null && b.Mods == null && a.Issued == b.Issued;
        }

        /// <summary>How many more a pile takes.</summary>
        public static int Room(Gear pile)
        {
            return Math.Max(0, Content.PileOf(pile.Kind, pile.Type) - pile.Count);
        }

        /// <summary>As much of `from` onto `into` as it takes (up to `most`); how many went. `from` may be left at 0.</summary>
        public static int Merge(Gear into, Gear from, int? most = null)
        {
            var n = Math.Min(Room(into), Math.Min(from.Count, most ?? from.Count));
            into.Count += n;
            from.Count -= n;
            return n;
        }

        /// <summary>
        /// Puts `g` in the grid by itself: onto its equal piles first, and what is left where it first fits. How many could
        /// not go (0: all of it); those stay in `g`, unplaced.
        /// </summary>
        public static int Stow(GearGrid grid, Gear g)
        {
            foreach (var p in grid.Items)
            {
                if (g.Count == 0) break;
                if (SamePile(p.Gear, g)) Merge(p.Gear, g);
            }
            if (g.Count == 0) return 0;
            var at = FirstFit(grid, g);
            if (at == null) return g.Count;
            grid.Items.Add(at);
            return 0;
        }

        /// <summary>Whether Stow would put all of `g` in the grid, with the gears in `free` gone.</summary>
        public static bool CanStow(GearGrid grid, Gear g, ICollection<Gear>? free = null)
        {
            var room = grid.Items.Where((p) => SamePile(p.Gear, g) && (free == null || !free.Contains(p.Gear))).Sum((p) => Room(p.Gear));
            return room >= g.Count || FirstFit(grid, g, free) != null;
        }

        /// <summary>The § in a grid: its supplies piles.</summary>
        public static int Supplies(GearGrid grid)
        {
            return grid.Items.Where((p) => p.Gear.Kind == GearKinds.Supplies).Sum((p) => p.Gear.Count);
        }

        /// <summary>
        /// Which supplies piles paying `amount` takes, and how much of each: grid by grid in order, the smallest piles first
        /// (they free their cells). Short if there isn't enough.
        /// </summary>
        public static List<(Placed Pile, GearGrid Grid, int Take)> SuppliesFor(IEnumerable<GearGrid> grids, int amount)
        {
            var plan = new List<(Placed Pile, GearGrid Grid, int Take)>();
            var left = amount;
            foreach (var grid in grids)
            {
                foreach (var p in grid.Items.Where((x) => x.Gear.Kind == GearKinds.Supplies).OrderBy((x) => x.Gear.Count))
                {
                    if (left <= 0) return plan;
                    var take = Math.Min(left, p.Gear.Count);
                    plan.Add((p, grid, take));
                    left -= take;
                }
            }
            return plan;
        }

        /// <summary>Pays `amount` from the supplies piles of `grids` (SuppliesFor); an emptied pile leaves its grid.</summary>
        public static void PaySupplies(IEnumerable<GearGrid> grids, int amount)
        {
            foreach (var (pile, grid, take) in SuppliesFor(grids, amount))
            {
                pile.Gear.Count -= take;
                if (pile.Gear.Count <= 0) grid.Items.Remove(pile);
            }
        }

        /// <summary>A new gear with its own id (from the campaign's NextId, as the soldiers'): "g17".</summary>
        public static Gear NewGear(CampaignState c, string kind, string? type, int count = 1)
        {
            return new Gear { Id = $"g{++c.NextId}", Kind = kind, Type = type, Count = count };
        }

        /// <summary>`count` off a pile, as a new gear (not placed anywhere yet).</summary>
        public static Gear Split(CampaignState c, Gear pile, int count)
        {
            pile.Count -= count;
            var part = NewGear(c, pile.Kind, pile.Type, count);
            // What the base gave stays given (Gear.Issued).
            part.Issued = pile.Issued;
            return part;
        }

        // ------------------------------------------------------- where things are

        /// <summary>A gear, deep: its own mods.</summary>
        public static Gear CloneGear(Gear g)
        {
            return new Gear { Id = g.Id, Kind = g.Kind, Type = g.Type, Count = g.Count, Mods = g.Mods != null ? new List<string>(g.Mods) : null, Issued = g.Issued };
        }

        public static GearGrid CloneGrid(GearGrid g)
        {
            return new GearGrid { W = g.W, H = g.H, Items = g.Items.Select((p) => new Placed { Gear = CloneGear(p.Gear), X = p.X, Y = p.Y, Turned = p.Turned }).ToList() };
        }

        /// <summary>A kit's places, deep, as a fight's (Unit.Kit).</summary>
        public static KitPlaces ClonePlaces(IKitPlaces k)
        {
            Gear? Copy(Gear? g) => g != null ? CloneGear(g) : null;
            return new KitPlaces { Primary = Copy(k.Primary), Melee = Copy(k.Melee), Pistol = Copy(k.Pistol), Vest = Copy(k.Vest), Belt = new[] { Copy(k.Belt.Length > 0 ? k.Belt[0] : null), Copy(k.Belt.Length > 1 ? k.Belt[1] : null) } };
        }

        /// <summary>A kit's place (KitSlots, not the pack): what is in it.</summary>
        public static Gear? InSlot(IKitPlaces kit, string slot)
        {
            switch (slot)
            {
                case KitSlots.Primary:
                    return kit.Primary;
                case KitSlots.Melee:
                    return kit.Melee;
                case KitSlots.Pistol:
                    return kit.Pistol;
                case KitSlots.Vest:
                    return kit.Vest;
                case KitSlots.Belt0:
                    return kit.Belt.Length > 0 ? kit.Belt[0] : null;
                case KitSlots.Belt1:
                    return kit.Belt.Length > 1 ? kit.Belt[1] : null;
                default:
                    return null;
            }
        }

        public static void SetSlot(IKitPlaces kit, string slot, Gear? g)
        {
            if (kit.Belt.Length < 2) kit.Belt = new[] { kit.Belt.Length > 0 ? kit.Belt[0] : null, null };
            switch (slot)
            {
                case KitSlots.Primary:
                    kit.Primary = g;
                    break;
                case KitSlots.Melee:
                    kit.Melee = g;
                    break;
                case KitSlots.Pistol:
                    kit.Pistol = g;
                    break;
                case KitSlots.Vest:
                    kit.Vest = g;
                    break;
                case KitSlots.Belt0:
                    kit.Belt[0] = g;
                    break;
                case KitSlots.Belt1:
                    kit.Belt[1] = g;
                    break;
            }
        }

        private static readonly IReadOnlyList<string> Places = new[] { KitSlots.Primary, KitSlots.Melee, KitSlots.Pistol, KitSlots.Vest, KitSlots.Belt0, KitSlots.Belt1 };

        /// <summary>Every gear of a kit, where it is: its places, then its pack.</summary>
        public static IEnumerable<GearSpot> OfKit(CampaignSoldier s)
        {
            if (s.Kit == null) yield break;
            foreach (var slot in Places)
            {
                var g = InSlot(s.Kit, slot);
                if (g != null) yield return new GearSpot { Gear = g, Soldier = s, Slot = slot };
            }
            foreach (var p in s.Kit.Pack.Items) yield return new GearSpot { Gear = p.Gear, Soldier = s, Slot = KitSlots.Pack, Grid = s.Kit.Pack, Placed = p };
        }

        /// <summary>Every gear of the campaign: the stash's, La Lata's trunk's, then the living soldiers'.</summary>
        public static IEnumerable<GearSpot> All(CampaignState c)
        {
            if (c.Stash != null)
            {
                foreach (var p in c.Stash.Items) yield return new GearSpot { Gear = p.Gear, Slot = KitSlots.Pack, Grid = c.Stash, Placed = p };
            }
            if (c.Run?.Trunk != null)
            {
                foreach (var p in c.Run.Trunk.Items) yield return new GearSpot { Gear = p.Gear, Slot = KitSlots.Pack, Grid = c.Run.Trunk, Placed = p, Trunk = true };
            }
            foreach (var s in c.Soldiers.Where((x) => x.Alive))
            {
                foreach (var spot in OfKit(s)) yield return spot;
            }
        }

        /// <summary>Where the gear with this id is, or null.</summary>
        public static GearSpot? Find(CampaignState c, string id)
        {
            return All(c).FirstOrDefault((x) => x.Gear.Id == id);
        }

        /// <summary>Takes a gear out of where it is.</summary>
        private static void Lift(GearSpot spot)
        {
            if (spot.Grid != null) spot.Grid.Items.Remove(spot.Placed!);
            else SetSlot(spot.Soldier!.Kit!, spot.Slot, null);
        }

        // ------------------------------------------------------------- who reaches

        /// <summary>Nothing of the gear in a fight: the combat pack is H3's.</summary>
        private static string? Busy(CampaignState c)
        {
            if (c.Stash == null) return RunTexts.NoSlot;
            return c.Run?.Phase == RunPhases.Fight ? RunTexts.InFight : null;
        }

        /// <summary>
        /// Whether `slot` may take from or put into the stash (`s` null: only in the base) or a soldier's kit (one it
        /// controls, Runs.Controls; during a run, of the squad). Null if so.
        /// </summary>
        public static string? Reach(CampaignState c, int slot, IReadOnlyList<int> connected, CampaignSoldier? s)
        {
            if (s == null) return c.Run != null ? RunTexts.StashAtBase : null;
            if (!s.Alive || s.Kit == null) return RunTexts.NoSoldier;
            if (!Runs.Controls(c, slot, connected, s)) return RunTexts.NotYours;
            if (c.Run != null && !c.Run.Squad.Contains(s.Id)) return RunTexts.OnlySquad;
            return null;
        }

        /// <summary>A command's `to`: the stash (null soldier), La Lata's trunk (`trunk`) or a living soldier.</summary>
        private static string? Target(CampaignState c, string to, out CampaignSoldier? soldier, out bool trunk)
        {
            soldier = null;
            trunk = to == GearPlaces.Trunk;
            if (to == GearPlaces.Stash) return null;
            if (trunk) return c.Run?.Trunk == null ? RunTexts.NoTrunk : null;
            soldier = c.Soldiers.FirstOrDefault((x) => x.Id == to && x.Alive);
            return soldier == null || soldier.Kit == null ? RunTexts.NoSoldier : null;
        }

        /// <summary>
        /// Reach of where a gear is, or of a `to` (`trunk`): La Lata's trunk, anyone's while a run goes (shared, as the stash;
        /// a fight, Busy's); else Reach.
        /// </summary>
        private static string? ReachOf(CampaignState c, int slot, IReadOnlyList<int> connected, CampaignSoldier? s, bool trunk)
        {
            if (trunk) return c.Run?.Trunk == null ? RunTexts.NoTrunk : null;
            return Reach(c, slot, connected, s);
        }

        /// <summary>
        /// Runs a change of kits during a run so each soldier keeps the damage they had: a vest put on adds its health, one
        /// taken off takes it (never below 1).
        /// </summary>
        private static void KeepDamage(CampaignState c, IEnumerable<CampaignSoldier?> soldiers, Action change)
        {
            var run = c.Run;
            var before = run == null ? new List<(CampaignSoldier S, int Max)>() : soldiers.Where((s) => s != null && run.Hp.ContainsKey(s.Id)).Distinct().Select((s) => (S: s!, Max: Runs.MaxHp(s!))).ToList();
            change();
            foreach (var (s, max) in before)
            {
                var now = Runs.MaxHp(s);
                run!.Hp[s.Id] = Math.Max(1, Math.Min(now, run.Hp[s.Id] + now - max));
            }
        }

        // ------------------------------------------------------------------- move

        /// <summary>Why `slot` can't move a gear so (MoveGearCommand), or null.</summary>
        public static string? MoveRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, MoveGearCommand cmd)
        {
            return PlanMove(c, slot, connected, cmd).Error;
        }

        /// <summary>MoveGearCommand, once MoveRefusal has let it.</summary>
        public static void Move(CampaignState c, int slot, IReadOnlyList<int> connected, MoveGearCommand cmd)
        {
            PlanMove(c, slot, connected, cmd).Apply?.Invoke();
        }

        /// <summary>Why not, or what to do: one reading for the refusal and the command.</summary>
        private static (string? Error, Action? Apply) PlanMove(CampaignState c, int slot, IReadOnlyList<int> connected, MoveGearCommand cmd)
        {
            var error = Busy(c);
            if (error != null) return (error, null);
            var from = Find(c, cmd.Gear);
            if (from == null) return (RunTexts.NoGear, null);
            error = ReachOf(c, slot, connected, from.Soldier, from.Trunk);
            if (error != null) return (error, null);
            error = Target(c, cmd.To, out var to, out var trunk) ?? ReachOf(c, slot, connected, to, trunk);
            if (error != null) return (error, null);
            var g = from.Gear;
            var soldiers = new[] { from.Soldier, to };
            var where = to == null ? KitSlots.Pack : cmd.Slot;
            if (!KitSlots.All.Contains(where)) return (RunTexts.NoSlot, null);
            if (where == KitSlots.Pack)
            {
                var grid = trunk ? c.Run!.Trunk! : to == null ? c.Stash! : to.Kit!.Pack;
                var there = At(grid, cmd.X, cmd.Y);
                // Onto an equal pile: it fills up, and the rest stays where it was.
                if (there != null && SamePile(there.Gear, g))
                {
                    if (Room(there.Gear) == 0) return (RunTexts.PileFull, null);
                    return (null, () =>
                    {
                        Merge(there.Gear, g);
                        if (g.Count == 0) Lift(from);
                    });
                }
                if (!Fits(grid, g, cmd.X, cmd.Y, cmd.Turned)) return (RunTexts.NoRoom, null);
                return (null, () => KeepDamage(c, soldiers, () =>
                {
                    Lift(from);
                    grid.Items.Add(new Placed { Gear = g, X = cmd.X, Y = cmd.Y, Turned = cmd.Turned });
                }));
            }
            if (!Content.SlotTakes(where, g)) return (RunTexts.NotThere(g), null);
            var kit = to!.Kit!;
            var now = InSlot(kit, where);
            if (now == g) return (null, () => { });
            if (now != null && SamePile(now, g))
            {
                if (Room(now) == 0) return (RunTexts.PileFull, null);
                return (null, () =>
                {
                    Merge(now, g);
                    if (g.Count == 0) Lift(from);
                });
            }
            if (now == null)
            {
                return (null, () => KeepDamage(c, soldiers, () =>
                {
                    Lift(from);
                    SetSlot(kit, where, g);
                }));
            }
            // A kit's place taken: they swap, the one there going where the other came from (turned if that makes it fit).
            if (from.Grid != null)
            {
                var back = new[] { false, true }.Where((t) => Fits(from.Grid, now, from.Placed!.X, from.Placed.Y, t, new[] { g })).Select((t) => (bool?)t).FirstOrDefault();
                if (back == null) return (RunTexts.SwapNoRoom(now, g), null);
                return (null, () => KeepDamage(c, soldiers, () =>
                {
                    Lift(from);
                    SetSlot(kit, where, g);
                    from.Grid.Items.Add(new Placed { Gear = now, X = from.Placed!.X, Y = from.Placed.Y, Turned = back.Value });
                }));
            }
            if (!Content.SlotTakes(from.Slot, now)) return (RunTexts.SwapNoRoom(now, g), null);
            return (null, () => KeepDamage(c, soldiers, () =>
            {
                SetSlot(from.Soldier!.Kit!, from.Slot, now);
                SetSlot(kit, where, g);
            }));
        }

        // ------------------------------------------------------------------ split

        /// <summary>Why `slot` can't split a pile so (SplitGearCommand), or null.</summary>
        public static string? SplitRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, SplitGearCommand cmd)
        {
            return PlanSplit(c, slot, connected, cmd).Error;
        }

        /// <summary>SplitGearCommand, once SplitRefusal has let it.</summary>
        public static void SplitGear(CampaignState c, int slot, IReadOnlyList<int> connected, SplitGearCommand cmd)
        {
            PlanSplit(c, slot, connected, cmd).Apply?.Invoke();
        }

        private static (string? Error, Action? Apply) PlanSplit(CampaignState c, int slot, IReadOnlyList<int> connected, SplitGearCommand cmd)
        {
            var error = Busy(c);
            if (error != null) return (error, null);
            var from = Find(c, cmd.Gear);
            if (from == null) return (RunTexts.NoGear, null);
            error = ReachOf(c, slot, connected, from.Soldier, from.Trunk);
            if (error != null) return (error, null);
            error = Target(c, cmd.To, out var to, out var trunk) ?? ReachOf(c, slot, connected, to, trunk);
            if (error != null) return (error, null);
            var g = from.Gear;
            if (Content.PileOf(g.Kind, g.Type) <= 1 || g.Count < 2) return (RunTexts.NotAPile, null);
            if (cmd.Count < 1 || cmd.Count >= g.Count) return (RunTexts.SplitCount(g.Count), null);
            var where = to == null ? KitSlots.Pack : cmd.Slot;
            if (!KitSlots.All.Contains(where)) return (RunTexts.NoSlot, null);
            Gear? onto;
            if (where == KitSlots.Pack)
            {
                var grid = trunk ? c.Run!.Trunk! : to == null ? c.Stash! : to.Kit!.Pack;
                onto = At(grid, cmd.X, cmd.Y)?.Gear;
                if (onto == null)
                {
                    // The pile itself stays where it is: its cells are taken.
                    var part = new Gear { Kind = g.Kind, Type = g.Type, Count = cmd.Count };
                    if (!Fits(grid, part, cmd.X, cmd.Y, cmd.Turned)) return (RunTexts.NoRoom, null);
                    return (null, () => grid.Items.Add(new Placed { Gear = Split(c, g, cmd.Count), X = cmd.X, Y = cmd.Y, Turned = cmd.Turned }));
                }
            }
            else
            {
                if (!Content.SlotTakes(where, g)) return (RunTexts.NotThere(g), null);
                var kit = to!.Kit!;
                onto = InSlot(kit, where);
                if (onto == null) return (null, () => SetSlot(kit, where, Split(c, g, cmd.Count)));
            }
            if (!SamePile(onto, g)) return (onto == g ? RunTexts.SlotTaken : where == KitSlots.Pack ? RunTexts.NoRoom : RunTexts.SlotTaken, null);
            if (Room(onto) == 0) return (RunTexts.PileFull, null);
            return (null, () => Merge(onto, g, cmd.Count));
        }

        // ------------------------------------------------------------------- drop

        /// <summary>Why `slot` can't throw a gear away (DropGearCommand), or null.</summary>
        public static string? DropRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, DropGearCommand cmd)
        {
            var error = Busy(c);
            if (error != null) return error;
            var from = Find(c, cmd.Gear);
            return from == null ? RunTexts.NoGear : ReachOf(c, slot, connected, from.Soldier, from.Trunk);
        }

        /// <summary>DropGearCommand, once DropRefusal has let it.</summary>
        public static void Drop(CampaignState c, DropGearCommand cmd)
        {
            var from = Find(c, cmd.Gear);
            if (from != null) KeepDamage(c, new[] { from.Soldier }, () => Lift(from));
        }

        // ------------------------------------------------------------------- mods

        /// <summary>Why `slot` can't mount that mod on that weapon (MountCommand), or null.</summary>
        public static string? MountRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, MountCommand cmd)
        {
            var error = Busy(c);
            if (error != null) return error;
            var mod = Find(c, cmd.Mod);
            var weapon = Find(c, cmd.Weapon);
            if (mod == null || weapon == null) return RunTexts.NoGear;
            error = ReachOf(c, slot, connected, mod.Soldier, mod.Trunk) ?? ReachOf(c, slot, connected, weapon.Soldier, weapon.Trunk);
            if (error != null) return error;
            if (mod.Gear.Kind != GearKinds.Mod) return RunTexts.NotAMod;
            if (weapon.Gear.Kind != GearKinds.Weapon) return RunTexts.NotAWeapon;
            if (!Content.TakesMods(weapon.Gear)) return RunTexts.ModsOnGuns;
            var mounted = weapon.Gear.Mods ?? new List<string>();
            if (mounted.Contains(mod.Gear.Type!)) return RunTexts.ModOn;
            if (mounted.Count >= Content.ModSlots) return RunTexts.ModsFull(Content.ModSlots);
            return null;
        }

        /// <summary>MountCommand, once MountRefusal has let it: one of the mod's pile goes onto the weapon.</summary>
        public static void Mount(CampaignState c, MountCommand cmd)
        {
            var mod = Find(c, cmd.Mod)!;
            var weapon = Find(c, cmd.Weapon)!.Gear;
            weapon.Mods = (weapon.Mods ?? new List<string>()).Append(mod.Gear.Type!).ToList();
            mod.Gear.Count -= 1;
            if (mod.Gear.Count <= 0) Lift(mod);
        }

        /// <summary>Why `slot` can't take that mod off that weapon (UnmountCommand): only in the base, into the stash.</summary>
        public static string? UnmountRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, UnmountCommand cmd)
        {
            var error = Busy(c);
            if (error != null) return error;
            if (c.Run != null) return RunTexts.UnmountAtBase;
            var weapon = Find(c, cmd.Weapon);
            if (weapon == null) return RunTexts.NoGear;
            error = Reach(c, slot, connected, weapon.Soldier);
            if (error != null) return error;
            if (weapon.Gear.Kind != GearKinds.Weapon) return RunTexts.NotAWeapon;
            if (weapon.Gear.Mods == null || !weapon.Gear.Mods.Contains(cmd.Mod)) return RunTexts.ModOff;
            return CanStow(c.Stash!, new Gear { Kind = GearKinds.Mod, Type = cmd.Mod, Count = 1 }) ? null : RunTexts.StashFull;
        }

        /// <summary>UnmountCommand, once UnmountRefusal has let it: the mod, a gear again, into the stash.</summary>
        public static void Unmount(CampaignState c, UnmountCommand cmd)
        {
            var weapon = Find(c, cmd.Weapon)!.Gear;
            weapon.Mods = weapon.Mods!.Where((m) => m != cmd.Mod).ToList();
            if (weapon.Mods.Count == 0) weapon.Mods = null;
            Stow(c.Stash!, NewGear(c, GearKinds.Mod, cmd.Mod));
        }

        // -------------------------------------------------------------------- use

        /// <summary>Why `slot` can't give that medicine to that soldier (UseGearCommand): only between nodes.</summary>
        public static string? UseGearRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, UseGearCommand cmd)
        {
            var error = Busy(c);
            if (error != null) return error;
            if (c.Run == null) return RunTexts.BetweenNodes;
            var med = Find(c, cmd.Gear);
            if (med == null) return RunTexts.NoGear;
            error = ReachOf(c, slot, connected, med.Soldier, med.Trunk);
            if (error != null) return error;
            if (med.Gear.Kind != GearKinds.Medicine) return RunTexts.NotUsable;
            var patient = c.Soldiers.FirstOrDefault((x) => x.Id == cmd.Soldier && x.Alive);
            error = patient == null ? RunTexts.NoSoldier : Reach(c, slot, connected, patient);
            if (error != null) return error;
            return Runs.HpOf(c, patient!) >= Runs.MaxHp(patient!) ? RunTexts.FullHealth : null;
        }

        /// <summary>UseGearCommand, once UseGearRefusal has let it: +3 health (Content.MedicineHeal), up to the most.</summary>
        public static void Use(CampaignState c, UseGearCommand cmd)
        {
            var med = Find(c, cmd.Gear)!;
            var patient = c.Soldiers.First((x) => x.Id == cmd.Soldier);
            c.Run!.Hp[patient.Id] = Math.Min(Runs.MaxHp(patient), Runs.HpOf(c, patient) + Content.MedicineHeal);
            med.Gear.Count -= 1;
            if (med.Gear.Count <= 0) Lift(med);
        }

        // ------------------------------------------------------------------ trade

        /// <summary>
        /// Where a player's § are at a Trade, to pay a ware from and to take a sale's: the buyer's pack first, then the rest of
        /// the squad's soldiers they control, in the squad's order, and La Lata's trunk last (cuadro 167: it pays too).
        /// </summary>
        private static List<GearGrid> Purse(CampaignState c, int slot, IReadOnlyList<int> connected, CampaignSoldier buyer)
        {
            var others = c.Run!.Squad.Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id))
                .Where((s) => s != null && s != buyer && s.Alive && s.Kit != null && Runs.Controls(c, slot, connected, s))
                .Select((s) => s!.Kit!.Pack);
            var grids = new[] { buyer.Kit!.Pack }.Concat(others).ToList();
            if (c.Run.Trunk != null) grids.Add(c.Run.Trunk);
            return grids;
        }

        /// <summary>
        /// M10 (cuadro 141): where a sale's § go, with `sold` gone from where it was: the seller's pack, else the rest of the
        /// squad's soldiers its player controls, else La Lata's trunk (Purse's order). Null if none has room.
        /// </summary>
        private static GearGrid? Till(CampaignState c, int slot, IReadOnlyList<int> connected, CampaignSoldier seller, Gear sold, int price)
        {
            return Purse(c, slot, connected, seller).FirstOrDefault((grid) => TakesSupplies(grid, sold, price));
        }

        /// <summary>
        /// What `seller` offers at a Trade, where it is: worn or in its pack, or in La Lata's trunk (Arturo, cuadro 167: «Sí,
        /// las dos cosas»), the § to its player's packs as ever.
        /// </summary>
        private static GearSpot? Offered(CampaignState c, CampaignSoldier seller, string id)
        {
            return OfKit(seller).FirstOrDefault((x) => x.Gear.Id == id) ?? All(c).FirstOrDefault((x) => x.Trunk && x.Gear.Id == id);
        }

        /// <summary>
        /// Where a ware bought goes (cuadro 167): the buyer's pack, else La Lata's trunk; never a partner's pack (Producto: the
        /// partners' packs only take a sale's §). With `emptied` gone (the piles paying it empties). Null: nowhere.
        /// </summary>
        private static GearGrid? Shelf(CampaignState c, CampaignSoldier buyer, Gear g, ICollection<Gear> emptied)
        {
            var grids = new List<GearGrid> { buyer.Kit!.Pack };
            if (c.Run?.Trunk != null) grids.Add(c.Run.Trunk);
            return grids.FirstOrDefault((grid) => CanStow(grid, g, emptied));
        }

        /// <summary>
        /// A sum of § as the piles a pack holds them in (Content.PileOf, 100), as a reward's: 108, a 100 and an 8 (Producto's
        /// review of 24/11: a pile past 100 the room won't split).
        /// </summary>
        private static List<int> SupplyPiles(int amount)
        {
            var most = Content.PileOf(GearKinds.Supplies, null);
            return Enumerable.Range(0, (amount + most - 1) / most).Select((i) => Math.Min(most, amount - i * most)).ToList();
        }

        /// <summary>Whether Stow would put all of `amount` § in the grid (SupplyPiles), with `sold` gone from it.</summary>
        private static bool TakesSupplies(GearGrid grid, Gear sold, int amount)
        {
            var copy = CloneGrid(grid);
            var at = grid.Items.FindIndex((p) => p.Gear == sold);
            if (at >= 0) copy.Items.RemoveAt(at);
            return SupplyPiles(amount).All((n) => Stow(copy, new Gear { Kind = GearKinds.Supplies, Count = n }) == 0);
        }

        /// <summary>Why `slot` can't sell that gear of that soldier at the Trade (SellGearCommand), or null.</summary>
        public static string? SellRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, SellGearCommand cmd)
        {
            var run = c.Run;
            if (run == null || run.Phase != RunPhases.Stop || run.Stop == null) return RunTexts.NotAtStop;
            if (run.Stop.Wares == null) return RunTexts.NotTrade;
            var seller = c.Soldiers.FirstOrDefault((x) => x.Id == cmd.Soldier && x.Alive);
            var error = seller == null ? RunTexts.NoSoldier : Reach(c, slot, connected, seller);
            if (error != null) return error;
            var spot = Offered(c, seller!, cmd.Gear);
            if (spot == null) return RunTexts.NoGear;
            var price = Content.SellPrice(spot.Gear, Runs.Level(c, UpgradeIds.Haggle) > 0);
            if (price <= 0) return RunTexts.NotForSale;
            return Till(c, slot, connected, seller!, spot.Gear, price) == null ? RunTexts.SuppliesNoRoom(seller!.Name, run.Trunk != null) : null;
        }

        /// <summary>
        /// SellGearCommand, once SellRefusal has let it (cuadro 141): the gear gone (worn or in the pack; a vest's health with it,
        /// KeepDamage), its § into the seller's pack (or one of its player's), the log saying so. What is sold isn't on sale
        /// after: it is gone.
        /// </summary>
        public static void Sell(CampaignState c, int slot, IReadOnlyList<int> connected, SellGearCommand cmd)
        {
            var seller = c.Soldiers.First((x) => x.Id == cmd.Soldier);
            var spot = Offered(c, seller, cmd.Gear)!;
            var price = Content.SellPrice(spot.Gear, Runs.Level(c, UpgradeIds.Haggle) > 0);
            var till = Till(c, slot, connected, seller, spot.Gear, price)!;
            KeepDamage(c, new[] { seller }, () => Lift(spot));
            foreach (var n in SupplyPiles(price)) Stow(till, NewGear(c, GearKinds.Supplies, null, n));
            Campaign.RunsLog(c, RunTexts.Sold(seller.Name, Lore.GearA(spot.Gear.Kind, spot.Gear.Type, spot.Gear.Count), price), LogTones.Good);
        }

        // ----------------------------------------------------- the base: Taller and Chatarrero

        /// <summary>
        /// Why the Taller can't make that ware (WorkshopCommand; cuadro 165), or null: only at the base, with the upgrade at the
        /// ware's level, the stash's § enough, and room for it once paid.
        /// </summary>
        public static string? WorkshopRefusal(CampaignState c, WorkshopCommand cmd)
        {
            if (c.Stash == null) return RunTexts.NoSlot;
            if (c.Run != null) return RunTexts.WorkshopAtBase;
            if (!Content.WorkshopWares.TryGetValue(cmd.Ware, out var ware)) return RunTexts.NoWorkshopWare;
            var level = Runs.Level(c, UpgradeIds.Workshop);
            if (level < ware.Level) return level == 0 ? RunTexts.NoWorkshop : RunTexts.WorkshopLevel;
            var have = Supplies(c.Stash);
            if (have < ware.Price) return RunTexts.Missing(ware.Price - have);
            var emptied = SuppliesFor(new[] { c.Stash }, ware.Price).Where((x) => x.Take == x.Pile.Gear.Count).Select((x) => x.Pile.Gear).ToList();
            return CanStow(c.Stash, new Gear { Kind = ware.Kind, Type = ware.Type, Count = ware.Count }, emptied) ? null : RunTexts.StashFull;
        }

        /// <summary>WorkshopCommand, once WorkshopRefusal has let it: paid from the stash, into it; the log says so.</summary>
        public static void Workshop(CampaignState c, WorkshopCommand cmd)
        {
            var ware = Content.WorkshopWares[cmd.Ware];
            PaySupplies(new[] { c.Stash! }, ware.Price);
            Stow(c.Stash!, NewGear(c, ware.Kind, ware.Type, ware.Count));
            Campaign.RunsLog(c, RunTexts.Made(Lore.GearA(ware.Kind, ware.Type, ware.Count), ware.Price), LogTones.Good);
        }

        /// <summary>
        /// Why the Chatarrero won't take that gear (ScrapGearCommand; cuadro 165), or null: only at the base, with the upgrade,
        /// a gear of the stash, one it pays for (Content.ScrapPrice), and room for its § once it is gone.
        /// </summary>
        public static string? ScrapRefusal(CampaignState c, ScrapGearCommand cmd)
        {
            if (c.Stash == null) return RunTexts.NoSlot;
            if (c.Run != null) return RunTexts.ScrapAtBase;
            if (Runs.Level(c, UpgradeIds.Scrap) == 0) return RunTexts.NoScrap;
            var spot = Find(c, cmd.Gear);
            if (spot == null || spot.Grid != c.Stash) return RunTexts.ScrapStashOnly;
            var price = Content.ScrapPrice(spot.Gear);
            if (price <= 0) return RunTexts.NotForSale;
            return TakesSupplies(c.Stash, spot.Gear, price) ? null : RunTexts.StashFull;
        }

        /// <summary>ScrapGearCommand, once ScrapRefusal has let it: the gear gone, its § into the stash; the log says so.</summary>
        public static void Scrap(CampaignState c, ScrapGearCommand cmd)
        {
            var spot = Find(c, cmd.Gear)!;
            var price = Content.ScrapPrice(spot.Gear);
            Lift(spot);
            foreach (var n in SupplyPiles(price)) Stow(c.Stash!, NewGear(c, GearKinds.Supplies, null, n));
            Campaign.RunsLog(c, RunTexts.Scrapped(Lore.GearA(spot.Gear.Kind, spot.Gear.Type, spot.Gear.Count), price), LogTones.Good);
        }

        /// <summary>Why `slot` can't buy that ware for that soldier (BuyWareCommand), or null.</summary>
        public static string? BuyWareRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, BuyWareCommand cmd)
        {
            var run = c.Run;
            if (run == null || run.Phase != RunPhases.Stop || run.Stop == null) return RunTexts.NotAtStop;
            if (run.Stop.Wares == null) return RunTexts.NotTrade;
            var ware = run.Stop.Wares.FirstOrDefault((w) => w.Gear.Id == cmd.Ware);
            if (ware == null) return RunTexts.NoWare;
            var buyer = c.Soldiers.FirstOrDefault((x) => x.Id == cmd.Soldier && x.Alive);
            var error = buyer == null ? RunTexts.NoSoldier : Reach(c, slot, connected, buyer);
            if (error != null) return error;
            var purse = Purse(c, slot, connected, buyer!);
            var have = purse.Sum(Supplies);
            if (have < ware.Price) return RunTexts.Missing(ware.Price - have);
            return WhereBought(c, slot, connected, buyer!, ware) != null ? null : RunTexts.NoRoomToBuy(buyer!.Name, run.Trunk != null);
        }

        /// <summary>
        /// Where a ware will go once bought (cuadro 167), for the screen before buying it: the buyer's id (its pack) or `trunk`
        /// (La Lata's); null if it can't be bought.
        /// </summary>
        public static string? BuyTo(CampaignState c, int slot, IReadOnlyList<int> connected, BuyWareCommand cmd)
        {
            if (BuyWareRefusal(c, slot, connected, cmd) != null) return null;
            var ware = c.Run!.Stop!.Wares!.First((w) => w.Gear.Id == cmd.Ware);
            var buyer = c.Soldiers.First((x) => x.Id == cmd.Soldier);
            var grid = WhereBought(c, slot, connected, buyer, ware)!;
            return grid == c.Run.Trunk ? GearPlaces.Trunk : buyer.Id;
        }

        /// <summary>Where a ware bought goes (Shelf), with what paying for it empties (the piles it takes whole) gone.</summary>
        private static GearGrid? WhereBought(CampaignState c, int slot, IReadOnlyList<int> connected, CampaignSoldier buyer, Ware ware)
        {
            var purse = Purse(c, slot, connected, buyer);
            var emptied = SuppliesFor(purse, ware.Price).Where((x) => x.Take == x.Pile.Gear.Count).Select((x) => x.Pile.Gear).ToList();
            return Shelf(c, buyer, ware.Gear, emptied);
        }

        /// <summary>
        /// BuyWareCommand, once BuyWareRefusal has let it: paid (Purse's order, La Lata's trunk last), off the list, into the
        /// buyer's pack, else La Lata's trunk (cuadro 167; Shelf).
        /// </summary>
        public static void BuyWare(CampaignState c, int slot, IReadOnlyList<int> connected, BuyWareCommand cmd)
        {
            var wares = c.Run!.Stop!.Wares!;
            var ware = wares.First((w) => w.Gear.Id == cmd.Ware);
            var buyer = c.Soldiers.First((x) => x.Id == cmd.Soldier);
            var grid = WhereBought(c, slot, connected, buyer, ware)!;
            PaySupplies(Purse(c, slot, connected, buyer), ware.Price);
            wares.Remove(ware);
            KeepDamage(c, new[] { buyer }, () => Stow(grid, ware.Gear));
        }
    }
}
