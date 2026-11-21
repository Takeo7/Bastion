using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G23.5 Saqueadores (Arturo, cuadro 71; .dev-snapshots/gameplay/g23/SAQUEADORES.md): a Saqueo with a camp of the living
    /// on the map: its fence, tents and fire (the camping's marks), its stash in three crates searched one by one, its
    /// guards at their posts (by the fire, by the stash, on the round), each a group of its own, the camp woken whole; the
    /// dead kept off it; the living's shots winding the horde. Out of the maps till the cuadro (rows, how many, the reward,
    /// the silent blow against them).
    /// </summary>
    public class RunRaidersTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };

        /// <summary>A run's Saqueadores on `site`'s map of `seed` at `difficulty` (or another stage's, `kind`): the campaign and the mission.</summary>
        private static (CampaignState C, GameState S) Raid(int seed = 3, string site = Places.Country, int difficulty = 2, string kind = NodeKinds.Raiders)
        {
            return LaunchAt(kind, seed, node: (n) =>
            {
                n.Site = site;
                n.Seed = seed;
                n.Modifier = null;
                n.Difficulty = difficulty;
            });
        }

        private static List<Unit> Guards(GameState s) => s.Units.Where((u) => u.Guard != null).ToList();

        private static Pos At(GameState s, int i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };

        private static Pod PodOf(GameState s, Unit u) => s.Pods.Single((p) => p.Id == u.PodId);

        private static InteriorSite Camp(GameState s) => s.Interiors!.Single((x) => x.Kind == InteriorKinds.Camping && x.Rooms.Any((r) => r.Use == CampUses.Raiders));

        private static Dictionary<int, int> Walks(GameState s, Unit u, Pos from)
        {
            var was = u.Pos;
            u.Pos = from;
            var cost = Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost.ToDictionary((kv) => kv.Key, (kv) => kv.Value);
            u.Pos = was;
            return cost;
        }

        // ------------------------------------------------------------------ the stage

        [Fact]
        public void ASaqueoWithItsCampOnlyOutOfDoors()
        {
            Assert.Equal(MissionKinds.Scavenge, Runs.MissionKindOf(NodeKinds.Raiders));
            Assert.Contains(NodeKinds.Raiders, NodeKinds.All);
            Assert.Equal(new[] { Places.Country, Places.Roadside, Places.Camping }, RunMap.RaidersSites);
            // Not on the maps till the cuadro.
            Assert.DoesNotContain(NodeKinds.Raiders, RunMap.Specials);
            Assert.Equal("Saqueadores", Lore.StageName(NodeKinds.Raiders));
        }

        [Fact]
        public void TheCampOnItsThreeSitesWithItsStashAndItsGuards()
        {
            foreach (var site in RunMap.RaidersSites)
            {
                foreach (var seed in Seeds)
                {
                    var (_, s) = Raid(seed, site);
                    var camp = s.Mission.Raiders;
                    Assert.True(camp != null, $"{site} {seed}");
                    var where = Camp(s);
                    // Its stash: three crates, low, pieces searched one by one, each with somewhere to stand to open it from.
                    Assert.Equal(Mapgen.StashCrates, camp!.Crates.Count);
                    var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
                    var walks = Walks(s, soldier, soldier.Pos);
                    foreach (var i in camp.Crates)
                    {
                        var p = At(s, i);
                        Assert.True(TileKinds.Low == Grid.KindAt(s, p), $"{site} {seed}: crate {p.X},{p.Y} {Grid.KindAt(s, p)}");
                        Assert.Equal(SearchPieces.StashCrate, Looting.PieceAt(s, p));
                        Assert.Contains(i, s.Mission.Pieces!);
                        Assert.Equal(SearchPieces.StashCrate, Looting.SearchSpot(s, p));
                        Assert.True(Grid.Dirs8.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = 0 }).Any((q) => Grid.InBounds(s, q.X, q.Y) && walks.ContainsKey(Grid.NodeOf(s, q)) && Looting.ReachesToOpen(s, q, p)), $"{site} {seed}: crate {p.X},{p.Y}");
                    }
                    // Its guards: four at difficulty 2, each a group of its own and unaware; one on the round, the rest posted.
                    var guards = Guards(s);
                    Assert.Equal(Setup.RaidersBand(2).Count, guards.Count);
                    Assert.All(guards, (u) => Assert.False(Content.Templates[u.Template].Undead));
                    Assert.All(guards, (u) => Assert.Single(PodOf(s, u).UnitIds));
                    Assert.All(guards, (u) => Assert.False(PodOf(s, u).Active));
                    var round = guards.Where((u) => u.Guard == GuardPosts.Round).ToList();
                    Assert.Single(round);
                    Assert.True(PodOf(s, round[0]).Patrol.Count >= 2);
                    foreach (var u in guards.Where((g) => g.Guard == GuardPosts.Fire))
                    {
                        // On a chair of the camp, looking as it does.
                        var chair = where.Dressing.Single((d) => d.Kind == CampDetailKinds.CampChair && d.X == u.Pos.X && d.Y == u.Pos.Y);
                        Assert.Equal(Js.Atan2(Grid.Dirs4[chair.Facing].Y, Grid.Dirs4[chair.Facing].X), u.Facing);
                        Assert.True(Grid.SamePos(u.Pos, PodOf(s, u).Patrol.Single()));
                    }
                    Assert.Contains(guards, (u) => u.Guard == GuardPosts.Fire);
                    // The dead off the camp at the start.
                    var mid = new Pos { X = camp.X + camp.W / 2, Y = camp.Y + camp.H / 2, Z = 0 };
                    var dead = s.Units.Where((u) => u.Alive && u.Team == Teams.Alien && Content.Templates[u.Template].Undead).ToList();
                    Assert.All(dead, (u) => Assert.True(Math.Max(Math.Abs(u.Pos.X - mid.X), Math.Abs(u.Pos.Y - mid.Y)) >= 10, $"{site} {seed}: {u.Id} at {u.Pos.X},{u.Pos.Y}"));
                    // Nor any of them moved next to where the squad comes in (R1 of 20/11: a group off the camp went there).
                    var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();
                    Assert.All(dead, (u) => Assert.True(squad.Min((x) => Math.Max(Math.Abs(u.Pos.X - x.Pos.X), Math.Abs(u.Pos.Y - x.Pos.Y))) >= 10, $"{site} {seed}: {u.Id} at {u.Pos.X},{u.Pos.Y} by the squad"));
                    // La Lata on foot from the start.
                    Assert.Contains(s.Mission.EvacZone, (z) => walks.ContainsKey(Grid.NodeOf(s, new Pos { X = (int)z.X, Y = (int)z.Y, Z = 0 })));
                }
            }
        }

        [Fact]
        public void FiveGuardsFromDifficultyThreeTheOneMoreByTheStash()
        {
            var (_, s) = Raid(3, Places.Country, 3);
            var guards = Guards(s);
            Assert.Equal(5, guards.Count);
            Assert.Contains(guards, (u) => u.Guard == GuardPosts.Stash);
            Assert.Single(guards, (u) => u.Template == TemplateIds.Officer);
        }

        [Fact]
        public void TheFenceItsGapsAndTheFireOfTheCamp()
        {
            foreach (var seed in Seeds)
            {
                var (_, s) = Raid(seed, Places.Country);
                var where = Camp(s);
                var room = where.Rooms.Single((r) => r.Use == CampUses.Raiders);
                var fence = where.Dressing.Where((d) => d.Kind == CampDetailKinds.Barricade || d.Kind == CampDetailKinds.BarricadeCorner).ToList();
                // On the edge, low; its gaps the rest of the edge, open; four corners, each with its two outer sides.
                var edge = new List<(int X, int Y)>();
                for (var x = room.X; x < room.X + room.W; x++) { edge.Add((x, room.Y)); edge.Add((x, room.Y + room.H - 1)); }
                for (var y = room.Y + 1; y < room.Y + room.H - 1; y++) { edge.Add((room.X, y)); edge.Add((room.X + room.W - 1, y)); }
                Assert.All(fence, (d) => Assert.Contains((d.X, d.Y), edge));
                Assert.All(fence, (d) => Assert.Equal(TileKinds.Low, Grid.KindAt(s, new Pos { X = d.X, Y = d.Y, Z = 0 })));
                var gaps = edge.Where((e) => !fence.Any((d) => d.X == e.X && d.Y == e.Y)).ToList();
                Assert.InRange(gaps.Count, 4, 6);
                Assert.All(gaps, (g) => Assert.True(Grid.IsWalkable(Grid.KindAt(s, new Pos { X = g.X, Y = g.Y, Z = 0 }))));
                foreach (var d in fence.Where((f) => f.Kind == CampDetailKinds.BarricadeCorner))
                {
                    var outs = new[] { d.Facing, (d.Facing + 1) % 4 }.Select((f) => Grid.Dirs4[f]).ToList();
                    Assert.All(outs, (o) => Assert.False(d.X + o.X >= room.X && d.X + o.X < room.X + room.W && d.Y + o.Y >= room.Y && d.Y + o.Y < room.Y + room.H));
                }
                foreach (var d in fence.Where((f) => f.Kind == CampDetailKinds.Barricade))
                {
                    var o = Grid.Dirs4[d.Facing];
                    Assert.False(d.X + o.X >= room.X && d.X + o.X < room.X + room.W && d.Y + o.Y >= room.Y && d.Y + o.Y < room.Y + room.H);
                }
                // Its fire lights at night, as the camping's.
                var fire = where.Dressing.Single((d) => d.Kind == CampDetailKinds.FirePit && d.X > room.X && d.X < room.X + room.W);
                Assert.Contains(s.Lights ?? new List<Light>(), (l) => l.Tile.X == fire.X && l.Tile.Y == fire.Y);
            }
        }

        [Fact]
        public void NoCampOnAnotherStage()
        {
            foreach (var site in RunMap.RaidersSites)
            {
                var (_, s) = Raid(3, site, kind: NodeKinds.Normal);
                Assert.Null(s.Mission.Raiders);
                Assert.Empty(Guards(s));
                Assert.DoesNotContain(s.Interiors ?? new List<InteriorSite>(), (x) => x.Rooms.Any((r) => r.Use == CampUses.Raiders));
            }
        }

        // ------------------------------------------------------------------ the stash

        [Fact]
        public void ACrateOfTheStashSearchedGivesItsOwnOnce()
        {
            var (_, s) = Raid(3, Places.Country);
            var crate = At(s, s.Mission.Raiders!.Crates[0]);
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null);
            var beside = Grid.Dirs8.Select((d) => new Pos { X = crate.X + d.X, Y = crate.Y + d.Y, Z = 0 })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Grid.UnitAt(s, q) == null && Looting.ReachesToOpen(s, q, crate));
            u.Pos = beside;
            u.Ap = 2;
            var r = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = crate });
            Assert.True(r.Ok, r.Error);
            var e = r.Events!.OfType<SearchedEvent>().Single();
            Assert.NotEmpty((e.Packed ?? new List<Placed>()).Select((x) => x.Gear).Concat(e.Floor ?? new List<Gear>()));
            Assert.False(Looting.IsSearchable(s, crate));
        }

        [Fact]
        public void TheStashsLootALongGunOrSuppliesAndAMedicine()
        {
            var rng = new Rng(5);
            var guns = 0;
            for (var k = 0; k < 200; k++)
            {
                var (loot, vest) = Looting.RollPiece(SearchPieces.StashCrate, rng, true, true, 2);
                Assert.NotNull(loot);
                Assert.Equal(0, vest);
                if (loot!.Weapon != null)
                {
                    guns++;
                    Assert.Contains(loot.Weapon, new[] { FoundWeaponIds.AssaultRifle, FoundWeaponIds.HuntingRifle, FoundWeaponIds.PumpShotgun });
                    Assert.Equal(1, loot.Mags);
                }
                else
                {
                    Assert.True(loot.Supplies >= 10);
                    Assert.Equal(1, loot.Medicines);
                }
            }
            Assert.InRange(guns, 80, 120);
        }

        // ------------------------------------------------------------------ the guards

        [Fact]
        public void OneGuardWokenWakesTheWholeCamp()
        {
            var (_, s) = Raid(3, Places.Country);
            var guards = Guards(s);
            var sim = NewSim(s);
            var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
            sim.ActivatePods(new List<string> { guards[1].PodId! }, new Activation { Cause = PodCauses.Sighted, Unit = soldier.Id, By = guards[1].Id });
            Assert.All(guards, (u) => Assert.True(PodOf(s, u).Active, u.Id));
            // The dead's, as they were.
            Assert.All(s.Pods.Where((p) => p.UnitIds.All((id) => Grid.GetUnit(s, id)?.Guard == null)), (p) => Assert.False(p.Active, p.Id));
        }

        [Fact]
        public void AGuardOffItsPostGoesBackAndLooksAsItsChairDoes()
        {
            var (_, s) = Raid(3, Places.Country);
            var u = Guards(s).First((g) => g.Guard == GuardPosts.Fire);
            var pod = PodOf(s, u);
            var post = pod.Patrol.Single();
            var facing = u.Facing;
            var off = Grid.Dirs8.Select((d) => new Pos { X = post.X + 2 * d.X, Y = post.Y + 2 * d.Y, Z = 0 })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.CanStandOn(Grid.KindAt(s, q)) && Grid.UnitAt(s, q) == null);
            u.Pos = off;
            u.Facing = 0.5;
            s.ActiveTeam = Teams.Alien;
            var sim = NewSim(s);
            Ai.PatrolPod(sim, pod);
            Assert.True(Grid.SamePos(u.Pos, post));
            Assert.Equal(facing, u.Facing);
            // There it stays, never looking about.
            for (var k = 0; k < 6; k++) Ai.PatrolPod(sim, pod);
            Assert.True(Grid.SamePos(u.Pos, post));
            Assert.Equal(facing, u.Facing);
        }

        [Fact]
        public void TheRoundGuardWalksItsRound()
        {
            var (_, s) = Raid(3, Places.Country);
            var u = Guards(s).Single((g) => g.Guard == GuardPosts.Round);
            var pod = PodOf(s, u);
            s.ActiveTeam = Teams.Alien;
            var sim = NewSim(s);
            var seen = new HashSet<(int, int)>();
            for (var k = 0; k < 12 && !pod.Active; k++)
            {
                Ai.PatrolPod(sim, pod);
                seen.Add((u.Pos.X, u.Pos.Y));
            }
            Assert.True(seen.Count >= 4);
        }

        [Fact]
        public void AGuardOnWatchSeesWhoStandsInItsViewAtItsTurnAndTheCampWakes()
        {
            // Arte's lab (Producto, 20/11): a soldier in front of a guard by the fire, the camp asleep; at its turn, it sees.
            var (_, s) = Raid(3, Places.Country);
            var u = Guards(s).First((g) => g.Guard == GuardPosts.Fire);
            var d = Grid.Dirs8.OrderBy((v) => Math.Abs(Js.Atan2(v.Y, v.X) - u.Facing!.Value)).First();
            var soldier = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null);
            var at = Enumerable.Range(2, 2).Select((k) => new Pos { X = u.Pos.X + k * d.X, Y = u.Pos.Y + k * d.Y, Z = 0 })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.CanStandOn(Grid.KindAt(s, q)) && Grid.UnitAt(s, q) == null && Los.CanSee(s, u, new Unit { Pos = q, Team = Teams.Xcom, Template = TemplateIds.Rookie }));
            soldier.Pos = at;
            s.ActiveTeam = Teams.Alien;
            var sim = NewSim(s);
            Ai.PatrolPod(sim, PodOf(s, u));
            Assert.All(Guards(s), (g) => Assert.True(PodOf(s, g).Active, g.Id));
            Assert.False(s.Concealed);
        }

        [Fact]
        public void SittingByTheFireAGuardNoticesTheHiddenSquadOnlyClose()
        {
            // Producto, 20/11, after Red's R15: its eyes on the fire (Rules.FireWatch); the round and the stash's guard keep watch.
            var (_, s) = Raid(3, Places.Country);
            var u = Guards(s).First((g) => g.Guard == GuardPosts.Fire);
            Assert.True(Rules.Sitting(s, u));
            Assert.Contains(Guards(s), (g) => g.Guard == GuardPosts.Stash);
            Assert.Single(Guards(s), (g) => g.Guard == GuardPosts.Round);
            // Looking at the fire.
            var fire = Camp(s).Dressing.Single((d) => d.Kind == CampDetailKinds.FirePit);
            Assert.Equal(Los.FacingTo(u.Pos, new Pos { X = fire.X, Y = fire.Y, Z = 0 }), u.Facing);
            var d = Grid.Dirs8.OrderBy((v) => Math.Abs(Js.Atan2(v.Y, v.X) - u.Facing!.Value)).First();
            var soldier = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null);
            var sim = NewSim(s);
            bool Noticed(int k)
            {
                var p = new Pos { X = u.Pos.X + k * d.X, Y = u.Pos.Y + k * d.Y, Z = 0 };
                return sim.PodsNoticing(p, soldier).Contains(u.PodId!);
            }
            Assert.True(Noticed(3));
            Assert.False(Noticed(6));
            // The rules' reach and the client's eyes say the same (UI, 21/11: Rules.NoticeRange, Rules.Notices, Los.Watchers).
            Assert.Equal(Rules.FireWatch, Rules.NoticeRange(s, u, new Pos { X = u.Pos.X + 6 * d.X, Y = u.Pos.Y + 6 * d.Y, Z = 0 }), 3);
            foreach (var k in new[] { 3, 6 })
            {
                var p = new Pos { X = u.Pos.X + k * d.X, Y = u.Pos.Y + k * d.Y, Z = 0 };
                Assert.Equal(Noticed(k), Rules.Notices(s, u, soldier, p));
                var was = soldier.Pos;
                soldier.Pos = p;
                Assert.Equal(Noticed(k), Los.Watchers(s, soldier, p).Contains(u));
                soldier.Pos = was;
            }
            // Stood up (it heard something), it notices as far as ever.
            u.Heard = new Pos { X = u.Pos.X, Y = u.Pos.Y, Z = 0 };
            Assert.False(Rules.Sitting(s, u));
            Assert.True(Noticed(6));
            Assert.True(Rules.Notices(s, u, soldier, new Pos { X = u.Pos.X + 6 * d.X, Y = u.Pos.Y + 6 * d.Y, Z = 0 }));
        }

        [Fact]
        public void TheStashIsThePrizeNoRewardOnTop()
        {
            // Cuadro 99: as La llave's armoury, the stash is what is won; no 40–80 § (RunFights.ApplyFight).
            var (c, s) = Raid(3, Places.Country);
            var rules = new Bastion.Net.EngineCampaignRules();
            foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive).ToList()) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            var sim = NewSim(s);
            sim.DepartEvac();
            Assert.Equal(Outcomes.Victory, s.Outcome);
            rules.ApplyMissionResult(c, s, new Rng(1));
            Assert.Null(c.LastReport!.Run!.Reward);
            Assert.Equal(3, RunMap.RaidersFrom);
        }

        [Fact]
        public void OnTheMapsOnlyAtItsSitesFromItsRowNeverASpecialMapNorALooseSurvivor()
        {
            // Producto's review of 21/11, 3 and 8: what the maps draw for it once there (it isn't on them yet).
            var n = new RunNode { Kind = NodeKinds.Raiders, Row = 3, Seed = 5, Site = Places.Country };
            for (var seed = 0; seed < 50; seed++)
            {
                n.Seed = seed * RunMap.HospitalOdds;
                Assert.Null(RunMap.SpecialFor(n));
            }
            var loose = Enumerable.Range(1, 400).First((x) => RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Normal, Row = 3, Seed = x }));
            Assert.False(RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Raiders, Row = 3, Seed = loose }));
            foreach (var seed in Enumerable.Range(1, 200)) Assert.Contains(RunMap.SiteOf(NodeKinds.Raiders, 3, new Rng(seed)), RunMap.RaidersSites);
        }

        [Fact]
        public void AlwaysThreeCratesOrNoCampAndWithoutOneASaqueo()
        {
            // Producto's review of 21/11, 4 and 3: never a stash of fewer; with no room, a Saqueo of the site, nothing of the camp.
            foreach (var site in RunMap.RaidersSites)
                for (var seed = 1; seed <= 30; seed++)
                {
                    var (_, s) = Raid(seed, site);
                    if (s.Mission.Raiders == null)
                    {
                        Assert.Empty(Guards(s));
                        Assert.DoesNotContain(s.Interiors ?? new List<InteriorSite>(), (x) => x.Rooms.Any((r) => r.Use == CampUses.Raiders));
                        continue;
                    }
                    Assert.True(s.Mission.Raiders.Crates.Count == Mapgen.StashCrates, $"{site} {seed}: {s.Mission.Raiders.Crates.Count}");
                }
            // Out of its sites, a Saqueo.
            var (_, town) = Raid(3, Places.Town);
            Assert.Null(town.Mission.Raiders);
            Assert.Empty(Guards(town));
        }

        [Fact]
        public void WakingTheCampWakesNoGroupOfTheDeadGuards()
        {
            // Producto's review of 21/11, 6: a guard taken down, its group is not woken with the rest.
            var (_, s) = Raid(3, Places.Country);
            var guards = Guards(s);
            var gone = guards.First((g) => g.Guard == GuardPosts.Stash);
            NewSim(s).Damage(gone, 99, 0);
            var sim = NewSim(s);
            var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
            var fire = guards.First((g) => g.Guard == GuardPosts.Fire);
            sim.ActivatePods(new List<string> { fire.PodId! }, new Activation { Cause = PodCauses.Sighted, Unit = soldier.Id, By = fire.Id });
            Assert.DoesNotContain(sim.Events, (e) => e is PodActivatedEvent p && p.Pod == gone.PodId);
            Assert.All(guards.Where((g) => g.Alive), (g) => Assert.True(PodOf(s, g).Active));
        }

        // ------------------------------------------------------------------ the horde

        [Fact]
        public void TheLivingsShotsWindTheHordeOnlyAtACamp()
        {
            var (_, s) = Raid(3, Places.Country);
            var guard = Guards(s)[0];
            var before = s.Mission.Noise ?? 0;
            Events.ApplyEvent(s, new NoiseEvent { Tile = guard.Pos, Radius = 14, By = guard.Id, Heard = new List<string>() });
            Assert.Equal(before + Content.HordePoints(14), s.Mission.Noise ?? 0);
            // Not without the camp, nor the dead's.
            var zombie = s.Units.First((u) => u.Alive && Content.Templates[u.Template].Undead);
            Events.ApplyEvent(s, new NoiseEvent { Tile = zombie.Pos, Radius = 14, By = zombie.Id, Heard = new List<string>() });
            Assert.Equal(before + Content.HordePoints(14), s.Mission.Noise ?? 0);
            s.Mission.Raiders = null;
            Events.ApplyEvent(s, new NoiseEvent { Tile = guard.Pos, Radius = 14, By = guard.Id, Heard = new List<string>() });
            Assert.Equal(before + Content.HordePoints(14), s.Mission.Noise ?? 0);
        }

        // ------------------------------------------------------------------ saved

        [Fact]
        public void TheCampAndItsGuardsKeptInTheState()
        {
            var (_, s) = Raid(3, Places.Country);
            var back = Json.Read<GameState>(Json.Write(s));
            Assert.Equal(s.Mission.Raiders!.Crates, back.Mission.Raiders!.Crates);
            Assert.Equal(Guards(s).Select((u) => u.Guard), Guards(back).Select((u) => u.Guard));
            var copy = Events.CloneState(s);
            Assert.Equal(s.Mission.Raiders.Crates, copy.Mission.Raiders!.Crates);
            Assert.Equal(Guards(s).Select((u) => u.Guard), Guards(copy).Select((u) => u.Guard));
            // A state of before: none of it.
            var (_, plain) = Raid(3, Places.Country, kind: NodeKinds.Normal);
            Assert.Null(Json.Read<GameState>(Json.Write(plain)).Mission.Raiders);
        }
    }
}
