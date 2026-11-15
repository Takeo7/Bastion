using System;
using System.Linq;
using Bastion.Client.Map;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The animals about a map (Fauna; Gameplay 1/11) on MapLab's (BASTION_MAP, city-1 unless said), and a stand-in for
    /// someone (a grey post a man's height) walking at a soldier's pace towards them from BASTION_DESDE tiles off (9), so
    /// they are seen to run or fly off; the camera turned BASTION_GIRO (45).
    ///   Perros: on the first dog. Pajaros: on the first crows. Campo: on the first wild one (a country map, wilds-7).
    /// The camera on it (between it and the stand-in's start, a third of the way) BASTION_DIST tiles off (12).
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.AnimalesLab.Perros -shotFrames 80 -shotFps 10 -shotWarm 0.5 …
    /// For Arturo's play-test of 14/11 (fewer, and food, shelter and running from the dead of their own): BASTION_NOCHE=1
    /// the map by night; BASTION_CUERPO=x,y (or 1) a body of the dead to eat at (Body); BASTION_QUIERE=Food|Shelter|Wander
    /// what every animal goes about (Fauna.LabWant); BASTION_SONDA_PLAN=1 each choice in the log ([plan]). Palomas: the
    /// pigeons. Muerto: one of the dead walking at the animal. Cuantos: how many each map gets. Coste: what they cost.
    /// </summary>
    public static class AnimalesLab
    {
        public static void Perros() => Show("Dog");

        /// <summary>BASTION_SONDA_FAUNA=1: in play mode, each dog's animator in the log every second (Fauna.AnimatorReport).</summary>
        [InitializeOnLoadMethod]
        static void Probe()
        {
            if (Environment.GetEnvironmentVariable("BASTION_SONDA_FAUNA") != "1") return;
            double next = 0;
            EditorApplication.update += () =>
            {
                if (!Application.isPlaying || Fauna.Current == null || EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + 1;
                foreach (string line in Fauna.Current.AnimatorReport()) Debug.Log($"[sonda] {Time.time:0.0} s {line}");
            };
        }
        public static void Pajaros() => Show("Crow");
        /// <summary>The pigeons' flock on a town's paving (Arturo's play-test of 14/11: they fly up to a roof, not off the map).</summary>
        public static void Palomas() => Show("Pigeon");
        public static void Campo() => Show("Wild");
        /// <summary>A rabbit (SIMPLE's, Arturo 1/11) on a country map, wilds-7: hopping off as the stand-in comes, before the rest.</summary>
        public static void Conejo() => Show("Grazer", "Rabbit");
        /// <summary>A cat (SIMPLE - Cats', Ana's #11 of 1/11) on a city, by a wall: sitting, off before the dog as the stand-in comes.</summary>
        public static void Gatos() => Show("Cat");
        /// <summary>The stag (BASTION_RAZA another: Doe, Boar).</summary>
        public static void Ciervo() => Show("Grazer", Environment.GetEnvironmentVariable("BASTION_RAZA") ?? "Stag");

        /// <summary>
        /// For Arturo's #7 (1/11, more animals: POLYGON has no deer nor rabbits, SIMPLE - Forest Animals has): the Dog Pack's
        /// wolf, fox, coyote and a dog, then SIMPLE's wolf, doe, stag, boar and rabbit, in a row on a clearing of MapLab's
        /// map (wilds-7, where its first wild one stands), under the game's light and camera (BASTION_DIST tiles off, 24).
        /// SIMPLE's models from a test folder outside git (Assets/Synty/_PruebaSimple, its source FBX and texture), each
        /// brought to its real size.
        /// </summary>
        public static void Comparar()
        {
            MapLab.Day();
            var fauna = Fauna.Current;
            var spot = fauna?.Animals.FirstOrDefault(a => a.kind == "Wild").root;
            if (spot == null)
            {
                Debug.LogWarning("[animales] no clearing found");
                return;
            }
            var at = spot.position;
            fauna.gameObject.SetActive(false);
            var prefabOf = MapLab.Prefabs();
            var controller = Resources.Load<RuntimeAnimatorController>(Fauna.ControllerName);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Synty/_PruebaSimple/SimpleForestAnimalsTexture.png");
            var simple = new Material(Shader.Find("Universal Render Pipeline/Lit")) { mainTexture = texture };
            simple.SetFloat("_Smoothness", 0.15f);
            var row = new (string model, float tall)[]
            {
                ("dog:SK_Dog_Wolf_01", 0), ("dog:SK_Dog_Fox_01", 0), ("dog:SK_Dog_Coyote_01", 0), ("dog:SK_Dog_Labrador_01", 0),
                ("SM_Wolf_01", 0.85f), (Environment.GetEnvironmentVariable("BASTION_CIERVA") ?? "SM_Doe_02", 1.4f), ("SM_Stag_01", 1.6f), ("SM_Boar_01", 0.8f), ("SM_Rabbit_01", 0.3f),
            };
            // (Across the picture, along the camera's right, from the clearing's edge into it: each as near as the others.)
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            MapSpace.Aim(camera, at, Number("BASTION_DIST", 24), Number("BASTION_GIRO", 45));
            var right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            var down = Vector3.ProjectOnPlane(-camera.transform.up, Vector3.up).normalized;
            at += right * Number("BASTION_DERECHA", 5) * MapSpace.Tile + down * Number("BASTION_ABAJO", 2) * MapSpace.Tile;
            float x = -(row.Length - 1) / 2f * 1.3f * MapSpace.Tile;
            foreach (var (model, tall) in row)
            {
                var prefab = tall == 0 ? prefabOf(model) : AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Synty/_PruebaSimple/{model}.fbx");
                if (prefab == null)
                {
                    Debug.LogWarning($"[animales] missing {model}");
                    continue;
                }
                var made = (GameObject)UnityEngine.Object.Instantiate(prefab);
                made.name = model;
                made.transform.position = at + right * x;
                made.transform.rotation = Quaternion.LookRotation(right);
                x += 1.3f * MapSpace.Tile;
                var animator = made.GetComponentInChildren<Animator>();
                if (animator != null && controller != null) animator.runtimeAnimatorController = controller;
                if (tall == 0) continue;
                foreach (var r in made.GetComponentsInChildren<Renderer>()) r.sharedMaterial = simple;
                var box = made.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                made.transform.localScale *= tall / Mathf.Max(box.size.y, 1e-4f);
            }
            MapSpace.Aim(camera, at, Number("BASTION_DIST", 24), Number("BASTION_GIRO", 45));
            Debug.Log($"[animales] a row of {row.Length} at {MapSpace.WorldToTile(at)}");
        }

        /// <summary>
        /// Each clip of the animals' controller (Fauna.ControllerName) played on a Labrador at 60 fps, in the log: its length,
        /// whether it loops, how far its last pose is from its first (the greatest turn of a bone, degrees, and the most a
        /// bone's end moves, cm: what jumps as it starts again), its usual turn from one frame to the next, and the frames
        /// where a bone turns or moves far more than that (a hitch in the clip).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.AnimalesLab.Clips -quit
        /// </summary>
        public static void Clips()
        {
            var controller = Resources.Load<RuntimeAnimatorController>(Fauna.ControllerName);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonDog/Prefabs/Dogs/SK_Dog_Labrador_01.prefab");
            if (controller == null || prefab == null)
            {
                Debug.LogError("[clips] no controller or no dog");
                return;
            }
            var dog = (GameObject)UnityEngine.Object.Instantiate(prefab);
            var bones = dog.GetComponentsInChildren<Transform>().Where(t => t != dog.transform && t.GetComponent<Renderer>() == null).ToArray();
            var log = new System.Text.StringBuilder();
            foreach (var clip in controller.animationClips.Distinct().OrderBy(c => c.name))
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * 60));
                var turns = new Quaternion[frames + 1][];
                var places = new Vector3[frames + 1][];
                for (int f = 0; f <= frames; f++)
                {
                    clip.SampleAnimation(dog, clip.length * f / frames);
                    turns[f] = bones.Select(b => b.localRotation).ToArray();
                    places[f] = bones.Select(b => dog.transform.InverseTransformPoint(b.position)).ToArray();
                }
                (float turn, float move, string bone) Step(int a, int b)
                {
                    float turn = 0, move = 0;
                    string which = "";
                    for (int k = 0; k < bones.Length; k++)
                    {
                        float t = Quaternion.Angle(turns[a][k], turns[b][k]), m = Vector3.Distance(places[a][k], places[b][k]) * 100;
                        if (t > turn) turn = t;
                        if (m > move)
                        {
                            move = m;
                            which = bones[k].name;
                        }
                    }
                    return (turn, move, which);
                }
                var steps = Enumerable.Range(0, frames).Select(f => Step(f, f + 1)).ToArray();
                var sorted = steps.Select(s => s.move).OrderBy(m => m).ToArray();
                float usual = sorted[sorted.Length / 2], usualTurn = steps.Select(s => s.turn).OrderBy(m => m).ToArray()[sorted.Length / 2];
                var wrap = Step(frames, 0);
                // Across the loop: the last step into the first frame against the usual one (with its own last step, Step(frames − 1, frames)).
                var across = Step(frames - 1, 0);
                log.AppendLine(FormattableString.Invariant($"{clip.name}: {clip.length:0.00} s ({clip.frameRate:0} fps), loop {settings.loopTime} (pose {settings.loopBlend}); end to start {wrap.turn:0.0}° {wrap.move:0.0} cm ({wrap.bone}); usual step {usualTurn:0.00}° {usual:0.00} cm, the step across the loop {across.move:0.00} cm"));
                for (int f = 0; f < frames; f++)
                    if (steps[f].move > Mathf.Max(4 * usual, 0.6f))
                        log.AppendLine(FormattableString.Invariant($"   frame {f} → {f + 1} ({clip.length * f / frames:0.000} s): {steps[f].turn:0.0}° {steps[f].move:0.00} cm ({steps[f].bone})"));
            }
            UnityEngine.Object.DestroyImmediate(dog);
            Debug.Log("[clips]\n" + log);
        }

        /// <summary>
        /// Whether the animals' walking and running clips go on ahead as they play (Gameplay, 7/11: «como que vuelve»), each
        /// played alone at 60 fps (SampleAnimation) on its own animal: the dog's on our controller, SIMPLE's stag, boar,
        /// rabbit and cat on theirs. For each clip, how its hips, its skin's root bone and the middle of its bones move
        /// forward and up over one loop (cm, in the animal's own space: the least and most, and from its end back to its
        /// start), and how its model was imported (its root motion node, and the clip's root baked into its pose or not).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.AnimalesLab.ClipsAhead -quit
        /// </summary>
        public static void ClipsAhead()
        {
            var log = new System.Text.StringBuilder();
            var animals = new (string prefab, RuntimeAnimatorController controller)[]
            {
                ("Assets/Synty/PolygonDog/Prefabs/Dogs/SK_Dog_Labrador_01.prefab", Resources.Load<RuntimeAnimatorController>(Fauna.ControllerName)),
                ("Assets/Synty/SimpleForestAnimal/Prefabs/Stag_02.prefab", null),
                ("Assets/Synty/SimpleForestAnimal/Prefabs/Boar_01.prefab", null),
                ("Assets/Synty/SimpleForestAnimal/Prefabs/Rabbit_01.prefab", null),
                ("Assets/Synty/SimpleCats/Prefabs/SimpleCats_Tabby_01.prefab", null),
            };
            foreach (var (path, own) in animals)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    log.AppendLine($"{path}: none");
                    continue;
                }
                var body = (GameObject)UnityEngine.Object.Instantiate(prefab);
                body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = body.GetComponentInChildren<Animator>();
                var controller = own ?? animator?.runtimeAnimatorController;
                // (SIMPLE's prefabs keep every look but one off: the one named as the prefab, or the first.)
                var skins = body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null).ToList();
                var skin = skins.FirstOrDefault(r => r.gameObject.activeInHierarchy) ?? skins.FirstOrDefault(r => r.name.EndsWith(prefab.name.Split('_').Last())) ?? skins.FirstOrDefault();
                var bones = skin != null ? skin.bones.Where(b => b != null).ToArray() : new Transform[0];
                var hip = body.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.IndexOf("hip", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("pelvis", StringComparison.OrdinalIgnoreCase) >= 0) ?? skin?.rootBone;
                log.AppendLine($"{prefab.name}: controller {controller?.name}, hip {hip?.name}, root bone {skin?.rootBone?.name}, {bones.Length} bones");
                if (controller == null)
                {
                    UnityEngine.Object.DestroyImmediate(body);
                    continue;
                }
                // (What it plays from: SampleAnimation on the object the animator is on, its bones' paths from there.)
                var on = animator != null ? animator.gameObject : body;
                foreach (var clip in controller.animationClips.Distinct().Where(c => System.Text.RegularExpressions.Regex.IsMatch(c.name, "walk|run|trot|gallop|canter|sprint|locomotion|move", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).OrderBy(c => c.name))
                {
                    int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * 60));
                    var points = new Vector3[frames + 1][];
                    // (The object the animator is on moved by the clip itself: its root's curves, which an animator without
                    // root motion writes onto it, frame by frame.)
                    var self = new Vector3[frames + 1];
                    var turned = new float[frames + 1];
                    for (int f = 0; f <= frames; f++)
                    {
                        clip.SampleAnimation(on, clip.length * f / frames);
                        self[f] = on.transform.localPosition;
                        turned[f] = on.transform.localEulerAngles.y;
                        var middle = bones.Length > 0 ? bones.Aggregate(Vector3.zero, (s, b) => s + b.position) / bones.Length : Vector3.zero;
                        points[f] = new[] { hip != null ? hip.position : Vector3.zero, skin?.rootBone != null ? skin.rootBone.position : Vector3.zero, middle }
                            .Select(v => body.transform.InverseTransformPoint(v)).ToArray();
                    }
                    string clipPath = AssetDatabase.GetAssetPath(clip);
                    string import = "";
                    if (AssetImporter.GetAtPath(clipPath) is ModelImporter model)
                    {
                        var setting = model.clipAnimations.Concat(model.defaultClipAnimations).FirstOrDefault(c => c.name == clip.name);
                        import = $"; import: {model.animationType}, motion node «{model.motionNodeName}»" + (setting != null ? $", loop {setting.loopTime}, bake XZ {setting.lockRootPositionXZ}, bake Y {setting.lockRootHeightY}, bake turn {setting.lockRootRotation}" : "");
                    }
                    log.AppendLine(FormattableString.Invariant($"  {clip.name} ({clip.length:0.00} s, root motion curves {clip.hasRootCurves || clip.hasMotionCurves}{import})"));
                    var travel = self[frames] - self[0];
                    log.AppendLine(FormattableString.Invariant($"    the object itself: {travel.z * 100:+0.0;-0.0} cm forward, {travel.x * 100:+0.0;-0.0} cm aside, up {self.Min(v => v.y) * 100:0.0} to {self.Max(v => v.y) * 100:0.0} cm over the loop ({travel.z / clip.length:0.00} m/s), turned {turned.Min(a => Mathf.DeltaAngle(0, a)):0.0}° to {turned.Max(a => Mathf.DeltaAngle(0, a)):0.0}°"));
                    on.transform.localPosition = Vector3.zero;
                    on.transform.localRotation = Quaternion.identity;
                    string[] names = { "hip", "root bone", "middle" };
                    for (int k = 0; k < 3; k++)
                    {
                        var forward = points.Select(q => q[k].z * 100).ToArray();
                        var up = points.Select(q => q[k].y * 100).ToArray();
                        var side = points.Select(q => q[k].x * 100).ToArray();
                        log.AppendLine(FormattableString.Invariant($"    {names[k]}: forward {forward.Min():0.0} to {forward.Max():0.0} cm, end to start {forward[0] - forward[frames]:+0.0;-0.0} cm; side {side.Min():0.0} to {side.Max():0.0} cm, end to start {side[0] - side[frames]:+0.0;-0.0}; up {up.Min():0.0} to {up.Max():0.0} cm"));
                    }
                }
                UnityEngine.Object.DestroyImmediate(body);
            }
            Debug.Log("[avance]\n" + log);
        }

        static float Number(string name, float otherwise) =>
            float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : otherwise;

        /// <summary>
        /// How many animals each map gets (Arturo's play-test of 14/11: «dio la impresión de que había demasiados»): for each
        /// of BASTION_MAPAS (step1's goldens, and BASTION_SEMILLAS generated missions of each biome, 12, unless said), its
        /// Fauna made as MapBuilder makes it (an empty stands in for each model: nothing drawn), and in the log a line
        /// [cuantos] with the map, its size and its outdoor tiles, each kind's count and how many of them stand where the
        /// squad sees as the mission starts (Los.TeamVisibility; one on a roof, if a tile beside its own is), and at the end
        /// each biome's mean a map.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.AnimalesLab.Cuantos -quit
        /// </summary>
        public static void Cuantos()
        {
            var names = Environment.GetEnvironmentVariable("BASTION_MAPAS")?.Split(',') ?? new[] { "city-1", "city-5-night", "city-7", "facility-1", "facility-7", "wilds-1", "wilds-3-night", "wilds-7", "torre" };
            var maps = names.Select(n => (name: n, read: (Func<Bastion.Engine.GameState>)(() => MapLab.Golden(n)))).ToList();
            int seeds = (int)Number("BASTION_SEMILLAS", 12);
            foreach (string biome in Bastion.Engine.Biomes.All)
                for (int k = 1; k <= seeds; k++)
                {
                    int seed = k;
                    maps.Add(($"{biome}:{seed}", () => MapLab.Generated(biome, seed, "elimination")));
                }
            var stub = new GameObject("modelo de prueba");
            string[] kinds = { "Dog", "Cat", "Wild", "Grazer", "Crow", "Pigeon" };
            var sums = new System.Collections.Generic.Dictionary<string, (int maps, float tiles, int[] all, int[] seen)>();
            foreach (var (name, read) in maps)
            {
                Bastion.Engine.GameState s;
                try { s = read(); }
                catch (Exception e)
                {
                    Debug.Log($"[cuantos] {name}: not made ({e.GetType().Name}: {e.Message})");
                    continue;
                }
                var plan = new BuildingPlan(s);
                BuildingSurfaces.Of(s, plan);
                var parent = new GameObject("Cuantos");
                var fauna = Fauna.Make(parent.transform, s, plan, key => stub);
                var sight = Bastion.Engine.Los.TeamVisibility(s, Bastion.Engine.Teams.Xcom);
                bool Seen(int x, int y)
                {
                    if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return false;
                    int node = Bastion.Engine.Grid.NodeOf(s, new Bastion.Engine.Pos { X = x, Y = y, Z = 0 });
                    return node >= 0 && node < sight.Length && sight[node];
                }
                int outdoor = 0;
                for (int i = 0; i < s.Width * s.Height; i++)
                    if (Bastion.Engine.Grid.IsWalkable(s.Tiles[i]) && plan.Owner[i] < 0 && (s.Ground == null || i >= s.Ground.Count || (s.Ground[i] != 2 && s.Ground[i] != 4))) outdoor++;
                var all = new int[kinds.Length];
                var seen = new int[kinds.Length];
                foreach (var (kind, root) in fauna.Animals)
                {
                    int k = Array.IndexOf(kinds, kind);
                    if (k < 0) continue;
                    all[k]++;
                    var t = MapSpace.WorldToTile(root.position);
                    bool high = root.position.y > 1;
                    bool look = Seen(t.x, t.y);
                    for (int dy = -1; dy <= 1 && high && !look; dy++) for (int dx = -1; dx <= 1 && !look; dx++) look = Seen(t.x + dx, t.y + dy);
                    if (look) seen[k]++;
                }
                Debug.Log($"[cuantos] {name} {s.Biome}{(s.Base != null ? " base" : "")} {s.Width}×{s.Height}, {outdoor} outdoor: " + string.Join(", ", kinds.Select((k, i) => $"{k} {all[i]} ({seen[i]})")) + $"; total {all.Sum()} ({seen.Sum()} seen)");
                string key = s.Base != null ? "base" : s.Biome;
                var sum = sums.TryGetValue(key, out var was) ? was : (maps: 0, tiles: 0f, all: new int[kinds.Length], seen: new int[kinds.Length]);
                sum.maps++;
                sum.tiles += outdoor;
                for (int i = 0; i < kinds.Length; i++)
                {
                    sum.all[i] += all[i];
                    sum.seen[i] += seen[i];
                }
                sums[key] = sum;
                UnityEngine.Object.DestroyImmediate(parent);
            }
            UnityEngine.Object.DestroyImmediate(stub);
            foreach (var (key, sum) in sums.OrderBy(p => p.Key))
                Debug.Log(FormattableString.Invariant($"[cuantos] mean of {sum.maps} {key} maps ({sum.tiles / sum.maps:0} outdoor tiles): ") + string.Join(", ", kinds.Select((k, i) => FormattableString.Invariant($"{k} {sum.all[i] / (float)sum.maps:0.0} ({sum.seen[i] / (float)sum.maps:0.0})")))
                    + FormattableString.Invariant($"; total {sum.all.Sum() / (float)sum.maps:0.0} ({sum.seen.Sum() / (float)sum.maps:0.0} seen)"));
        }

        /// <summary>
        /// What the animals cost in play (Arturo's play-test of 14/11, their own behaviour: measured before and after): MapLab's
        /// map (BASTION_MAP, city-7) by day with its animals, the camera over its middle BASTION_DIST tiles off (24); from
        /// BASTION_ESPERA frames on (120), BASTION_CUADROS frames (1200) in blocks of 60, the animals on and off in turn (Fauna's
        /// object): the main thread's frame, the scripts' Update (Fauna's among them) and the animators' (the player loop's
        /// DirectorUpdateAnimation markers), ms a frame, the mean of each half and the difference. [coste]
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.AnimalesLab.Coste -shotPlay -shotCamera "Shot camera"
        ///     -shotFps 30 -shotWarm 46 -shotFrames 1 -shotOut &lt;file.png&gt;
        /// </summary>
        public static void Coste()
        {
            MapLab.Day();
            var fauna = Fauna.Current;
            if (fauna == null)
            {
                Debug.LogWarning("[coste] the map made no animals");
                return;
            }
            var state = Bastion.Client.NightLights.StateForLab;
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            MapSpace.Aim(camera, MapSpace.World(state.Width / 2, state.Height / 2), Number("BASTION_DIST", 24));
            int wait = (int)Number("BASTION_ESPERA", 120), frames = (int)Number("BASTION_CUADROS", 1200);
            string[] wanted = { "PlayerLoop", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.DirectorUpdateAnimationBegin", "PreLateUpdate.DirectorUpdateAnimationEnd" };
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var recorders = new System.Collections.Generic.List<(string name, Unity.Profiling.ProfilerRecorder recorder)>();
                var on = new double[wanted.Length];
                var off = new double[wanted.Length];
                int from = -1, frame = -1, counted = 0, onFrames = 0, offFrames = 0;
                var animals = fauna.gameObject;
                void Step()
                {
                    if (from < 0) from = Time.frameCount + wait;
                    if (Time.frameCount == frame) return;
                    frame = Time.frameCount;
                    if (Time.frameCount < from) return;
                    if (recorders.Count == 0)
                    {
                        var handles = new System.Collections.Generic.List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
                        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
                        foreach (string name in wanted)
                        {
                            var found = handles.Select(h => Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h)).FirstOrDefault(d => d.Name == name);
                            recorders.Add((name, found.Name != null ? Unity.Profiling.ProfilerRecorder.StartNew(found.Category, found.Name, 1) : default));
                        }
                        Debug.Log($"[coste] {fauna.Animals.Count()} animals; markers: " + string.Join(", ", recorders.Select(r => $"{r.name} {(r.recorder.Valid ? "on" : "missing")}")));
                        return;
                    }
                    // (The block just measured: the animals were as they are now since its first frame.)
                    bool were = animals.activeSelf;
                    for (int k = 0; k < recorders.Count; k++)
                        if (recorders[k].recorder.Valid) (were ? on : off)[k] += recorders[k].recorder.LastValue / 1e6;
                    if (were) onFrames++;
                    else offFrames++;
                    // (Switched a frame ahead of the block's first counted frame: what goes off settles.)
                    if (++counted % 60 == 0) animals.SetActive(!were);
                    if (counted < frames) return;
                    animals.SetActive(true);
                    EditorApplication.update -= Step;
                    Debug.Log($"[coste] {Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7"}: {onFrames} frames with the animals, {offFrames} without; ms a frame, with / without / the difference: "
                        + string.Join(" · ", wanted.Select((n, k) => FormattableString.Invariant($"{n} {on[k] / Math.Max(1, onFrames):0.000} / {off[k] / Math.Max(1, offFrames):0.000} / {on[k] / Math.Max(1, onFrames) - off[k] / Math.Max(1, offFrames):+0.000;-0.000}"))));
                    foreach (var (_, recorder) in recorders) recorder.Dispose();
                }
                EditorApplication.update += Step;
            };
        }

        /// <summary>
        /// One of the dead walking at an animal (Arturo's play-test of 14/11: they run from the dead too; the dead don't see
        /// them, Arturo's choice): MapLab's map (BASTION_MAP, city-7; BASTION_NOCHE=1 by night), the animal Show would pick
        /// (BASTION_TIPO, Dog unless said; BASTION_RAZA, BASTION_ANIMAL, BASTION_EN) and one of the map's dead put
        /// BASTION_DESDE tiles off it (8) to the south-east, on the nearest tile it can stand on, the map's other units gone; a
        /// second in it walks at the animal by the engine's ways (UnitViews.MoveAlong). BASTION_OCULTO=1: the dead not drawn
        /// (out of the squad's sight: the animal runs all the same). The camera as Show's (BASTION_DIST, BASTION_GIRO,
        /// BASTION_MIRA, BASTION_SEGUIR).
        ///   BASTION_MAP=wilds-7 BASTION_TIPO=Grazer unity/tools/captura.sh -shotSetup Bastion.EditorTools.AnimalesLab.Muerto -shotPlay
        ///     -shotCamera "Shot camera" -shotFrames 150 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Muerto()
        {
            if (Environment.GetEnvironmentVariable("BASTION_NOCHE") == "1") MapLab.Night();
            else MapLab.Day();
            var fauna = Fauna.Current;
            var state = Bastion.Client.NightLights.StateForLab;
            if (fauna == null || state == null)
            {
                Debug.LogWarning("[animales] the map made no animals");
                return;
            }
            string kind = Environment.GetEnvironmentVariable("BASTION_TIPO") ?? "Dog", breed = Environment.GetEnvironmentVariable("BASTION_RAZA");
            var middle = MapSpace.World(state.Width / 2, state.Height / 2);
            var first = fauna.Animals.Where(a => a.kind == kind && (breed == null || a.root.name.StartsWith(breed))).OrderBy(a => Vector3.Distance(a.root.position, middle)).Skip((int)Number("BASTION_ANIMAL", 0)).FirstOrDefault().root;
            if (first == null)
            {
                Debug.LogWarning($"[animales] no {kind} on the map");
                return;
            }
            if (Environment.GetEnvironmentVariable("BASTION_EN")?.Split(',') is string[] en && en.Length == 2)
                first.position = MapSpace.World(int.Parse(en[0]), int.Parse(en[1]));
            var dead = state.Units.Where(u => u.Alive && u.Team == Bastion.Engine.Teams.Alien && Bastion.Client.Units.Gaits.MovesDead(u.Template)).OrderBy(u => u.Template == "zombie" ? 0 : 1).FirstOrDefault();
            if (dead == null)
            {
                Debug.LogWarning("[animales] no dead on the map");
                return;
            }
            state.Units = new System.Collections.Generic.List<Bastion.Engine.Unit> { dead };
            var plan = new BuildingPlan(state);
            var at = MapSpace.WorldToTile(first.position);
            float off = Number("BASTION_DESDE", 8);
            int wantX = at.x + Mathf.RoundToInt(off / Mathf.Sqrt(2)), wantY = at.y + Mathf.RoundToInt(off / Mathf.Sqrt(2));
            // (Where it starts: the open outdoor tile nearest that place; where it goes, the one beside the animal it reaches.)
            bool Open(int x, int y) => x >= 1 && y >= 1 && x < state.Width - 1 && y < state.Height - 1 && Bastion.Engine.Grid.IsWalkable(state.Tiles[y * state.Width + x]) && plan.Owner[y * state.Width + x] < 0;
            var start = Enumerable.Range(0, state.Width * state.Height).Select(i => (x: i % state.Width, y: i / state.Width)).Where(p => Open(p.x, p.y))
                .OrderBy(p => (p.x - wantX) * (p.x - wantX) + (p.y - wantY) * (p.y - wantY)).First();
            dead.Pos = new Bastion.Engine.Pos { X = start.x, Y = start.y, Z = 0 };
            dead.Facing = Math.Atan2(at.y - start.y, at.x - start.x);
            var reach = Bastion.Engine.Paths.ComputeReach(state, dead, 400);
            var to = Bastion.Engine.Paths.Destinations(state, dead, reach).OrderBy(d => Math.Max(Math.Abs(d.Pos.X - at.x), Math.Abs(d.Pos.Y - at.y))).First().Pos;
            var path = Bastion.Engine.Paths.BuildPath(state, dead, reach, to) ?? new System.Collections.Generic.List<Bastion.Engine.Pos>();
            var units = new GameObject("Unidades").AddComponent<Bastion.Client.Units.UnitViews>();
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            var startAt = MapSpace.World(start.x, start.y);
            MapSpace.Aim(camera, Vector3.Lerp(first.position, startAt, Number("BASTION_MIRA", 0.3f)), Number("BASTION_DIST", 12), Number("BASTION_GIRO", 45));
            bool follow = Environment.GetEnvironmentVariable("BASTION_SEGUIR") == "1";
            Debug.Log($"[animales] {kind} {first.name} on {at.x},{at.y}; {dead.Id} ({dead.Template}) from {start.x},{start.y} to {to.X},{to.Y}, {path.Count} tiles{(Environment.GetEnvironmentVariable("BASTION_OCULTO") == "1" ? ", not drawn" : "")}");
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                Walk();
            };
            async void Walk()
            {
                try
                {
                    units.Sync(state);
                    if (Environment.GetEnvironmentVariable("BASTION_OCULTO") != "1") units.Reveal(dead.Id);
                    if (follow)
                    {
                        var stepping = Fauna.Stepping;
                        Fauna.Stepping = dt =>
                        {
                            stepping?.Invoke(dt);
                            if (camera != null && first != null) MapSpace.Aim(camera, Vector3.Lerp(first.position, units.Feet(dead.Id), Number("BASTION_MIRA", 0.3f)), Number("BASTION_DIST", 12), Number("BASTION_GIRO", 45));
                        };
                    }
                    await Awaitable.WaitForSecondsAsync(1);
                    Debug.Log($"[animales] {Time.time:0.00} s: {dead.Id} walks");
                    await units.MoveAlong(dead.Id, path, 1);
                    Debug.Log($"[animales] {Time.time:0.00} s: {dead.Id} there");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// BASTION_CUERPO=x,y (or 1: two tiles south-east of the animal): one of the map's dead lying on the open tile nearest
        /// there, a body to eat at (Arturo's play-test of 14/11: the dog, the fox, the wolf and the crows go to the bodies of
        /// the dead put down), drawn in play mode (UnitViews.Sync); the map's other units gone. Where it lies, or null.
        /// </summary>
        static Vector3? Body(Transform near)
        {
            string spec = Environment.GetEnvironmentVariable("BASTION_CUERPO");
            var state = Bastion.Client.NightLights.StateForLab;
            if (spec == null || state == null) return null;
            var corpse = state.Units.FirstOrDefault(u => u.Alive && u.Team == Bastion.Engine.Teams.Alien && Bastion.Client.Units.Gaits.MovesDead(u.Template));
            if (corpse == null)
            {
                Debug.LogWarning("[animales] no dead on the map for a body");
                return null;
            }
            var at = MapSpace.WorldToTile(near.position);
            var xy = spec.Split(',');
            int wantX = xy.Length == 2 ? int.Parse(xy[0]) : at.x + 2, wantY = xy.Length == 2 ? int.Parse(xy[1]) : at.y + 2;
            var plan = new BuildingPlan(state);
            bool Open(int x, int y) => x >= 1 && y >= 1 && x < state.Width - 1 && y < state.Height - 1 && Bastion.Engine.Grid.IsWalkable(state.Tiles[y * state.Width + x]) && plan.Owner[y * state.Width + x] < 0;
            // (In the open, where the camera sees it: two tiles clear round it, no crown nor wall over it.)
            bool Clear(int x, int y)
            {
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        if (!Open(x + dx, y + dy)) return false;
                return true;
            }
            var spot = Enumerable.Range(0, state.Width * state.Height).Select(i => (x: i % state.Width, y: i / state.Width)).Where(p => Open(p.x, p.y))
                .OrderBy(p => Clear(p.x, p.y) ? 0 : 1).ThenBy(p => (p.x - wantX) * (p.x - wantX) + (p.y - wantY) * (p.y - wantY)).First();
            corpse.Pos = new Bastion.Engine.Pos { X = spot.x, Y = spot.y, Z = 0 };
            corpse.Alive = false;
            corpse.Hp = 0;
            state.Units = new System.Collections.Generic.List<Bastion.Engine.Unit> { corpse };
            var units = new GameObject("Unidades").AddComponent<Bastion.Client.Units.UnitViews>();
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                units.Sync(state);
                units.Reveal(corpse.Id);
            };
            Debug.Log($"[animales] a body ({corpse.Id}, {corpse.Template}) on {spot.x},{spot.y}");
            return MapSpace.World(spot.x, spot.y);
        }

        static void Show(string kind, string breed = null)
        {
            // (BASTION_NOCHE=1: the map by night.)
            if (Environment.GetEnvironmentVariable("BASTION_NOCHE") == "1") MapLab.Night();
            else MapLab.Day();
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            // (The state MapLab built, a generated one too: BASTION_GENERAR.)
            var state = Bastion.Client.NightLights.StateForLab ?? MapLab.Golden(name);
            // (The map makes its own, as the game's does: MapBuilder.)
            var fauna = Fauna.Current;
            if (fauna == null)
            {
                Debug.LogWarning("[animales] the map made no animals");
                return;
            }
            // (The one nearest the map's middle: one by the edge runs off it at once.)
            var middle = MapSpace.World(state.Width / 2, state.Height / 2);
            // (BASTION_ANIMAL=k: the k-th so, not the first.)
            int skip = (int)Number("BASTION_ANIMAL", 0);
            var first = fauna.Animals.Where(a => a.kind == kind && (breed == null || a.root.name.StartsWith(breed))).OrderBy(a => Vector3.Distance(a.root.position, middle)).Skip(skip).FirstOrDefault().root;
            if (first == null)
            {
                Debug.LogWarning($"[animales] no {kind} on {name}");
                return;
            }
            // (BASTION_EN=x,y: put there, on a tile the camera sees, past the woods that hide where it was.)
            if (Environment.GetEnvironmentVariable("BASTION_EN")?.Split(',') is string[] en && en.Length == 2)
                first.position = MapSpace.World(int.Parse(en[0]), int.Parse(en[1]));
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            var body = Body(first);
            // (BASTION_SONDA_TODOS=1: every animal's place and turn each frame, [animal] k kind name t x z yaw: where the
            // way a cluttered street gives them is seen, not only the first one's in the open.)
            if (Environment.GetEnvironmentVariable("BASTION_SONDA_TODOS") == "1")
            {
                var all = fauna.Animals.Where(a => a.kind != "Crow" && a.kind != "Pigeon").Select(a => a.root).ToList();
                float clock = 0;
                var before = Fauna.Stepping;
                Fauna.Stepping = dt =>
                {
                    before?.Invoke(dt);
                    clock += dt;
                    for (int k = 0; k < all.Count; k++)
                    {
                        var p = all[k].position;
                        if (all[k].gameObject.activeInHierarchy) Debug.Log(FormattableString.Invariant($"[animal] {k} {all[k].name} {clock:0.000} {p.x:0.0000} {p.z:0.0000} {all[k].eulerAngles.y:0.00}"));
                    }
                };
            }
            // (BASTION_SONDA_HUESOS=1: its bones as the animator leaves them, each frame it is drawn (after the animator, not
            // as Fauna moves it): its place and turn, its hips, its skin's root bone and the middle of all its bones, in the
            // world, and the clip it plays, how far through and its pace parameter. [hueso] t x z yaw | hip | root | middle | clip
            // ntime param: whether its body goes on ahead within the clip and back as it starts again (Gameplay, 7/11).)
            if (Environment.GetEnvironmentVariable("BASTION_SONDA_HUESOS") == "1")
            {
                var skin = first.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null).OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault();
                var bones = skin != null ? skin.bones.Where(b => b != null).ToArray() : new Transform[0];
                var hip = first.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.IndexOf("hip", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("pelvis", StringComparison.OrdinalIgnoreCase) >= 0) ?? skin?.rootBone;
                var animator = first.GetComponentInChildren<Animator>();
                Debug.Log($"[hueso] {first.name}: skin {skin?.name}, {bones.Length} bones, hip {hip?.name}, root bone {skin?.rootBone?.name}, root motion {animator?.applyRootMotion}");
                int drawn = -1;
                float began = -1;
                UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += (context, cam) =>
                {
                    if (!Application.isPlaying || first == null || Time.frameCount == drawn) return;
                    drawn = Time.frameCount;
                    if (began < 0) began = Time.time;
                    var p = first.position;
                    string At(Vector3 v) => FormattableString.Invariant($"{v.x:0.0000} {v.y:0.0000} {v.z:0.0000}");
                    // (SIMPLE's models have their bones optimised away: what the animator itself is on, and the skin's bounds.)
                    var model = animator != null ? animator.transform.position : p;
                    var middle = bones.Length > 0 ? bones.Aggregate(Vector3.zero, (s, b) => s + b.position) / bones.Length : model;
                    string clip = "-";
                    float ntime = 0, param = -1;
                    if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                    {
                        var clips = animator.GetCurrentAnimatorClipInfo(0);
                        clip = clips.Length > 0 ? clips.OrderByDescending(c => c.weight).First().clip.name.Replace(' ', '_') : "-";
                        ntime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        string pace = animator.parameters.Any(q => q.name == "Movement_f") ? "Movement_f" : animator.parameters.Any(q => q.name == "Speed_f") ? "Speed_f" : null;
                        if (pace != null) param = animator.GetFloat(pace);
                    }
                    Debug.Log(FormattableString.Invariant($"[hueso] {Time.time - began:0.0000} {p.x:0.0000} {p.z:0.0000} {first.eulerAngles.y:0.00} | {At(hip != null ? hip.position : model)} | {At(skin?.rootBone != null ? skin.rootBone.position : skin != null ? skin.bounds.center : p)} | {At(middle)} | {clip} {ntime:0.0000} {param:0.000} | {Low()}"));
                };
                // (Its lowest bone, a foot on the ground: which and where, to see whether it slides as it stands on it.)
                string Low()
                {
                    int k = -1;
                    for (int i = 0; i < bones.Length; i++)
                        if (k < 0 || bones[i].position.y < bones[k].position.y) k = i;
                    return k < 0 ? "-1 0 0 0" : FormattableString.Invariant($"{k} {bones[k].position.x:0.0000} {bones[k].position.y:0.0000} {bones[k].position.z:0.0000}");
                }
            }
            // (BASTION_SONDA_PERRO=1: its place, turn and animator each frame, [perro] t x z yaw speed state time param
            // transition | animator's speed, its pace parameter's name, its clip, whether a camera draws it (Renderer.isVisible)
            // and its culling: the 3D review's A-29, 10/11, the wild ones sliding with their legs still.)
            if (Environment.GetEnvironmentVariable("BASTION_SONDA_PERRO") == "1")
            {
                var animator = first.GetComponentInChildren<Animator>();
                var drawn = first.GetComponentsInChildren<Renderer>(true);
                var last = first.position;
                float clock = 0;
                var before = Fauna.Stepping;
                Fauna.Stepping = dt =>
                {
                    before?.Invoke(dt);
                    clock += dt;
                    var p = first.position;
                    float speed = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(last.x, 0, last.z)) / Mathf.Max(dt, 1e-4f);
                    last = p;
                    bool on = animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
                    var info = on ? animator.GetCurrentAnimatorStateInfo(0) : default;
                    string name = on ? animator.parameters.Any(q => q.name == "Movement_f") ? "Movement_f" : animator.parameters.Any(q => q.name == "Speed_f") ? "Speed_f" : "-" : "-";
                    float pace = name != "-" ? animator.GetFloat(name) : -1;
                    var clips = on ? animator.GetCurrentAnimatorClipInfo(0) : new AnimatorClipInfo[0];
                    string clip = clips.Length > 0 ? string.Join("+", clips.OrderByDescending(c => c.weight).Select(c => FormattableString.Invariant($"{c.clip.name.Replace(' ', '_')}:{c.weight:0.00}"))) : "-";
                    bool visible = drawn.Any(r => r != null && r.isVisible);
                    Debug.Log(FormattableString.Invariant($"[perro] {clock:0.000} {p.x:0.000} {p.z:0.000} {first.eulerAngles.y:0.0} {speed:0.00} {info.shortNameHash} {info.normalizedTime:0.000} {pace:0.00} {(on && animator.IsInTransition(0) ? 1 : 0)} | speed {(animator != null ? animator.speed : 0):0.00} {name} {clip} visible {(visible ? 1 : 0)} culling {(animator != null ? animator.cullingMode.ToString() : "-")}"));
                };
            }
            // (BASTION_SOLO=1: nobody coming, the camera on the animal: how it stands and wanders.)
            if (Environment.GetEnvironmentVariable("BASTION_SOLO") == "1")
            {
                // (BASTION_MIRAR=x,y: the camera still, on that place (in tiles from the map's corner, decimals too: a tile's
                // middle is .5), not on the animal.)
                var look = Environment.GetEnvironmentVariable("BASTION_MIRAR")?.Split(',');
                // (BASTION_MIRAR=cuerpo: on the body, BASTION_CUERPO's.)
                var at = body is Vector3 lying && Environment.GetEnvironmentVariable("BASTION_MIRAR") == "cuerpo" ? lying : look != null && look.Length == 2 ? new Vector3(float.Parse(look[0], System.Globalization.CultureInfo.InvariantCulture), 0, -float.Parse(look[1], System.Globalization.CultureInfo.InvariantCulture)) * MapSpace.Tile : first.position;
                MapSpace.Aim(camera, at, Number("BASTION_DIST", 12), Number("BASTION_GIRO", 45));
                Debug.Log($"[animales] {kind} {first.name} alone, at {MapSpace.WorldToTile(first.position)}");
                // (BASTION_SEGUIR=1: the camera on it wherever it wanders.)
                if (Environment.GetEnvironmentVariable("BASTION_SEGUIR") == "1")
                {
                    Camera alone = null;
                    var stepping = Fauna.Stepping;
                    Fauna.Stepping = dt =>
                    {
                        stepping?.Invoke(dt);
                        if ((alone ??= GameObject.Find("Shot camera")?.GetComponent<Camera>()) != null)
                            MapSpace.Aim(alone, first.position, Number("BASTION_DIST", 12), Number("BASTION_GIRO", 45));
                    };
                }
                return;
            }
            // The stand-in: from so many tiles off (to the south-east, the camera's side) towards the animal, at 1.4 m/s.
            var post = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            post.name = "Persona";
            UnityEngine.Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            post.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.55f, 0.55f, 0.6f) };
            var goal = new Vector3(first.position.x, 0, first.position.z);
            var start = goal + new Vector3(1, 0, -1).normalized * Number("BASTION_DESDE", 9) * MapSpace.Tile;
            float walked = 0, delay = 1;
            void Place() => post.transform.position = Vector3.MoveTowards(start, goal, Mathf.Max(0, walked - delay) * 1.4f) + Vector3.up * 0.9f;
            Place();
            Fauna.ExtraThreats = () => new[] { post.transform.position };
            // (BASTION_SEGUIR=1: the camera following the animal as it runs, the stand-in in view too.)
            bool follow = Environment.GetEnvironmentVariable("BASTION_SEGUIR") == "1";
            Camera shot = null;
            var probe = Fauna.Stepping;
            Fauna.Stepping = dt =>
            {
                probe?.Invoke(dt);
                walked += dt;
                Place();
                if (follow && (shot ??= GameObject.Find("Shot camera")?.GetComponent<Camera>()) != null)
                    MapSpace.Aim(shot, Vector3.Lerp(first.position, post.transform.position - Vector3.up * 0.9f, Number("BASTION_MIRA", 0.3f)), Number("BASTION_DIST", 12), Number("BASTION_GIRO", 45));
            };
            // (On the animal, at its height, the stand-in coming in from the bottom of the picture.)
            // (BASTION_MIRA: how far towards the stand-in's start the camera looks, 0.3.)
            MapSpace.Aim(camera, Vector3.Lerp(first.position, start, Number("BASTION_MIRA", 0.3f)), Number("BASTION_DIST", 12), Number("BASTION_GIRO", 45));
            var tile = MapSpace.WorldToTile(first.position);
            Debug.Log(FormattableString.Invariant($"[animales] {kind} {first.name} on {tile.x},{tile.y} at {first.position.y:0.00} m; ") + string.Join(", ", fauna.Animals.Select(a => $"{a.kind} {MapSpace.WorldToTile(a.root.position)}")));
        }
    }
}
