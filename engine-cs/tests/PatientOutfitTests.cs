using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The dead in plain clothes that start inside a clinic or a hospital wear its patients' gowns (Outfits.Patient;
    /// Arturo, 9/11, the patients of Horror Asylum): Setup, after the groups and the lone dead are placed. A uniform stays
    /// a uniform, and nothing else changes.
    /// </summary>
    public class PatientOutfitTests
    {
        private static bool Inside(MedicalSite b, Pos p)
        {
            return p.X >= b.X && p.Y >= b.Y && p.X < b.X + b.W && p.Y < b.Y + b.H;
        }

        [Fact]
        public void TheDeadInsideAMedicalSiteWearItsGowns()
        {
            var patients = 0;
            foreach (var medical in MedicalSiteKinds.All)
            {
                for (var seed = 0; seed < 12; seed++)
                {
                    var kind = medical == MedicalSiteKinds.Hospital ? MissionKinds.Rescue : MissionKinds.Recovery;
                    var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed * 104729, medical: medical), Slots = new List<int> { 0 }, Rng = new Rng(seed), Kind = kind });
                    var site = s.Medical!.Single();
                    foreach (var u in s.Units)
                    {
                        if (u.Outfit == Outfits.Patient)
                        {
                            Assert.Equal(TemplateIds.Zombie, u.Template);
                            Assert.True(Inside(site, u.Pos), $"{medical}/{seed}/{u.Id}");
                            patients++;
                        }
                        else if (u.Template == TemplateIds.Zombie && Inside(site, u.Pos))
                        {
                            // Only a uniform keeps a zombie inside out of the gown.
                            Assert.Contains(u.Outfit, new[] { Outfits.Police, Outfits.Military });
                        }
                    }
                }
            }
            Assert.True(patients > 0, "no patient in 36 medical missions");
        }

        [Fact]
        public void ElsewhereNobodyIsAPatient()
        {
            foreach (var biome in new[] { Biomes.City, Biomes.Wilds, Biomes.Facility })
            {
                for (var seed = 1; seed <= 6; seed++)
                {
                    var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, seed * 7919), Slots = new List<int> { 0 }, Rng = new Rng(seed) });
                    if (s.Medical != null) continue;
                    Assert.DoesNotContain(s.Units, (u) => u.Outfit == Outfits.Patient);
                }
            }
        }
    }
}
