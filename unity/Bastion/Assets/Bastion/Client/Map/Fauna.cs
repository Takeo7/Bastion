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
    /// and nothing in its clearing. Fewer since Arturo's play-test of 14/11 («dio la impresión de que había demasiados»:
    /// Populate's budget), and each about its own business (the same play-test: «buscar comida y refugio y huir de los
    /// zombis»): they wander a few tiles (now and then sniffing, wagging, shaking, yawning, digging, howling, or sitting or
    /// lying a while: #3, FaunaMake's controller), go to eat where their kind finds food (Want.Food: bins and the bodies of
    /// the dead put down for the dogs, the fox and the wolf, grass and the patches for the deer, the boar and the rabbit,
    /// bins for the cats, bodies and bins for the crows, a square's paving for the pigeons) and to rest where it is
    /// sheltered (Want.Shelter: under the trees, against a hedge, a wall, a car or a shed, in a corner; more and longer by
    /// night; the birds on the roofs). When one of the living comes near (the squad, the Cuervos: the figures UnitViews
    /// has, and the lab's ExtraThreats) or one of the dead (seen by the squad or not: the animal knows; the dead pay them no
    /// heed, Arturo's choice of 14/11, so nothing of the rules changes), the dogs and the wild ones get up and run off,
    /// to cover if there is some, and the birds take wing to another roof; once far enough they look back a moment and go
    /// to ground, or are gone past the map's edge. Where they start is the map's own (its tiles, hashed), the same for
    /// both players; how they move, the frame's.
    /// </summary>
    public sealed class Fauna : MonoBehaviour, IShotStep
    {
        public static Fauna Current { get; private set; }

        /// <summary>For a lab: more places to run from than the figures (a stand-in walking by).</summary>
        public static Func<IEnumerable<Vector3>> ExtraThreats;
        /// <summary>For a lab: the same for the dead (a stand-in one of them).</summary>
        public static Func<IEnumerable<Vector3>> ExtraDead;
        /// <summary>For a lab: called with each step's dt before the animals move (to walk the stand-in).</summary>
        public static Action<float> Stepping;
        /// <summary>For a lab: what every animal goes about each time it chooses (Want's name: Food, Shelter, Wander); null, its own choice.</summary>
        public static string LabWant = Environment.GetEnvironmentVariable("BASTION_QUIERE");
        /// <summary>BASTION_SONDA_PLAN=1: each choice an animal makes in the log, [plan] time, the animal, what for and where.</summary>
        static readonly bool PlanProbe = Environment.GetEnvironmentVariable("BASTION_SONDA_PLAN") == "1";

        enum Kind { Dog, Wild, Grazer, Cat, Crow, Pigeon }
        enum State { Idle, Walk, Flee, Fly }
        /// <summary>
        /// What it is about (Arturo's play-test of 14/11): wandering a few tiles, going to eat, or to rest under cover; as it
        /// gets there it does it (Settle), and once done it chooses again (Decide).
        /// </summary>
        enum Want { Wander, Food, Shelter }

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
            /// <summary>
            /// What it is about (Want), and what it was last (not to rest twice in a row); how long it stays wary after a fright
            /// (s: it goes to cover next); where to face once there (the bin, the body, out of its corner, back at what scared
            /// it) while `faces`; when a forager makes its next gesture (s).
            /// </summary>
            public Want want, was;
            public float wary;
            public Vector3 face;
            public bool faces;
            public float again;
            /// <summary>Time not yet stepped while out of the squad's sight (stepped a few times a second then, Step).</summary>
            public float unseen;
            /// <summary>
            /// A bird's flight to a place (FlyTo): from, to, how long it has flown and will fly (s), how high it arcs over the
            /// line (m); the roof it stands on (a building's index; −1 the ground), its flock (the pigeons of a spot, who go
            /// together; −1 alone), and the roof it lands on (null, the ground); how long it stays where it is before its next
            /// errand (s); flying off the map for good.
            /// </summary>
            public Vector3 flyFrom, flyTo;
            public float flyTime, flyFor, flyArc;
            public int roof = -1, flock = -1;
            public Building landOn;
            public float stay;
            public bool away;
            /// <summary>The place a roof's cutaway is asked about (Cutaway.Hides: its tile and the roof's storey).</summary>
            public Pos roofAt;
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
        /// <summary>Eating: a grazer grazing or a cat (their controller's Eat_b, while it stands so); a dog, the fox or the wolf, our controller's Comer (Accion 9, a gesture as the others).</summary>
        public const int Graze = 9;

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
            // (Eating at a bin or a body: Arturo's play-test of 14/11. Its number is Graze's, what a grazer's eating is.)
            (Graze, "Comer", "Actions_Standing/_POLYGON_Dog_Action_Standing_Eat"),
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

        /// <summary>
        /// Arturo's play-test of 14/11, «huir de los zombis»: how near one of the dead scares an animal on the ground (tiles; a
        /// rabbit and the deer, keener, DeadFearKeen), and a bird on the ground or on a roof; how near one of the living
        /// scares a bird on a roof (it is out of reach up there: BirdFear on the ground); how long it stays wary once it has
        /// run (s: it looks back, then goes to cover).
        /// </summary>
        const float DeadFear = 5, DeadFearKeen = 6, BirdDeadFear = 3.5f, RoofFear = 3, RoofDeadFear = 2, Wary = 14;
        /// <summary>
        /// How far it looks for a place to eat or to rest (steps, as Reach goes), how far a bird flies to one (tiles), and how
        /// much farther than its fear from the living and the dead such a place must be (tiles): not to be scared off as it
        /// gets there.
        /// </summary>
        const int SeekFood = 14, SeekShelter = 12;
        const float BirdSeek = 14, SeekMargin = 2;
        /// <summary>A bird's flight to a place (FlyTo): its speed over the ground (m/s), the least it takes (s), and the farthest roof it flies to (tiles).</summary>
        const float FlySpeed = 5.5f, FlyLeast = 1.2f, RoofReach = 20;
        /// <summary>How often an animal out of the squad's sight is stepped (s): it is not drawn, and its animator is culled.</summary>
        const float UnseenStep = 0.25f;

        readonly List<Animal> animals = new List<Animal>();
        /// <summary>
        /// Where the living are (the squad, the Cuervos, the lab's), the dead that walk (seen or not: the animals know), and the
        /// bodies of the dead put down (food for the dogs, the wild ones and the crows), as Look last found them.
        /// </summary>
        readonly List<Vector3> threats = new List<Vector3>(), dead = new List<Vector3>(), bodies = new List<Vector3>();
        /// <summary>The roofs a bird can stand on (Populate's: a building's top, not a ruin's nor the frame's), and each flock's pigeons.</summary>
        readonly List<Building> roofs = new List<Building>();
        readonly List<List<Animal>> flocks = new List<List<Animal>>();
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
            return FormattableString.Invariant($"{a.root.name} {a.state} {a.want} wary {a.wary:0.0} doing {a.doing} down {Down(a)} active {a.animator.isActiveAndEnabled} culling {a.animator.cullingMode} clip {clip} move {a.animator.GetFloat("Movement_f"):0.00} shown {a.shown} visible {visible} controller {(a.animator.runtimeAnimatorController != null ? a.animator.runtimeAnimatorController.name : "none")}");
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
            Survey();
            // The budget (Arturo's play-test of 14/11, «demasiados»; AnimalesLab.Cuantos, 45 maps): by the map's open ground
            // (its outdoor tiles; a town's 1,800 or so, the country's 2,700) and its kind, about half as many as before: a
            // town 16 → 8 (one dog, one cat, a roof of crows, a flock of pigeons), the wilds 8 → 4–5, a facility 13 → 5. At
            // the game's camera (24 tiles off it sees some 900 tiles of ground) that is two or three at a time, not five or
            // six, and a flock that takes wing is three or four birds, not a roof's four and a square's five.
            int ground = open.Count;
            bool big = ground >= 2400;
            // A dog only in a town, one (Ana's #11, 1/11), not on a small map; the fox or the wolf in the country, at the base one or two.
            int dogs = town && !home && ground >= 1000 ? 1 : 0;
            foreach (var p in Spots(70, 12, 4, 8).Take(dogs).ToList())
            {
                taken.Add(p);
                AddDog(Strays[(int)(Hash(p.x, p.y, 71) * Strays.Length)], p, Kind.Dog, dogOf);
            }
            // And a cat by a building's wall (any open tile if none is), apart from the dog; two on a big town.
            bool ByWall(int x, int y) => new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) }.Any(q => q.Item1 >= 0 && q.Item2 >= 0 && q.Item1 < s.Width && q.Item2 < s.Height && plan.Owner[q.Item2 * s.Width + q.Item1] >= 0);
            int cats = town && !home ? big ? 2 : 1 : 0;
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
                // Out in the country, in its clearings (Arturo's play-test of 14/11: fewer; it had two of the fox and the wolf,
                // the stag and the doe, a boar and two rabbits): a fox, or a wolf one time in three; in the wilds the stag with
                // the doe beside it or a boar, half and half, at a facility the stag and the doe one time in three; and a rabbit.
                // (By its open ground's count, not its size: every wilds map is 72 × 72, and all had the same.)
                float roll = Hash(ground, s.Height, 93);
                string herd = s.Biome == Biomes.Wilds ? roll < 0.5f ? "deer" : "boar" : roll < 0.34f ? "deer" : null;
                var order = new List<string> { "wild" };
                if (herd != null) order.Add(herd);
                order.Add("rabbit");
                int k = -1;
                foreach (var p in Spots(72, 14, 6, 6).Where(p => Clearing(p.x, p.y)))
                {
                    if (++k >= order.Count) break;
                    taken.Add(p);
                    if (order[k] == "wild") AddDog(Hash(p.x, p.y, 73) < 0.67f ? Wild[0] : Wild[1], p, Kind.Wild, dogOf);
                    else if (order[k] == "deer")
                    {
                        AddDog(Pick(Stags, p.x, p.y, 86), p, Kind.Grazer, dogOf);
                        var by = new[] { (p.x + 1, p.y), (p.x, p.y + 1), (p.x - 1, p.y), (p.x, p.y - 1) }.FirstOrDefault(q => Outdoors(q.Item1, q.Item2));
                        if (by != default) AddDog(Pick(Does, p.x, p.y, 87), by, Kind.Grazer, dogOf);
                    }
                    else AddDog(order[k] == "boar" ? Pick(Boars, p.x, p.y, 88) : Pick(Rabbits, p.x, p.y, 89), p, Kind.Grazer, dogOf);
                }
            }
            // The roofs a bird can stand on.
            roofs.AddRange(plan.Buildings.Where(b => b.kind != BuildingKinds.Ruin && !b.frame && b.top > 0));
            // Crows on the roofs, along a house's ridge or a flat roof's front edge: one roof in a town (two on a big one), two
            // or three on it; one in the country, two (it was two or three roofs, two to four on each: 14/11).
            foreach (var b in roofs.OrderBy(b => Hash(b.x0, b.y0, 74)).Take(home ? 0 : town && big ? 2 : 1).ToList())
            {
                int n = town ? 2 + (int)(Hash(b.x0, b.y0, 75) * 2) : 2;
                for (int k = 0; k < n; k++)
                {
                    float along = (k - (n - 1) / 2f) * (0.45f + 0.3f * Hash(b.x0 + k, b.y0, 76));
                    AddBird(Kind.Crow, RoofSpot(b, along), Hash(b.x0 + k, b.y0, 77) * 360, (uint)(b.index * 31 + k + 1), b);
                }
            }
            // A flock of pigeons pecking about an open spot of a town, three or four (it was five), not on a small map; by
            // night up on the nearest roof, roosting.
            if (town && !home && ground >= 800)
                foreach (var p in Spots(78, 10, 5, 6).Take(1).ToList())
                {
                    taken.Add(p);
                    var flock = new List<Animal>();
                    int n = 3 + (int)(Hash(p.x, p.y, 94) * 2);
                    var roost = Night ? roofs.OrderBy(b => Flat(Middle(b), MapSpace.World(p.x, p.y))).FirstOrDefault() : null;
                    for (int k = 0; k < n; k++)
                    {
                        var at = MapSpace.World(p.x, p.y) + new Vector3(Hash(p.x, k, 79) - 0.5f, 0, Hash(k, p.y, 80) - 0.5f) * MapSpace.Tile * 1.2f;
                        if (roost != null) at = RoofSpot(roost, (k - (n - 1) / 2f) * 0.45f);
                        AddBird(Kind.Pigeon, at, Hash(p.x + k, p.y, 81) * 360, (uint)(p.x * 977 + p.y * 31 + k + 1), roost);
                        var pigeon = animals[animals.Count - 1];
                        pigeon.flock = flocks.Count;
                        pigeon.want = roost != null ? Want.Shelter : Want.Food;
                        // (A while pecking about before they fly up: the flock goes as its first does.)
                        pigeon.stay = 18 + 20 * Hash(p.x, p.y, 95);
                        flock.Add(pigeon);
                    }
                    flocks.Add(flock);
                }
            Debug.Log($"[fauna] {animals.Count(a => a.kind == Kind.Dog)} dogs, {animals.Count(a => a.kind == Kind.Cat)} cats, {animals.Count(a => a.kind == Kind.Wild)} wild, {animals.Count(a => a.kind == Kind.Grazer)} grazing ({string.Join(", ", animals.Where(a => a.kind == Kind.Grazer).Select(a => a.breed))}), {animals.Count(a => a.kind == Kind.Crow)} crows, {animals.Count(a => a.kind == Kind.Pigeon)} pigeons; {ground} outdoor tiles{(Night ? ", by night" : "")}");
        }

        /// <summary>Whether it is night now (Mood's, or a night mission's, the mood perhaps not shown yet): more rest, the birds roosting.</summary>
        bool Night => Mood.IsNight || s?.Mission?.Night == true;

        /// <summary>Building b's middle on the ground (m).</summary>
        static Vector3 Middle(Building b) => MapSpace.World((b.x0 + b.x1) / 2f, (b.y0 + b.y1) / 2f);

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

        /// <summary>A bird at `at`, standing on building `on`'s roof (null, on the ground).</summary>
        Transform AddBird(Kind kind, Vector3 at, float yaw, uint seed, Building on = null)
        {
            var root = new GameObject(kind == Kind.Crow ? "Cuervo" : "Paloma").transform;
            root.SetParent(transform, false);
            root.position = at;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            var (body, left, right) = Bird(root, kind == Kind.Crow ? new Color(0.07f, 0.07f, 0.08f) : new Color(0.42f, 0.44f, 0.48f));
            var bird = new Animal { kind = kind, root = root, body = body, left = left, right = right, state = State.Idle, wait = 0.5f + (seed % 7) * 0.3f, yaw = yaw, rng = seed * 2654435761u + 1, renderers = root.GetComponentsInChildren<Renderer>(), id = animals.Count };
            Perch(bird, on);
            bird.want = on != null ? Want.Shelter : Want.Food;
            // (Not on its errands at once, all of them: a while first, each its own.)
            bird.stay = 6 + (seed % 11) * 1.3f;
            animals.Add(bird);
            return root;
        }

        /// <summary>
        /// Bird `a` standing where it is now, on building `on`'s roof (null, the ground): seen as that roof is (Fog.InSight's
        /// roof), and gone while the cutaway opens it (Cutaway.Hides at the roof's storey, as Cutaway.Track hid it once: it
        /// may fly to another roof now).
        /// </summary>
        void Perch(Animal a, Building on)
        {
            a.roof = on?.index ?? -1;
            a.onRoof = on != null;
            var t = MapSpace.WorldToTile(a.root.position);
            a.roofAt = on != null ? new Pos { X = t.x, Y = t.y, Z = on.storeys } : null;
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

        void Update()
        {
            // (Made before play, a lab's map: its clips' root handed to FaunaBody from now on, as the game's are, not written
            // onto the model, which went on ahead and back with each loop.)
            if (!rooted)
            {
                rooted = true;
                foreach (var a in animals) if (a.animator != null && a.stride != null) a.animator.applyRootMotion = true;
            }
            Step(Time.deltaTime);
        }

        bool rooted;

        public void ShotStep(float dt) => Step(dt);

        uint Next(Animal a)
        {
            a.rng ^= a.rng << 13;
            a.rng ^= a.rng >> 17;
            a.rng ^= a.rng << 5;
            return a.rng;
        }

        float Random01(Animal a) => (Next(a) & 0xffffff) / (float)0x1000000;

        /// <summary>
        /// What the animals keep away from and go to (Arturo's play-test of 14/11): the units with a figure (UnitViews: where
        /// it stands now, walking or not), the living (the squad, and the Cuervos drawn) and the dead that walk and are drawn
        /// (Gameplay, 15/11: not those the fog hides), apart, and
        /// the bodies of the dead put down; and the lab's stand-ins. Before 14/11 every figure was one to run from, the bodies
        /// too.
        /// </summary>
        void Look()
        {
            threats.Clear();
            dead.Clear();
            bodies.Clear();
            var views = Units.UnitViews.Current;
            var units = views?.State?.Units;
            if (units != null)
                foreach (var u in units)
                {
                    if (u.Evacuated || !views.Has(u.Id)) continue;
                    var at = views.Feet(u.Id);
                    if (!u.Alive)
                    {
                        // (Not a soldier's body, nor one that has got up again.)
                        if (u.Team != Teams.Xcom && !u.Reanimated) bodies.Add(at);
                    }
                    // Only those the squad sees scare them (Gameplay, 15/11): a dog bolting must not give away the dead, or a
                    // Cuervo, hidden in the fog: what the fog hides is the game's. The squad's own, always.
                    else if (u.Team == Teams.Xcom || views.Drawn(u.Id)) (Units.Gaits.MovesDead(u.Template) ? dead : threats).Add(at);
                }
            if (ExtraThreats != null) threats.AddRange(ExtraThreats());
            if (ExtraDead != null) dead.AddRange(ExtraDead());
        }

        /// <summary>The nearest of `list` to `from` on the ground, and how far (m; MaxValue if none).</summary>
        static (Vector3 at, float distance) Nearest(List<Vector3> list, Vector3 from)
        {
            Vector3 best = from;
            float least = float.MaxValue;
            foreach (var t in list)
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

        /// <summary>How near one of the living scares ground animal `a` (tiles), and one of the dead.</summary>
        static (float living, float dead) Fears(Animal a)
        {
            float living = Simple(a.kind) ? Grazing(a.breed).fear : a.kind == Kind.Wild ? WildFear : DogFear;
            bool keen = a.kind == Kind.Grazer && !a.breed.StartsWith("Boar");
            return (living, keen ? DeadFearKeen : DeadFear);
        }

        /// <summary>
        /// What scares animal `a` where it is: the one nearest against its fear, of the living within their fear (`living`,
        /// tiles) and of the dead within theirs; and how far it is (m). None: `scared` false.
        /// </summary>
        (bool scared, Vector3 at, float distance) Scare(Animal a, float living, float dead)
        {
            var from = a.root.position;
            var (p, dp) = Nearest(threats, from);
            var (z, dz) = Nearest(this.dead, from);
            float lp = dp / (living * MapSpace.Tile), lz = dz / (dead * MapSpace.Tile);
            if (lp >= 1 && lz >= 1) return (false, from, float.MaxValue);
            return lp <= lz ? (true, p, dp) : (true, z, dz);
        }

        /// <summary>Whether a place is too near the living or the dead for `a` to go to (their fear and SeekMargin, tiles).</summary>
        bool Unsafe(Vector3 at, float living, float dead) =>
            Nearest(threats, at).distance < (living + SeekMargin) * MapSpace.Tile || Nearest(this.dead, at).distance < (dead + SeekMargin) * MapSpace.Tile;

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
            var cut = Cutaway.Current;
            foreach (var a in animals)
            {
                if (a.gone) continue;
                // Out of the squad's sight (not drawn, its animator culled) stepped a few times a second, not every frame:
                // what it costs is for those seen (the brief of 14/11: cheap).
                float step = dt;
                if (!a.shown)
                {
                    a.unseen += dt;
                    step = a.unseen;
                }
                if (step >= UnseenStep || a.shown)
                {
                    a.unseen = 0;
                    a.clock += step;
                    a.wary = Mathf.Max(0, a.wary - step);
                    if (a.kind == Kind.Crow || a.kind == Kind.Pigeon) StepBird(a, step);
                    else StepDog(a, step);
                    if (a.gone) continue;
                }
                // Only where the squad looks, as the enemies (Arturo, 1/11); everywhere with no fog shown (a lab's map). One on
                // a roof as its roof is seen, and not while the cutaway opens it.
                var t = MapSpace.WorldToTile(a.root.position);
                bool perched = a.onRoof && a.state != State.Fly;
                // (A bird flying over a building, as its roof is seen: not gone over a house the squad hasn't been in.)
                bool high = perched || (a.state == State.Fly && t.x >= 0 && t.y >= 0 && t.x < s.Width && t.y < s.Height && plan.Owner[t.y * s.Width + t.x] >= 0);
                bool shown = (fog == null || fog.InSight(t.x, t.y, high)) && !(perched && a.roofAt != null && cut != null && cut.Hides(a.roofAt));
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
                // (Not onto another, nor where another is going: A-30. Nor towards the living or the dead: 14/11.)
                if (Crowded(a, end)) continue;
                var (living, dead) = Fears(a);
                if (Unsafe(end, living, dead)) continue;
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
        /// as far again), the one farthest from `threat` and from the others near it (the living and the dead within Calm:
        /// not from one of the dead into another's arms; Arturo's play-test of 14/11), less a little for the way there, more
        /// for cover there (Shelter's, up to a tile and a half: «hacia refugio»); one on the map's edge goes on off it, a way
        /// out (there it is gone).
        /// </summary>
        Vector3 Away(Animal a, Vector3 threat)
        {
            var from = a.root.position;
            var (steps, back) = Reach(from, Mathf.CeilToInt(Calm * 1.5f), true);
            var near = new List<Vector2Int> { MapSpace.WorldToTile(threat) };
            foreach (var t in threats.Concat(dead))
                if (Flat(t, from) < Calm * MapSpace.Tile) near.Add(MapSpace.WorldToTile(t));
            int best = -1;
            float bestScore = float.MinValue;
            foreach (int i in touched)
            {
                if (steps[i] <= 0) continue;
                int x = i % s.Width, y = i / s.Width;
                float least = float.MaxValue;
                foreach (var t in near) least = Mathf.Min(least, Mathf.Sqrt((x - t.x) * (x - t.x) + (y - t.y) * (y - t.y)));
                float score = least - 0.25f * steps[i] + 0.4f * Mathf.Min(ShelterAt(a, i), 4);
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
        /// cut). Each tile's steps (0 none, the start 1) and the one it is come to from; the tiles reached in `touched`. The
        /// arrays are kept from one call to the next, only what the last reached cleared (it allocated two of the map's size
        /// each time, and the animals ask more often since 14/11): good till the next call.
        /// </summary>
        (int[] steps, int[] back) Reach(Vector3 from, int most, bool edge)
        {
            int w = s.Width, n = w * s.Height;
            if (reachSteps == null || reachSteps.Length != n)
            {
                reachSteps = new int[n];
                reachBack = new int[n];
                touched.Clear();
            }
            var steps = reachSteps;
            var back = reachBack;
            foreach (int i in touched) steps[i] = 0;
            touched.Clear();
            var start = MapSpace.WorldToTile(from);
            if (start.x < 0 || start.y < 0 || start.x >= w || start.y >= s.Height) return (steps, back);
            queue.Clear();
            int first = start.y * w + start.x;
            steps[first] = 1;
            back[first] = -1;
            touched.Add(first);
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
                    touched.Add(ny * w + nx);
                    queue.Enqueue(ny * w + nx);
                }
            }
            return (steps, back);
        }

        int[] reachSteps, reachBack;
        readonly List<int> touched = new List<int>();
        readonly Queue<int> queue = new Queue<int>();

        /// <summary>
        /// What stands round each outdoor tile (Arturo's play-test of 14/11: food and shelter of each kind's own), Survey's:
        /// a bit for each kind of thing on the eight tiles round it (Near), and how many of those eight can't be walked on (a
        /// corner, a nook: `nook`).
        /// </summary>
        [Flags]
        enum Near { None = 0, Bin = 1, Tree = 2, Bush = 4, Fence = 8, Wall = 16, Car = 32, Hay = 64, Patch = 128, Square = 256, Shed = 512, Rock = 1024 }

        Near[] near;
        byte[] nook;
        bool[] outdoor;

        /// <summary>What each outdoor tile has round it (near, nook), once, as the map is made.</summary>
        void Survey()
        {
            int w = s.Width, n = w * s.Height;
            near = new Near[n];
            nook = new byte[n];
            outdoor = new bool[n];
            for (int i = 0; i < n; i++) outdoor[i] = Outdoors(i % w, i / w);
            for (int i = 0; i < n; i++)
            {
                if (!outdoor[i]) continue;
                int x = i % w, y = i / w;
                foreach (var (dx, dy) in Ways)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= s.Height)
                    {
                        nook[i]++;
                        continue;
                    }
                    int j = ny * w + nx;
                    if (!outdoor[j]) nook[i]++;
                    if (plan.Owner[j] >= 0) near[i] |= Near.Wall;
                    near[i] |= Thing(j);
                }
            }
        }

        /// <summary>The kind of thing on tile j (its object's, Decor.AnchorOf), as Near has it.</summary>
        Near Thing(int j)
        {
            if (s.Decor == null || j >= s.Decor.Count || s.Decor[j] == 0) return Near.None;
            int anchor = Decor.AnchorOf(s.Decor, s.Width, j);
            switch (anchor >= 0 ? Decor.DecorOf(s.Decor[anchor])?.Kind : null)
            {
                case DecorKinds.Bin: return Near.Bin;
                case DecorKinds.Tree: case DecorKinds.StreetTree: return Near.Tree;
                case DecorKinds.Bush: case DecorKinds.Planter: return Near.Bush;
                case DecorKinds.Fence: case DecorKinds.Wire: case DecorKinds.GatePost: return Near.Fence;
                case DecorKinds.CarFront: case DecorKinds.CarRear: case DecorKinds.Truck: case DecorKinds.Bus: return Near.Car;
                case DecorKinds.Hay: return Near.Hay;
                case DecorKinds.VegPatch: return Near.Patch;
                case DecorKinds.Bench: case DecorKinds.Kiosk: case DecorKinds.Fountain: case DecorKinds.FountainCorner: case DecorKinds.Statue: return Near.Square;
                case DecorKinds.Shed: return Near.Shed;
                case DecorKinds.Rock: case DecorKinds.Log: case DecorKinds.LogPile: case DecorKinds.Crates: case DecorKinds.Pallets: case DecorKinds.Drums: case DecorKinds.Container: return Near.Rock;
                default: return Near.None;
            }
        }

        static float Of(Near n, Near which, float worth) => (n & which) != 0 ? worth : 0;

        /// <summary>The ground's kind on tile i (GameState.ground: 0 open ground, 1 a street or a track, 3 a yard).</summary>
        int GroundAt(int i) => s.Ground != null && i < s.Ground.Count ? s.Ground[i] : 0;

        /// <summary>
        /// How good outdoor tile i is for ground animal `a` to eat at (0, not at all; Arturo's play-test of 14/11): the dog and
        /// the cat at a bin, the fox and the wolf at a bin too, the fox mousing out in the open; the deer on open grass, more
        /// by hay or a garden's patch; the boar on grass, more under the trees and in a patch; the rabbit on grass, more by a
        /// bush or a patch. A body of the dead put down within a tile and a half, more than anything, for the dog, the fox
        /// and the wolf.
        /// </summary>
        float FoodAt(Animal a, int i)
        {
            if (!outdoor[i]) return 0;
            var n = near[i];
            int g = GroundAt(i);
            float worth = 0;
            switch (a.kind)
            {
                case Kind.Dog:
                case Kind.Cat:
                    worth = Of(n, Near.Bin, 3);
                    break;
                case Kind.Wild:
                    worth = Mathf.Max(Of(n, Near.Bin, 2), a.breed.Contains("Fox") && g == 0 && nook[i] == 0 ? 1 : 0);
                    break;
                case Kind.Grazer:
                    if (g != 0 && g != 3) return 0;
                    if (a.breed.StartsWith("Rabbit")) worth = 1 + Of(n, Near.Bush, 2) + Of(n, Near.Patch, 2);
                    else if (a.breed.StartsWith("Boar")) worth = 1 + Of(n, Near.Tree, 1.5f) + Of(n, Near.Patch, 2);
                    else worth = (nook[i] <= 1 ? 1 : 0.3f) + Of(n, Near.Hay, 2) + Of(n, Near.Patch, 1);
                    // (A yard is a garden only with a patch in it.)
                    if (g == 3 && (n & Near.Patch) == 0) worth = 0;
                    break;
            }
            if ((a.kind == Kind.Dog || a.kind == Kind.Wild) && bodies.Count > 0)
            {
                var at = MapSpace.World(i % s.Width, i / s.Width);
                float d = Nearest(bodies, at).distance;
                if (d > 0.3f * MapSpace.Tile && d < 1.5f * MapSpace.Tile) worth += 4;
            }
            return worth;
        }

        /// <summary>
        /// How sheltered outdoor tile i is for ground animal `a` (0 not at all; Arturo's play-test of 14/11: «refugio»): a
        /// corner or a nook (what can't be walked on round it), and by kind: the cat by a car or a wall, the rabbit in a hedge,
        /// the deer and the boar under the trees, the dog by a wall, a car or a shed, the fox and the wolf under the trees,
        /// in the bushes, by the rocks.
        /// </summary>
        float ShelterAt(Animal a, int i)
        {
            if (!outdoor[i]) return 0;
            var n = near[i];
            float corner = Mathf.Min(nook[i], 4) * 0.5f;
            switch (a.kind)
            {
                case Kind.Cat: return corner + Of(n, Near.Car, 2) + Of(n, Near.Wall, 1.5f) + Of(n, Near.Bush, 1);
                case Kind.Dog: return corner + Of(n, Near.Wall, 1) + Of(n, Near.Car, 1.5f) + Of(n, Near.Shed, 2) + Of(n, Near.Bush, 1) + Of(n, Near.Tree, 1);
                case Kind.Wild: return corner + Of(n, Near.Tree, 2) + Of(n, Near.Bush, 2) + Of(n, Near.Rock, 1.5f) + Of(n, Near.Shed, 1.5f);
                default:
                    if (a.breed.StartsWith("Rabbit")) return corner + Of(n, Near.Bush, 3) + Of(n, Near.Fence, 1) + Of(n, Near.Rock, 1);
                    return corner * 0.5f + Of(n, Near.Tree, 3) + Of(n, Near.Bush, 1.5f) + Of(n, Near.Shed, 1);
            }
        }

        /// <summary>
        /// A place for ground animal `a` to eat at or to rest at (`want`; Arturo's play-test of 14/11): of the outdoor tiles
        /// it can get to (Reach, SeekFood or SeekShelter steps), the best of FoodAt or ShelterAt (at least 1 to eat, 2 to
        /// rest), less a little for the way there, with a little chance (each its own); not one too near the living or the
        /// dead (their fear and SeekMargin), nor where another is or goes (A-30). Its way in `a.way`, where to face once there
        /// in `a.face` (the bin, the body; out of its corner); false if none.
        /// </summary>
        bool Seek(Animal a, Want want)
        {
            var from = a.root.position;
            bool food = want == Want.Food;
            var (steps, back) = Reach(from, food ? SeekFood : SeekShelter, false);
            var (living, deadFear) = Fears(a);
            // (Only those near enough to matter, not every unit of the map for every tile.)
            var far = (Mathf.Max(food ? SeekFood : SeekShelter, 1) + Mathf.Max(living, deadFear) + SeekMargin) * MapSpace.Tile;
            var nearLiving = threats.Where(t => Flat(t, from) < far).ToList();
            var nearDead = dead.Where(t => Flat(t, from) < far).ToList();
            int best = -1;
            float bestScore = float.MinValue;
            foreach (int i in touched)
            {
                float worth = food ? FoodAt(a, i) : ShelterAt(a, i);
                if (worth < (food ? 1 : 2)) continue;
                var at = MapSpace.World(i % s.Width, i / s.Width);
                if (Nearest(nearLiving, at).distance < (living + SeekMargin) * MapSpace.Tile || Nearest(nearDead, at).distance < (deadFear + SeekMargin) * MapSpace.Tile) continue;
                if (Crowded(a, at)) continue;
                // (Nearer better: a rest isn't worth a long way, a meal a little more.)
                float score = worth - (food ? 0.12f : 0.25f) * steps[i] + 0.8f * Random01(a);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0) return false;
            int bx = best % s.Width, by = best / s.Width;
            var end = MapSpace.World(bx, by);
            // Where it faces there: at the body or the bin it eats at; out of its corner as it rests (the way round it open).
            var look = Vector3.zero;
            if (food && bodies.Count > 0 && Nearest(bodies, end).distance < 1.5f * MapSpace.Tile) look = Nearest(bodies, end).at - end;
            else
                foreach (var (dx, dy) in Ways)
                {
                    int nx = bx + dx, ny = by + dy;
                    if (nx < 0 || ny < 0 || nx >= s.Width || ny >= s.Height) continue;
                    int j = ny * s.Width + nx;
                    var toward = new Vector3(dx, 0, -dy).normalized;
                    if (food ? Thing(j) == Near.Bin : outdoor[j]) look += toward;
                }
            look.y = 0;
            a.faces = look.sqrMagnitude > 1e-4f;
            a.face = end + look.normalized * MapSpace.Tile;
            a.way = Route(from, best, back, end);
            a.target = end;
            return true;
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
            var graze = Simple(a.kind) ? Grazing(a.breed) : default;
            var (living, deadFear) = Fears(a);
            var (scared, threat, distance) = Scare(a, living, deadFear);
            if (scared && a.state != State.Flee)
            {
                a.state = State.Flee;
                a.want = Want.Wander;
                a.wary = Wary;
                a.faces = false;
                a.target = Away(a, threat);
                Stand(a);
                Note(a, "flees");
            }
            float speed = Simple(a.kind) ? (a.state == State.Flee ? graze.run : a.state == State.Walk ? graze.walk : 0)
                : a.state == State.Flee ? (a.kind == Kind.Wild ? WildRun : DogRun) : a.state == State.Walk ? (a.kind == Kind.Wild ? WildWalk : DogWalk) : 0;
            a.turning = 0;
            if (a.state == State.Idle)
            {
                // (What pace it still had as it got there, gone in a moment: its legs stop, not at once.)
                a.pace = Mathf.MoveTowards(a.pace, 0, FleeBrake * dt);
                // Facing what it came to (the bin, the body, out of its corner, back at what it ran from), turning on the spot
                // with a slow step round, before it does anything there.
                if (a.faces && !Down(a)) Turn(a, dt);
                if (!a.chosen && !a.faces) Settle(a);
                // (Eating at a bin or a body: the dog's, the fox's and the wolf's gesture again, or a sniff, while it lasts.)
                if (a.chosen && a.want == Want.Food && !Simple(a.kind) && a.animator != null && a.animator.runtimeAnimatorController != null && (a.again -= dt) <= 0)
                {
                    a.again = 2.5f + 1.5f * Random01(a);
                    bool open = a.breed.Contains("Fox") && bodies.Count == 0 && (near[Index(a)] & Near.Bin) == 0;
                    a.doing = Random01(a) < 0.65f ? open ? 5 : Graze : 1;
                    a.animator.SetInteger("Accion", a.doing);
                    a.animator.SetTrigger("Gesto");
                }
                a.wait -= dt;
                if (a.wait <= 0) Decide(a);
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
                    // Still too near: on again. Calm there: if it ran, it turns to look back at what scared it a moment (and
                    // then goes to cover: Decide, wary); else it does what it came for (Settle).
                    bool fled = a.state == State.Flee;
                    a.state = scared ? State.Flee : State.Idle;
                    if (scared) a.target = Away(a, threat);
                    else if (fled)
                    {
                        a.want = Want.Wander;
                        a.face = Nearest(threats.Concat(dead).ToList(), a.root.position).at;
                        a.faces = threats.Count + dead.Count > 0;
                        a.chosen = true;
                        a.doing = 0;
                        a.wait = 2.5f + Random01(a) * 3;
                    }
                    else a.wait = 2 + Random01(a) * 5;
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
        /// What animal `a` goes about next, as it is done where it is (Arturo's play-test of 14/11): wary from a fright, to
        /// cover; else by chance, by day to eat more often than not, by night to rest more often than not (the cat, napping,
        /// more by day too), not twice in a row; then where (Seek): no place to eat, it wanders; none to rest, it rests where
        /// it is if that is sheltered, else wanders.
        /// </summary>
        void Decide(Animal a)
        {
            a.faces = false;
            a.was = a.want;
            float food = 0.45f, shelter = 0.25f, wander = 0.3f;
            if (Night) (food, shelter, wander) = (0.2f, 0.65f, 0.15f);
            else if (a.kind == Kind.Cat) (food, shelter, wander) = (0.3f, 0.45f, 0.25f);
            if (a.was == Want.Shelter) shelter *= 0.2f;
            float roll = Random01(a) * (food + shelter + wander);
            var want = a.wary > 0 ? Want.Shelter : roll < food ? Want.Food : roll < food + shelter ? Want.Shelter : Want.Wander;
            if (LabWant != null && Enum.TryParse(LabWant, out Want asked)) want = asked;
            a.want = want;
            if (want != Want.Wander && Seek(a, want))
            {
                Stand(a);
                a.state = State.Walk;
                Note(a, "goes to");
                return;
            }
            // (Nowhere to go for it: resting where it is, if it is sheltered there; lying or sitting already, it stays so.)
            if (want == Want.Shelter && ShelterAt(a, Index(a)) >= 2)
            {
                a.way.Clear();
                if (a.doing == Sit || a.doing == Lie)
                {
                    a.chosen = true;
                    a.wait = (12 + 14 * Random01(a)) * (Night ? 1.8f : 1);
                }
                else Stand(a);
                Note(a, "rests here");
                return;
            }
            Stand(a);
            a.want = Want.Wander;
            a.target = Somewhere(a, a.root.position, Wander);
            a.state = State.Walk;
            Note(a, "wanders to");
        }

        /// <summary>Animal a's tile (its index in the map's tiles; clamped to the map).</summary>
        int Index(Animal a)
        {
            var t = MapSpace.WorldToTile(a.root.position);
            return Mathf.Clamp(t.y, 0, s.Height - 1) * s.Width + Mathf.Clamp(t.x, 0, s.Width - 1);
        }

        /// <summary>Turns standing animal `a` towards a.face (a slow step round, Stride's), till it faces it.</summary>
        void Turn(Animal a, float dt)
        {
            var to = a.face - a.root.position;
            to.y = 0;
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, was = a.yaw;
            if (to.sqrMagnitude < 1e-4f || Mathf.Abs(Mathf.DeltaAngle(a.yaw, want)) < 10)
            {
                a.faces = false;
                return;
            }
            a.yaw = Mathf.MoveTowardsAngle(a.yaw, want, 150 * dt);
            a.turning = Mathf.Abs(Mathf.DeltaAngle(was, a.yaw)) / dt;
            a.root.rotation = Quaternion.Euler(0, a.yaw, 0);
        }

        /// <summary>BASTION_SONDA_PLAN=1: what animal `a` does now, in the log.</summary>
        void Note(Animal a, string what)
        {
            if (!PlanProbe) return;
            var t = MapSpace.WorldToTile(a.state == State.Idle || a.state == State.Fly ? a.root.position : a.target);
            var at = MapSpace.WorldToTile(a.state == State.Fly ? a.flyTo : a.target);
            Debug.Log(FormattableString.Invariant($"[plan] {Time.time:0.0} {a.root.name} {a.id} {a.want} {what} {at.x},{at.y}{(a.state == State.Idle ? $" (on {t.x},{t.y})" : "")}{(Night ? " night" : "")}{(a.wary > 0 ? " wary" : "")} food {(Simple(a.kind) || Flies(a) ? 0 : FoodAt(a, Mathf.Clamp(at.y, 0, s.Height - 1) * s.Width + Mathf.Clamp(at.x, 0, s.Width - 1))):0.0}"));
        }

        /// <summary>
        /// What animal `a` does as it stops (once), by what it came for (Arturo's play-test of 14/11): eating a while (a
        /// grazer and a cat their Eat; a dog, the fox and the wolf Comer, again and again, now and then a sniff, the fox
        /// digging out in the open); resting (a dog, the fox and the wolf lying or sitting, a cat sitting, a grazer standing
        /// still), longer by night; or, wandering, as it always did: nothing, a gesture (a dog wags its tail, a wolf howls, a
        /// fox digs), or sitting or lying a while. And how long it stays so.
        /// </summary>
        void Settle(Animal a)
        {
            a.chosen = true;
            float night = Night ? 1.8f : 1, r = Random01(a);
            if (a.want == Want.Food)
            {
                a.doing = Graze;
                a.wait = Simple(a.kind) ? (a.kind == Kind.Cat ? 3 + 4 * r : 5 + 6 * r) : 6 + 6 * r;
                a.again = 0;
                return;
            }
            if (a.want == Want.Shelter)
            {
                a.doing = a.kind == Kind.Grazer ? 0 : a.kind == Kind.Cat || Random01(a) < 0.35f ? Sit : Lie;
                a.wait = (a.kind == Kind.Grazer ? 8 + 10 * r : 12 + 14 * r) * night;
                if (a.doing == 0 || Simple(a.kind) || a.animator == null) return;
                a.animator.SetInteger("Accion", a.doing);
                a.animator.SetTrigger("Gesto");
                return;
            }
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

        /// <summary>
        /// A bird (Arturo's play-test of 14/11: food, shelter and running from the dead of their own): standing on a roof
        /// (resting; by night they roost there) or on the ground (eating: a crow at a body of the dead or a bin, the pigeons
        /// on a square's paving), and between the two flying (FlyTo: up, along an arc and down onto its place, gliding between
        /// the beats). Scared (the living within BirdFear on the ground, RoofFear up on a roof; the dead within BirdDeadFear,
        /// RoofDeadFear), it takes wing to a roof away from it, a pigeon with its flock, or off the map if there is none
        /// (and is gone: all of them did so before 14/11). Calm, by day, it goes on its errands (Errand).
        /// </summary>
        void StepBird(Animal a, float dt)
        {
            if (a.state == State.Fly)
            {
                if (!a.away)
                {
                    Glide(a, dt);
                    return;
                }
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
            bool up = a.onRoof;
            var (p, dp) = Nearest(threats, a.root.position);
            var (z, dz) = Nearest(dead, a.root.position);
            float living = (up ? RoofFear : BirdFear) * MapSpace.Tile, deadly = (up ? RoofDeadFear : BirdDeadFear) * MapSpace.Tile;
            if (dp < living || dz < deadly)
            {
                Scatter(a, dp / living <= dz / deadly ? p : z);
                return;
            }
            // Standing: the wings folded along the back; now and then it turns a little, or bobs; on the ground it pecks
            // about, and hops (a crow at what it eats too).
            a.left.localRotation = Quaternion.Euler(0, 75, -8);
            a.right.localRotation = Quaternion.Euler(0, 75, -8);
            a.wait -= dt;
            if (a.wait <= 0)
            {
                a.wait = 0.6f + Random01(a) * 2.2f;
                a.yaw += (Random01(a) - 0.5f) * 70;
                if (!up && Random01(a) < (a.kind == Kind.Pigeon ? 0.4f : 0.25f)) a.root.position += Quaternion.Euler(0, a.yaw, 0) * Vector3.forward * 0.12f;
                a.root.rotation = Quaternion.Euler(0, a.yaw, 0);
            }
            float peck = !up ? Mathf.Max(0, Mathf.Sin(a.clock * (a.kind == Kind.Pigeon ? 3.1f : 2.3f) + a.rng % 5)) * (a.kind == Kind.Pigeon ? 35 : 30) : Mathf.Sin(a.clock * 1.7f + a.rng % 5) * 6;
            a.body.localRotation = Quaternion.Euler(peck, 0, 0);
            a.stay -= dt;
            if (a.stay <= 0) Errand(a);
        }

        /// <summary>
        /// What a calm bird does next, as its time where it is runs out (Arturo's play-test of 14/11): a crow on a roof, by
        /// day, two times in five down to eat (at a body of the dead, at a bin), else it stays; a crow on the ground back up
        /// to a roof; the pigeons, as their flock's first does, down to a square's paving by day and up to a roof to rest in
        /// turn (by night they stay up there).
        /// </summary>
        void Errand(Animal a)
        {
            float r = Random01(a);
            if (a.kind == Kind.Pigeon && a.flock >= 0)
            {
                var flock = flocks[a.flock];
                // (The flock goes as its first does: the others wait for it.)
                if (flock.FirstOrDefault(m => !m.gone) != a)
                {
                    a.stay = 4 + 4 * r;
                    return;
                }
                var spot = Vector3.zero;
                if (a.onRoof && (Night || !Feeding(a, out spot)))
                {
                    a.stay = 15 + 15 * r;
                    return;
                }
                var roost = a.onRoof ? null : Roost(a.root.position);
                if (!a.onRoof && roost == null)
                {
                    a.stay = 10 + 10 * r;
                    return;
                }
                int k = 0;
                float along = RoofAlong(roost, r);
                // (Down or up, as the first stood before it flew: FlyTo takes it off its roof, and the rest must go the same
                // way, not up to a roost there is none of. The 0.12.23 app's exception in a town, 15/11.)
                bool down = a.onRoof;
                foreach (var m in flock)
                {
                    if (m.gone || m.state == State.Fly) continue;
                    m.want = down ? Want.Food : Want.Shelter;
                    if (down) FlyTo(m, spot + new Vector3(Random01(m) - 0.5f, 0, Random01(m) - 0.5f) * MapSpace.Tile * 1.2f, null);
                    else FlyTo(m, RoofSpot(roost, along + (k++ - (flock.Count - 1) / 2f) * 0.45f), roost);
                    // (Not all in the same instant: a beat apart.)
                    m.flyTime = -0.15f * k * Random01(m);
                    Note(m, "flies to");
                }
                return;
            }
            if (a.onRoof)
            {
                // (A lab's BASTION_QUIERE=Food: down to eat each time, by night too.)
                bool hungry = LabWant == nameof(Want.Food) || (!Night && r < 0.4f);
                if (hungry && Feeding(a, out var spot))
                {
                    a.want = Want.Food;
                    FlyTo(a, spot + new Vector3(Random01(a) - 0.5f, 0, Random01(a) - 0.5f) * 0.6f, null);
                    Note(a, "flies to");
                }
                else a.stay = 10 + 15 * Random01(a);
                return;
            }
            var back = Roost(a.root.position);
            if (back == null)
            {
                a.stay = 6 + 6 * r;
                return;
            }
            a.want = Want.Shelter;
            FlyTo(a, RoofSpot(back, RoofAlong(back, r)), back);
            Note(a, "flies to");
        }

        /// <summary>
        /// Bird `a` scared by `threat`: off to the roof that is farthest from it and from the others near, and not too far to
        /// fly (Refuge), with its flock if it is a pigeon's; off the map if there is none.
        /// </summary>
        void Scatter(Animal a, Vector3 threat)
        {
            var flock = a.kind == Kind.Pigeon && a.flock >= 0 ? flocks[a.flock] : new List<Animal> { a };
            var refuge = Refuge(a.root.position, a.roof);
            float along = RoofAlong(refuge, Random01(a));
            int k = 0;
            foreach (var m in flock)
            {
                if (m.gone || m.state == State.Fly) continue;
                m.want = Want.Shelter;
                if (refuge != null) FlyTo(m, RoofSpot(refuge, along + (k++ - (flock.Count - 1) / 2f) * 0.45f), refuge);
                else
                {
                    var away = m.root.position - threat;
                    away.y = 0;
                    away = away.sqrMagnitude < 1e-4f ? m.root.forward : away.normalized;
                    // (Each its own way off, a little.)
                    away = Quaternion.Euler(0, (Random01(m) - 0.5f) * 70, 0) * away;
                    m.velocity = away * FlyAway + Vector3.up * FlyUp;
                    m.yaw = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                    m.root.rotation = Quaternion.Euler(0, m.yaw, 0);
                    m.state = State.Fly;
                    m.away = true;
                    m.onRoof = false;
                    m.clock = Random01(m) * 0.15f;
                }
                // (The others a moment after the one that saw it.)
                if (m != a) m.flyTime = -0.1f - 0.25f * Random01(m);
                Note(m, refuge != null ? "takes wing to" : "takes wing off");
            }
        }

        /// <summary>
        /// A roof to fly to from `from`, away from the living and the dead (Arturo's play-test of 14/11: «refugio»): within
        /// RoofReach, its middle 6 tiles from any of them or more, the farthest from them less a little for the way; not
        /// building `not` (where it stands). Null if none.
        /// </summary>
        Building Refuge(Vector3 from, int not)
        {
            Building best = null;
            float bestScore = float.MinValue;
            foreach (var b in roofs)
            {
                if (b.index == not) continue;
                var m = Middle(b);
                float far = Flat(m, from) / MapSpace.Tile;
                if (far > RoofReach) continue;
                float safe = Mathf.Min(Nearest(threats, m).distance, Nearest(dead, m).distance) / MapSpace.Tile;
                if (safe < 6) continue;
                float score = Mathf.Min(safe, 16) - 0.4f * far;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = b;
                }
            }
            return best;
        }

        /// <summary>The nearest roof to `from` to rest on, within RoofReach and clear of the living and the dead (6 and 4 tiles); null if none.</summary>
        Building Roost(Vector3 from) =>
            roofs.Where(b => Flat(Middle(b), from) <= RoofReach * MapSpace.Tile && Nearest(threats, Middle(b)).distance >= 6 * MapSpace.Tile && Nearest(dead, Middle(b)).distance >= 4 * MapSpace.Tile)
                .OrderBy(b => Flat(Middle(b), from)).FirstOrDefault();

        /// <summary>Where along roof b's ridge or front edge a bird lands (m from its middle, by `r` 0 to 1), half a metre in from its ends.</summary>
        static float RoofAlong(Building b, float r)
        {
            if (b == null) return 0;
            float length = (b.x1 - b.x0 >= b.y1 - b.y0 ? b.x1 - b.x0 + 1 : b.y1 - b.y0 + 1) * MapSpace.Tile;
            return (r - 0.5f) * Mathf.Max(0, length - 2);
        }

        /// <summary>
        /// A place on the ground for bird `a` to eat at (Arturo's play-test of 14/11), within BirdSeek tiles: a crow at a body
        /// of the dead put down, or at a bin; a pigeon on a square's or a street's paving, more by a bench, a kiosk, a fountain
        /// or a bin; open round it, clear of the living (BirdFear and 3 tiles) and the dead (BirdDeadFear and 2), the best
        /// less a little for the way, with a little chance. False if none.
        /// </summary>
        bool Feeding(Animal a, out Vector3 spot)
        {
            spot = default;
            var from = a.root.position;
            var t = MapSpace.WorldToTile(from);
            int reach = Mathf.CeilToInt(BirdSeek), best = -1;
            float bestScore = float.MinValue;
            for (int y = Mathf.Max(0, t.y - reach); y <= Mathf.Min(s.Height - 1, t.y + reach); y++)
                for (int x = Mathf.Max(0, t.x - reach); x <= Mathf.Min(s.Width - 1, t.x + reach); x++)
                {
                    int i = y * s.Width + x;
                    // (The pigeons on open paving; a crow by a bin or a body, a corner too.)
                    if (!outdoor[i] || nook[i] > (a.kind == Kind.Crow ? 4 : 2)) continue;
                    var at = MapSpace.World(x, y);
                    float worth;
                    if (a.kind == Kind.Crow) worth = (bodies.Count > 0 && Nearest(bodies, at).distance < 1.2f * MapSpace.Tile ? 4 : 0) + Of(near[i], Near.Bin, 2);
                    else worth = GroundAt(i) <= 1 ? 1 + Of(near[i], Near.Square, 2) + Of(near[i], Near.Bin, 1) : 0;
                    if (worth < (a.kind == Kind.Crow ? 2 : 1)) continue;
                    float score = worth - 0.05f * Flat(at, from) / MapSpace.Tile + 0.8f * Random01(a);
                    if (score <= bestScore) continue;
                    if (Nearest(threats, at).distance < (BirdFear + 3) * MapSpace.Tile || Nearest(dead, at).distance < (BirdDeadFear + 2) * MapSpace.Tile) continue;
                    bestScore = score;
                    best = i;
                }
            if (best < 0) return false;
            spot = MapSpace.World(best % s.Width, best / s.Width);
            // (A crow at a body: by it, on what it eats.)
            if (a.kind == Kind.Crow && bodies.Count > 0 && Nearest(bodies, spot).distance < 1.2f * MapSpace.Tile) spot = Nearest(bodies, spot).at;
            spot.y = 0;
            return true;
        }

        /// <summary>Bird `a` off to `to` (on building `roof`'s roof, or the ground with null): up, along an arc and down onto it (Glide).</summary>
        void FlyTo(Animal a, Vector3 to, Building roof)
        {
            a.state = State.Fly;
            a.away = false;
            a.flyFrom = a.root.position;
            a.flyTo = to;
            a.flyTime = 0;
            float far = Flat(a.flyFrom, to);
            a.flyFor = Mathf.Max(FlyLeast, far / FlySpeed);
            a.flyArc = Mathf.Clamp(0.25f * far, 1, 4);
            a.landOn = roof;
            a.onRoof = false;
            a.roof = -1;
            a.roofAt = null;
            var way = to - a.flyFrom;
            if (new Vector2(way.x, way.z).sqrMagnitude > 1e-4f) a.yaw = Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg;
            a.root.rotation = Quaternion.Euler(0, a.yaw, 0);
            a.flap = Random01(a);
        }

        /// <summary>
        /// A bird's flight to its place (FlyTo): along the line eased in and out, over an arc (a sine's hump, FlyTo's height),
        /// beating as it goes up (its first third) and as it comes down onto its place (its last fifth, its body up for it),
        /// spread and gliding between; standing there at the end (Perch), a while before its next errand.
        /// </summary>
        void Glide(Animal a, float dt)
        {
            a.flyTime += dt;
            // (Before its flight starts: one of a flock, a beat after the first.)
            if (a.flyTime < 0) return;
            float u = Mathf.Clamp01(a.flyTime / a.flyFor), e = u * u * (3 - 2 * u);
            a.root.position = Vector3.Lerp(a.flyFrom, a.flyTo, e) + Vector3.up * (a.flyArc * Mathf.Sin(Mathf.PI * u));
            a.flap += dt * 11;
            bool beating = u < 0.35f || u > 0.8f;
            float beat = beating ? Mathf.Sin(a.flap * Mathf.PI * 2) * 55 : -6 + Mathf.Sin(a.flap * 0.8f) * 4;
            a.left.localRotation = Quaternion.Euler(0, 0, beat);
            a.right.localRotation = Quaternion.Euler(0, 0, beat);
            a.body.localRotation = Quaternion.Euler(u > 0.85f ? -25 : u < 0.2f ? -15 : -4, 0, 0);
            if (u < 1) return;
            a.root.position = a.flyTo;
            a.state = State.Idle;
            Perch(a, a.landOn);
            float r = Random01(a);
            a.stay = a.onRoof ? 12 + 15 * r : a.kind == Kind.Pigeon ? 20 + 20 * r : 8 + 8 * r;
            a.wait = 0.4f + r;
            a.body.localRotation = Quaternion.identity;
        }
    }
}
