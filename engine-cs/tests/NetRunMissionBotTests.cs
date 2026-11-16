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
    /// campaign starts with, and with rifles.
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
                output.WriteLine($"{(rifles ? "fusiles" : "cuchillos")} {seed} ({s.MapName}): {s.Outcome}/{s.OutcomeReason}, turno {s.Turn}, {s.RoomsSearched?.Count ?? 0} habitaciones, {report.Found.Count} objetos, {report.Fallen.Count} caídos, {played.Orders} órdenes");
            }
            output.WriteLine($"ganadas: {won} de {Seeds}");
            Assert.True(won >= (rifles ? 7 : 5), $"{won} de {Seeds}");
        }
    }
}
