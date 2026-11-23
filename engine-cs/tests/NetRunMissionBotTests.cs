using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// MissionBot in a run's fight (H3; Gameplay, 16/11): the Saqueo of the first row, searched and left on La Lata, never
    /// hunting the dead (it fell on every one so: 4 of 4); the pack used, magazines spent on reloading. With the knife the
    /// campaign starts with, and with rifles. And the stages of H5 and H6: the Asedio, the Travesía, the Guarida and El Toro.
    /// </summary>
    public class NetRunMissionBotTests
    {
        private readonly ITestOutputHelper output;

        public NetRunMissionBotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>The first row's node (always a Saqueo) of a campaign alone with seed `seed`, launched; with rifles, a pistol, a vest and magazines, or the knife only.</summary>
        private static (CampaignState c, GameState s) Saqueo(int seed, bool rifles)
        {
            var c = Solo(seed);
            if (rifles)
                foreach (var x in c.Soldiers)
                {
                    InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                    InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                    InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
                    InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
                }
            // Out with the seed's own map (RunsHelpers.Start rolls every run with the same Rng).
            Ok(c, new StartRunCommand(), 0, Zero, seed);
            var n = RunMap.Choices(c.Run!)[0];
            Assert.Equal(NodeKinds.Normal, n.Kind);
            Ok(c, new ChooseNodeCommand { Node = n.Id });
            return (c, new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state);
        }

        /// <summary>A mission played by MissionBot for every seat, as the test apps do (RunFightTests.Play): the orders given and refused.</summary>
        private static (int Orders, int Refused) Play(GameState state, int seed, IReadOnlyList<int> connected)
        {
            var rng = new Rng(seed * 7919 + 13);
            int orders = 0, refused = 0, idle = 0;
            while (state.Outcome == null && orders < 3000 && idle < 3)
            {
                var any = false;
                foreach (var slot in connected)
                {
                    var cmd = MissionBot.Next(state, slot, connected);
                    if (cmd == null) continue;
                    orders++;
                    if (Rules.ExecuteCommand(state, cmd, slot, connected, rng).Ok) any = true;
                    else refused++;
                }
                idle = any ? 0 : idle + 1;
            }
            return (orders, refused);
        }

        /// <summary>One soldier left to play in `s` (the rest with nothing left this turn), and the bot's next order for it.</summary>
        private static Unit Alone(GameState s)
        {
            var squad = Grid.Combatants(s, Teams.Xcom).ToList();
            foreach (var x in squad.Skip(1))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            return squad[0];
        }

        private static void Do(GameState s, Command cmd) => Assert.True(Rules.ExecuteCommand(s, cmd, 0, Zero, new Rng(1)).Ok);

        [Fact]
        public void AtHalfHealthItTakesAMedicineFromItsPack()
        {
            var (_, s) = Saqueo(4, false);
            var u = Alone(s);
            u.Hp = u.MaxHp / 2;
            u.Pack!.Items.Add(new Placed { Gear = new Gear { Id = "g900", Kind = GearKinds.Medicine, Count = 2 }, X = 0, Y = 0 });
            var use = Assert.IsType<ItemCommand>(MissionBot.Next(s, 0, Zero));
            Assert.Equal((ItemActions.Use, "g900"), (use.Action, use.Gear));
            Do(s, use);
            Assert.True(u.Hp > u.MaxHp / 2);
        }

        [Fact]
        public void AGunAtItsFeetIsPickedUpAndPutOn()
        {
            var (_, s) = Saqueo(4, false);
            var u = Alone(s);
            Assert.Null(u.Kit!.Primary);
            s.FloorGear = new List<DroppedGear> { new DroppedGear { Gear = new Gear { Id = "g901", Kind = GearKinds.Weapon, Type = FoundWeaponIds.AssaultRifle }, Pos = u.Pos } };
            var take = Assert.IsType<ItemCommand>(MissionBot.Next(s, 0, Zero));
            Assert.Equal((ItemActions.Take, "g901"), (take.Action, take.Gear));
            Do(s, take);
            var equip = Assert.IsType<ItemCommand>(MissionBot.Next(s, 0, Zero));
            Assert.Equal((ItemActions.Equip, "g901"), (equip.Action, equip.Gear));
            Do(s, equip);
            Assert.Equal("g901", u.Kit.Primary?.Id);
        }

        /// <summary>
        /// One with no pack (a recruit made in a fight) on a gun of the run it can't take: it doesn't stand on it for ever, it
        /// goes on (Red's R2 by level, 23/11: easy, 2 seats, seed 5, crouched 40 turns of a Limpieza on a machine gun).
        /// </summary>
        [Fact]
        public void WithNoPackAGunAtItsFeetDoesntHoldItThere()
        {
            var (_, s) = Saqueo(4, false);
            var u = Alone(s);
            u.Pack = null;
            s.FloorGear = new List<DroppedGear> { new DroppedGear { Gear = new Gear { Id = "g901", Kind = GearKinds.Weapon, Type = FoundWeaponIds.AssaultRifle }, Pos = u.Pos } };
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.IsType<MoveCommand>(cmd);
        }

        /// <summary>
        /// The four stages of H5 and H6 with both players and rifles, as the squad is by the chapter's end, on six seeds: the
        /// Asedio held till La Lata comes and the Travesía crossed, won; a Guarida's box opened and El Toro killed, what the
        /// bot does for them (16/11: 2 Guaridas of 6 won, the box opened in 5, the rest lost on the long way back from it to
        /// La Lata, the box being as far from it as it can; El Toro killed in 6, 4 won). No order refused, and every one over.
        /// The least asked is some two under what it does (the Travesía 12 of 16 on more seeds): the map's generator moves
        /// on under it, and a bot broken loses them all.
        /// </summary>
        [Theory]
        [InlineData(NodeKinds.Hold, 4)]
        [InlineData(NodeKinds.Crossing, 3)]
        [InlineData(NodeKinds.Elite, 3)]
        [InlineData(NodeKinds.Boss, 4)]
        public void TheNewStagesArePlayed(string kind, int least)
        {
            var done = 0;
            for (var seed = 1; seed <= 6; seed++)
            {
                var c = Two(seed);
                foreach (var x in c.Soldiers)
                {
                    InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                    InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                    InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
                    InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
                }
                Start(c, seed);
                GoTo(c, kind);
                var s = new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
                var played = Play(s, seed, Both);
                Assert.Equal(0, played.Refused);
                Assert.NotNull(s.Outcome);
                var job = kind == NodeKinds.Elite || kind == NodeKinds.Boss ? s.Mission.ObjectiveDone : s.Outcome == Outcomes.Victory;
                if (job) done++;
                output.WriteLine($"{kind} {seed} ({s.MapName}): {s.Outcome}/{s.OutcomeReason}, turno {s.Turn}, objetivo {(s.Mission.ObjectiveDone ? "cumplido" : "no")}, {played.Orders} órdenes");
            }
            output.WriteLine($"{kind}: {done} de 6");
            Assert.True(done >= least, $"{kind}: {done} de 6");
        }

        private const int Seeds = 8;

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ASaqueoIsSearchedAndLeft(bool rifles)
        {
            var won = 0;
            for (var seed = 1; seed <= Seeds; seed++)
            {
                var (c, s) = Saqueo(seed, rifles);
                var played = Play(s, seed, Zero);
                Assert.Equal(0, played.Refused);
                Assert.NotNull(s.Outcome);
                new EngineCampaignRules().ApplyMissionResult(c, s, new Rng(seed));
                var report = c.LastReport!.Run!;
                if (s.Outcome == Outcomes.Victory) won++;
                output.WriteLine($"{(rifles ? "fusiles" : "cuchillos")} {seed} ({s.MapName}): {s.Outcome}/{s.OutcomeReason}, turno {s.Turn}, {s.RoomsSearched?.Count ?? 0} habitaciones, {report.Found.Count} objetos, {report.Found.Where((g) => g.Kind == GearKinds.Supplies).Sum((g) => g.Count)} §, {report.Fallen.Count} caídos, {played.Orders} órdenes");
            }
            output.WriteLine($"ganadas: {won} de {Seeds}");
            // On 20 seeds (16/11): 19 with knives, 17 with rifles. Some two under it on these 8, as the map's generator moves on
            // under the test; hunting the dead, it lost them all. (19/11, Gameplay: with G22's caps on the Refuges these 8 are
            // other maps, and with knives 5 of them are won; R2's row 1 with knives, 72 %. 21/11, Gameplay: with Saqueadores on
            // the maps, G23.5, seed 4's first fight is an estate, not a city, lost with knives: 4 of 8.)
            Assert.True(won >= 4, $"{won} de {Seeds}");
        }
    }
}
