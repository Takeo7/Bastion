using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The prudent bot's improvement 1, moving without being bitten (Bots, 24/11; .dev-snapshots/bots/MEJORA-1-DISENO.md),
    /// with MissionBot.Style on (Fair and Careful), on small fields drawn by hand: it ends its turn where none of the dead
    /// awake reaches it, doesn't walk past one, steps away on the way out instead of striking, with the blade goes up only
    /// where it holds, knows only what the squad sees (and saw), never stands still for ever, and every order it gives the
    /// engine takes. With the style off (null), the bot is as it was: the other bot tests.
    /// </summary>
    public class NetMissionBotCarefulTests
    {
        private static readonly MissionBot.BotStyle Prudent = new MissionBot.BotStyle { Fair = true, Careful = true };

        /// <summary>`body` with the bot's style `style` on this thread, off again after.</summary>
        private static T With<T>(MissionBot.BotStyle? style, Func<T> body)
        {
            var was = MissionBot.Style;
            MissionBot.Style = style;
            try { return body(); }
            finally { MissionBot.Style = was; }
        }

        /// <summary>An open field of `w` by `h`.</summary>
        private static GameState Field(int w, int h) => StateFrom(Enumerable.Repeat(new string('.', w), h).ToArray());

        /// <summary>A run's Saqueo of row 1 on its way out (RunLeaveTurn come), La Lata on `bus`.</summary>
        private static void Leaving(GameState s, params Pos[] bus)
        {
            s.Run = new RunFight { Node = "n1-0", NodeKind = NodeKinds.Normal, Row = 1 };
            s.Mission.Kind = MissionKinds.Scavenge;
            s.Mission.EvacZone = bus.Select((p) => new Vec2 { X = p.X, Y = p.Y }).ToList();
            s.Turn = MissionBot.RunLeaveTurn;
        }

        /// <summary>A soldier with the blade only (no gun, as a run starts), at `hp`.</summary>
        private static Unit Blade(GameState s, int x, int y, int hp = 5, string id = "s1") => AddUnit(s, id, TemplateIds.Assault, x, y, 0, (u) =>
        {
            u.Hp = hp;
            u.MaxHp = hp;
            u.Arms = new Arms { Primary = null, Pistol = false };
            u.Ammo = 0;
            u.Mags = 0;
        });

        /// <summary>One of the dead in a group of its own, awake or asleep.</summary>
        private static Unit Dead(GameState s, string id, int x, int y, bool awake = true)
        {
            var z = AddUnit(s, id, TemplateIds.Zombie, x, y);
            Pod(s, "p" + id, new[] { z }, awake);
            return z;
        }

        private static readonly int[] Alone = { 0 };

        /// <summary>The squad's turn as the bot plays it alone (slot 0), up to and with its end (the dead's turn): its orders, and all the events.</summary>
        private static (List<Command> Orders, List<GameEvent> Events) SquadTurn(GameState s, Rng rng, MissionBot.BotStyle? style)
        {
            var orders = new List<Command>();
            var events = new List<GameEvent>();
            for (var i = 0; i < 60; i++)
            {
                var cmd = With(style, () => MissionBot.Next(s, 0, Alone));
                if (cmd == null) break;
                var r = Rules.ExecuteCommand(s, cmd, 0, Alone, rng);
                Assert.True(r.Ok, $"{cmd.GetType().Name}: {r.Error}");
                orders.Add(cmd);
                events.AddRange(r.Events ?? new List<GameEvent>());
                if (cmd is EndTurnCommand || s.ActiveTeam != Teams.Xcom || s.Outcome != null) break;
            }
            return (orders, events);
        }

        private static int Bitten(IEnumerable<GameEvent> events, Unit u) => Of<DamagedEvent>(events).Where((e) => e.Unit == u.Id).Sum((e) => e.Amount);

        /// <summary>Where the dead awake reach to bite next turn: within its walk of a turn (from where it stands, or `from`) and a blade's reach.</summary>
        private static bool InBiteReach(GameState s, Unit dead, Pos p, Pos? from = null)
        {
            var at = from ?? dead.Pos;
            var reach = Paths.ComputeReach(s, dead, Paths.FullMoves(s, dead), at, (o) => o.Team == dead.Team);
            return reach.Cost.Keys.Append(Grid.NodeOf(s, at)).Any((n) => Aim.InMeleeReach(s, Grid.PosOf(s, n), p));
        }

        [Fact(DisplayName = "careful › on the way out it ends its turn where none of the dead awake can bite it")]
        public void ItEndsItsTurnOutOfReach()
        {
            // La Lata east, one of the dead awake between, nine tiles off: any stretch toward it ends within the dead's walk
            // and bite. The careful bot ends its turn out of it (a step, or none), and the dead's turn takes nothing from it.
            // The bot as it was ends it within the dead's reach on the same field.
            GameState Made(out Unit soldier, out Unit zombie)
            {
                var f = Field(24, 5);
                Leaving(f, P(22, 2));
                soldier = Blade(f, 1, 2);
                zombie = Dead(f, "a1", 10, 2);
                return f;
            }
            var s = Made(out var careful, out var zombie);
            Assert.True(Aim.TeamSees(s, Teams.Xcom, zombie), "the squad sees it");
            var (endedAt, bites) = FirstTurn(s, careful, Prudent);
            Assert.False(InBiteReach(s, zombie, endedAt, P(10, 2)), $"the careful bot ended at {endedAt.X},{endedAt.Y}");
            Assert.Equal(0, bites);
            var t = Made(out var before, out var dead);
            var (beforeAt, _) = FirstTurn(t, before, null);
            Assert.True(InBiteReach(t, dead, beforeAt, P(10, 2)), $"the bot of before ended at {beforeAt.X},{beforeAt.Y}");
        }

        /// <summary>
        /// The squad's first turn as the bot plays it alone, to the order that ends it (its own end, or one that ends the turn,
        /// as the watch does) and the dead's turn: where `u` stood as the dead's turn began, before any of the dead moved, and
        /// the damage it took in it.
        /// </summary>
        private static (Pos At, int Bites) FirstTurn(GameState s, Unit u, MissionBot.BotStyle? style)
        {
            var rng = new Rng(5);
            var turn = s.Turn;
            for (var i = 0; i < 40; i++)
            {
                var cmd = With(style, () => MissionBot.Next(s, 0, Alone))!;
                var from = Events.CopyPos(u.Pos);
                var r = Rules.ExecuteCommand(s, cmd, 0, Alone, rng);
                Assert.True(r.Ok, r.Error);
                if (s.Turn == turn && s.Outcome == null) continue;
                var at = cmd is MoveCommand m ? m.To : from;
                return (at, Bitten(r.Events, u));
            }
            throw new Exception("the turn never ended");
        }

        [Fact(DisplayName = "careful › it doesn't walk past one of the dead that sees it: it goes round")]
        public void ItGoesRoundOneOfTheDead()
        {
            // One of the dead asleep (no bite zone of its own) beside the straight way east, looking all round: walking past it,
            // it bites (Rules.PassBite). The careful bot's walk takes no bite mark (Rules.ReactionsAlong) and no bite.
            var s = Field(24, 7);
            Leaving(s, P(22, 3));
            var soldier = Blade(s, 1, 3);
            var zombie = Dead(s, "a1", 5, 2, awake: false);
            var cmd = With(Prudent, () => MissionBot.Next(s, 0, Alone));
            var move = Assert.IsType<MoveCommand>(cmd);
            var path = Paths.BuildPath(s, soldier, Paths.ComputeReach(s, soldier, Paths.MoveBudget(s, soldier)), move.To);
            Assert.NotNull(path);
            Assert.DoesNotContain(Rules.ReactionsAlong(s, soldier, path), (r) => r.Kind == PathReactions.Bite);
            var r = Exec(s, move, new Rng(3), 0, Alone);
            Assert.True(r.Ok, r.Error);
            Assert.DoesNotContain(Of<ShotEvent>(r.Events), (e) => e.Unit == zombie.Id && e.Target == soldier.Id);
            Assert.True(move.To.X > 1, "it went on toward La Lata");
            // The bot as it was walks straight past it: a bite mark on its way.
            var t = Field(24, 7);
            Leaving(t, P(22, 3));
            var other = Blade(t, 1, 3);
            Dead(t, "a1", 5, 2, awake: false);
            var straight = Assert.IsType<MoveCommand>(With(null, () => MissionBot.Next(t, 0, Alone)));
            var way = Paths.BuildPath(t, other, Paths.ComputeReach(t, other, Paths.MoveBudget(t, other)), straight.To);
            Assert.Contains(Rules.ReactionsAlong(t, other, way), (r) => r.Kind == PathReactions.Bite);
        }

        [Fact(DisplayName = "careful › on the way out it steps away from two of the dead beside it instead of striking")]
        public void ItStepsAwayInsteadOfStriking()
        {
            // Two of the dead awake beside it, to the west; La Lata east, a free field between. The bot as it was strikes the
            // one beside it (a run's way out: what stands beside it); the careful one walks east, out of both bites.
            var s = Field(24, 5);
            Leaving(s, P(22, 2));
            var soldier = Blade(s, 6, 2);
            Dead(s, "a1", 5, 2);
            Dead(s, "a2", 5, 1);
            var cmd = With(Prudent, () => MissionBot.Next(s, 0, Alone));
            var move = Assert.IsType<MoveCommand>(cmd);
            Assert.True(move.To.X > soldier.Pos.X);
            var before = With(null, () => MissionBot.Next(s, 0, Alone));
            Assert.Equal(AbilityIds.Slash, Assert.IsType<AbilityCommand>(before).Ability);
        }

        [Fact(DisplayName = "careful › on the way out it strikes one alone it kills all but surely, with nobody else near")]
        public void ItStrikesWhatItKills()
        {
            // One of the dead beside it at 1 health, nobody else, and a rifle at point blank: the shot kills it all but surely
            // (KillOdds), and then nothing reaches it. Shot, not walked away from. (The knife, at 65 % here, isn't sure enough:
            // the bot walks away.)
            var s = Field(24, 5);
            Leaving(s, P(22, 2));
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 6, 2);
            var zombie = Dead(s, "a1", 5, 2);
            zombie.Hp = 1;
            var cmd = Assert.IsType<AbilityCommand>(With(Prudent, () => MissionBot.Next(s, 0, Alone)));
            Assert.Equal(zombie.Id, cmd.Target);
            var odds = Aim.PreviewShot(s, soldier, zombie, new ShotOptions { Ability = cmd.Ability })!.Hit;
            Assert.True(odds >= 80, $"{cmd.Ability} at {odds} %");
            var t = Field(24, 5);
            Leaving(t, P(22, 2));
            Blade(t, 6, 2);
            Dead(t, "a1", 5, 2).Hp = 1;
            Assert.IsType<MoveCommand>(With(Prudent, () => MissionBot.Next(t, 0, Alone)));
        }

        [Fact(DisplayName = "careful › with the blade it doesn't go up to two of the dead together, but to one alone")]
        public void TheBladeGoesOnlyWhereItHolds()
        {
            // A Limpieza (the dead to kill), 4 of health: up to two of the dead side by side it would take the other's bite after
            // its blow, 4 or more: it doesn't go up. Up to one alone, it does, as before.
            var s = Field(16, 5);
            var soldier = Blade(s, 1, 2, hp: 4);
            Dead(s, "a1", 6, 2);
            Dead(s, "a2", 6, 3);
            var cmd = With(Prudent, () => MissionBot.Next(s, 0, Alone));
            if (cmd is MoveCommand m) Assert.DoesNotContain(s.Units.Where((u) => u.Team == Teams.Alien), (z) => Aim.InMeleeReach(s, m.To, z.Pos));
            var t = Field(16, 5);
            Blade(t, 1, 2, hp: 4);
            var alone = Dead(t, "a1", 6, 2);
            var go = Assert.IsType<MoveCommand>(With(Prudent, () => MissionBot.Next(t, 0, Alone)));
            Assert.True(Aim.InMeleeReach(t, go.To, alone.Pos));
        }

        [Fact(DisplayName = "fair › one of the dead it doesn't see changes nothing; one it saw is still kept clear of")]
        public void OnlyWhatTheSquadKnows()
        {
            // Two fields alike, one with one of the dead awake behind a wall, out of the squad's sight: the same order.
            string[] rows =
            {
                "........................",
                "........................",
                "........................",
                "########################",
                "........................",
            };
            var a = StateFrom(rows);
            Leaving(a, P(22, 2));
            Blade(a, 1, 2);
            var b = StateFrom(rows);
            Leaving(b, P(22, 2));
            Blade(b, 1, 2);
            var hidden = Dead(b, "a1", 12, 4);
            Assert.False(Aim.TeamSees(b, Teams.Xcom, hidden));
            var mine = With(Prudent, () => MissionBot.Next(a, 0, Alone));
            var theirs = With(Prudent, () => MissionBot.Next(b, 0, Alone));
            Assert.Equal(Json.Write(mine), Json.Write(theirs));
            // One of the dead awake seen by a mate south of the wall, by the gap at (14, 3), then the mate gone: out of sight
            // now, it is still kept clear of where it was seen (its walk through the gap reaches the street east). A copy of
            // the same field the bot has seen nothing of (a state anew, as an app's snapshot) walks into its reach.
            string[] gap =
            {
                "........................",
                "........................",
                "........................",
                "##############.#########",
                "........................",
                "........................",
                "........................",
            };
            var c = StateFrom(gap);
            Leaving(c, P(22, 2));
            var soldier = Blade(c, 4, 2);
            var mate = Blade(c, 14, 6, id: "s2");
            var seen = Dead(c, "a1", 14, 4);
            Assert.True(Aim.TeamSees(c, Teams.Xcom, seen));
            With(Prudent, () => MissionBot.Next(c, 0, Alone));
            c.Units.Remove(mate);
            c.Seq++;
            Assert.False(Aim.TeamSees(c, Teams.Xcom, seen));
            var fresh = Json.Read<GameState>(Json.Write(c));
            var kept = Assert.IsType<MoveCommand>(With(Prudent, () => MissionBot.Next(c, 0, Alone)));
            Assert.False(InBiteReach(c, seen, kept.To), $"kept clear: {kept.To.X},{kept.To.Y}");
            var blind = Assert.IsType<MoveCommand>(With(Prudent, () => MissionBot.Next(fresh, 0, Alone)));
            Assert.True(InBiteReach(c, seen, blind.To), $"not seen, not kept clear of: {blind.To.X},{blind.To.Y}");
        }

        [Fact(DisplayName = "careful › with nowhere safe it still goes on: the fight ends")]
        public void ItNeverStandsStillForEver()
        {
            // La Lata east past three of the dead awake across the field: nowhere out of every bite. The fight ends (La Lata
            // or the squad fallen) within 40 turns.
            var s = Field(24, 7);
            Leaving(s, P(22, 3));
            Blade(s, 1, 3);
            Dead(s, "a1", 8, 1);
            Dead(s, "a2", 8, 3);
            Dead(s, "a3", 8, 5);
            var rng = new Rng(11);
            for (var turn = 0; turn < 40 && s.Outcome == null; turn++) SquadTurn(s, rng, Prudent);
            Assert.NotNull(s.Outcome);
        }

        [Fact(DisplayName = "careful › row 1's fights as a game starts them: every order taken, every fight over")]
        public void EveryOrderIsTaken()
        {
            // Two seats, the starting kit, each fight of row 1 of seeds 1–3 (as the bench, BotsBenchF1): no order the engine
            // refuses, none stuck.
            foreach (var seed in new[] { 1, 2, 3 })
            {
                var c = Campaign.CreateRunsCampaign(RunsHelpers.Both, new Rng(seed));
                var run = RunsHelpers.Start(c, seed);
                var node = RunMap.Choices(run).First((n) => !NodeKinds.IsStop(n.Kind));
                RunsHelpers.Agree(c, new ChooseNodeCommand { Node = node.Id });
                var s = new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
                var rng = new Rng(seed * 7919 + 13);
                int orders = 0, refused = 0, idle = 0;
                while (s.Outcome == null && orders < 3000 && idle < 3)
                {
                    var any = false;
                    foreach (var slot in RunsHelpers.Both)
                    {
                        var cmd = With(Prudent, () => MissionBot.Next(s, slot, RunsHelpers.Both));
                        if (cmd == null) continue;
                        orders++;
                        if (Rules.ExecuteCommand(s, cmd, slot, RunsHelpers.Both, rng).Ok) any = true;
                        else refused++;
                    }
                    idle = any ? 0 : idle + 1;
                }
                Assert.Equal(0, refused);
                Assert.NotNull(s.Outcome);
            }
        }
    }
}
