using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Bastion.Engine;
using Xunit;
using Xunit.Sdk;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/medicalDressing.test.ts, test by test (the dressing Setup.CreateMission gives the medical sites,
    /// and MedicalAbandonment.Abandon). `JSON.parse(JSON.stringify(s))` is a round trip through GameState with the tests'
    /// JSON options. Nothing left out.
    /// </summary>
    public class MedicalDressingTests
    {
        private static readonly HashSet<string> Corpses = new HashSet<string> { MedicalDetailKinds.CorpseCivilian, MedicalDetailKinds.CorpseStaff, MedicalDetailKinds.CoveredCorpse, MedicalDetailKinds.CoveredBed };

        private static GameState Mission(string medical, int seed)
        {
            return Setup.CreateMission(new MissionOptions
            {
                Map = MapSource.Generated(Biomes.City, seed, medical: medical),
                Slots = new List<int> { 0 },
                Rng = new Rng(seed),
                Kind = medical == MedicalSiteKinds.Hospital ? MissionKinds.Rescue : MissionKinds.Recovery,
            });
        }

        private static readonly string[] Centres = { MedicalSiteKinds.Clinic, MedicalSiteKinds.Ward, MedicalSiteKinds.Hospital };

        /// <summary>`d.z ? s.levels[d.z - 1] : s`: the tiles and decor of the storey a detail is on.</summary>
        private static (IReadOnlyList<string?> Tiles, IReadOnlyList<int>? Decor) StoreyOf(GameState s, int z)
        {
            return z != 0 ? (s.Levels![z - 1].Tiles, s.Levels[z - 1].Decor) : ((IReadOnlyList<string?>)s.Tiles, s.Decor);
        }

        /// <summary>`decorOf(decor?.[i])?.kind`.</summary>
        private static string? KindAt(IReadOnlyList<int>? decor, int i)
        {
            return Decor.DecorOf(decor != null && i >= 0 && i < decor.Count ? decor[i] : (int?)null)?.Kind;
        }

        /// <summary>`tiles[i]`: undefined (null) out of it.</summary>
        private static string? TileAt(IReadOnlyList<string?> tiles, int i)
        {
            return i >= 0 && i < tiles.Count ? tiles[i] : null;
        }

        /// <summary>toEqual on anything: the same JSON, null and missing alike.</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            if (diff != null) throw new XunitException(diff);
        }

        // ------------------------------------------------------------ medical atmosphere

        [Fact(DisplayName = "medical atmosphere › gives every centre seeded bodies, including centres with five and ten, without adding units (what stands up in the mess takes its tile: furnishing.test.ts)")]
        public void GivesEveryCentreSeededBodies()
        {
            var counts = new HashSet<int>();
            var kinds = new HashSet<string>();
            foreach (var medical in Centres)
            {
                for (var seed = 0; seed < 48; seed++)
                {
                    var s = Mission(medical, seed * 7919);
                    var b = s.Medical![0];
                    var details = b.Dressing!;
                    var count = details.Count((d) => Corpses.Contains(d.Kind));
                    Assert.True(count >= 1, $"{medical}/{seed}");
                    Assert.True(count <= 10);
                    counts.Add(count);
                    var occupied = new HashSet<string>();
                    foreach (var d in details)
                    {
                        kinds.Add(d.Kind);
                        Assert.True(d.X > b.X);
                        Assert.True(d.X < b.X + b.W - 1);
                        Assert.True(d.Y > b.Y);
                        Assert.True(d.Y < b.Y + b.H - 1);
                        var st = StoreyOf(s, d.Z);
                        var i = d.Y * s.Width + d.X;
                        if (Corpses.Contains(d.Kind))
                        {
                            var key = $"{d.X},{d.Y},{d.Z}";
                            Assert.DoesNotContain(key, occupied);
                            occupied.Add(key);
                            if (d.Kind == MedicalDetailKinds.CoveredBed) Assert.Equal(DecorKinds.HospitalBed, KindAt(st.Decor, i));
                            else Assert.Equal(TileKinds.Floor, st.Tiles[i]);
                        }
                        if (d.Kind == MedicalDetailKinds.PrivacyScreen || d.Kind == MedicalDetailKinds.BareBed) Assert.Contains(KindAt(st.Decor, i), new[] { DecorKinds.HospitalBed, DecorKinds.ExamTable, DecorKinds.Gurney });
                        if (d.Kind == MedicalDetailKinds.WallCross || d.Kind == MedicalDetailKinds.CautionSign || d.Kind == MedicalDetailKinds.SafeSign)
                        {
                            var f = Grid.Dirs4[d.Facing];
                            Assert.Equal(TileKinds.Wall, st.Tiles[(d.Y - f.Y) * s.Width + d.X - f.X]);
                        }
                    }
                    Same(s.Medical, Events.CloneState(s).Medical);
                    Assert.True(s.Units.All((u) => !u.Id.StartsWith("corpse")));
                }
            }
            Assert.True(counts.Contains(1) && counts.Contains(5) && counts.Contains(10), string.Join(",", counts.OrderBy((c) => c)));
            foreach (var kind in new[] { MedicalDetailKinds.BareBed, MedicalDetailKinds.CoveredBed, MedicalDetailKinds.PrivacyScreen, MedicalDetailKinds.MedicalSupplies, MedicalDetailKinds.Luggage, MedicalDetailKinds.Books, MedicalDetailKinds.Crutches, MedicalDetailKinds.WallCross, MedicalDetailKinds.CautionSign, MedicalDetailKinds.SafeSign }) Assert.True(kinds.Contains(kind), kind);
        }

        [Fact(DisplayName = "medical atmosphere › keeps dressing stable when replayed or saved and keeps it out of ordinary maps")]
        public void KeepsDressingStable()
        {
            foreach (var medical in Centres)
            {
                var s = Mission(medical, 209458);
                Same(s.Medical, Mission(medical, 209458).Medical);
                Same(s.Medical, JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(s, Options), Options)!.Medical);
                var old = Events.CloneState(s);
                old.Medical![0].Dressing = null;
                Assert.Null(Events.CloneState(old).Medical![0].Dressing);
            }
            var plain = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 13), Slots = new List<int> { 0 }, Rng = new Rng(13), Kind = MissionKinds.Elimination });
            Assert.Null(plain.Medical);
        }

        [Fact(DisplayName = "medical atmosphere › places blood and litter on their own floor or wall, preserves bodies, and on its own (no strew) never changes the tactical map")]
        public void PlacesBloodAndLitterOnTheirOwnFloorOrWall()
        {
            var added = new HashSet<string>
            {
                MedicalDetailKinds.BloodPool, MedicalDetailKinds.BloodDrops, MedicalDetailKinds.WallBlood, MedicalDetailKinds.FloorGrime, MedicalDetailKinds.ScatteredPapers,
                MedicalDetailKinds.MedicalWaste, MedicalDetailKinds.OverturnedChair, MedicalDetailKinds.FallenIV, MedicalDetailKinds.SpilledSupplies, MedicalDetailKinds.RumpledLinen,
            };
            var seen = new HashSet<string>();
            var variations = new HashSet<int?>();
            foreach (var medical in Centres)
            {
                for (var seed = 0; seed < 24; seed++)
                {
                    var s = Mission(medical, seed * 7919);
                    var b = s.Medical![0];
                    var original = b.Dressing!.Where((d) => !added.Contains(d.Kind)).ToList();
                    var before = JsonSerializer.Serialize(s, Options);
                    var originalBefore = JsonSerializer.Serialize(original, Options);
                    var debris = MedicalAbandonment.Abandon(b, s.Width, s.Tiles, s.Decor!, s.Levels ?? new List<Level>(), seed * 7919, new List<Vec2>(), original);
                    Assert.Equal(before, JsonSerializer.Serialize(s, Options));
                    Assert.Equal(originalBefore, JsonSerializer.Serialize(original, Options));
                    Assert.Equal(original.Count((d) => Corpses.Contains(d.Kind)), debris.Count((d) => d.Kind == MedicalDetailKinds.BloodPool));
                    Assert.Contains(debris, (d) => d.Kind == MedicalDetailKinds.ScatteredPapers);
                    Assert.Contains(debris, (d) => d.Kind == MedicalDetailKinds.FloorGrime);
                    foreach (var d in debris)
                    {
                        seen.Add(d.Kind);
                        variations.Add(d.Variant);
                        var st = StoreyOf(s, d.Z);
                        var i = d.Y * s.Width + d.X;
                        Assert.Contains(b.Rooms!, (r) => r.Use != MedicalUses.Stairs && r.Z == d.Z && d.X >= r.X && d.X < r.X + r.W && d.Y >= r.Y && d.Y < r.Y + r.H);
                        if (d.Kind == MedicalDetailKinds.WallBlood)
                        {
                            var f = Grid.Dirs4[d.Facing];
                            Assert.Equal(TileKinds.Wall, st.Tiles[(d.Y - f.Y) * s.Width + d.X - f.X]);
                        }
                        else if (d.Kind == MedicalDetailKinds.BloodPool && st.Tiles[i] == TileKinds.Low) Assert.Equal(DecorKinds.HospitalBed, KindAt(st.Decor, i));
                        else Assert.Equal(TileKinds.Floor, st.Tiles[i]);
                        if (d.Kind == MedicalDetailKinds.FallenIV || d.Kind == MedicalDetailKinds.OverturnedChair)
                        {
                            Assert.DoesNotContain(original, (p) => Corpses.Contains(p.Kind) && p.X == d.X && p.Y == d.Y && p.Z == d.Z);
                            Assert.DoesNotContain(Grid.Dirs4, (f) => new[] { TileKinds.Door, TileKinds.Ladder }.Contains(TileAt(st.Tiles, (d.Y + f.Y) * s.Width + d.X + f.X) ?? ""));
                        }
                    }
                }
            }
            Assert.True(added.All((k) => seen.Contains(k)), string.Join(", ", added.Where((k) => !seen.Contains(k))));
            Assert.Equal(8, variations.Count);
        }
    }
}
