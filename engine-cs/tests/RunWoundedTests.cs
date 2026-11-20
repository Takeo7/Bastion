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
    /// G23.3 El herido (Arturo, cuadros 71, 73 y 92; .dev-snapshots/gameplay/g23/HERIDO.md): a Rescate whose survivor can't
    /// walk. Never acts; carried from beside (the first time they join, no Talk), set down, passed on, an action each;
    /// carried, off the field and where their carrier goes, who walks 6 tiles at most and fires no long gun; their carrier
    /// down, on the floor, unhurt. La Lata doesn't go while they live out of it, takes them with no seat, and wins with them
    /// aboard; dead, it goes with no prize nor recruit. The dead bite them on the floor.
    /// </summary>
    public class RunWoundedTests
    {
        private static (CampaignState C, GameState S) Wounded(int seed = 7, string site = Places.Country, int seats = 1) => LaunchAt(NodeKinds.Wounded, before: (c) =>
        {
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = seats;
            c.Squad = Runs.DefaultSquad(c, c.Players);
        }, node: (n) =>
        {
            n.Site = site;
            n.Seed = seed;
            n.Modifier = null;
        });

        private static List<Unit> Squad(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive).ToList();

        private static Unit Soldier(GameState s, int i = 0) => Squad(s)[i];

        private static Unit Hurt(GameState s) => s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);

        /// <summary>A free tile beside `p`, on its level, reached from it as a box is searched, other than `but`.</summary>
        private static Pos Beside(GameState s, Pos p, Pos? but = null) => Grid.Dirs8.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z })
            .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && Grid.UnitAt(s, q) == null
                && Looting.ReachesToSearch(s, p, q) && (but == null || !Grid.SamePos(q, but)));

        private static CommandResult Act(GameState s, Unit u, string ability, string? target = null, Pos? tile = null)
        {
            u.Ap = Math.Max(u.Ap, 1);
            return Exec(s, new AbilityCommand { Unit = u.Id, Ability = ability, Target = target, Tile = tile }, slot: u.Owner ?? 0);
        }

        /// <summary>`u` beside the wounded, carrying them.</summary>
        private static Unit Carried(GameState s, Unit u)
        {
            var w = Hurt(s);
            u.Pos = Beside(s, w.Pos);
            u.Ap = 2;
            var r = Act(s, u, AbilityIds.Carry);
            Assert.True(r.Ok, r.Error);
            return w;
        }

        private static CommandResult EndTurn(GameState s) => Exec(s, new EndTurnCommand { Ready = true });

        // ------------------------------------------------------------------ the stage

        [Fact]
        public void OnTheMapsRowsTwoToFiveARescatesMaps()
        {
            // Producto, 19/11 (the 0.13.5): as the other specials, on a Rescate's maps.
            Assert.Contains(NodeKinds.Wounded, RunMap.Specials);
            var nodes = Enumerable.Range(1, 500).SelectMany((seed) => RunMap.Generate(1, seed)).ToList();
            var hurt = nodes.Where((n) => n.Kind == NodeKinds.Wounded).ToList();
            Assert.All(hurt, (n) => Assert.InRange(n.Row, 2, RunMap.NodeRows));
            double share = hurt.Count / (double)nodes.Count((n) => n.Row >= 2 && n.Row <= RunMap.NodeRows);
            // One special in twelve since Separados (G23.4, 20/11: 0.0147 on these 500 maps, 0.016 before).
            Assert.InRange(share, 0.012, 0.04);
            // Where a Rescate is: the six places and the special maps for them, never the Key's or Mordido's own only.
            var rescue = nodes.Where((n) => n.Kind == NodeKinds.Rescue).Select((n) => n.Site).Distinct().ToList();
            Assert.All(hurt, (n) => Assert.Contains(n.Site, rescue));
            Assert.True(hurt.Select((n) => n.Site).Distinct().Count() >= 6);
            Assert.All(hurt, (n) => Assert.False(RunMap.LooseSurvivor(n)));
            Assert.Equal(MissionKinds.Rescue, Runs.MissionKindOf(NodeKinds.Wounded));
        }

        [Fact]
        public void ARescateWithItsOwnSurvivor()
        {
            Assert.Equal(MissionKinds.Rescue, Runs.MissionKindOf(NodeKinds.Wounded));
            Assert.False(RunMap.LooseSurvivor(new RunNode { Id = "n3-0", Row = 3, Kind = NodeKinds.Wounded, Seed = 4242 }));
            var (_, s) = Wounded();
            var w = Hurt(s);
            Assert.True(s.Mission.Wounded);
            Assert.True(w.Lame);
            Assert.True(w.Captive);
            Assert.Equal(s.Mission.Survivor, w.Id);
            // Never acts, never walks.
            Assert.Equal(0, Content.ApPerTurnOf(w));
            Assert.Equal(0, Paths.FullMoves(w));
            Assert.DoesNotContain(AbilityIds.Carry, Abilities.AbilitiesOf(w));
            Assert.Empty(Abilities.CarryAbilities(s, w));
            // Carrying is this stage's only: never in AbilitiesOf (the bar shows that list in every fight).
            var u = Squad(s)[0];
            Assert.DoesNotContain(Abilities.AbilitiesOf(u), (a) => a == AbilityIds.Carry || a == AbilityIds.SetDown || a == AbilityIds.Pass);
            Assert.Equal(new[] { AbilityIds.Carry }, Abilities.CarryAbilities(s, u));
            Carried(s, u);
            Assert.Equal(new[] { AbilityIds.SetDown, AbilityIds.Pass }, Abilities.CarryAbilities(s, u));
            var (_, other) = LaunchAt(NodeKinds.Rescue, node: (n) => { n.Site = Places.Country; n.Seed = 7; n.Modifier = null; });
            var o = Squad(other)[0];
            Assert.Empty(Abilities.CarryAbilities(other, o));
            Assert.Equal("No tiene esa habilidad.", Abilities.AbilityBlocker(other, o, AbilityIds.Carry));
        }

        [Fact]
        public void ARunStartedBeforeHasNone()
        {
            // Saved by the 0.13.2 a turn in: no wounded, nobody lame or carried.
            var s = Bastion.Net.Json.Read<GameState>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "g19", "comisaria-0132.json")));
            Assert.Null(s.Mission.Wounded);
            Assert.All(s.Units, (u) => Assert.True(u.Lame == null && u.Carrying == null && u.CarriedBy == null));
            Assert.Null(Rules.WoundedOf(s));
            Assert.False(Rules.WoundedOut(s));
        }

        // ------------------------------------------------------------------ carrying

        [Fact]
        public void CarriedFromBesideOnlyAndTheFirstTimeTheyJoin()
        {
            var (_, s) = Wounded();
            var w = Hurt(s);
            var u = Soldier(s);
            // From afar, no.
            if (!Aim.Adjacent(u.Pos, w.Pos))
            {
                var far = Act(s, u, AbilityIds.Carry);
                Assert.False(far.Ok);
            }
            // Talking to them, no: they are carried.
            u.Pos = Beside(s, w.Pos);
            Assert.NotNull(Abilities.AbilityBlocker(s, u, AbilityIds.Talk));
            // Beside them: on its back, found and joined, its player's.
            var at = Events.CopyPos(w.Pos);
            u.Ap = 2;
            var r = Act(s, u, AbilityIds.Carry);
            Assert.True(r.Ok, r.Error);
            Assert.Equal(1, u.Ap);
            Assert.True(s.Mission.SurvivorFound);
            Assert.False(w.Captive);
            Assert.Equal(u.Owner, w.Owner);
            Assert.Equal(w.Id, u.Carrying);
            Assert.Equal(u.Id, w.CarriedBy);
            // Off the field: nobody on their tile, and where their carrier is.
            Assert.False(w.Alive);
            Assert.Null(Grid.UnitAt(s, at));
            Assert.True(Grid.SamePos(w.Pos, u.Pos));
            Assert.Same(u, Grid.UnitAt(s, u.Pos));
            Assert.True(Rules.WoundedOut(s));
            // Only one.
            Assert.NotNull(Abilities.AbilityBlocker(s, u, AbilityIds.Carry));
        }

        [Fact]
        public void TheyNeverActNorWalk()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            var w = Carried(s, u);
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.SetDown, tile: Beside(s, u.Pos))).Ok);
            Assert.True(w.Alive);
            Assert.True(EndTurn(s).Ok);
            if (s.ActiveTeam != Teams.Xcom) EndTurn(s);
            Assert.Equal(0, w.Ap);
            Assert.Equal(0, Paths.MoveBudget(s, w));
            Assert.False(Paths.HasTurnLeft(w));
            Assert.Equal("Sin acciones.", Abilities.AbilityBlocker(s, w, AbilityIds.Search));
        }

        [Fact]
        public void TheyGoWhereTheirCarrierGoes()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            var w = Carried(s, u);
            var to = Beside(s, u.Pos, w.Pos);
            Events.ApplyEvent(s, new MovedEvent { Unit = u.Id, Path = new List<Pos> { Events.CopyPos(u.Pos), to }, Ap = 0, Moves = Paths.CostOrtho });
            Assert.True(Grid.SamePos(to, u.Pos));
            Assert.True(Grid.SamePos(to, w.Pos));
            // With the state, to the partner and in a save (a late one, or one come back, sees them on its back).
            foreach (var copy in new[] { Events.CloneState(s), Bastion.Net.Json.Read<GameState>(Bastion.Net.Json.Write(s)) })
            {
                Assert.True(copy.Mission.Wounded);
                var cu = copy.Units.First((x) => x.Id == u.Id);
                var cw = copy.Units.First((x) => x.Id == w.Id);
                Assert.Equal(cw.Id, cu.Carrying);
                Assert.Equal(cu.Id, cw.CarriedBy);
                Assert.True(cw.Lame);
                Assert.True(Grid.SamePos(cu.Pos, cw.Pos));
            }
        }

        [Fact]
        public void TheCarrierWalksSixAtMostAndKeepsTheRest()
        {
            var (_, s) = Wounded();
            // Found already (the hidden squad's extra aside: it goes when they are).
            s.Concealed = false;
            var u = Soldier(s);
            var raw = Paths.MovesLeft(s, u);
            Assert.True(raw > Paths.CarryMoves);
            var w = Carried(s, u);
            Assert.Equal(12, Paths.CarryMoves);
            Assert.Equal(Paths.CarryMoves, Paths.MoveBudget(s, u));
            Assert.All(Paths.ComputeReach(s, u, Paths.MoveBudget(s, u), ignore: (_) => true).Cost.Values, (c) => Assert.True(c <= Paths.CarryMoves));
            // It doesn't run (Arturo: «no corre»): never heard as a run.
            Assert.False(Paths.IsRun(s, u, Paths.CarryMoves));
            // Three tiles with them: what it had less 3, capped at the 6.
            var to = Beside(s, u.Pos, w.Pos);
            Events.ApplyEvent(s, new MovedEvent { Unit = u.Id, Path = new List<Pos> { Events.CopyPos(u.Pos), to }, Ap = 0, Moves = 3 * Paths.CostOrtho });
            Assert.Equal(raw - 3 * Paths.CostOrtho, u.Moves);
            Assert.Equal(Paths.CarryMoves - 3 * Paths.CostOrtho, Paths.MoveBudget(s, u));
            // Set down, the rest of its own movement is there.
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.SetDown, tile: Beside(s, u.Pos))).Ok);
            Assert.Equal(raw - 3 * Paths.CostOrtho, Paths.MoveBudget(s, u));
            // Up again and three more: six with them this turn, no more, whatever is left.
            Assert.True(Act(s, u, AbilityIds.Carry).Ok);
            Assert.Equal(Paths.CarryMoves - 3 * Paths.CostOrtho, Paths.MoveBudget(s, u));
            Assert.True(Act(s, u, AbilityIds.SetDown, tile: Beside(s, u.Pos)).Ok);
            // Taken up having walked: what it has left, 6 at most.
            var v = Soldier(s, 1);
            v.Moves = 2 * Paths.CostOrtho;
            v.Pos = Beside(s, w.Pos, u.Pos);
            Assert.True(Act(s, v, AbilityIds.Carry).Ok);
            Assert.Equal(2 * Paths.CostOrtho, Paths.MoveBudget(s, v));
        }

        [Fact]
        public void TheCarrierFiresNoLongGunButThePistolAndTheBlade()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            u.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Pistol = true, Melee = FoundWeaponIds.Machete };
            u.Ammo = 4;
            u.PistolAmmo = 4;
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Shoot));
            Carried(s, u);
            Assert.Equal(Lore.WoundedText.NoLongArm, Abilities.AbilityBlocker(s, u, AbilityIds.Shoot));
            Assert.Equal(Lore.WoundedText.NoLongArm, Abilities.AbilityBlocker(s, u, AbilityIds.Overwatch));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Pistol));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Slash));
        }

        [Fact]
        public void PassedToOneBesideTheGiverSpendsAnActionSetDownBesideToo()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            var v = Soldier(s, 1);
            var w = Carried(s, u);
            // Nobody beside: no.
            v.Pos = Grid.Dirs8.Select((d) => new Pos { X = u.Pos.X + 4 * d.X, Y = u.Pos.Y + 4 * d.Y, Z = u.Pos.Z }).FirstOrDefault((q) => Grid.InBounds(s, q.X, q.Y) && Grid.UnitAt(s, q) == null) ?? v.Pos;
            if (!Aim.Adjacent(u.Pos, v.Pos)) Assert.NotNull(Abilities.AbilityBlocker(s, u, AbilityIds.Pass));
            v.Pos = Beside(s, u.Pos, w.Pos);
            u.Ap = 1;
            v.Ap = 2;
            var r = Act(s, u, AbilityIds.Pass, target: v.Id);
            Assert.True(r.Ok, r.Error);
            Assert.Equal(0, u.Ap);
            Assert.Equal(2, v.Ap);
            Assert.Null(u.Carrying);
            Assert.Equal(w.Id, v.Carrying);
            Assert.Equal(v.Id, w.CarriedBy);
            Assert.True(Grid.SamePos(v.Pos, w.Pos));
            // Down, on a free tile beside: on the field again.
            var tile = Beside(s, v.Pos);
            var d = Exec(s, Ability(v.Id, AbilityIds.SetDown, tile: tile));
            Assert.True(d.Ok, d.Error);
            Assert.Equal(1, v.Ap);
            Assert.Null(v.Carrying);
            Assert.Null(w.CarriedBy);
            Assert.True(w.Alive);
            Assert.True(Grid.SamePos(tile, w.Pos));
            Assert.Same(w, Grid.UnitAt(s, tile));
            // Not on a tile taken.
            var u2 = Soldier(s);
            u2.Pos = Beside(s, w.Pos);
            u2.Ap = 2;
            Assert.True(Act(s, u2, AbilityIds.Carry).Ok);
            Assert.False(Exec(s, Ability(u2.Id, AbilityIds.SetDown, tile: v.Pos)).Ok);
        }

        [Fact]
        public void NobodyAimsAtThemCarriedTheDeadBiteThemOnTheFloor()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            var w = Carried(s, u);
            var z = s.Units.First((x) => x.Alive && x.Team == Teams.Alien && x.Template == TemplateIds.Zombie);
            z.Pos = Beside(s, u.Pos);
            var targets = Abilities.MeleeTargets(s, z, AbilityIds.Claw).Select((o) => o.Target).ToList();
            Assert.Contains(u, targets);
            Assert.DoesNotContain(w, targets);
            // On the floor, beside the dead: one to bite.
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.SetDown, tile: Beside(s, u.Pos, z.Pos))).Ok);
            z.Pos = Beside(s, w.Pos, u.Pos);
            Assert.Contains(w, Abilities.MeleeTargets(s, z, AbilityIds.Claw).Select((o) => o.Target));
        }

        [Fact]
        public void FoundTheDeadGoForThemCarriedYetOrNot()
        {
            // Producto, 19/11 (cuadro 92: «en el suelo le muerden, como a cualquiera»): found, a wounded on the floor.
            var (_, s) = Wounded();
            var w = Hurt(s);
            var z = s.Units.First((x) => x.Alive && x.Team == Teams.Alien && x.Template == TemplateIds.Zombie);
            z.Pos = Beside(s, w.Pos);
            // Not found: hidden away, as the Rescate's survivor.
            Assert.True(w.Captive);
            Assert.DoesNotContain(w, Abilities.MeleeTargets(s, z, AbilityIds.Claw).Select((o) => o.Target));
            // Found, nobody's yet: one to bite.
            Events.ApplyEvent(s, new SurvivorFoundEvent { Unit = w.Id, By = Soldier(s).Id });
            Assert.False(w.Captive);
            Assert.Null(w.Owner);
            Assert.Contains(w, Abilities.MeleeTargets(s, z, AbilityIds.Claw).Select((o) => o.Target));
            Assert.True(Rules.WoundedOut(s));
            Assert.Equal(Lore.WoundedText.NoTalk, Abilities.AbilityBlocker(s, Soldier(s), AbilityIds.Talk));
            // Taken up the first time: joined, its player's.
            var u = Soldier(s);
            u.Pos = Beside(s, w.Pos, z.Pos);
            u.Ap = 2;
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.Carry)).Ok);
            Assert.Equal(u.Owner, w.Owner);
            Assert.Equal(u.Id, w.CarriedBy);
        }

        [Fact]
        public void EveryoneInLaLataTheDeadEndItForThem()
        {
            // The R1 fight that never ended (factory, knives, 16): one in La Lata, the wounded found outside. Now the dead
            // go for them, and with them dead La Lata may go («no se sale sin él» stays).
            var (_, s) = Wounded();
            var w = Hurt(s);
            Events.ApplyEvent(s, new SurvivorFoundEvent { Unit = w.Id, By = Soldier(s).Id });
            foreach (var u in Squad(s)) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            Assert.False(Rules.AllAboard(s));
            var dead = s.Units.Where((x) => x.Alive && x.Team == Teams.Alien && x.Template == TemplateIds.Zombie).Take(3).ToList();
            foreach (var z in dead)
            {
                z.Pos = Beside(s, w.Pos);
                s.Pods.First((p) => p.Id == z.PodId).Active = true;
            }
            for (var i = 0; i < 12 && s.Outcome == null; i++) EndTurn(s);
            Assert.False(w.Alive);
            Assert.NotNull(s.Outcome);
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            Assert.True(s.Mission.EvacDone);
        }

        [Fact]
        public void TheirCarrierDownTheyAreOnTheFloorUnhurtAndNeverRise()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            var w = Carried(s, u);
            var hp = w.Hp;
            var sim = new Sim(s, new Rng(1), new[] { 0 });
            sim.Damage(u, 99, 0);
            Assert.False(u.Alive);
            Assert.True(w.Alive);
            Assert.Null(w.CarriedBy);
            Assert.Equal(hp, w.Hp);
            Assert.True(Grid.SamePos(w.Pos, u.Pos));
            Assert.Contains(sim.Events, (e) => e is SetDownEvent d && d.Fell == true && d.Target == w.Id);
            // Carried, never one of the fallen that rise: a fight's turns over with them on someone's back.
            var (_, s2) = Wounded();
            var w2 = Carried(s2, Soldier(s2));
            for (var i = 0; i < 4 && s2.Outcome == null; i++) EndTurn(s2);
            Assert.False(w2.Reanimated);
            Assert.DoesNotContain(s2.Units, (x) => x.RisenFrom == w2.Id);
        }

        [Fact]
        public void CarriedTheyAreNoneOfTheDead()
        {
            // Producto, 19/11: Alive false on someone's back, and nothing of the dead's for it.
            var (_, s) = Wounded();
            var u = Soldier(s);
            var floor = s.FloorGear?.Count ?? 0;
            var dropped = s.Dropped?.Count ?? 0;
            var events = new List<GameEvent>();
            var w = Carried(s, u);
            // No body to search, nothing of theirs on the floor; none of the dead's for a Chillón to raise.
            Assert.NotSame(w, Looting.BodyAt(s, w.Pos));
            Assert.Equal(floor, s.FloorGear?.Count ?? 0);
            Assert.Equal(dropped, s.Dropped?.Count ?? 0);
            var z = s.Units.First((x) => x.Alive && x.Team == Teams.Alien);
            z.Pos = Grid.Dirs8.Select((d) => new Pos { X = u.Pos.X + 2 * d.X, Y = u.Pos.Y + 2 * d.Y, Z = u.Pos.Z }).First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Grid.UnitAt(s, q) == null);
            Assert.DoesNotContain(w, Abilities.CorpseTargets(s, z));
            // Turns on their carrier's back: never died (no «ha caído», no pack on the floor), never risen.
            for (var i = 0; i < 4 && s.Outcome == null; i++)
            {
                var r = EndTurn(s);
                events.AddRange(r.Events ?? new List<GameEvent>());
                if (!u.Alive) break;
            }
            if (u.Alive)
            {
                Assert.Equal(u.Id, w.CarriedBy);
                Assert.Equal(floor, s.FloorGear?.Count ?? 0);
            }
            Assert.DoesNotContain(events, (e) => e is DiedEvent d && d.Unit == w.Id);
            Assert.False(w.Reanimated);
            Assert.DoesNotContain(s.Units, (x) => x.RisenFrom == w.Id);
            // The fight over with them on someone's back: not one of the fallen in the report (only the squad's are),
            // nor a recruit.
            var (c2, s2) = Wounded();
            var w2 = Carried(s2, Soldier(s2));
            var had = c2.Soldiers.Count;
            var report = EndFight(c2, s2, Outcomes.Defeat, EndReasons.Abandoned);
            Assert.DoesNotContain(report.Squad, (e) => e.Status != ReportStatuses.Ok);
            Assert.Equal(had, c2.Soldiers.Count);
            Assert.DoesNotContain(report.Run!.Fallen, (f) => f.Name == w2.Name);
        }

        [Fact]
        public void TheLastOneDownCarryingThemItIsOver()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            var w = Carried(s, u);
            var sim = new Sim(s, new Rng(1), new[] { 0 });
            foreach (var x in Squad(s).Where((x) => x != u)) sim.Damage(x, 99, 0);
            Assert.Null(s.Outcome);
            sim.Damage(u, 99, 0);
            sim.CheckOutcome();
            Assert.True(w.Alive);
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            Assert.Equal(EndReasons.Wiped, s.OutcomeReason);
        }

        // ------------------------------------------------------------------ La Lata

        [Fact]
        public void LaLataDoesntGoWithoutThemFoundOrNot()
        {
            var (_, s) = Wounded();
            var w = Hurt(s);
            // Everyone in La Lata, the wounded never found: it waits.
            foreach (var u in Squad(s)) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            Assert.True(Rules.WoundedOut(s));
            Assert.False(Rules.AllAboard(s));
            var inside = s.Units.First((u) => u.Aboard == true);
            var r = Exec(s, new EvacCommand { Unit = inside.Id });
            Assert.False(r.Ok);
            Assert.Equal(Lore.WoundedText.NotWithout(null), r.Error);
            Assert.Null(s.Mission.EvacDone);
            // Their turn over: still there, all of them.
            Assert.True(EndTurn(s).Ok);
            Assert.Null(s.Outcome);
            Assert.True(w.Alive);
        }

        [Fact]
        public void TheCarrierInTheyAreInWithNoSeatAndItIsWon()
        {
            // The moto: 2 seats, 2 of the squad, and the wounded.
            var (c, s) = Wounded(seats: 0);
            Assert.Equal(2, s.Mission.Seats);
            var squad = Squad(s);
            Assert.Equal(2, squad.Count);
            var u = squad[0];
            var w = Carried(s, u);
            // Its walk ends on a tile of La Lata's: both in, one seat taken.
            var z = s.Mission.EvacZone.Select((v) => new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }).First((p) => Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p));
            var near = Beside(s, z);
            u.Pos = near;
            Events.ApplyEvent(s, new MovedEvent { Unit = u.Id, Path = new List<Pos> { near }, Ap = 0, Moves = 0 });
            u.Moves = null;
            var view = Events.CloneState(s);
            var go = Exec(s, new MoveCommand { Unit = u.Id, To = z });
            Assert.True(go.Ok, go.Error);
            // Event by event, as a view plays it: on its back or in, never neither (Arte, 19/11).
            foreach (var e in go.Events!)
            {
                Events.ApplyEvent(view, e);
                var vw = view.Units.Single((x) => x.Id == w.Id);
                Assert.True(vw.CarriedBy != null || vw.Aboard == true, e.T);
            }
            Assert.True(u.Aboard);
            Assert.True(w.Aboard);
            Assert.True(w.Evacuated);
            Assert.Null(w.CarriedBy);
            Assert.Equal(1, Rules.SeatsFree(s));
            Assert.False(Rules.WoundedOut(s));
            Assert.True(Rules.EvacWins(s));
            // The other one in: La Lata goes by itself, won.
            var v = squad[1];
            var z2 = s.Mission.EvacZone.Select((x) => new Pos { X = (int)x.X, Y = (int)x.Y, Z = 0 }).First((p) => Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p) && Abilities.InEvacZone(s, p));
            v.Pos = Beside(s, z2);
            v.Moves = null;
            v.Ap = 2;
            var go2 = Exec(s, new MoveCommand { Unit = v.Id, To = z2 });
            Assert.True(go2.Ok, go2.Error);
            Assert.True(v.Aboard);
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.True(Rules.WoundedSafe(s));
            // On the maps (the 0.13.5): a special's reward, and they join.
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.InRange(report.Run!.Reward ?? 0, Runs.RewardMin, Runs.RewardMax);
            Assert.NotNull(report.Run.Joined);
            Assert.Contains(c.Soldiers, (x) => x.Id == report.Run.Joined && x.Name == w.Name);
        }

        [Fact]
        public void SetDownOnLaLataTheyAreInWithNoSeat()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            var w = Carried(s, u);
            var z = s.Mission.EvacZone.Select((v) => new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }).First((p) => Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p));
            u.Pos = Beside(s, z);
            Events.ApplyEvent(s, new MovedEvent { Unit = u.Id, Path = new List<Pos> { Events.CopyPos(u.Pos) }, Ap = 0, Moves = 0 });
            var free = Rules.SeatsFree(s);
            u.Ap = 2;
            var r = Exec(s, Ability(u.Id, AbilityIds.SetDown, tile: z));
            Assert.True(r.Ok, r.Error);
            Assert.True(w.Aboard);
            Assert.Equal(free, Rules.SeatsFree(s));
            Assert.False(Rules.WoundedOut(s));
        }

        [Fact]
        public void DeadLaLataGoesWithNoPrizeNorRecruit()
        {
            var (c, s) = Wounded();
            var w = Hurt(s);
            var u = Soldier(s);
            Carried(s, u);
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.SetDown, tile: Beside(s, u.Pos))).Ok);
            new Sim(s, new Rng(1), new[] { 0 }).Damage(w, 99, 0);
            Assert.False(w.Alive);
            Assert.False(Rules.WoundedOut(s));
            // Everyone else in: La Lata goes, not won.
            foreach (var x in Squad(s)) Events.ApplyEvent(s, new BoardedEvent { Unit = x.Id });
            Assert.True(Rules.AllAboard(s));
            Assert.False(Rules.EvacWins(s));
            Assert.False(Rules.RunsRescue(s));
            new Sim(s, new Rng(1), new[] { 0 }).CheckOutcome();
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            var had = c.Soldiers.Count;
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.Null(report.Run!.Reward);
            Assert.Equal(had, c.Soldiers.Count);
        }

        [Fact]
        public void TheWoundedAloneIsNoSquadItIsOver()
        {
            var (_, s) = Wounded();
            var u = Soldier(s);
            Carried(s, u);
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.SetDown, tile: Beside(s, u.Pos))).Ok);
            var sim = new Sim(s, new Rng(1), new[] { 0 });
            foreach (var x in Squad(s)) sim.Damage(x, 99, 0);
            sim.CheckOutcome();
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            Assert.Equal(EndReasons.Wiped, s.OutcomeReason);
        }

        // ------------------------------------------------------------------ never stuck

        [Fact]
        public void TheyAreAlwaysWithinReachAndLaLataFromThem()
        {
            // Producto, 19/11: never a stage that can't end. Every site a Rescate is on, 12 maps each: the squad walks to a
            // tile beside them it carries them from, and from there on to La Lata, on foot (no door locked, no cell).
            var sites = new[] { Places.City, Places.Town, Places.Estate, Places.Factory, Places.Roadside, Places.Country, Places.Hospital, Places.Police, Places.Plaza, Places.Offices, Places.Shed, Places.Camping };
            foreach (var site in sites)
                foreach (var seed in Enumerable.Range(1, 12))
                {
                    var (_, s) = Wounded(seed, site);
                    var w = Hurt(s);
                    var u = Soldier(s);
                    var reach = Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost;
                    var from = Grid.Dirs8.Select((d) => new Pos { X = w.Pos.X + d.X, Y = w.Pos.Y + d.Y, Z = w.Pos.Z })
                        .Where((q) => Grid.InBounds(s, q.X, q.Y) && reach.ContainsKey(Grid.NodeOf(s, q)) && Abilities.CarryTarget(s, u, q) == w).ToList();
                    Assert.True(from.Count > 0, $"{site} {seed}: nowhere to carry them from");
                    var was = u.Pos;
                    u.Pos = from[0];
                    var home = Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost;
                    u.Pos = was;
                    Assert.True(s.Mission.EvacZone.Any((v) => home.ContainsKey(Grid.NodeOf(s, new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }))), $"{site} {seed}: La Lata out of reach");
                    Assert.DoesNotContain(Grid.NodeOf(s, w.Pos), s.Mission.Locked ?? new List<int>());
                    Assert.NotEqual(true, w.Caged);
                }
        }
    }
}
