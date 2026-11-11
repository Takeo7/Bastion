using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The animals about a map (Gameplay, 1/11: «más vida»; only on the screen, the rules untouched): a stray dog in a
    /// town's streets and a cat or two by its walls (Ana's #11, 1/11: one dog, not three; SIMPLE - Cats', the cat Arturo
    /// allowed, off before the dog), a fox or a wolf out in the country (Arturo's #7 of 1/11: no dogs there; Lore: no coyotes in
    /// Europe, so none in the Pindo) with a stag and a doe, a boar and rabbits (SIMPLE - Forest Animals', the one SIMPLE
    /// Arturo allowed, 1/11: POLYGON has none; grazing now and then, the rabbit hopping and off first), crows
    /// on the roofs and pigeons pecking about a square; at the Bastion's base, one or two wild ones in the pinar round it
    /// and nothing in its clearing. They stand (now and then sniffing, wagging, shaking, yawning, digging, howling, or
    /// sitting or lying a while: #3, FaunaMake's controller), and wander a few tiles; when anyone comes near (a soldier,
    /// one of the dead: every figure UnitViews draws, and the lab's ExtraThreats) the dogs and the wild ones get up and
    /// run off and the birds take wing; once far enough they settle again, or are gone past the map's edge. Where they
    /// start is the map's own (its tiles, hashed), the same for both players; how they move, the frame's.
    /// </summary>
    public sealed class Fauna : MonoBehaviour, IShotStep
    {
        public static Fauna Current { get; private set; }

        /// <summary>For a lab: more places to run from than the figures (a stand-in walking by).</summary>
        public static Func<IEnumerable<Vector3>> ExtraThreats;
        /// <summary>For a lab: called with each step's dt before the animals move (to walk the stand-in).</summary>
        public static Action<float> Stepping;

        enum Kind { Dog, Wild, Grazer, Cat, Crow, Pigeon }
        enum State { Idle, Walk, Flee, Fly }

        sealed class Animal
        {
            public Kind kind;
            /// <summary>Its model's name (which gestures are its own: a wolf howls, a fox digs).</summary>
            public string breed;
            /// <summary>What it does while it stands (Fauna.Gestures' Accion, Sit, Lie; 0 nothing), chosen as it stops.</summary>
            public int doing;
            public bool chosen;
            public Transform root;
            public Animator animator;
            /// <summary>What its clips walk it by (FaunaBody, on its animator: none on a bird), and its animator's speed for its legs to keep up with its pace (Stride).</summary>
            public FaunaBody stride;
            public float legs = 1;
            public Transform body, left, right;
            public State state;
            public Vector3 target, velocity;
            /// <summary>The way to its target, the next point first (Route), and how fast it goes over the ground now (m/s).</summary>
            public List<Vector3> way = new List<Vector3>();
            public float pace;
            /// <summary>How fast it turned this step (degrees a second), and its legs' step round for it (Movement_f, eased in and out).</summary>
            public float turning, stepping;
            /// <summary>
            /// The curve it is going round a corner of its way on (RoundCorner): from where it began it, by the corner
            /// (way[0], of `bendWay`), to where it leaves it; how far along (0 to 1) and how long it is (m).
            /// </summary>
            public bool bending;
            public Vector3 bendFrom, bendAt, bendTo;
            public float bendDone, bendLength;
            public List<Vector3> bendWay;
            public float wait, clock, flap, yaw;
            public uint rng;
            public bool gone;
            /// <summary>What draws it, and whether it is drawn (only in the squad's sight: Fog).</summary>
            public Renderer[] renderers;
            public bool shown = true;
            /// <summary>On a building's roof (seen as its roof is, Fog.InSight's roof).</summary>
            public bool onRoof;
            /// <summary>Its place among them (who gives way to whom: A-30), and how long it has waited for one in its way (s).</summary>
            public int id;
            public float held;
        }

        /// <summary>Strays of the Dog Pack (no collars), and the wild ones of the country.</summary>
        public static readonly string[] Strays =
        {
            "SK_Dog_Labrador_01", "SK_Dog_GermanShepherd_01", "SK_Dog_Husky_01", "SK_Dog_Doberman_01", "SK_Dog_GoldenRetrieve_01",
            "SK_Dog_Pointer_01", "SK_Dog_Ridgeback_01", "SK_Dog_Greyhound_01", "SK_Dog_Shiba_01", "SK_Dog_Dalmatian_01",
        };
        public static readonly string[] Wild = { "SK_Dog_Fox_01", "SK_Dog_Wolf_01" };
        /// <summary>SIMPLE - Forest Animals' stags, does, boars and rabbits (each in its colours).</summary>
        static readonly string[] Stags = { "Stag_01", "Stag_02", "Stag_03" }, Does = { "Doe_01", "Doe_02" }, Boars = { "Boar_01", "Boar_02", "Boar_03" }, Rabbits = { "Rabbit_01", "Rabbit_02", "Rabbit_03" };
        /// <summary>SIMPLE - Cats' street cats: the tabbies, grey and ginger, and the grey Russian (not its sphynx, Persians or Siamese).</summary>
        static readonly string[] Cats =
        {
            "SimpleCats_Tabby_01", "SimpleCats_Tabby_02", "SimpleCats_Tabby_03", "SimpleCats_StripedTabby_01", "SimpleCats_StripedTabby_02",
            "SimpleCats_StripedTabby_03", "SimpleCats_RussianBlue_01", "SimpleCats_RussianBlue_02", "SimpleCats_RussianBlue_03",
        };
        /// <summary>The packs' keys (MapArt.ModelKey's pack), and every model the map may ask for (MapCatalogMake puts them in the game's catalog).</summary>
        public const string Pack = "dog", SimplePack = "simple", CatPack = "cats";
        public static IEnumerable<string> Models => Strays.Concat(Wild).Select(n => $"{Pack}:{n}").Concat(Stags.Concat(Does).Concat(Boars).Concat(Rabbits).Select(n => $"{SimplePack}:{n}"))
            .Concat(Cats.Select(n => $"{CatPack}:{n}"));
        /// <summary>SIMPLE's animals' materials (Resources; FaunaMake): their own are the built-in pipeline's.</summary>
        public const string SimpleMaterial = "AnimalesSimple", CatMaterial = "AnimalesGatos";
        static Material simpleMaterial, catMaterial;
        /// <summary>A grazer grazing (its controller's Eat_b, while it stands so).</summary>
        const int Graze = 9;

        /// <summary>The animals' animator (Resources; made by FaunaMake), for the Dog Pack's prefabs name one that isn't in the project.</summary>
        public const string ControllerName = "Animales";
        static RuntimeAnimatorController controller;
        /// <summary>The gestures a standing animal makes (the controller's Accion with Gesto): its number, its state, the Dog Pack's clip.</summary>
        public static readonly (int accion, string name, string clip)[] Gestures =
        {
            (1, "Olfatear", "Actions_Standing/_POLYGON_Dog_Action_Standing_Sniff"),
            (2, "Cola", "Actions_Standing/_POLYGON_Dog_Action_Standing_TailWag"),
            (3, "Sacudirse", "Actions_Standing/_POLYGON_Dog_Action_Standing_Shake"),
            (4, "Bostezar", "Actions_Standing/_POLYGON_Dog_Action_Standing_Yawn"),
            (5, "Escarbar", "Actions_Standing/_POLYGON_Dog_Action_Standing_Dig"),
            (6, "Aullar", "Actions_Standing/_POLYGON_Dog_Action_Standing_Howl"),
        };
        /// <summary>Sitting and lying down a while (the controller's Accion while it lasts).</summary>
        public const int Sit = 7, Lie = 8;

        /// <summary>Walking and running (m/s), how near anyone scares them and how far they run (tiles), how far they wander.</summary>
        const float DogWalk = 1.1f, DogRun = 6.5f, WildWalk = 1.0f, WildRun = 7.5f;
        const float DogFear = 6, WildFear = 9, BirdFear = 4.5f, Calm = 14, Wander = 5;
        /// <summary>How quickly it gets going and slows down (m/s²), walking and running off, and how much quicker it gets up off the ground to run.</summary>
        const float WalkStart = 2.5f, WalkBrake = 2, FleeStart = 14, FleeBrake = 9, RiseQuick = 2.5f;
        /// <summary>
        /// The 3D review's A-29, 10/11: how fast it turns to run off (degrees a second; it was 540, 54° from frame to frame at
        /// 10 fps), a cat up off the ground to run (RiseQuick's 2.5 got it up in a frame), and how long a grazer takes off
        /// its grazing into its walk or run (s).
        /// </summary>
        const float FleeTurn = 360, CatRise = 1.6f, EatOut = 0.3f, EatOutFleeing = 0.15f;
        /// <summary>
        /// How far before a corner of its way it starts round it (tiles; as far after it, or half the next stretch), and
        /// the sharpest corner it goes round so (degrees; past it, it turns on the spot): Arturo's of 7/11, its jerks from
        /// tile to tile, were its way's corners taken at once, its body sliding sideways till it had turned.
        /// </summary>
        const float RoundCorner = 0.4f, RoundMost = 120;

        /// <summary>
        /// A grazer's or a cat's walk and run (m/s), how near anyone scares it (tiles), and how tall it stands (m): the rabbit
        /// off first and quick, a hare's size (as a rabbit's, the tufts hid it from the game's camera); the cat off before
        /// the dog (DogFear), a big tom's size for the same reason.
        /// </summary>
        static (float walk, float run, float fear, float tall) Grazing(string breed) =>
            breed.StartsWith("SimpleCats") ? (0.7f, 6f, 9, 0.5f) : breed.StartsWith("Rabbit") ? (0.6f, 7f, 12, 0.55f) : breed.StartsWith("Boar") ? (0.9f, 6.5f, 7, 0.85f) : breed.StartsWith("Stag") ? (1.0f, 8.5f, 10, 1.75f) : (1.0f, 8.5f, 10, 1.4f);
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

        /// <summary>For a probe: each animal's animator as it is now (what it plays, what it does, whether it is drawn and seen).</summary>
        public IEnumerable<string> AnimatorReport() => animals.Where(a => a.animator != null).Select(a =>
        {
            var clips = a.animator.isActiveAndEnabled ? a.animator.GetCurrentAnimatorClipInfo(0) : new AnimatorClipInfo[0];
            string clip = clips.Length > 0 ? clips[0].clip.name : "-";
            bool visible = a.renderers.Any(r => r != null && r.isVisible);
            return FormattableString.Invariant($"{a.root.name} {a.state} doing {a.doing} down {Down(a)} active {a.animator.isActiveAndEnabled} culling {a.animator.cullingMode} clip {clip} move {a.animator.GetFloat("Movement_f"):0.00} shown {a.shown} visible {visible} controller {(a.animator.runtimeAnimatorController != null ? a.animator.runtimeAnimatorController.name : "none")}");
        });

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// The animals of map `s` (its buildings as `plan` has them, their tops set by BuildingSurfaces), under `parent`;
        /// their prefabs by key (`dog:SK_Dog_…`, `simple:Stag_01`…) from `dogOf` (none drawn if it has none).
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
            // (The Bastion's base, and its siege: no more than in the wood round it, and only in its pinar, #7.)
            bool home = s.Base != null;
            // Dogs only in a town, one (Ana's #11, 1/11); the fox and the wolf in the country, at the base one or two.
            int dogs = town && !home ? 1 : 0;
            foreach (var p in Spots(70, 12, 4, 8).Take(dogs).ToList())
            {
                taken.Add(p);
                AddDog(Strays[(int)(Hash(p.x, p.y, 71) * Strays.Length)], p, Kind.Dog, dogOf);
            }
            // And a cat or two by a building's wall (any open tile if none is), apart from the dog.
            bool ByWall(int x, int y) => new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) }.Any(q => q.Item1 >= 0 && q.Item2 >= 0 && q.Item1 < s.Width && q.Item2 < s.Height && plan.Owner[q.Item2 * s.Width + q.Item1] >= 0);
            int cats = town && !home ? 1 + (int)(Hash(s.Width, s.Height, 90) * 2) : 0;
            for (int k = 0; k < cats; k++)
            {
                var spots = Spots(91, 10, 4, 8).ToList();
                var p = spots.Where(q => ByWall(q.x, q.y)).Concat(spots).FirstOrDefault();
                if (p == default) break;
                taken.Add(p);
                AddDog(Cats[(int)(Hash(p.x, p.y, 92) * Cats.Length) % Cats.Length], p, Kind.Cat, dogOf);
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
            // (At the base, in its pinar: a pine two tiles off, but none beside it, so their crowns don't hide it; three tiles
            // in from the map's edge.)
            bool Tree(int x, int y)
            {
                if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return false;
                int i = y * s.Width + x;
                return s.Tiles[i] == TileKinds.High && s.Decor != null && i < s.Decor.Count && Bastion.Engine.Decor.DecorOf(s.Decor[i])?.Kind == DecorKinds.Tree;
            }
            bool Pinar(int x, int y)
            {
                if (x < 3 || y < 3 || x >= s.Width - 3 || y >= s.Height - 3) return false;
                bool near = false;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (!Tree(x + dx, y + dy)) continue;
                        if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1) return false;
                        near = true;
                    }
                return near;
            }
            string Pick(string[] list, int x, int y, int salt) => list[(int)(Hash(x, y, salt) * list.Length) % list.Length];
            if (home)
            {
                // At the base, one or two, any of them, among the pines.
                var any = Wild.Select(n => (n, Kind.Wild)).Concat(new[] { Stags[0], Does[0], Boars[0], Rabbits[0] }.Select(n => (n, Kind.Grazer))).ToArray();
                // (Taken as they are put: each apart from those before it.)
                int count = 1 + (int)(Hash(s.Width, s.Height, 84) * 2);
                foreach (var p in Spots(72, 14, 6, 10).Where(p => Pinar(p.x, p.y)))
                {
                    if (count-- <= 0) break;
                    taken.Add(p);
                    var (name, kind) = any[(int)(Hash(p.x, p.y, 73) * any.Length) % any.Length];
                    string breed = kind == Kind.Grazer ? Pick(name.StartsWith("Stag") ? Stags : name.StartsWith("Doe") ? Does : name.StartsWith("Boar") ? Boars : Rabbits, p.x, p.y, 85) : name;
                    AddDog(breed, p, kind, dogOf);
                }
            }
            else if (!town)
            {
                // Out in the country, in its clearings: a fox or a wolf, two; a stag with a doe beside it, a boar, two rabbits.
                int k = -1;
                foreach (var p in Spots(72, 14, 6, 6).Where(p => Clearing(p.x, p.y)))
                {
                    if (++k >= 6) break;
                    taken.Add(p);
                    if (k < 2) AddDog(Pick(Wild, p.x, p.y, 73), p, Kind.Wild, dogOf);
                    else if (k == 2)
                    {
                        AddDog(Pick(Stags, p.x, p.y, 86), p, Kind.Grazer, dogOf);
                        var by = new[] { (p.x + 1, p.y), (p.x, p.y + 1), (p.x - 1, p.y), (p.x, p.y - 1) }.FirstOrDefault(q => Outdoors(q.Item1, q.Item2));
                        if (by != default) AddDog(Pick(Does, p.x, p.y, 87), by, Kind.Grazer, dogOf);
                    }
                    else AddDog(k == 3 ? Pick(Boars, p.x, p.y, 88) : Pick(Rabbits, p.x, p.y, 89), p, Kind.Grazer, dogOf);
                }
            }
            // Crows on the roofs: two or three buildings, two to four on each, along a house's ridge or a flat roof's front edge.
            var roofs = plan.Buildings.Where(b => b.kind != BuildingKinds.Ruin && !b.frame && b.top > 0).OrderBy(b => Hash(b.x0, b.y0, 74)).Take(home ? 0 : town ? 3 : 2).ToList();
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
            Debug.Log($"[fauna] {animals.Count(a => a.kind == Kind.Dog)} dogs, {animals.Count(a => a.kind == Kind.Cat)} cats, {animals.Count(a => a.kind == Kind.Wild)} wild, {animals.Count(a => a.kind == Kind.Grazer)} grazing ({string.Join(", ", animals.Where(a => a.kind == Kind.Grazer).Select(a => a.breed))}), {animals.Count(a => a.kind == Kind.Crow)} crows, {animals.Count(a => a.kind == Kind.Pigeon)} pigeons");
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

        /// <summary>Whether it is one of SIMPLE's (a grazer or a cat): their own controller (Speed_f, Eat_b, a cat's Sit_b), our material.</summary>
        static bool Simple(Kind kind) => kind == Kind.Grazer || kind == Kind.Cat;

        void AddDog(string prefab, (int x, int y) p, Kind kind, Func<string, GameObject> dogOf)
        {
            bool grazer = Simple(kind);
            var model = dogOf?.Invoke($"{(kind == Kind.Cat ? CatPack : grazer ? SimplePack : Pack)}:{prefab}");
            if (model == null) return;
            var root = new GameObject(prefab).transform;
            root.SetParent(transform, false);
            root.position = MapSpace.World(p.x, p.y);
            float yaw = Hash(p.x, p.y, 82) * 360;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            var made = Instantiate(model, root, false);
            made.name = "modelo";
            if (grazer)
            {
                // SIMPLE's in our material (theirs is the built-in pipeline's), at their true size.
                simpleMaterial ??= Resources.Load<Material>(SimpleMaterial);
                catMaterial ??= Resources.Load<Material>(CatMaterial);
                var material = kind == Kind.Cat ? catMaterial : simpleMaterial;
                var drawn = made.GetComponentsInChildren<Renderer>();
                if (material != null) foreach (var r in drawn) r.sharedMaterial = material;
                if (drawn.Length > 0)
                {
                    float tall = drawn.Select(r => r.bounds.max.y).Max() - root.position.y;
                    if (tall > 1e-3f) made.transform.localScale *= Grazing(prefab).tall / tall;
                }
            }
            var animator = made.GetComponentInChildren<Animator>();
            FaunaBody stride = null;
            if (animator != null)
            {
                // (The Dog Pack's prefabs name a controller that isn't there: ours; SIMPLE's their own.)
                controller ??= Resources.Load<RuntimeAnimatorController>(ControllerName);
                if (controller != null && !grazer) animator.runtimeAnimatorController = controller;
                // Its clips' root (their own walk: the dog's a metre each 0.8 s) handed to FaunaBody and left there, not
                // written onto the model: without root motion it went on ahead within each loop and back as it started
                // again (Arturo, 7/11: «como que vuelve»). (In play only: out of it nothing calls FaunaBody, and the root
                // motion would be the model's.)
                animator.applyRootMotion = Application.isPlaying;
                stride = animator.gameObject.AddComponent<FaunaBody>();
                // (Not moved while no camera sees it, in the game; a lab's shot camera draws into a texture, unseen.)
                animator.cullingMode = Application.isPlaying ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.AlwaysAnimate;
            }
            foreach (var r in made.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            animals.Add(new Animal { kind = kind, breed = prefab, root = root, animator = animator, stride = stride, state = State.Idle, wait = 1 + Hash(p.y, p.x, 83) * 4, yaw = yaw, rng = (uint)(p.x * 7919 + p.y * 104729 + 1), renderers = made.GetComponentsInChildren<Renderer>(), id = animals.Count });
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
            animals.Add(new Animal { kind = kind, root = root, body = body, left = left, right = right, state = State.Idle, wait = 0.5f + (seed % 7) * 0.3f, yaw = yaw, rng = seed * 2654435761u + 1, renderers = root.GetComponentsInChildren<Renderer>(), onRoof = roof, id = animals.Count });
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

        /// <summary>A free outdoor tile some tiles from `from` that it can get to (a wander's end), its way in `a.way`; or `from` if none found.</summary>
        Vector3 Somewhere(Animal a, Vector3 from, float reach)
        {
            var (steps, back) = Reach(from, Mathf.CeilToInt(reach * 2), false);
            for (int tries = 0; tries < 12; tries++)
            {
                float angle = Random01(a) * Mathf.PI * 2, d = (0.4f + 0.6f * Random01(a)) * reach * MapSpace.Tile;
                var t = MapSpace.WorldToTile(from + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * d);
                if (t.x < 0 || t.y < 0 || t.x >= s.Width || t.y >= s.Height || steps[t.y * s.Width + t.x] <= 0) continue;
                var end = MapSpace.World(t.x, t.y) + new Vector3(Random01(a) - 0.5f, 0, Random01(a) - 0.5f) * MapSpace.Tile * 0.6f;
                // (Not onto another, nor where another is going: A-30.)
                if (Crowded(a, end)) continue;
                a.way = Route(from, t.y * s.Width + t.x, back, end);
                return end;
            }
            a.way.Clear();
            return from;
        }

        /// <summary>Whether `end` is within Apart of another animal on the ground, or of where one walks to (A-30).</summary>
        bool Crowded(Animal a, Vector3 end) =>
            animals.Any(b => b != a && !b.gone && !Flies(b) && (Flat(b.root.position, end) < Apart || (b.state == State.Walk && Flat(b.target, end) < Apart)));

        static bool Flies(Animal a) => a.kind == Kind.Crow || a.kind == Kind.Pigeon;

        /// <summary>
        /// How much room an animal takes (m, its middle to its side): the stag and the doe, the boar, the wolf, the fox and
        /// the dog, the rabbit and the cat.
        /// </summary>
        static float Room(Animal a) =>
            a.kind == Kind.Cat || a.breed.StartsWith("Rabbit") ? 0.2f : a.breed.StartsWith("Stag") ? 0.6f : a.breed.StartsWith("Doe") ? 0.5f : a.breed.StartsWith("Boar") ? 0.45f : 0.4f;

        /// <summary>
        /// The 3D review's A-30 (10/11: the doe walked into the stag and he turned inside her): how far apart a wander's end
        /// keeps from the others and where they go (m); the room kept round each besides their own (m); how far to either
        /// side of its way another is in its way (degrees); how much it swerves off its way round one (degrees); how long
        /// the one that gives way waits before it goes round (s).
        /// </summary>
        const float Apart = 1.5f, RoomMargin = 0.3f, AheadAngle = 60, SwerveAngle = 40, HoldMost = 1.5f;

        /// <summary>
        /// Whether another animal on the ground is in `a`'s way, going `to` (A-30): nearer than their rooms and RoomMargin,
        /// within AheadAngle of it (`any`). Then, walking, the later one (its id) waits for it to go, HoldMost at most; the
        /// other, or one running off, swerves away from it (`swerve`, degrees, + to its right).
        /// </summary>
        (bool any, bool hold, float swerve) InTheWay(Animal a, Vector3 to, bool flees)
        {
            bool any = false, hold = false;
            float swerve = 0;
            var mine = a.root.position;
            foreach (var b in animals)
            {
                if (b == a || b.gone || Flies(b)) continue;
                var off = b.root.position - mine;
                off.y = 0;
                float d = off.magnitude;
                if (d < 1e-4f || d >= Room(a) + Room(b) + RoomMargin) continue;
                float angle = Vector3.SignedAngle(to, off, Vector3.up);
                if (Mathf.Abs(angle) > AheadAngle) continue;
                any = true;
                if (!flees && a.id > b.id && a.held < HoldMost) hold = true;
                else swerve = angle >= 0 ? -SwerveAngle : SwerveAngle;
            }
            return (any, hold, swerve);
        }

        /// <summary>
        /// Where to run from `threat`, its way in `a.way`: of the outdoor tiles it can get to (Reach, as far as Calm and half
        /// as far again), the one farthest from `threat`, less a little for the way there; one on the map's edge goes on off
        /// it, a way out (there it is gone).
        /// </summary>
        Vector3 Away(Animal a, Vector3 threat)
        {
            var from = a.root.position;
            var (steps, back) = Reach(from, Mathf.CeilToInt(Calm * 1.5f), true);
            var near = MapSpace.WorldToTile(threat);
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] <= 0) continue;
                int x = i % s.Width, y = i / s.Width;
                float score = Mathf.Sqrt((x - near.x) * (x - near.x) + (y - near.y) * (y - near.y)) - 0.25f * steps[i];
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0)
            {
                a.way.Clear();
                return from;
            }
            var end = MapSpace.World(best % s.Width, best / s.Width);
            int bx = best % s.Width, by = best / s.Width;
            // (On the edge: two tiles on, straight out of the map.)
            if (bx < 1 || by < 1 || bx >= s.Width - 1 || by >= s.Height - 1)
                end += new Vector3(bx < 1 ? -1 : bx >= s.Width - 1 ? 1 : 0, 0, by < 1 ? 1 : by >= s.Height - 1 ? -1 : 0) * 2 * MapSpace.Tile;
            a.way = Route(from, best, back, end);
            return end;
        }

        /// <summary>Where one can go: an outdoor tile, or, with `edge`, one on the map's edge (a way out) that would be so.</summary>
        bool Open(int x, int y, bool edge)
        {
            if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return false;
            if (Outdoors(x, y)) return true;
            if (!edge || (x >= 1 && y >= 1 && x < s.Width - 1 && y < s.Height - 1)) return false;
            int i = y * s.Width + x;
            return Bastion.Engine.Grid.IsWalkable(s.Tiles[i]) && plan.Owner[i] < 0;
        }

        static readonly (int x, int y)[] Ways = { (0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1) };

        /// <summary>
        /// The tiles one at `from` can walk to over the open ground as a soldier does (Arturo's #2 of 2/11: the dog went
        /// through cars and walls), at most `most` steps off: eight ways, a diagonal only past two open sides (no corner
        /// cut). Each tile's steps (0 none, the start 1) and the one it is come to from.
        /// </summary>
        (int[] steps, int[] back) Reach(Vector3 from, int most, bool edge)
        {
            int w = s.Width, n = w * s.Height;
            var steps = new int[n];
            var back = new int[n];
            var start = MapSpace.WorldToTile(from);
            if (start.x < 0 || start.y < 0 || start.x >= w || start.y >= s.Height) return (steps, back);
            var queue = new Queue<int>();
            int first = start.y * w + start.x;
            steps[first] = 1;
            back[first] = -1;
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, y = i / w;
                if (steps[i] > most) continue;
                foreach (var (dx, dy) in Ways)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!Open(nx, ny, edge) || steps[ny * w + nx] > 0) continue;
                    if (dx != 0 && dy != 0 && (!Open(x + dx, y, edge) || !Open(x, y + dy, edge))) continue;
                    steps[ny * w + nx] = steps[i] + 1;
                    back[ny * w + nx] = i;
                    queue.Enqueue(ny * w + nx);
                }
            }
            return (steps, back);
        }

        /// <summary>
        /// The way from `from` to tile `to` (Reach's `back`), ending at `end`: its tiles' middles, straightened wherever the
        /// line keeps clear of what fills a tile (Wide).
        /// </summary>
        List<Vector3> Route(Vector3 from, int to, int[] back, Vector3 end)
        {
            var tiles = new List<Vector3>();
            for (int i = to; i >= 0 && back[i] >= 0; i = back[i]) tiles.Add(MapSpace.World(i % s.Width, i / s.Width));
            tiles.Reverse();
            if (tiles.Count > 0) tiles[tiles.Count - 1] = end;
            else tiles.Add(end);
            var way = new List<Vector3>();
            var at = from;
            int k = 0;
            while (k < tiles.Count)
            {
                int far = k;
                for (int j = tiles.Count - 1; j > k; j--)
                    if (Wide(at, tiles[j]))
                    {
                        far = j;
                        break;
                    }
                way.Add(tiles[far]);
                at = tiles[far];
                k = far + 1;
            }
            return way;
        }

        /// <summary>Whether a body walking from `a` to `b` keeps clear of what fills a tile: its line and a third of a tile to each side stay open, sampled every quarter tile.</summary>
        bool Wide(Vector3 a, Vector3 b)
        {
            var along = b - a;
            along.y = 0;
            float length = along.magnitude;
            if (length < 1e-4f) return true;
            var side = new Vector3(-along.z, 0, along.x) / length * (MapSpace.Tile / 3);
            int n = Mathf.CeilToInt(length / (MapSpace.Tile * 0.25f));
            for (int k = 1; k <= n; k++)
            {
                var p = Vector3.Lerp(a, b, k / (float)n);
                foreach (var q in new[] { p, p + side, p - side })
                {
                    var t = MapSpace.WorldToTile(q);
                    if (!Open(t.x, t.y, true)) return false;
                }
            }
            return true;
        }

        void StepDog(Animal a, float dt)
        {
            var (threat, distance) = Nearest(a.root.position);
            var graze = Simple(a.kind) ? Grazing(a.breed) : default;
            float fear = (Simple(a.kind) ? graze.fear : a.kind == Kind.Wild ? WildFear : DogFear) * MapSpace.Tile;
            if (distance < fear && a.state != State.Flee)
            {
                a.state = State.Flee;
                a.target = Away(a, threat);
                Stand(a);
            }
            float speed = Simple(a.kind) ? (a.state == State.Flee ? graze.run : a.state == State.Walk ? graze.walk : 0)
                : a.state == State.Flee ? (a.kind == Kind.Wild ? WildRun : DogRun) : a.state == State.Walk ? (a.kind == Kind.Wild ? WildWalk : DogWalk) : 0;
            a.turning = 0;
            if (a.state == State.Idle)
            {
                // (What pace it still had as it got there, gone in a moment: its legs stop, not at once.)
                a.pace = Mathf.MoveTowards(a.pace, 0, FleeBrake * dt);
                if (!a.chosen) Choose(a);
                a.wait -= dt;
                if (a.wait <= 0)
                {
                    Stand(a);
                    a.target = Somewhere(a, a.root.position, Wander);
                    a.state = State.Walk;
                }
            }
            // (Sitting or lying, or getting up: it moves once on its feet; up quicker with someone coming.)
            else if (Down(a)) a.pace = 0;
            else
            {
                // Along its way: straight at its next corner, round it on a curve (Bend), to its end once there.
                if (a.bending && (a.bendWay != a.way || a.way.Count < 2 || a.way[0] != a.bendAt)) a.bending = false;
                var aim = a.way.Count > 0 ? a.way[0] : a.target;
                var to = aim - a.root.position;
                to.y = 0;
                float left = to.magnitude;
                if (!a.bending && a.way.Count > 1 && left <= RoundCorner * MapSpace.Tile) Bend(a, left);
                // (A corner too sharp to go round, or it is on it: on to the next stretch, turning on the spot.)
                if (!a.bending && a.way.Count > 1 && left < 0.005f)
                {
                    a.way.RemoveAt(0);
                    aim = a.way[0];
                    to = aim - a.root.position;
                    to.y = 0;
                    left = to.magnitude;
                }
                bool last = a.way.Count <= 1;
                // There once braked to it (Arturo's #7 of 5/11: at 15 cm it still went at 0.8 m/s, and stopped dead, its
                // legs from mid-stride to standing in a frame); what pace is left goes as it stands (Idle).
                if (last && left < 0.005f)
                {
                    a.way.Clear();
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
                    // Another in its way (A-30): it waits for it, or goes round it.
                    var (blocked, hold, swerve) = a.bending ? (false, false, 0f) : InTheWay(a, to, a.state == State.Flee);
                    a.held = blocked ? a.held + dt : 0;
                    if (swerve != 0) to = Quaternion.Euler(0, swerve, 0) * to;
                    // The way it goes now: along the curve, or at its next corner.
                    var ahead = a.bending ? BendTangent(a, a.bendDone) : to;
                    float want = Mathf.Atan2(ahead.x, ahead.z) * Mathf.Rad2Deg, was = a.yaw;
                    a.yaw = Mathf.MoveTowardsAngle(a.yaw, want, (a.state == State.Flee ? FleeTurn : 200) * dt);
                    a.turning = Mathf.Abs(Mathf.DeltaAngle(was, a.yaw)) / dt;
                    a.root.rotation = Quaternion.Euler(0, a.yaw, 0);
                    // (Turning on the spot first when it faces away: off only once it faces within 60° of its way, and
                    // full on facing it, not sliding sideways as it turns.)
                    float facing = Mathf.Clamp01((Mathf.Cos(Mathf.DeltaAngle(a.yaw, want) * Mathf.Deg2Rad) - 0.5f) / 0.5f);
                    // Speeding up and slowing down, not off at once and stopped dead (Arturo's #1 of 2/11, its jerks): to
                    // its end slowing down so as to stop there.
                    bool flees = a.state == State.Flee;
                    float goal = hold ? 0 : speed * facing;
                    // (Braking for what is left of its whole way, not only its last stretch: one short after a corner left it
                    // no room, and it got there running.)
                    // (To a stop at a corner too sharp to go round: it turns there on the spot.)
                    bool sharp = !a.bending && a.way.Count > 1 && Vector3.Angle(to, Vector3.Scale(a.way[1] - a.way[0], new Vector3(1, 0, 1))) > RoundMost;
                    float rest = a.bending ? (1 - a.bendDone) * a.bendLength + Flat(a.bendTo, a.way[1]) : left;
                    for (int k = a.bending ? 2 : 1; k < a.way.Count && !sharp; k++) rest += Flat(a.way[k - 1], a.way[k]);
                    goal = Mathf.Min(goal, Mathf.Sqrt(2 * (flees ? FleeBrake : WalkBrake) * rest));
                    // (Slowing, up to half as hard again: a step at a time it lags the curve it brakes to, and got there at 2 m/s.)
                    a.pace = Mathf.MoveTowards(a.pace, goal, (goal > a.pace ? (flees ? FleeStart : WalkStart) : 1.5f * (flees ? FleeBrake : WalkBrake)) * dt);
                    if (!a.bending) a.root.position += to / left * Mathf.Min(left, a.pace * dt);
                    else
                    {
                        // Along the curve by as much ground as its pace covers (its parameter by the curve's own speed).
                        float u = a.bendDone, go = a.pace * dt, part = go / 4;
                        for (int k = 0; k < 4 && go > 1e-6f && u < 1; k++)
                        {
                            float rate = Mathf.Max(BendTangent(a, u).magnitude, 1e-3f), du = Mathf.Min(1 - u, Mathf.Min(part, go) / rate);
                            u += du;
                            go -= du * rate;
                        }
                        a.bendDone = u >= 0.9999f ? 1 : u;
                        var on = BendAt(a, a.bendDone);
                        a.root.position = new Vector3(on.x, a.root.position.y, on.z);
                        // Round it: on along the next stretch, with what is left of the step (not a step short in the frame
                        // it gets round, a hitch at every corner).
                        if (a.bendDone >= 1)
                        {
                            a.bending = false;
                            a.way.RemoveAt(0);
                            var next = a.way[0] - a.root.position;
                            next.y = 0;
                            float far = next.magnitude;
                            if (far > 1e-4f) a.root.position += next / far * Mathf.Min(far, go);
                        }
                    }
                }
            }
            if (a.animator != null && a.animator.runtimeAnimatorController != null)
            {
                // Its legs as fast as it goes (the controller's 0 standing, 0.5 walking, 1 running), and up off the ground
                // quicker with someone coming.
                float walk = Simple(a.kind) ? graze.walk : a.kind == Kind.Wild ? WildWalk : DogWalk, run = Simple(a.kind) ? graze.run : a.kind == Kind.Wild ? WildRun : DogRun;
                float movement = a.pace <= walk ? 0.5f * a.pace / walk : 0.5f + 0.5f * Mathf.Clamp01((a.pace - walk) / (run - walk));
                // (Turning on the spot, a slow step round, eased in and out: not a statue turned on its stand.)
                a.stepping = Mathf.MoveTowards(a.stepping, 0.2f * Mathf.Clamp01(a.turning / 200), dt);
                movement = Mathf.Max(movement, a.stepping);
                a.animator.speed = a.state == State.Flee && Down(a) ? (a.kind == Kind.Cat ? CatRise : RiseQuick) : Stride(a, dt);
                // (SIMPLE's controller: Speed_f the same way, Eat_b while it grazes, a cat's Sit_b while it sits.)
                string pace = Simple(a.kind) ? "Speed_f" : "Movement_f";
                a.animator.SetFloat(pace, movement);
                if (Simple(a.kind)) a.animator.SetBool("Eat_b", a.doing == Graze && a.state == State.Idle);
                if (a.kind == Kind.Cat) a.animator.SetBool("Sit_b", a.doing == Sit && a.state == State.Idle);
                // Off its grazing as it goes (the 3D review's A-29, 10/11): SIMPLE's controller leaves Eat only at its clip's
                // end (exit time 0.93, the deer's a 2.5 s loop), and meanwhile it went on its way, or turned round, or ran
                // off at 5 m/s, its head down and its legs still, then snapped to its walk in a frame.
                if (Simple(a.kind) && a.state != State.Idle && !a.animator.IsInTransition(0) && a.animator.GetCurrentAnimatorStateInfo(0).IsName("Eat"))
                    a.animator.CrossFadeInFixedTime("Locomotion", a.state == State.Flee ? EatOutFleeing : EatOut);
            }
        }

        /// <summary>
        /// The animator's speed for `a`'s legs to keep up with its pace: as fast against its clips' own walk (FaunaBody's,
        /// the blend of standing, walking and running it plays) as it goes against it, from half to twice, eased; 1 standing
        /// or with clips that walk it nowhere. The dog's walk went 1.29 m/s to its 1.1: its feet slid.
        /// </summary>
        static float Stride(Animal a, float dt)
        {
            float clip = a.stride != null ? a.stride.ClipPace : 0;
            float want = a.pace > 0.05f && clip > 0.05f ? Mathf.Clamp(a.pace / clip, 0.5f, 2) : 1;
            a.legs += (want - a.legs) * (1 - Mathf.Exp(-8 * dt));
            return a.legs;
        }

        /// <summary>
        /// Round the corner `a` is `left` from (its way's first point, the next stretch from it to the second): a curve from
        /// where it is to as far along the next stretch (half of it at most), the corner its middle (a quadratic Bézier),
        /// within the corner's own triangle, so no nearer what fills a tile than the way's straight stretches (Wide). None
        /// for a corner sharper than RoundMost.
        /// </summary>
        static void Bend(Animal a, float left)
        {
            var at = a.way[0];
            var on = a.way[1] - at;
            on.y = 0;
            var from = a.root.position;
            var into = at - from;
            into.y = 0;
            if (left < 1e-3f || on.magnitude < 1e-3f || Vector3.Angle(into, on) > RoundMost) return;
            a.bending = true;
            a.bendWay = a.way;
            a.bendFrom = new Vector3(from.x, 0, from.z);
            a.bendAt = at;
            a.bendTo = new Vector3(at.x, 0, at.z) + on.normalized * Mathf.Min(left, on.magnitude / 2);
            a.bendDone = 0;
            a.bendLength = 0;
            for (int k = 1; k <= 8; k++) a.bendLength += Flat(BendAt(a, (k - 1) / 8f), BendAt(a, k / 8f));
        }

        /// <summary>The point `u` (0 to 1) of `a`'s curve round a corner (on the ground: y 0).</summary>
        static Vector3 BendAt(Animal a, float u)
        {
            var corner = new Vector3(a.bendAt.x, 0, a.bendAt.z);
            return (1 - u) * (1 - u) * a.bendFrom + 2 * u * (1 - u) * corner + u * u * a.bendTo;
        }

        /// <summary>Its curve's way and speed at `u` (m per unit of `u`, on the ground).</summary>
        static Vector3 BendTangent(Animal a, float u)
        {
            var corner = new Vector3(a.bendAt.x, 0, a.bendAt.z);
            return 2 * (1 - u) * (corner - a.bendFrom) + 2 * u * (a.bendTo - corner);
        }

        /// <summary>How far apart two points are on the ground (m).</summary>
        static float Flat(Vector3 p, Vector3 q) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));

        /// <summary>
        /// What animal `a` does as it stops (once): nothing, a gesture (a dog wags its tail, a wolf howls, a fox
        /// digs), or sitting or lying a while; and how long it stands so.
        /// </summary>
        void Choose(Animal a)
        {
            a.chosen = true;
            // A grazer grazes, three times in five, a while (SIMPLE's Eat), or stands.
            if (a.kind == Kind.Grazer)
            {
                a.doing = Random01(a) < 0.6f ? Graze : 0;
                a.wait = a.doing == Graze ? 3 + 5 * Random01(a) : 1 + 3 * Random01(a);
                return;
            }
            // A cat sits a long while, half the times, or eats something off the ground now and then, or stands.
            if (a.kind == Kind.Cat)
            {
                float roll = Random01(a);
                a.doing = roll < 0.5f ? Sit : roll < 0.62f ? Graze : 0;
                a.wait = a.doing == Sit ? 8 + 10 * Random01(a) : a.doing == Graze ? 3 + 3 * Random01(a) : 1 + 3 * Random01(a);
                return;
            }
            bool dog = a.kind == Kind.Dog, fox = a.breed.Contains("Fox"), howls = a.breed.Contains("Wolf");
            // Its share of each, in turn: nothing, sniffing, the tail, shaking, yawning, digging, howling, sitting, lying.
            float[] shares = dog ? new[] { 0.3f, 0.15f, 0.1f, 0.08f, 0.07f, 0.05f, 0, 0.15f, 0.1f } : new[] { 0.35f, 0.2f, 0, 0.05f, 0.1f, fox ? 0.1f : 0, howls ? 0.12f : 0, 0.1f, 0 };
            float at = Random01(a) * shares.Sum();
            int pick = 0;
            while (pick < shares.Length - 1 && at >= shares[pick]) at -= shares[pick++];
            a.doing = pick <= 6 ? pick : pick == 7 ? Sit : Lie;
            float k = Random01(a);
            a.wait = a.doing == 0 ? 1 + 4 * k : a.doing == Sit ? 6 + 8 * k : a.doing == Lie ? 10 + 10 * k : 2.5f + 2 * k;
            if (a.animator == null || a.doing == 0) return;
            a.animator.SetInteger("Accion", a.doing);
            a.animator.SetTrigger("Gesto");
        }

        /// <summary>On its feet again (it was sitting or lying, or about to make a gesture): to choose again at its next stop.</summary>
        static void Stand(Animal a)
        {
            a.chosen = false;
            a.doing = 0;
            if (a.animator == null || a.animator.runtimeAnimatorController == null || Simple(a.kind)) return;
            a.animator.SetInteger("Accion", 0);
            a.animator.ResetTrigger("Gesto");
        }

        /// <summary>Whether animal `a` is sitting, lying, or getting down or up (it can't walk till it's on its feet).</summary>
        static bool Down(Animal a)
        {
            var an = a.animator;
            if (an == null || !an.isActiveAndEnabled || an.runtimeAnimatorController == null) return false;
            var state = an.IsInTransition(0) ? an.GetNextAnimatorStateInfo(0) : an.GetCurrentAnimatorStateInfo(0);
            // (A cat's: SIMPLE's controller has no tags, its sitting down, sitting and getting up by name.)
            if (a.kind == Kind.Cat) return state.IsName("Sit_Start") || state.IsName("Sit_Idle") || state.IsName("Sit_Up");
            return state.IsTag("Suelo");
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
