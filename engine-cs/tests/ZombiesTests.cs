using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/zombies.test.ts, first half (noise, the dead, whom they follow, the groups, the lone dead,
    /// surrounded, the Hinchado, the Chillón); ZombiesTests2 has the rest. Phase 2 of the zombies (docs/FASE2.md): noise,
    /// the dead, searching and the Saqueo.
    /// </summary>
    public class ZombiesTests
    {
        // ------------------------------------------------------------ the test file's own helpers

        internal static GameState Field(int w = 20, int h = 20)
        {
            return StateFrom(Enumerable.Range(0, h).Select((_) => new string('.', w)).ToArray());
        }

        /// <summary>`pod(s, id, active, units)`.</summary>
        internal static Pod PodOf(GameState s, string id, bool active, params Unit[] units)
        {
            return Step2Helpers.Pod(s, id, units, active);
        }

        internal static void Hp(Unit u, int hp)
        {
            u.Hp = hp;
            u.MaxHp = hp;
        }

        /// <summary>`Math.round(radius * SQUAD_LOUDER * 10) / 10`: how far the squad's noise of `radius` is heard.</summary>
        internal static double Louder(double radius)
        {
            return Js.Round(radius * Rules.SquadLouder * 10) / 10;
        }

        /// <summary>toEqual on a whole noise event.</summary>
        internal static void SameNoise(NoiseEvent e, Pos tile, double radius, string? by, params string[] heard)
        {
            SamePlace(tile, e.Tile);
            Assert.Equal(radius, e.Radius);
            Assert.Equal(by, e.By);
            Assert.Equal(heard, e.Heard);
        }

        /// <summary>toBeCloseTo (2 digits): within 0.005.</summary>
        internal static void Close(double expected, double? actual)
        {
            Assert.NotNull(actual);
            Assert.True(Math.Abs(expected - actual!.Value) < 0.005, $"expected {expected} ± 0.005, got {actual}");
        }

        internal static string Who(GameEvent e)
        {
            return e is MovedEvent m ? m.Unit : e is ShotEvent sh ? sh.Unit : "";
        }

        /// <summary>toEqual through JSON (null and missing alike, as the README has it): the first difference, named.</summary>
        internal static void SameJson(object? expected, object? actual)
        {
            var a = expected == null ? null : JsonSerializer.SerializeToNode(expected, expected.GetType(), Options);
            var b = actual == null ? null : JsonSerializer.SerializeToNode(actual, actual.GetType(), Options);
            var diff = JsonDiff(a, b, "");
            if (diff != null) throw new Xunit.Sdk.XunitException(diff);
        }

        private static string Show(JsonNode? n)
        {
            var text = n == null ? "null" : n.ToJsonString();
            return text.Length > 300 ? text.Substring(0, 300) + "…" : text;
        }

        private static string? JsonDiff(JsonNode? a, JsonNode? b, string path)
        {
            if (a == null && b == null) return null;
            if (a == null || b == null || a.GetValueKind() != b.GetValueKind()) return $"{path}: expected {Show(a)}, got {Show(b)}";
            if (a is JsonObject x && b is JsonObject y)
            {
                var keys = x.Select((p) => p.Key).ToList();
                foreach (var p in y) if (!keys.Contains(p.Key)) keys.Add(p.Key);
                foreach (var k in keys)
                {
                    var d = JsonDiff(x[k], y[k], $"{path}.{k}");
                    if (d != null) return d;
                }
                return null;
            }
            if (a is JsonArray xs && b is JsonArray ys)
            {
                for (var i = 0; i < Math.Min(xs.Count, ys.Count); i++)
                {
                    var d = JsonDiff(xs[i], ys[i], $"{path}[{i}]");
                    if (d != null) return d;
                }
                return xs.Count == ys.Count ? null : $"{path}: {xs.Count} items expected, got {ys.Count}: expected {Show(a)}, got {Show(b)}";
            }
            return JsonNode.DeepEquals(a, b) ? null : $"{path}: expected {Show(a)}, got {Show(b)}";
        }

        // ------------------------------------------------------------ noise

        [Fact(DisplayName = "noise › every weapon and action is heard as far as the table says")]
        public void EveryWeaponAndActionIsHeardAsFarAsTheTableSays()
        {
            var s = Field();
            Unit Kit(string template) => AddUnit(s, $"s{s.Units.Count}", template, 0, 0);
            // Louder since 17/10 (Arturo: «disparar debería hacer más ruido»).
            Assert.Equal(12, Abilities.NoiseOf(s, Kit(TemplateIds.Sharpshooter), AbilityIds.Pistol));
            Assert.Equal(16, Abilities.NoiseOf(s, Kit(TemplateIds.Assault), AbilityIds.Shoot));
            Assert.Equal(18, Abilities.NoiseOf(s, Kit(TemplateIds.Rookie), AbilityIds.Shoot));
            Assert.Equal(20, Abilities.NoiseOf(s, Kit(TemplateIds.Grenadier), AbilityIds.Shoot));
            Assert.Equal(22, Abilities.NoiseOf(s, Kit(TemplateIds.Sharpshooter), AbilityIds.Shoot));
            Assert.Equal(26, Abilities.NoiseOf(s, Kit(TemplateIds.Rookie), AbilityIds.Grenade));
            Assert.Equal(2, Abilities.NoiseOf(s, Kit(TemplateIds.Rookie), AbilityIds.Search));
            Assert.Equal(1, Abilities.NoiseOf(s, Kit(TemplateIds.Assault), AbilityIds.Slash));
            Assert.Equal(0, Abilities.NoiseOf(s, Kit(TemplateIds.Rookie), AbilityIds.Overwatch));
            Assert.Equal(16, Abilities.NoiseOf(s, AddUnit(s, "a1", TemplateIds.Sectoid, 0, 0), AbilityIds.Mindspin));
            Assert.Equal((1, 6, 3, 16), (Content.Noise.Door, Content.Noise.Breach, Content.Noise.Dash, Content.Noise.Alarm));
        }

        [Fact(DisplayName = "noise › those in earshot that see nobody go to look; the busy, the deaf and the maker’s own pod do not")]
        public void ThoseInEarshotThatSeeNobodyGoToLook()
        {
            var s = Field(30, 30);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 2, 2);
            var busy = AddUnit(s, "a1", TemplateIds.Zombie, 2, 8);
            var idle = AddUnit(s, "a2", TemplateIds.Zombie, 12, 2);
            var deaf = AddUnit(s, "a3", TemplateIds.Zombie, 2, 20);
            PodOf(s, "p1", true, busy);
            PodOf(s, "p2", false, idle);
            PodOf(s, "p3", false, deaf);
            var run = NewSim(s);
            run.MakeNoise(soldier.Pos, 12, soldier);
            var noise = Of<NoiseEvent>(run.Events);
            Assert.Single(noise);
            SameNoise(noise[0], P(2, 2), Louder(12), "s1", "a2");
            SamePlace(P(2, 2), idle.Heard);
            Assert.Null(busy.Heard);
            Assert.Null(deaf.Heard);
            Assert.False(s.Pods.First((p) => p.Id == "p2").Active);
            // The squad's noise winds up the horde's clock.
            Assert.Equal(Content.HordePoints(12), s.Mission.Noise);
            // A zombie's own pod knows what the noise was.
            var mate = AddUnit(s, "a4", TemplateIds.Zombie, 13, 2);
            s.Pods.First((p) => p.Id == "p2").UnitIds.Add("a4");
            mate.PodId = "p2";
            var again = NewSim(s);
            again.MakeNoise(idle.Pos, 6, idle);
            Assert.Empty(Of<NoiseEvent>(again.Events)[0].Heard);
            Assert.Equal(Content.HordePoints(12), s.Mission.Noise);
        }

        [Fact(DisplayName = "noise › noise never gives the squad away: a pod drawn by a dash wakes only when it sees someone")]
        public void NoiseNeverGivesTheSquadAway()
        {
            // A wall between the street and the yard, open at the far end.
            var s = StateFrom(new[] { "....................", "....................", "###################.", "...................." });
            s.Concealed = true;
            var runner = AddUnit(s, "s1", TemplateIds.Rookie, 0, 1);
            // A partner still to act: the squad's turn goes on.
            AddUnit(s, "s2", TemplateIds.Rookie, 0, 0);
            var z1 = AddUnit(s, "a1", TemplateIds.Zombie, 13, 3);
            var z2 = AddUnit(s, "a2", TemplateIds.Zombie, 12, 3);
            PodOf(s, "p1", false, z1, z2);
            var events = Exec(s, Move("s1", 13, 1));
            Assert.True(events.Ok);
            Assert.Equal(0, runner.Ap);
            var noise = Of<NoiseEvent>(events.Events!);
            Assert.Single(noise);
            SameNoise(noise[0], P(13, 1), Louder(Content.Noise.Dash), "s1", "a1", "a2");
            Assert.True(s.Concealed);
            Assert.False(s.Pods[0].Active);
            // On their turn they come round the end of the wall, see the runner and only then wake up.
            var turn = NewSim(s, 2);
            turn.RunAlienTurn();
            Assert.True(z1.Pos.X > 13);
            var woke = Of<PodActivatedEvent>(turn.Events);
            Assert.NotEmpty(woke);
            Assert.Equal(("p1", PodCauses.Patrol), (woke[0].Pod, woke[0].Cause));
            var broken = Of<ConcealmentBrokenEvent>(turn.Events);
            Assert.NotEmpty(broken);
            Assert.Equal(ConcealReasons.Patrol, broken[0].Reason);
        }

        [Fact(DisplayName = "noise › opening a door is quiet; going through a window is heard")]
        public void OpeningADoorIsQuiet()
        {
            var s = StateFrom(new[] { "..........", "....+=....", ".........." });
            AddUnit(s, "s1", TemplateIds.Rookie, 4, 0);
            AddUnit(s, "s2", TemplateIds.Rookie, 5, 0);
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 9, 2);
            PodOf(s, "p1", false, z);
            var r = Exec(s, Move("s1", 4, 2));
            Assert.True(r.Ok);
            var door = Of<NoiseEvent>(r.Events!);
            Assert.Single(door);
            SameNoise(door[0], P(4, 1), Louder(Content.Noise.Door), "s1");
            var w = Exec(s, Move("s2", 5, 2));
            Assert.True(w.Ok);
            var window = Of<NoiseEvent>(w.Events!);
            Assert.NotEmpty(window);
            Assert.Equal((5, 1), (window[0].Tile.X, window[0].Tile.Y));
            Assert.Equal(Louder(Content.Noise.Breach), window[0].Radius);
        }

        [Fact(DisplayName = "noise › a pod that finds nobody where it heard something forgets it and stays unaware")]
        public void APodThatFindsNobodyForgetsIt()
        {
            var s = StateFrom(new[] { "....#.........", "....#.........", "....#........." });
            s.Concealed = true;
            AddUnit(s, "s1", TemplateIds.Rookie, 0, 1);
            var z1 = AddUnit(s, "a1", TemplateIds.Zombie, 13, 1, extra: (u) => u.Heard = P(6, 1));
            var z2 = AddUnit(s, "a2", TemplateIds.Zombie, 13, 2, extra: (u) => u.Heard = P(6, 1));
            PodOf(s, "p1", false, z1, z2);
            var events = new List<GameEvent>();
            for (var i = 0; i < 4 && z1.Heard != null; i++)
            {
                var turn = NewSim(s, i);
                turn.RunAlienTurn();
                events.AddRange(turn.Events);
            }
            Assert.True(z1.Pos.X <= 7);
            Assert.Equal(new[] { "a1", "a2" }, Of<ForgotEvent>(events).Select((e) => e.Unit).OrderBy((id) => id, StringComparer.Ordinal));
            Assert.Null(z1.Heard);
            Assert.False(s.Pods[0].Active);
            Assert.True(s.Concealed);
        }

        // ------------------------------------------------------------ the dead

        [Fact(DisplayName = "the dead › the zombie templates fight with their bodies: no guns, no cover")]
        public void TheZombieTemplatesFightWithTheirBodies()
        {
            foreach (var id in new[] { TemplateIds.Zombie, TemplateIds.Xenoid, TemplateIds.Mec, TemplateIds.Sectoid, TemplateIds.Bloater })
            {
                var t = Content.Templates[id];
                Assert.True(t.Undead);
                Assert.DoesNotContain(AbilityIds.Shoot, t.Abilities);
                Assert.DoesNotContain(AbilityIds.Overwatch, t.Abilities);
            }
            Assert.Equal(new[] { AbilityIds.Mindspin, AbilityIds.Reanimate }, Content.Templates[TemplateIds.Sectoid].Abilities);
            Assert.True(Content.Templates[TemplateIds.Xenoid].Mobility > Content.Templates[TemplateIds.Zombie].Mobility);
            Assert.Equal(1, Content.Templates[TemplateIds.Mec].Armor);
            var bloater = Content.Templates[TemplateIds.Bloater];
            Assert.Equal(("Hinchado", "HIN"), (bloater.Name, bloater.Short));
            Assert.Equal(new[] { AbilityIds.Burst }, bloater.Abilities);
            Assert.False(Content.Templates[TemplateIds.Trooper].Undead);
        }

        [Fact(DisplayName = "the dead › a zombie bites the nearest soldier in reach, and otherwise lurches toward the nearest it sees")]
        public void AZombieBitesTheNearestSoldierInReach()
        {
            var s = Field(30, 30);
            s.ActiveTeam = Teams.Alien;
            var near = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => Hp(u, 50));
            AddUnit(s, "s2", TemplateIds.Rookie, 5, 12, extra: (u) => Hp(u, 50));
            var biter = AddUnit(s, "a1", TemplateIds.Zombie, 8, 5);
            var walker = AddUnit(s, "a2", TemplateIds.Zombie, 18, 5);
            PodOf(s, "p1", true, biter, walker);
            var turn = NewSim(s);
            turn.RunAlienTurn();
            var bites = Of<ShotEvent>(turn.Events).Where((e) => e.Unit == "a1").ToList();
            Assert.Single(bites);
            Assert.Equal((AbilityIds.Claw, "s1"), (bites[0].Ability, bites[0].Target));
            // Ten tiles off: two actions of walking (4 tiles each), straight at it, no shot.
            Assert.DoesNotContain(Of<ShotEvent>(turn.Events), (e) => e.Unit == "a2");
            Assert.True(Js.Hypot(walker.Pos.X - near.Pos.X, walker.Pos.Y - near.Pos.Y) <= 13 - 8 + 0.5);
        }

        [Fact(DisplayName = "the dead › whoever goes for the squad acts first; then the dead with nobody in reach walk, all together (playtest 2026-10-16)")]
        public void WhoeverGoesForTheSquadActsFirst()
        {
            var s = Field(40, 30);
            s.ActiveTeam = Teams.Alien;
            AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => Hp(u, 50));
            var biter = AddUnit(s, "a1", TemplateIds.Zombie, 8, 5);
            var far1 = AddUnit(s, "a2", TemplateIds.Zombie, 30, 5);
            var far2 = AddUnit(s, "a3", TemplateIds.Zombie, 31, 6);
            PodOf(s, "p1", true, biter, far1, far2);
            var turn = NewSim(s);
            turn.RunAlienTurn();
            var acts = turn.Events.Where((e) => e is MovedEvent || e is ShotEvent).Select(Who).ToList();
            // Everything of the one that bites comes before the far ones' walks.
            var firstWalker = acts.FindIndex((id) => id != "a1");
            Assert.True(firstWalker > 0);
            Assert.True(acts.Take(firstWalker).All((id) => id == "a1"));
            Assert.Equal(new[] { "a2", "a3" }, acts.Skip(firstWalker).Distinct().OrderBy((id) => id, StringComparer.Ordinal));
            var bites = Of<ShotEvent>(turn.Events).Where((e) => e.Unit == "a1").ToList();
            Assert.Single(bites);
            Assert.Equal((AbilityIds.Claw, "s1"), (bites[0].Ability, bites[0].Target));
        }

        [Fact(DisplayName = "the dead › a zombie bites only whoever it sees itself, not whoever another of the dead sees (Arturo, 16/10)")]
        public void AZombieBitesOnlyWhoeverItSeesItself()
        {
            // Another house: a wall between the soldier and the zombie, with a way round it within one action.
            var s = StateFrom(new[] { "....................", "....................", "....................", "....................", "###.................", "....................", "...................." });
            s.ActiveTeam = Teams.Alien;
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 2, extra: (u) => Hp(u, 50));
            var watcher = AddUnit(s, "a1", TemplateIds.Zombie, 9, 2);
            // It could reach the soldier round the wall in one action (four steps), but doesn't see it.
            var behind = AddUnit(s, "a2", TemplateIds.Zombie, 2, 5, extra: (u) => u.Heard = P(12, 6));
            PodOf(s, "p1", true, watcher, behind);
            Assert.True(Los.CanSee(s, watcher, s.Units[0]));
            Assert.False(Los.CanSee(s, behind, s.Units[0]));
            var turn = NewSim(s);
            turn.RunAlienTurn();
            Assert.DoesNotContain(Of<ShotEvent>(turn.Events), (e) => e.Unit == "a2");
        }

        [Fact(DisplayName = "the dead › the dead see in a cone ahead of them, and whoever is right beside them (Arturo, 17/10)")]
        public void TheDeadSeeInACone()
        {
            var s = Field(20, 20);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 10, 10);
            var ahead = AddUnit(s, "a1", TemplateIds.Zombie, 4, 10, extra: (u) => u.Facing = 0);
            var behind = AddUnit(s, "a2", TemplateIds.Zombie, 16, 10, extra: (u) => u.Facing = 0);
            var side = AddUnit(s, "a3", TemplateIds.Zombie, 10, 4, extra: (u) => u.Facing = 0);
            var beside = AddUnit(s, "a4", TemplateIds.Zombie, 11, 10, extra: (u) => u.Facing = 0);
            Assert.True(Los.CanSee(s, ahead, soldier));
            Assert.False(Los.CanSee(s, behind, soldier));
            // Straight down from it is 90° off where it faces: outside the 120° cone.
            Assert.False(Los.CanSee(s, side, soldier));
            Assert.True(Los.CanSee(s, beside, soldier));
            // The living enemies too: they have eyes all the same (Arturo, 18/10 #46).
            Assert.False(Los.CanSee(s, AddUnit(s, "h1", TemplateIds.Trooper, 16, 11, extra: (u) => u.Facing = 0), soldier));
            Assert.True(Los.CanSee(s, AddUnit(s, "h2", TemplateIds.Trooper, 16, 12, extra: (u) => u.Facing = Math.PI), soldier));
            // The squad still looks all round.
            Assert.True(Los.CanSee(s, AddUnit(s, "s2", TemplateIds.Rookie, 4, 12, extra: (u) => u.Facing = Math.PI), behind));
        }

        [Fact(DisplayName = "the dead › the dead turn to what they hear, and look the way they walk or strike")]
        public void TheDeadTurnToWhatTheyHear()
        {
            var s = Field(20, 20);
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 10, 10, extra: (u) => u.Facing = 0);
            Events.ApplyEvent(s, new NoiseEvent { Tile = P(10, 2), Radius = 12, Heard = new List<string> { "a1" } });
            Close(-Math.PI / 2, z.Facing);
            Events.ApplyEvent(s, new MovedEvent { Unit = "a1", Path = new List<Pos> { P(9, 10), P(8, 11) }, Ap = 1 });
            Close((3 * Math.PI) / 4, z.Facing);
            AddUnit(s, "s1", TemplateIds.Rookie, 9, 11);
            Events.ApplyEvent(s, new ShotEvent { Unit = "a1", Target = "s1", Ability = AbilityIds.Claw, From = z.Pos, Result = ShotResults.Miss, Chance = 50, Roll = 90, Reaction = false });
            Close(0, z.Facing);
            // Out of a mission's making, every one of the dead looks some way.
            var m = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 3), Slots = new List<int> { 0 }, Rng = new Rng(3) });
            Assert.True(m.Units.Where((u) => Content.Templates[u.Template].Undead).All((u) => u.Facing != null));
        }

        [Fact(DisplayName = "the dead › a pod that faces away from a soldier walking past does not notice him")]
        public void APodThatFacesAwayDoesNotNotice()
        {
            bool Run(double facing)
            {
                var s = Field(20, 20);
                var z = AddUnit(s, "a1", TemplateIds.Zombie, 10, 10, extra: (u) => u.Facing = facing);
                PodOf(s, "p1", false, z);
                var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 2, 12, extra: (u) => u.Owner = 0);
                var sim = new Sim(s, new Rng(1), new[] { 0 });
                sim.MoveAlong(soldier, new List<Pos> { P(3, 12), P(4, 12), P(5, 12) }, 1);
                return s.Pods[0].Active;
            }
            Assert.True(Run(Math.PI));
            Assert.False(Run(0));
        }

        [Fact(DisplayName = "the dead › only those that will bite this turn go first: beside a soldier, or seeing one a step away (Arturo, 17/10)")]
        public void OnlyThoseThatWillBiteGoFirst()
        {
            var s = Field(30, 30);
            AddUnit(s, "s1", TemplateIds.Rookie, 10, 10);
            var beside = AddUnit(s, "a1", TemplateIds.Zombie, 11, 10, extra: (u) => u.Facing = 0);
            var near = AddUnit(s, "a2", TemplateIds.Zombie, 14, 10, extra: (u) => u.Facing = Math.PI);
            var blind = AddUnit(s, "a3", TemplateIds.Zombie, 14, 12, extra: (u) => u.Facing = 0);
            var far = AddUnit(s, "a4", TemplateIds.Zombie, 25, 10, extra: (u) => u.Facing = Math.PI);
            Assert.Equal(new[] { false, false, true, true }, new[] { beside, near, blind, far }.Select((z) => Ai.OnlyWalks(s, z)));
        }

        [Fact(DisplayName = "the dead › seeing nobody, a zombie goes to the last noise it heard; with nothing heard, it wanders")]
        public void SeeingNobodyAZombieGoesToTheLastNoise()
        {
            var s = StateFrom(new[] { "...............", ".#############.", "..............." });
            s.ActiveTeam = Teams.Alien;
            AddUnit(s, "s1", TemplateIds.Rookie, 0, 0);
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 14, 2, extra: (u) => u.Heard = P(2, 2));
            PodOf(s, "p1", true, z);
            NewSim(s).RunAlienTurn();
            // 6,4 tiles an action (80 % of a soldier, 28/10): there in two.
            SamePlace(P(2, 2), z.Pos);
        }

        [Fact(DisplayName = "the dead › the living keep their ways, but seeing nobody they too go where they heard something")]
        public void TheLivingKeepTheirWays()
        {
            GameState Street()
            {
                var st = StateFrom(new[] { ".....#..............", ".....#..............", ".....#.............." });
                st.ActiveTeam = Teams.Alien;
                AddUnit(st, "s1", TemplateIds.Rookie, 0, 2);
                return st;
            }
            // Without a noise, a Saqueador hunts the squad as ever...
            var s = Street();
            var hunter = AddUnit(s, "a1", TemplateIds.Trooper, 10, 2);
            PodOf(s, "p1", true, hunter);
            NewSim(s).RunAlienTurn();
            Assert.True(hunter.Pos.X < 10);
            // ...with one, and nobody in sight, it goes to look.
            var t = Street();
            var raider = AddUnit(t, "a1", TemplateIds.Trooper, 10, 2, extra: (u) => u.Heard = P(19, 0));
            PodOf(t, "p1", true, raider);
            NewSim(t).RunAlienTurn();
            Assert.True(raider.Pos.X > 10);
        }

        [Fact(DisplayName = "the dead › a Corredor covers twice the ground of a zombie: nine tiles off, a step and a bite; the zombie only gets there")]
        public void ACorredorCoversTwiceTheGround()
        {
            List<ShotEvent> Turn(string template)
            {
                var s = StateFrom(new[] { "................................" });
                s.ActiveTeam = Teams.Alien;
                AddUnit(s, "s1", TemplateIds.Rookie, 0, 0, extra: (u) => Hp(u, 50));
                PodOf(s, "p1", true, AddUnit(s, "a1", template, 9, 0));
                var t = NewSim(s);
                t.RunAlienTurn();
                return Of<ShotEvent>(t.Events);
            }
            var runner = Turn(TemplateIds.Xenoid);
            Assert.Single(runner);
            Assert.Equal(("a1", AbilityIds.Claw, "s1"), (runner[0].Unit, runner[0].Ability, runner[0].Target));
            Assert.Empty(Turn(TemplateIds.Zombie));
        }

        [Fact(DisplayName = "the dead › the dead have two actions (playtest 16/10 #21): two bites, or a step and a bite; a bite needs the soldier beside it")]
        public void TheDeadHaveTwoActions()
        {
            var s = Field(12, 7);
            s.ActiveTeam = Teams.Alien;
            AddUnit(s, "s1", TemplateIds.Rookie, 2, 3, extra: (u) => Hp(u, 50));
            var beside = AddUnit(s, "a1", TemplateIds.Zombie, 3, 3);
            var near = AddUnit(s, "a2", TemplateIds.Zombie, 6, 5);
            PodOf(s, "p1", true, beside, near);
            var t = NewSim(s);
            t.RunAlienTurn();
            var bites = Of<ShotEvent>(t.Events);
            Assert.Equal(2, bites.Count((e) => e.Unit == "a1"));
            Assert.Equal(1, bites.Count((e) => e.Unit == "a2"));
            Assert.Equal(3, Content.Templates[TemplateIds.Zombie].Hp);
            Assert.Equal(Content.Templates[TemplateIds.Rookie].Mobility * 0.8, Content.Templates[TemplateIds.Zombie].Mobility);
        }

        /// <summary>The test of the dead that measures time, measured alone (it plays a town's whole enemy turn).</summary>
        [Collection(TimeBudgets.Name)]
        public class Timed
        {
            [Fact(DisplayName = "the dead › a horde of twenty plays its turn quickly")]
            public void AHordeOfTwentyPlaysItsTurnQuickly()
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 11), Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = MissionKinds.Scavenge, Difficulty = 4 });
                var soldiers = s.Units.Where((u) => u.Team == Teams.Xcom).ToList();
                var horde = new List<Unit>();
                for (var y = 0; y < s.Height && horde.Count < 20; y += 2)
                {
                    for (var x = 0; x < s.Width && horde.Count < 20; x += 3)
                    {
                        if (s.Tiles[y * s.Width + x] != TileKinds.Floor || s.Units.Any((u) => u.Pos.X == x && u.Pos.Y == y)) continue;
                        if (soldiers.Any((o) => Js.Hypot(o.Pos.X - x, o.Pos.Y - y) < 10)) continue;
                        var heard = Events.CopyPos(soldiers[0].Pos);
                        horde.Add(AddUnit(s, $"h{horde.Count}", horde.Count % 5 == 0 ? TemplateIds.Xenoid : TemplateIds.Zombie, x, y, extra: (u) => u.Heard = heard));
                    }
                }
                PodOf(s, "ph", true, horde.ToArray());
                s.Concealed = false;
                var t0 = Stopwatch.StartNew();
                for (var i = 0; i < 3 && s.Outcome == null; i++)
                {
                    var turn = NewSim(s, i);
                    turn.RunAlienTurn();
                    foreach (var u in s.Units) if (u.Team == Teams.Xcom) u.Ap = 2;
                }
                Assert.True(t0.ElapsedMilliseconds < 3000, $"{t0.ElapsedMilliseconds} ms");
            }
        }

        // ------------------------------------------------------------ the dead follow whom they saw (Arturo, 22/10)

        [Fact(DisplayName = "the dead follow whom they saw (Arturo, 22/10) › out of its sight, a zombie goes after the soldier it saw, wherever they are, for three of its turns; then it forgets them")]
        public void OutOfItsSightAZombieGoesAfterTheSoldierItSaw()
        {
            // The soldier shut in a room far off: the zombie never sees them, and still makes for them.
            string Room(int y) => y < 8 || y > 12 ? "....." : y == 8 || y == 12 ? "#####" : "#...#";
            var s = StateFrom(Enumerable.Range(0, 21).Select((y) => new string('.', 28) + Room(y) + new string('.', 7)).ToArray());
            s.ActiveTeam = Teams.Alien;
            s.Concealed = false;
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 30, 10, extra: (u) => Hp(u, 50));
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 5, 10, extra: (u) => u.Hunting = new Hunting { Id = "s1", Missed = 0 });
            PodOf(s, "p1", true, z);
            var x = z.Pos.X;
            for (var turn = 1; turn <= Ai.HuntTurns; turn++)
            {
                z.Ap = 2;
                var t = NewSim(s, turn);
                t.RunAlienTurn();
                Assert.False(Los.CanSee(s, z, soldier));
                // Straight at the room: not a wander.
                if (turn < Ai.HuntTurns) Assert.True(z.Pos.X >= x + 5, $"turn {turn}: from {x} to {z.Pos.X}");
                x = z.Pos.X;
                if (turn < Ai.HuntTurns)
                {
                    Assert.NotNull(z.Hunting);
                    Assert.Equal(("s1", turn), (z.Hunting!.Id, z.Hunting.Missed));
                }
                else Assert.Null(z.Hunting);
            }
            Assert.True(x >= 20);
            // Forgotten: nothing more about them.
            z.Ap = 2;
            var after = NewSim(s, 9);
            after.RunAlienTurn();
            Assert.Empty(Of<HuntingEvent>(after.Events));
        }

        [Fact(DisplayName = "the dead follow whom they saw (Arturo, 22/10) › whoever it sees on its turn is whom it follows; seeing them again, it starts counting afresh")]
        public void WhoeverItSeesOnItsTurnIsWhomItFollows()
        {
            var s = Field(30, 30);
            s.ActiveTeam = Teams.Alien;
            s.Concealed = false;
            AddUnit(s, "s1", TemplateIds.Rookie, 20, 10, extra: (u) => Hp(u, 50));
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 10, 10, extra: (u) => u.Hunting = new Hunting { Id = "s1", Missed = 2 });
            z.Facing = Los.FacingTo(z.Pos, new Vec2 { X = 20, Y = 10 }) ?? z.Facing;
            PodOf(s, "p1", true, z);
            var t = NewSim(s);
            t.RunAlienTurn();
            var hunting = Of<HuntingEvent>(t.Events);
            Assert.Single(hunting);
            Assert.Equal(("a1", "s1", 0), (hunting[0].Unit, hunting[0].Target, hunting[0].Missed));
        }

        [Fact(DisplayName = "the dead follow whom they saw (Arturo, 22/10) › the awake dead that see a soldier where they stop go after them; the asleep, the blind and the far, not")]
        public void TheAwakeDeadThatSeeASoldierGoAfterThem()
        {
            var s = StateFrom(new[] { "....................", "....................", "....................", "....................", "....................", "#########...........", "...................." });
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 2);
            var awake = AddUnit(s, "a1", TemplateIds.Zombie, 9, 2);
            awake.Facing = Los.FacingTo(awake.Pos, soldier.Pos) ?? awake.Facing;
            var behind = AddUnit(s, "a2", TemplateIds.Zombie, 5, 6);
            PodOf(s, "p1", true, awake, behind);
            var asleep = AddUnit(s, "a3", TemplateIds.Zombie, 2, 2);
            asleep.Facing = Los.FacingTo(asleep.Pos, soldier.Pos) ?? asleep.Facing;
            PodOf(s, "p2", false, asleep);
            Assert.True(Los.CanSee(s, awake, soldier));
            Assert.False(Los.CanSee(s, behind, soldier));
            var t = NewSim(s);
            t.HuntedBy(soldier);
            var hunting = Of<HuntingEvent>(t.Events);
            Assert.Single(hunting);
            Assert.Equal(("a1", "s1", 0), (hunting[0].Unit, hunting[0].Target, hunting[0].Missed));
        }

        // ------------------------------------------------------------ a group comes all together (Ana, 23/10 #25)

        [Fact(DisplayName = "a group comes all together (Ana, 23/10 #25) › a pod woken by a soldier turns to them and goes after them, all of it, not only whoever saw")]
        public void APodWokenByASoldierGoesAfterThemAllOfIt()
        {
            var s = Field(30, 30);
            s.Concealed = false;
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 15);
            var seer = AddUnit(s, "a1", TemplateIds.Zombie, 12, 15);
            seer.Facing = Los.FacingTo(seer.Pos, soldier.Pos)!.Value;
            var others = new[] { AddUnit(s, "a2", TemplateIds.Zombie, 13, 13), AddUnit(s, "a3", TemplateIds.Zombie, 13, 17) };
            foreach (var o in others) o.Facing = Los.FacingTo(soldier.Pos, o.Pos)!.Value;
            PodOf(s, "p1", false, new[] { seer }.Concat(others).ToArray());
            Assert.DoesNotContain(others, (o) => Los.CanSee(s, o, soldier));
            var t = NewSim(s);
            t.ActivatePods(t.PodsNoticing(soldier.Pos), new Activation { Cause = PodCauses.Sighted, Unit = "s1", By = "a1" });
            Assert.True(s.Pods[0].Active);
            foreach (var z in new[] { seer }.Concat(others))
            {
                Assert.NotNull(z.Hunting);
                Assert.Equal(("s1", 0), (z.Hunting!.Id, z.Hunting.Missed));
                Assert.True(Los.CanSee(s, z, soldier));
            }
            // On their turn, the whole group comes.
            s.ActiveTeam = Teams.Alien;
            foreach (var z in new[] { seer }.Concat(others)) z.Ap = 2;
            var before = others.Select((o) => Grid.Distance(o.Pos, soldier.Pos)).ToList();
            NewSim(s, 2).RunAlienTurn();
            for (var i = 0; i < others.Length; i++) Assert.True(Grid.Distance(others[i].Pos, soldier.Pos) < before[i] - 3);
        }

        [Fact(DisplayName = "a group comes all together (Ana, 23/10 #25) › an unaware pod walks its round even when its leader is boxed in by the rest")]
        public void AnUnawarePodWalksItsRound()
        {
            var s = StateFrom(new[] { "##############################" }.Concat(Enumerable.Range(0, 11).Select((_) => "#.............................")).ToArray());
            AddUnit(s, "s1", TemplateIds.Rookie, 29, 11);
            var group = new[] { AddUnit(s, "a1", TemplateIds.Zombie, 1, 1), AddUnit(s, "a2", TemplateIds.Zombie, 2, 1), AddUnit(s, "a3", TemplateIds.Zombie, 1, 2), AddUnit(s, "a4", TemplateIds.Zombie, 2, 2) };
            PodOf(s, "p1", false, group).Patrol = new List<Pos> { P(14, 8), P(14, 1) };
            s.ActiveTeam = Teams.Alien;
            foreach (var z in group) z.Ap = 2;
            NewSim(s).RunAlienTurn();
            Assert.True(group.All((z) => z.Pos.X + z.Pos.Y > 4));
        }

        [Fact(DisplayName = "a group comes all together (Ana, 23/10 #25) › an unaware pod with nowhere to walk looks about, and wakes when it turns to a soldier in sight")]
        public void AnUnawarePodWithNowhereToWalkLooksAbout()
        {
            var s = Field(20, 20);
            s.Concealed = false;
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 10, 3);
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 10, 10);
            z.Facing = Los.FacingTo(soldier.Pos, z.Pos)!.Value;
            PodOf(s, "p1", false, z);
            Assert.False(Los.CanSee(s, z, soldier));
            var turned = 0;
            for (var turn = 1; turn <= 20 && !s.Pods[0].Active; turn++)
            {
                s.ActiveTeam = Teams.Alien;
                z.Ap = 2;
                var t = NewSim(s, turn);
                t.RunAlienTurn();
                turned += Of<TurnedEvent>(t.Events).Count;
                Assert.True(Grid.SamePos(z.Pos, P(10, 10)));
            }
            Assert.True(turned > 0);
            Assert.True(s.Pods[0].Active);
            Assert.NotNull(z.Hunting);
            Assert.Equal(("s1", 0), (z.Hunting!.Id, z.Hunting.Missed));
        }

        // ------------------------------------------------------------ the lone dead (Ana and Arturo, 22/10 #22)

        [Fact(DisplayName = "the lone dead (Ana and Arturo, 22/10 #22) › over the patrols: in the houses and round the corners, standing still, far from where the squad starts")]
        public void OverThePatrols()
        {
            var inside = 0;
            var outside = 0;
            for (var seed = 1; seed <= 6; seed++)
            {
                foreach (var difficulty in new[] { 1, 2, 3, 4 })
                {
                    var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Slots = new List<int> { 0 }, Rng = new Rng(seed), Kind = MissionKinds.Scavenge, Difficulty = difficulty });
                    var lone = s.Units.Where((u) => u.PodId?.StartsWith("l", StringComparison.Ordinal) == true).ToList();
                    Assert.True(lone.Count > 0);
                    Assert.True(lone.Count <= Setup.Lurkers[difficulty]);
                    var squad = s.Units.Where((u) => u.Team == Teams.Xcom).ToList();
                    foreach (var z in lone)
                    {
                        Assert.Equal(TemplateIds.Zombie, z.Template);
                        var p = s.Pods.Find((x) => x.Id == z.PodId);
                        Assert.True(p != null && !p.Active && p.Patrol.Count == 0);
                        Assert.True(squad.Min((u) => Js.Hypot(u.Pos.X - z.Pos.X, u.Pos.Y - z.Pos.Y)) >= 14);
                        if ((s.Storeys?[z.Pos.Y * s.Width + z.Pos.X] ?? 0) > 0) inside++;
                        else outside++;
                    }
                }
            }
            Assert.True(inside > 0);
            Assert.True(outside > 0);
            // Never in a siege.
            var siege = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("bastion"), Slots = new List<int> { 0 }, Rng = new Rng(1) });
            Assert.DoesNotContain(siege.Units, (u) => u.PodId?.StartsWith("l", StringComparison.Ordinal) == true);
        }

        // ------------------------------------------------------------ surrounded (Ana, 22/10 #38)

        [Fact(DisplayName = "surrounded (Ana, 22/10 #38) › the dead round the squad come for it when it steps aside, even facing where it stood: none wanders off")]
        public void TheDeadRoundTheSquadComeForIt()
        {
            var s = Field(20, 20);
            s.Concealed = false;
            var a = AddUnit(s, "s1", TemplateIds.Rookie, 9, 9, extra: (u) => Hp(u, 50));
            var b = AddUnit(s, "s2", TemplateIds.Rookie, 10, 9, extra: (u) => { Hp(u, 50); u.Owner = 1; });
            var spots = new[] { (6, 6), (13, 6), (6, 12), (13, 12), (9, 5), (10, 13), (5, 9), (14, 9) };
            var zs = spots.Select(((int x, int y) p, int k) => AddUnit(s, $"z{k}", TemplateIds.Zombie, p.x, p.y)).ToList();
            foreach (var z in zs) z.Facing = Los.FacingTo(z.Pos, a.Pos) ?? z.Facing;
            PodOf(s, "p1", true, zs.ToArray());
            // They see the squad where it stands; it steps two tiles aside, behind some of them.
            var seen = NewSim(s);
            seen.HuntedBy(a);
            seen.HuntedBy(b);
            a.Pos = P(9, 11);
            b.Pos = P(11, 11);
            foreach (var z in zs) z.Facing = Los.FacingTo(z.Pos, new Vec2 { X = 9, Y = 3 }) ?? z.Facing;
            double Near(Unit z) => new[] { a, b }.Min((u) => Js.Hypot(u.Pos.X - z.Pos.X, u.Pos.Y - z.Pos.Y));
            var before = zs.Select(Near).ToList();
            s.ActiveTeam = Teams.Alien;
            NewSim(s).RunAlienTurn();
            for (var k = 0; k < zs.Count; k++) Assert.True(Near(zs[k]) <= Math.Max(1.5, before[k] - 1), $"{zs[k].Id}: {Near(zs[k])} from {before[k]}");
        }

        // ------------------------------------------------------------ the Hinchado

        [Fact(DisplayName = "the Hinchado › bursts when it dies: 3 to everyone beside it, scenery untouched")]
        public void BurstsWhenItDies()
        {
            var s = Field();
            var shooter = AddUnit(s, "s1", TemplateIds.Rookie, 2, 10);
            var bloater = AddUnit(s, "a1", TemplateIds.Bloater, 8, 10, extra: (u) => u.Hp = 1);
            var beside = AddUnit(s, "a2", TemplateIds.Zombie, 9, 11);
            var soldier = AddUnit(s, "s2", TemplateIds.Rookie, 7, 10, extra: (u) => Hp(u, 10));
            var apart = AddUnit(s, "a3", TemplateIds.Zombie, 10, 10);
            s.Tiles[9 * 20 + 8] = TileKinds.Low;
            PodOf(s, "p1", true, bloater, beside, apart);
            var run = NewSim(s);
            run.Damage(bloater, 5, 0, shooter);
            Assert.False(bloater.Alive);
            var bursts = Of<ExplosiveEvent>(run.Events);
            Assert.Single(bursts);
            Assert.Equal(("a1", AbilityIds.Burst), (bursts[0].Unit, bursts[0].Ability));
            SamePlace(P(8, 10), bursts[0].Target);
            Assert.Equal(7, soldier.Hp);
            Assert.Equal(Content.Templates[TemplateIds.Zombie].Hp - 3, beside.Hp);
            Assert.Equal(Content.Templates[TemplateIds.Zombie].Hp, apart.Hp);
            Assert.Equal(TileKinds.Low, s.Tiles[9 * 20 + 8]);
            var noise = Of<NoiseEvent>(run.Events);
            Assert.NotEmpty(noise);
            Assert.Equal((8, 10), (noise[0].Tile.X, noise[0].Tile.Y));
            Assert.Equal(6, noise[0].Radius);
        }

        [Fact(DisplayName = "the Hinchado › one burst sets off the Hinchado beside it")]
        public void OneBurstSetsOffTheHinchadoBesideIt()
        {
            var s = Field();
            var a = AddUnit(s, "a1", TemplateIds.Bloater, 8, 10, extra: (u) => u.Hp = 1);
            var b = AddUnit(s, "a2", TemplateIds.Bloater, 9, 10, extra: (u) => u.Hp = 3);
            AddUnit(s, "s1", TemplateIds.Rookie, 0, 0);
            var run = NewSim(s);
            run.Damage(a, 1, 0);
            Assert.False(b.Alive);
            Assert.Equal(new[] { "a1", "a2" }, Of<ExplosiveEvent>(run.Events).Select((e) => e.Unit));
        }

        [Fact(DisplayName = "the Hinchado › walks up to a soldier and bursts beside it")]
        public void WalksUpToASoldierAndBursts()
        {
            var s = Field();
            s.ActiveTeam = Teams.Alien;
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => Hp(u, 10));
            var bloater = AddUnit(s, "a1", TemplateIds.Bloater, 9, 5);
            PodOf(s, "p1", true, bloater);
            var turn = NewSim(s);
            turn.RunAlienTurn();
            Assert.False(bloater.Alive);
            var used = Of<AbilityUsedEvent>(turn.Events);
            Assert.Single(used);
            Assert.Equal(("a1", AbilityIds.Burst), (used[0].Unit, used[0].Ability));
            Assert.Equal(7, soldier.Hp);
        }

        // ------------------------------------------------------------ the Chillón

        [Fact(DisplayName = "the Chillón › screams at a soldier it sees, and the zombies out of sight come to the scream without waking")]
        public void ScreamsAtASoldierItSees()
        {
            var s = StateFrom(new[]
            {
                "..............................",
                "..............................",
                "..............................",
                "#######################.......",
                "..............................",
            });
            s.ActiveTeam = Teams.Alien;
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 2, 0, extra: (u) => u.Will = 0);
            var chillon = AddUnit(s, "a1", TemplateIds.Sectoid, 12, 2);
            PodOf(s, "p1", true, chillon);
            var z = AddUnit(s, "a2", TemplateIds.Zombie, 24, 4);
            PodOf(s, "p2", false, z);
            var turn = NewSim(s);
            turn.RunAlienTurn();
            var psi = Of<PsiEvent>(turn.Events);
            Assert.NotEmpty(psi);
            Assert.Equal(("a1", "s1"), (psi[0].Unit, psi[0].Target));
            Assert.NotEqual(PsiEffects.Controlled, psi[0].Effect);
            var noise = Of<NoiseEvent>(turn.Events);
            Assert.NotEmpty(noise);
            Assert.Equal((12, 2), (noise[0].Tile.X, noise[0].Tile.Y));
            Assert.Equal(16, noise[0].Radius);
            Assert.Equal(new[] { "a2" }, noise[0].Heard);
            // The zombie went to look the same turn.
            Assert.True(Js.Hypot(z.Pos.X - 12, z.Pos.Y - 2) < Js.Hypot(24 - 12, 4 - 2));
            Assert.Equal(Teams.Xcom, soldier.Team);
        }

        [Fact(DisplayName = "the Chillón › does not bite: it keeps its distance from those it sees")]
        public void DoesNotBite()
        {
            var s = Field(30, 10);
            s.ActiveTeam = Teams.Alien;
            AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => u.Will = 0);
            var chillon = AddUnit(s, "a1", TemplateIds.Sectoid, 7, 5, extra: (u) => u.Cooldowns = new Dictionary<string, int> { [AbilityIds.Mindspin] = 2, [AbilityIds.Reanimate] = 2 });
            PodOf(s, "p1", true, chillon);
            var turn = NewSim(s);
            turn.RunAlienTurn();
            Assert.Empty(Of<ShotEvent>(turn.Events));
            Assert.True(chillon.Pos.X - 5 >= 5);
        }
    }
}
