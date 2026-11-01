using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The animals about a map (Gameplay, 1/11: «más vida»; only on the screen, the rules untouched): stray dogs in a
    /// town's streets and yards, a fox or a coyote out in the country, crows on the roofs and pigeons pecking about a
    /// square. They stand, sniff and wander a few tiles; when anyone comes near (a soldier, one of the dead: every
    /// figure UnitViews draws, and the lab's ExtraThreats) the dogs and the wild ones run off and the birds take wing;
    /// once far enough they settle again, or are gone past the map's edge. Where they start is the map's own (its tiles,
    /// hashed), the same for both players; how they move, the frame's.
    /// </summary>
    public sealed class Fauna : MonoBehaviour, IShotStep
    {
        public static Fauna Current { get; private set; }

        /// <summary>For a lab: more places to run from than the figures (a stand-in walking by).</summary>
        public static Func<IEnumerable<Vector3>> ExtraThreats;
        /// <summary>For a lab: called with each step's dt before the animals move (to walk the stand-in).</summary>
        public static Action<float> Stepping;

        enum Kind { Dog, Wild, Crow, Pigeon }
        enum State { Idle, Walk, Flee, Fly }

        sealed class Animal
        {
            public Kind kind;
            public Transform root;
            public Animator animator;
            public Transform body, left, right;
            public State state;
            public Vector3 target, velocity;
            public float wait, clock, flap, yaw;
            public uint rng;
            public bool gone;
            /// <summary>What draws it, and whether it is drawn (only in the squad's sight: Fog).</summary>
            public Renderer[] renderers;
            public bool shown = true;
            /// <summary>On a building's roof (seen as its roof is, Fog.InSight's roof).</summary>
            public bool onRoof;
        }

        /// <summary>Strays of the Dog Pack (no collars), and the wild ones of the country.</summary>
        public static readonly string[] Strays =
        {
            "SK_Dog_Labrador_01", "SK_Dog_GermanShepherd_01", "SK_Dog_Husky_01", "SK_Dog_Doberman_01", "SK_Dog_GoldenRetrieve_01",
            "SK_Dog_Pointer_01", "SK_Dog_Ridgeback_01", "SK_Dog_Greyhound_01", "SK_Dog_Shiba_01", "SK_Dog_Dalmatian_01",
        };
        public static readonly string[] Wild = { "SK_Dog_Fox_01", "SK_Dog_Coyote_01" };
        /// <summary>The Dog Pack's key (MapArt.ModelKey's pack), and every model of it the map may ask for (MapCatalogMake puts them in the game's catalog).</summary>
        public const string Pack = "dog";
        public static IEnumerable<string> Models => Strays.Concat(Wild).Select(n => $"{Pack}:{n}");

        /// <summary>Walking and running (m/s), how near anyone scares them and how far they run (tiles), how far they wander.</summary>
        const float DogWalk = 1.1f, DogRun = 6.5f, WildWalk = 1.0f, WildRun = 7.5f;
        const float DogFear = 6, WildFear = 9, BirdFear = 4.5f, Calm = 14, Wander = 5;
        /// <summary>A bird's take-off: away and up (m/s), and how long before it is gone (s).</summary>
        const float FlyAway = 6.5f, FlyUp = 3.2f, FlyFor = 5;

        readonly List<Animal> animals = new List<Animal>();
        readonly List<Vector3> threats = new List<Vector3>();
        GameState s;
        BuildingPlan plan;
        float sinceLook;

        void OnEnable() => Current = this;

        /// <summary>The animals there are and where (a lab's framing): their kind (Dog, Wild, Crow, Pigeon) and their root.</summary>
        public IEnumerable<(string kind, Transform root)> Animals => animals.Where(a => !a.gone).Select(a => (a.kind.ToString(), a.root));

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// The animals of map `s` (its buildings as `plan` has them, their tops set by BuildingSurfaces), under `parent`;
        /// the dogs' prefabs by name from `dogOf` (none drawn if it has none).
        /// </summary>
        public static Fauna Make(Transform parent, GameState s, BuildingPlan plan, Func<string, GameObject> dogOf)
        {
            var fauna = new GameObject("Fauna").AddComponent<Fauna>();
            // (Set here too: in the editor, out of play, OnEnable isn't called.)
            Current = fauna;
            fauna.transform.SetParent(parent, false);
            fauna.s = s;
            fauna.plan = plan;
            fauna.Populate(dogOf);
            return fauna;
        }

        static float Hash(int x, int y, int salt) => (float)BuildingPlan.Hash(x, y, salt);

        bool Outdoors(int x, int y)
        {
            if (x < 1 || y < 1 || x >= s.Width - 1 || y >= s.Height - 1) return false;
            int i = y * s.Width + x;
            int ground = s.Ground != null && i < s.Ground.Count ? s.Ground[i] : 0;
            int elev = s.Elev != null && i < s.Elev.Count ? s.Elev[i] : 0;
            return Bastion.Engine.Grid.IsWalkable(s.Tiles[i]) && plan.Owner[i] < 0 && ground != 2 && ground != 4 && elev == 0;
        }

        /// <summary>Where they start: away from the squad and the dead, apart from each other, as the map's tiles hash.</summary>
        void Populate(Func<string, GameObject> dogOf)
        {
            var units = s.Units.Where(u => u.Alive).ToList();
            bool Clear(int x, int y, int squad, int dead) =>
                units.All(u => Math.Max(Math.Abs(u.Pos.X - x), Math.Abs(u.Pos.Y - y)) >= (u.Team == "xcom" ? squad : dead));
            var open = new List<(int x, int y)>();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width; x++) if (Outdoors(x, y)) open.Add((x, y));
            var taken = new List<(int x, int y)>();
            IEnumerable<(int x, int y)> Spots(int salt, int squad, int dead, int apart) =>
                open.OrderBy(p => Hash(p.x, p.y, salt)).Where(p => Clear(p.x, p.y, squad, dead) && taken.All(t => Math.Max(Math.Abs(t.x - p.x), Math.Abs(t.y - p.y)) >= apart));
            bool town = s.Biome == Biomes.City;
            // Dogs in a town (three), a farm dog out in the country; the fox and the coyote in the country.
            int dogs = town ? 3 : s.Biome == Biomes.Wilds ? 1 : 2;
            foreach (var p in Spots(70, 12, 4, 8).Take(dogs).ToList())
            {
                taken.Add(p);
                AddDog(Strays[(int)(Hash(p.x, p.y, 71) * Strays.Length)], p, Kind.Dog, dogOf);
            }
            // (The wild ones out in the open: three tiles clear of the woods' edge, of walls and of tall cover, where the pines' crowns don't hide them.)
            bool Clearing(int x, int y)
            {
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= s.Width || ny >= s.Height) return false;
                        string t = s.Tiles[ny * s.Width + nx];
                        if (t == TileKinds.Wall || t == TileKinds.High) return false;
                    }
                return true;
            }
            if (s.Biome == Biomes.Wilds)
                foreach (var p in Spots(72, 14, 6, 10).Where(p => Clearing(p.x, p.y)).Take(2).ToList())
                {
                    taken.Add(p);
                    AddDog(Wild[(int)(Hash(p.x, p.y, 73) * Wild.Length)], p, Kind.Wild, dogOf);
                }
            // Crows on the roofs: two or three buildings, two to four on each, along a house's ridge or a flat roof's front edge.
            var roofs = plan.Buildings.Where(b => b.kind != BuildingKinds.Ruin && !b.frame && b.top > 0).OrderBy(b => Hash(b.x0, b.y0, 74)).Take(town ? 3 : 2).ToList();
            foreach (var b in roofs)
            {
                int n = 2 + (int)(Hash(b.x0, b.y0, 75) * 3);
                for (int k = 0; k < n; k++)
                {
                    float along = (k - (n - 1) / 2f) * (0.45f + 0.3f * Hash(b.x0 + k, b.y0, 76));
                    var crow = AddBird(Kind.Crow, RoofSpot(b, along), Hash(b.x0 + k, b.y0, 77) * 360, (uint)(b.index * 31 + k + 1));
                    // (Gone with its roof when the cutaway opens the building, as what stands on roofs.)
                    Cutaway.Current?.Track(crow.gameObject, b.index, b.storeys);
                }
            }
            // Pigeons pecking about an open spot of a town.
            if (town)
                foreach (var p in Spots(78, 10, 5, 6).Take(1).ToList())
                {
                    taken.Add(p);
                    for (int k = 0; k < 5; k++)
                    {
                        var at = MapSpace.World(p.x, p.y) + new Vector3(Hash(p.x, k, 79) - 0.5f, 0, Hash(k, p.y, 80) - 0.5f) * MapSpace.Tile * 1.6f;
                        AddBird(Kind.Pigeon, at, Hash(p.x + k, p.y, 81) * 360, (uint)(p.x * 977 + p.y * 31 + k + 1));
                    }
                }
            Debug.Log($"[fauna] {animals.Count(a => a.kind == Kind.Dog)} dogs, {animals.Count(a => a.kind == Kind.Wild)} wild, {animals.Count(a => a.kind == Kind.Crow)} crows, {animals.Count(a => a.kind == Kind.Pigeon)} pigeons");
        }

        /// <summary>Where a bird stands on building b's roof, `along` metres from its middle: a house's ridge, else its roof's front edge.</summary>
        static Vector3 RoofSpot(Building b, float along)
        {
            bool alongX = b.x1 - b.x0 >= b.y1 - b.y0;
            float cx = (b.x0 + b.x1) / 2f + 0.5f, cz = (b.y0 + b.y1) / 2f + 0.5f;
            float top = b.top - (b.pitched != null ? 0.06f : 0);
            // (Tiles of 0.10.1's frame, z south, to Unity's metres.)
            Vector3 At(float x, float y, float z) => new Vector3(x * MapSpace.Tile, y * MapSpace.Tile, -z * MapSpace.Tile);
            if (b.pitched != null) return alongX ? At(cx + along / MapSpace.Tile, top, cz) : At(cx, top, cz + along / MapSpace.Tile);
            // A flat roof: on its parapet's top, along the south side (the camera's).
            return At(cx + along / MapSpace.Tile, b.top, b.y1 + 0.5f + (float)BuildingSurfaces.WallHalf);
        }

        void AddDog(string prefab, (int x, int y) p, Kind kind, Func<string, GameObject> dogOf)
        {
            var model = dogOf?.Invoke(prefab);
            if (model == null) return;
            var root = new GameObject(prefab).transform;
            root.SetParent(transform, false);
            root.position = MapSpace.World(p.x, p.y);
            float yaw = Hash(p.x, p.y, 82) * 360;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            var made = Instantiate(model, root, false);
            made.name = "modelo";
            var animator = made.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                // (Not moved while no camera sees it, in the game; a lab's shot camera draws into a texture, unseen.)
                animator.cullingMode = Application.isPlaying ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.AlwaysAnimate;
                animator.SetBool("Grounded_b", true);
            }
            foreach (var r in made.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            animals.Add(new Animal { kind = kind, root = root, animator = animator, state = State.Idle, wait = 1 + Hash(p.y, p.x, 83) * 4, yaw = yaw, rng = (uint)(p.x * 7919 + p.y * 104729 + 1), renderers = made.GetComponentsInChildren<Renderer>() });
        }

        Transform AddBird(Kind kind, Vector3 at, float yaw, uint seed)
        {
            var root = new GameObject(kind == Kind.Crow ? "Cuervo" : "Paloma").transform;
            root.SetParent(transform, false);
            root.position = at;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            var (body, left, right) = Bird(root, kind == Kind.Crow ? new Color(0.07f, 0.07f, 0.08f) : new Color(0.42f, 0.44f, 0.48f));
            var tile = MapSpace.WorldToTile(at);
            bool roof = tile.x >= 0 && tile.y >= 0 && tile.x < s.Width && tile.y < s.Height && plan.Owner[tile.y * s.Width + tile.x] >= 0;
            animals.Add(new Animal { kind = kind, root = root, body = body, left = left, right = right, state = State.Idle, wait = 0.5f + (seed % 7) * 0.3f, yaw = yaw, rng = seed * 2654435761u + 1, renderers = root.GetComponentsInChildren<Renderer>(), onRoof = roof });
            return root;
        }

        static Mesh bodyMesh, wingMesh;
        static readonly Dictionary<Color, Material> birdMaterials = new Dictionary<Color, Material>();

        /// <summary>
        /// A bird of our own (low-poly, a crow's size): a body and a head with a beak, a tail, and two wings hinged at the
        /// shoulders (folded while it stands, beating as it flies). Its parts under `root`, facing +z.
        /// </summary>
        static (Transform body, Transform left, Transform right) Bird(Transform root, Color colour)
        {
            bodyMesh ??= BodyMesh();
            wingMesh ??= WingMesh();
            if (!birdMaterials.TryGetValue(colour, out var material))
            {
                // (A copy of Resources' plain Lit: a build has only the shaders its own materials use.)
                material = KitMaterials.New(KitMaterials.Plain, "Pájaro");
                if (material != null)
                {
                    material.color = colour;
                    material.SetFloat("_Smoothness", 0.25f);
                }
                birdMaterials[colour] = material;
            }
            var body = new GameObject("cuerpo").transform;
            body.SetParent(root, false);
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            body.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            Transform Wing(float side)
            {
                var wing = new GameObject(side < 0 ? "ala izquierda" : "ala derecha").transform;
                wing.SetParent(body, false);
                wing.localPosition = new Vector3(0.045f * side, 0.14f, 0.02f);
                wing.localScale = new Vector3(side, 1, 1);
                wing.gameObject.AddComponent<MeshFilter>().sharedMesh = wingMesh;
                wing.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                return wing;
            }
            var left = Wing(-1);
            var right = Wing(1);
            // (A big crow's size, a little over: seen from the game's camera.)
            root.localScale = Vector3.one * 1.6f;
            return (body, left, right);
        }

        /// <summary>The body: a diamond, its head and beak forward, its tail back, flat-shaded (each face its own corners).</summary>
        static Mesh BodyMesh()
        {
            var v = new List<Vector3>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                v.Add(a);
                v.Add(b);
                v.Add(c);
            }
            void Diamond(Vector3 front, Vector3 back, Vector3 top, Vector3 bottom, Vector3 left, Vector3 right)
            {
                foreach (var (a, b) in new[] { (top, right), (right, bottom), (bottom, left), (left, top) })
                {
                    Tri(front, b, a);
                    Tri(back, a, b);
                }
            }
            Diamond(new Vector3(0, 0.12f, 0.11f), new Vector3(0, 0.11f, -0.12f), new Vector3(0, 0.17f, 0), new Vector3(0, 0.05f, 0), new Vector3(-0.055f, 0.11f, 0), new Vector3(0.055f, 0.11f, 0));
            // The head and its beak.
            Diamond(new Vector3(0, 0.165f, 0.21f), new Vector3(0, 0.17f, 0.09f), new Vector3(0, 0.205f, 0.13f), new Vector3(0, 0.14f, 0.13f), new Vector3(-0.035f, 0.17f, 0.13f), new Vector3(0.035f, 0.17f, 0.13f));
            // The tail, both faces.
            Tri(new Vector3(-0.04f, 0.12f, -0.1f), new Vector3(0.04f, 0.12f, -0.1f), new Vector3(0, 0.1f, -0.24f));
            Tri(new Vector3(0.04f, 0.12f, -0.1f), new Vector3(-0.04f, 0.12f, -0.1f), new Vector3(0, 0.1f, -0.24f));
            // Legs: two thin wedges down to the feet.
            foreach (float side in new[] { -0.025f, 0.025f })
            {
                Tri(new Vector3(side - 0.01f, 0.07f, 0), new Vector3(side + 0.01f, 0.07f, 0), new Vector3(side, 0, 0.01f));
                Tri(new Vector3(side + 0.01f, 0.07f, 0), new Vector3(side - 0.01f, 0.07f, 0), new Vector3(side, 0, 0.01f));
            }
            var mesh = new Mesh { name = "Pájaro" };
            mesh.SetVertices(v);
            mesh.SetTriangles(Enumerable.Range(0, v.Count).ToArray(), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A wing, out along +x from its hinge (both faces): broad at the shoulder, to a point at its tip.</summary>
        static Mesh WingMesh()
        {
            var v = new[] { new Vector3(0, 0, 0.05f), new Vector3(0, 0, -0.07f), new Vector3(0.14f, 0, -0.06f), new Vector3(0.26f, 0, -0.1f), new Vector3(0.13f, 0, 0.03f) };
            var mesh = new Mesh { name = "Ala" };
            mesh.SetVertices(v.Concat(v).ToList());
            mesh.SetTriangles(new[] { 0, 4, 1, 1, 4, 2, 2, 4, 3, 5, 6, 9, 6, 7, 9, 7, 8, 9 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update() => Step(Time.deltaTime);

        public void ShotStep(float dt) => Step(dt);

        uint Next(Animal a)
        {
            a.rng ^= a.rng << 13;
            a.rng ^= a.rng >> 17;
            a.rng ^= a.rng << 5;
            return a.rng;
        }

        float Random01(Animal a) => (Next(a) & 0xffffff) / (float)0x1000000;

        /// <summary>Everyone the animals keep away from: every figure drawn (the living and the dead), and the lab's.</summary>
        void Look()
        {
            threats.Clear();
            if (Units.UnitViews.Current != null) foreach (Transform t in Units.UnitViews.Current.transform) threats.Add(t.position);
            if (ExtraThreats != null) threats.AddRange(ExtraThreats());
        }

        (Vector3 at, float distance) Nearest(Vector3 from)
        {
            Vector3 best = from;
            float least = float.MaxValue;
            foreach (var t in threats)
            {
                float d = Vector2.Distance(new Vector2(t.x, t.z), new Vector2(from.x, from.z));
                if (d < least)
                {
                    least = d;
                    best = t;
                }
            }
            return (best, least);
        }

        void Step(float dt)
        {
            if (dt <= 0 || s == null) return;
            Stepping?.Invoke(dt);
            sinceLook += dt;
            if (sinceLook >= 0.2f || threats.Count == 0)
            {
                sinceLook = 0;
                Look();
            }
            var fog = Fog.Current;
            foreach (var a in animals)
            {
                if (a.gone) continue;
                a.clock += dt;
                if (a.kind == Kind.Crow || a.kind == Kind.Pigeon) StepBird(a, dt);
                else StepDog(a, dt);
                // Only where the squad looks, as the enemies (Arturo, 1/11); everywhere with no fog shown (a lab's map).
                var t = MapSpace.WorldToTile(a.root.position);
                bool shown = fog == null || fog.InSight(t.x, t.y, a.onRoof && a.state != State.Fly);
                if (shown != a.shown)
                {
                    a.shown = shown;
                    foreach (var r in a.renderers) if (r != null) r.enabled = shown;
                }
            }
        }

        /// <summary>A free outdoor tile some tiles from `from` (a wander's end), or `from` if none found.</summary>
        Vector3 Somewhere(Animal a, Vector3 from, float reach)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                float angle = Random01(a) * Mathf.PI * 2, d = (0.4f + 0.6f * Random01(a)) * reach * MapSpace.Tile;
                var to = from + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * d;
                var t = MapSpace.WorldToTile(to);
                if (Outdoors(t.x, t.y) && Clear(from, to)) return MapSpace.World(t.x, t.y) + new Vector3(Random01(a) - 0.5f, 0, Random01(a) - 0.5f) * MapSpace.Tile * 0.6f;
            }
            return from;
        }

        /// <summary>Whether the way from `a` to `b` stays outdoors (sampled every half tile).</summary>
        bool Clear(Vector3 a, Vector3 b)
        {
            int n = Mathf.CeilToInt(Vector3.Distance(a, b) / (MapSpace.Tile * 0.5f));
            for (int k = 1; k <= n; k++)
            {
                var t = MapSpace.WorldToTile(Vector3.Lerp(a, b, k / (float)n));
                if (!Outdoors(t.x, t.y)) return false;
            }
            return true;
        }

        /// <summary>Where to run from `threat`: the farthest outdoor spot it can reach straight, away from it (or to the side).</summary>
        Vector3 Away(Animal a, Vector3 threat)
        {
            var from = a.root.position;
            var away = from - threat;
            away.y = 0;
            if (away.sqrMagnitude < 1e-4f) away = a.root.forward;
            away.Normalize();
            Vector3 best = from;
            float bestScore = -1;
            foreach (float turn in new[] { 0f, 30, -30, 60, -60, 90, -90 })
            {
                var dir = Quaternion.Euler(0, turn, 0) * away;
                for (float d = Calm * MapSpace.Tile; d >= 2 * MapSpace.Tile; d -= MapSpace.Tile)
                {
                    var to = from + dir * d;
                    var t = MapSpace.WorldToTile(to);
                    // (Off the map's edge is a way out: there it is gone.)
                    bool off = t.x < 1 || t.y < 1 || t.x >= s.Width - 1 || t.y >= s.Height - 1;
                    if ((off || Outdoors(t.x, t.y)) && Clear(from, off ? from + dir * (d - 2 * MapSpace.Tile) : to))
                    {
                        float score = d * Mathf.Cos(turn * Mathf.Deg2Rad);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = to;
                        }
                        break;
                    }
                }
            }
            return best;
        }

        void StepDog(Animal a, float dt)
        {
            var (threat, distance) = Nearest(a.root.position);
            float fear = (a.kind == Kind.Wild ? WildFear : DogFear) * MapSpace.Tile;
            if (distance < fear && a.state != State.Flee)
            {
                a.state = State.Flee;
                a.target = Away(a, threat);
            }
            float speed = a.state == State.Flee ? (a.kind == Kind.Wild ? WildRun : DogRun) : a.state == State.Walk ? (a.kind == Kind.Wild ? WildWalk : DogWalk) : 0;
            if (a.state == State.Idle)
            {
                a.wait -= dt;
                if (a.wait <= 0)
                {
                    a.target = Somewhere(a, a.root.position, Wander);
                    a.state = State.Walk;
                }
            }
            else
            {
                var to = a.target - a.root.position;
                to.y = 0;
                float left = to.magnitude;
                if (left < 0.15f)
                {
                    // Fled past the map's edge: gone; else (calm far enough, or a wander's end) it stands a while.
                    var t = MapSpace.WorldToTile(a.root.position);
                    if (t.x < 1 || t.y < 1 || t.x >= s.Width - 1 || t.y >= s.Height - 1)
                    {
                        a.gone = true;
                        a.root.gameObject.SetActive(false);
                        return;
                    }
                    a.state = distance < fear ? State.Flee : State.Idle;
                    if (a.state == State.Flee) a.target = Away(a, threat);
                    a.wait = 2 + Random01(a) * 5;
                }
                else
                {
                    float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    a.yaw = Mathf.MoveTowardsAngle(a.yaw, want, (a.state == State.Flee ? 540 : 200) * dt);
                    a.root.rotation = Quaternion.Euler(0, a.yaw, 0);
                    // (Turning on the spot first when it faces away.)
                    float facing = Mathf.Clamp01(Mathf.Cos(Mathf.DeltaAngle(a.yaw, want) * Mathf.Deg2Rad));
                    a.root.position += to / left * Mathf.Min(left, speed * facing * dt);
                }
            }
            if (a.animator != null)
            {
                float movement = a.state == State.Flee ? 1 : a.state == State.Walk ? 0.5f : 0;
                a.animator.SetFloat("Movement_f", Mathf.MoveTowards(a.animator.GetFloat("Movement_f"), movement, 3 * dt));
            }
        }

        void StepBird(Animal a, float dt)
        {
            if (a.state == State.Fly)
            {
                // Up and away, beating hard, then gone.
                a.velocity += new Vector3(a.velocity.x, 0, a.velocity.z).normalized * 2 * dt;
                a.root.position += a.velocity * dt;
                a.flap += dt * 11;
                float beat = Mathf.Sin(a.flap * Mathf.PI * 2) * 55;
                a.left.localRotation = Quaternion.Euler(0, 0, beat);
                a.right.localRotation = Quaternion.Euler(0, 0, beat);
                a.body.localRotation = Quaternion.Euler(-12, 0, 0);
                if (a.clock > FlyFor)
                {
                    a.gone = true;
                    a.root.gameObject.SetActive(false);
                }
                return;
            }
            var (threat, distance) = Nearest(a.root.position);
            if (distance < BirdFear * MapSpace.Tile)
            {
                var away = a.root.position - threat;
                away.y = 0;
                away = away.sqrMagnitude < 1e-4f ? a.root.forward : away.normalized;
                // (Each its own way off, a little.)
                away = Quaternion.Euler(0, (Random01(a) - 0.5f) * 70, 0) * away;
                a.velocity = away * FlyAway + Vector3.up * FlyUp;
                a.yaw = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                a.root.rotation = Quaternion.Euler(0, a.yaw, 0);
                a.state = State.Fly;
                a.clock = Random01(a) * 0.15f;
                return;
            }
            // Standing: the wings folded along the back; now and then it turns a little, or bobs (a pigeon pecks and hops).
            a.left.localRotation = Quaternion.Euler(0, 75, -8);
            a.right.localRotation = Quaternion.Euler(0, 75, -8);
            a.wait -= dt;
            if (a.wait <= 0)
            {
                a.wait = 0.6f + Random01(a) * 2.2f;
                a.yaw += (Random01(a) - 0.5f) * 70;
                if (a.kind == Kind.Pigeon && Random01(a) < 0.4f) a.root.position += Quaternion.Euler(0, a.yaw, 0) * Vector3.forward * 0.12f;
                a.root.rotation = Quaternion.Euler(0, a.yaw, 0);
            }
            float peck = a.kind == Kind.Pigeon ? Mathf.Max(0, Mathf.Sin(a.clock * 3.1f + a.rng % 5)) * 35 : Mathf.Sin(a.clock * 1.7f + a.rng % 5) * 6;
            a.body.localRotation = Quaternion.Euler(peck, 0, 0);
        }
    }
}
