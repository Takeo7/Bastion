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
            /// <summary>Drawn not at all for now (faded out, veiled: UnitViews.Fade).</summary>
            public bool gone;
            /// <summary>Its Sidekick figure's body (null: a stand-in), its build ("masc", "femn"), whether it moves as the dead.</summary>
            public FigureAnimator anim;
            public Animator animator;
            public string build = "masc";
            /// <summary>Its figure's size over the figures' (CharacterScale): the special dead's own (UnitViews.Dead).</summary>
            public float size = 1;
            /// <summary>How far into the Chillón's scream it is (0 none, 1 thrown back: UnitViews.Dead.Scream).</summary>
            public float scream;
            /// <summary>The last blow at it (UnitViews.Blows): what its hit and its death go by.</summary>
            public Blow blow;
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
            /// <summary>The pistol at rest kept out of the thigh (syntyFigures.ts spreadPistol).</summary>
            public readonly PistolSpread spread = new PistolSpread();
            public float reload;
            /// <summary>The gun's turn in the right hand's frame, learnt while both hands are on it (syntyFigures.ts clipGrip).</summary>
            public Quaternion? grip;
            /// <summary>How far it is aimed (0 at rest, 1 aimed), going there over AimBlend.</summary>
            public float aim;
            public bool aiming;
            /// <summary>How far its weapon is up for any of those, crouched or not (unitViews.ts max(aim, aimFx)): what the step back at point blank goes by.</summary>
            public float up;
            /// <summary>Aimed at a target being picked (unitViews.ts aimAt, 23/10 #3): where, with the blade on guard, with the pistol; null none.</summary>
            public (Vector3 at, bool melee, bool sidearm)? picking;
            /// <summary>On watch (Unit.Overwatch: the gun up), with the blade on guard (Unit.WatchMelee), as last shown.</summary>
            public bool watch, guard;
            /// <summary>How many aims it has taken (RaiseWeapon, AimAt): a lowering meant for an earlier one leaves a later one be.</summary>
            public int aims;
            /// <summary>
            /// How near what it aims at is (tiles, 0 none: unitViews.ts pose.close); how far ahead of its feet the muzzle of
            /// the gun `reachOf` comes shouldered (tiles: syntyFigures.ts aimReach, measured aimed); how far the body is
            /// stepped back from it now (tiles).
            /// </summary>
            public float close, reach, back;
            public string reachOf;
            /// <summary>Crouched (hunkered, held: unitViews.ts crouchTarget); eating a fallen body; walking; striking with the blade (StrokeOn).</summary>
            public bool crouched;
            public bool feeding;
            public bool walking;
            public bool striking;
            /// <summary>The yaw it turns to (degrees; null, none: where it is) and how fast (unitViews.ts pose.yawTarget, turn).</summary>
            public float? yawTo;
            /// <summary>Out of the way of the shot over a shoulder (Veil).</summary>
            public bool veiled;
            /// <summary>How much of it is drawn (1 all; UnitViews.Fade), how much is to be, and how fast it goes there.</summary>
            public float fade = 1, fadeGoal = 1, fadeRate;
            /// <summary>It has been through Sync once: what comes into sight after that fades in.</summary>
            public bool synced;
            public float turn = Turn;
            public bool slumped;
            /// <summary>The stance it stands in at ease (the living enemy's, the pistol's way of waiting); null the plain idle.</summary>
            public string stance;
            /// <summary>Where it stood before it went down to eat (it goes back there as it gets up).</summary>
            public Vector3 home;
            /// <summary>How many bites it has taken (Lunge): letting go after one is for that one only.</summary>
            public int bites;
            /// <summary>
            /// The dead's arms out in front (UnitViews.Dead DeadArms): its upper chest's turn in the root's frame as it was
            /// made (the bind pose 0.10.1's rest is measured in), and how far a lunge raises them (aim) and closes them (grab).
            /// </summary>
            public Quaternion? chestRest;
            public float reachAim, reachGrab;
            /// <summary>How far into a search's pose it is (Rummage: 0 none, 1 down and rummaging).</summary>
            public float search;
            /// <summary>The gun in its right hand's frame as a search began, and its upper chest's turn then in the root's frame.</summary>
            public Vector3 searchGunAt;
            public Quaternion searchGun = Quaternion.identity, searchChest = Quaternion.identity;
            /// <summary>On a ladder's rungs (ClimbPose), on a window's sill (a landing's crouch kept off it).</summary>
            public bool climbing, onSill;
            /// <summary>Its night rim on (UnitViews.Rim): over which renderers of which body.</summary>
            public bool rimmed;
            public Transform rimBody;
            public List<Renderer> rimRenderers;
            /// <summary>Its stencil bit's renderers (UnitViews.StepMasks), for which body and how much carried.</summary>
            public List<Renderer> maskRenderers;
            public Transform maskBody;
            public int maskCarried = -1;
            /// <summary>Its weapons as last shown (unitViews.ts armsKey: Unit.arms), to see one taken in hand from the pack.</summary>
            public string armsKey;
            /// <summary>The death it went down in (its clip's id), for one that gets up out of its body (Reanimate).</summary>
            public string deathId;
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
        /// <summary>
        /// Shouldered, the trunk turned this much further side-on to the line of fire (degrees, clockwise from above):
        /// «Rifle Aiming Idle» blades the body in its hips, which an upper body mask leaves out of a humanoid's clip (its
        /// body's turn), and three's over Spine_01 keeps. Measured (Stance): the chest 63° off the target in 0.10.1, 22° here.
        /// </summary>
        const float AimBlade = 41;

        /// <summary>The yaw's ease for steps, strikes and aims (unitViews.ts TURN: toward its target by 1 − e^(−dt × this)).</summary>
        const float Turn = 14;

        void Step(float dt)
        {
            var cutaway = Map.Cutaway.Current;
            foreach (var v in views.Values)
            {
                if (v.yawTo is float yawTo && v.root != null)
                {
                    float yaw = v.root.eulerAngles.y, left = Mathf.DeltaAngle(yaw, yawTo);
                    if (Mathf.Abs(left) < 0.2f) v.yawTo = null;
                    v.root.rotation = Quaternion.Euler(0, v.yawTo == null ? yawTo : yaw + left * (1 - Mathf.Exp(-dt * v.turn)), 0);
                }
                // What stands on a storey the cutaway hides isn't drawn (buildings.ts Cutaway.hides).
                StepFade(v, dt);
                bool cut = cutaway != null && v.place != null && cutaway.Hides(v.place);
                bool gone = v.fade <= 0;
                if (cut != v.cut || gone != v.gone)
                {
                    v.cut = cut;
                    v.gone = gone;
                    foreach (var r in v.root.GetComponentsInChildren<Renderer>(true)) r.enabled = !cut && !gone;
                    if (!cut && !gone && v.fade < 1) foreach (var w in v.carried.Values) if (w != null) Show(w, v.fade >= 0.5f);
                }
                bool up = Up(v) && !v.dead;
                v.aim = Mathf.MoveTowards(v.aim, up && !v.crouched ? 1 : 0, dt / AimBlend);
                v.up = Mathf.MoveTowards(v.up, up ? 1 : 0, dt / AimBlend);
                StepBack(v, dt);
                v.anim?.Step(dt);
                if (v.aim > 0 && v.inHand == Held.Rifle)
                {
                    Blade(v, AimBlade * v.aim);
                    Straighten(v, AimStraighten * v.aim);
                }
                HoldNow(v, dt);
                SearchPose(v);
                ClimbPose(v);
                Reach(v);
                if (v.deadKind) DeadPose(v);
            }
            StepFlashes(dt);
            StepRims();
            StepMasks();
            StepMarks(dt);
        }

        /// <summary>
        /// The trunk turned back `degrees` (half at the spine, half at the chest), the arms forward as much so the gun
        /// stays level, the neck forward by most of it so the eyes stay on the sights: upright, the weight a little ahead.
        /// </summary>
        /// <summary>The trunk turned `degrees` about the upright, half at the spine and half at the chest, what it holds with it.</summary>
        static void Blade(View v, float degrees)
        {
            var a = v.animator;
            if (a == null || !a.isHuman) return;
            foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest })
                if (a.GetBoneTransform(bone) is Transform b) b.rotation = Quaternion.AngleAxis(degrees / 2, Vector3.up) * b.rotation;
        }

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
        /// <summary>Whether unit `id`'s figure is on the map now (the squad, an enemy in sight, a shade).</summary>
        public bool Shown(string id) => id != null && views.TryGetValue(id, out var v) && v.root != null && v.root.gameObject.activeSelf;
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
                // A weapon from the backpack taken in hand (the engine's weaponEquipped): Equip.
                if (u.Alive && !v.deadKind && v.anim != null && ArmsKey(u) != v.armsKey)
                {
                    v.armsKey = ArmsKey(u);
                    _ = Equip(v, u);
                }
                if (!u.Alive && !v.dead) Lie(v);
                if (u.Alive && v.dead) Stand(v);
                Pose(v, u);
                v.root.position = MapSpace.StandAt(view, v.place);
                int node = Bastion.Engine.Grid.NodeOf(view, u.Pos);
                bool shown = u.Team == "xcom" || forced.Contains(u.Id) || v.shaded || (node >= 0 && node < inSight.Length && inSight[node]);
                // An enemy coming into sight fades in (not on the first look at the map: what is there at the start just is).
                if (shown && !v.root.gameObject.activeSelf && v.synced && u.Team != "xcom" && u.Alive) StartFade(v, FadeIn);
                if (v.root.gameObject.activeSelf != shown) v.root.gameObject.SetActive(shown);
                v.synced = true;
            }
            foreach (var id in views.Keys.Where(id => !seen.Contains(id)).ToList()) Remove(id);
        }

        View Make(Unit u)
        {
            var root = new GameObject(u.Id).transform;
            root.SetParent(transform, false);
            // 0.10.1: a soldier's campaign id picks the same body everywhere; a skirmish soldier's name; the enemy's id
            // (they share their template's name).
            var view = new View { id = u.Id, template = u.Template, team = u.Team, root = root, seed = u.CampaignId ?? (u.Team == "xcom" ? u.Name : u.Id), armsKey = ArmsKey(u) };
            view.deadKind = Gaits.MovesDead(u.Template);
            var model = FigureOf(u, view);
            if (model != null)
            {
                model.transform.SetParent(root, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * Figures.CharacterScale * view.size;
                view.body = model.transform;
                view.animator = model.GetComponentInChildren<Animator>();
                if (view.deadKind && view.animator != null && view.animator.isHuman && ChestOf(view.animator) is Transform chest)
                    view.chestRest = Quaternion.Inverse(root.rotation) * chest.rotation;
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
            // On watch: the gun up; with the blade (Unit.WatchMelee), drawn from the belt and held on guard (unitViews.ts sync).
            bool watch = u.Overwatch && u.Alive, guard = watch && u.WatchMelee == true;
            if (watch != v.watch || guard != v.guard)
            {
                v.watch = watch;
                v.guard = guard;
                if (!v.walking && !v.feeding && !v.aiming && !v.striking && v.picking == null && v.weapon != null)
                {
                    Draw(v, guard ? BeltCarry : MainCarry);
                    Settle(v, 0.3f);
                }
            }
            if (crouched == v.crouched) return;
            v.crouched = crouched;
            if (!v.walking && !v.feeding) v.anim.Motion(IdleOf(v));
            if (v.weapon != null) Settle(v, 0.3f);
        }

        /// <summary>The Sidekick presets each kind of the enemy is drawn as (syntyManifest.ts UNIT_BODIES, of what Unity has so far).</summary>
        static readonly Dictionary<string, (string preset, bool female)[]> Bodies = new Dictionary<string, (string, bool)[]>
        {
            // The dead: Sidekick's five, then the living made dead (UndeadFigure: civilians, police, survivors; syntyManifest.ts
            // UNIT_BODIES zombie, 17 in all, picked among as one). The special ones are City Zombies' and Boss Zombies' (DeadBodies).
            ["dead"] = new[] { ("ApocalypseZombie_01", true), ("ApocalypseZombie_02", false), ("ApocalypseZombie_03", false), ("ApocalypseZombie_04", false), ("ApocalypseZombie_05", false) }
                .Concat(UndeadFigure.Bodies.Select(b => (b.id, b.female))).ToArray(),
            ["trooper"] = new[] { ("ApocalypseOutlaw_03", false), ("ApocalypseOutlaw_05", false) },
            ["officer"] = new[] { ("ApocalypseOutlaw_02", false) },
            ["lancer"] = new[] { ("ApocalypseOutlaw_01", false), ("ApocalypseOutlaw_04", true) },
        };

        /// <summary>Every preset the enemy is drawn as, the living made dead among them (FigureCatalogMake keeps only these).</summary>
        public static IEnumerable<string> PresetsUsed => Bodies.Values.SelectMany(b => b.Select(p => p.preset)).Distinct();

        /// <summary>Its figure: a preset whole for the enemy, a soldier put together from their dress for anyone else; null, none.</summary>
        static GameObject FigureOf(Unit u, View view)
        {
            var catalog = FigureCatalog.Load();
            // An objective (the relay: its siren car) is no body: a stand-in until it is drawn as its own.
            if (catalog == null || u.Objective) return null;
            // The special dead: City Zombies' and Boss Zombies' (UnitViews.Dead); Sidekick's dead if their prefab is missing.
            if (DeadFigureOf(u, view, catalog) is GameObject special) return special;
            string kind = view.deadKind ? "dead" : u.Template;
            if (Bodies.TryGetValue(kind, out var bodies))
            {
                var (preset, female) = bodies[PickIndex(bodies.Length, view.seed)];
                view.build = female ? "femn" : "masc";
                var prefab = catalog.Preset(preset);
                if (prefab == null)
                {
                    // (The living made dead are only ever baked: FigureCatalogMake.Run, and AppBuild won't build without them.)
                    Debug.LogError($"[figures] no {preset} in the catalog (Bastion.EditorTools.FigureCatalogMake.Run)");
                    return null;
                }
                var figure = Instantiate(prefab);
                figure.name = preset;
                if (kind == "dead") SoldierPalette.DullDead(figure);
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
                v.deathId = DeathIdOf(v, false);
                var death = FigureCatalog.Load()?.Clip(v.deathId);
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

        /// <summary>Turned to `at`, easing round (unitViews.ts face: TURN).</summary>
        public void Face(string id, Vector3 at)
        {
            if (!views.TryGetValue(id, out var v)) return;
            var d = at - v.root.position;
            d.y = 0;
            if (d.sqrMagnitude > 1e-4f) TurnTo(v, Quaternion.LookRotation(d).eulerAngles.y);
        }

        static void TurnTo(View v, float yaw, float turn = Turn)
        {
            v.yawTo = yaw;
            v.turn = turn;
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
            if (!views.TryGetValue(id, out var v)) return;
            // Out of the fog (a step into sight, a contact): faded in, as Sync does (playtest 20/10 #10).
            if (!v.root.gameObject.activeSelf && v.synced && !v.dead) StartFade(v, FadeIn);
            v.root.gameObject.SetActive(true);
        }

        readonly HashSet<string> forced = new HashSet<string>();

        /// <summary>
        /// Out of the way of the shot over a shoulder (unitViews.ts clearView, syntyFigures.ts veil): `on`, not drawn; off,
        /// back. UI says who and when each frame the shot is up (Gameplay 26/10). For now at once (0.10.1 faded it, dithered,
        /// over 0.2 s).
        /// </summary>
        public void Veil(string id, bool on)
        {
            if (!views.TryGetValue(id, out var v) || v.veiled == on) return;
            v.veiled = on;
            // Dithered out over VeilSeconds, or back in (syntyFigures.ts veil).
            v.fadeGoal = on ? 0 : 1;
            v.fadeRate = 1 / VeilSeconds;
        }

        /// <summary>
        /// One coming into the battle (arrivals.ts, Gameplay 26/10: UI's Arrivals says who, by which way, when): its figure
        /// made before the state has it, at points[0] past the map's edge, walked in to its tile through the rest of
        /// `points` (world, a tile's middle each), `steps` seconds each, waiting `waits` before each, passing another on
        /// `passes`; the next Sync finds it where it stands.
        /// </summary>
        public async Awaitable Arrive(Unit u, IReadOnlyList<Vector3> points, IReadOnlyList<float> steps, IReadOnlyList<float> waits = null, IReadOnlyList<bool> passes = null,
            CancellationToken ct = default)
        {
            if (u == null || points == null || points.Count == 0) return;
            if (!views.TryGetValue(u.Id, out var v)) views[u.Id] = v = Make(u);
            v.root.position = points[0];
            v.root.gameObject.SetActive(true);
            if (points.Count > 1)
            {
                var d = points[1] - points[0];
                d.y = 0;
                if (d.sqrMagnitude > 1e-6f) v.root.rotation = Quaternion.LookRotation(d);
            }
            v.yawTo = null;
            // Its feet at the pace it walks in at (unitViews.ts arrive: a tile in its straight legs' time).
            var (one, _) = Gaits.Of(v.template);
            float tile = one.seconds;
            if (steps != null)
            {
                tile = steps.Count > 0 ? steps[0] : tile;
                for (int k = 1; k < steps.Count; k++)
                    if (steps[k] > 0)
                    {
                        tile = steps[k];
                        break;
                    }
            }
            float clip = Gaits.ClipSpeed(v.template, new Gaits.Gait(one.motion, Mathf.Max(0.05f, tile)));
            // To the right of its way through a point where it goes by another, on the flat; never where it stops (Swerve).
            var way = new List<Vector3>(points);
            for (int k = 1; k + 1 < points.Count; k++)
            {
                if (passes == null || k >= passes.Count || !passes[k]) continue;
                Vector3 before = points[k - 1], at = points[k], next = points[k + 1];
                if (Mathf.Abs(before.y - at.y) > 0.05f * MapSpace.Tile || Mathf.Abs(next.y - at.y) > 0.05f * MapSpace.Tile) continue;
                float dx = next.x - before.x, dz = next.z - before.z, length = Mathf.Sqrt(dx * dx + dz * dz);
                // (0.10.1's (−dz, dx), its z south: Unity's (dz, −dx).)
                if (length > 0) way[k] = at + new Vector3(dz / length, 0, -dx / length) * (Swerve * MapSpace.Tile);
            }
            v.walking = true;
            v.anim?.Motion(one.motion, clip);
            for (int k = 1; k < way.Count; k++)
            {
                float hold = waits != null && k - 1 < waits.Count ? waits[k - 1] : 0;
                if (hold > 0.02f)
                {
                    v.anim?.Motion(IdleOf(v));
                    await Awaitable.WaitForSecondsAsync(hold, ct);
                    v.anim?.Motion(one.motion, clip);
                }
                Vector3 from = v.root.position, to = way[k];
                var heading = to - from;
                heading.y = 0;
                if (heading.sqrMagnitude > 1e-6f) TurnTo(v, Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg);
                float seconds = steps != null && k - 1 < steps.Count ? Mathf.Max(0.01f, steps[k - 1]) : one.seconds;
                for (float t = 0; t < seconds;)
                {
                    await Awaitable.NextFrameAsync(ct);
                    t = Mathf.Min(seconds, t + Time.deltaTime);
                    v.root.position = Vector3.Lerp(from, to, t / seconds);
                }
            }
            v.place = new Pos { X = u.Pos.X, Y = u.Pos.Y, Z = u.Pos.Z };
            v.walking = false;
            v.anim?.Motion(IdleOf(v));
        }

        /// <summary>
        /// One coming into the battle out of the ground on its tile (arrivals.ts, unitViews.ts getUp: far from any edge):
        /// crouched in the dust UI kicks up, and up over 0.5 s.
        /// </summary>
        public async Awaitable Rise(Unit u, CancellationToken ct = default)
        {
            if (u == null) return;
            if (!views.TryGetValue(u.Id, out var v)) views[u.Id] = v = Make(u);
            if (state != null) v.root.position = MapSpace.StandAt(state, u.Pos);
            v.place = new Pos { X = u.Pos.X, Y = u.Pos.Y, Z = u.Pos.Z };
            v.root.gameObject.SetActive(true);
            if (v.anim == null)
            {
                await Awaitable.NextFrameAsync(ct);
                return;
            }
            // Down at once (its rifle held kneeling), then up as the crouch eases off.
            bool was = v.crouched;
            v.crouched = true;
            v.anim.Motion(IdleOf(v));
            if (v.weapon != null) Settle(v, 0);
            v.anim.Step(1);
            v.crouched = was;
            v.anim.Motion(IdleOf(v));
            if (v.weapon != null) Settle(v, 0.5f);
            await Awaitable.WaitForSecondsAsync(0.5f, ct);
        }

        /// <summary>The target being picked, marked red (Arturo 23/10 #3); null, none.</summary>
        public void Highlight(string id) => highlighted = id;

        /// <summary>
        /// Aimed at `at` while its target is picked (unitViews.ts aimAt, Arturo 23/10 #3: over the shoulder it stood at
        /// rest): turned to it, the gun up (`sidearm`, the pistol beside a main gun: the shot picked is FiresSidearm's);
        /// `melee`, the blade drawn and on guard towards it. Null: back to how it stands. The same again changes nothing;
        /// another target, it turns to it with the gun kept up. A shot then goes on from the aim (RaiseWeapon), not lowered first.
        /// </summary>
        public void AimAt(string id, Vector3? at, bool melee = false, bool sidearm = false)
        {
            if (!views.TryGetValue(id, out var v) || v.dead) return;
            var was = v.picking;
            if (!(at is Vector3 p))
            {
                if (was == null) return;
                v.picking = null;
                _ = Lowered(v, ++v.aims);
                return;
            }
            if (was is { } w && w.melee == melee && w.sidearm == sidearm && (w.at - p).sqrMagnitude < 1e-4f) return;
            v.picking = (p, melee, sidearm);
            ++v.aims;
            Face(id, p);
            if (v.weapon == null) return;
            string carry = melee ? BeltCarry : sidearm ? SidearmCarry : MainCarry;
            if (v.drawn != carry) Draw(v, carry);
            v.close = melee ? 0 : Flat(p - v.root.position) / MapSpace.Tile;
            Settle(v, 0.3f);
        }

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
            Walk(id, path, ap, pace, null, null, null, ct);

        /// <summary>The same with UI's walk plan: `waits` before each step, `passes` the steps it may pass another on.</summary>
        public Awaitable MoveAlong(string id, IReadOnlyList<Pos> path, int ap, float pace, IReadOnlyList<float> waits, IReadOnlyList<bool> passes = null,
            CancellationToken ct = default) => Walk(id, path, ap, pace, waits, passes, null, ct);

        /// <summary>
        /// The same, `onStep` told of each tile it gets to as it gets there, where it stands in the world (unitViews.ts
        /// moveAlong's onStep: the footsteps' sound, docs/UNITY-SONIDO.md §9).
        /// </summary>
        public Awaitable MoveAlong(string id, IReadOnlyList<Pos> path, int ap, float pace, Action<Vector3> onStep, CancellationToken ct = default) =>
            Walk(id, path, ap, pace, null, null, onStep, ct);

        /// <summary>The same with UI's walk plan and `onStep`.</summary>
        public Awaitable MoveAlong(string id, IReadOnlyList<Pos> path, int ap, float pace, IReadOnlyList<float> waits, IReadOnlyList<bool> passes, Action<Vector3> onStep,
            CancellationToken ct = default) => Walk(id, path, ap, pace, waits, passes, onStep, ct);

        /// <summary>
        /// Plain steps one after another are walked as one way (unitViews.ts, playtest 23/10 #6: a jolt at every tile and a
        /// sharp turn at each corner): its corners rounded off within this of the tile's middle (tiles), this many samples
        /// round; and one going by another on a tile goes this far to the right of its way there (walkPlan.ts passes).
        /// </summary>
        const float WalkRound = 0.38f, Swerve = 0.24f;
        const int WalkRoundSteps = 8;

        async Awaitable Walk(string id, IReadOnlyList<Pos> path, int ap, float pace, IReadOnlyList<float> waits, IReadOnlyList<bool> passes, Action<Vector3> onStep,
            CancellationToken ct)
        {
            if (!views.TryGetValue(id, out var v) || path.Count == 0 || state == null) return;
            // One eating gets up first (unitViews.ts moveAlong: unfeed).
            if (v.feeding) await GetUp(v, ct);
            var (one, two) = Gaits.Of(v.template);
            var gait = v.crouched ? Gaits.Crouched : ap >= 2 ? two : one;
            float tile = gait.seconds / Mathf.Max(0.01f, pace);
            v.walking = true;
            float clip = Gaits.ClipSpeed(v.template, gait) * pace;
            float Hold(int k) => waits != null && k < waits.Count ? waits[k] : 0;
            Pos Before(int k) => k == 0 ? v.place : path[k - 1];
            // (The steps on stairs, onto a sill and off it, up and down a ladder or off an edge are their own: StepAcross.)
            bool Flat(Pos a, Pos b) => StepOf(a, b, tile).kind == StepKind.Flat;
            v.anim?.Motion(gait.motion, clip);
            for (int k = 0; k < path.Count;)
            {
                // Its turn to go there (walkPlan.ts): it stands, turned to it, until then.
                if (Hold(k) > 0.02f)
                {
                    Face(id, MapSpace.StandAt(state, path[k]));
                    v.anim?.Motion("idle");
                    await Awaitable.WaitForSecondsAsync(Hold(k), ct);
                    v.anim?.Motion(gait.motion, clip);
                }
                if (!Flat(Before(k), path[k]))
                {
                    var (kind, across) = StepOf(Before(k), path[k], tile);
                    await StepAcross(v, id, kind, Before(k), path[k], across, gait, tile, clip, ct);
                    // On its storey once there (docs/FASE6.md): the cutaway checks this.
                    v.place = path[k];
                    onStep?.Invoke(v.root.position);
                    k++;
                    continue;
                }
                int m = k + 1;
                    while (m < path.Count && Hold(m) <= 0.02f && Flat(path[m - 1], path[m])) m++;
                // Through the tiles' middles, to one side where it goes by another (never on the last, where it stands).
                var points = new List<Vector3> { v.root.position };
                for (int n = k; n < m; n++)
                {
                    var at = MapSpace.StandAt(state, path[n]);
                    if (passes != null && n < passes.Count && passes[n] && n + 1 < path.Count && Flat(Before(n), path[n]) && Flat(path[n], path[n + 1]))
                    {
                        float dx = path[n + 1].X - Before(n).X, dz = path[n + 1].Y - Before(n).Y, length = Mathf.Sqrt(dx * dx + dz * dz);
                        // (0.10.1's (−dz, dx) to the right, in its frame: z south, Unity's north.)
                        if (length > 0) at += new Vector3(-dz / length, 0, -dx / length) * (Swerve * MapSpace.Tile);
                    }
                    points.Add(at);
                }
                var way = RoundedWay(points);
                int passed = 0;
                void Pass(float d)
                {
                    while (passed < m - k && d >= way.marks[passed] - 1e-4f)
                    {
                        v.place = path[k + passed];
                        onStep?.Invoke(v.root.position);
                        passed++;
                    }
                }
                // At the gait's one speed all along (a diagonal as fast as a straight step), facing where it heads.
                float seconds = way.length / MapSpace.Tile * tile;
                for (float t = 0; t < seconds;)
                {
                    await Awaitable.NextFrameAsync(ct);
                    t = Mathf.Min(seconds, t + Time.deltaTime);
                    float d = seconds > 0 ? t / seconds * way.length : way.length;
                    var (at, heading) = OnWay(way, d);
                    v.root.position = at;
                    if (heading.x * heading.x + heading.z * heading.z > 1e-8f) TurnTo(v, Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg);
                    Pass(d);
                }
                Pass(float.PositiveInfinity);
                k = m;
            }
            v.root.position = MapSpace.StandAt(state, path[path.Count - 1]);
            v.walking = false;
            v.onSill = false;
            v.anim?.Motion(IdleOf(v));
        }

        /// <summary>A walk's way through `points` (where it is, then the tiles' middles), its corners rounded (unitViews.ts roundedWay).</summary>
        sealed class Way
        {
            public List<Vector3> samples;
            public List<float> along;
            /// <summary>How far along each point after the first is passed (its corner's middle).</summary>
            public List<float> marks;
            public float length;
        }

        static Way RoundedWay(IReadOnlyList<Vector3> points)
        {
            var samples = new List<Vector3> { points[0] };
            var marks = new List<int>();
            float Flat(Vector3 a, Vector3 b) => Mathf.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 a = points[i - 1], b = points[i];
                bool more = i + 1 < points.Count;
                float inner = Flat(a, b), outer = more ? Flat(b, points[i + 1]) : 0;
                float r = more ? Mathf.Min(WalkRound * MapSpace.Tile, inner / 2, outer / 2) : 0;
                if (r < 1e-3f)
                {
                    samples.Add(b);
                    marks.Add(samples.Count - 1);
                    continue;
                }
                var p0 = Vector3.Lerp(b, a, r / inner);
                var p2 = Vector3.Lerp(b, points[i + 1], r / outer);
                samples.Add(p0);
                for (int j = 1; j <= WalkRoundSteps; j++)
                {
                    float t = (float)j / WalkRoundSteps;
                    samples.Add(p0 * ((1 - t) * (1 - t)) + b * (2 * t * (1 - t)) + p2 * (t * t));
                    if (j == WalkRoundSteps / 2) marks.Add(samples.Count - 1);
                }
            }
            var along = new List<float> { 0 };
            for (int k = 1; k < samples.Count; k++) along.Add(along[k - 1] + Flat(samples[k - 1], samples[k]));
            return new Way { samples = samples, along = along, marks = marks.Select(k => along[k]).ToList(), length = along[along.Count - 1] };
        }

        /// <summary>Where along `way`, `d` from its start, and the way it heads there (unitViews.ts onWay).</summary>
        static (Vector3 at, Vector3 heading) OnWay(Way way, float d)
        {
            int lo = 0, hi = way.along.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (way.along[mid] <= d) lo = mid;
                else hi = mid;
            }
            if (hi == lo) return (way.samples[lo], Vector3.zero);
            float span = way.along[hi] - way.along[lo];
            var at = Vector3.Lerp(way.samples[lo], way.samples[hi], span > 1e-6f ? Mathf.Clamp01((d - way.along[lo]) / span) : 1);
            return (at, way.samples[hi] - way.samples[lo]);
        }

        /// <summary>
        /// The weapon raised at `at` (the pistol drawn from its holster, `sidearm`: unitViews.ts raiseWeapon); its number,
        /// for LowerWeapon. Already up (aimed as its target was picked, or on watch), the shot goes on from there at once.
        /// </summary>
        public async Awaitable<int> RaiseWeapon(string id, Vector3 at, bool sidearm = false, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v)) return 0;
            int aim = ++v.aims;
            v.picking = null;
            Face(id, at);
            // Whoever stands where it aims takes the blow from here (unitViews.ts raiseWeapon: struck).
            Struck(at, v.root.position, false);
            if (v.weapon == null)
            {
                await Awaitable.WaitForSecondsAsync(0.35f, ct);
                return aim;
            }
            string carry = sidearm ? SidearmCarry : MainCarry;
            bool drawn = v.drawn != carry;
            if (drawn) Draw(v, carry);
            // How close it is: at point blank the figure pulls a long gun back (StepBack).
            v.close = Flat(at - v.root.position) / MapSpace.Tile;
            v.aiming = true;
            float from = drawn ? 0 : v.up;
            Settle(v, AimBlend * (1 - from));
            if (from < 0.99f) await Awaitable.WaitForSecondsAsync(0.35f * (1 - from), ct);
            return aim;
        }

        /// <summary>
        /// Down from aiming (unitViews.ts lowerWeapon): `aim` (RaiseWeapon's) only, not one taken since; to the watch's aim
        /// if it is on watch. Its main weapon (the blade, on guard) drawn again once lowered, the step back gone.
        /// </summary>
        public async Awaitable LowerWeapon(string id, int aim, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || aim != v.aims) return;
            v.aiming = false;
            await Lowered(v, aim, ct);
        }

        /// <summary>
        /// To how it stands now, and 0.3 s on (if `aim` is still its last), the weapon it stands with drawn again; not while
        /// it strikes with the blade (29/10, Red in Ana's app: the target picked and the blow confirmed, the rifle came back
        /// into the hands halfway through the chainsaw's stroke, the chainsaw hung up again): the stroke draws it after.
        /// </summary>
        async Awaitable Lowered(View v, int aim, CancellationToken ct = default)
        {
            if (v.weapon != null && !v.striking) Settle(v, 0.3f);
            await Awaitable.WaitForSecondsAsync(0.3f, ct);
            if (aim != v.aims || v.root == null || v.aiming || v.striking || v.picking != null) return;
            v.close = 0;
            string carry = v.guard ? BeltCarry : MainCarry;
            if (v.weapon != null && v.drawn != carry)
            {
                Draw(v, carry);
                Settle(v, 0.2f);
            }
        }

        static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

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
            // A blow at arm's length, from where it lunges from (unitViews.ts lunge: struck).
            if (t != null && views.TryGetValue(id, out var striker)) StruckBy(t, striker.root.position, true);
            // The dead lunge as Mixamo's zombie headbutts, over their upper body, from HeadbuttLead into it (its blow 0.45 s in
            // lands on lunge's own, 0.34 s: WindUp and StrokeTime); one that kills
            // and eats takes the body by the neck and bites (FEED_CLIPS bite), and holds on to it for Feed.
            if (eats && views.TryGetValue(id, out var e) && e.deadKind && e.anim != null && Anims.Feed("bite") is var (biteId, biteFrom, biteTo)
                && FigureCatalog.Load()?.Clip(biteId) is AnimationClip bite)
            {
                e.home = e.root.position;
                float seconds = e.anim.PlayWholePart(bite, biteFrom, biteTo, 0.15f, false);
                // Into the neck as the clip leans the head in (unitViews.ts lunge: BITE_GAP short of its middle, there by
                // 0.62 of the clip), and it holds on: Feed goes on from there, or it lets go 2 s on (LetGo).
                var reach = t != null ? Reach(e.home, t.root.position, BiteGap, 1) : Vector3.zero;
                int bit = ++e.bites;
                _ = Approach(e, e.home, reach, seconds, bit, ct);
                await Awaitable.WaitForSecondsAsync(0.55f, ct);
                onHit?.Invoke();
                await Awaitable.WaitForSecondsAsync(Mathf.Max(0.2f, biteTo - biteFrom - 0.55f - 0.3f), ct);
                return;
            }
            if (views.TryGetValue(id, out var v) && v.deadKind && v.anim != null && FigureCatalog.Load()?.Clip("zombieHeadbutt") is AnimationClip butt)
            {
                v.anim.PlayUpper(butt, 0.1f, 1, HeadbuttLead);
                // A step in as the blow lands and back (unitViews.ts lunge: LUNGE_SHORT short of its middle, LUNGE at most):
                // still while it winds up, in over the 0.14 s before the clip's blow, back over 0.35 s.
                var home = v.root.position;
                var reach = t != null ? Reach(home, t.root.position, LungeShort, LungeMost) : Vector3.zero;
                // (The arms with it, as lunge's pose: up as it winds up, closing on the blow, down as it goes back.)
                _ = LungeArms(v, ct);
                await Slide(v, home, home, WindUp, ct);
                await Slide(v, home, home + reach, StrokeTime, ct);
                onHit?.Invoke();
                // (Resolved as the blow lands, unitViews.ts lunge: the step back goes on by itself.)
                _ = StepHome(v, home + reach, home, ct);
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
            FlashHit(v);
            if (v.anim != null)
            {
                // The hit that goes with its last blow (UnitViews.Blows HitFor): from its way, a critical hard or to the head.
                var chosen = HitFor(v, strength);
                var hit = chosen != null ? FigureCatalog.Load()?.Clip(chosen.id) : null;
                if (hit != null)
                {
                    // Its share of the upper body (syntyFigures.ts react: a critical all of it, a hit 0.85, a graze half), as
                    // three's mixer averaged it with what else plays there: a gun held at rest or aimed (direct, 0.99 of the
                    // pose between its clips) leaves a soldier's hit a twentieth of it (a stagger), the dead's much of it.
                    float share = Mathf.Min(0.95f, strength > 1 ? 1 : strength >= 1 ? 0.85f : 0.5f);
                    float own = share / (1 - share), held = v.inHand == Held.Rifle && !v.dead ? 0.99f / 0.01f : 0;
                    float length = v.anim.PlayUpperShare(hit, 0.06f, 0.2f, own / (own + held + 1));
                    _ = Knock(v, strength, ct);
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


        /// <summary>unitViews.ts hitReact's knock-back with a clip (syntyFigures.ts: 0.12 of pose.hit): the body back a little over 0.4 s, the jolt.</summary>
        static async Awaitable Knock(View v, float strength, CancellationToken ct)
        {
            try
            {
                for (float t = 0; t < 0.4f && v.body != null && !v.dead; t += Time.deltaTime)
                {
                    float hit = Mathf.Sin(Mathf.Min(1, t / 0.4f * 1.6f) * Mathf.PI) * 0.6f * strength;
                    v.body.localRotation = Quaternion.Euler(-hit * 0.12f * Mathf.Rad2Deg, 0, 0);
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            if (v.body != null && !v.dead) v.body.localRotation = Quaternion.identity;
        }

        public async Awaitable Die(string id, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v)) return;
            FlashDeath(v);
            if (v.anim != null)
            {
                v.dead = true;
                // The death that goes with its last blow (UnitViews.Blows: away from it, room for the body).
                var chosen = DeathFor(v, out bool cramped);
                var death = chosen != null ? FigureCatalog.Load()?.Clip(chosen.id) : null;
                v.deathId = chosen?.id;
                if (death != null)
                {
                    // syntyAnims.ts DEATH_BLEND; seen whole, until it lies still (settleOf).
                    float length = v.anim.PlayWhole(death, 0.2f, true);
                    float settle = Mathf.Min(length, FigureCatalog.Load()?.Settle(chosen.id) ?? length);
                    if (cramped) await Tuck(v, chosen, settle, ct);
                    else await Awaitable.WaitForSecondsAsync(settle, ct);
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

        /// <summary>A blow at a siege's door or barricade on `tile`; `onHit` as it lands.</summary>
        public async Awaitable Smash(string id, Pos tile, Action onHit = null, CancellationToken ct = default)
        {
            // (unitViews.ts smash: the dead one lunges at it as at a unit, A5's headbutt and its step in. Resolves as the blow
            // lands; the gate shakes then (MapView.Knock), and the step back goes on by itself.)
            if (state != null && views.TryGetValue(id, out var v) && v.deadKind && v.anim != null && FigureCatalog.Load()?.Clip("zombieHeadbutt") is AnimationClip butt)
            {
                var at = MapSpace.StandAt(state, tile);
                Face(id, at);
                v.anim.PlayUpper(butt, 0.1f, 1, HeadbuttLead);
                var home = v.root.position;
                var reach = Reach(home, at, LungeShort, LungeMost);
                _ = LungeArms(v, ct);
                await Slide(v, home, home, WindUp, ct);
                await Slide(v, home, home + reach, StrokeTime, ct);
                onHit?.Invoke();
                MapView.Current?.Knock(tile);
                _ = StepHome(v, home + reach, home, ct);
                return;
            }
            if (state != null) Face(id, MapSpace.TileToWorld(state, tile));
            await Awaitable.WaitForSecondsAsync(0.45f, ct);
            onHit?.Invoke();
            MapView.Current?.Knock(tile);
            await Awaitable.WaitForSecondsAsync(0.25f, ct);
        }

        /// <summary>Back from a blow to where it struck from (unitViews.ts lunge's step back, 0.35 s).</summary>
        static async Awaitable StepHome(View v, Vector3 from, Vector3 home, CancellationToken ct)
        {
            try
            {
                await Slide(v, from, home, 0.35f, ct);
            }
            catch (System.OperationCanceledException) { }
            if (v.root != null) v.root.position = home;
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
            // (unitViews.ts reanimate: the body twitches where it lies, three times, 0.13 s, 0.16 and 0.19; as the body's
            // pose is its death's, its hit doesn't move it there: it lies still that long.) Then it goes.
            views.TryGetValue(corpse, out var body);
            if (body != null) await Awaitable.WaitForSecondsAsync(0.13f + 0.16f + 0.19f, ct);
            if (body != null) body.root.gameObject.SetActive(false);
            var u = state?.Units.FirstOrDefault(x => x.Id == zombie);
            if (!views.TryGetValue(zombie, out var v))
            {
                if (u == null)
                {
                    await Awaitable.WaitForSecondsAsync(0.9f, ct);
                    return;
                }
                views[zombie] = v = Make(u);
            }
            // The dead it has become gets up stiffly in its place, out of the body's death played backwards (the same
            // clip), 0.8 of it and 0.9 s at least.
            v.root.position = u != null ? MapSpace.StandAt(state, u.Pos) : body.root.position;
            if (body != null) v.root.rotation = body.root.rotation;
            v.root.gameObject.SetActive(true);
            v.synced = true;
            var death = body?.deathId != null ? FigureCatalog.Load()?.Clip(body.deathId) : null;
            if (v.anim == null || death == null)
            {
                await Awaitable.WaitForSecondsAsync(0.9f, ct);
                return;
            }
            v.anim.PlayWhole(death, 0, true, death.length, 0);
            float seconds = Mathf.Max(0.9f, death.length * 0.8f);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                v.anim.WholeAt(death.length * (1 - t / seconds));
                await Awaitable.NextFrameAsync(ct);
            }
            v.anim.WholeAt(0);
            v.anim.StopWhole(0.25f);
        }

        public async Awaitable Alert(string id, CancellationToken ct = default) => await Awaitable.WaitForSecondsAsync(0.4f, ct);

        static void DestroyNow(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
