#nullable enable
// The runs' stages in Setup (H5 and H6; new in C#): what CreateMission does for an Asedio (La Lata late, waves every two
// turns), a Travesía (the squad at the far end from La Lata, the horde behind it), a Guarida (the reward's box in a room,
// its guard round it) and a boss (the unit to kill). The Sigilo is a recovery with a flag (MissionState.Stealth).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Setup
    {
        /// <summary>An Asedio's La Lata: on this turn (Lore.StageLine's holdTurns).</summary>
        public const int HoldTurns = 8;
        /// <summary>A Guarida's guard and its group: the guard first.</summary>
        public static readonly IReadOnlyList<string> GuardPod = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid };
        /// <summary>The squad's start this far at least (tiles) from any group of the dead, on a Travesía.</summary>
        private const int CrossingClear = 5;

        // ----------------------------------------------------------------- Asedio

        /// <summary>
        /// An Asedio's waves (H5): one every two turns till La Lata comes, each bigger, by every edge in groups of
        /// SiegeGroup at most; a Corredor with the second, a Hinchado with the third, an Acorazado and a Corredor with the
        /// fourth.
        /// </summary>
        public static List<Reinforcement> HoldWaves(int difficulty, int until, int first)
        {
            var d = Math.Max(1, Math.Min(4, difficulty));
            var out_ = new List<Reinforcement>();
            var side = 0;
            for (var k = 1; 2 * k <= until; k++)
            {
                var units = Enumerable.Repeat(TemplateIds.Zombie, 2 + d + 2 * k).ToList();
                if (k == 2) units.Add(TemplateIds.Xenoid);
                if (k == 3) units.Add(TemplateIds.Bloater);
                if (k >= 4) units.AddRange(new[] { TemplateIds.Mec, TemplateIds.Xenoid });
                for (var i = 0; i < units.Count; i += SiegeGroup)
                {
                    out_.Add(new Reinforcement { Id = $"r{first + out_.Count}", Turn = 2 * k, Pos = null, Units = units.Skip(i).Take(SiegeGroup).ToList(), Landed = false, Horde = true, Side = side++ % 4 });
                }
            }
            return out_;
        }

        // --------------------------------------------------------------- Travesía

        /// <summary>A layout tile one can stand on at the ground, inside what is played.</summary>
        private static bool GroundTile(MissionLayout layout, int i)
        {
            return Grid.IsWalkable(layout.Tiles[i]) && Js.At(layout.Elev, i, 0) == 0 && !(layout.Ground != null && i < layout.Ground.Count && layout.Ground[i] == 4);
        }

        /// <summary>Steps by the ground (four ways) from `from` to every tile it reaches; -1 where it doesn't.</summary>
        private static int[] StepsFrom(MissionLayout layout, IEnumerable<int> from)
        {
            var steps = Enumerable.Repeat(-1, layout.Width * layout.Height).ToArray();
            var queue = new Queue<int>();
            foreach (var i in from)
            {
                if (steps[i] >= 0) continue;
                steps[i] = 0;
                queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                int x = i % layout.Width, y = i / layout.Width;
                foreach (var d in Grid.Dirs4)
                {
                    int nx = x + d.X, ny = y + d.Y;
                    if (nx < 0 || ny < 0 || nx >= layout.Width || ny >= layout.Height) continue;
                    var j = ny * layout.Width + nx;
                    if (steps[j] >= 0 || !GroundTile(layout, j)) continue;
                    steps[j] = steps[i] + 1;
                    queue.Enqueue(j);
                }
            }
            return steps;
        }

        /// <summary>
        /// A Travesía (H5): the squad starts at the far end from La Lata, on the ground one can walk from there to it the
        /// farthest from its zone as the crow flies, the spawns round it, clear of the dead. As it was if the map has no La
        /// Lata, if its start is that far already, or if there is no room there.
        /// </summary>
        private static void FarSpawns(MissionLayout layout)
        {
            if (layout.EvacZone.Count == 0 || layout.Spawns.Count == 0) return;
            var w = layout.Width;
            var steps = StepsFrom(layout, layout.EvacZone.Select((z) => z.Y * w + z.X).Where((i) => i >= 0 && i < layout.Tiles.Count));
            int Gap(int x, int y) => layout.EvacZone.Min((z) => Math.Max(Math.Abs(z.X - x), Math.Abs(z.Y - y)));
            var dead = layout.Pods.SelectMany((p) => p.Tiles).ToList();
            bool Clear(int i) => !dead.Any((p) => Math.Max(Math.Abs(p.X - i % w), Math.Abs(p.Y - i / w)) < CrossingClear);
            var far = -1;
            for (var i = 0; i < steps.Length; i++) if (steps[i] > 0 && Clear(i) && (far < 0 || Gap(i % w, i / w) > Gap(far % w, far / w))) far = i;
            if (far < 0 || Gap(far % w, far / w) <= layout.Spawns.Min((p) => Gap(p.X, p.Y))) return;
            var near = StepsFrom(layout, new[] { far });
            var spots = Enumerable.Range(0, near.Length).Where((i) => near[i] >= 0 && Clear(i) && steps[i] > 0).OrderBy((i) => near[i]).ThenBy((i) => i).Take(layout.Spawns.Count).ToList();
            if (spots.Count < layout.Spawns.Count) return;
            layout.Spawns = spots.Select((i) => new Vec2 { X = i % w, Y = i / w }).ToList();
        }

        /// <summary>The edge nearest the squad's start (Sim.ChooseDropZone's sides: 0 top, 1 right, 2 bottom, 3 left).</summary>
        private static int StartSide(MissionLayout layout)
        {
            var x = layout.Spawns.Average((p) => p.X);
            var y = layout.Spawns.Average((p) => p.Y);
            var gaps = new[] { y, layout.Width - 1 - x, layout.Height - 1 - y, x };
            return Array.IndexOf(gaps, gaps.Min());
        }

        // ---------------------------------------------------------------- Guarida

        /// <summary>A Guarida's box from La Lata, on foot (Networking, R2: opened at the far end, the squad never got back): 15 to 25.</summary>
        public static readonly IReadOnlyList<int> LairWalk = new[] { 15, 25 };

        /// <summary>
        /// Where a Guarida's box goes (H5): a tile of floor inside a building (a room), with floor all round it, so that it
        /// can be opened from any side; anywhere on the ground with none indoors. At mid distance from La Lata (LairWalk on
        /// foot, the nearer its middle the better: Gameplay, 17/11); with no La Lata, the farthest from the squad's start, as
        /// it was. Null with nowhere at all.
        /// </summary>
        private static Vec2? LairSpot(MissionLayout layout)
        {
            var w = layout.Width;
            var steps = StepsFrom(layout, layout.Spawns.Select((p) => p.Y * w + p.X));
            var evac = layout.EvacZone.Count > 0 ? StepsFrom(layout, layout.EvacZone.Select((z) => z.Y * w + z.X).Where((i) => i >= 0 && i < layout.Tiles.Count)) : null;
            var mid = (LairWalk[0] + LairWalk[1]) / 2.0;
            double Score(int i)
            {
                if (evac == null) return -steps[i];
                var n = evac[i];
                if (n < 0) return 1e6;
                var off = n < LairWalk[0] ? LairWalk[0] - n : n > LairWalk[1] ? n - LairWalk[1] : 0;
                return off * 100 + Math.Abs(n - mid);
            }
            var taken = new HashSet<int>(layout.Pods.SelectMany((p) => p.Tiles).Select((p) => p.Y * w + p.X).Concat(layout.EvacZone.Select((p) => p.Y * w + p.X)));
            bool Open(int i) => Grid.Dirs4.All((d) =>
            {
                int x = i % w + d.X, y = i / w + d.Y;
                return x >= 0 && y >= 0 && x < w && y < layout.Height && GroundTile(layout, y * w + x);
            });
            int? Best(bool indoors)
            {
                var best = -1;
                for (var i = 0; i < steps.Length; i++)
                {
                    if (steps[i] <= 0 || taken.Contains(i) || layout.Tiles[i] != TileKinds.Floor || !Open(i)) continue;
                    if (indoors && !(layout.Ground != null && i < layout.Ground.Count && layout.Ground[i] == 2)) continue;
                    if (best < 0 || Score(i) < Score(best)) best = i;
                }
                return best >= 0 ? best : (int?)null;
            }
            var at = Best(true) ?? Best(false);
            return at != null ? new Vec2 { X = at.Value % w, Y = at.Value / w } : null;
        }

        /// <summary>
        /// A Guarida (H5): the reward's box (a weapons crate on the map) on its spot, and its guard and group on the floor
        /// nearest it, a group of their own, unaware. The box's tile in MissionState.lair.
        /// </summary>
        private static Pos? PlaceLair(MissionLayout layout, string guard, List<Unit> units, List<Pod> pods, ref int alienIndex)
        {
            var spot = LairSpot(layout);
            if (spot == null) return null;
            var w = layout.Width;
            var box = spot.Y * w + spot.X;
            layout.Tiles[box] = TileKinds.Low;
            if (layout.Decor != null && box < layout.Decor.Count) layout.Decor[box] = Decor.DecorCode(DecorKinds.LootWeapons);
            var near = StepsFrom(layout, Grid.Dirs4.Select((d) => (spot.Y + d.Y) * w + spot.X + d.X).Where((i) => i >= 0 && i < layout.Tiles.Count && GroundTile(layout, i)));
            var used = new HashSet<int>(units.Select((u) => u.Pos.Y * w + u.Pos.X).Concat(layout.Spawns.Select((p) => p.Y * w + p.X)));
            var members = new[] { guard }.Concat(GuardPod).ToList();
            var tiles = Enumerable.Range(0, near.Length).Where((i) => near[i] >= 0 && !used.Contains(i)).OrderBy((i) => near[i]).ThenBy((i) => i).Take(members.Count).ToList();
            var pod = new Pod { Id = "pg", Active = false, UnitIds = new List<string>(), Patrol = new List<Pos>(), PatrolIndex = 0 };
            for (var k = 0; k < tiles.Count; k++)
            {
                var id = $"a{++alienIndex}";
                var u = MakeUnit(id, members[k], Content.Templates[members[k]].Name, null, new Pos { X = tiles[k] % w, Y = tiles[k] / w, Z = 0 }, pod.Id);
                // Facing the box it keeps.
                u.Facing = Los.FacingTo(u.Pos, spot) ?? u.Facing;
                units.Add(u);
                pod.UnitIds.Add(id);
            }
            if (pod.UnitIds.Count > 0) pods.Add(pod);
            return new Pos { X = spot.X, Y = spot.Y, Z = 0 };
        }

        // ------------------------------------------------------ the hospital only inside

        /// <summary>
        /// The dead of a map that is one building (H7; Gameplay, 16/11: about a third on each storey, in rooms, «patients in
        /// beds fit the place»): its groups and its lone dead, shared out over its storeys, the biggest first to the storey
        /// with fewest so far; up there each group in a room of its own, not the corridor (a storey's biggest), a ward first
        /// (MedicalUses.Ward), beside its beds (a bed is furniture: nobody stands on it) and then round them, its round that
        /// room. On the ground floor, where they were; a Guarida's guard by its box. As many as before.
        /// </summary>
        private static void UpstairsDead(MissionLayout layout, List<Unit> units, List<Pod> pods, Rng rng)
        {
            var storeys = layout.Storeys;
            var levels = layout.Levels;
            if (storeys == null || levels == null) return;
            var w = layout.Width;
            var area = w * layout.Height;
            if (storeys.Count != area) return;
            var floors = 1;
            while (floors <= levels.Count && storeys.Any((k) => k > floors)) floors++;
            if (floors < 2) return;
            var groups = pods
                .Select((p, k) => (Pod: p, K: k, Units: p.UnitIds.Select((id) => units.First((u) => u.Id == id)).ToList()))
                .Where((g) => g.Pod.Id != "pg" && g.Units.Count > 0 && g.Units.All((u) => Content.Templates[u.Template].Undead))
                .OrderByDescending((g) => g.Units.Count)
                .ThenBy((g) => g.K)
                .ToList();
            // Each storey's rooms: its floor joined side by side, not through doors, flights nor ladders.
            var rooms = new Dictionary<int, List<List<int>>>();
            var wards = new Dictionary<int, HashSet<int>>();
            var byBed = new Dictionary<int, HashSet<int>>();
            var taken = new HashSet<int>(units.Select((u) => u.Pos.Z * area + u.Pos.Y * w + u.Pos.X));
            var used = new HashSet<(int, int)>();
            for (var z = 1; z < floors; z++)
            {
                var level = levels[z - 1];
                if (level.Tiles.Count != area) continue;
                bool Floor(int i) => storeys[i] > z && level.Tiles[i] == TileKinds.Floor && !Flights.LongFlightTile(level.Tiles, level.Decor, w, i);
                var of = Enumerable.Repeat(-1, area).ToArray();
                var list = new List<List<int>>();
                for (var start = 0; start < area; start++)
                {
                    if (of[start] >= 0 || !Floor(start)) continue;
                    var tiles = new List<int> { start };
                    of[start] = list.Count;
                    for (var k = 0; k < tiles.Count; k++)
                    {
                        int x = tiles[k] % w, y = tiles[k] / w;
                        foreach (var d in Grid.Dirs4)
                        {
                            int nx = x + d.X, ny = y + d.Y;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= layout.Height) continue;
                            var j = ny * w + nx;
                            if (of[j] >= 0 || !Floor(j)) continue;
                            of[j] = list.Count;
                            tiles.Add(j);
                        }
                    }
                    list.Add(tiles);
                }
                // Not the corridor.
                if (list.Count > 1) list.Remove(list.OrderByDescending((r) => r.Count).ThenBy((r) => r[0]).First());
                rooms[z] = list;
                var sites = layout.Medical ?? new List<MedicalSite>();
                wards[z] = new HashSet<int>(sites.SelectMany((m) => m.Rooms ?? new List<MedicalRoom>()).Where((r) => r.Z == z && r.Use == MedicalUses.Ward).SelectMany((r) => Enumerable.Range(r.Y, r.H).SelectMany((y) => Enumerable.Range(r.X, r.W).Select((x) => y * w + x))));
                var beds = sites.SelectMany((m) => m.Dressing ?? new List<MedicalDetail>()).Where((d) => d.Z == z && (d.Kind == MedicalDetailKinds.CoveredBed || d.Kind == MedicalDetailKinds.BareBed)).Select((d) => (d.X, d.Y)).ToList();
                byBed[z] = new HashSet<int>(beds.SelectMany((b) => Grid.Dirs4.Select((d) => (b.X + d.X, b.Y + d.Y))).Where((p) => p.Item1 >= 0 && p.Item2 >= 0 && p.Item1 < w && p.Item2 < layout.Height).Select((p) => p.Item2 * w + p.Item1));
            }
            var count = new int[floors];
            foreach (var g in groups)
            {
                var placed = false;
                foreach (var z in Enumerable.Range(0, floors).OrderBy((k) => count[k]).ThenBy((k) => k))
                {
                    if (z == 0)
                    {
                        placed = true;
                        count[0] += g.Units.Count;
                        break;
                    }
                    if (!rooms.TryGetValue(z, out var list)) continue;
                    List<int> Free(List<int> room) => room.Where((i) => !taken.Contains(z * area + i)).ToList();
                    var fit = list.Select((r, k) => (R: r, K: k)).Where((r) => Free(r.R).Count >= g.Units.Count).ToList();
                    if (fit.Count == 0) continue;
                    // A room nobody has yet, a ward, first.
                    var best = fit.Where((r) => !used.Contains((z, r.K))).ToList();
                    if (best.Count == 0) best = fit;
                    var ward = best.Where((r) => r.R.Any(wards[z].Contains)).ToList();
                    if (ward.Count > 0) best = ward;
                    var (room, index) = best[rng.Int(best.Count)];
                    used.Add((z, index));
                    var free = Free(room);
                    // Beside its beds first, then round the first of them.
                    var first = free.Where(byBed[z].Contains).OrderBy((i) => i).ToList();
                    var anchor = first.Count > 0 ? first[0] : free.OrderBy((i) => i).First();
                    var spots = first.Concat(free.Where((i) => !byBed[z].Contains(i)).OrderBy((i) => Math.Max(Math.Abs(i % w - anchor % w), Math.Abs(i / w - anchor / w))).ThenBy((i) => i)).Take(g.Units.Count).ToList();
                    for (var k = 0; k < g.Units.Count; k++)
                    {
                        var u = g.Units[k];
                        taken.Remove(u.Pos.Z * area + u.Pos.Y * w + u.Pos.X);
                        u.Pos = new Pos { X = spots[k] % w, Y = spots[k] / w, Z = z };
                        taken.Add(z * area + spots[k]);
                    }
                    g.Pod.Patrol = new List<Pos> { new Pos { X = anchor % w, Y = anchor / w, Z = z } };
                    g.Pod.PatrolIndex = 0;
                    count[z] += g.Units.Count;
                    placed = true;
                    break;
                }
                if (!placed) count[0] += g.Units.Count;
            }
        }

        /// <summary>
        /// The caged (H9; Arturo, 17/11: «los encerrados van hacia las verjas»): one of the dead in about half a police
        /// station's cells, one at least, in every node (a Limpieza too: they come to the bars and are struck through them).
        /// Each a group of its own, awake from the start, on the first floor tile of its cell. The lone dead and the waves
        /// keep out of the cells still.
        /// </summary>
        private static void CagedDead(MissionLayout layout, List<Unit> units, List<Pod> pods, ref int alienIndex, Rng rng)
        {
            var cells = (layout.Interiors ?? new List<InteriorSite>()).SelectMany((x) => x.Rooms).Where((r) => r.Use == PoliceUses.Cells).ToList();
            if (cells.Count == 0) return;
            var held = cells.Where((_) => rng.Int(2) == 0).ToList();
            if (held.Count == 0) held.Add(cells[rng.Int(cells.Count)]);
            var w = layout.Width;
            var k = 0;
            foreach (var r in held)
            {
                var spot = Enumerable.Range(r.Y, r.H).SelectMany((y) => Enumerable.Range(r.X, r.W).Select((x) => new Pos { X = x, Y = y, Z = r.Z }))
                    .FirstOrDefault((p) => layout.Tiles[p.Y * w + p.X] == TileKinds.Floor && !units.Any((u) => Grid.SamePos(u.Pos, p)));
                if (spot == null) continue;
                var id = $"a{++alienIndex}";
                var pod = new Pod { Id = $"c{++k}", Active = true, UnitIds = new List<string> { id }, Patrol = new List<Pos>(), PatrolIndex = 0 };
                var u = MakeUnit(id, TemplateIds.Zombie, Content.Templates[TemplateIds.Zombie].Name, null, spot, pod.Id);
                u.Caged = true;
                units.Add(u);
                pods.Add(pod);
            }
        }

        /// <summary>A Guarida's guard (H5), by the stage's map seed: an Acorazado or a Chillón.</summary>
        public static string GuardOf(int seed)
        {
            return Math.Abs(seed) % 2 == 0 ? TemplateIds.Mec : TemplateIds.Sectoid;
        }

        /// <summary>
        /// G23.1 La llave (Arturo, 18/11, cuadros 71, 73 y 79; .dev-snapshots/gameplay/g23/LLAVE.md): the police station's
        /// armoury locked (MissionState.Locked: its door), all its furniture full (MissionState.Pieces), nobody in it; its key
        /// (MissionState.Key, a gear of the fight's) on one of the dead in uniform the squad can get to and not caged
        /// (MissionState.KeyHolder), a common one put in a policeman's uniform where none is. After OnlyWithinReach.
        /// </summary>
        public static void KeyArmory(GameState s, Rng rng)
        {
            var station = (s.Interiors ?? new List<InteriorSite>()).FirstOrDefault((x) => x.Kind == InteriorKinds.Police);
            var armory = station?.Rooms.FirstOrDefault((r) => r.Use == PoliceUses.Armory && r.Z == 0);
            if (station == null || armory == null || s.Run == null) return;
            s.Mission.KeyArmory = true;
            bool In(int x, int y) => x >= armory.X && y >= armory.Y && x < armory.X + armory.W && y < armory.Y + armory.H;
            // Its door (in its walls), shut and locked.
            var locked = new List<int>();
            for (var y = armory.Y - 1; y <= armory.Y + armory.H; y++)
            {
                for (var x = armory.X - 1; x <= armory.X + armory.W; x++)
                {
                    if (In(x, y) || !Grid.InBounds(s, x, y)) continue;
                    var i = Grid.TileIndex(s, x, y);
                    if (s.Tiles[i] != TileKinds.Door && s.Tiles[i] != TileKinds.DoorOpen) continue;
                    s.Tiles[i] = TileKinds.Door;
                    locked.Add(Grid.NodeOf(s, new Pos { X = x, Y = y, Z = 0 }));
                }
            }
            if (locked.Count == 0) return;
            s.Mission.Locked = locked;
            // All its furniture full.
            var pieces = s.Mission.Pieces ??= new List<int>();
            foreach (var d in station.Dressing.Where((d) => d.Z == 0 && In(d.X, d.Y) && SearchPieces.Police.Contains(d.Kind)))
            {
                var at = new Pos { X = d.X, Y = d.Y, Z = 0 };
                var node = Grid.NodeOf(s, at);
                if (SearchPieces.Standing(s, at, d.Kind) && !pieces.Contains(node)) pieces.Add(node);
            }
            // Nobody in it: the dead there out to the nearest free tiles of the station.
            var soldier = s.Units.FirstOrDefault((u) => u.Team == Teams.Xcom && u.Alive);
            var free = Enumerable.Range(0, s.Width * s.Height).Select((i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 })
                .Where((p) => !In(p.X, p.Y) && Grid.IsWalkable(Grid.KindAt(s, p)) && Flights.CanStopAt(s, p) && InteriorSite.UseAt(s, p) != null && InteriorSite.UseAt(s, p) != PoliceUses.Cells)
                .OrderBy((p) => Math.Abs(p.X - (armory.X + armory.W / 2)) + Math.Abs(p.Y - (armory.Y + armory.H / 2))).ThenBy((p) => Grid.NodeOf(s, p)).ToList();
            foreach (var u in s.Units.Where((u) => u.Alive && u.Team == Teams.Alien && u.Pos.Z == 0 && In(u.Pos.X, u.Pos.Y)).ToList())
            {
                var to = free.FirstOrDefault((p) => Grid.UnitAt(s, p) == null);
                if (to != null) u.Pos = to;
            }
            // Its key, on one of the dead in uniform the squad gets to.
            if (soldier == null) return;
            var reach = new HashSet<int>(Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Keys);
            bool Can(Unit u) => u.Alive && u.Team == Teams.Alien && !u.Captive && u.Template == TemplateIds.Zombie && reach.Contains(Grid.NodeOf(s, u.Pos))
                && InteriorSite.UseAt(s, u.Pos) != PoliceUses.Cells;
            var uniformed = s.Units.Where((u) => Can(u) && (u.Outfit == Outfits.Police || u.Outfit == Outfits.Military)).ToList();
            if (uniformed.Count == 0)
            {
                var common = s.Units.Where(Can).ToList();
                if (common.Count == 0) return;
                var dressed = rng.Pick(common);
                dressed.Outfit = Outfits.Police;
                uniformed.Add(dressed);
            }
            var holder = rng.Pick(uniformed);
            s.Mission.KeyHolder = holder.Id;
            var maker = new RunLoot.Maker(s.Run.NextGear);
            s.Mission.Key = maker.Make(GearKinds.Key, null)[0];
            s.Run.NextGear = maker.Next;
        }

        /// <summary>
        /// A run's fight (Red, R3 17/11): what there is to search that no soldier gets beside on foot from the squad is out of
        /// reach (MissionState.OutOfReach: never a spot to search), and no such car is a police car. A town's outline shuts up
        /// the ground nobody can get to (Mapgen's SealPockets) but not what stands on it, so a lot's cars were left whole in its
        /// wall. Not mended there: the step recordings' maps would change.
        /// </summary>
        public static void OnlyWithinReach(GameState s)
        {
            var soldier = s.Units.FirstOrDefault((u) => u.Team == Teams.Xcom && u.Alive && !u.Evacuated);
            if (soldier == null) return;
            var reach = new HashSet<int>(Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Keys);
            var area = s.Width * s.Height;
            var out_ = new List<int>();
            for (var z = 0; z <= (s.Levels?.Count ?? 0); z++)
            {
                for (var i = 0; i < area; i++)
                {
                    var p = new Pos { X = i % s.Width, Y = i / s.Width, Z = z };
                    if (Looting.SearchSpot(s, p) == null) continue;
                    var beside = Grid.Dirs8.Any((d) =>
                    {
                        var from = new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = z };
                        return Grid.InBounds(s, from.X, from.Y) && reach.Contains(Grid.NodeOf(s, from)) && Looting.ReachesToSearch(s, from, p);
                    });
                    if (!beside) out_.Add(Grid.NodeOf(s, p));
                }
            }
            if (out_.Count == 0) return;
            s.Mission.OutOfReach = out_;
            var police = s.Mission.PoliceCar ?? new List<int>();
            var kept = new List<int>();
            for (var k = 0; k + 1 < police.Count; k += 2)
            {
                if (out_.Contains(police[k + 1])) continue;
                kept.Add(police[k]);
                kept.Add(police[k + 1]);
            }
            s.Mission.PoliceCar = kept.Count > 0 ? kept : null;
        }
    }
}
