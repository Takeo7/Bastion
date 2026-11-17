using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// A run's node fought (H3, Campaign/RunFights.cs, RunLoot.cs, RunPack.cs): the mission of each kind of node with the
    /// squad's kits; what searching turns up as gear, into the pack or onto the floor; the pack's orders in a fight;
    /// reloading from the pack; a fallen soldier's gear; nothing wearing; the fight folded back into the run; Rebuscar.
    /// </summary>
    public class RunFightTests
    {
        private readonly ITestOutputHelper output;

        public RunFightTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>A campaign alone at a node of `kind` (made so), fighting, and its mission as the room launches it.</summary>
        private static (CampaignState C, GameState S) Fight(string kind = NodeKinds.Normal, string? modifier = null, int seed = 1, Action<CampaignState>? before = null)
        {
            var c = Solo(seed);
            before?.Invoke(c);
            var run = Start(c);
            var n = RunMap.Choices(run)[0];
            n.Kind = kind;
            n.Modifier = modifier;
            if (kind == NodeKinds.Sabotage || kind == NodeKinds.Boss) n.Site = Places.Factory;
            var r = Ok(c, new ChooseNodeCommand { Node = n.Id });
            Assert.True(r.Launch);
            var launched = new EngineCampaignRules().LaunchMission(c, new Rng(seed + 100));
            Assert.NotNull(launched);
            Assert.Equal(RunTexts.FightName(n, c.Run!.Chapter), launched!.Value.name);
            return (c, launched.Value.state);
        }

        private static Unit Squaddie(GameState s, int nth = 0) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ElementAt(nth);

        private static ItemCommand Item(Unit u, string action, string gear, int? x = null, int? y = null, bool? turned = null, string? to = null, string? slot = null)
        {
            return new ItemCommand { Unit = u.Id, Action = action, Gear = gear, X = x, Y = y, Turned = turned, To = to, Slot = slot };
        }

        private static Gear Made(GameState s, string kind, string? type, int count = 1)
        {
            return new Gear { Id = $"g{++s.Run!.NextGear}", Kind = kind, Type = type, Count = count };
        }

        private static Gear InPack(GameState s, Unit u, string kind, string? type, int x, int y, int count = 1, bool turned = false)
        {
            var g = Made(s, kind, type, count);
            Assert.NotNull(Gears.Place(u.Pack!, g, x, y, turned));
            RunPack.SyncMags(u);
            return g;
        }

        private static void Refused(string error, GameState s, Command cmd)
        {
            var r = Exec(s, cmd);
            Assert.Equal((false, error), (r.Ok, r.Error));
        }

        /// <summary>Two of the squad side by side: the second moved next to the first if it isn't.</summary>
        private static (Unit A, Unit B) Pair(GameState s)
        {
            var a = Squaddie(s);
            var b = Squaddie(s, 1);
            if (Aim.InMeleeReach(s, a.Pos, b.Pos)) return (a, b);
            foreach (var d in Grid.Dirs4)
            {
                var p = new Pos { X = a.Pos.X + d.X, Y = a.Pos.Y + d.Y, Z = a.Pos.Z };
                if (Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null && Aim.InMeleeReach(s, a.Pos, p))
                {
                    b.Pos = p;
                    return (a, b);
                }
            }
            throw new Exception("No room beside the first soldier.");
        }

        // ------------------------------------------------------------------ launch

        [Fact]
        public void EachKindOfNodeIsItsMission()
        {
            var expected = new Dictionary<string, string>
            {
                [NodeKinds.Normal] = MissionKinds.Scavenge,
                [NodeKinds.Elimination] = MissionKinds.Elimination,
                [NodeKinds.Recovery] = MissionKinds.Recovery,
                [NodeKinds.Sabotage] = MissionKinds.Sabotage,
                [NodeKinds.Hack] = MissionKinds.Hack,
                [NodeKinds.Rescue] = MissionKinds.Rescue,
                [NodeKinds.Hold] = MissionKinds.Scavenge,
                [NodeKinds.Crossing] = MissionKinds.Scavenge,
                [NodeKinds.Stealth] = MissionKinds.Recovery,
                [NodeKinds.Elite] = MissionKinds.Scavenge,
                [NodeKinds.Boss] = MissionKinds.Scavenge,
            };
            foreach (var e in expected)
            {
                var (c, s) = Fight(e.Key, seed: 3);
                Assert.Equal(e.Value, s.Mission.Kind);
                Assert.Equal(c.Run!.At, s.Run!.Node);
                Assert.Equal(e.Key, s.Run.NodeKind);
                Assert.Equal((c.NextId, c.NextId), (s.Run.FirstGear, s.Run.NextGear));
                Assert.Equal(RunTexts.FightName(RunMap.Node(c.Run, c.Run.At)!, c.Run.Chapter), s.MapName);
                Assert.Equal(true, s.Mission.Pistols);
                Assert.All(s.Units.Where((u) => u.CampaignId != null), (u) =>
                {
                    Assert.NotNull(u.Pack);
                    Assert.NotNull(u.Kit);
                    Assert.Null(u.Backpack);
                    Assert.Equal(FoundWeaponIds.Knife, u.Kit!.Melee!.Type);
                    Assert.Null(u.Arms!.Melee);
                });
                if (e.Key == NodeKinds.Boss) Assert.Equal(s.Mission.Boss, s.Units.Single((u) => u.Template == TemplateIds.Brute).Id);
            }
        }

        [Fact]
        public void ANormalNodeIsASaqueoOnEverySite()
        {
            foreach (var site in new[] { Places.Town, Places.Estate, Places.Factory, Places.Roadside, Places.Country })
            {
                for (var seed = 1; seed <= 3; seed++)
                {
                    var node = new RunNode { Kind = NodeKinds.Normal, Site = site, Seed = seed * 7717, Difficulty = 1 };
                    var map = RunMap.MapOf(node)!;
                    var s = Setup.CreateMission(new MissionOptions { Map = map, Slots = new List<int> { 0 }, Rng = new Rng(seed), Kind = MissionKinds.Scavenge, Difficulty = 1 });
                    Assert.Equal(MissionKinds.Scavenge, s.Mission.Kind);
                    Assert.NotEmpty(s.Mission.EvacZone);
                }
            }
        }

        [Fact]
        public void TheModifiers()
        {
            Assert.True(Fight(NodeKinds.Normal, Modifiers.Night).S.Mission.Night);
            Assert.Equal(Weathers.Storm, Fight(NodeKinds.Normal, Modifiers.Storm).S.Weather);
            var (_, s) = Fight(NodeKinds.Normal, Modifiers.Awake);
            Assert.True(s.Mission.Awake);
            Assert.All(s.Pods, (p) => Assert.True(p.Active));
            Assert.Contains(s.Mission.Reinforcements, (r) => r.Turn == 1 && r.Horde == true && r.Units.Count > 0);
            var (_, plain) = Fight(NodeKinds.Normal);
            Assert.Null(plain.Mission.Awake);
            Assert.Null(plain.Weather);
            Assert.False(plain.Mission.Night);
            Assert.Contains(plain.Pods, (p) => !p.Active);
        }

        [Fact]
        public void EachSoldierGoesOutWithItsKit()
        {
            var (c, s) = Fight(before: (c) =>
            {
                var a = Soldier(c);
                InSlot(c, a, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle).Mods = new List<string> { WeaponModIds.Scope };
                InSlot(c, a, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                InSlot(c, a, KitSlots.Vest, GearKinds.Vest, VestIds.HeavyVest);
                InSlot(c, a, KitSlots.Belt0, GearKinds.Item, ItemIds.FragGrenade, 2);
                InSlot(c, a, KitSlots.Belt1, GearKinds.Item, ItemIds.Molotov);
                RunsHelpers.InPack(c, a, GearKinds.Mags, null, 0, 0, 3);
                RunsHelpers.InPack(c, a, GearKinds.Medicine, null, 1, 0);
            });
            var soldier = Soldier(c);
            var u = s.Units.First((x) => x.CampaignId == soldier.Id);
            Assert.Equal((FoundWeaponIds.AssaultRifle, soldier.Kit!.Primary!.Id), (u.Arms!.Primary, u.Arms.PrimaryId));
            Assert.Equal(new[] { WeaponModIds.Scope }, u.Arms.Mods);
            Assert.Null(u.Arms.Condition);
            Assert.True(u.Arms.Pistol);
            Assert.Equal(Runs.MaxHp(soldier), u.MaxHp);
            Assert.Equal(u.MaxHp, u.Hp);
            Assert.Equal(Bars.FighterStatsOf(soldier.Skills, soldier.Growth, soldier.Board).MaxHp + 4, u.MaxHp);
            Assert.True(u.Armor >= 1);
            Assert.Equal(4, u.Charges[ChargeIds.Grenade]);
            Assert.Equal(2, u.Charges[ChargeIds.Molotov]);
            Assert.Contains(ItemIds.Molotov, u.Gear);
            Assert.Contains(AbilityIds.Molotov, Abilities.AbilitiesOf(u));
            Assert.Equal(3, u.Mags);
            Assert.Equal(2, u.Pack!.Items.Count);
            Assert.Equal(Content.ClipOf(u), u.Ammo);
            Assert.Equal("Fusil de asalto", u.WeaponName);
            // The health it has left in the run.
            var (c2, s2) = Fight(before: (x) => { });
            var hurt = Soldier(c2);
            Assert.Equal(Runs.MaxHp(hurt), s2.Units.First((x) => x.CampaignId == hurt.Id).Hp);
            var c3 = Solo();
            var run = Start(c3);
            run.Hp[Soldier(c3).Id] = 2;
            var n = RunMap.Choices(run)[0];
            Ok(c3, new ChooseNodeCommand { Node = n.Id });
            var s3 = Runs.Launch(c3, new Rng(1))!.Value.State;
            Assert.Equal(2, s3.Units.First((x) => x.CampaignId == Soldier(c3).Id).Hp);
            // Not at a fight: nothing.
            Assert.Null(Runs.Launch(Solo(), new Rng(1)));
        }

        // -------------------------------------------------------------- the loot

        [Fact]
        public void TodaysFindsAsGear()
        {
            var run = new RunFight { Row = 1 };
            var maker = new RunLoot.Maker(10);
            var rng = new Rng(1);
            Assert.Empty(RunLoot.GearOf(null, run, rng, maker));
            var supplies = RunLoot.GearOf(new Loot { Supplies = 12 }, run, rng, maker).Single();
            Assert.Equal(("g11", GearKinds.Supplies, 12), (supplies.Id, supplies.Kind, supplies.Count));
            // A quarter more a row, half as much again with a modifier.
            Assert.Equal(13, RunLoot.GearOf(new Loot { Supplies = 10 }, new RunFight { Row = 2 }, rng, maker).Single().Count);
            Assert.Equal(15, RunLoot.GearOf(new Loot { Supplies = 10 }, new RunFight { Row = 1, Modifier = Modifiers.Night }, rng, maker).Single().Count);
            Assert.Equal(2.625, RunLoot.SuppliesScale(new RunFight { Row = 4, Modifier = Modifiers.Awake }));
            Assert.Equal(GearKinds.Supplies, RunLoot.GearOf(new Loot { Food = 3 }, run, rng, maker).Single().Kind);
            Assert.Equal((GearKinds.Item, ItemIds.Molotov, 2), RunLoot.GearOf(new Loot { Item = ItemIds.Molotov, Count = 2 }, run, rng, maker).Select((g) => (g.Kind, g.Type!, g.Count)).Single());
            // Not the vest, the earplugs nor the rounds of before.
            Assert.Empty(RunLoot.GearOf(new Loot { Item = ItemIds.NanoVest }, run, rng, maker));
            Assert.Empty(RunLoot.GearOf(new Loot { Item = ItemIds.MindShield }, run, rng, maker));
            Assert.Equal(GearKinds.Medicine, RunLoot.GearOf(new Loot { Medicines = 2 }, run, rng, maker).Single().Kind);
            var gun = RunLoot.GearOf(new Loot { Weapon = FoundWeaponIds.HuntingRifle, Condition = 50, Mods = new List<string> { WeaponModIds.Stock } }, run, rng, maker).Single();
            Assert.Equal((GearKinds.Weapon, FoundWeaponIds.HuntingRifle), (gun.Kind, gun.Type!));
            Assert.Equal(new[] { WeaponModIds.Stock }, gun.Mods);
            Assert.Equal(GearKinds.Mod, RunLoot.GearOf(new Loot { Mod = WeaponModIds.Scope }, run, rng, maker).Single().Kind);
            Assert.Equal((GearKinds.Mags, 2), RunLoot.GearOf(new Loot { Mags = 2 }, run, rng, maker).Select((g) => (g.Kind, g.Count)).Single());
            // Parts: a magazine half the time.
            var parts = Enumerable.Range(0, 40).Select((_) => RunLoot.GearOf(new Loot { Parts = 2 }, run, rng, maker)).ToList();
            Assert.Contains(parts, (p) => p.Count == 0);
            Assert.Contains(parts, (p) => p.Count == 1 && p[0].Kind == GearKinds.Mags);
            // Big finds come in piles.
            Assert.Equal(new[] { 100, 30 }, RunLoot.GearOf(new Loot { Supplies = 130 }, run, rng, maker).Select((g) => g.Count));
            // The ids went on.
            Assert.True(maker.Next > 20);
        }

        [Fact]
        public void WhatIsFoundGoesToThePackOrTheFloor()
        {
            var pack = new GearGrid { W = 2, H = 1 };
            Gears.Place(pack, new Gear { Id = "g1", Kind = GearKinds.Supplies, Count = 90 }, 0, 0, false);
            var found = new List<Gear>
            {
                new Gear { Id = "g2", Kind = GearKinds.Supplies, Count = 30 },
                new Gear { Id = "g3", Kind = GearKinds.Weapon, Type = FoundWeaponIds.Pistol },
            };
            var (packed, floor) = RunLoot.Stow(pack, found);
            // 10 onto the pile, 20 in the free cell; the pistol nowhere.
            Assert.Equal(new[] { ("g2", 10, 0), ("g2", 20, 1) }, packed.Select((p) => (p.Gear.Id, p.Gear.Count, p.X)));
            Assert.Equal("g3", floor.Single().Id);
            // Untouched till applied.
            Assert.Single(pack.Items);
        }

        /// <summary>A room in the mission, free to stand in.</summary>
        private static Pos? RoomTile(GameState s)
        {
            for (var y = 0; y < s.Height; y++)
            {
                for (var x = 0; x < s.Width; x++)
                {
                    var p = new Pos { X = x, Y = y, Z = 0 };
                    if (Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p) && Looting.RoomAt(s, p) != null) return p;
                }
            }
            return null;
        }

        [Fact]
        public void SearchingARoomTurnsUpGear()
        {
            for (var seed = 1; seed < 30; seed++)
            {
                var (_, s) = Fight(seed: seed);
                var u = Squaddie(s);
                var room = RoomTile(s);
                if (room == null) continue;
                u.Pos = room;
                var next = s.Run!.NextGear;
                var r = Exec(s, Ability(u.Id, AbilityIds.SearchRoom), seed);
                Assert.True(r.Ok, r.Error);
                var ev = Of<RoomSearchedEvent>(r.Events!).Single();
                Assert.Null(ev.Loot);
                Assert.Null(u.Backpack);
                if (ev.Packed == null) continue;
                Assert.Equal(ev.NextGear, s.Run.NextGear);
                Assert.True(s.Run.NextGear > next);
                Assert.All(ev.Packed, (p) => Assert.Contains(u.Pack!.Items, (q) => q.Gear.Id == p.Gear.Id));
                return;
            }
            throw new Exception("No search turned anything up.");
        }

        [Fact]
        public void AFullPackLeavesTheFindOnTheFloor()
        {
            for (var seed = 1; seed < 40; seed++)
            {
                var (_, s) = Fight(seed: seed);
                var u = Squaddie(s);
                var room = RoomTile(s);
                if (room == null) continue;
                u.Pos = room;
                for (var y = 0; y < u.Pack!.H; y++) for (var x = 0; x < u.Pack.W; x++) InPack(s, u, GearKinds.Mags, null, x, y, 5);
                var r = Exec(s, Ability(u.Id, AbilityIds.SearchRoom), seed);
                var ev = Of<RoomSearchedEvent>(r.Events!).Single();
                if (ev.Floor == null) continue;
                Assert.Null(ev.Packed);
                Assert.All(ev.Floor, (g) => Assert.Contains(s.FloorGear!, (d) => d.Gear.Id == g.Id && Grid.SamePos(d.Pos, u.Pos) && d.From == null));
                // Taken up again once there's room.
                u.Pack.Items.RemoveAt(0);
                RunPack.SyncMags(u);
                var g0 = ev.Floor[0];
                if (Content.GearSize(g0.Kind, g0.Type).W > 1 || Content.GearSize(g0.Kind, g0.Type).H > 1) return;
                var take = Exec(s, Item(u, ItemActions.Take, g0.Id));
                Assert.True(take.Ok, take.Error);
                Assert.Contains(u.Pack.Items, (p) => p.Gear.Id == g0.Id);
                return;
            }
            throw new Exception("No search turned anything up.");
        }

        // ------------------------------------------------------------ the pack

        [Fact]
        public void DropAndTake()
        {
            var (_, s) = Fight();
            var u = Squaddie(s);
            var med = InPack(s, u, GearKinds.Medicine, null, 1, 1, 2);
            var ap = u.Ap;
            Assert.True(Exec(s, Item(u, ItemActions.Drop, med.Id)).Ok);
            Assert.Empty(u.Pack!.Items);
            var on = s.FloorGear!.Single();
            Assert.Equal((med.Id, 2), (on.Gear.Id, on.Gear.Count));
            Assert.True(Grid.SamePos(on.Pos, u.Pos));
            Assert.Equal(ap, u.Ap);
            Refused(RunPack.Texts.NoGear, s, Item(u, ItemActions.Drop, med.Id));
            Refused(RunPack.Texts.PackOnly, s, Item(u, ItemActions.Drop, u.Kit!.Melee!.Id));
            // Taken where asked, free.
            Refused(RunPack.Texts.NoRoomThere, s, Item(u, ItemActions.Take, med.Id, 9, 9));
            Assert.True(Exec(s, Item(u, ItemActions.Take, med.Id, 3, 2)).Ok);
            Assert.Equal((3, 2), (u.Pack.Items.Single().X, u.Pack.Items.Single().Y));
            Assert.Empty(s.FloorGear!);
            Assert.Equal(ap, u.Ap);
            // Not from another tile; onto an equal pile, as much as it takes.
            var other = Squaddie(s, 1);
            s.FloorGear!.Add(new DroppedGear { Gear = Made(s, GearKinds.Medicine, null, 4), Pos = Events.CopyPos(other.Pos) });
            Refused(RunPack.Texts.NotHere, s, Item(u, ItemActions.Take, s.FloorGear[0].Gear.Id));
            s.FloorGear[0].Pos = Events.CopyPos(u.Pos);
            Assert.True(Exec(s, Item(u, ItemActions.Take, s.FloorGear[0].Gear.Id, 3, 2)).Ok);
            Assert.Equal(5, u.Pack.Items.Single().Gear.Count);
            Assert.Equal(1, s.FloorGear.Single().Gear.Count);
        }

        [Fact]
        public void GiveAndUse()
        {
            var (_, s) = Fight();
            var (a, b) = Pair(s);
            var med = InPack(s, a, GearKinds.Medicine, null, 0, 0, 2);
            var rifle = InPack(s, a, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 1);
            var ap = a.Ap;
            var give = Exec(s, Item(a, ItemActions.Give, rifle.Id, to: b.Id));
            Assert.True(give.Ok, give.Error);
            Assert.Contains(b.Pack!.Items, (p) => p.Gear == rifle || p.Gear.Id == rifle.Id);
            Assert.Equal(ap - 1, a.Ap);
            // No room in theirs.
            for (var y = 0; y < b.Pack.H; y++) for (var x = 0; x < b.Pack.W; x++) if (Gears.At(b.Pack, x, y) == null) InPack(s, b, GearKinds.Mags, null, x, y, 5);
            Refused("No le cabe.", s, Item(a, ItemActions.Give, med.Id, to: b.Id));
            Refused("Solo a un compañero.", s, Item(a, ItemActions.Give, med.Id, to: a.Id));
            // A medicine on oneself.
            Refused("No tienes heridas.", s, Item(a, ItemActions.Use, med.Id));
            a.Hp = 1;
            a.Ap = 2;
            var use = Exec(s, Item(a, ItemActions.Use, med.Id));
            Assert.True(use.Ok, use.Error);
            Assert.Equal(Math.Min(a.MaxHp, 1 + Content.MedicineHeal + a.Mods.Heal), a.Hp);
            Assert.Equal(1, med.Count);
            Assert.Single(Of<GearUsedEvent>(use.Events!));
            Refused(RunPack.Texts.OnlyMedicine, s, Item(b, ItemActions.Use, b.Pack.Items[0].Gear.Id));
        }

        [Fact]
        public void EquipSwapsWithTheKit()
        {
            var (_, s) = Fight();
            var u = Squaddie(s);
            u.Ap = 2;
            var rifle = InPack(s, u, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 0);
            rifle.Mods = new List<string> { WeaponModIds.ExtMag };
            var r = Exec(s, Item(u, ItemActions.Equip, rifle.Id));
            Assert.True(r.Ok, r.Error);
            Assert.Same(rifle, u.Kit!.Primary);
            Assert.Equal((FoundWeaponIds.AssaultRifle, rifle.Id), (u.Arms!.Primary, u.Arms.PrimaryId));
            Assert.Equal(new[] { WeaponModIds.ExtMag }, u.Arms.Mods);
            Assert.Equal(Content.ClipOf(u), u.Ammo);
            Assert.Equal(1, u.Ap);
            // Another rifle: they swap, the one in the hand to where this lay.
            var shotgun = InPack(s, u, GearKinds.Weapon, FoundWeaponIds.PumpShotgun, 0, 2);
            Assert.True(Exec(s, Item(u, ItemActions.Equip, shotgun.Id)).Ok);
            Assert.Same(shotgun, u.Kit.Primary);
            Assert.Equal((0, 2), (u.Pack!.Items.Single((p) => p.Gear == rifle).X, u.Pack.Items.Single((p) => p.Gear == rifle).Y));
            Refused(RunPack.Texts.NoActions, s, Item(u, ItemActions.Equip, rifle.Id));
            // Not if what is in the kit doesn't fit where it lay.
            u.Ap = 2;
            u.Pack.Items.Clear();
            var mg = Made(s, GearKinds.Weapon, FoundWeaponIds.MachineGun);
            u.Kit.Primary = mg;
            var low = InPack(s, u, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 2);
            Refused("La ametralladora no cabe donde estaba el fusil de asalto.", s, Item(u, ItemActions.Equip, low.Id));
            // A vest: its health, keeping the damage.
            var vest = InPack(s, u, GearKinds.Vest, VestIds.Vest, 0, 0);
            var max = u.MaxHp;
            u.Hp = max - 2;
            Assert.True(Exec(s, Item(u, ItemActions.Equip, vest.Id)).Ok);
            Assert.Equal((max + 2, max), (u.MaxHp, u.Hp));
            // The belt: an empty place, then its own pile.
            u.Ap = 2;
            var grenades = InPack(s, u, GearKinds.Item, ItemIds.FragGrenade, 3, 0, 2);
            Assert.True(Exec(s, Item(u, ItemActions.Equip, grenades.Id)).Ok);
            Assert.Equal(4, u.Charges[ChargeIds.Grenade]);
            Assert.Contains(ItemIds.FragGrenade, u.Gear);
            var more = InPack(s, u, GearKinds.Item, ItemIds.FragGrenade, 3, 0);
            Assert.True(Exec(s, Item(u, ItemActions.Equip, more.Id)).Ok);
            Assert.Equal(3, u.Kit.Belt[0]!.Count);
            Assert.Equal(6, u.Charges[ChargeIds.Grenade]);
            u.Ap = 2;
            var smoke = InPack(s, u, GearKinds.Item, ItemIds.SmokeGrenade, 3, 0);
            Assert.True(Exec(s, Item(u, ItemActions.Equip, smoke.Id)).Ok);
            Assert.Same(smoke, u.Kit.Belt[1]);
            var molotov = InPack(s, u, GearKinds.Item, ItemIds.Molotov, 3, 0);
            Refused(RunPack.Texts.BeltTaken, s, Item(u, ItemActions.Equip, molotov.Id, slot: KitSlots.Belt0));
            Refused(RunPack.Texts.NotWorn, s, Item(u, ItemActions.Equip, InPack(s, u, GearKinds.Mags, null, 2, 0).Id));
        }

        [Fact]
        public void ArrangeIsFree()
        {
            var (_, s) = Fight();
            var u = Squaddie(s);
            var ap = u.Ap;
            var med = InPack(s, u, GearKinds.Medicine, null, 0, 0, 2);
            var more = InPack(s, u, GearKinds.Medicine, null, 1, 0, 4);
            Assert.True(Exec(s, Item(u, ItemActions.Arrange, med.Id, 3, 2)).Ok);
            Assert.Equal((3, 2), (u.Pack!.Items.Single((p) => p.Gear == med).X, u.Pack.Items.Single((p) => p.Gear == med).Y));
            // Onto its own kind: as much as it takes, the rest where it was.
            Assert.True(Exec(s, Item(u, ItemActions.Arrange, med.Id, 1, 0)).Ok);
            Assert.Equal((5, 1), (more.Count, med.Count));
            Refused(RunPack.Texts.NoRoomThere, s, Item(u, ItemActions.Arrange, med.Id, 1, 0));
            Assert.Equal(ap, u.Ap);
            // A weapon off the kit into the pack: the arms follow.
            var pistol = Made(s, GearKinds.Weapon, FoundWeaponIds.Pistol);
            u.Kit!.Pistol = pistol;
            RunPack.Refit(u, KitSlots.Pistol, null, pistol);
            Assert.True(u.Arms!.Pistol);
            Assert.True(Exec(s, Item(u, ItemActions.Arrange, pistol.Id, 0, 1)).Ok);
            Assert.Null(u.Kit.Pistol);
            Assert.False(u.Arms.Pistol);
            // The belt stays.
            var molotov = Made(s, GearKinds.Item, ItemIds.Molotov);
            u.Kit.Belt[0] = molotov;
            Refused(RunPack.Texts.BeltStays, s, Item(u, ItemActions.Arrange, molotov.Id, 0, 2));
            Refused(RunPack.Texts.NoRoomThere, s, Item(u, ItemActions.Arrange, more.Id, 9, 0));
        }

        [Fact]
        public void TheRefusalsAreTheUIs()
        {
            var (_, s) = Fight();
            var u = Squaddie(s);
            var med = InPack(s, u, GearKinds.Medicine, null, 0, 0);
            Assert.Null(RunPack.Refusal(s, Item(u, ItemActions.Arrange, med.Id, 2, 2), 0, Zero));
            Assert.Equal(RunPack.Texts.NoRoomThere, RunPack.Refusal(s, Item(u, ItemActions.Arrange, med.Id, 4, 0), 0, Zero));
            Assert.Equal("Es de la escuadra del otro jugador.", RunPack.Refusal(s, Item(u, ItemActions.Drop, med.Id), 1, new[] { 0, 1 }));
            u.Ap = 0;
            Assert.Equal(RunPack.Texts.NoActions, RunPack.Refusal(s, Item(u, ItemActions.Use, med.Id), 0, Zero));
            Assert.Null(RunPack.Refusal(s, Item(u, ItemActions.Drop, med.Id), 0, Zero));
        }

        [Fact]
        public void ReloadingTakesAMagazineFromThePack()
        {
            var (_, s) = Fight();
            var u = Squaddie(s);
            var rifle = Made(s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            u.Kit!.Primary = rifle;
            RunPack.Refit(u, KitSlots.Primary, null, rifle);
            InPack(s, u, GearKinds.Mags, null, 0, 0, 2);
            u.Ammo = 0;
            u.Ap = 2;
            var r = Exec(s, Ability(u.Id, AbilityIds.Reload));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(Content.ClipOf(u), u.Ammo);
            Assert.Equal(1, u.Pack!.Items.Single().Gear.Count);
            Assert.Equal(1, u.Mags);
            u.Ammo = 0;
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.Reload)).Ok);
            Assert.Empty(u.Pack.Items);
            Assert.Equal(0, u.Mags);
            u.Ammo = 0;
            u.Ap = 2;
            Assert.Equal("No quedan cargadores.", Abilities.AbilityBlocker(s, u, AbilityIds.Reload));
        }

        [Fact]
        public void AFallenSoldiersGearLiesWhereItFell()
        {
            var (_, s) = Fight();
            var u = Squaddie(s);
            var rifle = Made(s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            u.Kit!.Primary = rifle;
            var grenades = Made(s, GearKinds.Item, ItemIds.FragGrenade, 2);
            u.Kit.Belt[0] = grenades;
            RunPack.Belted(u, ItemIds.FragGrenade, 2);
            // One grenade thrown of four uses: both items still there.
            u.Charges[ChargeIds.Grenade] = 3;
            var med = InPack(s, u, GearKinds.Medicine, null, 0, 0, 3);
            var knife = u.Kit.Melee!;
            Events.ApplyEvent(s, new DiedEvent { Unit = u.Id });
            var left = s.FloorGear!.Where((d) => d.From == u.Id).ToList();
            Assert.Equal(new[] { rifle.Id, knife.Id, grenades.Id, med.Id }.OrderBy((x) => x), left.Select((d) => d.Gear.Id).OrderBy((x) => x));
            Assert.Equal(2, left.Single((d) => d.Gear.Id == grenades.Id).Gear.Count);
            Assert.Null(u.Kit.Primary);
            Assert.Empty(u.Pack!.Items);
            Assert.Null(u.Backpack);
            Assert.Null(s.Dropped);
        }

        [Fact]
        public void WhatIsLeftOnTheBelt()
        {
            var u = new Unit { Kit = new KitPlaces(), Charges = new Dictionary<string, int>() };
            u.Kit.Belt[0] = new Gear { Id = "g1", Kind = GearKinds.Item, Type = ItemIds.FragGrenade, Count = 2 };
            u.Kit.Belt[1] = new Gear { Id = "g2", Kind = GearKinds.Item, Type = ItemIds.SmokeGrenade, Count = 1 };
            int?[] Left(int grenades, int smoke)
            {
                u.Charges[ChargeIds.Grenade] = grenades;
                u.Charges[ChargeIds.Smoke] = smoke;
                return RunPack.BeltLeft(u).Select((g) => g?.Count).ToArray();
            }
            Assert.Equal(new int?[] { 2, 1 }, Left(4, 1));
            Assert.Equal(new int?[] { 2, 1 }, Left(3, 1));
            Assert.Equal(new int?[] { 1, null }, Left(1, 0));
            Assert.Equal(new int?[] { null, 1 }, Left(0, 1));
            // Extra uses of the board's go first.
            Assert.Equal(new int?[] { 2, 1 }, Left(6, 1));
        }

        [Fact]
        public void NothingWearsInARun()
        {
            var (_, s) = Fight();
            var u = Squaddie(s);
            var rifle = Made(s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            u.Kit!.Primary = rifle;
            RunPack.Refit(u, KitSlots.Primary, null, rifle);
            var shot = new ShotEvent { Unit = u.Id, Target = "zz", Ability = AbilityIds.Shoot, From = u.Pos, Result = ShotResults.Miss, Chance = 50, Roll = 90, Reaction = false };
            Events.ApplyEvent(s, shot);
            Assert.Null(u.Arms!.Condition);
            Assert.Equal(100, Content.ConditionOf(u));
            // Outside a run, as before.
            s.Run = null;
            Events.ApplyEvent(s, shot);
            Assert.True(u.Arms.Condition < 100);
        }

        [Fact]
        public void RebuscarSearchesTheRoomAgain()
        {
            for (var seed = 1; seed < 30; seed++)
            {
                var (_, s) = Fight(seed: seed);
                var u = Squaddie(s);
                var room = RoomTile(s);
                if (room == null) continue;
                u.Pos = room;
                u.Perks.Add(PerkIds.PatchUp);
                u.Charges[ChargeIds.PatchUp] = 1;
                u.Ap = 2;
                Assert.True(Exec(s, Ability(u.Id, AbilityIds.SearchRoom), seed).Ok);
                Assert.Equal("Esta habitación ya está registrada.", Abilities.AbilityBlocker(s, u, AbilityIds.SearchRoom));
                Assert.Contains(AbilityIds.PatchUp, Abilities.AbilitiesOf(u));
                var r = Exec(s, Ability(u.Id, AbilityIds.PatchUp), seed);
                Assert.True(r.Ok, r.Error);
                Assert.True(Of<RoomSearchedEvent>(r.Events!).Single().Again);
                Assert.Single(s.RoomsSearched!);
                u.Ap = 2;
                Assert.Equal("No quedan usos.", Abilities.AbilityBlocker(s, u, AbilityIds.PatchUp));
                Assert.Equal("Rebuscar", Content.Abilities[AbilityIds.PatchUp].Name);
                Assert.Equal("Rebuscar", Board.BoardCells["patchUp"].Name);
                Assert.Equal("Todo cabe", Board.BoardCells["care"].Name);
                return;
            }
            throw new Exception("No room to search.");
        }

        [Fact]
        public void TodoCabeIsAPackRowMore()
        {
            var c = Solo();
            var s = Soldier(c);
            Assert.Equal(3, Runs.PackRowsOf(c, s));
            s.Board.Add(Runs.CareCell);
            Assert.Equal(4, Runs.PackRowsOf(c, s));
        }

        [Fact]
        public void ARunsFightReadsBackAsItWas()
        {
            var (_, s) = Fight();
            var u = Squaddie(s);
            InPack(s, u, GearKinds.Medicine, null, 0, 0, 2);
            s.FloorGear = new List<DroppedGear> { new DroppedGear { Gear = Made(s, GearKinds.Mags, null), Pos = Events.CopyPos(u.Pos), From = "s9" } };
            var json = Json.Write(s);
            Assert.Equal(json, Json.Write(Json.Read<GameState>(json)));
            Assert.Equal(json, Json.Write(Events.CloneState(s)));
        }

        // -------------------------------------------------------------- the result

        /// <summary>The fight over as `end` leaves it, folded into the run.</summary>
        private static MissionReport End(CampaignState c, GameState s, string outcome, string reason, Action<GameState>? end = null)
        {
            end?.Invoke(s);
            s.Outcome = outcome;
            s.OutcomeReason = reason;
            var before = c.NextId;
            new EngineCampaignRules().ApplyMissionResult(c, s, new Rng(9));
            Assert.True(c.NextId >= before);
            return c.LastReport!;
        }

        [Fact]
        public void ASpecialWonBringsItsReward()
        {
            var (c, s) = Fight(NodeKinds.Elimination);
            var u = Squaddie(s);
            var found = InPack(s, u, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 0);
            u.Hp = Math.Max(1, u.MaxHp - 2);
            u.Kills = 3;
            var next = s.Run!.NextGear;
            var report = End(c, s, Outcomes.Victory, EndReasons.Objective);
            var soldier = c.Soldiers.First((x) => x.Id == u.CampaignId);
            Assert.Equal(RunPhases.Choose, c.Run!.Phase);
            Assert.InRange(report.Run!.Reward!.Value, Runs.RewardMin, Runs.RewardMax);
            Assert.Null(report.Run.RewardLost);
            Assert.Equal(report.Run.Reward, c.Soldiers.Where((x) => x.Alive).Sum((x) => Gears.Supplies(x.Kit!.Pack)));
            // Its kit back, with what it found.
            Assert.Contains(soldier.Kit!.Pack.Items, (p) => p.Gear.Id == found.Id);
            Assert.Contains(report.Run.Found, (g) => g.Id == found.Id);
            Assert.Equal(Math.Max(1, u.MaxHp - 2), c.Run.Hp[soldier.Id]);
            Assert.Equal(3, soldier.Kills);
            Assert.Equal(1, soldier.Missions);
            // The counter goes on: no gear id is made twice.
            Assert.True(c.NextId >= next);
            Assert.True(Gears.NewGear(c, GearKinds.Mags, null).Id != found.Id);
            Assert.Equal(report.Squad.Count, s.Units.Count((x) => x.CampaignId != null));
        }

        [Fact]
        public void AReportKeepsItsStageWhenAStopFollows()
        {
            // UI, U8 (17/11): the base told one debrief from the next by NodesPlayed, which the stop chosen after it raises,
            // and the same debrief came back unread at the stop.
            var (c, s) = Fight();
            var played = c.NodesPlayed;
            var report = End(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.Equal(played, report.Run!.Stage);
            Assert.True(GoTo(c, NodeKinds.Trade).Ok);
            Assert.Equal(played + 1, c.NodesPlayed);
            Assert.Same(report, c.LastReport);
            Assert.Equal(played, c.LastReport!.Run!.Stage);
        }

        [Fact]
        public void TheOneRescuedJoins()
        {
            var (c, s) = Fight(NodeKinds.Rescue, seed: 3, before: (x) => x.Upgrades![UpgradeIds.Squad] = 2);
            Assert.Equal(MissionKinds.Rescue, s.Mission.Kind);
            var vip = s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);
            vip.Hp = 2;
            vip.Evacuated = true;
            var report = End(c, s, Outcomes.Victory, EndReasons.Objective);
            var joined = c.Soldiers.Single((x) => x.Id == report.Run!.Joined);
            Assert.Equal(vip.Name, joined.Name);
            Assert.True(joined.Alive);
            Assert.Equal(FoundWeaponIds.Knife, joined.Kit!.Melee!.Type);
            Assert.Contains(joined.Id, c.Run!.Squad);
            Assert.True(report.Run.InSquad);
            Assert.Equal(2, c.Run.Hp[joined.Id]);
            // No seat: at the base.
            var (d, t) = Fight(NodeKinds.Rescue, seed: 3);
            t.Units.Single((u) => u.Id == t.Mission.ObjectiveUnit).Evacuated = true;
            var waits = End(d, t, Outcomes.Victory, EndReasons.Objective);
            Assert.NotNull(waits.Run!.Joined);
            Assert.False(waits.Run.InSquad);
            Assert.DoesNotContain(waits.Run.Joined, d.Run!.Squad);
            // Not out: nobody.
            var (e, u2) = Fight(NodeKinds.Rescue, seed: 3);
            var before = e.Soldiers.Count;
            var lost = End(e, u2, Outcomes.Defeat, EndReasons.Abandoned);
            Assert.Null(lost.Run!.Joined);
            Assert.Equal(before, e.Soldiers.Count);
        }

        [Fact]
        public void LeavingASpecialWithoutItsObjective()
        {
            var (c, s) = Fight(NodeKinds.Recovery);
            var report = End(c, s, Outcomes.Defeat, EndReasons.Abandoned);
            Assert.Null(report.Run!.Reward);
            Assert.Null(report.Run.Ended);
            Assert.Equal(RunPhases.Choose, c.Run!.Phase);
            Assert.Equal(1, c.NodesPlayed);
        }

        [Fact]
        public void TheFallenDieForGood()
        {
            var (c, s) = Fight();
            var dead = Squaddie(s);
            var rifle = Made(s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            dead.Kit!.Primary = rifle;
            var mate = Squaddie(s, 1);
            Events.ApplyEvent(s, new DiedEvent { Unit = dead.Id });
            // The mate took up the rifle; the knife stays on the floor.
            var picked = s.FloorGear!.First((d) => d.Gear.Id == rifle.Id);
            s.FloorGear!.Remove(picked);
            Gears.Place(mate.Pack!, picked.Gear, 0, 0, false);
            var report = End(c, s, Outcomes.Victory, EndReasons.Objective, (x) => mate.Evacuated = true);
            var soldier = c.Soldiers.First((x) => x.Id == dead.CampaignId);
            Assert.False(soldier.Alive);
            Assert.Null(soldier.Kit);
            Assert.DoesNotContain(soldier.Id, c.Run!.Squad);
            Assert.DoesNotContain(soldier.Id, c.Run.Hp.Keys);
            var fallen = report.Run!.Fallen.Single();
            Assert.Equal(soldier.Id, fallen.Id);
            Assert.Contains(fallen.Lost, (g) => g.Type == FoundWeaponIds.Knife);
            Assert.DoesNotContain(fallen.Lost, (g) => g.Id == rifle.Id);
            Assert.Contains(c.Soldiers.First((x) => x.Id == mate.CampaignId).Kit!.Pack.Items, (p) => p.Gear.Id == rifle.Id);
            Assert.Equal(ReportStatuses.Dead, report.Squad.First((e) => e.Id == soldier.Id).Status);
        }

        [Fact]
        public void TheSquadWipedEndsTheRun()
        {
            var (c, s) = Fight();
            foreach (var u in s.Units.Where((x) => x.CampaignId != null).ToList()) Events.ApplyEvent(s, new DiedEvent { Unit = u.Id });
            var report = End(c, s, Outcomes.Defeat, EndReasons.Wiped);
            Assert.Equal(RunEnds.Wiped, report.Run!.Ended);
            Assert.Null(c.Run);
            Assert.Equal(RunTexts.Wiped, c.Log.Skip(1).First().Text);
            // Somebody comes for nothing, with a knife.
            Assert.Single(c.Soldiers, (x) => x.Alive);
            Assert.Equal(FoundWeaponIds.Knife, c.Soldiers.Single((x) => x.Alive).Kit!.Melee!.Type);
        }

        [Fact]
        public void TheBossDeadTheChapterIsDone()
        {
            var (c, s) = Fight(NodeKinds.Boss);
            var report = End(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.Equal(RunEnds.Boss, report.Run!.Ended);
            Assert.Null(c.Run);
            Assert.Equal((2, 1), (c.Chapter, c.ChaptersDone));
            // Left alive: back home, the chapter still open.
            var (d, t) = Fight(NodeKinds.Boss);
            var back = End(d, t, Outcomes.Defeat, EndReasons.Abandoned);
            Assert.Equal(RunEnds.Returned, back.Run!.Ended);
            Assert.Null(d.Run);
            Assert.Equal((1, 0), (d.Chapter, d.ChaptersDone));
        }

        [Fact]
        public void MendHealsAfterTheFight()
        {
            var (c, s) = Fight(before: (x) => x.Upgrades![UpgradeIds.Mend] = 1);
            var u = Squaddie(s);
            u.Hp = 1;
            End(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.Equal(1 + Runs.MendHeal, c.Run!.Hp[u.CampaignId!]);
        }

        [Fact]
        public void TheBeltComesHomeLessWhatWasUsed()
        {
            var (c, s) = Fight(before: (x) => InSlot(x, Soldier(x), KitSlots.Belt0, GearKinds.Item, ItemIds.Molotov, 2));
            var u = s.Units.First((x) => x.CampaignId == Soldier(c).Id);
            Assert.Equal(4, u.Charges[ChargeIds.Molotov]);
            u.Charges[ChargeIds.Molotov] = 1;
            End(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.Equal(1, Soldier(c).Kit!.Belt[0]!.Count);
            var (d, t) = Fight(before: (x) => InSlot(x, Soldier(x), KitSlots.Belt0, GearKinds.Item, ItemIds.Molotov, 1));
            t.Units.First((x) => x.CampaignId == Soldier(d).Id).Charges[ChargeIds.Molotov] = 0;
            End(d, t, Outcomes.Victory, EndReasons.Objective);
            Assert.Null(Soldier(d).Kit!.Belt[0]);
        }

        // ---------------------------------------------------------- end to end

        /// <summary>A node's mission played by MissionBot for every seat, as the test apps do; its state.</summary>
        private static (GameState State, int Orders, int Refused) Play(GameState state, int seed, IReadOnlyList<int> connected)
        {
            var rng = new Rng(seed * 7919 + 13);
            int orders = 0, refused = 0, idle = 0;
            while (state.Outcome == null && orders < 3000 && idle < 3)
            {
                var any = false;
                foreach (var slot in connected)
                {
                    var cmd = MissionBot.Next(state, slot, connected);
                    if (cmd == null) continue;
                    orders++;
                    if (Rules.ExecuteCommand(state, cmd, slot, connected, rng).Ok) any = true;
                    else refused++;
                }
                idle = any ? 0 : idle + 1;
            }
            return (state, orders, refused);
        }

        [Fact]
        public void ARunPlayedByTheBots()
        {
            var rules = new EngineCampaignRules();
            var nodes = 0;
            var won = 0;
            for (var seed = 1; seed <= 4; seed++)
            {
                var c = Solo(seed);
                // Out with something to shoot, and magazines for it.
                foreach (var s in c.Soldiers)
                {
                    InSlot(c, s, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                    InSlot(c, s, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                    InSlot(c, s, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
                    RunsHelpers.InPack(c, s, GearKinds.Mags, null, 0, 0, 5);
                }
                // Each seed its own map.
                Start(c, seed);
                for (var step = 0; step < 3 && c.Run != null; step++)
                {
                    var choices = RunMap.Choices(c.Run);
                    var n = choices.FirstOrDefault((x) => RunMap.IsFight(x.Kind)) ?? choices[0];
                    // TODO(Red): MissionBot plays a Saqueo now (normal nodes), but not an Asedio's late La Lata, a
                    // Travesía, a Guarida's box or El Toro. Those, a Limpieza.
                    if (n.Kind == NodeKinds.Hold || n.Kind == NodeKinds.Crossing || n.Kind == NodeKinds.Elite || n.Kind == NodeKinds.Boss) n.Kind = NodeKinds.Elimination;
                    Ok(c, new ChooseNodeCommand { Node = n.Id });
                    if (c.Run.Phase == RunPhases.Stop)
                    {
                        if (c.Run.Stop!.Outcome == null && n.Kind == NodeKinds.Event) Ok(c, new AnswerStopCommand { Option = 1 });
                        Ok(c, new LeaveStopCommand());
                        continue;
                    }
                    var state = rules.LaunchMission(c, new Rng(seed * 31 + step))!.Value.state;
                    var played = Play(state, seed, Zero);
                    Assert.Equal(0, played.Refused);
                    if (state.Outcome == null) break;
                    rules.ApplyMissionResult(c, state, new Rng(seed));
                    nodes++;
                    if (state.Outcome == Outcomes.Victory)
                    {
                        won++;
                        // Won and on: the map again, a row further.
                        Assert.Equal(RunPhases.Choose, c.Run!.Phase);
                    }
                    Assert.NotNull(c.LastReport!.Run);
                    output.WriteLine($"Semilla {seed}, {n.Id} {n.Kind}: {state.Outcome}/{state.OutcomeReason}, {played.Orders} órdenes, {c.LastReport.Run!.Found.Count} objetos encontrados, {c.LastReport.Run.Fallen.Count} caídos{(c.LastReport.Run.Ended != null ? $", la run acaba ({c.LastReport.Run.Ended})" : "")}");
                }
                // Every gear id once in the campaign.
                var ids = Gears.All(c).Select((x) => x.Gear.Id).ToList();
                Assert.Equal(ids.Count, ids.Distinct().Count());
            }
            Assert.True(nodes >= 4 && won >= 2, $"{nodes} nodes fought, {won} won");
        }
    }
}
