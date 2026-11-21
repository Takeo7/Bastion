#nullable enable
// A run's soldier's pack in a fight (H3; new in C#): the backpack orders of before (ItemCommand: drop, take, give, use,
// equip) on gear by its id, and «arrange» within one's own pack; what each refuses (Refusal, which the UI calls too),
// what the host makes of them (Act) and what every copy applies (Apply). And what follows from the kit: Unit.Arms, the
// belt's charges and the vest's health (Refit), the magazines (SyncMags, SpendMag), what comes home off the belt
// (BeltLeft) and what a fallen soldier leaves on the floor (Fallen).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class RunPack
    {
        // --------------------------------------------------------------- texts

        /// <summary>The refusals of the fight's pack (Lore, 16/11).</summary>
        public static class Texts
        {
            public const string NoPack = "Ese soldado no lleva mochila.";
            public const string NoGear = "No lleva eso.";
            public const string NotHere = "Aquí no hay eso.";
            public const string NoRoom = "No cabe en la mochila.";
            public const string NoRoomThere = "No cabe ahí.";
            public const string PackOnly = "Solo lo de la mochila.";
            public const string BeltStays = "Lo del cinturón no se quita en combate.";
            public const string BeltTaken = "Ese hueco del cinturón está ocupado.";
            public const string NotWorn = "Eso no se lleva puesto.";
            public const string PileFull = "Esa pila está llena.";
            public const string OnlyMedicine = "De la mochila, solo se usan las medicinas.";
            public const string NoActions = "No le quedan acciones.";
        }

        // ---------------------------------------------------------- what is where

        /// <summary>Whether an order of the pack takes no action: dropping, taking, arranging, and a medicine with Manos de enfermero.</summary>
        public static bool Free(Unit u, string action)
        {
            return action == ItemActions.Drop || action == ItemActions.Take || action == ItemActions.Arrange || (action == ItemActions.Use && Rules.FreeMedicine(u));
        }

        public static Placed? InPack(Unit u, string? id)
        {
            return id == null ? null : u.Pack?.Items.FirstOrDefault((p) => p.Gear.Id == id);
        }

        /// <summary>The kit place a gear with this id is in, or null.</summary>
        public static string? SlotOf(Unit u, string? id)
        {
            if (u.Kit == null || id == null) return null;
            return new[] { KitSlots.Primary, KitSlots.Melee, KitSlots.Pistol, KitSlots.Vest, KitSlots.Belt0, KitSlots.Belt1 }.FirstOrDefault((slot) => Gears.InSlot(u.Kit, slot)?.Id == id);
        }

        public static DroppedGear? OnFloor(GameState s, string? id)
        {
            return id == null ? null : s.FloorGear?.FirstOrDefault((d) => d.Gear.Id == id);
        }

        /// <summary>
        /// Whether `u` can take `left` up from where it stands (ItemActions.Take, free): what lies on its tile; and what a
        /// fallen comrade left (DroppedGear.From) from beside its body as well, as far as Registrar reaches, past nothing
        /// solid (Looting.ReachesToOpen; Gancho two off): G16, Arturo 18/11, «registrar el cuerpo de mis aliados caídos».
        /// </summary>
        public static bool InReach(GameState s, Unit u, DroppedGear left)
        {
            if (Grid.SamePos(left.Pos, u.Pos)) return true;
            return left.From != null && left.Pos.Z == u.Pos.Z && Looting.ReachesToOpen(s, u.Pos, left.Pos, 1 + Math.Max(0, u.Mods.SearchReach));
        }

        /// <summary>What `u` can take up now (InReach): the client's floor beside the pack.</summary>
        public static List<DroppedGear> FloorInReach(GameState s, Unit u)
        {
            return (s.FloorGear ?? new List<DroppedGear>()).Where((d) => InReach(s, u, d)).ToList();
        }

        /// <summary>
        /// Where `g` goes in `grid`: at x, y (onto the equal pile lying there if it has room, or laid there if it fits); without
        /// them, onto an equal pile that takes it all, where it first fits, or onto one that takes some. Null: nowhere.
        /// </summary>
        public static (int X, int Y, bool Turned)? Spot(GearGrid grid, Gear g, int? x, int? y, bool? turned)
        {
            if (x != null && y != null)
            {
                var there = Gears.At(grid, x.Value, y.Value);
                if (there != null && there.Gear != g && Gears.SamePile(there.Gear, g)) return Gears.Room(there.Gear) > 0 ? (there.X, there.Y, there.Turned) : ((int, int, bool)?)null;
                return Gears.Fits(grid, g, x.Value, y.Value, turned == true) ? (x.Value, y.Value, turned == true) : ((int, int, bool)?)null;
            }
            var pile = grid.Items.FirstOrDefault((p) => p.Gear != g && Gears.SamePile(p.Gear, g) && Gears.Room(p.Gear) >= g.Count);
            if (pile != null) return (pile.X, pile.Y, pile.Turned);
            var at = Gears.FirstFit(grid, g);
            if (at != null) return (at.X, at.Y, at.Turned);
            pile = grid.Items.FirstOrDefault((p) => p.Gear != g && Gears.SamePile(p.Gear, g) && Gears.Room(p.Gear) > 0);
            return pile != null ? (pile.X, pile.Y, pile.Turned) : ((int, int, bool)?)null;
        }

        /// <summary>
        /// Into a pack at x, y: poured onto the equal pile lying there (as much as it takes, the rest left in `g`), or laid
        /// there (moved, if it already lay in that pack). True if `g` itself lies in the pack now.
        /// </summary>
        public static bool Put(GearGrid grid, Gear g, int x, int y, bool turned)
        {
            var there = Gears.At(grid, x, y);
            if (there != null && there.Gear != g && Gears.SamePile(there.Gear, g))
            {
                Gears.Merge(there.Gear, g);
                return false;
            }
            var own = grid.Items.FirstOrDefault((p) => p.Gear == g);
            if (own != null)
            {
                own.X = x;
                own.Y = y;
                own.Turned = turned;
                return true;
            }
            grid.Items.Add(new Placed { Gear = g, X = x, Y = y, Turned = turned });
            return true;
        }

        /// <summary>
        /// The kit place a gear goes to when equipped: a weapon's by its kind, a vest's, and for the belt the one asked
        /// for, or the one with an equal pile that has room, or the first empty one. Null: it isn't worn.
        /// </summary>
        public static string? SlotFor(Unit u, Gear g, string? want)
        {
            var weapon = Content.GearWeapon(g);
            if (weapon != null) return weapon.Slot == FoundWeaponSlots.Primary ? KitSlots.Primary : weapon.Slot == FoundWeaponSlots.Melee ? KitSlots.Melee : KitSlots.Pistol;
            if (g.Kind == GearKinds.Vest) return KitSlots.Vest;
            if (g.Kind != GearKinds.Item || u.Kit == null) return null;
            if (want == KitSlots.Belt0 || want == KitSlots.Belt1) return want;
            var belts = new[] { KitSlots.Belt0, KitSlots.Belt1 };
            return belts.FirstOrDefault((b) => Gears.InSlot(u.Kit, b) is Gear on && Gears.SamePile(on, g) && Gears.Room(on) > 0) ?? belts.FirstOrDefault((b) => Gears.InSlot(u.Kit, b) == null) ?? want;
        }

        // ---------------------------------------------------------------- refusals

        /// <summary>Why a run's soldier can't do that with its pack (ItemCommand with `gear`), or null. As BackpackRefusal for the rest.</summary>
        public static string? Refusal(GameState s, ItemCommand cmd, int slot, IReadOnlyList<int> connected)
        {
            var u = Grid.GetUnit(s, cmd.Unit);
            if (u == null || !u.Alive) return "Esa unidad no existe o está fuera de combate.";
            if (u.Team != Teams.Xcom || u.Captive || u.Objective || u.Automaton != null) return "No puedes dar órdenes a esa unidad.";
            if (!Content.CommandsFor(slot, u.Owner, connected)) return "Es de la escuadra del otro jugador.";
            if (!string.IsNullOrEmpty(u.ControlledBy) || u.Panicked) return "Ese soldado no responde.";
            if (u.Pack == null || u.Kit == null) return Texts.NoPack;
            if (!ItemActions.All.Contains(cmd.Action)) return Texts.NoGear;
            if (!Free(u, cmd.Action) && u.Ap <= 0) return Texts.NoActions;
            var id = cmd.Gear ?? cmd.Id;
            if (cmd.Action == ItemActions.Take)
            {
                var left = OnFloor(s, id);
                if (left == null || !InReach(s, u, left)) return Texts.NotHere;
                return Spot(u.Pack, left.Gear, cmd.X, cmd.Y, cmd.Turned) == null ? (cmd.X != null ? Texts.NoRoomThere : Texts.NoRoom) : null;
            }
            var placed = InPack(u, id);
            var worn = SlotOf(u, id);
            if (placed == null && worn == null) return Texts.NoGear;
            switch (cmd.Action)
            {
                case ItemActions.Drop:
                    return placed == null ? Texts.PackOnly : null;
                case ItemActions.Give:
                {
                    if (placed == null) return Texts.PackOnly;
                    var to = !string.IsNullOrEmpty(cmd.To) ? Grid.GetUnit(s, cmd.To!) : null;
                    if (to == null || !to.Alive || to.Team != Teams.Xcom || to.Captive || to.Objective || to.Id == u.Id || to.Pack == null) return "Solo a un compañero.";
                    if (!Aim.InMeleeReach(s, u.Pos, to.Pos)) return "Tiene que estar a tu lado.";
                    return Spot(to.Pack, placed.Gear, null, null, null) == null ? "No le cabe." : null;
                }
                case ItemActions.Use:
                {
                    if (placed == null) return Texts.PackOnly;
                    // Mordido's antiviral (G23.2): on the bitten only, beside (or the bitten on themselves), once.
                    if (placed.Gear.Kind == GearKinds.Antiviral)
                    {
                        var bitten = !string.IsNullOrEmpty(cmd.To) ? Grid.GetUnit(s, cmd.To!) : u;
                        if (bitten == null || !bitten.Alive || s.Mission.Bitten != true || bitten.Id != s.Mission.ObjectiveUnit) return Lore.BiteText.AntiviralOnlyBite;
                        if (s.Mission.Cured == true) return Lore.BiteText.AlreadyCured;
                        if (bitten != u && !Aim.InMeleeReach(s, u.Pos, bitten.Pos)) return "Tiene que estar a tu lado.";
                        return null;
                    }
                    if (placed.Gear.Kind != GearKinds.Medicine) return Texts.OnlyMedicine;
                    var to = !string.IsNullOrEmpty(cmd.To) ? Grid.GetUnit(s, cmd.To!) : u;
                    if (to == null || !to.Alive || to.Team != Teams.Xcom || to.Objective) return "Solo en ti o en un compañero.";
                    if (to != u && !Aim.InMeleeReach(s, u.Pos, to.Pos)) return "Tiene que estar a tu lado.";
                    // On the bitten not cured (G23.2), the medicine is no antiviral: say so (Lore).
                    if (to.Hp >= to.MaxHp) return Rules.BittenUncured(s, to) ? Lore.BiteText.MedicineNo : to == u ? "No tienes heridas." : "No tiene heridas.";
                    return null;
                }
                case ItemActions.Arrange:
                {
                    if (worn == KitSlots.Belt0 || worn == KitSlots.Belt1) return Texts.BeltStays;
                    var g = placed?.Gear ?? Gears.InSlot(u.Kit, worn!)!;
                    return Spot(u.Pack, g, cmd.X, cmd.Y, cmd.Turned) == null ? (cmd.X != null ? Texts.NoRoomThere : Texts.NoRoom) : null;
                }
                case ItemActions.Equip:
                {
                    if (placed == null) return Texts.PackOnly;
                    var g = placed.Gear;
                    var at = SlotFor(u, g, cmd.Slot);
                    if (at == null || !Content.SlotTakes(at, g)) return Texts.NotWorn;
                    var now = Gears.InSlot(u.Kit, at);
                    if (now == null) return null;
                    var belt = at == KitSlots.Belt0 || at == KitSlots.Belt1;
                    if (belt) return Gears.SamePile(now, g) ? (Gears.Room(now) > 0 ? null : Texts.PileFull) : Texts.BeltTaken;
                    return SwapBack(u.Pack, placed, now) == null ? RunTexts.SwapNoRoom(now, g) : null;
                }
            }
            return null;
        }

        /// <summary>Where what a kit place holds goes when `placed` takes its place: where that lay, unturned or turned; null if neither fits.</summary>
        private static (int X, int Y, bool Turned)? SwapBack(GearGrid pack, Placed placed, Gear now)
        {
            foreach (var turned in new[] { false, true })
            {
                if (Gears.Fits(pack, now, placed.X, placed.Y, turned, new[] { placed.Gear })) return (placed.X, placed.Y, turned);
            }
            return null;
        }

        // --------------------------------------------------------------- the host

        /// <summary>A pack order already checked (Refusal): its events.</summary>
        public static void Act(Sim sim, ItemCommand cmd)
        {
            var s = sim.S;
            var u = Grid.GetUnit(s, cmd.Unit)!;
            var id = (cmd.Gear ?? cmd.Id)!;
            switch (cmd.Action)
            {
                case ItemActions.Drop:
                    sim.Emit(new GearMovedEvent { Unit = u.Id, Gear = id, Action = ItemActions.Drop, Ap = 0 });
                    break;
                case ItemActions.Take:
                {
                    var at = Spot(u.Pack!, OnFloor(s, id)!.Gear, cmd.X, cmd.Y, cmd.Turned)!.Value;
                    sim.Emit(new GearMovedEvent { Unit = u.Id, Gear = id, Action = ItemActions.Take, X = at.X, Y = at.Y, Turned = at.Turned, Ap = 0 });
                    break;
                }
                case ItemActions.Give:
                {
                    var to = Grid.GetUnit(s, cmd.To!)!;
                    var at = Spot(to.Pack!, InPack(u, id)!.Gear, null, null, null)!.Value;
                    sim.Emit(new GearMovedEvent { Unit = u.Id, Gear = id, Action = ItemActions.Give, To = to.Id, X = at.X, Y = at.Y, Turned = at.Turned, Ap = 1 });
                    break;
                }
                case ItemActions.Arrange:
                {
                    var g = InPack(u, id)?.Gear ?? Gears.InSlot(u.Kit!, SlotOf(u, id)!)!;
                    var at = Spot(u.Pack!, g, cmd.X, cmd.Y, cmd.Turned)!.Value;
                    sim.Emit(new GearMovedEvent { Unit = u.Id, Gear = id, Action = ItemActions.Arrange, X = at.X, Y = at.Y, Turned = at.Turned, Ap = 0 });
                    break;
                }
                case ItemActions.Equip:
                {
                    var placed = InPack(u, id)!;
                    var slot = SlotFor(u, placed.Gear, cmd.Slot)!;
                    var now = Gears.InSlot(u.Kit!, slot);
                    var back = now != null && !Gears.SamePile(now, placed.Gear) ? SwapBack(u.Pack!, placed, now) : null;
                    sim.Emit(new GearMovedEvent { Unit = u.Id, Gear = id, Action = ItemActions.Equip, Slot = slot, X = back?.X, Y = back?.Y, Turned = back?.Turned, Ap = 1 });
                    break;
                }
                case ItemActions.Use:
                {
                    var target = !string.IsNullOrEmpty(cmd.To) ? Grid.GetUnit(s, cmd.To!)! : u;
                    var antiviral = InPack(u, id)?.Gear.Kind == GearKinds.Antiviral;
                    sim.Emit(new GearUsedEvent { Unit = u.Id, Target = target.Id, Gear = id, Free = Rules.FreeMedicine(u) ? true : (bool?)null });
                    // The antiviral cures the bite (Events: MissionState.Cured), not wounds.
                    if (antiviral) break;
                    var amount = Math.Min(Content.MedicineHeal + u.Mods.Heal, target.MaxHp - target.Hp);
                    if (amount > 0) sim.Emit(new HealedEvent { Unit = u.Id, Target = target.Id, Amount = amount });
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- applying

        /// <summary>GearMovedEvent on any copy of the state.</summary>
        public static void Apply(GameState s, GearMovedEvent ev)
        {
            var u = Grid.GetUnit(s, ev.Unit);
            if (u == null || u.Pack == null || u.Kit == null) return;
            u.Ap = Math.Max(0, u.Ap - ev.Ap);
            var x = ev.X ?? 0;
            var y = ev.Y ?? 0;
            var turned = ev.Turned == true;
            switch (ev.Action)
            {
                case ItemActions.Take:
                {
                    var d = OnFloor(s, ev.Gear);
                    if (d == null) break;
                    if (Put(u.Pack, d.Gear, x, y, turned) || d.Gear.Count <= 0) s.FloorGear!.Remove(d);
                    break;
                }
                case ItemActions.Drop:
                {
                    var p = InPack(u, ev.Gear);
                    if (p == null) break;
                    u.Pack.Items.Remove(p);
                    (s.FloorGear ??= new List<DroppedGear>()).Add(new DroppedGear { Gear = p.Gear, Pos = Events.CopyPos(u.Pos) });
                    break;
                }
                case ItemActions.Give:
                {
                    var to = !string.IsNullOrEmpty(ev.To) ? Grid.GetUnit(s, ev.To!) : null;
                    var p = InPack(u, ev.Gear);
                    if (to?.Pack == null || p == null) break;
                    if (Put(to.Pack, p.Gear, x, y, turned) || p.Gear.Count <= 0) u.Pack.Items.Remove(p);
                    SyncMags(to);
                    break;
                }
                case ItemActions.Arrange:
                {
                    var p = InPack(u, ev.Gear);
                    if (p != null)
                    {
                        if (!Put(u.Pack, p.Gear, x, y, turned) && p.Gear.Count <= 0) u.Pack.Items.Remove(p);
                        break;
                    }
                    var slot = SlotOf(u, ev.Gear);
                    if (slot == null) break;
                    var g = Gears.InSlot(u.Kit, slot)!;
                    Gears.SetSlot(u.Kit, slot, null);
                    Put(u.Pack, g, x, y, turned);
                    Refit(u, slot, g, null);
                    break;
                }
                case ItemActions.Equip:
                {
                    var p = InPack(u, ev.Gear);
                    if (p == null || ev.Slot == null) break;
                    var now = Gears.InSlot(u.Kit, ev.Slot);
                    if (now != null && Gears.SamePile(now, p.Gear))
                    {
                        var n = Gears.Merge(now, p.Gear);
                        Belted(u, now.Type!, n);
                        if (p.Gear.Count <= 0) u.Pack.Items.Remove(p);
                        break;
                    }
                    u.Pack.Items.Remove(p);
                    Gears.SetSlot(u.Kit, ev.Slot, p.Gear);
                    if (now != null) u.Pack.Items.Add(new Placed { Gear = now, X = ev.X ?? p.X, Y = ev.Y ?? p.Y, Turned = ev.Turned == true });
                    if (ev.Slot == KitSlots.Belt0 || ev.Slot == KitSlots.Belt1) Belted(u, p.Gear.Type!, p.Gear.Count);
                    else Refit(u, ev.Slot, now, p.Gear);
                    break;
                }
            }
            SyncMags(u);
        }

        /// <summary>GearUsedEvent: one off the medicine's pile, and an action unless it was free (the healing is `healed`'s).</summary>
        public static void Apply(GameState s, GearUsedEvent ev)
        {
            var u = Grid.GetUnit(s, ev.Unit);
            if (u == null) return;
            if (ev.Free != true) u.Ap = Math.Max(0, u.Ap - 1);
            var p = InPack(u, ev.Gear);
            if (p == null) return;
            p.Gear.Count -= 1;
            if (p.Gear.Count <= 0) u.Pack!.Items.Remove(p);
        }

        // ------------------------------------------------------------- the kit

        /// <summary>The uses `count` of a belt thing give, by charge (Content.Items' charges, each item its own).</summary>
        private static IEnumerable<KeyValuePair<string, int>> UsesOf(string item, int count)
        {
            var charges = Content.Items.TryGetValue(item, out var def) ? def.Charges : null;
            if (charges == null) return new KeyValuePair<string, int>[0];
            return charges.Select((e) => new KeyValuePair<string, int>(e.Key, e.Value * count)).ToList();
        }

        /// <summary>`count` of a belt thing more on the belt: their uses, and what it lets do (Unit.Gear).</summary>
        public static void Belted(Unit u, string item, int count)
        {
            foreach (var e in UsesOf(item, count)) u.Charges[e.Key] = (u.Charges.TryGetValue(e.Key, out var n) ? n : 0) + e.Value;
            if (!u.Gear.Contains(item)) u.Gear.Add(item);
        }

        /// <summary>
        /// A kit place's gear changed from `before` to `after` (a weapon or the vest): the soldier's arms, loaded, and the
        /// vest's health and armour, the damage it had kept.
        /// </summary>
        public static void Refit(Unit u, string slot, Gear? before, Gear? after)
        {
            var arms = u.Arms ??= new Arms();
            switch (slot)
            {
                case KitSlots.Primary:
                {
                    var had = !string.IsNullOrEmpty(arms.Primary);
                    arms.Primary = after?.Type;
                    arms.PrimaryId = after?.Id;
                    arms.Mods = after?.Mods != null && after.Mods.Count > 0 ? new List<string>(after.Mods) : null;
                    arms.Condition = null;
                    if (after != null)
                    {
                        // The pistol's rounds were the main gun's: they stay with it at the side.
                        if (!had && arms.Pistol == true) u.PistolAmmo = u.Ammo;
                        u.Ammo = Content.ClipOf(u, AbilityIds.Shoot);
                    }
                    else if (arms.Pistol == true)
                    {
                        u.Ammo = u.PistolAmmo ?? Content.PistolClip;
                        u.PistolAmmo = null;
                    }
                    else u.Ammo = 0;
                    u.WeaponName = WeaponName(u.Kit);
                    break;
                }
                case KitSlots.Melee:
                    arms.Melee = after == null || after.Type == FoundWeaponIds.Knife ? null : after.Type;
                    arms.MeleeId = after?.Id;
                    u.WeaponName = WeaponName(u.Kit);
                    break;
                case KitSlots.Pistol:
                    arms.Pistol = after != null;
                    arms.PistolId = after?.Id;
                    if (string.IsNullOrEmpty(arms.Primary)) u.Ammo = after != null ? Content.PistolClip : 0;
                    else u.PistolAmmo = after != null ? Content.PistolClip : (int?)null;
                    u.WeaponName = WeaponName(u.Kit);
                    break;
                case KitSlots.Vest:
                {
                    var was = VestOf(before);
                    var now = VestOf(after);
                    var d = now.Hp - was.Hp;
                    u.MaxHp += d;
                    u.Hp = Math.Max(1, Math.Min(u.MaxHp, u.Hp + d));
                    u.Armor = Math.Max(0, u.Armor + now.Armor - was.Armor);
                    break;
                }
            }
        }

        private static (int Hp, int Armor) VestOf(Gear? g)
        {
            return g?.Type != null && Content.Vests.TryGetValue(g.Type, out var def) ? (def.Hp, def.Armor) : (0, 0);
        }

        /// <summary>The card's weapon of a kit: the one in the hand, else the pistol, else the blade.</summary>
        public static string WeaponName(IKitPlaces? kit)
        {
            var weapon = kit?.Primary ?? kit?.Pistol ?? kit?.Melee;
            return weapon != null ? Content.GearWeapon(weapon)?.Name ?? Content.Knife.Name : Content.Knife.Name;
        }

        /// <summary>The magazines a run's soldier counts are those in its pack (Unit.Mags); its arrows too (SyncArrows).</summary>
        public static void SyncMags(Unit u)
        {
            if (u.Pack == null) return;
            u.Mags = u.Pack.Items.Where((p) => p.Gear.Kind == GearKinds.Mags).Sum((p) => p.Gear.Count);
            SyncArrows(u);
        }

        /// <summary>Arco y ballesta (cuadro 102): the arrows a run's soldier counts are those in its pack (Unit.Arrows), none or many.</summary>
        public static void SyncArrows(Unit u)
        {
            if (u.Pack == null) return;
            u.Arrows = u.Pack.Items.Where((p) => p.Gear.Kind == GearKinds.Arrows).Sum((p) => p.Gear.Count);
        }

        /// <summary>A bow's shot or a crossbow's reload takes an arrow out of the pack: off the smallest pile.</summary>
        public static void SpendArrow(Unit u)
        {
            if (u.Pack == null) return;
            var pile = u.Pack.Items.Where((p) => p.Gear.Kind == GearKinds.Arrows).OrderBy((p) => p.Gear.Count).FirstOrDefault();
            if (pile != null)
            {
                pile.Gear.Count -= 1;
                if (pile.Gear.Count <= 0) u.Pack.Items.Remove(pile);
            }
            SyncArrows(u);
        }

        /// <summary>Reloading takes a magazine out of the pack: off the smallest pile.</summary>
        public static void SpendMag(Unit u)
        {
            if (u.Pack == null) return;
            var pile = u.Pack.Items.Where((p) => p.Gear.Kind == GearKinds.Mags).OrderBy((p) => p.Gear.Count).FirstOrDefault();
            if (pile != null)
            {
                pile.Gear.Count -= 1;
                if (pile.Gear.Count <= 0) u.Pack.Items.Remove(pile);
            }
            SyncMags(u);
        }

        /// <summary>
        /// What is left on the belt: each thing's uses still there (the board's extra uses spent first), as many of it as
        /// they make, rounded up; a place used up, empty.
        /// </summary>
        public static Gear?[] BeltLeft(Unit u)
        {
            var belt = u.Kit?.Belt ?? new Gear?[2];
            var out_ = new Gear?[2];
            var left = new Dictionary<string, int>();
            // The belt's own uses of each charge, at most what the soldier has left.
            foreach (var g in belt)
            {
                if (g?.Type == null) continue;
                foreach (var e in UsesOf(g.Type, g.Count)) left[e.Key] = (left.TryGetValue(e.Key, out var n) ? n : 0) + e.Value;
            }
            foreach (var key in left.Keys.ToList()) left[key] = Math.Min(left[key], u.Charges.TryGetValue(key, out var has) ? has : 0);
            for (var i = 0; i < belt.Length && i < 2; i++)
            {
                var g = belt[i];
                if (g?.Type == null) continue;
                var per = UsesOf(g.Type, 1).ToList();
                if (per.Count == 0)
                {
                    out_[i] = Gears.CloneGear(g);
                    continue;
                }
                var count = 0;
                foreach (var e in per)
                {
                    var keep = Math.Min(e.Value * g.Count, left.TryGetValue(e.Key, out var n) ? n : 0);
                    left[e.Key] = (left.TryGetValue(e.Key, out var m) ? m : 0) - keep;
                    count = Math.Max(count, (int)Math.Ceiling(keep / (double)e.Value));
                }
                if (count <= 0) continue;
                var back = Gears.CloneGear(g);
                back.Count = count;
                out_[i] = back;
            }
            return out_;
        }

        /// <summary>
        /// A run's soldier fell (DiedEvent): its kit (the belt with what is left of it) and its pack, on the floor where it
        /// lies, `from` it, for another to take.
        /// </summary>
        public static void Fallen(GameState s, Unit u, Pos at)
        {
            if (u.Pack == null || u.Kit == null) return;
            var all = new List<Gear?> { u.Kit.Primary, u.Kit.Melee, u.Kit.Pistol, u.Kit.Vest }.Concat(BeltLeft(u)).Concat(u.Pack.Items.Select((p) => p.Gear));
            foreach (var g in all)
            {
                if (g != null) (s.FloorGear ??= new List<DroppedGear>()).Add(new DroppedGear { Gear = g, Pos = Events.CopyPos(at), From = u.Id });
            }
            u.Kit = new KitPlaces();
            u.Pack.Items.Clear();
            SyncMags(u);
        }
    }
}
