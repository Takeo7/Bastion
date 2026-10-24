using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The streets' dressing (0.10.1's dressing.ts dressStreets; UI's): what a town left behind, placed from the map and
    /// always the same for a map, cosmetic. Nothing may read as cover: tall things are slim and stand by the kerb, near a
    /// tile's corner, where nobody stands (street lights over the road, power poles with their wires along straight streets,
    /// a stop sign at a block's corner); what lies about is flat or small, below the knee (bags and tyres against the
    /// façades, a pallet, a bike, weeds in the cracks). The hydrants and mailboxes stand on tiles of their own, where the
    /// engine puts them, drawn here by the kerb. In 0.10.1's frame and tiles; the street lights' lamps said apart, for the
    /// night's lights.
    /// </summary>
    public static class StreetDressing
    {
        /// <summary>Tiles between street lights along a kerb, and between power poles (a wire's length).</summary>
        const int LightEvery = 7;
        const int PoleEvery = 10;
        /// <summary>Where the tall things by a kerb stand from the middle of their tile: towards the street, and along it.</summary>
        const double KerbOut = 0.47;
        const double KerbAlong = 0.21;

        /// <summary>
        /// The dressing of `s`'s streets and pavements (towns and facilities; the wilds only their lamps), `density` of the
        /// small things. The street lights stand where the engine says (its own lights, those that never go out), else every
        /// few tiles along the kerbs.
        /// </summary>
        public static List<FrameArt.Dressing> Dress(GameState s, GroundMap ground, double density)
        {
            var output = new List<FrameArt.Dressing>();
            int w = s.Width, h = s.Height;
            // On a map with a base, none: its lights are its floodlights (BasePieces; mapView.ts `fixed`).
            var lamps = s.Base != null ? new List<Pos>() : s.Lights?.Where(l => l.Turns == null).Select(l => l.Tile).ToList();
            // The engine's lights: a street light on each, near the kerb, its arm over the road.
            foreach (var p in lamps ?? new List<Pos>())
            {
                int k = -1;
                for (int j = 0; j < 4 && k < 0; j++)
                {
                    var dd = Grid.Dirs4[j];
                    int x = p.X + dd.X, y = p.Y + dd.Y;
                    if (x >= 0 && y >= 0 && x < w && y < h && ground.Kinds[y * w + x] == GroundMap.Street) k = j;
                }
                var d = Grid.Dirs4[k < 0 ? 0 : k];
                int alongX = -d.Y, alongY = d.X;
                int e = p.Y * w + p.X < s.Elev.Count ? s.Elev[p.Y * w + p.X] : 0;
                output.Add(FrameArt.Placed("streetLight", p.X + 0.5 + d.X * KerbOut + alongX * KerbAlong, e * BuildingPlan.Storey, p.Y + 0.5 + d.Y * KerbOut + alongY * KerbAlong, d.X, d.Y, true));
            }
            if (s.Biome == Biomes.Wilds) return output;
            int Kind(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? -1 : ground.Kinds[y * w + x];
            string Tile(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? null : s.Tiles[y * w + x];
            int Level(int x, int y) => y * w + x < s.Elev.Count ? s.Elev[y * w + x] : 0;
            bool Free(int x, int y) => Tile(x, y) == TileKinds.Floor && Level(x, y) == 0;
            bool Street(int x, int y) => Kind(x, y) == GroundMap.Street;
            bool Paving(int x, int y) => Kind(x, y) == GroundMap.Open && !ground.IsLawn(y * w + x);
            bool Wallish(int x, int y)
            {
                var t = Tile(x, y);
                return t == TileKinds.Wall || t == TileKinds.Window || t == TileKinds.WindowBroken || t == TileKinds.Door || t == TileKinds.DoorOpen;
            }
            // Tiles a tall thing stands on (lamps, poles, hydrants, stop signs): the next keeps its distance.
            var taken = new byte[w * h];
            void Take(int x, int y) => taken[y * w + x] = 1;
            foreach (var p in lamps ?? new List<Pos>()) Take(p.X, p.Y);
            // Something taken less than `r` tiles away, walking.
            bool Near(int x, int y, int r)
            {
                for (int dy = 1 - r; dy < r; dy++)
                {
                    int ty = y + dy;
                    if (ty < 0 || ty >= h) continue;
                    int reach = r - 1 - Math.Abs(dy);
                    for (int tx = Math.Max(0, x - reach); tx <= Math.Min(w - 1, x + reach); tx++)
                        if (taken[ty * w + tx] != 0) return true;
                }
                return false;
            }

            // Power poles: along straight kerbs over a wire and a bit long, on about half of them, wired pole to pole.
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
            {
                int lines = dx != 0 ? h : w, steps = dx != 0 ? w : h;
                foreach (int sign in new[] { -1, 1 })
                {
                    // The street on this side of the kerb.
                    int sideX = dx != 0 ? 0 : sign, sideY = dx != 0 ? sign : 0;
                    for (int line = 0; line < lines; line++)
                    {
                        var run = new List<(int x, int y)>();
                        void Flush()
                        {
                            if (run.Count >= PoleEvery + 3 && FrameArt.Hash(run[0].x, run[0].y, 3 + sign) < 0.5)
                            {
                                Vector3? last = null;
                                for (int k = 1; k < run.Count - 1; k += PoleEvery)
                                {
                                    var t = run[k];
                                    var p = new Vector3((float)(t.x + 0.5 + sideX * KerbOut - dx * KerbAlong), 0, (float)(t.y + 0.5 + sideY * KerbOut - dy * KerbAlong));
                                    output.Add(FrameArt.Placed("powerPole", p.x, 0, p.z, dx, dy, true));
                                    // The wire hangs from the pole before.
                                    if (last != null) output.Add(FrameArt.Placed("powerLine", last.Value.x, 0, last.Value.z, dx, dy, false, Vector3.Distance(p, last.Value) / PoleEvery));
                                    last = p;
                                    Take(t.x, t.y);
                                }
                            }
                            run.Clear();
                        }
                        for (int k = 0; k < steps; k++)
                        {
                            int x = dx != 0 ? k : line, y = dx != 0 ? line : k;
                            if (Paving(x, y) && Free(x, y) && Street(x + sideX, y + sideY)) run.Add((x, y));
                            else Flush();
                        }
                        Flush();
                    }
                }
            }

            // The engine's hydrants and mailboxes, on the pavement by the street: towards the kerb in the middle of their
            // tile, a little narrowed.
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var look = Tile(x, y) == TileKinds.Low ? Decor.DecorOf(s.Decor != null && y * w + x < s.Decor.Count ? s.Decor[y * w + x] : (int?)null) : null;
                    if (look?.Kind != DecorKinds.Hydrant && look?.Kind != DecorKinds.Mailbox) continue;
                    var d = Grid.Dirs4[look.Facing];
                    output.Add(FrameArt.Placed(look.Kind, x + 0.5 + d.X * 0.18, 0, y + 0.5 + d.Y * 0.18, -d.X, -d.Y, true, 1, 0.85));
                    Take(x, y);
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!Free(x, y)) continue;
                    int px = x, py = y;
                    Func<double, double> r = salt => FrameArt.Hash(px, py, salt);
                    double cx = x + 0.5, cz = y + 0.5;
                    if (Paving(x, y))
                    {
                        var toStreet = Grid.Dirs4.Where(dd => Street(px + dd.X, py + dd.Y)).ToList();
                        if (toStreet.Count > 0)
                        {
                            var d = toStreet[0];
                            int alongX = -d.Y, alongY = d.X;
                            // A block's corner (the street on two sides): a stop sign.
                            if (toStreet.Count >= 2 && r(1) < 0.45 && !Near(x, y, 3))
                            {
                                var e = toStreet[1];
                                output.Add(FrameArt.Placed("stopSign", cx + d.X * KerbOut + e.X * KerbAlong, 0, cz + d.Y * KerbOut + e.Y * KerbAlong, d.X + e.X, d.Y + e.Y, true));
                                Take(x, y);
                                continue;
                            }
                            // Street lights along the kerb, the arm over the road (where the engine places none).
                            if (lamps == null && r(2) < 1.0 / LightEvery + 0.04 && !Near(x, y, LightEvery - 2))
                            {
                                output.Add(FrameArt.Placed("streetLight", cx + d.X * KerbOut + alongX * KerbAlong, 0, cz + d.Y * KerbOut + alongY * KerbAlong, d.X, d.Y, true));
                                Take(x, y);
                                continue;
                            }
                        }
                        // Against a façade: a few bags, rubble fallen from it, a tyre or a pallet, now and then; small.
                        var toWall = Grid.Dirs4.Where(dd => Wallish(px + dd.X, py + dd.Y)).ToList();
                        if (toWall.Count > 0 && r(5) < 0.22 * density)
                        {
                            var d = toWall[0];
                            int alongX = -d.Y, alongY = d.X;
                            int n = 1 + (int)Math.Floor(r(6) * 3);
                            for (int k = 0; k < n; k++)
                            {
                                double u = (r(10 + k) - 0.5) * 0.7;
                                double pick = r(20 + k);
                                string id = pick < 0.55 ? (r(30 + k) * 2 < 1 ? "trashBag" : "trashBag2") : pick < 0.78 ? "pebbles" : r(40 + k) < 0.5 ? "tire" : "pallet";
                                double yaw = r(50 + k) * Math.PI * 2;
                                output.Add(FrameArt.Placed(id, cx + d.X * 0.3 + alongX * u, 0, cz + d.Y * 0.3 + alongY * u, Math.Cos(yaw), Math.Sin(yaw), id != "pallet" && id != "tire" && id != "pebbles", 1, 0.75 + r(60 + k) * 0.25));
                            }
                            continue;
                        }
                    }
                    // Weeds in the cracks of the paving and in the gutters; a bike or a tyre in the road.
                    if ((Paving(x, y) || Street(x, y)) && r(7) < 0.05 * density)
                    {
                        double yaw = r(8) * Math.PI * 2;
                        string id = r(9) < 0.75 ? "tuftLow" : "tuft";
                        output.Add(FrameArt.Placed(id, cx + (r(11) - 0.5) * 0.6, 0, cz + (r(12) - 0.5) * 0.6, Math.Cos(yaw), Math.Sin(yaw), false, 1, 0.7 + r(13) * 0.5));
                    }
                    else if (Street(x, y) && r(14) < 0.012 * density)
                    {
                        double yaw = r(15) * Math.PI * 2;
                        output.Add(FrameArt.Placed(r(16) < 0.5 ? "bike" : "tire", cx + (r(17) - 0.5) * 0.5, 0, cz + (r(18) - 0.5) * 0.5, Math.Cos(yaw), Math.Sin(yaw), true));
                    }
                }
            }
            return output;
        }

        /// <summary>
        /// Where a street light's lamp hangs (mapView.ts: at the end of its arm, its +x, 1.45 tiles out and 4.05 up), in
        /// Unity's metres, and the way the arm points (over the road).
        /// </summary>
        public static (Vector3 at, Vector3 facing) LampOf(FrameArt.Dressing light)
        {
            var m = light.Matrix;
            float x = m.m03 + m.m00 * 1.45f, z = m.m23 + m.m20 * 1.45f;
            return (new Vector3(x * MapSpace.Tile, (m.m13 + 4.05f) * MapSpace.Tile, -z * MapSpace.Tile), new Vector3(m.m00, 0, -m.m20).normalized);
        }
    }
}
