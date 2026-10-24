using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bastion.Engine;
using Xunit;
using Xunit.Sdk;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/medical.test.ts, test by test (the medical sites Setup.CreateMission makes, reached by
    /// Paths.ComputeReach and the flights of Flights). Nothing left out.
    /// </summary>
    public class MedicalTests
    {
        private static bool Inside(MedicalSite b, int x, int y)
        {
            return x >= b.X && y >= b.Y && x < b.X + b.W && y < b.Y + b.H;
        }

        private static readonly HashSet<string> Kinds = new HashSet<string>
        {
            DecorKinds.HospitalBed, DecorKinds.MedicalShelf, DecorKinds.Gurney, DecorKinds.MedicalCabinet, DecorKinds.MedicalCart, DecorKinds.Wheelchair, DecorKinds.ReceptionDesk,
            DecorKinds.WaitingSeats, DecorKinds.ExamTable, DecorKinds.MedicalMonitor, DecorKinds.Xray, DecorKinds.BedsideTable, DecorKinds.VisitorChair, DecorKinds.ClinicalSink,
            DecorKinds.Sterilizer, DecorKinds.Ultrasound, DecorKinds.WaitingTable, DecorKinds.WaterDispenser, DecorKinds.Toilet, DecorKinds.ClinicalBin, DecorKinds.ConsultDesk,
        };

        private static MissionOptions Opts(MapSource map, int seed, string? kind = null)
        {
            return new MissionOptions { Map = map, Slots = new List<int> { 0 }, Rng = new Rng(seed), Kind = kind };
        }

        /// <summary>`decor?.[i]`: undefined (null) out of it.</summary>
        private static DecorLook? DecorAt(IReadOnlyList<int>? decor, int i)
        {
            return Decor.DecorOf(decor != null && i >= 0 && i < decor.Count ? decor[i] : (int?)null);
        }

        private static readonly MethodInfo Shallow = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

        /// <summary>`computeReach({ ...s, units: [scout] }, scout, 100_000)`.</summary>
        private static Reach ReachAlone(GameState s, Unit scout)
        {
            var alone = (GameState)Shallow.Invoke(s, null)!;
            alone.Units = new List<Unit> { scout };
            return Paths.ComputeReach(alone, scout, 100_000);
        }

        /// <summary>`expect(xs, label).toContain(k)`.</summary>
        private static void Has(IEnumerable<string> xs, string k, string label = "")
        {
            var list = xs.ToList();
            if (!list.Contains(k)) throw new XunitException($"{label}: expected [{string.Join(", ", list)}] to contain {k}");
        }

        /// <summary>toEqual on anything: the same JSON, null and missing alike.</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            if (diff != null) throw new XunitException(diff);
        }

        // ------------------------------------------------------------ medical locations

        [Fact(DisplayName = "medical locations › scopes clinical furniture and wear to explicit locations, with varied plans")]
        public void ScopesClinicalFurnitureAndWear()
        {
            var plans = new HashSet<string>();
            var furniture = new HashSet<string>();
            foreach (var medical in new[] { MedicalSiteKinds.Clinic, MedicalSiteKinds.Ward, MedicalSiteKinds.Hospital })
            {
                for (var seed = 0; seed < 24; seed++)
                {
                    var s = Setup.CreateMission(Opts(MapSource.Generated(Biomes.City, seed * 104729, medical: medical), seed, medical == MedicalSiteKinds.Hospital ? MissionKinds.Rescue : MissionKinds.Recovery));
                    Assert.Single(s.Medical!);
                    var b = s.Medical![0];
                    Assert.Equal(medical == MedicalSiteKinds.Clinic ? 0.08 : medical == MedicalSiteKinds.Ward ? 0.45 : 0.85, b.Wear);
                    foreach (var ds in new[] { s.Decor }.Concat((s.Levels ?? new List<Level>()).Select((l) => l.Decor)))
                    {
                        for (var i = 0; i < (ds?.Count ?? 0); i++)
                        {
                            var k = Decor.DecorOf(ds![i])?.Kind;
                            if (k == null || !Kinds.Contains(k)) continue;
                            furniture.Add(k);
                            Assert.True(Inside(b, i % s.Width, i / s.Width), $"{medical}/{seed}/{k}");
                        }
                    }
                    plans.Add(string.Join(",", s.Decor!.Where((d) => Kinds.Contains(Decor.DecorOf(d)?.Kind ?? ""))));
                    Same(s.Medical, Events.CloneState(s).Medical);
                }
            }
            Assert.True(plans.Count > 12, $"{plans.Count}");
            Assert.True(Kinds.All((k) => furniture.Contains(k)), string.Join(", ", Kinds.Where((k) => !furniture.Contains(k))));
        }

        [Fact(DisplayName = "medical locations › keeps every hospital floor, stair foot and landing reachable with furniture in place")]
        public void KeepsEveryHospitalFloorReachable()
        {
            for (var seed = 0; seed < 64; seed++)
            {
                var s = Setup.CreateMission(new MissionOptions
                {
                    Map = MapSource.Generated(Biomes.City, seed * 7919),
                    Slots = new List<int> { 0, 1 },
                    Rng = new Rng(seed),
                    Kind = MissionKinds.Rescue,
                    CampaignMission = new CampaignMission { Id = "doctor", Name = "La doctora", Story = 2 },
                });
                var b = s.Medical![0];
                var label = $"hospital/{seed}";
                var scout = s.Units.First((u) => u.Team == Teams.Xcom);
                var reach = ReachAlone(s, scout);
                bool Reached(int x, int y, int z) => reach.Cost.Has(Grid.NodeOf(s, P(x, y, z)));
                for (var z = 0; z < 3; z++)
                {
                    var floors = 0;
                    var stairs = 0;
                    IReadOnlyList<string?> tiles = z != 0 ? s.Levels![z - 1].Tiles : (IReadOnlyList<string?>)s.Tiles;
                    var decor = z != 0 ? s.Levels![z - 1].Decor : s.Decor;
                    for (var i = 0; i < tiles.Count; i++)
                    {
                        var at = Grid.PosOf(s, i);
                        var p = P(at.X, at.Y, z);
                        if (!Inside(b, p.X, p.Y)) continue;
                        var t = tiles[i];
                        if (!string.IsNullOrEmpty(t) && Grid.CanStandOn(t))
                        {
                            floors++;
                            Assert.True(Reached(p.X, p.Y, z), $"{label}/p{z}/{i}");
                        }
                        var d = DecorAt(decor, i);
                        if (t != TileKinds.Ladder || d?.Kind != DecorKinds.Stairs) continue;
                        stairs++;
                        // A flight of several tiles (docs/MAPAS.md, phase 2): on it from its foot, off it onto its landing.
                        var flight = Flights.FlightAt(s, p)!.Flight;
                        var foot = Flights.FlightFoot(flight);
                        var landing = Flights.FlightLanding(flight);
                        var first = P(flight.Tiles[0].X, flight.Tiles[0].Y, z);
                        Assert.True(Grid.KindAt(s, foot) == TileKinds.Floor, label);
                        Assert.True(Grid.KindAt(s, landing) == TileKinds.Floor, label);
                        Assert.True(Paths.StepBetween(s, foot, first) != null, label);
                        Assert.True(Paths.StepBetween(s, p, landing) != null, label);
                        Assert.True(Paths.StepBetween(s, landing, p) != null, label);
                    }
                    Assert.True(floors > 0, $"{label}/p{z}");
                    if (z < 2) Assert.True(stairs > 0, $"{label}/p{z}");
                }
                var vip = s.Units.First((u) => u.Captive);
                Assert.True(Reached(vip.Pos.X, vip.Pos.Y, vip.Pos.Z), label);
            }
        }

        [Fact(DisplayName = "medical locations › gives compact clinics separate reception, consultation and equipment rooms")]
        public void GivesCompactClinicsSeparateRooms()
        {
            for (var seed = 0; seed < 80; seed++)
            {
                var s = Setup.CreateMission(Opts(MapSource.Generated(Biomes.City, seed * 104729, medical: MedicalSiteKinds.Clinic), seed, MissionKinds.Recovery));
                var b = s.Medical![0];
                Assert.True(Math.Min(b.W, b.H) <= 11);
                Assert.True(Math.Max(b.W, b.H) <= 11);
                Assert.Equal(new[] { MedicalUses.Reception, MedicalUses.Consultation, MedicalUses.Equipment }, b.Rooms?.Select((r) => r.Use).ToArray());
                var scout = s.Units.First((u) => u.Team == Teams.Xcom);
                var reach = ReachAlone(s, scout);
                foreach (var r in b.Rooms!)
                {
                    var furniture = new List<string>();
                    int reached = 0, doors = 0;
                    for (var y = r.Y; y < r.Y + r.H; y++)
                    {
                        for (var x = r.X; x < r.X + r.W; x++)
                        {
                            var i = y * s.Width + x;
                            var d = DecorAt(s.Decor, i);
                            if (d != null) furniture.Add(d.Kind);
                            if (Grid.KindAt(s, P(x, y, 0)) == TileKinds.Floor)
                            {
                                Assert.True(reach.Cost.Has(i), $"clinic/{seed}/{r.Use}");
                                reached++;
                            }
                            foreach (var v in Grid.Dirs4) if (Grid.KindAt(s, P(x + v.X, y + v.Y, 0)) == TileKinds.Door) doors++;
                        }
                    }
                    Assert.True(reached > 0);
                    Assert.True(doors > 0);
                    Has(furniture, r.Use == MedicalUses.Reception ? DecorKinds.ReceptionDesk : DecorKinds.ExamTable);
                    if (r.Use == MedicalUses.Equipment) Has(furniture, DecorKinds.Xray);
                }
            }
        }

        [Fact(DisplayName = "medical locations › restricts large hospitals to cities and gives their floors distinct functions")]
        public void RestrictsLargeHospitalsToCities()
        {
            foreach (var biome in new[] { Biomes.City, Biomes.Wilds, Biomes.Facility })
            {
                for (var seed = 0; seed < 16; seed++)
                {
                    var s = Setup.CreateMission(Opts(MapSource.Generated(biome, seed * 7919, medical: MedicalSiteKinds.Hospital), seed, MissionKinds.Rescue));
                    if (biome != Biomes.City)
                    {
                        Assert.Null(s.Medical);
                        continue;
                    }
                    var b = s.Medical![0];
                    Assert.True(Math.Min(b.W, b.H) >= 12);
                    for (var z = 0; z < 3; z++)
                    {
                        var uses = b.Rooms!.Where((r) => r.Z == z).Select((r) => r.Use).ToList();
                        Has(uses, z == 0 ? MedicalUses.Reception : MedicalUses.Ward);
                        Has(uses, z == 1 ? MedicalUses.Nurses : MedicalUses.Equipment);
                    }
                }
            }
        }

        [Fact(DisplayName = "medical locations › uses small inpatient rooms with one or two beds and equipped consultations")]
        public void UsesSmallInpatientRooms()
        {
            for (var seed = 0; seed < 48; seed++)
            {
                var s = Setup.CreateMission(Opts(MapSource.Generated(Biomes.City, seed * 7919, medical: MedicalSiteKinds.Hospital), seed, MissionKinds.Rescue));
                var rooms = s.Medical![0].Rooms!;
                List<string> Contents(MedicalRoom r)
                {
                    var ds = r.Z != 0 ? s.Levels![r.Z - 1].Decor : s.Decor;
                    var @out = new List<string>();
                    for (var y = r.Y; y < r.Y + r.H; y++)
                    {
                        for (var x = r.X; x < r.X + r.W; x++)
                        {
                            var d = DecorAt(ds, y * s.Width + x);
                            if (d != null && d.Kind != DecorKinds.Stairs) @out.Add(d.Kind);
                        }
                    }
                    return @out;
                }
                for (var z = 0; z < 3; z++) Assert.True(rooms.Count((r) => r.Z == z) >= 8);
                foreach (var r in rooms)
                {
                    var label = $"hospital/{seed}/p{r.Z}/{r.Use}";
                    if (r.Use == MedicalUses.Stairs) continue;
                    var furniture = Contents(r);
                    // (One bed 3 × 4, two 4 × 5: docs/MAPAS.md §4.3.)
                    if (r.Use == MedicalUses.Ward)
                    {
                        Assert.True(r.W * r.H <= 20, label);
                        var beds = furniture.Count((k) => k == DecorKinds.HospitalBed);
                        Assert.True(beds >= 1, label);
                        Assert.True(beds <= 2, label);
                        Assert.True(furniture.Count((k) => k == DecorKinds.BedsideTable) >= beds, label);
                        Has(furniture, DecorKinds.MedicalMonitor, label);
                        Assert.True(furniture.Count >= 4, label);
                    }
                    if (r.Use == MedicalUses.Consultation)
                    {
                        Assert.True(r.W * r.H <= 20, label);
                        foreach (var k in new[] { DecorKinds.ConsultDesk, DecorKinds.ExamTable, DecorKinds.ClinicalSink, DecorKinds.MedicalCabinet }) Has(furniture, k, label);
                    }
                    if (r.Use == MedicalUses.Equipment)
                    {
                        Assert.True(furniture.Any((k) => k == DecorKinds.Xray || k == DecorKinds.Ultrasound), label);
                        Has(furniture, DecorKinds.ExamTable, label);
                        Assert.True(furniture.Count >= 4, label);
                    }
                }
                var waiting = rooms.Where((r) => r.Z == 0 && r.Use == MedicalUses.Waiting).ToList();
                Assert.True(waiting.Count > 0);
                foreach (var r in waiting) Has(Contents(r), DecorKinds.WaitingSeats);
            }
        }

        [Fact(DisplayName = "medical locations › gives infirmaries waiting/triage, treatment, observation and stocked medical stores")]
        public void GivesInfirmariesWaitingTreatmentObservationAndStores()
        {
            for (var seed = 0; seed < 64; seed++)
            {
                var s = Setup.CreateMission(Opts(MapSource.Generated(Biomes.City, seed * 7919, medical: MedicalSiteKinds.Ward), seed, MissionKinds.Recovery));
                var rooms = s.Medical![0].Rooms!;
                foreach (var use in new[] { MedicalUses.Waiting, MedicalUses.Treatment, MedicalUses.Ward, MedicalUses.Supplies }) Has(rooms.Select((r) => r.Use), use, $"infirmary/{seed}");
                var scout = s.Units.First((u) => u.Team == Teams.Xcom);
                var reach = ReachAlone(s, scout);
                foreach (var r in rooms)
                {
                    var furniture = new List<string>();
                    for (var y = r.Y; y < r.Y + r.H; y++)
                    {
                        for (var x = r.X; x < r.X + r.W; x++)
                        {
                            var i = y * s.Width + x;
                            var d = DecorAt(s.Decor, i);
                            if (d != null) furniture.Add(d.Kind);
                            if (Grid.CanStandOn(s.Tiles[i])) Assert.True(reach.Cost.Has(i), $"infirmary/{seed}/{r.Use}");
                        }
                    }
                    Assert.True(furniture.Count >= 4);
                    if (r.Use == MedicalUses.Waiting) foreach (var k in new[] { DecorKinds.ReceptionDesk, DecorKinds.WaitingSeats }) Has(furniture, k);
                    if (r.Use == MedicalUses.Ward) foreach (var k in new[] { DecorKinds.HospitalBed, DecorKinds.BedsideTable, DecorKinds.MedicalMonitor }) Has(furniture, k);
                    if (r.Use == MedicalUses.Treatment)
                    {
                        Assert.Contains(furniture, (k) => k == DecorKinds.ExamTable || k == DecorKinds.Gurney);
                        Has(furniture, DecorKinds.Sterilizer);
                        Has(furniture, DecorKinds.ClinicalSink);
                    }
                    if (r.Use == MedicalUses.Supplies) foreach (var k in new[] { DecorKinds.MedicalShelf, DecorKinds.MedicalCabinet, DecorKinds.MedicalCart }) Has(furniture, k);
                }
            }
        }

        [Fact(DisplayName = "medical locations › leaves ordinary maps without medical finishes or furniture")]
        public void LeavesOrdinaryMapsWithoutMedicalFinishes()
        {
            foreach (var biome in new[] { Biomes.City, Biomes.Wilds, Biomes.Facility })
            {
                var s = Setup.CreateMission(Opts(MapSource.Generated(biome, 7), 1));
                Assert.Null(s.Medical);
                foreach (var ds in new[] { s.Decor }.Concat((s.Levels ?? new List<Level>()).Select((l) => l.Decor))) Assert.False(ds?.Any((d) => Kinds.Contains(Decor.DecorOf(d)?.Kind ?? "")) ?? false);
            }
        }
    }
}
