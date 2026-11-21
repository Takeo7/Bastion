using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// MissionBot with a bow or a crossbow (Arco y ballesta, cuadros 101–106; Gameplay, 21/11): armed while it has arrows
    /// (Unit.Arrows: the bow's Ammo says nothing), the crossbow reloaded with one; the arrows in a body beside it taken back;
    /// while a camp of the living sleeps, one of them unaware shot only for a silent kill (Rules.SilentKill), never with a gun.
    /// On the base (HeardGame), a pile of arrows from the stash into the pack of one with a bow; another's, to the stash.
    /// </summary>
    public class NetRunBowBotTests
    {
        private static readonly int[] Seats = { 2, 4, 6, 8 };

        /// <summary>A run's fight of `kind` at `site` on map seed 100 + `seed`, 4 seats alone, launched as a bench does it.</summary>
        private static GameState Fight(string kind, string site, int seed)
        {
            var c = Solo(seed);
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = System.Array.IndexOf(Seats, 4);
            var rng = new Rng(seed * 101);
            while (c.Soldiers.Count((x) => x.Alive) < 4) Runs.NewSoldier(c, c.Players[0], rng);
            c.Squad = Runs.DefaultSquad(c, c.Players);
            var run = Start(c, seed);
            var n = RunMap.Choices(run)[0];
            n.Kind = kind;
            n.Modifier = null;
            n.Seed = 100 + seed;
            n.Site = site;
            n.Difficulty = 2;
            Assert.True(Ok(c, new ChooseNodeCommand { Node = n.Id }).Launch);
            return new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
        }

        /// <summary>The first soldier with `weapon` in the hands (loaded) and `arrows`, sure to hit; the rest of the squad still.</summary>
        private static Unit Archer(GameState s, string weapon, int arrows)
        {
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive);
            u.Arms = new Arms { Primary = weapon, Pistol = false };
            u.Mods = new UnitMods { Aim = 300 };
            u.Arrows = arrows;
            u.Mags = 0;
            u.Ammo = Content.ClipOf(u, AbilityIds.Shoot);
            u.Ap = 2;
            foreach (var x in s.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Id != u.Id))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            return u;
        }

        private static bool Free(GameState s, Pos p) => Grid.InBounds(s, p.X, p.Y) && Grid.IsWalkable(Grid.KindAt(s, p)) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null;

        /// <summary>One of the dead of the map, awake, `range` tiles east of `u` in the open, its line to `u` clear.</summary>
        private static Unit AwakeAhead(GameState s, Unit u, int range)
        {
            var z = s.Units.First((x) => x.Alive && x.Team == Teams.Alien && x.Template == TemplateIds.Zombie);
            var at = new Pos { X = u.Pos.X + range, Y = u.Pos.Y, Z = 0 };
            z.Pos = at;
            s.Pods.First((p) => p.Id == z.PodId).Active = true;
            return z;
        }

        /// <summary>Somewhere in the open with `range` free tiles east of it, for `u` and one of the dead.</summary>
        private static void Open(GameState s, Unit u, int range)
        {
            u.Pos = Enumerable.Range(0, s.Width * s.Height).Select((i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 })
                .First((p) => Enumerable.Range(0, range + 1).All((k) => Free(s, new Pos { X = p.X + k, Y = p.Y, Z = 0 }) && Free(s, new Pos { X = p.X + k, Y = p.Y + 1, Z = 0 }) && Free(s, new Pos { X = p.X + k, Y = p.Y - 1, Z = 0 }))
                    && s.Units.All((x) => !x.Alive || x.Team == Teams.Xcom || Grid.Distance(x.Pos, p) > 12));
            s.Concealed = false;
        }

        [Fact]
        public void WithArrowsTheBowShootsWithoutThemNotAndTheCrossbowReloads()
        {
            var s = Fight(NodeKinds.Elimination, Places.Country, 3);
            var u = Archer(s, FoundWeaponIds.Bow, 5);
            Open(s, u, 4);
            var z = AwakeAhead(s, u, 4);
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Shoot } a && a.Target == z.Id, Json.Write(cmd));
            // No arrows: not armed, no shot with it.
            u.Arrows = 0;
            cmd = MissionBot.Next(s, 0, Zero);
            Assert.False(cmd is AbilityCommand { Ability: AbilityIds.Shoot }, Json.Write(cmd));
            // The crossbow empty, arrows in the pack: reloaded.
            u = Archer(s, FoundWeaponIds.Crossbow, 3);
            u.Ammo = 0;
            cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Reload }, Json.Write(cmd));
        }

        [Fact]
        public void TheArrowsInABodyBesideAreTakenBack()
        {
            var s = Fight(NodeKinds.Elimination, Places.Country, 3);
            var u = Archer(s, FoundWeaponIds.Bow, 1);
            Open(s, u, 4);
            var b = s.Units.First((x) => x.Alive && x.Team == Teams.Alien && x.Template == TemplateIds.Zombie);
            b.Pos = new Pos { X = u.Pos.X + 1, Y = u.Pos.Y, Z = 0 };
            b.Alive = false;
            b.Hp = 0;
            b.ArrowsIn = 2;
            Assert.True(Looting.IsSearchable(s, b.Pos));
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Search, Tile: Pos t } && Grid.SamePos(t, b.Pos), Json.Write(cmd));
        }

        [Fact]
        public void WhileTheCampSleepsOneUnawareIsShotOnlyForASilentKill()
        {
            // Saqueadores, the camp asleep: the round's guard from behind, 4 to 6 tiles off, nobody else to see, an arrow sure to
            // kill him (2 hp: the bow does 2 to 4). With the bow, shot (it dies unseen); at a coin's odds (4 hp), not (a miss or
            // a wound gives the squad away, and wakes the camp); with a rifle, never (G23.5).
            var s = Fight(NodeKinds.Raiders, Places.Country, 3);
            Assert.NotNull(s.Mission.Raiders);
            var g = s.Units.First((x) => x.Alive && x.Guard == GuardPosts.Round);
            g.Hp = 2;
            var u = Archer(s, FoundWeaponIds.Bow, 5);
            var sim = new Sim(s, new Rng(0), new List<int> { 0 });
            u.Pos = Enumerable.Range(0, s.Width * s.Height).Select((i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 })
                .Where((p) => Free(s, p) && Grid.Distance(p, g.Pos) is >= 4 and <= 6 && !Los.AheadOf(g, p))
                .First((p) =>
                {
                    u.Pos = p;
                    return sim.PodsNoticing(p, u).Count == 0 && Aim.PreviewShot(s, u, g, new ShotOptions { Ability = AbilityIds.Shoot }) != null && Rules.SilentKill(s, u, g);
                });
            Assert.True(Paths.OnPatrol(s, g));
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Shoot } a && a.Target == g.Id, Json.Write(cmd));
            g.Hp = 4;
            cmd = MissionBot.Next(s, 0, Zero);
            Assert.False(cmd is AbilityCommand { Target: string coin } && coin == g.Id, Json.Write(cmd));
            g.Hp = 2;
            u.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Pistol = false };
            u.Ammo = Content.ClipOf(u, AbilityIds.Shoot);
            cmd = MissionBot.Next(s, 0, Zero);
            Assert.False(cmd is AbilityCommand { Target: string t } && t == g.Id, Json.Write(cmd));
        }

        [Fact]
        public void OnTheBaseTheArrowsGoToTheArcherOnly()
        {
            var c = Solo();
            var (archer, rifleman) = (Soldier(c), Soldier(c, 0, 1));
            InSlot(c, archer, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.Bow);
            InSlot(c, rifleman, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            var stray = InPack(c, rifleman, GearKinds.Arrows, null, 0, 0, 4);
            InStash(c, GearKinds.Arrows, null, 0, 0, Content.ArrowsPile);
            var heard = new HeardGame(new FakeClock());
            heard.Observe(new RoomMessage { Phase = RoomPhases.Base, Players = new List<PlayerInfo> { new PlayerInfo { Slot = 0, Name = "P0", Connected = true } } });
            heard.Observe(new CampaignMessage { State = c });
            var next = heard.BaseOrder(0, true);
            for (var n = 0; next is MoveGearCommand && n < 40; n++)
            {
                Ok(c, next, 0, Zero);
                next = heard.BaseOrder(0, true);
            }
            Assert.IsNotType<MoveGearCommand>(next);
            Assert.Contains(archer.Kit!.Pack.Items, (p) => p.Gear.Kind == GearKinds.Arrows);
            Assert.DoesNotContain(rifleman.Kit!.Pack.Items, (p) => p.Gear.Kind == GearKinds.Arrows);
            Assert.Contains(c.Stash!.Items, (p) => p.Gear == stray);
            Assert.All(c.Soldiers.Where((s) => s != archer && s.Alive), (s) => Assert.DoesNotContain(s.Kit!.Pack.Items, (p) => p.Gear.Kind == GearKinds.Arrows));
        }
    }
}
