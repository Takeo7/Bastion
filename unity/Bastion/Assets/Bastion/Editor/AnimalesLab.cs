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

        static float Number(string name, float otherwise) =>
            float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : otherwise;

        static void Show(string kind, string breed = null)
        {
            MapLab.Day();
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            var state = MapLab.Golden(name);
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
            // (BASTION_SONDA_PERRO=1: its place, turn and animator each frame, [perro] t x z yaw speed state time param.)
            if (Environment.GetEnvironmentVariable("BASTION_SONDA_PERRO") == "1")
            {
                var animator = first.GetComponentInChildren<Animator>();
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
                    var info = animator != null && animator.isActiveAndEnabled ? animator.GetCurrentAnimatorStateInfo(0) : default;
                    float pace = animator != null && animator.isActiveAndEnabled && animator.parameters.Any(q => q.name == "Movement_f") ? animator.GetFloat("Movement_f") : -1;
                    Debug.Log(FormattableString.Invariant($"[perro] {clock:0.000} {p.x:0.000} {p.z:0.000} {first.eulerAngles.y:0.0} {speed:0.00} {info.shortNameHash} {info.normalizedTime:0.000} {pace:0.00} {(animator != null && animator.IsInTransition(0) ? 1 : 0)}"));
                };
            }
            // (BASTION_SOLO=1: nobody coming, the camera on the animal: how it stands and wanders.)
            if (Environment.GetEnvironmentVariable("BASTION_SOLO") == "1")
            {
                MapSpace.Aim(camera, first.position, Number("BASTION_DIST", 12), Number("BASTION_GIRO", 45));
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
