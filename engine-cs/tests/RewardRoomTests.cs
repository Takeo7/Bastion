using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The room the squad's packs have left for a stage's reward, before the fight is applied (Runs.RewardRoom; UI, cuadro
    /// 148: the card said «y la recompensa» with the packs full): what ApplyFight then puts in, the rest lost.
    /// </summary>
    public class RewardRoomTests
    {
        /// <summary>A Hold launched, the squad's packs full of rifles but for `last` on the first one's last row; all aboard.</summary>
        private static (CampaignState C, GameState S) Packed(System.Action<CampaignState, GearGrid, int>? last = null)
        {
            var (c, s) = LaunchAt(NodeKinds.Hold);
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && !string.IsNullOrEmpty(u.CampaignId)).ToList();
            foreach (var u in squad)
            {
                u.Pack!.Items.Clear();
                for (var y = 0; y < u.Pack.H; y++)
                    if (u != squad[0] || y < u.Pack.H - 1) Assert.True(RunPack.Put(u.Pack, Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle), 0, y, false));
            }
            last?.Invoke(c, squad[0].Pack!, squad[0].Pack!.H - 1);
            foreach (var u in squad) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            return (c, s);
        }

        /// <summary>RewardRoom asked before, nothing changed by it; then the fight applied: what of the reward was lost.</summary>
        private static (int Room, int Reward, int Lost) Applied(CampaignState c, GameState s)
        {
            var (campaign, fight) = (Json.Write(c), Json.Write(s));
            var room = Runs.RewardRoom(c, s);
            Assert.Equal((campaign, fight), (Json.Write(c), Json.Write(s)));
            var run = EndFight(c, s, Outcomes.Victory, EndReasons.Objective).Run!;
            return (room, run.Reward!.Value, run.RewardLost ?? 0);
        }

        [Fact(DisplayName = "recompensa › the packs full: no room, and all of it lost")]
        public void PacksFull()
        {
            var (c, s) = Packed((c, pack, y) => Assert.True(RunPack.Put(pack, Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle), 0, y, false)));
            var (room, reward, lost) = Applied(c, s);
            Assert.Equal(0, room);
            Assert.Equal(reward, lost);
        }

        [Fact(DisplayName = "recompensa › room on a pile only: that much goes, the rest lost")]
        public void RoomOnAPile()
        {
            var (c, s) = Packed((c, pack, y) =>
            {
                Assert.True(RunPack.Put(pack, Gears.NewGear(c, GearKinds.Supplies, null, 80), 0, y, false));
                Assert.True(RunPack.Put(pack, Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Bat), 1, y, false));
            });
            var (room, reward, lost) = Applied(c, s);
            Assert.Equal(20, room);
            Assert.Equal(reward - 20, lost);
        }

        [Fact(DisplayName = "recompensa › a cell free: a full pile's room, the reward all in")]
        public void ACellFree()
        {
            var (c, s) = Packed((c, pack, y) =>
            {
                Assert.True(RunPack.Put(pack, Gears.NewGear(c, GearKinds.Supplies, null, 80), 0, y, false));
                Assert.True(RunPack.Put(pack, Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol), 1, y, false));
            });
            var (room, reward, lost) = Applied(c, s);
            Assert.Equal(120, room);
            Assert.True(reward <= room);
            Assert.Equal(0, lost);
        }
    }
}
