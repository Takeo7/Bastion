using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// «A mano» (Producto, 24/11): a run's soldier with nothing at the belt, its knife moved to the pack or left on the floor,
    /// is told apart for the text (Content.BareHanded); the blow is the knife's all the same.
    /// </summary>
    public class BareHandedTests
    {
        [Fact(DisplayName = "a mano › the knife into the pack or on the floor: bare-handed for the text, the blow the knife's; worn again, the knife")]
        public void TheKnifeTakenOff()
        {
            var (_, s) = LaunchAt(NodeKinds.Normal);
            var u = s.Units.First((x) => x.Team == Teams.Xcom && !string.IsNullOrEmpty(x.CampaignId));
            var knife = u.Kit!.Melee!;
            Assert.Equal(FoundWeaponIds.Knife, knife.Type);
            Assert.False(Content.BareHanded(u));
            var blow = Content.AttackWeapon(u, AbilityIds.Slash);
            void Do(string action, string? slot = null)
            {
                u.Ap = 2;
                var cmd = new ItemCommand { Unit = u.Id, Action = action, Gear = knife.Id, Slot = slot };
                Assert.Null(RunPack.Refusal(s, cmd, 0, new[] { 0 }));
                Assert.True(Rules.ExecuteCommand(s, cmd, 0, new[] { 0 }, new Rng(0)).Ok);
            }
            Do(ItemActions.Arrange);
            Assert.Contains(u.Pack!.Items, (p) => p.Gear.Id == knife.Id);
            Assert.True(Content.BareHanded(u));
            Do(ItemActions.Drop);
            Assert.True(Content.BareHanded(u));
            // The blow as with the knife, and the Tajo still there.
            Assert.Same(blow, Content.AttackWeapon(u, AbilityIds.Slash));
            Assert.Contains(AbilityIds.Slash, Abilities.AbilitiesOf(u));
            Do(ItemActions.Take);
            Do(ItemActions.Equip, KitSlots.Melee);
            Assert.False(Content.BareHanded(u));
        }

        [Fact(DisplayName = "a mano › none taken out of the base: bare-handed in the fight; outside the runs, never")]
        public void NoneFromTheBase()
        {
            var (_, s) = LaunchAt(NodeKinds.Normal, 1, (c) => c.Soldiers.ForEach((x) => { if (x.Kit != null) x.Kit.Melee = null; }));
            var squad = s.Units.Where((x) => x.Team == Teams.Xcom && !string.IsNullOrEmpty(x.CampaignId)).ToList();
            Assert.NotEmpty(squad);
            Assert.All(squad, (u) => Assert.True(Content.BareHanded(u)));
            Assert.All(squad, (u) => Assert.Equal(Content.Weapons[WeaponIds.Knife], Content.AttackWeapon(u, AbilityIds.Slash)));
            Assert.False(Content.BareHanded(new Unit()));
        }
    }
}
