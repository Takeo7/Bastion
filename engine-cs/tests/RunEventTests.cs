using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The events of the runs (H4, .dev-snapshots/lore/runs/SUCESOS.md; Content.RunEvents, Lore.RunEvents): Lore's ten, by
    /// their ids; drawn from the run's seed, none twice; what each kind of consequence does (supplies paid and won, one hurt
    /// at random but never killed, everyone hurt or healed, gear into the packs or lost, a modifier for the next fight,
    /// someone who joins); and what StopState.result says for Lore's line.
    /// </summary>
    public class RunEventTests
    {
        /// <summary>A campaign alone at an event (`id`), its squad with this much health each.</summary>
        private static (CampaignState C, StopState Stop) At(string id, int? seats = null)
        {
            var c = Solo();
            if (seats != null) c.Upgrades![UpgradeIds.Squad] = seats.Value;
            GoTo(c, NodeKinds.Event);
            var stop = c.Run!.Stop!;
            stop.Event = id;
            return (c, stop);
        }

        private static int Supplies(CampaignState c) => Squad(c).Sum((s) => Gears.Supplies(s.Kit!.Pack));

        [Fact]
        public void LoresTenByTheirIds()
        {
            Assert.Equal(10, Content.RunEvents.Count);
            Assert.Equal(Content.RunEvents.Keys.OrderBy((x) => x), Content.RunEventIds.OrderBy((x) => x));
            foreach (var e in Content.RunEvents.Values)
            {
                Assert.True(Lore.RunEvents.ContainsKey(e.Id), e.Id);
                Assert.Equal(2, e.Options.Count);
                // Each option says itself by its button, with Content's figures.
                foreach (var o in e.Options)
                {
                    var said = Lore.RunEventEffects(o.Supplies, o.HurtOne, o.HurtAll, o.HealAll, o.GearKind, o.GearType, o.GearCount, o.Modifier, o.Survivor, o.SurvivorHalf);
                    Assert.False(string.IsNullOrEmpty(said));
                    if (o.GearKind != null) Assert.True(Content.IsGear(o.GearKind, o.GearType), $"{e.Id}: {o.GearKind} {o.GearType}");
                    if (o.Modifier != null) Assert.Contains(o.Modifier, Modifiers.All);
                }
            }
            // A few of SUCESOS.md's figures.
            Assert.Equal(-30, Content.RunEvents["control"].Options[0].Supplies);
            Assert.Equal((Modifiers.Awake, 25), (Content.RunEvents["gasolinera"].Options[0].Modifier!, Content.RunEvents["gasolinera"].Options[0].Supplies));
            Assert.True(Content.RunEvents["herido"].Options[0].SurvivorHalf);
            Assert.Equal((GearKinds.Item, ItemIds.FragGrenade, 2), (Content.RunEvents["caja-atada"].Options[0].GearKind!, Content.RunEvents["caja-atada"].Options[0].GearType!, Content.RunEvents["caja-atada"].Options[0].GearCount));
        }

        [Fact]
        public void DrawnFromTheRunsSeedNoneTwice()
        {
            List<string> Draws(int seed)
            {
                var c = Solo();
                // The tests' commands all roll Rng(7): the run's seed by hand.
                Start(c).Seed = seed;
                var out_ = new List<string>();
                for (var i = 0; i < 10; i++)
                {
                    var run = c.Run!;
                    run.At = null;
                    run.Phase = RunPhases.Choose;
                    run.Stop = null;
                    GoTo(c, NodeKinds.Event);
                    out_.Add(c.Run!.Stop!.Event!);
                }
                return out_;
            }
            var a = Draws(1);
            Assert.Equal(10, a.Distinct().Count());
            // The same run, the same events.
            Assert.Equal(a, Draws(1));
            Assert.NotEqual(a, Draws(2));
        }

        [Fact]
        public void WhatItCostsIsPaidOrRefused()
        {
            var (c, stop) = At("control");
            Refused(Lore.RunEventShort(30), c, new AnswerStopCommand { Option = 0 });
            InPack(c, Squad(c)[0], GearKinds.Supplies, null, 0, 0, 20);
            Assert.Equal(Lore.RunEventShort(10), Runs.AnswerStopRefusal(c, 0));
            InPack(c, Squad(c)[1], GearKinds.Supplies, null, 0, 0, 20);
            Ok(c, new AnswerStopCommand { Option = 0 });
            Assert.Equal(10, Supplies(c));
            Assert.Equal("control.0", stop.Outcome);
            Assert.Equal(("control", 0), (stop.Result!.Event, stop.Result.Option));
            // The other way: everyone loses 2, never below 1.
            var (d, _) = At("control");
            var hurt = Squad(d)[0];
            d.Run!.Hp[hurt.Id] = 2;
            Ok(d, new AnswerStopCommand { Option = 1 });
            Assert.Equal(1, d.Run.Hp[hurt.Id]);
            Assert.All(Squad(d).Skip(1), (s) => Assert.Equal(Runs.MaxHp(s) - 2, d.Run.Hp[s.Id]));
        }

        [Fact]
        public void OneAtRandomButNeverKilled()
        {
            var (c, stop) = At("puente");
            var squad = Squad(c);
            foreach (var s in squad) c.Run!.Hp[s.Id] = 3;
            var strong = squad[2];
            c.Run!.Hp[strong.Id] = 8;
            Ok(c, new AnswerStopCommand { Option = 0 });
            Assert.Equal(5, c.Run.Hp[strong.Id]);
            Assert.Equal((strong.Id, strong.Name), (stop.Result!.Soldier, stop.Result.SoldierName));
            Assert.All(squad.Where((s) => s != strong), (s) => Assert.Equal(3, c.Run.Hp[s.Id]));
            // Nobody with more than it takes: nobody.
            var (d, other) = At("puente");
            foreach (var s in Squad(d)) d.Run!.Hp[s.Id] = 3;
            Ok(d, new AnswerStopCommand { Option = 0 });
            Assert.Null(other.Result!.Soldier);
            Assert.All(Squad(d), (s) => Assert.Equal(3, d.Run!.Hp[s.Id]));
            // The same run, the same one.
            var (e, again) = At("puente");
            foreach (var s in Squad(e)) e.Run!.Hp[s.Id] = 6;
            Ok(e, new AnswerStopCommand { Option = 0 });
            var (f, twice) = At("puente");
            foreach (var s in Squad(f)) f.Run!.Hp[s.Id] = 6;
            Ok(f, new AnswerStopCommand { Option = 0 });
            Assert.Equal(again.Result!.Soldier, twice.Result!.Soldier);
        }

        [Fact]
        public void GearGoesToTheOneItFellToAndIsLostWithoutRoom()
        {
            var (c, stop) = At("farmacia");
            Ok(c, new AnswerStopCommand { Option = 0 });
            var hit = c.Soldiers.First((s) => s.Id == stop.Result!.Soldier);
            Assert.Contains(hit.Kit!.Pack.Items, (p) => p.Gear.Type == ItemIds.FirstAid);
            Assert.Null(stop.Result!.Lost);
            Assert.Equal(Lore.RunEvents["farmacia"].Options[0].Result(hit.Name, null), c.Log[0].Text);
            // Every pack full: lost, and said.
            var (d, full) = At("caja-atada");
            foreach (var s in Squad(d)) for (var y = 0; y < s.Kit!.Pack.H; y++) for (var x = 0; x < s.Kit.Pack.W; x++) InPack(d, s, GearKinds.Mags, null, x, y, 5);
            Ok(d, new AnswerStopCommand { Option = 0 });
            Assert.True(full.Result!.Lost);
            Assert.EndsWith(Lore.RunEventLost, d.Log[0].Text);
            Assert.DoesNotContain(Squad(d).SelectMany((s) => s.Kit!.Pack.Items), (p) => p.Gear.Type == ItemIds.FragGrenade);
            // Supplies won, and everyone a little hurt.
            var (e, _) = At("supermercado");
            Ok(e, new AnswerStopCommand { Option = 0 });
            Assert.Equal(30, Supplies(e));
            Assert.All(Squad(e), (s) => Assert.Equal(Runs.MaxHp(s) - 1, e.Run!.Hp[s.Id]));
            // Paid and given: a pistol for 15 §.
            var (f, _) = At("herido");
            InPack(f, Squad(f)[0], GearKinds.Supplies, null, 0, 0, 15);
            Ok(f, new AnswerStopCommand { Option = 1 });
            Assert.Equal(0, Supplies(f));
            Assert.Contains(Squad(f).SelectMany((s) => s.Kit!.Pack.Items), (p) => p.Gear.Type == FoundWeaponIds.Pistol);
        }

        [Fact]
        public void AModifierForTheNextFight()
        {
            var (c, _) = At("campamento");
            var run = c.Run!;
            foreach (var s in Squad(c)) run.Hp[s.Id] = 1;
            Ok(c, new AnswerStopCommand { Option = 0 });
            Assert.All(Squad(c), (s) => Assert.Equal(System.Math.Min(Runs.MaxHp(s), 3), run.Hp[s.Id]));
            Assert.Equal(Modifiers.Night, run.Pending);
            Ok(c, new LeaveStopCommand());
            // A stop next: it waits for a fight.
            GoTo(c, NodeKinds.Trade);
            Assert.Equal(Modifiers.Night, run.Pending);
            Ok(c, new LeaveStopCommand());
            GoTo(c, NodeKinds.Normal);
            var node = RunMap.Node(run, run.At)!;
            Assert.Null(run.Pending);
            Assert.Equal(Modifiers.Night, node.Added);
            var s0 = new EngineCampaignRules().LaunchMission(c, new Rng(3))!.Value.state;
            Assert.True(s0.Mission.Night);
            Assert.Equal(Modifiers.Night, s0.Run!.Added);
            // It makes the supplies richer, as any modifier does.
            Assert.Equal(1.5 * (1 + 0.25 * (node.Row - 1)), RunLoot.SuppliesScale(s0.Run));
        }

        [Fact]
        public void SomeoneJoins()
        {
            // With a seat: in the squad, at half health.
            var (c, stop) = At("herido", seats: 1);
            var before = c.Soldiers.Count;
            Ok(c, new AnswerStopCommand { Option = 0 });
            Assert.Equal(before + 1, c.Soldiers.Count);
            var s = c.Soldiers.Last();
            Assert.Equal((s.Id, s.Name), (stop.Result!.Survivor, stop.Result.SurvivorName));
            Assert.True(stop.Result.InSquad);
            Assert.Contains(s.Id, c.Run!.Squad);
            Assert.Equal((Runs.MaxHp(s) + 1) / 2, c.Run.Hp[s.Id]);
            Assert.Equal(FoundWeaponIds.Knife, s.Kit!.Melee!.Type);
            Assert.Equal(Lore.RunEvents["herido"].Options[0].Result(null, s.Name), c.Log[0].Text);
            // At full health, and the night for the next fight; without a seat, at the base.
            var (d, other) = At("radio");
            Ok(d, new AnswerStopCommand { Option = 0 });
            var t = d.Soldiers.Last();
            Assert.Equal(t.Id, other.Result!.Survivor);
            Assert.False(other.Result.InSquad);
            Assert.DoesNotContain(t.Id, d.Run!.Squad);
            Assert.Equal(Modifiers.Night, d.Run.Pending);
            var (e, _) = At("radio", seats: 1);
            Ok(e, new AnswerStopCommand { Option = 0 });
            Assert.Equal(Runs.MaxHp(e.Soldiers.Last()), e.Run!.Hp[e.Soldiers.Last().Id]);
        }

        [Fact]
        public void WithBothTheAnswerIsVotedByItsButton()
        {
            var c = Two();
            GoTo(c, NodeKinds.Event);
            var stop = c.Run!.Stop!;
            stop.Event = "perro";
            Ok(c, new AnswerStopCommand { Option = 1 }, 1, Both);
            Assert.Contains(Lore.RunEvents["perro"].Options[1].Label, c.Proposal!.Text);
            Ok(c, new VoteCommand { Accept = true }, 0, Both);
            Assert.Equal("perro.1", stop.Outcome);
            // What can't be paid is refused when proposed.
            var d = Two();
            GoTo(d, NodeKinds.Event);
            d.Run!.Stop!.Event = "perro";
            Refused(Lore.RunEventShort(5), d, new AnswerStopCommand { Option = 0 }, 1, Both);
            Assert.Null(d.Proposal);
        }

        [Fact]
        public void TheResultReadsBack()
        {
            var (c, stop) = At("puente");
            Ok(c, new AnswerStopCommand { Option = 0 });
            var json = Json.Write(c);
            var back = Json.Read<CampaignState>(json);
            Assert.Equal(json, Json.Write(back));
            Assert.Equal(stop.Result!.Soldier, back.Run!.Stop!.Result!.Soldier);
        }
    }
}
