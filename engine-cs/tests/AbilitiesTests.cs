using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>packages/engine/test/abilities.test.ts, test by test.</summary>
    public class AbilitiesTests
    {
        private static readonly int[] Both = { 0, 1 };

        /// <summary>`ctx(seed, slot)`: both players connected.</summary>
        private static CommandResult Ctx(GameState s, Command cmd, int seed = 1, int slot = 0)
        {
            return Exec(s, cmd, slot, seed, Both);
        }

        private static GameState Field(int w = 20, int h = 20)
        {
            return StateFrom(Enumerable.Range(0, h).Select((_) => new string('.', w)).ToArray());
        }

        private static List<GameEvent> Run(GameState s, Command cmd, int seed = 1)
        {
            var r = Ctx(s, cmd, seed);
            if (!r.Ok) throw new Exception(r.Error);
            return r.Events!;
        }

        private static AbilityCommand Use(string unit, string ability, string? target = null, Pos? tile = null)
        {
            return Ability(unit, ability, target, tile);
        }

        /// <summary>A soldier with perks, plus a partner soldier that keeps the XCOM turn going.</summary>
        private static Unit Squad(GameState s, string id, string template, int x, int y, string[]? perks = null, Action<Unit>? extra = null)
        {
            if (!s.Units.Any((u) => u.Id == "keeper")) AddUnit(s, "keeper", TemplateIds.Specialist, 0, s.Height - 1, extra: (u) => u.Owner = 1);
            return AddUnit(s, id, template, x, y, extra: (u) =>
            {
                u.Perks = (perks ?? new string[0]).ToList();
                extra?.Invoke(u);
            });
        }

        private static void Has(IEnumerable<Modifier> mods, string label, int value)
        {
            Assert.Contains(mods, (m) => m.Label == label && m.Value == value);
        }

        private static Action<Unit> Tough => (u) =>
        {
            u.Hp = 99;
            u.MaxHp = 99;
        };

        // ------------------------------------------------------------ cooldowns and free actions

        [Fact(DisplayName = "cooldowns and free actions › a shot spends one action, and the sniper rifle both")]
        public void AShotSpendsOneActionAndTheSniperRifleBoth()
        {
            var s = Field();
            var rifle = Squad(s, "s1", TemplateIds.Grenadier, 2, 2);
            var sniper = Squad(s, "s2", TemplateIds.Sharpshooter, 2, 4);
            AddUnit(s, "a1", TemplateIds.Trooper, 8, 3, extra: Tough);
            Run(s, Use("s1", AbilityIds.Shoot, "a1"));
            Assert.Equal(1, rifle.Ap);
            Run(s, Use("s1", AbilityIds.Shoot, "a1"));
            Assert.Equal(0, rifle.Ap);
            // Done with s1, the partner plays one of theirs (playtest 2026-10-15, #9), and hands the turn back.
            Assert.Equal(1, s.Command);
            Assert.True(Ctx(s, new PassCommand(), 1, 1).Ok);
            Run(s, Use("s2", AbilityIds.Shoot, "a1"));
            Assert.Equal(0, sniper.Ap);
        }

        [Fact(DisplayName = "cooldowns and free actions › Salvo: a grenade no longer ends the turn")]
        public void SalvoAGrenadeNoLongerEndsTheTurn()
        {
            var s = Field();
            var u = Squad(s, "s1", TemplateIds.Grenadier, 2, 2, new[] { PerkIds.Salvo });
            AddUnit(s, "a1", TemplateIds.Trooper, 10, 2);
            Run(s, Use("s1", AbilityIds.Launch, tile: P(10, 2)));
            Assert.Equal(1, u.Ap);
        }

        // ------------------------------------------------------------ attack variants

        [Fact(DisplayName = "attack variants › Ráfaga shoots twice with −15 aim")]
        public void RafagaShootsTwiceWithMinus15Aim()
        {
            var s = Field();
            Squad(s, "s1", TemplateIds.Assault, 2, 2, new[] { PerkIds.RapidFire });
            AddUnit(s, "a1", TemplateIds.Trooper, 6, 2, extra: Tough);
            var shooter = s.Units.Find((u) => u.Id == "s1")!;
            var target = s.Units.Find((u) => u.Id == "a1")!;
            Has(Aim.PreviewShot(s, shooter, target, new ShotOptions { Ability = AbilityIds.RapidFire })!.Mods, "Ráfaga", -15);
            var events = Run(s, Use("s1", AbilityIds.RapidFire, "a1"));
            Assert.Equal(2, Of<ShotEvent>(events).Count);
            Assert.Equal(2, shooter.Ammo);
        }

        [Fact(DisplayName = "attack variants › Face Off fires the pistol at every enemy in sight")]
        public void FaceOffFiresThePistolAtEveryEnemyInSight()
        {
            var s = Field();
            Squad(s, "s1", TemplateIds.Sharpshooter, 2, 2, new[] { PerkIds.Faceoff });
            var xs = new[] { 5, 8, 11 };
            for (var i = 0; i < xs.Length; i++) AddUnit(s, $"a{i}", TemplateIds.Trooper, xs[i], 2, extra: Tough);
            var events = Run(s, Use("s1", AbilityIds.Faceoff));
            Assert.Equal(3, Of<ShotEvent>(events).Count);
        }

        // ------------------------------------------------------------ reactions

        [Fact(DisplayName = "reactions › Kill Zone keeps firing, once per enemy; plain overwatch fires once")]
        public void KillZoneKeepsFiringOncePerEnemy()
        {
            var s = Field();
            s.ActiveTeam = Teams.Alien;
            var sniper = AddUnit(s, "s1", TemplateIds.Sharpshooter, 2, 10, extra: (u) =>
            {
                u.Overwatch = true;
                u.KillZone = true;
                u.Ammo = 3;
            });
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 15, 2, extra: Tough);
            var b = AddUnit(s, "a2", TemplateIds.Trooper, 15, 18, extra: Tough);
            var sim = NewSim(s, 1, 0);
            sim.MoveAlong(a, new List<Pos> { P(14, 2), P(13, 2) }, 1);
            sim.MoveAlong(b, new List<Pos> { P(14, 18), P(13, 18) }, 1);
            Assert.Equal(2, Of<ShotEvent>(sim.Events).Count);
            Assert.True(sniper.Overwatch);
        }

        [Fact(DisplayName = "reactions › Shadowstep never draws reaction fire")]
        public void ShadowstepNeverDrawsReactionFire()
        {
            var s = Field();
            Squad(s, "s1", TemplateIds.Assault, 0, 10, new[] { PerkIds.Shadowstep });
            AddUnit(s, "a1", TemplateIds.Trooper, 10, 0, extra: (u) => u.Overwatch = true);
            var events = Run(s, Move("s1", 6, 10));
            Assert.DoesNotContain(events, (e) => e is ShotEvent);
        }

        [Fact(DisplayName = "reactions › Covering Fire shoots at an enemy who attacks")]
        public void CoveringFireShootsAtAnEnemyWhoAttacks()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Specialist, 2, 2, extra: (u) =>
            {
                u.Perks = new List<string> { PerkIds.CoveringFire };
                u.Overwatch = true;
            });
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 8, 2, extra: (u) =>
            {
                u.Hp = 99;
                u.MaxHp = 99;
                u.Ap = 2;
            });
            s.ActiveTeam = Teams.Alien;
            var sim = NewSim(s, 1, 0);
            sim.Perform(a, AbilityIds.Shoot, target: s.Units[0]);
            var shots = Of<ShotEvent>(sim.Events);
            Assert.Equal("s1", shots[0].Unit);
            Assert.True(shots[0].Reaction);
        }

        // ------------------------------------------------------------ support

        [Fact(DisplayName = "support › Pasar visita heals and cleanses everyone within 4, once a mission (Fase 9)")]
        public void PasarVisitaHealsAndCleansesEveryoneWithin4()
        {
            var s = Field();
            var medic = Squad(s, "s1", TemplateIds.Specialist, 2, 2, new[] { PerkIds.Restoration }, (u) => u.Charges = new Dictionary<string, int> { [ChargeIds.Restoration] = 1 });
            var ally = AddUnit(s, "s2", TemplateIds.Assault, 6, 2, extra: (u) =>
            {
                u.Hp = 2;
                u.Disoriented = 1;
                u.Panicked = true;
            });
            var far = AddUnit(s, "s3", TemplateIds.Assault, 7, 2, extra: (u) => u.Hp = 2);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            Run(s, Use("s1", AbilityIds.Restoration));
            Assert.Equal(4, ally.Hp);
            Assert.Equal(0, ally.Disoriented);
            Assert.False(ally.Panicked);
            Assert.Equal(2, far.Hp);
            medic.Ap = 2;
            Assert.False(Exec(s, Use("s1", AbilityIds.Restoration), 0, 1, new[] { 0 }).Ok);
        }

        // ------------------------------------------------------------ kills that pay back

        [Fact(DisplayName = "kills that pay back › Implacable: a killing blade strike gives an action back, once per turn")]
        public void ImplacableAKillingBladeStrikeGivesAnActionBack()
        {
            var s = Field();
            var u = Squad(s, "s1", TemplateIds.Assault, 2, 2, new[] { PerkIds.Implacable });
            AddUnit(s, "a1", TemplateIds.Trooper, 3, 2, extra: (x) =>
            {
                x.Hp = 1;
                x.MaxHp = 1;
            });
            AddUnit(s, "a2", TemplateIds.Trooper, 19, 19);
            for (var seed = 1; seed < 20 && !u.Refunded; seed++)
            {
                u.Ap = 2;
                var r = Ctx(s, Use("s1", AbilityIds.Slash, "a1"), seed);
                if (!r.Ok) break;
            }
            Assert.True(u.Refunded);
            // The Tajo costs one action (playtest 16/10), and the kill gives it back.
            Assert.Equal(2, u.Ap);
        }

        [Fact(DisplayName = "kills that pay back › Death From Above: a kill from higher ground gives an action back")]
        public void DeathFromAboveAKillFromHigherGroundGivesAnActionBack()
        {
            var rows = Enumerable.Range(0, 10).Select((_) => new string('.', 10)).ToArray();
            var heights = Enumerable.Range(0, 10).Select((y) => y < 3 ? "1111000000" : "0000000000").ToArray();
            var s = StateFrom(rows, heights);
            var u = Squad(s, "s1", TemplateIds.Sharpshooter, 1, 1, new[] { PerkIds.DeathFromAbove });
            AddUnit(s, "a1", TemplateIds.Trooper, 7, 7, extra: (x) =>
            {
                x.Hp = 1;
                x.MaxHp = 1;
            });
            AddUnit(s, "a2", TemplateIds.Trooper, 9, 0);
            for (var seed = 1; seed < 20 && !u.Refunded; seed++)
            {
                u.Ap = 2;
                u.Ammo = 3;
                var target = s.Units.Find((x) => x.Id == "a1")!;
                if (!target.Alive) break;
                Ctx(s, Use("s1", AbilityIds.Shoot, "a1"), seed);
            }
            Assert.True(u.Refunded);
        }

        // ------------------------------------------------------------ psionics

        private static (GameState s, Unit soldier, Unit sectoid) SectoidField()
        {
            var s = Field();
            s.ActiveTeam = Teams.Alien;
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 2, 2, extra: (u) => u.Will = 40);
            AddUnit(s, "s2", TemplateIds.Specialist, 2, 6);
            var sectoid = AddUnit(s, "a1", TemplateIds.Sectoid, 9, 2, extra: (u) =>
            {
                u.PodId = "p1";
                u.Ap = 2;
            });
            s.Pods.Add(new Pod { Id = "p1", Active = true, UnitIds = new List<string> { "a1" }, Patrol = new List<Pos>(), PatrolIndex = 0 });
            return (s, soldier, sectoid);
        }

        [Fact(DisplayName = "psionics › will resists mindspin; machines are immune")]
        public void WillResistsMindspinMachinesAreImmune()
        {
            var (s, soldier, _) = SectoidField();
            Assert.Equal(70, Abilities.PsiChance(soldier));
            var strong = Events.CloneUnit(soldier);
            strong.Will = 60;
            Assert.Equal(50, Abilities.PsiChance(strong));
            Assert.Equal(0, Abilities.PsiChance(AddUnit(s, "a9", TemplateIds.Mec, 0, 0)));
        }

        [Fact(DisplayName = "psionics › the scream disorients or panics but never takes control, and every zombie within 16 tiles hears it")]
        public void TheScreamDisorientsOrPanicsButNeverTakesControl()
        {
            var effects = new HashSet<string>();
            for (var seed = 1; seed < 200; seed++)
            {
                var (s, soldier, sectoid) = SectoidField();
                var near = AddUnit(s, "a2", TemplateIds.Zombie, 18, 2, extra: (u) => u.PodId = "p2");
                var far = AddUnit(s, "a3", TemplateIds.Zombie, 19, 19, extra: (u) => u.PodId = "p2");
                s.Pods.Add(new Pod { Id = "p2", Active = false, UnitIds = new List<string> { "a2", "a3" }, Patrol = new List<Pos>(), PatrolIndex = 0 });
                var sim = NewSim(s, seed, 0);
                sim.Perform(sectoid, AbilityIds.Mindspin, target: soldier);
                var psi = Of<PsiEvent>(sim.Events)[0];
                if (!string.IsNullOrEmpty(psi.Effect)) effects.Add(psi.Effect!);
                Assert.Equal(Teams.Xcom, soldier.Team);
                var noise = Of<NoiseEvent>(sim.Events)[0];
                SamePlace(sectoid.Pos, noise.Tile);
                Assert.Equal(16, noise.Radius);
                Assert.Equal("a1", noise.By);
                Assert.Equal(new[] { "a2" }, noise.Heard);
                SamePlace(sectoid.Pos, near.Heard);
                Assert.Null(far.Heard);
                // Heard, not seen: the pod stays unaware.
                Assert.False(s.Pods[1].Active);
            }
            Assert.Equal(new[] { PsiEffects.Disoriented, PsiEffects.Panicked }, effects.OrderBy((e) => e, StringComparer.Ordinal));
        }

        [Fact(DisplayName = "psionics › panic costs the next turn; disorientation lowers aim and forbids explosives")]
        public void PanicCostsTheNextTurnDisorientationLowersAim()
        {
            var (s, soldier, _) = SectoidField();
            Events.ApplyEvent(s, new PsiEvent { Unit = "a1", Target = "s1", Success = true, Effect = PsiEffects.Panicked, Chance = 70 });
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = 2 });
            Assert.Equal(0, soldier.Ap);
            Assert.True(soldier.Hunkered);
            Events.ApplyEvent(s, new PsiEvent { Unit = "a1", Target = "s2", Success = true, Effect = PsiEffects.Disoriented, Chance = 70 });
            var spec = s.Units.Find((u) => u.Id == "s2")!;
            spec.Ap = 2;
            Assert.Matches("desorientación", Abilities.AbilityBlocker(s, spec, AbilityIds.Grenade));
            Has(Aim.PreviewShot(s, spec, s.Units.Find((u) => u.Id == "a1")!)!.Mods, "Desorientación", -20);
        }

        [Fact(DisplayName = "psionics › sectoids raise corpses as zombies that join their pod and claw at soldiers")]
        public void SectoidsRaiseCorpsesAsZombies()
        {
            var (s, _, sectoid) = SectoidField();
            var corpse = AddUnit(s, "a5", TemplateIds.Trooper, 7, 4, extra: (u) =>
            {
                u.Alive = false;
                u.Hp = 0;
            });
            Assert.Contains(corpse, Abilities.AbilityTargets(s, sectoid, AbilityIds.Reanimate));
            var host = Events.CloneState(s);
            var sim = NewSim(host, 1, 0);
            sim.Perform(host.Units.Find((u) => u.Id == "a1")!, AbilityIds.Reanimate, target: host.Units.Find((u) => u.Id == "a5")!);
            var zombie = host.Units.Find((u) => u.Template == TemplateIds.Zombie)!;
            Assert.True(zombie.Alive);
            Assert.Contains(zombie.Id, host.Pods[0].UnitIds);
            Assert.True(host.Units.Find((u) => u.Id == "a5")!.Reanimated);
            foreach (var e in sim.Events) Events.ApplyEvent(s, e);
            Assert.Equal(Json(host), Json(s));
            // Next alien turn, the zombie shambles over and attacks.
            zombie.Pos = P(3, 3);
            var turn = NewSim(host, 2, 0);
            turn.RunAlienTurn();
            Assert.Contains(turn.Events, (e) => e is ShotEvent shot && shot.Unit == zombie.Id && shot.Ability == AbilityIds.Claw);
        }

        /// <summary>toEqual on states: their JSON (null and missing alike).</summary>
        private static string Json(GameState s)
        {
            return JsonSerializer.Serialize(s, Options);
        }

        // ------------------------------------------------------------ equipment in the field

        [Fact(DisplayName = "equipment in the field › smoke makes everyone inside harder to hit and clears after two turns")]
        public void SmokeMakesEveryoneInsideHarderToHit()
        {
            var s = Field();
            Squad(s, "s1", TemplateIds.Assault, 2, 2, null, (u) =>
            {
                u.Gear = new List<string> { ItemIds.SmokeGrenade };
                u.Charges = new Dictionary<string, int> { [ChargeIds.Grenade] = 1, [ChargeIds.Smoke] = 1 };
            });
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 10, 2);
            var ally = AddUnit(s, "s2", TemplateIds.Specialist, 4, 4);
            Run(s, Use("s1", AbilityIds.Smoke, tile: P(4, 4)));
            Has(Aim.PreviewShot(s, a, ally)!.Mods, "Humo", -20);
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = 2 });
            Assert.Single(s.Smoke);
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = 3 });
            Assert.Empty(s.Smoke);
        }

        [Fact(DisplayName = "equipment in the field › a flashbang disorients the enemies caught in it, without damage")]
        public void AFlashbangDisorientsTheEnemiesCaughtInIt()
        {
            var s = Field();
            Squad(s, "s1", TemplateIds.Specialist, 2, 2, null, (u) =>
            {
                u.Gear = new List<string> { ItemIds.Flashbang };
                u.Charges = new Dictionary<string, int> { [ChargeIds.Grenade] = 1, [ChargeIds.Flashbang] = 1 };
            });
            var a = AddUnit(s, "a1", TemplateIds.Trooper, 9, 2);
            var mec = AddUnit(s, "a2", TemplateIds.Mec, 9, 3);
            Run(s, Use("s1", AbilityIds.Flashbang, tile: P(9, 2)));
            Assert.Equal(1, a.Disoriented);
            Assert.Equal(a.MaxHp, a.Hp);
            Assert.Equal(0, mec.Disoriented);
        }

        [Fact(DisplayName = "equipment in the field › a first-aid kit heals an adjacent ally; armour-piercing rounds ignore armour")]
        public void AFirstAidKitHealsAnAdjacentAlly()
        {
            var s = Field();
            Squad(s, "s1", TemplateIds.Assault, 2, 2, null, (u) =>
            {
                u.Gear = new List<string> { ItemIds.FirstAid };
                u.Charges = new Dictionary<string, int> { [ChargeIds.Grenade] = 1, [ChargeIds.FirstAid] = 1 };
            });
            var hurt = AddUnit(s, "s2", TemplateIds.Sharpshooter, 3, 2, extra: (u) => u.Hp = 1);
            AddUnit(s, "a1", TemplateIds.Trooper, 19, 19);
            Run(s, Use("s1", AbilityIds.FirstAid, "s2"));
            Assert.Equal(5, hurt.Hp);

            // Entre los dos (board.ts): 2 more on the other player's soldiers.
            var b = Field();
            Squad(b, "s1", TemplateIds.Assault, 2, 2, new[] { PerkIds.Bridge }, (u) =>
            {
                u.Gear = new List<string> { ItemIds.FirstAid };
                u.Charges = new Dictionary<string, int> { [ChargeIds.Grenade] = 1, [ChargeIds.FirstAid] = 1 };
            });
            var theirs = AddUnit(b, "s2", TemplateIds.Sharpshooter, 3, 2, extra: (u) =>
            {
                u.Hp = 1;
                u.MaxHp = 10;
                u.Owner = 1;
            });
            AddUnit(b, "a1", TemplateIds.Trooper, 19, 19);
            Run(b, Use("s1", AbilityIds.FirstAid, "s2"));
            Assert.Equal(7, theirs.Hp);

            var t = Field();
            var tmp = AddUnit(t, "tmp", TemplateIds.Specialist, 0, 0);
            var gunner = AddUnit(t, "g1", TemplateIds.Specialist, 2, 2, extra: (u) => u.Mods = Content.AddMods(tmp.Mods, new UnitMods { Pierce = 1 }));
            t.Units = t.Units.Where((u) => u.Id != "tmp").ToList();
            var mec = AddUnit(t, "a1", TemplateIds.Mec, 4, 2);
            var sim = NewSim(t, 1, 0);
            sim.Damage(mec, 3, 0, gunner, gunner.Mods.Pierce);
            Assert.Equal(Content.Templates[TemplateIds.Mec].Hp - 3, mec.Hp);
        }
    }
}
