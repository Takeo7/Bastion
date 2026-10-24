#nullable enable
// stairAccess.ts: moving the one-tile stairs of old generated missions off the walls.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class StairAccess
    {
        /// <summary>Terrain reachability, without units temporarily blocking a passage.</summary>
        private static JsSet<int> Reachable(GameState s, Pos start)
        {
            var seen = new JsSet<int>(new[] { Grid.NodeOf(s, start) });
            var queue = new List<Pos> { start };
            while (queue.Count > 0)
            {
                var p = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var d in Grid.Dirs4)
                {
                    foreach (var step in Paths.StepsFrom(s, p, d.X, d.Y))
                    {
                        var n = Grid.NodeOf(s, step.To);
                        if (seen.Has(n)) continue;
                        seen.Add(n);
                        queue.Add(step.To);
                    }
                }
            }
            return seen;
        }

        /// <summary>`{ ...s, tiles, decor, levels }`: a shallow copy of the state with those three replaced.</summary>
        private static GameState With(GameState s, List<string> tiles, List<int>? decor, List<Level>? levels)
        {
            return new GameState
            {
                Seq = s.Seq,
                Realtime = s.Realtime,
                MapId = s.MapId,
                MapName = s.MapName,
                Biome = s.Biome,
                Site = s.Site,
                Width = s.Width,
                Height = s.Height,
                Tiles = tiles,
                Elev = s.Elev,
                Ground = s.Ground,
                Medical = s.Medical,
                Homes = s.Homes,
                Markets = s.Markets,
                Decor = decor,
                Storeys = s.Storeys,
                Levels = levels,
                Lights = s.Lights,
                Frame = s.Frame,
                Units = s.Units,
                Pods = s.Pods,
                Mission = s.Mission,
                Turn = s.Turn,
                ActiveTeam = s.ActiveTeam,
                Concealed = s.Concealed,
                MissStreak = s.MissStreak,
                Blow = s.Blow,
                Ready = s.Ready,
                Command = s.Command,
                ActiveUnit = s.ActiveUnit,
                EvacCall = s.EvacCall,
                AbandonCall = s.AbandonCall,
                Outcome = s.Outcome,
                OutcomeReason = s.OutcomeReason,
                CampaignMission = s.CampaignMission,
                Smoke = s.Smoke,
                Vehicle = s.Vehicle,
                Snow = s.Snow,
                Searched = s.Searched,
                RoomsSearched = s.RoomsSearched,
                Dropped = s.Dropped,
                DroppedCount = s.DroppedCount,
                Breakables = s.Breakables,
                Base = s.Base,
                Traps = s.Traps,
                Slow = s.Slow,
            };
        }

        private static Pos P(int x, int y, int z)
        {
            return new Pos { X = x, Y = y, Z = z };
        }

        /// <summary>
        /// Old generated missions preferred stairs against a wall. Move those flights
        /// within their existing room when there is safe floor on both storeys. Never
        /// move a unit, remove furniture or an objective, or cut off a reachable floor.
        /// If no safe position fits, keep the saved terrain intact. Flights of several
        /// tiles (docs/MAPAS.md, phase 2) are laid out right by the generator: left
        /// alone.
        /// </summary>
        public static void RepairStairAccess(GameState s)
        {
            if (!s.MapId.StartsWith("gen-", StringComparison.Ordinal) || s.Levels == null || s.Ground == null || s.Storeys == null) return;
            var levels = s.Levels;
            var searched = (s.RoomsSearched ?? new List<int>()).Select((id) => (id, room: Looting.RoomAt(s, Grid.PosOf(s, id)))).ToList();
            var changed = false;
            // The ground's tiles are never null: List<string> and List<string?> are the same list.
            List<string?> groundTiles = s.Tiles!;
            for (var z = 0; z < levels.Count; z++)
            {
                var tiles = z != 0 ? levels[z - 1].Tiles : groundTiles;
                var decor = z != 0 ? levels[z - 1].Decor : s.Decor;
                if (decor == null) continue;
                // Snapshot: a newly moved flight must not be processed a second time.
                var flights = new List<(int i, int up)>();
                for (var i = 0; i < tiles.Count; i++)
                {
                    var look = Decor.DecorOf(Js.At(decor, i, 0));
                    if (tiles[i] != TileKinds.Ladder || look?.Kind != DecorKinds.Stairs || Flights.LongFlightAt(s, P(i % s.Width, (int)Math.Floor((double)i / s.Width), z)) != null) continue;
                    flights.Add((i, look.Facing));
                }
                foreach (var (i, up) in flights)
                {
                    var at = P(i % s.Width, (int)Math.Floor((double)i / s.Width), z);
                    bool Clear(GameState state, Vec2 p) => Grid.Dirs4.All((d) => new[] { z, z + 1 }.All((k) =>
                    {
                        var kind = Grid.KindAt(state, P(p.X + d.X, p.Y + d.Y, k));
                        return kind == TileKinds.Floor || kind == TileKinds.Low || kind == TileKinds.High;
                    }));
                    // `s.levels[z]!.tiles[i] !== null`: undefined past its end is not null.
                    if (Clear(s, at) || i >= levels[z].Tiles.Count || levels[z].Tiles[i] != null) continue;
                    var before = Reachable(s, at);
                    var lower = new List<string?>(tiles);
                    var lowerDecor = new List<int>(decor);
                    var upper = new List<string?>(levels[z].Tiles);
                    var upperDecor = levels[z].Decor;
                    lower[i] = TileKinds.Floor;
                    lowerDecor[i] = 0;
                    upper[i] = TileKinds.Floor;
                    List<string> lowerGround = lower!;
                    var trial = With(
                        s,
                        z != 0 ? s.Tiles : lowerGround,
                        z != 0 ? s.Decor : lowerDecor,
                        levels.Select((l, k) => k == z ? new Level { Tiles = upper, Decor = l.Decor } : k == z - 1 ? new Level { Tiles = lower, Decor = lowerDecor } : l).ToList());
                    // Above the ground, roomAt needs the ceiling temporarily restored too.
                    var room = Looting.RoomAt(trial, at);
                    if (room == null) continue;
                    bool Occupied(Pos p) => s.Units.Any((u) => Grid.SamePos(u.Pos, p)) || (s.Dropped ?? new List<DroppedLoot>()).Any((d) => Grid.SamePos(d.Pos, p)) ||
                        s.Mission.Item?.Pos != null && Grid.SamePos(s.Mission.Item.Pos, p) || s.Mission.Terminal != null && Grid.SamePos(s.Mission.Terminal, p) ||
                        s.Mission.Reinforcements.Any((r) => r.Pos != null && Grid.SamePos(r.Pos, p)) || s.Mission.EvacZone.Any((q) => Grid.SamePos(P(q.X, q.Y, 0), p));
                    var options = room.Tiles.Where((p) =>
                    {
                        var j = p.Y * s.Width + p.X;
                        return j != i && Js.At(lower, j, null) == TileKinds.Floor && Js.At(lowerDecor, j, 0) == 0 && Js.At(upper, j, null) == TileKinds.Floor && Js.At(upperDecor, j, 0) == 0 && !Occupied(P(p.X, p.Y, z + 1)) && Clear(trial, p);
                    }).ToList();
                    Js.Sort(options, (a, b) => Math.Abs(a.X - at.X) + Math.Abs(a.Y - at.Y) - Math.Abs(b.X - at.X) - Math.Abs(b.Y - at.Y));
                    var moved = false;
                    foreach (var p in options)
                    {
                        var j = p.Y * s.Width + p.X;
                        foreach (var facing in new[] { up }.Concat(Enumerable.Range(0, Grid.Dirs4.Count).Where((k) => k != up)))
                        {
                            var d = Grid.Dirs4[facing];
                            var foot = P(p.X - d.X, p.Y - d.Y, z);
                            var landing = P(p.X + d.X, p.Y + d.Y, z + 1);
                            if (Grid.KindAt(trial, foot) != TileKinds.Floor || Grid.KindAt(trial, landing) != TileKinds.Floor) continue;
                            lower[j] = TileKinds.Ladder;
                            lowerDecor[j] = Decor.DecorCode(DecorKinds.Stairs, facing);
                            upper[j] = null;
                            var after = Reachable(trial, at);
                            var lost = before.Any((n) => Grid.KindAt(trial, Grid.PosOf(s, n)) != null && !after.Has(n));
                            if (!lost && Paths.StepBetween(trial, p, landing) != null && Paths.StepBetween(trial, landing, p) != null && after.Has(Grid.NodeOf(s, landing)))
                            {
                                tiles[i] = TileKinds.Floor;
                                decor[i] = 0;
                                tiles[j] = TileKinds.Ladder;
                                decor[j] = lowerDecor[j];
                                levels[z].Tiles[i] = TileKinds.Floor;
                                levels[z].Tiles[j] = null;
                                moved = changed = true;
                                break;
                            }
                            lower[j] = TileKinds.Floor;
                            lowerDecor[j] = 0;
                            upper[j] = TileKinds.Floor;
                        }
                        if (moved) break;
                    }
                }
            }
            // Moving a stairwell may change the lowest tile index used as a room id.
            if (changed && s.RoomsSearched != null) s.RoomsSearched = new JsSet<int>(searched.Select((e) =>
            {
                foreach (var p in e.room?.Tiles ?? new List<Pos>())
                {
                    var now = Looting.RoomAt(s, p);
                    if (now != null) return now.Id;
                }
                return e.id;
            })).ToList();
        }
    }
}
