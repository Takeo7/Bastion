using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Wrecks and fires (Arturo, 1/11; new in C#, not in mapgen.ts): cars crashed about the streets, and on some maps what
    /// burns, lighting at night and burning whoever steps on it (Mapgen.LayWrecks, LayFires; Rules.Burn).
    /// </summary>
    public class WrecksAndFiresTests
    {
        private static GameState Town(int seed)
        {
            return Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Kind = MissionKinds.Recovery, Slots = new List<int> { 0 }, Rng = new Rng(seed) });
        }

        [Fact(DisplayName = "wrecks › towns have them, of every kind and askew, each a car of the decor; one driven into a front stands in its wall line")]
        public void TownsHaveWrecks()
        {
            var kinds = new Dictionary<string, int>();
            var tilts = new HashSet<int>();
            for (var seed = 1; seed <= 24; seed++)
            {
                var s = Town(seed);
                foreach (var w in s.Wrecks ?? new List<Wreck>())
                {
                    kinds[w.Kind] = kinds.TryGetValue(w.Kind, out var n) ? n + 1 : 1;
                    tilts.Add(w.Tilt);
                    Assert.InRange(w.Tilt, -35, 35);
                    var look = Decor.DecorOf(s.Decor![w.Anchor]);
                    Assert.Equal(DecorKinds.CarFront, look?.Kind);
                    Assert.Equal(TileKinds.Low, s.Tiles[w.Anchor]);
                    // Into a front: its nose on the building's own ground, the rest outside it.
                    if (w.Kind == WreckKinds.Wall) Assert.True(s.Storeys![w.Anchor] > 0, $"seed {seed}");
                }
            }
            Assert.True(kinds.GetValueOrDefault(WreckKinds.Pileup) > 0 && kinds.GetValueOrDefault(WreckKinds.Abandoned) > 0 && kinds.GetValueOrDefault(WreckKinds.Wall) > 0, string.Join(", ", kinds));
            Assert.True(tilts.Count > 10);
        }

        [Fact(DisplayName = "wrecks › they cut no way: from where the squad starts, the evacuation, the objective and every group of the dead are still reached")]
        public void TheyCutNoWay()
        {
            foreach (var biome in new[] { Biomes.City, Biomes.Wilds })
            {
                for (var seed = 1; seed <= 12; seed++)
                {
                    var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, seed), Kind = MissionKinds.Recovery, Slots = new List<int> { 0 }, Rng = new Rng(seed) });
                    var soldier = s.Units.First((u) => u.Team == Teams.Xcom);
                    var reach = Paths.ComputeReach(s, soldier, 1e6, null, (o) => true);
                    bool Reached(Pos p) => reach.Cost.Has(Grid.NodeOf(s, p));
                    foreach (var z in s.Mission.EvacZone) Assert.True(Reached(Grid.Ground(z)), $"{biome} {seed}: the evacuation at {z.X},{z.Y}");
                    if (s.Mission.Item?.Pos != null) Assert.True(Reached(s.Mission.Item.Pos), $"{biome} {seed}: the objective");
                    foreach (var u in s.Units.Where((u) => u.Team != Teams.Xcom && u.Pos.Z == 0)) Assert.True(Reached(u.Pos), $"{biome} {seed}: {u.Id} at {u.Pos.X},{u.Pos.Y}");
                }
            }
        }

        [Fact(DisplayName = "fires › some maps burn: ground one can step on, off the squad and the dead, each with a light of its own id")]
        public void SomeMapsBurn()
        {
            var burning = 0;
            for (var seed = 1; seed <= 40; seed++)
            {
                var s = Town(seed);
                if (s.Fires == null) continue;
                burning++;
                Assert.InRange(s.Fires.Count, 1, 3);
                foreach (var f in s.Fires)
                {
                    Assert.NotEmpty(f.Tiles);
                    foreach (var i in f.Tiles)
                    {
                        Assert.True(Grid.CanStandOn(s.Tiles[i]), $"seed {seed}: {f.Id} on {s.Tiles[i]}");
                        Assert.DoesNotContain(s.Units, (u) => u.Pos.Z == 0 && Grid.TileIndex(s, u.Pos.X, u.Pos.Y) == i);
                    }
                    var light = s.Lights!.Single((l) => l.Id == f.Id);
                    Assert.Equal(Content.Flames.Light, light.Radius);
                    Assert.Equal(f.Source, Grid.TileIndex(s, light.Tile.X, light.Tile.Y));
                }
            }
            // Two towns in five, about.
            Assert.InRange(burning, 8, 24);
        }

        [Fact(DisplayName = "pitched roofs › towns list their houses' roofs, which nobody stands on")]
        public void PitchedRoofs()
        {
            var roofs = 0;
            for (var seed = 1; seed <= 8; seed++)
            {
                var s = Town(seed);
                foreach (var r in s.Pitched ?? new List<PitchedRoof>())
                {
                    roofs++;
                    Assert.Contains(r.Kind, new[] { PitchedKinds.House, PitchedKinds.Barn });
                    for (var y = r.Y; y < r.Y + r.H; y++)
                    {
                        for (var x = r.X; x < r.X + r.W; x++)
                        {
                            var top = s.Storeys![Grid.TileIndex(s, x, y)];
                            Assert.True(top > 0, $"seed {seed}: {x},{y} off the building");
                            Assert.False(Grid.CanStandOn(Grid.KindAt(s, P(x, y, top))), $"seed {seed}: a roof stood on at {x},{y},{top}");
                        }
                    }
                }
            }
            Assert.True(roofs > 20, $"{roofs}");
        }

        private static GameState Street()
        {
            var s = StateFrom(new[] { "..........", "..........", "..........", "..........", ".........." });
            s.Fires = new List<Fire> { new Fire { Id = "f1", Source = Grid.TileIndex(s, 4, 0), Tiles = new List<int> { Grid.TileIndex(s, 4, 1) } } };
            s.Lights = new List<Light> { new Light { Id = "f1", Tile = P(4, 0), Radius = Content.Flames.Light, Tiles = Los.LightTiles(s, P(4, 0), Content.Flames.Light, Content.Flames.LightHeight) } };
            return s;
        }

        [Fact(DisplayName = "fires › stepping on fire burns a point, armour or not; ending the turn on it, another")]
        public void SteppingOnFireBurns()
        {
            var s = Street();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            // One of the dead far off, so the mission goes on.
            AddUnit(s, "a1", TemplateIds.Zombie, 9, 4);
            var hp = soldier.Hp;
            var sim = NewSim(s);
            sim.MoveAlong(soldier, new List<Pos> { P(2, 1), P(3, 1), P(4, 1), P(5, 1) }, 1);
            Assert.Contains("burned", Tags(sim.Events));
            Assert.Equal(hp - 1, soldier.Hp);
            Assert.True(Grid.SamePos(P(5, 1), soldier.Pos));
            // On it at the end of the squad's turn: again; and the dead too, at the end of theirs.
            soldier.Pos = P(4, 1);
            var end = NewSim(s);
            end.RunAlienTurn();
            Assert.Equal(hp - 2, soldier.Hp);
            Assert.True(Los.IsLit(s, P(4, 1)));
        }

        [Fact(DisplayName = "fires › the enemies go round it when they can, and through it when they can't")]
        public void TheEnemiesGoRound()
        {
            var s = Street();
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 2, 1);
            var reach = Paths.ComputeReach(s, zombie, 100);
            var path = Paths.BuildPath(s, zombie, reach, P(6, 1))!;
            Assert.DoesNotContain(path, (p) => p.X == 4 && p.Y == 1);
            // A corridor one wide with the fire in it: through it.
            var narrow = StateFrom(new[] { "#########", ".........", "#########" });
            narrow.Fires = new List<Fire> { new Fire { Id = "f1", Source = Grid.TileIndex(narrow, 4, 0), Tiles = new List<int> { Grid.TileIndex(narrow, 4, 1) } } };
            var walker = AddUnit(narrow, "a1", TemplateIds.Zombie, 1, 1);
            var through = Paths.BuildPath(narrow, walker, Paths.ComputeReach(narrow, walker, 100), P(7, 1));
            Assert.NotNull(through);
            Assert.Contains(through!, (p) => p.X == 4 && p.Y == 1);
            // The squad's ways don't change.
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 2, 3);
            var fresh = StateFrom(new[] { "..........", "..........", "..........", "..........", ".........." });
            var node = Grid.NodeOf(s, P(4, 1));
            Assert.Equal(Paths.ComputeReach(fresh, AddUnit(fresh, "s1", TemplateIds.Rookie, 2, 3), 100).Cost[node], Paths.ComputeReach(s, soldier, 100).Cost[node]);
        }
    }
}
