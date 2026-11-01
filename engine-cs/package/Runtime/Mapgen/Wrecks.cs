#nullable enable
// Wrecks and fires (Arturo, 1/11; new in C#, not in mapgen.ts): cars left crashed about the streets (a pile-up by a
// junction, one abandoned askew in the road, now and then one driven into a building's front) and, on some maps, what
// burns: a wreck, a parked car, bins and drums, a pile on the pavement. Laid last on the ground, once the squad, the groups
// of the dead, the objective and the evacuation have their places, each on its own seed, and a car only where every place
// the squad could reach stays reachable.
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>A wreck laid on the canvas: its nose (the anchor), how it came there and how far askew it lies.</summary>
        private sealed class WreckSpot
        {
            public Vec2 Nose = V(0, 0);
            public string Kind = WreckKinds.Abandoned;
            public int Tilt;
        }

        /// <summary>A fire laid on the canvas: the tile of what burns and the ground burning round it (or the pile itself).</summary>
        private sealed class FireSpot
        {
            public Vec2 Source = V(0, 0);
            public List<Vec2> Tiles = new List<Vec2>();
        }

        /// <summary>None of either this near where the squad comes in.</summary>
        private const int WreckClear = 10;
        /// <summary>Fires keep this far apart.</summary>
        private const int FireGap = 6;

        /// <summary>A car's tiles, nose first (the anchor, decor.ts FOOTPRINT): from `nose` back, against `facing`.</summary>
        private static List<Vec2> CarCells(Vec2 nose, int facing)
        {
            var d = Grid.Dirs4[facing];
            return new List<Vec2> { nose, V(nose.X - d.X, nose.Y - d.Y), V(nose.X - 2 * d.X, nose.Y - 2 * d.Y) };
        }

        /// <summary>Askew by `lo` to `hi` degrees, either way.</summary>
        private static int Tilt(Rng rng, int lo, int hi)
        {
            var t = lo + rng.Int(hi - lo + 1);
            return rng.Int(2) == 0 ? t : -t;
        }

        /// <summary>
        /// Wrecked cars about the streets (Arturo, 1/11: «coches chocados, algunos en medio de la carretera, quizás uno
        /// reventado contra una casa… pero orgánico»): in a town, a pile-up by a junction most times, one or two abandoned
        /// askew in the road and now and then one driven into a house's or a shop's front; fewer on an industrial estate; in
        /// the country, now and then one left on the road. Never on what `avoid` holds nor near `start`; each car only if
        /// every tile of `reach` but its own is still reachable from `start` (its tiles leave `reach`).
        /// </summary>
        private static List<WreckSpot> LayWrecks(Canvas c, int seed, string biome, List<PlanRoad>? roads, bool estate, Rect objRect, Vec2 start, JsSet<int> reach, HashSet<int> avoid)
        {
            var rng = new Rng(seed ^ 0x7772);
            var out_ = new List<WreckSpot>();
            if (biome == Biomes.Facility) return out_;
            var town = roads != null && !estate;
            var pileups = town ? (rng.Next() < 0.7 ? 1 : 0) + (rng.Next() < 0.2 ? 1 : 0) : estate ? (rng.Next() < 0.5 ? 1 : 0) : 0;
            var abandoned = town ? 1 + (rng.Next() < 0.5 ? 1 : 0) : estate ? 1 : rng.Next() < 0.6 ? 1 : 0;
            var walls = town ? (rng.Next() < 0.45 ? 1 : 0) : estate ? (rng.Next() < 0.2 ? 1 : 0) : 0;

            bool Clear(Vec2 p) => c.Free(p.X, p.Y) && c.Decor[c.I(p.X, p.Y)] == 0 && c.GroundAt(p.X, p.Y) != Indoor && !avoid.Contains(c.I(p.X, p.Y)) && Dist(p, start) >= WreckClear;
            bool OnRoad(Vec2 p) => c.GroundAt(p.X, p.Y) == Road;
            // A country road is kept open (reserved): a car may stand on it all the same, where it cuts no way.
            bool Track(Vec2 p) => c.In(p.X, p.Y) && c.Get(p.X, p.Y) == TileKinds.Floor && c.ElevAt(p.X, p.Y) == 0 && c.Decor[c.I(p.X, p.Y)] == 0 && !avoid.Contains(c.I(p.X, p.Y)) && Dist(p, start) >= WreckClear;
            // The cars of one scene, laid one by one: if one of them would cut a way, the whole scene goes.
            var scene = new List<(List<Vec2> Cells, string[] Tiles, int[] Decor)>();
            bool Lay(List<Vec2> cells, int facing, string kind, int tilt)
            {
                var before = (cells.Select((p) => c.Get(p.X, p.Y)).ToArray(), cells.Select((p) => c.Decor[c.I(p.X, p.Y)]).ToArray());
                foreach (var p in cells) c.Force(p.X, p.Y, TileKinds.Low);
                DressObject(c, cells, DecorKinds.CarFront, facing);
                var own = new JsSet<int>(cells.Select((p) => c.I(p.X, p.Y)));
                var after = Flood(c, start);
                if (!reach.All((i) => own.Has(i) || after.Has(i)))
                {
                    Undo(cells, before.Item1, before.Item2);
                    return false;
                }
                scene.Add((cells, before.Item1, before.Item2));
                foreach (var i in own) reach.Delete(i);
                out_.Add(new WreckSpot { Nose = cells[0], Kind = kind, Tilt = tilt });
                return true;
            }
            void Undo(List<Vec2> cells, string[] tiles, int[] decor)
            {
                for (var k = 0; k < cells.Count; k++)
                {
                    c.Force(cells[k].X, cells[k].Y, tiles[k]);
                    c.Decor[c.I(cells[k].X, cells[k].Y)] = decor[k];
                }
                c.Objects.RemoveAt(c.Objects.Count - 1);
            }
            void Drop()
            {
                for (var k = scene.Count - 1; k >= 0; k--)
                {
                    Undo(scene[k].Cells, scene[k].Tiles, scene[k].Decor);
                    out_.RemoveAt(out_.Count - 1);
                }
                scene.Clear();
            }

            // A pile-up by a junction: one ran into the side of one crossing it, and maybe a third into the back of the first.
            if (roads != null && pileups > 0)
            {
                var junctions = new List<Rect>();
                foreach (var v in roads.Where((r) => r.Vertical))
                {
                    foreach (var h in roads.Where((r) => !r.Vertical))
                    {
                        int x0 = Math.Max(v.R.X, h.R.X), y0 = Math.Max(v.R.Y, h.R.Y), x1 = Math.Min(v.R.X + v.R.W, h.R.X + h.R.W), y1 = Math.Min(v.R.Y + v.R.H, h.R.Y + h.R.H);
                        if (x1 > x0 && y1 > y0) junctions.Add(Rect(x0, y0, x1 - x0, y1 - y0));
                    }
                }
                foreach (var j in Shuffle(junctions, rng))
                {
                    if (pileups == 0) break;
                    var f = rng.Int(4);
                    var d = Grid.Dirs4[f];
                    // Coming into the junction going `d`: its nose on the arm, a tile or two short of it, in one of the arm's lanes.
                    var back = rng.Int(2);
                    var lanes = d.X != 0 ? j.H : j.W;
                    var lane = rng.Int(Math.Max(1, lanes));
                    Vec2 nose = d.X == 1 ? V(j.X - 1 - back, j.Y + lane)
                        : d.X == -1 ? V(j.X + j.W + back, j.Y + lane)
                        : d.Y == 1 ? V(j.X + lane, j.Y - 1 - back)
                        : V(j.X + lane, j.Y + j.H + back);
                    var first = CarCells(nose, f);
                    var side = rng.Int(2) == 0 ? (f + 1) % 4 : (f + 3) % 4;
                    var hit = V(nose.X + d.X, nose.Y + d.Y);
                    var sd = Grid.Dirs4[side];
                    var second = CarCells(V(hit.X + sd.X, hit.Y + sd.Y), side);
                    var third = rng.Next() < 0.4 ? CarCells(V(nose.X - 3 * d.X, nose.Y - 3 * d.Y), f) : null;
                    if (!first.All((p) => Clear(p) && OnRoad(p)) || !second.All(Clear)) continue;
                    if (third != null && !third.All((p) => Clear(p) && OnRoad(p))) third = null;
                    scene.Clear();
                    if (!Lay(first, f, WreckKinds.Pileup, Tilt(rng, 5, 20)) || !Lay(second, side, WreckKinds.Pileup, Tilt(rng, 10, 30)))
                    {
                        Drop();
                        continue;
                    }
                    if (third != null) Lay(third, f, WreckKinds.Pileup, Tilt(rng, 5, 15));
                    scene.Clear();
                    pileups--;
                }
            }

            // Abandoned in the road, askew: in a street's middle lanes, or on a country road.
            if (abandoned > 0)
            {
                var spots = new List<(Vec2 Nose, int Facing)>();
                if (roads != null)
                {
                    foreach (var r in roads)
                    {
                        var width = r.Vertical ? r.R.W : r.R.H;
                        var length = r.Vertical ? r.R.H : r.R.W;
                        if (width < 4) continue;
                        for (var lane = 1; lane <= width - 2; lane++)
                        {
                            for (var t = 4; t + 4 < length; t++)
                            {
                                var p = r.Vertical ? V(r.R.X + lane, r.R.Y + t) : V(r.R.X + t, r.R.Y + lane);
                                var f = r.Vertical ? (rng.Int(2) == 0 ? 0 : 2) : rng.Int(2) == 0 ? 1 : 3;
                                spots.Add((p, f));
                            }
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < c.Tiles.Count; i++)
                    {
                        var p = V(i % c.Width, i / c.Width);
                        if (!OnRoad(p)) continue;
                        var f = rng.Int(4);
                        spots.Add((p, f));
                    }
                }
                foreach (var (nose, f) in Shuffle(spots, rng))
                {
                    if (abandoned == 0) break;
                    var cells = CarCells(nose, f);
                    // A whole car on the road, and road either side of it along the way it lies (not across a junction's mouth).
                    if (!cells.All((p) => (roads != null ? Clear(p) : Track(p)) && OnRoad(p))) continue;
                    var d = Grid.Dirs4[f];
                    if (!OnRoad(V(nose.X + d.X, nose.Y + d.Y)) || !OnRoad(V(cells[2].X - d.X, cells[2].Y - d.Y))) continue;
                    scene.Clear();
                    if (Lay(cells, f, WreckKinds.Abandoned, Tilt(rng, 12, 35))) abandoned--;
                    scene.Clear();
                }
            }

            // Driven into a house's or a shop's front: its nose where the wall was, the rest out on the pavement.
            if (walls > 0)
            {
                var fronts = new List<(Vec2 Wall, int Facing)>();
                foreach (var b in c.Buildings)
                {
                    if ((b.Furnish != Furnishes.Home && b.Furnish != Furnishes.Shop) || b.Ruined || b.MedicalKind != null || b.Market || ReferenceEquals(b.Outer, objRect)) continue;
                    var r = b.Outer;
                    foreach (var p in TilesIn(r))
                    {
                        var top = p.Y == r.Y;
                        var bottom = p.Y == r.Y + r.H - 1;
                        var left = p.X == r.X;
                        var right = p.X == r.X + r.W - 1;
                        // An edge but not a corner.
                        if ((top || bottom) == (left || right)) continue;
                        var outward = top ? 0 : right ? 1 : bottom ? 2 : 3;
                        fronts.Add((p, (outward + 2) % 4));
                    }
                }
                foreach (var (w, f) in Shuffle(fronts, rng))
                {
                    if (walls == 0) break;
                    var d = Grid.Dirs4[f];
                    var along = Grid.Dirs4[(f + 1) % 4];
                    // A plain stretch of wall, with floor behind it, and the pavement or the road in front free for the car's back.
                    if (c.Get(w.X, w.Y) != TileKinds.Wall || c.Get(w.X + d.X, w.Y + d.Y) != TileKinds.Floor) continue;
                    if (c.Get(w.X + along.X, w.Y + along.Y) != TileKinds.Wall || c.Get(w.X - along.X, w.Y - along.Y) != TileKinds.Wall) continue;
                    var cells = CarCells(w, f);
                    if (!cells.Skip(1).All(Clear) || avoid.Contains(c.I(w.X, w.Y)) || Dist(w, start) < WreckClear) continue;
                    scene.Clear();
                    if (Lay(cells, f, WreckKinds.Wall, Tilt(rng, 3, 15))) walls--;
                    scene.Clear();
                }
            }
            return out_;
        }

        /// <summary>
        /// What burns, on some maps (Arturo, 1/11: «algún mapa con cosas incendiadas»; it lights at night and burns whoever
        /// steps on it): two towns in five and an industrial estate as often, one map in four in the country or a facility. One
        /// to three fires, apart: a wreck or a parked car with the ground burning beside it, bins or drums, a pile on the
        /// pavement. Their ground is floor one can step on, off what `avoid` holds and away from `start`.
        /// </summary>
        private static List<FireSpot> LayFires(Canvas c, int seed, string biome, bool town, List<WreckSpot> wrecks, Vec2 start, HashSet<int> avoid)
        {
            var rng = new Rng(seed ^ 0x6669);
            var out_ = new List<FireSpot>();
            var share = town || biome == Biomes.City ? 0.4 : 0.25;
            if (rng.Next() >= share) return out_;
            var count = 1 + (rng.Next() < 0.5 ? 1 : 0) + (rng.Next() < 0.2 ? 1 : 0);
            bool Ground(Vec2 p) => c.In(p.X, p.Y) && c.Get(p.X, p.Y) == TileKinds.Floor && c.ElevAt(p.X, p.Y) == 0 && !c.Keep[c.I(p.X, p.Y)] && c.GroundAt(p.X, p.Y) != Indoor && !avoid.Contains(c.I(p.X, p.Y)) && Dist(p, start) >= WreckClear;
            bool Apart(Vec2 p) => out_.All((f) => Dist(f.Source, p) >= FireGap);
            // What may burn: the wrecks first, then the cars by the kerb, the bins and the drums, and piles on the pavement.
            var cars = new List<List<Vec2>>();
            var small = new List<Vec2>();
            for (var i = 0; i < c.Tiles.Count; i++)
            {
                var look = global::Bastion.Engine.Decor.DecorOf(c.Decor[i]);
                if (look == null) continue;
                var p = V(i % c.Width, i / c.Width);
                if (look.Kind == DecorKinds.CarFront && c.GroundAt(p.X, p.Y) != Indoor) cars.Add(CarCells(p, look.Facing));
                else if ((look.Kind == DecorKinds.Bin || look.Kind == DecorKinds.Drums) && c.GroundAt(p.X, p.Y) != Indoor) small.Add(p);
            }
            var wrecked = wrecks.Select((w) => CarCells(w.Nose, global::Bastion.Engine.Decor.DecorOf(c.Decor[c.I(w.Nose.X, w.Nose.Y)])?.Facing ?? 0)).ToList();
            var sources = new List<(List<Vec2> Cells, int Ground)>();
            sources.AddRange(Shuffle(wrecked, rng).Select((cells) => (cells, 2)));
            var rest = Shuffle(cars, rng).Select((cells) => (cells, 2)).Concat(Shuffle(small, rng).Select((p) => (new List<Vec2> { p }, 1))).ToList();
            sources.AddRange(Shuffle(rest, rng));
            foreach (var (cells, ground) in sources)
            {
                if (out_.Count == count) break;
                var source = cells[0];
                if (!Apart(source) || Dist(source, start) < WreckClear) continue;
                var round = new List<Vec2>();
                foreach (var p in cells) foreach (var d in Grid.Dirs8)
                {
                    var q = V(p.X + d.X, p.Y + d.Y);
                    if (Ground(q) && !round.Any((o) => o.X == q.X && o.Y == q.Y) && !cells.Any((o) => o.X == q.X && o.Y == q.Y)) round.Add(q);
                }
                if (round.Count == 0) continue;
                var burning = Shuffle(round, rng).Take(1 + rng.Int(ground)).ToList();
                out_.Add(new FireSpot { Source = source, Tiles = burning });
                foreach (var p in burning) avoid.Add(c.I(p.X, p.Y));
            }
            // Short of what burns, a pile of rubbish on fire out in the street.
            if (out_.Count < count)
            {
                var piles = Shuffle(Enumerable.Range(0, c.Tiles.Count).Select((i) => V(i % c.Width, i / c.Width)).Where((p) => Ground(p) && c.Decor[c.I(p.X, p.Y)] == 0).ToList(), rng);
                foreach (var p in piles)
                {
                    if (out_.Count == count) break;
                    if (!Apart(p)) continue;
                    out_.Add(new FireSpot { Source = p, Tiles = new List<Vec2> { p } });
                    avoid.Add(c.I(p.X, p.Y));
                }
            }
            return out_;
        }
    }
}
