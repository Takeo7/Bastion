using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The living and the dead, and what the living know (Arturo, 25/11, cuadro 157; gameplay A1): they don't go beside the
    /// dead (P1), they hear those close behind them (P2), with one beside them they step away and then shoot (P3), and of the
    /// squad they know what each of them sees, following who they saw last, as the dead do (P4).
    /// </summary>
    public class LivingAiTests
    {
        private const double West = Math.PI;

        private static bool Beside(Pos a, Pos b) => a.Z == b.Z && Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= 1;

        private static List<string> Acts(Sim sim, string id) => sim.Events.Select((e) => e switch
        {
            MovedEvent m when m.Unit == id => "moved",
            ShotEvent sh when sh.Unit == id => $"shot:{sh.Target}",
            _ => "",
        }).Where((t) => t != "").ToList();

        [Fact(DisplayName = "los vivos › waking, it takes cover from what it sees, never beside one of the dead it knows (P1)")]
        public void ScamperNotBesideTheDead()
        {
            // A high block west of 8,1 (cover from the soldier) with one of the dead at its foot; another west of 13,3.
            var s = StateFrom(new[] { "..............", ".......H......", "..............", "............H.", ".............." });
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
            var raider = AddUnit(s, "a1", TemplateIds.Trooper, 10, 2, extra: (u) => u.Facing = West);
            var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 9, 0, extra: (u) => u.Facing = 0);
            ZombiesTests.PodOf(s, "pz", false, zombie);
            var to = Ai.ChooseScamperTile(s, raider);
            Assert.NotNull(to);
            Assert.False(Beside(to!, zombie.Pos), $"{to!.X},{to.Y}");
            Assert.Equal(2, Cover.CoverAgainst(s, to, s.Units[0].Pos));
        }

        [Fact(DisplayName = "los vivos › seeing none of the squad, it keeps no tile beside the dead: they bite, they don't shoot (P1)")]
        public void TheDeadAreNoShooters()
        {
            // Nobody of the squad in sight; one of the dead heard behind a wall: it fights it from where it can, never beside.
            var s = StateFrom(new[] { "......#.......", "......#.......", "......#.......", "..............", ".............." });
            AddUnit(s, "s1", TemplateIds.Rookie, 0, 4);
            s.Tiles[Grid.TileIndex(s, 0, 3)] = TileKinds.Wall;
            s.Tiles[Grid.TileIndex(s, 1, 3)] = TileKinds.Wall;
            s.Tiles[Grid.TileIndex(s, 1, 4)] = TileKinds.Wall;
            var raider = AddUnit(s, "a1", TemplateIds.Trooper, 8, 1, extra: (u) => u.Facing = 0);
            var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 5, 1, extra: (u) => u.Facing = West);
            ZombiesTests.PodOf(s, "pr", true, raider);
            ZombiesTests.PodOf(s, "pz", false, zombie);
            s.ActiveTeam = Teams.Alien;
            var sim = NewSim(s);
            sim.RunAlienTurn();
            Assert.False(Beside(raider.Pos, zombie.Pos));
        }

        [Fact(DisplayName = "los vivos › one of the dead close behind it, out of its sight: it hears it and shoots it, still unaware of the squad (P2)")]
        public void ItHearsTheDeadBehind()
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 16), 6).ToArray());
            AddUnit(s, "s1", TemplateIds.Rookie, 0, 0);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 10, 3, extra: (u) => u.Facing = 0);
            var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 8, 3, extra: (u) => u.Facing = West);
            ZombiesTests.PodOf(s, "pr", false, raider);
            ZombiesTests.PodOf(s, "pz", false, zombie);
            Assert.False(Los.CanSee(s, raider, zombie));
            s.ActiveTeam = Teams.Alien;
            var sim = NewSim(s);
            sim.RunAlienTurn();
            Assert.Contains("shot:z1", Acts(sim, "r1"));
        }

        [Fact(DisplayName = "los vivos › one of the dead beside it and both actions: it steps away first, then shoots (P3)")]
        public void AwayFirstThenTheShot()
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 20), 7).ToArray());
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 3);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 12, 3, extra: (u) => u.Facing = West);
            var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 13, 3, extra: (u) => u.Facing = 0);
            ZombiesTests.PodOf(s, "pr", true, raider);
            ZombiesTests.PodOf(s, "pz", false, zombie);
            s.Concealed = false;
            s.ActiveTeam = Teams.Alien;
            var sim = NewSim(s);
            sim.RunAlienTurn();
            var acts = Acts(sim, "r1");
            Assert.Equal("moved", acts.First());
            Assert.Contains(acts, (a) => a.StartsWith("shot:"));
            Assert.False(Beside(raider.Pos, zombie.Pos));
        }

        [Fact(DisplayName = "los vivos › of the squad, what it sees: it follows the one it saw last for a while, then forgets them (P4)")]
        public void ItFollowsWhoItSawLast()
        {
            // The soldier out of its sight behind a wall; it saw them last turn.
            var s = StateFrom(new[] { "...#............", "...#............", "...#............", "...#............" });
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 14, 1, extra: (u) => { u.Facing = West; u.Hunting = new Hunting { Id = "s1", Missed = 0 }; });
            ZombiesTests.PodOf(s, "pr", true, raider);
            Assert.False(Los.CanSee(s, raider, soldier));
            s.ActiveTeam = Teams.Alien;
            NewSim(s).RunAlienTurn();
            Assert.True(raider.Pos.X < 14, $"{raider.Pos.X}");
            Assert.Equal(("s1", 1), (raider.Hunting!.Id, raider.Hunting.Missed));
            // Seeing nobody for Ai.HuntTurns of its turns, it forgets them.
            for (var k = 1; k < Ai.HuntTurns && raider.Hunting != null; k++)
            {
                s.ActiveTeam = Teams.Alien;
                raider.Ap = 2;
                NewSim(s).RunAlienTurn();
            }
            Assert.Null(raider.Hunting);
        }

        [Fact(DisplayName = "los vivos › seeing a soldier at its turn's end, it is the one it follows next (P4)")]
        public void ItRemembersWhoItSees()
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 16), 5).ToArray());
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 12, 2, extra: (u) => u.Facing = West);
            ZombiesTests.PodOf(s, "pr", true, raider);
            s.Concealed = false;
            s.ActiveTeam = Teams.Alien;
            NewSim(s).RunAlienTurn();
            Assert.Equal(("s1", 0), (raider.Hunting!.Id, raider.Hunting.Missed));
        }

        [Fact(DisplayName = "los vivos › woken by a soldier's sight or shot, the one that saw or was struck follows them; the rest of its group, what it sees")]
        public void WokenTheSpotterFollows()
        {
            var s = StateFrom(new[] { "...#............", "...#............", "...#............" });
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            var spotter = AddUnit(s, "r1", TemplateIds.Trooper, 12, 1, extra: (u) => u.Facing = West);
            var mate = AddUnit(s, "r2", TemplateIds.Trooper, 13, 2, extra: (u) => u.Facing = 0);
            ZombiesTests.PodOf(s, "pr", false, spotter, mate);
            var sim = NewSim(s);
            sim.ActivatePods(new List<string> { "pr" }, new Activation { Cause = PodCauses.Sighted, Unit = "s1", By = "r1" });
            Assert.Equal(("s1", 0), (spotter.Hunting!.Id, spotter.Hunting.Missed));
            Assert.Null(mate.Hunting);
        }

        [Fact(DisplayName = "los vivos › a group awake from the start (Despiertos) knows where the squad comes in, as a noise heard there")]
        public void AwakeTheyKnowWhereTheSquadComesIn()
        {
            var (_, s) = RunsHelpers.LaunchAt(NodeKinds.Rescue, 2, null, (n) => { n.Guarded = true; n.Modifier = Modifiers.Awake; n.Site = Places.Town; });
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).Select((u) => u.Pos).ToList();
            var living = s.Units.Where((u) => u.Team == Teams.Alien && !Content.Templates[u.Template].Undead && !u.Captive && !u.Objective).ToList();
            Assert.NotEmpty(living);
            Assert.All(living, (u) => Assert.Contains(squad, (p) => u.Heard != null && Grid.SamePos(p, u.Heard)));
            // Without it, nothing heard.
            var (_, t) = RunsHelpers.LaunchAt(NodeKinds.Rescue, 2, null, (n) => { n.Guarded = true; n.Modifier = null; n.Site = Places.Town; });
            Assert.All(t.Units.Where((u) => u.Team == Teams.Alien && !Content.Templates[u.Template].Undead), (u) => Assert.Null(u.Heard));
        }

        [Fact(DisplayName = "los vivos › one going a second time in an enemy turn (jammed) doesn't count another turn without the squad in sight")]
        public void OnceAnEnemyTurn()
        {
            var s = StateFrom(new[] { "...#............", "...#............", "...#............" });
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 14, 1, extra: (u) => { u.Facing = West; u.Hunting = new Hunting { Id = "s1", Missed = 0 }; });
            ZombiesTests.PodOf(s, "pr", true, raider);
            var sim = NewSim(s);
            Ai.TakeAlienTurn(sim, raider);
            raider.Ap = 2;
            Ai.TakeAlienTurn(sim, raider);
            Assert.Equal(("s1", 1), (raider.Hunting!.Id, raider.Hunting.Missed));
        }

        [Fact(DisplayName = "los vivos › on watch with no actions left, it fires at one of the dead coming at it")]
        public void OnWatchAtTheDead()
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 20), 5).ToArray());
            AddUnit(s, "s1", TemplateIds.Rookie, 0, 0);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 5, 2, extra: (u) => { u.Facing = 0; u.Overwatch = true; u.Ap = 0; });
            var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 14, 2, extra: (u) => u.Facing = West);
            ZombiesTests.PodOf(s, "pr", true, raider);
            ZombiesTests.PodOf(s, "pz", true, zombie);
            s.Concealed = false;
            var sim = NewSim(s);
            sim.MoveAlong(zombie, Enumerable.Range(0, 5).Select((k) => P(13 - k, 2)).ToList(), 1);
            Assert.Contains(sim.Events.OfType<ShotEvent>(), (e) => e.Unit == "r1" && e.Target == "z1" && e.Reaction);
        }

        [Fact(DisplayName = "los vivos › on watch, the squad hidden: no shot at a soldier it doesn't notice; noticed, the squad is seen, and then the shot")]
        public void OnWatchTheSquadHidden()
        {
            GameState Field(out Unit raider, out Unit soldier)
            {
                var s = StateFrom(Enumerable.Repeat(new string('.', 24), 5).ToArray());
                soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
                raider = AddUnit(s, "r1", TemplateIds.Trooper, 20, 2, extra: (u) => { u.Facing = West; u.Overwatch = true; u.Ap = 0; });
                ZombiesTests.PodOf(s, "pr", true, raider);
                s.Concealed = true;
                return s;
            }
            // Far off (within its sight, beyond its detection): unseen, nothing.
            var s = Field(out _, out var far);
            var sim = NewSim(s);
            sim.MoveAlong(far, new List<Pos> { P(2, 2), P(3, 2) }, 1);
            Assert.DoesNotContain(sim.Events.OfType<ShotEvent>(), (e) => e.Unit == "r1");
            Assert.True(s.Concealed);
            // Close, before it: noticed, the squad seen, and the shot.
            var t = Field(out _, out var near);
            near.Pos = P(12, 2);
            var close = NewSim(t);
            close.MoveAlong(near, new List<Pos> { P(13, 2), P(14, 2) }, 1);
            Assert.False(t.Concealed);
            Assert.Contains(close.Events.OfType<ShotEvent>(), (e) => e.Unit == "r1" && e.Reaction);
        }

        [Fact(DisplayName = "los vivos › in the open with a wall to hide behind, a good shot still comes first")]
        public void AGoodShotBeforeHiding()
        {
            // The soldier bare in the open, flanked; a wall north of the living to hide behind.
            var s = StateFrom(new[] { "........................", "...................####.", "........................", "........................" });
            AddUnit(s, "s1", TemplateIds.Rookie, 12, 3);
            var raider = AddUnit(s, "r1", TemplateIds.Trooper, 20, 3, extra: (u) => u.Facing = West);
            ZombiesTests.PodOf(s, "pr", true, raider);
            s.Concealed = false;
            s.ActiveTeam = Teams.Alien;
            var sim = NewSim(s);
            sim.RunAlienTurn();
            Assert.Contains("shot:s1", Acts(sim, "r1"));
        }

        [Fact(DisplayName = "los vivos › woken seeing nobody, it doesn't run for cover from a squad it doesn't know of (P4)")]
        public void NoScamperSeeingNobody()
        {
            var s = StateFrom(new[] { "...#..........", "...#..........", "...#.......H.." });
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            var raider = AddUnit(s, "a1", TemplateIds.Trooper, 10, 1, extra: (u) => u.Facing = West);
            Assert.Null(Ai.ChooseScamperTile(s, raider));
        }
    }
}
