using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// How each kind of unit moves (0.10.1's gaits.ts; Gameplay decides the seconds): a move of one action takes the
    /// gait `one`, of two `two`: the clip it plays and the seconds a tile takes (a diagonal one 1.4 times as long). The clip
    /// plays at the speed that keeps its feet on the ground (ClipSpeed).
    /// </summary>
    public static class Gaits
    {
        public readonly struct Gait
        {
            public readonly string motion;
            public readonly float seconds;
            public Gait(string motion, float seconds)
            {
                this.motion = motion;
                this.seconds = seconds;
            }
        }

        /// <summary>
        /// Metres a second each clip's feet cover at its own speed on our characters (gaits.ts CLIP_SPEED), the living's
        /// (Base Locomotion) and the dead's (Goblin Locomotion).
        /// </summary>
        static readonly Dictionary<string, float> LivingClip = new Dictionary<string, float> { ["walk"] = 1.46f, ["run"] = 2.4f, ["sprint"] = 5.95f, ["crouchWalk"] = 1.48f };
        static readonly Dictionary<string, float> DeadClip = new Dictionary<string, float> { ["walk"] = 1.78f, ["run"] = 2.95f, ["sprint"] = 5.6f, ["crouchWalk"] = 1.48f };
        /// <summary>Tiles a character's metre covers: a tile is 1.5 m, the characters drawn 1.125 times larger.</summary>
        const float TilesPerMetre = 1 / MapSpace.Tile * Figures.CharacterScale;

        /// <summary>The living (the squad, the Cuervos, the VIP): a run for a move (8 tiles: 3 s), a sprint for a dash.</summary>
        static readonly (Gait one, Gait two) Living = (new Gait("run", 0.375f), new Gait("sprint", 0.24f));

        static readonly Dictionary<string, (Gait one, Gait two)> Dead = new Dictionary<string, (Gait, Gait)>
        {
            ["zombie"] = (new Gait("walk", 0.82f), new Gait("walk", 0.62f)),
            ["xenoid"] = (new Gait("run", 0.52f), new Gait("sprint", 0.2f)),
            ["mec"] = (new Gait("walk", 0.8f), new Gait("walk", 0.6f)),
            ["sectoid"] = (new Gait("walk", 0.75f), new Gait("walk", 0.56f)),
            ["bloater"] = (new Gait("walk", 0.86f), new Gait("walk", 0.64f)),
        };

        /// <summary>Moving crouched (hunkered, held): the crouched walk.</summary>
        public static readonly Gait Crouched = new Gait("crouchWalk", 0.71f);

        public static (Gait one, Gait two) Of(string template) => Dead.TryGetValue(template ?? "", out var g) ? g : Living;

        /// <summary>Whether `template` moves like the dead (their clips, Goblin Locomotion's).</summary>
        public static bool MovesDead(string template) => Dead.ContainsKey(template ?? "");

        /// <summary>Mixamo's clips a soldier moves in with a gun (gaits.ts ARMED_CLIP_SPEED): slow ones, with long strides.</summary>
        static readonly Dictionary<string, float> ArmedClip = new Dictionary<string, float> { ["rifleWalk"] = 0.54f, ["rifleRun"] = 1.84f, ["rifleSprint"] = 2.89f };

        /// <summary>How much faster than Synty's clip for `motion` clip `id` plays for its feet to keep the same pace (gaits.ts armedPace).</summary>
        public static float ArmedPace(string motion, string id) =>
            id != null && ArmedClip.TryGetValue(id, out float own) && LivingClip.TryGetValue(motion, out float synty) ? synty / own : 1;

        /// <summary>The speed template's clip for `gait` plays at so its feet keep pace with a tile every `gait.seconds`.</summary>
        public static float ClipSpeed(string template, Gait gait)
        {
            var clips = MovesDead(template) ? DeadClip : LivingClip;
            return 1 / (TilesPerMetre * (clips.TryGetValue(gait.motion, out float m) ? m : 2.4f) * gait.seconds);
        }
    }

    /// <summary>
    /// The units in the scene, as 0.10.1's unitViews.ts: their figures brought in line with the state seen (Sync), and
    /// what UI's event player plays on them (docs/UNITY-ARTE.md). Those to wait for return an Awaitable that ends once
    /// the action has been seen whole; a moment inside one (the shot landing) comes as an Action. Until each has its
    /// Sidekick figure and clips, the figures are stand-ins (a body of the team's colour) that walk, turn, flinch and
    /// fall at the right pace, so the player's rhythm can be built on them.
    /// </summary>
    public sealed partial class UnitViews : MonoBehaviour, IShotStep
    {
        public static UnitViews Current { get; private set; }

        /// <summary>The dead that eat whom they kill (syntyManifest.ts FEEDERS).</summary>
        static readonly HashSet<string> Feeders = new HashSet<string> { "zombie", "xenoid", "mec" };

        sealed class View
        {
            public string id;
            public string template;
            public string team;
            public Transform root;
            public Transform body;
            public Pos place;
            public bool dead;
            public bool shaded;
            /// <summary>On a storey its building's cutaway hides (Cutaway.Hides): its renderers off.</summary>
            public bool cut;
            /// <summary>Its Sidekick figure's body (null: a stand-in), its build ("masc", "femn"), whether it moves as the dead.</summary>
            public FigureAnimator anim;
            public Animator animator;
            public string build = "masc";
            public bool deadKind;
            public string seed;
            /// <summary>The weapon in its hands (UnitViews.Arms: a gun, the pistol, a blade), its muzzle in its frame, how it is held at rest.</summary>
            public Transform weapon;
            public Vector3 muzzle;
            public string rest;
            /// <summary>What it carries by where it rides (weapon, sidearm, belt: syntyFigures.ts CARRIES), each's look, muzzle and middle, and the places on the body they ride in.</summary>
            public readonly Dictionary<string, Transform> carried = new Dictionary<string, Transform>();
            public readonly Dictionary<string, WeaponArt> arts = new Dictionary<string, WeaponArt>();
            public readonly Dictionary<string, Vector3> muzzles = new Dictionary<string, Vector3>();
            public readonly Dictionary<string, float> middles = new Dictionary<string, float>();
            public readonly Dictionary<string, Transform> stows = new Dictionary<string, Transform>();
            /// <summary>Which of them is in its hands, and what that is.</summary>
            public string drawn = "weapon";
            public Held inHand;
            /// <summary>The pistol's turn in the right hand's frame (syntyFigures.ts clipGrip for a pistol); how far into a reload it is (0 none).</summary>
            public Quaternion? pistolGrip;
            public float reload;
            /// <summary>The gun's turn in the right hand's frame, learnt while both hands are on it (syntyFigures.ts clipGrip).</summary>
            public Quaternion? grip;
            /// <summary>How far it is aimed (0 at rest, 1 aimed), going there over AimBlend.</summary>
            public float aim;
            public bool aiming;
            /// <summary>Crouched (hunkered, held: unitViews.ts crouchTarget); eating a fallen body; walking.</summary>
            public bool crouched;
            public bool feeding;
            public bool walking;
            public bool slumped;
            /// <summary>The stance it stands in at ease (the living enemy's, the pistol's way of waiting); null the plain idle.</summary>
            public string stance;
            /// <summary>Where it stood before it went down to eat (it goes back there as it gets up).</summary>
            public Vector3 home;
        }

        readonly Dictionary<string, View> views = new Dictionary<string, View>();
        GameState state;
        string highlighted;
        readonly List<string> fed = new List<string>();

        void Awake() => Current = this;

        void OnEnable() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            foreach (var v in views.Values) v.anim?.Dispose();
        }

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        /// <summary>Captures in edit mode step it by hand (Shots); in play mode it steps itself.</summary>
        public void ShotStep(float dt)
        {
            if (!Application.isPlaying) Step(dt);
        }

        /// <summary>Seconds from rest to aimed (the held clip's crossfade).</summary>
        const float AimBlend = 0.25f;
        /// <summary>
        /// How far the trunk is brought back up while aimed (Gameplay 24/10, Ana's #37: Mixamo's aim doubled over like one
        /// of the wounded): «Rifle Aim» bends forward about this much over Synty's standing legs.
        /// </summary>
        const float AimStraighten = 18;

        void Step(float dt)
        {
            var cutaway = Map.Cutaway.Current;
            foreach (var v in views.Values)
            {
                // What stands on a storey the cutaway hides isn't drawn (buildings.ts Cutaway.hides).
                bool cut = cutaway != null && v.place != null && cutaway.Hides(v.place);
                if (cut != v.cut)
                {
                    v.cut = cut;
                    foreach (var r in v.root.GetComponentsInChildren<Renderer>(true)) r.enabled = !cut;
                }
                v.aim = Mathf.MoveTowards(v.aim, v.aiming && !v.dead && !v.crouched ? 1 : 0, dt / AimBlend);
                v.anim?.Step(dt);
                if (v.aim > 0 && v.inHand == Held.Rifle) Straighten(v, AimStraighten * v.aim);
                HoldNow(v);
            }
        }

        /// <summary>
        /// The trunk turned back `degrees` (half at the spine, half at the chest), the arms forward as much so the gun
        /// stays level, the neck forward by most of it so the eyes stay on the sights: upright, the weight a little ahead.
        /// </summary>
        static void Straighten(View v, float degrees)
        {
            var a = v.animator;
            if (a == null || !a.isHuman) return;
            var right = v.root.right;
            Transform spine = a.GetBoneTransform(HumanBodyBones.Spine), chest = a.GetBoneTransform(HumanBodyBones.Chest);
            if (spine != null) spine.rotation = Quaternion.AngleAxis(-degrees / 2, right) * spine.rotation;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(-degrees / 2, right) * chest.rotation;
            foreach (var bone in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm })
            {
                var arm = a.GetBoneTransform(bone);
                if (arm != null) arm.rotation = Quaternion.AngleAxis(degrees, right) * arm.rotation;
            }
            var neck = a.GetBoneTransform(HumanBodyBones.Neck);
            if (neck != null) neck.rotation = Quaternion.AngleAxis(degrees * 0.7f, right) * neck.rotation;
        }

        // ------------------------------------------------------------ where things are (no waiting)

        /// <summary>The state last shown (Sync), and whether unit `id` has a figure.</summary>
        public GameState State => state;
        public bool Has(string id) => id != null && views.ContainsKey(id);
        public Vector3 Feet(string id) => views.TryGetValue(id, out var v) ? v.root.position : Vector3.zero;
        public Vector3 Chest(string id) => Bone(id, HumanBodyBones.UpperChest) ?? Bone(id, HumanBodyBones.Chest) ?? Feet(id) + Vector3.up * 1.25f;
        public Vector3 Head(string id) => Figures.HeadOf(id) is Transform h ? h.position : Feet(id) + Vector3.up * 1.75f;
        public Vector3 Muzzle(string id) => views.TryGetValue(id, out var v) ? v.weapon != null ? v.weapon.TransformPoint(v.muzzle) : Chest(id) + v.root.forward * 0.55f : Vector3.zero;
        public Vector3 Hand(string id) => Bone(id, HumanBodyBones.RightHand) ?? (views.TryGetValue(id, out var v) ? Chest(id) + v.root.right * 0.3f : Vector3.zero);

        Vector3? Bone(string id, HumanBodyBones bone)
        {
            if (!views.TryGetValue(id, out var v) || v.animator == null || !v.animator.isHuman) return null;
            var t = v.animator.GetBoneTransform(bone);
            return t != null ? t.position : (Vector3?)null;
        }
        /// <summary>Where a body lies (or a unit stands).</summary>
        public Vector3 BodyPos(string id) => Feet(id);

        /// <summary>The way it faces now: Unity's yaw in degrees (0 north, 90 east); null without a figure.</summary>
        public float? FacingNow(string id) => views.TryGetValue(id, out var v) ? v.root.eulerAngles.y : (float?)null;

        /// <summary>Whether a soldier can lean out from `from` without its figure going through anything.</summary>
        public bool ClearFor(Pos from, string id) => true;

        /// <summary>Whether `killer` would eat `victim` as it dies (a feeder of the dead, close enough).</summary>
        public bool CanFeed(string killer, string victim) =>
            views.TryGetValue(killer, out var k) && views.TryGetValue(victim, out var v) && Feeders.Contains(k.template) && !k.dead
            && Vector3.Distance(k.root.position, v.root.position) < 2.5f * MapSpace.Tile;

        // ------------------------------------------------------------ the state seen

        /// <summary>The figures as the state seen says: those that come in made, those gone taken away, each on its tile.</summary>
        public void Sync(GameState view)
        {
            state = view;
            // The enemy shows where the squad has it in sight (teamVisibility), or where UI shows it (ForceVisible, a shade).
            var inSight = Los.TeamVisibility(view, "xcom");
            var seen = new HashSet<string>();
            foreach (var u in view.Units)
            {
                if (u.Evacuated) continue;
                seen.Add(u.Id);
                if (!views.TryGetValue(u.Id, out var v)) views[u.Id] = v = Make(u);
                v.place = new Pos { X = u.Pos.X, Y = u.Pos.Y, Z = u.Pos.Z };
                if (!u.Alive && !v.dead) Lie(v);
                if (u.Alive && v.dead) Stand(v);
                Pose(v, u);
                v.root.position = MapSpace.StandAt(view, v.place);
                int node = Bastion.Engine.Grid.NodeOf(view, u.Pos);
                bool shown = u.Team == "xcom" || forced.Contains(u.Id) || v.shaded || (node >= 0 && node < inSight.Length && inSight[node]);
                if (v.root.gameObject.activeSelf != shown) v.root.gameObject.SetActive(shown);
            }
            foreach (var id in views.Keys.Where(id => !seen.Contains(id)).ToList()) Remove(id);
        }

        View Make(Unit u)
        {
            var root = new GameObject(u.Id).transform;
            root.SetParent(transform, false);
            // 0.10.1: a soldier's campaign id picks the same body everywhere; a skirmish soldier's name; the enemy's id
            // (they share their template's name).
            var view = new View { id = u.Id, template = u.Template, team = u.Team, root = root, seed = u.CampaignId ?? (u.Team == "xcom" ? u.Name : u.Id) };
            view.deadKind = Gaits.MovesDead(u.Template);
            var model = FigureOf(u, view);
            if (model != null)
            {
                model.transform.SetParent(root, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * Figures.CharacterScale;
                view.animator = model.GetComponentInChildren<Animator>();
                var catalog = FigureCatalog.Load();
                bool armed = !view.deadKind && Arm(view, u, catalog);
                bool rifled = armed && Rifled(view);
                // With a gun in the hands it moves in Mixamo's armed clips where there is one (ARMED_MOTIONS), crouches
                // kneeling with it; the rest Synty's (with the pistol, its own: 'own'). It stands in its stance
                // (syntyFigures.ts: the dead drunk, the living enemy aggressive; with the pistol, one of PISTOL_RESTS).
                string stance = view.deadKind ? "drunk" : LivingEnemies.Contains(u.Template) ? "aggressive"
                    : armed && PistolMain(view) && PistolRests.TryGetValue(view.rest ?? "", out var waits) ? waits : null;
                view.stance = stance;
                string ArmedId(string m) => !rifled ? null : m == "crouchIdle" ? "rifleKneel" : Anims.Armed("rifle", m) is string a && a != "own" ? a : null;
                string IdOf(string m) => ArmedId(m) ?? (m == "idle" ? Anims.Stance(view.build, stance) : null) ?? Anims.Motion(view.build, m, view.deadKind);
                if (view.animator != null && catalog != null)
                {
                    view.anim = new FigureAnimator(view.animator, m => catalog.Clip(IdOf(m)), m => Gaits.ArmedPace(m, ArmedId(m)), m => m != "crouchIdle" && ArmedId(m) != null);
                    if (armed) AtRest(view, 0);
                }
                view.body = model.transform;
            }
            else view.body = StandIn(u, root);
            Figures.Register(u.Id, root, view.animator != null && view.animator.isHuman ? view.animator.GetBoneTransform(HumanBodyBones.Head) : root.Find("cabeza"));
            if (u.Facing is double f) root.rotation = Quaternion.Euler(0, YawOf(f), 0);
            return view;
        }

        /// <summary>The living of the enemy (syntyFigures.ts LIVING_ENEMIES): the Cuervos.</summary>
        static readonly HashSet<string> LivingEnemies = new HashSet<string> { "trooper", "officer", "lancer" };

        /// <summary>The sitting-still motion: crouched or standing.</summary>
        static string IdleOf(View v) => v.crouched ? "crouchIdle" : "idle";

        /// <summary>How it holds its gun at rest: kneeling crouched, else its own of RIFLE_RESTS.</summary>
        static string RestOf(View v) => v.crouched ? "rifleKneel" : v.rest;

        /// <summary>
        /// What the state says of how it stands: crouched when hunkered or held (unitViews.ts crouchTarget), a soldier
        /// slumped under half its health (setStance 'slumped').
        /// </summary>
        void Pose(View v, Unit u)
        {
            if (v.anim == null || v.dead) return;
            bool crouched = u.Hunkered || u.Captive;
            bool slumped = u.Team == "xcom" && u.Alive && u.Hp * 2 <= u.MaxHp;
            if (slumped != v.slumped)
            {
                v.slumped = slumped;
                var catalog = FigureCatalog.Load();
                v.anim.SetMotion("idle", catalog?.Clip(Anims.Stance(v.build, slumped ? "slumped" : v.stance) ?? Anims.Motion(v.build, "idle", v.deadKind)));
            }
            if (crouched == v.crouched) return;
            v.crouched = crouched;
            if (!v.walking && !v.feeding) v.anim.Motion(IdleOf(v));
            if (v.weapon != null && !v.aiming) AtRest(v, 0.3f);
        }

        /// <summary>The Sidekick presets each kind of the enemy is drawn as (syntyManifest.ts UNIT_BODIES, of what Unity has so far).</summary>
        static readonly Dictionary<string, (string preset, bool female)[]> Bodies = new Dictionary<string, (string, bool)[]>
        {
            // The dead: Sidekick's five. 0.10.1's living made dead (civilians, police, survivors) and the City Zombies and
            // Boss Zombies figures (the runners, the armoured, the screamer, the bloated one) come next.
            ["dead"] = new[] { ("ApocalypseZombie_01", true), ("ApocalypseZombie_02", false), ("ApocalypseZombie_03", false), ("ApocalypseZombie_04", false), ("ApocalypseZombie_05", false) },
            ["trooper"] = new[] { ("ApocalypseOutlaw_03", false), ("ApocalypseOutlaw_05", false) },
            ["officer"] = new[] { ("ApocalypseOutlaw_02", false) },
            ["lancer"] = new[] { ("ApocalypseOutlaw_01", false), ("ApocalypseOutlaw_04", true) },
        };

        /// <summary>Its figure: a preset whole for the enemy, a soldier put together from their dress for anyone else; null, none.</summary>
        static GameObject FigureOf(Unit u, View view)
        {
            var catalog = FigureCatalog.Load();
            // An objective (the relay: its siren car) is no body: a stand-in until it is drawn as its own.
            if (catalog == null || u.Objective) return null;
            string kind = view.deadKind ? "dead" : u.Template;
            if (Bodies.TryGetValue(kind, out var bodies))
            {
                var (preset, female) = bodies[PickIndex(bodies.Length, view.seed)];
                var prefab = catalog.Preset(preset);
                if (prefab == null) return null;
                view.build = female ? "femn" : "masc";
                var figure = Instantiate(prefab);
                figure.name = preset;
                SoldierFigure.OwnColours(figure);
                return figure;
            }
            try
            {
                // Who has no look gets a body of their sex by their name (0.10.1's sexOfPerson; the campaign's people and
                // relatives come with the campaign).
                var dress = Dress.Of(u.Appearance, view.seed, u.Appearance == null ? Content.SexOfName(u.Name) : null);
                view.build = dress.sex == "f" ? "femn" : "masc";
                var soldier = SoldierFigure.Make(dress, "Soldado");
                // In their colours and with their player's band if Ajustes says so (SquadTint; syntyFigures.ts paint).
                if (u.Team == "xcom") SquadTint.Paint(soldier, u.Appearance, u.Owner is int owner ? SquadTint.ColourOf(owner) : null);
                return soldier;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[units] {u.Id}: no soldier figure ({e.Message})");
                return null;
            }
        }

        /// <summary>
        /// A two-handed gun's model (`art`) under `parent`, at the figures' scale, its muzzle in its own frame; null for a
        /// pistol or a blade (they come next) or one without a model.
        /// </summary>
        public static Transform Gun(WeaponArt art, Transform parent, FigureCatalog catalog, out Vector3 muzzle)
        {
            muzzle = Vector3.zero;
            var prefab = art != null && !art.melee && !art.pistol && catalog != null ? catalog.Weapon(art.fbx) : null;
            if (prefab == null) return null;
            var gun = Instantiate(prefab, parent, false);
            gun.name = "arma";
            // Apocalypse's modular parts left off (its silencers: WEAPON_PARTS puts on what a gun has).
            foreach (var part in gun.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Silencer")).ToList()) part.gameObject.SetActive(false);
            var renderers = gun.GetComponentsInChildren<Renderer>();
            var box = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in renderers)
                foreach (var corner in Corners(r.bounds)) box.Encapsulate(gun.transform.InverseTransformPoint(corner));
            muzzle = art.muzzle != null && art.muzzle.Length >= 3 ? new Vector3(art.muzzle[0], art.muzzle[1], art.muzzle[2]) : new Vector3(0, 0.02f, box.max.z);
            gun.transform.localScale = Vector3.one * Figures.CharacterScale * (art.size > 0 ? art.size : 1);
            foreach (var c in gun.GetComponentsInChildren<Collider>()) DestroyNow(c);
            return gun.transform;
        }

        /// <summary>How one holds a gun at rest (one of RIFLE_RESTS), by its seed.</summary>
        public static string RestFor(string seed)
        {
            var rests = Anims.RifleRests;
            return rests.Count > 0 ? rests[PickIndex(rests.Count, seed + "|rest")] : "rifleIdle";
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int k = 0; k < 8; k++) yield return b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
        }

        /// <summary>
        /// The gun where the hands hold it, after the body's pose: in the right palm, along to the left hand while both are
        /// on it (and that turn learnt in the right hand's frame); held so, rigid in the right hand, while the left lets go
        /// (a hit, a throw).
        /// </summary>
        static void Hold(View v) => Hold(v.animator, v.weapon, v.root, v.dead, ref v.grip);

        /// <summary>Hold(View) for any figure: `animator`'s hands on `weapon`, `root` its upright, `grip` the turn learnt in the right hand.</summary>
        public static void Hold(Animator a, Transform weapon, Transform root, bool dead, ref Quaternion? grip)
        {
            if (weapon == null || a == null) return;
            Transform right = a.GetBoneTransform(HumanBodyBones.RightHand), rightBase = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform left = a.GetBoneTransform(HumanBodyBones.LeftHand), leftBase = a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            if (right == null) return;
            // The middle of each palm: past the wrist, short of the fingers' roots.
            var palm = rightBase != null ? Vector3.Lerp(right.position, rightBase.position, 0.6f) : right.position;
            var support = left != null ? (leftBase != null ? Vector3.Lerp(left.position, leftBase.position, 0.6f) : left.position) : palm;
            float apart = Vector3.Distance(palm, support) / Figures.CharacterScale;
            bool both = !dead && apart > 0.12f && apart < 0.8f;
            Quaternion turn;
            if (both)
            {
                var along = (support - palm).normalized;
                turn = Quaternion.LookRotation(along, Vector3.ProjectOnPlane(root.up, along));
                grip = Quaternion.Inverse(right.rotation) * turn;
            }
            else turn = grip.HasValue ? right.rotation * grip.Value : Quaternion.LookRotation(root.forward, root.up);
            weapon.SetPositionAndRotation(palm, turn);
        }

        /// <summary>wardrobe.ts pick's hash (FNV-1a of the seed) over `n`.</summary>
        static int PickIndex(int n, string seed)
        {
            uint h = 0x811c9dc5;
            foreach (char c in seed ?? "") h = unchecked((h ^ c) * 0x01000193);
            return (int)(h % (uint)n);
        }

        /// <summary>A stand-in where there is no figure: a body of the team's colour, its front marked.</summary>
        static Transform StandIn(Unit u, Transform root)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            body.name = "cuerpo";
            body.SetParent(root, false);
            body.localPosition = Vector3.up * 0.9f;
            body.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            nose.SetParent(body, false);
            nose.localPosition = new Vector3(0, 0.55f, 0.45f);
            nose.localScale = new Vector3(0.3f, 0.15f, 0.4f);
            var colour = u.Team == "xcom" ? new Color(0.25f, 0.45f, 0.75f) : Feeders.Contains(u.Template) ? new Color(0.35f, 0.5f, 0.3f) : new Color(0.7f, 0.3f, 0.25f);
            var m = KitMaterials.New(KitMaterials.Plain, "Cápsula");
            if (m != null) m.color = colour;
            foreach (var r in body.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            foreach (var c in body.GetComponentsInChildren<Collider>()) DestroyNow(c);
            var head = new GameObject("cabeza").transform;
            head.SetParent(root, false);
            head.localPosition = Vector3.up * 1.75f;
            return body;
        }

        void Remove(string id)
        {
            if (views.TryGetValue(id, out var v))
            {
                v.anim?.Dispose();
                DestroyNow(v.root.gameObject);
            }
            views.Remove(id);
            Figures.Unregister(id);
        }

        /// <summary>The engine's facing (radians round the map: 0 along +x, π/2 along +y, map south) as Unity's yaw (0 north, 90 east).</summary>
        static float YawOf(double facing) => 90 + (float)(facing * Mathf.Rad2Deg);

        /// <summary>Lying dead at once (a state that comes with it dead): its death's last pose, or the stand-in on its back.</summary>
        static void Lie(View v)
        {
            v.dead = true;
            if (v.anim != null)
            {
                var death = DeathOf(v, false);
                if (death != null) v.anim.PlayWhole(death, 0, true, death.length);
                return;
            }
            v.body.localRotation = Quaternion.Euler(-90, 0, 0);
            v.body.localPosition = Vector3.up * 0.3f;
        }

        static void Stand(View v)
        {
            v.dead = false;
            if (v.anim != null)
            {
                v.anim.StopWhole(0.4f);
                return;
            }
            v.body.localRotation = Quaternion.identity;
            v.body.localPosition = Vector3.up * 0.9f;
        }

        /// <summary>One of its build's deaths (syntyManifest.ts DEATHS), by its seed; `head`, a shot to the head's.</summary>
        static AnimationClip DeathOf(View v, bool head) => FigureCatalog.Load()?.Clip(DeathIdOf(v, head));

        static string DeathIdOf(View v, bool head)
        {
            var deaths = Anims.Deaths(v.build).Where(d => d.head == head).ToList();
            if (deaths.Count == 0) deaths = Anims.Deaths(v.build).ToList();
            return deaths.Count > 0 ? deaths[PickIndex(deaths.Count, v.seed + "|death")].id : null;
        }

        // ------------------------------------------------------------ at once (no waiting)

        public void Face(string id, Vector3 at)
        {
            if (!views.TryGetValue(id, out var v)) return;
            var d = at - v.root.position;
            d.y = 0;
            if (d.sqrMagnitude > 1e-4f) v.root.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>The shot's kick: Mixamo's fire clip once over the aim.</summary>
        public void Recoil(string id)
        {
            // (The pistol's own clip goes unplayed, as in 0.10.1: its aim held.)
            if (views.TryGetValue(id, out var v) && v.weapon != null && v.inHand == Held.Rifle) v.anim?.PlayUpper(FigureCatalog.Load()?.Clip("rifleFire"), 0.03f);
        }

        /// <summary>Into La Lata: out of sight at once.</summary>
        public void Board(string id)
        {
            if (views.TryGetValue(id, out var v)) v.root.gameObject.SetActive(false);
        }

        /// <summary>A shade (heard, not seen): drawn in the fog, dark, nothing of what it is (0.10.1's shade).</summary>
        public void Shade(string id, bool on)
        {
            if (!views.TryGetValue(id, out var v)) return;
            v.shaded = on;
            if (on) v.root.gameObject.SetActive(true);
        }

        /// <summary>Shown even out of sight (an enemy UI reveals as it acts) until the next Sync says it is in sight anyway.</summary>
        public void ForceVisible(string id)
        {
            forced.Add(id);
            if (views.TryGetValue(id, out var v)) v.root.gameObject.SetActive(true);
        }

        readonly HashSet<string> forced = new HashSet<string>();

        /// <summary>The target being picked, marked red (Arturo 23/10 #3); null, none.</summary>
        public void Highlight(string id) => highlighted = id;

        /// <summary>Aimed at `at` while a target is picked; null, back at rest.</summary>
        public void AimAt(string id, Vector3? at)
        {
            if (at is Vector3 p) Face(id, p);
        }

        /// <summary>The bodies a blast at `at` throws about.</summary>
        public void BlastAt(Vector3 at) { }

        // ------------------------------------------------------------ to wait for

        /// <summary>The seconds each step of `id`'s walk along `path` from `from` takes (for walks played together).</summary>
        public float[] StepSeconds(string id, Pos from, IReadOnlyList<Pos> path, int ap = 1, float pace = 1)
        {
            string template = views.TryGetValue(id, out var v) ? v.template : "";
            var (one, two) = Gaits.Of(template);
            var gait = v != null && v.crouched ? Gaits.Crouched : ap >= 2 ? two : one;
            float tile = gait.seconds / Mathf.Max(0.01f, pace);
            var seconds = new float[path.Count];
            var at = from;
            for (int k = 0; k < path.Count; k++)
            {
                // unitViews.ts stepOf: a diagonal step 1.4 times as long (stairs, ladders and sills come with the buildings).
                bool diagonal = path[k].X != at.X && path[k].Y != at.Y;
                seconds[k] = tile * (diagonal ? 1.4f : 1);
                at = path[k];
            }
            return seconds;
        }

        /// <summary>
        /// Walks `id` along `path` (from where it stands) at its gait, turning the way it goes: one clock for the whole
        /// walk, so what is left of a frame at the end of a step goes into the next. `waits`: seconds it stands before
        /// each step (UI's walk plan, walkPlan.ts: nobody walks through anybody); `passes`: the steps it may pass another
        /// on without waiting (taken, not yet drawn as a swerve).
        /// </summary>
        public Awaitable MoveAlong(string id, IReadOnlyList<Pos> path, int ap = 1, float pace = 1, CancellationToken ct = default) =>
            Walk(id, path, ap, pace, null, null, ct);

        /// <summary>The same with UI's walk plan: `waits` before each step, `passes` the steps it may pass another on.</summary>
        public Awaitable MoveAlong(string id, IReadOnlyList<Pos> path, int ap, float pace, IReadOnlyList<float> waits, IReadOnlyList<bool> passes = null,
            CancellationToken ct = default) => Walk(id, path, ap, pace, waits, passes, ct);

        async Awaitable Walk(string id, IReadOnlyList<Pos> path, int ap, float pace, IReadOnlyList<float> waits, IReadOnlyList<bool> passes, CancellationToken ct)
        {
            if (!views.TryGetValue(id, out var v) || path.Count == 0 || state == null) return;
            // One eating gets up first (unitViews.ts moveAlong: unfeed).
            if (v.feeding) await GetUp(v, ct);
            var seconds = StepSeconds(id, v.place, path, ap, pace);
            var (one, two) = Gaits.Of(v.template);
            var gait = v.crouched ? Gaits.Crouched : ap >= 2 ? two : one;
            v.walking = true;
            float clip = Gaits.ClipSpeed(v.template, gait) * pace;
            // The walk as spans of time: each step's wait (if any), then the step.
            var spans = new List<(int step, bool moving, float length)>();
            for (int k = 0; k < path.Count; k++)
            {
                float hold = waits != null && k < waits.Count ? waits[k] : 0;
                if (hold > 0) spans.Add((k, false, hold));
                spans.Add((k, true, seconds[k]));
            }
            var from = v.root.position;
            var to = from;
            float t = 0;
            float begun = 0;
            int i = -1;
            void Begin(int n)
            {
                var span = spans[n];
                if (!span.moving)
                {
                    v.anim?.Motion("idle");
                    return;
                }
                to = MapSpace.StandAt(state, path[span.step]);
                Face(id, to);
                v.anim?.Motion(gait.motion, clip);
            }
            Begin(i = 0);
            while (true)
            {
                await Awaitable.NextFrameAsync(ct);
                t += Time.deltaTime;
                while (i < spans.Count && t >= begun + spans[i].length)
                {
                    begun += spans[i].length;
                    if (spans[i].moving)
                    {
                        v.place = path[spans[i].step];
                        from = to;
                        v.root.position = to;
                    }
                    if (++i < spans.Count) Begin(i);
                }
                if (i >= spans.Count) break;
                if (spans[i].moving) v.root.position = Vector3.Lerp(from, to, (t - begun) / spans[i].length);
            }
            v.root.position = to;
            v.walking = false;
            v.anim?.Motion(IdleOf(v));
        }

        /// <summary>The weapon raised at `at` (the pistol drawn from its holster, `sidearm`: unitViews.ts raiseWeapon); its number, for LowerWeapon.</summary>
        public async Awaitable<int> RaiseWeapon(string id, Vector3 at, bool sidearm = false, CancellationToken ct = default)
        {
            Face(id, at);
            if (views.TryGetValue(id, out var v) && v.weapon != null)
            {
                if (sidearm) Draw(v, SidearmCarry);
                Aimed(v, AimBlend);
                v.aiming = true;
            }
            await Awaitable.WaitForSecondsAsync(0.35f, ct);
            return ++aims;
        }

        int aims;

        /// <summary>Lowered at rest; the pistol back in its holster after (unitViews.ts lowerWeapon: the main weapon drawn, if nothing raised it again meanwhile).</summary>
        public async Awaitable LowerWeapon(string id, int aim, CancellationToken ct = default)
        {
            if (views.TryGetValue(id, out var v) && v.weapon != null)
            {
                AtRest(v, 0.3f);
                v.aiming = false;
            }
            await Awaitable.WaitForSecondsAsync(0.3f, ct);
            if (aim == aims && v != null && v.weapon != null && v.drawn != MainCarry && !v.aiming)
            {
                Draw(v, MainCarry);
                AtRest(v, 0.2f);
            }
        }

        /// <summary>Out to `to` and back again (a lean out from cover): over `seconds`.</summary>
        public async Awaitable Slide(string id, Vector3 to, float seconds, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v)) return;
            var from = v.root.position;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                v.root.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
                await Awaitable.NextFrameAsync(ct);
            }
            v.root.position = to;
        }

        /// <summary>A blow at `target` (the claw's bite, `eats`: it kills and eats); `onHit` as it lands.</summary>
        public async Awaitable Lunge(string id, string target, bool eats = false, Action onHit = null, CancellationToken ct = default)
        {
            if (views.TryGetValue(target, out var t)) Face(id, t.root.position);
            // The dead lunge as Mixamo's zombie headbutts, over their upper body (its blow lands 0.45 s in); one that kills
            // and eats takes the body by the neck and bites (FEED_CLIPS bite), and holds on to it for Feed.
            if (eats && views.TryGetValue(id, out var e) && e.deadKind && e.anim != null && Anims.Feed("bite") is var (biteId, biteFrom, biteTo)
                && FigureCatalog.Load()?.Clip(biteId) is AnimationClip bite)
            {
                e.home = e.root.position;
                e.anim.PlayWholePart(bite, biteFrom, biteTo, 0.15f, false);
                await Awaitable.WaitForSecondsAsync(0.55f, ct);
                onHit?.Invoke();
                await Awaitable.WaitForSecondsAsync(Mathf.Max(0.2f, biteTo - biteFrom - 0.55f - 0.3f), ct);
                return;
            }
            if (views.TryGetValue(id, out var v) && v.deadKind && v.anim != null && FigureCatalog.Load()?.Clip("zombieHeadbutt") is AnimationClip butt)
            {
                v.anim.PlayUpper(butt, 0.1f);
                await Awaitable.WaitForSecondsAsync(0.45f, ct);
                onHit?.Invoke();
                await Awaitable.WaitForSecondsAsync(0.35f, ct);
                return;
            }
            // A living one strikes with its blade (unitViews.ts lunge: the belt's, its own if it is all it has).
            if (views.TryGetValue(id, out var s) && !s.deadKind && s.anim != null && s.carried.Count > 0)
            {
                views.TryGetValue(target, out var aimed);
                await Strike(s, aimed, onHit, ct);
                return;
            }
            await Awaitable.WaitForSecondsAsync(0.35f, ct);
            onHit?.Invoke();
            await Awaitable.WaitForSecondsAsync(0.3f, ct);
        }

        /// <summary>Hit: 1.5 a critical, 1 a hit, 0.4 a graze.</summary>
        public async Awaitable HitReact(string id, float strength, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v)) return;
            if (v.anim != null)
            {
                // A critical staggers (Sword Combat's hard ones); a graze or a hit, one of Bow Combat's by the body.
                var hits = Anims.Hits(v.build).Where(h => h.hard == strength >= 1.5f).ToList();
                var hit = hits.Count > 0 ? FigureCatalog.Load()?.Clip(hits[PickIndex(hits.Count, v.seed + "|hit" + (++hitCount))].id) : null;
                if (hit != null)
                {
                    float length = v.anim.PlayUpper(hit, 0.08f, strength < 1 ? 1.3f : 1);
                    await Awaitable.WaitForSecondsAsync(Mathf.Min(length, 0.25f + 0.15f * strength + 0.15f), ct);
                    return;
                }
            }
            float seconds = 0.25f + 0.15f * strength;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                v.body.localRotation = Quaternion.Euler(-12 * strength * Mathf.Sin(Mathf.PI * t / seconds), 0, 0);
                await Awaitable.NextFrameAsync(ct);
            }
            v.body.localRotation = Quaternion.identity;
        }

        int hitCount;

        public async Awaitable Die(string id, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v)) return;
            if (v.anim != null)
            {
                v.dead = true;
                var death = DeathOf(v, false);
                if (death != null)
                {
                    // syntyAnims.ts DEATH_BLEND; seen whole, until it lies still (settleOf).
                    float length = v.anim.PlayWhole(death, 0.2f, true);
                    float settle = FigureCatalog.Load()?.Settle(DeathIdOf(v, false)) ?? length;
                    await Awaitable.WaitForSecondsAsync(Mathf.Min(length, settle), ct);
                }
                return;
            }
            for (float t = 0; t < 0.7f; t += Time.deltaTime)
            {
                v.body.localRotation = Quaternion.Euler(-90 * Mathf.SmoothStep(0, 1, t / 0.7f), 0, 0);
                v.body.localPosition = Vector3.up * Mathf.Lerp(0.9f, 0.3f, t / 0.7f);
                await Awaitable.NextFrameAsync(ct);
            }
            Lie(v);
        }

        /// <summary>unitViews.ts FEED_WAIT, FEED_SIDE, FEED_SEEN: before it goes down; how far beside the body's chest it kneels (tiles); how long it is seen eating.</summary>
        const float FeedWait = 0.4f, FeedSide = 0.4f, FeedSeen = 0.8f;

        /// <summary>
        /// The killer eats the fallen (0.10.1, 0.9.7: Mixamo's; its bite was its Lunge): a moment on, it goes down on its
        /// knees beside the body at its chest, across the way it lies, on the side nearer where it stood, coming after it as
        /// it falls; and eats, round and round, until it gets up at its next action (StandUpFed, or as it walks). `onHit`
        /// as it starts eating. Ends once it has been seen eating a little.
        /// </summary>
        public async Awaitable Feed(string killer, string victim, Action onHit = null, CancellationToken ct = default)
        {
            if (!views.TryGetValue(killer, out var k) || !views.TryGetValue(victim, out var body)) return;
            fed.Add(killer);
            var catalog = FigureCatalog.Load();
            if (k.anim == null || catalog == null)
            {
                Face(killer, body.root.position);
                await Awaitable.WaitForSecondsAsync(1.2f, ct);
                return;
            }
            if (!k.feeding && k.home == Vector3.zero) k.home = k.root.position;
            k.feeding = true;
            await Awaitable.WaitForSecondsAsync(FeedWait, ct);
            var start = k.root.position;
            Vector3 Spot()
            {
                var chest = BodyChest(body);
                var hips = Bone(victim, HumanBodyBones.Hips) ?? body.root.position;
                var head = Bone(victim, HumanBodyBones.Head) ?? chest;
                var across = new Vector3(head.z - hips.z, 0, hips.x - head.x);
                if (across.sqrMagnitude < 1e-4f) across = k.home - chest;
                across.y = 0;
                across = across.sqrMagnitude < 1e-4f ? Vector3.forward : across.normalized;
                if (Vector3.Dot(across, k.home - chest) < 0) across = -across;
                var spot = chest + across * FeedSide * MapSpace.Tile;
                spot.y = k.home.y;
                return spot;
            }
            float down = 0.6f;
            if (Anims.Feed("down") is var (downId, downFrom, downTo)) down = Mathf.Max(0.6f, k.anim.PlayWholePart(catalog.Clip(downId), downFrom, downTo, 0.2f, false));
            for (float t = 0; t < down; t += Time.deltaTime)
            {
                k.root.position = Vector3.Lerp(start, Spot(), Mathf.SmoothStep(0, 1, Mathf.Min(1, t / down * 1.3f)));
                Face(killer, BodyChest(body));
                await Awaitable.NextFrameAsync(ct);
            }
            if (Anims.Feed("loop") is var (loopId, loopFrom, loopTo)) k.anim.PlayWholePart(catalog.Clip(loopId), loopFrom, loopTo, 0.25f, true);
            onHit?.Invoke();
            await Awaitable.WaitForSecondsAsync(FeedSeen, ct);
        }

        /// <summary>A body's chest where it lies (its bones), or where it stands.</summary>
        Vector3 BodyChest(View v) => Bone(v.id, HumanBodyBones.Chest) ?? v.root.position;

        /// <summary>Those eating get up (all at once: the dead's turn starts).</summary>
        public async Awaitable StandUpFed(CancellationToken ct = default)
        {
            var up = fed.Where(views.ContainsKey).Select(id => views[id]).Where(v => v.feeding).ToList();
            fed.Clear();
            if (up.Count == 0) return;
            var rising = up.Select(v => GetUp(v, ct)).ToList();
            foreach (var r in rising) await r;
        }

        /// <summary>Off its knees by the body it ate (FEED_CLIPS up), back where it stood, to its stance (unitViews.ts unfeed).</summary>
        async Awaitable GetUp(View v, CancellationToken ct)
        {
            v.feeding = false;
            fed.Remove(v.id);
            var catalog = FigureCatalog.Load();
            float up = 0.35f;
            if (v.anim != null && catalog != null && Anims.Feed("up") is var (upId, upFrom, upTo))
                up = Mathf.Max(0.35f, v.anim.PlayWholePart(catalog.Clip(upId), upFrom, upTo, 0.2f, false));
            var from = v.root.position;
            var home = v.home != Vector3.zero ? v.home : from;
            for (float t = 0; t < up; t += Time.deltaTime)
            {
                v.root.position = Vector3.Lerp(from, home, Mathf.SmoothStep(0, 1, Mathf.Min(1, t / up * 1.25f)));
                if (t > up - 0.3f) v.anim?.StopWhole(0.3f);
                await Awaitable.NextFrameAsync(ct);
            }
            v.root.position = home;
            v.home = Vector3.zero;
            v.anim?.StopWhole(0.2f);
        }

        /// <summary>A reload (unitViews.ts reload: 0.6 s, «Reloading» scrubbed through, the gun held fast in the right hand); none with a pistol.</summary>
        public async Awaitable Reload(string id, CancellationToken ct = default)
        {
            if (views.TryGetValue(id, out var v)) await ReloadNow(v, ct);
            else await Awaitable.WaitForSecondsAsync(ReloadSeconds, ct);
        }

        /// <summary>
        /// Where in Mixamo's throws the hand lets go, and how long before that the game starts them (docs/MIXAMO.md;
        /// 0.10.1's syntyFigures.ts throwMotion, tossStone): the wind-up cut short, the throw whole.
        /// </summary>
        const float GrenadeRelease = 1.8f, GrenadeLead = 0.49f, StoneRelease = 2.4f, StoneLead = 0.24f;

        /// <summary>A stone thrown at `to` (its throw by the free hand: Mixamo's, mirrored); the hand it leaves, for the stone's flight.</summary>
        public async Awaitable<Vector3> TossStone(string id, Vector3 to, CancellationToken ct = default)
        {
            Face(id, to);
            if (views.TryGetValue(id, out var v)) v.anim?.PlayFree(FigureCatalog.Load()?.Clip("throwStone"), 0.1f, 1, StoneRelease - StoneLead);
            await Awaitable.WaitForSecondsAsync(StoneLead, ct);
            return LeftHand(id);
        }

        Vector3 LeftHand(string id) => Bone(id, HumanBodyBones.LeftHand) ?? Hand(id);

        /// <summary>A grenade's throw, by the free left hand (Mixamo's, mirrored); `onHit` as it leaves it (where the arc starts: ThrowHand(id)).</summary>
        public async Awaitable ThrowMotion(string id, Action onHit = null, CancellationToken ct = default)
        {
            float rest = 0.2f;
            if (views.TryGetValue(id, out var v) && v.anim != null && FigureCatalog.Load()?.Clip("tossGrenade") is AnimationClip toss)
                rest = Mathf.Max(0.1f, v.anim.PlayFree(toss, 0.1f, 1, GrenadeRelease - GrenadeLead) - GrenadeLead - 0.2f);
            await Awaitable.WaitForSecondsAsync(GrenadeLead, ct);
            onHit?.Invoke();
            await Awaitable.WaitForSecondsAsync(rest, ct);
        }

        /// <summary>The hand a grenade or a stone leaves: the left (the gun stays in the right).</summary>
        public Vector3 ThrowHand(string id) => LeftHand(id);

        public async Awaitable Rummage(string id, Vector3 at, CancellationToken ct = default)
        {
            Face(id, at);
            await Awaitable.WaitForSecondsAsync(1.2f, ct);
        }

        /// <summary>A blow at a siege's door or barricade on `tile`; `onHit` as it lands.</summary>
        public async Awaitable Smash(string id, Pos tile, Action onHit = null, CancellationToken ct = default)
        {
            if (state != null) Face(id, MapSpace.TileToWorld(state, tile));
            await Awaitable.WaitForSecondsAsync(0.45f, ct);
            onHit?.Invoke();
            await Awaitable.WaitForSecondsAsync(0.25f, ct);
        }

        /// <summary>Falls to `to` (a floor given way, off a roof).</summary>
        public async Awaitable Fall(string id, Pos to, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || state == null) return;
            var from = v.root.position;
            var end = MapSpace.StandAt(state, to);
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                float k = t / 0.5f;
                v.root.position = Vector3.Lerp(from, end, k * k);
                await Awaitable.NextFrameAsync(ct);
            }
            v.root.position = end;
            v.place = to;
        }

        /// <summary>A corpse gets up as one of the dead: the body goes, the new unit's figure stands up where it lay.</summary>
        public async Awaitable Reanimate(string corpse, string zombie, CancellationToken ct = default)
        {
            if (views.TryGetValue(corpse, out var c)) c.root.gameObject.SetActive(false);
            await Awaitable.WaitForSecondsAsync(1f, ct);
        }

        public async Awaitable Scream(string id, CancellationToken ct = default) => await Awaitable.WaitForSecondsAsync(0.9f, ct);

        public async Awaitable Alert(string id, CancellationToken ct = default) => await Awaitable.WaitForSecondsAsync(0.4f, ct);

        static void DestroyNow(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
