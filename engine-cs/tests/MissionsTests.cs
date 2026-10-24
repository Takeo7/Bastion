using System;
using System.Collections.Generic;
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
    /// packages/engine/test/missions.test.ts, test by test. Not ported: «skirmish soldiers › the campaign is untouched: its
    /// soldiers bring exactly what they learnt», needs campaign.ts (createCampaign, buildSquad); «the dead first › the
    /// living hold the story chapters and quest steps that are theirs, and nothing else», needs quests.ts (QUESTS).
    /// </summary>
    public class MissionsTests
    {
        /// <summary>`run`: executeCommand with ctx(seed), its events; a refusal throws.</summary>
        private static List<GameEvent> Run(GameState s, Command cmd, int seed = 1)
        {
            var r = Exec(s, cmd, 0, seed);
            if (!r.Ok) throw new InvalidOperationException(r.Error);
            return r.Events!;
        }

        private static GameState Field(string kind, string[]? rows = null)
        {
            var s = StateFrom(rows ?? Enumerable.Repeat(new string('.', 20), 20).ToArray());
            s.Mission.Kind = kind;
            s.Mission.TurnsLeft = 5;
            s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 0, Y = 19 }, new Vec2 { X = 1, Y = 19 } };
            return s;
        }

        private static string[] Rows(int n)
        {
            return Enumerable.Repeat(new string('.', n), n).ToArray();
        }

        private static EvacCommand Evac(string unit)
        {
            return new EvacCommand { Unit = unit };
        }

        /// <summary>toEqual on two states: their JSON, field by field; the first difference, named.</summary>
        private static void SameState(GameState expected, GameState actual)
        {
            var diff = Diff(JsonSerializer.SerializeToNode(expected, Options), JsonSerializer.SerializeToNode(actual, Options), "state");
            if (diff != null) throw new Xunit.Sdk.XunitException(diff);
        }

        private static string? Diff(JsonNode? a, JsonNode? b, string path)
        {
            if (a is JsonObject oa && b is JsonObject ob)
            {
                foreach (var kv in oa)
                {
                    if (!ob.ContainsKey(kv.Key)) return $"{path}.{kv.Key}: expected {kv.Value?.ToJsonString()}, missing";
                    var d = Diff(kv.Value, ob[kv.Key], path + "." + kv.Key);
                    if (d != null) return d;
                }
                foreach (var kv in ob) if (!oa.ContainsKey(kv.Key)) return $"{path}.{kv.Key}: not expected, got {kv.Value?.ToJsonString()}";
                return null;
            }
            if (a is JsonArray xa && b is JsonArray xb)
            {
                if (xa.Count != xb.Count) return $"{path}: expected {xa.Count} items, got {xb.Count}";
                for (var i = 0; i < xa.Count; i++)
                {
                    var d = Diff(xa[i], xb[i], $"{path}[{i}]");
                    if (d != null) return d;
                }
                return null;
            }
            return JsonNode.DeepEquals(a, b) ? null : $"{path}: expected {a?.ToJsonString()}, got {b?.ToJsonString()}";
        }

        /// <summary>toEqual on anything: the same JSON, null and missing alike.</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            if (diff != null) throw new Xunit.Sdk.XunitException(diff);
        }

        // ------------------------------------------------------------ sabotage

        [Fact(DisplayName = "sabotage › destroying the relay secures the objective and stops the timer; the mission is won back on the bus")]
        public void DestroyingTheRelaySecuresTheObjective()
        {
            var s = Field(MissionKinds.Sabotage);
            AddUnit(s, "s1", TemplateIds.Sharpshooter, 0, 0);
            var relay = Setup.MakeUnit("relay", TemplateIds.Relay, "Retransmisor", null, P(6, 0), null);
            relay.Objective = true;
            s.Units.Add(relay);
            s.Mission.ObjectiveUnit = "relay";
            var alien = AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            var sim = NewSim(s, 1);
            sim.Damage(relay, 99, 0);
            sim.CheckOutcome();
            Assert.True(s.Mission.ObjectiveDone);
            Assert.Null(s.Mission.TurnsLeft);
            Assert.Null(s.Outcome);
            sim.Damage(alien, 99, 0);
            sim.CheckOutcome();
            // Clearing the map is not enough (docs/PRUEBA-2026-10-14.md P1): it is won getting out.
            Assert.Null(s.Outcome);
            s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 0, Y = 0 } };
            Run(s, Evac("s1"));
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        [Fact(DisplayName = "sabotage › the relay never counts as a hostile for an elimination check")]
        public void TheRelayNeverCountsAsAHostile()
        {
            var s = Field(MissionKinds.Elimination);
            AddUnit(s, "s1", TemplateIds.Sharpshooter, 0, 0);
            var relay = Setup.MakeUnit("relay", TemplateIds.Relay, "R", null, P(6, 0), null);
            relay.Objective = true;
            s.Units.Add(relay);
            var sim = NewSim(s, 1);
            sim.CheckOutcome();
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        // ------------------------------------------------------------ hack

        private static GameState HackField()
        {
            var rows = Rows(20);
            rows[5] = "....." + "C" + new string('.', 14);
            var s = Field(MissionKinds.Hack, rows);
            s.Mission.Terminal = P(5, 5);
            s.Concealed = true;
            return s;
        }

        [Fact(DisplayName = "hack › only next to the terminal")]
        public void OnlyNextToTheTerminal()
        {
            var s = HackField();
            AddUnit(s, "s1", TemplateIds.Assault, 0, 0);
            AddUnit(s, "s2", TemplateIds.Assault, 5, 6);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 0);
            Assert.False(Exec(s, Ability("s1", AbilityIds.Hack)).Ok);
            Assert.True(Exec(s, Ability("s2", AbilityIds.Hack)).Ok);
        }

        [Fact(DisplayName = "hack › success secures the objective; failure raises the alarm, heard far, and a horde comes in by an edge")]
        public void SuccessSecuresTheObjectiveFailureRaisesTheAlarm()
        {
            GameState? failed = null;
            for (var seed = 1; seed < 40; seed++)
            {
                var s = HackField();
                AddUnit(s, "s1", TemplateIds.Assault, 4, 4);
                AddUnit(s, "s2", TemplateIds.Assault, 0, 19); // still has actions: the XCOM turn goes on
                AddUnit(s, "a1", TemplateIds.Trooper, 19, 0);
                var events = Run(s, Ability("s1", AbilityIds.Hack), seed);
                var hack = Of<HackedEvent>(events).First();
                if (hack.Success)
                {
                    Assert.True(s.Mission.ObjectiveDone);
                }
                else
                {
                    var coming = Of<ReinforcementsIncomingEvent>(events).First();
                    Assert.True(coming.R.Horde);
                    Assert.Null(coming.R.Pos);
                    Assert.True(coming.R.Units.All((t) => t != TemplateIds.Trooper && t != TemplateIds.Officer && t != TemplateIds.Lancer));
                    var noise = Of<NoiseEvent>(events).First();
                    SamePlace(s.Mission.Terminal!, noise.Tile);
                    Assert.Equal(Js.Round(16 * Rules.SquadLouder * 10) / 10, noise.Radius);
                    Assert.False(s.Concealed);
                    failed = s;
                }
            }
            Assert.NotNull(failed);
            // The horde lands at the end of the enemy turn, by an edge, on the prowl.
            {
                var s = failed!;
                var before = s.Units.Count;
                NewSim(s, 2).RunAlienTurn();
                Assert.True(s.Units.Count > before);
                Assert.True(s.Mission.Reinforcements.All((r) => r.Landed));
                var pod = s.Pods.Find((p) => p.Id.StartsWith("pr", StringComparison.Ordinal));
                Assert.NotNull(pod);
                Assert.False(pod!.Active);
                foreach (var u in s.Units.Where((x) => x.PodId != null && x.PodId.StartsWith("pr", StringComparison.Ordinal))) Assert.True(u.Pos.X < 2 || u.Pos.Y < 2 || u.Pos.X >= s.Width - 2 || u.Pos.Y >= s.Height - 2);
            }
        }

        // ------------------------------------------------------------ rescue

        private static GameState RescueField()
        {
            var s = Field(MissionKinds.Rescue);
            var vip = Setup.MakeUnit("vip", TemplateIds.Vip, "VIP", null, P(8, 8), null);
            vip.Captive = true;
            vip.Ap = 0;
            s.Units.Add(vip);
            s.Mission.ObjectiveUnit = "vip";
            AddUnit(s, "s1", TemplateIds.Assault, 0, 8);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 0);
            return s;
        }

        [Fact(DisplayName = "rescue › the VIP joins whoever reaches them, then must be extracted")]
        public void TheVipJoinsWhoeverReachesThem()
        {
            var s = RescueField();
            Run(s, Move("s1", 7, 8));
            var vip = s.Units.Find((u) => u.Id == "vip")!;
            Assert.False(vip.Captive);
            Assert.Equal(0, vip.Owner);
            // Both on the bus; it leaves, and the VIP is out (docs/PRUEBA-2026-10-14.md P1).
            s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 0, Y = 19 }, new Vec2 { X = 1, Y = 19 } };
            vip.Pos = P(0, 19);
            s.Units.Find((u) => u.Id == "s1")!.Pos = P(1, 19);
            Run(s, Evac("vip"));
            Assert.True(s.Mission.ObjectiveDone);
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        [Fact(DisplayName = "rescue › losing the VIP fails the mission")]
        public void LosingTheVipFailsTheMission()
        {
            var s = RescueField();
            var sim = NewSim(s, 1);
            sim.Damage(s.Units.Find((u) => u.Id == "vip")!, 99, 0);
            sim.CheckOutcome();
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            Assert.Equal(EndReasons.ObjectiveLost, s.OutcomeReason);
        }

        [Fact(DisplayName = "rescue › a captive VIP is ignored by the aliens and gives no vision")]
        public void ACaptiveVipIsIgnoredByTheAliens()
        {
            var s = RescueField();
            s.Units.Find((u) => u.Id == "s1")!.Pos = P(0, 0);
            var sim = NewSim(s, 1);
            Assert.False(sim.PodSpotsSquad(s.Units.Find((u) => u.Id == "a1")!, P(19, 19)));
        }

        // ------------------------------------------------------------ patrols and reinforcements

        [Fact(DisplayName = "patrols and reinforcements › a patrolling pod walks its route and stops when it spots the squad")]
        public void APatrollingPodWalksItsRoute()
        {
            var s = Field(MissionKinds.Elimination, Rows(30));
            s.Concealed = false;
            AddUnit(s, "s1", TemplateIds.Specialist, 2, 28);
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 28, 2, 0, (u) => u.PodId = "p1");
            AddUnit(s, "a2", TemplateIds.Trooper, 28, 3, 0, (u) => u.PodId = "p1");
            s.Pods.Add(new Pod { Id = "p1", Active = false, UnitIds = new List<string> { "a1", "a2" }, Patrol = new List<Pos> { P(28, 2), P(4, 26) }, PatrolIndex = 0 });
            var client = Events.CloneState(s);
            var sim = NewSim(s, 1);
            var moved = false;
            for (var turn = 0; turn < 8 && !s.Pods[0].Active; turn++)
            {
                var before = Events.CopyPos(a.Pos);
                sim.RunAlienTurn();
                if (a.Pos.X != before.X || a.Pos.Y != before.Y) moved = true;
            }
            Assert.True(moved);
            Assert.True(s.Pods[0].Active);
            foreach (var e in sim.Events) Events.ApplyEvent(client, e);
            SameState(s, client);
        }

        [Fact(DisplayName = "patrols and reinforcements › a pod on patrol goes in by the door, never through a window")]
        public void APodOnPatrolGoesInByTheDoor()
        {
            var s = Field(MissionKinds.Elimination, new[]
            {
                "........................",
                "........................",
                "....#####...............",
                "....#...#...............",
                "....=...+...............",
                "....#...#...............",
                "....#####...............",
            }.Concat(Enumerable.Repeat(new string('.', 24), 17)).ToArray());
            s.Concealed = true;
            AddUnit(s, "s1", TemplateIds.Specialist, 23, 23);
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 1, 4, 0, (u) => u.PodId = "p1");
            s.Pods.Add(new Pod { Id = "p1", Active = false, UnitIds = new List<string> { "a1" }, Patrol = new List<Pos> { P(6, 4) }, PatrolIndex = 0 });
            var glass = 4 * 24 + 4;
            Assert.False(Paths.ComputeReach(s, a, 100).Cost.Has(glass));
            var sim = NewSim(s, 1);
            for (var turn = 0; turn < 4; turn++) sim.RunAlienTurn();
            Assert.Equal(TileKinds.Window, s.Tiles[glass]);
            Assert.Equal(TileKinds.DoorOpen, s.Tiles[4 * 24 + 8]);
            Assert.DoesNotContain(sim.Events, (e) => e is TileChangedEvent t && t.Kind == TileKinds.WindowBroken);
            // Once the pod is in the fight, the window is a way in again.
            s.Pods[0].Active = true;
            Assert.True(Paths.ComputeReach(s, a, 100).Cost.Has(glass));
        }

        // Playtest 2026-10-13: nobody drops in out of nowhere.
        [Fact(DisplayName = "patrols and reinforcements › every enemy is on the map from the start; only hordes of the dead come later, by an edge; the living only where the mission is theirs")]
        public void EveryEnemyIsOnTheMapFromTheStart()
        {
            var kinds = new[] { MissionKinds.Elimination, MissionKinds.Recovery, MissionKinds.Sabotage, MissionKinds.Hack, MissionKinds.Rescue, MissionKinds.Scavenge };
            // The living on the map (the Sirena is a machine, not one of them).
            int LivingOn(GameState s) => s.Units.Count((u) => u.Team == Teams.Alien && !Content.Templates[u.Template].Undead && !Content.Templates[u.Template].Robotic);
            foreach (var kind in kinds)
            {
                foreach (var difficulty in new[] { 1, 2, 3, 4 })
                {
                    var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 11 + difficulty), Slots = new List<int> { 0 }, Rng = new Rng(difficulty), Kind = kind, Difficulty = difficulty });
                    var label = $"{kind} {difficulty}";
                    // docs/ZOMBIS-PRIMERO.md: a mission of the dead has none of the living; one of the living, at least their garrison.
                    Assert.True(LivingOn(s) == 0, label);
                    if (kind != MissionKinds.Scavenge)
                    {
                        var held = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 11 + difficulty), Slots = new List<int> { 0 }, Rng = new Rng(difficulty), Kind = kind, Difficulty = difficulty, Living = true });
                        Assert.True(LivingOn(held) >= Math.Max(2, Setup.Garrison(held.Mission.Kind, difficulty, true)?.Count ?? 0), label);
                    }
                    foreach (var r in s.Mission.Reinforcements)
                    {
                        Assert.True(r.Horde == true && r.Pos == null && !r.Landed, label);
                        Assert.True(r.Units.All((t) => Content.Templates[t].Undead), label);
                    }
                    Assert.True(s.Mission.Horde?.All((t) => Content.Templates[t].Undead) ?? true, label);
                    // Sabotage and hack keep their pressure as two hordes; the others bring nobody.
                    Assert.True(s.Mission.Reinforcements.Select((r) => r.Turn).SequenceEqual(s.Mission.Kind == MissionKinds.Sabotage || s.Mission.Kind == MissionKinds.Hack ? new[] { 4, 8 } : new int[0]), label);
                    // The strength those drops had is a garrison on the map from the start, of the dead here.
                    var extra = Setup.Garrison(s.Mission.Kind, difficulty);
                    var dead = s.Units.Count((u) => u.Team == Teams.Alien && Content.Templates[u.Template].Undead);
                    Assert.True(dead >= (extra?.Count ?? 0), label);
                }
            }
            Assert.Null(Setup.Garrison(MissionKinds.Scavenge, 4));
            Assert.Null(Setup.Garrison(MissionKinds.Elimination, 2));
            Assert.Equal(new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie }, Setup.Garrison(MissionKinds.Sabotage, 1));
            Assert.Equal(new[] { TemplateIds.Trooper, TemplateIds.Trooper }, Setup.Garrison(MissionKinds.Sabotage, 1, true));
            Assert.Equal(Setup.HordeWave(4), Setup.HordeWave(9));
        }

        [Fact(DisplayName = "patrols and reinforcements › a horde due lands at the end of its enemy turn, by an edge and away from the squad, on the prowl")]
        public void AHordeDueLandsAtTheEndOfItsEnemyTurn()
        {
            var s = Field(MissionKinds.Sabotage, Rows(30));
            s.Concealed = true;
            AddUnit(s, "s1", TemplateIds.Specialist, 15, 15);
            AddUnit(s, "a1", TemplateIds.Trooper, 29, 29);
            s.Mission.TurnsLeft = 20;
            s.Mission.Reinforcements = new List<Reinforcement> { new Reinforcement { Id = "r1", Turn = 2, Pos = null, Units = Setup.HordeWave(3), Landed = false, Horde = true } };
            var sim = NewSim(s, 4);
            sim.RunAlienTurn(); // turn 1: nothing yet, and no flare
            Assert.DoesNotContain(sim.Events, (e) => e.T == "reinforcementsIncoming" || e.T == "spawned");
            Assert.Null(s.Mission.Reinforcements[0].Pos);
            s.Turn = 2;
            sim.RunAlienTurn(); // turn 2: in by an edge
            Assert.True(s.Mission.Reinforcements[0].Landed);
            var horde = s.Units.Where((u) => u.Id.StartsWith("r1", StringComparison.Ordinal)).ToList();
            Assert.Equal(Setup.HordeWave(3), horde.Select((u) => u.Template));
            foreach (var u in horde)
            {
                Assert.True(u.Pos.X < 2 || u.Pos.Y < 2 || u.Pos.X >= 28 || u.Pos.Y >= 28);
                Assert.True(Js.Hypot(u.Pos.X - 15, u.Pos.Y - 15) >= 12);
                SamePlace(P(15, 15), u.Heard);
            }
            // Unaware: they come looking, and the squad stays hidden.
            Assert.False(s.Pods.Find((p) => p.Id == "pr1")!.Active);
            Assert.True(s.Concealed);
        }

        [Fact(DisplayName = "patrols and reinforcements › scheduled reinforcements from older saves are still announced a turn early and land alert")]
        public void ScheduledReinforcementsFromOlderSaves()
        {
            var s = Field(MissionKinds.Recovery, Rows(30));
            s.Concealed = false;
            AddUnit(s, "s1", TemplateIds.Specialist, 15, 15);
            AddUnit(s, "a1", TemplateIds.Trooper, 29, 29);
            s.Mission.TurnsLeft = 20;
            s.Mission.Reinforcements = new List<Reinforcement> { new Reinforcement { Id = "r1", Turn = 2, Pos = null, Units = new List<string> { TemplateIds.Trooper, TemplateIds.Trooper }, Landed = false } };
            var sim = NewSim(s, 4);
            sim.RunAlienTurn(); // turn 1: flare for turn 2
            Assert.NotNull(s.Mission.Reinforcements[0].Pos);
            Assert.False(s.Mission.Reinforcements[0].Landed);
            sim.RunAlienTurn(); // turn 2: drop
            Assert.True(s.Mission.Reinforcements[0].Landed);
            var dropped = s.Units.Where((u) => u.Id.StartsWith("r1", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, dropped.Count);
            foreach (var u in dropped)
            {
                var d = Js.Hypot(u.Pos.X - 15, u.Pos.Y - 15);
                Assert.True(d >= 3);
            }
        }

        // ------------------------------------------------------------ skirmish soldiers (docs/FASE10.md §7)

        [Fact(DisplayName = "skirmish soldiers (docs/FASE10.md §7) › each one a weapon and gear at random; veterans, bars in every line and their points spent; the same seed, the same squad")]
        public void EachOneAWeaponAndGearAtRandom()
        {
            var kinds = new HashSet<string>();
            for (var seed = 1; seed <= 20; seed++)
            {
                List<Unit> Veterans(Rng rng) => Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0, 1 }, Rng = rng, Veterans = true }).Units.Where((u) => u.Team == Teams.Xcom).ToList();
                var squad = Veterans(new Rng(seed));
                Same(squad.Select((u) => new object?[] { u.Perks, u.Arms }), Veterans(new Rng(seed)).Select((u) => new object?[] { u.Perks, u.Arms }));
                foreach (var u in squad)
                {
                    kinds.Add(u.Arms!.Primary!);
                    Assert.Equal(TemplateIds.Rookie, u.Template);
                    Assert.Contains(ItemIds.FragGrenade, u.Gear);
                    Assert.Contains(AbilityIds.Grenade, Abilities.AbilitiesOf(u));
                    // Two to five bars in each of the six lines: Curtido at least.
                    Assert.True(u.Rank >= 2);
                }
            }
            Assert.Equal(new[] { FoundWeaponIds.AssaultRifle, FoundWeaponIds.MachineGun, FoundWeaponIds.PrecisionRifle, FoundWeaponIds.PumpShotgun }, kinds.OrderBy((k) => k, StringComparer.Ordinal));
            // Rookies in a skirmish have no talents yet.
            var rookies = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(1) }).Units.Where((u) => u.Team == Teams.Xcom).ToList();
            Assert.True(rookies.All((u) => u.Perks.Count == 0 && u.Rank == 0));
        }

        // ------------------------------------------------------------ the dead first (docs/ZOMBIS-PRIMERO.md)

        private static int Living(List<List<string>> pods)
        {
            return pods.SelectMany((p) => p).Count((t) => !Content.Templates[t].Undead);
        }

        [Fact(DisplayName = "the dead first (docs/ZOMBIS-PRIMERO.md) › the groups of an ordinary mission are all the dead; a mission of the living has two of their bands among them")]
        public void TheGroupsOfAnOrdinaryMissionAreAllTheDead()
        {
            foreach (var difficulty in new[] { 1, 2, 3, 4 })
            {
                foreach (var kind in new[] { MissionKinds.Elimination, MissionKinds.Recovery, MissionKinds.Sabotage, MissionKinds.Hack, MissionKinds.Rescue, MissionKinds.Scavenge })
                {
                    Assert.True(Living(Setup.MissionPods(difficulty, kind)) == 0, $"{kind} {difficulty}");
                }
                var held = Setup.MissionPods(difficulty, MissionKinds.Rescue, true);
                Assert.Equal(2, held.Count((p) => p.All((t) => !Content.Templates[t].Undead)));
                Assert.Equal(2, held.Count((p) => p.All((t) => Content.Templates[t].Undead)));
            }
        }

        // ------------------------------------------------------------ ranks (docs/PRUEBA-2026-10-14.md P5)

        [Fact(DisplayName = "ranks (docs/PRUEBA-2026-10-14.md P5) › a soldier from Comandante on starts each turn with three actions; anyone else, two")]
        public void ASoldierFromComandanteOnStartsWithThreeActions()
        {
            var s = Field(MissionKinds.Elimination);
            var veteran = AddUnit(s, "s1", TemplateIds.Assault, 2, 2, 0, (u) => u.Rank = Content.ThirdActionRank);
            var captain = AddUnit(s, "s2", TemplateIds.Assault, 4, 2, 0, (u) => u.Rank = Content.ThirdActionRank - 1);
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = 2 });
            Assert.Equal(3, veteran.Ap);
            Assert.Equal(2, captain.Ap);
            Assert.Equal(2, Content.ApPerTurnOf(new Unit { Team = Teams.Alien, Rank = 7 }));
        }
    }
}
