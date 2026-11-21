using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Golpe silencioso (Arturo, 16/11, chosen in the box): with a blade, from behind one of the dead that hasn't seen you, it
    /// always kills, silently; only a Zombi or a Corredor, and of the living a Saqueador or a Cazador (cuadro 99), never the
    /// Cabecilla; never with the chainsaw (Gameplay). Abilities.TakedownRefusal,
    /// Rules.Takedown. And how one gets there: while the squad is hidden, the dead don't notice whoever comes up behind or
    /// beside them, out of their cone (Los.InNoticeCone: «por detrás de un zombi que no te ha visto»).
    /// </summary>
    public class TakedownTests
    {
        /// <summary>East, as Los.FacingTo has it; and west.</summary>
        private const double East = 0;
        private static readonly double West = System.Math.PI;

        /// <summary>
        /// An open field 14 × 7, the squad hidden: a soldier with a knife at (5, 3) and, beside it at (6, 3), one of the dead
        /// of `template`, unaware and looking east, away from it. Another far off and looking away keeps the mission going.
        /// </summary>
        private static GameState Behind(out Unit soldier, out Unit dead, string template = TemplateIds.Zombie, double facing = East, bool alert = false, int x = 6)
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 14), 7).ToArray());
            s.Concealed = true;
            soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 3, extra: (u) => u.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle });
            dead = AddUnit(s, "a1", template, x, 3, extra: (u) => u.Facing = facing);
            Pod(s, "p1", new[] { dead }, active: alert);
            var far = AddUnit(s, "a9", TemplateIds.Zombie, 13, 6, extra: (u) => u.Facing = East);
            Pod(s, "p9", new[] { far }, active: false);
            return s;
        }

        private static CommandResult Takedown(GameState s) => Exec(s, Ability("s1", AbilityIds.Takedown, target: "a1"));

        [Fact(DisplayName = "takedown › from behind, a Zombi dies at once: no noise, the squad still hidden, its group still unaware")]
        public void FromBehindAZombieDies()
        {
            var s = Behind(out var u, out var z);
            Assert.Contains(AbilityIds.Takedown, Abilities.AbilitiesOf(u));
            Assert.Equal(AbilityIds.Takedown, Abilities.AbilitiesOf(u)[Abilities.AbilitiesOf(u).IndexOf(AbilityIds.Slash) + 1]);
            Assert.Equal(new[] { "a1" }, Abilities.AbilityTargets(s, u, AbilityIds.Takedown).Select((t) => t.Id));
            var r = Takedown(s);
            Assert.True(r.Ok, r.Error);
            Assert.False(z.Alive);
            Assert.Empty(Of<NoiseEvent>(r.Events));
            Assert.Empty(Of<ConcealmentBrokenEvent>(r.Events));
            Assert.Empty(Of<PodActivatedEvent>(r.Events));
            Assert.True(s.Concealed);
            Assert.False(s.Pods.Single((p) => p.Id == "p1").Active);
            // A blade's kill, with no roll: the shot at 100, its damage, the death.
            var shot = Of<ShotEvent>(r.Events).Single();
            Assert.Equal((u.Id, z.Id, AbilityIds.Takedown, ShotResults.Hit, 100.0, 0, false), (shot.Unit, shot.Target, shot.Ability, shot.Result, shot.Chance, shot.Roll, shot.Reaction));
            Assert.Equal(new[] { "abilityUsed", "shot", "damaged", "died" }, Tags(r.Events).Where((t) => t == "abilityUsed" || t == "shot" || t == "damaged" || t == "died"));
            Assert.Equal(z.Id, Of<DiedEvent>(r.Events).Single().Unit);
            Assert.Equal(u.Id, Of<DiedEvent>(r.Events).Single().By);
            Assert.Equal((1, 1), (u.Kills, u.Ap));
            Assert.Equal(1, u.Deeds![DeedIds.BladeKill]);
        }

        [Fact(DisplayName = "takedown › a Corredor too, and with any blade but the chainsaw")]
        public void ACorredorTooAndAnyBlade()
        {
            var s = Behind(out var u, out var x, TemplateIds.Xenoid);
            u.Arms!.Melee = FoundWeaponIds.Katana;
            var r = Takedown(s);
            Assert.True(r.Ok, r.Error);
            Assert.False(x.Alive);
            Assert.True(s.Concealed);
        }

        [Fact(DisplayName = "takedown › not with the chainsaw")]
        public void NotWithTheChainsaw()
        {
            var s = Behind(out var u, out var z);
            u.Arms!.Melee = FoundWeaponIds.Chainsaw;
            u.Charges[ChargeIds.Fuel] = Content.ChainsawFuel;
            Assert.Contains(AbilityIds.Takedown, Abilities.AbilitiesOf(u));
            var r = Takedown(s);
            Assert.False(r.Ok);
            Assert.Equal("Con la motosierra no hay golpe silencioso.", r.Error);
            Assert.True(z.Alive);
        }

        [Fact(DisplayName = "takedown › refused if it looks at the soldier, or looks all round")]
        public void RefusedIfItSeesTheSoldier()
        {
            var s = Behind(out _, out var z, facing: West);
            var r = Takedown(s);
            Assert.Equal((false, "Te tiene delante: ponte detrás o a un lado."), (r.Ok, r.Error));
            Assert.True(z.Alive);
            z.Facing = null;
            Assert.Equal("Te tiene delante: ponte detrás o a un lado.", Takedown(s).Error);
        }

        [Fact(DisplayName = "takedown › refused if its group is awake")]
        public void RefusedIfItsGroupIsAwake()
        {
            var s = Behind(out _, out var z, alert: true);
            var r = Takedown(s);
            Assert.Equal((false, "Ya está alerta."), (r.Ok, r.Error));
            Assert.True(z.Alive);
        }

        [Fact(DisplayName = "takedown › refused if it isn't beside the soldier")]
        public void RefusedIfNotBeside()
        {
            var s = Behind(out _, out var z, x: 7);
            var r = Takedown(s);
            Assert.Equal((false, "Tiene que estar a tu lado."), (r.Ok, r.Error));
            Assert.True(z.Alive);
        }

        [Fact(DisplayName = "takedown › refused against the Hinchado and the Acorazado")]
        public void RefusedAgainstTheHinchadoAndTheAcorazado()
        {
            foreach (var template in new[] { TemplateIds.Bloater, TemplateIds.Mec })
            {
                var s = Behind(out _, out var b, template);
                var r = Takedown(s);
                Assert.Equal((false, "Solo contra Zombis, Corredores, Saqueadores y Cazadores."), (r.Ok, r.Error));
                Assert.True(b.Alive);
            }
        }

        [Fact(DisplayName = "takedown › of the living, the Saqueador and the Cazador unaware die at once; never the Cabecilla (cuadro 99)")]
        public void OfTheLivingTheSaqueadorAndTheCazadorNotTheCabecilla()
        {
            foreach (var template in new[] { TemplateIds.Trooper, TemplateIds.Lancer })
            {
                var s = Behind(out _, out var l, template);
                var r = Takedown(s);
                Assert.True(r.Ok, r.Error);
                Assert.False(l.Alive);
                Assert.Empty(Of<NoiseEvent>(r.Events));
                Assert.True(s.Concealed);
            }
            var o = Behind(out _, out var officer, TemplateIds.Officer);
            var refused = Takedown(o);
            Assert.Equal((false, "Contra el Cabecilla no hay golpe silencioso."), (refused.Ok, refused.Error));
            Assert.True(officer.Alive);
            // Awake, not one of them.
            var a = Behind(out _, out var awake, TemplateIds.Trooper, alert: true);
            Assert.Equal((false, "Ya está alerta."), (Takedown(a).Ok, Takedown(a).Error));
            Assert.True(awake.Alive);
        }

        [Fact(DisplayName = "takedown › hidden, a soldier walks up behind one of the dead unnoticed, and takes it down")]
        public void WalksUpBehindUnnoticed()
        {
            var s = Behind(out var u, out var z);
            u.Pos = P(2, 3);
            var r = Exec(s, Move("s1", 5, 3));
            Assert.True(r.Ok, r.Error);
            Assert.Empty(Of<PodActivatedEvent>(r.Events));
            Assert.True(s.Concealed);
            // The rules' eye over it (Los.Watchers) doesn't count it either.
            Assert.DoesNotContain(z, Los.Watchers(s, u, u.Pos));
            r = Takedown(s);
            Assert.True(r.Ok, r.Error);
            Assert.False(z.Alive);
            Assert.True(s.Concealed);
        }

        [Fact(DisplayName = "takedown › hidden, a soldier walks up behind a Saqueador and a Cazador unnoticed, and takes them down (Red's R15)")]
        public void WalksUpBehindTheLivingUnnoticed()
        {
            foreach (var template in new[] { TemplateIds.Trooper, TemplateIds.Lancer })
            {
                var s = Behind(out var u, out var l, template);
                u.Pos = P(2, 3);
                var r = Exec(s, Move("s1", 5, 3));
                Assert.True(r.Ok, r.Error);
                Assert.Empty(Of<PodActivatedEvent>(r.Events));
                Assert.True(s.Concealed);
                Assert.DoesNotContain(l, Los.Watchers(s, u, u.Pos));
                r = Takedown(s);
                Assert.True(r.Ok, r.Error);
                Assert.False(l.Alive);
                Assert.True(s.Concealed);
            }
            // In front of one, it wakes, as ever; once the squad is found, beside it too.
            var f = Behind(out var v, out var w, TemplateIds.Trooper, facing: West);
            v.Pos = P(2, 3);
            Assert.NotEmpty(Of<PodActivatedEvent>(Exec(f, Move("s1", 5, 3)).Events));
            var g = Behind(out var x, out var y, TemplateIds.Trooper);
            g.Concealed = false;
            Assert.Contains(y, Los.Watchers(g, x, x.Pos));
        }

        [Fact(DisplayName = "takedown › walking up in front of one of the dead wakes its group, as before")]
        public void WalkingUpInFrontWakesIt()
        {
            var s = Behind(out var u, out var z);
            u.Pos = P(11, 3);
            var r = Exec(s, Move("s1", 7, 3));
            Assert.True(r.Ok, r.Error);
            Assert.Contains(Of<PodActivatedEvent>(r.Events), (e) => e.Pod == "p1");
            Assert.False(s.Concealed);
        }

        [Fact(DisplayName = "takedown › one of the dead on its round that walks up to a hidden soldier from the front spots him")]
        public void APatrolFromTheFrontSpotsHim()
        {
            // At night and hiding, it notices him only beside him (6 × 0.5 × 0.5 = 1.5 tiles).
            var s = Behind(out var u, out var z, facing: West, x: 9);
            s.Mission.Night = true;
            u.Hidden = true;
            var sim = NewSim(s);
            sim.MoveAlong(z, new System.Collections.Generic.List<Pos> { P(8, 3), P(7, 3), P(6, 3) }, 0);
            Assert.Contains(Of<PodActivatedEvent>(sim.Events), (e) => e.Pod == "p1" && e.Cause == PodCauses.Patrol);
            Assert.Single(Of<ConcealmentBrokenEvent>(sim.Events));
            SamePlace(P(6, 3), z.Pos);
            Assert.False(s.Concealed);
        }

        [Fact(DisplayName = "takedown › once the squad is found, the dead notice whoever is beside them whichever way they look, as before")]
        public void FoundTheyNoticeWhoeverIsBeside()
        {
            var s = Behind(out var u, out var z);
            s.Concealed = false;
            Assert.Contains(z, Los.Watchers(s, u, u.Pos));
            u.Pos = P(2, 3);
            var r = Exec(s, Move("s1", 5, 3));
            Assert.True(r.Ok, r.Error);
            Assert.Contains(Of<PodActivatedEvent>(r.Events), (e) => e.Pod == "p1");
        }
    }
}
