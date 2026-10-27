using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bastion.Client;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The settlement's clearing for Shots, as 0.10.1's clearing lab (claro.html, lab/clearingLab.ts) has it: a fresh base
    /// (seed 3) with a clearing felled round its middle and the same pieces put in it, or a golden base (BASTION_BASE =
    /// a case of engine-cs/golden/step3/base.json, as base-70-siege), drawn as the map its siege would be fought on, with
    /// nobody on it (clearingArt.ts clearingState). Its camera as the lab's look(x, z, distance, yaw).
    ///   BASTION_BARE=1 none of the lab's pieces · BASTION_NIVEL=n the base's level · BASTION_DESGASTE=w the Bastion worn 0 to 3
    ///   BASTION_LATA=v La Lata as vehicle v (0 4×4, 1 van, 2 lorry, 3 the bus) · BASTION_IR=1 it goes, BASTION_VOLVER=1 it
    ///   comes back (a strip: -shotFrames) · BASTION_NIEVE=1 in winter · BASTION_VIENTO=k the trees' wind (MapBuilder.Wind) · BASTION_SEED=s the fresh base's seed · BASTION_BASE_FILE=base.json a base of one's own (as 0.10.1's __claro.base takes it)
    ///   BASTION_LOOK=x,z,distance[,yaw] the camera (the lab's look; yaw in radians, 0 from the south-east) · BASTION_CERCO=1
    ///   BASTION_SOMBRAS=m how far the shadows reach (90). To weigh the light against 0.10.1's: BASTION_GRADE=0 without the
    ///   grade · BASTION_SOL=0 without sun nor fill · with it BASTION_CIELO=0 without the sky's light, and BASTION_NIEBLA=1
    ///   the haze alone · BASTION_SOMBRAS_RES=texels,cascades,split the shadows' map
    ///   unity/tools/captura.sh -shotOut claro.png -shotSetup Bastion.EditorTools.SettlementLab.Clearing -shotSize 1600x900
    /// </summary>
    public static class SettlementLab
    {
        /// <summary>
        /// Each clip of the figures' catalog (those BASTION_IDS names, comma-separated; all if not said) and its seconds, to
        /// set against 0.10.1's (its GLBs: 60 fps files it read as 30 lasted twice as long there).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.SettlementLab.ClipLengths -quit
        /// </summary>
        public static void ClipLengths()
        {
            var catalog = Bastion.Client.Units.FigureCatalog.Load();
            var ids = Environment.GetEnvironmentVariable("BASTION_IDS")?.Split(',');
            for (int k = 0; k < catalog.clipIds.Length; k++)
                if (ids == null || ids.Contains(catalog.clipIds[k]))
                    Debug.Log($"[clips] {catalog.clipIds[k]} {catalog.clips[k].length:0.000} s at {catalog.clips[k].frameRate:0} fps");
        }

        /// <summary>
        /// Getting into La Lata measured on our bodies (clearingArt.ts ENTER_*, STEP_*): the hips and the hands from the
        /// figure's ground, in tiles of its frame (right, up, forward), through «Entering Car»'s part and the stairs' step
        /// (CAMP_SPANS), a woman's and a man's.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.SettlementLab.Into -quit
        /// </summary>
        public static void Into()
        {
            var catalog = Bastion.Client.Units.FigureCatalog.Load();
            foreach (string sex in new[] { "f", "m" })
            {
                var look = Appearances.DefaultAppearance(0, "#e8a33a", sex);
                var figure = Bastion.Client.Units.SoldierFigure.Make(Bastion.Client.Units.Dress.Of(look, "into" + sex, null), "into", true);
                figure.transform.localScale = Vector3.one * Bastion.Client.Units.Figures.CharacterScale;
                var animator = figure.GetComponentInChildren<Animator>();
                string build = sex == "f" ? "femn" : "masc";
                var anim = new Bastion.Client.Units.FigureAnimator(animator, m => catalog.Clip(Bastion.Client.Units.Anims.Motion(build, m, false)));
                string At(HumanBodyBones bone)
                {
                    var d = animator.GetBoneTransform(bone).position - figure.transform.position;
                    return $"({Vector3.Dot(d, figure.transform.right) / MapSpace.Tile:0.00} {d.y / MapSpace.Tile:0.00} {Vector3.Dot(d, figure.transform.forward) / MapSpace.Tile:0.00})";
                }
                void Report(string what) => Debug.Log($"[into] {build} {what}: hips {At(HumanBodyBones.Hips)} left hand {At(HumanBodyBones.LeftHand)} right hand {At(HumanBodyBones.RightHand)} head {At(HumanBodyBones.Head)}");
                anim.Step(0);
                Report("idle");
                // (In the clip's own seconds: 0.10.1 had them at half their pace, CAMP_SPANS' twice these.)
                foreach (var (id, from, to) in new[] { ("enterCar", 1.1f, 4.2f), ("enterCarR", 1.1f, 4.2f), ("stepUp", 0.15f, 0.95f) })
                {
                    var clip = catalog.Clip(id);
                    if (clip == null)
                    {
                        Debug.LogWarning($"[into] no clip {id}");
                        continue;
                    }
                    anim.PlayWholePart(clip, from, to, 0, false);
                    anim.Step(0);
                    float t = 0;
                    Report($"{id} {from:0.00} s");
                    foreach (float c in id == "stepUp" ? new[] { 0.4f, 0.8f, 1.2f, 1.6f } : new[] { 0.25f, 0.5f, 1f, 1.5f, 2f, 3f, 4f, 5f, 5.3f, 5.6f, 5.9f, 6.2f })
                    {
                        float at = c / 2;
                        while (t < at - 1e-4f)
                        {
                            float dt = Mathf.Min(0.025f, at - t);
                            anim.Step(dt);
                            t += dt;
                        }
                        Report($"{id} c {c:0.00}");
                    }
                }
                anim.Dispose();
                UnityEngine.Object.DestroyImmediate(figure);
            }
        }

        /// <summary>
        /// What building shows (Settlement.Build: clearing.ts ClearingSet's drawing) over the lab's clearing (Clearing's):
        /// the grid and the edge band, a workshop about to go down in green with its tiles marked and the cursor round it, a
        /// line of palisade in red, the radio picked, a proposal (a tower), the partner's piece in hand (a garden, in blue)
        /// and two siege posts, one held. From BASTION_LOOK (28, 30, 22 unless said).
        ///   unity/tools/captura.sh -shotOut construir.png -shotSetup Bastion.EditorTools.SettlementLab.Construir -shotSize 1600x900
        /// </summary>
        /// <summary>
        /// Walking about the clearing in first person (Settlement.WalkAbout, walkAbout.ts), with Vida's soldiers out: from
        /// where BASTION_LOOK's camera looks (28,30 unless said), on in play mode, the keys and the mouse played by
        /// BASTION_ANDAR, its steps «ahead,side,run,mouse x,mouse y,seconds» split by ';' (the mouse in pixels a frame, y
        /// up); unless said, it walks ahead, turns right, runs on into what stops it, looks up and down, and walks aslant.
        /// Where it stands every half second in the log ([andar]).
        ///   unity/tools/captura.sh -shotOut andar -shotSetup Bastion.EditorTools.SettlementLab.Andar -shotPlay -shotFrames 140 -shotFps 10
        /// </summary>
        public static void Andar()
        {
            if (Environment.GetEnvironmentVariable("BASTION_LOOK") == null) Environment.SetEnvironmentVariable("BASTION_LOOK", "28,30,22,0");
            Vida();
            string script = Environment.GetEnvironmentVariable("BASTION_ANDAR") ?? "1,0,0,0,0,3;0,0,0,20,0,1.5;1,0,1,0,0,3;0,0,0,0,10,1;0,0,0,0,-20,1;1,1,0,0,0,3";
            var steps = script.Split(';').Select(k => k.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToList();
            float started = -1, logged = -1;
            void Go(UnityEditor.PlayModeStateChange change)
            {
                if (change != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                UnityEditor.EditorApplication.playModeStateChanged -= Go;
                var s = Settlement.Current;
                s.Camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(k => k.name == "Shot camera");
                s.WalkAbout(true);
                started = Time.time;
                Debug.Log(FormattableString.Invariant($"[andar] on at {s.WalkAt.x:0.00},{s.WalkAt.y:0.00} facing {s.WalkYaw:0.00}: {s.Walking}"));
                Settlement.WalkFake = () =>
                {
                    float t = Time.time - started;
                    foreach (var k in steps)
                    {
                        if (t < k[5]) return (k[0], k[1], k[2] > 0, new Vector2(k[3], k[4]));
                        t -= k[5];
                    }
                    return (0, 0, false, Vector2.zero);
                };
                void Log()
                {
                    if (!UnityEditor.EditorApplication.isPlaying || Time.time < logged + 0.5f) return;
                    logged = Time.time;
                    Debug.Log(FormattableString.Invariant($"[andar] {Time.time - started:0.0} s: at {s.WalkAt.x:0.00},{s.WalkAt.y:0.00} facing {s.WalkYaw:0.00}"));
                }
                UnityEditor.EditorApplication.update += Log;
            }
            UnityEditor.EditorApplication.playModeStateChanged += Go;
        }

        /// <summary>
        /// claro.html's vida(n, distance, yaw) (clearingLab.ts): the lab's clearing (Clearing's base) with BASTION_N (6)
        /// soldiers of both players out about it, vida0…, as there (the same entries, so the same places and the same
        /// seeds), from look(c, c, distance, yaw) (BASTION_LOOK, 22 unless said). In play mode, the figures made once it
        /// runs (their clips' graphs don't outlive entering it); BASTION_VIDA=1 logs what they do every second. Its chances
        /// drawn from BASTION_SEMILLA (1 unless said): with Shots' fixed step, the same run every time (find a moment in a
        /// cheap run's log, then shoot it with -shotWarm).
        ///   unity/tools/captura.sh -shotOut vida -shotSetup Bastion.EditorTools.SettlementLab.Vida -shotPlay -shotFrames 100 -shotFps 10
        /// </summary>
        public static void Vida()
        {
            if (Environment.GetEnvironmentVariable("BASTION_LOOK") == null) Environment.SetEnvironmentVariable("BASTION_LOOK", "28,28,22,0");
            Clearing();
            int n = int.TryParse(Environment.GetEnvironmentVariable("BASTION_N"), out int k) ? k : 6;
            string[] templates = { "assault", "sharpshooter", "specialist", "grenadier", "rookie" };
            var entries = Enumerable.Range(0, n).Select(i =>
            {
                string colour = i % 2 != 0 ? "#4fb0e8" : "#e8a33a", sex = i % 3 != 0 ? "m" : "f";
                return new LifeEntry { Template = templates[i % templates.Length], Colour = colour, Appearance = Appearances.DefaultAppearance(i % 2, colour, sex), Seed = $"vida{i}" };
            }).ToList();
            // BASTION_PISTOLA=k[,…]: those out with only the pistol (from the Armería with no gun of their own).
            foreach (var p in (Environment.GetEnvironmentVariable("BASTION_PISTOLA") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(p, out int at) && at >= 0 && at < entries.Count) entries[at].Gear = new LifeGear { Weapon = 1, Arms = new Bastion.Engine.Arms() };
            void Go(UnityEditor.PlayModeStateChange change)
            {
                if (change != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                UnityEditor.EditorApplication.playModeStateChanged -= Go;
                UnityEngine.Random.InitState(int.TryParse(Environment.GetEnvironmentVariable("BASTION_SEMILLA"), out int seed) ? seed : 1);
                Settlement.Current?.SetLife(entries);
                // BASTION_CHARLA=1: without the fires and La Lata's sides, so that two talk; 2, La Lata's sides kept; 3, those
                // and no chairs (the one walking leans on La Lata).
                string talk = Environment.GetEnvironmentVariable("BASTION_CHARLA");
                if (talk == "1" || talk == "2" || talk == "3") Settlement.Current?.LifeBareCamp(talk != "1", talk != "3");
                // BASTION_APARTAR=x0,z0,x1,z1[;…][@seconds]: those tiles kept clear (as a building's ghost keeps them), from the
                // start or from `seconds` on (who is sat, knelt or leant there gets up and goes).
                if (Environment.GetEnvironmentVariable("BASTION_APARTAR") is string apart)
                {
                    var at = apart.Split('@');
                    float from = at.Length > 1 ? float.Parse(at[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
                    var cells = new System.Collections.Generic.List<Vector2Int>();
                    foreach (var box in at[0].Split(';'))
                    {
                        var r = box.Split(',').Select(int.Parse).ToArray();
                        for (int x = r[0]; x <= r[2]; x++)
                            for (int z = r[1]; z <= r[3]; z++) cells.Add(new Vector2Int(x, z));
                    }
                    bool done = false;
                    void Apart()
                    {
                        if (done || !UnityEditor.EditorApplication.isPlaying || Time.time < from) return;
                        done = true;
                        Debug.Log($"[vida] {Time.time:0.0} s: {cells.Count} tiles kept clear");
                        Settlement.Current?.LifeKeepClear(cells);
                    }
                    if (from <= 0) Apart();
                    else UnityEditor.EditorApplication.update += Apart;
                }
                Debug.Log($"[vida] {Settlement.Current?.LifeCount} out: {Settlement.Current?.LifeReport()}");
            }
            UnityEditor.EditorApplication.playModeStateChanged += Go;
            if (Environment.GetEnvironmentVariable("BASTION_VIDA") == "1")
            {
                int frames = 0;
                void Report()
                {
                    if (!UnityEditor.EditorApplication.isPlaying) return;
                    if (++frames % 10 == 0 && Settlement.Current != null) Debug.Log($"[vida] {Time.time:0.0} s: {Settlement.Current.LifeReport()}");
                }
                UnityEditor.EditorApplication.update += Report;
            }
            // BASTION_FRENTE=seed[,metres[,degrees[,up[,still]]]]: the shot camera before that one (turned `degrees` to its left,
            // `metres` of its size away), a little above its head (or `up` of its size) and looking down at its chest, following
            // it (half its body, as 0.10.1's close looks at the camp: notas 22/10 07-beber; up 4 and metres 0.05, from above);
            // from `still` seconds on, left where it stood then (one sat, getting up and going: the camera doesn't turn with it).
            // BASTION_FRENTE_ADELANTE=m: looking at m metres ahead of it instead (between two talking).
            if (Environment.GetEnvironmentVariable("BASTION_FRENTE") is string front)
            {
                var f = front.Split(',');
                float metres = f.Length > 1 ? float.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture) : 1.8f;
                float degrees = f.Length > 2 ? float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture) : 25;
                float up = f.Length > 3 ? float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture) : 1.95f;
                float still = f.Length > 4 ? float.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture) : -1;
                bool placed = false;
                var shot = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(k => k.name == "Shot camera");
                int seen = -1;
                float tall;
                void Follow()
                {
                    if (!UnityEditor.EditorApplication.isPlaying || Time.frameCount == seen || shot == null) return;
                    seen = Time.frameCount;
                    var who = Settlement.Current?.LifeFigure(f[0]);
                    if (who == null) return;
                    shot.fieldOfView = 35;
                    if (still >= 0 && Time.time > still && placed) return;
                    placed = true;
                    // Its size: the figures' scale (sat, its body's height was half its own).
                    tall = Bastion.Client.Units.Figures.CharacterScale;
                    shot.transform.position = who.position + Quaternion.AngleAxis(-degrees, Vector3.up) * who.forward * metres * tall + Vector3.up * up * tall;
                    float ahead = float.TryParse(Environment.GetEnvironmentVariable("BASTION_FRENTE_ADELANTE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float m) ? m : 0;
                    shot.transform.LookAt(who.position + who.forward * ahead + Vector3.up * 1.3f * tall);
                }
                UnityEditor.EditorApplication.update += Follow;
            }
            // BASTION_JUEGO=seed[,tiles]: the shot camera as the game's (CameraRig: 52° down, turned 45°, 9 tiles off unless
            // said, its nearest), following that one.
            if (Environment.GetEnvironmentVariable("BASTION_JUEGO") is string game)
            {
                var g = game.Split(',');
                float tiles = g.Length > 1 ? float.Parse(g[1], System.Globalization.CultureInfo.InvariantCulture) : 9;
                var shot = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(k => k.name == "Shot camera");
                int seen = -1;
                void Follow()
                {
                    if (!UnityEditor.EditorApplication.isPlaying || Time.frameCount == seen || shot == null) return;
                    seen = Time.frameCount;
                    var who = Settlement.Current?.LifeFigure(g[0]);
                    if (who == null) return;
                    float pitch = 52 * Mathf.Deg2Rad, yaw = 45 * Mathf.Deg2Rad, back = Mathf.Cos(pitch) * tiles * MapSpace.Tile;
                    var target = who.position + Vector3.up * 0.9f;
                    shot.fieldOfView = 40;
                    shot.transform.position = target + new Vector3(Mathf.Sin(yaw) * back, Mathf.Sin(pitch) * tiles * MapSpace.Tile, -Mathf.Cos(yaw) * back);
                    shot.transform.LookAt(target);
                }
                UnityEditor.EditorApplication.update += Follow;
            }
            // BASTION_APOYO=seed: where that one's right shoulder and arm are against La Lata's sides (its drawn box, plates and
            // all), every second (the one leant on it: Gameplay 26/10).
            if (Environment.GetEnvironmentVariable("BASTION_APOYO") is string leaner)
            {
                int last = -1;
                void Measure()
                {
                    if (!UnityEditor.EditorApplication.isPlaying || Settlement.Current?.Lata == null) return;
                    int second = Mathf.FloorToInt(Time.time);
                    if (second == last) return;
                    last = second;
                    var who = Settlement.Current.LifeFigure(leaner);
                    var animator = who != null ? who.GetComponentInChildren<Animator>() : null;
                    if (animator == null) return;
                    var drawn = Settlement.Current.Lata.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    string Bone(HumanBodyBones b)
                    {
                        var t = animator.GetBoneTransform(b);
                        return t == null ? "-" : FormattableString.Invariant($"{t.position.x:0.000},{t.position.y:0.00} (to the sides {t.position.x - drawn.min.x:0.000} / {drawn.max.x - t.position.x:0.000})");
                    }
                    Debug.Log(FormattableString.Invariant($"[apoyo] {Time.time:0.0} s: La Lata x {drawn.min.x:0.000} to {drawn.max.x:0.000} (centre {drawn.center.x:0.000}); root {who.position.x:0.000}; right shoulder {Bone(HumanBodyBones.RightShoulder)}, upper arm {Bone(HumanBodyBones.RightUpperArm)}, elbow {Bone(HumanBodyBones.RightLowerArm)}; left upper arm {Bone(HumanBodyBones.LeftUpperArm)}; scale {Bastion.Client.Units.Figures.CharacterScale:0.###}"));
                }
                UnityEditor.EditorApplication.update += Measure;
            }
            // BASTION_PESOS=seed[,from,to]: that one's animator weights every frame from `from` to `to` seconds (a test).
            if (Environment.GetEnvironmentVariable("BASTION_PESOS") is string weighed)
            {
                var w = weighed.Split(',');
                float from = w.Length > 1 ? float.Parse(w[1], System.Globalization.CultureInfo.InvariantCulture) : 0, to = w.Length > 2 ? float.Parse(w[2], System.Globalization.CultureInfo.InvariantCulture) : 1e9f;
                int last = -1;
                void Weigh()
                {
                    if (!UnityEditor.EditorApplication.isPlaying || Time.frameCount == last) return;
                    last = Time.frameCount;
                    if (Time.time >= from && Time.time <= to && Settlement.Current != null) Debug.Log($"[pesos] {Time.time:0.00} {Settlement.Current.LifeWeights(w[0])}");
                }
                UnityEditor.EditorApplication.update += Weigh;
            }
        }

        public static void Construir()
        {
            if (Environment.GetEnvironmentVariable("BASTION_LOOK") == null) Environment.SetEnvironmentVariable("BASTION_LOOK", "28,30,22,0");
            Clearing();
            var s = Settlement.Current;
            int c = s.Base.Width / 2;
            s.SetBuilding(true);
            var at = new Vector2Int(c + 1, c + 4);
            s.Ghost(PieceIds.Workshop, 0, at, true);
            var cells = Bastion.Engine.Base.PieceCells(PieceIds.Workshop, at.x, at.y, 0);
            s.Marks(cells.Select(k => (new Vector2Int(k.X, k.Y), Settlement.Mark.Ok)).Concat(new[] { (new Vector2Int(c - 3, c + 8), Settlement.Mark.Amber), (new Vector2Int(c - 2, c + 8), Settlement.Mark.Amber) }).ToList());
            int x0 = cells.Min(k => k.X), y0 = cells.Min(k => k.Y);
            s.Outline(new RectInt(x0, y0, cells.Max(k => k.X) - x0 + 1, cells.Max(k => k.Y) - y0 + 1));
            s.GhostLine(PieceIds.Palisade, 0, Enumerable.Range(0, 5).Select(k => new Vector2Int(c - 8 + k, c + 6)).ToList(), false);
            s.Select(s.Base.Pieces.FirstOrDefault(p => p.Piece == PieceIds.Radio)?.Id);
            s.SetProposal(new Settlement.PieceCursor { piece = PieceIds.Tower, x = c + 6, y = c + 6, rot = 0 });
            s.PartnerCursor(new Settlement.PieceCursor { piece = PieceIds.Garden, x = c - 4, y = c + 9, rot = 0 }, new Color(0x4f / 255f, 0xb0 / 255f, 0xe8 / 255f));
            s.SetPosts(new[]
            {
                new Settlement.SiegePost { id = "p1", name = "Uno", colour = new Color(0xe8 / 255f, 0xa3 / 255f, 0x3a / 255f), x = c - 6, y = c + 3, mine = true },
                new Settlement.SiegePost { id = "p2", name = "Dos", colour = new Color(0x4f / 255f, 0xb0 / 255f, 0xe8 / 255f), x = c - 5, y = c + 3, mine = false },
            }, "p1");
            Debug.Log($"[construir] the partner's name at {s.PartnerTagWorld}, post p1's at {s.PostWorld("p1")}; tile under the screen's middle {s.TileAt(new Vector2(800, 450))}");
            // BASTION_TAPAR=1: a workshop built where La Lata stands (UI, 26/10), and the clearing shown again: it parks elsewhere (parkBus).
            if (Environment.GetEnvironmentVariable("BASTION_TAPAR") == "1" && s.Lata != null)
            {
                var stood = s.Lata.transform.localPosition / MapSpace.Tile;
                int tx = Mathf.FloorToInt(stood.x), ty = Mathf.FloorToInt(-stood.z);
                var @base = Newtonsoft.Json.JsonConvert.DeserializeObject<BaseState>(Newtonsoft.Json.JsonConvert.SerializeObject(s.Base));
                var put = Bastion.Engine.Base.PlacePiece(@base, PieceIds.Workshop, tx - 1, ty - 1, 1);
                s.Ghost(null, 0, default, true);
                s.Show(@base);
                var now = s.Lata.transform.localPosition / MapSpace.Tile;
                Debug.Log($"[construir] La Lata stood on tile {tx},{ty}; a workshop put at {put.X},{put.Y} turned {put.Rot}; now it stands at {now.x:0.##},{-now.z:0.##} (shown {s.Lata.gameObject.activeInHierarchy})");
            }
        }

        public static void Clearing()
        {
            var @base = Base();
            bool snow = Environment.GetEnvironmentVariable("BASTION_NIEVE") == "1";
            // BASTION_VIENTO=k: the trees' wind, k times Nature's own (MapBuilder.Wind; 0 still).
            if (float.TryParse(Environment.GetEnvironmentVariable("BASTION_VIENTO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float wind)) MapBuilder.Wind = wind;
            // The settlement's own (Settlement: the clearing, its light, La Lata as BASTION_LATA says: 0 4×4, 1 van, 2 lorry, 3 the bus).
            var settlement = new GameObject("Asentamiento").AddComponent<Settlement>();
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_LATA"), out int vehicle)) settlement.SetVehicle(vehicle);
            // The sun's shadows over the whole clearing (0.10.1's shadow camera over all of it), as far as the view reaches.
            settlement.ShadowMetres = float.TryParse(Environment.GetEnvironmentVariable("BASTION_SOMBRAS"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float reach) ? reach : 90;
            settlement.Show(@base, snow, MapLab.Prefabs(), MapLab.Materials());
            // BASTION_IR=1: La Lata going, BASTION_VOLVER=1 coming back, from the first frame of a strip.
            if (Environment.GetEnvironmentVariable("BASTION_IR") == "1") settlement.Depart();
            if (Environment.GetEnvironmentVariable("BASTION_VOLVER") == "1")
            {
                settlement.Depart();
                settlement.Lata.ShotStep(10);
                settlement.Arrive();
            }
            int c = @base.Width / 2;
            var look = Environment.GetEnvironmentVariable("BASTION_LOOK")?.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Look(look != null && look.Length >= 3 ? look[0] : c, look != null && look.Length >= 3 ? look[1] : c, look != null && look.Length >= 3 ? look[2] : 34, look != null && look.Length >= 4 ? look[3] : 0);
            // The light again for the camera put (fixed opposite the sun, as the settlement has it; BASTION_RELLENO=camara,
            // with the camera, as before E5).
            Mood.Show(snow ? Mood.SnowDay : Mood.Day).FillOpposite = Environment.GetEnvironmentVariable("BASTION_RELLENO") != "camara";
            // (BASTION_RELLENO=no: without the fill, to weigh what it gives.)
            if (Environment.GetEnvironmentVariable("BASTION_RELLENO") == "no")
                foreach (var l in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(FindObjectsSortMode.None)) if (l.name == "Relleno") l.enabled = false;
            // BASTION_SOMBRAS_RES=texels,cascades,split: to weigh the shadows' sharpness (a test).
            var res = Environment.GetEnvironmentVariable("BASTION_SOMBRAS_RES")?.Split(',');
            if (res != null && (UnityEngine.QualitySettings.renderPipeline ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline) is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.mainLightShadowmapResolution = int.Parse(res[0]);
                urp.shadowCascadeCount = int.Parse(res[1]);
                urp.cascade2Split = float.Parse(res[2], System.Globalization.CultureInfo.InvariantCulture);
                Debug.Log($"[claro] shadows {urp.mainLightShadowmapResolution} texels, {urp.shadowCascadeCount} cascades, split {urp.cascade2Split}, {urp.shadowDistance} m");
            }
            // BASTION_SOL=0: no sun nor fill, the sky's light alone (a test, to weigh it against 0.10.1's).
            if (Environment.GetEnvironmentVariable("BASTION_SOL") == "0")
            {
                var alone = new Mood.Values();
                foreach (var f in typeof(Mood.Values).GetFields()) f.SetValue(alone, f.GetValue(Mood.Day));
                alone.sunIntensity = 0;
                alone.fillIntensity = 0;
                Mood.Show(alone);
                // BASTION_CIELO=0 as well: nor the sky's (what is left is what else lights the ground).
                if (Environment.GetEnvironmentVariable("BASTION_CIELO") == "0")
                {
                    RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
                    RenderSettings.fog = Environment.GetEnvironmentVariable("BASTION_NIEBLA") == "1";
                }
                Debug.Log($"[claro] ambient {RenderSettings.ambientMode} sky {RenderSettings.ambientSkyColor} equator {RenderSettings.ambientEquatorColor} ground {RenderSettings.ambientGroundColor}; probe up {Up(RenderSettings.ambientProbe)} side {Up(RenderSettings.ambientProbe, Vector3.right)} {Up(RenderSettings.ambientProbe, Vector3.forward)} down {Up(RenderSettings.ambientProbe, Vector3.down)}; reflections {RenderSettings.reflectionIntensity} {RenderSettings.defaultReflectionMode}; skybox {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}");
            }
            // BASTION_GRADE=0: the picture as lit, without the grade (to weigh the light against 0.10.1's before its grade).
            if (Environment.GetEnvironmentVariable("BASTION_GRADE") == "0") Shader.SetGlobalFloat("_GradeOn", 0);
        }

        /// <summary>
        /// What the clearing costs to draw (Gameplay 24/10: the pinar past the edge, thousands of models): at each quality
        /// level (BASTION_CALIDAD=Medio,Alto; all three if not said) the lab's clearing (its env as Clearing's) drawn n times
        /// (BASTION_FRAMES, 120) at 1920×1080, each frame waited for (a pixel read back), with and without the pinar past
        /// the edge, in ms a frame; and how many renderers and triangles the pinar is. In the editor, on this machine's GPU.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.SettlementLab.Cost -quit
        /// </summary>
        public static void Cost()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Clearing();
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(k => k.name == "Shot camera");
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080;
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            int frames = int.TryParse(Environment.GetEnvironmentVariable("BASTION_FRAMES"), out int f) ? f : 120;
            double Frames()
            {
                void One()
                {
                    camera.Render();
                    RenderTexture.active = target;
                    probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    probe.Apply(false);
                    RenderTexture.active = null;
                }
                for (int k = 0; k < 10; k++) One();
                var watch = Stopwatch.StartNew();
                for (int k = 0; k < frames; k++) One();
                return watch.Elapsed.TotalMilliseconds / frames;
            }
            var wood = GameObject.Find("Bosque");
            var renderers = wood != null ? wood.GetComponentsInChildren<MeshRenderer>() : Array.Empty<MeshRenderer>();
            long triangles = renderers.Sum(r => { var m = r.GetComponent<MeshFilter>()?.sharedMesh; return m == null ? 0L : Enumerable.Range(0, m.subMeshCount).Sum(k => (long)m.GetIndexCount(k)) / 3; });
            int all = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;
            Debug.Log($"[cost] the pinar past the edge: {renderers.Length} renderers, {triangles} triangles; the whole scene {all} renderers");
            string asked = Environment.GetEnvironmentVariable("BASTION_CALIDAD");
            var levels = asked != null ? asked.Split(',') : QualitySettings.names;
            int was = QualitySettings.GetQualityLevel();
            foreach (string level in levels)
            {
                int index = Array.IndexOf(QualitySettings.names, level);
                if (index < 0)
                {
                    Debug.LogWarning($"[cost] no quality level {level}");
                    continue;
                }
                QualitySettings.SetQualityLevel(index, true);
                // The shadows over the whole clearing, on this level's pipeline too.
                Mood.ShadowReach(90);
                if (wood != null) wood.SetActive(true);
                double with = Frames();
                if (wood != null) wood.SetActive(false);
                double without = Frames();
                Debug.Log($"[cost] {level}: {with:0.00} ms a frame with the pinar past the edge, {without:0.00} without ({frames} frames, 1920×1080)");
            }
            if (wood != null) wood.SetActive(true);
            // The project as it was: its level and its pipelines' shadows (the editor keeps the level it is left at).
            Mood.ShadowReach(null);
            QualitySettings.SetQualityLevel(was, true);
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>
        /// Each prop's model box as it comes (BASTION_IDS, MapArt ids, comma-separated, or `all`), in its own frame, in metres: to
        /// tell how Unity's model stands from 0.10.1's GLB of it (a quarter or half turn, Unity's mirror against three's).
        ///   BASTION_IDS=plywoodShed,bastionHouse unity/tools/unity.sh -executeMethod Bastion.EditorTools.SettlementLab.Boxes -quit
        /// </summary>
        public static void Boxes()
        {
            var prefabs = MapLab.Prefabs();
            string asked = Environment.GetEnvironmentVariable("BASTION_IDS") ?? "";
            foreach (string id in asked == "all" ? MapArt.Ids().ToArray() : asked.Split(','))
            {
                var art = MapArt.Prop(id);
                var prefab = art != null && !string.IsNullOrEmpty(art.fbx) ? prefabs(MapArt.ModelKey(art)) : null;
                if (prefab == null)
                {
                    Debug.Log($"[box] {id}: no model");
                    continue;
                }
                var o = (GameObject)UnityEngine.Object.Instantiate(prefab);
                o.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var renderers = o.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    Debug.Log($"[box] {id}: no renderer");
                    UnityEngine.Object.DestroyImmediate(o);
                    continue;
                }
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                string F(Vector3 v) => FormattableString.Invariant($"[{v.x:0.000}, {v.y:0.000}, {v.z:0.000}]");
                Debug.Log($"[box] {id} {UnityEditor.AssetDatabase.GetAssetPath(prefab)} min {F(b.min)} max {F(b.max)} centre {F(b.center)}");
                UnityEngine.Object.DestroyImmediate(o);
            }
        }

        /// <summary>
        /// Many views of one clearing in one run, each as Clearing's camera (the lab's look): BASTION_VIEWS a JSON file of
        /// [{name, x, z, d, yaw}], BASTION_OUT the folder their PNGs go to (name.png), 1600×900 unless BASTION_SIZE=w×h;
        /// the clearing as Clearing's env says (BASTION_BASE_FILE a base of one's own, as 0.10.1's __claro.base takes it).
        ///   BASTION_BASE_FILE=n1.json BASTION_VIEWS=views.json BASTION_OUT=out unity/tools/unity.sh -executeMethod Bastion.EditorTools.SettlementLab.Views -quit
        /// </summary>
        public static void Views()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Clearing();
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(k => k.name == "Shot camera");
            var size = (Environment.GetEnvironmentVariable("BASTION_SIZE") ?? "1600x900").Split('x').Select(int.Parse).ToArray();
            camera.aspect = (float)size[0] / size[1];
            string folder = Environment.GetEnvironmentVariable("BASTION_OUT") ?? throw new ArgumentException("BASTION_OUT is missing");
            Directory.CreateDirectory(folder);
            var views = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("BASTION_VIEWS") ?? throw new ArgumentException("BASTION_VIEWS is missing")));
            // A frame thrown away first, as Shots does (the first one a run draws has no ambient light nor reflections yet).
            Shots.Render(camera, size[0], size[1], null);
            foreach (var v in views)
            {
                Look((float)v["x"], (float)v["z"], (float)v["d"], (float)v["yaw"]);
                Shots.Render(camera, size[0], size[1], Path.Combine(folder, (string)v["name"] + ".png"));
            }
            Debug.Log($"[claro] {views.Count} views in {Path.GetFullPath(folder)}");
        }

        /// <summary>The lab's base (clearingLab.ts), a golden one, or one from a file.</summary>
        /// <summary>
        /// Soldados over the clearing of UI's sample campaign (.dev-snapshots/ui/estrategia-0101/partida-muestra.json, the one
        /// 0.10.1's references were taken with; BASTION_PARTIDA another): the settlement as the game shows it
        /// (Settlement.Show(campaign)), BASTION_SOLDADO's soldier (c1 unless said) as Showcase has them, in BASTION_ROPA's
        /// outfit if given (Personalizar's draft; BASTION_CAIDO=1 fallen), the camera on BASTION_VISTA's tab (barracks unless said), the Armería's corner with the
        /// campaign's stock, and the squad's row (BASTION_FILA) or the debrief's, framed in 0.10.1's panels.
        ///   unity/tools/captura.sh -shotOut soldados.png -shotSetup Bastion.EditorTools.SettlementLab.Soldados -shotSize 1440x900
        /// </summary>
        public static void Soldados()
        {
            // (BASTION_SIN_LUZ=1: without the soldiers' own key and sky, Settlement.Key.)
            Settlement.FaceKeyOff = Environment.GetEnvironmentVariable("BASTION_SIN_LUZ") == "1";
            if (Environment.GetEnvironmentVariable("BASTION_TINTE") == "1") Bastion.Client.Units.SquadTint.On = true;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string file = Environment.GetEnvironmentVariable("BASTION_PARTIDA") ?? Path.Combine(root, ".dev-snapshots/ui/estrategia-0101/partida-muestra.json");
            var campaign = SampleCampaign(file);
            // Their clothes as the game dresses them (Settlement.Show(campaign) and Portraits.Know(campaign) do it).
            Bastion.Client.Units.Dress.Roster(campaign.Soldiers);
            var settlement = new GameObject("Asentamiento").AddComponent<Settlement>();
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(k => k.name == "Shot camera") ?? new GameObject("Shot camera").AddComponent<Camera>();
            settlement.Camera = camera;
            settlement.Show(campaign.Bastion, Campaign.Wintry(campaign), MapLab.Prefabs(), MapLab.Materials());
            settlement.SetVehicle(Campaign.VehicleOf(campaign));
            string id = Environment.GetEnvironmentVariable("BASTION_SOLDADO") ?? "c1";
            var look = campaign.Soldiers.FirstOrDefault(s => s.Id == id)?.Appearance;
            string outfit = Environment.GetEnvironmentVariable("BASTION_ROPA");
            if (outfit != null && look != null)
            {
                look = Newtonsoft.Json.JsonConvert.DeserializeObject<Appearance>(Newtonsoft.Json.JsonConvert.SerializeObject(look));
                look.Character = outfit;
            }
            // BASTION_CAIDO=1: as if they had fallen (their grave).
            if (Environment.GetEnvironmentVariable("BASTION_CAIDO") == "1") settlement.Showcase(id, look, null, true);
            else settlement.Showcase(campaign, id, look);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            settlement.SetArmory(campaign);
            double armory = clock.Elapsed.TotalMilliseconds;
            string tab = Environment.GetEnvironmentVariable("BASTION_VISTA") ?? Settlement.Views.Barracks;
            // The row: BASTION_FILA's ids ("-" an empty place; the Escuadra of 0.10.1's reference unless said: Arturo's
            // three and a free place, seen from his side, mirrored), the debrief's from the campaign's last report.
            var colours = Content.PlayerColors;
            // (BASTION_N: the report's first n, to try other rows.)
            if (campaign.LastReport != null && int.TryParse(Environment.GetEnvironmentVariable("BASTION_N"), out int first))
                campaign.LastReport.Squad = campaign.LastReport.Squad.Take(first).ToList();
            if (tab == Settlement.Views.Debrief && campaign.LastReport != null)
                settlement.SetLineup(campaign, campaign.LastReport.Squad.Select(e => new Settlement.LineupEntry { SoldierId = e.Id, Colour = colours[e.Owner % colours.Count], Status = e.Status == "lost" ? "dead" : e.Status }).ToList(), true, true);
            else if (tab == Settlement.Views.Squad || Environment.GetEnvironmentVariable("BASTION_FILA") != null)
            {
                var ids = (Environment.GetEnvironmentVariable("BASTION_FILA") ?? "-,c8,c2,c4").Split(',');
                settlement.SetLineup(campaign, ids.Select(i => i == "-" ? null : new Settlement.LineupEntry { SoldierId = i, Colour = colours[(campaign.Soldiers.FirstOrDefault(s => s.Id == i)?.Owner ?? 0) % colours.Count] }).ToList(), true, true);
            }
            // The panels as 0.10.1's screens had them at 1440 × 900 (stage.ts PANELS: the strip, the tabs, the sheets,
            // the squad's bottom), for the shots that fit in what they leave.
            var size = new Vector2(1440, 900);
            Rect R(float x0, float y0, float x1, float y1) => new Rect(x0 / size.x, y0 / size.y, (x1 - x0) / size.x, (y1 - y0) / size.y);
            var panels = new System.Collections.Generic.List<Rect> { R(0, 0, 1440, 46), R(435, 850, 1005, 898) };
            if (tab == Settlement.Views.Engineering) panels.AddRange(new[] { R(10, 55, 345, 335), R(1090, 60, 1428, 265) });
            if (tab == Settlement.Views.Squad) panels.AddRange(new[] { R(10, 55, 238, 235), R(1200, 55, 1428, 230), R(250, 732, 1190, 885) });
            // (The debrief's panel from 65 % of the height, as UI's: 25/10.)
            if (tab == Settlement.Views.Debrief) panels.AddRange(new[] { R(245, 585, 1195, 885) });
            double row = clock.Elapsed.TotalMilliseconds - armory;
            // Again, its figures made already (as in the game, where the busts made them): what the row itself costs.
            if (Environment.GetEnvironmentVariable("BASTION_FILA_OTRA") == "1" && tab == Settlement.Views.Debrief && campaign.LastReport != null)
            {
                var again = System.Diagnostics.Stopwatch.StartNew();
                settlement.SetLineup(campaign, campaign.LastReport.Squad.Select(e => new Settlement.LineupEntry { SoldierId = e.Id, Colour = colours[e.Owner % colours.Count], Status = e.Status == "lost" ? "dead" : e.Status }).ToList(), false, true);
                double second = again.Elapsed.TotalMilliseconds;
                again.Restart();
                settlement.SetLineup(campaign, campaign.LastReport.Squad.Select(e => new Settlement.LineupEntry { SoldierId = e.Id, Colour = colours[e.Owner % colours.Count], Status = e.Status == "lost" ? "dead" : e.Status }).ToList(), true, true);
                Debug.Log(System.FormattableString.Invariant($"[settlement] the row again, mirrored the other way: {second:0.0} ms; and back: {again.Elapsed.TotalMilliseconds:0.0} ms"));
            }
            settlement.ScreenSize = size;
            // BASTION_CADERA=1: how its arms stand (the upper arm from hanging, the hand out from the hips and ahead of them, in
            // shoulder widths), as 0.10.1's was measured (scratchpad pist/start-cadera.js).
            if (Environment.GetEnvironmentVariable("BASTION_CADERA") == "1")
                foreach (var a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x.isHuman && x.GetBoneTransform(HumanBodyBones.Hips) != null))
                {
                    Vector3 P(HumanBodyBones b) => a.GetBoneTransform(b).position;
                    var across = P(HumanBodyBones.LeftUpperArm) - P(HumanBodyBones.RightUpperArm);
                    across.y = 0;
                    float width = across.magnitude;
                    across.Normalize();
                    var ahead = a.transform.forward;
                    ahead.y = 0;
                    ahead.Normalize();
                    string Arm(string side, HumanBodyBones s, HumanBodyBones e, HumanBodyBones h, float sign)
                    {
                        var up = (P(e) - P(s)).normalized;
                        float open = Mathf.Acos(Mathf.Clamp(-up.y, -1, 1)) * Mathf.Rad2Deg;
                        var rel = P(h) - P(HumanBodyBones.Hips);
                        return System.FormattableString.Invariant($"{side}: upper arm {open:0.0}° from hanging, hand {Vector3.Dot(rel, across) * sign / width:0.00} out and {Vector3.Dot(rel, ahead) / width:0.00} ahead of the hips");
                    }
                    Debug.Log($"[hip] {a.name}: {Arm("L", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 1)}; {Arm("R", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -1)}");
                }
            camera.aspect = size.x / size.y;
            settlement.SetInsets(panels);
            clock.Restart();
            settlement.SetView(tab, true);
            Debug.Log(System.FormattableString.Invariant($"[settlement] the Armería filled in {armory:0.0} ms, the row in {row:0.0} ms, the {tab} shot in {clock.Elapsed.TotalMilliseconds:0.0} ms"));
            // The fill turned with the camera.
            Mood.Show(settlement.Snow ? Mood.SnowDay : Mood.Day);
            Debug.Log($"[settlement] Soldados: {id} on day {campaign.Day}, {(Campaign.Wintry(campaign) ? "winter" : "thaw")}, the camera on {settlement.View}");
        }

        /// <summary>
        /// The scene «Base» as the game has it, in play mode: once it runs, the sample campaign shown as UI shows it
        /// (Settlement.Instance.Show, the game's catalog), BASTION_SOLDADO's soldier on Soldados (c1) and its busts drawn
        /// (Portraits.Know; each logged as it is ready), the camera on BASTION_VISTA's tab (barracks), and on
        /// to BASTION_LUEGO's if given; BASTION_MAPA=1 into the Mapa and back, through black.
        ///   unity/tools/captura.sh -shotOut base.png -shotScene Assets/Bastion/Scenes/Base.unity -shotSetup Bastion.EditorTools.SettlementLab.BaseScene -shotPlay -shotWarm 3 -shotSize 1440x900
        /// </summary>
        public static void BaseScene()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var campaign = SampleCampaign(Environment.GetEnvironmentVariable("BASTION_PARTIDA") ?? Path.Combine(root, ".dev-snapshots/ui/estrategia-0101/partida-muestra.json"));
            void Go(UnityEditor.PlayModeStateChange change)
            {
                if (change != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                UnityEditor.EditorApplication.playModeStateChanged -= Go;
                var settlement = Settlement.Instance;
                if (settlement == null)
                {
                    Debug.LogError("[settlement] no Settlement.Instance in the scene");
                    return;
                }
                Portraits.Changed += id => Debug.Log($"[portraits] {id} drawn at frame {Time.frameCount} in {Portraits.LastDrawMs:0.0} ms");
                Portraits.Know(campaign);
                settlement.Show(campaign);
                settlement.Showcase(campaign, Environment.GetEnvironmentVariable("BASTION_SOLDADO") ?? "c1");
                settlement.SetView(Environment.GetEnvironmentVariable("BASTION_VISTA") ?? Settlement.Views.Barracks);
                // BASTION_FILA=id,…: those by La Lata (as the Escuadra's row stays up on every tab); 2 s later, who is in the
                // row and who about the camp (none in both: clearingArt.ts relife).
                if (Environment.GetEnvironmentVariable("BASTION_FILA") is string row)
                {
                    var colours = new[] { "#e8a33a", "#4fb0e8" };
                    settlement.SetLineup(campaign, row.Split(',').Select(i => new Settlement.LineupEntry { SoldierId = i, Colour = colours[(campaign.Soldiers.FirstOrDefault(s => s.Id == i)?.Owner ?? 0) % 2] }).ToList());
                    Tell();
                    async void Tell()
                    {
                        await Awaitable.WaitForSecondsAsync(2);
                        string Names(string root) => string.Join(", ", settlement.transform.Find(root)?.Cast<Transform>().Select(c => c.name).Where(n => n != "sitio").OrderBy(n => n) ?? Enumerable.Empty<string>());
                        Debug.Log($"[fila] row: {Names("La fila")}; camp: {Names("Vida del campamento")}");
                        // BASTION_FILA_ARMA=soldier:weapon: that found weapon given them (the Armería), the row set again as UI
                        // would; each figure of it before and after (the same one kept, a new one where the gun changed).
                        if (Environment.GetEnvironmentVariable("BASTION_FILA_ARMA") is string arm && arm.Split(':') is var a && a.Length == 2)
                        {
                            string Figures() => string.Join("; ", settlement.transform.Find("La fila").Cast<Transform>().Where(c => c.name != "sitio")
                                .Select(c => $"{c.name} #{c.gameObject.GetInstanceID()} {string.Join("+", c.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh != null ? f.sharedMesh.name : "").Where(n => n.StartsWith("SM_Wep")).Distinct())}"));
                            Debug.Log("[fila] before: " + Figures());
                            foreach (var w in campaign.FoundWeapons.Where(w => w.Holder == a[0])) w.Holder = null;
                            campaign.FoundWeapons.First(w => w.Id == a[1]).Holder = a[0];
                            settlement.SetLineup(campaign, row.Split(',').Select(i => new Settlement.LineupEntry { SoldierId = i, Colour = colours[(campaign.Soldiers.FirstOrDefault(s => s.Id == i)?.Owner ?? 0) % 2] }).ToList());
                            await Awaitable.WaitForSecondsAsync(0.5f);
                            Debug.Log("[fila] after: " + Figures());
                        }
                    }
                }
                // BASTION_PREPARA=1: Personalizar's outfits made in the background, each step logged, then one shown.
                if (Environment.GetEnvironmentVariable("BASTION_PREPARA") == "1")
                {
                    string id = Environment.GetEnvironmentVariable("BASTION_SOLDADO") ?? "c1";
                    settlement.Prepare(campaign, id);
                    int frames = 0;
                    void Watch()
                    {
                        if (!UnityEditor.EditorApplication.isPlaying) return;
                        frames++;
                        if (Bastion.Client.Units.SoldierFigure.Waiting > 0)
                        {
                            Debug.Log($"[preparar] frame {Time.frameCount}: step {Bastion.Client.Units.SoldierFigure.LastStepMs:0.0} ms, {Bastion.Client.Units.SoldierFigure.Waiting} waiting");
                            return;
                        }
                        UnityEditor.EditorApplication.update -= Watch;
                        var look = Newtonsoft.Json.JsonConvert.DeserializeObject<Appearance>(Newtonsoft.Json.JsonConvert.SerializeObject(campaign.Soldiers.First(s => s.Id == id).Appearance));
                        look.Character = "civilian1";
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        settlement.Showcase(campaign, id, look);
                        Debug.Log($"[preparar] done at frame {Time.frameCount}; shown in civilian1 in {clock.Elapsed.TotalMilliseconds:0.0} ms");
                    }
                    UnityEditor.EditorApplication.update += Watch;
                }
                // BASTION_LUEGO=tab: on to that tab at once, easing there (a strip shows the move).
                if (Environment.GetEnvironmentVariable("BASTION_LUEGO") is string then) settlement.SetView(then);
                // BASTION_MAPA=1: into the Mapa 15 frames on and back out 15 after (the fade through black: Settlement.MapShown),
                // how black each frame is and when UI's valley is told, logged.
                if (Environment.GetEnvironmentVariable("BASTION_MAPA") == "1")
                {
                    int start = Time.frameCount;
                    string back = settlement.View;
                    settlement.MapShown += on => Debug.Log($"[mapa] frame {Time.frameCount - start}: MapShown({on}), the clearing's camera {(settlement.Camera.enabled ? "on" : "off")}");
                    void Step()
                    {
                        if (!UnityEditor.EditorApplication.isPlaying)
                        {
                            UnityEditor.EditorApplication.update -= Step;
                            return;
                        }
                        int at = Time.frameCount - start;
                        if (at == 15) settlement.SetView(Settlement.Views.Geoscape);
                        if (at == 30) settlement.SetView(back);
                        if (at >= 10 && at <= 45) Debug.Log($"[mapa] frame {at}: black {Shader.GetGlobalFloat("_StageFade"):0.00}");
                    }
                    UnityEditor.EditorApplication.update += Step;
                }
                Debug.Log($"[settlement] the scene's: camera {settlement.Camera?.name}, on {settlement.View}");
            }
            // BASTION_CERCA_VIDA=x,z,distance[,yaw]: a close look (the lab's look) for -shotCamera "Cámara cerca", made now so
            // the shots can find it; and BASTION_VIDA=1, what those about the clearing do, logged every second.
            if (Environment.GetEnvironmentVariable("BASTION_CERCA_VIDA") is string close)
            {
                var v = close.Split(',').Select(k => float.Parse(k, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                var cam = new GameObject("Cámara cerca").AddComponent<Camera>();
                Look(v[0], v[1], v[2], v.Length > 3 ? v[3] : 0, cam);
            }
            if (Environment.GetEnvironmentVariable("BASTION_VIDA") == "1")
            {
                int frames = 0;
                void Report()
                {
                    if (!UnityEditor.EditorApplication.isPlaying) return;
                    if (++frames % 10 == 0 && Settlement.Instance != null) Debug.Log($"[vida] {Time.time:0.0} s: {Settlement.Instance.LifeReport()}");
                }
                UnityEditor.EditorApplication.update += Report;
            }
            UnityEditor.EditorApplication.playModeStateChanged += Go;
        }

        /// <summary>
        /// claro.html's escuadra(n, debrief, mirror, alone, empty, 'default'): the lab's clearing (Clearing's base) with a
        /// squad of BASTION_N (6) by La Lata, each with an assault rifle, in their player's default look (the first half
        /// #e8a33a, the rest #4fb0e8; BASTION_SOLO=1 one commander's), dressed by the roster; BASTION_INFORME=1 the
        /// debrief's (the second fallen, every third wounded), BASTION_ESPEJO=1 mirrored, BASTION_LIBRES=k places free;
        /// framed as there, by its lineupShot alone. BASTION_TINTE=1 with «Ropa con el color del jugador».
        ///   BASTION_TINTE=1 unity/tools/captura.sh -shotOut escuadra.png -shotSetup Bastion.EditorTools.SettlementLab.Escuadra -shotSize 1600x900
        /// </summary>
        public static void Escuadra()
        {
            // (BASTION_SIN_LUZ=1: without the soldiers' own key and sky, Settlement.Key.)
            Settlement.FaceKeyOff = Environment.GetEnvironmentVariable("BASTION_SIN_LUZ") == "1";
            // BASTION_SUBIR=1, in play mode (-shotPlay): the squad made once it runs (its clips' graphs don't outlive the
            // switch), and a moment later boarding La Lata, which then goes (Settlement.Leave). BASTION_LATA=v another vehicle.
            if (Environment.GetEnvironmentVariable("BASTION_SUBIR") == "1")
            {
                void Go(UnityEditor.PlayModeStateChange change)
                {
                    if (change != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                    UnityEditor.EditorApplication.playModeStateChanged -= Go;
                    MakeEscuadra();
                    Later();
                    async void Later()
                    {
                        await Awaitable.WaitForSecondsAsync(0.5f);
                        // BASTION_CERCA=k: the strip from close by doorway k's side (0 the driver's, 2 the other's: the 4×4's
                        // front door and the one behind it), the game's own camera moving unseen.
                        if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_CERCA"), out int near) && Settlement.Instance.Lata is LaLata lata)
                        {
                            var doors = lata.Doorways();
                            var a = doors[Mathf.Clamp(near, 0, doors.Count - 1)];
                            var b = doors[Mathf.Clamp(near + 1, 0, doors.Count - 1)];
                            var mid = (a + b) / 2;
                            var dir = new Vector3(Mathf.Sin(lata.Heading), 0, Mathf.Cos(lata.Heading));
                            var side = mid - lata.At;
                            side.y = 0;
                            side = (side - dir * Vector3.Dot(side, dir)).normalized;
                            var shot = Settlement.Instance.Camera;
                            Vector3 World(Vector3 t) => new Vector3(t.x, t.y, -t.z) * MapSpace.Tile;
                            shot.transform.position = World(mid + side * 5.2f + dir * 1.2f + Vector3.up * 1.9f);
                            shot.transform.LookAt(World(mid + Vector3.up * 0.75f));
                            shot.fieldOfView = 38;
                            var unseen = new GameObject("Cámara del juego").AddComponent<Camera>();
                            unseen.enabled = false;
                            unseen.transform.SetPositionAndRotation(shot.transform.position, shot.transform.rotation);
                            Settlement.Instance.Camera = unseen;
                            Debug.Log($"[settlement] close by doors {near}-{near + 1}: camera at {mid + side * 5.2f + dir * 1.2f + Vector3.up * 1.9f}, on {mid}");
                        }
                        float at = Time.time;
                        await Settlement.Instance.Leave();
                        Debug.Log($"[settlement] boarded and gone in {Time.time - at:0.00} s of game time");
                    }
                }
                UnityEditor.EditorApplication.playModeStateChanged += Go;
                return;
            }
            MakeEscuadra();
        }

        static void MakeEscuadra()
        {
            if (Environment.GetEnvironmentVariable("BASTION_TINTE") == "1") Bastion.Client.Units.SquadTint.On = true;
            int n = int.TryParse(Environment.GetEnvironmentVariable("BASTION_N"), out int k) ? k : 6;
            bool debrief = Environment.GetEnvironmentVariable("BASTION_INFORME") == "1";
            bool mirror = Environment.GetEnvironmentVariable("BASTION_ESPEJO") == "1";
            bool alone = Environment.GetEnvironmentVariable("BASTION_SOLO") == "1";
            int empty = int.TryParse(Environment.GetEnvironmentVariable("BASTION_LIBRES"), out int e) ? e : 0;
            var campaign = new CampaignState { FoundWeapons = new System.Collections.Generic.List<FoundWeapon>() };
            // BASTION_PISTOLA=k[,…]: those with no gun of their own in the Armería, only the pistol.
            var pistols = (Environment.GetEnvironmentVariable("BASTION_PISTOLA") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < n; i++)
            {
                bool first = alone || i < Mathf.CeilToInt(n / 2f);
                string colour = first ? "#e8a33a" : "#4fb0e8";
                campaign.Soldiers.Add(new CampaignSoldier { Id = $"lab{i}", Name = $"Lab {i}", Owner = first ? 0 : 1, Alive = true, Appearance = Appearances.DefaultAppearance(first ? 0 : 1, colour, i % 2 == 1 ? "m" : "f") });
                if (!pistols.Contains(i.ToString())) campaign.FoundWeapons.Add(new FoundWeapon { Id = $"w{i}", Kind = "assaultRifle", Holder = $"lab{i}" });
            }
            Bastion.Client.Units.Dress.Roster(campaign.Soldiers);
            Bastion.Client.Units.SquadTint.ColourOf = slot => slot == 0 ? "#e8a33a" : "#4fb0e8";
            var settlement = new GameObject("Asentamiento").AddComponent<Settlement>();
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Shot camera") ?? new GameObject("Shot camera").AddComponent<Camera>();
            settlement.Camera = camera;
            settlement.FitShots = false;
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_LATA"), out int vehicle)) settlement.SetVehicle(vehicle);
            settlement.Show(Base(), false, MapLab.Prefabs(), MapLab.Materials());
            var entries = campaign.Soldiers.Select((s, i) => new Settlement.LineupEntry
            {
                SoldierId = s.Id,
                Colour = s.Owner == 0 ? "#e8a33a" : "#4fb0e8",
                Status = !debrief ? "ok" : i == 1 ? "dead" : i % 3 == 2 ? "wounded" : "ok",
            }).Concat(Enumerable.Repeat<Settlement.LineupEntry>(null, empty)).ToList();
            // BASTION_VISTA=barracks: claro.html's soldados(), an assault without a look of its own by the caravan (seed «lab»).
            if (Environment.GetEnvironmentVariable("BASTION_VISTA") == Settlement.Views.Barracks)
            {
                settlement.SetView(Settlement.Views.Barracks, true);
                settlement.Showcase("lab", null, "pumpShotgun", false, null, "#e8a33a");
            }
            else
            {
                settlement.SetLineup(campaign, entries, mirror, !alone);
                settlement.SetView(Settlement.Views.Squad, true);
            }
            Mood.Show(Mood.Day);
            Debug.Log($"[settlement] escuadra of {n}{(debrief ? ", the debrief's" : "")}, tint {(Bastion.Client.Units.SquadTint.On ? "on" : "off")}");
        }

        /// <summary>A campaign saved as the server sends it (UI's SampleCampaign's way: the net's JSON).</summary>
        /// <summary>
        /// The siege's posts with their soldiers (Settlement.SetPosts, clearingArt.ts setPosts) on UI's sample campaign: its
        /// first soldiers on foot each on a post round the Bastión, one on a watchtower's deck if the clearing has one, in
        /// their players' colours; the Escuadra's tab in a siege. From BASTION_LOOK (the clearing's middle, 16 unless said).
        /// BASTION_TORRE=1: a watchtower put up first where it can stand nearest the middle, one of them on its deck;
        /// BASTION_MARCA=1, a yellow ball where each post's name goes (PostWorld: UI's tag).
        ///   unity/tools/captura.sh -shotOut puestos.png -shotSetup Bastion.EditorTools.SettlementLab.Puestos -shotPlay -shotFrames 1
        /// </summary>
        public static void Puestos()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var campaign = SampleCampaign(Environment.GetEnvironmentVariable("BASTION_PARTIDA") ?? Path.Combine(root, ".dev-snapshots/ui/estrategia-0101/partida-muestra.json"));
            int c = campaign.Bastion.Width / 2;
            if (Environment.GetEnvironmentVariable("BASTION_TORRE") == "1")
            {
                var b = campaign.Bastion;
                var spot = Enumerable.Range(0, 400).Select(k => (x: c - 10 + k % 20, y: c - 10 + k / 20))
                    .OrderBy(q => Mathf.Abs(Mathf.Sqrt((q.x - c) * (q.x - c) + (q.y - c) * (q.y - c)) - 6))
                    .FirstOrDefault(q => Bastion.Engine.Base.PlaceBlocker(b, PieceIds.Tower, q.x, q.y, 0) == null);
                b.Pieces.Add(new BasePiece { Id = "torre-lab", Piece = PieceIds.Tower, X = spot.x, Y = spot.y, Rot = 0, Hp = 30, Level = 1 });
                Debug.Log($"[puestos] a watchtower at {spot.x},{spot.y}");
            }
            var settlement = new GameObject("Asentamiento").AddComponent<Settlement>();
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(k => k.name == "Shot camera") ?? new GameObject("Shot camera").AddComponent<Camera>();
            settlement.Camera = camera;
            settlement.Show(campaign.Bastion, Campaign.Wintry(campaign), MapLab.Prefabs(), MapLab.Materials());
            var look = Environment.GetEnvironmentVariable("BASTION_LOOK")?.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Look(look != null && look.Length >= 3 ? look[0] : c, look != null && look.Length >= 3 ? look[1] : c, look != null && look.Length >= 3 ? look[2] : 16, look != null && look.Length >= 4 ? look[3] : 0);
            // Round the Bastión, out a few tiles each way, and a deck if there is one.
            var state = Settlement.ClearingState(campaign.Bastion, Campaign.Wintry(campaign));
            var tiles = new System.Collections.Generic.List<Vector2Int> { new Vector2Int(c - 5, c + 2), new Vector2Int(c + 5, c - 1), new Vector2Int(c - 1, c - 6), new Vector2Int(c + 2, c + 6) };
            // (A deck's middle: raised all round.)
            bool Raised(int x, int y) => x >= 0 && y >= 0 && x < state.Width && y < state.Height && state.Elev[y * state.Width + x] > 0;
            int deck = Enumerable.Range(0, state.Elev.Count).FirstOrDefault(i => state.Tiles[i] == "floor" && Enumerable.Range(0, 9).All(k => Raised(i % state.Width + k % 3 - 1, i / state.Width + k / 3 - 1)));
            if (deck > 0) tiles.Add(new Vector2Int(deck % state.Width, deck / state.Width));
            var colours = Content.PlayerColors;
            void Go(UnityEditor.PlayModeStateChange change)
            {
                if (change != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                UnityEditor.EditorApplication.playModeStateChanged -= Go;
                // (Figures made in play mode: their clips' graphs don't outlive entering it.)
                settlement.Show(campaign);
                settlement.SiegeNow = true;
                settlement.SetView(Settlement.Views.Squad, true);
                settlement.CameraFree = true;
                var squad = campaign.Soldiers.Where(s => s.Alive && s.WoundedDays <= 0).Take(tiles.Count).ToList();
                var posts = squad.Select((s, i) =>
                {
                    ColorUtility.TryParseHtmlString(colours[Mathf.Clamp(s.Owner, 0, colours.Count - 1)], out var colour);
                    return new Settlement.SiegePost { id = s.Id, name = s.Name, colour = colour, x = tiles[i].x, y = tiles[i].y, mine = s.Owner == 0 };
                }).ToList();
                settlement.SetPosts(posts, null);
                if (Environment.GetEnvironmentVariable("BASTION_MARCA") == "1")
                    foreach (var p in posts)
                    {
                        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
                        ball.name = "Etiqueta " + p.name;
                        ball.transform.position = settlement.PostWorld(p.id) ?? Vector3.zero;
                        ball.transform.localScale = Vector3.one * 0.25f;
                        var m = KitMaterials.New(KitMaterials.Glowing, "Etiqueta");
                        if (m != null) m.SetColor("_BaseColor", Color.yellow);
                        ball.GetComponent<Renderer>().sharedMaterial = m;
                    }
                Look(look != null && look.Length >= 3 ? look[0] : c, look != null && look.Length >= 3 ? look[1] : c, look != null && look.Length >= 3 ? look[2] : 16, look != null && look.Length >= 4 ? look[3] : 0, camera);
                // BASTION_LOOK's 5th: the point looked at that many storeys up (a deck).
                if (look != null && look.Length >= 5) camera.transform.position += Vector3.up * look[4] * MapSpace.Storey;
                Debug.Log($"[puestos] {string.Join("; ", posts.Select(p => $"{p.name} at {p.x},{p.y}{(state.Elev[p.y * state.Width + p.x] > 0 ? " (deck)" : "")}"))}");
            }
            UnityEditor.EditorApplication.playModeStateChanged += Go;
        }

        static CampaignState SampleCampaign(string file)
        {
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));
            var net = Bastion.Net.Json.Settings;
            var settings = new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = net.ContractResolver, NullValueHandling = net.NullValueHandling, MissingMemberHandling = net.MissingMemberHandling, MaxDepth = net.MaxDepth };
            foreach (var c in net.Converters) settings.Converters.Add(c);
            settings.Converters.Add(new Bastion.Net.TagConverter<ProposalAction>("type", a => a.Type));
            settings.Converters.Add(new Bastion.Net.TagConverter<CampaignCommand>("type", c => c.Type));
            return (json["campaign"] ?? json).ToObject<CampaignState>(Newtonsoft.Json.JsonSerializer.Create(settings));
        }

        static BaseState Base()
        {
            string file = Environment.GetEnvironmentVariable("BASTION_BASE_FILE");
            if (file != null)
                return Newtonsoft.Json.JsonConvert.DeserializeObject<BaseState>(File.ReadAllText(file), new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() });
            string golden = Environment.GetEnvironmentVariable("BASTION_BASE");
            if (golden != null)
            {
                var cases = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "../../../engine-cs/golden/step3/base.json")))["cases"];
                var found = cases.FirstOrDefault(k => (string)k["name"] == golden) ?? throw new ArgumentException($"no base {golden} in base.json");
                return found["options"]["map"]["base"].ToObject<BaseState>(Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }));
            }
            var @base = Bastion.Engine.Base.FreshBase(new Rng(int.TryParse(Environment.GetEnvironmentVariable("BASTION_SEED"), out int seed) ? seed : 3));
            int c = @base.Width / 2;
            if (Environment.GetEnvironmentVariable("BASTION_BARE") != "1") Pieces(@base, c);
            // The Bastion at another level, worn 0 to 3 (the lab's nivel(n, desgaste), once its pieces are in: the caravan's health).
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_NIVEL"), out int level)) @base.Level = level;
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_DESGASTE"), out int wear) && @base.Pieces.Find(p => p.Piece == PieceIds.Caravan) is BasePiece caravan)
                caravan.Hp = new[] { 40, 26, 12, 0 }[Mathf.Clamp(wear, 0, 3)];
            return @base;
        }

        /// <summary>The lab's pieces round the caravan (clearingLab.ts), on a clearing felled round the middle `c`.</summary>
        static void Pieces(BaseState @base, int c)
        {
            for (int y = c - 9; y <= c + 9; y++)
                for (int x = c - 11; x <= c + 11; x++)
                    Bastion.Engine.Base.FellTree(@base, x, y);
            void Put(string id, int x, int y, int rot = 0, int lv = 1)
            {
                var p = Bastion.Engine.Base.PlacePiece(@base, id, x, y, rot);
                if (p.Level != 0) p.Level = lv;
            }
            Put("radio", c - 9, c - 8, 0, 2);
            Put("quarters", c - 5, c - 8, 0, 3);
            Put("workshop", c + 4, c - 8, 1, 1);
            Put("garden", c - 9, c + 3, 0, 2);
            Put("memorial", c + 1, c + 4);
            for (int y = c - 4; y <= c + 1; y++) Put(y == c - 2 ? "gate" : "palisade", c - 10, y);
            Put("sandbags", c - 6, c - 3);
            Put("sandbags", c - 5, c - 3);
            Put("logs", c + 4, c - 3);
            Put("tower", c + 7, c - 3);
            Put("floodlight", c - 7, c);
            Put("turret", c + 7, c + 1);
            Put("snare", c - 5, c + 2);
            // A palisade across the barracks' front and a tower by the caravan's end, in the way of both shots.
            if (Environment.GetEnvironmentVariable("BASTION_CERCO") == "1")
            {
                for (int x = c - 7; x <= c; x++) Put("palisade", x, c - 2);
                Put("tower", c + 4, c + 1);
            }
        }

        /// <summary>What the ambient probe gives a surface facing up (URP's SampleSH, linear).</summary>
        static Color Up(UnityEngine.Rendering.SphericalHarmonicsL2 sh, Vector3? normal = null)
        {
            var colours = new Color[1];
            sh.Evaluate(new[] { normal ?? Vector3.up }, colours);
            return colours[0];
        }

        /// <summary>The clearing as a battlefield with nobody on it: the map its siege would be fought on (clearingArt.ts clearingState).</summary>
        public static GameState ClearingState(BaseState @base)
        {
            return Settlement.ClearingState(@base, false);
        }

        /// <summary>The lab's camera (clearingLab.ts look): on (x, z) tiles from `distance` tiles, its bearing `yaw` from the south-east, 50° down, 40° of view.</summary>
        static void Look(float x, float z, float distance, float yaw, Camera camera = null)
        {
            camera = camera != null ? camera : UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(k => k.name == "Shot camera")
                ?? new GameObject("Shot camera").AddComponent<Camera>();
            float a = Mathf.PI / 4 + yaw, down = 50 * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a) * Mathf.Cos(down), Mathf.Sin(down), Mathf.Cos(a) * Mathf.Cos(down));
            var target = new Vector3(x, 0, z);
            var at = target + dir * distance;
            // 0.10.1's tiles (z south) to Unity's metres.
            camera.transform.position = new Vector3(at.x, at.y, -at.z) * MapSpace.Tile;
            camera.transform.LookAt(new Vector3(target.x, target.y, -target.z) * MapSpace.Tile);
            camera.fieldOfView = 40;
            camera.farClipPlane = 800;
        }
    }
}
