using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// MissionBot in La llave (G23.1; Gameplay, 19/11): it kills and searches the dead in uniform till the key turns up,
    /// picks it up, opens the armoury with it, searches its furniture from inside and goes; never refused, and never told
    /// which of the dead holds the key (Mission.KeyHolder).
    /// </summary>
    public class NetRunKeyBotTests
    {
        private readonly ITestOutputHelper output;

        public NetRunKeyBotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>La llave on map seed `seed`, its squad of four (4 seats); with `rifles`, each with a rifle, a pistol, a vest and magazines, as NetRunMissionBotTests' Saqueo.</summary>
        private static GameState Key(int seed, bool rifles = false) => LaunchAt(NodeKinds.Key, seed, before: (c) =>
        {
            if (!rifles) return;
            foreach (var x in c.Soldiers)
            {
                InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
                InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
            }
        }, node: (n) =>
        {
            n.Site = Places.Police;
            n.Seed = seed;
        }).S;

        private static InteriorRoom Armory(GameState s) => s.Interiors!.Single((i) => i.Kind == InteriorKinds.Police).Rooms.First((r) => r.Use == PoliceUses.Armory && r.Z == 0);

        private static bool InArmory(GameState s, Pos p)
        {
            var a = Armory(s);
            return p.Z == 0 && p.X >= a.X && p.Y >= a.Y && p.X < a.X + a.W && p.Y < a.Y + a.H;
        }

        private static Pos Door(GameState s) => Grid.PosOf(s, s.Mission.Locked!.Single());

        /// <summary>
        /// `seed`'s La llave with nobody of the dead left and one soldier to play, the first, put on `at` (a place of the
        /// map; null: where it is) with its actions; the rest done this turn. What the bot does next is that one's.
        /// </summary>
        private static (GameState S, Unit U) Alone(int seed, System.Func<GameState, Unit, Pos>? at = null)
        {
            var s = Key(seed);
            s.Units.RemoveAll((x) => x.Team == Teams.Alien);
            s.Pods.Clear();
            var squad = s.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive).ToList();
            foreach (var x in squad.Skip(1))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            var u = squad[0];
            if (at != null) u.Pos = at(s, u);
            u.Ap = 2;
            u.Moves = null;
            return (s, u);
        }

        /// <summary>A free place beside the locked door, out of the armoury, that reaches it with the hands.</summary>
        private static Pos ByTheDoor(GameState s, Unit u)
        {
            var door = Door(s);
            return Grid.Dirs8.Select((d) => new Pos { X = door.X + d.X, Y = door.Y + d.Y, Z = 0 })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && !InArmory(s, q) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q)
                    && (Grid.UnitAt(s, q) == null || Grid.UnitAt(s, q) == u) && Looting.ReachesToSearch(s, q, door));
        }

        private static void IntoPack(Unit u, Gear g)
        {
            var at = Gears.FirstFit(u.Pack!, g);
            Assert.NotNull(at);
            Gears.Place(u.Pack!, g, at!.X, at.Y, at.Turned);
        }

        [Fact]
        public void WithTheKeyByTheDoorItOpensIt()
        {
            var (s, u) = Alone(12, ByTheDoor);
            var key = s.Mission.Key!;
            s.Mission.Key = null;
            IntoPack(u, key);
            Assert.Equal(KeyPlaces.Pack, Looting.KeyWhere(s));
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.OpenDoor }, Bastion.Net.Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.Equal(KeyPlaces.Used, Looting.KeyWhere(s));
        }

        [Fact]
        public void ItPicksTheKeyUpWhereItLies()
        {
            var (s, u) = Alone(12);
            var key = s.Mission.Key!;
            s.Mission.Key = null;
            s.FloorGear = new List<DroppedGear> { new DroppedGear { Gear = key, Pos = Events.CopyPos(u.Pos) } };
            Assert.Equal(KeyPlaces.Floor, Looting.KeyWhere(s));
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is ItemCommand { Action: ItemActions.Take } take && take.Gear == key.Id, Bastion.Net.Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.NotNull(Looting.KeyOf(u));
        }

        [Fact]
        public void ABodyInUniformBesideItIsSearchedBeforeAnyShot()
        {
            // The holder fallen beside the soldier, one of the dead in sight a few tiles off: the body first (R10, 19/11:
            // with the dead coming every turn, the holder's lay by the squad the whole fight, never searched).
            var s = Key(12);
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive);
            foreach (var x in s.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Id != u.Id))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            var holder = s.Units.Single((x) => x.Id == s.Mission.KeyHolder);
            var spot = Grid.Dirs4.Select((d) => new Pos { X = u.Pos.X + d.X, Y = u.Pos.Y + d.Y, Z = 0 })
                .First((p) => Grid.InBounds(s, p.X, p.Y) && Grid.IsWalkable(Grid.KindAt(s, p)) && Grid.UnitAt(s, p) == null && Looting.ReachesToSearch(s, u.Pos, p));
            holder.Pos = spot;
            holder.Alive = false;
            holder.Hp = 0;
            var near = s.Units.First((x) => x.Team == Teams.Alien && x.Alive && x.Id != holder.Id);
            near.Pos = Grid.Dirs8.Select((d) => new Pos { X = u.Pos.X + d.X * 3, Y = u.Pos.Y + d.Y * 3, Z = 0 })
                .First((p) => Grid.InBounds(s, p.X, p.Y) && Grid.IsWalkable(Grid.KindAt(s, p)) && Grid.UnitAt(s, p) == null);
            u.Ap = 2;
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Search } search && Grid.SamePos(search.Tile!, spot), Bastion.Net.Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.NotNull(Looting.KeyOf(u));
        }

        [Fact]
        public void WithTheArmouryOpenItGoesInAndSearchesIt()
        {
            var (s, u) = Alone(12, ByTheDoor);
            var key = s.Mission.Key!;
            s.Mission.Key = null;
            IntoPack(u, key);
            Assert.True(Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.OpenDoor, Tile = Door(s) }).Ok);
            u.Ap = 2;
            u.Moves = null;
            // In through the door, never stopping in it, then a piece searched.
            var orders = new List<Command>();
            for (var k = 0; k < 6; k++)
            {
                var cmd = MissionBot.Next(s, 0, Zero);
                Assert.NotNull(cmd);
                orders.Add(cmd!);
                Assert.True(Exec(s, cmd!).Ok, Bastion.Net.Json.Write(cmd));
                if (cmd is AbilityCommand { Ability: AbilityIds.Search }) break;
            }
            var search = Assert.IsType<AbilityCommand>(orders.Last());
            Assert.Equal(AbilityIds.Search, search.Ability);
            Assert.True(InArmory(s, search.Tile!));
            Assert.True(InArmory(s, u.Pos));
        }

        /// <summary>What a fight played by both seats' bots came to: the key's furthest place, the armoury's furniture searched, the end.</summary>
        private sealed class Played
        {
            public string Key = KeyPlaces.Holder;
            public int Searched;
            public int Refused;
            public int Turns;
            public string Outcome = "";
            public int Fallen;
        }

        private static Played Play(GameState s, int seed)
        {
            var connected = new[] { 0, 1 };
            var rng = new Rng(seed * 7919 + 13);
            var played = new Played();
            var order = new[] { KeyPlaces.Holder, KeyPlaces.Floor, KeyPlaces.Pack, KeyPlaces.Used };
            var pieces = new HashSet<int>(s.Mission.Pieces ?? new List<int>());
            int orders = 0, idle = 0;
            while (s.Outcome == null && orders < 4000 && idle < 3)
            {
                var any = false;
                foreach (var slot in connected)
                {
                    var cmd = MissionBot.Next(s, slot, connected);
                    if (cmd == null) continue;
                    orders++;
                    if (Rules.ExecuteCommand(s, cmd, slot, connected, rng).Ok) any = true;
                    else played.Refused++;
                    var where = Looting.KeyWhere(s) ?? KeyPlaces.Holder;
                    if (System.Array.IndexOf(order, where) > System.Array.IndexOf(order, played.Key)) played.Key = where;
                }
                idle = any ? 0 : idle + 1;
            }
            played.Searched = (s.Searched ?? new List<int>()).Count((n) => pieces.Contains(n) && InArmory(s, Grid.PosOf(s, n)));
            played.Turns = s.Turn;
            played.Outcome = s.Outcome ?? "-";
            played.Fallen = s.Units.Count((x) => x.Team == Teams.Xcom && x.CampaignId != null && !x.Alive && x.Aboard != true && !x.Evacuated);
            return played;
        }

        /// <summary>
        /// Twenty fights each way, both seats' bots, said one by one: never refused; with rifles, the armoury opened and
        /// searched in some (R9, 19/11: 10 opened, 6 searched whole; with the knife alone, 1 opened).
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ItGoesForTheKeyAndTheArmoury(bool rifles)
        {
            var all = new List<Played>();
            foreach (var seed in Enumerable.Range(1, 20))
            {
                var p = Play(Key(seed, rifles), seed);
                all.Add(p);
                output.WriteLine($"seed {seed}: key {p.Key}, armoury searched {p.Searched}, {p.Outcome} in turn {p.Turns}, fallen {p.Fallen}, refused {p.Refused}");
            }
            Assert.All(all, (p) => Assert.Equal(0, p.Refused));
            if (rifles) Assert.Contains(all, (p) => p.Key == KeyPlaces.Used && p.Searched > 0);
        }
    }
}
