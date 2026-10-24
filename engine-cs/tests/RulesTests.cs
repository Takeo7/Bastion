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
    /// packages/engine/test/rules.test.ts, test by test. «replication» plays its bot on the step-1 golden missions
    /// (createMission's, by TypeScript) instead of making eighteen with createMission: made here as TypeScript makes
    /// them it passes too, but in 28 s instead of 18, and it is more than a one-line swap.
    /// </summary>
    public class RulesTests
    {
        /// <summary>`ctx(slot, connected, seed)`: connected [0] when not given, whatever the slot.</summary>
        private static CommandContext Ctx(int slot = 0, int[]? connected = null, int seed = 1)
        {
            return new CommandContext { Slot = slot, Connected = connected ?? new[] { 0 }, Rng = new Rng(seed) };
        }

        private static List<GameEvent> Run(GameState s, Command cmd, CommandContext? c = null)
        {
            var r = Rules.ExecuteCommand(s, cmd, c ?? Ctx());
            if (!r.Ok) throw new Exception(r.Error);
            return r.Events!;
        }

        private static GameState Field(int w = 20, int h = 20)
        {
            return StateFrom(Enumerable.Range(0, h).Select((_) => new string('.', w)).ToArray());
        }

        private static readonly int[] Both = { 0, 1 };

        private static Unit UnitOf(GameState s, string id)
        {
            return s.Units.First((u) => u.Id == id);
        }

        // ------------------------------------------------------------ actions

        [Fact(DisplayName = "actions › a short move costs 1 AP, a dash costs 2")]
        public void AShortMoveCosts1ApADashCosts2()
        {
            var s = Field();
            var u = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            AddUnit(s, "s2", TemplateIds.Specialist, 19, 19);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 0);
            Run(s, Move("s1", 5, 0));
            Assert.Equal(1, u.Ap);
            var t = Field();
            var v = AddUnit(t, "s1", TemplateIds.Specialist, 0, 0);
            AddUnit(t, "s2", TemplateIds.Specialist, 19, 19);
            AddUnit(t, "a1", TemplateIds.Trooper, 19, 0);
            Run(t, Move("s1", 12, 0));
            Assert.Equal(0, v.Ap);
        }

        [Fact(DisplayName = "actions › rejects orders for the partner soldiers")]
        public void RejectsOrdersForThePartnerSoldiers()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 0, extra: (u) => u.Owner = 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            var r = Rules.ExecuteCommand(s, Move("s1", 1, 0), Ctx(0, Both));
            Assert.False(r.Ok);
        }

        [Fact(DisplayName = "actions › enemy overwatch interrupts a move and splits it into segments")]
        public void EnemyOverwatchInterruptsAMoveAndSplitsItIntoSegments()
        {
            var s = Field();
            var u = AddUnit(s, "s1", TemplateIds.Specialist, 0, 10);
            AddUnit(s, "s2", TemplateIds.Specialist, 0, 19);
            AddUnit(s, "a1", TemplateIds.Trooper, 10, 0, extra: (x) => x.Overwatch = true);
            var events = Run(s, Move("s1", 6, 10));
            var firstMove = events.FindIndex((e) => e.T == "moved");
            var shot = events.FindIndex((e) => e.T == "shot");
            Assert.True(firstMove >= 0);
            Assert.True(shot > firstMove);
            var e = Assert.IsType<ShotEvent>(events[shot]);
            Assert.True(e.Reaction);
            Assert.Equal("a1", e.Unit);
            if (u.Alive) SamePlace(P(6, 10, 0), u.Pos);
        }

        [Fact(DisplayName = "actions › grenades damage everyone in the blast and destroy cover")]
        public void GrenadesDamageEveryoneInTheBlastAndDestroyCover()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 5, 5);
            AddUnit(s, "a2", TemplateIds.Trooper, 19, 19);
            s.Tiles[5 * 20 + 6] = TileKinds.Low;
            s.Tiles[4 * 20 + 5] = TileKinds.High;
            Run(s, Ability("s1", AbilityIds.Grenade, tile: P(5, 5)));
            Assert.True(a.Hp < 3);
            Assert.Equal(TileKinds.Floor, s.Tiles[5 * 20 + 6]);
            Assert.Equal(TileKinds.Low, s.Tiles[4 * 20 + 5]);
        }

        [Fact(DisplayName = "actions › the grenade launcher reaches further than a thrown grenade")]
        public void TheGrenadeLauncherReachesFurtherThanAThrownGrenade()
        {
            var s = Field();
            var g = AddUnit(s, "s1", TemplateIds.Grenadier, 0, 0);
            var sp = AddUnit(s, "s2", TemplateIds.Specialist, 0, 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            Assert.False(Abilities.CanTargetTile(s, sp, AbilityIds.Grenade, P(12, 0, 0)));
            Assert.True(Abilities.CanTargetTile(s, g, AbilityIds.Launch, P(12, 0, 0)));
        }

        // ------------------------------------------------------------ classes

        [Fact(DisplayName = "classes › each class brings its own kit")]
        public void EachClassBringsItsOwnKit()
        {
            List<string> Kit(string t) => Abilities.AbilitiesOf(AddUnit(Field(), "x", t, 0, 0));
            Assert.Subset(Kit(TemplateIds.Assault).ToHashSet(), new HashSet<string> { AbilityIds.Slash, AbilityIds.Shoot });
            Assert.Contains(AbilityIds.Launch, Kit(TemplateIds.Grenadier));
            Assert.Subset(Kit(TemplateIds.Sharpshooter).ToHashSet(), new HashSet<string> { AbilityIds.Pistol, AbilityIds.Shoot });
        }

        [Fact(DisplayName = "classes › the sniper rifle needs both actions, the pistol only one")]
        public void TheSniperRifleNeedsBothActionsThePistolOnlyOne()
        {
            var s = Field();
            var sniper = AddUnit(s, "s1", TemplateIds.Sharpshooter, 0, 0, extra: (u) => u.Ap = 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 0, 8);
            Assert.Matches("2 acciones", Abilities.AbilityBlocker(s, sniper, AbilityIds.Shoot));
            Assert.Null(Abilities.AbilityBlocker(s, sniper, AbilityIds.Pistol));
        }

        [Fact(DisplayName = "classes › squadsight lets the sniper hit what an ally sees, with a distance penalty")]
        public void SquadsightLetsTheSniperHitWhatAnAllySees()
        {
            var s = StateFrom(Enumerable.Range(0, 30).Select((_) => new string('.', 5)).ToArray());
            var sniper = AddUnit(s, "s1", TemplateIds.Sharpshooter, 2, 0);
            AddUnit(s, "s2", TemplateIds.Specialist, 2, 15);
            var far = AddUnit(s, "a1", TemplateIds.Trooper, 2, 25);
            var p = Aim.PreviewShot(s, sniper, far)!;
            Assert.True(p.Squadsight);
            Assert.Equal(-2 * (25 - 18), p.Mods.FirstOrDefault((m) => m.Label == "Visión de escuadra")?.Value);
            Assert.Null(Aim.PreviewShot(s, sniper, far, new ShotOptions { Ability = AbilityIds.Pistol }));
        }

        [Fact(DisplayName = "classes › slash strikes only whoever is alongside (striking is striking, running is running: 16/10), and ignores cover")]
        public void SlashStrikesOnlyWhoeverIsAlongside()
        {
            var s = Field();
            var ranger = AddUnit(s, "s1", TemplateIds.Assault, 0, 5);
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 5, 5, extra: (u) =>
            {
                u.Hp = 20;
                u.MaxHp = 20;
            });
            s.Tiles[5 * 20 + 6] = TileKinds.High;
            Assert.Empty(Abilities.MeleeTargets(s, ranger, AbilityIds.Slash));
            ranger.Pos = P(4, 5, 0);
            var options = Abilities.MeleeTargets(s, ranger, AbilityIds.Slash);
            Assert.Equal(new[] { "a1" }, options.Select((o) => o.Target.Id));
            Assert.Empty(options[0].Path);
            var p = Aim.PreviewShot(s, ranger, a, new ShotOptions { Ability = AbilityIds.Slash, From = options[0].Tile })!;
            Assert.Equal(0, p.Cover);
            Assert.Equal(20, p.Mods.FirstOrDefault((m) => m.Label == "Arma cuerpo a cuerpo")?.Value);
            var events = Run(s, Ability("s1", AbilityIds.Slash, target: "a1"));
            Assert.DoesNotContain(events, (e) => e.T == "moved");
            Assert.Contains(events, (e) => e is ShotEvent shot && shot.Ability == AbilityIds.Slash);
            SamePlace(P(4, 5, 0), ranger.Pos);
        }

        // ------------------------------------------------------------ enemies

        [Fact(DisplayName = "enemies › armour absorbs damage and explosives shred it")]
        public void ArmourAbsorbsDamageAndExplosivesShredIt()
        {
            var s = Field();
            var mec = AddUnit(s, "a1", TemplateIds.Mec, 5, 5);
            var sim = new Sim(s, new Rng(1), new[] { 0 });
            var hp = Content.Templates[TemplateIds.Mec].Hp;
            sim.Damage(mec, 4, 0);
            Assert.Equal(hp - 3, mec.Hp);
            sim.Damage(mec, 3, 1);
            Assert.Equal(0, mec.Armor);
            sim.Damage(mec, 1, 0);
            Assert.Equal(hp - 3 - 2 - 1, mec.Hp);
        }

        [Fact(DisplayName = "enemies › a stun lance hit leaves the soldier with a single action next turn")]
        public void AStunLanceHitLeavesTheSoldierWithASingleActionNextTurn()
        {
            var s = Field();
            var soldier = AddUnit(s, "s1", TemplateIds.Specialist, 5, 5, extra: (u) =>
            {
                u.Hp = 50;
                u.MaxHp = 50;
            });
            var lancer = AddUnit(s, "a1", TemplateIds.Lancer, 6, 5);
            var stunned = false;
            for (var seed = 1; seed < 20 && !stunned; seed++)
            {
                var sim = new Sim(s, new Rng(seed), new[] { 0 });
                sim.MeleeAttack(lancer, Abilities.MeleeTargets(s, lancer, AbilityIds.StunLance)[0], AbilityIds.StunLance);
                stunned = soldier.Stunned;
            }
            Assert.True(stunned);
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = 2 });
            Assert.Equal(1, soldier.Ap);
        }

        [Fact(DisplayName = "enemies › aliens throw grenades at clustered soldiers")]
        public void AliensThrowGrenadesAtClusteredSoldiers()
        {
            var s = Field();
            var spots = new[] { (8, 10), (9, 10), (8, 11) };
            for (var i = 0; i < spots.Length; i++) AddUnit(s, $"s{i}", TemplateIds.Specialist, spots[i].Item1, spots[i].Item2);
            var trooper = AddUnit(s, "a1", TemplateIds.Trooper, 8, 2, extra: (u) => u.PodId = "p1");
            s.Pods.Add(new Pod { Id = "p1", Active = true, UnitIds = new List<string> { "a1" }, Patrol = new List<Pos>(), PatrolIndex = 0 });
            s.ActiveTeam = Teams.Alien;
            var sim = new Sim(s, new Rng(3), new[] { 0 });
            sim.RunAlienTurn();
            Assert.Contains(sim.Events, (e) => e is ExplosiveEvent x && x.Unit == trooper.Id);
        }

        // ------------------------------------------------------------ concealment and pods

        private static GameState AmbushSetup()
        {
            var s = Field(30, 20);
            s.Concealed = true;
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 10);
            AddUnit(s, "s2", TemplateIds.Specialist, 0, 12);
            AddUnit(s, "a1", TemplateIds.Trooper, 20, 10, extra: (u) => u.PodId = "p1");
            AddUnit(s, "a2", TemplateIds.Trooper, 20, 12, extra: (u) => u.PodId = "p1");
            s.Pods.Add(new Pod { Id = "p1", Active = false, UnitIds = new List<string> { "a1", "a2" }, Patrol = new List<Pos>(), PatrolIndex = 0 });
            return s;
        }

        [Fact(DisplayName = "concealment and pods › concealed soldiers are only noticed inside the detection radius")]
        public void ConcealedSoldiersAreOnlyNoticedInsideTheDetectionRadius()
        {
            var s = AmbushSetup();
            Run(s, Move("s1", 6, 10));
            Assert.True(s.Concealed);
            Assert.False(s.Pods[0].Active);
            Run(s, Move("s2", 13, 12));
            Assert.False(s.Concealed);
            Assert.True(s.Pods[0].Active);
        }

        [Fact(DisplayName = "concealment and pods › shooting from concealment breaks it and the pod scampers")]
        public void ShootingFromConcealmentBreaksItAndThePodScampers()
        {
            var s = AmbushSetup();
            Run(s, Move("s1", 6, 10));
            var events = Run(s, Ability("s1", AbilityIds.Shoot, target: "a1"));
            var types = Tags(events);
            Assert.Contains("concealmentBroken", types);
            Assert.Contains("podActivated", types);
            var broken = events.OfType<ConcealmentBrokenEvent>().First();
            Assert.Equal(("attack", "s1", "a1"), (broken.Reason, broken.Unit, broken.By));
            var contact = events.OfType<PodActivatedEvent>().First();
            Assert.Equal(("attacked", "s1", "a1"), (contact.Cause, contact.Unit, contact.By));
        }

        [Fact(DisplayName = "concealment and pods › says who saw the squad and from how far, before the contact")]
        public void SaysWhoSawTheSquadAndFromHowFarBeforeTheContact()
        {
            var s = AmbushSetup();
            var events = Run(s, Move("s2", 13, 12));
            var broken = events.FindIndex((e) => e.T == "concealmentBroken");
            var contact = events.FindIndex((e) => e.T == "podActivated");
            Assert.True(broken >= 0);
            Assert.True(contact > broken);
            var e = (ConcealmentBrokenEvent)events[broken];
            Assert.Equal(("spotted", "s2", (double?)8), (e.Reason, e.Unit, e.Range));
            Assert.Contains(e.By, new[] { "a1", "a2" });
            var c = (PodActivatedEvent)events[contact];
            Assert.Equal(("sighted", "s2", e.By), (c.Cause, c.Unit, c.By));
        }

        [Fact(DisplayName = "concealment and pods › a patrol that walks up to the squad is the one that saw it")]
        public void APatrolThatWalksUpToTheSquadIsTheOneThatSawIt()
        {
            var s = Field(30, 20);
            s.Concealed = true;
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 10);
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 20, 10, extra: (u) => u.PodId = "p1");
            s.Pods.Add(new Pod { Id = "p1", Active = false, UnitIds = new List<string> { "a1" }, Patrol = new List<Pos> { P(2, 10, 0) }, PatrolIndex = 0 });
            var sim = new Sim(s, new Rng(1), new[] { 0 });
            sim.MoveAlong(a, Enumerable.Range(0, 15).Select((i) => P(19 - i, 10, 0)).ToList(), 0);
            Assert.False(s.Concealed);
            var broken = sim.Events.OfType<ConcealmentBrokenEvent>().First();
            Assert.Equal(("patrol", "s1", "a1", (double?)8), (broken.Reason, broken.Unit, broken.By, broken.Range));
            var contact = sim.Events.OfType<PodActivatedEvent>().First();
            Assert.Equal(("patrol", "s1", "a1"), (contact.Cause, contact.Unit, contact.By));
        }

        // ------------------------------------------------------------ recovery missions

        private static (GameState s, Unit a, Unit b) Recovery()
        {
            var s = Field();
            s.Mission = new MissionState
            {
                Kind = MissionKinds.Recovery,
                TurnsLeft = 3,
                Item = new MissionItem { Pos = P(4, 0, 0), Carrier = null, Evacuated = false },
                EvacZone = new List<Vec2> { new Vec2 { X = 0, Y = 5 } },
                Terminal = null,
                ObjectiveUnit = null,
                ObjectiveDone = false,
                HackBonus = 0,
                Reinforcements = new List<Reinforcement>(),
            };
            var a = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            var b = AddUnit(s, "s2", TemplateIds.Specialist, 1, 0);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            return (s, a, b);
        }

        [Fact(DisplayName = "recovery missions › walking over the objective picks it up; a carrier who falls drops it")]
        public void WalkingOverTheObjectivePicksItUp()
        {
            var (s, a, b) = Recovery();
            // Nobody walks through anyone (19/10): the partner steps out of the row first.
            b.Pos = P(1, 3, 0);
            Run(s, Move("s1", 6, 0));
            Assert.Equal("s1", s.Mission.Item?.Carrier);
            var sim = new Sim(s, new Rng(1), new[] { 0 });
            sim.Damage(a, 99, 0);
            Assert.Null(s.Mission.Item!.Carrier);
            Assert.Equal((6, 0), (s.Mission.Item.Pos!.X, s.Mission.Item.Pos.Y));
        }

        [Fact(DisplayName = "recovery missions › evacuating is a joint call from the bus: on the partner’s yes whoever is in it gets out with the objective, and the rest are lost")]
        public void EvacuatingIsAJointCallFromTheBus()
        {
            var (s, _, b) = Recovery();
            b.Owner = 1;
            CommandContext BothCtx(int slot) => Ctx(slot, Both);
            Run(s, Move("s1", 4, 0), BothCtx(0));
            Run(s, Move("s1", 0, 5), BothCtx(0));
            Assert.Equal("s1", s.Mission.Item?.Carrier);
            // Holding the item with no enemy left is not a win any more (docs/PRUEBA-2026-10-14.md P1).
            // s1 calls from the bus; the partner's s2 is still out in the street.
            Run(s, new EvacCommand { Unit = "s1" }, BothCtx(0));
            Assert.NotNull(s.EvacCall);
            Assert.Equal((0, "s1"), (s.EvacCall!.By, s.EvacCall.Unit));
            Assert.Null(s.Outcome);
            // Only the partner answers, and a no keeps everyone on the field.
            Assert.False(Rules.ExecuteCommand(s, new EvacAnswerCommand { Accept = true }, BothCtx(0)).Ok);
            Run(s, new EvacAnswerCommand { Accept = false }, BothCtx(1));
            Assert.Null(s.EvacCall);
            // Not from outside the bus.
            Assert.False(Rules.ExecuteCommand(s, new EvacCommand { Unit = "s2" }, BothCtx(1)).Ok);
            // Called again and a yes: s1 gets out with the item, s2 is lost, and the mission is won.
            Assert.True(Rules.EvacWins(s));
            Run(s, new EvacCommand { Unit = "s1" }, BothCtx(0));
            Run(s, new EvacAnswerCommand { Accept = true }, BothCtx(1));
            Assert.True(s.Mission.Item?.Evacuated);
            Assert.True(b.Lost);
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        [Fact(DisplayName = "recovery missions › a call to evacuate whose answerer has gone leaves at once")]
        public void ACallToEvacuateWhoseAnswererHasGoneLeavesAtOnce()
        {
            var (s, _, b) = Recovery();
            b.Owner = 1;
            Run(s, Move("s1", 0, 5), Ctx(0, Both));
            Run(s, new EvacCommand { Unit = "s1" }, Ctx(0, Both));
            Assert.NotNull(s.EvacCall);
            Rules.SyncTurn(s, new[] { 0 }, new Rng(1));
            Assert.True(s.Mission.EvacDone);
            Assert.True(b.Lost);
        }

        [Fact(DisplayName = "recovery missions › when the timer runs out La Lata leaves: nobody aboard with the objective, the mission is lost to the clock")]
        public void WhenTheTimerRunsOutLaLataLeaves()
        {
            var (s, a, b) = Recovery();
            Run(s, Move("s1", 0, 5));
            for (var i = 0; i < 4 && s.Outcome == null; i++) Run(s, new EndTurnCommand { Ready = true });
            Assert.True(s.Mission.EvacDone);
            // Whoever was aboard got out; the rest are lost.
            Assert.True(a.Evacuated);
            Assert.True(b.Lost);
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            Assert.Equal(EndReasons.Timer, s.OutcomeReason);
        }

        [Fact(DisplayName = "recovery missions › when the timer runs out with the objective aboard, the mission is won; whoever was outside is lost (playtest 18/10, #8)")]
        public void WhenTheTimerRunsOutWithTheObjectiveAboardTheMissionIsWon()
        {
            var (s, a, b) = Recovery();
            Run(s, Move("s1", 4, 0));
            Run(s, Move("s1", 0, 5));
            Assert.Equal("s1", s.Mission.Item?.Carrier);
            for (var i = 0; i < 4 && s.Outcome == null; i++) Run(s, new EndTurnCommand { Ready = true });
            Assert.True(s.Mission.Item?.Evacuated);
            Assert.True(a.Evacuated);
            Assert.True(b.Lost);
            Assert.Equal((Outcomes.Victory, EndReasons.Objective), (s.Outcome, s.OutcomeReason));
        }

        [Fact(DisplayName = "recovery missions › either player can abandon the mission")]
        public void EitherPlayerCanAbandonTheMission()
        {
            var (s, _, _) = Recovery();
            var events = Rules.AbandonMission(s);
            Assert.Single(events);
            Assert.Equal(EndReasons.Abandoned, s.OutcomeReason);
        }

        [Fact(DisplayName = "recovery missions › alone, a player commands the partner’s soldiers too; with the partner here, not (Arturo, 17/10)")]
        public void AloneAPlayerCommandsThePartnersSoldiersToo()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Rookie, 2, 2, extra: (u) => u.Owner = 0);
            var theirs = AddUnit(s, "s2", TemplateIds.Rookie, 4, 2, extra: (u) => u.Owner = 1);
            AddUnit(s, "a1", TemplateIds.Zombie, 18, 18);
            var refused = Rules.ExecuteCommand(s, Move("s2", 5, 2), 0, new[] { 0, 1 }, new Rng(1));
            Assert.Equal((false, "Es de la escuadra del otro jugador."), (refused.Ok, refused.Error));
            Assert.True(Rules.ExecuteCommand(s, Move("s2", 5, 2), 0, new[] { 0 }, new Rng(1)).Ok);
            Assert.Equal((5, 2), (theirs.Pos.X, theirs.Pos.Y));
        }

        [Fact(DisplayName = "recovery missions › with the partner here, abandoning is asked first: a no keeps it going, a yes ends it (playtest 16/10)")]
        public void WithThePartnerHereAbandoningIsAskedFirst()
        {
            var (s, _, _) = Recovery();
            var called = Assert.IsType<AbandonCalledEvent>(Assert.Single(Rules.CallAbandon(s, 0, Both)));
            Assert.Equal(0, called.Slot);
            Assert.Equal(0, s.AbandonCall?.By);
            // Whoever asked can't answer for the partner.
            Assert.Empty(Rules.AnswerAbandon(s, 0, true));
            var refused = Assert.IsType<AbandonRefusedEvent>(Assert.Single(Rules.AnswerAbandon(s, 1, false)));
            Assert.Equal(1, refused.Slot);
            Assert.Null(s.AbandonCall);
            Assert.Null(s.Outcome);
            Rules.CallAbandon(s, 0, Both);
            Rules.AnswerAbandon(s, 1, true);
            Assert.Equal(EndReasons.Abandoned, s.OutcomeReason);
            // Alone, it ends at once.
            var alone = Recovery().s;
            Rules.CallAbandon(alone, 0, new[] { 0 });
            Assert.Equal(EndReasons.Abandoned, alone.OutcomeReason);
        }

        // ------------------------------------------------------------ turn flow

        [Fact(DisplayName = "turn flow › the XCOM turn ends when every connected player is ready")]
        public void TheXcomTurnEndsWhenEveryConnectedPlayerIsReady()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 0, extra: (u) => u.Owner = 0);
            AddUnit(s, "s2", TemplateIds.Specialist, 1, 0, extra: (u) => u.Owner = 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            Run(s, new EndTurnCommand { Ready = true }, Ctx(0, Both));
            Assert.Equal(Teams.Xcom, s.ActiveTeam);
            var events = Run(s, new EndTurnCommand { Ready = true }, Ctx(1, Both));
            Assert.Equal(new[] { Teams.Alien, Teams.Xcom }, events.OfType<TurnBeganEvent>().Select((e) => e.Team));
            Assert.Equal(2, s.Turn);
        }

        [Fact(DisplayName = "turn flow › a disconnected partner does not block the end of turn")]
        public void ADisconnectedPartnerDoesNotBlockTheEndOfTurn()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 0, extra: (u) => u.Owner = 0);
            AddUnit(s, "s2", TemplateIds.Specialist, 1, 0, extra: (u) => u.Owner = 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            Run(s, new EndTurnCommand { Ready = true }, Ctx(0, new[] { 0 }));
            Assert.Equal(2, s.Turn);
        }

        [Fact(DisplayName = "turn flow › only the player with the command gives orders, until they pass it")]
        public void OnlyThePlayerWithTheCommandGivesOrders()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 0, extra: (u) => u.Owner = 0);
            AddUnit(s, "s2", TemplateIds.Specialist, 1, 0, extra: (u) => u.Owner = 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            s.Command = 0;
            var notYet = Rules.ExecuteCommand(s, Move("s2", 1, 2), Ctx(1, Both));
            Assert.False(notYet.Ok);
            Assert.Matches("mando", notYet.Error);
            Assert.False(Rules.ExecuteCommand(s, new PassCommand(), Ctx(1, Both)).Ok);
            var passed = Assert.IsType<CommandPassedEvent>(Assert.Single(Run(s, new PassCommand(), Ctx(0, Both))));
            Assert.Equal(1, passed.To);
            Run(s, Move("s2", 1, 2), Ctx(1, Both));
            SamePlace(P(1, 2, 0), UnitOf(s, "s2").Pos);
            // Alone, there's nobody to pass it to.
            Assert.False(Rules.ExecuteCommand(s, new PassCommand(), Ctx(1, new[] { 1 })).Ok);
            // Nor to a partner with nothing left to do.
            UnitOf(s, "s1").Ap = 0;
            var idle = Rules.ExecuteCommand(s, new PassCommand(), Ctx(1, Both));
            Assert.False(idle.Ok);
            Assert.Matches("acciones", idle.Error);
        }

        [Fact(DisplayName = "turn flow › ending the turn hands the command over, and the other player opens the next turn")]
        public void EndingTheTurnHandsTheCommandOver()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 0, extra: (u) => u.Owner = 0);
            AddUnit(s, "s2", TemplateIds.Specialist, 1, 0, extra: (u) => u.Owner = 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            s.Command = 0;
            Run(s, new EndTurnCommand { Ready = true }, Ctx(0, Both));
            Assert.Equal((Teams.Xcom, (int?)1), (s.ActiveTeam, s.Command));
            // Done players can't be handed the command back.
            Assert.False(Rules.ExecuteCommand(s, new PassCommand(), Ctx(1, Both)).Ok);
            Run(s, new EndTurnCommand { Ready = true }, Ctx(1, Both));
            Assert.Equal((2, Teams.Xcom, (int?)1), (s.Turn, s.ActiveTeam, s.Command));
        }

        [Fact(DisplayName = "turn flow › a holder out of actions passes on their own; a holder who left blocks nobody")]
        public void AHolderOutOfActionsPassesOnTheirOwn()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 0, extra: (u) =>
            {
                u.Owner = 0;
                u.Ap = 1;
            });
            AddUnit(s, "s2", TemplateIds.Specialist, 1, 0, extra: (u) => u.Owner = 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            s.Command = 0;
            Run(s, Move("s1", 0, 2), Ctx(0, Both));
            Assert.Equal(1, s.Command);
            // Player 1 drops out: player 0 can act and takes the command.
            UnitOf(s, "s1").Ap = 2;
            Run(s, Move("s1", 0, 3), Ctx(0, new[] { 0 }));
            Assert.Equal(0, s.Command);
        }

        [Fact(DisplayName = "turn flow › killing the last alien wins an elimination mission")]
        public void KillingTheLastAlienWinsAnEliminationMission()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 5, 5);
            var sim = new Sim(s, new Rng(1), new[] { 0 });
            sim.Damage(a, 99, 0);
            sim.CheckOutcome();
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.False(Rules.ExecuteCommand(s, new EndTurnCommand { Ready = true }, Ctx()).Ok);
        }

        // ------------------------------------------------------------ maps

        [Fact(DisplayName = "maps › every map parses and every pod and the objective are reachable from the spawn")]
        public void EveryMapParsesAndEveryPodIsReachable()
        {
            foreach (var def in Maps.All.Values)
            {
                var map = Maps.ParseMap(def);
                Assert.True(map.Spawns.Count >= 6);
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade(def.Id), Slots = new List<int> { 0, 1 }, Rng = new Rng(3), Kind = MissionKinds.Recovery });
                var scout = Events.CloneUnit(s.Units[0]);
                scout.Team = Teams.Xcom;
                s.Units = new List<Unit> { scout };
                var reach = Paths.ComputeReach(s, scout, 10_000);
                var targets = map.PodSpawns.SelectMany((kv) => kv.Value).Concat(map.Item != null ? new[] { Grid.Ground(map.Item) } : new Pos[0]).Concat(map.EvacZone.Select(Grid.Ground));
                // Upstairs too, on maps with storeys (docs/FASE6-PLAN.md).
                foreach (var t in targets) Assert.True(reach.Cost.Has(Grid.NodeOf(s, Grid.ToPos(t))), $"{def.Id} {t.X},{t.Y},{t.Z}");
            }
        }

        // ------------------------------------------------------------ alien AI

        [Fact(DisplayName = "alien AI › scampering aliens do not end up next to a soldier")]
        public void ScamperingAliensDoNotEndUpNextToASoldier()
        {
            for (var seed = 1; seed <= 6; seed++)
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(seed) });
                var sim = NewSim(s, seed);
                var spots = new[] { P(12, 15, 0), P(13, 15, 0), P(14, 16, 0), P(11, 16, 0), P(15, 15, 0), P(10, 15, 0) };
                var squad = s.Units.Where((u) => u.Team == Teams.Xcom).ToList();
                for (var i = 0; i < squad.Count; i++) squad[i].Pos = spots[i];
                s.Concealed = false;
                sim.ActivatePods(s.Pods.Select((p) => p.Id).ToList(), new Activation { Cause = PodCauses.Sighted });
                foreach (var a in s.Units.Where((u) => u.Team == Teams.Alien && u.Alive && u.Template != TemplateIds.Lancer).ToList())
                {
                    var nearest = s.Units.Where((u) => u.Team == Teams.Xcom && u.Alive).Select((x) => Js.Hypot(x.Pos.X - a.Pos.X, x.Pos.Y - a.Pos.Y)).Min();
                    Assert.True(nearest >= 2, $"seed {seed}: {a.Id} at {a.Pos.X},{a.Pos.Y}");
                }
            }
        }

        // ------------------------------------------------------------ replication

        /// <summary>A crude bot that tries every ability it has, with valid-looking targets.</summary>
        private static Command BotCommand(GameState s, int slot, Rng rng)
        {
            var mine = s.Units.Where((u) => u.Alive && u.Team == Teams.Xcom && u.Owner == slot && u.Ap > 0).ToList();
            if (mine.Count == 0 || rng.Int(10) == 0) return new EndTurnCommand { Ready = true };
            if (rng.Int(12) == 0) return new PassCommand();
            var u = rng.Pick(mine);
            var aliens = s.Units.Where((a) => a.Alive && a.Team == Teams.Alien).ToList();
            // Searching whenever there is something at hand, half the time.
            if (Looting.SearchableTiles(s, u).Count > 0 && rng.Int(2) == 0) return Ability(u.Id, AbilityIds.Search, tile: rng.Pick(Looting.SearchableTiles(s, u)));
            if (rng.Int(3) == 0 && aliens.Count > 0)
            {
                var id = rng.Pick(Abilities.AbilitiesOf(u));
                var def = Content.Abilities[id];
                switch (def.Target)
                {
                    case AbilityTargets.Enemy:
                    case AbilityTargets.Ally:
                    case AbilityTargets.Corpse:
                    {
                        var t = Abilities.AbilityTargets(s, u, id);
                        return Ability(u.Id, id, target: (t.Count > 0 ? rng.Pick(t) : rng.Pick(aliens)).Id);
                    }
                    case AbilityTargets.Melee:
                    {
                        var t = Content.IsAttack(id) ? Abilities.MeleeTargets(s, u, id) : new List<MeleeOption>();
                        return Ability(u.Id, id, target: (t.Count > 0 ? rng.Pick(t).Target : rng.Pick(aliens)).Id);
                    }
                    case AbilityTargets.Tile:
                    {
                        var furniture = id == AbilityIds.Search ? Looting.SearchableTiles(s, u) : new List<Pos>();
                        return Ability(u.Id, id, tile: furniture.Count > 0 ? rng.Pick(furniture) : rng.Pick(aliens).Pos);
                    }
                    default:
                        return Ability(u.Id, id);
                }
            }
            var item = s.Mission.Item;
            Vec2 goal = (item != null && item.Carrier == u.Id ? s.Mission.EvacZone.FirstOrDefault() : item?.Pos) ?? (aliens.Count > 0 ? rng.Pick(aliens).Pos : u.Pos);
            var dx = Math.Sign(goal.X - u.Pos.X) * rng.Range(1, 6);
            var dy = Math.Sign(goal.Y - u.Pos.Y) * rng.Range(1, 6);
            return new MoveCommand { Unit = u.Id, To = P(u.Pos.X + dx, u.Pos.Y + dy) };
        }

        /// <summary>Makes every soldier a colonel with a random spread of aptitudes and talents.</summary>
        private static void Veterans(GameState s, Rng rng)
        {
            foreach (var u in s.Units)
            {
                if (u.Team != Teams.Xcom || u.Template == TemplateIds.Vip) continue;
                var lines = Bars.VeteranSkills(rng);
                var stats = Bars.FighterStatsOf(lines.Skills, lines.Growth, lines.Board!, u.Gear, u.Arms);
                u.Rank = stats.Rank;
                u.Perks = stats.Perks;
                u.Mods = stats.Mods;
                u.Charges = stats.Charges;
                u.MaxHp = stats.MaxHp;
                u.Hp = stats.MaxHp;
                u.Will = stats.Will;
                u.Armor = stats.Armor;
            }
        }

        /// <summary>
        /// The missions are the step-1 golden ones (createMission's, by TypeScript: the three biomes generated, «la torre»
        /// with its storeys, two at night), each an elimination with its two squads, instead of eighteen made here with
        /// createMission (setup.ts is step 3's) of every kind, with the pods and difficulties varied.
        /// </summary>
        [Fact(DisplayName = "replication › clients replaying the event stream end with exactly the host state")]
        public void ClientsReplayingTheEventStreamEndWithExactlyTheHostState()
        {
            var missions = new[] { "city-1", "city-7", "wilds-1", "wilds-7", "facility-1", "facility-7", "torre", "city-5-night", "wilds-3-night" };
            for (var seed = 1; seed <= missions.Length; seed++)
            {
                var rng = new Rng(seed);
                var host = GoldenState(missions[seed - 1]);
                if (seed % 3 != 0) Veterans(host, rng);
                var client = Events.CloneState(host);
                for (var i = 0; i < 700 && host.Outcome == null; i++)
                {
                    // Mostly whoever holds the command, sometimes the other (whose orders are refused).
                    var slot = rng.Int(5) == 0 ? i % 2 : host.Command ?? i % 2;
                    var r = Rules.ExecuteCommand(host, BotCommand(host, slot, rng), slot, Both, rng);
                    if (r.Ok) foreach (var e in r.Events!) Events.ApplyEvent(client, e);
                }
                var diff = Step2Json.StateDiff(host, client);
                if (diff != null) throw new Xunit.Sdk.XunitException($"{missions[seed - 1]} (seed {seed}): {diff}");
            }
        }
    }
}
