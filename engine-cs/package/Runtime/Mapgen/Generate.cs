#nullable enable
// mapgen.ts, its last part: the map's lights and names, the orchestrator (generateLayout: the passes in order, the final
// mirror/transpose, the pods, spawns, objectives, lights, furnishing and dressing, the MissionLayout it returns), the
// pockets the outline shuts (sealPockets), the outline turned (turnPiece), the mission's walk (missionRoute) and the
// hand-made maps' layout (handmadeLayout).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>How far apart the map's lights stand: street lights in town, floodlights in a facility, a lamp now and then along a track.</summary>
        private static readonly IReadOnlyDictionary<string, int> LightSpacing = new Dictionary<string, int>
        {
            [Biomes.City] = 8,
            [Biomes.Facility] = 9,
            [Biomes.Wilds] = 15,
        };

        /// <summary>A town's street lights (docs/MAPAS.md §4.7, §6): every 11 tiles, lighting three round them.</summary>
        private static class TownLight
        {
            public const int Spacing = 11;
            public const int Radius = 3;
        }

        /// <summary>
        /// Where the map's lights stand (docs/ALTURA-Y-NOCHE.md): on the pavement
        /// beside a street (beside a track in the wilds), LIGHT_SPACING apart. At night
        /// they light NIGHT.streetLight tiles round them. Picked on a seed of their own.
        /// </summary>
        private static List<Vec2> StreetLights(Canvas c, string biome, int seed, bool town = false)
        {
            var rng = new Rng(seed ^ 0x11647);
            bool Kerb(Vec2 p) =>
                c.Get(p.X, p.Y) == TileKinds.Floor &&
                c.ElevAt(p.X, p.Y) == 0 &&
                c.GroundAt(p.X, p.Y) == Open &&
                // At a factory, along its yard lanes too (playtest 15/10, #17).
                Grid.Dirs4.Any((d) => c.In(p.X + d.X, p.Y + d.Y) && (c.GroundAt(p.X + d.X, p.Y + d.Y) == Road || (biome == Biomes.Facility && c.GroundAt(p.X + d.X, p.Y + d.Y) == Yard)));
            var out_ = new List<Vec2>();
            foreach (var p in Shuffle(TilesIn(Rect(0, 0, c.Width, c.Height)).Where(Kerb).ToList(), rng))
            {
                if (out_.All((q) => Dist(p, q) >= (town ? TownLight.Spacing : LightSpacing[biome]))) out_.Add(p);
            }
            return out_;
        }

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Names = new Dictionary<string, IReadOnlyList<string>>
        {
            [Biomes.City] = new[] { "Distrito Portuario", "Barrio Viejo", "Centro Financiero", "Barrio de la Estación", "Ensanche", "Mercado Central", "Zona Universitaria" },
            [Biomes.Wilds] = new[] { "Bosque de Lirio", "Valle Hondo", "Granjas del Sur", "Pinar Quemado", "Ruinas de la ermita", "Las afueras" },
            [Biomes.Facility] = new[] { "Conservera del Norte", "Nave 14", "Planta química", "Almacén frigorífico", "Fábrica de piensos" },
        };

        /// <summary>Names of the scenarios' maps (Lore's to change).</summary>
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SiteNames = new Dictionary<string, IReadOnlyList<string>>
        {
            [Sites.Roadside] = new[] { "Área de servicio del Pinar", "El aserradero viejo", "Motel Los Abetos", "La gasolinera del alto" },
            [Sites.Industrial] = new[] { "Polígono Este", "Los muelles de carga", "Naves del Sur", "Polígono Norte" },
        };

        // interface GenOptions is GenOptions (Layout.cs).

        // In mapgen.ts generateLayout's doc comment ("Builds a full battlefield for a mission. Deterministic for a given
        // seed.") sits before noCornerDoors's, which came in between; here it is on GenerateLayout.

        /// <summary>
        /// A double door one of whose leaves came on a building's corner, where the wall that turns reaches it (playtest 23/10
        /// #11: whoever went in and turned brushed that wall's end): that leaf is wall, and the other a single door. Only
        /// where the other leaf is there, so no building loses its way in.
        /// </summary>
        private static void NoCornerDoors(Canvas c)
        {
            bool IsDoor(int x, int y) => c.In(x, y) && c.Get(x, y) == TileKinds.Door;
            bool Solid(int x, int y) => c.In(x, y) && (c.Get(x, y) == TileKinds.Wall || c.Get(x, y) == TileKinds.Window || c.Get(x, y) == TileKinds.WindowBroken);
            for (var y = 0; y < c.Height; y++)
            {
                for (var x = 0; x < c.Width; x++)
                {
                    if (!IsDoor(x, y)) continue;
                    // Its twin along the wall one way; the wall that turns, across it.
                    var along = IsDoor(x - 1, y) || IsDoor(x + 1, y) ? "x" : IsDoor(x, y - 1) || IsDoor(x, y + 1) ? "y" : null;
                    if (along == null) continue;
                    var turns = along == "x" ? Solid(x, y - 1) || Solid(x, y + 1) : Solid(x - 1, y) || Solid(x + 1, y);
                    if (turns) c.Force(x, y, TileKinds.Wall);
                }
            }
        }

        /// <summary>Builds a full battlefield for a mission. Deterministic for a given seed.</summary>
        public static MissionLayout GenerateLayout(GenOptions opts)
        {
            var rng = new Rng(opts.Seed);
            var biome = opts.Biome;
            var kind = opts.Kind;
            var clinic = biome == Biomes.City && ((kind == MissionKinds.Recovery && opts.Clinic == true) || opts.Medical == MedicalSiteKinds.Clinic || opts.Medical == MedicalSiteKinds.Ward);
            var hospital = biome == Biomes.City && ((kind == MissionKinds.Rescue && opts.Hospital == true) || opts.Medical == MedicalSiteKinds.Hospital);
            // A scenario only where it belongs: the service area in the wilds, the industrial estate in a city.
            var site = (opts.Site == Sites.Roadside && biome == Biomes.Wilds) || (opts.Site == Sites.Industrial && biome == Biomes.City) ? opts.Site : null;
            var (W, H) = GeneratedSize(biome, site, hospital);
            var c = new Canvas(W, H);
            // The finished map is mirrored and/or transposed, so the squad may start in any corner.
            var flipX = rng.Int(2) == 1;
            var flipY = rng.Int(2) == 1;
            var swap = rng.Int(2) == 1;
            var needsEvac = kind != MissionKinds.Elimination;
            var ward = opts.Medical == MedicalSiteKinds.Ward;
            var outdoorItem = kind == MissionKinds.Recovery && opts.ItemOutdoors == true && !clinic;
            var role = clinic || hospital || (!outdoorItem && (kind == MissionKinds.Recovery || kind == MissionKinds.Rescue || (kind == MissionKinds.Hack && biome != Biomes.Wilds))) ? Roles.Indoor : Roles.Open;

            // Keep the squad's corner clear.
            for (var y = 0; y < 5; y++) for (var x = 0; x < 5; x++) c.Reserve(x, y);
            var style = PickStyle(rng);
            (Rect Objective, bool Inside)? compound = null;
            if (biome == Biomes.Facility) compound = Facility(c, rng, style, role);
            (Rect Objective, TownPlan Plan)? built = null;
            if (IsTown(biome, site)) built = Town(c, rng, style, role, clinic, hospital, ward);
            else if (IsEstate(biome, site)) built = Estate(c, rng, style, role);
            var plan = built?.Plan;
            Rect objRect;
            if (compound != null) objRect = compound.Value.Objective;
            else if (built != null) objRect = built.Value.Objective;
            else if (biome == Biomes.City)
            {
                // `{ ...style, industrial: site === 'industrial' }`: a copy.
                var cityStyle = new Style
                {
                    House = style.House,
                    HouseDepth = style.HouseDepth,
                    Gap = style.Gap,
                    Office = style.Office,
                    OfficeDepth = style.OfficeDepth,
                    Hall = style.Hall,
                    WindowEvery = style.WindowEvery,
                    Furniture = style.Furniture,
                    Organic = style.Organic,
                    Industrial = site == Sites.Industrial,
                };
                objRect = City(c, rng, cityStyle, role, clinic, hospital, ward);
            }
            else if (site == Sites.Roadside) objRect = Roadside(c, rng, style, role);
            else objRect = Wilds(c, rng, style, role);
            ThinClutter(c, opts.Seed);
            // Something to take cover behind on the squad's first move: barricades and crates, rocks in the wilds. In town, only
            // with nothing else a few tiles round (the squad's square has its fountain, benches and trees: playtest 18/10, #12).
            var posts = new[] { Barricade, Corner, Pair }.Select((parts) => (IReadOnlyList<Part>)(biome == Biomes.Wilds ? Dressed(parts, DecorKinds.Rock) : Dressed(parts, DecorKinds.Barricade, DecorKinds.Crates))).ToList();
            bool Bare(Vec2 p) => biome != Biomes.City || TilesIn(Rect(p.X - 3, p.Y - 3, 7, 7)).All((q) => c.Get(q.X, q.Y) != TileKinds.Low && c.Get(q.X, q.Y) != TileKinds.High);
            // In a town, down the squad's street from where it gathers: two either side four tiles in, one eight in.
            var sq = c.Squad;
            var entrySide = c.SquadSide;
            var inward = entrySide != null ? Grid.Dirs4[(entrySide.Value + 2) % 4] : V(1, 1);
            var across = entrySide != null ? Grid.Dirs4[(entrySide.Value + 1) % 4] : V(0, 0);
            List<Vec2> postSpots;
            if (entrySide != null)
            {
                postSpots = new[] { -1, 1 }.Select((k) => V(sq.X + inward.X * 4 + across.X * 3 * k, sq.Y + inward.Y * 4 + across.Y * 3 * k)).ToList();
                postSpots.Add(V(sq.X + inward.X * 8, sq.Y + inward.Y * 8));
            }
            else postSpots = new List<Vec2> { V(8, 3), V(3, 8), V(8, 8) };
            foreach (var at in postSpots) if (Bare(at)) GroupNear(c, at, rng, posts, 1, 0, 5);

            var evacZone = new List<Vec2>();
            BusPlace? townBus = null;
            if (needsEvac && c.Exits.Count > 0)
            {
                // A town's on a way out, the bus by the kerb; the country's by the far end of its track (its own seed); else
                // somewhere between the squad and the objective.
                var evac = AvenueEvac(c, objRect, new Rng(opts.Seed ^ 0x6576)) ?? AvenueEvac(c, objRect, new Rng(opts.Seed ^ 0x6577), true);
                if (evac != null)
                {
                    evacZone = evac.Value.Zone;
                    townBus = evac.Value.Bus;
                }
                else
                {
                    var mid = Centre(objRect);
                    evacZone = CarveEvac(c, Rect(0, 0, W, H), V((int)Js.Round((mid.X + sq.X) / 2), (int)Js.Round((mid.Y + sq.Y) / 2)), rng);
                }
            }
            else if (needsEvac)
            {
                // In a facility entered from the apron the evac is inside the fence (a yard by the far wall).
                var top = compound != null && !compound.Value.Inside ? FacilityInside : 0;
                var corner = V(W - 1, top);
                evacZone = CarveEvac(c, Rect(W - 14, top, 14, 14), corner, rng);
                if (evacZone.Count == 0) evacZone = CarveEvac(c, Rect(W - 22, top, 22, 22), corner, rng);
            }
            // The bus waits beside it, drawn on its own seed so nothing else in the layout moves.
            var bus = townBus ?? (evacZone.Count > 0 ? ParkBus(c, evacZone, new Rng(opts.Seed ^ 0x6275)) : null);
            PlaceLootBoxes(c, opts.Seed);
            // What the evacuation and the bus's way out cut of a car goes whole.
            TidyObjects(c);
            var startTile = V(sq.X, sq.Y);
            OpenDeadEnds(c, startTile);

            var reach = Connect(c, startTile);
            // A way cut through a car or a lorry (connect: only where nothing else opens one) takes the rest of it.
            if (TidyObjects(c)) reach = Flood(c, startTile);
            if (c.Frame.Count > 0) SealPockets(c, reach);
            var taken = new JsSet<int>();
            bool Standable(Vec2 p) => c.In(p.X, p.Y) && reach.Has(c.I(p.X, p.Y)) && Grid.CanStandOn(c.Get(p.X, p.Y)) && !taken.Has(c.I(p.X, p.Y));

            // Squad: the tiles closest to where it comes in (the corner, or a town's way in), none side by side with another:
            // packed shoulder to shoulder, the one in the middle had nowhere to step (nobody walks through anybody since 0.8.0,
            // 20/10). Where there is no room so, the closest left.
            var closest = TilesIn(Rect(sq.X - 11, sq.Y - 11, 23, 23))
                .Where((p) => Standable(p) && c.ElevAt(p.X, p.Y) == 0)
                .ToList();
            Js.Sort(closest, (Vec2 a, Vec2 b) =>
            {
                // `dist(a, startTile) - dist(b, startTile) || a.y - b.y || a.x - b.x`.
                var d = Dist(a, startTile) - Dist(b, startTile);
                if (d != 0 && !double.IsNaN(d)) return d;
                return a.Y - b.Y != 0 ? a.Y - b.Y : a.X - b.X;
            });
            var spawns = new List<Vec2>();
            foreach (var p in closest) if (spawns.Count < MaxSquad && !spawns.Any((q) => Math.Abs(q.X - p.X) + Math.Abs(q.Y - p.Y) == 1)) spawns.Add(p);
            foreach (var p in closest) if (spawns.Count < MaxSquad && !spawns.Any((q) => ReferenceEquals(q, p))) spawns.Add(p);
            foreach (var p in spawns) taken.Add(c.I(p.X, p.Y));
            // spawns.reduce((acc, p) => ({ x: acc.x + p.x / spawns.length, y: acc.y + p.y / spawns.length }), { x: 0, y: 0 }): fractional.
            double centreX = 0, centreY = 0;
            foreach (var p in spawns)
            {
                centreX = centreX + (double)p.X / spawns.Count;
                centreY = centreY + (double)p.Y / spawns.Count;
            }
            var squadCenter = new Pt(centreX, centreY);

            // Objective.
            var objTiles = Shuffle(TilesIn(objRect).Where((p) => Standable(p) && !evacZone.Any((z) => z.X == p.X && z.Y == p.Y)).ToList(), rng);
            int Enclosed(Vec2 p) => Grid.Dirs4.Count((d) => c.Get(p.X + d.X, p.Y + d.Y) == TileKinds.Wall);
            Vec2? item = null;
            Vec2? terminal = null;
            Vec2? objectiveSpot = null;
            if (kind == MissionKinds.Recovery || kind == MissionKinds.Rescue)
            {
                // Tucked away indoors, or for an item out in the open (#27) the most open tile outdoors.
                var outside = outdoorItem ? objTiles.Where((p) => c.GroundAt(p.X, p.Y) != Indoor).ToList() : new List<Vec2>();
                // Door and window tiles also carry INDOOR. The clinic's objective must be
                // on actual room floor, with a tile between it and the exterior walls.
                var medicineRoom = clinic ? c.Buildings.FirstOrDefault((b) => ReferenceEquals(b.Outer, objRect))?.MedicalRooms?.FirstOrDefault((r) => r.Use == MedicalUses.Equipment || r.Use == MedicalUses.Treatment) : null;
                var clinicFloor = clinic ? objTiles.Where((p) => InRect(Inset(objRect, 2), p) && c.Get(p.X, p.Y) == TileKinds.Floor && c.GroundAt(p.X, p.Y) == Indoor).ToList() : objTiles;
                var medicineRect = medicineRoom != null ? Rect(medicineRoom.X, medicineRoom.Y, medicineRoom.W, medicineRoom.H) : null;
                var inMedicineRoom = medicineRect != null ? clinicFloor.Where((p) => InRect(medicineRect, p)).ToList() : new List<Vec2>();
                var inside = inMedicineRoom.Count > 0 ? inMedicineRoom : clinicFloor;
                var spot = outdoorItem && outside.Count > 0 ? Best(outside, (p) => Enclosed(p)) : Best(inside, (p) => -Enclosed(p) - (c.GroundAt(p.X, p.Y) == Indoor ? 2 : 0));
                if (kind == MissionKinds.Recovery) item = spot;
                else objectiveSpot = spot;
            }
            else if (kind == MissionKinds.Sabotage)
            {
                objectiveSpot = objTiles.FirstOrDefault((p) => c.ElevAt(p.X, p.Y) == 0 && Grid.Dirs8.Count((d) => Standable(V(p.X + d.X, p.Y + d.Y))) >= 5) ?? (objTiles.Count > 0 ? objTiles[0] : null);
                // A patrol car (1 × 3) or an ambulance (2 × 4) stands over the siren's tile and the rest of its footprint
                // (#14; decor.ts FOOTPRINT): its nose ahead of the siren, the rest blocked, `sirenCar` the tile before the siren's
                // and the others its parts; on the paving and with room round it if it can, and never where it would close a way.
                // Where the whole of it doesn't fit, a shorter one: down to the two tiles it took before.
                if (objectiveSpot != null && (opts.Alarm == Alarms.Police || opts.Alarm == Alarms.Ambulance))
                {
                    var at = objectiveSpot;
                    int Room(Vec2 p) => Grid.Dirs8.Count((d) => Standable(V(p.X + d.X, p.Y + d.Y)));
                    var sizes = opts.Alarm == Alarms.Ambulance
                        ? new IReadOnlyList<int>[] { Decor.Footprint.Ambulance, Decor.Footprint.PatrolCar, new[] { 1, 2 } }
                        : new IReadOnlyList<int>[] { Decor.Footprint.PatrolCar, new[] { 1, 2 } };
                    var options = new List<(int K, int F, List<Vec2> Cells)>();
                    for (var k = 0; k < sizes.Length; k++)
                    {
                        var wide = sizes[k][0];
                        var deep = sizes[k][1];
                        for (var f = 0; f < Grid.Dirs4.Count; f++)
                        {
                            foreach (var side in wide > 1 ? new[] { (f + 1) % 4, (f + 3) % 4 } : new[] { (f + 1) % 4 })
                            {
                                var cells = Footprint(at, f, side, wide, deep).Skip(1).ToList();
                                // The tile before the siren's first: the car's anchor.
                                var nose = Add(at, Grid.Dirs4[f]);
                                var withNose = new List<Vec2> { nose };
                                withNose.AddRange(cells.Where((p) => p.X != nose.X || p.Y != nose.Y));
                                options.Add((k, f, withNose));
                            }
                        }
                    }
                    options = options.Where((o) => o.Cells.All((p) => Standable(p) && c.Free(p.X, p.Y))).ToList();
                    int Paved(List<Vec2> cells) => cells.Count((p) => c.GroundAt(p.X, p.Y) != Open);
                    Js.Sort(options, (a, b) =>
                        a.K - b.K != 0 ? a.K - b.K
                        : Paved(b.Cells) - Paved(a.Cells) != 0 ? Paved(b.Cells) - Paved(a.Cells)
                        : Room(b.Cells[0]) - Room(a.Cells[0]) != 0 ? Room(b.Cells[0]) - Room(a.Cells[0])
                        : a.F - b.F);
                    foreach (var (_, f, cells) in options)
                    {
                        foreach (var p in cells) c.Force(p.X, p.Y, TileKinds.High);
                        var after = Flood(c, startTile);
                        var own = new JsSet<int>(cells.Select((p) => c.I(p.X, p.Y)));
                        if (reach.All((i) => own.Has(i) || after.Has(i)))
                        {
                            DressObject(c, cells, DecorKinds.SirenCar, f);
                            foreach (var i in own) reach.Delete(i);
                            break;
                        }
                        foreach (var p in cells) c.Force(p.X, p.Y, TileKinds.Floor);
                    }
                }
            }
            else if (kind == MissionKinds.Hack)
            {
                var indoorFirst = objTiles.Where((p) => c.GroundAt(p.X, p.Y) == Indoor).Concat(objTiles.Where((p) => c.GroundAt(p.X, p.Y) != Indoor)).ToList();
                foreach (var p in indoorFirst)
                {
                    if (c.ElevAt(p.X, p.Y) != 0 || Grid.Dirs8.Count((d) => Standable(V(p.X + d.X, p.Y + d.Y))) < 3) continue;
                    c.Force(p.X, p.Y, TileKinds.Console);
                    var after = Flood(c, startTile);
                    if (reach.All((i) => i == c.I(p.X, p.Y) || after.Has(i)))
                    {
                        terminal = p;
                        reach.Delete(c.I(p.X, p.Y));
                        break;
                    }
                    c.Force(p.X, p.Y, TileKinds.Floor);
                }
            }
            if (objectiveSpot != null) taken.Add(c.I(objectiveSpot.X, objectiveSpot.Y));
            foreach (var z in evacZone) taken.Add(c.I(z.X, z.Y));

            // Pods: spread over the map, never near the squad. What the map is made of never depends on how many
            // there are (a night's extra group, three times the dead: playtest 2026-10-14): what goes after keeps off
            // only what was taken before them.
            var beforePods = new JsSet<int>(taken);
            var anchors = new List<Vec2>();
            var pods = new List<(List<Vec2> Tiles, List<Vec2> Patrol)>();
            // Further off and further apart on a bigger map (docs/MAPAS.md §6: a third of its side, 14 apart at 64).
            var sideLength = Math.Max(W, H);
            var podGap = Math.Max(PodDistance, (int)Js.Round(sideLength / 3.0));
            var podSpread = Math.Max(11, (int)Js.Round(sideLength / 4.5));
            var candidates = Shuffle(
                Enumerable.Range(0, c.Tiles.Count).Select((i) => V(i % c.Width, (int)Math.Floor((double)i / c.Width))).Where(
                    (p) => Standable(p) && Dist(p, squadCenter) >= podGap).ToList(),
                rng);
            bool Spread(Vec2 p, int gap) => anchors.All((a) => Dist(a, p) >= gap);
            foreach (var size in opts.PodSizes)
            {
                var anchor = candidates.FirstOrDefault((p) => Standable(p) && Spread(p, podSpread)) ?? candidates.FirstOrDefault((p) => Standable(p) && Spread(p, 6)) ?? candidates.FirstOrDefault(Standable);
                if (anchor == null) break;
                if (Cluster(c, reach, anchor, size, taken).Count < size)
                {
                    anchor = candidates.FirstOrDefault((p) => Standable(p) && Spread(p, 3) && Cluster(c, reach, p, size, taken).Count >= size);
                    if (anchor == null) break;
                }
                var found = anchor;
                var members = Cluster(c, reach, found, size, taken);
                foreach (var p in members) taken.Add(c.I(p.X, p.Y));
                anchors.Add(found);
                var waypoints = Shuffle(candidates.Where((p) => Dist(p, found) >= 7 && Dist(p, squadCenter) >= 12).ToList(), rng).Take(2).ToList();
                var patrol = new List<Vec2> { found };
                patrol.AddRange(waypoints);
                pods.Add((members, patrol));
            }

            TidyDecor(c);
            var heights = BuildingStoreys(c, biome, opts.Seed);
            var tall = StoreysOf(c, heights);
            // The outline stops sight as high as it is drawn (Arte, 19/10): a block shut up, a ruin, works, the motorway.
            foreach (var f in c.Frame)
            {
                if (f.Storeys == null || f.Storeys == 0) continue;
                foreach (var p in TilesIn(Rect(f.X, f.Y, f.W, f.H)))
                {
                    // Past the end of `tall` JavaScript only adds a property nobody reads: skipped here.
                    var i = c.I(p.X, p.Y);
                    if (i >= 0 && i < tall.Count) tall[i] = f.Storeys.Value;
                }
            }
            // The walk the mission takes (docs/MAPAS.md §6, its turns): from the squad to the objective, and on to the evacuation.
            var goal = item ?? terminal ?? objectiveSpot;
            var route = goal != null ? MissionRoute(c, spawns.Count > 0 ? spawns[0] : sq, goal, evacZone) : null;
            // None on the bus's way out (parkBus): it would drive through the post.
            var lamps = StreetLights(c, biome, opts.Seed, plan != null).Where((p) => !(bus != null && bus.Lane.Any((q) => q.X == p.X && q.Y == p.Y))).ToList();
            // The storeys above the ground (docs/FASE6-PLAN.md), last: stairs and ladders keep off what is already placed.
            var placed = new JsSet<int>(beforePods);
            foreach (var p in lamps) placed.Add(c.I(p.X, p.Y));
            foreach (var p in new[] { item, terminal }) if (p != null) placed.Add(c.I(p.X, p.Y));
            var upper = UpperFloors(c, heights, opts.Seed, placed, reach, squadCenter);
            // A flight of stairs laid where one of a group was to start (docs/MAPAS.md, phase 2: nobody stands on one): the
            // nearest free tile one can stand on instead, so the group keeps its size.
            Vec2 OffFlight(Vec2 p)
            {
                if (!Flights.LongFlightTile(c.Tiles, c.Decor, c.Width, c.I(p.X, p.Y))) return p;
                for (var r = 1; r <= 4; r++)
                {
                    var ring = TilesIn(Rect(p.X - r, p.Y - r, 2 * r + 1, 2 * r + 1)).Where((o) => Math.Max(Math.Abs(o.X - p.X), Math.Abs(o.Y - p.Y)) == r).ToList();
                    var q = Best(ring.Where((o) => Standable(o) && c.Get(o.X, o.Y) != TileKinds.Ladder).ToList(), (o) => Dist(p, o));
                    if (q == null) continue;
                    taken.Delete(c.I(p.X, p.Y));
                    taken.Add(c.I(q.X, q.Y));
                    return q;
                }
                return p;
            }
            for (var k = 0; k < pods.Count; k++)
            {
                var moved = new List<Vec2>();
                foreach (var p in pods[k].Tiles) moved.Add(OffFlight(p));
                pods[k] = (moved, pods[k].Patrol);
            }

            // Mirror/transpose everything into the final frame.
            var FW = swap ? H : W;
            var FH = swap ? W : H;
            Vec2 T(Vec2 p)
            {
                var x = swap ? p.Y : p.X;
                var y = swap ? p.X : p.Y;
                return V(flipX ? FW - 1 - x : x, flipY ? FH - 1 - y : y);
            }
            // A direction (DIRS4 index) in the final frame.
            int D(int f)
            {
                var d = Grid.Dirs4[f];
                var x = (swap ? d.Y : d.X) * (flipX ? -1 : 1);
                var y = (swap ? d.X : d.Y) * (flipY ? -1 : 1);
                return FindIndex(Grid.Dirs4, (q) => q.X == x && q.Y == y);
            }
            // A decor hint turned with the map. A mirror can't turn a corner piece: it faces its other side instead.
            int Dc(int code)
            {
                var look = Decor.DecorOf(code);
                if (look == null) return 0;
                var f = D(look.Facing);
                if (look.Kind != DecorKinds.FountainCorner) return Decor.DecorCode(look.Kind, f);
                return Decor.DecorCode(look.Kind, D((look.Facing + 3) % 4) == (f + 3) % 4 ? f : D((look.Facing + 3) % 4));
            }
            // Last, once every wall and door is where it stays (playtest 23/10 #11).
            NoCornerDoors(c);
            var tiles = Enumerable.Repeat(TileKinds.Floor, FW * FH).ToList();
            var elev = Enumerable.Repeat(0, FW * FH).ToList();
            var ground = Enumerable.Repeat(0, FW * FH).ToList();
            var decor = Enumerable.Repeat(0, FW * FH).ToList();
            var storeys = Enumerable.Repeat(0, FW * FH).ToList();
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                {
                    var q = T(V(x, y));
                    var i = c.I(x, y);
                    tiles[q.Y * FW + q.X] = c.Tiles[i];
                    elev[q.Y * FW + q.X] = c.Elev[i];
                    ground[q.Y * FW + q.X] = c.Ground[i];
                    decor[q.Y * FW + q.X] = Dc(c.Decor[i]);
                    storeys[q.Y * FW + q.X] = tall[i];
                }
            }
            var levels = upper.Storeys.Select((st) =>
            {
                var levelTiles = Enumerable.Repeat((string?)null, FW * FH).ToList();
                var levelDecor = Enumerable.Repeat(0, FW * FH).ToList();
                for (var y = 0; y < H; y++)
                {
                    for (var x = 0; x < W; x++)
                    {
                        var q = T(V(x, y));
                        var i = c.I(x, y);
                        levelTiles[q.Y * FW + q.X] = st.Slab[i] ? st.C.Tiles[i] : null;
                        levelDecor[q.Y * FW + q.X] = st.Slab[i] ? Dc(st.C.Decor[i]) : 0;
                    }
                }
                return new Level { Tiles = levelTiles, Decor = levelDecor.All((d) => d == 0) ? null : levelDecor };
            }).ToList();
            var medical = new List<MedicalSite>();
            foreach (var b in c.Buildings)
            {
                if (b.MedicalWear == null) continue;
                var a = T(V(b.Outer.X, b.Outer.Y));
                var z = T(V(b.Outer.X + b.Outer.W - 1, b.Outer.Y + b.Outer.H - 1));
                medical.Add(new MedicalSite
                {
                    X = Math.Min(a.X, z.X),
                    Y = Math.Min(a.Y, z.Y),
                    W = Math.Abs(a.X - z.X) + 1,
                    H = Math.Abs(a.Y - z.Y) + 1,
                    Wear = b.MedicalWear.Value,
                    Kind = b.MedicalKind,
                    Rooms = b.MedicalRooms?.Select((r) =>
                    {
                        var ra = T(V(r.X, r.Y));
                        var rz = T(V(r.X + r.W - 1, r.Y + r.H - 1));
                        return new MedicalRoom { X = Math.Min(ra.X, rz.X), Y = Math.Min(ra.Y, rz.Y), W = Math.Abs(ra.X - rz.X) + 1, H = Math.Abs(ra.Y - rz.Y) + 1, Z = r.Z, Use = r.Use };
                    }).ToList(),
                });
            }

            // The houses (playtest 18/10, #15): their rooms by use, and what lies about in them.
            var homes = new List<HomeSite>();
            foreach (var b in c.Buildings)
            {
                if (b.HomeRooms == null || b.Ruined) continue;
                var a = T(V(b.Outer.X, b.Outer.Y));
                var z = T(V(b.Outer.X + b.Outer.W - 1, b.Outer.Y + b.Outer.H - 1));
                homes.Add(new HomeSite
                {
                    X = Math.Min(a.X, z.X),
                    Y = Math.Min(a.Y, z.Y),
                    W = Math.Abs(a.X - z.X) + 1,
                    H = Math.Abs(a.Y - z.Y) + 1,
                    Wear = b.HomeWear ?? 0,
                    Rooms = b.HomeRooms.Select((r) =>
                    {
                        var ra = T(V(r.X, r.Y));
                        var rz = T(V(r.X + r.W - 1, r.Y + r.H - 1));
                        return new HomeRoom { X = Math.Min(ra.X, rz.X), Y = Math.Min(ra.Y, rz.Y), W = Math.Abs(ra.X - rz.X) + 1, H = Math.Abs(ra.Y - rz.Y) + 1, Z = r.Z, Use = r.Use };
                    }).ToList(),
                });
            }
            // The supermarkets (playtest 20/10, #18): where the client draws a glass front and its sign.
            var markets = new List<MarketSite>();
            foreach (var b in c.Buildings)
            {
                if (!b.Market || b.Ruined || b.Front == null) continue;
                var a = T(V(b.Outer.X, b.Outer.Y));
                var z = T(V(b.Outer.X + b.Outer.W - 1, b.Outer.Y + b.Outer.H - 1));
                // Three in five still stocked (#19), by position.
                var stock = Mix(b.Outer.X * 7 + 3, b.Outer.Y * 11 + 5) % 5 < 3 ? MarketStocks.Full : MarketStocks.Picked;
                markets.Add(new MarketSite { X = Math.Min(a.X, z.X), Y = Math.Min(a.Y, z.Y), W = Math.Abs(a.X - z.X) + 1, H = Math.Abs(a.Y - z.Y) + 1, Front = D(b.Front.Value), Stock = stock });
            }
            // What stands on the floor takes its tile (Arturo, 19/10: «que ocupen»; furnishing.ts): the houses' furniture, the shops'
            // by their trade, a library's tables, a barn's tools (a sawmill's too, as the client sees it), the kerbs' hydrants and
            // mailboxes. Clear of everyone's place, of what lies on the floor and of the bus's way out; then what lies about in the
            // houses and the clinics, the chairs over and the bags among it. If the map lost a way after all, none of it blocks.
            var sites = new List<FurnishSite>();
            foreach (var b in c.Buildings)
            {
                var siteKind = b.Ruined ? null : b.Furnish == Furnishes.Barn || (biome == Biomes.Wilds && b.Hall) ? "barn" : b.Hall && b.Furnish == Furnishes.Store ? "hall" : b.Furnish == Furnishes.Shop ? "shop" : null;
                if (siteKind == null) continue;
                var a = T(V(b.Outer.X, b.Outer.Y));
                var z = T(V(b.Outer.X + b.Outer.W - 1, b.Outer.Y + b.Outer.H - 1));
                sites.Add(new FurnishSite { Kind = siteKind, X = Math.Min(a.X, z.X), Y = Math.Min(a.Y, z.Y), W = Math.Abs(a.X - z.X) + 1, H = Math.Abs(a.Y - z.Y) + 1 });
            }
            // The terminal, the relay or VIP and the loot boxes keep the room round them they were given (a box is opened from beside it).
            var lootKinds = new[] { DecorKinds.LootWeapons, DecorKinds.LootMedical, DecorKinds.LootAmmo, DecorKinds.LootFood };
            var boxes = new List<Vec2>();
            for (var i = 0; i < c.Decor.Count; i++) if (lootKinds.Contains(Decor.DecorOf(c.Decor[i])?.Kind ?? "")) boxes.Add(V(i % W, (int)Math.Floor((double)i / W)));
            var round = new List<Vec2?> { terminal, objectiveSpot }.Concat(boxes).SelectMany((p) => p != null ? Grid.Dirs8.Select((d) => Add(p, d)) : Enumerable.Empty<Vec2>()).ToList();
            // (Not the groups of the dead: the map never depends on how many there are; they step off what stands up after.)
            var onFloor = new List<Vec2?>();
            onFloor.AddRange(spawns);
            onFloor.Add(item);
            onFloor.Add(terminal);
            onFloor.Add(objectiveSpot);
            onFloor.AddRange(round);
            onFloor.AddRange(evacZone);
            if (bus != null) onFloor.AddRange(bus.Lane);
            onFloor.AddRange(lamps);
            var placedOn = onFloor.Where((p) => p != null).Select((p) =>
            {
                var q = T(p!);
                return new Pos { X = q.X, Y = q.Y, Z = 0 };
            }).ToList();
            var map = new FurnishMap { Width = FW, Height = FH, Tiles = tiles, Elev = elev, Decor = decor, Levels = levels, Ground = ground };
            var before = Furnishing.StoreyPieces(map);
            var furnishReserved = new List<Pos>(placedOn);
            furnishReserved.AddRange(upper.Perch.Select((p) =>
            {
                var q = T(p);
                return new Pos { X = q.X, Y = q.Y, Z = p.Z };
            }));
            var furnisher = Furnishing.FurnishMap(map, homes, sites, opts.Seed, furnishReserved, biome == Biomes.Wilds ? null : lamps.Select(T).ToList());
            var reserved = new List<Vec2?>(spawns) { item, terminal, objectiveSpot }.Where((p) => p != null).Select((p) => T(p!)).ToList();
            foreach (var b in medical) b.Dressing = MedicalDressing.DressMedical(b, FW, tiles, decor, levels, opts.Seed, reserved, furnisher.Strew);
            foreach (var h in homes) h.Dressing = HomeDressing.DressHome(h, FW, tiles, decor, levels, opts.Seed, reserved, furnisher.Strew);
            // Each storey's flood fill, as it was: what was joined is joined still, or none of it stands.
            if (!Furnishing.JoinedAsBefore(before, Furnishing.StoreyPieces(map))) furnisher.Undo();
            // A group of the dead where something now stands starts on the nearest free tile instead, its round too.
            var outPods = pods.Select((p) => (Tiles: p.Tiles.Select(T).ToList(), Patrol: p.Patrol.Select(T).ToList())).ToList();
            var held = new JsSet<int>(spawns.Select(T).Concat(outPods.SelectMany((p) => p.Tiles)).Select((p) => p.Y * FW + p.X));
            bool OnGround(Vec2 p) => p.X >= 0 && p.Y >= 0 && p.X < FW && p.Y < FH && Grid.CanStandOn(tiles[p.Y * FW + p.X]) && elev[p.Y * FW + p.X] == 0;
            Vec2 FreeNear(Vec2 p, JsSet<int> busy) // TypeScript's `taken`
            {
                for (var r = 1; r <= 4; r++)
                {
                    var ring = TilesIn(Rect(p.X - r, p.Y - r, 2 * r + 1, 2 * r + 1)).Where((o) => Math.Max(Math.Abs(o.X - p.X), Math.Abs(o.Y - p.Y)) == r && OnGround(o) && !busy.Has(o.Y * FW + o.X)).ToList();
                    var q = Best(ring, (o) => Dist(p, o));
                    if (q != null) return q;
                }
                return p;
            }
            for (var k = 0; k < outPods.Count; k++)
            {
                var podTiles = new List<Vec2>();
                foreach (var p in outPods[k].Tiles)
                {
                    if (OnGround(p))
                    {
                        podTiles.Add(p);
                        continue;
                    }
                    var q = FreeNear(p, held);
                    held.Add(q.Y * FW + q.X);
                    podTiles.Add(q);
                }
                var podPatrol = outPods[k].Patrol.Select((p) => OnGround(p) ? p : FreeNear(p, new JsSet<int>())).ToList();
                outPods[k] = (podTiles, podPatrol);
            }

            return new MissionLayout
            {
                Id = $"gen-{site ?? biome}-{opts.Seed.ToString(CultureInfo.InvariantCulture)}",
                Name = rng.Pick(site != null ? SiteNames[site] : Names[biome]),
                Biome = biome,
                Site = site,
                Width = FW,
                Height = FH,
                Tiles = tiles,
                Elev = elev,
                Ground = ground,
                Medical = medical.Count > 0 ? medical : null,
                Homes = homes.Count > 0 ? homes : null,
                Markets = markets.Count > 0 ? markets : null,
                Decor = decor,
                Storeys = storeys,
                Levels = levels.Count > 0 ? levels : null,
                Perch = upper.Perch.Count > 0
                    ? upper.Perch.Select((p) =>
                    {
                        var q = T(p);
                        return new Pos { X = q.X, Y = q.Y, Z = p.Z };
                    }).ToList()
                    : null,
                Lights = lamps.Select((p) =>
                {
                    var q = T(p);
                    return new LayoutLight { X = q.X, Y = q.Y, Radius = plan != null ? TownLight.Radius : (double?)null };
                }).ToList(),
                Spawns = spawns.Select(T).ToList(),
                Pods = outPods.Select((p) => new PodSpot
                {
                    Tiles = p.Tiles.Select((q) => new Pos { X = q.X, Y = q.Y }).ToList(),
                    Patrol = p.Patrol.Select((q) => new Pos { X = q.X, Y = q.Y }).ToList(),
                }).ToList(),
                Item = item != null ? T(item) : null,
                Terminal = terminal != null ? T(terminal) : null,
                ObjectiveSpot = objectiveSpot != null ? T(objectiveSpot) : null,
                EvacZone = evacZone.Select(T).ToList(),
                EvacBus = bus != null ? new EvacBus { Tiles = bus.Tiles.Select(T).ToList(), Heading = D(bus.Heading) } : null,
                Frame = c.Frame.Count > 0 ? c.Frame.Select((f) => TurnPiece(f, T, D)).ToList() : null,
                Courtyards = c.Courtyards.Count > 0
                    ? c.Courtyards.Select((r) =>
                    {
                        var f = TurnPiece(new FramePiece { Kind = FrameKinds.Lot, X = r.X, Y = r.Y, W = r.W, H = r.H }, T, D);
                        return Rect(f.X, f.Y, f.W, f.H);
                    }).ToList()
                    : null,
                // `...(route ? { route } : {})`: none for 0 either.
                Route = route != null && route != 0 ? route : null,
            };
        }

        /// <summary>
        /// A town's ground out of doors that nobody can get to (a strip left between the outline and the back of a building):
        /// shut with the outline, as what is next to it (a lot if nothing is), in pieces of its own. A building nobody can get
        /// into any more (its door onto ground the outline took) goes with it, whole: a block shut up, its storeys.
        /// </summary>
        private static void SealPockets(Canvas c, JsSet<int> reach)
        {
            // Only ever read by key (the last piece set wins, as Map.set has it).
            var kindAt = new Dictionary<int, FramePiece>();
            foreach (var f in c.Frame) foreach (var p in TilesIn(Rect(f.X, f.Y, f.W, f.H))) kindAt[c.I(p.X, p.Y)] = f;
            var masks = new JsMap<string, byte[]>();
            var shut = new JsSet<Building>();
            for (var i = 0; i < c.Tiles.Count; i++)
            {
                if (reach.Has(i) || !Grid.IsWalkable(c.Tiles[i]) || c.Ground[i] != Indoor || c.Elev[i] != 0) continue;
                var p = V(i % c.Width, (int)Math.Floor((double)i / c.Width));
                var b = c.Buildings.FirstOrDefault((x) => InRect(x.Outer, p));
                if (b == null || TilesIn(b.Outer).Any((q) => c.In(q.X, q.Y) && reach.Has(c.I(q.X, q.Y)))) continue;
                shut.Add(b);
            }
            foreach (var b in shut)
            {
                var key = $"block/{b.Storeys ?? 1}";
                if (!masks.TryGetValue(key, out var mask))
                {
                    mask = new byte[c.Tiles.Count];
                    masks.Set(key, mask);
                }
                foreach (var q in TilesIn(b.Outer))
                {
                    if (!c.In(q.X, q.Y)) continue;
                    var j = c.I(q.X, q.Y);
                    mask[j] = 1;
                    c.Tiles[j] = TileKinds.Wall;
                    c.Ground[j] = Out;
                    c.Decor[j] = 0;
                    c.Keep[j] = true;
                }
                c.Buildings.RemoveAt(c.Buildings.IndexOf(b));
            }
            var along = new[] { FrameKinds.River, FrameKinds.Rail, FrameKinds.Highway, FrameKinds.Barrier };
            for (var i = 0; i < c.Tiles.Count; i++)
            {
                if (reach.Has(i) || !Grid.IsWalkable(c.Tiles[i]) || c.Ground[i] == Indoor || c.Ground[i] == Out || c.Elev[i] != 0) continue;
                var x = i % c.Width;
                var y = (i - x) / c.Width;
                FramePiece? by = null;
                foreach (var d in Grid.Dirs4)
                {
                    if (!c.In(x + d.X, y + d.Y) || !kindAt.TryGetValue(c.I(x + d.X, y + d.Y), out var piece)) continue;
                    by = piece;
                    break;
                }
                var key = by != null && !along.Contains(by.Kind) ? $"{by.Kind}/{by.Storeys ?? 0}" : "lot/0";
                if (!masks.TryGetValue(key, out var mask))
                {
                    mask = new byte[c.Tiles.Count];
                    masks.Set(key, mask);
                }
                mask[i] = 1;
                c.Tiles[i] = TileKinds.Wall;
                c.Ground[i] = Out;
                c.Decor[i] = 0;
                c.Keep[i] = true;
            }
            foreach (var entry in masks)
            {
                var parts = entry.Key.Split('/');
                var kind = parts[0]; // FrameKinds
                var storeys = int.Parse(parts[1], CultureInfo.InvariantCulture);
                foreach (var r in TownPlans.RectsOf(entry.Value, c.Width, c.Height, 1)) c.Frame.Add(new FramePiece { Kind = kind, X = r.X, Y = r.Y, W = r.W, H = r.H, Storeys = storeys != 0 ? storeys : (int?)null });
            }
        }

        /// <summary>A piece of the outline in the map's final frame: its rectangle turned, the side it runs along too.</summary>
        private static FramePiece TurnPiece(FramePiece f, Func<Vec2, Vec2> T, Func<int, int> D)
        {
            var a = T(V(f.X, f.Y));
            var z = T(V(f.X + f.W - 1, f.Y + f.H - 1));
            return new FramePiece
            {
                Kind = f.Kind,
                X = Math.Min(a.X, z.X),
                Y = Math.Min(a.Y, z.Y),
                W = Math.Abs(a.X - z.X) + 1,
                H = Math.Abs(a.Y - z.Y) + 1,
                Side = f.Side != null ? D(f.Side.Value) : (int?)null,
                Storeys = f.Storeys,
            };
        }

        /// <summary>
        /// Steps on foot from `from` to the objective on `goal` (a tile beside it, when it stands on something one can't step
        /// on: a terminal, a siren), and from there to the nearest tile of the evacuation; null where either can't be walked.
        /// </summary>
        private static int? MissionRoute(Canvas c, Vec2 from, Vec2 goal, List<Vec2> evac)
        {
            int At(int[] steps, Vec2 p)
            {
                var own = c.In(p.X, p.Y) ? steps[c.I(p.X, p.Y)] : -1;
                if (own >= 0) return own;
                var near = Grid.Dirs8.Select((d) => Add(p, d)).Where((q) => c.In(q.X, q.Y) && steps[c.I(q.X, q.Y)] >= 0).Select((q) => steps[c.I(q.X, q.Y)] + 1).ToList();
                return near.Count > 0 ? near.Min() : -1;
            }
            var there = At(StepsFrom(c, new List<Vec2> { from }), goal);
            if (there < 0) return null;
            if (evac.Count == 0) return there;
            var start = Grid.IsWalkable(c.Get(goal.X, goal.Y)) ? new List<Vec2> { goal } : Grid.Dirs8.Select((d) => Add(goal, d)).Where((q) => c.In(q.X, q.Y) && Grid.IsWalkable(c.Get(q.X, q.Y))).ToList();
            var back = StepsFrom(c, start);
            // Math.min over none is Infinity: not finite, null.
            var reachable = evac.Select((z) => Js.At(back, c.I(z.X, z.Y), -1)).Where((n) => n >= 0).ToList();
            if (reachable.Count == 0) return null;
            var out_ = reachable.Min();
            return there + out_ + (start.Count > 0 && ReferenceEquals(start[0], goal) ? 0 : 1);
        }

        /// <summary>Layout of a hand-made map; objectives go on its 'D' spot.</summary>
        public static MissionLayout HandmadeLayout(MapDef def, string kind)
        {
            var map = Maps.ParseMap(def);
            var tiles = new List<string>(map.Tiles);
            var spot = map.Item;
            var objective = kind != MissionKinds.Elimination && spot != null;
            if (kind == MissionKinds.Hack && spot != null) tiles[spot.Y * map.Width + spot.X] = TileKinds.Console;
            // Object.keys(def.pods).sort(): the default sort on strings, by UTF-16 code units (ordinal).
            var keys = def.Pods.Keys.ToList();
            Js.Sort(keys, (a, b) => string.CompareOrdinal(a, b));
            return new MissionLayout
            {
                Id = def.Id,
                Name = def.Name,
                Biome = def.Biome,
                Width = map.Width,
                Height = map.Height,
                Tiles = tiles,
                Elev = map.Elev,
                // A map with storeys (docs/FASE6-PLAN.md): the storeys, their stairs and how tall each building stands.
                Decor = map.Decor,
                Levels = map.Levels,
                Storeys = map.Levels != null ? map.Storeys : null,
                Spawns = map.Spawns,
                Pods = keys.Select((key) => new PodSpot { Tiles = map.PodSpawns.Get(key, new List<Pos>()), Patrol = new List<Pos>() }).ToList(),
                Item = kind == MissionKinds.Recovery ? spot : null,
                Terminal = kind == MissionKinds.Hack ? spot : null,
                ObjectiveSpot = objective && (kind == MissionKinds.Sabotage || kind == MissionKinds.Rescue) ? spot : null,
                EvacZone = objective ? map.EvacZone : new List<Vec2>(),
                Siege = def.Siege == true ? true : (bool?)null,
                Barricades = map.Barricades.Count > 0 ? map.Barricades : null,
            };
        }
    }
}
