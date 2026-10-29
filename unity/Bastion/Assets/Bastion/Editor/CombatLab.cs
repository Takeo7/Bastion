using System;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Effects;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// A few of the battle's actions played on the units' figures and the effects, for strips in play mode (Shots
    /// -shotPlay): on a recorded state (BASTION_MAP, city-7 unless said) a soldier walks a move, shoots the nearest of
    /// the dead twice (a hit, then a critical that kills), lowers the weapon and throws a grenade. It starts a second
    /// in, as the strip's first frame is taken (Shots waits 10 frames: at 10 fps, a second):
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Shoot -shotPlay -shotCamera "Shot camera"
    ///     -shotFrames 90 -shotFps 10 -shotOut &lt;folder&gt;
    /// </summary>
    public static class CombatLab
    {
        static UnitViews units;
        static Fx fx;
        static GameState state;
        static Unit soldier;
        static Unit target;
        static Pos[] path;

        public static void Shoot()
        {
            if (Environment.GetEnvironmentVariable("BASTION_TINTE") == "1") Bastion.Client.Units.SquadTint.On = true;
            if (Resources.Load<FxArt>("FxArt") == null) FxArtMake.Run();
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            state = MapLab.Golden(name);
            MapLab.Lighting(true);
            MapLab.Map(state, name);
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            fx = new GameObject("Efectos").AddComponent<Fx>();
            // A soldier walks a move of four tiles or more, and one of the dead is put five or six tiles past where it
            // ends, on a tile it could walk to (the state is the lab's: nobody plays it).
            static int Off(Pos a, Pos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
            soldier = state.Units.First(u => u.Alive && u.Team == "xcom" && !u.Objective);
            target = state.Units.Where(u => u.Alive && u.Team != "xcom" && !u.Objective).OrderBy(u => Off(u.Pos, soldier.Pos)).First();
            var start = soldier.Pos;
            var reach = Paths.ComputeReach(state, soldier, Paths.CostPerAp(soldier));
            var end = Paths.Destinations(state, soldier, reach).Where(d => Off(d.Pos, start) >= 4).OrderByDescending(d => Off(d.Pos, start)).ThenBy(d => d.Cost).First();
            path = Paths.BuildPath(state, soldier, reach, end.Pos).ToArray();
            var far = Paths.ComputeReach(state, soldier, Paths.MaxMoveCost(soldier) * 2);
            var spot = Paths.Destinations(state, soldier, far).Where(d => Off(d.Pos, end.Pos) is 5 or 6)
                .OrderByDescending(d => Off(d.Pos, start)).First().Pos;
            target.Pos = new Pos { X = spot.X, Y = spot.Y, Z = spot.Z };
            Debug.Log($"[combat] {name}: {soldier.Id} ({start.X},{start.Y}) walks {path.Length} tiles to ({end.Pos.X},{end.Pos.Y}) and shoots {target.Id} {target.Template} put at ({spot.X},{spot.Y})");

            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            // Over where it ends and the target (the walk comes into the picture), far enough off for both.
            var ends = MapSpace.TileToWorld(state, end.Pos);
            var aimed = MapSpace.TileToWorld(state, spot);
            MapSpace.Aim(camera, (ends + aimed) / 2, Mathf.Max(9, Vector3.Distance(ends, aimed) / MapSpace.Tile * 1.2f + 4));

            EditorApplication.playModeStateChanged += Started;
        }

        /// <summary>
        /// The same actions close up, on a plain floor (Gameplay reviews each figure's strips so, and from the game's camera):
        /// a soldier and one of the dead alone (BASTION_TEMPLATE, zombie unless said), the soldier walking three tiles and
        /// stopping four short of it; the camera at BASTION_DIST tiles (6 unless said; 24 is the game's).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Close -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 110 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Close()
        {
            if (Environment.GetEnvironmentVariable("BASTION_TINTE") == "1") Bastion.Client.Units.SquadTint.On = true;
            if (Resources.Load<FxArt>("FxArt") == null) FxArtMake.Run();
            state = MapLab.Golden("city-7");
            MapLab.Lighting(true);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Suelo";
            floor.transform.position = MapSpace.World(12, 10);
            floor.transform.localScale = Vector3.one * 4;
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.42f, 0.42f, 0.44f) };
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            string template = Environment.GetEnvironmentVariable("BASTION_TEMPLATE") ?? "zombie";
            soldier = state.Units.First(u => u.Alive && u.Team == "xcom");
            target = state.Units.FirstOrDefault(u => u.Alive && u.Template == template) ?? state.Units.First(u => u.Alive && u.Team != "xcom");
            soldier.Pos = new Pos { X = 8, Y = 10 };
            soldier.Facing = 0;
            target.Pos = new Pos { X = 15, Y = 10 };
            target.Facing = Math.PI;
            state.Units = new System.Collections.Generic.List<Unit> { soldier, target };
            path = new[] { new Pos { X = 9, Y = 10 }, new Pos { X = 10, Y = 10 }, new Pos { X = 11, Y = 10 } };
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            fx = new GameObject("Efectos").AddComponent<Fx>();
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 400;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            float distance = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 6;
            // From the south, the row across the picture: the soldier on the left, the dead on the right. BASTION_VIEW=side:
            // low and from the side, a figure a third of the picture's height (Gameplay's strips that close an animation).
            var middle = (MapSpace.World(11, 10) + MapSpace.World(15, 10)) / 2;
            if (Environment.GetEnvironmentVariable("BASTION_VIEW") == "side")
            {
                camera.fieldOfView = 40;
                camera.transform.position = middle + new Vector3(0, 1.3f, -6.5f);
                camera.transform.LookAt(middle + Vector3.up * 0.95f);
            }
            else MapSpace.Aim(camera, middle + Vector3.up * 0.9f, distance, 0);
            Debug.Log($"[combat] close: {soldier.Id} and {target.Id} {target.Template}, the camera {distance} tiles off");
            EditorApplication.playModeStateChanged += Started;
        }

        /// <summary>The play-mode sequence the setup chose (Shoot's and Close's: Play).</summary>
        static Func<Awaitable> sequence;

        /// <summary>
        /// A plain floor round tile (12, 10) of city-7's state with these of its units alone, each where it is put, and the
        /// camera low from the side on tiles `from` to `to` (Gameplay's strips that close an animation).
        /// </summary>
        static void Plain(Func<Unit, bool> keep, int from, int to)
        {
            if (Resources.Load<FxArt>("FxArt") == null) FxArtMake.Run();
            state = MapLab.Golden("city-7");
            MapLab.Lighting(true);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Suelo";
            floor.transform.position = MapSpace.World(12, 10);
            floor.transform.localScale = Vector3.one * 4;
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.42f, 0.42f, 0.44f) };
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            state.Units = state.Units.Where(keep).ToList();
            // The state as open as the floor drawn (city-7 has walls on row 10): what a body falls across (UnitViews.Blows) finds room.
            for (int y = 0; y < Mathf.Min(state.Height, 24); y++)
                for (int x = 0; x < Mathf.Min(state.Width, 28); x++)
                {
                    int i = y * state.Width + x;
                    state.Tiles[i] = TileKinds.Floor;
                    if (state.Elev != null && i < state.Elev.Count) state.Elev[i] = 0;
                    if (state.Decor != null && i < state.Decor.Count) state.Decor[i] = 0;
                }
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            fx = new GameObject("Efectos").AddComponent<Fx>();
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 400;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            var middle = (MapSpace.World(from, 10) + MapSpace.World(to, 10)) / 2;
            camera.fieldOfView = 40;
            // (BASTION_DIST: the game's camera that many tiles off, 24 unless said; 9 its nearest.)
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tiles) ? tiles : 24;
            // (BASTION_VIEW=norte: low from the north, as referencia.html?map=plaza's films are, the sun's side.)
            string view = Environment.GetEnvironmentVariable("BASTION_VIEW");
            if (view == "game") MapSpace.Aim(camera, middle, off);
            else
            {
                camera.transform.position = middle + new Vector3(0, 1.3f, view == "norte" ? 6.5f : -6.5f);
                camera.transform.LookAt(middle + Vector3.up * 0.95f);
            }
            EditorApplication.playModeStateChanged += Started;
        }

        /// <summary>
        /// The walk (Gameplay 26/10, A1: unitViews.ts walkRun): two soldiers on a plain floor, one coming down from (20, 12)
        /// round the corner at (20, 15) and west along row 15, the other east along it from (12, 15), passing each other on
        /// (15–17, 15) (`passes`: each to its right). BASTION_VIEW=game the game's camera BASTION_DIST tiles off (24), else 8.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Andar -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 110 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Andar()
        {
            var first = MapLab.Golden("city-7");
            var ids = first.Units.Where(u => u.Alive && u.Team == "xcom").Select(u => u.Id).Take(2).ToList();
            Plain(u => ids.Contains(u.Id), 14, 20);
            var a = Put(state.Units.First(u => u.Id == ids[0]), 20, 12, -Math.PI / 2);
            var b = Put(state.Units.First(u => u.Id == ids[1]), 12, 15, 0);
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tiles) ? tiles : 24;
            MapSpace.Aim(camera, MapSpace.World(17, 15), Environment.GetEnvironmentVariable("BASTION_VIEW") == "game" ? off : 8);
            Pos P(int x, int y) => new Pos { X = x, Y = y };
            var pathA = new[] { P(20, 13), P(20, 14), P(20, 15), P(19, 15), P(18, 15), P(17, 15), P(16, 15), P(15, 15), P(14, 15) };
            var passA = pathA.Select(p => p.Y == 15 && p.X >= 15 && p.X <= 17).ToArray();
            var pathB = new[] { P(13, 15), P(14, 15), P(15, 15), P(16, 15), P(17, 15), P(18, 15), P(19, 15) };
            var passB = pathB.Select(p => p.X >= 15 && p.X <= 17).ToArray();
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(1);
                    Debug.Log($"[combat] {Time.time:0.00} s: walk");
                    var one = units.MoveAlong(a.Id, pathA, 1, 1, null, passA);
                    var two = units.MoveAlong(b.Id, pathB, 1, 1, null, passB);
                    await one;
                    await two;
                    Debug.Log($"[combat] {Time.time:0.00} s: there, {units.Feet(a.Id)} and {units.Feet(b.Id)}");
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }

        /// <summary>
        /// A siege's door as the dead beat on it (Gameplay 27/10, D2: unitViews.ts smash, doors.ts knock and WEAR): on the
        /// base's map (step2/bastion-siege), one of the dead at (17, 5) strikes the gate at (17, 6) BASTION_GOLPES times (5),
        /// 4 of its 18 a blow, a second apart: its leaf shakes, ajar, leant and sagging as it gives. The game's camera
        /// BASTION_DIST tiles off (8), or low from the north (BASTION_VIEW=norte).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Asedio -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 80 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Asedio()
        {
            if (Resources.Load<FxArt>("FxArt") == null) FxArtMake.Run();
            state = MapLab.Golden("../step2/bastion-siege");
            var dead = MapLab.Golden("city-7").Units.First(u => u.Alive && u.Template == "zombie");
            dead.Pos = new Pos { X = 17, Y = 5, Z = 0 };
            dead.Facing = 0;
            state.Units = new System.Collections.Generic.List<Unit> { dead };
            MapLab.Lighting(true);
            MapLab.Map(state, "bastion");
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            fx = new GameObject("Efectos").AddComponent<Fx>();
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            camera.fieldOfView = 40;
            var gate = new Pos { X = 17, Y = 6, Z = 0 };
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tiles) ? tiles : 8;
            var at = MapSpace.TileToWorld(state, gate);
            if (Environment.GetEnvironmentVariable("BASTION_VIEW") == "norte")
            {
                camera.transform.position = at + new Vector3(1.2f, 1.6f, 4.5f);
                camera.transform.LookAt(at + Vector3.up * 1.2f);
            }
            else MapSpace.Aim(camera, at, off);
            int blows = int.TryParse(Environment.GetEnvironmentVariable("BASTION_GOLPES"), out int n) ? n : 5;
            int node = Bastion.Engine.Grid.NodeOf(state, gate);
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(1);
                    for (int k = 0; k < blows; k++)
                    {
                        int left = Math.Max(0, state.Breakables[node] - 4);
                        state.Breakables[node] = left;
                        Debug.Log($"[asedio] {Time.time:0.00} s: blow {k + 1}, {left} left; gate tile {node}; {MapView.Current?.Doors}");
                        await units.Smash(dead.Id, gate);
                        fx.Smash(MapSpace.TileToWorld(state, gate, 1.65f), "door", left <= 0);
                        await Awaitable.WaitForSecondsAsync(1);
                    }
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
            EditorApplication.playModeStateChanged += Started;
        }

        /// <summary>
        /// The steps that aren't on the flat (Gameplay 27/10, C2: unitViews.ts walk's stairs, sill, offSill, ladder and drop):
        /// a soldier with a rifle on BASTION_MAP (torre unless said) goes, by the engine's paths, to each of BASTION_RUTA's
        /// places in turn ("x,y,z;x,y,z": unless said, up the ladder by the door to the first storey, out of a window to the
        /// ground, and back). The game's camera over the map's middle, BASTION_DIST tiles off (14), BASTION_AT=x,y another
        /// middle; the cutaway following the soldier.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Pasos -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 200 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Pasos()
        {
            if (Resources.Load<FxArt>("FxArt") == null) FxArtMake.Run();
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "torre";
            // BASTION_GENERAR=biome,seed[,kind]: the mission referencia.html?biome=…&seed=…&kind=… makes (reference.ts), not a recorded one.
            var made = Environment.GetEnvironmentVariable("BASTION_GENERAR")?.Split(',');
            state = made != null && made.Length >= 2
                ? Setup.CreateMission(new MissionOptions
                {
                    Map = new MapSource { Kind = MapSourceKinds.Generated, Biome = made[0], Seed = int.Parse(made[1]) },
                    Slots = new System.Collections.Generic.List<int> { 0 },
                    Rng = new Rng((uint)int.Parse(made[1])),
                    Kind = made.Length > 2 ? made[2] : "elimination",
                    Difficulty = 3,
                })
                : MapLab.Golden(name);
            Debug.Log($"[pasos] {state.MapId} {state.Width}x{state.Height}: {string.Join(" ", state.Tiles.GroupBy(t => t).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"))}; units {state.Units.Count}");
            soldier = state.Units.First(u => u.Alive && u.Team == "xcom");
            soldier.Template = "rookie";
            soldier.Arms = new Arms { Primary = "assaultRifle" };
            state.Units = new System.Collections.Generic.List<Unit> { soldier };
            // BASTION_DESDE=x,y,z: where it starts.
            if (Environment.GetEnvironmentVariable("BASTION_DESDE")?.Split(',').Select(int.Parse).ToArray() is int[] start && start.Length >= 2)
                soldier.Pos = new Pos { X = start[0], Y = start[1], Z = start.Length > 2 ? start[2] : 0 };
            // BASTION_NOCHE=1: by night, with its street lights (NightLights); BASTION_FRENTE, its facing (radians, 0.10.1's).
            bool dark = Environment.GetEnvironmentVariable("BASTION_NOCHE") == "1";
            if (dark && state.Mission != null) state.Mission.Night = true;
            if (Environment.GetEnvironmentVariable("BASTION_FRENTE") is string facing) soldier.Facing = double.Parse(facing, System.Globalization.CultureInfo.InvariantCulture);
            Bastion.Client.NightLights.StateForLab = state;
            MapLab.Lighting(!dark);
            var view = MapLab.Map(state, name);
            var cutaway = view.GetComponent<Cutaway>();
            if (cutaway != null) cutaway.Focus.selected = soldier.Id;
            // BASTION_FOG=1: the fog of war as the squad sees (its lit tiles' glow by night too); BASTION_FOG_ALL=1, all in sight
            // as referencia.html has it.
            if (Environment.GetEnvironmentVariable("BASTION_FOG") == "1")
            {
                var fog = new GameObject("Niebla").AddComponent<Fog>();
                fog.Reset(state);
                fog.Show(state, true);
                if (Environment.GetEnvironmentVariable("BASTION_FOG_ALL") == "1") fog.SeeAll();
                Debug.Log($"[pasos] fog: night {Bastion.Client.Mood.Current != null && Bastion.Client.Mood.Current.Now == Bastion.Client.Mood.Night}, {state.Lights?.Count ?? 0} lights, {Los.LitMask(state).Count(b => b)} lit");
            }
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            fx = new GameObject("Efectos").AddComponent<Fx>();
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tiles) ? tiles : 10;
            var middle = Environment.GetEnvironmentVariable("BASTION_AT")?.Split(',').Select(int.Parse).ToArray() ?? new[] { state.Width / 2, state.Height / 2 };
            MapSpace.Aim(camera, MapSpace.World(middle[0], middle[1]), off);
            // BASTION_CAMARA=x,y,z,lx,ly,lz: the camera there looking there, fixed (0.10.1's frame, tiles: as a referencia.html setup puts it).
            var fixedAt = Environment.GetEnvironmentVariable("BASTION_CAMARA")?.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (fixedAt != null && fixedAt.Length == 6)
            {
                camera.fieldOfView = 40;
                camera.transform.position = new Vector3(fixedAt[0], fixedAt[1], -fixedAt[2]) * MapSpace.Tile;
                camera.transform.LookAt(new Vector3(fixedAt[3], fixedAt[4], -fixedAt[5]) * MapSpace.Tile);
            }
            // (Every ladder on the ground that isn't a flight of stairs, and the floor beside it a storey up: to find one.)
            for (int i = 0; i < state.Tiles.Count; i++)
            {
                var at = new Pos { X = i % state.Width, Y = i / state.Width, Z = 0 };
                if (state.Tiles[i] != TileKinds.Ladder || Flights.FlightAt(state, at) != null) continue;
                var tops = Bastion.Engine.Grid.Dirs4.Select(d => new Pos { X = at.X + d.X, Y = at.Y + d.Y, Z = 1 }).Where(p => Bastion.Engine.Grid.InBounds(state, p.X, p.Y) && Bastion.Engine.Grid.CanStandOn(Bastion.Engine.Grid.KindAt(state, p)));
                Debug.Log($"[pasos] ladder at {at.X},{at.Y}: up to {string.Join(" ", tops.Select(p => $"{p.X},{p.Y},1"))}");
            }
            // ("arriba": the nearest floor a storey up; "abajo", back where it started.)
            var home = soldier.Pos;
            var route = (Environment.GetEnvironmentVariable("BASTION_RUTA") ?? "10,4,1;9,8,0;1,18,0").Split(';')
                .Select(p => p == "arriba" ? null : p == "abajo" ? home : p.Split(',').Select(int.Parse).ToArray() is int[] v ? new Pos { X = v[0], Y = v[1], Z = v.Length > 2 ? v[2] : 0 } : null).ToList();
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    // (BASTION_AT unsaid: the camera after the soldier, as the game's follows the one it moves.)
                    bool done = false;
                    if (Environment.GetEnvironmentVariable("BASTION_AT") == null && fixedAt == null)
                        _ = Follow();
                    async Awaitable Follow()
                    {
                        while (!done)
                        {
                            MapSpace.Aim(camera, units.Feet(soldier.Id), off);
                            await Awaitable.NextFrameAsync();
                        }
                    }
                    await Awaitable.WaitForSecondsAsync(1);
                    foreach (var asked in route)
                    {
                        var reach = Paths.ComputeReach(state, soldier, 999);
                        var to = asked ?? Paths.Destinations(state, soldier, reach).Where(d => d.Pos.Z == 1).OrderBy(d => d.Cost).Select(d => d.Pos).FirstOrDefault() ?? soldier.Pos;
                        var path = Paths.BuildPath(state, soldier, reach, to);
                        if (path == null || path.Count == 0)
                        {
                            var near = Paths.Destinations(state, soldier, reach).Where(d => d.Pos.Z == to.Z).OrderBy(d => Math.Abs(d.Pos.X - to.X) + Math.Abs(d.Pos.Y - to.Y)).Take(6);
                            Debug.LogWarning($"[pasos] no way to {to.X},{to.Y},{to.Z} ({reach.Cost.Count} reached); nearest there: {string.Join(" ", near.Select(d => $"{d.Pos.X},{d.Pos.Y},{d.Pos.Z}"))}");
                            continue;
                        }
                        Debug.Log($"[pasos] {Time.time:0.00} s: to {to.X},{to.Y},{to.Z}: {units.StepsOf(soldier.Pos, path)}");
                        await units.MoveAlong(soldier.Id, path, 1, 1, at => Debug.Log($"[pasos] step {Time.time:0.00}"));
                        soldier.Pos = new Pos { X = to.X, Y = to.Y, Z = to.Z };
                        Debug.Log($"[pasos] {Time.time:0.00} s: there, {units.Feet(soldier.Id)}");
                        await Awaitable.WaitForSecondsAsync(0.5f);
                    }
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                    done = true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
            EditorApplication.playModeStateChanged += Started;
        }

        /// <summary>
        /// A body that gets up as one of the dead (Gameplay 27/10, D4: unitViews.ts reanimate): a soldier at (11, 10) shot dead
        /// from two tiles west 0.8 s in, a second later risen as a zombie out of its body's death played backwards. On a plain
        /// floor, the camera low (BASTION_VIEW: norte, game).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Levanta -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 50 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Levanta()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            var risen = first.Units.First(u => u.Alive && u.Template == "zombie");
            Plain(u => u.Id == soldierId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 11, 10, 0);
            soldier.Template = "rookie";
            soldier.Arms = new Arms { Primary = "assaultRifle" };
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(0.8f);
                    Debug.Log($"[combat] {Time.time:0.00} s: dies");
                    units.StruckFrom(soldier.Id, units.Feet(soldier.Id) + Vector3.left * 2 * MapSpace.Tile);
                    await units.Die(soldier.Id);
                    soldier.Alive = false;
                    await Awaitable.WaitForSecondsAsync(1);
                    risen.Id = "r1";
                    risen.Pos = new Pos { X = soldier.Pos.X, Y = soldier.Pos.Y, Z = 0 };
                    risen.Facing = soldier.Facing;
                    state.Units.Add(risen);
                    Debug.Log($"[combat] {Time.time:0.00} s: rises");
                    await units.Reanimate(soldier.Id, risen.Id);
                    Debug.Log($"[combat] {Time.time:0.00} s: up");
                    await Awaitable.WaitForSecondsAsync(1);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }

        /// <summary>
        /// A weapon from the backpack taken in hand (Gameplay 27/10, D3: unitViews.ts equip): a soldier with the assault rifle
        /// takes a pump shotgun a second in (its Unit.arms changed and shown: Sync). On a plain floor, the camera low
        /// (BASTION_VIEW: norte, game).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Cambiar -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 30 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Cambiar()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            Plain(u => u.Id == soldierId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 11, 10, 0);
            soldier.Template = "rookie";
            soldier.Arms = new Arms { Primary = "assaultRifle" };
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1);
                    Debug.Log($"[combat] {Time.time:0.00} s: takes the shotgun");
                    soldier.Arms = new Arms { Primary = "pumpShotgun" };
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }

        /// <summary>
        /// A search (Gameplay 27/10, C3: unitViews.ts rummage): a soldier with a rifle searches the tile ahead of it twice,
        /// a second apart. On a plain floor, the camera low (BASTION_VIEW: norte, game).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Registrar -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 40 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Registrar()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            Plain(u => u.Id == soldierId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 11, 10, 0);
            soldier.Template = "rookie";
            soldier.Arms = new Arms { Primary = "assaultRifle" };
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1);
                    for (int k = 0; k < 2; k++)
                    {
                        Debug.Log($"[combat] {Time.time:0.00} s: searches");
                        await units.Rummage(soldier.Id, MapSpace.World(12, 10));
                        await Awaitable.WaitForSecondsAsync(1);
                    }
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
        }

        /// <summary>
        /// The dead's blow (Gameplay 26/10, A5: unitViews.ts lunge): one of the dead beside a soldier strikes it (a step in
        /// and back), then bites as one that kills and would eat, with nothing eating after: it holds on 2 s and lets go.
        /// On a plain floor, the camera low (BASTION_VIEW: norte, game).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Mordisco -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 80 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Mordisco()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Plain(u => u.Id == soldierId || u.Id == deadId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 11, 10, 0);
            target = Put(state.Units.First(u => u.Id == deadId), 12, 10, Math.PI);
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(1);
                    var home = units.Feet(target.Id);
                    Debug.Log($"[combat] {Time.time:0.00} s: strikes");
                    await units.Lunge(target.Id, soldier.Id, false, () =>
                        Debug.Log($"[combat] {Time.time:0.00} s: lands, {(units.Feet(target.Id) - home).magnitude / MapSpace.Tile:0.00} tiles in"));
                    // (As the game's damage event, once the lunge is back: unitViews.ts lunge resolves as the blow lands.)
                    Debug.Log($"[combat] {Time.time:0.00} s: hit react");
                    _ = units.HitReact(soldier.Id, 1);
                    await Awaitable.WaitForSecondsAsync(1);
                    Debug.Log($"[combat] {Time.time:0.00} s: bites, back by {(units.Feet(target.Id) - home).magnitude / MapSpace.Tile:0.00}");
                    await units.Lunge(target.Id, soldier.Id, true, () => _ = units.HitReact(soldier.Id, 1));
                    Debug.Log($"[combat] {Time.time:0.00} s: holds, {(units.Feet(target.Id) - home).magnitude / MapSpace.Tile:0.00} tiles in");
                    // (Its chest and head ahead of its feet, and the soldier's chest from its feet, along the way it bites: tiles.)
                    var way = (units.Feet(soldier.Id) - home).normalized;
                    float Ahead(Vector3 p) => Vector3.Dot(p - home, way) / MapSpace.Tile;
                    Debug.Log($"[combat] holding: feet {Ahead(units.Feet(target.Id)):0.00}, chest {Ahead(units.Chest(target.Id)):0.00}, head {Ahead(units.Head(target.Id)):0.00}; the soldier's chest {Ahead(units.Chest(soldier.Id)):0.00}, head {Ahead(units.Head(soldier.Id)):0.00}");
                    await Awaitable.WaitForSecondsAsync(3.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done, {(units.Feet(target.Id) - home).magnitude / MapSpace.Tile:0.00} tiles in");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
        }

        /// <summary>
        /// Coming into sight (Gameplay 26/10, A4: unitViews.ts FADE_IN, syntyFigures.ts fadeIn and veil): one of the dead
        /// out of the soldier's sight at (26, 22), hidden, put four tiles off it at (14, 10) and faded in as Sync shows it;
        /// 1.5 s on veiled (the shot over a shoulder) and back. On a plain floor, the camera low (BASTION_VIEW: norte, game).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Fundido -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 50 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Fundido()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Plain(u => u.Id == soldierId || u.Id == deadId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 10, 10, 0);
            target = Put(state.Units.First(u => u.Id == deadId), 26, 22, Math.PI);
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Debug.Log($"[combat] {Time.time:0.00} s: far off, shown {Figures.FeetOf(target.Id)?.gameObject.activeInHierarchy}");
                    await Awaitable.WaitForSecondsAsync(1);
                    target.Pos = new Pos { X = 14, Y = 10 };
                    units.Sync(state);
                    Debug.Log($"[combat] {Time.time:0.00} s: in sight, shown {Figures.FeetOf(target.Id)?.gameObject.activeInHierarchy}");
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: veiled");
                    units.Veil(target.Id, true);
                    await Awaitable.WaitForSecondsAsync(1f);
                    units.Veil(target.Id, false);
                    await Awaitable.WaitForSecondsAsync(1f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
        }

        /// <summary>
        /// What is left on the floor (Gameplay 26/10, B2: dropped.ts, MapBuilder.Dropped) along row 12 of city-7: a rifle, a
        /// medicine, food, two things together (a bag), and a fallen soldier's (its rucksack and its rifle), every other
        /// tile from (20, 12). The game's camera BASTION_DIST tiles off (10 unless said). Still.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Tirado -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 10 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Tirado()
        {
            state = MapLab.Golden("city-7");
            Pos At(int x) => new Pos { X = x, Y = 12 };
            state.Dropped = new System.Collections.Generic.List<DroppedLoot>
            {
                new DroppedLoot { Id = "d1", Pos = At(20), Loot = new Loot { Weapon = "assaultRifle" } },
                new DroppedLoot { Id = "d2", Pos = At(22), Loot = new Loot { Medicines = 1 } },
                new DroppedLoot { Id = "d3", Pos = At(24), Loot = new Loot { Food = 2 } },
                new DroppedLoot { Id = "d4", Pos = At(26), Loot = new Loot { Parts = 3 } },
                new DroppedLoot { Id = "d5", Pos = At(26), Loot = new Loot { Supplies = 2 } },
                new DroppedLoot { Id = "d6", Pos = At(28), Loot = new Loot { Weapon = "precisionRifle" }, From = "s1" },
                new DroppedLoot { Id = "d7", Pos = At(28), Loot = new Loot { Medicines = 1 }, From = "s1" },
            };
            state.Units = new System.Collections.Generic.List<Unit>();
            MapLab.Lighting(true);
            MapLab.Map(state, "city-7");
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            fx = new GameObject("Efectos").AddComponent<Fx>();
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tiles) ? tiles : 10;
            MapSpace.Aim(camera, MapSpace.TileToWorld(state, At(24)), off);
            // (Each thing's box as the map fits it, in tiles: to set against 0.10.1's syntyProp.)
            var probe = new GameObject("Medida").transform;
            foreach (var id in new[] { "dropBag", "fallenPack", "lootMedicalKit", "lootFoodTop", "lootCan" })
            {
                var placed = MapBuilder.Prop(probe, id, MapLab.Prefabs());
                if (placed == null) { Debug.Log($"[tirado] {id}: none"); continue; }
                var box = placed.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Debug.Log($"[tirado] {id}: {box.size.x / MapSpace.Tile:0.000} {box.size.y / MapSpace.Tile:0.000} {box.size.z / MapSpace.Tile:0.000}");
            }
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("tirado ")).OrderBy(t => t.name))
                foreach (Transform part in t)
                {
                    var rs = part.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    var box = rs.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    Debug.Log($"[tirado] {t.name} {part.name}: {box.size.x / MapSpace.Tile:0.000} {box.size.y / MapSpace.Tile:0.000} {box.size.z / MapSpace.Tile:0.000}");
                }
            sequence = async () => await Awaitable.WaitForSecondsAsync(2);
            EditorApplication.playModeStateChanged += Started;
        }

        /// <summary>
        /// The walk on city-7 itself (Gameplay 26/10, A1): a soldier down the street along the west wall of the block at
        /// (33–39, 5–11) and round its south-west corner east along row 12, the rounded corner passing the building's.
        /// BASTION_DIST: the game's camera that many tiles off (24 unless said).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Esquina -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 90 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Esquina()
        {
            if (Resources.Load<FxArt>("FxArt") == null) FxArtMake.Run();
            state = MapLab.Golden("city-7");
            MapLab.Lighting(true);
            MapLab.Map(state, "city-7");
            var walker = state.Units.First(u => u.Alive && u.Team == "xcom");
            state.Units = new System.Collections.Generic.List<Unit> { Put(walker, 32, 6, Math.PI / 2) };
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            fx = new GameObject("Efectos").AddComponent<Fx>();
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tiles) ? tiles : 24;
            MapSpace.Aim(camera, MapSpace.TileToWorld(state, new Pos { X = 33, Y = 11 }), off);
            var way = new System.Collections.Generic.List<Pos>();
            for (int y = 7; y <= 12; y++) way.Add(new Pos { X = 32, Y = y });
            for (int x = 33; x <= 38; x++) way.Add(new Pos { X = x, Y = 12 });
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(1);
                    Debug.Log($"[combat] {Time.time:0.00} s: walk");
                    await units.MoveAlong(walker.Id, way, 1);
                    Debug.Log($"[combat] {Time.time:0.00} s: there, {units.Feet(walker.Id)}");
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
            EditorApplication.playModeStateChanged += Started;
        }

        /// <summary>
        /// Aiming (Gameplay 26/10, A2: unitViews.ts aimAt, raiseWeapon, apply's step back): a soldier with a machine gun
        /// (BASTION_ARMA: assaultRifle, pumpShotgun…) aims at one of the dead four tiles off as it is picked (marked: A6,
        /// Highlight, taken off as it fires, as UI does), fires, lowers;
        /// the dead one comes up beside it, and it aims again at point blank, stepping back, fires and lowers. On a plain
        /// floor, the camera low from the side (BASTION_VIEW=game, the game's).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Apuntar -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 110 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Apuntar()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Plain(u => u.Id == soldierId || u.Id == deadId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 10, 10, 0);
            target = Put(state.Units.First(u => u.Id == deadId), 14, 10, Math.PI);
            soldier.Template = "rookie";
            soldier.Arms = new Arms { Primary = Environment.GetEnvironmentVariable("BASTION_ARMA") ?? "machineGun" };
            // (BASTION_TEMPLATE: the target another kind, a Cuervo with its weapons: trooper, lancer.)
            if (Environment.GetEnvironmentVariable("BASTION_TEMPLATE") is string kind) target.Template = kind;
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(1);
                    for (int round = 0; round < 2; round++)
                    {
                        var at = units.Chest(target.Id);
                        Debug.Log($"[combat] {Time.time:0.00} s: picked ({Vector3.Distance(units.Feet(soldier.Id), units.Feet(target.Id)) / MapSpace.Tile:0.00} tiles)");
                        units.AimAt(soldier.Id, at);
                        units.Highlight(target.Id);
                        await Awaitable.WaitForSecondsAsync(1.2f);
                        Debug.Log($"[combat] {Time.time:0.00} s: fires; muzzle {units.Muzzle(soldier.Id):F2}, feet {units.Feet(soldier.Id):F2}; {units.Stance(soldier.Id)}");
                        units.Highlight(null);
                        int aim = await units.RaiseWeapon(soldier.Id, at);
                        for (int shot = 0; shot < 2; shot++)
                        {
                            var from = units.Muzzle(soldier.Id);
                            fx.MuzzleFlash(from, Fx.Hex(0xffd27a));
                            units.Recoil(soldier.Id);
                            await fx.Bullet(from, at, Fx.Hex(0xffd27a));
                            _ = units.HitReact(target.Id, 1);
                            await Awaitable.WaitForSecondsAsync(0.3f);
                        }
                        await Awaitable.WaitForSecondsAsync(0.8f);
                        await units.LowerWeapon(soldier.Id, aim);
                        await Awaitable.WaitForSecondsAsync(0.8f);
                        if (round == 0) await units.MoveAlong(target.Id, new[] { new Pos { X = 13, Y = 10 }, new Pos { X = 12, Y = 10 }, new Pos { X = 11, Y = 10 } }, 1);
                    }
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
        }

        /// <summary>
        /// On watch (Gameplay 26/10, A3: unitViews.ts sync's aimTarget, the blade on guard): a soldier with a rifle and one
        /// with the knife at rest, both put on watch (the second with the blade, Unit.WatchMelee), one of the dead walks up
        /// and the rifle fires at it from the watch and goes back to it; then both off watch. On a plain floor, the camera
        /// low from the side (BASTION_VIEW=game, the game's).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Vigilancia -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 100 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Vigilancia()
        {
            var first = MapLab.Golden("city-7");
            var ids = first.Units.Where(u => u.Alive && u.Team == "xcom").Select(u => u.Id).Take(2).ToList();
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Plain(u => ids.Contains(u.Id) || u.Id == deadId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == ids[0]), 10, 10, 0);
            var blade = Put(state.Units.First(u => u.Id == ids[1]), 12, 11, 0);
            target = Put(state.Units.First(u => u.Id == deadId), 16, 9, Math.PI);
            soldier.Template = blade.Template = "rookie";
            soldier.Arms = new Arms { Primary = "assaultRifle" };
            blade.Arms = new Arms { Primary = "assaultRifle", Melee = Environment.GetEnvironmentVariable("BASTION_HOJA") };
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(1);
                    Debug.Log($"[combat] {Time.time:0.00} s: on watch");
                    soldier.Overwatch = blade.Overwatch = true;
                    blade.WatchMelee = true;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: the dead walk; {units.ArmsReport(blade.Id)}");
                    await units.MoveAlong(target.Id, new[] { new Pos { X = 15, Y = 9 }, new Pos { X = 14, Y = 9 } }, 1);
                    var at = units.Chest(target.Id);
                    Debug.Log($"[combat] {Time.time:0.00} s: fires from the watch");
                    int aim = await units.RaiseWeapon(soldier.Id, at);
                    var from = units.Muzzle(soldier.Id);
                    fx.MuzzleFlash(from, Fx.Hex(0xffd27a));
                    units.Recoil(soldier.Id);
                    await fx.Bullet(from, at, Fx.Hex(0xffd27a));
                    await units.HitReact(target.Id, 1);
                    soldier.Overwatch = false;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(0.5f);
                    await units.LowerWeapon(soldier.Id, aim);
                    await Awaitable.WaitForSecondsAsync(1.2f);
                    Debug.Log($"[combat] {Time.time:0.00} s: the blade off watch");
                    blade.Overwatch = false;
                    blade.WatchMelee = null;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
        }

        /// <summary>
        /// Where the blade's guard is in its stroke's clip (A3): soldiers in a row with the knife (BASTION_HOJA=machete, the
        /// machete), each holding the clip at a time BASTION_GUARDIA_PASO apart (0.1 s unless said), from the left. Still.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Guardia -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 30 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Guardia()
        {
            var first = MapLab.Golden("city-7");
            var soldiers = first.Units.Where(u => u.Alive && u.Team == "xcom").ToList();
            var ids = soldiers.Select(u => u.Id).ToList();
            Plain(u => ids.Contains(u.Id), 9, 9 + ids.Count - 1);
            float step = float.TryParse(Environment.GetEnvironmentVariable("BASTION_GUARDIA_PASO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float st) ? st : 0.1f;
            float start = float.TryParse(Environment.GetEnvironmentVariable("BASTION_GUARDIA_DESDE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sf) ? sf : 0;
            for (int k = 0; k < ids.Count; k++)
            {
                var u = Put(state.Units.First(x => x.Id == ids[k]), 9 + k, 10, 0);
                u.Template = "rookie";
                u.Arms = new Arms { Primary = "assaultRifle", Melee = Environment.GetEnvironmentVariable("BASTION_HOJA") };
            }
            sequence = async () =>
            {
                units.Sync(state);
                Reveal();
                for (int k = 0; k < ids.Count; k++)
                {
                    units.GuardForLab(ids[k], start + k * step);
                    Debug.Log($"[combat] {ids[k]} at {start + k * step:0.00} s");
                }
                await Awaitable.WaitForSecondsAsync(2);
            };
        }

        /// <summary>
        /// A blade's blow (Ana's #10: the pan, the bat, the crowbar, the chainsaw; BASTION_HOJA, the machete unless said): a
        /// soldier with a rifle in the hands and the blade at the belt (the chainsaw on the back), one of the dead on the next
        /// tile, as the game strikes; a second in, the blow (Lunge: drawn, swung, back, the rifle again), and then on watch
        /// with the blade, on guard. On a plain floor, the camera low from the side (BASTION_VIEW=game, the game's).
        /// BASTION_ELEGIR=1: the target picked first, as the game does (the blade drawn on guard, let go at the confirm).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Golpes -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 50 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Golpes()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Plain(u => u.Id == soldierId || u.Id == deadId, 10, 11);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 10, 10, 0);
            target = Put(state.Units.First(u => u.Id == deadId), 11, 10, Math.PI);
            soldier.Template = "rookie";
            // (The knife is no weapon of the Armería: what one strikes with when the belt's slot is empty.)
            string blade = Environment.GetEnvironmentVariable("BASTION_HOJA") ?? "machete";
            soldier.Arms = new Arms { Primary = "assaultRifle", Melee = blade == "knife" ? null : blade };
            // The blow's sounds as EventPlayer plays them (Red's, 29/10: BASTION_SONIDO_TRAZA=1 says which and when).
            var sfx = new Bastion.Client.Audio.CombatSound();
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.name == "Shot camera");
            sfx.Attach(camera, () => units.Chest(target.Id), MapSpace.Tile, (s, p) => MapSpace.TileToWorld(s, p, MapSpace.Tile));
            string weapon = Content.AttackWeapon(soldier, AbilityIds.Slash).Id;
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    Debug.Log($"[combat] {units.ArmsReport(soldier.Id)}");
                    // (As the game: CombatController.Orders' pick, EventPlayer's wait before the lunge.)
                    bool pick = Environment.GetEnvironmentVariable("BASTION_ELEGIR") == "1";
                    if (pick) units.AimAt(soldier.Id, units.Chest(target.Id), true);
                    await Awaitable.WaitForSecondsAsync(1);
                    if (pick)
                    {
                        units.AimAt(soldier.Id, null);
                        await Awaitable.WaitForSecondsAsync(0.2f);
                    }
                    Debug.Log($"[combat] {Time.time:0.00} s: the blow ({weapon})");
                    sfx.Melee("swing", units.Chest(soldier.Id), weapon);
                    await units.Lunge(soldier.Id, target.Id, false, () =>
                    {
                        Debug.Log($"[combat] {Time.time:0.00} s: it lands");
                        sfx.Melee("sword", units.Chest(target.Id), weapon);
                        sfx.Impact(Bastion.Client.Audio.CombatSound.Body(target), units.Chest(target.Id));
                        _ = units.HitReact(target.Id, 1);
                    });
                    Debug.Log($"[combat] {Time.time:0.00} s: struck; {units.ArmsReport(soldier.Id)}");
                    await Awaitable.WaitForSecondsAsync(1);
                    soldier.Overwatch = true;
                    soldier.WatchMelee = true;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.5f);
                    Debug.Log($"[combat] {Time.time:0.00} s: on guard; {units.ArmsReport(soldier.Id)}");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
        }

        static Unit Put(Unit u, int x, int y, double facing)
        {
            u.Pos = new Pos { X = x, Y = y };
            u.Facing = facing;
            return u;
        }

        /// <summary>
        /// The dead (Gameplay: their waiting, their feeding): one walks up to a soldier dragging its feet, lunges, bites;
        /// the soldier falls and it kneels and eats; three seconds on it gets up and walks on; another stands by swaying.
        /// </summary>
        public static void Dead()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            var deadIds = first.Units.Where(u => u.Alive && u.Template == "zombie").Select(u => u.Id).Take(2).ToList();
            Plain(u => u.Id == soldierId || deadIds.Contains(u.Id), 10, 14);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 10, 10, 0);
            target = Put(state.Units.First(u => u.Id == deadIds[0]), 14, 10, Math.PI);
            Put(state.Units.First(u => u.Id == deadIds[1]), 15, 9, Math.PI);
            sequence = async () =>
            {
                units.Sync(state);
                Reveal();
                await Awaitable.WaitForSecondsAsync(1);
                // Where each figure's head is registered against the top of what is drawn (UI's marks hang there).
                foreach (var u in state.Units)
                {
                    var feet = Figures.FeetOf(u.Id);
                    var head = Figures.HeadOf(u.Id);
                    var drawn = feet.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    Debug.Log($"[combat] {u.Id} {u.Template}: feet {feet.position:F2}, head bone {head.position:F2} ({head.name}), drawn centre {drawn.center:F2} top {drawn.max.y:F2}");
                }
                Debug.Log($"[combat] {Time.time:0.00} s: the dead walk");
                await units.MoveAlong(target.Id, new[] { new Pos { X = 13, Y = 10 }, new Pos { X = 12, Y = 10 }, new Pos { X = 11, Y = 10 } }, 1);
                Debug.Log($"[combat] {Time.time:0.00} s: lunge");
                await units.Lunge(target.Id, soldier.Id, true, () =>
                {
                    fx.Bite(units.Chest(soldier.Id), units.Chest(target.Id));
                    _ = units.HitReact(soldier.Id, 1);
                });
                Debug.Log($"[combat] {Time.time:0.00} s: dies and is eaten");
                var dies = units.Die(soldier.Id);
                await units.Feed(target.Id, soldier.Id, () => fx.Bite(units.Chest(soldier.Id), units.Head(target.Id)));
                await dies;
                await Awaitable.WaitForSecondsAsync(3);
                Debug.Log($"[combat] {Time.time:0.00} s: gets up");
                await units.StandUpFed();
                await units.MoveAlong(target.Id, new[] { new Pos { X = 12, Y = 11 } }, 1);
                Debug.Log($"[combat] {Time.time:0.00} s: done");
            };
        }

        /// <summary>
        /// The special dead (City Zombies' and Boss Zombies' bodies: UnitViews.Dead) as 0.10.1's referencia.html?map=plaza
        /// films them (scratchpad muertos/make.cjs, seen from the other side): BASTION_TEMPLATE (xenoid unless said; mec,
        /// sectoid, bloater) with the id BASTION_ID (m0 unless said: which of its bodies) walks three tiles up to a soldier,
        /// takes a hit, screams if it is the Chillón and, if it eats, lunges, bites, eats it three seconds and gets up; then
        /// dies (the Hinchado bursting as it falls). BASTION_FILA=1: every
        /// special body in a row facing the camera, a Sidekick zombie first for size. BASTION_VIEW=game: the game's camera.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Especiales -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 140 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Especiales()
        {
            UnitViews.DeadArmsOff = Environment.GetEnvironmentVariable("BASTION_SIN_BRAZOS") == "1";
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Unit Like(Unit u, string id, string template)
            {
                var copy = Bastion.Net.Json.Read<Unit>(Bastion.Net.Json.Write(u));
                copy.Id = id;
                copy.Template = template;
                return copy;
            }
            string fila = Environment.GetEnvironmentVariable("BASTION_FILA");
            if (fila == "1" || fila == "comunes" || fila == "hinchados")
            {
                // (comunes: the common dead's 17 bodies, in their order, each its id's pick: syntyManifest.ts UNIT_BODIES zombie.
                // hinchados: a common one for size and the five Hinchados, in DeadBodies' order, Ana's #13.)
                var row = fila == "comunes"
                    ? new[] { "c2", "c6", "c11", "c15", "c29", "c1", "c5", "c9", "c16", "c0", "c4", "c8", "c17", "c3", "c7", "c10", "c14" }.Select(i => ("zombie", i)).ToArray()
                    : fila == "hinchados" ? new[] { ("zombie", "z0"), ("bloater", "h4"), ("bloater", "h0"), ("bloater", "h1"), ("bloater", "h2"), ("bloater", "h3") }
                    : new[] { ("zombie", "z0"), ("xenoid", "m0"), ("xenoid", "m3"), ("xenoid", "m2"), ("xenoid", "m1"), ("mec", "m0"), ("mec", "m3"), ("mec", "m2"), ("mec", "m1"), ("sectoid", "c0"), ("bloater", "h0") };
                int last = 2 + row.Length - 1;
                Plain(u => u.Id == deadId, 2, last);
                var like = state.Units[0];
                state.Units = row.Select((r, k) => Put(Like(like, fila == "comunes" ? r.Item2 : r.Item1 + "-" + r.Item2, r.Item1), 2 + k, 10, Math.PI / 2)).ToList();
                // From high up, as 0.10.1's (8 tiles up and 9 off, over the row's middle).
                var shot = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.name == "Shot camera");
                var middle = (MapSpace.World(2, 10) + MapSpace.World(last, 10)) / 2;
                shot.transform.position = middle + new Vector3(0, 8, -9) * MapSpace.Tile * (row.Length / 11f);
                shot.transform.LookAt(middle + Vector3.up * 0.9f * MapSpace.Tile);
                sequence = async () =>
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(0.5f);
                    Debug.Log($"[combat] especiales: {string.Join(", ", state.Units.Select(u => $"{u.Id} {Figures.FeetOf(u.Id)?.GetComponentInChildren<Animator>()?.name}"))}");
                    // What each is drawn with: its renderers, each one's submeshes and materials.
                    foreach (var u in state.Units.Take(3).Concat(state.Units.Skip(9)))
                        Debug.Log($"[combat] {u.Id} drawn by " + string.Join("; ", Figures.FeetOf(u.Id).GetComponentsInChildren<Renderer>().Select(r => $"{r.name} {(r is SkinnedMeshRenderer s ? s.sharedMesh?.subMeshCount : r.GetComponent<MeshFilter>()?.sharedMesh?.subMeshCount)} sub, {r.sharedMaterials.Length} mat")));
                };
                return;
            }
            string template = Environment.GetEnvironmentVariable("BASTION_TEMPLATE") ?? "xenoid";
            string id = Environment.GetEnvironmentVariable("BASTION_ID") ?? "m0";
            Plain(u => u.Id == soldierId || u.Id == deadId, 14, 18);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 14, 10, 0);
            target = Put(Like(state.Units.First(u => u.Id == deadId), id, template), 18, 10, Math.PI);
            state.Units = new System.Collections.Generic.List<Unit> { soldier, target };
            // BASTION_MEDIR=1: how far it leans (its trunk, hips to neck, and its head, from upright along its facing, in
            // degrees), standing six seconds and walking back and forth, as 0.10.1's measured (scratchpad muertos/medir-inclinacion.js).
            // BASTION_MEDIR=brazos: where its hands are from its upper chest along its facing (tiles, ahead and up) and how bent
            // its elbows (180° straight), standing three seconds and walking, as 0.10.1's (scratchpad muertos/medir-brazos-*.js).
            if (Environment.GetEnvironmentVariable("BASTION_MEDIR") == "brazos")
            {
                sequence = async () =>
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(2);
                    var root = Figures.FeetOf(target.Id);
                    var a = root.GetComponentInChildren<Animator>();
                    var chest = a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest);
                    (float ahead, float up, float bend) Probe()
                    {
                        float ahead = 0, up = 0, bend = 0;
                        foreach (var (sh, el, ha) in new[] { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
                        {
                            Vector3 S = a.GetBoneTransform(sh).position, E = a.GetBoneTransform(el).position, H = a.GetBoneTransform(ha).position;
                            var d = H - chest.position;
                            ahead += Vector3.Dot(d, root.forward) / MapSpace.Tile / 2;
                            up += d.y / MapSpace.Tile / 2;
                            bend += Vector3.Angle(S - E, H - E) / 2;
                        }
                        return (ahead, up, bend);
                    }
                    async Awaitable<string> Mean(float seconds)
                    {
                        float t0 = Time.time, x = 0, y = 0, z = 0;
                        int n = 0;
                        while (Time.time - t0 < seconds)
                        {
                            await Awaitable.NextFrameAsync();
                            var (p, q, r) = Probe();
                            x += p; y += q; z += r; n++;
                        }
                        return FormattableString.Invariant($"ahead {x / n:0.000}, up {y / n:0.000}, elbow {z / n:0}°");
                    }
                    Debug.Log($"[arms] {template} still: {await Mean(3)}");
                    var walk = units.MoveAlong(target.Id, new[] { new Pos { X = 17, Y = 10 }, new Pos { X = 16, Y = 10 }, new Pos { X = 15, Y = 10 } }, 1);
                    await Awaitable.WaitForSecondsAsync(0.5f);
                    Debug.Log($"[arms] {template} walking: {await Mean(1.2f)}");
                    await walk;
                };
                return;
            }
            if (Environment.GetEnvironmentVariable("BASTION_MEDIR") == "1")
            {
                sequence = async () =>
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(2);
                    var a = Figures.FeetOf(target.Id).GetComponentInChildren<Animator>();
                    var root = Figures.FeetOf(target.Id);
                    (float trunk, float head) Lean()
                    {
                        var hips = a.GetBoneTransform(HumanBodyBones.Hips).position;
                        float Of(Vector3 p) { var d = p - hips; return Mathf.Atan2(Vector3.Dot(d, root.forward), Vector3.Dot(d, Vector3.up)) * Mathf.Rad2Deg; }
                        return (Of((a.GetBoneTransform(HumanBodyBones.Neck) ?? a.GetBoneTransform(HumanBodyBones.Head)).position), Of(a.GetBoneTransform(HumanBodyBones.Head).position));
                    }
                    async Awaitable<string> Mean(float seconds)
                    {
                        float t0 = Time.time, trunk = 0, head = 0;
                        int n = 0;
                        while (Time.time - t0 < seconds)
                        {
                            await Awaitable.NextFrameAsync();
                            var (x, y) = Lean();
                            trunk += x;
                            head += y;
                            n++;
                        }
                        return FormattableString.Invariant($"trunk {trunk / n:0.0}°, head {head / n:0.0}°");
                    }
                    Debug.Log($"[lean] {template} still: {await Mean(6)}");
                    var walk = units.MoveAlong(target.Id, new[] { new Pos { X = 17, Y = 10 }, new Pos { X = 16, Y = 10 }, new Pos { X = 15, Y = 10 } }, 1);
                    await Awaitable.WaitForSecondsAsync(0.5f);
                    Debug.Log($"[lean] {template} walking: {await Mean(1.2f)}");
                    await walk;
                };
                return;
            }
            bool eats = template == "zombie" || template == "xenoid" || template == "mec";
            sequence = async () =>
            {
                units.Sync(state);
                Reveal();
                await Awaitable.WaitForSecondsAsync(1);
                Debug.Log($"[combat] {Time.time:0.00} s: {template} {id} ({Figures.FeetOf(target.Id)?.GetComponentInChildren<Animator>()?.name}) walks");
                Debug.Log($"[combat] {soldier.Id} drawn by " + string.Join("; ", Figures.FeetOf(soldier.Id).GetComponentsInChildren<Renderer>().Select(r => $"{r.name} {(r is SkinnedMeshRenderer s ? s.sharedMesh?.subMeshCount : r.GetComponent<MeshFilter>()?.sharedMesh?.subMeshCount)} sub, {r.sharedMaterials.Length} mat")));
                await units.MoveAlong(target.Id, new[] { new Pos { X = 17, Y = 10 }, new Pos { X = 16, Y = 10 }, new Pos { X = 15, Y = 10 } }, 1);
                Debug.Log($"[combat] {Time.time:0.00} s: hit");
                // (The soldier's aim at it: the blow comes from the soldier, as 0.10.1's lab's die(T, from).)
                await units.RaiseWeapon(soldier.Id, units.Chest(target.Id));
                await units.HitReact(target.Id, 1);
                await Awaitable.WaitForSecondsAsync(0.4f);
                if (template == "sectoid")
                {
                    Debug.Log($"[combat] {Time.time:0.00} s: screams");
                    _ = units.Scream(target.Id);
                    fx.Scream(units.Head(target.Id));
                    await Awaitable.WaitForSecondsAsync(0.4f);
                    // Where its arms point at the scream's height (in its own frame: x its right, z ahead).
                    var a = Figures.FeetOf(target.Id).GetComponentInChildren<Animator>();
                    string Dir(HumanBodyBones from, HumanBodyBones to) => a.GetBoneTransform(from) is Transform f && a.GetBoneTransform(to) is Transform t2 ? Figures.FeetOf(target.Id).InverseTransformDirection(t2.position - f.position).normalized.ToString("F2") : "-";
                    Debug.Log($"[combat] scream: left upper {Dir(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm)} fore {Dir(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand)}; right upper {Dir(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm)} fore {Dir(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand)}");
                    await Awaitable.WaitForSecondsAsync(0.8f);
                }
                if (eats)
                {
                    Debug.Log($"[combat] {Time.time:0.00} s: lunge");
                    await units.Lunge(target.Id, soldier.Id, true, () =>
                    {
                        fx.Bite(units.Chest(soldier.Id), units.Chest(target.Id));
                        _ = units.HitReact(soldier.Id, 1);
                    });
                    var dies = units.Die(soldier.Id);
                    await units.Feed(target.Id, soldier.Id, () => fx.Bite(units.Chest(soldier.Id), units.Head(target.Id)));
                    await dies;
                    await Awaitable.WaitForSecondsAsync(3);
                    Debug.Log($"[combat] {Time.time:0.00} s: up");
                    await units.StandUpFed();
                    await Awaitable.WaitForSecondsAsync(0.5f);
                }
                Debug.Log($"[combat] {Time.time:0.00} s: dies");
                if (template == "bloater")
                {
                    // It bursts as it falls (EventPlayer: the death not waited for, the blast 0.15 s on).
                    var at = units.Feet(target.Id) + Vector3.up * 0.2f * MapSpace.Tile;
                    var falls = units.Die(target.Id);
                    await Awaitable.WaitForSecondsAsync(0.15f);
                    units.BlastAt(at);
                    _ = fx.Explosion(at, 1.5f);
                    fx.Burst(at, 1.5f);
                    await falls;
                }
                else await units.Die(target.Id);
                Debug.Log($"[combat] {Time.time:0.00} s: done");
            };
        }

        /// <summary>
        /// What a horde costs a frame (Gameplay 26/10, the special dead's bodies): BASTION_N of the dead (40 unless said) on a
        /// plain floor under the game's camera (24 tiles), in 0.10.1's horde lab's mix (horde.ts TEMPLATES: three common for
        /// a Corredor, an Acorazado and a Hinchado), every other one walking back and forth; BASTION_SOLO_ZOMBIS=1, all of
        /// them common (Sidekick's), to weigh against. In play mode, three turns of 120 frames, the least of each turn's
        /// mean (real time a frame, in the editor on this machine). BASTION_QUIETOS=1: nobody walks (each waiting in its
        /// own time, Ana 28/10; FigureAnimator's BASTION_SIN_FASE=1, all in step as before); BASTION_DIST its distance.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Horda -shotPlay -shotCamera "Shot camera" -shotWarm 16 -shotFrames 1 -shotOut &lt;file&gt;
        /// </summary>
        public static void Horda()
        {
            var first = MapLab.Golden("city-7");
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            int n = int.TryParse(Environment.GetEnvironmentVariable("BASTION_N"), out int count) ? count : 40;
            bool plain = Environment.GetEnvironmentVariable("BASTION_SOLO_ZOMBIS") == "1";
            string[] mix = { "zombie", "zombie", "zombie", "xenoid", "mec", "bloater" };
            if (Environment.GetEnvironmentVariable("BASTION_VIEW") == null) Environment.SetEnvironmentVariable("BASTION_VIEW", "game");
            bool still = Environment.GetEnvironmentVariable("BASTION_QUIETOS") == "1";
            Plain(u => u.Id == deadId, 8, 16);
            var like = state.Units[0];
            state.Units = Enumerable.Range(0, n).Select(k =>
            {
                var u = Bastion.Net.Json.Read<Unit>(Bastion.Net.Json.Write(like));
                u.Id = $"h{k}";
                u.Template = plain ? "zombie" : mix[k % mix.Length];
                return Put(u, 6 + k % 8 * 2, 4 + k / 8 * 2, Math.PI / 2);
            }).ToList();
            // (Still: the camera on the crowd's middle, at BASTION_DIST.)
            if (still && GameObject.Find("Shot camera")?.GetComponent<Camera>() is Camera shot)
            {
                float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tiles) ? tiles : 24;
                int rows = (n + 7) / 8;
                MapSpace.Aim(shot, (MapSpace.World(6, 4) + MapSpace.World(6 + Math.Min(n, 8) * 2 - 2, 4 + rows * 2 - 2)) / 2, off);
            }
            sequence = async () =>
            {
                units.Sync(state);
                Reveal();
                // Every other one walking two tiles and back, on and on.
                bool on = true;
                foreach (var u in state.Units.Where((u, k) => !still && k % 2 == 1))
                    Walk(u);
                async void Walk(Unit u)
                {
                    var there = new[] { new Pos { X = u.Pos.X + 1, Y = u.Pos.Y }, new Pos { X = u.Pos.X + 2, Y = u.Pos.Y } };
                    var back = new[] { new Pos { X = u.Pos.X + 1, Y = u.Pos.Y }, new Pos { X = u.Pos.X, Y = u.Pos.Y } };
                    while (on && Application.isPlaying)
                    {
                        await units.MoveAlong(u.Id, there, 1);
                        await units.MoveAlong(u.Id, back, 1);
                    }
                }
                await Awaitable.WaitForSecondsAsync(3);
                double least = double.MaxValue;
                for (int turn = 0; turn < 3; turn++)
                {
                    float spent = 0;
                    for (int k = 0; k < 120; k++)
                    {
                        await Awaitable.NextFrameAsync();
                        spent += Time.unscaledDeltaTime;
                    }
                    least = Math.Min(least, spent / 120 * 1000);
                }
                on = false;
                var mixed = state.Units.GroupBy(u => u.Template).Select(g => $"{g.Key} ×{g.Count()}");
                Debug.Log(FormattableString.Invariant($"[horde] {n} of the dead ({string.Join(", ", mixed)}), half walking: {least:0.00} ms a frame (the least of three turns of 120), {QualitySettings.names[QualitySettings.GetQualityLevel()]}"));
            };
        }

        /// <summary>A soldier in cover (hunkered): kneeling with the rifle, two tiles crouched, aims kneeling, fires, back at rest.</summary>
        public static void Crouch()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            Plain(u => u.Id == soldierId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 10, 10, 0);
            soldier.Hunkered = true;
            sequence = async () =>
            {
                units.Sync(state);
                Reveal();
                await Awaitable.WaitForSecondsAsync(1.5f);
                Debug.Log($"[combat] {Time.time:0.00} s: crouched walk");
                await units.MoveAlong(soldier.Id, new[] { new Pos { X = 11, Y = 10 }, new Pos { X = 12, Y = 10 } }, 1);
                await Awaitable.WaitForSecondsAsync(0.5f);
                var at = MapSpace.World(17, 10) + Vector3.up * 1.2f;
                int aim = await units.RaiseWeapon(soldier.Id, at);
                await Awaitable.WaitForSecondsAsync(0.4f);
                for (int shot = 0; shot < 2; shot++)
                {
                    var from = units.Muzzle(soldier.Id);
                    fx.MuzzleFlash(from, Fx.Hex(0xffd27a));
                    units.Recoil(soldier.Id);
                    await fx.Bullet(from, at, Fx.Hex(0xffd27a));
                    await Awaitable.WaitForSecondsAsync(0.6f);
                }
                await units.LowerWeapon(soldier.Id, aim);
                Debug.Log($"[combat] {Time.time:0.00} s: done");
            };
        }

        /// <summary>
        /// A soldier's weapons as 0.10.1's ARMS_CASES (weaponLab.ts; BASTION_ARMAS: «pistola» the pistol alone, «fusil» a
        /// rifle and the pistol drawn to fire, «cuchillo» a rifle and the knife at the belt, «machete» a shotgun and the
        /// machete): at rest 2 s, aims at one of the dead four tiles off and fires twice, lowers it, reloads, and strikes the
        /// dead one brought up beside it. On a plain floor, the camera low from the side (BASTION_VIEW=game, the game's).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Armas -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 120 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Armas()
        {
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            Plain(u => u.Id == soldierId || u.Id == deadId, 10, 13);
            soldier = Put(state.Units.First(u => u.Id == soldierId), 10, 10, 0);
            target = Put(state.Units.First(u => u.Id == deadId), 14, 10, Math.PI);
            string which = Environment.GetEnvironmentVariable("BASTION_ARMAS") ?? "pistola";
            soldier.Template = "rookie";
            soldier.Arms = which switch
            {
                "fusil" => new Arms { Primary = "assaultRifle" },
                "cuchillo" => new Arms { Primary = "assaultRifle" },
                "machete" => new Arms { Primary = "pumpShotgun", Melee = "machete" },
                _ => new Arms { Primary = null },
            };
            bool sidearm = which == "fusil";
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    Debug.Log($"[combat] {Time.time:0.00} s: {which}, at rest");
                    await Awaitable.WaitForSecondsAsync(2f);
                    Debug.Log($"[armas] at rest: {units.ArmsReport(soldier.Id)}");
                    var at = units.Chest(target.Id);
                    Debug.Log($"[combat] {Time.time:0.00} s: aims");
                    int aim = await units.RaiseWeapon(soldier.Id, at, sidearm);
                    await Awaitable.WaitForSecondsAsync(0.4f);
                    for (int shot = 0; shot < 2; shot++)
                    {
                        var from = units.Muzzle(soldier.Id);
                        fx.MuzzleFlash(from, Fx.Hex(0xffd27a));
                        units.Recoil(soldier.Id);
                        await fx.Bullet(from, at, Fx.Hex(0xffd27a));
                        await Awaitable.WaitForSecondsAsync(0.4f);
                    }
                    await units.LowerWeapon(soldier.Id, aim);
                    await Awaitable.WaitForSecondsAsync(0.8f);
                    Debug.Log($"[combat] {Time.time:0.00} s: reloads");
                    await units.Reload(soldier.Id);
                    await Awaitable.WaitForSecondsAsync(0.8f);
                    // The dead one brought up beside it, to strike.
                    await units.MoveAlong(target.Id, new[] { new Pos { X = 13, Y = 10 }, new Pos { X = 12, Y = 10 }, new Pos { X = 11, Y = 10 } }, 1);
                    await Awaitable.WaitForSecondsAsync(0.3f);
                    Debug.Log($"[combat] {Time.time:0.00} s: strikes");
                    await units.Lunge(soldier.Id, target.Id, false, () => Debug.Log($"[combat] {Time.time:0.00} s: the blow lands; {units.ArmsReport(soldier.Id)}"));
                    Debug.Log($"[armas] after: {units.ArmsReport(soldier.Id)}");
                    await Awaitable.WaitForSecondsAsync(1f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
        }

        /// <summary>
        /// Each figure of a state (BASTION_MAP, step2/city-3-elimination unless said) checked in play mode: how far its head
        /// bone is, across, from the middle of what is drawn (UI's marks hang over the head bone).
        /// </summary>
        public static void Heads()
        {
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "../step2/city-3-elimination";
            state = MapLab.Golden(name);
            MapLab.Lighting(true);
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            sequence = async () =>
            {
                units.Sync(state);
                Reveal();
                await Awaitable.WaitForSecondsAsync(0.5f);
                foreach (var u in state.Units.Where(u => u.Alive))
                {
                    var feet = Figures.FeetOf(u.Id);
                    var head = Figures.HeadOf(u.Id);
                    if (feet == null || head == null) continue;
                    var renderers = feet.GetComponentsInChildren<SkinnedMeshRenderer>();
                    if (renderers.Length == 0) continue;
                    var drawn = renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    var off = head.position - drawn.center;
                    off.y = 0;
                    var lean = head.position - feet.position;
                    lean.y = 0;
                    Debug.Log($"[heads] {u.Id}: head {lean.magnitude:0.00} m across from its feet");
                    string figure = feet.childCount > 0 ? feet.GetChild(0).name : "-";
                    Debug.Log($"[heads] {u.Id} {u.Template} {figure}: head {off.magnitude:0.00} m across from the middle of what is drawn" + (off.magnitude > 0.3f ? "  <<<" : ""));
                    // A red ball on the head bone, to see it against the head drawn.
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ball.transform.SetParent(head, false);
                    ball.transform.localScale = Vector3.one * 0.12f / head.lossyScale.x;
                    ball.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.red };
                }
                // The camera, as UI's game camera, on the first of the dead of preset BASTION_PRESET (ApocalypseZombie_02 unless said).
                string preset = Environment.GetEnvironmentVariable("BASTION_PRESET") ?? "ApocalypseZombie_02";
                var one = state.Units.FirstOrDefault(u => u.Alive && Figures.FeetOf(u.Id) is Transform f && f.childCount > 0 && f.GetChild(0).name == preset);
                if (one != null) MapSpace.Aim(Camera.main, Figures.FeetOf(one.Id).position, 12);
            };
            var shot = new GameObject("Shot camera").AddComponent<Camera>();
            shot.tag = "MainCamera";
            shot.clearFlags = CameraClearFlags.SolidColor;
            shot.backgroundColor = new Color(0.5f, 0.5f, 0.52f);
            EditorApplication.playModeStateChanged += Started;
        }

        /// <summary>
        /// The scene Partida as the game starts it, without UI: in play mode a recorded state (BASTION_MAP) is shown by
        /// Battlefield (the map from the game's catalog, not the editor's) and the figures put on it; the camera
        /// («Cámara») over the squad, 24 tiles off (UI's default). Run with -shotScene Assets/Bastion/Scenes/Partida.unity
        /// -shotPlay -shotCamera Cámara.
        /// </summary>
        public static void Partida()
        {
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            state = MapLab.Golden(name);
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                try
                {
                    Battlefield.Current.Show(state);
                    UnitViews.Current.Sync(state);
                    var squad = state.Units.Where(u => u.Alive && u.Team == "xcom").Select(u => MapSpace.TileToWorld(state, u.Pos)).ToList();
                    var middle = squad.Aggregate(Vector3.zero, (a, b) => a + b) / Mathf.Max(1, squad.Count);
                    MapSpace.Aim(Camera.main, middle, 24);
                    Debug.Log($"[combat] Partida: {name} shown, {state.Units.Count} units");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
        }

        /// <summary>
        /// La Lata at a mission's evacuation (mapView.ts placeBus, evacBus.ts): a recorded mission with its bay (BASTION_MAP,
        /// step2/city-11-scavenge unless said), as the campaign's vehicle BASTION_VEHICULO (0 the 4×4, 1 the van, 2 the lorry,
        /// 3 the bus, as unless said), the camera on its bay as the game puts it when it goes (16 tiles off; BASTION_DIST
        /// another). BASTION_IR=1, in play mode: it goes (MapView.LataDeparts) from the first frame of a strip.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Evac -shotOut lata.png
        ///   BASTION_IR=1 unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Evac -shotPlay -shotFrames 30 -shotFps 10 -shotOut tira
        /// </summary>
        public static void Evac()
        {
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "../step2/city-11-scavenge";
            state = MapLab.Golden(name);
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_VEHICULO"), out int vehicle)) state.Vehicle = vehicle;
            MapLab.Lighting(state.Mission?.Night != true);
            var view = MapLab.Map(state, name);
            var bay = state.Mission?.EvacBus?.Tiles;
            var zone = state.Mission?.EvacZone;
            var tiles = bay != null && bay.Count > 0 ? bay : zone;
            // game.ts busTile: the bay's tile nearest its middle (else the zone's).
            float mx = (float)tiles.Average(t => t.X), my = (float)tiles.Average(t => t.Y);
            var at = tiles.OrderBy(t => (t.X - mx) * (t.X - mx) + (t.Y - my) * (t.Y - my)).First();
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Shot camera") ?? new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            float distance = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 16;
            MapSpace.Aim(camera, MapSpace.TileToWorld(state, new Pos { X = at.X, Y = at.Y }), distance);
            MapLab.Lighting(state.Mission?.Night != true);
            Debug.Log($"[combat] {name}: La Lata ({view.Lata?.Vehicle}) at the bay's ({at.X},{at.Y}), {(view.Lata != null && view.Lata.Shown ? "waiting" : "gone")}");
            if (Environment.GetEnvironmentVariable("BASTION_IR") != "1") return;
            void Go(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                EditorApplication.playModeStateChanged -= Go;
                float started = Time.time;
                Wait();
                async void Wait()
                {
                    await MapView.Current.LataDeparts();
                    Debug.Log($"[combat] La Lata gone after {Time.time - started:0.00} s of game time");
                }
            }
            EditorApplication.playModeStateChanged += Go;
        }

        /// <summary>The effects side by side on a plain floor, close up, a moment after they start (-shotWarm 0.05).</summary>
        public static void Effects()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 3;
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.35f, 0.35f, 0.37f) };
            fx = new GameObject("Efectos").AddComponent<Fx>();
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
            MapSpace.Aim(camera, new Vector3(0, 1, 0), 7, 0);
            float x = -6;
            Vector3 Next() => new Vector3(x += 2, 1.5f, 0);
            fx.MuzzleFlash(Next(), Fx.Hex(0xffd27a));
            fx.Sparks(Next(), Fx.Hex(0xffc46b));
            fx.Slash(Next(), Fx.Hex(0xd8f0ff));
            fx.Bite(Next(), new Vector3(x - 1, 1.5f, 0));
            fx.Heal(Next());
            _ = fx.Explosion(new Vector3(x += 3, 0, 0), 1);
            // BASTION_NOLAMP=1: the effects' lights off, to tell what they light from what is drawn.
            if (Environment.GetEnvironmentVariable("BASTION_NOLAMP") == "1")
                foreach (var light in fx.GetComponentsInChildren<UnityEngine.Light>()) light.enabled = false;
        }

        /// <summary>The labs' enemies shown wherever they are put (out of the squad's sight they would hide).</summary>
        static void Reveal()
        {
            foreach (var u in state.Units) units.ForceVisible(u.Id);
        }

        static void Started(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Started;
            _ = (sequence ?? Play)();
        }

        static async Awaitable Play()
        {
            try
            {
                var bullet = Fx.Hex(0xffd27a);
                // The figures made in play mode, as the game makes them: a graph made before it is gone once it starts.
                units.Sync(state);
                Reveal();
                await Awaitable.WaitForSecondsAsync(1);
                Debug.Log($"[combat] {Time.time:0.00} s: walks");
                await units.MoveAlong(soldier.Id, path, 1);
                Debug.Log($"[combat] {Time.time:0.00} s: there, {units.Feet(soldier.Id)}");

                Debug.Log($"[combat] {Time.time:0.00} s: aims");
                int aim = await units.RaiseWeapon(soldier.Id, units.Chest(target.Id));
                for (int shot = 0; shot < 2; shot++)
                {
                    var from = units.Muzzle(soldier.Id);
                    var to = units.Chest(target.Id);
                    fx.MuzzleFlash(from, bullet);
                    units.Recoil(soldier.Id);
                    await fx.Bullet(from, to, bullet);
                    fx.Sparks(to, Fx.Hex(0xffc46b));
                    Debug.Log($"[combat] {Time.time:0.00} s: hit {shot + 1}");
                    await units.HitReact(target.Id, shot == 0 ? 1 : 1.5f);
                    if (shot == 0) await Awaitable.WaitForSecondsAsync(0.4f);
                }
                await units.Die(target.Id);
                await units.LowerWeapon(soldier.Id, aim);

                // A grenade a tile short of where the target lay.
                var feet = units.Feet(soldier.Id);
                var lay = units.BodyPos(target.Id);
                var blast = lay - (lay - feet).normalized * MapSpace.Tile;
                blast.y = lay.y;
                Debug.Log($"[combat] {Time.time:0.00} s: grenade");
                // The arc starts as the grenade leaves the hand, while the throw ends.
                Awaitable arc = null;
                await units.ThrowMotion(soldier.Id, () => arc = fx.GrenadeArc(units.ThrowHand(soldier.Id), blast));
                await arc;
                await fx.Explosion(blast, 2);
                Debug.Log($"[combat] {Time.time:0.00} s: done");
            }
            catch (Exception e)
            {
                Debug.LogError("[combat] " + e);
            }
        }
    }
}
