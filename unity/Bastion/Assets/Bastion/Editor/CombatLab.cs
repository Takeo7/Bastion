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
            if (Environment.GetEnvironmentVariable("BASTION_VIEW") == "game") MapSpace.Aim(camera, middle, off);
            else
            {
                camera.transform.position = middle + new Vector3(0, 1.3f, -6.5f);
                camera.transform.LookAt(middle + Vector3.up * 0.95f);
            }
            EditorApplication.playModeStateChanged += Started;
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
            if (fila == "1" || fila == "comunes")
            {
                // (comunes: the common dead's 17 bodies, in their order, each its id's pick: syntyManifest.ts UNIT_BODIES zombie.)
                var row = fila == "comunes"
                    ? new[] { "c2", "c6", "c11", "c15", "c29", "c1", "c5", "c9", "c16", "c0", "c4", "c8", "c17", "c3", "c7", "c10", "c14" }.Select(i => ("zombie", i)).ToArray()
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
        /// mean (real time a frame, in the editor on this machine).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Horda -shotPlay -shotCamera "Shot camera" -shotWarm 16 -shotFrames 1 -shotOut &lt;file&gt;
        /// </summary>
        public static void Horda()
        {
            var first = MapLab.Golden("city-7");
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            int n = int.TryParse(Environment.GetEnvironmentVariable("BASTION_N"), out int count) ? count : 40;
            bool plain = Environment.GetEnvironmentVariable("BASTION_SOLO_ZOMBIS") == "1";
            string[] mix = { "zombie", "zombie", "zombie", "xenoid", "mec", "bloater" };
            Environment.SetEnvironmentVariable("BASTION_VIEW", "game");
            Plain(u => u.Id == deadId, 8, 16);
            var like = state.Units[0];
            state.Units = Enumerable.Range(0, n).Select(k =>
            {
                var u = Bastion.Net.Json.Read<Unit>(Bastion.Net.Json.Write(like));
                u.Id = $"h{k}";
                u.Template = plain ? "zombie" : mix[k % mix.Length];
                return Put(u, 6 + k % 8 * 2, 4 + k / 8 * 2, Math.PI / 2);
            }).ToList();
            sequence = async () =>
            {
                units.Sync(state);
                Reveal();
                // Every other one walking two tiles and back, on and on.
                bool on = true;
                foreach (var u in state.Units.Where((u, k) => k % 2 == 1))
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
