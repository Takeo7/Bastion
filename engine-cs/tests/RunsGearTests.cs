using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The gear commands of a run campaign (H0, H0-DISENO §5, Campaign/Gears.cs): moving, swapping, piling, splitting,
    /// throwing away, mounting and taking off mods; who may touch what (the stash only in the base, one's own soldiers,
    /// the squad's during a run, nothing in a fight); and that each refusal is the command's own.
    /// </summary>
    public class RunsGearTests
    {
        private static MoveGearCommand Move(Gear g, string to, string slot = KitSlots.Pack, int x = 0, int y = 0, bool turned = false)
        {
            return new MoveGearCommand { Gear = g.Id, To = to, Slot = slot, X = x, Y = y, Turned = turned };
        }

        // ------------------------------------------------------------------- move

        [Fact]
        public void IntoAGridWhereItFits()
        {
            var c = Solo();
            var s = Soldier(c);
            var rifle = InStash(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 0);
            Refused(RunTexts.NoRoom, c, Move(rifle, s.Id, x: 1, y: 0));
            Refused(RunTexts.NoRoom, c, Move(rifle, s.Id, x: 0, y: 0, turned: true));
            Assert.Null(Gears.MoveRefusal(c, 0, Zero, Move(rifle, s.Id, x: 0, y: 2)));
            Ok(c, Move(rifle, s.Id, x: 0, y: 2));
            Assert.Empty(c.Stash!.Items);
            var p = s.Kit!.Pack.Items.Single();
            Assert.Equal((rifle, 0, 2, false), (p.Gear, p.X, p.Y, p.Turned));
            // On something else: no; a step within its own cells: yes.
            var med = InPack(c, s, GearKinds.Medicine, null, 0, 0);
            Refused(RunTexts.NoRoom, c, Move(med, s.Id, x: 1, y: 2));
            Ok(c, Move(med, s.Id, x: 1, y: 0));
            // Back to the stash, turned.
            Ok(c, Move(rifle, GearPlaces.Stash, x: 9, y: 2, turned: true));
            Assert.Equal((9, 2, true), (c.Stash.Items[0].X, c.Stash.Items[0].Y, c.Stash.Items[0].Turned));
            Refused(RunTexts.NoRoom, c, Move(med, GearPlaces.Stash, x: 9, y: 5));
            Refused(RunTexts.NoGear, c, new MoveGearCommand { Gear = "g999", To = GearPlaces.Stash });
            Refused(RunTexts.NoSoldier, c, Move(med, "c999"));
            Refused(RunTexts.NoSlot, c, Move(med, s.Id, "hat"));
        }

        [Fact]
        public void IntoAKitPlace()
        {
            var c = Solo();
            var s = Soldier(c);
            var rifle = InStash(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 0);
            var grenade = InStash(c, GearKinds.Item, ItemIds.FragGrenade, 0, 1, 2);
            Refused("El fusil de asalto no va en ese hueco.", c, Move(rifle, s.Id, KitSlots.Belt0));
            Refused("La granada no va en ese hueco.", c, Move(grenade, s.Id, KitSlots.Primary));
            Ok(c, Move(rifle, s.Id, KitSlots.Primary));
            Ok(c, Move(grenade, s.Id, KitSlots.Belt1));
            Assert.Same(rifle, s.Kit!.Primary);
            Assert.Same(grenade, s.Kit.Belt[1]);
            Assert.Empty(c.Stash!.Items);
            // From a kit place to the pack.
            Ok(c, Move(grenade, s.Id, x: 3, y: 2));
            Assert.Null(s.Kit.Belt[1]);
            // On itself: nothing.
            Ok(c, Move(rifle, s.Id, KitSlots.Primary));
            Assert.Same(rifle, s.Kit.Primary);
        }

        [Fact]
        public void OntoATakenKitPlaceTheySwap()
        {
            var c = Solo();
            var s = Soldier(c);
            var knife = s.Kit!.Melee!;
            var machete = InStash(c, GearKinds.Weapon, FoundWeaponIds.Machete, 4, 3);
            Ok(c, Move(machete, s.Id, KitSlots.Melee));
            Assert.Same(machete, s.Kit.Melee);
            var back = c.Stash!.Items.Single();
            Assert.Equal((knife, 4, 3, false), (back.Gear, back.X, back.Y, back.Turned));
            // Turned if that makes it fit.
            var bat = InSlot(c, s, KitSlots.Melee, GearKinds.Weapon, FoundWeaponIds.Bat);
            var pan = InPack(c, s, GearKinds.Weapon, FoundWeaponIds.Pan, 3, 0, turned: true);
            Ok(c, Move(pan, s.Id, KitSlots.Melee));
            Assert.Same(pan, s.Kit.Melee);
            var lying = s.Kit.Pack.Items.Single();
            Assert.Equal((bat, 3, 0, true), (lying.Gear, lying.X, lying.Y, lying.Turned));
            // Not if it doesn't fit there either way.
            var mg = InSlot(c, s, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.MachineGun);
            s.Kit.Pack.Items.Clear();
            var bottom = InPack(c, s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 2);
            Refused("La ametralladora no cabe donde estaba el fusil de asalto.", c, Move(bottom, s.Id, KitSlots.Primary));
            Assert.Same(mg, s.Kit.Primary);
            // Between two kit places.
            var a = InSlot(c, s, KitSlots.Belt0, GearKinds.Item, ItemIds.FragGrenade);
            var b = InSlot(c, s, KitSlots.Belt1, GearKinds.Item, ItemIds.Molotov);
            Ok(c, Move(a, s.Id, KitSlots.Belt1));
            Assert.Equal(new[] { b, a }, s.Kit.Belt);
            // Between two soldiers' kits: the one there must take it.
            var t = Soldier(c, 0, 1);
            var pistol = InSlot(c, t, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
            var other = InSlot(c, s, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
            Ok(c, Move(pistol, s.Id, KitSlots.Pistol));
            Assert.Equal((pistol, other), (s.Kit.Pistol, t.Kit!.Pistol));
        }

        [Fact]
        public void OntoAnEqualPileItFillsUp()
        {
            var c = Solo();
            var s = Soldier(c);
            var pile = InPack(c, s, GearKinds.Medicine, null, 0, 0, 3);
            var more = InStash(c, GearKinds.Medicine, null, 5, 5, 4);
            Ok(c, Move(more, s.Id, x: 0, y: 0));
            Assert.Equal((5, 2), (pile.Count, more.Count));
            // The rest stays where it was.
            Assert.Same(more, c.Stash!.Items.Single().Gear);
            Refused(RunTexts.PileFull, c, Move(more, s.Id, x: 0, y: 0));
            // All of it: it goes.
            pile.Count = 1;
            Ok(c, Move(more, s.Id, x: 0, y: 0));
            Assert.Equal(3, pile.Count);
            Assert.Empty(c.Stash.Items);
            // On a belt place too.
            var belt = InSlot(c, s, KitSlots.Belt0, GearKinds.Item, ItemIds.Molotov, 2);
            var loose = InStash(c, GearKinds.Item, ItemIds.Molotov, 0, 0, 2);
            Ok(c, Move(loose, s.Id, KitSlots.Belt0));
            Assert.Equal(4, belt.Count);
            // Not two different things, nor weapons.
            var mags = InStash(c, GearKinds.Mags, null, 0, 0);
            Refused(RunTexts.NoRoom, c, Move(mags, s.Id, x: 0, y: 0));
        }

        // ------------------------------------------------------------- who touches

        [Fact]
        public void EachTouchesTheirOwnAndTheStashInTheBase()
        {
            var c = Two();
            var mine = Soldier(c, 0);
            var theirs = Soldier(c, 1);
            var knife = theirs.Kit!.Melee!;
            Refused(RunTexts.NotYours, c, Move(knife, GearPlaces.Stash), 0, Both);
            Refused(RunTexts.NotYours, c, Move(mine.Kit!.Melee!, theirs.Id, x: 0, y: 0), 0, Both);
            Ok(c, Move(knife, GearPlaces.Stash), 1, Both);
            // The stash is both's.
            Ok(c, Move(knife, mine.Id, x: 0, y: 0), 0, Both);
            // The other away: theirs are commanded by whoever is here.
            Ok(c, Move(Soldier(c, 1, 1).Kit!.Melee!, GearPlaces.Stash, x: 3, y: 3), 0, Zero);
            // A second order on what is gone fails by itself.
            Refused(RunTexts.NotYours, c, Move(knife, GearPlaces.Stash), 1, Both);
            Ok(c, new DropGearCommand { Gear = knife.Id }, 0, Both);
            Refused(RunTexts.NoGear, c, Move(knife, GearPlaces.Stash), 0, Both);
        }

        [Fact]
        public void DuringARunOnlyTheSquadAndNeverInAFight()
        {
            var c = Solo();
            var out1 = Soldier(c, 0, 3);
            c.Squad = c.Squad.Where((id) => id != out1.Id).ToList();
            var med = InStash(c, GearKinds.Medicine, null, 0, 0, 3);
            var s = Soldier(c);
            Ok(c, Move(med, s.Id, x: 0, y: 0));
            var mags = InStash(c, GearKinds.Mags, null, 0, 0);
            Start(c);
            Refused(RunTexts.StashAtBase, c, Move(mags, s.Id, x: 1, y: 0));
            Refused(RunTexts.StashAtBase, c, Move(med, GearPlaces.Stash, x: 5, y: 5));
            Refused(RunTexts.OnlySquad, c, Move(med, out1.Id, x: 0, y: 0));
            Refused(RunTexts.OnlySquad, c, Move(out1.Kit!.Melee!, s.Id, x: 2, y: 2));
            // Between the squad's, yes.
            var t = Soldier(c, 0, 1);
            Ok(c, new SplitGearCommand { Gear = med.Id, Count = 1, To = t.Id, Slot = KitSlots.Pack, X = 0, Y = 0 });
            Ok(c, Move(med, t.Id, x: 0, y: 0));
            Assert.Equal(3, t.Kit!.Pack.Items.Single().Gear.Count);
            GoTo(c, NodeKinds.Normal);
            Refused(RunTexts.InFight, c, Move(t.Kit.Pack.Items[0].Gear, s.Id, x: 0, y: 0));
            Refused(RunTexts.InFight, c, new DropGearCommand { Gear = t.Kit.Melee!.Id });
        }

        [Fact]
        public void AVestKeepsTheDamageTaken()
        {
            var c = Solo();
            var s = Soldier(c);
            var vest = InPack(c, s, GearKinds.Vest, VestIds.Vest, 0, 0);
            var run = Start(c);
            var max = Runs.MaxHp(s);
            run.Hp[s.Id] = max - 3;
            Ok(c, Move(vest, s.Id, KitSlots.Vest));
            Assert.Equal(max + 2, Runs.MaxHp(s));
            Assert.Equal(max - 1, run.Hp[s.Id]);
            Ok(c, Move(vest, s.Id, x: 2, y: 1));
            Assert.Equal(max - 3, run.Hp[s.Id]);
            // Never below 1.
            Ok(c, Move(vest, s.Id, KitSlots.Vest));
            run.Hp[s.Id] = 1;
            Ok(c, new DropGearCommand { Gear = vest.Id });
            Assert.Equal(1, run.Hp[s.Id]);
            // The heavy one, armour too.
            InSlot(c, s, KitSlots.Vest, GearKinds.Vest, VestIds.HeavyVest);
            Assert.Equal((max + 4, 1), (Runs.MaxHp(s), Runs.ArmorOf(s)));
        }

        // ------------------------------------------------------------------ split

        [Fact]
        public void SplittingAPile()
        {
            var c = Solo();
            var s = Soldier(c);
            var pile = InStash(c, GearKinds.Supplies, null, 0, 0, 80);
            Refused(RunTexts.SplitCount(80), c, new SplitGearCommand { Gear = pile.Id, Count = 0, To = GearPlaces.Stash, X = 1 });
            Refused(RunTexts.SplitCount(80), c, new SplitGearCommand { Gear = pile.Id, Count = 80, To = GearPlaces.Stash, X = 1 });
            Refused(RunTexts.NoRoom, c, new SplitGearCommand { Gear = pile.Id, Count = 10, To = GearPlaces.Stash, X = 10 });
            Refused(RunTexts.SlotTaken, c, new SplitGearCommand { Gear = pile.Id, Count = 10, To = GearPlaces.Stash, X = 0 });
            Ok(c, new SplitGearCommand { Gear = pile.Id, Count = 30, To = s.Id, Slot = KitSlots.Pack, X = 2, Y = 1 });
            var part = s.Kit!.Pack.Items.Single().Gear;
            Assert.Equal((50, 30), (pile.Count, part.Count));
            Assert.NotEqual(pile.Id, part.Id);
            // Onto an equal pile: as much as it takes.
            var near = InStash(c, GearKinds.Supplies, null, 1, 0, 95);
            Ok(c, new SplitGearCommand { Gear = pile.Id, Count = 20, To = GearPlaces.Stash, X = 1, Y = 0 });
            Assert.Equal((45, 100), (pile.Count, near.Count));
            Refused(RunTexts.PileFull, c, new SplitGearCommand { Gear = pile.Id, Count = 5, To = GearPlaces.Stash, X = 1, Y = 0 });
            // Onto something else.
            InStash(c, GearKinds.Mags, null, 2, 0);
            Refused(RunTexts.NoRoom, c, new SplitGearCommand { Gear = pile.Id, Count = 5, To = GearPlaces.Stash, X = 2, Y = 0 });
            // One, or a weapon: no pile.
            var one = InStash(c, GearKinds.Medicine, null, 3, 0);
            Refused(RunTexts.NotAPile, c, new SplitGearCommand { Gear = one.Id, Count = 1, To = GearPlaces.Stash, X = 4 });
            Refused(RunTexts.NotAPile, c, new SplitGearCommand { Gear = s.Kit.Melee!.Id, Count = 1, To = GearPlaces.Stash, X = 4 });
            // Into a belt place.
            var grenades = InStash(c, GearKinds.Item, ItemIds.FragGrenade, 5, 0, 4);
            Ok(c, new SplitGearCommand { Gear = grenades.Id, Count = 2, To = s.Id, Slot = KitSlots.Belt0 });
            Assert.Equal((2, 2), (grenades.Count, s.Kit.Belt[0]!.Count));
        }

        // ------------------------------------------------------------------- drop

        [Fact]
        public void DroppingIsForGood()
        {
            var c = Solo();
            var s = Soldier(c);
            var rifle = InSlot(c, s, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            rifle.Mods = new List<string> { WeaponModIds.Scope };
            Ok(c, new DropGearCommand { Gear = rifle.Id });
            Assert.Null(s.Kit!.Primary);
            Assert.Null(Gears.Find(c, rifle.Id));
            var med = InStash(c, GearKinds.Medicine, null, 0, 0);
            Ok(c, new DropGearCommand { Gear = med.Id });
            Assert.Empty(c.Stash!.Items);
            Refused(RunTexts.NoGear, c, new DropGearCommand { Gear = med.Id });
        }

        // ------------------------------------------------------------------- mods

        [Fact]
        public void MountingAndTakingOffMods()
        {
            var c = Solo();
            var s = Soldier(c);
            var rifle = InSlot(c, s, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            var scopes = InStash(c, GearKinds.Mod, WeaponModIds.Scope, 0, 0, 2);
            var silencer = InStash(c, GearKinds.Mod, WeaponModIds.Silencer, 1, 0);
            var stock = InStash(c, GearKinds.Mod, WeaponModIds.Stock, 2, 0);
            Refused(RunTexts.ModsOnGuns, c, new MountCommand { Mod = scopes.Id, Weapon = s.Kit!.Melee!.Id });
            Refused(RunTexts.NotAMod, c, new MountCommand { Mod = s.Kit.Melee!.Id, Weapon = rifle.Id });
            Refused(RunTexts.NotAWeapon, c, new MountCommand { Mod = scopes.Id, Weapon = silencer.Id });
            Ok(c, new MountCommand { Mod = scopes.Id, Weapon = rifle.Id });
            Assert.Equal(new[] { WeaponModIds.Scope }, rifle.Mods);
            Assert.Equal(1, scopes.Count);
            Refused(RunTexts.ModOn, c, new MountCommand { Mod = scopes.Id, Weapon = rifle.Id });
            Ok(c, new MountCommand { Mod = silencer.Id, Weapon = rifle.Id });
            Assert.DoesNotContain(c.Stash!.Items, (p) => p.Gear == silencer);
            Refused(RunTexts.ModsFull(2), c, new MountCommand { Mod = stock.Id, Weapon = rifle.Id });
            // Off, into the stash: onto the pile of its own.
            Refused(RunTexts.ModOff, c, new UnmountCommand { Weapon = rifle.Id, Mod = WeaponModIds.Stock });
            Ok(c, new UnmountCommand { Weapon = rifle.Id, Mod = WeaponModIds.Scope });
            Assert.Equal(new[] { WeaponModIds.Silencer }, rifle.Mods);
            Assert.Equal(2, scopes.Count);
            Ok(c, new UnmountCommand { Weapon = rifle.Id, Mod = WeaponModIds.Silencer });
            Assert.Null(rifle.Mods);
            Assert.Contains(c.Stash.Items, (p) => p.Gear.Type == WeaponModIds.Silencer);
            // A weapon with mods piles with nothing.
            rifle.Mods = new List<string> { WeaponModIds.Scope };
            Assert.False(Gears.SamePile(rifle, rifle));
            // Not with the stash full, nor in a run.
            c.Stash.Items.Clear();
            for (var y = 0; y < c.Stash.H; y++) for (var x = 0; x < c.Stash.W; x++) InStash(c, GearKinds.Mags, null, x, y, 5);
            Refused(RunTexts.StashFull, c, new UnmountCommand { Weapon = rifle.Id, Mod = WeaponModIds.Scope });
            c.Stash.Items.RemoveAt(0);
            Start(c);
            Refused(RunTexts.UnmountAtBase, c, new UnmountCommand { Weapon = rifle.Id, Mod = WeaponModIds.Scope });
            // Mounting, between nodes, with the squad's.
            var loose = InPack(c, s, GearKinds.Mod, WeaponModIds.ExtMag, 0, 0);
            Ok(c, new MountCommand { Mod = loose.Id, Weapon = rifle.Id });
            Assert.Equal(new[] { WeaponModIds.Scope, WeaponModIds.ExtMag }, rifle.Mods);
        }
    }
}
