using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>M8 (Arturo, 23/11: «el único que debería perder de cerca es el francotirador»): the crossbow the same at any distance.</summary>
    public class CrossbowReachTests
    {
        [Fact(DisplayName = "alcance › only the precision rifle loses aim close: every other weapon that shoots, the Cazador's crossbow too, loses nothing at one tile")]
        public void OnlyThePrecisionRifleLosesAimClose()
        {
            var close = Content.Weapons.Values.Where((w) => !w.Melee && w.Range != RangeTableIds.None && Content.RangeModifier(w.Range, 1) < 0).Select((w) => w.Id).ToList();
            Assert.Equal(new[] { WeaponIds.Sniper }, close);
            Assert.Equal(WeaponIds.Crossbow, Content.Templates[TemplateIds.Lancer].Weapon);
        }

        [Fact(DisplayName = "alcance › the crossbow keeps its reach: nothing off from 11 tiles on, as before, and nothing off closer either")]
        public void TheCrossbowKeepsItsReach()
        {
            var crossbow = Content.Weapons[WeaponIds.Crossbow];
            for (var d = 1; d <= 25; d++) Assert.Equal(0, Content.RangeModifier(crossbow.Range, d));
            for (var d = 11; d <= 25; d++) Assert.Equal(Content.RangeModifier(RangeTableIds.Long, d), Content.RangeModifier(crossbow.Range, d));
            var reach = Content.ReachTiles(crossbow);
            Assert.Equal((1, Content.Templates[TemplateIds.Rookie].Sight), (reach.From, reach.To));
            Assert.NotNull(Lore.Reaches[crossbow.Range].Name);
        }
    }
}
