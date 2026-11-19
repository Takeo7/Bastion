using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G23.1 La llave (Arturo, 18/11, cuadros 71, 73 y 79; .dev-snapshots/gameplay/g23/LLAVE.md): a police station's armoury
    /// locked; its key on one of the dead in uniform; its furniture all full. Its door opens only with the key, which its
    /// holder's body gives when searched and drops where it lay if it rises; its walls break as any (a grenade).
    /// </summary>
    public class RunKeyTests
    {
        private static (CampaignState C, GameState S) Key(int seed = 12) => LaunchAt(NodeKinds.Key, node: (n) =>
        {
            n.Site = Places.Police;
            n.Seed = seed;
        });

        private static Unit Soldier(GameState s) => s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive);

        /// <summary>`g` into `pack` where it first fits.</summary>
        private static Placed? Pack(GearGrid pack, Gear g)
        {
            var at = Gears.FirstFit(pack, g);
            return at != null ? Gears.Place(pack, g, at.X, at.Y, at.Turned) : null;
        }

        private static InteriorRoom Armory(GameState s) => s.Interiors!.Single().Rooms.First((r) => r.Use == PoliceUses.Armory);

        private static Pos Door(GameState s)
        {
            var node = s.Mission.Locked!.Single();
            return new Pos { X = node % s.Width, Y = node / s.Width % s.Height, Z = 0 };
        }

        /// <summary>A free tile beside `p` `u` stands on, reaching it with the hands, out of the armoury.</summary>
        private static Pos Beside(GameState s, Pos p, Unit u)
        {
            var a = Armory(s);
            return Grid.Dirs4.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = 0 })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && !(q.X >= a.X && q.Y >= a.Y && q.X < a.X + a.W && q.Y < a.Y + a.H) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q)
                    && (Grid.UnitAt(s, q) == null || Grid.UnitAt(s, q) == u) && Looting.ReachesToSearch(s, q, p));
        }

        [Fact]
        public void OnTheMapsFromRowThreeAtAPoliceStation()
        {
            // Producto, 19/11: a special from row KeyFrom, always at a police station.
            int maps = 0, with = 0, keys = 0;
            for (var seed = 1; seed <= 2000; seed++)
            {
                var all = RunMap.Generate(1, seed);
                var here = all.Where((n) => n.Kind == NodeKinds.Key).ToList();
                Assert.All(here, (n) => Assert.True(n.Row >= RunMap.KeyFrom && n.Row < RunMap.RefugeRow, n.Id));
                Assert.All(here, (n) => Assert.Equal(Places.Police, n.Site));
                maps++;
                if (here.Count > 0) with++;
                keys += here.Count;
            }
            Assert.InRange(with / (double)maps, 0.1, 0.9);
        }

        [Fact]
        public void ARunStartedBeforeHasNone()
        {
            // Saved by the 0.13.2 between stages (Red's r8, n1-0 played): its map goes with it as it was, read as the room
            // reads a save; nothing draws it again, so no Key comes into it.
            var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "g23", "expedicion-0132.json"));
            var c = new Bastion.Net.EngineCampaignRules().MigrateCampaign(Bastion.Net.Json.Read<CampaignState>(text));
            var saved = Newtonsoft.Json.Linq.JObject.Parse(text)["run"]!["nodes"]!;
            Assert.True(Newtonsoft.Json.Linq.JToken.DeepEquals(saved, Newtonsoft.Json.Linq.JToken.Parse(Bastion.Net.Json.Write(c.Run!.Nodes))));
            Assert.DoesNotContain(c.Run.Nodes, (n) => n.Kind == NodeKinds.Key);
            // A new one in the same campaign may have it.
            Assert.Contains(Enumerable.Range(1, 50), (seed) => RunMap.Generate(c.Chapter ?? 1, seed).Any((n) => n.Kind == NodeKinds.Key));
        }

        [Fact]
        public void WonItGivesNoRewardItsArmouryIsIt()
        {
            // Won as a Saqueo (LLAVE.md): no special's 40–80 §; what it gives is what is searched.
            var (c, s) = Key();
            var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.True(report.Victory);
            Assert.Null(report.Run!.Reward);
        }

        [Fact]
        public void TheArmouryIsLockedAndFull()
        {
            foreach (var seed in Enumerable.Range(1, 20))
            {
                var (_, s) = Key(seed);
                Assert.True(s.Mission.KeyArmory);
                var a = Armory(s);
                // Its one door, locked.
                Assert.Single(s.Mission.Locked!);
                Assert.Equal(TileKinds.Door, Grid.KindAt(s, Door(s)));
                // All its furniture full: four racks.
                var racks = s.Interiors!.Single().Dressing.Where((d) => d.Kind == SearchPieces.WeaponRack).ToList();
                Assert.Equal(4, racks.Count);
                Assert.All(racks, (d) => Assert.Contains(Grid.NodeOf(s, new Pos { X = d.X, Y = d.Y, Z = 0 }), s.Mission.Pieces!));
                // Nobody in it.
                Assert.DoesNotContain(s.Units, (u) => u.Alive && u.Pos.Z == 0 && u.Pos.X >= a.X && u.Pos.Y >= a.Y && u.Pos.X < a.X + a.W && u.Pos.Y < a.Y + a.H);
                // Its key on one of the dead in uniform, not caged, that the squad gets to.
                var holder = s.Units.Single((u) => u.Id == s.Mission.KeyHolder);
                Assert.Equal(TemplateIds.Zombie, holder.Template);
                Assert.Contains(holder.Outfit, new[] { Outfits.Police, Outfits.Military });
                Assert.NotEqual(PoliceUses.Cells, InteriorSite.UseAt(s, holder.Pos));
                Assert.Equal(GearKinds.Key, s.Mission.Key!.Kind);
                Assert.Equal(KeyPlaces.Holder, Looting.KeyWhere(s));
            }
        }

        [Fact]
        public void NobodyWalksThroughTheLockedDoorAndOnlyTheKeyOpensIt()
        {
            var (_, s) = Key();
            var u = Soldier(s);
            var door = Door(s);
            u.Pos = Beside(s, door, u);
            u.Ap = 2;
            // No way through it, nor for the dead.
            Assert.DoesNotContain(Grid.NodeOf(s, door), Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost.Keys);
            var dead = s.Units.First((x) => x.Team == Teams.Alien && x.Alive);
            Assert.DoesNotContain(Grid.NodeOf(s, door), Paths.ComputeReach(s, dead, 1_000_000, ignore: (_) => true).Cost.Keys);
            // Without the key: no.
            Assert.Equal("Está cerrada. Hace falta la llave.", Abilities.AbilityBlocker(s, u, AbilityIds.OpenDoor));
            var refused = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.OpenDoor, Tile = door });
            Assert.False(refused.Ok);
            // With it: open, the key gone, the door unlocked for good.
            var key = s.Mission.Key!;
            s.Mission.Key = null;
            Assert.NotNull(Pack(u.Pack!, key));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.OpenDoor));
            var r = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.OpenDoor, Tile = door });
            Assert.True(r.Ok, r.Error);
            Assert.Contains(r.Events!, (e) => e is UnlockedEvent);
            Assert.Equal(TileKinds.DoorOpen, Grid.KindAt(s, door));
            Assert.Null(s.Mission.Locked);
            Assert.Null(Looting.KeyOf(u));
            Assert.Equal(KeyPlaces.Used, Looting.KeyWhere(s));
            Assert.True(Looting.ArmoryOpen(s));
            Assert.Contains(Grid.NodeOf(s, door), Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost.Keys);
        }

        [Fact]
        public void ItsHoldersBodyGivesIt()
        {
            var (_, s) = Key();
            var u = Soldier(s);
            var holder = s.Units.Single((x) => x.Id == s.Mission.KeyHolder);
            Events.ApplyEvent(s, new DiedEvent { Unit = holder.Id });
            // From on the body (Looting.ReachesToOpen), wherever it lay.
            u.Pos = Events.CopyPos(holder.Pos);
            u.Ap = 2;
            var r = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = holder.Pos });
            Assert.True(r.Ok, r.Error);
            Assert.NotNull(Looting.KeyOf(u));
            Assert.Null(s.Mission.Key);
            Assert.Equal(KeyPlaces.Pack, Looting.KeyWhere(s));
        }

        /// <summary>One of the dead other than its holder, dead on `at`, listed before the holder (so the first body there).</summary>
        private static Unit DeadOn(GameState s, Pos at)
        {
            var holder = s.Units.Single((x) => x.Id == s.Mission.KeyHolder);
            var other = s.Units.First((x) => x.Team == Teams.Alien && x.Alive && x.Id != holder.Id && x.Template == TemplateIds.Zombie);
            s.Units.Remove(other);
            s.Units.Insert(s.Units.IndexOf(holder), other);
            other.Pos = Events.CopyPos(at);
            Events.ApplyEvent(s, new DiedEvent { Unit = other.Id });
            return other;
        }

        [Fact]
        public void ItsHoldersBodyUnderAnotherGivesIt()
        {
            // Red, 19/11: two bodies on a tile, the holder's under the other (Looting.BodyAt took the first): searched, the
            // other's things and the tile marked, the key lost. Now the holder's comes first.
            var (_, s) = Key();
            var u = Soldier(s);
            var holder = s.Units.Single((x) => x.Id == s.Mission.KeyHolder);
            Events.ApplyEvent(s, new DiedEvent { Unit = holder.Id });
            DeadOn(s, holder.Pos);
            Assert.Same(holder, Looting.BodyAt(s, holder.Pos));
            u.Pos = Events.CopyPos(holder.Pos);
            u.Ap = 2;
            var r = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = holder.Pos });
            Assert.True(r.Ok, r.Error);
            Assert.NotNull(Looting.KeyOf(u));
            Assert.False(Looting.KeyLost(s));
        }

        [Fact]
        public void ItsHoldersBodyWhereAnotherWasSearchedGivesIt()
        {
            // The other searched first, the tile marked; then the holder falls on it: searchable again, the key alone.
            var (_, s) = Key();
            var u = Soldier(s);
            var holder = s.Units.Single((x) => x.Id == s.Mission.KeyHolder);
            var at = Events.CopyPos(holder.Pos);
            // The holder out of the way, on the soldier's tile; the soldier on the other's body.
            holder.Pos = Events.CopyPos(u.Pos);
            DeadOn(s, at);
            u.Pos = Events.CopyPos(at);
            u.Ap = 2;
            Assert.True(Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = at }).Ok);
            Assert.True(Looting.WasSearched(s, at));
            Assert.False(Looting.IsSearchable(s, at));
            Assert.Null(Looting.KeyOf(u));
            // The holder dies on it.
            holder.Pos = Events.CopyPos(at);
            Events.ApplyEvent(s, new DiedEvent { Unit = holder.Id });
            Assert.True(Looting.IsSearchable(s, at));
            Assert.False(Looting.KeyLost(s));
            u.Ap = 2;
            var before = u.Pack!.Items.Count;
            var r = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = at });
            Assert.True(r.Ok, r.Error);
            var searched = r.Events!.OfType<SearchedEvent>().Single();
            Assert.Equal(GearKinds.Key, Assert.Single(searched.Packed!).Gear.Kind);
            Assert.Null(searched.Floor);
            Assert.Equal(before + 1, u.Pack.Items.Count);
            Assert.NotNull(Looting.KeyOf(u));
            // Searched now, its tile is done.
            Assert.False(Looting.IsSearchable(s, at));
            Assert.Single(s.Searched!, (n) => n == Grid.NodeOf(s, at));
            Assert.False(Looting.KeyLost(s));
        }

        [Fact]
        public void TheKeyIsNeverLostWhereverItGoes()
        {
            // Looting.KeyLost (the bench's invariant): on its holder alive, its body, the floor, a pack, La Lata, the door.
            var (_, s) = Key();
            Assert.False(Looting.KeyLost(s));
            var holder = s.Units.Single((x) => x.Id == s.Mission.KeyHolder);
            Events.ApplyEvent(s, new DiedEvent { Unit = holder.Id });
            Assert.False(Looting.KeyLost(s));
            // A body whose tile can't be searched (out of reach): lost; KeyLost says so.
            (s.Mission.OutOfReach ??= new List<int>()).Add(Grid.NodeOf(s, holder.Pos));
            Assert.True(Looting.KeyLost(s));
            s.Mission.OutOfReach.Remove(Grid.NodeOf(s, holder.Pos));
            var u = Soldier(s);
            var key = s.Mission.Key!;
            s.Mission.Key = null;
            Pack(u.Pack!, key);
            Assert.False(Looting.KeyLost(s));
            // In La Lata with it: there still.
            u.Alive = false;
            u.Evacuated = true;
            u.Aboard = true;
            Assert.False(Looting.KeyLost(s));
            Assert.Equal(KeyPlaces.Pack, Looting.KeyWhere(s));
        }

        [Fact]
        public void ItFallsWhereItsHoldersBodyRises()
        {
            // Bodies never go (nothing takes a unit off the map); one rises: the key on the floor there, taken from on it.
            var (_, s) = Key();
            var holder = s.Units.Single((x) => x.Id == s.Mission.KeyHolder);
            Events.ApplyEvent(s, new DiedEvent { Unit = holder.Id });
            var risen = Events.CloneUnit(holder);
            risen.Id = "z99";
            risen.Alive = true;
            Events.ApplyEvent(s, new ReanimatedEvent { Corpse = holder.Id, Zombie = risen });
            Assert.Null(s.Mission.Key);
            var lying = s.FloorGear!.Single((d) => d.Gear.Kind == GearKinds.Key);
            Assert.True(Grid.SamePos(holder.Pos, lying.Pos));
            Assert.Null(lying.From);
            Assert.Equal(KeyPlaces.Floor, Looting.KeyWhere(s));
        }

        [Fact]
        public void ItGoesWithTheFallensPackAndNeverHome()
        {
            var (c, s) = Key();
            var u = Soldier(s);
            var key = s.Mission.Key!;
            s.Mission.Key = null;
            Assert.NotNull(Pack(u.Pack!, key));
            // Falls with its pack (G16): on the floor where it fell, a partner's from beside.
            Events.ApplyEvent(s, new DiedEvent { Unit = u.Id });
            Assert.Contains(s.FloorGear!, (d) => d.Gear.Id == key.Id && d.From == u.Id);
            Assert.Equal(KeyPlaces.Floor, Looting.KeyWhere(s));
            // Never home: out of every kit when the fight is folded in.
            var other = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive);
            Assert.NotNull(Pack(other.Pack!, Gears.CloneGear(key)));
            EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.DoesNotContain(c.Soldiers.Where((x) => x.Kit != null).SelectMany((x) => x.Kit!.Pack.Items), (p) => p.Gear.Kind == GearKinds.Key);
        }

        [Fact]
        public void ItsWallsBreakAsAny()
        {
            // Arturo, cuadro 79: «también volando la pared»; nothing in the way of the blast but a wall, as anywhere.
            var (_, s) = Key();
            var a = Armory(s);
            var wall = new Pos { X = a.X - 1, Y = a.Y + a.H - 1, Z = 0 };
            Assert.Equal(TileKinds.Wall, Grid.KindAt(s, wall));
            var center = new Pos { X = wall.X - 1, Y = wall.Y, Z = 0 };
            Assert.Contains(Abilities.BreachedWalls(s, AbilityIds.Grenade, center), (p) => Grid.SamePos(p, wall));
            // A hole in it: the armoury open (its objective's step), the door locked still and the key where it was.
            Assert.False(Looting.ArmoryOpen(s));
            Events.ApplyEvent(s, new TileChangedEvent { Pos = wall, Kind = TileKinds.Low, Decor = DecorKinds.Breach });
            Assert.True(Looting.ArmoryOpen(s));
            Assert.NotNull(s.Mission.Locked);
            Assert.Equal(KeyPlaces.Holder, Looting.KeyWhere(s));
        }
    }
}
